using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// A front door on the street that really opens (27 Sept; Mansoor: "lets now
// add doors for these houses. so we can see them leave and enter more
// believably").
//
// Built by Fixit Fidget > Night > Doors 2 (StreetDoorSteps): the house's own
// door leaf, glass, panel and knob on a hinge, a real doorway cut into the
// front wall and a short dark hall behind it. This component swings the leaf.
//
// People open it; nobody else does. CafeArrivals holds a door open while
// someone walking to or from it is in its hall or on its doorstep, and lets
// go once they are clear or inside: the door then closes by itself. Someone
// coming out waits in the dark hall until the door is open (NpcJourney gate);
// someone going in stands in the hall while it closes behind them, and only
// then leaves the game.
//
// Markers (children, not turned by the hinge):
//   Doorway: the middle of the opening at floor level, in the door's plane;
//   Hall:    where people stand inside, out of sight, before and after;
//   Wait here 1..n: free spots on the pavement near the door where people on
//            their way in wait for their turn (Fixit Fidget > Night > Doors 4).
// The door's forward (blue axis) faces the street.
//
// TAKING TURNS (27 Sept, the door jams; Mansoor: "they just get stuck and then
// that causes everyother npc to also get stuck")
// A doorway fits one person, and so does the stoop (and at the shop, the path past
// its bench and bike racks). So the way through is used one way at a time. People
// going the same way may follow each other through, a walking gap apart. When
// people wait on both sides, the sides take turns, a few people at a time
// (turnBatch), so neither side is ever locked out. Someone coming out waits
// inside the house, unseen and in nobody's way, until it is their turn; someone
// going in waits on one of the door's waiting spots (NpcJourney walks them). A
// walker asks every frame it wants or is using the way through; a ticket nobody
// has asked about for a moment is forgotten, so nobody can keep the way for good.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class StreetDoor : MonoBehaviour
{
    [Tooltip("The part that swings (the door leaf and what's on it). Turns about its own up axis.")]
    public Transform hinge;
    [Tooltip("Degrees the door swings in when fully open (inward, away from the street).")]
    [Range(30f, 110f)] public float openAngle = 88f;
    [Tooltip("Seconds from closed to fully open.")]
    [Range(.1f, 2f)] public float openSeconds = .35f;
    [Tooltip("Seconds from fully open to closed.")]
    [Range(.1f, 2f)] public float closeSeconds = .6f;
    [Tooltip("The middle of the doorway at floor level, in the door's plane.")]
    public Transform doorway;
    [Tooltip("Where people stand inside, out of sight, before coming out and after going in.")]
    public Transform hall;
    [Tooltip("Free spots on the pavement where people on their way in wait for their turn, best first (Doors 4 marks them).")]
    public Transform[] waitSpots = System.Array.Empty<Transform>();
    [Tooltip("With people waiting on both sides, this many go through one way before it is the other side's turn.")]
    [Range(1, 6)] public int turnBatch = 3;
    [Tooltip("Seconds between two people starting through the same way: a walking gap.")]
    [Range(.3f, 2f)] public float followGap = 1f;

    // Editor record (Fixit Fidget > Night > Doors - Put the old doors back): the
    // building's merged meshes the doorway was cut into, and what they were before.
    [HideInInspector] public MeshFilter[] cutParts = System.Array.Empty<MeshFilter>();
    [HideInInspector] public Mesh[] originalMeshes = System.Array.Empty<Mesh>();

    private static readonly List<StreetDoor> doors = new();
    private readonly Dictionary<Object, float> holds = new();
    private readonly List<Object> expired = new();
    private Quaternion closedRotation = Quaternion.identity;
    private bool hasClosedRotation;
    private float amount;       // 0 closed .. 1 fully open

    public static IReadOnlyList<StreetDoor> All => doors;

    /// <summary>0 when closed, 1 when fully open.</summary>
    public float OpenAmount => amount;
    /// <summary>Open far enough to walk through.</summary>
    public bool IsOpen => amount >= .9f;
    public bool IsClosed => amount <= .001f;
    /// <summary>Anyone holding it open right now.</summary>
    public bool Held => holds.Count > 0;

    public Vector3 DoorwayPoint => doorway != null ? doorway.position : transform.position;
    public Vector3 HallPoint => hall != null ? hall.position : transform.position - transform.forward;
    /// <summary>Deeper inside, behind the dark hall's back wall: where people wait, unseen, for their turn to come out.</summary>
    public Vector3 InsidePoint => HallPoint - Outward * InsideBeyondHall;
    private const float InsideBeyondHall = .9f;
    /// <summary>Towards the street, flat.</summary>
    public Vector3 Outward
    {
        get { Vector3 f = transform.forward; f.y = 0f; return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward; }
    }

    /// <summary>
    /// Metres in front of the door's plane (negative: inside the doorway or the hall).
    /// </summary>
    public float Outside(Vector3 point) => Vector3.Dot(point - DoorwayPoint, Outward);

    /// <summary>Keeps the door open for <paramref name="who"/> for the next few seconds (call again to keep it).</summary>
    public void Hold(Object who, float seconds = .3f)
    {
        if (who == null) return;
        holds[who] = Time.time + Mathf.Max(0f, seconds);
    }

    /// <summary><paramref name="who"/> no longer needs the door; it closes once nobody does.</summary>
    public void Release(Object who)
    {
        if (who != null) holds.Remove(who);
    }

    /// <summary>The door whose doorway is nearest <paramref name="point"/>, within <paramref name="within"/> metres; null if none.</summary>
    public static StreetDoor Near(Vector3 point, float within)
    {
        StreetDoor best = null;
        float bestSq = within * within;
        foreach (StreetDoor door in doors)
        {
            if (door == null || !door.isActiveAndEnabled) continue;
            Vector3 d = door.DoorwayPoint - point;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq <= bestSq) { bestSq = sq; best = door; }
        }
        return best;
    }

    private void Awake() => RememberClosed();

    private void RememberClosed()
    {
        if (hasClosedRotation || hinge == null) return;
        closedRotation = hinge.localRotation;
        hasClosedRotation = true;
    }

    private void OnEnable() { if (!doors.Contains(this)) doors.Add(this); }

    private void OnDisable()
    {
        doors.Remove(this);
        holds.Clear();
        tickets.Clear();
        amount = 0f;
        Pose();
    }

    // ------------------------------------------------------------ taking turns

    public enum Way { Out, In }   // Out: from inside onto the street; In: from the street inside

    private sealed class Ticket
    {
        public Object who;
        public Way way;
        public float asked, seen;
        public bool admitted;
        public int spot = -1;       // a waiting spot claimed while waiting to go in
    }

    private readonly List<Ticket> tickets = new();
    private Way flow = Way.Out;
    private int streak;             // admitted in a row the way things are flowing
    private float lastAdmit = -100f;
    private int solvedFrame = -1;
    private const float ForgetAfter = .75f;

    /// <summary>
    /// <paramref name="who"/> wants to go through <paramref name="way"/>, or is on the way through.
    /// Call every frame for as long as that is so.
    /// </summary>
    public void Ask(Object who, Way way)
    {
        if (who == null) return;
        Ticket t = Find(who);
        if (t == null) tickets.Add(new Ticket { who = who, way = way, asked = Time.time, seen = Time.time });
        else
        {
            if (t.way != way && !t.admitted) { t.way = way; t.asked = Time.time; }
            t.seen = Time.time;
        }
    }

    /// <summary>True once it is <paramref name="who"/>'s turn, until they <see cref="Leave"/>.</summary>
    public bool MayGo(Object who)
    {
        Solve();
        Ticket t = Find(who);
        return t != null && t.admitted;
    }

    /// <summary><paramref name="who"/> is through, home, or gone another way: the next person may go.</summary>
    public void Leave(Object who)
    {
        for (int i = tickets.Count - 1; i >= 0; i--)
            if (tickets[i].who == who || tickets[i].who == null) tickets.RemoveAt(i);
    }

    /// <summary>
    /// The waiting spot <paramref name="who"/> should stand on while waiting to go in (the best free
    /// one, kept until their turn), or -1 when every spot is taken.
    /// </summary>
    public int WaitSpotFor(Object who)
    {
        Ticket t = Find(who);
        if (t == null || t.admitted || t.way != Way.In) return -1;
        if (t.spot >= 0 && t.spot < waitSpots.Length && waitSpots[t.spot] != null) return t.spot;
        t.spot = -1;
        for (int i = 0; i < waitSpots.Length; i++)
        {
            if (waitSpots[i] == null) continue;
            bool taken = false;
            foreach (Ticket other in tickets) if (other != t && !other.admitted && other.spot == i) { taken = true; break; }
            if (!taken) { t.spot = i; break; }
        }
        return t.spot;
    }

    public Vector3 WaitSpotPoint(int spot) => spot >= 0 && spot < waitSpots.Length && waitSpots[spot] != null ? waitSpots[spot].position : DoorwayPoint;

    /// <summary>How many waiting spots are marked at this door.</summary>
    public int MarkedSpots
    {
        get
        {
            int count = 0;
            foreach (Transform spot in waitSpots) if (spot != null) count++;
            return count;
        }
    }

    /// <summary>0 for the first of those waiting to go <paramref name="who"/>'s way, 1 for the next…; -1 if not waiting.</summary>
    public int PlaceInLine(Object who)
    {
        Ticket t = Find(who);
        if (t == null || t.admitted) return -1;
        int place = 0;
        foreach (Ticket other in tickets)
        {
            if (other == t) break;
            if (!other.admitted && other.way == t.way) place++;
        }
        return place;
    }

    /// <summary>Anyone other than <paramref name="who"/> waiting for the way through or on it.</summary>
    public bool OthersUsing(Object who)
    {
        Purge();
        foreach (Ticket t in tickets) if (t.who != who) return true;
        return false;
    }

    /// <summary>How many are waiting to go <paramref name="way"/> (not yet their turn).</summary>
    public int Waiting(Way way)
    {
        Purge();
        int count = 0;
        foreach (Ticket t in tickets) if (!t.admitted && t.way == way) count++;
        return count;
    }

    /// <summary>How many it is the turn of right now (on the way through).</summary>
    public int Passing
    {
        get
        {
            Purge();
            int count = 0;
            foreach (Ticket t in tickets) if (t.admitted) count++;
            return count;
        }
    }

    /// <summary>For traces: who is going through which way, and who is waiting.</summary>
    public string TurnState => $"{Passing} going {(flow == Way.Out ? "out" : "in")}, waiting {Waiting(Way.Out)} out {Waiting(Way.In)} in";

    private Ticket Find(Object who)
    {
        foreach (Ticket t in tickets) if (t.who == who) return t;
        return null;
    }

    private void Purge()
    {
        float now = Time.time;
        for (int i = tickets.Count - 1; i >= 0; i--)
            if (tickets[i].who == null || now - tickets[i].seen > ForgetAfter) tickets.RemoveAt(i);
    }

    private Ticket FirstWaiting(Way way)
    {
        Ticket first = null;
        foreach (Ticket t in tickets)
            if (!t.admitted && t.way == way && (first == null || t.asked < first.asked)) first = t;
        return first;
    }

    // Whose turn is it? Worked out at most once a frame, when someone asks.
    private void Solve()
    {
        if (solvedFrame == Time.frameCount) return;
        solvedFrame = Time.frameCount;
        Purge();
        bool anyoneThrough = false;
        foreach (Ticket t in tickets) if (t.admitted) { anyoneThrough = true; break; }
        Ticket nextOut = FirstWaiting(Way.Out), nextIn = FirstWaiting(Way.In);
        if (nextOut == null && nextIn == null) return;
        if (!anyoneThrough)
        {
            // Nobody on the way through: whoever has waited longest goes - unless their side
            // has just had its turn while the other side waited.
            Ticket pick = nextOut == null ? nextIn : nextIn == null ? nextOut : nextOut.asked <= nextIn.asked ? nextOut : nextIn;
            Ticket other = pick == nextOut ? nextIn : nextOut;
            if (other != null && pick.way == flow && streak >= turnBatch) pick = other;
            Admit(pick);
            return;
        }
        // People are on the way through: only the same way may follow, a walking gap behind,
        // and only until the other side has waited through a whole batch.
        Ticket same = flow == Way.Out ? nextOut : nextIn;
        Ticket opposite = flow == Way.Out ? nextIn : nextOut;
        if (same == null || Time.time - lastAdmit < followGap) return;
        if (opposite != null && streak >= turnBatch) return;
        Admit(same);
    }

    private void Admit(Ticket t)
    {
        if (t.way != flow) { flow = t.way; streak = 0; }
        streak++;
        t.admitted = true;
        t.spot = -1;
        lastAdmit = Time.time;
    }

    private void Update()
    {
        if (hinge == null) return;
        RememberClosed();
        float now = Time.time;
        expired.Clear();
        foreach (KeyValuePair<Object, float> hold in holds)
            if (hold.Key == null || hold.Value < now) expired.Add(hold.Key);
        foreach (Object who in expired) holds.Remove(who);

        float target = holds.Count > 0 ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;
        float seconds = target > amount ? openSeconds : closeSeconds;
        amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.01f, seconds));
        Pose();
    }

    private void Pose()
    {
        if (hinge == null || !hasClosedRotation) return;
        // Quick near the frame, slow near fully open: opening is a push that swings on
        // and slows; closing starts gently and ends with the door shutting.
        float t = amount;
        float eased = 1f - (1f - t) * (1f - t);
        hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * eased, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.3f, .8f, 1f, .9f);
        Vector3 d = DoorwayPoint, h = HallPoint;
        Gizmos.DrawWireSphere(d, .08f);
        Gizmos.DrawWireSphere(h, .12f);
        Gizmos.DrawLine(d, h);
        Gizmos.DrawLine(d, d + Outward * 1.5f);
        Gizmos.color = new Color(1f, .75f, .2f, .9f);
        foreach (Transform spot in waitSpots)
            if (spot != null) Gizmos.DrawWireSphere(spot.position + Vector3.up * .05f, .28f);
    }
}
