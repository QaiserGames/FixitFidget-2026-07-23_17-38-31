#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// FAULT EXPANSION — the content pass, 2026-08-27
//
// WHY THIS EXISTS
//
// Three logged days established that every job in the game is the same job.
// Both devices carry exactly one fault, and each fault has exactly ONE task,
// which has two consequences that block most of the roadmap:
//
//   1. Quality is (done / total), so with one task it can only ever be 0 or 1.
//      Grade thresholds are Perfect >= 0.999, Good >= 0.66, Passable > 0. With
//      a single task, Good and Passable are ARITHMETICALLY unreachable — twelve
//      Perfects across three logged days, and not one of anything else.
//      Three tasks is the smallest number that reaches all four grades.
//
//   2. A wave of arrivals is only a pressure curve if the jobs differ in
//      length. Six identical 60-second teardowns is a queue.
//
// WHAT IT BUILDS
//
//   Pocket Watch   Debris Inside    Cleaning     1 grime            $75
//                  Seized Movement  Mechanical   2 grime + 1 part   $160
//   Phone          Cracked Screen   Mechanical   1 part             $110
//                  Gunked Up        Cleaning     3 grime            $45
//
// Payouts are real-world: a screen swap is $90-150 and a full watch overhaul
// is $150-250 at an independent shop. Drinks were already realistic at $5.
//
// WHAT IT DOESN'T DO
//
// Placement. New grime spots land offset from the original so you can see
// them; you drag them onto the mesh yourself. Code does the wiring, you do the
// eyeballing.
//
// ⚠️ PREFAB EDITS ARE NOT UNDOABLE. Commit before running. The tool is
// idempotent — running twice does nothing the second time — but Ctrl+Z will
// not take these back.
// ---------------------------------------------------------------------------

public static class FaultExpansionSetup
{
    private const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab";
    private const string WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab";

    // Marker names. Also how the tool knows it has already run.
    private const string GrimePrefix = "Grime_Extra_";
    private const string PartPrefix  = "Part_Extra_";

