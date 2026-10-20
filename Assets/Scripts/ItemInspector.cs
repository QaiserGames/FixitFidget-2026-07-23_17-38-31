using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// ---------------------------------------------------------------------------
// WORKING ON AN ITEM AT THE BENCH
//
// The inspection close-up: the item lies on the repair mat, which the inspection camera frames with the stage (the mat's
// cups, pad and well, the old-parts tray, the tool rest); tools are taken off the rest and used on the item with the pointer
// (the mouse, or the pad's cursor, PadCursor).
//
// THE FORGIVING BENCH (14 Oct 2026; specs/systems-forgiving-bench.md §4.4-4.6, td-one-scale.md §5 and §6.7,
// uiux-checklist-v2.md §5.10 item 7). His playtest 5: "i couldnt get past day 1 because the bench was too hard". What
// changed here:
//   * THE PICKER (§4.6). What a press acts on is no longer whatever collider the cursor's ray hits first. Every frame each
//     candidate (the job's parts, its loose pieces, its dirt spots, the tools at rest) is projected to the screen and measured
//     against its own hit area: a screw or a hole a circle of at least 28 px (1080p) round its middle, a cover its outline + 12
//     px, a part + 14, a spot + 16, a tool at rest + 16. Of those the cursor is inside, the best by four classes wins (the step
//     to do now's targets the tool in hand can act on; other targets it can act on; the step's targets it can't; anything
//     else), then the nearest by normalised distance. A target can't be hit under a cover, facing away, or out of sight unless
//     it is one of the step's own (those are hit through what hides them, and drawn dashed). A hold owns its target until the
//     button is let go, wherever the cursor drifts. The physics ray stays for the surface point.
//   * THE DIRECT GRAB (§4.4.1). A press on a tool at rest puts it in the hand on that frame (tool.pick), its tip exactly under
//     the cursor from then on (ToolPickup). Let go: a tap, or over the device or its own place on the rest, and it stays in the
//     hand; anywhere else it flies home. The cloth and the brush go home by themselves when their last spot on the device is
//     clean (and the press is spent until the button is up).
//   * THE RUB (§4.4.3-4.4.5). The cloth or the brush held down over its own dirt presses down and rubs: its footprint (a disc
//     of the spot's clean radius, at the cursor's ray on the spot's plane, so a tiny spot and a 15 fps jump both behave) sweeps
//     from last frame's contact to this one's (GrimeSpot.Pass). A pad held still rubs in place: the footprint orbits the cursor.
//     The stroke's speed drives the rub loop, the squeak, the cloth's bunch and the brush's bend, the pad's rumble, and the
//     device's budge, a few pixels along the stroke (added in Pose; it never turns it): BenchFeel.
//   * THE CAMERA (the TD's §5, §6.7). The close-up is placed from the device's layout (DeviceDefinition.benchLayout: aim,
//     distance, pitch 58°, FOV 40) before it takes over; the zoom is a lens zoom (the FOV narrows toward the close aim, the
//     camera stays at its distance: the near clip, the lamp and the perspective don't change). Let go, the scene's pose and FOV
//     40 come back. A device with no framing yet keeps the scene's framing, and zooms its lens.
//   * THE WORDS (UI/UX §5.10 item 7). The hover is the part's name; its own "why not" when it is blocked; "Needs the cloth"
//     when the wrong tool is in hand; never "Hold to ..." or a percentage.
//   * "WHAT'S LEFT" BY ITSELF (the feel targets' T17): on the first repair, after 3 s of scrubbing with nothing coming off,
//     BenchWork.Flash fires once (UI/UX's card owns the rest).
//
// Kept from before (the history below): the tabletop (the device lies on the mat, spins, peeks and flips), the stick's
// quarter turns, the flip, R / Y, the aim help's snap (now from the picker's targets, with friction over the step's targets
// and the D-pad reaching hidden ones), A uses as RT does, B / right-click / Esc step back, E takes it away.
//
// STATIONS AS REACH (playtest 3, 7 Oct 2026): in first person, click or RT on a device on the bench (PlayerInteractor); from
// above, E on it. B, Esc or right-click step back (the tool first, then the item), and E picks the item up and steps back in
// one press.
//
// THE TABLETOP (8 Oct 2026; "the exact same way restory has it"). The device LIES ON THE MAT, one work face up, under a camera
// looking down at it; nothing hangs in the air. It spins on the mat (drag sideways, or the stick), with a little inertia, and
// settles square (to the nearest quarter turn) when let go; drag up or down (or push the stick) and it TILTS to let you peek
// under an edge, and lies back flat when let go; R / Y FLIPS it over, along its length, onto its other face. Its pose is
// always one of these, so it can never be left at an angle nothing can be done at.
//
// HIS PLAYTEST WITH A CONTROLLER (12 Oct 2026): the device is measured by its own solid parts only (DeviceBounds); the device
// worked on always lies at its place on the mat, and a device already lying there moves to this one's place on the bench
// until the work is done; on a pad A uses, as RT does; X picks it up and steps back.
//
// THE STICK, SIMPLE (12-13 Oct 2026): a push of the stick SIDEWAYS turns the device exactly one quarter, eased over a third of a
// second, landing with a click (device.snap); held, it keeps turning a quarter at a time; UP or DOWN lifts an edge to peek
// under only when the push is clearly up or down; with GameSettings.SwapBenchSticks the sticks swap at the bench.
// ---------------------------------------------------------------------------
public class ItemInspector : MonoBehaviour
{
    [SerializeField] private Transform inspectPoint;
    [SerializeField] private CinemachineCamera inspectCam;
    [SerializeField] private float rotateSpeed = 0.4f;
    [SerializeField] private float benchReach = 6f;

    [Header("Controller")]
    [Tooltip("A push of the turn stick sideways turns the device one quarter, eased over this many seconds (12 Oct: quarter turns).")]
    [SerializeField, Range(.1f, .6f)] private float quarterTurnSeconds = .28f;
    [Tooltip("Held sideways, the stick turns another quarter every this many seconds (a calm pace; longer than the turn itself, so each lands first).")]
    [SerializeField, Range(.3f, 1f)] private float quarterRepeatSeconds = .45f;
    [Tooltip("A pad holding the cloth or the brush still on its dirt rubs as if the hand moved this fast (1080p pixels a second): the " +
             "stroke speed the rub loop, the rumble and the look follow (BenchFeel), while the footprint orbits the cursor.")]
    [SerializeField, Min(0f)] private float padScrubRate = 180f;

    // The stick's three readings (the brief's numbers): sideways past this much is a turn, up or down past this much is a
    // peek, and either only when it is clearly that way (this many times the other axis). Between them, nothing.
    public const float TurnPush = .5f, PeekPush = .7f, SideRatio = 1.6f;

    [Header("The bench")]
    [Tooltip("Within this many degrees of a quarter turn, a device let go spinning settles square to the view (45: always; Systems' decision 12).")]
    [SerializeField, Range(0f, 45f)] private float faceSnapWithin = 45f;
    [Tooltip("How far an edge can be tilted up to peek under it, degrees.")]
    [SerializeField, Range(10f, 60f)] private float peekTilt = 35f;
    [Tooltip("The lens zoom's magnification at zoom 1 for a device whose layout has no close framing yet (fovClose 0): the TD's " +
             "phone, 2.2. (Renamed from the dolly's zoomRange: a field whose meaning changed takes a new name, Bible §7.)")]
    [SerializeField, Range(1f, 4f)] private float lensZoomFallback = 2.2f;

    /// <summary>The grab rule's tap: a press let go within this long, the cursor moved less than this (1080p px): the tool stays
    /// in the hand (§4.4.1).</summary>
    public const float TapSeconds = .30f, TapPixels = 24f;
    /// <summary>"Over the device" for the grab rule: its outline on screen grown by this (1080p px), so a near miss beside a screw
    /// at its edge doesn't put the tool away.</summary>
    public const float OverDevicePixels = 32f;
    /// <summary>T17: scrubbing this long with nothing coming off fires "what's left" once, on the first repair.</summary>
    public const float StuckScrubSeconds = 3f;
    /// <summary>The pad's cursor over a target of the step to do now moves at this share of its speed (aim help on; §4.6).</summary>
    public const float PadFriction = .55f;
    /// <summary>The close-up's lens at zoom 0 and its look down at the mat (the TD: one camera language with Cinematics).</summary>
    public const float LayoutFov = 40f, Pitch = 58f;

    private JobBase focusedItem;
    private Vector3 restPosition;
    private Quaternion restRotation;

    // A device that lay in the middle of the mat, moved to this one's place for the work, and its own place to go back to.
    private Transform swappedDevice;
    private Vector3 swappedPosition;
    private Quaternion swappedRotation;

    /// <summary>Ace's inspector (one in the café), for the HUD's bench cards and the pad's cursor.</summary>
    public static ItemInspector Instance { get; private set; }
    /// <summary>The close-up is up: something is being worked on at the bench.</summary>
    public static bool Working => Instance != null && Instance.focusedItem != null;

    private BenchInteractable currentBenchHover;
    private ToolType currentTool = ToolType.Hand;
    private ToolPickup currentToolPickup;
    private Camera cam;
    private PlayerInteractor interaction;
    private CircuitPuzzle[] focusedCircuits = System.Array.Empty<CircuitPuzzle>();
    private bool rotateGesture;
    // The frame the item was picked up to work on: that press was the press that began it, not one on the item.
    private int begunFrame = -1;

    // The hold under way (a grab: none are left at the forgiving bench, kept for any part that still offers one).
    private BenchInteractable holdTarget, grabTarget;
    private float holdProgressSeen;
    private BenchHand hand;
    private Coroutine flipping;

    // The press under way: when and where it began, whether it took a tool or acted on something, and a press spent (the
    // auto-home after the last spot) until the button is up.
    private bool pressLive, pressTookTool, pressActed, consumedUntilUp;
    private float pressAt;
    private Vector2 pressFrom;
    private bool wasHeld;

