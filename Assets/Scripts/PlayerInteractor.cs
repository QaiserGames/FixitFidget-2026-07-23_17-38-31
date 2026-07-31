using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float stationReach = 4f;

    private Interactable focused;
    private StationInteractable currentStation;
    private PlayerMovement movement;
    private Renderer bodyRenderer;
    private Camera cam;

    public bool IsAtStation => currentStation != null;
    public string CurrentPrompt { get; private set; }
    public string DebugInfo { get; private set; }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        bodyRenderer = GetComponentInChildren<Renderer>();
        cam = Camera.main;
    }

    private void Update()
    {
        Interactable next = FindBest();

        // Tell things when the crosshair arrives and leaves.
        if (next != focused)
        {
            if (focused != null) focused.SetFocused(false);
            focused = next;
            if (focused != null) focused.SetFocused(true);
        }

        CurrentPrompt = focused != null ? focused.Prompt : "";
        BuildDebugInfo();

        // FALLBACK: direct device read. Delete this block once "OnBack"
        // shows up in the Player Input message list.
        bool backPressed =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (backPressed) ExitStation();
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
                if (it is StationInteractable) continue;   // includes the counter we're stood at
                if (!it.IsAvailable) continue;
                return it;
            }

            return null;
        }

        // ---- On the shop floor: nearest station wins ----
        Collider[] near = Physics.OverlapSphere(transform.position, reach);

        Interactable best = null;
        float bestScore = float.MinValue;

        foreach (Collider h in near)
        {
            Interactable it = h.GetComponentInParent<Interactable>();
            if (it == null || !it.IsAvailable) continue;
            if (it is CustomerInteractable) continue;      // customers are served from the counter

            float score = it.Priority * 100f - Vector3.Distance(transform.position, it.transform.position);

            if (score > bestScore)
            {
                bestScore = score;
                best = it;
            }
        }

        return best;
    }

    private void OnInteract()
    {
        if (focused != null) focused.Interact(this);
    }

    private void OnBack()
    {
        ExitStation();
    }

    public void EnterStation(StationInteractable station)
    {
        currentStation = station;
        station.ActivateCamera(true);
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

    private void BuildDebugInfo()
    {
        DebugInfo = $"station:{(currentStation != null ? currentStation.name : "none")}  focus:{(focused != null ? focused.name : "NULL")}";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);
    }
}