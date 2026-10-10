#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// SEATS 1: THE SAN FRANCISCO SEAT PLAN (the café redesign, 10 Oct 2026; brief U-Seats;
// claude/studio/cafe-sf-research-and-redesign.md §6.2, §6.11, §7.3, §7.5, §7.6; the seating lead's report §5)
//
// His decision (9 Oct, 23:40): the San Francisco seat plan, 24 seats, "i want detail for every item in the cafe itself".
// Today the café seats 21: four round 4-tops in the middle of the room (16) and a lounge by the west window (two sofas,
// a tub chair: 5). This step switches all of that off (never deletes it) and lays the plan's 24, each a working
// TableSeat set up the way today's are:
//
//   Repair bar      3 x CK_Stool_Bar at (-5.2 / -4.2 / -3.2, 0, 12.85), facing the bench                         3
//   West banquette  the built-in bench: 3 x CK_BanquetteBay at (-7.40, 0, 4.69 / 6.79 / 8.89) and its two ends,
//                   a CK_Table_Square2 in front of each bay (x -6.30) and a CK_Chair_Cafe across it (x -5.60)    6
//   Communal        CK_CommunalTable at (-2.95, 0, 6.20), 8 x CK_Stool_Low at x -3.75 / -2.15, z 4.85 to 7.55   8
//   Bistro          2 x CK_Table_BistroRound at (3.0, 4.6) and (3.0, 7.8), 4 x CK_Chair_Spindle at x 2.38 / 3.62  4
//   Window counter  CK_WindowCounter on the east wall at z 7.20, 3 x CK_Stool_Bar at (6.50, z 6.00 / 7.20 / 8.40)  3
//
// Off: the four 4-tops (their 16 seats and colliders), the sixteen timber chairs drawn on them and the caddies on their
// tops; the two sofas, the tub chair and the lounge's five seats; the coffee table and what stood on it, the two floor
// lamps (their lights with them), the book and throw on the sofa; the oak bookcase, its books and plant (11 - neighborhood
// reading shelf); the old banquette stand-in (its timber base was still solid where the new banquette goes); the east
// window bar: its two high-tops, four stools, what stood on them and the sill plant the new counter would swallow.
//
// EVERY SEAT: its sit point from the manifest (seats[].pose in the piece's frame; the banquette's at its 2-top's centre
// along the bay); its cup spot on the top in front of the sitter (the seating report §5); its stand point on the walkable
// floor, facing the table: today's 0.67 m behind the seat wherever the room allows it, and where it doesn't (the west zone,
// where every waiting body's 1.03 m envelope closes the aisle) the place the occupied-circulation guard serves, found
// with a replica of the guard (steps-seats/analysis/final_plan.py prints every number and the map). Style Bench for the
// banquette (footprint 0.3, as the lounge's benches), Chair for the rest. Drain 0.6 everywhere.
//
// THE BAR STOOLS DON'T SIT (Snap To Seat off on the six). The sit clips are fitted to the café's 0.45 m chairs, and
// NpcSeating lifts a body at most 12 cm to meet a seat (maxHeightCorrection): on a 0.745 m stool the hips would land about
// 14 cm under where the clip wants them, 5 cm inside the seat, the thighs through it (Sit 4 allows 5 cm). Off, a customer
// waits standing at the stool's stand point facing the counter, as the grey-box did; the seat still counts as a seat
// (drain 0.6, the guards, the claims), with a 0.3 m claim footprint so that neighbours 1.0 m apart can both be taken.
// Tick Snap To Seat on the six to see them sit anyway; a tall-stool sit clip is the real fix (not built: no new mechanic).
//
// THE SOLIDS: each piece gets the manifest's box (cafe_seating.json `collider`) on its root; the step's group carries a
// Not Walkable NavMeshModifier (as the tables' and the lounge's groups do), so no table top or cushion becomes a step
// people can climb. The routes are re-baked.
//
//   Fixit Fidget > Cafe redesign > Seats 1 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Seats 1 1 - Lay the seats (scene)
//   Fixit Fidget > Cafe redesign > Seats 1 1 - Undo (scene)
//
// Everything runs through CafeRedesignShared.Step: the survey refuses with its reasons and changes nothing; one undo group
// and the record; the checks after (any FAIL puts everything back); the routes re-baked and the guards before and after
// (and this step's own: 24 seats, 31/31 PASS); photos before and after; never saved. Report and photos:
// Logs/CafeRedesign/seats-1-<time>/; the last word: Logs/CafeRedesign/seats-1-last.txt.
// ---------------------------------------------------------------------------
internal static class CafeSeatsSteps
{
    public const string Title = "Seats 1 - The San Francisco seat plan";
    const string Label = "seats-1";
    const string GroupName = "Seats 1 - the San Francisco seat plan";
    const string BarStep = "Bar 1 - The front counter";
    const string MenuCheck = Shared.Menu + "Seats 1 0 - Check first (read-only)";
    const string MenuDo = Shared.Menu + "Seats 1 1 - Lay the seats (scene)";
    const string MenuUndo = Shared.Menu + "Seats 1 1 - Undo (scene)";

    // ================================================================== menus

    [MenuItem(MenuCheck)]
    static void CheckFirst() => Shared.Check(Title, Label + "-check", Survey);

    [MenuItem(MenuDo)]
    static void LayTheSeats() => Shared.Step(Title, Label, Survey, Change, Photos);

    [MenuItem(MenuUndo)]
    static void TakeTheSeatsUp() => Shared.UndoStep(Title, Label);

