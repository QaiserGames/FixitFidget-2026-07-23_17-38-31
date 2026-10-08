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
// device and does its motion at the part. Rotating the device by dragging has a little inertia and snaps to the
// nearest face when let go near one (so it never slides past the angle you need); R / Y flips it; the scroll wheel or
// the D-pad up/down zooms the close-up. The clock runs throughout.
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
    [Tooltip("Within this many degrees of a face-on orientation, a device let go settles onto it.")]
    [SerializeField, Range(0f, 45f)] private float faceSnapWithin = 22f;
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

    // v2: the hold and the grab under way, the device's spin, the camera's zoom.
    private BenchInteractable holdTarget, grabTarget;
    private BenchHand hand;
    private Vector2 spinVelocity;          // degrees per second about the camera's up (x) and right (y)
    private bool settling, settled;
    private bool stickTurning;              // the left stick turned the device this frame: no coasting on top of it
    private Quaternion settleTo;
    private Vector3 thinAxis = Vector3.up;   // the device's thinnest local axis: its two work faces are either side of it
    private Coroutine flipping;
    private Vector3 camOffset;
    private Quaternion camRestRotation;
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
        spinVelocity = Vector2.zero;
        settling = settled = false;
        zoom = 0f;
        zoomShown = -2f;
        camOffset = inspectCam.transform.position - inspectPoint.position;
        camRestRotation = inspectCam.transform.rotation;

        item.transform.position = inspectPoint.position;
        inspectCam.Priority = 30;
        CurrentJobCard = item.JobCard;
        // Presented face-on: a device lying flat on the bench would show the camera its edge. It goes back as it lay.
        thinAxis = ThinAxis(item.transform);
        item.transform.rotation = NearestFace(item.transform.rotation);
        settled = true;

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
            if (inspectPoint != null && camOffset != Vector3.zero)
            {
                inspectCam.transform.position = inspectPoint.position + camOffset;
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

        // Controller: LB / RB cycle the bench tools, the left stick turns the item. R or Y flips it.
        if (PadInput.Pressed(PadButton.RightShoulder)) CycleTool(1);
        else if (PadInput.Pressed(PadButton.LeftShoulder)) CycleTool(-1);
        Vector2 spin = PadInput.Curved(PadInput.LeftStick, 1.3f);
        stickTurning = spin != Vector2.zero && holdTarget == null && grabTarget == null;
        if (stickTurning)
        {
            float step = padRotateSpeed * dt;
            Turn(-spin.x * step, spin.y * step);
            spinVelocity = new Vector2(-spin.x, spin.y) * padRotateSpeed;
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
            else if (currentTool == ToolType.Hand && rotateGesture && !overBoard)
            {
                Turn(-delta.x * rotateSpeed, delta.y * rotateSpeed);
                // The spin it will keep when let go (smoothed over a few frames).
                Vector2 instant = new Vector2(-delta.x, delta.y) * rotateSpeed / Mathf.Max(dt, 1e-4f);
                spinVelocity = Vector2.Lerp(spinVelocity, instant, 1f - Mathf.Exp(-14f * dt));
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

        PlaceTool(target, grime, hitSomething, hit, ray, dt);
    }

    // ---------- the device's spin: inertia, and the settle onto a face ----------

    private void Turn(float aboutUp, float aboutRight)
    {
        focusedItem.transform.Rotate(cam.transform.up, aboutUp, Space.World);
        focusedItem.transform.Rotate(cam.transform.right, aboutRight, Space.World);
    }

    private void Coast(float dt)
    {
        if (focusedItem == null || flipping != null || stickTurning) return;   // (the stick's own turn is not to be doubled: 7 Oct, the lab's swing went twice as far)
        if (spinVelocity.sqrMagnitude > 1f)
        {
            Turn(spinVelocity.x * dt, spinVelocity.y * dt);
            spinVelocity *= Mathf.Exp(-6f * dt);
            if (spinVelocity.magnitude < 15f) { spinVelocity = Vector2.zero; BeginSettle(); }
            return;
        }
        if (!settling) return;
        Quaternion now = focusedItem.transform.rotation;
        float angle = Quaternion.Angle(now, settleTo);
        if (angle < .3f)
        {
            focusedItem.transform.rotation = settleTo;
            settling = false;
            if (!settled) { settled = true; Sfx.Play("device.snap", focusedItem.transform.position); }
            return;
        }
        focusedItem.transform.rotation = Quaternion.Slerp(now, settleTo, 1f - Mathf.Exp(-10f * dt));
    }

    // The nearest face-on orientation; within reach, the device goes there (else it stays as it was left).
    private void BeginSettle()
    {
        settling = false;
        settled = false;
        if (focusedItem == null || faceSnapWithin <= 0f) return;
        Quaternion now = focusedItem.transform.rotation;
        Quaternion best = NearestFace(now);
        if (Quaternion.Angle(now, best) <= faceSnapWithin) { settleTo = best; settling = true; }
    }

    // The eight orientations that show the camera one of the device's two work faces square-on (either face, spun to
    // any of the four uprights), and the nearest of them to a rotation. Never an edge: a phone, a watch and a camera are
    // slabs, and the faces either side of the thin axis are where the work is.
    private Quaternion NearestFace(Quaternion now)
    {
        // The close-up camera's own frame, not the live camera's: when the close-up opens the live camera is still
        // Ace's, part way through the blend.
        Transform view = View;
        if (view == null) return now;
        Vector3 toCamera = -view.forward;
        Vector3 across = thinAxis == Vector3.up || thinAxis == Vector3.right ? Vector3.forward : Vector3.up;   // a long axis of the slab
        Quaternion best = now;
        float bestAngle = float.PositiveInfinity;
        Vector3[] uprights = { view.up, view.right, -view.up, -view.right };
        for (int face = -1; face <= 1; face += 2)
        {
            Quaternion local = Quaternion.LookRotation(thinAxis * face, across);
            foreach (Vector3 up in uprights)
            {
                Quaternion candidate = Quaternion.LookRotation(toCamera, up) * Quaternion.Inverse(local);
                float a = Quaternion.Angle(now, candidate);
                if (a < bestAngle) { bestAngle = a; best = candidate; }
            }
        }
        return best;
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

    // The device's thinnest local axis, from its renderers' bounds.
    private static Vector3 ThinAxis(Transform root)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 local = root.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        if (float.IsInfinity(min.x)) return Vector3.up;
        Vector3 size = max - min;
        if (size.x <= size.y && size.x <= size.z) return Vector3.right;
        if (size.y <= size.z) return Vector3.up;
        return Vector3.forward;
    }

    private IEnumerator Flip()
    {
        if (focusedItem == null) yield break;
        settling = false;
        spinVelocity = Vector2.zero;
        Quaternion from = focusedItem.transform.rotation;
        Quaternion to = Quaternion.AngleAxis(180f, View.up) * from;
        Sfx.Play("device.flip", focusedItem.transform.position);
        float t = 0f;
        while (t < 1f && focusedItem != null)
        {
            t += Time.deltaTime / .3f;
            focusedItem.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        if (focusedItem != null) focusedItem.transform.rotation = to;
        flipping = null;
        BeginSettle();
    }

    // ---------- the zoom ----------

    private void HandleZoom(float dt)
    {
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        float change = 0f;
        if (Mathf.Abs(wheel) > .01f) change += Mathf.Sign(wheel) * .18f;
        if (PadInput.Held(PadButton.DpadUp)) change += dt * 1.2f;
        if (PadInput.Held(PadButton.DpadDown)) change -= dt * 1.2f;
        if (change != 0f) zoom = Mathf.Clamp(zoom + change, -1f, 1f);     // -1 fully out, 0 as placed, 1 fully in
        if (zoomShown < -1.5f) zoomShown = zoom;
        if (Mathf.Abs(zoom - zoomShown) < .0005f) return;
        zoomShown = Mathf.Lerp(zoomShown, zoom, 1f - Mathf.Exp(-12f * dt));
        float factor = zoomShown >= 0f ? Mathf.Lerp(1f, zoomRange.y, zoomShown) : Mathf.Lerp(1f, zoomRange.x, -zoomShown);
        if (inspectCam != null && inspectPoint != null)
            inspectCam.transform.position = inspectPoint.position + camOffset * factor;
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
        Vector3 point = ray.origin + ray.direction * .35f;
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
        if (rotateGesture) { settling = false; spinVelocity = Vector2.zero; }
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
