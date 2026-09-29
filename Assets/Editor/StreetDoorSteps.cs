#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Real front doors for the street (27 Sept; Mansoor: "lets now add doors for
// these houses. so we can see them leave and enter more believably"; he chose
// a real doorway with the door swinging in, for all six bay-window houses and
// the courtyard shop the walk-ins come out of).
//
// The street builder (CafeStreetUpgrade) made each building from boxes and then
// merged them into one mesh per material. The door was a solid frame block with
// a leaf, glass, panel and knob stuck on its front, over a solid house. So:
//
//  1. The door itself (leaf, glass, panel or light, knob or pull) is lifted out
//     of the merged meshes, unchanged, onto a hinge at its edge: it looks exactly
//     as before while closed. StreetDoor swings it.
//  2. The solid frame block becomes a real frame (two jambs and a head, in the
//     same place and material) around an opening.
//  3. The opening is cut through the faces behind it (the painted front wall,
//     the stone foundation, the clapboard lips) and a short dark hall (1.6 m)
//     is added behind it, so an open door shows a dark inside, not a solid block.
//
// Every piece is found by its exact place and size before anything changes; a
// building where anything doesn't match is left alone and reported. New meshes
// go to Assets/Playtests/AcesCafeLayout/Street doors.asset (the originals in
// Street geometry.asset are never touched), and "Put the old doors back" undoes
// it all. New faces take their texture mapping from the faces they continue.
//
// Menus (Fixit Fidget > Night): Doors 1 (survey, read-only), Doors 2 (build),
// Doors 4 (mark where people wait for their turn at each door walk-ins use),
// Doors - Put the old doors back. The checks are in StreetDoorCheck (Doors 3,
// and Checks > Street doors (Play Mode, lab session)) and StreetDoorRushCheck
// (Checks > Street doors - rush hour at ...).
// ---------------------------------------------------------------------------
public static class StreetDoorSteps
{
    const string Menu = "Fixit Fidget/Night/";
    const string Tag = "[Street doors] ";
    const string Folder = "Assets/Playtests/AcesCafeLayout";
    const string StreetGeometry = Folder + "/Street geometry.asset";
    const string DoorsAsset = Folder + "/Street doors.asset";
    const string HallMaterialPath = Folder + "/Street doors - dark hall.mat";
    public const string CutSuffix = " (doorway)";
    public const string DoorName = "Front door (opens)";
    public const string HallName = "Doorway hall";
    public const float HallDepth = 1.6f;
    const float Eps = .004f;

    // The six bay-window houses across the west street, and the courtyard shop the
    // walk-ins from the east street come out of.
    public static readonly string[] Buildings =
    {
        "1 - Saffron bay-window house", "2 - Dusty rose bay-window house", "3 - Sea green bay-window house",
        "4 - Lavender bay-window house", "5 - Sky blue bay-window house", "6 - Saffron bay-window house",
        "Courtyard - 1 - neighborhood shop house",
    };

    // ================================================================== recipes

    // Where a building's front door is, in the building's own space (x across the
    // front, y up, z out towards the street), as the street builder made it.
    public sealed class Recipe
    {
        public string kind;
        public Bounds doorZone;              // every triangle wholly inside it is part of the door
        public Bounds frameBox;              // the old solid frame block
        public float left, right;            // the opening, across
        public float floor, top;             // the opening, up: the doorway's floor and the underside of the head
        public float hingeZ;                 // the leaf's back face; its hinge edge is at x = left
        public float leafWidth;
        public float frameBack, frameFront, frameOuterLeft, frameOuterRight, frameBottom, frameTop;
        public Bounds threshold;             // a stone step under the door
        public Bounds thresholdTemplate;     // a stone box whose faces give the threshold its texture mapping
        public float kickPlateBottom = float.NaN; // the shop's door stops short of the ground: extend it down to here
        public readonly List<Cut> cuts = new();
        public readonly List<Bounds> lips = new(); // thin boxes running across the opening, split either side of it
        public Vector3 doorway, hall;        // markers
    }

    public struct Cut
    {
        public int axis;     // the face's plane: 1 = horizontal (y = value), 2 = facing the street (z = value)
        public float value;
        public int sign;     // the face looks this way along the axis
        public Rect hole;    // what to cut out: (x, y) for axis 2, (x, z) for axis 1
        public string what;
    }

    public static Recipe RecipeFor(Transform building)
    {
        string name = building.name;
        if (name.EndsWith("bay-window house", StringComparison.Ordinal))
            return building.GetComponentInChildren<GraceHouse>(true) != null ? WidenedBayHouse() : BayHouse();
        if (name.EndsWith("neighborhood shop house", StringComparison.Ordinal))
        {
            float width = name.StartsWith("Courtyard", StringComparison.Ordinal) ? 5.95f
                        : name.StartsWith("Front", StringComparison.Ordinal) ? 5.9f : 5.85f;
            return Shop(width);
        }
        return null;
    }

    static Recipe BayHouse()
    {
        var r = new Recipe
        {
            kind = "bay-window house",
            left = 1.11f, right = 2.09f, floor = .15f, top = 2.34f, hingeZ = .17f, leafWidth = .98f,
            doorZone = MinMax(new Vector3(1.105f, .135f, .165f), new Vector3(2.095f, 2.345f, .38f)),
            frameBox = MinMax(new Vector3(.99f, .07f, 0f), new Vector3(2.21f, 2.55f, .20f)),
            frameBack = 0f, frameFront = .20f, frameOuterLeft = .99f, frameOuterRight = 2.21f, frameBottom = .07f, frameTop = 2.55f,
            threshold = MinMax(new Vector3(1.11f, .10f, 0f), new Vector3(2.09f, .15f, .24f)),
            thresholdTemplate = MinMax(new Vector3(.825f, .01f, .24f), new Vector3(2.375f, .15f, .60f)), // the top stoop step
            doorway = new Vector3(1.6f, .15f, .20f),
            hall = new Vector3(1.6f, .15f, -1.3f),
        };
        r.cuts.Add(new Cut { axis = 2, value = 0f, sign = 1, hole = Rect.MinMaxRect(r.left, r.floor, r.right, r.top), what = "the painted front wall" });
        r.cuts.Add(new Cut { axis = 2, value = .095f, sign = 1, hole = Rect.MinMaxRect(r.left, r.floor, r.right, .54f), what = "the stone foundation's front" });
        r.cuts.Add(new Cut { axis = 1, value = .54f, sign = 1, hole = Rect.MinMaxRect(r.left, -HallDepth, r.right, .095f), what = "the stone foundation's top" });
        // The clapboard lips (0.035 tall, from 0.8 m up every 0.25 m) run right across the doorway.
        for (int i = 0; i < 12; i++)
        {
            float y = .8f + .25f * i;
            if (y - .0175f >= r.top) break;
            r.lips.Add(MinMax(new Vector3(-2.8f, y - .0175f, 0f), new Vector3(2.8f, y + .0175f, .05f)));
        }
        return r;
    }

