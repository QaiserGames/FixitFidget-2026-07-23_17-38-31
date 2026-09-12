using UnityEngine;

// A small, complete track carries a draining freshness arc. Its size stays modest
// in both the close dispenser view and the shop camera.
[DefaultExecutionOrder(11000)]
public sealed class DrinkFreshnessRing : MonoBehaviour
{
    private DrinkJob cup;
    private PlayerCarry carry;
    private PlayerInteractor player;
    private ConversationController dialogue;
    private ItemInspector inspector;
    private CounterRepairView counter;
    private Camera view;
    private Transform visual;
    private LineRenderer track, fill;
    private Renderer[] solidRenderers;
    private Material material;
    private const int Segments = 64;

    private void Awake()
    {
        cup = GetComponent<DrinkJob>();
        carry = FindAnyObjectByType<PlayerCarry>();
        player = carry != null ? carry.GetComponent<PlayerInteractor>() : null;
        dialogue = carry != null ? carry.GetComponent<ConversationController>() : null;
        inspector = carry != null ? carry.GetComponent<ItemInspector>() : null;
        counter = carry != null ? carry.GetComponent<CounterRepairView>() : null;
        view = Camera.main;
        solidRenderers = GetComponentsInChildren<MeshRenderer>(true);
        visual = new GameObject("Cup freshness indicator").transform;
        visual.SetParent(transform, false);
        material = new Material(Shader.Find("Sprites/Default"));
        track = Make("Full freshness track", new Color(.20f, .24f, .24f, .90f));
        fill = Make("Remaining freshness", new Color(.32f, .92f, .54f));
        // Keep the renderer flags enabled and toggle only this child object.
        // Carrying records renderer flags, including initially hidden indicators.
        visual.gameObject.SetActive(false);
    }

    private LineRenderer Make(string label, Color color)
    {
        var child = new GameObject(label);
        child.transform.SetParent(visual, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.startColor = line.endColor = color;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void LateUpdate()
    {
        if (view == null) view = Camera.main;
        bool visible = cup != null && !cup.IsEmpty && !cup.Locked && cup.HasFreshness && view != null
            && Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation)
            && (inspector == null || !inspector.IsHoldingItem)
            && (counter == null || !counter.OwnsInput);
        if (visible && carry != null && carry.Contains(cup) && player != null && player.IsAtStation
            && player.CurrentStation.GetComponent<BeverageStation>() == null) visible = false;
        visual.gameObject.SetActive(visible);
        if (!visible) return;

        bool found = false;
        Bounds bounds = default;
        foreach (var renderer in solidRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        Vector3 centre = found ? bounds.center : transform.position + Vector3.up * .07f;
        Vector3 projected = view.WorldToScreenPoint(centre);
        if (projected.z <= view.nearClipPlane) { visual.gameObject.SetActive(false); return; }
        float scale = Mathf.Clamp(Screen.height / 900f, .7f, 1.4f);
        float radiusPixels = 10f * scale;
        float worldPerPixel = view.orthographic ? view.orthographicSize * 2 / Mathf.Max(1, Screen.height)
            : 2 * projected.z * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, Screen.height);
        float radius = radiusPixels * worldPerPixel;
        // Project the highest physical corner so the ring sits above the cup,
        // irrespective of its carried scale or the shop camera's angle.
        float highest = projected.y;
        if (found)
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        highest = Mathf.Max(highest, view.WorldToScreenPoint(corner).y);
                    }
        projected.y = highest + radiusPixels + 4 * scale;
        centre = view.ScreenToWorldPoint(projected);
        Color color = cup.FreshnessStage == DrinkFreshness.Stage.Cold ? new Color(.95f, .25f, .18f)
            : cup.FreshnessStage == DrinkFreshness.Stage.Cooling ? new Color(1f, .68f, .18f)
            : new Color(.32f, .92f, .54f);
        float fraction = Mathf.Clamp01(cup.FreshnessRemaining);
        if (fraction > .995f) fraction = 1;
        track.startColor = track.endColor = cup.FreshnessStage == DrinkFreshness.Stage.Cold
            ? color : new Color(.20f, .24f, .24f, .9f);
        fill.startColor = fill.endColor = color;
        Draw(track, centre, view, radius, 1);
        fill.gameObject.SetActive(fraction > .001f);
        if (fraction > .001f) Draw(fill, centre - view.transform.forward * worldPerPixel, view, radius, fraction);
    }

    private static void Draw(LineRenderer line, Vector3 centre, Camera cam, float radius, float fraction)
    {
        bool closed = fraction >= .9999f;
        int count = closed ? Segments : Mathf.Max(2, Mathf.CeilToInt(Segments * fraction) + 1);
        line.loop = closed;
        line.positionCount = count;
        line.startWidth = line.endWidth = radius * .16f;
        for (int i = 0; i < count; i++)
        {
            float angle = (90 - 360 * fraction * i / (closed ? count : count - 1)) * Mathf.Deg2Rad;
            line.SetPosition(i, centre + radius * (cam.transform.right * Mathf.Cos(angle) + cam.transform.up * Mathf.Sin(angle)));
        }
    }

    private void OnDisable() { if (visual != null) visual.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
    }
}
