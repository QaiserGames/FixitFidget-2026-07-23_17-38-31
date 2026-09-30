using UnityEngine;

// ---------------------------------------------------------------------------
// ACE'S WAY INTO GRACE'S HOUSE (the break-ins; 30 Sept 2026)
//
// A place on her stoop, made by GraceHouse while a night runs: a trigger over the steps and the
// doorway, like the café's "Call it a night". E there ("Let yourself in"; A / Cross on a pad) opens
// her front door for Ace. The door then minds Ace's capsule by itself (it holds open while Ace is in
// the doorway or just inside, and shuts behind Ace on the pavement: GraceHouse.DoorForAce).
//
// Until 30 Sept the door opened by itself as Ace stepped onto the stoop, and nothing on screen said
// the house could be entered; and in the real game the break-ins were off altogether, so Mansoor's
// second playtest (Days 1-3) found her door shut on Night 2. Now the break-ins are on in the real
// game (NightWalk.breakIns), and the prompt says what E does. With them off (a lab that says so), the
// prompt reads "Try the door" and E only finds it locked.
//
// A zone (IsZone): offered wherever Ace stands in it, in either view, not only where the crosshair
// is. Priority -1, so "Take Barnaby" (0) comes first while he is on the step.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceDoorZone : NightInteractable
{
    [HideInInspector] public GraceHouse house;

    public override bool IsZone => true;

    void Awake() => SetPriority(-1);

    protected override bool AvailableTonight => house != null && house.DoorPromptShown;

    public override string Prompt => house != null ? house.DoorPrompt : "";

    public override void Interact(PlayerInteractor player)
    {
        if (house != null) house.LetAceIn();
    }
}
