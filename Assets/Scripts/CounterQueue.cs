using UnityEngine;

public class CounterQueue : MonoBehaviour
{
    [SerializeField] private Transform[] slots;
    [SerializeField] private Transform[] itemSpots;

    private CustomerBrain[] occupants;

    private void Awake()
    {
        occupants = new CustomerBrain[slots.Length];
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
    public Transform ItemSpot(int index) => itemSpots[index];

    public void ReleaseSlot(CustomerBrain customer)
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == customer)
                occupants[i] = null;

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