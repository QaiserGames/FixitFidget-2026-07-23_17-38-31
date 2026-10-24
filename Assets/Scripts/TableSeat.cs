using UnityEngine;

// One seat at a café table.
//
// WHY THIS IS SO SHORT
//
// The architecture spec called for a TableSeat *and* a SeatingArea that would
// hand seats out. That second file is now deleted from the plan: WaitingArea
// already does claim/release with a kind preference and a fallback, and a
// second manager doing the same thing with a different name is just two
// things to keep in sync.
//
// So a seat is a WaitingSpot that happens to be a Seat, knows where its cup
// goes, and can be blocked by a dirty one. Everything else it inherits.
//
// CustomerBrain needed ZERO changes for this. DrainRate already reads
// waitingSpot.DrainMultiplier, and CustomerIdentity.PreferredWaitKind already
// defaults to Seat — so people start sitting down the moment seats exist.
//
// 23 Oct 2026: a seat also says what it is (Tags: window, soft, a bar stool, the
// communal table, a quiet corner, by the door), for the way people choose seats
// (SeatChoice, in SeatTags.cs). The tags are not kept here: they come from a
// SeatTags on the seat or its furniture, else the seat's own shape, plus the
// room's areas it stands in, so a seat placed anywhere needs no setting up.
public class TableSeat : WaitingSpot
{
    /// <summary>
    /// How a person gets onto this seat. A Chair is stepped round (beside it,
    /// then in front, then turn and sit). A Bench - a sofa or banquette - is
    /// walked up to from the front: step in, turn round, sit back.
    /// </summary>
    public enum SitStyle { Chair, Bench }

    [Header("Table seat")]

    [Tooltip("Where a drink is set down on the table for this seat. Used by " +
             "the cup lifecycle in step 7. Leave empty and it falls back to " +
             "this object's position.")]
    [SerializeField] private Transform cupSpot;

    [Header("Sitting")]

    [Tooltip("ON: customers and patrons with an NpcSeating component sit down " +
             "on this chair (sit-down, seated idle, stand-up clips baked by " +
             "Fixit Fidget > NPC > Sit 1). OFF: they stand at the Stand Point " +
             "beside the chair facing the table, as in the grey-box, so a seat " +
             "chooser counts it as a standing wait, overflow only (SeatChoice.StandingWait).")]
    [SerializeField] private bool snapToSeat = false;

    [Tooltip("The chair's seat surface, at its centre. The sitting NPC's hips " +
             "come down here; which way they face comes from the Cup Spot.")]
    [SerializeField] private Transform seatPose;

    [Tooltip("Chair: stepped round from the side. Bench (sofa, banquette): walked up to from the front, " +
             "turn, sit back. Pass 2.")]
    [SerializeField] private SitStyle sitStyle = SitStyle.Chair;

    [Tooltip("Optional. Which way the seated person faces (a point in the room). Empty = towards the Cup Spot, " +
             "which is wrong for a sofa whose cup goes down beside the sitter.")]
    [SerializeField] private Transform faceTowards;

    [Tooltip("Optional. Room reserved round the stand point when other people claim nearby spots, metres. " +
             "0 = the agent's own radius plus stopping distance (chairs). Bench seats sit 0.7 m apart, so they " +
             "use a smaller footprint - the body leaves the stand point as soon as it sits.")]
    [SerializeField, Min(0f)] private float clearanceRadius = 0f;

    // THE REPAIR BAR (14 Oct 2026, NPC wave 2). His girlfriend, playtest 5: "people wouldn't sit down on the bar stools
    // that are infront of the repair station". Nobody could sit on them (Snap To Seat off: the sit clips are fitted to
    // the 0.45 m chairs, and NpcSeating lifted a body at most 0.12 m), and the nearest-seat rule almost never reached the
    // middle or west stool. Now a customer whose repair goes to the bench takes the free stool nearest the bench first
    // (WaitingArea.ClaimRepairBar), and sits on it: the stool and its counter are a café chair and table raised 0.30 m
    // together, so the chair clips fit once the body is lifted with them. All three default to "as before". (23 Oct: a bench
    // job whose character wants comfort more than the bar sits elsewhere, CustomerBrain.StaysAtTheBar, and everyone else
    // treats these stools as kept for repairs: SeatChoice.KeptForRepairs.)
    [Header("The repair bar (NPC wave 2)")]

    [Tooltip("A repair-bar stool: a customer whose repair goes to the bench takes the free one nearest the bench first " +
             "(WaitingArea.ClaimRepairBar). Off: an ordinary seat, chosen like any other (SeatChoice).")]
    [SerializeField] private bool repairBar;

