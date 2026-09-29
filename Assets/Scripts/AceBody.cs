using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// ---------------------------------------------------------------------------
// ACE'S STAND-IN BODY (29 Sept 2026: claude/break-ins-spec.md section 8, calls 1, 6 and d)
//
// Until Ace has a model of Ace's own, Ace wears a stand-in body at night: look 11, the green jacket
// (POLYGON City's Character_Male_Jacket, one of the walk-ins' looks, which leaves their pool so Ace
// never meets a double). By day the café keeps the capsule, except in its own lab
// (Fixit Fidget > Night > Ace's body 2 - Try it in the café).
//
// How: the same way every café person is drawn. A hidden café rig (Quaternius Beach, CC0) plays its
// idle, walk or run, blended by how fast Ace really moves, with each clip played at the rate that
// keeps its feet planted; the city look copies the rig bone by bone (PolygonNpcVisual). The body
// stands on the floor under Ace (a ray down from the capsule's middle) and turns to face where Ace
// is going; it keeps that heading when Ace stops.
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

    [Header("Set by Fixit Fidget > Night > Ace's body 1 - Put on Ace's stand-in body")]
    [Tooltip("The city look (a prefab in Assets/Art/CityNeighbors/Prefabs). Missing: Ace stays the capsule.")]
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

    /// <summary>The body is on (at night, or by day in the café lab).</summary>
    public bool Worn { get; private set; }
    /// <summary>The body is on and drawn (not first person, not at a station).</summary>
    public bool Drawn { get; private set; }
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
    public string LookName => look != null ? look.name : "";
    public Transform RigRoot => rigRoot;
    public PolygonNpcVisual Visual => visual;

    static bool labRequest;
    CafeViewMode view;
    CharacterController capsule;
    Transform rigRoot;
    PolygonNpcVisual visual;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable idle, walk, run;
    GameObject drawnInstance;
    bool failed;
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

    // Ace's look leaves the walk-ins' pool whenever Ace has one, by day too.
    void OnEnable()
    {
        if (look != null) CustomerProfile.ReserveStandInLook(this, look.name);
    }

    void OnDisable()
    {
        CustomerProfile.ReleaseStandInLook(this);
        TakeOff();
    }

    void OnDestroy() => TakeOff();

    bool HasParts => look != null && rig != null && idleClip != null && walkClip != null && runClip != null;

    bool Wanted
    {
        get
        {
            if (!Application.isPlaying || failed || !HasParts) return false;
            NightWalk night = NightWalk.Instance;
            return night != null && night.Active || labRequest && CafeLab.Active;
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

        graph = PlayableGraph.Create("Ace's body");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 3);
        idle = Clip(idleClip, 0);
        walk = Clip(walkClip, 1);
        run = Clip(runClip, 2);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Ace's body", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();

        visual = body.AddComponent<PolygonNpcVisual>();
        visual.Configure(new[] { look }, 0f, 0, 0f);
        if (!visual.ApplyAppearance(0))
        {
            Debug.LogWarning("[Ace's body] The look " + look.name + " couldn't copy the café rig; Ace stays the capsule.");
            failed = true;
            TakeOff();
            return;
        }
        Height = standingHeight > 0f ? standingHeight : MeasureHeight();
        lastPosition = transform.position;
        speed = 0f;
        bodyYaw = transform.eulerAngles.y;
        placed = false;
        drawnInstance = null;
        Worn = true;
        if (view != null) view.BodyStandsIn = true;
        Place(0f);
        Draw(view == null || view.ShowsAce);
    }

    AnimationClipPlayable Clip(AnimationClip clip, int port)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(0);   // their times are set by hand each frame (the run doesn't loop by itself)
        graph.Connect(playable, 0, mixer, port);
        return playable;
    }

    void TakeOff()
    {
        if (graph.IsValid()) graph.Destroy();
        if (rigRoot != null)
        {
            // PolygonNpcVisual takes its city look with it.
            if (Application.isPlaying) Destroy(rigRoot.gameObject); else DestroyImmediate(rigRoot.gameObject);
        }
        rigRoot = null;
        visual = null;
        drawnInstance = null;
        bool wasWorn = Worn;
        Worn = Drawn = false;
        if (wasWorn && view != null) view.BodyStandsIn = false;
    }

    float MeasureHeight()
    {
        if (visual == null || visual.VisualInstance == null) return 0f;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in visual.VisualInstance.GetComponentsInChildren<Renderer>(true))
        {
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
        float scale = rigRoot != null ? rigRoot.lossyScale.y : rigScale;
        float walkRate = Mathf.Clamp(speed / Mathf.Max(.1f, walkClipSpeed * scale), .6f, 1.8f);
        RunRate = Mathf.Clamp(speed / Mathf.Max(.1f, runClipSpeed * scale), .7f, 1.6f);
        float stepping = wWalk + wRun;
        float cycles = stepping > 1e-3f
            ? (wWalk * walkRate / walkClip.length + wRun * RunRate / runClip.length) / stepping
            : walkRate / walkClip.length;
        phase = Mathf.Repeat(phase + dt * cycles, 1f);
        idleTime = Mathf.Repeat(idleTime + dt, idleClip.length);

        idle.SetTime(idleTime);
        walk.SetTime(Mathf.Repeat(phase + walkLeftForward, 1f) * walkClip.length);
        run.SetTime(Mathf.Repeat(phase + runLeftForward, 1f) * runClip.length);
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

    // Drawn, or hidden (first person, a station): the city look's own renderers.
    void Draw(bool show)
    {
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
