#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// Night track, step 3 (homes for regulars): a read-only look at the six
// bay-window houses across the west street, so Mansoor can choose which front
// door is Grace's. Nothing in the scene changes: photos are rendered by a
// temporary camera (CafeSecondPassSteps.Capture).
//
// For each house: where its street front is, how far it is from the café door,
// which existing walking route (if any) starts at it, and whether the front can
// be seen from the default game camera and from inside the café. Photos, and
// the houses' positions in each photo (for labelling), go to
// <project>/Logs/Night/homes-<time>/.
// ---------------------------------------------------------------------------
public static class HomeSurvey
{
    const string Suffix = "bay-window house";

    // The default game camera (CafeSecondPassSteps' w01 view).
    static readonly Vector3 GamePosition = new(-10.51f, 23.79f, -13.54f), GameTarget = new(-1.24f, 3.33f, 6.35f);
    const float GameFov = 44f;

    [MenuItem("Fixit Fidget/Night/Survey the bay-window houses (read-only)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Homes survey] Stop Play Mode first."); return; }
        try
        {
            var houses = CafeSecondPassSteps.InScene<Transform>()
                .Where(t => t.name.EndsWith(Suffix, StringComparison.Ordinal))
                .OrderBy(t => t.name, StringComparer.Ordinal).ToList();
            if (houses.Count == 0) { Debug.LogError("[Homes survey] No '* " + Suffix + "' in the open scene."); return; }

            CafeArrivals arrivals = UnityEngine.Object.FindAnyObjectByType<CafeArrivals>(FindObjectsInactive.Include);
            Transform door = arrivals != null ? new SerializedObject(arrivals).FindProperty("door").objectReferenceValue as Transform : null;
            Vector3 cafeDoor = door != null ? door.position : Vector3.zero;
            CafeArrivals.Route[] routes = arrivals != null ? arrivals.EditorFootRoutes : Array.Empty<CafeArrivals.Route>();

            var log = new StringBuilder();
            log.AppendLine($"Café door {cafeDoor:F2}. Walking routes: " + string.Join("; ", routes.Where(r => r.points.Length > 0).Select(r => $"'{r.name}' starts {r.points[0]:F2}")));

            // Each house's box (what it draws).
            var boxes = new List<(Transform house, Bounds bounds)>();
            foreach (Transform house in houses)
            {
                Renderer[] parts = house.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
                if (parts.Length == 0) { log.AppendLine($"{house.name}: nothing drawn."); continue; }
                Bounds b = parts[0].bounds;
                foreach (Renderer r in parts) b.Encapsulate(r.bounds);
                boxes.Add((house, b));
            }
            if (boxes.Count == 0) { Debug.LogError("[Homes survey] The houses draw nothing.\n" + log); return; }

            // The row runs along the line through the houses' middles; their street fronts face across it,
            // towards the café, snapped to the boxes' own axes.
            Vector3 middle = Vector3.zero;
            foreach (var box in boxes) middle += box.bounds.center;
            middle /= boxes.Count;
            float xx = 0, xz = 0, zz = 0;
            foreach (var box in boxes)
            {
                float dx = box.bounds.center.x - middle.x, dz = box.bounds.center.z - middle.z;
                xx += dx * dx; xz += dx * dz; zz += dz * dz;
            }
            float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
            Vector3 along = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 across = new(-along.z, 0f, along.x);
            if (Vector3.Dot(across, Flat(cafeDoor - middle)) < 0f) across = -across;
            Vector3 facing = Mathf.Abs(across.x) >= Mathf.Abs(across.z) ? new Vector3(Mathf.Sign(across.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(across.z));
            log.AppendLine($"The row runs along {along:F2}; the fronts face {facing} (towards the café).");

            var fronts = new List<(Transform house, Vector3 front, Vector3 facing, Bounds bounds)>();
            foreach (var (house, b) in boxes)
            {
                Vector3 front = new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.Scale(b.extents, facing);
                fronts.Add((house, front, facing, b));

                string route = "none";
                foreach (CafeArrivals.Route r in routes)
                    if (r.points.Length > 0 && Flat(r.points[0] - front).magnitude < 4f) route = $"'{r.name}' ({Flat(r.points[0] - front).magnitude:F1} m from the front's middle)";

                bool inGame = OnPhoto(GamePosition, GameTarget, GameFov, front + Vector3.up * 1.5f).HasValue;
                log.AppendLine($"{house.name}: box centre {b.center:F1}, size {b.size:F1}; street front {front:F1}; " +
                               $"{Flat(front - cafeDoor).magnitude:F1} m from the café door; walking route: {route}; front inside the default game camera's frame: {(inGame ? "yes" : "no")}.");
            }

            // Photos
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", "homes-" + stamp));
            Directory.CreateDirectory(folder);
            var labels = new StringBuilder("photo\thouse\tx\ty\n");

            Bounds row = fronts[0].bounds;
            foreach (var f in fronts) row.Encapsulate(f.bounds);
            Vector3 rowFront = new Vector3(row.center.x, row.min.y, row.center.z) + Vector3.Scale(row.extents, facing);

            var views = new List<(string name, Vector3 position, Vector3 target, float fov, bool iso)>
            {
                ("h1-the-row-from-above", rowFront + facing * 16f + Vector3.up * 14f, rowFront + Vector3.up * 3f, 60f, false),
                ("h2-the-row-from-the-cafe-sofas", new Vector3(cafeDoor.x - 4.5f, 1.3f, cafeDoor.z + 9f), rowFront + Vector3.up * 2.5f, 70f, false),
                ("h3-default-game-camera", GamePosition, GameTarget, GameFov, true),
            };
            foreach (var f in fronts)
                views.Add(("h-front-" + Slug(f.house.name), f.front + f.facing * 7f + Vector3.up * 1.6f, f.front + Vector3.up * 2.2f, 62f, false));

            foreach (var view in views)
            {
                CafeSecondPassSteps.Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, view.iso);
                foreach (var f in fronts)
                {
                    Vector2? at = OnPhoto(view.position, view.target, view.fov, f.front + Vector3.up * 1.2f);
                    if (at.HasValue) labels.AppendLine($"{view.name}\t{f.house.name}\t{at.Value.x:F0}\t{at.Value.y:F0}");
                }
            }

            File.WriteAllText(Path.Combine(folder, "houses.txt"), log.ToString());
            File.WriteAllText(Path.Combine(folder, "labels.tsv"), labels.ToString());
            Debug.Log("[Homes survey] " + folder + "\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError("[Homes survey] FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem("Fixit Fidget/Night/Survey the bay-window houses (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Where a world point lands on a 1440x900 photo taken from position towards target (top-left origin), or null.
    static Vector2? OnPhoto(Vector3 position, Vector3 target, float fov, Vector3 point)
    {
        Quaternion look = Quaternion.LookRotation(target - position);
        Vector3 local = Quaternion.Inverse(look) * (point - position);
        if (local.z <= 0.1f) return null;
        float half = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float aspect = 1440f / 900f;
        float x = local.x / (local.z * half * aspect), y = local.y / (local.z * half);
        if (Mathf.Abs(x) > 1f || Mathf.Abs(y) > 1f) return null;
        return new Vector2((x + 1f) * 0.5f * 1440f, (1f - (y + 1f) * 0.5f) * 900f);
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    // "2 - Dusty rose bay-window house" -> "2-dusty-rose"
    static string Slug(string name) => name.Replace(" " + Suffix, "").Replace(" - ", "-").Replace(' ', '-').ToLowerInvariant();
}
#endif
