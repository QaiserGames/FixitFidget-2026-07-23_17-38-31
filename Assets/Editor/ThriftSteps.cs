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
// THE SIDE PROJECT'S THINGS: DAY 1'S FIND, THE THRIFT SHELF AND THE BACK SHELF (24 Oct 2026; Designer P's spec §2.2, §2.5, §6.3
// and §6.6, approved by him with his answers of 24 Oct)
//
//   Fixit Fidget > Days > Thrift 1 - Day 1's find: the thrift watch (prefab)
//   Fixit Fidget > Days > Props 1 - The thrift shelf and the back shelf (scene)
//
// THRIFT 1. His p1: Day 1's side project is "a cleaning job, so Day 1 brings no new tool". Day 1's tools are the cloth and the
// brush (Grace's clean, the walk-in's gunked phone); its walk-in repair is the phone's gunk, and Grace's camera is her own
// story's, so Day 1's find is the pocket watch, Day 2's walk-in device, with a clean of its own: grime round its bezel, dust on
// its dial glass, a fingerprint smudge on the crystal (two brush spots and the cloth). A prefab VARIANT of PocketWatch.prefab
// (Assets/Data/Thrift/ThriftWatch.prefab): its own one fault (SideProject.ThriftWatchFault), so the base watch is untouched,
// no walk-in ever rolls it (it isn't in the café's device pool, and outside Assets/AssetsPrefabs no customer has to say its
// label), and it inherits the hero watch's model, bench layout and framing (Bench 7, Bench 9). The watch's other faults'
// parts are switched off in it, so nothing under the back cover (the movement's grit) is a task: no screwdriver.
// The two new spots are copies of the watch's own "Movement grime" (the same patch and dirt), laid on the face at the
// crystal's height beside the smudge, clear of it.
//
// PROPS 1. Two small props made from simple shapes and the café's own materials (Environment's to replace with models; the
// Blender builder is on the art fixes this session):
//   * the THRIFT SHELF: a small wooden crate on the repair bench's east end, within Ace's reach from the bench's stand point
//     (no walk), with a find spot on its floor and a trigger round it for E and the crosshair (ThriftShelf); and the bench's
//     corner for the find (BenchSlots' Side Slot, "Slot_Side (the thrift find's corner)"), on the bench top east of the mat,
//     outside the customers' slots;
//   * the BACK SHELF: a two-tier crate of cup sleeves and bean bags on the back bar, between the bar mat and the cup
//     station, 2.9 m from the drinks machine, on the staff side (BackShelf: "Take cups and beans").
// Both are fixed things, not furniture (F2's floor map treats them as fixed, like the repair bar). One undo group; the scene is
// saved when the checks pass; a second run changes nothing.
// Reports: Logs/Days/<step>-<time>.txt and -last.txt, and one PASS/DONE line in the Console.
// ---------------------------------------------------------------------------
public static class ThriftSteps
{
    const string Menu = "Fixit Fidget/Days/";
    const string Tag = "[Days props] ";
    public const string MenuThrift = Menu + "Thrift 1 - Day 1's find: the thrift watch (prefab)";
    public const string MenuProps = Menu + "Props 1 - The thrift shelf and the back shelf (scene)";

    public const string WatchPath = PacingSteps.WatchPath, VariantPath = PacingSteps.ThriftWatchPath;
    const string VariantFolder = "Assets/Data/Thrift";
    public const int ThriftWatchSale = 55;

    // The clean's spots: the base watch's smudge, and two copies of its movement grime laid on the face (local to the watch,
    // metres; the crystal's face is at the smudge's height). Clear of the smudge (radius 7.5 mm at (4, -3.3)) and of each other.
    public const string SmudgeName = "Smudge_Crystal", GrimeTemplate = "Movement grime";
    public const string BezelName = "Bezel grime", DialName = "Dial dust";
    static readonly Vector2 BezelAt = new Vector2(-.0110f, .0060f), DialAt = new Vector2(.0040f, .0140f);   // (x, z)