    [MenuItem(MenuCheck, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the rules (each with where it comes from)

    const float StandY = .04f;            // the NavMesh's height over the floor: today's snapped stand points sit at 0.040 to 0.043
    const float SeatDrain = .6f;          // every seat (the brief; NeighborhoodRefreshChecks: "table seats drain patience at 0.6")
    const float BenchFootprint = .3f;     // a bench seat's claim footprint (TableSeat.clearanceRadius; the lounge's benches: 0.3), and a
                                          // bar stool's: standing at it (Snap To Seat off) its footprint would be the body's 0.35 plus
                                          // its 0.30 stopping margin, and two stools 1.0 m apart couldn't both be claimed (WaitingArea)
    const float NavMargin = .40f;         // a stand point at least this far from every solid: the agent's 0.35 and a 0.08 voxel's slack
    const float SeatClaim = .70f;         // WaitingArea.HasClaimClearance between two seats: 0.3 + 0.3 + 0.1
    const float SeatToWaiting = 1.05f;    // ... between a seat and a loiter spot: 0.3 + (0.35 agent + 0.30 stopping) + 0.1
    const float SeatToQueue = .90f;       // a stand point clear of a queue place by the queue's keep-clear
    const float KeepSeat = .75f, KeepWaiting = .80f, KeepQueue = .90f, KeepStation = .80f, KeepSpawn = 1f;  // CafeFurnishing.BuildKeepClear
    const float LaneX0 = -1f, LaneX1 = 1f, LaneZ0 = -1.2f, LaneZ1 = 12.5f;    // the door-to-counter lane (CafeFurnishing.CheckFloorClearance)
    const float LedgeZ = 13.45f;          // the repair-bar cups: on the front counter's ledge (z 13.30 to 13.65), 0.15 m in from its front
    const float LedgeLow = 1.06f, LedgeHigh = 1.14f;   // the ledge's top as Bar 1 built it: 1.095 (the bar report §5); measured live
    const float KitSlack = .003f;         // the kit's sizes are written to the millimetre
    const float Mm = .001f;
    const float SeatEyeAbove = .755f;     // a seated adult's eye over the seat: 0.445 + 0.755 = the brief's 1.20

    // ================================================================== the kit (cafe_seating.json, 10 Oct)

    // Each piece in its own frame (Unity axes; the front is local +Z; free pieces: origin at the bottom centre; wall pieces:
    // the bottom centre of the back, the back at local z 0): its mesh's box, its solid (the manifest's `collider`) and its
    // seat's surface centre (the manifest's seats[0].pose).
    // backFrom..backTo: where the top 15 % of the piece's height lies, front to back (local z), for the pieces with a back:
    // a chair's or bar stool's backrest behind its seat, the banquette's back at the wall. That is the proof, from the
    // pieces themselves, that their front is local +Z as imported (the FBX files read directly, analysis/fbx_bounds.py:
    // café chair -0.231, spindle -0.212, bar stool -0.168, bay +0.074).
    sealed class Kit
    {
        public readonly string piece;
        public readonly Vector3 min, max, boxCentre, boxSize;
        public readonly Vector3? pose;
        public readonly bool wall;
        public readonly float backFrom, backTo;
        public Kit(string piece, Vector3 min, Vector3 max, Vector3 boxCentre, Vector3 boxSize, Vector3? pose, bool wall, float backFrom = float.NaN, float backTo = float.NaN)
        {
            this.piece = piece; this.min = min; this.max = max; this.boxCentre = boxCentre; this.boxSize = boxSize; this.pose = pose; this.wall = wall;
            this.backFrom = backFrom; this.backTo = backTo;
        }
        public float Top => max.y;
        public bool HasBack => !float.IsNaN(backFrom);
    }

    static readonly Kit Bay = new Kit("CK_BanquetteBay", V(-1.05f, 0f, 0f), V(1.05f, .765f, .554f), V(0f, .3825f, .277f), V(2.1f, .765f, .554f), V(0f, .444f, .28f), true, -.02f, .15f);
    static readonly Kit End = new Kit("CK_BanquetteEnd", V(-.02f, 0f, 0f), V(.02f, .765f, .565f), V(0f, .3825f, .2825f), V(.04f, .765f, .565f), null, true);
    static readonly Kit Square = new Kit("CK_Table_Square2", V(-.36f, 0f, -.36f), V(.36f, .75f, .36f), V(0f, .375f, 0f), V(.72f, .75f, .72f), null, false);
    static readonly Kit Round = new Kit("CK_Table_BistroRound", V(-.36f, 0f, -.36f), V(.36f, .75f, .36f), V(0f, .375f, 0f), V(.72f, .75f, .72f), null, false);
    static readonly Kit Communal = new Kit("CK_CommunalTable", V(-.45f, 0f, -1.8f), V(.45f, .75f, 1.8f), V(0f, .375f, 0f), V(.9f, .75f, 3.6f), null, false);
    static readonly Kit Counter = new Kit("CK_WindowCounter", V(-2f, .34f, 0f), V(2f, 1.05f, .45f), V(0f, .695f, .225f), V(4f, .71f, .45f), null, true);
    static readonly Kit CafeChair = new Kit("CK_Chair_Cafe", V(-.2235f, 0f, -.26f), V(.2235f, .895f, .261f), V(0f, .4475f, .0005f), V(.447f, .895f, .521f), V(0f, .4444f, .0356f), false, -.30f, -.10f);
    static readonly Kit Spindle = new Kit("CK_Chair_Spindle", V(-.212f, 0f, -.242f), V(.212f, .863f, .242f), V(0f, .4315f, 0f), V(.424f, .863f, .484f), V(0f, .443f, .0303f), false, -.30f, -.10f);
    static readonly Kit BarStool = new Kit("CK_Stool_Bar", V(-.1925f, 0f, -.2f), V(.1925f, 1.039f, .201f), V(0f, .5195f, .0005f), V(.385f, 1.039f, .401f), V(0f, .7449f, .0065f), false, -.25f, -.08f);
    static readonly Kit LowStool = new Kit("CK_Stool_Low", V(-.1705f, 0f, -.163f), V(.1705f, .452f, .163f), V(0f, .226f, 0f), V(.341f, .452f, .326f), V(.0115f, .4452f, 0f), false);
    static readonly Kit[] Kits = { Bay, End, Square, Round, Communal, Counter, CafeChair, Spindle, BarStool, LowStool };

    // ================================================================== the plan (Claude's QA decisions, common §6; the seating report §5)

    sealed class Piece
    {
        public readonly string name, group;
        public readonly Kit kit;
        public readonly Vector3 at;
        public readonly float yaw;
        public GameObject placed;
        public BoxCollider box;
        public Piece(string name, Kit kit, string group, float x, float z, float yaw) { this.name = name; this.kit = kit; this.group = group; at = new Vector3(x, 0f, z); this.yaw = yaw; }
        public Quaternion Turn => Quaternion.Euler(0f, yaw, 0f);
        public Vector3 World(Vector3 local) => at + Turn * local;
        public float Front => Shared.Facing(Turn);
        public Bounds MeshBox => Turned(at, Turn, kit.min, kit.max);
        public Bounds SolidBox => Turned(at, Turn, kit.boxCentre - kit.boxSize * .5f, kit.boxCentre + kit.boxSize * .5f);
        public float TopY => at.y + kit.Top;
        // The horizontal distance from p to the solid's footprint (exact for the turned box).
        public float FlatDistance(Vector3 p)
        {
            Vector3 local = Quaternion.Inverse(Turn) * (p - at);
            float dx = Mathf.Max(Mathf.Abs(local.x - kit.boxCentre.x) - kit.boxSize.x * .5f, 0f);
            float dz = Mathf.Max(Mathf.Abs(local.z - kit.boxCentre.z) - kit.boxSize.z * .5f, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
        // Whether p (seen from above) lies inside the top's footprint, at least `inset` in from its edges.
        public bool OverTop(Vector3 p, float inset)
        {
            Vector3 local = Quaternion.Inverse(Turn) * (p - at);
            return local.x >= kit.min.x + inset && local.x <= kit.max.x - inset && local.z >= kit.min.z + inset && local.z <= kit.max.z - inset;
        }
    }

    const string GRepair = "Repair bar stools", GBanquette = "Banquette (west wall)", GCommunal = "Communal table", GWindow = "Window counter";
    static string GTwoTop(string which) => $"Banquette 2-top ({which})";
    static string GBistro(string which) => $"Bistro 2-top ({which})";

    // The three bays, each with its 2-top in front (declared before the tables below: static fields start in this order).
    static readonly (string label, float z)[] Bays = { ("south", 4.69f), ("middle", 6.79f), ("north", 8.89f) };

    static readonly Piece[] Pieces = MakePieces();

    static Piece[] MakePieces()
    {
        var list = new List<Piece>();
        foreach ((string label, float x) in new[] { ("west", -5.2f), ("middle", -4.2f), ("east", -3.2f) })
            list.Add(new Piece($"CK_Stool_Bar (repair bar, {label})", BarStool, GRepair, x, 12.85f, 0f));
        list.Add(new Piece("CK_BanquetteEnd (banquette, south end)", End, GBanquette, -7.40f, 3.62f, 90f));
        foreach ((string label, float z) in Bays)
            list.Add(new Piece($"CK_BanquetteBay (banquette, {label} bay)", Bay, GBanquette, -7.40f, z, 90f));
        list.Add(new Piece("CK_BanquetteEnd (banquette, north end)", End, GBanquette, -7.40f, 9.96f, 90f));
        foreach ((string label, float z) in Bays)
        {
            list.Add(new Piece($"CK_Table_Square2 (banquette 2-top, {label})", Square, GTwoTop(label), -6.30f, z, 0f));
            list.Add(new Piece($"CK_Chair_Cafe (banquette 2-top, {label})", CafeChair, GTwoTop(label), -5.60f, z, -90f));
        }
        list.Add(new Piece("CK_CommunalTable (communal table)", Communal, GCommunal, -2.95f, 6.20f, 0f));
        for (int i = 0; i < 4; i++)
        {
            float z = 4.85f + .9f * i;
            // The stools face the table: the west row turned +90 (its front, local +Z, to +x, east), the east row -90.
            list.Add(new Piece($"CK_Stool_Low (communal table, west {i + 1})", LowStool, GCommunal, -3.75f, z, 90f));
            list.Add(new Piece($"CK_Stool_Low (communal table, east {i + 1})", LowStool, GCommunal, -2.15f, z, -90f));
        }
        foreach ((string label, float z) in new[] { ("south", 4.6f), ("north", 7.8f) })
        {
            list.Add(new Piece($"CK_Table_BistroRound (bistro 2-top, {label})", Round, GBistro(label), 3.0f, z, 0f));
            list.Add(new Piece($"CK_Chair_Spindle (bistro 2-top, {label}, west)", Spindle, GBistro(label), 2.38f, z, 90f));
            list.Add(new Piece($"CK_Chair_Spindle (bistro 2-top, {label}, east)", Spindle, GBistro(label), 3.62f, z, -90f));
        }
        list.Add(new Piece("CK_WindowCounter (window counter)", Counter, GWindow, 7.40f, 7.20f, -90f));
        foreach ((string label, float z) in new[] { ("south", 6.00f), ("middle", 7.20f), ("north", 8.40f) })
            list.Add(new Piece($"CK_Stool_Bar (window counter, {label})", BarStool, GWindow, 6.50f, z, 90f));
        return list.ToArray();
    }

    // Every seat: the piece it is on and its table's group; its sit point, cup spot and stand point in the world (the step
    // re-derives the sit point from the piece and the manifest and checks it against these to the millimetre); the stand
    // point's reason. analysis/final_plan.py prints the same table and scores it against the guard (31/31).
    sealed class Seat
    {
        public readonly string name, piece, group, why;
        public readonly bool bench, snap;
        public readonly Vector3 pose, stand;
        public Vector3 cup;                     // the repair bar's height is measured on the ledge in the survey
        public Piece on;
        public GameObject made;
        public TableSeat seat;
        public Seat(string name, string piece, string group, bool bench, bool snap, Vector3 pose, Vector3 cup, float standX, float standZ, string why)
        {
            this.name = name; this.piece = piece; this.group = group; this.bench = bench; this.snap = snap; this.pose = pose; this.cup = cup;
            stand = new Vector3(standX, StandY, standZ); this.why = why;
        }
        public Quaternion Facing => Quaternion.LookRotation(Flat(cup - pose), Vector3.up);
        public Quaternion StandFacing => Quaternion.LookRotation(Flat(cup - stand), Vector3.up);
    }

    static readonly Seat[] Seats = MakeSeats();

    static Seat[] MakeSeats()
    {
        var s = new List<Seat>();
        const string behind = "today's rule: 0.67 m behind the seat, facing the table";
        foreach ((string label, float x) in new[] { ("west", -5.2f), ("middle", -4.2f), ("east", -3.2f) })
            s.Add(new Seat($"Seat (repair bar, {label})", $"CK_Stool_Bar (repair bar, {label})", GRepair, false, false,
                           V(x, .7449f, 12.8565f), V(x, 1.095f, LedgeZ), x, 12.1865f, behind));
        (float x, float z, string why)[] bench =
        {
            (-6.24f, 3.36f, "south of the 2-top, past the banquette's south end: the sitter steps along the cushion from the south"),
            (-6.09f, 7.77f, "north of the 2-top, in the 2.1 m between the middle and north 2-tops"),
            (-6.34f, 9.72f, "north of the 2-top, by the banquette's north end"),
        };
        (float x, float z, string why)[] chair =
        {
            (-5.286f, 5.49f, "behind the chair and 0.80 m to its north (0.67 straight behind closes the west aisle)"),
            (-5.336f, 7.49f, "behind the chair and 0.70 m to its north"),
            (-5.536f, 9.94f, "beside the chair, 1.05 m to its north, short of the waiting spot by the counter"),
        };
        for (int i = 0; i < 3; i++)
        {
            (string label, float z) = Bays[i];
            s.Add(new Seat($"Seat (banquette 2-top, {label}, on the banquette)", $"CK_BanquetteBay (banquette, {label} bay)", GTwoTop(label), true, true,
                           V(-7.12f, .444f, z), V(-6.48f, .75f, z), bench[i].x, bench[i].z, bench[i].why));
            s.Add(new Seat($"Seat (banquette 2-top, {label}, chair)", $"CK_Chair_Cafe (banquette 2-top, {label})", GTwoTop(label), false, true,
                           V(-5.6356f, .4444f, z), V(-6.12f, .75f, z), chair[i].x, chair[i].z, chair[i].why));
        }
        (float x, float z)[] west = { (-4.60f, 5.088f), (-4.40f, 5.788f), (-4.35f, 6.639f), (-4.60f, 7.338f) };
        for (int i = 0; i < 4; i++)
        {
            float z = 4.85f + .9f * i;
            s.Add(new Seat($"Seat (communal table, west {i + 1})", $"CK_Stool_Low (communal table, west {i + 1})", GCommunal, false, true,
                           V(-3.75f, .4452f, z - .0115f), V(-3.20f, .75f, z - .0115f), west[i].x, west[i].z,
                           "behind the stool, eased along the row so the west row and the banquette chairs leave a way through"));
            s.Add(new Seat($"Seat (communal table, east {i + 1})", $"CK_Stool_Low (communal table, east {i + 1})", GCommunal, false, true,
                           V(-2.15f, .4452f, z + .0115f), V(-2.70f, .75f, z + .0115f), -1.48f, z + .0115f, behind));
        }
        foreach ((string label, float z) in new[] { ("south", 4.6f), ("north", 7.8f) })
        {
            s.Add(new Seat($"Seat (bistro 2-top, {label}, west)", $"CK_Chair_Spindle (bistro 2-top, {label}, west)", GBistro(label), false, true,
                           V(2.4103f, .443f, z), V(2.80f, .75f, z), 1.6603f, z, "0.75 m behind the seat (at 0.67 the stand point touches the chair's box)"));
            s.Add(new Seat($"Seat (bistro 2-top, {label}, east)", $"CK_Chair_Spindle (bistro 2-top, {label}, east)", GBistro(label), false, true,
                           V(3.5897f, .443f, z), V(3.20f, .75f, z), 4.3397f, z, "0.75 m behind the seat"));
        }
        foreach ((string label, float z) in new[] { ("south", 6.00f), ("middle", 7.20f), ("north", 8.40f) })
            s.Add(new Seat($"Seat (window counter, {label})", $"CK_Stool_Bar (window counter, {label})", GWindow, false, false,
                           V(6.5065f, .7449f, z), V(7.10f, 1.05f, z), 5.8365f, z, behind));
        return s.ToArray();
    }

    // ================================================================== what goes off (paths under the café's root, 216cc1a)

    // Checked in the code (Assets/Editor, Assets/Scripts): no lab, no night set-up and no runtime script finds any of
    // these by name. The builders that made them (CafeFurnishing, CafeInteriorPass, CafeNpcPass2Setup, CafeStreetUpgrade,
    // NeighborhoodVisualRefresh) and NpcLifeChecks / NpcBeatChecks / NeighborhoodRefreshChecks ("Lounge seats (pass 2)",
    // a parent's name) do; the three checks are delivered changed (modified/).
    static readonly (string path, string why)[] OffPaths =
    {
        ("05 - seating - four neighborhood tables/Table 1", "the round 4-top at (-3.0, 9.0): its table and four seats (research §7.3: the four 4-tops go)"),
        ("05 - seating - four neighborhood tables/Table 2", "the round 4-top at (3.0, 9.0): its table and four seats"),
        ("05 - seating - four neighborhood tables/Table 3", "the round 4-top at (-3.0, 3.5): its table and four seats"),
        ("05 - seating - four neighborhood tables/Table 4", "the round 4-top at (3.0, 3.5): its table and four seats"),
        ("05 - seating - four neighborhood tables/Window banquette - future seated animation",
         "the old banquette stand-in: drawn by nothing since the lounge came, but its timber base's box (x -7.25..-6.39, z 1.65..8.35) is still solid where the new banquette and 2-tops go"),
        ("12 - authored cafe interior/Window upholstered seating", "the two sofas and the lounge's five seats (two sofas, the tub chair)"),
        ("11 - neighborhood reading shelf", "the oak bookcase by the west window (the brief: the bookcase and its books)"),
        ("18 - cafe furnishing/A - Window lounge/Lounge table", "the lounge's coffee table"),
        ("18 - cafe furnishing/A - Window lounge/Tub chair", "the tub chair"),
        ("18 - cafe furnishing/A - Window lounge/Floor lamp (front sofa)", "the floor lamp by the front sofa (its light goes off with it)"),
        ("18 - cafe furnishing/A - Window lounge/Floor lamp (rear sofa)", "the floor lamp by the reading sofa (its light goes off with it)"),
        ("18 - cafe furnishing/A - Window lounge/Serving tray", "on the coffee table: it would float"),
        ("18 - cafe furnishing/A - Window lounge/Latte cup", "on the coffee table: it would float"),
        ("18 - cafe furnishing/A - Window lounge/Little plant", "on the coffee table: it would float"),
        ("18 - cafe furnishing/A - Window lounge/Book pair", "on the coffee table: it would float"),
        ("18 - cafe furnishing/A - Window lounge/Book left on the sofa", "on the reading sofa: it would float"),
        ("18 - cafe furnishing/A - Window lounge/Folded throw", "on the reading sofa: it would float"),
        ("18 - cafe furnishing/B - Reading nook/Bookcase books", "on the bookcase: they would float"),
        ("18 - cafe furnishing/B - Reading nook/Bookcase plant", "on the bookcase: it would float"),
        ("18 - cafe furnishing/C - Window bar/High-top (courtyard)", "the east window bar's north high-top (the window counter replaces the bar)"),
        ("18 - cafe furnishing/C - Window bar/High-top (gallery)", "the east window bar's south high-top"),
        ("18 - cafe furnishing/C - Window bar/Bar stool (courtyard, south)", "a window-bar stool"),
        ("18 - cafe furnishing/C - Window bar/Bar stool (courtyard, north)", "a window-bar stool"),
        ("18 - cafe furnishing/C - Window bar/Bar stool (gallery, south)", "a window-bar stool"),
        ("18 - cafe furnishing/C - Window bar/Bar stool (gallery, north)", "a window-bar stool"),
        ("18 - cafe furnishing/C - Window bar/Iced coffee", "on a high-top: it would float"),
        ("18 - cafe furnishing/C - Window bar/Phone", "on a high-top: it would float"),
        ("18 - cafe furnishing/C - Window bar/Latte cup", "on a high-top: it would float"),
        ("18 - cafe furnishing/C - Window bar/Newspaper", "on a high-top: it would float"),
    };

    // Names shared by several objects: the one standing nearest `near` (within 5 cm).
    static readonly (string parent, string name, Vector3 near, string why)[] OffNear =
    {
        ("12 - authored cafe interior/Cafe service details", "TableCaddy", V(-3f, .754f, 9f), "on the round 4-top at (-3.0, 9.0): it would float"),
        ("12 - authored cafe interior/Cafe service details", "TableCaddy", V(3f, .754f, 9f), "on the round 4-top at (3.0, 9.0): it would float"),
        ("12 - authored cafe interior/Cafe service details", "TableCaddy", V(-3f, .754f, 3.5f), "on the round 4-top at (-3.0, 3.5): it would float"),
        ("12 - authored cafe interior/Cafe service details", "TableCaddy", V(3f, .754f, 3.5f), "on the round 4-top at (3.0, 3.5): it would float"),
        ("18 - cafe furnishing/C - Window bar", "Sill plant", V(7.31f, .86f, 6.95f), "on the east sill at z 6.95, inside the window counter's span (z 5.20..9.20, under its top): the counter would swallow it"),
    };

    // The sixteen timber chairs drawn on the 4-tops' seats (one by one: their group also holds the two patio chairs, which stay).
    static IEnumerable<(string path, string why)> TimberChairs()
    {
        for (int t = 1; t <= 4; t++)
            for (int k = 1; k <= 4; k++)
                yield return ($"13 - CC0 neighborhood refresh/Warm timber cafe chairs/Table {t} - Seat {k} timber chair", $"the timber chair drawn on Table {t}'s seat {k}");
    }

    // Gameplay a switched-off thing must never hold.
    static bool Gameplay(Component c) =>
        c is StationInteractable || c is ItemSlotArea || c is BeverageStation || c is BeverageCupSupply || c is CounterQueue || c is CustomerSpawner ||
        c is PatronSpawner || c is PlayerMovement || c is NavMeshSurface || (c is WaitingSpot w && !(w is TableSeat));

    // ================================================================== the survey's findings, for the change and the photos

    sealed class Plan
    {
        public readonly List<(GameObject go, string why)> off = new List<(GameObject, string)>();
        public readonly HashSet<Transform> offRoots = new HashSet<Transform>();
        public readonly List<TableSeat> today = new List<TableSeat>();
        public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        public readonly Dictionary<WaitingSpot, bool> loiterOn = new Dictionary<WaitingSpot, bool>();
        public readonly List<Light> lights = new List<Light>();
        public readonly List<string> floaters = new List<string>();
        public readonly List<(Bounds b, string what)> tops = new List<(Bounds, string)>();   // what goes off, drawn, as it stood
        public Transform spawn;
        public bool UnderOff(Transform t) => t != null && offRoots.Any(t.IsChildOf);
    }

    static Plan plan;

    // ================================================================== the survey (changes nothing; refuses with the reasons)

    static void Survey(Shared.Run run)
    {
        var p = plan = new Plan();
        foreach (Seat s in Seats) s.on = Pieces.First(x => x.name == s.piece);

        // ---- what comes first ----
        run.Require(!Shared.Done(run, Title), $"{Title} is already done in this scene: its Undo (Seats 1 1 - Undo) puts it back.");
        run.Require(Shared.Done(run, BarStep), $"'{BarStep}' hasn't run in this scene: the seats come after the front counter (brief U-Seats; the repair-bar cups stand on its ledge).");
        run.Require(Shared.RepairBarDone(run), "The repair bench isn't at the repair bar (Repair bar 1): its three stools face the bench.");
        List<string> steps = Shared.Records(run.Cafe).OrderBy(r => r.order).Select(r => $"{r.order}. {r.step}").ToList();
        run.Info("The redesign's steps already in this scene: " + (steps.Count > 0 ? string.Join("; ", steps) : "none") + "; the repair bar (its own record).");

        // ---- the kit: imported upright at scale 1 about the manifest's origins, and the manifest as this step was written for ----
        run.Line();
        run.Line("The kit (Assets/Art/Models/CafeKit):");
        foreach (Kit k in Kits) KitReady(run, k);
        ManifestMatches(run);

        // ---- what goes off ----
        run.Line();
        run.Line("What goes off (switched off, never deleted):");
        FindOff(run, p);

        // ---- today's 21 seats, mapped ----
        run.Line();
        run.Line("Today's seats (world metres):");
        TodaysSeats(run, p);

        // ---- anything that stood on what goes off ----
        Floaters(run, p, true);

        // ---- the pieces: frames, faces, footprints, the floor and the walls ----
        run.Line();
        run.Line("The pieces (origin, yaw, the heading of the front: degrees clockwise from north):");
        PlanPieces(run, p);

        // ---- the seats: sit points, cups, stand points, the rules between them ----
        run.Line();
        run.Line("The 24 seats:");
        PlanSeats(run, p);

        // ---- what must not move ----
        foreach (KeyValuePair<Transform, Vector3> a in Anchors(run)) p.anchors[a.Key] = a.Value;
        foreach (WaitingSpot w in Shared.InScene<WaitingSpot>(run).Where(w => !(w is TableSeat))) p.loiterOn[w] = w.gameObject.activeInHierarchy;
        run.Info($"Gameplay anchors noted (checked after, 1 mm): {p.anchors.Count}; waiting spots: {p.loiterOn.Count} ({p.loiterOn.Count(kv => kv.Value)} on).");
    }

    // Place() gives a piece's root its own turn, so the mesh must already stand upright, at scale 1, about the root's origin,
    // as the manifest says. A piece imported lying down or at scale 100 would land wrong: refuse instead.
    static void KitReady(Shared.Run run, Kit k)
    {
        string path = Shared.KitModels + "/" + k.piece + ".fbx";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { run.Check(false, $"{path} isn't imported: run Fixit Fidget > Cafe redesign > Kit 1 first."); return; }
        MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        if (filters.Length == 0) { run.Check(false, $"{path} has no mesh."); return; }
        Bounds b = default;
        bool first = true;
        foreach (MeshFilter f in filters)
        {
            Bounds one = Transformed(f.sharedMesh.bounds, asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix);
            if (first) { b = one; first = false; } else b.Encapsulate(one);
        }
        bool ok = Near(b.min, k.min, KitSlack) && Near(b.max, k.max, KitSlack);
        run.Check(ok, $"{k.piece} as imported spans {Shared.BoundsText(b)} about its origin (the manifest: {Shared.BoundsText(MinMax(k.min, k.max))})" +
                      (ok ? "" : ": Kit 1 imported it turned, scaled or off its origin, so it would land wrong. Re-import it upright at scale 1, then run this again."));
        if (!k.HasBack) return;
        // Its front, from the piece itself: where the top 15 % of its height lies, front to back.
        float sum = 0f; int count = 0;
        foreach (MeshFilter f in filters)
        {
            Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
            foreach (Vector3 v in f.sharedMesh.vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(v);
                if (p.y < b.max.y * .85f) continue;
                sum += p.z; count++;
            }
        }
        float mean = count > 0 ? sum / count : float.NaN;
        run.Check(count > 0 && mean >= k.backFrom && mean <= k.backTo,
                  $"{k.piece}: its {(k == Bay ? "back" : "backrest")} (the top 15 % of it, {count} vertices) lies at local z {F(mean)} " +
                  $"({F(k.backFrom, "0.00")} to {F(k.backTo, "0.00")} expected): its front is local +Z, as the yaws below assume.");
    }

    // The seating manifest Kit 1 copied into the project: its colliders and seat poses must be the ones this step lays
    // (to the millimetre). A newer manifest needs a newer step.
    [Serializable] sealed class Manifest { public ManifestPiece[] pieces; }
    [Serializable] sealed class ManifestPiece { public string name; public ManifestBox collider; public ManifestSeat[] seats; }
    [Serializable] sealed class ManifestBox { public float[] centre; public float[] size; }
    [Serializable] sealed class ManifestSeat { public float[] pose; public string style; }

    static void ManifestMatches(Shared.Run run)
    {
        string path = Shared.KitModels + "/cafe_seating.json";
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (text == null) { run.Check(false, $"{path} isn't in the project: Kit 1 copies the four manifests beside the models (common §4)."); return; }
        Manifest m;
        try { m = JsonUtility.FromJson<Manifest>(text.text); }
        catch (Exception e) { run.Check(false, $"{path} doesn't read: {e.Message}"); return; }
        var wrong = new List<string>();
        foreach (Kit k in Kits)
        {
            ManifestPiece mp = m?.pieces?.FirstOrDefault(x => x != null && x.name == k.piece);
            if (mp == null) { wrong.Add(k.piece + " isn't in it"); continue; }
            if (mp.collider == null || !Near(Vec(mp.collider.centre), k.boxCentre, Mm) || !Near(Vec(mp.collider.size), k.boxSize, Mm))
                wrong.Add($"{k.piece}'s collider ({(mp.collider != null ? $"{V3(Vec(mp.collider.centre))} size {V3(Vec(mp.collider.size))}" : "none")}, the step: {V3(k.boxCentre)} size {V3(k.boxSize)})");
            ManifestSeat seat = mp.seats != null && mp.seats.Length > 0 ? mp.seats[0] : null;
            if (k.pose.HasValue != (seat != null) || seat != null && !Near(Vec(seat.pose), k.pose.Value, Mm))
                wrong.Add($"{k.piece}'s seat ({(seat != null ? V3(Vec(seat.pose)) : "none")}, the step: {(k.pose.HasValue ? V3(k.pose.Value) : "none")})");
            if (seat != null && seat.style != (k == Bay ? "Bench" : "Chair")) wrong.Add($"{k.piece}'s sit style {seat.style}");
        }
        run.Check(wrong.Count == 0, wrong.Count == 0
            ? $"{path}: every piece's collider and seat pose is the one this step lays (to the millimetre)."
            : $"{path} differs from what this step was written for: {string.Join("; ", wrong)}. Nothing was changed: the step needs the new numbers.");
    }

    static void FindOff(Shared.Run run, Plan p)
    {
        void Add(Transform t, string path, string why)
        {
            if (t == null) { run.Check(false, $"'{path}' isn't under the café's root (the plan read it in the scene at 216cc1a)."); return; }
            if (!t.gameObject.activeSelf) { run.Info($"Already off: {Shared.PathOf(t)}."); return; }
            if (p.offRoots.Contains(t)) return;
            Component held = t.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && Gameplay(c));
            if (held != null) { run.Check(false, $"{Shared.PathOf(t)} holds {held.GetType().Name} on {held.name}: gameplay never goes off with the furniture."); return; }
            p.off.Add((t.gameObject, why));
            p.offRoots.Add(t);
        }
        foreach ((string path, string why) in OffPaths.Concat(TimberChairs())) Add(Shared.Find(run, path), path, why);
        foreach ((string parent, string name, Vector3 near, string why) in OffNear)
        {
            Transform under = Shared.Find(run, parent);
            List<Transform> found = under == null ? new List<Transform>()
                : under.Cast<Transform>().Where(t => t.name == name && Vector3.Distance(t.position, near) < .05f).ToList();
            if (found.Count > 1) { run.Check(false, $"{found.Count} '{name}' under {parent} at {V3(near)}: which one goes is unclear."); continue; }
            Add(found.FirstOrDefault(), $"{parent}/{name} at {V3(near)}", why);
        }
        foreach ((GameObject go, string why) in p.off)
        {
            foreach (Light l in go.GetComponentsInChildren<Light>(true).Where(l => l.gameObject.activeInHierarchy))
            {
                p.lights.Add(l);
                run.Info($"  its light goes dark with it: {Shared.PathOf(l.transform)} ({l.type}, {F(l.intensity, "0.00")})");
            }
            run.Info($"Off: {Shared.PathOf(go.transform)}: {why}.");
        }
    }

    static void TodaysSeats(Shared.Run run, Plan p)
    {
        TableSeat[] all = Shared.InScene<TableSeat>(run, true);
        foreach (TableSeat s in all.Where(s => s.gameObject.activeInHierarchy).OrderBy(s => Shared.PathOf(s.transform), StringComparer.Ordinal))
        {
            p.today.Add(s);
            Collider own = s.GetComponent<Collider>();
            string furniture = own != null ? $"its solid {Shared.BoundsText(own.bounds)}" : "no solid of its own";
            run.Info($"{Shared.PathOf(s.transform)} ({furniture}): stand {V3(s.StandPoint.position)}, sit {V3(s.SeatPose.position)}, cup {V3(s.CupSpot.position)}, " +
                     $"{s.Style}, Snap To Seat {(s.SnapToSeat ? "on" : "off")}, drain {F(s.DrainMultiplier, "0.00")}, footprint {F(s.ClearanceRadius, "0.00")}" +
                     (p.UnderOff(s.transform) ? "; goes off" : "; STAYS ON"));
        }
        run.Check(p.today.Count == 21, $"Today's active seats: {p.today.Count} (the plan counted 21: sixteen at the four round tables, five in the lounge).");
        foreach (TableSeat s in p.today.Where(s => !p.UnderOff(s.transform)))
            run.Check(false, $"{Shared.PathOf(s.transform)} is a seat the plan doesn't know: it would stay on beside the new 24. Nothing was changed.");
        run.Info($"Inactive TableSeats already in the scene: {all.Length - p.today.Count}.");
    }

    // Anything resting on what goes off would float: a renderer no wider than 1.2 m whose bottom lies on the top of a
    // switched-off renderer (within 3 cm under it to 4 cm over it) and whose middle stands over it. Before: what goes off
    // with it (refusing anything with a script). After: nothing left.
    static void Floaters(Shared.Run run, Plan p, bool before)
    {
        // The tops are read before anything goes off (a switched-off renderer has no bounds worth reading).
        if (before)
        {
            p.tops.Clear();
            foreach ((GameObject go, string _) in p.off)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && !(r is ParticleSystemRenderer) && r.bounds.size.y > .02f) p.tops.Add((r.bounds, go.name));
        }
        List<(Bounds b, string what)> tops = p.tops;
        Transform redesign = run.Cafe.Find(Shared.RedesignGroup);
        var found = new List<(GameObject go, string on)>();
        foreach (Renderer r in run.Cafe.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || p.UnderOff(r.transform)) continue;
            if (redesign != null && r.transform.IsChildOf(redesign)) continue;
            if (run.Player != null && r.transform.IsChildOf(run.Player)) continue;
            Bounds b = r.bounds;
            if (b.size.x > 1.2f || b.size.z > 1.2f || b.min.y < .05f) continue;
            foreach ((Bounds t, string what) in tops)
            {
                if (b.min.y < t.max.y - .03f || b.min.y > t.max.y + .04f) continue;
                if (b.center.x <= t.min.x || b.center.x >= t.max.x || b.center.z <= t.min.z || b.center.z >= t.max.z) continue;
                if (!found.Any(f => f.go == r.gameObject)) found.Add((r.gameObject, what));
                break;
            }
        }
        if (before)
        {
            foreach ((GameObject go, string on) in found)
            {
                MonoBehaviour script = go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m != null);
                if (script != null) { run.Check(false, $"{Shared.PathOf(go.transform)} stands on {on}, which goes off, and carries {script.GetType().Name}: decide what it is before it goes."); continue; }
                p.off.Add((go, $"it stood on {on}, which goes off: it would float"));
                p.offRoots.Add(go.transform);
                p.floaters.Add(Shared.PathOf(go.transform));
                run.Info($"Off too: {Shared.PathOf(go.transform)} (it stood on {on}).");
            }
            if (found.Count == 0) run.Info("Nothing else stands on what goes off (checked: every renderer up to 1.2 m across resting on a top that goes).");
        }
        else run.Check(found.Count == 0, found.Count == 0
            ? "Nothing is left floating where the old furniture stood."
            : "Left floating: " + string.Join(", ", found.Select(f => $"{Shared.PathOf(f.go.transform)} (on {f.on})")));
    }

