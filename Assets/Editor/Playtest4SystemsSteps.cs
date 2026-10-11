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

    const string MenuCheckFirst = Menu + "0 - Check first (read-only)";
    const string MenuSeat = Menu + "1 - Seat the impatient and rushed, Day 1's patience (scene + assets)";
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

    // ---------- the survey (both items): Info and Require only, so nothing here can fail the step ----------

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
        PlayerInteractor ace = Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (ace != null)
        {
            Field(run, ace, "acrossCounterReach", "PlayerInteractor: E's reach over the counter, flat (m)");
            Field(run, ace, "benchRange", "PlayerInteractor: behind the repair bar's bench, within this of its stand point (m)");
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
