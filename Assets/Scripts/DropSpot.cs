using UnityEngine;

public class DropSpot : MonoBehaviour
{
    public enum SpotKind { Bench, Counter }

    [SerializeField] private SpotKind kind = SpotKind.Bench;
    [SerializeField] private ItemSlotArea slotArea;

    public SpotKind Kind => kind;

    // Pure question — never changes state.
    public bool CanAccept(JobBase item)
    {
        if (kind == SpotKind.Counter) return true;
        if (slotArea == null) return false;
        return slotArea.HasFreeSlot || slotArea.Holds(item);
    }

    public Transform ResolvePoint(JobBase item)
    {
        // Counter: back to its owner's spot, in front of the right customer.
        if (kind == SpotKind.Counter && item != null && item.Owner != null)
        {
            CounterQueue queue = FindAnyObjectByType<CounterQueue>();
            if (queue != null && item.Owner.SlotIndex >= 0)
                return queue.ItemSpot(item.Owner.SlotIndex);
        }

        // Bench: first free slot. Null means full.
        if (slotArea != null) return slotArea.ClaimSlot(item);

        return null;
    }

    public void Release(JobBase item)
    {
        if (slotArea != null) slotArea.ReleaseSlot(item);
    }
}