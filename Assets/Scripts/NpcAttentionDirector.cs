using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place that knows about everyone in the café at once (pass 2). It
/// exists so that nobody scans everybody every frame: NpcSocial components
/// register here, and the director runs a few cheap, timed passes over the
/// list -
///  * ARRIVALS: when someone comes in, one to three people who can see the
///    door glance at them (more likely the sociable ones), each after their
///    own reaction delay.
///  * EXCHANGES: now and then two people sitting or standing near each other
///    and facing each other acknowledge one another - a look, a look back, a
///    nod - and go back to their own business. Pairs then leave each other
///    alone for a minute or so.
///  * CHATS: two people seated at the same table occasionally fall into a
///    short conversation: they look at each other, take turns "talking" (the
///    seated talk clip), the listener nods, and after ten seconds or so they
///    return to their own idles. No dialogue UI, no text: body language only.
///    At most two chats at a time, so most of the room stays ambient.
///    With the Mixamo clips (NpcBeats) the speaker uses the seated talking
///    clips, and on a sofa or the tub chair the listener now and then laughs;
///    two sociable people standing near each other sometimes turn an exchange
///    into a friendly remark and a laugh.
///  * EVENTS (the NPC life pass, 12 Oct 2026; measured first: 47 of 62
///    arrivals in the 10 Oct recordings drew no glance, and none of the four
///    storm-outs was watched): the door bell (people near the door glance at
///    who came in, seated people look up for a beat; whoever goes out gets a
///    look too), a storm-out (people near it watch them go, one standing
///    person may shake their head), a "Fixed!" at the repair bar (the stools'
///    customers and anyone near look toward the bench), and Ace passing close
///    (NpcSocial's own glance). Each looker keeps a cooldown, so nobody turns
///    like a weathervane, and an event turns at most a few heads. Visual only.
///  * COMPANIONS (pairs): two people who came in together chat sooner, longer
///    and more often at their table (PatronBrain registers them).
/// It also answers the small questions NpcSocial asks ("who is nearest in
/// front of me?", "who is walking past?") from the same list, holds the
/// shared points of interest (the counter, the menu board, Ace), and counts
/// what happened for the checks and recordings.
///
/// Created on demand; never saved into a scene. Nothing here moves anyone.
/// </summary>
[DefaultExecutionOrder(110)]
public sealed class NpcAttentionDirector : MonoBehaviour
{
    public sealed class ChatSession
    {
        public NpcSocial a, b;
        public float startedAt, endsAt, nextSwapAt, lookAwayUntilA, lookAwayUntilB, nextLookAwayAt;
        public bool aSpeaking;
        public bool Active => Time.time < endsAt;
    }

    private sealed class Exchange
    {
        public NpcSocial a, b;
        public float startedAt;
        public int step;
    }

    [Header("Pacing")]
    [Tooltip("Seconds between passes over the room for exchanges and chats.")]
    [SerializeField, Range(.5f, 5f)] private float passInterval = 2f;
    [Tooltip("Chance per pass that an eligible pair acknowledges each other, scaled by their sociability.")]
    [SerializeField, Range(0f, .5f)] private float exchangeChance = .06f;
    [Tooltip("Chance per pass that an eligible seated pair at one table starts a chat, scaled by their sociability.")]
    [SerializeField, Range(0f, .5f)] private float chatChance = .05f;
    [SerializeField, Range(0, 4)] private int maxChats = 2;
    [Tooltip("Furthest two people are for a glance exchange, metres.")]
    [SerializeField, Range(1.5f, 8f)] private float exchangeRange = 4f;
    [Tooltip("Furthest apart two seats are to count as the same table, metres (seat pose to seat pose).")]
    [SerializeField, Range(1f, 4f)] private float tableRange = 2.2f;
    [Tooltip("How long a pair leaves each other alone after an exchange or chat, seconds (a range).")]
    [SerializeField] private Vector2 pairCooldown = new Vector2(45f, 100f);
    [Tooltip("With the Mixamo clips: chance that two people standing near each other turn an exchange into a friendly " +
             "remark and a laugh, scaled by both their sociability.")]
    [SerializeField, Range(0f, 1f)] private float sharedLaugh = .25f;
    [Tooltip("With the Mixamo clips: chance per turn that the listener in a chat on a sofa or the tub chair laughs.")]
    [SerializeField, Range(0f, 1f)] private float chatLaugh = .2f;

