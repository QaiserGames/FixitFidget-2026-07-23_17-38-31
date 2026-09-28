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
// NIGHT WALK 2 (the edges): where Ace can't go at night, there's a visible reason.
// (claude/night-city-proposal.md §7 in the project.)
//
//   * Night road works at the 8 street ends, a different job at each, right across the
//     street from building to building. They live in the night group, off by day, and come
//     on with the night (NightWalk.nightOnly). Each piece has exact collision of its own.
//   * The corners, closed for good (Mansoor, 28 Sept: "Fence + building site"): the city
//     car park's open west side gets a brick-and-railing fence with a locked iron gate, the
//     empty ground by the south-east hotel becomes a fenced building site, and the two
//     passages behind the north-west and north-east corner buildings get the same fence.
//     These are ordinary, permanent scenery. Like the rest of the city they have no
//     collision by day (their prefab colliders are switched off); the night's collision list
//     makes them solid exactly as they look.
//
// Every line was measured from the night sweep's grid (Logs/Night/edges-night-*/grid.txt): the
// open ground from one building to the other, at the ground's height (-0.28 m), reaching on to the
// walls themselves. The sweep then checks there is no way left out. Nothing here touches the day's routes: the
// build refuses to run if a permanent fence would cross a walker's, a car's or a visitor's route.
// ---------------------------------------------------------------------------
internal static class NightEdges
{
    const string Tag = "[Night walk] ";
    const string Menu = "Fixit Fidget/Night/";
    const string LayoutRoot = "ACE'S CAFE - layout study 02";
    const string NightGroupName = "20 - Night walk";
    internal const string WorksName = "Road works (night only)";
    internal const string EdgesName = "21 - Edges of the 9 blocks";
    const string LampMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - works lamp.mat";
    const string GateMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - iron gate.mat";
    const float Ground = -.28f;
    // The sweep's grid marks where Ace's body (a 0.5 m radius) first touches a wall, so the wall itself
    // stands about half a metre further on: each line reaches that far past the measured faces.
    const float Overlap = .5f;

    const string City = "Assets/Synty/PolygonCity/Prefabs/";
    const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/";
    const string Barrier = City + "Props/SM_Prop_Barrier_01.prefab";
    const string Fence = City + "Environments/SM_Env_Fence_01.prefab";
    const string Pier = City + "Environments/SM_Env_Fence_End_01.prefab";
    const string Cone = City + "Props/SM_Prop_Cone_01.prefab";
    const string Drum = City + "Props/SM_Prop_Cone_02.prefab";
    const string Warning = City + "Props/SM_Prop_Sign_Warning_01.prefab";
    const string Van = City + "Vehicles/SM_Veh_Car_Van_01.prefab";
    const string Manhole = City + "Props/SM_Prop_Manhole_01.prefab";
    const string Steam = City + "FX/FX_Steam.prefab";
    const string Patch = City + "Environments/SM_Env_Road_Patch_01.prefab";
    const string Pallet = City + "Props/SM_Prop_Pallet_01.prefab";
    const string PowerBox = City + "Props/SM_Prop_PowerBox_01.prefab";
    const string Box3 = City + "Props/SM_Prop_CardboardBox_03.prefab";
    const string Box4 = City + "Props/SM_Prop_CardboardBox_04.prefab";
    const string Skip = City + "Props/SM_Prop_Skip_02.prefab";
    const string Bag1 = City + "Props/SM_Prop_TrashBag_01.prefab";
    const string Bag2 = City + "Props/SM_Prop_TrashBag_02.prefab";
    const string Pipe = Gen + "Building/SM_Gen_Bld_Pipe_Straight_01.prefab";
    const string Beam = Gen + "Building/SM_Gen_Bld_Beam_01.prefab";
    const string Ladder = Gen + "Building/SM_Gen_Bld_Ladder_01.prefab";
    const string Barrel1 = Gen + "Props/SM_Gen_Prop_Barrel_Metal_01.prefab";
    const string Barrel2 = Gen + "Props/SM_Gen_Prop_Barrel_Metal_02.prefab";
    const string Barrel3 = Gen + "Props/SM_Gen_Prop_Barrel_Metal_03.prefab";
    const string Crate2 = Gen + "Props/SM_Gen_Prop_Crate_02.prefab";
    const string Crate3 = Gen + "Props/SM_Gen_Prop_Crate_03.prefab";
    const string Crates = Gen + "Props/SM_Gen_Prop_Crate_Preset_01.prefab";
    const string Plank1 = Gen + "Props/SM_Gen_Prop_Plank_01.prefab";
    const string Plank2 = Gen + "Props/SM_Gen_Prop_Plank_02.prefab";
    const string Sacks1 = Gen + "Props/SM_Gen_Prop_Sack_Stack_01.prefab";
    const string Sacks2 = Gen + "Props/SM_Gen_Prop_Sack_Stack_02.prefab";

