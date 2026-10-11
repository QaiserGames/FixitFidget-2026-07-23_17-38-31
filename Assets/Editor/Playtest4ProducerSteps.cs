using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// PLAYTEST 4, the Producer's own small steps (10 Oct 2026): the seams between the leads' work.
//
//   1 - The pocket watch rests on its surface (prefab). The Tray lead measured it: the watch's back screw heads sit
//       21.7 mm under its anchor, so at JobBase.restHeight 0.01 it sinks 6.7 mm into the counter's stone (11.7 mm into
//       any surface at its spot's height) and 14.7 mm into the new intake tray's felt. At 0.025 it rests 0.3 mm over the
//       felt. restHeight is read by PlacementJitter (the intake slots) and PlayerCarry (setting a held thing down).
//
//   0 - The café floor's steps (read-only) and
//   2 - Nobody steps up onto a plant pot (scene, re-bakes the routes). The lab sweep after the six leads (10 Oct, 23:54)
//       ran Checks › Café life setup, which nobody had run since the café redesign: "No walkable NavMesh between 0.2 and
//       0.9 m that people can walk up onto" FAILED, 13 connected samples, the highest 0.44 m at (-7.0, 12.5). It passed at
//       14:15, before Seats 1; Seats 1's own plateau check passed too (14:13). Room 1 (14:25) put the monstera at the
//       counter's west end, (-6.80, 12.60), with a capsule on its pot (r 0.20, 0.40 tall), and re-baked the routes; its
//       group has no Not Walkable modifier, as the seats' and the tables' groups do, so the top of the capsule baked as
//       a step a customer can walk up onto. Step 0 finds every such step and what stands under it; step 2 gives each
//       owning group of Room 1 the seats' Not Walkable modifier, re-bakes the routes, and checks: no step left, every
//       seat, waiting spot, queue place and station still reached from the spawn, the occupied-circulation guard, and
//       the routes changed only round the pots (the routes' height on a 0.25 m grid, before and after: a pot people walk
//       round takes its own reach plus the agent's radius out of the routes, about 1 m² a pot; the ramps the bake had laid
//       up its sides come down to the floor; nothing else may move and nothing may go up).
// ---------------------------------------------------------------------------
public static class Playtest4ProducerSteps
{
    const string Area = Playtest4Shared.Menu + "Producer/";
    const string WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab";
    const float WatchRest = 0.025f;

