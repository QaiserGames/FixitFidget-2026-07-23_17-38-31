using UnityEngine;

// A screw's head under the cursor (the bench, v2): hold the driver on it and it backs out of its hole along its own axis
// and falls (Screw). A loose screw can be held too: the driver fetches it to its hole and screws it in (the same as
// holding the empty hole, ScrewSocket). Activate does the whole thing by itself, for the labs' hand and the Day 1 guide.
//
// NOTHING TO UNDO (playtest 4, 10 Oct; both playtesters: "Back together · Perfect" sat over "Screw: Hold to unscrew", "the
// finished state still offers to take it apart"). On a device that is finished (every fault fixed and back in one piece:
// JobBase.IsFinished) a seated screw offers nothing: it can't be turned, the hover names it with no verb, and the pad's
// aim help passes it by (it is not interactable). A device closed up with faults left can still be opened: the player
// may want to finish it.
[RequireComponent(typeof(Screw))]
public class ScrewTarget : BenchInteractable
{
    private Screw screw;
    private RemovablePart heldPlate;
    private JobBase job;

    protected override void Awake()
    {
        base.Awake();
        screw = GetComponent<Screw>();
        job = GetComponentInParent<JobBase>();
    }

    // The editor's checks add one without Awake: find what it needs when first asked.
    private Screw Own => screw != null ? screw : screw = GetComponent<Screw>();

    // The plate this screw fastens tells us who it is at startup.
    public void SetPlate(RemovablePart plate) => heldPlate = plate;

    /// <summary>The cover this screw holds is on (or there is none): the screw may go back in.</summary>
    public bool PlateSeated => heldPlate == null || !heldPlate.IsRemoved;

    /// <summary>Seated in a device that is finished: there is nothing to do with it (no verb, no turn).</summary>
    public bool Done
    {
        get
        {
            Screw s = Own;
            if (s == null || s.IsOut) return false;
            // A seated screw is in the device, so the device is its parent's job; a freed one has left the item and is found
            // through the job remembered at Awake (it never needs this answer while it is out).
            if (job == null) job = GetComponentInParent<JobBase>();
            return job != null && job.IsFinished;
        }
    }

    // Seated screws can come out (unless the device is finished). Loose screws can go back in — but only once the plate
    // they hold is seated again.
    public override bool CanInteract
    {
        get
        {
            Screw s = Own;
            if (s == null || s.IsBusy) return false;
            if (!s.IsOut) return !Done;
            return PlateSeated;
        }
    }

    public override string DisplayName => "Screw";
    public override string Prompt => Done ? ""
        : !Own.IsOut ? (Own.Progress > .02f ? "Hold to unscrew (part way out)" : "Hold to unscrew")
        : Own.Fetched ? "Hold to screw in" : "Hold to screw in (the driver fetches it)";
    public override ToolType RequiredTool => ToolType.Screwdriver;
    public override bool Holdable => true;
    public override float HoldProgress => Own.IsOut ? 1f - Own.Progress : Own.Progress;
    public override Vector3 WorkPoint => Own.HeadPoint;
    public override Vector3 WorkNormal => Own.Axis;

    public override bool HoldTick(BenchHand hand)
    {
        if (!Own.IsOut) return Done || Own.HoldTurn(hand.deltaTime, outward: true);
        if (!Own.Fetched) { Own.BeginFetch(); return false; }
        return Own.HoldTurn(hand.deltaTime, outward: false);
    }

    public override void Activate()
    {
        if (Own.IsOut) Own.Rescrew();
        else if (!Done) Own.Unscrew(null);
    }
}