    /// <summary>
    /// Grace's saffron house since the break-ins (GraceHouseSteps): the doorway widened from 0.98 m to 1.30 m (a
    /// 1.0 m Ace fits through it), the cream surround moved out 0.16 m each side to the stoop's width, the leaf
    /// stretched to match, and real rooms behind it instead of the dark hall.
    /// </summary>
    public static Recipe WidenedBayHouse()
    {
        const float wider = .16f;
        Recipe r = BayHouse();
        r.kind = "bay-window house, widened for the break-ins";
        r.left -= wider; r.right += wider;
        r.leafWidth = r.right - r.left;
        r.frameOuterLeft -= wider; r.frameOuterRight += wider;
        r.frameBox = MinMax(r.frameBox.min - new Vector3(wider, 0f, 0f), r.frameBox.max + new Vector3(wider, 0f, 0f));
        r.doorZone = MinMax(r.doorZone.min - new Vector3(wider, 0f, 0f), r.doorZone.max + new Vector3(wider, 0f, 0f));
        r.threshold = MinMax(r.threshold.min - new Vector3(wider, 0f, 0f), r.threshold.max + new Vector3(wider, 0f, 0f));
        r.hall = new Vector3(1.76f, .15f, -1.16f);   // at the foot of her stairs, clear of the door's swing
        return r;
    }

    static Recipe Shop(float width)
    {
        float dx = width * .5f - .71f;
        var r = new Recipe
        {
            kind = "neighborhood shop",
            left = dx - .435f, right = dx + .435f, floor = 0f, top = 2.385f, hingeZ = .20f, leafWidth = .87f,
            frameBox = MinMax(new Vector3(dx - .55f, .095f, .03f), new Vector3(dx + .55f, 2.545f, .23f)),
            frameBack = .03f, frameFront = .23f, frameOuterLeft = dx - .55f, frameOuterRight = dx + .55f, frameBottom = 0f, frameTop = 2.545f,
            kickPlateBottom = .012f,
            doorway = new Vector3(dx, 0f, .23f),
            hall = new Vector3(dx, .012f, -1.3f),
        };
        r.doorZone = MinMax(new Vector3(r.left - .005f, .13f, .195f), new Vector3(r.right + .005f, 2.39f, .37f));
        r.threshold = MinMax(new Vector3(r.left, -.06f, r.frameBack), new Vector3(r.right, .012f, .31f));   // meets the hall floor at the frame's back
        r.thresholdTemplate = MinMax(new Vector3(-width * .5f, -3.3f, -5.2f), new Vector3(width * .5f, 0f, 0f)); // the street block foundation
        r.cuts.Add(new Cut { axis = 2, value = .09f, sign = 1, hole = Rect.MinMaxRect(r.left, 0f, r.right, r.top), what = "the stone shop surround" });
        r.cuts.Add(new Cut { axis = 2, value = -.04f, sign = -1, hole = Rect.MinMaxRect(r.left, 0f, r.right, r.top), what = "the back of the stone shop surround" });
        r.cuts.Add(new Cut { axis = 2, value = 0f, sign = 1, hole = Rect.MinMaxRect(r.left, 0f, r.right, r.top), what = "the painted front wall" });
        r.cuts.Add(new Cut { axis = 1, value = 0f, sign = 1, hole = Rect.MinMaxRect(r.left, -HallDepth, r.right, r.frameBack), what = "the foundation's top" });
        return r;
    }

