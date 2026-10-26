using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// WHERE A PIECE OF FURNITURE MAY GO (24 Oct 2026; furniture-spec.md §4.2, §1.3 "Green or red, and why")
//
// One set of rules for the ghost (ArrangeMode), the layout (CafeLayout), the steps (Furniture 1-3) and the labs, so what the
// player may build is exactly what the café's own editor steps were allowed to build. Each rule is a step's rule, with the
// numbers that step uses (Designer F's model, /root/review23/mockups/src/arrange_placement.py, is the Python twin):
//
//   R1  on the customer floor: inside the room (CafeDaylight.CafeInside, x -7.4..7.4 from z 0.1), south of the counter (its
//       front at z 13.3, less 0.4), and out of three aprons in front of it: the queue's (x -2.6..2.6, from z 11.3), Ace's way
//       out from behind the counter (x 4.6..7.4, from z 11.3) and the repair bar's (x -5.9..-2.5, from z 11.6). A wall piece
//       stands with its back on the west or the east wall;
//   R2  nothing within 0.10 m of another solid or a wall (Lived in 2's FreeMargin); bays may touch bays, and a wall piece
//       its wall;
//   R3  out of the door-to-counter aisle (x -1..1, z -1.2..12.5: CafeFurnishing's lane) and the 1.2 m walks from the door to
//       each queue place (CafeDressing3Steps.QueueWalkHalf 0.6);
//   R4  its solids clear of people's places: 0.75 m from other seats' stand points, 0.80 from waiting spots, 0.90 from the
//       queue's places, 1.0 from the door (CafeFurnishing.BuildKeepClear);
//   R5  its own stand points: 0.40 m from any solid, 0.70 from another stand point, 1.05 from a waiting spot, 0.90 from a
//       queue place, never in the aisle (Seats 1's NavMargin, SeatClaim, SeatToWaiting, SeatToQueue);
//   R6a every seat, waiting spot, queue place and station still reached from the door by a 0.35 m walker (the routes'
//       agent): AcesCafeLayoutSetup.ValidateLayout's question, on a 0.10 m grid (29,400 cells);
//   R6b with every one of them taken (0.65 m bodies), Ace still comes within 2.05 m of each: ValidateOccupiedCirculationV2's
//       own grid (0.20 m, 7,957 cells), capsule (+0.03) and reach;
//   R7  the money is in the till.
// The first rule a candidate breaks gives its reason, in at most seven words, for the ghost's plate.
//
// HOW IT RUNS. The floor map is built once when Arrange opens (BuildMap): every solid that isn't a piece or a dust sheet
// (walls, counters, the bins, P2's shelves behind the counter...), the people's places, the door and Ace's start. Physics is
// asked once there (the two grids' fixed cells, the way the guards ask it), then never per ghost: a candidate is its boxes and
// stand points at a pose, and R1-R5 are box and point distances; R6 rasterises every piece's boxes onto the two grids and
// floods them. A few milliseconds, only when the ghost moves to another cell or turns. The routes themselves are carved by
// each piece at run time (NavMeshObstacle), so in Edit Mode, where nothing carves, the steps and the labs ask these rules.
// MapFromData builds the same map from plain data (no scene): the Edit-mode check (Fixit Fidget > Checks > Furniture rules).
// ---------------------------------------------------------------------------
public static class FurnitureRules
{
    public enum Verdict
    {
        Fits, OffTheFloor, QueueApron, AceWay, RepairBar, TooClose, Aisle, BlocksAChair,
        NearWaiting, NearQueue, NearDoor, NoRoomToSit, NearSeat, BlocksAWay, AceCantPass, NotEnoughMoney
    }

    /// <summary>A verdict, its words for the plate, and what's in the way (to light up). CheckWholeLayout also counts.</summary>
    public struct Result
    {
        public Verdict verdict;
        public string words;
        public Bounds inTheWay;
        /// <summary>R6a: places reached from the door, of how many (CheckWholeLayout and every full check).</summary>
        public int reached, places;
        /// <summary>R6b: places Ace comes within reach of with every one taken, of how many.</summary>
        public int served, servedOf;
        public bool Fits => verdict == Verdict.Fits;
        public override string ToString() => Fits ? "fits" : $"{verdict}: \"{words}\"";
    }

    /// <summary>The ghost's cell, metres: the pivot jumps from one to the next, square to the room (x from the door's middle,
    /// z from the shop front).</summary>
    public const float Grid = .25f;

    // ================================================================== the numbers (with where they come from)

    /// <summary>The room (CafeDaylight.CafeInside) and the counter's front (Seats 1, Bar 1).</summary>
    public const float RoomX0 = -7.4f, RoomX1 = 7.4f, RoomZ0 = .1f, CounterFront = 13.3f;
    /// <summary>The side walls' faces: where a wall piece's back stands (the banquette at x -7.40, the window counter at +7.40).</summary>
    public const float WestWall = -7.40f, EastWall = 7.40f;
    /// <summary>A wall piece turned to face the room: +Z (its front) east from the west wall, west from the east wall.</summary>
    public const float WestWallYaw = 90f, EastWallYaw = 270f;
    const float Eps = .001f;

    struct Apron { public float x0, x1, z0; public Verdict verdict; public string words; }
    static readonly Apron[] Aprons =
    {
        new Apron { x0 = -2.6f, x1 = 2.6f, z0 = 11.3f, verdict = Verdict.QueueApron, words = "Keep the queue clear" },
        new Apron { x0 = 4.6f, x1 = 7.4f, z0 = 11.3f, verdict = Verdict.AceWay, words = "Keep Ace's way from the counter clear" },
        new Apron { x0 = -5.9f, x1 = -2.5f, z0 = 11.6f, verdict = Verdict.RepairBar, words = "Keep the repair bar clear" },
    };

