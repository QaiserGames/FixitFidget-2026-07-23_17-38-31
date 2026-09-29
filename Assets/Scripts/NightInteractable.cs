using UnityEngine;

// ---------------------------------------------------------------------------
// SOMETHING ACE CAN USE AT NIGHT, AND ONLY AT NIGHT (the Night 1 slice)
//
// The café is closed at night, so PlayerInteractor offers only these then (the nearest from above,
// the one under the crosshair in first person; E, or A / Cross on a pad), and by day these offer
// nothing at all: a thing in the street is scenery until the night walk runs. Nor while the screen
// goes dark at nightfall or at the night's end (NightCycle): only while the night is on.
// ---------------------------------------------------------------------------
public abstract class NightInteractable : Interactable
{
    public override bool IsAvailable => NightWalk.Instance != null && NightWalk.Instance.Active
        && (NightCycle.Instance == null || NightCycle.Instance.Now == NightCycle.Phase.Night) && AvailableTonight;

    /// <summary>
    /// A place rather than a thing (NightDoorway): offered wherever Ace stands in or near it, whatever the
    /// first-person crosshair is on. A ray from a camera inside a trigger never hits that trigger.
    /// </summary>
    public virtual bool IsZone => false;

    /// <summary>Whether it can be used now, the night walk running.</summary>
    protected virtual bool AvailableTonight => true;
}
