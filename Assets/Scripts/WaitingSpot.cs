using UnityEngine;

// A claimable place for a customer to stand once you've taken their job.
//
// Before this existed, an accepted customer kept their counter slot until they
// left, so three drink orders could lock the repair queue out completely. Now
// the counter slot is only for the conversation, and this is where they go
// afterwards.
//
// STEP 2 CHANGE — spots now register themselves with WaitingArea.
//
// They used to be found with GetComponentsInChildren, which meant every spot
// had to be parented under the WaitingArea object. That was fine while spots
// were six loose transforms, but a TableSeat belongs to its TABLE, not to a
// manager on the other side of the hierarchy. Self-registration means the
// hierarchy stops mattering — and a table bought as an upgrade and dropped in
// mid-day registers itself with no extra wiring.
public class WaitingSpot : MonoBehaviour
{
    public enum SpotKind { Loiter, Seat, Browse }

    // protected rather than private so TableSeat can force itself to Seat.
    // Serialization is unaffected — the Inspector and any SerializedObject
    // lookups by name ("kind", "drainMultiplier") work exactly as before.
    [SerializeField] protected SpotKind kind = SpotKind.Loiter;

    [Tooltip("Where the customer actually stands. Must be on the NavMesh. " +
             "Leave empty to use this object's own position.")]
    [SerializeField] protected Transform standPoint;

    [Tooltip("How fast patience drains while waiting here. 1 = normal. " +
             "Seats are calmer (0.6), loitering worse (1.15).")]
    [SerializeField] protected float drainMultiplier = 1f;

    public SpotKind Kind => kind;
    public float DrainMultiplier => drainMultiplier;
    public Transform StandPoint => standPoint != null ? standPoint : transform;

    public CustomerBrain Occupant { get; private set; }
    public bool IsOccupied => Occupant != null;

    // Virtual because a TableSeat is also unavailable while it has a dirty cup
    // sitting on it — that's what gives bussing its teeth in step 7.
    public virtual bool IsAvailable => Occupant == null;

    // Registration is tied to enable/disable rather than Awake/OnDestroy on
    // purpose: switching a spot off in the Inspector is now the supported way
    // to take it out of service, and it comes straight back when you tick it
    // on again. That's how step 2 retires the two far loiter spots without
    // deleting anything you might want back.
    protected virtual void OnEnable()
    {
        WaitingArea.Register(this);
    }

    protected virtual void OnDisable()
    {
        WaitingArea.Unregister(this);

        // Don't strand whoever was standing here. They'll ask WaitingArea for
        // somewhere else on their next tick.
        Occupant = null;
    }

    public bool Claim(CustomerBrain customer)
    {
        if (customer == null || !IsAvailable) return false;
        Occupant = customer;
        return true;
    }

    public void Release(CustomerBrain customer)
    {
        if (Occupant == customer) Occupant = null;
    }

    // Shows in the Scene view so you can see where people will stand and
    // which way they'll face without pressing play.
    protected virtual void OnDrawGizmos()
    {
        Transform p = StandPoint;
        Gizmos.color = IsOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(p.position, 0.35f);
        Gizmos.DrawRay(p.position, p.forward * 0.6f);
    }
}
