#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Run = Playtest4Shared.Run;

// ---------------------------------------------------------------------------
// DAYS 1-3 AS AN INTRO LEVEL: THE DATA (14 Oct 2026; specs/days-1-3.md §9, specs/systems-forgiving-bench.md §7 step 11, the
// Producer's calls in feel/feel-targets-2026-10-14.md: 5, 7, 8 and 14)
//
// His words after playtest 5: "the playtest couldn't even get passed day 1 for me, so how could i sell that to anyone else".
// The scene's day was 180 s and Grace's four-task camera ate all of it. These steps make Days 1-3 an intro level: a 420 s day,
// Grace's camera in two visits (Day 1 the clean, Day 2 the shutter), the days' arrivals re-authored, and the walk-ins' repairs
// in the order the game teaches them.
//
//   Fixit Fidget > Days > Days 1–3 - the intro level (data)     all of A-F below in one undo group and one report
//   Fixit Fidget > Days > A - The day: 420 s (scene)            item 1   (the café scene open; saves it)
//   Fixit Fidget > Days > B - Grace's camera: the clean and the shutter (prefab)   items 2-4
//   Fixit Fidget > Days > C - Day 1: Grace's clean (data)       items 5-11
//   Fixit Fidget > Days > D - Day 2: Grace's shutter (data)     items 12-17
//   Fixit Fidget > Days > E - Day 3: the screwdriver (data)     items 18-25
//   Fixit Fidget > Days > F - Days 4-5: the 420 s stopgap (data)   items 26-27 (the Producer's call 14)
//   Fixit Fidget > Checks > Days 1–3                            read-only: the spec's §10 check
//
// Every step: Edit Mode only; a survey that refuses with the reason and changes nothing; one undo group (the prefab's contents
// have no undo in Unity: it is saved only when every check passes, and the report holds before → after); old → new for every
// item; checks in numbers, any failure undoes the whole step before anything is saved; idempotent (a second run reports
// "already" for every item and writes nothing). The day assets and the prefab are saved when the step passes; step A (and the
// all-in-one) saves the café scene too, which must be open and saved before it starts, so the step's changes are its own.
// Reports: Logs/Days/<step>-<time>.txt and <step>-last.txt, and the Console.
//
// 24 Oct 2026 (Days): Designer P's pacing pass (Docs/Studio/sessions/2026-10-23/designs/pacing-downtime-spec.md §1.5, §6.1),
// approved by him. Fixit Fidget > Days > G - The pacing pass (PacingSteps) writes the new numbers; these steps write and check
// the same ones (PacingSteps' constants), never the 14 Oct ones, so running either never undoes the other. Moved: Day 1's
// drink-only share (.70 → .55); Day 2's phases, patience (x1.4 → x1.8), drink-only share (.70 → .45) and walk-in (the
// gunked phone → the watch's grit); Day 3's phases, drink-only share (.45 → .35) and walk-ins (the screen, the seized
// watch, the gunked phone, and the support call once its phone is real: his answer p4); Days 4-5 (F) from the stopgap to
// the ramp (phases, x1.45 and x1.3, three at once). The check's "only taught verbs" rule (days spec §10): Day 2 teaches
// the screwdriver and the pry too (the watch's four screws), the support call is Day 3's (p4), and a thrift find may use
// tomorrow's verb, because it is nobody's job.
// ---------------------------------------------------------------------------
public static class DaysOneToThreeSteps
{
    const string Menu = "Fixit Fidget/Days/";
    const string Tag = "[Days 1-3] ";
    public const string CheckMenu = "Fixit Fidget/Checks/Days 1–3";

    public const string CameraPath = "Assets/GraceShowcase/GraceReunionCamera.prefab";
    public const string Day1Path = "Assets/GraceShowcase/Day_01_GraceCamera.asset";
    public const string Day2Path = "Assets/GraceShowcase/Day_02_GraceReturn.asset";
    public const string Day3Path = "Assets/Data/Days/Day_03_TheRealGame.asset";
    public const string Day4Path = "Assets/Data/Days/Day_04_Pressure.asset";
    public const string Day5Path = "Assets/Data/Days/Day_05_TwoRushes.asset";
    public const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab";
    public const string WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab";
    public const string GracePath = "Assets/Data/Regulars/Regular_Grace.asset";
    public const string OfficerPath = "Assets/Data/Regulars/Regular_Officer.asset";

    /// <summary>The day, every day (specs/days-1-3.md §7): 420 s; it was 180.</summary>
    public const float DayLength = 420f, OldDayLength = 180f;
    /// <summary>Day 1's look-alike of the jammed shutter: meshes only, no part, no collider (§5.2). Fault 0 turns it on.</summary>
    public const string RestingShutterName = "Shutter mechanism (resting)";
    const string RestingSuffix = " (resting)";
    public const string CleanLabel = "Dirty Lens and Film Path", ShutterLabel = "Jammed Shutter";
    public const int CleanPayout = 40, ShutterPayout = 60;
    // The device prefabs' fault indices the walk-in lists name (PhoneRepair: 0 Cracked Screen, 1 Gunked Up; PocketWatch:
    // 0 Debris Inside, 1 Seized Movement).
    public const int PhoneScreen = 0, PhoneGunked = 1, WatchDebris = 0, WatchSeized = 1;

    const string Day1Intent = "Intro: Grace's clean (cloth, brush), the Officer's first coffee; walk-ins after her.";
    const string Day2Intent = "Grace's shutter (the swap) and her latte: the juggle.";
    const string Day3Intent = "The screwdriver; the Officer's question; Grace's photo.";

    // ---------- the numbers (specs/days-1-3.md §9; "was" is the 180 s day's, read from the assets on 14 Oct) ----------

    struct Phase
    {
        public string name;
        public float at, gap;
        public Phase(string name, float at, float gap) { this.name = name; this.at = at; this.gap = gap; }
    }

    static Phase P(string name, float at, float gap) => new Phase(name, at, gap);

    static readonly Phase[] Day1Was = { P("Calm", 0f, 26f), P("Build", .30f, 20f), P("Rush", .55f, 16f), P("Recover", .80f, 24f) };
    static readonly Phase[] Day1Now = { P("Calm", 0f, 75f), P("Build", .25f, 30f), P("Rush", .55f, 24f), P("Recover", .80f, 30f) };
    // 24 Oct 2026 (Days): Days 2 and 3 move to the pacing pass's rhythm (PacingSteps, P's §1.5); "was" is now the 14 Oct one.
    static readonly Phase[] Day2Was = Of(PacingSteps.Day2PhasesWas);
    static readonly Phase[] Day2Now = Of(PacingSteps.Day2Phases);
    static readonly Phase[] Day3Was = Of(PacingSteps.Day3PhasesWas);
    static readonly Phase[] Day3Now = Of(PacingSteps.Day3Phases);
    // The Producer's call 14: a stopgap so Day 4 isn't a cliff at 420 s. Days 4-5's gaps x 420/180 (to 0.1 s), patience x1.0.
    // Proper tuning waits for the scope freeze to lift.
    static readonly Phase[] Day4Was = { P("Calm", 0f, 18f), P("Build", .20f, 12f), P("Rush", .42f, 7f), P("Recover", .75f, 16f) };
    static readonly Phase[] Day5Was =
        { P("Opening", 0f, 16f), P("Morning rush", .15f, 7f), P("Lull", .38f, 26f), P("Lunch rush", .58f, 6f), P("Last orders", .82f, 15f) };
    static Phase[] Scaled(Phase[] was) =>
        was.Select(p => P(p.name, p.at, Mathf.Round(p.gap * DayLength / OldDayLength * 10f) / 10f)).ToArray();
    // 24 Oct 2026 (Days): the pacing pass's rows (PacingSteps) in this file's own shape.
    static Phase[] Of(PacingSteps.PhaseRow[] rows) => rows.Select(r => P(r.name, r.at, r.gap)).ToArray();
    static readonly Phase[] Day4Now = Of(PacingSteps.Day4Phases), Day5Now = Of(PacingSteps.Day5Phases);
    static (GameObject prefab, int fault)[] Entries((string prefab, int fault)[] list) =>
        list.Select(e => (AssetDatabase.LoadAssetAtPath<GameObject>(e.prefab), e.fault)).ToArray();
    static bool CallIsReal() =>
        AssetDatabase.LoadAssetAtPath<GameObject>(PacingSteps.CallPath) is GameObject call && SupportCallSteps.HasRealModel(call);

    // ---------- the menu ----------

    [MenuItem(Menu + "Days 1–3 - the intro level (data)", false, 0)]
    static void All() => Perform("Days 1-3 - the intro level (data): items 1-25 (specs/days-1-3.md §9) and 26-27 (call 14)",
        "days-1-3-all", TheDay(), TheCamera(), DayOne(), DayTwo(), DayThree(), DaysFourAndFive());

