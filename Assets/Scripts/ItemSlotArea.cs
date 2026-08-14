using UnityEngine;

public class ItemSlotArea : MonoBehaviour
{
    [SerializeField] private Transform[] slots;
    [Tooltip("Slots available before upgrades. Extra slots unlock with bench capacity.")]
    [SerializeField] private int baseSlots = 2;

    private JobBase[] occupants;

    private void Awake()
    {
        occupants = new JobBase[slots.Length];
    }

    // Upgrades unlock slots beyond the base count.
    private int UsableSlots
    {
        get
        {
            int extra = UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraBenchSlots : 0;
            return Mathf.Min(baseSlots + extra, slots.Length);
        }
    }

    public bool HasFreeSlot
    {
        get
        {
            for (int i = 0; i < UsableSlots; i++)
                if (occupants[i] == null) return true;
            return false;
        }
    }

    public bool Holds(JobBase item)
    {
        foreach (JobBase o in occupants)
            if (o == item) return true;
        return false;
    }

    public Transform ClaimSlot(JobBase item)
    {
        for (int i = 0; i < UsableSlots; i++)
            if (occupants[i] == item) return slots[i];

        for (int i = 0; i < UsableSlots; i++)
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
}