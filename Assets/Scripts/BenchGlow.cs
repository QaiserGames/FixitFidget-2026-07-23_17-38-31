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
//   * until the step's tool is in hand, the tool glows in its stand, AND its seat on the rest gets a ring on the screen round
//     it (19 Oct: the cloth lies in a tray with 6 mm walls, and her note: the cloth's highlight was "hidden in the bottom
//     left... you might have to highlight the actual tray itself"); then the step's targets (RepairChecklist.Step.targets,
//     what is still to do): a part, a cover, a tool or the fresh part waiting on its pad gets the bench's own outline
//     (BenchOutline's guide layer), a screw, a hole or a dirt spot a ring on the screen round it (its projected size + 10 px,
//     at least 14);
//   * ONE LOOK FOR "NEXT", IN TWO TONES (19 Oct 2026; playtest 7: the fresh part "is highlighted, but it doesn't show. You
//     gotta make it red. Or highlight it."). The mint line (1.01:1 on the pale pad, 1.04:1 on the yellow cloth) is gone. The
//     next thing wears a dark inner line (ink, warmed toward the amber) and a warm amber outer line (BenchOutline.Guide,
//     #FFB238), the outline at least 4 px at 1080p, a ring 2 px of ink inside 3 px of amber; steady (never under 70 % alpha)
//     and breathing slowly, once a second, the amber brightening toward #FFD27A and the outer line widening a little. Amber,
//     not red: red reads as "wrong" (his plan of 19 Oct). The same two tones mark the fresh part on its pad, the tool on the
//     rest and its seat, a screw, a hole, a spot, and the whole device when the work lies on the face turned down;
//   * a target something nearer covers (ItemInspector.IsSeen false) is drawn dashed: the outline's x-ray pass (the amber
//     line's), a dashed ring (KeenGamer on ReStory: "highlight parts hidden under other parts");
//   * work on the face turned down (the step's `under`): the whole device breathes, and the card says flip;
//   * on one part a hold under way (gold) and the cursor (white) come first: the outline's pointer layer is drawn over the
//     guide's, and a ring stands aside while its target is hovered or held;
//   * a tick: the next step's glow starts 0.3 s later (the eye goes to the tick, then back); at "Fixed!" and when the
//     close-up ends, nothing glows;
//   * stuck (12 s with no progress: BenchCards' clock): one bright pulse, 2.2x and white to amber over 0.5 s.
//
// "WHAT'S LEFT" (Tab / View, or by itself on the first repair: BenchWork.Flash raises Flashed, whoever called it). For 1.5 s
// every part with work left in any step is outlined in gold, steady (dashed where hidden); screws and holes get a gold ring;
// each dirt spot left sends out two gold rings, from its edge to twice its size (0.1-0.6 s, again at 0.5 s), while its dirt
// itself brightens gold (GrimeSpot.Flash, the bench's); everything fades out from 0.5 s. With nothing left, the device's
// outline flashes white once (0.4 s). The guide's glow steps aside meanwhile. bench.showleft plays on the press (a sound
// asked for twice in one frame plays once: the bank's minimum gap). Since 19 Oct the gold is two-tone too (the ink inside
// it), so it reads on the pad and the cloth; it is told from the guide's amber by what it does (everything, for 1.5 s), not
// by hue alone (Xbox accessibility guideline 103: motion and shape, never hue by itself).
//
// WHERE TO SET IT DOWN (19 Oct 2026; her: "highlight wherever you have to take the actual item ... where you're going to
// place the phone or the watch ... I don't know where to go"). While Ace carries a device that still needs the bench
// (PlayerCarry.BenchBound) and no close-up is open, the device's own place on the repair mat (its layout's rest point,
// BenchStage) wears the same two tones: a rounded ring on the mat itself, around the footprint the device will take, in the
// world (first person, the walking view, the overhead alike), and a ring on the screen at that point at least 26 px across so
// it still shows from the overhead, where the whole mat is a few pixels. Both breathe with the guide. It goes the moment the
// device is set down or the close-up opens.
//
// Made while playing, on a canvas of its own under the cards (15 < 20). Nothing is saved; nothing is allocated per frame once
// the targets' renderers are known (lists and rings are kept and reused). Timed on UiClock, a pure function of the time, so
// 15 fps breathes as 60 does.
// ---------------------------------------------------------------------------
public sealed class BenchGlow : MonoBehaviour
{
    const int SortingOrder = 15;
    const float Breath = 1f, WaitAfterTick = .3f, KindsEvery = .5f, SeenEvery = .1f;
    const float RingStroke = 3f, RingInner = 2f, RingPad = 10f, RingMin = 14f, RingBreathe = 3f, RingSmallest = 22f;
    const float PulseSeconds = .5f, PulseWidth = 2.2f, BreathWidth = 2f, RingAlphaLow = .7f;
    const float FadeFrom = .5f, WaveFrom = .1f, WaveSeconds = .5f, SecondWave = .5f, NothingLeftSeconds = .4f;
    /// <summary>The seat ring round a tool on the rest: its box on screen + this (1080p px).</summary>
    const float SeatPad = 12f;
    /// <summary>The put-down ring: at least this radius on the screen (the overhead draws the whole mat at 20-odd px).</summary>
    const float PutDownRingMin = 26f;
    /// <summary>The put-down mark on the mat: the device's footprint grown by this (metres), the ink band and the amber band.</summary>
    const float PutDownMargin = .02f, PutDownInk = .004f, PutDownAmber = .007f, PutDownBreathe = .003f, PutDownLift = .0008f;

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
    /// <summary>The tool on the rest has its seat ringed on the screen too (19 Oct).</summary>
    public static bool SeatRinged => Instance != null && Instance.seatRinged;
    /// <summary>The whole device breathes (the step's work lies on the face turned down).</summary>
    public static bool DeviceGlowing => Instance != null && Instance.deviceGlow;
    /// <summary>"What's left" is on the screen.</summary>
    public static bool Flashing => Instance != null && Instance.flashJob != null && UiClock.Now - Instance.flashAt < Instance.flashFor;
    /// <summary>How many things the last "what's left" showed.</summary>
    public static int FlashCount => Instance != null ? Instance.flashItems.Count : 0;
    /// <summary>The device's place on the mat is marked for a carried device (19 Oct), and where (the mark's middle, world).</summary>
    public static bool PutDownShowing => Instance != null && Instance.putDownShown;
    public static Vector3 PutDownPoint => Instance != null ? Instance.putDownAt : Vector3.zero;
    /// <summary>The guide's two tones now, as drawn this frame (labs: the inner ink, the outer amber).</summary>
    public static Color GuideInnerNow => Instance != null ? Instance.innerNow : BenchOutline.GuideInk;
    public static Color GuideOuterNow => Instance != null ? Instance.outerNow : BenchOutline.Guide;

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
    bool deviceGlow, seatRinged;
    int dashed;
    Color innerNow = BenchOutline.GuideInk, outerNow = BenchOutline.Guide;

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

