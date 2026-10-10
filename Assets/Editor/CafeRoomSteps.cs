#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using S = CafeRedesignShared;

// ---------------------------------------------------------------------------
// THE CAFÉ REDESIGN: THE FLOOR, AND THE ROOM'S CEILING, LIGHT AND FEW THINGS (10 Oct 2026)
//
// His words (9 Oct, 20:12): "the decorations, the lines for words on the sign and or anything just looks tacky. i really
// want an indie level cafe design done." His decision 4 (23:40), the floor: "whichever looks and feels the best", Claude
// picks it from photos. The plan: claude/studio/cafe-sf-research-and-redesign.md §5, §6.6, §6.7, §6.9, §6.10, §7.3.
//
//   Fixit Fidget > Cafe redesign > Floor 0 - Photograph both floors (read-only)
//       The café's inside floor is the scene's root "Floor" (the built-in plane, 14.8 x 18 m, UVs 0..1 across it, a
//       MeshCollider and the NavMeshSurface on it) and the staff zone's grey inset behind the counter. For the photos only
//       they wear CF_Floor_Oak, then CF_Floor_Concrete, on temporary copies of their meshes with world UVs in metres (the
//       kit's floor textures tile per metre), with the rugs hidden; then everything is put back exactly, in a finally,
//       and checked. Photos: Logs/CafeRedesign/floor-ab-<time>/ now-*, oak-*, concrete-*. No record, no undo.
//   Fixit Fidget > Cafe redesign > Floor 1 - Lay the oak floor (scene)
//   Fixit Fidget > Cafe redesign > Floor 1 - Lay the concrete floor (scene)
//       Record step "Floor 1 - The floor": both surfaces get the chosen material and a copy of their mesh with world UVs
//       in metres (saved in Cafe redesign geometry.asset). The oak's boards run north-south (along z); the concrete's 3 m
//       saw cuts are moved 1.5 m off the door's centre line. No collider changes: the MeshCollider keeps its own mesh.
//   Fixit Fidget > Cafe redesign > Floor 1 - Undo (scene)
//
//   Fixit Fidget > Cafe redesign > Room 1 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Room 1 1 - Hang the ceiling, light the tables, dress the room (scene)
//       Record step "Room 1 - Ceiling, light and the room's few things" (after Bar 1, the repair bar, Bar 2, Seats 1):
//       five chestnut beams and three skylight wells (their lights, the ceiling deck cut for them); every pendant's shade
//       cream enamel, seven pendants over the new tables in place of the four over the old ones; five plants; the notice
//       board and the photo ledge on the east wall's piers; the repair bar's REPAIRS sign from the beam over it; the rugs,
//       the runner, the doormat, the hanging pots, the three prints, the cat portrait, the old pot plants and the window
//       ledges' small plants, books and mugs off (research §6.9, §6.10), each with its reason in the report.
//   Fixit Fidget > Cafe redesign > Room 1 1 - Undo (scene)
//
// Why the table pendants are made, not moved: the four over the old round tables are one combined mesh per material
// inside the joinery model (PendantFixtures, CafeInteriorPass), their lights separate objects ("V3 table pendant",
// 6 at 6.8 m). They can't move one by one. The counter's four (session 6) are that same pendant, cut out of PendantFixtures
// as its own mesh with its own light (#FFC98F, 1.2, 4.5): the seven over the new tables are copies of it, so the café has
// one pendant, eleven times, one light each. The old four's bodies are hidden (renderers off: the joinery is found by
// name) and their lights switched off.
// ---------------------------------------------------------------------------
internal static class CafeRoomSteps
{
    // ---- the steps ----
    const string FloorTitle = "Floor 1 - The floor";
    const string FloorLabel = "floor-1";
    const string FloorPhotosTitle = "Floor 0 - Photograph both floors";
    const string FloorPhotosLabel = "floor-ab";
    const string RoomTitle = "Room 1 - Ceiling, light and the room's few things";
    const string RoomLabel = "room-1";
    const string Bar1 = "Bar 1 - The front counter", Bar2 = "Bar 2 - The back bar", Seats1 = "Seats 1 - The San Francisco seat plan";
    const string RoomGroup = "Room 1 - ceiling, light and the room's few things";

    // ---- the floor ----
    const string FloorName = "Floor";                                                   // a scene root, found by name by the layout code
    const string InsetPath = "01 - room and windows/Floor grain and runner/Staff floor inset";
    const string Oak = "CF_Floor_Oak", Concrete = "CF_Floor_Concrete";
    const float OakTile = 1.8f, ConcreteTile = 3.0f;                                    // the textures lead's tiles (textures report §5)
    // The oak: an 18 cm board centred on the door's axis (x 0), not a seam. The concrete: its saw cut is on the 3 m tile's
    // edge, so u = x + 1.5 puts the joints at x -4.5, -1.5, 1.5, 4.5 (the brief: never through the door's centre line).
    static readonly Vector2 OakOffset = new Vector2(.09f, 0f), ConcreteOffset = new Vector2(1.5f, 0f);
    static readonly Rect Room = Rect.MinMaxRect(-7.4f, 0f, 7.4f, 18f);

    // ---- the ceiling (research §6.6; props report §5; Claude's QA: wells between the beams) ----
    const string DeckPath = "06 - atmosphere placeholders/Inside-only ceiling";
    static readonly float[] BeamZ = { 2f, 5f, 8f, 11f, 14f };
    static readonly float[] WellZ = { 3.5f, 6.5f, 9.5f };
    const float DeckY = 3.20f;
    const float HoleHalf = .62f;                // a 1.24 m hole: the well's lining is 1.232 outside, its trim 1.34 (it hides the cut)
    const float TrimHalf = .67f;
    // The beam as built (cafe_props.beam): chestnut 14.788 x 0.20 x 0.32 hung from its top, steel hangers to x +-7.40 that
    // widen it to +-0.164 and drop it to 0.325 over their last 0.12 m.
    const float BeamHalfLength = 7.394f, BeamHalfWidth = .10f, BeamDepth = .32f;
    const float HangerFrom = 7.28f, HangerHalfWidth = .164f, HangerDepth = .325f;
    // The skylight's spot (research §6.7; brief): at the glass, straight down.
    static readonly Color SkylightColour = new Color(1f, 241f / 255f, 221f / 255f);   // #FFF1DD
    const float SkylightIntensity = 1f, SkylightRange = 6f, SkylightAngle = 70f, SkylightLocalY = .43f;

    // ---- the pendants (research §6.7) ----
    const string CounterPendantsPath = "25 - counter pendants (session 6)";
    const string JoineryPath = "12 - authored cafe interior/Layered windows, open doors and woven textiles";
    const string V3LightsPath = "10 - cohesive neighborhood V4/V3 lights";
    const string V3TablePendant = "V3 table pendant";
    static readonly Vector2[] OldTablePendants = { new Vector2(3f, 3.5f), new Vector2(3f, 9f), new Vector2(-3f, 3.5f), new Vector2(-3f, 9f) };
    static readonly Color PendantColour = new Color(1f, 201f / 255f, 143f / 255f);    // #FFC98F, as the counter's
    const float PendantIntensity = 1.2f, PendantRange = 4.5f;                           // the counter's lights, as session 6 made them
    // The new table pendants' light: the brief's 1.2, the counter's. Today's four over the old tables ("V3 table pendant")
    // are 6.0 at 6.8 m, so the seats get less lamp light; if the evening photos read dim, this one number raises all seven.
    const float TablePendantIntensity = PendantIntensity;
    // Over a table the pendant hangs low, its shade's rim 0.95 m over the table's top (Claude, 10 Oct: at the counter's drop
    // the seven hung 1.9 m over the tables and read as ceiling lights, not the tables' own lamps); a thin cord carries it
    // from where the counter's pendants meet the ceiling down to its own stem.
    const float TableTopHeight = .75f, TableRimOverTop = .95f, CordDiameter = .008f;
    const float CordClear = .10f;               // the cord clears every beam's face by this (the brief)
    const float MeshClear = .03f;               // and the whole lamp (rose, cap, shade) clears every beam by this
    const float ShiftStep = .05f, ShiftMax = .25f;

    // ---- the tables Seats 1 placed (common brief §6) ----
    const string Square2 = "CK_Table_Square2", Communal = "CK_CommunalTable", Bistro = "CK_Table_BistroRound";
    static readonly Vector2[] PlanSquare2 = { new Vector2(-6.30f, 4.69f), new Vector2(-6.30f, 6.79f), new Vector2(-6.30f, 8.89f) };
    static readonly Vector2 PlanCommunal = new Vector2(-2.95f, 6.20f);
    static readonly Vector2[] PlanBistro = { new Vector2(3f, 4.6f), new Vector2(3f, 7.8f) };
    const float CommunalPendantOffset = .90f;   // z ~ 5.3 and 7.1: over each pair of stools
    const float TableTolerance = .30f;

    // ---- the plants (props report §5) ----
    sealed class PlantSpec
    {
        public string piece, name, why; public Vector2 at; public float yaw, radius, height;
        public PlantSpec(string piece, string name, float x, float z, float yaw, float radius, float height, string why)
        { this.piece = piece; this.name = name; at = new Vector2(x, z); this.yaw = yaw; this.radius = radius; this.height = height; this.why = why; }
    }
    static readonly PlantSpec[] Plants =
    {
        new PlantSpec("CK_FiddleLeafFig", "CK_FiddleLeafFig (front-left corner)", -6.85f, .75f, 45f, .21f, .40f, "the front-left corner (Philz had one); turned to face the room"),
        new PlantSpec("CK_OliveTree", "CK_OliveTree (door, west)", -1.6f, .5f, 0f, .22f, .37f, "a welcome either side of the door"),
        new PlantSpec("CK_OliveTree", "CK_OliveTree (door, east)", 1.6f, .5f, 140f, .22f, .37f, "the pair's second, turned 140 so they aren't twins"),
        new PlantSpec("CK_RubberPlant", "CK_RubberPlant (between the bistro tables)", 3f, 6.2f, 0f, .17f, .31f, "the big plant between the bistro 2-tops"),
        new PlantSpec("CK_Monstera", "CK_Monstera (the counter's west end)", -6.8f, 12.6f, 0f, .20f, .33f, "in front of the counter's plain west end"),
    };
    // The door lane (common brief §7) and the door-to-counter lane furniture must keep out of (CafeFurnishing).
    static readonly Rect DoorLane = Rect.MinMaxRect(-1f, 0f, 1f, 2f);
    static readonly Rect DoorToCounter = Rect.MinMaxRect(-1f, -1.2f, 1f, 12.5f);

    // ---- the east wall (CafeStreetUpgrade.V3CafeWindows: the posts at z 0, 2.6, 4.3, 5.1, 6.9, 9.1, 12.2, 15.2, 18; the
    //      plaster piers z 2.925-4.075, 5.1-6.9, 16.375-18.125; CafeWallSteps: the glass sits inside them) ----
    const string EastPlasterPath = "09 - neighborhood surrounds V3/V3 - courtyard-facing cafe windows/Cloud plaster";
    const float EastFace = 7.40f;
    // On the small-print pier (the brief's "z ~ 2.6" is a 16 cm post with glass both sides), by the waiting nook.
    static readonly Vector3 NoticeBoardAt = new Vector3(EastFace, 1.15f, 3.50f);
    // On the local-art pier: the front-of-house pier nearest the counter (the back corner's pier can't take 1.2 m without
    // reaching into the back wall's fade: CutawayWall counts anything within 0.55 m of a wall's face as hung on it).
    static readonly Vector3 PhotoLedgeAt = new Vector3(EastFace, 1.30f, 6.00f);

    // ---- the repair bar's sign (textures report §5) ----
    const float EnamelX = -4.2f, EnamelZ = 14f;

    // ---- what goes off (research §5 items 4-5, §6.9, §7.3; Room 1 owns these) ----
    sealed class OffSpec
    {
        public string path, why, owner; public bool hideOnly;
        public OffSpec(string path, bool hideOnly, string owner, string why) { this.path = path; this.hideOnly = hideOnly; this.owner = owner; this.why = why; }
    }
    static readonly OffSpec[] Offs =
    {
        new OffSpec("18 - cafe furnishing/A - Window lounge/Lounge kilim", false, null,
            "the kilim: one of four patterned floor pieces at once, the loudest things in the overhead view (research §5 item 5); no rugs inside (§6.9)"),
        new OffSpec("18 - cafe furnishing/B - Reading nook/Reading table rug", false, null,
            "the round rug: as the kilim (research §5 item 5)"),
        new OffSpec("18 - cafe furnishing/D - Entrance and waiting nook/Hanging plant (door, left)", false, null,
            "a small pot on a long string (research §5 item 5): the five big plants replace them (§6.10)"),
        new OffSpec("18 - cafe furnishing/D - Entrance and waiting nook/Hanging plant (door, right)", false, null,
            "a small pot on a long string (research §5 item 5): the five big plants replace them (§6.10)"),
        new OffSpec("18 - cafe furnishing/C - Window bar/Hanging plant (window bar)", false, null,
            "a small pot on a long string (research §5 item 5): the five big plants replace them (§6.10)"),
        new OffSpec("18 - cafe furnishing/A - Window lounge/Hanging plant (between the sofas)", false, null,
            "a small pot on a long string (research §5 item 5): the five big plants replace them (§6.10)"),
        new OffSpec("06 - atmosphere placeholders/Small neighborhood print", false, null,
            "the abstract print, one of three copies (research §5 item 5); no placeholder art (§6.9): the notice board takes its pier"),
        new OffSpec("06 - atmosphere placeholders/Right wall local art", false, null,
            "the abstract print, one of three copies (research §5 item 5); no placeholder art (§6.9): the photo ledge takes its pier"),
        new OffSpec("06 - atmosphere placeholders/Bay sunset print", false, Bar2,
            "the abstract print, the third copy, on the back wall (research §5 item 5; §6.4: nothing on that wall but the menu and the shelf)"),
        // NightZeroSteps (Bins 2) finds it by name: hidden, never switched off.
        new OffSpec("06 - atmosphere placeholders/Cat portrait study", true, null,
            "the cat silhouette portrait that reads as Halloween (research §5 item 5; his note on the cat: Content's lost-cat flyer is friendly)"),
        // NightZeroSteps finds it by name, and the cut-away check expects it on the back wall: hidden, never switched off.
        new OffSpec("16 - POLYGON cafe details/Walls/Walls - Clock_01", true, Bar2,
            "a wall clock that disagrees with the HUD's time (research §5 item 5; §6.9: no clock)"),
        new OffSpec("18 - cafe furnishing/A - Window lounge/Floor lamp (front sofa)", false, Seats1,
            "a floor lamp of the lounge set (research §5 item 5, §7.3)"),
        new OffSpec("18 - cafe furnishing/A - Window lounge/Floor lamp (rear sofa)", false, Seats1,
            "a floor lamp of the lounge set (research §5 item 5, §7.3)"),
    };
    const string SyntyPlantsPath = "16 - POLYGON cafe details/Plants";
    const string PrimitiveRunnerPath = "01 - room and windows/Floor grain and runner";
    static readonly string[] LedgeProps =
    {
        "18 - cafe furnishing/C - Window bar/Sill plant", "18 - cafe furnishing/C - Window bar/Sill books", "18 - cafe furnishing/C - Window bar/Sill mug",
        "18 - cafe furnishing/A - Window lounge/Ledge plant", "18 - cafe furnishing/A - Window lounge/Mug left on the ledge",
    };
    static readonly string[] OldSeatingGroups =
    {
        "18 - cafe furnishing/A - Window lounge", "18 - cafe furnishing/B - Reading nook", "18 - cafe furnishing/C - Window bar",
        "11 - neighborhood reading shelf", "05 - seating - four neighborhood tables", "12 - authored cafe interior/Window upholstered seating",
    };

