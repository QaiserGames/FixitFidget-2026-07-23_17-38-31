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

    public void ReleaseSlot(CustomerBrain customer)
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == customer)
                occupants[i] = null;

        // Shuffle everyone forward so there's never a gap in the line.
        bool moved = true;
        while (moved)
        {
            moved = false;
            for (int i = 0; i < occupants.Length - 1; i++)
            {
                if (occupants[i] == null && occupants[i + 1] != null)
                {
                    occupants[i] = occupants[i + 1];
                    occupants[i + 1] = null;
                    occupants[i].MoveToSlot(i);
                    moved = true;
                }
            }
        }
    }
}
