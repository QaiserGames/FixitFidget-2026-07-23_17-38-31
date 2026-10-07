using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// WHAT E (AND THE OTHER VERBS) ACT ON
//
// STATIONS AS REACH (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2). Mansoor: too much button
// pressing at the counter, the bench and the espresso machine, in both cameras. Every station used to be a room: F (or
// X) docked Ace, teleported him to a stand point, swapped to the station's camera, stopped his legs and locked the
// cursor; F or Esc undocked. Now nothing is stepped up to:
//
//   * the counter: behind it (BehindCounter), E talks: in first person to whoever the crosshair is on (they're further
//     than an arm's length, across the counter: Counter Reach); from above, to whoever has waited longest (the prompt
//     says their name). The conversation's own camera is as it was;
//   * the drinks: in first person the dispenser's controls work on Ace's own crosshair (left click / LB the left hand,
//     right click / RB the right, E either); from above, E at the dispenser opens its close-up (pouring can't be read
//     from twenty metres up: StationCloseUp), and walking, B or Esc leave it. That close-up is the one "station" left:
//     currentStation, IsAtStation;
//   * the bench: in first person, click or RT on a device works on it (the inspection close-up), E picks it up; from
//     above, E on a device works on it until it's finished, and picks it up once it is ("Pick up (Fixed)");
//   * F is retired by day (at night it's the torch, NightTorch).
//
// InCloseUp is the one word for "Ace is busy" (a conversation, the drinks close-up, an item at the bench, the counter
// phone): Ace's legs stop (PlayerMovement), and the HUD, the badges and the name tags follow it.
//
// Aim help on a pad (AimAssist): which things the views may draw the crosshair to is decided here (AimTargets), by the
// same rules as what the crosshair picks (FindBest), and D-pad left / right step between them.
// ---------------------------------------------------------------------------
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float reach = 2.2f;
    [Tooltip("The drinks close-up's crosshair reaches this far (metres).")]
    [SerializeField] private float stationReach = 10f;

    [Tooltip("In the overhead view, how close to the dispenser's stand point E opens its close-up (metres). Measured to " +
             "the stand point, not to the collider: see OverheadOffer.")]
    [SerializeField] private float stationEnterRange = 1.5f;

    [Header("The counter (stations as reach)")]
    [Tooltip("Behind the counter: within this distance of the counter's stand point, on the staff side (metres). The " +
             "queue's three slots are 1.6 m apart, so the outer ones are about 2.5 m from the till.")]
    [SerializeField] private float counterRange = 2.5f;
    [Tooltip("First person, behind the counter: how far the crosshair reaches someone waiting across it (metres).")]
    [SerializeField] private float counterReach = 4f;

    private Interactable focused;
    private StationInteractable currentStation;
    private PlayerMovement movement;
    private Renderer bodyRenderer;
    private Camera cam;
    private ConversationController conversation;
    private CounterRepairView counterRepair;
    private StationInteractable[] allStations;
    private StationInteractable counterStation, drinksStation;
    private StationCloseUp drinksOffer;
    private CounterQueue queue;
    private ItemInspector inspector;
    private PlayerCarry carry;
    private CafeViewMode viewMode;
    private int lastInteractionFrame = -1;
    private int lastBackFrame = -1;
    // The drinks close-up: walking leaves it once the walking keys have been let go after stepping in (they may still be
    // held from walking up to it).
    private bool leaveArmed;
    private bool wasUsingDrinks;
    // Behind the counter, worked out once a frame.
    private int behindFrame = -1;
    private bool behind;

    /// <summary>In the drinks close-up (the overhead view's one station; "docked" before playtest 3).</summary>
    public bool IsAtStation => currentStation != null;
    public bool IsAtBeverageStation => currentStation != null && currentStation.GetComponent<BeverageStation>() != null;
    public Interactable Focused => focused;
    public string CurrentPrompt { get; private set; }
    /// <summary>The second verb, on click or RT ("Work on it": a device on the bench, in first person), or "".</summary>
    public string WorkPrompt { get; private set; } = "";
    public string DebugInfo { get; private set; }
    public StationInteractable CurrentStation => currentStation;
    /// <summary>The counter's station (its drop spot is the intake shelf) and the dispenser's.</summary>
    public StationInteractable CounterStation => counterStation;
    public StationInteractable DrinksStation => drinksStation;
    static bool NightIsOn => NightWalk.Instance != null && NightWalk.Instance.Active;

    /// <summary>
    /// Ace is busy with something that has the screen: a conversation, the drinks close-up, an item at the bench, or the
    /// counter phone. His legs stop (E still works), and what's drawn over the café keeps out of the way.
    /// </summary>
    public bool InCloseUp => currentStation != null
        || conversation != null && conversation.InConversation
        || inspector != null && inspector.IsHoldingItem
        || counterRepair != null && counterRepair.OwnsInput;

    /// <summary>
    /// Ace is behind the counter: within Counter Range of its stand point, and not past it toward the customers. From
    /// here E talks to the people waiting at the counter.
    /// </summary>
    public bool BehindCounter
    {
        get
        {
            if (behindFrame == Time.frameCount) return behind;
            behindFrame = Time.frameCount;
            behind = IsBehindCounter(transform.position);
            return behind;
        }
    }

    /// <summary>Whether a point (Ace's middle) counts as behind the counter.</summary>
    public bool IsBehindCounter(Vector3 at)
    {
        if (counterStation == null || counterStation.StandPoint == null) return false;
        Vector3 stand = counterStation.StandPoint.position;
        Vector3 d = at - stand;
        d.y = 0f;
        if (d.magnitude > counterRange) return false;
        // Toward the customers: from the till to the middle of the queue (the stand point's own facing as a fallback).
        Vector3 toward = TowardCustomers();
        return Vector3.Dot(d, toward) <= .5f;
    }

    // The direction from the till toward the people waiting, flat.
    private Vector3 TowardCustomers()
    {
        Vector3 stand = counterStation.StandPoint.position;
        Vector3 toward = counterStation.StandPoint.forward;
        if (queue != null && queue.SlotCount > 0)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Transform slot = queue.SlotPoint(i);
                if (slot == null) continue;
                sum += slot.position;
                n++;
            }
            if (n > 0) toward = sum / n - stand;
        }
        toward.y = 0f;
        return toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.forward;
    }

    /// <summary>
    /// The crosshair is on the dispenser (a cup pad, a paddle, the cup stack, the discard tray, or a cup in one of its
    /// pads), walking in first person: the hands at the drinks (left click / LB, right click / RB) are the dispenser's.
    /// </summary>
    public bool AimingAtDrinks => viewMode != null && viewMode.WalkingFirstPerson && IsDrinksThing(focused);

    /// <summary>Ace stands at the dispenser (within half a metre past Station Enter Range of its stand point), in either
    /// view: the Day 1 guide's words follow it (a steadier test than the crosshair).</summary>
    public bool NearDrinks
    {
        get
        {
            if (drinksStation == null || drinksStation.StandPoint == null) return false;
            Vector3 d = transform.position - drinksStation.StandPoint.position;
            d.y = 0f;
            return d.magnitude <= stationEnterRange + .5f;
        }
    }

    /// <summary>The drinks' hands are in use: the close-up, or aiming at the dispenser in first person.</summary>
    public bool UsingDrinks => IsAtBeverageStation || AimingAtDrinks;

    /// <summary>Part of the dispenser: one of its controls, or a cup standing in one of its pads.</summary>
    public static bool IsDrinksThing(Interactable thing)
    {
        if (thing == null) return false;
        if (thing is BeverageControl || thing is BeverageCupSupply) return true;
        if (thing is ItemInteractable item && item.Job is DrinkJob cup)
            foreach (BeverageSlot slot in BeverageSlots)
                if (slot != null && slot.Cup == cup) return true;
        return false;
    }

    static BeverageSlot[] beverageSlots;
    static BeverageSlot[] BeverageSlots
    {
        get
        {
            if (beverageSlots == null || beverageSlots.Length == 0 || beverageSlots[0] == null)
                beverageSlots = FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude);
            return beverageSlots;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => beverageSlots = null;

    private void Awake()
    {
        conversation = GetComponent<ConversationController>();
        counterRepair = GetComponent<CounterRepairView>();
        if (counterRepair == null) counterRepair = gameObject.AddComponent<CounterRepairView>();
        movement = GetComponent<PlayerMovement>();
        inspector = GetComponent<ItemInspector>();
        carry = GetComponent<PlayerCarry>();
        viewMode = GetComponent<CafeViewMode>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        cam = Camera.main;

        // Stations don't spawn at runtime, so find them once instead of
        // sweeping physics every frame.
        allStations = FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude);
        foreach (StationInteractable s in allStations)
        {
            if (s == null || s.StandPoint == null) continue;
            if (s.GetComponent<BeverageStation>() != null) { if (drinksStation == null) drinksStation = s; }
            else if (!s.IsWorkSurface && counterStation == null) counterStation = s;
        }
        queue = FindAnyObjectByType<CounterQueue>();
        // The dispenser's close-up, offered from above (made while playing only: an edit-mode check never adds to a scene).
        if (Application.isPlaying && drinksStation != null) drinksOffer = StationCloseUp.For(drinksStation);
    }

    private void Update()
    {
        // A night walk: the café is closed, and only the night's own things are
        // offered (NightInteractable: the Night 1 slice).
        if (NightIsOn) { NightUpdate(); return; }

        // Paused or the day over: nothing is offered and nothing can be used.
        if (Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            ClearFocus();
            return;
        }

        // Controller B. Also bound to PlayerInput's Back action; the frame
        // guard in OnBack keeps the two from peeling two layers at once.
        if (PadInput.Pressed(PadButton.East)) OnBack();

        // The conversation owns input while it's open.
        if (conversation != null && conversation.InConversation)
        {
            ClearFocus();
            return;
        }

        // So does a scene that holds Ace still (Barks): its E moves the scene on.
        if (PlayerMovement.Held)
        {
            ClearFocus();
            return;
        }

        if (viewMode != null && viewMode.SuppressWalkingInteraction)
        {
            ClearFocus();
            return;
        }

        if (counterRepair != null && counterRepair.OwnsInput || inspector != null && inspector.IsHoldingItem)
        {
            if (focused != null) focused.SetFocused(false);
            focused = null;
            CurrentPrompt = inspector != null && inspector.IsHoldingItem ? inspector.CollectionPrompt : "";
            WorkPrompt = "";
            // Controller A collects the inspected item, exactly like E.
            if (PadInput.Pressed(PadButton.South)) PerformInteraction(-1);
            return;
        }

        // The drinks close-up: walking (the left stick or WASD) leaves it.
        if (currentStation != null && LeaveByWalking()) ExitStation();

        Interactable next = FindBest();
        if (next != focused)
        {
            if (focused != null) focused.SetFocused(false);
            focused = next;
            if (focused != null) focused.SetFocused(true);
        }

        CurrentPrompt = PromptFor(focused);
        WorkPrompt = WorkPromptFor(focused);

        // The drinks' hands: left click or LB uses the left hand, right click or RB the right (E chooses by itself).
        bool usingDrinks = UsingDrinks;
        if (wasUsingDrinks && !usingDrinks && carry != null) carry.UseAutomaticHand();
        wasUsingDrinks = usingDrinks;
        Mouse mouse = Mouse.current;
        if (usingDrinks)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame || PadInput.Pressed(PadButton.LeftShoulder))
                PerformInteraction(0);
            else if (mouse != null && mouse.rightButton.wasPressedThisFrame || PadInput.Pressed(PadButton.RightShoulder))
                PerformInteraction(1);
        }
        // A device on the bench, in first person: click or RT works on it.
        else if (viewMode != null && viewMode.WalkingFirstPerson && focused is ItemInteractable bench && bench.OnBench
                 && GamePointer.PrimaryPressed && lastInteractionFrame != Time.frameCount)
        {
            lastInteractionFrame = Time.frameCount;
            ClearFocus();
            WorkOn(bench);
            return;
        }

        // Controller A. PlayerInput's Interact action may already have run this
        // frame; PerformInteraction's frame guard makes the second call a no-op.
        if (PadInput.Pressed(PadButton.South)) PerformInteraction(-1);

        bool refuse = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame
            || PadInput.Pressed(PadButton.North);
        if (refuse && focused != null)
        {
            CustomerBrain b = focused.GetComponent<CustomerBrain>();
            if (b != null && b.CanRefuse) b.RefuseJob();
        }

        // D-pad left / right: the view steps to the next target that side (aim help, pad only).
        StepAim();

        DebugInfo = $"station:{(currentStation != null ? currentStation.name : "none")}  focus:{(focused != null ? focused.name : "NULL")}  behind:{BehindCounter}";
    }

    // ---------- what the crosshair, or Ace's reach, picks ----------

    private Interactable FindBest()
    {
        // ---- The drinks close-up: the station camera's crosshair ----
        if (currentStation != null) return FindStationTarget();

        if (viewMode != null && viewMode.WalkingFirstPerson) return FindFirstPersonFloorTarget();

        // ---- Overhead: nearest available wins, and the counter and the dispenser by where Ace stands ----
        int near = Physics.OverlapSphereNonAlloc(transform.position, reach, ColliderBuffer);
        Interactable best = FindFloorTargetAmong(ColliderBuffer, near);
        Interactable offer = OverheadOffer();
        if (offer != null && (best == null || best.Priority <= StationCloseUp.OfferPriority)) return offer;
        return best;
    }

    // The crosshair in the drinks close-up: the first available thing under it, seeing past solids that aren't
    // things to use (the dispenser's housing, the counter) up to Station Reach.
    private Interactable FindStationTarget()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, stationReach);
        System.Array.Sort(HitBuffer, 0, count, ByDistance);
        for (int i = 0; i < count; i++)
        {
            Interactable it = HitBuffer[i].collider.GetComponentInParent<Interactable>();
            if (it == null || it is StationInteractable || !it.IsAvailable) continue;
            return it;
        }
        return null;
    }

    /// <summary>
    /// What E offers from above beyond arm's reach, by where Ace stands: at the dispenser (within Station Enter Range of
    /// its stand point, measured to the stand point: a wide counter's collider is touched from metres away) its close-up;
    /// behind the counter, whoever has waited longest there. Null otherwise, and in first person (it aims).
    /// </summary>
    private Interactable OverheadOffer()
    {
        if (viewMode != null && viewMode.WalkingFirstPerson) return null;
        if (drinksOffer != null && drinksStation != null && drinksStation.StandPoint != null && drinksOffer.IsAvailable)
        {
            Vector3 d = transform.position - drinksStation.StandPoint.position;
            d.y = 0f;
            if (d.magnitude <= stationEnterRange) return drinksOffer;
        }
        if (BehindCounter) return QueueHead();
        return null;
    }

    /// <summary>
    /// Whoever at the counter has waited longest for Ace (to be heard, to have their mind made up for them, or for the
    /// counter phone): what E talks to from above, behind the counter. Null when nobody is waiting there.
    /// </summary>
    public CustomerInteractable QueueHead()
    {
        if (queue == null) return null;
        CustomerInteractable best = null;
        float earliest = float.PositiveInfinity;
        for (int i = 0; i < queue.SlotCount; i++)
        {
            CustomerBrain c = queue.Occupant(i);
            if (c == null || !c.isActiveAndEnabled || c.IsLeaving) continue;
            CustomerInteractable talk = c.GetComponent<CustomerInteractable>();
            if (talk == null || !talk.CounterBusiness || !talk.IsAvailable) continue;
            if (c.ArrivedAt < earliest) { earliest = c.ArrivedAt; best = talk; }
        }
        return best;
    }

    // The whole array (the checks hand one in by reflection, so this name and shape stay).
    private Interactable FindFloorTarget(Collider[] near) => FindFloorTargetAmong(near, near != null ? near.Length : 0);

    // Query buffers, so aiming allocates nothing frame after frame (the garbage brought a collection
    // every few seconds: a hitch). Sixty-four is more than the café ever has within Ace's reach.
    static readonly RaycastHit[] HitBuffer = new RaycastHit[64];
    static readonly Collider[] ColliderBuffer = new Collider[64];
    static readonly System.Collections.Generic.IComparer<RaycastHit> ByDistance = new HitDistanceComparer();
    sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // First person: what the crosshair is on, within reach. Someone waiting across the counter is further than an arm's
    // length, so behind the counter they're reached out to Counter Reach.
    private Interactable FindFirstPersonFloorTarget()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f));
        bool counter = BehindCounter;
        float far = counter ? Mathf.Max(reach, counterReach) : reach;
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, far, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(HitBuffer, 0, count, ByDistance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider.transform.IsChildOf(transform)) continue;
            Interactable item = hit.collider.GetComponentInParent<Interactable>();
            if (item != null && item.IsAvailable && Reachable(item, hit.distance, counter))
                return item;
            // Triggers can provide prompts, but solid furniture and walls stop
            // the view ray from selecting an item hidden on their other side.
            if (!hit.collider.isTrigger) return null;
        }
        return null;
    }

    // First person: whether the crosshair may pick this at this distance.
    private bool Reachable(Interactable item, float distance, bool counter)
    {
        if (item is StationCloseUp) return false;     // first person has no close-up
        if (item is CustomerInteractable customer)
        {
            if (counter && customer.CounterBusiness) return distance <= counterReach;
            return customer.FloorAvailable && distance <= reach;
        }
        return distance <= reach;
    }

    private Interactable FindFloorTargetAmong(Collider[] near, int count)
    {
        Interactable best = null;
        float bestScore = float.MinValue;

        for (int n = 0; n < count; n++)
        {
            Collider h = near[n];
            Interactable it = h.GetComponentInParent<Interactable>();
            if (it == null || !it.IsAvailable) continue;

            // Customers used to be skipped out here entirely, because intake is
            // a counter conversation and shouldn't start from across the room.
            // But once people wait away from the counter, you have to be able to
            // walk up and hand them things. So: only the non-intake actions
            // (serve, hand back, reassure) are reachable on the floor. (The
            // counter's own business is offered from behind it: OverheadOffer.)
            CustomerInteractable ci = it as CustomerInteractable;
            if (ci != null && !ci.FloorAvailable) continue;

            float score = it.Priority * 100f - Vector3.Distance(transform.position, it.transform.position);

            if (score > bestScore)
            {
                bestScore = score;
                best = it;
            }
        }

        // Override pickup only for a device that this reachable bench already
        // holds. Deliveries, intake, loose pickups and full benches are untouched.
        if (best is ItemInteractable)
        {
            if (carry == null) carry = GetComponent<PlayerCarry>();
            StationInteractable placement = null;
            float placementDistance = float.PositiveInfinity;
            for (int n = 0; n < count; n++)
            {
                Collider h = near[n];
                var station = h.GetComponentInParent<StationInteractable>();
                if (station == null || !station.PrefersPlacementOver(best, carry)) continue;
                float distance = Vector3.Distance(transform.position, station.transform.position);
                if (distance < placementDistance) { placementDistance = distance; placement = station; }
            }
            if (placement != null) return placement;
        }
        return best;
    }

    // ---------- the words on the HUD ----------

    // What E would do: a device on the bench says what it does in this view (worked on from above until it's finished).
    private string PromptFor(Interactable thing)
    {
        if (thing == null) return "";
        if (thing is ItemInteractable item && item.OnBench && !(viewMode != null && viewMode.WalkingFirstPerson) && !item.Fixed)
            return WorkOnLabel(item);
        return thing.Prompt;
    }

    // The second verb (click or RT): a device on the bench, in first person.
    private string WorkPromptFor(Interactable thing) =>
        thing is ItemInteractable item && item.OnBench && viewMode != null && viewMode.WalkingFirstPerson ? "Work on it" : "";

    private static string WorkOnLabel(ItemInteractable item)
    {
        string subject = item.Job != null && item.Job.Record != null ? item.Job.Record.Subject : "";
        return string.IsNullOrEmpty(subject) ? "Work on it" : $"Work on the {subject}";
    }

    // ---------- doing it ----------

    // E — pick up, set down, accept, hand back; from above, also work on a device and open the drinks close-up.
    private void OnInteract() => PerformInteraction(-1);
    private void PerformInteraction(int hand)
    {
        if (lastInteractionFrame == Time.frameCount || Time.timeScale <= 0) return;
        // A scene holding Ace still (Barks) owns E: it moves the scene on, and the press that ended it was its too.
        if (PlayerMovement.Held || Barks.SceneEndedFrame == Time.frameCount) return;
        if (NightIsOn) { NightInteract(); return; }
        if (viewMode != null && viewMode.SuppressWalkingInteraction) return;
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return;
        if (conversation != null && conversation.InConversation) return;
        if (counterRepair != null && counterRepair.OwnsInput) return;
        if (inspector != null && inspector.IsHoldingItem)
        {
            lastInteractionFrame = Time.frameCount;
            inspector.TryCollectInspectedItem();
            return;
        }
        if (carry == null) carry = GetComponent<PlayerCarry>();
        // Input callbacks can run before Update; resolve the current pointer and
        // carrying state now rather than acting on last frame's highlighted cup.
        Interactable target = FindBest();
        if (carry != null)
        {
            // The drinks' hands: the one asked for, or either. Anywhere else an earlier choice of hand is forgotten.
            if ((IsAtBeverageStation || viewMode != null && viewMode.WalkingFirstPerson && IsDrinksThing(target)) && hand >= 0)
                carry.SelectHand(hand);
            else carry.UseAutomaticHand();
        }
        if (target == null || !target.IsAvailable) { ClearFocus(); return; }
        lastInteractionFrame = Time.frameCount;
        ClearFocus();
        focused = target;
        focused.SetFocused(true);
        // From above, E on a device on the bench works on it, until it's finished.
        if (target is ItemInteractable item && item.OnBench && !item.Fixed && !(viewMode != null && viewMode.WalkingFirstPerson))
        {
            ClearFocus();
            WorkOn(item);
            return;
        }
        focused.Interact(this);
        ClearFocus();
    }

    /// <summary>Works on a device on the bench: the inspection close-up (ItemInspector). False when it can't start.</summary>
    public bool WorkOn(ItemInteractable item)
    {
        if (inspector == null || item == null || item.Job == null || !item.OnBench) return false;
        return inspector.BeginInspection(item.Job);
    }

    // ---------- at night (the Night 1 slice) ----------

    // Only the night's own things, with the day's reach and rules: the nearest from
    // above, what the crosshair is on in first person (and, either way, a place Ace
    // stands in: NightInteractable.IsZone). E, or A on a controller.
    private void NightUpdate()
    {
        WorkPrompt = "";
        // Paused, the cursor released, or a scene holding Ace still (Barks: its E moves the scene on).
        if (Time.timeScale <= 0 || viewMode != null && viewMode.SuppressWalkingInteraction || PlayerMovement.Held)
        {
            ClearFocus();
            return;
        }
        Interactable next = FindNightTarget();
        if (next != focused)
        {
            if (focused != null) focused.SetFocused(false);
            focused = next;
            if (focused != null) focused.SetFocused(true);
        }
        // Under something low with the sneak let go: Ace stays crouched until there's room (PlayerMovement).
        CurrentPrompt = focused != null ? focused.Prompt
            : movement != null && movement.TooLowToStand ? "Too low to stand" : "";
        if (PadInput.Pressed(PadButton.South)) PerformInteraction(-1);
    }

    private void NightInteract()
    {
        if (viewMode != null && viewMode.SuppressWalkingInteraction) return;
        Interactable target = FindNightTarget();
        if (target == null || !target.IsAvailable) { ClearFocus(); return; }
        lastInteractionFrame = Time.frameCount;
        ClearFocus();
        target.Interact(this);
    }

    private Interactable FindNightTarget()
    {
        if (viewMode != null && viewMode.WalkingFirstPerson)
        {
            // What the crosshair is on first; failing that, a place Ace is standing in (the café's
            // doorway: a ray from a camera inside its trigger never hits it).
            NightInteractable seen = FindNightTargetInView();
            return seen != null ? seen : FindNightTargetNear(zonesOnly: true);
        }
        return FindNightTargetNear(zonesOnly: false);
    }

    private NightInteractable FindNightTargetInView()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f));
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(HitBuffer, 0, count, ByDistance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider.transform.IsChildOf(transform)) continue;
            NightInteractable thing = hit.collider.GetComponentInParent<NightInteractable>();
            if (thing != null && thing.IsAvailable) return thing;
            if (!hit.collider.isTrigger) return null;
        }
        return null;
    }

    // The nearest (by priority, then distance) within reach of Ace; only places, when asked.
    private NightInteractable FindNightTargetNear(bool zonesOnly)
    {
        NightInteractable best = null;
        float bestScore = float.MinValue;
        int count = Physics.OverlapSphereNonAlloc(transform.position, reach, ColliderBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int n = 0; n < count; n++)
        {
            Collider near = ColliderBuffer[n];
            NightInteractable thing = near.GetComponentInParent<NightInteractable>();
            if (thing == null || zonesOnly && !thing.IsZone || !thing.IsAvailable) continue;
            float score = thing.Priority * 100f - Vector3.Distance(transform.position, thing.transform.position);
            if (score > bestScore) { bestScore = score; best = thing; }
        }
        return best;
    }

    private void ClearFocus()
    {
        if (focused != null) focused.SetFocused(false);
        focused = null; CurrentPrompt = ""; WorkPrompt = "";
    }

    public string FocusName
    {
        get
        {
            if (focused == null) return "";
            CustomerBrain b = focused.GetComponent<CustomerBrain>();
            return b != null ? b.CustomerName : "";
        }
    }

    // ---------- leaving ----------

    // Esc, or B on a controller. Back peels ONE layer per press: the counter
    // phone first, then the item being worked on (its tool before the item),
    // and only then the drinks close-up.
    private void OnBack()
    {
        // Esc arrives through PlayerInput and B through Update as well.
        if (lastBackFrame == Time.frameCount) return;
        if ((conversation != null && conversation.InConversation)
            || (DayClock.Instance != null && DayClock.Instance.DayOver)) return;
        lastBackFrame = Time.frameCount;
        if (counterRepair != null && counterRepair.IsOpen) { counterRepair.Close(); return; }
        if (inspector != null && inspector.IsHoldingItem) { inspector.StepBack(); return; }
        ExitStation();
    }

    // The drinks close-up: the walking keys or the left stick, once they've been let go since stepping in.
    private bool LeaveByWalking()
    {
        Vector2 walk = movement != null ? movement.WalkIntent : Vector2.zero;
        bool walking = walk.sqrMagnitude > .25f;
        if (!leaveArmed)
        {
            if (!walking) leaveArmed = true;
            return false;
        }
        return walking;
    }

    // The drinks close-up from above (StationCloseUp); the checks may put Ace at any station this way.
    public void EnterStation(StationInteractable station)
    {
        if (station == null || (DayClock.Instance != null && DayClock.Instance.DayOver)) return;
        currentStation = station;
        lastInteractionFrame = Time.frameCount;
        leaveArmed = false;

        // Stand in the same place every time, so the view is always composed
        // the same way and the crosshair can always reach the work surface.
        if (station.StandPoint != null)
        {
            // TAKE THE FOOTPRINT, KEEP OUR OWN HEIGHT.
            //
            // CounterStandPoint sits at y = 0 — floor level — and so does the
            // Workbench's. But the player's CharacterController is 2 m tall
            // with its centre on the transform origin, so the transform
            // belongs at y = 1 for the capsule's feet to reach the floor.
            // Teleporting to the stand point's y buried the body exactly one
            // metre in the floor, leaving the top dome poking through.
            //
            // Ignoring the stand point's height means nobody has to remember
            // to author it at 1.0 — which is why the bench had the same bug.
            // The cost is that a station on a raised platform would need its
            // height from somewhere else. Fine for a flat shop.
            Vector3 target = station.StandPoint.position;
            target.y = transform.position.y;

            // AND MOVE IT THE SUPPORTED WAY.
            //
            // A CharacterController keeps its own internal position.
            // Assigning transform.position behind its back leaves the two
            // disagreeing until the next Move(). Disabling the controller
            // across the teleport is how Unity says to relocate one.
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = target;
            transform.rotation = station.StandPoint.rotation;

            if (cc != null) cc.enabled = true;
        }

        station.ActivateCamera(true);
        station.ResetView();
        // The legs stop because this is a close-up (PlayerMovement.StoppedByCloseUp), not because walking is switched
        // off: the keys held are kept, so walking away from it walks on at once.
        if (bodyRenderer != null) bodyRenderer.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ExitStation()
    {
        if (currentStation == null) return;
        lastInteractionFrame = Time.frameCount;
        if (counterRepair != null) counterRepair.Close();

        if (focused != null)
        {
            focused.SetFocused(false);
            focused = null;
        }

        currentStation.ActivateCamera(false);
        currentStation = null;
        if (carry != null) carry.UseAutomaticHand();
        if (movement != null) movement.enabled = true;
        if (bodyRenderer != null) bodyRenderer.enabled = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---------- aim help on a pad (AimAssist) ----------

    static readonly List<Interactable> SeenThings = new List<Interactable>(32);

    /// <summary>
    /// What the view may draw the crosshair to right now (the aim help): in first person, what's in reach and in sight by
    /// the crosshair's own rules; in the drinks close-up, the dispenser's controls and the cups in its pads. Empty
    /// anywhere else. Returns how many.
    /// </summary>
    public int AimTargets(List<AimAssist.Target> into)
    {
        into.Clear();
        if (cam == null) cam = Camera.main;
        if (cam == null) return 0;
        Vector3 eye = cam.transform.position;
        if (currentStation != null)
        {
            int n = Physics.OverlapSphereNonAlloc(eye, stationReach * .4f, ColliderBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            SeenThings.Clear();
            for (int i = 0; i < n; i++)
            {
                Collider c = ColliderBuffer[i];
                Interactable it = c.GetComponentInParent<Interactable>();
                if (it == null || it is StationInteractable || !it.IsAvailable || !IsDrinksThing(it) || SeenThings.Contains(it)) continue;
                SeenThings.Add(it);
                into.Add(TargetOf(it, c));
            }
            return into.Count;
        }
        if (viewMode == null || !viewMode.WalkingFirstPerson) return 0;
        bool counter = BehindCounter;
        float far = counter ? Mathf.Max(reach, counterReach) : reach;
        int count = Physics.OverlapSphereNonAlloc(eye, far, ColliderBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        SeenThings.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider c = ColliderBuffer[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;
            Interactable it = c.GetComponentInParent<Interactable>();
            if (it == null || SeenThings.Contains(it) || !it.IsAvailable) continue;
            AimAssist.Target t = TargetOf(it, c);
            float distance = Vector3.Distance(eye, c.bounds.ClosestPoint(eye));
            if (!Reachable(it, distance, counter)) continue;
            if (!InSight(eye, t.point, it)) continue;
            SeenThings.Add(it);
            into.Add(t);
        }
        return into.Count;
    }

    // Where to aim at a thing, and half its smallest width: a person at their chest (aiming at their middle would aim
    // into the counter), anything else at the middle of the collider that was found.
    private static AimAssist.Target TargetOf(Interactable it, Collider c)
    {
        Bounds b = c.bounds;
        if (it is CustomerInteractable)
        {
            CustomerBrain brain = it.GetComponent<CustomerBrain>();
            Vector3 chest = brain != null && brain.LookTarget != null
                ? brain.LookTarget.position - Vector3.up * .2f
                : b.center + Vector3.up * b.extents.y * .5f;
            return new AimAssist.Target(chest, Mathf.Min(b.extents.x, b.extents.z));
        }
        float size = Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z));
        return new AimAssist.Target(b.center, Mathf.Max(.03f, size));
    }

    // Nothing solid but the thing itself between the eye and the point.
    private bool InSight(Vector3 eye, Vector3 point, Interactable it)
    {
        Vector3 d = point - eye;
        float distance = d.magnitude;
        if (distance < .01f) return true;
        int count = Physics.RaycastNonAlloc(eye, d / distance, HitBuffer, distance + .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(HitBuffer, 0, count, ByDistance);
        for (int i = 0; i < count; i++)
        {
            Collider c = HitBuffer[i].collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<Interactable>() == it) return true;
            if (!c.isTrigger) return false;
        }
        return true;
    }

    static readonly List<AimAssist.Target> StepTargets = new List<AimAssist.Target>(32);

    // D-pad left / right, a pad in hand: turn to the next target that side (first person, or the drinks close-up).
    private void StepAim()
    {
        if (!AimAssist.Active) return;
        int direction = PadInput.Pressed(PadButton.DpadRight) ? 1 : PadInput.Pressed(PadButton.DpadLeft) ? -1 : 0;
        if (direction == 0) return;
        if (AimTargets(StepTargets) == 0) return;
        int next = AimAssist.Next(cam, StepTargets, direction);
        if (next < 0) return;
        Vector3 point = StepTargets[next].point;
        if (currentStation != null)
        {
            BeverageLook look = currentStation.GetComponentInChildren<BeverageLook>();
            if (look != null) look.StepAimTo(point);
        }
        else if (viewMode != null && viewMode.WalkingFirstPerson) viewMode.StepAimTo(point);
    }

    private void OnDrawGizmosSelected()
    {
        // Cyan = what you can pick up / hand over. Yellow = how close to the
        // dispenser's stand point E opens its close-up from above. Orange =
        // behind the counter.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);

        foreach (StationInteractable s in FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
        {
            if (s == null || s.StandPoint == null) continue;
            if (s.GetComponent<BeverageStation>() != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(s.StandPoint.position, stationEnterRange);
            }
            else if (!s.IsWorkSurface)
            {
                Gizmos.color = new Color(1f, .6f, .1f);
                Gizmos.DrawWireSphere(s.StandPoint.position, counterRange);
            }
        }
    }
}
