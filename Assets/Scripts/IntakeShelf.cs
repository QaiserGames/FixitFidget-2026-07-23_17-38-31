using UnityEngine;

// Where an accepted device sits until you carry it to the bench.
//
// This exists because the customer no longer stands at the counter holding
// their place. The device used to spawn at "their" counter spot and be
// returned there by slot index — which stops working the moment they walk
// away. So the item lives on a shelf of its own, and the job number and
// colour are what tie it back to a person.
//
// The shelf can fill up. That's deliberate: a full shelf stops you taking new
// work until you clear it, which is a legible squeeze that costs nothing to
// build because ItemSlotArea already does all of it.
public class IntakeShelf : MonoBehaviour
{
    public static IntakeShelf Instance { get; private set; }

    [Tooltip("Leave empty to use an ItemSlotArea on this same object.")]
    [SerializeField] private ItemSlotArea slotArea;

    private void Awake()
    {
        Instance = this;
        if (slotArea == null) slotArea = GetComponent<ItemSlotArea>();
    }

    public bool HasRoom => slotArea != null && slotArea.HasFreeSlot;

    // Reserve a slot for a device that's about to be spawned into it.
    // Null means full — check HasRoom before accepting a job.
    public Transform Claim(JobBase item) => slotArea != null ? slotArea.ClaimSlot(item) : null;
}
