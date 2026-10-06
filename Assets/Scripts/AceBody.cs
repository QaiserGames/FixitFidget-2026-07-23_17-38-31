using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// ---------------------------------------------------------------------------
// ACE'S BODY (29 Sept 2026: claude/break-ins-spec.md section 8, calls 1, 6 and d, and the Sidekick Ace)
//
// Ace's own body is a Synty Sidekick character: "Ace", made in the Sidekick Character Creator (for now
// Mansoor's placeholder from the free Starter Pack; everyday clothes come with Modern Civilians). It plays
// the free animation library's idle, walk and run (Quaternius, CC0), re-imported as Humanoid so they fit
// this body, and its face lives (AceFace: blinks and small glances). Fixit Fidget > Night > Ace's body 4
// sets it up. Where the Sidekick files or those clips are missing, Ace wears the stand-in body instead:
// look 11, the green jacket (POLYGON City's Character_Male_Jacket, one of the walk-ins' looks, which
// leaves their pool while Ace wears it so Ace never meets a double; with the Sidekick body it goes back).
// Day and night since Mansoor's call on 29 Sept (By Day below); with By Day off, only at night and in the
// café lab (Fixit Fidget > Night > Ace's body 2 - Try it in the café). By day the cups and hands Ace
// carries still sit where the capsule held them (a known rough edge).
//
// How the stand-in moves: the way every café person is drawn. A hidden café rig (Quaternius Beach, CC0)
// plays its idle, walk or run, and the city look copies the rig bone by bone (PolygonNpcVisual). The
// Sidekick body plays its Humanoid clips on its own Animator. Either way the three clips are blended by
// how fast Ace really moves, each played at the rate that keeps its feet planted, with one stride phase
// shared by the walk and the run. The body stands on the floor under Ace (a ray down from the capsule's
// middle) and turns to face where Ace is going; it keeps that heading when Ace stops.
//
// Turning round (5 Oct 2026, Mansoor's report "he runs in a diagonal animation when I move the stick from up
// to down", and his call): the capsule reverses in a single frame, so the body used to swing round at a flat
// 720°/s with the run still playing, and for about 0.15 s after a quick flick Ace ran side-on. Now the body
// turns faster the further it has left to turn (Quick Turn: a full about-face in about 0.13 s, small steering
// as before), and while it is still well round (Pivot Angles) the legs leave the run for a quick step (Pivot
// Gait), so Ace never runs sideways. The capsule, and so the controls, are unchanged. Fixit Fidget > Night >
// Ace's body 6 measures it (AceTurnCheck), before and after.
//
// Sneaking (break-ins chunk B, 30 Sept): as Ace crouches (PlayerMovement.Crouch), the Sidekick body blends
// into the library's crouch clips (Crouch_Idle_Loop and Crouch_Fwd_Loop, Humanoid), the crouch walk at the
// rate that keeps its feet planted. Fixit Fidget > Night > Ace's body 5 fits them. The stand-in has no
// crouch clips, so it sneaks upright, just slowly.
//
// Only what you see changes. The collider is still the 1.0 m capsule (call 1), so the body stops
// about a third of a metre short of walls; walking, interacting and the cameras are untouched. The
// capsule's own mesh hides while the body is drawn; in first person and at a station the body hides,
// as the capsule does. Without the purchased art (a fresh clone of the public repository, which never
// has Synty files) or the clips, nothing changes: Ace stays the capsule.
//
// Close-ups (6 Oct 2026): a conversation at the counter, an item in hand and the counter's repair view are
// seen from where Ace stands, so the body hides in them too, and stays hidden while the camera pulls back
// out of one until it is a few metres off (Clear Of Camera). Since the body came (29 Sept), the
// conversation's camera behind the counter had been looking through the back of Ace's shirt.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent, DefaultExecutionOrder(120)]
public sealed class AceBody : MonoBehaviour
{
    /// <summary>Set by the editor for one Play session: wear the body by day too (the café lab).</summary>
    public const string LabKey = "FixitFidget.AceBody.CafeLab";

