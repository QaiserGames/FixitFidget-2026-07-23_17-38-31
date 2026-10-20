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

    protected override string Title => "The day's shape at the counter - play check" +
                                       (dayNumber > 0 ? $", Day {dayNumber} by the Days 1-3 bars" : "") +
                                       (dayLength > 0f ? $", a {dayLength:0} s day" : "") +
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
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(ShipLengthKey);
        PlayerPrefs.DeleteKey(DayKey);
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
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeLife",
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
        Check(first >= 0f && first <= FirstAtTheTillWithin,
            $"the first customer reached the till {(first >= 0f ? $"{first:0.0} s" : "never")} after opening (within {FirstAtTheTillWithin:0} s; was 39-41 s)");

        // Last orders (12 Oct).
        LastOrders(day, spawner);

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
        Check(endedAt >= 0f && endedAt <= DayEndsBy[d], $"the day ended at {Clock(endedAt)} (by {Clock(DayEndsBy[d])})");

        // The walk-ins' repairs: the day's list, in order (a prefix of it: not every entry need come).
        var listed = new List<string>();
        foreach (FeaturedRepairRequest entry in today.WalkInRepairsOn(d))
            if (entry != null && entry.TryCreateJob(out Job job, out _)) listed.Add($"{job.deviceName}: {job.faultDescription}");
        var brought = walkIns.Where(a => a.job.Length > 0 && a.job != "a drink").Select(a => a.job).ToList();
        bool inOrder = brought.Count <= listed.Count && brought.Select((j, i) => j == listed[i]).All(ok => ok);
        Check(listed.Count > 0 && inOrder,
            $"the walk-ins brought [{string.Join(", ", brought)}] (only the day's list, in order: [{string.Join(", ", listed)}]); " +
            $"{walkIns.Count(a => a.job == "a drink")} came for a drink; the spawner handed out {(spawner != null ? spawner.WalkInRepairsUsed : 0)}");

        // This run, then every run of this day so far: the medians the spec's 20 runs a day are judged on.
        string row = string.Join(",", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), d, day.ToString("0"), Num(firstWalkIn), Num(graceDone),
            stormOuts, Num(endedAt), "\"" + string.Join(" | ", brought) + "\"", failures == 0 ? "pass" : "fail");
        string file = Path.Combine(Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? folder, RunsFile);
        try
        {
            if (!File.Exists(file)) File.WriteAllText(file, "when,day,length,first walk-in at till,grace done,storm-outs,day ended,walk-in repairs,result\n");
            File.AppendAllText(file, row + "\n");
            var runs = File.ReadAllLines(file).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 7 && c[1] == d.ToString()).ToList();
            Note($"Day {d}: {runs.Count} run(s) recorded in {file} (the spec asks for 20). Medians: first walk-in at the till {Clock(Median(runs, 3))}, " +
                 $"{graceWhat} {Clock(Median(runs, 4))}, storm-outs {Median(runs, 5):0.#}, the day ended {Clock(Median(runs, 6))}.");
            if (d == 3 && runs.Count >= 3)
                Check(Median(runs, 5) <= 1f, $"Day 3: the median run loses at most one ({Median(runs, 5):0.#} over {runs.Count} runs)");
        }
        catch (Exception e) { Note("couldn't keep the runs file: " + e.Message); }
    }

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
