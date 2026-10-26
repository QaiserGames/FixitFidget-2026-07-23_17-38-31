using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// DEAD AIR, MEASURED (24 Oct 2026; Designer P's spec, Docs/Studio/sessions/2026-10-23/designs/pacing-downtime-spec.md §3.2 and
// §6.1, approved by him for building with his answers of 24 Oct)
//
// His words (22 Oct): "day one and two should really teach mostly but what i mean about dead air is really the things you could
// be doing when waiting for a customer to approach the counter." P's simulation of today's Day 1 found nobody needing Ace for
// 151 of its 420 s, and a customer already walking in for 127 of those: the dead air is mostly the walk, so the answer is
// things to do during it (the four waiting jobs he chose on 22 Oct), not shorter walks.
//
// One reading of the room, shared by everything that asks, so they can never disagree:
//   * SOMEBODY NEEDS ACE (the Now line's own reading, DayOneGuideUI): a customer at the counter to hear or decide; an accepted
//     customer whose device has a step left (on the tray, on the bench, finished and waiting to be handed back); a drink ordered
//     and not served (a repair customer's wish counts once they've said it); a support call to dial or ringing (on hold has
//     nothing to do until it rings); someone with the sweat drop who can be reassured.
//   * SOMETHING TO DO WHILE WAITING, in the Now line's order (§2.1): a refill when the machine is at 3 or fewer and the back
//     shelf has stock (it saves a stall later); a dirty table (seats are money); a seated guest with gossip; the thrift find
//     (offered once the day's first customer has been heard; on Day 1, once Grace's visit is over). A customer always wins:
//     these are offered only while nobody needs Ace.
//     26 Oct 2026 (Days and first table): A FIND ALREADY BEGUN COMES BEFORE A TABLE (and a chat): the refill, then the find
//     once it's on the bench's corner or in the close-up (ThriftShelf.Begun), then the tables, the gossip, a find not begun.
//     His Mac's Day 2 and 3 runs never finished a find: guests leave a cup about every half minute, every cup came before the
//     find, and a find left for a customer came back only once no cup was left, so 8-9 tables (6 s each) and 2 chats took
//     60-66 of the 60-105 s nobody needed Ace and left 0-45 s for a 35-70 s find. For the player: start the find and the Now
//     line keeps you on it until it's done (a customer still always wins); the cups wait, as cups do. A find not begun still
//     comes after the tables and the gossip, as before, until it has been on offer for half a minute (integration, the same
//     afternoon: FindsTurn): then it gets its turn before the cups too, or a busy day of cups would never point at it.
//   * NOTHING TO DO: while the café is open or anyone is still inside, nobody needs Ace, there is nothing to do, and Ace isn't
//     in the middle of a waiting job (the find in the close-up, a crate in hand, a chat just said).
// CustomerSpawner asks it for the cap's retry and the quiet nudge (§3.1); the Now line for its quiet line (§6.4); DowntimeMeter
// samples it at 10 Hz into the day's counters, which the Day labs judge (§6.8; DayShapeCheck).
// ---------------------------------------------------------------------------
public static class Downtime
{
    /// <summary>The four things to do while waiting, in the Now line's order (and None).</summary>
    public enum Job { None, Refill, Table, Gossip, Find }

    /// <summary>The machine at this many cups or beans, or fewer, wants a refill from the back shelf (§2.5: "low" at 3).</summary>
    public const int RefillAt = 3;

    /// <summary>The first this many seconds of a day are reported apart: the opener's walk under the goal card (§3.2).</summary>
    public const float OpeningSeconds = 15f;

    /// <summary>After a chat, Ace is "in it" this long (the guest's line in the bubble).</summary>
    public const float ChatSeconds = 2.5f;

    // ------------------------------------------------------------------ the room, read once a frame

