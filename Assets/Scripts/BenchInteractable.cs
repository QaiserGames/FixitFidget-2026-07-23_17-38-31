using UnityEngine;

public abstract class BenchInteractable : MonoBehaviour
{
    [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.6f);

    private Renderer rend;
    private Color baseColor;
    private bool highlighted;

    protected virtual void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) baseColor = rend.material.color;
    }

    // What this part is called, for the hover tooltip.
    public virtual string DisplayName => "Part";

    // What pressing it would do.
    public virtual string Prompt => "Interact";
    // Can the player act on this right now?
    public abstract bool CanInteract { get; }

    // Which tool does this need? Hand means "any".
    public abstract ToolType RequiredTool { get; }

    public abstract void Activate();

    public void SetHighlight(bool on)
    {
        if (rend == null || highlighted == on) return;
        highlighted = on;
        rend.material.color = on ? highlightColor : baseColor;
    }
}