    /// <summary>R2: the gap kept round every solid (Lived in 2's FreeMargin).</summary>
    public const float FreeMargin = .10f;
    /// <summary>R3: the door-to-counter aisle (CafeFurnishing.CheckFloorClearance) and the half-width of the walks to the queue.</summary>
    public const float LaneX0 = -1f, LaneX1 = 1f, LaneZ0 = -1.2f, LaneZ1 = 12.5f, QueueWalkHalf = .6f;
    /// <summary>R4 (CafeFurnishing.BuildKeepClear).</summary>
    public const float KeepSeat = .75f, KeepWaiting = .80f, KeepQueue = .90f, KeepDoor = 1f;
    /// <summary>R5 (Seats 1).</summary>
    public const float NavMargin = .40f, SeatClaim = .70f, SeatToWaiting = 1.05f, SeatToQueue = .90f;
    /// <summary>R6a: the routes' agent, the NavMesh's sample radius (ValidateLayout's 0.22).</summary>
    public const float Walker = .35f, SampleReach = .22f;
    /// <summary>R6b: the guard's numbers (AcesCafeLayoutSetup.ValidateOccupiedCirculationV2).</summary>
    public const float BodyEnvelope = .65f, GuardPad = .03f, Reach = 2.05f;

    // The two grids: the routes' (F's model, 147 x 200) and the guard's own (73 x 109).
    public const float RouteStep = .10f, RouteMinX = -7.3f, RouteMinZ = -2.0f;
    public const int RouteCols = 147, RouteRows = 200;
    public const float GuardStep = .20f, GuardMinX = -7.2f, GuardMinZ = -3.8f;
    public const int GuardCols = 73, GuardRows = 109;

    /// <summary>A bay's own boards: a bay may touch another bay (they join into one run: BanquetteRun).</summary>
    public const string BayPart = "bay";

    // ================================================================== the data core

    /// <summary>A candidate or a placed piece: its solids (world boxes), its seats' stand points, what each solid is.</summary>
    public struct Shape
    {
        public string name;
        public List<Bounds> solids;
        public List<Vector3> stands;
        public bool againstAWall;
        /// <summary>What each solid is (parallel to solids; BayPart for a bay's boards). May be null.</summary>
        public List<string> parts;

        public bool PartIsBay(int i) => parts != null && i < parts.Count && parts[i] == BayPart;
    }

    /// <summary>The café's floor as the rules see it: built once per Arrange (BuildMap) or from data (MapFromData).</summary>
    public sealed class FloorMap
    {
        public readonly List<Bounds> fixedSolids = new List<Bounds>();
        public readonly List<string> fixedNames = new List<string>();
        public readonly List<bool> fixedIsWall = new List<bool>();
        public readonly List<Vector3> waiting = new List<Vector3>();
        public readonly List<Vector3> queue = new List<Vector3>();
        public readonly List<Vector3> stations = new List<Vector3>();
        /// <summary>Seats that aren't on any piece (the repair bar's three): people's places like any seat's.</summary>
        public readonly List<Vector3> fixedSeatStands = new List<Vector3>();
        public Vector3 door, aceStart;
        public float aceRadius = .35f, aceHeight = 2f, floorY;
        /// <summary>Cells a fixed solid blocks: the routes' (a 0.35 m walker) and the guard's (Ace's capsule + 0.03).</summary>
        public bool[] routeFixed = new bool[RouteCols * RouteRows];
        public bool[] guardFixed = new bool[GuardCols * GuardRows];
        /// <summary>
        /// The boxes on the dust sheets still lying (a new game's half-finished café), each with its sheet's floor: never in the
        /// ghost's way (R1-R5 don't see them: a piece put on a sheet folds it, boxes and all), but solid for R6 while their sheet
        /// lies, unless the candidate itself would fold it (FoldsSheet). Their sheet is null from data (always lying).
        /// </summary>
        public readonly List<Bounds> sheetBoxes = new List<Bounds>();
        public readonly List<Rect> sheetAreas = new List<Rect>();
        public readonly List<DustSheet> sheetOf = new List<DustSheet>();
        /// <summary>How it was built ("physics" or "data") and how long it took, for the reports.</summary>
        public string how = "";
        public float milliseconds;

        // Working arrays, kept so a check allocates nothing after the first.
        internal readonly bool[] routeBlocked = new bool[RouteCols * RouteRows];
        internal readonly bool[] routeSeen = new bool[RouteCols * RouteRows];
        internal readonly bool[] guardClear = new bool[GuardCols * GuardRows];
        internal readonly bool[] guardSeen = new bool[GuardCols * GuardRows];
        internal readonly Queue<int> frontier = new Queue<int>(4096);
        internal readonly List<Vector3> targets = new List<Vector3>(64);
        internal readonly List<Vector3> served = new List<Vector3>(64);

        public int FixedCount => fixedSolids.Count;
    }