    // ---- the kit ----
    static readonly string[] RoomPieces = { "CK_Beam", "CK_SkylightWell", "CK_FiddleLeafFig", "CK_OliveTree", "CK_RubberPlant", "CK_Monstera", "CK_NoticeBoard", "CK_PhotoLedge", "CK_EnamelSign" };
    // Size (across, up, deep) and bottom as the props and signs manifests say (props/fbx/cafe_props.json, textures/fbx/cafe_signs.json).
    static readonly Dictionary<string, (Vector3 size, float bottom)> Built = new Dictionary<string, (Vector3, float)>
    {
        ["CK_Beam"] = (new Vector3(14.8f, .325f, .328f), -.325f),
        ["CK_SkylightWell"] = (new Vector3(1.34f, .47f, 1.34f), -.018f),
        ["CK_FiddleLeafFig"] = (new Vector3(.646f, 1.815f, .633f), 0f),
        ["CK_OliveTree"] = (new Vector3(.764f, 1.636f, .691f), 0f),
        ["CK_RubberPlant"] = (new Vector3(.675f, 1.279f, .646f), 0f),
        ["CK_Monstera"] = (new Vector3(1.064f, .943f, .934f), 0f),
        ["CK_NoticeBoard"] = (new Vector3(.96f, .66f, .025f), 0f),
        ["CK_PhotoLedge"] = (new Vector3(1.2f, .221f, .12f), 0f),
        ["CK_EnamelSign"] = (new Vector3(.9f, .43f, .017f), -.43f),
    };

    // ================================================================== menus

    [MenuItem(S.Menu + "Floor 0 - Photograph both floors (read-only)")]
    static void PhotographBothFloors() => S.Check(FloorPhotosTitle, FloorPhotosLabel, FloorPhotos);

    [MenuItem(S.Menu + "Floor 1 - Lay the oak floor (scene)")]
    static void LayOak() => LayTheFloor(Oak);

    [MenuItem(S.Menu + "Floor 1 - Lay the concrete floor (scene)")]
    static void LayConcrete() => LayTheFloor(Concrete);

    [MenuItem(S.Menu + "Floor 1 - Undo (scene)")]
    static void UndoFloor() => S.UndoStep(FloorTitle, FloorLabel, null, rebake: false);

    [MenuItem(S.Menu + "Room 1 0 - Check first (read-only)")]
    static void CheckRoom() => S.Check(RoomTitle, RoomLabel + "-check", run => ReportPlan(run, SurveyRoom(run)));

    [MenuItem(S.Menu + "Room 1 1 - Hang the ceiling, light the tables, dress the room (scene)")]
    static void DoRoom()
    {
        RoomPlan plan = null;
        S.Step(RoomTitle, RoomLabel,
            run => { plan = SurveyRoom(run); ReportPlan(run, plan); },
            run => ChangeRoom(run, plan),
            run => RoomPhotos(run, plan),
            guards: true, rebake: true, roomPhotos: true);
    }

    [MenuItem(S.Menu + "Room 1 1 - Undo (scene)")]
    static void UndoRoom() => S.UndoStep(RoomTitle, RoomLabel);