    [Header("Events (12 Oct 2026)")]
    [Tooltip("Seconds a person leaves between two event looks (the bell, a storm-out, a Fixed!), so nobody is a weathervane.")]
    [SerializeField, Range(1f, 20f)] private float eventLookCooldown = 6f;
    [Tooltip("Most people who turn their heads to one event.")]
    [SerializeField, Range(1, 6)] private int eventLookers = 3;
    [Tooltip("How far from a storm-out people notice it, metres.")]
    [SerializeField, Range(2f, 12f)] private float stormOutRange = 6.5f;
    [Tooltip("How far from the bench a Fixed! turns heads, metres (the stools' customers are 1-2 m from it).")]
    [SerializeField, Range(2f, 12f)] private float fixedRange = 6.5f;
    [Tooltip("Chance the nearest standing onlooker shakes their head at a storm-out (an existing beat).")]
    [SerializeField, Range(0f, 1f)] private float stormOutHeadShake = .5f;

    private static NpcAttentionDirector instance;
    private static readonly List<NpcSocial> people = new();
    private static readonly Dictionary<NpcSocial.Beat, int> counts = new();
    private static Transform player;
    private static PlayerMovement playerMovement;
    private static float playerSearchedAt = -10f;
    private static Vector3? counterPoint, menuPoint;
    private static bool pointsSearched;

    private readonly List<Exchange> exchanges = new();
    private readonly List<ChatSession> chats = new();
    private readonly Dictionary<long, float> pairCooldownUntil = new();
    private readonly List<(NpcSocial a, NpcSocial b)> companionPairs = new();
    // Small things that happen a moment after an event (a head shake once the look has landed).
    private readonly List<(float at, NpcSocial who, System.Action what)> later = new();
    private readonly Dictionary<Transform, float> lastArrivalOf = new();
    private float nextPassAt, nextFixedPoll;
    private int exchangesStarted, chatsStarted, arrivalLooks, arrivalsSeen, stormOutsSeen, stormOutLooks, fixedSeen, fixedLooks, leaversSeen, leaverLooks;
    private int finishesSeen = -1;
    private System.Delegate fixedHandler;
    private System.Reflection.EventInfo fixedEvent;

