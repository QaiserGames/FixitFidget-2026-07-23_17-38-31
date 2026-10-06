using UnityEngine;

// ---------------------------------------------------------------------------
// THE DUMPSTER'S NEAR HALF, AT NIGHT (6 Oct 2026; NightZeroSet, Fixit Fidget > Night > Bins 1)
//
// A place (a trigger) in front of the dumpster's near half. On Night 0, with the bin bag in hand, "Bin it": the lid
// lifts, the bag goes in, and the night's man stands up out of the other half (NightZero). Nothing else, yet: every
// night opening at the bins comes with the favours (claude/foundation-pass-build-plan.md §6).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightBins : NightInteractable
{
    public override bool IsZone => true;
    protected override bool DuringTheBins => true;
    protected override bool AvailableTonight => NightZero.Instance != null && NightZero.Instance.CanBinIt;
    public override string Prompt => "Bin it";

    public override void Interact(PlayerInteractor player)
    {
        if (IsAvailable) NightZero.Instance.BinIt();
    }
}
