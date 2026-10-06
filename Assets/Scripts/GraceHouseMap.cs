using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THE WAYS GRACE WALKS IN HER OWN HOUSE (break-ins chunk C, 6 Oct 2026; claude/chunk-c-grace-at-home-plan.md §5)
//
// There is no navigation mesh inside her house (by day nothing walks in it, and the café's mesh stops at her door), so
// she walks set ways between known spots, the way Ace's walk check does. Spots are in plan metres (claude/break-ins-spec.md
// §4): X along West Street from the south wall (north is +X), Y from the back wall toward the street, Z up from the
// ground floor (the first floor is at 2.40). Most of them are stops Ace's own capsule reached in the walk check
// (GraceHouseWalkCheck: 1.16 m wide, wider than her); the rest stand beside a piece of furniture, measured from the
// pieces as built (GraceHouseSteps; Tools/Blender/plan_v2.py AS_BUILT):
//   * in front of her armchair (it faces the TV on the north wall; she sits back into it), at the kettle (on the hob),
//     at the sink (the counter's north end), beside the bed (its open side faces the back of the house);
//   * the stairs: the bottom step, the top of each flight and the landing between, so her height follows the flights.
// A way is a straight line between two spots that clears the walls, the stairs' block and the furniture.
//
// Paths are the shortest by length (Dijkstra over a few dozen spots). No Unity types: Tests/GraceRules checks the map
// (every spot reaches every other; her armchair to her bed goes up both flights; nothing crosses a wall).
// ---------------------------------------------------------------------------
public static class GraceHouseMap
{
    public readonly struct Spot
    {
        public readonly string name;
        public readonly float X, Y, Z;

        public Spot(string name, float x, float y, float z = 0f)
        {
            this.name = name;
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>0 on the ground floor (and the lower flight), 1 upstairs (from the top of the upper flight).</summary>
        public int Storey => Z > 1.8f ? 1 : 0;
    }

    // The spots by name, for the code that uses them.
    public const int Doorway = 0, Entry = 1, EntryByStairs = 2, FrontRoomWest = 3, Pocket = 4, TowardKitchen = 5, CupboardCorner = 6,
        Kettle = 7, Kitchen = 8, Sink = 9, Fridge = 10, FrontRoomEast = 11, Armchair = 12, FootOfStairs = 13, BottomStep = 14,
        LowerFlightTop = 15, Landing = 16, UpperFlightFoot = 17, UpperFlightTop = 18, TopOfStairs = 19, LandingUpstairs = 20,
        BedroomDoorway = 21, InsideBedroomDoor = 22, Bedside = 23, Bedroom = 24, AcrossBedroom = 25, Wardrobe = 26;

    public static readonly Spot[] Spots =
    {
        new Spot("in her doorway", 1.11f, 4.05f),
        new Spot("in the entry", 1.11f, 3.35f),
        new Spot("in the entry, by the stairs", 1.90f, 3.25f),
        new Spot("in the front room, west of her armchair", 2.45f, 2.75f),
        new Spot("in the pocket, at the cupboard under the stairs", 2.22f, 2.08f),
        new Spot("round toward the kitchen", 3.00f, 2.02f),
        new Spot("past the cupboard's corner", 3.28f, 1.50f),
        new Spot("at the kettle", 3.40f, 1.05f),
        new Spot("in the kitchen", 3.90f, 1.45f),
        new Spot("at the sink", 4.16f, 1.05f),
        new Spot("at the fridge", 4.65f, 1.30f),
        new Spot("in the front room, beside her armchair", 4.60f, 2.10f),
        new Spot("in front of her armchair", 4.62f, 2.83f),
        new Spot("at the foot of the stairs", .72f, 3.05f),
        new Spot("on the bottom step", .72f, 2.72f),
        new Spot("at the top of the lower flight", .70f, 1.40f, 1.20f),
        new Spot("on the landing", .70f, .72f, 1.20f),
        new Spot("at the foot of the upper flight", 1.40f, .70f, 1.20f),
        new Spot("at the top of the upper flight", 2.60f, .70f, 2.40f),
        new Spot("at the top of the stairs", 2.95f, .72f, 2.40f),
        new Spot("on the landing upstairs", 3.05f, .85f, 2.40f),
        new Spot("in the bedroom's doorway", 3.05f, 1.55f, 2.40f),
        new Spot("just inside the bedroom doors", 3.05f, 2.30f, 2.40f),
        new Spot("beside her bed", 4.40f, 2.25f, 2.40f),
        new Spot("in the bedroom", 2.65f, 2.90f, 2.40f),
        new Spot("across the bedroom", 1.85f, 3.42f, 2.40f),
        new Spot("at the wardrobe", 1.25f, 3.62f, 2.40f),
    };

    // The straight ways between spots (both directions).
    static readonly (int a, int b)[] Ways =
    {
        (Doorway, Entry), (Entry, EntryByStairs), (Entry, FootOfStairs), (EntryByStairs, FrontRoomWest), (EntryByStairs, Pocket),
        (FrontRoomWest, Pocket), (FrontRoomWest, TowardKitchen), (Pocket, TowardKitchen), (TowardKitchen, CupboardCorner),
        (TowardKitchen, FrontRoomEast), (CupboardCorner, Kettle), (CupboardCorner, Kitchen), (Kettle, Kitchen), (Kitchen, Sink),
        (Kitchen, Fridge), (Sink, Fridge), (Kitchen, FrontRoomEast), (FrontRoomEast, Armchair),
        (FootOfStairs, BottomStep), (BottomStep, LowerFlightTop), (LowerFlightTop, Landing), (Landing, UpperFlightFoot),
        (UpperFlightFoot, UpperFlightTop), (UpperFlightTop, TopOfStairs), (TopOfStairs, LandingUpstairs),
        (LandingUpstairs, BedroomDoorway), (BedroomDoorway, InsideBedroomDoor), (InsideBedroomDoor, Bedside),
        (InsideBedroomDoor, Bedroom), (Bedroom, AcrossBedroom), (AcrossBedroom, Wardrobe),
    };

    static List<int>[] links;

    static List<int>[] Links
    {
        get
        {
            if (links != null) return links;
            var made = new List<int>[Spots.Length];
            for (int i = 0; i < made.Length; i++) made[i] = new List<int>(4);
            foreach ((int a, int b) in Ways)
            {
                made[a].Add(b);
                made[b].Add(a);
            }
            links = made;
            return links;
        }
    }

    /// <summary>How many ways there are (for the checks).</summary>
    public static int WayCount => Ways.Length;

    /// <summary>The spots one way away from <paramref name="spot"/>. Treat as read-only.</summary>
    public static IReadOnlyList<int> Next(int spot) => Links[spot];

    public static bool Joined(int a, int b) => a >= 0 && a < Spots.Length && Links[a].Contains(b);

    /// <summary>The spot with this name, or -1.</summary>
    public static int Find(string name)
    {
        for (int i = 0; i < Spots.Length; i++) if (Spots[i].name == name) return i;
        return -1;
    }

    /// <summary>Straight-line metres between two spots (up the stairs too).</summary>
    public static float Length(int a, int b) => Distance(Spots[a], Spots[b].X, Spots[b].Y, Spots[b].Z);

    public static float Distance(Spot s, float x, float y, float z)
    {
        float dx = s.X - x, dy = s.Y - y, dz = s.Z - z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// The spots from <paramref name="from"/> to <paramref name="to"/>, both included, the shortest way; empty if there is
    /// none. Written into <paramref name="path"/> (cleared first), so a caller can reuse one list.
    /// </summary>
    public static bool Path(int from, int to, List<int> path)
    {
        path.Clear();
        int n = Spots.Length;
        if (from < 0 || from >= n || to < 0 || to >= n) return false;
        if (from == to) { path.Add(from); return true; }
        var distance = new float[n];
        var previous = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { distance[i] = float.MaxValue; previous[i] = -1; }
        distance[from] = 0f;
        for (int round = 0; round < n; round++)
        {
            int best = -1;
            for (int i = 0; i < n; i++)
                if (!done[i] && distance[i] < float.MaxValue && (best < 0 || distance[i] < distance[best])) best = i;
            if (best < 0 || best == to) break;
            done[best] = true;
            foreach (int next in Links[best])
            {
                float through = distance[best] + Length(best, next);
                if (through < distance[next]) { distance[next] = through; previous[next] = best; }
            }
        }
        if (previous[to] < 0) return false;
        for (int at = to; at >= 0; at = previous[at]) path.Add(at);
        path.Reverse();
        return path.Count > 0 && path[0] == from;
    }

    /// <summary>The length of the shortest way between two spots, or float.MaxValue with none.</summary>
    public static float PathLength(int from, int to)
    {
        var path = new List<int>();
        if (!Path(from, to, path)) return float.MaxValue;
        float length = 0f;
        for (int i = 1; i < path.Count; i++) length += Length(path[i - 1], path[i]);
        return length;
    }

    /// <summary>
    /// The spot nearest a point (plan metres) at about its height: within a metre up or down of it, so a point on the
    /// landing finds the landing, never the room under it. -1 when no spot is that near in height.
    /// </summary>
    public static int Nearest(float x, float y, float z)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Spots.Length; i++)
        {
            Spot s = Spots[i];
            if (Math.Abs(s.Z - z) > 1f) continue;
            float dx = s.X - x, dy = s.Y - y;
            float d = dx * dx + dy * dy;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }
}
