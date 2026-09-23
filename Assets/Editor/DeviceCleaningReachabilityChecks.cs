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
// The bench brush scrubs only what the view ray hits FIRST (ItemInspector),
// so a grime spot buried inside a neighbouring collider can never be cleaned,
// and its device can never reach Perfect. Grace's reunion camera shipped like
// that: its lens grime sat inside the lens glass's capsule collider.
//
// This brushes every grime spot of every fault of every device prefab through
// view rays, re-checking as the spot shrinks, with covers and screws out of the
// way as they are once the device has been opened. Preview scenes only.
public static class DeviceCleaningReachabilityChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // The player can turn a device any way up, so any one clear line of sight
    // is enough: the six axes and the eight corner diagonals.
    static readonly Vector3[] Views = BuildViews();

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
                        spots++;
                        string problem = BrushThroughView(device, grime);
                        if (problem != null) failures.Add($"{prefab.name} (fault {fault}) '{grime.name}': {problem}");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException("[Device cleaning] FAIL: grime the brush cannot reach:\n  " + string.Join("\n  ", failures));
        Debug.Log($"[Device cleaning] PASS: {spots} grime spots across {devices.Count} devices ({string.Join(", ", devices)}) "
            + "can each be brushed clean through a clear line of sight, re-checked as they shrink. No scene or asset changes.");
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

    // Returns null when the spot can be brushed clean, or what blocked it.
    static string BrushThroughView(GameObject device, GrimeSpot grime)
    {
        Call(grime, "Awake"); // edit mode: record its full health and size first
        float full = (float)Get(grime, "scrubHealth");
        float stroke = Mathf.Max(1f, full / 10f);
        for (int i = 0; i < 20; i++)
        {
            Physics.SyncTransforms();
            float health = (float)Get(grime, "scrubHealth");
            if (!Reachable(device, grime, out string blocker))
                return $"at {Mathf.RoundToInt(100f * health / full)}% dirty every view ray first hits '{blocker}'";
            // GrimeSpot destroys itself at zero with Destroy(), which edit mode
            // refuses; remove it the same way once the last stroke would land.
            if (health <= stroke) { UnityEngine.Object.DestroyImmediate(grime.gameObject); return null; }
            grime.Scrub(stroke);
        }
        return "did not come clean after 20 brush strokes";
    }

    static bool Reachable(GameObject device, GrimeSpot grime, out string blocker)
    {
        blocker = "nothing";
        var collider = grime.GetComponent<Collider>();
        if (collider == null || !collider.enabled) { blocker = "(the spot has no enabled collider)"; return false; }
        Vector3 target = collider.bounds.center;
        var blockers = new Dictionary<string, int>();
        foreach (Vector3 view in Views)
        {
            Collider hit = NearestHit(device, new Ray(target - view * .5f, view));
            if (hit != null && ResolveGrime(hit) == grime) return true;
            string name = hit != null ? hit.name : "nothing";
            blockers[name] = blockers.TryGetValue(name, out int n) ? n + 1 : 1;
        }
        int most = 0;
        foreach (var pair in blockers) if (pair.Value > most) { most = pair.Value; blocker = pair.Key; }
        return false;
    }

    static Collider NearestHit(GameObject root, Ray ray)
    {
        Collider nearest = null; float distance = float.PositiveInfinity;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
            if (collider.enabled && collider.Raycast(ray, out RaycastHit hit, 1f) && hit.distance < distance)
            { nearest = collider; distance = hit.distance; }
        return nearest;
    }

    // The inspector's own resolver, so the check agrees with the game.
    static GrimeSpot ResolveGrime(Collider collider)
    {
        object[] args = { collider, null, null, null };
        typeof(ItemInspector).GetMethod("ResolveBenchHit", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return (GrimeSpot)args[2];
    }

    static Vector3[] BuildViews()
    {
        var views = new List<Vector3> { Vector3.down, Vector3.up, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    views.Add(new Vector3(x, y, z).normalized);
        return views.ToArray();
    }

    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
#endif
