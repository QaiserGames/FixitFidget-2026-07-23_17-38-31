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
// Only what you see changes. The collider is still the 1.0 m capsule (call 1), so the body stops
// about a third of a metre short of walls; walking, interacting and the cameras are untouched. The
// capsule's own mesh hides while the body is drawn; in first person and at a station the body hides,
// as the capsule does. Without the purchased art (a fresh clone of the public repository, which never
// has Synty files) or the clips, nothing changes: Ace stays the capsule.
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
    [Tooltip("Degrees a second the body turns toward where Ace is going.")]
    [Range(90f, 1440f)] public float turnSpeed = 720f;
    [Tooltip("Seconds over which the gait follows Ace's speed (idle, walk, run).")]
    [Range(.02f, .5f)] public float gaitSmoothing = .08f;
    [Tooltip("Speeds, m/s: from standing to walking.")]
    public Vector2 walkBlend = new Vector2(.15f, .6f);
    [Tooltip("Speeds, m/s: from walking to running.")]
    public Vector2 runBlend = new Vector2(2.2f, 3.4f);
    [Tooltip("Seconds the feet take to follow the floor's height (a step up, the stairs).")]
    [Range(0f, .3f)] public float feetSmoothing = .05f;

    /// <summary>The body is on (by day and night, or as By Day says).</summary>
    public bool Worn { get; private set; }
    /// <summary>The body is on and drawn (not first person, not at a station).</summary>
    public bool Drawn { get; private set; }
    /// <summary>The body on is the Sidekick Ace (else the stand-in, look 11).</summary>
    public bool WearsSidekick { get; private set; }
    /// <summary>Ace's horizontal speed as the gait sees it, m/s (smoothed).</summary>
    public float Speed => speed;
    /// <summary>How much of each clip is showing: idle, walk, run (they add up to 1).</summary>
    public Vector3 Gait => new Vector3(wIdle, wWalk, wRun);
    /// <summary>The run clip's playback rate right now (1 = as made).</summary>
    public float RunRate { get; private set; } = 1f;
    /// <summary>While Ace moves: degrees between where the body faces and where Ace is going (0 standing).</summary>
    public float FacingError { get; private set; }
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
    Transform rigRoot;
    PolygonNpcVisual visual;
    Renderer[] ownRenderers;
    AceFace face;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable idle, walk, run;
    GameObject drawnInstance;
    bool drawnSet, sidekickFailed, standInFailed;
    // The clips of the body that's on, and their numbers.
    AnimationClip idleNow, walkNow, runNow;
    float walkSpeedNow, runSpeedNow, walkLeftNow, runLeftNow;
    Vector3 lastPosition;
    float speed, wIdle = 1f, wWalk, wRun, idleTime, phase, bodyYaw, feetY, feetVelocity;
    bool placed;

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
        Draw(view == null || view.ShowsAce);
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
        BuildGraph(animator, false);

        visual = body.AddComponent<PolygonNpcVisual>();
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
        mixer = AnimationMixerPlayable.Create(graph, 3);
        idle = Clip(idleNow, 0, footIK);
        walk = Clip(walkNow, 1, footIK);
        run = Clip(runNow, 2, footIK);
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
        wWalk = wRun = 0f;
        bodyYaw = transform.eulerAngles.y;
        placed = false;
        drawnInstance = null;
        drawnSet = false;
        Worn = true;
        if (view != null) view.BodyStandsIn = true;
        Place(0f);
        Draw(view == null || view.ShowsAce);
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
            float toward = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, toward, turnSpeed * dt);
            FacingError = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, toward));
        }
        else FacingError = 0f;

        // Which clips: standing, walking, running.
        float moving = Smooth(Mathf.InverseLerp(walkBlend.x, walkBlend.y, speed));
        float running = Smooth(Mathf.InverseLerp(runBlend.x, runBlend.y, speed));
        wIdle = 1f - moving;
        wWalk = moving * (1f - running);
        wRun = moving * running;

        // One stride phase for both, so the feet keep step while they blend: each clip at the rate that
        // keeps its stance foot planted at this speed.
        float scale = rigRoot != null ? rigRoot.lossyScale.y : 1f;
        float walkRate = Mathf.Clamp(speed / Mathf.Max(.1f, walkSpeedNow * scale), .6f, 1.8f);
        RunRate = Mathf.Clamp(speed / Mathf.Max(.1f, runSpeedNow * scale), .7f, 1.6f);
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

    // Drawn, or hidden (first person, a station): the body's own renderers (the Sidekick's, or the city look's).
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