    /// <summary>
    /// The map from plain data, with no scene (the Edit-mode check): the fixed solids, the people's places, the door and Ace's
    /// start. Both grids' fixed cells come from the solids' boxes (solids lower than a step are walked over).
    /// </summary>
    public static FloorMap MapFromData(IReadOnlyList<Bounds> fixedSolids, IReadOnlyList<Vector3> waitingSpots,
                                       IReadOnlyList<Vector3> queuePlaces, IReadOnlyList<Vector3> stationStands,
                                       Vector3 door, Vector3 aceStart,
                                       IReadOnlyList<Vector3> fixedSeatStands = null, IReadOnlyList<string> fixedNames = null,
                                       IReadOnlyList<Bounds> sheetBoxes = null, IReadOnlyList<Rect> sheetAreas = null)
    {
        float started = Time.realtimeSinceStartup;
        var map = new FloorMap { door = door, aceStart = aceStart, how = "data" };
        if (sheetBoxes != null && sheetAreas != null)
            for (int i = 0; i < sheetBoxes.Count && i < sheetAreas.Count; i++)
            {
                map.sheetBoxes.Add(sheetBoxes[i]);
                map.sheetAreas.Add(sheetAreas[i]);
                map.sheetOf.Add(null);
            }
        if (fixedSolids != null)
            for (int i = 0; i < fixedSolids.Count; i++)
                AddFixed(map, fixedSolids[i], fixedNames != null && i < fixedNames.Count ? fixedNames[i] : "");
        if (waitingSpots != null) map.waiting.AddRange(waitingSpots);
        if (queuePlaces != null) map.queue.AddRange(queuePlaces);
        if (stationStands != null) map.stations.AddRange(stationStands);
        if (fixedSeatStands != null) map.fixedSeatStands.AddRange(fixedSeatStands);
        for (int i = 0; i < map.fixedSolids.Count; i++)
        {
            Bounds b = map.fixedSolids[i];
            if (BlocksWalker(b)) Rasterise(map.routeFixed, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, b, Walker);
            if (BlocksAce(map, b)) Rasterise(map.guardFixed, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, b, map.aceRadius + GuardPad);
        }
        map.milliseconds = (Time.realtimeSinceStartup - started) * 1000f;
        return map;
    }

    /// <summary>
    /// The map from the open scene (Arrange's, the steps', the labs'): every collider over the room that isn't a piece, a
    /// dust sheet, a person, Ace or a cup; the people's places; the door (SpawnPoint) and Ace's start (CafeLayout keeps it from
    /// the scene's load). The two grids' fixed cells are asked of physics once, as the guards ask it.
    /// </summary>
    public static FloorMap BuildMap() => BuildMap(null, null);

    /// <summary>
    /// The same, with more that isn't fixed: colliders and seats that will be pieces but aren't yet (Furniture 0's and 1's
    /// survey asks the rules about the café's sets before they become pieces).
    /// </summary>
    public static FloorMap BuildMap(ICollection<Collider> alsoNotFixed, ICollection<TableSeat> alsoNotFixedSeats)
    {
        float started = Time.realtimeSinceStartup;
        Physics.SyncTransforms();
        var map = new FloorMap { how = "physics" };

        // What isn't fixed: the pieces (on or off), the dust sheets and their boxes, people, Ace, the cups.
        var ignored = new HashSet<Collider>();
        if (alsoNotFixed != null) foreach (Collider c in alsoNotFixed) if (c != null) ignored.Add(c);
        foreach (PlacedPiece p in Object.FindObjectsByType<PlacedPiece>(FindObjectsInactive.Include))
            foreach (Collider c in p.GetComponentsInChildren<Collider>(true)) ignored.Add(c);
        foreach (DustSheet s in Object.FindObjectsByType<DustSheet>(FindObjectsInactive.Include))
            foreach (Collider c in s.GetComponentsInChildren<Collider>(true))
            {
                ignored.Add(c);
                if (!s.Lying || !c.enabled || c.isTrigger) continue;
                map.sheetBoxes.Add(c.bounds);
                map.sheetAreas.Add(s.Area);
                map.sheetOf.Add(s);
            }
        foreach (NavMeshAgent a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude))
            foreach (Collider c in a.GetComponentsInChildren<Collider>(true)) ignored.Add(c);
        foreach (DirtyCup cup in Object.FindObjectsByType<DirtyCup>(FindObjectsInactive.Exclude))
            foreach (Collider c in cup.GetComponentsInChildren<Collider>(true)) ignored.Add(c);
        PlayerMovement ace = Object.FindAnyObjectByType<PlayerMovement>();
        if (ace != null)
        {
            foreach (Collider c in ace.GetComponentsInChildren<Collider>(true)) ignored.Add(c);
            CharacterController body = ace.GetComponent<CharacterController>();
            if (body != null)
            {
                map.aceRadius = PlayerMovement.RadiusFor(body);
                map.aceHeight = Mathf.Max(body.height * Mathf.Abs(body.transform.lossyScale.y), map.aceRadius * 2f);
            }
        }
        GameObject floor = GameObject.Find("Floor");
        map.floorY = floor != null ? floor.transform.position.y : 0f;

