#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE FORGIVING BENCH: THE STAGE IN THE SCENE (14 Oct 2026; specs/systems-forgiving-bench.md §7 step 14; blender-wave1.md §5
// step 3; td-one-scale.md §6.9)
//
//   Fixit Fidget > Bench > Bench 7 - The forgiving stage (scene)
//       Under the Workbench's InspectRig/Stage (v2): Art's silicone repair mat (BM_RepairMat, with its cups, pad and well), the
//       tool rest (BT_ToolRest, the five tools moved onto its seats) and the old-parts tray (BT_ScrapTray), from the files the
//       importers bring in (contracts §8). A file not there yet gets a stand-in with the same empties at Art's coordinates
//       (bench_stage.json, bench_tools.json), so nothing waits on Blender; run it again once the files land and the stand-ins
//       are swapped for the models. The v2 slab, parts tray (the whole group, its walls too: 23 Oct 2026) and caddy are
//       switched off, not deleted. BenchStage is wired; the inspect point goes to the mat's top; the bench's three device
//       slots move off the mat to the bench in front of it (the TD's §6.9: mat y -265, out of every close-up frame), so a
//       device waiting never covers a compartment; the close-up's mouse settle widens to 45° (faceSnapWithin: a spin always
//       lands square, Systems' decision 12); the three devices' prefabs get their layouts (DeviceDefinition.benchLayout's
//       rest, tool rest and tray; the camera's fields are Bench 9's).
//       One Undo for the scene; photos before and after; the scene saved; a report in Logs/Bench/forgiving-stage-<time>/.
//       A second run says "already done" and changes nothing (unless a stand-in can now be swapped for its model).
//   Fixit Fidget > Bench > Bench 8 - The clumsy hand (lab, drives itself), and its full run (three seeds each)
//       A first-timer's hand (ClumsyHand) plays the camera's clean and swap, the phone's screen and the watch's seized movement
//       through the real input path, by mouse and by pad, at 60 and 15 fps, against §5's bars (ClumsyBenchLab).
//   Fixit Fidget > Checks > Bench rules
//       The forgiving bench's maths (BenchRules: the hit areas, the flight, the slot rule, the press pass, the finisher). The
//       same check runs in the console (Tests/BenchRules).
// ---------------------------------------------------------------------------
static class ForgivingBenchSteps
{
    const string Menu = "Fixit Fidget/Bench/";
    const string Tag = "[Bench 7] ";
    const string Title = "Bench 7 - The forgiving stage";
    const string StageName = "Stage (v2)";
    const string MatPath = "Assets/Art/Models/BenchStage/BM_RepairMat.fbx";
    const string RestPath = "Assets/Art/Models/BenchTools/BT_ToolRest.fbx";
    const string TrayPath = "Assets/Art/Models/BenchTools/BT_ScrapTray.fbx";
    const string ClothHeldPath = "Assets/Art/Models/BenchTools/BT_ClothHeld.fbx";
    const string StandIn = " (stand-in)";
    const float MatZ = -.02f;            // the mat a touch toward the camera, as Bench 2 laid it (Playtest3Session4Steps.cs:102)

