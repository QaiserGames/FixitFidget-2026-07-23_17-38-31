#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// Read-only photos of the café's walls, for two things Mansoor saw on 27 Sept:
//   * the east wall (the one with the paintings) has no glass;
//   * the back wall and the tan rear wall vanish when the overhead camera turns.
// The overhead photos use the game's own camera formula (CafeViewMode) at four
// turns, with the cutaway walls hidden the way CafeSecondPassSteps.Capture does.
// Also lists every drawn piece on the east wall and what it's made of.
// Nothing in the scene changes. Output: <project>/Logs/CafeWalls/walls-<time>/.
// ---------------------------------------------------------------------------
public static class CafeWallsSurvey
{
    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/Photograph the cafe walls from inside and every side (read-only)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Cafe walls] Stop Play Mode first."); return; }
        try
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeWalls", "walls-" + stamp));
            Directory.CreateDirectory(folder);
            var log = new StringBuilder();

            CafeViewMode view = UnityEngine.Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            Vector3 focus = view != null ? view.isometricFocus : new Vector3(0f, .6f, 9f);
            log.AppendLine("Cutaway walls: " + (view != null ? string.Join(", ", view.cutawayWalls.Where(r => r != null).Select(r => $"{r.name} (drawn {r.enabled}, centre {r.bounds.center:F2}, size {r.bounds.size:F2})")) : "no CafeViewMode"));

            // The east wall, as built by the V3 street pass, and anything glassy near it.
            foreach (Renderer r in CafeSecondPassSteps.InScene<Renderer>().Where(r => r.enabled && r.bounds.center.x > 7f && r.bounds.center.x < 8.2f
                                                                                     && r.bounds.center.z > -.5f && r.bounds.center.z < 18.6f && r.bounds.center.y < 3.4f))
                log.AppendLine($"  east: {PathOf(r.transform)}  centre {r.bounds.center:F2} size {r.bounds.size:F2}  materials {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))}");
            foreach (Renderer r in CafeSecondPassSteps.InScene<Renderer>().Where(r => r.enabled && r.sharedMaterials.Any(m => m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)
                                                                                     && r.bounds.center.x > -9f && r.bounds.center.x < 9f && r.bounds.center.z > -1.5f && r.bounds.center.z < 19f))
                log.AppendLine($"  glass: {PathOf(r.transform)}  centre {r.bounds.center:F2} size {r.bounds.size:F2}  materials {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))}");

            // The overhead camera at four turns (CafeViewMode.RefreshCameraPose).
            foreach (float yaw in new[] { 45f, 135f, 225f, 315f })
            {
                Quaternion angle = Quaternion.Euler(50f, yaw, 0f);
                Vector3 position = focus - angle * Vector3.forward * 34f;
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"o-overhead-yaw{yaw:0}.png"), position, focus, 44f, true);
                log.AppendLine($"overhead yaw {yaw:0}: camera {position:F1}");
            }
            // The east wall from inside and from the courtyard.
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e1-east-wall-from-inside.png"), new Vector3(-3.5f, 1.6f, 9f), new Vector3(7.5f, 1.6f, 9f), 78f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e2-east-wall-from-the-courtyard.png"), new Vector3(14f, 1.7f, 9f), new Vector3(7.5f, 1.6f, 9f), 78f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e3-back-wall-and-tan-wall-from-inside.png"), new Vector3(2f, 1.6f, 4f), new Vector3(-4f, 1.6f, 18f), 78f, false);

            File.WriteAllText(Path.Combine(folder, "walls.txt"), log.ToString());
            Debug.Log("[Cafe walls] " + folder + "\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError("[Cafe walls] FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/Photograph the cafe walls from inside and every side (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Everything drawn within half a metre of each cut-away wall's faces and above
    // the window sills: what would float if that wall were lowered to sill height.
    // Output: <project>/Logs/CafeWalls/fixtures-<time>.txt. Nothing changes.
    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/List what is fixed to the cut-away walls (read-only)")]
    static void ListFixtures()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Cafe walls] Stop Play Mode first."); return; }
        try
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeWalls"));
            Directory.CreateDirectory(folder);
            var log = new StringBuilder();
            CafeViewMode view = UnityEngine.Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            if (view == null) { Debug.LogError("[Cafe walls] No CafeViewMode in the open scene."); return; }
            Renderer[] all = CafeSecondPassSteps.InScene<Renderer>();
            foreach (Renderer wall in view.cutawayWalls.Where(r => r != null))
            {
                Bounds w = wall.bounds;
                bool normalX = w.size.x < w.size.z;
                float s0 = normalX ? w.min.x : w.min.z, s1 = normalX ? w.max.x : w.max.z;
                float t0 = normalX ? w.min.z : w.min.x, t1 = normalX ? w.max.z : w.max.x;
                float inside = normalX ? view.isometricFocus.x : view.isometricFocus.z;
                log.AppendLine($"== {PathOf(wall.transform)}  drawn {wall.enabled}  centre {w.center:F2} size {w.size:F2}  mesh {wall.GetComponent<MeshFilter>()?.sharedMesh?.name}  static {wall.gameObject.isStatic}  batched {wall.isPartOfStaticBatch}  children {wall.transform.childCount}");
                foreach (Renderer r in all)
                {
                    if (r == wall || !r.gameObject.activeInHierarchy) continue;
                    Bounds b = r.bounds;
                    float a = normalX ? b.min.x : b.min.z, e = normalX ? b.max.x : b.max.z;
                    float c = normalX ? b.center.z : b.center.x;
                    if (e < s0 - .5f || a > s1 + .5f || c < t0 - .1f || c > t1 + .1f || b.max.y < .8f || b.min.y > 3.5f) continue;
                    float gap = Mathf.Max(0f, Mathf.Max(a - s1, s0 - e));
                    float depth = Mathf.Max(0f, e - s1) + Mathf.Max(0f, s0 - a);
                    string side = (normalX ? b.center.x : b.center.z) < s0 == inside < s0 ? "in " : "out";
                    string parents = string.Join(",", new[] { "Interactable", "Rigidbody", "Animator", "NavMeshAgent", "CharacterController" }
                        .Where(n => r.GetComponentsInParent<Component>(true).Any(k => k != null && k.GetType().Name.Contains(n))));
                    log.AppendLine($"  {side} {(r.enabled ? "on " : "off")} {r.GetType().Name,-22} y {b.min.y:F2}-{b.max.y:F2}  gap {gap:F2} depth {depth:F2}  along {c:F2}  static {r.gameObject.isStatic}  {(parents.Length > 0 ? "[" + parents + "] " : "")}{PathOf(r.transform)}  ({string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name).Distinct())})");
                }
            }
            string file = Path.Combine(folder, "fixtures-" + stamp + ".txt");
            File.WriteAllText(file, log.ToString());
            Debug.Log("[Cafe walls] " + file + "\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError("[Cafe walls] FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/List what is fixed to the cut-away walls (read-only)", true)]
    static bool NotPlayingList() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && path.Count(c => c == '/') < 2; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
#endif
