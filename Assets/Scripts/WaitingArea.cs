using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Owns every place a waiting customer can go. A customer asks for a spot the
// moment their job is accepted, and hands it back when they leave.
//
// STEP 2 CHANGE — the spot list is now a registry, not a hierarchy scan.
//
// Spots call Register/Unregister from their own OnEnable/OnDisable, so a
// TableSeat can live under its table where it belongs instead of being forced
// under this object. Everything else about the API is unchanged, which is why
// CustomerBrain needed no edits at all for seating to start working.
public class WaitingArea : MonoBehaviour
{
    public static WaitingArea Instance { get; private set; }

    private static readonly List<WaitingSpot> registry = new List<WaitingSpot>();
    private static readonly List<WaitingSpot> scratch = new List<WaitingSpot>();
    private static readonly List<float> distances = new List<float>();
    private static readonly Vector3[] corners = new Vector3[32];
    private static NavMeshPath route;

    [Tooltip("People take the nearest free spot (by walking distance). Spots within this many metres of the " +
             "nearest count as equally near and are chosen between at random, so two people arriving together " +
             "don't both head for the same chair.")]
    [SerializeField, Range(0f, 3f)] private float nearlyAsNear = 0.6f;

    [Tooltip("Metres added to a spot's walking distance for each other person still on their way to a spot at the " +
             "same table (within 2 m). Someone arriving alone takes the nearest seat; a group arriving together " +
             "spreads over the room instead of all converging on the table by the door.")]
    [SerializeField, Range(0f, 6f)] private float onTheirWayPenalty = 2.5f;

    [Tooltip("Optional. Leave empty — spots register themselves now, wherever " +
             "they sit in the hierarchy. Anything listed here is registered " +
             "too, so old scene wiring still works.")]
    [SerializeField] private WaitingSpot[] spots;