    public static IReadOnlyList<NpcSocial> People => people;
    public static int ActiveChats => instance != null ? instance.chats.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        people.Clear();
        counts.Clear();
        player = null;
        playerMovement = null;
        playerSearchedAt = -10f;
        counterPoint = menuPoint = null;
        pointsSearched = false;
    }

    private static NpcAttentionDirector Ensure()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;
        // An ordinary object of the Play session, so it ends with it. Marked DontSave it outlived the
        // session in the editor and ran again beside the next one's director (PlaySessionLeftovers).
        var go = new GameObject("NPC attention director (runtime)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        instance = go.AddComponent<NpcAttentionDirector>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        CafeSoundscape.DoorCrossed -= OnDoorCrossed;
        CafeSoundscape.DoorCrossed += OnDoorCrossed;
        finishesSeen = Juice.Finishes;
        ListenForFixed();
    }

    private void OnDestroy()
    {
        CafeSoundscape.DoorCrossed -= OnDoorCrossed;
        if (fixedEvent != null && fixedHandler != null)
        {
            try { fixedEvent.RemoveEventHandler(null, fixedHandler); }
            catch (System.Exception) { }
        }
        if (instance == this) instance = null;
    }

    // Juice's "Fixed!" event (Systems A adds `public static event System.Action<JobBase> OnFixed;` this session) is
    // listened for by name, so this compiles and runs whether or not it has landed; without it the count of finishes
    // (Juice.Finishes) is polled four times a second instead, which catches the same moment a quarter second late.
    private void ListenForFixed()
    {
        try
        {
            fixedEvent = typeof(Juice).GetEvent("OnFixed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (fixedEvent == null || fixedEvent.EventHandlerType != typeof(System.Action<JobBase>)) { fixedEvent = null; return; }
            fixedHandler = new System.Action<JobBase>(OnFixedEvent);
            fixedEvent.AddEventHandler(null, fixedHandler);
        }
        catch (System.Exception)
        {
            fixedEvent = null;
            fixedHandler = null;
        }
    }

    private void OnFixedEvent(JobBase job)
    {
        Vector3 at = BenchPoint(job);
        finishesSeen = Juice.Finishes;
        NotifyFixed(at);
    }

    // Where the repair happened: the bench's mat (ItemInspector), else the device itself, else the counter.
    private static Vector3 BenchPoint(JobBase job)
    {
        if (ItemInspector.Instance != null && ItemInspector.Working) return ItemInspector.Instance.TableTop + Vector3.up * .12f;
        if (job != null) return job.transform.position + Vector3.up * .1f;
        return CounterPoint ?? Vector3.zero;
    }

    // ------------------------------------------------------------ registry and shared points

    public static void Register(NpcSocial who)
    {
        if (who == null || people.Contains(who)) return;
        people.Add(who);
        Ensure();
    }

    public static void Unregister(NpcSocial who)
    {
        people.Remove(who);
        if (instance != null) instance.Forget(who);
    }

    public static Transform Player
    {
        get
        {
            if (player == null && Time.time - playerSearchedAt > 2f)
            {
                playerSearchedAt = Time.time;
                PlayerMovement found = FindAnyObjectByType<PlayerMovement>();
                playerMovement = found;
                player = found != null ? found.transform : null;
            }
            return player;
        }
    }

    /// <summary>How fast Ace is walking right now, m/s (0 without him): NpcSocial's glance at Ace passing close.</summary>
    public static float PlayerSpeed
    {
        get
        {
            if (Player == null || playerMovement == null) return 0f;
            Vector3 v = playerMovement.CommandedVelocity;
            v.y = 0f;
            return v.magnitude;
        }
    }

    /// <summary>Behind the counter, at head height: where the barista works.</summary>
    public static Vector3? CounterPoint { get { FindPoints(); return counterPoint; } }
    /// <summary>The chalkboard menu on the back wall, or the counter if there is none.</summary>
    public static Vector3? MenuPoint { get { FindPoints(); return menuPoint; } }

    private static void FindPoints()
    {
        if (pointsSearched) return;
        pointsSearched = true;
        CounterQueue queue = FindAnyObjectByType<CounterQueue>();
        if (queue != null && queue.SlotCount > 0)
        {
            Transform slot = queue.SlotPoint(Mathf.Min(1, queue.SlotCount - 1));
            counterPoint = slot.position + slot.forward * 1.1f + Vector3.up * 1.45f;
        }
        Transform menu = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("menu") && (n.Contains("chalk") || n.Contains("board")) && t.position.y > .8f) { menu = t; break; }
            if (menu == null && n.Contains("menuboard")) menu = t;
        }
        if (menu != null)
        {
            Renderer r = menu.GetComponentInChildren<Renderer>();
            menuPoint = r != null ? r.bounds.center : menu.position + Vector3.up * 1.6f;
        }
        else if (counterPoint.HasValue) menuPoint = counterPoint.Value + Vector3.up * .5f;
    }

    // ------------------------------------------------------------ questions from NpcSocial

    /// <summary>The nearest other person within <paramref name="range"/> and in front (<paramref name="maxAngle"/>), optionally only walkers.</summary>
    public static NpcSocial Nearest(NpcSocial me, float range, float maxAngle, bool walkersOnly)
    {
        NpcSocial best = null;
        float bestD = range * range;
        Vector3 at = me.transform.position, fwd = me.transform.forward;
        for (int i = 0; i < people.Count; i++)
        {
            NpcSocial p = people[i];
            if (p == null || p == me || !p.isActiveAndEnabled) continue;
            if (walkersOnly && !p.IsWalkingAbout) continue;
            Vector3 d = p.transform.position - at;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq >= bestD || sq < 1e-4f) continue;
            if (Vector3.Angle(fwd, d) > maxAngle) continue;
            best = p;
            bestD = sq;
        }
        return best;
    }

    /// <summary>Someone walking past in front of <paramref name="me"/>: nearest walker that is actually moving.</summary>
    public static NpcSocial NearestWalker(NpcSocial me, float range, float maxAngle)
    {
        NpcSocial best = null;
        float bestD = range * range;
        Vector3 at = me.transform.position, fwd = me.transform.forward;
        for (int i = 0; i < people.Count; i++)
        {
            NpcSocial p = people[i];
            if (p == null || p == me || !p.isActiveAndEnabled || !p.IsWalkingAbout) continue;
            Vector3 d = p.transform.position - at;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq >= bestD || sq < 1e-4f) continue;
            if (Vector3.Angle(fwd, d) > maxAngle) continue;
            NpcLocomotion loco = p.GetComponent<NpcLocomotion>();
            if (loco != null && loco.Speed < .35f) continue;
            best = p;
            bestD = sq;
        }
        return best;
    }

    /// <summary>NpcSocial looked at a neighbour by itself; the director may turn it into an exchange.</summary>
    public static void NoticedNeighbour(NpcSocial who, NpcSocial other)
    {
        if (instance == null || who == null || other == null) return;
        if (!other.Available() || instance.OnCooldown(who, other)) return;
        float chance = .35f * Sociability(other);
        if (Random.value > chance) return;
        instance.StartExchange(who, other, true);
    }

    /// <summary>
    /// Someone has just come through the door (the brains say so as they start inside; the bell says so too). The
    /// people by the door look at who it is: the nearest one who can see the door nearly always (12 Oct: before, 47 of
    /// 62 arrivals drew no glance at all), one or two more by how curious and sociable they are; seated people look up
    /// from their table for a beat. Each looker keeps a cooldown between event looks.
    /// </summary>
    public static void NotifyArrival(Transform who)
    {
        if (who == null) return;
        NpcAttentionDirector d = Ensure();
        if (d == null) return;
        // The brain and the bell both announce one arrival: once a person.
        if (d.lastArrivalOf.TryGetValue(who, out float lastAt) && Time.time - lastAt < 3f) return;
        d.lastArrivalOf[who] = Time.time;
        d.arrivalsSeen++;
        d.arrivalLooks += d.LookAt(who, 10f, 105f, NpcSocial.Beat.ArrivalLook, 1.4f, 2.6f, .95f, .55f, true);
    }

    /// <summary>Someone went out of the door (the bell rang for them): the people near it glance after them.</summary>
    public static void NotifyLeaver(Transform who)
    {
        if (who == null) return;
        NpcAttentionDirector d = Ensure();
        if (d == null) return;
        d.leaversSeen++;
        d.leaverLooks += d.LookAt(who, 6f, 105f, NpcSocial.Beat.LeaverLook, 1f, 1.8f, .6f, .3f, false);
    }

    /// <summary>
    /// A customer is storming out (CustomerBrain.StormOut). The people within a few metres who can see them watch them
    /// go, a little longer than a glance; the nearest standing onlooker may shake their head (an existing beat). The
    /// café's owner has the storm-out itself; this is only the room noticing it.
    /// </summary>
    public static void NotifyStormOut(Transform who)
    {
        if (who == null) return;
        NpcAttentionDirector d = Ensure();
        if (d == null) return;
        d.stormOutsSeen++;
        int looked = d.LookAt(who, d.stormOutRange, 110f, NpcSocial.Beat.StormLook, 2f, 3.4f, 1f, .7f, false, out NpcSocial first);
        d.stormOutLooks += looked;
        if (first == null || Random.value > d.stormOutHeadShake) return;
        // Standing (the queue, a standing wait): the annoyed head shake once their eyes are on them. Seated people
        // have no seated head-shake clip in batch 1; they keep the look.
        bool standing = first.Current == NpcSocial.Situation.Queue || first.Current == NpcSocial.Situation.StandingWait;
        if (!standing || first.Beats == null) return;
        NpcSocial shaker = first;
        d.later.Add((Time.time + 1.1f, shaker, () => { if (shaker != null && shaker.Available(true)) shaker.Beats.PlayClip(NpcBeats.Clip.HeadShake); }));
    }

    /// <summary>
    /// "Fixed!" went off at the bench (Juice): the stools' customers and anyone within a few metres who can see the bench
    /// look toward it for a moment. The device's owner keeps their own reaction at the hand-back.
    /// </summary>
    public static void NotifyFixed(Vector3 benchPoint)
    {
        NpcAttentionDirector d = Ensure();
        if (d == null) return;
        d.fixedSeen++;
        d.fixedLooks += d.LookAt(benchPoint, null, d.fixedRange, 110f, NpcSocial.Beat.FixedLook, 1.4f, 2.4f, .9f, .5f, true, 4, out _);
    }

    // The bell: someone the brains did not announce (a patron the arrivals system starts at the door is announced by
    // its brain a frame later; the dedupe above handles both) and, going out, whoever leaves.
    private void OnDoorCrossed(Transform who, bool inside)
    {
        if (who == null) return;
        if (inside) NotifyArrival(who);
        else NotifyLeaver(who);
    }

    private int LookAt(Transform who, float range, float maxAngle, NpcSocial.Beat kind, float minSeconds, float maxSeconds, float firstChance, float otherChance, bool seatedLookUp)
        => LookAt(who.position, who, range, maxAngle, kind, minSeconds, maxSeconds, firstChance, otherChance, seatedLookUp, eventLookers, out _);

    private int LookAt(Transform who, float range, float maxAngle, NpcSocial.Beat kind, float minSeconds, float maxSeconds, float firstChance, float otherChance, bool seatedLookUp, out NpcSocial first)
        => LookAt(who.position, who, range, maxAngle, kind, minSeconds, maxSeconds, firstChance, otherChance, seatedLookUp, eventLookers, out first);

    // Heads turn to a point or a person: nearest first, each with their own reaction delay, no more than `most` of
    // them, never someone who looked at an event a moment ago, never someone Ace is talking to or who is in a chat.
    // Returns how many looked; `first` is the nearest who did.
    private int LookAt(Vector3 at, Transform who, float range, float maxAngle, NpcSocial.Beat kind, float minSeconds, float maxSeconds,
                       float firstChance, float otherChance, bool seatedLookUp, int most, out NpcSocial first)
    {
        first = null;
        int lookers = 0;
        people.Sort((x, y) => (x.transform.position - at).sqrMagnitude.CompareTo((y.transform.position - at).sqrMagnitude));
        NpcLookAt look = who != null ? who.GetComponent<NpcLookAt>() : null;
        Transform head = look != null ? look.HeadBone : null;
        for (int i = 0; i < people.Count && lookers < most; i++)
        {
            NpcSocial p = people[i];
            if (p == null || (who != null && p.transform == who) || !p.Available(true)) continue;
            if (Time.time - p.LastEventLookAt < eventLookCooldown) continue;
            Vector3 d2 = at - p.transform.position;
            d2.y = 0f;
            if (d2.magnitude > range || !p.CanSee(at, maxAngle)) continue;
            NpcMovementProfile prof = p.Profile;
            float curiosity = prof != null ? .6f + prof.lookTendency * .5f + prof.sociability * .2f - prof.shyness * .2f : .9f;
            float chance = Mathf.Clamp01((lookers == 0 ? firstChance : otherChance) * curiosity);
            if (Random.value > chance) continue;
            float delay = (prof != null ? prof.reactionDelay : .2f) + Random.Range(.05f, .35f) + lookers * .12f;
            float seconds = Random.Range(minSeconds, maxSeconds);
            if (who != null) p.Glance(head != null ? head : who, head != null ? Vector3.zero : Vector3.up * 1.55f, seconds, .85f, delay, kind);
            else p.Glance(at, seconds, .85f, delay, kind);
            // Seated: the look up from the table shows in the back too (a small sit-up), not only the head.
            if (seatedLookUp && p.Current == NpcSocial.Situation.Seated) p.SitUp(delay);
            p.LastEventLookAt = Time.time;
            if (first == null) first = p;
            lookers++;
        }
        return lookers;
    }

    public static void Count(NpcSocial.Beat beat)
    {
        counts.TryGetValue(beat, out int n);
        counts[beat] = n + 1;
    }

    /// <summary>One line of counts for the recorder's notes and the checks.</summary>
    public static string Summary()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("people ").Append(people.Count);
        if (instance != null)
            sb.Append(", arrivals ").Append(instance.arrivalsSeen).Append(" (looked at ").Append(instance.arrivalLooks).Append(" times)")
              .Append(", leavers ").Append(instance.leaversSeen).Append(" (looked at ").Append(instance.leaverLooks).Append(")")
              .Append(", storm-outs ").Append(instance.stormOutsSeen).Append(" (watched ").Append(instance.stormOutLooks).Append(" times)")
              .Append(", fixed ").Append(instance.fixedSeen).Append(" (looked at ").Append(instance.fixedLooks).Append(")")
              .Append(", exchanges ").Append(instance.exchangesStarted).Append(", chats ").Append(instance.chatsStarted)
              .Append(", companion pairs ").Append(instance.companionPairs.Count);
        foreach (var kv in counts) sb.Append(", ").Append(kv.Key).Append(' ').Append(kv.Value);
        return sb.ToString();
    }

    public static int CountOf(NpcSocial.Beat beat) => counts.TryGetValue(beat, out int n) ? n : 0;

    /// <summary>For the checks: events seen and heads turned (arrivals, leavers, storm-outs, fixed).</summary>
    public static int ArrivalsSeen => instance != null ? instance.arrivalsSeen : 0;
    public static int StormOutsSeen => instance != null ? instance.stormOutsSeen : 0;
    public static int StormOutLooks => instance != null ? instance.stormOutLooks : 0;
    public static int FixedSeen => instance != null ? instance.fixedSeen : 0;
    public static int FixedLooks => instance != null ? instance.fixedLooks : 0;
    public static int LeaversSeen => instance != null ? instance.leaversSeen : 0;
    public static int LeaverLooks => instance != null ? instance.leaverLooks : 0;
    public static bool ListensToJuice => instance != null && instance.fixedEvent != null;

    // ------------------------------------------------------------ companions (pairs)

    /// <summary>
    /// Two people who came in together (PatronSpawner's pairs): at their table they fall into a chat sooner, for longer
    /// and more often than strangers, and start one at once if both are seated.
    /// </summary>
    public static void Companions(NpcSocial a, NpcSocial b)
    {
        NpcAttentionDirector d = Ensure();
        if (d == null || a == null || b == null || a == b) return;
        if (!d.Together(a, b)) d.companionPairs.Add((a, b));
        a.Companion = b;
        b.Companion = a;
    }

    public static bool AreCompanions(NpcSocial a, NpcSocial b) => instance != null && instance.Together(a, b);

    private bool Together(NpcSocial a, NpcSocial b)
    {
        if (a == null || b == null) return false;
        for (int i = companionPairs.Count - 1; i >= 0; i--)
        {
            var pair = companionPairs[i];
            if (pair.a == null || pair.b == null) { companionPairs.RemoveAt(i); continue; }
            if (pair.a == a && pair.b == b || pair.a == b && pair.b == a) return true;
        }
        return false;
    }

    /// <summary>For the checks: start a chat between two seated people now (same rules as the pass, no dice).</summary>
    public static bool ForceChat(NpcSocial a, NpcSocial b)
    {
        NpcAttentionDirector d = Ensure();
        if (d == null || a == null || b == null || a == b) return false;
        if (a.Current != NpcSocial.Situation.Seated || b.Current != NpcSocial.Situation.Seated || a.InChat || b.InChat) return false;
        d.StartChat(a, b);
        return true;
    }

    /// <summary>For the checks: make <paramref name="a"/> acknowledge <paramref name="b"/> now.</summary>
    public static bool ForceExchange(NpcSocial a, NpcSocial b)
    {
        NpcAttentionDirector d = Ensure();
        if (d == null || a == null || b == null || a == b) return false;
        d.StartExchange(a, b, false);
        return true;
    }
    public static int Exchanges => instance != null ? instance.exchangesStarted : 0;
    public static int Chats => instance != null ? instance.chatsStarted : 0;
    public static int ArrivalLooks => instance != null ? instance.arrivalLooks : 0;

    // ------------------------------------------------------------ passes

    private void Update()
    {
        StepExchanges();
        StepChats();
        StepLater();
        PollFixed();
        if (Time.time < nextPassAt) return;
        nextPassAt = Time.time + passInterval;
        Pass();
    }

    // Without Juice's event: the count of finishes, four times a second.
    private void PollFixed()
    {
        // Beside the event too (13 Oct 2026, QA): a check that fires Juice.FixedMoment bumps Finishes without raising OnFixed;
        // the handler sets finishesSeen, so a real Finish is never counted twice.
        if (Time.time < nextFixedPoll) return;
        nextFixedPoll = Time.time + .25f;
        int now = Juice.Finishes;
        if (finishesSeen < 0) { finishesSeen = now; return; }
        if (now == finishesSeen) return;
        finishesSeen = now;
        NotifyFixed(BenchPoint(ItemInspector.Instance != null ? ItemInspector.Instance.FocusedItem : null));
    }

    private void StepLater()
    {
        for (int i = later.Count - 1; i >= 0; i--)
        {
            if (Time.time < later[i].at) continue;
            var item = later[i];
            later.RemoveAt(i);
            if (item.who == null) continue;
            try { item.what?.Invoke(); }
            catch (System.Exception e) { Debug.LogWarning("[NPC attention] A delayed beat failed: " + e.Message, this); }
        }
    }

    private readonly List<Transform> staleArrivals = new();

    private void Pass()
    {
        // Arrivals announced a while ago are forgotten (the dedupe only needs the last few seconds).
        if (lastArrivalOf.Count > 0)
        {
            staleArrivals.Clear();
            foreach (var kv in lastArrivalOf) if (kv.Key == null || Time.time - kv.Value > 60f) staleArrivals.Add(kv.Key);
            foreach (Transform t in staleArrivals) lastArrivalOf.Remove(t);
        }
        for (int i = 0; i < people.Count; i++)
        {
            NpcSocial a = people[i];
            if (a == null || !a.Available()) continue;
            for (int j = i + 1; j < people.Count; j++)
            {
                NpcSocial b = people[j];
                if (b == null || !b.Available() || OnCooldown(a, b)) continue;
                Vector3 d = b.transform.position - a.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > exchangeRange || dist < .3f) continue;
                if (!a.CanSee(b.transform.position, 85f) || !b.CanSee(a.transform.position, 85f)) continue;
                float social = (Sociability(a) + Sociability(b)) * .5f;
                bool together = Together(a, b);

                // Same table, both seated: a chat now and then; two who came in together, nearly at once.
                if (a.Current == NpcSocial.Situation.Seated && b.Current == NpcSocial.Situation.Seated && SameTable(a, b)
                    && (together ? Random.value < .6f : chats.Count < maxChats && Random.value < chatChance * social * 2f))
                {
                    StartChat(a, b);
                    continue;
                }
                if (Random.value < exchangeChance * social * 2f * (together ? 3f : 1f)) StartExchange(a, b, false);
            }
        }
    }

    private static float Sociability(NpcSocial p) => p.Profile != null ? Mathf.Clamp01(p.Profile.sociability * (1f - p.Profile.shyness * .6f)) : .5f;

    private bool SameTable(NpcSocial a, NpcSocial b)
    {
        NpcSeating sa = a.Seating, sb = b.Seating;
        if (sa == null || sb == null || sa.Seat == null || sb.Seat == null) return false;
        return (sa.Seat.SeatPose.position - sb.Seat.SeatPose.position).sqrMagnitude < tableRange * tableRange;
    }

    private long PairKey(NpcSocial a, NpcSocial b)
    {
        int x = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a), y = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b);
        if (x > y) (x, y) = (y, x);
        return ((long)x << 32) | (uint)y;
    }

    private bool OnCooldown(NpcSocial a, NpcSocial b)
    {
        bool together = Together(a, b);
        float own = together ? 6f : 12f;
        if (Time.time - a.LastExchangeAt < own || Time.time - b.LastExchangeAt < own) return true;
        return pairCooldownUntil.TryGetValue(PairKey(a, b), out float until) && Time.time < until;
    }

    // Strangers leave each other alone for a minute or so after a chat; two who came in together only for a short while.
    private void SetCooldown(NpcSocial a, NpcSocial b) =>
        pairCooldownUntil[PairKey(a, b)] = Time.time + (Together(a, b) ? Random.Range(10f, 24f) : Random.Range(pairCooldown.x, pairCooldown.y));

    private void Forget(NpcSocial who)
    {
        exchanges.RemoveAll(e => e.a == who || e.b == who);
        for (int i = chats.Count - 1; i >= 0; i--)
            if (chats[i].a == who || chats[i].b == who) EndChat(chats[i]);
        later.RemoveAll(l => l.who == who || l.who == null);
        companionPairs.RemoveAll(c => c.a == who || c.b == who || c.a == null || c.b == null);
        if (who != null) lastArrivalOf.Remove(who.transform);
    }

    // ---------- exchanges: a look, a look back, a nod

    private void StartExchange(NpcSocial a, NpcSocial b, bool aAlreadyLooking)
    {
        var e = new Exchange { a = a, b = b, startedAt = Time.time, step = aAlreadyLooking ? 1 : 0 };
        exchanges.Add(e);
        exchangesStarted++;
        a.LastExchangeAt = b.LastExchangeAt = Time.time;
        SetCooldown(a, b);
        if (!aAlreadyLooking)
        {
            float delay = a.Profile != null ? a.Profile.reactionDelay * .5f : 0f;
            a.Glance(Head(b), HeadOffset(b), Random.Range(1.8f, 2.6f), .8f, delay, NpcSocial.Beat.Exchange);
            e.step = 1;
        }
    }

    private void StepExchanges()
    {
        for (int i = exchanges.Count - 1; i >= 0; i--)
        {
            Exchange e = exchanges[i];
            if (e.a == null || e.b == null) { exchanges.RemoveAt(i); continue; }
            float t = Time.time - e.startedAt;
            float bDelay = .5f + (e.b.Profile != null ? e.b.Profile.reactionDelay : .2f);
            if (e.step == 1 && t >= bDelay)
            {
                e.step = 2;
                if (e.b.Available(true))
                {
                    e.b.Glance(Head(e.a), HeadOffset(e.a), Random.Range(1.3f, 2f), .8f, 0f, NpcSocial.Beat.Exchange);
                    if (Random.value < Gesture(e.a)) e.a.Nod(7f);
                }
            }
            else if (e.step == 2 && t >= bDelay + .7f)
            {
                e.step = 3;
                // Two sociable people standing close: now and then a friendly remark and a
                // laugh instead of the nod (NpcBeats; nothing happens without the clips).
                if (StandingPair(e.a, e.b) && Random.value < sharedLaugh * Sociability(e.a) * Sociability(e.b) * 2f && e.a.Friendly())
                    e.b.Laugh();
                else if (Random.value < Gesture(e.b)) e.b.Nod(7f);
            }
            else if (e.step >= 3 && t >= bDelay + 2.6f) exchanges.RemoveAt(i);
        }
    }

    private static float Gesture(NpcSocial p) => p.Profile != null ? .3f + p.Profile.gestureLikelihood * .6f : .5f;

    private static bool StandingPair(NpcSocial a, NpcSocial b)
    {
        bool standing(NpcSocial p) => (p.Current == NpcSocial.Situation.Queue || p.Current == NpcSocial.Situation.StandingWait)
                                      && !p.BrainHasFocus && !p.InChat;
        if (!standing(a) || !standing(b)) return false;
        Vector3 d = a.transform.position - b.transform.position;
        d.y = 0f;
        return d.magnitude < 2.2f;
    }
    private static Transform Head(NpcSocial p) => p.HeadTransform != null ? p.HeadTransform : p.transform;
    private static Vector3 HeadOffset(NpcSocial p) => p.HeadTransform != null ? Vector3.zero : Vector3.up * 1.55f;

    // ---------- chats: two seated people at one table

    private void StartChat(NpcSocial a, NpcSocial b)
    {
        bool together = Together(a, b);
        var chat = new ChatSession
        {
            a = a, b = b, startedAt = Time.time,
            endsAt = Time.time + (together ? Random.Range(10f, 18f) : Random.Range(7f, 14f)),
            nextSwapAt = Time.time + Random.Range(2.5f, 4.5f),
            nextLookAwayAt = Time.time + Random.Range(3f, 6f),
            aSpeaking = Random.value < .5f
        };
        chats.Add(chat);
        chatsStarted++;
        a.Chat = b.Chat = chat;
        a.LastExchangeAt = b.LastExchangeAt = Time.time;
        SetCooldown(a, b);
        // Torsos turn a little towards each other; the heads do the rest.
        a.LeanTowards(b.EyePoint);
        b.LeanTowards(a.EyePoint);
        NpcSocial speaker = chat.aSpeaking ? a : b;
        speaker.Gesture(chat.nextSwapAt - Time.time);
        Count(NpcSocial.Beat.Chat);
    }

    private void StepChats()
    {
        for (int i = chats.Count - 1; i >= 0; i--)
        {
            ChatSession c = chats[i];
            if (c.a == null || c.b == null || !c.Active
                || c.a.Current != NpcSocial.Situation.Seated || c.b.Current != NpcSocial.Situation.Seated
                || c.a.BrainHasFocus || c.b.BrainHasFocus)
            {
                EndChat(c);
                continue;
            }
            // Eyes on each other, with the odd look away.
            if (Time.time >= c.nextLookAwayAt)
            {
                c.nextLookAwayAt = Time.time + Random.Range(3f, 6f);
                if (Random.value < .5f) c.lookAwayUntilA = Time.time + Random.Range(.8f, 1.6f);
                else c.lookAwayUntilB = Time.time + Random.Range(.8f, 1.6f);
            }
            if (Time.time >= c.lookAwayUntilA) c.a.HoldLook(Head(c.b), HeadOffset(c.b), .85f, NpcSocial.Beat.Chat);
            else c.a.StopGlance();
            if (Time.time >= c.lookAwayUntilB) c.b.HoldLook(Head(c.a), HeadOffset(c.a), .8f, NpcSocial.Beat.Chat);
            else c.b.StopGlance();
            // Turns.
            if (Time.time >= c.nextSwapAt)
            {
                c.aSpeaking = !c.aSpeaking;
                c.nextSwapAt = Time.time + Random.Range(2.5f, 4.5f);
                NpcSocial speaker = c.aSpeaking ? c.a : c.b, listener = c.aSpeaking ? c.b : c.a;
                listener.StopGesture();
                if (Random.value < .75f) speaker.Gesture(Mathf.Min(c.nextSwapAt, c.endsAt) - Time.time);
                // On a sofa (no table to hit) the listener sometimes laughs at what was said.
                bool laughed = Random.value < chatLaugh && listener.Laugh();
                if (!laughed && Random.value < Gesture(listener) * .8f) listener.Nod(6f, Random.value < .3f ? 2 : 1);
            }
        }
    }

    private void EndChat(ChatSession c)
    {
        chats.Remove(c);
        c.endsAt = Mathf.Min(c.endsAt, Time.time);
        foreach (NpcSocial p in new[] { c.a, c.b })
        {
            if (p == null) continue;
            if (p.Chat == c) p.Chat = null;
            p.StopGesture();
            p.StopGlance();
            p.RelaxPosture();
            p.LastChatEndedAt = Time.time;
        }
    }
}
