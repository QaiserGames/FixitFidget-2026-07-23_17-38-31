using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Body language from the Mixamo clips (batch 1, wired 27 Sept 2026): the idles,
/// frustration, happy reactions, greetings and seated chat gestures the café's
/// people play on top of the café's own animation.
///
/// HOW IT PLAYS
///  * The customer animator has a second layer, "Beats" (Fixit Fidget > NPC >
///    Mixamo 3), with one state per clip. Its weight is 0 unless a beat is showing;
///    this component fades it in, holds it, and fades it out. The base layer keeps
///    running underneath, so whatever the café was doing (idling, sitting) is
///    exactly what the body returns to.
///  * A beat only shows while the body holds still: standing (Idle, Standing Talk)
///    for a standing clip, settled in a chair (Sitting, Sitting Talk) for a seated
///    one. The moment the base layer starts anything else - a walk, a step round,
///    the hand-over gesture, standing up - the beat fades out in 0.15 s. So no
///    brain has to remember to stop one: walking, sitting and being served always
///    win.
///  * The Mixamo clips never enter the public repository. The animator only holds
///    empty placeholders; the baked clips come from <see cref="NpcBeatLibrary"/>
///    in the git-ignored Mixamo folder, swapped in by a shared override controller
///    when a person is created. Without that asset this component does nothing and
///    everyone uses the café's own clips, exactly as before.
///
/// WHEN (the brains decide, this component picks the clip)
///  * Idle variety: <see cref="NpcSocial"/> asks for one now and then among its
///    other idle beats (<see cref="TryIdle"/>). Which clip depends on standing or
///    seated, the person's movement profile (Distracted people reach for their
///    phone) and, for customers, how much patience they have left - the same
///    number the patience bar shows: calm, restless, frustrated, furious.
///  * Patience: crossing into frustrated, and again into furious, plays one
///    frustration beat straight away (a head shake or a pout; tapping fingers or
///    an impatient shuffle in a chair; at the very end, the angry gesture).
///  * Reactions (<see cref="React"/>): CustomerBrain calls these where it used to
///    play the one "Interact" gesture for everything - a greeting when Ace comes
///    to serve them, a nod for a drink order, thanks when served, excitement for a
///    perfect repair, a shrug for a passable one, disappointment when turned away,
///    relief when reassured, anger when they storm out. The body follows the face
///    the portrait shows. When a reaction can't play (no library, or the body is
///    busy) the brain falls back to Interact as before.
///  * Seated chats: the attention director's speaker gestures with the seated
///    talking clips (<see cref="PlayTalk"/>); on a sofa the listener sometimes
///    laughs.
///
/// SEATS WITHOUT A TABLE. Mansoor's rule (27 Sept): "Sitting Laughing" and
/// "Sitting Thumbs Up" play only where there is no table in front to hit (the
/// sofas and the tub chair); "Sitting - Tapping Fingers" only where there is one
/// (the hand rests on it). "Seated Idle" is not used at all. The two leaning
/// clips wait for lean spots (Step C) and are never picked yet.
///
/// Presentation only: nothing here changes queue order, patience, orders, money
/// or the day.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)] // Awake before the other NPC components touch the animator
public sealed class NpcBeats : MonoBehaviour
{
    /// <summary>The clips by name (the library entry and animator state names).</summary>
    public static class Clip
    {
        public const string BreathingIdle = "Breathing Idle", WeightShift = "Weight Shift", LookingAround = "Looking Around",
            Bored = "Bored", Thinking = "Thinking", HoldingIdle = "Holding Idle", Texting = "Texting",
            PhoneCall = "Talking On A Cell Phone", Leaning = "Leaning", ShoulderLean = "One Shoulder Lean",
            HeadShake = "Annoyed Head Shake", Pouting = "Pouting", AngryGesture = "Angry Gesture", Dismissing = "Dismissing Gesture",
            Disappointed = "Disappointed", Shrugging = "Shrugging", Thankful = "Thankful", RelievedSigh = "Relieved Sigh",
            HappyIdle = "Happy Idle", HappyHand = "Happy Hand Gesture", Excited = "Excited", Laughing = "Laughing",
            Greeting = "Standing Greeting", NodYes = "Head Nod Yes";
        public const string SitBreathing = "Sitting Idle - Breathing", SitHandsOnThighs = "Sitting Idle - Hands On Thighs",
            SitLookAround = "Sitting - Looking Side To Side", SitTalking = "Sitting Talking", SitTalkShort = "Talking",
            SitLaughing = "Sitting Laughing", SitImpatient = "Sitting - Impatiently Waiting", SitTapping = "Sitting - Tapping Fingers",
            SitAngry = "Sitting Angry", SitThumbsUp = "Sitting Thumbs Up", Beckoning = "Beckoning";
    }

    /// <summary>What just happened, for <see cref="React"/>.</summary>
    public enum Moment
    {
        Greet,          // at the counter, next to be heard, Ace comes over to serve them (once a visit)
        Intake,         // the conversation opens and they say what they want
        AcceptedDrink,  // a drink order taken at the counter
        CallForDrink,   // someone waiting on a repair asks for a coffee across the room
        Served,         // a drink handed to them
        Returned,       // their repair handed back (by grade)
        Reassured,      // Ace reassured them
        LetDown,        // turned away at the counter, or the drink they wanted ran out
        StormOut,       // patience gone: they walk out
        LookAround,     // just come in: the pause to take the room in
    }

    public enum Kind { None, Idle, Reaction, Talk }
    public enum Mood { Calm, Restless, Frustrated, Furious }
    private enum Place { Any, Table, NoTable }