    [MenuItem(Menu + "A - The day: 420 s (scene)", false, 11)]
    static void StepA() => Perform("Days A - The day: 420 s (scene)", "days-a-the-day", TheDay());

    [MenuItem(Menu + "B - Grace's camera: the clean and the shutter (prefab)", false, 12)]
    static void StepB() => Perform("Days B - Grace's camera: the clean and the shutter (prefab)", "days-b-camera", TheCamera());

    [MenuItem(Menu + "C - Day 1: Grace's clean (data)", false, 13)]
    static void StepC() => Perform("Days C - Day 1: Grace's clean (data)", "days-c-day-1", DayOne());

    [MenuItem(Menu + "D - Day 2: Grace's shutter (data)", false, 14)]
    static void StepD() => Perform("Days D - Day 2: Grace's shutter (data)", "days-d-day-2", DayTwo());

    [MenuItem(Menu + "E - Day 3: the screwdriver (data)", false, 15)]
    static void StepE() => Perform("Days E - Day 3: the screwdriver (data)", "days-e-day-3", DayThree());

    [MenuItem(Menu + "F - Days 4-5: the 420 s stopgap (data)", false, 16)]
    static void StepF() => Perform("Days F - Days 4-5: the 420 s stopgap (data)", "days-f-days-4-5", DaysFourAndFive());