    [Header("Ace's own body, a Sidekick character: set by Fixit Fidget > Night > Ace's body 4")]
    [Tooltip("Ace, exported by the Sidekick Character Creator (Export Character as FBX: a Humanoid prefab). Missing: the stand-in body below.")]
    public GameObject sidekick;
    [Tooltip("Humanoid copies of the animation library's idle, walk and run (Assets/ThirdParty/Quaternius_UAL/Humanoid).")]
    public AnimationClip sidekickIdle, sidekickWalk, sidekickRun;
    [Tooltip("The Sidekick body's size (1 = as exported). The set-up step matches the café people's height.")]
    [Min(.1f)] public float sidekickScale = 1f;
    [Tooltip("Each clip's natural ground speed on the Sidekick body at scale 1, m/s: how fast its stance foot slides back.")]
    [Min(.1f)] public float sidekickWalkSpeed = 1.3f, sidekickRunSpeed = 3.5f;
    [Tooltip("Where in each clip (0-1) the left foot is furthest forward, so the walk and the run keep step while they blend.")]
    [Range(0f, 1f)] public float sidekickWalkLeftForward, sidekickRunLeftForward;
    [Tooltip("The Sidekick body's height standing, metres, as measured by the set-up step (for the reports).")]
    [Min(0f)] public float sidekickHeight;
    [Tooltip("Humanoid foot IK: the feet go where the clips put them (less sliding once the clips are fitted to this body).")]
    public bool sidekickFootIK = true;
    [Tooltip("The face lives: blinks and small glances (AceFace). Off: the eyes stay open and still; the jaw is held shut either way.")]
    public bool sidekickFace = true;

    [Header("Sneaking, the crouch (break-ins chunk B): set by Fixit Fidget > Night > Ace's body 5")]
    [Tooltip("Humanoid crouch clips for the Sidekick body: crouched and still, and walking crouched (Crouch_Idle_Loop, Crouch_Fwd_Loop from the Humanoid copy). Missing: sneaking is slow but upright.")]
    public AnimationClip sidekickCrouchIdle, sidekickCrouchWalk;
    [Tooltip("The crouch walk's natural ground speed on the Sidekick body at scale 1, m/s.")]
    [Min(.1f)] public float sidekickCrouchSpeed = 1f;
    [Tooltip("Where in the crouch walk (0-1) the left foot is furthest forward.")]
    [Range(0f, 1f)] public float sidekickCrouchLeftForward;
    [Tooltip("The Sidekick body's height crouched, metres, as measured by the set-up step. Grace's eyes aim at it (chunk C).")]
    [Min(0f)] public float sidekickCrouchHeight;

    [Header("The stand-in body (look 11), also the fallback: set by Fixit Fidget > Night > Ace's body 1")]
    [Tooltip("The city look (a prefab in Assets/Art/CityNeighbors/Prefabs). Missing, with no Sidekick body: Ace stays the capsule.")]
    public GameObject look;
    [Tooltip("The café rig the look copies (Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx).")]
    public GameObject rig;
    [Tooltip("The rig's size. The café's people are 1.1.")]
    [Min(.1f)] public float rigScale = 1.1f;
    public AnimationClip idleClip, walkClip, runClip;
    [Tooltip("Each clip's natural ground speed on the rig at scale 1, m/s: how fast its stance foot slides back.")]
    [Min(.1f)] public float walkClipSpeed = 1.32f, runClipSpeed = 3.08f;
    [Tooltip("Where in each clip (0-1) the left foot is furthest forward, so the walk and the run keep step while they blend.")]
    [Range(0f, 1f)] public float walkLeftForward, runLeftForward = .38f;
    [Tooltip("The body's height standing, metres, as measured by the set-up step (for the reports).")]
    [Min(0f)] public float standingHeight;

