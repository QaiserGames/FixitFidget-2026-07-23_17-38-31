#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;
using Kit = CafeLivedInSteps;

// ---------------------------------------------------------------------------
// CAFÉ DRESSING 3: A LOUNGE ON THE BARE FLOOR, THE PENDANTS SMALLER AND HIGHER, SOFTER SHELF LIGHTS (19 Oct 2026; playtest 7)
//
// Her words, playtest 7 (his girlfriend, first time, on a pad): "Still looks empty, right? The café?… Like a carpet, you
// think? Yeah, maybe a carpet. I think maybe like a couch. Like, you know how people have like some areas to relax? You can
// add, like, maybe a couch table, and then… some nice lamps." Of the lamps over the tables: "You either need to make the
// ceiling… Yeah, the ceiling has to go higher… Or you need to get rid of these lamps. No, I can scale them smaller." And,
// of the light: "it probably gets dark in here though."
// The fresh-eyes reviewers (19 Oct, cafe-look, day-intro, day-ui): from the game camera about 7 in 10 pixels of the
// customer floor are bare boards; every dressing piece so far went on counters, tables or walls; the under-shelf bulbs
// behind the back bar blow the tile wall out to white; the reading corner (two mustard tub chairs, a low table, a rug and
// a lamp) is "the one spot that reads cozy from every camera".
//
//   Fixit Fidget > Cafe redesign > Café dressing 3 - A lounge on the bare floor (scene, with Undo)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - Undo the lounge (scene)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - The pendant lamps smaller and higher (scene, with Undo)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - Undo the pendants (scene)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - Softer shelf lights (scene, with Undo)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - Undo the shelf lights (scene)
//   Fixit Fidget > Cafe redesign > Café dressing 3 - Photograph the room (read-only)
//
// Every scene step runs through the café redesign's shared step (CafeRedesignShared.Step: Edit Mode only, the survey
// first and it changes nothing, one undo group with a CafeRedesignRecord on the café's root, the checks after, the photos
// before and after into Logs/CafeRedesign/<label>-<time>/, anything failed puts everything back, nothing deleted, never
// saved). Each has its own record, so its Undo puts back exactly what it did, after a save and on either machine; the
// shared rule stands: only the latest of the redesign's steps may be undone, so undo these three newest first.
//
// THE LOUNGE is the reading corner again, measured, not copied by numbers: the corner's two tub chairs facing each other
// across the low table, its lamp beside one chair on the closed side, its rug under the table and the chairs' fronts, the
// two seats' sit points, cup spots and stand points, all read off the corner as it stands (Lived in 2 built it; the NPC
// seating and the guards already live with that geometry). Added: a monstera beside the other chair, on the closed side
// (the kit's CK_Monstera; the corner has the window there). Only models already in the project: the corner's own pieces
// are copied (the same model, fabric and solid), the plant comes from the café kit. There is no sofa in the project: a
// couch is a Blender session (named in the report), not a stand-in.
// WHERE IT GOES is measured at run time, never guessed: the customer floor (the scene's Floor to the front counter's
// customers' edge), the door (the open leaves), the queue (CounterQueue's places), every seat's stand point, waiting
// spot, station stand point and spawn, every chair's solid, every collider standing on the floor. A 0.10 m map of the
// floor says where nothing may stand: colliders; the door-to-counter lane (2.0 m wide, as CafeFurnishing keeps it, more
// than the brief's 1.2) and a 1.2 m walk from the door to each queue place; the keep-clears round people's places
// (CafeFurnishing's 0.75 / 0.80 / 0.90 / 0.80 / 1.0); 0.90 m round every chair (the brief). The lounge is tried at every
// 0.10 m and four turns; the roomiest places (the most free floor round the whole group, chairs facing east-west
// preferred so both sitters show from the game camera) are tested exactly (colliders and drawn triangles against each
// piece's box, the stand points' rules: 0.40 m from any solid, 0.70 m between seats, 1.05 m from a waiting spot, 0.90 m
// from the queue), then placed one after another, the routes re-baked and BOTH guards run, until one keeps every
// customer served (the layout check and the occupied-circulation guard, every place, PASS kept): the seats are real
// seats, made the way Seats 1 and the corner make them. Then one or two TOUCHES: a rubber plant (CK_RubberPlant) in the
// next bare bands, near a wall or the front, snug but passable (0.62 to 1.05 m of free floor round it), spread from the
// other plants and the lounge, each kept only if both guards stay as they were. A magazine rack (the reviewers' other
// idea) is no model in the project: named in the report for a Blender session. The group is Not Walkable for the routes.
//
// THE PENDANTS: the eleven pendants are one pendant (the counter's four, session 6, and Room 1's seven copies over the
// tables, hung with their rims 0.95 m over the tables: 1.70 m off the floor, where a standing head walks through them).
// Each is scaled to 0.7 about its own top and raised so its shade's bottom sits at least 2.60 m over the floor, or as
// high as the ceiling deck allows with 10 cm to spare; never lowered; brought down a centimetre at a time if the raise
// would run it into a beam or a skylight well (3 cm clear). Its light rises with it, as it was (colour, strength,
// range untouched: say in the report if the tables now read dim). A table pendant's cord is re-cut from its new top to
// the ceiling where it met it; a pendant with no cord that has to rise gets one (as Room 1 cut them).
//
// THE SHELF LIGHTS: Bar 2's seven point lights under the long shelf (#FFD7A8, 0.6, range 1.2, 22 cm off the tile) each
// halved. No other light is touched (the menu board's picture light does the same above it: his call, named in the report).
// ---------------------------------------------------------------------------
internal static class CafeDressing3Steps
{
    // ---- the steps ----
    public const string LoungeTitle = "Cafe dressing 3 - A lounge on the bare floor";
    const string LoungeLabel = "cafe-dressing-3-lounge";
    public const string PendantsTitle = "Cafe dressing 3 - The pendant lamps smaller and higher";
    const string PendantsLabel = "cafe-dressing-3-pendants";
    public const string ShelfTitle = "Cafe dressing 3 - Softer shelf lights";
    const string ShelfLabel = "cafe-dressing-3-shelf-lights";
    const string PhotosTitle = "Cafe dressing 3 - Photograph the room";
    const string PhotosLabel = "dressing-3";

    const string MenuLounge = Shared.Menu + "Café dressing 3 - A lounge on the bare floor (scene, with Undo)";
    const string MenuLoungeUndo = Shared.Menu + "Café dressing 3 - Undo the lounge (scene)";
    const string MenuPendants = Shared.Menu + "Café dressing 3 - The pendant lamps smaller and higher (scene, with Undo)";
    const string MenuPendantsUndo = Shared.Menu + "Café dressing 3 - Undo the pendants (scene)";
    const string MenuShelf = Shared.Menu + "Café dressing 3 - Softer shelf lights (scene, with Undo)";
    const string MenuShelfUndo = Shared.Menu + "Café dressing 3 - Undo the shelf lights (scene)";
    const string MenuPhotos = Shared.Menu + "Café dressing 3 - Photograph the room (read-only)";

    const string LoungeGroup = "Cafe dressing 3 - lounge";
    const string TouchesGroup = "Touches (plants in the other bare bands)";

    // ---- what it stands on, by name (where each name comes from) ----
    const string LivedIn2 = "Lived in 2 - Dress the cafe";                                   // CafeLivedInSteps.Title
    const string CornerPath = Shared.RedesignGroup + "/Lived in/Reading corner";             // CafeLivedInSteps: GroupName, CornerGroup
    const string CounterName = "CK_FrontCounter (the one front counter)";                   // CafeLivedInSteps.CounterName (Bar 1's)
    const string DoorLeavesPath = "12 - authored cafe interior/Open door collision";        // CafeInteriorPass: the two open leaves
    const string DeckPath = "06 - atmosphere placeholders/Inside-only ceiling";             // CafeRoomSteps.DeckPath
    const string CounterPendantsPath = "25 - counter pendants (session 6)";                 // CafeTidySteps.PendantGroup
    const string CordName = "Table pendant cord";                                            // CafeRoomSteps
    const string ShelfLightPrefix = "Shelf light (";                                         // CafeBarSteps: "Shelf light (x -3.0)"
    const string PropsManifest = Shared.KitModels + "/cafe_props.json";                      // Kit 1 copies it beside the models
    const string LoungePlant = "CK_Monstera", TouchPlant = "CK_RubberPlant";               // cafe_props.py; capsules round the pots

    // ---- the lane and the keep-clears (CafeFurnishing.BuildKeepClear and CheckFloorClearance; CafeSeatsSteps; the brief) ----
    const float LaneHalfWidth = 1f;          // the door-to-counter lane is 2.0 m wide (CafeFurnishing: x -1..1); the brief asks for 1.2 at least
    const float LaneToCounter = .8f;         // it ends this far short of the counter's front (CafeFurnishing: z 12.5 for a front at 13.3)
    const float LaneBeforeDoor = 1.2f;       // and starts this far outside the door (the spawn stands there)
    const float QueueWalkHalf = .6f;         // the walk from the door to each queue place: 1.2 m wide (the brief)
    const float KeepSeat = .75f, KeepWaiting = .80f, KeepQueue = .90f, KeepStation = .80f, KeepSpawn = 1f;
    const float ChairRing = .90f;            // the brief: 0.9 m round every chair
    const float FreeMargin = .10f;           // nothing within a solid piece's footprint plus this (Lived in 2's rule)
    const float RugMargin = .02f;            // a rug is flat: only solids and the lane keep off it
    const float NavMargin = .40f, SeatClaim = .70f, SeatToWaiting = 1.05f, SeatToQueue = .90f;   // CafeSeatsSteps
    const float Cell = .10f;                 // the floor map's cell
    const float CentreClear = .80f;          // a lounge centre needs at least this much free floor round it before anything else is tried
    const int ExactTests = 40, LoungeTries = 6;
    const float TrySpacing = .60f;           // the places tried are at least this far apart (or turned differently)
    const float Shrink = .003f;              // a piece's box shrunk for "nothing passes through" (what it rests on only touches it)
    const int MinPixels = 12, ScreenW = 1920, ScreenH = 1080;   // Art's rule (T21): a piece reads as a shape from above
    static readonly float[] Yaws = { 90f, 270f, 0f, 180f };    // the chairs' line east-west first (both sitters show from the game camera)
    const float EastWestBonus = .05f;

    // ---- the touches ----
    const int Touches = 2, TouchTries = 6;
    const float TouchClearMin = .62f, TouchClearMax = 1.05f;   // snug but passable: the plant's leaves reach 0.34, people pass 0.60
    const float TouchNearWall = 1.3f;        // a plant stands near a wall or the front windows, never mid-floor
    const float TouchFromCounter = 1.5f;     // and never in the band in front of the counter (the player's way from the gate)
    const float TouchFromLounge = 2.5f, TouchFromPlants = 2f, TouchFromFailed = 1f;

    // ---- the pendants ----
    const float PendantScale = .7f;          // her: "No, I can scale them smaller."
    const float ShadeBottomMin = 2.6f;       // the shade's bottom at least this high: a standing head (1.65 eye, 1.8 top) walks under it
    const float CeilingSpare = .10f;         // or as high as the deck allows, with this to spare
    const float BeamClear = .03f;            // the whole lamp clears every beam and well by this (Room 1's MeshClear)
    const float CordDiameter = .008f;        // Room 1's cord

    // ---- the shelf lights ----
    const float ShelfLightFactor = .5f;      // halved (the reviewers' smallest fix)

    // ================================================================== menus

    [MenuItem(MenuLounge)]
    static void DressTheLounge()
    {
        LoungePlan plan = null;
        Shared.Step(LoungeTitle, LoungeLabel, run => plan = SurveyLounge(run), run => ChangeLounge(run, plan), run => LoungePhotos(run, plan),
                    guards: true, rebake: true, roomPhotos: true);
    }

    [MenuItem(MenuLoungeUndo)]
    static void UndoTheLounge() => Shared.UndoStep(LoungeTitle, LoungeLabel);

    [MenuItem(MenuPendants)]
    static void RaiseThePendants()
    {
        PendantPlan plan = null;
        Shared.Step(PendantsTitle, PendantsLabel, run => plan = SurveyPendants(run), run => ChangePendants(run, plan), run => PendantPhotos(run),
                    guards: false, rebake: false, roomPhotos: false);
    }

    [MenuItem(MenuPendantsUndo)]
    static void UndoThePendants() => Shared.UndoStep(PendantsTitle, PendantsLabel, rebake: false);

    [MenuItem(MenuShelf)]
    static void SoftenTheShelfLights()
    {
        ShelfPlan plan = null;
        Shared.Step(ShelfTitle, ShelfLabel, run => plan = SurveyShelf(run), run => ChangeShelf(run, plan), run => ShelfPhotos(run),
                    guards: false, rebake: false, roomPhotos: false);
    }

    [MenuItem(MenuShelfUndo)]
    static void UndoTheShelfLights() => Shared.UndoStep(ShelfTitle, ShelfLabel, rebake: false);

    [MenuItem(MenuPhotos)]
    static void PhotographTheRoom() => Shared.Check(PhotosTitle, PhotosLabel, run =>
    {
        run.Folder = NewFolder(PhotosLabel);
        run.Phase = "view";
        RoomPhotos(run);
        run.Info($"Photos: {run.Folder} (view-1-the-game-camera, view-2-the-counter-camera, view-3-from-the-door, view-4-the-waiting-nook, view-5-the-front-left, view-6-the-back-bar).");
    });

