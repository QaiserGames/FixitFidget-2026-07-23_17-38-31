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
public class TableSeat : WaitingSpot
{
    [Header("Table seat")]

    [Tooltip("Where a drink is set down on the table for this seat. Used by " +
             "the cup lifecycle in step 7. Leave empty and it falls back to " +
             "this object's position.")]
    [SerializeField] private Transform cupSpot;

    [Header("Sitting (grey-box)")]

    [Tooltip("OFF until a sit animation exists. While off, the customer walks " +
             "to the Stand Point beside the chair and stands there facing the " +
             "table — which reads fine. Turning it on before you have the " +
             "animation makes them stand THROUGH the chair, which does not.")]
    [SerializeField] private bool snapToSeat = false;

    [Tooltip("Where the body goes once snapToSeat is on — the chair's seat " +
             "surface, facing the table.")]
    [SerializeField] private Transform seatPose;

    // What's sitting on the table for this seat. A GameObject rather than a
    // DrinkJob on purpose: step 7 hasn't decided what a dirty cup IS yet, and
    // this shouldn't need rewriting when it does.
    private GameObject dirtyCup;

    public Transform CupSpot => cupSpot != null ? cupSpot : transform;
    public Transform SeatPose => seatPose != null ? seatPose : StandPoint;
    public bool SnapToSeat => snapToSeat;

    public bool IsDirty => dirtyCup != null;

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
