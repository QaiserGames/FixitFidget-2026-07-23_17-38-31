using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// NIGHT WALK 2 - THE EDGES SWEEP (night step 4, part 2; claude/night-city-proposal.md §7)
//
// The rule for the night's edges: wherever Ace can't go, there's a visible reason.
// This sweep is how that is measured. It moves an Ace-sized body over the 9 blocks,
// and 12 m beyond them, on a 25 cm grid, and at every spot compares two worlds:
//
//   * the PHYSICS world: what Ace's CharacterController really stands on and bumps
//     into (every collider Ace's layer collides with; walkers, cars and Ace
//     themselves left out);
//   * the VISIBLE world: every visible mesh, rebuilt as exact mesh collision in a
//     separate preview scene (it never touches the café scene).
//
// At each spot it looks for the lowest ground that one of the two worlds lets Ace
// stand on (with Ace's body clear above the 0.3 m step), and names what it finds:
//
//   Open         both worlds agree Ace can stand there;
//   Walk-through Ace can stand there, but something visible fills the space
//                (a bench, a planter, a wall with no collision);
//   Air wall     nothing visible is there, but a collider stops Ace (a POLYGON
//                prop's rough convex collision under a lamp's arm or an awning);
//   Hole         visible ground with no collision under it;
//   Solid        both agree: a wall, a building, a parked car.
//
// Then it floods every spot Ace could walk to from the pavement outside the café
// door (steps of up to 0.3 m, slopes up to 45°) and lists every way out of the
// district: the street ends and any gaps.
//
// Read-only. The survey menu (Edit Mode) sweeps the scene as saved, by day, when the
// night's objects are all switched off; the night check (part 2c) runs the same
// sweep in Play Mode with the night switched on.
// ---------------------------------------------------------------------------
internal static class NightEdgesSweep
{
    const string Tag = "[Night walk] ";
    const string SurveyMenu = "Fixit Fidget/Night/Night walk 2 - Survey the edges and collisions (read-only)";
    const string NightMenu = "Fixit Fidget/Night/Night walk 2 - Sweep the night (Edit Mode, the night simulated)";
    /// <summary>Ground more than this above the café's front pavement is somewhere Ace shouldn't get to yet (a roof, a wall top, a fire escape).</summary>
    public const float HighAbove = 1f;

    /// <summary>The 9 blocks: the café's block and the eight round it (the V4 street grid).</summary>
    public static readonly Rect District = Rect.MinMaxRect(-38f, -34f, 39f, 37f);
    public const float Margin = 12f, Cell = .25f;
    /// <summary>The pavement just outside the café door, where the flood starts.</summary>
    public static readonly Vector2 Start = new Vector2(0f, -2.6f);
    const float RayTop = 80f, RayDepth = 100f, GroundTolerance = .35f;
    const int VisualLayer = 31, NightLayer = 30;

    public enum Kind : byte { Nothing, Solid, Open, WalkThrough, AirWall, Hole, InvisibleFloor }

    public struct Body { public float radius, height, step, slope; public string from; }

    public sealed class Leak
    {
        public int cells;
        public Vector2 min, max;
        public string side = "";
        public float from, to;              // along the district edge it crosses
        public string street = "";
    }

    public sealed class Result
    {
        public int width, height;
        public float x0, z0;
        public Kind[] kind;
        public float[] level;
        public bool[] reached;
        public Body body;
        public int visualMeshes, visualFailed, visualNoTriangle, physicsIgnored, startIndex = -1, nightPieces, nightObjectColliders;
        public bool night;
        public bool separatePhysics;
        public readonly Dictionary<string, int> airWall = new Dictionary<string, int>(), airWallTouching = new Dictionary<string, int>();
        public readonly Dictionary<string, int> walkThrough = new Dictionary<string, int>(), walkThroughReached = new Dictionary<string, int>();
        public readonly Dictionary<string, int> hole = new Dictionary<string, int>(), holeTouching = new Dictionary<string, int>();
        public readonly Dictionary<string, Vector2> where = new Dictionary<string, Vector2>(), whereNear = new Dictionary<string, Vector2>();
        public readonly List<(int index, string key)> blamed = new List<(int, string)>();
        public readonly List<Leak> leaks = new List<Leak>();
        public string[] groundKey;
        public float streetLevel;
        public readonly Dictionary<string, int> high = new Dictionary<string, int>();

        public int Index(int i, int j) => j * width + i;
        public Vector2 Centre(int i, int j) => new Vector2(x0 + (i + .5f) * Cell, z0 + (j + .5f) * Cell);
        public int Count(Kind k) => kind.Count(x => x == k);
        public int ReachedCount => reached.Count(r => r);
        // Reachable ground on the swept area's own edge: from there Ace could walk on, out of the sweep.
        public int ReachedAtBorder
        {
            get
            {
                int n = 0;
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                        if (reached[Index(i, j)] && (i == 0 || j == 0 || i == width - 1 || j == height - 1)) n++;
                return n;
            }
        }
        public Rect ReachedBox
        {
            get
            {
                float x0r = float.MaxValue, z0r = float.MaxValue, x1r = float.MinValue, z1r = float.MinValue;
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                        if (reached[Index(i, j)])
                        {
                            Vector2 c = Centre(i, j);
                            x0r = Mathf.Min(x0r, c.x); x1r = Mathf.Max(x1r, c.x); z0r = Mathf.Min(z0r, c.y); z1r = Mathf.Max(z1r, c.y);
                        }
                return x0r > x1r ? new Rect() : Rect.MinMaxRect(x0r, z0r, x1r, z1r);
            }
        }
        public int ReachedOutside
        {
            get
            {
                int n = 0;
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                        if (reached[Index(i, j)] && !District.Contains(Centre(i, j))) n++;
                return n;
            }
        }
    }