    private static int readFrame = -1;
    private static readonly List<CustomerBrain> customers = new List<CustomerBrain>();
    private static CustomerSpawner spawner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        readFrame = -1;
        customers.Clear();
        spawner = null;
        today = new DowntimeDay();
        heardDay = -1;
        heard = false;
    }

    private static List<CustomerBrain> Customers
    {
        get
        {
            if (readFrame == Time.frameCount) return customers;
            readFrame = Time.frameCount;
            customers.Clear();
            // Disabled brains are included: a walk-in still on the street has its brain off until the door (CafeArrivals).
            customers.AddRange(Object.FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude));
            return customers;
        }
    }

    /// <summary>The café's customer spawner (cached), or null.</summary>
    public static CustomerSpawner Spawner
    {
        get
        {
            if (spawner == null) spawner = Object.FindAnyObjectByType<CustomerSpawner>();
            return spawner;
        }
    }

    // ------------------------------------------------------------------ somebody needs Ace (§3.2)

    /// <summary>Whether <paramref name="c"/> needs something from Ace right now (see the header).</summary>
    public static bool NeedsAce(CustomerBrain c)
    {
        if (c == null || !c.isActiveAndEnabled || c.IsLeaving) return false;
        if (c.CanHearIntake || c.CanDecide) return true;
        if (!c.WasAccepted) return false;
        if (c.HasDrinkOrder) return true;
        JobBase job = c.ActiveJob;
        if (job is HoldCallJob call)
        {
            // On hold there is nothing to do until it rings (and Connecting is the line going through).
            HoldCallRun.State phase = call.CurrentPhase;
            if (phase == HoldCallRun.State.NeedsDialing || phase == HoldCallRun.State.Ringing || call.IsComplete) return true;
            return DayOneGuideUI.Frustrated(c);
        }
        // A repair not yet handed back: on the tray, on the bench, or finished and waiting for the hand-back (a counter repair
        // too: its switch at the counter).
        if (job != null) return true;
        return DayOneGuideUI.Frustrated(c);
    }

    /// <summary>How many customers need Ace right now: "customer steps" (the cap's retry: at most one left, §3.1).</summary>
    public static int CustomerSteps()
    {
        int n = 0;
        foreach (CustomerBrain c in Customers) if (NeedsAce(c)) n++;
        return n;
    }

    /// <summary>Nobody needs Ace right now.</summary>
    public static bool NobodyNeedsAce
    {
        get
        {
            foreach (CustomerBrain c in Customers) if (NeedsAce(c)) return false;
            return true;
        }
    }

    /// <summary>A customer is on the way to the counter: still on the street (brain off), or inside and not yet at the counter.</summary>
    public static bool IsWalkingIn(CustomerBrain c)
    {
        if (c == null || c.IsLeaving) return false;
        if (!c.enabled) return true;                         // walking over from a car or a neighbour's door (CafeArrivals)
        return !c.WasAccepted && c.ReachedCounterAt < 0f;    // inside, drifting or walking to the counter
    }

    /// <summary>How many customers are on their way to the counter.</summary>
    public static int WalkingIn
    {
        get
        {
            int n = 0;
            foreach (CustomerBrain c in Customers) if (IsWalkingIn(c)) n++;
            return n;
        }
    }

    /// <summary>The customer walking in who will reach the counter first (the one who set off earliest), or null.</summary>
    public static CustomerBrain NextToArrive
    {
        get
        {
            CustomerBrain pick = null;
            float earliest = float.PositiveInfinity;
            foreach (CustomerBrain c in Customers)
            {
                if (!IsWalkingIn(c)) continue;
                float since = c.enabled ? c.ArrivedAt : -1f;   // on the street: they set off before anyone already inside
                if (pick == null || since < earliest) { pick = c; earliest = since; }
            }
            return pick;
        }
    }

    /// <summary>Anyone at all is still inside (a customer, walking in or waiting).</summary>
    public static bool AnyoneInside
    {
        get
        {
            foreach (CustomerBrain c in Customers) if (c != null && !c.IsLeaving) return true;
            return false;
        }
    }

    /// <summary>A customer stands at the counter waiting to be heard (what ends the find's close-up by itself, §2.2, his p6).</summary>
    public static CustomerBrain AtTheCounter
    {
        get
        {
            foreach (CustomerBrain c in Customers)
                if (c != null && c.isActiveAndEnabled && !c.IsLeaving && c.CanHearIntake) return c;
            return null;
        }
    }

    // ------------------------------------------------------------------ the day's teaching

    private static int heardDay = -1;
    private static bool heard;

    /// <summary>The day's first customer has been heard (their intake taken or their mind made up) today.</summary>
    public static bool FirstCustomerHeard
    {
        get
        {
            DayClock clock = DayClock.Instance;
            int d = clock != null ? clock.Day : -1;
            if (heardDay != d) { heardDay = d; heard = false; }
            if (!heard)
                foreach (CustomerBrain c in Customers)
                    if (c != null && (c.WasAccepted || c.CanDecide)) { heard = true; break; }
            return heard;
        }
    }

    /// <summary>
    /// The day's teaching has begun, so a waiting job that teaches by itself (the find, a guest's gossip) can't compete with the
    /// day's first card: on Day 1 once the featured visit is over (P's §2.2: "on Day 1, once Grace's visit is over"), on other
    /// days once the first customer has been heard (§3.2). A refill and a dirty table aren't held back: they're the café's.
    /// </summary>
    public static bool TeachingStarted
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (clock == null) return false;
            if (clock.Day <= 1)
            {
                CustomerSpawner s = Spawner;
                return s == null || s.FeaturedVisitOver;
            }
            return FirstCustomerHeard;
        }
    }

    // ------------------------------------------------------------------ what there is to do while waiting (§2.1)

    /// <summary>Whether the machine wants a refill from the back shelf now (§2.5).</summary>
    public static bool RefillWanted
    {
        get
        {
            ShopInventory stock = ShopInventory.Instance;
            return stock != null && stock.RefillWanted && !CarryingCrate;
        }
    }

    /// <summary>A crate of cups and beans is in Ace's hands (on its way to the machine).</summary>
    public static bool CarryingCrate
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;
            if (carry == null) return false;
            for (int i = 0; i < carry.Count; i++) if (carry.GetItem(i) is StockCrate) return true;
            return false;
        }
    }

    /// <summary>The used cup nearest <paramref name="from"/> on any table (the man's mess or a guest's), or null.</summary>
    public static DirtyCup NearestDirtyCup(Vector3 from)
    {
        DirtyCup best = null;
        float nearest = float.PositiveInfinity;
        foreach (DirtyCup cup in DirtyCup.Live)
        {
            if (cup == null || !cup.OnTheTable) continue;
            Vector3 d = cup.transform.position - from;
            d.y = 0f;
            float m = d.sqrMagnitude;
            if (m < nearest) { nearest = m; best = cup; }
        }
        return best;
    }

    /// <summary>The thrift find may be offered now: one on the shelf, unfinished, and the day has begun its teaching (§2.2).</summary>
    public static bool FindWanted => ThriftShelf.Instance != null && ThriftShelf.Instance.Offered;

    // 26 Oct 2026 (integration, his Mac's afternoon run): THE FIND GETS ITS TURN. Guests leave a cup about every half minute, and
    // a find nobody has begun waited behind every one of them: on his Mac's Day 2 and 3 runs it was begun only on days with a
    // quiet start, so a player following the Now line might never be pointed at it. So once the find has been on offer for
    // FindTurnSeconds of the day, it comes before the tables and the gossip (a cup waits; a customer still always wins).
    /// <summary>How long the find is on offer before the Now line puts it ahead of the tables, seconds of the day.</summary>
    public const float FindTurnSeconds = 30f;
    static int findOfferedDay = -1;
    static float findOfferedAt;

    /// <summary>The find has been on offer today for at least Find Turn Seconds (false with no day clock: Edit mode).</summary>
    public static bool FindsTurn
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (!FindWanted || clock == null) return false;
            if (findOfferedDay != clock.Day) { findOfferedDay = clock.Day; findOfferedAt = clock.SecondsIntoDay; }
            return clock.SecondsIntoDay - findOfferedAt >= FindTurnSeconds;
        }
    }

    /// <summary>
    /// The first thing to do while waiting, in the Now line's order: a refill, a dirty table, a guest with gossip, the find.
    /// <paramref name="target"/> is what it's about (the back shelf, a cup, the guest, the find), for the pin. None (and null)
    /// when there is nothing to do.
    /// </summary>
    public static Job Next(Vector3 from, out Component target)
    {
        target = null;
        if (RefillWanted)
        {
            target = BackShelf.Instance;
            return Job.Refill;
        }
        // 26 Oct 2026 (Days and first table): a find already begun is finished before a cup (see the header); and (integration)
        // one on offer for half a minute gets its turn before the cups too (FindsTurn).
        if (FindWanted && (ThriftShelf.Instance.Begun || FindsTurn))
        {
            target = ThriftShelf.Instance.Find;
            return Job.Find;
        }
        DirtyCup cup = NearestDirtyCup(from);
        if (cup != null)
        {
            target = cup;
            return Job.Table;
        }
        if (GuestGossip.Instance != null && GuestGossip.Instance.TryNext(from, out Component guest))
        {
            target = guest;
            return Job.Gossip;
        }
        if (FindWanted)
        {
            target = ThriftShelf.Instance.Find;
            return Job.Find;
        }
        return Job.None;
    }

    /// <summary>Whether anything at all can be done while waiting now.</summary>
    public static bool SomethingToDo(Vector3 from) => Next(from, out _) != Job.None;

    /// <summary>The waiting job Ace is in the middle of now: the find in the close-up, a crate in hand, a chat just said; else None.</summary>
    public static Job InActivity
    {
        get
        {
            ItemInspector bench = ItemInspector.Instance;
            if (bench != null && SideProject.IsSideProject(bench.FocusedItem)) return Job.Find;
            if (CarryingCrate) return Job.Refill;
            if (GuestGossip.Instance != null && UiClock.Now < GuestGossip.Instance.ChattingUntil) return Job.Gossip;
            return Job.None;
        }
    }

    // ------------------------------------------------------------------ the day's counters (§3.2, §6.8), for the labs

    private static DowntimeDay today = new DowntimeDay();

    /// <summary>Today's counters (reset each morning by DowntimeMeter).</summary>
    public static DowntimeDay Today => today;

    /// <summary>A new day's counters (DowntimeMeter, each morning).</summary>
    public static void BeginDay(int day) => today = new DowntimeDay { day = day };

    /// <summary>
    /// One sample of the room, <paramref name="step"/> seconds of it (DowntimeMeter, 10 a second while the day runs).
    /// <paramref name="stretch"/> is the running stretch of nothing to do, kept by the meter.
    /// </summary>
    public static void Sample(float step, ref float stretch)
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) return;
        bool inside = AnyoneInside;
        if (!clock.IsOpen && !inside) { stretch = 0f; return; }
        DowntimeDay d = today;
        d.sampled += step;
        float seconds = clock.SecondsIntoDay;
        bool opening = seconds < OpeningSeconds;
        CustomerSpawner s = Spawner;
        bool afterLastOrders = s != null && s.LastOrdersForWalkIns || !clock.IsOpen;

        bool nobody = NobodyNeedsAce;
        Vector3 ace = PlayerCarry.Current != null ? PlayerCarry.Current.transform.position : Vector3.zero;
        Job doing = InActivity;
        Job next = nobody ? Next(ace, out _) : Job.None;
        if (nobody)
        {
            d.nobodyNeedsAce += step;
            if (WalkingIn > 0) d.nobodyNeedsAceWalkingIn += step;
        }
        switch (doing)
        {
            case Job.Find: d.onFind += step; break;
            case Job.Refill: d.onRefill += step; break;
            case Job.Gossip: d.onGossip += step; break;
        }
        // The Now line showing a waiting job while somebody needs Ace: never (§6.8).
        if (!nobody && DayOneGuideUI.ShowingWaitingJob != Job.None) d.waitingLineWhileNeeded++;
        // The find offered before the day's featured visit is over: never on Day 1 (§6.8).
        if (DayOneGuideUI.ShowingWaitingJob == Job.Find && s != null && !s.FeaturedVisitOver) d.findBeforeVisitOver = true;

        bool nothing = nobody && next == Job.None && doing == Job.None;
        if (nothing)
        {
            d.nothingToDo += step;
            if (opening) d.nothingFirst15 += step;
            else if (afterLastOrders) d.nothingAfterLastOrders += step;
            else d.nothingRest += step;
            stretch += step;
            d.longestNothing = Mathf.Max(d.longestNothing, stretch);
            if (!opening) d.longestNothingAfter15 = Mathf.Max(d.longestNothingAfter15, stretch);
        }
        else stretch = 0f;
    }
}