    // Art's empties (bench_stage.json; Unity local, metres: x = mat x, z = mat y).
    static readonly (string name, Vector3 at)[] MatEmpties =
    {
        ("Cup_1", new Vector3(.036f, .0018f, -.024f)), ("Cup_2", new Vector3(.036f, .0018f, 0f)), ("Cup_3", new Vector3(.036f, .0018f, .024f)),
        ("Cup_4", new Vector3(.060f, .0018f, -.024f)), ("Cup_5", new Vector3(.060f, .0018f, 0f)), ("Cup_6", new Vector3(.060f, .0018f, .024f)),
        ("Pad_Small", new Vector3(.116f, .0053f, -.058f)), ("Pad_Large", new Vector3(.130f, .0053f, 0f)),
        ("Well_Small", new Vector3(.206f, .002f, -.058f)), ("Well_Large", new Vector3(.222f, .002f, 0f)),
    };
    // The rest's seats (bench_tools.json: local to the rest; the tools lie at about 5°, handle end higher, tips away).
    static readonly (string name, ToolType tool, Vector3 at, Vector3 euler)[] Seats =
    {
        ("Seat_Brush", ToolType.Brush, new Vector3(.0375f, .01055f, .032f), new Vector3(275f, 0f, 0f)),
        ("Seat_Screwdriver", ToolType.Screwdriver, new Vector3(-.0375f, .00588f, .03f), new Vector3(275f, 0f, 0f)),
        ("Seat_Pry", ToolType.Pry, new Vector3(-.0125f, .00528f, .025f), new Vector3(275f, 0f, 0f)),
        ("Seat_Tweezers", ToolType.Tweezers, new Vector3(.0125f, .00921f, .018f), new Vector3(275f, 0f, 0f)),
        ("Seat_Cloth", ToolType.Cloth, new Vector3(0f, .003f, .075f), new Vector3(0f, 270f, 0f)),
    };
    static readonly (string name, Vector3 at)[] TraySlots =
    {
        ("Slot_1", new Vector3(-.01f, .0008f, 0f)), ("Slot_2", new Vector3(.025f, .0008f, 0f)), ("Slot_3", new Vector3(.06f, .0008f, 0f)),
    };
    // The bench's three device slots, off the mat (the TD's §6.9: in front of it, toward Ace, mat y -230..-300, between x -150
    // and +120; every close-up frame's bottom ray meets the mat at y -162..-177, so a device there is out of every frame).
    static readonly (string name, Vector2 mm)[] DeviceSlots =
    {
        ("Slot_0", new Vector2(-15f, -265f)), ("Slot_1", new Vector2(-150f, -265f)), ("Slot_2", new Vector2(120f, -265f)),
    };
    // Which layout each device prefab gets (the TD's devices; PhoneJob, the support call's phone, lies as the phone does).
    static readonly (string path, string kind)[] Devices =
    {
        ("Assets/AssetsPrefabs/PhoneRepair.prefab", "phone"), ("Assets/AssetsPrefabs/PhoneJob.prefab", "phone"),
        ("Assets/GraceShowcase/GraceReunionCamera.prefab", "camera"), ("Assets/AssetsPrefabs/PocketWatch.prefab", "watch"),
    };

    // ================================================================== Bench 7