    // ------------------------------------------------------------------ survey menu

    [MenuItem(SurveyMenu)]
    static void Survey()
    {
        try
        {
            CityPackChecks.RequireScene();
            string folder = LogFolder("edges-survey");
            var result = Sweep();
            var report = new StringBuilder();
            report.AppendLine("Night walk 2 - survey the edges and collisions (read-only, the scene as saved: by day, night objects off)");
            report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            report.AppendLine();
            Describe(result, report);
            report.AppendLine();
            NavMeshSettings(report);
            report.AppendLine();
            WorksProps(report);
            Write(result, folder, report);
            Debug.Log(Tag + $"Edges survey: {result.leaks.Count} way(s) out, {result.airWallTouching.Count} air walls and " +
                      $"{result.walkThroughReached.Count} walk-throughs Ace could meet. Written to {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Edges survey FAILED: " + e.Message + "\n" + e); }
    }

    // The night can't be swept in Play Mode: Unity's static batching merges most meshes there, so the
    // visible world can't be rebuilt from them. So the night is simulated here, in Edit Mode, from the
    // same things NightWalk switches on: the list of what is solid by night, the night's own objects
    // (their renderers and their collision, although they are switched off by day) and the patio's day
    // fence switched off. The scene itself is not touched.
    [MenuItem(NightMenu)]
    static void SweepTheNight()
    {
        try
        {
            CityPackChecks.RequireScene();
            var walk = CityPackChecks.InScene<NightWalk>().FirstOrDefault();
            if (walk == null) throw new InvalidOperationException("Run Fixit Fidget > Night > Night walk 1 first.");
            if (walk.nightCollision == null) throw new InvalidOperationException("The night has no collision list yet: run Night walk 1 again.");
            string folder = LogFolder("edges-night");
            var result = Sweep(true);
            var report = new StringBuilder();
            report.AppendLine("Night walk 2 - sweep the night (Edit Mode, the night simulated: solid by night, the night's own objects, the patio fence off)");
            report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            report.AppendLine($"Solid by night: {result.nightPieces} meshes from '{AssetDatabase.GetAssetPath(walk.nightCollision)}' " +
                              $"({walk.nightCollision.triangles:N0} triangles, {walk.nightCollision.sheared} under a sheared transform); " +
                              $"the night's own objects bring {result.nightObjectColliders} colliders; day-only colliders off: {walk.dayOnlyColliders.Length}.");
            report.AppendLine();
            Describe(result, report);
            Write(result, folder, report);
            Debug.Log(Tag + $"Night sweep: Ace can reach {result.ReachedCount * Cell * Cell:0} m², {result.ReachedOutside * Cell * Cell:0} m² of it outside the district, " +
                      $"{(result.ReachedAtBorder == 0 ? "all of it enclosed" : $"NOT enclosed ({result.ReachedAtBorder} spots on the sweep's edge)")}; " +
                      $"{result.leaks.Count} way(s) out, {result.airWallTouching.Count} air walls, {result.walkThroughReached.Count} walk-throughs, " +
                      $"{result.holeTouching.Count} holes, {result.high.Count} high places. Written to {folder}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Night sweep FAILED: " + e.Message + "\n" + e); }
    }

    // ------------------------------------------------------------------ the sweep

    /// <summary>
    /// Runs the sweep on the active scene as it is (Edit Mode). With simulateNight, the night's collision
    /// and objects are added and the day-only colliders left out, as NightWalk does. Changes nothing.
    /// </summary>
    public static Result Sweep(bool simulateNight = false)
    {
        var walk = simulateNight ? CityPackChecks.InScene<NightWalk>().FirstOrDefault() : null;
        var body = AceBody();
        var result = new Result
        {
            body = body,
            x0 = District.xMin - Margin,
            z0 = District.yMin - Margin,
            width = Mathf.CeilToInt((District.width + 2f * Margin) / Cell),
            height = Mathf.CeilToInt((District.height + 2f * Margin) / Cell),
        };
        int n = result.width * result.height;
        result.kind = new Kind[n];
        result.level = new float[n];
        result.reached = new bool[n];
        result.groundKey = new string[n];

        // Everything that moves, or is Ace, is left out of both worlds.
        var moving = MovingRoots();
        var ignored = new HashSet<Collider>();
        foreach (var root in moving)
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) ignored.Add(c);
        result.physicsIgnored = ignored.Count;
        result.night = simulateNight;
        if (walk != null)
        {
            foreach (var c in walk.dayOnlyColliders) if (c != null) ignored.Add(c);
            foreach (var c in walk.GetComponentsInChildren<Collider>(true)) ignored.Add(c);   // they are copied into the night's own physics
        }

        int physMask = 0;
        int aceLayer = LayerMask.NameToLayer("Player");
        if (aceLayer < 0) aceLayer = 6;
        for (int l = 0; l < 32; l++)
            if (!Physics.GetIgnoreLayerCollision(aceLayer, l) && l != aceLayer && l != VisualLayer && l != NightLayer) physMask |= 1 << l;
        int npc = LayerMask.NameToLayer("NPC");
        if (npc >= 0) physMask &= ~(1 << npc);

        Physics.SyncTransforms();
        PhysicsScene main = Physics.defaultPhysicsScene;
        Scene preview = EditorSceneManager.NewPreviewScene();
        Scene nightScene = walk != null ? EditorSceneManager.NewPreviewScene() : default;
        var source = new Dictionary<Collider, string>();
        try
        {
            BuildVisible(preview, moving, source, result, walk != null ? walk.transform : null);
            if (walk != null) BuildNightPhysics(nightScene, walk, source, result);
            Physics.SyncTransforms();
            PhysicsScene visible = preview.GetPhysicsScene();
            PhysicsScene night = walk != null ? nightScene.GetPhysicsScene() : default;
            result.separatePhysics = !visible.Equals(main);
            int visMask = 1 << VisualLayer, nightMask = 1 << NightLayer;

            var physHits = new RaycastHit[128];
            var moreHits = new RaycastHit[64];
            var visHits = new RaycastHit[64];
            var overlaps = new Collider[32];
            var levels = new List<float>(16);
            float minNormal = Mathf.Cos(body.slope * Mathf.Deg2Rad) - .001f;
            float r = body.radius;

            bool Clear(PhysicsScene scene, int mask, bool real, float x, float y, float z)
            {
                int c = scene.OverlapCapsule(new Vector3(x, y + body.step + r, z), new Vector3(x, y + body.height - r, z), r, overlaps, mask, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < c; k++)
                    if (!real || !ignored.Contains(overlaps[k])) return false;
                // The real world at night is the scene plus the night's own physics.
                if (real && walk != null && night.OverlapCapsule(new Vector3(x, y + body.step + r, z), new Vector3(x, y + body.height - r, z), r, overlaps, nightMask, QueryTriggerInteraction.Ignore) > 0)
                    return false;
                return true;
            }

            int PhysRaycast(Vector3 from)
            {
                int count = main.Raycast(from, Vector3.down, physHits, RayDepth, physMask, QueryTriggerInteraction.Ignore), kept = 0;
                for (int k = 0; k < count; k++) if (!ignored.Contains(physHits[k].collider)) physHits[kept++] = physHits[k];
                if (walk != null)
                {
                    int more = night.Raycast(from, Vector3.down, moreHits, RayDepth, nightMask, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < more && kept < physHits.Length; k++) physHits[kept++] = moreHits[k];
                }
                return kept;
            }

            Collider groundHit = null;
            var keyCache = new Dictionary<Collider, string>();
            string KeyOf(Collider col)
            {
                if (col == null) return "?";
                if (keyCache.TryGetValue(col, out var known)) return known;
                string key = source.TryGetValue(col, out var s) ? s : PathOf(col.transform) + " [" + ColliderKind(col) + "]";
                keyCache[col] = key;
                return key;
            }
            float? GroundNear(RaycastHit[] hits, int count, bool real, float y)
            {
                float best = float.NaN, bestDelta = float.MaxValue;
                groundHit = null;
                for (int k = 0; k < count; k++)
                {
                    if (real && ignored.Contains(hits[k].collider)) continue;
                    if (hits[k].normal.y < minNormal) continue;
                    float d = Mathf.Abs(hits[k].point.y - y);
                    if (d <= GroundTolerance && d < bestDelta) { bestDelta = d; best = hits[k].point.y; groundHit = hits[k].collider; }
                }
                return float.IsNaN(best) ? (float?)null : best;
            }

            void Blame(Dictionary<string, int> into, PhysicsScene scene, int mask, bool real, float x, float y, float z, Vector2 at, int cell)
            {
                for (int pass = 0; pass < (real && walk != null ? 2 : 1); pass++)
                {
                    PhysicsScene inScene = pass == 0 ? scene : night;
                    int c = inScene.OverlapCapsule(new Vector3(x, y + body.step + r, z), new Vector3(x, y + body.height - r, z), r, overlaps, pass == 0 ? mask : nightMask, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < c; k++)
                    {
                        var col = overlaps[k];
                        if (pass == 0 && real && ignored.Contains(col)) continue;
                        string key = pass == 1 || !real ? (source.TryGetValue(col, out var s) ? s : col.name) : PathOf(col.transform) + " [" + ColliderKind(col) + "]";
                        into[key] = into.TryGetValue(key, out int v) ? v + 1 : 1;
                        if (!result.where.ContainsKey(key)) result.where[key] = at;
                        result.blamed.Add((cell, key));
                    }
                }
            }

            for (int j = 0; j < result.height; j++)
                for (int i = 0; i < result.width; i++)
                {
                    Vector2 p = result.Centre(i, j);
                    int index = result.Index(i, j);
                    Vector3 top = new Vector3(p.x, RayTop, p.y);
                    int pn = PhysRaycast(top);
                    int vn = visible.Raycast(top, Vector3.down, visHits, RayDepth, visMask, QueryTriggerInteraction.Ignore);
                    levels.Clear();
                    for (int k = 0; k < pn; k++)
                        if (physHits[k].normal.y >= minNormal) levels.Add(physHits[k].point.y);
                    for (int k = 0; k < vn; k++)
                        if (visHits[k].normal.y >= minNormal) levels.Add(visHits[k].point.y);
                    if (levels.Count == 0) { result.kind[index] = pn + vn > 0 ? Kind.Solid : Kind.Nothing; continue; }
                    levels.Sort();
                    Kind kind = Kind.Solid;
                    float level = levels[0], last = float.MinValue;
                    foreach (float y in levels)
                    {
                        if (y - last < .05f) continue;
                        last = y;
                        float? pg = GroundNear(physHits, pn, false, y);
                        Collider physGround = groundHit;
                        float? vg = GroundNear(visHits, vn, false, y);
                        float py = pg ?? y, vy = vg ?? y;
                        bool pc = Clear(main, physMask, true, p.x, py, p.y);
                        bool vc = Clear(visible, visMask, false, p.x, vy, p.y);
                        bool physStand = pg.HasValue && pc, visStand = vg.HasValue && vc;
                        if (!physStand && !visStand) continue;
                        if (physStand)
                        {
                            level = py;
                            result.groundKey[index] = KeyOf(physGround);
                            if (visStand) kind = Kind.Open;
                            else if (!vg.HasValue && vc) kind = Kind.InvisibleFloor;
                            else { kind = Kind.WalkThrough; Blame(result.walkThrough, visible, visMask, false, p.x, py, p.y, p, index); }
                        }
                        else
                        {
                            level = vy;
                            if (!pg.HasValue && pc)
                            {
                                kind = Kind.Hole;
                                string key = "ground: " + GroundSource(visHits, vn, vy, source);
                                result.hole[key] = result.hole.TryGetValue(key, out int v) ? v + 1 : 1;
                                if (!result.where.ContainsKey(key)) result.where[key] = p;
                                result.blamed.Add((index, key));
                            }
                            else { kind = Kind.AirWall; Blame(result.airWall, main, physMask, true, p.x, vy, p.y, p, index); }
                        }
                        break;
                    }
                    result.kind[index] = kind;
                    result.level[index] = level;
                }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (walk != null) EditorSceneManager.ClosePreviewScene(nightScene);
        }

        Flood(result);
        Touching(result);
        FindLeaks(result);
        HighSpots(result);
        return result;
    }