    [MenuItem(Menu + "Days 1–3 - the intro level (data)", true)]
    [MenuItem(Menu + "A - The day: 420 s (scene)", true)]
    [MenuItem(Menu + "B - Grace's camera: the clean and the shutter (prefab)", true)]
    [MenuItem(Menu + "C - Day 1: Grace's clean (data)", true)]
    [MenuItem(Menu + "D - Day 2: Grace's shutter (data)", true)]
    [MenuItem(Menu + "E - Day 3: the screwdriver (data)", true)]
    [MenuItem(Menu + "F - Days 4-5: the 420 s stopgap (data)", true)]
    [MenuItem(CheckMenu, true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    // ---------- the driver: survey → change (one undo group) → checks → save, or undo it all ----------

    sealed class Group
    {
        public string name;
        public bool scene;                       // needs the café scene open (and saves it)
        public Action<Run> survey, change, checks;
    }

    static void Perform(string title, string label, params Group[] groups)
    {
        var run = new Run(title, label);
        run.Text.AppendLine($"{title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        int undo = -1;
        bool began = false, needsScene = groups.Any(g => g.scene);
        Scene scene = default;
        try
        {
            run.Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first (Edit › Play Mode › Play).");
            if (needsScene) scene = Playtest4Shared.RequireCafeScene(run);   // open and saved: the step's changes are its own

            run.Section("Survey");
            foreach (Group g in groups) { run.Section("  " + g.name); g.survey?.Invoke(run); }

            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            began = true;

            run.Section("Change (old → new)");
            foreach (Group g in groups) { run.Section("  " + g.name); g.change?.Invoke(run); }

            run.Section("Checks");
            foreach (Group g in groups) { run.Section("  " + g.name); g.checks?.Invoke(run); }
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
            Debug.Log(Tag + $"{title}: {(run.Changes == 0 ? "already done" : "done")} ({run.Changes} change(s), {run.Passes} check(s) passed). Report: {Write(run)}");
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

    // ---------- A. the day: 420 s (item 1) ----------

    static DayClock clock;

    static Group TheDay() => new Group
    {
        name = "A. The scene: Assets/Playtests/AcesCafeLayoutPlaytest.unity",
        scene = true,
        survey = run =>
        {
            clock = Playtest4Shared.One<DayClock>(run);
            float now = run.Prop(new SerializedObject(clock), "dayLengthSeconds").floatValue;
            run.Info($"DayClock ({Playtest4Shared.PathOf(clock.gameObject)}): the day is {now:0} s.");
        },
        change = run => Float(run, clock, "dayLengthSeconds", OldDayLength, DayLength, "1. DayClock.dayLengthSeconds (the day, real seconds)"),
        checks = run =>
        {
            float now = new SerializedObject(clock).FindProperty("dayLengthSeconds").floatValue;
            run.Check(Near(now, DayLength), $"the day is {now:0} s (was {OldDayLength:0}): a first-timer's first repair is about a sixth of Day 1 (T11)");
        }
    };

    // ---------- B. Grace's camera: fault 0 the clean, fault 1 the shutter, and the resting shutter (items 2-4) ----------

    sealed class Contents { public GameObject root; public bool changed, released; }

    static Contents cameraContents;

    static Group TheCamera() => new Group
    {
        name = "B. " + CameraPath,
        survey = run =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
            run.Require(prefab != null, $"No prefab at {CameraPath} (Content › Grace showcase › Create prototype assets).");
            var def = prefab.GetComponent<DeviceDefinition>();
            var job = prefab.GetComponent<GraceCameraRepairJob>();
            run.Require(def != null && job != null && job.Shutter != null,
                "The camera has no DeviceDefinition, GraceCameraRepairJob or shutter part. Nothing changed; tell Claude.");
            int faults = def.faults != null ? def.faults.Length : 0;
            run.Require(faults <= 2, $"The camera has {faults} faults; this step knows two (the clean, the shutter). Nothing changed; tell Claude.");
            int spots = prefab.GetComponentsInChildren<GrimeSpot>(true).Length;
            run.Require(spots == 3, $"The camera has {spots} grime spots, not the lens, film path and film guide. Nothing changed; tell Claude.");
            // The resting shutter is a copy of the jammed assembly as it stands: made before the one-scale bake it would keep the
            // 2x meshes under names the bake doesn't map, and Devices 2 couldn't bake the camera (QA m20; the integration, 14 Oct).
            run.Require(def.realSize, "The camera isn't real size yet: run Fixit Fidget › Bench › Devices 2 first (after Devices 1), then this. " +
                                      "Nothing changed. (A, C, D, E and F can run on their own meanwhile.)");
            run.Info($"faults now: {string.Join(" | ", (def.faults ?? new DeviceFault[0]).Select(Describe))}");
        },
        change = run =>
        {
            var c = new Contents { root = PrefabUtility.LoadPrefabContents(CameraPath) };
            cameraContents = c;
            run.onFail.Add(() => Release(c, false, run));
            run.afterPass.Add(() => Release(c, true, run));
            EditCamera(run, c);
        },
        checks = run => CheckCamera(run, cameraContents.root)
    };

    static void Release(Contents c, bool save, Run run)
    {
        if (c == null || c.released || c.root == null) return;
        c.released = true;
        if (save && c.changed)
        {
            PrefabUtility.SaveAsPrefabAsset(c.root, CameraPath);
            run.Info("saved prefab " + CameraPath);
        }
        PrefabUtility.UnloadPrefabContents(c.root);
    }

    static void EditCamera(Run run, Contents c)
    {
        GameObject root = c.root;
        var def = root.GetComponent<DeviceDefinition>();
        var job = root.GetComponent<GraceCameraRepairJob>();
        run.Require(def != null && job != null && job.Shutter != null, "The camera's contents have no definition, job or shutter.");
        GameObject jammed = job.Shutter.gameObject;
        GameObject fresh = FreshVisualOf(job.Shutter);
        GameObject[] spots = root.GetComponentsInChildren<GrimeSpot>(true).Select(s => s.gameObject).ToArray();

        // 4. The resting shutter (§5.2): the camera on Day 1 has its shutter, it just isn't today's job. A copy of the jammed
        // assembly's meshes and materials at the same pose (the mechanism and its bent blade, never the working blade), with no
        // part and no collider; inactive in the prefab, turned on by fault 0. Re-copied whenever the jammed assembly changes
        // (Art's A7, the one-scale step), so it always looks like what comes out on Day 2.
        Transform resting = FindChild(jammed.transform.parent, RestingShutterName);
        string want = Signature(jammed.transform, fresh, false);
        string have = resting != null ? Signature(resting, null, true) : "";
        if (resting != null && have == want)
            run.Info($"4. {RestingShutterName}: already a copy of {jammed.name} ({CountVisuals(resting)} mesh(es), no part, no collider)");
        else
        {
            bool again = resting != null;
            if (again) Object.DestroyImmediate(resting.gameObject);
            resting = BuildResting(jammed.transform, fresh);
            c.changed = true;
            run.Changed($"4. {RestingShutterName}: {(again ? "re-copied (the jammed assembly changed)" : "made")}, a copy of {jammed.name}'s " +
                        $"{CountVisuals(resting)} mesh(es) at its pose, no part, no collider, inactive in the prefab (fault 0 turns it on)");
        }
        if (resting.gameObject.activeSelf)
        {
            resting.gameObject.SetActive(false);
            c.changed = true;
            run.Changed($"4. {RestingShutterName}: inactive in the prefab (the fault turns it on)");
        }

        // 2-3. The two faults (Systems' step 11 with Days' rows).
        GameObject[] cleanObjects = spots.Concat(new[] { resting.gameObject }).ToArray();
        GameObject[] shutterObjects = { jammed };
        DeviceFault[] was = def.faults ?? new DeviceFault[0];
        DeviceFault first = was.Length > 0 && was[0] != null ? was[0] : new DeviceFault();
        DeviceFault second = was.Length > 1 && was[1] != null ? was[1] : new DeviceFault();
        string firstWas = was.Length > 0 ? Describe(was[0]) : "(none)", secondWas = was.Length > 1 ? Describe(was[1]) : "(none)";
        bool firstRight = Is(first, FaultType.Cleaning, CleanLabel, CleanPayout, cleanObjects);
        bool secondRight = was.Length > 1 && Is(second, FaultType.Mechanical, ShutterLabel, ShutterPayout, shutterObjects);
        if (firstRight && secondRight && was.Length == 2)
        {
            run.Info($"2. faults[0]: already {firstWas}");
            run.Info($"3. faults[1]: already {secondWas}");
            return;
        }
        first.type = FaultType.Cleaning;
        first.description = CleanLabel;
        first.enableObjects = cleanObjects;
        first.payout = CleanPayout;
        second.type = FaultType.Mechanical;
        second.description = ShutterLabel;
        second.enableObjects = shutterObjects;
        second.payout = ShutterPayout;
        def.faults = new[] { first, second };
        EditorUtility.SetDirty(def);
        c.changed = true;
        run.Changed($"2. faults[0] (Day 1, the clean): {firstWas} → {Describe(first)}");
        run.Changed($"3. faults[1] (Day 2, the shutter): {secondWas} → {Describe(second)}");
    }

    static void CheckCamera(Run run, GameObject root)
    {
        if (!run.Check(root != null, "the camera's contents are loaded")) return;
        var def = root.GetComponent<DeviceDefinition>();
        var job = root.GetComponent<GraceCameraRepairJob>();
        GameObject jammed = job != null && job.Shutter != null ? job.Shutter.gameObject : null;
        Transform resting = jammed != null ? FindChild(jammed.transform.parent, RestingShutterName) : null;
        GameObject[] spots = root.GetComponentsInChildren<GrimeSpot>(true).Select(s => s.gameObject).ToArray();
        run.Check(def != null && def.storyEpisodeId == GraceCameraEpisode.EpisodeId, $"the camera keeps its episode id ({GraceCameraEpisode.EpisodeId})");
        run.Check(def != null && def.faults != null && def.faults.Length == 2, "the camera has two faults: the clean and the shutter");
        if (def == null || def.faults == null || def.faults.Length != 2 || jammed == null) return;
        run.Check(resting != null && Is(def.faults[0], FaultType.Cleaning, CleanLabel, CleanPayout, spots.Concat(new[] { resting.gameObject }).ToArray()),
            $"fault 0 is the clean: Cleaning, \"{CleanLabel}\", ${CleanPayout}, the three spots and the resting shutter, not the part ({Describe(def.faults[0])})");
        run.Check(Is(def.faults[1], FaultType.Mechanical, ShutterLabel, ShutterPayout, new[] { jammed }),
            $"fault 1 is the shutter: Mechanical, \"{ShutterLabel}\", ${ShutterPayout}, the jammed part only ({Describe(def.faults[1])})");
        if (resting == null) { run.Check(false, $"{RestingShutterName} exists"); return; }
        run.Check(!resting.gameObject.activeSelf, "the resting shutter is inactive in the prefab (fault 0 turns it on)");
        run.Check(resting.GetComponentsInChildren<Collider>(true).Length == 0 && resting.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
            "the resting shutter has no collider and no part: nothing to hover, glow or grab (the step's targets never include it)");
        run.Check(CountVisuals(resting) > 0 && Signature(resting, null, true) == Signature(jammed.transform, FreshVisualOf(job.Shutter), false),
            $"it is the jammed assembly's {CountVisuals(resting)} mesh(es) and materials at the same pose, without the working blade");
        DeviceWake wake = root.GetComponent<DeviceWake>();
        run.Check(wake != null && wake.What == DeviceWake.Kind.ShutterFires,
            "the camera still wakes by its shutter (DeviceWake decides when it fires: Day 2 only, R4)");
    }

    static bool Is(DeviceFault f, FaultType type, string label, int payout, GameObject[] objects) =>
        f != null && f.type == type && f.description == label && f.payout == payout
        && f.enableObjects != null && f.enableObjects.Length == objects.Length && f.enableObjects.SequenceEqual(objects);

    static string Describe(DeviceFault f) => f == null ? "(none)"
        : $"{f.type} \"{f.description}\" ${f.payout} [{string.Join(", ", (f.enableObjects ?? new GameObject[0]).Select(g => g != null ? g.name : "(missing)"))}]";

    // The working blade (the swap's fresh part): never part of the resting look. Its field is private on the part.
    static GameObject FreshVisualOf(ReplaceablePart part)
    {
        if (part == null) return null;
        SerializedProperty p = new SerializedObject(part).FindProperty("freshVisual");
        if (p != null && p.objectReferenceValue is GameObject g) return g;
        Transform named = FindChild(part.transform, "Working shutter blade");
        return named != null ? named.gameObject : null;
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent) if (child.name == name) return child;
        return null;
    }

    static Transform BuildResting(Transform jammed, GameObject fresh)
    {
        var copy = new GameObject(RestingShutterName) { layer = jammed.gameObject.layer };
        copy.transform.SetParent(jammed.parent, false);
        copy.transform.localPosition = jammed.localPosition;
        copy.transform.localRotation = jammed.localRotation;
        copy.transform.localScale = jammed.localScale;
        CopyVisual(jammed, copy.transform);
        foreach (Transform child in jammed) CopyNode(child, copy.transform, fresh);
        copy.transform.SetSiblingIndex(jammed.GetSiblingIndex() + 1);
        return copy.transform;
    }

    static void CopyNode(Transform source, Transform parent, GameObject fresh)
    {
        if (!Shows(source, fresh)) return;
        var node = new GameObject(source.name + RestingSuffix) { layer = source.gameObject.layer };
        node.transform.SetParent(parent, false);
        node.transform.localPosition = source.localPosition;
        node.transform.localRotation = source.localRotation;
        node.transform.localScale = source.localScale;
        CopyVisual(source, node.transform);
        foreach (Transform child in source) CopyNode(child, node.transform, fresh);
    }

    // A child is part of the resting look when it is switched on and isn't the working blade (or under it).
    static bool Shows(Transform t, GameObject fresh) =>
        t.gameObject.activeSelf && (fresh == null || (t.gameObject != fresh && !t.IsChildOf(fresh.transform)));

    static void CopyVisual(Transform source, Transform target)
    {
        var filter = source.GetComponent<MeshFilter>();
        var renderer = source.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null || filter.sharedMesh == null) return;
        target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var copy = target.gameObject.AddComponent<MeshRenderer>();
        copy.sharedMaterials = renderer.sharedMaterials;
        copy.shadowCastingMode = renderer.shadowCastingMode;
        copy.receiveShadows = renderer.receiveShadows;
    }

    static int CountVisuals(Transform t) => t == null ? 0 : t.GetComponentsInChildren<MeshRenderer>(true).Length;

    // What a subtree looks like: each shown node's place, mesh, materials and pose (the resting copy's names lose " (resting)").
    static string Signature(Transform top, GameObject fresh, bool isCopy)
    {
        var lines = new List<string> { "parent " + (top.parent != null ? top.parent.name : "-") };
        void Walk(Transform t, string path)
        {
            var filter = t.GetComponent<MeshFilter>();
            var renderer = t.GetComponent<MeshRenderer>();
            bool visual = filter != null && filter.sharedMesh != null && renderer != null;   // what CopyVisual copies
            string mesh = visual ? AssetDatabase.GetAssetPath(filter.sharedMesh) + ":" + filter.sharedMesh.name : "-";
            string mats = visual ? string.Join(",", renderer.sharedMaterials.Select(m => m != null ? AssetDatabase.GetAssetPath(m) + ":" + m.name : "-")) : "-";
            lines.Add($"{path} {mesh} {mats} {t.localPosition.ToString("F5")} {t.localRotation.ToString("F5")} {t.localScale.ToString("F5")}");
            foreach (Transform child in t)
            {
                if (!isCopy && !Shows(child, fresh)) continue;
                string name = isCopy && child.name.EndsWith(RestingSuffix, StringComparison.Ordinal)
                    ? child.name.Substring(0, child.name.Length - RestingSuffix.Length) : child.name;
                Walk(child, path + "/" + name);
            }
        }
        Walk(top, ".");
        return string.Join("\n", lines);
    }

    // ---------- C. Day 1 (items 5-11) ----------

    static DayDefinition day1, day2, day3, day4, day5;
    static GameObject cameraPrefab, phonePrefab, watchPrefab;
    static CustomerProfile grace, officer;

    static void LoadShared(Run run)
    {
        cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
        phonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
        watchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
        grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath);
        officer = AssetDatabase.LoadAssetAtPath<CustomerProfile>(OfficerPath);
        run.Require(cameraPrefab != null && phonePrefab != null && watchPrefab != null,
            $"The device prefabs are at {CameraPath}, {PhonePath} and {WatchPath}.");
        run.Require(grace != null && officer != null, $"Grace and the Officer are at {GracePath} and {OfficerPath}.");
    }

    static DayDefinition LoadDay(Run run, string path, int number)
    {
        var day = AssetDatabase.LoadAssetAtPath<DayDefinition>(path);
        run.Require(day != null, $"No day asset at {path}.");
        run.Require(day.dayNumber == number, $"{path} is day {day.dayNumber}, not Day {number}. Nothing changed; tell Claude.");
        run.Prop(new SerializedObject(day), "walkInRepairs");   // refuses until the new DayDefinition has compiled
        return day;
    }

    static Group DayOne() => new Group
    {
        name = "C. " + Day1Path,
        survey = run =>
        {
            LoadShared(run);
            day1 = LoadDay(run, Day1Path, 1);
            run.Require(day1.storyVisits != null && day1.storyVisits.Length > 0 && day1.storyVisits[0] != null && day1.storyVisits[0].who == officer,
                "Day 1's first story visit is not the Officer. Nothing changed; tell Claude.");
            run.Require(day1.featuredRegular == grace, "Day 1's featured regular is not Grace. Nothing changed; tell Claude.");
            run.Info($"Day 1 now: {Summary(day1)}");
        },
        change = run =>
        {
            SetPhases(run, day1, Day1Was, Day1Now, "5", false);
            Float(run, day1, "drinkOnlyChance", PacingSteps.Day1DrinkOnlyWas, PacingSteps.Day1DrinkOnly, "6. drinkOnlyChance (the walk-ins who come for a drink; 24 Oct: .55, the pacing pass)");
            Bool(run, day1, "suppressRepairDrinkWishes", true, "7. suppressRepairDrinkWishes (call 7: no latte for Grace on Day 1, the lock's words)");
            Float(run, day1, "storyVisits.Array.data[0].arrivesAt", .35f, .25f, "8. storyVisits[0].arrivesAt (the Officer's first coffee, ≈ 1:52)");
            run.Info($"9. featuredRepair: unchanged ({FeaturedWords(day1)}): fault 0 is now the clean");
            SetWalkIns(run, day1, new[] { (phonePrefab, PhoneGunked) }, "10");
            Text(run, day1, "intent", Day1Intent, "11. intent");
        },
        checks = run =>
        {
            CheckPhases(run, day1, Day1Now);
            run.Check(Near(day1.drinkOnlyChance, PacingSteps.Day1DrinkOnly), $"Day 1: {day1.drinkOnlyChance:0.##} of walk-ins come for a drink ({PacingSteps.Day1DrinkOnly:0.##}, the pacing pass)");
            run.Check(day1.SuppressesRepairDrinksOn(1) && !day1.SuppressesRepairDrinksOn(2), "Day 1: no drink wishes from repair customers (Grace's latte waits for Day 2), on Day 1 only");
            run.Check(Near(day1.storyVisits[0].arrivesAt, .25f), $"Day 1: the Officer at {day1.storyVisits[0].arrivesAt:0.##} of the day (.25)");
            run.Check(day1.useFeaturedRepair && day1.featuredRepair != null && day1.featuredRepair.devicePrefab == cameraPrefab
                      && day1.featuredRepair.faultIndex == 0 && day1.featuredRepair.storyEpisodeId == GraceCameraEpisode.EpisodeId
                      && Near(day1.featuredRegularArrivesAt, 0f) && day1.GuidesFeaturedVisitOn(1),
                $"Day 1: Grace first, with her camera's fault 0 (the clean), and her visit's hints ({FeaturedWords(day1)})");
            CheckWalkIns(run, day1, new[] { (phonePrefab, PhoneGunked) });
            run.Check(Near(day1.patienceMultiplier, 2f) && day1.maxCustomers == 2, $"Day 1: patience x{day1.patienceMultiplier:0.##}, two walk-ins at once (unchanged)");
        }
    };

    // ---------- D. Day 2 (items 12-17) ----------

    static Group DayTwo() => new Group
    {
        name = "D. " + Day2Path,
        survey = run =>
        {
            LoadShared(run);
            day2 = LoadDay(run, Day2Path, 2);
            run.Require(day2.featuredRegular == grace, "Day 2's featured regular is not Grace. Nothing changed; tell Claude.");
            run.Info($"Day 2 now: {Summary(day2)}");
        },
        change = run =>
        {
            Bool(run, day2, "useFeaturedRepair", true, "12. useFeaturedRepair (Grace brings the camera back: the shutter)");
            ObjectRef(run, day2, "featuredRepair.devicePrefab", cameraPrefab, "12. featuredRepair.devicePrefab");
            Int(run, day2, "featuredRepair.faultIndex", 1, "12. featuredRepair.faultIndex (fault 1, the shutter)");
            Text(run, day2, "featuredRepair.storyEpisodeId", GraceCameraEpisode.EpisodeId, "12. featuredRepair.storyEpisodeId");
            Float(run, day2, "patienceMultiplier", PacingSteps.Day2PatienceWas, PacingSteps.Day2Patience, "13. patienceMultiplier (no regular leaves \"Never again\" on Days 1-3; 24 Oct: x1.8, the pacing pass)");
            SetPhases(run, day2, Day2Was, Day2Now, "14", false);
            Float(run, day2, "drinkOnlyChance", PacingSteps.Day2DrinkOnlyWas, PacingSteps.Day2DrinkOnly, "15. drinkOnlyChance (24 Oct: .45)");
            SetWalkIns(run, day2, Entries(PacingSteps.Day2WalkIns), "16");
            Text(run, day2, "intent", Day2Intent, "17. intent");
        },
        checks = run =>
        {
            run.Check(day2.useFeaturedRepair && day2.featuredRepair != null && day2.featuredRepair.devicePrefab == cameraPrefab
                      && day2.featuredRepair.faultIndex == 1 && day2.featuredRepair.storyEpisodeId == GraceCameraEpisode.EpisodeId
                      && Near(day2.featuredRegularArrivesAt, 0f) && day2.GuidesFeaturedVisitOn(2),
                $"Day 2: Grace first, with her camera's fault 1 (the shutter) and her visit's hints ({FeaturedWords(day2)})");
            run.Check(!day2.SuppressesRepairDrinksOn(2), "Day 2: drink wishes on (her latte is the juggle)");
            run.Check(Near(day2.patienceMultiplier, PacingSteps.Day2Patience), $"Day 2: patience x{day2.patienceMultiplier:0.##} ({PacingSteps.Day2Patience:0.#})");
            CheckPhases(run, day2, Day2Now);
            run.Check(Near(day2.drinkOnlyChance, PacingSteps.Day2DrinkOnly), $"Day 2: {day2.drinkOnlyChance:0.##} of walk-ins come for a drink ({PacingSteps.Day2DrinkOnly:0.##})");
            CheckWalkIns(run, day2, Entries(PacingSteps.Day2WalkIns));
        }
    };

    // ---------- E. Day 3 (items 18-25) ----------

    static Group DayThree() => new Group
    {
        name = "E. " + Day3Path,
        survey = run =>
        {
            LoadShared(run);
            day3 = LoadDay(run, Day3Path, 3);
            run.Info($"Day 3 now: {Summary(day3)}");
        },
        change = run =>
        {
            ObjectRef(run, day3, "featuredRegular", grace, "18. featuredRegular (Grace: her latte and the photo)");
            Float(run, day3, "featuredRegularArrivesAt", .15f, 0f, "18. featuredRegularArrivesAt (first thing)");
            Bool(run, day3, "showFeaturedVisitHints", false, "18. showFeaturedVisitHints (stays off)");
            Bool(run, day3, "useFeaturedRepair", false, "18. useFeaturedRepair (stays off: she comes for her latte)");
            Float(run, day3, "patienceMultiplier", 1f, 1.6f, "19. patienceMultiplier");
            Int(run, day3, "maxCustomers", 2, "20. maxCustomers (walk-ins at once; was 5)");
            SetPhases(run, day3, Day3Was, Day3Now, "21", false);
            Float(run, day3, "drinkOnlyChance", PacingSteps.Day3DrinkOnlyWas, PacingSteps.Day3DrinkOnly, "22. drinkOnlyChance (24 Oct: .35)");
            SetDevices(run, day3, new[] { watchPrefab }, "23. devices (read only by the fallback)");
            SetWalkIns(run, day3, Entries(PacingSteps.Day3WalkIns(CallIsReal())), "24");
            Text(run, day3, "intent", Day3Intent, "25. intent");
        },
        checks = run =>
        {
            run.Check(day3.featuredRegular == grace && Near(day3.featuredRegularArrivesAt, 0f) && !day3.showFeaturedVisitHints && !day3.useFeaturedRepair,
                "Day 3: Grace first, no repair, no hints (her latte, then the photo's news)");
            run.Check(!day3.SuppressesRepairDrinksOn(3), "Day 3: drink wishes on (DayOneOnboardingChecks requires it)");
            run.Check(Near(day3.patienceMultiplier, 1.6f) && day3.maxCustomers == 2, $"Day 3: patience x{day3.patienceMultiplier:0.##} (1.6), {day3.maxCustomers} walk-ins at once (2)");
            CheckPhases(run, day3, Day3Now);
            run.Check(Near(day3.drinkOnlyChance, PacingSteps.Day3DrinkOnly), $"Day 3: {day3.drinkOnlyChance:0.##} of walk-ins come for a drink ({PacingSteps.Day3DrinkOnly:0.##})");
            run.Check(day3.devices != null && day3.devices.Length == 1 && day3.devices[0] == watchPrefab, "Day 3: the fallback's devices are the watch alone");
            CheckWalkIns(run, day3, Entries(PacingSteps.Day3WalkIns(CallIsReal())));
            run.Check(day3.StoryVisitsOn(3).Any(v => v != null && v.who == officer && v.arrivesAt <= .3f), "Day 3: the Officer's question still comes (story visit, at .25)");
        }
    };

    // ---------- F. Days 4-5: the stopgap (items 26-27; the Producer's call 14) ----------

    static Group DaysFourAndFive() => new Group
    {
        name = "F. " + Day4Path + ", " + Day5Path + " (24 Oct 2026: the pacing pass's ramp, was the stopgap: gaps x 420/180, patience x1.0)",
        survey = run =>
        {
            day4 = LoadDay(run, Day4Path, 4);
            day5 = LoadDay(run, Day5Path, 5);
            run.Info($"Day 4 now: {Summary(day4)}");
            run.Info($"Day 5 now: {Summary(day5)}");
        },
        change = run =>
        {
            // 24 Oct 2026 (Days): the pacing pass's ramp (P's §1.5), from the stopgap (or the 180 s day's numbers: a NOTE).
            SetPhases(run, day4, Scaled(Day4Was), Day4Now, "26", false);
            Float(run, day4, "patienceMultiplier", PacingSteps.Day4PatienceWas, PacingSteps.Day4Patience, "26. Day 4 patienceMultiplier");
            Int(run, day4, "maxCustomers", PacingSteps.Day4AtOnce, "26. Day 4 maxCustomers (was 6)");
            SetPhases(run, day5, Scaled(Day5Was), Day5Now, "27", false);
            Float(run, day5, "patienceMultiplier", PacingSteps.Day5PatienceWas, PacingSteps.Day5Patience, "27. Day 5 patienceMultiplier");
            Int(run, day5, "maxCustomers", PacingSteps.Day5AtOnce, "27. Day 5 maxCustomers (was 6)");
        },
        checks = run =>
        {
            CheckPhases(run, day4, Day4Now);
            CheckPhases(run, day5, Day5Now);
            run.Check(Near(day4.patienceMultiplier, PacingSteps.Day4Patience) && Near(day5.patienceMultiplier, PacingSteps.Day5Patience)
                      && day4.maxCustomers == PacingSteps.Day4AtOnce && day5.maxCustomers == PacingSteps.Day5AtOnce,
                $"Days 4-5: patience x{day4.patienceMultiplier:0.##} and x{day5.patienceMultiplier:0.##}, {day4.maxCustomers} and {day5.maxCustomers} at once " +
                $"(x{PacingSteps.Day4Patience:0.##} and x{PacingSteps.Day5Patience:0.#}, three at once: the pacing pass; were x1.0 and six)");
        }
    };

    // ---------- the setters: record, change, report old → new, save the asset once the step passes ----------

    static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-3f;

    static void SaveIfAsset(Run run, Object target)
    {
        if (target != null && AssetDatabase.Contains(target)) run.SaveAsset(target);
    }

    static void Float(Run run, Object target, string path, float was, float now, string item)
    {
        float current = run.Prop(new SerializedObject(target), path).floatValue;
        if (!Near(current, now) && !Near(current, was))
            run.Info($"{item}: NOTE it was {current:0.###}, not the expected {was:0.###} (tuned by hand?); set anyway");
        run.SetFloat(target, path, now, item);
        if (!Near(current, now)) SaveIfAsset(run, target);
    }

    static void Int(Run run, Object target, string path, int now, string item)
    {
        SerializedProperty p = run.Prop(new SerializedObject(target), path);
        int current = p.propertyType == SerializedPropertyType.Enum ? p.enumValueIndex : p.intValue;
        run.SetInt(target, path, now, item);
        if (current != now) SaveIfAsset(run, target);
    }

    static void Bool(Run run, Object target, string path, bool now, string item)
    {
        bool current = run.Prop(new SerializedObject(target), path).boolValue;
        run.SetBool(target, path, now, item);
        if (current != now) SaveIfAsset(run, target);
    }

    static void Text(Run run, Object target, string path, string now, string item)
    {
        string current = run.Prop(new SerializedObject(target), path).stringValue;
        run.SetString(target, path, now, item);
        if (current != now) SaveIfAsset(run, target);
    }

    static void ObjectRef(Run run, Object target, string path, Object now, string item)
    {
        Object current = run.Prop(new SerializedObject(target), path).objectReferenceValue;
        run.SetObject(target, path, now, item);
        if (current != now) SaveIfAsset(run, target);
    }

    static Phase[] Read(DayDefinition day) =>
        day.phases == null ? new Phase[0] : day.phases.Where(p => p != null).Select(p => P(p.phaseName, p.startsAt, p.spawnInterval)).ToArray();

    static bool Same(Phase[] a, Phase[] b) =>
        a.Length == b.Length && a.Zip(b, (x, y) => x.name == y.name && Near(x.at, y.at) && Near(x.gap, y.gap)).All(ok => ok);

    static string Words(Phase[] phases) => string.Join(" · ", phases.Select(p => $"{p.name} {p.at:0.##}: {p.gap:0.#}"));

    static void SetPhases(Run run, DayDefinition day, Phase[] was, Phase[] now, string item, bool onlyFromWas)
    {
        Phase[] current = Read(day);
        string label = $"{item}. {day.name}: phases (start: gap, s)";
        if (Same(current, now)) { run.Info($"{label}: already {Words(now)}"); return; }
        if (!Same(current, was))
        {
            if (onlyFromWas)
            {
                run.Info($"{label}: NOTE left alone: {Words(current)} is neither the 180 s day's {Words(was)} nor the stopgap's (tuned by hand? tell Claude)");
                return;
            }
            run.Info($"{label}: NOTE it was {Words(current)}, not the expected {Words(was)} (tuned by hand?); set anyway");
        }
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
        run.Changed($"{label}: {Words(current)} → {Words(now)}");
        run.SaveAsset(day);
    }

    static void CheckPhases(Run run, DayDefinition day, Phase[] now)
    {
        Phase[] current = Read(day);
        bool ascending = true;
        for (int i = 1; i < current.Length; i++) ascending &= current[i].at >= current[i - 1].at;
        run.Check(Same(current, now) && ascending, $"{day.name}: phases {Words(current)} (start: gap in seconds, ascending)");
    }

    static void SetDevices(Run run, DayDefinition day, GameObject[] now, string item)
    {
        GameObject[] current = day.devices ?? new GameObject[0];
        string Names(GameObject[] list) => "[" + string.Join(", ", list.Select(g => g != null ? g.name : "(none)")) + "]";
        if (current.SequenceEqual(now)) { run.Info($"{item}: already {Names(now)}"); return; }
        var so = new SerializedObject(day);
        SerializedProperty devices = run.Prop(so, "devices");
        run.Record(day);
        devices.arraySize = now.Length;
        for (int i = 0; i < now.Length; i++) devices.GetArrayElementAtIndex(i).objectReferenceValue = now[i];
        so.ApplyModifiedProperties();
        run.Changed($"{item}: {Names(current)} → {Names(now)}");
        run.SaveAsset(day);
    }

    static string EntryWords(GameObject prefab, int fault)
    {
        DeviceDefinition def = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
        string label = def != null && def.faults != null && fault >= 0 && fault < def.faults.Length && def.faults[fault] != null
            ? def.faults[fault].description : "?";
        return $"{(prefab != null ? prefab.name : "(none)")} · {fault} \"{label}\"";
    }

    static (GameObject prefab, int fault)[] ReadWalkIns(DayDefinition day) =>
        (day.walkInRepairs ?? new FeaturedRepairRequest[0]).Select(r => r != null ? (r.devicePrefab, r.faultIndex) : ((GameObject)null, -1)).ToArray();

    static string WalkInWords((GameObject prefab, int fault)[] list) =>
        list.Length == 0 ? "[] (the old roll)" : "[" + string.Join(", ", list.Select(e => EntryWords(e.prefab, e.fault))) + "]";

    static void SetWalkIns(Run run, DayDefinition day, (GameObject prefab, int fault)[] now, string item)
    {
        var current = ReadWalkIns(day);
        bool emptyIds = (day.walkInRepairs ?? new FeaturedRepairRequest[0]).All(r => r == null || string.IsNullOrEmpty(r.storyEpisodeId));
        string label = $"{item}. {day.name}: walkInRepairs (in order; call 8)";
        if (current.SequenceEqual(now) && emptyIds) { run.Info($"{label}: already {WalkInWords(now)}"); return; }
        var so = new SerializedObject(day);
        SerializedProperty list = run.Prop(so, "walkInRepairs");
        run.Record(day);
        list.arraySize = now.Length;
        for (int i = 0; i < now.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("devicePrefab").objectReferenceValue = now[i].prefab;
            e.FindPropertyRelative("faultIndex").intValue = now[i].fault;
            e.FindPropertyRelative("storyEpisodeId").stringValue = "";
        }
        so.ApplyModifiedProperties();
        run.Changed($"{label}: {WalkInWords(current)} → {WalkInWords(now)}");
        run.SaveAsset(day);
    }

    static void CheckWalkIns(Run run, DayDefinition day, (GameObject prefab, int fault)[] now)
    {
        var current = ReadWalkIns(day);
        bool emptyIds = (day.walkInRepairs ?? new FeaturedRepairRequest[0]).All(r => r != null && string.IsNullOrEmpty(r.storyEpisodeId));
        run.Check(current.SequenceEqual(now) && emptyIds && day.WalkInRepairsOn(day.dayNumber).Length == now.Length
                  && day.WalkInRepairsOn(day.dayNumber + 1).Length == 0,
            $"{day.name}: the walk-ins' repairs, in order, on its own day only: {WalkInWords(current)}");
    }

    static string FeaturedWords(DayDefinition day) => day.featuredRepair == null || day.featuredRepair.devicePrefab == null ? "none"
        : $"{EntryWords(day.featuredRepair.devicePrefab, day.featuredRepair.faultIndex)}, episode \"{day.featuredRepair.storyEpisodeId}\"" +
          (day.useFeaturedRepair ? "" : " (off)");

    static string Summary(DayDefinition day) =>
        $"phases {Words(Read(day))}; patience x{day.patienceMultiplier:0.##}; at once {day.maxCustomers}; drink-only {day.drinkOnlyChance:0.##}; " +
        $"suppress wishes {day.suppressRepairDrinkWishes}; featured {(day.featuredRegular != null ? day.featuredRegular.name : "none")} at {day.featuredRegularArrivesAt:0.##}, " +
        $"repair {FeaturedWords(day)}; story visits {string.Join(", ", (day.storyVisits ?? new StoryVisit[0]).Where(v => v != null).Select(v => (v.who != null ? v.who.name : "?") + " at " + v.arrivesAt.ToString("0.##")))}; " +
        $"walk-ins {WalkInWords(ReadWalkIns(day))}; devices [{string.Join(", ", (day.devices ?? new GameObject[0]).Select(g => g != null ? g.name : "-"))}]";

    // =================================================================================================================
    // Fixit Fidget > Checks > Days 1–3 (read-only; specs/days-1-3.md §10). Open the café scene first.
    // =================================================================================================================

    sealed class Lint
    {
        public readonly StringBuilder text = new StringBuilder();
        public int passes, failures;
        public bool Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            text.AppendLine((ok ? "  PASS   " : "  FAIL   ") + what);
            return ok;
        }
        public void Note(string line) => text.AppendLine("  " + line);
        public void Section(string name) => text.AppendLine().AppendLine(name);
    }

