using UnityEngine;

// One world cue per cup, billboarded and sized to remain visible from the shop camera.
public sealed class DrinkFreshnessRing : MonoBehaviour
{
    private DrinkJob cup;
    private LineRenderer track, fill;
    private Material material;
    private const int Segments = 40;
    private void Awake()
    {
        cup = GetComponent<DrinkJob>();
        material = new Material(Shader.Find("Sprites/Default"));
        track = Make("Freshness track", new Color(.08f, .1f, .1f, .85f));
        fill = Make("Freshness", Color.green);
    }
    private LineRenderer Make(string label, Color color)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        bool visible = cup != null && cup.HasFreshness && !cup.UsesDispenserVisual && cam != null;
        var carry = FindAnyObjectByType<PlayerCarry>();
        var player = carry != null ? carry.GetComponent<PlayerInteractor>() : null;
        if (carry != null && carry.Contains(cup) && player != null && player.IsAtStation) visible = false;
        track.enabled = fill.enabled = visible;
        if (!visible) return;
        Vector3 centre = transform.position + Vector3.up * .22f;
        float distance = Vector3.Distance(cam.transform.position, centre);
        float radius = cam.orthographic ? cam.orthographicSize * .019f
            : Mathf.Max(.055f, distance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) * .019f);
        Color color = cup.FreshnessStage == DrinkFreshness.Stage.Cold ? new Color(.95f, .18f, .13f)
            : cup.FreshnessStage == DrinkFreshness.Stage.Cooling ? new Color(1f, .64f, .12f) : new Color(.24f, .9f, .4f);
        fill.startColor = fill.endColor = color;
        Draw(track, centre, cam, radius, 1);
        Draw(fill, centre, cam, radius, cup.FreshnessStage == DrinkFreshness.Stage.Cold ? 1 : cup.FreshnessRemaining);
    }
    private static void Draw(LineRenderer line, Vector3 centre, Camera cam, float radius, float fraction)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(Segments * fraction) + 1);
        line.positionCount = count; line.startWidth = line.endWidth = radius * .18f;
        for (int i = 0; i < count; i++)
        {
            float angle = (90 - 360 * fraction * i / (count - 1)) * Mathf.Deg2Rad;
            line.SetPosition(i, centre + radius * (cam.transform.right * Mathf.Cos(angle) + cam.transform.up * Mathf.Sin(angle)));
        }
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