    // Somewhere Ace can reach that is well above the street: a roof, a wall top, a fire escape, a skip's lid.
    static void HighSpots(Result result)
    {
        if (result.startIndex < 0) return;
        result.streetLevel = result.level[result.startIndex];
        for (int index = 0; index < result.kind.Length; index++)
        {
            if (!result.reached[index] || result.level[index] <= result.streetLevel + HighAbove) continue;
            string key = result.groundKey[index] ?? "?";
            result.high[key] = result.high.TryGetValue(key, out int v) ? v + 1 : 1;
            if (!result.whereNear.ContainsKey(key)) result.whereNear[key] = result.Centre(index % result.width, index / result.width);
        }
    }

    static bool Walkable(Kind k) => k == Kind.Open || k == Kind.WalkThrough || k == Kind.InvisibleFloor;

    static void Flood(Result result)
    {
        // The nearest walkable spot to the café's front pavement.
        float best = float.MaxValue;
        for (int j = 0; j < result.height; j++)
            for (int i = 0; i < result.width; i++)
            {
                int index = result.Index(i, j);
                if (!Walkable(result.kind[index])) continue;
                float d = (result.Centre(i, j) - Start).sqrMagnitude;
                if (d < best) { best = d; result.startIndex = index; }
            }
        if (result.startIndex < 0) return;
        float climb = result.body.step + .05f;
        var queue = new Queue<int>();
        queue.Enqueue(result.startIndex);
        result.reached[result.startIndex] = true;
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int i = index % result.width, j = index / result.width;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= result.width || nj >= result.height) continue;
                    int next = result.Index(ni, nj);
                    if (result.reached[next] || !Walkable(result.kind[next])) continue;
                    if (Mathf.Abs(result.level[next] - result.level[index]) > climb) continue;
                    result.reached[next] = true;
                    queue.Enqueue(next);
                }
        }
    }

    // Air walls and holes are never walked on: the ones that matter are right next to a spot Ace can reach.
    // Walk-throughs matter where Ace can actually stand in them.
    static void Touching(Result result)
    {
        foreach (var (index, key) in result.blamed)
        {
            var k = result.kind[index];
            int i = index % result.width, j = index / result.width;
            bool counts;
            if (k == Kind.WalkThrough) counts = result.reached[index];
            else
            {
                counts = false;
                for (int dj = -1; dj <= 1 && !counts; dj++)
                    for (int di = -1; di <= 1 && !counts; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= result.width || nj >= result.height) continue;
                        counts = result.reached[result.Index(ni, nj)];
                    }
            }
            if (!counts) continue;
            var into = k == Kind.WalkThrough ? result.walkThroughReached : k == Kind.AirWall ? result.airWallTouching : result.holeTouching;
            into[key] = into.TryGetValue(key, out int v) ? v + 1 : 1;
            if (!result.whereNear.ContainsKey(key)) result.whereNear[key] = result.Centre(i, j);
        }
    }

    static void FindLeaks(Result result)
    {
        var seen = new bool[result.kind.Length];
        for (int start = 0; start < result.kind.Length; start++)
        {
            if (seen[start] || !result.reached[start]) continue;
            int si = start % result.width, sj = start / result.width;
            if (District.Contains(result.Centre(si, sj))) continue;
            var leak = new Leak { min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue) };
            var edge = new List<Vector2>();
            var queue = new Queue<int>();
            queue.Enqueue(start);
            seen[start] = true;
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int i = index % result.width, j = index / result.width;
                Vector2 c = result.Centre(i, j);
                leak.cells++;
                leak.min = Vector2.Min(leak.min, c);
                leak.max = Vector2.Max(leak.max, c);
                bool atEdge = false;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= result.width || nj >= result.height) continue;
                        int next = result.Index(ni, nj);
                        if (!result.reached[next]) continue;
                        if (District.Contains(result.Centre(ni, nj))) { atEdge = true; continue; }
                        if (seen[next]) continue;
                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                if (atEdge) edge.Add(c);
            }
            if (edge.Count > 0)
            {
                Vector2 mid = edge.Aggregate(Vector2.zero, (a, b) => a + b) / edge.Count;
                float dW = Mathf.Abs(mid.x - District.xMin), dE = Mathf.Abs(mid.x - District.xMax), dS = Mathf.Abs(mid.y - District.yMin), dN = Mathf.Abs(mid.y - District.yMax);
                float m = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
                if (m == dW || m == dE)
                {
                    leak.side = m == dW ? "west edge" : "east edge";
                    leak.from = edge.Min(e => e.y); leak.to = edge.Max(e => e.y);
                }
                else
                {
                    leak.side = m == dS ? "south edge" : "north edge";
                    leak.from = edge.Min(e => e.x); leak.to = edge.Max(e => e.x);
                }
                leak.street = NearestStreet(mid);
            }
            result.leaks.Add(leak);
        }
        result.leaks.Sort((a, b) => b.cells.CompareTo(a.cells));
    }

    static string NearestStreet(Vector2 p)
    {
        var streets = new (string name, bool alongX, float line)[]
        {
            ("West Street (x -12)", false, -12f), ("East Street (x 12.6)", false, 12.6f),
            ("Front Street (z -7.7)", true, -7.7f), ("Back Street (z 23.2)", true, 23.2f),
        };
        string best = "";
        float bestD = float.MaxValue;
        foreach (var s in streets)
        {
            float d = s.alongX ? Mathf.Abs(p.y - s.line) : Mathf.Abs(p.x - s.line);
            if (d < bestD) { bestD = d; best = s.name; }
        }
        return bestD < 8f ? best : $"no street (nearest {best}, {bestD:0.0} m away)";
    }

    // ------------------------------------------------------------------ the two worlds

    static Body AceBody()
    {
        var body = new Body { radius = .5f, height = 2f, step = .3f, slope = 45f, from = "defaults (no Player CharacterController found)" };
        var player = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<CharacterController>(true))
            .FirstOrDefault(c => c.gameObject.layer == LayerMask.NameToLayer("Player") || c.gameObject.name == "Player");
        if (player != null)
        {
            float scale = Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z));
            body = new Body
            {
                radius = player.radius * scale,
                height = player.height * Mathf.Abs(player.transform.lossyScale.y),
                step = player.stepOffset,
                slope = player.slopeLimit,
                from = $"'{PathOf(player.transform)}' (height {player.height:0.##}, radius {player.radius:0.##}, step {player.stepOffset:0.##}, slope {player.slopeLimit:0}°, skin {player.skinWidth:0.##})",
            };
        }
        return body;
    }

    // Walkers, cars, café visitors and Ace: everything that moves is left out of both worlds.
    static List<Transform> MovingRoots()
    {
        var moving = new List<Transform>();
        foreach (var life in CityPackChecks.InScene<StreetLife>())
            foreach (var actor in life.actors)
                if (actor != null && actor.actor != null) moving.Add(actor.actor);
        foreach (var mode in CityPackChecks.InScene<CafeViewMode>()) moving.Add(mode.transform);
        foreach (var visual in CityPackChecks.InScene<PolygonNpcVisual>()) moving.Add(visual.transform);
        foreach (var c in CityPackChecks.InScene<CharacterController>()) moving.Add(c.transform);
        foreach (var a in CityPackChecks.InScene<UnityEngine.AI.NavMeshAgent>()) moving.Add(a.transform);
        return moving;
    }

    static void BuildVisible(Scene preview, List<Transform> moving, Dictionary<Collider, string> source, Result result, Transform nightRoot = null)
    {
        var region = new Bounds(new Vector3(District.center.x, 0f, District.center.y),
                                new Vector3(District.width + 2f * Margin + 4f, 400f, District.height + 2f * Margin + 4f));
        var triangles = new Dictionary<Mesh, bool>();
        foreach (var filter in CityPackChecks.InScene<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            bool nightObject = nightRoot != null && filter.transform.IsChildOf(nightRoot);
            if (renderer == null || mesh == null || !renderer.enabled || !(filter.gameObject.activeInHierarchy || nightObject)) continue;
            if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
            if (!renderer.bounds.Intersects(region)) continue;
            if (moving.Any(m => filter.transform.IsChildOf(m))) continue;
            if (filter.GetComponents<Component>().Any(c => c != null && c.GetType().Name.StartsWith("TextMeshPro", StringComparison.Ordinal))) continue;
            // Nothing to see and nothing PhysX could build (it would only log an error).
            if (!NightCollisionList.CanCollide(mesh, triangles)) { result.visualNoTriangle++; continue; }
            try
            {
                var go = new GameObject("visible") { hideFlags = HideFlags.HideAndDontSave, layer = VisualLayer };
                SceneManager.MoveGameObjectToScene(go, preview);
                go.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                go.transform.localScale = filter.transform.lossyScale;
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                source[collider] = PathOf(filter.transform);
                result.visualMeshes++;
            }
            catch (Exception) { result.visualFailed++; }
        }
    }

    // The night's own physics, in a preview scene of its own: what is solid by night, and the night's
    // own objects' collision (copied: those objects are switched off by day, so their colliders aren't live).
    static void BuildNightPhysics(Scene scene, NightWalk walk, Dictionary<Collider, string> source, Result result)
    {
        if (walk.nightCollision != null)
            foreach (var piece in walk.nightCollision.pieces)
            {
                if (piece.mesh == null) continue;
                var mc = NightObject(scene, piece.position, piece.rotation, piece.scale).AddComponent<MeshCollider>();
                mc.sharedMesh = piece.mesh;
                source[mc] = piece.from + " [solid by night]";
                result.nightPieces++;
            }
        foreach (var c in walk.GetComponentsInChildren<Collider>(true))
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            var t = c.transform;
            var go = NightObject(scene, t.position, t.rotation, t.lossyScale);
            Collider copy = null;
            switch (c)
            {
                case BoxCollider b: { var x = go.AddComponent<BoxCollider>(); x.center = b.center; x.size = b.size; copy = x; break; }
                case SphereCollider sp: { var x = go.AddComponent<SphereCollider>(); x.center = sp.center; x.radius = sp.radius; copy = x; break; }
                case CapsuleCollider cp: { var x = go.AddComponent<CapsuleCollider>(); x.center = cp.center; x.radius = cp.radius; x.height = cp.height; x.direction = cp.direction; copy = x; break; }
                case MeshCollider mc: { var x = go.AddComponent<MeshCollider>(); x.convex = mc.convex; x.sharedMesh = mc.sharedMesh; copy = x; break; }
            }
            if (copy == null) continue;
            source[copy] = PathOf(t) + " [" + ColliderKind(c) + ", night object]";
            result.nightObjectColliders++;
        }
    }

    static GameObject NightObject(Scene scene, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var go = new GameObject("night") { hideFlags = HideFlags.HideAndDontSave, layer = NightLayer };
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        return go;
    }

    static string GroundSource(RaycastHit[] hits, int count, float y, Dictionary<Collider, string> source)
    {
        for (int k = 0; k < count; k++)
            if (Mathf.Abs(hits[k].point.y - y) < .01f && hits[k].collider != null)
                return source.TryGetValue(hits[k].collider, out var s) ? s : hits[k].collider.name;
        return "?";
    }

    static string ColliderKind(Collider c) => c switch
    {
        MeshCollider m => m.convex ? "convex mesh" : "mesh",
        BoxCollider _ => "box",
        CapsuleCollider _ => "capsule",
        SphereCollider _ => "sphere",
        _ => c.GetType().Name,
    };

    // ------------------------------------------------------------------ reporting

    public static void Describe(Result r, StringBuilder report)
    {
        report.AppendLine($"District (the 9 blocks): x {District.xMin:0}..{District.xMax:0}, z {District.yMin:0}..{District.yMax:0}; swept {Margin:0} m beyond it, {r.width} x {r.height} spots of {Cell:0.00} m.");
        report.AppendLine($"Ace's body: {r.body.from}.");
        report.AppendLine($"  Swept as a capsule of radius {r.body.radius:0.##} m clear from {r.body.step:0.##} m to {r.body.height:0.##} m above the ground; steps up to {r.body.step:0.##} m, slopes up to {r.body.slope:0}°.");
        report.AppendLine($"The visible world: {r.visualMeshes} meshes rebuilt as exact collision in a preview scene{(r.visualFailed > 0 ? $" ({r.visualFailed} could not be)" : "")}" +
                          $"{(r.visualNoTriangle > 0 ? $" ({r.visualNoTriangle} left out: not one real triangle, so nothing to see or touch)" : "")}; " +
                          $"{(r.separatePhysics ? "its own physics" : "shared physics, kept apart by layer")}. Colliders left out as moving (walkers, cars, visitors, Ace): {r.physicsIgnored}.");
        report.AppendLine();
        int reached = r.ReachedCount, outside = r.ReachedOutside;
        report.AppendLine($"Spots: open {r.Count(Kind.Open)}, walk-through {r.Count(Kind.WalkThrough)}, air wall {r.Count(Kind.AirWall)}, hole {r.Count(Kind.Hole)}, " +
                          $"invisible floor {r.Count(Kind.InvisibleFloor)}, solid {r.Count(Kind.Solid)}, nothing {r.Count(Kind.Nothing)}.");
        report.AppendLine($"From the café's front pavement Ace can reach {reached} spots ({reached * Cell * Cell:0} m²), {outside} of them outside the district.");
        Rect box = r.ReachedBox;
        int border = r.ReachedAtBorder;
        report.AppendLine(border == 0
            ? $"ENCLOSED: all of it lies within x {box.xMin:0.0}..{box.xMax:0.0}, z {box.yMin:0.0}..{box.yMax:0.0}, and none of it reaches the edge of the swept area ({Margin:0} m beyond the district)."
            : $"NOT ENCLOSED: {border} reachable spots lie on the edge of the swept area ({Margin:0} m beyond the district), so Ace can walk on out of it.");
        report.AppendLine();
        report.AppendLine($"WAYS OUT OF THE DISTRICT: {r.leaks.Count} (reachable ground beyond its box; the night's closing lines stand a little further out, so check each against them)");
        foreach (var leak in r.leaks)
            report.AppendLine($"  {leak.street}: crosses the {leak.side} at {leak.from:0.0}..{leak.to:0.0} ({leak.to - leak.from + Cell:0.0} m wide); " +
                              $"{leak.cells} spots outside, x {leak.min.x:0.0}..{leak.max.x:0.0}, z {leak.min.y:0.0}..{leak.max.y:0.0}");
        report.AppendLine();
        List("AIR WALLS Ace could bump into, outside the café room (collider, spots)", Outside(r.airWallTouching, r), r, report, 80);
        List("WALK-THROUGHS on ground Ace can reach, outside the café room (visible mesh, spots)", Outside(r.walkThroughReached, r), r, report, 120);
        List("HOLES next to where Ace can go, outside the café room (visible ground with no collision, spots)", Outside(r.holeTouching, r), r, report, 40);
        report.AppendLine($"Inside the café room (Ace's day workplace, its own collision, unchanged at night): {r.airWallTouching.Count - Outside(r.airWallTouching, r).Count} air walls, " +
                          $"{r.walkThroughReached.Count - Outside(r.walkThroughReached, r).Count} walk-throughs, {r.holeTouching.Count - Outside(r.holeTouching, r).Count} holes.");
        report.AppendLine();
        List($"HIGH SPOTS Ace can reach (more than {HighAbove:0} m above the café's front pavement, which is at {r.streetLevel:0.00} m; ground, spots)", r.high, r, report, 60);
        List("All air walls, reachable or not (collider, spots)", r.airWall, r, report, 40);
    }

    static Dictionary<string, int> Outside(Dictionary<string, int> items, Result r)
    {
        var room = CafeDaylight.CafeInside;
        var outside = new Dictionary<string, int>();
        foreach (var pair in items)
        {
            var at = r.whereNear.TryGetValue(pair.Key, out var near) ? near : r.where.TryGetValue(pair.Key, out var w) ? w : Vector2.zero;
            if (at.x > room.xMin - .5f && at.x < room.xMax + .5f && at.y > room.yMin - .5f && at.y < room.yMax + .5f) continue;
            outside[pair.Key] = pair.Value;
        }
        return outside;
    }

    static void List(string title, Dictionary<string, int> items, Result r, StringBuilder report, int max)
    {
        report.AppendLine($"{title}: {items.Count}");
        foreach (var pair in items.OrderByDescending(p => p.Value).Take(max))
        {
            var at = r.whereNear.TryGetValue(pair.Key, out var near) ? near : r.where.TryGetValue(pair.Key, out var w) ? w : Vector2.zero;
            report.AppendLine($"  {pair.Value,5}  ({at.x,6:0.0}, {at.y,6:0.0})  {pair.Key}");
        }
        if (items.Count > max) report.AppendLine($"  ... and {items.Count - max} more");
        report.AppendLine();
    }

    public static void Write(Result r, string folder, StringBuilder report)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        File.WriteAllBytes(Path.Combine(folder, "map.png"), Map(r));
        Csv(Path.Combine(folder, "air-walls.csv"), r.airWall, r, r.airWallTouching);
        Csv(Path.Combine(folder, "walk-throughs.csv"), r.walkThrough, r, r.walkThroughReached);
        Csv(Path.Combine(folder, "holes.csv"), r.hole, r, r.holeTouching);
        var leaks = new StringBuilder("street,side,from,to,width,cells,xmin,xmax,zmin,zmax\n");
        foreach (var l in r.leaks)
            leaks.AppendLine(string.Join(",", Q(l.street), Q(l.side), F(l.from), F(l.to), F(l.to - l.from + Cell), l.cells, F(l.min.x), F(l.max.x), F(l.min.y), F(l.max.y)));
        File.WriteAllText(Path.Combine(folder, "ways-out.csv"), leaks.ToString());
        // The raw grid, for tools outside Unity: kind (0-6), level (m), reached (0/1).
        var grid = new StringBuilder($"# {r.width} {r.height} {F(r.x0)} {F(r.z0)} {F(Cell)}\n");
        for (int j = 0; j < r.height; j++)
        {
            for (int i = 0; i < r.width; i++)
            {
                int index = r.Index(i, j);
                if (i > 0) grid.Append(' ');
                grid.Append((int)r.kind[index]).Append(':').Append(r.level[index].ToString("0.00", CultureInfo.InvariantCulture)).Append(':').Append(r.reached[index] ? 1 : 0);
            }
            grid.Append('\n');
        }
        File.WriteAllText(Path.Combine(folder, "grid.txt"), grid.ToString());
    }

    static void Csv(string path, Dictionary<string, int> all, Result r, Dictionary<string, int> near)
    {
        var sb = new StringBuilder("object,spots,spots_near_ace,x,z\n");
        foreach (var pair in all.OrderByDescending(p => p.Value))
        {
            var at = r.whereNear.TryGetValue(pair.Key, out var n2) ? n2 : r.where.TryGetValue(pair.Key, out var w) ? w : Vector2.zero;
            sb.AppendLine(string.Join(",", Q(pair.Key), pair.Value, near.TryGetValue(pair.Key, out int m) ? m : 0, F(at.x), F(at.y)));
        }
        File.WriteAllText(path, sb.ToString());
    }

    static byte[] Map(Result r)
    {
        const int scale = 2;
        int w = r.width * scale, h = r.height * scale;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color32[w * h];
        for (int j = 0; j < r.height; j++)
            for (int i = 0; i < r.width; i++)
            {
                int index = r.Index(i, j);
                bool reached = r.reached[index], inside = District.Contains(r.Centre(i, j));
                Color32 c;
                switch (r.kind[index])
                {
                    case Kind.Open: c = reached ? (inside ? new Color32(214, 214, 220, 255) : new Color32(235, 60, 60, 255)) : new Color32(92, 92, 100, 255); break;
                    case Kind.InvisibleFloor: c = reached ? (inside ? new Color32(150, 215, 150, 255) : new Color32(235, 60, 60, 255)) : new Color32(70, 110, 70, 255); break;
                    case Kind.WalkThrough: c = reached ? (inside ? new Color32(40, 205, 235, 255) : new Color32(235, 60, 60, 255)) : new Color32(30, 105, 125, 255); break;
                    case Kind.AirWall: c = new Color32(255, 185, 0, 255); break;
                    case Kind.Hole: c = new Color32(235, 0, 235, 255); break;
                    case Kind.Solid: c = new Color32(30, 30, 36, 255); break;
                    default: c = new Color32(8, 8, 10, 255); break;
                }
                for (int b = 0; b < scale; b++)
                    for (int a = 0; a < scale; a++)
                        px[(j * scale + b) * w + i * scale + a] = c;
            }
        // The district's edge in green, the start in blue.
        Color32 green = new Color32(40, 220, 90, 255), blue = new Color32(40, 90, 255, 255);
        int u0 = Mathf.RoundToInt((District.xMin - r.x0) / Cell * scale), u1 = Mathf.RoundToInt((District.xMax - r.x0) / Cell * scale);
        int v0 = Mathf.RoundToInt((District.yMin - r.z0) / Cell * scale), v1 = Mathf.RoundToInt((District.yMax - r.z0) / Cell * scale);
        for (int u = u0; u <= u1; u++) { Put(px, w, h, u, v0, green); Put(px, w, h, u, v1, green); }
        for (int v = v0; v <= v1; v++) { Put(px, w, h, u0, v, green); Put(px, w, h, u1, v, green); }
        if (r.startIndex >= 0)
        {
            int si = r.startIndex % r.width * scale, sj = r.startIndex / r.width * scale;
            for (int b = -3; b <= 4; b++) for (int a = -3; a <= 4; a++) Put(px, w, h, si + a, sj + b, blue);
        }
        tex.SetPixels32(px);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return png;
    }

    static void Put(Color32[] px, int w, int h, int u, int v, Color32 c) { if (u >= 0 && v >= 0 && u < w && v < h) px[v * w + u] = c; }

    // ------------------------------------------------------------------ what part 2 has to build with

    static void NavMeshSettings(StringBuilder report)
    {
        report.AppendLine("THE PEOPLE'S ROUTES (NavMesh surfaces):");
        bool any = false;
        foreach (var mb in CityPackChecks.InScene<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "NavMeshSurface") continue;
            any = true;
            var so = new SerializedObject(mb);
            string Get(string name)
            {
                var p = so.FindProperty(name);
                if (p == null) return "?";
                return p.propertyType switch
                {
                    SerializedPropertyType.Enum => p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString(),
                    SerializedPropertyType.Integer => p.intValue.ToString(),
                    SerializedPropertyType.LayerMask => p.intValue.ToString(),
                    SerializedPropertyType.ObjectReference => p.objectReferenceValue != null ? p.objectReferenceValue.name : "none",
                    _ => p.propertyType.ToString(),
                };
            }
            report.AppendLine($"  '{PathOf(mb.transform)}': collect {Get("m_CollectObjects")}, geometry {Get("m_UseGeometry")}, layers {Get("m_LayerMask")}, data {Get("m_NavMeshData")}.");
        }
        if (!any) report.AppendLine("  none found.");
        report.AppendLine("  (Built from physics colliders, a re-bake picks up any active collider; the night's objects are inactive by day, so a re-bake never sees them.)");
    }

    static readonly string[] WorksPropNames =
    {
        "SM_Prop_Barrier_01", "SM_Prop_Cone_01", "SM_Prop_Cone_02", "SM_Prop_Sign_Warning_01", "SM_Prop_Sign_Arrow_01", "SM_Prop_Sign_Stop_01",
        "SM_Env_Sidewalk_Construction_01", "SM_Env_Road_Patch_01", "SM_Env_Road_Bare_01", "SM_Env_Street_Divider_01", "SM_Env_Street_Divider_02",
        "SM_Env_Fence_01", "SM_Env_Fence_End_01", "SM_Prop_Pipe_Preset_01", "SM_Prop_Pipe_Preset_02", "SM_Prop_Pipe_Preset_03",
        "SM_Prop_Pipe_Part_Straight_01", "SM_Prop_Pipe_Small_01", "SM_Prop_Pallet_01", "SM_Prop_Skip_01", "SM_Prop_Skip_02",
        "SM_Prop_Manhole_01", "SM_Prop_Manhole_02", "SM_Veh_Car_Van_01", "SM_Prop_Trashbin_01",
        "SM_Gen_Prop_Barrel_Metal_01", "SM_Gen_Prop_Barrel_Metal_02", "SM_Gen_Prop_Crate_01", "SM_Gen_Prop_Crate_Preset_01", "SM_Gen_Prop_Plank_01",
        "SM_Gen_Prop_Plank_02", "SM_Gen_Prop_Sack_Stack_01", "SM_Gen_Prop_Sack_Stack_02", "SM_Gen_Prop_Light_Wall_01", "SM_Gen_Prop_Light_Roof_01",
        "SM_Gen_Prop_Rope_01", "SM_Gen_Prop_Cardboard_Box_Preset_01",
    };

    static void WorksProps(StringBuilder report)
    {
        report.AppendLine("WHAT THE WORKS CAN BE BUILT FROM (size x, y, z in m; the pivot's offset from the bottom centre; collision):");
        foreach (string name in WorksPropNames)
        {
            string path = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            if (path == null) { report.AppendLine($"  {name}: not found"); continue; }
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var rs = root.GetComponentsInChildren<Renderer>(true);
                Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(root.transform.position, Vector3.zero);
                foreach (var rr in rs) b.Encapsulate(rr.bounds);
                var cols = root.GetComponentsInChildren<Collider>(true).Select(c => ColliderKind(c) + (c.isTrigger ? " trigger" : "")).ToList();
                Vector3 offset = root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                report.AppendLine($"  {name}: {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}; pivot {offset.x:0.00}, {offset.y:0.00}, {offset.z:0.00}; " +
                                  $"collision {(cols.Count == 0 ? "none" : string.Join(", ", cols.GroupBy(c => c).Select(g => g.Count() + " " + g.Key)))}; {path}");
            }
            catch (Exception e) { report.AppendLine($"  {name}: could not open ({e.Message})"); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    // ------------------------------------------------------------------ utils

    public static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
}
