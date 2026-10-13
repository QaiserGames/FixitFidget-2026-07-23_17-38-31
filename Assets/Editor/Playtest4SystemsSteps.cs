#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4, SYSTEMS (10 Oct 2026; the veteran's and the first-hour playtester's notes, claude/playtest-4-notes.md;
// the Systems lead's brief, findings S1-S9)
//
// The scene and asset data behind the day's fairness, through Playtest4Shared (one undo group, checks in numbers, never
// saves the scene, never deletes anything, safe to run twice):
//
//   Fixit Fidget > Playtest 4 > Systems > 0 - Check first (read-only)
//       every walk-in archetype's and every regular's wait (seat or standing) and patience, in seconds per day (in the
//       queue; after "yes", seated and standing), the scene's days (Day 1's values in full), every seat and standing
//       spot, the bar stools' settings, and the new Systems fields once compiled.
//   Fixit Fidget > Playtest 4 > Systems > 1 - Seat the impatient and rushed, Day 1's patience (scene + assets)
//       S1: the Impatient and Rushed walk-ins wait in a seat like everyone else (CustomerSpawner.archetypes[*].
//           preferredWaitKind Loiter -> Seat; their patience multipliers, their short fuse in the queue, stay). A regular
//           (CustomerProfile) who prefers to stand is seated too (none does today: Grace and the Officer sit).
//       S4: Day 1 (the scene's Day 1, Assets/GraceShowcase/Day_01_GraceCamera) patience x1.4 -> x2.0.
//       Checks: every archetype and regular waits in a seat; the multipliers are unchanged; Day 1 is x2.0; every bar
//       stool (Snap To Seat off: a customer waits standing beside it) drains at a seat's rate, so the impatient who
//       pick one aren't back to a stander's drain.
//   Fixit Fidget > Playtest 4 > Systems > 2 - Rushed patience x0.6 -> x0.75 (scene)
//       12 Oct 2026 (the playtesters' #4, his "yes and i trust the judgement"): the stools' fix (WaitingArea: a stander
//       at a bar stool has arrived) puts the second stool within E's reach for a queue of two or more, but it moves no
//       clock. A seated Rushed bench repair on Day 5 lasts 40.5 s, 64.3 s with three reassures from the bench; his
//       repairs take 41-84 s (median 63) plus about 9 s of talk, carry and hand-back. At x0.75: 50.6 s seated, 80.4 s
//       with the reassures, 27.0 s in the queue (was 21.6): won by going straight to it and keeping them calm, lost by
//       making them wait behind another job. Impatient (x0.7) is left alone: 47.2 s seated, 75.0 s with the reassures.
//       Checks: Rushed is x0.75 and still seated; every other archetype keeps its multiplier; the arithmetic in the report.
//
// The labs (each one Play session in the lab's own save; your playtest save is never used):
//   Fixit Fidget > Playtest 4 > Systems > Lab A / B - Stations and the repair bar, from above / first person
//       (StationsCheck: the playtest 3 check, plus the repair bar's hand-back and a drink over the counter; S2, S9)
//   Fixit Fidget > Playtest 4 > Systems > Lab C / D / E - The day's shape at the counter, Day 5: the scene's 180 s day /
//       the ship's 420 s day / the autopilot at human speed (DayShapeCheck; S3)
//   Fixit Fidget > Café life > Lab > Play a lab session - Day 5, autopilot at human speed (S8; CafeLab.HumanSpeedKey)
// ---------------------------------------------------------------------------
public static class Playtest4SystemsSteps
{
    const string Menu = Playtest4Shared.Menu + "Systems/";
    const string LabMenu = "Fixit Fidget/Café life/Lab/";
    const string Tag = "[Playtest 4 · Systems] ";

    // S4: the scene's Day 1 is the Grace showcase (Mansoor's opening); its patience goes from x1.4 to x2.0.
    const string Day1Name = "Day_01_GraceCamera";
    const float Day1Patience = 2f;

    // What a waiting customer's patience drains at: a seat (TableSeat's 0.6) and a standing spot (WaitSpot's 1.15).
    const float SeatDrain = .6f, StandingDrain = 1.15f;