    // The rub: the spots under the footprint this frame and last, last frame's ray (a stroke coming onto a spot starts where
    // the tool was), the contact the tool sits on, the pad's in-place orbit.
    private readonly List<GrimeSpot> rubbing = new();
    private readonly List<GrimeSpot> rubbedLast = new();
    private Ray lastRay;
    private bool hasLastRay;
    private Vector3 rubContact, rubNormal;
    private float orbit;
    private float padStillFor;   // how long the pad's cursor has been still on the rub (s)
    private bool padRubbingInPlace;
    private bool checkToolHome;
    // T17: scrubbing with no progress, and the job "what's left" has already fired for.
    private float scrubWithoutProgress, progressSeenAt = -10f;
    private JobBase flashedFor;

    // The camera: where the scene placed it, whether this close-up placed it, its layout aim, close aim and distance.
    private Vector3 camRestPosition;
    private Quaternion camRestRotation;
    private bool camPlaced, framed;
    private Vector3 layoutAim, closeAimWorld;
    private float camDistance = .48f;
    private float magnification = 2.2f;
    private Quaternion camLook = Quaternion.identity;
    private float lastNotchAt = -10f;

    // The tabletop: the device's pose on the mat is a face, a yaw and a tilt, never a free rotation.
    private Vector3 tableTop;               // where the device rests (its layout's place on the mat's top)
    private Vector3 thinAxis = Vector3.up;  // the device's thinnest local axis: its two work faces are either side of it
    private Vector3 longAxis = Vector3.forward;
    private Vector3 localMin, localMax;     // its renderers' bounds in its own frame
    private float faceSign = 1f;            // +1: local +thin is up; -1: local -thin is up
    private float yaw, yawVelocity;         // degrees about the world's up, from square-on (the long side up the view)
    private float tilt, tiltVelocity;       // degrees about the view's right, an edge lifted to peek under
    private bool stickTurning;              // the stick has the device this frame (a quarter turn under way, or a peek)
    // The quarter turns (12 Oct): the one easing now, which way the stick is pushed, the repeat, and whether the stick has been
    // let go once since the close-up opened (a thumb still walking Ace up to the bench must not turn the device as it opens).
    private bool turning;
    private float turnFrom, turnTarget, turnT;
    private int stickSide;
    private float repeatIn;
    private bool stickArmed;
    private bool settling, settled;
    private float settleYaw;
    private float boundsAt;
    private float zoom;                     // 0 = the layout view, 1 = the close view (the lens zoom)
    private float zoomShown = -2f;          // below -1.5: not shown yet

    public bool IsHoldingItem => focusedItem != null;
    /// <summary>Working on an item at the bench (the inspection close-up is up). Was "docked at the bench" until playtest 3.</summary>
    public bool IsAtWorkbench => focusedItem != null;
    public JobBase FocusedItem => focusedItem;
    public ToolType CurrentTool => currentTool;
    /// <summary>The tool in the hand, or null (labs).</summary>
    public ToolPickup ToolInHand => currentToolPickup;
    public BenchInteractable HoldTarget => holdTarget;
    public BenchInteractable GrabTarget => grabTarget;
    /// <summary>The lens zoom, 0 (the layout view) .. 1 (the close view).</summary>
    public float Zoom => zoom;
    /// <summary>The device's turn on the mat, degrees from square-on (its long side up the view).</summary>
    public float Yaw => yaw;
    /// <summary>The edge lifted to peek under, degrees (0: lying flat).</summary>
    public float Tilt => tilt;
    /// <summary>Which of the device's two work faces is up: +1 its local +thin axis, -1 its local -thin.</summary>
    public float FaceUp => faceSign;
    /// <summary>A quarter turn from the stick is easing to its quarter now (labs).</summary>
    public bool Turning => turning;
    /// <summary>How many quarter turns the stick has begun in this close-up (labs: one per push, one per 0.45 s held).</summary>
    public int QuarterTurns { get; private set; }
    /// <summary>
    /// The device lies with its face up: its screen, its dial, its lens. The face is the side it shows as it is handed over,
    /// its own up (+Y). True too when its face can't be told (a device whose thin axis isn't its up), so nothing is ever turned
    /// over on a guess.
    /// </summary>
    public bool FaceShowing => focusedItem == null || thinAxis != Vector3.up || faceSign > 0f;
    /// <summary>The device's thinnest local axis (its work faces are either side of it).</summary>
    public Vector3 ThinAxisLocal => thinAxis;
    /// <summary>Where the device rests: its layout's place on the mat's top.</summary>
    public Vector3 TableTop => tableTop;
    public string CurrentJobCard { get; private set; }
    public string HoverName { get; private set; }
    public string HoverAction { get; private set; }
    /// <summary>The way out at the bench (the pickup line). The task summary is the card's now (UI/UX §5.10 item 7).</summary>
    public string CollectionPrompt
    {
        get
        {
            if (focusedItem == null) return "";
            var carry = GetComponent<PlayerCarry>();
            if (carry == null || !carry.HasSpace) return "Hands full — free a hand to collect";
            return "Pick up item and step back";
        }
    }

    // ---------- what the labs read (the clumsy hand, Bench 3, Aim help) ----------

    /// <summary>The picker's choice under the cursor now: a part (a screw's Screw), a spot or a tool at rest; null for none.</summary>
    public Component Hovered => hasHover ? hover.key : null;
    /// <summary>The hovered target's normalised distance (0 its middle, 1 its hit area's edge) and class (1 best .. 4).</summary>
    public float HoveredDistance => hasHover ? hover.distance : float.PositiveInfinity;
    public int HoveredClass => hasHover ? hover.priority : 0;
    /// <summary>The hovered target is one of the step to do now's.</summary>
    public bool HoveredIsStepTarget => hasHover && hover.stepTarget;
    /// <summary>Presses at the bench in this close-up; whether the last one acted on something (took a tool, began a hold, a
    /// press, a rub on its own dirt), and what it was over.</summary>
    public int Presses { get; private set; }
    public bool LastPressActed => pressActed;
    public Component LastPressTarget { get; private set; }
    /// <summary>The cloth or the brush is pressed on its own dirt this frame (the spots under its footprint).</summary>
    public bool Rubbing => rubbing.Count > 0;
    public int RubbingSpots => rubbing.Count;
    /// <summary>The step to do now, as this inspector last built it (RepairChecklist), and its place in the list (labs).</summary>
    public RepairChecklist.Step CurrentStep => currentStep;
    public int CurrentStepIndex { get; private set; } = -1;
    /// <summary>The close-up camera was placed from the device's layout (false: the scene's framing kept).</summary>
    public bool Framed => framed;

    private void Awake()
    {
        cam = Camera.main;
        interaction = GetComponent<PlayerInteractor>();
        Instance = this;
    }

    private void OnEnable()
    {
        BenchWork.TargetDone += OnTargetDone;
        BenchWork.Flashed += OnFlashed;
    }

