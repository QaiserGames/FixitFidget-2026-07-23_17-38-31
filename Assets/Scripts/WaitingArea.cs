using System.Collections.Generic;
using UnityEngine;

// Owns every place a waiting customer can go. A customer asks for a spot the
// moment their job is accepted, and hands it back when they leave.
//
// STEP 2 CHANGE — the spot list is now a registry, not a hierarchy scan.
//
// Spots call Register/Unregister from their own OnEnable/OnDisable, so a
// TableSeat can live under its table where it belongs instead of being forced
// under this object. Everything else about the API is unchanged, which is why
// CustomerBrain needed no edits at all for seating to start working.
public class WaitingArea : MonoBehaviour
{
    public static WaitingArea Instance { get; private set; }

    private static readonly List<WaitingSpot> registry = new List<WaitingSpot>();
    private static readonly List<WaitingSpot> scratch = new List<WaitingSpot>();

    [Tooltip("Optional. Leave empty — spots register themselves now, wherever " +
             "they sit in the hierarchy. Anything listed here is registered " +
             "too, so old scene wiring still works.")]
    [SerializeField] private WaitingSpot[] spots;

    // With Enter Play Mode Options set to skip domain reload, statics survive
    // between play sessions — so the registry would still be holding last
    // run's destroyed spots. This runs before the scene loads and wipes them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
        scratch.Clear();
        Instance = null;
    }

    public static void Register(WaitingSpot spot)
    {
        if (spot == null || registry.Contains(spot)) return;
        registry.Add(spot);
    }

    public static void Unregister(WaitingSpot spot)
    {
        registry.Remove(spot);
    }

    // Handy in the Console when you're wondering why nobody sits down.
    public static int RegisteredSpots => registry.Count;

    private void Awake()
    {
        Instance = this;

        // Belt and braces for anything wired the old way.
        if (spots != null)
            foreach (WaitingSpot s in spots) Register(s);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool HasFreeSpot
    {
        get
        {
            for (int i = 0; i < registry.Count; i++)
            {
                WaitingSpot s = registry[i];
                if (s != null && s.IsAvailable) return true;
            }
            return false;
        }
    }

    // Try the kind they'd prefer first, then settle for anything free.
    // Null means the whole floor is full — the caller decides what to do.
    //
    // This two-pass shape is what makes seating a PREFERENCE rather than a
    // requirement: when every seat is taken (or dirty), a customer who wanted
    // to sit falls through to a loiter spot and starts draining at 1.15x
    // instead of 0.6x. That difference is the entire pressure model.
    public WaitingSpot Claim(CustomerBrain customer, WaitingSpot.SpotKind preferred)
    {
        WaitingSpot chosen = PickRandom(customer, preferred, true);
        if (chosen == null) chosen = PickRandom(customer, preferred, false);
        return chosen;
    }

    private WaitingSpot PickRandom(CustomerBrain customer, WaitingSpot.SpotKind kind, bool matchKind)
    {
        scratch.Clear();

        for (int i = 0; i < registry.Count; i++)
        {
            WaitingSpot s = registry[i];
            if (s == null || !s.IsAvailable) continue;
            if (matchKind && s.Kind != kind) continue;
            scratch.Add(s);
        }

        if (scratch.Count == 0) return null;

        // Random rather than first-free, so the room doesn't fill left to right.
        WaitingSpot pick = scratch[Random.Range(0, scratch.Count)];
        return pick.Claim(customer) ? pick : null;
    }

    public void Release(CustomerBrain customer)
    {
        for (int i = 0; i < registry.Count; i++)
            if (registry[i] != null) registry[i].Release(customer);
    }
}