    [Tooltip("Play the Mixamo beats when the clip library is present. Off: the café's own clips only.")]
    [SerializeField] private bool beats = true;
    [Tooltip("How long an idle beat takes to blend in and out, seconds.")]
    [SerializeField, Range(.1f, .8f)] private float idleFade = .45f;
    [Tooltip("How long a reaction takes to blend in and out, seconds (a reaction starts sooner).")]
    [SerializeField, Range(.1f, .6f)] private float reactionFade = .25f;
    [Tooltip("How fast a beat gets out of the way when the body has something else to do (a walk, a step round, " +
             "standing up), seconds.")]
    [SerializeField, Range(.05f, .4f)] private float interruptFade = .15f;

    // How long each clip plays (seconds; 0 = its own length) and where. Loops play
    // a stretch of themselves from a random point; reactions play from the start.
    private readonly struct Use
    {
        public readonly float min, max;
        public readonly bool loop;
        public readonly Place place;
        public Use(float min, float max, bool loop, Place place = Place.Any) { this.min = min; this.max = max; this.loop = loop; this.place = place; }
    }

    private static readonly Dictionary<string, Use> Uses = new()
    {
        // standing idles
        [Clip.BreathingIdle] = new Use(6f, 9.9f, true),
        [Clip.WeightShift] = new Use(5.5f, 9.4f, true),
        [Clip.LookingAround] = new Use(0f, 0f, false),
        [Clip.Bored] = new Use(6f, 10.7f, true),
        [Clip.Thinking] = new Use(0f, 0f, false),
        [Clip.HoldingIdle] = new Use(4f, 6f, true),
        [Clip.Texting] = new Use(7f, 14f, true),
        [Clip.PhoneCall] = new Use(8f, 16f, true),
        [Clip.HappyIdle] = new Use(2.9f, 5.8f, true),
        // standing one-offs
        [Clip.HeadShake] = new Use(0f, 0f, false),
        [Clip.Pouting] = new Use(0f, 0f, false),
        [Clip.AngryGesture] = new Use(0f, 0f, false),
        [Clip.Dismissing] = new Use(0f, 0f, false),
        [Clip.Disappointed] = new Use(0f, 0f, false),
        [Clip.Shrugging] = new Use(0f, 0f, false),
        [Clip.Thankful] = new Use(0f, 0f, false),
        [Clip.RelievedSigh] = new Use(0f, 0f, false),
        [Clip.HappyHand] = new Use(0f, 0f, false),
        [Clip.Excited] = new Use(4f, 4f, false),
        [Clip.Laughing] = new Use(3.5f, 4.5f, false),
        [Clip.Greeting] = new Use(0f, 0f, false),
        [Clip.NodYes] = new Use(0f, 0f, false),
        // seated
        [Clip.SitBreathing] = new Use(4.3f, 8.6f, true),
        [Clip.SitHandsOnThighs] = new Use(5f, 9f, true),
        [Clip.SitLookAround] = new Use(4f, 8f, true),
        [Clip.SitTalking] = new Use(0f, 0f, true),
        [Clip.SitTalkShort] = new Use(0f, 0f, true),
        [Clip.SitLaughing] = new Use(3.5f, 4.5f, false, Place.NoTable),
        [Clip.SitImpatient] = new Use(5f, 7f, true),
        [Clip.SitTapping] = new Use(4.8f, 9.6f, true, Place.Table),
        [Clip.SitAngry] = new Use(0f, 0f, false),
        [Clip.SitThumbsUp] = new Use(0f, 0f, false, Place.NoTable),
        [Clip.Beckoning] = new Use(0f, 0f, false),
    };

    // Base-layer states in which a beat may show.
    private static readonly int IdleState = Animator.StringToHash("CharacterArmature|Idle");
    private static readonly int StandingTalkState = Animator.StringToHash("Standing Talk");
    private static readonly int SittingState = Animator.StringToHash("Sitting");
    private static readonly int SittingTalkState = Animator.StringToHash("Sitting Talk");
    private static readonly int RestHash = Animator.StringToHash(NpcBeatLibrary.RestState);

    // ---------- shared: the library and one override controller per base controller

