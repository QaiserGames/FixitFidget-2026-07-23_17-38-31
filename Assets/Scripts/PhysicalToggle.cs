using UnityEngine;

// Same hover/activate contract as screws and circuit tiles. The task owns
// completion; the presentation can be a switch, cap, plug or crown.
public sealed class PhysicalToggle : BenchInteractable
{
    private HumanFault fault;
    private CustomerBrain owner;
    protected override void Awake() { } // View owns its materials; don't clone them.
    public void Bind(HumanFault task, CustomerBrain customer) { fault = task; owner = customer; }
    public override string DisplayName => "Mute switch";
    public override string Prompt => "Turn sound on";
    public override ToolType RequiredTool => ToolType.Hand;
    public override bool CanInteract => fault != null && fault.CanFix(owner);
    public override void Activate() { if (CanInteract) fault.Flip(owner); }
    public override void SetHighlight(bool on)
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial.color = on
            ? new Color(1f, .8f, .28f) : new Color(1f, .43f, .08f);
    }
}
