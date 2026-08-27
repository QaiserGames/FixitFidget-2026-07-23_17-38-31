using UnityEngine;

public class DropInteractable : Interactable
{
    private DropSpot spot;

    private void Awake()
    {
        spot = GetComponent<DropSpot>();
    }

    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && carry.IsCarrying && spot != null;
        }
    }

    // THE BUG THIS FIXES: this always said "Set down", even with every slot
    // taken. So the game invited you to press E and then threw — see Interact.
    // StationInteractable has said "No room here" for months; this is the same
    // check, on the sibling that never got it.
    public override string Prompt
    {
        get
        {
            if (spot == null) return "";

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry == null || !carry.IsCarrying) return "";

            if (!spot.CanAccept(carry.Carried)) return "No room here";

            return spot.Kind == DropSpot.SpotKind.Counter ? "Return item" : "Set down";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null || !carry.IsCarrying || spot == null) return;

        Transform point = spot.ResolvePoint(carry.Carried);

        // THE STUCK HANDS. ResolvePoint returns null when the area is full, and
        // this used to hand that null straight to PlaceAt — which re-enables the
        // renderers BEFORE reading spot.position, so it threw halfway through
        // and never reached `Carried = null`.
        //
        // Result: an item you couldn't put down and couldn't let go of, and
        // every later interaction reading "Hands full" while your hands looked
        // empty. One missing line.
        if (point == null) return;      // full — the prompt already said so

        carry.PlaceAt(point);
    }
}