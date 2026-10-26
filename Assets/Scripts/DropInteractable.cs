using System.Collections.Generic;
using UnityEngine;

public class DropInteractable : Interactable
{
    /// <summary>E with something in hand at the counter's drop spot, the intake tray (here and StationInteractable). PLACEHOLDER COPY.</summary>
    public const string IntakeTrayWords = "Put it on the intake tray";
    /// <summary>E at the intake tray with a device lifted from that same tray (checklist v2 §5.7). PLACEHOLDER COPY.</summary>
    public const string PutBackWords = "Put it back";

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
            return carry != null && carry.IsCarrying && spot != null && Takes(spot, carry.Carried);
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

            // The counter's drop spot is the intake tray (playtest 4). "Return item", right after the pickup at the till with
            // the customer still in reach, read like giving it back to them (playtest 4 run 2; the photo of the pickup).
            return TrayWords(spot, carry.Carried);
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null || !carry.IsCarrying || spot == null || !Takes(spot, carry.Carried)) return;

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

    // ------------------------------------------------------------------ the intake tray's words (checklist v2 §5.7)
    //
    // The First Hour's #4 (13 Oct): on Day 1, right after Ace picks Grace's camera up at the till, E offered "Put it on the
    // intake tray": one press put it straight back where it came from, while the card said to carry it to the bench. Now:
    //   * on Day 1's carry step (DayOneGuideUI.CarryingToBench: the card is saying "Carry the ... to the repair bench") the
    //     tray doesn't offer to take that device at all: the card and E agree, and the line shows only "In hand ...";
    //   * on any day, a device lifted from this same tray reads "Put it back" (true words: it goes back where it was), any
    //     other "Put it on the intake tray".
    // Used here and by the counter's StationInteractable (the café's intake tray is the counter station's drop spot; this
    // component isn't in the café scene).
    //
    // Where a device was lifted from is PlayerCarry's to say (the Producer's call 22, 14 Oct): it records the spot that held
    // the device at the pickup (PlayerCarry.LiftedFromFor) and forgets it with the device. That replaced this file's own
    // once-a-frame watch of the tray, which could only guess from a frame's difference.

    /// <summary>Whether <paramref name="spot"/> takes <paramref name="carried"/> now: never the counter's tray for the device Day 1's card is sending to the bench.</summary>
    public static bool Takes(DropSpot spot, JobBase carried) =>
        spot != null && carried != null
        && !(spot.Kind == DropSpot.SpotKind.Counter && carried == DayOneGuideUI.CarryingToBench)
        // 24 Oct 2026 (Days, restock by hand): a crate of cups and beans goes to the machine or back to its shelf, never on a
        // counter or the bench (StockCrate).
        && !(carried is StockCrate);

    /// <summary>What E says at <paramref name="spot"/> with <paramref name="carried"/> in hand ("" when it doesn't take it).</summary>
    public static string TrayWords(DropSpot spot, JobBase carried)
    {
        if (spot == null || carried == null || !Takes(spot, carried)) return "";
        return TrayWords(spot.Kind, spot.CanAccept(carried), spot.Kind == DropSpot.SpotKind.Counter && LiftedFrom(spot, carried));
    }

    /// <summary>The words alone (checks): "No room here", "Set down" at the bench, "Put it back" or "Put it on the intake tray" at the tray.</summary>
    public static string TrayWords(DropSpot.SpotKind kind, bool room, bool liftedFromThisTray)
    {
        if (!room) return "No room here";
        if (kind != DropSpot.SpotKind.Counter) return "Set down";
        return liftedFromThisTray ? PutBackWords : IntakeTrayWords;
    }

    /// <summary>
    /// The device in hand was lifted from this same <paramref name="spot"/> and hasn't left Ace's hands since: PlayerCarry
    /// records the spot at the pickup (LiftedFromFor) and forgets it with the device (the Producer's call 22).
    /// </summary>
    public static bool LiftedFrom(DropSpot spot, JobBase item)
    {
        if (spot == null || item == null) return false;
        PlayerCarry carry = PlayerCarry.Current;
        return carry != null && carry.LiftedFromFor(item) == spot;
    }
}
