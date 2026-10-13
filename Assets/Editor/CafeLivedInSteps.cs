#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// THE LIVED-IN CAFÉ (12-13 Oct 2026). His words (12 Oct, 01:19): "I do wish to also add more decorations to the cafe
// itself, like carpets, rugs, some napkins, trash cans, people on their laptops". The plan: playtest-4-findings-and-
// proposals.md §8, approved ("i trust your judgement"). Fewer, calmer pieces, where real cafés have them: the redesign of
// 10 Oct took four loud floor pieces out on purpose, and they don't come back.
//
//   Fixit Fidget > Cafe redesign > Lived in 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Lived in 1 - Import and check the lived-in pieces (the models only)
//   Fixit Fidget > Cafe redesign > Lived in 2 - Dress the café (scene)
//   Fixit Fidget > Cafe redesign > Lived in 2 - Undo (scene)
//
// THE PIECES are the Blender lead's (Tools/Blender/cafe_livedin.py): Assets/Art/Models/CafeKit/LI_*.fbx, the kit's
// manifest cafe_livedin.json beside them, their textures Assets/Art/Textures/Cafe/LI_*.png. Every size is read from the
// manifest and measured off the imported mesh when the step runs, so a piece that came out a little bigger or smaller
// still lands right; a piece that isn't there, isn't imported upright at scale 1, or doesn't stand on its origin makes
// the step refuse by name before anything changes.
//
// LIVED IN 1 is Kit 1's import for this one manifest (CafeKitSteps' code is private: its pattern is copied here): the
// manifest's own textures and materials (the LI_ and CF_ names it defines; Kit 1's own materials are never redefined),
// every FBX's import settings (scale 1, no animation, cameras or lights, readable, no colliders, normals imported), its
// materials mapped by name (wood to the café's grain), and a check of each piece: one mesh, its triangles and size as
// built, upright, standing on its origin, every material mapped. Photos in a preview scene (the café isn't touched).
//
// LIVED IN 2 (CafeRedesignShared.Step: survey, guards, one undo group with a record, checks, routes re-baked, guards,
// photos; anything failed puts everything back; never saved; nothing deleted). Under 30 - cafe redesign/Lived in:
//   * THE READING CORNER, front-left by the fiddle-leaf fig, along the west window: the old lounge's tub chair, its low
//     table and the front sofa's floor lamp (Seats 1 switched them off on 10 Oct: switched back on here and moved), and
//     a second tub chair copied from the first (the scene has one). The chairs face each other across the table on a
//     north-south line, as far from it as they stood in the lounge; LI_Rug_Corner under the table and the chairs'
//     fronts; the lamp by the window, lit (a warm light, registered with CafeDaylight). Two seats made the way Seats 1
//     makes them (sit point, cup on the table, stand point; Chair, Snap To Seat on, drain 0.6): the occupied-circulation
//     guard's places go from 31 to 33, all served.
//   * MATS: LI_TillMat under Ace's stand point at the till, on the staff floor at the bench and drink mats' height.
//     LI_Rug_Entry only if the mosaic doesn't already cover the door's inside (it does, at 4364aec: the survey says which).
//   * NAPKINS: LI_NapkinDispenser on every table: the three banquette 2-tops and the two bistro tables at the edge away
//     from the overhead camera, the communal table twice on its centre line, the window counter once toward the window.
//     Clear of every cup spot.
//   * THE CONDIMENT STATION on the counter's lowered section (the one stretch of the customers' side near the pickup end
//     that is free: the pastry case and the water station stand on the rest), facing the customers.
//   * BINS: LI_BinStation (exactly that name: the NPC lead's guests find it by name) inside against the front wall east
//     of the door, its openings to the room, a "Drop point" child where a leaving guest can stand; LI_StreetBin on the
//     pavement against the facade, at the place nearest the door that keeps every rule CafeStreetSteps keeps for the
//     sidewalk bench (planters, patio set, spawn, door leaves, the walks to the door, the street's walkers and cars).
//   * LAPTOPS AND BAGS, off: LI_Laptop_Open and LI_Laptop_Closed at the communal table's east 1 (the stool nearest the
//     door) and west 4 (the far end, the other side: two remote workers sit as far apart as one table allows), on the
//     sitter's left of the cup spot, and LI_ToteBag at that seat's feet, each a CHILD OF ITS SEAT named "<piece> (seat
//     <the seat's words>)". Not the window counter (the brief's first thought): its bar stools have Snap To Seat off
//     (Seats 1: the sit clips can't reach a 0.75 m seat, people stand beside them), so a laptop patron there would stand
//     1.3 m from an open laptop; and its south stool sits under the photo ledge, its skirting where the bag would go. It
//     gets its laptop when the bar stools get a perch. The NPC lead's code finds them by name prefix and distance (a
//     laptop within 0.9 m of a seat's cup spot, a bag within 1.1 m of its sit point): each is placed where its own seat
//     finds it, with as few other seats in reach as the table allows.
//   * COATS, on: LI_Coat_OnChair over the backs of the two banquette café chairs people reach last (from the door and
//     from the counter), at the manifest's own fit (the Blender lead built the coat on CK_Chair_Cafe's top rail),
//     children of their chairs, named for their seats.
//   * THE KID'S DRAWING on the notice board, a child of the board (so the overhead view fades it with the board), where it
//     covers the least of the four flyers with its pin in bare cork (the flyers measured off the board's own mesh).
//   * LEFT CUPS, off: LI_LeftCup on three seats' cup spots, children of their seats, named "LI_LeftCup (table <words>)".
// Colliders only where the manifest gives one, and only on floor pieces (the bins): a table-top piece never gets one (a
// collider on a table catches E's ray before the customer or the cup: the intake tray's reason, 10 Oct). A Not Walkable
// NavMeshModifier on the group, as Seats 1's.
//
// Every place is SEARCHED, not assumed: each piece starts where the plan puts it and, if anything is wrong there, tries
// the nearby places its own rule allows (a rug a few centimetres east, a dispenser further in from the edge, the bin
// station further along the wall), taking the first where every rule holds; the survey lists any piece that found none.
// The rules for every piece: nothing drawn passes through it; on its floor or top within 2 mm (a top-piece 5 cm in from
// the edge); nothing standing within its footprint plus 0.10 m (not at its back for a piece set against a wall or the
// counter; leaves and table tops overhead may overhang, as a plant's leaves overhang a reading chair) and no solid there;
// clear of every cup spot; a solid floor piece keeps the furnishing pass's distances from people's places and the
// door-to-counter lane.
//
// THE UNDO: the shared undo moves the old lounge's pieces back but can't switch them off again (the record has no list
// for "switched on"), so this step's own Undo switches them off first, then runs the shared one.
// After it: save the scene, then Fixit Fidget > Night > Night walk 2 - Rebuild the list of what is solid by night (Edit
// Mode) (the street bin is new outside), and save again.
// Reports and photos: Logs/CafeRedesign/lived-in-*; the room's 14 photos: Logs/CafeFurnishing/lived-in-2-before|after-*.
// ---------------------------------------------------------------------------
internal static class CafeLivedInSteps
{
    // ---- the steps ----
    public const string Title = "Lived in 2 - Dress the cafe";
    const string Label = "lived-in-2";
    const string CheckTitle = "Lived in 0 - Check first";
    const string CheckLabel = "lived-in-0";
    const string ImportTitle = "Lived in 1 - Import and check the lived-in pieces (the models only)";
    const string ImportLabel = "lived-in-1";
    const string MenuCheck = Shared.Menu + "Lived in 0 - Check first (read-only)";
    const string MenuImport = Shared.Menu + "Lived in 1 - Import and check the lived-in pieces (the models only)";
    const string MenuDo = Shared.Menu + "Lived in 2 - Dress the café (scene)";
    const string MenuUndo = Shared.Menu + "Lived in 2 - Undo (scene)";
    const string GroupName = "Lived in";
    const string CornerGroup = "Reading corner";
    // The marker on the record's "moved" entries this step also switched on (its Undo switches them off again).
    const string OnMark = "Switched back on and moved: ";
    const string DropPointName = "Drop point (a leaving guest stands here to drop a cup)";

    // ---- the steps it stands on (their records on the café's root) ----
    const string Bar1 = "Bar 1 - The front counter", Seats1 = "Seats 1 - The San Francisco seat plan",
                 Room1 = "Room 1 - Ceiling, light and the room's few things", Street1 = "Street 1 - The storefront's words";

    // ---- the Blender lead's kit ----
    const string Models = Shared.KitModels;
    const string ManifestPath = Models + "/cafe_livedin.json";
    const string Textures = Shared.KitTextures;
    const string MaterialFolder = Shared.KitMaterials;
    const string ProjectMaterialFolder = "Assets/Art/Materials";
    const string FurnishingMaterials = "Assets/Art/CafeFurnishing/Materials";
    const string Palette = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";
    // Kit 1's manifests (and the intake tray's): the materials they define are theirs, never redefined here.
    static readonly string[] KitOneManifests = { "cafe_signs.json", "cafe_bar.json", "cafe_props.json", "cafe_seating.json", "cafe_intake.json" };
    static readonly Dictionary<string, string> GrainRemaps = new Dictionary<string, string>
    {
        { "CC_Wood_Counter", "Cafe grain - CC_Wood_Counter" },
        { "CC_Wood_Espresso", "Cafe grain - CC_Wood_Espresso" },
    };

    const string RugCorner = "LI_Rug_Corner", RugEntry = "LI_Rug_Entry", TillMat = "LI_TillMat", Napkins = "LI_NapkinDispenser",
                 Condiments = "LI_CondimentStation", BinStation = "LI_BinStation", StreetBin = "LI_StreetBin", LaptopOpen = "LI_Laptop_Open",
                 LaptopClosed = "LI_Laptop_Closed", Tote = "LI_ToteBag", Coat = "LI_Coat_OnChair", Drawing = "LI_KidsDrawing", LeftCup = "LI_LeftCup";
    static readonly string[] AllPieces = { RugCorner, RugEntry, TillMat, Napkins, Condiments, BinStation, StreetBin, LaptopOpen, LaptopClosed, Tote, Coat, Drawing, LeftCup };

    // ---- the scene's things, by their paths or names under the café's root (YAML at 4364aec) ----
    const string LoungePath = "18 - cafe furnishing/A - Window lounge/";
    const string TubChairPath = LoungePath + "Tub chair", LoungeTablePath = LoungePath + "Lounge table", LampPath = LoungePath + "Floor lamp (front sofa)";
    const string OldTubSeatPath = "12 - authored cafe interior/Window upholstered seating/Lounge seats (pass 2)/Tub chair";
    const string CounterName = "CK_FrontCounter (the one front counter)";
    const string WaterName = "CK_WaterStation (the counter, on the half-round end)";
    const string CasePath = "12 - authored cafe interior/Cafe service details/PastryDisplay";
    const string MosaicName = "CK_EntryMosaic (inside the door)";
    const string BoardName = "CK_NoticeBoard (east pier by the waiting nook)";
    const string OliveEastName = "CK_OliveTree (door, east)";
    const string BenchMatPath = "18 - cafe furnishing/F - Work areas/Repair bench mat", DrinkMatPath = "18 - cafe furnishing/F - Work areas/Drink station mat";
    const string FrontRightSillPath = "01 - room and windows/Front right sill", FrontLeftSillPath = "01 - room and windows/Front left sill";
    const string ShellPath = "01 - room and windows";
    const string RightWallPath = ShellPath + "/Right plaster";
    const string DoorLeavesPath = "12 - authored cafe interior/Open door collision";
    const string PlanterName = "Patio - Planter_01";
    const string PatioPath = "08 - front patio bistro nook";
    const string AFramePath = "18 - cafe furnishing/D - Entrance and waiting nook/Patio A-frame";
    const string SidewalkBenchName = "CK_SidewalkBench (west bulkhead)";
    const string WindowCounterName = "CK_WindowCounter (window counter)", CommunalName = "CK_CommunalTable (communal table)";

    // ================================================================== the plan's numbers (each with where it comes from)

    // THE READING CORNER. Front-left, by the fig, along the west (street) window: the low table's centre here, its long
    // side across (x), the chairs north and south of it facing each other. Chosen on a replica of the occupied-
    // circulation guard fed every collider of the scene as saved (4364aec): 33 of 33 places served with the corner's two
    // seats; the banquette's south bench seat keeps 34 cells within reach (it had 106; the guard needs one). Rejected:
    // across the front window (x -6.6..-4.2: it stands on WaitSpot 5's old place and crowds the door's west side) and past
    // the banquette's north end (no room: WaitSpot 1, the north 2-top's seats, the monstera).
    static readonly Vector2 CornerTable = new Vector2(-6.00f, 1.40f);
    // The old lounge as it stood (read off the old seat and table when the step runs; these are the fall-backs): chair
    // centre to table centre 0.83 m, the sit point 8 cm ahead of the chair's centre and 0.415 m over its root, the cup
    // 0.17 m from the table's centre toward the chair and 0.427 m over the table's root.
    const float OldChairToTable = .83f, OldSeatAhead = .08f, OldSeatUp = .415f, OldCupFromTable = .17f, OldCupUp = .427f;
    // The stand points, from the table's centre: beside each chair on the room's side (the old tub chair's stood 0.92 m to
    // its side), the north one level with the table's north edge so the banquette's south bench keeps its approach.
    static readonly Vector2 CornerStandSouth = new Vector2(.92f, -.88f), CornerStandNorth = new Vector2(.95f, .55f);
    // The floor lamp: by the window at the north chair's side (between it and the glass, clear of the rug).
    static readonly Vector2 CornerLamp = new Vector2(-6.80f, 2.30f);
    static readonly Color LampColour = new Color(1f, 201f / 255f, 143f / 255f);     // #FFC98F, warm, as the pendants
    const float LampIntensity = .8f, LampRange = 2.5f, LampShadeHeight = 1.53f;     // the evening's (CafeDaylight: 40 % by day)
    // The rug may sit off the table's centre by this much (a few centimetres east, clear of the fig's pot), the table
    // keeping 3 cm of rug on every side and the chairs' fronts 15 cm.
    const float RugSlide = .10f, RugBorder = .03f, RugUnderFront = .15f;

    // Seats 1's rules for a seat, kept: the stand point's height, the drain, the clearances.
    const float StandY = .04f, SeatDrain = .6f, NavMargin = .40f, SeatClaim = .70f, SeatToWaiting = 1.05f, SeatToQueue = .90f;
    // CafeFurnishing's keep-clear for floor pieces with a solid, and the door-to-counter lane.
    const float KeepSeat = .75f, KeepWaiting = .80f, KeepQueue = .90f, KeepStation = .80f, KeepSpawn = 1f;
    static readonly Rect DoorLane = Rect.MinMaxRect(-1f, -1.2f, 1f, 12.5f);
    const float FreeMargin = .10f;          // nothing within a piece's footprint plus this (the brief)
    const float StandingHeight = .30f;      // ... standing on its floor or top up to this high (leaves and tops above may overhang)
    const float OnSurface = .002f;          // on its floor or top within 2 mm (the brief)
    const float EdgeMargin = .05f;          // a top-piece's footprint at least this far in from its top's edges
    const float CupKeep = .07f;             // ... and from any seat's cup spot (the game's cup is 9.2 cm round: 4.6 cm, and 2.4 cm more)

    // THE COUNTER (CafeBarSteps' measured numbers): the stone's edge on the customers' side, the lowered section's height.
    const float CounterFrontEdge = 13.30f, LowTop = .91f;

    // THE BIN STATION: against the front wall east of the door, its back 2 cm off the bulkhead, at least 15 cm past the
    // east olive's leaves; searched east from there for up to 0.6 m. WaitSpots 5 and 6 (switched off since 23 Sept) kept
    // 0.8 m clear all the same.
    const float BinMinX = 2.65f, BinOffWall = .02f, BinPastLeaves = .15f, BinSearch = .60f, DropAhead = .45f;
    // THE STREET BIN: the sidewalk bench's rules (CafeStreetSteps), and a person passes any other street piece.
    const float StreetPlanterGap = .25f, StreetPasses = .60f, StreetSpawn = 1f, StreetLeaves = 1f, StreetWalks = 1f, StreetWalkers = .6f, StreetCars = 1.2f, StreetLaneHalf = 1.5f;

    // THE LAPTOPS: on the sitter's left of the cup spot (right-handers keep the cup on the right), 5 cm between.
    const float LaptopGapToCup = .05f, CupRadius = .045f;

    // THE KID'S DRAWING: tilted a little (a child pinned it), 0.4 mm in front of the flyers.
    const float DrawingTilt = -3f, DrawingProud = .0004f;
    // The notice board's flyers if the board's mesh can't be read (Tools/Blender/cafe_signs.py FLYERS, in the board's
    // Unity frame: x = -Blender x; up from the frame's inside edge, 3 cm).
    static readonly Rect[] FlyersAsBuilt =
    {
        Rect.MinMaxRect(.192f, .2405f, .408f, .5195f), Rect.MinMaxRect(-.033f, .2055f, .183f, .4845f),
        Rect.MinMaxRect(-.270f, .275f, -.054f, .525f), Rect.MinMaxRect(-.426f, .212f, -.286f, .428f),
    };
    static readonly Rect CorkAsBuilt = Rect.MinMaxRect(-.453f, .027f, .453f, .633f);
    const float FlyersFrontAsBuilt = .0146f;

    // CutawayWall's rule for what hangs on a wall (its constants), to say whether the board and its drawing fade.
    const float WallReach = .55f, MountedAbove = 1.14f, WallFlatGap = .05f, WallFlatDepth = .12f, WallFlatHeight = .35f;

    // ================================================================== menus

    [MenuItem(MenuCheck)]
    static void CheckFirst() => Shared.Check(CheckTitle, CheckLabel, run =>
    {
        if (Shared.Done(run, Title))
        {
            run.Info($"'{Title}' is already done in this scene: its Undo ({MenuUndo.Substring(Shared.Menu.Length)}) puts it back.");
            run.Line("  " + AcesCafeLayoutSetup.ValidateLayout());
            run.Line("  " + AcesCafeLayoutSetup.ValidateOccupiedCirculationV2());
            return;
        }
        Plan p = Survey(run);
        run.Line();
        run.Line("The guards now (the step compares them after: as many paths and places served, and two more of each):");
        run.Line("  " + p.layoutBefore);
        run.Line("  " + p.circulationBefore);
        run.Info($"After the step: {p.seatsBefore + p.seats.Count} seats; the layout check {p.pathsBefore + p.seats.Count}/{p.pathTargetsBefore + p.seats.Count} paths; " +
                 $"the occupied-circulation guard {p.servedBefore + p.seats.Count}/{p.placesBefore + p.seats.Count} places served.");
    });

    [MenuItem(MenuImport)]
    static void ImportThePieces() => Import();

    [MenuItem(MenuDo)]
    static void DressTheCafe()
    {
        Plan plan = null;
        Shared.Step(Title, Label, run => plan = Survey(run), run => Change(run, plan), run => Photos(run, plan));
    }

    [MenuItem(MenuUndo)]
    static void UndoTheDressing() => UndoDressing();

