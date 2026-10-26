using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE DAY'S SHAPE AT THE COUNTER: A PLAY CHECK (playtest 4, 10 Oct 2026; the veteran's #3)
//
// Fixit Fidget > Playtest 4 > Systems > Lab C / D / E - The day's shape at the counter, Day 5 (the scene's 180 s day /
// the ship's 420 s day / autopilot at human speed): a lab session on Day 5, the autopilot serving (Playtest4SystemsSteps).
// The veteran measured that Day 5's authored Opening / Morning rush / Lull / Lunch rush / Last orders reached the
// counter as 0 / 2 / 3 / 1 / 3 customers, and that the first customer reached the till 41 s in, because walk-ins take
// 25-52 s (median 38) from the street to the till. The spawner now reads each phase at "now + the walk" and the day's
// first arrival is already on the pavement when the café opens (CustomerSpawner, CafeArrivals.TryArriveNearTheDoor).
//
// This watches one lab day from its opening to its closing and counts, per authored phase, how many customers REACHED
// THE COUNTER in it (CustomerBrain.ReachedCounterAt), and when the first did. Passes:
//   * the first customer at the till within 15 s of opening;
//   * each rush brought at least as many to the counter as the lull, and the busiest phase at the counter is a rush.
// The brief's stricter reading (the two rushes are the two busiest phases, and the lull the quietest after the opening)
// is reported as a note, with why: a 180 s day brings about ten people, one to three per phase, so one run is noisy
// (simulated from the measured walks, the autopilot and the café's two limits, it holds in about 3 runs in 4 at 180 s
// and about 9 in 10 at 420 s; the old shape held it in 1 in 6). The rushes are also limited by the counter itself: its
// three places are kept for people still walking over, so through a ~36 s walk the counter can take a customer about
// every 14 s at most (both rushes are authored faster, 6-7 s); the report says how often all three were taken.
//
// LAST ORDERS (12 Oct 2026; the playtesters' #9, the Producer's call: nobody new reaches the till after closing, everyone
// already inside is served). On the human-speed Day 5 two walk-ins who set off before 8 PM reached the till 6.5 and
// 11.7 s after it and service ran 67 s past closing. The spawner now calls last orders for walk-ins when less than the
// longest walk to the till is left on the clock (CustomerSpawner.LastOrdersLead); named visits keep their rules, and
// CafeArrivals still turns back anyone on the street at closing. So the check now watches on past closing until the
// day ends, and passes:
//   * last orders for walk-ins were called before closing, and no walk-in set off after them;
//   * nobody reached the till after closing;
//   * the day ended only once everyone who was inside at closing had been seen to (served, or gone of their own accord),
//     and within a limit (nobody cut off by the day's end).
// It reports how long service ran past closing (the veteran's run 2: 67 s) and who was inside at closing, with how
// each left.
//
// Optional: the ship's day length (420 s), set on the clock as the day opens (DayClock's own length, not saved), and the
// autopilot at human speed (CafeLab.HumanSpeedKey). Photos: the first customer at the till from the game's camera, the
// middle of each phase, closing time and the day's end. Report and photos: Logs/CafeLife/day-shape-<time>/. Nothing is
// saved in the scene; the lab's own save is the only file the game writes.
//
// DAYS 1-3 (14 Oct 2026; specs/days-1-3.md §10, the feel targets' T19): with a day number (Fixit Fidget > Days > Lab - Day N
// at the counter) the check judges that day by the spec's own bars instead of Day 5's rushes and lull, at the scene's 420 s:
//                          Day 1                     Day 2         Day 3
//   first walk-in at till  >= 2:00 (after the Officer) >= 1:30     -
//   Grace handed back      <= 2:00                    <= 2:00       her latte <= 1:30
//   storm-outs             0                          0             <= 1 (median)
//   the day ends           <= 7:30                    <= 7:30       <= 8:30
//   walk-in repairs        only "Gunked up"           only "Gunked up"  only the listed three, in order
// Each run adds a line to Logs/CafeLife/days-1-3-runs.csv; the report gives that day's medians over every run so far (the
// spec asks for 20 runs a day: one Play session each).
//
// DEAD AIR (24 Oct 2026; Designer P's spec §3.2 and §6.8, with the autopilot at human speed doing the four things to do while
// waiting, CafeLabDirector.WaitingJobs). Downtime's counters for the day (DowntimeMeter) and P's bars:
//                                                    Day 1    Day 2    Day 3
//   nothing to do after the first 15 s, median of 3   <= 50    <= 25    <= 30   (s)
//   the longest stretch of it, this run               <= 25    <= 20    <= 20   (s)
//   a waiting job on the Now line while needed        0        0        0      (samples)
//   the find offered before Grace's visit is over     never    -        -
//   bench jobs done (customers' and the find), med.   >= 3     >= 3     >= 3
//   a walk-in held back by the cap > 2 s, Ace <= 1    0        0        0
// And two of the 23 Oct run's misses, judged on the median of the day's last 3 runs as the seat medians are (each run's own
// number is still noted): THE DAY ENDS by its time (3 of 9 runs missed 7:30, each time a walk-in repair set off just before
// last orders: walk-in repairs' last orders now come a repair's length earlier, CustomerSpawner.RepairLastOrdersLead), and
// THE FIRST CUSTOMER AT THE TILL within 15 s (13.7-15.5 s). Its cause is only the random sequence: the opener's movement
// profile is rolled by weight from Unity's Random (NpcProfileLibrary.Roll), and the five profiles walk at 0.86-1.18x, so the
// same walk from the street takes 13.5-15.5 s; the seat dice of 23 Oct drew more numbers first, which changed the profile she
// drew. The bar stays 15 s. These go in Logs/CafeLife/days-1-3-downtime.csv (one row a run), beside the runs file.
//
// SEAT VARIETY (23 Oct 2026; his note: drink customers "only sit to the left of the couch", repair customers "only sit on
// the right next to the bar stool": "I want that to be more spread out and diverse"). People choose seats by their reasons
// now (SeatChoice, in SeatTags.cs). The check records every seat a customer claims (CustomerBrain.ClaimedSpot; a patron's
// too, reported) with its zone (TableSeat.Zone).
//   * Each run on Days 1-3 Checks only what can't be chance: the room's seat areas are laid (Fixit Fidget > Cafe redesign >
//     Seat tags 1: without them nobody finds a window, and a zone is a seat's group, so the banquette's three 2-tops would
//     count as three zones), and the day's customers sat down at all.
//   * It notes four numbers and adds them to the runs file's row, after the result: the different seats used; the zones;
//     the busiest share of the seats the scorer chose (a bench job at the repair bar is left out: ClaimRepairBar gives it the
//     middle stool whenever that's free, by design, so two or three of them would break any share alone); the zones the
//     drink customers sat in (left out with fewer than two of them).
//   * From the third run of a day on, the medians of that day's last 3 runs (Seat Runs For Median) are Checked, as Day 3's
//     storm-outs are: at least 4 seats and 3 zones, a busiest chosen share of at most 50 %, the drinks in at least 2 zones.
//     In a café with fewer than 10 seats in 4 zones (Room At Least: the furniture spec's half-finished Day 1) they are
//     reported instead: there the room, not the choosing, limits a day. Today's café has 28 seats in 7 zones.
// Why medians: one Day 1 brings about seven sittings, too few to judge alone. Two models of a day disagree on the numbers,
// so here is what each assumed. Mine (23 Oct, after the review's fixes): Grace at 9 s with her camera, the Officer at
// 95 s, five walk-ins from 156 s 25-90 s apart, eight in ten for a drink, Cheerful or Chatty with a rolled way of moving,
// drinks staying 20-40 s and repairs 45-90, patrons from the door every 5-19 s, straight walks plus 12 % and 0.4 m, every
// customer from the till: a good build passed all four on 89 % of single runs and on 98 % of medians of three, the old
// nearest rule on none of either. With six sittings 83 % and 95 %; with ten, closer together, 98 % and 99.9 % (the old rule
// 3 % and 0.5 %). The reviewer's (23 Oct; the Officer, Grace, Cheerful and Chatty walk-ins, 55 % drinks, one pair of hands,
// patrons from the door, straight walks; before the standing-wait fix): all four on 98 % of runs at twelve sittings, 85 %
// at eight, 72 % at six, and the old rule on 59 %, 34 % and 23 % (busier days spread even the nearest rule, as seats fill).
// Neither model is the game, so the medians are what is judged, and the deterministic proof is in Edit Mode: Fixit Fidget >
// Checks > Seat choice rules, section 7, on the open café's own seats and walks.
// Also reported: the bench jobs who sat away from the repair bar (none expected on Day 1: its walk-ins' short cleans keep
// them there) and where the patrons sat. On Day 5's shape everything is reported, not judged.
//
// THE HALF-FINISHED CAFÉ (24 Oct 2026, Furniture; furniture-spec.md §5.5 with his answers r1 and r3): Fixit Fidget > Days >
// Lab - Day N at the counter (half-finished café) (FurnitureLabMenus) runs the same check on the café a new game opens (10
// seats, the dust sheets lying; Days 2 and 3 with the week's bought pieces: 12 and 14 seats). Its runs file is its own
// (Logs/CafeLife/HalfFinished/), so its medians never mix with today's 28-seat café's. At the closing FurnitureCheck.AtClosing
// adds its lines: on Day 1 the Shop's badge and YOUR CAFÉ card and the first table placed from Arrange; on Days 2 and 3
// what goes on sale. The day's own bars are P2's, unchanged.
// ---------------------------------------------------------------------------
public sealed class DayShapeCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.DayShape.Check";
    public const string ShipLengthKey = "FixitFidget.DayShape.ShipLength";
    /// <summary>Days 1-3: the day whose bars to judge (specs/days-1-3.md §10); absent or 0 for Day 5's shape.</summary>
    public const string DayKey = "FixitFidget.DayShape.Day";
    public const float FirstAtTheTillWithin = 15f;
    public const float ShipDayLength = 420f;
    // After closing, how long the day may run on before the check calls it: a repair accepted just before closing at
    // human speed (45-90 s), a drink owed after it (20-40 s), the walk out; the clock's own deadlock grace is 90 s.
    public const float ServiceAfterClosingAtMost = 240f;
    // The day's first arrival sets off half a second in, already near the door (CustomerSpawner): not a walk from the street.
    const float OpenerSeenBefore = 2f;

    /// <summary>0: the day as the scene has it (420 s since 14 Oct; it was 180); otherwise this many seconds (the ship's 420).</summary>
    public float dayLength;

    /// <summary>0: Day 5's shape (the rushes and the lull); 1-3: that day's own bars (specs/days-1-3.md §10).</summary>
    public int dayNumber;

    /// <summary>24 Oct 2026 (Furniture): the day runs on the half-finished café (FurnitureLabMenus' Days labs; FurnitureCheck.AtClosing at its closing).</summary>
    public bool halfFinished;

    protected override string Title => "The day's shape at the counter - play check" +
                                       (dayNumber > 0 ? $", Day {dayNumber} by the Days 1-3 bars" : "") +
                                       (dayLength > 0f ? $", a {dayLength:0} s day" : "") +
                                       (halfFinished ? ", on the half-finished café" : "") +   // 24 Oct 2026 (Furniture)
                                       (CafeLabDirector.Instance != null && CafeLabDirector.Instance.HumanSpeed ? ", the autopilot at human speed" : "");
    protected override string Tag => "[Day shape check]";

