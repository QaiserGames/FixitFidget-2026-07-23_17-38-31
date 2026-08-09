using UnityEngine;

public class DropSpot : MonoBehaviour
{
    public enum SpotKind { Bench, Counter }

    [SerializeField] private SpotKind kind = SpotKind.Bench;
    [SerializeField] private ItemSlotArea slotArea;

    public SpotKind Kind => kind;

    public bool CanAccept(JobBase item)
    {
        if (kind == SpotKind.Counter) return true;   // the owner's spot is always theirs
        return slotArea != null && (slotArea.HasFreeSlot || slotArea.ClaimSlot(item) != null);
    }

    public Transform ResolvePoint(JobBase item)
    {
        // Counter: goes back to its owner's spot, in front of the right customer.
        if (kind == SpotKind.Counter && item != null && item.Owner != null)
        {
            CounterQueue queue = FindAnyObjectByType<CounterQueue>();
            if (queue != null && item.Owner.SlotIndex >= 0)
                return queue.ItemSpot(item.Owner.SlotIndex);
        }

        // Bench: first free slot.
        if (slotArea != null) return slotArea.ClaimSlot(item);

        return transform;
    }

    public void Release(JobBase item)
    {
        if (slotArea != null) slotArea.ReleaseSlot(item);
    }
}