        // The fixed solids over the room, for R2 and the plates' names.
        foreach (Collider c in Physics.OverlapBox(new Vector3(0f, 1f, 7f), new Vector3(8f, 1f, 11f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c == null || ignored.Contains(c) || !c.enabled) continue;
            if (c.GetComponentInParent<CustomerBrain>() != null || c.GetComponentInParent<PatronBrain>() != null) { ignored.Add(c); continue; }
            Bounds b = c.bounds;
            if (b.max.y <= map.floorY + .06f) continue;   // the floor itself, rugs, mats: walked over
            AddFixed(map, b, FriendlyName(c.transform));
        }

        // The people's places.
        foreach (WaitingSpot w in Object.FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
        {
            if (w == null || !w.isActiveAndEnabled) continue;
            if (w is TableSeat seat)
            {
                bool piece = seat.GetComponentInParent<PlacedPiece>() != null || (alsoNotFixedSeats != null && alsoNotFixedSeats.Contains(seat));
                if (!piece) map.fixedSeatStands.Add(seat.StandPoint.position);
            }
            else map.waiting.Add(w.StandPoint.position);
        }
        CounterQueue queue = Object.FindAnyObjectByType<CounterQueue>();
        if (queue != null)
            foreach (Transform slot in queue.transform) map.queue.Add(slot.position);
        foreach (StationInteractable st in Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude))
            if (st != null && st.StandPoint != null) map.stations.Add(st.StandPoint.position);
        GameObject spawn = GameObject.Find("SpawnPoint");
        map.door = spawn != null ? spawn.transform.position : new Vector3(0f, .06f, -1.2f);
        map.aceStart = CafeLayout.Instance != null ? CafeLayout.Instance.AceStart
                     : ace != null ? ace.transform.position : new Vector3(0f, 1f, 15.15f);

        // The grids' fixed cells, asked of physics once (as the guards ask it).
        var hits = new Collider[24];
        for (int z = 0; z < RouteRows; z++)
            for (int x = 0; x < RouteCols; x++)
            {
                float px = RouteMinX + x * RouteStep, pz = RouteMinZ + z * RouteStep;
                Vector3 bottom = new Vector3(px, map.floorY + .30f + Walker, pz), top = new Vector3(px, map.floorY + 1.9f - Walker, pz);
                map.routeFixed[z * RouteCols + x] = Blocked(Physics.OverlapCapsuleNonAlloc(bottom, top, Walker, hits, ~0, QueryTriggerInteraction.Ignore), hits, ignored);
            }
        float r = map.aceRadius;
        for (int z = 0; z < GuardRows; z++)
            for (int x = 0; x < GuardCols; x++)
            {
                float px = GuardMinX + x * GuardStep, pz = GuardMinZ + z * GuardStep;
                Vector3 bottom = new Vector3(px, map.floorY + .06f + r, pz), top = new Vector3(px, map.floorY + .06f + map.aceHeight - r, pz);
                map.guardFixed[z * GuardCols + x] = Blocked(Physics.OverlapCapsuleNonAlloc(bottom, top, r + GuardPad, hits, ~0, QueryTriggerInteraction.Ignore), hits, ignored);
            }
        map.milliseconds = (Time.realtimeSinceStartup - started) * 1000f;
        return map;
    }

    static bool Blocked(int count, Collider[] hits, HashSet<Collider> ignored)
    {
        if (count >= hits.Length) return true;   // so many things here: treat it as solid, as the guard does
        for (int i = 0; i < count; i++)
            if (hits[i] != null && !ignored.Contains(hits[i])) return true;
        return false;
    }

    static void AddFixed(FloorMap map, Bounds b, string name)
    {
        map.fixedSolids.Add(b);
        // A wall: nothing of it inside the room's open floor (the sills, the window boundaries, the plaster).
        bool wall = b.max.x <= RoomX0 + Eps || b.min.x >= RoomX1 - Eps || b.max.z <= RoomZ0 + .15f;
        map.fixedIsWall.Add(wall);
        map.fixedNames.Add(wall ? "the wall" : string.IsNullOrEmpty(name) ? "something" : name);
    }

    // A solid a walker can't step over (the routes' agent climbs a low step), or that Ace's capsule meets (from 6 cm up).
    static bool BlocksWalker(Bounds b) => b.max.y > .30f && b.min.y < 1.9f;
    static bool BlocksAce(FloorMap map, Bounds b) => b.max.y > map.floorY + .06f && b.min.y < map.floorY + .06f + map.aceHeight;

    // What the plate calls a fixed thing ("Too close to the counter").
    static string FriendlyName(Transform t)
    {
        string all = "";
        for (Transform p = t; p != null; p = p.parent) all += " " + p.name;
        if (all.Contains("Counter") || all.Contains("counter") || all.Contains("Workbench")) return "the counter";
        if (all.Contains("BinStation") || all.Contains("Bin")) return "the bins";
        if (all.Contains("door")) return "the door";
        if (all.Contains("plaster") || all.Contains("sill") || all.Contains("Window collision")) return "the wall";
        if (all.Contains("Shelf") || all.Contains("shelf")) return "the shelf";
        if (all.Contains("Plant") || all.Contains("plant")) return "the plant";
        return "something";
    }

    // ================================================================== the checks, on plain shapes

    /// <summary>
    /// Whether <paramref name="candidate"/> may stand where it is, against <paramref name="layout"/> (every other piece that is on;
    /// leave the one being moved out), with <paramref name="money"/> in the till for its <paramref name="price"/>. The first rule
    /// it breaks, with its words.
    /// </summary>
    public static Result Check(FloorMap map, IReadOnlyList<Shape> layout, Shape candidate, int price = 0, int money = int.MaxValue)
    {
        Result r = Local(map, layout, candidate);
        if (!r.Fits) return r;
        r = Guards(map, layout, candidate, true);
        if (!r.Fits) return r;
        if (price > money) return Say(Verdict.NotEnoughMoney, $"You need ${price - money} more", default);
        return r;
    }

    /// <summary>Both guards on a whole layout (the steps and the labs): every place reached from the door, and served with them all taken.</summary>
    public static Result CheckWholeLayout(FloorMap map, IReadOnlyList<Shape> layout) => Guards(map, layout, default, false);

    /// <summary>R1-R5 alone, no guards and no money: boxes and points only, a few microseconds (Arrange's search for where a
    /// new piece's ghost first waits tries every cell with this, then asks the whole Check of the few that pass).</summary>
    public static Result CheckLocal(FloorMap map, IReadOnlyList<Shape> layout, Shape candidate) =>
        map == null ? Say(Verdict.OffTheFloor, "Only on the café floor", default) : Local(map, layout, candidate);

