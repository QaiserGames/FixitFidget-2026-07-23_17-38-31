using UnityEngine;

// ---------------------------------------------------------------------------
// "CALL IT A NIGHT", JUST INSIDE THE CAFÉ'S DOOR (the Night 1 slice)
//
// Made by NightCycle while a night runs, a trigger just inside the door; offered only while Ace is
// inside the café, and only a moment after the night began (so the button that closed the recap
// can't end it too). E, or A / Cross.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightDoorway : NightInteractable
{
    CafeViewMode view;

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

    public override string Prompt => "Call it a night";

    public override void Interact(PlayerInteractor player)
    {
        if (IsAvailable && NightCycle.Instance != null) NightCycle.Instance.CallItANight();
    }
}
