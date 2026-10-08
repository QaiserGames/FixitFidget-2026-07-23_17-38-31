using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// ---------------------------------------------------------------------------
// WORKING ON AN ITEM AT THE BENCH
//
// The inspection close-up: the item moves to the inspect point, which the inspection camera frames with the stage (the
// mat, the parts tray, the tool caddy); tools are picked out of the caddy and used on the item with the pointer (the
// mouse, or the pad's cursor, PadCursor).
//
// STATIONS AS REACH (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.1). Getting here used to take
// F at the bench (a camera of its own), then aiming the crosshair at the item and clicking it. Now nothing is stepped up
// to: in first person, click or RT on a device on the bench (PlayerInteractor); from above, E on it (when it isn't
// finished). B, Esc or right-click step back (the tool first, then the item), and E picks the item up and steps back in
// one press.
//
// THE BENCH, V2 (7 Oct 2026, evening; claude/bench-spec-v2.md). A press on a part is one of three things, decided by the
// part (BenchInteractable): a HOLD (a screw backing out under the driver, the pry at a cover's edge, the tweezers'
// pinch), a GRAB (a popped cover, a part in the tweezers: it follows the cursor and drops where it's let go), or the old
// instant press (a circuit tile, the mute switch). The chosen tool is in the hand: the model follows the cursor over the
// device and does its motion at the part. The scroll wheel or the D-pad up/down zooms the close-up. The clock runs
// throughout.
//
// THE TABLETOP (8 Oct 2026; Mansoor's notes after playing 4a: the stage floated, and "the exact same way restory has
// it"). The device LIES ON THE MAT, one work face up, under a camera looking down at it; nothing hangs in the air. It
// spins on the mat (drag sideways, or the left stick), with a little inertia, and settles square (to the nearest quarter
// turn) when let go near one; drag up or down (or push the stick) and it TILTS to let you peek under an edge, and lies
// back flat when let go; R / Y FLIPS it over, along its length, onto its other face. Its pose is always one of these, so
// it can never be left at an angle nothing can be done at.
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

    [Header("The bench, v2")]
    [Tooltip("Within this many degrees of a quarter turn, a device let go spinning settles square to the view.")]
    [SerializeField, Range(0f, 45f)] private float faceSnapWithin = 22f;
    [Tooltip("How far an edge can be tilted up to peek under it, degrees.")]
    [SerializeField, Range(10f, 60f)] private float peekTilt = 35f;
    [Tooltip("The close-up camera's distance, as a share of where it is placed: zoomed fully out (x) and fully in (y).")]
    [SerializeField] private Vector2 zoomRange = new Vector2(1.2f, .42f);

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

    // v2: the hold and the grab under way, the camera's zoom.
    private BenchInteractable holdTarget, grabTarget;
    private BenchHand hand;
    private Coroutine flipping;
    private Vector3 camOffset;              // the close-up camera from the table point, as placed
    private Vector3 camRestPosition;
    private Quaternion camRestRotation;

    // The tabletop: the device's pose on the mat is a face, a yaw and a tilt, never a free rotation.
    private Vector3 tableTop;               // where the device rests (the mat's top under the inspect point)
    private Vector3 thinAxis = Vector3.up;  // the device's thinnest local axis: its two work faces are either side of it
    private Vector3 longAxis = Vector3.forward;
    private Vector3 localMin, localMax;     // its renderers' bounds in its own frame
    private float faceSign = 1f;            // +1: local +thin is up; -1: local -thin is up
    private float yaw, yawVelocity;         // degrees about the world's up, from square-on (the long side up the view)
    private float tilt, tiltVelocity;       // degrees about the view's right, an edge lifted to peek under
    private bool stickTurning;              // the left stick turned the device this frame: no coasting on top of it
    private bool settling, settled;
    private float settleYaw;
    private float boundsAt;
    private float zoom;                     // 0 = as placed, 1 = fully in
    private float zoomShown = -2f;          // below -1.5: not shown yet

    public bool IsHoldingItem => focusedItem != null;
    /// <summary>Working on an item at the bench (the inspection close-up is up). Was "docked at the bench" until playtest 3.</summary>
    public bool IsAtWorkbench => focusedItem != null;
    public JobBase FocusedItem => focusedItem;
    public ToolType CurrentTool => currentTool;
    public BenchInteractable HoldTarget => holdTarget;
    public BenchInteractable GrabTarget => grabTarget;
    public float Zoom => zoom;
    /// <summary>The device's turn on the mat, degrees from square-on (its long side up the view).</summary>
    public float Yaw => yaw;
    /// <summary>The edge lifted to peek under, degrees (0: lying flat).</summary>
    public float Tilt => tilt;
    /// <summary>Which of the device's two work faces is up: +1 its local +thin axis, -1 its local -thin.</summary>
    public float FaceUp => faceSign;
    /// <summary>The device's thinnest local axis (its work faces are either side of it).</summary>
    public Vector3 ThinAxisLocal => thinAxis;
    /// <summary>Where the device rests: the mat's top under the inspect point.</summary>
    public Vector3 TableTop => tableTop;
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
        yawVelocity = tiltVelocity = 0f;
        tilt = 0f;
        settling = false;
        settled = true;
        zoom = 0f;
        zoomShown = -2f;
        camRestPosition = inspectCam.transform.position;
        camRestRotation = inspectCam.transform.rotation;
        inspectCam.Priority = 30;
        CurrentJobCard = item.JobCard;

        // Laid on the mat, the face that was up on the bench still up, square to the view. It goes back as it lay.
        tableTop = TableTopPoint(item.transform);
        camOffset = camRestPosition - tableTop;
        MeasureSlab(item.transform);
        faceSign = Vector3.Dot(item.transform.rotation * thinAxis, Vector3.up) >= 0f ? 1f : -1f;
        Vector3 longNow = Vector3.ProjectOnPlane(item.transform.rotation * longAxis, Vector3.up);
        yaw = longNow.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(ViewForwardOnTable(), longNow, Vector3.up) : 0f;
        yaw = Mathf.Round(yaw / 90f) * 90f;      // square to the view when it arrives
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        Pose();

        // The fresh parts wait in the tray from the first time the device is worked on.
        foreach (ReplaceablePart part in item.GetComponentsInChildren<ReplaceablePart>(true)) part.PresentFresh();

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
        EndHold(false);
        EndGrab();
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
        EndHold(false);
        EndGrab();
        foreach (CircuitPuzzle puzzle in focusedCircuits)
            if (puzzle != null) puzzle.HideForInspection();
        focusedCircuits = System.Array.Empty<CircuitPuzzle>();
        rotateGesture = false;
        settling = false;
        if (flipping != null) { StopCoroutine(flipping); flipping = null; }
        if (focusedItem != null)
        {
            focusedItem.transform.position = restPosition;
            focusedItem.transform.rotation = restRotation;
        }
        focusedItem = null;
        if (inspectCam != null)
        {
            inspectCam.Priority = 0;
            if (camOffset != Vector3.zero)
            {
                inspectCam.transform.position = camRestPosition;
                inspectCam.transform.rotation = camRestRotation;
            }
        }
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
        float dt = Time.deltaTime;

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

        // Controller: LB / RB cycle the bench tools; the left stick spins the item on the mat (sideways) and tilts an
        // edge up to peek under (up or down). R or Y flips it over.
        if (PadInput.Pressed(PadButton.RightShoulder)) CycleTool(1);
        else if (PadInput.Pressed(PadButton.LeftShoulder)) CycleTool(-1);
        Vector2 spin = PadInput.Curved(PadInput.LeftStick, 1.3f);
        stickTurning = spin != Vector2.zero && holdTarget == null && grabTarget == null && flipping == null;
        if (stickTurning)
        {
            yaw += -spin.x * padRotateSpeed * dt;
            yawVelocity = -spin.x * padRotateSpeed;
            tilt = Mathf.MoveTowards(tilt, spin.y * peekTilt, peekTilt * 4f * dt);
            tiltVelocity = 0f;
            settling = false;
        }
        if (holdTarget == null && grabTarget == null && flipping == null
            && (PadInput.Pressed(PadButton.North) || Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
            flipping = StartCoroutine(Flip());

        HandleZoom(dt);

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
        bool hitSomething = false;
        RaycastHit hit = default;

        foreach (CircuitPuzzle puzzle in focusedCircuits)
        {
            if (puzzle == null) continue;
            CircuitTile tile = puzzle.TileAtScreenPoint(pointer);
            if (tile != null) { hovered = tile; break; }
        }
        if (!overBoard && hovered == null && Physics.Raycast(ray, out hit, benchReach, ~0, QueryTriggerInteraction.Ignore))
        {
            hitSomething = true;
            ResolveBenchHit(hit.collider, out hovered, out grime, out hoveredTool);
            // The tool in the hand is never what the cursor is over.
            if (hoveredTool != null && hoveredTool == currentToolPickup) { hoveredTool = null; }
        }

        hand = new BenchHand { ray = ray, hit = hit, hitSomething = hitSomething, tool = currentTool, toolModel = currentToolPickup, camera = cam, deltaTime = dt };

        BenchInteractable target = null;
        HoverName = "";
        HoverAction = "";

        if (holdTarget != null)
        {
            HoverName = holdTarget.DisplayName;
            HoverAction = holdTarget.Prompt;
        }
        else if (grabTarget != null)
        {
            HoverName = grabTarget.DisplayName;
            HoverAction = "Let go to drop it" + (grabTarget is RemovablePart ? " (near its seat: it snaps home)" : "");
        }
        else if (hoveredTool != null)
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
                HoverAction = hovered is CircuitTile || hovered is ReplaceablePart || hovered is LoosePart || hovered is RemovablePart ? hovered.Prompt : "Not yet";
            else if (currentTool == hovered.RequiredTool || hovered.RequiredTool == ToolType.Hand)
            {
                target = hovered;
                HoverAction = hovered.Prompt;
            }
            else
                HoverAction = $"Needs the {hovered.RequiredTool.ToString().ToLower()}";
        }

        SetBenchHover(holdTarget != null ? holdTarget : grabTarget != null ? grabTarget : target);

        // Left click / RT.
        if (GamePointer.PrimaryPressed)
        {
            HandleBenchPress(hovered, grime, hoveredTool, overBoard);
        }

        bool held = GamePointer.PrimaryHeld;
        if (held)
        {
            Vector2 delta = GamePointer.Delta;

            if (holdTarget != null)
            {
                bool finished = holdTarget.HoldTick(hand);
                if (finished) EndHold(true);
            }
            else if (grabTarget != null)
            {
                grabTarget.GrabMove(hand);
            }
            else if (currentTool == ToolType.Brush)
            {
                // Wide Brush was sold in the upgrade shop but never read here,
                // so buying it changed nothing. It now scales every stroke.
                float brush = UpgradeManager.Instance != null ? UpgradeManager.Instance.ScrubSpeedMultiplier : 1f;
                // A stick can't scrub back and forth like a mouse, so holding
                // RT on the grime scrubs at a steady rate as well.
                float travel = delta.magnitude + (GamePointer.PadCursorActive ? padScrubRate * dt : 0f);
                if (grime != null)
                {
                    grime.Scrub(travel * scrubPower * brush);
                    if (currentToolPickup != null) currentToolPickup.Animate(ToolPickup.Motion.Stroke, 0f, 1f, dt);
                }
            }
            else if (currentTool == ToolType.Hand && rotateGesture && !overBoard && flipping == null)
            {
                // Sideways spins it on the mat; up or down tilts an edge to peek under (it lies back flat when let go).
                yaw += -delta.x * rotateSpeed;
                tilt = Mathf.Clamp(tilt + delta.y * rotateSpeed, -peekTilt, peekTilt);
                tiltVelocity = 0f;
                // The spin it will keep when let go (smoothed over a few frames).
                float instant = -delta.x * rotateSpeed / Mathf.Max(dt, 1e-4f);
                yawVelocity = Mathf.Lerp(yawVelocity, instant, 1f - Mathf.Exp(-14f * dt));
                settling = false;
            }
        }
        else
        {
            if (holdTarget != null) EndHold(false);
            if (grabTarget != null) EndGrab();
            rotateGesture = false;
            Coast(dt);
        }
        if (flipping == null) Pose();

        PlaceTool(target, grime, hitSomething, hit, ray, dt);
    }

    // ---------- the device on the mat: its pose, the spin's inertia and the settle, the peek, the flip ----------

    // Where the device rests: on the mat's top, where it was set down (its own slot, so two devices on the mat never share
    // a spot; the zoom closes on it there). Without a stage, the inspect point, as before.
    private Vector3 TableTopPoint(Transform item)
    {
        Vector3 p = inspectPoint.position;
        BenchStage stage = BenchStage.Instance;
        if (stage != null && stage.Mat != null)
        {
            Collider matCollider = stage.Mat.GetComponent<Collider>();
            Renderer matRenderer = stage.Mat.GetComponent<Renderer>();
            Bounds matBounds = matRenderer != null ? matRenderer.bounds : matCollider != null ? matCollider.bounds : new Bounds(stage.Mat.position, new Vector3(.6f, .006f, .36f));
            p = item.position;
            // Kept well inside the mat (a device set down near its edge still gets the whole of itself on it).
            p.x = Mathf.Clamp(p.x, matBounds.min.x + .09f, matBounds.max.x - .09f);
            p.z = Mathf.Clamp(p.z, matBounds.min.z + .09f, matBounds.max.z - .09f);
            p.y = matBounds.max.y;
        }
        return p;
    }

    // The view's forward laid flat on the table: "up the screen" on the mat. Square-on means the device's long side runs
    // this way (a phone stands portrait on the screen).
    private Vector3 ViewForwardOnTable()
    {
        Transform view = View;
        Vector3 f = view != null ? Vector3.ProjectOnPlane(view.forward, Vector3.up) : Vector3.forward;
        return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
    }

    private Vector3 ViewRightOnTable()
    {
        Transform view = View;
        Vector3 r = view != null ? Vector3.ProjectOnPlane(view.right, Vector3.up) : Vector3.right;
        return r.sqrMagnitude > 1e-6f ? r.normalized : Vector3.right;
    }

    // The close-up's point of view: the inspection camera's transform (its direction never changes; the zoom only moves it).
    private Transform View
    {
        get
        {
            if (inspectCam != null) return inspectCam.transform;
            if (cam == null) cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }

    /// <summary>The rotation that lays the device on the mat with the given face up and its long side up the view (yaw 0).</summary>
    private Quaternion Lay(float face)
    {
        Vector3 up = thinAxis * face;
        Vector3 along = longAxis;
        if (Mathf.Abs(Vector3.Dot(up, along)) > .99f) along = Vector3.Cross(up, Vector3.right).sqrMagnitude > .01f ? Vector3.Cross(up, Vector3.right) : Vector3.Cross(up, Vector3.forward);
        return Quaternion.LookRotation(ViewForwardOnTable(), Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(along, up));
    }

    /// <summary>The device's rotation from its pose: the tilt (about the view's right), the yaw (about up), the face.</summary>
    private Quaternion PoseRotation(float face, float aboutUp, float peek)
        => Quaternion.AngleAxis(peek, ViewRightOnTable()) * Quaternion.AngleAxis(aboutUp, Vector3.up) * Lay(face);

    // The device set down from its pose: turned as the pose says, its bounds' middle over the table point, its lowest
    // corner on the mat (an edge tilted up lifts it; a flip lifts it clear).
    private void Pose(float extraLift = 0f)
    {
        if (focusedItem == null) return;
        if (Time.time - boundsAt > .5f) MeasureSlab(focusedItem.transform);
        Place(PoseRotation(faceSign, yaw, tilt), extraLift);
    }

    private void Place(Quaternion rotation, float extraLift)
    {
        Transform t = focusedItem.transform;
        t.rotation = rotation;
        float lowest = float.PositiveInfinity;
        Vector3 centre = rotation * ((localMin + localMax) * .5f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? localMin.x : localMax.x, (i & 2) == 0 ? localMin.y : localMax.y, (i & 4) == 0 ? localMin.z : localMax.z);
            lowest = Mathf.Min(lowest, (rotation * corner).y);
        }
        if (float.IsInfinity(lowest)) lowest = 0f;
        t.position = new Vector3(tableTop.x - centre.x, tableTop.y - lowest + extraLift, tableTop.z - centre.z);
    }

    /// <summary>Lays the device flat and square at a yaw (the labs' hand; the device's own settle does this by itself).</summary>
    public void LayDown(float aboutUp = 0f)
    {
        yaw = aboutUp;
        yawVelocity = tiltVelocity = 0f;
        tilt = 0f;
        settling = false;
        settled = true;
        Pose();
    }

    // Let go: the spin coasts and dies, and the device settles square to the view when it stops near a quarter turn; a
    // lifted edge lies back flat.
    private void Coast(float dt)
    {
        if (focusedItem == null || flipping != null || stickTurning) return;   // (the stick's own turn is not to be doubled: 7 Oct, the lab's swing went twice as far)
        if (Mathf.Abs(yawVelocity) > 1f)
        {
            yaw += yawVelocity * dt;
            yawVelocity *= Mathf.Exp(-6f * dt);
            if (Mathf.Abs(yawVelocity) < 15f) { yawVelocity = 0f; BeginSettle(); }
        }
        else if (settling)
        {
            float remaining = Mathf.DeltaAngle(yaw, settleYaw);
            if (Mathf.Abs(remaining) < .3f)
            {
                yaw = settleYaw;
                settling = false;
                if (!settled) { settled = true; Sfx.Play("device.snap", focusedItem.transform.position); }
            }
            else yaw += remaining * (1f - Mathf.Exp(-10f * dt));
        }
        // The peek lies back: a spring, critically damped, so it lands without a bounce.
        if (Mathf.Abs(tilt) > .01f || Mathf.Abs(tiltVelocity) > .01f)
        {
            const float w = 14f;
            float accel = -w * w * tilt - 2f * w * tiltVelocity;
            tiltVelocity += accel * dt;
            tilt += tiltVelocity * dt;
            if (Mathf.Abs(tilt) < .02f && Mathf.Abs(tiltVelocity) < .5f) { tilt = 0f; tiltVelocity = 0f; }
        }
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
    }

    // The nearest quarter turn; within reach, the device goes there (else it stays as it was left).
    private void BeginSettle()
    {
        settling = false;
        settled = false;
        if (focusedItem == null || faceSnapWithin <= 0f) return;
        float nearest = Mathf.Round(yaw / 90f) * 90f;
        if (Mathf.Abs(Mathf.DeltaAngle(yaw, nearest)) <= faceSnapWithin) { settleYaw = nearest; settling = true; }
    }

    // Over it goes, along its length, lifted clear of the mat on the way, onto its other face.
    private IEnumerator Flip()
    {
        if (focusedItem == null) yield break;
        settling = false;
        yawVelocity = 0f;
        tiltVelocity = 0f;
        float fromTilt = tilt;
        float oldFace = faceSign;
        Vector3 longWorld = Quaternion.AngleAxis(yaw, Vector3.up) * ViewForwardOnTable();
        // Half the width it turns on, plus a little: how high its middle has to be at the half-way point.
        Vector3 size = localMax - localMin;
        float across = Mathf.Abs(Vector3.Dot(size, Vector3.one) - Mathf.Abs(Vector3.Dot(size, thinAxis)) - Mathf.Abs(Vector3.Dot(size, longAxis)));
        float clearance = across * .5f + .012f;
        Sfx.Play("device.flip", focusedItem.transform.position);
        float t = 0f;
        while (t < 1f && focusedItem != null)
        {
            t += Time.deltaTime / .32f;
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Quaternion turn = Quaternion.AngleAxis(180f * c, longWorld);
            Quaternion rotation = turn * PoseRotation(oldFace, yaw, Mathf.Lerp(fromTilt, 0f, c));
            Place(rotation, Mathf.Sin(c * Mathf.PI) * clearance);
            yield return null;
        }
        if (focusedItem != null)
        {
            faceSign = -oldFace;
            tilt = 0f;
            flipping = null;
            Pose();
            Sfx.Play("device.snap", focusedItem.transform.position, .7f);
        }
        flipping = null;
    }

    // The device's slab: its thinnest and longest local axes, and its renderers' bounds in its own frame (turned with it,
    // but in metres: its scale is kept in, so the corners can be turned by a rotation alone).
    private void MeasureSlab(Transform root)
    {
        boundsAt = Time.time;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        Quaternion unturn = Quaternion.Inverse(root.rotation);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.GetComponent<BenchOutlineHull>() != null) continue;
            // The mesh's own box, carried into the world, so a turned device measures the same as a square one.
            var mf = r.GetComponent<MeshFilter>();
            Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : r.bounds;
            bool own = mf != null && mf.sharedMesh != null;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                if (own) corner = r.transform.TransformPoint(corner);
                Vector3 local = unturn * (corner - root.position);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        if (float.IsInfinity(min.x)) { min = -Vector3.one * .03f; max = Vector3.one * .03f; }
        localMin = min; localMax = max;
        Vector3 size = max - min;
        if (size.x <= size.y && size.x <= size.z) thinAxis = Vector3.right;
        else if (size.y <= size.z) thinAxis = Vector3.up;
        else thinAxis = Vector3.forward;
        if (size.x >= size.y && size.x >= size.z) longAxis = Vector3.right;
        else if (size.y >= size.z) longAxis = Vector3.up;
        else longAxis = Vector3.forward;
        if (longAxis == thinAxis) longAxis = thinAxis == Vector3.up ? Vector3.forward : Vector3.up;
    }

    // ---------- the zoom ----------

    private void HandleZoom(float dt)
    {
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        float change = 0f;
        // A notch of a wheel is 120 on Windows and a handful on a Mac; a trackpad gives a little every frame. A step per
        // notch, no more than a step a frame, so a brush of a trackpad doesn't throw the camera to its limit (8 Oct:
        // "somehow just broke the camera").
        if (Mathf.Abs(wheel) > .01f) change += Mathf.Sign(wheel) * Mathf.Clamp(Mathf.Abs(wheel) * .0015f + .03f, .03f, .18f);
        if (PadInput.Held(PadButton.DpadUp)) change += dt * 1.2f;
        if (PadInput.Held(PadButton.DpadDown)) change -= dt * 1.2f;
        if (change != 0f) zoom = Mathf.Clamp(zoom + change, -1f, 1f);     // -1 fully out, 0 as placed, 1 fully in
        if (zoomShown < -1.5f) zoomShown = zoom;
        if (Mathf.Abs(zoom - zoomShown) < .0005f) return;
        zoomShown = Mathf.Lerp(zoomShown, zoom, 1f - Mathf.Exp(-12f * dt));
        float factor = zoomShown >= 0f ? Mathf.Lerp(1f, zoomRange.y, zoomShown) : Mathf.Lerp(1f, zoomRange.x, -zoomShown);
        if (inspectCam != null)
            inspectCam.transform.position = tableTop + camOffset * factor;
    }

    // ---------- holds and grabs ----------

    private void BeginHold(BenchInteractable part)
    {
        holdTarget = part;
        part.HoldBegin(hand);
        SetBenchHover(part);
    }

    private void EndHold(bool finished)
    {
        if (holdTarget == null) return;
        BenchInteractable part = holdTarget;
        holdTarget = null;
        part.HoldEnd(hand, finished);
        if (currentToolPickup != null) currentToolPickup.Rest();
    }

    private void BeginGrab(BenchInteractable part)
    {
        grabTarget = part;
        part.GrabBegin(hand);
        SetBenchHover(part);
    }

    private void EndGrab()
    {
        if (grabTarget == null) return;
        BenchInteractable part = grabTarget;
        grabTarget = null;
        part.GrabEnd(hand);
    }

    // The tool in the hand: on the part being worked, over the part under the cursor, on the surface under the cursor,
    // or floating by the cursor over nothing.
    private void PlaceTool(BenchInteractable target, GrimeSpot grime, bool hitSomething, RaycastHit hit, Ray ray, float dt)
    {
        if (currentToolPickup == null) return;
        ToolPickup tool = currentToolPickup;
        if (holdTarget != null)
        {
            tool.Follow(holdTarget.WorkPoint, holdTarget.WorkNormal, cam, true, dt);
            float direction = holdTarget is ScrewSocket || holdTarget is ScrewTarget st && st.GetComponent<Screw>().IsOut ? -1f : 1f;
            ToolPickup.Motion motion = currentTool switch
            {
                ToolType.Screwdriver => ToolPickup.Motion.Turn,
                ToolType.Tweezers => ToolPickup.Motion.Pinch,
                ToolType.Pry => ToolPickup.Motion.Lever,
                ToolType.Cloth => ToolPickup.Motion.Circle,
                _ => ToolPickup.Motion.None
            };
            tool.Animate(motion, holdTarget.HoldProgress, direction, dt);
            return;
        }
        if (grabTarget != null)
        {
            // The tweezers hold the part; the hand tool holds a cover: the tool rides with the cursor over it.
            tool.Follow(BenchInteractable.BoundsCentre(grabTarget.transform) + (cam.transform.position - grabTarget.transform.position).normalized * .01f,
                (cam.transform.position - grabTarget.transform.position).normalized, cam, true, dt);
            tool.Animate(ToolPickup.Motion.Pinch, 1f, 1f, dt);
            return;
        }
        if (target != null) { tool.Follow(target.WorkPoint, target.WorkNormal, cam, false, dt); return; }
        if (grime != null && hitSomething) { tool.Follow(hit.point, hit.normal, cam, GamePointer.PrimaryHeld, dt); return; }
        if (hitSomething && hit.collider.GetComponentInParent<JobBase>() == focusedItem) { tool.Follow(hit.point, hit.normal, cam, false, dt); return; }
        // Over nothing: by the cursor, 35 cm out, tip toward the item.
        Vector3 point = cam.transform.position + ray.direction * .35f;   // 35 cm from the eye (the ray's own origin is out on the near plane)
        tool.Follow(point, -ray.direction, cam, false, dt);
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
        foreach (GameObject part in focusedItem.LooseParts)
            if (part != null) GatherWork(part.transform);
        foreach (var w in workPoints) snapPoints.Add(w.screen);
        if (railTools == null) railTools = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude);
        foreach (ToolPickup tool in railTools)
            if (tool != null && !tool.InHand && OnScreen(BoundsCentre(tool.transform), out Vector2 s)) snapPoints.Add(s);
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
        if (part is ScrewSocket) return 6;
        if (part is RemovablePart cover) return cover.IsRemoved ? 5 : 1;
        if (part is ReplaceablePart || part is LoosePart) return 3;
        if (part is CircuitTile) return 4;
        return 7;
    }

    private bool OnScreen(Vector3 world, out Vector2 screen)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        screen = p;
        return p.z > cam.nearClipPlane && p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
    }

    private static Vector3 BoundsCentre(Transform t) => BenchInteractable.BoundsCentre(t);

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
        EndHold(false);
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
        if (part != null && !part.enabled) part = null;
    }

    private void HandleBenchPress(BenchInteractable part, GrimeSpot grime, ToolPickup tool, bool overBoard)
    {
        if (Time.timeScale <= 0f || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        // Only a drag that starts on empty space rotates the item. Picking a
        // tool, turning a wire, or pressing a covered part owns that press.
        rotateGesture = part == null && grime == null && tool == null && !overBoard;
        if (rotateGesture) { settling = false; yawVelocity = 0f; }
        if (tool != null)
        {
            EndHold(false);
            if (currentToolPickup != null) currentToolPickup.SetSelected(false);
            currentToolPickup = tool;
            currentTool = tool.tool;
            tool.SetSelected(true);
        }
        else if (grime == null && part != null && part.isActiveAndEnabled && part.CanInteract
            && (currentTool == part.RequiredTool || part.RequiredTool == ToolType.Hand))
        {
            if (part.Holdable) BeginHold(part);
            else if (part.Grabbable) BeginGrab(part);
            else part.Activate();
        }
    }

    private void ClearTool()
    {
        EndHold(false);
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
