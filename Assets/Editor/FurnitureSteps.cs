#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// FURNITURE 0-3: THE CAFÉ'S FURNITURE BECOMES PLACEABLE (24 Oct 2026; Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md
// §4.3, §4.6, §4.7, §4.9; his answers of 24 Oct)
//
// His choice of 23 Oct, "Grow the café" (buy and place furniture; seats count, décor is looks), and of 24 Oct: arranging only
// while the café is closed (f1), a 25 cm grid with quarter turns (f2), sold back for half, moved for free (f3), Tier 2 later
// (f4), no décor reactions (f5), the spec's prices (r2), and a half-finished Day 1 with 10 seats open (r1). These steps turn the
// café as it is built into furniture the runtime can move (PlacedPiece, CafeLayout), build the pieces the Shop sells, and lay
// the half-finished café's dust sheets. Furniture 1 and 3 go through the café redesign's shared step (CafeRedesignShared.Step):
// the survey first, which changes nothing; one undo group and the record; the checks after (any FAIL puts everything back);
// the guards "not worse"; photos; never saved.
//
//   Furniture 0 - Check first (read-only). Finds every set by the name its step gave it and says what will move where, by
//     path, and what the rules make of the café today and half-finished. After Furniture 1, it reports the pieces instead.
//   Furniture 1 - The café's own pieces become placeable (scene, with Undo). Needs Seat tags 1 and Art tidy 1 first.
//     * Each set becomes one piece: a root under 31 - furniture/The café's own pieces at its pivot (a table's middle; a wall
//       piece's back, at its wall), its set's GROUP moved under it whole (world poses, names and references kept: the settled
//       guests still point at the same seats, and a seat's parent is still "Bistro 2-top (south)", "Reading corner" or
//       "Cafe dressing 3 - lounge", which NpcBeatChecks reads), its tables' napkin dispensers with it. 17 pieces: the two
//       bistro 2-tops, the three banquette bays (each with its 2-top and two end caps: the run's two ends, and four made here,
//       shown only where no bay joins: BanquetteRun), the communal table, the window counter, the reading corner, the lounge,
//       and the eight floor plants.
//     * The lounge's floor lamp stays where Café dressing 3 put it, as the room's (31 - furniture/Not furniture (fixed), Not
//       Walkable for the routes as its group was): it stands in Ace's way out from behind the counter (FurnitureRules R1), so
//       as part of the lounge the lounge could never stand there again. The reading corner's lamp goes with its corner (§4.8).
//     * Each piece: its PlacedPiece (its kind, id "scene:<root>"), a carving NavMeshObstacle per solid (child "Carve - ..."),
//       NavMeshModifier Ignore From Build on its root (the lounge's own Not Walkable modifier switched off: the nearest
//       modifier wins), every static flag cleared (a static-batched mesh would stay drawn where the scene was built). The
//       routes are re-baked without the pieces; every piece carves its own hole at run time (§4.3).
//     * The bays' backs (art finding a56, "The banquette's missing back"): a thin dark board (CC_Dark_Kick) at the back of
//       each backrest, between its base and its top roll, 3 mm off the wall, where the channel back has no faces of its own.
//     * The window counter's three stools SIT (his call c12, kept: "Nobody is sent to wait standing at the window counter
//       until its stools can be sat on. Next step: make those 3 stools sit, like the repair bar's"): Snap To Seat on, Max Lift
//       0.32 (the body rises inside the sit clips to the 0.745 m seat, as NPC wave 2's repair-bar stools), Approach Side 0.60
//       (half their 1.2 m spacing: the way in passes between two stools, as the repair bar's 0.50 at 1.0 m), and no turn: the
//       counter's brackets leave knee space. SeatChoice's Standing Wait lifts by itself, and the Officer's favourite wakes.
//     * CafeLayout on GameManager (beside SaveManager), with the kinds' catalogue, the 17 pieces and the new game's nine (ten
//       seats with the repair bar's three).
//     The kinds and the catalogue (Assets/Data/Furniture/) are made here as assets, with no prefabs yet: an undo leaves them,
//     and a rerun reuses them.
//     The checks: 17 pieces, every part and the 25 seats on them where they were (1 mm) with the tags Seat tags 1 gave them,
//     28 seats in all, the stools' sitting fields, no static flag, one carving box per solid, every piece passes the rules
//     against the rest, today's café passes both guards' replicas (FurnitureRules.CheckWholeLayout: every place reached from
//     the door, every place served with all of them taken) and the real ones (the shared step's guards, not worse), and the
//     half-finished café (10 seats) passes both replicas too.
//   Furniture 2 - Build the furniture prefabs (assets). The seven kinds' prefabs under Assets/Prefabs/Furniture/, copied from
//     the café's own dressed pieces (so they wear the same grain: art finding a80, "Tier 1 shop furniture comes in flat"), the
//     square table made from the south banquette 2-top's table and café chair with a second chair turned to face it, the
//     four plants; the ghost's material; the kinds filled in. A bought piece brings no coat and no left cup; its seats are
//     named "Seat (bought <kind>, <seat>)", never a scene seat's name. Checks: a prefab placed at its source's pivot has the
//     same solids and stand points (1 mm); no flat wood (CC_Wood_*) or DC_Fabric left on any renderer (swapped for the café
//     palette's grain, as CafeFurnishing.Remap does, and listed, if any were); its seat count is its kind's.
//   Furniture 3 - Lay the dust sheets (scene, with Undo). Five canvas drop cloths with soft folds and a stack of Grace's taped
//     boxes each (GH_BoxStack, a collider and a carving box), over where the banquette, the communal table, the reading corner
//     and the south bistro stand, switched off in the scene: the runtime lays them for a new game. Checks: clear of the open
//     pieces, the waiting nook, the aisle, the queue and the door; the half-finished café still passes both guards' replicas
//     with the boxes as solids. Its photos show the half-finished café (switched to it for the photo, then back).
//   Furniture 1 - Undo / Furniture 3 - Undo (scene): newest first (3, then 1).
//
// Reports and photos: Logs/CafeRedesign/furniture-<n>-<time>/; the last word: Logs/CafeRedesign/furniture-<n>-last.txt.
// The art findings this touches but leaves to the Unity art builder (keep these names): a51 the lounge's rug is
// "LI_Rug_Corner (lounge)" under the piece "Lounge corner, by the counter" (and in Lounge corner.prefab); a48 the napkin boxes
// are "LI_NapkinDispenser (...)" under their tables' pieces (and in the bistro, square, window counter, bay and communal
// prefabs); a43 the repair bar's stools are not furniture and stay in Seats 1's "Repair bar stools"; a58, a59 and a61 (the
// patio table, the door planters' boulders, the patio chairs) are outside and untouched.
// ---------------------------------------------------------------------------
internal static class FurnitureSteps
{
    public const string Title1 = "Furniture 1 - The café's own pieces become placeable";
    public const string Title2 = "Furniture 2 - Build the furniture prefabs";
    public const string Title3 = "Furniture 3 - Lay the dust sheets";
    const string Label0 = "furniture-0", Label1 = "furniture-1", Label2 = "furniture-2", Label3 = "furniture-3";
    const string Menu0 = Shared.Menu + "Furniture 0 - Check first (read-only)";
    const string Menu1 = Shared.Menu + "Furniture 1 - The café's own pieces become placeable (scene, with Undo)";
    const string Menu2 = Shared.Menu + "Furniture 2 - Build the furniture prefabs (assets)";
    const string Menu3 = Shared.Menu + "Furniture 3 - Lay the dust sheets (scene, with Undo)";
    const string Menu1Undo = Shared.Menu + "Furniture 1 - Undo (scene)";
    const string Menu3Undo = Shared.Menu + "Furniture 3 - Undo (scene)";

    public const string FurnitureGroup = "31 - furniture", OwnGroup = "The café's own pieces", BoughtGroup = "Bought pieces",
                        SheetsGroup = "Dust sheets", FixedGroup = "Not furniture (fixed)";
    public const string DataFolder = "Assets/Data/Furniture", PrefabFolder = "Assets/Prefabs/Furniture";
    public const string CataloguePath = DataFolder + "/Furniture catalogue.asset";
    public const string GhostPath = DataFolder + "/Furniture ghost.mat";
    const string CanvasPath = DataFolder + "/Dust sheet canvas.mat";
    const string WeavePath = DataFolder + "/Dust sheet canvas weave.asset";
    const string ClothMeshPath = DataFolder + "/Dust sheet cloths.asset";
    const string PalettePath = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";   // CafeFurnishing's "Cafe grain - ..." woods
    const string FabricPath = "Assets/Art/Materials/Cafe/CF_Fabric.mat";               // what the kit maps DC_Fabric to (the bay)
    const string DarkBoardPath = "Assets/Art/Materials/CC_Dark_Kick.mat";              // the kit's dark kick: the bays' backs
    const string BoxStackModel = "Assets/Art/Models/GraceHouse/GH_BoxStack.fbx";        // Grace's three taped boxes, 1.14 m
    const string NoteUndo = "furniture-undo|";
    /// <summary>His answer of 24 Oct (r1): Day 1's half-finished café opens 10 seats.</summary>
    public const int NewGameSeats = 10;
    /// <summary>Today's café: 25 seats on its pieces and the repair bar's 3.</summary>
    public const int PieceSeats = 25, AllSeats = 28;

    const string Seats1 = "30 - cafe redesign/Seats 1 - the San Francisco seat plan";
    const string Banquette = Seats1 + "/Banquette (west wall)";
    const string Napkins = "30 - cafe redesign/Lived in/Napkins";
    const string Lounge = "30 - cafe redesign/Cafe dressing 3 - lounge";
    const string LoungeLamp = Lounge + "/Floor lamp (lounge)";
    const string ReadingCorner = "30 - cafe redesign/Lived in/Reading corner";
    const string Room1Plants = "30 - cafe redesign/Room 1 - ceiling, light and the room's few things/Plants";
    const string Touches = Lounge + "/Touches (plants in the other bare bands)";

    // ================================================================== menus

    [MenuItem(Menu0)]
    static void CheckFirst() => Shared.Check(Title1 + " (check first)", Label0, Survey);

    [MenuItem(Menu1)]
    static void MakePlaceable() => Shared.Step(Title1, Label1, Survey, Change1, Photos1, guards: true, rebake: true, roomPhotos: true);

    [MenuItem(Menu2)]
    static void BuildPrefabs() => Furniture2();

    [MenuItem(Menu3)]
    static void LaySheets() => Shared.Step(Title3, Label3, Survey3, Change3, Photos3, guards: true, rebake: false, roomPhotos: false);

    [MenuItem(Menu1Undo)]
    static void UndoOne() => Shared.UndoStep(Title1, Label1, run => RestoreFromNotes(run, Title1), rebake: true);

    [MenuItem(Menu3Undo)]
    static void UndoThree() => Shared.UndoStep(Title3, Label3, null, rebake: false);

