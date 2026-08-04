using UnityEngine;

[RequireComponent(typeof(Screw))]
public class ScrewTarget : BenchInteractable
{
    private Screw screw;
    private BenchRig rig;
    private RemovablePart heldPlate;

    protected override void Awake()
    {
        base.Awake();
        screw = GetComponent<Screw>();
    }

    private void Start()
    {
        rig = FindFirstObjectByType<BenchRig>();
    }

    // The plate this screw fastens tells us who it is at startup.
    public void SetPlate(RemovablePart plate) => heldPlate = plate;

    // Fastened screws can come out. Loose screws can go back in —
    // but only once the plate they hold is seated again.
    public override bool CanInteract
    {
        get
        {
            if (screw.IsBusy) return false;
            if (!screw.IsOut) return true;
            return heldPlate == null || !heldPlate.IsRemoved;
        }
    }

    public override ToolType RequiredTool => ToolType.Screwdriver;

    public override void Activate()
    {
        if (screw.IsOut) screw.Rescrew();
        else if (rig != null) screw.Unscrew(rig.ScrewBin);
    }
}