    [MenuItem("Fixit Fidget/Content/1 · Preview fault expansion")]
    public static void Preview()
    {
        List<string> report = new();

        foreach (string path in new[] { WatchPath, PhonePath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { report.Add($"MISSING: {path}"); continue; }

            try
            {
                DeviceDefinition def = root.GetComponent<DeviceDefinition>();
                if (def == null) { report.Add($"{root.name}: no DeviceDefinition"); continue; }

                report.Add($"— {root.name} ({def.displayName}) —");
                report.Add($"   grime spots in prefab: {root.GetComponentsInChildren<GrimeSpot>(true).Length}");
                report.Add($"   replaceable parts:     {root.GetComponentsInChildren<ReplaceablePart>(true).Length}");

                if (def.faults != null)
                {
                    foreach (DeviceFault f in def.faults)
                    {
                        int n = f.enableObjects != null ? f.enableObjects.Count(o => o != null) : 0;
                        report.Add($"   fault: \"{f.description}\"  type={f.type}  tasks={n}  ${f.payout}");
                    }
                }

                bool already = root.GetComponentsInChildren<Transform>(true)
                                   .Any(t => t.name.StartsWith(GrimePrefix) || t.name.StartsWith(PartPrefix));
                report.Add(already ? "   ALREADY EXPANDED — step 2 would do nothing."
                                   : "   not expanded yet.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        string text = string.Join("\n", report);
        Debug.Log("[Fault expansion — preview]\n" + text);
        EditorUtility.DisplayDialog("Fault expansion — preview", text, "OK");
    }

    [MenuItem("Fixit Fidget/Content/2 · Build fault variants")]
    public static void Build()
    {
        bool go = EditorUtility.DisplayDialog(
            "Build fault variants",
            "This edits PhoneRepair.prefab and PocketWatch.prefab.\n\n" +
            "PREFAB EDITS CANNOT BE UNDONE WITH CTRL+Z.\n\n" +
            "Commit to git first if you haven't.\n\n" +
            "Safe to run twice — it does nothing the second time.",
            "Build it", "Cancel");

        if (!go) return;

        List<string> log = new();

        BuildWatch(log);
        BuildPhone(log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string text = string.Join("\n", log);
        Debug.Log("[Fault expansion — build]\n" + text);
        EditorUtility.DisplayDialog("Fault expansion", text +
            "\n\nNEXT: open each prefab and drag the new Grime_Extra_* objects " +
            "onto the mesh. They're sitting offset from the original so you can " +
            "find them.", "OK");
    }

    // ---------------- pocket watch ----------------

    private static void BuildWatch(List<string> log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WatchPath);
        if (root == null) { log.Add("MISSING PocketWatch.prefab"); return; }

        try
        {
            DeviceDefinition def = root.GetComponent<DeviceDefinition>();
            if (def == null) { log.Add("PocketWatch: no DeviceDefinition — skipped"); return; }

            if (AlreadyExpanded(root)) { log.Add("PocketWatch: already expanded, skipped."); return; }

            GrimeSpot original = root.GetComponentsInChildren<GrimeSpot>(true).FirstOrDefault();
            if (original == null) { log.Add("PocketWatch: no GrimeSpot to copy — skipped"); return; }

            // CONFIRMED BY THE PREVIEW, 2026-08-27: the watch has NO ReplaceablePart
            // of its own. Two grime spots would cap Seized Movement at 2 tasks,
            // which reaches Passable but not Good — and Good is the whole reason
            // for this pass. So the mainspring is copied across from the phone's
            // screen part.
            //
            // ⚠️ It arrives wearing the phone screen's MESH. That's a deliberate
            // placeholder: a job that plays right with the wrong shape beats one
            // that plays flat with the right shape. Swap the mesh in Blender when
            // convenient — nothing in the logic depends on it.
            ReplaceablePart partSource = root.GetComponentsInChildren<ReplaceablePart>(true).FirstOrDefault()
                                      ?? BorrowPartFromPhone();

            // Fault 0 — the existing one. It was labelled Mechanical but its
            // only task is a grime spot, so the log has been reporting both
            // devices as Mechanical and telling us nothing.
            DeviceFault clean = def.faults != null && def.faults.Length > 0 ? def.faults[0] : new DeviceFault();
            clean.type = FaultType.Cleaning;
            clean.description = "Debris Inside, Won't Tell Time";
            clean.payout = 75;
            clean.enableObjects = new[] { original.gameObject };

            // Fault 1 — the long one. Three tasks, so every grade is reachable.
            GameObject g1 = Duplicate(original.gameObject, GrimePrefix + "Watch_1", new Vector3(0.012f, 0f, 0.008f));
            GameObject g2 = Duplicate(original.gameObject, GrimePrefix + "Watch_2", new Vector3(-0.010f, 0f, -0.009f));

            GameObject mainspring = null;
            if (partSource != null)
            {
                Transform parent = original.transform.parent != null
                                 ? original.transform.parent : root.transform;

                mainspring = Object.Instantiate(partSource.gameObject, parent);
                mainspring.name = PartPrefix + "Mainspring";
                mainspring.transform.localPosition = original.transform.localPosition + new Vector3(0f, 0.006f, 0f);
                mainspring.transform.localRotation = Quaternion.identity;
                mainspring.SetActive(false);

                // The copy came from the phone, so its coveredBy still points at
                // the PHONE's cover — an object that doesn't exist in this
                // prefab. Left alone, the mainspring would be interactable with
                // the watch case still screwed shut. Re-point it at this
                // device's own cover.
                RemovablePart ownCover = root.GetComponentsInChildren<RemovablePart>(true).FirstOrDefault();

                SerializedObject so = new SerializedObject(mainspring.GetComponent<ReplaceablePart>());
                so.FindProperty("coveredBy").objectReferenceValue = ownCover;
                so.FindProperty("partName").stringValue = "Mainspring";
                so.ApplyModifiedPropertiesWithoutUndo();

                if (ownCover == null)
                    log.Add("   ⚠ no RemovablePart on the watch — the mainspring will be " +
                            "reachable without opening the case.");
            }

            List<GameObject> seizedTasks = new() { g1, g2 };
            if (mainspring != null) seizedTasks.Add(mainspring);

            DeviceFault seized = new DeviceFault
            {
                type = FaultType.Mechanical,
                description = "Seized Movement",
                payout = 160,
                enableObjects = seizedTasks.ToArray()
            };

            def.faults = new[] { clean, seized };

            PrefabUtility.SaveAsPrefabAsset(root, WatchPath);

            log.Add("PocketWatch:");
            log.Add("   fault 0 \"Debris Inside\"   Cleaning    1 task    $75  (type corrected)");
            log.Add($"   fault 1 \"Seized Movement\" Mechanical  {seizedTasks.Count} tasks   $160");
            if (mainspring == null)
                log.Add("   ⚠ couldn't source a ReplaceablePart from either prefab — Seized " +
                        "Movement has 2 tasks, which reaches Passable but NOT Good.");
            else
                log.Add("   note: the mainspring wears the phone screen's mesh. Placeholder — " +
                        "swap it in Blender whenever. Logic doesn't care.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------- phone ----------------

    private static void BuildPhone(List<string> log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PhonePath);
        if (root == null) { log.Add("MISSING PhoneRepair.prefab"); return; }

        try
        {
            DeviceDefinition def = root.GetComponent<DeviceDefinition>();
            if (def == null) { log.Add("PhoneRepair: no DeviceDefinition — skipped"); return; }

            if (AlreadyExpanded(root)) { log.Add("PhoneRepair: already expanded, skipped."); return; }

            ReplaceablePart screen = root.GetComponentsInChildren<ReplaceablePart>(true).FirstOrDefault();
            if (screen == null) { log.Add("PhoneRepair: no ReplaceablePart — skipped"); return; }

            DeviceFault cracked = def.faults != null && def.faults.Length > 0 ? def.faults[0] : new DeviceFault();
            cracked.type = FaultType.Mechanical;
            cracked.description = "Cracked Screen";
            cracked.payout = 110;
            cracked.enableObjects = new[] { screen.gameObject };

            // The phone has no grime spot to copy, so borrow one from the watch.
            GrimeSpot template = LoadGrimeTemplate();
            if (template == null)
            {
                def.faults = new[] { cracked };
                PrefabUtility.SaveAsPrefabAsset(root, PhonePath);
                log.Add("PhoneRepair: repriced to $110, but no GrimeSpot anywhere to copy — " +
                        "\"Gunked Up\" not built.");
                return;
            }

            Transform parent = screen.transform.parent != null ? screen.transform.parent : root.transform;

            GameObject p1 = Instantiate(template.gameObject, parent, GrimePrefix + "Phone_1", screen.transform.localPosition + new Vector3(0.018f, 0.002f, 0.020f));
            GameObject p2 = Instantiate(template.gameObject, parent, GrimePrefix + "Phone_2", screen.transform.localPosition + new Vector3(-0.016f, 0.002f, 0.004f));
            GameObject p3 = Instantiate(template.gameObject, parent, GrimePrefix + "Phone_3", screen.transform.localPosition + new Vector3(0.002f, 0.002f, -0.022f));

            DeviceFault gunked = new DeviceFault
            {
                type = FaultType.Cleaning,
                description = "Gunked Up, Won't Charge",
                payout = 45,
                enableObjects = new[] { p1, p2, p3 }
            };

            def.faults = new[] { cracked, gunked };

            PrefabUtility.SaveAsPrefabAsset(root, PhonePath);

            log.Add("PhoneRepair:");
            log.Add("   fault 0 \"Cracked Screen\"  Mechanical  1 task    $110");
            log.Add("   fault 1 \"Gunked Up\"       Cleaning    3 tasks   $45");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------- helpers ----------------

    private static bool AlreadyExpanded(GameObject root) =>
        root.GetComponentsInChildren<Transform>(true)
            .Any(t => t.name.StartsWith(GrimePrefix) || t.name.StartsWith(PartPrefix));

    // Copy within the same prefab. Unity re-targets references that point
    // inside the copied subtree and preserves ones pointing outside it, so a
    // duplicated ReplaceablePart keeps its own visuals AND its coveredBy link.
    private static GameObject Duplicate(GameObject source, string name, Vector3 offset)
    {
        GameObject copy = Object.Instantiate(source, source.transform.parent);
        copy.name = name;
        copy.transform.localPosition = source.transform.localPosition + offset;
        copy.transform.localRotation = source.transform.localRotation;
        copy.transform.localScale = source.transform.localScale;
        copy.SetActive(false);          // ApplyFault turns on whichever fault is rolled
        return copy;
    }

    private static GameObject Instantiate(GameObject source, Transform parent, string name, Vector3 localPos)
    {
        GameObject copy = Object.Instantiate(source, parent);
        copy.name = name;
        copy.transform.localPosition = localPos;
        copy.transform.localRotation = Quaternion.identity;
        copy.SetActive(false);
        return copy;
    }

    // The phone has no grime of its own, so pull one out of the watch prefab.
    private static GrimeSpot LoadGrimeTemplate()
    {
        GameObject watch = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
        return watch != null ? watch.GetComponentInChildren<GrimeSpot>(true) : null;
    }

    // ...and the watch has no replaceable part, so borrow the phone's screen.
    private static ReplaceablePart BorrowPartFromPhone()
    {
        GameObject phone = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
        return phone != null ? phone.GetComponentInChildren<ReplaceablePart>(true) : null;
    }

    [MenuItem("Fixit Fidget/Content/3 · Check")]
    public static void Check()
    {
        List<string> report = new();

        foreach (string path in new[] { WatchPath, PhonePath })
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { report.Add($"MISSING {path}"); continue; }

            DeviceDefinition def = root.GetComponent<DeviceDefinition>();
            if (def == null || def.faults == null) { report.Add($"{root.name}: no faults"); continue; }

            report.Add($"— {root.name} —");
            foreach (DeviceFault f in def.faults)
            {
                int tasks = 0;
                if (f.enableObjects != null)
                {
                    foreach (GameObject g in f.enableObjects)
                    {
                        if (g == null) continue;
                        if (g.GetComponent<GrimeSpot>() != null || g.GetComponent<ReplaceablePart>() != null) tasks++;
                    }
                }

                string reach = tasks >= 3 ? "all four grades"
                             : tasks == 2 ? "Perfect / Passable only"
                             : tasks == 1 ? "Perfect or Rejected only"
                                          : "no tasks (Human family)";

                report.Add($"   \"{f.description}\"  {f.type}  {tasks} tasks  ${f.payout}   -> {reach}");
            }
        }

        string text = string.Join("\n", report);
        Debug.Log("[Fault expansion — check]\n" + text);
        EditorUtility.DisplayDialog("Fault expansion — check", text, "OK");
    }
}
#endif
