using UnityEngine;

// A screw's head under the cursor (the bench, v2): hold the driver on it and it backs out of its hole along its own axis
// and falls (Screw). A loose screw can be held too: the driver fetches it to its hole and screws it in (the same as
// holding the empty hole, ScrewSocket). Activate does the whole thing by itself, for the labs' hand and the Day 1 guide.
[RequireComponent(typeof(Screw))]
public class ScrewTarget : BenchInteractable
{
    private Screw screw;
    private RemovablePart heldPlate;

    protected override void Awake()
    {
        base.Awake();
        screw = GetComponent<Screw>();
    }

    // The plate this screw fastens tells us who it is at startup.
    public void SetPlate(RemovablePart plate) => heldPlate = plate;

    /// <summary>The cover this screw holds is on (or there is none): the screw may go back in.</summary>
    public bool PlateSeated => heldPlate == null || !heldPlate.IsRemoved;

    // Seated screws can come out. Loose screws can go back in — but only once the plate they hold is seated again.
    public override bool CanInteract
    {
        get
        {
            if (screw.IsBusy) return false;
            if (!screw.IsOut) return true;
            return PlateSeated;
        }
    }

    public override string DisplayName => "Screw";
    public override string Prompt => !screw.IsOut
        ? (screw.Progress > .02f ? "Hold to unscrew (part way out)" : "Hold to unscrew")
        : screw.Fetched ? "Hold to screw in" : "Hold to screw in (the driver fetches it)";
    public override ToolType RequiredTool => ToolType.Screwdriver;
    public override bool Holdable => true;
    public override float HoldProgress => screw.IsOut ? 1f - screw.Progress : screw.Progress;
    public override Vector3 WorkPoint => screw.HeadPoint;
    public override Vector3 WorkNormal => screw.Axis;

    public override bool HoldTick(BenchHand hand)
    {
        if (!screw.IsOut) return screw.HoldTurn(hand.deltaTime, outward: true);
        if (!screw.Fetched) { screw.BeginFetch(); return false; }
        return screw.HoldTurn(hand.deltaTime, outward: false);
    }

    public override void Activate()
    {
        if (screw.IsOut) screw.Rescrew();
        else screw.Unscrew(null);
    }
}