    // The put-down mark (the carried device's place on the mat): its own ring on the screen and its mark in the world.
    BenchRing putDownRing;
    MatMark putDownMark;
    bool putDownShown;
    Vector3 putDownAt;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (putDownMark != null) putDownMark.Dispose();
    }

    void OnEnable() => BenchWork.Flashed += OnFlashed;

    void OnDisable()
    {
        BenchWork.Flashed -= OnFlashed;
        AllOff();
        PutDownOff();
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
        bool live = Time.timeScale > 0f && !RecapUI.Showing;
        bool on = focused != null && live && !focused.IsFinished;
        if (cam == null) cam = Camera.main;
        float now = UiClock.Now;
        Breathe(now);

        // The carried device's place on the mat, while no close-up is open (19 Oct).
        PutDown(live && focused == null, now);

        if (!on && !(focused != null && Time.timeScale > 0f && now - nothingLeftAt < NothingLeftSeconds))
        {
            AllOff();
            return;
        }
        if (canvas == null) Build();

        glowing.Clear();
        hullsNow.Clear();
        ringsUsed = 0;
        dashed = 0;
        tool = null;
        deviceGlow = false;
        seatRinged = false;

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

    // The guide's breath this frame: the amber brightening toward its soft end once a second, the outer line widening a
    // little with it. The ink inside never changes.
    float breath, breathWidth, breathRingAlpha, breathRingGrow;

    void Breathe(float now)
    {
        breath = .5f - .5f * Mathf.Cos(now * Mathf.PI * 2f / Breath);
        innerNow = BenchOutline.GuideInk;
        outerNow = Color.Lerp(BenchOutline.Guide, BenchOutline.GuideSoft, breath);
        breathWidth = Mathf.Lerp(BenchOutline.GuideOuterWidth, BreathWidth, breath);
        breathRingAlpha = Mathf.Lerp(RingAlphaLow, 1f, breath);
        breathRingGrow = Mathf.Lerp(-RingBreathe, RingBreathe, breath);
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
        seatRinged = false;
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

        // The breath (once a second) and the stuck pulse (one, 0.5 s, 2.2x and white to amber).
        Color inner = innerNow, outer = outerNow;
        float outerWidth = breathWidth;
        float ringAlpha = breathRingAlpha, ringGrow = breathRingGrow, stroke = RingStroke;
        float pulse = (now - BenchCards.StuckPulseAt) / PulseSeconds;
        if (pulse >= 0f && pulse < 1f)
        {
            outer = Color.Lerp(Color.white, outer, pulse);
            outerWidth = Mathf.Lerp(PulseWidth, outerWidth, pulse);
            stroke = Mathf.Lerp(RingStroke * PulseWidth, RingStroke, pulse);
            ringAlpha = 1f;
        }

        // The work lies on the face turned down: the whole device breathes (the card says flip).
        if (step.under)
        {
            deviceGlow = true;
            Hull(focused, focused.gameObject, inner, outer, outerWidth, false);
            return;
        }

        // The step's tool, until it is in hand (on a pad, whichever LB / RB has picked is in hand): the tool itself, and its
        // seat on the rest ringed on the screen, so a tray's walls can't hide it (19 Oct).
        if (step.tool != ToolType.Hand && inspector.CurrentTool != step.tool)
        {
            tool = ToolFor(step.tool, focused);
            if (tool != null)
            {
                Hull(tool, tool.gameObject, inner, outer, outerWidth, false);
                seatRinged = SeatRing(tool, WithAlpha(outer, ringAlpha), WithAlpha(inner, ringAlpha), stroke, ringGrow);
            }
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
                if (!Ring(target, WithAlpha(outer, ringAlpha), WithAlpha(inner, ringAlpha), stroke, ringGrow, hidden)) continue;
                glowing.Add(target);
                if (hidden) dashed++;
            }
            else
            {
                Hull(target, target.gameObject, inner, outer, outerWidth, hidden);
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
                    if (Ring(target, WithAlpha(BenchOutline.Left, fade), WithAlpha(BenchOutline.GuideInk, fade), RingStroke, 0f, hidden)) glowing.Add(target);
                    break;
                default:
                    // An opaque outline can't fade: it thins out instead (the ink line thins with it).
                    Hull(target, target.gameObject, BenchOutline.GuideInk, BenchOutline.Left, Mathf.Max(.1f, BenchOutline.GuideOuterWidth * fade), hidden,
                        Mathf.Max(.05f, BenchOutline.GuideInnerWidth * fade));
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
        HullOneTone(focused, focused.gameObject, Color.white, Mathf.Max(.05f, 1.5f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI)), false);
    }

    // ------------------------------------------------------------------ drawing one glow

    // The two-tone outline: the ink line against the part, the colour outside it.
    void Hull(Component target, GameObject go, Color inner, Color outer, float outerWidth, bool hidden, float innerWidth = BenchOutline.GuideInnerWidth)
    {
        if (go == null) return;
        BenchOutline.Glow(go, true, inner, innerWidth, outer, outerWidth, hidden);
        if (!hullsNow.Contains(go)) hullsNow.Add(go);
        glowing.Add(target);
    }

    // One line (the white flash for "nothing left").
    void HullOneTone(Component target, GameObject go, Color colour, float width, bool hidden)
    {
        if (go == null) return;
        BenchOutline.Glow(go, true, colour, width, hidden);
        if (!hullsNow.Contains(go)) hullsNow.Add(go);
        glowing.Add(target);
    }

    // A two-tone ring round the target's projected size + 10 px (at least 14), grown by `grow` px; false when it isn't on screen.
    bool Ring(Component target, Color outer, Color inner, float stroke, float grow, bool dashedRing)
    {
        if (!Project(target, out Vector2 centre, out float radius)) return false;
        BenchRing ring = NextRing();
        ring.Shape(Mathf.Max(RingMin, radius + RingPad) + grow, stroke, dashedRing, RingInner, inner);
        ring.color = dashedRing ? new Color(outer.r, outer.g, outer.b, outer.a * .7f) : outer;
        ring.rectTransform.anchoredPosition = centre;
        return true;
    }

    // The ring round a tool's seat on the rest: its box as it lies at home, projected, + 12 px; so the cloth's tray walls can't
    // hide the sign that the cloth is next (19 Oct). False when the seat isn't on screen.
    bool SeatRing(ToolPickup tool, Color outer, Color inner, float stroke, float grow)
    {
        if (cam == null || canvas == null || tool == null) return false;
        Pose home = tool.HomePose;
        Bounds box = tool.RestBox;
        Vector3 middle = cam.WorldToScreenPoint(home.position + home.rotation * box.center);
        if (middle.z <= cam.nearClipPlane) return false;
        float most = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 p = cam.WorldToScreenPoint(home.position + home.rotation * corner);
            if (p.z <= 0f) continue;
            most = Mathf.Max(most, new Vector2(p.x - middle.x, p.y - middle.y).magnitude);
        }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        if (middle.x < 0f || middle.y < 0f || middle.x > Screen.width || middle.y > Screen.height) return false;
        BenchRing ring = NextRing();
        ring.Shape(Mathf.Max(RingMin, most / scale + SeatPad) + grow, stroke, false, RingInner, inner);
        ring.color = outer;
        ring.rectTransform.anchoredPosition = new Vector2(middle.x / scale, middle.y / scale);
        return true;
    }

    BenchRing NextRing()
    {
        if (ringsUsed >= rings.Count) rings.Add(MakeRing("Ring " + (rings.Count + 1)));
        BenchRing next = rings[ringsUsed++];
        if (!next.gameObject.activeSelf) next.gameObject.SetActive(true);
        return next;
    }

    BenchRing MakeRing(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);
        var ring = go.AddComponent<BenchRing>();
        ring.raycastTarget = false;
        RectTransform r = ring.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(.5f, .5f);
        return ring;
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

    // ------------------------------------------------------------------ where to set it down (19 Oct)

    // The carried device's place on the mat, marked while `wanted` (no close-up open, the game running) and Ace carries a
    // device that still needs the bench: the mark on the mat in the world, and the ring on the screen over it.
    void PutDown(bool wanted, float now)
    {
        PlayerCarry carry = wanted ? PlayerCarry.Current : null;
        JobBase bound = carry != null ? carry.BenchBound : null;
        BenchStage stage = BenchStage.Instance;
        if (bound == null || stage == null || cam == null)
        {
            PutDownOff();
            return;
        }
        if (canvas == null) Build();
        BenchLayout layout = DeviceDefinition.LayoutOf(bound);
        Vector3 centre = stage.MatToWorld(layout.rest, BenchStage.MatTop + PutDownLift);
        Quaternion turn = stage.MatRotation;
        // The footprint it will take, lying flat: its two longer sides as it was picked up (real size), grown by the margin.
        Vector3 size = carry.CarriedSize(bound);
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = Mathf.Max(.02f, size.x + size.y + size.z - longest - thinnest);
        float halfX = Mathf.Max(middle, .6f * longest) * .5f + PutDownMargin, halfZ = Mathf.Max(longest, .06f) * .5f + PutDownMargin;
        if (putDownMark == null) putDownMark = new MatMark();
        float amber = PutDownAmber + PutDownBreathe * breath;
        putDownMark.Show(centre, turn, halfX, halfZ, PutDownInk, amber, WithAlpha(innerNow, .9f), WithAlpha(outerNow, Mathf.Lerp(.8f, 1f, breath)));
        putDownShown = true;
        putDownAt = centre;

        // The ring on the screen: the mark's own size there, and never under 26 px (from the overhead the mat is tiny).
        Vector3 s = cam.WorldToScreenPoint(centre);
        bool onScreen = s.z > cam.nearClipPlane && s.x >= 0f && s.y >= 0f && s.x <= Screen.width && s.y <= Screen.height;
        if (putDownRing == null) putDownRing = MakeRing("Put-down ring");
        if (!onScreen)
        {
            if (putDownRing.gameObject.activeSelf) putDownRing.gameObject.SetActive(false);
            return;
        }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float most = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = centre + turn * new Vector3((i & 1) == 0 ? -halfX : halfX, 0f, (i & 2) == 0 ? -halfZ : halfZ);
            Vector3 p = cam.WorldToScreenPoint(corner);
            if (p.z > 0f) most = Mathf.Max(most, new Vector2(p.x - s.x, p.y - s.y).magnitude / scale);
        }
        if (!putDownRing.gameObject.activeSelf) putDownRing.gameObject.SetActive(true);
        putDownRing.Shape(Mathf.Max(PutDownRingMin, most + RingPad) + breathRingGrow, RingStroke, false, RingInner, WithAlpha(innerNow, breathRingAlpha));
        putDownRing.color = WithAlpha(outerNow, breathRingAlpha);
        putDownRing.rectTransform.anchoredPosition = new Vector2(s.x / scale, s.y / scale);
    }

    void PutDownOff()
    {
        if (!putDownShown) return;
        putDownShown = false;
        if (putDownMark != null) putDownMark.Hide();
        if (putDownRing != null && putDownRing.gameObject.activeSelf) putDownRing.gameObject.SetActive(false);
    }

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("Bench glow (while playing)", transform, SortingOrder);
        root = (RectTransform)canvas.transform;
    }

    // ------------------------------------------------------------------ the mark on the mat (the world)

    // A rounded band lying on the mat round the device's place: an ink band just inside the footprint's edge and an amber band
    // outside it, two sub-meshes in two unlit see-through materials (URP's Unlit, as the dirt patches use URP's Lit), a hair
    // above the mat's top. Its shape is rebuilt into kept arrays each frame it shows (the amber band breathes), so it
    // allocates nothing after the first frame.
    sealed class MatMark
    {
        const int PerCorner = 6, Points = 4 * (PerCorner + 1);
        readonly GameObject go;
        readonly Mesh mesh;
        readonly Material ink, amber;
        readonly Vector3[] verts = new Vector3[Points * 3];
        readonly int[] inkTris = new int[Points * 6], amberTris = new int[Points * 6];

        public MatMark()
        {
            go = new GameObject("Put-down mark (bench glow)") { hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Put-down mark", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            ink = Unlit("Put-down mark (ink, runtime)");
            amber = Unlit("Put-down mark (amber, runtime)");
            renderer.sharedMaterials = new[] { ink, amber };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // The bands' quads, one way round; the materials cull nothing, so the mark reads from above and from below alike.
            for (int i = 0; i < Points; i++)
            {
                int j = (i + 1) % Points;
                // Vertices per point: 0 the inner edge of the ink band, 1 the footprint's edge, 2 the amber band's outer edge.
                Quad(inkTris, i * 6, i * 3 + 0, i * 3 + 1, j * 3 + 1, j * 3 + 0);
                Quad(amberTris, i * 6, i * 3 + 1, i * 3 + 2, j * 3 + 2, j * 3 + 1);
            }
            go.SetActive(false);
        }

        static void Quad(int[] tris, int at, int a, int b, int c, int d)
        {
            tris[at] = a; tris[at + 1] = b; tris[at + 2] = c;
            tris[at + 3] = a; tris[at + 4] = c; tris[at + 5] = d;
        }

        // URP's Unlit, see-through, no depth write, double-sided (GrimeSpot's recipe for URP's Lit).
        static Material Unlit(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;   // over the mat and the dirt patches' queue
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            return m;
        }

        public void Show(Vector3 centre, Quaternion turn, float halfX, float halfZ, float inkWidth, float amberWidth, Color inkColour, Color amberColour)
        {
            float r = Mathf.Min(halfX, halfZ) * .3f;
            // Each point of the path at three offsets from the footprint's edge: -inkWidth, 0, +amberWidth. A rounded
            // rectangle offset by d is the rounded rectangle with its half sizes and its corner radius grown by d.
            int v = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = (corner == 0 || corner == 3 ? 1f : -1f) * (halfX - r), cz = (corner < 2 ? 1f : -1f) * (halfZ - r);
                for (int k = 0; k <= PerCorner; k++)
                {
                    float a = (corner * 90f + 90f * k / PerCorner) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts[v++] = new Vector3(cx, 0f, cz) + dir * Mathf.Max(0f, r - inkWidth);
                    verts[v++] = new Vector3(cx, 0f, cz) + dir * r;
                    verts[v++] = new Vector3(cx, 0f, cz) + dir * (r + amberWidth);
                }
            }
            mesh.Clear(false);
            mesh.subMeshCount = 2;
            mesh.SetVertices(verts, 0, Points * 3);
            mesh.SetTriangles(inkTris, 0, false);
            mesh.SetTriangles(amberTris, 1, false);
            mesh.RecalculateBounds();
            go.transform.SetPositionAndRotation(centre, turn);
            ink.SetColor("_BaseColor", inkColour);
            amber.SetColor("_BaseColor", amberColour);
            if (ink.HasProperty("_Color")) { ink.SetColor("_Color", inkColour); amber.SetColor("_Color", amberColour); }
            if (!go.activeSelf) go.SetActive(true);
        }

        public void Hide()
        {
            if (go != null && go.activeSelf) go.SetActive(false);
        }

        public void Dispose()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (ink != null) Object.Destroy(ink);
            if (amber != null) Object.Destroy(amber);
        }
    }
}

