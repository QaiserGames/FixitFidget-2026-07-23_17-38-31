using UnityEngine;

// A claimable place for a customer to stand once you've taken their job.
//
// Before this existed, an accepted customer kept their counter slot until they
// left, so three drink orders could lock the repair queue out completely. Now
// the counter slot is only for the conversation, and this is where they go
// afterwards.
//
// Step 1 only places Loiter spots. TableSeat extends this in step 2 and adds
// the "a dirty cup is on it" case to IsAvailable.
public class WaitingSpot : MonoBehaviour
{
    public enum SpotKind { Loiter, Seat, Browse }

    [SerializeField] private SpotKind kind = SpotKind.Loiter;

    [Tooltip("Where the customer actually stands. Must be on the NavMesh. " +
             "Leave empty to use this object's own position.")]
    [SerializeField] private Transform standPoint;

    [Tooltip("How fast patience drains while waiting here. 1 = normal. " +
             "Seats will be calmer (0.6), loitering worse (1.15).")]
    [SerializeField] private float drainMultiplier = 1f;

    public SpotKind Kind => kind;
    public float DrainMultiplier => drainMultiplier;
    public Transform StandPoint => standPoint != null ? standPoint : transform;

    public CustomerBrain Occupant { get; private set; }
    public bool IsOccupied => Occupant != null;

    // Virtual because a TableSeat is also unavailable while it has a dirty cup
    // sitting on it — that's what will give bussing its teeth in step 7.
    public virtual bool IsAvailable => Occupant == null;

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
    private void OnDrawGizmos()
    {
        Transform p = StandPoint;
        Gizmos.color = IsOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(p.position, 0.35f);
        Gizmos.DrawRay(p.position, p.forward * 0.6f);
    }
}