    // With Enter Play Mode Options set to skip domain reload, statics survive
    // between play sessions — so the registry would still be holding last
    // run's destroyed spots. This runs before the scene loads and wipes them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
        scratch.Clear();
        Instance = null;
    }

    public static void Register(WaitingSpot spot)
    {
        if (spot == null || registry.Contains(spot)) return;
        registry.Add(spot);
    }

    public static void Unregister(WaitingSpot spot)
    {
        registry.Remove(spot);
    }

    // Handy in the Console when you're wondering why nobody sits down.
    public static int RegisteredSpots => registry.Count;

    private void Awake()
    {
        Instance = this;

        // Belt and braces for anything wired the old way.
        if (spots != null)
            foreach (WaitingSpot s in spots) Register(s);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool HasFreeSpot
    {
        get
        {
            for (int i = 0; i < registry.Count; i++)
            {
                WaitingSpot s = registry[i];
                if (s != null && s.IsAvailable && HasClaimClearance(s, null)) return true;
            }
            return false;
        }
    }

    // Try the kind they'd prefer first, then settle for anything free.
    // Null means the whole floor is full — the caller decides what to do.
    //
    // This two-pass shape is what makes seating a PREFERENCE rather than a
    // requirement: when every seat is taken (or dirty), a customer who wanted
    // to sit falls through to a loiter spot and starts draining at 1.15x
    // instead of 0.6x. That difference is the entire pressure model.
    public WaitingSpot Claim(Component occupant, WaitingSpot.SpotKind preferred)
    {
        WaitingSpot chosen = PickNearest(occupant, preferred, true);
        if (chosen == null) chosen = PickNearest(occupant, preferred, false);
        return chosen;
    }

    // THE NEAREST FREE SPOT (pass 2b, 27 Sept 2026). This used to be a random
    // free spot anywhere in the room ("so the room doesn't fill left to right"),
    // which had people walking past empty chairs to one across the café - it
    // read as automated. People take the nearest free seat to where they are
    // (the counter after ordering, the door when they come in), by walking
    // distance along the NavMesh. Spots nearly as near as the nearest are
    // shared at random. The room still fills naturally: from the door for
    // walk-ins, from the counter for customers.
    private WaitingSpot PickNearest(Component occupant, WaitingSpot.SpotKind kind, bool matchKind)
    {
        scratch.Clear();

        for (int i = 0; i < registry.Count; i++)
        {
            WaitingSpot s = registry[i];
            if (s == null || !s.IsAvailable || !HasClaimClearance(s, occupant)) continue;
            if (matchKind && s.Kind != kind) continue;
            scratch.Add(s);
        }

        if (scratch.Count == 0) return null;

        Vector3 from = occupant != null ? occupant.transform.position : transform.position;
        // Straight-line order first; only the closest few need a real route.
        scratch.Sort((a, b) => Flat(a.StandPoint.position - from).sqrMagnitude.CompareTo(Flat(b.StandPoint.position - from).sqrMagnitude));
        int evaluate = Mathf.Min(scratch.Count, 8);
        distances.Clear();
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < evaluate; i++)
        {
            float d = WalkingDistance(from, scratch[i].StandPoint.position)
                      + onTheirWayPenalty * OnTheirWayNear(scratch[i], occupant);
            distances.Add(d);
            nearest = Mathf.Min(nearest, d);
        }
        int ties = 0;
        for (int i = 0; i < evaluate; i++) if (distances[i] <= nearest + nearlyAsNear) ties++;
        int chosen = Random.Range(0, ties);
        WaitingSpot pick = null;
        for (int i = 0; i < evaluate && pick == null; i++)
            if (distances[i] <= nearest + nearlyAsNear && chosen-- == 0) pick = scratch[i];
        if (pick == null) pick = scratch[0];
        return pick.Claim(occupant) ? pick : null;
    }

    // People who have claimed a spot at the same table but are not sitting in
    // it yet (still walking over): heading there too would converge on them.
    private static int OnTheirWayNear(WaitingSpot candidate, Component asker)
    {
        Vector3 here = candidate is TableSeat c ? c.SeatPose.position : candidate.StandPoint.position;
        int count = 0;
        for (int i = 0; i < registry.Count; i++)
        {
            WaitingSpot s = registry[i];
            if (s == null || s == candidate || !s.IsOccupied || s.Occupant == asker || s.Occupant == null) continue;
            Vector3 there = s is TableSeat t ? t.SeatPose.position : s.StandPoint.position;
            Vector3 d = there - here;
            d.y = 0f;
            if (d.sqrMagnitude > 2f * 2f) continue;
            NpcSeating seating = s.Occupant.GetComponent<NpcSeating>();
            if (seating == null || !seating.Busy) count++;   // claimed, and not in (or getting into) the chair yet
        }
        return count;
    }

    // Metres along the floor, or a pessimistic straight line when there is no route.
    private static float WalkingDistance(Vector3 from, Vector3 to)
    {
        if (route == null) route = new NavMeshPath();
        if (NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(start.position, to, NavMesh.AllAreas, route)
            && route.status == NavMeshPathStatus.PathComplete)
        {
            int count = route.GetCornersNonAlloc(corners);
            float length = 0f;
            for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
        return Flat(to - from).magnitude * 1.5f + 2f;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    public void Release(Component occupant)
    {
        for (int i = 0; i < registry.Count; i++)
            if (registry[i] != null) registry[i].Release(occupant);
    }

    // Separate spot components can still point to the same physical space.
    // Reserve body room, including the stopping margin, across both populations.
    public static bool HasClaimClearance(WaitingSpot candidate, Component occupant)
    {
        if (candidate == null) return false;
        float radius = Footprint(candidate, occupant);
        foreach (WaitingSpot spot in registry)
        {
            if (spot == null || spot == candidate || !spot.IsOccupied || spot.Occupant == occupant) continue;
            float otherRadius = Footprint(spot, spot.Occupant);
            Vector3 delta = candidate.StandPoint.position - spot.StandPoint.position;
            if (Mathf.Abs(delta.y) > 1.5f) continue;
            delta.y = 0;
            float clearance = radius + otherRadius + 0.1f;
            if (delta.sqrMagnitude < clearance * clearance) return false;
        }
        return true;
    }

    // Room kept round a spot's stand point for the clearance rule. A spot's own
    // footprint wins when it has one (bench seats sit 0.7 m apart). A chair that
    // people actually sit on (Snap To Seat) keeps a small footprint too: its
    // sitter leaves the stand point the moment they sit, so two chairs whose
    // stand points are 0.8 m apart may both be taken. Loiter spots, where the
    // body stays standing on the point, keep the body's own room.
    private static float Footprint(WaitingSpot spot, Component occupant)
    {
        if (spot.ClearanceRadius > 0f) return spot.ClearanceRadius;
        if (spot is TableSeat seat && seat.SnapToSeat) return 0.3f;
        NavMeshAgent agent = occupant != null ? occupant.GetComponent<NavMeshAgent>() : null;
        return agent != null ? agent.radius + agent.stoppingDistance : 0.35f;
    }

    // How full the room looks right now. Used by PatronSpawner so patrons stop
    // arriving before they can squeeze paying customers out entirely — a cafe
    // so busy you can't work is atmosphere winning over gameplay.
    public static int FreeSeats
    {
        get
        {
            int n = 0;
            for (int i = 0; i < registry.Count; i++)
            {
                WaitingSpot s = registry[i];
                if (s != null && s.IsAvailable && s.Kind == WaitingSpot.SpotKind.Seat && HasClaimClearance(s, null)) n++;
            }
            return n;
        }
    }
}
