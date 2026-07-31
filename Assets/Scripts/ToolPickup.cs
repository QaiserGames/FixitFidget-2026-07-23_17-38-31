using UnityEngine;

public enum ToolType { Hand, Brush }

public class ToolPickup : MonoBehaviour
{
    public ToolType tool;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    // Swell a bit while selected so the player always knows what's in hand.
    public void SetSelected(bool on)
    {
        transform.localScale = on ? baseScale * 1.25f : baseScale;
    }
}