    // Which way a prop's own +Z points: along the line (U), across it (W = U x up, so the prop's own
    // X runs along the line), towards the outside (N) or the inside (In).
    enum Face { U, MinusU, W, MinusW, N, In }

    readonly struct Prop
    {
        public readonly string path; public readonly float along, outward, y; public readonly Face face;
        public readonly Vector3 turn, scale;
        public Prop(string path, float along, float outward, float y = 0f, Face face = Face.W, Vector3? turn = null, Vector3? scale = null)
        {
            this.path = path; this.along = along; this.outward = outward; this.y = y; this.face = face;
            this.turn = turn ?? Vector3.zero; this.scale = scale ?? Vector3.one;
        }
    }

    sealed class Line
    {
        public string name, job;
        public bool alongX;            // the line runs along world x (at z = at) or along world z (at x = at)
        public float at, from, to;     // measured faces of the open ground
        public Vector3 outside;        // the world direction away from the 9 blocks
        public bool gate;              // A1: a locked iron gate in the fence
        public Prop[] props = Array.Empty<Prop>();

        public Vector3 Start => alongX ? new Vector3(from - Overlap, Ground, at) : new Vector3(at, Ground, from - Overlap);
        public Vector3 End => alongX ? new Vector3(to + Overlap, Ground, at) : new Vector3(at, Ground, to + Overlap);
        public float Length => (End - Start).magnitude;
        public Vector3 Mid => (Start + End) * .5f;
        public Vector3 U => (End - Start).normalized;
        public Vector3 W => Vector3.Cross(U, Vector3.up);
        public Vector3 N => outside.normalized;
        public Vector3 Point(float along, float outward, float y) => Mid + U * along + N * outward + Vector3.up * y;
        public Vector3 Dir(Face f) => f switch
        {
            Face.U => U, Face.MinusU => -U, Face.W => W, Face.MinusW => -W, Face.N => N, _ => -N,
        };
    }

    // ------------------------------------------------------------------ the 8 street ends (night only)