    [MenuItem(Menu0, true)]
    [MenuItem(Menu1, true)]
    [MenuItem(Menu2, true)]
    [MenuItem(Menu3, true)]
    [MenuItem(Menu1Undo, true)]
    [MenuItem(Menu3Undo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the kinds (his prices, r2; the spec's §2.1-2.2)

    sealed class KindSpec
    {
        public string asset, display, zone;
        public int price, seats, day;
        public bool wall;
        public string[] variants = new string[0];
    }

    // The strip's order: by the day they come on sale (the tables first on Day 1, then the plant), then price. THE ASSET NAMES
    // ARE SAVE IDENTITY: never rename them. A kind's zone is its bought seats' zone (the labs' and reports' word).
    static readonly KindSpec[] Kinds =
    {
        new KindSpec { asset = "Bistro table", display = "Bistro table", price = 80, seats = 2, day = 1, zone = "bistro" },
        new KindSpec { asset = "Square table", display = "Square table", price = 90, seats = 2, day = 1, zone = "square table" },
        new KindSpec { asset = "Plant", display = "Plant", price = 35, seats = 0, day = 1, zone = "",
                       variants = new[] { "Monstera", "Rubber plant", "Fiddle-leaf fig", "Olive tree" } },
        new KindSpec { asset = "Banquette bay", display = "Banquette bay", price = 140, seats = 2, day = 2, wall = true, zone = "banquette" },
        new KindSpec { asset = "Window counter", display = "Window counter", price = 150, seats = 3, day = 2, wall = true, zone = "window counter" },
        new KindSpec { asset = "Lounge corner", display = "Lounge corner", price = 160, seats = 2, day = 3, zone = "lounge" },
        new KindSpec { asset = "Communal table", display = "Communal table", price = 360, seats = 8, day = 3, zone = "communal table" },
    };

    static KindSpec KindOf(string asset) => Kinds.First(k => k.asset == asset);

    // ================================================================== the café's own pieces (paths under the café's root)

    sealed class PieceSpec
    {
        public string root;                       // the piece root's name: its save id is "scene:" + this. NEVER RENAME
        public string kind, variant = "";
        public string[] moveWhole;                // its set: groups (or objects) moved under the root whole
        public string pivotFrom;                  // the part whose place on the floor is the pivot
        public bool wall;                         // a wall piece: the root turns as its pivot part does
        public string[] napkins = new string[0];  // its tables' napkin dispensers (under Lived in/Napkins)
        public bool bay;                          // a banquette bay: two end caps and a back board
        public bool open;                         // on in a new game (his answer of 24 Oct, r1)
        public string sourceOf = "";              // Furniture 2 copies this piece for this prefab
        // filled by the survey and the change
        public readonly List<Transform> parts = new List<Transform>();
        public readonly List<Collider> colliders = new List<Collider>();
        public readonly List<TableSeat> seats = new List<TableSeat>();
        public Transform pivotPart, board;
        public Vector3 pivot;
        public float yaw;
        public PlacedPiece made;
        public GameObject capLow, capHigh;
    }

    static string Tops(string which) => $"{Seats1}/Bistro 2-top ({which})";

    // Plants first: two of them stand inside the lounge's group, which then moves whole.
    static List<PieceSpec> MakePieces() => new List<PieceSpec>
    {
        Plant("Plant, fig in the front-left corner", Room1Plants + "/CK_FiddleLeafFig (front-left corner)", "Fiddle-leaf fig", true, "Plant - Fiddle-leaf fig"),
        Plant("Plant, monstera at the counter's west end", Room1Plants + "/CK_Monstera (the counter's west end)", "Monstera", true, "Plant - Monstera"),
        Plant("Plant, olive by the door (east)", Room1Plants + "/CK_OliveTree (door, east)", "Olive tree", true, ""),
        Plant("Plant, olive by the door (west)", Room1Plants + "/CK_OliveTree (door, west)", "Olive tree", true, "Plant - Olive tree"),
        Plant("Plant, rubber plant between the bistros", Room1Plants + "/CK_RubberPlant (between the bistro tables)", "Rubber plant", false, "Plant - Rubber plant"),
        Plant("Plant, monstera by the lounge", Lounge + "/CK_Monstera (lounge)", "Monstera", true, ""),
        Plant("Plant, rubber plant front right", Touches + "/CK_RubberPlant (touch 1)", "Rubber plant", true, ""),
        Plant("Plant, rubber plant front left", Touches + "/CK_RubberPlant (touch 2)", "Rubber plant", false, ""),
        new PieceSpec { root = "Bistro table, south", kind = "Bistro table", moveWhole = new[] { Tops("south") }, sourceOf = "Bistro table",
                        pivotFrom = Tops("south") + "/CK_Table_BistroRound (bistro 2-top, south)", napkins = new[] { "LI_NapkinDispenser (bistro 2-top, south)" } },
        new PieceSpec { root = "Bistro table, north", kind = "Bistro table", moveWhole = new[] { Tops("north") }, open = true,
                        pivotFrom = Tops("north") + "/CK_Table_BistroRound (bistro 2-top, north)", napkins = new[] { "LI_NapkinDispenser (bistro 2-top, north)" } },
        Bay("south", ""), Bay("middle", "Banquette bay"), Bay("north", ""),
        new PieceSpec { root = "Communal table, west of the aisle", kind = "Communal table", moveWhole = new[] { Seats1 + "/Communal table" },
                        pivotFrom = Seats1 + "/Communal table/CK_CommunalTable (communal table)", sourceOf = "Communal table",
                        napkins = new[] { "LI_NapkinDispenser (communal table, north)", "LI_NapkinDispenser (communal table, south)" } },
        new PieceSpec { root = "Window counter, east wall", kind = "Window counter", moveWhole = new[] { Seats1 + "/Window counter" }, wall = true, open = true,
                        pivotFrom = Seats1 + "/Window counter/CK_WindowCounter (window counter)", sourceOf = "Window counter",
                        napkins = new[] { "LI_NapkinDispenser (window counter)" } },
        new PieceSpec { root = "Lounge corner, the reading corner", kind = "Lounge corner", moveWhole = new[] { ReadingCorner },
                        pivotFrom = ReadingCorner + "/Lounge table" },
        new PieceSpec { root = "Lounge corner, by the counter", kind = "Lounge corner", moveWhole = new[] { Lounge }, open = true,
                        pivotFrom = Lounge + "/Lounge table (lounge)", sourceOf = "Lounge corner" },
    };

    static PieceSpec Plant(string root, string path, string variant, bool open, string source) =>
        new PieceSpec { root = root, kind = "Plant", variant = variant, moveWhole = new[] { path }, pivotFrom = path, open = open, sourceOf = source };

    static PieceSpec Bay(string which, string source) => new PieceSpec
    {
        root = "Banquette bay, " + which, kind = "Banquette bay", bay = true, wall = true, sourceOf = source,
        moveWhole = new[] { $"{Banquette}/CK_BanquetteBay (banquette, {which} bay)", $"{Seats1}/Banquette 2-top ({which})" },
        pivotFrom = $"{Banquette}/CK_BanquetteBay (banquette, {which} bay)", napkins = new[] { $"LI_NapkinDispenser (banquette 2-top, {which})" },
    };

    // The banquette run's two ends (Seats 1's), which become the end bays' outer caps; the other four are made.
    const string SouthEnd = Banquette + "/CK_BanquetteEnd (banquette, south end)", NorthEnd = Banquette + "/CK_BanquetteEnd (banquette, north end)";
    const string CapPiece = "CK_BanquetteEnd";
    /// <summary>A cap stands 2 cm outside its bay's end: it is 4 cm thick, centred on its origin (Seats 1's ends at 3.62 and 9.96).</summary>
    const float CapOut = .02f;
    // The bay's back board (a56), in the bay's own frame (the kit's: x across, y up, +z into the room, the back at z = 0):
    // across the bay less 2 mm a side, from the base's top (0.342) to the top roll's underside (0.734), 12 mm thick, 3 mm off
    // the wall. The channel back's seams are 62-112 mm off the wall (cafe_seating.py, back_base_y), so it never shows in front.
    static readonly Vector3 BoardCentre = new Vector3(0f, .538f, .009f), BoardSize = new Vector3(2.096f, .392f, .012f);
    const string BoardName = "Back board (a56: the banquette's missing back)";

    // The window counter's stools sit (c12).
    static readonly string[] CounterStools =
        { Seats1 + "/Window counter/Seat (window counter, south)", Seats1 + "/Window counter/Seat (window counter, middle)", Seats1 + "/Window counter/Seat (window counter, north)" };
    public const float StoolLift = .32f, StoolApproach = .60f;

    // ================================================================== the dust sheets (the spec's §1.1, §4.7)

    sealed class SheetSpec
    {
        public string id, label;
        public float x0, x1, z0, z1, boxX, boxZ, boxYaw;
        public Rect Area => Rect.MinMaxRect(x0, z0, x1, z1);
    }

    // Where the closed sets stand, clear of the open pieces, the waiting nook, the aisle and the walks to the queue (checked
    // by Furniture 3 and, before it was written, by the rules' Python twin against the scene).
    static readonly SheetSpec[] SheetPlan =
    {
        new SheetSpec { id = "sheet-1", label = "the west banquette, south", x0 = -7.30f, x1 = -4.95f, z0 = 3.30f, z1 = 6.75f, boxX = -6.55f, boxZ = 4.45f, boxYaw = 12f },
        new SheetSpec { id = "sheet-2", label = "the west banquette, north", x0 = -7.30f, x1 = -4.95f, z0 = 6.85f, z1 = 10.00f, boxX = -6.70f, boxZ = 8.95f, boxYaw = -8f },
        new SheetSpec { id = "sheet-3", label = "the communal table", x0 = -4.40f, x1 = -1.75f, z0 = 4.15f, z1 = 8.25f, boxX = -3.10f, boxZ = 5.30f, boxYaw = 20f },
        new SheetSpec { id = "sheet-4", label = "the reading corner", x0 = -6.50f, x1 = -4.40f, z0 = .30f, z1 = 2.85f, boxX = -5.85f, boxZ = 1.55f, boxYaw = -15f },
        new SheetSpec { id = "sheet-5", label = "the south bistro", x0 = 1.55f, x1 = 3.75f, z0 = 3.40f, z1 = 6.65f, boxX = 2.70f, boxZ = 4.80f, boxYaw = 6f },
    };
    static readonly Color Canvas = new Color(0xCE / 255f, 0xC3 / 255f, 0xAE / 255f);   // warm canvas, about #CEC3AE (the spec's §4.7)
    // The boxes' solid and carving box, in the stack's own frame: the bottom box (0.56 x 0.44) and the two above it, turned
    // 9 and -7 degrees, together; 1.14 m tall. They sit on the cloth.
    static readonly Vector3 BoxesCentre = new Vector3(0f, .57f, 0f), BoxesSize = new Vector3(.58f, 1.14f, .48f);
    const float ClothRest = .006f;

    // ================================================================== Furniture 0 and 1: the survey

    sealed class Plan
    {
        public readonly List<PieceSpec> pieces = new List<PieceSpec>();
        public readonly Dictionary<Transform, PieceSpec> partOf = new Dictionary<Transform, PieceSpec>();
        public readonly Dictionary<Transform, (Vector3 at, Quaternion turn)> poses = new Dictionary<Transform, (Vector3, Quaternion)>();
        public readonly Dictionary<TableSeat, (Vector3 stand, Vector3 sit, SeatTag tags, string zone)> seats =
            new Dictionary<TableSeat, (Vector3, Vector3, SeatTag, string)>();
        public readonly List<TableSeat> stools = new List<TableSeat>();
        public readonly List<NavMeshModifier> innerModifiers = new List<NavMeshModifier>();
        public Transform lamp, southEnd, northEnd, gameManager;
        public Vector3 lampAt;
        public Quaternion lampTurn;
        public Material dark;
    }

    static Plan plan;

    static void Survey(Shared.Run run)
    {
        plan = null;
        if (Shared.Done(run, Title1)) { SurveyConverted(run); return; }
        run.Require(Shared.Done(run, CafeSeatsSteps.Title), $"'{CafeSeatsSteps.Title}' hasn't run in this scene: the café's sets are its.");
        run.Require(Shared.Done(run, CafeLivedInSteps.Title), $"'{CafeLivedInSteps.Title}' hasn't run in this scene: the reading corner and the napkins are its.");
        run.Require(Shared.Done(run, CafeDressing3Steps.LoungeTitle), $"'{CafeDressing3Steps.LoungeTitle}' hasn't run in this scene: the lounge is its.");
        run.Require(Shared.Done(run, SeatTagsSteps.Title), $"Run '{SeatTagsSteps.Title}' first: the pieces keep the tags it gives their seats.");
        run.Require(Shared.Done(run, ArtTidySteps.Title), $"Run '{ArtTidySteps.Title}' first: the coats come out before the pieces are copied for the Shop.");
        run.Require(Shared.Find(run, FurnitureGroup) == null, $"'{FurnitureGroup}' is already under the café's root, with no record of {Title1}: nothing was changed.");
        run.Require(Shared.InScene<CafeLayout>(run).Length == 0, "The scene already has a CafeLayout: Furniture 1 adds its own.");
        run.Require(Shared.InScene<PlacedPiece>(run).Length == 0, "The scene already has placed pieces: Furniture 1 makes them.");
        SaveManager save = Shared.InScene<SaveManager>(run).FirstOrDefault();
        run.Require(save != null, "No SaveManager in the scene: the café's layout goes beside it (GameManager).");
        NavMeshSurface[] surfaces = Shared.InScene<NavMeshSurface>(run, false);
        run.Require(surfaces.Length == 1, $"Expected one NavMeshSurface (the café's routes), found {surfaces.Length}.");
        NavMeshSurface routes = surfaces[0];
        run.Require(routes.ignoreNavMeshObstacle, "The routes' NavMeshSurface bakes NavMeshObstacles in: the pieces' carving boxes would leave holes where they were baked. Tick its Ignore NavMesh Obstacle first.");
        run.Info($"The routes: one NavMeshSurface on '{routes.name}' (collects {routes.collectObjects}, {routes.useGeometry}, voxel {(routes.overrideVoxelSize ? Shared.F(routes.voxelSize) : "default")}, " +
                 "ignores NavMeshObstacles): the pieces leave the bake (NavMeshModifier Ignore From Build) and carve their own holes at run time.");

        var p = new Plan { gameManager = save.transform };
        plan = p;
        p.lamp = Shared.Need(run, LoungeLamp);
        p.lampAt = p.lamp.position;
        p.lampTurn = p.lamp.rotation;
        p.southEnd = Shared.Need(run, SouthEnd);
        p.northEnd = Shared.Need(run, NorthEnd);
        p.dark = AssetDatabase.LoadAssetAtPath<Material>(DarkBoardPath);
        run.Require(p.dark != null, $"{DarkBoardPath} isn't in the project: the bays' back boards wear it.");
        run.Require(AssetDatabase.LoadAssetAtPath<GameObject>(Shared.KitModels + "/" + CapPiece + ".fbx") != null, $"{Shared.KitModels}/{CapPiece}.fbx isn't imported: run Kit 1 first.");

        foreach (PieceSpec spec in MakePieces())
        {
            foreach (string path in spec.moveWhole) spec.parts.Add(Shared.Need(run, path));
            foreach (string n in spec.napkins) spec.parts.Add(Shared.Need(run, Napkins + "/" + n));
            spec.pivotPart = Shared.Need(run, spec.pivotFrom);
            Vector3 at = spec.pivotPart.position;
            spec.pivot = new Vector3(at.x, 0f, at.z);
            spec.yaw = spec.wall ? FurnitureRules.Quarter(Shared.Facing(spec.pivotPart.rotation)) : 0f;
            if (spec.wall) run.Require(FurnitureRules.OnASideWall(spec.pivot, spec.yaw), $"{spec.root}: its back isn't on a side wall facing the room ({Shared.V(spec.pivot)}, {Shared.F(spec.yaw, "0")} degrees).");
            p.pieces.Add(spec);
        }
        foreach (PieceSpec spec in p.pieces)
            foreach (Transform part in spec.parts)
            {
                run.Require(!p.partOf.ContainsKey(part), $"'{Shared.PathOf(part)}' would go in two pieces.");
                run.Require(!PrefabUtility.IsPartOfPrefabInstance(part.gameObject) || PrefabUtility.IsOutermostPrefabInstanceRoot(part.gameObject),
                            $"'{Shared.PathOf(part)}' is inside a prefab instance: it can't be moved under a piece.");
                p.partOf[part] = spec;
            }

        // What each piece is made of: everything under its parts that no nearer piece (a plant in the lounge) or the lamp owns.
        foreach (PieceSpec spec in p.pieces)
            foreach (Transform t in Owned(spec))
            {
                p.poses[t] = (t.position, t.rotation);
                foreach (Collider c in t.GetComponents<Collider>()) if (IsSolid(c)) spec.colliders.Add(c);
                TableSeat seat = t.GetComponent<TableSeat>();
                if (seat != null) spec.seats.Add(seat);
                foreach (NavMeshModifier m in t.GetComponents<NavMeshModifier>()) if (m.enabled) p.innerModifiers.Add(m);
            }
        foreach (PieceSpec spec in p.pieces)
            foreach (TableSeat s in spec.seats)
                p.seats[s] = (s.StandPoint.position, s.SeatPose.position, s.Tags, s.Zone);
        foreach (string path in CounterStools)
        {
            TableSeat seat = Shared.Need(run, path).GetComponent<TableSeat>();
            run.Require(seat != null, $"'{path}' has no TableSeat.");
            p.stools.Add(seat);
        }

        // The report: what moves where.
        run.Line();
        run.Line($"The café's own pieces ({p.pieces.Count}), under {FurnitureGroup}/{OwnGroup}:");
        foreach (PieceSpec spec in p.pieces)
        {
            run.Info($"{spec.root}: a {spec.kind}{(spec.variant.Length > 0 ? $" ({spec.variant})" : "")}, id \"scene:{spec.root}\", pivot ({Shared.F(spec.pivot.x)}, {Shared.F(spec.pivot.z)})" +
                     $"{(spec.wall ? $", against its wall at {Shared.F(spec.yaw, "0")} degrees" : "")}, {spec.seats.Count} seat(s), {spec.colliders.Count} solid(s); " +
                     $"{(spec.open ? "on in a new game" : "under a dust sheet in a new game")}");
            foreach (Transform part in spec.parts) run.Line($"          moves {Shared.PathOf(part)}");
        }
        run.Info($"The lounge's floor lamp stays where it stands, the room's: {Shared.PathOf(p.lamp)} moves to {FurnitureGroup}/{FixedGroup} (Not Walkable for the routes, as its group was).");
        run.Info($"The banquette run's ends become the end bays' outer caps ({Shared.PathOf(p.southEnd)}, {Shared.PathOf(p.northEnd)}); four caps are made for the joins, shown only where no bay joins.");
        foreach (NavMeshModifier m in p.innerModifiers)
            run.Info($"{Shared.PathOf(m.transform)}'s own NavMeshModifier (Not Walkable) is switched off: inside a piece only its root's counts (Ignore From Build).");
        int statics = p.pieces.Sum(s => Owned(s).Count(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0));
        run.Info($"{statics} part(s) carry static flags, to be cleared.");
        run.Info("The window counter's stools today: " + string.Join("; ", p.stools.Select(s => $"{s.name}: Snap To Seat {(s.SnapToSeat ? "on" : "off")}, Max Lift {Shared.F(s.MaxLift, "0.00")}, Approach Side {Shared.F(s.ApproachSide, "0.00")}")) +
                 $"; after: on, {Shared.F(StoolLift, "0.00")}, {Shared.F(StoolApproach, "0.00")} (c12).");
        int pieceSeats = p.pieces.Sum(s => s.seats.Count);
        run.Check(pieceSeats == PieceSeats, $"the pieces bring {pieceSeats} seats (the repair bar's 3 aren't furniture): {PieceSeats} expected");

        // The rules, asked before anything changes (the same questions the change's checks ask again after).
        var notFixed = new List<Collider>();
        var notFixedSeats = new List<TableSeat>();
        foreach (PieceSpec spec in p.pieces) { notFixed.AddRange(spec.colliders); notFixedSeats.AddRange(spec.seats); }
        notFixed.AddRange(p.southEnd.GetComponentsInChildren<Collider>(true));
        notFixed.AddRange(p.northEnd.GetComponentsInChildren<Collider>(true));
        FurnitureRules.FloorMap map = FurnitureRules.BuildMap(notFixed, notFixedSeats);
        run.Line();
        run.Line("The rules on today's café and on the half-finished one (before):");
        RulesReport(run, map, p.pieces.Select(s => s.root).ToList(), p.pieces.Select(SurveyShape).ToList(), p.pieces.Select(s => s.open).ToList(), "before");
    }

    // Everything under a piece's parts that it owns (the nearest part above it is one of its own, and it isn't the lamp).
    static IEnumerable<Transform> Owned(PieceSpec spec)
    {
        foreach (Transform part in spec.parts)
            foreach (Transform t in part.GetComponentsInChildren<Transform>(true))
                if (OwnerOf(t) == spec) yield return t;
    }

    static PieceSpec OwnerOf(Transform t)
    {
        for (Transform a = t; a != null; a = a.parent)
        {
            if (a == plan.lamp) return null;
            if (plan.partOf.TryGetValue(a, out PieceSpec s)) return s;
        }
        return null;
    }

    // A solid: an enabled, solid collider on something that's there, not an end cap (4 cm boards that show only at a run's
    // ends: BanquetteRun) and not a cup.
    static bool IsSolid(Collider c) =>
        c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy &&
        !c.name.StartsWith(CapPiece, StringComparison.Ordinal) && c.GetComponentInParent<DirtyCup>() == null;

    static FurnitureRules.Shape SurveyShape(PieceSpec s) => new FurnitureRules.Shape
    {
        name = KindOf(s.kind).display,
        solids = s.colliders.Select(c => c.bounds).ToList(),
        stands = s.seats.Select(t => t.StandPoint.position).ToList(),
        parts = s.colliders.Select(c => c.name.StartsWith("CK_BanquetteBay", StringComparison.Ordinal) ? FurnitureRules.BayPart : c.name).ToList(),
        againstAWall = s.wall,
    };

    // Every piece against the rest, today's café as a whole, and the half-finished café as a whole (the rules' replicas of the
    // two guards: FurnitureRules R6a and R6b).
    static void RulesReport(Shared.Run run, FurnitureRules.FloorMap map, List<string> names, List<FurnitureRules.Shape> shapes, List<bool> open, string when,
                            List<FurnitureRules.Shape> extraSolids = null)
    {
        run.Info($"The floor map ({map.how}, {map.milliseconds:0} ms): {map.FixedCount} fixed solids, {map.fixedSeatStands.Count} seat(s) that aren't furniture, " +
                 $"{map.waiting.Count} waiting spot(s), {map.queue.Count} queue place(s), {map.stations.Count} station(s); the door at {Shared.V(map.door)}, Ace from {Shared.V(map.aceStart)}.");
        int fit = 0;
        for (int i = 0; i < shapes.Count; i++)
        {
            List<FurnitureRules.Shape> rest = shapes.Where((s, j) => j != i).ToList();
            FurnitureRules.Result r = FurnitureRules.Check(map, rest, shapes[i]);
            if (r.Fits) fit++;
            else run.Info($"{names[i]} against the rest: {r}");
        }
        run.Check(fit == shapes.Count, $"{when}: every piece of the café's own passes the rules against the rest ({fit} of {shapes.Count})");
        FurnitureRules.Result all = FurnitureRules.CheckWholeLayout(map, shapes);
        int seats = shapes.Sum(s => s.stands.Count) + map.fixedSeatStands.Count;
        run.Check(seats == AllSeats, $"{when}: today's café has {seats} seats ({AllSeats} expected)");
        run.Check(all.Fits && all.reached == all.places && all.served == all.servedOf,
                  $"{when}: today's café passes both guards' replicas: {all.reached} of {all.places} places reached from the door, {all.served} of {all.servedOf} served with every one taken" +
                  (all.Fits ? "" : $" ({all})"));
        List<FurnitureRules.Shape> half = shapes.Where((s, j) => open[j]).ToList();
        int halfSeats = half.Sum(s => s.stands.Count) + map.fixedSeatStands.Count;
        run.Check(halfSeats == NewGameSeats, $"{when}: the half-finished café opens {halfSeats} seats (his answer of 24 Oct, r1: {NewGameSeats})");
        if (extraSolids != null) half.AddRange(extraSolids);
        FurnitureRules.Result h = FurnitureRules.CheckWholeLayout(map, half);
        run.Check(h.Fits && h.reached == h.places && h.served == h.servedOf,
                  $"{when}: the half-finished café{(extraSolids != null && extraSolids.Count > 0 ? $" (its {extraSolids.Count} box stacks as solids)" : "")} passes both: " +
                  $"{h.reached} of {h.places} reached, {h.served} of {h.servedOf} served" + (h.Fits ? "" : $" ({h})"));
    }

    // Furniture 0 after Furniture 1: the pieces as they are.
    static void SurveyConverted(Shared.Run run)
    {
        CafeLayout layout = Shared.InScene<CafeLayout>(run).FirstOrDefault();
        run.Require(layout != null, $"{Title1} is recorded, but the scene has no CafeLayout.");
        IReadOnlyList<PlacedPiece> own = layout.OwnPieces;
        run.Info($"{Title1} is done: {own.Count} pieces of the café's own, {own.Sum(x => x != null ? x.SeatCount : 0)} seats on them, {layout.Opening.Count} on in a new game; " +
                 $"{layout.Sheets.Count} dust sheet(s); the catalogue {(layout.Catalogue != null ? AssetDatabase.GetAssetPath(layout.Catalogue) : "MISSING")}.");
        foreach (PlacedPiece piece in own)
            if (piece != null)
                run.Info($"{piece.Id}: {(piece.Kind != null ? piece.Kind.name : "NO KIND")}, pivot ({Shared.F(piece.Pivot.x)}, {Shared.F(piece.Pivot.z)}), {Shared.F(piece.Yaw, "0")} degrees, " +
                         $"{piece.SeatCount} seat(s), {piece.SolidColliders.Count} solid(s), {(piece.On ? "on" : "off")}");
        FurnitureCatalogue cat = layout.Catalogue;
        if (cat != null)
            foreach (FurnitureDefinition k in cat.kinds)
                if (k != null) run.Info($"kind {k.name}: ${k.price}, {k.seats} seat(s), from Day {k.forSaleFromDay}, prefab {(k.PrefabFor(k.FirstVariant) != null ? "built" : "not built yet (Furniture 2)")}");
        run.Info(Shared.Done(run, Title3) ? $"{Title3} is done." : $"{Title3} hasn't run yet.");
        FurnitureRules.FloorMap map = FurnitureRules.BuildMap();
        List<PlacedPiece> pieces = own.Where(x => x != null).ToList();
        RulesReport(run, map, pieces.Select(x => x.Id).ToList(), pieces.Select(ShapeOf).ToList(), pieces.Select(x => layout.Opening.Contains(x)).ToList(), "now",
                    layout.Sheets.Where(s => s != null).Select(BoxesShape).ToList());
    }

    static FurnitureRules.Shape ShapeOf(PlacedPiece piece)
    {
        FurnitureRules.Shape s = piece.Shape;
        s.againstAWall = piece.Kind != null && piece.Kind.againstAWall;
        return s;
    }

    // ================================================================== Furniture 1: the change

    static void Change1(Shared.Run run)
    {
        Plan p = plan ?? throw new InvalidOperationException("The survey didn't run.");
        Dictionary<string, FurnitureDefinition> kinds = EnsureKinds();
        FurnitureCatalogue catalogue = EnsureCatalogue(kinds);
        Shared.Note(run, $"The kinds and the catalogue: {DataFolder} ({kinds.Count} kinds, {CataloguePath}); assets, kept by an undo.");

        Transform furniture = Shared.Group(run, run.Cafe, FurnitureGroup);
        Transform own = Shared.Group(run, furniture, OwnGroup);
        Transform bought = Shared.Group(run, furniture, BoughtGroup);
        Transform fixedRoom = Shared.Group(run, furniture, FixedGroup);
        NavMeshModifier notWalkable = Shared.AddComponent<NavMeshModifier>(run, fixedRoom.gameObject);
        notWalkable.overrideArea = true;
        notWalkable.area = 1;              // Not Walkable, as the groups it came from (Playtest 4 Producer 2)
        notWalkable.ignoreFromBuild = false;
        notWalkable.applyToChildren = true;
        EditorUtility.SetDirty(notWalkable);

        // The lounge's lamp stays the room's.
        Shared.Move(run, p.lamp, p.lamp.position, p.lamp.rotation, "the lounge's floor lamp stays where it stands, the room's (it stands in Ace's way from the counter)", fixedRoom);

        // Each set becomes one piece (plants first: two stand in the lounge's group).
        foreach (PieceSpec spec in p.pieces)
        {
            var go = new GameObject(spec.root);
            go.transform.SetParent(own, false);
            go.transform.SetPositionAndRotation(spec.pivot, Quaternion.Euler(0f, spec.yaw, 0f));
            Shared.Made(run, go);
            foreach (Transform part in spec.parts)
                Shared.Move(run, part, part.position, part.rotation, $"part of the piece '{spec.root}'", go.transform);
            spec.made = Shared.AddComponent<PlacedPiece>(run, go);
            spec.made.Init(kinds[spec.kind], "scene:" + spec.root, true, 1, spec.variant);
            NavMeshModifier m = Shared.AddComponent<NavMeshModifier>(run, go);
            m.ignoreFromBuild = true;      // out of the bake: it carves its own hole (§4.3)
            m.applyToChildren = true;
            m.overrideArea = false;
            EditorUtility.SetDirty(m);
        }
        foreach (NavMeshModifier m in p.innerModifiers)
            Shared.Toggle(run, m, false, "inside a piece only its root's NavMeshModifier counts (Ignore From Build): the nearest modifier wins");

        // The bays: their caps and their back boards.
        foreach (PieceSpec spec in p.pieces.Where(s => s.bay))
        {
            Transform root = spec.made.transform;
            Collider boardBox = spec.colliders.First(c => c.name.StartsWith("CK_BanquetteBay", StringComparison.Ordinal));
            float zMin = boardBox.bounds.min.z, zMax = boardBox.bounds.max.z;
            GameObject south, north;
            if (spec.root.EndsWith("south", StringComparison.Ordinal))
            {
                Shared.Move(run, p.southEnd, p.southEnd.position, p.southEnd.rotation, "the run's south end: the south bay's outer cap", root);
                south = p.southEnd.gameObject;
            }
            else south = Shared.Place(run, CapPiece, root, new Vector3(spec.pivot.x, 0f, zMin - CapOut), spec.yaw, $"{CapPiece} ({spec.root.ToLowerInvariant()}, south cap)");
            if (spec.root.EndsWith("north", StringComparison.Ordinal))
            {
                Shared.Move(run, p.northEnd, p.northEnd.position, p.northEnd.rotation, "the run's north end: the north bay's outer cap", root);
                north = p.northEnd.gameObject;
            }
            else north = Shared.Place(run, CapPiece, root, new Vector3(spec.pivot.x, 0f, zMax + CapOut), spec.yaw, $"{CapPiece} ({spec.root.ToLowerInvariant()}, north cap)");
            // The cap at the root's local -X end is its "low" one (BanquetteRun doesn't care which: it reads where they stand).
            bool southLow = root.InverseTransformPoint(south.transform.position).x < 0f;
            spec.capLow = southLow ? south : north;
            spec.capHigh = southLow ? north : south;
            spec.board = BackBoard(run, root, p.dark).transform;
        }

        // c12: the window counter's stools sit.
        foreach (TableSeat seat in p.stools)
        {
            var so = new SerializedObject(seat);
            run.Record.notes.Add($"{NoteUndo}seat|{Key(run.Cafe, seat.transform)}|{(so.FindProperty("snapToSeat").boolValue ? 1 : 0)}|" +
                                 $"{Shared.F(so.FindProperty("maxLift").floatValue, "0.0000")}|{Shared.F(so.FindProperty("approachSide").floatValue, "0.0000")}");
            so.FindProperty("snapToSeat").boolValue = true;
            so.FindProperty("maxLift").floatValue = StoolLift;
            so.FindProperty("approachSide").floatValue = StoolApproach;
            so.ApplyModifiedProperties();
            run.Info($"{seat.name}: Snap To Seat on, Max Lift {Shared.F(StoolLift, "0.00")}, Approach Side {Shared.F(StoolApproach, "0.00")} (c12: the window counter's stools sit).");
        }

        // No static flag on any part (and the note to put them back after a save).
        int statics = 0;
        foreach (PieceSpec spec in p.pieces) statics += ClearStatic(run, spec.made.transform);
        run.Info($"Static flags cleared on {statics} part(s).");

        // Each piece's seats, solids and carving boxes.
        foreach (PieceSpec spec in p.pieces) WirePiece(spec.made, spec.capLow, spec.capHigh, run);

        // The café's layout, beside SaveManager.
        CafeLayout layout = Shared.AddComponent<CafeLayout>(run, p.gameManager.gameObject);
        PlacedPiece[] ownPieces = p.pieces.Select(s => s.made).ToArray();
        PlacedPiece[] opening = p.pieces.Where(s => s.open).Select(s => s.made).ToArray();
        layout.Wire(catalogue, ownPieces, opening, new DustSheet[0], bought);
        EditorUtility.SetDirty(layout);
        BanquetteRun.Refresh(ownPieces);
        Physics.SyncTransforms();

        Checks1(run, p, layout, kinds, fixedRoom);
    }

    // The bay's back (a56): a thin dark board where the channel back has no faces, no collider (it is inside the bay's own).
    static GameObject BackBoard(Shared.Run run, Transform root, Material dark)
    {
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(board.GetComponent<Collider>());
        board.name = BoardName;
        board.transform.SetParent(root, false);
        board.transform.localPosition = BoardCentre;
        board.transform.localRotation = Quaternion.identity;
        board.transform.localScale = BoardSize;
        var r = board.GetComponent<MeshRenderer>();
        r.sharedMaterial = dark;
        r.shadowCastingMode = ShadowCastingMode.On;
        Shared.Made(run, board);
        return board;
    }

    // Clears every static flag under a piece (noting each in the step's record, so its undo can put them back after a save).
    static int ClearStatic(Shared.Run run, Transform root)
    {
        int cleared = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            if (flags == 0) continue;
            if (run != null)
            {
                run.Record.notes.Add($"{NoteUndo}static|{Key(run.Cafe, t)}|{(int)flags}");
                Undo.RecordObject(t.gameObject, run.Title);
            }
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            cleared++;
        }
        return cleared;
    }

    // A piece's seats (all under it), solids (its colliders that count) and one carving box per solid (made fresh).
    static void WirePiece(PlacedPiece piece, GameObject low, GameObject high, Shared.Run run)
    {
        Transform root = piece.transform;
        foreach (NavMeshObstacle old in root.GetComponentsInChildren<NavMeshObstacle>(true))
            if (old != null && old.transform.parent == root && old.name.StartsWith("Carve - ", StringComparison.Ordinal))
            {
                if (run != null) Undo.DestroyObjectImmediate(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }
        TableSeat[] seats = root.GetComponentsInChildren<TableSeat>(true);
        Collider[] solids = root.GetComponentsInChildren<Collider>(true).Where(IsSolid).ToArray();
        var carving = new List<NavMeshObstacle>();
        foreach (Collider c in solids) carving.Add(Carve(root, c, run));
        if (run != null) Undo.RecordObject(piece, run.Title);
        piece.Wire(seats, solids, carving.ToArray(), low, high);
        piece.ForgetShape();
        EditorUtility.SetDirty(piece);
    }

    // A solid's carving box, the same box (round solids a capsule), in the piece's frame (§4.3).
    static NavMeshObstacle Carve(Transform root, Collider c, Shared.Run run)
    {
        PlacedPiece.BoxIn(root, c, out Bounds box);
        var go = new GameObject("Carve - " + c.name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        NavMeshObstacle o = go.AddComponent<NavMeshObstacle>();
        bool round = c is CapsuleCollider || c is SphereCollider;
        o.shape = round ? NavMeshObstacleShape.Capsule : NavMeshObstacleShape.Box;
        o.center = box.center;
        if (round)
        {
            o.radius = Mathf.Max(box.extents.x, box.extents.z);
            o.height = box.size.y;
        }
        else o.size = box.size;
        o.carving = true;
        o.carveOnlyStationary = false;     // carve as soon as it's on (as CafeLab's route block does)
        if (run != null) Undo.RegisterCreatedObjectUndo(go, run.Title);
        return o;
    }

    static void Checks1(Shared.Run run, Plan p, CafeLayout layout, Dictionary<string, FurnitureDefinition> kinds, Transform fixedRoom)
    {
        run.Line();
        List<PlacedPiece> pieces = p.pieces.Select(s => s.made).ToList();
        run.Check(pieces.Count == 17 && pieces.All(x => x != null && x.Kind != null && x.FromScene),
                  $"{pieces.Count} pieces of the café's own, each with its kind: {string.Join(", ", Kinds.Select(k => $"{pieces.Count(x => x != null && x.Kind == kinds[k.asset])} {k.asset.ToLowerInvariant()}"))}");
        run.Check(pieces.Select(x => x.Id).Distinct().Count() == pieces.Count && pieces.All(x => x.Id == "scene:" + x.name), "every piece's id is \"scene:\" and its root's own name");

        int movedOff = 0;
        foreach (KeyValuePair<Transform, (Vector3 at, Quaternion turn)> kv in p.poses)
            if (kv.Key == null || Vector3.Distance(kv.Key.position, kv.Value.at) > .001f || Quaternion.Angle(kv.Key.rotation, kv.Value.turn) > .1f) movedOff++;
        run.Check(movedOff == 0, $"every part is where it was ({p.poses.Count} objects under the pieces; {movedOff} not where they were)");
        run.Check(p.lamp.parent == fixedRoom && Vector3.Distance(p.lamp.position, p.lampAt) < .001f && Quaternion.Angle(p.lamp.rotation, p.lampTurn) < .1f,
                  $"the lounge's lamp stands where it stood, under {FurnitureGroup}/{FixedGroup}");

        int seatsOff = 0, tagsOff = 0;
        foreach (var kv in p.seats)
        {
            TableSeat s = kv.Key;
            if (Vector3.Distance(s.StandPoint.position, kv.Value.stand) > .001f || Vector3.Distance(s.SeatPose.position, kv.Value.sit) > .001f) seatsOff++;
            if (s.Tags != kv.Value.tags || s.Zone != kv.Value.zone) { tagsOff++; run.Info($"{s.name}: tags {s.Tags} (were {kv.Value.tags}), zone '{s.Zone}' (was '{kv.Value.zone}')"); }
        }
        run.Check(p.seats.Count == PieceSeats && seatsOff == 0, $"the {p.seats.Count} seats on the pieces stand and sit where they did (1 mm; {seatsOff} moved)");
        run.Check(tagsOff == 0, $"every seat's tags and zone read as Seat tags 1 left them ({tagsOff} changed)");
        int seatsInCafe = Shared.InScene<TableSeat>(run, false).Count(s => s.isActiveAndEnabled);
        run.Check(seatsInCafe == AllSeats, $"{seatsInCafe} seats in the café ({AllSeats}: the pieces' {PieceSeats} and the repair bar's 3)");
        run.Check(pieces.Sum(x => x.SeatCount) == PieceSeats, $"the pieces count {pieces.Sum(x => x.SeatCount)} seats");

        run.Check(p.stools.All(s => s.SnapToSeat && Mathf.Abs(s.MaxLift - StoolLift) < 1e-4f && Mathf.Abs(s.ApproachSide - StoolApproach) < 1e-4f && !s.RepairBar),
                  $"the window counter's three stools sit (c12): Snap To Seat on, Max Lift {Shared.F(StoolLift, "0.00")}, Approach Side {Shared.F(StoolApproach, "0.00")}, not repair-bar stools");

        int statics = pieces.Sum(x => x.GetComponentsInChildren<Transform>(true).Count(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0));
        run.Check(statics == 0, $"no static flag on any part of a piece ({statics} left)");
        int carveOff = pieces.Count(x => x.Obstacles.Count != x.SolidColliders.Count || x.Obstacles.Any(o => o == null || !o.carving || o.carveOnlyStationary));
        run.Check(carveOff == 0 && pieces.All(x => x.SolidColliders.Count > 0),
                  $"every piece has one carving box per solid, carving at once ({pieces.Sum(x => x.Obstacles.Count)} boxes for {pieces.Sum(x => x.SolidColliders.Count)} solids)");
        int modifiersOff = pieces.Count(x =>
        {
            NavMeshModifier root = x.GetComponent<NavMeshModifier>();
            bool inner = x.GetComponentsInChildren<NavMeshModifier>(true).Any(m => m != root && m.enabled);
            return root == null || !root.ignoreFromBuild || !root.applyToChildren || inner;
        });
        run.Check(modifiersOff == 0, "every piece's root is out of the bake (NavMeshModifier: Ignore From Build, applied to its children), and no other modifier inside counts");

        List<PieceSpec> bays = p.pieces.Where(s => s.bay).ToList();
        run.Check(bays.All(s => s.made.IsBay && s.capLow != null && s.capHigh != null && s.board != null && s.board.GetComponent<MeshRenderer>().sharedMaterial == p.dark),
                  "each bay has its two caps and its dark back board (a56)");
        int capsShown = bays.Sum(s => (s.capLow.activeSelf ? 1 : 0) + (s.capHigh.activeSelf ? 1 : 0));
        run.Check(capsShown == 2 && p.southEnd.gameObject.activeSelf && p.northEnd.gameObject.activeSelf,
                  $"the run shows its two ends only: {capsShown} of 6 caps shown (the joins' four hidden: BanquetteRun)");

        run.Check(layout != null && layout.Catalogue != null && layout.OwnPieces.Count == pieces.Count && layout.Opening.Count == p.pieces.Count(s => s.open),
                  $"CafeLayout on '{p.gameManager.name}': the catalogue, the {layout.OwnPieces.Count} pieces, the new game's {layout.Opening.Count}");
        int openSeats = layout.Opening.Sum(x => x.SeatCount) + 3;
        run.Check(openSeats == NewGameSeats, $"a new game opens {openSeats} seats: the repair bar's 3, " +
                                             string.Join(", ", layout.Opening.Where(x => x.SeatCount > 0).Select(x => $"{x.name} {x.SeatCount}")) +
                                             $"; and {layout.Opening.Count(x => x.SeatCount == 0)} plants (his answer of 24 Oct, r1)");

        // The rules on the pieces as they are now.
        FurnitureRules.FloorMap map = FurnitureRules.BuildMap();
        run.Line("The rules on today's café and on the half-finished one (after):");
        RulesReport(run, map, pieces.Select(x => x.Id).ToList(), pieces.Select(ShapeOf).ToList(), p.pieces.Select(s => s.open).ToList(), "after");
        Shared.Note(run, $"{pieces.Count} pieces under {FurnitureGroup}/{OwnGroup}; {p.stools.Count} window-counter stools sit (c12); the bays' back boards (a56); the lamp under {FurnitureGroup}/{FixedGroup}.");
    }

    static void Photos1(Shared.Run run)
    {
        Shared.GameCameraPhoto(run, "game-camera");
        // The bays' backs, from the street side with the walls in the way hidden (a56).
        Shared.Photo(run, "banquette-backs", new Vector3(-11f, 3.2f, 6.8f), new Vector3(-7f, .45f, 6.8f), 38f, isometric: true);
    }

    // ================================================================== Furniture 1's undo: what the record can't hold

    // The shared undo puts back every move, toggle and component; the static flags and the stools' fields come back from the
    // record's notes, first, while everything is still where the step left it.
    static string RestoreFromNotes(Shared.Run run, string title)
    {
        CafeRedesignRecord r = Shared.RecordOf(run.Cafe, title);
        if (r == null) return null;
        int statics = 0, stools = 0, missing = 0;
        foreach (string note in r.notes)
        {
            if (note == null || !note.StartsWith(NoteUndo, StringComparison.Ordinal)) continue;
            string[] f = note.Substring(NoteUndo.Length).Split('|');
            if (f.Length < 3) continue;
            Transform t = Resolve(run.Cafe, f[1]);
            if (t == null) { missing++; continue; }
            if (f[0] == "static")
            {
                Undo.RecordObject(t.gameObject, title + " - Undo");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)int.Parse(f[2], CultureInfo.InvariantCulture));
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                statics++;
            }
            else if (f[0] == "seat" && f.Length >= 5)
            {
                TableSeat seat = t.GetComponent<TableSeat>();
                if (seat == null) { missing++; continue; }
                var so = new SerializedObject(seat);
                so.FindProperty("snapToSeat").boolValue = f[2] == "1";
                so.FindProperty("maxLift").floatValue = float.Parse(f[3], CultureInfo.InvariantCulture);
                so.FindProperty("approachSide").floatValue = float.Parse(f[4], CultureInfo.InvariantCulture);
                so.ApplyModifiedProperties();
                stools++;
            }
        }
        run.Line($"From the record's notes: {statics} static flag(s) put back, {stools} window-counter stool(s) as they were" +
                 (missing > 0 ? $"; {missing} noted object(s) no longer in the scene" : "") + ".");
        return null;
    }

    // A path from the café's root that survives same-named siblings: "<index>:<name>/...".
    static string Key(Transform cafe, Transform t)
    {
        var parts = new List<string>();
        for (Transform a = t; a != null && a != cafe; a = a.parent) parts.Add(a.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + ":" + a.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static Transform Resolve(Transform cafe, string key)
    {
        Transform at = cafe;
        foreach (string part in key.Split('/'))
        {
            int colon = part.IndexOf(':');
            if (colon < 0 || at == null) return null;
            int index = int.Parse(part.Substring(0, colon), CultureInfo.InvariantCulture);
            string name = part.Substring(colon + 1);
            Transform next = index < at.childCount && at.GetChild(index).name == name ? at.GetChild(index) : null;
            if (next == null)
                foreach (Transform c in at) if (c.name == name) { next = c; break; }
            at = next;
        }
        return at;
    }

    // ================================================================== the kinds and the catalogue (assets)

    static Dictionary<string, FurnitureDefinition> EnsureKinds()
    {
        EnsureFolder(DataFolder);
        var kinds = new Dictionary<string, FurnitureDefinition>();
        foreach (KindSpec k in Kinds)
        {
            string path = $"{DataFolder}/{k.asset}.asset";
            FurnitureDefinition d = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<FurnitureDefinition>();
                AssetDatabase.CreateAsset(d, path);
            }
            d.displayName = k.display;
            d.price = k.price;
            d.seats = k.seats;
            d.forSaleFromDay = k.day;
            d.againstAWall = k.wall;
            d.zone = k.zone;
            if (k.variants.Length > 0) d.variantNames = k.variants.ToArray();
            EditorUtility.SetDirty(d);
            kinds[k.asset] = d;
        }
        AssetDatabase.SaveAssets();
        return kinds;
    }

    static FurnitureCatalogue EnsureCatalogue(Dictionary<string, FurnitureDefinition> kinds)
    {
        FurnitureCatalogue c = AssetDatabase.LoadAssetAtPath<FurnitureCatalogue>(CataloguePath);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<FurnitureCatalogue>();
            AssetDatabase.CreateAsset(c, CataloguePath);
        }
        c.kinds = Kinds.Select(k => kinds[k.asset]).ToArray();
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        return c;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ================================================================== Furniture 2: the prefabs (assets)

    static void Furniture2()
    {
        var report = new StringBuilder();
        int failures = 0;
        string folder = null, tag = "[" + Title2 + "] ";
        void Line(string text = "") => report.AppendLine(text);
        void Info(string text) => report.AppendLine("  info  " + text);
        void Check(bool ok, string text)
        {
            report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + text);
            if (!ok) failures++;
        }

        Scene preview = default;
        bool previewOpen = false;
        string verdict;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Scene scene = SceneManager.GetActiveScene();
            GameObject cafeGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Shared.CafeRootName);
            if (cafeGo == null) throw new InvalidOperationException($"'{Shared.CafeRootName}' isn't in the open scene.");
            Transform cafe = cafeGo.transform;
            if (Shared.RecordOf(cafe, Title1) == null) throw new InvalidOperationException($"Run {Title1} first: the prefabs are copied from the café's own pieces.");
            CafeLayout layout = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CafeLayout>(true)).FirstOrDefault();
            if (layout == null) throw new InvalidOperationException("The scene has no CafeLayout: run Furniture 1 again.");
            folder = Path.Combine(Shared.LogRoot, Label2 + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            Line(Title2);
            Line(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Line();

            EnsureFolder(PrefabFolder);
            Dictionary<string, FurnitureDefinition> kinds = EnsureKinds();
            FurnitureCatalogue catalogue = EnsureCatalogue(kinds);
            Material ghost = EnsureGhostMaterial();
            Dictionary<string, Material> palette = AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Material>()
                .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            Material fabric = AssetDatabase.LoadAssetAtPath<Material>(FabricPath);
            Info($"The café palette: {palette.Count} materials ({PalettePath}); the kit's fabric: {(fabric != null ? FabricPath : "missing")}.");

            Transform own = cafe.Find(FurnitureGroup + "/" + OwnGroup);
            if (own == null) throw new InvalidOperationException($"{FurnitureGroup}/{OwnGroup} isn't under the café's root.");
            PlacedPiece Source(string root)
            {
                Transform t = own.Find(root);
                PlacedPiece piece = t != null ? t.GetComponent<PlacedPiece>() : null;
                if (piece == null) throw new InvalidOperationException($"The piece '{root}' isn't under {FurnitureGroup}/{OwnGroup}.");
                return piece;
            }
            var sceneSeatNames = new HashSet<string>(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TableSeat>(true)).Select(s => s.name));

            preview = EditorSceneManager.NewPreviewScene();
            previewOpen = true;
            int swaps = 0;
            var built = new Dictionary<string, GameObject>();
            var sources = new Dictionary<string, PlacedPiece>();
            foreach (PieceSpec spec in MakePieces().Where(s => s.sourceOf.Length > 0))
            {
                PlacedPiece source = Source(spec.root);
                FurnitureDefinition kind = kinds[spec.kind];
                GameObject copy = CopyFor(preview, source, spec.sourceOf, kind, spec.variant);
                if (spec.bay)
                    foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith(CapPiece, StringComparison.Ordinal)) t.gameObject.SetActive(true);   // a bay alone is finished both ends
                swaps += Finish(copy, kind, spec.variant, palette, fabric, report);
                built[spec.sourceOf] = Save(copy, spec.sourceOf);
                sources[spec.sourceOf] = source;
            }
            // The square table: the south banquette 2-top's table and café chair, and a second chair turned to face it.
            {
                PlacedPiece bay = Source("Banquette bay, south");
                FurnitureDefinition kind = kinds["Square table"];
                GameObject copy = SquareTable(preview, bay, kind);
                swaps += Finish(copy, kind, "", palette, fabric, report);
                built["Square table"] = Save(copy, "Square table");
            }
            Info($"{built.Count} prefabs saved under {PrefabFolder}/; {swaps} flat material(s) swapped for the café's grain.");

            // The kinds filled in, the catalogue, the ghost.
            foreach (KindSpec k in Kinds)
            {
                FurnitureDefinition d = kinds[k.asset];
                if (k.variants.Length > 0)
                {
                    d.variants = k.variants.Select(v => built["Plant - " + v]).ToArray();
                    d.variantNames = k.variants.ToArray();
                    d.prefab = d.variants[0];
                }
                else
                {
                    d.prefab = built[k.asset];
                    d.variants = new GameObject[0];
                    d.variantNames = new string[0];
                }
                EditorUtility.SetDirty(d);
            }
            catalogue.ghostMaterial = ghost;
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();

            // The checks.
            Line();
            Line("The checks:");
            foreach (KeyValuePair<string, PlacedPiece> kv in sources)
            {
                PlacedPiece template = built[kv.Key].GetComponent<PlacedPiece>();
                template.ForgetShape();
                FurnitureRules.Shape want = kv.Value.Shape, got = template.ShapeAt(kv.Value.Pivot, kv.Value.Yaw);
                bool same = SameBoxes(want.solids, got.solids) && SamePoints(want.stands, got.stands);
                Check(same, $"{kv.Key}: placed at its source's pivot ({kv.Value.name}), the same {got.solids.Count} solid(s) and {got.stands.Count} stand point(s) (1 mm)");
            }
            {
                // The square table against its source: the east chair is the source chair, the west chair its turned twin.
                PlacedPiece bay = Source("Banquette bay, south");
                Transform table = bay.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("CK_Table_Square2", StringComparison.Ordinal));
                PlacedPiece template = built["Square table"].GetComponent<PlacedPiece>();
                template.ForgetShape();
                FurnitureRules.Shape got = template.ShapeAt(new Vector3(table.position.x, 0f, table.position.z), 0f);
                Bounds tableBox = table.GetComponent<Collider>().bounds;
                Collider chair = bay.GetComponentsInChildren<Collider>(true).First(c => c.name.StartsWith("CK_Chair_Cafe", StringComparison.Ordinal));
                bool hasTable = got.solids.Any(b => SameBox(b, tableBox)), hasChair = got.solids.Any(b => SameBox(b, chair.bounds));
                float east = got.stands.Count == 2 ? got.stands.Max(s => s.x) - table.position.x : 0f;
                float west = got.stands.Count == 2 ? table.position.x - got.stands.Min(s => s.x) : 0f;
                Check(got.solids.Count == 3 && hasTable && hasChair && got.stands.Count == 2 && Mathf.Abs(east - west) < .001f,
                      $"Square table: the source's table and chair where they stand (1 mm), a second chair facing it, stand points {Shared.F(east)} m either side");
            }
            foreach (KeyValuePair<string, GameObject> kv in built)
            {
                GameObject asset = kv.Value;
                PlacedPiece piece = asset.GetComponent<PlacedPiece>();
                FurnitureDefinition kind = piece != null ? piece.Kind : null;
                List<string> flat = asset.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null && (m.name.StartsWith("CC_Wood_", StringComparison.Ordinal) || m.name == "DC_Fabric")).Select(m => m.name).Distinct().ToList();
                Check(flat.Count == 0, $"{kv.Key}: no flat wood or DC_Fabric on any renderer (a80){(flat.Count > 0 ? ": " + string.Join(", ", flat) : "")}");
                int seats = asset.GetComponentsInChildren<TableSeat>(true).Length;
                Check(kind != null && seats == kind.seats && piece.SeatCount == seats, $"{kv.Key}: {seats} seat(s), its kind's {(kind != null ? kind.seats : -1)}");
                List<string> clash = asset.GetComponentsInChildren<TableSeat>(true).Where(s => sceneSeatNames.Contains(s.name)).Select(s => s.name).ToList();
                Check(clash.Count == 0, $"{kv.Key}: no seat named as a scene seat is{(clash.Count > 0 ? ": " + string.Join(", ", clash) : "")}");
                int statics = asset.GetComponentsInChildren<Transform>(true).Count(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0);
                Check(piece != null && statics == 0 && piece.Obstacles.Count == piece.SolidColliders.Count && piece.SolidColliders.Count > 0 && !piece.FromScene,
                      $"{kv.Key}: no static flag, {(piece != null ? piece.SolidColliders.Count : 0)} solid(s) with a carving box each, a bought piece's PlacedPiece");
            }
            Check(catalogue.kinds.Length == Kinds.Length && catalogue.kinds.All(k => k != null && k.PrefabFor(k.FirstVariant) != null) && catalogue.ghostMaterial != null,
                  $"the catalogue: {catalogue.kinds.Length} kinds, each with its prefab, and the ghost's material");
            Check(Kinds.All(k => kinds[k.asset].price == k.price && kinds[k.asset].SellBack == k.price / 2),
                  "his prices (r2): " + string.Join(", ", Kinds.Select(k => $"{k.asset.ToLowerInvariant()} ${k.price}")) + "; each sells back for half (f3)");
            Line();
            Line($"Prefabs: {PrefabFolder}/. Kinds and the catalogue: {DataFolder}/. This report: {folder}.");
            verdict = failures == 0 ? "DONE. " + Title2 : $"FAILED: {failures} check(s). The prefabs are saved; fix what failed and run it again (it rebuilds them in place).";
        }
        catch (Exception e)
        {
            failures++;
            verdict = "FAILED: " + e.Message + "\n\n" + e;
        }
        finally
        {
            if (previewOpen) EditorSceneManager.ClosePreviewScene(preview);
        }
        string text = verdict + "\n\n" + report;
        try
        {
            Directory.CreateDirectory(Shared.LogRoot);
            File.WriteAllText(Path.Combine(Shared.LogRoot, Label2 + "-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (folder != null) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(tag + "Couldn't write the report: " + e.Message); }
        if (failures > 0) Debug.LogError(tag + text); else Debug.Log(tag + text);
    }

    // A copy of a piece in the preview scene, at the origin, square, with what a bought piece doesn't bring taken out.
    static GameObject CopyFor(Scene preview, PlacedPiece source, string name, FurnitureDefinition kind, string variant)
    {
        GameObject copy = Object.Instantiate(source.gameObject);
        SceneManager.MoveGameObjectToScene(copy, preview);
        copy.name = name;
        copy.SetActive(true);
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        copy.transform.localScale = Vector3.one;
        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true).Reverse())
        {
            if (t == null || t == copy.transform) continue;
            string n = t.name;
            // The coats (Art tidy 1 took them out) and the cups left behind (nothing switches them on): a new piece is clean.
            if (n.StartsWith("LI_Coat_OnChair", StringComparison.Ordinal) || n.StartsWith("LI_LeftCup", StringComparison.Ordinal)) { Object.DestroyImmediate(t.gameObject); continue; }
            // The lounge's emptied band of plants.
            if (n.StartsWith("Touches (", StringComparison.Ordinal) && t.childCount == 0) Object.DestroyImmediate(t.gameObject);
        }
        NavMeshModifier rootModifier = copy.GetComponent<NavMeshModifier>();
        foreach (NavMeshModifier m in copy.GetComponentsInChildren<NavMeshModifier>(true))
            if (m != rootModifier) Object.DestroyImmediate(m);
        copy.GetComponent<PlacedPiece>().Init(kind, "", false, 1, variant);
        return copy;
    }

    // Built from the south banquette 2-top: its table at the middle, its café chair east facing it and a second one west.
    static GameObject SquareTable(Scene preview, PlacedPiece bay, FurnitureDefinition kind)
    {
        Transform table = bay.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("CK_Table_Square2", StringComparison.Ordinal));
        Transform chair = bay.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("CK_Chair_Cafe", StringComparison.Ordinal));
        TableSeat seat = bay.GetComponentsInChildren<TableSeat>(true).First(s => s.name.EndsWith(", chair)", StringComparison.Ordinal));
        Transform napkin = bay.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("LI_NapkinDispenser", StringComparison.Ordinal));
        var root = new GameObject("Square table");
        SceneManager.MoveGameObjectToScene(root, preview);
        Vector3 middle = new Vector3(table.position.x, 0f, table.position.z);
        Transform Copy(Transform t, string name)
        {
            GameObject c = Object.Instantiate(t.gameObject);
            SceneManager.MoveGameObjectToScene(c, preview);
            c.name = name;
            c.transform.SetParent(root.transform, false);
            c.transform.localPosition = t.position - middle;
            c.transform.localRotation = t.rotation;
            c.transform.localScale = t.lossyScale;
            return c.transform;
        }
        Copy(table, "CK_Table_Square2 (square table)");
        Transform east = Copy(chair, "CK_Chair_Cafe (square table, east)");
        Transform eastSeat = Copy(seat.transform, "Seat (square table, east)");
        // The west chair: the east one turned half round about the table's middle.
        Quaternion half = Quaternion.Euler(0f, 180f, 0f);
        Transform west = Copy(chair, "CK_Chair_Cafe (square table, west)");
        west.localPosition = half * east.localPosition;
        west.localRotation = half * east.localRotation;
        Transform westSeat = Copy(seat.transform, "Seat (square table, west)");
        westSeat.localPosition = half * eastSeat.localPosition;
        westSeat.localRotation = half * eastSeat.localRotation;
        // A table in the open: each stand point straight behind its chair, 0.75 m from the seat (the bistro's), not the bay's
        // diagonal one (which keeps clear of the next bay).
        foreach (Transform s in new[] { eastSeat, westSeat })
        {
            TableSeat ts = s.GetComponent<TableSeat>();
            Transform stand = ts != null ? ts.StandPoint : null;
            if (stand != null && stand != s) { stand.localPosition = new Vector3(0f, .04f, -.75f); stand.localRotation = Quaternion.identity; }
            foreach (Transform t in s.GetComponentsInChildren<Transform>(true).Reverse())
                if (t != s && t.name.StartsWith("LI_", StringComparison.Ordinal)) Object.DestroyImmediate(t.gameObject);
            SeatTags tags = s.GetComponent<SeatTags>();
            if (tags != null) tags.Set(SeatTag.None, "square table");
        }
        if (napkin != null) Copy(napkin, "LI_NapkinDispenser (square table)");
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Reverse())
            if (t != null && t.name.StartsWith("LI_Coat_OnChair", StringComparison.Ordinal)) Object.DestroyImmediate(t.gameObject);
        PlacedPiece piece = root.AddComponent<PlacedPiece>();
        piece.Init(kind, "", false, 1, "");
        NavMeshModifier m = root.AddComponent<NavMeshModifier>();
        m.ignoreFromBuild = true;
        m.applyToChildren = true;
        return root;
    }

    // The copy made ready: its seats named as a bought piece's, the grain (a80), no static flag, wired, carving.
    static int Finish(GameObject copy, FurnitureDefinition kind, string variant, Dictionary<string, Material> palette, Material fabric, StringBuilder report)
    {
        string kindWords = (kind != null ? kind.displayName : copy.name).ToLowerInvariant();
        foreach (TableSeat s in copy.GetComponentsInChildren<TableSeat>(true))
        {
            // "Seat (bistro 2-top, south, west)" -> "Seat (bought bistro table, west)": the seat's own word is the last one.
            string inside = s.name.StartsWith("Seat (", StringComparison.Ordinal) && s.name.EndsWith(")", StringComparison.Ordinal) ? s.name.Substring(6, s.name.Length - 7) : s.name;
            string own = inside.Split(new[] { ", " }, StringSplitOptions.None).Last();
            s.name = $"Seat (bought {kindWords}, {own})";
            SeatTags tags = s.GetComponent<SeatTags>();
            if (tags != null && kind != null && !string.IsNullOrEmpty(kind.zone)) tags.Set(tags.Tags, kind.zone);
        }
        int swaps = 0;
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name;
                if (n.StartsWith("CC_Wood_", StringComparison.Ordinal) && palette.TryGetValue("Cafe grain - " + n, out Material grain)) mats[i] = grain;
                else if (n == "DC_Fabric" && fabric != null) mats[i] = fabric;
                else continue;
                changed = true;
                report.AppendLine($"  info  {copy.name}: {r.name}'s {n} became {mats[i].name} (a80: the café's grain)");
                swaps++;
            }
            if (changed) r.sharedMaterials = mats;
        }
        ClearStatic(null, copy.transform);
        PlacedPiece piece = copy.GetComponent<PlacedPiece>();
        GameObject low = null, high = null;
        foreach (Transform t in copy.transform)
        {
            if (!t.name.StartsWith(CapPiece, StringComparison.Ordinal)) continue;
            if (t.localPosition.x < 0f) low = t.gameObject; else high = t.gameObject;
        }
        piece.Init(kind, "", false, 1, variant);
        WirePiece(piece, low, high, null);
        return swaps;
    }

    static GameObject Save(GameObject copy, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(copy, path, out bool ok);
        Object.DestroyImmediate(copy);
        if (!ok || asset == null) throw new InvalidOperationException($"Couldn't save {path}.");
        return asset;
    }

    static bool SameBox(Bounds a, Bounds b) => Vector3.Distance(a.min, b.min) <= .001f && Vector3.Distance(a.max, b.max) <= .001f;

    static bool SameBoxes(List<Bounds> want, List<Bounds> got)
    {
        if (want.Count != got.Count) return false;
        var left = new List<Bounds>(got);
        foreach (Bounds w in want)
        {
            int i = left.FindIndex(g => SameBox(g, w));
            if (i < 0) return false;
            left.RemoveAt(i);
        }
        return true;
    }

    static bool SamePoints(List<Vector3> want, List<Vector3> got)
    {
        if (want.Count != got.Count) return false;
        var left = new List<Vector3>(got);
        foreach (Vector3 w in want)
        {
            int i = left.FindIndex(g => Vector3.Distance(g, w) <= .001f);
            if (i < 0) return false;
            left.RemoveAt(i);
        }
        return true;
    }

    // The ghost: the URP Lit shader, transparent (the house's recipe: SeeThroughMaterials), white, tinted by the ghost itself.
    static Material EnsureGhostMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(GhostPath);
        if (m == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader isn't in the project.");
            m = new Material(lit) { name = "Furniture ghost" };
            AssetDatabase.CreateAsset(m, GhostPath);
        }
        SetIf(m, "_Surface", 1f);
        SetIf(m, "_Blend", 0f);
        SetIf(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetIf(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetIf(m, "_SrcBlendAlpha", (float)BlendMode.One);
        SetIf(m, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetIf(m, "_ZWrite", 0f);
        SetIf(m, "_AlphaClip", 0f);
        SetIf(m, "_Smoothness", .2f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("ShadowCaster", false);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(1f, 1f, 1f, .42f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetIf(Material m, string property, float value)
    {
        if (m.HasProperty(property)) m.SetFloat(property, value);
    }

    // ================================================================== Furniture 3: the dust sheets

    static CafeLayout sheetLayout;

    static void Survey3(Shared.Run run)
    {
        sheetLayout = null;
        run.Require(Shared.Done(run, Title1), $"Run {Title1} first: the sheets lie where its pieces stand, and its CafeLayout lays them.");
        CafeLayout layout = Shared.InScene<CafeLayout>(run).FirstOrDefault();
        run.Require(layout != null, "The scene has no CafeLayout: undo Furniture 1 and run it again.");
        run.Require(layout.Sheets.All(s => s == null), $"The layout already holds {layout.Sheets.Count} dust sheet(s).");
        Transform furniture = Shared.Need(run, FurnitureGroup);
        run.Require(furniture.Find(SheetsGroup) == null, $"{FurnitureGroup}/{SheetsGroup} is already there.");
        run.Require(AssetDatabase.LoadAssetAtPath<GameObject>(BoxStackModel) != null, $"{BoxStackModel} isn't imported (Grace's cover, Blender kit).");
        sheetLayout = layout;

        FurnitureRules.FloorMap map = FurnitureRules.BuildMap();
        List<PlacedPiece> open = layout.Opening.Where(x => x != null).ToList();
        List<PlacedPiece> closed = layout.OwnPieces.Where(x => x != null && !open.Contains(x)).ToList();
        run.Line();
        run.Line($"The five sheets, where the closed pieces stand ({closed.Count} of them, {closed.Sum(x => x.SeatCount)} seats):");
        foreach (SheetSpec s in SheetPlan)
        {
            Bounds area = AreaBox(s);
            List<string> under = closed.Where(x => Overlaps(x.FootprintAt(x.Pivot, x.Yaw), area)).Select(x => x.name).ToList();
            run.Info($"{s.id} over {s.label}: x {Shared.F(s.x0, "0.00")}..{Shared.F(s.x1, "0.00")}, z {Shared.F(s.z0, "0.00")}..{Shared.F(s.z1, "0.00")}; " +
                     $"boxes at ({Shared.F(s.boxX, "0.00")}, {Shared.F(s.boxZ, "0.00")}); covers {(under.Count > 0 ? string.Join(", ", under) : "nothing")}");
            SheetClearances(run, s, map, open);
        }
        run.Line("The rules on the half-finished café with the boxes (before):");
        List<FurnitureRules.Shape> withBoxes = open.Select(ShapeOf).Concat(SheetPlan.Select(PlannedBoxes)).ToList();
        FurnitureRules.Result h = FurnitureRules.CheckWholeLayout(map, withBoxes);
        run.Check(h.Fits && h.reached == h.places && h.served == h.servedOf,
                  $"the half-finished café with the five box stacks as solids passes both guards' replicas: {h.reached} of {h.places} reached, {h.served} of {h.servedOf} served" + (h.Fits ? "" : $" ({h})"));
    }

    static Bounds AreaBox(SheetSpec s)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(s.x0, 0f, s.z0), new Vector3(s.x1, .05f, s.z1));
        return b;
    }

    static bool Overlaps(Bounds a, Bounds b) => a.max.x > b.min.x && a.min.x < b.max.x && a.max.z > b.min.z && a.min.z < b.max.z;

    // A sheet keeps off the open pieces (their solids and stand points), the waiting spots, the aisle and the walks to the
    // queue, the queue's places and the door, and every fixed solid taller than a rug.
    static void SheetClearances(Shared.Run run, SheetSpec s, FurnitureRules.FloorMap map, List<PlacedPiece> open)
    {
        Bounds area = AreaBox(s);
        var hits = new List<string>();
        foreach (PlacedPiece x in open)
        {
            FurnitureRules.Shape shape = x.Shape;
            if (shape.solids.Any(b => Overlaps(b, area))) hits.Add(x.name);
            if (shape.stands.Any(p => FurnitureRules.Flat(p, area) < .30f)) hits.Add(x.name + "'s seats");
        }
        foreach (Vector3 w in map.waiting) if (FurnitureRules.Flat(w, area) < .60f) hits.Add($"the waiting spot at ({Shared.F(w.x, "0.0")}, {Shared.F(w.z, "0.0")})");
        foreach (Vector3 q in map.queue)
        {
            if (FurnitureRules.Flat(q, area) < FurnitureRules.KeepQueue) hits.Add("the queue");
            if (FurnitureRules.OnAWalkToTheQueue(area, q)) hits.Add("the walk to the queue");
        }
        if (area.max.x > FurnitureRules.LaneX0 && area.min.x < FurnitureRules.LaneX1) hits.Add("the aisle");
        if (FurnitureRules.Flat(map.door, area) < FurnitureRules.KeepDoor) hits.Add("the door");
        for (int i = 0; i < map.fixedSolids.Count; i++)
            if (!map.fixedIsWall[i] && map.fixedSolids[i].max.y > .06f && Overlaps(map.fixedSolids[i], area)) hits.Add(map.fixedNames[i]);
        foreach (Vector3 st in map.fixedSeatStands) if (FurnitureRules.Flat(st, area) < .30f) hits.Add("the repair bar's seats");
        Vector2 box = new Vector2(s.boxX, s.boxZ);
        bool boxesOn = s.Area.Contains(box + new Vector2(-.35f, -.35f)) && s.Area.Contains(box + new Vector2(.35f, .35f));
        run.Check(hits.Count == 0 && boxesOn, $"{s.id} keeps clear of the open pieces, the waiting spots, the aisle, the queue, the door and the room's fixed things, its boxes on it" +
                                             (hits.Count > 0 ? ": it meets " + string.Join(", ", hits.Distinct()) : "") + (boxesOn ? "" : "; its boxes hang off it"));
    }

    // The box stack's solid where it will stand (its collider's box, turned with it), for the rules.
    static FurnitureRules.Shape PlannedBoxes(SheetSpec s)
    {
        Quaternion turn = Quaternion.Euler(0f, s.boxYaw, 0f);
        Vector3 at = new Vector3(s.boxX, ClothRest, s.boxZ);
        var b = new Bounds(at + turn * BoxesCentre, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = BoxesCentre + Vector3.Scale(BoxesSize * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            b.Encapsulate(at + turn * c);
        }
        return new FurnitureRules.Shape { name = "boxes", solids = new List<Bounds> { b }, stands = new List<Vector3>(), parts = new List<string> { "boxes" } };
    }

    // A laid sheet's boxes, for the rules (its collider's world box, read from its size: the sheet may be off in the scene).
    static FurnitureRules.Shape BoxesShape(DustSheet sheet)
    {
        BoxCollider c = sheet.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault();
        var solids = new List<Bounds>();
        if (c != null)
        {
            Matrix4x4 m = c.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(c.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(m.MultiplyPoint3x4(c.center + Vector3.Scale(c.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            solids.Add(b);
        }
        return new FurnitureRules.Shape { name = "boxes", solids = solids, stands = new List<Vector3>(), parts = solids.Select(_ => "boxes").ToList() };
    }

    static void Change3(Shared.Run run)
    {
        CafeLayout layout = sheetLayout ?? throw new InvalidOperationException("The survey didn't run.");
        Material canvas = EnsureCanvasMaterial();
        GameObject boxModel = AssetDatabase.LoadAssetAtPath<GameObject>(BoxStackModel);
        Transform furniture = Shared.Need(run, FurnitureGroup);
        Transform group = Shared.Group(run, furniture, SheetsGroup);
        var made = new List<DustSheet>();
        for (int i = 0; i < SheetPlan.Length; i++)
        {
            SheetSpec s = SheetPlan[i];
            Vector2 middle = s.Area.center;
            var go = new GameObject($"Dust sheet {i + 1} ({s.label})");
            go.transform.SetParent(group, false);
            go.transform.SetPositionAndRotation(new Vector3(middle.x, 0f, middle.y), Quaternion.identity);
            Shared.Made(run, go);
            DustSheet sheet = Shared.AddComponent<DustSheet>(run, go);
            sheet.Init(s.id, s.Area);
            EditorUtility.SetDirty(sheet);

            // The cloth: no collider (it never blocks anyone, or the ghost).
            var cloth = new GameObject("Canvas drop cloth");
            cloth.transform.SetParent(go.transform, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = ClothMesh(s, i + 1);
            var r = cloth.AddComponent<MeshRenderer>();
            r.sharedMaterial = canvas;
            r.shadowCastingMode = ShadowCastingMode.Off;
            Undo.RegisterCreatedObjectUndo(cloth, run.Title);

            // The boxes on it: a collider and a carving box (Ace shouldn't walk through boxes); they go with their sheet.
            var stack = (GameObject)PrefabUtility.InstantiatePrefab(boxModel, go.transform);
            stack.name = "Boxes (GH_BoxStack)";
            stack.transform.SetPositionAndRotation(new Vector3(s.boxX, ClothRest, s.boxZ), Quaternion.Euler(0f, s.boxYaw, 0f));
            Undo.RegisterCreatedObjectUndo(stack, run.Title);
            BoxCollider solid = Undo.AddComponent<BoxCollider>(stack);
            solid.center = BoxesCentre;
            solid.size = BoxesSize;
            var carve = new GameObject("Carve - boxes");
            carve.transform.SetParent(stack.transform, false);
            NavMeshObstacle o = carve.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box;
            o.center = BoxesCentre;
            o.size = BoxesSize;
            o.carving = true;
            o.carveOnlyStationary = false;
            Undo.RegisterCreatedObjectUndo(carve, run.Title);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

            // Off in the scene: the runtime lays them for a new game (CafeLayout.Apply).
            go.SetActive(false);
            Shared.AddToList(run, layout, "sheets", sheet, "a dust sheet for the half-finished café (new game)");
            made.Add(sheet);
        }
        Physics.SyncTransforms();

        // The checks.
        run.Line();
        run.Check(made.Count == SheetPlan.Length && made.Select(x => x.Id).Distinct().Count() == made.Count && made.All(x => !x.gameObject.activeSelf),
                  $"{made.Count} dust sheets ({string.Join(", ", made.Select(x => x.Id))}), each its own id, switched off in the scene");
        run.Check(layout.Sheets.Count == made.Count && made.All(x => layout.Sheets.Contains(x)), "the café's layout holds them (lying in a new game, gone in an old save)");
        run.Check(made.All(x => x.GetComponentsInChildren<Collider>(true).Length == 1 && x.GetComponentsInChildren<NavMeshObstacle>(true).Length == 1),
                  "each has one solid (its boxes) and one carving box; the cloth has no collider");
        run.Check(made.All(x => x.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0)), "each has its cloth");
        FurnitureRules.FloorMap map = FurnitureRules.BuildMap();
        List<PlacedPiece> open = layout.Opening.Where(x => x != null).ToList();
        List<FurnitureRules.Shape> withBoxes = open.Select(ShapeOf).Concat(made.Select(BoxesShape)).ToList();
        FurnitureRules.Result h = FurnitureRules.CheckWholeLayout(map, withBoxes);
        run.Check(h.Fits && h.reached == h.places && h.served == h.servedOf,
                  $"the half-finished café with its five box stacks as solids passes both guards' replicas: {h.reached} of {h.places} reached, {h.served} of {h.servedOf} served" + (h.Fits ? "" : $" ({h})"));
        foreach (SheetSpec s in SheetPlan) SheetClearances(run, s, map, open);
        Shared.Note(run, $"{made.Count} dust sheets under {FurnitureGroup}/{SheetsGroup}, off in the scene; a new game lays them.");
    }

    // After: the half-finished café, switched to for the photos and back (nothing is kept).
    static void Photos3(Shared.Run run)
    {
        if (run.Phase != "2-after") { Shared.GameCameraPhoto(run, "game-camera"); return; }
        CafeLayout layout = Shared.InScene<CafeLayout>(run).FirstOrDefault();
        if (layout == null) return;
        var changed = new List<(GameObject go, bool was)>();
        void Set(GameObject go, bool on)
        {
            if (go == null || go.activeSelf == on) return;
            changed.Add((go, go.activeSelf));
            go.SetActive(on);
        }
        try
        {
            foreach (PlacedPiece piece in layout.OwnPieces) if (piece != null) Set(piece.gameObject, layout.Opening.Contains(piece));
            foreach (DustSheet s in layout.Sheets) if (s != null) Set(s.gameObject, true);
            Physics.SyncTransforms();
            Shared.GameCameraPhoto(run, "half-finished-game-camera");
            Shared.Photo(run, "half-finished-overhead", new Vector3(0f, 21f, 6.2f), new Vector3(0f, 0f, 6.6f), 48f, isometric: true);
            Shared.Photo(run, "half-finished-sheets-close", new Vector3(-.8f, 3.6f, -.6f), new Vector3(-4.2f, .2f, 5.6f), 50f, isometric: true);
        }
        finally
        {
            for (int i = changed.Count - 1; i >= 0; i--) if (changed[i].go != null) changed[i].go.SetActive(changed[i].was);
            Physics.SyncTransforms();
        }
    }

    // The canvas: URP Lit, warm canvas, a fine weave (a texture made here), matte.
    static Material EnsureCanvasMaterial()
    {
        EnsureFolder(DataFolder);
        Texture2D weave = AssetDatabase.LoadAssetAtPath<Texture2D>(WeavePath);
        if (weave == null)
        {
            weave = Weave(128);
            AssetDatabase.CreateAsset(weave, WeavePath);
        }
        Material m = AssetDatabase.LoadAssetAtPath<Material>(CanvasPath);
        if (m == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader isn't in the project.");
            m = new Material(lit) { name = "Dust sheet canvas" };
            AssetDatabase.CreateAsset(m, CanvasPath);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Canvas);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", weave);
        SetIf(m, "_Smoothness", .08f);
        SetIf(m, "_Metallic", 0f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // A canvas weave, near white (the material's colour is the canvas): warp and weft threads with a little slub.
    static Texture2D Weave(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Dust sheet canvas weave", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        var rnd = new System.Random(1023);
        var slubX = new float[size];
        var slubY = new float[size];
        for (int i = 0; i < size; i++) { slubX[i] = (float)rnd.NextDouble() * .06f; slubY[i] = (float)rnd.NextDouble() * .06f; }
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool over = ((x / 2) + (y / 2)) % 2 == 0;                      // plain weave: over one, under one
                float thread = over ? .96f - slubY[y] : .90f - slubX[x];
                float edge = (x % 2 == 0 ? .985f : 1f) * (y % 2 == 0 ? .985f : 1f);
                float v = Mathf.Clamp01(thread * edge + ((float)rnd.NextDouble() - .5f) * .03f);
                px[y * size + x] = new Color(v, v, v * .99f, 1f);
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    // A drop cloth over a sheet's area, in its own frame (the area's middle): soft folds across it, a little ruck round the
    // boxes, and a hem that falls to the floor. Saved into Dust sheet cloths.asset (replaced in place on a rerun).
    static Mesh ClothMesh(SheetSpec s, int seed)
    {
        float w = s.x1 - s.x0, d = s.z1 - s.z0;
        int nx = Mathf.CeilToInt(w / .08f) + 1, nz = Mathf.CeilToInt(d / .08f) + 1;
        var rnd = new System.Random(seed * 7919);
        var folds = new List<(Vector2 dir, float at, float width, float height)>();
        for (int i = 0; i < 4; i++)
        {
            float a = (float)(rnd.NextDouble() * Math.PI);
            folds.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)), ((float)rnd.NextDouble() - .5f) * Mathf.Min(w, d) * .8f,
                       .14f + .16f * (float)rnd.NextDouble(), .010f + .016f * (float)rnd.NextDouble()));
        }
        Vector2 boxes = new Vector2(s.boxX - (s.x0 + s.x1) * .5f, s.boxZ - (s.z0 + s.z1) * .5f);
        var verts = new Vector3[nx * nz];
        var uvs = new Vector2[nx * nz];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float u = ix / (float)(nx - 1), v = iz / (float)(nz - 1);
                float x = -w * .5f + u * w, z = -d * .5f + v * d;
                float y = ClothRest;
                foreach (var f in folds)
                {
                    float t = Vector2.Dot(new Vector2(x, z), f.dir) - f.at;
                    y += f.height * Mathf.Exp(-(t * t) / (f.width * f.width));
                }
                float r = Vector2.Distance(new Vector2(x, z), boxes);
                y += .012f * Mathf.Exp(-((r - .40f) * (r - .40f)) / (.06f * .06f));      // rucked up round the boxes' feet
                y += .0025f * Mathf.Sin(x * 9.1f + seed) * Mathf.Sin(z * 7.3f + seed * .7f);
                float edge = Mathf.Min(Mathf.Min(u, 1f - u) * w, Mathf.Min(v, 1f - v) * d);
                y = Mathf.Lerp(.002f, y, Mathf.Clamp01(edge / .06f));                       // the hem on the floor
                verts[iz * nx + ix] = new Vector3(x, y, z);
                uvs[iz * nx + ix] = new Vector2((x + w * .5f) * 2f, (z + d * .5f) * 2f);   // the weave twice a metre
            }
        var tris = new int[(nx - 1) * (nz - 1) * 6];
        int k = 0;
        for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int i0 = iz * nx + ix, i1 = i0 + 1, i2 = i0 + nx, i3 = i2 + 1;
                tris[k++] = i0; tris[k++] = i2; tris[k++] = i1;   // facing up (clockwise seen from above)
                tris[k++] = i1; tris[k++] = i2; tris[k++] = i3;
            }
        var mesh = new Mesh { name = "Dust sheet cloth - " + s.id };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return SaveClothMesh(mesh);
    }

    static Mesh SaveClothMesh(Mesh mesh)
    {
        Mesh existing = AssetDatabase.LoadAllAssetsAtPath(ClothMeshPath).OfType<Mesh>().FirstOrDefault(m => m.name == mesh.name);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }
        if (AssetDatabase.LoadMainAssetAtPath(ClothMeshPath) == null) AssetDatabase.CreateAsset(mesh, ClothMeshPath);
        else AssetDatabase.AddObjectToAsset(mesh, ClothMeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }
}
#endif
