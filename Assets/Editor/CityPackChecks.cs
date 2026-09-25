#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Guards for the POLYGON City pass (23 Sept). Nothing here saves the scene.
//
//   Clearance raster  A 20 cm map of everything that stands between 0.12 m
//                     and 2.1 m above the street - poles, benches, walls,
//                     trunks, planters - computed from the meshes' triangles.
//                     It is everything a pedestrian or a car could walk or
//                     drive into. Taken before and after the pass, the
//                     difference is exactly what the pass added.
//   Shadows           CafeDaylight's own sun path, 9:30 to 17:00: no new
//                     building may throw a shadow into the cafe interior.
//   Orbit camera      The overhead camera orbits (0, 9) at 24-48 m and 38-68
//                     degrees; a building must stay below the camera's
//                     lowest possible height at that distance.
public static class CityPackChecks
{
    const string Tag = "[City pack] ";
    public static readonly Rect Area = Rect.MinMaxRect(-90f, -80f, 90f, 100f);
    public const int PixelsPerMeter = 5;
    public static readonly Rect CafeInterior = Rect.MinMaxRect(-7.4f, .1f, 7.4f, 18f);
    public static readonly Rect CafeIsland = Rect.MinMaxRect(-9.4f, -5.2f, 10.1f, 20.7f);
    public static readonly Vector2 OrbitFocus = new Vector2(0f, 9f);
    public static string BaselineFolder => Path.Combine(CityPackCatalog.LogRoot, "baseline");
    static string BeforeRasterPath => Path.Combine(BaselineFolder, "clearance-before.png");
    static string GameplayBaselinePath => Path.Combine(BaselineFolder, "gameplay-before.json");

    // ---------------------------------------------------------------- raster

    public sealed class Raster
    {
        public int width, height;
        public bool[] blocked;

        public bool Blocked(float x, float z)
        {
            int u = Mathf.FloorToInt((x - Area.xMin) * PixelsPerMeter), v = Mathf.FloorToInt((z - Area.yMin) * PixelsPerMeter);
            return u >= 0 && v >= 0 && u < width && v < height && blocked[v * width + u];
        }

