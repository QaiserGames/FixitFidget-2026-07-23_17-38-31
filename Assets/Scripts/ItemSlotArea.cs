using UnityEngine;

public class ItemSlotArea : MonoBehaviour
{
    [SerializeField] private Transform[] slots;

    private JobBase[] occupants;

    private void Awake()
    {
        occupants = new JobBase[slots.Length];
    }

    public bool HasFreeSlot
    {
        get
        {
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == null) return true;
            return false;
        }
    }

    // Take the first free slot. Returns null if the surface is full.
    public Transform ClaimSlot(JobBase item)
    {
        // Already on this surface? Keep the same spot.
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == item) return slots[i];

        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null)
            {
                occupants[i] = item;
                return slots[i];
            }
        }
        return null;
    }

    public void ReleaseSlot(JobBase item)
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == item) occupants[i] = null;
    }

    // Housekeeping: clear slots whose item was destroyed.
    private void Update()
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == null) occupants[i] = null;
    }
}