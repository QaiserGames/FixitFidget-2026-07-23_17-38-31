#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Run = Playtest4Shared.Run;

// ---------------------------------------------------------------------------
// DAYS 1-3 THAT TEACH: THE PACING PASS (24 Oct 2026; Designer P's spec, Docs/Studio/sessions/2026-10-23/designs/
// pacing-downtime-spec.md §1.5, §4.2 and §6.1, approved by him for building with his answers of 24 Oct)
//
// His words: "day one and two should really teach mostly but what i mean about dead air is really the things you could be
// doing when waiting for a customer to approach the counter." P measured the dead air: on Day 1 nobody needed Ace for 151 of
// 420 seconds, and for 127 of them a customer was walking in. These steps write the days that fill it:
//
//   Fixit Fidget > Days > G - The pacing pass (data)                the five day assets (§1.5), one undo group, one report
//   Fixit Fidget > Days > H - Day 1's two settled guests (scene)     PatronSpawner's settled guests: two on Day 1, four from Day 2
//
// His answers applied: Day 1's side project is a clean (p1: "a cleaning job, so Day 1 brings no new tool"), so Day 1's find is
// the thrift watch's grimy glass (Fixit Fidget > Days > Thrift 1 makes it) and the chain of "tomorrow's job with nobody waiting"
// starts on Day 2; finds one at a time, bought at closing and sold at the next (p2); Day 3 two walk-ins at once (p3); the
// support call on Day 3 once its phone has the real model (p4, art finding a03: Fixit Fidget > Days > Support call 1); two
// settled guests on Day 1 (r3), in the half-finished café's open seats (r1: the lounge's tub chairs and the north bistro, F2's).
//
// The chain of finds (each is the next day's walk-in, rehearsed with nobody waiting, within the tools that day has taught):
//   Day 1  the thrift watch's grimy glass (brush, cloth: Day 1's tools), free, sells $55   → Day 2's walk-in is the watch
//   Day 2  a phone with a cracked screen ($25 → $75: the swap Day 2 teaches)             → Day 3's first walk-in
//   Day 3  a phone frozen on its start-up screen, the circuit ($20 → $60)                 → Day 4's first walk-in
//   Day 4  a seized pocket watch ($35 → $110)
//   then the Shop rotates through the jobs taught (ThriftShelf), never the same two nights running.
//
// Both steps follow Days 1-3's (DaysOneToThreeSteps): Edit Mode only; a survey that refuses with the reason and changes
// nothing (and refuses until the new DayDefinition fields have compiled); one undo group; old → new for every item; checks
// in numbers, and any failure undoes the whole step before anything is saved; idempotent (a second run says "already" for
// every item and saves nothing). G saves the day assets; H needs the café scene open and saved, and saves it.
// Reports: Logs/Days/<step>-<time>.txt and <step>-last.txt, and one PASS/DONE line in the Console.
// ---------------------------------------------------------------------------
public static class PacingSteps
{
    const string Menu = "Fixit Fidget/Days/";
    const string Tag = "[Days pacing] ";
    public const string MenuG = Menu + "G - The pacing pass (data)";
    public const string MenuH = Menu + "H - Day 1's two settled guests (scene)";

    public const string Day1Path = DaysOneToThreeSteps.Day1Path, Day2Path = DaysOneToThreeSteps.Day2Path, Day3Path = DaysOneToThreeSteps.Day3Path;
    public const string Day4Path = DaysOneToThreeSteps.Day4Path, Day5Path = DaysOneToThreeSteps.Day5Path;
    public const string PhonePath = DaysOneToThreeSteps.PhonePath, WatchPath = DaysOneToThreeSteps.WatchPath;
    /// <summary>The support call's phone (a HoldCallJob): on Day 3 once it has the detailed phone's parts (SupportCallSteps).</summary>
    public const string CallPath = "Assets/AssetsPrefabs/PhoneJob.prefab";
    /// <summary>Day 1's find, made by Fixit Fidget › Days › Thrift 1 (ThriftSteps): a variant of the pocket watch with one fault,
    /// a clean (outside AssetsPrefabs, so no walk-in ever rolls it and no customer has to say its label).</summary>
    public const string ThriftWatchPath = "Assets/Data/Thrift/ThriftWatch.prefab";

    // The device prefabs' fault indices (PhoneRepair: 0 Cracked Screen, 1 Gunked Up, 2 Frozen Boot Sequence, the circuit,
    // 3 the Human fault; PocketWatch: 0 Debris Inside, 1 Seized Movement; PhoneJob: 0 the service, the call).
    public const int PhoneScreen = 0, PhoneGunked = 1, PhoneCircuit = 2, PhoneHuman = 3, WatchDebris = 0, WatchSeized = 1, CallFault = 0;

    // ---------- the numbers (§1.5; "was" is what the 14 Oct steps wrote, as on his Mac on 24 Oct) ----------

    public readonly struct PhaseRow
    {
        public readonly string name;
        public readonly float at, gap;
        public PhaseRow(string name, float at, float gap) { this.name = name; this.at = at; this.gap = gap; }
    }

    static PhaseRow R(string name, float at, float gap) => new PhaseRow(name, at, gap);

    /// <summary>Day 1's rhythm: unchanged (it protects the first repair; the cap retry does the rest).</summary>
    public static readonly PhaseRow[] Day1Phases = { R("Calm", 0f, 75f), R("Build", .25f, 30f), R("Rush", .55f, 24f), R("Recover", .80f, 30f) };
    public static readonly PhaseRow[] Day2PhasesWas = { R("Calm", 0f, 60f), R("Build", .25f, 24f), R("Rush", .50f, 18f), R("Recover", .80f, 26f) };
    /// <summary>A longer walk-in job (the watch) needs a little more room.</summary>
    public static readonly PhaseRow[] Day2Phases = { R("Calm", 0f, 60f), R("Build", .22f, 26f), R("Rush", .45f, 20f), R("Recover", .80f, 28f) };
    public static readonly PhaseRow[] Day3PhasesWas = { R("Calm", 0f, 35f), R("Build", .20f, 22f), R("Rush", .45f, 18f), R("Recover", .72f, 26f) };
    /// <summary>The first real day is busier.</summary>
    public static readonly PhaseRow[] Day3Phases = { R("Calm", 0f, 35f), R("Build", .20f, 20f), R("Rush", .45f, 16f), R("Recover", .75f, 24f) };
    public static readonly PhaseRow[] Day4PhasesWas = { R("Calm", 0f, 42f), R("Build", .20f, 28f), R("Rush", .42f, 16.3f), R("Recover", .75f, 37.3f) };
    /// <summary>A real step up from Day 3, without today's cliff (the model: 7.2 lost today, 0.8 with these).</summary>
    public static readonly PhaseRow[] Day4Phases = { R("Calm", 0f, 36f), R("Build", .20f, 20f), R("Rush", .42f, 15f), R("Recover", .75f, 26f) };
    public static readonly PhaseRow[] Day5PhasesWas =
        { R("Opening", 0f, 37.3f), R("Morning rush", .15f, 16.3f), R("Lull", .38f, 60.7f), R("Lunch rush", .58f, 14f), R("Last orders", .82f, 35f) };
    /// <summary>Keeps its two rushes, survivable.</summary>
    public static readonly PhaseRow[] Day5Phases =
        { R("Opening", 0f, 37.3f), R("Morning rush", .15f, 18f), R("Lull", .38f, 50f), R("Lunch rush", .58f, 16f), R("Last orders", .82f, 32f) };

