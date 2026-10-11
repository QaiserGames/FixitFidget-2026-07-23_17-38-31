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
// Optional: the ship's day length (420 s), set on the clock as the day opens (DayClock's own length, not saved), and the
// autopilot at human speed (CafeLab.HumanSpeedKey). Photos: the first customer at the till from the game's camera, and
// the middle of each phase. Report and photos: Logs/CafeLife/day-shape-<time>/. Nothing is saved in the scene; the lab's
// own save is the only file the game writes.
// ---------------------------------------------------------------------------
public sealed class DayShapeCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.DayShape.Check";
    public const string ShipLengthKey = "FixitFidget.DayShape.ShipLength";
    public const float FirstAtTheTillWithin = 15f;
    public const float ShipDayLength = 420f;
    // The day's first arrival sets off half a second in, already near the door (CustomerSpawner): not a walk from the street.
    const float OpenerSeenBefore = 2f;

    /// <summary>0: the day as the scene has it (180 s); otherwise this many seconds (the ship's 420).</summary>
    public float dayLength;

    protected override string Title => "The day's shape at the counter - play check" +
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
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(ShipLengthKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Day shape check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Day shape check (this Play session only)");
        var check = go.AddComponent<DayShapeCheck>();
        check.dayLength = ship ? ShipDayLength : 0f;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeLife",
            $"day-shape-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    sealed class Arrival
    {
        public string name;
        public float seen = -1f, atCounter = -1f;
        public bool counted;
    }

    readonly Dictionary<CustomerBrain, Arrival> people = new Dictionary<CustomerBrain, Arrival>();
    readonly List<Arrival> arrivals = new List<Arrival>();
    int fullCounterSamples, samples;

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

        // Photos: the first customer at the till, and the middle of each phase.
        var photoAt = new List<(float at, string name)>();
        for (int i = 0; i < today.phases.Length; i++)
        {
            float from = today.phases[i].startsAt, to = i + 1 < today.phases.Length ? today.phases[i + 1].startsAt : 1f;
            photoAt.Add(((from + to) * .5f * day, $"{i + 2:00}-{Slug(today.phases[i].phaseName)}"));
        }
        bool firstPhoto = false;

        // Watch until the café closes (last orders: no one reaching the counter after this counts for the day's shape).
        while (clock != null && clock.IsOpen && !clock.DayOver)
        {
            float now = clock.SecondsIntoDay;
            foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            {
                if (c == null) continue;
                if (!people.TryGetValue(c, out Arrival a))
                {
                    a = new Arrival { name = c.CustomerName, seen = now };
                    people[c] = a;
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

        Report(today, day);
    }

    void Report(DayDefinition today, float day)
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
        report.AppendLine("   (10 Oct, before: Opening 0 / Morning rush 2 / Lull 3 / Lunch rush 1 / Last orders 3; the first at the till at 41 s.)");
        foreach (Arrival a in arrivals.OrderBy(x => x.atCounter))
            report.AppendLine($"     {a.atCounter,6:0.0} s  {a.name}" + (a.seen >= OpenerSeenBefore ? $"  (on the way {a.atCounter - a.seen:0.0} s)" : "  (on the way already at the open)"));
        var walks = arrivals.Where(a => a.seen >= OpenerSeenBefore).Select(a => a.atCounter - a.seen).OrderBy(x => x).ToList();
        if (walks.Count > 0)
            Note($"From the street to the counter: {walks.First():0}-{walks.Last():0} s, median {walks[walks.Count / 2]:0} s " +
                 $"({walks.Count} walk(s); the spawner looked {FindAnyObjectByType<CustomerSpawner>()?.LeadSeconds ?? 0f:0} s ahead by the end).");
        if (samples > 0)
            Note($"The counter's three places were all taken (by people at it or walking over) {100f * fullCounterSamples / samples:0}% of the day: " +
                 "while they are, nobody new sets off.");

        // The first at the till.
        float first = arrivals.Count > 0 ? arrivals.Min(a => a.atCounter) : -1f;
        Check(first >= 0f && first <= FirstAtTheTillWithin,
            $"the first customer reached the till {(first >= 0f ? $"{first:0.0} s" : "never")} after opening (within {FirstAtTheTillWithin:0} s; was 39-41 s)");

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

    static string Slug(string name) => string.IsNullOrEmpty(name) ? "phase" : name.ToLowerInvariant().Replace(' ', '-');
}
