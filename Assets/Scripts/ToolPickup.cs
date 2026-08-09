using UnityEngine;

public enum ToolType { Hand, Brush, Screwdriver, Pry, Tweezers }

public class ToolPickup : MonoBehaviour
{
    public ToolType tool;

    [Tooltip("Shown in the hover tooltip.")]
    public string displayName = "Tool";

    [Tooltip("Tint so tools are distinguishable at a glance.")]
    public Color tint = Color.white;

    private Vector3 baseScale;
    private Renderer rend;

    private void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = tint;
    }

    public void SetSelected(bool on)
    {
        transform.localScale = on ? baseScale * 1.25f : baseScale;
        if (rend != null)
            rend.material.color = on ? Color.Lerp(tint, Color.white, 0.5f) : tint;
    }
}