    public const float Day1DrinkOnly = .55f, Day1DrinkOnlyWas = .70f;   // the walk-in phone comes sooner
    public const float Day2Patience = 1.8f, Day2PatienceWas = 1.4f;     // a teaching day shouldn't lose a slow player
    public const float Day2DrinkOnly = .45f, Day2DrinkOnlyWas = .70f;   // more repairs, fewer plain coffees
    public const float Day3DrinkOnly = .35f, Day3DrinkOnlyWas = .45f;
    public const float Day3Patience = 1.6f;                              // unchanged
    public const int Day3AtOnce = 2;                                     // his answer p3: two walk-ins at once
    public const float Day4Patience = 1.45f, Day4PatienceWas = 1f;
    public const int Day4AtOnce = 3, Day4AtOnceWas = 6;
    public const float Day5Patience = 1.3f, Day5PatienceWas = 1f;
    public const int Day5AtOnce = 3, Day5AtOnceWas = 6;

    /// <summary>The walk-ins' repairs, in order (a prefab path and a fault index). Never yesterday's walk-in repair (Days 1-3).</summary>
    public static readonly (string prefab, int fault)[] Day1WalkIns = { (PhonePath, PhoneGunked) };
    public static readonly (string prefab, int fault)[] Day2WalkIns = { (WatchPath, WatchDebris) };
    /// <summary>Day 3: the screen (Day 2's find, now with a customer), the seized watch, the gunked phone again (Day 1's, not
    /// Day 2's), and the support call once its phone is real (his answer p4).</summary>
    public static (string prefab, int fault)[] Day3WalkIns(bool withCall) => withCall
        ? new[] { (PhonePath, PhoneScreen), (WatchPath, WatchSeized), (PhonePath, PhoneGunked), (CallPath, CallFault) }
        : new[] { (PhonePath, PhoneScreen), (WatchPath, WatchSeized), (PhonePath, PhoneGunked) };
    /// <summary>Day 4 opens on its new jobs: the circuit, the seized watch, the watch's grit, the Human fault.</summary>
    public static readonly (string prefab, int fault)[] Day4WalkIns =
        { (PhonePath, PhoneCircuit), (WatchPath, WatchSeized), (WatchPath, WatchDebris), (PhonePath, PhoneHuman) };

    /// <summary>A day's find: the device, its fault, what it costs at the closing before and what it sells for (Perfect).</summary>
    public readonly struct FindRow
    {
        public readonly string prefab;
        public readonly int fault, buy, sale;
        public FindRow(string prefab, int fault, int buy, int sale) { this.prefab = prefab; this.fault = fault; this.buy = buy; this.sale = sale; }
    }

    // §5.2: a find sells for about 55-60 % of what a customer pays for the same job at Perfect (the fault's payout x1.25), and
    // costs about a third of its sale. P guessed the circuit's payout at $85 ($60 for its find); the phone prefab pays $45 for
    // it ($56 at Perfect), so by P's own rule the circuit's find costs $10 and sells for $32 (57 %): a customer always wins.
    public static readonly FindRow Day1Find = new FindRow(ThriftWatchPath, 0, 0, 55);    // free, on the shelf at the open
    public static readonly FindRow Day2Find = new FindRow(PhonePath, PhoneScreen, 25, 75);
    public static readonly FindRow Day3Find = new FindRow(PhonePath, PhoneCircuit, 10, 32);
    public static readonly FindRow Day4Find = new FindRow(WatchPath, WatchSeized, 35, 110);

    /// <summary>Today's gossip (GuestGossip's ids; NotebookEntries' gossip table holds the lines). PLACEHOLDER COPY.</summary>
    public static readonly string[] Day2Gossip = { NotebookEntries.GossipGraceCups, NotebookEntries.GossipGraceFilm };
    public static readonly string[] Day3Gossip = { NotebookEntries.GossipOfficerAsking, NotebookEntries.GossipLodgerBefore };

    /// <summary>Who leaves their cup on the table (P's §2.3 and §6.2): the five movement profiles' chance; about a third of
    /// guests (weighted by the profiles' own weights: 0.35).</summary>
    public static readonly (string path, float chance)[] LeavesCup =
    {
        ("Assets/Data/NpcProfiles/NpcProfile_Hurried.asset", .6f), ("Assets/Data/NpcProfiles/NpcProfile_Distracted.asset", .6f),
        ("Assets/Data/NpcProfiles/NpcProfile_Social.asset", .25f), ("Assets/Data/NpcProfiles/NpcProfile_Relaxed.asset", .2f),
        ("Assets/Data/NpcProfiles/NpcProfile_Shy.asset", .15f),
    };

    /// <summary>The recap's "Tomorrow:" line about each day, at the closing before (at most 8 words). PLACEHOLDER COPY.</summary>
    public const string Day2Preview = "Grace again, and a customer's pocket watch.";
    public const string Day3Preview = "a full house, and a cracked screen.";
    public const string Day4Preview = "a frozen phone, and three at once.";
    public const int PreviewWords = 8;

    // ---------- the menu ----------

    [MenuItem(MenuG, false, 21)]
    static void StepG() => Perform("Days G - The pacing pass (data)", "days-g-pacing", false, Survey, Change, Checks);

    [MenuItem(MenuH, false, 22)]
    static void StepH() => Perform("Days H - Day 1's two settled guests (scene)", "days-h-settled-guests", true, GuestsSurvey, GuestsChange, GuestsChecks);