    // The verbs each day has taught by the time it plays (one new job type a day, §6): Day 1 cleaning (cloth, brush), Day 2
    // the swap (tweezers), Day 3 opening up (screwdriver, pry). The hand (a cover back on, a fresh part placed) is free.
    // 24 Oct 2026 (Days, the pacing pass §6.1): Day 2's walk-in is the watch's grit, four screws and a back to pry, so Day 2
    // teaches the screwdriver and the pry too; Day 4 is the first day past the intro (anything the bench has).
    static readonly ToolType[][] Taught =
    {
        new ToolType[0],
        new[] { ToolType.Cloth, ToolType.Brush },
        new[] { ToolType.Cloth, ToolType.Brush, ToolType.Tweezers, ToolType.Screwdriver, ToolType.Pry },
        new[] { ToolType.Cloth, ToolType.Brush, ToolType.Tweezers, ToolType.Screwdriver, ToolType.Pry },
        new[] { ToolType.Cloth, ToolType.Brush, ToolType.Tweezers, ToolType.Screwdriver, ToolType.Pry },
    };

    // What isn't bench work, by the first day it may come (24 Oct 2026: the support call on Day 3, his answer p4; the circuit
    // and the counter fault on Day 4).
    static int FirstDayOf(string other) => other == "a support call" ? 3 : 4;

