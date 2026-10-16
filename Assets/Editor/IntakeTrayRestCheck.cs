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
// THE INTAKE TRAY CHECK: EVERY DEVICE RESTS ON THE FELT (one scale, 14 Oct 2026; specs/td-one-scale.md §7, stage 3)
//
//   Fixit Fidget > Checks > Intake tray - every device rests on the felt (read-only)
//
// A customer's device is set on an intake slot at JobBase.restHeight over the slot point (PlacementJitter), and the tray's
// felt lies over that point (3.0 mm on 10 Oct: Playtest4TraySteps). At real size the devices' lowest points move (the watch's
// back screws were 21.7 mm under its anchor at 3x, 7.2 at real size), so Devices 2 sets each rest height to put the lowest
// point 3.3 mm over the slot point. This check measures it in the café scene, the felt's top off the tray's own mesh: each
// bench device must rest 0–1.2 mm over the felt (the TD's bar), not hover and not sink into it. Run it after Devices 2.
// Read-only: nothing in the scene or the prefabs changes. Report: Logs/Bench/intake-tray-rest-<time>/report.txt.
// ---------------------------------------------------------------------------
internal static class IntakeTrayRestCheck
{
    const string Menu = "Fixit Fidget/Checks/Intake tray - every device rests on the felt (read-only)";
    const string TrayManifest = "Assets/Art/Models/CafeKit/cafe_intake.json";
    const string FeltName = "CF_Felt";
    static readonly string[] DeviceFolders = { "Assets/AssetsPrefabs", "Assets/GraceShowcase" };

    [MenuItem(Menu)]
    static void Check()
    {
        var run = new BenchScale.Run("Intake tray check: every device rests on the felt (read-only)", "intake-tray-rest");
        bool error = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (SceneManager.GetActiveScene().path != BenchScale.CafeScene) throw new InvalidOperationException($"Open the café scene ({BenchScale.CafeScene}) first: the felt is measured in it.");
            IntakeShelf[] shelves = Object.FindObjectsByType<IntakeShelf>(FindObjectsInactive.Include);
            if (shelves.Length != 1) throw new InvalidOperationException($"Expected one IntakeShelf in the café scene, found {shelves.Length}.");
            ItemSlotArea area = shelves[0].GetComponent<ItemSlotArea>();
            if (area == null) throw new InvalidOperationException("The intake shelf has no ItemSlotArea.");
            SerializedProperty slots = new SerializedObject(area).FindProperty("slots");
            Transform slot = slots != null && slots.arraySize > 0 ? slots.GetArrayElementAtIndex(0).objectReferenceValue as Transform : null;
            if (slot == null) throw new InvalidOperationException("The intake's ItemSlotArea has no slot 0.");

            float feltOverSlot = Felt(slot, run);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", DeviceFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                JobBase job = root != null ? root.GetComponent<JobBase>() : null;
                if (job == null || job is DrinkJob) continue;
                float bottom = BenchScale.Bottom(root), over = job.restHeight + bottom - feltOverSlot;
                DeviceDefinition def = root.GetComponent<DeviceDefinition>();
                string line = $"{root.name}: {over * 1000f:+0.0;-0.0} mm over the felt (rest height {job.restHeight:0.0000} m, its lowest point {BenchScale.Mm(-bottom)} under its anchor" +
                              $"{(def != null && def.realSize ? ", real size" : "")})";
                if (BenchScale.Devices.Any(d => d.prefab == path))
                    run.Check(over >= BenchScale.FeltBandLow && over <= BenchScale.FeltBandHigh, line + " — 0 to 1.2 mm wanted");
                else run.Info(line + " (not one of the bench's three devices: reported only)");
            }
        }
        catch (Exception e)
        {
            error = true;
            run.Info("ERROR " + e.Message);
        }
        run.Finish(error ? "ERROR: stopped (see above)." : $"{run.Passes} PASS, {run.Fails} FAIL (read-only).", error || run.Fails > 0);
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // The felt's top over slot 0's point: the felt submesh's highest vertex when the tray's mesh is readable (Tray 1 makes it so),
    // else the tray's bottom plus its manifest's felt_top, else the 10 Oct number (3.0 mm).
    static float Felt(Transform slot, BenchScale.Run run)
    {
        IntakeTray tray = slot.GetComponentInChildren<IntakeTray>(true);
        MeshRenderer r = tray != null ? tray.GetComponentInChildren<MeshRenderer>(true) : null;
        MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null)
        {
            Mesh mesh = mf.sharedMesh;
            int sub = Array.FindIndex(r.sharedMaterials, look => look != null && look.name == FeltName);
            if (mesh.isReadable && sub >= 0 && sub < mesh.subMeshCount)
            {
                Vector3[] v = mesh.vertices;
                float top = mesh.GetIndices(sub).Select(i => mf.transform.TransformPoint(v[i]).y).Max();
                run.Info($"the felt (slot 0's tray, {tray.name}): its top {1000f * (top - slot.position.y):+0.0;-0.0} mm against the slot point, read off its mesh");
                return top - slot.position.y;
            }
            string manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TrayManifest));
            if (File.Exists(manifest)
                && BenchScale.Json(File.ReadAllText(manifest)) is Dictionary<string, object> doc && doc.TryGetValue("pieces", out object list) && list is List<object> pieces)
                foreach (object p in pieces)
                    if (p is Dictionary<string, object> piece && piece.TryGetValue("name", out object n) && (n as string) == "CK_IntakeTray" && piece.TryGetValue("felt_top", out object ft) && ft is double feltTop)
                    {
                        float top = r.bounds.min.y + (float)feltTop;
                        run.Info($"the felt (slot 0's tray): {1000f * (top - slot.position.y):+0.0;-0.0} mm against the slot point (its bottom + the manifest's felt_top; the mesh isn't readable)");
                        return top - slot.position.y;
                    }
        }
        run.Info($"the felt: no tray under slot 0 to measure: taken as {BenchScale.FeltOverSlot * 1000f:0.0} mm over the slot point (10 Oct)");
        return BenchScale.FeltOverSlot;
    }
}
#endif
