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
//
// 23 OCT 2026 — SEATS CHOSEN THE WAY PEOPLE CHOOSE THEM. Claim no longer gives
// the nearest free spot: it gives the one with the best score for the person
// asking (SeatChoice, in SeatTags.cs: their character's leanings for the seat's
// tags, a regular's favourite, strangers close by, then the walk). The API is
// the same, so every caller works as it did. A spot placed at run time (a
// table the player buys) needs nothing more: it registers itself, and its
// tags come from the seat and the room (SeatTags).
public class WaitingArea : MonoBehaviour
{
    public static WaitingArea Instance { get; private set; }

    private static readonly List<WaitingSpot> registry = new List<WaitingSpot>();
    private static readonly List<WaitingSpot> scratch = new List<WaitingSpot>();
    private static readonly Vector3[] corners = new Vector3[32];
    private static NavMeshPath route;

    // PickBest's working lists, kept so a claim allocates nothing after the first.
    private static readonly List<Vector3> others = new List<Vector3>();
    private static readonly List<float> reasons = new List<float>();
    private static readonly List<float> atMost = new List<float>();
    private static readonly List<int> order = new List<int>();
    private static readonly List<int> weighed = new List<int>();
    private static readonly List<float> scores = new List<float>();
    private static readonly List<int> nearlyBest = new List<int>();

    // 23 Oct 2026: points of a seat's score now (SeatChoice; a point is about 3 m of walking for someone in no hurry), not
    // metres; the scene's 0.6 carries over as 0.6 points, two metres of an ordinary walk.
    [Tooltip("Seats within this many points of the best seat's score (SeatChoice: a point is about 3 m of walking for " +
             "someone in no hurry) are near-equals, and one of them is chosen at random, so two people with the same " +
             "reasons don't both head for the same chair.")]
    [SerializeField, Range(0f, 3f)] private float nearlyAsNear = 0.6f;

    [Tooltip("Metres added to a spot's walking distance for each other person still on their way to a spot at the " +
             "same table (within 2 m), so a group arriving together doesn't converge on one table as they walk. " +
             "Strangers already there count as well, as personal space (SeatChoice.Crowding).")]
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
        others.Clear();
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
    // Each pass takes the best spot for this person (PickBest, 23 Oct 2026).
    public WaitingSpot Claim(Component occupant, WaitingSpot.SpotKind preferred)
    {
        WaitingSpot chosen = PickBest(occupant, preferred, true);
        if (chosen == null) chosen = PickBest(occupant, preferred, false);
        return chosen;
    }

    // THE REPAIR BAR FIRST (14 Oct 2026, NPC wave 2). His girlfriend, playtest 5: "people wouldn't sit down on the bar
    // stools that are infront of the repair station". The nearest-seat rule of the time measured from the counter place the
    // customer stands at, and from the place everyone alone uses (x +1.6) the bistro's chair was as near as the east stool
    // and the middle and west stools never won: two of the three were never used. The repair bar exists for the person
    // whose thing is on the mat (since playtest 4 Ace reassures and hands back over the counter without a step), so a
    // customer whose repair goes to the bench takes the free stool whose seat is nearest the bench, the middle one first;
    // two within 0.1 m of each other go to the one nearer the customer (east, from the counter). Null when all three are
    // taken: the caller then uses Claim, the best seat for them. Drinks, the Human fault and patrons never ask, and since
    // 23 Oct nor does a bench job whose character wants comfort more than the bar (CustomerBrain.StaysAtTheBar).
    public const float RepairBarTie = .1f;