    static Line[] RoadWorks() => new[]
    {
        new Line
        {
            name = "1 - West street, south end", job = "a burst water main", alongX = true, at = -43f, from = -20f, to = -7.25f,
            outside = Vector3.back,
            props = new[]
            {
                new Prop(Van, -3f, 4.2f, face: Face.N),
                new Prop(Pipe, 3.9f, 1.5f, .19f), new Prop(Pipe, 3.9f, 1.9f, .19f), new Prop(Pipe, 3.9f, 1.7f, .52f),
                new Prop(Manhole, .2f, 1.7f, 0f, Face.N), new Prop(Steam, .2f, 1.7f, .03f),
                new Prop(Cone, -4.5f, -.95f), new Prop(Cone, -3f, -.95f), new Prop(Cone, -1.5f, -.95f),
            },
        },
        new Line
        {
            name = "2 - East street, south end", job = "resurfacing", alongX = true, at = -43f, from = 7.25f, to = 20f,
            outside = Vector3.back,
            props = new[]
            {
                new Prop(Patch, -2.6f, 2.4f, 0f, Face.N), new Prop(Patch, .4f, 2.4f, 0f, Face.N),
                new Prop(Pallet, 4f, 2.2f), new Prop(Sacks1, 3.6f, 2.2f, .39f), new Prop(Sacks2, 4.45f, 2.25f, .39f),
                new Prop(Drum, -4.5f, -1f), new Prop(Drum, -1.5f, -1f), new Prop(Drum, 1.5f, -1f), new Prop(Drum, 4.5f, -1f),
            },
        },
        new Line
        {
            name = "3 - West street, north end", job = "a timber delivery", alongX = true, at = 35.6f, from = -16f, to = -9f,
            outside = Vector3.forward,
            props = new[]
            {
                new Prop(Beam, -2.4f, 1.3f, .18f, Face.U), new Prop(Beam, -2.4f, 1.7f, .18f, Face.U), new Prop(Beam, -2.3f, 1.5f, .54f, Face.U),
                new Prop(Barrel1, 2.9f, 1.2f), new Prop(Barrel2, 2.8f, 2.0f),
                new Prop(Cone, -2f, -.9f), new Prop(Cone, 2f, -.9f),
            },
        },
        new Line
        {
            name = "4 - East street, north end", job = "a blocked drain", alongX = true, at = 34.9f, from = 9.75f, to = 16f,
            outside = Vector3.forward,
            props = new[]
            {
                new Prop(Manhole, -.3f, 1.7f, 0f, Face.N),
                new Prop(Barrel2, 1.6f, 1.2f), new Prop(Barrel3, 2.3f, 1.3f), new Prop(Barrel1, 1.95f, 1.95f),
                new Prop(Crate2, -2f, 1.6f, 0f, Face.N),
                new Prop(Cone, -1.8f, -.9f), new Prop(Cone, 1.8f, -.9f),
            },
        },
        new Line
        {
            name = "5 - Front street, west end", job = "a new cable box", alongX = false, at = -44.6f, from = -15.25f, to = -.25f,
            outside = Vector3.left,
            props = new[]
            {
                new Prop(PowerBox, 3.5f, 1.4f, 0f, Face.In),
                new Prop(Box3, 5.2f, 1.2f), new Prop(Box4, 5.8f, 2.0f, 0f, Face.U),
                new Prop(Plank1, -3f, 2.4f, .06f, Face.U), new Prop(Plank1, -2.7f, 2.7f, .06f, Face.U), new Prop(Plank2, -2.4f, 2.4f, .06f, Face.U),
                new Prop(Cone, -1.5f, -.95f), new Prop(Cone, 0f, -.95f), new Prop(Cone, 1.5f, -.95f),
            },
        },
        new Line
        {
            name = "6 - Back street, west end", job = "a trench", alongX = false, at = -45f, from = 16f, to = 28.25f,
            outside = Vector3.left,
            props = new[]
            {
                new Prop(Sacks1, -2.2f, 1.2f), new Prop(Sacks2, -1f, 1.25f),
                new Prop(Plank2, 1.5f, 1.8f, .055f), new Prop(Plank2, 1.5f, 2.1f, .055f), new Prop(Plank2, 1.5f, 2.4f, .055f), new Prop(Plank2, 1.5f, 2.7f, .055f),
                new Prop(Ladder, 3.8f, 1.3f, .1f, Face.W, new Vector3(90f, 0f, 0f)),
                new Prop(Drum, -1.5f, -1f), new Prop(Drum, 1.5f, -1f), new Prop(Drum, 4f, -1f),
            },
        },
        new Line
        {
            name = "7 - Front street, east end", job = "a lane closure", alongX = false, at = 43.3f, from = -15.5f, to = .75f,
            outside = Vector3.right,
            props = new[]
            {
                new Prop(Drum, -4.8f, -1.9f), new Prop(Drum, -3.2f, -1.45f), new Prop(Drum, -1.6f, -1f), new Prop(Drum, 0f, -1f),
                new Prop(Drum, 1.6f, -1f), new Prop(Drum, 3.2f, -1.45f), new Prop(Drum, 4.8f, -1.9f),
                new Prop(Crates, 5f, 2.3f, 0f, Face.N), new Prop(Pallet, -5f, 2.2f), new Prop(Crate3, -5f, 2.2f, .39f),
            },
        },
        new Line
        {
            name = "8 - Back street, east end", job = "a skip and rubble", alongX = false, at = 46.3f, from = 15.25f, to = 28.75f,
            outside = Vector3.right,
            props = new[]
            {
                new Prop(Skip, -2.6f, 1.5f, 0f, Face.In),
                new Prop(Pallet, 1.8f, 2.1f), new Prop(Bag1, 3.9f, 1.2f), new Prop(Bag2, 4.5f, 1.8f),
                new Prop(Cone, -4f, -.9f), new Prop(Cone, 0f, -.9f), new Prop(Cone, 4f, -.9f),
            },
        },
    };