    // R1-R5: boxes and points.
    static Result Local(FloorMap map, IReadOnlyList<Shape> layout, Shape c)
    {
        if (c.solids == null) c.solids = new List<Bounds>();
        if (c.stands == null) c.stands = new List<Vector3>();
        // R1: on the customer floor, out of the aprons.
        foreach (Bounds b in c.solids)
        {
            if (b.min.x < RoomX0 - Eps || b.max.x > RoomX1 + Eps || b.min.z < RoomZ0 - Eps || b.max.z > CounterFront - NavMargin + Eps)
                return Say(Verdict.OffTheFloor, "Only on the café floor", b);
            foreach (Apron a in Aprons)
                if (b.max.x > a.x0 && b.min.x < a.x1 && b.max.z > a.z0 && b.min.z < CounterFront)
                    return Say(a.verdict, a.words, ApronBox(a));
        }
        // R2-R4, solid by solid (Designer F's order: the first one broken is the plate's).
        for (int i = 0; i < c.solids.Count; i++)
        {
            Bounds b = c.solids[i];
            bool bay = c.PartIsBay(i);
            if (layout != null)
                foreach (Shape o in layout)
                {
                    if (o.solids == null) continue;
                    for (int j = 0; j < o.solids.Count; j++)
                    {
                        float margin = bay && o.PartIsBay(j) ? 0f : FreeMargin;
                        if (Gap(b, o.solids[j]) < margin - Eps)
                            return Say(Verdict.TooClose, "Too close to the " + Lower(o.name), o.solids[j]);
                    }
                }
            for (int j = 0; j < map.fixedSolids.Count; j++)
            {
                if (c.againstAWall && map.fixedIsWall[j]) continue;
                if (!Tall(map.fixedSolids[j])) continue;
                if (Gap(b, map.fixedSolids[j]) < FreeMargin - Eps)
                    return Say(Verdict.TooClose, "Too close to " + map.fixedNames[j], map.fixedSolids[j]);
            }
            if (InLane(b, out Bounds lane)) return Say(Verdict.Aisle, "Keep the way from the door clear", lane);
            foreach (Vector3 q in map.queue)
                if (OnAWalkToTheQueue(b, q)) return Say(Verdict.Aisle, "Keep the way from the door clear", WalkBox(q));
            if (layout != null)
                foreach (Shape o in layout)
                    if (o.stands != null)
                        foreach (Vector3 s in o.stands)
                            if (Flat(s, b) < KeepSeat - Eps) return Say(Verdict.BlocksAChair, "A chair there would be blocked", Around(s, KeepSeat));
            foreach (Vector3 s in map.fixedSeatStands)
                if (Flat(s, b) < KeepSeat - Eps) return Say(Verdict.BlocksAChair, "A chair there would be blocked", Around(s, KeepSeat));
            foreach (Vector3 w in map.waiting)
                if (Flat(w, b) < KeepWaiting - Eps) return Say(Verdict.NearWaiting, "Too close to where people wait", Around(w, KeepWaiting));
            foreach (Vector3 q in map.queue)
                if (Flat(q, b) < KeepQueue - Eps) return Say(Verdict.NearQueue, "Too close to the queue", Around(q, KeepQueue));
            if (Flat(map.door, b) < KeepDoor - Eps) return Say(Verdict.NearDoor, "Keep the door clear", Around(map.door, KeepDoor));
        }
        // R5: its own stand points.
        foreach (Vector3 s in c.stands)
        {
            if (s.x < RoomX0 + NavMargin - Eps || s.x > RoomX1 - NavMargin + Eps || s.z < RoomZ0 + NavMargin - Eps || s.z > CounterFront - NavMargin + Eps)
                return Say(Verdict.NoRoomToSit, "No room to sit down there", Around(s, NavMargin));
            if (s.x > LaneX0 && s.x < LaneX1 && s.z > LaneZ0 && s.z < LaneZ1)
                return Say(Verdict.Aisle, "Keep the way from the door clear", LaneBox());
            for (int j = 0; j < map.fixedSolids.Count; j++)
                if (Tall(map.fixedSolids[j]) && Flat(s, map.fixedSolids[j]) < NavMargin - Eps) return Say(Verdict.NoRoomToSit, "No room to sit down there", map.fixedSolids[j]);
            if (layout != null)
                foreach (Shape o in layout)
                    if (o.solids != null)
                        foreach (Bounds b in o.solids)
                            if (Flat(s, b) < NavMargin - Eps) return Say(Verdict.NoRoomToSit, "No room to sit down there", b);
            foreach (Bounds b in c.solids)
                if (Flat(s, b) < NavMargin - Eps) return Say(Verdict.NoRoomToSit, "No room to sit down there", b);
            if (layout != null)
                foreach (Shape o in layout)
                    if (o.stands != null)
                        foreach (Vector3 t in o.stands)
                            if (FlatPoints(s, t) < SeatClaim - Eps) return Say(Verdict.NearSeat, "Too close to another seat", Around(t, SeatClaim));
            foreach (Vector3 t in map.fixedSeatStands)
                if (FlatPoints(s, t) < SeatClaim - Eps) return Say(Verdict.NearSeat, "Too close to another seat", Around(t, SeatClaim));
            foreach (Vector3 w in map.waiting)
                if (FlatPoints(s, w) < SeatToWaiting - Eps) return Say(Verdict.NearWaiting, "Too close to where people wait", Around(w, SeatToWaiting));
            foreach (Vector3 q in map.queue)
                if (FlatPoints(s, q) < SeatToQueue - Eps) return Say(Verdict.NearQueue, "Too close to the queue", Around(q, SeatToQueue));
        }
        return Say(Verdict.Fits, "", default);
    }

