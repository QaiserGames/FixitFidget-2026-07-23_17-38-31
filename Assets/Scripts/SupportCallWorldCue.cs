using UnityEngine;

// Depth-tested world cue; tickets cover off-screen/occluded obligations.
public sealed class SupportCallWorldCue : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float minimumWorldSize = .45f;
    [SerializeField, Min(16f)] private float minimumPixels = 52f;
    [SerializeField, Min(.1f)] private float maximumWorldSize = 1.8f;
    [SerializeField, Min(0f)] private float aboveDevice = .25f;
    private HoldCallJob job;
    private Camera cam;
    private Canvas canvas;
    private RectTransform marker;
    private SupportCallIcon icon;
    private Renderer[] deviceRenderers;
    private void Start()
    {
        job = GetComponent<HoldCallJob>();
        deviceRenderers = GetComponentsInChildren<Renderer>();
        var go = new GameObject("Support phone handset cue", typeof(RectTransform), typeof(Canvas));
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        marker = (RectTransform)go.transform; marker.sizeDelta = new Vector2(100, 100);
        var rect = RepairOverlayUI.Rect("Handset", marker, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(100, 100));
        icon = rect.gameObject.AddComponent<SupportCallIcon>(); icon.raycastTarget = false;
        canvas.gameObject.SetActive(false);
    }
    private void LateUpdate()
    {
        if (canvas == null) return;
        if (cam == null) cam = Camera.main;
        bool visible = job != null && job.CanOperate && !job.IsComplete && cam != null;
        canvas.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 position = job.transform.position;
        float top = position.y;
        foreach (var renderer in deviceRenderers)
            if (renderer != null && renderer.enabled) top = Mathf.Max(top, renderer.bounds.max.y);
        float depth = Vector3.Dot(position - cam.transform.position, cam.transform.forward);
        if (depth <= cam.nearClipPlane) { canvas.gameObject.SetActive(false); return; }
        float viewHeight = cam.orthographic ? cam.orthographicSize * 2
            : 2 * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
        float size = Mathf.Clamp(viewHeight * minimumPixels / Mathf.Max(1, cam.pixelHeight),
            minimumWorldSize, Mathf.Max(minimumWorldSize, maximumWorldSize));
        position.y = top + aboveDevice + size * .5f;
        marker.SetPositionAndRotation(position, cam.transform.rotation);
        marker.localScale = Vector3.one * (size / 100f);
        icon.Show(job.CurrentPhase, Time.time);
    }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
}
