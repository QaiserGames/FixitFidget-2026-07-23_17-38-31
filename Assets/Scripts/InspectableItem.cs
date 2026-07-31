using UnityEngine;

public class InspectableItem : MonoBehaviour
{
    [SerializeField] private float hoverScale = 1.06f;

    private Vector3 savedPos;
    private Quaternion savedRot;
    private Vector3 baseScale;
    private bool hovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void SetHovered(bool on)
    {
        hovered = on;
    }

    private void Update()
    {
        // Smoothly ease toward swollen (hovered) or normal, every frame.
        Vector3 target = hovered ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, 12f * Time.deltaTime);
    }

    public void RememberPose()
    {
        savedPos = transform.position;
        savedRot = transform.rotation;
    }

    public void ReturnToPose()
    {
        transform.position = savedPos;
        transform.rotation = savedRot;
    }
}