    // Step 2 (12 Oct): the Rushed walk-ins' patience, x0.6 -> x0.75. Reassure (CustomerBrain): +30% of the max, then
    // x0.6 of that each time, three times at most, 12 s apart.
    const string RushedName = "Rushed";
    const float RushedWas = .6f, RushedPatience = .75f;
    const float ReassureAmount = .3f, ReassureFalloff = .6f;
    const int ReassureUses = 3;

    const string MenuCheckFirst = Menu + "0 - Check first (read-only)";
    const string MenuSeat = Menu + "1 - Seat the impatient and rushed, Day 1's patience (scene + assets)";
    const string MenuRushed = Menu + "2 - Rushed patience x0.6 to x0.75 (scene)";
    const string MenuStationsAbove = Menu + "Lab A - Stations and the repair bar, from above (drives itself)";
    const string MenuStationsFirst = Menu + "Lab B - Stations and the repair bar, first person (drives itself)";
    const string MenuShape = Menu + "Lab C - The day's shape at the counter, Day 5 (the scene's 180 s day)";
    const string MenuShapeShip = Menu + "Lab D - The day's shape at the counter, Day 5 (the ship's 420 s day)";
    const string MenuShapeHuman = Menu + "Lab E - The day's shape at the counter, Day 5 (autopilot at human speed)";
    const string MenuHumanSession = LabMenu + "Play a lab session - Day 5, autopilot at human speed";

    // What the survey found, for the change and the checks.
    sealed class Found
    {
        public CustomerSpawner spawner;
        public DayDefinition day1;
        public readonly List<DayDefinition> days = new List<DayDefinition>();
        public readonly List<CustomerProfile> profiles = new List<CustomerProfile>();
        public readonly Dictionary<string, float> multiplierBefore = new Dictionary<string, float>();
        public float day1Before;
        public float queuePatience = 40f, servicePatience = 45f;
    }

    static Found found;

    // ---------- 0 - Check first ----------

    [MenuItem(MenuCheckFirst, false, 0)]
    static void CheckFirst() => Playtest4Shared.Survey("Playtest 4 · Systems · 0 - Check first", "systems-0-check-first", Survey);

    [MenuItem(MenuCheckFirst, true)]
    static bool CanCheckFirst() => Playtest4Shared.NotPlaying();

    // ---------- 1 - Seat the impatient and rushed, Day 1's patience ----------

    [MenuItem(MenuSeat, false, 1)]
    static void SeatThem() => Playtest4Shared.Step("Playtest 4 · Systems · 1 - Seat the impatient and rushed, Day 1's patience",
        "systems-1-seats-and-day-1", Survey, Change, Checks);

    [MenuItem(MenuSeat, true)]
    static bool CanSeatThem() => Playtest4Shared.NotPlaying();

    // ---------- 2 - Rushed patience x0.6 -> x0.75 (12 Oct) ----------

    [MenuItem(MenuRushed, false, 2)]
    static void RushedPatienceStep() => Playtest4Shared.Step("Playtest 4 · Systems · 2 - Rushed patience x0.6 to x0.75",
        "systems-2-rushed-patience", Survey, ChangeRushed, ChecksRushed);

    [MenuItem(MenuRushed, true)]
    static bool CanRushedPatienceStep() => Playtest4Shared.NotPlaying();

    // ---------- the survey (all three items): Info and Require only, so nothing here can fail the step ----------

