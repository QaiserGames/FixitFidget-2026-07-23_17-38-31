using System.Collections.Generic;
using UnityEngine;

// Owns every place a waiting customer can go. A customer asks for a spot the
// moment their job is accepted, and hands it back when they leave.
//
// Mirrors ItemSlotArea's claim/release shape deliberately — same pattern, so
// there's only one idea to learn.
public class WaitingArea : MonoBehaviour
{
    public static WaitingArea Instance { get; private set; }

    [Tooltip("Leave empty and it collects every WaitingSpot underneath this " +
             "object automatically — including ones you add later.")]
    [SerializeField] private WaitingSpot[] spots;

    private void Awake()
    {
        Instance = this;

        if (spots == null || spots.Length == 0)
            spots = GetComponentsInChildren<WaitingSpot>(true);
    }

    public bool HasFreeSpot
    {
        get
        {
            if (spots == null) return false;
            foreach (WaitingSpot s in spots)
                if (s != null && s.IsAvailable) return true;
            return false;
        }
    }

    // Try the kind they'd prefer first, then settle for anything free.
    // Null means the whole floor is full — the caller decides what to do.
    public WaitingSpot Claim(CustomerBrain customer, WaitingSpot.SpotKind preferred)
    {
        WaitingSpot chosen = PickRandom(customer, preferred, true);
        if (chosen == null) chosen = PickRandom(customer, preferred, false);
        return chosen;
    }

    private WaitingSpot PickRandom(CustomerBrain customer, WaitingSpot.SpotKind kind, bool matchKind)
    {
        if (spots == null) return null;

        List<WaitingSpot> free = new List<WaitingSpot>();

        foreach (WaitingSpot s in spots)
        {
            if (s == null || !s.IsAvailable) continue;
            if (matchKind && s.Kind != kind) continue;
            free.Add(s);
        }

        if (free.Count == 0) return null;

        // Random rather than first-free, so the room doesn't fill left to right.
        WaitingSpot pick = free[Random.Range(0, free.Count)];
        return pick.Claim(customer) ? pick : null;
    }

    public void Release(CustomerBrain customer)
    {
        if (spots == null) return;
        foreach (WaitingSpot s in spots)
            if (s != null) s.Release(customer);
    }
}
