using UnityEngine;

// A surface you can put a carried item down on. Both kinds now work the same
// way — claim a free slot from an ItemSlotArea — because devices no longer
// belong to a counter position.
//
// The Kind is kept purely so the prompt can say "Return item" at the counter
// and "Set down" at the bench.
public class DropSpot : MonoBehaviour
{
    public enum SpotKind { Bench, Counter }

    [SerializeField] private SpotKind kind = SpotKind.Bench;
    [SerializeField] private ItemSlotArea slotArea;

    public SpotKind Kind => kind;
    public bool Holds(JobBase item) => slotArea != null && slotArea.Holds(item);

    // Pure question — never changes state.
    public bool CanAccept(JobBase item)
    {
        if (slotArea == null) return false;
        return slotArea.HasFreeSlot || slotArea.Holds(item);
    }

    // First free slot. Null means full.
    public Transform ResolvePoint(JobBase item)
    {
        return slotArea != null ? slotArea.ClaimSlot(item) : null;
    }

    public void Release(JobBase item)
    {
        if (slotArea != null) slotArea.ReleaseSlot(item);
    }
}
