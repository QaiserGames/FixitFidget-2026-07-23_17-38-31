using UnityEngine;

public enum ToolType { Hand, Brush, Screwdriver, Pry, Tweezers }

public class ToolPickup : MonoBehaviour
{
    public ToolType tool;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void SetSelected(bool on)
    {
        transform.localScale = on ? baseScale * 1.25f : baseScale;
    }
}