    // ------------------------------------------------------------------ the corners (for good)

    static Line[] Corners() => new[]
    {
        new Line { name = "A1 - City car park, west side", job = "the car park's fence and its locked gate", alongX = false, at = -41.5f, from = -40.5f, to = -24.5f, outside = Vector3.left, gate = true },
        new Line { name = "A2 - Behind the car park", job = "a fence between the two buildings", alongX = true, at = -42.8f, from = -40.75f, to = -36f, outside = Vector3.back },
        new Line { name = "B1 - Building site, north", job = "the building site's fence", alongX = true, at = -24.2f, from = 36.75f, to = 40f, outside = Vector3.back,
                   props = new[] { new Prop(Warning, .2f, -.26f, 1.15f, Face.In) } },
        new Line { name = "B2 - Building site, behind the hotel", job = "the building site's fence", alongX = false, at = 35.8f, from = -42f, to = -34f, outside = Vector3.right,
                   props = new[] { new Prop(Warning, -1.9f, -.26f, 1.15f, Face.In) } },
        new Line { name = "NW - Behind the north-west corner", job = "a fence between the two buildings", alongX = true, at = 31.6f, from = -42.75f, to = -38f, outside = Vector3.forward },
        new Line { name = "NE - Behind the north-east corner", job = "a fence between the two buildings", alongX = true, at = 31f, from = 39.75f, to = 45.75f, outside = Vector3.forward },
    };

    // The building site's materials, on the empty ground beyond B1 and B2 (world x, z, y, yaw).
    static readonly (string path, float x, float z, float y, float yaw)[] Site =
    {
        (Skip, 40.6f, -27.6f, 0f, 0f),
        (Pallet, 38.6f, -30.6f, 0f, 0f), (Sacks1, 38.2f, -30.6f, .39f, 0f), (Sacks2, 39.05f, -30.4f, .39f, 15f),
        (Beam, 37.6f, -33.2f, .18f, 90f), (Beam, 37.6f, -33.6f, .18f, 90f), (Beam, 37.7f, -33.4f, .54f, 90f),
        (Barrel1, 41.8f, -36.2f, 0f, 0f), (Barrel2, 42.5f, -36.6f, 0f, 40f), (Barrel3, 42.0f, -37.3f, 0f, 80f),
        (Crates, 39.4f, -38.9f, 0f, 0f),
        (Plank1, 42.8f, -30.5f, .06f, 0f), (Plank2, 42.8f, -30.2f, .06f, 0f), (Plank1, 42.9f, -29.9f, .06f, 0f),
        (Cone, 37.3f, -35.2f, 0f, 0f), (Cone, 37.4f, -40.6f, 0f, 0f),
    };

    // ------------------------------------------------------------------ menus

    [MenuItem(Menu + "Night walk 2 - Build the edges (road works at night, the corners for good)")]
    static void BuildEdges()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            var group = layout != null ? layout.transform.Find(NightGroupName) : null;
            var walk = group != null ? group.GetComponent<NightWalk>() : null;
            if (walk == null) throw new InvalidOperationException("No night group with a NightWalk: run Night walk 1 (the set-up) first.");

            // The corners stay by day, so first make sure they cross nobody's route.
            var conflicts = RouteConflicts(Corners(), report);
            if (conflicts.Count > 0)
                throw new InvalidOperationException("A permanent fence would cross a day route; nothing was built:\n  " + string.Join("\n  ", conflicts));

            Undo.SetCurrentGroupName("Night walk 2 - build the edges");
            int undoGroup = Undo.GetCurrentGroup();

            var oldWorks = group.Find(WorksName);
            if (oldWorks != null) { Undo.DestroyObjectImmediate(oldWorks.gameObject); report.AppendLine("Rebuilt: the old road works were taken out first."); }
            var works = BuildRoadWorks(group, report);
            Undo.RecordObject(walk, "Night walk - the road works come on at night");
            walk.nightOnly = walk.nightOnly.Where(g => g != null).Append(works).Distinct().ToArray();

            var oldEdges = layout.transform.Find(EdgesName);
            if (oldEdges != null) { Undo.DestroyObjectImmediate(oldEdges.gameObject); report.AppendLine("Rebuilt: the old corners were taken out first."); }
            BuildCorners(layout.transform, report);
            FitPatioBoxes(layout.transform, report);