    [MenuItem(S.Menu + "Floor 0 - Photograph both floors (read-only)", true)]
    [MenuItem(S.Menu + "Floor 1 - Lay the oak floor (scene)", true)]
    [MenuItem(S.Menu + "Floor 1 - Lay the concrete floor (scene)", true)]
    [MenuItem(S.Menu + "Floor 1 - Undo (scene)", true)]
    [MenuItem(S.Menu + "Room 1 0 - Check first (read-only)", true)]
    [MenuItem(S.Menu + "Room 1 1 - Hang the ceiling, light the tables, dress the room (scene)", true)]
    [MenuItem(S.Menu + "Room 1 1 - Undo (scene)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== THE FLOOR: what it is

    sealed class FloorSurface
    {
        public string what;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public MeshCollider collider;      // the root floor's: never touched
        public Material[] materials;
        public Mesh mesh;
        public bool metres;                // its UVs are already world metres
    }

    // The café's inside floor: the root "Floor" (the walkable plane, x -7.4..7.4, z 0..18) and the staff zone's inset
    // behind the counter (a 1.65 cm cube on it, renderer only: the NavMesh build ignores its group). The street's apron and
    // the patio are not the café's floor.
    static List<FloorSurface> FindFloor(S.Run run)
    {
        var found = new List<FloorSurface>();
        GameObject[] roots = run.Scene.GetRootGameObjects().Where(g => g.name == FloorName).ToArray();
        run.Require(roots.Length == 1, $"Expected one scene root called '{FloorName}' (the café's walkable floor), found {roots.Length}.");
        GameObject floor = roots[0];
        var f = new FloorSurface
        {
            what = "the café's floor (scene root 'Floor')", filter = floor.GetComponent<MeshFilter>(), renderer = floor.GetComponent<MeshRenderer>(),
            collider = floor.GetComponent<MeshCollider>()
        };
        run.Require(f.filter != null && f.filter.sharedMesh != null && f.renderer != null, "'Floor' has no MeshFilter with a mesh, or no MeshRenderer.");
        Bounds b = f.renderer.bounds;
        run.Require(Mathf.Abs(b.min.x - Room.xMin) < .02f && Mathf.Abs(b.max.x - Room.xMax) < .02f && Mathf.Abs(b.min.z - Room.yMin) < .02f &&
                    Mathf.Abs(b.max.z - Room.yMax) < .02f && Mathf.Abs(b.max.y) < .01f && b.size.y < .01f,
                    $"'Floor' isn't the café's floor as measured (x -7.40..7.40, z 0..18 at y 0): it covers {S.BoundsText(b)}.");
        found.Add(f);
        Transform inset = S.Find(run, InsetPath);
        if (inset != null && inset.gameObject.activeInHierarchy && inset.GetComponent<MeshRenderer>() != null && inset.GetComponent<MeshRenderer>().enabled)
        {
            var i = new FloorSurface { what = "the staff zone's inset behind the counter", filter = inset.GetComponent<MeshFilter>(), renderer = inset.GetComponent<MeshRenderer>() };
            run.Require(i.filter != null && i.filter.sharedMesh != null, $"{InsetPath} has no mesh.");
            run.Require(inset.GetComponent<Collider>() == null, $"{InsetPath} has a collider: it was renderer-only when this step was written.");
            found.Add(i);
        }
        else run.Info($"No visible staff floor inset ({InsetPath}): only the root floor is laid.");
        foreach (FloorSurface s in found)
        {
            s.materials = s.renderer.sharedMaterials;
            s.mesh = s.filter.sharedMesh;
            s.metres = UVsAreWorldMetres(s.filter, Vector2.zero, out string how);
            run.Info($"{s.what}: {S.BoundsText(s.renderer.bounds)}, mesh '{s.mesh.name}' ({s.mesh.vertexCount} vertices), wears " +
                     $"{string.Join(", ", s.materials.Select(m => m != null ? m.name : "none"))}; its UVs: {how}.");
        }
        return found;
    }

    // Do the mesh's UVs equal world (x + offset.x, z + offset.y) in metres on every upward face (within 1 mm)?
    static bool UVsAreWorldMetres(MeshFilter f, Vector2 offset, out string how)
    {
        MeshData md = MeshData.Read(f.sharedMesh);
        if (md == null || md.uv == null || md.uv.Length != md.v.Length) { how = "none readable"; return false; }
        float worst = 0f;
        Vector2 umin = new Vector2(float.MaxValue, float.MaxValue), umax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < md.v.Length; i++)
        {
            umin = Vector2.Min(umin, md.uv[i]);
            umax = Vector2.Max(umax, md.uv[i]);
            Vector3 n = md.n != null && md.n.Length == md.v.Length ? f.transform.TransformDirection(md.n[i]) : Vector3.up;
            if (Mathf.Abs(n.y) < .6f) continue;
            Vector3 p = f.transform.TransformPoint(md.v[i]);
            worst = Mathf.Max(worst, Mathf.Max(Mathf.Abs(md.uv[i].x - (p.x + offset.x)), Mathf.Abs(md.uv[i].y - (p.z + offset.y))));
        }
        bool metres = worst <= .001f;
        how = metres ? "world metres (u = x, v = z)" : $"u {S.F(umin.x, "0.##")}..{S.F(umax.x, "0.##")}, v {S.F(umin.y, "0.##")}..{S.F(umax.y, "0.##")}, not world metres (worst {S.F(worst, "0.##")} m off)";
        return metres;
    }

    // A copy of the surface's mesh with world UVs in metres: CafeStreetUpgrade.WorldUV's mapping (upward faces u = x, v = z;
    // faces along x: u = z, v = y; along z: u = x, v = y), shifted by the floor's offset.
    static Mesh WorldUVCopy(MeshFilter f, Vector2 offset, string name)
    {
        Mesh mesh = Object.Instantiate(f.sharedMesh);
        mesh.name = name;
        Vector3[] v = mesh.vertices;
        Vector3[] n = mesh.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = f.transform.TransformPoint(v[i]);
            Vector3 d = n.Length == v.Length ? f.transform.TransformDirection(n[i]) : Vector3.up;
            uv[i] = Mathf.Abs(d.y) > .6f ? new Vector2(p.x + offset.x, p.z + offset.y)
                  : Mathf.Abs(d.x) > .6f ? new Vector2(p.z + offset.y, p.y) : new Vector2(p.x + offset.x, p.y);
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        return mesh;
    }

    // The kit's floor material, as Kit 1 made it: the texture on, tiled once per tile (textures report §5).
    static Material FloorMaterial(S.Run run, string name)
    {
        string path = S.KitMaterials + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        run.Require(m != null, $"{path} isn't there: run Fixit Fidget > Cafe redesign > Kit 1 first.");
        float tile = name == Oak ? OakTile : ConcreteTile;
        bool textured = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
        Vector2 tiling = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap") : Vector2.zero;
        run.Check(textured, $"{name} wears its texture ({(textured ? m.GetTexture("_BaseMap").name : "none")})");
        run.Check(Mathf.Abs(tiling.x - 1f / tile) < .005f && Mathf.Abs(tiling.y - 1f / tile) < .005f,
                  $"{name} tiles once every {S.F(tile, "0.0")} m (_BaseMap tiling {S.F(tiling.x, "0.0000")} x {S.F(tiling.y, "0.0000")}, needs {S.F(1f / tile, "0.0000")})");
        return m;
    }

    static Vector2 OffsetFor(string material) => material == Oak ? OakOffset : ConcreteOffset;

    // ================================================================== FLOOR 0: both floors photographed, nothing kept

    static readonly (string name, Vector3 from, Vector3 to, float fov)[] FloorViews =
    {
        ("2-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, .8f, 12f), 72f),
        ("3-from-the-counter", new Vector3(0f, 1.65f, 15f), new Vector3(0f, .5f, 4f), 72f),
        ("4-a-seats-eye", new Vector3(-5.6f, 1.2f, 6.8f), new Vector3(2f, .3f, 6f), 62f),
        ("5-low-the-boards-or-joints", new Vector3(3f, 1f, 3f), new Vector3(-1f, 0f, 7f), 60f),
    };

    static void FloorPhotos(S.Run run)
    {
        List<FloorSurface> surfaces = FindFloor(run);
        Material oak = FloorMaterial(run, Oak), concrete = FloorMaterial(run, Concrete);
        if (run.Failures > 0) throw new InvalidOperationException("The kit's floor materials aren't as Kit 1 should make them (see above): no photos taken.");
        bool wasDirty = run.Scene.isDirty;
        List<Renderer> rugs = FloorCoverings(run).SelectMany(c => c.renderers).Where(r => r.enabled && !r.forceRenderingOff).Distinct().ToList();
        run.Info($"For the photos only, the rugs, the runner and the doormat are hidden (Room 1 takes them away): {rugs.Count} renderer(s).");

        string folder = Path.Combine(S.LogRoot, FloorPhotosLabel + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        run.Folder = folder;                    // the shared Photo() writes into the run's folder, the report goes beside them
        var temporary = new List<Mesh>();
        try
        {
            foreach (Renderer r in rugs) r.forceRenderingOff = true;
            Shoot(run, "now");
            foreach ((string name, Material material) in new[] { ("oak", oak), ("concrete", concrete) })
            {
                string choice = name == "oak" ? Oak : Concrete;
                foreach (FloorSurface s in surfaces)
                {
                    Mesh copy = WorldUVCopy(s.filter, OffsetFor(choice), s.mesh.name + " (world UV, " + name + ", temporary)");
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    temporary.Add(copy);
                    s.filter.sharedMesh = copy;
                    s.renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, s.materials.Length)).ToArray();
                }
                Shoot(run, name);
            }
        }
        finally
        {
            foreach (FloorSurface s in surfaces)
            {
                if (s.filter != null) s.filter.sharedMesh = s.mesh;
                if (s.renderer != null) s.renderer.sharedMaterials = s.materials;
            }
            foreach (Renderer r in rugs) if (r != null) r.forceRenderingOff = false;
            foreach (Mesh m in temporary) if (m != null) Object.DestroyImmediate(m);
            run.Phase = "0-check";
        }

        // The scene as it began.
        foreach (FloorSurface s in surfaces)
        {
            run.Check(s.filter.sharedMesh == s.mesh, $"{s.what}: its own mesh is back ('{s.filter.sharedMesh.name}')");
            run.Check(s.renderer.sharedMaterials.SequenceEqual(s.materials), $"{s.what}: its own material(s) are back ({string.Join(", ", s.renderer.sharedMaterials.Select(m => m != null ? m.name : "none"))})");
            if (s.collider != null) run.Info($"{s.what}: its MeshCollider was never touched ('{(s.collider.sharedMesh != null ? s.collider.sharedMesh.name : "none")}').");
        }
        run.Check(rugs.All(r => r == null || !r.forceRenderingOff), "the rugs are drawn again");
        run.Check(run.Scene.isDirty == wasDirty, wasDirty ? "the scene was already modified before, and still is" : "the scene is still unmodified (nothing to save)");
        run.Line();
        run.Line($"Photos: {folder}: now-*, oak-*, concrete-* (1 the game camera, 2 from the door, 3 from the counter, 4 a seat's eye, 5 low).");
        run.Line($"Floor 1 would lay the chosen floor on {surfaces.Count} surface(s) with world UVs in metres (oak: a board on the door's axis, " +
                 "boards north-south; concrete: joints at x -4.5, -1.5, 1.5, 4.5 and every 3 m in z). Pick from the photos.");
    }

    // Floor 0's photos under a phase of its own (now-, oak-, concrete-).
    static void Shoot(S.Run run, string phase)
    {
        run.Phase = phase;
        FloorShots(run);
    }

    // The floor's five views; the run's phase names the files (Floor 1: 1-before-, 2-after-).
    static void FloorShots(S.Run run)
    {
        S.GameCameraPhoto(run, "1-game-camera");
        foreach (var v in FloorViews) S.Photo(run, v.name, v.from, v.to, v.fov);
    }

    // ================================================================== FLOOR 1: lay the chosen floor

    static void LayTheFloor(string choice)
    {
        List<FloorSurface> surfaces = null;
        Material material = null;
        var before = new Dictionary<FloorSurface, (Vector3[] verts, Bounds bounds, Mesh colliderMesh)>();
        S.Step(FloorTitle, FloorLabel,
            run =>
            {
                surfaces = FindFloor(run);
                material = FloorMaterial(run, choice);
                foreach (FloorSurface s in surfaces)
                {
                    MeshData md = MeshData.Read(s.mesh);
                    run.Require(md != null, $"{s.what}: its mesh '{s.mesh.name}' can't be read, so its UVs can't be laid in metres.");
                    before[s] = (md.v, s.renderer.bounds, s.collider != null ? s.collider.sharedMesh : null);
                }
                Vector2 o = OffsetFor(choice);
                run.Info(choice == Oak
                    ? $"Laying {Oak}: boards run north-south (the texture's boards run along v, v = z); u = x + {S.F(o.x, "0.00")} centres a board on the door's axis."
                    : $"Laying {Concrete}: 3 m slabs; u = x + {S.F(o.x, "0.0")} puts the saw cuts at x -4.5, -1.5, 1.5, 4.5 (none on the door's centre line), v = z at z 0, 3, ... 18.");
            },
            run =>
            {
                Vector2 o = OffsetFor(choice);
                foreach (FloorSurface s in surfaces)
                {
                    Mesh mesh = WorldUVCopy(s.filter, o, s.mesh.name + " - world UV in metres (" + (choice == Oak ? "oak" : "concrete") + ")");
                    S.Reshape(run, s.filter, mesh, $"{s.what}: UVs in metres, so {choice}'s {S.F(choice == Oak ? OakTile : ConcreteTile, "0.0")} m tile is to scale (they were 0..1 across the room)");
                    S.Repaint(run, s.renderer, Enumerable.Repeat(material, Mathf.Max(1, s.materials.Length)).ToArray(), $"{s.what}: {choice} (his decision 4: the floor that looks and feels best)");
                }
                Physics.SyncTransforms();
                run.Line("Checked after laying it:");
                foreach (FloorSurface s in surfaces)
                {
                    run.Check(s.renderer.sharedMaterials.All(m => m == material), $"{s.what} wears {choice}");
                    run.Check(UVsAreWorldMetres(s.filter, o, out string how), $"{s.what}: UVs are {how} (offset {S.F(o.x, "0.00")}, {S.F(o.y, "0.00")})");
                    MeshData md = MeshData.Read(s.filter.sharedMesh);
                    Vector3[] was = before[s].verts;
                    float moved = md != null && md.v.Length == was.Length ? was.Select((v, i) => (v - md.v[i]).magnitude).DefaultIfEmpty(0f).Max() : float.MaxValue;
                    run.Check(moved < 1e-5f, $"{s.what}: the same {was.Length} vertices where they were (largest change {S.F(moved, "0.000000")})");
                    run.Check(Approximately(s.renderer.bounds, before[s].bounds, .001f), $"{s.what}: covers {S.BoundsText(s.renderer.bounds)}, as before");
                    if (s.collider != null)
                        run.Check(s.collider.sharedMesh == before[s].colliderMesh, $"{s.what}: its MeshCollider keeps its own mesh ('{(s.collider.sharedMesh != null ? s.collider.sharedMesh.name : "none")}'): no collider changes");
                }
            },
            FloorShots,
            // No collider changes, so the routes can't change: no re-bake. The guards still run before and after.
            guards: true, rebake: false, roomPhotos: true);
    }

    // ================================================================== ROOM 1: what it finds

    sealed class Hidden
    {
        public string what, why;
        public GameObject switchOff;                                // switched off (nothing finds it by name)
        public List<Renderer> renderers = new List<Renderer>();     // or hidden: renderers (and colliders) off, the object stays
        public List<Collider> colliders = new List<Collider>();
        public Bounds bounds;
        public bool coversFloor;                                    // a rug: anything standing on it would float
    }

    sealed class Target
    {
        public string what;
        public Transform table;
        public Vector3 over;          // over the table, where the brief wants it
        public Vector3 at;            // where it hangs (shifted off a beam if one was in the way)
        public Vector3 axis;          // the table's long axis, along which it may shift
        public float shift;
    }

    sealed class PlantPlan { public PlantSpec spec; public Vector3 at; public Vector3 moved; public Bounds crown; }

    sealed class RoomPlan
    {
        public CafeViewMode view;
        public CafeDaylight daylight;
        public Dictionary<string, GameObject> kit = new Dictionary<string, GameObject>();
        public Material enamel;
        // the ceiling
        public MeshFilter deck;
        public MeshData deckData;
        public float deckY;
        public Dictionary<float, (float west, float east)> wallFaces = new Dictionary<float, (float, float)>();
        // the pendants
        public Renderer[] counterPendants;
        public Light[] counterLights;
        public int shadeSlot;
        public Mesh pendantMesh;
        public MeshData pendantData;
        public float pendantTop;
        public Renderer[] fixtures = Array.Empty<Renderer>();       // PendantFixtures (the old four's bodies)
        public GameObject[] oldLights = Array.Empty<GameObject>();  // their lights
        public List<Target> targets = new List<Target>();
        // the plants, the east wall, the sign
        public List<PlantPlan> plants = new List<PlantPlan>();
        public MeshFilter eastPlaster;
        public MeshData eastPlasterData;
        public Transform benchLamp, closeUp;
        public float closeUpFov = 40f;
        // what goes off
        public List<Hidden> off = new List<Hidden>();
        public List<(Vector3 p, float r, string what)> keepClear = new List<(Vector3, float, string)>();
        public List<(Transform t, Vector3 p, Quaternion r, string what)> anchors = new List<(Transform, Vector3, Quaternion, string)>();
        public List<string> notes = new List<string>();
    }

    static RoomPlan SurveyRoom(S.Run run)
    {
        var plan = new RoomPlan();

        // ---- the steps before this one ----
        run.Require(S.Done(run, Bar1), $"'{Bar1}' hasn't run in this scene: the front counter comes first.");
        run.Require(S.RepairBarDone(run), "The repair bar move hasn't run (no CafeRepairBar on the café's root): the bench must be at the counter first.");
        run.Require(S.Done(run, Bar2), $"'{Bar2}' hasn't run in this scene: the back bar comes first.");
        run.Require(S.Done(run, Seats1), $"'{Seats1}' hasn't run in this scene: the pendants hang over its tables.");
        run.Info(S.Done(run, FloorTitle)
            ? "Floor 1 has laid the floor."
            : "Floor 1 hasn't run yet: the floor can be laid after this step, but then this step can only be undone after Floor 1's (the newest first).");

        // ---- the scene's lists ----
        CafeViewMode[] views = S.InScene<CafeViewMode>(run);
        run.Require(views.Length == 1, $"Expected one CafeViewMode (on the Player), found {views.Length}.");
        plan.view = views[0];
        CafeDaylight[] days = S.InScene<CafeDaylight>(run);
        run.Require(days.Length == 1, $"Expected one CafeDaylight, found {days.Length}.");
        plan.daylight = days[0];
        run.Info($"CafeViewMode.overheadFixtures holds {plan.view.overheadFixtures.Length}; CafeDaylight.warmLights {plan.daylight.warmLights.Length}.");

        // ---- the kit (Kit 1), each piece as its manifest says ----
        foreach (string piece in RoomPieces)
        {
            string path = S.KitModels + "/" + piece + ".fbx";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            run.Require(asset != null, $"{path} isn't imported: run Fixit Fidget > Cafe redesign > Kit 1 first.");
            plan.kit[piece] = asset;
            Bounds b = AssetBounds(asset);
            (Vector3 size, float bottom) built = Built[piece];
            run.Check((b.size - built.size).magnitude < .012f && Mathf.Abs(b.min.y - built.bottom) < .006f,
                      $"{piece} imported as built: {S.V(b.size)} (manifest {S.V(built.size)}), bottom {S.F(b.min.y)} (manifest {S.F(built.bottom)})");
            int colliders = asset.GetComponentsInChildren<Collider>(true).Length;
            if (colliders > 0) run.Info($"{piece} was imported with {colliders} collider(s) (the manifests say none): switched off on every one this step places; the plants get a capsule on the pot.");
        }
        plan.enamel = AssetDatabase.LoadAssetAtPath<Material>(S.KitMaterials + "/CF_Enamel.mat");
        run.Require(plan.enamel != null, $"{S.KitMaterials}/CF_Enamel.mat isn't there: run Kit 1 first (the bar manifest defines it).");
        run.Info($"CF_Enamel (Kit 1's): #{ColorUtility.ToHtmlStringRGB(BaseColour(plan.enamel))}, smoothness " +
                 $"{(plan.enamel.HasProperty("_Smoothness") ? S.F(plan.enamel.GetFloat("_Smoothness"), "0.00") : "none")} (the brief: #F2EFE8, 0.60).");

        // ---- the ceiling deck: one-sided, facing down, x -7.4..7.4, z 0..18 at 3.20, world UVs ----
        Transform deck = S.Need(run, DeckPath);
        plan.deck = deck.GetComponent<MeshFilter>();
        run.Require(plan.deck != null && plan.deck.sharedMesh != null && deck.GetComponent<MeshRenderer>() != null, $"{DeckPath} has no mesh or renderer.");
        plan.deckData = MeshData.Read(plan.deck.sharedMesh);
        run.Require(plan.deckData != null, $"The ceiling deck's mesh '{plan.deck.sharedMesh.name}' can't be read: its holes can't be cut.");
        Vector3[] dw = plan.deckData.v.Select(v => deck.TransformPoint(v)).ToArray();
        float yMin = dw.Min(p => p.y), yMax = dw.Max(p => p.y);
        plan.deckY = (yMin + yMax) * .5f;
        float dx0 = dw.Min(p => p.x), dx1 = dw.Max(p => p.x), dz0 = dw.Min(p => p.z), dz1 = dw.Max(p => p.z);
        run.Require(yMax - yMin < .005f && Mathf.Abs(plan.deckY - DeckY) < .01f, $"The ceiling deck isn't flat at {S.F(DeckY, "0.00")} m (y {S.F(yMin)}..{S.F(yMax)}).");
        run.Require(Mathf.Abs(dx0 - Room.xMin) < .02f && Mathf.Abs(dx1 - Room.xMax) < .02f && Mathf.Abs(dz0 - Room.yMin) < .02f && Mathf.Abs(dz1 - Room.yMax) < .02f,
                    $"The ceiling deck covers x {S.F(dx0)}..{S.F(dx1)}, z {S.F(dz0)}..{S.F(dz1)}, not the room (x -7.40..7.40, z 0..18).");
        float area = Area(plan.deckData, deck.localToWorldMatrix, out int up, out int down);
        run.Require(Mathf.Abs(area - (dx1 - dx0) * (dz1 - dz0)) < .005f * area, $"The ceiling deck's triangles cover {S.F(area, "0.00")} m², not its whole outline ({S.F((dx1 - dx0) * (dz1 - dz0), "0.00")}): it has holes or overlaps already.");
        run.Require(up == 0 && down > 0, $"The ceiling deck faces {(up > 0 && down > 0 ? "both ways" : "up")}, not down only ({down} down, {up} up).");
        run.Require(UVsAreWorldMetres(plan.deck, Vector2.zero, out string deckUV), $"The ceiling deck's UVs aren't world metres ({deckUV}).");
        run.Info($"The ceiling deck: '{plan.deck.sharedMesh.name}', {plan.deckData.v.Length} vertices, x {S.F(dx0)}..{S.F(dx1)}, z {S.F(dz0)}..{S.F(dz1)} at {S.F(plan.deckY)}, " +
                 $"one-sided facing down, {deckUV}, wearing {string.Join(", ", deck.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m != null ? m.name : "none"))}.");

        // ---- the side walls at the beams' ends (rays at y 3.0, where the hangers' flanges are) ----
        foreach (float z in BeamZ)
        {
            float west = WallFace(run, new Vector3(-6.5f, 3.0f, z), Vector3.left), east = WallFace(run, new Vector3(6.5f, 3.0f, z), Vector3.right);
            plan.wallFaces[z] = (west, east);
            run.Check(!float.IsNaN(west) && !float.IsNaN(east) && west <= -BeamHalfLength - .002f + .012f && west >= -7.46f && east >= BeamHalfLength - .01f && east <= 7.46f,
                      $"the beam at z {S.F(z, "0")} lands on solid walls: west face x {S.F(west)}, east face x {S.F(east)} (its hangers reach x +-7.400)");
        }

        // ---- nothing above the wells (a roof or an upper floor would leave them dark) ----
        foreach (float z in WellZ)
        {
            List<string> over = Above(run, new Vector3(0f, plan.deckY, z));
            run.Check(over.Count == 0, over.Count == 0 ? $"nothing closes the well at z {S.F(z, "0.0")} from above (open sky over its glass)"
                                                      : $"the well at z {S.F(z, "0.0")} would be dark: {string.Join("; ", over)}");
        }

        // ---- the pendants ----
        Transform counterGroup = S.Need(run, CounterPendantsPath);
        CafeCounterPendants record = counterGroup.GetComponent<CafeCounterPendants>();
        run.Require(record != null && record.shades != null && record.shades.Length == 4 && record.shades.All(r => r != null) && record.lights != null && record.lights.Length == 4,
                    "The counter's four pendants (session 6) aren't as built: no CafeCounterPendants record with four shades and four lights.");
        plan.counterPendants = record.shades;
        plan.counterLights = record.lights;
        MeshFilter pf = plan.counterPendants[0].GetComponent<MeshFilter>();
        run.Require(pf != null && pf.sharedMesh != null, "Counter pendant 1 has no mesh.");
        plan.pendantMesh = pf.sharedMesh;
        plan.pendantData = MeshData.Read(plan.pendantMesh);
        run.Require(plan.pendantData != null, $"The pendant's mesh '{plan.pendantMesh.name}' can't be read.");
        foreach (Renderer r in plan.counterPendants)
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            run.Require(f != null && f.sharedMesh == plan.pendantMesh && r.sharedMaterials.SequenceEqual(plan.counterPendants[0].sharedMaterials),
                        $"{S.PathOf(r.transform)} isn't the same pendant as Counter pendant 1 (mesh or materials differ).");
            run.Require(Quaternion.Angle(r.transform.rotation, Quaternion.identity) < .5f && (r.transform.lossyScale - Vector3.one).magnitude < .001f,
                        $"{S.PathOf(r.transform)} is turned or scaled: the cut-out pendant hangs unturned at scale 1.");
        }
        plan.pendantTop = plan.counterPendants[0].transform.position.y;
        plan.shadeSlot = ShadeSlot(run, plan);
        run.Info($"The pendant: '{plan.pendantMesh.name}', from {S.F(plan.pendantTop)} to {S.F(plan.pendantTop + plan.pendantData.v.Min(v => v.y))} m; " +
                 $"slots: {string.Join(", ", plan.counterPendants[0].sharedMaterials.Select((m, i) => $"{i} {(m != null ? m.name : "none")} #{(m != null ? ColorUtility.ToHtmlStringRGB(BaseColour(m)) : "")}"))}; " +
                 $"the shade is slot {plan.shadeSlot}.");
        for (int i = 0; i < plan.counterLights.Length; i++)
        {
            Light l = plan.counterLights[i];
            run.Require(l != null && l.type == LightType.Point && Same(l.color, PendantColour) && Mathf.Abs(l.intensity - PendantIntensity) < .01f && Mathf.Abs(l.range - PendantRange) < .01f,
                        $"Counter pendant light {i + 1} isn't the counter's light (#FFC98F, 1.2, range 4.5): " + (l != null ? $"#{ColorUtility.ToHtmlStringRGB(l.color)}, {S.F(l.intensity, "0.00")}, {S.F(l.range, "0.0")}" : "missing"));
            run.Require(l.transform.IsChildOf(plan.counterPendants[i].transform) || l.transform.parent == plan.counterPendants[0].transform.parent,
                        $"Counter pendant light {i + 1} doesn't hang in its pendant.");
        }
        run.Require(plan.counterLights[0].transform.IsChildOf(plan.counterPendants[0].transform), "Counter pendant 1's light isn't its child: copying the pendant wouldn't copy its light.");

        // Today's table pendants: their bodies (PendantFixtures in the joinery) and their lights.
        Transform joinery = S.Need(run, JoineryPath);
        Transform fixtures = joinery.Find("PendantFixtures");
        run.Require(fixtures != null, $"{JoineryPath}/PendantFixtures (the tables' pendants) isn't in the joinery model.");
        plan.fixtures = fixtures.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        List<Vector2> clusters = PendantClusters(plan.fixtures, out bool readable);
        if (readable)
        {
            run.Check(clusters.Count > 0 && clusters.All(c => OldTablePendants.Any(o => (o - c).magnitude < .5f)),
                      $"PendantFixtures holds the pendants over the old round tables only: {string.Join(", ", clusters.Select(c => $"({S.F(c.x, "0.00")}, {S.F(c.y, "0.00")})"))}");
        }
        else
        {
            Bounds fb = Union(plan.fixtures.Select(r => r.bounds));
            run.Check(fb.min.x > -3.6f && fb.max.x < 3.6f && fb.min.z > 2.9f && fb.max.z < 9.6f && fb.min.y > 2.4f,
                      $"PendantFixtures' meshes can't be read; its bounds {S.BoundsText(fb)} hold only the four old table pendants' places");
        }
        Transform v3 = S.Need(run, V3LightsPath);
        plan.oldLights = v3.Cast<Transform>().Where(t => t.name == V3TablePendant && t.gameObject.activeSelf).Select(t => t.gameObject).ToArray();
        run.Check(plan.oldLights.Length == 4 && plan.oldLights.All(g => g.GetComponent<Light>() != null && OldTablePendants.Any(o => (o - Flat2(g.transform.position)).magnitude < .1f)),
                  $"the old table pendants' four lights ('{V3TablePendant}') are over the old tables: {string.Join(", ", plan.oldLights.Select(g => S.V(g.transform.position)))}");
        float oldTop = plan.fixtures.Length > 0 ? Union(plan.fixtures.Select(r => r.bounds)).max.y : float.NaN;
        run.Check(float.IsNaN(oldTop) || Mathf.Abs(oldTop - plan.pendantTop) < .03f,
                  $"the counter's pendant hangs at the old table pendants' drop: top {S.F(plan.pendantTop)} m against theirs {S.F(oldTop)} (the brief: the same drop)");

        // The counter's four stay where they are: their cords and lamps clear of the z 14 beam.
        foreach (Renderer r in plan.counterPendants)
        {
            Vector3 at = r.transform.position;
            (float cord, float lamp, string near) = Clearance(plan, at);
            run.Check(cord >= CordClear - .0005f && lamp >= MeshClear - .0005f,
                      $"{r.name} at x {S.F(at.x, "0.00")}, z {S.F(at.z, "0.00")} stays: its cord {S.F(cord, "0.000")} m from {near}'s face (needs {S.F(CordClear, "0.00")}), the lamp {S.F(lamp, "0.000")} m from it (needs {S.F(MeshClear, "0.00")})");
        }

        // The seven over the new tables (Seats 1's pieces, by their names' prefix), each clear of the beams.
        PlanTargets(run, plan);

        // ---- what goes off (first: a plant's spot may be free only once an old pot plant's collider is off) ----
        PlanOffs(run, plan);

        // ---- the plants: where they stand and what they must clear ----
        BuildKeepClear(run, plan);
        foreach (PlantSpec spec in Plants) plan.plants.Add(PlanPlant(run, plan, spec));

        // ---- the east wall: the board and the ledge hang on plaster, never on glass ----
        Transform plaster = S.Need(run, EastPlasterPath);
        plan.eastPlaster = plaster.GetComponent<MeshFilter>();
        run.Require(plan.eastPlaster != null && plan.eastPlaster.sharedMesh != null, $"{EastPlasterPath} has no mesh.");
        plan.eastPlasterData = MeshData.Read(plan.eastPlaster.sharedMesh);
        run.Require(plan.eastPlasterData != null, $"The east wall's plaster mesh '{plan.eastPlaster.sharedMesh.name}' can't be read: the piers can't be found.");
        CheckOnPier(run, plan, "CK_NoticeBoard", NoticeBoardAt, "the notice board on the small-print pier");
        CheckOnPier(run, plan, "CK_PhotoLedge", PhotoLedgeAt, "the photo ledge on the local-art pier");
        Bounds board = Predicted(plan.kit["CK_NoticeBoard"], NoticeBoardAt, -90f), ledge = Predicted(plan.kit["CK_PhotoLedge"], PhotoLedgeAt, -90f);
        run.Check(board.max.x <= EastFace + .002f && ledge.max.x <= EastFace + .002f && board.min.x >= EastFace - .03f && ledge.min.x >= EastFace - .13f,
                  $"both stand out from the wall's face (x {S.F(EastFace, "0.00")}) only by their depth: the board x {S.F(board.min.x)}..{S.F(board.max.x)}, the ledge x {S.F(ledge.min.x)}..{S.F(ledge.max.x)} (their backs on the wall)");
        Transform counter = ActiveNamed(run, "CK_WindowCounter");
        if (counter != null)
        {
            Bounds wc = S.RendererBounds(counter);
            run.Info($"the window counter's top at {S.F(wc.max.y, "0.00")} m runs z {S.F(wc.min.z, "0.00")}..{S.F(wc.max.z, "0.00")}: the photo ledge's bottom is {S.F(ledge.min.y - wc.max.y, "0.00")} m above it");
            run.Check(!Shrunk(wc, .002f).Intersects(ledge), "the photo ledge clears the window counter");
        }
        foreach (WaitingSpot spot in S.InScene<WaitingSpot>(run).Where(w => !(w is TableSeat) && w.gameObject.activeInHierarchy))
        {
            float d = FlatDistance(spot.StandPoint.position, board);
            if (d < 2.5f) run.Check(d >= .8f, $"the notice board is {S.F(d, "0.00")} m from {spot.name} (needs 0.80)");
        }

        // ---- the repair bar: the bench lamp (Bar 1) and the close-up (the repair bar move) ----
        plan.benchLamp = ActiveNamed(run, "CK_BenchLamp");
        if (plan.benchLamp == null)
        {
            Transform clamp = S.Find(run, "23 - real cafe pass/Bench lamp and tool stand/Clamp lamp");
            if (clamp != null && clamp.gameObject.activeInHierarchy) plan.benchLamp = clamp;
            run.Info(plan.benchLamp != null ? "No CK_BenchLamp (Bar 1 places it): the sign is checked against the old clamp lamp, which is still on."
                                            : "No bench lamp is on (neither Bar 1's CK_BenchLamp nor the old clamp lamp): the sign's clearance from it is left to the stool photo.");
        }
        plan.closeUp = S.Find(run, "03 - repair work area/Workbench/InspectRig/CM_InspectCam");
        CinemachineCamera cm = plan.closeUp != null ? plan.closeUp.GetComponent<CinemachineCamera>() : null;
        if (cm != null) plan.closeUpFov = cm.Lens.FieldOfView;
        if (plan.closeUp == null) run.Info("No close-up camera found (03 - repair work area/Workbench/InspectRig/CM_InspectCam): Bench 3 will judge the frame.");

        // ---- the gameplay objects, to check after that none moved ----
        plan.anchors = GameplayAnchors(run);
        run.Info($"{plan.anchors.Count} gameplay anchors noted (stations and their stand points, slots, the queue, waiting spots and seats, spawns): none may move.");
        return plan;
    }

