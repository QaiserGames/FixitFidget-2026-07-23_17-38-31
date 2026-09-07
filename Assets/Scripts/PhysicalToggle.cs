using UnityEngine;

// Same hover/activate contract as screws and circuit tiles. The task owns
// completion; the presentation can be a switch, cap, plug or crown.
public sealed class PhysicalToggle : BenchInteractable
{
    private HumanFault fault;
    private CustomerBrain owner;
    [SerializeField] private Collider hitTarget;
    private Renderer surface;
    private MaterialPropertyBlock properties;
    private Color restingColor;
    public Collider HitTarget => hitTarget != null ? hitTarget : GetComponentInChildren<Collider>();
    protected override void Awake() { } // View owns its materials; don't clone them.
    public void Bind(HumanFault task, CustomerBrain customer)
    {
        fault = task; owner = customer;
        surface = GetComponentInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        restingColor = surface != null && surface.sharedMaterial != null ? surface.sharedMaterial.color : Color.white;
    }
    public override string DisplayName => "Mute switch";
    public override string Prompt => "Turn sound on";
    public override ToolType RequiredTool => ToolType.Hand;
    public override bool CanInteract => fault != null && fault.CanFix(owner);
    public override void Activate() { if (CanInteract) fault.Flip(owner); }
    public override void SetHighlight(bool on)
    {
        if (surface == null || properties == null) return;
        // Per-instance feedback must never recolour an imported/shared material.
        Color tint = fault != null && fault.Finished ? RepairOverlayUI.Mint
            : on ? new Color(1f, .82f, .40f) : restingColor;
        surface.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", tint); properties.SetColor("_Color", tint);
        surface.SetPropertyBlock(properties);
    }
}
