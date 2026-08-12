using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float stationReach = 10f;

    private Interactable focused;
    private StationInteractable currentStation;
    private StationInteractable nearbyStation;
    private PlayerMovement movement;
    private Renderer bodyRenderer;
    private Camera cam;

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
        movement = GetComponent<PlayerMovement>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        cam = Camera.main;
    }

    private void Update()
    {
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
    private StationInteractable FindNearestStation()
    {
        if (currentStation != null) return null;

        Collider[] near = Physics.OverlapSphere(transform.position, reach);
        StationInteractable best = null;
        float bestDist = float.MaxValue;

        foreach (Collider h in near)
        {
            StationInteractable s = h.GetComponentInParent<StationInteractable>();
            if (s == null) continue;

            float d = Vector3.Distance(transform.position, s.transform.position);
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
            if (it is CustomerInteractable) continue;   // customers are served from the counter

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
        if (focused != null) focused.Interact(this);
    }

    // F — enter and leave stations.
    private void OnAction()
    {
        if (currentStation != null) { ExitStation(); return; }
        if (nearbyStation != null) EnterStation(nearbyStation);
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
            transform.position = station.StandPoint.position;
            transform.rotation = station.StandPoint.rotation;
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);
    }
}