    // ---------- the scene's names (the reference copy of the café scene, 24 Oct) ----------
    public const string BenchSlotsPath = "ACE'S CAFE - layout study 02/03 - repair work area/Workbench/BenchSlots";
    public const string RepairAreaPath = "ACE'S CAFE - layout study 02/03 - repair work area";
    public const string DrinksAreaPath = "ACE'S CAFE - layout study 02/04 - drinks work area";
    public const string ThriftShelfName = "Thrift shelf (the side project)", SideSlotName = "Slot_Side (the thrift find's corner)";
    public const string BackShelfName = "Back shelf (cups and beans)";
    /// <summary>The bench top's height (cafe_bar.py: the chestnut top at 1.005 exactly).</summary>
    public const float BenchTop = 1.005f, BackBarTop = 1.0f;
    public static readonly Vector3 ThriftShelfAt = new Vector3(-3.30f, BenchTop, 14.27f);     // the bench's east end, Ace's side
    public static readonly Vector3 SideSlotAt = new Vector3(-4.30f, 1.006f, 14.263f);          // east of the mat, the slots' row
    public static readonly Vector3 BackShelfAt = new Vector3(.15f, BackBarTop, 17.72f);        // the back bar, by the cup station
    const float ThriftShelfYaw = 0f, BackShelfYaw = 180f, SideSlotYaw = 180f;
    const string Wood = "Assets/Art/Materials/CC_Wood_Inner.mat", DarkWood = "Assets/Art/Materials/CC_Wood_Espresso.mat";
    const string Cream = "Assets/Art/Materials/DC_Cream.mat", White = "Assets/Art/Materials/DC_White.mat", Fabric = "Assets/Art/Materials/DC_Fabric.mat";

    [MenuItem(MenuThrift, false, 31)]
    static void ThriftMenu() => Perform("Thrift 1 - Day 1's find: the thrift watch (prefab)", "thrift-1", false, ThriftSurvey, ThriftChange, ThriftChecks);

    [MenuItem(MenuProps, false, 32)]
    static void PropsMenu() => Perform("Props 1 - The thrift shelf and the back shelf (scene)", "props-1", true, PropsSurvey, PropsChange, PropsChecks);