    [MenuItem(MenuG, true)]
    [MenuItem(MenuH, true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    // ---------- the driver (as DaysOneToThreeSteps.Perform): survey → change (one undo group) → checks → save, or undo it all ----------

    static void Perform(string title, string label, bool needsScene, Action<Run> survey, Action<Run> change, Action<Run> checks)
    {
        var run = new Run(title, label);
        run.Text.AppendLine($"{title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        int undo = -1;
        bool began = false;
        Scene scene = default;
        try
        {
            run.Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first (Edit › Play Mode › Play).");
            if (needsScene) scene = Playtest4Shared.RequireCafeScene(run);

            run.Section("Survey");
            survey(run);

            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            began = true;

            run.Section("Change (old → new)");
            change(run);

            run.Section("Checks");
            checks(run);
            if (run.Failures > 0) throw new Playtest4Shared.Refused($"{run.Failures} check(s) failed, so the whole change is undone.");

            Undo.CollapseUndoOperations(undo);
            foreach (Object asset in run.assetsToSave)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                run.Info("saved asset " + AssetDatabase.GetAssetPath(asset));
            }
            foreach (Action a in run.afterPass) a();
            if (needsScene && scene.IsValid() && scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
                run.Info("saved the scene " + scene.path);
            }
            run.Text.AppendLine().AppendLine(run.Changes == 0
                ? $"NOTHING TO CHANGE: already done. {run.Passes} check(s) passed."
                : $"DONE: {run.Changes} change(s); {run.Passes} check(s) passed; saved. Next: Fixit Fidget › Checks › Days 1–3.");
            Debug.Log(Tag + $"{(run.Changes == 0 ? "PASS" : "DONE")}: {title}: {(run.Changes == 0 ? "already done" : "done")} ({run.Changes} change(s), {run.Passes} check(s) passed). Report: {Write(run)}");
        }
        catch (Exception e)
        {
            if (began)
            {
                Undo.RevertAllDownToGroup(undo);
                run.Text.AppendLine("  (every change of this step is undone; nothing was saved)");
            }
            foreach (Action a in run.onFail)
            {
                try { a(); }
                catch (Exception cleanup) { run.Text.AppendLine("  (cleanup: " + cleanup.Message + ")"); }
            }
            bool refused = e is Playtest4Shared.Refused;
            run.Text.AppendLine().AppendLine((refused ? "REFUSED: " : "ERROR: ") + e.Message);
            if (!refused) run.Text.AppendLine(e.ToString());
            Debug.LogError(Tag + $"{title}: {(refused ? "refused" : "error")}: {e.Message}  Report: {Write(run)}");
        }
    }

    static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Days");

    static string Write(Run run)
    {
        Directory.CreateDirectory(LogRoot);
        string path = Path.Combine(LogRoot, $"{run.Label}-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(path, run.Text.ToString());
        File.WriteAllText(Path.Combine(LogRoot, $"{run.Label}-last.txt"), run.Text.ToString());
        return path;
    }

    // =================================================================================================================
    // G. The pacing pass (data)
    // =================================================================================================================

    static DayDefinition day1, day2, day3, day4, day5;
    static GameObject phone, watch, call, thriftWatch;
    static bool callIsReal;
    static readonly List<NpcMovementProfile> profiles = new List<NpcMovementProfile>();

    static DayDefinition LoadDay(Run run, string path, int number)
    {
        var day = AssetDatabase.LoadAssetAtPath<DayDefinition>(path);
        run.Require(day != null, $"No day asset at {path}.");
        run.Require(day.dayNumber == number, $"{path} is day {day.dayNumber}, not Day {number}. Nothing changed; tell Claude.");
        // Refuses until the new DayDefinition fields have compiled (24 Oct 2026).
        var so = new SerializedObject(day);
        run.Prop(so, "thriftFind");
        run.Prop(so, "gossip");
        run.Prop(so, "previewLine");
        return day;
    }

    static void Survey(Run run)
    {
        phone = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
        watch = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
        call = AssetDatabase.LoadAssetAtPath<GameObject>(CallPath);
        thriftWatch = AssetDatabase.LoadAssetAtPath<GameObject>(ThriftWatchPath);
        run.Require(phone != null && watch != null, $"The walk-ins' devices are at {PhonePath} and {WatchPath}.");
        run.Require(thriftWatch != null, $"Day 1's find isn't made yet: run Fixit Fidget › Days › Thrift 1 first ({ThriftWatchPath}). Nothing changed.");
        run.Require(Faults(phone) >= 4 && Faults(watch) >= 2 && Faults(thriftWatch) >= 1,
            $"The phone has {Faults(phone)} faults (4 wanted: the screen, the gunk, the circuit, the Human fault), the watch {Faults(watch)} (2), the thrift watch {Faults(thriftWatch)} (1). Nothing changed; tell Claude.");
        day1 = LoadDay(run, Day1Path, 1);
        day2 = LoadDay(run, Day2Path, 2);
        day3 = LoadDay(run, Day3Path, 3);
        day4 = LoadDay(run, Day4Path, 4);
        day5 = LoadDay(run, Day5Path, 5);
        callIsReal = call != null && SupportCallSteps.HasRealModel(call);
        profiles.Clear();
        foreach (var (path, _) in LeavesCup)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NpcMovementProfile>(path);
            run.Require(profile != null, $"No movement profile at {path}. Nothing changed.");
            run.Prop(new SerializedObject(profile), "leavesCup");   // refuses until the new field has compiled
            profiles.Add(profile);
        }
        run.Info(callIsReal
            ? "the support call's phone has the detailed phone's parts: it goes on Day 3's walk-in list (his answer p4)"
            : $"the support call's phone is still the grey block{(call == null ? $" (no {CallPath})" : "")}: Day 3 leaves it out (his answer p4: \"once it has its real phone model\"). " +
              "Run Fixit Fidget › Days › Support call 1, then this step again, to put it in.");
        foreach (DayDefinition d in new[] { day1, day2, day3, day4, day5 }) run.Info($"Day {d.dayNumber} now: {Summary(d)}");
    }

    static int Faults(GameObject prefab)
    {
        DeviceDefinition def = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
        return def != null && def.faults != null ? def.faults.Length : 0;
    }

    static void Change(Run run)
    {
        // Day 1, Monday: the clean, the first drink, a walk-in's gunked phone, the thrift watch's glass, the first table.
        Float(run, day1, "drinkOnlyChance", Day1DrinkOnlyWas, Day1DrinkOnly, "1. Day 1 drinkOnlyChance (the walk-in phone comes sooner: about 3:23)");
        SetFind(run, day1, Day1Find, "2. Day 1 thriftFind (his answer p1: a clean, Day 1's tools only; free, on the shelf at the open)");
        SetStrings(run, day1, "gossip", new string[0], "3. Day 1 gossip (none: chat is Day 2's lesson)");

        // Day 2, Tuesday: the swap, the juggle, the screwdriver on a customer's watch, a chat, a refill.
        SetPhases(run, day2, Day2PhasesWas, Day2Phases, "4");
        Float(run, day2, "patienceMultiplier", Day2PatienceWas, Day2Patience, "5. Day 2 patienceMultiplier (a slow player loses 0.09, against 0.64 at x1.4)");
        Float(run, day2, "drinkOnlyChance", Day2DrinkOnlyWas, Day2DrinkOnly, "6. Day 2 drinkOnlyChance");
        SetWalkIns(run, day2, Day2WalkIns, "7");
        SetFind(run, day2, Day2Find, "8. Day 2 thriftFind (Day 3's first walk-in, with nobody waiting)");
        SetStrings(run, day2, "gossip", Day2Gossip, "9. Day 2 gossip (the chat lesson; feeds Night 2)");
        Text(run, day2, "previewLine", Day2Preview, "10. Day 2 previewLine (Day 1's recap: \"Tomorrow: ...\")");

        // Day 3, Wednesday: the first real day, two walk-ins at once (his answer p3).
        SetPhases(run, day3, Day3PhasesWas, Day3Phases, "11");
        Float(run, day3, "drinkOnlyChance", Day3DrinkOnlyWas, Day3DrinkOnly, "12. Day 3 drinkOnlyChance");
        Int(run, day3, "maxCustomers", Day3AtOnce, "13. Day 3 maxCustomers (his answer p3: two at once)");
        Float(run, day3, "patienceMultiplier", Day3Patience, Day3Patience, "13. Day 3 patienceMultiplier (unchanged)");
        SetWalkIns(run, day3, Day3WalkIns(callIsReal), "14");
        SetFind(run, day3, Day3Find, "15. Day 3 thriftFind (the circuit: Day 4's new job, with nobody waiting)");
        SetStrings(run, day3, "gossip", Day3Gossip, "16. Day 3 gossip (the story: Night 3 has no errand)");
        Text(run, day3, "previewLine", Day3Preview, "17. Day 3 previewLine (Day 2's recap)");

        // Days 4-5: eased into a ramp (no cliff after Day 3).
        SetPhases(run, day4, Day4PhasesWas, Day4Phases, "18");
        Int(run, day4, "maxCustomers", Day4AtOnce, "19. Day 4 maxCustomers (was 6)");
        Float(run, day4, "patienceMultiplier", Day4PatienceWas, Day4Patience, "20. Day 4 patienceMultiplier");
        SetWalkIns(run, day4, Day4WalkIns, "21");
        SetFind(run, day4, Day4Find, "22. Day 4 thriftFind");
        Text(run, day4, "previewLine", Day4Preview, "23. Day 4 previewLine (Day 3's recap)");
        SetPhases(run, day5, Day5PhasesWas, Day5Phases, "24");
        Int(run, day5, "maxCustomers", Day5AtOnce, "25. Day 5 maxCustomers (was 6)");
        Float(run, day5, "patienceMultiplier", Day5PatienceWas, Day5Patience, "26. Day 5 patienceMultiplier");
        run.Info("27. Days 4-5 gossip: left empty (§2.4: his sister writes them; flagged in the report)");

        // Who leaves a cup on the table (clearing tables, §2.3).
        for (int i = 0; i < LeavesCup.Length; i++)
        {
            float was = run.Prop(new SerializedObject(profiles[i]), "leavesCup").floatValue;
            run.SetFloat(profiles[i], "leavesCup", LeavesCup[i].chance, $"28. {profiles[i].name}.leavesCup (leaves the cup on the table)");
            if (!Near(was, LeavesCup[i].chance)) run.SaveAsset(profiles[i]);
        }
    }

    static void Checks(Run run)
    {
        run.Section("  Day 1");
        CheckPhases(run, day1, Day1Phases);
        run.Check(Near(day1.drinkOnlyChance, Day1DrinkOnly), $"Day 1: {day1.drinkOnlyChance:0.##} of walk-ins come for a drink ({Day1DrinkOnly:0.##})");
        run.Check(Near(day1.patienceMultiplier, 2f) && day1.maxCustomers == 2, $"Day 1: patience x{day1.patienceMultiplier:0.##}, {day1.maxCustomers} at once (x2, 2: unchanged)");
        CheckWalkIns(run, day1, Day1WalkIns);
        CheckFind(run, day1, Day1Find);
        run.Check(day1.GossipOn(1).Length == 0, "Day 1: no gossip");
        CheckFindIsNotTheWalkIn(run, day1);

        run.Section("  Day 2");
        CheckPhases(run, day2, Day2Phases);
        run.Check(Near(day2.patienceMultiplier, Day2Patience), $"Day 2: patience x{day2.patienceMultiplier:0.##} ({Day2Patience:0.#})");
        run.Check(Near(day2.drinkOnlyChance, Day2DrinkOnly), $"Day 2: {day2.drinkOnlyChance:0.##} come for a drink ({Day2DrinkOnly:0.##})");
        run.Check(day2.maxCustomers == 2, $"Day 2: {day2.maxCustomers} at once (2, unchanged)");
        CheckWalkIns(run, day2, Day2WalkIns);
        CheckFind(run, day2, Day2Find);
        CheckGossip(run, day2, Day2Gossip);
        CheckPreview(run, day2, Day2Preview);
        CheckFindIsNotTheWalkIn(run, day2);

        run.Section("  Day 3");
        CheckPhases(run, day3, Day3Phases);
        run.Check(Near(day3.drinkOnlyChance, Day3DrinkOnly), $"Day 3: {day3.drinkOnlyChance:0.##} come for a drink ({Day3DrinkOnly:0.##})");
        run.Check(day3.maxCustomers == Day3AtOnce && Near(day3.patienceMultiplier, Day3Patience), $"Day 3: {day3.maxCustomers} at once, patience x{day3.patienceMultiplier:0.##} (2, x1.6)");
        CheckWalkIns(run, day3, Day3WalkIns(callIsReal));
        run.Check(callIsReal == day3.WalkInRepairsOn(3).Any(e => e != null && e.devicePrefab == call),
            callIsReal ? "Day 3: the support call is on the list (its phone is real)" : "Day 3: no support call yet (its phone is still the grey block)");
        CheckFind(run, day3, Day3Find);
        CheckGossip(run, day3, Day3Gossip);
        CheckPreview(run, day3, Day3Preview);
        CheckFindIsNotTheWalkIn(run, day3);

        run.Section("  Days 4-5");
        CheckPhases(run, day4, Day4Phases);
        run.Check(day4.maxCustomers == Day4AtOnce && Near(day4.patienceMultiplier, Day4Patience), $"Day 4: {day4.maxCustomers} at once, patience x{day4.patienceMultiplier:0.##} (3, x1.45)");
        CheckWalkIns(run, day4, Day4WalkIns);
        CheckFind(run, day4, Day4Find);
        CheckPreview(run, day4, Day4Preview);
        CheckPhases(run, day5, Day5Phases);
        run.Check(day5.maxCustomers == Day5AtOnce && Near(day5.patienceMultiplier, Day5Patience), $"Day 5: {day5.maxCustomers} at once, patience x{day5.patienceMultiplier:0.##} (3, x1.3)");
        run.Check(day5.ThriftFindOn(5) == null && string.IsNullOrEmpty(day5.previewLine), "Day 5: no authored find (the Shop rotates), no Tomorrow line");

        run.Section("  Who leaves a cup (clearing tables)");
        float weights = 0f, share = 0f;
        for (int i = 0; i < LeavesCup.Length; i++)
        {
            run.Check(Near(profiles[i].leavesCup, LeavesCup[i].chance), $"{profiles[i].name}: leaves the cup {profiles[i].leavesCup:0.##} of the time ({LeavesCup[i].chance:0.##})");
            weights += profiles[i].weight;
            share += profiles[i].weight * profiles[i].leavesCup;
        }
        share = weights > 0f ? share / weights : 0f;
        run.Check(share >= .25f && share <= .42f, $"about a third of guests leave their cup ({share:0.00}, weighted by the profiles' weights; 0.25-0.42)");

        run.Section("  The chain: each find is the next day's walk-in");
        run.Check(day2.WalkInRepairsOn(2).Length > 0 && day2.WalkInRepairsOn(2)[0].devicePrefab == watch && thriftWatch.GetComponent<DeviceDefinition>().displayName == watch.GetComponent<DeviceDefinition>().displayName,
            "Day 1's find is a pocket watch; Day 2's walk-in is the pocket watch (tomorrow's device, today's tools)");
        run.Check(Same(day2.ThriftFindOn(2), day3.WalkInRepairsOn(3).FirstOrDefault()), "Day 2's find (the cracked screen) is Day 3's first walk-in");
        run.Check(Same(day3.ThriftFindOn(3), day4.WalkInRepairsOn(4).FirstOrDefault()), "Day 3's find (the circuit) is Day 4's first walk-in");
    }

    static bool Same(FeaturedRepairRequest a, FeaturedRepairRequest b) =>
        a != null && b != null && a.devicePrefab == b.devicePrefab && a.faultIndex == b.faultIndex;

    // ---------- G's setters: record, change, report old → new, save the asset once the step passes ----------

    static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-3f;

    static void Float(Run run, DayDefinition day, string path, float was, float now, string item)
    {
        float current = run.Prop(new SerializedObject(day), path).floatValue;
        if (!Near(current, now) && !Near(current, was))
            run.Info($"{item}: NOTE it was {current:0.###}, not the expected {was:0.###} (tuned by hand?); set anyway");
        run.SetFloat(day, path, now, item);
        if (!Near(current, now)) run.SaveAsset(day);
    }

    static void Int(Run run, DayDefinition day, string path, int now, string item)
    {
        int current = run.Prop(new SerializedObject(day), path).intValue;
        run.SetInt(day, path, now, item);
        if (current != now) run.SaveAsset(day);
    }

    static void Text(Run run, DayDefinition day, string path, string now, string item)
    {
        string current = run.Prop(new SerializedObject(day), path).stringValue;
        run.SetString(day, path, now, item);
        if (current != now) run.SaveAsset(day);
    }

    static void SetStrings(Run run, DayDefinition day, string path, string[] now, string item)
    {
        var so = new SerializedObject(day);
        SerializedProperty list = run.Prop(so, path);
        var current = new string[list.arraySize];
        for (int i = 0; i < current.Length; i++) current[i] = list.GetArrayElementAtIndex(i).stringValue;
        string Words(string[] a) => "[" + string.Join(", ", a) + "]";
        if (current.SequenceEqual(now)) { run.Info($"{item}: already {Words(now)}"); return; }
        run.Record(day);
        list.arraySize = now.Length;
        for (int i = 0; i < now.Length; i++) list.GetArrayElementAtIndex(i).stringValue = now[i];
        so.ApplyModifiedProperties();
        run.Changed($"{item}: {Words(current)} → {Words(now)}");
        run.SaveAsset(day);
    }

    static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    static void SetFind(Run run, DayDefinition day, FindRow find, string item)
    {
        GameObject prefab = Prefab(find.prefab);
        run.Require(prefab != null, $"{item}: no prefab at {find.prefab}.");
        var so = new SerializedObject(day);
        SerializedProperty device = run.Prop(so, "thriftFind.devicePrefab"), fault = run.Prop(so, "thriftFind.faultIndex"),
            episode = run.Prop(so, "thriftFind.storyEpisodeId"), buy = run.Prop(so, "thriftBuy"), sale = run.Prop(so, "thriftSale");
        string was = $"{EntryWords(device.objectReferenceValue as GameObject, fault.intValue)}, buy ${buy.intValue}, sells ${sale.intValue}";
        string now = $"{EntryWords(prefab, find.fault)}, buy ${find.buy}, sells ${find.sale}";
        if (device.objectReferenceValue == prefab && fault.intValue == find.fault && string.IsNullOrEmpty(episode.stringValue)
            && buy.intValue == find.buy && sale.intValue == find.sale)
        {
            run.Info($"{item}: already {now}");
            return;
        }
        run.Record(day);
        device.objectReferenceValue = prefab;
        fault.intValue = find.fault;
        episode.stringValue = "";
        buy.intValue = find.buy;
        sale.intValue = find.sale;
        so.ApplyModifiedProperties();
        run.Changed($"{item}: {was} → {now}");
        run.SaveAsset(day);
    }

    static (GameObject prefab, int fault)[] Resolve((string prefab, int fault)[] list) => list.Select(e => (Prefab(e.prefab), e.fault)).ToArray();

    static (GameObject prefab, int fault)[] ReadWalkIns(DayDefinition day) =>
        (day.walkInRepairs ?? new FeaturedRepairRequest[0]).Select(r => r != null ? (r.devicePrefab, r.faultIndex) : ((GameObject)null, -1)).ToArray();

    static string WalkInWords((GameObject prefab, int fault)[] list) =>
        list.Length == 0 ? "[] (the old roll)" : "[" + string.Join(", ", list.Select(e => EntryWords(e.prefab, e.fault))) + "]";

    public static string EntryWords(GameObject prefab, int fault)
    {
        DeviceDefinition def = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
        string label = def != null && def.faults != null && fault >= 0 && fault < def.faults.Length && def.faults[fault] != null
            ? def.faults[fault].description : "?";
        return $"{(prefab != null ? prefab.name : "(none)")} · {fault} \"{label}\"";
    }

    static void SetWalkIns(Run run, DayDefinition day, (string prefab, int fault)[] list, string item)
    {
        var now = Resolve(list);
        run.Require(now.All(e => e.prefab != null), $"{item}: a walk-in's device is missing ({string.Join(", ", list.Select(e => e.prefab))}).");
        var current = ReadWalkIns(day);
        bool emptyIds = (day.walkInRepairs ?? new FeaturedRepairRequest[0]).All(r => r == null || string.IsNullOrEmpty(r.storyEpisodeId));
        string label = $"{item}. {day.name}: walkInRepairs (in order)";
        if (current.SequenceEqual(now) && emptyIds) { run.Info($"{label}: already {WalkInWords(now)}"); return; }
        var so = new SerializedObject(day);
        SerializedProperty walkIns = run.Prop(so, "walkInRepairs");
        run.Record(day);
        walkIns.arraySize = now.Length;
        for (int i = 0; i < now.Length; i++)
        {
            SerializedProperty e = walkIns.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("devicePrefab").objectReferenceValue = now[i].prefab;
            e.FindPropertyRelative("faultIndex").intValue = now[i].fault;
            e.FindPropertyRelative("storyEpisodeId").stringValue = "";
        }
        so.ApplyModifiedProperties();
        run.Changed($"{label}: {WalkInWords(current)} → {WalkInWords(now)}");
        run.SaveAsset(day);
    }

    static PhaseRow[] ReadPhases(DayDefinition day) =>
        day.phases == null ? new PhaseRow[0] : day.phases.Where(p => p != null).Select(p => R(p.phaseName, p.startsAt, p.spawnInterval)).ToArray();

    public static bool SamePhases(PhaseRow[] a, PhaseRow[] b) =>
        a.Length == b.Length && a.Zip(b, (x, y) => x.name == y.name && Near(x.at, y.at) && Near(x.gap, y.gap)).All(ok => ok);

    public static string PhaseWords(PhaseRow[] phases) => string.Join(" · ", phases.Select(p => $"{p.name} {p.at:0.##}: {p.gap:0.#}"));

    static void SetPhases(Run run, DayDefinition day, PhaseRow[] was, PhaseRow[] now, string item)
    {
        PhaseRow[] current = ReadPhases(day);
        string label = $"{item}. {day.name}: phases (start: gap, s)";
        if (SamePhases(current, now)) { run.Info($"{label}: already {PhaseWords(now)}"); return; }
        if (!SamePhases(current, was)) run.Info($"{label}: NOTE it was {PhaseWords(current)}, not the expected {PhaseWords(was)} (tuned by hand?); set anyway");
        var so = new SerializedObject(day);
        SerializedProperty phases = run.Prop(so, "phases");
        run.Record(day);
        phases.arraySize = now.Length;
        for (int i = 0; i < now.Length; i++)
        {
            SerializedProperty p = phases.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("phaseName").stringValue = now[i].name;
            p.FindPropertyRelative("startsAt").floatValue = now[i].at;
            p.FindPropertyRelative("spawnInterval").floatValue = now[i].gap;
        }
        so.ApplyModifiedProperties();
        run.Changed($"{label}: {PhaseWords(current)} → {PhaseWords(now)}");
        run.SaveAsset(day);
    }

    // ---------- G's checks ----------

    static void CheckPhases(Run run, DayDefinition day, PhaseRow[] now)
    {
        PhaseRow[] current = ReadPhases(day);
        bool ascending = true;
        for (int i = 1; i < current.Length; i++) ascending &= current[i].at >= current[i - 1].at;
        run.Check(SamePhases(current, now) && ascending, $"{day.name}: phases {PhaseWords(current)} (start: gap in seconds, ascending)");
    }

    static void CheckWalkIns(Run run, DayDefinition day, (string prefab, int fault)[] list)
    {
        var want = Resolve(list);
        var current = ReadWalkIns(day);
        FeaturedRepairRequest[] today = day.WalkInRepairsOn(day.dayNumber);
        bool all = today.All(e => e != null && string.IsNullOrEmpty(e.storyEpisodeId) && e.TryCreateJob(out _, out _));
        run.Check(current.SequenceEqual(want) && all && today.Length == want.Length && day.WalkInRepairsOn(day.dayNumber + 1).Length == 0,
            $"{day.name}: the walk-ins' repairs, in order, on its own day only, each makes a job: {WalkInWords(current)}");
    }

    static void CheckFind(Run run, DayDefinition day, FindRow find)
    {
        FeaturedRepairRequest f = day.ThriftFindOn(day.dayNumber);
        string error = "none authored";
        bool made = f != null && f.TryCreateJob(out _, out error);
        run.Check(made && f.devicePrefab == Prefab(find.prefab) && f.faultIndex == find.fault && string.IsNullOrEmpty(f.storyEpisodeId)
                  && day.thriftBuy == find.buy && day.thriftSale == find.sale && day.ThriftFindOn(day.dayNumber + 1) == null,
            $"{day.name}: the find is {(f != null ? EntryWords(f.devicePrefab, f.faultIndex) : "(none)")}, buy ${day.thriftBuy}, sells ${day.thriftSale} " +
            $"({EntryWords(Prefab(find.prefab), find.fault)}, ${find.buy}, ${find.sale} wanted){(made ? "" : ": " + error)}");
        // §5.2: a customer is always worth more (the find sells for less than the same job's Perfect pay). The thrift watch's
        // clean is nobody's job, so it is weighed against the watch's own cleaning job (a customer's grit inside, $94).
        if (made)
        {
            FeaturedRepairRequest same = f.devicePrefab == thriftWatch ? new FeaturedRepairRequest { devicePrefab = watch, faultIndex = WatchDebris } : f;
            int customerPerfect = Mathf.RoundToInt(FaultPayout(same) * JobBase.PayMultiplier(JobGrade.Perfect));
            run.Check(find.sale < customerPerfect,
                $"{day.name}: the find sells for ${find.sale}, under a customer's ${customerPerfect} for {EntryWords(same.devicePrefab, same.faultIndex)} at Perfect (a customer always wins)");
        }
    }

    static int FaultPayout(FeaturedRepairRequest f)
    {
        DeviceDefinition def = f.devicePrefab.GetComponent<DeviceDefinition>();
        return def != null && def.faults != null && f.faultIndex < def.faults.Length && def.faults[f.faultIndex] != null ? def.faults[f.faultIndex].payout : 0;
    }

    static void CheckFindIsNotTheWalkIn(Run run, DayDefinition day)
    {
        FeaturedRepairRequest f = day.ThriftFindOn(day.dayNumber);
        bool clash = f != null && day.WalkInRepairsOn(day.dayNumber).Any(e => e != null && e.devicePrefab == f.devicePrefab && e.faultIndex == f.faultIndex);
        run.Check(f != null && !clash, $"{day.name}: the find is never the same device and fault as one of the day's walk-ins");
    }

    static void CheckGossip(Run run, DayDefinition day, string[] want)
    {
        string[] ids = day.GossipOn(day.dayNumber);
        bool known = ids.All(id => NotebookEntries.GossipFor(id) != null);
        run.Check(ids.SequenceEqual(want) && known && day.GossipOn(day.dayNumber + 1).Length == 0,
            $"{day.name}: gossip [{string.Join(", ", ids)}], each in the gossip table (NotebookEntries), on its own day only");
    }

    static void CheckPreview(Run run, DayDefinition day, string want)
    {
        int words = WordBudget.Words(day.previewLine ?? "");
        run.Check(day.previewLine == want && words <= PreviewWords, $"{day.name}: the recap's \"Tomorrow: {day.previewLine}\" ({words} words; {PreviewWords} at most)");
    }

    static string Summary(DayDefinition day) =>
        $"phases {PhaseWords(ReadPhases(day))}; patience x{day.patienceMultiplier:0.##}; at once {day.maxCustomers}; drink-only {day.drinkOnlyChance:0.##}; " +
        $"walk-ins {WalkInWords(ReadWalkIns(day))}; find {(day.thriftFind != null && day.thriftFind.devicePrefab != null ? EntryWords(day.thriftFind.devicePrefab, day.thriftFind.faultIndex) : "none")} " +
        $"(${day.thriftBuy} → ${day.thriftSale}); gossip [{string.Join(", ", day.gossip ?? new string[0])}]; preview \"{day.previewLine}\"";

    // =================================================================================================================
    // H. Day 1's two settled guests (scene)
    //
    // r3, his answer: "two settled guests" on Day 1 (four from Day 2, P's §4.2). NPC wave 2 put three entries in (a reader in
    // the reading corner, a pair at a banquette 2-top, a laptop worker at the communal table: four people) for Days 1-3. On
    // the half-finished Day 1 those seats are under dust sheets (F2's: the reading corner, the banquettes and the communal
    // table wait), so: the three move to Days 2-3 (four people from Day 2, as P's §8.1), and two new entries sit on Day 1 only,
    // in two of its 10 open seats: a reader in the lounge's tub chairs and someone at the north bistro 2-top (F2's open one).
    // Neither is a stool (the repair bar's and the window counter's are kept for customers). They leave at 0.30 and 0.40 of
    // the day (2:06 and 2:48), so their seats free up before Day 1's rush, and the first table to clear comes near 3:00.
    // PatronSpawner itself is unchanged: the step only writes its list (F2 owns its fallback for a sheeted seat).
    // =================================================================================================================

    sealed class Guest
    {
        public string label;
        public string[] seats, alternative;
        public float leaveAt;
    }

    public const string ReaderLabel = "a reader in the lounge (Day 1)", BistroLabel = "someone at the bistro (Day 1)";
    static readonly Guest[] DayOneGuests =
    {
        new Guest { label = ReaderLabel, seats = new[] { "Seat (lounge, tub chair A)" }, alternative = new[] { "Seat (lounge, tub chair B)" }, leaveAt = .30f },
        new Guest { label = BistroLabel, seats = new[] { "Seat (bistro 2-top, north, west)" }, alternative = new[] { "Seat (bistro 2-top, north, east)" }, leaveAt = .40f },
    };
    /// <summary>NPC wave 2's three (four people): from Day 2 now.</summary>
    static readonly string[] WaveTwoLabels = { "a reader", "a pair", "a laptop worker" };
    public const int WaveTwoFirstDay = 2, WaveTwoLastDay = 3, DayOnePeople = 2, FromDayTwoPeople = 4;

    static PatronSpawner patrons;
    static readonly Dictionary<string, TableSeat> seatsByName = new Dictionary<string, TableSeat>();

    static void GuestsSurvey(Run run)
    {
        patrons = Playtest4Shared.One<PatronSpawner>(run);
        seatsByName.Clear();
        TableSeat[] all = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Include);
        foreach (Guest g in DayOneGuests)
            foreach (string name in g.seats.Concat(g.alternative))
            {
                TableSeat[] named = all.Where(s => s.name == name).ToArray();
                run.Require(named.Length == 1, $"{g.label}: expected one seat named \"{name}\" in the café scene, found {named.Length} (Seats 1 and Cafe dressing 3 lay them). Nothing changed.");
                run.Require(!named[0].RepairBar, $"{g.label}: {name} is a repair-bar stool: settled guests never take one. Nothing changed.");
                run.Require(named[0].SnapToSeat, $"{g.label}: {name} has Snap To Seat off: a settled guest must sit. Nothing changed.");
                seatsByName[name] = named[0];
            }
        var so = new SerializedObject(patrons);
        SerializedProperty list = run.Prop(so, "openingGuests");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            run.Info($"settled guest {i + 1} now: \"{e.FindPropertyRelative("label").stringValue}\", Days {e.FindPropertyRelative("firstDay").intValue}-{e.FindPropertyRelative("lastDay").intValue}, " +
                     $"leaves at {e.FindPropertyRelative("leaveAt").floatValue:0.00}, {e.FindPropertyRelative("seats").arraySize} seat(s)");
        }
        foreach (string label in WaveTwoLabels)
            run.Require(Find(list, label) >= 0, $"NPC wave 2's \"{label}\" isn't in PatronSpawner's settled guests (Fixit Fidget › NPC › Wave 2 2 puts them in). Nothing changed.");
    }