    [MenuItem(MenuLounge, true)]
    [MenuItem(MenuLoungeUndo, true)]
    [MenuItem(MenuPendants, true)]
    [MenuItem(MenuPendantsUndo, true)]
    [MenuItem(MenuShelf, true)]
    [MenuItem(MenuShelfUndo, true)]
    [MenuItem(MenuPhotos, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== THE LOUNGE: the room as measured

    sealed class Room
    {
        public float floorY;
        public Rect floor;                      // the customer floor: x and z (metres), the walls to the counter's front
        public float counterFront;              // the front counter's customers' edge (z)
        public Vector3 door, spawn;             // the doorway's middle at the threshold; where customers come in
        public readonly List<Transform> queue = new List<Transform>();
        public readonly List<(Vector3 at, float keep, string what)> people = new List<(Vector3, float, string)>();   // what solids keep clear of
        public readonly List<(Vector3 at, string what)> seatStands = new List<(Vector3, string)>();    // every seat's stand point (the claim rule)
        public readonly List<(Vector3 at, string what)> loiters = new List<(Vector3, string)>();       // every waiting spot that is on
        public readonly List<(Bounds box, string what)> chairs = new List<(Bounds, string)>();        // every chair's solid: 0.9 m round it
        public readonly List<(Vector3 at, string what)> plants = new List<(Vector3, string)>();        // the kit's plants: the touches spread from them
        public int seatsBefore, pathsBefore, targetsBefore, servedBefore, placesBefore;
        public bool allPathsBefore, allServedBefore;
        public string layoutBefore = "", circulationBefore = "";
    }

    // A 0.10 m map of the customer floor: where nothing may stand (solid: a collider; lane: the door's lane and the walks to
    // the queue; keep: a person's place's keep-clear or a chair's 0.9 m ring), and how far each cell is from the nearest
    // blocked cell (clear: from any of the three; clearSolid: from solids and the lane only, for a rug or a stand point).
    sealed class FloorMap
    {
        public readonly float x0, x1, z0, z1, y;
        public readonly int nx, nz;
        public readonly bool[] solid, lane, keep;
        public readonly float[] clear, clearSolid;

        public FloorMap(Rect floor, float y)
        {
            x0 = floor.xMin; x1 = floor.xMax; z0 = floor.yMin; z1 = floor.yMax; this.y = y;
            nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / Cell));
            nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / Cell));
            solid = new bool[nx * nz]; lane = new bool[nx * nz]; keep = new bool[nx * nz];
            clear = new float[nx * nz]; clearSolid = new float[nx * nz];
        }

        public float X(int i) => x0 + (i + .5f) * Cell;
        public float Z(int k) => z0 + (k + .5f) * Cell;
        public int Col(float x) => Mathf.FloorToInt((x - x0) / Cell);
        public int Row(float z) => Mathf.FloorToInt((z - z0) / Cell);
        public bool In(int i, int k) => i >= 0 && k >= 0 && i < nx && k < nz;
        public bool Inside(Vector3 p) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1;
        public bool LaneAt(Vector3 p) { int i = Col(p.x), k = Row(p.z); return !In(i, k) || lane[k * nx + i]; }
        public float ClearAt(Vector3 p, bool withKeep) { int i = Col(p.x), k = Row(p.z); return In(i, k) ? (withKeep ? clear : clearSolid)[k * nx + i] : 0f; }

        public void MarkDisc(bool[] map, Vector3 at, float r)
        {
            int i0 = Mathf.Max(0, Col(at.x - r)), i1 = Mathf.Min(nx - 1, Col(at.x + r)), k0 = Mathf.Max(0, Row(at.z - r)), k1 = Mathf.Min(nz - 1, Row(at.z + r));
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = X(i) - at.x, dz = Z(k) - at.z;
                    if (dx * dx + dz * dz <= r * r) map[k * nx + i] = true;
                }
        }

        public void MarkRect(bool[] map, float ax, float az, float bx, float bz, float grow)
        {
            int i0 = Mathf.Max(0, Col(ax - grow)), i1 = Mathf.Min(nx - 1, Col(bx + grow)), k0 = Mathf.Max(0, Row(az - grow)), k1 = Mathf.Min(nz - 1, Row(bz + grow));
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = Mathf.Max(ax - X(i), 0f, X(i) - bx), dz = Mathf.Max(az - Z(k), 0f, Z(k) - bz);
                    if (dx * dx + dz * dz <= grow * grow) map[k * nx + i] = true;
                }
        }

        public void MarkSegment(bool[] map, Vector3 a, Vector3 b, float halfWidth)
        {
            int i0 = Mathf.Max(0, Col(Mathf.Min(a.x, b.x) - halfWidth)), i1 = Mathf.Min(nx - 1, Col(Mathf.Max(a.x, b.x) + halfWidth));
            int k0 = Mathf.Max(0, Row(Mathf.Min(a.z, b.z) - halfWidth)), k1 = Mathf.Min(nz - 1, Row(Mathf.Max(a.z, b.z) + halfWidth));
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                    if (PointToSegment(new Vector3(X(i), 0f, Z(k)), a, b) <= halfWidth) map[k * nx + i] = true;
        }

        // The cells under a footprint grown by `grow`: the first blocked one's kind, or null.
        public string Blocked(Bounds b, float grow, bool withKeep)
        {
            if (b.min.x - grow < x0 || b.max.x + grow > x1 || b.min.z - grow < z0 || b.max.z + grow > z1) return "the edge of the customer floor";
            int i0 = Mathf.Max(0, Col(b.min.x - grow)), i1 = Mathf.Min(nx - 1, Col(b.max.x + grow)), k0 = Mathf.Max(0, Row(b.min.z - grow)), k1 = Mathf.Min(nz - 1, Row(b.max.z + grow));
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                {
                    int idx = k * nx + i;
                    if (solid[idx]) return "something solid";
                    if (lane[idx]) return "the door's lane or the walk to the queue";
                    if (withKeep && keep[idx]) return "a person's place (its keep-clear) or a chair's 0.9 m";
                }
            return null;
        }

        public void Distances()
        {
            Chamfer(clear, idx => solid[idx] || lane[idx] || keep[idx]);
            Chamfer(clearSolid, idx => solid[idx] || lane[idx]);
        }

        // Metres from each cell's centre to the nearest blocked cell's centre (two passes; outside the floor counts as a wall).
        void Chamfer(float[] d, Func<int, bool> blocked)
        {
            const float Big = 1e6f;
            float diag = Cell * 1.41421356f;
            for (int idx = 0; idx < d.Length; idx++) d[idx] = blocked(idx) ? 0f : Big;
            float At(int i, int k) => In(i, k) ? d[k * nx + i] : 0f;
            for (int k = 0; k < nz; k++)
                for (int i = 0; i < nx; i++)
                {
                    int idx = k * nx + i;
                    if (d[idx] == 0f) continue;
                    float best = Mathf.Min(At(i - 1, k) + Cell, At(i, k - 1) + Cell);
                    best = Mathf.Min(best, Mathf.Min(At(i - 1, k - 1) + diag, At(i + 1, k - 1) + diag));
                    d[idx] = Mathf.Min(d[idx], best);
                }
            for (int k = nz - 1; k >= 0; k--)
                for (int i = nx - 1; i >= 0; i--)
                {
                    int idx = k * nx + i;
                    if (d[idx] == 0f) continue;
                    float best = Mathf.Min(At(i + 1, k) + Cell, At(i, k + 1) + Cell);
                    best = Mathf.Min(best, Mathf.Min(At(i + 1, k + 1) + diag, At(i - 1, k + 1) + diag));
                    d[idx] = Mathf.Min(d[idx], best);
                }
        }
    }

    // One piece of the lounge, in the group's own frame: x across (the open side, where the stand points are, is +x), y
    // over the floor, z along the chairs' line.
    sealed class Part
    {
        public string label, name, piece;       // piece: the kit model (Shared.Place) when there is no source to copy
        public Transform source;                // the reading corner's piece, copied (the same model, fabric and solid)
        public Vector3 at;
        public Quaternion turn = Quaternion.identity;
        public Bounds local;                    // its meshes' box about its own root (the root's scale inside it)
        public bool solid, isRug, isChair, isTable, isLamp, isPlant;
        public float capsuleRadius, capsuleHeight;   // a plant's pot (CafeRoomSteps: a capsule round the pot only; the leaves none)
        public string why;
        public GameObject placed;
    }

    sealed class SeatSpec
    {
        public string name;
        public Vector3 sit, cup, stand;         // in the group's frame
        public Part chair;
        public GameObject made;
        public TableSeat seat;
    }

    sealed class Template
    {
        public readonly List<Part> parts = new List<Part>();
        public readonly List<SeatSpec> seats = new List<SeatSpec>();
        public TableSeat sample;                // the corner's seat: the new seats copy its fields
        public Vector3 cornerCentre;            // the corner's table, to spread the new lounge from it
        public IEnumerable<Part> Solids => parts.Where(p => p.solid);
    }

    sealed class Placement
    {
        public Vector3 centre;                  // on the floor, under the table's centre
        public float yaw, score;
        public Quaternion Frame => Quaternion.Euler(0f, yaw, 0f);
        public Vector3 World(Vector3 local) => centre + Frame * local;
        public Quaternion Turn(Quaternion local) => Frame * local;
        public Bounds Box(Part p) => BlenderKitSteps.Transformed(p.local, Matrix4x4.TRS(World(p.at), Turn(p.turn), Vector3.one));
        public string Where => $"({Shared.F(centre.x, "0.00")}, {Shared.F(centre.z, "0.00")}) turned {Shared.F(yaw, "0")}";
    }

    sealed class Touch
    {
        public GameObject go;
        public Vector3 at;
        public float clear, wall, yaw;
        public string name;
    }

    sealed class LoungePlan
    {
        public Room room;
        public FloorMap map;
        public Kit.Scenery scene;
        public readonly HashSet<Renderer> movers = new HashSet<Renderer>();
        public Template template;
        public readonly List<Placement> candidates = new List<Placement>();
        public Placement chosen;
        public readonly List<Touch> touches = new List<Touch>();
        public readonly List<string> tried = new List<string>();
        public Overhead view;
        public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        public CafeDaylight daylight;
        public Light lampLight;
        public int pathsAfter, targetsAfter, servedAfter, placesAfter;
        public string layoutAfter = "", circulationAfter = "";
        public string zones = "";
    }

    // ================================================================== THE LOUNGE: the survey (changes nothing; refuses with the reasons)

    static LoungePlan SurveyLounge(Shared.Run run)
    {
        var p = new LoungePlan();
        Physics.SyncTransforms();
        run.Require(Shared.Done(run, LivedIn2), $"'{LivedIn2}' hasn't run in this scene: the lounge is the reading corner's pieces again, measured off the corner.");
        run.Info("The redesign's steps in this scene: " + string.Join("; ", Shared.Records(run.Cafe).OrderBy(r => r.order).Select(r => $"{r.order}. {r.step}")) + ".");
        CafeDaylight[] daylights = Shared.InScene<CafeDaylight>(run);
        run.Require(daylights.Length == 1, $"One CafeDaylight expected in the scene (the lounge lamp's light registers with it); {daylights.Length} found.");
        p.daylight = daylights[0];

        // ---- the room ----
        run.Line();
        run.Line("The room, measured:");
        p.room = SurveyRoom(run);

        // ---- the drawn scene, for the exact tests (the street's moving actors left out, as every café step does) ----
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
                if (a != null && a.actor != null)
                    foreach (Renderer r in a.actor.GetComponentsInChildren<Renderer>(true)) p.movers.Add(r);
        p.scene = new Kit.Scenery(run, p.movers);
        run.Info($"The drawn scene read for the measuring: {p.scene.Count} renderers round the café.");

        // ---- the reading corner, measured: the template ----
        run.Line();
        run.Line("The reading corner, measured (the lounge repeats it):");
        p.template = MeasureCorner(run, p.room);

        // ---- the floor map and the bare zones ----
        run.Line();
        run.Line("The floor map (0.10 m cells; solids, the lane and the walks to the queue, the keep-clears and the chairs' 0.9 m):");
        p.map = BuildMap(run, p.room, null);
        p.zones = BareZones(p.map);
        run.Info("The biggest bare zones (the largest free discs, the game camera sees the whole customer floor): " + p.zones);

        // ---- where the lounge can go ----
        run.Line();
        run.Line("Where the lounge can go (every 0.10 m and four turns; the roomiest first; then exactly, against colliders and drawn triangles):");
        FindCandidates(run, p);
        run.Require(p.candidates.Count > 0, "No place on the customer floor holds the lounge with every rule (the map says where: " + p.zones + ").");
        foreach (Placement c in p.candidates)
            run.Info($"{c.Where}: {Shared.F(c.score, "0.00")} m of free floor round the group at least" + (c.score > 0f ? "" : " (tight)"));
        run.Info($"The step tries them in this order, re-baking the routes and running both guards after each, and keeps the first that serves every customer ({LoungeTries} at most).");

        // ---- from above: each piece's size on screen at the first place (Art's 12 px rule; judged after) ----
        p.view = HomeView(run);
        run.Line();
        run.Line($"From above ({p.view.how}): each piece's smaller side at {ScreenW} x {ScreenH} at the first place ({MinPixels} px reads as a shape):");
        foreach (Part part in p.template.parts)
        {
            Rect r = OnScreen(p.view, PartVertices(part, p.candidates[0]));
            run.Info($"{part.label}: {Shared.F(r.width, "0")} x {Shared.F(r.height, "0")} px" + (Mathf.Min(r.width, r.height) >= MinPixels ? "" : $" (UNDER {MinPixels})"));
        }

        foreach (KeyValuePair<Transform, Vector3> a in Kit.Anchors(run)) p.anchors[a.Key] = a.Value;
        run.Info($"Gameplay anchors noted (checked after, 1 mm): {p.anchors.Count}.");
        return p;
    }

    static Room SurveyRoom(Shared.Run run)
    {
        var room = new Room();
        Transform floor = run.Scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Floor");
        run.Require(floor != null, "No 'Floor' at the scene's root (the café's floor, with its collider and the NavMeshSurface).");
        room.floorY = floor.position.y;
        Bounds fb = Shared.RendererBounds(floor);
        run.Require(fb.size.x > 5f && fb.size.z > 5f, "The floor's drawn box is too small to be the café's floor: " + Shared.BoundsText(fb));
        Transform counter = Shared.Named(run, CounterName).FirstOrDefault(t => t.gameObject.activeInHierarchy);
        run.Require(counter != null, $"'{CounterName}' isn't under the café's root (Bar 1's front counter): the customer floor ends at its front.");
        room.counterFront = Shared.RendererBounds(counter).min.z;
        run.Require(room.counterFront > fb.min.z + 4f && room.counterFront < fb.max.z, $"The counter's front at z {Shared.F(room.counterFront, "0.00")} isn't inside the floor ({Shared.BoundsText(fb)}).");
        room.floor = Rect.MinMaxRect(fb.min.x, fb.min.z, fb.max.x, room.counterFront);
        run.Info($"The customer floor: x {Shared.F(room.floor.xMin, "0.00")}..{Shared.F(room.floor.xMax, "0.00")}, z {Shared.F(room.floor.yMin, "0.00")}..{Shared.F(room.floor.yMax, "0.00")} " +
                 $"(the counter's customers' edge), at y {Shared.F(room.floorY)}.");

        // The door: between the open leaves' hinges, at their line; without them, the spawn's x at the floor's front edge.
        Transform leaves = Shared.Find(run, DoorLeavesPath);
        GameObject spawn = run.Scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SpawnPoint");
        run.Require(spawn != null, "No 'SpawnPoint' at the scene's root (where customers come in; the layout check walks from it).");
        room.spawn = spawn.transform.position;
        if (leaves != null && leaves.childCount >= 2)
        {
            List<Transform> hinges = leaves.Cast<Transform>().ToList();
            room.door = new Vector3(hinges.Average(t => t.position.x), room.floorY, hinges.Average(t => t.position.z));
            run.Info($"The door: between the open leaves' hinges, its middle at {Shared.V(room.door)}.");
        }
        else
        {
            room.door = new Vector3(room.spawn.x, room.floorY, fb.min.z);
            run.Info($"The door ('{DoorLeavesPath}' not found): the spawn's line at the floor's front edge, {Shared.V(room.door)}.");
        }
        run.Info($"The spawn: {Shared.V(room.spawn)}.");

        // The people's places: what solids keep clear of (CafeFurnishing's distances), what the new stand points keep from.
        Transform queue = run.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "CounterQueue");
        run.Require(queue != null, "No CounterQueue in the scene.");
        foreach (Transform q in queue) { room.queue.Add(q); room.people.Add((q.position, KeepQueue, "the queue's " + q.name)); }
        TableSeat[] seats = Shared.InScene<TableSeat>(run, false).Where(s => s.isActiveAndEnabled).ToArray();
        foreach (TableSeat s in seats)
        {
            room.people.Add((s.StandPoint.position, KeepSeat, s.name + "'s stand point"));
            room.seatStands.Add((s.StandPoint.position, s.name));
            // (from 1.2 m over the sit point: a ray that starts inside the chair's own box would miss it)
            RaycastHit? under = Shared.Down(run, s.SeatPose.position + Vector3.up * 1.2f, 1.5f, c => c.transform == floor || c.transform.IsChildOf(floor));
            if (under.HasValue) room.chairs.Add((under.Value.collider.bounds, under.Value.collider.name));
            else room.chairs.Add((new Bounds(s.SeatPose.position, new Vector3(.5f, .9f, .5f)), s.name + " (no solid under its sit point: a 0.5 m box)"));
        }
        WaitingSpot[] waits = Shared.InScene<WaitingSpot>(run, true).Where(w => !(w is TableSeat)).ToArray();
        foreach (WaitingSpot w in waits)
            if (w.isActiveAndEnabled) { room.people.Add((w.StandPoint.position, KeepWaiting, w.name)); room.loiters.Add((w.StandPoint.position, w.name)); }
        StationInteractable[] stations = Shared.InScene<StationInteractable>(run, false);
        foreach (StationInteractable st in stations) if (st.StandPoint != null) room.people.Add((st.StandPoint.position, KeepStation, $"the station \"{st.name}\"'s stand point"));
        foreach (GameObject g in run.Scene.GetRootGameObjects().Where(root => root.name == "SpawnPoint" || root.name == "PatronSpawnPoint"))
            room.people.Add((g.transform.position, KeepSpawn, "the " + g.name));
        foreach (CapsuleCollider c in Shared.InScene<CapsuleCollider>(run, false))
            if (c.enabled && c.gameObject.activeInHierarchy && c.name.StartsWith("CK_", StringComparison.Ordinal) && c.transform.IsChildOf(run.Cafe))
                room.plants.Add((c.bounds.center, c.name));
        room.seatsBefore = seats.Length;
        run.Info($"People's places: {seats.Length} seats ({room.chairs.Count} chairs' solids found), {room.loiters.Count} waiting spots on, {room.queue.Count} queue places, " +
                 $"{stations.Length} stations; {room.plants.Count} kit plants on the floor.");

        // The guards now: the numbers the step compares with after.
        room.layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
        room.circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        Counted(room.layoutBefore, out room.pathsBefore, out room.targetsBefore);
        Counted(room.circulationBefore, out room.servedBefore, out room.placesBefore);
        run.Require(room.targetsBefore >= 0 && room.placesBefore >= 0, $"A guard gave no count (\"{room.layoutBefore}\"; \"{room.circulationBefore}\"): look at it before dressing the café.");
        room.allPathsBefore = room.pathsBefore == room.targetsBefore;
        room.allServedBefore = room.servedBefore == room.placesBefore;
        run.Info($"The guards now: \"{room.layoutBefore}\"; \"{room.circulationBefore}\".");
        if (!room.allPathsBefore || !room.allServedBefore) run.Info("NOTE: a guard already fails somewhere; the step only asks that nothing gets worse and that the lounge's own seats are served.");
        return room;
    }

    // The first "N/M" in a guard's result.
    static void Counted(string result, out int done, out int of)
    {
        Match m = Regex.Match(result ?? "", @"(\d+)/(\d+)");
        done = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        of = m.Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
    }

    // ---------------------------------------------------------------- the reading corner, measured

    static Template MeasureCorner(Shared.Run run, Room room)
    {
        var t = new Template();
        Transform corner = Shared.Find(run, CornerPath);
        run.Require(corner != null, $"'{CornerPath}' isn't under the café's root (Lived in 2's reading corner).");
        List<Transform> pieces = corner.Cast<Transform>().Where(c => c.gameObject.activeSelf).ToList();
        List<Transform> chairs = pieces.Where(c => c.name.StartsWith("Tub chair", StringComparison.Ordinal) && c.GetComponent<BoxCollider>() != null).ToList();
        Transform table = pieces.FirstOrDefault(c => c.name.StartsWith("Lounge table", StringComparison.Ordinal) && c.GetComponent<BoxCollider>() != null);
        Transform lamp = pieces.FirstOrDefault(c => c.name.StartsWith("Floor lamp", StringComparison.Ordinal) && c.GetComponent<BoxCollider>() != null);
        Transform rug = pieces.FirstOrDefault(c => c.name.StartsWith("LI_Rug_Corner", StringComparison.Ordinal));
        TableSeat[] seats = corner.GetComponentsInChildren<TableSeat>(false).Where(s => s.isActiveAndEnabled).ToArray();
        run.Require(chairs.Count == 2, $"The reading corner holds {chairs.Count} tub chair(s) with a solid (two expected: 'Tub chair' and its copy).");
        run.Require(table != null, "The reading corner's 'Lounge table' (with its solid) isn't there.");
        run.Require(lamp != null, "The reading corner's 'Floor lamp …' (with its solid) isn't there.");
        run.Require(rug != null, "The reading corner's 'LI_Rug_Corner (reading corner)' isn't there.");
        run.Require(seats.Length == 2, $"The reading corner holds {seats.Length} seat(s) (two expected).");
        foreach (Transform c in chairs)
            run.Require(c.GetComponentsInChildren<WaitingSpot>(true).Length == 0, $"{Shared.PathOf(c)} holds a seat or waiting spot: its copy would copy it too.");

        // The frame: the floor under the table's centre; along: from one chair to the other; across: to the right of that,
        // flipped so the stand points lie on +x (the open side, where people walk in).
        Vector3 origin = new Vector3(table.position.x, room.floorY, table.position.z);
        Vector3 along = Flat(chairs[1].position - chairs[0].position).normalized;
        run.Require(along.sqrMagnitude > .5f, "The corner's two chairs stand on one spot: no line to measure.");
        Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
        float standsAcross = seats.Average(s => Vector3.Dot(s.StandPoint.position - origin, across));
        if (standsAcross < 0f) { along = -along; across = -across; chairs.Reverse(); }
        Quaternion frame = Quaternion.LookRotation(along, Vector3.up), unframe = Quaternion.Inverse(frame);
        Vector3 Local(Vector3 world) => unframe * (world - origin);
        t.cornerCentre = origin;
        run.Info($"The corner's frame: the table's centre {Shared.V(origin)}; the chairs' line heads {Shared.F(Shared.Facing(frame), "0")}; the open side (the stand points) is to its right.");

        Part Add(Transform source, string label, string name, bool solid, string why, bool isRug = false, bool isChair = false, bool isTable = false, bool isLamp = false)
        {
            var part = new Part
            {
                label = label, name = name, source = source, solid = solid, why = why, isRug = isRug, isChair = isChair, isTable = isTable, isLamp = isLamp,
                at = Local(source.position), turn = unframe * source.rotation, local = LocalBox(source),
            };
            part.at.y = source.position.y - room.floorY;
            t.parts.Add(part);
            run.Info($"{label}: {Shared.PathOf(source)}, at ({Shared.F(part.at.x, "0.00")} across, {Shared.F(part.at.z, "0.00")} along, {Shared.F(part.at.y, "0.000")} up), " +
                     $"its box {Shared.V(part.local.size)} m; {why}.");
            return part;
        }
        Part rugPart = Add(rug, "the rug", "LI_Rug_Corner (lounge)", false, "a flatweave under the table and the chairs' fronts (the corner's)", isRug: true);
        Part chairA = Add(chairs[0], "the first tub chair", "Tub chair (lounge, A)", true, "a mustard tub chair facing its pair across the low table", isChair: true);
        Part chairB = Add(chairs[1], "the second tub chair", "Tub chair (lounge, B)", true, "its pair, facing it", isChair: true);
        Part tablePart = Add(table, "the low table", "Lounge table (lounge)", true, "the low table between the chairs, on the rug", isTable: true);
        Part lampPart = Add(lamp, "the floor lamp", "Floor lamp (lounge)", true, "the corner's floor lamp beside one chair, on the closed side, lit", isLamp: true);
        Light lampLight = lamp.GetComponentInChildren<Light>(true);
        run.Info(lampLight != null ? $"The lamp carries its light ({lampLight.name}: #{ColorUtility.ToHtmlStringRGB(lampLight.color)}, {Shared.F(lampLight.intensity, "0.0")}, range {Shared.F(lampLight.range, "0.0")}): the copy brings it, registered with CafeDaylight."
                                   : "The lamp carries no light of its own: the copy gets the corner's (#FFC98F, 0.8, range 2.5) at its shade.");

        // The plant: beside the other chair on the closed side, the mirror of the lamp along the line, from the kit.
        Part plant = KitPart(run, LoungePlant, "the plant", $"{LoungePlant} (lounge)", "a monstera beside the other chair, on the closed side (the corner has the window there)");
        plant.isPlant = true;
        plant.at = new Vector3(lampPart.at.x, 0f, -lampPart.at.z);
        plant.turn = Quaternion.Euler(0f, 35f, 0f);
        // ... nudged outward on the closed side until its pot and leaves clear the chair and the table (the pot must; the leaves may overhang the rug).
        float sign = lampPart.at.x < 0f ? -1f : 1f;
        for (int i = 0; i <= 8; i++)
        {
            Bounds pb = BlenderKitSteps.Transformed(plant.local, Matrix4x4.TRS(plant.at, plant.turn, Vector3.one));
            var pot = new Bounds(new Vector3(plant.at.x, plant.capsuleHeight * .5f, plant.at.z), new Vector3(plant.capsuleRadius * 2f, plant.capsuleHeight, plant.capsuleRadius * 2f));
            bool clear = t.parts.Where(x => x != plant && x.solid).All(x =>
            {
                Bounds xb = BlenderKitSteps.Transformed(x.local, Matrix4x4.TRS(x.at, x.turn, Vector3.one));
                return Gap(pot, xb) >= FreeMargin && Gap(pb, xb) >= .02f;
            });
            if (clear) break;
            plant.at.x += sign * .05f;
        }
        t.parts.Add(plant);
        run.Info($"{plant.label}: {LoungePlant} at ({Shared.F(plant.at.x, "0.00")} across, {Shared.F(plant.at.z, "0.00")} along), its pot's capsule r {Shared.F(plant.capsuleRadius, "0.00")} h {Shared.F(plant.capsuleHeight, "0.00")}; {plant.why}.");

        // The seats: sit point, cup spot and stand point in the frame; each belongs to the chair nearest its sit point.
        t.sample = seats[0];
        foreach (TableSeat s in seats)
        {
            var spec = new SeatSpec { sit = Local(s.SeatPose.position), cup = Local(s.CupSpot.position), stand = Local(s.StandPoint.position) };
            spec.sit.y = s.SeatPose.position.y - room.floorY;
            spec.cup.y = s.CupSpot.position.y - room.floorY;
            spec.stand.y = s.StandPoint.position.y - room.floorY;
            spec.chair = FlatDistance(s.SeatPose.position, chairs[0].position) <= FlatDistance(s.SeatPose.position, chairs[1].position) ? chairA : chairB;
            spec.name = $"Seat (lounge, tub chair {(spec.chair == chairA ? "A" : "B")})";
            t.seats.Add(spec);
            run.Info($"{s.name} -> {spec.name}: sit ({Shared.F(spec.sit.x, "0.00")}, {Shared.F(spec.sit.z, "0.00")}, {Shared.F(spec.sit.y, "0.000")} up), cup ({Shared.F(spec.cup.x, "0.00")}, {Shared.F(spec.cup.z, "0.00")}, " +
                     $"{Shared.F(spec.cup.y, "0.000")} up), stand ({Shared.F(spec.stand.x, "0.00")}, {Shared.F(spec.stand.z, "0.00")}); {s.Style}, Snap To Seat {(s.SnapToSeat ? "on" : "off")}, drain {Shared.F(s.DrainMultiplier, "0.0")}.");
        }
        run.Require(t.seats.Select(s => s.chair).Distinct().Count() == 2, "The corner's two seats sit on the same chair: the measuring can't tell them apart.");
        return t;
    }

    // A kit piece as a part: imported upright at scale 1, its box about its origin, its pot's capsule from the props manifest.
    static Part KitPart(Shared.Run run, string piece, string label, string name, string why)
    {
        string path = Shared.KitModels + "/" + piece + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        run.Require(asset != null, $"{path} isn't imported: run Fixit Fidget > Cafe redesign > Kit 1 first.");
        Bounds box = AssetBox(asset, out float turned, out Vector3 scale);
        var part = new Part { label = label, name = name, piece = piece, solid = true, why = why, local = box };
        run.Require(part.local.size.sqrMagnitude > 1e-6f, $"{piece}: no mesh in {path}.");
        run.Require(turned < .5f && (scale - Vector3.one).magnitude < .001f, $"{piece} is imported turned or scaled ({Shared.F(turned, "0.0")} degrees, scale {Shared.V(scale)}): Kit 1 imports it upright at scale 1.");
        run.Require(Mathf.Abs(part.local.min.y) < .01f, $"{piece}'s bottom is at {Shared.F(part.local.min.y)} about its origin: the kit's rule is the origin at the bottom.");
        PotCapsule(piece, part.local, out part.capsuleRadius, out part.capsuleHeight, out string from);
        run.Info($"{piece}: {Shared.V(part.local.size)} m (across, up, deep); its pot's capsule r {Shared.F(part.capsuleRadius, "0.00")}, h {Shared.F(part.capsuleHeight, "0.00")} ({from}).");
        return part;
    }

    // The pot's capsule: the props manifest's words ("a capsule round the pot (r 0.20, h 0.33)"); without them, a fifth of
    // the piece's width round, a quarter of its height high (a pot under a plant).
    static void PotCapsule(string piece, Bounds local, out float radius, out float height, out string from)
    {
        radius = Mathf.Max(.12f, Mathf.Min(local.size.x, local.size.z) * .25f);
        height = Mathf.Max(local.size.y * .25f, 2f * radius);
        from = "no manifest words: a quarter of its width round, a quarter of its height high";
        try
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PropsManifest));
            if (!File.Exists(file)) return;
            if (!(Kit.Json.Parse(File.ReadAllText(file)) is Dictionary<string, object> root) || !root.TryGetValue("pieces", out object listed) || !(listed is List<object> list)) return;
            foreach (object o in list)
            {
                if (!(o is Dictionary<string, object> d) || Kit.Str(d, "name") != piece) continue;
                string words = Kit.Str(d, "collider");
                if (words == null) return;
                Match r = Regex.Match(words.ToLowerInvariant(), @"\br\s*(\d*\.?\d+)"), h = Regex.Match(words.ToLowerInvariant(), @"\bh\s*(\d*\.?\d+)");
                if (!r.Success || !h.Success) return;
                radius = float.Parse(r.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                height = Mathf.Max(float.Parse(h.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture), 2f * radius);
                from = $"cafe_props.json: \"{words}\"";
                return;
            }
        }
        catch (Exception) { }
    }

    // ---------------------------------------------------------------- the floor map

    // extraPeople: places added after the lounge (its seats' stand points), for the touches.
    static FloorMap BuildMap(Shared.Run run, Room room, List<(Vector3 at, float keep, string what)> extraPeople)
    {
        var map = new FloorMap(room.floor, room.floorY);
        Transform floor = run.Scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Floor");
        var hits = new Collider[32];
        int solids = 0;
        for (int k = 0; k < map.nz; k++)
            for (int i = 0; i < map.nx; i++)
            {
                // A box the cell's size from 5 cm over the floor to 1.15 m: a chair, a table top, a pot, a wall; not the floor, not what hangs higher.
                var centre = new Vector3(map.X(i), room.floorY + .60f, map.Z(k));
                int n = Physics.OverlapBoxNonAlloc(centre, new Vector3(Cell * .5f, .55f, Cell * .5f), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = n == hits.Length;
                for (int h = 0; h < n && !blocked; h++)
                {
                    Collider c = hits[h];
                    if (c == null) continue;
                    if (floor != null && (c.transform == floor || c.transform.IsChildOf(floor))) continue;
                    if (run.Player != null && c.transform.IsChildOf(run.Player)) continue;
                    if (c.GetComponentInParent<PlayerMovement>() != null) continue;
                    blocked = true;
                }
                if (blocked) { map.solid[k * map.nx + i] = true; solids++; }
            }
        // The lane: from outside the door to short of the counter, 2.0 m wide; the walks from the door to each queue place, 1.2 m wide.
        map.MarkRect(map.lane, room.door.x - LaneHalfWidth, room.door.z - LaneBeforeDoor, room.door.x + LaneHalfWidth, room.counterFront - LaneToCounter, 0f);
        foreach (Transform q in room.queue) map.MarkSegment(map.lane, room.door, q.position, QueueWalkHalf);
        // The keep-clears round people's places, and 0.9 m round every chair's solid.
        foreach ((Vector3 at, float keep, string what) in room.people) map.MarkDisc(map.keep, at, keep);
        if (extraPeople != null) foreach ((Vector3 at, float keep, string what) in extraPeople) map.MarkDisc(map.keep, at, keep);
        foreach ((Bounds box, string what) in room.chairs) map.MarkRect(map.keep, box.min.x, box.min.z, box.max.x, box.max.z, ChairRing);
        map.Distances();
        int free = 0;
        for (int idx = 0; idx < map.clear.Length; idx++) if (map.clear[idx] > 0f) free++;
        run.Info($"The map: {map.nx} x {map.nz} cells; {solids} hold something solid; {free} are free of solids, the lane and the keep-clears ({Shared.F(free * Cell * Cell, "0.0")} m2 of {Shared.F(map.nx * map.nz * Cell * Cell, "0.0")}).");
        return map;
    }

    // The three largest free discs on the map (each masked out before the next): where the bare floor is.
    static string BareZones(FloorMap map)
    {
        var taken = new List<(Vector3 at, float r)>();
        var words = new List<string>();
        for (int n = 0; n < 3; n++)
        {
            float best = 0f; int bi = -1, bk = -1;
            for (int k = 0; k < map.nz; k++)
                for (int i = 0; i < map.nx; i++)
                {
                    float c = map.clear[k * map.nx + i];
                    if (c <= best) continue;
                    var at = new Vector3(map.X(i), 0f, map.Z(k));
                    if (taken.Any(z => FlatDistance(z.at, at) < z.r + c)) continue;
                    best = c; bi = i; bk = k;
                }
            if (bi < 0 || best < .5f) break;
            var centre = new Vector3(map.X(bi), 0f, map.Z(bk));
            taken.Add((centre, best));
            words.Add($"a free disc of r {Shared.F(best, "0.0")} m at ({Shared.F(centre.x, "0.0")}, {Shared.F(centre.z, "0.0")})");
        }
        return words.Count > 0 ? string.Join("; ", words) : "none bigger than 1 m across";
    }

    // ---------------------------------------------------------------- the candidates

    static void FindCandidates(Shared.Run run, LoungePlan p)
    {
        FloorMap map = p.map;
        Room room = p.room;
        Template t = p.template;
        var passing = new List<Placement>();
        int looked = 0;
        for (int k = 0; k < map.nz; k++)
            for (int i = 0; i < map.nx; i++)
            {
                int idx = k * map.nx + i;
                if (map.clear[idx] < CentreClear) continue;
                var centre = new Vector3(map.X(i), room.floorY, map.Z(k));
                foreach (float yaw in Yaws)
                {
                    var c = new Placement { centre = centre, yaw = yaw };
                    looked++;
                    if (GridProblem(map, room, t, c) != null) continue;
                    c.score = Roominess(map, t, c) + (Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f ? EastWestBonus : 0f)
                              + Mathf.Min(FlatDistance(centre, t.cornerCentre), 6f) * .01f;
                    passing.Add(c);
                }
            }
        run.Info($"{looked} places and turns looked at on the map; {passing.Count} hold the lounge by the map alone.");
        passing.Sort((a, b) => b.score.CompareTo(a.score));
        int exact = 0, kept = 0;
        var reasons = new List<string>();
        foreach (Placement c in passing)
        {
            if (exact >= ExactTests || kept >= LoungeTries) break;
            if (p.candidates.Any(d => Mathf.Abs(Mathf.DeltaAngle(d.yaw, c.yaw)) < 1f && FlatDistance(d.centre, c.centre) < TrySpacing)) continue;
            exact++;
            string why = ExactProblem(run, p, c);
            if (why != null) { if (reasons.Count < 6) reasons.Add($"{c.Where}: {why}"); continue; }
            p.candidates.Add(c);
            kept++;
        }
        if (reasons.Count > 0) run.Info("Refused by the exact test: " + string.Join("; ", reasons) + (exact - kept > reasons.Count ? "; …" : "") + ".");
    }

    // The map's rules, for every piece and stand point of the lounge at this place. Null when it holds; else why not.
    static string GridProblem(FloorMap map, Room room, Template t, Placement c)
    {
        foreach (Part part in t.parts)
        {
            Bounds b = c.Box(part);
            string why = map.Blocked(b, part.isRug ? RugMargin : FreeMargin, !part.isRug);
            if (why != null) return $"{part.label} would meet {why}";
        }
        var stands = new List<Vector3>();
        foreach (SeatSpec s in t.seats)
        {
            Vector3 at = c.World(s.stand);
            if (!map.Inside(at)) return $"{s.name}'s stand point would be off the customer floor";
            if (map.LaneAt(at)) return $"{s.name}'s stand point would be in the door's lane";
            if (map.ClearAt(at, false) < NavMargin) return $"{s.name}'s stand point would be within {Shared.F(NavMargin, "0.00")} m of something solid";
            foreach (Part part in t.Solids)
                if (FlatGap(c.Box(part), at) < NavMargin) return $"{s.name}'s stand point would be within {Shared.F(NavMargin, "0.00")} m of {part.label}";
            foreach (Vector3 other in stands) if (FlatDistance(other, at) < SeatClaim) return "the two stand points would be too close to claim both seats";
            foreach ((Vector3 o, string what) in room.seatStands) if (FlatDistance(o, at) < SeatClaim) return $"{s.name}'s stand point would be within {Shared.F(SeatClaim, "0.00")} m of {what}";
            foreach ((Vector3 o, string what) in room.loiters) if (FlatDistance(o, at) < SeatToWaiting) return $"{s.name}'s stand point would be within {Shared.F(SeatToWaiting, "0.00")} m of {what}";
            foreach (Transform q in room.queue) if (FlatDistance(q.position, at) < SeatToQueue) return $"{s.name}'s stand point would be within {Shared.F(SeatToQueue, "0.00")} m of the queue";
            stands.Add(at);
        }
        return null;
    }

    // The least free floor round the group: the map's distance at each solid piece's grown corners and edge middles, and
    // at the stand points (metres beyond the piece's own footprint).
    static float Roominess(FloorMap map, Template t, Placement c)
    {
        float least = float.MaxValue;
        foreach (Part part in t.Solids)
        {
            Bounds b = c.Box(part);
            foreach (Vector3 s in RectSamples(b, FreeMargin)) least = Mathf.Min(least, map.ClearAt(s, true));
        }
        foreach (SeatSpec s in t.seats) least = Mathf.Min(least, map.ClearAt(c.World(s.stand), false) - NavMargin);
        return least == float.MaxValue ? 0f : least;
    }

    static IEnumerable<Vector3> RectSamples(Bounds b, float grow)
    {
        float x0 = b.min.x - grow, x1 = b.max.x + grow, z0 = b.min.z - grow, z1 = b.max.z + grow, xm = b.center.x, zm = b.center.z;
        yield return new Vector3(x0, 0f, z0); yield return new Vector3(x1, 0f, z0); yield return new Vector3(x0, 0f, z1); yield return new Vector3(x1, 0f, z1);
        yield return new Vector3(xm, 0f, z0); yield return new Vector3(xm, 0f, z1); yield return new Vector3(x0, 0f, zm); yield return new Vector3(x1, 0f, zm);
    }

    // The exact test: every piece's box (grown) against the scene's colliders and drawn triangles, the chairs' 0.9 m, the
    // people's keep-clears. Null when it holds; else why not.
    static string ExactProblem(Shared.Run run, LoungePlan p, Placement c)
    {
        Transform floor = run.Scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Floor");
        foreach (Part part in p.template.parts)
        {
            Bounds b = c.Box(part);
            float grow = part.isRug ? RugMargin : FreeMargin;
            var grown = new Bounds(b.center, b.size + Vector3.one * 2f * grow);
            grown.SetMinMax(new Vector3(grown.min.x, b.min.y + .02f, grown.min.z), grown.max);
            foreach (Collider col in Physics.OverlapBox(grown.center, grown.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col == null || col.isTrigger) continue;
                if (floor != null && (col.transform == floor || col.transform.IsChildOf(floor))) continue;
                if (run.Player != null && col.transform.IsChildOf(run.Player)) continue;
                if (col.GetComponentInParent<PlayerMovement>() != null) continue;
                return $"{part.label} would stand within {Shared.F(grow, "0.00")} m of {Shared.PathOf(col.transform)}";
            }
            Bounds shrunk = ThroughBox(part);
            List<string> through = p.scene.Through(shrunk, Matrix4x4.TRS(c.World(part.at), c.Turn(part.turn), Vector3.one), tr => run.Player != null && tr.IsChildOf(run.Player));
            if (through.Count > 0) return $"{part.label} would pass through {string.Join(", ", through.Take(3))}";
            if (!part.solid) continue;
            foreach ((Bounds chair, string what) in p.room.chairs)
                if (Gap(b, chair) < ChairRing) return $"{part.label} would stand within {Shared.F(ChairRing, "0.0")} m of {what}";
            foreach ((Vector3 at, float keep, string what) in p.room.people)
                if (FlatGap(b, at) < keep) return $"{part.label} would stand within {Shared.F(keep, "0.00")} m of {what}";
        }
        return null;
    }

    // ================================================================== THE LOUNGE: the change

    static void ChangeLounge(Shared.Run run, LoungePlan p)
    {
        if (p == null) throw new InvalidOperationException("The survey didn't run.");

        // 1. The group, Not Walkable for the routes (as Seats 1's and the corner's): no cushion, top or pot becomes a step.
        Transform group = Shared.StepGroup(run, LoungeGroup);
        NavMeshModifier modifier = group.GetComponent<NavMeshModifier>();      // (not ??: a missing component is Unity's fake null in the Editor)
        if (modifier == null) modifier = Shared.AddComponent<NavMeshModifier>(run, group.gameObject);
        Undo.RecordObject(modifier, run.Title);
        modifier.overrideArea = true;
        modifier.area = 1;                 // Not Walkable
        modifier.ignoreFromBuild = false;
        modifier.applyToChildren = true;

        // 2. The places, one after another: placed, the routes re-baked, both guards; the first that serves everyone stays.
        run.Line("The places tried (the routes re-baked and both guards run after each):");
        foreach (Placement c in p.candidates)
        {
            List<GameObject> made = PlaceLounge(run, group, p, c);
            Physics.SyncTransforms();
            string why = Guard(run, p, p.template.seats, p.room.pathsBefore + p.template.seats.Count, p.room.servedBefore + p.template.seats.Count,
                               p.room.allPathsBefore, p.room.allServedBefore, out p.layoutAfter, out p.circulationAfter);
            if (why == null) { p.chosen = c; run.Info($"{c.Where}: KEPT. {p.layoutAfter} {p.circulationAfter}"); break; }
            p.tried.Add($"{c.Where}: {why}");
            run.Info($"{c.Where}: not kept: {why}");
            Unplace(run, made);
            foreach (Part part in p.template.parts) part.placed = null;
            foreach (SeatSpec s in p.template.seats) { s.made = null; s.seat = null; }
        }
        if (p.chosen == null) throw new InvalidOperationException("No place for the lounge kept every customer served: " + string.Join("; ", p.tried));
        Counted(p.layoutAfter, out p.pathsAfter, out p.targetsAfter);
        Counted(p.circulationAfter, out p.servedAfter, out p.placesAfter);
        // The new chairs are chairs too: the touches keep 0.9 m from them (the map for the touches reads room.chairs).
        foreach (Part part in p.template.parts.Where(x => x.isChair && x.placed != null))
        {
            BoxCollider box = part.placed.GetComponent<BoxCollider>();
            if (box != null) p.room.chairs.Add((box.bounds, part.name));
        }

        // 3. The lamp's light, one of CafeDaylight's warm lights (40 % by day, dimmed when closed, as the corner's).
        Part lamp = p.template.parts.First(x => x.isLamp);
        p.lampLight = lamp.placed.GetComponentInChildren<Light>(true);
        if (p.lampLight == null)
        {
            Bounds lb = Shared.RendererBounds(lamp.placed.transform, true);
            p.lampLight = Shared.MakeLight(run, lamp.placed.transform, "Lounge lamp light", new Vector3(lb.center.x, lb.max.y - .15f, lb.center.z), Quaternion.identity,
                                           LightType.Point, new Color(1f, 201f / 255f, 143f / 255f), .8f, 2.5f);
        }
        p.lampLight.name = "Lounge lamp light (the lounge's floor lamp)";
        Shared.AddToList(run, p.daylight, "warmLights", p.lampLight, "the lounge's floor lamp: dimmed by day and while closed, like the café's other lamps");

        // 4. The touches: a plant in the next bare bands, each kept only if both guards stay as they are.
        run.Line("The touches (a rubber plant in the next bare bands, near a wall or the front, kept only if both guards stay):");
        PlaceTouches(run, group, p);

        Physics.SyncTransforms();
        Shared.Note(run, $"Café dressing 3: the lounge at {p.chosen.Where} ({p.template.parts.Count} pieces, {p.template.seats.Count} seats: the guards' places {p.room.placesBefore} -> {p.placesAfter}); " +
                         $"{p.touches.Count} touch(es): {(p.touches.Count > 0 ? string.Join(", ", p.touches.Select(x => $"{x.name} at ({Shared.F(x.at.x, "0.00")}, {Shared.F(x.at.z, "0.00")})")) : "none found that kept the guards")}. " +
                         $"The biggest bare zones measured before: {p.zones}.");

        // 5. The checks.
        AfterLounge(run, p, group);
    }

    static List<GameObject> PlaceLounge(Shared.Run run, Transform group, LoungePlan p, Placement c)
    {
        var made = new List<GameObject>();
        foreach (Part part in p.template.parts)
        {
            Vector3 at = c.World(part.at);
            Quaternion turn = c.Turn(part.turn);
            GameObject go;
            if (part.source != null)
            {
                go = Object.Instantiate(part.source.gameObject, group);
                go.name = part.name;
                go.transform.SetPositionAndRotation(at, turn);
                go.SetActive(true);
                Shared.Made(run, go);
            }
            else
            {
                go = Shared.Place(run, part.piece, group, at, 0f, part.name);
                go.transform.SetPositionAndRotation(at, turn);
                if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                NoColliders(go);                                                                           // Kit 1 imports none; the pot's capsule follows
                if (part.capsuleRadius > 0f)
                {
                    CapsuleCollider capsule = Shared.AddComponent<CapsuleCollider>(run, go);
                    capsule.direction = 1;
                    capsule.radius = part.capsuleRadius;
                    capsule.height = Mathf.Max(part.capsuleHeight, 2f * part.capsuleRadius);
                    capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
                    capsule.isTrigger = false;
                }
            }
            part.placed = go;
            made.Add(go);
        }
        foreach (SeatSpec s in p.template.seats) made.Add(MakeSeat(run, group, p.template.sample, s, c));
        return made;
    }

    // A seat the way Seats 1 and the corner make one: a TableSeat on the floor under its sit point, its SeatPose, CupSpot and
    // StandPoint; its fields the corner's seat's (Chair, Snap To Seat on, drain 0.6, footprint 0).
    static GameObject MakeSeat(Shared.Run run, Transform group, TableSeat sample, SeatSpec s, Placement c)
    {
        Vector3 sit = c.World(s.sit), cup = c.World(s.cup), stand = c.World(s.stand);
        Quaternion facing = Quaternion.LookRotation(Flat(cup - sit), Vector3.up), standFacing = Quaternion.LookRotation(Flat(cup - stand), Vector3.up);
        var go = new GameObject(s.name);
        go.transform.SetParent(group, false);
        go.transform.SetPositionAndRotation(new Vector3(sit.x, c.centre.y, sit.z), facing);
        Transform pose = Anchor(go.transform, "SeatPose", sit, facing);
        Transform cupT = Anchor(go.transform, "CupSpot", cup, facing);
        Transform standT = Anchor(go.transform, "StandPoint", stand, standFacing);
        TableSeat seat = go.AddComponent<TableSeat>();
        var so = new SerializedObject(seat);
        so.FindProperty("kind").intValue = (int)WaitingSpot.SpotKind.Seat;
        so.FindProperty("standPoint").objectReferenceValue = standT;
        so.FindProperty("drainMultiplier").floatValue = sample.DrainMultiplier;
        so.FindProperty("cupSpot").objectReferenceValue = cupT;
        so.FindProperty("snapToSeat").boolValue = sample.SnapToSeat;
        so.FindProperty("seatPose").objectReferenceValue = pose;
        so.FindProperty("sitStyle").intValue = (int)sample.Style;
        so.FindProperty("faceTowards").objectReferenceValue = null;
        so.FindProperty("clearanceRadius").floatValue = sample.ClearanceRadius;
        so.ApplyModifiedPropertiesWithoutUndo();                 // a new object: its creation is the undo
        Shared.Made(run, go);
        s.made = go;
        s.seat = seat;
        return go;
    }

    static Transform Anchor(Transform parent, string name, Vector3 world, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(world, rotation);
        return go.transform;
    }

    // A placed kit piece's own colliders, if Kit 1 imported any, switched off (CafeRoomSteps.NoColliders: the manifests
    // name none; a plant's capsule comes after). The piece is the step's own, so its Undo takes them with it.
    static void NoColliders(GameObject piece)
    {
        foreach (Collider col in piece.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
            if (PrefabUtility.IsPartOfPrefabInstance(col)) PrefabUtility.RecordPrefabInstancePropertyModifications(col);
        }
    }

    // What a try made, taken out again (inside the step's undo group; the record forgets it).
    static void Unplace(Shared.Run run, List<GameObject> made)
    {
        for (int i = made.Count - 1; i >= 0; i--)
        {
            GameObject go = made[i];
            if (go == null) continue;
            run.Record.made.Remove(go);
            run.Record.addedComponents.RemoveAll(x => x == null || x.transform.IsChildOf(go.transform));
            Undo.DestroyObjectImmediate(go);
        }
        made.Clear();
        Physics.SyncTransforms();
    }

    // The routes re-baked; the new seats' stand points on them and walked to from the spawn; the layout check and the
    // occupied-circulation guard: at least the counts asked for, everything if everything was served before, and never a
    // new seat named as unserved. Null when all holds; else why not.
    static string Guard(Shared.Run run, LoungePlan p, IEnumerable<SeatSpec> seats, int minPaths, int minServed, bool allPaths, bool allServed, out string layout, out string circulation)
    {
        layout = ""; circulation = "";
        AcesCafeLayoutSetup.BakeRoutes();
        var path = new NavMeshPath();
        foreach (SeatSpec s in seats)
        {
            if (s.seat == null) continue;
            Vector3 stand = s.seat.StandPoint.position;
            bool on = NavMesh.SamplePosition(stand, out NavMeshHit hit, .22f, NavMesh.AllAreas);
            if (!on) return $"{s.name}'s stand point isn't on the routes (nothing walkable within 0.22 m)";
            bool reached = NavMesh.CalculatePath(p.room.spawn, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (!reached) return $"{s.name}'s stand point can't be walked to from the spawn";
        }
        // (the results are copied to locals: an out parameter can't be read inside a lambda)
        string layoutNow = AcesCafeLayoutSetup.ValidateLayout();
        layout = layoutNow;
        Counted(layoutNow, out int paths, out int targets);
        if (paths < minPaths || (allPaths && paths != targets) || seats.Any(s => layoutNow.Contains(s.name))) return "the layout check: " + layoutNow;
        string circulationNow = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        circulation = circulationNow;
        Counted(circulationNow, out int served, out int places);
        if (served < minServed || (allServed && served != places) || seats.Any(s => circulationNow.Contains(s.name))) return "the occupied-circulation guard: " + circulationNow;
        return null;
    }

    // ---------------------------------------------------------------- the touches

    static void PlaceTouches(Shared.Run run, Transform group, LoungePlan p)
    {
        string path = Shared.KitModels + "/" + TouchPlant + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { run.Info($"{path} isn't imported: no touches (the lounge stands)."); return; }
        Bounds local = AssetBox(asset, out float turned, out Vector3 scale);
        if (turned >= .5f || (scale - Vector3.one).magnitude >= .001f || local.size.sqrMagnitude < 1e-6f) { run.Info($"{TouchPlant} isn't imported upright at scale 1: no touches."); return; }
        PotCapsule(TouchPlant, local, out float potR, out float potH, out string from);
        float reach = Mathf.Max(local.extents.x, local.extents.z);
        run.Info($"{TouchPlant}: {Shared.V(local.size)} m, its leaves reach {Shared.F(reach, "0.00")} m, its pot's capsule r {Shared.F(potR, "0.00")} h {Shared.F(potH, "0.00")} ({from}).");

        // The floor again, with the lounge in it (its solids are colliders now, its stand points people's places).
        var extra = new List<(Vector3 at, float keep, string what)>();
        foreach (SeatSpec s in p.template.seats)
            if (s.seat != null) extra.Add((s.seat.StandPoint.position, KeepSeat, s.name + "'s stand point"));
        FloorMap map = BuildMap(run, p.room, extra);
        var plants = p.room.plants.Select(x => x.at).ToList();
        Part loungePlant = p.template.parts.FirstOrDefault(x => x.isPlant);
        if (loungePlant != null && loungePlant.placed != null) plants.Add(loungePlant.placed.transform.position);
        var spots = new List<(Vector3 at, float clear, float wall)>();
        for (int k = 0; k < map.nz; k++)
            for (int i = 0; i < map.nx; i++)
            {
                int idx = k * map.nx + i;
                float c = map.clear[idx];
                if (c < TouchClearMin || c > TouchClearMax) continue;
                var at = new Vector3(map.X(i), p.room.floorY, map.Z(k));
                float wall = Mathf.Min(at.x - map.x0, Mathf.Min(map.x1 - at.x, at.z - map.z0));
                if (wall > TouchNearWall) continue;
                if (at.z > p.room.counterFront - TouchFromCounter) continue;
                if (FlatDistance(at, p.chosen.centre) < TouchFromLounge) continue;
                if (plants.Any(pl => FlatDistance(at, pl) < TouchFromPlants)) continue;
                spots.Add((at, c, wall));
            }
        spots.Sort((a, b) => b.clear.CompareTo(a.clear));
        run.Info($"{spots.Count} snug spots near a wall or the front ({Shared.F(TouchClearMin, "0.00")}..{Shared.F(TouchClearMax, "0.00")} m of free floor round them), spread from the plants and the lounge.");
        Transform touches = null;
        var failed = new List<Vector3>();
        int tries = 0;
        foreach ((Vector3 at, float clear, float wall) in spots)
        {
            if (p.touches.Count >= Touches || tries >= TouchTries) break;
            if (p.touches.Any(x => FlatDistance(x.at, at) < TouchFromPlants) || failed.Any(f => FlatDistance(f, at) < TouchFromFailed)) continue;
            float yaw = p.touches.Count == 0 ? 20f : 160f;        // not twins
            var pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            List<string> through = p.scene.Through(ThroughBox(local, false), pose, tr => run.Player != null && tr.IsChildOf(run.Player));
            if (through.Count > 0) { run.Info($"({Shared.F(at.x, "0.00")}, {Shared.F(at.z, "0.00")}): skipped, the plant would pass through {string.Join(", ", through.Take(3))}."); failed.Add(at); continue; }
            tries++;
            if (touches == null) touches = Shared.Group(run, group, TouchesGroup);
            string name = $"{TouchPlant} (touch {p.touches.Count + 1})";
            GameObject go = Shared.Place(run, TouchPlant, touches, at, yaw, name);
            NoColliders(go);
            CapsuleCollider capsule = Shared.AddComponent<CapsuleCollider>(run, go);
            capsule.direction = 1;
            capsule.radius = potR;
            capsule.height = Mathf.Max(potH, 2f * potR);
            capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
            capsule.isTrigger = false;
            Physics.SyncTransforms();
            string why = Guard(run, p, Enumerable.Empty<SeatSpec>(), p.pathsAfter, p.servedAfter, p.pathsAfter == p.targetsAfter, p.servedAfter == p.placesAfter, out string layout, out string circulation);
            if (why == null)
            {
                p.touches.Add(new Touch { go = go, at = at, clear = clear, wall = wall, yaw = yaw, name = name });
                p.layoutAfter = layout; p.circulationAfter = circulation;
                run.Info($"{name} at ({Shared.F(at.x, "0.00")}, {Shared.F(at.z, "0.00")}) turned {Shared.F(yaw, "0")}: KEPT ({Shared.F(clear, "0.00")} m of free floor round it, {Shared.F(wall, "0.00")} m from the nearest wall).");
            }
            else
            {
                run.Info($"{name} at ({Shared.F(at.x, "0.00")}, {Shared.F(at.z, "0.00")}): not kept: {why}");
                failed.Add(at);
                Unplace(run, new List<GameObject> { go });
            }
        }
        if (p.touches.Count == 0) run.Info("No touch kept: the lounge stands alone (the report says what was tried).");
        if (p.touches.Count > 0 && p.touches.Count < Touches) AcesCafeLayoutSetup.BakeRoutes();
    }

    // ================================================================== THE LOUNGE: the checks after (any FAIL puts everything back)

    static void AfterLounge(Shared.Run run, LoungePlan p, Transform group)
    {
        run.Line("Checked after:");
        Placement c = p.chosen;
        var scene = new Kit.Scenery(run, p.movers);
        bool Mine(Transform tr) => tr.IsChildOf(group) || (run.Player != null && tr.IsChildOf(run.Player));
        foreach (Part part in p.template.parts)
        {
            GameObject go = part.placed;
            run.Check(go != null && go.activeInHierarchy && go.transform.IsChildOf(group), $"{part.label}: placed and on, under {LoungeGroup}");
            if (go == null) continue;
            Vector3 at = c.World(part.at);
            run.Check(Vector3.Distance(go.transform.position, at) <= .001f && Quaternion.Angle(go.transform.rotation, c.Turn(part.turn)) <= .1f,
                      $"{part.label}: at {Shared.V(go.transform.position)}, turned {Shared.F(Shared.Facing(go.transform.rotation), "0")} ({part.why})");
            Bounds drawn = Shared.RendererBounds(go.transform);
            run.Check(drawn.min.y >= p.room.floorY - .005f && drawn.min.y <= p.room.floorY + .02f,
                      $"{part.label}: stands on the floor{(part.isTable ? " (on the rug)" : "")}: its lowest drawn point at {Shared.F(drawn.min.y)} (the floor {Shared.F(p.room.floorY)})");
            List<string> through = scene.Through(ThroughBox(part), Matrix4x4.TRS(go.transform.position, go.transform.rotation, Vector3.one), Mine);
            run.Check(through.Count == 0, $"{part.label}: nothing else passes through it{(through.Count > 0 ? ": " + string.Join("; ", through.Take(6)) : "")}");
            Collider[] solids = go.GetComponentsInChildren<Collider>(true).Where(x => x.enabled).ToArray();
            if (part.isRug) run.Check(solids.Length == 0, $"{part.label}: no collider (a rug is flat; nothing walks round it) ({solids.Length})");
            else if (part.isPlant) run.Check(solids.Length == 1 && solids[0] is CapsuleCollider, $"{part.label}: one capsule round its pot ({solids.Length} collider(s))");
            else run.Check(solids.Length >= 1 && solids.Any(x => x is BoxCollider), $"{part.label}: its box, as the corner's ({solids.Length} collider(s))");
            Rect r = OnScreen(p.view, PlacedVertices(go));
            float small = Mathf.Min(r.width, r.height);
            run.Check(small >= MinPixels, $"{part.label}: {Shared.F(r.width, "0")} x {Shared.F(r.height, "0")} px from above ({(small >= MinPixels ? "reads" : "UNDER " + MinPixels)})");
        }

        // The seats: wired like the corner's, their points where planned, the sit point over the chair's cushion, the cup on the table.
        Part table = p.template.parts.First(x => x.isTable);
        foreach (SeatSpec s in p.template.seats)
        {
            TableSeat t = s.seat;
            TableSeat sample = p.template.sample;
            bool wired = t != null && t.Kind == WaitingSpot.SpotKind.Seat && Mathf.Approximately(t.DrainMultiplier, sample.DrainMultiplier) && t.SnapToSeat == sample.SnapToSeat
                         && t.Style == sample.Style && Mathf.Approximately(t.ClearanceRadius, sample.ClearanceRadius)
                         && Near(t.StandPoint.position, c.World(s.stand), .001f) && Near(t.SeatPose.position, c.World(s.sit), .001f) && Near(t.CupSpot.position, c.World(s.cup), .001f);
            run.Check(wired, $"{s.name}: a Seat like the corner's ({sample.Style}, Snap To Seat {(sample.SnapToSeat ? "on" : "off")}, drain {Shared.F(sample.DrainMultiplier, "0.0")}), its three points where planned");
            if (t == null) continue;
            float? cushion = s.chair.placed != null ? MeshTopUnder(s.chair.placed, t.SeatPose.position + Vector3.up * .3f, .6f) : null;
            run.Check(cushion.HasValue && t.SeatPose.position.y - cushion.Value >= -.005f && t.SeatPose.position.y - cushion.Value <= .03f,
                      $"{s.name}: the sit point {Shared.F(t.SeatPose.position.y)} over its chair's cushion at {(cushion.HasValue ? Shared.F(cushion.Value) : "nothing")}");
            RaycastHit? under = Shared.Down(run, t.CupSpot.position + Vector3.up * .3f, .6f);
            bool onTable = under.HasValue && table.placed != null && under.Value.collider.transform.IsChildOf(table.placed.transform) && Mathf.Abs(under.Value.point.y - t.CupSpot.position.y) <= .01f;
            run.Check(onTable, $"{s.name}: the cup spot on the low table ({(under.HasValue ? under.Value.collider.name + " at " + Shared.F(under.Value.point.y) : "nothing under it")}; the cup at {Shared.F(t.CupSpot.position.y)})");
            run.Info($"{s.name}: NpcBeats counts {(NpcBeats.TableInFront(t) ? "a table in front (the table clips)" : "no table in front (a low table: the sofa clips, as the corner's)")}.");
        }
        foreach (Touch x in p.touches)
        {
            run.Check(x.go != null && x.go.activeInHierarchy, $"{x.name}: placed and on");
            if (x.go == null) continue;
            Bounds drawn = Shared.RendererBounds(x.go.transform);
            run.Check(drawn.min.y >= p.room.floorY - .005f && drawn.min.y <= p.room.floorY + .02f, $"{x.name}: stands on the floor (its lowest drawn point at {Shared.F(drawn.min.y)})");
            run.Check(x.go.GetComponentsInChildren<Collider>(true).Count(col => col.enabled) == 1, $"{x.name}: one capsule round its pot");
            Rect r = OnScreen(p.view, PlacedVertices(x.go));
            run.Check(Mathf.Min(r.width, r.height) >= MinPixels, $"{x.name}: {Shared.F(r.width, "0")} x {Shared.F(r.height, "0")} px from above");
        }
        run.Check(p.lampLight != null && p.daylight.warmLights != null && p.daylight.warmLights.Contains(p.lampLight), "the lounge lamp's light is one of CafeDaylight's warm lights");
        var moved = p.anchors.Where(a => a.Key == null || Vector3.Distance(a.Key.position, a.Value) > .001f).Select(a => a.Key != null ? Shared.PathOf(a.Key) : "(gone)").ToList();
        run.Check(moved.Count == 0, moved.Count == 0 ? $"No gameplay anchor moved ({p.anchors.Count}, 1 mm)." : "Moved: " + string.Join(", ", moved));
        TableSeat[] live = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(t => t.gameObject.scene == run.Scene).ToArray();
        run.Check(live.Length == p.room.seatsBefore + p.template.seats.Count, $"Active seats in the scene: {live.Length} ({p.room.seatsBefore} + {p.template.seats.Count} expected).");
        run.Info($"The guards with the lounge{(p.touches.Count > 0 ? " and the touches" : "")}: \"{p.layoutAfter}\"; \"{p.circulationAfter}\" (the shared step runs them once more after the final bake).");
        run.Info("Next, on the Mac: look at the photos, save the scene, then Fixit Fidget > Night > Night walk 2 (the night's solid list), and save again.");
    }

    // ================================================================== THE LOUNGE: the photos (before and after, from the same places)

    static void LoungePhotos(Shared.Run run, LoungePlan p)
    {
        Shared.GameCameraPhoto(run, "01-the-game-camera");
        CounterCameraPhoto(run, "02-the-counter-camera");
        Shared.Photo(run, "03-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.1f, 12f), 72f);
        Placement c = p != null ? (p.chosen ?? (p.candidates.Count > 0 ? p.candidates[0] : null)) : null;
        if (c != null)
        {
            Vector3 open = c.Frame * Vector3.right;
            Vector3 eye = c.centre + open * 2.8f + c.Frame * Vector3.forward * -1.2f + Vector3.up * 1.55f;
            Shared.Photo(run, "04-the-lounge-close", eye, c.centre + Vector3.up * .45f, 55f);
            Overhead o = p.view ?? HomeView(run);
            Shared.Photo(run, "05-the-lounge-from-above-x2", o.position, c.centre + Vector3.up * .4f, o.fov * .5f, true);
        }
        Shared.Photo(run, "06-the-waiting-nook", new Vector3(3.2f, 1.6f, 5.4f), new Vector3(6.9f, 1f, 2.3f), 62f);
        Shared.Photo(run, "07-the-front-left", new Vector3(-1.9f, 1.55f, 1f), new Vector3(-6.2f, .55f, 5.2f), 62f);
        if (p != null)
            for (int i = 0; i < p.touches.Count; i++)
            {
                Touch x = p.touches[i];
                if (x.go == null) continue;
                Vector3 toRoom = Flat(new Vector3(0f, 0f, 7f) - x.at).normalized;
                Shared.Photo(run, $"08-touch-{i + 1}", x.at + toRoom * 2.6f + Vector3.up * 1.5f, x.at + Vector3.up * .7f, 55f);
            }
    }

    static void CounterCameraPhoto(Shared.Run run, string name)
    {
        CinemachineCamera counter = Shared.InScene<CinemachineCamera>(run).FirstOrDefault(cm => cm.name == "CmCounterCam");
        if (counter == null) { run.Info("No CmCounterCam: the counter camera's photo wasn't taken."); return; }
        Shared.Photo(run, name, counter.transform.position, counter.transform.position + counter.transform.forward * 5f, counter.Lens.FieldOfView > 1f ? counter.Lens.FieldOfView : 50f);
    }

    // ================================================================== THE PENDANTS

    sealed class Pendant
    {
        public string label;
        public Transform root, cord;
        public Light light;
        public float topWas, rimWas, scaleWas, anchor;    // world metres; anchor: where its cord meets the ceiling (its own top without one)
        public Color colour; public float intensity, range;
        public float topTo, rimTo;
        public bool capped, lowered, needsCord;
        public GameObject cordMade;
    }

    sealed class PendantPlan
    {
        public readonly List<Pendant> pendants = new List<Pendant>();
        public readonly List<(Bounds box, string what)> beams = new List<(Bounds, string)>();
        public Mesh mesh;
        public Vector3[] vertices;
        public float minY, maxY, deckY, cap;
        public CafeViewMode view;
    }

    static PendantPlan SurveyPendants(Shared.Run run)
    {
        var p = new PendantPlan();
        Transform group = Shared.Find(run, CounterPendantsPath);
        run.Require(group != null, $"'{CounterPendantsPath}' isn't under the café's root: the counter's pendants (Cafe furnishing > 4 - Light the counter) aren't up, and the table pendants are copies of theirs.");
        CafeCounterPendants record = group.GetComponent<CafeCounterPendants>();
        run.Require(record != null && record.shades != null && record.shades.Length > 0 && record.shades[0] != null, "The counter pendants' record (CafeCounterPendants) has no shades.");
        MeshFilter pf = record.shades[0].GetComponent<MeshFilter>();
        run.Require(pf != null && pf.sharedMesh != null, "Counter pendant 1 has no mesh.");
        p.mesh = pf.sharedMesh;
        p.minY = p.mesh.bounds.min.y;
        p.maxY = p.mesh.bounds.max.y;
        run.Require(p.minY < -.05f && p.maxY < .05f, $"The pendant's mesh hangs from its root (session 6 cut it with the pivot at its top): expected its rim below the root and its top at it, found y {Shared.F(p.minY)}..{Shared.F(p.maxY)}.");
        try { p.vertices = p.mesh.vertices; }
        catch (Exception) { p.vertices = null; }
        if (p.vertices == null || p.vertices.Length == 0)
        {
            Bounds b = p.mesh.bounds;
            p.vertices = Enumerable.Range(0, 8).Select(i => new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)).ToArray();
            run.Info("The pendant's mesh can't be read: its box's corners stand in for its vertices against the beams.");
        }
        p.view = Shared.InScene<CafeViewMode>(run).FirstOrDefault();

        // The ceiling deck and what hangs under it (Room 1's beams and skylight wells), each grown by the clearance.
        Transform deck = Shared.Find(run, DeckPath);
        foreach (Transform t in run.Cafe.GetComponentsInChildren<Transform>(false))
            if (t.gameObject.activeInHierarchy && (t.name.StartsWith("CK_Beam", StringComparison.Ordinal) || t.name.StartsWith("CK_SkylightWell", StringComparison.Ordinal)))
            {
                Bounds b = Shared.RendererBounds(t);
                if (b.size.sqrMagnitude < 1e-6f) continue;
                b.Expand(BeamClear * 2f);
                p.beams.Add((b, t.name));
            }

        // Every pendant: the counter's and every copy of its mesh under the café's root (Room 1's seven over the tables).
        var counterRoots = new HashSet<Transform>(record.shades.Where(s => s != null).Select(s => s.transform));
        foreach (MeshFilter f in Shared.InScene<MeshFilter>(run, false))
        {
            if (f.sharedMesh != p.mesh || !f.gameObject.activeInHierarchy || !f.transform.IsChildOf(run.Cafe)) continue;
            Transform root = f.transform;
            run.Require(Quaternion.Angle(root.rotation, Quaternion.identity) < .5f, $"{Shared.PathOf(root)} is turned: Room 1 and session 6 hang the pendant unturned; look before running this.");
            var x = new Pendant { label = root.name, root = root, light = root.GetComponentInChildren<Light>(true), cord = root.Find(CordName) };
            x.scaleWas = root.lossyScale.y;
            x.topWas = root.position.y;
            x.rimWas = x.topWas + x.scaleWas * p.minY;
            x.anchor = x.topWas;
            if (x.cord != null)
            {
                MeshFilter cf = x.cord.GetComponent<MeshFilter>();
                if (cf != null && cf.sharedMesh != null) x.anchor = BlenderKitSteps.Transformed(cf.sharedMesh.bounds, cf.transform.localToWorldMatrix).max.y;
            }
            if (x.light != null) { x.colour = x.light.color; x.intensity = x.light.intensity; x.range = x.light.range; }
            p.pendants.Add(x);
        }
        run.Require(p.pendants.Count > 0, "No pendant found (nothing under the café's root draws the counter pendant's mesh).");
        p.deckY = deck != null ? Shared.RendererBounds(deck).min.y : p.pendants.Max(x => x.anchor);
        p.cap = p.deckY - CeilingSpare;
        run.Line($"The pendants: {p.pendants.Count} ({counterRoots.Count} over the counter, {p.pendants.Count - counterRoots.Count} over the tables); the mesh '{p.mesh.name}' hangs " +
                 $"{Shared.F(-p.minY, "0.00")} m below its root; the ceiling deck at {Shared.F(p.deckY, "0.00")}{(deck == null ? " (no deck found: the highest cord)" : "")}, so a top may rise to {Shared.F(p.cap, "0.00")}; " +
                 $"{p.beams.Count} beams and wells to clear by {Shared.F(BeamClear, "0.00")}.");
        run.Line($"Each, scaled to {Shared.F(PendantScale, "0.0")} about its top and raised so its shade's bottom is at least {Shared.F(ShadeBottomMin, "0.00")} m up (never lowered; down a centimetre at a time off a beam):");
        foreach (Pendant x in p.pendants)
        {
            float s = PendantScale;
            float need = ShadeBottomMin - s * p.minY;
            float top = Mathf.Max(x.topWas, need);
            x.capped = false;
            if (top > p.cap) { top = Mathf.Max(x.topWas, p.cap); x.capped = top < need; }
            x.lowered = false;
            while (top > x.topWas + 1e-4f && HitsBeam(p, x, top, s)) { top -= .01f; x.lowered = true; }
            if (top < x.topWas) top = x.topWas;
            x.topTo = top;
            x.rimTo = top + s * p.minY;
            x.needsCord = x.cord == null && top > x.topWas + .005f;
            run.Info($"{x.label} at ({Shared.F(x.root.position.x, "0.00")}, {Shared.F(x.root.position.z, "0.00")}): top {Shared.F(x.topWas, "0.00")} -> {Shared.F(x.topTo, "0.00")}, " +
                     $"shade's bottom {Shared.F(x.rimWas, "0.00")} -> {Shared.F(x.rimTo, "0.00")}, scale {Shared.F(x.scaleWas, "0.00")} -> {Shared.F(s, "0.00")}" +
                     (x.cord != null ? $", its cord re-cut to the ceiling at {Shared.F(x.anchor, "0.00")}" : x.needsCord ? $", a cord added up to {Shared.F(x.anchor, "0.00")}" : "") +
                     (x.capped ? " (CAPPED by the ceiling: as high as it allows with 10 cm to spare)" : "") + (x.lowered ? " (brought down off a beam)" : "") +
                     (x.light != null ? $"; its light #{ColorUtility.ToHtmlStringRGB(x.colour)}, {Shared.F(x.intensity, "0.00")}, range {Shared.F(x.range, "0.0")} rises with it, as it is" : "; no light in it") + ".");
            run.Check(x.rimTo >= x.rimWas - 1e-3f, $"{x.label}: its shade's bottom never comes down ({Shared.F(x.rimWas, "0.00")} -> {Shared.F(x.rimTo, "0.00")})");
        }
        int under = p.pendants.Count(x => x.rimTo < ShadeBottomMin - 1e-3f);
        if (under > 0) run.Info($"FLAG: {under} pendant(s) can't reach {Shared.F(ShadeBottomMin, "0.00")} m under this ceiling (capped or off a beam): the report says which; his call whether the deck rises.");
        return p;
    }

    // Any vertex of the lamp, scaled `s` about its top at `top`, inside a beam's or a well's grown box.
    static bool HitsBeam(PendantPlan p, Pendant x, float top, float s)
    {
        foreach ((Bounds box, string what) in p.beams)
        {
            Bounds lamp = BlenderKitSteps.Transformed(p.mesh.bounds, Matrix4x4.TRS(new Vector3(x.root.position.x, top, x.root.position.z), Quaternion.identity, Vector3.one * s));
            if (!box.Intersects(lamp)) continue;
            foreach (Vector3 v in p.vertices)
                if (box.Contains(new Vector3(x.root.position.x + s * v.x, top + s * v.y, x.root.position.z + s * v.z))) return true;
        }
        return false;
    }

    static void ChangePendants(Shared.Run run, PendantPlan p)
    {
        if (p == null) throw new InvalidOperationException("The survey didn't run.");
        foreach (Pendant x in p.pendants)
        {
            Vector3 at = new Vector3(x.root.position.x, x.topTo, x.root.position.z);
            Shared.Move(run, x.root, at, x.root.rotation,
                        $"{x.label}: scaled to {Shared.F(PendantScale, "0.0")} and its top raised {Shared.F(x.topWas, "0.00")} -> {Shared.F(x.topTo, "0.00")} so its shade's bottom sits at {Shared.F(x.rimTo, "0.00")} " +
                        "(her: \"the ceiling has to go higher… or you need to get rid of these lamps. No, I can scale them smaller.\")");
            Undo.RecordObject(x.root, run.Title);
            Vector3 parent = x.root.parent != null ? x.root.parent.lossyScale : Vector3.one;
            x.root.localScale = new Vector3(PendantScale / Mathf.Max(parent.x, 1e-4f), PendantScale / Mathf.Max(parent.y, 1e-4f), PendantScale / Mathf.Max(parent.z, 1e-4f));
            if (PrefabUtility.IsPartOfPrefabInstance(x.root)) PrefabUtility.RecordPrefabInstancePropertyModifications(x.root);
            float length = Mathf.Max(x.anchor - x.topTo, .002f);
            if (x.cord != null)
            {
                Shared.Move(run, x.cord, new Vector3(at.x, x.topTo + length * .5f, at.z), Quaternion.identity, $"{x.label}'s cord, re-cut from its new top to the ceiling at {Shared.F(x.anchor, "0.00")}");
                Undo.RecordObject(x.cord, run.Title);
                SetCord(x.cord, length);
            }
            else if (x.needsCord)
            {
                var cord = new GameObject("Pendant cord (Cafe dressing 3)", typeof(MeshFilter), typeof(MeshRenderer));
                cord.transform.SetParent(x.root, false);
                cord.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                Renderer own = x.root.GetComponent<Renderer>();
                Material[] mats = own != null ? own.sharedMaterials : new Material[0];
                MeshRenderer cr = cord.GetComponent<MeshRenderer>();
                cr.sharedMaterial = mats.FirstOrDefault(m => m != null && m.name.Contains("Ink")) ?? mats.FirstOrDefault(m => m != null);
                cr.shadowCastingMode = ShadowCastingMode.Off;
                SetCord(cord.transform, length);
                Shared.Made(run, cord);
                x.cordMade = cord;
                if (p.view != null) Shared.AddToList(run, p.view, "overheadFixtures", cr, "a pendant's cord: hidden from above with its pendant");
            }
        }
        Physics.SyncTransforms();
        Shared.Note(run, $"Café dressing 3: {p.pendants.Count} pendants scaled to {Shared.F(PendantScale, "0.0")} and raised; shades' bottoms now {Shared.F(p.pendants.Min(x => x.rimTo), "0.00")}..{Shared.F(p.pendants.Max(x => x.rimTo), "0.00")} m " +
                         $"(were {Shared.F(p.pendants.Min(x => x.rimWas), "0.00")}..{Shared.F(p.pendants.Max(x => x.rimWas), "0.00")}); the lights untouched: they rose with their shades.");

        run.Line("Checked after:");
        foreach (Pendant x in p.pendants)
        {
            run.Check(Mathf.Abs(x.root.lossyScale.y - PendantScale) < 1e-3f && Mathf.Abs(x.root.lossyScale.x - PendantScale) < 1e-3f, $"{x.label}: scale {Shared.F(x.root.lossyScale.y, "0.000")}");
            run.Check(Mathf.Abs(x.root.position.y - x.topTo) < 1e-3f, $"{x.label}: its top at {Shared.F(x.root.position.y, "0.000")} (planned {Shared.F(x.topTo, "0.000")})");
            Renderer r = x.root.GetComponent<Renderer>();
            float rim = r != null ? r.bounds.min.y : x.root.position.y + x.root.lossyScale.y * p.minY;
            run.Check(Mathf.Abs(rim - x.rimTo) < .01f, $"{x.label}: its shade's bottom at {Shared.F(rim, "0.00")} m" + (x.rimTo >= ShadeBottomMin - 1e-3f ? "" : x.capped ? " (capped by the ceiling)" : " (off a beam)"));
            // A pendant that wasn't raised kept its top where it hung, on its beam: its canopy touched the beam before the change
            // and still does, by design (20 Oct, the first run on his Mac: the four counter pendants, top 3.17 = the deck). So the
            // test is "no new contact": a raised pendant must clear every beam; one left at its mount may touch only if it did before.
            bool hitsNow = HitsBeam(p, x, x.root.position.y, x.root.lossyScale.y);
            bool keptItsMount = x.topTo <= x.topWas + 1e-3f && HitsBeam(p, x, x.topWas, x.scaleWas);
            run.Check(!hitsNow || keptItsMount, hitsNow
                ? $"{x.label}: hangs from its beam as it did (its top unmoved at {Shared.F(x.topWas, "0.00")} m; only scaled about it)"
                : $"{x.label}: clears every beam and well by {Shared.F(BeamClear, "0.00")} m");
            if (x.light != null)
            {
                run.Check(x.light.color == x.colour && Mathf.Abs(x.light.intensity - x.intensity) < 1e-4f && Mathf.Abs(x.light.range - x.range) < 1e-4f,
                          $"{x.label}'s light as it was: #{ColorUtility.ToHtmlStringRGB(x.light.color)}, {Shared.F(x.light.intensity, "0.00")}, range {Shared.F(x.light.range, "0.0")}");
                run.Check(x.light.transform.position.y >= x.rimTo - .02f && x.light.transform.position.y <= x.topTo + .01f, $"{x.label}'s light rose with it: at {Shared.F(x.light.transform.position.y, "0.00")} m, inside the shade");
            }
            Transform cord = x.cord != null ? x.cord : x.cordMade != null ? x.cordMade.transform : null;
            if (cord != null)
            {
                Renderer cr = cord.GetComponent<Renderer>();
                Bounds cb = cr != null ? cr.bounds : new Bounds(cord.position, Vector3.zero);
                run.Check(Mathf.Abs(cb.max.y - x.anchor) < .015f && Mathf.Abs(cb.min.y - x.topTo) < .015f && cb.size.x < .02f,
                          $"{x.label}'s cord runs from its top ({Shared.F(cb.min.y, "0.00")}) to the ceiling ({Shared.F(cb.max.y, "0.00")}), {Shared.F(cb.size.x * 1000f, "0")} mm thick");
            }
            if (p.view != null && r != null) run.Check(p.view.overheadFixtures != null && p.view.overheadFixtures.Contains(r), $"{x.label} is still hidden from above (an overhead fixture)");
        }
    }

    // The cord: Unity's cylinder (2 m tall, 1 m wide at scale 1) under the scaled root, from the root's top up `length`, 8 mm thick.
    static void SetCord(Transform cord, float length)
    {
        Vector3 s = cord.parent != null ? cord.parent.lossyScale : Vector3.one;
        float sx = Mathf.Max(s.x, 1e-4f), sy = Mathf.Max(s.y, 1e-4f), sz = Mathf.Max(s.z, 1e-4f);
        cord.localRotation = Quaternion.identity;
        cord.localPosition = new Vector3(0f, length * .5f / sy, 0f);
        cord.localScale = new Vector3(CordDiameter / sx, length * .5f / sy, CordDiameter / sz);
    }

    static void PendantPhotos(Shared.Run run)
    {
        Shared.GameCameraPhoto(run, "01-the-game-camera");
        Shared.Photo(run, "02-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.1f, 12f), 72f);
        Shared.Photo(run, "03-walking-mid-room-to-the-counter", new Vector3(0f, 1.65f, 6f), new Vector3(0f, 1.45f, 13.5f), 60f);
        Shared.Photo(run, "04-seated-at-the-communal-table", new Vector3(-4.5f, 1.2f, 6.2f), new Vector3(0f, 1.5f, 7.5f), 60f);
        Shared.Photo(run, "05-the-bistro-tables-from-the-window-counter", new Vector3(6f, 1.2f, 7.2f), new Vector3(3f, 1.6f, 6.2f), 60f);
        CounterCameraPhoto(run, "06-the-counter-camera");
    }

    // ================================================================== THE SHELF LIGHTS

    sealed class ShelfPlan
    {
        public readonly List<(Light light, Color colour, float intensity, float range)> lights = new List<(Light, Color, float, float)>();
        public CafeDaylight daylight;
    }

    static ShelfPlan SurveyShelf(Shared.Run run)
    {
        var p = new ShelfPlan();
        p.daylight = Shared.InScene<CafeDaylight>(run).FirstOrDefault();
        foreach (Light l in Shared.InScene<Light>(run, false))
            if (l.gameObject.activeInHierarchy && l.transform.IsChildOf(run.Cafe) && l.name.StartsWith(ShelfLightPrefix, StringComparison.Ordinal))
                p.lights.Add((l, l.color, l.intensity, l.range));
        run.Require(p.lights.Count > 0, $"No light named \"{ShelfLightPrefix}…\" under the café's root: Bar 2's lights under the long shelf aren't there.");
        run.Line($"The lights under the long shelf ({p.lights.Count}; each halved, nothing else touched):");
        foreach ((Light l, Color colour, float intensity, float range) in p.lights)
            run.Info($"{Shared.PathOf(l.transform)} at {Shared.V(l.transform.position)}: {l.type}, #{ColorUtility.ToHtmlStringRGB(colour)}, intensity {Shared.F(intensity, "0.00")} -> {Shared.F(intensity * ShelfLightFactor, "0.00")}, range {Shared.F(range, "0.0")}" +
                     (p.daylight != null && p.daylight.warmLights != null && p.daylight.warmLights.Contains(l) ? " (a warm light: dims by day and at night)" : "") + ".");
        Light menu = Shared.InScene<Light>(run, false).FirstOrDefault(l => l.gameObject.activeInHierarchy && l.name.StartsWith("Menu board picture light", StringComparison.Ordinal));
        if (menu != null) run.Info($"Left alone (the brief: every other light): the menu board's picture light at {Shared.F(menu.intensity, "0.00")}, range {Shared.F(menu.range, "0.0")}, which the reviewers saw blow the tile out above the shelf too.");
        return p;
    }

    static void ChangeShelf(Shared.Run run, ShelfPlan p)
    {
        if (p == null) throw new InvalidOperationException("The survey didn't run.");
        foreach ((Light l, Color colour, float intensity, float range) in p.lights)
            Shared.ChangeLight(run, l, x => x.intensity = intensity * ShelfLightFactor,
                               $"{l.name}: {Shared.F(intensity, "0.00")} -> {Shared.F(intensity * ShelfLightFactor, "0.00")}: 22 cm off the tile it blew the wall out to white (the reviewers, 19 Oct; her: \"it probably gets dark in here though\" is the room, not this wall)");
        Shared.Note(run, $"Café dressing 3: {p.lights.Count} shelf light(s) halved ({string.Join(", ", p.lights.Select(x => Shared.F(x.intensity, "0.00") + " -> " + Shared.F(x.intensity * ShelfLightFactor, "0.00")))}).");
        run.Line("Checked after:");
        foreach ((Light l, Color colour, float intensity, float range) in p.lights)
        {
            run.Check(Mathf.Abs(l.intensity - intensity * ShelfLightFactor) < 1e-4f, $"{l.name}: intensity {Shared.F(l.intensity, "0.00")} (was {Shared.F(intensity, "0.00")})");
            run.Check(l.color == colour && Mathf.Abs(l.range - range) < 1e-4f && l.enabled, $"{l.name}: colour #{ColorUtility.ToHtmlStringRGB(l.color)} and range {Shared.F(l.range, "0.0")} as they were, still on");
            if (p.daylight != null) run.Check(p.daylight.warmLights != null && p.daylight.warmLights.Contains(l), $"{l.name}: still one of CafeDaylight's warm lights");
        }
    }

    static void ShelfPhotos(Shared.Run run)
    {
        Shared.Photo(run, "01-the-back-bar-from-the-counter", new Vector3(0f, 1.65f, 15f), new Vector3(-1f, 1.4f, 18f), 70f);
        Shared.Photo(run, "02-the-tile-under-the-shelf", new Vector3(.5f, 1.45f, 16.3f), new Vector3(.5f, 1.5f, 17.95f), 60f);
        Shared.Photo(run, "03-the-drinks-corner", new Vector3(2.4f, 1.65f, 15.6f), new Vector3(3.6f, 1.2f, 17.6f), 62f);
        BeverageStation station = Shared.InScene<BeverageStation>(run, false).FirstOrDefault(b => b.gameObject.activeInHierarchy);
        StationInteractable at = station != null ? station.GetComponentInParent<StationInteractable>() : null;
        if (station != null)
        {
            Vector3 eye = (at != null && at.StandPoint != null ? at.StandPoint.position : station.transform.position - station.transform.forward * .9f) + Vector3.up * 1.65f;
            Shared.Photo(run, "04-at-the-dispenser", eye, station.transform.position + Vector3.up * 1.2f, 62f);
        }
        Shared.Photo(run, "05-the-menu-from-the-queue", new Vector3(0f, 1.6f, 12.6f), new Vector3(0f, 2.45f, 18f), 50f);
        Shared.GameCameraPhoto(run, "06-the-game-camera");
    }

    // ================================================================== THE PHOTOGRAPHS (read-only): for the reviewers

    static void RoomPhotos(Shared.Run run)
    {
        Shared.GameCameraPhoto(run, "1-the-game-camera");
        CounterCameraPhoto(run, "2-the-counter-camera");
        Shared.Photo(run, "3-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.1f, 12f), 72f);
        Shared.Photo(run, "4-the-waiting-nook", new Vector3(3.2f, 1.6f, 5.4f), new Vector3(6.9f, 1f, 2.3f), 62f);
        Shared.Photo(run, "5-the-front-left", new Vector3(-1.9f, 1.55f, 1f), new Vector3(-6.2f, .55f, 5.2f), 62f);
        Shared.Photo(run, "6-the-back-bar", new Vector3(0f, 1.65f, 15f), new Vector3(-1f, 1.4f, 18f), 70f);
    }

    // ================================================================== measuring

    // A thing's meshes' box about its own root (the root's scale inside the box; enabled renderers only).
    static Bounds LocalBox(Transform root)
    {
        Matrix4x4 toRoot = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
        bool any = false;
        var result = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Renderer r = f.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;
            Bounds b = BlenderKitSteps.Transformed(f.sharedMesh.bounds, toRoot * f.transform.localToWorldMatrix);
            if (!any) { result = b; any = true; } else result.Encapsulate(b);
        }
        return result;
    }

    // A kit asset's meshes' box about its origin; how far it is imported turned, and its root's scale.
    static Bounds AssetBox(GameObject asset, out float turned, out Vector3 scale)
    {
        bool any = false;
        var result = new Bounds(Vector3.zero, Vector3.zero);
        turned = Quaternion.Angle(asset.transform.localRotation, Quaternion.identity);
        scale = asset.transform.localScale;
        foreach (MeshFilter f in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
            turned = Mathf.Max(turned, Quaternion.Angle(toRoot.rotation, Quaternion.identity));
            Bounds b = BlenderKitSteps.Transformed(f.sharedMesh.bounds, toRoot);
            if (!any) { result = b; any = true; } else result.Encapsulate(b);
        }
        return result;
    }

    // The highest drawn surface of a thing straight under `from`, down to `depth` below it (CafeSeatsSteps.MeshTopUnder).
    static float? MeshTopUnder(GameObject piece, Vector3 from, float depth)
    {
        float? best = null;
        foreach (MeshFilter f in piece.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = f.sharedMesh;
            if (m == null) continue;
            Vector3[] v;
            int[] tri;
            try { v = m.vertices; tri = m.triangles; }
            catch (Exception) { continue; }
            Matrix4x4 w = f.transform.localToWorldMatrix;
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                Vector3 a = w.MultiplyPoint3x4(v[tri[i]]), b = w.MultiplyPoint3x4(v[tri[i + 1]]), c = w.MultiplyPoint3x4(v[tri[i + 2]]);
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
        return best;
    }

    // ---------------------------------------------------------------- from above (Art's 12-pixel rule; CafeDressingSteps' home view)

    sealed class Overhead
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public string how;
    }

    // The overhead as Play starts it (CafeViewMode.Start): the camera's yaw and pitch, its distance from isometricFocus clamped
    // to the zoom range, its lens. Without the camera: Art's numbers (43 degrees, 34 m from (0, 0.6, 9), FOV 44).
    static Overhead HomeView(Shared.Run run)
    {
        CafeViewMode view = Shared.InScene<CafeViewMode>(run).FirstOrDefault();
        CinemachineCamera shop = view != null ? view.isometricCamera : null;
        if (view != null && shop != null)
        {
            float yaw = shop.transform.eulerAngles.y, pitch = Mathf.DeltaAngle(0f, shop.transform.eulerAngles.x);
            float distance = Mathf.Clamp(Vector3.Distance(shop.transform.position, view.isometricFocus), view.minimumDistance, view.maximumDistance);
            Quaternion angle = Quaternion.Euler(pitch, yaw, 0f);
            float fov = shop.Lens.FieldOfView > 1f ? shop.Lens.FieldOfView : 44f;
            return new Overhead
            {
                position = view.isometricFocus - angle * Vector3.forward * distance, rotation = angle, fov = fov,
                how = $"the home view: {Shared.F(pitch, "0")} degrees down, {Shared.F(distance, "0.0")} m from {Shared.V(view.isometricFocus)}, FOV {Shared.F(fov, "0")}"
            };
        }
        Quaternion art = Quaternion.Euler(43f, 0f, 0f);
        return new Overhead { position = new Vector3(0f, .6f, 9f) - art * Vector3.forward * 34f, rotation = art, fov = 44f, how = "Art's numbers (no overhead camera found): 43 degrees, 34 m, FOV 44" };
    }

    // The rectangle round a set of world points through that view, at 1920 x 1080, in pixels.
    static Rect OnScreen(Overhead o, IEnumerable<Vector3> points)
    {
        float t = Mathf.Tan(o.fov * .5f * Mathf.Deg2Rad), aspect = ScreenW / (float)ScreenH;
        Quaternion un = Quaternion.Inverse(o.rotation);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (Vector3 world in points)
        {
            Vector3 c = un * (world - o.position);
            if (c.z < .1f) continue;
            float sx = (c.x / (c.z * t * aspect) * .5f + .5f) * ScreenW, sy = (c.y / (c.z * t) * .5f + .5f) * ScreenH;
            x0 = Mathf.Min(x0, sx); x1 = Mathf.Max(x1, sx); y0 = Mathf.Min(y0, sy); y1 = Mathf.Max(y1, sy);
        }
        return x0 > x1 ? new Rect(0f, 0f, 0f, 0f) : Rect.MinMaxRect(x0, y0, x1, y1);
    }

    // A part's mesh vertices in the world at a place (its source's or its kit asset's meshes), or its box's corners.
    static IEnumerable<Vector3> PartVertices(Part part, Placement c)
    {
        Matrix4x4 pose = Matrix4x4.TRS(c.World(part.at), c.Turn(part.turn), Vector3.one);
        GameObject from = part.source != null ? part.source.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(Shared.KitModels + "/" + part.piece + ".fbx");
        if (from == null) return Corners(BlenderKitSteps.Transformed(part.local, pose));
        Matrix4x4 toRoot = part.source != null ? Matrix4x4.TRS(part.source.position, part.source.rotation, Vector3.one).inverse : from.transform.worldToLocalMatrix;
        var points = new List<Vector3>();
        foreach (MeshFilter f in from.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Matrix4x4 m = pose * toRoot * f.transform.localToWorldMatrix;
            Vector3[] v;
            try { v = f.sharedMesh.vertices; } catch (Exception) { v = new Vector3[0]; }
            if (v.Length == 0) points.AddRange(Corners(BlenderKitSteps.Transformed(f.sharedMesh.bounds, m)));
            else foreach (Vector3 local in v) points.Add(m.MultiplyPoint3x4(local));
        }
        return points.Count > 0 ? points : Corners(BlenderKitSteps.Transformed(part.local, pose));
    }

    static IEnumerable<Vector3> PlacedVertices(GameObject go)
    {
        var points = new List<Vector3>();
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Matrix4x4 m = f.transform.localToWorldMatrix;
            Vector3[] v;
            try { v = f.sharedMesh.vertices; } catch (Exception) { v = new Vector3[0]; }
            if (v.Length == 0) points.AddRange(Corners(BlenderKitSteps.Transformed(f.sharedMesh.bounds, m)));
            else foreach (Vector3 local in v) points.Add(m.MultiplyPoint3x4(local));
        }
        return points;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    // ---------------------------------------------------------------- small helpers

    // A piece's box for "nothing passes through it": shrunk 3 mm all round, its bottom lifted 1.2 cm so what it rests on
    // (the floor, or the 1 cm rug under the table and the chairs' fronts) only touches it; a rug, 1 cm thick, is lifted 2 mm.
    static Bounds ThroughBox(Part part) => ThroughBox(part.local, part.isRug);

    static Bounds ThroughBox(Bounds local, bool rug)
    {
        float lift = rug ? .002f : .012f;
        var b = new Bounds(local.center, Vector3.Max(local.size - Vector3.one * 2f * Shrink, Vector3.one * .001f));
        b.SetMinMax(new Vector3(b.min.x, Mathf.Min(local.min.y + lift, b.max.y - .001f), b.min.z), b.max);
        return b;
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

    static float PointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
        Vector2 ab = B - A;
        float t = ab.sqrMagnitude < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
        return Vector2.Distance(P, A + ab * t);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    static bool Near(Vector3 a, Vector3 b, float slack) => Mathf.Abs(a.x - b.x) <= slack && Mathf.Abs(a.y - b.y) <= slack && Mathf.Abs(a.z - b.z) <= slack;

    static string NewFolder(string label)
    {
        string folder = Path.Combine(Shared.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
#endif
