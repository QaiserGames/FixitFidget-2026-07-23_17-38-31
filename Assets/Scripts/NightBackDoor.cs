using UnityEngine;

// ---------------------------------------------------------------------------
// THE CAFÉ'S BACK DOOR, AT NIGHT (6 Oct 2026; NightZeroSet, Fixit Fidget > Night > Bins 1)
//
// Two of these, one each side of the back door, each a place (a trigger) where Ace stands:
//   * inside, behind the counter: on Night 0, with the bag in hand, "Take the bins out" (NightZero); any other night,
//     "Go out the back";
//   * outside on Back Street: "Call it a night" once Ace can (NightCycle: Ace has been out, the deal is made), or
//     else "Go back inside".
// Going through is a blink (NightCycle.Through): a quick dip to black, and Ace is on the other side, facing away from
// the door. By day the door is scenery: nothing at night is offered by day.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightBackDoor : NightInteractable
{
    public enum Side { Inside, Outside }

    public Side side = Side.Inside;
    [Tooltip("Where Ace comes out on the other side, facing away from the door.")]
    public Transform through;
    [Tooltip("How near Ace stands (metres from this place's middle) for the door to be offered. Outside, the dumpster is " +
             "two metres away: from there the door isn't offered, so the deal's last E never calls it a night.")]
    public float reach = 1.6f;

    static Transform ace;

    bool AceAtTheDoor
    {
        get
        {
            if (ace == null)
            {
                PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
                ace = player != null ? player.transform : null;
            }
            if (ace == null) return true;
            Vector3 d = ace.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= reach * reach;
        }
    }

    public override bool IsZone => true;

    bool Bins => side == Side.Inside && NightZero.Instance != null && NightZero.Instance.Now == NightZero.Step.AtTheDoor;

    // On Night 0 only the way out with the bag; the way back in waits for the deal.
    protected override bool DuringTheBins => Bins;
    protected override bool AvailableTonight => through != null && (side == Side.Inside || !NightZero.Pending) && AceAtTheDoor;

    public override string Prompt => side == Side.Inside
        ? Bins ? "Take the bins out" : "Go out the back"
        : NightCycle.Instance != null && NightCycle.Instance.CanCallItANight ? NightCycle.CallItANightPrompt : "Go back inside";

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        if (Bins) { NightZero.Instance.TakeTheBinsOut(); return; }
        if (side == Side.Outside && NightCycle.Instance != null && NightCycle.Instance.CanCallItANight)
        {
            NightCycle.Instance.CallItANight();
            return;
        }
        NightCycle.Through(through);
    }
}
