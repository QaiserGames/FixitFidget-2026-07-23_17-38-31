using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// ---------------------------------------------------------------------------
// WORKING ON AN ITEM AT THE BENCH
//
// The inspection close-up: the item moves to the inspect point, which the inspection camera frames with the tool rail,
// the tray and the screw bin; tools are picked off the rail and used on the item with the pointer (the mouse, or the
// pad's cursor, PadCursor).
//
// STATIONS AS REACH (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.1). Getting here used to take
// F at the bench (a camera of its own), then aiming the crosshair at the item and clicking it. Now nothing is stepped up
// to: in first person, click or RT on a device on the bench (PlayerInteractor); from above, E on it (when it isn't
// finished). The close-up itself is kept as it was: B, Esc or right-click step back (the tool first, then the item),
// and E picks the item up and steps back in one press.
//
// Aim help on a pad (§2.3): when the stick lets go within about 60 px of a part, a tool or grime, the cursor settles on
// it (PadCursor), and D-pad left or right steps the cursor to the next part in the order the work goes (screws, cover,
// grime, parts).
// ---------------------------------------------------------------------------
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
    // The frame the item was picked up to work on: that press was the press that began it, not one on the item.
    private int begunFrame = -1;

    public bool IsHoldingItem => focusedItem != null;
    /// <summary>Working on an item at the bench (the inspection close-up is up). Was "docked at the bench" until playtest 3.</summary>
    public bool IsAtWorkbench => focusedItem != null;
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

        // Nothing in hand (or the item gone): no close-up.
        if (focusedItem == null)
        {
            if (inspectCam != null && inspectCam.Priority > 0) Release();
            HoverName = ""; HoverAction = ""; CurrentJobCard = "";
            return;
        }

        // The press that began the work is not a press on the item.
        if (Time.frameCount == begunFrame) return;
        HandleBench();
    }

    // ---------- STARTING: from the interactor (click or RT in first person, E from above) ----------

    /// <summary>
    /// Takes <paramref name="item"/> (a device on the bench) into the inspection close-up: it moves to the inspect point and
    /// the inspection camera takes over, from whatever view Ace is in. False when something is already in hand, the day is
    /// over, or the bench's camera isn't set up.
    /// </summary>
    public bool BeginInspection(JobBase item)
    {
        if (item == null || focusedItem != null || inspectPoint == null || inspectCam == null || Time.timeScale <= 0f
            || DayClock.Instance != null && DayClock.Instance.DayOver) return false;
        if (cam == null) cam = Camera.main;
        focusedItem = item;
        focusedCircuits = item.GetComponentsInChildren<CircuitPuzzle>();
        rotateGesture = false;
        restPosition = item.transform.position;
        restRotation = item.transform.rotation;
        begunFrame = Time.frameCount;
        railTools = null;

        item.transform.position = inspectPoint.position;
        inspectCam.Priority = 30;
        CurrentJobCard = item.JobCard;

        // Manipulating, not aiming: the pointer is free (CafeViewMode keeps it so while this is up).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !PadInput.UsingPad;
        return true;
    }

    // Cancelling for the recap leaves the cursor as the recap wants it.
    public void CancelInspection() => Release();

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

    // The item goes back where it was and the close-up comes down. The cursor is the view's to set again (CafeViewMode
    // locks it in first person and frees it above): nothing here locks it.
    private void Release()
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
    }

    // ---------- MANIPULATING: cursor-aimed ----------

    private void HandleBench()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        // A controller gets its own on-screen cursor while an item is in hand.
        GamePointer.WantCursor();
        OfferAimHelp();
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

    // ---------- aim help on a pad (playtest 3, §2.3) ----------

    // The points the pad's cursor settles on (every part, grime spot and tool on screen), and D-pad left / right: the
    // cursor glides to the next part in the order the work goes.
    private readonly List<Vector2> snapPoints = new();
    private ToolPickup[] railTools;   // the bench's tools, found once an item is taken up (they don't come and go)
    private readonly List<(int order, Vector2 screen)> workPoints = new();

    private void OfferAimHelp()
    {
        if (!GamePointer.PadCursorActive || !AimAssist.Enabled) return;
        snapPoints.Clear();
        workPoints.Clear();
        GatherWork(focusedItem.transform);
        foreach (GameObject part in focusedItem.DetachedParts)
            if (part != null) GatherWork(part.transform);
        foreach (var w in workPoints) snapPoints.Add(w.screen);
        if (railTools == null) railTools = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude);
        foreach (ToolPickup tool in railTools)
            if (tool != null && OnScreen(BoundsCentre(tool.transform), out Vector2 s)) snapPoints.Add(s);
        PadCursor.OfferSnapPoints(snapPoints);

        int direction = PadInput.Pressed(PadButton.DpadRight) ? 1 : PadInput.Pressed(PadButton.DpadLeft) ? -1 : 0;
        if (direction == 0 || workPoints.Count == 0) return;
        // In the work's order (screws, covers, grime, parts, tiles), left to right within each kind.
        workPoints.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.screen.x.CompareTo(b.screen.x));
        Vector2 at = GamePointer.Position;
        int nearest = 0;
        float best = float.PositiveInfinity;
        for (int i = 0; i < workPoints.Count; i++)
        {
            float d = (workPoints[i].screen - at).sqrMagnitude;
            if (d < best) { best = d; nearest = i; }
        }
        // On a part already (within 30 px): the next one that way; on none: the first of the work (right) or the last (left).
        bool onOne = best <= 30f * 30f * Scale * Scale;
        int next = onOne ? (nearest + direction + workPoints.Count) % workPoints.Count
            : direction > 0 ? 0 : workPoints.Count - 1;
        PadCursor.GlideTo(workPoints[next].screen);
    }

    private static float Scale => Mathf.Max(.5f, Screen.height / 1080f);

    // Each part of the item that work can be done on now, with its place in the order the work goes: screws out, the
    // cover off, the grime, the parts, the circuit, then the cover back on and the screws back in.
    private void GatherWork(Transform root)
    {
        foreach (BenchInteractable part in root.GetComponentsInChildren<BenchInteractable>())
        {
            if (!part.isActiveAndEnabled || !part.CanInteract) continue;
            if (OnScreen(BoundsCentre(part.transform), out Vector2 s)) workPoints.Add((WorkOrder(part), s));
        }
        // Grime counts once it can be reached: nothing (the cover) between the camera and it.
        foreach (GrimeSpot grime in root.GetComponentsInChildren<GrimeSpot>())
            if (grime.isActiveAndEnabled && OnScreen(BoundsCentre(grime.transform), out Vector2 s) && Uncovered(grime))
                workPoints.Add((2, s));
    }

    private bool Uncovered(GrimeSpot grime)
    {
        Vector3 to = BoundsCentre(grime.transform) - cam.transform.position;
        if (!Physics.Raycast(cam.transform.position, to.normalized, out RaycastHit hit, to.magnitude + .05f)) return true;
        return hit.collider.GetComponentInParent<GrimeSpot>() == grime;
    }

    /// <summary>A part's place in the order the work goes (0 first): screws out, the cover off, the grime (2), the parts,
    /// the circuit, the cover back on, the screws back in.</summary>
    public static int WorkOrder(BenchInteractable part)
    {
        if (part is ScrewTarget)
        {
            Screw screw = part.GetComponent<Screw>();
            return screw != null && screw.IsOut ? 6 : 0;
        }
        if (part is RemovablePart cover) return cover.IsRemoved ? 5 : 1;
        if (part is ReplaceablePart) return 3;
        if (part is CircuitTile) return 4;
        return 7;
    }

    private bool OnScreen(Vector3 world, out Vector2 screen)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        screen = p;
        return p.z > cam.nearClipPlane && p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
    }

    private static Vector3 BoundsCentre(Transform t)
    {
        var c = t.GetComponentInChildren<Collider>();
        if (c != null && c.enabled) return c.bounds.center;
        var r = t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
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
