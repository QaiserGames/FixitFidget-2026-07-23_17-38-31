using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The life layer of one café NPC (pass 2): who they are as a mover (their
/// NpcMovementProfile), what their eyes do when nothing important is
/// happening, and the small things a person does while sitting or standing
/// about - shift their weight, glance at the door, look at a neighbour,
/// gesture once in a while, and mostly do nothing at all.
///
/// HOW IT FITS
///  * The brains still decide everything that matters. They tell this
///    component the SITUATION (queueing, ordering, waiting on a chair, walking
///    out) and, when Ace matters, where the head must look (<see cref="Focus"/>).
///    A brain focus always wins over anything ambient.
///  * <see cref="NpcAttentionDirector"/> (one per scene) coordinates the
///    things that involve two people - a glance returned, a short seated
///    conversation, heads turning when someone comes in - and calls
///    <see cref="Glance"/> / <see cref="Gesture"/> here. It also keeps the
///    counts the checks and recordings report.
///  * The body parts are driven through the components that already own them:
///    NpcLookAt (head), NpcPosture (spine), the Animator's Talking flag
///    (a seated hand gesture / the standing talk loop), NpcLocomotion (walk
///    speed and style) and PersonalSpace (body room). Nothing here moves feet.
///
/// PACING. Everything ambient is timer-driven with random gaps, so people are
/// still most of the time and never move in unison: a beat every 5-14 seconds
/// for an average profile, scaled by the profile's idle frequency.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(115)] // after the brains and NpcLocomotion (20), before NpcPosture (118) and NpcLookAt (120)
public sealed class NpcSocial : MonoBehaviour
{
    public enum Situation { None, Walking, Queue, Ordering, StandingWait, Seated, Leaving }
    public enum Beat { None, Still, Posture, LookAround, LookCounter, LookMenu, LookPlayer, LookNeighbour, LookPasser, Gesture, Exchange, Chat, ArrivalLook, PassingGlance }

    [Tooltip("Where profiles come from when a spawner does not hand one over.")]
    [SerializeField] private NpcProfileLibrary library;
    [Tooltip("Ambient beats (glances, posture, gestures). Off = the brain's focus only, as in pass 1.")]
    [SerializeField] private bool ambient = true;
    [Tooltip("Where Ace's eyes are above his transform, metres.")]
    [SerializeField] private float aceEyeOffset = .65f;

    private static readonly int TalkingHash = Animator.StringToHash("Talking");
    private static readonly int StandingTalkHash = Animator.StringToHash("Standing Talk");
    private static readonly int IdleStyleHash = Animator.StringToHash("IdleStyle");

    private NpcLookAt lookAt;
    private NpcPosture posture;
    private NpcLocomotion locomotion;
    private NpcSeating seating;
    private PersonalSpace space;
    private Animator animator;
    private NavMeshAgent agent;
    private Transform head;
    private bool hasStandingTalk, hasTalking, animatorChecked;

    // Brain focus: set every frame by the brain while Ace has their attention.
    private bool brainFocus;
    private Transform brainTarget;
    private Vector3 brainOffset, brainPoint;
    private float brainWeight;

    // Ambient look: a glance with a start delay and an end.
    private Transform glanceTarget;
    private Vector3 glanceOffset, glancePoint;
    private float glanceFrom, glanceUntil, glanceWeight;

    // Beats and their pacing.
    private float nextBeatAt, beatUntil;
    private Beat beat = Beat.None;
    private bool postureHeld;
    private float nextTalkAt;
    private float talkUntil = -1f;
    private bool talkingSet;
    private float nextPasserScanAt, lastPasserGlanceAt;
    private Transform lastPasser;
    private float lastArrivalLookAt = -99f;
    private float lastPlayerGlanceAt = -99f;

    private Situation situation;
    private float situationSince;

    public NpcMovementProfile Profile { get; private set; }
    public string ProfileName => Profile != null ? Profile.Name : "-";
    public Beat CurrentBeat => Time.time < beatUntil || Time.time < glanceUntil ? beat : Beat.None;
    public bool BrainHasFocus => brainFocus;
    /// <summary>Eyes on Ace right now - the brain's doing (being served, a delivery, a look as he comes over) or a glance of their own.</summary>
    public bool LookingAtPlayer => (brainFocus && brainTarget != null && NpcAttentionDirector.Player != null && brainTarget == NpcAttentionDirector.Player)
                                   || CurrentBeat == Beat.LookPlayer;
    public NpcAttentionDirector.ChatSession Chat { get; internal set; }
    public bool InChat => Chat != null && Chat.Active;
    public float LastExchangeAt { get; internal set; } = -99f;
    public float LastChatEndedAt { get; internal set; } = -99f;
    public NpcLookAt LookAt => lookAt;
    public NpcSeating Seating => seating;

    /// <summary>What this person is doing, as their brain sees it. Set by CustomerBrain / PatronBrain.</summary>
    public Situation Current
    {
        get => situation;
        set
        {
            if (situation == value) return;
            situation = value;
            situationSince = Time.time;
            // A new situation starts quiet: no beat straight away, no glance carried over.
            beat = Beat.None;
            beatUntil = 0f;
            glanceUntil = 0f;
            nextBeatAt = Time.time + FirstGap();
            if (talkingSet && value != Situation.Ordering && value != Situation.Seated) SetTalking(false);
            if (posture != null)
            {
                bool standing = value == Situation.Queue || value == Situation.StandingWait;
                posture.Sway(standing && Profile != null ? Profile.standingSway : 0f, Random.Range(6f, 9.5f));
                if (!standing && value != Situation.Seated) { posture.Relax(); postureHeld = false; }
            }
        }
    }

    /// <summary>Roughly where the eyes are, for other people to look at.</summary>
    public Vector3 EyePoint => head != null ? head.position : transform.position + Vector3.up * (1.55f * transform.lossyScale.y);
    public Transform HeadTransform => head;

    private void Awake()
    {
        lookAt = GetComponent<NpcLookAt>();
        if (lookAt == null) lookAt = gameObject.AddComponent<NpcLookAt>();
        posture = GetComponent<NpcPosture>();
        if (posture == null) posture = gameObject.AddComponent<NpcPosture>();
        locomotion = GetComponent<NpcLocomotion>();
        seating = GetComponent<NpcSeating>();
        space = GetComponent<PersonalSpace>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        if (seating != null) seating.ExternalChat = true;   // this component and the director own the seated talk flag
        nextBeatAt = Time.time + FirstGap();
    }

    private void OnEnable() => NpcAttentionDirector.Register(this);

    private void OnDisable()
    {
        NpcAttentionDirector.Unregister(this);
        if (talkingSet) SetTalking(false);
    }

    // ------------------------------------------------------------ profile

    /// <summary>
    /// Picks and applies a profile: the identity's authored one, else a stable
    /// one for a regular (same every visit), else a weighted roll.
    /// </summary>
    public NpcMovementProfile AssignProfile(CustomerIdentity identity)
    {
        NpcMovementProfile chosen = null;
        if (identity != null && identity.Profile != null)
        {
            chosen = identity.Profile.movementProfile;
            if (chosen == null && library != null) chosen = library.ForKey(identity.Profile.PersistentId);
        }
        if (chosen == null && library != null) chosen = library.Roll();
        ApplyProfile(chosen);
        return chosen;
    }

    public void ApplyProfile(NpcMovementProfile profile)
    {
        Profile = profile;
        if (profile == null) return;
        if (locomotion == null) locomotion = GetComponent<NpcLocomotion>();
        if (locomotion != null) locomotion.ApplyProfile(profile);
        if (space == null) space = GetComponent<PersonalSpace>();
        if (space != null) space.ExtraRadius = profile.personalSpace;
        CheckAnimator();
        if (animator != null && animator.runtimeAnimatorController != null && HasParameter(IdleStyleHash))
            animator.SetFloat(IdleStyleHash, (int)profile.idleStyle);
    }

    // ------------------------------------------------------------ brain focus

    /// <summary>The brain wants the head on <paramref name="target"/> this frame (Ace at the counter, a delivery).</summary>
    public void Focus(Transform target, Vector3 offset, float weight)
    {
        brainFocus = target != null;
        brainTarget = target;
        brainOffset = offset;
        brainWeight = weight;
    }

    public void Focus(Vector3 point, float weight)
    {
        brainFocus = true;
        brainTarget = null;
        brainPoint = point;
        brainWeight = weight;
    }

    public void ClearFocus() => brainFocus = false;

    // ------------------------------------------------------------ ambient requests

    /// <summary>Look at <paramref name="target"/> for <paramref name="seconds"/>, starting after <paramref name="delay"/>.</summary>
    public void Glance(Transform target, Vector3 offset, float seconds, float weight = .85f, float delay = 0f, Beat kind = Beat.LookNeighbour)
    {
        if (target == null) return;
        glanceTarget = target;
        glanceOffset = offset;
        glanceWeight = Mathf.Clamp01(weight);
        glanceFrom = Time.time + Mathf.Max(0f, delay);
        glanceUntil = glanceFrom + Mathf.Max(.2f, seconds);
        beat = kind;
        beatUntil = glanceUntil;
        NpcAttentionDirector.Count(kind);
    }

    public void Glance(Vector3 point, float seconds, float weight = .8f, float delay = 0f, Beat kind = Beat.LookAround)
    {
        glanceTarget = null;
        glancePoint = point;
        glanceWeight = Mathf.Clamp01(weight);
        glanceFrom = Time.time + Mathf.Max(0f, delay);
        glanceUntil = glanceFrom + Mathf.Max(.2f, seconds);
        beat = kind;
        beatUntil = glanceUntil;
        NpcAttentionDirector.Count(kind);
    }

    /// <summary>Keep an ambient look alive this frame (a conversation partner); call every frame.</summary>
    public void HoldLook(Transform target, Vector3 offset, float weight, Beat kind)
    {
        glanceTarget = target;
        glanceOffset = offset;
        glanceWeight = Mathf.Clamp01(weight);
        if (Time.time >= glanceUntil) glanceFrom = Time.time;
        glanceUntil = Time.time + .25f;
        beat = kind;
        beatUntil = glanceUntil;
    }

    public void StopGlance()
    {
        glanceUntil = 0f;
        if (Time.time < beatUntil) beatUntil = Time.time;
    }

    /// <summary>
    /// Turn the upper body a little towards <paramref name="point"/> (a chat
    /// partner beside you on a sofa): the head alone cannot reach someone at
    /// your shoulder, and people turn their torso to talk. A few degrees only.
    /// </summary>
    public void LeanTowards(Vector3 point, float maxTwist = 8f)
    {
        if (posture == null) return;
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-4f) return;
        float bearing = Vector3.SignedAngle(transform.forward, to, Vector3.up);
        // Twist towards them (left is +), lean in slightly.
        posture.Shift(2f, Mathf.Clamp(-bearing * .12f, -maxTwist, maxTwist), Mathf.Clamp(-bearing * .04f, -2.5f, 2.5f), Random.Range(.9f, 1.4f));
        postureHeld = true;
    }

    public void RelaxPosture()
    {
        if (posture == null) return;
        posture.Relax(Random.Range(1f, 1.8f));
        postureHeld = false;
    }

    /// <summary>A short animated gesture: the seated talk loop on a chair, the standing talk loop on foot (when wired).</summary>
    public bool Gesture(float seconds)
    {
        CheckAnimator();
        if (!hasTalking) return false;
        bool seated = seating != null && seating.IsSeated;
        if (!seated && !hasStandingTalk) return false;
        if (!seated && locomotion != null && locomotion.IsMoving) return false;
        SetTalking(true);
        talkUntil = Time.time + Mathf.Clamp(seconds, .8f, 12f);
        NpcAttentionDirector.Count(Beat.Gesture);
        return true;
    }

    public void StopGesture()
    {
        if (talkingSet) SetTalking(false);
    }

    public void Nod(float amplitude = 8f, int count = 1)
    {
        if (lookAt != null) lookAt.Nod(amplitude * Random.Range(.8f, 1.2f), count, Random.Range(.45f, .65f));
    }

    /// <summary>Free for something the director wants (a returned glance, a chat, a look at the door).</summary>
    public bool Available(bool allowMidBeat = false)
    {
        if (!isActiveAndEnabled || !ambient || brainFocus || InChat) return false;
        if (situation != Situation.Seated && situation != Situation.StandingWait && situation != Situation.Queue) return false;
        if (seating != null && seating.Busy && !seating.IsSeated) return false;   // stepping to or from a chair
        if (!allowMidBeat && Time.time < beatUntil && beat != Beat.Posture && beat != Beat.Still) return false;
        return true;
    }

    public bool IsWalkingAbout => situation == Situation.Walking || situation == Situation.Leaving;

    /// <summary>Whether <paramref name="point"/> is in front of this person (head reach plus a margin).</summary>
    public bool CanSee(Vector3 point, float maxAngle = 95f)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 1e-4f && Vector3.Angle(transform.forward, to) <= maxAngle;
    }

    // ------------------------------------------------------------ each frame

    private void Update()
    {
        if (talkingSet && talkUntil > 0f && Time.time >= talkUntil) SetTalking(false);
        if (!ambient || Profile == null && library == null) return;
        if (InChat) return;   // the director runs the conversation

        switch (situation)
        {
            case Situation.Walking:
            case Situation.Leaving:
                PassingGlances();
                break;
            case Situation.Ordering:
                OrderingGestures();
                break;
            case Situation.Queue:
            case Situation.StandingWait:
            case Situation.Seated:
                if (!brainFocus) NoticePassers();
                IdleBeats();
                break;
        }
    }

    private void LateUpdate()
    {
        if (lookAt == null) return;
        if (brainFocus)
        {
            if (brainTarget != null) lookAt.LookAt(brainTarget, brainOffset, brainWeight);
            else lookAt.LookAt(brainPoint, brainWeight);
            return;
        }
        if (Time.time >= glanceFrom && Time.time < glanceUntil)
        {
            if (glanceTarget != null) lookAt.LookAt(glanceTarget, glanceOffset, glanceWeight);
            else lookAt.LookAt(glancePoint, glanceWeight);
            return;
        }
        lookAt.Clear();
    }

    // ------------------------------------------------------------ idle beats (seated, waiting, queueing)

    private float FirstGap() => Random.Range(2f, 6f) / Mathf.Max(.4f, Profile != null ? Profile.idleFrequency : 1f);

    private float Gap()
    {
        float f = Profile != null ? Profile.idleFrequency : 1f;
        float baseGap = situation == Situation.Queue ? Random.Range(4f, 10f) : Random.Range(5f, 14f);
        return baseGap / Mathf.Max(.4f, f);
    }

    private float Dur(float min, float max) => Random.Range(min, max) * (Profile != null ? Profile.idleDuration : 1f);

    private void IdleBeats()
    {
        if (Time.time < beatUntil) return;
        if (beat != Beat.None && beat != Beat.Still)
        {
            // The beat just ended: quiet again for a while.
            beat = Beat.Still;
            nextBeatAt = Time.time + Gap();
            return;
        }
        if (Time.time < nextBeatAt) return;
        if (brainFocus) { nextBeatAt = Time.time + 2f; return; }   // Ace has their attention; try later
        PickBeat();
    }

    private void PickBeat()
    {
        float look = Profile != null ? Profile.lookTendency : .5f;
        float gesture = Profile != null ? Profile.gestureLikelihood : .4f;
        float shy = Profile != null ? Profile.shyness : 0f;
        bool seated = situation == Situation.Seated;
        bool queue = situation == Situation.Queue;

        // Weighted choice among what fits the situation.
        float wPosture = seated ? 30f : 12f;
        float wAround = queue ? 20f : 22f;
        float wCounter = queue ? 0f : 12f;
        float wMenu = queue ? 26f : 0f;
        float wPlayer = (queue ? 26f : 12f) * (.4f + look);
        float wNeighbour = 14f * (1f - shy * .7f) * (.5f + look);
        float wGesture = seated ? 9f * gesture : 0f;
        float wStill = 10f;
        float total = wPosture + wAround + wCounter + wMenu + wPlayer + wNeighbour + wGesture + wStill;
        float r = Random.value * total;
        bool done;
        if ((r -= wPosture) <= 0f) done = PostureBeat(seated);
        else if ((r -= wAround) <= 0f) done = LookAroundBeat();
        else if ((r -= wCounter) <= 0f) done = LookPointBeat(NpcAttentionDirector.CounterPoint, Beat.LookCounter, 1.4f, 3f, .8f);
        else if ((r -= wMenu) <= 0f) done = LookPointBeat(NpcAttentionDirector.MenuPoint, Beat.LookMenu, 2f, 4.5f, .85f);
        else if ((r -= wPlayer) <= 0f) done = LookPlayerBeat(queue);
        else if ((r -= wNeighbour) <= 0f) done = LookNeighbourBeat();
        else if ((r -= wGesture) <= 0f) done = seated && Gesture(Dur(1.6f, 2.8f));
        else done = false;

        if (done) return;
        // Nothing fitted (nobody near, target behind): stay still a little longer.
        beat = Beat.Still;
        NpcAttentionDirector.Count(Beat.Still);
        nextBeatAt = Time.time + Gap() * .6f;
    }

    private bool PostureBeat(bool seated)
    {
        if (posture == null) return false;
        beat = Beat.Posture;
        beatUntil = Time.time + .5f;
        NpcAttentionDirector.Count(Beat.Posture);
        if (postureHeld && Random.value < .45f)
        {
            posture.Relax(Random.Range(.8f, 1.6f));
            postureHeld = false;
            return true;
        }
        float lean = seated ? Random.Range(-3.5f, 4.5f) : Random.Range(-1.5f, 2f);
        float twist = Random.Range(-4f, 4f);
        float tilt = seated ? Random.Range(-3f, 3f) : Random.Range(-2.5f, 2.5f);
        // A rarer, bigger beat: sitting back (a small stretch) or leaning in on the elbows.
        if (seated && Random.value < .2f) lean = Random.value < .5f ? -6f : 6f;
        posture.Shift(lean, twist, tilt, Random.Range(.7f, 1.5f));
        postureHeld = true;
        return true;
    }

    private bool LookAroundBeat()
    {
        // Somewhere in the room, off to one side: a window, a table, the door.
        float yaw = Random.Range(-70f, 70f) * (Random.value < .5f ? 1f : -1f);
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * transform.forward;
        Vector3 point = EyePoint + dir * Random.Range(3f, 7f) + Vector3.up * Random.Range(-.4f, .2f);
        Glance(point, Dur(1.4f, 3.2f), Random.Range(.55f, .85f), 0f, Beat.LookAround);
        return true;
    }

    private bool LookPointBeat(Vector3? point, Beat kind, float min, float max, float weight)
    {
        if (!point.HasValue || !CanSee(point.Value, 100f)) return false;
        Glance(point.Value, Dur(min, max), weight, 0f, kind);
        return true;
    }

    private bool LookPlayerBeat(bool queue)
    {
        Transform ace = NpcAttentionDirector.Player;
        if (ace == null) return false;
        Vector3 to = ace.position - transform.position;
        to.y = 0f;
        float range = queue ? 6f : 8f;
        if (to.magnitude > range || !CanSee(ace.position, 105f)) return false;
        if (Time.time - lastPlayerGlanceAt < 6f) return false;
        lastPlayerGlanceAt = Time.time;
        Glance(ace, Vector3.up * aceEyeOffset, Dur(1.4f, 3f), queue ? .9f : .8f, 0f, Beat.LookPlayer);
        return true;
    }

    private bool LookNeighbourBeat()
    {
        NpcSocial other = NpcAttentionDirector.Nearest(this, 4.5f, 95f, false);
        if (other == null) return false;
        float delay = Profile != null ? Profile.reactionDelay * .5f : 0f;
        Glance(other.HeadTransform != null ? other.HeadTransform : other.transform, other.HeadTransform != null ? Vector3.zero : Vector3.up * 1.55f,
            Dur(1.2f, 2.4f), .75f, delay, Beat.LookNeighbour);
        NpcAttentionDirector.NoticedNeighbour(this, other);
        return true;
    }

    // Someone walking past a person who is sitting or standing: a brief look.
    private void NoticePassers()
    {
        if (Time.time < nextPasserScanAt || Time.time < beatUntil) return;
        nextPasserScanAt = Time.time + .7f;
        if (Time.time - lastPasserGlanceAt < 7f) return;
        float look = Profile != null ? Profile.lookTendency : .5f;
        NpcSocial passer = NpcAttentionDirector.NearestWalker(this, 2.8f, 90f);
        if (passer == null || passer.transform == lastPasser && Time.time - lastPasserGlanceAt < 15f) return;
        if (Random.value > look * .5f) { lastPasserGlanceAt = Time.time - 5f; return; }   // not this one; look again soon
        lastPasser = passer.transform;
        lastPasserGlanceAt = Time.time;
        float delay = Profile != null ? Profile.reactionDelay : .2f;
        Glance(passer.HeadTransform != null ? passer.HeadTransform : passer.transform, passer.HeadTransform != null ? Vector3.zero : Vector3.up * 1.55f,
            Dur(1f, 1.9f), .7f, delay, Beat.LookPasser);
    }

    // A walker glancing at someone they pass, or at Ace.
    private void PassingGlances()
    {
        if (Time.time < nextPasserScanAt) return;
        nextPasserScanAt = Time.time + .6f;
        if (Time.time < glanceUntil || brainFocus) return;
        if (Time.time - lastPasserGlanceAt < 8f) return;
        float look = Profile != null ? Profile.lookTendency : .5f;
        float shy = Profile != null ? Profile.shyness : 0f;
        Transform ace = NpcAttentionDirector.Player;
        if (ace != null && Time.time - lastPlayerGlanceAt > 12f)
        {
            Vector3 to = ace.position - transform.position;
            to.y = 0f;
            if (to.magnitude < 3f && CanSee(ace.position, 70f) && Random.value < look * .6f)
            {
                lastPlayerGlanceAt = lastPasserGlanceAt = Time.time;
                Glance(ace, Vector3.up * aceEyeOffset, Dur(.9f, 1.5f), .75f, 0f, Beat.PassingGlance);
                return;
            }
        }
        NpcSocial other = NpcAttentionDirector.Nearest(this, 2.6f, 70f, true);
        if (other == null || other.transform == lastPasser) return;
        if (Random.value > look * (1f - shy * .6f) * .5f) { lastPasserGlanceAt = Time.time - 6f; return; }
        lastPasser = other.transform;
        lastPasserGlanceAt = Time.time;
        Glance(other.HeadTransform != null ? other.HeadTransform : other.transform, other.HeadTransform != null ? Vector3.zero : Vector3.up * 1.55f,
            Dur(.9f, 1.4f), .7f, 0f, Beat.PassingGlance);
    }

    // At the counter, talking to Ace: the standing talk loop in short bursts.
    private void OrderingGestures()
    {
        if (Time.time < nextTalkAt) return;
        if (talkingSet) return;
        float gesture = Profile != null ? Profile.gestureLikelihood : .4f;
        nextTalkAt = Time.time + Random.Range(4f, 8f);
        if (Time.time - situationSince < .4f) { nextTalkAt = Time.time + .4f; return; }
        if (Random.value < .35f + gesture * .5f) Gesture(Random.Range(1.8f, 3.2f));
    }

    // ------------------------------------------------------------ animator

    private void CheckAnimator()
    {
        if (animatorChecked) return;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isInitialized) return;
        animatorChecked = true;
        hasTalking = HasParameter(TalkingHash);
        hasStandingTalk = animator.HasState(0, StandingTalkHash);
    }

    private bool HasParameter(int hash)
    {
        if (animator == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters) if (p.nameHash == hash) return true;
        return false;
    }

    private void SetTalking(bool on)
    {
        talkingSet = on;
        if (!on) talkUntil = -1f;
        if (animator != null && animator.isActiveAndEnabled && hasTalking) animator.SetBool(TalkingHash, on);
    }

    private void Start()
    {
        if (lookAt != null) head = lookAt.HeadBone;
        CheckAnimator();
    }
}