    [MenuItem(MenuCheck, true)]
    [MenuItem(MenuImport, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the manifest and the pieces as imported

    sealed class Manifest
    {
        public readonly Dictionary<string, Dictionary<string, object>> pieces = new Dictionary<string, Dictionary<string, object>>();
        public readonly Dictionary<string, Dictionary<string, object>> materials = new Dictionary<string, Dictionary<string, object>>();
        public readonly Dictionary<string, Dictionary<string, object>> existing = new Dictionary<string, Dictionary<string, object>>();
    }

    enum SolidKind { None, Box, Capsule }

    // A lived-in piece as imported: its one mesh's box about its origin (Unity axes, front +Z), measured; the manifest's
    // numbers; its solid (in the piece's own frame).
    sealed class Piece
    {
        public string name, path;
        public Dictionary<string, object> entry;
        public GameObject asset;
        public Mesh mesh;
        public Matrix4x4 toRoot;
        public Bounds local;
        public Vector3 built;
        public float bottom;
        public int tris, builtTris;
        public Material[] used = new Material[0];
        // The manifest's fits (the coat): a chair model's name -> the piece's local position and rotation on it.
        public readonly Dictionary<string, (Vector3 position, Vector3 euler)> fits = new Dictionary<string, (Vector3, Vector3)>();
        public SolidKind solid = SolidKind.None;
        public Vector3 solidCentre, solidSize;       // a box's size; a capsule's (radius, height, 0)
        public string solidWhy = "the manifest names none";
        public readonly List<string> problems = new List<string>();
        public float Across => local.size.x;
        public float Up => local.size.y;
        public float Deep => local.size.z;
    }

    static Manifest ReadManifest(out string problem)
    {
        problem = null;
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
        if (!File.Exists(file))
        {
            problem = $"{ManifestPath} isn't in the project. It is the Blender lead's: copy cafe_livedin.json and the LI_*.fbx into {Models} and the " +
                      $"LI_*.png into {Textures}, then Fixit Fidget > Checks > Pick up scripts changed from outside (reimport), then Lived in 1.";
            return null;
        }
        Dictionary<string, object> root;
        try { root = Json.Parse(File.ReadAllText(file)) as Dictionary<string, object>; }
        catch (Exception e) { problem = $"{ManifestPath} doesn't read as JSON: {e.Message}"; return null; }
        if (root == null || !root.TryGetValue("pieces", out object listed) || !(listed is List<object> list))
        {
            problem = $"{ManifestPath} has no pieces list (the kit's format: version, kit, note, pieces[], materials, existing_materials).";
            return null;
        }
        var m = new Manifest();
        foreach (object o in list)
            if (o is Dictionary<string, object> d && d.TryGetValue("name", out object n) && n is string s && s.Length > 0) m.pieces[s] = d;
        if (root.TryGetValue("materials", out object mats) && mats is Dictionary<string, object> md)
            foreach (KeyValuePair<string, object> kv in md)
                if (kv.Value is Dictionary<string, object> def) m.materials[kv.Key] = def;
        if (root.TryGetValue("existing_materials", out object ex) && ex is Dictionary<string, object> ed)
            foreach (KeyValuePair<string, object> kv in ed)
                m.existing[kv.Key] = kv.Value as Dictionary<string, object> ?? new Dictionary<string, object>();
        return m;
    }

    // The piece as imported, measured as Kit 1 measures (one mesh, its box about the root, upright, on its origin, its
    // materials mapped). problem: why it can't be used (null when it can; minor things go in piece.problems).
    static Piece Measure(Manifest m, string name, out string problem)
    {
        problem = null;
        if (!m.pieces.TryGetValue(name, out Dictionary<string, object> entry))
        {
            problem = $"{name} isn't in {ManifestPath} (the Blender lead's manifest names every piece this step places).";
            return null;
        }
        string path = Models + "/" + name + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            problem = $"{path} isn't in the project: copy the Blender lead's {name}.fbx into {Models}, reimport, then Lived in 1.";
            return null;
        }
        MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        if (filters.Length != 1)
        {
            problem = $"{name}: one mesh expected (the kit's rule), {filters.Length} found in {path}.";
            return null;
        }
        var p = new Piece { name = name, path = path, entry = entry, asset = asset, mesh = filters[0].sharedMesh };
        p.toRoot = asset.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
        p.local = Transformed(p.mesh.bounds, p.toRoot);
        long indices = 0;
        for (int i = 0; i < p.mesh.subMeshCount; i++) indices += p.mesh.GetIndexCount(i);
        p.tris = (int)(indices / 3);
        if (!TryVec(entry, "size", out p.built))
        {
            problem = $"{name}: the manifest gives no size [across, up, deep].";
            return null;
        }
        p.bottom = Num(entry, "bottom", 0f);
        p.builtTris = Mathf.RoundToInt(Num(entry, "tris", p.tris));
        Renderer r = filters[0].GetComponent<Renderer>();
        p.used = r != null ? r.sharedMaterials : new Material[0];
        bool sized = (p.local.size - p.built).magnitude < .012f;
        bool standing = Mathf.Abs(p.local.min.y - p.bottom) < .006f;
        // Upright: the mesh square on its root, and the root itself not turned or scaled by the import (Shared.Place sets
        // the root's rotation to the yaw alone, so a root turned by the axis conversion would lay the piece on its back).
        float turned = Quaternion.Angle(p.toRoot.rotation, Quaternion.identity) + Quaternion.Angle(asset.transform.localRotation, Quaternion.identity);
        bool upright = turned < .5f && (p.toRoot.lossyScale - Vector3.one).magnitude < .001f && (asset.transform.localScale - Vector3.one).magnitude < .001f;
        bool mapped = p.used.Length > 0 && p.used.All(x => x != null && AssetDatabase.Contains(x) && !AssetDatabase.GetAssetPath(x).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
        if (!upright) problem = $"{name} is imported turned or scaled ({F(turned, "0.0")} degrees off upright, scale {V(p.toRoot.lossyScale)}): the kit's export (Forward -Z, Up +Y, Apply Transform) and Lived in 1's import are expected.";
        else if (!mapped) problem = $"{name}'s materials aren't mapped to the project's ({string.Join(", ", p.used.Select(x => x == null ? "NONE" : x.name))}): run Lived in 1.";
        else if (!standing) problem = $"{name}'s bottom is at {F(p.local.min.y)} about its origin (the manifest: {F(p.bottom)}): the kit's rule is the origin at the bottom centre.";
        if (!sized) p.problems.Add($"its size as imported {V(p.local.size)} differs from the manifest's {V(p.built)} by {F((p.local.size - p.built).magnitude * 100f, "0.0")} cm (the step uses what it measures)");
        if (p.builtTris > 0 && Mathf.Abs(p.tris - p.builtTris) > Mathf.Max(2, p.builtTris / 200)) p.problems.Add($"{p.tris} triangles, the manifest says {p.builtTris}");
        ReadSolid(p);
        // "fits": {"CK_Chair_Cafe": {"local_position_unity": [x, y, z], "local_rotation": [x, y, z]}, ...} (the coat, built on the chair).
        if (entry.TryGetValue("fits", out object fo) && fo is Dictionary<string, object> fits)
            foreach (KeyValuePair<string, object> kv in fits)
                if (kv.Value is Dictionary<string, object> f && TryVec(f, "local_position_unity", out Vector3 at))
                    p.fits[kv.Key] = (at, TryVec(f, "local_rotation", out Vector3 e) ? e : Vector3.zero);
        return p;
    }

    // The manifest's `collider`, in the forms the kit's manifests use: an object {"centre": [x, y, z], "size": [x, y, z]}
    // (cafe_seating.json) or {"radius": r, "height": h} is used as given; words with three sizes ("a box 0.90 x 0.90 x 0.40",
    // across x up x deep, as cafe_props.json writes them) make a box standing on the piece's bottom, centred on its
    // footprint; "a capsule (r 0.30, h 1.00)" makes an upright capsule; "box" or "solid" without sizes means the mesh's own
    // box (said so); "none", false, an "if ..." (only if it is ever needed) or no field mean none.
    static void ReadSolid(Piece p)
    {
        p.solid = SolidKind.None;
        p.solidWhy = "the manifest names none";
        if (!p.entry.TryGetValue("collider", out object c) || c == null) return;
        Vector3 foot = new Vector3(p.local.center.x, 0f, p.local.center.z);
        if (c is Dictionary<string, object> d)
        {
            Vector3 centre;
            bool hasCentre = TryVec(d, "centre", out centre) || TryVec(d, "center", out centre);
            if (TryVec(d, "size", out Vector3 size))
            {
                if (!hasCentre) centre = new Vector3(foot.x, p.local.min.y + size.y * .5f, foot.z);
                p.solid = SolidKind.Box; p.solidCentre = centre; p.solidSize = size;
                p.solidWhy = $"the manifest's box (centre {V(centre)}, size {V(size)})";
            }
            else if (d.ContainsKey("radius") && d.ContainsKey("height"))
            {
                float r = Num(d, "radius", 0f), h = Num(d, "height", 0f);
                if (!hasCentre) centre = new Vector3(foot.x, p.local.min.y + h * .5f, foot.z);
                p.solid = SolidKind.Capsule; p.solidCentre = centre; p.solidSize = new Vector3(r, h, 0f);
                p.solidWhy = $"the manifest's capsule (r {F(r)}, h {F(h)})";
            }
            else
            {
                p.solid = SolidKind.Box; p.solidCentre = p.local.center; p.solidSize = p.local.size;
                p.solidWhy = "the manifest names a solid without its size: the mesh's own box";
            }
            return;
        }
        if (c is bool yes)
        {
            if (!yes) { p.solidWhy = "the manifest: false"; return; }
            p.solid = SolidKind.Box; p.solidCentre = p.local.center; p.solidSize = p.local.size;
            p.solidWhy = "the manifest says solid without a size: the mesh's own box";
            return;
        }
        if (!(c is string s)) return;
        string low = s.Trim().ToLowerInvariant();
        if (low.Length == 0 || low.StartsWith("none", StringComparison.Ordinal) || low == "no" || low.Contains("no collider")) { p.solidWhy = $"the manifest: \"{s}\""; return; }
        if (Regex.IsMatch(low, @"\bif\b")) { p.solidWhy = $"the manifest: \"{s}\" (only if it is ever needed: none here)"; return; }
        Match three = Regex.Match(low, @"(\d*\.?\d+)\s*[x×]\s*(\d*\.?\d+)\s*[x×]\s*(\d*\.?\d+)");
        Match radius = Regex.Match(low, @"\br(?:adius)?\s*=?\s*(\d*\.?\d+)"), height = Regex.Match(low, @"\bh(?:eight)?\s*=?\s*(\d*\.?\d+)");
        if ((low.Contains("capsule") || low.Contains("cylinder")) && radius.Success && height.Success)
        {
            float r = ParseF(radius.Groups[1].Value), h = ParseF(height.Groups[1].Value);
            p.solid = SolidKind.Capsule; p.solidCentre = new Vector3(foot.x, p.local.min.y + h * .5f, foot.z); p.solidSize = new Vector3(r, h, 0f);
            p.solidWhy = $"the manifest: \"{s}\" (an upright capsule r {F(r)}, h {F(h)} on its bottom)";
        }
        else if (three.Success)
        {
            var size = new Vector3(ParseF(three.Groups[1].Value), ParseF(three.Groups[2].Value), ParseF(three.Groups[3].Value));
            p.solid = SolidKind.Box; p.solidCentre = new Vector3(foot.x, p.local.min.y + size.y * .5f, foot.z); p.solidSize = size;
            p.solidWhy = $"the manifest: \"{s}\" (a box {V(size)} standing on its bottom)";
        }
        else if (low.Contains("box") || low.Contains("solid") || low.Contains("capsule"))
        {
            p.solid = SolidKind.Box; p.solidCentre = p.local.center; p.solidSize = p.local.size;
            p.solidWhy = $"the manifest: \"{s}\" (no sizes given: the mesh's own box)";
        }
        else p.solidWhy = $"the manifest: \"{s}\" (no solid named)";
    }

    // ================================================================== LIVED IN 1: import and check the pieces (no scene)

    static void Import()
    {
        var report = new StringBuilder(ImportTitle + "\n" + When() + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        void Info(string what) => report.AppendLine("      " + what);
        string folder = NewFolder(ImportLabel);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            if (!AssetDatabase.IsValidFolder(Models)) throw new InvalidOperationException($"{Models} is missing: Kit 1 hasn't run on this machine.");
            Manifest m = ReadManifest(out string problem);
            if (m == null) throw new InvalidOperationException(problem);
            Info($"{ManifestPath}: {m.pieces.Count} pieces, {m.materials.Count} material(s) defined, {m.existing.Count} existing one(s) named.");
            foreach (string need in AllPieces) Check(m.pieces.ContainsKey(need), $"{need} is in the manifest");
            foreach (string other in m.pieces.Keys.Where(k => !AllPieces.Contains(k))) Info($"{other}: in the manifest, not placed by this pass (imported all the same).");

            // ---- the textures the manifest's materials name (only the lived-in kit's own: LI_*) ----
            HashSet<string> kitOne = KitOneMaterials(out string kitOneNote);
            Info(kitOneNote);
            var configured = new List<string>();
            var leftAlone = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<string, Dictionary<string, object>> mat in m.materials)
                    foreach (string key in new[] { "texture", "normal" })
                    {
                        string tex = Str(mat.Value, key);
                        if (tex == null) continue;
                        string file = Path.GetFileNameWithoutExtension(tex);
                        if (!file.StartsWith("LI_", StringComparison.Ordinal) || kitOne.Contains(mat.Key)) { leftAlone.Add(Path.GetFileName(tex)); continue; }
                        bool there = AssetDatabase.LoadAssetAtPath<Texture2D>(tex) != null;
                        Check(there, $"{tex} is in the project ({mat.Key}'s {key})");
                        if (there && ConfigureTexture(tex, Bool(mat.Value, "transparent"))) configured.Add(Path.GetFileName(tex));
                    }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Info($"Textures set up: {configured.Count}{(configured.Count > 0 ? " (" + string.Join(", ", configured.Distinct()) + ")" : "")}" +
                 (leftAlone.Count > 0 ? $"; left as they are (not the lived-in kit's): {string.Join(", ", leftAlone.Distinct())}" : "") + ".");

            // ---- the materials ----
            Dictionary<string, Material> materials = ProjectMaterials();
            Check(materials.ContainsKey(GrainRemaps["CC_Wood_Counter"]), $"the café's grain is in {Palette} (\"{GrainRemaps["CC_Wood_Counter"]}\")");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader wasn't found.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialFolder)?.Replace('\\', '/'), Path.GetFileName(MaterialFolder));
            int made = 0, updated = 0, kept = 0;
            foreach (KeyValuePair<string, Dictionary<string, object>> mat in m.materials)
            {
                string name = mat.Key;
                if (kitOne.Contains(name))
                {
                    kept++;
                    Check(materials.ContainsKey(name), $"{name}: Kit 1's own material, used as it is{(materials.ContainsKey(name) ? "" : " (NOT FOUND: run Kit 1 first)")}");
                    continue;
                }
                bool ours = name.StartsWith("CF_", StringComparison.Ordinal) || name.StartsWith("LI_", StringComparison.Ordinal);
                if (!ours && materials.ContainsKey(name)) { kept++; Info($"{name}: the project's own material, used as it is."); continue; }
                string path = MaterialFolder + "/" + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                    made++;
                }
                else updated++;
                Define(material, mat.Value, lit, materials);
                EditorUtility.SetDirty(material);
                materials[name] = material;
            }
            AssetDatabase.SaveAssets();
            Info($"Materials: {made} made and {updated} updated in {MaterialFolder}; {kept} existing used as they are.");
            foreach (string name in m.existing.Keys)
            {
                string target = GrainRemaps.TryGetValue(name, out string gr) ? gr : name;
                Check(materials.ContainsKey(target), $"the existing material {name}{(target != name ? " (as \"" + target + "\")" : "")} is in the project");
            }

            // ---- the models ----
            var unmapped = new SortedSet<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<string, Dictionary<string, object>> piece in m.pieces)
                {
                    string path = Models + "/" + piece.Key + ".fbx";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Check(false, $"{path} is in the project"); continue; }
                    List<string> sources = piece.Value.TryGetValue("materials", out object ml) && ml is List<object> l ? l.OfType<string>().ToList() : new List<string>();
                    ConfigureModel(path, piece.Key, sources, materials, unmapped);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Check(unmapped.Count == 0, "every material in the FBX files maps to a project material" + (unmapped.Count > 0 ? " (not found: " + string.Join(", ", unmapped) + ")" : ""));

            // ---- each piece, as imported ----
            report.AppendLine();
            report.AppendLine("The pieces (metres: across, up, deep; the house's budgets: rugs and mats under 60 triangles, bins under 900, the rest under 300):");
            var pieces = new List<Piece>();
            int total = 0;
            foreach (string name in m.pieces.Keys)
            {
                Piece p = Measure(m, name, out string why);
                if (p == null) { Check(false, why); continue; }
                pieces.Add(p);
                total += p.tris;
                int budget = name.StartsWith("LI_Rug", StringComparison.Ordinal) || name == TillMat ? 60 : name == BinStation || name == StreetBin ? 900 : 300;
                Check(why == null, $"{name}: {p.tris} triangles{(p.builtTris != p.tris ? " (built " + p.builtTris + ")" : "")}, {V(p.local.size)} (built {V(p.built)}), " +
                                   $"bottom {F(p.local.min.y)}, materials: {string.Join(", ", p.used.Select(x => x == null ? "NONE" : x.name))}; solid: {p.solidWhy}" +
                                   (why != null ? ". " + why : "") + (p.problems.Count > 0 ? ". NOTE " + string.Join("; ", p.problems) : ""));
                if (p.tris > budget) Info($"NOTE {name}: {p.tris} triangles, over the house's {budget} for its kind (the Blender lead's audit decides).");
                if (name == Drawing) Check(p.Up > p.Deep, $"{name} stands upright, its face to +Z like the board's flyers ({F(p.Up)} up, {F(p.Deep)} deep)");
            }
            Info($"{pieces.Count} pieces, {total} triangles in all.");

            // ---- photos ----
            report.AppendLine();
            try { report.AppendLine(PhotographKit(folder, pieces)); }
            catch (Exception e) { Check(false, "the photos: " + e.Message); }

            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            Keep(ImportLabel, folder, (failures == 0 ? "Imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n\n" + report, failures > 0);
        }
        catch (Exception e)
        {
            Keep(ImportLabel, folder, "The import FAILED: " + e.Message + "\n\n" + report + "\n" + e, true);
        }
    }

