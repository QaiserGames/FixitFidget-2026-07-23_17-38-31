using UnityEngine;

// Tile colliders use the existing bench interaction, with their own shared-material tint.
public sealed class CircuitTile : BenchInteractable
{
    private CircuitPuzzle owner;
    private Transform wire;
    private Renderer plate;
    private int index;
    private Color plateColor;
    private MaterialPropertyBlock tint;
    public bool Locked => owner == null || owner.Run.IsLocked(index);
    public bool IsCorrect => owner != null && owner.Run.IsAligned(index);
    public override string DisplayName => $"Signal tile {index + 1}";
    public override string Prompt => Locked ? "Verified - locked" : "Rotate clockwise";
    public override ToolType RequiredTool => ToolType.Hand;
    public override bool CanInteract => owner != null && owner.IsBeingInspected && !Locked;

    // Base Awake creates a material instance. All circuit plates share one material.
    protected override void Awake() { }

    public void Build(CircuitPuzzle puzzle, int tileIndex, Transform wireRoot, Renderer backing, Color color)
    {
        owner = puzzle; index = tileIndex; wire = wireRoot; plate = backing; plateColor = color;
        Refresh();
        SetHighlight(false);
    }

    public override void Activate()
    {
        if (!CanInteract || !owner.Run.Turn(index)) return;
        Refresh();
        owner.Refresh();
    }

    public void Refresh()
    {
        if (wire != null) wire.localRotation = Quaternion.Euler(0f, 0f, -90f * owner.Run.Turns(index));
    }

    public override void SetHighlight(bool on)
    {
        if (plate == null) return;
        if (tint == null) tint = new MaterialPropertyBlock();
        Color c = on ? new Color(0.3f, 0.43f, 0.48f) : plateColor;
        tint.SetColor("_BaseColor", c);
        tint.SetColor("_Color", c);
        plate.SetPropertyBlock(tint);
    }
}