    [MenuItem(Menu + "Bench 7 - The forgiving stage (scene)")]
    static void BuildForgivingStage()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play first: this step changes the scene."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath) { Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first."); return; }
        BenchRig rig = Object.FindAnyObjectByType<BenchRig>(FindObjectsInactive.Include);
        if (rig == null) { Debug.LogError(Tag + "No BenchRig (the Workbench's InspectRig) in the scene."); return; }
        Transform rigT = rig.transform;
        Transform stageRoot = rigT.Find(StageName);
        BenchStage stage = rig.GetComponent<BenchStage>();
        if (stageRoot == null || stage == null) { Debug.LogError(Tag + "No v2 stage under the rig: run Fixit Fidget > Bench > Bench 2 - Build the bench stage first."); return; }

        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Bench", $"forgiving-stage-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        var report = new StringBuilder();
        report.AppendLine(Title + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();

        bool matFile = AssetDatabase.LoadAssetAtPath<GameObject>(MatPath) != null;
        bool restFile = AssetDatabase.LoadAssetAtPath<GameObject>(RestPath) != null;
        bool trayFile = AssetDatabase.LoadAssetAtPath<GameObject>(TrayPath) != null;
        Transform oldMat = Child(stageRoot, "BM_RepairMat") ?? Child(stageRoot, "BM_RepairMat" + StandIn);
        Transform oldRest = Child(stageRoot, "BT_ToolRest") ?? Child(stageRoot, "BT_ToolRest" + StandIn);
        Transform oldTray = Child(stageRoot, "BT_ScrapTray") ?? Child(stageRoot, "BT_ScrapTray" + StandIn);
        bool wired = oldMat != null && stage.matFrame == oldMat && oldRest != null && stage.toolRest == oldRest && oldTray != null && stage.scrapTray == oldTray;
        bool upgrade = wired && (matFile && oldMat.name.EndsWith(StandIn) || restFile && oldRest.name.EndsWith(StandIn) || trayFile && oldTray.name.EndsWith(StandIn));
        if (wired && !upgrade)
        {
            // A re-run brings the devices' prefabs to today's layouts (call 21), writing only what differs, and (15 Oct) sets the
            // stage on the bench's real top if the first run put it into the wood; otherwise the scene is left as it is.
            string settled = SettleOnBenchTop(rigT, stage, oldMat, oldRest, oldTray, scene, report);
            string layouts = LayOutDevices(report, apply: true);
            Debug.Log(Tag + $"Already done: the stage is wired to {oldMat.name}, {oldRest.name} and {oldTray.name}. {settled} {layouts}");
            return;
        }

        Directory.CreateDirectory(folder);
        ItemInspector inspector = Object.FindAnyObjectByType<ItemInspector>(FindObjectsInactive.Include);
        Transform camera = rigT.Find("CM_InspectCam");
        Photo(folder, "01-before-close-up", camera);
        PhotoFromAbove(folder, "02-before-from-above", stageRoot);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(Title);
        int group = Undo.GetCurrentGroup();

        // The bench top, in the rig's frame: under the v2 slab (its bottom), else by a ray down through the rig.
        float benchTop = BenchTop(rigT, stage);
        report.AppendLine($"Bench top: rig-local y {benchTop:0.0000} ({rigT.TransformPoint(new Vector3(0f, benchTop, 0f)).y:0.000} m in the world).");

        // ---- the pieces (a re-run swapping stand-ins removes the old ones first) ----
        if (upgrade)
        {
            if (matFile && oldMat.name.EndsWith(StandIn)) { Undo.DestroyObjectImmediate(oldMat.gameObject); oldMat = null; }
            if (restFile && oldRest.name.EndsWith(StandIn)) { ParkTools(stageRoot, oldRest); Undo.DestroyObjectImmediate(oldRest.gameObject); oldRest = null; }
            if (trayFile && oldTray.name.EndsWith(StandIn)) { Undo.DestroyObjectImmediate(oldTray.gameObject); oldTray = null; }
        }
        Transform matT = oldMat != null ? oldMat : MakeMat(stageRoot, benchTop, matFile, report);
        Transform restT = oldRest != null ? oldRest : MakeRest(stageRoot, restFile, report);
        Transform trayT = oldTray != null ? oldTray : MakeTray(stageRoot, trayFile, report);
        // Where the phone's layout puts the rest and the tray (BenchStage moves them per device as each close-up opens).
        BenchLayout phone = BenchLayout.ForPhone;
        Undo.RecordObject(restT, "rest");
        restT.localPosition = new Vector3(phone.toolRestCentreX * .001f, benchTop + BenchStage.MatTop, MatZ + phone.toolRestCentreY * .001f);
        restT.localRotation = Quaternion.identity;
        Undo.RecordObject(trayT, "tray");
        trayT.localPosition = new Vector3(phone.scrapTray.x * .001f, benchTop + BenchStage.MatTop, MatZ + phone.scrapTray.y * .001f);
        trayT.localRotation = Quaternion.identity;

        Transform[] cups = Enumerable.Range(1, 6).Select(i => FindDeep(matT, "Cup_" + i)).ToArray();
        Transform padSmall = FindDeep(matT, "Pad_Small"), padLarge = FindDeep(matT, "Pad_Large");
        Transform wellSmall = FindDeep(matT, "Well_Small"), wellLarge = FindDeep(matT, "Well_Large");
        Transform[] slots = TraySlots.Select(s => FindDeep(trayT, s.name)).ToArray();
        // In ToolType order after Hand (brush, screwdriver, pry, tweezers, cloth), as BenchStage.ToolSlot reads them.
        var seats = new Transform[5];
        foreach (var seat in Seats) seats[(int)seat.tool - 1] = FindDeep(restT, seat.name);
        var missing = new List<string>();
        if (cups.Any(c => c == null)) missing.Add("a cup");
        if (padSmall == null || padLarge == null || wellSmall == null || wellLarge == null) missing.Add("a pad or a well");
        if (slots.Any(s => s == null)) missing.Add("a tray slot");
        if (seats.Any(s => s == null)) missing.Add("a seat");
        if (missing.Count > 0) Debug.LogError(Tag + "Missing empties (" + string.Join(", ", missing) + "): the FBX doesn't carry Art's names (contracts §8). The stage is wired with what is there.");

        // ---- the tools onto the rest's seats ----
        int toolsMoved = 0;
        foreach (ToolPickup tool in stageRoot.GetComponentsInChildren<ToolPickup>(true))
        {
            int index = (int)tool.tool - 1;
            if (index < 0 || index >= seats.Length || seats[index] == null) continue;
            if (tool.transform.parent == seats[index]) continue;
            Undo.SetTransformParent(tool.transform, seats[index], "tool to its seat");
            Undo.RecordObject(tool.transform, "tool pose");
            tool.transform.localPosition = Vector3.zero;
            tool.transform.localRotation = Quaternion.identity;
            toolsMoved++;
        }
        report.AppendLine($"Tools onto the rest's seats: {toolsMoved} moved.");
        string held = HeldCloth(stageRoot, report);

        // ---- the v2 pieces off (not deleted) ----
        // 23 Oct 2026: the v2 parts tray is a group (Bench 2: its floor, four dark walls and the magnet zone) and BenchStage keeps
        // only its floor as the legacy tray, so switching that off left the four walls standing on the new mat, a thick dark frame
        // round the cover well, over the mat's right edge and across the new-part pad (his note, 22 Oct 2026: "the tray on the
        // right ... kind of look off"). The whole group goes off. Bench 7b does the same in a scene this step has already built.
        Transform v2Tray = BenchStageTidySteps.OldTray(stage, stageRoot) ?? stage.LegacyTray;
        int off = 0;
        foreach (Transform old in new[] { stage.LegacyMat, v2Tray, stage.LegacyCaddy })
        {
            if (old == null || !old.gameObject.activeSelf) continue;
            Undo.RecordObject(old.gameObject, "v2 piece off");
            old.gameObject.SetActive(false);
            off++;
        }
        report.AppendLine($"The v2 slab, parts tray and caddy: {off} switched off (not deleted).");

        // ---- the stage wired, the inspect point on the mat's top ----
        Undo.RecordObject(stage, "wire the stage");
        stage.WireStage(matT, cups, padSmall, padLarge, wellSmall, wellLarge, trayT, slots, restT, seats);
        EditorUtility.SetDirty(stage);
        Transform point = rigT.Find("InspectPoint");
        if (point != null)
        {
            Undo.RecordObject(point, "inspect point");
            point.localPosition = new Vector3(0f, benchTop + BenchStage.MatTop, MatZ);
        }

        // ---- the frame check: Art's empties where the mat's frame says they are ----
        float worst = 0f;
        foreach (var (name, at) in MatEmpties)
        {
            Transform e = FindDeep(matT, name);
            if (e == null) continue;
            Vector3 expected = stage.MatToWorld(new Vector2(at.x * 1000f, at.z * 1000f), at.y);
            worst = Mathf.Max(worst, (e.position - expected).magnitude);
        }
        report.AppendLine($"The mat's frame against its empties: worst {worst * 1000f:0.0} mm (must be under 1 mm: the FBX came in with no turn).");
        if (worst > .001f) Debug.LogError(Tag + $"The mat's empties are {worst * 1000f:0.0} mm from where its frame puts them: the FBX was imported turned (contracts §8: no turn).");

        // ---- the bench's device slots off the mat ----
        string slotsMoved = MoveDeviceSlots(rigT, stage, report);

        // ---- the close-up's mouse settle: any spin lands square (Systems' decision 12) ----
        if (inspector != null)
        {
            var so = new SerializedObject(inspector);
            SerializedProperty snap = so.FindProperty("faceSnapWithin");
            if (snap != null && Mathf.Abs(snap.floatValue - 45f) > .01f)
            {
                Undo.RecordObject(inspector, "settle within 45");
                report.AppendLine($"ItemInspector.faceSnapWithin: {snap.floatValue:0} -> 45.");
                snap.floatValue = 45f;
                so.ApplyModifiedProperties();
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // ---- the devices' layouts (prefab data; not in the scene's Undo) ----
        string laid = LayOutDevices(report, apply: true);

        Photo(folder, "03-after-close-up", camera);
        PhotoFromAbove(folder, "04-after-from-above", stageRoot);
        report.AppendLine();
        report.AppendLine(slotsMoved);
        report.AppendLine(laid);
        report.AppendLine($"Stage: mat {matT.name}, rest {restT.name}, tray {trayT.name}{held}. Saved. Undo: Edit > Undo \"{Title}\" (the scene); the prefabs' layouts are data, set again by a re-run.");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        Debug.Log(Tag + $"Done: {matT.name}, {restT.name} and {trayT.name} under InspectRig/{StageName}, the tools on the rest, the v2 pieces off, the stage wired. {laid} Report and photos: {folder}\n{report}");
    }

    // ================================================================== the pieces

    static Transform MakeMat(Transform stageRoot, float benchTop, bool fromFile, StringBuilder report)
    {
        GameObject mat;
        if (fromFile)
        {
            mat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MatPath), stageRoot);
            mat.name = "BM_RepairMat";
            report.AppendLine("The mat: " + MatPath + ".");
        }
        else
        {
            mat = new GameObject("BM_RepairMat" + StandIn);
            mat.transform.SetParent(stageRoot, false);
            // A 600 x 360 x 5 mm slab in slate blue, its cups, pad and well marked, until Art's mat lands (blender-wave1.md §5 step 2).
            Material silicone = Mat("Assets/Art/Materials/Bench/BM_Silicone.mat") ?? Mat("Assets/Art/Materials/Bench/BM_Mat.mat");
            Material steel = Mat("Assets/Art/Materials/DC_SteelDark.mat"), pale = Mat("Assets/Art/Materials/DC_Chalk.mat");
            GameObject slab = Box("Slab", mat.transform, new Vector3(0f, BenchStage.MatTop * .5f, 0f), new Vector3(.6f, BenchStage.MatTop, .36f), silicone, true);
            slab.name = "Slab";
            foreach (var (name, at) in MatEmpties.Where(e => e.name.StartsWith("Cup_")))
                Disc(name + " disc", mat.transform, new Vector3(at.x, BenchStage.MatTop + .0002f, at.z), .018f, steel);
            Box("New-part pad", mat.transform, new Vector3(.130f, BenchStage.MatTop + .00015f, .001f), new Vector3(.080f, .0003f, .166f), pale, false);
            Box("Cover well", mat.transform, new Vector3(.222f, BenchStage.MatTop + .00015f, .001f), new Vector3(.080f, .0003f, .166f), steel, false);
            report.AppendLine($"The mat: a stand-in ({MatPath} isn't in the project yet): the slab, the cups, the pad and the well at Art's coordinates.");
        }
        Undo.RegisterCreatedObjectUndo(mat, "mat");
        mat.transform.localPosition = new Vector3(0f, benchTop, MatZ);
        mat.transform.localRotation = Quaternion.identity;
        mat.transform.localScale = Vector3.one;
        // Its solid for the cursor's ray (the surface point, the sight line): the slab, 5 mm. Nothing rests on it by physics.
        if (mat.GetComponent<Collider>() == null && mat.GetComponentInChildren<Collider>() == null)
        {
            var box = Undo.AddComponent<BoxCollider>(mat);
            box.center = new Vector3(0f, BenchStage.MatTop * .5f, 0f);
            box.size = new Vector3(.6f, BenchStage.MatTop, .36f);
        }
        foreach (var (name, at) in MatEmpties) EnsureEmpty(mat.transform, name, at, Vector3.zero, report);
        return mat.transform;
    }

    static Transform MakeRest(Transform stageRoot, bool fromFile, StringBuilder report)
    {
        GameObject rest;
        if (fromFile)
        {
            rest = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RestPath), stageRoot);
            rest.name = "BT_ToolRest";
            report.AppendLine("The tool rest: " + RestPath + ".");
        }
        else
        {
            rest = new GameObject("BT_ToolRest" + StandIn);
            rest.transform.SetParent(stageRoot, false);
            // Art A2: a steel plate 110 x 230, a near bar (16 mm tall, 20 mm in) and a far bar (9 mm, 120 mm in), a cork tray behind.
            Material steel = Mat("Assets/Art/Materials/DC_Steel.mat"), cork = Mat("Assets/Art/Materials/CC_Wood_Espresso.mat");
            Box("Plate", rest.transform, new Vector3(0f, .001f, 0f), new Vector3(.110f, .002f, .230f), steel, false);
            Box("Near bar", rest.transform, new Vector3(0f, .008f, -.115f + .020f), new Vector3(.110f, .016f, .004f), steel, false);
            Box("Far bar", rest.transform, new Vector3(0f, .0045f, -.115f + .120f), new Vector3(.110f, .009f, .004f), steel, false);
            Box("Cloth tray", rest.transform, new Vector3(0f, .003f, -.115f + .190f), new Vector3(.100f, .006f, .080f), cork, false);
            report.AppendLine($"The tool rest: a stand-in ({RestPath} isn't in the project yet), its seats at bench_tools.json's places.");
        }
        Undo.RegisterCreatedObjectUndo(rest, "rest");
        rest.transform.localScale = Vector3.one;
        foreach (var (name, _, at, euler) in Seats) EnsureEmpty(rest.transform, name, at, euler, report);
        return rest.transform;
    }

    static Transform MakeTray(Transform stageRoot, bool fromFile, StringBuilder report)
    {
        GameObject tray;
        if (fromFile)
        {
            tray = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrayPath), stageRoot);
            tray.name = "BT_ScrapTray";
            report.AppendLine("The old-parts tray: " + TrayPath + ".");
        }
        else
        {
            tray = new GameObject("BT_ScrapTray" + StandIn);
            tray.transform.SetParent(stageRoot, false);
            // Art A3: a shallow stainless tray 160 x 90 x 22, standing behind the device.
            Material steel = Mat("Assets/Art/Materials/DC_Steel.mat");
            Box("Floor", tray.transform, new Vector3(0f, .0004f, 0f), new Vector3(.160f, .0008f, .090f), steel, false);
            Box("Wall N", tray.transform, new Vector3(0f, .011f, .044f), new Vector3(.160f, .022f, .002f), steel, false);
            Box("Wall S", tray.transform, new Vector3(0f, .011f, -.044f), new Vector3(.160f, .022f, .002f), steel, false);
            Box("Wall E", tray.transform, new Vector3(.079f, .011f, 0f), new Vector3(.002f, .022f, .090f), steel, false);
            Box("Wall W", tray.transform, new Vector3(-.079f, .011f, 0f), new Vector3(.002f, .022f, .090f), steel, false);
            report.AppendLine($"The old-parts tray: a stand-in ({TrayPath} isn't in the project yet), its slots at bench_tools.json's places.");
        }
        Undo.RegisterCreatedObjectUndo(tray, "tray");
        tray.transform.localScale = Vector3.one;
        foreach (var (name, at) in TraySlots) EnsureEmpty(tray.transform, name, at, Vector3.zero, report);
        return tray.transform;
    }

    // The held cloth (Art A4): a second model under the cloth, hidden at rest, shown while it is in the hand (ToolPickup).
    static string HeldCloth(Transform stageRoot, StringBuilder report)
    {
        ToolPickup cloth = stageRoot.GetComponentsInChildren<ToolPickup>(true).FirstOrDefault(t => t.tool == ToolType.Cloth);
        if (cloth == null) return "";
        if (cloth.transform.Find("BT_ClothHeld") != null) return ", the held cloth already in";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ClothHeldPath);
        if (asset == null)
        {
            report.AppendLine($"The held cloth: {ClothHeldPath} isn't in the project yet; the folded cloth is held as it is until it lands (re-run this step then).");
            return "";
        }
        var held = (GameObject)PrefabUtility.InstantiatePrefab(asset, cloth.transform);
        Undo.RegisterCreatedObjectUndo(held, "held cloth");
        held.name = "BT_ClothHeld";
        held.transform.localPosition = Vector3.zero;
        held.transform.localRotation = Quaternion.identity;
        held.transform.localScale = Vector3.one;
        held.SetActive(false);
        report.AppendLine("The held cloth: " + ClothHeldPath + " under the cloth, hidden at rest.");
        return ", the held cloth";
    }

    // A stand-in rest swapped for the model: its tools wait under the stage meanwhile (they are moved onto the new seats).
    static void ParkTools(Transform stageRoot, Transform rest)
    {
        foreach (ToolPickup tool in rest.GetComponentsInChildren<ToolPickup>(true))
            Undo.SetTransformParent(tool.transform, stageRoot, "park the tool");
    }

    // An empty of Art's at its place: kept when the model carries it (and checked), made when it doesn't (a stand-in).
    static void EnsureEmpty(Transform root, string name, Vector3 at, Vector3 euler, StringBuilder report)
    {
        Transform e = FindDeep(root, name);
        if (e != null)
        {
            Vector3 local = root.InverseTransformPoint(e.position);
            if ((local - at).magnitude > .001f) report.AppendLine($"  {root.name}/{name} is {(local - at).magnitude * 1000f:0.0} mm from the manifest's place (kept where the model has it).");
            return;
        }
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(euler);
    }

    // ================================================================== the bench's device slots, off the mat (the TD's §6.9)

    static string MoveDeviceSlots(Transform rigT, BenchStage stage, StringBuilder report)
    {
        Transform workbench = rigT.parent;
        Physics.SyncTransforms();     // the new mat's collider, for the rays below
        int moved = 0;
        var lines = new List<string>();
        foreach (var (name, mm) in DeviceSlots)
        {
            Transform slot = workbench != null ? FindDeep(workbench, name) : null;
            if (slot == null) { lines.Add($"{name} not found under the Workbench"); continue; }
            Vector3 to = stage.MatToWorld(mm, 0f);    // the bench top (the mat's origin plane), in front of the mat
            // A bench under it? A ray down from 10 cm up must meet the bench's top within 15 mm of the mat's base.
            bool bench = Physics.Raycast(to + Vector3.up * .1f, Vector3.down, out RaycastHit hit, .3f, ~0, QueryTriggerInteraction.Ignore)
                         && Mathf.Abs(hit.point.y - to.y) < .015f;
            if ((slot.position - to).magnitude < .001f) { lines.Add($"{name} already at mat ({mm.x:0}, {mm.y:0})"); continue; }
            Undo.RecordObject(slot, "device slot off the mat");
            slot.position = to;
            slot.rotation = rigT.rotation;
            moved++;
            lines.Add($"{name} to mat ({mm.x:0}, {mm.y:0}){(bench ? "" : " (NO bench top found under it: Environment to confirm)")}");
            if (!bench) Debug.LogWarning(Tag + $"{name}: no bench top within 15 mm under mat ({mm.x:0}, {mm.y:0}); the TD's §6.9 asks Environment to confirm the bench's depth there.");
        }
        report.AppendLine("The bench's device slots: " + string.Join("; ", lines) + ".");
        return $"Device slots moved off the mat: {moved}.";
    }

    // ================================================================== the devices' layouts (prefab data)

    static string LayOutDevices(StringBuilder report, bool apply)
    {
        int set = 0, already = 0, missing = 0;
        foreach (var (path, kind) in Devices)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { missing++; continue; }
            BenchLayout want = kind == "camera" ? BenchLayout.ForCamera : kind == "watch" ? BenchLayout.ForWatch : BenchLayout.ForPhone;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                DeviceDefinition definition = root.GetComponent<DeviceDefinition>();
                if (definition == null) { missing++; continue; }
                var so = new SerializedObject(definition);
                SerializedProperty rest = so.FindProperty("benchLayout.rest"), x = so.FindProperty("benchLayout.toolRestCentreX"),
                    y = so.FindProperty("benchLayout.toolRestCentreY"), tray = so.FindProperty("benchLayout.scrapTray");
                if (rest == null || x == null || y == null || tray == null) { missing++; continue; }
                bool same = (rest.vector2Value - want.rest).sqrMagnitude < 1e-4f && Mathf.Abs(x.floatValue - want.toolRestCentreX) < .01f
                            && Mathf.Abs(y.floatValue - want.toolRestCentreY) < .01f && (tray.vector2Value - want.scrapTray).sqrMagnitude < 1e-4f;
                if (same) { already++; continue; }
                if (!apply) continue;
                rest.vector2Value = want.rest;
                x.floatValue = want.toolRestCentreX;
                y.floatValue = want.toolRestCentreY;
                tray.vector2Value = want.scrapTray;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{path}: the {kind}'s layout ({want}).");
                set++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return apply ? $"Device layouts: {set} set, {already} already right{(missing > 0 ? $", {missing} not found" : "")}."
                     : $"Device layouts: {already} of {Devices.Length} already right{(already < Devices.Length ? " (run the step after a stand-in swap, or check the prefabs)" : "")}.";
    }

    // ================================================================== helpers

    // The Workbench's real top, in the rig's frame: the highest solid a ray down through the rig meets, past the rig's own
    // pieces (the mat, the stage, the inspect point) and any device. 15 Oct: the first runs took the v2 slab's bottom, but the
    // slab sat 6 mm into the Workbench's top, so the mat's top (5.8 mm) ended 0.2 mm under the wood (1.006 m) and the two
    // flickered through each other in play (Bench 3: "the mat's bottom -0.6 cm above it"). The slab is only the fallback now.
    static float BenchTop(Transform rigT, BenchStage stage)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(rigT.position + Vector3.up * .05f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(rigT) || h.collider.GetComponentInParent<JobBase>() != null) continue;
            best = Mathf.Max(best, h.point.y);
        }
        if (best > float.NegativeInfinity) return rigT.InverseTransformPoint(new Vector3(rigT.position.x, best, rigT.position.z)).y;
        Transform slab = stage.LegacyMat;
        if (slab != null)
        {
            // The v2 slab lay in the bench top: its bottom (it is a cube scaled to 0.6 x 0.006 x 0.36).
            Vector3 bottom = slab.position - slab.up * (Mathf.Abs(slab.lossyScale.y) * .5f);
            return rigT.InverseTransformPoint(bottom).y;
        }
        return -.35f;
    }

    // A re-run sets the stage on the bench's real top when it isn't there (the first runs put it 6 mm into the wood): the mat
    // (its origin is its bottom), the rest, the tray and the inspect point move together, then the device slots follow the
    // mat. Everything else (the cups, the pads, the seats, the tools, the held cloth) is under those and moves with them.
    static string SettleOnBenchTop(Transform rigT, BenchStage stage, Transform matT, Transform restT, Transform trayT, Scene scene, StringBuilder report)
    {
        float top = BenchTop(rigT, stage);
        float matBase = rigT.InverseTransformPoint(matT.position).y;
        float dy = top - matBase;
        if (Mathf.Abs(dy) < .0005f) return "The stage sits on the bench top.";
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(Title + " - onto the bench top");
        int group = Undo.GetCurrentGroup();
        Vector3 lift = rigT.up * dy;
        foreach (Transform t in new[] { matT, restT, trayT, rigT.Find("InspectPoint") })
        {
            if (t == null) continue;
            Undo.RecordObject(t, "onto the bench top");
            t.position += lift;
        }
        string slots = MoveDeviceSlots(rigT, stage, report);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string line = $"The stage moved {dy * 1000f:+0.0;-0.0} mm onto the bench top (rig-local y {top:0.0000}, {rigT.TransformPoint(new Vector3(0f, top, 0f)).y:0.000} m in the world): " +
                      $"the mat, the rest, the tray and the inspect point; {slots} Saved (Undo: Edit > Undo \"{Title} - onto the bench top\").";
        report.AppendLine(line);
        return line;
    }

    static Transform Child(Transform parent, string name)
    {
        foreach (Transform t in parent) if (t.name == name) return t;
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

    // A box under an unscaled parent (a child scaled, never a parent: a turned child of a stretched cube comes out sheared).
    static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localScale = size;
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Disc(string name, Transform parent, Vector3 at, float diameter, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localScale = new Vector3(diameter, .0002f, diameter);
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // A photo from the close-up camera's place (FOV 40, 1920 x 1080), and one from above the mat.
    static void Photo(string folder, string name, Transform view)
    {
        if (view == null) return;
        Shoot(folder, name, view.position, view.rotation, 40f);
    }

    static void PhotoFromAbove(string folder, string name, Transform stageRoot)
    {
        Vector3 middle = stageRoot.TransformPoint(new Vector3(0f, -.3f, MatZ));
        Shoot(folder, name, middle + Vector3.up * .75f, Quaternion.LookRotation(Vector3.down, stageRoot.forward), 45f);
    }

    static void Shoot(string folder, string name, Vector3 at, Quaternion turn, float fov)
    {
        Directory.CreateDirectory(folder);
        var go = new GameObject("Temporary bench photo camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(at, turn);
            cam.fieldOfView = fov;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 60f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
        }
        catch (Exception e) { Debug.LogWarning(Tag + "A photo failed: " + e.Message); }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    // ================================================================== Bench 8: the clumsy hand (§5)

    [MenuItem(Menu + "Bench 8 - The clumsy hand (lab, drives itself)")]
    static void ClumsyHandLab() => StartClumsyHand(false);

    [MenuItem(Menu + "Bench 8 - The clumsy hand, full (lab, three seeds each: a release candidate)")]
    static void ClumsyHandLabFull() => StartClumsyHand(true);

    // A lab session on a fresh Day 1 (the lab's own save; his playtest save is not touched) that plays the four repairs with
    // the clumsy hand, by mouse and by pad, at 60 and at 15 fps, and writes Logs/BenchClumsy/<time>/.
    static void StartClumsyHand(bool full)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Bench 8] Stop Play first."); return; }
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError("[Bench 8] Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        BenchStage stage = Object.FindAnyObjectByType<BenchStage>(FindObjectsInactive.Include);
        if (stage == null || stage.matFrame == null || stage.scrapTray == null || stage.toolRest == null)
        {
            Debug.LogError("[Bench 8] The bench has no repair mat, old-parts tray or tool rest yet: run Fixit Fidget > Bench > Bench 7 - The forgiving stage (scene) first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(ClumsyBenchLab.PendingKey, 1);
        PlayerPrefs.SetInt(ClumsyBenchLab.FullKey, full ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[Bench 8] The clumsy hand{(full ? ", full" : "")}: {(full ? 48 : 16)} runs (the camera's clean and swap, the phone's screen, the watch's seized " +
                  "movement; mouse and pad; 60 and 15 fps). Keep the Game view in front and leave the mouse, the keyboard and any pad alone until it " +
                  $"reports (your mouse is switched off for the mouse runs and comes back after). About {(full ? "45" : "15")} minutes. Test save {path}.");
        EditorApplication.EnterPlaymode();
    }

    // A lab asked for that never became a Play session must not run in the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleClumsyRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.GetInt(ClumsyBenchLab.PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(ClumsyBenchLab.PendingKey);
        PlayerPrefs.DeleteKey(ClumsyBenchLab.FullKey);
        PlayerPrefs.Save();
    }

    // ================================================================== the rules check

    [MenuItem("Fixit Fidget/Checks/Bench rules")]
    static void RulesCheck()
    {
        try { Debug.Log($"[Bench rules] PASS: {BenchRules.SelfCheck()} assertions (the hit areas, the flight, the slot rule, the press pass, the finisher, the stroke speed)."); }
        catch (Exception e) { Debug.LogError("[Bench rules] " + e.Message); }
    }
}
#endif