    // Kit 1's materials (and the intake tray's CF_Felt): the names their manifests define.
    static HashSet<string> KitOneMaterials(out string note)
    {
        var names = new HashSet<string>();
        int read = 0;
        foreach (string manifest in KitOneManifests)
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Models, manifest));
            if (!File.Exists(file)) continue;
            try
            {
                if (Json.Parse(File.ReadAllText(file)) is Dictionary<string, object> root && root.TryGetValue("materials", out object mats) && mats is Dictionary<string, object> md)
                    foreach (string key in md.Keys) names.Add(key);
                read++;
            }
            catch (Exception) { }
        }
        note = $"Kit 1's manifests read: {read} of {KitOneManifests.Length} ({names.Count} materials of theirs, never redefined here).";
        return names;
    }

    // The project's materials by name: Assets/Art/Materials (the folder itself, Kit 1's rule), the kit's (CF_), the
    // furnishing pass's ("Mat - rubber"), and the café's palette (its grain).
    static Dictionary<string, Material> ProjectMaterials()
    {
        var result = new Dictionary<string, Material>();
        void AddFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != folder) continue;
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !result.ContainsKey(mat.name)) result[mat.name] = mat;
            }
        }
        AddFolder(ProjectMaterialFolder);
        AddFolder(MaterialFolder);
        AddFolder(FurnishingMaterials);
        foreach (Material mat in AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>())
            if (!result.ContainsKey(mat.name)) result[mat.name] = mat;
        return result;
    }

    // Kit 1's texture settings (CafeKitSteps.ConfigureTexture) for the lived-in kit's own: tiling colour and normal maps
    // repeat; a texture drawn for one face set (the rug, the mats, the labels, the drawing) clamps, sharp.
    static bool ConfigureTexture(string path, bool transparent)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        string file = Path.GetFileNameWithoutExtension(path);
        bool normal = file.EndsWith("_normal", StringComparison.Ordinal);
        bool tiling = normal || file.EndsWith("_color", StringComparison.Ordinal);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.anisoLevel = tiling ? 4 : 8;
        importer.maxTextureSize = tiling ? 1024 : 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = transparent;
        importer.SaveAndReimport();
        return true;
    }

    // Kit 1's material definition (CafeKitSteps.Define) without the window gilding's special case: URP Lit, metallic 0; a
    // textured one has a white base (the texture carries the colour) and its tiling; a transparent one blends; emission.
    static void Define(Material m, Dictionary<string, object> def, Shader lit, Dictionary<string, Material> project)
    {
        if (m.shader != lit) m.shader = lit;
        m.enableInstancing = true;
        Color colour = Hex(Str(def, "hex") ?? "FFFFFF");
        string texture = Str(def, "texture"), normal = Str(def, "normal"), emission = Str(def, "emission");
        float tiling = Num(def, "tiling", 1f);
        bool transparent = Bool(def, "transparent");
        Texture2D baseMap = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texture) : null;
        Texture2D bump = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
        m.SetTexture("_BaseMap", baseMap);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        Color baseColour = baseMap != null ? Color.white : colour;
        m.SetFloat("_Smoothness", Num(def, "smoothness", .3f));
        m.SetFloat("_Metallic", 0f);
        if (bump != null)
        {
            m.SetTexture("_BumpMap", bump);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_Cutoff", .4f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.SetFloat("_QueueOffset", 0f);
        if (transparent)
        {
            Material glass = project.TryGetValue("CF_Glass", out Material g) ? g : null;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = glass != null ? glass.renderQueue : (int)RenderQueue.Transparent;
            baseColour.a = baseMap != null ? 1f : .22f;
        }
        else
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = -1;
        }
        m.SetColor("_BaseColor", baseColour);
        if (emission != null)
        {
            m.SetColor("_EmissionColor", Hex(emission) * 1.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    // Kit 1's model settings (CafeKitSteps.Configure): scale 1, no animation, cameras or lights, readable (the step reads
    // the meshes), no colliders, normals imported; every material remapped by its Blender name (wood to the café's grain).
    static void ConfigureModel(string path, string piece, List<string> sources, Dictionary<string, Material> materials, SortedSet<string> unmapped)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = true;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var old in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            if (!sources.Contains(old.name)) importer.RemoveRemap(old);
        foreach (string source in sources)
        {
            string target = GrainRemaps.TryGetValue(source, out string g) ? g : source;
            if (materials.TryGetValue(target, out Material mat))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), mat);
            else unmapped.Add($"{piece}: {source} (wanted {target})");
        }
        importer.SaveAndReimport();
    }

    // The kit in a preview scene with the Unity materials: the floor pieces in a row, the table things on a 0.75 m table,
    // the coat fitted on each kit chair (the scene step's own fit) and the drawing on the notice board (its own choice of
    // place), and the bin station from a standing eye.
    static string PhotographKit(string folder, List<Piece> pieces)
    {
        Piece P(string n) => pieces.FirstOrDefault(q => q.name == n);
        int shots = 0;
        float binX = float.NaN;
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, 0f), new Vector3(40f, .02f, 40f), s.Flat(new Color(.62f, .58f, .52f)));
            float x = -4.4f;
            foreach (string n in new[] { RugCorner, RugEntry, TillMat, BinStation, StreetBin, Tote })
            {
                Piece p = P(n);
                if (p == null) continue;
                if (n == BinStation) binX = x + p.local.size.x * .5f;
                s.Place(p.asset, new Vector3(x + p.local.size.x * .5f - p.local.center.x, -p.local.min.y, -p.local.center.z), Quaternion.identity);
                x += p.local.size.x + .35f;
            }
            s.Primitive(PrimitiveType.Cube, "Table (photo)", new Vector3(0f, .375f, -2.2f), new Vector3(3.6f, .75f, .8f), s.Flat(new Color(.55f, .36f, .21f)));
            x = -1.6f;
            foreach (string n in new[] { Napkins, Condiments, LaptopOpen, LaptopClosed, LeftCup })
            {
                Piece p = P(n);
                if (p == null) continue;
                s.Place(p.asset, new Vector3(x + p.local.size.x * .5f - p.local.center.x, .75f - p.local.min.y, -2.2f - p.local.center.z), Quaternion.identity);
                x += p.local.size.x + .15f;
            }
            float cx = -2.4f;
            foreach (string chairName in new[] { "CK_Chair_Cafe", "CK_Chair_Spindle" })
            {
                GameObject chairAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + chairName + ".fbx");
                if (chairAsset == null) continue;
                GameObject chair = s.Place(chairAsset, new Vector3(cx, 0f, -4.4f), Quaternion.Euler(0f, 160f, 0f));
                Piece coat = P(Coat);
                if (coat != null && chair != null && FitCoat(chair.transform, chairName, coat, out Vector3 at, out Quaternion turn, out _, out _)) s.Place(coat.asset, at, turn);
                cx += 1.2f;
            }
            GameObject boardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/CK_NoticeBoard.fbx");
            if (boardAsset != null)
            {
                GameObject board = s.Place(boardAsset, new Vector3(1.4f, 1.15f, -4.4f), Quaternion.identity);      // its face (+Z) to the camera
                Piece drawing = P(Drawing);
                if (drawing != null && board != null && PlaceDrawing(board.transform, drawing, out Vector3 at, out Quaternion turn, out _, out _)) s.Place(drawing.asset, at, turn);
            }
            s.Shoot(Path.Combine(folder, "01-the-kit-from-52-degrees.png"), new Vector3(0f, 7.2f, 5.6f), new Vector3(0f, .3f, -2.0f), 55f); shots++;
            s.Shoot(Path.Combine(folder, "02-the-table-things-close.png"), new Vector3(0f, 1.45f, -.6f), new Vector3(0f, .8f, -2.2f), 50f); shots++;
            s.Shoot(Path.Combine(folder, "03-the-coats-on-the-chairs.png"), new Vector3(-1.8f, 1.35f, -2.6f), new Vector3(-1.8f, .65f, -4.4f), 50f); shots++;
            s.Shoot(Path.Combine(folder, "04-the-drawing-on-the-board.png"), new Vector3(1.4f, 1.48f, -3.3f), new Vector3(1.4f, 1.48f, -4.4f), 45f); shots++;
            if (!float.IsNaN(binX))
            {
                s.Shoot(Path.Combine(folder, "05-the-bin-station-from-a-standing-eye.png"), new Vector3(binX, 1.65f, 2.2f), new Vector3(binX, .55f, 0f), 50f);
                shots++;
            }
        }
        return $"Photos (a preview scene with the Unity materials; the café scene isn't touched): {shots}, in {folder}.";
    }

    sealed class Studio : IDisposable
    {
        readonly PreviewRenderUtility utility = new PreviewRenderUtility();
        readonly List<Object> made = new List<Object>();
        readonly Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        public Studio()
        {
            Camera cam = utility.camera;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.83f, .82f, .79f);
            utility.lights[0].intensity = 1.2f;
            utility.lights[0].color = new Color(1f, .95f, .88f);
            utility.lights[0].transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            utility.lights[1].intensity = .6f;
            utility.lights[1].transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            utility.ambientColor = new Color(.40f, .38f, .36f);
        }

        public Material Flat(Color c)
        {
            var m = new Material(lit) { color = c };
            made.Add(m);
            return m;
        }

        // A stand-in from Unity's built-in meshes (never GameObject.CreatePrimitive, which would land in the open scene).
        public GameObject Primitive(PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            go.transform.position = at;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            utility.AddSingleGO(go);
            return go;
        }

        public GameObject Place(GameObject asset, Vector3 at, Quaternion turn)
        {
            if (asset == null) return null;
            GameObject go = utility.InstantiatePrefabInScene(asset);
            go.transform.SetPositionAndRotation(at, turn);
            return go;
        }

        public void Shoot(string file, Vector3 from, Vector3 at, float fov)
        {
            Camera cam = utility.camera;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            utility.BeginStaticPreview(new Rect(0f, 0f, 1600f, 900f));
            utility.Render(true);
            utility.Render(true);
            Texture2D picture = utility.EndStaticPreview();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
        }

        public void Dispose()
        {
            utility.Cleanup();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
        }
    }

    // ================================================================== LIVED IN 2: the plan

    // Floor: on the floor in the open. Against: on the floor, its back (local -Z) set against a wall or the counter (the
    // 0.10 m rule isn't applied behind it). Top: on a table or the counter. OnChair, OnBoard: fitted to what holds it.
    enum Kind { Floor, Against, Top, OnChair, OnBoard }

    // One lived-in piece's place.
    sealed class Placement
    {
        public string label, name, subgroup, why, surface;
        public Piece piece;
        public Kind kind;
        public Transform parent;            // null: a group under "Lived in"
        public Vector2 at;                  // the footprint's middle (x, z)
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool active = true, solid;
        public float surfaceY;
        public Transform support;           // the top it stands on (Top)
        public Transform partner;           // what holds it (the coat's chair, the drawing's board): it may touch it
        public readonly List<Transform> near = new List<Transform>();   // what may stand within its 0.10 m (the table it's under, its seat's chair)
        public TableSeat seat;              // the seat it belongs to (laptops, bags, coats, left cups)
        public bool atCup;                  // it stands on its own seat's cup spot (a left cup)
        public string moved = "";           // how far the search moved it from the plan's first place
        public bool positioned;             // it has a place (the others keep their 0.10 m from it)
        public GameObject placed;
        public Matrix4x4 Pose => Matrix4x4.TRS(position, rotation, Vector3.one);
        public Bounds World => Transformed(piece.local, Pose);
    }

    // One of the old lounge's pieces, switched back on and moved (or the second chair, copied from the first).
    sealed class Lounge
    {
        public string label, name;
        public Transform t;
        public bool copy;
        public Vector3 position;
        public Quaternion rotation;
        public float heading;
        public Bounds drawn;                // its drawn box at the new pose (all its meshes)
        public Bounds solid;                // its box collider's world box at the new pose
        public Vector3 solidCentre, solidHalf;   // the box collider's centre (world) and half size (its own axes)
        public GameObject placed;
    }

    // One of the reading corner's two seats.
    sealed class CornerSeat
    {
        public string name;
        public Lounge chair;
        public Vector3 pose, cup, stand;
        public Quaternion Facing => Quaternion.LookRotation(Flat(cup - pose), Vector3.up);
        public Quaternion StandFacing => Quaternion.LookRotation(Flat(cup - stand), Vector3.up);
        public GameObject made;
        public TableSeat seat;
    }

    sealed class Plan
    {
        public Manifest manifest;
        public Scenery scene;
        public readonly HashSet<Renderer> movers = new HashSet<Renderer>();
        public readonly Dictionary<string, Piece> pieces = new Dictionary<string, Piece>();
        public readonly List<Placement> places = new List<Placement>();
        public readonly List<Lounge> lounge = new List<Lounge>();
        public readonly List<CornerSeat> seats = new List<CornerSeat>();
        public float floorY;
        public Lounge chairSouth, chairNorth, table, lamp;
        public Placement rug;
        public bool entryRug;
        public string entryWhy = "";
        public Transform board;
        public Transform shell;             // the room's walls, windows, sills and floor ("01 - room and windows")
        public CafeDaylight daylight;
        public int seatsBefore, placesBefore, servedBefore, pathsBefore, pathTargetsBefore;
        public string layoutBefore = "", circulationBefore = "";
        public TableSeat[] liveSeats = new TableSeat[0];
        public readonly List<(Vector3 at, float keep, string what)> people = new List<(Vector3, float, string)>();
        public readonly List<(Vector3 at, TableSeat seat)> cups = new List<(Vector3, TableSeat)>();
        public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        public IEnumerable<Placement> Off => places.Where(x => !x.active);
        public Placement Of(string piece) => places.FirstOrDefault(x => x.piece.name == piece);
    }

    // ================================================================== the survey (changes nothing; refuses with the reasons)

    static Plan Survey(Shared.Run run)
    {
        var p = new Plan();
        Physics.SyncTransforms();

        // ---- what it stands on ----
        foreach (string step in new[] { Bar1, Seats1, Room1, Street1 })
            run.Require(Shared.Done(run, step), $"'{step}' hasn't run in this scene: the lived-in pass dresses the café as the redesign built it.");
        run.Info("The redesign's steps in this scene: " + string.Join("; ", Shared.Records(run.Cafe).OrderBy(r => r.order).Select(r => $"{r.order}. {r.step}")) + ".");
        Transform floor = run.Scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Floor");
        run.Require(floor != null, "No 'Floor' at the scene's root (the café's floor, with its collider and the NavMeshSurface).");
        p.floorY = floor.position.y;
        p.shell = Shared.Find(run, ShellPath);
        run.Require(p.shell != null, $"'{ShellPath}' isn't under the café's root (the room's walls and windows).");
        CafeDaylight[] daylights = Shared.InScene<CafeDaylight>(run);
        run.Require(daylights.Length == 1, $"One CafeDaylight expected in the scene (the reading lamp's light registers with it); {daylights.Length} found.");
        p.daylight = daylights[0];

        // ---- the guards now, the numbers the step will compare with ----
        p.layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
        p.circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        Counted(p.layoutBefore, out p.pathsBefore, out p.pathTargetsBefore);
        Counted(p.circulationBefore, out p.servedBefore, out int places);
        run.Info($"The guards now: \"{p.layoutBefore}\"; \"{p.circulationBefore}\".");

        // ---- what to leave out of the measuring: the street's moving people and cars (CafeStreetSteps leaves them out) ----
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
                if (a != null && a.actor != null)
                    foreach (Renderer r in a.actor.GetComponentsInChildren<Renderer>(true)) p.movers.Add(r);
        p.scene = new Scenery(run, p.movers);
        run.Info($"The drawn scene read for the measuring: {p.scene.Count} renderers round the café (the street's {p.movers.Count} moving renderers left out).");

        // ---- the people's places: what everything keeps clear of ----
        PeoplesPlaces(run, p);
        run.Require(places >= 0 && p.pathTargetsBefore >= 0, $"A guard gave no count (\"{p.layoutBefore}\"; \"{p.circulationBefore}\"): look at it before dressing the café.");
        run.Require(p.placesBefore == places, $"The occupied-circulation guard counts {places} places; this step counts {p.placesBefore} (the same rule): something changed in the guard.");

        // ---- the kit ----
        run.Line();
        run.Line($"The lived-in kit ({ManifestPath}):");
        p.manifest = ReadManifest(out string problem);
        run.Require(p.manifest != null, problem);
        EntryRugDecision(run, p);
        foreach (string name in AllPieces)
        {
            if (name == RugEntry && !p.entryRug) { run.Info($"{name}: not placed ({p.entryWhy})."); continue; }
            Piece piece = Measure(p.manifest, name, out string why);
            run.Require(piece != null && why == null, why ?? $"{name} couldn't be measured.");
            p.pieces[name] = piece;
            // Only the bins stand on the floor with a solid; anything else the manifest gives a box keeps none in the scene.
            string solidText = piece.solid == SolidKind.None || name == BinStation || name == StreetBin ? piece.solidWhy
                : piece.solidWhy + ", NOT added: a piece on a table or the counter gets no collider (it would catch E's ray before the customer or the cup: the intake tray's reason, 10 Oct)";
            run.Info($"{name}: {V(piece.local.size)} m (across, up, deep; the manifest {V(piece.built)}), bottom {F(piece.local.min.y)}, {piece.tris} triangles; " +
                     $"solid: {solidText}" + (piece.problems.Count > 0 ? "; NOTE " + string.Join("; ", piece.problems) : "") + ".");
        }
        run.Require(p.pieces[Drawing].Up > p.pieces[Drawing].Deep, $"{Drawing} is built lying flat ({F(p.pieces[Drawing].Up)} up, {F(p.pieces[Drawing].Deep)} deep): " +
                                                                    "the step pins it on the board standing, its face to +Z (the kit's front), as the board's flyers are.");

        // ---- each part of the plan ----
        run.Line();
        run.Line("The reading corner:");
        PlanCorner(run, p);
        run.Line();
        run.Line("The mats:");
        PlanMats(run, p);
        run.Line();
        run.Line("Laptops, bags, coats and left cups (each a child of its seat, the coats of their chairs):");
        PlanSeatThings(run, p);
        run.Line();
        run.Line("The napkins:");
        PlanNapkins(run, p);
        run.Line();
        run.Line("The condiment station:");
        PlanCondiments(run, p);
        run.Line();
        run.Line("The bins:");
        PlanBinStation(run, p);
        PlanStreetBin(run, p);
        run.Line();
        run.Line("The kid's drawing:");
        PlanDrawing(run, p);

        // ---- every place, as finally chosen ----
        run.Line();
        run.Line("Every place (nothing passes through; on its floor or top within 2 mm; nothing standing within its footprint plus 0.10 m and no solid there; " +
                 "clear of the cup spots; a solid floor piece clear of people's places):");
        foreach (Placement x in p.places)
        {
            string why = x.positioned ? Problem(run, p, x) : "no place for it: every spot its rule allows has nothing under it, or its own seat wouldn't find it there";
            run.Check(why == null, $"{x.name}: {(why == null ? "free where it goes" : why)}{(x.moved.Length > 0 ? " (" + x.moved + ")" : "")}");
        }
        foreach (Lounge l in p.lounge) SurveyLounge(run, p, l);
        foreach (CornerSeat s in p.seats) SurveyStand(run, p, s);

        // ---- what must not move ----
        foreach (KeyValuePair<Transform, Vector3> a in Anchors(run)) p.anchors[a.Key] = a.Value;
        run.Info($"Gameplay anchors noted (checked after, 1 mm): {p.anchors.Count}.");

        run.Line();
        run.Line("The plan, piece by piece (world metres; yaw: degrees clockwise from north):");
        foreach (Placement x in p.places)
            run.Info($"{x.name}: footprint middle ({F(x.at.x, "0.00")}, {F(x.at.y, "0.00")}), box {Shared.BoundsText(x.World)}, yaw {F(Shared.Facing(x.rotation), "0")}" +
                     $"{(x.active ? "" : ", off")}, on {x.surface} at {F(x.surfaceY)}{(x.parent != null ? ", a child of " + x.parent.name : "")}: {x.why}");
        foreach (Lounge l in p.lounge)
            run.Info($"{l.name}: {V(l.position)}, its front heading {F(l.heading, "0")}{(l.copy ? " (a copy of the first)" : " (switched back on)")}, box {Shared.BoundsText(l.solid)}");
        foreach (CornerSeat s in p.seats)
            run.Info($"{s.name}: sit {V(s.pose)} facing {F(Shared.Facing(s.Facing), "0")}, cup {V(s.cup)}, stand {V(s.stand)}");
        return p;
    }

    // The first "N/M" in a guard's result.
    static void Counted(string result, out int done, out int of)
    {
        Match m = Regex.Match(result ?? "", @"(\d+)/(\d+)");
        done = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        of = m.Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
    }

    // Every place a person stands or a cup goes: what floor pieces keep clear of (CafeFurnishing's distances), what the new
    // seats keep clear of (Seats 1's), what table-top pieces keep off (the cup spots). The guard's places counted its way.
    static void PeoplesPlaces(Shared.Run run, Plan p)
    {
        p.liveSeats = Shared.InScene<TableSeat>(run, false).Where(s => s.isActiveAndEnabled).ToArray();
        WaitingSpot[] waits = Shared.InScene<WaitingSpot>(run, true).Where(w => !(w is TableSeat)).ToArray();
        Transform queue = run.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "CounterQueue");
        run.Require(queue != null, "No CounterQueue in the scene.");
        StationInteractable[] stations = Shared.InScene<StationInteractable>(run, false);
        foreach (TableSeat s in p.liveSeats)
        {
            p.people.Add((s.StandPoint.position, KeepSeat, s.name + "'s stand point"));
            p.cups.Add((s.CupSpot.position, s));
        }
        foreach (WaitingSpot w in waits)
            if (w.isActiveAndEnabled) p.people.Add((w.StandPoint.position, KeepWaiting, w.name));
        foreach (Transform q in queue) p.people.Add((q.position, KeepQueue, "the queue's " + q.name));
        foreach (StationInteractable st in stations) if (st.StandPoint != null) p.people.Add((st.StandPoint.position, KeepStation, $"the station \"{st.name}\"'s stand point"));
        foreach (GameObject g in run.Scene.GetRootGameObjects().Where(root => root.name == "SpawnPoint" || root.name == "PatronSpawnPoint"))
            p.people.Add((g.transform.position, KeepSpawn, "the " + g.name));
        p.seatsBefore = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Count(s => s.gameObject.scene == run.Scene);
        p.placesBefore = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude).Where(s => s.gameObject.scene == run.Scene).Select(s => s.StandPoint)
            .Concat(queue.Cast<Transform>()).Where(t => t != null).Distinct().Count();
        string off = string.Join(", ", waits.Where(w => !w.isActiveAndEnabled).Select(w => $"{w.name} {V(w.StandPoint.position)}"));
        run.Info($"People's places: {p.liveSeats.Length} seats, {waits.Count(w => w.isActiveAndEnabled)} waiting spots on ({(off.Length > 0 ? "off: " + off : "none off")}), " +
                 $"{queue.childCount} queue places, {stations.Length} stations; the guard's places now {p.placesBefore}.");
    }

    // ---------------------------------------------------------------- the entry rug: only where the mosaic isn't

    static void EntryRugDecision(Shared.Run run, Plan p)
    {
        Transform mosaic = Shared.Named(run, MosaicName).FirstOrDefault(t => t.gameObject.activeInHierarchy);
        Transform leaves = Shared.Find(run, DoorLeavesPath);
        // The doorway: between the open leaves' hinges (the scene's "Open door collision"), or the posts at x -1.2 / +1.2.
        float x0 = -1.2f, x1 = 1.2f;
        if (leaves != null && leaves.childCount >= 2)
        {
            float[] hinges = leaves.Cast<Transform>().Select(t => t.position.x).OrderBy(x => x).ToArray();
            x0 = hinges.First();
            x1 = hinges.Last();
        }
        if (mosaic == null)
        {
            p.entryRug = true;
            p.entryWhy = "no entry mosaic in the scene: the coir mat goes just inside the door";
            run.Info("The door's inside: " + p.entryWhy + ".");
            return;
        }
        Bounds b = Shared.RendererBounds(mosaic);
        float covered = Mathf.Max(0f, Mathf.Min(b.max.x, x1) - Mathf.Max(b.min.x, x0)) / Mathf.Max(.01f, x1 - x0);
        bool atThreshold = b.min.z <= .45f;
        p.entryRug = !(covered >= .9f && atThreshold);
        p.entryWhy = p.entryRug
            ? $"the mosaic covers {covered * 100f:0}% of the doorway (x {F(x0, "0.00")}..{F(x1, "0.00")}) from z {F(b.min.z, "0.00")}: a coir mat goes in front of it"
            : $"the mosaic already covers the door's inside ({Shared.BoundsText(b)}: {covered * 100f:0}% of the doorway x {F(x0, "0.00")}..{F(x1, "0.00")}, " +
              $"from {F(b.min.z, "0.00")} m inside the threshold), so no entry rug: a rug on it would hide the café's name, and two floor pieces at the door is the clutter the redesign took out";
        run.Info("The door's inside: " + p.entryWhy + ".");
    }

    // ---------------------------------------------------------------- the reading corner

    static void PlanCorner(Shared.Run run, Plan p)
    {
        Transform tub = Shared.Find(run, TubChairPath), table = Shared.Find(run, LoungeTablePath), lamp = Shared.Find(run, LampPath);
        run.Require(tub != null, $"'{TubChairPath}' isn't under the café's root (the old lounge's tub chair: Seats 1 switched it off on 10 Oct).");
        run.Require(table != null, $"'{LoungeTablePath}' isn't under the café's root (the old lounge's low table).");
        run.Require(lamp != null, $"'{LampPath}' isn't under the café's root (the old lounge's floor lamp).");
        foreach (Transform t in new[] { tub, table, lamp })
            run.Require(!t.gameObject.activeSelf, $"{Shared.PathOf(t)} is already switched on: something brought the lounge back already; look before running this.");
        CafeRedesignRecord seats1 = Shared.RecordOf(run.Cafe, Seats1);
        foreach (Transform t in new[] { tub, table, lamp })
        {
            bool held = seats1 != null && seats1.switchedOff.Any(o => o != null && o.target == t.gameObject);
            run.Info($"{Shared.PathOf(t)}: off{(held ? ", in Seats 1's record (it switched it off on 10 Oct)" : "")}; at {V(t.position)}.");
        }
        BoxCollider tubBox = tub.GetComponent<BoxCollider>(), tableBox = table.GetComponent<BoxCollider>(), lampBox = lamp.GetComponent<BoxCollider>();
        run.Require(tubBox != null && tableBox != null && lampBox != null, "The tub chair, the low table and the floor lamp each carry their solid (the furnishing pass's box): one is missing.");
        // The copy takes everything under the chair: a seat or a waiting spot there would be copied too (a third seat).
        WaitingSpot inside = tub.GetComponentsInChildren<WaitingSpot>(true).FirstOrDefault();
        run.Require(inside == null, $"The tub chair holds a seat or waiting spot ({(inside != null ? Shared.PathOf(inside.transform) : "")}): its copy would copy it too. Look before running this.");
        run.Info("Two tub chairs: the scene has one (\"Tub chair\"); the second is a copy of it (the same model, fabric and solid), made by this step and deleted by its Undo.");

        // The old lounge, measured: the chair's distance from the table; the old seat's sit point and cup relative to them.
        Transform oldSeatT = Shared.Find(run, OldTubSeatPath);
        TableSeat oldSeat = oldSeatT != null ? oldSeatT.GetComponent<TableSeat>() : null;
        float chairToTable = FlatDistance(tub.position, table.position);
        bool measured = chairToTable > .6f && chairToTable < 1.1f;
        if (!measured) chairToTable = OldChairToTable;
        // The chair's front, from the six axes of its root: the level one that points nearest the old table (the old seat
        // faced it).
        Vector3 toOldTable = Flat(table.position - tub.position).normalized;
        Vector3 frontLocal = Vector3.forward;
        float best = 180f;
        foreach (Vector3 axis in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            Vector3 w = tub.rotation * axis;
            if (Mathf.Abs(w.y) > .3f) continue;
            float a = Vector3.Angle(Flat(w), toOldTable);
            if (a < best) { best = a; frontLocal = axis; }
        }
        run.Require(best <= 30f, $"The tub chair's front couldn't be told from its root's axes (the nearest is {F(best, "0")} degrees off the old table).");
        float headingNow = Shared.Facing(Quaternion.LookRotation(Flat(tub.rotation * frontLocal), Vector3.up));
        run.Info($"The old lounge, measured: the chair {F(chairToTable, "0.00")} m from the table's centre{(measured ? "" : " (not readable: the plan's 0.83)")}; its front (its local {frontLocal}) " +
                 $"heads {F(headingNow, "0")}, {F(best, "0")} degrees off the table.");
        Vector3 seatLocal = new Vector3(0f, OldSeatUp, 0f) + Quaternion.Inverse(tub.rotation) * (Flat(tub.rotation * frontLocal).normalized * OldSeatAhead);
        if (oldSeat != null)
        {
            seatLocal = Quaternion.Inverse(tub.rotation) * (oldSeat.SeatPose.position - tub.position);
            run.Info($"The old seat ({Shared.PathOf(oldSeatT)}): sit {V(oldSeat.SeatPose.position)}, cup {V(oldSeat.CupSpot.position)}: its sit point carried over in the chair's own frame.");
        }
        else run.Info($"The old seat ({OldTubSeatPath}) isn't there: the sit point {F(OldSeatAhead, "0.00")} m ahead of the chair's centre, {F(OldSeatUp)} m over its root.");
        float cupFromTable = OldCupFromTable, cupUp = OldCupUp;
        if (oldSeat != null)
        {
            Vector3 c = oldSeat.CupSpot.position - table.position;
            if (Flat(c).magnitude < .45f) { cupFromTable = Flat(c).magnitude; cupUp = c.y; }
        }

        // The rug's height: the table stands on it.
        Piece rugPiece = p.pieces[RugCorner];
        float rugTop = p.floorY + rugPiece.Up;
        // The table: its long side across (x).
        Vector3 tableLong = LongestLevelAxis(table, tableBox, out float tableLongSize, out float tableShortSize);
        float tableHeadingNow = Shared.Facing(Quaternion.LookRotation(Flat(table.rotation * tableLong), Vector3.up));
        float turnTable = Mathf.DeltaAngle(tableHeadingNow, 90f);
        if (Mathf.Abs(Mathf.DeltaAngle(tableHeadingNow, 270f)) < Mathf.Abs(turnTable)) turnTable = Mathf.DeltaAngle(tableHeadingNow, 270f);
        p.table = new Lounge { label = "the low table", name = table.name, t = table };
        p.table.rotation = Quaternion.AngleAxis(turnTable, Vector3.up) * table.rotation;
        p.table.position = new Vector3(CornerTable.x, rugTop - BottomOffset(table, p.table.rotation), CornerTable.y);
        p.table.heading = Mathf.Repeat(tableHeadingNow + turnTable, 360f);
        run.Info($"The table: {F(tableLongSize, "0.00")} x {F(tableShortSize, "0.00")} m; turned {F(turnTable, "0")} degrees so its long side runs across; on the rug (its top at {F(rugTop)}).");

        // The chairs: the south one facing north (0), the north one facing south (180), each the old distance from the table.
        Lounge Chair(string label, string name, bool copy, float heading, float dz)
        {
            var l = new Lounge { label = label, name = name, t = tub, copy = copy, heading = heading };
            l.rotation = Quaternion.AngleAxis(Mathf.DeltaAngle(headingNow, heading), Vector3.up) * tub.rotation;
            l.position = new Vector3(CornerTable.x, p.floorY - BottomOffset(tub, l.rotation), CornerTable.y + dz);
            return l;
        }
        p.chairSouth = Chair("the south tub chair (the old lounge's)", tub.name, false, 0f, -chairToTable);
        p.chairNorth = Chair("the north tub chair (a copy of it)", "Tub chair (reading corner, north)", true, 180f, chairToTable);
        p.lamp = new Lounge { label = "the floor lamp", name = lamp.name, t = lamp, rotation = lamp.rotation, heading = Shared.Facing(lamp.rotation) };
        foreach (Lounge l in new[] { p.chairSouth, p.chairNorth, p.table, p.lamp }) p.lounge.Add(l);

        // The lamp: the plan's place, or the nearest few centimetres off it where it stands free.
        Vector3 lampSpot = new Vector3(CornerLamp.x, p.floorY - BottomOffset(lamp, lamp.rotation), CornerLamp.y);
        Vector2[] lampTries = { Vector2.zero, new Vector2(.05f, 0f), new Vector2(0f, -.05f), new Vector2(.05f, -.05f), new Vector2(-.05f, 0f), new Vector2(0f, .05f) };
        string lampFirst = null;
        foreach (Vector2 d in lampTries)
        {
            p.lamp.position = lampSpot + new Vector3(d.x, 0f, d.y);
            Measure(p.lamp);
            string why = LoungeProblem(run, p, p.lamp, false);
            if (why == null) { if (d != Vector2.zero) run.Info($"The lamp moved {F(d.magnitude * 100f, "0")} cm from the plan's place (there: {lampFirst})."); lampFirst = null; break; }
            if (lampFirst == null) lampFirst = why;
        }
        if (lampFirst != null) { p.lamp.position = lampSpot; Measure(p.lamp); }
        foreach (Lounge l in new[] { p.chairSouth, p.chairNorth, p.table }) Measure(l);

        // The rug: centred on the table, its long side along the chairs' line (north-south), on the floor; a few centimetres
        // east or west if the fig's pot (or anything else) stands within 0.10 m of it, the table and the chairs' fronts
        // staying on it.
        float rugYaw = rugPiece.Across >= rugPiece.Deep ? 90f : 0f;
        p.rug = NewPlace(p, rugPiece, "the reading corner's rug", "LI_Rug_Corner (reading corner)", CornerGroup, Kind.Floor, rugYaw, "the floor",
                         "under the low table and the chairs' fronts (their backs on the floor, the pile pressed under their front edges, as a flatweave is); its long side north-south");
        var rugSpots = new List<Vector2>();
        for (int i = 0; i <= Mathf.RoundToInt(RugSlide * 100f); i++) rugSpots.Add(new Vector2(CornerTable.x + i * .01f, CornerTable.y));
        for (int i = 1; i <= Mathf.RoundToInt(RugSlide * 100f); i++) rugSpots.Add(new Vector2(CornerTable.x - i * .01f, CornerTable.y));
        rugSpots = rugSpots.Where(at => RugHolds(p, at)).ToList();
        run.Require(rugSpots.Count > 0, $"The rug ({F(rugPiece.Across, "0.00")} x {F(rugPiece.Deep, "0.00")} m) can't hold the table with {F(RugBorder * 100f, "0")} cm to spare and both chairs' fronts {F(RugUnderFront * 100f, "0")} cm on it.");
        Settle(run, p, p.rug, rugSpots, at => FloorAt(p, at), "east or west of the table's centre");
        Bounds rugBox = p.rug.World;
        run.Info($"The rug: {Shared.BoundsText(rugBox)}{(p.rug.moved.Length > 0 ? " (" + p.rug.moved + ")" : "")}; the chairs' fronts on it by {F(p.chairSouth.drawn.max.z - rugBox.min.z, "0.00")} and {F(rugBox.max.z - p.chairNorth.drawn.min.z, "0.00")} m.");

        // The two seats: the old seat's sit point in each chair's frame; the cup on the table toward the chair; the stand points.
        CornerSeat Seat(string name, Lounge chair, Vector2 stand)
        {
            Vector3 toChair = Flat(chair.position - p.table.position).normalized;
            var s = new CornerSeat { name = name, chair = chair };
            s.pose = chair.position + chair.rotation * seatLocal;
            s.cup = new Vector3(p.table.position.x, p.table.position.y + cupUp, p.table.position.z) + toChair * cupFromTable;
            s.stand = new Vector3(CornerTable.x + stand.x, StandY, CornerTable.y + stand.y);
            return s;
        }
        p.seats.Add(Seat("Seat (reading corner, south tub chair)", p.chairSouth, CornerStandSouth));
        p.seats.Add(Seat("Seat (reading corner, north tub chair)", p.chairNorth, CornerStandNorth));
        foreach (CornerSeat s in p.seats)
            run.Info($"{s.name}: sit {V(s.pose)}, cup {V(s.cup)} ({F(FlatDistance(s.cup, s.pose), "0.00")} m ahead, {F((s.cup.y - s.pose.y) * 100f, "0")} cm up: a low table, " +
                     $"so the sofa clips play here as they did in the lounge), stand {V(s.stand)}.");
        run.Info($"The lamp: {V(p.lamp.position)}, by the window at the north chair's side; lit: a warm point light at its shade (#{ColorUtility.ToHtmlStringRGB(LampColour)}, " +
                 $"{F(LampIntensity, "0.0")} in the evening, range {F(LampRange, "0.0")}), one of CafeDaylight's warm lights (40 % by day, dimmed when closed).");
    }

    // Does the rug, its middle at `at`, hold the table with RugBorder to spare and both chairs' fronts RugUnderFront on it?
    static bool RugHolds(Plan p, Vector2 at)
    {
        Placement r = p.rug;
        Quaternion q = r.rotation;
        Vector3 mid = q * new Vector3(r.piece.local.center.x, 0f, r.piece.local.center.z);
        Bounds b = Transformed(r.piece.local, Matrix4x4.TRS(new Vector3(at.x - mid.x, 0f, at.y - mid.z), q, Vector3.one));
        Bounds t = p.table.drawn;
        return b.min.x <= t.min.x - RugBorder && b.max.x >= t.max.x + RugBorder && b.min.z <= t.min.z - RugBorder && b.max.z >= t.max.z + RugBorder
               && p.chairSouth.drawn.max.z - b.min.z >= RugUnderFront && b.max.z - p.chairNorth.drawn.min.z >= RugUnderFront;
    }

    // A lounge piece's boxes at its (new) pose: its meshes' and its box collider's.
    static void Measure(Lounge l)
    {
        Matrix4x4 pose = Matrix4x4.TRS(l.position, l.rotation, l.t.lossyScale);
        l.drawn = DrawnBoxAt(l.t, pose);
        BoxCollider box = l.t.GetComponent<BoxCollider>();
        l.solidCentre = pose.MultiplyPoint3x4(box.center);
        l.solidHalf = Vector3.Scale(box.size * .5f, AbsVec(l.t.lossyScale));
        l.solid = Transformed(new Bounds(box.center, box.size), pose);
    }

    // The longest level axis of a box collider's frame (the low table's long side): its local axis, sizes.
    static Vector3 LongestLevelAxis(Transform t, BoxCollider box, out float longSize, out float shortSize)
    {
        Vector3 best = Vector3.right;
        longSize = 0f;
        shortSize = float.MaxValue;
        foreach ((Vector3 axis, float size) in new[] { (Vector3.right, box.size.x * Mathf.Abs(t.lossyScale.x)), (Vector3.up, box.size.y * Mathf.Abs(t.lossyScale.y)), (Vector3.forward, box.size.z * Mathf.Abs(t.lossyScale.z)) })
        {
            if (Mathf.Abs((t.rotation * axis).y) > .3f) continue;
            if (size > longSize) { longSize = size; best = axis; }
            shortSize = Mathf.Min(shortSize, size);
        }
        return best;
    }

    // How far a thing's lowest drawn point lies from its root when turned to `rotation` (its meshes, even while it is off).
    static float BottomOffset(Transform t, Quaternion rotation) => DrawnBoxAt(t, Matrix4x4.TRS(Vector3.zero, rotation, t.lossyScale)).min.y;

    // The world box of a thing's meshes if its root stood at `pose` (works while it is switched off).
    static Bounds DrawnBoxAt(Transform root, Matrix4x4 pose)
    {
        bool any = false;
        Bounds result = new Bounds(pose.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
        Matrix4x4 toRoot = Matrix4x4.TRS(root.position, root.rotation, root.lossyScale).inverse;
        foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Renderer r = f.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;
            Bounds b = Transformed(f.sharedMesh.bounds, pose * toRoot * f.transform.localToWorldMatrix);
            if (!any) { result = b; any = true; }
            else result.Encapsulate(b);
        }
        return result;
    }

    // ---------------------------------------------------------------- the mats

    static void PlanMats(Shared.Run run, Plan p)
    {
        StationInteractable till = Shared.InScene<StationInteractable>(run, false).FirstOrDefault(s => s.name == "Counter" && s.StandPoint != null);
        run.Require(till != null, "No \"Counter\" station with a stand point (Ace's place at the till).");
        Transform counter = Shared.Named(run, CounterName).FirstOrDefault();
        run.Require(counter != null, $"'{CounterName}' isn't under the café's root (Bar 1's counter).");
        Bounds counterBox = Shared.RendererBounds(counter);
        Transform benchMat = Shared.Find(run, BenchMatPath), drinkMat = Shared.Find(run, DrinkMatPath);
        run.Require(benchMat != null && drinkMat != null, "The bench's and the drinks' rubber mats aren't where the furnishing pass put them (18 - cafe furnishing/F - Work areas).");
        Bounds benchMatBox = Shared.RendererBounds(benchMat);
        Piece mat = p.pieces[TillMat];
        Vector3 stand = till.StandPoint.position;
        // Its back to the counter (yaw 0: local -Z to the south), 2 cm off the counter's back; along x as far as Ace's stand
        // point stays 15 cm in from its ends.
        Placement x = NewPlace(p, mat, "the till mat", "LI_TillMat (Ace's place at the till)", "Mats", Kind.Against, 0f, "the staff floor",
                               $"under Ace's stand point at the till {V(stand)}, its back 2 cm off the counter's back (z {F(counterBox.max.z, "0.00")}), as the bench's and the drinks' mats lie");
        float z = counterBox.max.z + .02f + HalfAlong(mat, 0f, false);
        float half = HalfAlong(mat, 0f, true);
        var spots = new List<Vector2>();
        foreach (float dx in new[] { 0f, .02f, -.02f, .04f, -.04f, .06f, -.06f })
            if (half - Mathf.Abs(dx) >= .15f) spots.Add(new Vector2(stand.x + dx, z));
        Settle(run, p, x, spots, at => FloorAt(p, at), "along the counter");
        Bounds w = x.World;
        run.Check(stand.x >= w.min.x + .1f && stand.x <= w.max.x - .1f && stand.z >= w.min.z + .1f && stand.z <= w.max.z - .1f,
                  $"Ace's stand point at the till is on the mat, 10 cm or more in from its edges ({Shared.BoundsText(w)})");
        string look = string.Join(", ", mat.used.Select(m => m != null ? m.name : "NONE"));
        Renderer benchMatRenderer = benchMat.GetComponentInChildren<Renderer>(true);
        string theirs = benchMatRenderer != null ? string.Join(", ", benchMatRenderer.sharedMaterials.Select(m => m != null ? m.name : "NONE")) : "?";
        run.Info($"The till mat's look: {look}; the bench's and the drinks' mats: {theirs}{(look == theirs ? " (the same)" : " (its own black rubber: his eye on the photos)")}; " +
                 $"it lies on {F(x.surfaceY)}, the bench's mat on {F(benchMatBox.min.y)}{(Mathf.Abs(x.surfaceY - benchMatBox.min.y) <= .004f ? " (the same floor)" : " (NOTE: not the same height)")}.");

        if (p.entryRug)
        {
            Piece rug = p.pieces[RugEntry];
            Collider sill = Shared.Find(run, FrontLeftSillPath)?.GetComponent<Collider>();
            float inside = sill != null ? sill.bounds.max.z : 0f;
            Transform mosaicT = Shared.Named(run, MosaicName).FirstOrDefault(t => t.gameObject.activeInHierarchy);
            float from = mosaicT != null ? Shared.RendererBounds(mosaicT).max.z + .05f : inside + .05f;
            Placement e = NewPlace(p, rug, "the entry mat", "LI_Rug_Entry (inside the door)", "Mats", Kind.Floor, 0f, "the floor", "just inside the door, on the door's line");
            var entrySpots = new List<Vector2>();
            for (int i = 0; i <= 10; i++) entrySpots.Add(new Vector2(0f, from + HalfAlong(rug, 0f, false) + i * .02f));
            Settle(run, p, e, entrySpots, at => FloorAt(p, at), "further in");
        }
    }

    // ---------------------------------------------------------------- the napkins

    static void PlanNapkins(Shared.Run run, Plan p)
    {
        Piece n = p.pieces[Napkins];
        // A dispenser on a top: from `start`, slid toward `toward` (1 cm at a time, up to 0.25 m, up to 6 cm either side:
        // on the bistro 2-tops a left cup pushed 2 cm in from its spot leaves the dispenser 2 mm short of its 10 cm on the
        // centre line, and 4 cm to the other side clears it) to the first place where every rule holds; its two faces to
        // the two sitters (`yaw`).
        void OnTable(string tableName, string label, Vector2 start, Vector2 toward, float yaw, string why)
        {
            Transform t = Shared.Named(run, tableName).FirstOrDefault();
            run.Require(t != null, $"'{tableName}' isn't under the café's root (Seats 1's).");
            Placement x = NewPlace(p, n, "a napkin dispenser", $"LI_NapkinDispenser ({label})", "Napkins", Kind.Top, yaw, tableName, why);
            x.support = t;
            Vector2 side = new Vector2(-toward.y, toward.x);
            var spots = new List<Vector2>();
            for (int i = 0; i <= 25; i++)
                foreach (float s in new[] { 0f, .02f, -.02f, .04f, -.04f, .06f, -.06f })
                    spots.Add(start + toward * (i * .01f) + side * s);
            float topY = Shared.RendererBounds(t).max.y + .01f;
            Settle(run, p, x, spots, at => p.scene.TopAt(at, topY, t), "further in from the edge or along it");
        }
        // Behind the cups from the overhead camera (it looks north): at the table's north edge, in the middle.
        foreach (string which in new[] { "south", "middle", "north" })
        {
            string name = $"CK_Table_Square2 (banquette 2-top, {which})";
            Transform t = Shared.Named(run, name).FirstOrDefault();
            run.Require(t != null, $"'{name}' isn't under the café's root (Seats 1's).");
            Bounds b = Shared.RendererBounds(t);
            OnTable(name, $"banquette 2-top, {which}", new Vector2(b.center.x, b.max.z - EdgeMargin - HalfAlong(n, 90f, false)), Vector2.down, 90f,
                    "at the middle of the table's north edge, between the bench and the chair (behind the cups from the overhead camera), a face to each");
        }
        foreach (string which in new[] { "south", "north" })
        {
            string name = $"CK_Table_BistroRound (bistro 2-top, {which})";
            Transform t = Shared.Named(run, name).FirstOrDefault();
            run.Require(t != null, $"'{name}' isn't under the café's root (Seats 1's).");
            Bounds b = Shared.RendererBounds(t);
            OnTable(name, $"bistro 2-top, {which}", new Vector2(b.center.x, b.max.z - EdgeMargin - HalfAlong(n, 90f, false)), Vector2.down, 90f,
                    "at the round top's north edge, between the two chairs, a face to each");
        }
        // The communal table: on its centre line between its first and second pairs of stools, and its third and fourth.
        Transform communal = Shared.Named(run, CommunalName).FirstOrDefault();
        run.Require(communal != null, $"'{CommunalName}' isn't under the café's root.");
        Bounds cb = Shared.RendererBounds(communal);
        float[] rows = p.liveSeats.Where(s => s.name.StartsWith("Seat (communal table, east", StringComparison.Ordinal)).Select(s => s.CupSpot.position.z).OrderBy(z => z).ToArray();
        run.Require(rows.Length == 4, $"The communal table's east row: {rows.Length} seats found (4 expected: Seats 1's).");
        for (int i = 0; i < 2; i++)
        {
            float z = (rows[2 * i] + rows[2 * i + 1]) * .5f;
            OnTable(CommunalName, i == 0 ? "communal table, south" : "communal table, north", new Vector2(cb.center.x, z), i == 0 ? Vector2.up : Vector2.down, 90f,
                    $"on the table's centre line between its {(i == 0 ? "first and second" : "third and fourth")} pairs of stools, a face to each row");
        }
        // The window counter: toward the window, between the middle and north stools, its face to the stools (west).
        TableSeat mid = p.liveSeats.FirstOrDefault(s => s.name == "Seat (window counter, middle)"), north = p.liveSeats.FirstOrDefault(s => s.name == "Seat (window counter, north)");
        run.Require(mid != null && north != null, "The window counter's middle and north seats aren't there (Seats 1's).");
        Transform wc = Shared.Named(run, WindowCounterName).FirstOrDefault();
        run.Require(wc != null, $"'{WindowCounterName}' isn't under the café's root.");
        Bounds wb = Shared.RendererBounds(wc);
        OnTable(WindowCounterName, "window counter", new Vector2(wb.max.x - EdgeMargin - HalfAlong(n, 270f, true), (mid.CupSpot.position.z + north.CupSpot.position.z) * .5f), Vector2.left, 270f,
                "toward the window, between the middle and north stools, its face to the stools");
    }

    // Half a piece's extent along world x (alongX) or z when turned by yaw.
    static float HalfAlong(Piece piece, float yaw, bool alongX)
    {
        Bounds b = Transformed(piece.local, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
        return alongX ? b.extents.x : b.extents.z;
    }

    // ---------------------------------------------------------------- the condiment station

    static void PlanCondiments(Shared.Run run, Plan p)
    {
        Piece c = p.pieces[Condiments];
        Transform counter = Shared.Named(run, CounterName).FirstOrDefault();
        run.Require(counter != null, $"'{CounterName}' isn't under the café's root.");
        // The lowered section: where the counter's top, read by rays down onto its own collider at z 13.95 along its east
        // run, is at the lowered height (0.91, CafeBarSteps).
        var low = new List<float>();
        for (int i = 0; i <= 280; i++)
        {
            float x = 1.6f + i * .01f;
            RaycastHit? h = RayOnto(counter, new Vector3(x, 2f, 13.95f), 1.5f);
            if (h.HasValue && Mathf.Abs(h.Value.point.y - LowTop) < .03f) low.Add(x);
        }
        run.Require(low.Count >= 50, $"The counter's lowered section wasn't found on its east run (rays at z 13.95 found {low.Count} cm of top near {F(LowTop, "0.00")} m): Bar 1's counter is expected.");
        float x0 = low.Min(), x1 = low.Max();
        float top = low.Select(x => RayOnto(counter, new Vector3(x, 2f, 13.95f), 1.5f).Value.point.y).Average();
        float mid = (x0 + x1) * .5f;
        // Its customers' edge: the stone's edge (z 13.30, the bar lead's measurement), or further in if the drawn top starts later.
        float edge = CounterFrontEdge;
        for (float z = CounterFrontEdge; z <= CounterFrontEdge + .2f; z += .005f)
        {
            float y = p.scene.TopAt(new Vector2(mid, z), top + .01f, counter);
            if (!float.IsNegativeInfinity(y) && Mathf.Abs(y - top) <= OnSurface) { edge = z; break; }
        }
        float halfX = HalfAlong(c, 180f, true), halfZ = HalfAlong(c, 180f, false);
        run.Require(x1 - x0 >= 2f * (halfX + FreeMargin), $"The lowered section is {F(x1 - x0, "0.00")} m long: the condiment station ({F(2f * halfX, "0.00")} m) and 0.10 m each side don't fit.");
        Placement x2 = NewPlace(p, c, "the condiment station", "LI_CondimentStation (the counter's lowered section)", "Condiments", Kind.Top, 180f,
                                $"the counter's lowered section (x {F(x0, "0.00")}..{F(x1, "0.00")}, top {F(top)})",
                                "on the counter's lowered section, the stretch of the customers' side between the till's reach (to x 2.25) and the pastry case; its front to the " +
                                "customers, 5 cm in from the stone's edge. The water station's old place (x 3.9, the brief's) is the pastry case's since Bar 1");
        x2.support = counter;
        var spots = new List<Vector2>();
        foreach (float dx in new[] { 0f, .025f, -.025f, .05f, -.05f, .075f, -.075f })
        {
            float cx = mid + dx;
            if (cx - halfX < x0 + FreeMargin - .001f || cx + halfX > x1 - FreeMargin + .001f) continue;
            foreach (float dz in new[] { 0f, .02f, .04f }) spots.Add(new Vector2(cx, edge + EdgeMargin + halfZ + dz));
        }
        Settle(run, p, x2, spots, at => p.scene.TopAt(at, top + .01f, counter), "along the lowered section");
        // Clear of the till's reach and the stations' other reaches (their triggers), and of the intake's slots. A station's
        // trigger that encloses the counter itself under the piece (the counter station's reach zone: the whole counter and
        // the staff aisle behind it, floor to 1.0 m, where Ace's reach finds the station) is a zone round the station, not a
        // surface nothing may touch: nothing standing on the counter can be clear of it, and nothing on it needs to be (the
        // piece has no solid; a zone is read by Ace's reach sphere, never by what sits inside it). Said, and left out.
        Bounds w = x2.World;
        Vector3 under = new Vector3(w.center.x, w.min.y - .02f, w.center.z);
        foreach (StationInteractable st in Shared.InScene<StationInteractable>(run, false))
            foreach (Collider t in st.GetComponentsInChildren<Collider>(false).Where(k => k.isTrigger && k.enabled))
            {
                if (t.bounds.Contains(under))
                {
                    run.Info($"\"{st.name}\"'s reach {Shared.BoundsText(t.bounds)} encloses the counter under the condiment station (a zone round the station, " +
                             "not a surface): nothing on the counter can be clear of it; the piece has no solid, so it does nothing to it. Not a rule here.");
                    continue;
                }
                run.Check(Gap(t.bounds, w) >= .05f || t.bounds.max.y < w.min.y || t.bounds.min.y > w.max.y,
                          $"the condiment station is {F(Gap(t.bounds, w), "0.00")} m from the reach of \"{st.name}\" ({Shared.BoundsText(t.bounds)}; at least 0.05)");
            }
        foreach (ItemSlotArea area in Shared.InScene<ItemSlotArea>(run, false))
        {
            SerializedProperty slots = new SerializedObject(area).FindProperty("slots");
            if (slots == null || !slots.isArray) continue;
            for (int i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).objectReferenceValue is Transform slot)
                    run.Check(FlatGap(w, slot.position) >= .30f, $"the condiment station is {F(FlatGap(w, slot.position), "0.00")} m from {area.name}'s slot {i + 1} (a device's room: at least 0.30)");
        }
        Transform water = Shared.Named(run, WaterName).FirstOrDefault();
        Transform pastry = Shared.Find(run, CasePath);
        run.Info($"Beside it: the pastry case {(pastry != null ? Shared.BoundsText(Shared.RendererBounds(pastry)) : "(not found)")}; the water station {(water != null ? Shared.BoundsText(Shared.RendererBounds(water)) : "(not found)")}.");
    }

    // ---------------------------------------------------------------- the bins

    static void PlanBinStation(Shared.Run run, Plan p)
    {
        Piece bin = p.pieces[BinStation];
        Collider sillSolid = Shared.Find(run, FrontRightSillPath)?.GetComponent<Collider>();
        run.Require(sillSolid != null, $"'{FrontRightSillPath}' with its solid isn't there (the bulkhead the bin station backs onto).");
        float wallZ = sillSolid.bounds.max.z;
        Transform olive = Shared.Named(run, OliveEastName).FirstOrDefault();
        float leaves = olive != null ? Shared.RendererBounds(olive).max.x : 1.98f;
        float halfX = HalfAlong(bin, 0f, true);
        float startX = Mathf.Max(BinMinX, leaves + BinPastLeaves + halfX);
        float z = wallZ + BinOffWall + HalfAlong(bin, 0f, false);
        Placement x = NewPlace(p, bin, "the bin station", BinStation, "Bins", Kind.Against, 0f, "the floor",
                               $"against the front wall east of the door (its back {F(BinOffWall * 100f, "0")} cm off the bulkhead's solid at z {F(wallZ)}, or off the shopfront's " +
                               $"drawn joinery where that stands further into the room), past the east olive's leaves (x {F(leaves)}); its openings (its front) to the room; " +
                               "the NPC lead's leaving guests find it by this exact name");
        x.solid = bin.solid != SolidKind.None;
        // Nearest the wall first: the bulkhead's solid is what the routes know, but the shopfront's interior joinery (the
        // authored interior's FrontJoinery: ink, oak and sage, drawn without a solid) stands proud of it; the search steps
        // the bin into the room by 2 cm at a time until nothing drawn passes through it, trying the whole run east at each.
        var spots = new List<Vector2>();
        foreach (float off in new[] { 0f, .02f, .04f, .06f, .08f, .10f, .12f, .15f, .18f, .22f })
            for (int i = 0; i <= Mathf.RoundToInt(BinSearch / .05f); i++) spots.Add(new Vector2(startX + i * .05f, z + off));
        Settle(run, p, x, spots, at => FloorAt(p, at), "east along the wall or off it (the joinery)");
        if (x.positioned)
            run.Info($"The bin station's back at z {F(x.World.min.z)}: {F((x.World.min.z - wallZ) * 100f, "0")} cm off the bulkhead's solid" +
                     (x.World.min.z - wallZ > BinOffWall + .005f ? " (the shopfront's drawn joinery stands proud of the solid there: the bin stands off the joinery, not the solid)." : "."));
        // WaitSpots 5 and 6 (switched off since 23 Sept) kept clear all the same (the brief).
        foreach (WaitingSpot w in Shared.InScene<WaitingSpot>(run, true).Where(spot => !(spot is TableSeat) && (spot.name == "WaitSpot 5" || spot.name == "WaitSpot 6")))
        {
            float d = FlatGap(x.World, w.StandPoint.position);
            run.Check(d >= KeepWaiting, $"the bin station is {F(d, "0.00")} m from {w.name} ({(w.isActiveAndEnabled ? "on" : "off")}; at least {F(KeepWaiting, "0.00")})");
        }
        if (!x.solid) run.Info("NOTE the bin station's manifest gives it no collider: it stands without a solid (the routes go through it). The Blender lead's brief asks for a box.");
    }

    // The street bin: along the facade, the place nearest the door (east first) that keeps every street rule and every
    // rule of the step.
    static void PlanStreetBin(Shared.Run run, Plan p)
    {
        Piece bin = p.pieces[StreetBin];
        Collider left = Shared.Find(run, FrontLeftSillPath)?.GetComponent<Collider>(), right = Shared.Find(run, FrontRightSillPath)?.GetComponent<Collider>();
        run.Require(left != null && right != null, "The front bulkheads (Front left sill, Front right sill) with their solids aren't there.");
        float face = Mathf.Min(left.bounds.min.z, right.bounds.min.z);
        // Its front to the street (yaw 180): its back (local -Z) toward the facade, 2 cm off it.
        const float yaw = 180f;
        float cz = face - BinOffWall - HalfAlong(bin, yaw, false);
        var planters = Shared.Named(run, PlanterName, false).Where(t => t.gameObject.activeInHierarchy).Select(t => Shared.RendererBounds(t)).ToList();
        var street = new List<(Bounds b, string what)>();
        Transform patio = Shared.Find(run, PatioPath);
        if (patio != null) foreach (Renderer r in patio.GetComponentsInChildren<Renderer>(false)) street.Add((r.bounds, "the patio set"));
        Transform aframe = Shared.Find(run, AFramePath);
        if (aframe != null) street.Add((Shared.RendererBounds(aframe), "the patio A-frame"));
        Transform bench = Shared.Named(run, SidewalkBenchName).FirstOrDefault();
        if (bench != null) street.Add((Shared.RendererBounds(bench), "the sidewalk bench"));
        var leaves = new List<Bounds>();
        Transform doors = Shared.Find(run, DoorLeavesPath);
        if (doors != null) foreach (Collider c in doors.GetComponentsInChildren<Collider>(false)) leaves.Add(c.bounds);
        var spawns = run.Scene.GetRootGameObjects().Where(g => g.name == "SpawnPoint" || g.name == "PatronSpawnPoint").Select(g => g.transform.position).ToList();
        List<(string who, Vector3 a, Vector3 b)> walks = Walks(run), walkers = StreetRoutes(run, false), cars = StreetRoutes(run, true);
        run.Info($"The pavement's rules: {planters.Count} planters ({F(StreetPlanterGap, "0.00")} m), {street.Count} other street pieces ({F(StreetPasses, "0.00")} m: a person passes), " +
                 $"the spawn ({F(StreetSpawn, "0.0")} m), {leaves.Count} door leaves ({F(StreetLeaves, "0.0")} m) and the door lane (x -{F(StreetLaneHalf, "0.0")}..{F(StreetLaneHalf, "0.0")}), " +
                 $"{walks.Count} walk segments to the door ({F(StreetWalks, "0.0")} m), {walkers.Count} street walkers' ({F(StreetWalkers, "0.0")} m), {cars.Count} cars' ({F(StreetCars, "0.0")} m).");

        Placement x = NewPlace(p, bin, "the street bin", "LI_StreetBin (the pavement by the door)", "Bins", Kind.Against, yaw, "the pavement", "");
        x.solid = bin.solid != SolidKind.None;
        string firstFailure = null;
        for (int i = 0; i <= 108; i++)
            foreach (float side in new[] { 1f, -1f })
            {
                float cx = side * (1.6f + .05f * i);
                var at = new Vector2(cx, cz);
                float y = FloorAt(p, at);
                if (float.IsNegativeInfinity(y)) { if (firstFailure == null) firstFailure = $"x {F(cx, "0.00")}: no pavement under it"; continue; }
                Put(x, at, y);
                Bounds box = x.World;
                string why = StreetFailure(run, box, planters, street, spawns, leaves, walks, walkers, cars, face) ?? Problem(run, p, x);
                if (why != null)
                {
                    if (firstFailure == null || Mathf.Abs(cx) <= 2f) firstFailure = $"x {F(cx, "0.00")}: {why}";
                    continue;
                }
                x.why = $"on the pavement against the facade (its back {F(BinOffWall * 100f, "0")} cm off the bulkhead's street face at z {F(face)}), its opening to the street; " +
                        $"the nearest place to the door that keeps the street's rules ({(cx > 0f ? "east" : "west")} of the door, x {F(cx, "0.00")})";
                run.Info($"The street bin at x {F(cx, "0.00")}: every rule kept (planters, street pieces, spawn, door leaves and lane, walks, walkers, cars, and the step's own).");
                if (!x.solid) run.Info("NOTE the street bin's manifest gives it no collider: it stands without a solid. The Blender lead's brief asks for one.");
                return;
            }
        run.Require(false, "No place on the pavement along the facade keeps the street's rules for the street bin; the nearest to the door failed on " + firstFailure + ".");
    }

    static string StreetFailure(Shared.Run run, Bounds box, List<Bounds> planters, List<(Bounds b, string what)> street, List<Vector3> spawns, List<Bounds> leaves,
                                List<(string who, Vector3 a, Vector3 b)> walks, List<(string who, Vector3 a, Vector3 b)> walkers, List<(string who, Vector3 a, Vector3 b)> cars, float face)
    {
        if (box.max.x > -StreetLaneHalf && box.min.x < StreetLaneHalf) return "in the door lane";
        foreach (Bounds pl in planters) if (Gap(pl, box) < StreetPlanterGap) return $"{F(Gap(pl, box), "0.00")} m from a planter";
        foreach ((Bounds b, string what) in street) if (Gap(b, box) < StreetPasses) return $"{F(Gap(b, box), "0.00")} m from {what}";
        foreach (Vector3 s in spawns) if (FlatGap(box, s) < StreetSpawn) return $"{F(FlatGap(box, s), "0.00")} m from the spawn";
        foreach (Bounds l in leaves) if (Gap(l, box) < StreetLeaves) return $"{F(Gap(l, box), "0.00")} m from an open door leaf";
        foreach ((string who, Vector3 a, Vector3 b) in walks) if (BoxToSegment(box, a, b) < StreetWalks) return $"{F(BoxToSegment(box, a, b), "0.00")} m from the walk {who}";
        foreach ((string who, Vector3 a, Vector3 b) in walkers) if (BoxToSegment(box, a, b) < StreetWalkers) return $"{F(BoxToSegment(box, a, b), "0.00")} m from {who}'s path";
        foreach ((string who, Vector3 a, Vector3 b) in cars) if (BoxToSegment(box, a, b) < StreetCars) return $"{F(BoxToSegment(box, a, b), "0.00")} m from {who}'s path (a car)";
        // The facade right behind it (a ray from its back toward the wall meets the bulkhead within 5 cm).
        if (!Physics.Raycast(new Vector3(box.center.x, .4f, box.max.z - .05f), Vector3.forward, out RaycastHit wall, .2f, ~0, QueryTriggerInteraction.Ignore) || wall.point.z > face + .01f)
            return "no facade right behind it";
        return null;
    }

    // The walks to the café's door (CafeArrivals: the neighbours' doors, the car park), as CafeStreetSteps reads them.
    static List<(string, Vector3, Vector3)> Walks(Shared.Run run)
    {
        var segments = new List<(string, Vector3, Vector3)>();
        foreach (CafeArrivals arrivals in Shared.InScene<CafeArrivals>(run))
        {
            var routes = new List<CafeArrivals.Route>();
            if (arrivals.EditorFootRoutes != null) routes.AddRange(arrivals.EditorFootRoutes);
            if (arrivals.EditorLotToDoor != null) routes.Add(arrivals.EditorLotToDoor);
            foreach (CafeArrivals.Route r in routes)
                if (r != null && r.points != null)
                    for (int i = 0; i + 1 < r.points.Length; i++) segments.Add((r.name, r.points[i], r.points[i + 1]));
        }
        return segments;
    }

    // The street's actors' routes (CafeStreetSteps.Routes): walkers (no wheels) or cars; flyers (smooth routes) left out.
    static List<(string, Vector3, Vector3)> StreetRoutes(Shared.Run run, bool carsOnly)
    {
        var segments = new List<(string, Vector3, Vector3)>();
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
            {
                if (a == null || a.waypoints == null || a.smoothRoute) continue;
                bool car = a.wheels != null && a.wheels.Length > 0;
                if (car != carsOnly) continue;
                Transform[] w = a.waypoints.Where(t => t != null).ToArray();
                string who = a.actor != null ? a.actor.name : "an actor";
                for (int i = 0; i + 1 < w.Length; i++) segments.Add((who, w[i].position, w[i + 1].position));
                if (!a.openRoute && w.Length > 2) segments.Add((who, w[w.Length - 1].position, w[0].position));
            }
        return segments;
    }

    // ---------------------------------------------------------------- laptops, bags, coats, left cups

    static void PlanSeatThings(Shared.Run run, Plan p)
    {
        TableSeat Seat(string name)
        {
            TableSeat s = p.liveSeats.FirstOrDefault(x => x.name == name);
            run.Require(s != null, $"'{name}' isn't a live seat in the scene (Seats 1's).");
            return s;
        }
        // The sitter's frame at a seat: forward (to what they face), left.
        void Frame(TableSeat s, out Vector3 forward, out Vector3 left)
        {
            forward = Flat(s.FacingPoint - s.SeatPose.position).normalized;
            left = -Vector3.Cross(Vector3.up, forward).normalized;
        }
        Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        // The spots in the order to try them: the fewest other seats that would take the prop by the NPC lead's rule first
        // (NpcLifeProps: the nearest laptop within 0.9 m of a seat's cup spot, the nearest bag within 1.1 m of its sit
        // point), then nearest the plan's spot; only spots where its own seat finds it.
        List<Vector2> ByClaim(Placement x, TableSeat own, bool laptop, IEnumerable<Vector2> spots, float y) =>
            spots.Select((at, i) => (at, i, origin: OriginAt(x, at, y)))
                 .Where(c => Claims(own, c.origin, laptop))
                 .OrderBy(c => p.liveSeats.Count(o => o != own && Claims(o, c.origin, laptop))).ThenBy(c => c.i).Select(c => c.at).ToList();

        // ---- laptops (open and closed, at the same place) and the bag: off, children of the seat ----
        Piece open = p.pieces[LaptopOpen], closed = p.pieces[LaptopClosed], tote = p.pieces[Tote];
        // Both at the communal table, at its two far corners (east 1 by the door, west 4 at the other end on the other side):
        // its low stools have Snap To Seat on, so a laptop patron sits in front of the laptop. The window counter's bar
        // stools don't (people stand beside them: a patron would stand 1.3 m from an open laptop), its south stool is under
        // the photo ledge and its skirting is where the bag would go; so no laptop there until the bar stools get a perch
        // (the branch for it below stays).
        foreach ((string seatName, string topName, string label) in new[]
                 {
                     ("Seat (communal table, east 1)", CommunalName, "the communal table's east 1 (the stool nearest the door)"),
                     ("Seat (communal table, west 4)", CommunalName, "the communal table's west 4 (the far end, the other side)"),
                 })
        {
            TableSeat s = Seat(seatName);
            Transform top = Shared.Named(run, topName).FirstOrDefault();
            run.Require(top != null, $"'{topName}' isn't under the café's root.");
            Transform chair = SeatPiece(run, s);
            Frame(s, out Vector3 forward, out Vector3 left);
            float yaw = Shared.Facing(Quaternion.LookRotation(-forward, Vector3.up));          // its front (the keys) to the sitter
            float across = Mathf.Max(HalfAlongDir(open, yaw, left), HalfAlongDir(closed, yaw, left));
            float depth = Mathf.Max(HalfAlongDir(open, yaw, forward), HalfAlongDir(closed, yaw, forward));
            Vector3 c = s.CupSpot.position + left * (CupRadius + LaptopGapToCup + across);
            // Its near edge (the sitter's) at least 5 cm in from the top's near edge.
            Bounds tb = Shared.RendererBounds(top);
            float nearEdge = NearEdge(tb, forward);
            float inFromEdge = Vector3.Dot(c - forward * depth, forward) - nearEdge;
            if (inFromEdge < EdgeMargin) c += forward * (EdgeMargin - inFromEdge);
            string inner = Inner(s.name);
            float topY = tb.max.y + .01f;
            Placement first = null;
            foreach ((Piece piece, string name) in new[] { (open, $"{LaptopOpen} (seat {inner})"), (closed, $"{LaptopClosed} (seat {inner})") })
            {
                Placement x = NewPlace(p, piece, piece == open ? "a laptop, open (off)" : "a laptop, closed (off)", name, null, Kind.Top, yaw, topName,
                                       $"at {label}: on the sitter's left of the cup spot (the cup keeps its spot), its keys to the sitter; off until the NPC lead's laptop patron sits here");
                x.parent = s.transform;
                x.active = false;
                x.seat = s;
                x.support = top;
                if (first == null)
                {
                    var spots = new List<Vector2>();
                    foreach (float l in new[] { 0f, -.01f, .01f, -.02f, .02f, -.025f, .03f, .04f, .05f, .06f })
                        foreach (float f in new[] { 0f, .01f, .02f, .03f, -.01f })
                            spots.Add(XZ(c) + XZ(left) * l + XZ(forward) * f);
                    Settle(run, p, x, ByClaim(x, s, true, spots, s.CupSpot.position.y), at => p.scene.TopAt(at, topY, top), "beside the cup");
                    first = x;
                }
                else
                {
                    // The closed one where the open one is (the patron opens it there).
                    Settle(run, p, x, new[] { first.at }, at => p.scene.TopAt(at, topY, top), "");
                }
            }
            // The bag at the seat's foot, on the sitter's left: under the window counter against the wall; at the communal
            // stool's foot under the table's edge; out of every walk to and from the seats.
            Placement bag;
            var bagSpots = new List<Vector2>();
            if (topName == WindowCounterName)
            {
                const float bagYaw = 270f;      // its back to the wall (east)
                bag = NewPlace(p, tote, "a tote bag (off)", $"{Tote} (seat {inner})", null, Kind.Against, bagYaw, "the floor",
                               $"at {label}'s feet: under the counter against the wall, on the sitter's left; off until the laptop patron sits here");
                // Its back off the counter's wall edge by 2 cm, or further in where the wall's skirting (8 cm proud, to 0.26 m)
                // or a bracket's flange would pass through it: 2 cm at a time, up to 12 cm.
                float bx = tb.max.x - BinOffWall - HalfAlong(tote, bagYaw, true);
                for (int k = 0; k <= 6; k++)
                    for (int i = 0; i <= 8; i++) bagSpots.Add(new Vector2(bx - k * .02f, s.SeatPose.position.z + left.z * (.25f + i * .02f)));
            }
            else
            {
                float bagYaw = Shared.Facing(Quaternion.LookRotation(left, Vector3.up));
                bag = NewPlace(p, tote, "a tote bag (off)", $"{Tote} (seat {inner})", null, Kind.Floor, bagYaw, "the floor",
                               $"at {label}'s foot: 0.30 m to the sitter's left, under the table's edge; off until the laptop patron sits here");
                Vector3 b0 = s.SeatPose.position + forward * .25f + left * .30f;
                foreach (float l in new[] { 0f, .05f, -.05f })
                    foreach (float f in new[] { 0f, .05f, -.05f, .10f })
                        bagSpots.Add(XZ(b0) + XZ(left) * l + XZ(forward) * f);
            }
            bag.parent = s.transform;
            bag.active = false;
            bag.seat = s;
            bag.near.Add(top);
            if (chair != null) bag.near.Add(chair);
            Settle(run, p, bag, ByClaim(bag, s, false, bagSpots, p.floorY), at => FloorAt(p, at), "at the seat's foot");
            // What the NPC lead's rule makes of them (it finds props by distance, not by parent: see the report).
            foreach ((Placement prop, bool laptop) in new[] { (first, true), (bag, false) })
            {
                if (!prop.positioned) continue;
                List<string> others = p.liveSeats.Where(o => o != s && Claims(o, prop.position, laptop)).Select(o => o.name).ToList();
                run.Check(Claims(s, prop.position, laptop), $"{prop.name}: its seat finds it by the NPC lead's rule ({(laptop ? "within 0.9 m of the cup spot" : "within 1.1 m of the sit point")}: " +
                                                            $"{F(ClaimDistance(s, prop.position, laptop), "0.00")} m)");
                run.Info($"{prop.name}: other seats within the NPC lead's reach of it: {(others.Count == 0 ? "none" : string.Join(", ", others))}" +
                         (others.Count == 0 ? "" : " (a patron choosing one of those could open this one: the parent rule in the report fixes it)"));
            }
            if (!s.SnapToSeat)
                run.Info($"NOTE {s.name} has Snap To Seat off (Seats 1: people stand beside the bar stools): a laptop patron there would stand 1.3 m from the laptop. " +
                         "The NPC lead's choice: seat laptop patrons where Snap To Seat is on, or leave this laptop as decor that stays off.");
        }

        // ---- coats: on, over the backs of the two banquette café chairs people reach last ----
        Piece coat = p.pieces[Coat];
        Vector3 door = run.Scene.GetRootGameObjects().Where(g => g.name == "SpawnPoint").Select(g => g.transform.position).DefaultIfEmpty(new Vector3(0f, 0f, -1.2f)).First();
        StationInteractable till = Shared.InScene<StationInteractable>(run, false).FirstOrDefault(st => st.name == "Counter");
        Vector3 counterSide = new Vector3(till != null ? till.transform.position.x : 0f, 0f, 12.12f);
        var chairs = new List<(TableSeat seat, Transform chair, float reach)>();
        foreach (string which in new[] { "south", "middle", "north" })
        {
            TableSeat s = Seat($"Seat (banquette 2-top, {which}, chair)");
            Transform chair = Shared.Named(run, $"CK_Chair_Cafe (banquette 2-top, {which})").FirstOrDefault();
            run.Require(chair != null, $"'CK_Chair_Cafe (banquette 2-top, {which})' isn't under the café's root.");
            chairs.Add((s, chair, Mathf.Min(WalkTo(door, s.StandPoint.position), WalkTo(counterSide, s.StandPoint.position))));
        }
        run.Info("The banquette chairs' walks (the nearer of the door and the counter): " + string.Join(", ", chairs.Select(ch => $"{ch.chair.name} {F(ch.reach, "0.0")} m")) + ".");
        foreach ((TableSeat s, Transform chair, float reach) in chairs.OrderByDescending(ch => ch.reach).Take(2))
        {
            bool fits = FitCoat(chair, ModelName(chair), coat, out Vector3 at, out Quaternion turn, out string fit, out string bad);
            run.Check(fits, $"the coat fits {chair.name}'s back{(fits ? ": " + fit : ": " + bad)}");
            Placement x = NewPlace(p, coat, "a coat on a chair's back", $"{Coat} (seat {Inner(s.name)})", null, Kind.OnChair, 0f, chair.name,
                                   $"over the back of {chair.name} (its child, as the Blender lead built it): of the banquette's three café chairs, one of the two people reach last " +
                                   $"({F(reach, "0.0")} m from the door or the counter); {fit}");
            x.position = at;
            x.rotation = turn;
            x.at = new Vector2(at.x, at.z);
            x.surfaceY = at.y;
            x.positioned = true;
            x.parent = chair;
            x.seat = s;
            x.partner = chair;
        }
        run.Info("Coats on the two banquette chairs farthest to walk to: a coat reads as a taken seat, so they go where people sit least; children of their chairs " +
                 "(named for the seat), so they can follow who sits there later by the same name rule as the laptops.");

        // ---- left cups: off, on three seats' cup spots, children of their seats ----
        Piece cup = p.pieces[LeftCup];
        foreach ((string seatName, string words) in new[] { ("Seat (bistro 2-top, south, west)", "bistro 2-top, south"), ("Seat (banquette 2-top, middle, chair)", "banquette 2-top, middle"), ("Seat (communal table, west 2)", "communal table, west") })
        {
            TableSeat s = Seat(seatName);
            Vector3 c = s.CupSpot.position;
            Transform top = TopUnder(run, c);
            run.Require(top != null, $"Nothing solid under {s.name}'s cup spot {V(c)}.");
            Placement x = NewPlace(p, cup, "a left cup (off)", $"{LeftCup} (table {words})", null, Kind.Top, 25f, top.name,
                                   $"on {s.name}'s cup spot, where the guest's own cup stood (or a few centimetres in from it toward the table's middle, where the chair's " +
                                   "back crowds the spot: a cup left behind was pushed away); off until the NPC lead shows it for a moment after the guest leaves");
            x.parent = s.transform;
            x.active = false;
            x.seat = s;
            x.atCup = true;
            x.support = top;
            // The cup spot itself first; then 1 cm at a time toward the table's middle, up to 8 cm (the bistro 2-top's cup
            // spot is 16 cm in from a 0.72 m top's edge, and its spindle chair's back rises 2 cm short of that edge: the
            // cup's 10 cm of room, turned 25 degrees, reaches the chair's solid by a centimetre or two).
            var cupSpots = new List<Vector2> { new Vector2(c.x, c.z) };
            Vector3 inward = Flat(top.position - c);
            if (inward.sqrMagnitude > .0001f)
            {
                inward.Normalize();
                for (int k = 1; k <= 8; k++) cupSpots.Add(new Vector2(c.x + inward.x * k * .01f, c.z + inward.z * k * .01f));
            }
            Settle(run, p, x, cupSpots, at => p.scene.TopAt(at, c.y + .05f, top), "in from the cup spot toward the table's middle");
        }
    }

    // The NPC lead's rule (NpcLifeProps.LaptopFor / BagFor): a laptop belongs to a seat when its origin is within 0.9 m of the
    // seat's cup spot, a bag within 1.1 m of its sit point, the height counting half.
    static float ClaimDistance(TableSeat seat, Vector3 origin, bool laptop)
    {
        Vector3 d = origin - (laptop ? seat.CupSpot.position : seat.SeatPose.position);
        d.y *= .5f;
        return d.magnitude;
    }

    static bool Claims(TableSeat seat, Vector3 origin, bool laptop) => ClaimDistance(seat, origin, laptop) < (laptop ? .9f : 1.1f);

    // Where a placement's origin would be with its footprint's middle at `at` (as Put sets it), at height y.
    static Vector3 OriginAt(Placement x, Vector2 at, float y)
    {
        Vector3 mid = x.rotation * new Vector3(x.piece.local.center.x, 0f, x.piece.local.center.z);
        return new Vector3(at.x - mid.x, y, at.y - mid.z);
    }

    // The piece a seat sits on (Seats 1 names it after the seat: "Seat (communal table, east 3)" sits on
    // "CK_Stool_Low (communal table, east 3)", "Seat (banquette 2-top, south, chair)" on "CK_Chair_Cafe (banquette 2-top, south)").
    static Transform SeatPiece(Shared.Run run, TableSeat seat)
    {
        string inner = Inner(seat.name);
        string bare = inner.EndsWith(", chair", StringComparison.Ordinal) ? inner.Substring(0, inner.Length - ", chair".Length) : inner;
        return run.Cafe.GetComponentsInChildren<Transform>(false).FirstOrDefault(t => t.name.StartsWith("CK_", StringComparison.Ordinal) && (Inner(t.name) == inner || Inner(t.name) == bare));
    }

    // A piece's half extent along a world direction when turned by yaw.
    static float HalfAlongDir(Piece piece, float yaw, Vector3 dir)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 centre = new Vector3(piece.local.center.x, 0f, piece.local.center.z);
        float half = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? piece.local.min.x : piece.local.max.x, 0f, (i & 2) == 0 ? piece.local.min.z : piece.local.max.z);
            half = Mathf.Max(half, Mathf.Abs(Vector3.Dot(q * (corner - centre), dir)));
        }
        return half;
    }

    // A top's near edge (the sitter's side) as a distance along `forward`: the lowest of its box's corners along it.
    static float NearEdge(Bounds top, Vector3 forward)
    {
        float best = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? top.min.x : top.max.x, 0f, (i & 2) == 0 ? top.min.z : top.max.z);
            best = Mathf.Min(best, Vector3.Dot(corner, forward));
        }
        return best;
    }

    // Metres along the floor (the NavMesh as baked), or a straight line and a half when there is no route.
    static float WalkTo(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        if (NavMesh.SamplePosition(from, out NavMeshHit a, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out NavMeshHit b, .5f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }
        return FlatDistance(from, to) * 1.5f;
    }

    // ---------------------------------------------------------------- the coat on a chair's back

    // The coat on a chair's back. The manifest's own fit first: the Blender lead built the coat on the kit chairs' top
    // rails and gives its local pose on each chair model ("fits": on CK_Chair_Cafe at the chair's origin, unturned). For
    // any other chair, fitted from the two meshes: the coat's fold (its top tenth) laid on the chair's top rail (the
    // chair's top 15 %, as CafeSeatsSteps reads a backrest), its top a coat's thickness over the rail, centred across,
    // turned with the chair (or round, if it fits better that way). Either way, how much of it hangs where a sitter's back
    // goes (in front of the backrest, within its width, from a hand over the seat to the rail's foot) is measured; a fit
    // from the meshes with more than 5 % there is refused.
    static bool FitCoat(Transform chair, string model, Piece coat, out Vector3 at, out Quaternion turn, out string fit, out string problem)
    {
        at = chair.position;
        turn = chair.rotation;
        fit = "";
        problem = null;
        MeshFilter f = chair.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.sharedMesh != null);
        if (f == null) { problem = "the chair has no mesh"; return false; }
        Matrix4x4 toChair = Matrix4x4.TRS(chair.position, chair.rotation, Vector3.one).inverse * f.transform.localToWorldMatrix;
        Vector3[] v = f.sharedMesh.vertices.Select(x => toChair.MultiplyPoint3x4(x)).ToArray();
        Vector3[] built = coat.mesh.vertices.Select(x => coat.toRoot.MultiplyPoint3x4(x)).ToArray();
        if (v.Length == 0 || built.Length == 0) { problem = "a mesh can't be read"; return false; }
        float top = v.Max(x => x.y);
        Vector3[] rail = v.Where(x => x.y >= top * .85f).ToArray();
        float railZ = rail.Average(x => x.z), railFront = rail.Max(x => x.z), railBack = rail.Min(x => x.z);
        float railHalf = Mathf.Max(Mathf.Abs(rail.Min(x => x.x)), Mathf.Abs(rail.Max(x => x.x)));
        float seat = v.Where(x => x.z > railFront + .05f).Select(x => x.y).DefaultIfEmpty(.45f).Max();
        // The share of the coat's points (in the chair's frame, moved by `by`) where a sitter's back goes.
        float Share(Vector3[] cv, Vector3 by) =>
            (float)cv.Count(x => x.z + by.z > railFront + .06f && Mathf.Abs(x.x + by.x) < railHalf && x.y + by.y > seat + .05f && x.y + by.y < top - .12f) / cv.Length;

        if (model != null && coat.fits.TryGetValue(model, out (Vector3 position, Vector3 euler) given))
        {
            Quaternion q = Quaternion.Euler(given.euler);
            at = chair.TransformPoint(given.position);
            turn = chair.rotation * q;
            float share = Share(built.Select(x => given.position + q * x).ToArray(), Vector3.zero);
            fit = $"the manifest's fit for {model} (local {V(given.position)}, turned {V(given.euler)}), as the Blender lead built it on the chair's top rail; " +
                  $"{F(share * 100f, "0.0")} % of it where a sitter's back goes";
            return true;
        }
        float bestShare = float.MaxValue;
        foreach (bool round in new[] { false, true })
        {
            Vector3[] cv = round ? built.Select(x => new Vector3(-x.x, x.y, -x.z)).ToArray() : built;
            float ctop = cv.Max(x => x.y), cbottom = cv.Min(x => x.y);
            Vector3[] fold = cv.Where(x => x.y >= ctop - .1f * (ctop - cbottom)).ToArray();
            float foldZ = fold.Average(x => x.z);
            float lift = Mathf.Clamp(((fold.Max(x => x.z) - fold.Min(x => x.z)) - (railFront - railBack)) * .5f, .004f, .03f);
            float centreX = (cv.Min(x => x.x) + cv.Max(x => x.x)) * .5f;
            Vector3 local = new Vector3(-centreX, top + lift - ctop, railZ - foldZ);
            float share = Share(cv, local);
            if (share >= bestShare - 1e-4f) continue;
            bestShare = share;
            at = chair.TransformPoint(local);
            turn = round ? chair.rotation * Quaternion.Euler(0f, 180f, 0f) : chair.rotation;
            fit = $"fitted from the meshes ({model ?? "this chair"} isn't in the manifest's fits): its fold on the rail (the rail at z {F(railZ)} in the chair's frame, " +
                  $"the coat's top {F(lift * 1000f, "0")} mm over the chair's top at {F(top)}), {F(share * 100f, "0.0")} % of it where a sitter's back goes" +
                  (round ? " (turned round: it fits the chair better that way)" : "");
        }
        if (bestShare > .05f) { problem = $"{F(bestShare * 100f, "0")} % of the coat hangs where a sitter's back goes, either way round"; return false; }
        return true;
    }

    // The model a placed kit piece was made from ("CK_Chair_Cafe (banquette 2-top, south)" -> "CK_Chair_Cafe").
    static string ModelName(Transform t)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
        if (source != null) return source.name;
        int i = t.name.IndexOf(" (", StringComparison.Ordinal);
        return i > 0 ? t.name.Substring(0, i) : t.name;
    }

    // ---------------------------------------------------------------- the kid's drawing on the notice board

    static void PlanDrawing(Shared.Run run, Plan p)
    {
        p.board = Shared.Named(run, BoardName).FirstOrDefault(t => t.gameObject.activeInHierarchy);
        run.Require(p.board != null, $"'{BoardName}' isn't in the scene (Room 1's notice board).");
        run.Require((p.board.lossyScale - Vector3.one).magnitude < .01f, $"The notice board is scaled ({V(p.board.lossyScale)}): the drawing is measured against it at scale 1.");
        Piece d = p.pieces[Drawing];
        run.Require(PlaceDrawing(p.board, d, out Vector3 at, out Quaternion turn, out string why, out _), "The drawing has no place on the board's cork: " + why);
        Placement x = NewPlace(p, d, "the kid's drawing", "LI_KidsDrawing (pinned on the notice board)", null, Kind.OnBoard, 0f, p.board.name, why);
        x.position = at;
        x.rotation = turn;
        x.at = new Vector2(at.x, at.z);
        x.surfaceY = at.y;
        x.positioned = true;
        x.parent = p.board;
        x.partner = p.board;
        run.Info($"The drawing: {why}.");
        Transform wall = Shared.Find(run, RightWallPath);
        Bounds both = Shared.RendererBounds(p.board);
        both.Encapsulate(x.World);
        if (wall == null || wall.GetComponent<Renderer>() == null) run.Info("The east wall (Right plaster) wasn't found: whether the board fades with it is unknown.");
        else
        {
            bool fades = HangsOn(both, wall.GetComponent<Renderer>().bounds);
            run.Info($"The board hangs on the east wall (Right plaster), one of the overhead view's cut-away walls: the board and its drawing {(fades ? "fade with it" : "do NOT fade with it")} " +
                     "(CutawayWall judges a small group as one thing: the drawing, a child of the board, goes with it). The cut-away check's lists name back-wall pieces only: nothing to add.");
        }
    }

    // The board's flyers and cork measured off its own mesh (the materials CF_Flyer1..4, CF_Cork); the drawing where it
    // covers the least of them, inside the cork by 1 cm, lowest of equals (a child pins it low), tilted a little, 0.4 mm
    // in front of the last flyer.
    static bool PlaceDrawing(Transform board, Piece drawing, out Vector3 at, out Quaternion turn, out string why, out float overlap)
    {
        at = board.position;
        turn = board.rotation;
        overlap = 0f;
        var flyers = new List<Rect>();
        Rect cork = CorkAsBuilt;
        float front = FlyersFrontAsBuilt;
        bool measured = false;
        MeshFilter f = board.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.sharedMesh != null);
        Renderer r = f != null ? f.GetComponent<Renderer>() : null;
        if (f != null && r != null)
        {
            Matrix4x4 toBoard = Matrix4x4.TRS(board.position, board.rotation, board.lossyScale).inverse * f.transform.localToWorldMatrix;
            Mesh m = f.sharedMesh;
            Vector3[] v = m.vertices;
            Material[] mats = r.sharedMaterials;
            float flyerFront = float.MinValue;
            bool corkFound = false;
            for (int s = 0; s < m.subMeshCount && s < mats.Length && v.Length > 0; s++)
            {
                string n = mats[s] != null ? mats[s].name : "";
                bool flyer = n.StartsWith("CF_Flyer", StringComparison.Ordinal), isCork = n == "CF_Cork";
                if (!flyer && !isCork) continue;
                int[] idx = m.GetIndices(s);
                if (idx.Length == 0) continue;
                var b = new Bounds(toBoard.MultiplyPoint3x4(v[idx[0]]), Vector3.zero);
                foreach (int i in idx) b.Encapsulate(toBoard.MultiplyPoint3x4(v[i]));
                if (flyer) { flyers.Add(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y)); flyerFront = Mathf.Max(flyerFront, b.max.z); }
                else { cork = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y); corkFound = true; }
            }
            if (flyers.Count > 0 && corkFound) { front = flyerFront; measured = true; }
            else flyers.Clear();
        }
        if (!measured) flyers.AddRange(FlyersAsBuilt);
        Quaternion tilt = Quaternion.Euler(0f, 0f, DrawingTilt);
        Bounds db = drawing.local;
        Vector2 centre = new Vector2(db.center.x, db.center.y);
        float hx = 0f, hy = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = tilt * new Vector3(((i & 1) == 0 ? db.min.x : db.max.x) - centre.x, ((i & 2) == 0 ? db.min.y : db.max.y) - centre.y, 0f);
            hx = Mathf.Max(hx, Mathf.Abs(c.x));
            hy = Mathf.Max(hy, Mathf.Abs(c.y));
        }
        // Its push pin (the drawing's CF_Pin material; else its top middle) goes into bare cork, never through a flyer.
        Vector2 pin = new Vector2(centre.x, db.max.y - .015f);
        Material[] dm = drawing.used;
        for (int sm = 0; sm < drawing.mesh.subMeshCount && sm < dm.Length; sm++)
        {
            if (dm[sm] == null || !dm[sm].name.StartsWith("CF_Pin", StringComparison.Ordinal)) continue;
            int[] idx = drawing.mesh.GetIndices(sm);
            Vector3[] dv = drawing.mesh.vertices;
            if (idx.Length == 0 || dv.Length == 0) break;
            Vector3 sum = Vector3.zero;
            foreach (int i in idx) sum += drawing.toRoot.MultiplyPoint3x4(dv[i]);
            sum /= idx.Length;
            pin = new Vector2(sum.x, sum.y);
            break;
        }
        Vector2 pinFromCentre = (Vector2)(tilt * new Vector3(pin.x - centre.x, pin.y - centre.y, 0f));
        float bestScore = float.MaxValue;
        Vector2 best = Vector2.zero;
        bool found = false;
        for (float u = cork.xMin + .01f + hx; u <= cork.xMax - .01f - hx + 1e-4f; u += .005f)
            for (float w = cork.yMin + .01f + hy; w <= cork.yMax - .01f - hy + 1e-4f; w += .005f)
            {
                Rect mine = Rect.MinMaxRect(u - hx, w - hy, u + hx, w + hy);
                float covered = 0f;
                foreach (Rect fl in flyers) covered += Overlap(mine, fl);
                Vector2 pinAt = new Vector2(u, w) + pinFromCentre;
                bool pinOnFlyer = flyers.Any(sheet => sheet.Contains(pinAt));
                float score = covered + (pinOnFlyer ? 1f : 0f) + .001f * (w - cork.yMin) / Mathf.Max(.01f, cork.height);
                if (score < bestScore) { bestScore = score; best = new Vector2(u, w); overlap = covered; found = !pinOnFlyer; }
            }
        if (!found)
        {
            why = $"the drawing ({F(db.size.x, "0.00")} x {F(db.size.y, "0.00")} m, tilted {F(DrawingTilt, "0")} degrees) doesn't fit inside the cork ({F(cork.width, "0.00")} x {F(cork.height, "0.00")} m) " +
                  "with 1 cm to spare and its pin in bare cork";
            return false;
        }
        Vector3 local = new Vector3(best.x, best.y, 0f) - tilt * new Vector3(centre.x, centre.y, 0f);
        local.z = front + DrawingProud - db.min.z;
        at = board.TransformPoint(local);
        turn = board.rotation * tilt;
        float area = db.size.x * db.size.y;
        why = $"on the cork at ({F(best.x)}, {F(best.y)}) in the board's frame, tilted {F(DrawingTilt, "0")} degrees, its back {F((front + DrawingProud) * 1000f, "0.0")} mm in front of the board's back; " +
              $"it covers {F(overlap * 10000f, "0")} cm² of the flyers ({F(overlap / Mathf.Max(1e-6f, area) * 100f, "0")} % of its own area: the least anywhere on the cork " +
              "with its pin in bare cork)" +
              (measured ? "" : " (the flyers as built in cafe_signs.py: the board's mesh couldn't be read)");
        return true;
    }

    static float Overlap(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    // CutawayWall.Mounted's rule for one thing: close to the wall's faces, along it, above the cut, and either hung high
    // (its lowest point from 1.14 m) or flat against it. The cut (the sill) is taken at the wall's foot: the most lenient.
    static bool HangsOn(Bounds u, Bounds w)
    {
        bool normalX = w.size.x < w.size.z;
        float a = normalX ? u.min.x : u.min.z, e = normalX ? u.max.x : u.max.z;
        float s0 = normalX ? w.min.x : w.min.z, s1 = normalX ? w.max.x : w.max.z;
        float gap = Mathf.Max(0f, Mathf.Max(a - s1, s0 - e));
        float depth = Mathf.Max(0f, e - s1) + Mathf.Max(0f, s0 - a);
        float across = normalX ? u.size.x : u.size.z, along = normalX ? u.size.z : u.size.x, length = normalX ? w.size.z : w.size.x;
        if (gap > WallReach || across > 1.2f || along > length + .3f || u.size.y > 3.4f) return false;
        float cut = w.min.y;
        return u.min.y >= MountedAbove || gap <= WallFlatGap && depth <= WallFlatDepth && u.size.y >= WallFlatHeight && u.min.y >= cut - .05f;
    }

    // ---------------------------------------------------------------- a placement, and the search for a good one

    static Placement NewPlace(Plan p, Piece piece, string label, string name, string subgroup, Kind kind, float yaw, string surface, string why)
    {
        var x = new Placement { label = label, name = name, subgroup = subgroup, kind = kind, piece = piece, why = why, surface = surface, rotation = Quaternion.Euler(0f, yaw, 0f) };
        p.places.Add(x);
        return x;
    }

    // The footprint's middle at `at`, the bottom on `surfaceY`.
    static void Put(Placement x, Vector2 at, float surfaceY)
    {
        Vector3 mid = x.rotation * new Vector3(x.piece.local.center.x, 0f, x.piece.local.center.z);
        x.at = at;
        x.surfaceY = surfaceY;
        x.position = new Vector3(at.x - mid.x, surfaceY - x.piece.local.min.y, at.y - mid.z);
        x.positioned = true;
    }

    // The first of `spots` (footprint middles, in the order the piece's rule prefers) where nothing is wrong; if none, the
    // first spot (the survey then says what is wrong there).
    static bool Settle(Shared.Run run, Plan p, Placement x, IEnumerable<Vector2> spots, Func<Vector2, float> surface, string how)
    {
        Vector2? firstAt = null;
        float firstY = 0f;
        string firstWhy = null;
        foreach (Vector2 at in spots)
        {
            float y = surface(at);
            if (float.IsNegativeInfinity(y) || float.IsNaN(y))
            {
                if (firstWhy == null) firstWhy = $"nothing to stand on at ({F(at.x, "0.00")}, {F(at.y, "0.00")})";
                continue;
            }
            Put(x, at, y);
            string why = Problem(run, p, x);
            if (firstAt == null) { firstAt = at; firstY = y; if (why != null) firstWhy = why; }
            if (why == null)
            {
                if (at != firstAt.Value) x.moved = $"{F(Vector2.Distance(at, firstAt.Value) * 100f, "0")} cm {how} from its first place, where: {firstWhy}";
                return true;
            }
        }
        if (firstAt.HasValue) Put(x, firstAt.Value, firstY);
        return false;
    }

    // The floor's drawn top under a spot (up to 5 cm over the café's floor), or -Infinity.
    static float FloorAt(Plan p, Vector2 at) => p.scene.TopAt(at, p.floorY + .05f, null);

    // What is wrong with a placement where it is (null: nothing), the first problem found.
    static string Problem(Shared.Run run, Plan p, Placement x)
    {
        Bounds w = x.World;
        bool onSomething = x.kind == Kind.Floor || x.kind == Kind.Against || x.kind == Kind.Top;
        if (onSomething)
            foreach (Vector3 c in FootCorners(x, .02f, 0f))
            {
                float y = p.scene.TopAt(new Vector2(c.x, c.z), x.surfaceY + .03f, x.kind == Kind.Top ? x.support : null);
                if (float.IsNegativeInfinity(y) || Mathf.Abs(y - x.surfaceY) > OnSurface)
                    return $"not flat on {x.surface} at {F(x.surfaceY)} (under a corner: {(float.IsNegativeInfinity(y) ? "nothing" : F(y))})";
            }
        if (x.kind == Kind.Top && x.support != null)
            foreach (Vector3 c in FootCorners(x, 0f, EdgeMargin))
            {
                float y = p.scene.TopAt(new Vector2(c.x, c.z), x.surfaceY + .03f, x.support);
                if (float.IsNegativeInfinity(y) || Mathf.Abs(y - x.surfaceY) > OnSurface) return $"less than {F(EdgeMargin * 100f, "0")} cm in from {x.surface}'s edge";
            }
        if (x.kind == Kind.Top)
            foreach ((Vector3 at, TableSeat seat) in p.cups)
            {
                if (x.atCup && seat == x.seat) continue;
                float d = FlatGap(w, at);
                if (d < CupKeep) return $"{F(d)} m from {seat.name}'s cup spot (at least {F(CupKeep)})";
            }
        List<string> through = p.scene.Through(Shrunk(x.piece.local, .002f, .003f), x.Pose, t => IsUnder(t, x.partner));
        if (through.Count > 0) return "something drawn passes through where it goes: " + string.Join("; ", through.Take(4));
        if (x.kind != Kind.OnChair && x.kind != Kind.OnBoard)
        {
            List<string> near = Crowding(run, p, x);
            if (near.Count > 0) return $"within its footprint plus {F(FreeMargin, "0.00")} m: " + string.Join("; ", near.Take(4));
            string mine = OwnCrowding(p, x);
            if (mine != null) return mine;
        }
        if (x.solid && (x.kind == Kind.Floor || x.kind == Kind.Against) && w.max.z > -.05f)
        {
            string close = TooClose(p, w, false);
            if (close != null) return close;
        }
        return null;
    }

    // The brief's "nothing within the piece's footprint plus 0.1 m": nothing drawn standing on its floor or top there (from
    // 1 cm up to 0.30 m: leaves and table tops above may overhang) and no solid there at all, up to the piece's top. Not
    // grown at its back when it is set against a wall or the counter. What it stands on or belongs to, and the room's own
    // walls and windows (a piece may stand by a wall: nothing may pass through one), aside.
    static List<string> Crowding(Shared.Run run, Plan p, Placement x)
    {
        Bounds l = x.piece.local;
        float back = x.kind == Kind.Against ? 0f : FreeMargin;
        var slab = new Bounds();
        slab.SetMinMax(new Vector3(l.min.x - FreeMargin, l.min.y + .01f, l.min.z - back), new Vector3(l.max.x + FreeMargin, l.min.y + StandingHeight, l.max.z + FreeMargin));
        bool Aside(Transform t) => IsUnder(t, x.support) || IsUnder(t, x.partner) || IsUnder(t, p.shell) || x.near.Any(n => IsUnder(t, n)) || (run.Player != null && t.IsChildOf(run.Player));
        var found = p.scene.Through(slab, x.Pose, Aside);
        var full = new Bounds();
        full.SetMinMax(new Vector3(slab.min.x, l.min.y + .01f, slab.min.z), new Vector3(slab.max.x, Mathf.Max(l.max.y, l.min.y + .02f), slab.max.z));
        foreach (Collider c in Physics.OverlapBox(x.Pose.MultiplyPoint3x4(full.center), full.extents, x.rotation, ~0, QueryTriggerInteraction.Ignore))
            if (c != null && !Aside(c.transform)) found.Add(Shared.PathOf(c.transform) + " (solid)");
        return found.Distinct().ToList();
    }

    // The same 0.10 m between this step's own pieces (the laptops and bags are off, but they come on where they are): any
    // other piece already given a place whose box comes within 0.10 m of this one's at the same height. A seat's open and
    // closed laptops share their place (one is on at a time); the coats and the drawing are fitted to what holds them.
    static string OwnCrowding(Plan p, Placement x)
    {
        Bounds w = x.World;
        foreach (Placement y in p.places)
        {
            if (y == x || !y.positioned || y.kind == Kind.OnChair || y.kind == Kind.OnBoard) continue;
            bool laptops = x.seat != null && x.seat == y.seat && x.piece.name.StartsWith("LI_Laptop", StringComparison.Ordinal) && y.piece.name.StartsWith("LI_Laptop", StringComparison.Ordinal);
            if (laptops) continue;
            Bounds o = y.World;
            if (o.min.y > w.max.y || o.max.y < w.min.y) continue;
            float gap = Gap(o, w);
            if (gap < FreeMargin) return $"{F(gap, "0.00")} m from {y.name} (this step's own; at least {F(FreeMargin, "0.00")})";
        }
        return null;
    }

    // CafeFurnishing's keep-clear for a solid floor piece: from the seats' stand points, the waiting spots, the queue, the
    // stations' stand points and the spawn (and the reading corner's new stand points unless it is one of the corner's own
    // pieces); out of the door-to-counter lane. Null when clear.
    static string TooClose(Plan p, Bounds b, bool cornerPiece)
    {
        var close = new List<string>();
        foreach ((Vector3 at, float keep, string who) in p.people)
        {
            float d = FlatGap(b, at);
            if (d < keep) close.Add($"{who} {F(d, "0.00")} m (needs {F(keep, "0.00")})");
        }
        if (!cornerPiece)
            foreach (CornerSeat s in p.seats)
            {
                float d = FlatGap(b, s.stand);
                if (d < KeepSeat) close.Add($"{s.name}'s stand point {F(d, "0.00")} m (needs {F(KeepSeat, "0.00")})");
            }
        bool lane = b.max.x > DoorLane.xMin && b.min.x < DoorLane.xMax && b.max.z > DoorLane.yMin && b.min.z < DoorLane.yMax;
        if (lane) close.Add("in the door-to-counter lane (x -1..1, z -1.2..12.5)");
        return close.Count == 0 ? null : "too close to people's places: " + string.Join("; ", close);
    }

    // The footprint's corners of a placement at its bottom: `inset` in from its edges, or `out` beyond them on both axes.
    static IEnumerable<Vector3> FootCorners(Placement x, float inset, float outward)
    {
        Bounds l = x.piece.local;
        float d = outward - inset;
        foreach (Vector2 c in new[] { new Vector2(l.min.x - d, l.min.z - d), new Vector2(l.max.x + d, l.min.z - d), new Vector2(l.min.x - d, l.max.z + d), new Vector2(l.max.x + d, l.max.z + d) })
            yield return x.Pose.MultiplyPoint3x4(new Vector3(c.x, l.min.y, c.y));
    }

    // A lounge piece at its new pose: no solid there, nothing standing within 0.10 m of it (the floor aside, leaves above
    // may overhang), on the floor (2 mm; the table stands on the rug), clear of people's places. Null when fine.
    static string LoungeProblem(Shared.Run run, Plan p, Lounge l, bool onRug)
    {
        Vector3 up = Quaternion.Inverse(l.rotation) * Vector3.up;
        int v = Mathf.Abs(up.x) > .7f ? 0 : Mathf.Abs(up.y) > .7f ? 1 : 2;
        Vector3 half = l.solidHalf - Vector3.one * .01f;
        half[v] = l.solidHalf[v] - .015f;
        var there = Physics.OverlapBox(l.solidCentre + Vector3.up * .015f, half, l.rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c != null && !IsPlayer(run, c.transform) && !c.transform.IsChildOf(l.t)).Select(c => Shared.PathOf(c.transform)).Distinct().ToList();
        if (there.Count > 0) return "something solid where it goes: " + string.Join("; ", there.Take(4));
        var slab = new Bounds();
        slab.SetMinMax(new Vector3(l.drawn.min.x - FreeMargin, l.drawn.min.y + .012f, l.drawn.min.z - FreeMargin), new Vector3(l.drawn.max.x + FreeMargin, l.drawn.min.y + StandingHeight, l.drawn.max.z + FreeMargin));
        List<string> near = p.scene.Through(slab, Matrix4x4.identity, t => t.IsChildOf(l.t) || IsUnder(t, p.shell) || IsPlayer(run, t));
        if (near.Count > 0) return $"within its footprint plus {F(FreeMargin, "0.00")} m: " + string.Join("; ", near.Take(4));
        if (!onRug)
            foreach (Vector2 c in new[] { new Vector2(l.solid.min.x + .02f, l.solid.min.z + .02f), new Vector2(l.solid.max.x - .02f, l.solid.min.z + .02f), new Vector2(l.solid.min.x + .02f, l.solid.max.z - .02f), new Vector2(l.solid.max.x - .02f, l.solid.max.z - .02f) })
            {
                float y = FloorAt(p, c);
                if (float.IsNegativeInfinity(y) || Mathf.Abs(y - p.floorY) > OnSurface) return $"the floor under it isn't at {F(p.floorY)} (a corner: {(float.IsNegativeInfinity(y) ? "nothing" : F(y))})";
            }
        return TooClose(p, l.solid, true);
    }

    static void SurveyLounge(Shared.Run run, Plan p, Lounge l)
    {
        string why = LoungeProblem(run, p, l, l == p.table);
        run.Check(why == null, $"{l.label} at {V(l.position)}: {(why ?? "free where it goes (no solid, nothing standing within 0.10 m, on the floor, clear of people's places)")}");
    }

    // A new seat's stand point: on the floor, clear of every solid (the corner's own included) by 0.40 m, and of the other
    // seats, the waiting spots and the queue by Seats 1's claim distances.
    static void SurveyStand(Shared.Run run, Plan p, CornerSeat s)
    {
        RaycastHit? floor = Shared.Down(run, s.stand + Vector3.up * .5f, 1f);
        float solids = float.PositiveInfinity;
        string what = "";
        foreach (Collider c in Physics.OverlapBox(new Vector3(s.stand.x, 1f, s.stand.z), new Vector3(NavMargin + .05f, .9f, NavMargin + .05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c == null || IsPlayer(run, c.transform) || c.bounds.max.y < .05f) continue;
            float d = FlatGap(c.bounds, s.stand);
            if (d < solids) { solids = d; what = Shared.PathOf(c.transform); }
        }
        foreach (Lounge l in p.lounge)
        {
            float d = FlatGap(l.solid, s.stand);
            if (d < solids) { solids = d; what = l.label; }
        }
        run.Check(floor.HasValue && Mathf.Abs(floor.Value.point.y - p.floorY) <= .02f && solids >= NavMargin,
                  $"{s.name}: its stand point {V(s.stand)} on the floor, {F(solids, "0.00")} m from the nearest solid ({what}; at least {F(NavMargin, "0.00")})");
        float seats = p.liveSeats.Select(t => FlatDistance(t.StandPoint.position, s.stand)).Concat(p.seats.Where(o => o != s).Select(o => FlatDistance(o.stand, s.stand)))
                       .DefaultIfEmpty(float.PositiveInfinity).Min();
        float waits = p.people.Where(x => x.keep == KeepWaiting).Select(x => FlatDistance(x.at, s.stand)).DefaultIfEmpty(float.PositiveInfinity).Min();
        float queue = p.people.Where(x => x.keep == KeepQueue).Select(x => FlatDistance(x.at, s.stand)).DefaultIfEmpty(float.PositiveInfinity).Min();
        run.Check(seats >= SeatClaim && waits >= SeatToWaiting && queue >= SeatToQueue,
                  $"{s.name}: {F(seats, "0.00")} m from the nearest seat's stand point (at least {F(SeatClaim, "0.00")}), {F(waits, "0.00")} m from the nearest waiting spot " +
                  $"(at least {F(SeatToWaiting, "0.00")}), {F(queue, "0.00")} m from the queue (at least {F(SeatToQueue, "0.00")})");
    }

    // ================================================================== the change

    static void Change(Shared.Run run, Plan p)
    {
        if (p == null) throw new InvalidOperationException("The survey didn't run.");

        // 1. The group, Not Walkable for the routes (as Seats 1's): no table top, cushion or bin becomes a step.
        Transform group = Shared.StepGroup(run, GroupName);
        NavMeshModifier modifier = group.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = Shared.AddComponent<NavMeshModifier>(run, group.gameObject);
        Undo.RecordObject(modifier, run.Title);
        modifier.overrideArea = true;
        modifier.area = 1;                 // Not Walkable
        modifier.ignoreFromBuild = false;
        modifier.applyToChildren = true;

        // 2. The reading corner: the rug first (the table stands on it); the old lounge's pieces back on and moved here; the
        //    second chair copied from the first; the two seats; the lamp's light, one of CafeDaylight's warm lights.
        Transform corner = Shared.Group(run, group, CornerGroup);
        Place(run, group, p.rug);
        foreach (Lounge l in new[] { p.chairSouth, p.table, p.lamp })
        {
            GameObject go = l.t.gameObject;
            Undo.RecordObject(go, run.Title);
            go.SetActive(true);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            Shared.Move(run, l.t, l.position, l.rotation, OnMark + $"{l.label}, into the reading corner (Seats 1 switched it off with the lounge on 10 Oct)", corner);
            l.placed = go;
        }
        GameObject copy = Object.Instantiate(p.chairSouth.t.gameObject, corner);
        copy.name = p.chairNorth.name;
        copy.transform.SetPositionAndRotation(p.chairNorth.position, p.chairNorth.rotation);
        copy.SetActive(true);
        Shared.Made(run, copy);
        p.chairNorth.placed = copy;
        p.chairNorth.t = copy.transform;
        foreach (CornerSeat s in p.seats) MakeSeat(run, corner, s);
        Light light = Shared.MakeLight(run, p.lamp.t, "Reading lamp light (the corner's floor lamp)", p.lamp.position + Vector3.up * LampShadeHeight, Quaternion.identity,
                                       LightType.Point, LampColour, LampIntensity, LampRange);
        Shared.AddToList(run, p.daylight, "warmLights", light, "the reading corner's floor lamp: dimmed by day and while closed, like the café's other lamps");

        // 3. Everything else, each at its place.
        foreach (Placement x in p.places.Where(other => other != p.rug)) Place(run, group, x);

        // 4. The bin station's drop point: where a leaving guest can stand to drop a cup in (on the room's side, 0.45 m out).
        Placement bins = p.Of(BinStation);
        Bounds bw = bins.World;
        var drop = new GameObject(DropPointName);
        drop.transform.SetParent(bins.placed.transform, false);
        Vector3 front = bins.rotation * Vector3.forward;
        Vector3 dropAt = new Vector3(bw.center.x, StandY, bw.center.z) + front * (Vector3.Dot(bw.extents, AbsVec(front)) + DropAhead);
        drop.transform.SetPositionAndRotation(dropAt, Quaternion.LookRotation(-front, Vector3.up));
        Undo.RegisterCreatedObjectUndo(drop, run.Title);

        Physics.SyncTransforms();
        Shared.Note(run, $"Lived in: the reading corner (two seats: the guard's places {p.placesBefore} -> {p.placesBefore + p.seats.Count}), {p.places.Count} pieces placed " +
                         $"({p.Off.Count()} off until people use them); the old lounge's tub chair, low table and floor lamp switched back on and moved; the entry rug " +
                         $"{(p.entryRug ? "placed" : "not placed: " + p.entryWhy)}.");

        // 5. The checks.
        After(run, p);
    }

    // A piece placed: its group (or its parent: a seat, the board), its pose, on or off, its solid on the object that holds
    // its mesh (as the sidewalk bench's: Night walk 2's list then counts it solid on its own).
    static void Place(Shared.Run run, Transform group, Placement x)
    {
        Transform parent = x.parent != null ? x.parent : Shared.Group(run, group, x.subgroup ?? "Other");
        GameObject go = Shared.Place(run, x.piece.name, parent, x.position, 0f, x.name);
        go.transform.SetPositionAndRotation(x.position, x.rotation);
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        if (x.solid && x.piece.solid != SolidKind.None)
        {
            MeshFilter f = go.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.sharedMesh != null);
            GameObject holder = f != null ? f.gameObject : go;
            Vector3 centre = holder.transform.InverseTransformPoint(go.transform.TransformPoint(x.piece.solidCentre));
            if (x.piece.solid == SolidKind.Box)
            {
                BoxCollider box = Shared.AddComponent<BoxCollider>(run, holder);
                Undo.RecordObject(box, run.Title);
                box.center = centre;
                box.size = x.piece.solidSize;
                box.isTrigger = false;
            }
            else
            {
                CapsuleCollider capsule = Shared.AddComponent<CapsuleCollider>(run, holder);
                Undo.RecordObject(capsule, run.Title);
                capsule.center = centre;
                capsule.radius = x.piece.solidSize.x;
                capsule.height = x.piece.solidSize.y;
                capsule.direction = 1;
                capsule.isTrigger = false;
            }
        }
        if (!x.active)
        {
            go.SetActive(false);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }
        x.placed = go;
    }

    // A seat in the reading corner, the way Seats 1 makes one (CafeSeatsSteps.MakeSeat): a TableSeat on the floor under its
    // sit point, its SeatPose, CupSpot and StandPoint. Chair, Snap To Seat on, drain 0.6 (every seat's: the lounge's 0.55
    // was a hidden "comfier" number a layout would buy, and layout is cosmetic, Bible §2).
    static void MakeSeat(Shared.Run run, Transform corner, CornerSeat s)
    {
        var go = new GameObject(s.name);
        go.transform.SetParent(corner, false);
        go.transform.SetPositionAndRotation(new Vector3(s.pose.x, 0f, s.pose.z), s.Facing);
        Transform pose = Anchor(go.transform, "SeatPose", s.pose, s.Facing);
        Transform cup = Anchor(go.transform, "CupSpot", s.cup, s.Facing);
        Transform stand = Anchor(go.transform, "StandPoint", s.stand, s.StandFacing);
        TableSeat seat = go.AddComponent<TableSeat>();
        var so = new SerializedObject(seat);
        so.FindProperty("kind").intValue = (int)WaitingSpot.SpotKind.Seat;
        so.FindProperty("standPoint").objectReferenceValue = stand;
        so.FindProperty("drainMultiplier").floatValue = SeatDrain;
        so.FindProperty("cupSpot").objectReferenceValue = cup;
        so.FindProperty("snapToSeat").boolValue = true;
        so.FindProperty("seatPose").objectReferenceValue = pose;
        so.FindProperty("sitStyle").intValue = (int)TableSeat.SitStyle.Chair;
        so.FindProperty("faceTowards").objectReferenceValue = null;
        so.FindProperty("clearanceRadius").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();                 // a new object: its creation is the undo
        Shared.Made(run, go);
        s.made = go;
        s.seat = seat;
    }

    static Transform Anchor(Transform parent, string name, Vector3 world, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(world, rotation);
        return go.transform;
    }

    // ================================================================== the checks after the change (any FAIL puts everything back)

    static void After(Shared.Run run, Plan p)
    {
        // The drawn scene again, with everything placed.
        p.scene = new Scenery(run, p.movers);

        // ---- every piece where planned, on its surface, as named, on or off, its solid ----
        foreach (Placement x in p.places)
        {
            GameObject go = x.placed;
            run.Check(go != null && go.name == x.name && (x.parent == null || go.transform.parent == x.parent) && go.activeSelf == x.active,
                      $"{x.name}: placed{(x.parent != null ? " under " + x.parent.name : "")}, {(x.active ? "on" : "off")}");
            if (go == null) continue;
            run.Check(Vector3.Distance(go.transform.position, x.position) <= .001f && Quaternion.Angle(go.transform.rotation, x.rotation) <= .1f, $"{x.name}: at its planned pose {V(go.transform.position)}");
            Bounds drawn = WorldBox(go);
            run.Check(Near(drawn.min, x.World.min, .01f) && Near(drawn.max, x.World.max, .01f), $"{x.name}: drawn at {Shared.BoundsText(drawn)} (planned {Shared.BoundsText(x.World)})");
            if (x.kind == Kind.Floor || x.kind == Kind.Against || x.kind == Kind.Top)
                run.Check(Mathf.Abs(drawn.min.y - x.surfaceY) <= OnSurface, $"{x.name}: its bottom {F(drawn.min.y)} on {x.surface} at {F(x.surfaceY)} (2 mm)");
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            bool solidRight = x.solid ? colliders.Length == 1 && !colliders[0].isTrigger : colliders.Length == 0;
            run.Check(solidRight, $"{x.name}: {(x.solid ? "its one solid (" + x.piece.solidWhy + ")" : "no collider")} ({colliders.Length})");
            if (x.seat != null && x.kind != Kind.OnChair)
                run.Check(go.transform.parent == x.seat.transform && go.name.StartsWith(x.piece.name, StringComparison.Ordinal),
                          $"{x.name}: a child of {x.seat.name}, its name starting {x.piece.name} (the NPC lead finds it by that prefix)");
            if (x.kind == Kind.OnChair)
                run.Check(go.transform.parent == x.partner, $"{x.name}: a child of {x.partner.name} (it goes where the chair goes)");
        }
        Placement bin = p.Of(BinStation);
        float binHeading = bin.placed != null ? Shared.Facing(bin.placed.transform.rotation) : -1f;
        run.Check(bin.placed != null && bin.placed.name == BinStation && bin.placed.activeInHierarchy && Mathf.Abs(Mathf.DeltaAngle(binHeading, 0f)) <= 5f,
                  $"the bin station is named exactly {BinStation}, on, its openings (its front) to the room (heading {F(binHeading, "0")})");
        run.Check(GameObject.Find(BinStation) == bin.placed, $"GameObject.Find(\"{BinStation}\") finds it (one way the NPC lead's code can)");

        // ---- the reading corner: the old pieces on and where planned, the copy, the rug under the soft seats only, the seats ----
        foreach (Lounge l in p.lounge)
        {
            Transform t = l.t;
            run.Check(t != null && t.gameObject.activeInHierarchy && Vector3.Distance(t.position, l.position) <= .001f && Quaternion.Angle(t.rotation, l.rotation) <= .1f,
                      $"{l.label}: on, at {V(l.position)}, its front heading {F(l.heading, "0")}");
            if (t == null) continue;
            BoxCollider box = t.GetComponent<BoxCollider>();
            run.Check(box != null && box.enabled && !box.isTrigger && Near(box.bounds.min, l.solid.min, .01f) && Near(box.bounds.max, l.solid.max, .01f),
                      $"{l.label}: its solid at {(box != null ? Shared.BoundsText(box.bounds) : "-")}");
            Bounds drawn = WorldBox(t.gameObject);
            float under = l == p.table ? p.rug.surfaceY + p.rug.piece.Up : p.floorY;
            run.Check(Mathf.Abs(drawn.min.y - under) <= OnSurface, $"{l.label}: its lowest point {F(drawn.min.y)} on {(l == p.table ? "the rug's top" : "the floor")} at {F(under)} (2 mm)");
        }
        Bounds rugNow = WorldBox(p.rug.placed);
        var onRug = Physics.OverlapBox(rugNow.center + Vector3.up * .03f, new Vector3(rugNow.extents.x, .02f, rugNow.extents.z), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c != null && !IsPlayer(run, c.transform) && !p.lounge.Any(l => l != p.lamp && c.transform.IsChildOf(l.t))).Select(c => Shared.PathOf(c.transform)).Distinct().ToList();
        run.Check(onRug.Count == 0, $"the rug lies under the soft seats only (the two tub chairs and the low table; no other solid on it){(onRug.Count > 0 ? ": " + string.Join("; ", onRug) : "")}");
        foreach (CornerSeat s in p.seats)
        {
            TableSeat t = s.seat;
            bool wired = t != null && t.Kind == WaitingSpot.SpotKind.Seat && Mathf.Approximately(t.DrainMultiplier, SeatDrain) && t.SnapToSeat && t.Style == TableSeat.SitStyle.Chair
                         && Near(t.StandPoint.position, s.stand, .001f) && Near(t.SeatPose.position, s.pose, .001f) && Near(t.CupSpot.position, s.cup, .001f);
            run.Check(wired, $"{s.name}: a Seat, drain {F(SeatDrain, "0.0")}, Chair, Snap To Seat on, its three points where planned");
            float? cushion = MeshTopUnder(s.chair.t.gameObject, s.pose + Vector3.up * .3f, .6f);
            run.Check(cushion.HasValue && s.pose.y - cushion.Value >= -.005f && s.pose.y - cushion.Value <= .03f,
                      $"{s.name}: the sit point at {F(s.pose.y)} over the tub chair's cushion at {(cushion.HasValue ? F(cushion.Value) : "nothing")} (a ray down its mesh; 0 to 3 cm)");
            float? top = MeshTopUnder(p.table.t.gameObject, s.cup + Vector3.up * .3f, .6f);
            run.Check(top.HasValue && Mathf.Abs(top.Value - s.cup.y) <= .01f, $"{s.name}: the cup spot at {F(s.cup.y)} on the low table's top at {(top.HasValue ? F(top.Value) : "nothing")} (1 cm)");
            run.Check(t != null && !NpcBeats.TableInFront(t), $"{s.name}: no table at table height in front (NpcBeats: the sofa clips play here, as they did in the lounge)");
        }
        int live = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Count(t => t.gameObject.scene == run.Scene);
        run.Check(live == p.seatsBefore + p.seats.Count, $"Active TableSeats: {live} ({p.seatsBefore} and the corner's {p.seats.Count})");
        run.Check(p.daylight.warmLights.Any(l => l != null && l.name.StartsWith("Reading lamp light", StringComparison.Ordinal)), "the reading lamp's light is one of CafeDaylight's warm lights");

        // ---- nothing passes through anything: each piece where it is (on or off: its box against everything drawn now), each
        //      moved lounge piece ----
        foreach (Placement x in p.places)
        {
            if (x.placed == null) continue;
            var mates = new List<Transform> { x.placed.transform };
            if (x.partner != null) mates.Add(x.partner);
            if (x == p.rug) mates.AddRange(p.lounge.Where(l => l != p.lamp).Select(l => l.t));
            List<string> through = p.scene.Through(Shrunk(x.piece.local, .002f, .003f), x.Pose, t => mates.Any(m => IsUnder(t, m)));
            run.Check(through.Count == 0, $"{x.name}: nothing passes through it{(through.Count > 0 ? ": " + string.Join("; ", through.Take(6)) : "")}");
        }
        foreach (Lounge l in p.lounge)
        {
            var through = new List<string>();
            foreach (MeshFilter f in l.t.GetComponentsInChildren<MeshFilter>(false))
                if (f.sharedMesh != null)
                    through.AddRange(p.scene.Through(Shrunk(f.sharedMesh.bounds, .004f, .004f), f.transform.localToWorldMatrix, t => t.IsChildOf(l.t) || IsUnder(t, p.rug.placed.transform)));
            through = through.Distinct().ToList();
            run.Check(through.Count == 0, $"{l.label}: nothing passes through it{(through.Count > 0 ? ": " + string.Join("; ", through.Take(6)) : "")}");
        }

        // ---- the gameplay anchors never moved ----
        var moved = p.anchors.Where(a => a.Key == null || Vector3.Distance(a.Key.position, a.Value) > .001f).Select(a => a.Key != null ? Shared.PathOf(a.Key) : "(gone)").ToList();
        run.Check(moved.Count == 0, moved.Count == 0 ? $"No gameplay anchor moved ({p.anchors.Count}, 1 mm)." : "Moved: " + string.Join(", ", moved));

        // ---- the drawing: a child of the board, on its face ----
        Placement drawing = p.Of(Drawing);
        if (drawing.placed != null)
        {
            Vector3 local = p.board.InverseTransformPoint(WorldBox(drawing.placed).center);
            run.Check(drawing.placed.transform.parent == p.board && Mathf.Abs(local.x) < .5f && local.y > 0f && local.y < .7f && local.z > 0f && local.z < .03f,
                      $"the drawing is the board's child, on its face (its middle at {V(local)} in the board's frame): the overhead view fades it with the board");
        }

        // ---- the routes, re-baked here so the new stand points can be walked to before anything is kept ----
        AcesCafeLayoutSetup.BakeRoutes();
        GameObject spawnGo = run.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SpawnPoint");
        Vector3 origin = spawnGo != null ? spawnGo.transform.position : new Vector3(0f, .06f, -1.2f);
        var path = new NavMeshPath();
        var targets = p.seats.Select(s => (s.name + "'s stand point", s.stand)).ToList();
        Transform drop = bin.placed != null ? bin.placed.transform.Find(DropPointName) : null;
        if (drop != null) targets.Add(("the bin station's drop point", drop.position));
        foreach ((string what, Vector3 at) in targets)
        {
            bool on = NavMesh.SamplePosition(at, out NavMeshHit hit, .22f, NavMesh.AllAreas);
            bool reached = on && NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            run.Check(on && reached, $"{what}: on the NavMesh ({(on ? F(FlatDistance(hit.position, at) * 100f, "0.0") + " cm off it" : "not within 0.22 m")}) and walked to from the spawn ({(reached ? "a complete path" : "NO PATH")})");
        }
        BinProbes(run, bin, origin);
        Plateaus(run, origin, p);

        // ---- the guards, with the corner's two seats: as many served as before, and the two new ones too ----
        string layout = AcesCafeLayoutSetup.ValidateLayout();
        Counted(layout, out int paths, out int pathTargets);
        run.Check(layout.Contains($"{p.seatsBefore + p.seats.Count} seats") && pathTargets == p.pathTargetsBefore + p.seats.Count && paths == p.pathsBefore + p.seats.Count,
                  $"The layout check: {paths}/{pathTargets} (before {p.pathsBefore}/{p.pathTargetsBefore}; the corner's two stand points added and reached): {layout}");
        string circulation = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        Counted(circulation, out int served, out int places);
        bool cornerServed = !p.seats.Any(s => circulation.Contains(s.name + "/"));
        run.Check(places == p.placesBefore + p.seats.Count && served == p.servedBefore + p.seats.Count && cornerServed,
                  $"The occupied-circulation guard: {served}/{places} (before {p.servedBefore}/{p.placesBefore}; the corner's two places served): {circulation}");
    }

    // The NPC lead's way to the bin (NpcLifeProps.TryGetBin): twelve probes round the station at its larger half-size
    // plus 0.42 m, on the NavMesh within 0.35 m. How many land, and whether the one nearest the counter is walked to.
    static void BinProbes(Shared.Run run, Placement bin, Vector3 origin)
    {
        if (bin.placed == null) return;
        Bounds b = WorldBox(bin.placed);
        float reach = Mathf.Max(b.extents.x, b.extents.z) + .42f;
        var roomward = new Vector3(0f, b.center.y, 12.5f);
        int landed = 0;
        Vector3? best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            var probe = new Vector3(b.center.x + Mathf.Cos(a) * reach, b.min.y, b.center.z + Mathf.Sin(a) * reach);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .35f, NavMesh.AllAreas)) continue;
            landed++;
            float d = (hit.position - roomward).sqrMagnitude;
            if (d < bestD) { bestD = d; best = hit.position; }
        }
        var path = new NavMeshPath();
        bool walked = best.HasValue && NavMesh.CalculatePath(origin, best.Value, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        run.Check(landed > 0 && walked, $"the bin station from the guests' side: {landed} of 12 probes round it are on the NavMesh, the one nearest the counter " +
                                        $"{(best.HasValue ? V(best.Value) : "-")} {(walked ? "walked to from the spawn" : "NOT reached")}");
    }

    // No walkable NavMesh between 0.2 and 0.9 m over the new solids that people can walk up onto (the café life setup's
    // test, as Seats 1 runs it): islands at most, never a step.
    static void Plateaus(Shared.Run run, Vector3 door, Plan p)
    {
        var solids = p.lounge.Select(l => l.solid).Concat(p.places.Where(x => x.solid && x.placed != null).Select(x => x.World)).ToList();
        int plateau = 0, islands = 0;
        var route = new NavMeshPath();
        string where = "";
        foreach (Bounds b in solids)
            for (float z = b.min.z - .2f; z <= b.max.z + .2f; z += .1f)
                for (float x = b.min.x - .2f; x <= b.max.x + .2f; x += .1f)
                    if (NavMesh.SamplePosition(new Vector3(x, .6f, z), out NavMeshHit h, .45f, NavMesh.AllAreas) && h.position.y > .2f && h.position.y < .9f)
                    {
                        bool connected = NavMesh.CalculatePath(door, h.position, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete;
                        if (!connected) { islands++; continue; }
                        plateau++;
                        if (where.Length == 0) where = $" (the first at {V(h.position)})";
                    }
        run.Check(plateau == 0, $"No new solid became a step: {plateau} connected NavMesh samples between 0.2 and 0.9 m over the corner's furniture and the bins{where}; {islands} unreachable island samples ignored.");
    }

    // ================================================================== photos (before and after, from the same places)

    static void Photos(Shared.Run run, Plan p)
    {
        Vector3 PlacedAt(string piece, Vector3 fallback) { Placement x = p != null ? p.Of(piece) : null; return x != null ? x.position : fallback; }
        Vector3 bin = PlacedAt(BinStation, new Vector3(2.65f, 0f, .25f));
        Vector3 street = PlacedAt(StreetBin, new Vector3(4.8f, 0f, -.6f));
        Vector3 condiments = PlacedAt(Condiments, new Vector3(2.8f, .91f, 13.5f));
        Vector3 till = PlacedAt(TillMat, new Vector3(-.26f, .02f, 14.56f));
        // The three close-ups the brief asks for (the reading corner, the bin station by the door, the counter's end), and the rest.
        Shared.Photo(run, "01-reading-corner", new Vector3(-3.7f, 1.55f, 3.5f), new Vector3(-6.1f, .45f, 1.35f), 58f);
        Shared.Photo(run, "02-reading-corner-from-the-door", new Vector3(-.6f, 1.65f, .9f), new Vector3(-6.2f, .6f, 1.6f), 50f);
        Shared.Photo(run, "03-bin-station-by-the-door", new Vector3(bin.x - 1.6f, 1.6f, 3.2f), new Vector3(bin.x, .45f, bin.z), 55f);
        Shared.Photo(run, "04-counter-end-condiments-pastries-water", new Vector3(condiments.x - .3f, 1.55f, 11.5f), new Vector3(condiments.x + .6f, 1.0f, 13.6f), 55f);
        Shared.Photo(run, "05-till-mat", new Vector3(till.x + 1.4f, 1.9f, 16.2f), new Vector3(till.x, 0f, till.z), 55f);
        Shared.Photo(run, "06-notice-board", new Vector3(6.1f, 1.5f, 3.5f), new Vector3(7.4f, 1.47f, 3.5f), 40f);
        Shared.Photo(run, "07-street-bin", new Vector3(street.x - 2.4f, 1.6f, -3.4f), new Vector3(street.x, .5f, street.z), 55f);
        Shared.Photo(run, "08-banquette-napkins-and-coats", new Vector3(-4.3f, 1.35f, 3.4f), new Vector3(-6.1f, .75f, 5.8f), 55f);
        Shared.Photo(run, "09-communal-table", new Vector3(-.7f, 1.6f, 6.2f), new Vector3(-2.95f, .75f, 6.2f), 60f);
        Shared.Photo(run, "10-window-counter", new Vector3(4.6f, 1.55f, 6.6f), new Vector3(7.1f, 1.0f, 6.6f), 55f);
        Shared.Photo(run, "11-plan-from-above", new Vector3(0f, 17f, 6.6f), new Vector3(0f, 0f, 6.61f), 50f, true);
        Shared.GameCameraPhoto(run, "12-game-camera");
        if (run.Phase != "2-after" || p == null) return;
        // In use, for the photo only: the laptops, bags and left cups switched on (they are off in the scene), then off again.
        var shown = p.Off.Where(x => x.placed != null && !x.placed.activeSelf && x.piece.name != LaptopClosed).Select(x => x.placed).ToList();
        try
        {
            foreach (GameObject go in shown) go.SetActive(true);
            Shared.Photo(run, "13-in-use-communal-west-4-laptop-and-bag (photo only)", new Vector3(-5.3f, 1.45f, 9.3f), new Vector3(-3.3f, .75f, 7.5f), 55f);
            Shared.Photo(run, "14-in-use-communal-laptop-and-left-cups (photo only)", new Vector3(-1.0f, 1.45f, 7.6f), new Vector3(-2.8f, .7f, 6.2f), 58f);
        }
        finally { foreach (GameObject go in shown) if (go != null) go.SetActive(false); }
    }

    // ================================================================== the undo

    // The shared undo puts every moved piece back under its old parent at its old pose, deletes what the step made and takes
    // its list entries out; it can't switch off what the step switched on (its record has no list for that). So: switch
    // those off first (the record's "moved" entries marked OnMark), then the shared undo; if it refused, back on again.
    static void UndoDressing()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject cafe = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Shared.CafeRootName);
        CafeRedesignRecord record = cafe != null ? Shared.RecordOf(cafe.transform, Title) : null;
        bool later = record != null && Shared.Records(cafe.transform).Any(r => r.order > record.order);
        if (record == null || later || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Shared.UndoStep(Title, Label);      // it says why
            return;
        }
        List<GameObject> on = record.moved.Where(m => m != null && m.target != null && m.why != null && m.why.StartsWith(OnMark, StringComparison.Ordinal))
                                          .Select(m => m.target.gameObject).Distinct().ToList();
        Undo.IncrementCurrentGroup();
        foreach (GameObject go in on)
        {
            Undo.RecordObject(go, Title + " - Undo");
            go.SetActive(false);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }
        Shared.UndoStep(Title, Label);
        if (Shared.RecordOf(cafe.transform, Title) == null) return;
        foreach (GameObject go in on)
            if (go != null)
            {
                Undo.RecordObject(go, Title + " - Undo");
                go.SetActive(true);
                if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
    }

    // ================================================================== measuring

    // The drawn scene round the café, read once (each mesh's vertices and triangles cached): the highest surface under a
    // point (CafeFurnishing.TopAt's way) and what passes through a box (the triangles against the box, separating axes).
    // Active, enabled mesh renderers only; Ace, the street's moving actors and shadow-only meshes left out.
    sealed class Scenery
    {
        sealed class Entry { public MeshFilter f; public Bounds b; public Matrix4x4 m; }
        static readonly Bounds Roi = new Bounds(new Vector3(0f, 1.5f, 7.5f), new Vector3(22f, 7f, 30f));
        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<Mesh, Vector3[]> vertices = new Dictionary<Mesh, Vector3[]>();
        readonly Dictionary<Mesh, int[]> triangles = new Dictionary<Mesh, int[]>();
        public int Count => entries.Count;

        public Scenery(Shared.Run run, HashSet<Renderer> leaveOut)
        {
            foreach (MeshRenderer r in Shared.InScene<MeshRenderer>(run, false))
            {
                if (r == null || !r.enabled || r.forceRenderingOff || leaveOut.Contains(r) || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                if (run.Player != null && r.transform.IsChildOf(run.Player)) continue;
                Bounds b = r.bounds;
                if (!Roi.Intersects(b)) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                entries.Add(new Entry { f = f, b = b, m = f.transform.localToWorldMatrix });
            }
        }

        Vector3[] V(Mesh m)
        {
            if (!vertices.TryGetValue(m, out Vector3[] v))
            {
                try { v = m.vertices; } catch (Exception) { v = new Vector3[0]; }
                vertices[m] = v;
            }
            return v;
        }

        int[] T(Mesh m)
        {
            if (!triangles.TryGetValue(m, out int[] t))
            {
                try { t = m.triangles; } catch (Exception) { t = new int[0]; }
                triangles[m] = t;
            }
            return t;
        }

        // The highest drawn surface under (x, z) at or below `below`; `only`: that thing's alone. -Infinity for none.
        public float TopAt(Vector2 p, float below, Transform only)
        {
            float best = float.NegativeInfinity;
            foreach (Entry e in entries)
            {
                Bounds b = e.b;
                if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.z || p.y > b.max.z || b.min.y > below) continue;
                if (only != null && !e.f.transform.IsChildOf(only)) continue;
                Vector3[] v = V(e.f.sharedMesh);
                int[] t = T(e.f.sharedMesh);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = e.m.MultiplyPoint3x4(v[t[i]]), c = e.m.MultiplyPoint3x4(v[t[i + 1]]), d = e.m.MultiplyPoint3x4(v[t[i + 2]]);
                    float det = (c.z - d.z) * (a.x - d.x) + (d.x - c.x) * (a.z - d.z);
                    if (Mathf.Abs(det) < 1e-9f) continue;
                    float l1 = ((c.z - d.z) * (p.x - d.x) + (d.x - c.x) * (p.y - d.z)) / det;
                    float l2 = ((d.z - a.z) * (p.x - d.x) + (a.x - d.x) * (p.y - d.z)) / det;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                    float y = l1 * a.y + l2 * c.y + l3 * d.y;
                    if (y <= below && y > best) best = y;
                }
            }
            return best;
        }

        // The things whose triangles pass through `box` (in `pose`'s frame); `aside`: leave those out.
        public List<string> Through(Bounds box, Matrix4x4 pose, Func<Transform, bool> aside)
        {
            var found = new List<string>();
            Bounds world = Transformed(box, pose);
            Matrix4x4 toBox = pose.inverse;
            Vector3 c = box.center, h = box.extents;
            foreach (Entry e in entries)
            {
                if (!e.b.Intersects(world)) continue;
                if (aside != null && aside(e.f.transform)) continue;
                Vector3[] v = V(e.f.sharedMesh);
                int[] t = T(e.f.sharedMesh);
                if (v.Length == 0 || t.Length == 0) continue;
                Matrix4x4 m = toBox * e.m;
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = m.MultiplyPoint3x4(v[i]);
                for (int i = 0; i + 2 < t.Length; i += 3)
                    if (TriangleBox(c, h, w[t[i]], w[t[i + 1]], w[t[i + 2]])) { found.Add(Shared.PathOf(e.f.transform)); break; }
            }
            return found;
        }
    }

    // A piece's box shrunk by `shrink` all round and its bottom raised by `lift` (what it stands on only touches it).
    static Bounds Shrunk(Bounds local, float shrink, float lift)
    {
        var b = new Bounds(local.center, local.size - Vector3.one * 2f * shrink);
        b.SetMinMax(new Vector3(b.min.x, b.min.y + lift, b.min.z), b.max);
        return b;
    }

    // A triangle against an axis-aligned box (centre, half size): the separating axis test.
    static bool TriangleBox(Vector3 centre, Vector3 half, Vector3 a, Vector3 b, Vector3 d)
    {
        Vector3 v0 = a - centre, v1 = b - centre, v2 = d - centre;
        if (Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x)) < -half.x || Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x)) > half.x) return false;
        if (Mathf.Max(v0.y, Mathf.Max(v1.y, v2.y)) < -half.y || Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y)) > half.y) return false;
        if (Mathf.Max(v0.z, Mathf.Max(v1.z, v2.z)) < -half.z || Mathf.Min(v0.z, Mathf.Min(v1.z, v2.z)) > half.z) return false;
        Vector3 e0 = v1 - v0, e1 = v2 - v1, e2 = v0 - v2;
        if (Separated(Vector3.Cross(e0, e1), v0, v1, v2, half)) return false;
        foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            foreach (Vector3 e in new[] { e0, e1, e2 })
            {
                Vector3 s = Vector3.Cross(axis, e);
                if (s.sqrMagnitude < 1e-12f) continue;
                if (Separated(s, v0, v1, v2, half)) return false;
            }
        return true;
    }

    static bool Separated(Vector3 axis, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 half)
    {
        float p0 = Vector3.Dot(axis, v0), p1 = Vector3.Dot(axis, v1), p2 = Vector3.Dot(axis, v2);
        float r = half.x * Mathf.Abs(axis.x) + half.y * Mathf.Abs(axis.y) + half.z * Mathf.Abs(axis.z);
        return Mathf.Min(p0, Mathf.Min(p1, p2)) > r || Mathf.Max(p0, Mathf.Max(p1, p2)) < -r;
    }

    // The highest drawn surface of a piece straight under `from`, down to `depth` below it (CafeSeatsSteps.MeshTopUnder).
    static float? MeshTopUnder(GameObject piece, Vector3 from, float depth)
    {
        float? best = null;
        foreach (MeshFilter f in piece.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = f.sharedMesh;
            if (m == null) continue;
            Matrix4x4 w = f.transform.localToWorldMatrix;
            Vector3[] v = m.vertices;
            for (int sub = 0; sub < m.subMeshCount; sub++)
            {
                if (m.GetTopology(sub) != MeshTopology.Triangles) continue;
                int[] t = m.GetTriangles(sub);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = w.MultiplyPoint3x4(v[t[i]]), b = w.MultiplyPoint3x4(v[t[i + 1]]), c = w.MultiplyPoint3x4(v[t[i + 2]]);
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-9f) continue;
                    float l1 = ((b.z - c.z) * (from.x - c.x) + (c.x - b.x) * (from.z - c.z)) / d;
                    float l2 = ((c.z - a.z) * (from.x - c.x) + (a.x - c.x) * (from.z - c.z)) / d;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -1e-5f || l2 < -1e-5f || l3 < -1e-5f) continue;
                    float y = l1 * a.y + l2 * b.y + l3 * c.y;
                    if (y > from.y || y < from.y - depth) continue;
                    if (best == null || y > best.Value) best = y;
                }
            }
        }
        return best;
    }

    // The first hit of a ray down onto one thing's own colliders.
    static RaycastHit? RayOnto(Transform thing, Vector3 from, float distance)
    {
        RaycastHit? best = null;
        foreach (Collider c in thing.GetComponentsInChildren<Collider>(false))
        {
            if (c.isTrigger || !c.enabled) continue;
            if (c.Raycast(new Ray(from, Vector3.down), out RaycastHit h, distance) && (best == null || h.distance < best.Value.distance)) best = h;
        }
        return best;
    }

    // The thing whose top a point lies on (the first solid under it).
    static Transform TopUnder(Shared.Run run, Vector3 at)
    {
        RaycastHit? h = Shared.Down(run, at + Vector3.up * .2f, .5f);
        return h.HasValue ? h.Value.collider.transform : null;
    }

    // What must not move: the stations and their stand points, every waiting spot and seat and its stand point, the queue,
    // the intake shelf, the dispenser, the cup supply, the spawners, the doors (CafeSeatsSteps.Anchors).
    static Dictionary<Transform, Vector3> Anchors(Shared.Run run)
    {
        var a = new Dictionary<Transform, Vector3>();
        void Add(Transform t) { if (t != null && !a.ContainsKey(t)) a[t] = t.position; }
        foreach (StationInteractable s in Shared.InScene<StationInteractable>(run)) { Add(s.transform); Add(s.StandPoint); }
        foreach (WaitingSpot w in Shared.InScene<WaitingSpot>(run)) { Add(w.transform); Add(w.StandPoint); }
        foreach (CounterQueue q in Shared.InScene<CounterQueue>(run)) { Add(q.transform); foreach (Transform c in q.transform) Add(c); }
        foreach (ItemSlotArea s in Shared.InScene<ItemSlotArea>(run)) Add(s.transform);
        foreach (BeverageStation b in Shared.InScene<BeverageStation>(run)) Add(b.transform);
        foreach (BeverageCupSupply b in Shared.InScene<BeverageCupSupply>(run)) Add(b.transform);
        foreach (CustomerSpawner s in Shared.InScene<CustomerSpawner>(run)) Add(s.transform);
        foreach (PatronSpawner s in Shared.InScene<PatronSpawner>(run)) Add(s.transform);
        foreach (Transform t in Shared.Named(run, "Open door collision")) { Add(t); foreach (Transform c in t) Add(c); }
        return a;
    }

    // The world box of a thing's enabled renderers (while it is switched off, from its meshes at its pose).
    static Bounds WorldBox(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        if (go.activeInHierarchy)
        {
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
        return DrawnBoxAt(go.transform, Matrix4x4.TRS(go.transform.position, go.transform.rotation, go.transform.lossyScale));
    }

    // ================================================================== small helpers

    static Bounds Transformed(Bounds b, Matrix4x4 m)
    {
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    // The gap across the ground between two boxes (0 when their footprints overlap).
    static float Gap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The distance across the ground from a box's footprint to a point.
    static float FlatGap(Bounds b, Vector3 p)
    {
        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
        float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The distance across the ground from a box's footprint to a segment (CafeStreetSteps.BoxToSegment: 0 when it crosses).
    static float BoxToSegment(Bounds box, Vector3 a, Vector3 b)
    {
        float x0 = box.min.x, x1 = box.max.x, z0 = box.min.z, z1 = box.max.z;
        float t0 = 0f, t1 = 1f, dx = b.x - a.x, dz = b.z - a.z;
        bool crosses = true;
        foreach ((float pp, float q) in new[] { (-dx, a.x - x0), (dx, x1 - a.x), (-dz, a.z - z0), (dz, z1 - a.z) })
        {
            if (Mathf.Abs(pp) < 1e-9f) { if (q < 0f) { crosses = false; break; } continue; }
            float r = q / pp;
            if (pp < 0f) { if (r > t1) { crosses = false; break; } if (r > t0) t0 = r; }
            else { if (r < t0) { crosses = false; break; } if (r < t1) t1 = r; }
        }
        if (crosses && t0 <= t1) return 0f;
        float best = float.PositiveInfinity;
        foreach (Vector3 corner in new[] { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1) })
            best = Mathf.Min(best, PointToSegment(corner, a, b));
        best = Mathf.Min(best, FlatGap(box, a));
        best = Mathf.Min(best, FlatGap(box, b));
        return best;
    }

    static float PointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
        Vector2 ab = B - A;
        float t = ab.sqrMagnitude < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
        return Vector2.Distance(P, A + ab * t);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    static Vector3 AbsVec(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static bool Near(Vector3 a, Vector3 b, float slack) => Mathf.Abs(a.x - b.x) <= slack && Mathf.Abs(a.y - b.y) <= slack && Mathf.Abs(a.z - b.z) <= slack;
    static bool IsPlayer(Shared.Run run, Transform t) => run.Player != null && t.IsChildOf(run.Player);
    static bool IsUnder(Transform t, Transform root) => root != null && t != null && t.IsChildOf(root);

    // The words inside a name's parentheses: "Seat (window counter, south)" -> "window counter, south".
    static string Inner(string name)
    {
        int a = name.IndexOf('('), b = name.LastIndexOf(')');
        return a >= 0 && b > a ? name.Substring(a + 1, b - a - 1) : name;
    }

    static string V(Vector3 v) => Shared.V(v);
    static string F(float f, string format = "0.000") => Shared.F(f, format);
    static float ParseF(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    static string When() => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c) ? c : Color.magenta;
    static string Str(Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out object v) && v is string s && s.Length > 0 ? s : null;
    static float Num(Dictionary<string, object> d, string key, float fallback) =>
        d != null && d.TryGetValue(key, out object v) && v is double n ? (float)n : fallback;
    static bool Bool(Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out object v) && (v is bool b ? b : v is double n && n != 0);

    static bool TryVec(Dictionary<string, object> d, string key, out Vector3 v)
    {
        v = Vector3.zero;
        if (d == null || !d.TryGetValue(key, out object o) || !(o is List<object> l) || l.Count != 3 || !l.All(x => x is double)) return false;
        v = new Vector3((float)(double)l[0], (float)(double)l[1], (float)(double)l[2]);
        return true;
    }

    static string NewFolder(string label)
    {
        string folder = Path.Combine(Shared.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static void Keep(string label, string folder, string text, bool error)
    {
        try
        {
            Directory.CreateDirectory(Shared.LogRoot);
            File.WriteAllText(Path.Combine(Shared.LogRoot, label + "-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (folder != null && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning("[Lived in] Couldn't write the report: " + e.Message); }
        if (error) Debug.LogError("[Lived in] " + text); else Debug.Log("[Lived in] " + text);
    }

    // A small JSON reader (objects, arrays, strings, numbers, true/false/null): CafeKitSteps.Json, which is private there.
    static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            return Value(text, ref i);
        }

        static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static bool At(string s, int i, string word) => i + word.Length <= s.Length && string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON ended early.");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (At(s, i, "true")) { i += 4; return true; }
            if (At(s, i, "false")) { i += 5; return false; }
            if (At(s, i, "null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"JSON: unexpected '{c}' at {i}.");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new FormatException($"JSON: ':' expected at {i}.");
                i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException($"JSON: ',' or '}}' expected at {i}.");
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException($"JSON: ',' or ']' expected at {i}.");
            }
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"JSON: a string expected at {i}.");
            i++;
            var b = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'u') { b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else b.Append(s[i]);
                i++;
            }
            i++;
            return b.ToString();
        }
    }
}
#endif
