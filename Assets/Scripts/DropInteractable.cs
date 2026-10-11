using UnityEngine;

public class DropInteractable : Interactable
{
    /// <summary>E with something in hand at the counter's drop spot, the intake tray (here and StationInteractable). PLACEHOLDER COPY.</summary>
    public const string IntakeTrayWords = "Put it on the intake tray";

    private DropSpot spot;

    private void Awake()
    {
        spot = GetComponent<DropSpot>();
    }

    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;
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

            PlayerCarry carry = PlayerCarry.Current;
            if (carry == null || !carry.IsCarrying) return "";

            if (!spot.CanAccept(carry.Carried)) return "No room here";

            // The counter's drop spot is the intake tray (playtest 4). "Return item", right after the pickup at the till with
            // the customer still in reach, read like giving it back to them (playtest 4 run 2; the photo of the pickup).
            return spot.Kind == DropSpot.SpotKind.Counter ? IntakeTrayWords : "Set down";
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