using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// PINS OVER THE CAFÉ (playtest 4, 10 Oct 2026; the first-hour playtester's #3: "From the default camera, nothing points
// at who or where")
//
// What the overhead camera couldn't say, pinned over the café on a screen of its own, each the same size at any zoom and
// in first person, the way the badges and the barks are pinned (Barks.HeadPointOf for a person, a point for a thing):
//
//   The patience bars   every customer's, while they wait (PatienceBar still says when: shown and hidden exactly as
//                        before). 56 x 7 px at 1080p, coloured the way the ticket's meter is (green, amber, red), with its
//                        top just under the head point: a badge pops up from the head point and a bark sits over it, so
//                        neither ever covers the bar. Once their job is taken, a dot in the job's colour at its left end:
//                        the tape on their ticket and the tag on their device are that colour too (the HUD spec §4).
//                        The bar it replaces was a 0.6 x 0.08 m strip in the world: 23-26 x 3 px from the default camera.
//   The name             of whoever or whatever E acts on, over them, in the overhead view (FocusNameUI). It used to be a
//                        label fixed under the middle of the screen: "Grace" over the empty floor while she stood at the
//                        counter. It sits above a badge, a bark or a speech bubble over the same head, never on them.
//   Day 1's pins         a green pin over the thing the guide's card names (Grace, the intake tray, the repair bench, the
//                        drinks machine), and "You" over Ace for the first seconds of his first day (DayOneGuideUI). When
//                        the name is pinned over that same thing, the name alone shows: one pin, not two stacked.
//
// Sorting order 30: over the HUD (0) and its corners (2), under the night's notebook (40), the recap phone (60), the
// juice (75) and the barks (80). Everything is hidden while the recap has the screen, at night, while a conversation is
// open, and while Ace is in a close-up (the bench, the drinks machine, the counter phone): the close-up has its own words.
// Placed just before the canvases are drawn, after every camera has moved (as the barks and the juice are). Built in code
// while playing: nothing in the scene changes.
//
// Run 2 (11 Oct): a pin shows its new words only where it now belongs. When Day 1's step changed, the green pin faded out
// where it was already showing the next step's name (a half-faded "Officer" over the bench; "Walter" over the bench while
// the same name was pinned over Walter at the counter): a pin fading out now keeps its old words.
// ---------------------------------------------------------------------------
public sealed class UiPins : MonoBehaviour
{
    public const int SortingOrder = 30;

    // ---- sizes (reference pixels, 1920 x 1080) ----
    public const float BarWidth = 56f, BarHeight = 7f;
    const float BarGap = 3f;          // from the head point down to the bar's top
    const float TagDot = 10f;         // the job colour's dot, its paper rim included
    const float PinGap = 4f;          // from what's under a pin to the pin's point
    const float PopHeight = 62f;      // a badge over a head (Juice's 58 px icon, with room)
    const float NameSize = 18f, PinTextSize = 17f, PointerHeight = 8f;
    const float EdgeMargin = 18f, TopReserve = 150f;   // clear of the tabs and today's takings at the top
    const float FadeRate = 8f;

    public static UiPins Instance { get; private set; }