    private void OnDisable()
    {
        BenchWork.TargetDone -= OnTargetDone;
        BenchWork.Flashed -= OnFlashed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) BenchFeel.Silence();   // the rumble never runs on in the background
    }

    // A on a pad uses, as RT does (12 Oct, Mansoor: "A uses, X takes it"). Only in the close-up: everywhere else A keeps E's job.
    private static bool UsePressedRaw => GamePointer.PrimaryPressed
        || GamePointer.PadCursorActive && PadInput.Pressed(PadButton.South) && !PadCursor.PressConsumed;
    private static bool UseHeldRaw => GamePointer.PrimaryHeld
        || GamePointer.PadCursorActive && PadInput.Held(PadButton.South) && !PadCursor.PressConsumed;
    // ...unless the press was spent (the tool went home by itself under it): nothing until the button is up.
    private bool UsePressed => !consumedUntilUp && UsePressedRaw;
    private bool UseHeld => !consumedUntilUp && UseHeldRaw;

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
            if (focusedItem != null) BenchFeel.Silence();   // the pause: no rumble, no rub
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
    /// Takes <paramref name="item"/> (a device on the bench) into the inspection close-up: it lies at its layout's place on the
    /// mat and the inspection camera takes over, placed for it, from whatever view Ace is in. False when something is already in
    /// hand, the day is over, or the bench's camera isn't set up.
    /// </summary>
    public bool BeginInspection(JobBase item)
    {
        if (item == null || focusedItem != null || inspectPoint == null || inspectCam == null || Time.timeScale <= 0f
            || DayClock.Instance != null && DayClock.Instance.DayOver) return false;
        if (cam == null) cam = Camera.main;
        focusedItem = item;
        item.MarkWorkedOn();   // from now on, giving it back unfixed is a choice he can make (CustomerBrain.JobReady)
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
        turning = false;
        stickTurning = false;
        stickSide = 0;
        repeatIn = 0f;
        stickArmed = false;
        QuarterTurns = 0;
        Presses = 0;
        pressLive = pressActed = pressTookTool = consumedUntilUp = wasHeld = false;
        LastPressTarget = null;
        rubbing.Clear();
        rubbedLast.Clear();
        hasLastRay = false;
        scrubWithoutProgress = 0f;
        progressSeenAt = BenchWork.LastProgressAt;
        boxes.Clear();
        stepsAt = -10f;
        zoom = 0f;
        zoomShown = -2f;
        BenchFeel.Silence();
        BenchFeel.ResetBudge();
        CurrentJobCard = item.JobCard;

        // Laid on the mat, the face that was up on the bench still up, square to the view. It goes back as it lay. Where its
        // layout says (DeviceDefinition.benchLayout.rest, clear of the cups, the pad and the well); a device lying there takes
        // this one's place until it's done.
        MeasureSlab(item.transform);
        BenchLayout layout = DeviceDefinition.LayoutOf(item);
        BenchStage stage = BenchStage.Instance;
        tableTop = stage != null ? stage.MatToWorld(layout.rest, BenchStage.MatTop) : TableTopPoint(inspectPoint);
        if (stage != null) stage.BeginJob(item, layout, FootprintHalfMm());
        MakeRoomInTheMiddle(item);

        // The camera, placed for this device BEFORE it takes over, so the blend lands right (the TD's §6.7).
        camRestPosition = inspectCam.transform.position;
        camRestRotation = inspectCam.transform.rotation;
        PlaceCamera(layout);
        inspectCam.Priority = 30;

        faceSign = Vector3.Dot(item.transform.rotation * thinAxis, Vector3.up) >= 0f ? 1f : -1f;
        Vector3 longNow = Vector3.ProjectOnPlane(item.transform.rotation * longAxis, Vector3.up);
        yaw = longNow.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(ViewForwardOnTable(), longNow, Vector3.up) : 0f;
        yaw = Mathf.Round(yaw / 90f) * 90f;      // square to the view when it arrives
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        Pose();

        // The fresh parts wait on the new-part pad from the first time the device is worked on (only the parts of THIS job: a
        // part its fault switched off is not a task).
        foreach (ReplaceablePart part in item.GetComponentsInChildren<ReplaceablePart>()) part.PresentFresh();

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
        EndRub();
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

    // The item goes back where it was and the close-up comes down; the camera goes back to the scene's pose at FOV 40 (the
    // TD's §6.7). The cursor is the view's to set again (CafeViewMode locks it in first person and frees it above).
    private void Release()
    {
        EndHold(false);
        EndGrab();
        EndRub();
        foreach (CircuitPuzzle puzzle in focusedCircuits)
            if (puzzle != null) puzzle.HideForInspection();
        focusedCircuits = System.Array.Empty<CircuitPuzzle>();
        rotateGesture = false;
        settling = false;
        if (flipping != null) { StopCoroutine(flipping); flipping = null; }
        BenchFeel.ResetBudge();
        if (focusedItem != null)
        {
            focusedItem.transform.position = restPosition;
            focusedItem.transform.rotation = restRotation;
        }
        // The device that made room in the middle goes back there (if it is still on the bench).
        if (swappedDevice != null)
        {
            JobBase other = swappedDevice.GetComponent<JobBase>();
            if (other != null && StationInteractable.BenchHolds(other)) swappedDevice.SetPositionAndRotation(swappedPosition, swappedRotation);
            swappedDevice = null;
        }
        if (focusedItem != null && BenchStage.Instance != null) BenchStage.Instance.EndJob(focusedItem);
        focusedItem = null;
        if (inspectCam != null)
        {
            inspectCam.Priority = 0;
            if (camPlaced)
            {
                inspectCam.transform.SetPositionAndRotation(camRestPosition, camRestRotation);
                SetFov(LayoutFov);
                camPlaced = false;
            }
        }
        framed = false;
        SetBenchHover(null);
        ClearTool();
        BenchFeel.Silence();
        hasHover = false;
        pressLive = false;
        consumedUntilUp = false;
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
        Vector2 pointer = GamePointer.Position;
        Vector2 delta = GamePointer.Delta;
        float dt = Time.deltaTime;
        if (consumedUntilUp && !UseHeldRaw) consumedUntilUp = false;

        // UI uses the existing Input System EventSystem. Raw bench input must not click through the retry button or rotate the
        // device underneath it.
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

        // Controller: LB / RB cycle the bench tools; the turn stick turns the device a quarter at a time (sideways) and lifts an
        // edge up to peek under (up or down). R or Y flips it over.
        if (PadInput.Pressed(PadButton.RightShoulder)) CycleTool(1);
        else if (PadInput.Pressed(PadButton.LeftShoulder)) CycleTool(-1);
        StickOnTheMat(dt);
        if (holdTarget == null && grabTarget == null && flipping == null
            && (PadInput.Pressed(PadButton.North) || Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
            flipping = StartCoroutine(Flip());

        HandleZoom(dt);
        RefreshSteps();

        // The stroke's speed, every frame of the close-up (Audio's s: BenchFeel).
        BenchFeel.Sample(delta, dt, padRubbingInPlace, padScrubRate);

        CurrentJobCard = focusedItem.JobCard;
        if (overHud)
        {
            rotateGesture = false;
            SetBenchHover(null);
            hasHover = false;
            HoverName = ""; HoverAction = "";
            if (!UseHeld) EndRub();
            BenchFeel.Tick();
            return;
        }
        Ray ray = cam.ScreenPointToRay(pointer);

        // The surface point (the physics ray): where the tool sits off its targets, the pry's seam, the rotate gesture's space.
        bool hitSomething = false;
        RaycastHit hit = default;
        BenchInteractable tile = null;
        foreach (CircuitPuzzle puzzle in focusedCircuits)
        {
            if (puzzle == null) continue;
            CircuitTile t = puzzle.TileAtScreenPoint(pointer);
            if (t != null) { tile = t; break; }
        }
        if (!overBoard && Physics.Raycast(ray, out hit, benchReach, ~0, QueryTriggerInteraction.Ignore)) hitSomething = true;

        // The picker (§4.6): what a press here acts on. A hold owns its target until let go, so nothing is picked meanwhile.
        hasHover = false;
        candidates.Clear();
        if (holdTarget == null && grabTarget == null && tile == null && !overBoard) Pick(pointer);
        bool padAim = GamePointer.PadCursorActive;
        if (padAim) OfferAimHelp(pointer);

        hand = new BenchHand { ray = ray, hit = hit, hitSomething = hitSomething, tool = currentTool, toolModel = currentToolPickup, camera = cam, deltaTime = dt };

        // ---- the words (UI/UX §5.10 item 7) ----
        BenchInteractable hoverPart = tile != null ? tile : hasHover ? hover.part : null;
        GrimeSpot hoverSpot = tile == null && hasHover ? hover.spot : null;
        ToolPickup hoverTool = tile == null && hasHover ? hover.tool : null;
        HoverName = "";
        HoverAction = "";
        if (holdTarget != null) HoverName = holdTarget.DisplayName;
        else if (grabTarget != null) HoverName = grabTarget.DisplayName;
        else if (rubbing.Count > 0) HoverName = rubbing[0].PartName;
        else if (hoverTool != null) HoverName = hoverTool.displayName;
        else if (hoverSpot != null)
        {
            HoverName = hoverSpot.PartName;
            HoverAction = currentTool == ToolType.Hand ? "" : hoverSpot.Prompt(currentTool);
        }
        else if (hoverPart != null)
        {
            HoverName = hoverPart.DisplayName;
            HoverAction = Words(hoverPart);
        }
        BenchInteractable target = hoverPart != null && hoverPart.CanInteract
            && (hoverPart.RequiredTool == ToolType.Hand || hoverPart.RequiredTool == currentTool) ? hoverPart : null;
        SetBenchHover(holdTarget != null ? holdTarget : grabTarget != null ? grabTarget : target);
        // Over a piece's seat (an alias) the tool stays at the cursor, not on the piece across the mat.
        BenchInteractable toolOn = hasHover && hover.alias ? null : target;

        // ---- the press: left click / RT, or A on a pad (12 Oct) ----
        bool pressed = UsePressed;
        if (pressed) HandleBenchPress(hoverPart, hoverSpot, hoverTool, overBoard);
        // A press is held for its own frame at least: a click shorter than a 15 fps frame still ticks its hold once (a cover in
        // its well goes home on its first tick), and its release is still the grab rule's.
        bool held = UseHeld || pressed;
        bool rubbedThisFrame = false;
        if (held)
        {
            if (holdTarget != null)
            {
                bool finished = holdTarget.HoldTick(hand);
                NoteHoldProgress();
                if (finished) EndHold(true);
            }
            else if (grabTarget != null)
            {
                grabTarget.GrabMove(hand);
            }
            else if (currentToolPickup != null && !currentToolPickup.Returning && IsRubTool(currentTool))
            {
                rubbedThisFrame = Rub(ray, delta, dt);
            }
            else if (currentTool == ToolType.Hand && rotateGesture && !overBoard && flipping == null && !turning)
            {
                // Sideways spins it on the mat; up or down tilts an edge to peek under (it lies back flat when let go).
                yaw += -delta.x * rotateSpeed;
                tilt = Mathf.Clamp(tilt + delta.y * rotateSpeed, -peekTilt, peekTilt);
                tiltVelocity = 0f;
                // The spin it will keep when let go (smoothed over a few frames).
                float instant = -delta.x * rotateSpeed / Mathf.Max(dt, 1e-4f);
                yawVelocity = Mathf.Lerp(yawVelocity, instant, 1f - Mathf.Exp(-14f * dt));
                settling = false;
                if (Mathf.Abs(delta.x) > 0f) settled = false;   // let go, it lands square whatever the spin (Coast)
            }
        }
        else
        {
            if (holdTarget != null) EndHold(false);
            if (grabTarget != null) EndGrab();
            rotateGesture = false;
            Coast(dt);
        }
        if (!rubbedThisFrame) EndRub();
        if (wasHeld && !held && pressLive) Released(pointer);
        if (!held) pressLive = false;
        wasHeld = held;
        lastRay = ray;
        hasLastRay = true;

        // The device budges under the rub (BenchFeel; Pose adds it), and lies where its pose says.
        BenchStage stage = BenchStage.Instance;
        BenchFeel.Budge(cam, tableTop, stage != null ? stage.MatUp : Vector3.up, rubbing.Count > 0, dt);
        if (flipping == null) Pose();

        // The cloth or the brush home by itself once its last spot on this device is clean (§4.4.1).
        if (checkToolHome) ToolHomeAfterLastSpot();
        StuckScrub(held, dt);

        PlaceTool(toolOn, hoverSpot, hitSomething, hit, ray, dt);
        BenchFeel.Tick();
    }

    // ---------- the press, the release, the grab rule (§4.4.1) ----------

    // What a press does, decided by the picker's choice: a tool at rest comes into the hand; a part the tool in hand (or the
    // bare hand) can act on begins its hold (or its press); a press on the tool's own dirt starts a rub (the held branch); a
    // press on nothing with bare hands starts the rotate gesture. (The editor's checks call this by reflection, with the
    // physics ray's resolution: keep its shape.)
    private void HandleBenchPress(BenchInteractable part, GrimeSpot grime, ToolPickup tool, bool overBoard)
    {
        if (Time.timeScale <= 0f || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        Presses++;
        pressLive = true;
        pressAt = Time.unscaledTime;
        pressFrom = GamePointer.Position;
        pressTookTool = false;
        pressActed = false;
        LastPressTarget = tool != null ? tool : grime != null ? grime : (Component)part;
        // Only a drag that starts on empty space rotates the item. Picking a tool, turning a wire, or pressing a part (even one
        // that can't be worked now) owns that press.
        rotateGesture = part == null && grime == null && tool == null && !overBoard;
        if (rotateGesture) { settling = false; yawVelocity = 0f; }
        if (tool != null)
        {
            // In the hand on this frame; the one held before goes home.
            EndHold(false);
            EndRub();
            if (currentToolPickup != null && currentToolPickup != tool) currentToolPickup.SetSelected(false);
            currentToolPickup = tool;
            currentTool = tool.tool;
            tool.SetSelected(true);
            pressTookTool = true;
            pressActed = true;
        }
        else if (grime == null && part != null && part.isActiveAndEnabled && part.CanInteract
            && (currentTool == part.RequiredTool || part.RequiredTool == ToolType.Hand))
        {
            if (part.Holdable) BeginHold(part);
            else if (part.Grabbable) BeginGrab(part);
            else part.Activate();
            pressActed = true;
        }
        else if (grime != null && currentTool == grime.Tool && currentToolPickup != null) pressActed = true;
    }

    // The button let go after a press made in the close-up: the grab rule decides where the tool in the hand goes. A tap, a
    // release over the device or over its own place on the rest keeps it in the hand; a press that worked something keeps it;
    // a press on nothing let go anywhere else (the mat, empty space) sends it home.
    private void Released(Vector2 pointer)
    {
        ToolPickup tool = currentToolPickup;
        if (tool == null || tool.IsStandIn || tool.Returning) return;
        if (GamePointer.PadCursorActive && !pressTookTool) return;   // LB/RB tools stay in the hand (Systems §4.4.1, pad row)
        float scale = Scale;
        bool tap = Time.unscaledTime - pressAt <= TapSeconds && (pointer - pressFrom).magnitude < TapPixels * scale;
        bool keep = tap || OverDevice(pointer) || OverOwnPlace(tool, pointer) || pressActed && !pressTookTool;
        if (!keep) ClearTool();
    }

    // The cursor is over the device (its outline on screen, grown by 32 px).
    private bool OverDevice(Vector2 pointer)
    {
        if (focusedItem == null || cam == null) return false;
        Transform t = focusedItem.transform;
        int n = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? localMin.x : localMax.x, (i & 2) == 0 ? localMin.y : localMax.y, (i & 4) == 0 ? localMin.z : localMax.z);
            if (!Project(t.position + t.rotation * corner, n)) return false;
            n++;
        }
        return HullHit(pointer, n, OverDevicePixels * Scale) <= 1f;
    }

    // The cursor is over the tool's own place on the rest (its outline lying at home, grown as its hit area is).
    private bool OverOwnPlace(ToolPickup tool, Vector2 pointer)
    {
        Pose home = tool.HomePose;
        return BoxHit(tool.RestBox, home.position, home.rotation, pointer, BenchRules.ToolMarginPx * Scale) <= 1f;
    }

    // ---------- the rub (§4.4.3-4.4.5) ----------

    private static bool IsRubTool(ToolType tool) => tool == ToolType.Cloth || tool == ToolType.Brush;

    // One frame of the cloth or the brush held down: each of its own spots under its footprint takes the pass from last frame's
    // contact to this one's. True when it is on at least one of them.
    private readonly List<GrimeSpot> spotsBuffer = new();

    private bool Rub(Ray ray, Vector2 delta, float dt)
    {
        rubbing.Clear();
        bool cameOff = false;
        // Held still on a pad means still for a moment, not for one frame: a stroke that steps (a thumb that pauses a frame at
        // 15 fps, the labs' hand) otherwise orbited and rubbed in place between its steps, and the first pass over grease took
        // 97 % instead of smearing (15 Oct, Bench 3).
        bool padNow = GamePointer.PadCursorActive && delta.sqrMagnitude < .25f;
        padStillFor = padNow ? padStillFor + dt : 0f;
        bool padStill = padNow && padStillFor >= BenchRules.PadStillSeconds;
        if (padStill) orbit += dt * BenchRules.PadOrbitTurns * 2f * Mathf.PI;
        float best = float.PositiveInfinity;
        focusedItem.GetComponentsInChildren(spotsBuffer);
        foreach (GrimeSpot spot in spotsBuffer)
        {
            if (spot == null || !spot.isActiveAndEnabled || spot.IsClean || spot.Tool != currentTool) continue;
            if (spot.CoveredBy != null || !Facing(spot.Normal, spot.Centre)) continue;
            if (!spot.Contact(ray, out Vector3 contact)) continue;
            float r = spot.CleanRadius(currentTool, cam, GamePointer.PadCursorActive ? BenchRules.PadRubReach : 1f);
            if (padStill)
            {
                // Held still on a pad: the footprint orbits the cursor at 0.35 of its clean radius, two turns a second.
                Vector3 n = spot.Normal;
                Vector3 a = Vector3.ProjectOnPlane(cam.transform.right, n).normalized, b = Vector3.Cross(n, a);
                contact += (a * Mathf.Cos(orbit) + b * Mathf.Sin(orbit)) * (BenchRules.PadOrbitShare * r);
            }
            if (!spot.Under(contact, r)) continue;
            // Coming onto the spot with the button held: its stroke starts where the tool was last frame, so the edge it crossed
            // is taken too, however long the frame.
            if (!rubbedLast.Contains(spot) && hasLastRay && spot.Contact(lastRay, out Vector3 from)) spot.StrokeFrom(from);
            if (spot.Pass(contact, r)) cameOff = true;
            if (padStill) spot.RubInPlace(dt);
            rubbing.Add(spot);
            float d = (contact - spot.Centre).sqrMagnitude;
            if (d < best) { best = d; rubContact = contact; rubNormal = spot.Normal; }
        }
        spotsBuffer.Clear();
        // A spot the footprint left this frame: its stroke is over.
        foreach (GrimeSpot spot in rubbedLast) if (spot != null && !rubbing.Contains(spot)) spot.EndStroke();
        rubbedLast.Clear();
        rubbedLast.AddRange(rubbing);
        padRubbingInPlace = padStill && rubbing.Count > 0;
        BenchFeel.Rubbing(currentTool, currentToolPickup != null ? currentToolPickup.transform : null, rubbing.Count > 0, cameOff, dt);
        return rubbing.Count > 0;
    }

    // Off the dirt, or let go: every stroke is over and the rub loop fades.
    private void EndRub()
    {
        foreach (GrimeSpot spot in rubbedLast) if (spot != null) spot.EndStroke();
        bool was = rubbedLast.Count > 0 || rubbing.Count > 0;
        rubbedLast.Clear();
        rubbing.Clear();
        padRubbingInPlace = false;
        padStillFor = 0f;
        if (was || BenchFeel.RubPlaying) BenchFeel.NotRubbing();
    }

    private void OnTargetDone(JobBase job, Component target)
    {
        if (job != null && job == focusedItem) checkToolHome = true;   // 19 Oct: every tool, not only the cloth and the brush
        stepsAt = -10f;   // the steps change with it
    }

    private void OnFlashed(JobBase job, float seconds)
    {
        if (job != null) flashedFor = job;
    }

    // The tool in the hand whose step is done flies home, held or not, and the press is spent until the button is up (§4.4.1).
    // The cloth and the brush did after their last spot; 19 Oct, the reviewers: "at 'Fixed!' the last tool stays where it was
    // dropped" (the driver across the fixed phone's screen). A rub tool goes when none of its spots is left; any other when the
    // next step wants another tool, or the job is done.
    private void ToolHomeAfterLastSpot()
    {
        checkToolHome = false;
        if (currentToolPickup == null || currentToolPickup.Returning || holdTarget != null) return;
        if (IsRubTool(currentTool))
        {
            focusedItem.GetComponentsInChildren(spotsBuffer);
            bool left = false;
            foreach (GrimeSpot spot in spotsBuffer)
                if (spot != null && spot.isActiveAndEnabled && !spot.IsClean && spot.Tool == currentTool) { left = true; break; }
            spotsBuffer.Clear();
            if (left) return;
        }
        else
        {
            RepairChecklist.Build(focusedItem, steps);
            RepairChecklist.Step next = RepairChecklist.Current(steps);
            stepsAt = -10f;
            if (!focusedItem.IsFinished && next != null && next.tool == currentTool) return;
        }
        EndRub();
        if (UseHeldRaw) consumedUntilUp = true;
        pressLive = false;
        ToolPickup tool = currentToolPickup;
        currentToolPickup = null;
        currentTool = ToolType.Hand;
        tool.SetSelected(false, liftFirst: true);
    }

    // T17 (the feel targets): on the first repair, 3 s of scrubbing with nothing coming off fires "what's left" once by itself.
    private void StuckScrub(bool held, float dt)
    {
        if (BenchWork.LastProgressAt != progressSeenAt) { progressSeenAt = BenchWork.LastProgressAt; scrubWithoutProgress = 0f; }
        bool scrubbing = held && currentToolPickup != null && IsRubTool(currentTool) && BenchFeel.StrokeSpeed > 40f;
        if (!scrubbing) return;
        scrubWithoutProgress += dt;
        if (scrubWithoutProgress < StuckScrubSeconds || flashedFor == focusedItem || !FirstRepair) return;
        scrubWithoutProgress = 0f;
        BenchWork.Flash(focusedItem);
        flashedFor = focusedItem;
    }

    /// <summary>The first repair of the game (Day 1, nothing handed back yet): UI/UX's rule for the first-repair card, read the
    /// same way here for T17's automatic "what's left".</summary>
    public static bool FirstRepair
    {
        get
        {
            DayClock clock = DayClock.Instance;
            return clock != null && clock.Day == 1 && clock.Repairs == 0;
        }
    }

    // ---------- the picker (§4.6) ----------

    private struct Candidate
    {
        public Component key;            // what the step's targets name: a screw's Screw, or the part, the spot, the tool itself
        public BenchInteractable part;   // a screw, a hole, a cover, a broken or fresh part, a toggle (or the piece an alias acts on)
        public GrimeSpot spot;
        public ToolPickup tool;
        public int priority;             // BenchRules' four classes, 1 best
        public float distance;           // normalised: 0 its middle, 1 the edge of its hit area
        public bool stepTarget;
        public bool canAct;
        public Vector2 screen;           // its middle on screen (pixels): the aim help's point
        public bool seenChecked, seen;
        public bool alias;               // the fresh part's empty seat, or the cover's open face: a press there acts on the piece
    }

    private readonly List<Candidate> candidates = new();
    private Candidate hover;
    private bool hasHover;
    private readonly List<BenchInteractable> partsBuffer = new();
    private readonly List<BenchInteractable> pieceBuffer = new();
    private readonly HashSet<BenchInteractable> listedParts = new();
    private readonly HashSet<Component> stepTargets = new();
    private readonly List<RepairChecklist.Step> steps = new();
    private RepairChecklist.Step currentStep;
    private float stepsAt = -10f;
    private readonly Dictionary<Component, Bounds> boxes = new();
    // The projected points of the shape being measured, and its hull (no allocation per candidate).
    private readonly float[] px = new float[16], py = new float[16], hx = new float[16], hy = new float[16];

    // The steps as they stand (RepairChecklist, the card's own), every 0.1 s and at once when a target finishes.
    private void RefreshSteps()
    {
        if (Time.unscaledTime - stepsAt < .1f) return;
        stepsAt = Time.unscaledTime;
        RepairChecklist.Build(focusedItem, steps);
        currentStep = RepairChecklist.Current(steps);
        CurrentStepIndex = currentStep != null ? steps.IndexOf(currentStep) : -1;
        stepTargets.Clear();
        if (currentStep != null && currentStep.targets != null)
            foreach (Component c in currentStep.targets) if (c != null) stepTargets.Add(c);
    }

    // Every candidate measured against the cursor; the best hit (if any) is the hover. With the pad's cursor, every eligible one
    // is kept with its middle on screen for the aim help.
    private void Pick(Vector2 pointer)
    {
        candidates.Clear();
        listedParts.Clear();
        focusedItem.GetComponentsInChildren(partsBuffer);
        foreach (BenchInteractable part in partsBuffer) AddPart(part, pointer, true);
        // The pieces off it (LooseParts: the detached ones, then its stock and scrap): screws in their cups, a cover in its
        // well, the broken part in the tray, the fresh part on its pad or over its seat.
        foreach (GameObject piece in focusedItem.LooseParts) AddPieces(piece, pointer);
        partsBuffer.Clear();
        focusedItem.GetComponentsInChildren(spotsBuffer);
        foreach (GrimeSpot spot in spotsBuffer) AddSpot(spot, pointer);
        spotsBuffer.Clear();
        AddTools(pointer);

        // The best hit: by class, then by normalised distance; out of sight only if it is a target of the step to do now.
        int bestIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate c = candidates[i];
            if (!(c.distance <= 1f)) continue;
            if (bestIndex >= 0 && !BenchRules.Beats(c.priority, c.distance, candidates[bestIndex].priority, candidates[bestIndex].distance)) continue;
            if (!c.stepTarget && !SeenNow(ref c)) { candidates[i] = c; continue; }
            candidates[i] = c;
            bestIndex = i;
        }
        if (bestIndex >= 0) { hover = candidates[bestIndex]; hasHover = true; }
    }

    private bool SeenNow(ref Candidate c)
    {
        if (!c.seenChecked)
        {
            c.seenChecked = true;
            Transform thing = c.tool != null ? c.tool.transform : c.spot != null ? c.spot.transform : c.part != null ? c.part.transform : null;
            c.seen = thing == null || c.tool != null || IsSeen(thing);
        }
        return c.seen;
    }

    private void AddPieces(GameObject piece, Vector2 pointer)
    {
        if (piece == null || !piece.activeInHierarchy) return;
        piece.GetComponentsInChildren(pieceBuffer);
        foreach (BenchInteractable part in pieceBuffer) AddPart(part, pointer, false);
        pieceBuffer.Clear();
    }

    private void AddPart(BenchInteractable part, Vector2 pointer, bool onDevice)
    {
        if (part == null || !part.isActiveAndEnabled || part is CircuitTile || !listedParts.Add(part)) return;
        if (part == holdTarget) return;
        BenchStage stage = BenchStage.Instance;
        // A piece flying is nobody's target (it lands where it can be pressed).
        if (stage != null && stage.IsFlying(part)) return;
        bool onTheDevice = onDevice && part.transform.IsChildOf(focusedItem.transform);
        Component key = part;
        if (part is ScrewTarget st) { Screw screw = st.GetComponent<Screw>(); if (screw != null) key = screw; }
        // (A part its cover holds in stays hoverable: its own CanInteract is false and it says why, "Held in by the back cover";
        // a part the cover hides is out of sight, so it isn't hit unless the step to do now is its own.)
        // On the device, its work face must face the camera: a screw on the underside is never hit (the card says flip).
        if (onTheDevice && !Facing(part.WorkNormal, part.WorkPoint)) return;
        bool stepTarget = stepTargets.Contains(key) || stepTargets.Contains(part);
        bool canAct = part.CanInteract && (part.RequiredTool == ToolType.Hand || part.RequiredTool == currentTool);
        var c = new Candidate { key = key, part = part, stepTarget = stepTarget, canAct = canAct, priority = BenchRules.PriorityClass(stepTarget, canAct) };
        float scale = Scale;
        switch (part)
        {
            case ScrewTarget target:
            {
                Screw screw = target.GetComponent<Screw>();
                Vector3 head = screw != null ? screw.HeadPoint : part.WorkPoint;
                float headRadius = screw != null ? screw.HeadRadius : .002f;
                if (!Circle(head, headRadius, pointer, scale, out c.distance, out c.screen)) return;
                break;
            }
            case ScrewSocket socket:
            {
                float headRadius = socket.Screw != null ? socket.Screw.HeadRadius * ScrewSocket.DecalPerHead : .002f;
                if (!Circle(part.WorkPoint, headRadius, pointer, scale, out c.distance, out c.screen)) return;
                break;
            }
            default:
            {
                float margin = (part is RemovablePart ? BenchRules.CoverMarginPx : BenchRules.PartMarginPx) * scale;
                c.distance = BoxHit(BoxOf(part), part.transform.position, part.transform.rotation, pointer, margin, out c.screen);
                if (float.IsNaN(c.distance)) return;
                break;
            }
        }
        candidates.Add(c);

        // The aliases: a press on the empty seat acts on the fresh part waiting on its pad; in the Cover on step a press on the
        // device's open face acts on the cover in its well (§4.2-4.3).
        if (part is LoosePart fresh && fresh.OnPad && fresh.Part != null && fresh.Part.SeatEmpty && fresh.Part.SeatFacesCamera(cam))
        {
            fresh.Part.SeatPose(out Transform frame, out Vector3 local, out Quaternion rotation);
            Vector3 at = frame != null ? frame.TransformPoint(local) : local;
            Quaternion turn = frame != null ? frame.rotation * rotation : rotation;
            Alias(c, BoxOf(fresh), at, turn, pointer, BenchRules.PartMarginPx * scale);
        }
        else if (part is RemovablePart cover && cover.InWell && stepTarget && cover.SeatFacesCamera(cam))
            Alias(c, BoxOf(cover), cover.HomePosition, cover.HomeRotation, pointer, BenchRules.CoverMarginPx * scale);
    }

    private void Alias(Candidate of, Bounds box, Vector3 position, Quaternion rotation, Vector2 pointer, float margin)
    {
        Candidate c = of;
        c.seenChecked = true;   // its seat faces the camera (checked by the caller); nothing on the device hides its own seat
        c.seen = true;
        c.alias = true;
        c.distance = BoxHit(box, position, rotation, pointer, margin, out c.screen);
        if (float.IsNaN(c.distance)) return;
        candidates.Add(c);
    }

    private void AddSpot(GrimeSpot spot, Vector2 pointer)
    {
        if (spot == null || !spot.isActiveAndEnabled || spot.IsClean) return;
        // Under a seated or popped cover (§4.6, new for spots), or facing away: never hit.
        if (spot.CoveredBy != null || !Facing(spot.Normal, spot.Centre)) return;
        bool stepTarget = stepTargets.Contains(spot);
        bool canAct = currentTool == spot.Tool;
        var c = new Candidate { key = spot, spot = spot, stepTarget = stepTarget, canAct = canAct, priority = BenchRules.PriorityClass(stepTarget, canAct) };
        // Its patch on screen (a disc on the part: eight points round its edge) + 16 px.
        Transform t = spot.transform;
        Vector3 centre = spot.Centre, n = spot.Normal;
        Vector3 a = Vector3.ProjectOnPlane(t.right, n).normalized * (Mathf.Abs(t.lossyScale.x) * .5f);
        Vector3 b = Vector3.ProjectOnPlane(t.forward, n).normalized * (Mathf.Abs(t.lossyScale.z) * .5f);
        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * .25f;
            if (!Project(centre + a * Mathf.Cos(angle) + b * Mathf.Sin(angle), count)) return;
            count++;
        }
        if (!OnScreen(centre, out c.screen)) return;
        c.distance = HullHit(pointer, count, BenchRules.SpotMarginPx * Scale);
        candidates.Add(c);
    }

    // The tools at rest (+16 px round each): the step's own tool first among equals.
    private void AddTools(Vector2 pointer)
    {
        if (railTools == null) railTools = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude);
        foreach (ToolPickup tool in railTools)
        {
            if (tool == null || !tool.isActiveAndEnabled || tool.InHand) continue;
            bool wanted = currentStep != null && currentStep.tool == tool.tool;
            var c = new Candidate { key = tool, tool = tool, canAct = true, priority = wanted ? 1 : 2, seenChecked = true, seen = true };
            c.distance = BoxHit(tool.RestBox, tool.transform.position, tool.transform.rotation, pointer, BenchRules.ToolMarginPx * Scale, out c.screen);
            if (float.IsNaN(c.distance)) continue;
            candidates.Add(c);
        }
    }

    // A screw or a hole: a circle round its middle, max(28 px, its head's projected radius + 10 px). NaN-free: false when it is
    // behind the camera.
    private bool Circle(Vector3 middle, float headRadius, Vector2 pointer, float scale, out float distance, out Vector2 screen)
    {
        distance = float.PositiveInfinity;
        if (!OnScreen(middle, out screen)) return false;
        Vector3 edge = cam.WorldToScreenPoint(middle + cam.transform.right * headRadius);
        float headPx = ((Vector2)edge - screen).magnitude / Mathf.Max(.01f, scale);
        float radius = BenchRules.ScrewHitRadius(headPx) * scale;
        distance = BenchRules.CircleDistance(pointer.x, pointer.y, screen.x, screen.y, radius);
        return true;
    }

    // A box (a piece's own, in its frame) at a pose, projected: the normalised distance to its outline grown by the margin; NaN
    // when any corner is behind the camera. Its middle on screen comes back for the aim help.
    private float BoxHit(Bounds box, Vector3 position, Quaternion rotation, Vector2 pointer, float margin, out Vector2 screen)
    {
        screen = default;
        if (!OnScreen(position + rotation * box.center, out screen)) return float.NaN;
        return BoxHit(box, position, rotation, pointer, margin);
    }

    private float BoxHit(Bounds box, Vector3 position, Quaternion rotation, Vector2 pointer, float margin)
    {
        if (cam == null) return float.NaN;
        Vector3 min = box.min, max = box.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            if (!Project(position + rotation * corner, i)) return float.NaN;
        }
        return HullHit(pointer, 8, margin);
    }

    // Projects a world point into the scratch arrays at index i; false when it is behind the camera.
    private bool Project(Vector3 world, int i)
    {
        Vector3 s = cam.WorldToScreenPoint(world);
        if (s.z <= cam.nearClipPlane * .5f) return false;
        px[i] = s.x;
        py[i] = s.y;
        return true;
    }

    // The normalised distance to the hull of the first n projected points, grown by the margin. A cursor well outside the
    // points' box is a miss without building the hull.
    private float HullHit(Vector2 pointer, int n, float margin)
    {
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < n; i++) { minX = Mathf.Min(minX, px[i]); maxX = Mathf.Max(maxX, px[i]); minY = Mathf.Min(minY, py[i]); maxY = Mathf.Max(maxY, py[i]); }
        if (pointer.x < minX - margin || pointer.x > maxX + margin || pointer.y < minY - margin || pointer.y > maxY + margin) return 2f;
        int corners = BenchRules.ConvexHull(px, py, n, hx, hy);
        return BenchRules.HullDistance(pointer.x, pointer.y, hx, hy, corners, margin);
    }

    // A piece's own box (its frame, metres, scale kept in), measured once per close-up.
    private Bounds BoxOf(Component piece)
    {
        if (boxes.TryGetValue(piece, out Bounds b)) return b;
        b = BenchStage.Instance != null ? BenchStage.Instance.LocalBox(piece) : BenchStage.Measure(piece.transform);
        boxes[piece] = b;
        return b;
    }

    // A work face faces the camera: its normal within 100° of the line to the camera (§4.6).
    private bool Facing(Vector3 normal, Vector3 at)
    {
        if (cam == null || normal.sqrMagnitude < 1e-8f) return true;
        return Vector3.Dot(normal.normalized, (cam.transform.position - at).normalized) > BenchRules.FacingDot;
    }

    // The hover's words for a part: its own "why not" when it can't be worked; the tool it needs when the wrong one is in the
    // hand (bare hands: the name alone, UI/UX's S1 count); a cover in its well over unfinished work says the click won't close
    // it; otherwise nothing but its name.
    private string Words(BenchInteractable part)
    {
        if (part == null) return "";
        if (!part.CanInteract) return WhyNot(part);
        if (part.RequiredTool != ToolType.Hand && part.RequiredTool != currentTool)
            return currentTool == ToolType.Hand ? "" : Needs(part.RequiredTool);
        if (part is RemovablePart cover && cover.InWell && cover.WorkBeneathToDo) return cover.Prompt;
        return "";
    }

    /// <summary>The words for a screw that can't go back in yet (PLACEHOLDER COPY).</summary>
    public const string CoverFirst = "The cover goes back on first";

    private static string WhyNot(BenchInteractable part)
    {
        switch (part)
        {
            case ScrewTarget target:
            {
                Screw screw = target.GetComponent<Screw>();
                return screw != null && screw.IsOut && !screw.IsBusy && !target.PlateSeated ? CoverFirst : "";
            }
            case ScrewSocket socket:
                return socket.Screw != null && socket.Screw.IsOut && !socket.Screw.IsBusy ? CoverFirst : "";
        }
        string words = part.Prompt ?? "";
        // A reason only: never "Hold to ..." or "Click to ..." (UI/UX §5.10 item 7).
        return words.StartsWith("Hold") || words.StartsWith("Click") ? "" : words;
    }

    private static string Needs(ToolType tool) => "Needs the " + RepairChecklist.ToolName(tool).ToLowerInvariant();

    /// <summary>
    /// A thing is in the close-up's sight: nothing solid between the camera and the middle of what is drawn of it but the thing
    /// itself (the tool in the hand doesn't count: it rides over the part being worked). UI/UX's glow draws a target that isn't
    /// seen dashed (contracts §6).
    /// </summary>
    public bool IsSeen(Transform thing)
    {
        if (thing == null) return false;
        if (cam == null) cam = Camera.main;
        if (cam == null) return true;
        Renderer r = thing.GetComponentInChildren<Renderer>();
        return Seen(thing, r != null ? r.bounds.center : BoundsCentre(thing));
    }

    // ---------- the device on the mat: its pose, the spin's inertia and the settle, the peek, the flip ----------

    // A point on the mat's top, under the given one, kept well inside the mat (a scene without the forgiving stage). Without a
    // stage, the inspect point, as before.
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
            p.x = Mathf.Clamp(p.x, matBounds.min.x + .09f, matBounds.max.x - .09f);
            p.z = Mathf.Clamp(p.z, matBounds.min.z + .09f, matBounds.max.z - .09f);
            p.y = matBounds.max.y;
        }
        return p;
    }

    // A device lying where this one lies (another job, set down there) moves to this one's place on the bench for the work,
    // and goes back on Release. Devices lie on the same mat, so each keeps its own height on it.
    private void MakeRoomInTheMiddle(JobBase item)
    {
        swappedDevice = null;
        Vector3 middle = Flat(tableTop);
        if ((Flat(item.transform.position) - middle).sqrMagnitude < .04f * .04f) return;   // it lies there already
        JobBase other = null;
        float best = .09f * .09f;
        foreach (JobBase job in FindObjectsByType<JobBase>())
        {
            if (job == null || job == item || !StationInteractable.BenchHolds(job)) continue;
            float d = (Flat(job.transform.position) - middle).sqrMagnitude;
            if (d < best) { best = d; other = job; }
        }
        if (other == null) return;
        swappedDevice = other.transform;
        swappedPosition = other.transform.position;
        swappedRotation = other.transform.rotation;
        Vector3 to = restPosition;
        to.y = swappedPosition.y;
        other.transform.position = to;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>A device moved out of the middle of the mat for this one's work (labs).</summary>
    public Transform MadeRoomFor => swappedDevice;

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

    // The close-up's point of view: the inspection camera's transform (its direction never changes; the zoom is the lens).
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

    // The device set down from its pose: turned as the pose says, its bounds' middle over the table point, its lowest corner on
    // the mat (an edge tilted up lifts it; a flip lifts it clear), plus the rub's budge (BenchFeel: a few pixels along the
    // stroke, never a turn).
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
        t.position = new Vector3(tableTop.x - centre.x, tableTop.y - lowest + extraLift, tableTop.z - centre.z) + BenchFeel.BudgeOffset;
    }

    /// <summary>Turns the device over onto its other face, as Y or R does (the labs' hand). False while it can't (mid-flip).</summary>
    public bool FlipOver()
    {
        if (focusedItem == null || flipping != null) return false;
        flipping = StartCoroutine(Flip());
        return true;
    }

    /// <summary>The device is still turning over (FlipOver, Y, R).</summary>
    public bool Flipping => flipping != null;

    /// <summary>Lays the device flat and square at a yaw (the labs' hand; the device's own settle does this by itself).</summary>
    public void LayDown(float aboutUp = 0f)
    {
        yaw = aboutUp;
        yawVelocity = tiltVelocity = 0f;
        tilt = 0f;
        settling = false;
        settled = true;
        turning = false;
        Pose();
    }

    /// <summary>
    /// Turns the device face up when it lies on its face and nothing is held: the ending's moment (Juice). True when it began
    /// turning over; false when it already shows its face, can't be told, or a hold or a grab or a flip is under way.
    /// </summary>
    public bool ShowFace()
    {
        if (focusedItem == null || FaceShowing || flipping != null || holdTarget != null || grabTarget != null) return false;
        flipping = StartCoroutine(Flip());
        return true;
    }

    // ---------- the stick on the mat (12 Oct): a quarter at a time, a peek only when clearly asked ----------

    // The stick that turns the device: the left one, or the right one with the sticks swapped (the hand takes the other:
    // PadCursor). Read raw past the pad's deadzone, not curved: the push is read as a direction, not a rate.
    private static Vector2 TurnStick => GameSettings.SwapBenchSticks ? PadInput.RightStick : PadInput.LeftStick;

    private void StickOnTheMat(float dt)
    {
        Vector2 stick = TurnStick;
        // A thumb still on the stick from the walk up to the bench does nothing until it has let go once.
        if (!stickArmed) { if (stick.sqrMagnitude < .09f) stickArmed = true; else stick = Vector2.zero; }   // a light stick arms it too (QA, 13 Oct)
        bool free = holdTarget == null && grabTarget == null && flipping == null;
        // One of three: sideways (a quarter turn), clearly up or down (a peek), or neither.
        int side = free && Mathf.Abs(stick.x) >= TurnPush && Mathf.Abs(stick.x) > SideRatio * Mathf.Abs(stick.y) ? (stick.x > 0f ? 1 : -1) : 0;
        bool peek = free && Mathf.Abs(stick.y) > PeekPush && Mathf.Abs(stick.y) > SideRatio * Mathf.Abs(stick.x);

        // A push sideways: one quarter, now. Held, another every repeat (a calm pace, each landing before the next begins).
        if (side != 0 && side != stickSide) { QuarterTurn(side); repeatIn = quarterRepeatSeconds; }
        else if (side != 0)
        {
            repeatIn -= dt;
            if (repeatIn <= 0f) { QuarterTurn(side); repeatIn += quarterRepeatSeconds; }
        }
        stickSide = side;

        // The peek: an edge lifts while the push is clearly up or down, and lies back flat (Coast's spring) once it isn't.
        if (peek)
        {
            tilt = Mathf.MoveTowards(tilt, Mathf.Sign(stick.y) * peekTilt, peekTilt * 4f * dt);
            tiltVelocity = 0f;
            settling = false;
        }

        // The turn under way eases to its quarter and lands with a click. It waits while the device is turned over (Y).
        if (turning && flipping == null)
        {
            turnT += dt / Mathf.Max(.05f, quarterTurnSeconds);
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(turnT));
            yaw = turnFrom + (turnTarget - turnFrom) * c;
            if (turnT >= 1f)
            {
                turning = false;
                yaw = Mathf.Repeat(turnTarget + 180f, 360f) - 180f;
                settled = true;
                Sfx.Play("device.snap", focusedItem.transform.position, .7f);
            }
        }
        stickTurning = turning || peek;
    }

    // The next quarter that way from the quarter the device is on, or is turning to: a device the mouse left at an odd angle
    // lands square. Pushing right turns it the way a drag to the right does (yaw falls).
    private void QuarterTurn(int side)
    {
        float from = turning ? turnTarget : yaw;
        float q = from / 90f;
        int k = side > 0 ? Mathf.CeilToInt(q - .01f) - 1 : Mathf.FloorToInt(q + .01f) + 1;
        turnFrom = yaw;
        turnTarget = k * 90f;
        turnT = 0f;
        turning = true;
        yawVelocity = 0f;
        settling = false;
        rotateGesture = false;
        QuarterTurns++;
    }

    // Let go: the spin coasts and dies, and the device settles square to the view (within faceSnapWithin of a quarter: 45,
    // always); a lifted edge lies back flat.
    private void Coast(float dt)
    {
        if (focusedItem == null || flipping != null || stickTurning) return;   // the stick's own turn is not to be doubled (7 Oct)
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
        else if (!settled) BeginSettle();
        // The peek lies back: a spring, critically damped, solved exactly for the frame (a stepped spring runs away at 15 fps).
        if (Mathf.Abs(tilt) > .01f || Mathf.Abs(tiltVelocity) > .01f)
        {
            float x = tilt, v = tiltVelocity;
            BenchRules.Spring(ref x, ref v, 14f, dt);
            tilt = x;
            tiltVelocity = v;
            if (Mathf.Abs(tilt) < .02f && Mathf.Abs(tiltVelocity) < .5f) { tilt = 0f; tiltVelocity = 0f; }
        }
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
    }

    // The nearest quarter turn; within reach (45°: always), the device goes there.
    private void BeginSettle()
    {
        settling = false;
        settled = false;
        if (focusedItem == null || faceSnapWithin <= 0f) { settled = true; return; }
        float nearest = Mathf.Round(yaw / 90f) * 90f;
        if (Mathf.Abs(Mathf.DeltaAngle(yaw, nearest)) <= faceSnapWithin) { settleYaw = nearest; settling = true; }
        else settled = true;
    }

    // Over it goes, along its length, lifted clear of the mat on the way, onto its other face.
    private IEnumerator Flip()
    {
        if (focusedItem == null) yield break;
        settling = false;
        yawVelocity = 0f;
        tiltVelocity = 0f;
        BenchFeel.ResetBudge();
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

    // The device's slab: its thinnest and longest local axes, and its renderers' bounds in its own frame (turned with it, but in
    // metres: its scale is kept in, so the corners can be turned by a rotation alone).
    private void MeasureSlab(Transform root)
    {
        boundsAt = Time.time;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        Quaternion unturn = Quaternion.Inverse(root.rotation);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            // Its own solid parts only (DeviceBounds, 12 Oct): not its job tag, sprites, lines, particles, nothing without a mesh.
            if (!DeviceBounds.Counts(r) || r.GetComponent<BenchOutlineHull>() != null) continue;
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

    // The device's footprint lying square (its long side up the view, as BeginInspection lays it), half sizes in mm: the slot
    // rule keeps every place clear of it and of it turned a quarter (BenchStage).
    private Vector2 FootprintHalfMm()
    {
        Vector3 size = localMax - localMin;
        float along = Mathf.Abs(Vector3.Dot(size, longAxis));
        float thin = Mathf.Abs(Vector3.Dot(size, thinAxis));
        float across = Mathf.Abs(size.x) + Mathf.Abs(size.y) + Mathf.Abs(size.z) - along - thin;
        return new Vector2(across * 500f, along * 500f);
    }

    // ---------- the camera and its lens zoom (the TD's §5, §6.7) ----------

    // The close-up placed from the device's layout: aim (mat mm, on the mat's plane), looking up the mat at 58° down, at its
    // distance, FOV 40 (contracts §1; the scale builder's Bench 9 assumes exactly this). With no framing yet the scene's pose
    // is kept, at FOV 40.
    private void PlaceCamera(BenchLayout layout)
    {
        BenchStage stage = BenchStage.Instance;
        framed = layout != null && layout.HasFraming && stage != null && inspectCam != null;
        bool close = layout != null && layout.fovClose > 0f;
        magnification = close ? Mathf.Tan(20f * Mathf.Deg2Rad) / Mathf.Tan(Mathf.Clamp(layout.fovClose, 1f, 40f) * .5f * Mathf.Deg2Rad) : lensZoomFallback;
        if (framed)
        {
            camLook = stage.MatRotation * Quaternion.Euler(Pitch, 0f, 0f);
            camDistance = layout.cameraDistance;
            layoutAim = stage.MatToWorld(layout.cameraAim, 0f);
            closeAimWorld = stage.MatToWorld(close ? layout.closeAim : layout.rest, 0f);
            inspectCam.transform.SetPositionAndRotation(layoutAim - camLook * Vector3.forward * camDistance, camLook);
        }
        SetFov(LayoutFov);
        camPlaced = inspectCam != null;
    }

    private void SetFov(float fov)
    {
        if (inspectCam == null) return;
        LensSettings lens = inspectCam.Lens;
        lens.FieldOfView = fov;
        inspectCam.Lens = lens;
    }

    // The lens zoom (§5.3): z 0..1, a wheel notch ±0.25 (a trackpad's flurry counts a notch at most every 0.08 s), the D-pad
    // up / down 0.8 a second, eased at 12 a second. The magnification M^z narrows the FOV from 40; the aim slides from the layout
    // aim to the close aim; the camera keeps its distance.
    private void HandleZoom(float dt)
    {
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(wheel) > .01f && Time.unscaledTime - lastNotchAt >= .08f)
        {
            zoom = Mathf.Clamp01(zoom + Mathf.Sign(wheel) * .25f);
            lastNotchAt = Time.unscaledTime;
        }
        if (PadInput.Held(PadButton.DpadUp)) zoom = Mathf.Clamp01(zoom + dt * .8f);
        if (PadInput.Held(PadButton.DpadDown)) zoom = Mathf.Clamp01(zoom - dt * .8f);
        if (zoomShown < -1.5f) zoomShown = zoom;
        if (Mathf.Abs(zoom - zoomShown) < .0005f) return;
        zoomShown = Mathf.Lerp(zoomShown, zoom, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(zoom - zoomShown) < .0005f) zoomShown = zoom;
        float m = Mathf.Pow(Mathf.Max(1f, magnification), zoomShown);
        SetFov(2f * Mathf.Atan(Mathf.Tan(20f * Mathf.Deg2Rad) / m) * Mathf.Rad2Deg);
        if (framed && inspectCam != null)
        {
            Vector3 aim = Vector3.Lerp(layoutAim, closeAimWorld, zoomShown);
            inspectCam.transform.SetPositionAndRotation(aim - camLook * Vector3.forward * camDistance, camLook);
        }
    }

    // ---------- holds and grabs ----------

    private void BeginHold(BenchInteractable part)
    {
        holdTarget = part;
        holdProgressSeen = part.HoldProgress;
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

    // A hold's progress rising is work moving on (UI/UX's stuck timer: BenchWork, throttled).
    private void NoteHoldProgress()
    {
        if (holdTarget == null) return;
        float p = holdTarget.HoldProgress;
        if (p > holdProgressSeen + .001f && focusedItem != null) BenchWork.NoteProgress(focusedItem);
        holdProgressSeen = p;
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

    // ---------- the tool in the hand ----------

    // Where the tool is this frame: on the part a hold works (the pry in its seam); pressed on the dirt it rubs; on the part it
    // can work under the cursor (the pry waiting in the seam nearest the cursor); else its tip exactly at the cursor's surface
    // point (over its own dirt, on the spot's plane), hovering a tenth of its contact's width off it; over nothing, by the
    // cursor 35 cm out.
    private void PlaceTool(BenchInteractable target, GrimeSpot grime, bool hitSomething, RaycastHit hit, Ray ray, float dt)
    {
        if (currentToolPickup == null) return;
        ToolPickup tool = currentToolPickup;
        if (tool.Returning) return;
        if (holdTarget != null)
        {
            // The pry levers in the seam it went in at (playtest 4); every other hold puts the tool on the part's work point.
            if (!(currentTool == ToolType.Pry && holdTarget is RemovablePart prying && AtTheSeam(tool, prying, hitSomething, hit, true, dt)))
                tool.HoldAt(holdTarget.WorkPoint, holdTarget.WorkNormal, cam, true, false, dt);
            float direction = holdTarget is ScrewSocket || holdTarget is ScrewTarget st && st.GetComponent<Screw>().IsOut ? -1f : 1f;
            ToolPickup.Motion motion = currentTool switch
            {
                ToolType.Screwdriver => ToolPickup.Motion.Turn,
                ToolType.Tweezers => ToolPickup.Motion.Pinch,
                ToolType.Pry => ToolPickup.Motion.Lever,
                _ => ToolPickup.Motion.None
            };
            tool.Animate(motion, holdTarget.HoldProgress, direction, dt);
            return;
        }
        if (grabTarget != null)
        {
            tool.HoldAt(BenchInteractable.BoundsCentre(grabTarget.transform), (cam.transform.position - grabTarget.transform.position).normalized, cam, true, false, dt);
            tool.Animate(ToolPickup.Motion.Pinch, 1f, 1f, dt);
            return;
        }
        tool.Rest();
        // Pressed on its own dirt: the tip on the spot's plane at the cursor (or its orbit), pressing.
        if (rubbing.Count > 0) { tool.HoldAt(rubContact, rubNormal, cam, true, true, dt); return; }
        if (target != null && !IsRubTool(currentTool))
        {
            // Over a cover it can pry (or one it has just popped), the pry waits in the seam nearest the cursor (playtest 4).
            if (currentTool == ToolType.Pry && target is RemovablePart cover && AtTheSeam(tool, cover, hitSomething, hit, false, dt)) return;
            tool.HoldAt(target.WorkPoint, target.WorkNormal, cam, false, false, dt);
            return;
        }
        // While a pried cover pops (a moment it is nobody's target), the pry stays in the seam under its edge (playtest 4).
        if (currentTool == ToolType.Pry && hitSomething && hit.collider.GetComponentInParent<RemovablePart>() is RemovablePart popping && popping.Popping
            && AtTheSeam(tool, popping, hitSomething, hit, true, dt)) return;
        // The cloth or the brush over its own dirt: on the spot's plane, exactly under the cursor.
        if (grime != null && IsRubTool(currentTool) && grime.Tool == currentTool && grime.Contact(ray, out Vector3 onSpot))
        {
            tool.HoldAt(onSpot, grime.Normal, cam, false, false, dt);
            return;
        }
        if (hitSomething) { tool.HoldAt(hit.point, hit.normal, cam, false, false, dt); return; }
        // Over nothing: by the cursor, 35 cm out, tip toward the item.
        Vector3 point = cam.transform.position + ray.direction * .35f;   // 35 cm from the eye (the ray's own origin is out on the near plane)
        tool.HoldAt(point, -ray.direction, cam, false, false, dt);
    }

    // The pry tool in a cover's seam, the way the cover says (RemovablePart.PryPose): the seam nearest the cursor, or the one
    // it went in at. False when the cover isn't the pry's (off the device): the tool follows as any other.
    private bool AtTheSeam(ToolPickup tool, RemovablePart cover, bool hitSomething, RaycastHit hit, bool working, float dt)
    {
        Vector3 near = hitSomething ? hit.point : BenchInteractable.BoundsCentre(cover.transform);
        if (!cover.PryPose(near, out Vector3 tip, out Vector3 handle, out Vector3 flat, out Vector3 normal)) return false;
        tool.FollowPose(tip, handle, flat, normal, cam, working, dt);
        return true;
    }

    // ---------- aim help on a pad (playtest 3, §2.3; the forgiving bench, §4.6) ----------

    // The points the pad's cursor settles on are the picker's eligible targets (and the tools at rest); over a target of the
    // step to do now the cursor slows (friction); D-pad left / right glides to the next part in the order the work goes, the
    // step's own targets first, even one something hides.
    private readonly List<Vector2> snapPoints = new();
    /// <summary>The points the pad's cursor may settle on this frame (the picker's eligible targets and the tools; labs).</summary>
    public IReadOnlyList<Vector2> AimPoints => snapPoints;
    private ToolPickup[] railTools;   // the bench's tools, found once an item is taken up (they don't come and go)
    private readonly List<(int order, int sub, Vector2 screen)> workPoints = new();
    private readonly RaycastHit[] sight = new RaycastHit[8];

    private void OfferAimHelp(Vector2 pointer)
    {
        snapPoints.Clear();
        workPoints.Clear();
        // The picker ran this frame unless a hold owns the cursor (no help is wanted mid-hold) or the circuit's board has it.
        if (holdTarget != null || grabTarget != null) return;
        // Nor mid-scrub (15 Oct, Bench 8): the cloth or the brush held down with the stick waggling, every turn of the stroke
        // let the stick through its dead zone and the cursor glided back onto the spot's middle, and over it the friction
        // halved the stroke: a 6 px scrub that never reached the edges. Aim help is for getting there, not for the rub.
        if (IsRubTool(currentTool) && UseHeld) return;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate c = candidates[i];
            // Eligible (§4.6): a part that can be worked now (whatever the tool in hand), a spot, a tool, the step's own.
            bool usable = c.tool != null || c.stepTarget || c.spot != null || c.part != null && c.part.CanInteract;
            if (!usable) continue;
            bool seen = c.stepTarget || SeenNow(ref c);
            candidates[i] = c;
            if (!seen) continue;
            if (!OnScreen(c.screen)) continue;
            snapPoints.Add(c.screen);
            if (c.tool != null) continue;
            // The step's own targets first (hidden ones too), then the rest in the work's order, the step's tool's spot first.
            int order = c.stepTarget ? -1 : c.part != null ? WorkOrder(c.part) : 2;
            int sub = c.spot != null && currentStep != null && c.spot.Tool != currentStep.tool ? 1 : 0;
            workPoints.Add((order, sub, c.screen));
        }
        if (AimAssist.Enabled)
        {
            PadCursor.OfferSnapPoints(snapPoints);
            if (hasHover && hover.stepTarget && hover.distance <= 1f) PadCursor.Friction(PadFriction);
        }

        int direction = PadInput.Pressed(PadButton.DpadRight) ? 1 : PadInput.Pressed(PadButton.DpadLeft) ? -1 : 0;
        if (direction == 0 || workPoints.Count == 0) return;
        workPoints.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.sub != b.sub ? a.sub.CompareTo(b.sub) : a.screen.x.CompareTo(b.screen.x));
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

    // A thing is in the camera's sight: nothing solid between the camera and its middle but the thing itself. The tool in the
    // hand doesn't count (it rides over the part being worked). A screw on the underside of a phone lying face up is not in
    // sight (12 Oct: the D-pad sent the cursor to it, under the glass).
    private bool Seen(Transform thing, Vector3 centre)
    {
        Vector3 from = cam.transform.position;
        Vector3 to = centre - from;
        float length = to.magnitude;
        if (length < 1e-4f) return true;
        int n = Physics.RaycastNonAlloc(from, to / length, sight, length + .02f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Transform first = null;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = sight[i];
            if (h.collider == null || h.collider.GetComponentInParent<ToolPickup>() != null) continue;
            if (h.distance > length - .004f) continue;   // within 4 mm of its middle: its own bed (a cup's or a well's floor in the mat's box)
            if (h.distance < nearest) { nearest = h.distance; first = h.collider.transform; }
        }
        return first == null || first.IsChildOf(thing);
    }

    /// <summary>A part's place in the order the work goes (0 first), the checklist's: screws out, the cover off, the grime
    /// (2), the broken part out and the fresh one in, the circuit, the cover back on, the screws back in; a loose piece that
    /// is nobody's step (the fresh part while the broken one is still in, scrap in the tray) last.</summary>
    public static int WorkOrder(BenchInteractable part)
    {
        if (part is ScrewTarget)
        {
            Screw screw = part.GetComponent<Screw>();
            return screw != null && screw.IsOut ? 6 : 0;
        }
        if (part is ScrewSocket) return 6;
        if (part is RemovablePart cover) return cover.IsRemoved ? 5 : 1;
        if (part is ReplaceablePart broken) return broken.Loose ? 8 : 3;
        if (part is LoosePart fresh) return fresh.SeatEmpty ? 3 : 8;
        if (part is CircuitTile) return 4;
        return 7;
    }

    private bool OnScreen(Vector3 world, out Vector2 screen)
    {
        Vector3 p = cam.WorldToScreenPoint(world);
        screen = p;
        return p.z > cam.nearClipPlane && p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
    }

    private static bool OnScreen(Vector2 screen) => screen.x >= 0f && screen.y >= 0f && screen.x <= Screen.width && screen.y <= Screen.height;

    private static Vector3 BoundsCentre(Transform t) => BenchInteractable.BoundsCentre(t);

    // Controller tool selection: bare hands, then each bench tool in turn (sticky: it stays in the hand until another is
    // picked, B, or its work is done).
    private void CycleTool(int direction)
    {
        var order = new List<ToolPickup> { null };
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
        EndRub();
        if (next == null) { ClearTool(); return; }
        if (currentToolPickup != null) currentToolPickup.SetSelected(false);
        currentToolPickup = next;
        currentTool = next.tool;
        next.SetSelected(true);
    }

    public string CurrentToolName => currentTool == ToolType.Hand || currentToolPickup == null
        ? "Hands" : currentToolPickup.displayName;

    // What a collider the physics ray hit belongs to (the editor's checks call this by reflection: keep its shape). A model's
    // visible blade, screw head or tool handle can own the hit collider while its behaviour lives on the containing part.
    private static void ResolveBenchHit(Collider collider, out BenchInteractable part,
        out GrimeSpot grime, out ToolPickup tool)
    {
        part = collider != null ? collider.GetComponentInParent<BenchInteractable>() : null;
        grime = collider != null ? collider.GetComponentInParent<GrimeSpot>() : null;
        tool = collider != null ? collider.GetComponentInParent<ToolPickup>() : null;
        if (part != null && !part.enabled) part = null;
    }

    // The tool goes home (B, another tool, the close-up's end, a release on nothing): its flight back to the rest.
    private void ClearTool()
    {
        EndHold(false);
        EndRub();
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
