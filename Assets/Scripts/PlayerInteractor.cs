using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float stationReach = 10f;

    [Tooltip("How close to a station's STAND POINT you must be before F will " +
             "put you there. Measured to the stand point, not to the collider — " +
             "see FindNearestStation for why that matters.")]
    [SerializeField] private float stationEnterRange = 1.5f;

    private Interactable focused;
    private StationInteractable currentStation;
    private StationInteractable nearbyStation;
    private PlayerMovement movement;
    private Renderer bodyRenderer;
    private Camera cam;
    private ConversationController conversation;
    private StationInteractable[] allStations;

    public bool IsAtStation => currentStation != null;
    public Interactable Focused => focused;
    public string CurrentPrompt { get; private set; }
    public string DebugInfo { get; private set; }
    public StationInteractable CurrentStation => currentStation;

    // What the Action button would do right now.
    public string StationPrompt =>
        currentStation != null ? "Step back" :
        nearbyStation != null ? nearbyStation.StationLabel : "";

    private void Awake()
    {
        conversation = GetComponent<ConversationController>();
        movement = GetComponent<PlayerMovement>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        cam = Camera.main;

        // Stations don't spawn at runtime, so find them once instead of
        // sweeping physics every frame.
        allStations = FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude);
    }

    private void Update()
    {
        // The conversation owns input while it's open.
        if (conversation != null && conversation.InConversation)
        {
            CurrentPrompt = "";
            return;
        }

        Interactable next = FindBest();

        if (next != focused)
        {
            if (focused != null) focused.SetFocused(false);
            focused = next;
            if (focused != null) focused.SetFocused(true);
        }

        CurrentPrompt = focused != null ? focused.Prompt : "";
        nearbyStation = FindNearestStation();
        // Q declines whatever we're looking at, once they've had their say.
        StationKey();

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && focused != null)
        {
            CustomerBrain b = focused.GetComponent<CustomerBrain>();
            if (b != null && b.CanRefuse) b.RefuseJob();
        }

        DebugInfo = $"station:{(currentStation != null ? currentStation.name : "none")}  focus:{(focused != null ? focused.name : "NULL")}";

        HandlePhoneTreeInput();
    }

    private void HandlePhoneTreeInput()
    {
        if (currentStation == null || Keyboard.current == null) return;

        HoldCallJob call = FindAnyObjectByType<HoldCallJob>();
        if (call == null || call.CurrentPhase != HoldCallJob.Phase.InTree) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) call.PressNumber(1);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) call.PressNumber(2);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) call.PressNumber(3);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) call.PressNumber(4);
    }

    // Stations are found separately — Action uses them, Interact never does.
    //
    // THE BUG THIS FIXES: this used to be an OverlapSphere against colliders.
    // OverlapSphere returns anything the sphere TOUCHES, and the counter's box
    // collider is 2.6 m wide — so a 2.2 m sphere clipped it from anywhere
    // within 2.2 m of any part of it. Effective activation zone: about seven
    // metres across. You could press F from most of the room and teleport to
    // the counter.
    //
    // It then ranked by distance to the station's ORIGIN, which for a wide
    // counter is its centre, so the ranking was wrong too.
    //
    // Now: measure to the stand point — the place you'd actually walk to, which
    // already exists in the scene as CounterStandPoint — with a tight radius.
    private StationInteractable FindNearestStation()
    {
        if (currentStation != null) return null;
        if (allStations == null) return null;

        StationInteractable best = null;
        float bestDist = stationEnterRange;

        foreach (StationInteractable s in allStations)
        {
            if (s == null) continue;

            Transform point = s.StandPoint != null ? s.StandPoint : s.transform;
            float d = Vector3.Distance(transform.position, point.position);

            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    private Interactable FindBest()
    {
        // ---- At a station: physical raycast from the crosshair ----
        if (currentStation != null)
        {
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = cam.ScreenPointToRay(centre);

            RaycastHit[] hits = Physics.RaycastAll(ray, stationReach);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit h in hits)
            {
                Interactable it = h.collider.GetComponentInParent<Interactable>();
                if (it == null) continue;
                if (it is StationInteractable) continue;
                if (!it.IsAvailable) continue;
                return it;
            }

            return null;
        }

        // ---- On the shop floor: nearest available wins ----
        Collider[] near = Physics.OverlapSphere(transform.position, reach);

        Interactable best = null;
        float bestScore = float.MinValue;

        foreach (Collider h in near)
        {
            Interactable it = h.GetComponentInParent<Interactable>();
            if (it == null || !it.IsAvailable) continue;

            // Customers used to be skipped out here entirely, because intake is
            // a counter conversation and shouldn't start from across the room.
            // But once people wait away from the counter, you have to be able to
            // walk up and hand them things. So: only the non-intake actions
            // (serve, hand back, reassure) are reachable on the floor.
            CustomerInteractable ci = it as CustomerInteractable;
            if (ci != null && !ci.FloorAvailable) continue;

            float score = it.Priority * 100f - Vector3.Distance(transform.position, it.transform.position);

            if (score > bestScore)
            {
                bestScore = score;
                best = it;
            }
        }

        return best;
    }

    // E — pick up, set down, accept, hand back.
    private void OnInteract()
    {
        if (conversation != null && conversation.InConversation) return;
        if (focused != null) focused.Interact(this);
    }

    // F — enter and leave stations.
    //
    // ⚠️ THIS WAS NEVER BEING CALLED. PlayerInput sends messages named after
    // actions in the Input Actions asset, and that asset contains Move, Look,
    // Interact, Attack, Crouch, Jump, Sprint, Previous, Next and Back — but no
    // action called "Action" and no binding on <Keyboard>/f at all. So the
    // method existed, read correctly, and never once fired.
    //
    // Kept public-facing as OnAction so that adding the binding later starts
    // working with no further changes. Until then StationKey() below drives it
    // directly, the same way Q-decline and the phone-tree digits already do.
    private void OnAction() => ToggleStation();

    private void ToggleStation()
    {
        if (conversation != null && conversation.InConversation) return;
        if (currentStation != null) { ExitStation(); return; }
        if (nearbyStation != null) EnterStation(nearbyStation);
    }

    // Read directly rather than through the action map.
    //
    // Not the long-term answer — proper gamepad support means every verb goes
    // through the asset — but it's the same pattern Q and the phone-tree digits
    // already use, it needs no Unity-side setup, and it makes the verb work
    // TODAY. Migrating all of them together is a job for the controller pass.
    private void StationKey()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.fKey.wasPressedThisFrame) ToggleStation();
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

    private void OnBack()
    {
        ExitStation();
    }

    public void EnterStation(StationInteractable station)
    {
        currentStation = station;

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
            // disagreeing until the next Move() — and EnterStation disables
            // `movement`, so no Move() runs while docked. That's why the body
            // only snapped back once you walked away. Disabling the
            // controller across the teleport is how Unity says to relocate one.
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = target;
            transform.rotation = station.StandPoint.rotation;

            if (cc != null) cc.enabled = true;
        }

        station.ActivateCamera(true);
        station.ResetView();
        movement.enabled = false;
        if (bodyRenderer != null) bodyRenderer.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ExitStation()
    {
        if (currentStation == null) return;

        if (focused != null)
        {
            focused.SetFocused(false);
            focused = null;
        }

        currentStation.ActivateCamera(false);
        currentStation = null;
        movement.enabled = true;
        if (bodyRenderer != null) bodyRenderer.enabled = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDrawGizmosSelected()
    {
        // Cyan = what you can pick up / hand over. Yellow = how close you must
        // be to each station's stand point for F to work. If a yellow sphere is
        // sitting somewhere odd, that station's Stand Point isn't assigned.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);

        Gizmos.color = Color.yellow;
        foreach (StationInteractable s in FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
        {
            if (s == null) continue;
            Transform point = s.StandPoint != null ? s.StandPoint : s.transform;
            Gizmos.DrawWireSphere(point.position, stationEnterRange);
        }
    }
}