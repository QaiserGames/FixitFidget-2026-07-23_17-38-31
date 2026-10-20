using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

// One optional walking-camera owner. Station, inspection and conversation
// cameras keep their existing higher priorities and their own look controls.
//
// By day the overhead camera circles a fixed point in the café. On a night walk
// (NightWalk calls FollowAce) it follows Ace instead, trailing a fifth of a
// second behind so the capsule's instant starts and stops don't jolt the view.
// It keeps the day's own tilt and zoom (Follow With Day Framing): the second
// playtest (29 Sept) found the closer, steeper night view felt like a different
// camera. Switched off, the night's own limits below apply (the 28 Sept framing).
// The café's cut-away walls then only give way when they hide Ace.
// FollowAce(false) puts the day's view back exactly as it was.
//
// Inside a house at night (the break-ins; GraceHouse calls EnterHouseView), the
// camera turns to look in from the street, like a doll's house with its front
// open, and zooms in closer than the café allows (House Distance Min/Max). The
// player can still orbit and zoom. ExitHouseView turns it back to the angle it
// had outside.
//
// THE NIGHT ZOOM (14 Oct 2026, decision 10; claude cinematics spec §4.3). His note: "when talking to the random guy in the
// dumpster, i can zoom up pretty close to ace which is awesome but when i walk towards to graces house ... the zoom
// INSTANTLY SHOOTS up ONLY if i try to zoom or touch the zoom button at all". The cause: leaving the bins put the limits
// back to 24-48 m while any camera input killed the turn back, leaving the distance at 8 m; the next zoom press clamped it
// to 24 m in one frame (240 m/s at his 15 fps). Now:
//   * outside at night, following Ace, the view may come in to Night Zoom Min (8 m) everywhere, not only round the back;
//   * a turn has two parts, the angle and the distance: orbiting or tilting cancels only the angle part, zooming only the
//     distance part, so steering can never strand the distance outside its limits;
//   * limits are never applied in one frame: a distance outside them eases back (smoothstep, never over 35 m/s);
//   * zoom input can't push the distance further out of range, and never yanks it in;
//   * leaving a house or the bins keeps a zoom he chose inside; a zoom the house view set by itself is undone;
//   * R3 eases home (the angle in 0.5 s), by day too.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CafeViewMode : MonoBehaviour
{
    public CinemachineCamera isometricCamera;
    public CinemachineCamera firstPersonCamera;
    public Vector3 isometricFocus = new Vector3(0, .6f, 9);
    public Renderer bodyRenderer;
    [Tooltip("Only these authored wall pieces are cut away in an overhead view: above the sill they fade to a ghost when they are in the way.")]
    public Renderer[] cutawayWalls = System.Array.Empty<Renderer>();
    [Tooltip("Hanging fixture meshes hidden in the overhead view. Assign renderers, not lights.")]
    public Renderer[] overheadFixtures = System.Array.Empty<Renderer>();
    [Range(.02f, .3f)] public float lookSensitivity = .09f;
    [Range(1.3f, 1.9f)] public float eyeHeight = 1.65f;
    [Range(20, 60)] public float minimumDistance = 24;
    [Range(25, 80)] public float maximumDistance = 48;

    [Header("Controller")]
    [Tooltip("First-person turn speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(60, 360)] private float padLookYawSpeed = 170f;
    [Tooltip("First-person look up/down speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(40, 240)] private float padLookPitchSpeed = 110f;
    [SerializeField] private bool invertPadLookY;
    [Tooltip("Overhead orbit speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(30, 240)] private float padOrbitSpeed = 110f;
    [Tooltip("Overhead tilt speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(10, 120)] private float padTiltSpeed = 45f;
    [Tooltip("Overhead zoom speed on the triggers, metres per second.")]
    [SerializeField, Range(4, 60)] private float padZoomSpeed = 22f;

    [Header("Cut-away walls")]
    [Tooltip("The see-through shader (Fixit Fidget/Night see-through). Held here so a build keeps it; the walls and the night's buildings fade with it.")]
    [SerializeField] private Shader seeThroughShader;
    [Tooltip("How much of a cut-away wall stays solid in the overhead view, in metres: the window-sill height. Above it the wall fades.")]
    [SerializeField, Range(.3f, 1.6f)] private float cutawayHeight = .78f;
    [Tooltip("Over how many metres above the sill the wall feathers from solid to the ghost.")]
    [SerializeField, Range(.05f, 1.5f)] private float cutawayFeather = .45f;
    [Tooltip("How much of the wall above the sill is still drawn when it is out of the way: .2 is one dot in five.")]
    [SerializeField, Range(.05f, .6f)] private float cutawayGhost = .2f;
    [Tooltip("Seconds a wall takes to fade to its ghost, or back.")]
    [SerializeField, Range(0, 1)] private float cutawaySlideSeconds = .35f;
    [Tooltip("Seconds a wall waits, clearly out of the way, before it comes back. Stops it flickering at the edge.")]
    [SerializeField, Range(0, 2)] private float cutawayRiseDelay = .5f;
    [Tooltip("How much taller a lowered wall is treated when deciding whether it is clearly out of the way, in metres.")]
    [SerializeField, Range(0, 1.5f)] private float cutawayRiseMargin = .4f;

    [Header("Following Ace (the night walk)")]
    [Tooltip("Follow Ace with the café's own tilt and zoom (38-68°, Minimum/Maximum Distance), carrying on from the view the day left. Off: the night's own limits below (the 28 Sept framing: closer and steeper).")]
    [SerializeField] private bool followWithDayFraming = true;
    [Tooltip("Only with Follow With Day Framing off. Tilt limits while the overhead camera follows Ace outside, degrees. The café's own view uses 38-68.")]
    [SerializeField, Range(30, 89)] private float followPitchMin = 55f;
    [SerializeField, Range(30, 89)] private float followPitchMax = 80f;
    [Tooltip("Only with Follow With Day Framing off. Zoom limits while following Ace, metres from Ace. The café's own view uses Minimum/Maximum Distance.")]
    [SerializeField, Range(4, 80)] private float followDistanceMin = 12f;
    [SerializeField, Range(4, 80)] private float followDistanceMax = 34f;
    [Tooltip("Only with Follow With Day Framing off. Where following starts, and where R3 returns to: tilt in degrees, distance in metres.")]
    [SerializeField, Range(30, 89)] private float followStartPitch = 62f;
    [SerializeField, Range(4, 80)] private float followStartDistance = 20f;
    [Tooltip("How far the camera trails behind Ace, in seconds. Hides the capsule's instant starts and stops.")]
    [SerializeField, Range(0, 1)] private float followLag = .2f;
    [Tooltip("The point on Ace the camera looks at, metres above the feet.")]
    [SerializeField, Range(0, 2)] private float followHeight = 1f;
    [Tooltip("How close the overhead camera may zoom while it follows Ace outside at night (Follow With Day Framing on), metres " +
             "from Ace, everywhere he walks. A house view keeps its own House Distance Min. His note, 14 Oct: \"i can zoom up " +
             "pretty close to ace which is awesome\".")]
    [SerializeField, Range(4, 24)] private float nightZoomMin = 8f;

    [Header("Inside a house (the break-ins)")]
    [Tooltip("Zoom limits while Ace is inside a house and the camera looks in from the street, metres from Ace.")]
    [SerializeField, Range(4, 40)] private float houseDistanceMin = 8f;
    [SerializeField, Range(4, 40)] private float houseDistanceMax = 22f;
    [Tooltip("Seconds the camera takes to turn to the house's view, and back.")]
    [SerializeField, Range(0, 3)] private float houseTurnSeconds = .8f;

    PlayerInteractor interactor;
    ConversationController conversation;
    ItemInspector inspector;
    CounterRepairView counterRepair;
    CharacterController capsule;
    PlayerMovement movement;
    CinemachineBrain brain;
    bool firstPerson, pointerReleased, acceptingLook, bodyWasVisible;
    bool[] wallVisibility, fixtureVisibility;
    CutawayWall[] cuts;
    readonly HashSet<Renderer> cutawaySkip = new();
    float yaw, pitch = 8, isoYaw = 45, isoPitch = 50, isoDistance = 34;
    float homeIsoYaw = 45, homeIsoPitch = 50, homeIsoDistance = 34;
    int resumedAtFrame = -1;
    // Following Ace: the smoothed point the overhead camera looks at, and the
    // day's view to go back to.
    bool following;
    Vector3 followFocus, followVelocity;
    float dayIsoYaw, dayIsoPitch, dayIsoDistance, dayHomeYaw, dayHomePitch, dayHomeDistance;
    // Inside a house (the break-ins): the view to turn back to, and whether he zoomed while inside (then his zoom stays).
    bool inHouse, zoomedInside;
    float outsideYaw, outsidePitch, outsideDistance, outsideHomeYaw, outsideHomePitch, outsideHomeDistance;
    // A turn, in two parts (14 Oct): the angle (turn and tilt) over its own seconds, and the distance, eased (smoothstep) over
    // max(0.5 s, 1.5 x the gap / Limit Speed) so it never moves faster than Limit Speed. Orbit and tilt input cancel only the
    // angle part; zoom input only the distance part. The distance part also carries the limits' pull (KeepInLimits).
    bool turningAngle, turningDistance, zoomedThisFrame;
    float angleT, angleSeconds = .8f, distanceT, distanceSeconds = .5f;
    float turnFromYaw, turnFromPitch, turnToYaw, turnToPitch, turnFromDistance, turnToDistance;

    /// <summary>The most a limit change, an eased turn or R3 moves the overhead camera's distance, metres a second (feel target T20).</summary>
    public const float LimitSpeed = 35f;
    /// <summary>The shortest distance ease, seconds (decision 10: "easing over about half a second").</summary>
    public const float DistanceEaseLeast = .5f;
    /// <summary>R3's angle part, seconds.</summary>
    public const float HomeTurnSeconds = .5f;
    // The tilt's own pull, should its limits ever move under it (today they don't: Follow With Day Framing is on).
    const float PitchLimitSpeed = 90f;

    public bool FirstPersonSelected => firstPerson;
    public bool WalkingFirstPerson => isActiveAndEnabled && firstPerson && !AtStation && !OverlayOwnsInput;
    public bool PointerReleased => pointerReleased;
    public bool CanChangeView => isActiveAndEnabled && !AtStation && !OverlayOwnsInput;
    // Built once per state, not once per frame: the HUD asks every frame, and the pad's line with its
    // labels was 0.3 KB of garbage a frame (30 Sept). The same string comes back until something changes.
    string hintCache = "";
    int hintKey = -1;
    public string ControlsHint
    {
        get
        {
            int key = (CanChangeView ? 1 : 0) | (PadInput.UsingPad ? 2 : 0) | (firstPerson ? 4 : 0) | (pointerReleased ? 8 : 0) | ((int)PadInput.Kind << 4);
            if (key == hintKey) return hintCache;
            hintKey = key;
            // Esc (Start on a pad) brings up the phone and pauses (PausePhone, playtest 3 session 3); it no longer frees
            // the cursor in first person, the phone does that while it's up.
            string pause = $"    {ControlHints.Pause}  Pause";
            hintCache = !CanChangeView ? "" : PadInput.UsingPad
                ? firstPerson ? $"{ControlHints.View}  Isometric" + pause
                    : $"{ControlHints.View}  First person    Right stick  Orbit    {ControlHints.Zoom}  Zoom" + pause
                : firstPerson
                    ? pointerReleased ? "Click to look around    V  Isometric" + pause : "V  Isometric" + pause
                    : "V  First person    Middle-drag  Orbit    Scroll  Zoom" + pause;
            return hintCache;
        }
    }
    public bool SuppressWalkingInteraction => WalkingFirstPerson
        && (pointerReleased || Time.frameCount <= resumedAtFrame || brain != null && brain.IsBlending);
    public bool SuppressWalkingMovement => WalkingFirstPerson && pointerReleased;
    public float MovementYaw => WalkingFirstPerson ? yaw : isoYaw;
    /// <summary>The overhead camera's turn, tilt and distance.</summary>
    public Vector3 OverheadAngle => new Vector3(isoYaw, isoPitch, isoDistance);
    /// <summary>The cut-away walls (null entries: walls that are never drawn).</summary>
    public IReadOnlyList<CutawayWall> CutawayWalls => cuts ?? System.Array.Empty<CutawayWall>();
    /// <summary>True while the overhead camera follows Ace (a night walk).</summary>
    public bool Following => following;
    /// <summary>True while Ace is inside a house and the camera looks in from the street (EnterHouseView).</summary>
    public bool InHouseView => inHouse;
    /// <summary>The point the overhead camera looks at: the café's centre by day, Ace (trailing) when following.</summary>
    public Vector3 OverheadFocus => following ? followFocus : isometricFocus;
    /// <summary>The overhead camera's limits right now: tilt min and max in degrees, distance min and max in metres
    /// (the café's own, and the night's own only while following with Follow With Day Framing off).</summary>
    public Vector4 OverheadLimits => new Vector4(PitchMin, PitchMax, DistanceMin, DistanceMax);
    /// <summary>Where the controller's R3 returns the overhead camera to: turn, tilt, distance.</summary>
    public Vector3 OverheadHome => new Vector3(homeIsoYaw, homeIsoPitch, homeIsoDistance);
    /// <summary>The overhead view is on screen: not first person, not a close-up at a station, a dialogue or an item.</summary>
    public bool OverheadShown => isActiveAndEnabled && OverheadPresentation;
    /// <summary>Ace's feet: the capsule's bottom (the player's origin is the capsule's centre).</summary>
    public Vector3 AceFeet => transform.position + Vector3.up * (capsule != null ? capsule.center.y - capsule.height * .5f : -1f);
    /// <summary>Ace is inside the café room (by position; the same room the café's own lights belong to).</summary>
    public bool AceInsideCafe => CafeDaylight.CafeInside.Contains(new Vector2(transform.position.x, transform.position.z));
    /// <summary>Ace is drawn: not in first person and not at a station (the capsule's rule, which AceBody follows).</summary>
    public bool ShowsAce => !AtStation && !firstPerson;
    /// <summary>
    /// A conversation begun from the overhead view, seen from just behind Ace's shoulder (ConversationController, 12 Oct): Ace's
    /// body is drawn in it (AceBody), though the overhead view itself is off the screen meanwhile.
    /// </summary>
    public bool TalkingOverShoulder => isActiveAndEnabled && !firstPerson && !AtStation
        && conversation != null && conversation.SeenOverShoulder
        && !(inspector != null && inspector.IsHoldingItem)
        && !(counterRepair != null && counterRepair.OwnsInput);
    /// <summary>Set by AceBody while Ace's stand-in body is drawn instead of the capsule: the capsule's mesh hides.</summary>
    public bool BodyStandsIn { get; set; }
    /// <summary>
    /// Ace is hidden in a cupboard or a wardrobe (break-ins chunk C, GraceAtHome): nothing of Ace is drawn (the body, the
    /// capsule, what's in Ace's hand), and the view can't be switched until Ace comes out; it stays where it is.
    /// </summary>
    public bool AceSetAside { get; set; }
    /// <summary>How close the view may come while following Ace outside at night (metres; checks).</summary>
    public float NightZoomMin => nightZoomMin;
    /// <summary>A turn's angle part is easing (EnterHouseView, ExitHouseView, R3).</summary>
    public bool AngleEasing => turningAngle;
    /// <summary>The distance is easing: a turn's distance part, R3's, or the limits' pull back into range.</summary>
    public bool DistanceEasing => turningDistance;
    // The night's own limits apply only while following with the day's framing switched off. Following with the day's
    // framing (the scene's setting), the night comes in to Night Zoom Min everywhere outside (14 Oct); a house view keeps
    // its own; by day nothing changes (24-48 m round the café's middle).
    bool NightFraming => following && !followWithDayFraming;
    float PitchMin => NightFraming ? followPitchMin : 38f;
    float PitchMax => NightFraming ? followPitchMax : 68f;
    float DistanceMin => inHouse ? houseDistanceMin : NightFraming ? followDistanceMin : following ? nightZoomMin : minimumDistance;
    float DistanceMax => inHouse ? houseDistanceMax : NightFraming ? followDistanceMax : maximumDistance;
    Vector3 AceFocus => AceFeet + Vector3.up * followHeight;
    bool AtStation => interactor != null && interactor.IsAtStation;
    // The day intro (12 Oct 2026, Cinematics) owns the screen for its 6-8 s: the view takes no input meanwhile (no V, no look),
    // and the first delta after it is dropped as after any blend, so the player's own view comes back exactly where it was.
    bool OverlayOwnsInput => Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.RecapOwnsInput
        || conversation != null && conversation.InConversation || inspector != null && inspector.IsHoldingItem
        || counterRepair != null && counterRepair.OwnsInput || DayIntro.Playing;
    // Pausing or opening the end-of-day recap does not change the camera's
    // presentation. Keep the cutaway consistent until a close-up owns it. The day intro's shots are
    // close-ups too (the storefront seen from the street wants its walls solid and its pendants hung).
    bool OverheadPresentation => !firstPerson && !AtStation
        && !(conversation != null && conversation.InConversation)
        && !(inspector != null && inspector.IsHoldingItem)
        && !(counterRepair != null && counterRepair.OwnsInput)
        && !(DayIntro.Playing && !(DayIntro.Instance != null && DayIntro.Instance.PullingOut));   // the walls open as the pull-out begins (QA, 13 Oct)

    void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        conversation = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        capsule = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        var main = Camera.main;
        brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
        bodyWasVisible = bodyRenderer != null && bodyRenderer.enabled;
        wallVisibility = new bool[cutawayWalls.Length];
        for (int i = 0; i < cutawayWalls.Length; i++)
            wallVisibility[i] = cutawayWalls[i] != null && cutawayWalls[i].enabled;
        // Each drawn cut-away wall fades above the sill when it is in the way (see CutawayWall).
        SeeThroughMaterials.Provide(seeThroughShader);
        cuts = new CutawayWall[cutawayWalls.Length];
        for (int i = 0; i < cutawayWalls.Length; i++)
        {
            if (cutawayWalls[i] == null) continue;
            cutawaySkip.Add(cutawayWalls[i]);
            if (!wallVisibility[i]) continue;
            cuts[i] = new CutawayWall(cutawayWalls[i], cutawayHeight, cutawayFeather, cutawayGhost);
            if (!cuts[i].Fades) Debug.Log($"[View] {cutawayWalls[i].name} can't fade ({cuts[i].Problem}), so it hides in the overhead view instead.", cutawayWalls[i]);
        }
        foreach (Renderer fixture in overheadFixtures) if (fixture != null) cutawaySkip.Add(fixture);
        fixtureVisibility = new bool[overheadFixtures.Length];
        for (int i = 0; i < overheadFixtures.Length; i++)
            fixtureVisibility[i] = overheadFixtures[i] != null && overheadFixtures[i].enabled;
        if (isometricCamera != null)
        {
            isoYaw = isometricCamera.transform.eulerAngles.y;
            isoPitch = Mathf.DeltaAngle(0, isometricCamera.transform.eulerAngles.x);
            isoDistance = Mathf.Clamp(Vector3.Distance(isometricCamera.transform.position, isometricFocus), minimumDistance, maximumDistance);
        }
        // Where the controller's R3 returns the overhead view to.
        homeIsoYaw = isoYaw; homeIsoPitch = isoPitch; homeIsoDistance = isoDistance;
        // Let the authored walking camera face the cafe on first entry.
        yaw = firstPersonCamera != null ? firstPersonCamera.transform.eulerAngles.y : isoYaw;
        if (firstPersonCamera != null) firstPersonCamera.Priority = 0;
    }

    // THE WALLS, READIED AHEAD OF TIME (30 Sept 2026, the performance pass)
    // A wall going down for the first time used to search the scene for what hangs on it and make its
    // see-through copies in that very frame: with several walls at once (back from first person) that
    // was a 10 ms hitch in the build. Now the frames right after loading do it, the search first and
    // then one wall a frame, while nobody is looking.
    void Start() => StartCoroutine(PrepareCutawayWalls());

    System.Collections.IEnumerator PrepareCutawayWalls()
    {
        yield return null;
        if (cuts == null) yield break;
        CutawayWall.PrepareCandidates();
        for (int i = 0; i < cuts.Length; i++)
        {
            yield return null;
            if (cuts[i] != null && cuts[i].Wall != null) cuts[i].Prepare(transform, cutawaySkip);
        }
    }

    // Aim help in first person (AimAssist): the view's own state, and what it may draw the crosshair to this frame.
    readonly AimAssist.State aim = new AimAssist.State();
    readonly List<AimAssist.Target> aimTargets = new List<AimAssist.Target>(32);

    /// <summary>The first-person view's aim help as it goes (checks).</summary>
    public AimAssist.State Aim => aim;

    /// <summary>A D-pad step in first person (PlayerInteractor): the view glides to look at <paramref name="point"/>.</summary>
    public void StepAimTo(Vector3 point) => aim.StepTo(point);

    /// <summary>Where Ace's eyes are (the first-person camera's place): the conversation is seen from here (12 Oct).</summary>
    public Vector3 EyePoint => Eye;

    // Where the first-person eye is (as RefreshCameraPose puts it).
    Vector3 Eye
    {
        get
        {
            float floorOffset = capsule != null ? capsule.center.y - capsule.height * .5f : -1;
            float crouchDrop = movement != null ? movement.EyeDrop : 0f;
            return transform.position + Vector3.up * (floorOffset + eyeHeight - crouchDrop);
        }
    }

    void Update()
    {
        if (counterRepair == null) counterRepair = GetComponent<CounterRepairView>();
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (!Application.isFocused || !CanChangeView) { acceptingLook = false; aim.Reset(); return; }
        // V, or the controller's View / Share / Minus button.
        if ((keyboard != null && keyboard.vKey.wasPressedThisFrame || PadInput.Pressed(PadButton.Select)) && !AceSetAside)
        {
            SetFirstPerson(!firstPerson);
            return;
        }
        // A hitch must not turn one held stick into a huge jump.
        float padDelta = Mathf.Min(Time.unscaledDeltaTime, .1f);
        if (firstPerson)
        {
            if (pointerReleased)
            {
                bool clickResume = mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI();
                // A controller never needs a free cursor: touching a stick resumes looking.
                bool padResume = PadInput.UsingPad
                    && (PadInput.RightStick != Vector2.zero || PadInput.LeftStick != Vector2.zero);
                if (clickResume || padResume)
                { pointerReleased = false; resumedAtFrame = Time.frameCount; RefreshCursor(); }
                acceptingLook = false;
                return;
            }
            if (brain != null && brain.IsBlending)
            { acceptingLook = false; aim.Reset(); return; }
            // Ignore the first delta after a blend, cursor lock or focus change.
            if (!acceptingLook) { acceptingLook = true; return; }
            // The mouse turns the view by the distance it travelled, untouched by the aim help. The player's look
            // sensitivity and invert Y (GameSettings, the phone's Settings) apply on top of the view's own.
            Vector2 delta = mouse != null ? mouse.delta.ReadValue() * lookSensitivity * GameSettings.LookScale : Vector2.zero;
            yaw = Mathf.Repeat(yaw + delta.x, 360);
            pitch = Mathf.Clamp(pitch - delta.y * GameSettings.YSign, -75, 75);
            // The stick is a turn RATE (degrees per second), unlike the mouse's
            // travelled distance, so it is scaled by frame time (and by the player's pad look speed).
            Vector2 stick = PadInput.Curved(PadInput.RightStick);
            float padLook = GameSettings.PadLookScale;
            Vector2 turn = stick == Vector2.zero ? Vector2.zero
                : new Vector2(stick.x * padLookYawSpeed * padLook * padDelta,
                    -stick.y * padLookPitchSpeed * padLook * padDelta * (invertPadLookY ? -1f : 1f) * GameSettings.YSign);
            if (AimAssist.Active && interactor != null)
            {
                // Aim help (a pad only; playtest 3): slower over a target, drawn toward its middle while turning, settled
                // onto it when the stick lets go, and the D-pad's steps (PlayerInteractor asks: StepAimTo).
                bool moving = stick != Vector2.zero;
                if (aim.WantsTargets(moving)) interactor.AimTargets(aimTargets);
                else aimTargets.Clear();
                Vector2 view = AimAssist.Steer(Eye, new Vector2(yaw, pitch), turn, stick.magnitude, padDelta, aimTargets, aim);
                yaw = Mathf.Repeat(view.x, 360);
                pitch = Mathf.Clamp(view.y, -75, 75);
            }
            else
            {
                aim.Reset();
                yaw = Mathf.Repeat(yaw + turn.x, 360);
                pitch = Mathf.Clamp(pitch + turn.y, -75, 75);
            }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        else
        {
            if (mouse != null && !PointerOverUI())
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (delta != Vector2.zero) turningAngle = false;   // steering ends the angle part only: the distance eases on
                    isoYaw = Mathf.Repeat(isoYaw + delta.x * .18f * GameSettings.LookScale, 360);
                    isoPitch = Mathf.Clamp(isoPitch + delta.y * .12f * GameSettings.LookScale * GameSettings.YSign, PitchMin, PitchMax);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f && !Barks.Choosing) Zoom(-Mathf.Clamp(scroll / 120f, -3, 3) * 1.6f);   // 20 Oct: while a reply is being chosen the wheel moves the highlight, as by day
            }
            // Controller: right stick orbits and tilts, triggers zoom (RT in,
            // LT out), R3 eases back to the authored overhead angle.
            Vector2 orbit = PadInput.Curved(PadInput.RightStick, 1.4f);
            if (orbit != Vector2.zero)
            {
                turningAngle = false;   // the angle part only (14 Oct): the stick he steers with while walking can't strand the distance
                isoYaw = Mathf.Repeat(isoYaw + orbit.x * padOrbitSpeed * GameSettings.PadLookScale * padDelta, 360);
                isoPitch = Mathf.Clamp(isoPitch - orbit.y * padTiltSpeed * GameSettings.PadLookScale * padDelta * GameSettings.YSign, PitchMin, PitchMax);
            }
            float zoom = PadInput.RightTrigger - PadInput.LeftTrigger;
            if (Mathf.Abs(zoom) > .01f) Zoom(-zoom * padZoomSpeed * padDelta);
            if (PadInput.Pressed(PadButton.RightStickPress)) GoHome();
        }
    }

    // Zoom input (the wheel, the triggers): it cancels the distance part of a turn and skips this frame's pull. Clamped to the
    // limits widened to include where the distance is now (14 Oct): it can't push the distance further out of range and never
    // pulls it in. The old hard clamp here was the snap: 8 m to 24 m in one frame the moment he touched a trigger.
    void Zoom(float change)
    {
        zoomedThisFrame = true;
        turningDistance = false;
        if (inHouse) zoomedInside = true;
        float lo = Mathf.Min(DistanceMin, isoDistance), hi = Mathf.Max(DistanceMax, isoDistance);
        isoDistance = Mathf.Clamp(isoDistance + change, lo, hi);
    }

    // R3: home, eased (14 Oct; by day too, so the controller's re-centre reads as one camera language with the house turns).
    // Outside at night home is the day's home that FollowAce carried over; in a house view, the house's own.
    void GoHome() => TurnTo(homeIsoYaw, Mathf.Clamp(homeIsoPitch, PitchMin, PitchMax), Mathf.Clamp(homeIsoDistance, DistanceMin, DistanceMax), HomeTurnSeconds);

    /// <summary>
    /// Turns the overhead camera to a given angle (the same limits as orbiting by
    /// hand). For play-mode checks and scripted views; the player's own orbit
    /// simply continues from here.
    /// </summary>
    public void OrbitTo(float yawDegrees, float pitchDegrees, float distance)
    {
        // A hard set, as before (labs and scripted views): no ease, both parts of any turn ended.
        turningAngle = turningDistance = false;
        isoYaw = Mathf.Repeat(yawDegrees, 360);
        isoPitch = Mathf.Clamp(pitchDegrees, PitchMin, PitchMax);
        isoDistance = Mathf.Clamp(distance, DistanceMin, DistanceMax);
        RefreshCameraPose();
    }

    /// <summary>
    /// Turns the walking (first-person) view to a heading and tilt. For play-mode checks
    /// and scripted views; the player's own mouse or stick carries on from here.
    /// </summary>
    public void LookTo(float yawDegrees, float pitchDegrees)
    {
        yaw = Mathf.Repeat(yawDegrees, 360);
        pitch = Mathf.Clamp(pitchDegrees, -75, 75);
        if (firstPerson) transform.rotation = Quaternion.Euler(0, yaw, 0);
        RefreshCameraPose();
    }

    /// <summary>
    /// The night walk's overhead camera: on, it follows Ace with the same orbit and zoom
    /// controls. With Follow With Day Framing (the default) the tilt, the zoom and R3's home
    /// carry on from the day's view; without it, following starts closer and steeper, within
    /// the night's own limits. Off, the day's view comes back exactly as it was left. Safe to
    /// call twice.
    /// </summary>
    public void FollowAce(bool on)
    {
        if (on == following) return;
        if (on)
        {
            dayIsoYaw = isoYaw; dayIsoPitch = isoPitch; dayIsoDistance = isoDistance;
            dayHomeYaw = homeIsoYaw; dayHomePitch = homeIsoPitch; dayHomeDistance = homeIsoDistance;
            turningAngle = turningDistance = false;   // a cut under the night's curtain: whatever the day was easing is over
            following = true;
            // Keep the heading, so the streets stay the way round they were. With the day's
            // framing the tilt and the zoom carry on too: only what the camera looks at changes.
            if (!followWithDayFraming)
            {
                // The night's own framing; R3 comes back here.
                isoPitch = homeIsoPitch = Mathf.Clamp(followStartPitch, followPitchMin, followPitchMax);
                isoDistance = homeIsoDistance = Mathf.Clamp(followStartDistance, followDistanceMin, followDistanceMax);
            }
            followFocus = AceFocus;
            followVelocity = Vector3.zero;
        }
        else
        {
            // The night is over: the day's view comes back, whatever the house view was doing. A cut, under the morning's
            // curtain (NightWalk.End), never an ease.
            inHouse = false;
            zoomedInside = false;
            turningAngle = turningDistance = false;
            following = false;
            isoYaw = dayIsoYaw; isoPitch = dayIsoPitch; isoDistance = dayIsoDistance;
            homeIsoYaw = dayHomeYaw; homeIsoPitch = dayHomePitch; homeIsoDistance = dayHomeDistance;
        }
        RefreshCameraPose();
    }

    /// <summary>
    /// Ace has walked into a house (the break-ins): turn the overhead camera to the given angle over House Turn
    /// Seconds (yaw and tilt in degrees, distance in metres from Ace), and allow closer zoom (House Distance Min/Max).
    /// R3 comes back to this view. Only while following Ace; safe to call twice.
    /// </summary>
    public void EnterHouseView(float yawDegrees, float pitchDegrees, float distance)
    {
        if (!following || inHouse) return;
        // Where the view was going, if it was still on its way back from the last house (so a quick in-and-out doesn't
        // remember a half-way distance as "outside").
        outsideYaw = turningAngle ? turnToYaw : isoYaw;
        outsidePitch = turningAngle ? turnToPitch : isoPitch;
        outsideDistance = turningDistance ? turnToDistance : isoDistance;
        outsideHomeYaw = homeIsoYaw; outsideHomePitch = homeIsoPitch; outsideHomeDistance = homeIsoDistance;
        inHouse = true;
        zoomedInside = false;
        homeIsoYaw = Mathf.Repeat(yawDegrees, 360);
        homeIsoPitch = Mathf.Clamp(pitchDegrees, PitchMin, PitchMax);
        homeIsoDistance = Mathf.Clamp(distance, DistanceMin, DistanceMax);
        TurnTo(homeIsoYaw, homeIsoPitch, homeIsoDistance, houseTurnSeconds);
    }

    /// <summary>
    /// Ace has walked out again: turn back to the angle from before EnterHouseView. The distance goes back too, unless he
    /// zoomed while inside: a zoom he chose stays (14 Oct; "it stays in this close view which is cool"), a zoom the house view
    /// set by itself is undone. Safe to call twice.
    /// </summary>
    public void ExitHouseView()
    {
        if (!inHouse) return;
        inHouse = false;
        homeIsoYaw = outsideHomeYaw; homeIsoPitch = outsideHomePitch; homeIsoDistance = outsideHomeDistance;
        float distance = zoomedInside ? isoDistance : outsideDistance;
        zoomedInside = false;
        TurnTo(outsideYaw, Mathf.Clamp(outsidePitch, PitchMin, PitchMax), Mathf.Clamp(distance, DistanceMin, DistanceMax), houseTurnSeconds);
    }

    // A turn in two parts: the angle over its own seconds (0: at once, as before), the distance eased (EaseDistance).
    void TurnTo(float yawDegrees, float pitchDegrees, float distance, float seconds)
    {
        turnFromYaw = isoYaw; turnFromPitch = isoPitch;
        turnToYaw = Mathf.Repeat(yawDegrees, 360f); turnToPitch = pitchDegrees;
        angleT = 0f;
        angleSeconds = seconds;
        turningAngle = seconds > 0f;
        if (!turningAngle) { isoYaw = turnToYaw; isoPitch = turnToPitch; }
        EaseDistance(distance);
    }

    // The distance part: from where it is, smoothstep over max(0.5 s, 1.5 x the gap / Limit Speed). Smoothstep's steepest
    // slope is 1.5, so the camera never moves faster than Limit Speed (35 m/s); a gap up to 11.7 m takes the half second.
    // Closed form from the ease's own elapsed time (rule 8: his Mac runs at 15-20 fps), never a stepped spring.
    void EaseDistance(float target)
    {
        float gap = Mathf.Abs(target - isoDistance);
        if (gap < .001f) { isoDistance = target; turningDistance = false; return; }
        turnFromDistance = isoDistance;
        turnToDistance = target;
        distanceT = 0f;
        distanceSeconds = Mathf.Max(DistanceEaseLeast, 1.5f * gap / LimitSpeed);
        turningDistance = true;
    }

    void StepTurn(float dt)
    {
        if (turningAngle)
        {
            angleT = Mathf.MoveTowards(angleT, 1f, dt / Mathf.Max(.01f, angleSeconds));
            float t = Mathf.SmoothStep(0f, 1f, angleT);
            isoYaw = Mathf.Repeat(Mathf.LerpAngle(turnFromYaw, turnToYaw, t), 360);
            isoPitch = Mathf.Lerp(turnFromPitch, turnToPitch, t);
            if (angleT >= 1f) turningAngle = false;
        }
        if (turningDistance)
        {
            distanceT = Mathf.MoveTowards(distanceT, 1f, dt / Mathf.Max(.01f, distanceSeconds));
            isoDistance = Mathf.Lerp(turnFromDistance, turnToDistance, Mathf.SmoothStep(0f, 1f, distanceT));
            if (distanceT >= 1f) turningDistance = false;
        }
    }

    // Limits are never applied in one frame (14 Oct, decision 10): a distance outside them, by whatever way it got there,
    // eases back into them as a distance part of its own; one already easing toward a place the limits now forbid is aimed
    // at the limit instead, from where it is (never a jump). A frame with zoom input skips this, so the two never stack.
    void KeepInLimits(float dt)
    {
        if (!zoomedThisFrame)
        {
            float lo = DistanceMin, hi = DistanceMax;
            if (turningDistance)
            {
                if (turnToDistance < lo - .001f || turnToDistance > hi + .001f) EaseDistance(Mathf.Clamp(turnToDistance, lo, hi));
            }
            else if (isoDistance < lo - .001f || isoDistance > hi + .001f) EaseDistance(Mathf.Clamp(isoDistance, lo, hi));
        }
        if (!turningAngle && (isoPitch < PitchMin - .001f || isoPitch > PitchMax + .001f))
            isoPitch = Mathf.MoveTowards(isoPitch, Mathf.Clamp(isoPitch, PitchMin, PitchMax), PitchLimitSpeed * dt);
    }

    // Public so the existing scene recipe and play-mode validation can use the
    // same transition as V; this never forces an exit from a repair or dialog.
    public bool SetFirstPerson(bool enabled)
    {
        if (!CanChangeView || enabled && firstPersonCamera == null || !enabled && isometricCamera == null) return false;
        firstPerson = enabled;
        pointerReleased = false;
        acceptingLook = false;
        resumedAtFrame = Time.frameCount;
        if (firstPerson) transform.rotation = Quaternion.Euler(0, yaw, 0);
        RefreshCameraPose();
        RefreshCursor();
        return true;
    }

    void LateUpdate()
    {
        // Following Ace: trail the capsule by followLag seconds (critically damped, so it
        // settles without overshoot). Paused, it holds still.
        if (following)
            followFocus = followLag <= 0f ? AceFocus
                : Vector3.SmoothDamp(followFocus, AceFocus, ref followVelocity, followLag, Mathf.Infinity, Time.deltaTime);
        // Real time (the pause phone can't strand a turn half way), with a hitch capped as the intro caps it: after a long
        // frame the ease carries on from where it was rather than leaping to its end. The cap is 3/35 s (T20, restated 14 Oct):
        // at most 3 m of the distance's ease in any frame, a hitch included; 15 fps is unchanged.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 3f / LimitSpeed);
        StepTurn(dt);
        KeepInLimits(dt);
        zoomedThisFrame = false;
        RefreshCameraPose();
        RefreshCursor();
        if (bodyRenderer != null)
            bodyRenderer.enabled = bodyWasVisible && !AtStation && !firstPerson && !BodyStandsIn && !AceSetAside;
        RefreshCutawayWalls();
        RefreshOverheadFixtures();
    }

    void RefreshCameraPose()
    {
        if (firstPersonCamera != null)
        {
            // Player origin is at the capsule centre, not its feet. Crouched (sneaking), the eye drops with Ace's head
            // (the capsule crouches with it from 7 Oct, its feet kept where they are, so the floor stays where it was).
            firstPersonCamera.transform.SetPositionAndRotation(Eye, Quaternion.Euler(pitch, yaw, 0));
            firstPersonCamera.Priority = firstPerson ? 15 : 0;
        }
        if (isometricCamera != null)
        {
            Quaternion angle = Quaternion.Euler(isoPitch, isoYaw, 0);
            isometricCamera.transform.SetPositionAndRotation(OverheadFocus - angle * Vector3.forward * isoDistance, angle);
        }
    }

    void RefreshCursor()
    {
        bool locked = Application.isFocused && !OverlayOwnsInput && (AtStation || firstPerson && !pointerReleased);
        CursorLockMode mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != mode) { Cursor.lockState = mode; acceptingLook = false; }
        // With a controller in hand the mouse arrow is only clutter; views that
        // need a pointer draw the controller's own cursor (PadCursor).
        Cursor.visible = !locked && !PadInput.UsingPad;
    }

    // A wall that hides the room from the overhead camera fades to a ghost above
    // the sill instead of vanishing (CutawayWall). It goes as soon as it is in
    // the way, and only comes back once it has been clearly out of the way
    // (with a margin) for a moment, so it never flickers at the edge.
    void RefreshCutawayWalls()
    {
        if (cuts == null || isometricCamera == null) return;
        Vector3 origin = isometricCamera.transform.position;
        bool overhead = OverheadPresentation;
        // Following Ace outside, a café wall only gives way when it hides Ace (the room
        // behind it is closed and empty); inside, the day's rule shows the room.
        bool aceOutside = following && !AceInsideCafe;
        float dt = Time.unscaledDeltaTime;
        foreach (CutawayWall cut in cuts)
        {
            if (cut == null || cut.Wall == null) continue;
            Bounds full = cut.FullBounds;
            bool blocks = overhead && (aceOutside ? BlocksAce(full, origin) : BlocksInterior(full, origin));
            bool nearly = blocks;
            if (!nearly && overhead && cut.GoingDown)
            {
                Bounds taller = full;
                taller.SetMinMax(full.min, full.max + Vector3.up * cutawayRiseMargin);
                nearly = aceOutside ? BlocksAce(taller, origin) : BlocksInterior(taller, origin);
            }
            cut.Step(overhead, blocks, nearly, dt, cutawaySlideSeconds, cutawayRiseDelay, transform, cutawaySkip);
        }
    }

    // Sightlines to Ace's knees, middle and head.
    bool BlocksAce(Bounds wall, Vector3 origin)
    {
        Vector3 feet = AceFeet;
        return BlocksSightline(wall, origin, feet + Vector3.up * .4f)
            || BlocksSightline(wall, origin, feet + Vector3.up * 1f)
            || BlocksSightline(wall, origin, feet + Vector3.up * 1.7f);
    }

    bool BlocksInterior(Bounds wall, Vector3 origin)
    {
        // A camera being outside a wall does not mean that wall obscures the
        // room: the view may clear its top or its end. Test finite sightlines
        // to the room centre and three points just inside this wall instead.
        // The edge probes keep occupied perimeter seats readable when orbiting.
        Vector3 target = wall.center;
        target.y = isometricFocus.y + .55f;
        bool normalX = wall.size.x < wall.size.z;
        float inward = normalX ? isometricFocus.x - wall.center.x : isometricFocus.z - wall.center.z;
        float direction = inward < 0 ? -1 : 1;
        Vector3 along;
        if (normalX)
        {
            target.x += direction * (wall.extents.x + 1.25f);
            along = Vector3.forward * Mathf.Max(0, wall.extents.z - 1.25f);
        }
        else
        {
            target.z += direction * (wall.extents.z + 1.25f);
            along = Vector3.right * Mathf.Max(0, wall.extents.x - 1.25f);
        }
        return BlocksSightline(wall, origin, isometricFocus)
            || BlocksSightline(wall, origin, target)
            || BlocksSightline(wall, origin, target - along)
            || BlocksSightline(wall, origin, target + along);
    }

    static bool BlocksSightline(Bounds wall, Vector3 origin, Vector3 target)
    {
        Vector3 segment = target - origin;
        float length = segment.magnitude;
        return length > .01f && wall.IntersectRay(new Ray(origin, segment / length), out float hitDistance)
            && hitDistance < length - .02f;
    }

    void RefreshOverheadFixtures()
    {
        if (fixtureVisibility == null) return;
        bool overhead = OverheadPresentation;
        for (int i = 0; i < Mathf.Min(overheadFixtures.Length, fixtureVisibility.Length); i++)
            if (overheadFixtures[i] != null)
                overheadFixtures[i].enabled = fixtureVisibility[i] && !overhead;
    }

    static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    void OnApplicationFocus(bool focused) { acceptingLook = false; if (!focused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    void OnDisable()
    {
        if (firstPersonCamera != null) firstPersonCamera.Priority = 0;
        if (bodyRenderer != null) bodyRenderer.enabled = bodyWasVisible;
        if (cuts != null)
            foreach (CutawayWall cut in cuts) cut?.Restore();
        if (wallVisibility != null)
            for (int i = 0; i < Mathf.Min(cutawayWalls.Length, wallVisibility.Length); i++)
                if (cutawayWalls[i] != null) cutawayWalls[i].enabled = wallVisibility[i];
        if (fixtureVisibility != null)
            for (int i = 0; i < Mathf.Min(overheadFixtures.Length, fixtureVisibility.Length); i++)
                if (overheadFixtures[i] != null) overheadFixtures[i].enabled = fixtureVisibility[i];
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        if (cuts == null) return;
        foreach (CutawayWall cut in cuts) cut?.Dispose();
        cuts = null;
    }
}
