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

    private static NpcAttentionDirector instance;
    private static readonly List<NpcSocial> people = new();
    private static readonly Dictionary<NpcSocial.Beat, int> counts = new();
    private static Transform player;
    private static float playerSearchedAt = -10f;
    private static Vector3? counterPoint, menuPoint;
    private static bool pointsSearched;

    private readonly List<Exchange> exchanges = new();
    private readonly List<ChatSession> chats = new();
    private readonly Dictionary<long, float> pairCooldownUntil = new();
    private float nextPassAt;
    private int exchangesStarted, chatsStarted, arrivalLooks, arrivalsSeen;

    public static IReadOnlyList<NpcSocial> People => people;
    public static int ActiveChats => instance != null ? instance.chats.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        people.Clear();
        counts.Clear();
        player = null;
        playerSearchedAt = -10f;
        counterPoint = menuPoint = null;
        pointsSearched = false;
    }

    private static NpcAttentionDirector Ensure()
    {
        if (instance != null) return instance;
        if (!Application.isPlaying) return null;
        var go = new GameObject("NPC attention director (runtime)") { hideFlags = HideFlags.DontSave };
        instance = go.AddComponent<NpcAttentionDirector>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
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
                player = found != null ? found.transform : null;
            }
            return player;
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

    /// <summary>Someone has just come through the door. A few people who can see it look up.</summary>
    public static void NotifyArrival(Transform who)
    {
        if (who == null) return;
        NpcAttentionDirector d = Ensure();
        if (d == null) return;
        d.arrivalsSeen++;
        Vector3 at = who.position;
        int lookers = 0;
        // Nearest first, so the people by the door are the ones who react.
        people.Sort((x, y) => (x.transform.position - at).sqrMagnitude.CompareTo((y.transform.position - at).sqrMagnitude));
        for (int i = 0; i < people.Count && lookers < 3; i++)
        {
            NpcSocial p = people[i];
            if (p == null || p.transform == who || !p.Available(true)) continue;
            Vector3 d2 = at - p.transform.position;
            d2.y = 0f;
            if (d2.magnitude > 10f || !p.CanSee(at, 100f)) continue;
            NpcMovementProfile prof = p.Profile;
            float chance = (prof != null ? prof.lookTendency * .45f + prof.sociability * .15f : .3f) * (lookers == 0 ? 1.4f : 1f);
            if (Random.value > chance) continue;
            float delay = (prof != null ? prof.reactionDelay : .2f) + Random.Range(.05f, .4f);
            NpcLookAt look = who.GetComponent<NpcLookAt>();
            Transform head = look != null ? look.HeadBone : null;
            p.Glance(head != null ? head : who, head != null ? Vector3.zero : Vector3.up * 1.55f, Random.Range(1.4f, 2.6f), .8f, delay, NpcSocial.Beat.ArrivalLook);
            lookers++;
            d.arrivalLooks++;
        }
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
              .Append(", exchanges ").Append(instance.exchangesStarted).Append(", chats ").Append(instance.chatsStarted);
        foreach (var kv in counts) sb.Append(", ").Append(kv.Key).Append(' ').Append(kv.Value);
        return sb.ToString();
    }

    public static int CountOf(NpcSocial.Beat beat) => counts.TryGetValue(beat, out int n) ? n : 0;

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
        if (Time.time < nextPassAt) return;
        nextPassAt = Time.time + passInterval;
        Pass();
    }

    private void Pass()
    {
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

                // Same table, both seated: a chat now and then.
                if (chats.Count < maxChats && a.Current == NpcSocial.Situation.Seated && b.Current == NpcSocial.Situation.Seated
                    && SameTable(a, b) && Random.value < chatChance * social * 2f)
                {
                    StartChat(a, b);
                    continue;
                }
                if (Random.value < exchangeChance * social * 2f) StartExchange(a, b, false);
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
        if (Time.time - a.LastExchangeAt < 12f || Time.time - b.LastExchangeAt < 12f) return true;
        return pairCooldownUntil.TryGetValue(PairKey(a, b), out float until) && Time.time < until;
    }

    private void SetCooldown(NpcSocial a, NpcSocial b) =>
        pairCooldownUntil[PairKey(a, b)] = Time.time + Random.Range(pairCooldown.x, pairCooldown.y);

    private void Forget(NpcSocial who)
    {
        exchanges.RemoveAll(e => e.a == who || e.b == who);
        for (int i = chats.Count - 1; i >= 0; i--)
            if (chats[i].a == who || chats[i].b == who) EndChat(chats[i]);
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
                if (Random.value < Gesture(e.b)) e.b.Nod(7f);
            }
            else if (e.step >= 3 && t >= bDelay + 2.6f) exchanges.RemoveAt(i);
        }
    }

    private static float Gesture(NpcSocial p) => p.Profile != null ? .3f + p.Profile.gestureLikelihood * .6f : .5f;
    private static Transform Head(NpcSocial p) => p.HeadTransform != null ? p.HeadTransform : p.transform;
    private static Vector3 HeadOffset(NpcSocial p) => p.HeadTransform != null ? Vector3.zero : Vector3.up * 1.55f;

    // ---------- chats: two seated people at one table

    private void StartChat(NpcSocial a, NpcSocial b)
    {
        var chat = new ChatSession
        {
            a = a, b = b, startedAt = Time.time,
            endsAt = Time.time + Random.Range(7f, 14f),
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
                if (Random.value < Gesture(listener) * .8f) listener.Nod(6f, Random.value < .3f ? 2 : 1);
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
