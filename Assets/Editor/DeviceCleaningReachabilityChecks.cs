#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// GDD v4 6.6: never hide a required action behind geometry.
//
// Grace's reunion camera once shipped with its lens grime inside the lens glass's capsule collider, where the old bench's
// view ray (it scrubbed only what the ray hit first) could never reach it, so the camera could never reach Perfect.
//
// THE FORGIVING BENCH (14 Oct 2026; systems-forgiving-bench.md §4.6, §6): what reaches a spot now is the picker and the
// press pass, not a ray. A spot is reachable when the picker can make it eligible: not under a seated cover once the device is
// opened, and its face toward the close-up's camera (58° down the mat, within 100° of the line to it) at some quarter turn,
// face up or after one flip (the step to do now's own targets are hit through anything in front of them, so a collider in
// the way no longer hides one). And it must come clean the way a hand cleans it: strokes of its own tool's footprint across
// it, through the pass-based erase (GrimeSpot.Pass), within 20 strokes.
//
// Every grime spot (and every smudge) of every fault of every device prefab, with covers and screws out of the way as they
// are once the device has been opened. Preview scenes only.
public static class DeviceCleaningReachabilityChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Every device's grime can be brushed")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");

        var failures = new List<string>();
        var devices = new List<string>();
        int spots = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var definition = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
            if (definition == null || prefab.GetComponentsInChildren<GrimeSpot>(true).Length == 0) continue;
            devices.Add(prefab.name);

            int faults = definition.faults != null && definition.faults.Length > 0 ? definition.faults.Length : 1;
            for (int fault = 0; fault < faults; fault++)
            {
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var host = new GameObject("Cleaning reachability");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    var device = UnityEngine.Object.Instantiate(prefab, host.transform);
                    device.transform.SetPositionAndRotation(new Vector3(5, 5, 5), Quaternion.identity);
                    device.GetComponent<DeviceDefinition>().ApplyFault(fault);
                    var grimeSpots = device.GetComponentsInChildren<GrimeSpot>();
                    Open(device, grimeSpots);
                    foreach (var grime in grimeSpots)
                    {
                        if (grime == null || !grime.gameObject.activeInHierarchy) continue;   // another fault's spot
                        spots++;
                        string problem = CleanByStrokes(device, grime);
                        if (problem != null) failures.Add($"{prefab.name} (fault {fault}) '{grime.name}': {problem}");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException("[Device cleaning] FAIL: grime the bench cannot reach or clean:\n  " + string.Join("\n  ", failures));
        Debug.Log($"[Device cleaning] PASS: {spots} grime spots across {devices.Count} devices ({string.Join(", ", devices)}) "
            + "are each eligible in the bench's picker (uncovered once opened, facing the close-up's camera at some quarter turn, face up or turned over) "
            + "and come clean by strokes of their own tool through the press pass. No scene or asset changes.");
    }

    // The opened device: covers and screws are off, except any that carry a
    // grime spot themselves (dirt on a cover is cleaned on the cover).
    static void Open(GameObject device, GrimeSpot[] grimeSpots)
    {
        var parts = new List<Component>();
        parts.AddRange(device.GetComponentsInChildren<RemovablePart>());
        parts.AddRange(device.GetComponentsInChildren<ScrewTarget>());
        foreach (Component part in parts)
        {
            bool carriesGrime = false;
            foreach (GrimeSpot grime in grimeSpots)
                carriesGrime |= grime.transform.IsChildOf(part.transform);
            if (!carriesGrime) part.gameObject.SetActive(false);
        }
    }

    // Returns null when the spot is eligible and comes clean by strokes of its own tool, or what stopped it.
    static string CleanByStrokes(GameObject device, GrimeSpot grime)
    {
        Call(grime, "Awake"); // edit mode: lay the patch and paint the dirt first
        RemovablePart cover = grime.CoveredBy;
        if (cover != null) return $"still under '{cover.name}' with the device opened (add it to the cover's faultsBeneath, or move it)";
        if (!FacesTheCamera(device, grime)) return "faces away from the close-up's camera at every quarter turn, face up and turned over";
        // Strokes across the patch, back and forth, each a little off the last (as a hand scrubs), through the press pass.
        Transform t = grime.transform;
        Vector3 n = grime.Normal, c = grime.Centre;
        Vector3 a = Vector3.ProjectOnPlane(t.right, n).normalized, b = Vector3.ProjectOnPlane(t.forward, n).normalized;
        float ha = Mathf.Abs(t.lossyScale.x) * .5f, hb = Mathf.Abs(t.lossyScale.z) * .5f;
        float r = grime.CleanRadius(grime.Tool, null);
        float[] across = { -.4f, .4f, 0f, -.2f, .2f };
        string tool = grime.Tool == ToolType.Cloth ? "cloth" : "brush";
        for (int k = 0; k < 20; k++)
        {
            if (grime == null || grime.IsClean) return null;
            float off = across[k % across.Length] * hb;
            Vector3 from = c - a * ha * 1.2f + b * off, to = c + a * ha * 1.2f + b * off;
            if (k % 2 == 1) { Vector3 swap = from; from = to; to = swap; }
            for (int i = 0; i <= 12; i++)
            {
                if (grime == null || grime.IsClean) return null;
                grime.Pass(Vector3.Lerp(from, to, i / 12f), r);
            }
        }
        return grime == null || grime.IsClean ? null : $"not clean after 20 strokes of the {tool} ({grime.Progress:P0}): {grime.Remaining()}";
    }

    // The spot's face is toward the close-up's camera (58° down the mat, as ItemInspector places it) at some quarter turn of the
    // device lying face up or turned over (its thinnest side up or down, its longest up the view, as the bench lays it).
    static bool FacesTheCamera(GameObject device, GrimeSpot grime)
    {
        Bounds box = LocalBox(device.transform);
        Vector3 size = box.size;
        Vector3 thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
        Vector3 along = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        if (along == thin) along = thin == Vector3.up ? Vector3.forward : Vector3.up;
        Vector3 normal = Quaternion.Inverse(device.transform.rotation) * grime.Normal;
        float pitch = ItemInspector.Pitch * Mathf.Deg2Rad;
        Vector3 toCamera = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch));
        foreach (float face in new[] { 1f, -1f })
        {
            Quaternion lay = Quaternion.Inverse(Quaternion.LookRotation(along, thin * face));
            for (int quarter = 0; quarter < 4; quarter++)
            {
                Quaternion pose = Quaternion.AngleAxis(90f * quarter, Vector3.up) * lay;
                if (Vector3.Dot(pose * normal, toCamera) > BenchRules.FacingDot) return true;
            }
        }
        return false;
    }

    // What is drawn of the device, as a box in its own frame (metres; its scale kept in).
    static Bounds LocalBox(Transform root)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        Quaternion unturn = Quaternion.Inverse(root.rotation);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!DeviceBounds.Counts(r)) continue;
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 local = unturn * (corner - root.position);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        if (float.IsInfinity(min.x)) return new Bounds(Vector3.zero, Vector3.one * .05f);
        var box = new Bounds();
        box.SetMinMax(min, max);
        return box;
    }

    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
#endif