            walk.nightCollision = NightCollisionList.Build(group, report);
            EditorUtility.SetDirty(walk);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string folder = LogFolder("night-edges");
            File.WriteAllText(Path.Combine(folder, "report.txt"), "Night walk 2 - build the edges\n\n" + report);
            Debug.Log(Tag + "Built the edges. Sweep the night next (Night walk 2 - Sweep the night), then read the scene diff before saving.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Building the edges FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Night walk 2 - Take the edges out again")]
    static void TakeOut()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            var layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            if (layout == null) throw new InvalidOperationException($"No '{LayoutRoot}' in the scene.");
            Undo.SetCurrentGroupName("Night walk 2 - take the edges out");
            int undoGroup = Undo.GetCurrentGroup();
            var group = layout.transform.Find(NightGroupName);
            var works = group != null ? group.Find(WorksName) : null;
            if (works != null) Undo.DestroyObjectImmediate(works.gameObject);
            var edges = layout.transform.Find(EdgesName);
            if (edges != null) Undo.DestroyObjectImmediate(edges.gameObject);
            var walk = group != null ? group.GetComponent<NightWalk>() : null;
            var report = new StringBuilder();
            if (walk != null)
            {
                Undo.RecordObject(walk, "Night walk - without the road works");
                walk.nightOnly = walk.nightOnly.Where(g => g != null).ToArray();
                walk.nightCollision = NightCollisionList.Build(group, report);
                EditorUtility.SetDirty(walk);
            }
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + "Took the edges out (Edit > Undo puts them back). The works lamp and gate materials stay; they are harmless.\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    // ------------------------------------------------------------------ building

    /// <summary>The night's road works, under the night group, switched off (NightWalk switches them on).</summary>
    internal static GameObject BuildRoadWorks(Transform nightGroup, StringBuilder report)
    {
        var root = new GameObject(WorksName);
        Undo.RegisterCreatedObjectUndo(root, "Night walk road works");
        root.transform.SetParent(nightGroup, false);
        Material lamp = LampMaterial();
        int barriers = 0, props = 0, missing = 0;
        foreach (var line in RoadWorks())
        {
            var site = new GameObject(line.name + " (" + line.job + ")");
            site.transform.SetParent(root.transform, false);
            float length = line.Length;
            int count = Mathf.CeilToInt(length / 1.40f);
            float width = length / count;
            for (int i = 0; i < count; i++)
            {
                float along = -length * .5f + width * (i + .5f);
                var b = Place(Barrier, site.transform, line, new Prop(Barrier, along, 0f, 0f, Face.W, null, new Vector3(width / 1.435f, 1f, 1f)), night: true);
                if (b == null) { missing++; continue; }
                barriers++;
            }
            // Warning boards on the barriers a quarter of the way in from each end, facing the 9 blocks.
            foreach (float quarter in new[] { -.25f, .25f })
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((quarter * length + length * .5f) / width - .5f), 0, count - 1);
                float along = -length * .5f + width * (i + .5f);
                if (Place(Warning, site.transform, line, new Prop(Warning, along, -.12f, .30f, Face.In), night: true) != null) props++;
            }
            foreach (var p in line.props)
                if (Place(p.path, site.transform, line, p, night: true) != null) props++; else missing++;
            // Amber lamps on the two end barriers and the middle one; a small light to see them by.
            foreach (float along in new[] { -length * .5f + .12f, 0f, length * .5f - .12f })
            {
                // Its own sphere collider stays: it is solid exactly as it looks.
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "Works lamp";
                glow.transform.SetParent(site.transform, false);
                glow.transform.position = line.Point(along, 0f, 1.04f);
                glow.transform.localScale = Vector3.one * .13f;
                var mr = glow.GetComponent<MeshRenderer>();
                mr.sharedMaterial = lamp;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            var lightGo = new GameObject("Works light");
            lightGo.transform.SetParent(site.transform, false);
            lightGo.transform.position = line.Point(0f, -.6f, 1.4f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .58f, .16f);
            light.range = 5.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            report.AppendLine($"  {line.name} ({line.job}): {Describe(line)}, {count} barriers");
        }
        root.SetActive(false);
        report.AppendLine($"Road works (night only, under '{NightGroupName}'): 8 sites, {barriers} barriers and {props} other pieces, each with exact collision of its own" +
                          (missing > 0 ? $"; {missing} piece(s) skipped (their prefab is missing: the Synty packs aren't in this copy)." : "."));
        return root;
    }

    static void BuildCorners(Transform layout, StringBuilder report)
    {
        var root = new GameObject(EdgesName);
        Undo.RegisterCreatedObjectUndo(root, "Edges of the 9 blocks");
        root.transform.SetParent(layout, false);
        int panels = 0, piers = 0, pieces = 0, missing = 0;
        foreach (var line in Corners())
        {
            var part = new GameObject(line.name);
            part.transform.SetParent(root.transform, false);
            float length = line.Length;
            const float pierWidth = .711f, gateWidth = 1.4f;
            if (line.gate)
            {
                // panel, pier, panel, pier, gate, pier, panel
                float p = (length - 3f * pierWidth - gateWidth) / 3f;
                float s = -length * .5f;
                FencePanel(part.transform, line, s, p, ref panels, ref missing); s += p;
                PierAt(part.transform, line, s + pierWidth * .5f, ref piers, ref missing); s += pierWidth;
                FencePanel(part.transform, line, s, p, ref panels, ref missing); s += p;
                PierAt(part.transform, line, s + pierWidth * .5f, ref piers, ref missing); s += pierWidth;
                IronGate(part.transform, line, s + gateWidth * .5f, gateWidth); s += gateWidth;
                PierAt(part.transform, line, s + pierWidth * .5f, ref piers, ref missing); s += pierWidth;
                FencePanel(part.transform, line, s, p, ref panels, ref missing);
            }
            else
            {
                // As many panels as keep them closest to their own 5 m, with a pier between each two.
                int n = 1;
                float best = float.MaxValue;
                for (int k = 1; k <= 6; k++)
                {
                    float p = (length - (k - 1) * pierWidth) / k;
                    if (p <= 0f) break;
                    float off = Mathf.Abs(Mathf.Log(p / 5f));
                    if (off < best) { best = off; n = k; }
                }
                float panel = (length - (n - 1) * pierWidth) / n;
                float s = -length * .5f;
                for (int k = 0; k < n; k++)
                {
                    FencePanel(part.transform, line, s, panel, ref panels, ref missing);
                    s += panel;
                    if (k < n - 1) { PierAt(part.transform, line, s + pierWidth * .5f, ref piers, ref missing); s += pierWidth; }
                }
            }
            foreach (var prop in line.props)
                if (Place(prop.path, part.transform, line, prop, night: false) != null) pieces++; else missing++;
            report.AppendLine($"  {line.name} ({line.job}): {Describe(line)}");
        }
        var site = new GameObject("Building site (beyond B1 and B2)");
        site.transform.SetParent(root.transform, false);
        foreach (var (path, x, z, y, yaw) in Site)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { missing++; continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, site.transform);
            go.transform.SetPositionAndRotation(new Vector3(x, Ground + y, z), Quaternion.Euler(0f, yaw, 0f));
            DayQuiet(go);
            pieces++;
        }
        report.AppendLine($"The corners, for good ('{EdgesName}'): {panels} fence panels, {piers} piers, a locked iron gate, {pieces} other pieces" +
                          (missing > 0 ? $"; {missing} skipped (prefab missing)." : ".") +
                          " No collision by day (their prefab colliders are off); the night's list makes them solid.");
    }

    // The sweep's two air walls on the patio (the A-frame sign's box and an outdoor chair's) were boxes a
    // little bigger than the things they stand for. Each box on the patio is fitted to its own object's
    // meshes: their bounds, in the box's own space. Safe to run again (a fitted box is left as it is).
    static readonly string[] PatioBoxes =
    {
        "18 - cafe furnishing/D - Entrance and waiting nook/Patio A-frame",
        "08 - front patio bistro nook",
    };

    static void FitPatioBoxes(Transform layout, StringBuilder report)
    {
        int fitted = 0, already = 0;
        foreach (string path in PatioBoxes)
        {
            var holder = layout.Find(path);
            if (holder == null) { report.AppendLine($"  The patio: '{path}' not found; its boxes are left as they are."); continue; }
            foreach (var box in holder.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.enabled || box.isTrigger) continue;
                var filters = box.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                if (filters.Length == 0) continue;
                bool any = false;
                var local = new Bounds();
                foreach (var f in filters)
                {
                    Bounds mb = f.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                        Vector3 p = box.transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                        if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                    }
                }
                if ((box.center - local.center).magnitude < .01f && (box.size - local.size).magnitude < .01f) { already++; continue; }
                Vector3 before = Vector3.Scale(box.size, box.transform.lossyScale);
                Undo.RecordObject(box, "Night walk - fit a patio box to its shape");
                box.center = local.center;
                box.size = local.size;
                if (PrefabUtility.IsPartOfPrefabInstance(box)) PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                Vector3 after = Vector3.Scale(box.size, box.transform.lossyScale);
                report.AppendLine($"  The patio: '{box.transform.name}' box fitted to its shape: {before.x:0.00} x {before.y:0.00} x {before.z:0.00} m -> {after.x:0.00} x {after.y:0.00} x {after.z:0.00} m.");
                fitted++;
            }
        }
        report.AppendLine($"The patio's boxes: {fitted} fitted to their shapes, {already} already fitted.");
    }

    static void FencePanel(Transform parent, Line line, float start, float length, ref int panels, ref int missing)
    {
        var p = Place(Fence, parent, line, new Prop(Fence, start, 0f, 0f, Face.W, null, new Vector3(length / 5f, 1f, 1f)), night: false);
        if (p != null) panels++; else missing++;
    }

    static void PierAt(Transform parent, Line line, float along, ref int piers, ref int missing)
    {
        var p = Place(Pier, parent, line, new Prop(Pier, along, 0f), night: false);
        if (p != null) piers++; else missing++;
    }

    // A plain iron gate between two piers: rails, bars and stiles, dark like the fence's railings.
    static void IronGate(Transform parent, Line line, float along, float width)
    {
        var gate = new GameObject("Iron gate (locked)");
        gate.transform.SetParent(parent, false);
        gate.transform.SetPositionAndRotation(line.Point(along, 0f, 0f), Quaternion.LookRotation(line.W, Vector3.up));
        Material iron = GateMaterial();
        void Bar(string name, Vector3 centre, Vector3 size)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(gate.transform, false);
            bar.transform.localPosition = centre;
            bar.transform.localScale = size;
            bar.GetComponent<MeshRenderer>().sharedMaterial = iron;
        }
        float half = width * .5f - .04f;
        Bar("Stile", new Vector3(-half, .98f, 0f), new Vector3(.06f, 1.9f, .06f));
        Bar("Stile", new Vector3(half, .98f, 0f), new Vector3(.06f, 1.9f, .06f));
        foreach (float y in new[] { .14f, 1.0f, 1.86f }) Bar("Rail", new Vector3(0f, y, 0f), new Vector3(width - .1f, .05f, .04f));
        for (float x = -half + .15f; x < half - .05f; x += .15f) Bar("Bar", new Vector3(x, .98f, 0f), new Vector3(.03f, 1.78f, .03f));
    }

    // Puts one prefab on a line. Night pieces get exact collision of their own; permanent ones get none
    // by day (the night's list makes them solid).
    static GameObject Place(string path, Transform parent, Line line, Prop p, bool night)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Quaternion facing = Quaternion.LookRotation(line.Dir(p.face), Vector3.up) * Quaternion.Euler(p.turn);
        go.transform.SetPositionAndRotation(line.Point(p.along, p.outward, p.y), facing);
        go.transform.localScale = Vector3.Scale(go.transform.localScale, p.scale);
        if (night) NightSolid(go); else DayQuiet(go);
        return go;
    }

    // The prefab's own (rough, convex) colliders off; exact collision from each of its meshes instead.
    static void NightSolid(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        var cache = new Dictionary<Mesh, bool>();
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = filter.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !NightCollisionList.CanCollide(filter.sharedMesh, cache)) continue;
            var mc = filter.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = filter.sharedMesh;
            mc.convex = false;
        }
    }

    // Like the rest of the city: no collision by day.
    static void DayQuiet(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }

    static string Describe(Line line)
    {
        Vector3 a = line.Start, b = line.End;
        return $"({a.x:0.00}, {a.z:0.00}) to ({b.x:0.00}, {b.z:0.00}), {line.Length:0.00} m, outside to the {Compass(line.N)}";
    }

    static string Compass(Vector3 d) => Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x > 0 ? "east" : "west") : (d.z > 0 ? "north" : "south");

    // ------------------------------------------------------------------ the day's routes

    // Every route something walks or drives by day: StreetLife's walkers and cars, and the café's
    // visitors (on foot from the neighbours' doors, from the car park, and the cars' own paths).
    static List<string> RouteConflicts(IEnumerable<Line> fences, StringBuilder report)
    {
        var routes = new List<(string name, Vector3[] points, bool closed)>();
        foreach (var life in CityPackChecks.InScene<StreetLife>())
            foreach (var actor in life.actors)
            {
                if (actor == null || actor.waypoints == null) continue;
                var points = actor.waypoints.Where(t => t != null).Select(t => t.position).ToArray();
                if (points.Length >= 2) routes.Add(((actor.actor != null ? actor.actor.name : "street actor") + " (StreetLife)", points, !actor.openRoute));
            }
        foreach (var arrivals in CityPackChecks.InScene<CafeArrivals>())
        {
            foreach (var r in arrivals.EditorFootRoutes) if (r?.points != null && r.points.Length >= 2) routes.Add(("foot route " + r.name, r.points, false));
            var lot = arrivals.EditorLotToDoor;
            if (lot?.points != null && lot.points.Length >= 2) routes.Add(("car park to the door", lot.points, false));
            foreach (var stall in arrivals.EditorStalls)
            {
                if (stall == null) continue;
                if (stall.walk?.points != null && stall.walk.points.Length >= 2) routes.Add(("stall " + stall.name + " walk", stall.walk.points, false));
                foreach (var path in new[] { stall.entry, stall.backOut, stall.toExit })
                    if (path != null && path.Length >= 2) routes.Add(("stall " + stall.name + " car", path.Select(v => new Vector3(v.x, v.y, v.z)).ToArray(), false));
            }
            foreach (var path in new[] { arrivals.EditorTurnIn, arrivals.EditorTurnOut })
                if (path != null && path.Length >= 2) routes.Add(("café car turning", path.Select(v => new Vector3(v.x, v.y, v.z)).ToArray(), false));
        }
        var conflicts = new List<string>();
        foreach (var line in fences)
        {
            Vector2 a = Flat(line.Start), b = Flat(line.End);
            foreach (var (name, points, closed) in routes)
            {
                int segments = closed ? points.Length : points.Length - 1;
                for (int i = 0; i < segments; i++)
                {
                    Vector2 p = Flat(points[i]), q = Flat(points[(i + 1) % points.Length]);
                    float d = SegmentDistance(a, b, p, q);
                    if (d < .75f) { conflicts.Add($"{line.name} is {d:0.00} m from '{name}' (segment {i})"); break; }
                }
            }
        }
        report.AppendLine($"Day routes checked against the permanent fences: {routes.Count} routes, {conflicts.Count} too close (under 0.75 m).");
        return conflicts;
    }

    static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Crosses(a, b, c, d)) return 0f;
        return Mathf.Min(Mathf.Min(PointToSegment(a, c, d), PointToSegment(b, c, d)), Mathf.Min(PointToSegment(c, a, b), PointToSegment(d, a, b)));
    }

    static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
        float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return (p - (a + ab * t)).magnitude;
    }

    // ------------------------------------------------------------------ materials (the project's own, no pack)

    static Material LampMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
        if (m == null)
        {
            m = new Material(shader) { name = "Night walk - works lamp" };
            AssetDatabase.CreateAsset(m, LampMaterialPath);
        }
        m.shader = shader;
        m.SetColor("_BaseColor", new Color(2.6f, .8f, .05f));   // amber: brighter mixes bloom out to lime-yellow
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material GateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(GateMaterialPath);
        if (m == null)
        {
            m = new Material(shader) { name = "Night walk - iron gate" };
            AssetDatabase.CreateAsset(m, GateMaterialPath);
        }
        m.shader = shader;
        m.SetColor("_BaseColor", new Color(.075f, .075f, .085f));
        m.SetFloat("_Metallic", .55f);
        m.SetFloat("_Smoothness", .35f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