    public WaitingSpot ClaimRepairBar(Component occupant, Vector3 benchPoint)
    {
        scratch.Clear();
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < registry.Count; i++)
        {
            if (!(registry[i] is TableSeat stool) || !stool.RepairBar || !stool.IsAvailable || !HasClaimClearance(stool, occupant)) continue;
            scratch.Add(stool);
            nearest = Mathf.Min(nearest, Flat(stool.SeatPose.position - benchPoint).magnitude);
        }
        if (scratch.Count == 0) return null;
        Vector3 from = occupant != null ? occupant.transform.position : transform.position;
        WaitingSpot pick = null;
        float pickToOccupant = float.PositiveInfinity;
        foreach (WaitingSpot s in scratch)
        {
            if (Flat(((TableSeat)s).SeatPose.position - benchPoint).magnitude > nearest + RepairBarTie) continue;
            float toOccupant = Flat(s.StandPoint.position - from).magnitude;
            if (toOccupant < pickToOccupant) { pickToOccupant = toOccupant; pick = s; }
        }
        return pick != null && pick.Claim(occupant) ? pick : null;
    }

    // THE BEST SPOT FOR THIS PERSON (23 Oct 2026; SeatChoice, in SeatTags.cs). His note: drink customers "only sit to the
    // left of the couch", repair customers "only... next to the bar stool": "I want that to be more spread out and diverse...
    // a feel for the player to like kind of look for the customer that did the order".
    //
    // Before, people took the nearest free seat (pass 2b, 27 Sept 2026), which had replaced a random free spot anywhere in
    // the room: people walking past empty chairs to one across the café, which read as automated. The nearest seat from the
    // till is the same seat every time, so a day used three or four. Now each free spot is scored for the person asking:
    // their character's leanings for its tags, a regular's favourite, a repair-bar stool kept for repairs, a seat nobody sits
    // on (waited at standing: overflow only), the strangers near it (personal space), and the walk there along the NavMesh
    // (counted less than the reasons, so it breaks ties). Spots within Nearly As Near points of the best are shared at
    // random. Nobody walks past a seat as good without a reason; a quiet café spreads out, a busy one fills in. A spot no
    // route reaches scores minus infinity (WalkingDistance): CustomerBrain hands back a spot it can't reach, and with its
    // reasons such a spot would otherwise be picked again on every retry while reachable seats stood free.
    //
    // The walk is the dear part (a route each), so it is measured best-first: a spot's score can only be lower than its
    // reasons less a straight-line walk, so once that falls short of the best real score by more than the margin, neither
    // it nor anything after it can be a near-equal. Exact for anyone standing on the NavMesh (SeatChoice.StraightSlack), and
    // usually a handful of routes (it was always the nearest 8).
    private WaitingSpot PickBest(Component occupant, WaitingSpot.SpotKind kind, bool matchKind)
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
        SeatChoice.Person who = SeatChoice.PersonOf(occupant);

        // Everyone who already has a place, sitting, standing at it or on their way there: their places, for personal space.
        others.Clear();
        for (int i = 0; i < registry.Count; i++)
        {
            WaitingSpot s = registry[i];
            if (s == null || !s.IsOccupied || s.Occupant == occupant) continue;
            others.Add(PlaceOf(s));
        }

        // Everything but the walk, and the most each spot could score (no walk is shorter than the straight line).
        reasons.Clear();
        atMost.Clear();
        order.Clear();
        for (int i = 0; i < scratch.Count; i++)
        {
            WaitingSpot s = scratch[i];
            TableSeat seat = s as TableSeat;
            var place = new SeatChoice.Seat(PlaceOf(s), seat != null ? seat.Tags : SeatTag.None, seat != null && seat.RepairBar,
                                            standing: seat != null && !seat.SnapToSeat);
            float r = SeatChoice.Reasons(who, place) - SeatChoice.Crowding(who, place.at, others);
            float straight = Mathf.Max(0f, Flat(s.StandPoint.position - from).magnitude - SeatChoice.StraightSlack);
            reasons.Add(r);
            atMost.Add(r - SeatChoice.WalkCost(who, straight));
            order.Add(i);
        }
        order.Sort(ByMost);

        float margin = Mathf.Max(0f, nearlyAsNear);
        float best = float.NegativeInfinity;
        weighed.Clear();
        scores.Clear();
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            if (atMost[i] < best - margin) break;
            float walk = WalkingDistance(from, scratch[i].StandPoint.position)
                         + onTheirWayPenalty * OnTheirWayNear(scratch[i], occupant);
            float score = reasons[i] - SeatChoice.WalkCost(who, walk);
            weighed.Add(i);
            scores.Add(score);
            if (score > best) best = score;
        }

        SeatChoice.NearlyBest(scores, margin, nearlyBest);
        WaitingSpot pick = nearlyBest.Count > 0 ? scratch[weighed[nearlyBest[Random.Range(0, nearlyBest.Count)]]] : scratch[order[0]];
        return pick.Claim(occupant) ? pick : null;
    }

    // Most first (a cached delegate, so no closure; List.Sort still wraps it in one small comparer a claim, never a frame).
    private static readonly System.Comparison<int> ByMost = (a, b) => atMost[b].CompareTo(atMost[a]);

    // Where someone at this spot is: a seat's sit point, a standing spot's stand point (as OnTheirWayNear measures).
    private static Vector3 PlaceOf(WaitingSpot spot) => spot is TableSeat seat ? seat.SeatPose.position : spot.StandPoint.position;

    // People who have claimed a spot at the same table but are not sitting in
    // it yet (still walking over): heading there too would converge on them.
    //
    // PLAYTEST 4 (12 Oct 2026; the veteran, run 2: "Only the x -3.2 stool ever took a waiting customer: 4 of 4
    // repair-bar waits; 0 at -4.2 and -5.2"). "Not in the chair yet" was read off NpcSeating.Busy alone, and at a
    // bar stool nobody ever gets into the chair (Snap To Seat off: the customer waits standing beside it), so whoever
    // held the first stool counted as still on their way for their whole wait, and the two stools beside them
    // (1.0 and 2.0 m away) carried the 2.5 m penalty the whole time. From the counter's middle and west places that
    // put the second stool (4.2 and 2.6 m) behind the nearest floor seat (the bistro's west chair at 5.0 m, the
    // communal table's at 4.9 m), which is where the recordings show people going. Someone standing on their stand
    // point has arrived, chair or no chair.
    // (23 Oct 2026: still added to the walk in PickBest, metres as before; who is near a seat at all, arrived or not, is
    // personal space now: SeatChoice.Crowding. The Waiting space check reads this method by name.)
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
            if (!HasArrived(s)) count++;   // claimed, and still on the way there
        }
        return count;
    }

    // A walk to a spot ends within arriveDistance + 0.15 m of its stand point (CustomerBrain 0.3 m, NpcLocomotion's
    // arrive radius); the repair bar's stools are 1 m apart, so 0.8 m can only mean this spot's own occupant.
    private const float ArrivedWithin = .8f;

    /// <summary>
    /// Whether the spot's occupant has got there: in (or getting into) the chair (NpcSeating), or standing on the
    /// stand point, which is how a bar stool, a standing spot and anyone without a seating component are waited at.
    /// </summary>
    public static bool HasArrived(WaitingSpot spot)
    {
        if (spot == null || spot.Occupant == null) return false;
        NpcSeating seating = spot.Occupant.GetComponent<NpcSeating>();
        if (seating != null && seating.Busy) return true;
        return Flat(spot.Occupant.transform.position - spot.StandPoint.position).sqrMagnitude <= ArrivedWithin * ArrivedWithin;
    }

    // Metres along the floor. With a NavMesh under the claimer and no complete route there, infinite (23 Oct 2026): the spot
    // then scores minus infinity, so it is never the best while any spot can be reached (CustomerBrain.CanReach hands back a
    // spot it can't walk to, and the retry a second later would pick the same one again). With no NavMesh near the claimer
    // at all (Edit Mode with none loaded, a check's preview scene), the pessimistic straight line, as before.
    private static float WalkingDistance(Vector3 from, Vector3 to)
    {
        if (route == null) route = new NavMeshPath();
        if (NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
        {
            if (NavMesh.CalculatePath(start.position, to, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete)
            {
                int count = route.GetCornersNonAlloc(corners);
                float length = 0f;
                for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                return length;
            }
            return float.PositiveInfinity;
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
