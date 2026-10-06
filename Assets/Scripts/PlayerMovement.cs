using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    // WHY FIRST-PERSON WALKING IS SMOOTHED
    //
    // The first-person camera is bolted to the capsule, so every change in the
    // capsule's velocity is a change in what the player sees. Keyboard movement
    // used to be instant: walking a square, you release W a few milliseconds
    // before pressing A, and for those frames the input is zero. Instant
    // movement turned that gap into a dead stop and then a full-speed jump —
    // a visible stutter at every corner, and a hard jolt on every reversal.
    //
    // So first-person walking eases the *input*, not the camera: the view stays
    // attached to the body (no lag, no swimming), and only speed and direction
    // change over about a tenth of a second. The easing happens in camera space,
    // so turning the mouse still turns your walking instantly.
    //
    // Isometric movement keeps its immediate response on purpose — the overhead
    // camera doesn't move with the body, so the same jolt isn't visible there.
    [Header("First-person walking feel")]
    [Tooltip("How quickly walking speeds up or changes direction, in metres per second squared. " +
             "50 reaches full speed in a tenth of a second. Higher is snappier, lower is floatier.")]
    [SerializeField, Min(1f)] private float firstPersonAcceleration = 50f;

    [Tooltip("How quickly walking slows once every movement key is released, in metres per second squared. " +
             "50 stops from full speed in a tenth of a second, sliding about a quarter of a metre. It also sets " +
             "how much speed survives the short gap between letting go of one key and pressing the next at a corner.")]
    [SerializeField, Min(1f)] private float firstPersonBraking = 50f;

    // WHY THE OVERHEAD VIEW PULLS A STICK ONTO THE ROOM'S AXES (30 Sept 2026)
    //
    // The overhead camera looks at the café from a corner (the authored view turns 25° from the room's
    // axes; the player can orbit it anywhere), and the stick is turned by that yaw, so pushing straight
    // up walks straight up the screen, which is diagonal to every wall, counter and aisle: they all lie
    // on the world's axes. Walking along the counter means holding the stick at exactly the angle the
    // counter makes on screen, and a thumb on an analog stick never holds an angle for long, so Ace
    // drifted into the counter or away from it (Mansoor's second playtest: "hard to move straight on
    // the controller in isometric view").
    //
    // So in the overhead view the WORLD direction the stick asks for is pulled onto the nearest line of
    // two families: the room's axes (0, 90, 180, 270°: along the walls), and the screen's axes (the
    // camera's yaw and its right angles: straight up, down, left and right on screen). Dead on within a
    // core, blending back to the stick's own direction at the edge, so nothing jumps as the thumb rolls;
    // between the bands the stick is free. The keyboard's directions are exact already, so it changes
    // nothing there (at the authored 25° no key walks along a wall: that is the camera's angle, not the
    // stick's); first person keeps pure analog (you steer with the mouse or the right stick); the labs'
    // scripted input is never shaped (a check steers toward exact points).
    [Header("Overhead movement assist")]
    [Tooltip("In the overhead view a stick direction close to one of the room's axes (along the walls), or to straight up, down, left or right on screen, is pulled onto it, so walking along a counter or a wall doesn't drift. Off: pure analog. First person and the keyboard are unchanged either way.")]
    [SerializeField] private bool movementAssist = true;
    [Tooltip("Degrees either side of a room axis (along the walls) that count as dead on: the walk is exactly along the axis.")]
    [SerializeField, Range(0f, 22f)] private float assistAxisCore = 10f;
    [Tooltip("Degrees either side of a room axis where the pull fades out; past this the stick is free.")]
    [SerializeField, Range(0f, 30f)] private float assistAxisEdge = 15f;
    [Tooltip("Degrees either side of straight up, down, left or right on screen that count as dead on.")]
    [SerializeField, Range(0f, 22f)] private float assistScreenCore = 5f;
    [Tooltip("Degrees either side of a screen axis where the pull fades out.")]
    [SerializeField, Range(0f, 30f)] private float assistScreenEdge = 9f;

    // SNEAKING (break-ins chunk B, 30 Sept 2026: claude/break-ins-spec.md §6 and §8)
    //
    // At night Ace can sneak: held Ctrl or C on the keyboard; on a controller the left stick's click
    // switches it on and off (holding a stick down while steering with it is a cramp, so the pad
    // toggles). Sneaking is 1.6 m/s instead of 5, crouched: the body lowers (AceBody plays the
    // crouch clips) and the first-person eye drops, but the capsule stays exactly as it is (call 1),
    // so nothing has to check headroom when Ace stands up. Steps are quieter and heard within 1 m
    // instead of 4 (AceFootsteps, NightNoise). By day it's off: C switches hands in the café.
    [Header("Sneaking (at night)")]
    [Tooltip("Sneaking speed, m/s (the break-ins spec: 1.6; walking is Move Speed).")]
    [SerializeField, Min(.2f)] private float sneakSpeed = 1.6f;
    [Tooltip("Seconds to crouch down, or to stand back up.")]
    [SerializeField, Range(.05f, 1f)] private float crouchSeconds = .25f;
    [Tooltip("How far the first-person eye drops when crouched, metres.")]
    [SerializeField, Range(0f, 1f)] private float crouchEyeDrop = .55f;
    [Tooltip("Sneaking by day too (the café). Off: only at night (by day C switches hands).")]
    [SerializeField] private bool sneakByDay;

    /// <summary>Ace is sneaking (held Ctrl or C, or the pad's toggle), at night.</summary>
    public bool Sneaking { get; private set; }
    /// <summary>How crouched Ace is, 0 standing to 1 crouched (eased over Crouch Seconds). AceBody and the eye follow it.</summary>
    public float Crouch { get; private set; }
    /// <summary>How far the first-person eye is lowered right now, metres.</summary>
    public float EyeDrop => Crouch * crouchEyeDrop;
    /// <summary>The speed Ace walks at right now, m/s: Move Speed standing, Sneak Speed crouched, eased with the crouch.</summary>
    public float TopSpeed => Mathf.Lerp(moveSpeed, sneakSpeed, Crouch);
    /// <summary>Diagnostics: while set, it stands in for the sneak key (the checks sneak on their own).</summary>
    public bool? ScriptedSneak { get; set; }
    /// <summary>The settings, for checks: walking and sneaking speed (m/s), and how far the eye drops crouched (m).</summary>
    public float WalkSpeed => moveSpeed;
    public float SneakSpeed => sneakSpeed;
    public float CrouchEyeDrop => crouchEyeDrop;
    bool padSneak;

    /// <summary>The player's setting, kept between sessions; the Inspector's value is the default.</summary>
    public const string AssistPrefKey = "FixitFidget.MovementAssist";
    bool? assistSetting;
    public bool MovementAssist
    {
        get
        {
            assistSetting ??= PlayerPrefs.HasKey(AssistPrefKey) ? PlayerPrefs.GetInt(AssistPrefKey) == 1 : movementAssist;
            return assistSetting.Value;
        }
        set
        {
            assistSetting = value;
            PlayerPrefs.SetInt(AssistPrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    /// <summary>Degrees the assist turned this frame's walk by (0 when off, free, or standing still). For checks.</summary>
    public float AssistApplied { get; private set; }

    // ---- held still by a short scene (Barks: Night 0's deal) ----
    // Walking stops (the camera stays free) while anything holds Ace; E belongs to the scene meanwhile
    // (PlayerInteractor stands aside). Static, so a scene needn't find the player.
    static readonly HashSet<object> holds = new HashSet<object>();

    /// <summary>Hold Ace still until <see cref="Release"/> with the same owner (a scene, a check).</summary>
    public static void Hold(object owner) { if (owner != null) holds.Add(owner); }
    public static void Release(object owner) { if (owner != null) holds.Remove(owner); }
    /// <summary>Something holds Ace still (a scene is playing).</summary>
    public static bool Held => holds.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ForgetHolds() => holds.Clear();

    private CharacterController controller;
    private Vector2 moveInput;

    // Diagnostics only (the café lab walks Ace along the aisle for footage and
    // stress runs). While set, it stands in for the stick/WASD input and goes
    // through exactly the same path as a real key press, so nothing is skipped.
    public Vector2? ScriptedInput { get; set; }
    private Vector2 walkInput;
    private ConversationController conversation;
    private CafeViewMode viewMode;

    // The horizontal velocity handed to the CharacterController this frame.
    // Read by the walking-feel check to separate "what we asked for" from
    // "what the capsule actually did" (collisions, stalls, pops).
    public Vector3 CommandedVelocity { get; private set; }

    private void Awake()
    {
        // Runs once when the object wakes up. Grab a reference
        // to the CharacterController sitting on this same GameObject.
        controller = GetComponent<CharacterController>();
        conversation = GetComponent<ConversationController>();
        viewMode = GetComponent<CafeViewMode>();

        // MIN MOVE DISTANCE MUST BE ZERO.
        //
        // The controller silently ignores any Move() shorter than this. Unity's
        // default is 1 mm, which sounds harmless — but this script moves every
        // frame, and frames are short. At ~250 fps the gravity SimpleMove adds
        // while standing still is about 0.15 mm, so it was being thrown away:
        // the capsule stopped counting as grounded, then did a small catch-up
        // drop every few frames. Starting to walk during one of those
        // "ungrounded" frames lost the first ~20 ms of movement, and then the
        // view lurched to full speed. Measured on 23 Sept with
        // Fixit Fidget > Checks > Walking feel. Unity's own guidance is to
        // leave this at 0, so it is enforced here rather than trusted to every
        // scene's Inspector.
        if (controller != null) controller.minMoveDistance = 0f;
    }

    // Called automatically by the Player Input component whenever
    // the "Move" action fires (WASD, stick, d-pad — doesn't matter).
    // The left stick is also read directly in Update (see PadInput).
    // The name matters: "On" + the action's name.
    private void OnMove(InputValue value)
    {
        // The recap stops Ace; the night after it (the Night 1 slice) doesn't.
        moveInput = DayClock.Instance != null && DayClock.Instance.RecapOwnsInput
            ? Vector2.zero : value.Get<Vector2>();
    }

    // Stations disable this component. Coming back, walking starts from rest
    // rather than resuming whatever was held when you stepped up to the bench.
    private void OnDisable() => ClearInput();

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ClearInput();
    }

    public void ClearInput()
    {
        moveInput = Vector2.zero;
        walkInput = Vector2.zero;
        CommandedVelocity = Vector3.zero;
    }

    // Sneaking: at night only (unless Sneak By Day), held Ctrl or C, or the pad's toggle, or a check's
    // ScriptedSneak. Crouch eases toward it, and the speed follows the crouch (see TopSpeed).
    private void UpdateSneak()
    {
        NightWalk night = NightWalk.Instance;
        bool allowed = sneakByDay || night != null && night.Active;
        if (!allowed)
        {
            padSneak = false;
            Sneaking = false;
        }
        else
        {
            if (PadInput.Pressed(PadButton.LeftStickPress)) padSneak = !padSneak;
            Keyboard keys = Keyboard.current;
            bool held = keys != null && Application.isFocused
                && (keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed || keys.cKey.isPressed);
            Sneaking = ScriptedSneak ?? (held || padSneak);
        }
        Crouch = Mathf.MoveTowards(Crouch, Sneaking ? 1f : 0f, Time.deltaTime / Mathf.Max(.01f, crouchSeconds));
    }

    private void Update()
    {
        UpdateSneak();
        if (Time.timeScale <= 0 || (DayClock.Instance != null && DayClock.Instance.RecapOwnsInput)
            || (conversation != null && conversation.InConversation))
        {
            ClearInput();
            return;
        }

        if (Held)
        {
            // A scene holds Ace: stand still (gravity still applies), and start from rest after.
            ClearInput();
            if (controller != null && controller.enabled) controller.SimpleMove(Vector3.zero);
            return;
        }

        if (viewMode != null && viewMode.SuppressWalkingMovement)
        {
            // Esc released the cursor: stand still, and start from rest after.
            walkInput = Vector2.zero;
            CommandedVelocity = Vector3.zero;
            return;
        }

        // A controller's left stick is read directly as well as through
        // PlayerInput's Move action. PlayerInput only listens to the device of
        // the control scheme it last switched to, and walking must never wait
        // for that switch. The larger of the two wins, so nothing doubles.
        Vector2 stick = PadInput.LeftStick;
        Vector2 input = ScriptedInput ?? (stick.sqrMagnitude > moveInput.sqrMagnitude ? stick : moveInput);

        // A stick can report slightly more than 1 on its diagonals.
        Vector2 target = Vector2.ClampMagnitude(input, 1f);

        if (viewMode != null && viewMode.WalkingFirstPerson)
        {
            // Rates are authored in m/s² but applied to input (0–1), so divide
            // by top speed. MoveTowards keeps it frame-rate independent: the same
            // corner feels the same at 60 fps and at 240.
            bool held = target.sqrMagnitude > 0.0001f;
            float rate = (held ? firstPersonAcceleration : firstPersonBraking) / Mathf.Max(TopSpeed, 0.01f);
            walkInput = Vector2.MoveTowards(walkInput, target, rate * Time.deltaTime);
        }
        else
        {
            walkInput = target;
        }

        // Existing scenes keep their 45-degree controls. The optional walking
        // camera supplies its yaw so orbiting never reverses screen movement.
        float cameraYaw = viewMode != null && viewMode.isActiveAndEnabled ? viewMode.MovementYaw : 45;
        Vector3 move = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(walkInput.x, 0f, walkInput.y);

        AssistApplied = 0f;
        bool overhead = viewMode == null || !viewMode.WalkingFirstPerson;
        if (overhead && ScriptedInput == null && MovementAssist) move = Assisted(move, cameraYaw);

        CommandedVelocity = move * TopSpeed;
        controller.SimpleMove(CommandedVelocity);
    }

    // The world direction pulled onto the nearest room axis or screen axis (see the note above). The
    // magnitude is kept: the assist turns the walk, it never slows it.
    Vector3 Assisted(Vector3 move, float cameraYaw)
    {
        float length = move.magnitude;
        if (length < .0001f) return move;
        float heading = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;         // world yaw of the walk
        // The nearest line of each family, and how far off it the stick is (signed).
        float wall = NearestLine(heading, 0f), wallError = Mathf.DeltaAngle(wall, heading);
        float screen = NearestLine(heading, cameraYaw), screenError = Mathf.DeltaAngle(screen, heading);
        // Whichever the stick is closer to, measured against that family's own band.
        float wallEdge = Mathf.Max(assistAxisCore, assistAxisEdge), screenEdge = Mathf.Max(assistScreenCore, assistScreenEdge);
        bool useWall = Mathf.Abs(wallError) / Mathf.Max(.01f, wallEdge) <= Mathf.Abs(screenError) / Mathf.Max(.01f, screenEdge);
        float line = useWall ? wall : screen, error = useWall ? wallError : screenError;
        float core = useWall ? assistAxisCore : assistScreenCore, edge = useWall ? wallEdge : screenEdge;
        float off = Mathf.Abs(error);
        if (off >= edge) return move;
        // Dead on inside the core; between core and edge the line lets go smoothly (no jump at the edge).
        float keep = edge > core ? Mathf.SmoothStep(0f, 1f, (off - core) / (edge - core)) : 0f;
        float shaped = line + error * keep;
        AssistApplied = Mathf.DeltaAngle(heading, shaped);
        float rad = shaped * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * length;
    }

    /// <summary>The line nearest <paramref name="heading"/> among <paramref name="offset"/> and its right angles (degrees).</summary>
    public static float NearestLine(float heading, float offset) => offset + Mathf.Round(Mathf.DeltaAngle(offset, heading) / 90f) * 90f;

    /// <summary>The assist's bands, for checks: room-axis core and edge, screen-axis core and edge (degrees).</summary>
    public Vector4 AssistBands => new Vector4(assistAxisCore, Mathf.Max(assistAxisCore, assistAxisEdge), assistScreenCore, Mathf.Max(assistScreenCore, assistScreenEdge));
}