    [Header("When")]
    [Tooltip("Wear the body by day too (the café). Off: only at night, and by day in the café lab.")]
    public bool byDay = true;

    [Header("Feel")]
    [Tooltip("Degrees a second the body turns toward where Ace is going, at least (Quick Turn makes big turns faster).")]
    [Range(90f, 1440f)] public float turnSpeed = 720f;
    [Tooltip("The further the body has left to turn, the faster it turns: each second it closes this many times the gap " +
             "(20: half of it in 0.035 s), never slower than Turn Speed. So a full about-face takes about 0.13 s, and small " +
             "steering stays at Turn Speed. 0: always Turn Speed (before 5 Oct 2026).")]
    [Range(0f, 40f)] public float quickTurn = 20f;
    [Tooltip("While the body swings round, the legs leave the run for a quick step, so Ace never runs sideways. Off: the run " +
             "plays through the swing (before 5 Oct 2026).")]
    public bool pivotStep = true;
    [Tooltip("Degrees between where the body faces and where Ace is going: past the first the legs start leaving the run, " +
             "past the second they only step. (25, 55): the run never shows more than about 36° off.")]
    public Vector2 pivotAngles = new Vector2(25f, 55f);
    [Tooltip("How fast the legs go while they step round, m/s: a brisk walk, under where the run starts (Run Blend).")]
    [Min(0f)] public float pivotGait = 1.2f;
    [Tooltip("Seconds over which the gait follows Ace's speed (idle, walk, run).")]
    [Range(.02f, .5f)] public float gaitSmoothing = .08f;
    [Tooltip("Speeds, m/s: from standing to walking.")]
    public Vector2 walkBlend = new Vector2(.15f, .6f);
    [Tooltip("Speeds, m/s: from walking to running.")]
    public Vector2 runBlend = new Vector2(2.2f, 3.4f);
    [Tooltip("Speeds, m/s: crouched, from still to walking.")]
    public Vector2 crouchBlend = new Vector2(.1f, .45f);
    [Tooltip("Seconds the feet take to follow the floor's height (a step up, the stairs).")]
    [Range(0f, .3f)] public float feetSmoothing = .05f;

    /// <summary>The body is on (by day and night, or as By Day says).</summary>
    public bool Worn { get; private set; }
    /// <summary>The body is on and drawn (not first person, not at a station, not in a close-up).</summary>
    public bool Drawn { get; private set; }
    /// <summary>The body on is the Sidekick Ace (else the stand-in, look 11).</summary>
    public bool WearsSidekick { get; private set; }
    /// <summary>Ace's horizontal speed as the gait sees it, m/s (smoothed).</summary>
    public float Speed => speed;
    /// <summary>How much of each standing clip is showing: idle, walk, run (they add up to 1 standing, less as Ace crouches).</summary>
    public Vector3 Gait => new Vector3(wIdle, wWalk, wRun);
    /// <summary>How much of the crouch clips is showing: crouched and still, crouch walking (they add up to how crouched Ace is).</summary>
    public Vector2 CrouchGait => new Vector2(wCrouchIdle, wCrouchWalk);
    /// <summary>The body on can crouch (the Sidekick body with its crouch clips; the stand-in can't).</summary>
    public bool CanCrouch => crouchIdleNow != null && crouchWalkNow != null;
    /// <summary>The crouch walk's playback rate right now (1 = as made).</summary>
    public float CrouchRate { get; private set; } = 1f;
    /// <summary>The body's height crouched, metres (measured by the set-up step; else about two thirds of standing).</summary>
    public float CrouchedHeight => WearsSidekick && sidekickCrouchHeight > .3f ? sidekickCrouchHeight : Height * .66f;
    /// <summary>The run clip's playback rate right now (1 = as made).</summary>
    public float RunRate { get; private set; } = 1f;
    /// <summary>While Ace moves: degrees between where the body faces and where Ace is going (0 standing).</summary>
    public float FacingError { get; private set; }
    /// <summary>How far the legs have left the run to step round while the body swings (0-1; see Pivot Angles).</summary>
    public float Pivot { get; private set; }
    /// <summary>The speed the legs move at, m/s: Speed, held down to Pivot Gait while the body swings round.</summary>
    public float GaitSpeed { get; private set; }
    /// <summary>The way the body faces, degrees (world yaw; the rig root's).</summary>
    public float BodyYaw => bodyYaw;
    /// <summary>Where the walk and the run are in their shared stride (0-1), for checks.</summary>
    public float StridePhase => phase;
    /// <summary>The floor was found under Ace this frame (else the feet are put at the capsule's bottom).</summary>
    public bool OnFloor { get; private set; }
    /// <summary>The body's height standing, metres (measured when it is put on).</summary>
    public float Height { get; private set; }
    /// <summary>The stand-in look's name (look 11).</summary>
    public string LookName => look != null ? look.name : "";
    /// <summary>Which body is on, for the reports: the Sidekick's name, or the stand-in look's.</summary>
    public string BodyName => WearsSidekick && sidekick != null ? sidekick.name + " (Sidekick)" : LookName;
    public Transform RigRoot => rigRoot;
    /// <summary>The stand-in's city look while the stand-in is on (null with the Sidekick body).</summary>
    public PolygonNpcVisual Visual => visual;
    /// <summary>The Sidekick face while the Sidekick body is on.</summary>
    public AceFace Face => face;

