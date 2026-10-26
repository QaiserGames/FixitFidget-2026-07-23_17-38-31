using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// CHAT WITH SEATED GUESTS: GOSSIP INTO THE NOTEBOOK (24 Oct 2026; Designer P's spec §2.4 and §6.5, approved by him for building
// with his answers of 24 Oct)
//
// One of the four things to do while waiting he chose on 22 Oct ("chatting with seated guests"); his words that started it:
// "what i mean about dead air is really the things you could be doing when waiting for a customer to approach the counter."
//
// Each day's data names its gossip (DayDefinition.gossip: Day 1 none, Days 2 and 3 two lines each; the lines and facts are
// NotebookEntries' gossip table, PLACEHOLDER COPY for his sister). Once the day's teaching has begun (Downtime.TeachingStarted:
// the first customer heard, so the chat never competes with the day's first card), each line is dealt to one SEATED guest, a
// patron or a settled guest, never a customer waiting on Ace and never the man at the bins on his morning visit. A "…" shows
// over their head (Juice.Mark, Icon.Dots) and a "Chat" GossipChat sits on them (a small trigger round their chest). E: the
// guest says the line in a bubble (Barks.Say, at most 10 words) and the notebook learns the fact under the person it is about
// (NotebookHooks.HeardGossip: overheard, likely), with the notebook's page turn. No camera, no panel: Ace is free the moment he
// presses, and walking off ends nothing. A guest who gets up first takes nothing with them: their line goes to someone else.
//
// The Now line offers it only while nobody needs Ace (Downtime.Next: third, after a refill and a dirty table), as
// "{E} Chat with the guest in the lounge." (DayOneGuideUI). CustomerSpawner adds this to itself: no scene change. Never saved:
// a day replayed after a quit has its gossip again (the notebook learns a fact once, whatever).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GuestGossip : MonoBehaviour
{
    public static GuestGossip Instance { get; private set; }

    /// <summary>Who a guest's bark is said as (Barks.Say's speaker id): anyone in the café.</summary>
    public const string SpeakerId = "guest";
    /// <summary>The "…" and the chat trigger sit this high over a seated guest's feet, metres, when their body has no top.</summary>
    public const float ChestHeight = .85f;
    /// <summary>How often the guests are looked over (a new one sat down, one got up), seconds.</summary>
    public const float LookEvery = .5f;

    private sealed class Holder
    {
        public string id = "";
        public PatronBrain guest;
        public GossipChat chat;
        public int mark;
        public Vector3 markAt;
    }

    private readonly List<string> untold = new List<string>();     // today's lines not yet told, in the day's order
    private readonly List<Holder> holders = new List<Holder>();
    private readonly List<PatronBrain> seated = new List<PatronBrain>();
    private int day = -1;
    private float nextLook;

    /// <summary>Until when (UiClock) Ace is "in" a chat: the guest's line is in the bubble (Downtime.InActivity).</summary>
    public float ChattingUntil { get; private set; } = -10f;
    /// <summary>Chats today.</summary>
    public int ChatsToday { get; private set; }
    /// <summary>The last line told today (its id), or "" (the labs).</summary>
    public string LastTold { get; private set; } = "";
    /// <summary>Today's lines not yet told (the labs).</summary>
    public IReadOnlyList<string> Untold => untold;
    /// <summary>How many guests hold a line now (the "…" over them).</summary>
    public int Holding => holders.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        DropAll();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) return;
        if (clock.Day != day) Morning(clock.Day);
        if (clock.DayOver || NightWalk.Instance != null && NightWalk.Instance.Active)
        {
            if (holders.Count > 0) DropAll();
            return;
        }
        if (Time.time < nextLook) return;
        nextLook = Time.time + LookEvery;
        Keep();
        if (untold.Count > holders.Count && Downtime.TeachingStarted) Deal();
    }

    // A new day: its lines, none dealt yet (a line the notebook already knows isn't told again).
    private void Morning(int dayNumber)
    {
        day = dayNumber;
        DropAll();
        untold.Clear();
        ChatsToday = 0;
        LastTold = "";
        CustomerSpawner spawner = Downtime.Spawner;
        DayDefinition def = spawner != null ? spawner.AuthoredDay(dayNumber) : null;
        string[] ids = def != null ? def.GossipOn(dayNumber) : null;
        if (ids == null) return;
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        foreach (string id in ids)
            if (NotebookEntries.GossipFor(id) != null && !untold.Contains(id) && (notebook == null || !notebook.Knows(id)))
                untold.Add(id);
    }

    // Who still holds a line: a guest who got up (or went) gives theirs back, to be dealt again; one who settled a little
    // differently has their "…" moved over them.
    private void Keep()
    {
        for (int i = holders.Count - 1; i >= 0; i--)
        {
            Holder h = holders[i];
            if (h.guest == null || !h.guest.isActiveAndEnabled || h.guest.IsLeaving || !h.guest.IsSeated)
            {
                Release(h);
                holders.RemoveAt(i);
                continue;
            }
            Vector3 at = MarkPoint(h.guest);
            if (h.mark == 0 || (at - h.markAt).sqrMagnitude > .25f * .25f)
            {
                if (h.mark != 0) Juice.Unmark(h.mark);
                h.mark = Juice.Mark(at, Juice.Icon.Dots);
                h.markAt = at;
            }
        }
    }

    // Each line not yet held goes to a seated guest without one: settled guests first (they stay longest), then anyone, by a
    // dice of its own that never touches the game's random sequence.
    private void Deal()
    {
        seated.Clear();
        GameObject theMan = LodgerDay.Instance != null ? LodgerDay.Instance.Visitor : null;
        foreach (PatronBrain p in FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude))
        {
            if (p == null || !p.isActiveAndEnabled || !p.IsSeated || p.IsLeaving) continue;
            if (theMan != null && p.gameObject == theMan) continue;
            if (HolderOf(p) != null) continue;
            seated.Add(p);
        }
        if (seated.Count == 0) return;
        // A stable order (by where they sit), so the dice picks the same guest for the same room.
        seated.Sort((a, b) =>
        {
            int settled = b.SettledGuest.CompareTo(a.SettledGuest);
            if (settled != 0) return settled;
            Vector3 pa = a.transform.position, pb = b.transform.position;
            int x = pa.x.CompareTo(pb.x);
            return x != 0 ? x : pa.z.CompareTo(pb.z);
        });
        foreach (string id in untold)
        {
            if (seated.Count == 0) return;
            if (Held(id)) continue;
            int settledCount = 0;
            foreach (PatronBrain p in seated) if (p.SettledGuest) settledCount++;
            int from = settledCount > 0 ? settledCount : seated.Count;
            int pick = SeatChoice.DiceIndex(this, (day * 131 + id.Length * 17 + untold.IndexOf(id)) ^ 0x6055, from);
            PatronBrain guest = seated[Mathf.Clamp(pick, 0, seated.Count - 1)];
            seated.Remove(guest);
            var h = new Holder { id = id, guest = guest };
            h.chat = GossipChat.Make(guest, id, ChestHeight);
            h.markAt = MarkPoint(guest);
            h.mark = Juice.Mark(h.markAt, Juice.Icon.Dots);
            holders.Add(h);
        }
    }

    private static Vector3 MarkPoint(PatronBrain guest)
    {
        Vector3 top = FocusNameUI.TopOf(guest.gameObject);
        if (top == Vector3.zero) top = guest.transform.position + Vector3.up * (ChestHeight + .45f);
        return top + Vector3.up * .22f;
    }

    private bool Held(string id)
    {
        foreach (Holder h in holders) if (h.id == id) return true;
        return false;
    }

    private Holder HolderOf(PatronBrain guest)
    {
        foreach (Holder h in holders) if (h.guest == guest) return h;
        return null;
    }

    private Holder HolderOf(GossipChat chat)
    {
        foreach (Holder h in holders) if (h.chat == chat) return h;
        return null;
    }

    private static void Release(Holder h)
    {
        if (h.mark != 0) Juice.Unmark(h.mark);
        h.mark = 0;
        if (h.chat != null) Destroy(h.chat.gameObject);
        h.chat = null;
    }

    private void DropAll()
    {
        foreach (Holder h in holders) Release(h);
        holders.Clear();
    }

    // ------------------------------------------------------------------ what the rest of the game asks

    /// <summary>The guest holding a line who is nearest <paramref name="from"/> and can be chatted to now (the Now line, Downtime).</summary>
    public bool TryNext(Vector3 from, out Component guest)
    {
        guest = null;
        float best = float.PositiveInfinity;
        foreach (Holder h in holders)
        {
            if (h.guest == null || h.chat == null || !h.chat.IsAvailable) continue;
            Vector3 d = h.guest.transform.position - from;
            d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; guest = h.guest; }
        }
        return guest != null;
    }

    /// <summary>Whether <paramref name="guest"/> holds a line now (the "…" over them).</summary>
    public bool Holds(PatronBrain guest) => guest != null && HolderOf(guest) != null;

    /// <summary>The line <paramref name="guest"/> holds (its id), or "".</summary>
    public string LineOf(PatronBrain guest)
    {
        Holder h = guest != null ? HolderOf(guest) : null;
        return h != null ? h.id : "";
    }

    /// <summary>The chat on the guest holding <paramref name="id"/>, or null (the labs walk Ace to it).</summary>
    public GossipChat ChatFor(string id)
    {
        foreach (Holder h in holders) if (h.id == id) return h.chat;
        return null;
    }

    /// <summary>Deal now, whatever the clock (the labs: a guest is seated and the teaching has begun).</summary>
    public void DealNow()
    {
        Keep();
        if (untold.Count > holders.Count) Deal();
    }

    /// <summary>
    /// E on a guest with a line (GossipChat): they say it in a bubble, the notebook learns its fact under the person it's
    /// about, and their "…" goes. One press; Ace is free at once.
    /// </summary>
    public void Chat(GossipChat chat)
    {
        Holder h = HolderOf(chat);
        if (h == null || h.guest == null) return;
        NotebookEntries.Gossip line = NotebookEntries.GossipFor(h.id);
        if (line != null) Barks.Say(h.guest.transform, SpeakerId, line.line);
        if (NotebookHooks.HeardGossip(h.id)) Sfx.Play2D("notebook.page");
        ChatsToday++;
        Downtime.Today.chats++;
        ChattingUntil = UiClock.Now + Downtime.ChatSeconds;
        LastTold = h.id;
        untold.Remove(h.id);
        holders.Remove(h);
        Release(h);
    }
}
