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
/// GamePointer, exactly as it reads the mouse. With the sticks swapped
/// (GameSettings.SwapBenchSticks, 12 Oct: for players who expect the right
/// stick to turn things) the LEFT stick moves it at the bench, and the right
/// one turns the device (ItemInspector reads the same setting).
///
/// Aim help (playtest 3, 7 Oct 2026: "very hard to get the controller cursor onto an item"): the view offers the
/// points worth landing on each frame (OfferSnapPoints: the bench's parts, grime and tools), and when the stick lets go
/// within about 60 px of one (on a 1080p screen) the cursor glides onto it. The view can also glide it somewhere
/// (GlideTo: the bench's D-pad steps). With aim help switched off (AimAssist.Enabled) it never snaps.
///
/// Friction (the forgiving bench, 14 Oct 2026; systems-forgiving-bench.md §4.6): while the cursor is over the hit area of
/// a target of the step to do now, the bench asks for 0.55 of the stick's speed (Friction), so a thumb that overshoots a 4 mm
/// screw lingers on it instead of sailing past. Aim help only, like the snap.
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
    // Aim help: the points offered this frame or last, a glide under way, and whether the stick was moving last frame.
    private static readonly List<Vector2> snapPoints = new();
    private static int snapFrame = -10;
    private bool gliding, stickMoving;
    private Vector2 glideTo;
    private Vector2 pushFrom;   // where the stick's current push took the cursor from (the glide never undoes a push: Nearest)
    /// <summary>How near a point (pixels on a 1080p screen) the stick must let go for the cursor to settle on it.</summary>
    public const float SnapPixels = 60f;
    /// <summary>The glide's time constant, seconds (about three of these to land).</summary>
    public const float GlideSeconds = .03f;
    // Friction: the share of its speed the cursor moves at this frame (the bench asks every frame it wants it).
    private static float friction = 1f;
    private static int frictionFrame = -10;
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

    // The cursor's "use": RT, and at the bench A too (12 Oct, "A uses, X takes it": ItemInspector), so A presses a button under
    // the cursor there (the circuit's retry) the way RT does, and that press is spent on it.
    private static bool UsePressedNow => PadInput.Pressed(PadButton.RightTrigger) || ItemInspector.Working && PadInput.Pressed(PadButton.South);
    private static bool UseHeld => PadInput.Held(PadButton.RightTrigger) || ItemInspector.Working && PadInput.Held(PadButton.South);

    // The stick that moves the hand: the right one; at the bench with the sticks swapped, the left one (the right turns the
    // device there, ItemInspector). The cursor only ever shows at the bench, so the swap reaches nothing else.
    private static Vector2 HandStick => ItemInspector.Working && GameSettings.SwapBenchSticks ? PadInput.LeftStick : PadInput.RightStick;

    /// <summary>Views that need a pointer call this every frame they want one.</summary>
    public static void Request() => requestedFrame = Time.frameCount;

    /// <summary>The points worth landing on (screen pixels), offered each frame by the view that asked for the cursor.</summary>
    public static void OfferSnapPoints(List<Vector2> points)
    {
        snapPoints.Clear();
        if (points != null) snapPoints.AddRange(points);
        snapFrame = Time.frameCount;
    }

    /// <summary>Glides the cursor to <paramref name="screen"/> (pixels), unless the stick takes over first.</summary>
    public static void GlideTo(Vector2 screen)
    {
        if (instance == null || !instance.active) return;
        instance.glideTo = screen;
        instance.gliding = true;
    }

    /// <summary>
    /// Slows the cursor to <paramref name="share"/> of its speed for this frame and the next (aim help only): the bench asks
    /// while the cursor is over a current-step target's hit area (0.55, §4.6).
    /// </summary>
    public static void Friction(float share)
    {
        friction = Mathf.Clamp(share, .1f, 1f);
        frictionFrame = Time.frameCount;
    }

    /// <summary>The share of its speed the cursor moves at now: under 1 over a current-step target with aim help on (checks).</summary>
    public static float FrictionNow => AimAssist.Enabled && frictionFrame >= Time.frameCount - 1 ? friction : 1f;

    /// <summary>A glide is under way (checks).</summary>
    public static bool Gliding => instance != null && instance.gliding;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        requestedFrame = -10;
        snapPoints.Clear();
        snapFrame = -10;
        friction = 1f;
        frictionFrame = -10;
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
        // A lab check drives itself with a virtual pad whether or not the editor is in front (7 Oct: the bench check ran
        // on a Mac whose Unity was on another desktop); for a player, no focus means no cursor.
        bool wanted = requestedFrame >= Time.frameCount - 1 && PadInput.UsingPad && PadInput.Connected
            && (Application.isFocused || CafeLab.Active);
        if (wanted && !active) { position = new Vector2(Screen.width * .5f, Screen.height * .5f); gliding = false; stickMoving = false; }
        active = wanted;
        delta = Vector2.zero;
        if (!UseHeld) consumedHold = false;

        if (active)
        {
            float scale = Mathf.Max(.5f, Screen.height / 1080f);
            Vector2 stick = PadInput.Curved(HandStick);
            Vector2 next = position;
            if (stick != Vector2.zero)
            {
                // The stick has the cursor: any glide stops.
                gliding = false;
                if (!stickMoving) pushFrom = position;
                stickMoving = true;
                next = position + stick * speed * scale * FrictionNow * Time.unscaledDeltaTime;
            }
            else
            {
                // Just let go near something worth landing on: settle onto it (never undoing the push: Nearest).
                if (stickMoving && AimAssist.Enabled && snapFrame >= Time.frameCount - 1
                    && Nearest(position, SnapPixels * scale, position - pushFrom, out Vector2 snap))
                {
                    glideTo = snap;
                    gliding = true;
                }
                stickMoving = false;
                if (gliding)
                {
                    float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / GlideSeconds);
                    next = Vector2.Lerp(position, glideTo, k);
                    if ((glideTo - next).sqrMagnitude < .25f) { next = glideTo; gliding = false; }
                }
            }
            next.x = Mathf.Clamp(next.x, 0f, Mathf.Max(0f, Screen.width - 1f));
            next.y = Mathf.Clamp(next.y, 0f, Mathf.Max(0f, Screen.height - 1f));
            delta = next - position;
            position = next;
            if (UsePressedNow && PressUiAt(position))
            {
                consumedFrame = Time.frameCount;
                consumedHold = true;
            }
        }
        Draw();
    }

    // The offered point nearest the cursor, within a radius (pixels), that doesn't undo the push just let go: ahead of it or
    // beside it, or behind it by under half its length (a flick that went past its target settles back onto it).
    //
    // 15 Oct, Bench 8 on his Mac (a pad at 15 fps): the first push for the screwdriver on the rest went past it, and every small
    // push back after was pulled back by the glide to the point it had just been nudged off, a tug of war of 8 s (206 to 244 px,
    // fourteen pushes) that ended with the screwdriver not taken. A nudge away from a point is never pulled back onto it now.
    private static bool Nearest(Vector2 from, float radius, Vector2 push, out Vector2 point)
    {
        point = from;
        float best = radius * radius;
        bool found = false;
        float pushed = push.magnitude;
        Vector2 way = pushed > 1e-3f ? push / pushed : Vector2.zero;
        foreach (Vector2 p in snapPoints)
        {
            Vector2 d = p - from;
            float sq = d.sqrMagnitude;
            if (sq > best) continue;
            if (Vector2.Dot(d, way) < 0f && sq > .25f * pushed * pushed) continue;   // behind it, by half the push or more
            best = sq; point = p; found = true;
        }
        return found;
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
        bool pressed = UseHeld;
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