    static Bounds MinMax(Vector3 min, Vector3 max)
    {
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    // ================================================================== menus

    [MenuItem(Menu + "Doors 1 - Survey the front doors (read-only)")]
    static void SurveyMenu() => Run(false);

    [MenuItem(Menu + "Doors 2 - Give the houses real front doors")]
    static void BuildMenu() => Run(true);

    [MenuItem(Menu + "Doors 1 - Survey the front doors (read-only)", true)]
    [MenuItem(Menu + "Doors 2 - Give the houses real front doors", true)]
    [MenuItem(Menu + "Doors 4 - Mark where people wait for their turn at the door", true)]
    [MenuItem(Menu + "Doors - Put the old doors back", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(bool build)
    {
        var report = new StringBuilder();
        int built = 0, skipped = 0;
        try
        {
            RequireScene();
            Material hallMaterial = build ? HallMaterial() : null;
            foreach (string name in Buildings)
            {
                Transform building = FindOptional(name);
                if (building == null) { report.AppendLine($"SKIP  {name}: not in the scene."); skipped++; continue; }
                if (building.Find(DoorName) != null) { report.AppendLine($"ok    {name}: already has a real front door."); continue; }
                Recipe recipe = RecipeFor(building);
                var plan = new Plan(building, recipe);
                string problem = plan.Find();
                report.AppendLine((problem == null ? (build ? "BUILD " : "ok    ") : "SKIP  ") + name + ": " + plan.Summary());
                if (problem != null) { report.AppendLine("      " + problem); skipped++; continue; }
                if (!build) continue;
                plan.Build(hallMaterial);
                built++;
            }
            if (build && built > 0)
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            string head = build ? $"Doors 2: {built} building(s) given a real front door, {skipped} skipped." : $"Doors 1 (survey): {skipped} building(s) would be skipped.";
            if (build && built > 0) head += " Save the scene (Ctrl+S).";
            if (skipped > 0) Debug.LogWarning(Tag + head + "\n" + report);
            else Debug.Log(Tag + head + "\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + (build ? "Build" : "Survey") + " FAILED: " + e.Message + "\n" + report + "\n" + e); }
    }

    [MenuItem(Menu + "Doors - Put the old doors back")]
    static void RestoreMenu()
    {
        try
        {
            RequireScene();
            Mesh[] originals = AssetDatabase.LoadAllAssetsAtPath(StreetGeometry).OfType<Mesh>().ToArray();
            var log = new StringBuilder();
            int restored = 0;
            foreach (string name in Buildings)
            {
                Transform building = FindOptional(name);
                if (building == null) continue;
                int meshes = 0;
                // The door keeps a record of the meshes it cut and what they were.
                StreetDoor record = building.Find(DoorName) != null ? building.Find(DoorName).GetComponent<StreetDoor>() : null;
                var before = new Dictionary<MeshFilter, Mesh>();
                if (record != null && record.cutParts != null && record.originalMeshes != null)
                    for (int i = 0; i < Mathf.Min(record.cutParts.Length, record.originalMeshes.Length); i++)
                        if (record.cutParts[i] != null && record.originalMeshes[i] != null) before[record.cutParts[i]] = record.originalMeshes[i];
                foreach (MeshFilter filter in Parts(building))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    int at = mesh.name.IndexOf(CutSuffix, StringComparison.Ordinal);
                    if (at < 0) continue;
                    if (!before.TryGetValue(filter, out Mesh back))
                    {
                        string original = mesh.name.Substring(0, at);
                        back = originals.FirstOrDefault(m => m.name == original);
                    }
                    if (back == null) throw new InvalidOperationException($"The original of \"{mesh.name}\" can't be found; nothing more was changed.");
                    Undo.RecordObject(filter, "Put the old doors back");
                    filter.sharedMesh = back;
                    meshes++;
                }
                bool had = false;
                foreach (string child in new[] { DoorName, HallName })
                {
                    Transform t = building.Find(child);
                    if (t == null) continue;
                    Undo.DestroyObjectImmediate(t.gameObject);
                    had = true;
                }
                if (meshes > 0 || had) { restored++; log.AppendLine($"{name}: {meshes} mesh(es) back to the originals{(had ? ", the hinged door and the hall removed" : "")}."); }
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + $"Put the old doors back on {restored} building(s). Save the scene (Ctrl+S).\n" + log);
        }
        catch (Exception e) { Debug.LogError(Tag + "Restore FAILED: " + e.Message); }
    }

    // ================================================================== the plan for one building

    // One merged mesh of the building (one material), worked on in the building's space.
    sealed class Work
    {
        public MeshFilter filter;
        public Mesh source;
        public Vector3[] v, n;
        public Vector2[] uv;
        public int[] t;
        public bool[] gone;            // triangle index / 3 -> removed from this mesh
        public bool[] toLeaf;          // ... and part of the door
        public readonly List<Vector3> addV = new(), addN = new();
        public readonly List<Vector2> addUV = new();
        public readonly List<int> addT = new();
        public bool changed;
        public string Material => filter.GetComponent<MeshRenderer>() is MeshRenderer r && r.sharedMaterial != null ? r.sharedMaterial.name : filter.name;
    }

    sealed class Plan
    {
        readonly Transform building;
        readonly Recipe r;
        readonly List<Work> works = new();
        Work frameWork, thresholdWork;
        readonly List<(Work work, int tri)> frameTris = new(), templateTris = new();
        readonly List<(Cut cut, Work work, List<int> tris, Rect face)> cuts = new();
        readonly List<(Bounds lip, Work work, List<int> tris)> lips = new();
        readonly Dictionary<Work, int> doorCounts = new();
        readonly List<string> notes = new();
        Bounds leafBounds;
        bool haveLeaf;

        public Plan(Transform building, Recipe recipe) { this.building = building; r = recipe; }

        public string Summary()
        {
            if (r == null) return "not a building this knows";
            var parts = new List<string>();
            if (haveLeaf) parts.Add($"door {doorCounts.Values.Sum()} triangles in {doorCounts.Count} meshes ({string.Join(", ", doorCounts.Select(p => p.Key.Material + " " + p.Value))}), {leafBounds.size.x:0.00} m wide");
            parts.Add($"frame block {frameTris.Count} triangles");
            parts.Add($"faces to open {cuts.Count}/{r.cuts.Count}");
            if (r.lips.Count > 0) parts.Add($"clapboard lips across the doorway {lips.Count}/{r.lips.Count}");
            return r.kind + ": " + string.Join("; ", parts) + string.Concat(notes.Select(n => "\n      " + n));
        }

        // Finds every piece; returns what doesn't match, or null when all is as expected.
        public string Find()
        {
            if (r == null) return "Unknown kind of building.";
            foreach (MeshFilter f in Parts(building))
            {
                Mesh m = f.sharedMesh;
                if (m == null) continue;
                if (!m.isReadable) return $"{f.name}'s mesh can't be read.";
                if (m.subMeshCount != 1) return $"{f.name}'s mesh has {m.subMeshCount} parts (expected one merged mesh).";
                if (m.name.Contains(CutSuffix)) return $"{f.name} already has a doorway cut (but no hinged door).";
                var w = new Work { filter = f, source = m, v = m.vertices, n = m.normals, uv = m.uv, t = m.GetTriangles(0) };
                if (w.n.Length != w.v.Length) return $"{f.name}'s mesh has no normals.";
                if (w.uv.Length != w.v.Length) w.uv = new Vector2[w.v.Length];
                w.gone = new bool[w.t.Length / 3];
                w.toLeaf = new bool[w.t.Length / 3];
                works.Add(w);
            }
            if (works.Count == 0) return "No merged meshes under it.";

            // 1. The door: everything wholly inside the door zone.
            foreach (Work w in works)
                for (int k = 0; k < w.gone.Length; k++)
                    if (Inside(w, k, r.doorZone))
                    {
                        w.gone[k] = w.toLeaf[k] = true;
                        doorCounts[w] = doorCounts.TryGetValue(w, out int c) ? c + 1 : 1;
                        Encapsulate(ref leafBounds, ref haveLeaf, w, k);
                    }
            int doorTris = doorCounts.Values.Sum();
            if (!haveLeaf || doorTris < 36) return $"Found only {doorTris} triangles of the door (expected its leaf, glass and handle: 36 or more).";
            if (Mathf.Abs(leafBounds.size.x - r.leafWidth) > .02f) return $"The door found is {leafBounds.size.x:0.000} m wide (expected {r.leafWidth:0.00}).";
            if (Mathf.Abs(leafBounds.min.x - r.left) > .02f || Mathf.Abs(leafBounds.min.z - r.hingeZ) > .02f)
                return $"The door found starts at x {leafBounds.min.x:0.000}, z {leafBounds.min.z:0.000} (expected {r.left:0.000}, {r.hingeZ:0.000}).";

            // 2. The solid frame block: one box, 12 triangles, in one mesh. Only triangles on the
            //    block's own surface count (anything else inside it is left where it is).
            var insideFrame = new List<(Work work, int tri)>();
            foreach (Work w in works)
                for (int k = 0; k < w.gone.Length; k++)
                    if (!w.gone[k] && Inside(w, k, r.frameBox)) insideFrame.Add((w, k));
            frameTris.AddRange(insideFrame.Where(p => OnSurface(p.work, p.tri, r.frameBox)));
            if (frameTris.Count != 12 || frameTris.Select(p => p.work).Distinct().Count() != 1)
                return $"The frame block: found {frameTris.Count} triangles on its surface in {frameTris.Select(p => p.work).Distinct().Count()} meshes (expected one box: 12 in one). " +
                       Describe(insideFrame);
            frameWork = frameTris[0].work;
            var strays = insideFrame.Where(p => !frameTris.Contains(p)).ToList();
            if (strays.Count > 0) notes.Add("Inside the frame block but not part of it (left alone): " + Describe(strays));

            // 3. The faces the doorway goes through: each one face (2 triangles) that covers the hole.
            foreach (Cut cut in r.cuts)
            {
                Work found = null;
                var tris = new List<int>();
                foreach (Work w in works)
                    for (int k = 0; k < w.gone.Length; k++)
                        if (!w.gone[k] && InPlane(w, k, cut) && Overlaps(w, k, cut))
                        {
                            if (found != null && found != w) return $"{Cap(cut.what)}: faces in two meshes ({found.filter.name}, {w.filter.name}).";
                            found = w;
                            tris.Add(k);
                        }
                if (found == null) return $"{Cap(cut.what)}: not found (a face at {Axis(cut.axis)} = {cut.value:0.000}).";
                if (tris.Count != 2) return $"{Cap(cut.what)}: {tris.Count} triangles where the doorway goes (expected one face, 2).";
                Rect face = FaceRect(found, tris, cut.axis);
                Rect hole = Clip(cut.hole, face);
                if (hole.width < .5f || hole.height < .05f) return $"{Cap(cut.what)}: it doesn't cover the doorway (face {face}).";
                if (face.width - hole.width < .2f) return $"{Cap(cut.what)}: no wall left either side of the doorway (face {face}).";
                cuts.Add((cut, found, tris, face));
            }
            thresholdWork = cuts.FirstOrDefault(c => c.cut.axis == 1).work ?? frameWork;

            // 4. The clapboard lips across the doorway: each one box (12 triangles).
            foreach (Bounds lip in r.lips)
            {
                Work found = null;
                var tris = new List<int>();
                foreach (Work w in works)
                    for (int k = 0; k < w.gone.Length; k++)
                        if (!w.gone[k] && Inside(w, k, lip)) { found ??= w; if (found == w) tris.Add(k); }
                if (found == null || tris.Count != 12) return $"The clapboard lip at {lip.center.y:0.00} m: found {tris.Count} triangles (expected one box, 12).";
                lips.Add((lip, found, tris));
            }

            // 5. The threshold's texture mapping: a stone box nearby.
            if (thresholdWork != null)
                for (int k = 0; k < thresholdWork.gone.Length; k++)
                    if (!thresholdWork.gone[k] && Inside(thresholdWork, k, r.thresholdTemplate)) templateTris.Add((thresholdWork, k));
            return null;
        }

        public void Build(Material hallMaterial)
        {
            Vector3 hinge = new Vector3(r.left, 0f, r.hingeZ);

            // ---- the door leaf, lifted out onto its hinge ----
            var leafParts = new List<(Work work, List<int> tris)>();
            foreach (Work w in works)
            {
                var tris = new List<int>();
                for (int k = 0; k < w.toLeaf.Length; k++) if (w.toLeaf[k]) tris.Add(k);
                if (tris.Count > 0) leafParts.Add((w, tris));
            }
            // The shop's door stops 13 cm above the ground: a kick plate in the leaf's own wood takes it down to the threshold.
            Work leafWood = leafParts.OrderByDescending(p => p.tris.Count(k => FaceArea(p.work, k) > .5f)).First().work;
            var kick = new Kit();
            if (!float.IsNaN(r.kickPlateBottom) && leafBounds.min.y - r.kickPlateBottom > .01f)
            {
                var maps = Maps(leafWood, leafParts.First(p => p.work == leafWood).tris);
                kick.Box(MinMax(new Vector3(leafBounds.min.x, r.kickPlateBottom, leafBounds.min.z), new Vector3(leafBounds.max.x, leafBounds.min.y + .002f, Mathf.Min(leafBounds.max.z, r.hingeZ + .10f))), maps, BuildingToWorld);
            }
            Mesh leaf = LeafMesh(leafParts, leafWood, kick, hinge);

            // ---- the frame: two jambs and a head where the block was ----
            var frameMaps = Maps(frameWork, frameTris.Select(p => p.tri).ToList());
            var frame = new Kit();
            frame.Box(MinMax(new Vector3(r.frameOuterLeft, r.frameBottom, r.frameBack), new Vector3(r.left, r.frameTop, r.frameFront)), frameMaps, BuildingToWorld);
            frame.Box(MinMax(new Vector3(r.right, r.frameBottom, r.frameBack), new Vector3(r.frameOuterRight, r.frameTop, r.frameFront)), frameMaps, BuildingToWorld);
            frame.Box(MinMax(new Vector3(r.left, r.top, r.frameBack), new Vector3(r.right, r.frameTop, r.frameFront)), frameMaps, BuildingToWorld);
            foreach ((Work w, int k) in frameTris) w.gone[k] = true;
            frame.AddTo(frameWork);

            // ---- the threshold ----
            var thresholdMaps = Maps(thresholdWork, templateTris.Select(p => p.tri).ToList());
            var step = new Kit();
            step.Box(r.threshold, thresholdMaps, BuildingToWorld);
            step.AddTo(thresholdWork);

            // ---- the faces the doorway goes through ----
            foreach ((Cut cut, Work w, List<int> tris, Rect face) in cuts)
            {
                UvMap map = FitFace(w, tris[0], cut.axis);
                Vector3 normal = Vector3.zero;
                normal[cut.axis] = cut.sign;
                var kit = new Kit();
                foreach (Rect piece in Subtract(face, Clip(cut.hole, face)))
                    kit.Rect(cut.axis, cut.value, piece, normal, map);
                foreach (int k in tris) w.gone[k] = true;
                kit.AddTo(w);
            }

            // ---- the clapboard lips: either side of the doorway ----
            foreach ((Bounds lip, Work w, List<int> tris) in lips)
            {
                var maps = Maps(w, tris);
                var kit = new Kit();
                // Each piece ends a centimetre inside the jamb, so its cut end never shares a plane with the jamb's face.
                kit.Box(MinMax(lip.min, new Vector3(r.left - .01f, lip.max.y, lip.max.z)), maps, BuildingToWorld);
                kit.Box(MinMax(new Vector3(r.right + .01f, lip.min.y, lip.min.z), lip.max), maps, BuildingToWorld);
                foreach (int k in tris) w.gone[k] = true;
                kit.AddTo(w);
            }

            // ---- new meshes for everything that changed ----
            var cutParts = new List<MeshFilter>();
            var originalMeshes = new List<Mesh>();
            foreach (Work w in works)
            {
                if (!w.changed && !w.gone.Any(g => g)) continue;
                Mesh cut = Keep(Rebuild(w), w.source.name + CutSuffix);
                Undo.RecordObject(w.filter, "Give the houses real front doors");
                w.filter.sharedMesh = cut;
                if (PrefabUtility.IsPartOfPrefabInstance(w.filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(w.filter);
                cutParts.Add(w.filter);
                originalMeshes.Add(w.source);
            }

            // ---- the dark hall behind the doorway ----
            Mesh hall = Keep(HallMesh(), building.name + " - doorway hall");
            var hallObject = new GameObject(HallName) { layer = building.gameObject.layer };
            hallObject.transform.SetParent(building, false);
            hallObject.AddComponent<MeshFilter>().sharedMesh = hall;
            var hallRenderer = hallObject.AddComponent<MeshRenderer>();
            hallRenderer.sharedMaterial = hallMaterial;
            hallRenderer.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(hallObject, GameObjectUtility.GetStaticEditorFlags(frameWork.filter.gameObject));
            Undo.RegisterCreatedObjectUndo(hallObject, "Give the houses real front doors");

            // ---- the door on its hinge ----
            var door = new GameObject(DoorName) { layer = building.gameObject.layer };
            door.transform.SetParent(building, false);
            door.transform.localPosition = hinge;
            var hingeObject = new GameObject("Hinge") { layer = door.layer };
            hingeObject.transform.SetParent(door.transform, false);
            var leafObject = new GameObject("Door leaf") { layer = door.layer };
            leafObject.transform.SetParent(hingeObject.transform, false);
            Mesh leafMesh = Keep(leaf, building.name + " - door leaf");
            leafObject.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            var leafRenderer = leafObject.AddComponent<MeshRenderer>();
            leafRenderer.sharedMaterials = leafParts.Select(p => p.work.filter.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
            MeshRenderer like = leafWood.filter.GetComponent<MeshRenderer>();
            leafRenderer.shadowCastingMode = like.shadowCastingMode;
            leafRenderer.receiveShadows = like.receiveShadows;
            leafRenderer.lightProbeUsage = like.lightProbeUsage;
            leafRenderer.reflectionProbeUsage = like.reflectionProbeUsage;
            var box = leafObject.AddComponent<BoxCollider>();
            Bounds lb = leafMesh.bounds;
            box.center = lb.center;
            box.size = lb.size;
            var doorway = new GameObject("Doorway");
            doorway.transform.SetParent(door.transform, false);
            doorway.transform.localPosition = r.doorway - hinge;
            var hallMarker = new GameObject("Hall");
            hallMarker.transform.SetParent(door.transform, false);
            hallMarker.transform.localPosition = r.hall - hinge;
            StreetDoor street = door.AddComponent<StreetDoor>();
            street.hinge = hingeObject.transform;
            street.doorway = doorway.transform;
            street.hall = hallMarker.transform;
            street.cutParts = cutParts.ToArray();
            street.originalMeshes = originalMeshes.ToArray();
            Undo.RegisterCreatedObjectUndo(door, "Give the houses real front doors");
        }

        Vector3 BuildingToWorld(Vector3 p) => building.TransformPoint(p);

        // The leaf in the hinge's space, one part per material (the kick plate, if any, in the door's wood).
        Mesh LeafMesh(List<(Work work, List<int> tris)> parts, Work wood, Kit kick, Vector3 hinge)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var subs = new List<List<int>>();
            foreach ((Work w, List<int> tris) in parts)
            {
                var map = new Dictionary<int, int>();
                var list = new List<int>();
                foreach (int k in tris)
                    for (int c = 0; c < 3; c++)
                    {
                        int i = w.t[k * 3 + c];
                        if (!map.TryGetValue(i, out int j))
                        {
                            j = v.Count;
                            map[i] = j;
                            v.Add(w.v[i] - hinge);
                            n.Add(w.n[i]);
                            uv.Add(w.uv[i]);
                        }
                        list.Add(j);
                    }
                if (w == wood)
                {
                    int start = v.Count;
                    v.AddRange(kick.v.Select(p => p - hinge));
                    n.AddRange(kick.n);
                    uv.AddRange(kick.uv);
                    list.AddRange(kick.t.Select(i => i + start));
                }
                subs.Add(list);
            }
            var mesh = new Mesh { indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // The hall: floor, two walls, ceiling and back, all facing in; open at the front.
        Mesh HallMesh()
        {
            float front = r.frameBack, back = -HallDepth;
            float bottom = float.IsNaN(r.kickPlateBottom) ? r.floor : r.kickPlateBottom;
            var kit = new Kit();
            UvMap flat = UvMap.Planar;
            kit.Rect(1, bottom, Rect.MinMaxRect(r.left, back, r.right, front), Vector3.up, flat);
            kit.Rect(1, r.top, Rect.MinMaxRect(r.left, back, r.right, front), Vector3.down, flat);
            kit.Rect(0, r.left, Rect.MinMaxRect(back, bottom, front, r.top), Vector3.right, flat);
            kit.Rect(0, r.right, Rect.MinMaxRect(back, bottom, front, r.top), Vector3.left, flat);
            kit.Rect(2, back, Rect.MinMaxRect(r.left, bottom, r.right, r.top), Vector3.forward, flat);
            var mesh = new Mesh();
            mesh.SetVertices(kit.v);
            mesh.SetNormals(kit.n);
            mesh.SetUVs(0, kit.uv);
            mesh.SetTriangles(kit.t, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static void Encapsulate(ref Bounds b, ref bool have, Work w, int k)
        {
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = w.v[w.t[k * 3 + c]];
                if (!have) { b = new Bounds(p, Vector3.zero); have = true; }
                else b.Encapsulate(p);
            }
        }
    }

    // ================================================================== geometry helpers

    // New faces to add to a mesh (building space).
    sealed class Kit
    {
        public readonly List<Vector3> v = new(), n = new();
        public readonly List<Vector2> uv = new();
        public readonly List<int> t = new();

        // A rectangle in a plane: axis 0 (x = value; rect is z, y), 1 (y = value; rect x, z), 2 (z = value; rect x, y).
        public void Rect(int axis, float value, Rect rect, Vector3 normal, UvMap map)
        {
            Vector3 P(float a, float b) => axis == 0 ? new Vector3(value, b, a) : axis == 1 ? new Vector3(a, value, b) : new Vector3(a, b, value);
            Quad(P(rect.xMin, rect.yMin), P(rect.xMin, rect.yMax), P(rect.xMax, rect.yMax), P(rect.xMax, rect.yMin), normal, map);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, UvMap map)
        {
            // Unity draws a triangle's front where its corners run clockwise, i.e. where
            // Cross(b - a, c - a) points at the viewer.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { (b, d) = (d, b); }
            int i = v.Count;
            foreach (Vector3 p in new[] { a, b, c, d })
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(map.At(p));
            }
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        // A box, facing out, each face mapped like the matching face of a model box.
        public void Box(Bounds box, Dictionary<Vector3, UvMap> maps, Func<Vector3, Vector3> toWorld)
        {
            Vector3 min = box.min, max = box.max;
            foreach (Vector3 normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                int axis = normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;
                float value = (normal[axis] > 0 ? max : min)[axis];
                Rect rect = axis == 0 ? UnityEngine.Rect.MinMaxRect(min.z, min.y, max.z, max.y)
                          : axis == 1 ? UnityEngine.Rect.MinMaxRect(min.x, min.z, max.x, max.z)
                          : UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (rect.width < 1e-5f || rect.height < 1e-5f) continue;
                UvMap map = maps.TryGetValue(normal, out UvMap m) ? m
                          : maps.TryGetValue(-normal, out m) ? m.OnAxis(axis)
                          : UvMap.World(axis, toWorld);
                Rect(axis, value, rect, normal, map);
            }
        }

        public void AddTo(Work w)
        {
            if (v.Count == 0) return;
            int start = w.addV.Count;
            w.addV.AddRange(v);
            w.addN.AddRange(n);
            w.addUV.AddRange(uv);
            w.addT.AddRange(t.Select(i => i + start));
            w.changed = true;
        }
    }

    // Texture coordinates as a straight function of position in a face's plane.
    public struct UvMap
    {
        public Vector2 a, b, c;
        public int axis;
        public Func<Vector3, Vector2> world;

        public Vector2 At(Vector3 p)
        {
            if (world != null) return world(p);
            Vector2 st = Plane2(p, axis);
            return a * st.x + b * st.y + c;
        }

        public UvMap OnAxis(int newAxis) { UvMap m = this; m.axis = newAxis; return m; }

        public static UvMap Planar => new() { a = new Vector2(1, 0), b = new Vector2(0, 1), axis = 2, world = p => new Vector2(p.x + p.z, p.y + p.z) };

        // The street builder's own rule (CafeStreetUpgrade.Combine): by the face's direction,
        // from its position in the world.
        public static UvMap World(int axis, Func<Vector3, Vector3> toWorld) => new()
        {
            axis = axis,
            world = p =>
            {
                Vector3 w = toWorld(p);
                return axis == 1 ? new Vector2(w.x, w.z) : axis == 0 ? new Vector2(w.z, w.y) : new Vector2(w.x, w.y);
            },
        };
    }

    static Vector2 Plane2(Vector3 p, int axis) => axis == 0 ? new Vector2(p.z, p.y) : axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);

    static int AxisOf(Vector3 normal)
    {
        float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
        return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
    }

    // The mapping of the face a triangle lies on.
    static UvMap FitFace(Work w, int k, int axis)
    {
        int i0 = w.t[k * 3], i1 = w.t[k * 3 + 1], i2 = w.t[k * 3 + 2];
        Vector2 s0 = Plane2(w.v[i0], axis), s1 = Plane2(w.v[i1], axis), s2 = Plane2(w.v[i2], axis);
        Vector2 e1 = s1 - s0, e2 = s2 - s0;
        float det = e1.x * e2.y - e1.y * e2.x;
        var map = new UvMap { axis = axis };
        if (Mathf.Abs(det) < 1e-9f) { map.a = new Vector2(1, 0); map.b = new Vector2(0, 1); return map; }
        Vector2 d1 = w.uv[i1] - w.uv[i0], d2 = w.uv[i2] - w.uv[i0];
        map.a = (d1 * e2.y - d2 * e1.y) / det;
        map.b = (d2 * e1.x - d1 * e2.x) / det;
        map.c = w.uv[i0] - map.a * s0.x - map.b * s0.y;
        return map;
    }

    // For each face direction of a model box's triangles, the mapping of that face.
    static Dictionary<Vector3, UvMap> Maps(Work w, List<int> tris)
    {
        var maps = new Dictionary<Vector3, UvMap>();
        if (w == null) return maps;
        foreach (int k in tris)
        {
            Vector3 normal = FaceNormal(w, k);
            int axis = AxisOf(normal);
            if (Mathf.Abs(normal[axis]) < .9f) continue;
            Vector3 key = Vector3.zero;
            key[axis] = Mathf.Sign(normal[axis]);
            if (!maps.ContainsKey(key)) maps[key] = FitFace(w, k, axis);
        }
        return maps;
    }

    static Vector3 FaceNormal(Work w, int k)
    {
        Vector3 n = w.n[w.t[k * 3]] + w.n[w.t[k * 3 + 1]] + w.n[w.t[k * 3 + 2]];
        return n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.zero;
    }

    static float FaceArea(Work w, int k)
    {
        Vector3 a = w.v[w.t[k * 3]], b = w.v[w.t[k * 3 + 1]], c = w.v[w.t[k * 3 + 2]];
        return Vector3.Cross(b - a, c - a).magnitude * .5f;
    }

    static bool Inside(Work w, int k, Bounds box)
    {
        Vector3 min = box.min - Vector3.one * Eps, max = box.max + Vector3.one * Eps;
        for (int c = 0; c < 3; c++)
        {
            Vector3 p = w.v[w.t[k * 3 + c]];
            if (p.x < min.x || p.y < min.y || p.z < min.z || p.x > max.x || p.y > max.y || p.z > max.z) return false;
        }
        return true;
    }

    // All three corners on one face of the box (and the triangle facing out of it).
    static bool OnSurface(Work w, int k, Bounds box)
    {
        Vector3 min = box.min, max = box.max;
        Vector3 normal = FaceNormal(w, k);
        for (int axis = 0; axis < 3; axis++)
            foreach ((float value, int sign) in new[] { (min[axis], -1), (max[axis], 1) })
            {
                bool all = true;
                for (int c = 0; c < 3 && all; c++) all = Mathf.Abs(w.v[w.t[k * 3 + c]][axis] - value) <= Eps;
                if (all && normal[axis] * sign > .9f) return true;
            }
        return false;
    }

    static string Describe(List<(Work work, int tri)> tris) =>
        string.Join("; ", tris.GroupBy(p => p.work).Select(g =>
        {
            var b = new Bounds();
            bool have = false;
            foreach ((Work w, int k) in g)
                for (int c = 0; c < 3; c++)
                {
                    Vector3 p = w.v[w.t[k * 3 + c]];
                    if (!have) { b = new Bounds(p, Vector3.zero); have = true; } else b.Encapsulate(p);
                }
            return $"{g.Key.Material} {g.Count()} triangles, {b.min.x:0.000}..{b.max.x:0.000} across, {b.min.y:0.000}..{b.max.y:0.000} up, {b.min.z:0.000}..{b.max.z:0.000} out";
        }));

    static bool InPlane(Work w, int k, Cut cut)
    {
        for (int c = 0; c < 3; c++)
            if (Mathf.Abs(w.v[w.t[k * 3 + c]][cut.axis] - cut.value) > Eps) return false;
        return FaceNormal(w, k)[cut.axis] * cut.sign > .9f;
    }

    static bool Overlaps(Work w, int k, Cut cut)
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int c = 0; c < 3; c++)
        {
            Vector2 p = Plane2(w.v[w.t[k * 3 + c]], cut.axis);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return min.x < cut.hole.xMax - Eps && max.x > cut.hole.xMin + Eps && min.y < cut.hole.yMax - Eps && max.y > cut.hole.yMin + Eps;
    }

    static Rect FaceRect(Work w, List<int> tris, int axis)
    {
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (int k in tris)
            for (int c = 0; c < 3; c++)
            {
                Vector2 p = Plane2(w.v[w.t[k * 3 + c]], axis);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static Rect Clip(Rect r, Rect to) =>
        Rect.MinMaxRect(Mathf.Max(r.xMin, to.xMin), Mathf.Max(r.yMin, to.yMin), Mathf.Min(r.xMax, to.xMax), Mathf.Min(r.yMax, to.yMax));

    // A face with a rectangular hole in it, as up to four rectangles.
    static IEnumerable<Rect> Subtract(Rect face, Rect hole)
    {
        var pieces = new[]
        {
            Rect.MinMaxRect(face.xMin, face.yMin, hole.xMin, face.yMax),   // left of the hole, full height
            Rect.MinMaxRect(hole.xMax, face.yMin, face.xMax, face.yMax),   // right of it
            Rect.MinMaxRect(hole.xMin, face.yMin, hole.xMax, hole.yMin),   // below it
            Rect.MinMaxRect(hole.xMin, hole.yMax, hole.xMax, face.yMax),   // above it
        };
        foreach (Rect p in pieces)
            if (p.width > 1e-4f && p.height > 1e-4f) yield return p;
    }

    // The mesh without its removed triangles, with the added faces; only the vertices still used.
    static Mesh Rebuild(Work w)
    {
        var map = new Dictionary<int, int>();
        var order = new List<int>();
        var tris = new List<int>();
        for (int k = 0; k < w.gone.Length; k++)
        {
            if (w.gone[k]) continue;
            for (int c = 0; c < 3; c++)
            {
                int i = w.t[k * 3 + c];
                if (!map.TryGetValue(i, out int j)) { j = order.Count; map[i] = j; order.Add(i); }
                tris.Add(j);
            }
        }
        int kept = order.Count;
        var mesh = new Mesh { indexFormat = kept + w.addV.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var vertices = order.Select(i => w.v[i]).ToList();
        vertices.AddRange(w.addV);
        var normals = order.Select(i => w.n[i]).ToList();
        normals.AddRange(w.addN);
        var uv = order.Select(i => w.uv[i]).ToList();
        uv.AddRange(w.addUV);
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        Color[] colors = w.source.colors;
        if (colors.Length == w.v.Length)
        {
            var list = order.Select(i => colors[i]).ToList();
            list.AddRange(Enumerable.Repeat(Color.white, w.addV.Count));
            mesh.SetColors(list);
        }
        for (int channel = 1; channel < 4; channel++)
        {
            var source = new List<Vector2>();
            w.source.GetUVs(channel, source);
            if (source.Count != w.v.Length) continue;
            var list = order.Select(i => source[i]).ToList();
            list.AddRange(Enumerable.Repeat(Vector2.zero, w.addV.Count));
            mesh.SetUVs(channel, list);
        }
        tris.AddRange(w.addT.Select(i => i + kept));
        mesh.SetTriangles(tris, 0, false);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // Stores a new mesh in the doors asset under a name, or returns the identical one an
    // earlier run stored there. Nothing is ever deleted.
    static Mesh Keep(Mesh mesh, string name)
    {
        mesh.name = name;
        Mesh existing = AssetDatabase.LoadAllAssetsAtPath(DoorsAsset).OfType<Mesh>().FirstOrDefault(m => m.name == name);
        if (existing != null && Same(existing, mesh))
        {
            Object.DestroyImmediate(mesh);
            return existing;
        }
        // The file's main object takes the file's name, so it is an empty placeholder and
        // every real mesh keeps its own name.
        if (AssetDatabase.LoadMainAssetAtPath(DoorsAsset) == null) AssetDatabase.CreateAsset(new Mesh { name = "Street doors" }, DoorsAsset);
        if (existing != null) mesh.name += " " + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        AssetDatabase.AddObjectToAsset(mesh, DoorsAsset);
        return mesh;
    }

    static bool Same(Mesh a, Mesh b)
    {
        if (a.vertexCount != b.vertexCount || a.subMeshCount != b.subMeshCount || a.triangles.Length != b.triangles.Length) return false;
        Vector3[] va = a.vertices, vb = b.vertices;
        for (int i = 0; i < va.Length; i++) if ((va[i] - vb[i]).sqrMagnitude > 1e-10f) return false;
        return a.triangles.SequenceEqual(b.triangles);
    }

    static Material HallMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(HallMaterialPath);
        if (material != null) return material;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader is missing.");
        material = new Material(lit) { name = "Street doors - dark hall", enableInstancing = true };
        // A dim hallway: dark warm wood, almost no shine.
        material.SetColor("_BaseColor", new Color(.075f, .058f, .047f));
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .06f);
        AssetDatabase.CreateAsset(material, HallMaterialPath);
        return material;
    }

    // ================================================================== waiting spots (Doors 4)
    //
    // A doorway fits one person, so people take turns at a front door (StreetDoor, 27
    // Sept). Someone on their way in whose turn it isn't yet needs somewhere to stand:
    // on the pavement near where the door's single-file stretch starts, out of the way of
    // whoever is coming out, and clear of everything standing there (railings, bike racks,
    // benches, bins, lamps, trees), of the road and of the crossings, with a clear straight
    // walk from there to the stretch and from the way in to there. Measured with the car
    // park's own survey of standing things (a person 0.28 m round, 5 cm to spare). Up to
    // four spots per door, best first: markers "Wait here 1..4" under the door, which
    // StreetDoor hands out (the first free one) and NpcJourney walks people to. Only doors
    // a walking route starts at get spots; the others have none (and need none).

    public const string WaitName = "Wait here ";
    const int MostSpots = 4;
    const float SpotClear = .05f;       // beyond a person's own radius
    const float OutOfTheWay = .7f;      // from the stretch and from the way on from it
    const float SpotApart = .75f;

    [MenuItem(Menu + "Doors 4 - Mark where people wait for their turn at the door")]
    static void WaitSpotsMenu()
    {
        var report = new StringBuilder();
        try
        {
            RequireScene();
            CafeArrivals arrivals = Object.FindAnyObjectByType<CafeArrivals>(FindObjectsInactive.Include)
                                    ?? throw new InvalidOperationException("No CafeArrivals in the scene (Cafe parking lot > 1 - Build).");
            List<CafeParkingLot.Obstacle> obstacles = CafeParkingLot.Obstacles(arrivals.transform);
            var lanes = CafeParkingLot.LaneSegments();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "door-waiting-spots-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            int doors = 0, spots = 0, short_ = 0;
            foreach (string name in Buildings)
            {
                Transform building = FindOptional(name);
                StreetDoor door = building != null && building.Find(DoorName) != null ? building.Find(DoorName).GetComponent<StreetDoor>() : null;
                if (door == null) continue;
                CafeArrivals.Route route = RouteFrom(door, arrivals);
                if (route == null)
                {
                    SetSpots(door, Array.Empty<Vector3>());
                    report.AppendLine($"-     {name}: no walking route starts at this door; no waiting spots needed.");
                    continue;
                }
                Vector3[] found = FindSpots(door, route, arrivals, obstacles, lanes, out string how);
                SetSpots(door, found);
                doors++;
                spots += found.Length;
                if (found.Length < 2) short_++;
                report.AppendLine($"{(found.Length >= 2 ? "ok    " : "FEW   ")}{name} (\"{route.name}\"): {found.Length} spot(s) " +
                                  string.Join(", ", found.Select(f => $"({f.x:0.00}, {f.z:0.00})")) + ". " + how);
                PhotoSpots(Path.Combine(folder, name + ".png"), door, route, found);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            string head = $"Doors 4: {spots} waiting spot(s) at {doors} door(s) walk-ins use. Save the scene (Ctrl+S). Photos: {folder}";
            File.WriteAllText(Path.Combine(folder, "report.txt"), head + "\n\n" + report);
            if (short_ > 0) Debug.LogWarning(Tag + head + $"\n{short_} door(s) got fewer than two spots:\n" + report);
            else Debug.Log(Tag + head + "\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "Doors 4 FAILED (nothing after the failure was changed): " + e.Message + "\n" + report + "\n" + e); }
    }

    /// <summary>The walking route that starts at this door's doorway, or null.</summary>
    public static CafeArrivals.Route RouteFrom(StreetDoor door, CafeArrivals arrivals) =>
        door == null || arrivals == null ? null
        : arrivals.EditorFootRoutes.FirstOrDefault(r => r != null && r.points.Length > 1 && Flat2(r.points[0] - door.DoorwayPoint).magnitude < .7f);

    /// <summary>
    /// Free spots to wait at near where the door's single-file stretch starts, best first.
    /// Public for Doors 3, which checks the marked ones with the same tests.
    /// </summary>
    internal static Vector3[] FindSpots(StreetDoor door, CafeArrivals.Route route, CafeArrivals arrivals,
                                      List<CafeParkingLot.Obstacle> obstacles, List<(Vector3 a, Vector3 b)> lanes, out string how)
    {
        int end = CafeArrivals.SingleFileEnd(route);
        Vector3 start = route.points[end];
        var stretch = Stretch(door, route, end);
        var wayOn = WayOn(route, end, 3.5f);
        int tried = 0, free = 0;
        var candidates = new List<(Vector3 p, float score)>();
        for (float dx = -3f; dx <= 3.001f; dx += .25f)
            for (float dz = -3f; dz <= 3.001f; dz += .25f)
            {
                var p = new Vector3(start.x + dx, start.y, start.z + dz);
                float fromStart = Flat2(p - start).magnitude;
                if (fromStart < .6f || fromStart > 3f) continue;
                tried++;
                if (!SpotProblem(p, door, route, arrivals, obstacles, lanes, stretch, wayOn, out _)) free++;
                else continue;
                float offTheWay = PathDistance(wayOn, p);
                candidates.Add((p, fromStart + .5f * Mathf.Max(0f, 1.3f - offTheWay)));
            }
        var chosen = new List<Vector3>();
        foreach (var c in candidates.OrderBy(c => c.score))
        {
            if (chosen.Any(o => Flat2(o - c.p).magnitude < SpotApart)) continue;
            chosen.Add(c.p);
            if (chosen.Count >= MostSpots) break;
        }
        how = $"The stretch starts at ({start.x:0.00}, {start.z:0.00}); {free} of {tried} places within 3 m are free and out of the way.";
        return chosen.ToArray();
    }

    /// <summary>Why a person couldn't wait at <paramref name="p"/> (null: they can). For Doors 3 and 4.</summary>
    internal static string SpotProblem(Vector3 p, StreetDoor door, CafeArrivals.Route route, CafeArrivals arrivals,
                                     List<CafeParkingLot.Obstacle> obstacles, List<(Vector3 a, Vector3 b)> lanes)
    {
        int end = CafeArrivals.SingleFileEnd(route);
        return SpotProblem(p, door, route, arrivals, obstacles, lanes, Stretch(door, route, end), WayOn(route, end, 3.5f), out string why) ? why : null;
    }

    static bool SpotProblem(Vector3 p, StreetDoor door, CafeArrivals.Route route, CafeArrivals arrivals,
                            List<CafeParkingLot.Obstacle> obstacles, List<(Vector3 a, Vector3 b)> lanes,
                            List<Vector3> stretch, List<Vector3> wayOn, out string why)
    {
        Vector3 start = stretch[stretch.Count - 1];
        why = null;
        if (door.Outside(p) < .9f) why = "up against the house";
        else if (PathDistance(stretch, p) < OutOfTheWay) why = "on the door's single-file stretch";
        else if (PathDistance(wayOn, p) < OutOfTheWay) why = "in the way of people coming out";
        else if (Blocked(p, p, obstacles, out string thing)) why = "something stands there: " + thing;
        else if (Blocked(p, start, obstacles, out thing)) why = "no clear walk from there to the stretch: " + thing;
        else if (Blocked(NearestOn(wayOn, p), p, obstacles, out thing)) why = "no clear walk to there from the way in: " + thing;
        else if (lanes.Any(l => CafeParkingLot.SegmentSegmentDistance(p, p, l.a, l.b) - CafeParkingLot.LaneHalfWidth - CafeParkingLot.WalkerRadius < SpotClear)) why = "on or by the road";
        else if (arrivals.EditorCrossings.Any(c => OnCrossing(p, c))) why = "on a crossing";
        return why != null;
    }

    // The single-file stretch: the dark hall, the doorway and the route out to where it widens.
    static List<Vector3> Stretch(StreetDoor door, CafeArrivals.Route route, int end)
    {
        var list = new List<Vector3> { door.HallPoint };
        for (int i = 0; i <= end; i++) list.Add(route.points[i]);
        return list;
    }

    // The first metres of the way on from the stretch (where people coming out walk), up to a crossing.
    static List<Vector3> WayOn(CafeArrivals.Route route, int end, float metres)
    {
        var list = new List<Vector3> { route.points[end] };
        for (int i = end + 1; i < route.points.Length && metres > 0f; i++)
        {
            if (i - 1 < route.crossingAtSegment.Length && route.crossingAtSegment[i - 1] >= 0) break;
            Vector3 a = route.points[i - 1], b = route.points[i];
            float length = Flat2(b - a).magnitude;
            list.Add(length <= metres ? b : a + (b - a) * (metres / Mathf.Max(length, 1e-4f)));
            metres -= length;
        }
        return list;
    }

    static bool Blocked(Vector3 a, Vector3 b, List<CafeParkingLot.Obstacle> obstacles, out string what)
    {
        foreach (CafeParkingLot.Obstacle o in obstacles)
        {
            if (o.top < Mathf.Min(a.y, b.y) + CafeParkingLot.StepHeight) continue;   // a kerb, a step: walked over
            if (CafeParkingLot.SegmentRectDistance(a, b, o.footprint) - CafeParkingLot.WalkerRadius < SpotClear) { what = o.name; return true; }
        }
        what = null;
        return false;
    }

    static bool OnCrossing(Vector3 p, CafeArrivals.Crossing c)
    {
        Quaternion turn = Quaternion.Euler(0f, c.yaw, 0f);
        Vector3 local = Quaternion.Inverse(turn) * (p - c.center);
        return Mathf.Abs(local.x) < c.halfSize.x + .3f && Mathf.Abs(local.z) < c.halfSize.y + .3f;
    }

    static float PathDistance(List<Vector3> path, Vector3 p)
    {
        if (path.Count == 1) return Flat2(p - path[0]).magnitude;
        float best = float.PositiveInfinity;
        for (int i = 1; i < path.Count; i++) best = Mathf.Min(best, Flat2(p - Nearest(path[i - 1], path[i], p)).magnitude);
        return best;
    }

    static Vector3 NearestOn(List<Vector3> path, Vector3 p)
    {
        if (path.Count == 1) return path[0];
        Vector3 best = path[0];
        float bestDistance = float.PositiveInfinity;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 q = Nearest(path[i - 1], path[i], p);
            float d = Flat2(p - q).magnitude;
            if (d < bestDistance) { bestDistance = d; best = q; }
        }
        return best;
    }

    static Vector3 Nearest(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 e = Flat2(b - a);
        float t = e.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(Flat2(p - a), e) / e.sqrMagnitude) : 0f;
        return a + (b - a) * t;
    }

    static Vector3 Flat2(Vector3 v) => new(v.x, 0f, v.z);

    // Replaces the door's "Wait here" markers with these (undoable).
    static void SetSpots(StreetDoor door, Vector3[] spots)
    {
        var old = new List<GameObject>();
        foreach (Transform child in door.transform) if (child.name.StartsWith(WaitName, StringComparison.Ordinal)) old.Add(child.gameObject);
        foreach (GameObject g in old) Undo.DestroyObjectImmediate(g);
        var marks = new Transform[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            var g = new GameObject(WaitName + (i + 1)) { layer = door.gameObject.layer };
            Undo.RegisterCreatedObjectUndo(g, "Mark where people wait");
            g.transform.SetParent(door.transform, false);
            g.transform.position = spots[i];
            Vector3 face = Flat2(door.DoorwayPoint - spots[i]);
            if (face.sqrMagnitude > 1e-6f) g.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            marks[i] = g.transform;
        }
        Undo.RecordObject(door, "Mark where people wait");
        door.waitSpots = marks;
        EditorUtility.SetDirty(door);
    }

    // From above and in front: the door, its stretch (blue) and the waiting spots (orange discs).
    static void PhotoSpots(string path, StreetDoor door, CafeArrivals.Route route, Vector3[] spots)
    {
        var temporary = new List<Object>();
        try
        {
            Material Mat(Color c)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { hideFlags = HideFlags.HideAndDontSave };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                temporary.Add(m);
                return m;
            }
            GameObject Disc(Vector3 at, float size, Material m)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.position = at + Vector3.up * .03f;
                g.transform.localScale = new Vector3(size, .01f, size);
                g.GetComponent<Renderer>().sharedMaterial = m;
                temporary.Add(g);
                return g;
            }
            Material orange = Mat(new Color(1f, .55f, .1f)), blue = Mat(new Color(.2f, .6f, 1f));
            foreach (Vector3 s in spots) Disc(s, .56f, orange);
            var stretch = Stretch(door, route, CafeArrivals.SingleFileEnd(route));
            foreach (Vector3 p in stretch) Disc(p, .18f, blue);
            Vector3 centre = stretch[stretch.Count - 1];
            Vector3 o = door.Outward;
            CafeSecondPassSteps.Capture(path, centre + o * 3.5f + Vector3.up * 7.5f, centre + o * .6f, 55f, false);
        }
        finally { foreach (Object g in temporary) if (g != null) Object.DestroyImmediate(g); }
    }

    // ================================================================== scene helpers

    // The building's merged meshes: its direct children with a mesh, in place (not moved).
    public static IEnumerable<MeshFilter> Parts(Transform building)
    {
        foreach (Transform child in building)
        {
            var filter = child.GetComponent<MeshFilter>();
            if (filter == null || child.GetComponent<MeshRenderer>() == null) continue;
            if (child.localPosition.sqrMagnitude > 1e-8f || child.localRotation != Quaternion.identity || child.localScale != Vector3.one) continue;
            yield return filter;
        }
    }

    static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
    }

    public static Transform FindOptional(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    static string Axis(int axis) => axis == 0 ? "x" : axis == 1 ? "y" : "z";
    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
#endif
