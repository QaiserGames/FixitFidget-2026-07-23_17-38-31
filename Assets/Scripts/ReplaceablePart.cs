using UnityEngine;

public class ReplaceablePart : BenchInteractable
{
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject freshVisual;
    [SerializeField] private RemovablePart coveredBy;

    public bool IsReplaced { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (brokenVisual != null) brokenVisual.SetActive(true);
        if (freshVisual != null) freshVisual.SetActive(false);
    }

    // Only reachable once whatever covers it has been lifted off.
    public override bool CanInteract =>
        !IsReplaced && (coveredBy == null || coveredBy.IsRemoved);

    public override ToolType RequiredTool => ToolType.Tweezers;

    public override void Activate()
    {
        if (IsReplaced) return;

        if (brokenVisual != null) brokenVisual.SetActive(false);
        if (freshVisual != null) freshVisual.SetActive(true);
        IsReplaced = true;
        // TODO audio: pull-out click, then magnetic snap
    }
}