    static int Find(SerializedProperty list, string label)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == label) return i;
        return -1;
    }

    static void GuestsChange(Run run)
    {
        var so = new SerializedObject(patrons);
        SerializedProperty list = so.FindProperty("openingGuests");
        bool changed = false;
        var lines = new List<string>();
        foreach (string label in WaveTwoLabels)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(Find(list, label));
            SerializedProperty first = e.FindPropertyRelative("firstDay"), last = e.FindPropertyRelative("lastDay");
            if (first.intValue == WaveTwoFirstDay && last.intValue == WaveTwoLastDay) { run.Info($"\"{label}\": already Days {WaveTwoFirstDay}-{WaveTwoLastDay}"); continue; }
            lines.Add($"\"{label}\": Days {first.intValue}-{last.intValue} → {WaveTwoFirstDay}-{WaveTwoLastDay} (their seats are under dust sheets on Day 1)");
            if (!changed) { run.Record(patrons); changed = true; }
            first.intValue = WaveTwoFirstDay;
            last.intValue = WaveTwoLastDay;
        }
        foreach (Guest g in DayOneGuests)
        {
            int at = Find(list, g.label);
            TableSeat[] seats = g.seats.Select(n => seatsByName[n]).ToArray(), alternative = g.alternative.Select(n => seatsByName[n]).ToArray();
            if (at >= 0 && Matches(list.GetArrayElementAtIndex(at), g, seats, alternative)) { run.Info($"\"{g.label}\": already {Describe(g)}"); continue; }
            if (!changed) { run.Record(patrons); changed = true; }
            if (at < 0)
            {
                at = list.arraySize;
                list.arraySize = at + 1;
            }
            SerializedProperty e = list.GetArrayElementAtIndex(at);
            e.FindPropertyRelative("label").stringValue = g.label;
            SetSeats(e.FindPropertyRelative("seats"), seats);
            SetSeats(e.FindPropertyRelative("alternative"), alternative);
            e.FindPropertyRelative("laptop").boolValue = false;
            e.FindPropertyRelative("leaveAt").floatValue = g.leaveAt;
            e.FindPropertyRelative("firstDay").intValue = 1;
            e.FindPropertyRelative("lastDay").intValue = 1;
            lines.Add($"\"{g.label}\": {Describe(g)}");
        }
        if (!changed) return;
        so.ApplyModifiedProperties();
        foreach (string line in lines) run.Changed("PatronSpawner.openingGuests: " + line);
    }

    static string Describe(Guest g) =>
        $"{string.Join(" + ", g.seats)} (or {string.Join(" + ", g.alternative)}), Day 1 only, leaves at {g.leaveAt:0.00} of the day ({g.leaveAt * 420f:0} s at 420 s)";

    static void SetSeats(SerializedProperty array, TableSeat[] seats)
    {
        array.arraySize = seats.Length;
        for (int i = 0; i < seats.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = seats[i];
    }

    static bool SameSeats(SerializedProperty array, TableSeat[] seats)
    {
        if (array.arraySize != seats.Length) return false;
        for (int i = 0; i < seats.Length; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue != seats[i]) return false;
        return true;
    }

    static bool Matches(SerializedProperty e, Guest g, TableSeat[] seats, TableSeat[] alternative) =>
        e.FindPropertyRelative("label").stringValue == g.label && !e.FindPropertyRelative("laptop").boolValue
        && Mathf.Abs(e.FindPropertyRelative("leaveAt").floatValue - g.leaveAt) < 1e-4f
        && e.FindPropertyRelative("firstDay").intValue == 1 && e.FindPropertyRelative("lastDay").intValue == 1
        && SameSeats(e.FindPropertyRelative("seats"), seats) && SameSeats(e.FindPropertyRelative("alternative"), alternative);

    /// <summary>The people the settled guests seat on <paramref name="day"/> (a pair is two): the checks' count.</summary>
    public static int PeopleOn(PatronSpawner.OpeningGuest[] guests, int day) =>
        (guests ?? new PatronSpawner.OpeningGuest[0]).Where(g => g != null && g.firstDay <= day && day <= g.lastDay)
            .Sum(g => g.seats != null ? g.seats.Length : 0);

    static void GuestsChecks(Run run)
    {
        PatronSpawner.OpeningGuest[] guests = patrons.OpeningGuests;
        int one = PeopleOn(guests, 1), two = PeopleOn(guests, 2), three = PeopleOn(guests, 3), four = PeopleOn(guests, 4);
        run.Check(one == DayOnePeople, $"Day 1: {one} settled guests ({DayOnePeople}: his answer r3)");
        run.Check(two == FromDayTwoPeople && three == FromDayTwoPeople, $"Days 2 and 3: {two} and {three} settled guests ({FromDayTwoPeople}: P's §8.1)");
        run.Check(four == 0, $"Day 4: {four} settled guests (Days 1-3 only, the Producer's call 13)");
        foreach (PatronSpawner.OpeningGuest g in guests.Where(g => g != null && g.firstDay <= 1))
        {
            TableSeat[] all = (g.seats ?? new TableSeat[0]).Concat(g.alternative ?? new TableSeat[0]).ToArray();
            bool open = all.All(s => s != null && DayOneGuests.Any(d => d.seats.Contains(s.name) || d.alternative.Contains(s.name)));
            run.Check(open && all.All(s => !s.RepairBar && s.SnapToSeat), $"\"{g.label}\": sits in Day 1's open seats ({string.Join(", ", all.Select(s => s != null ? s.name : "(none)"))}), no stool, Snap To Seat on");
            run.Check(g.leaveAt <= .45f, $"\"{g.label}\": leaves at {g.leaveAt:0.00} of the day, before Day 1's rush (0.55)");
        }
        foreach (string label in WaveTwoLabels)
        {
            PatronSpawner.OpeningGuest g = guests.FirstOrDefault(x => x != null && x.label == label);
            run.Check(g != null && g.firstDay == WaveTwoFirstDay && g.lastDay == WaveTwoLastDay, $"\"{label}\": Days {g?.firstDay}-{g?.lastDay} ({WaveTwoFirstDay}-{WaveTwoLastDay})");
        }
    }
}
#endif