    private static NpcBeatLibrary library;
    private static bool libraryLoaded;
    private static readonly Dictionary<RuntimeAnimatorController, AnimatorOverrideController> overrides = new();
    private static readonly Dictionary<RuntimeAnimatorController, HashSet<string>> slotted = new();
    private static readonly Dictionary<TableSeat, bool> tableInFront = new();
    private static readonly Dictionary<string, int> started = new();
    private static int interruptions, reactions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        library = null;
        libraryLoaded = false;
        overrides.Clear();
        slotted.Clear();
        tableInFront.Clear();
        started.Clear();
        interruptions = reactions = 0;
    }

    /// <summary>The clip library, or null on a copy of the project without the Mixamo folder.</summary>
    public static NpcBeatLibrary Library
    {
        get
        {
            if (!libraryLoaded)
            {
                libraryLoaded = true;
                library = Resources.Load<NpcBeatLibrary>(NpcBeatLibrary.ResourceName);
            }
            return library;
        }
    }

    /// <summary>For the checks and recordings: how often each clip has started this session.</summary>
    public static IReadOnlyDictionary<string, int> Started => started;
    /// <summary>For the checks: beats cut short because the body had something else to do.</summary>
    public static int Interruptions => interruptions;
    /// <summary>For the checks: reactions played in place of the old Interact gesture.</summary>
    public static int Reactions => reactions;

    // ---------- this person

    private Animator animator;
    private NpcSocial social;
    private NpcSeating seating;
    private NpcLocomotion locomotion;
    private NpcPosture posture;
    private PolygonNpcVisual visual;
    private CustomerBrain customer;
    private HashSet<string> available;
    private int layer = -1;
    private bool ready;

    private NpcBeatLibrary.Entry current;
    private Kind kind;
    private bool stopping;
    private float weight, fadeFor, stopFade, endsAt, startedAt;
    private readonly string[] recent = new string[3];
    private int recentNext;
    private GameObject phone;
    private Transform rigRoot;
    // The phone as held: its speaker (near its top end) in the hand bone's space and half its
    // thickness (for the ear).
    private Vector3 phoneSpeakerInHand;
    private float phoneHalfThickness;
    private Transform phoneHand;

    private bool greeted, frustratedShown, furiousShown;
    private Mood pendingMood = Mood.Calm;
    private float pendingUntil, pleasedUntil;
    // A reaction asked for while the body was still finishing a small move (a turn
    // on the spot, the last step of a stop): played as soon as it holds still.
    private string queuedClip;
    private Kind queuedKind;
    private float queuedSeconds, queuedUntil;
    private readonly List<(string name, float weight)> candidates = new(16);

    /// <summary>The library and the animator's Beats layer are both here: beats can play.</summary>
    public bool Ready => ready && layer >= 0 && animator != null && animator.isActiveAndEnabled;
    /// <summary>A beat is showing (or fading out).</summary>
    public bool Playing => current != null;
    /// <summary>A reaction is showing and has not started to fade: the brain should not walk off yet.</summary>
    public bool Reacting => current != null && kind == Kind.Reaction && !stopping;
    public Kind CurrentKind => current != null ? kind : Kind.None;
    public string CurrentName => current != null ? current.name : "";
    /// <summary>How much of the beat is showing, 0-1 (the layer weight).</summary>
    public float Shown => weight * weight * (3f - 2f * weight);
    /// <summary>Seconds until the beat has faded out.</summary>
    public float Remaining => current == null ? 0f : Mathf.Max(0f, (stopping ? 0f : endsAt - Time.time)) + (stopping ? weight * stopFade : 0f);
    /// <summary>The phone in their hand right now, if any (for checks and photos).</summary>
    public GameObject Phone => phone != null && phone.activeSelf ? phone : null;
    public NpcBeatLibrary.Hand PhoneHand => current != null ? current.phoneHand : NpcBeatLibrary.Hand.None;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        social = GetComponent<NpcSocial>();
        seating = GetComponent<NpcSeating>();
        locomotion = GetComponent<NpcLocomotion>();
        posture = GetComponent<NpcPosture>();
        visual = GetComponent<PolygonNpcVisual>();
        customer = GetComponent<CustomerBrain>();
        if (!beats || animator == null) return;
        NpcBeatLibrary lib = Library;
        if (lib == null || lib.entries == null || lib.entries.Length == 0) return;

        RuntimeAnimatorController own = animator.runtimeAnimatorController;
        if (own == null) return;
        AnimatorOverrideController shared = SharedOverride(own, lib, out available);
        if (shared == null) return;
        if (own != shared)
        {
            // Swapping controllers resets the parameters: carry them over.
            var saved = SaveParameters(animator);
            animator.runtimeAnimatorController = shared;
            RestoreParameters(animator, saved);
        }
        ready = true;
        rigRoot = animator.transform;
        // Known now if the animator is already running; otherwise on the first Update.
        if (animator.isInitialized)
        {
            layer = animator.GetLayerIndex(NpcBeatLibrary.LayerName);
            if (layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }

    private void OnDisable()
    {
        if (current != null) Finish();
    }

    private void OnDestroy()
    {
        if (phone != null) Destroy(phone);
    }

    // ------------------------------------------------------------ the shared override

    // One override controller per base controller, shared by everyone: the
    // placeholder clips ("Beat slot - Thankful") are swapped for the baked clips.
    private static AnimatorOverrideController SharedOverride(RuntimeAnimatorController own, NpcBeatLibrary lib, out HashSet<string> names)
    {
        names = null;
        if (overrides.TryGetValue(own, out AnimatorOverrideController cached))
        {
            if (cached != null) names = slotted[own];
            return cached;
        }
        // Already one of ours (a person copied from another at run time).
        foreach (var pair in overrides)
            if (pair.Value != null && pair.Value == own) { names = slotted[pair.Key]; return pair.Value; }

        var aoc = new AnimatorOverrideController(own) { name = own.name + " (beats)" };
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
        aoc.GetOverrides(pairs);
        var filled = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < pairs.Count; i++)
        {
            AnimationClip slot = pairs[i].Key;
            if (slot == null || !slot.name.StartsWith(NpcBeatLibrary.SlotPrefix, System.StringComparison.Ordinal)) continue;
            NpcBeatLibrary.Entry entry = lib.Find(slot.name.Substring(NpcBeatLibrary.SlotPrefix.Length));
            if (entry == null || entry.clip == null) continue;
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(slot, entry.clip);
            filled.Add(entry.name);
        }
        if (filled.Count == 0)
        {
            // The animator has no Beats layer yet (Mixamo 3 not run): nothing to do.
            Destroy(aoc);
            overrides[own] = null;
            return null;
        }
        aoc.ApplyOverrides(pairs);
        overrides[own] = aoc;
        slotted[own] = filled;
        names = filled;
        return aoc;
    }

    private static List<(AnimatorControllerParameter p, float f, int i, bool b)> SaveParameters(Animator a)
    {
        var list = new List<(AnimatorControllerParameter, float, int, bool)>();
        if (!a.isActiveAndEnabled || !a.isInitialized || a.runtimeAnimatorController == null) return list;
        foreach (AnimatorControllerParameter p in a.parameters)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Float: list.Add((p, a.GetFloat(p.nameHash), 0, false)); break;
                case AnimatorControllerParameterType.Int: list.Add((p, 0f, a.GetInteger(p.nameHash), false)); break;
                case AnimatorControllerParameterType.Bool: list.Add((p, 0f, 0, a.GetBool(p.nameHash))); break;
            }
        }
        return list;
    }

    private static void RestoreParameters(Animator a, List<(AnimatorControllerParameter p, float f, int i, bool b)> saved)
    {
        if (saved.Count == 0 || !a.isActiveAndEnabled) return;
        foreach (var s in saved)
        {
            switch (s.p.type)
            {
                case AnimatorControllerParameterType.Float: a.SetFloat(s.p.nameHash, s.f); break;
                case AnimatorControllerParameterType.Int: a.SetInteger(s.p.nameHash, s.i); break;
                case AnimatorControllerParameterType.Bool: a.SetBool(s.p.nameHash, s.b); break;
            }
        }
    }

    // ------------------------------------------------------------ asking for beats

    /// <summary>
    /// An idle beat for someone standing about or sitting (NpcSocial asks). Picks
    /// from what suits the situation, their profile and their patience; false when
    /// nothing fits or the body is busy.
    /// </summary>
    public bool TryIdle(NpcSocial.Situation situation)
    {
        if (!Ready || current != null || queuedClip != null) return false;
        bool seated = situation == NpcSocial.Situation.Seated;
        if (seated != SettledInChair) return false;
        if (!seated && situation != NpcSocial.Situation.Queue && situation != NpcSocial.Situation.StandingWait) return false;
        Mood mood = CurrentMood();
        candidates.Clear();
        if (seated) SeatedPool(mood);
        else StandingPool(mood, situation == NpcSocial.Situation.Queue);
        string pick = Pick();
        return pick != null && PlayUse(pick, Kind.Idle);
    }

    /// <summary>At the menu board: a hand to the chin while reading it.</summary>
    public bool TryThinking()
    {
        if (!Ready || current != null || queuedClip != null || SettledInChair) return false;
        return PlayUse(Clip.Thinking, Kind.Idle);
    }

    /// <summary>A reaction to something that just happened (see <see cref="Moment"/>). False: nothing played; the caller keeps its old gesture.</summary>
    public bool React(Moment moment, PortraitExpression face = PortraitExpression.Neutral, JobGrade grade = JobGrade.Good, float seconds = 0f)
    {
        if (!Ready) return false;
        bool seated = SettledInChair;
        bool noTable = seated && !TableInFront(seating.Seat);
        bool played = false;
        switch (moment)
        {
            case Moment.Greet:
                if (seated || greeted) return false;
                greeted = true;   // once a visit, whether or not the body could do it now
                played = PlayUse(Clip.Greeting, Kind.Reaction);
                break;

            case Moment.Intake:
                if (seated) return false;
                if (face == PortraitExpression.Impatient) played = PlayUse(Shy ? Clip.Pouting : Clip.HeadShake, Kind.Reaction);
                else if (!greeted) { greeted = true; played = PlayUse(Clip.Greeting, Kind.Reaction); }
                else if (face == PortraitExpression.Happy) played = PlayUse(Clip.HappyHand, Kind.Reaction);
                break;   // otherwise the café's own gesture, as before

            case Moment.AcceptedDrink:
                if (!seated) played = PlayUse(Clip.NodYes, Kind.Reaction, 1.8f);
                break;

            case Moment.CallForDrink:
                // On foot they turn to Ace first (CustomerBrain), so the wave may wait for the turn.
                played = seated ? PlayUse(Clip.Beckoning, Kind.Reaction) : PlayUse(Clip.Greeting, Kind.Reaction, 3f, 2.5f);
                break;

            case Moment.Served:
                if (seated) played = noTable ? PlayUse(Clip.SitThumbsUp, Kind.Reaction) : PlayUse(Clip.SitTalkShort, Kind.Reaction, 2.4f);
                else played = PlayUse(Random.value < .7f ? Clip.Thankful : Clip.HappyHand, Kind.Reaction);
                if (played) pleasedUntil = Time.time + 25f;
                if (seated && social != null) social.Nod(7f);
                break;

            case Moment.Returned:
                played = Returned(grade, seated, noTable);
                if (seated && grade >= JobGrade.Good && social != null) social.Nod(7f);
                if (played && grade >= JobGrade.Good) pleasedUntil = Time.time + 25f;
                break;

            case Moment.Reassured:
                if (!seated) played = PlayUse(Clip.RelievedSigh, Kind.Reaction);
                else if (social != null) social.Nod(6f);
                if (played) frustratedShown = furiousShown = false;
                break;

            case Moment.LetDown:
                if (!seated) played = PlayUse(Easygoing ? Clip.Shrugging : Clip.Disappointed, Kind.Reaction);
                break;

            case Moment.StormOut:
                // Whatever they were doing, this wins (Play cross-fades over it).
                played = seated ? PlayUse(Clip.SitAngry, Kind.Reaction)
                                : PlayUse(Shy || Easygoing ? Clip.Dismissing : Clip.AngryGesture, Kind.Reaction);
                break;

            case Moment.LookAround:
                if (!seated) played = PlayUse(Clip.LookingAround, Kind.Idle, seconds);
                break;
        }
        if (played && moment != Moment.LookAround) reactions++;
        return played;
    }

    private bool Returned(JobGrade grade, bool seated, bool noTable)
    {
        switch (grade)
        {
            case JobGrade.Perfect:
                if (seated) return noTable ? PlayUse(Clip.SitThumbsUp, Kind.Reaction) : PlayUse(Clip.SitTalkShort, Kind.Reaction, 2.4f);
                return PlayUse(Clip.Excited, Kind.Reaction);
            case JobGrade.Good:
                if (seated) return noTable ? PlayUse(Clip.SitThumbsUp, Kind.Reaction) : PlayUse(Clip.SitTalkShort, Kind.Reaction, 2.4f);
                return PlayUse(Random.value < .6f ? Clip.Thankful : Clip.HappyHand, Kind.Reaction);
            case JobGrade.Passable:
                return !seated && PlayUse(Clip.Shrugging, Kind.Reaction);
            default:   // Rejected: it goes home unfixed
                if (seated) return PatienceNow() < .3f && PlayUse(Clip.SitAngry, Kind.Reaction);
                return PlayUse(Shy ? Clip.Pouting : Clip.Disappointed, Kind.Reaction);
        }
    }

    /// <summary>
    /// A seated turn in a conversation (the attention director's speaker): the
    /// short talking clip for a quick turn, the long one (from a random point) for
    /// a longer turn. False when not settled in a chair.
    /// </summary>
    public bool PlayTalk(float seconds)
    {
        if (!Ready || !SettledInChair) return false;
        if (current != null && kind != Kind.Talk && kind != Kind.Idle) return false;
        seconds = Mathf.Clamp(seconds, 1.2f, 12f);
        string clip = seconds <= 3.5f ? Clip.SitTalkShort : Clip.SitTalking;
        float from = clip == Clip.SitTalking ? Random.Range(0f, .85f) : Random.Range(0f, .3f);
        return Play(clip, seconds, Kind.Talk, from);
    }

    /// <summary>A laugh: seated only where there is no table in front (a sofa, the tub chair); standing anywhere.</summary>
    public bool PlayLaugh()
    {
        if (!Ready || CurrentMood() >= Mood.Frustrated) return false;
        if (current != null && kind == Kind.Reaction) return false;
        bool seated = SettledInChair;
        if (seated && TableInFront(seating.Seat)) return false;
        return PlayUse(seated ? Clip.SitLaughing : Clip.Laughing, Kind.Reaction);
    }

    /// <summary>A friendly hand gesture while standing (the other half of a shared laugh).</summary>
    public bool PlayFriendly()
    {
        if (!Ready || SettledInChair || current != null || CurrentMood() >= Mood.Frustrated) return false;
        return PlayUse(Clip.HappyHand, Kind.Reaction);
    }

    /// <summary>
    /// One clip by name, now (checks and tools), under the same rules as everything
    /// else: the body must hold still, and seated clips keep to their seats. Seconds
    /// 0 = its usual length.
    /// </summary>
    public bool PlayClip(string clip, float seconds = 0f) => Ready && PlayUse(clip, Kind.Reaction, seconds);

    /// <summary>Stop whatever is showing, blending out over <paramref name="fade"/> seconds.</summary>
    public void Stop(float fade = .3f)
    {
        if (current == null || stopping) return;
        stopping = true;
        stopFade = Mathf.Max(.05f, fade);
    }

    /// <summary>Stop an idle beat (they have something to attend to); reactions and talk carry on.</summary>
    public void StopIdle(float fade = .35f)
    {
        if (current != null && kind == Kind.Idle) Stop(fade);
    }

    /// <summary>Stop a seated talking turn (the director's turn is over).</summary>
    public void StopTalk(float fade = .4f)
    {
        if (current != null && kind == Kind.Talk) Stop(fade);
    }

    // ------------------------------------------------------------ pools

    private void StandingPool(Mood mood, bool queue)
    {
        bool distracted = Is("Distracted");
        bool warm = Is("Social") || Is("Relaxed");
        float phoneLike = distracted ? 4f : .6f;
        bool pleased = Time.time < pleasedUntil;
        switch (mood)
        {
            case Mood.Calm:
                Add(Clip.BreathingIdle, 3f);
                Add(Clip.WeightShift, Is("Relaxed") ? 4f : 2.5f);
                Add(Clip.LookingAround, 1f + LookTendency * 2f);
                Add(Clip.Texting, 1.5f * phoneLike);
                if (CanCall) Add(Clip.PhoneCall, .5f * phoneLike);
                Add(Clip.HappyIdle, (warm ? 1.2f : .4f) + (pleased ? 3f : 0f));
                if (queue && customer != null && customer.CanHearIntake && customer.Record != null && customer.Record.kind == JobKind.Repair)
                    Add(Clip.HoldingIdle, 1.2f);
                break;
            case Mood.Restless:
                Add(Clip.Bored, 4f);
                Add(Clip.WeightShift, 3f);
                Add(Clip.LookingAround, 2f);
                Add(Clip.Texting, 1f * phoneLike);
                Add(Clip.Pouting, 1f);
                Add(Clip.BreathingIdle, 1f);
                break;
            case Mood.Frustrated:
                Add(Clip.HeadShake, 3f);
                Add(Clip.Pouting, 3f);
                Add(Clip.Bored, 2f);
                Add(Clip.WeightShift, 1f);
                Add(Clip.LookingAround, 1f);
                break;
            default:
                // Furious. The angry gesture is kept for the walk-out itself (StormOut), so
                // it plays once, as the last thing they do.
                Add(Clip.HeadShake, 3f);
                Add(Clip.Pouting, 1.5f);
                Add(Clip.Bored, 1f);
                break;
        }
    }

    private void SeatedPool(Mood mood)
    {
        bool table = TableInFront(seating.Seat);
        switch (mood)
        {
            case Mood.Calm:
                Add(Clip.SitBreathing, 3f);
                Add(Clip.SitHandsOnThighs, 3f);
                Add(Clip.SitLookAround, 1f + LookTendency * 2f);
                break;
            case Mood.Restless:
                Add(Clip.SitLookAround, 3f);
                Add(Clip.SitImpatient, 2f);
                if (table) Add(Clip.SitTapping, 2f);
                Add(Clip.SitHandsOnThighs, 1f);
                Add(Clip.SitBreathing, 1f);
                break;
            case Mood.Frustrated:
                Add(Clip.SitImpatient, 4f);
                if (table) Add(Clip.SitTapping, 3f);
                Add(Clip.SitLookAround, 2f);
                break;
            default:
                // Furious. Sitting Angry is kept for the walk-out itself (StormOut).
                Add(Clip.SitImpatient, 3f);
                if (table) Add(Clip.SitTapping, 2f);
                Add(Clip.SitLookAround, 1f);
                break;
        }
    }

    private void Add(string clip, float w)
    {
        if (w <= 0f || !Has(clip) || !PlaceAllows(clip)) return;
        // The last few beats this person played are less likely again straight away.
        for (int i = 0; i < recent.Length; i++) if (recent[i] == clip) w *= i == (recentNext + recent.Length - 1) % recent.Length ? .1f : .45f;
        candidates.Add((clip, w));
    }

    private string Pick()
    {
        float total = 0f;
        foreach (var c in candidates) total += c.weight;
        if (total <= 0f) return null;
        float r = Random.value * total;
        foreach (var c in candidates)
            if ((r -= c.weight) <= 0f) return c.name;
        return candidates[candidates.Count - 1].name;
    }

    private bool PlaceAllows(string clip)
    {
        if (!Uses.TryGetValue(clip, out Use use) || use.place == Place.Any) return true;
        if (!SettledInChair) return false;
        bool table = TableInFront(seating.Seat);
        return use.place == Place.Table ? table : !table;
    }

    // ------------------------------------------------------------ patience

    private float PatienceNow() => customer != null && customer.ShowsPatience ? customer.PatienceFraction : 1f;

    /// <summary>Calm, restless, frustrated or furious, from the patience bar (patrons are always calm).</summary>
    public Mood CurrentMood()
    {
        float p = PatienceNow();
        if (p < .12f) return Mood.Furious;
        if (p < .3f) return Mood.Frustrated;
        // A hurried person gets restless sooner, a relaxed one later.
        float urgency = social != null && social.Profile != null ? social.Profile.urgency : .5f;
        return p < Mathf.Lerp(.45f, .65f, urgency) ? Mood.Restless : Mood.Calm;
    }

    // Crossing into frustrated, and again into furious, shows at once: one beat each
    // (again only after patience has come back up - served, reassured - and gone down again).
    private void WatchPatience()
    {
        if (customer == null) return;
        if (!customer.ShowsPatience) { pendingUntil = 0f; return; }
        float p = customer.PatienceFraction;
        if (p > .45f) frustratedShown = false;
        if (p > .25f) furiousShown = false;
        if (p < .12f && !furiousShown) { furiousShown = frustratedShown = true; pendingMood = Mood.Furious; pendingUntil = Time.time + 6f; }
        else if (p < .3f && !frustratedShown) { frustratedShown = true; pendingMood = Mood.Frustrated; pendingUntil = Time.time + 6f; }
        if (Time.time >= pendingUntil) return;
        if (current != null && kind != Kind.Idle) return;
        if (social != null && social.BrainHasFocus) return;   // Ace is right there with them
        bool seated = SettledInChair;
        candidates.Clear();
        // One step at a time: a sulk when they get frustrated, a head shake when they get
        // furious. The angry gesture (standing or seated) is kept for the walk-out itself,
        // a few seconds later: playing it here too showed it twice in a row (the 27 Sept
        // recording).
        if (seated)
        {
            Add(Clip.SitImpatient, 1f);
            if (TableInFront(seating.Seat)) Add(Clip.SitTapping, 1f);
        }
        else if (pendingMood == Mood.Furious)
        {
            Add(Clip.HeadShake, Shy ? 1f : 2f);
            Add(Clip.Pouting, Shy ? 2f : .5f);
        }
        else
        {
            Add(Clip.Pouting, 2f);
            if (!Shy) Add(Clip.HeadShake, 1f);
        }
        string pick = Pick();
        if (pick == null) { pendingUntil = 0f; return; }
        // An idle gives way to it (Play cross-fades from one to the other).
        if (PlayUse(pick, Kind.Reaction)) pendingUntil = 0f;
    }

    // ------------------------------------------------------------ playing

    // waitFor: how long a reaction may wait for the body to hold still (the end of a
    // stop, a turn on the spot) before it is dropped.
    private bool PlayUse(string clip, Kind as_, float seconds = 0f, float waitFor = 1.2f)
    {
        if (!Uses.TryGetValue(clip, out Use use)) use = new Use(0f, 0f, false);
        NpcBeatLibrary.Entry e = Entry(clip);
        if (e == null) return false;
        float length = Mathf.Max(.1f, e.clip.length);
        float from = 0f;
        float duration;
        if (seconds > 0f) duration = seconds;
        else if (use.loop)
        {
            float scale = social != null && social.Profile != null ? social.Profile.idleDuration : 1f;
            duration = Mathf.Clamp(Random.Range(use.min, use.max) * scale, use.min * .8f, use.max * 1.2f);
            // Long loops start anywhere, so two people never text in step.
            if (length > duration + 1f) from = Random.Range(0f, 1f - duration / length);
        }
        else duration = use.max > 0f ? Mathf.Min(length, Random.Range(use.min, use.max)) : length;
        if (Play(clip, duration, as_, from)) return true;
        // Standing, not walking anywhere, just finishing a turn or a stop: the
        // reaction waits a moment for the body instead of being lost.
        if (as_ != Kind.Idle && !e.seated && AboutToStandStill)
        {
            queuedClip = clip;
            queuedKind = as_;
            queuedSeconds = duration;
            queuedUntil = Time.time + waitFor;
            return true;
        }
        if (clip == Clip.LookingAround && AboutToStandStill)
        {
            queuedClip = clip;
            queuedKind = as_;
            queuedSeconds = duration;
            queuedUntil = Time.time + 1f;
            return true;
        }
        return false;
    }

    // Not seated or sitting down, and slow enough that the stop (or the turn on the
    // spot) is nearly over.
    private bool AboutToStandStill => Ready && (seating == null || !seating.Busy)
        && (locomotion == null || locomotion.Speed < .9f) && (locomotion == null || !locomotion.IsMoving);

    private bool Play(string clip, float seconds, Kind as_, float fromFraction)
    {
        NpcBeatLibrary.Entry e = Entry(clip);
        if (e == null || !StillFor(e.seated)) return false;
        if (!PlaceAllows(clip)) return false;
        float length = Mathf.Max(.1f, e.clip.length);
        float start = Mathf.Clamp01(fromFraction) * length;
        int hash = Animator.StringToHash(e.name);
        // Over another beat that is still showing: cross-fade from it, never snap.
        if (current != null && weight > .02f) animator.CrossFadeInFixedTime(hash, .3f, layer, start);
        else animator.PlayInFixedTime(hash, layer, start);
        queuedClip = null;

        current = e;
        kind = as_;
        stopping = false;
        startedAt = Time.time;
        endsAt = Time.time + Mathf.Max(.5f, seconds);
        fadeFor = as_ == Kind.Idle ? idleFade : reactionFade;
        // The café's own talk loop (a seated or standing gesture) stops underneath,
        // so the body returns to a calm idle when the beat fades out.
        if (social != null) social.StopTalkLoop();
        ShowPhone(e);
        recent[recentNext] = clip;
        recentNext = (recentNext + 1) % recent.Length;
        started.TryGetValue(clip, out int n);
        started[clip] = n + 1;
        return true;
    }

    private void Update()
    {
        if (!ready) return;
        if (layer < 0)
        {
            if (animator == null || !animator.isInitialized) return;
            layer = animator.GetLayerIndex(NpcBeatLibrary.LayerName);
            if (layer < 0) { ready = false; return; }
        }
        if (current != null && !stopping)
        {
            if (!StillFor(current.seated)) { interruptions++; Stop(interruptFade); }
            else if (Time.time >= endsAt - fadeFor) Stop(fadeFor);
        }
        float want = current != null && !stopping ? 1f : 0f;
        float span = want > weight ? fadeFor : stopFade;
        weight = Mathf.MoveTowards(weight, want, Time.deltaTime / Mathf.Max(.05f, span));
        float shown = Shown;
        if (animator.isActiveAndEnabled) animator.SetLayerWeight(layer, shown);
        if (posture != null) posture.Hush = shown;
        // On a call a city body holds the phone against its own ear (PolygonNpcVisual).
        if (visual != null && current != null && current.name == Clip.PhoneCall && Phone != null && phoneHand != null
            && visual.VisualInstance != null && Library.TryGetEars(visual.ActiveAppearanceName, out Vector3 earL, out Vector3 earR))
        {
            bool left = current.phoneHand == NpcBeatLibrary.Hand.Left;
            visual.HoldPhoneToEar(left, shown, left ? earL : earR, phoneSpeakerInHand, phoneHalfThickness);
        }
        if (current != null && stopping && weight <= 0f) Finish();
        if (queuedClip != null)
        {
            if (Time.time > queuedUntil) queuedClip = null;
            else if (Entry(queuedClip) is NpcBeatLibrary.Entry q && StillFor(q.seated)) Play(queuedClip, queuedSeconds, queuedKind, 0f);
        }
        WatchPatience();
    }

    private void Finish()
    {
        current = null;
        kind = Kind.None;
        stopping = false;
        weight = 0f;
        if (animator != null && animator.isActiveAndEnabled && layer >= 0)
        {
            animator.SetLayerWeight(layer, 0f);
            animator.Play(RestHash, layer, 0f);
        }
        if (posture != null) posture.Hush = 0f;
        HidePhone();
    }

    // The body is doing nothing else: standing still (Idle or the standing talk
    // loop) for a standing beat, settled in the chair for a seated one.
    private bool StillFor(bool seatedBeat)
    {
        if (animator == null || !animator.isActiveAndEnabled || layer < 0) return false;
        if (seatedBeat)
        {
            if (!SettledInChair) return false;
        }
        else
        {
            if (seating != null && seating.Busy) return false;
            // Not while turning on the spot either: the feet stepping round, or still well
            // off the facing it was given (turning to Ace to call for a drink).
            if (locomotion != null && (locomotion.Speed > .3f || locomotion.SteppingRound || locomotion.FacingLeft > 20f)) return false;
        }
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        if (!StillState(now.shortNameHash, seatedBeat)) return false;
        if (animator.IsInTransition(0) && !StillState(animator.GetNextAnimatorStateInfo(0).shortNameHash, seatedBeat)) return false;
        return true;
    }

    private static bool StillState(int hash, bool seated) =>
        seated ? hash == SittingState || hash == SittingTalkState : hash == IdleState || hash == StandingTalkState;

    private bool SettledInChair => seating != null && seating.Current == NpcSeating.Phase.Seated && seating.Seat != null;

    private bool Has(string clip) => available != null && available.Contains(clip) && Library != null && Library.Find(clip)?.clip != null;

    private NpcBeatLibrary.Entry Entry(string clip)
    {
        if (!Ready || !Has(clip)) return null;
        return Library.Find(clip);
    }

    // A call needs a body whose ears are known (the city looks; see PolygonNpcVisual.HoldPhoneToEar).
    private bool CanCall => visual != null && visual.VisualInstance != null && Library != null
                            && Library.TryGetEars(visual.ActiveAppearanceName, out _, out _);

    private bool Is(string profileName) => social != null && social.Profile != null
        && string.Equals(social.Profile.Name, profileName, System.StringComparison.OrdinalIgnoreCase);
    private bool Shy => social != null && social.Profile != null && social.Profile.shyness > .5f;
    private bool Easygoing => social != null && social.Profile != null && (social.Profile.urgency < .3f || social.Profile.sociability > .8f);
    private float LookTendency => social != null && social.Profile != null ? social.Profile.lookTendency : .5f;

    // ------------------------------------------------------------ seats

    /// <summary>
    /// Is there a table in front of this seat, at table height? A café chair's cup
    /// goes on the table in front of it; a sofa's goes beside the sitter or on the
    /// low lounge table. Worked out once per seat.
    /// </summary>
    public static bool TableInFront(TableSeat seat)
    {
        if (seat == null) return false;
        if (tableInFront.TryGetValue(seat, out bool known)) return known;
        Transform pose = seat.SeatPose;
        Vector3 facing = seat.FacingPoint - pose.position;
        Vector3 toCup = seat.CupSpot.position - pose.position;
        float rise = toCup.y;
        facing.y = toCup.y = 0f;
        bool table = rise > .15f && toCup.magnitude < 1.1f && toCup.sqrMagnitude > 1e-4f
                     && (facing.sqrMagnitude < 1e-4f || Vector3.Angle(facing, toCup) < 60f);
        tableInFront[seat] = table;
        return table;
    }

    // ------------------------------------------------------------ the phone

    // POLYGON City's smartphone in the hand the clip holds it in: its long side along
    // the fingers, its back on the palm, the screen facing away from it (as the
    // Step A photos). Placed once when the beat starts, then carried by the hand.
    private void ShowPhone(NpcBeatLibrary.Entry e)
    {
        NpcBeatLibrary lib = Library;
        if (e.phoneHand == NpcBeatLibrary.Hand.None || lib == null || lib.phone == null) { HidePhone(); return; }
        string s = e.phoneHand == NpcBeatLibrary.Hand.Left ? "L" : "R";
        Transform wrist = RigBone("Wrist." + s), middle = RigBone("Middle2." + s), index = RigBone("Index2." + s), pinky = RigBone("Pinky2." + s);
        if (wrist == null || middle == null || index == null || pinky == null) return;
        Transform hand = visual != null && visual.VisualInstance != null ? visual.CityHand(s == "L") : null;
        if (hand == null) hand = wrist;

        if (phone == null)
        {
            phone = Instantiate(lib.phone);
            phone.name = "Phone (" + name + ")";
            foreach (Collider c in phone.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody r in phone.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
        }
        phone.transform.SetParent(null, false);
        phone.transform.localScale = lib.phone.transform.localScale;
        phone.SetActive(true);
        MeshFilter filter = phone.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Vector3 along = (middle.position - wrist.position).normalized;
        Vector3 across = (index.position - pinky.position).normalized;
        Vector3 palm = (s == "R" ? Vector3.Cross(along, across) : Vector3.Cross(across, along)).normalized;
        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 size = bounds.size;
        int longAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        int thinAxis = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
        Quaternion meshToRoot = Quaternion.Inverse(phone.transform.rotation) * filter.transform.rotation;
        Vector3 longLocal = meshToRoot * Axis(longAxis);
        Vector3 screenLocal = meshToRoot * Axis(thinAxis);
        phone.transform.rotation = Quaternion.LookRotation(along, palm) * Quaternion.Inverse(Quaternion.LookRotation(longLocal, screenLocal));
        float scale = filter.transform.lossyScale.x;
        float reach = Vector3.Distance(wrist.position, middle.position) * .75f;
        Vector3 centre = hand.position + along * reach + palm * (size[thinAxis] * scale * .5f + .012f * transform.lossyScale.y);
        phone.transform.position += centre - filter.transform.TransformPoint(bounds.center);
        phone.transform.SetParent(hand, true);
        phoneHand = hand;
        // The speaker: most of the way from the phone's centre to its top (the end towards the fingertips).
        phoneSpeakerInHand = hand.InverseTransformPoint(filter.transform.TransformPoint(bounds.center) + along * (size[longAxis] * scale * .5f * .65f));
        phoneHalfThickness = size[thinAxis] * scale * .5f;
    }

    private void HidePhone()
    {
        if (phone == null) return;
        phone.transform.SetParent(transform, false);
        phone.SetActive(false);
    }

    private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    // The rig's own bone (never the city look's copy).
    private Transform RigBone(string boneName)
    {
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        return Find(rigRoot != null ? rigRoot : transform, boneName, skip);
    }

    private static Transform Find(Transform node, string boneName, Transform skip)
    {
        if (node == skip || node.name.StartsWith("City look", System.StringComparison.Ordinal)) return null;
        if (node.name == boneName) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform hit = Find(node.GetChild(i), boneName, skip);
            if (hit != null) return hit;
        }
        return null;
    }
}