    [MenuItem(MenuThrift, true)]
    [MenuItem(MenuProps, true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    // ---------- the driver (DaysOneToThreeSteps' and PacingSteps' way) ----------

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
            run.Text.AppendLine().AppendLine(run.Changes == 0 ? $"NOTHING TO CHANGE: already done. {run.Passes} check(s) passed."
                : $"DONE: {run.Changes} change(s); {run.Passes} check(s) passed; saved.");
            Debug.Log(Tag + $"{(run.Changes == 0 ? "PASS" : "DONE")}: {title} ({run.Changes} change(s), {run.Passes} check(s) passed). Report: {Write(run)}");
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
    // Thrift 1: the thrift watch (a variant of the pocket watch with one clean)
    // =================================================================================================================

    static GameObject watchAsset;
    static GameObject variantRoot;   // the variant's loaded contents while the step runs

    static void ThriftSurvey(Run run)
    {
        watchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
        run.Require(watchAsset != null, $"No prefab at {WatchPath}. Nothing changed.");
        DeviceDefinition def = watchAsset.GetComponent<DeviceDefinition>();
        run.Require(def != null && def.faults != null && def.faults.Length >= 1 && watchAsset.GetComponent<RepairJob>() != null,
            $"{WatchPath} has no DeviceDefinition with faults, or no RepairJob. Nothing changed; tell Claude.");
        run.Require(def.realSize, $"{WatchPath} isn't real size yet: run Fixit Fidget › Bench › Devices 2 first. Nothing changed.");
        Transform smudge = watchAsset.transform.Find(SmudgeName), grime = watchAsset.transform.Find(GrimeTemplate);
        run.Require(smudge != null && smudge.GetComponent<GrimeSpot>() != null && smudge.GetComponent<GrimeSpot>().What == GrimeSpot.Kind.Smudge,
            $"{WatchPath} has no smudge \"{SmudgeName}\" (a cloth spot) on its crystal. Nothing changed; tell Claude.");
        run.Require(grime != null && grime.GetComponent<GrimeSpot>() != null && grime.GetComponent<GrimeSpot>().What == GrimeSpot.Kind.Dirt,
            $"{WatchPath} has no \"{GrimeTemplate}\" (a brush spot) to copy the look from. Nothing changed; tell Claude.");
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
        if (existing != null)
        {
            run.Require(PrefabUtility.GetPrefabAssetType(existing) == PrefabAssetType.Variant
                        && PrefabUtility.GetCorrespondingObjectFromSource(existing) == watchAsset,
                $"{VariantPath} exists but isn't a variant of {WatchPath}. Nothing changed; tell Claude.");
            run.Info($"{VariantPath}: there (a variant of the pocket watch); its fault: {Describe(existing.GetComponent<DeviceDefinition>())}");
        }
        else run.Info($"{VariantPath}: not made yet");
        run.Info($"the pocket watch's own faults (left as they are): {Describe(def)}");
    }

    static string Describe(DeviceDefinition def) => def == null || def.faults == null ? "(none)"
        : string.Join(" | ", def.faults.Select(f => f == null ? "(empty)" : $"{f.type} \"{f.description}\" ${f.payout} [{string.Join(", ", (f.enableObjects ?? new GameObject[0]).Select(g => g != null ? g.name : "-"))}]"));

    static void ThriftChange(Run run)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) == null)
        {
            if (!AssetDatabase.IsValidFolder(VariantFolder)) AssetDatabase.CreateFolder("Assets/Data", "Thrift");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(watchAsset, preview);
                instance.name = "ThriftWatch";
                PrefabUtility.SaveAsPrefabAsset(instance, VariantPath, out bool ok);
                run.Require(ok, $"Unity couldn't save the variant at {VariantPath}. Nothing else changed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            run.Changed($"{VariantPath}: made, a variant of {WatchPath}");
            // The new asset can't be undone by the scene's undo: an Undo from here deletes it.
            run.OnFail(() => AssetDatabase.DeleteAsset(VariantPath));
        }

        variantRoot = PrefabUtility.LoadPrefabContents(VariantPath);
        GameObject root = variantRoot;
        run.OnFail(() => { if (variantRoot != null) PrefabUtility.UnloadPrefabContents(variantRoot); variantRoot = null; });
        bool changed = false;
        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        GrimeSpot smudge = root.transform.Find(SmudgeName).GetComponent<GrimeSpot>();
        Transform template = root.transform.Find(GrimeTemplate);

        // The two new brush spots: copies of the movement grime on the face, beside the smudge.
        GameObject bezel = Spot(root, template, BezelName, BezelAt, smudge.transform.localPosition.y, run, ref changed);
        GameObject dial = Spot(root, template, DialName, DialAt, smudge.transform.localPosition.y, run, ref changed);

        // Every other fault's parts off: nothing under the back cover is a task (no screwdriver), no part to swap.
        var mine = new HashSet<GameObject> { smudge.gameObject, bezel, dial };
        DeviceDefinition baseDef = watchAsset.GetComponent<DeviceDefinition>();
        foreach (DeviceFault f in baseDef.faults ?? new DeviceFault[0])
            foreach (GameObject g in f?.enableObjects ?? new GameObject[0])
            {
                if (g == null) continue;
                Transform inVariant = root.transform.Find(PathIn(watchAsset.transform, g.transform));
                if (inVariant == null || mine.Contains(inVariant.gameObject) || !inVariant.gameObject.activeSelf) continue;
                inVariant.gameObject.SetActive(false);
                changed = true;
                run.Changed($"{inVariant.name}: switched off in the thrift watch (another fault's part: its clean is the face only)");
            }

        // Its one fault: the clean.
        GameObject[] objects = { smudge.gameObject, bezel, dial };
        bool right = def.faults != null && def.faults.Length == 1 && def.faults[0] != null && def.faults[0].type == FaultType.Cleaning
                     && def.faults[0].description == SideProject.ThriftWatchFault && def.faults[0].payout == ThriftWatchSale
                     && def.faults[0].enableObjects != null && def.faults[0].enableObjects.SequenceEqual(objects);
        if (right) run.Info($"its fault: already {Describe(def)}");
        else
        {
            string was = Describe(def);
            def.faults = new[] { new DeviceFault { type = FaultType.Cleaning, description = SideProject.ThriftWatchFault, enableObjects = objects, payout = ThriftWatchSale } };
            EditorUtility.SetDirty(def);
            changed = true;
            run.Changed($"its fault: {was} → {Describe(def)}");
        }
        foreach (GameObject g in objects)
            if (g.activeSelf) { g.SetActive(false); changed = true; run.Changed($"{g.name}: inactive in the prefab (the fault turns it on)"); }

        if (changed)
        {
            run.AfterPass(() =>
            {
                PrefabUtility.SaveAsPrefabAsset(variantRoot, VariantPath);
                PrefabUtility.UnloadPrefabContents(variantRoot);
                variantRoot = null;
                run.Info("saved prefab " + VariantPath);
            });
        }
        else run.AfterPass(() => { PrefabUtility.UnloadPrefabContents(variantRoot); variantRoot = null; });
    }