    static void Survey(Playtest4Shared.Run run)
    {
        found = new Found();
        CustomerSpawner spawner = Playtest4Shared.One<CustomerSpawner>(run);
        found.spawner = spawner;
        var so = new SerializedObject(spawner);
        SerializedProperty archetypes = run.Prop(so, "archetypes");
        SerializedProperty schedule = run.Prop(so, "schedule");
        run.Require(archetypes.isArray && archetypes.arraySize > 0, "The café's CustomerSpawner has no archetypes.");
        run.Require(schedule.isArray && schedule.arraySize > 0, "The café's CustomerSpawner has no days (schedule).");

        // A customer's patience: the Customer prefab's own numbers.
        GameObject body = so.FindProperty("customerPrefab")?.objectReferenceValue as GameObject;
        CustomerBrain brain = body != null ? body.GetComponent<CustomerBrain>() : null;
        if (brain != null)
        {
            var brainSO = new SerializedObject(brain);
            SerializedProperty queue = brainSO.FindProperty("queuePatience"), service = brainSO.FindProperty("servicePatience");
            if (queue != null) found.queuePatience = queue.floatValue;
            if (service != null) found.servicePatience = service.floatValue;
        }
        run.Info($"A customer's patience ({(brain != null ? Playtest4Shared.Describe(body) : "the Customer prefab wasn't found: the defaults")}): " +
                 $"{found.queuePatience:0} s in the queue and {found.servicePatience:0} s after \"yes\" at a drain of 1, times the person's " +
                 $"multiplier and the day's. Waiting in a seat drains x{SeatDrain}, standing x{StandingDrain}, talking x0.1.");

        // The days.
        run.Section("The days (CustomerSpawner.schedule)");
        for (int i = 0; i < schedule.arraySize; i++)
        {
            var day = schedule.GetArrayElementAtIndex(i).objectReferenceValue as DayDefinition;
            found.days.Add(day);
            if (day == null) { run.Info($"Day {i + 1}: (none)"); continue; }
            string allowed = day.allowedArchetypes == null || day.allowedArchetypes.Length == 0 ? "every archetype" : string.Join(", ", day.allowedArchetypes);
            run.Info($"Day {i + 1}: {AssetDatabase.GetAssetPath(day)}  patience x{day.patienceMultiplier:0.##}, at most {day.maxCustomers} customers, {allowed}");
        }
        found.day1 = found.days.Count > 0 ? found.days[0] : null;
        run.Require(found.day1 != null, "The café's Day 1 (CustomerSpawner.schedule[0]) is empty.");
        run.Require(found.day1.name == Day1Name,
            $"The café's Day 1 is {AssetDatabase.GetAssetPath(found.day1)}, not the Grace showcase's {Day1Name}: the patience decision (x2.0) was " +
            "made for that day. Nothing changed; tell Claude.");
        found.day1Before = found.day1.patienceMultiplier;
        DayDefinition d1 = found.day1;
        run.Info($"Day 1 in full: patience x{d1.patienceMultiplier:0.##} (the decision: x{Day1Patience:0.0}), at most {d1.maxCustomers} customers, " +
                 $"featured regular {(d1.featuredRegular != null ? d1.featuredRegular.name + " at " + d1.featuredRegularArrivesAt.ToString("0.##") : "none")}, " +
                 $"story visits: {(d1.storyVisits == null || d1.storyVisits.Length == 0 ? "none" : string.Join(", ", d1.storyVisits.Where(v => v != null).Select(v => (v.who != null ? v.who.name : "?") + " at " + v.arrivesAt.ToString("0.##"))))} " +
                 "(since playtest 4 a story visit comes when it is due: the cap limits walk-ins; CustomerSpawner).");

        // The walk-in archetypes.
        run.Section("The walk-ins (CustomerSpawner.archetypes): where they wait, and their patience in seconds");
        for (int i = 0; i < archetypes.arraySize; i++)
        {
            SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
            string name = a.FindPropertyRelative("archetypeName").stringValue;
            float multiplier = a.FindPropertyRelative("patienceMultiplier").floatValue;
            SerializedProperty kindProp = a.FindPropertyRelative("preferredWaitKind");
            var kind = (WaitingSpot.SpotKind)kindProp.enumValueIndex;
            found.multiplierBefore[name] = multiplier;
            run.Info($"{name}: patience x{multiplier:0.##}, waits {Wait(kind)}{(kind == WaitingSpot.SpotKind.Loiter ? "   <- step 1 seats them" : "")}");
            for (int d = 0; d < found.days.Count; d++)
            {
                DayDefinition day = found.days[d];
                if (day == null || !day.AllowsArchetype(name)) continue;
                run.Info("    " + Seconds($"Day {d + 1}", multiplier * day.patienceMultiplier, kind));
            }
        }

        // The regulars (CustomerProfile: their own wait and patience win over an archetype's).
        run.Section("The regulars (CustomerProfile assets; a profile's wait wins over the archetype's)");
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile == null) continue;
            found.profiles.Add(profile);
            run.Info($"{AssetDatabase.GetAssetPath(profile)}: patience x{profile.patienceMultiplier:0.##}, waits {Wait(profile.preferredWaitKind)}" +
                     (profile.preferredWaitKind == WaitingSpot.SpotKind.Loiter ? "   <- step 1 seats them" : ""));
        }
        if (found.profiles.Count == 0) run.Info("(no CustomerProfile assets found)");
        foreach (CustomerProfile who in new[] { d1.featuredRegular }.Concat((d1.storyVisits ?? Array.Empty<StoryVisit>()).Where(v => v != null).Select(v => v.who)))
        {
            if (who == null) continue;
            run.Info($"    {who.name} on Day 1, first visit: " + Seconds($"x{d1.patienceMultiplier:0.##} now", who.patienceMultiplier * d1.patienceMultiplier, who.preferredWaitKind));
            run.Info($"    {who.name} on Day 1, first visit: " + Seconds($"x{Day1Patience:0.0} after step 1", who.patienceMultiplier * Day1Patience, WaitingSpot.SpotKind.Seat));
        }
        int movement = AssetDatabase.FindAssets("t:NpcMovementProfile").Length;
        run.Info($"The {movement} NPC profiles in Assets/Data/NpcProfiles are how people move (NpcMovementProfile): no wait or patience in them.");

        // The places to wait.
        run.Section("The places to wait (every WaitingSpot and TableSeat in the scene)");
        WaitingSpot[] spots = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Include);
        var active = spots.Where(s => s != null && s.isActiveAndEnabled).ToList();
        int seats = active.Count(s => s.Kind == WaitingSpot.SpotKind.Seat);
        int standing = active.Count(s => s.Kind == WaitingSpot.SpotKind.Loiter);
        run.Info($"{active.Count} in use ({seats} seats, {standing} standing spots, {active.Count - seats - standing} other); {spots.Length - active.Count} switched off.");
        PatronSpawner patrons = Object.FindAnyObjectByType<PatronSpawner>(FindObjectsInactive.Include);
        if (patrons != null)
        {
            var pso = new SerializedObject(patrons);
            run.Info($"Patrons: at most {pso.FindProperty("maxPatrons")?.intValue}, and none come in once only {pso.FindProperty("reserveSeatsForCustomers")?.intValue} seats are free (PatronSpawner).");
        }
        foreach (WaitingSpot spot in active.Where(s => s.Kind != WaitingSpot.SpotKind.Seat))
            run.Info($"    standing: {Playtest4Shared.PathOf(spot.gameObject)}  drains x{spot.DrainMultiplier:0.##}");
        foreach (TableSeat stool in active.OfType<TableSeat>().Where(s => !s.SnapToSeat))
            run.Info($"    bar stool (Snap To Seat off: waits standing beside it): {Playtest4Shared.PathOf(stool.gameObject)}  " +
                     $"drains x{stool.DrainMultiplier:0.##}, at ({stool.StandPoint.position.x:0.00}, {stool.StandPoint.position.z:0.00}){BenchDistance(stool)}");

        // The new Systems fields (playtest 4), once the code has compiled.
        run.Section("The Systems fields this session added (defaults until someone changes them in the Inspector)");
        Field(run, spawner, "walkLeadSeconds", "CustomerSpawner: the walk in from the street, before it is measured (s)");
        Field(run, spawner, "leadLearning", "CustomerSpawner: how fast the measured walk is learnt (0-1)");
        Field(run, spawner, "openerMetresOut", "CustomerSpawner: the day's first arrival starts this far short of the door (m)");
        Field(run, spawner, "lastOrdersLeadSeconds", "CustomerSpawner: last orders for walk-ins, this long before closing (s; 12 Oct, #9)");
        PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (ace != null)
        {
            Field(run, ace, "acrossCounterReach", "PlayerInteractor: E's reach over the counter, flat (m)");
            Field(run, ace, "benchRange", "PlayerInteractor: behind the repair bar's bench, within this of its stand point (m)");
        }

        // The Rushed walk-ins against a bench repair at a person's pace (12 Oct, #4): the arithmetic behind step 2.
        run.Section("The Rushed walk-ins against a bench repair at a person's pace (step 2; seated, Reassure from the bench)");
        run.Info("A seated wait after \"yes\": 45 s x the person x the day, draining x0.6. Reassure: +30% of the max, then x0.6 of that " +
                 $"each time, {ReassureUses} at most, 12 s apart (CustomerBrain). His own repairs (yes -> walked out): 41-84 s, median 63, " +
                 "plus about 9 s of talk, carry and hand-back; the lab's hands at human speed 45-90 s.");
        for (int d = 0; d < found.days.Count; d++)
        {
            DayDefinition day = found.days[d];
            if (day == null || !day.AllowsArchetype(RushedName)) continue;
            run.Info($"    Day {d + 1} (x{day.patienceMultiplier:0.##}): " + RushedLine(RushedWas, day.patienceMultiplier) + "   <- now");
            run.Info($"    Day {d + 1} (x{day.patienceMultiplier:0.##}): " + RushedLine(RushedPatience, day.patienceMultiplier) + "   <- step 2");
        }
        run.Info("    The stools' fix (WaitingArea.HasArrived) puts the second stool within E's reach when two or more queue; it moves no clock. " +
                 "At x0.6 a median repair is lost even when started at once and reassured three times (64 s against about 72); at x0.75 it " +
                 "is won that way (80 s) and still lost when they wait behind another job.");

        // Last orders for walk-ins (12 Oct, #9): when they fall on each day length.
        SerializedProperty lastOrders = new SerializedObject(spawner).FindProperty("lastOrdersLeadSeconds");
        if (lastOrders != null)
        {
            DayClock clock = Object.FindAnyObjectByType<DayClock>(FindObjectsInactive.Include);
            SerializedProperty length = clock != null ? new SerializedObject(clock).FindProperty("dayLengthSeconds") : null;
            float scene = length != null ? length.floatValue : 180f;
            run.Section("Last orders for walk-ins (12 Oct, #9: nobody new reaches the till after closing)");
            run.Info($"No walk-in sets off with less than {lastOrders.floatValue:0} s left on the clock (or today's longest measured walk to the till, " +
                     $"if longer): on the scene's {scene:0} s day that is {scene - lastOrders.floatValue:0} s in ({ClockWords(clock, (scene - lastOrders.floatValue) / scene)}), " +
                     $"on the ship's 420 s day {420f - lastOrders.floatValue:0} s in ({ClockWords(clock, (420f - lastOrders.floatValue) / 420f)}). " +
                     "Arrivals go on until closing (the walk takes that long); only the ones who would have come after it are gone. " +
                     "The LAST ORDERS sign (the café's last hour) stays its own warning.");
            run.Info("On the human-speed Day 5 of 11 Oct that is two people: Saamin (a support call, served at 0.1: $75 + $6) and Beatriz (walked out " +
                     "unserved, a \"Never again\"): about $81 of the day's $446 ($376 from Ace's work), and one storm-out fewer.");
        }
    }

    // A Rushed customer's seconds on one day: in the queue, seated after "yes", and with the three reassures.
    static string RushedLine(float multiplier, float dayMultiplier)
    {
        float m = multiplier * dayMultiplier;
        float seated = Service(m, SeatDrain);
        float gain = 0f;
        for (int i = 0; i < ReassureUses; i++) gain += seated * ReassureAmount * Mathf.Pow(ReassureFalloff, i);
        return $"x{multiplier:0.##}: {found.queuePatience * m:0.0} s in the queue; after \"yes\" {seated:0.0} s seated, {seated + gain:0.0} s with " +
               $"{ReassureUses} reassures (+{seated * ReassureAmount:0.0}, +{seated * ReassureAmount * ReassureFalloff:0.0}, +{seated * ReassureAmount * ReassureFalloff * ReassureFalloff:0.0})";
    }

    // A moment of the day as the café's clock would show it (opening to closing hours, DayClock).
    static string ClockWords(DayClock clock, float fraction)
    {
        float open = 9f, close = 20f;
        if (clock != null)
        {
            var so = new SerializedObject(clock);
            SerializedProperty o = so.FindProperty("openingHour"), c = so.FindProperty("closingHour");
            if (o != null) open = o.floatValue;
            if (c != null) close = c.floatValue;
        }
        float hour = Mathf.Lerp(open, close, Mathf.Clamp01(fraction));
        int h = Mathf.FloorToInt(hour), m = Mathf.RoundToInt((hour - h) * 60f);
        if (m == 60) { h++; m = 0; }
        return $"{(h % 12 == 0 ? 12 : h % 12)}:{m:00} {(h >= 12 ? "PM" : "AM")} on the café's clock";
    }

    // ---------- step 2: the change and its checks (12 Oct) ----------

    static void ChangeRushed(Playtest4Shared.Run run)
    {
        var so = new SerializedObject(found.spawner);
        SerializedProperty archetypes = run.Prop(so, "archetypes");
        int index = -1;
        for (int i = 0; i < archetypes.arraySize; i++)
            if (archetypes.GetArrayElementAtIndex(i).FindPropertyRelative("archetypeName").stringValue == RushedName) index = i;
        run.Require(index >= 0, $"The café's CustomerSpawner has no \"{RushedName}\" archetype.");
        float now = archetypes.GetArrayElementAtIndex(index).FindPropertyRelative("patienceMultiplier").floatValue;
        run.Require(Mathf.Approximately(now, RushedWas) || Mathf.Approximately(now, RushedPatience),
            $"The {RushedName} walk-ins' patience is x{now:0.###}, neither x{RushedWas} nor x{RushedPatience}: someone tuned it by hand. Nothing changed; tell Claude.");
        run.SetFloat(found.spawner, $"archetypes.Array.data[{index}].patienceMultiplier", RushedPatience,
            $"the {RushedName} walk-ins' patience (CustomerSpawner.archetypes[{index}].patienceMultiplier)");
    }

    static void ChecksRushed(Playtest4Shared.Run run)
    {
        var so = new SerializedObject(found.spawner);
        SerializedProperty archetypes = so.FindProperty("archetypes");
        DayDefinition day5 = found.days.Count >= 5 ? found.days[4] : found.days.LastOrDefault(d => d != null);
        for (int i = 0; i < archetypes.arraySize; i++)
        {
            SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
            string name = a.FindPropertyRelative("archetypeName").stringValue;
            float multiplier = a.FindPropertyRelative("patienceMultiplier").floatValue;
            var kind = (WaitingSpot.SpotKind)a.FindPropertyRelative("preferredWaitKind").enumValueIndex;
            if (name == RushedName)
            {
                run.Check(Mathf.Approximately(multiplier, RushedPatience), $"the {name} walk-ins' patience is x{multiplier:0.###} (was x{RushedWas})");
                run.Check(kind == WaitingSpot.SpotKind.Seat, $"the {name} walk-ins still wait in a seat (step 1)");
                if (day5 != null && day5.AllowsArchetype(name))
                    run.Check(Service(multiplier * day5.patienceMultiplier, SeatDrain) >= 50f,
                        $"on {day5.name}: " + RushedLine(multiplier, day5.patienceMultiplier) + " (at least 50 s seated)");
            }
            else
            {
                bool kept = found.multiplierBefore.TryGetValue(name, out float before) && Mathf.Approximately(before, multiplier);
                run.Check(kept, $"the {name} walk-ins keep their patience x{multiplier:0.##}");
            }
        }
    }

    // ---------- step 1: the change ----------

    static void Change(Playtest4Shared.Run run)
    {
        var so = new SerializedObject(found.spawner);
        SerializedProperty archetypes = run.Prop(so, "archetypes");
        for (int i = 0; i < archetypes.arraySize; i++)
        {
            SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
            string name = a.FindPropertyRelative("archetypeName").stringValue;
            var kind = (WaitingSpot.SpotKind)a.FindPropertyRelative("preferredWaitKind").enumValueIndex;
            string label = $"the {name} walk-ins' wait (CustomerSpawner.archetypes[{i}].preferredWaitKind; 0 standing, 1 a seat)";
            if (kind == WaitingSpot.SpotKind.Loiter)
                run.SetInt(found.spawner, $"archetypes.Array.data[{i}].preferredWaitKind", (int)WaitingSpot.SpotKind.Seat, label);
            else run.Info($"{label}: already {Wait(kind)}");
        }
        foreach (CustomerProfile profile in found.profiles)
        {
            if (profile.preferredWaitKind != WaitingSpot.SpotKind.Loiter) continue;
            run.SetInt(profile, "preferredWaitKind", (int)WaitingSpot.SpotKind.Seat, $"{AssetDatabase.GetAssetPath(profile)}: preferredWaitKind");
            run.SaveAsset(profile);
        }
        run.SetFloat(found.day1, "patienceMultiplier", Day1Patience, $"{AssetDatabase.GetAssetPath(found.day1)}: patienceMultiplier (Day 1's patience)");
        if (!Mathf.Approximately(found.day1Before, Day1Patience)) run.SaveAsset(found.day1);
    }

    // ---------- step 1: the checks ----------

    static void Checks(Playtest4Shared.Run run)
    {
        var so = new SerializedObject(found.spawner);
        SerializedProperty archetypes = so.FindProperty("archetypes");
        DayDefinition day5 = found.days.Count >= 5 ? found.days[4] : found.days.LastOrDefault(d => d != null);
        for (int i = 0; i < archetypes.arraySize; i++)
        {
            SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
            string name = a.FindPropertyRelative("archetypeName").stringValue;
            float multiplier = a.FindPropertyRelative("patienceMultiplier").floatValue;
            var kind = (WaitingSpot.SpotKind)a.FindPropertyRelative("preferredWaitKind").enumValueIndex;
            string onDay5 = day5 != null && day5.AllowsArchetype(name)
                ? $"; on {day5.name} after \"yes\": {Service(multiplier * day5.patienceMultiplier, SeatDrain):0.0} s seated (standing it was {Service(multiplier * day5.patienceMultiplier, StandingDrain):0.0} s)"
                : "";
            run.Check(kind == WaitingSpot.SpotKind.Seat, $"the {name} walk-ins wait in a seat{onDay5}");
            bool kept = found.multiplierBefore.TryGetValue(name, out float before) && Mathf.Approximately(before, multiplier);
            run.Check(kept, $"the {name} walk-ins keep their patience x{multiplier:0.##} (their fuse in the queue: " +
                            $"{(day5 != null ? (found.queuePatience * multiplier * day5.patienceMultiplier).ToString("0.0") + " s on " + day5.name : "unchanged")})");
        }
        foreach (CustomerProfile profile in found.profiles)
            run.Check(profile.preferredWaitKind != WaitingSpot.SpotKind.Loiter, $"{profile.name} waits {Wait(profile.preferredWaitKind)}");

        DayDefinition d1 = found.day1;
        string grace = d1.featuredRegular != null
            ? $"; {d1.featuredRegular.name} (x{d1.featuredRegular.patienceMultiplier:0.##}) {found.queuePatience * d1.featuredRegular.patienceMultiplier * d1.patienceMultiplier:0} s in the queue, " +
              $"{Service(d1.featuredRegular.patienceMultiplier * d1.patienceMultiplier, SeatDrain):0} s seated after \"yes\" (the day is 180 s)"
            : "";
        run.Check(Mathf.Approximately(d1.patienceMultiplier, Day1Patience),
            $"Day 1 ({d1.name}) patience x{d1.patienceMultiplier:0.##} (was x{found.day1Before:0.##}){grace}");

        // A customer waiting at a bar stool stands beside it (Snap To Seat off), but drains at the seat's rate: an impatient
        // customer who picks one is not back to a stander's x1.15.
        var stools = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s.isActiveAndEnabled && !s.SnapToSeat).ToList();
        run.Check(stools.Count > 0, $"the bar stools are found ({stools.Count})");
        foreach (TableSeat stool in stools)
            run.Check(stool.Kind == WaitingSpot.SpotKind.Seat && stool.DrainMultiplier <= SeatDrain + .001f,
                $"{Playtest4Shared.PathOf(stool.gameObject)}: a seat, draining x{stool.DrainMultiplier:0.##} (a seat's rate){BenchDistance(stool)}");
    }

    // ---------- the labs ----------

    [MenuItem(MenuStationsAbove, false, 20)]
    static void StationsAbove() => StartLab(1, false, false, StationsCheck.PendingKey, () => PlayerPrefs.SetInt(StationsCheck.FirstPersonKey, 0),
        "Stations and the repair bar, from above: a fresh Day 1 that drives itself with a virtual gamepad");

    [MenuItem(MenuStationsFirst, false, 21)]
    static void StationsFirst() => StartLab(1, false, false, StationsCheck.PendingKey, () => PlayerPrefs.SetInt(StationsCheck.FirstPersonKey, 1),
        "Stations and the repair bar, first person: a fresh Day 1 that drives itself with a virtual gamepad");

    [MenuItem(MenuShape, false, 22)]
    static void Shape() => StartLab(5, true, false, DayShapeCheck.PendingKey, () => PlayerPrefs.SetInt(DayShapeCheck.ShipLengthKey, 0),
        "The day's shape at the counter: Day 5 as the scene has it, the autopilot serving");

    [MenuItem(MenuShapeShip, false, 23)]
    static void ShapeShip() => StartLab(5, true, false, DayShapeCheck.PendingKey, () => PlayerPrefs.SetInt(DayShapeCheck.ShipLengthKey, 1),
        $"The day's shape at the counter: Day 5 at the ship's {DayShapeCheck.ShipDayLength:0} s, the autopilot serving");

    [MenuItem(MenuShapeHuman, false, 24)]
    static void ShapeHuman() => StartLab(5, true, true, DayShapeCheck.PendingKey, () => PlayerPrefs.SetInt(DayShapeCheck.ShipLengthKey, 0),
        "The day's shape at the counter: Day 5, the autopilot at human speed");

    [MenuItem(MenuHumanSession)]
    static void HumanSession() => StartLab(5, true, true, null, null,
        "Day 5 with the autopilot at human speed (repairs 45-90 s, drinks 20-40 s, a 3-6 s reaction, one pair of hands)");

    [MenuItem(MenuStationsAbove, true)]
    [MenuItem(MenuStationsFirst, true)]
    [MenuItem(MenuShape, true)]
    [MenuItem(MenuShapeShip, true)]
    [MenuItem(MenuShapeHuman, true)]
    [MenuItem(MenuHumanSession, true)]
    static bool CanStartLab() => Playtest4Shared.NotPlaying();

    // One lab Play session: the lab's own save (Day 1 fresh, or day N with the lab's usual money and stock), the lab
    // switched on, its autopilot as asked, and the check's own request.
    static void StartLab(int day, bool autopilot, bool humanSpeed, string checkKey, Action more, string what)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveData start = day <= 1
            ? new SaveData()                                                   // a fresh game: Day 1's morning
            : new SaveData { day = day, money = 600, cups = 40, beans = 40, dayCompleted = false };
        SaveCheckpointStorage.Write(path, start);
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, autopilot ? 1 : 0);
        if (humanSpeed) PlayerPrefs.SetInt(CafeLab.HumanSpeedKey, 1);
        else PlayerPrefs.DeleteKey(CafeLab.HumanSpeedKey);
        if (checkKey != null) PlayerPrefs.SetInt(checkKey, 1);
        more?.Invoke();
        PlayerPrefs.Save();
        Debug.Log(Tag + what + (checkKey != null ? ". Keep the Game view in front and leave the mouse and keyboard alone until it reports." : ".") +
                  $" Lab save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A lab request that never became a Play session (a compile error, say) must not ride along with the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool any = false;
        foreach (string key in new[] { DayShapeCheck.PendingKey, DayShapeCheck.ShipLengthKey, CafeLab.HumanSpeedKey })
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); any = true; }
        if (any) PlayerPrefs.Save();
    }

    // ---------- the numbers ----------

    static string Wait(WaitingSpot.SpotKind kind) =>
        kind == WaitingSpot.SpotKind.Seat ? "in a seat" : kind == WaitingSpot.SpotKind.Loiter ? "standing" : "browsing";

    // Seconds of patience after "yes", waiting at a drain rate (a seat 0.6, a standing spot 1.15).
    static float Service(float multiplier, float drain) => found.servicePatience * multiplier / drain;

    static string Seconds(string when, float multiplier, WaitingSpot.SpotKind kind) =>
        $"{when} (x{multiplier:0.###}): {found.queuePatience * multiplier:0.0} s in the queue; after \"yes\" " +
        $"{Service(multiplier, SeatDrain):0.0} s seated, {Service(multiplier, StandingDrain):0.0} s standing" +
        $" (waits {Wait(kind)}: {Service(multiplier, kind == WaitingSpot.SpotKind.Seat ? SeatDrain : StandingDrain):0.0} s)";

    // How far a stool is from the repair bench's stand point, flat (E reaches over the counter from there).
    static string BenchDistance(TableSeat stool)
    {
        StationInteractable bench = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude)
            .FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        if (bench == null) return "";
        Vector3 d = stool.StandPoint.position - bench.StandPoint.position;
        d.y = 0f;
        return $", {d.magnitude:0.00} m from the bench's stand point";
    }

    static void Field(Playtest4Shared.Run run, Object target, string path, string what)
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(path);
        run.Info(p == null ? $"{what}: not there yet (let Unity finish compiling)" : $"{what}: {p.floatValue:0.##}");
    }
}
#endif