    // R6a and R6b on the layout (and the candidate, if given).
    static Result Guards(FloorMap map, IReadOnlyList<Shape> layout, Shape candidate, bool withCandidate)
    {
        // ---- the places: every seat (pieces', the candidate's, the fixed ones), waiting spot and queue place; stations for R6a ----
        List<Vector3> served = map.served;
        served.Clear();
        if (layout != null)
            foreach (Shape o in layout)
                if (o.stands != null) served.AddRange(o.stands);
        if (withCandidate && candidate.stands != null) served.AddRange(candidate.stands);
        served.AddRange(map.fixedSeatStands);
        served.AddRange(map.waiting);
        served.AddRange(map.queue);
        List<Vector3> reached = map.targets;
        reached.Clear();
        reached.AddRange(served);
        reached.AddRange(map.stations);

        // ---- R6a: the routes from the door ----
        bool[] blocked = map.routeBlocked;
        System.Array.Copy(map.routeFixed, blocked, blocked.Length);
        if (layout != null)
            foreach (Shape o in layout)
                if (o.solids != null) foreach (Bounds b in o.solids) Rasterise(blocked, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, b, Walker);
        if (withCandidate && candidate.solids != null)
            foreach (Bounds b in candidate.solids) Rasterise(blocked, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, b, Walker);
        for (int i = 0; i < map.sheetBoxes.Count; i++)
            if (SheetBoxCounts(map, i, candidate, withCandidate))
                Rasterise(blocked, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, map.sheetBoxes[i], Walker);
        bool[] seen = map.routeSeen;
        System.Array.Clear(seen, 0, seen.Length);
        int seed = Nearest(blocked, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, map.door, .5f, inverted: true);
        if (seed >= 0) Flood(map.frontier, blocked, seen, RouteCols, RouteRows, seed, inverted: true);
        int reachedCount = 0;
        Vector3 firstLost = Vector3.zero;
        bool anyLost = false;
        foreach (Vector3 t in reached)
        {
            if (AnyWithin(seen, RouteMinX, RouteMinZ, RouteStep, RouteCols, RouteRows, t, SampleReach)) reachedCount++;
            else if (!anyLost) { anyLost = true; firstLost = t; }
        }

        // ---- R6b: Ace with every place taken ----
        bool[] clear = map.guardClear;
        float pad = map.aceRadius + GuardPad, envelope = map.aceRadius + BodyEnvelope + GuardPad;
        for (int i = 0; i < clear.Length; i++) clear[i] = !map.guardFixed[i];
        if (layout != null)
            foreach (Shape o in layout)
                if (o.solids != null) foreach (Bounds b in o.solids) RasteriseClear(clear, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, b, pad);
        if (withCandidate && candidate.solids != null)
            foreach (Bounds b in candidate.solids) RasteriseClear(clear, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, b, pad);
        for (int i = 0; i < map.sheetBoxes.Count; i++)
            if (SheetBoxCounts(map, i, candidate, withCandidate))
                RasteriseClear(clear, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, map.sheetBoxes[i], pad);
        foreach (Vector3 p in served) Envelope(clear, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, p, envelope);
        bool[] gseen = map.guardSeen;
        System.Array.Clear(gseen, 0, gseen.Length);
        int gseed = Nearest(clear, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, map.aceStart, .75f, inverted: false);
        if (gseed >= 0) Flood(map.frontier, clear, gseen, GuardCols, GuardRows, gseed, inverted: false);
        int servedCount = 0;
        Vector3 firstUnserved = Vector3.zero;
        bool anyUnserved = false;
        foreach (Vector3 p in served)
        {
            if (gseed >= 0 && AnyWithin(gseen, GuardMinX, GuardMinZ, GuardStep, GuardCols, GuardRows, p, Reach)) servedCount++;
            else if (!anyUnserved) { anyUnserved = true; firstUnserved = p; }
        }

        Result r;
        if (anyLost) r = Say(Verdict.BlocksAWay, "That would block someone's way", Around(firstLost, .4f));
        else if (anyUnserved) r = Say(Verdict.AceCantPass, "Ace couldn't get past a full café", Around(firstUnserved, .4f));
        else r = Say(Verdict.Fits, "", default);
        r.reached = reachedCount;
        r.places = reached.Count;
        r.served = servedCount;
        r.servedOf = served.Count;
        return r;
    }

    /// <summary>A stand point this near a sheet folds it too: someone will stand there (the agent's 0.35 and a little).</summary>
    public const float SheetReach = .45f;

    /// <summary>
    /// Whether a piece standing as <paramref name="shape"/> folds the dust sheet on <paramref name="area"/>: its footprint is on
    /// the sheet, or one of its people would stand there (CafeLayout folds it; the guards stop counting its boxes).
    /// </summary>
    public static bool FoldsSheet(Shape shape, Rect area)
    {
        if (shape.solids != null)
            foreach (Bounds b in shape.solids)
                if (b.max.x > area.xMin && b.min.x < area.xMax && b.max.z > area.yMin && b.min.z < area.yMax) return true;
        if (shape.stands != null)
            foreach (Vector3 p in shape.stands)
                if (p.x > area.xMin - SheetReach && p.x < area.xMax + SheetReach && p.z > area.yMin - SheetReach && p.z < area.yMax + SheetReach) return true;
        return false;
    }

    // A sheet's box counts for the guards while its sheet lies and the candidate wouldn't fold it.
    static bool SheetBoxCounts(FloorMap map, int i, Shape candidate, bool withCandidate)
    {
        DustSheet sheet = map.sheetOf[i];
        if (sheet != null && !sheet.Lying) return false;
        return !(withCandidate && FoldsSheet(candidate, map.sheetAreas[i]));
    }

    // ================================================================== the checks, on pieces

    /// <summary>
    /// Whether a piece of <paramref name="kind"/> may stand at <paramref name="pivot"/> turned <paramref name="yaw"/>, against the
    /// pieces in <paramref name="layout"/> that are on (<paramref name="moving"/> left out: the one in hand), with
    /// <paramref name="money"/> in the till (int.MaxValue: a move, which is free).
    /// </summary>
    public static Result Check(FloorMap map, IReadOnlyList<PlacedPiece> layout, FurnitureDefinition kind,
                               Vector3 pivot, float yaw, PlacedPiece moving = null, int money = int.MaxValue)
    {
        if (map == null || kind == null) return Say(Verdict.OffTheFloor, "Only on the café floor", default);
        PlacedPiece template = moving != null ? moving : TemplateOf(kind, "");
        if (template == null) return Say(Verdict.OffTheFloor, "Only on the café floor", default);
        return Check(map, layout, kind, template, pivot, yaw, moving, money);
    }

