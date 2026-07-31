using UnityEngine;

public class PatienceBar : MonoBehaviour
{
    [SerializeField] private Renderer barRenderer;

    private Vector3 fullScale;

    private void Awake()
    {
        fullScale = transform.localScale;
    }

    public void SetFraction(float f, Color fullColor)
    {
        f = Mathf.Clamp01(f);

        // Never let the mesh shrink to literally zero — that was causing the flash.
        float width = Mathf.Max(f, 0.02f);
        transform.localScale = new Vector3(fullScale.x * width, fullScale.y, fullScale.z);

        barRenderer.material.color = Color.Lerp(Color.red, fullColor, f);
    }
}