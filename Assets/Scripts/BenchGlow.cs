using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE GLOW ON THE NEXT THING TO TOUCH, AND "WHAT'S LEFT" (checklist v2, 14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md
// §5.4 and §5.5; the Producer's call 10: the glow is always on, no setting)
//
// His decision 7 (14 Oct, 05:59): "the next thing to touch glows softly on the device (colour and shape in the world:
// allowed)". The card (BenchCards) says which tool; this says where, on the bench itself:
//
//   * until the step's tool is in hand, the tool glows in its stand; then the step's targets (RepairChecklist.Step.targets,
//     what is still to do): a part, a cover or a tool gets the bench's own outline (BenchOutline's guide layer), a screw, a
//     hole or a dirt spot a ring on the screen round it (3 px at 1080p, its projected size + 10 px, at least 14);
//   * mint (UiSkin.BrandBright, "a glow"), breathing every 1.6 s: the outline 1.0x to 1.5x and mint to soft mint, a ring's
//     radius +-3 px and its alpha 0.7 to 1. Mint (glow), white (hover) and gold (a hold, what's left) are told apart by motion
//     and shape, not hue alone (Xbox accessibility guideline 103);
//   * a target something nearer covers (ItemInspector.IsSeen false) is drawn dashed: the outline's x-ray pass, a dashed
//     ring (KeenGamer on ReStory: "highlight parts hidden under other parts");
//   * work on the face turned down (the step's `under`): the whole device breathes, and the card says flip;
//   * on one part a hold under way (gold) and the cursor (white) come first: the outline's pointer layer is drawn over the
//     guide's, and a ring stands aside while its target is hovered or held;
//   * a tick: the next step's glow starts 0.3 s later (the eye goes to the tick, then back); at "Fixed!" and when the
//     close-up ends, nothing glows;
//   * stuck (12 s with no progress: BenchCards' clock): one bright pulse, 2.2x and white to mint over 0.5 s.
//
// "WHAT'S LEFT" (Tab / View, or by itself on the first repair: BenchWork.Flash raises Flashed, whoever called it). For 1.5 s
// every part with work left in any step is outlined in gold, steady (dashed where hidden); screws and holes get a gold ring;
// each dirt spot left sends out two gold rings, from its edge to twice its size (0.1-0.6 s, again at 0.5 s), while its dirt
// itself brightens gold (GrimeSpot.Flash, the bench's); everything fades out from 0.5 s. With nothing left, the device's
// outline flashes white once (0.4 s). The guide's glow steps aside meanwhile. bench.showleft plays on the press (a sound
// asked for twice in one frame plays once: the bank's minimum gap).
//
// Made while playing, on a canvas of its own under the cards (15 < 20). Nothing is saved; nothing is allocated per frame once
// the targets' renderers are known (lists and rings are kept and reused). Timed on UiClock, a pure function of the time, so
// 15 fps breathes as 60 does.
// ---------------------------------------------------------------------------
public sealed class BenchGlow : MonoBehaviour
{
    const int SortingOrder = 15;
    const float Breath = 1.6f, WaitAfterTick = .3f, KindsEvery = .5f, SeenEvery = .1f;
    const float RingStroke = 3f, RingPad = 10f, RingMin = 14f, RingBreathe = 3f, RingSmallest = 22f;
    const float PulseSeconds = .5f, PulseWidth = 2.2f, BreathWidth = 1.5f;
    const float FadeFrom = .5f, WaveFrom = .1f, WaveSeconds = .5f, SecondWave = .5f, NothingLeftSeconds = .4f;

    public static BenchGlow Instance { get; private set; }

    // ------------------------------------------------------------------ what the labs read

    /// <summary>How many things glow now (outlines and rings, the tool and the device included).</summary>
    public static int Count => Instance != null ? Instance.glowing.Count : 0;
    /// <summary>The things glowing now: the step's targets, or the tool in its stand, or the device.</summary>
    public static IReadOnlyList<Component> Glowing => Instance != null ? Instance.glowing : null;
    /// <summary>Whether <paramref name="thing"/> (or the thing it is part of) glows now.</summary>
    public static bool IsGlowing(Component thing) => Instance != null && thing != null && Instance.glowing.Contains(thing);
    /// <summary>How many of the glows are drawn dashed (hidden behind something) now.</summary>
    public static int DashedCount => Instance != null ? Instance.dashed : 0;
    /// <summary>The tool glowing in its stand (the step's tool, not yet in hand), or null.</summary>
    public static ToolPickup GlowingTool => Instance != null ? Instance.tool : null;
    /// <summary>The whole device breathes (the step's work lies on the face turned down).</summary>
    public static bool DeviceGlowing => Instance != null && Instance.deviceGlow;
    /// <summary>"What's left" is on the screen.</summary>
    public static bool Flashing => Instance != null && Instance.flashJob != null && UiClock.Now - Instance.flashAt < Instance.flashFor;
    /// <summary>How many things the last "what's left" showed.</summary>
    public static int FlashCount => Instance != null ? Instance.flashItems.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<BenchGlow>() != null) return;
        var host = new GameObject("Bench glow (runtime)");
        DontDestroyOnLoad(host);
        host.AddComponent<BenchGlow>();
    }

    // ------------------------------------------------------------------ state

    struct FlashItem
    {
        public Component target;
        public BenchWork.Kind kind;
        public bool hidden;
    }

    Canvas canvas;
    RectTransform root;
    Camera cam;
    JobBase job;
    ToolPickup[] tools;
    ToolPickup tool;
    bool deviceGlow;
    int dashed;

    readonly List<Component> glowing = new();
    readonly List<GameObject> hullsNow = new(), hullsShown = new();
    readonly List<BenchRing> rings = new();
    int ringsUsed;

    readonly List<BenchWork.Item> work = new();
    readonly Dictionary<Component, BenchWork.Kind> kinds = new();
    float kindsAt = -10f;
    readonly Dictionary<Component, List<Renderer>> renderers = new();
    readonly Dictionary<Component, float> seenAt = new();
    readonly Dictionary<Component, bool> seen = new();

    JobBase flashJob;
    float flashAt = -10f, flashFor, nothingLeftAt = -10f;
    readonly List<FlashItem> flashItems = new();

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable() => BenchWork.Flashed += OnFlashed;

    void OnDisable()
    {
        BenchWork.Flashed -= OnFlashed;
        AllOff();
    }

    // ------------------------------------------------------------------ "what's left"

    void OnFlashed(JobBase flashed, float seconds)
    {
        ItemInspector inspector = ItemInspector.Instance;
        if (flashed == null || inspector == null || inspector.FocusedItem != flashed) return;
        // The press's shimmer (Audio's bench.showleft). If the bench plays it too, the bank's minimum gap plays it once.
        Sfx.Play2D("bench.showleft");
        float now = UiClock.Now;
        flashJob = flashed;
        flashAt = now;   // a second press restarts it
        flashFor = Mathf.Max(.5f, seconds);
        flashItems.Clear();
        BenchWork.WorkLeft(flashed, work);
        foreach (BenchWork.Item item in work)
            if (item.target != null) flashItems.Add(new FlashItem { target = item.target, kind = item.kind, hidden = item.hidden });
        nothingLeftAt = flashItems.Count == 0 ? now : -10f;
    }

    // ------------------------------------------------------------------ every frame

    void LateUpdate()
    {
        ItemInspector inspector = ItemInspector.Instance;
        JobBase focused = inspector != null ? inspector.FocusedItem : null;
        if (focused != job) Begin(focused);
        bool on = focused != null && Time.timeScale > 0f && !RecapUI.Showing && !focused.IsFinished;
        if (!on && !(focused != null && Time.timeScale > 0f && UiClock.Now - nothingLeftAt < NothingLeftSeconds))
        {
            AllOff();
            return;
        }
        if (canvas == null) Build();
        if (cam == null) cam = Camera.main;
        float now = UiClock.Now;

        glowing.Clear();
        hullsNow.Clear();
        ringsUsed = 0;
        dashed = 0;
        tool = null;
        deviceGlow = false;

        if (now - kindsAt >= KindsEvery) Kinds(focused, now);

        bool flashing = flashJob == focused && now - flashAt < flashFor;
        if (flashing) WhatsLeft(focused, now);
        else if (now - nothingLeftAt < NothingLeftSeconds) NothingLeft(focused, now);
        else if (on) Guide(inspector, focused, now);

        // Outlines lit last frame and not this one go out; rings past the ones used are hidden.
        foreach (GameObject go in hullsShown)
            if (go != null && !hullsNow.Contains(go)) BenchOutline.Glow(go, false, BenchOutline.Guide, 1f, false);
        hullsShown.Clear();
        hullsShown.AddRange(hullsNow);
        for (int i = ringsUsed; i < rings.Count; i++)
            if (rings[i].gameObject.activeSelf) rings[i].gameObject.SetActive(false);
    }

    void Begin(JobBase focused)
    {
        AllOff();
        job = focused;
        renderers.Clear();
        seenAt.Clear();
        seen.Clear();
        kinds.Clear();
        kindsAt = -10f;
        tools = focused != null ? FindObjectsByType<ToolPickup>(FindObjectsSortMode.None) : null;
        if (flashJob != focused) { flashJob = null; flashItems.Clear(); nothingLeftAt = -10f; }
        cam = Camera.main;
    }

    void AllOff()
    {
        foreach (GameObject go in hullsShown) if (go != null) BenchOutline.Glow(go, false, BenchOutline.Guide, 1f, false);
        hullsShown.Clear();
        foreach (BenchRing ring in rings) if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
        glowing.Clear();
        tool = null;
        deviceGlow = false;
        dashed = 0;
    }

    // What the bench says each target is (a screw, a hole, a spot: a ring; a cover or a part: an outline), from its own list
    // of what's left (BenchWork.WorkLeft), looked at twice a second.
    void Kinds(JobBase focused, float now)
    {
        kindsAt = now;
        kinds.Clear();
        BenchWork.WorkLeft(focused, work);
        foreach (BenchWork.Item item in work)
            if (item.target != null) kinds[item.target] = item.kind;
    }

    // ------------------------------------------------------------------ the guide

    void Guide(ItemInspector inspector, JobBase focused, float now)
    {
        RepairChecklist.Step step = BenchCards.CurrentStep;
        if (step == null || now - BenchCards.StepSince < WaitAfterTick) return;

        // The breath (1.6 s) and the stuck pulse (one, 0.5 s, 2.2x and white to mint).
        float breath = .5f - .5f * Mathf.Cos(now * Mathf.PI * 2f / Breath);
        Color colour = Color.Lerp(BenchOutline.Guide, UiSkin.BrandSoft, breath);
        float width = Mathf.Lerp(1f, BreathWidth, breath);
        float ringAlpha = Mathf.Lerp(.7f, 1f, breath), ringGrow = Mathf.Lerp(-RingBreathe, RingBreathe, breath), stroke = RingStroke;
        float pulse = (now - BenchCards.StuckPulseAt) / PulseSeconds;
        if (pulse >= 0f && pulse < 1f)
        {
            colour = Color.Lerp(Color.white, colour, pulse);
            width = Mathf.Lerp(PulseWidth, width, pulse);
            stroke = Mathf.Lerp(RingStroke * PulseWidth, RingStroke, pulse);
            ringAlpha = 1f;
        }

        // The work lies on the face turned down: the whole device breathes (the card says flip).
        if (step.under)
        {
            deviceGlow = true;
            Hull(focused, focused.gameObject, colour, width, false);
            return;
        }

        // The step's tool, until it is in hand (on a pad, whichever LB / RB has picked is in hand).
        if (step.tool != ToolType.Hand && inspector.CurrentTool != step.tool)
        {
            tool = ToolFor(step.tool, focused);
            if (tool != null) Hull(tool, tool.gameObject, colour, width, false);
            return;
        }

        if (step.targets == null) return;
        BenchInteractable held = inspector.HoldTarget;
        foreach (Component target in step.targets)
        {
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            bool hidden = Hidden(inspector, target, now);
            if (IsRing(target))
            {
                // The cursor's white hover, or a hold under way, stands in for the ring on its own target.
                if (BenchOutline.PointerOn(target.gameObject) || held != null && held.gameObject == target.gameObject) continue;
                if (!Ring(target, WithAlpha(colour, ringAlpha), stroke, ringGrow, hidden)) continue;
                glowing.Add(target);
                if (hidden) dashed++;
            }
            else
            {
                Hull(target, target.gameObject, colour, width, hidden);
                if (hidden) dashed++;
            }
        }
    }

    static Color WithAlpha(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);

    // ------------------------------------------------------------------ "what's left", drawn

    void WhatsLeft(JobBase focused, float now)
    {
        float t = now - flashAt;
        // Steady, then fading out from 0.5 s to the end (ease-out).
        float fade = t < FadeFrom ? 1f : 1f - Mathf.Clamp01((t - FadeFrom) / Mathf.Max(.1f, flashFor - FadeFrom));
        fade = 1f - (1f - fade) * (1f - fade);
        ItemInspector inspector = ItemInspector.Instance;
        foreach (FlashItem item in flashItems)
        {
            Component target = item.target;
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            bool hidden = item.hidden || inspector != null && Hidden(inspector, target, now);
            switch (item.kind)
            {
                case BenchWork.Kind.Spot:
                    // Two rings from the spot's edge out to twice its size, the second half a second after the first.
                    Wave(target, t - WaveFrom, hidden);
                    Wave(target, t - WaveFrom - SecondWave, hidden);
                    glowing.Add(target);
                    break;
                case BenchWork.Kind.Screw:
                case BenchWork.Kind.Hole:
                    if (Ring(target, WithAlpha(BenchOutline.Left, fade), RingStroke, 0f, hidden)) glowing.Add(target);
                    break;
                default:
                    // An opaque outline can't fade: it thins out instead.
                    Hull(target, target.gameObject, BenchOutline.Left, Mathf.Max(.05f, fade), hidden);
                    break;
            }
            if (hidden) dashed++;
        }
    }

    void Wave(Component target, float since, bool hidden)
    {
        if (since < 0f || since >= WaveSeconds) return;
        float k = since / WaveSeconds;
        if (!Project(target, out Vector2 centre, out float radius)) return;
        BenchRing ring = NextRing();
        float r = Mathf.Lerp(radius, radius * 2f, k);
        ring.Shape(r, RingStroke, hidden);
        ring.color = WithAlpha(BenchOutline.Left, 1f - k);
        ring.rectTransform.anchoredPosition = centre;
    }

    // Nothing left to do: the device's outline flashes white once.
    void NothingLeft(JobBase focused, float now)
    {
        float k = (now - nothingLeftAt) / NothingLeftSeconds;
        Hull(focused, focused.gameObject, Color.white, Mathf.Max(.05f, 1.5f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI)), false);
    }

    // ------------------------------------------------------------------ drawing one glow

    void Hull(Component target, GameObject go, Color colour, float width, bool hidden)
    {
        if (go == null) return;
        BenchOutline.Glow(go, true, colour, width, hidden);
        if (!hullsNow.Contains(go)) hullsNow.Add(go);
        glowing.Add(target);
    }

    // A ring round the target's projected size + 10 px (at least 14), grown by `grow` px; false when it isn't on screen.
    bool Ring(Component target, Color colour, float stroke, float grow, bool dashedRing)
    {
        if (!Project(target, out Vector2 centre, out float radius)) return false;
        BenchRing ring = NextRing();
        ring.Shape(Mathf.Max(RingMin, radius + RingPad) + grow, stroke, dashedRing);
        ring.color = dashedRing ? new Color(colour.r, colour.g, colour.b, colour.a * .7f) : colour;
        ring.rectTransform.anchoredPosition = centre;
        return true;
    }

    BenchRing NextRing()
    {
        if (ringsUsed >= rings.Count)
        {
            var go = new GameObject("Ring " + (rings.Count + 1), typeof(RectTransform));
            go.transform.SetParent(root, false);
            var ring = go.AddComponent<BenchRing>();
            ring.raycastTarget = false;
            RectTransform r = ring.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(.5f, .5f);
            rings.Add(ring);
        }
        BenchRing next = rings[ringsUsed++];
        if (!next.gameObject.activeSelf) next.gameObject.SetActive(true);
        return next;
    }

    // The target's middle on the canvas and its radius there (canvas units: 1080p pixels), from its own renderers' bounds.
    bool Project(Component target, out Vector2 centre, out float radius)
    {
        centre = Vector2.zero;
        radius = 0f;
        if (cam == null || canvas == null) return false;
        if (!renderers.TryGetValue(target, out List<Renderer> list))
        {
            list = new List<Renderer>();
            target.GetComponentsInChildren(true, list);
            // Not the outline's own hulls, nor sprites, lines or texts (DeviceBounds' rule).
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null || list[i].GetComponent<BenchOutlineHull>() != null || list[i] is SpriteRenderer
                    || list[i] is LineRenderer || list[i] is ParticleSystemRenderer || list[i] is TrailRenderer) list.RemoveAt(i);
            renderers[target] = list;
        }
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in list)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) b = new Bounds(target.transform.position, Vector3.one * .004f);
        Vector3 middle = cam.WorldToScreenPoint(b.center);
        if (middle.z <= cam.nearClipPlane) return false;
        float most = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 p = cam.WorldToScreenPoint(corner);
            most = Mathf.Max(most, new Vector2(p.x - middle.x, p.y - middle.y).magnitude);
        }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        centre = new Vector2(middle.x / scale, middle.y / scale);
        radius = most / scale;
        return middle.x >= 0f && middle.y >= 0f && middle.x <= Screen.width && middle.y <= Screen.height;
    }

    // A screw, a hole or a dirt spot gets a ring; a cover, a part or a tool an outline. The bench's own list says which;
    // anything it doesn't name is a ring when it is small on the screen (under 22 px across its middle).
    bool IsRing(Component target)
    {
        if (kinds.TryGetValue(target, out BenchWork.Kind kind))
            return kind == BenchWork.Kind.Screw || kind == BenchWork.Kind.Hole || kind == BenchWork.Kind.Spot;
        if (target is GrimeSpot || target is Screw) return true;
        return Project(target, out _, out float radius) && radius < RingSmallest;
    }

    // Something nearer covers it (the bench's own sight line), asked ten times a second per target.
    bool Hidden(ItemInspector inspector, Component target, float now)
    {
        if (seenAt.TryGetValue(target, out float at) && now - at < SeenEvery && seen.TryGetValue(target, out bool was)) return !was;
        bool sight = inspector.IsSeen(target.transform);
        seenAt[target] = now;
        seen[target] = sight;
        return !sight;
    }

    // The step's tool at rest: the one of its kind nearest the device (ItemInspector's own rule for LB / RB).
    ToolPickup ToolFor(ToolType type, JobBase focused)
    {
        if (tools == null) return null;
        ToolPickup best = null;
        float nearest = float.PositiveInfinity;
        foreach (ToolPickup t in tools)
        {
            if (t == null || t.tool != type || t.InHand || !t.isActiveAndEnabled) continue;
            float d = (t.transform.position - focused.transform.position).sqrMagnitude;
            if (d < nearest) { nearest = d; best = t; }
        }
        return best;
    }

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("Bench glow (while playing)", transform, SortingOrder);
        root = (RectTransform)canvas.transform;
    }
}