    /// <summary>The same with the piece's shape given (a plant's variant, a prefab): <paramref name="template"/> is what stands there.</summary>
    public static Result Check(FloorMap map, IReadOnlyList<PlacedPiece> layout, FurnitureDefinition kind, PlacedPiece template,
                               Vector3 pivot, float yaw, PlacedPiece moving = null, int money = int.MaxValue)
    {
        if (map == null || kind == null || template == null) return Say(Verdict.OffTheFloor, "Only on the café floor", default);
        if (kind.againstAWall && !OnASideWall(pivot, yaw))
            return Say(Verdict.OffTheFloor, "Only against a side wall", default);
        Shape candidate = template.ShapeAt(pivot, yaw);
        candidate.againstAWall = kind.againstAWall;
        return Check(map, ShapesOf(layout, moving), candidate, moving != null ? 0 : kind.price, money);
    }

    /// <summary>Both guards on the pieces in <paramref name="layout"/> that are on.</summary>
    public static Result CheckWholeLayout(FloorMap map, IReadOnlyList<PlacedPiece> layout) =>
        map == null ? Say(Verdict.BlocksAWay, "That would block someone's way", default) : CheckWholeLayout(map, ShapesOf(layout, null));

    /// <summary>A kind's shape (its prefab's piece; a plant's variant's).</summary>
    public static PlacedPiece TemplateOf(FurnitureDefinition kind, string variant)
    {
        if (kind == null) return null;
        GameObject prefab = kind.PrefabFor(variant);
        return prefab != null ? prefab.GetComponent<PlacedPiece>() : null;
    }

    /// <summary>The shapes of the pieces that are on, leaving one out.</summary>
    public static List<Shape> ShapesOf(IReadOnlyList<PlacedPiece> layout, PlacedPiece leaveOut)
    {
        var shapes = new List<Shape>();
        if (layout == null) return shapes;
        foreach (PlacedPiece p in layout)
            if (p != null && p != leaveOut && p.On) shapes.Add(p.Shape);
        return shapes;
    }

    // ================================================================== poses