#if UNITY_EDITOR
    // Asked for by the editor (Playtest4SystemsSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        bool ship = PlayerPrefs.GetInt(ShipLengthKey, 0) == 1;
        int number = PlayerPrefs.GetInt(DayKey, 0);
        bool half = PlayerPrefs.GetInt(FurnitureCheck.HalfFinishedKey, 0) == 1;   // 24 Oct 2026 (Furniture)
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(ShipLengthKey);
        PlayerPrefs.DeleteKey(DayKey);
        PlayerPrefs.DeleteKey(FurnitureCheck.HalfFinishedKey);   // 24 Oct 2026 (Furniture)
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Day shape check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Day shape check (this Play session only)");
        var check = go.AddComponent<DayShapeCheck>();
        check.dayLength = ship ? ShipDayLength : 0f;
        check.dayNumber = Mathf.Clamp(number, 0, 3);
        check.halfFinished = half;   // 24 Oct 2026 (Furniture): its folder, and so its runs file, under HalfFinished
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeLife", half ? "HalfFinished" : "",
            (check.dayNumber > 0 ? $"days-1-3-day{check.dayNumber}-" : "day-shape-") + $"{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    sealed class Arrival
    {
        public string name;
        public float seen = -1f, atCounter = -1f;
        public bool counted;
        // Last orders (12 Oct): a named visit (a regular) or a walk-in; what became of them, kept up to date while they are
        // in the café (their brain is gone once they reach the door).
        public bool regular, accepted, served, insideAtClosing, leavingAtClosing;
        public string state = "";
        public float gone = -1f;
        // Days 1-3 (14 Oct): who it is, what they came with, and when their repair came back and when they were served.
        public bool grace;
        public string job = "";
        // 24 Oct 2026 (Days): a support call or a counter repair: not a bench job (P's "bench jobs done" bar).
        public bool call, counter;
        public float returnedAt = -1f, servedAt = -1f;
    }

    static readonly FieldInfo WasServedField = typeof(CustomerBrain).GetField("wasServed", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo StateField = typeof(CustomerBrain).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo RepairReturnedField = typeof(CustomerBrain).GetField("repairReturned", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly Dictionary<CustomerBrain, Arrival> people = new Dictionary<CustomerBrain, Arrival>();
    readonly List<Arrival> arrivals = new List<Arrival>();
    int fullCounterSamples, samples;
    // One name per person (20 Oct, this lab's own photo "03-build": "Valerie" pinned twice, Day 1's green pin over her head
    // and the name of what E acts on hanging under it, clear of Ace's head). Every look of the day counts it.
    int pinLooks, doubledNames;
    string firstDoubled = "";
    float closedAt = -1f, endedAt = -1f;
    bool firstPhoto;
    int lostAtEnd = -1;   // the day's storm-outs (DayClock.Lost) when it ended

    // ---------- seat variety (23 Oct 2026; the header) ----------

    /// <summary>What the medians of a day's last Seat Runs For Median runs must show (DaysBars judges them; one run only notes).</summary>
    public const int SeatsAtLeast = 4, ZonesAtLeast = 3, DrinkZonesAtLeast = 2;
    /// <summary>The most of the sittings the scorer chose that one seat may take, in the median (bench jobs at the repair bar left out).</summary>
    public const float OneSeatAtMost = .5f;
    public const int SeatRunsForMedian = 3;
    /// <summary>The room it takes to judge a day's seat variety (below this the room decides it: reported only).</summary>
    public const int RoomAtLeast = 10, RoomZonesAtLeast = 4;
    const string DrinkKind = "a drink";

    // This run's four numbers, for the runs file (-1: nothing to count), and whether the room was big enough to judge them.
    int runSeats = -1, runZones = -1, runDrinkZones = -1;
    float runShare = -1f;
    bool seatRoomy;

    sealed class Sitting
    {
        public string who = "", seat = "", zone = "", kind = "";
        public bool regular, benchJob, awayFromTheBar;
        public float at;
    }

    // The seat each person last claimed (a new claim, a new sitting), and every sitting: the customers', the patrons'.
    readonly Dictionary<Component, WaitingSpot> seatOf = new Dictionary<Component, WaitingSpot>();
    readonly List<Sitting> sittings = new List<Sitting>();
    readonly List<Sitting> patronSittings = new List<Sitting>();

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.IsOpen && !DayClock.Instance.DayOver
                                 && FindAnyObjectByType<CustomerSpawner>() != null, 30f, "the lab's day is open");
        if (!lastWait) yield break;
        DayClock clock = DayClock.Instance;
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        CounterQueue queue = FindAnyObjectByType<CounterQueue>();

        // The ship's length, set while the day is a moment old (the opener sets off half a second in).
        if (dayLength > 0f)
        {
            FieldInfo length = typeof(DayClock).GetField("dayLengthSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo remaining = typeof(DayClock).GetProperty(nameof(DayClock.TimeRemaining));
            Check(length != null && remaining != null, "the clock's day length can be set for this session");
            if (length != null && remaining != null)
            {
                length.SetValue(clock, dayLength);
                remaining.SetValue(clock, Mathf.Max(1f, dayLength - clock.SecondsIntoDay));
            }
        }
        float started = clock.SecondsIntoDay;
        float day = clock.SecondsIntoDay + clock.TimeRemaining;
        Note($"Day {clock.Day}, {day:0} s long; the check began {started:0.0} s into it.");
        Check(started < 3f, $"the check began as the day opened ({started:0.0} s in), so nobody's arrival is missed");

        // Who serves: the lab's autopilot (an instant, tireless server unless it is at human speed).
        CafeLabDirector lab = CafeLabDirector.Instance;
        Check(lab != null && lab.Autopilot, "the lab's autopilot serves the counter");

        yield return Until(() => spawner.Today != null, 5f, "the spawner has resolved today's authored day");
        DayDefinition today = spawner.Today;
        if (today == null || today.phases == null || today.phases.Length == 0) { Check(false, "today has authored phases"); yield break; }
        Note($"Today is {today.name}: {string.Join(" / ", today.phases.Select(p => $"{p.phaseName} from {p.startsAt:0.00} every {p.spawnInterval:0} s"))}.");
        Note($"The spawner looks {spawner.LeadSeconds:0.0} s ahead (the walk from the street to the counter; " +
             $"{(spawner.LeadSamples > 0 ? $"learned from {spawner.LeadSamples} walk(s) this session" : "its starting value, nobody measured yet")}).");
        Note($"Last orders for walk-ins: none sets off with less than {spawner.LastOrdersLead:0} s left on the clock " +
             $"(the longest walk to the till; {day - spawner.LastOrdersLead:0} s into this day).");
        Check(spawner.LastOrdersLead > 0f && spawner.LastOrdersLead < day,
            $"the walk-ins' last orders fall inside the day ({spawner.LastOrdersLead:0} s before closing of a {day:0} s day)");
        // 24 Oct 2026 (Days): a walk-in repair's last orders come a repair's length earlier than a drink's (the 23 Oct misses).
        Check(spawner.RepairLastOrdersLead > spawner.LastOrdersLead && spawner.RepairLastOrdersLead < day,
            $"walk-in repairs' last orders come earlier: none sets off with less than {spawner.RepairLastOrdersLead:0} s left " +
            $"({day - spawner.RepairLastOrdersLead:0} s into the day; drinks until {day - spawner.LastOrdersLead:0} s)");

        // Photos: the first customer at the till, and the middle of each phase.
        var photoAt = new List<(float at, string name)>();
        for (int i = 0; i < today.phases.Length; i++)
        {
            float from = today.phases[i].startsAt, to = i + 1 < today.phases.Length ? today.phases[i + 1].startsAt : 1f;
            photoAt.Add(((from + to) * .5f * day, $"{i + 2:00}-{Slug(today.phases[i].phaseName)}"));
        }

        // Watch until the café closes (last orders: no one reaching the counter after this counts for the day's shape).
        while (clock != null && clock.IsOpen && !clock.DayOver)
        {
            float now = clock.SecondsIntoDay;
            yield return Watch(now);
            if (queue != null)
            {
                samples++;
                if (queue.FreeSlotCount == 0) fullCounterSamples++;
            }
            for (int i = photoAt.Count - 1; i >= 0; i--)
            {
                if (now < photoAt[i].at) continue;
                string name = photoAt[i].name;
                photoAt.RemoveAt(i);
                yield return Photo(name);
            }
            yield return Seconds(.1f);
        }
        Check(clock != null && !clock.IsOpen, "the café reached closing time (last orders) while the check watched");
        if (clock == null) yield break;

        // Closing time (12 Oct): who is inside, and how they are. Then watch on until the day ends.
        closedAt = Mathf.Max(day, clock.SecondsIntoDay);
        int insideCount = 0, waitingOnAce = 0;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            Arrival a = Known(c, clock.SecondsIntoDay);
            a.insideAtClosing = true;
            a.leavingAtClosing = c.IsLeaving;
            if (!c.IsLeaving) { insideCount++; if (c.InService) waitingOnAce++; }
        }
        CafeArrivals arrivalsNow = CafeArrivals.Instance;
        Note($"Closing time: {insideCount} inside and not yet leaving ({waitingOnAce} of them with a job taken)" +
             (arrivalsNow != null ? $"; {arrivalsNow.TurnedBack} walker(s) turned back on the street at closing (CafeArrivals)" : "") + ".");
        yield return Photo($"{today.phases.Length + 2:00}-closing-time");

        float closingReal = Time.realtimeSinceStartup;
        while (!clock.DayOver && Time.realtimeSinceStartup - closingReal < ServiceAfterClosingAtMost)
        {
            yield return Watch(clock.SecondsIntoDay);
            yield return Seconds(.1f);
        }
        endedAt = clock.SecondsIntoDay;
        lostAtEnd = clock.Lost;
        int stillInside = FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Count(c => c != null && c.isActiveAndEnabled && !c.IsLeaving);
        Check(clock.DayOver, $"the day ended within {ServiceAfterClosingAtMost:0} s of closing (service ran {endedAt - closedAt:0.0} s past it)");
        Check(stillInside == 0, $"the day ended only once everyone inside had been seen to ({stillInside} still waiting when it ended)");
        yield return Photo($"{today.phases.Length + 3:00}-the-day-ends");

        Check(doubledNames == 0, $"one name per person: no look of the day had the same name pinned twice, Day 1's green pin and " +
                                 $"the name of what E acts on ({doubledNames} of {pinLooks} looks{(doubledNames > 0 ? ", the first " + firstDoubled : "")})");
        Report(today, day, spawner);
        // 24 Oct 2026 (Furniture): the half-finished café's evening (furniture-spec.md §5.5).
        if (halfFinished) yield return FurnitureCheck.AtClosing(dayNumber, Check, Note, Photo);
    }

    // One look at everyone in the café (and on the street with a café brain): who has reached the counter, and how each
    // one is doing, kept up to date until they are gone.
    IEnumerator Watch(float now)
    {
        UiPins pins = UiPins.Instance;
        if (pins != null)
        {
            pinLooks++;
            if (pins.FocusShowing && pins.GuideShowing && pins.FocusText.Length > 0 && pins.FocusText == pins.GuideText)
            {
                doubledNames++;
                if (firstDoubled.Length == 0) firstDoubled = $"\"{pins.FocusText}\" at {now:0.0} s";
            }
        }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (c == null) continue;
            Arrival a = Known(c, now);
            if (c.isActiveAndEnabled)
            {
                a.name = c.CustomerName;
                a.accepted |= c.WasAccepted;
                if (WasServedField != null && WasServedField.GetValue(c) is bool served && served) a.served = true;
                if (StateField != null) a.state = StateField.GetValue(c)?.ToString() ?? "";
                a.gone = now;
                if (a.servedAt < 0f && a.served) a.servedAt = now;
                if (a.returnedAt < 0f && RepairReturnedField != null && RepairReturnedField.GetValue(c) is bool returned && returned) a.returnedAt = now;
                if (a.job.Length == 0 && c.Record != null)
                    a.job = c.Record.kind == JobKind.Repair ? $"{c.Record.deviceName}: {c.Record.faultDescription}" : "a drink";
                if (c.ActiveJob is HoldCallJob) a.call = true;          // 24 Oct 2026 (Days)
                if (c.IsCounterRepair) a.counter = true;
                NoteSeat(c, c.ClaimedSpot, now);   // seat variety (23 Oct)
            }
            if (!a.counted && c.ReachedCounterAt >= 0f)
            {
                a.counted = true;
                a.name = c.CustomerName;
                a.atCounter = c.ReachedCounterAt;
                arrivals.Add(a);
                if (!firstPhoto)
                {
                    firstPhoto = true;
                    Note($"The first at the till: {a.name}, {a.atCounter:0.0} s after opening.");
                    yield return Photo("01-the-first-customer-at-the-till");
                }
            }
        }
        // Seat variety (23 Oct): where the patrons sit as well (reported, not judged).
        foreach (PatronBrain p in FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude))
            if (p != null && p.isActiveAndEnabled) NoteSeat(p, p.Seat, now);
    }

    Arrival Known(CustomerBrain c, float now)
    {
        if (people.TryGetValue(c, out Arrival a)) return a;
        a = new Arrival
        {
            name = c.CustomerName, seen = now, regular = c.Identity != null && c.Identity.IsRegular,
            grace = c.Identity != null && c.Identity.Profile != null && c.Identity.Profile.PersistentId == GraceCameraEpisode.ProfileId
        };
        people[c] = a;
        return a;
    }

    void Report(DayDefinition today, float day, CustomerSpawner spawner)
    {
        report.AppendLine();
        report.AppendLine($"Reaching the counter, per authored phase ({arrivals.Count} customers in a {day:0} s day):");
        var counts = new int[today.phases.Length];
        var rates = new float[today.phases.Length];
        for (int i = 0; i < today.phases.Length; i++)
        {
            float from = today.phases[i].startsAt * day, to = (i + 1 < today.phases.Length ? today.phases[i + 1].startsAt : 1f) * day;
            counts[i] = arrivals.Count(a => a.atCounter >= from && a.atCounter < to);
            rates[i] = to > from ? counts[i] / ((to - from) / 60f) : 0f;
            report.AppendLine($"   {today.phases[i].phaseName,-14} {from,5:0}-{to,4:0} s, authored every {today.phases[i].spawnInterval,2:0} s: " +
                              $"{counts[i]} at the counter ({rates[i]:0.0} a minute)");
        }
        int afterClosing = arrivals.Count(a => a.atCounter >= day);
        report.AppendLine($"   after closing: {afterClosing} at the counter (12 Oct: nobody new reaches the till after closing)");
        report.AppendLine("   (10 Oct, before: Opening 0 / Morning rush 2 / Lull 3 / Lunch rush 1 / Last orders 3; the first at the till at 41 s.)");
        foreach (Arrival a in arrivals.OrderBy(x => x.atCounter))
            report.AppendLine($"     {a.atCounter,6:0.0} s  {a.name}{(a.regular ? " (a regular)" : "")}" +
                              (a.seen >= OpenerSeenBefore ? $"  (on the way {a.atCounter - a.seen:0.0} s)" : "  (on the way already at the open)") +
                              (a.atCounter >= day ? "  AFTER CLOSING" : ""));
        var walks = arrivals.Where(a => a.seen >= OpenerSeenBefore).Select(a => a.atCounter - a.seen).OrderBy(x => x).ToList();
        if (walks.Count > 0)
            Note($"From the street to the counter: {walks.First():0}-{walks.Last():0} s, median {walks[walks.Count / 2]:0} s " +
                 $"({walks.Count} walk(s); the spawner looked {(spawner != null ? spawner.LeadSeconds : 0f):0} s ahead by the end).");
        if (samples > 0)
            Note($"The counter's three places were all taken (by people at it or walking over) {100f * fullCounterSamples / samples:0}% of the day: " +
                 "while they are, nobody new sets off.");

        // The first at the till.
        float first = arrivals.Count > 0 ? arrivals.Min(a => a.atCounter) : -1f;
        firstAtTill = first;
        // 24 Oct 2026 (Days): on Days 1-3 this run's time is noted and the median of the day's last 3 runs is judged (DaysBars;
        // the header: the spread is only the opener's rolled walking pace). Other days judge each run, as before.
        string firstLine = $"the first customer reached the till {(first >= 0f ? $"{first:0.0} s" : "never")} after opening (within {FirstAtTheTillWithin:0} s; was 39-41 s)";
        if (dayNumber >= 1 && dayNumber <= 3) Note(firstLine + "; judged on the median of the day's last 3 runs");
        else Check(first >= 0f && first <= FirstAtTheTillWithin, firstLine);

        // Last orders (12 Oct).
        LastOrders(day, spawner);

        // Seat variety (23 Oct): this run's sittings and numbers (two Checks on Days 1-3); their medians in DaysBars.
        SeatVariety();

        // Days 1-3 (14 Oct): the day's own bars, not Day 5's rushes and lull.
        if (dayNumber >= 1 && dayNumber <= 3)
        {
            DaysBars(today, day, spawner);
            return;
        }

        // The rushes and the lull: the two phases authored busiest, and the quietest after the first.
        int[] byInterval = Enumerable.Range(0, today.phases.Length).OrderBy(i => today.phases[i].spawnInterval).ToArray();
        if (today.phases.Length < 4)
        {
            Note("Today has fewer than four phases: the rushes-and-lull checks are for Day 5 (Two rushes).");
            return;
        }
        int rushA = byInterval[0], rushB = byInterval[1];
        int lull = Enumerable.Range(1, today.phases.Length - 1).OrderByDescending(i => today.phases[i].spawnInterval).First();
        string rushes = $"{today.phases[rushA].phaseName} ({counts[rushA]}) and {today.phases[rushB].phaseName} ({counts[rushB]})";
        Check(counts[rushA] >= counts[lull] && counts[rushB] >= counts[lull],
            $"each rush brought at least as many to the counter as the lull: {rushes} against {today.phases[lull].phaseName} ({counts[lull]})");
        int busiest = counts.Max();
        Check(counts[rushA] == busiest || counts[rushB] == busiest,
            $"the busiest phase at the counter is a rush ({busiest} at most; {rushes})");

        // The brief's stricter reading, reported.
        bool topTwo = Enumerable.Range(0, counts.Length).Where(i => i != rushA && i != rushB).All(i => counts[i] <= Mathf.Min(counts[rushA], counts[rushB]));
        bool quietest = Enumerable.Range(1, counts.Length - 1).All(i => counts[lull] <= counts[i]);
        Note($"The stricter reading: the two rushes are the two busiest phases ({(topTwo ? "yes" : "no")}), and the lull the quietest " +
             $"after the opening ({(quietest ? "yes" : "no")}). With one to three people a phase, one run is noisy: see the header.");
    }

    // Nobody new reaches the till after closing; everyone inside is served (12 Oct, #9).
    void LastOrders(float day, CustomerSpawner spawner)
    {
        report.AppendLine();
        report.AppendLine("Last orders (12 Oct: nobody new reaches the till after closing; everyone already inside is served):");
        float called = spawner != null ? spawner.LastOrdersCalledAt : -1f;
        float lead = spawner != null ? spawner.LastOrdersLead : 0f;
        Check(called >= 0f && called < day,
            $"the spawner called last orders for walk-ins {(called >= 0f ? $"{called:0.0} s into the day, {day - called:0.0} s before closing" : "never")} " +
            $"(the longest walk to the till: {lead:0} s" + (spawner != null && spawner.LongestWalkMeasured > 0f ? $"; the longest measured this session {spawner.LongestWalkMeasured:0.0} s" : "") + ")");

        // Walk-ins seen for the first time after last orders: the spawner sent them (a regular's visit keeps its own rule).
        var lateWalkIns = people.Values.Where(a => !a.regular && called >= 0f && a.seen > called + .5f).ToList();
        Check(called >= 0f && lateWalkIns.Count == 0,
            $"no walk-in set off after last orders ({lateWalkIns.Count}" +
            (lateWalkIns.Count > 0 ? ": " + string.Join(", ", lateWalkIns.Select(a => $"{a.name} at {a.seen:0.0} s")) : "") + ")");
        var namedLate = people.Values.Where(a => a.regular && called >= 0f && a.seen > called + .5f).ToList();
        if (namedLate.Count > 0)
            Note($"Named visits that set off after last orders, by their own rules: {string.Join(", ", namedLate.Select(a => $"{a.name} at {a.seen:0.0} s"))}.");

        var late = arrivals.Where(a => a.atCounter >= day).ToList();
        Check(late.Count == 0, $"nobody new reached the till after closing ({late.Count}" +
                               (late.Count > 0 ? ": " + string.Join(", ", late.Select(a => $"{a.name} {a.atCounter - day:0.0} s after")) : "") +
                               "; the human-speed day of 11 Oct: 2, at 6.5 and 11.7 s after)");

        // Who was inside at closing, and how each left.
        var inside = people.Values.Where(a => a.insideAtClosing && !a.leavingAtClosing).OrderBy(a => a.atCounter).ToList();
        if (closedAt >= 0f && endedAt >= 0f)
            Note($"Service ran {endedAt - closedAt:0.0} s past closing (the veteran's human-speed day of 11 Oct: 67 s; all four of its losses came after 8 PM).");
        if (inside.Count == 0) Note("Nobody was inside at closing.");
        foreach (Arrival a in inside)
        {
            string how = a.served ? "served" : a.accepted ? $"job taken, left unserved ({a.state})" : $"never served ({a.state})";
            Note($"  inside at closing: {a.name}{(a.regular ? " (a regular)" : "")}, at the till {a.atCounter:0.0} s; {how}, gone by {a.gone:0.0} s");
        }
        int servedInside = inside.Count(a => a.served);
        Note($"Of the {inside.Count} inside at closing, {servedInside} were served; the rest left on their own patience or the autopilot's dice " +
             "(a decline, a lost race): the rule is that the day waits for them, not that every one is won.");
    }

    static string Slug(string name) => string.IsNullOrEmpty(name) ? "phase" : name.ToLowerInvariant().Replace(' ', '-');

    // ---------- seat variety (23 Oct 2026; the header) ----------

    // Every seat someone claims, once per claim (someone who gives up on a seat and takes another counts twice).
    void NoteSeat(Component who, WaitingSpot spot, float now)
    {
        if (who == null || !(spot is TableSeat seat)) return;
        if (seatOf.TryGetValue(who, out WaitingSpot was) && was == seat) return;
        seatOf[who] = seat;
        var s = new Sitting { seat = seat.name, zone = seat.Zone, at = now };
        if (who is CustomerBrain customer)
        {
            s.who = customer.CustomerName;
            s.regular = customer.Identity != null && customer.Identity.IsRegular;
            s.benchJob = customer.GoesToTheBench;
            s.kind = customer.Record == null ? "a visit" : customer.Record.kind == JobKind.Drink ? DrinkKind
                : customer.GoesToTheBench ? "a repair at the bench" : "a repair at the counter";
            s.awayFromTheBar = customer.GoesToTheBench && !seat.RepairBar;
            sittings.Add(s);
        }
        else if (who is PatronBrain patron)
        {
            s.who = patron.SettledGuest ? "a settled guest" : patron.IsLaptopPatron ? "a laptop patron" : patron.Companion != null ? "one of a pair" : "a patron";
            s.kind = "a patron";
            patronSittings.Add(s);
        }
    }

    void SeatVariety()
    {
        report.AppendLine();
        report.AppendLine("Seat variety (23 Oct 2026; his note: drink customers \"only sit to the left of the couch\", repair customers " +
                          "\"only sit on the right next to the bar stool\"; SeatChoice):");
        foreach (Sitting s in sittings)
            report.AppendLine($"   {Clock(s.at),5}  {s.who}{(s.regular ? " (a regular)" : "")}, {s.kind}: {s.seat} ({s.zone})" +
                              (s.awayFromTheBar ? "  - away from the repair bar" : ""));
        bool days13 = dayNumber >= 1 && dayNumber <= 3;
        // The room: today's café has 28 seats in 7 zones. A café still being furnished (the furniture spec's half-finished
        // Day 1) has its medians reported, not judged, until it has Room At Least seats in Room Zones At Least zones: there
        // the room, not the choosing, decides how varied a day can be.
        List<TableSeat> live = FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(t => t != null && t.isActiveAndEnabled).ToList();
        int liveZones = live.Select(t => t.Zone).Distinct().Count();
        seatRoomy = live.Count >= RoomAtLeast && liveZones >= RoomZonesAtLeast;
        if (days13 && !seatRoomy)
            Note($"The café has {live.Count} seats in {liveZones} zones: below {RoomAtLeast} seats in {RoomZonesAtLeast} zones the seat medians are reported, not judged.");

        // Per run, only what can't be chance. The room's areas (Seat tags 1): without them a seat knows only its own shape
        // (nobody finds a window), and a zone is its group's name, so the banquette's three 2-tops would pass as three zones.
        int areas = SeatTags.AreaCount;
        string areasLine = $"the room's seat areas are laid ({areas}): without them a seat knows only its own shape and its zone is " +
                           "its group's name (Fixit Fidget > Cafe redesign > Seat tags 1, then save the scene)";
        if (days13) Check(areas > 0, areasLine);
        else if (areas == 0) Note(areasLine);
        if (days13) Check(sittings.Count > 0, $"the day's customers sat down ({sittings.Count} sitting(s))");
        if (sittings.Count == 0)
        {
            if (!days13) Note("No customer sat down all day.");
            return;
        }

        // The four numbers, noted with what their medians must show: one day's handful of sittings is too few to judge on its
        // own (a good build missed them by chance on about one Day 1 run in nine in the 23 Oct model), so DaysBars judges the
        // medians of the day's last runs from the runs file, where these four go.
        runSeats = sittings.Select(s => s.seat).Distinct().Count();
        var zonesUsed = sittings.Select(s => s.zone).Distinct().ToList();
        runZones = zonesUsed.Count;
        // The busiest of the seats the scorer chose: a bench job at the repair bar was given its stool by ClaimRepairBar (the
        // middle one whenever it's free, by design), not chosen, so two or three of them would break any share by themselves.
        var chosen = sittings.Where(s => !(s.benchJob && !s.awayFromTheBar)).ToList();
        var busiest = chosen.GroupBy(s => s.seat).OrderByDescending(g => g.Count()).FirstOrDefault();
        runShare = busiest != null ? busiest.Count() / (float)chosen.Count : -1f;
        var drinks = sittings.Where(s => s.kind == DrinkKind).ToList();
        var drinkZones = drinks.GroupBy(s => s.zone).OrderByDescending(g => g.Count()).ToList();
        runDrinkZones = drinks.Count >= 2 ? drinkZones.Count : -1;

        Note($"Seats this run: {runSeats} different seats across {runZones} zones ({sittings.Count} sittings: {string.Join(", ", zonesUsed)}). " +
             $"The median of the day's last {SeatRunsForMedian} runs must be at least {SeatsAtLeast} seats and {ZonesAtLeast} zones.");
        Note(busiest != null
            ? $"The busiest seat the scorer chose this run: {busiest.Key}, {busiest.Count()} of {chosen.Count} ({runShare:P0}; bench jobs at the repair bar left out). " +
              $"The median must be at most {OneSeatAtMost:P0}."
            : "The scorer chose no seat this run: every sitting was a bench job at the repair bar (left out of the median).");
        Note(drinks.Count >= 2
            ? $"The drink customers this run: {drinks.Count} in {drinkZones.Count} zone(s) ({string.Join(", ", drinkZones.Select(g => $"{g.Key} {g.Count()}"))}). " +
              $"The median must be at least {DrinkZonesAtLeast}."
            : $"The drink customers this run: only {drinks.Count} sat down, nothing to spread (left out of the median).");
        var bench = sittings.Where(s => s.benchJob).ToList();
        Note($"Bench jobs: {bench.Count} sat down, {bench.Count(s => s.awayFromTheBar)} of them away from the repair bar " +
             "(their character wanting comfort more: CustomerBrain.StaysAtTheBar; none expected on a Day 1 of short cleans, about one in three later on).");
        if (patronSittings.Count > 0)
            Note($"Patrons: {patronSittings.Count} sittings at {patronSittings.Select(s => s.seat).Distinct().Count()} seats across " +
                 $"{patronSittings.Select(s => s.zone).Distinct().Count()} zones ({string.Join(", ", patronSittings.GroupBy(s => s.zone).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}).");
    }

    // ---------- Days 1-3 (14 Oct 2026; specs/days-1-3.md §10, T19) ----------

    // The bars, by day (index 1-3): the first walk-in at the till no sooner than, Grace handed back (Day 3: her latte served)
    // no later than, storm-outs at most, the day ended no later than (seconds from the opening).
    static readonly float[] FirstWalkInNoSooner = { 0f, 120f, 90f, 0f };
    static readonly float[] GraceDoneBy = { 0f, 120f, 120f, 90f };
    static readonly int[] StormOutsAtMost = { 0, 0, 0, 1 };
    static readonly float[] DayEndsBy = { 0f, 450f, 450f, 510f };
    const string RunsFile = "days-1-3-runs.csv";

    void DaysBars(DayDefinition today, float day, CustomerSpawner spawner)
    {
        int d = dayNumber;
        report.AppendLine();
        report.AppendLine($"Days 1-3: Day {d}'s bars (specs/days-1-3.md §10; T19: Days 1-2 lose nobody, Day 3 at most one, the median):");
        Check(DayClock.Instance != null && DayClock.Instance.Day == d, $"the lab played Day {d} ({(DayClock.Instance != null ? DayClock.Instance.Day : 0)})");
        Check(Mathf.Abs(day - 420f) < 1f, $"the day is {day:0} s (the scene's 420; Fixit Fidget › Days › A)");

        var walkIns = people.Values.Where(a => !a.regular).OrderBy(a => a.seen).ToList();
        var atTill = walkIns.Where(a => a.atCounter >= 0f).Select(a => a.atCounter).ToList();
        float firstWalkIn = atTill.Count > 0 ? atTill.Min() : -1f;
        if (FirstWalkInNoSooner[d] > 0f)
            Check(firstWalkIn < 0f || firstWalkIn >= FirstWalkInNoSooner[d],
                $"the first walk-in reached the till at {Clock(firstWalkIn)} (no sooner than {Clock(FirstWalkInNoSooner[d])}{(d == 1 ? ": after the Officer" : "")})");

        Arrival grace = people.Values.Where(a => a.grace).OrderBy(a => a.seen).FirstOrDefault();
        // The lab's autopilot ends a repair visit as served and leaving (CafeLabDirector: no device goes back through the hand-
        // back, so the brain's "repair returned" is never set): its served moment is the hand-back here (15 Oct, the first
        // Days lab on his Mac read "handed back never" for a Grace who left done at 1:27).
        float graceDone = grace == null ? -1f : d == 3 ? grace.servedAt : grace.returnedAt >= 0f ? grace.returnedAt : grace.servedAt;
        string graceWhat = d == 3 ? "Grace's latte served" : "Grace's camera handed back";
        Check(grace != null && graceDone >= 0f && graceDone <= GraceDoneBy[d],
            $"{graceWhat} at {Clock(graceDone)} (by {Clock(GraceDoneBy[d])}){(grace == null ? ": she never came" : "")}");
        // T11: the first repair, from her reaching the till to the hand-back, is at most half of Day 1 (the spec's ≈ 17 %).
        if (d == 1 && grace != null && grace.atCounter >= 0f && graceDone >= 0f)
            Check((graceDone - grace.atCounter) <= .5f * day,
                $"T11: the first repair took {graceDone - grace.atCounter:0} s from the till to the hand-back, {100f * (graceDone - grace.atCounter) / day:0}% of the day (at most 50%)");

        int stormOuts = Mathf.Max(0, lostAtEnd);
        Check(lostAtEnd >= 0 && stormOuts <= StormOutsAtMost[d], $"{stormOuts} storm-out(s) (at most {StormOutsAtMost[d]}{(d == 3 ? " in the median run" : "")})");
        // 24 Oct 2026 (Days): noted here; the median of the day's last 3 runs is judged (DeadAir), as the seat medians are.
        Note($"the day ended at {Clock(endedAt)} (by {Clock(DayEndsBy[d])}: judged on the median of the day's last 3 runs)");
        Check(endedAt >= 0f, "the day ended");

        // The walk-ins' repairs: the day's list, in order (a prefix of it: not every entry need come).
        var listed = new List<string>();
        foreach (FeaturedRepairRequest entry in today.WalkInRepairsOn(d))
            if (entry != null && entry.TryCreateJob(out Job job, out _)) listed.Add($"{job.deviceName}: {job.faultDescription}");
        var brought = walkIns.Where(a => a.job.Length > 0 && a.job != "a drink").Select(a => a.job).ToList();
        bool inOrder = brought.Count <= listed.Count && brought.Select((j, i) => j == listed[i]).All(ok => ok);
        Check(listed.Count > 0 && inOrder,
            $"the walk-ins brought [{string.Join(", ", brought)}] (only the day's list, in order: [{string.Join(", ", listed)}]); " +
            $"{walkIns.Count(a => a.job == "a drink")} came for a drink; the spawner handed out {(spawner != null ? spawner.WalkInRepairsUsed : 0)}");

        // This run, then every run of this day so far: the medians the spec's 20 runs a day are judged on. Since 23 Oct the row
        // ends with the seat variety's four numbers (SeatVariety), after the result, so every older read of it still works.
        string row = string.Join(",", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), d, day.ToString("0"), Num(firstWalkIn), Num(graceDone),
            stormOuts, Num(endedAt), "\"" + string.Join(" | ", brought) + "\"", failures == 0 ? "pass" : "fail",
            CsvWhole(runSeats), CsvWhole(runZones), CsvShare(runShare), CsvWhole(runDrinkZones));
        string file = Path.Combine(Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? folder, RunsFile);
        try
        {
            if (!File.Exists(file)) File.WriteAllText(file, RunsHeaderWithSeats + "\n");
            else
            {
                // A file from before 23 Oct: its header gains the four seat columns (its old rows simply have none).
                string[] lines = File.ReadAllLines(file);
                if (lines.Length > 0 && lines[0] == RunsHeader)
                {
                    lines[0] = RunsHeaderWithSeats;
                    File.WriteAllLines(file, lines);
                }
            }
            File.AppendAllText(file, row + "\n");
            var runs = File.ReadAllLines(file).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 7 && c[1] == d.ToString()).ToList();
            Note($"Day {d}: {runs.Count} run(s) recorded in {file} (the spec asks for 20). Medians: first walk-in at the till {Clock(Median(runs, 3))}, " +
                 $"{graceWhat} {Clock(Median(runs, 4))}, storm-outs {Median(runs, 5):0.#}, the day ended {Clock(Median(runs, 6))}.");
            if (d == 3 && runs.Count >= 3)
                Check(Median(runs, 5) <= 1f, $"Day 3: the median run loses at most one ({Median(runs, 5):0.#} over {runs.Count} runs)");
            SeatMedians(d, runs, file);
        }
        catch (Exception e) { Note("couldn't keep the runs file: " + e.Message); }

        DeadAir(d, walkIns, spawner);   // 24 Oct 2026 (Days)
    }

    // ---------- dead air (24 Oct 2026; P's spec §3.2 and §6.8; the header) ----------

    static readonly float[] NothingAfterOpeningAtMost = { 0f, 50f, 25f, 30f };
    static readonly float[] LongestNothingAtMost = { 0f, 25f, 20f, 20f };
    static readonly int[] BenchJobsAtLeast = { 0, 3, 3, 3 };
    public const int DeadAirRunsForMedian = 3;
    const string DeadAirFile = "days-1-3-downtime.csv";
    const string DeadAirHeader = "when,day,first at till,nothing after 15 s,longest after 15 s,nothing first 15 s,nothing after last orders," +
                                 "nothing rest,nobody needs Ace,with someone walking in,bench jobs,finds finished,tables cleared,chats,refills," +
                                 "waiting line while needed,cap waits over 2 s,day ended,result";
    float firstAtTill = -1f;

    void DeadAir(int d, List<Arrival> walkIns, CustomerSpawner spawner)
    {
        DowntimeDay t = Downtime.Today;
        report.AppendLine();
        report.AppendLine($"Day {d}: dead air (P's §3.2, §6.8), sampled 10 times a second: {t}.");
        CafeLabDirector lab = CafeLabDirector.Instance;
        if (lab != null)
            Note($"The autopilot's waiting jobs (at human speed only: {(lab.HumanSpeed ? "on" : "OFF, so these bars mean little")}): " +
                 $"{lab.TablesCleared} table(s) cleared, {lab.Chats} chat(s), {lab.Refills} refill(s), {lab.FindsWorked} time(s) at the find.");

        Check(t.longestNothingAfter15 <= LongestNothingAtMost[d] + .05f,
            $"the longest stretch of nothing to do after the first 15 s: {t.longestNothingAfter15:0.0} s (at most {LongestNothingAtMost[d]:0})");
        Check(t.waitingLineWhileNeeded == 0,
            $"no waiting job on the Now line while somebody needed Ace ({t.waitingLineWhileNeeded} sample(s))");
        if (d == 1) Check(!t.findBeforeVisitOver, "the find was never offered before the featured visit was over");
        int capWaits = spawner != null ? spawner.CapWaitsOver2s : 0;
        Check(capWaits == 0, $"no walk-in held back by the cap more than 2 s once a place freed with Ace on one job or none " +
                             $"({capWaits}; the longest {(spawner != null ? spawner.LongestCapWait : 0f):0.0} s)");

        // Bench jobs: the customers' repairs handed back at the bench (not a support call or a counter repair) and the finds.
        int customersBench = people.Values.Count(a => a.job.Length > 0 && a.job != "a drink" && !a.call && !a.counter
                                                      && (a.served || a.returnedAt >= 0f));
        int benchJobs = customersBench + t.findsFinished;
        Note($"Bench jobs done: {benchJobs} ({customersBench} customers', {t.findsFinished} find(s)); judged on the median of the day's last {DeadAirRunsForMedian} runs (at least {BenchJobsAtLeast[d]}).");
        Note($"Nothing to do after the first 15 s: {t.NothingAfterOpening:0} s (at most {NothingAfterOpeningAtMost[d]:0}: judged on the median of the day's last {DeadAirRunsForMedian} runs).");

        // c11 (his, kept on 23 Oct): from Day 2 about 2 in 5 walk-in bench jobs sit away from the repair bar; never on Day 1.
        var benchSat = sittings.Where(x => x.benchJob && !x.regular).ToList();
        Note($"c11: {benchSat.Count(x => x.awayFromTheBar)} of {benchSat.Count} walk-in bench job(s) sat away from the repair bar this run " +
             $"({(d == 1 ? "never on Day 1" : "about 2 in 5 from Day 2")}; Fixit Fidget > Checks > Seat choice rules proves the share over 4000 visits).");

        string row = string.Join(",", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), d, Num(firstAtTill), Num(t.NothingAfterOpening),
            Num(t.longestNothingAfter15), Num(t.nothingFirst15), Num(t.nothingAfterLastOrders), Num(t.nothingRest), Num(t.nobodyNeedsAce),
            Num(t.nobodyNeedsAceWalkingIn), benchJobs, t.findsFinished, t.tablesCleared, t.chats, t.refills, t.waitingLineWhileNeeded,
            capWaits, Num(endedAt), failures == 0 ? "pass" : "fail");
        string file = Path.Combine(Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? folder, DeadAirFile);
        try
        {
            if (!File.Exists(file)) File.WriteAllText(file, DeadAirHeader + "\n");
            File.AppendAllText(file, row + "\n");
            var runs = File.ReadAllLines(file).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 19 && c[1] == d.ToString()).ToList();
            if (runs.Count < DeadAirRunsForMedian)
            {
                Note($"Dead air: {runs.Count} of the {DeadAirRunsForMedian} runs of Day {d} its medians need are in {file}; the first customer, " +
                     $"the dead air, the bench jobs and the day's end are judged from run {DeadAirRunsForMedian} on (this run's are above).");
                return;
            }
            List<string[]> last = runs.Skip(runs.Count - DeadAirRunsForMedian).ToList();
            string over = $"the median of Day {d}'s last {DeadAirRunsForMedian} runs";
            float firstMedian = Median(last, 2), nothingMedian = Median(last, 3), benchMedian = Median(last, 10), endedMedian = Median(last, 17);
            Check(firstMedian >= 0f && firstMedian <= FirstAtTheTillWithin,
                $"the first customer at the till, {over}: {firstMedian:0.0} s (within {FirstAtTheTillWithin:0} s)");
            Check(nothingMedian >= 0f && nothingMedian <= NothingAfterOpeningAtMost[d],
                $"nothing to do after the first 15 s, {over}: {nothingMedian:0} s (at most {NothingAfterOpeningAtMost[d]:0})");
            Check(benchMedian >= BenchJobsAtLeast[d], $"bench jobs done, {over}: {benchMedian:0} (at least {BenchJobsAtLeast[d]})");
            Check(endedMedian >= 0f && endedMedian <= DayEndsBy[d], $"the day ended, {over}: {Clock(endedMedian)} (by {Clock(DayEndsBy[d])})");
        }
        catch (Exception e) { Note("couldn't keep the dead-air file: " + e.Message); }
    }

    // SEAT VARIETY, JUDGED OVER RUNS (23 Oct 2026; the header). The medians of the day's last Seat Runs For Median runs that
    // carry the seat columns, once there are that many, as Day 3's storm-outs are judged: one run's seven or so sittings
    // can miss by chance, three in a row hardly (a good build failed the medians on about 2 Day 1s in 100 in the 23 Oct
    // model, the old nearest rule on all of them). Reported, not judged, in a café without the room for it (SeatVariety).
    void SeatMedians(int d, List<string[]> runs, string file)
    {
        List<string[]> seatRuns = runs.Where(IsSeatRow).ToList();
        if (seatRuns.Count < SeatRunsForMedian)
        {
            Note($"Seat variety: {seatRuns.Count} of the {SeatRunsForMedian} runs of Day {d} its medians need are in {file}; they are judged " +
                 $"from run {SeatRunsForMedian} on (the numbers of this run are above).");
            return;
        }
        List<string[]> lastRuns = seatRuns.Skip(seatRuns.Count - SeatRunsForMedian).ToList();
        float seatsMedian = MedianFromEnd(lastRuns, 4), zonesMedian = MedianFromEnd(lastRuns, 3);
        float shareMedian = MedianFromEnd(lastRuns, 2), drinkMedian = MedianFromEnd(lastRuns, 1);
        string over = $"the median of Day {d}'s last {SeatRunsForMedian} runs";
        string seatsLine = $"seat variety, {over}: {seatsMedian:0} different seats across {zonesMedian:0} zones (at least {SeatsAtLeast} and {ZonesAtLeast})";
        string shareLine = shareMedian < 0f
            ? $"seat variety, {over}: the scorer chose no seat in them (every sitting a bench job at the repair bar): no share to judge"
            : $"seat variety, {over}: the busiest seat the scorer chose took {shareMedian:P0} of its sittings (at most {OneSeatAtMost:P0})";
        string drinkLine = drinkMedian < 0f
            ? $"seat variety, {over}: fewer than two drink customers sat down in each: nothing to spread"
            : $"seat variety, {over}: the drink customers sat in {drinkMedian:0} zones (at least {DrinkZonesAtLeast})";
        if (!seatRoomy)
        {
            Note(seatsLine + " (reported: the room is too small to judge)");
            Note(shareLine);
            Note(drinkLine);
            return;
        }
        Check(seatsMedian >= SeatsAtLeast && zonesMedian >= ZonesAtLeast, seatsLine);
        if (shareMedian < 0f) Note(shareLine);
        else Check(shareMedian <= OneSeatAtMost + .001f, shareLine);
        if (drinkMedian < 0f) Note(drinkLine);
        else Check(drinkMedian >= DrinkZonesAtLeast, drinkLine);
    }

    const string RunsHeader = "when,day,length,first walk-in at till,grace done,storm-outs,day ended,walk-in repairs,result";
    const string RunsHeaderWithSeats = RunsHeader + ",seats used,zones used,busiest chosen seat share,drink zones";

    // A row written since 23 Oct: the four seat numbers follow its result. They are read from the end, because the quoted
    // list of the walk-ins' repairs can hold commas ("Gunked up, won't charge") and the file is split on every comma.
    static bool IsSeatRow(string[] cells) =>
        cells.Length >= 13 && (cells[cells.Length - 5] == "pass" || cells[cells.Length - 5] == "fail")
        && cells[cells.Length - 1] != "pass" && cells[cells.Length - 1] != "fail";

    static float MedianFromEnd(List<string[]> runs, int fromEnd)
    {
        var values = new List<float>();
        foreach (string[] cells in runs)
            if (cells.Length >= fromEnd && float.TryParse(cells[cells.Length - fromEnd], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                values.Add(v);
        if (values.Count == 0) return -1f;
        values.Sort();
        return values[values.Count / 2];
    }

    static string CsvWhole(int n) => n < 0 ? "" : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    static string CsvShare(float share) => share < 0f ? "" : share.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

    static string Num(float seconds) => seconds < 0f ? "" : seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    static string Clock(float seconds) => seconds < 0f ? "never" : $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

    static float Median(List<string[]> runs, int column)
    {
        var values = new List<float>();
        foreach (string[] cells in runs)
            if (float.TryParse(cells[column], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                values.Add(v);
        if (values.Count == 0) return -1f;
        values.Sort();
        return values[values.Count / 2];
    }
}