    static bool labRequest;
    CafeViewMode view;
    CharacterController capsule;
    PlayerMovement movement;
    Transform rigRoot;
    PolygonNpcVisual visual;
    Renderer[] ownRenderers;
    AceFace face;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable idle, walk, run, crouchIdle, crouchWalk;
    GameObject drawnInstance;
    bool drawnSet, sidekickFailed, standInFailed;
    // The clips of the body that's on, and their numbers (no crouch clips on the stand-in).
    AnimationClip idleNow, walkNow, runNow, crouchIdleNow, crouchWalkNow;
    float walkSpeedNow, runSpeedNow, walkLeftNow, runLeftNow, crouchSpeedNow, crouchLeftNow;
    Vector3 lastPosition;
    float speed, wIdle = 1f, wWalk, wRun, wCrouchIdle, wCrouchWalk, idleTime, phase, crouchIdleTime, crouchPhase, bodyYaw, feetY, feetVelocity;
    bool placed;
    // A scene turns Ace, standing still, to face whoever speaks (FaceToward); walking off cancels it.
    float faceYaw;
    bool facing;

    /// <summary>
    /// While Ace stands still, turn the body to face <paramref name="point"/> (the man at the bins, as a scene begins);
    /// with <paramref name="snap"/>, at once. Ace walking off cancels it; in first person the view decides, as ever.
    /// </summary>
    public void FaceToward(Vector3 point, bool snap = false)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < .01f) return;
        faceYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        facing = !snap;
        // At once (Ace put somewhere behind a fade: through a door).
        if (snap) bodyYaw = faceYaw;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReadLabRequest()
    {
        labRequest = false;
#if UNITY_EDITOR
        if (PlayerPrefs.GetInt(LabKey, 0) == 1)
        {
            labRequest = true;
            PlayerPrefs.DeleteKey(LabKey);
            PlayerPrefs.Save();
        }
#endif
    }

    void Awake()
    {
        view = GetComponent<CafeViewMode>();
        capsule = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
    }

    // Look 11 leaves the walk-ins' pool while Ace wears it, by day too; with the Sidekick body it stays theirs.
    void OnEnable()
    {
        if (!SidekickReady) Reserve();
    }

    void OnDisable()
    {
        CustomerProfile.ReleaseStandInLook(this);
        TakeOff();
    }

    void OnDestroy() => TakeOff();

    void Reserve()
    {
        if (look != null) CustomerProfile.ReserveStandInLook(this, look.name);
    }

    bool SidekickReady => !sidekickFailed && sidekick != null && sidekickIdle != null && sidekickWalk != null && sidekickRun != null;
    bool StandInReady => !standInFailed && look != null && rig != null && idleClip != null && walkClip != null && runClip != null;

    bool Wanted
    {
        get
        {
            if (!Application.isPlaying || !SidekickReady && !StandInReady) return false;
            NightWalk night = NightWalk.Instance;
            return byDay || night != null && night.Active || labRequest && CafeLab.Active;
        }
    }

    void Update()
    {
        bool wanted = Wanted;
        if (wanted && !Worn) PutOn();
        else if (!wanted && Worn) TakeOff();
        if (Worn) Animate(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (!Worn) return;
        Place(Time.deltaTime);
        Draw(Shown());
    }

    // ------------------------------------------------------------------ on and off

    void PutOn()
    {
        if (SidekickReady && PutOnSidekick()) return;
        if (StandInReady) PutOnStandIn();
    }

    // Ace's own body: the Sidekick prefab, its own Humanoid Animator playing the Humanoid clips.
    bool PutOnSidekick()
    {
        GameObject body = Instantiate(sidekick, transform);
        body.name = "Ace's body (" + sidekick.name + ", Sidekick)";
        Animator animator = body.GetComponentInChildren<Animator>(true);
        Avatar avatar = animator != null ? animator.avatar : null;
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogWarning("[Ace's body] " + sidekick.name + " has no working Humanoid avatar, so Ace wears the stand-in body instead.");
            body.SetActive(false);
            Destroy(body);
            sidekickFailed = true;
            Reserve();
            return false;
        }
        rigRoot = body.transform;
        // The size the set-up step chose (the café people's height), whatever Ace's own scale.
        rigRoot.localScale = Vector3.one * (sidekickScale / Mathf.Max(.01f, transform.lossyScale.y));
        // Nothing on the body takes part in the game: it is drawn and nothing else.
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        idleNow = sidekickIdle; walkNow = sidekickWalk; runNow = sidekickRun;
        walkSpeedNow = sidekickWalkSpeed; runSpeedNow = sidekickRunSpeed;
        walkLeftNow = sidekickWalkLeftForward; runLeftNow = sidekickRunLeftForward;
        // The crouch, both clips or neither (Ace's body 5 sets them).
        bool crouch = sidekickCrouchIdle != null && sidekickCrouchWalk != null;
        crouchIdleNow = crouch ? sidekickCrouchIdle : null;
        crouchWalkNow = crouch ? sidekickCrouchWalk : null;
        crouchSpeedNow = sidekickCrouchSpeed;
        crouchLeftNow = sidekickCrouchLeftForward;
        BuildGraph(animator, sidekickFootIK);

        ownRenderers = body.GetComponentsInChildren<Renderer>(true);
        // The face: blinks and glances, and in any case the jaw held shut (the clips have no jaw; see AceFace).
        face = body.AddComponent<AceFace>();
        face.lives = sidekickFace;
        if (!face.Bind(rigRoot, animator))
        {
            Destroy(face);
            face = null;
        }
        WearsSidekick = true;
        Height = sidekickHeight > 0f ? sidekickHeight : Tallness(ownRenderers);
        Wear();
        return true;
    }

    // The stand-in: a hidden café rig plays the café clips; look 11 copies it bone by bone.
    void PutOnStandIn()
    {
        GameObject body = Instantiate(rig, transform);
        body.name = "Ace's body (the café rig, hidden; the city look copies it)";
        rigRoot = body.transform;
        // The café people's size in the world, whatever Ace's own scale.
        rigRoot.localScale = Vector3.one * (rigScale / Mathf.Max(.01f, transform.lossyScale.y));
        // Nothing on the body takes part in the game: it is drawn and nothing else.
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
        Animator animator = body.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        idleNow = idleClip; walkNow = walkClip; runNow = runClip;
        walkSpeedNow = walkClipSpeed; runSpeedNow = runClipSpeed;
        walkLeftNow = walkLeftForward; runLeftNow = runLeftForward;
        crouchIdleNow = crouchWalkNow = null;   // the café rig has no crouch: the stand-in sneaks upright
        BuildGraph(animator, false);

        visual = body.AddComponent<PolygonNpcVisual>();
        visual.EveryFrame = true;   // Ace is always posed: the café people take turns at high frame rates, Ace never
        visual.Configure(new[] { look }, 0f, 0, 0f);
        if (!visual.ApplyAppearance(0))
        {
            Debug.LogWarning("[Ace's body] The look " + look.name + " couldn't copy the café rig; Ace stays the capsule.");
            standInFailed = true;
            TakeOff();
            return;
        }
        Reserve();   // already kept unless the Sidekick body was meant to be on
        WearsSidekick = false;
        Height = standingHeight > 0f ? standingHeight
            : Tallness(visual.VisualInstance != null ? visual.VisualInstance.GetComponentsInChildren<Renderer>(true) : null);
        Wear();
    }

    void BuildGraph(Animator animator, bool footIK)
    {
        graph = PlayableGraph.Create("Ace's body");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, CanCrouch ? 5 : 3);
        idle = Clip(idleNow, 0, footIK);
        walk = Clip(walkNow, 1, footIK);
        run = Clip(runNow, 2, footIK);
        if (CanCrouch)
        {
            crouchIdle = Clip(crouchIdleNow, 3, footIK);
            crouchWalk = Clip(crouchWalkNow, 4, footIK);
        }
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Ace's body", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();
    }

    AnimationClipPlayable Clip(AnimationClip clip, int port, bool footIK)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(footIK);
        playable.SetSpeed(0);   // their times are set by hand each frame (the café run doesn't loop by itself)
        graph.Connect(playable, 0, mixer, port);
        return playable;
    }

    // Both bodies from here on: stand where Ace is, facing Ace's way, drawn or hidden as the capsule would be.
    void Wear()
    {
        lastPosition = transform.position;
        speed = 0f;
        wIdle = 1f;
        wWalk = wRun = wCrouchIdle = wCrouchWalk = 0f;
        bodyYaw = transform.eulerAngles.y;
        placed = false;
        drawnInstance = null;
        drawnSet = false;
        Worn = true;
        if (view != null) view.BodyStandsIn = true;
        Place(0f);
        afterCloseUp = false;
        Draw(Shown());
    }

    void TakeOff()
    {
        if (graph.IsValid()) graph.Destroy();
        if (rigRoot != null)
        {
            // PolygonNpcVisual takes its city look with it; the Sidekick body takes its face.
            if (Application.isPlaying) Destroy(rigRoot.gameObject); else DestroyImmediate(rigRoot.gameObject);
        }
        rigRoot = null;
        visual = null;
        ownRenderers = null;
        face = null;
        crouchIdleNow = crouchWalkNow = null;
        drawnInstance = null;
        drawnSet = false;
        bool wasWorn = Worn;
        Worn = Drawn = WearsSidekick = false;
        if (wasWorn && view != null) view.BodyStandsIn = false;
    }

    static float Tallness(Renderer[] renderers)
    {
        if (renderers == null) return 0f;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? b.size.y : 0f;
    }

    // ------------------------------------------------------------------ the gait

    void Animate(float dt)
    {
        Vector3 now = transform.position;
        Vector3 moved = now - lastPosition;
        lastPosition = now;
        moved.y = 0f;
        float distance = moved.magnitude;
        float raw = dt > 1e-5f ? distance / dt : 0f;
        if (distance > 3f) raw = 0f;   // a lab putting Ace somewhere, not a step
        if (dt > 0f) speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-dt / gaitSmoothing));

        // Facing: toward where Ace is going; in first person, where Ace looks.
        bool firstPerson = view != null && view.FirstPersonSelected;
        if (firstPerson) bodyYaw = transform.eulerAngles.y;
        if (raw > .3f && distance <= 3f && !firstPerson)
        {
            facing = false;
            float toward = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
            // At least Turn Speed; and the further there is to go, the faster: Quick Turn times the gap a second, as
            // an exponential, so a turn takes the same time at 60 fps as at 240.
            float left = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, toward));
            float step = Mathf.Max(turnSpeed * dt, left * (1f - Mathf.Exp(-quickTurn * dt)));
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, toward, step);
            FacingError = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, toward));
        }
        else
        {
            FacingError = 0f;
            // Turning to face someone, standing: unhurried, like a person who has just been spoken to.
            if (facing && !firstPerson)
            {
                float left = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, faceYaw));
                float step = Mathf.Max(turnSpeed * .5f * dt, left * (1f - Mathf.Exp(-8f * dt)));
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, faceYaw, step);
                if (left <= step) facing = false;
            }
        }

        // The legs go at Ace's speed; but while the body is still swinging round (a stick flicked from up to down:
        // the capsule has already turned back), they leave the run for a quick step, so Ace never runs sideways.
        Pivot = pivotStep ? Smooth(Mathf.InverseLerp(pivotAngles.x, Mathf.Max(pivotAngles.x + 1f, pivotAngles.y), FacingError)) : 0f;
        float gait = Mathf.Lerp(speed, Mathf.Min(speed, pivotGait), Pivot);
        GaitSpeed = gait;

        // Which clips: standing, walking, running; and, as Ace crouches (sneaking), crouched and still or
        // crouch walking instead. The stand-in has no crouch clips, so it sneaks upright.
        float crouched = CanCrouch && movement != null ? movement.Crouch : 0f;
        float moving = Smooth(Mathf.InverseLerp(walkBlend.x, walkBlend.y, gait));
        float running = Smooth(Mathf.InverseLerp(runBlend.x, runBlend.y, gait));
        float stalking = Smooth(Mathf.InverseLerp(crouchBlend.x, crouchBlend.y, gait));
        float upright = 1f - crouched;
        wIdle = upright * (1f - moving);
        wWalk = upright * moving * (1f - running);
        wRun = upright * moving * running;
        wCrouchIdle = crouched * (1f - stalking);
        wCrouchWalk = crouched * stalking;

        // One stride phase for both, so the feet keep step while they blend: each clip at the rate that
        // keeps its stance foot planted at this speed.
        float scale = rigRoot != null ? rigRoot.lossyScale.y : 1f;
        float walkRate = Mathf.Clamp(gait / Mathf.Max(.1f, walkSpeedNow * scale), .6f, 1.8f);
        RunRate = Mathf.Clamp(gait / Mathf.Max(.1f, runSpeedNow * scale), .7f, 1.6f);
        float stepping = wWalk + wRun;
        float cycles = stepping > 1e-3f
            ? (wWalk * walkRate / walkNow.length + wRun * RunRate / runNow.length) / stepping
            : walkRate / walkNow.length;
        phase = Mathf.Repeat(phase + dt * cycles, 1f);
        idleTime = Mathf.Repeat(idleTime + dt, idleNow.length);

        idle.SetTime(idleTime);
        walk.SetTime(Mathf.Repeat(phase + walkLeftNow, 1f) * walkNow.length);
        run.SetTime(Mathf.Repeat(phase + runLeftNow, 1f) * runNow.length);
        mixer.SetInputWeight(0, wIdle);
        mixer.SetInputWeight(1, wWalk);
        mixer.SetInputWeight(2, wRun);
        if (!CanCrouch) return;

        // The crouch walk keeps its own stride phase, at the rate that keeps its stance foot planted.
        CrouchRate = Mathf.Clamp(gait / Mathf.Max(.1f, crouchSpeedNow * scale), .5f, 2f);
        crouchPhase = Mathf.Repeat(crouchPhase + dt * CrouchRate / crouchWalkNow.length, 1f);
        crouchIdleTime = Mathf.Repeat(crouchIdleTime + dt, crouchIdleNow.length);
        crouchIdle.SetTime(crouchIdleTime);
        crouchWalk.SetTime(Mathf.Repeat(crouchPhase + crouchLeftNow, 1f) * crouchWalkNow.length);
        mixer.SetInputWeight(3, wCrouchIdle);
        mixer.SetInputWeight(4, wCrouchWalk);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    // ------------------------------------------------------------------ where it stands

    void Place(float dt)
    {
        if (rigRoot == null) return;
        Vector3 middle = capsule != null ? transform.TransformPoint(capsule.center) : transform.position;
        float half = capsule != null ? capsule.height * .5f * transform.lossyScale.y : 1f;
        float floor = middle.y - half - (capsule != null ? capsule.skinWidth : 0f);
        // The floor under the capsule's middle (the ray starts inside Ace's own collider, which it ignores).
        OnFloor = Physics.Raycast(middle, Vector3.down, out RaycastHit hit, half + 1f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        if (OnFloor) floor = hit.point.y;
        if (!placed || dt <= 0f || feetSmoothing <= 0f || Mathf.Abs(floor - feetY) > .6f)
        {
            feetY = floor;
            feetVelocity = 0f;
            placed = true;
        }
        else feetY = Mathf.SmoothDamp(feetY, floor, ref feetVelocity, feetSmoothing, Mathf.Infinity, dt);
        rigRoot.SetPositionAndRotation(new Vector3(middle.x, feetY, middle.z), Quaternion.Euler(0f, bodyYaw, 0f));
    }

    // How far the camera has to be from the body before it is drawn again after a close-up, metres (the
    // overhead view is never nearer than 8 m, inside a house).
    const float ClearOfCamera = 3f;
    // A close-up has had the screen, and the camera may still be pulling back out of it.
    bool afterCloseUp;
    Transform viewCamera;

    // Drawn only while the overhead view is on screen: not in first person, at a station or in a close-up
    // (CafeViewMode.OverheadShown), and not until the camera is clear of the body after one.
    bool Shown()
    {
        if (view == null) return true;
        // Hidden in a cupboard or a wardrobe (GraceAtHome): set aside, and back the moment Ace comes out.
        if (view.AceSetAside) return false;
        if (!(view.isActiveAndEnabled ? view.OverheadShown : view.ShowsAce))
        {
            afterCloseUp = true;
            return false;
        }
        if (!afterCloseUp) return true;
        if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
        Vector3 middle = rigRoot != null ? rigRoot.position + Vector3.up * (Height > 0f ? Height * .6f : 1.2f) : transform.position;
        if (viewCamera != null && (viewCamera.position - middle).sqrMagnitude < ClearOfCamera * ClearOfCamera) return false;
        afterCloseUp = false;
        return true;
    }

    // Drawn, or hidden (first person, a station, a close-up): the body's own renderers (the Sidekick's, or the city look's).
    void Draw(bool show)
    {
        if (WearsSidekick)
        {
            if (!drawnSet || show != Drawn)
            {
                if (ownRenderers != null)
                    foreach (Renderer r in ownRenderers)
                        if (r != null) r.enabled = show;
                if (face != null) face.enabled = show;   // no blinking while nobody can see it
                drawnSet = true;
            }
            Drawn = show;
            return;
        }
        GameObject instance = visual != null ? visual.VisualInstance : null;
        if (instance != drawnInstance || show != Drawn)
        {
            if (instance != null)
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
            drawnInstance = instance;
        }
        Drawn = show && instance != null;
    }
}