    [MenuItem(CheckMenu, false, 60)]
    public static void RunCheck()
    {
        var lint = new Lint();
        lint.text.AppendLine($"Days 1–3 - check   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})   read-only: nothing in the scene, the assets or any save changes");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            CheckSchedule(lint, out DayDefinition[] days, out DrinkDefinition[] drinks);
            CheckCameraFaults(lint);
            for (int d = 1; d <= 3; d++)
                if (days[d] != null) CheckDay(lint, days[d], d);
            if (drinks != null && drinks.Length > 0)
                for (int d = 1; d <= 3; d++)
                    if (days[d] != null) CheckWalkInRule(lint, days[d], drinks);
            CheckWords(lint);
            CheckStopgap(lint);
            CheckFinds(lint, days);
        }
        catch (Exception e)
        {
            lint.Check(false, "the check stopped: " + e.Message);
            Debug.LogException(e);
        }
        lint.text.AppendLine().AppendLine(lint.failures == 0 ? $"PASS: {lint.passes} check(s)." : $"FAIL: {lint.failures} of {lint.passes + lint.failures} check(s) failed.");
        Directory.CreateDirectory(LogRoot);
        string path = Path.Combine(LogRoot, $"check-days-1-3-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(path, lint.text.ToString());
        File.WriteAllText(Path.Combine(LogRoot, "check-days-1-3-last.txt"), lint.text.ToString());
        string line = Tag + $"Days 1–3 check: {(lint.failures == 0 ? "PASS" : "FAIL")} ({lint.passes} passed, {lint.failures} failed). Report: {path}";
        if (lint.failures == 0) Debug.Log(line); else Debug.LogError(line);
    }