    [Tooltip("How far the body may rise (or drop) to meet this seat, metres. 0 = NpcSeating's own limit (0.12 m, the " +
             "café's chairs). A seat with its own limit is met inside the sit clips: the walk ends on the floor, the body " +
             "rises during Sit Down and comes down during Stand Up. The repair-bar stools: 0.32.")]
    [SerializeField, Min(0f)] private float maxLift;

    [Tooltip("How far beside the seat's centre line the way in and out passes, metres. 0 = NpcSeating's own (0.62). " +
             "The repair-bar stools stand 1.0 m apart: 0.50 passes between them without brushing the next stool.")]
    [SerializeField, Min(0f)] private float approachSide;

    // What's sitting on the table for this seat. A GameObject rather than a
    // DrinkJob on purpose: step 7 hasn't decided what a dirty cup IS yet, and
    // this shouldn't need rewriting when it does.
    private GameObject dirtyCup;

    public Transform CupSpot => cupSpot != null ? cupSpot : transform;
    public Transform SeatPose => seatPose != null ? seatPose : StandPoint;
    public bool SnapToSeat => snapToSeat;
    public SitStyle Style => sitStyle;
    /// <summary>What the seated person faces: the Face Towards point if set, else the cup spot.</summary>
    public Vector3 FacingPoint => faceTowards != null ? faceTowards.position : CupSpot.position;
    /// <summary>Footprint kept clear round the stand point for other claims; 0 = default (see WaitingArea).</summary>
    public override float ClearanceRadius => clearanceRadius;
    /// <summary>A repair-bar stool: bench-job customers take these first (WaitingArea.ClaimRepairBar).</summary>
    public bool RepairBar => repairBar;
    /// <summary>The body's own lift limit for this seat, metres; 0 = NpcSeating's default. Above 0 the lift happens inside the sit clips.</summary>
    public float MaxLift => maxLift;
    /// <summary>How far beside the seat the way in and out passes, metres; 0 = NpcSeating's default.</summary>
    public float ApproachSide => approachSide;

    /// <summary>
    /// What this seat is and where it stands (23 Oct 2026): a SeatTags on it or a parent, else its own shape (a raised seat is
    /// at a bar, a bench seat is soft), plus every room area its sit point is in (SeatTags.Of). Read by SeatChoice.
    /// </summary>
    public SeatTag Tags => SeatTags.Of(this);

    /// <summary>The zone it belongs to, for the labs and the reports only (never for choosing): its SeatTags' zone, else its parent's name.</summary>
    public string Zone => SeatTags.ZoneOf(this);

    public bool IsDirty => dirtyCup != null;

    /// <summary>Whether <paramref name="cup"/> is what makes it dirty (a cup going mustn't clean up after a newer one).</summary>
    public bool IsDirtyWith(GameObject cup) => dirtyCup != null && cup != null && dirtyCup == cup;

    // THE LINE THAT MAKES BUSSING A MECHANIC.
    //
    // A dirty seat is not available, so it can't be claimed, so the next
    // customer who wanted to sit falls through to a loiter spot and drains
    // ~2x faster. Leave enough cups out and the whole room sours. Clearing
    // them is the pressure-release valve.
    public override bool IsAvailable => Occupant == null && !IsDirty;

    public void SetDirty(GameObject cup)
    {
        dirtyCup = cup;
    }

    public void Clean()
    {
        dirtyCup = null;
    }

    // A TableSeat is always a Seat. Forcing it here rather than trusting the
    // Inspector because "the kind field is set wrong" is invisible until you
    // notice nobody ever sits, and by then you're debugging the wrong file.
    private void Reset()
    {
        kind = SpotKind.Seat;
        drainMultiplier = 0.6f;
    }

    private void OnValidate()
    {
        if (kind != SpotKind.Seat) kind = SpotKind.Seat;
    }

    protected override void OnDrawGizmos()
    {
        Transform p = StandPoint;

        // Dirty reads as a distinct state at a glance — you should be able to
        // see why nobody is sitting without opening the Inspector.
        if (IsDirty) Gizmos.color = new Color(0.8f, 0.5f, 0.1f);
        else if (IsOccupied) Gizmos.color = Color.red;
        else Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(p.position, 0.35f);
        Gizmos.DrawRay(p.position, p.forward * 0.6f);

        // The cup spot, so you can see it's actually on the tabletop and not
        // floating above it or buried in the wood.
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawWireCube(CupSpot.position, new Vector3(0.09f, 0.1f, 0.09f));
    }
}