    /// <summary>A point snapped to the grid (x from the door's middle, z from the shop front), on the floor.</summary>
    public static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x / Grid) * Grid, 0f, Mathf.Round(p.z / Grid) * Grid);

    /// <summary>A quarter turn, 0..270.</summary>
    public static float Quarter(float yaw) => Mathf.Repeat(Mathf.Round(yaw / 90f) * 90f, 360f);

    /// <summary>A wall piece's pose against the west or the east wall nearest <paramref name="x"/>, at <paramref name="z"/> on the grid.</summary>
    public static void WallPose(float x, float z, out Vector3 pivot, out float yaw)
    {
        bool west = x < 0f;
        pivot = new Vector3(west ? WestWall : EastWall, 0f, Mathf.Round(z / Grid) * Grid);
        yaw = west ? WestWallYaw : EastWallYaw;
    }

    /// <summary>A wall piece's pose is on the west or the east wall, facing the room.</summary>
    public static bool OnASideWall(Vector3 pivot, float yaw)
    {
        float y = Mathf.Repeat(yaw, 360f);
        bool west = Mathf.Abs(pivot.x - WestWall) <= .02f && Mathf.Abs(Mathf.DeltaAngle(y, WestWallYaw)) <= 1f;
        bool east = Mathf.Abs(pivot.x - EastWall) <= .02f && Mathf.Abs(Mathf.DeltaAngle(y, EastWallYaw)) <= 1f;
        return west || east;
    }

    /// <summary>Every verdict's words, for the checks (each at most seven words).</summary>
    public static IEnumerable<string> AllWords()
    {
        foreach (Apron a in Aprons) yield return a.words;
        yield return "Only on the café floor";
        yield return "Only against a side wall";
        yield return "Too close to the banquette bay";
        yield return "Too close to the counter";
        yield return "Keep the way from the door clear";
        yield return "A chair there would be blocked";
        yield return "Too close to where people wait";
        yield return "Too close to the queue";
        yield return "Keep the door clear";
        yield return "No room to sit down there";
        yield return "Too close to another seat";
        yield return "That would block someone's way";
        yield return "Ace couldn't get past a full café";
        yield return "You need $360 more";
    }

    // ================================================================== geometry

    static Result Say(Verdict v, string words, Bounds inTheWay) => new Result { verdict = v, words = words ?? "", inTheWay = inTheWay };

    static string Lower(string name) => string.IsNullOrEmpty(name) ? "furniture" : name.ToLowerInvariant();

    // Something worth keeping a gap from: taller than a rug or a mat.
    static bool Tall(Bounds b) => b.max.y > .06f;

    /// <summary>The flat gap between two boxes (0 when they touch, negative when they overlap: the overlap's smaller depth).</summary>
    public static float Gap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x);
        float dz = Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z);
        if (dx < 0f && dz < 0f) return Mathf.Max(dx, dz);
        if (dx >= 0f && dz >= 0f) return Mathf.Sqrt(dx * dx + dz * dz);
        return Mathf.Max(dx, dz);
    }

    /// <summary>A point's flat distance from a box (0 inside it).</summary>
    public static float Flat(Vector3 p, Bounds b)
    {
        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
        float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float FlatPoints(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // R3: in the door-to-counter lane, or within 0.6 m of a walk from the door (0, 0) to a queue place (25 points along it,
    // as Designer F's model samples it).
    static bool InLane(Bounds b, out Bounds lane)
    {
        lane = LaneBox();
        if (b.max.x > LaneX0 && b.min.x < LaneX1 && b.max.z > LaneZ0 && b.min.z < LaneZ1) return true;
        return false;
    }

    /// <summary>Whether a box comes within 0.6 m of a walk from the door to <paramref name="queuePlace"/> (R3's second half).</summary>
    public static bool OnAWalkToTheQueue(Bounds b, Vector3 queuePlace)
    {
        for (int i = 0; i <= 24; i++)
        {
            float t = i / 24f;
            if (Flat(new Vector3(queuePlace.x * t, 0f, queuePlace.z * t), b) < QueueWalkHalf - Eps) return true;
        }
        return false;
    }

    static Bounds LaneBox()
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(LaneX0, 0f, LaneZ0), new Vector3(LaneX1, .05f, LaneZ1));
        return b;
    }

    // The walk from the door to a queue place, as a box to light up.
    static Bounds WalkBox(Vector3 q)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(Mathf.Min(0f, q.x) - QueueWalkHalf, 0f, 0f), new Vector3(Mathf.Max(0f, q.x) + QueueWalkHalf, .05f, q.z));
        return b;
    }

    static Bounds ApronBox(Apron a)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(a.x0, 0f, a.z0), new Vector3(a.x1, .05f, CounterFront));
        return b;
    }

    static Bounds Around(Vector3 p, float r) => new Bounds(new Vector3(p.x, .02f, p.z), new Vector3(2f * r, .04f, 2f * r));

    // Marks every cell whose centre lies within `pad` (flat) of a box as blocked (true).
    static void Rasterise(bool[] cells, float minX, float minZ, float step, int cols, int rows, Bounds b, float pad)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - pad - minX) / step)), x1 = Mathf.Min(cols - 1, Mathf.CeilToInt((b.max.x + pad - minX) / step));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - pad - minZ) / step)), z1 = Mathf.Min(rows - 1, Mathf.CeilToInt((b.max.z + pad - minZ) / step));
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (cells[z * cols + x]) continue;
                if (Flat(new Vector3(minX + x * step, 0f, minZ + z * step), b) < pad) cells[z * cols + x] = true;
            }
    }

    // The same, clearing (false) cells of a "clear" grid.
    static void RasteriseClear(bool[] clear, float minX, float minZ, float step, int cols, int rows, Bounds b, float pad)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - pad - minX) / step)), x1 = Mathf.Min(cols - 1, Mathf.CeilToInt((b.max.x + pad - minX) / step));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - pad - minZ) / step)), z1 = Mathf.Min(rows - 1, Mathf.CeilToInt((b.max.z + pad - minZ) / step));
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!clear[z * cols + x]) continue;
                if (Flat(new Vector3(minX + x * step, 0f, minZ + z * step), b) < pad) clear[z * cols + x] = false;
            }
    }

    // A body's envelope: every cell within `r` of a place is not clear (the guard: point within the envelope).
    static void Envelope(bool[] clear, float minX, float minZ, float step, int cols, int rows, Vector3 p, float r)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - r - minX) / step)), x1 = Mathf.Min(cols - 1, Mathf.CeilToInt((p.x + r - minX) / step));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((p.z - r - minZ) / step)), z1 = Mathf.Min(rows - 1, Mathf.CeilToInt((p.z + r - minZ) / step));
        float r2 = r * r;
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = minX + x * step - p.x, dz = minZ + z * step - p.z;
                if (dx * dx + dz * dz < r2) clear[z * cols + x] = false;
            }
    }

    // The cell nearest `p` within `within` that is open (blocked grids: false; clear grids: true); -1 if none.
    static int Nearest(bool[] cells, float minX, float minZ, float step, int cols, int rows, Vector3 p, float within, bool inverted)
    {
        int best = -1;
        float bestD = within * within;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - within - minX) / step)), x1 = Mathf.Min(cols - 1, Mathf.CeilToInt((p.x + within - minX) / step));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((p.z - within - minZ) / step)), z1 = Mathf.Min(rows - 1, Mathf.CeilToInt((p.z + within - minZ) / step));
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int i = z * cols + x;
                bool open = inverted ? !cells[i] : cells[i];
                if (!open) continue;
                float dx = minX + x * step - p.x, dz = minZ + z * step - p.z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = i; }
            }
        return best;
    }

    // Four-neighbour flood from `seed` over open cells (blocked grids: false; clear grids: true).
    static void Flood(Queue<int> frontier, bool[] cells, bool[] seen, int cols, int rows, int seed, bool inverted)
    {
        frontier.Clear();
        frontier.Enqueue(seed);
        seen[seed] = true;
        while (frontier.Count > 0)
        {
            int i = frontier.Dequeue(), x = i % cols, z = i / cols;
            if (x > 0) Visit(i - 1);
            if (x < cols - 1) Visit(i + 1);
            if (z > 0) Visit(i - cols);
            if (z < rows - 1) Visit(i + cols);
        }

        void Visit(int n)
        {
            if (seen[n]) return;
            bool open = inverted ? !cells[n] : cells[n];
            if (!open) return;
            seen[n] = true;
            frontier.Enqueue(n);
        }
    }

    // Whether any seen cell lies within `r` of `p`.
    static bool AnyWithin(bool[] seen, float minX, float minZ, float step, int cols, int rows, Vector3 p, float r)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - r - minX) / step)), x1 = Mathf.Min(cols - 1, Mathf.CeilToInt((p.x + r - minX) / step));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((p.z - r - minZ) / step)), z1 = Mathf.Min(rows - 1, Mathf.CeilToInt((p.z + r - minZ) / step));
        float r2 = r * r;
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!seen[z * cols + x]) continue;
                float dx = minX + x * step - p.x, dz = minZ + z * step - p.z;
                if (dx * dx + dz * dz <= r2) return true;
            }
        return false;
    }
}
