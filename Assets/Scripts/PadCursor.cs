using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A software cursor for controllers, shown only while a pointer-driven view
/// owns input (at the moment: working on an item at the repair bench).
/// The right stick moves it, RT clicks. A UI button under the cursor (the
/// circuit's Retry) is pressed directly; gameplay reads the cursor through
/// GamePointer, exactly as it reads the mouse.
///
/// Why not a virtual Mouse device: PlayerInput auto-switches control schemes
/// when a "mouse" moves, which would unpair the very pad driving it. A cursor
/// that lives in script space has no such side effects.
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class PadCursor : MonoBehaviour
{
    [Tooltip("Cursor speed at full stick deflection, in pixels per second on a 1080p screen.")]
    [SerializeField, Min(100f)] private float speed = 1150f;
    [Tooltip("Cursor diameter in pixels on a 1080p screen.")]
    [SerializeField, Min(8f)] private float size = 34f;

    private static PadCursor instance;
    private static int requestedFrame = -10;

    private bool active;
    private Vector2 position;
    private Vector2 delta;
    private int consumedFrame = -1;
    private bool consumedHold;
    private Canvas canvas;
    private RectTransform ring;
    private Texture2D texture;
    private Sprite sprite;
    private readonly List<RaycastResult> hits = new();

    public static bool IsActive => instance != null && instance.active;
    public static Vector2 Position => instance != null && instance.active
        ? instance.position : new Vector2(Screen.width * .5f, Screen.height * .5f);
    public static Vector2 Delta => IsActive ? instance.delta : Vector2.zero;
    /// <summary>True while the current RT press was spent on a UI button.</summary>
    public static bool PressConsumed => instance != null
        && (instance.consumedFrame == Time.frameCount || instance.consumedHold);

    /// <summary>Views that need a pointer call this every frame they want one.</summary>
    public static void Request() => requestedFrame = Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        requestedFrame = -10;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }

    private void Update()
    {
        PadInput.Track();
        // A request from last frame keeps the cursor alive: the views that ask
        // for it run after this component.
        bool wanted = requestedFrame >= Time.frameCount - 1 && PadInput.UsingPad && PadInput.Connected
            && Application.isFocused;
        if (wanted && !active) position = new Vector2(Screen.width * .5f, Screen.height * .5f);
        active = wanted;
        delta = Vector2.zero;
        if (!PadInput.Held(PadButton.RightTrigger)) consumedHold = false;

        if (active)
        {
            float scale = Mathf.Max(.5f, Screen.height / 1080f);
            Vector2 stick = PadInput.Curved(PadInput.RightStick);
            Vector2 next = position + stick * speed * scale * Time.unscaledDeltaTime;
            next.x = Mathf.Clamp(next.x, 0f, Mathf.Max(0f, Screen.width - 1f));
            next.y = Mathf.Clamp(next.y, 0f, Mathf.Max(0f, Screen.height - 1f));
            delta = next - position;
            position = next;
            if (PadInput.Pressed(PadButton.RightTrigger) && PressUiAt(position))
            {
                consumedFrame = Time.frameCount;
                consumedHold = true;
            }
        }
        Draw();
    }

    private bool PressUiAt(Vector2 screen)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return false;
        var data = new PointerEventData(events) { position = screen, button = PointerEventData.InputButton.Left };
        hits.Clear();
        events.RaycastAll(data, hits);
        if (hits.Count == 0 || hits[0].gameObject == null) return false;
        // Only the topmost UI element counts, as it would for the mouse.
        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        if (handler == null) return false;
        Selectable selectable = handler.GetComponent<Selectable>();
        if (selectable != null && !selectable.IsInteractable()) return true;
        data.pointerCurrentRaycast = hits[0];
        data.pointerPressRaycast = hits[0];
        data.rawPointerPress = hits[0].gameObject;
        data.pointerPress = handler;
        data.eligibleForClick = true;
        ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
        return true;
    }

    private void Draw()
    {
        if (!active)
        {
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }
        if (canvas == null) Build();
        canvas.enabled = true;
        float scale = Mathf.Max(.5f, Screen.height / 1080f);
        bool pressed = PadInput.Held(PadButton.RightTrigger);
        ring.sizeDelta = Vector2.one * size * scale * (pressed ? .8f : 1f);
        ring.position = position;
    }

    private void Build()
    {
        var root = new GameObject("Pad cursor", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var image = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ring = image.GetComponent<RectTransform>();
        ring.SetParent(root.transform, false);
        ring.anchorMin = ring.anchorMax = Vector2.zero;
        ring.pivot = new Vector2(.5f, .5f);
        texture = MakeRing(64);
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        var graphic = image.GetComponent<Image>();
        graphic.sprite = sprite;
        graphic.raycastTarget = false;
    }

    // White ring and centre dot with a dark rim, readable on any surface.
    private static Texture2D MakeRing(int resolution)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = "Pad cursor ring",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[resolution * resolution];
        float centre = (resolution - 1) * .5f;
        float unit = resolution / 64f;
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre)) / unit;
                float white = Mathf.Max(Band(d, 23.5f, 28f), Band(d, -1f, 3.2f));
                float dark = Mathf.Max(Band(d, 21.5f, 30f), Band(d, -1f, 5f));
                float alpha = Mathf.Max(white, dark * .75f);
                byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(24f, 255f, white));
                pixels[y * resolution + x] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    // 1 inside [inner, outer], fading over one pixel at each edge.
    private static float Band(float d, float inner, float outer)
        => Mathf.Clamp01(d - inner + .5f) * Mathf.Clamp01(outer - d + .5f);
}
