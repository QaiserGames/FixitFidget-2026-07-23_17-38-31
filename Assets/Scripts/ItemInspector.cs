using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class ItemInspector : MonoBehaviour
{
    [SerializeField] private Transform inspectPoint;
    [SerializeField] private CinemachineCamera inspectCam;
    [SerializeField] private float rotateSpeed = 0.4f;
    [SerializeField] private float scrubPower = 1.5f;
    [SerializeField] private float benchReach = 6f;

    private JobBase focusedItem;
    private Vector3 restPosition;
    private Quaternion restRotation;

    private BenchInteractable currentBenchHover;
    private ToolType currentTool = ToolType.Hand;
    private ToolPickup currentToolPickup;
    private Camera cam;
    private PlayerInteractor interaction;

    public bool IsHoldingItem => focusedItem != null;
    public string CurrentJobCard { get; private set; }
    public string HoverName { get; private set; }
    public string HoverAction { get; private set; }

    private void Awake()
    {
        cam = Camera.main;
        interaction = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Leaving the station, or the item dying, always releases focus.
        if (focusedItem == null || !interaction.IsAtStation)
        {
            if (focusedItem != null || inspectCam.Priority > 0) Release();
            HandleSelect(mouse);
            return;
        }

        HandleBench(mouse);
    }

    // ---------- CHOOSING: crosshair-aimed, work surfaces only ----------

    private void HandleSelect(Mouse mouse)
    {
        HoverName = "";
        HoverAction = "";
        CurrentJobCard = "";

        if (!interaction.IsAtStation) return;

        // Repairs happen at the bench, not the counter.
        if (interaction.CurrentStation == null || !interaction.CurrentStation.IsWorkSurface) return;

        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = cam.ScreenPointToRay(centre);
        if (!Physics.Raycast(ray, out RaycastHit hit, benchReach)) return;

        JobBase item = hit.collider.GetComponentInParent<JobBase>();
        if (item == null) return;

        HoverName = item.JobCard;
        HoverAction = "Work on this";

        if (mouse.leftButton.wasPressedThisFrame)
        {
            focusedItem = item;
            restPosition = item.transform.position;
            restRotation = item.transform.rotation;

            item.transform.position = inspectPoint.position;
            inspectCam.Priority = 30;

            // Now we're manipulating, not choosing — give the cursor back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Release()
    {
        if (focusedItem != null)
        {
            focusedItem.transform.position = restPosition;
            focusedItem.transform.rotation = restRotation;
        }
        focusedItem = null;
        inspectCam.Priority = 0;
        ClearTool();
        CurrentJobCard = "";
        HoverName = "";
        HoverAction = "";

        // Back to choosing — crosshair returns.
        if (interaction.IsAtStation)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---------- MANIPULATING: cursor-aimed ----------

    private void HandleBench(Mouse mouse)
    {
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        CurrentJobCard = focusedItem.JobCard;

        BenchInteractable hovered = null;
        GrimeSpot grime = null;
        ToolPickup hoveredTool = null;

        if (Physics.Raycast(ray, out RaycastHit hit, benchReach))
        {
            hovered = hit.collider.GetComponent<BenchInteractable>();
            grime = hit.collider.GetComponent<GrimeSpot>();
            hoveredTool = hit.collider.GetComponent<ToolPickup>();
        }

        BenchInteractable target = null;
        HoverName = "";
        HoverAction = "";

        if (hoveredTool != null)
        {
            HoverName = hoveredTool.displayName;
            HoverAction = currentTool == hoveredTool.tool ? "In hand" : "Pick up tool";
        }
        else if (grime != null)
        {
            HoverName = grime.DisplayName;
            HoverAction = currentTool == ToolType.Brush ? "Hold to scrub" : "Needs the brush";
        }
        else if (hovered != null)
        {
            HoverName = hovered.DisplayName;

            if (!hovered.CanInteract)
                HoverAction = "Not yet";
            else if (currentTool == hovered.RequiredTool || hovered.RequiredTool == ToolType.Hand)
            {
                target = hovered;
                HoverAction = hovered.Prompt;
            }
            else
                HoverAction = $"Needs the {hovered.RequiredTool.ToString().ToLower()}";
        }

        SetBenchHover(target);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (hoveredTool != null)
            {
                if (currentToolPickup != null) currentToolPickup.SetSelected(false);
                currentToolPickup = hoveredTool;
                currentTool = hoveredTool.tool;
                hoveredTool.SetSelected(true);
            }
            else if (target != null)
            {
                target.Activate();
            }
        }

        if (mouse.leftButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();

            if (currentTool == ToolType.Brush)
            {
                if (grime != null) grime.Scrub(delta.magnitude * scrubPower);
            }
            else if (currentTool == ToolType.Hand)
            {
                focusedItem.transform.Rotate(cam.transform.up, -delta.x * rotateSpeed, Space.World);
                focusedItem.transform.Rotate(cam.transform.right, delta.y * rotateSpeed, Space.World);
            }
        }

        // Right-click backs out one level: tool first, then the item.
        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (currentTool != ToolType.Hand) ClearTool();
            else Release();
        }
    }

    private void ClearTool()
    {
        currentTool = ToolType.Hand;
        if (currentToolPickup != null) currentToolPickup.SetSelected(false);
        currentToolPickup = null;
    }

    private void SetBenchHover(BenchInteractable item)
    {
        if (currentBenchHover == item) return;
        if (currentBenchHover != null) currentBenchHover.SetHighlight(false);
        currentBenchHover = item;
        if (currentBenchHover != null) currentBenchHover.SetHighlight(true);
    }
}