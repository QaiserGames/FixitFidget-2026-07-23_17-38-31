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

    [Header("Controller")]
    [Tooltip("How fast the left stick turns the item being worked on, degrees per second.")]
    [SerializeField, Min(10f)] private float padRotateSpeed = 150f;
    [Tooltip("Scrubbing progress while RT is held on grime without moving the cursor, " +
             "in the same units as mouse travel (pixels per second).")]
    [SerializeField, Min(0f)] private float padScrubRate = 180f;

    private JobBase focusedItem;
    private Vector3 restPosition;
    private Quaternion restRotation;

    private BenchInteractable currentBenchHover;
    private ToolType currentTool = ToolType.Hand;
    private ToolPickup currentToolPickup;
    private Camera cam;
    private PlayerInteractor interaction;
    private CircuitPuzzle[] focusedCircuits = System.Array.Empty<CircuitPuzzle>();
    private bool rotateGesture;

    public bool IsHoldingItem => focusedItem != null;
    public bool IsAtWorkbench => interaction != null && interaction.IsAtStation
        && interaction.CurrentStation != null && interaction.CurrentStation.IsWorkSurface;
    public JobBase FocusedItem => focusedItem;
    public ToolType CurrentTool => currentTool;
    public string CurrentJobCard { get; private set; }
    public string HoverName { get; private set; }
    public string HoverAction { get; private set; }
    public string CollectionPrompt
    {
        get
        {
            if (focusedItem == null) return "";
            var carry = GetComponent<PlayerCarry>();
            if (carry == null || !carry.HasSpace) return "Hands full — free a hand to collect";
            string progress = focusedItem is GraceCameraRepairJob grace ? "\n" + grace.TaskSummary : "";
            return "Pick up item and step back" + progress;
        }
    }

    private void Awake()
    {
        cam = Camera.main;
        interaction = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            CancelInspection();
            return;
        }
        if (Time.timeScale <= 0f)
        {
            rotateGesture = false;
            SetBenchHover(null);
            HoverName = ""; HoverAction = "";
            return;
        }

        // The mouse, or the controller's on-screen cursor (see GamePointer).
        if (!GamePointer.Available) return;

        // Leaving the station, or the item dying, always releases focus.
        if (focusedItem == null || !interaction.IsAtStation)
        {
            if (focusedItem != null || inspectCam.Priority > 0) Release();
            HandleSelect();
            return;
        }

        HandleBench();
    }

    // ---------- CHOOSING: crosshair-aimed, work surfaces only ----------

    private void HandleSelect()
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

        // Left click, or RT on a controller.
        if (GamePointer.PrimaryPressed)
        {
            focusedItem = item;
            focusedCircuits = item.GetComponentsInChildren<CircuitPuzzle>();
            rotateGesture = false;
            restPosition = item.transform.position;
            restRotation = item.transform.rotation;

            item.transform.position = inspectPoint.position;
            inspectCam.Priority = 30;

            // Now we're manipulating, not choosing — give the cursor back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Cancelling for the recap never returns cursor ownership to a workbench.
    public void CancelInspection() => Release(false);

    // Right-click, Esc or a controller's B: put the tool down first, then the item.
    public void StepBack()
    {
        if (focusedItem == null) return;
        if (currentTool != ToolType.Hand) ClearTool();
        else Release();
    }

    // Use the ordinary pickup rules; an unfinished repair remains collectable.
    // Release first so inspection cannot snap the carried item back to the bench.
    public bool TryCollectInspectedItem()
    {
        if (focusedItem == null || !IsAtWorkbench || Time.timeScale <= 0f
            || DayClock.Instance != null && DayClock.Instance.DayOver) return false;
        var pickup = focusedItem.GetComponentInChildren<ItemInteractable>();
        var carry = GetComponent<PlayerCarry>();
        if (pickup == null || !pickup.IsAvailable || carry == null || !carry.HasSpace) return false;
        var item = focusedItem;
        carry.UseAutomaticHand();
        Release();
        pickup.Interact(interaction);
        return carry.Contains(item);
    }

    private void Release(bool returnToStation = true)
    {
        foreach (CircuitPuzzle puzzle in focusedCircuits)
            if (puzzle != null) puzzle.HideForInspection();
        focusedCircuits = System.Array.Empty<CircuitPuzzle>();
        rotateGesture = false;
        if (focusedItem != null)
        {
            focusedItem.transform.position = restPosition;
            focusedItem.transform.rotation = restRotation;
        }
        focusedItem = null;
        if (inspectCam != null) inspectCam.Priority = 0;
        SetBenchHover(null);
        ClearTool();
        CurrentJobCard = "";
        HoverName = "";
        HoverAction = "";

        // Back to choosing — crosshair returns.
        if (returnToStation && interaction != null && interaction.IsAtStation
            && !(DayClock.Instance != null && DayClock.Instance.DayOver))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---------- MANIPULATING: cursor-aimed ----------

    private void HandleBench()
    {
        // A controller gets its own on-screen cursor while an item is in hand.
        GamePointer.WantCursor();
        Vector2 pointer = GamePointer.Position;

        // UI uses the existing Input System EventSystem. Raw bench input must
        // not click through the retry button or rotate the device underneath it.
        bool overHud = false, overBoard = false;
        foreach (CircuitPuzzle puzzle in focusedCircuits)
        {
            if (puzzle == null) continue;
            overHud |= puzzle.ContainsHudPoint(pointer);
            overBoard |= puzzle.ContainsBoardPoint(pointer);
        }
        if (GamePointer.SecondaryPressed)
        {
            StepBack();
            return;
        }

        // Controller: LB / RB cycle the bench tools, the left stick turns the item.
        if (PadInput.Pressed(PadButton.RightShoulder)) CycleTool(1);
        else if (PadInput.Pressed(PadButton.LeftShoulder)) CycleTool(-1);
        Vector2 spin = PadInput.Curved(PadInput.LeftStick, 1.3f);
        if (spin != Vector2.zero)
        {
            float step = padRotateSpeed * Time.deltaTime;
            focusedItem.transform.Rotate(cam.transform.up, -spin.x * step, Space.World);
            focusedItem.transform.Rotate(cam.transform.right, spin.y * step, Space.World);
        }

        if (overHud)
        {
            rotateGesture = false;
            SetBenchHover(null);
            HoverName = ""; HoverAction = "";
            return;
        }
        Ray ray = cam.ScreenPointToRay(pointer);

        CurrentJobCard = focusedItem.JobCard;

        BenchInteractable hovered = null;
        GrimeSpot grime = null;
        ToolPickup hoveredTool = null;

        foreach (CircuitPuzzle puzzle in focusedCircuits)
        {
            if (puzzle == null) continue;
            CircuitTile tile = puzzle.TileAtScreenPoint(pointer);
            if (tile != null) { hovered = tile; break; }
        }
        if (!overBoard && hovered == null && Physics.Raycast(ray, out RaycastHit hit, benchReach))
        {
            ResolveBenchHit(hit.collider, out hovered, out grime, out hoveredTool);
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
                HoverAction = hovered is CircuitTile || hovered is ReplaceablePart ? hovered.Prompt : "Not yet";
            else if (currentTool == hovered.RequiredTool || hovered.RequiredTool == ToolType.Hand)
            {
                target = hovered;
                HoverAction = hovered.Prompt;
            }
            else
                HoverAction = $"Needs the {hovered.RequiredTool.ToString().ToLower()}";
        }

        SetBenchHover(target);

        // Left click / RT.
        if (GamePointer.PrimaryPressed)
        {
            HandleBenchPress(hovered, grime, hoveredTool, overBoard);
        }

        bool held = GamePointer.PrimaryHeld;
        if (held)
        {
            Vector2 delta = GamePointer.Delta;

            if (currentTool == ToolType.Brush)
            {
                // Wide Brush was sold in the upgrade shop but never read here,
                // so buying it changed nothing. It now scales every stroke.
                float brush = UpgradeManager.Instance != null ? UpgradeManager.Instance.ScrubSpeedMultiplier : 1f;
                // A stick can't scrub back and forth like a mouse, so holding
                // RT on the grime scrubs at a steady rate as well.
                float travel = delta.magnitude + (GamePointer.PadCursorActive ? padScrubRate * Time.deltaTime : 0f);
                if (grime != null) grime.Scrub(travel * scrubPower * brush);
            }
            else if (currentTool == ToolType.Hand && rotateGesture && !overBoard)
            {
                focusedItem.transform.Rotate(cam.transform.up, -delta.x * rotateSpeed, Space.World);
                focusedItem.transform.Rotate(cam.transform.right, delta.y * rotateSpeed, Space.World);
            }
        }

        if (!held) rotateGesture = false;
    }

    // Controller tool selection: bare hands, then each bench tool in turn.
    private void CycleTool(int direction)
    {
        var order = new System.Collections.Generic.List<ToolPickup> { null };
        foreach (ToolType type in System.Enum.GetValues(typeof(ToolType)))
        {
            if (type == ToolType.Hand) continue;
            ToolPickup nearest = null;
            float best = float.PositiveInfinity;
            foreach (ToolPickup pickup in FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude))
            {
                if (pickup.tool != type) continue;
                float distance = inspectPoint != null
                    ? (pickup.transform.position - inspectPoint.position).sqrMagnitude : 0f;
                if (distance < best) { best = distance; nearest = pickup; }
            }
            if (nearest != null) order.Add(nearest);
        }
        if (order.Count <= 1) return;
        int index = order.IndexOf(currentTool == ToolType.Hand ? null : currentToolPickup);
        if (index < 0) index = 0;
        ToolPickup next = order[(index + direction + order.Count) % order.Count];
        if (next == null) { ClearTool(); return; }
        if (currentToolPickup != null) currentToolPickup.SetSelected(false);
        currentToolPickup = next;
        currentTool = next.tool;
        next.SetSelected(true);
    }

    public string CurrentToolName => currentTool == ToolType.Hand || currentToolPickup == null
        ? "Hands" : currentToolPickup.displayName;

    private static void ResolveBenchHit(Collider collider, out BenchInteractable part,
        out GrimeSpot grime, out ToolPickup tool)
    {
        // A model's visible blade, screw head or tool handle can own the hit
        // collider while its behaviour lives on the containing part. Resolve
        // the nearest owner so clicking that anatomy reaches the same task.
        part = collider != null ? collider.GetComponentInParent<BenchInteractable>() : null;
        grime = collider != null ? collider.GetComponentInParent<GrimeSpot>() : null;
        tool = collider != null ? collider.GetComponentInParent<ToolPickup>() : null;
    }

    private void HandleBenchPress(BenchInteractable part, GrimeSpot grime, ToolPickup tool, bool overBoard)
    {
        if (Time.timeScale <= 0f || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        // Only a drag that starts on empty space rotates the item. Picking a
        // tool, turning a wire, or pressing a covered part owns that press.
        rotateGesture = part == null && grime == null && tool == null && !overBoard;
        if (tool != null)
        {
            if (currentToolPickup != null) currentToolPickup.SetSelected(false);
            currentToolPickup = tool;
            currentTool = tool.tool;
            tool.SetSelected(true);
        }
        else if (grime == null && part != null && part.isActiveAndEnabled && part.CanInteract
            && (currentTool == part.RequiredTool || part.RequiredTool == ToolType.Hand))
            part.Activate();
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