    [MenuItem(Area + "1 - The pocket watch rests on its surface (prefab)", false, 1)]
    static void WatchRestsOnTheFelt()
    {
        float was = float.NaN;
        Playtest4Shared.Step("Playtest 4 › Producer › 1 - The pocket watch rests on its surface (prefab)", "producer-1-watch-rest",
            survey: run =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
                run.Require(prefab != null, "No prefab at " + WatchPath + ".");
                JobBase job = prefab.GetComponent<JobBase>();
                run.Require(job != null, WatchPath + " has no JobBase on its root.");
                was = job.restHeight;
                run.Info($"{WatchPath}: restHeight {was:0.###} m (will be {WatchRest:0.###})");
            },
            change: run =>
            {
                if (Mathf.Approximately(was, WatchRest))
                {
                    run.Info("already " + WatchRest.ToString("0.###"));
                    return;
                }
                Playtest4Shared.EditPrefab(run, WatchPath,
                    root =>
                    {
                        var so = new SerializedObject(root.GetComponent<JobBase>());
                        so.FindProperty("restHeight").floatValue = WatchRest;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    },
                    root => Mathf.Approximately(root.GetComponent<JobBase>().restHeight, WatchRest));
                run.Changed($"{WatchPath} JobBase.restHeight: {was:0.###} → {WatchRest:0.###}");
            },
            checks: run =>
            {
                run.Check(!float.IsNaN(was), "the watch's rest height was read");
            });
    }

    [MenuItem(Area + "1 - The pocket watch rests on its surface (prefab)", true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();

    // ================================================================== the café floor's steps

    const string RedesignRoot = "30 - cafe redesign";
    const string Room1Prefix = "Room 1 - ";
    static readonly Vector3 Door = new Vector3(0f, 0f, 1.5f);

    /// <summary>A walkable step at seat or table height that someone can walk up onto, what stands under it, and Room 1's
    /// pieces within 0.6 m of it (the bake climbs a pot's rounded capsule as a ramp, so a step's points lie beside the pot
    /// as well as on it).</summary>
    sealed class Plateau
    {
        public Vector3 at;
        public int samples;
        public Collider under;
        public readonly List<Collider> nearRoom1 = new List<Collider>();
    }

    // The café life setup's test (NpcLifeChecks), on its own grid or a finer one: NavMesh between 0.2 and 0.9 m that a
    // complete path from the door reaches (an unreachable table top is an island, ignored). Samples that snap to the same
    // point (within 5 cm) are one step.
    static List<Plateau> FindPlateaus(float grid, out int islands, out int samples)
    {
        var found = new List<Plateau>();
        islands = 0;
        samples = 0;
        var path = new NavMeshPath();
        for (float z = .5f; z <= 12.5f + 1e-3f; z += grid)
            for (float x = -7.2f; x <= 7.2f + 1e-3f; x += grid)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit h, .6f, NavMesh.AllAreas)) continue;
                if (h.position.y <= .2f || h.position.y >= .9f) continue;
                bool connected = NavMesh.CalculatePath(Door, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                if (!connected) { islands++; continue; }
                samples++;
                Plateau same = found.FirstOrDefault(p => (p.at - h.position).sqrMagnitude < .05f * .05f);
                if (same != null) { same.samples++; continue; }
                var step = new Plateau { at = h.position, samples = 1, under = Under(h.position) };
                foreach (Collider c in Physics.OverlapSphere(h.position, .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (c != null && Room1Group(c) != null && !step.nearRoom1.Contains(c)) step.nearRoom1.Add(c);
                found.Add(step);
            }
        return found;
    }

    // The solid collider right under a point of the NavMesh (triggers and the player's body aren't furniture).
    static Collider Under(Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * .3f, Vector3.down, .7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        return hits.Where(hit => hit.collider != null && hit.collider.GetComponentInParent<PlayerMovement>() == null)
                   .OrderBy(hit => hit.distance).Select(hit => hit.collider).FirstOrDefault();
    }

    // The group of Room 1 that owns a collider: its ancestor whose parent is Room 1's own group (under the redesign's root).
    static Transform Room1Group(Collider c)
    {
        if (c == null) return null;
        for (Transform t = c.transform; t != null && t.parent != null; t = t.parent)
        {
            Transform parent = t.parent;
            if (parent.name.StartsWith(Room1Prefix, System.StringComparison.Ordinal) && parent.parent != null && parent.parent.name == RedesignRoot)
                return t;
        }
        return null;
    }

    static float WalkableArea()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        float area = 0f;
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            Vector3 a = tri.vertices[tri.indices[i]], b = tri.vertices[tri.indices[i + 1]], c = tri.vertices[tri.indices[i + 2]];
            area += Vector3.Cross(b - a, c - a).magnitude * .5f;
        }
        return area;
    }

    // The café's routes on a 0.25 m grid (x -7.4..7.4, z 0..18): for each cell whose centre the NavMesh covers (within 2 cm,
    // flat), the height the routes have there. Compared before and after a re-bake, it says WHERE and HOW the routes
    // changed: a cell that left them, joined them, came down off a ramp, or went up onto a step.
    static Dictionary<Vector2Int, float> RouteHeights()
    {
        var cells = new Dictionary<Vector2Int, float>();
        for (int i = 0; i <= 59; i++)
            for (int j = 0; j <= 72; j++)
            {
                float x = -7.4f + i * .25f, z = j * .25f;
                if (!NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit h, .6f, NavMesh.AllAreas)) continue;
                float dx = h.position.x - x, dz = h.position.z - z;
                if (dx * dx + dz * dz < .02f * .02f) cells[new Vector2Int(i, j)] = h.position.y;
            }
        return cells;
    }

    const float FloorHigh = .12f;   // the routes over the bare floor sit under this; anything higher is on something

    static Vector2 CellCentre(Vector2Int c) => new Vector2(-7.4f + c.x * .25f, c.y * .25f);

    // The solid pieces under the groups (their colliders), flat: centre and reach.
    static List<(string name, Vector2 at, float reach)> Pieces(IEnumerable<Transform> groups)
    {
        var list = new List<(string, Vector2, float)>();
        foreach (Transform g in groups)
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy)
                    list.Add((c.name, new Vector2(c.bounds.center.x, c.bounds.center.z), Mathf.Max(c.bounds.extents.x, c.bounds.extents.z)));
        return list;
    }

    static string Where(Plateau p) =>
        $"({p.at.x:0.00}, {p.at.y:0.00}, {p.at.z:0.00}) from {p.samples} sample(s), on {(p.under != null ? p.under.name + " · " + p.under.GetType().Name : "nothing solid under it")}" +
        $"; Room 1's pieces within 0.6 m: {(p.nearRoom1.Count == 0 ? "none" : string.Join(", ", p.nearRoom1.Select(c => $"{c.name} · {c.GetType().Name} ({Room1Group(c).name})")))}";

    static void ReportPlateaus(Playtest4Shared.Run run, string grid, List<Plateau> plateaus, int islands, int samples)
    {
        run.Info($"{grid}: {plateaus.Count} step(s) people can walk up onto ({samples} connected sample(s)); {islands} unreachable island sample(s) ignored");
        foreach (Plateau p in plateaus) run.Info("  step at " + Where(p));
    }

    [MenuItem(Area + "0 - The café floor's steps (read-only)", false, 0)]
    static void FloorSteps()
    {
        Playtest4Shared.Survey("Playtest 4 › Producer › 0 - The café floor's steps (read-only)", "producer-0-floor-steps", run =>
        {
            List<Plateau> coarse = FindPlateaus(.5f, out int islands, out int samples);
            ReportPlateaus(run, "The café life setup's grid (0.5 m)", coarse, islands, samples);
            List<Plateau> fine = FindPlateaus(.25f, out int fineIslands, out int fineSamples);
            ReportPlateaus(run, "A finer grid (0.25 m)", fine, fineIslands, fineSamples);
            run.Info($"The walkable floor: {WalkableArea():0.00} m² of NavMesh");
            run.Check(coarse.Count == 0 && fine.Count == 0, "No walkable step between 0.2 and 0.9 m that people can walk up onto");
        });
    }

    [MenuItem(Area + "0 - The café floor's steps (read-only)", true)]
    static bool CanSurvey() => Playtest4Shared.NotPlaying();

    [MenuItem(Area + "2 - Nobody steps up onto a plant pot (scene, re-bakes the routes)", false, 2)]
    static void NoSteps()
    {
        var groups = new List<Transform>();
        float areaBefore = 0f, agentRadius = .35f;
        string layoutBefore = "", circulationBefore = "";
        Dictionary<Vector2Int, float> cellsBefore = null;
        List<(string name, Vector2 at, float reach)> pieces = null;
        Playtest4Shared.Step("Playtest 4 › Producer › 2 - Nobody steps up onto a plant pot (scene, re-bakes the routes)", "producer-2-no-steps",
            survey: run =>
            {
                run.Require(Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude).Length == 1, "Expected one NavMeshSurface (the café's routes) in the scene.");
                List<Plateau> plateaus = FindPlateaus(.25f, out int islands, out int samples);
                ReportPlateaus(run, "The finer grid (0.25 m)", plateaus, islands, samples);
                // The groups of Room 1 whose pieces the steps lie on or beside. The checks, not this list, decide: if a step is
                // left after the re-bake, the whole change is undone and the routes are baked back as they were.
                foreach (Plateau p in plateaus)
                    foreach (Collider c in p.nearRoom1)
                    {
                        Transform g = Room1Group(c);
                        if (g != null && !groups.Contains(g)) groups.Add(g);
                    }
                run.Require(plateaus.Count == 0 || groups.Count > 0,
                    "The steps lie near no piece of Room 1: this step only answers for Room 1's pieces. Nothing changed.");
                areaBefore = WalkableArea();
                cellsBefore = RouteHeights();
                pieces = Pieces(groups);
                NavMeshSurface routes = Object.FindAnyObjectByType<NavMeshSurface>();
                agentRadius = NavMesh.GetSettingsByID(routes.agentTypeID).agentRadius;
                run.Info($"The routes' agent: radius {agentRadius:0.00} m; the routes cover {cellsBefore.Count} cells of 0.25 m, " +
                         $"{cellsBefore.Count(c => c.Value >= FloorHigh)} of them higher than the bare floor");
                foreach (var piece in pieces) run.Info($"  a solid piece in those groups: {piece.name} at ({piece.at.x:0.00}, {piece.at.y:0.00}), reaching {piece.reach:0.00} m");
                layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
                circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                run.Info($"The walkable floor: {areaBefore:0.00} m²");
                run.Info("Before: " + layoutBefore);
                run.Info("Before: " + circulationBefore);
                run.Info(groups.Count == 0 ? "No step to take away." : "Room 1's groups to make Not Walkable for the routes: " + string.Join(", ", groups.Select(g => Playtest4Shared.PathOf(g.gameObject))));
            },
            change: run =>
            {
                if (groups.Count == 0) return;
                foreach (Transform g in groups)
                {
                    NavMeshModifier modifier = g.GetComponent<NavMeshModifier>();   // (not ??: a missing component is Unity's fake null)
                    if (modifier == null) modifier = run.Add<NavMeshModifier>(g.gameObject, "Not Walkable for the routes, as the seats' and the tables' groups");
                    run.Record(modifier);
                    modifier.overrideArea = true;
                    modifier.area = 1;              // Not Walkable
                    modifier.ignoreFromBuild = false;
                    modifier.applyToChildren = true;
                    EditorUtility.SetDirty(modifier);
                    run.Changed($"{Playtest4Shared.PathOf(g.gameObject)} · NavMeshModifier: override area, Not Walkable, applied to its children");
                }
                // The bake writes its own asset (Assets/Playtests/AcesCafeLayout/Cafe routes.asset), outside the undo: if any
                // check fails, the scene's change is undone first and then the routes are baked again from the scene as it was.
                run.OnFail(AcesCafeLayoutSetup.BakeRoutes);
                AcesCafeLayoutSetup.BakeRoutes();
                run.Changed("the café's routes re-baked (AcesCafeLayoutSetup.BakeRoutes: physics colliders, 0.08 m voxels)");
                NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
                if (surface != null && surface.navMeshData != null) run.SaveAsset(surface.navMeshData);
            },
            checks: run =>
            {
                List<Plateau> coarse = FindPlateaus(.5f, out int islands, out int samples);
                run.Check(coarse.Count == 0, $"the café life setup's test: no walkable NavMesh between 0.2 and 0.9 m that people can walk up onto ({samples} connected samples; {islands} unreachable island samples ignored)");
                List<Plateau> fine = FindPlateaus(.25f, out int fineIslands, out int fineSamples);
                run.Check(fine.Count == 0, $"on a 0.25 m grid too ({fineSamples} connected samples{(fine.Count > 0 ? ", the first at " + Where(fine[0]) : "")}; {fineIslands} island samples ignored)");
                string layout = AcesCafeLayoutSetup.ValidateLayout();
                run.Check(layout.Contains("24 seats") && layout.EndsWith("PASS", System.StringComparison.Ordinal),
                    "every seat, waiting spot, queue place and station is still reached from the spawn: " + layout);
                string circulation = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                run.Check(circulation.Contains("31/31") && circulation.Contains("PASS"), "the occupied-circulation guard: " + circulation);
                // Where and how the routes changed. A pot that is Not Walkable is walked round: the floor within its reach plus
                // the agent's radius leaves the routes; the ramps the bake had laid up its rounded sides (sloped polygons
                // reaching a metre and more over the bare floor) come down to the floor. Nothing else may change, and no cell
                // may go up.
                Dictionary<Vector2Int, float> cellsAfter = RouteHeights();
                float margin = agentRadius + .2f;
                bool NearAPot(Vector2Int c) => pieces.Any(piece => Vector2.Distance(CellCentre(c), piece.at) <= piece.reach + margin);
                float FromThePots(Vector2Int c) => pieces.Count == 0 ? 0f : pieces.Min(piece => Vector2.Distance(CellCentre(c), piece.at));
                string Cells(IEnumerable<Vector2Int> list) => string.Join(", ", list.Take(8).Select(c => $"({CellCentre(c).x:0.00}, {CellCentre(c).y:0.00})"));
                var lost = cellsBefore.Keys.Where(c => !cellsAfter.ContainsKey(c)).ToList();
                var joined = cellsAfter.Keys.Where(c => !cellsBefore.ContainsKey(c)).ToList();
                var lowered = cellsBefore.Keys.Where(c => cellsAfter.ContainsKey(c) && cellsBefore[c] >= FloorHigh && cellsAfter[c] < FloorHigh).ToList();
                var raised = cellsBefore.Keys.Where(c => cellsAfter.ContainsKey(c) && cellsBefore[c] < FloorHigh && cellsAfter[c] >= FloorHigh).ToList();
                // A re-bake of the very same scene can move the routes' edge by a voxel (0.08 m), so a cell whose centre lies on
                // the edge (a neighbour on the other side of it, before) may flip either way. A flip anywhere else, or more than a
                // few, is a real change.
                bool OnTheEdge(Vector2Int c)
                {
                    bool inside = cellsBefore.ContainsKey(c);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            if ((dx != 0 || dz != 0) && cellsBefore.ContainsKey(new Vector2Int(c.x + dx, c.y + dz)) != inside) return true;
                    return false;
                }
                var lostElsewhere = lost.Where(c => !NearAPot(c)).ToList();
                var joinedElsewhere = joined.Where(c => !NearAPot(c)).ToList();
                var edgeFlips = lostElsewhere.Concat(joinedElsewhere).Where(OnTheEdge).ToList();
                var realElsewhere = lostElsewhere.Concat(joinedElsewhere).Where(c => !OnTheEdge(c)).ToList();
                run.Check(realElsewhere.Count == 0 && edgeFlips.Count <= 4,
                    $"the routes changed only round the pots: {lost.Count} cell(s) of 0.25 m left them and {joined.Count} joined, all within a pot's reach plus {margin:0.00} m" +
                    (edgeFlips.Count > 0 ? $", but for {edgeFlips.Count} on the routes' edge elsewhere, as a re-bake moves it by a voxel ({Cells(edgeFlips)})" : "") +
                    (realElsewhere.Count > 0 ? "; ELSEWHERE, off the edge: " + Cells(realElsewhere) : ""));
                run.Check(raised.Count == 0, $"no cell of the routes went up off the floor ({raised.Count}{(raised.Count > 0 ? ": " + Cells(raised) : "")})");
                run.Info($"{lowered.Count} cell(s) came down off the ramps to the floor" +
                         (lowered.Count > 0 ? $", the farthest {lowered.Max(FromThePots):0.00} m from a pot's centre" : ""));
                run.Check(cellsAfter.Count(c => c.Value >= FloorHigh) == 0 || cellsAfter.Where(c => c.Value >= FloorHigh).All(c => cellsBefore.TryGetValue(c.Key, out float was) && was >= FloorHigh),
                    $"what is still higher than the bare floor ({cellsAfter.Count(c => c.Value >= FloorHigh)} cell(s)) was so before (the counter's foot, a doormat's lip: not this step's)");
                float areaAfter = WalkableArea();
                float bound = pieces.Sum(piece => Mathf.PI * (piece.reach + agentRadius) * (piece.reach + agentRadius));
                run.Check(areaAfter <= areaBefore && areaBefore - areaAfter <= bound * 1.25f,
                    $"the walkable floor: {areaBefore:0.00} → {areaAfter:0.00} m² ({areaAfter - areaBefore:+0.00;-0.00;0.00}); walking round the pots costs at most " +
                    $"{bound:0.00} m² (each pot's reach plus the agent's {agentRadius:0.00} m), and the ramps over them came off too");
            });
    }

    [MenuItem(Area + "2 - Nobody steps up onto a plant pot (scene, re-bakes the routes)", true)]
    static bool CanFix() => Playtest4Shared.NotPlaying();
}