/// <summary>
/// A ring drawn on the screen (the glow round a screw, a hole or a dirt spot; a tool's seat; "what's left"; the put-down
/// mark): a stroke of a fixed width in canvas units at any radius, solid or dashed 4 px on and 4 px off, and since 19 Oct
/// with an optional second band just inside it in another colour (the guide's ink inside its amber). Its mesh is built
/// again only when its shape or its colours change.
/// </summary>
public sealed class BenchRing : MaskableGraphic
{
    float radius = 20f, stroke = 3f, innerStroke;
    bool dashed;
    Color innerColour = Color.clear;

    /// <summary>The ring's radius (to the middle of its stroke) and stroke, in canvas units, and whether it is dashed.</summary>
    public void Shape(float r, float s, bool d) => Shape(r, s, d, 0f, Color.clear);

    /// <summary>The same, with a band <paramref name="innerS"/> wide in <paramref name="innerC"/> just inside the stroke (its
    /// alpha follows the ring's colour alpha, so a breath or a fade takes both).</summary>
    public void Shape(float r, float s, bool d, float innerS, Color innerC)
    {
        r = Mathf.Max(1f, r);
        s = Mathf.Max(.5f, s);
        innerS = Mathf.Max(0f, innerS);
        if (Mathf.Abs(r - radius) < .1f && Mathf.Abs(s - stroke) < .05f && d == dashed && Mathf.Abs(innerS - innerStroke) < .05f && innerC == innerColour) return;
        radius = r;
        stroke = s;
        dashed = d;
        innerStroke = innerS;
        innerColour = innerC;
        float size = 2f * (r + s);
        rectTransform.sizeDelta = new Vector2(size, size);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float inner = Mathf.Max(0f, radius - stroke * .5f), outer = radius + stroke * .5f;
        Band(vh, inner, outer, color);
        if (innerStroke > 0f)
        {
            Color c = innerColour;
            c.a *= color.a;
            Band(vh, Mathf.Max(0f, inner - innerStroke), inner, c);
        }
    }

    void Band(VertexHelper vh, float inner, float outer, Color32 c)
    {
        float mid = (inner + outer) * .5f;
        float circumference = 2f * Mathf.PI * Mathf.Max(1f, mid);
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
            float a = i * step, m = a + step * .25f, b = a + step * .5f;
            Arc(vh, a, m, inner, outer, c);
            Arc(vh, m, b, inner, outer, c);
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
