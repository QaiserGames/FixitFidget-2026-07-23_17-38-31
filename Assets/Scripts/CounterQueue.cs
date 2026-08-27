using UnityEngine;

// The line at the counter. This is now ONLY for the intake conversation —
// people claim a slot to be heard, and release it the moment you accept or
// decline. Where they wait afterwards is WaitingArea's problem, and where
// their device goes is IntakeShelf's.
//
// The old itemSpots array is gone. Devices used to spawn at "their customer's"
// counter spot and be returned there by slot index, which stops making sense
// once the customer walks away.
public class CounterQueue : MonoBehaviour
{
    [SerializeField] private Transform[] slots;

    private CustomerBrain[] occupants;

    public int SlotCount => slots != null ? slots.Length : 0;

    private void Awake()
    {
        occupants = new CustomerBrain[SlotCount];
    }

    // Asked by CustomerSpawner BEFORE it creates anyone.
    //
    // THE BUG THIS FIXES: the spawner only ever checked maxCustomers. Someone
    // who arrived with the line full got slotIndex -1 from ClaimSlot, turned
    // straight around and walked back out — and because that path skips
    // Depart(), they weren't even counted as Lost. Invisible customer loss.
    // Raising maxCustomers from 3 to 6 made it happen constantly.
    public bool HasFreeSlot
    {
        get
        {
            if (occupants == null) return false;
            foreach (CustomerBrain c in occupants)
                if (c == null) return true;
            return false;
        }
    }

    public int ClaimSlot(CustomerBrain customer)
    {
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null)
            {
                occupants[i] = customer;
                return i;
            }
        }
        return -1;
    }

    public Transform SlotPoint(int index) => slots[index];

    // Clear the slot and stop. Nobody is asked to move.
    //
    // WHAT USED TO HAPPEN, AND WHY IT'S GONE
    //
    // This used to compact the array toward index 0 and call MoveToSlot on
    // everyone it shifted, so the line always closed up. Two things wrong with
    // that:
    //
    // 1. It read like a school canteen. Serving the person on the right made
    //    everyone else slide left, which is what a queue under pressure does,
    //    not what people standing at a café counter do.
    //
    // 2. It was the source of the queue freeze. The shuffle handed a slot to
    //    someone who then had to physically walk into a space the previous
    //    occupant hadn't left yet — and a parked NavMeshAgent (isStopped) can't
    //    be pushed, so they'd lean on each other until one finally walked off.
    //
    // ClaimSlot already returns the first free index, so a gap is filled by the
    // next person through the door instead. The space closes from the entrance,
    // which is both what you'd actually see and one less thing to go wrong.
    //
    // CustomerBrain.MoveToSlot is deliberately left in place. Nothing calls it
    // now, but a bigger shop with a genuine single-file line would want it back.
    public void ReleaseSlot(CustomerBrain customer)
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == customer)
                occupants[i] = null;
    }
}