/// <summary>One day's downtime counters (§3.2), seconds unless said. Read by the Day labs at the day's end.</summary>
public sealed class DowntimeDay
{
    public int day;
    /// <summary>Seconds sampled: the café open, or anyone still inside.</summary>
    public float sampled;
    /// <summary>Nothing to do: nobody needs Ace, nothing to do while waiting, Ace not in a waiting job.</summary>
    public float nothingToDo;
    /// <summary>...of it in the first 15 s (the opener's walk), after last orders, and the rest.</summary>
    public float nothingFirst15, nothingAfterLastOrders, nothingRest;
    /// <summary>Nobody needs Ace (whatever there is to do), and of that, with someone walking in.</summary>
    public float nobodyNeedsAce, nobodyNeedsAceWalkingIn;
    /// <summary>Seconds in each waiting job (clearing a table is one press: it has no seconds of its own).</summary>
    public float onFind, onRefill, onGossip;
    /// <summary>The longest stretch of nothing to do, and the longest after the first 15 s.</summary>
    public float longestNothing, longestNothingAfter15;
    /// <summary>Samples with a waiting job on the Now line while somebody needed Ace (the bar: 0).</summary>
    public int waitingLineWhileNeeded;
    /// <summary>The find was offered on the Now line before the day's featured visit was over (the bar: never, Day 1).</summary>
    public bool findBeforeVisitOver;
    /// <summary>Tables cleared, chats, refills, finds finished today (each job's own count, for the reports).</summary>
    public int tablesCleared, chats, refills, findsFinished;

    /// <summary>Nothing to do after the first 15 s (the §6.8 bar's measure).</summary>
    public float NothingAfterOpening => nothingToDo - nothingFirst15;

    public override string ToString() =>
        $"nothing to do {nothingToDo:0} s (first 15 s {nothingFirst15:0}, after last orders {nothingAfterLastOrders:0}, the rest {nothingRest:0}); " +
        $"longest stretch {longestNothing:0} s ({longestNothingAfter15:0} after the first 15 s); nobody needs Ace {nobodyNeedsAce:0} s " +
        $"({nobodyNeedsAceWalkingIn:0} with someone walking in); on the find {onFind:0} s, refills {onRefill:0} s, chats {onGossip:0} s; " +
        $"{tablesCleared} table(s) cleared, {chats} chat(s), {refills} refill(s), {findsFinished} find(s) finished; " +
        $"waiting line while needed {waitingLineWhileNeeded} sample(s); find before the visit was over: {(findBeforeVisitOver ? "yes" : "no")}";
}
