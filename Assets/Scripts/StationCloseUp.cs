using UnityEngine;

// ---------------------------------------------------------------------------
// THE DRINKS CLOSE-UP, FROM ABOVE (stations as reach, playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.1)
//
// Mansoor's note: too much button pressing at the counter, the bench and the espresso machine, in both cameras. The
// stations aren't rooms any more: nothing is stepped up to. The one close-up that stays is the dispenser's, in the
// overhead view only, because pouring can't be read from twenty metres up: E beside the dispenser opens it (the
// station's own camera, as F used to), and walking (the left stick or WASD), B or Esc leave it. In first person the
// dispenser's controls work on Ace's own crosshair and there is no close-up at all.
//
// Made while playing by PlayerInteractor, on a child of the dispenser's station (a child, so the dispenser's colliders
// still find the station itself first); offered to an Ace standing at the dispenser in the overhead view, ahead of
// what's within reach on the counter behind him (Offer Priority).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class StationCloseUp : Interactable
{
    /// <summary>Ahead of a device or a cup (3) and a customer (2); a ringing support phone (5) still comes first.</summary>
    public const int OfferPriority = 4;

    public StationInteractable station;

    private void Awake() => SetPriority(OfferPriority);

    /// <summary>Makes the dispenser's offer (once).</summary>
    public static StationCloseUp For(StationInteractable station)
    {
        if (station == null) return null;
        StationCloseUp existing = station.GetComponentInChildren<StationCloseUp>(true);
        if (existing != null) return existing;
        var go = new GameObject("Drinks close-up (made while playing)");
        go.transform.SetParent(station.transform, false);
        var offer = go.AddComponent<StationCloseUp>();
        offer.station = station;
        return offer;
    }

    public override bool IsAvailable => station != null && station.isActiveAndEnabled;
    public override string Prompt => station != null ? station.StationLabel : "";

    public override void Interact(PlayerInteractor player)
    {
        if (player != null && station != null) player.EnterStation(station);
    }
}