    // The schedule plays GraceCamera, GraceReturn and TheRealGame as Days 1-3, and the clock is 420 s.
    static void CheckSchedule(Lint lint, out DayDefinition[] days, out DrinkDefinition[] drinks)
    {
        days = new DayDefinition[4];
        drinks = null;
        lint.Section("The scene: the schedule and the clock");
        if (!lint.Check(SceneManager.GetActiveScene().path == Playtest4Shared.ScenePath, $"the café scene is open ({Playtest4Shared.ScenePath})"))
        {
            days[1] = AssetDatabase.LoadAssetAtPath<DayDefinition>(Day1Path);
            days[2] = AssetDatabase.LoadAssetAtPath<DayDefinition>(Day2Path);
            days[3] = AssetDatabase.LoadAssetAtPath<DayDefinition>(Day3Path);
            lint.Note("(the days below are read from their assets; the schedule, the clock and the walk-in rule need the scene)");
            return;
        }
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        DayClock dayClock = Object.FindAnyObjectByType<DayClock>(FindObjectsInactive.Include);
        if (!lint.Check(spawner != null && dayClock != null, "the scene has its CustomerSpawner and DayClock")) return;
        var so = new SerializedObject(spawner);
        SerializedProperty schedule = so.FindProperty("schedule");
        string[] want = { Day1Path, Day2Path, Day3Path };
        for (int i = 0; i < 3; i++)
        {
            var day = schedule != null && schedule.arraySize > i ? schedule.GetArrayElementAtIndex(i).objectReferenceValue as DayDefinition : null;
            days[i + 1] = day;
            lint.Check(day != null && AssetDatabase.GetAssetPath(day) == want[i] && day.dayNumber == i + 1,
                $"Day {i + 1} plays {want[i]} ({(day != null ? AssetDatabase.GetAssetPath(day) + ", day " + day.dayNumber : "nothing")})");
        }
        SerializedProperty drinkList = so.FindProperty("drinks");
        var found = new List<DrinkDefinition>();
        for (int i = 0; drinkList != null && i < drinkList.arraySize; i++)
            if (drinkList.GetArrayElementAtIndex(i).objectReferenceValue is DrinkDefinition drink) found.Add(drink);
        drinks = found.ToArray();
        float length = new SerializedObject(dayClock).FindProperty("dayLengthSeconds").floatValue;
        lint.Check(Near(length, DayLength), $"the day is {length:0} s ({DayLength:0}; was {OldDayLength:0}: Fixit Fidget › Days › A, then save the scene)");
    }