        // Any blocked pixel within radius of the segment a-b.
        public bool SegmentBlocked(Vector2 a, Vector2 b, float radius, Raster ignore = null)
        {
            float step = 1f / PixelsPerMeter;
            float length = Vector2.Distance(a, b);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            for (int i = 0; i <= samples; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)samples);
                for (float dx = -radius; dx <= radius + 1e-4f; dx += step)
                    for (float dz = -radius; dz <= radius + 1e-4f; dz += step)
                        if (dx * dx + dz * dz <= radius * radius && Blocked(p.x + dx, p.y + dz)
                            && (ignore == null || !ignore.Blocked(p.x + dx, p.y + dz)))
                            return true;
            }
            return false;
        }

        // Any blocked pixel inside a (rotated) rectangle.
        public bool BoxBlocked(Vector2 centre, Vector2 half, float yaw)
        {
            float step = 1f / PixelsPerMeter;
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            for (float x = -half.x; x <= half.x + 1e-4f; x += step)
                for (float z = -half.y; z <= half.y + 1e-4f; z += step)
                {
                    Vector3 p = r * new Vector3(x, 0f, z);
                    if (Blocked(centre.x + p.x, centre.y + p.z)) return true;
                }
            return false;
        }

        public Raster Minus(Raster before)
        {
            var result = new Raster { width = width, height = height, blocked = new bool[blocked.Length] };
            for (int i = 0; i < blocked.Length; i++) result.blocked[i] = blocked[i] && !(before != null && before.blocked[i]);
            return result;
        }

        public void Save(string path, Color on, Color off)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[blocked.Length];
            Color32 a = on, b = off;
            for (int i = 0; i < blocked.Length; i++) pixels[i] = blocked[i] ? a : b;
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        public static Raster Load(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                var pixels = tex.GetPixels32();
                var raster = new Raster { width = tex.width, height = tex.height, blocked = new bool[pixels.Length] };
                for (int i = 0; i < pixels.Length; i++) raster.blocked[i] = pixels[i].r > 127;
                return raster;
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }

    // The map is computed from the meshes themselves, not rendered: every
    // triangle of every fixed mesh is cut to the 0.12-2.1 m slab and its plan
    // (x, z) shape is filled, with its edges widened to one pixel. A top-down
    // render would miss walls, poles and trunks, which it only sees edge-on.
    // Everything that moves (street actors, the player, NPC bodies) is left out.
    public const float SlabLow = .12f, SlabHigh = 2.1f;

    public static Raster CaptureRaster()
    {
        int w = Mathf.RoundToInt(Area.width * PixelsPerMeter), h = Mathf.RoundToInt(Area.height * PixelsPerMeter);
        var raster = new Raster { width = w, height = h, blocked = new bool[w * h] };
        var moving = new List<Transform>();
        foreach (var life in InScene<StreetLife>())
            foreach (var actor in life.actors)
                if (actor != null && actor.actor != null) moving.Add(actor.actor);
        foreach (var mode in InScene<CafeViewMode>()) moving.Add(mode.transform);
        foreach (var visual in InScene<PolygonNpcVisual>()) moving.Add(visual.transform);
        // The cafe's own POLYGON details have their own rules (seats, cup spots,
        // waiting spots, NavMesh); this map is about the streets.
        var cafeDetails = CafeLivelyProps.FindRoot();
        if (cafeDetails != null) moving.Add(cafeDetails);
        var meshes = new Dictionary<Mesh, (Vector3[] vertices, int[] triangles)>();
        var world = new List<Vector3>();
        var polygon = new List<Vector3>(8);
        var scratch = new List<Vector3>(8);
        foreach (var filter in InScene<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy || mesh == null
                || renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
            Bounds b = renderer.bounds;
            if (b.max.y < SlabLow || b.min.y > SlabHigh || b.max.x < Area.xMin || b.min.x > Area.xMax || b.max.z < Area.yMin || b.min.z > Area.yMax) continue;
            if (moving.Any(m => filter.transform.IsChildOf(m))) continue;
            if (!meshes.TryGetValue(mesh, out var data)) meshes[mesh] = data = (mesh.vertices, mesh.triangles);
            Matrix4x4 m2w = filter.transform.localToWorldMatrix;
            world.Clear();
            foreach (var v in data.vertices) world.Add(m2w.MultiplyPoint3x4(v));
            for (int t = 0; t + 2 < data.triangles.Length; t += 3)
            {
                Vector3 a = world[data.triangles[t]], c = world[data.triangles[t + 1]], d = world[data.triangles[t + 2]];
                float lo = Mathf.Min(a.y, Mathf.Min(c.y, d.y)), hi = Mathf.Max(a.y, Mathf.Max(c.y, d.y));
                if (hi < SlabLow || lo > SlabHigh) continue;
                polygon.Clear(); polygon.Add(a); polygon.Add(c); polygon.Add(d);
                if (lo < SlabLow) ClipY(polygon, scratch, SlabLow, true);
                if (hi > SlabHigh) ClipY(polygon, scratch, SlabHigh, false);
                if (polygon.Count > 0) Fill(raster, polygon);
            }
        }
        return raster;
    }

    // Keeps the part of a convex polygon above (keepAbove) or below the plane y = level.
    static void ClipY(List<Vector3> polygon, List<Vector3> scratch, float level, bool keepAbove)
    {
        scratch.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 p = polygon[i], q = polygon[(i + 1) % polygon.Count];
            bool pIn = keepAbove ? p.y >= level : p.y <= level, qIn = keepAbove ? q.y >= level : q.y <= level;
            if (pIn) scratch.Add(p);
            if (pIn != qIn) scratch.Add(Vector3.Lerp(p, q, (level - p.y) / (q.y - p.y)));
        }
        polygon.Clear();
        polygon.AddRange(scratch);
    }

    // Marks every pixel whose centre lies inside the polygon's plan shape or
    // within half a pixel of its outline (so edge-on walls still register).
    static void Fill(Raster raster, List<Vector3> polygon)
    {
        float cell = 1f / PixelsPerMeter, reach = cell * .5f;
        float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
        foreach (var p in polygon) { xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x); zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); }
        int u0 = Mathf.Max(0, Mathf.FloorToInt((xMin - reach - Area.xMin) * PixelsPerMeter)), u1 = Mathf.Min(raster.width - 1, Mathf.FloorToInt((xMax + reach - Area.xMin) * PixelsPerMeter));
        int v0 = Mathf.Max(0, Mathf.FloorToInt((zMin - reach - Area.yMin) * PixelsPerMeter)), v1 = Mathf.Min(raster.height - 1, Mathf.FloorToInt((zMax + reach - Area.yMin) * PixelsPerMeter));
        if (u0 > u1 || v0 > v1) return;
        int n = polygon.Count;
        float area = 0f;
        for (int i = 0; i < n; i++) { Vector3 p = polygon[i], q = polygon[(i + 1) % n]; area += p.x * q.z - q.x * p.z; }
        for (int v = v0; v <= v1; v++)
            for (int u = u0; u <= u1; u++)
            {
                int index = v * raster.width + u;
                if (raster.blocked[index]) continue;
                var c = new Vector2(Area.xMin + (u + .5f) * cell, Area.yMin + (v + .5f) * cell);
                bool inside = Mathf.Abs(area) > 1e-6f;
                float nearest = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = new Vector2(polygon[i].x, polygon[i].z), q = new Vector2(polygon[(i + 1) % n].x, polygon[(i + 1) % n].z);
                    float side = (q.x - p.x) * (c.y - p.y) - (q.y - p.y) * (c.x - p.x);
                    if (area > 0f ? side < 0f : side > 0f) inside = false;
                    nearest = Mathf.Min(nearest, DistanceToSegment(c, p, q));
                }
                if (inside || nearest <= reach) raster.blocked[index] = true;
            }
    }

    public static Raster BeforeRaster() => Raster.Load(BeforeRasterPath);

    // --------------------------------------------------------------- shadows

    public static string ShadowIntrusion(IList<Vector2> footprint, float topY, float fromHour = 9.5f, float toHour = 17f)
    {
        float morning = 110f, evening = 255f;
        var daylight = InScene<CafeDaylight>().FirstOrDefault();
        if (daylight != null)
        {
            using (var so = new SerializedObject(daylight))
            {
                var m = so.FindProperty("morningSunAzimuth");
                var e = so.FindProperty("eveningSunAzimuth");
                if (m != null) morning = m.floatValue;
                if (e != null) evening = e.floatValue;
            }
        }
        var room = new[] { new Vector2(CafeInterior.xMin, CafeInterior.yMin), new Vector2(CafeInterior.xMax, CafeInterior.yMin),
                           new Vector2(CafeInterior.xMax, CafeInterior.yMax), new Vector2(CafeInterior.xMin, CafeInterior.yMax) };
        for (float hour = fromHour; hour <= toHour + 1e-3f; hour += .25f)
        {
            float altitude = CafeDaylight.EvaluateAtHour(hour).altitude;
            if (altitude < 3f) continue;
            float azimuth = Mathf.LerpAngle(morning, evening, Mathf.InverseLerp(9f, 20f, hour));
            Vector3 f = Quaternion.Euler(altitude, azimuth, 0f) * Vector3.forward;
            var points = new List<Vector2>(footprint);
            float t = Mathf.Max(0f, topY) / -f.y;
            foreach (var c in footprint) points.Add(c + new Vector2(f.x, f.z) * t);
            if (ConvexOverlap(Hull(points), room))
                return (int)hour + ":" + ((int)Mathf.Round((hour - (int)hour) * 60f)).ToString("00", CultureInfo.InvariantCulture);
        }
        return null;
    }

    // Lowest the orbit camera can be above a point r metres from its focus.
    public static float CameraClearanceHeight(IList<Vector2> footprint)
    {
        float r = float.MaxValue;
        for (int i = 0; i < footprint.Count; i++)
            r = Mathf.Min(r, DistanceToSegment(OrbitFocus, footprint[i], footprint[(i + 1) % footprint.Count]));
        if (Inside(OrbitFocus, footprint)) r = 0f;
        // The camera is never below tan(38 deg) * r + 0.6; keep 10 % and a metre spare.
        return .7f * r - 1f;
    }

    public static Vector2[] Footprint(Bounds b) => new[]
    {
        new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.max.x, b.max.z), new Vector2(b.min.x, b.max.z)
    };

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static bool Inside(Vector2 p, IList<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y)
                && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    static List<Vector2> Hull(List<Vector2> points)
    {
        var p = points.Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        if (p.Count < 3) return p;
        var lower = new List<Vector2>();
        foreach (var v in p)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], v) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(v);
        }
        var upper = new List<Vector2>();
        for (int i = p.Count - 1; i >= 0; i--)
        {
            var v = p[i];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], v) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(v);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    static bool ConvexOverlap(IList<Vector2> a, IList<Vector2> b)
    {
        foreach (var poly in new[] { a, b })
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 edge = poly[(i + 1) % poly.Count] - poly[i];
                Vector2 axis = new Vector2(-edge.y, edge.x);
                if (axis.sqrMagnitude < 1e-10f) continue;
                float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                foreach (var v in a) { float d = Vector2.Dot(v, axis); aMin = Mathf.Min(aMin, d); aMax = Mathf.Max(aMax, d); }
                foreach (var v in b) { float d = Vector2.Dot(v, axis); bMin = Mathf.Min(bMin, d); bMax = Mathf.Max(bMax, d); }
                if (aMax < bMin || bMax < aMin) return false;
            }
        return true;
    }

    // ------------------------------------------------------ baseline + check

    public static string RecordBaseline()
    {
        RequireScene();
        Directory.CreateDirectory(BaselineFolder);
        Require(PolygonCityDressing.FindRoot() == null, "The POLYGON city is already built; record the baseline on the scene without it.");
        var raster = CaptureRaster();
        raster.Save(BeforeRasterPath, Color.white, Color.black);
        if (File.Exists(GameplayBaselinePath)) File.Delete(GameplayBaselinePath);
        string gameplay = NeighborhoodRefreshChecks.CaptureBaseline(GameplayBaselinePath);
        string photos = Photograph("before");
        return "Baseline: clearance map " + BeforeRasterPath + " (" + raster.blocked.Count(b => b) + " occupied cells)\n" + gameplay + "\n" + photos;
    }

    public static string CheckAll()
    {
        RequireScene();
        var root = PolygonCityDressing.FindRoot();
        Require(root != null, "Build the POLYGON city first.");
        var report = new StringBuilder();
        var failures = new List<string>();

        // 1. What the pass occupies, and whether any of it sits where people walk or cars drive.
        var before = BeforeRaster();
        Require(before != null, "Record the baseline first (City 1).");
        var after = CaptureRaster();
        var added = after.Minus(before);
        string folder = Path.Combine(CityPackCatalog.LogRoot, "check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        added.Save(Path.Combine(folder, "clearance-added.png"), Color.red, Color.black);
        after.Save(Path.Combine(folder, "clearance-after.png"), Color.white, Color.black);
        int walkers = 0, lanes = 0;
        foreach (var life in InScene<StreetLife>())
            foreach (var actor in life.actors)
            {
                if (actor == null || actor.actor == null || actor.waypoints == null || actor.actor.name.StartsWith("Passing bird", StringComparison.Ordinal)) continue;
                bool car = actor.wheels != null && actor.wheels.Length > 0;
                var points = actor.waypoints.Where(t => t != null).Select(t => new Vector2(t.position.x, t.position.z)).ToList();
                int segments = actor.openRoute ? points.Count - 1 : points.Count;
                for (int i = 0; i < segments; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Count];
                    if (!Clip(ref a, ref b)) continue;
                    // Cars: 1.1 m either side of the lane centre; walkers: shoulders.
                    if (added.SegmentBlocked(a, b, car ? 1.1f : .35f))
                        failures.Add((car ? "Lane of " : "Walk of ") + actor.actor.name + " crosses something new near "
                            + ((a + b) * .5f).ToString("F1"));
                }
                if (car) lanes++; else walkers++;
            }
        report.AppendLine("Clearance: " + walkers + " walking routes and " + lanes + " car routes checked against " + added.blocked.Count(b => b) + " newly occupied cells.");
        int islandCells = 0;
        for (float x = CafeIsland.xMin; x < CafeIsland.xMax; x += .2f)
            for (float z = CafeIsland.yMin; z < CafeIsland.yMax; z += .2f)
                if (added.Blocked(x, z)) islandCells++;
        if (islandCells > 0) failures.Add("The pass put " + islandCells + " cells of new geometry on the cafe's own block.");

        // 2. Shadows and the orbit camera, per building.
        int buildings = 0;
        foreach (Transform building in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Building - ", StringComparison.Ordinal)))
        {
            var renderers = building.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) continue;
            buildings++;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            var footprint = Footprint(b);
            string shadow = ShadowIntrusion(footprint, b.max.y);
            if (shadow != null) failures.Add(building.name + " shades the cafe interior at " + shadow + ".");
            float limit = CameraClearanceHeight(footprint);
            if (b.max.y > limit) failures.Add(building.name + " is " + b.max.y.ToString("F1") + " m tall where the orbit camera can come down to " + limit.ToString("F1") + " m.");
        }
        report.AppendLine("Shadows (9:30-17:00) and orbit-camera heights: " + buildings + " buildings checked.");

        // 3. Decorative only: no physics, lights or unsupported materials.
        var colliders = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
        if (colliders.Length > 0) failures.Add(colliders.Length + " colliders are enabled in the city pass (first: " + colliders[0].name + ").");
        var lights = root.GetComponentsInChildren<Light>(true).Where(l => l.enabled).ToArray();
        if (lights.Length > 0) failures.Add(lights.Length + " lights are enabled in the city pass.");
        var badMaterials = root.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m == null || m.shader == null
            || !m.shader.isSupported || m.shader.name.Contains("InternalError"))).Select(r => r.name).Distinct().ToArray();
        if (badMaterials.Length > 0) failures.Add("Unsupported or missing materials: " + string.Join(", ", badMaterials.Take(8)));
        report.AppendLine("City pass: " + root.GetComponentsInChildren<Renderer>(true).Length + " renderers, no enabled colliders/lights, materials " + (badMaterials.Length == 0 ? "OK" : "FAIL") + ".");

        // 4. Gameplay: the existing guards, and every recorded setting except the street actors this pass adds.
        report.AppendLine(NeighborhoodRefreshChecks.Validate());
        report.AppendLine(CompareGameplay(folder, failures));

        report.AppendLine(Photograph("after"));
        File.WriteAllText(Path.Combine(folder, "report.txt"), report + (failures.Count == 0 ? "PASS" : "FAIL:\n" + string.Join("\n", failures)));
        if (failures.Count > 0) throw new InvalidOperationException("City check FAILED (" + failures.Count + "):\n" + string.Join("\n", failures) + "\n\n" + report);
        return "City check PASS\n" + report + "Evidence: " + folder;
    }

    [Serializable] sealed class Entry { public string key, value; }
    [Serializable] sealed class Snapshot { public string scene; public List<Entry> entries = new List<Entry>(); }

    static string CompareGameplay(string folder, List<string> failures)
    {
        Require(File.Exists(GameplayBaselinePath), "Record the baseline first (City 1).");
        string afterPath = Path.Combine(folder, "gameplay-after.json");
        NeighborhoodRefreshChecks.CaptureBaseline(afterPath);
        var before = JsonUtility.FromJson<Snapshot>(File.ReadAllText(GameplayBaselinePath)).entries.ToDictionary(e => e.key, e => e.value);
        var after = JsonUtility.FromJson<Snapshot>(File.ReadAllText(afterPath)).entries.ToDictionary(e => e.key, e => e.value);
        var streetKeys = new HashSet<string>(InScene<StreetLife>().Select(s => "component:" + GlobalObjectId.GetGlobalObjectIdSlow(s)));
        int same = 0;
        foreach (var pair in before)
        {
            if (streetKeys.Contains(pair.Key)) continue;   // the pass adds walkers and cars to StreetLife on purpose
            if (!after.TryGetValue(pair.Key, out string now)) failures.Add("Gameplay record missing after the pass: " + pair.Key);
            else if (now != pair.Value) failures.Add("Gameplay record changed: " + pair.Key);
            else same++;
        }
        int added = after.Keys.Count(k => !before.ContainsKey(k));
        return "Gameplay preservation: " + same + " records unchanged; " + added + " new route anchors for the added street actors; StreetLife itself compared by its actors above.";
    }

    static bool Clip(ref Vector2 a, ref Vector2 b)
    {
        // Keep only the part of a route inside the mapped area.
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        float[] p = { -d.x, d.x, -d.y, d.y };
        float[] q = { a.x - Area.xMin, Area.xMax - a.x, a.y - Area.yMin, Area.yMax - a.y };
        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(p[i]) < 1e-6f) { if (q[i] < 0) return false; continue; }
            float t = q[i] / p[i];
            if (p[i] < 0) t0 = Mathf.Max(t0, t); else t1 = Mathf.Min(t1, t);
            if (t0 > t1) return false;
        }
        Vector2 start = a + d * t0, end = a + d * t1;
        a = start; b = end;
        return true;
    }

    // ---------------------------------------------------------------- photos

    public static readonly (string name, Vector3 position, Vector3 target, float fov, bool isometric)[] Views =
    {
        ("c01-game-camera", new Vector3(-10.51f, 23.79f, -13.54f), new Vector3(-1.24f, 3.33f, 6.35f), 44f, true),
        ("c02-orbit-zoomed-out-southwest", new Vector3(-24.1f, 30.1f, -15.1f), new Vector3(0f, .6f, 9f), 44f, true),
        ("c03-orbit-northwest", new Vector3(-24.1f, 30.1f, 33.1f), new Vector3(0f, .6f, 9f), 44f, true),
        ("c04-orbit-northeast", new Vector3(24.1f, 30.1f, 33.1f), new Vector3(0f, .6f, 9f), 44f, true),
        ("c05-orbit-southeast", new Vector3(24.1f, 30.1f, -15.1f), new Vector3(0f, .6f, 9f), 44f, true),
        ("c06-city-overview", new Vector3(-55f, 48f, -60f), new Vector3(0f, 0f, 10f), 55f, false),
        ("c07-front-street-east", new Vector3(-6f, 1.7f, -9.2f), new Vector3(30f, 3f, -12f), 64f, false),
        ("c08-front-street-west", new Vector3(10f, 1.7f, -9.5f), new Vector3(-30f, 3.5f, -10f), 64f, false),
        ("c09-bus-stop", new Vector3(19f, 1.6f, -7.5f), new Vector3(24f, 1.3f, -12.5f), 62f, false),
        ("c10-west-avenue-north", new Vector3(-12f, 1.7f, -30f), new Vector3(-14f, 4f, 30f), 62f, false),
        ("c11-rear-street", new Vector3(-30f, 1.7f, 22.5f), new Vector3(20f, 4f, 27f), 62f, false),
        ("c12-hill-from-patio", new Vector3(-2f, 1.6f, -3.5f), new Vector3(0f, 9f, 60f), 64f, false),
        ("c13-parking-lot", new Vector3(-12.5f, 7f, -37f), new Vector3(-27f, 0f, -27f), 60f, false),
        ("c14-east-courtyard-block", new Vector3(14f, 2f, -4f), new Vector3(30f, 5f, 8f), 62f, false),
        ("c15-top-down-map", new Vector3(0f, 190f, 10f), new Vector3(0f, 0f, 10.01f), 50f, false),
    };

    // Street neighbours wear their new looks for the photo (never saved), each
    // caught mid-stride in the cafe's own walk clip; posed copies stand in for
    // their skinned meshes so every view shows the pose (see PolygonNpcSetup.BakePose).
    public static string Photograph(string label)
    {
        string folder = Path.Combine(CityPackCatalog.LogRoot, "photos-" + label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var visuals = InScene<PolygonNpcVisual>().Where(v => v.FixedAppearance >= 0).ToArray();
        var applied = new List<PolygonNpcVisual>();
        var posed = new List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)>();
        var walk = AssetDatabase.LoadAllAssetsAtPath(WalkClipSource).OfType<AnimationClip>().FirstOrDefault(c => c.name == "CharacterArmature|Walk");
        try
        {
            if (walk != null && visuals.Length > 0)
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                for (int i = 0; i < visuals.Length; i++)
                {
                    var animator = visuals[i].GetComponentInChildren<Animator>(true);
                    if (animator != null) AnimationMode.SampleAnimationClip(animator.gameObject, walk, (i * .37f % 1f) * walk.length);
                }
                AnimationMode.EndSampling();
            }
            foreach (var v in visuals)
                if (v.ApplyAppearance(v.FixedAppearance))
                {
                    applied.Add(v);
                    posed.AddRange(PolygonNpcSetup.BakePose(v.VisualInstance));
                }
            foreach (var view in Views)
            {
                // The map looks down from 190 m, beyond the fog's end: no fog for that one.
                bool map = view.name.Contains("top-down");
                bool fog = RenderSettings.fog;
                if (map) RenderSettings.fog = false;
                try { CafeSecondPassSteps.Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, view.isometric); }
                finally { RenderSettings.fog = fog; }
            }
        }
        finally
        {
            PolygonNpcSetup.UnbakePose(posed);
            foreach (var v in applied) if (v != null) v.RemoveAppearance();
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }
        return "Photos (" + label + "): " + folder + (applied.Count > 0 ? " - " + applied.Count + " street neighbours shown in their new looks, mid-stride" : "");
    }

    const string WalkClipSource = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";

    // ----------------------------------------------------------------- utils

    internal static T[] InScene<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).Where(c => c != null).ToArray();

    internal static void RequireScene()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        Require(SceneManager.GetActiveScene().path == AcesCafeLayoutSetup.ScenePath, "Open the Ace's Cafe layout scene first.");
    }

    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