    static readonly List<PatienceBar> bars = new List<PatienceBar>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        bars.Clear();
        pointer = null;
    }

    /// <summary>The pins of this Play session (made on first use; nothing outside Play).</summary>
    public static UiPins Ensure()
    {
        if (Instance != null) return Instance;
        if (!Application.isPlaying) return null;
        var go = new GameObject("Pins over the café (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        return go.AddComponent<UiPins>();
    }

    /// <summary>A bar that's up (PatienceBar, while its customer waits).</summary>
    public static void Register(PatienceBar bar)
    {
        if (bar == null || bars.Contains(bar)) return;
        bars.Add(bar);
        Ensure();
    }

    public static void Unregister(PatienceBar bar) => bars.Remove(bar);

    // ------------------------------------------------------------------ the parts

    sealed class Pin
    {
        public RectTransform root, chip, pointerRect;
        public Image face, pointerImage, shadow;
        public TextMeshProUGUI label;
        public CanvasGroup group;
        public bool wanted, bob, placed;
        public Transform who;          // over this person's head, or
        public Vector3 point;          // over this point in the world
        public string text = "";
        public bool relabel;           // the label still shows the words before text (they change where it's drawn next)
        public Rect onScreen;          // where it was last drawn (screen pixels from the bottom left)
    }

    sealed class BarView
    {
        public RectTransform root, fill, rim, dot;
        public Image track, fillImage, rimImage, dotImage;
        public PatienceBar bar;
        public Rect onScreen;
    }

    Canvas canvas;
    RectTransform canvasRect;
    CanvasGroup all;
    Camera cam;
    Pin focus, guide, self;
    readonly List<BarView> views = new List<BarView>();
    readonly Dictionary<Transform, Renderer> bubbles = new Dictionary<Transform, Renderer>();
    PlayerInteractor player;
    bool hidden;
    static readonly Vector3[] corners = new Vector3[4];

    // ------------------------------------------------------------------ what the game calls

    /// <summary>The name of what E acts on, over <paramref name="who"/>'s head (a person) or over <paramref name="point"/>.</summary>
    public void ShowFocus(string text, Transform who, Vector3 point) => Want(focus, text, who, point);
    public void HideFocus() { if (focus != null) focus.wanted = false; }

    /// <summary>Day 1's pin over the thing the guide's card names.</summary>
    public void ShowGuide(string text, Transform who, Vector3 point) => Want(guide, text, who, point);
    public void HideGuide() { if (guide != null) guide.wanted = false; }

    /// <summary>"You" over Ace, for the first seconds of his first day.</summary>
    public void ShowSelf(string text, Transform ace) => Want(self, text, ace, Vector3.zero);
    public void HideSelf() { if (self != null) self.wanted = false; }

    // ------------------------------------------------------------------ what checks read

    public bool FocusShowing => Showing(focus);
    public bool GuideShowing => Showing(guide);
    public bool SelfShowing => Showing(self);
    public string FocusText => focus != null ? focus.text : "";
    public string GuideText => guide != null ? guide.text : "";
    public string SelfText => self != null ? self.text : "";
    /// <summary>Where each was last drawn, in screen pixels from the bottom left.</summary>
    public Rect FocusOnScreen => focus != null ? focus.onScreen : default;
    public Rect GuideOnScreen => guide != null ? guide.onScreen : default;
    public Rect SelfOnScreen => self != null ? self.onScreen : default;
    /// <summary>Who or where the guide's pin points at now.</summary>
    public Transform GuideWho => guide != null && guide.wanted ? guide.who : null;
    public Vector3 GuidePoint => guide != null ? guide.point : Vector3.zero;
    /// <summary>How many patience bars are drawn now, and how many are up.</summary>
    public int BarsShowing { get; private set; }
    public static int BarsUp => bars.Count;
    /// <summary>Everything is hidden now (the recap, the night, a conversation, a close-up).</summary>
    public bool AllHidden => hidden;

    /// <summary>The bar over <paramref name="who"/> as last drawn (screen pixels from the bottom left), and its fill's colour.</summary>
    public bool TryGetBar(Transform who, out Rect onScreen, out Color colour, out bool dot)
    {
        onScreen = default;
        colour = default;
        dot = false;
        foreach (BarView v in views)
        {
            if (v.bar == null || v.bar.Owner != who || !v.root.gameObject.activeSelf) continue;
            onScreen = v.onScreen;
            colour = v.fillImage.color;
            dot = v.rim.gameObject.activeSelf;   // the rim holds the dot: the dot's own activeSelf never changes
            return true;
        }
        return false;
    }

    /// <summary>The bar's colour at this much patience: the ticket's meter's (JobTicket), brightened for the dark track.</summary>
    public static Color ColourAt(float patience)
    {
        patience = Mathf.Clamp01(patience);
        Color low = new Color(.93f, .36f, .29f), mid = UiSkin.Gold, high = UiSkin.BrandBright;
        return patience < .5f ? Color.Lerp(low, mid, patience * 2f) : Color.Lerp(mid, high, (patience - .5f) * 2f);
    }

    // ------------------------------------------------------------------ making it

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        canvas = UiSkin.ScreenCanvas("Pins over the café", transform, SortingOrder);
        canvasRect = (RectTransform)canvas.transform;
        all = canvas.gameObject.AddComponent<CanvasGroup>();
        all.blocksRaycasts = false;
        all.interactable = false;
        // The bars first, so the names and pins draw over them.
        focus = MakePin("Name (what E acts on)", UiSkin.Band, UiSkin.BandText, NameSize, false);
        guide = MakePin("Day 1's pin", UiSkin.Brand, UiSkin.Paper, PinTextSize, true);
        self = MakePin("You", UiSkin.Gold, UiSkin.Ink, PinTextSize, true);
        guide.bob = self.bob = true;
    }

    void OnEnable() => Canvas.willRenderCanvases += Place;

    void OnDisable() => Canvas.willRenderCanvases -= Place;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        bubbles.Clear();
    }

    static RectTransform Box(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, 0f);
        return rect;
    }

    Pin MakePin(string name, Color faceColour, Color ink, float size, bool withPointer)
    {
        var pin = new Pin();
        pin.root = Box(name, canvasRect);
        pin.group = pin.root.gameObject.AddComponent<CanvasGroup>();
        pin.group.blocksRaycasts = false;
        pin.group.interactable = false;
        pin.group.alpha = 0f;
        pin.chip = Box("Chip", pin.root);
        pin.chip.anchorMin = pin.chip.anchorMax = new Vector2(.5f, 0f);   // hangs from the pin's bottom middle
        pin.chip.anchoredPosition = new Vector2(0f, withPointer ? PointerHeight - 1f : 0f);
        pin.shadow = UiSkin.DropShadow(pin.chip, 6f, 2f, .3f);
        pin.face = UiSkin.Paint(pin.chip, faceColour, 9f);
        pin.label = UiSkin.Text("Words", pin.chip, size, ink, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform words = pin.label.rectTransform;
        words.anchorMin = Vector2.zero;
        words.anchorMax = Vector2.one;
        words.offsetMin = words.offsetMax = Vector2.zero;
        if (withPointer)
        {
            pin.pointerRect = Box("Pointer", pin.root);
            pin.pointerRect.anchorMin = pin.pointerRect.anchorMax = new Vector2(.5f, 0f);   // its tip on the pinned point
            pin.pointerRect.anchoredPosition = Vector2.zero;
            pin.pointerRect.sizeDelta = new Vector2(14f, PointerHeight);
            pin.pointerImage = pin.pointerRect.gameObject.AddComponent<Image>();
            pin.pointerImage.sprite = Pointer;
            pin.pointerImage.color = faceColour;
            pin.pointerImage.raycastTarget = false;
        }
        pin.root.gameObject.SetActive(false);
        return pin;
    }

    BarView MakeBar()
    {
        var v = new BarView();
        v.root = Box("Patience", canvasRect);
        v.root.pivot = new Vector2(.5f, 1f);
        v.root.sizeDelta = new Vector2(BarWidth, BarHeight);
        v.track = UiSkin.Paint(v.root, new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .78f), BarHeight * .5f);
        v.fill = Box("So much left", v.root);
        v.fill.anchorMin = new Vector2(0f, 0f);
        v.fill.anchorMax = new Vector2(0f, 1f);
        v.fill.pivot = new Vector2(0f, .5f);
        v.fill.offsetMin = new Vector2(1f, 1f);
        v.fill.offsetMax = new Vector2(BarWidth - 1f, -1f);
        v.fillImage = UiSkin.Paint(v.fill, UiSkin.BrandBright, (BarHeight - 2f) * .5f);
        // The job's colour: a dot on a paper rim, just left of the bar.
        v.rim = Box("Their job's colour", v.root);
        v.rim.anchorMin = v.rim.anchorMax = new Vector2(0f, .5f);
        v.rim.pivot = new Vector2(1f, .5f);
        v.rim.anchoredPosition = new Vector2(-3f, 0f);
        v.rim.sizeDelta = new Vector2(TagDot, TagDot);
        v.rimImage = v.rim.gameObject.AddComponent<Image>();
        v.rimImage.sprite = UiSkin.Circle;
        v.rimImage.color = UiSkin.Paper;
        v.rimImage.raycastTarget = false;
        v.dot = Box("Dot", v.rim);
        v.dot.anchorMin = v.dot.anchorMax = new Vector2(.5f, .5f);
        v.dot.pivot = new Vector2(.5f, .5f);
        v.dot.anchoredPosition = Vector2.zero;
        v.dot.sizeDelta = new Vector2(TagDot - 3f, TagDot - 3f);
        v.dotImage = v.dot.gameObject.AddComponent<Image>();
        v.dotImage.sprite = UiSkin.Circle;
        v.dotImage.raycastTarget = false;
        v.root.SetSiblingIndex(0);   // under the pins
        v.root.gameObject.SetActive(false);
        views.Add(v);
        return v;
    }

    void Want(Pin pin, string text, Transform who, Vector3 point)
    {
        if (pin == null) return;
        pin.wanted = !string.IsNullOrEmpty(text);
        pin.who = who;
        pin.point = point;
        if (text == pin.text) return;
        pin.text = text ?? "";
        pin.relabel = true;
        // Not on screen: the new words now. On screen: where it's drawn next (PlacePin), so a pin fading out where it was
        // keeps its old words.
        if (!pin.root.gameObject.activeSelf || pin.group.alpha <= .01f) Relabel(pin);
    }

    // The label says the pin's words, and the chip fits them.
    static void Relabel(Pin pin)
    {
        pin.relabel = false;
        pin.label.text = pin.text;
        Vector2 words = pin.label.GetPreferredValues(pin.text, 999f, 999f);
        pin.chip.sizeDelta = new Vector2(Mathf.Ceil(words.x) + 18f, pin.label.fontSize + 10f);
        float height = pin.chip.sizeDelta.y + (pin.pointerRect != null ? PointerHeight - 1f : 0f);
        pin.root.sizeDelta = new Vector2(pin.chip.sizeDelta.x, height);
        UiSkin.Follow(pin.shadow.rectTransform, pin.chip, 6f, 2f);
    }

    static bool Showing(Pin pin) => pin != null && pin.wanted && pin.root.gameObject.activeSelf && pin.group.alpha > .5f && pin.placed;

    // ------------------------------------------------------------------ placing them

    // Whether everything is put away: the recap, the night, a conversation, a close-up.
    bool Hidden()
    {
        if (RecapUI.Showing) return true;
        if (NightWalk.Instance != null && NightWalk.Instance.Active) return true;
        if (ConversationController.AnyOpen) return true;
        if (player == null)
        {
            PlayerCarry carry = PlayerCarry.Instance;
            if (carry != null) player = carry.GetComponent<PlayerInteractor>();
        }
        return player != null && player.InCloseUp;
    }

    void Place()
    {
        if (canvas == null) return;
        hidden = Hidden();
        all.alpha = hidden ? 0f : 1f;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float dt = UiClock.Delta;
        PlaceBars(scale);
        PlacePin(focus, scale, dt, false);
        PlacePin(guide, scale, dt, SameAsFocus());
        PlacePin(self, scale, dt, false);
    }

    // Day 1's pin over the very thing the name is pinned over would say it twice, one pin on the other: the name is enough.
    bool SameAsFocus()
    {
        if (focus == null || guide == null || !focus.wanted || !guide.wanted || !focus.placed) return false;
        if (guide.who != null) return guide.who == focus.who;
        return focus.who == null && (focus.point - guide.point).sqrMagnitude < .36f;
    }

    void PlaceBars(float scale)
    {
        int shown = 0;
        for (int i = bars.Count - 1; i >= 0; i--)
            if (bars[i] == null) bars.RemoveAt(i);
        while (views.Count < bars.Count) MakeBar();
        for (int i = 0; i < views.Count; i++)
        {
            BarView v = views[i];
            PatienceBar bar = i < bars.Count ? bars[i] : null;
            v.bar = bar;
            bool on = bar != null && !hidden && cam != null && bar.Owner != null && bar.isActiveAndEnabled;
            Vector3 s = default;
            if (on)
            {
                s = cam.WorldToScreenPoint(Barks.HeadPointOf(bar.Owner));
                on = s.z > 0f;
            }
            if (v.root.gameObject.activeSelf != on) v.root.gameObject.SetActive(on);
            if (!on) continue;
            shown++;
            // Its top just under the head point: a badge or a bark over the same head sits above it.
            v.root.anchoredPosition = new Vector2(s.x / scale, s.y / scale - BarGap);
            float f = Mathf.Clamp01(bar.Fraction);
            v.fill.offsetMax = new Vector2(Mathf.Max(BarHeight - 2f, (BarWidth - 2f) * f) + 1f, -1f);
            Color c = ColourAt(f);
            // Losing patience (the ticket says so under a quarter): the fill breathes, to catch the eye.
            if (f <= .25f) c.a = .7f + .3f * (.5f + .5f * Mathf.Sin(UiClock.Now * Mathf.PI * 2f / 1.1f));
            v.fillImage.color = c;
            CustomerBrain customer = bar.Customer;
            bool dot = customer != null && customer.WasAccepted && customer.HasJob;
            if (v.rim.gameObject.activeSelf != dot) v.rim.gameObject.SetActive(dot);
            if (dot)
            {
                Color job = customer.JobColor;
                v.dotImage.color = new Color(job.r, job.g, job.b, 1f);
            }
            v.onScreen = new Rect(s.x - BarWidth * .5f * scale, s.y - (BarGap + BarHeight) * scale, BarWidth * scale, BarHeight * scale);
        }
        BarsShowing = shown;
    }

    void PlacePin(Pin pin, float scale, float dt, bool covered)
    {
        bool want = pin.wanted && !covered && !hidden && cam != null && (pin.who != null || pin.point != Vector3.zero);
        Vector3 s = default;
        if (want)
        {
            Vector3 world = pin.who != null ? Barks.HeadPointOf(pin.who) : pin.point;
            s = cam.WorldToScreenPoint(world);
            want = s.z > 0f;
        }
        // New words go up where the pin is placed now, or once it has faded out: never on a pin fading out where it was.
        if (pin.relabel && (want || pin.group.alpha <= .01f)) Relabel(pin);
        pin.group.alpha = Mathf.MoveTowards(pin.group.alpha, want ? 1f : 0f, (dt > 0f ? dt : Time.unscaledDeltaTime) * FadeRate);
        bool on = want || pin.group.alpha > .01f;
        if (pin.root.gameObject.activeSelf != on) pin.root.gameObject.SetActive(on);
        pin.placed = want;
        if (!want) return;
        float bottom = pin.who != null ? ClearAbove(pin.who, s.y, scale) : s.y + PinGap * scale;
        Vector2 at = new Vector2(s.x / scale, bottom / scale);
        if (pin.bob) at.y += 3f * (.5f + .5f * Mathf.Sin(UiClock.Now * Mathf.PI * 2f / 1.6f));
        // Kept on the screen (a pinned thing orbited out of view still has its pin at the edge), clear of the top corners.
        Vector2 size = pin.root.sizeDelta;
        Rect area = canvasRect.rect;
        at.x = Mathf.Clamp(at.x, EdgeMargin + size.x * .5f, Mathf.Max(EdgeMargin + size.x * .5f, area.width - EdgeMargin - size.x * .5f));
        at.y = Mathf.Clamp(at.y, EdgeMargin, Mathf.Max(EdgeMargin, area.height - TopReserve - size.y));
        pin.root.anchoredPosition = at;
        pin.onScreen = new Rect((at.x - size.x * .5f) * scale, at.y * scale, size.x * scale, size.y * scale);
    }

    // The lowest a pin over this head can sit, in screen pixels: above the head point, and above a badge, a bark or a
    // speech bubble that is over the same head now.
    float ClearAbove(Transform who, float headY, float scale)
    {
        float bottom = headY + PinGap * scale;
        if (Juice.ShowingOver(who, out Vector2 pop, out _)) bottom = Mathf.Max(bottom, pop.y + (PopHeight + PinGap) * scale);
        if (Barks.TryGetShown(who, out Barks.Shown line) && line.alpha > .05f) bottom = Mathf.Max(bottom, line.band.yMax + PinGap * scale);
        Renderer bubble = BubbleOf(who);
        if (bubble != null && bubble.enabled && bubble.gameObject.activeInHierarchy)
        {
            Bounds b = bubble.bounds;
            Vector3 top = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            if (top.z > 0f) bottom = Mathf.Max(bottom, top.y + PinGap * scale);
        }
        return bottom;
    }

    // A customer's own speech bubble (the world's text over them, "SpeechBubble"), found once.
    Renderer BubbleOf(Transform who)
    {
        if (who == null) return null;
        if (bubbles.TryGetValue(who, out Renderer found)) return found;
        Transform child = who.Find("SpeechBubble");
        found = child != null ? child.GetComponent<Renderer>() : null;
        bubbles[who] = found;
        return found;
    }

    // ------------------------------------------------------------------ the pointer, drawn in code once

    static Sprite pointer;

    // A triangle pointing down, white (the Image tints it), its edges smoothed.
    static Sprite Pointer
    {
        get
        {
            if (pointer != null) return pointer;
            const int w = 32, h = 18;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Pins - pointer", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + .5f, cy = y + .5f;
                    float half = w * .5f * (cy / h);              // the triangle's half width at this height (0 at the tip)
                    float d = Mathf.Abs(cx - w * .5f) - half;       // under 0: inside
                    float a = Mathf.Clamp01(.5f - d) * Mathf.Clamp01(h - cy + .5f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            pointer = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
            pointer.name = texture.name;
            pointer.hideFlags = HideFlags.DontSave;
            return pointer;
        }
    }
}