    // The camera's faults: fault 0 turns on the three spots and the resting shutter, and not the part; fault 1 the part only;
    // both make the episode's job. R1 (the grade), R2 (the visit) and R4 (the wake) on real instances in a preview scene.
    static void CheckCameraFaults(Lint lint)
    {
        lint.Section("Grace's camera: two faults (" + CameraPath + ")");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
        if (!lint.Check(prefab != null, "the camera prefab exists")) return;
        var def = prefab.GetComponent<DeviceDefinition>();
        if (!lint.Check(def != null && def.faults != null && def.faults.Length == 2, $"it has two faults ({(def != null && def.faults != null ? def.faults.Length : 0)}; Fixit Fidget › Days › B)")) return;

        for (int f = 0; f < 2; f++)
        {
            var request = new FeaturedRepairRequest { devicePrefab = prefab, faultIndex = f, storyEpisodeId = GraceCameraEpisode.EpisodeId };
            bool made = request.TryCreateJob(out Job job, out string error);
            GraceCameraVisit visit = made ? GraceCameraEpisode.VisitFor(job.faultType == FaultType.Cleaning) : GraceCameraVisit.None;
            lint.Check(made && job.storyEpisodeId == GraceCameraEpisode.EpisodeId && visit == (f == 0 ? GraceCameraVisit.Clean : GraceCameraVisit.Shutter)
                       && job.payout == (f == 0 ? CleanPayout : ShutterPayout),
                $"fault {f} makes her episode's job ({(made ? $"{job.faultType} \"{job.faultDescription}\" ${job.payout}: the {visit} visit" : error)})");
        }

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            // Fault 0: Day 1's clean.
            GameObject clean = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(clean, preview);
            clean.GetComponent<DeviceDefinition>().ApplyFault(0);
            var job0 = clean.GetComponent<GraceCameraRepairJob>();
            GrimeSpot[] spots = clean.GetComponentsInChildren<GrimeSpot>();
            Transform resting = job0 != null && job0.Shutter != null ? FindChild(job0.Shutter.transform.parent, RestingShutterName) : null;
            lint.Check(spots.Length == 3, $"fault 0 turns on the three spots ({spots.Length}: {string.Join(", ", spots.Select(s => s.name))})");
            lint.Check(resting != null && resting.gameObject.activeSelf, "fault 0 turns on the resting shutter (the camera has its shutter on Day 1)");
            lint.Check(job0 != null && job0.Shutter != null && !job0.Shutter.gameObject.activeSelf && !job0.ShutterIsTask,
                "fault 0 leaves the jammed part off: no glow, no hover, not a task");
            lint.Check(resting != null && resting.GetComponentsInChildren<Collider>(true).Length == 0 && resting.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "the resting shutter has no collider and no part");
            DeviceWake wake0 = clean.GetComponent<DeviceWake>();
            lint.Check(wake0 != null && !wake0.ShutterReady, "R4: Day 1's camera winds without firing (no working blade in it)");
            if (job0 != null)
            {
                lint.Check(job0.Grade == JobGrade.Rejected, $"R1: untouched, the clean is Rejected ({job0.Quality:0.00})");
                JobGrade[] after = { JobGrade.Passable, JobGrade.Good, JobGrade.Perfect };
                for (int i = 0; i < spots.Length && i < 3; i++)
                {
                    Object.DestroyImmediate(spots[i].gameObject);   // a spot cleaned away (edit mode: the patch goes at once)
                    lint.Check(job0.Grade == after[i], $"R1: {i + 1} of 3 spots clean: {job0.Grade} ({after[i]}; the shutter isn't today's job)");
                }
            }

            // Fault 1: Day 2's shutter.
            GameObject shutter = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(shutter, preview);
            shutter.GetComponent<DeviceDefinition>().ApplyFault(1);
            var job1 = shutter.GetComponent<GraceCameraRepairJob>();
            Transform resting1 = job1 != null && job1.Shutter != null ? FindChild(job1.Shutter.transform.parent, RestingShutterName) : null;
            lint.Check(shutter.GetComponentsInChildren<GrimeSpot>().Length == 0, "fault 1: the spots are off (the camera comes back clean)");
            lint.Check(job1 != null && job1.Shutter != null && job1.Shutter.gameObject.activeSelf && job1.ShutterIsTask
                       && (resting1 == null || !resting1.gameObject.activeSelf), "fault 1 turns on the jammed part only (the resting copy off)");
            if (job1 != null && job1.Shutter != null)
            {
                lint.Check(job1.Grade == JobGrade.Rejected, "R1: untouched, the jammed shutter is Rejected (a camera that can't take a picture)");
                job1.Shutter.Activate();   // edit mode: the swap at once (the bench's own hand)
                lint.Check(job1.Shutter.IsReplaced && job1.Grade == JobGrade.Perfect, $"swapped, the shutter visit is Perfect ({job1.Grade})");
                DeviceWake wake1 = shutter.GetComponent<DeviceWake>();
                lint.Check(wake1 != null && wake1.ShutterReady, "R4: on Day 2, with the working blade in, the camera winds and fires");
            }
            lint.Check(clean.transform.Find("KEEP - scratched sentimental strap") != null && shutter.transform.Find("KEEP - scratched sentimental strap") != null,
                "the strap is on the camera both days");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void CheckDay(Lint lint, DayDefinition day, int number)
    {
        lint.Section($"Day {number}: {AssetDatabase.GetAssetPath(day)}");
        lint.Note(Summary(day));
        FeaturedRepairRequest[] list = day.WalkInRepairsOn(number);
        lint.Check(list.Length > 0, $"Day {number} names its walk-ins' repairs ({list.Length})");
        for (int i = 0; i < list.Length; i++)
        {
            FeaturedRepairRequest entry = list[i];
            string error = "an empty entry";
            bool made = entry != null && entry.TryCreateJob(out Job job, out error);
            lint.Check(made && string.IsNullOrEmpty(entry.storyEpisodeId),
                $"walk-in repair {i + 1}: {(entry != null ? EntryWords(entry.devicePrefab, entry.faultIndex) : "(none)")} makes a job with no episode id{(made ? "" : ": " + error)}");
            if (made) Verbs(lint, entry.devicePrefab, entry.faultIndex, number, $"walk-in repair {i + 1}");
        }
        if (day.useFeaturedRepair && day.featuredRepair != null && day.featuredRepair.devicePrefab != null)
            Verbs(lint, day.featuredRepair.devicePrefab, day.featuredRepair.faultIndex, number, "the featured repair");
        switch (number)
        {
            case 1:
                lint.Check(day.SuppressesRepairDrinksOn(1), "Day 1: no latte for Grace (call 7): repair customers' drink wishes off");
                lint.Check(day.useFeaturedRepair && day.featuredRepair.faultIndex == 0 && Near(day.featuredRegularArrivesAt, 0f), "Day 1: Grace first, fault 0 (the clean)");
                lint.Check(day.StoryVisitsOn(1).Any(v => v != null && v.who != null && v.who.PersistentId == OfficerStory.ProfileId && Near(v.arrivesAt, .25f)),
                    "Day 1: the Officer's first coffee at .25 of the day (≈ 1:52)");
                break;
            case 2:
                lint.Check(day.useFeaturedRepair && day.featuredRepair.faultIndex == 1 && day.featuredRepair.storyEpisodeId == GraceCameraEpisode.EpisodeId
                           && !day.SuppressesRepairDrinksOn(2) && Near(day.patienceMultiplier, PacingSteps.Day2Patience),
                    $"Day 2: Grace first with fault 1 (the shutter), her latte on, patience x{PacingSteps.Day2Patience:0.#} (the pacing pass)");
                break;
            case 3:
                lint.Check(day.featuredRegular != null && day.featuredRegular.PersistentId == GraceCameraEpisode.ProfileId && Near(day.featuredRegularArrivesAt, 0f)
                           && !day.useFeaturedRepair && !day.SuppressesRepairDrinksOn(3) && day.maxCustomers == 2 && Near(day.patienceMultiplier, 1.6f),
                    "Day 3: Grace first for her latte and the photo, two walk-ins at once, patience x1.6");
                break;
        }
    }

    // No job uses a verb its day hasn't taught: RepairChecklist's tools for the device at that fault (§10), and the faults
    // that aren't bench work at all wait for their day (the support call Day 3, the circuit and the counter Day 4).
    // 24 Oct 2026 (Days): a thrift find is judged against the next day's verbs (Verbs(..., day + 1, ...)): it is nobody's job.
    static void Verbs(Lint lint, GameObject prefab, int fault, int day, string what)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject device = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(device, preview);
            device.GetComponent<DeviceDefinition>().ApplyFault(fault);
            JobBase job = device.GetComponent<JobBase>();
            var tools = new HashSet<ToolType>();
            string how = "RepairChecklist";
            try
            {
                var steps = new List<RepairChecklist.Step>();
                RepairChecklist.Build(job, steps);
                foreach (RepairChecklist.Step step in steps) if (step.tool != ToolType.Hand) tools.Add(step.tool);
            }
            catch (Exception e)
            {
                how = "the parts (RepairChecklist failed here: " + e.Message + "; covers not judged)";
                foreach (GrimeSpot spot in device.GetComponentsInChildren<GrimeSpot>()) tools.Add(spot.Tool);
                if (device.GetComponentsInChildren<ReplaceablePart>().Length > 0) tools.Add(ToolType.Tweezers);
            }
            var other = new List<string>();
            if (device.GetComponentsInChildren<CircuitPuzzle>().Length > 0) other.Add("the circuit");
            if (device.GetComponentsInChildren<HumanFault>().Length > 0) other.Add("the counter fault");
            if (device.GetComponent<HoldCallJob>() != null) other.Add("a support call");
            int taughtDay = Mathf.Clamp(day, 0, Taught.Length - 1);
            ToolType[] untaught = tools.Where(t => Array.IndexOf(Taught[taughtDay], t) < 0).ToArray();
            string[] early = other.Where(o => day < FirstDayOf(o)).ToArray();
            lint.Check(untaught.Length == 0 && early.Length == 0,
                $"Day {day}, {what} ({EntryWords(prefab, fault)}): needs {(tools.Count == 0 ? "no tool" : string.Join(", ", tools))}{(other.Count > 0 ? " and " + string.Join(", ", other) : "")} " +
                $"(by {how}); taught by Day {day}: {string.Join(", ", Taught[taughtDay])}" +
                (untaught.Length > 0 ? $"; NOT TAUGHT YET: {string.Join(", ", untaught)}" : "") +
                (early.Length > 0 ? $"; too early: {string.Join(", ", early.Select(o => o + " waits for Day " + FirstDayOf(o)))}" : ""));
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    // The walk-in rule on the day's real data, through the spawner's own job roll (as Day 1 onboarding does): a walk-in who
    // brings a device brings the list's next entry, in order; once they are used up, every walk-in comes for a drink.
    static void CheckWalkInRule(Lint lint, DayDefinition day, DrinkDefinition[] drinks)
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = new GameObject("Temporary Days 1-3 check") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false);
        UnityEngine.Random.State random = UnityEngine.Random.state;
        try
        {
            CustomerSpawner spawner = host.AddComponent<CustomerSpawner>();
            typeof(CustomerSpawner).GetField("today", Private).SetValue(spawner, day);
            typeof(CustomerSpawner).GetField("lastSeenDay", Private).SetValue(spawner, day.dayNumber);
            typeof(CustomerSpawner).GetField("drinks", Private).SetValue(spawner, drinks);
            typeof(CustomerSpawner).GetField("devicePrefabs", Private).SetValue(spawner, new[] { AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath), AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath) });
            MethodInfo roll = typeof(CustomerSpawner).GetMethod("RollJob", Private);
            FeaturedRepairRequest[] list = day.WalkInRepairsOn(day.dayNumber);
            var brought = new List<string>();
            int drinksAfter = 0, repairsAfter = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                UnityEngine.Random.InitState(seed);
                bool usedUp = spawner.WalkInRepairsUsed >= list.Length;
                var job = (Job)roll.Invoke(spawner, new object[] { null, null });
                if (job.kind == JobKind.Repair) { brought.Add($"{job.devicePrefab.name} · {job.faultIndex}"); if (usedUp) repairsAfter++; }
                else if (usedUp) drinksAfter++;
            }
            string want = string.Join(", ", list.Select(e => $"{e.devicePrefab.name} · {e.faultIndex}"));
            lint.Check(string.Join(", ", brought) == want && repairsAfter == 0 && drinksAfter > 0,
                $"{day.name}: 200 walk-ins brought [{string.Join(", ", brought)}] in that order (the list: [{want}]), then {drinksAfter} drinks and {repairsAfter} other repairs");
        }
        finally
        {
            UnityEngine.Random.state = random;
            Object.DestroyImmediate(host);
        }
    }

    // Her new lines keep to 90 characters a line (Dialogue rules), and the bench card's line keeps to 8 words and 46
    // characters (UI/UX's checklist v2 §5.2): Grace: "The lens and film path are dirty." / Grace: "The shutter's jammed."
    static void CheckWords(Lint lint)
    {
        lint.Section("Words: Grace's lines and the bench card");
        foreach (var (what, line) in GraceCameraEpisode.VisitLines())
        {
            int longest = (line ?? "").Split('\n').Max(l => l.Trim().Length);
            lint.Check(longest > 0 && longest <= 90, $"Grace's {what}: {longest} characters at most a line (90): \"{line.Replace("\n", " / ")}\"");
        }
        foreach (string label in new[] { CleanLabel, ShutterLabel })
        {
            bool known = FaultSpeech.TryGet(label, out string said);
            string card = known ? $"Grace: \"{char.ToUpperInvariant(said[0])}{said.Substring(1)}.\"" : "";
            lint.Check(known && WordBudget.Words(card) <= 8 && card.Length <= 46, $"the card's line for \"{label}\": {card} ({WordBudget.Words(card)} words, {card.Length} characters)");
        }
    }

    static void CheckStopgap(Lint lint)
    {
        // 24 Oct 2026 (Days): the stopgap's place is taken by the pacing pass's ramp (P's §1.5).
        lint.Section("Days 4-5: the ramp (the pacing pass; was call 14's stopgap)");
        var d4 = AssetDatabase.LoadAssetAtPath<DayDefinition>(Day4Path);
        var d5 = AssetDatabase.LoadAssetAtPath<DayDefinition>(Day5Path);
        if (!lint.Check(d4 != null && d5 != null, "Days 4 and 5 exist")) return;
        lint.Check(Same(Read(d4), Day4Now) && Near(d4.patienceMultiplier, PacingSteps.Day4Patience) && d4.maxCustomers == PacingSteps.Day4AtOnce,
            $"Day 4: {Words(Read(d4))}, patience x{d4.patienceMultiplier:0.##}, {d4.maxCustomers} at once");
        lint.Check(Same(Read(d5), Day5Now) && Near(d5.patienceMultiplier, PacingSteps.Day5Patience) && d5.maxCustomers == PacingSteps.Day5AtOnce,
            $"Day 5: {Words(Read(d5))}, patience x{d5.patienceMultiplier:0.##}, {d5.maxCustomers} at once");
    }

    // 24 Oct 2026 (Days): each day's thrift find (P's §2.2, his answers p1 and p2) makes a job with nobody waiting, uses only
    // the next day's verbs (Day 1's only Day 1's: his p1, "a cleaning job, so Day 1 brings no new tool"), and is never one of
    // the day's walk-ins.
    static void CheckFinds(Lint lint, DayDefinition[] days)
    {
        lint.Section("The thrift finds (the side project)");
        for (int d = 1; d <= 3; d++)
        {
            DayDefinition day = days[d];
            if (day == null) continue;
            FeaturedRepairRequest find = day.ThriftFindOn(d);
            string error = "none authored (Fixit Fidget › Days › G)";
            bool made = find != null && find.TryCreateJob(out Job job, out error);
            if (!lint.Check(made && string.IsNullOrEmpty(find.storyEpisodeId),
                    $"Day {d}: the find {(find != null ? EntryWords(find.devicePrefab, find.faultIndex) : "")} makes a job with no episode id{(made ? "" : ": " + error)}")) continue;
            Verbs(lint, find.devicePrefab, find.faultIndex, d == 1 ? 1 : d + 1, d == 1 ? "the thrift find (Day 1's tools only, p1)" : "the thrift find (tomorrow's verbs)");
            lint.Check(!day.WalkInRepairsOn(d).Any(e => e != null && e.devicePrefab == find.devicePrefab && e.faultIndex == find.faultIndex),
                $"Day {d}: the find isn't one of the day's walk-ins");
            lint.Check(d != 1 || day.thriftBuy == 0, $"Day {d}: the find costs ${day.thriftBuy} ({(d == 1 ? "free: it is on the shelf at the open" : "bought at the closing before")}), sells for ${day.thriftSale}");
        }
    }
}
#endif