/// <summary>
/// A ring drawn on the screen (the glow round a screw, a hole or a dirt spot; "what's left"): a stroke of a fixed width in
/// canvas units at any radius, solid or dashed 4 px on and 4 px off. Its mesh is built again only when its shape changes.
/// </summary>
public sealed class BenchRing : MaskableGraphic
{
    float radius = 20f, stroke = 3f;
    bool dashed;

    /// <summary>The ring's radius (to the middle of its stroke) and stroke, in canvas units, and whether it is dashed.</summary>
    public void Shape(float r, float s, bool d)
    {
        r = Mathf.Max(1f, r);
        s = Mathf.Max(.5f, s);
        if (Mathf.Abs(r - radius) < .1f && Mathf.Abs(s - stroke) < .05f && d == dashed) return;
        radius = r;
        stroke = s;
        dashed = d;
        float size = 2f * (r + s);
        rectTransform.sizeDelta = new Vector2(size, size);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float inner = Mathf.Max(0f, radius - stroke * .5f), outer = radius + stroke * .5f;
        float circumference = 2f * Mathf.PI * radius;
        Color32 c = color;
        if (!dashed)
        {
            int segments = Mathf.Clamp(Mathf.CeilToInt(circumference / 3f), 24, 180);
            for (int i = 0; i < segments; i++)
                Arc(vh, i * Mathf.PI * 2f / segments, (i + 1) * Mathf.PI * 2f / segments, inner, outer, c);
            return;
        }
        // 4 px on, 4 px off: a whole number of periods of about 8 px round the ring.
        int periods = Mathf.Max(6, Mathf.RoundToInt(circumference / 8f));
        float step = Mathf.PI * 2f / periods;
        for (int i = 0; i < periods; i++)
        {
            float a = i * step, mid = a + step * .25f, b = a + step * .5f;
            Arc(vh, a, mid, inner, outer, c);
            Arc(vh, mid, b, inner, outer, c);
        }
    }

    static void Arc(VertexHelper vh, float a, float b, float inner, float outer, Color32 c)
    {
        Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
        int start = vh.currentVertCount;
        vh.AddVert(da * inner, c, Vector2.zero);
        vh.AddVert(da * outer, c, Vector2.zero);
        vh.AddVert(db * outer, c, Vector2.zero);
        vh.AddVert(db * inner, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