    static void PlanPieces(Shared.Run run, Plan p)
    {
        // The café's drawn things without a solid, read once (rugs flat on the floor and anything over 3 m across skipped).
        var loose = new List<(Renderer r, Bounds b)>();
        foreach (Renderer r in run.Cafe.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r is ParticleSystemRenderer || p.UnderOff(r.transform) || IsPlayer(run, r.transform)) continue;
            Bounds b = r.bounds;
            if (b.max.y < .03f || b.size.x > 3f || b.size.z > 3f) continue;
            loose.Add((r, b));
        }
        foreach (Piece x in Pieces)
        {
            run.Info($"{x.name}: origin {V3(x.at)}, yaw {F(x.yaw, "0")}, front {F(x.Front, "0")}; its solid {Shared.BoundsText(x.SolidBox)}.");

            // Footprint free: nothing solid that stays where its box goes (its box a centimetre smaller all round, its
            // bottom 3 cm up: the floor and the wall it backs onto only touch it).
            Vector3 centre = x.World(x.kit.boxCentre) + Vector3.up * .01f;
            Vector3 half = x.kit.boxSize * .5f - new Vector3(.01f, .02f, .01f);
            var blockers = (Physics.OverlapBox(centre, half, x.Turn, ~0, QueryTriggerInteraction.Ignore) ?? new Collider[0])
                .Where(c => c != null && !p.UnderOff(c.transform) && !IsPlayer(run, c.transform))
                .Select(c => $"{Shared.PathOf(c.transform)} ({Shared.BoundsText(c.bounds)})").Distinct().ToList();
            run.Check(blockers.Count == 0, blockers.Count == 0 ? $"{x.name}: its place is free of anything solid that stays."
                                                               : $"{x.name}: its place holds {string.Join("; ", blockers)}. Move or switch them off first.");

            // Things without a solid where it goes (rugs on the floor excepted): another step's to move, said here.
            Bounds mesh = Shrunk(x.MeshBox, .02f);
            foreach ((Renderer r, Bounds b) in loose)
            {
                if (!b.Intersects(mesh) || r.GetComponentInParent<Collider>() != null) continue;
                run.Info($"  {Shared.PathOf(r.transform)} (no solid, {Shared.BoundsText(b)}) reaches into {x.name}'s box: Room 1's to move if it shows.");
            }

            // The floor under the middle of its footprint, at 0 (the old furniture skipped: it goes).
            Vector3 middle = x.World(new Vector3(0f, .3f, (x.kit.min.z + x.kit.max.z) * .5f));
            RaycastHit? floor = Shared.Down(run, middle, 1f, c => p.UnderOff(c.transform));
            run.Check(floor.HasValue && Mathf.Abs(floor.Value.point.y) <= .01f,
                      $"{x.name}: the floor under it at {(floor.HasValue ? F(floor.Value.point.y) + " (" + floor.Value.collider.name + ")" : "nothing")} (0 expected).");

            // A wall piece's back against the wall: a ray from 5 cm in front of its back, straight back, meets a solid within 3 cm of it.
            if (x.kit.wall)
            {
                Vector3 back = x.Turn * Vector3.back;
                Vector3 from = x.World(new Vector3(0f, Mathf.Max(x.kit.min.y + .1f, .45f), .05f));
                RaycastHit? wall = Cast(run, from, back, .2f, c => p.UnderOff(c.transform));
                float gap = wall.HasValue ? wall.Value.distance - .05f : float.PositiveInfinity;
                run.Check(wall.HasValue && Mathf.Abs(gap) <= .03f,
                          $"{x.name}: its back against {(wall.HasValue ? wall.Value.collider.name : "nothing")} ({(wall.HasValue ? F(gap * 100f, "0.0") + " cm off it" : "no wall within 15 cm")}).");
            }
        }
    }

    static void PlanSeats(Shared.Run run, Plan p)
    {
        // ---- each seat: its sit point from the manifest, its facing proved, its cup on its top ----
        foreach (Seat s in Seats)
        {
            Vector3 pose = s.on.World(s.on.kit.pose.Value);       // the banquette's: at its 2-top's centre along the bay (local x 0)
            run.Check(Near(pose, s.pose, Mm), $"{s.name}: the sit point from the manifest, {V3(pose)}, is the plan's {V3(s.pose)}.");
            float toCup = Shared.Facing(s.Facing);
            float off = Mathf.Abs(Mathf.DeltaAngle(toCup, s.on.Front));
            run.Check(off <= 2f, $"{s.name}: the sitter faces {F(toCup, "0")} (towards the cup), the piece's front {F(s.on.Front, "0")} ({F(off, "0.0")} degrees apart).");
            if (s.on.kit == LowStool)
            {
                // The seating lead's table turns the low stools 0 (west row) and 180 (east row). Unity turns the front, local +Z,
                // by the yaw: 0 keeps it north, along the table, so the sitter would face down the table, not across it.
                float lead = s.name.Contains("west") ? 0f : 180f;
                float leadOff = Mathf.Abs(Mathf.DeltaAngle(toCup, Shared.Facing(Quaternion.Euler(0f, lead, 0f))));
                if (s.name.EndsWith("1)", StringComparison.Ordinal))
                    run.Info($"  the seating lead's yaw {F(lead, "0")} for this row would face {F(Shared.Facing(Quaternion.Euler(0f, lead, 0f)), "0")}, {F(leadOff, "0")} degrees off the table; " +
                             $"the brief's {F(s.on.yaw, "0")} faces {F(s.on.Front, "0")}: the brief is right.");
            }
            if (s.on.group == GRepair)
            {
                // The ledge, measured: the first solid under the cup spot (the cups stand on Bar 1's counter).
                RaycastHit? ledge = Shared.Down(run, new Vector3(s.cup.x, 1.6f, LedgeZ), .8f, c => p.UnderOff(c.transform));
                bool ok = ledge.HasValue && ledge.Value.point.y >= LedgeLow && ledge.Value.point.y <= LedgeHigh;
                run.Check(ok, $"{s.name}: the counter's ledge under the cup at {(ledge.HasValue ? F(ledge.Value.point.y) + " (" + Shared.PathOf(ledge.Value.collider.transform) + ")" : "nothing")} " +
                              $"(Bar 1's top: 1.095).");
                if (ok) s.cup = new Vector3(s.cup.x, ledge.Value.point.y, s.cup.z);
            }
            else
            {
                Piece top = TableOf(s);
                bool over = top != null && top.OverTop(s.cup, .05f) && Mathf.Abs(s.cup.y - top.TopY) <= Mm;
                run.Check(over, $"{s.name}: the cup spot {V3(s.cup)} on {(top != null ? top.name : "no table")}'s top at {(top != null ? F(top.TopY) : "-")}, at least 5 cm in from its edges.");
            }
        }

        // ---- the stand points: on the floor, clear of every solid, claimable, not in the queue ----
        var loiter = Shared.InScene<WaitingSpot>(run).Where(w => !(w is TableSeat) && w.gameObject.activeInHierarchy).ToList();
        Transform queue = Named(run, "CounterQueue");
        List<Transform> slots = queue != null ? queue.Cast<Transform>().ToList() : new List<Transform>();
        run.Require(slots.Count > 0, "No CounterQueue with its places in the scene.");
        foreach (Seat s in Seats)
        {
            RaycastHit? floor = Shared.Down(run, s.stand + Vector3.up * .5f, 1f, c => p.UnderOff(c.transform));
            float nav = Pieces.Min(x => x.FlatDistance(s.stand));
            string nearest = Pieces.OrderBy(x => x.FlatDistance(s.stand)).First().name;
            float staying = float.PositiveInfinity; string what = "";
            foreach (Collider c in Physics.OverlapBox(new Vector3(s.stand.x, 1f, s.stand.z), new Vector3(NavMargin + .05f, .9f, NavMargin + .05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) ?? new Collider[0])
            {
                if (c == null || p.UnderOff(c.transform) || IsPlayer(run, c.transform) || c.bounds.max.y < .05f) continue;
                float d = FlatToBox(s.stand, c.bounds);
                if (d < staying) { staying = d; what = Shared.PathOf(c.transform); }
            }
            run.Check(floor.HasValue && Mathf.Abs(floor.Value.point.y) <= .02f && nav >= NavMargin && staying >= NavMargin,
                      $"{s.name}: the stand point {V3(s.stand)} on the floor ({(floor.HasValue ? F(floor.Value.point.y) : "none")}), {F(nav, "0.00")} m from the nearest new solid ({nearest}), " +
                      $"{(float.IsInfinity(staying) ? "nothing that stays within 0.45 m" : F(staying, "0.00") + " m from " + what)} (at least {F(NavMargin, "0.00")}: {s.why}).");
            float toLoiter = loiter.Count > 0 ? loiter.Min(w => FlatDistance(s.stand, w.StandPoint.position)) : float.PositiveInfinity;
            float toQueue = slots.Min(q => FlatDistance(s.stand, q.position));
            run.Check(toLoiter >= SeatToWaiting && toQueue >= SeatToQueue,
                      $"{s.name}: {F(toLoiter, "0.00")} m from the nearest waiting spot (at least {F(SeatToWaiting, "0.00")}), {F(toQueue, "0.00")} m from the queue (at least {F(SeatToQueue, "0.00")}).");
        }
        (float d, Seat a, Seat b) closest = (float.PositiveInfinity, null, null);
        for (int i = 0; i < Seats.Length; i++)
            for (int j = i + 1; j < Seats.Length; j++)
            {
                float d = FlatDistance(Seats[i].stand, Seats[j].stand);
                if (d < closest.d) closest = (d, Seats[i], Seats[j]);
                if (d < SeatClaim) run.Check(false, $"{Seats[i].name} and {Seats[j].name}: stand points {F(d, "0.00")} m apart; two claims need {F(SeatClaim, "0.00")}.");
            }
        run.Info($"Claims: the closest two stand points are {F(closest.d, "0.000")} m apart ({closest.a.name}, {closest.b.name}); two seats may both be claimed from {F(SeatClaim, "0.00")}.");

        // ---- the pieces clear of everyone else's places (CafeFurnishing's keep-clear rule), and the door lane ----
        var keep = new List<(Vector3 at, float r, string what, Seat seat)>();
        foreach (Seat s in Seats) keep.Add((s.stand, KeepSeat, s.name + "'s stand point", s));
        foreach (WaitingSpot w in Shared.InScene<WaitingSpot>(run).Where(w => !(w is TableSeat))) keep.Add((w.StandPoint.position, KeepWaiting, w.name, null));
        foreach (Transform q in slots) keep.Add((q.position, KeepQueue, "queue " + q.name, null));
        foreach (StationInteractable st in Shared.InScene<StationInteractable>(run, false)) if (st.StandPoint != null) keep.Add((st.StandPoint.position, KeepStation, st.name + "'s stand point", null));
        p.spawn = NamedInScene(run, "SpawnPoint");
        if (p.spawn != null) keep.Add((p.spawn.position, KeepSpawn, "the customers' spawn", null));
        int clashes = 0;
        foreach (Piece x in Pieces)
            foreach ((Vector3 at, float r, string what, Seat seat) in keep)
            {
                if (seat != null && Own(seat, x)) continue;
                float d = x.FlatDistance(at);
                if (d < r) { clashes++; run.Check(false, $"{x.name} is {F(d, "0.00")} m from {what} (needs {F(r, "0.00")})."); }
            }
        if (clashes == 0) run.Info($"Every piece keeps clear of the other seats' stand points ({F(KeepSeat, "0.00")}), the waiting spots ({F(KeepWaiting, "0.00")}), the queue ({F(KeepQueue, "0.00")}), " +
                                   $"the stations' stand points ({F(KeepStation, "0.00")}) and the spawn ({F(KeepSpawn, "0.00")}).");
        foreach (Piece x in Pieces)
        {
            Bounds b = x.SolidBox;
            if (b.max.x > LaneX0 && b.min.x < LaneX1 && b.max.z > LaneZ0 && b.min.z < LaneZ1) run.Check(false, $"{x.name} stands in the door-to-counter lane (x -1..1, z -1.2..12.5).");
        }

        // ---- the plan, seat by seat ----
        foreach (Seat s in Seats)
            run.Info($"{s.name}: {(s.bench ? "Bench" : "Chair")}, Snap To Seat {(s.snap ? "on" : "OFF (a bar stool: the sit clips can't reach it)")}; sit {V3(s.pose)} facing {F(Shared.Facing(s.Facing), "0")}; " +
                     $"cup {V3(s.cup)}; stand {V3(s.stand)} ({s.why}).");
    }

    // ================================================================== the change

    static void Change(Shared.Run run)
    {
        Plan p = plan ?? throw new InvalidOperationException("The survey didn't run.");

        // 1. Off, never deleted.
        foreach ((GameObject go, string why) in p.off) Shared.SwitchOff(run, go, why);

        // 2. The step's group: Not Walkable for the routes, as the tables' and the lounge's groups.
        Transform group = Shared.StepGroup(run, GroupName);
        NavMeshModifier modifier = group.GetComponent<NavMeshModifier>();      // (not ??: a missing component is Unity's fake null in the Editor)
        if (modifier == null) modifier = Shared.AddComponent<NavMeshModifier>(run, group.gameObject);
        Undo.RecordObject(modifier, run.Title);
        modifier.overrideArea = true;
        modifier.area = 1;                 // Not Walkable
        modifier.ignoreFromBuild = false;
        modifier.applyToChildren = true;

        // 3. The pieces, each with the manifest's solid.
        foreach (Piece x in Pieces)
        {
            Transform table = Shared.Group(run, group, x.group);
            x.placed = Shared.Place(run, x.kit.piece, table, x.at, x.yaw, x.name);
            x.box = Shared.AddComponent<BoxCollider>(run, x.placed);
            Undo.RecordObject(x.box, run.Title);
            x.box.center = x.kit.boxCentre;
            x.box.size = x.kit.boxSize;
            x.box.isTrigger = false;
        }

        // 4. The seats.
        foreach (Seat s in Seats) MakeSeat(run, group, s);
        Physics.SyncTransforms();
        Shared.Note(run, $"24 seats laid in {Shared.RedesignGroup}/{GroupName}; the six bar-stool seats have Snap To Seat off (customers wait standing beside them).");

        // 5. The checks.
        After(run, p, group);
    }

    static void MakeSeat(Shared.Run run, Transform group, Seat s)
    {
        Transform table = Shared.Group(run, group, s.group);
        var go = new GameObject(s.name);
        go.transform.SetParent(table, false);
        go.transform.SetPositionAndRotation(new Vector3(s.pose.x, 0f, s.pose.z), s.Facing);   // on the floor under the sit point, as today's
        Transform pose = Anchor(go.transform, "SeatPose", s.pose, s.Facing);
        Transform cup = Anchor(go.transform, "CupSpot", s.cup, s.Facing);
        Transform stand = Anchor(go.transform, "StandPoint", s.stand, s.StandFacing);           // facing the table, as today's
        TableSeat seat = go.AddComponent<TableSeat>();
        var so = new SerializedObject(seat);
        so.FindProperty("kind").intValue = (int)WaitingSpot.SpotKind.Seat;
        so.FindProperty("standPoint").objectReferenceValue = stand;
        so.FindProperty("drainMultiplier").floatValue = SeatDrain;
        so.FindProperty("cupSpot").objectReferenceValue = cup;
        so.FindProperty("snapToSeat").boolValue = s.snap;
        so.FindProperty("seatPose").objectReferenceValue = pose;
        so.FindProperty("sitStyle").intValue = (int)(s.bench ? TableSeat.SitStyle.Bench : TableSeat.SitStyle.Chair);
        so.FindProperty("faceTowards").objectReferenceValue = null;                               // towards the cup spot
        so.FindProperty("clearanceRadius").floatValue = s.bench || !s.snap ? BenchFootprint : 0f;   // chairs that sit: 0, i.e. 0.3 (WaitingArea)
        so.ApplyModifiedPropertiesWithoutUndo();                                                    // a new object: its creation is the undo
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

    static void After(Shared.Run run, Plan p, Transform group)
    {
        // ---- each piece where it should be, standing, its solid where its solid is ----
        foreach (Piece x in Pieces)
        {
            Bounds drawn = Shared.RendererBounds(x.placed.transform);
            Bounds expected = x.MeshBox;
            run.Check(Near(drawn.min, expected.min, .01f) && Near(drawn.max, expected.max, .01f),
                      $"{x.name} is drawn at {Shared.BoundsText(drawn)} (expected {Shared.BoundsText(expected)}).");
            Bounds solid = x.box.bounds;
            run.Check(x.box.enabled && !x.box.isTrigger && Near(solid.min, x.SolidBox.min, .005f) && Near(solid.max, x.SolidBox.max, .005f),
                      $"{x.name}'s solid at {Shared.BoundsText(solid)} (its box from the manifest: {Shared.BoundsText(x.SolidBox)}).");
            if (!x.kit.wall || x.kit.min.y <= 0f)
                run.Check(Mathf.Abs(drawn.min.y) <= .005f, $"{x.name} stands on the floor: its lowest point at {F(drawn.min.y)}.");
            if (x.kit.wall)
            {
                RaycastHit? wall = Cast(run, x.World(new Vector3(0f, Mathf.Max(x.kit.min.y + .1f, .45f), .05f)), x.Turn * Vector3.back, .2f, c => c.transform.IsChildOf(group));
                float gap = wall.HasValue ? wall.Value.distance - .05f : float.PositiveInfinity;
                run.Check(wall.HasValue && Mathf.Abs(gap) <= .03f, $"{x.name}: its back against {(wall.HasValue ? wall.Value.collider.name : "nothing")} ({(wall.HasValue ? F(gap * 100f, "0.0") + " cm" : "-")}).");
            }
        }

        // ---- every seat: the TableSeat, its sit point on its seat's surface, its cup on its top, its facing ----
        foreach (Seat s in Seats)
        {
            TableSeat t = s.seat;
            bool wired = t != null && t.Kind == WaitingSpot.SpotKind.Seat && Mathf.Approximately(t.DrainMultiplier, SeatDrain) && t.SnapToSeat == s.snap
                         && t.Style == (s.bench ? TableSeat.SitStyle.Bench : TableSeat.SitStyle.Chair) && Mathf.Approximately(t.ClearanceRadius, s.bench || !s.snap ? BenchFootprint : 0f)
                         && Near(t.StandPoint.position, s.stand, Mm) && Near(t.SeatPose.position, s.pose, Mm) && Near(t.CupSpot.position, s.cup, Mm);
            run.Check(wired, $"{s.name}: a Seat, drain {F(SeatDrain, "0.0")}, {(s.bench ? "Bench" : "Chair")}, Snap To Seat {(s.snap ? "on" : "off")}, its three points where planned.");
            float? surface = MeshTopUnder(s.on.placed, s.pose + Vector3.up * .3f, .6f);
            run.Check(surface.HasValue && s.pose.y - surface.Value >= -.005f && s.pose.y - surface.Value <= .03f,
                      $"{s.name}: the sit point {F(s.pose.y)} over its seat's surface at {(surface.HasValue ? F(surface.Value) : "nothing")} (a ray down the mesh).");
            Piece table = TableOf(s);
            RaycastHit? under = Shared.Down(run, s.cup + Vector3.up * .3f, .6f);
            bool onTop = under.HasValue && Mathf.Abs(under.Value.point.y - s.cup.y) <= .005f
                         && (table != null ? under.Value.collider.transform.IsChildOf(table.placed.transform) : !under.Value.collider.transform.IsChildOf(group));
            float? drawnTop = table != null ? MeshTopUnder(table.placed, s.cup + Vector3.up * .3f, .6f) : null;
            run.Check(onTop && (table == null || drawnTop.HasValue && Mathf.Abs(drawnTop.Value - s.cup.y) <= .01f),
                      $"{s.name}: the cup spot on {(under.HasValue ? under.Value.collider.name : "nothing")} (solid at {(under.HasValue ? F(under.Value.point.y) : "-")}" +
                      $"{(table != null ? ", drawn top at " + (drawnTop.HasValue ? F(drawnTop.Value) : "-") : "")}; the cup at {F(s.cup.y)}).");
            float face = Mathf.Abs(Mathf.DeltaAngle(Shared.Facing(Quaternion.LookRotation(Flat(t.FacingPoint - t.SeatPose.position), Vector3.up)), s.on.Front));
            run.Check(face <= 2f && NpcBeats.TableInFront(t), $"{s.name}: faces its table ({F(face, "0.0")} degrees off the piece's front; NpcBeats counts a table in front).");
        }

        // ---- exactly the plan's seats on, today's off ----
        TableSeat[] live = Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(t => t.gameObject.scene == run.Scene).ToArray();
        run.Check(live.Length == 24 && live.All(t => t.transform.IsChildOf(group)), $"Active TableSeats in the scene: {live.Length}, all of them this step's (24 expected).");
        run.Check(p.today.All(t => !t.gameObject.activeInHierarchy), $"Today's {p.today.Count} seats are all off.");
        run.Check(p.off.All(o => !o.go.activeSelf), $"Everything on the off list is off ({p.off.Count}).");
        run.Check(p.lights.All(l => !l.gameObject.activeInHierarchy), $"Their lights are dark ({p.lights.Count}).");
        Floaters(run, p, false);

        // ---- the claims and the keep-clear rule, live ----
        float closest = float.PositiveInfinity;
        for (int i = 0; i < live.Length; i++)
            for (int j = i + 1; j < live.Length; j++)
                closest = Mathf.Min(closest, FlatDistance(live[i].StandPoint.position, live[j].StandPoint.position));
        run.Check(closest >= SeatClaim, $"Any two seats may be claimed at once: the closest stand points are {F(closest, "0.000")} m apart (at least {F(SeatClaim, "0.00")}).");
        foreach (KeyValuePair<WaitingSpot, bool> w in p.loiterOn)
            run.Check(w.Key.gameObject.activeInHierarchy == w.Value, $"{w.Key.name} is {(w.Value ? "on" : "off")}, as before.");

        // ---- nothing else moved ----
        var moved = p.anchors.Where(a => a.Key == null || Vector3.Distance(a.Key.position, a.Value) > Mm).Select(a => a.Key != null ? Shared.PathOf(a.Key) : "(gone)").ToList();
        run.Check(moved.Count == 0, moved.Count == 0 ? $"No gameplay anchor moved ({p.anchors.Count}, 1 mm)." : "Moved: " + string.Join(", ", moved));

        // ---- the routes, re-baked here so the seats' stand points can be walked to before anything is kept ----
        AcesCafeLayoutSetup.BakeRoutes();
        Vector3 origin = p.spawn != null ? p.spawn.position : new Vector3(0f, .06f, -1.2f);
        var path = new NavMeshPath();
        foreach (Seat s in Seats)
        {
            bool on = NavMesh.SamplePosition(s.stand, out NavMeshHit hit, .22f, NavMesh.AllAreas);
            bool reached = on && NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            run.Check(on && reached, $"{s.name}: its stand point on the NavMesh ({(on ? F(FlatDistance(hit.position, s.stand) * 100f, "0.0") + " cm off it, at y " + F(hit.position.y) : "not within 0.22 m")}) " +
                                     $"and walked to from the spawn ({(reached ? "a complete path" : "NO PATH")}).");
        }
        Plateaus(run, origin);

        // ---- the guards: 24 seats, every path; 31 of 31 places served ----
        string layout = AcesCafeLayoutSetup.ValidateLayout();
        // 24 seats, and every path complete; a failure that names none of the plan's seats is another step's (the step's own
        // "not worse" comparison still refuses to lose a PASS).
        run.Check(layout.Contains("24 seats") && (layout.Contains("PASS") || !layout.Contains("Seat (")), "The layout check: " + layout);
        int places = live.Length + p.loiterOn.Count(kv => kv.Key.gameObject.activeInHierarchy) + (Named(run, "CounterQueue")?.childCount ?? 0);
        run.Check(places == 31, $"The guard's places: {places} (24 seats, the 4 waiting spots that are on, the 3 queue places).");
        string circulation = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        run.Check(circulation.Contains($"{places}/{places}") && circulation.Contains("PASS"), "The occupied-circulation guard: " + circulation);
        PlaceByPlace(run, p);
    }

    // No walkable NavMesh between 0.2 and 0.9 m that people can walk up onto (the café life setup's test): the new tops and
    // cushions must be islands at most, never a step. Only what lies over the new pieces counts against this step; anything
    // elsewhere is reported (another step's to answer).
    static void Plateaus(Shared.Run run, Vector3 door)
    {
        int plateau = 0, elsewhere = 0, islands = 0;
        var route = new NavMeshPath();
        string where = "", other = "";
        for (float z = .5f; z <= 12.5f; z += .25f)
            for (float x = -7.2f; x <= 7.2f; x += .25f)
                if (NavMesh.SamplePosition(new Vector3(x, .5f, z), out NavMeshHit h, .6f, NavMesh.AllAreas) && h.position.y > .2f && h.position.y < .9f)
                {
                    bool connected = NavMesh.CalculatePath(door, h.position, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete;
                    if (!connected) { islands++; continue; }
                    if (Pieces.Any(piece => piece.FlatDistance(h.position) < .3f))
                    {
                        plateau++;
                        if (where.Length == 0) where = $" (first at {V3(h.position)})";
                    }
                    else
                    {
                        elsewhere++;
                        if (other.Length == 0) other = V3(h.position);
                    }
                }
        run.Check(plateau == 0, $"No table top or cushion of the plan became a step: {plateau} connected NavMesh samples between 0.2 and 0.9 m over the new pieces{where}; {islands} unreachable islands ignored.");
        if (elsewhere > 0) run.Info($"Elsewhere in the café, {elsewhere} connected NavMesh samples between 0.2 and 0.9 m (first at {other}): not this step's pieces (the café life setup will say).");
    }

    // The occupied-circulation guard place by place: AcesCafeLayoutSetup.ValidateOccupiedCirculationV2's own grid, envelopes,
    // capsule and reach (10 Oct), with how near each place the reachable floor comes and how many cells it has within reach
    // (the guard says only served or not). The banquette's sitters are served from one side only (research §7.6 risk 5):
    // this is the proof, and the map of the west zone shows from where.
    static void PlaceByPlace(Shared.Run run, Plan p)
    {
        CharacterController body = run.Player != null ? run.Player.GetComponent<CharacterController>() : null;
        Transform floorT = NamedInScene(run, "Floor");
        Transform queue = Named(run, "CounterQueue");
        if (body == null || floorT == null || queue == null) { run.Info("The place-by-place table was skipped (no Player controller, Floor or CounterQueue)."); return; }
        var targets = Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude).Where(s => s.gameObject.scene == run.Scene).Select(s => s.StandPoint)
            .Concat(queue.Cast<Transform>()).Where(t => t != null).Distinct().ToArray();
        float radius = PlayerMovement.RadiusFor(body);
        float height = Mathf.Max(body.height * Mathf.Abs(body.transform.lossyScale.y), radius * 2f);
        const float step = .20f, minX = -7.2f, minZ = -3.8f, standing = .65f, reach = 2.05f;
        const int cols = 73, rows = 109;
        float floorY = floorT.position.y, envelope = radius + standing + .03f;
        var clear = new bool[cols * rows];
        var solid = new bool[cols * rows];
        var visited = new bool[cols * rows];
        var hits = new Collider[64];
        Vector2 start = new Vector2(body.transform.position.x, body.transform.position.z);
        int seed = -1;
        float seedDistance = .75f * .75f;
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < cols; x++)
            {
                int i = z * cols + x;
                var point = new Vector2(minX + x * step, minZ + z * step);
                if (targets.Any(t => (point - new Vector2(t.position.x, t.position.z)).sqrMagnitude < envelope * envelope)) continue;
                var bottom = new Vector3(point.x, floorY + .06f + radius, point.y);
                var top = new Vector3(point.x, floorY + .06f + height - radius, point.y);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + .03f, hits, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = count == hits.Length;
                for (int h = 0; h < count && !blocked; h++) blocked = hits[h] != null && hits[h].GetComponentInParent<PlayerMovement>() == null;
                if (blocked) { solid[i] = true; continue; }
                clear[i] = true;
                float d = (point - start).sqrMagnitude;
                if (d < seedDistance) { seedDistance = d; seed = i; }
            }
        if (seed < 0) { run.Check(false, "The place-by-place table: Ace's start has no clear cell near it."); return; }
        var frontier = new Queue<int>();
        frontier.Enqueue(seed);
        visited[seed] = true;
        while (frontier.Count > 0)
        {
            int i = frontier.Dequeue(), x = i % cols, z = i / cols;
            foreach (int n in new[] { x > 0 ? i - 1 : -1, x < cols - 1 ? i + 1 : -1, z > 0 ? i - cols : -1, z < rows - 1 ? i + cols : -1 })
                if (n >= 0 && clear[n] && !visited[n]) { visited[n] = true; frontier.Enqueue(n); }
        }
        run.Line();
        run.Line($"The guard place by place (Ace's radius {F(radius, "0.00")}, the bodies' envelopes {F(envelope, "0.00")} m, reach {F(reach, "0.00")} m; nearest = the nearest reached cell):");
        foreach (Transform t in targets.OrderBy(t => t.parent != null ? t.parent.name : t.name, StringComparer.Ordinal))
        {
            float nearest = float.PositiveInfinity; int approach = 0;
            for (int i = 0; i < visited.Length; i++)
            {
                if (!visited[i]) continue;
                float d = Vector2.Distance(new Vector2(minX + (i % cols) * step, minZ + (i / cols) * step), new Vector2(t.position.x, t.position.z));
                nearest = Mathf.Min(nearest, d);
                if (d <= reach) approach++;
            }
            string name = (t.parent != null ? t.parent.name + "/" : "") + t.name;
            bool bench = t.parent != null && t.parent.name.Contains("on the banquette");
            run.Line($"  {(nearest <= reach ? "served" : "NOT SERVED")}  {name}: nearest {F(nearest, "0.00")} m, {approach} cells within reach{(bench ? "  <- a banquette sitter" : "")}");
            if (bench) run.Check(nearest <= reach, $"The banquette sitter {t.parent.name} is served: the reachable floor comes within {F(nearest, "0.00")} m ({approach} cells within {F(reach, "0.00")}).");
        }
        // The west zone, as the guard sees it (o = a place's stand point, . = reached, # = a solid, ' ' = a body's envelope, , = clear but cut off).
        run.Line("The west zone (x -7.2 to -0.8 left to right, z 11.6 at the top to 2.4 at the bottom):");
        var marks = new HashSet<int>(targets.Select(t => Mathf.RoundToInt((t.position.z - minZ) / step) * cols + Mathf.RoundToInt((t.position.x - minX) / step)));
        for (int z = Mathf.RoundToInt((11.6f - minZ) / step); z >= Mathf.RoundToInt((2.4f - minZ) / step); z--)
        {
            var line = new StringBuilder("   " + F(minZ + z * step, "00.0") + " ");
            for (int x = 0; x <= Mathf.RoundToInt((-.8f - minX) / step); x++)
            {
                int i = z * cols + x;
                line.Append(marks.Contains(i) ? 'o' : visited[i] ? '.' : solid[i] ? '#' : clear[i] ? ',' : ' ');
            }
            run.Line(line.ToString());
        }
    }

    // ================================================================== photos (before and after, from the same places)

    static void Photos(Shared.Run run)
    {
        // Each zone in first person: standing (eye 1.65), then from one of its seats (eye 0.755 over the seat: 1.20 on the
        // chairs, stools and banquette, 1.50 on the bar stools); the banquette from the door; the game's camera; two close-ups.
        Shared.Photo(run, "01-banquette-standing", new Vector3(-3.9f, 1.65f, 3.0f), new Vector3(-6.6f, .6f, 6.9f), 60f);
        Shared.Photo(run, "02-banquette-from-its-seat", new Vector3(-7.05f, SeatEye(.444f), 6.79f), new Vector3(-3.5f, 1.0f, 6.79f), 65f);
        Shared.Photo(run, "03-communal-standing", new Vector3(-.4f, 1.65f, 3.2f), new Vector3(-2.95f, .55f, 6.4f), 60f);
        Shared.Photo(run, "04-communal-from-a-stool", new Vector3(-3.85f, SeatEye(.4452f), 5.74f), new Vector3(0f, 1.0f, 5.74f), 65f);
        Shared.Photo(run, "05-bistro-standing", new Vector3(.6f, 1.65f, 3.0f), new Vector3(3.0f, .55f, 6.2f), 60f);
        Shared.Photo(run, "06-bistro-from-a-chair", new Vector3(3.67f, SeatEye(.443f), 4.6f), new Vector3(-1f, 1.0f, 5.6f), 65f);
        Shared.Photo(run, "07-window-counter-standing", new Vector3(4.2f, 1.65f, 4.4f), new Vector3(7.0f, .95f, 7.4f), 60f);
        Shared.Photo(run, "08-window-counter-from-a-stool", new Vector3(6.42f, SeatEye(.7449f), 7.2f), new Vector3(9f, 1.2f, 7.2f), 65f);
        Shared.Photo(run, "09-repair-bar-standing", new Vector3(-4.2f, 1.65f, 10.2f), new Vector3(-4.2f, .95f, 13.3f), 60f);
        Shared.Photo(run, "10-repair-bar-from-a-stool", new Vector3(-4.2f, SeatEye(.7449f), 12.78f), new Vector3(-4.2f, 1.05f, 14.2f), 65f);
        Shared.Photo(run, "11-banquette-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(-6.5f, .8f, 6.8f), 55f);
        Shared.GameCameraPhoto(run, "12-game-camera");
        Shared.Photo(run, "13-plan-from-above", new Vector3(0f, 17f, 6.6f), new Vector3(0f, 0f, 6.61f), 50f, true);
        Shared.Photo(run, "14-close-banquette-2-top", new Vector3(-4.9f, 1.25f, 5.7f), new Vector3(-6.7f, .5f, 6.79f), 50f);
        Shared.Photo(run, "15-close-communal-stools", new Vector3(-1.2f, 1.0f, 3.9f), new Vector3(-2.95f, .45f, 5.6f), 50f);
    }

    static float SeatEye(float seat) => seat + SeatEyeAbove;

    // ================================================================== small helpers

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static string V3(Vector3 v) => Shared.V(v);
    static string F(float f, string format = "0.000") => Shared.F(f, format);
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    static bool Near(Vector3 a, Vector3 b, float slack) => Mathf.Abs(a.x - b.x) <= slack && Mathf.Abs(a.y - b.y) <= slack && Mathf.Abs(a.z - b.z) <= slack;
    static Vector3 Vec(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : new Vector3(float.NaN, float.NaN, float.NaN);
    static bool IsPlayer(Shared.Run run, Transform t) => run.Player != null && t.IsChildOf(run.Player);

    static Bounds MinMax(Vector3 min, Vector3 max)
    {
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    static Bounds Shrunk(Bounds b, float by)
    {
        b.Expand(-2f * by);
        return b;
    }

    // A local box (min..max) turned and placed: its world box.
    static Bounds Turned(Vector3 at, Quaternion turn, Vector3 min, Vector3 max)
    {
        var b = new Bounds(at + turn * min, Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(at + turn * new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
        return b;
    }

    static Bounds Transformed(Bounds local, Matrix4x4 m)
    {
        var b = new Bounds(m.MultiplyPoint3x4(local.min), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z)));
        return b;
    }

    // Horizontal distance from p to a world box.
    static float FlatToBox(Vector3 p, Bounds b)
    {
        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
        float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The seat's own pieces for the keep-clear rule: its seat, its table, and for a bench sitter the whole banquette.
    static bool Own(Seat s, Piece x) =>
        x.name == s.piece || x == TableOf(s) || s.bench && (x.kit == Bay || x.kit == End);

    // The table or counter whose top holds the seat's cup (none for the repair bar: Bar 1's counter).
    static Piece TableOf(Seat s)
    {
        if (s.group == GRepair) return null;
        return Pieces.FirstOrDefault(x => x.group == s.group && (x.kit == Square || x.kit == Round || x.kit == Communal || x.kit == Counter));
    }

    // The first transform of that name under the café's root, or anywhere in the scene (as AcesCafeLayoutSetup.Find).
    static Transform Named(Shared.Run run, string name) => Shared.Named(run, name).FirstOrDefault() ?? NamedInScene(run, name);
    static Transform NamedInScene(Shared.Run run, string name) =>
        run.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

    // The first solid along a ray (triggers and Ace skipped, and whatever `skip` says).
    static RaycastHit? Cast(Shared.Run run, Vector3 from, Vector3 direction, float distance, Func<Collider, bool> skip)
    {
        foreach (RaycastHit h in (Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore) ?? new RaycastHit[0]).OrderBy(h => h.distance))
        {
            if (IsPlayer(run, h.collider.transform) || skip != null && skip(h.collider)) continue;
            return h;
        }
        return null;
    }

    // The highest drawn surface of a piece straight under `from`, down to `depth` below it: its meshes' triangles, read in
    // Edit Mode (a ray down the mesh, not the box: the box's top is the backrest's, the seat is lower).
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

    // What must not move: the stations and their stand points, every waiting spot (and seat) and its stand point, the
    // queue, the intake shelf and the bench's slots, the dispenser, the cup stack and the basin, the spawn points, the doors.
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
}
#endif
