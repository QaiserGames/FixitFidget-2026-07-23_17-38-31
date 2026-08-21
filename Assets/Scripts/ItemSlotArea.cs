using UnityEngine;

public class ItemSlotArea : MonoBehaviour
{
    // Which upgrade, if any, unlocks extra slots here. The bench and the
    // intake shelf both use this component but grow on different upgrades.
    public enum CapacitySource { Bench, Shelf, Fixed }

    [SerializeField] private Transform[] slots;

    [Tooltip("Slots available before upgrades. Extra slots unlock from the source below.")]
    [SerializeField] private int baseSlots = 2;

    [Tooltip("Bench = grows with bench capacity. Shelf = grows with shelf capacity. " +
             "Fixed = never grows.")]
    [SerializeField] private CapacitySource capacitySource = CapacitySource.Bench;

    private JobBase[] occupants;

    // Built on demand rather than in Awake(), because three separate things
    // can ask about slots before Awake() has ever run: editor tooling in Edit
    // mode, a domain reload after a script recompile (which wipes non-
    // serialized fields WITHOUT re-running Awake), and changing the slots
    // array in the Inspector while playing. Any of those used to throw.
    private JobBase[] Occupants
    {
        get
        {
            int n = slots != null ? slots.Length : 0;

            if (occupants == null || occupants.Length != n)
            {
                JobBase[] resized = new JobBase[n];
                if (occupants != null)
                    for (int i = 0; i < Mathf.Min(occupants.Length, n); i++)
                        resized[i] = occupants[i];
                occupants = resized;
            }

            return occupants;
        }
    }

    private int ExtraSlots
    {
        get
        {
            if (UpgradeManager.Instance == null) return 0;

            if (capacitySource == CapacitySource.Bench) return UpgradeManager.Instance.ExtraBenchSlots;
            if (capacitySource == CapacitySource.Shelf) return UpgradeManager.Instance.ExtraShelfSlots;
            return 0;
        }
    }

    private int SlotCount => slots != null ? slots.Length : 0;

    private int UsableSlots => Mathf.Clamp(baseSlots + ExtraSlots, 0, SlotCount);

    public bool HasFreeSlot
    {
        get
        {
            JobBase[] o = Occupants;
            for (int i = 0; i < UsableSlots; i++)
                if (o[i] == null) return true;
            return false;
        }
    }

    public bool Holds(JobBase item)
    {
        foreach (JobBase o in Occupants)
            if (o == item) return true;
        return false;
    }

    public Transform ClaimSlot(JobBase item)
    {
        JobBase[] o = Occupants;

        for (int i = 0; i < UsableSlots; i++)
            if (o[i] == item) return slots[i];

        for (int i = 0; i < UsableSlots; i++)
        {
            if (o[i] == null)
            {
                o[i] = item;
                return slots[i];
            }
        }
        return null;
    }

    public void ReleaseSlot(JobBase item)
    {
        JobBase[] o = Occupants;
        for (int i = 0; i < o.Length; i++)
            if (o[i] == item) o[i] = null;
    }
}