    // The renderers' box of a model asset in its own frame (the prefab's root at the origin, unturned).
    static Bounds AssetBounds(GameObject asset) => Predicted(asset, Vector3.zero, 0f);

    // Where a model asset's renderers will be once placed at `at`, turned `yaw` about up, at scale 1: each mesh's own box
    // carried to the world, as Unity's renderer.bounds reports it, all of them together.
    static Bounds Predicted(GameObject asset, Vector3 at, float yaw)
    {
        Matrix4x4 place = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one) * asset.transform.worldToLocalMatrix;
        Bounds? all = null;
        foreach (MeshFilter f in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Matrix4x4 m = place * f.transform.localToWorldMatrix;
            Bounds b = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                if (all == null) all = new Bounds(c, Vector3.zero);
                else { Bounds u = all.Value; u.Encapsulate(c); all = u; }
            }
        }
        return all ?? new Bounds(at, Vector3.zero);
    }

    // The first solid along a horizontal ray, as the x of its face (NaN if none within 2 m). Triggers and Ace don't count.
    static float WallFace(S.Run run, Vector3 from, Vector3 dir)
    {
        foreach (RaycastHit h in Physics.RaycastAll(from, dir, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (run.Player != null && h.collider.transform.IsChildOf(run.Player)) continue;
            return h.point.x;
        }
        return float.NaN;
    }

    // What lies over a well's opening, above the deck: any drawn mesh crossed by five rays straight up (the middle and four
    // points 0.5 m out). Meshes that can't be read are judged by their boxes.
    static List<string> Above(S.Run run, Vector3 centre)
    {
        var found = new List<string>();
        var column = new Bounds(new Vector3(centre.x, centre.y + 30f, centre.z), new Vector3(1.24f, 59.98f, 1.24f));
        Vector3[] starts = { Vector3.zero, new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, -.5f), new Vector3(-.5f, 0, -.5f) };
        Transform deck = run.Cafe.Find(DeckPath);
        foreach (MeshRenderer r in S.InScene<MeshRenderer>(run, false))
        {
            if (!r.enabled || r.forceRenderingOff || !r.bounds.Intersects(column)) continue;
            if (deck != null && r.transform == deck) continue;                    // the deck itself: the hole is this step's
            MeshFilter f = r.GetComponent<MeshFilter>();
            MeshData md = f != null ? MeshData.Read(f.sharedMesh) : null;
            if (md == null) { found.Add($"{S.PathOf(r.transform)} (its mesh can't be read; its box reaches over the well: {S.BoundsText(r.bounds)})"); continue; }
            Matrix4x4 m = r.transform.localToWorldMatrix;
            foreach (Vector3 s in starts)
            {
                float t = md.Ray(m, centre + s + Vector3.up * .01f, Vector3.up, 60f);
                if (!float.IsPositiveInfinity(t)) { found.Add($"{S.PathOf(r.transform)} at y {S.F(centre.y + .01f + t, "0.00")}"); break; }
            }
        }
        return found;
    }

    // The shade's slot: the one green material (the brief: "today green shades"), and the widest part of the lamp. Both must
    // agree, or nothing is painted.
    static int ShadeSlot(S.Run run, RoomPlan plan)
    {
        Material[] mats = plan.counterPendants[0].sharedMaterials;
        var green = new List<(int i, float g)>();
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            Color c = BaseColour(mats[i]);
            green.Add((i, c.g - Mathf.Max(c.r, c.b)));
        }
        green = green.OrderByDescending(x => x.g).ToList();
        run.Require(green.Count > 0 && green[0].g >= .04f && (green.Count == 1 || green[0].g - green[1].g >= .04f),
                    $"The pendant has no one green material to recolour: {string.Join(", ", green.Select(x => $"slot {x.i} {mats[x.i].name} green by {S.F(x.g, "0.00")}"))}.");
        int slot = green[0].i;
        Mesh mesh = plan.pendantMesh;
        run.Require(mesh.subMeshCount == mats.Length, $"The pendant's mesh has {mesh.subMeshCount} parts for {mats.Length} materials.");
        // A shade is the lamp's widest part, or nearly (a diffuser may peep out under it): never a stem or a cap.
        float Width(int s) { Bounds b = SubMeshBounds(plan.pendantData, s); return Mathf.Max(b.size.x, b.size.z); }
        float widest = Enumerable.Range(0, mesh.subMeshCount).Max(Width);
        run.Require(Width(slot) >= .75f * widest, $"The green slot {slot} ({mats[slot].name}, {S.F(Width(slot), "0.00")} m across) is much narrower than the lamp's widest part ({S.F(widest, "0.00")} m): it may not be the shade.");
        run.Info($"The pendant's parts across: {string.Join(", ", Enumerable.Range(0, mesh.subMeshCount).Select(s => $"{(mats[s] != null ? mats[s].name : "none")} {S.F(Width(s), "0.00")} m"))}.");
        return slot;
    }

    // The four old pendants inside PendantFixtures, found as clusters of their triangles (0.9 m apart at least).
    static List<Vector2> PendantClusters(Renderer[] renderers, out bool readable)
    {
        var centres = new List<(Vector2 sum, int n)>();
        readable = true;
        foreach (Renderer r in renderers)
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            MeshData md = f != null ? MeshData.Read(f.sharedMesh) : null;
            if (md == null) { readable = false; continue; }
            Matrix4x4 m = r.transform.localToWorldMatrix;
            foreach (Vector3 v in md.v)
            {
                Vector2 p = Flat2(m.MultiplyPoint3x4(v));
                int k = centres.FindIndex(c => (c.sum / c.n - p).magnitude < .9f);
                if (k < 0) centres.Add((p, 1)); else centres[k] = (centres[k].sum + p, centres[k].n + 1);
            }
        }
        return centres.Select(c => c.sum / c.n).ToList();
    }

    // How far a pendant hanging at `at` (its top) stays from the beams: its cord (the pivot's vertical) from the nearest face
    // of a beam it passes beside, and its whole lamp from every beam's wood and hangers.
    static (float cord, float lamp, string near) Clearance(RoomPlan plan, Vector3 at)
    {
        float cord = float.PositiveInfinity, lamp = float.PositiveInfinity;
        string near = "no beam";
        foreach (float z in BeamZ)
        {
            float c = Mathf.Abs(at.z - z) - BeamHalfWidth;
            if (c < cord) { cord = c; near = $"the z {S.F(z, "0")} beam"; }
            foreach (Bounds box in BeamBoxes(plan.deckY, z))
                foreach (Vector3 v in plan.pendantData.v)
                    lamp = Mathf.Min(lamp, Distance(at + v, box));
        }
        return (cord, lamp, near);
    }

    // A beam as solids: its wood, and its two hangers' plates where they widen it.
    static IEnumerable<Bounds> BeamBoxes(float top, float z)
    {
        yield return new Bounds(new Vector3(0f, top - BeamDepth * .5f, z), new Vector3(BeamHalfLength * 2f, BeamDepth, BeamHalfWidth * 2f));
        foreach (float s in new[] { -1f, 1f })
            yield return new Bounds(new Vector3(s * (HangerFrom + 7.40f) * .5f, top - HangerDepth * .5f, z), new Vector3(7.40f - HangerFrom, HangerDepth, HangerHalfWidth * 2f));
    }

    static void PlanTargets(S.Run run, RoomPlan plan)
    {
        List<Transform> squares = AllActiveNamed(run, Square2), communal = AllActiveNamed(run, Communal), bistro = AllActiveNamed(run, Bistro);
        run.Require(squares.Count == 3 && communal.Count == 1 && bistro.Count == 2,
                    $"Expected Seats 1's tables: 3 {Square2}, 1 {Communal}, 2 {Bistro} (found {squares.Count}, {communal.Count}, {bistro.Count}).");
        var specs = new List<(string what, Transform t, Vector3 over, Vector3 axis)>();
        string[] bay = { "south", "middle", "north" };
        squares = squares.OrderBy(t => t.position.z).ToList();
        for (int i = 0; i < 3; i++)
        {
            Vector3 c = TableCentre(squares[i]);
            run.Check((Flat2(c) - PlanSquare2[i]).magnitude <= TableTolerance, $"{squares[i].name} at ({S.F(c.x, "0.00")}, {S.F(c.z, "0.00")}), the plan's ({S.F(PlanSquare2[i].x, "0.00")}, {S.F(PlanSquare2[i].y, "0.00")})");
            specs.Add(($"banquette 2-top, {bay[i]}", squares[i], c, Vector3.forward));
        }
        Vector3 cc = TableCentre(communal[0]);
        Vector3 along = Flat(communal[0].rotation * Vector3.forward).normalized;
        run.Check((Flat2(cc) - PlanCommunal).magnitude <= TableTolerance && Mathf.Abs(Vector3.Dot(along, Vector3.forward)) > .999f,
                  $"{communal[0].name} at ({S.F(cc.x, "0.00")}, {S.F(cc.z, "0.00")}), long along z (the plan's ({S.F(PlanCommunal.x, "0.00")}, {S.F(PlanCommunal.y, "0.00")}))");
        if (Vector3.Dot(along, Vector3.forward) < 0f) along = -along;
        specs.Add(("communal table, south", communal[0], cc - along * CommunalPendantOffset, along));
        specs.Add(("communal table, north", communal[0], cc + along * CommunalPendantOffset, along));
        bistro = bistro.OrderBy(t => t.position.z).ToList();
        for (int i = 0; i < 2; i++)
        {
            Vector3 c = TableCentre(bistro[i]);
            run.Check((Flat2(c) - PlanBistro[i]).magnitude <= TableTolerance, $"{bistro[i].name} at ({S.F(c.x, "0.00")}, {S.F(c.z, "0.00")}), the plan's ({S.F(PlanBistro[i].x, "0.00")}, {S.F(PlanBistro[i].y, "0.00")})");
            specs.Add(($"bistro table, {(i == 0 ? "south" : "north")}", bistro[i], c, Vector3.forward));
        }
        foreach (var (what, table, over, axis) in specs)
        {
            var t = new Target { what = what, table = table, over = new Vector3(over.x, plan.pendantTop, over.z), axis = axis };
            t.shift = ShiftOffBeams(plan, t.over, t.axis);
            t.at = t.over + t.axis * t.shift;
            (float c1, float l1, string n1) = Clearance(plan, t.at);
            run.Check(c1 >= CordClear - .0005f && l1 >= MeshClear - .0005f && Mathf.Abs(t.shift) <= ShiftMax + .001f,
                      $"the pendant over the {what} hangs at x {S.F(t.at.x, "0.00")}, z {S.F(t.at.z, "0.00")}: cord {S.F(c1, "0.000")} m from {n1}'s face, lamp {S.F(l1, "0.000")} m" +
                      (Mathf.Abs(t.shift) > .001f ? $" (shifted {S.F(t.shift, "+0.00;-0.00")} m along the table: a beam was in the way)" : ""));
            float toWell = WellZ.Min(z => Distance(t.at, new Bounds(new Vector3(0f, plan.deckY, z), new Vector3(TrimHalf * 2f, .1f, TrimHalf * 2f))));
            run.Check(toWell >= .3f, $"…{S.F(toWell, "0.00")} m from the nearest skylight well's trim (needs 0.30)");
            plan.targets.Add(t);
        }
    }

    // The top of a pendant hung low over a table: its rim 0.95 m over the table's top (the mesh's lowest point is its rim).
    static float TablePendantTop(RoomPlan plan) => TableTopHeight + TableRimOverTop - plan.pendantData.v.Min(v => v.y);

    // A beam in the way of a pendant hung at `over`: the shift along the table's long axis, away from the nearest beam, 5 cm
    // at a time and at most 25 cm, that clears its cord by 0.10 m and its lamp by 0.03 m; 0 when nothing is in the way. The
    // caller checks the result (25 cm may not be enough).
    static float ShiftOffBeams(RoomPlan plan, Vector3 over, Vector3 axis)
    {
        float shift = 0f;
        Vector3 at = over;
        while (true)
        {
            var (cord, lamp, _) = Clearance(plan, at);
            if (cord >= CordClear - .0005f && lamp >= MeshClear - .0005f) return shift;
            if (Mathf.Abs(shift) >= ShiftMax - .0001f) return shift;
            float nearest = BeamZ.OrderBy(z => Mathf.Abs(Vector3.Dot(at, axis) - Vector3.Dot(new Vector3(0f, 0f, z), axis))).First();
            shift += Vector3.Dot(at - new Vector3(at.x, at.y, nearest), axis) >= 0f ? ShiftStep : -ShiftStep;
            at = over + axis * shift;
        }
    }

    static Vector3 TableCentre(Transform table)
    {
        Bounds b = S.RendererBounds(table);
        return b.size.sqrMagnitude > 0f ? new Vector3(b.center.x, 0f, b.center.z) : new Vector3(table.position.x, 0f, table.position.z);
    }

    // CafeFurnishing.BuildKeepClear's places (live): seats' stand points 0.75, waiting spots 0.80, queue places 0.90,
    // stations' stand points 0.80, the customer spawn 1.0.
    static void BuildKeepClear(S.Run run, RoomPlan plan)
    {
        foreach (WaitingSpot spot in S.InScene<WaitingSpot>(run))
        {
            if (!spot.gameObject.activeInHierarchy) continue;
            plan.keepClear.Add((spot.StandPoint.position, spot is TableSeat ? .75f : .80f, spot.name + (spot is TableSeat ? $" (seat stand point, {spot.transform.parent?.name})" : " (waiting spot)")));
        }
        foreach (CounterQueue q in S.InScene<CounterQueue>(run))
            foreach (Transform slot in q.transform) plan.keepClear.Add((slot.position, .90f, "queue " + slot.name));
        foreach (StationInteractable st in S.InScene<StationInteractable>(run))
            if (st.StandPoint != null && st.gameObject.activeInHierarchy) plan.keepClear.Add((st.StandPoint.position, .80f, st.name + " stand point"));
        GameObject spawn = run.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SpawnPoint");
        if (spawn != null) plan.keepClear.Add((spawn.transform.position, 1f, "customer spawn"));
        run.Info($"Keep-clear places: {plan.keepClear.Count} ({plan.keepClear.Count(k => Mathf.Approximately(k.r, .75f))} seats).");
    }

    // A plant: where it stands (moved off the front wall or the door lane if its crown reaches them), its pot's collider
    // clear of every keep-clear place and the door-to-counter lane, its spot free of other solids.
    static PlantPlan PlanPlant(S.Run run, RoomPlan plan, PlantSpec spec)
    {
        var p = new PlantPlan { spec = spec, at = new Vector3(spec.at.x, 0f, spec.at.y) };
        p.moved = p.at;
        for (int k = 0; k < 10; k++)
        {
            p.crown = Predicted(plan.kit[spec.piece], p.moved, spec.yaw);
            Vector3 step = Vector3.zero;
            if (p.crown.min.z < .01f) step.z = .01f - p.crown.min.z + .002f;                                             // off the front wall's line
            bool inLane = p.crown.max.x > DoorLane.xMin && p.crown.min.x < DoorLane.xMax && p.crown.max.z > DoorLane.yMin && p.crown.min.z < DoorLane.yMax;
            if (inLane) step.x = p.moved.x > 0f ? DoorLane.xMax + .02f - p.crown.min.x : DoorLane.xMin - .02f - p.crown.max.x;   // out of the door lane
            if (step == Vector3.zero) break;
            p.moved += step;
        }
        Vector3 shift = p.moved - p.at;
        run.Check(shift.magnitude <= .20f, $"{spec.name}: stands at ({S.F(p.moved.x, "0.00")}, {S.F(p.moved.z, "0.00")})" +
                  (shift.magnitude > .001f ? $", moved {S.F(shift.magnitude, "0.00")} m from the plan's ({S.F(p.at.x, "0.00")}, {S.F(p.at.z, "0.00")}) so its crown clears the front wall and the door lane" : "") +
                  $"; crown {S.BoundsText(p.crown)}");
        Bounds pot = PotBox(spec, p.moved);
        foreach (var k in plan.keepClear)
        {
            float d = FlatDistance(k.p, pot);
            if (d < k.r + 1.5f) run.Check(d >= k.r, $"{spec.name}'s pot is {S.F(d, "0.00")} m from {k.what} (needs {S.F(k.r, "0.00")})");
        }
        run.Check(!Overlaps2(pot, DoorToCounter), $"{spec.name}'s pot stays out of the door-to-counter lane (x -1..1, z -1.2..12.5)");
        run.Check(!Overlaps2(p.crown, DoorLane), $"{spec.name}'s crown stays out of the door lane (x -1..1, z 0..2)");
        // Its spot is free: nothing solid where the pot goes, but the floor and what this step hides or switches off.
        var hiddenColliders = new HashSet<Collider>(plan.off.SelectMany(h => h.switchOff != null ? h.switchOff.GetComponentsInChildren<Collider>(true) : h.colliders.ToArray()));
        Collider[] there = Physics.OverlapCapsule(p.moved + Vector3.up * (spec.radius + .03f), p.moved + Vector3.up * Mathf.Max(spec.radius + .03f, CapsuleHeight(spec) - spec.radius),
                                                  spec.radius, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c != null && c.gameObject.name != FloorName && !hiddenColliders.Contains(c) && !IsSyntyPlant(run, c.transform) && !(run.Player != null && c.transform.IsChildOf(run.Player))).ToArray();
        run.Check(there.Length == 0, there.Length == 0 ? $"{spec.name}'s spot is free" : $"{spec.name}'s spot isn't free: {string.Join(", ", there.Select(c => S.PathOf(c.transform)))}");
        return p;
    }

    static bool IsSyntyPlant(S.Run run, Transform t)
    {
        Transform g = S.Find(run, SyntyPlantsPath);
        return g != null && t.IsChildOf(g);
    }

    static Bounds PotBox(PlantSpec spec, Vector3 at) => new Bounds(at + Vector3.up * spec.height * .5f, new Vector3(spec.radius * 2f, spec.height, spec.radius * 2f));

    // The props manifest's capsules are shorter than they are wide (the olive: r 0.22, h 0.37). Unity draws such a capsule
    // as a ball centred at half the height, its bottom 3.5 cm under the floor; so the capsule is never less than 2r tall,
    // and its centre is set from that height, which keeps its bottom on the floor.
    static float CapsuleHeight(PlantSpec spec) => Mathf.Max(spec.height, 2f * spec.radius);

    // On a pier: rays from the room toward the wall at the piece's four corners (2 cm in) and middle meet the plaster at
    // the wall's face.
    static void CheckOnPier(S.Run run, RoomPlan plan, string piece, Vector3 at, string what)
    {
        Vector3 size = Built[piece].size;
        float halfAlong = size.x * .5f - .02f;
        var points = new List<Vector3> { new Vector3(0f, size.y * .5f, 0f) };
        foreach (float a in new[] { -halfAlong, halfAlong }) foreach (float y in new[] { .02f, size.y - .02f }) points.Add(new Vector3(0f, y, a));
        Matrix4x4 m = plan.eastPlaster.transform.localToWorldMatrix;
        var misses = new List<string>();
        foreach (Vector3 o in points)
        {
            Vector3 from = new Vector3(at.x - .5f, at.y + o.y, at.z + o.z);
            float t = plan.eastPlasterData.Ray(m, from, Vector3.right, 1f);
            float x = from.x + t;
            if (float.IsPositiveInfinity(t) || Mathf.Abs(x - at.x) > .01f) misses.Add($"z {S.F(from.z, "0.00")}, y {S.F(from.y, "0.00")} ({(float.IsPositiveInfinity(t) ? "no plaster" : "plaster at x " + S.F(x))})");
        }
        run.Check(misses.Count == 0, misses.Count == 0 ? $"{what}: solid plaster behind all of it at x {S.F(at.x, "0.00")} (z {S.F(at.z - size.x * .5f, "0.00")}..{S.F(at.z + size.x * .5f, "0.00")}, y {S.F(at.y, "0.00")}..{S.F(at.y + size.y, "0.00")})"
                                                       : $"{what} wouldn't hang on plaster at: {string.Join("; ", misses)}");
    }

    // ---- what goes off ----

    static void PlanOffs(S.Run run, RoomPlan plan)
    {
        foreach (OffSpec o in Offs)
        {
            Transform t = S.Find(run, o.path);
            if (t == null) { run.Info($"Not in the scene: {o.path}."); continue; }
            if (!t.gameObject.activeInHierarchy || !t.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled))
            { run.Info($"Already off or hidden{(o.owner != null ? " (" + o.owner + ")" : "")}: {o.path}."); continue; }
            var h = new Hidden { what = o.path, why = o.why + (o.owner != null ? $" ({o.owner} owns it and left it on)" : ""), bounds = S.RendererBounds(t) };
            if (o.hideOnly) Hide(h, t); else h.switchOff = t.gameObject;
            h.coversFloor = o.path.Contains("kilim") || o.path.Contains("rug");
            plan.off.Add(h);
        }
        // The runner: the joinery model's WovenRunner (CafeInteriorPass finds it by name), and the old primitive runner if any
        // of it still draws (CafeStreetUpgrade finds "Central woven runner" by name).
        foreach (FloorCovering c in FloorCoverings(run).Where(c => c.what != "kilim" && c.what != "round rug"))
        {
            var h = new Hidden { what = c.path, why = c.why, coversFloor = true };
            h.renderers.AddRange(c.renderers.Where(r => r.enabled));
            if (h.renderers.Count == 0) continue;
            h.bounds = Union(h.renderers.Select(r => r.bounds));
            plan.off.Add(h);
        }
        // The old pot plants (POLYGON City), renderers and colliders hidden (Night 0's Bins 1 moves one by name and place).
        Transform synty = S.Find(run, SyntyPlantsPath);
        if (synty != null)
            foreach (Transform t in synty.Cast<Transform>().Where(x => x.gameObject.activeInHierarchy && x.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled)))
            {
                Vector3 p = t.position;
                string where = Plants.Where(s => (s.at - Flat2(p)).magnitude < .6f).Select(s => $"where the {s.name} goes").FirstOrDefault()
                               ?? "a second plant style beside the five new plants (one of everything, research §6.9; §6.10)";
                var h = new Hidden { what = $"{SyntyPlantsPath}/{t.name} at ({S.F(p.x, "0.00")}, {S.F(p.z, "0.00")})", why = "an old POLYGON pot plant: " + where, bounds = S.RendererBounds(t) };
                Hide(h, t);
                plan.off.Add(h);
            }
        PlanLedgeProps(run, plan);
    }

    // The window ledges' small things, left by the old lounge and window bar (CafeFurnishing made them; nothing finds them by
    // name): the sill plants and the ledge plant, and the books and mugs. Switched off, each with its reason.
    static void PlanLedgeProps(S.Run run, RoomPlan plan)
    {
        foreach (string path in LedgeProps)
            foreach (Transform t in Under(run, path))
            {
                bool plant = t.name.IndexOf("plant", StringComparison.OrdinalIgnoreCase) >= 0;
                plan.off.Add(new Hidden
                {
                    what = $"{S.PathOf(t)} at ({S.F(t.position.x, "0.00")}, {S.F(t.position.z, "0.00")})", switchOff = t.gameObject, bounds = S.RendererBounds(t),
                    why = plant ? "a small pot plant on the window ledge: the café's plants are the five big ones and the two pothos (research §6.10; one of everything, §6.9 rule 2)"
                                : "a prop left on the window ledge where nobody sits now: nothing that belongs to no station (research §6.9 rule 3)"
                });
            }
    }

    static IEnumerable<Transform> Under(S.Run run, string path)
    {
        int cut = path.LastIndexOf('/');
        Transform parent = cut > 0 ? S.Find(run, path.Substring(0, cut)) : run.Cafe;
        string name = path.Substring(cut + 1);
        return parent == null ? Enumerable.Empty<Transform>() : parent.Cast<Transform>().Where(t => t.name == name && t.gameObject.activeInHierarchy && t.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled));
    }

    static void Hide(Hidden h, Transform t)
    {
        h.renderers.AddRange(t.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled));
        h.colliders.AddRange(t.GetComponentsInChildren<Collider>(true).Where(c => c.enabled));
    }

    sealed class FloorCovering { public string what, path, why; public List<Renderer> renderers = new List<Renderer>(); }

    // The floor's coverings (research §5 item 5: four patterned floor pieces at once; §6.9: no rugs inside): the kilim, the
    // round rug, the woven runner and the cup doormat. The doormat lies in the door's recess, inside the joinery or the
    // entrance model: it is found by its shape (flat on the floor in the doorway, smaller than it, not wood or stone).
    static List<FloorCovering> FloorCoverings(S.Run run)
    {
        var found = new List<FloorCovering>();
        void Add(string what, string path, string why, IEnumerable<Renderer> rs)
        {
            var c = new FloorCovering { what = what, path = path, why = why };
            c.renderers.AddRange(rs.Where(r => r != null));
            if (c.renderers.Count > 0) found.Add(c);
        }
        Transform kilim = S.Find(run, Offs[0].path), round = S.Find(run, Offs[1].path);
        if (kilim != null && kilim.gameObject.activeInHierarchy) Add("kilim", Offs[0].path, Offs[0].why, kilim.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled));
        if (round != null && round.gameObject.activeInHierarchy) Add("round rug", Offs[1].path, Offs[1].why, round.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled));
        Transform joinery = S.Find(run, JoineryPath);
        Transform runner = joinery != null ? joinery.Find("WovenRunner") : null;
        if (runner != null && runner.gameObject.activeInHierarchy)
            Add("runner", JoineryPath + "/WovenRunner", "the flowered runner down the aisle (research §5 item 5); hidden, not switched off: CafeInteriorPass finds it by name",
                runner.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled));
        Transform old = S.Find(run, PrimitiveRunnerPath);
        if (old != null)
            Add("old runner", PrimitiveRunnerPath, "the first runner's pieces, still drawing (hidden: CafeStreetUpgrade finds them by name)",
                old.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && (r.name == "Central woven runner" || r.name == "Runner border" || r.name == "Woven diamond")));
        var mats = new[] { "oak", "wood", "grain", "floor", "stone", "pavement", "sidewalk", "asphalt", "paving" };
        Transform redesign = run.Cafe.Find(S.RedesignGroup);
        Add("doormat", "the cup doormat (found by its shape in the door's recess)",
            "the cup doormat: a pictogram where a word should be (research §5 item 4); the entry mosaic replaces it (Street 1)",
            run.Cafe.GetComponentsInChildren<Renderer>(false).Where(r =>
            {
                if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || (redesign != null && r.transform.IsChildOf(redesign))) return false;
                if (r.GetComponent<Collider>() != null) return false;
                Bounds b = r.bounds;
                if (b.size.y > .04f || b.min.y < -.03f || b.max.y > .06f) return false;
                if (b.center.x < -1f || b.center.x > 1f || b.center.z < -1.6f || b.center.z > 1f) return false;
                if (b.size.x > 1.6f || b.size.z > 1.2f || b.size.x < .3f || b.size.z < .2f) return false;
                return !r.sharedMaterials.Any(m => m != null && mats.Any(w => m.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
            }));
        return found;
    }

    // Every gameplay anchor in and around the café: interactables, slots, the queue, waiting spots and seats (their stand,
    // sit and cup points), the spawns, the drinks station.
    static List<(Transform t, Vector3 p, Quaternion r, string what)> GameplayAnchors(S.Run run)
    {
        var ts = new List<(Transform t, string what)>();
        void Add(Transform t, string what) { if (t != null) ts.Add((t, what)); }
        foreach (Interactable i in S.InScene<Interactable>(run)) { Add(i.transform, "interactable"); if (i is StationInteractable s) Add(s.StandPoint, "stand point"); }
        foreach (ItemSlotArea a in S.InScene<ItemSlotArea>(run)) { Add(a.transform, "slot area"); foreach (Transform c in a.transform) Add(c, "slot"); }
        foreach (IntakeShelf i in S.InScene<IntakeShelf>(run)) Add(i.transform, "intake shelf");
        foreach (CounterQueue q in S.InScene<CounterQueue>(run)) { Add(q.transform, "queue"); foreach (Transform c in q.transform) Add(c, "queue place"); }
        foreach (WaitingSpot w in S.InScene<WaitingSpot>(run))
        {
            Add(w.transform, w is TableSeat ? "seat" : "waiting spot");
            Add(w.StandPoint, "stand point");
            if (w is TableSeat seat) { Add(seat.SeatPose, "sit point"); Add(seat.CupSpot, "cup spot"); }
        }
        foreach (BeverageStation b in S.InScene<BeverageStation>(run)) Add(b.transform, "drinks station");
        foreach (CustomerSpawner c in S.InScene<CustomerSpawner>(run)) Add(c.transform, "customer spawn");
        foreach (PatronSpawner p in S.InScene<PatronSpawner>(run)) Add(p.transform, "patron spawn");
        return ts.Where(x => x.t.position.x > -9f && x.t.position.x < 9f && x.t.position.z > -4f && x.t.position.z < 20f)
                 .GroupBy(x => x.t).Select(g => g.First())
                 .Select(x => (x.t, x.t.position, x.t.rotation, x.what)).ToList();
    }

    // ---- the plan, written out (the read-only check and the step's survey) ----

    static void ReportPlan(S.Run run, RoomPlan plan)
    {
        run.Line();
        run.Line("Where everything goes (Unity world, metres; yaw: 0 faces north, 180 the street):");
        run.Line($"  5 x CK_Beam at (0, {S.F(plan.deckY, "0.00")}, z) for z {string.Join(", ", BeamZ.Select(z => S.F(z, "0")))}: chestnut, hangers on the side walls, no collider, no shadows, overhead fixtures.");
        run.Line($"  3 x CK_SkylightWell at (0, {S.F(plan.deckY, "0.00")}, z) for z {string.Join(", ", WellZ.Select(z => S.F(z, "0.0")))}; a spot at local (0, {S.F(SkylightLocalY, "0.00")}, 0) " +
                 $"straight down, #FFF1DD, {S.F(SkylightIntensity, "0.0")}, range {S.F(SkylightRange, "0")}, {S.F(SkylightAngle, "0")}°, no shadows.");
        run.Line($"  The ceiling deck: the same outline with a {S.F(HoleHalf * 2f, "0.00")} m square hole over each well, world UVs in metres, one-sided facing down.");
        run.Line($"  The pendants: slot {plan.shadeSlot} (the shade) of every pendant to CF_Enamel; the four over the counter stay.");
        foreach (Target t in plan.targets)
            run.Line($"  Pendant over the {t.what}: ({S.F(t.at.x, "0.00")}, top {S.F(t.at.y, "0.00")}, {S.F(t.at.z, "0.00")})" + (Mathf.Abs(t.shift) > .001f ? $", shifted {S.F(t.shift, "+0.00;-0.00")} m off a beam" : ""));
        run.Line($"  Off: the old four's bodies (PendantFixtures, {plan.fixtures.Length} renderers hidden) and their {plan.oldLights.Length} lights ('{V3TablePendant}').");
        foreach (PlantPlan p in plan.plants)
            run.Line($"  {p.spec.name}: ({S.F(p.moved.x, "0.00")}, 0, {S.F(p.moved.z, "0.00")}) yaw {S.F(p.spec.yaw, "0")}; a capsule on the pot r {S.F(p.spec.radius, "0.00")}, {S.F(CapsuleHeight(p.spec), "0.00")} tall (the pot {S.F(p.spec.height, "0.00")}): {p.spec.why}.");
        run.Line($"  CK_NoticeBoard at {S.V(NoticeBoardAt)} yaw -90 (the small-print pier, by the waiting nook: the brief's z 2.6 is a window post).");
        run.Line($"  CK_PhotoLedge at {S.V(PhotoLedgeAt)} yaw -90 (the local-art pier: the front-of-house pier nearest the counter).");
        run.Line($"  CK_EnamelSign from the z 14 beam's underside at x {S.F(EnamelX, "0.0")}, yaw 180 (the plate about 2.45 to 2.70 m).");
        run.Line("What goes off, and why (switched off; hidden where code finds the thing by name):");
        foreach (Hidden h in plan.off)
            run.Line($"  {(h.switchOff != null ? "off " : "hide")}  {h.what}: {h.why}" + (h.switchOff == null ? $" ({h.renderers.Count} renderer(s){(h.colliders.Count > 0 ? $", {h.colliders.Count} collider(s)" : "")})" : ""));
        if (!plan.off.Any(h => h.what.StartsWith("the cup doormat", StringComparison.Ordinal)))
            run.Info("No doormat found by its shape in the doorway (already gone, or part of a larger mesh): look at the door in the photos.");
    }

    // ================================================================== ROOM 1: the change

    sealed class Made
    {
        public List<GameObject> beams = new List<GameObject>(), wells = new List<GameObject>(), pendants = new List<GameObject>(), plants = new List<GameObject>();
        public List<Light> wellLights = new List<Light>(), pendantLights = new List<Light>();
        public GameObject board, ledge, sign;
        public Mesh deckWas, deckNow;
    }

    static void ChangeRoom(S.Run run, RoomPlan plan)
    {
        var made = new Made();
        Transform group = S.StepGroup(run, RoomGroup);
        Transform ceiling = S.Group(run, group, "Ceiling - beams and skylight wells");
        Transform lamps = S.Group(run, group, "Pendants over the tables");
        Transform greens = S.Group(run, group, "Plants");
        Transform wall = S.Group(run, group, "East wall - notice board and photo ledge");
        Transform bar = S.Group(run, group, "Repair bar - the REPAIRS sign");

        // 1. The beams and the wells: hidden from above, no collider, no shadows (the deck casts none: the sun would draw the
        //    beams' stripes on the floor in first person and not from above, as session 6 found with the pendants).
        foreach (float z in BeamZ)
        {
            GameObject beam = NoColliders(S.Place(run, "CK_Beam", ceiling, new Vector3(0f, plan.deckY, z), 0f, $"CK_Beam (z {S.F(z, "0")})"));
            Overhead(run, plan, beam, "a ceiling beam: seen in first person, hidden from above (research §6.6)");
            made.beams.Add(beam);
        }
        foreach (float z in WellZ)
        {
            GameObject well = NoColliders(S.Place(run, "CK_SkylightWell", ceiling, new Vector3(0f, plan.deckY, z), 0f, $"CK_SkylightWell (z {S.F(z, "0.0")})"));
            Overhead(run, plan, well, "a skylight well: seen in first person, hidden from above (research §6.6)");
            Light l = S.MakeLight(run, well.transform, "Skylight daylight (straight down)", well.transform.TransformPoint(new Vector3(0f, SkylightLocalY, 0f)),
                                  Quaternion.LookRotation(Vector3.down, Vector3.forward), LightType.Spot, SkylightColour, SkylightIntensity, SkylightRange, SkylightAngle, LightShadows.None);
            made.wells.Add(well);
            made.wellLights.Add(l);
        }
        S.Note(run, "The skylights' spots are plain lights: CafeDaylight has no day-only list, so they also shine at night, at full strength (flagged; no new mechanic).");

        // 2. The ceiling deck, cut for the wells.
        made.deckWas = plan.deck.sharedMesh;
        Mesh deck = DeckWithHoles(plan.deck, plan.deckData, plan.deckY, out string built);
        S.Reshape(run, plan.deck, deck, $"the inside-only ceiling with a {S.F(HoleHalf * 2f, "0.00")} m hole over each skylight well (z {string.Join(", ", WellZ.Select(z => S.F(z, "0.0")))}): {built}");
        made.deckNow = deck;

        // 3. The pendants: the shade cream enamel on the counter's four; the old four's bodies hidden, their lights off; seven
        //    copies of the counter's pendant (its mesh and its light) over the new tables.
        foreach (Renderer r in plan.counterPendants)
        {
            Material[] m = r.sharedMaterials;
            m[plan.shadeSlot] = plan.enamel;
            S.Repaint(run, r, m, "the shade cream enamel, CF_Enamel #F2EFE8 (research §6.7: the same pendant, a new shade colour)");
        }
        foreach (Renderer r in plan.fixtures)
        {
            S.Toggle(run, r, false, "today's table pendants (PendantFixtures, one mesh per material in the joinery: they can't move one by one): hidden, the seven new ones hang over the new tables");
            Overridden(r);
        }
        foreach (GameObject g in plan.oldLights)
        {
            S.SwitchOff(run, g, "today's table pendants' light: its pendant is hidden, the tables it lit are gone (Seats 1); each new pendant brings its own light");
            Overridden(g);
        }
        foreach (Target t in plan.targets)
        {
            GameObject copy = S.Made(run, Object.Instantiate(plan.counterPendants[0].gameObject, lamps));
            copy.name = $"Table pendant ({t.what})";
            float low = TablePendantTop(plan);
            copy.transform.SetPositionAndRotation(new Vector3(t.at.x, low, t.at.z), Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            // its cord, from the pendant's own top up to the ceiling where the counter's pendants hang from (a child: it
            // goes with the pendant, and is hidden from above with it)
            var cord = new GameObject("Table pendant cord", typeof(MeshFilter), typeof(MeshRenderer));
            cord.transform.SetParent(copy.transform, false);
            float cordLength = plan.pendantTop - low;
            cord.transform.localPosition = new Vector3(0f, cordLength * .5f, 0f);
            cord.transform.localRotation = Quaternion.identity;
            cord.transform.localScale = new Vector3(CordDiameter, cordLength * .5f, CordDiameter);
            cord.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            Material[] own = copy.GetComponent<Renderer>() != null ? copy.GetComponent<Renderer>().sharedMaterials : new Material[0];
            MeshRenderer cordR = cord.GetComponent<MeshRenderer>();
            cordR.sharedMaterial = own.FirstOrDefault(m => m != null && m.name.Contains("Ink")) ?? own.FirstOrDefault(m => m != null);
            cordR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Light l = copy.GetComponentInChildren<Light>(true);
            if (l != null)
            {
                l.name = "Table pendant light";
                l.intensity = TablePendantIntensity;
                l.range = PendantRange;
            }
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                S.AddToList(run, plan.view, "overheadFixtures", r, "a pendant over a table: hidden from above, like the counter's");
            if (l != null) S.AddToList(run, plan.daylight, "warmLights", l, "a pendant's light: dimmed by day and at night, like the counter's");
            made.pendants.Add(copy);
            if (l != null) made.pendantLights.Add(l);
        }

        // 4. The REPAIRS sign, from the z 14 beam's underside over the repair bar.
        GameObject beam14 = made.beams[BeamZ.ToList().IndexOf(14f)];
        float underside = Underside(beam14, new Vector3(EnamelX, plan.deckY - .5f, EnamelZ), plan.deckY);
        made.sign = NoColliders(S.Place(run, "CK_EnamelSign", bar, new Vector3(EnamelX, underside, EnamelZ), 180f, "CK_EnamelSign (repair bar, REPAIRS)"));
        Overhead(run, plan, made.sign, "the repair bar's REPAIRS sign, hung from the beam: hidden from above with it");

        // 5. The plants, a capsule on each pot (people walk round the pot; the leaves have none).
        foreach (PlantPlan p in plan.plants)
        {
            GameObject g = NoColliders(S.Place(run, p.spec.piece, greens, p.moved, p.spec.yaw, p.spec.name));
            CapsuleCollider c = S.AddComponent<CapsuleCollider>(run, g);
            c.direction = 1;
            c.radius = p.spec.radius;
            c.height = CapsuleHeight(p.spec);
            c.center = new Vector3(0f, CapsuleHeight(p.spec) * .5f, 0f);
            made.plants.Add(g);
        }

        // 6. The east wall's two things.
        made.board = NoColliders(S.Place(run, "CK_NoticeBoard", wall, NoticeBoardAt, -90f, "CK_NoticeBoard (east pier by the waiting nook)"));
        made.ledge = NoColliders(S.Place(run, "CK_PhotoLedge", wall, PhotoLedgeAt, -90f, "CK_PhotoLedge (east pier nearest the counter)"));

        // 7. What goes off.
        foreach (Hidden h in plan.off)
        {
            if (h.switchOff != null) { S.SwitchOff(run, h.switchOff, h.why); Overridden(h.switchOff); continue; }
            foreach (Renderer r in h.renderers) { S.Toggle(run, r, false, h.why); Overridden(r); }
            foreach (Collider c in h.colliders) { S.Toggle(run, c, false, h.why); Overridden(c); }
        }
        Physics.SyncTransforms();

        // 8. The checks, in numbers. Any FAIL puts everything back.
        SelfCheck(run, plan, made);
    }

    // The shared Toggle and SwitchOff record a change with Undo.RecordObject alone. On a prefab instance (the joinery's
    // PendantFixtures, the POLYGON pot plants) the override is also recorded explicitly, as the shared Move and Repaint do.
    static void Overridden(Object o)
    {
        if (o != null && PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // A placed piece's own colliders, if Kit 1 imported any, switched off (the manifests: none; the plants' capsule comes
    // after). The piece is the step's own, so its Undo takes them with it.
    static GameObject NoColliders(GameObject piece)
    {
        foreach (Collider c in piece.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
            Overridden(c);
        }
        return piece;
    }

    // A placed piece's renderers: no shadows, registered as overhead fixtures.
    static void Overhead(S.Run run, RoomPlan plan, GameObject piece, string why)
    {
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            S.AddToList(run, plan.view, "overheadFixtures", r, why);
        }
    }

    // The beam's wood underside at a point (a ray up against its mesh); its manifest depth when the mesh can't be read.
    static float Underside(GameObject beam, Vector3 below, float top)
    {
        float best = top - BeamDepth;
        foreach (MeshFilter f in beam.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshData md = MeshData.Read(f.sharedMesh);
            if (md == null) continue;
            float t = md.Ray(f.transform.localToWorldMatrix, below, Vector3.up, 1f);
            if (!float.IsPositiveInfinity(t)) return below.y + t;
        }
        return best;
    }

    // The deck rebuilt: its outline split along every hole's edges (a grid, so no T-junctions), every cell but the holes
    // kept; vertices in the deck's own frame, UVs in world metres as CafeStreetUpgrade.WorldUV gave it, every triangle
    // facing the way the old ones did (down), one submesh for its one material.
    static Mesh DeckWithHoles(MeshFilter deck, MeshData was, float y, out string built)
    {
        Transform t = deck.transform;
        Vector3[] w = was.v.Select(v => t.TransformPoint(v)).ToArray();
        float x0 = w.Min(p => p.x), x1 = w.Max(p => p.x), z0 = w.Min(p => p.z), z1 = w.Max(p => p.z);
        var xs = new List<float> { x0, -HoleHalf, HoleHalf, x1 };
        var zs = new List<float> { z0, z1 };
        foreach (float z in WellZ) { zs.Add(z - HoleHalf); zs.Add(z + HoleHalf); }
        xs = xs.Distinct().OrderBy(v => v).ToList();
        zs = zs.Distinct().OrderBy(v => v).ToList();
        bool Hole(float cx, float cz) => Mathf.Abs(cx) < HoleHalf && WellZ.Any(z => Mathf.Abs(cz - z) < HoleHalf);
        // The old mesh's facing, in the world: the new triangles turn the same way.
        Vector3 oldFacing = Vector3.zero;
        for (int i = 0; i + 2 < was.t.Length; i += 3) oldFacing += Vector3.Cross(w[was.t[i + 1]] - w[was.t[i]], w[was.t[i + 2]] - w[was.t[i]]);
        Vector3 localNormal = t.localToWorldMatrix.transpose.MultiplyVector(oldFacing.y < 0f ? Vector3.down : Vector3.up).normalized;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        int cols = xs.Count;
        for (int j = 0; j < zs.Count; j++)
            for (int i = 0; i < xs.Count; i++)
            {
                verts.Add(t.InverseTransformPoint(new Vector3(xs[i], y, zs[j])));
                normals.Add(localNormal);
                uv.Add(new Vector2(xs[i], zs[j]));
            }
        int holes = 0;
        for (int j = 0; j + 1 < zs.Count; j++)
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                if (Hole((xs[i] + xs[i + 1]) * .5f, (zs[j] + zs[j + 1]) * .5f)) { holes++; continue; }
                int a = j * cols + i, b = a + 1, c = a + cols + 1, d = a + cols;
                // World winding: Cross(b - a, c - a) for x then z points down; flip it if the old deck faced up.
                if (oldFacing.y < 0f) tris.AddRange(new[] { a, b, c, a, c, d });
                else tris.AddRange(new[] { a, c, b, a, d, c });
            }
        var mesh = new Mesh { name = deck.sharedMesh.name + " - cut for the skylight wells" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        built = $"{verts.Count} vertices, {tris.Count / 3} triangles, {holes} hole cells";
        return mesh;
    }

    // ================================================================== ROOM 1: the checks after

    static void SelfCheck(S.Run run, RoomPlan plan, Made made)
    {
        run.Line("Checked after the change:");
        HashSet<Renderer> overhead = new HashSet<Renderer>(plan.view.overheadFixtures.Where(r => r != null));
        HashSet<Light> warm = new HashSet<Light>(plan.daylight.warmLights.Where(l => l != null));

        // ---- the beams ----
        for (int i = 0; i < BeamZ.Length; i++)
        {
            Bounds b = S.RendererBounds(made.beams[i].transform);
            (float west, float east) faces = plan.wallFaces[BeamZ[i]];
            run.Check(Mathf.Abs(b.max.y - plan.deckY) < .005f && b.min.x >= -7.41f && b.max.x <= 7.41f && Mathf.Abs(b.center.z - BeamZ[i]) < .01f,
                      $"the beam at z {S.F(BeamZ[i], "0")} on the ceiling: {S.BoundsText(b)}");
            run.Check(Mathf.Abs(b.min.x - faces.west) <= .05f && Mathf.Abs(b.max.x - faces.east) <= .05f,
                      $"…its hangers meet the walls: west {S.F(b.min.x - faces.west, "+0.000;-0.000")} m, east {S.F(faces.east - b.max.x, "+0.000;-0.000")} m");
        }
        // ---- the wells and their lights ----
        for (int i = 0; i < WellZ.Length; i++)
        {
            Bounds b = S.RendererBounds(made.wells[i].transform);
            run.Check(Mathf.Abs(b.min.y - (plan.deckY - .018f)) < .005f && Mathf.Abs(b.center.x) < .01f && Mathf.Abs(b.center.z - WellZ[i]) < .01f,
                      $"the well at z {S.F(WellZ[i], "0.0")}: its trim 18 mm under the deck, {S.BoundsText(b)}");
            Light l = made.wellLights[i];
            run.Check(l.type == LightType.Spot && Vector3.Dot(l.transform.forward, Vector3.down) > .9999f && Same(l.color, SkylightColour) &&
                      Mathf.Abs(l.intensity - SkylightIntensity) < .001f && Mathf.Abs(l.range - SkylightRange) < .001f && Mathf.Abs(l.spotAngle - SkylightAngle) < .01f && l.shadows == LightShadows.None,
                      $"…its daylight: a spot straight down from {S.V(l.transform.position)}, #{ColorUtility.ToHtmlStringRGB(l.color)}, {S.F(l.intensity, "0.0")}, range {S.F(l.range, "0")}, {S.F(l.spotAngle, "0")}°, no shadows");
            run.Info($"…not a warm light (CafeDaylight has no day-only list): it shines by night as by day{(warm.Contains(l) ? " (BUT it is in warmLights)" : "")}");
        }
        // ---- the deck's holes ----
        MeshData now = MeshData.Read(made.deckNow);
        Transform deck = plan.deck.transform;
        float area = Area(now, deck.localToWorldMatrix, out int up, out int down);
        float want = (Room.xMax - Room.xMin) * (Room.yMax - Room.yMin) - WellZ.Length * 4f * HoleHalf * HoleHalf;
        run.Check(Mathf.Abs(area - want) < .01f && up == 0, $"the deck: {S.F(area, "0.00")} m² of ceiling (the room less three {S.F(HoleHalf * 2f, "0.00")} m holes: {S.F(want, "0.00")}), every triangle facing down ({down})");
        run.Check(UVsAreWorldMetres(plan.deck, Vector2.zero, out string uvs), $"…its UVs: {uvs}");
        foreach (float z in WellZ)
        {
            Matrix4x4 m = deck.localToWorldMatrix;
            bool open = new[] { Vector3.zero, new Vector3(.55f, 0, .55f), new Vector3(-.55f, 0, -.55f), new Vector3(.55f, 0, -.55f), new Vector3(-.55f, 0, .55f) }
                        .All(o => float.IsPositiveInfinity(now.Ray(m, new Vector3(o.x, plan.deckY - .2f, z + o.z), Vector3.up, .4f)));
            bool closedRound = new[] { new Vector3(.70f, 0, 0), new Vector3(-.70f, 0, 0), new Vector3(0, 0, .70f), new Vector3(0, 0, -.70f) }
                        .All(o => !float.IsPositiveInfinity(now.Ray(m, new Vector3(o.x, plan.deckY - .2f, z + o.z), Vector3.up, .4f)));
            run.Check(open && closedRound, $"…open over the well at z {S.F(z, "0.0")} (rays up through its middle and corners meet no ceiling) and closed 0.70 m out, under its trim");
        }
        // ---- the pendants ----
        Material[] counter = plan.counterPendants[0].sharedMaterials;
        foreach (Renderer r in plan.counterPendants.Concat(made.pendants.Select(p => p.GetComponent<Renderer>())))
        {
            Material[] m = r.sharedMaterials;
            bool painted = m.Length == counter.Length && m[plan.shadeSlot] == plan.enamel && Enumerable.Range(0, m.Length).All(i => i == plan.shadeSlot || m[i] == counter[i]);
            run.Check(painted && overhead.Contains(r), $"{r.name}: the shade CF_Enamel, its other parts as they were, an overhead fixture");
        }
        for (int i = 0; i < made.pendants.Count; i++)
        {
            Target t = plan.targets[i];
            GameObject p = made.pendants[i];
            Light l = i < made.pendantLights.Count ? made.pendantLights[i] : null;
            (float cord, float lamp, string near) = Clearance(plan, p.transform.position);
            Bounds top = S.RendererBounds(t.table);
            bool over = p.transform.position.x > top.min.x && p.transform.position.x < top.max.x && p.transform.position.z > top.min.z && p.transform.position.z < top.max.z;
            float low = TablePendantTop(plan);
            Transform cordT = p.transform.Find("Table pendant cord");
            Bounds cordB = cordT != null && cordT.GetComponent<Renderer>() != null ? cordT.GetComponent<Renderer>().bounds : new Bounds();
            run.Check(Flat2(p.transform.position - t.at).magnitude < .001f && Mathf.Abs(p.transform.position.y - low) < .001f && over &&
                      cordT != null && Mathf.Abs(cordB.max.y - plan.pendantTop) < .01f && Mathf.Abs(cordB.min.y - low) < .01f,
                      $"{p.name}: over its table ({S.F(top.min.x, "0.00")}..{S.F(top.max.x, "0.00")}, {S.F(top.min.z, "0.00")}..{S.F(top.max.z, "0.00")}) at {S.V(p.transform.position)}, " +
                      $"its shade's rim {S.F(TableTopHeight + TableRimOverTop)} m (0.95 over the table), its cord up to {S.F(plan.pendantTop)}");
            run.Check(cord >= CordClear - .0005f && lamp >= MeshClear - .0005f, $"…its cord {S.F(cord, "0.000")} m from {near}'s face, the lamp {S.F(lamp, "0.000")} m from any beam");
            run.Check(l != null && l.type == LightType.Point && Same(l.color, PendantColour) && Mathf.Abs(l.intensity - TablePendantIntensity) < .001f && Mathf.Abs(l.range - PendantRange) < .001f &&
                      l.shadows == LightShadows.None && warm.Contains(l) && l.transform.IsChildOf(p.transform),
                      $"…its light #FFC98F, {S.F(TablePendantIntensity, "0.0")}, range {S.F(PendantRange, "0.0")}, no shadows, a warm light, in its pendant ({(l != null ? S.V(l.transform.position) : "missing")})");
        }
        run.Check(plan.fixtures.All(r => !r.enabled) && plan.oldLights.All(g => !g.activeInHierarchy), "today's table pendants: their bodies hidden, their lights off");
        // ---- the sign ----
        Bounds sb = S.RendererBounds(made.sign.transform);
        run.Check(sb.min.y >= 2.43f && sb.min.y <= 2.47f && Mathf.Abs(sb.max.y - made.sign.transform.position.y) < .005f && Mathf.Abs(sb.center.x - EnamelX) < .02f,
                  $"the REPAIRS sign hangs from the beam's underside ({S.F(made.sign.transform.position.y)}) to {S.F(sb.min.y)} m (the plate 2.45 to 2.70): {S.BoundsText(sb)}");
        if (plan.benchLamp != null)
        {
            Bounds lb = S.RendererBounds(plan.benchLamp);
            run.Check(!Expand(lb, .3f).Intersects(sb), $"…0.3 m or more clear of the bench lamp ({S.BoundsText(lb)})");
        }
        foreach (Renderer r in plan.counterPendants)
            run.Check(!Expand(r.bounds, .1f).Intersects(sb), $"…clear of {r.name}");
        // ---- the plants ----
        foreach (var (p, g) in plan.plants.Zip(made.plants, (a, b) => (a, b)))
        {
            Bounds b = S.RendererBounds(g.transform);
            CapsuleCollider c = g.GetComponent<CapsuleCollider>();
            RaycastHit? under = S.Down(run, new Vector3(p.moved.x + p.spec.radius + .05f, .3f, p.moved.z), 1f, col => col.transform.IsChildOf(g.transform));
            run.Check(Mathf.Abs(b.min.y) < .01f && under.HasValue && Mathf.Abs(under.Value.point.y) < .01f,
                      $"{g.name} stands on the floor: its bottom at {S.F(b.min.y)}, the floor beside it at {(under.HasValue ? S.F(under.Value.point.y) : "nothing")}");
            Bounds cb = c != null ? c.bounds : new Bounds();
            run.Check(c != null && c.enabled && !c.isTrigger && Mathf.Abs(cb.center.x - p.moved.x) < .005f && Mathf.Abs(cb.center.z - p.moved.z) < .005f &&
                      Mathf.Abs(cb.min.y) < .01f && cb.max.y < CapsuleHeight(p.spec) + .01f,
                      $"…a capsule on its pot only (r {S.F(p.spec.radius, "0.00")}, {S.F(CapsuleHeight(p.spec), "0.00")} tall, on the floor): {S.BoundsText(cb)}");
            foreach (var k in plan.keepClear)
            {
                float d = FlatDistance(k.p, cb);
                if (d < k.r + .5f) run.Check(d >= k.r, $"…{S.F(d, "0.00")} m from {k.what} (needs {S.F(k.r, "0.00")})");
            }
            run.Check(!Overlaps2(b, DoorLane) && !Overlaps2(cb, DoorToCounter), $"…its leaves out of the door lane, its pot out of the door-to-counter lane (leaves x {S.F(b.min.x, "0.00")}..{S.F(b.max.x, "0.00")}, z {S.F(b.min.z, "0.00")}..{S.F(b.max.z, "0.00")})");
            run.Check(b.min.z >= 0f, $"…its leaves inside the front wall (z {S.F(b.min.z)} from 0)");
        }
        // ---- the east wall ----
        foreach ((GameObject g, string piece, Vector3 at) in new[] { (made.board, "CK_NoticeBoard", NoticeBoardAt), (made.ledge, "CK_PhotoLedge", PhotoLedgeAt) })
        {
            Bounds b = S.RendererBounds(g.transform);
            Bounds want2 = Predicted(plan.kit[piece], at, -90f);
            run.Check(Approximately(b, want2, .005f) && b.max.x <= EastFace + .002f && Mathf.Abs(b.min.y - at.y) < .005f,
                      $"{g.name} where planned, its back on the wall at x {S.F(b.max.x)}, its bottom at {S.F(b.min.y)}: {S.BoundsText(b)}");
        }
        // ---- nothing this step placed is solid but the plants' pots ----
        List<string> solid = made.beams.Concat(made.wells).Concat(made.plants).Append(made.sign).Append(made.board).Append(made.ledge)
            .SelectMany(g => g.GetComponentsInChildren<Collider>(true))
            .Where(c => c.enabled && !(c is CapsuleCollider && made.plants.Contains(c.gameObject))).Select(c => S.PathOf(c.transform)).ToList();
        run.Check(solid.Count == 0, solid.Count == 0 ? "no collider on the beams, the wells, the sign, the board or the ledge; the plants' only one the capsule on the pot"
                                                    : "solid where nothing should be: " + string.Join(", ", solid));
        // ---- the overhead view sees none of the ceiling's things ----
        var ceilingThings = made.beams.Concat(made.wells).Concat(made.pendants).Append(made.sign).Concat(plan.counterPendants.Select(r => r.gameObject))
                                      .SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToList();
        List<Renderer> missing = ceilingThings.Where(r => !overhead.Contains(r)).ToList();
        run.Check(missing.Count == 0, missing.Count == 0 ? $"every renderer of the ceiling's things ({ceilingThings.Count}) is an overhead fixture: hidden in the overhead view"
                                                        : "not overhead fixtures: " + string.Join(", ", missing.Select(r => S.PathOf(r.transform))));
        // ---- nothing in the close-up's frame (Bench 3) ----
        if (plan.closeUp != null)
        {
            Plane[] frustum = Frustum(plan.closeUp, plan.closeUpFov, 16f / 9f, .05f, 6f);
            // A box inside the frustum isn't yet in the frame: what the bench, the counter or the glass's shoe hide isn't seen
            // (10 Oct, Claude: the monstera's box met the frustum under the bench top, where the counter's body hides it, and
            // the step failed on it). Seen = a point of its box inside the frustum with a clear line from the camera.
            var boxOnly = new List<string>();
            List<string> seen = new List<string>();
            foreach (GameObject g in made.beams.Concat(made.wells).Concat(made.pendants).Concat(made.plants).Append(made.sign).Append(made.board).Append(made.ledge))
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (!GeometryUtility.TestPlanesAABB(frustum, r.bounds)) continue;
                    if (SeenFrom(plan.closeUp.position, frustum, r.bounds, g.transform)) seen.Add(S.PathOf(r.transform));
                    else boxOnly.Add(S.PathOf(r.transform));
                }
            if (boxOnly.Count > 0) run.Info("Inside the close-up's frustum but hidden from its camera (the bench, the counter): " + string.Join(", ", boxOnly));
            run.Check(seen.Count == 0, seen.Count == 0 ? $"nothing of this step is in the close-up's frame ({S.F(plan.closeUpFov, "0")}°, at {S.V(plan.closeUp.position)})" : "in the close-up's frame: " + string.Join(", ", seen));
        }
        // ---- what went off is off, and nothing floats where a rug lay ----
        foreach (Hidden h in plan.off)
        {
            bool gone = h.switchOff != null ? !h.switchOff.activeInHierarchy : h.renderers.All(r => !r.enabled) && h.colliders.All(c => !c.enabled);
            run.Check(gone, $"{(h.switchOff != null ? "off" : "hidden")}: {h.what}");
        }
        List<string> floating = LeftFloating(run, plan);
        run.Check(floating.Count == 0, floating.Count == 0 ? "nothing stood on the rugs, runner or doormat: nothing floats where they lay" : "would float where a rug lay: " + string.Join(", ", floating));
        // ---- the gameplay objects never moved ----
        var movedAnchors = plan.anchors.Where(a => a.t != null && ((a.t.position - a.p).magnitude > .001f || Quaternion.Angle(a.t.rotation, a.r) > .1f))
                                       .Select(a => $"{S.PathOf(a.t)} ({a.what}) {S.V(a.p)} -> {S.V(a.t.position)}").ToList();
        run.Check(movedAnchors.Count == 0, movedAnchors.Count == 0 ? $"none of the {plan.anchors.Count} gameplay anchors moved (1 mm, 0.1°)" : "MOVED: " + string.Join("; ", movedAnchors));
        // ---- what the old seating left, for Claude to see (Seats 1's) ----
        foreach (string path in OldSeatingGroups)
        {
            Transform g = S.Find(run, path);
            if (g == null || !g.gameObject.activeInHierarchy) continue;
            List<string> on = g.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy && t.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled)).Select(t => t.name).ToList();
            if (on.Count > 0) run.Info($"still drawn under {path} (Seats 1's, or kept on purpose): {string.Join(", ", on)}");
        }
    }

    // Renderers standing on a hidden floor covering (their bottom on its top, over it), which would now float.
    static List<string> LeftFloating(S.Run run, RoomPlan plan)
    {
        var found = new List<string>();
        foreach (Hidden h in plan.off.Where(h => h.coversFloor))
        {
            Bounds rug = h.bounds;
            foreach (Renderer r in S.InScene<Renderer>(run, false))
            {
                if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || h.renderers.Contains(r) || (h.switchOff != null && r.transform.IsChildOf(h.switchOff.transform))) continue;
                Bounds b = r.bounds;
                if (b.size.x > 3f || b.size.z > 3f) continue;
                if (b.min.y < rug.max.y - .004f || b.min.y > rug.max.y + .02f) continue;
                if (!Shrunk(rug, .02f).Intersects(new Bounds(new Vector3(b.center.x, rug.center.y, b.center.z), new Vector3(b.size.x, rug.size.y + .01f, b.size.z)))) continue;
                found.Add($"{S.PathOf(r.transform)} (on {h.what})");
            }
        }
        return found.Distinct().ToList();
    }

    // A point of the box (27: its corners, edge middles, face middles and centre) inside the frustum that the camera sees
    // with nothing solid in between (triggers and the piece's own colliders aside).
    static bool SeenFrom(Vector3 eye, Plane[] frustum, Bounds b, Transform own)
    {
        for (int i = 0; i <= 2; i++)
            for (int j = 0; j <= 2; j++)
                for (int k = 0; k <= 2; k++)
                {
                    var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, i * .5f), Mathf.Lerp(b.min.y, b.max.y, j * .5f), Mathf.Lerp(b.min.z, b.max.z, k * .5f));
                    if (!frustum.All(plane => plane.GetSide(p))) continue;
                    Vector3 d = p - eye;
                    float dist = d.magnitude;
                    if (dist < .02f) return true;
                    bool blocked = Physics.RaycastAll(eye, d / dist, dist - .01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                          .Any(h => !h.collider.transform.IsChildOf(own));
                    if (!blocked) return true;
                }
        return false;
    }

    // A camera's frustum from its pose and lens (Unity's view space looks down -z).
    static Plane[] Frustum(Transform cam, float fov, float aspect, float near, float far)
    {
        Matrix4x4 view = Matrix4x4.TRS(cam.position, cam.rotation, new Vector3(1f, 1f, -1f)).inverse;
        return GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(fov, aspect, near, far) * view);
    }

    // ================================================================== ROOM 1: photos

    static void RoomPhotos(S.Run run, RoomPlan plan)
    {
        S.Photo(run, "1-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.6f, 12f), 72f);
        S.Photo(run, "2-the-ceiling-from-the-middle", new Vector3(0f, 1.65f, 8f), new Vector3(0f, 3f, 12f), 75f);
        S.Photo(run, "3-from-the-counter", new Vector3(0f, 1.65f, 15f), new Vector3(0f, 1.4f, 4f), 72f);
        S.Photo(run, "4-from-a-banquette-seat", new Vector3(-7.0f, 1.2f, 6.79f), new Vector3(2f, 1.6f, 6f), 62f);
        S.GameCameraPhoto(run, "5-the-game-camera");
        S.Photo(run, "6-the-repair-bar-from-a-stool", new Vector3(-4.2f, 1.45f, 12.85f), new Vector3(-4.35f, 1.95f, 14.1f), 62f);
        S.Photo(run, "7-a-skylight-well-from-below", new Vector3(0f, 1.65f, 4.6f), new Vector3(0f, 3.4f, 6.5f), 60f);
        S.Photo(run, "8-the-east-wall-board-and-ledge", new Vector3(4.6f, 1.6f, 4.6f), new Vector3(7.4f, 1.45f, 4.8f), 62f);
    }

    // ================================================================== small things

    static Color BaseColour(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

    static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < .004f && Mathf.Abs(a.g - b.g) < .004f && Mathf.Abs(a.b - b.b) < .004f;

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Vector2 Flat2(Vector3 v) => new Vector2(v.x, v.z);

    // Horizontal distance from a point to a box (0 inside it), CafeFurnishing's keep-clear measure.
    static float FlatDistance(Vector3 p, Bounds b)
    {
        float dx = Mathf.Max(Mathf.Abs(p.x - b.center.x) - b.extents.x, 0f);
        float dz = Mathf.Max(Mathf.Abs(p.z - b.center.z) - b.extents.z, 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float Distance(Vector3 p, Bounds b)
    {
        float dx = Mathf.Max(Mathf.Abs(p.x - b.center.x) - b.extents.x, 0f);
        float dy = Mathf.Max(Mathf.Abs(p.y - b.center.y) - b.extents.y, 0f);
        float dz = Mathf.Max(Mathf.Abs(p.z - b.center.z) - b.extents.z, 0f);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    static bool Overlaps2(Bounds b, Rect r) => b.max.x > r.xMin && b.min.x < r.xMax && b.max.z > r.yMin && b.min.z < r.yMax;

    static Bounds Shrunk(Bounds b, float by) { b.Expand(-2f * by); return b; }

    static Bounds Expand(Bounds b, float by) { b.Expand(2f * by); return b; }

    static bool Approximately(Bounds a, Bounds b, float tolerance) => (a.min - b.min).magnitude <= tolerance && (a.max - b.max).magnitude <= tolerance;

    static Bounds Union(IEnumerable<Bounds> boxes)
    {
        Bounds? u = null;
        foreach (Bounds b in boxes) { if (u == null) u = b; else { Bounds v = u.Value; v.Encapsulate(b); u = v; } }
        return u ?? new Bounds();
    }

    static Bounds SubMeshBounds(MeshData md, int sub)
    {
        int[] t = md.subs[sub];
        if (t.Length == 0) return new Bounds();
        var b = new Bounds(md.v[t[0]], Vector3.zero);
        foreach (int i in t) b.Encapsulate(md.v[i]);
        return b;
    }

    // Triangles' area in the world, and how many face up and down.
    static float Area(MeshData md, Matrix4x4 m, out int up, out int down)
    {
        float area = 0f;
        up = down = 0;
        for (int i = 0; i + 2 < md.t.Length; i += 3)
        {
            Vector3 a = m.MultiplyPoint3x4(md.v[md.t[i]]), b = m.MultiplyPoint3x4(md.v[md.t[i + 1]]), c = m.MultiplyPoint3x4(md.v[md.t[i + 2]]);
            Vector3 n = Vector3.Cross(b - a, c - a);
            area += n.magnitude * .5f;
            if (n.y > 1e-6f) up++; else if (n.y < -1e-6f) down++;
        }
        return area;
    }

    static bool Under(Transform t, string prefix)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    static Transform ActiveNamed(S.Run run, string prefix) => AllActiveNamed(run, prefix).FirstOrDefault();

    static List<Transform> AllActiveNamed(S.Run run, string prefix) =>
        run.Cafe.GetComponentsInChildren<Transform>(false).Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal) && (t.parent == null || !t.parent.name.StartsWith(prefix, StringComparison.Ordinal))).ToList();

    // ================================================================== a mesh's data, read in the editor

    sealed class MeshData
    {
        public Vector3[] v;
        public Vector3[] n;
        public Vector2[] uv;
        public int[] t;            // every triangle, all submeshes
        public int[][] subs;

        // Straight from the mesh when it is readable; otherwise the editor's read-only copy (MeshUtility's, which reads a
        // model's mesh with Read/Write off; Mesh.AcquireReadOnlyMeshData doesn't); null when neither works (the caller
        // falls back to boxes and says so). Never switches an importer's Read/Write.
        public static MeshData Read(Mesh m)
        {
            if (m == null) return null;
            try
            {
                if (m.isReadable)
                {
                    var d = new MeshData { v = m.vertices, n = m.normals, uv = m.uv, subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s)).ToArray() };
                    d.t = d.subs.SelectMany(x => x).ToArray();
                    return d.v.Length > 0 ? d : null;
                }
                using (Mesh.MeshDataArray all = MeshUtility.AcquireReadOnlyMeshData(m))
                {
                    Mesh.MeshData one = all[0];
                    var d = new MeshData();
                    var v = new NativeArray<Vector3>(one.vertexCount, Allocator.Temp);
                    one.GetVertices(v);
                    d.v = v.ToArray();
                    v.Dispose();
                    if (one.HasVertexAttribute(VertexAttribute.Normal)) { var n = new NativeArray<Vector3>(one.vertexCount, Allocator.Temp); one.GetNormals(n); d.n = n.ToArray(); n.Dispose(); }
                    if (one.HasVertexAttribute(VertexAttribute.TexCoord0)) { var u = new NativeArray<Vector2>(one.vertexCount, Allocator.Temp); one.GetUVs(0, u); d.uv = u.ToArray(); u.Dispose(); }
                    d.subs = new int[one.subMeshCount][];
                    for (int s = 0; s < one.subMeshCount; s++)
                    {
                        var idx = new NativeArray<int>(one.GetSubMesh(s).indexCount, Allocator.Temp);
                        one.GetIndices(idx, s);
                        d.subs[s] = idx.ToArray();
                        idx.Dispose();
                    }
                    d.t = d.subs.SelectMany(x => x).ToArray();
                    return d.v.Length > 0 ? d : null;
                }
            }
            catch (Exception) { return null; }
        }

        // The nearest crossing of a ray with the mesh's triangles (both faces), as a distance; +infinity if none within max.
        public float Ray(Matrix4x4 localToWorld, Vector3 origin, Vector3 dir, float max)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = localToWorld.MultiplyPoint3x4(v[t[i]]), b = localToWorld.MultiplyPoint3x4(v[t[i + 1]]), c = localToWorld.MultiplyPoint3x4(v[t[i + 2]]);
                if (RayTriangle(origin, dir, a, b, c, out float d) && d <= max && d < best) best = d;
            }
            return best;
        }

        // Möller-Trumbore, both faces.
        static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float dist)
        {
            dist = 0f;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(d, q) * inv;
            if (w < 0f || u + w > 1f) return false;
            dist = Vector3.Dot(e2, q) * inv;
            return dist >= 0f;
        }
    }
}
#endif
