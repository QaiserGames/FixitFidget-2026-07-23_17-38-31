using UnityEngine;

// ---------------------------------------------------------------------------
// "CALL IT A NIGHT", JUST INSIDE THE CAFÉ'S DOOR (the Night 1 slice)
//
// Made by NightCycle while a night runs, a trigger just inside the door. Offered only while Ace is
// inside the café, and only once Ace has been out since the night began: never on the way out, nor
// to the same press that closed the recap (NightCycle.CanCallItANight). A zone: PlayerInteractor offers
// it wherever Ace stands in or near it, in either view, not only where the crosshair is. E, or A / Cross.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightDoorway : NightInteractable
{
    CafeViewMode view;

    public override bool IsZone => true;

    protected override bool AvailableTonight
    {
        get
        {
            NightCycle cycle = NightCycle.Instance;
            if (cycle == null || !cycle.CanCallItANight) return false;
            if (view == null) view = FindAnyObjectByType<CafeViewMode>();
            return view == null || view.AceInsideCafe;
        }
    }

    public override string Prompt => NightCycle.CallItANightPrompt;

    public override void Interact(PlayerInteractor player)
    {
        if (IsAvailable && NightCycle.Instance != null) NightCycle.Instance.CallItANight();
    }
}
