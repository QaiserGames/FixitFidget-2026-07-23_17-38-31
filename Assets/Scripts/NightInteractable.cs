using UnityEngine;

// ---------------------------------------------------------------------------
// SOMETHING ACE CAN USE AT NIGHT, AND ONLY AT NIGHT (the Night 1 slice)
//
// The café is closed at night, so PlayerInteractor offers only these then (the nearest from above,
// the one under the crosshair in first person; E, or A / Cross on a pad), and by day these offer
// nothing at all: a thing in the street is scenery until the night walk runs.
// ---------------------------------------------------------------------------
public abstract class NightInteractable : Interactable
{
    public override bool IsAvailable => NightWalk.Instance != null && NightWalk.Instance.Active && AvailableTonight;

    /// <summary>Whether it can be used now, the night walk running.</summary>
    protected virtual bool AvailableTonight => true;
}