    // A brush spot on the face: the movement grime's copy, named, at (x, crystal height, z); made once, kept where it is.
    static GameObject Spot(GameObject root, Transform template, string name, Vector2 at, float y, Run run, ref bool changed)
    {
        Transform existing = root.transform.Find(name);
        Vector3 want = new Vector3(at.x, y, at.y);
        if (existing != null)
        {
            if ((existing.localPosition - want).sqrMagnitude > 1e-10f)
            {
                existing.localPosition = want;
                changed = true;
                run.Changed($"{name}: moved to {Mm(want)} (the face, beside the smudge)");
            }
            else run.Info($"{name}: already at {Mm(want)}");
            return existing.gameObject;
        }
        GameObject copy = Object.Instantiate(template.gameObject, root.transform, false);
        copy.name = name;
        copy.transform.localPosition = want;
        copy.transform.localRotation = template.localRotation;
        copy.transform.localScale = template.localScale;
        copy.SetActive(false);
        changed = true;
        run.Changed($"{name}: made, a copy of the watch's \"{template.name}\" (a brush spot), at {Mm(want)} on the face");
        return copy;
    }

    static string PathIn(Transform root, Transform t)
    {
        var names = new List<string>();
        for (Transform x = t; x != null && x != root; x = x.parent) names.Add(x.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string Mm(Vector3 v) => $"({v.x * 1000f:0.0}, {v.y * 1000f:0.0}, {v.z * 1000f:0.0}) mm";

    static void ThriftChecks(Run run)
    {
        GameObject root = variantRoot;
        if (!run.Check(root != null, "the thrift watch's contents are loaded")) return;
        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        run.Check(def != null && def.faults != null && def.faults.Length == 1 && def.faults[0].type == FaultType.Cleaning
                  && def.faults[0].description == SideProject.ThriftWatchFault && def.faults[0].payout == ThriftWatchSale,
            $"one fault, the clean: Cleaning, \"{SideProject.ThriftWatchFault}\", ${ThriftWatchSale} ({Describe(def)})");
        DeviceDefinition baseDef = watchAsset.GetComponent<DeviceDefinition>();
        run.Check(baseDef.faults.Length >= 2 && baseDef.faults.All(f => f != null && f.description != SideProject.ThriftWatchFault),
            $"the pocket watch itself is untouched ({Describe(baseDef)})");
        run.Check(root.GetComponent<SideProject>() == null, "no SideProject on the prefab (the shelf marks the find when it puts it out)");

        // On a real instance: fault 0 turns on exactly the three spots (cloth and brush), nothing covered, nothing to swap.
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject device = Object.Instantiate(root);
            SceneManager.MoveGameObjectToScene(device, preview);
            device.GetComponent<DeviceDefinition>().ApplyFault(0);
            GrimeSpot[] spots = device.GetComponentsInChildren<GrimeSpot>();
            run.Check(spots.Length == 3 && spots.Select(s => s.name).OrderBy(n => n).SequenceEqual(new[] { BezelName, DialName, SmudgeName }.OrderBy(n => n)),
                $"the clean's spots: {string.Join(", ", spots.Select(s => $"{s.name} ({s.Tool})"))} (the bezel, the dial, the crystal)");
            var tools = new HashSet<ToolType>(spots.Select(s => s.Tool));
            run.Check(tools.SetEquals(new[] { ToolType.Brush, ToolType.Cloth }), $"Day 1's tools only: {string.Join(", ", tools)} (his p1: no new tool)");
            run.Check(device.GetComponentsInChildren<ReplaceablePart>().Length == 0, "no part to swap");
            run.Check(spots.All(s => s.CoveredBy == null), "no spot under a cover: the screws stay in (no screwdriver)");
            JobBase job = device.GetComponent<JobBase>();
            var steps = new List<RepairChecklist.Step>();
            string how = "";
            try { RepairChecklist.Build(job, steps); }
            catch (Exception e) { how = " (the checklist couldn't be read: " + e.Message + ")"; }
            var verbs = new HashSet<ToolType>(steps.Where(st => st.tool != ToolType.Hand).Select(st => st.tool));
            run.Check(how.Length == 0 && verbs.IsSubsetOf(new[] { ToolType.Brush, ToolType.Cloth }),
                $"the bench card's steps use {(verbs.Count == 0 ? "no tool" : string.Join(", ", verbs))}{how}");
            run.Check(job != null && job.Grade == JobGrade.Rejected, $"untouched, it's Rejected ({(job != null ? job.Quality : 0f):0.00})");
            if (job != null)
            {
                foreach (GrimeSpot s in spots) Object.DestroyImmediate(s.gameObject);   // cleaned away (edit mode: at once)
                run.Check(job.Grade == JobGrade.Perfect && job.IsFinished, $"its three spots clean, it's finished and Perfect ({job.Grade})");
            }
            var request = new FeaturedRepairRequest { devicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath), faultIndex = 0 };
            run.Check(request.devicePrefab == null || request.TryCreateJob(out _, out _), "the days' data can name it (FeaturedRepairRequest makes its job)");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        string card = "Your thrift watch: " + SideProject.WordsFor(SideProject.ThriftWatchFault).card + ".";
        run.Check(WordBudget.Words(card) <= SideProject.CardWords && card.Length <= SideProject.CardCharacters,
            $"the bench card's line: \"{card}\" ({WordBudget.Words(card)} words, {card.Length} characters; 8 and 46 at most)");
    }

    // =================================================================================================================
    // Props 1: the thrift shelf (and the bench's corner) and the back shelf
    // =================================================================================================================

    static GameObject benchSlots, repairArea, drinksArea;
    static ItemSlotArea benchArea;

    static void PropsSurvey(Run run)
    {
        benchSlots = Playtest4Shared.ByPath(run, BenchSlotsPath);
        repairArea = Playtest4Shared.ByPath(run, RepairAreaPath);
        drinksArea = Playtest4Shared.ByPath(run, DrinksAreaPath);
        benchArea = benchSlots.GetComponent<ItemSlotArea>();
        run.Require(benchArea != null, $"{BenchSlotsPath} has no ItemSlotArea. Nothing changed; tell Claude.");
        run.Prop(new SerializedObject(benchArea), "sideSlot");   // refuses until the new field has compiled
        foreach (string m in new[] { Wood, DarkWood, Cream, White, Fabric })
            if (AssetDatabase.LoadAssetAtPath<Material>(m) == null) run.Info($"NOTE: no material at {m}: that part keeps Unity's default (Environment's to dress)");
        run.Info($"the bench's slots: {string.Join(", ", benchSlots.transform.Cast<Transform>().Select(t => $"{t.name} {t.position.ToString("F3")}"))}");
        GameObject shelf = Playtest4Shared.FindByPath(RepairAreaPath + "/" + ThriftShelfName);
        GameObject back = Playtest4Shared.FindByPath(DrinksAreaPath + "/" + BackShelfName);
        run.Info($"the thrift shelf: {(shelf != null ? "there" : "to make")}; the back shelf: {(back != null ? "there" : "to make")}");
    }

    static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

    static void PropsChange(Run run)
    {
        // The bench's corner for the find.
        Transform corner = benchSlots.transform.Find(SideSlotName);
        if (corner == null)
        {
            corner = run.Make(SideSlotName, benchSlots.transform, "the thrift find's own corner of the bench: outside the customers' slots").transform;
        }
        Place(run, corner, SideSlotAt, SideSlotYaw, "the find's corner");
        run.SetObject(benchArea, "sideSlot", corner, $"{BenchSlotsPath} · ItemSlotArea.sideSlot (the find's corner)");

        // The thrift shelf: a little wooden crate on the bench's east end.
        Transform shelf = repairArea.transform.Find(ThriftShelfName);
        if (shelf == null) shelf = run.Make(ThriftShelfName, repairArea.transform, "the side project's shelf by the bench (P's §2.2)").transform;
        Place(run, shelf, ThriftShelfAt, ThriftShelfYaw, "the thrift shelf");
        Board(run, shelf, "Base", new Vector3(0f, .01f, 0f), new Vector3(.36f, .02f, .22f), Wood);
        Board(run, shelf, "Side (west)", new Vector3(-.17f, .15f, 0f), new Vector3(.02f, .30f, .22f), Wood);
        Board(run, shelf, "Side (east)", new Vector3(.17f, .15f, 0f), new Vector3(.02f, .30f, .22f), Wood);
        Board(run, shelf, "Back", new Vector3(0f, .15f, -.1025f), new Vector3(.36f, .30f, .015f), Wood);
        Board(run, shelf, "Middle shelf", new Vector3(0f, .17f, .005f), new Vector3(.32f, .015f, .20f), Wood);
        Board(run, shelf, "Price tag", new Vector3(.12f, .045f, .108f), new Vector3(.06f, .035f, .003f), Cream);
        Transform spot = shelf.Find("Find spot");
        if (spot == null) spot = run.Make("Find spot", shelf, "where the find rests on the shelf's floor").transform;
        LocalPose(run, spot, new Vector3(0f, .02f, .01f), Quaternion.Euler(0f, 180f, 0f), "the find spot");
        Trigger(run, shelf.gameObject, new Vector3(0f, .17f, 0f), new Vector3(.40f, .34f, .26f), "the shelf's reach (E and the crosshair)");
        // (No ?? on Unity objects: in the editor a missing component reads as a stand-in object, not null.)
        ThriftShelf thrift = shelf.GetComponent<ThriftShelf>();
        if (thrift == null) thrift = run.Add<ThriftShelf>(shelf.gameObject, "the side project: today's find, E works on it");
        run.SetObject(thrift, "findSpot", spot, ThriftShelfName + " · ThriftShelf.findSpot");
        run.SetObject(thrift, "bench", benchArea, ThriftShelfName + " · ThriftShelf.bench");

        // The back shelf: cups and beans on the back bar, two tiers.
        Transform back = drinksArea.transform.Find(BackShelfName);
        if (back == null) back = run.Make(BackShelfName, drinksArea.transform, "restock by hand: the café's stock beyond the machine (P's §2.5)").transform;
        Place(run, back, BackShelfAt, BackShelfYaw, "the back shelf");
        Board(run, back, "Base", new Vector3(0f, .01f, 0f), new Vector3(.50f, .02f, .30f), DarkWood);
        Board(run, back, "Side (west)", new Vector3(-.24f, .21f, 0f), new Vector3(.02f, .42f, .30f), DarkWood);
        Board(run, back, "Side (east)", new Vector3(.24f, .21f, 0f), new Vector3(.02f, .42f, .30f), DarkWood);
        Board(run, back, "Back", new Vector3(0f, .21f, .1425f), new Vector3(.50f, .42f, .015f), DarkWood);
        Board(run, back, "Middle tier", new Vector3(0f, .21f, 0f), new Vector3(.46f, .02f, .28f), DarkWood);
        Board(run, back, "Top", new Vector3(0f, .41f, 0f), new Vector3(.50f, .02f, .30f), DarkWood);
        for (int i = 0; i < 3; i++)
            Board(run, back, $"Cup sleeve {i + 1}", new Vector3(-.15f + i * .15f, .10f, -.02f), new Vector3(.08f, .16f, .08f), White, cylinder: true);
        Board(run, back, "Bean bag 1", new Vector3(-.10f, .30f, -.01f), new Vector3(.16f, .16f, .10f), Fabric);
        Board(run, back, "Bean bag 2", new Vector3(.10f, .29f, -.01f), new Vector3(.15f, .14f, .10f), Fabric);
        Trigger(run, back.gameObject, new Vector3(0f, .22f, -.02f), new Vector3(.56f, .46f, .40f), "the shelf's reach (E and the crosshair)");
        if (back.GetComponent<BackShelf>() == null) run.Add<BackShelf>(back.gameObject, "restock by hand: \"Take cups and beans\"");
    }

    // A transform at a world pose (recorded; reported only when it changes).
    static void Place(Run run, Transform t, Vector3 at, float yaw, string what)
    {
        Quaternion want = Quaternion.Euler(0f, yaw, 0f);
        if ((t.position - at).sqrMagnitude < 1e-8f && Quaternion.Angle(t.rotation, want) < .01f && (t.localScale - Vector3.one).sqrMagnitude < 1e-8f)
        {
            run.Info($"{what}: already at {at.ToString("F3")}, yaw {yaw:0}");
            return;
        }
        Undo.RecordObject(t, run.Title);
        string was = t.position.ToString("F3");
        t.SetPositionAndRotation(at, want);
        t.localScale = Vector3.one;
        run.Changed($"{what}: {was} → {at.ToString("F3")}, yaw {yaw:0}");
    }

    static void LocalPose(Run run, Transform t, Vector3 at, Quaternion rot, string what)
    {
        if ((t.localPosition - at).sqrMagnitude < 1e-10f && Quaternion.Angle(t.localRotation, rot) < .01f) { run.Info($"{what}: already placed"); return; }
        Undo.RecordObject(t, run.Title);
        t.localPosition = at;
        t.localRotation = rot;
        run.Changed($"{what}: placed at {at.ToString("F3")} (local)");
    }

    // A part of a prop: a cube (or a cylinder) with its collider gone, at a local pose and size, in a material.
    static void Board(Run run, Transform parent, string name, Vector3 at, Vector3 size, string material, bool cylinder = false)
    {
        Transform part = parent.Find(name);
        if (part == null)
        {
            GameObject go = GameObject.CreatePrimitive(cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Undo.RegisterCreatedObjectUndo(go, run.Title);
            Undo.SetTransformParent(go.transform, parent, run.Title);
            part = go.transform;
            run.Changed($"{Playtest4Shared.PathOf(go)}: made ({(cylinder ? "a cylinder" : "a board")} {size.ToString("F3")} m)");
        }
        // A cylinder primitive is 2 units tall: half its height on y.
        Vector3 scale = cylinder ? new Vector3(size.x, size.y * .5f, size.z) : size;
        if ((part.localPosition - at).sqrMagnitude > 1e-10f || (part.localScale - scale).sqrMagnitude > 1e-10f || Quaternion.Angle(part.localRotation, Quaternion.identity) > .01f)
        {
            Undo.RecordObject(part, run.Title);
            part.localPosition = at;
            part.localRotation = Quaternion.identity;
            part.localScale = scale;
            run.Changed($"{Playtest4Shared.PathOf(part.gameObject)}: placed");
        }
        MeshRenderer r = part.GetComponent<MeshRenderer>();
        Material m = Mat(material);
        if (r != null && m != null && r.sharedMaterial != m)
        {
            Undo.RecordObject(r, run.Title);
            r.sharedMaterial = m;
            run.Changed($"{Playtest4Shared.PathOf(part.gameObject)}: {m.name}");
        }
    }

    // The trigger round a prop that E and the crosshair find (a trigger: Ace never bumps into it, and in first person the ray
    // stops on it before the find inside).
    static void Trigger(Run run, GameObject go, Vector3 centre, Vector3 size, string what)
    {
        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null) box = run.Add<BoxCollider>(go, what);
        if (box.isTrigger && (box.center - centre).sqrMagnitude < 1e-10f && (box.size - size).sqrMagnitude < 1e-10f) { run.Info($"{what}: already set"); return; }
        Undo.RecordObject(box, run.Title);
        box.isTrigger = true;
        box.center = centre;
        box.size = size;
        run.Changed($"{Playtest4Shared.PathOf(go)}: {what}, a trigger {size.ToString("F2")} m");
    }

    static void PropsChecks(Run run)
    {
        Transform corner = benchSlots.transform.Find(SideSlotName);
        var so = new SerializedObject(benchArea);
        run.Check(corner != null && so.FindProperty("sideSlot").objectReferenceValue == corner, "the bench's slots have the find's corner as their side slot");
        SerializedProperty slots = so.FindProperty("slots");
        bool outside = true;
        for (int i = 0; slots != null && i < slots.arraySize; i++) outside &= slots.GetArrayElementAtIndex(i).objectReferenceValue != corner;
        run.Check(outside, "the corner is none of the customers' slots (never counted: a customer's device always has the main place)");
        if (corner != null)
        {
            float fromMat = float.PositiveInfinity;
            for (int i = 0; slots != null && i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) fromMat = Mathf.Min(fromMat, Flat(t.position - corner.position).magnitude);
            run.Check(fromMat >= .3f && corner.position.x > -5.79f && corner.position.x < -2.61f && Mathf.Abs(corner.position.y - 1.006f) < .002f,
                $"the corner is on the bench top ({corner.position.ToString("F3")}), {fromMat:0.00} m from the nearest customer slot (0.3 at least: never under the device in view)");
        }
        Transform shelf = repairArea.transform.Find(ThriftShelfName);
        ThriftShelf thrift = shelf != null ? shelf.GetComponent<ThriftShelf>() : null;
        run.Check(thrift != null, "the thrift shelf has its ThriftShelf");
        StationInteractable bench = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Include).FirstOrDefault(s => s.IsWorkSurface);
        Transform stand = bench != null && bench.StandPoint != null ? bench.StandPoint : null;
        if (shelf != null && stand != null)
        {
            float reach = Flat(shelf.position - stand.position).magnitude;
            run.Check(reach <= 2.0f, $"the thrift shelf is within Ace's reach from the bench's stand point ({reach:0.00} m; E reaches 2.2: no walk)");
        }
        BoxCollider shelfReach = shelf != null ? shelf.GetComponent<BoxCollider>() : null;
        run.Check(shelfReach != null && shelfReach.isTrigger, "the thrift shelf's reach is a trigger");
        Transform back = drinksArea.transform.Find(BackShelfName);
        BoxCollider backReach = back != null ? back.GetComponent<BoxCollider>() : null;
        run.Check(back != null && back.GetComponent<BackShelf>() != null && backReach != null && backReach.isTrigger,
            "the back shelf has its BackShelf and a trigger for E");
        BeverageStation machine = Object.FindAnyObjectByType<BeverageStation>(FindObjectsInactive.Include);
        if (back != null && machine != null)
        {
            float d = Flat(back.position - machine.transform.position).magnitude;
            run.Check(d >= 1.5f && d <= 3.5f, $"the back shelf is {d:0.00} m from the drinks machine (2-3 m: a short trip, on the staff side)");
        }
        run.Check(shelf == null || shelf.GetComponentsInChildren<Collider>().All(c => c.isTrigger), "the thrift shelf has no solid collider (it is on the bench top: nothing to bump)");
        run.Check(back == null || back.GetComponentsInChildren<Collider>().All(c => c.isTrigger), "the back shelf has no solid collider (the back bar is solid already)");
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
#endif
