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
//
// ACROSS THE COUNTER (playtest 4, 10 Oct 2026; the repair bar). The café's redesign put the bench in the front counter,
// facing the room, with three stools in front of it. A customer waiting at a stool stands 3.1-3.3 m from the bench's
// stand point (2.4-2.6 m from the nearest place Ace can stand behind it), E reached 2.2 m, and the counter's front is
// solid from the west wall to x 4.7: the hand-back walked round its east end, about 20 m. A repair bar hands things over
// the counter, so now, behind the counter or behind the repair bar's bench (StaffSide), E reaches someone waiting in
// front of it within Across Counter Reach, measured flat:
//   * holding what they're waiting for - their finished device (whole, nothing left to fix: IsComplete) or their drink -
//     E hands it over, whatever else is nearer (even standing at the bench, where E would otherwise set it down);
//   * holding a device that isn't finished, or isn't theirs, E sets it down on the bench as before;
//   * otherwise (reassure them, say you're out of their drink) only when nothing else is in reach, so the bench's
//     close-up and the queue head behind the counter (OverheadOffer, QueueHead) are offered exactly as before;
//   * REASSURE FROM BEHIND THE BENCH (12 Oct 2026, the playtesters' #8, his pick): behind the repair bar's bench with
//     nothing in Ace's hands, E prefers a FRUSTRATED customer across the counter (the sweat drop's threshold, under 30%
//     patience: DayOneGuideUI.Frustrated, the same test the GETTING IMPATIENT card uses) over the device on the mat.
//     Before, a customer going under at a stool could only be reassured by walking round or in first person. With a
//     device in hand, or nobody frustrated in reach, the mat wins as before; at the till the queue head still comes
//     first; X in the close-up (take it away) is untouched.
// In first person the crosshair aims, so a customer in front of the counter is reachable out to Counter Reach.
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

    [Header("Across the counter (playtest 4: the repair bar)")]
    [Tooltip("Behind the counter, or behind the repair bar's bench, E reaches someone waiting in front of it this far " +
             "away, measured flat (metres): a hand over the counter. The repair bar's stools stand 3.1-3.3 m from the " +
             "bench's stand point.")]
    [SerializeField] private float acrossCounterReach = 3.5f;
    [Tooltip("Behind the repair bar's bench: within this distance of its stand point, and not past it toward the room " +
             "(metres). The bench is 3.2 m wide.")]
    [SerializeField] private float benchRange = 2f;

    private Interactable focused;
    private StationInteractable currentStation;
    private PlayerMovement movement;
    private Renderer bodyRenderer;
    private Camera cam;
    private ConversationController conversation;
    private CounterRepairView counterRepair;
    private StationInteractable[] allStations;
    private StationInteractable counterStation, drinksStation, benchStation;
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
    /// <summary>The repair bench (the work surface): since 10 Oct, the repair bar's bench in the front counter.</summary>
    public StationInteractable BenchStation => benchStation;
    /// <summary>How far E reaches across the counter, flat (metres; playtest 4).</summary>
    public float AcrossCounterReach => acrossCounterReach;
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

    // ---------- across the counter (playtest 4: the repair bar) ----------

    /// <summary>
    /// Ace is on the staff side of the counter: behind it at the till (BehindCounter), or behind the repair bar's bench
    /// (within Bench Range of its stand point, not past it toward the room). From there E reaches across the counter.
    /// </summary>
    public bool OnStaffSide => StaffSide(transform.position, out _, out _);

    // Where Ace stands on the staff side, and which way the customers are from there (flat): the till's stand point and
    // the way to the queue, or the bench's stand point and the way it faces (the bench's camera looks into the room).
    private bool StaffSide(Vector3 at, out Vector3 from, out Vector3 toward)
    {
        if (IsBehindCounter(at))
        {
            from = counterStation.StandPoint.position;
            toward = TowardCustomers();
            return true;
        }
        if (BehindTheBench(at, out toward))
        {
            from = benchStation.StandPoint.position;
            return true;
        }
        from = toward = Vector3.zero;
        return false;
    }

    // Behind the bench: on its side (not past its front toward the room), and near its stand point or (15 Oct) with the bench
    // itself in Ace's reach. Wherever E sets a device down on the bench or works on it, E reaches across it. Day 1 played by
    // hand: Ace walked from the till until the bench offered "Set down" and worked Grace's camera from there, at the till's
    // end of the bench; with her camera fixed in his hands E still said "Set down", never "Hand it back", and she left
    // "Never again" while he held it. The probe (Fixit Fidget > Checks > Probe - the bench's hand-over reach) mapped it:
    // from 119 of the 220 places the bench is in reach, E could not hand over to the bench's own stool.
    private bool BehindTheBench(Vector3 at, out Vector3 toward)
    {
        toward = Vector3.zero;
        if (benchStation == null || benchStation.StandPoint == null) return false;
        Transform stand = benchStation.StandPoint;
        Vector3 facing = Flat(stand.forward);
        if (facing.sqrMagnitude < 1e-6f) return false;
        Vector3 d = Flat(at - stand.position);
        if (Vector3.Dot(d, facing.normalized) > .5f) return false;   // past the bench, out in the room
        if (d.magnitude > benchRange && !BenchInReach(at)) return false;
        toward = facing.normalized;
        return true;
    }

    // The bench within E's reach from above (the overhead pick's own sphere): what offers "Set down" or "Work on it" there.
    private bool BenchInReach(Vector3 at)
    {
        int count = Physics.OverlapSphereNonAlloc(at, reach, BenchReachBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (BenchReachBuffer[i] != null && BenchReachBuffer[i].GetComponentInParent<Interactable>() == benchStation) return true;
        return false;
    }

    // How much further the crosshair reaches over the bench than over the till: the reach over the bench is measured from its
    // stand point, wherever along it Ace stands (15 Oct). 0 behind the till, or anywhere else.
    private float BenchSlack(bool behindBench) =>
        behindBench && benchStation != null && benchStation.StandPoint != null
            ? Flat(transform.position - benchStation.StandPoint.position).magnitude : 0f;

    /// <summary>Whether someone standing at <paramref name="at"/> is across the counter from Ace, within his reach over it.</summary>
    public bool AcrossTheCounter(Vector3 at)
    {
        // Over the till's counter, measured from Ace; over the bench, from its stand point (15 Oct; BehindTheBench).
        if (IsBehindCounter(transform.position) && Vector3.Dot(Flat(at - counterStation.StandPoint.position), TowardCustomers()) > .5f
            && Flat(at - transform.position).magnitude <= acrossCounterReach) return true;
        if (!BehindTheBench(transform.position, out Vector3 toward)) return false;
        Vector3 stand = benchStation.StandPoint.position;
        return Vector3.Dot(Flat(at - stand), toward) > .5f && Flat(at - stand).magnitude <= AcrossTheBenchReach;
    }

    // Someone waiting in front of the counter, within reach over it, nearest first. handOver: only someone waiting for
    // what Ace holds (their finished device, their drink: CustomerInteractable.HandOverReady); otherwise anyone E can help
    // from the floor but for handing back a device that isn't finished (that's set down on the bench, or a walk round).
    private CustomerInteractable AcrossTheCounterTarget(bool handOver) =>
        AcrossTheCounterTarget(handOver ? (System.Func<CustomerInteractable, bool>)(who => who.HandOverReady)
                                        : who => who.HelpAcrossTheCounter);

    // Over the till's counter the reach is measured from Ace; over the bench, from its stand point (15 Oct): the bench is one
    // work place, and wherever along it Ace reaches it, its stools are across it from him. At the till's end of the bench he
    // is behind both, and both count.
    private CustomerInteractable AcrossTheCounterTarget(System.Func<CustomerInteractable, bool> wanted)
    {
        CustomerInteractable best = null;
        float nearest = float.PositiveInfinity;
        if (IsBehindCounter(transform.position))
            AcrossFrom(counterStation.StandPoint.position, TowardCustomers(), transform.position, acrossCounterReach, wanted, ref best, ref nearest);
        if (BehindTheBench(transform.position, out Vector3 toward))
            AcrossFrom(benchStation.StandPoint.position, toward, benchStation.StandPoint.position, AcrossTheBenchReach, wanted, ref best, ref nearest);
        return best;
    }

    // Over the bench the reach covers the repair bar's three stools (20 Oct: the stations lab's customer sat on the east stool,
    // 3.75 m from the bench's stand point, and Ace couldn't reassure them from the bench; the middle one is 3.25 m).
    const float BenchAcrossExtra = .6f;
    float AcrossTheBenchReach => acrossCounterReach + BenchAcrossExtra;

    private void AcrossFrom(Vector3 from, Vector3 toward, Vector3 reachFrom, float reach, System.Func<CustomerInteractable, bool> wanted,
                            ref CustomerInteractable best, ref float nearest)
    {
        int count = Physics.OverlapSphereNonAlloc(reachFrom, reach, AcrossBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider c = AcrossBuffer[i];
            if (c == null) continue;
            CustomerInteractable who = c.GetComponentInParent<CustomerInteractable>();
            if (who == null || who == best || !who.IsAvailable) continue;
            if (!wanted(who)) continue;
            Vector3 at = who.transform.position;
            float flat = Flat(at - reachFrom).magnitude;
            if (flat > reach || flat >= nearest) continue;
            if (Vector3.Dot(Flat(at - from), toward) <= .5f) continue;   // not in front of the counter
            nearest = flat;
            best = who;
        }
    }

    /// <summary>Nothing in either of Ace's hands.</summary>
    public bool HandsEmpty
    {
        get
        {
            if (carry == null) carry = GetComponent<PlayerCarry>();
            return carry == null || carry.Count == 0;
        }
    }

    /// <summary>
    /// Behind the repair bar's bench (not at the till, whose queue comes first) with empty hands: the nearest frustrated
    /// customer across the counter (DayOneGuideUI.Frustrated: the sweat drop, under 30% patience, and reassuring would
    /// help them now), or null. From above E is for them before the device on the mat (12 Oct, the playtesters' #8).
    /// </summary>
    public CustomerInteractable FrustratedAcrossTheBench()
    {
        if (!HandsEmpty || IsBehindCounter(transform.position) || !BehindTheBench(transform.position, out _)) return null;
        return AcrossTheCounterTarget(who => who.HelpAcrossTheCounter && who.Brain != null && DayOneGuideUI.Frustrated(who.Brain));
    }

    /// <summary>
    /// For the checks (15 Oct, Life 4: "frustrated on the stool, E reaches them first" failed with no reason given): why
    /// <see cref="FrustratedAcrossTheBench"/> doesn't give <paramref name="who"/>, the first of its rules that says no, or ""
    /// when it would. The same rules, in the same order; nothing changes.
    /// </summary>
    public string WhyNotFrustratedAcrossTheBench(CustomerBrain who)
    {
        if (!HandsEmpty) return "Ace's hands aren't empty";
        if (IsBehindCounter(transform.position)) return "Ace counts as behind the till (its queue comes first there)";
        if (!BehindTheBench(transform.position, out Vector3 toward))
            return benchStation == null || benchStation.StandPoint == null ? "the bench has no stand point"
                : $"Ace isn't behind the bench ({Flat(transform.position - benchStation.StandPoint.position).magnitude:0.00} m from its stand point, " +
                  $"{benchRange:0.0} allowed, and the bench is out of his reach, or he is past its front)";
        if (who == null) return "no customer";
        CustomerInteractable it = who.GetComponent<CustomerInteractable>();
        if (it == null) return $"{who.name} has no CustomerInteractable";
        if (who.JobNeedsAttention) return "their job needs Ace first (the call on hold is ringing or waits to be dialled), so E doesn't reassure them";
        if (!it.IsAvailable) return "E has nothing for them (IsAvailable is false)";
        if (!it.HelpAcrossTheCounter) return "nothing for them across the counter (HelpAcrossTheCounter is false)";
        if (!DayOneGuideUI.Frustrated(who))
            return $"not frustrated by the guide's rule (patience {who.PatienceFraction:P0}, showing {who.ShowsPatience}, accepted {who.WasAccepted}, " +
                   $"leaving {who.IsLeaving}, counter repair {who.IsCounterRepair}, call ringing {who.JobNeedsAttention}, can reassure {who.CanReassure})";
        // Measured from the bench's stand point, as AcrossTheCounterTarget measures over the bench (15 Oct).
        Vector3 from = benchStation.StandPoint.position, at = who.transform.position;
        float flat = Flat(at - from).magnitude;
        if (flat > acrossCounterReach) return $"out of reach over the bench ({flat:0.00} m flat from its stand point; {acrossCounterReach:0.0} allowed)";
        float ahead = Vector3.Dot(Flat(at - from), toward);
        if (ahead <= .5f) return $"not in front of the counter ({ahead:0.00} m ahead of the bench's stand point; more than 0.5 needed)";
        int count = Physics.OverlapSphereNonAlloc(from, acrossCounterReach, AcrossBuffer);
        bool seen = false;
        for (int i = 0; i < count && !seen; i++) seen = AcrossBuffer[i] != null && AcrossBuffer[i].GetComponentInParent<CustomerInteractable>() == it;
        if (!seen)
        {
            // What the reach did find, and what they have (15 Oct: "none of their colliders is among the 2 the reach finds").
            var found = new System.Text.StringBuilder();
            for (int i = 0; i < count && i < 6; i++)
                if (AcrossBuffer[i] != null) found.Append($"{AcrossBuffer[i].name} ({LayerMask.LayerToName(AcrossBuffer[i].gameObject.layer)}); ");
            var theirs = new System.Text.StringBuilder();
            foreach (Collider c in who.GetComponentsInChildren<Collider>(true))
                theirs.Append($"{c.name} {c.GetType().Name} on {c.enabled && c.gameObject.activeInHierarchy}, trigger {c.isTrigger}, layer {LayerMask.LayerToName(c.gameObject.layer)}, " +
                              $"{Vector3.Distance(c.ClosestPoint(transform.position), transform.position):0.00} m from Ace; ");
            return $"none of their colliders is among the {count} the reach finds{(count >= AcrossBuffer.Length ? " (the buffer is full)" : "")}: " +
                   $"found {found}theirs: {(theirs.Length > 0 ? theirs.ToString() : "none")}; Ace at {transform.position:0.00}";
        }
        CustomerInteractable picked = FrustratedAcrossTheBench();
        if (picked != it) return picked != null ? $"{picked.name} is nearer and frustrated too" : "the overlap finds them, yet nobody is picked (a rule this list doesn't know)";
        return "";
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
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
            else if (s.IsWorkSurface) { if (benchStation == null) benchStation = s; }
            else if (counterStation == null) counterStation = s;
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
            // X picks the device up and steps back, as E does (12 Oct, Mansoor: "A uses, X takes it"). A uses at the bench
            // now (ItemInspector): it had picked the device up and ended the work, the first thing a pad player presses.
            if (inspector != null && inspector.IsHoldingItem && PadInput.Pressed(PadButton.West)) PerformInteraction(-1);
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

        DebugInfo = $"station:{(currentStation != null ? currentStation.name : "none")}  focus:{(focused != null ? focused.name : "NULL")}  behind:{BehindCounter}  staff side:{OnStaffSide}";
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
        // Across the counter (playtest 4): holding what someone in front of it is waiting for, E hands it over.
        CustomerInteractable handOver = AcrossTheCounterTarget(true);
        if (handOver != null) return handOver;
        // Behind the bench with empty hands, someone across the counter going under comes before the device on the mat
        // (12 Oct, #8): a word over the counter, then E is the mat's again. With a device in hand, or nobody frustrated in
        // reach, nothing here changes.
        CustomerInteractable worried = FrustratedAcrossTheBench();
        if (worried != null) return worried;
        Interactable offer = OverheadOffer();
        if (offer != null && (best == null || best.Priority <= StationCloseUp.OfferPriority)) return offer;
        // Anything else for someone over the counter (a word of reassurance) only when nothing here is in reach.
        return best != null ? best : AcrossTheCounterTarget(false);
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

    // PLAYTEST 4 (10 Oct 2026, "[E] Talk to Walter" while the Officer was first in line, Day 1 at 8 PM): the Officer had
    // queued unheard for about 62 s and was storming out - the angry burst over his head and his parting line are in the
    // photo - and someone saying their parting line is leaving (IsLeaving), so E rightly moved on to Walter, the only one
    // still waiting. The order itself had a fault of its own: it went by ArrivedAt, the time at the café's DOOR, and the
    // look round the room (1-3 s, longer for a relaxed walker) lets someone who came in second reach the counter first
    // (the Day 3 lab: Tomas through the door 0.3 s before Priya, at the counter 0.7 s after her). Now it is the time each
    // reached the counter (CustomerBrain.ReachedCounterAt), the door time only for someone placed there by a check.
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
            float since = c.ReachedCounterAt >= 0f ? c.ReachedCounterAt : c.ArrivedAt;
            if (since < earliest) { earliest = since; best = talk; }
        }
        return best;
    }

    // The whole array (the checks hand one in by reflection, so this name and shape stay).
    private Interactable FindFloorTarget(Collider[] near) => FindFloorTargetAmong(near, near != null ? near.Length : 0);

    // Query buffers, so aiming allocates nothing frame after frame (the garbage brought a collection
    // every few seconds: a hitch). Sixty-four is more than the café ever has within Ace's reach.
    static readonly RaycastHit[] HitBuffer = new RaycastHit[64];
    static readonly Collider[] ColliderBuffer = new Collider[64];
    // Across the counter reaches 3.5 m, four times the room of Ace's 2.2: the intake shelf's devices and the bench's parts
    // and tools each bring their colliders, so a roomier buffer, or a customer could fall off the end of a full one.
    static readonly Collider[] AcrossBuffer = new Collider[256];
    static readonly Collider[] BenchReachBuffer = new Collider[256];
    static readonly System.Collections.Generic.IComparer<RaycastHit> ByDistance = new HitDistanceComparer();
    sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // First person: what the crosshair is on, within reach. Someone waiting across the counter is further than an arm's
    // length, so behind the counter they're reached out to Counter Reach; and since playtest 4, someone waiting in front
    // of the counter (a repair-bar stool) from behind it or behind the repair bar's bench, too.
    private Interactable FindFirstPersonFloorTarget()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f));
        bool counter = BehindCounter;
        bool bench = BehindTheBench(transform.position, out _);
        bool staff = counter || bench;
        float slack = BenchSlack(bench);
        float far = staff ? Mathf.Max(reach, counterReach) + slack : reach;
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, far, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(HitBuffer, 0, count, ByDistance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider.transform.IsChildOf(transform)) continue;
            Interactable item = hit.collider.GetComponentInParent<Interactable>();
            if (item != null && item.IsAvailable && Reachable(item, hit.distance, counter, staff, slack))
                return item;
            // Triggers can provide prompts, but solid furniture and walls stop
            // the view ray from selecting an item hidden on their other side.
            if (!hit.collider.isTrigger) return null;
        }
        return null;
    }

    // First person: whether the crosshair may pick this at this distance. staff: Ace is on the staff side of the counter
    // (at the till or behind the repair bar's bench), where a customer in front of it is reached over it. slack: behind the
    // bench, how far Ace stands from its stand point (the reach over the bench is measured from there, 15 Oct).
    private bool Reachable(Interactable item, float distance, bool counter, bool staff, float slack = 0f)
    {
        if (item is StationCloseUp) return false;     // first person has no close-up
        if (item is CustomerInteractable customer)
        {
            if (counter && customer.CounterBusiness) return distance <= counterReach;
            if (!customer.FloorAvailable) return false;
            if (distance <= reach) return true;
            // Across the counter (playtest 4): the crosshair says who, so whatever E does for them from the floor.
            return staff && distance <= counterReach + slack && AcrossTheCounter(customer.transform.position);
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
            // The Interact action brings the pad's A here too; at the bench A uses (ItemInspector), so not that press.
            if (PadInput.Pressed(PadButton.South) && !(Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)) return;
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
        bool bench = BehindTheBench(transform.position, out _);
        bool staff = counter || bench;
        float slack = BenchSlack(bench);
        float far = staff ? Mathf.Max(reach, counterReach) + slack : reach;
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
            if (!Reachable(it, distance, counter, staff, slack)) continue;
            if (!InSight(eye, t.point, it)) continue;
            SeenThings.Add(it);
            into.Add(t);
        }
        return into.Count;
    }

    // Where to aim at a thing, and half its smallest width: a person at their chest (aiming at their middle would aim
    // into the counter), anything else at the middle of the collider that was found.
    //
    // SEATED (19 Oct 2026, playtest 7). Her, aiming at a man at a table: "I keep looking at his top left head. And it just
    // gets stuck there? ... Oh, maybe it's when they're sitting down." The look target is a standing-height transform on the
    // root, and NpcSeating lowers only the patience bar and the speech bubble when they sit, never it: so the aim point
    // stayed about 1.4 m up over a seated chest at about 1.05 m. The same drop NpcSeating applies to its overheads (the
    // standing hips to the seated hips, less a city body's seated lift) is applied here, so the help lands on the chest.
    // The conversation camera frames the look target itself and conversations happen at the counter, standing: unchanged.
    private static AimAssist.Target TargetOf(Interactable it, Collider c)
    {
        Bounds b = c.bounds;
        if (it is CustomerInteractable)
        {
            CustomerBrain brain = it.GetComponent<CustomerBrain>();
            Vector3 chest = brain != null && brain.LookTarget != null
                ? brain.LookTarget.position - Vector3.up * .2f
                : b.center + Vector3.up * b.extents.y * .5f;
            chest -= Vector3.up * SeatedDrop(it);
            return new AimAssist.Target(chest, Mathf.Min(b.extents.x, b.extents.z));
        }
        float size = Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z));
        return new AimAssist.Target(b.center, Mathf.Max(.03f, size));
    }

    // How far a seated customer's head and chest are below where they stand (world metres; 0 when standing): NpcSeating's
    // own drop for its overheads (NpcSeating.PlaceOverheads), in local units, scaled to the world.
    private static float SeatedDrop(Interactable it)
    {
        NpcSeating seating = it.GetComponent<NpcSeating>();
        if (seating == null || !seating.IsSeated || seating.Data == null) return 0f;
        float scaleY = Mathf.Max(1e-4f, it.transform.lossyScale.y);
        float drop = Mathf.Max(0f, seating.Data.standingHipHeight - seating.Data.seatedHip.y);
        PolygonNpcVisual visual = it.GetComponent<PolygonNpcVisual>();
        if (visual != null) drop = Mathf.Max(0f, drop - visual.SeatedLift / scaleY);
        return drop * scaleY;
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
        // behind the counter. Violet = behind the repair bar's bench, and pale
        // blue = how far E reaches over the counter from either (playtest 4).
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);
        Gizmos.color = new Color(.55f, .75f, 1f);
        Gizmos.DrawWireSphere(transform.position, acrossCounterReach);

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
            else
            {
                Gizmos.color = new Color(.7f, .4f, 1f);
                Gizmos.DrawWireSphere(s.StandPoint.position, benchRange);
            }
        }
    }
}
