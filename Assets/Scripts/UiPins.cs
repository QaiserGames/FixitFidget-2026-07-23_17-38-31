using System;
using System.Collections.Generic;
using System.Reflection;
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
//                        before). BarWidth x BarHeight at 1080p (one number sets the size: BarScale, below), coloured the
//                        way the ticket's meter is (green, amber, red), with its top just under the head point: a badge
//                        pops up from the head point and a bark sits over it, so neither ever covers the bar. Once their
//                        job is taken, a dot in the job's colour at its left end: the tape on their ticket and the tag on
//                        their device are that colour too (the HUD spec §4). The bar it replaces was a 0.6 x 0.08 m strip
//                        in the world: 23-26 x 3 px from the default camera.
//   The name             of whoever or whatever E acts on, over them, in the overhead view (FocusNameUI). It used to be a
//                        label fixed under the middle of the screen: "Grace" over the empty floor while she stood at the
//                        counter. It sits above a badge, a bark or a speech bubble over the same head, never on them.
//   Day 1's pins         a green pin over the thing the guide's card names (Grace, the intake tray, the repair bench, the
//                        drinks machine), and "You" over Ace for the first seconds of his first day (DayOneGuideUI). When
//                        the name is pinned over that same thing, the name alone shows: one pin, not two stacked.
//
// Sorting order 30: over the HUD (0) and its corners (2), under the night's notebook (40), the recap phone (60), the
// juice (75) and the barks (80). Everything is hidden while the recap has the screen, at night, while a conversation is
// open, while Ace is in a close-up (the bench, the drinks machine, the counter phone): the close-up has its own words; and
// since 12 Oct while the morning's intro plays (DayIntro.Playing, read by name: below).
// Placed just before the canvases are drawn, after every camera has moved (as the barks and the juice are). Built in code
// while playing: nothing in the scene changes.
//
// Run 2 (11 Oct): a pin shows its new words only where it now belongs. When Day 1's step changed, the green pin faded out
// where it was already showing the next step's name (a half-faded "Officer" over the bench; "Walter" over the bench while
// the same name was pinned over Walter at the counter): a pin fading out now keeps its old words.
//
// 12-13 Oct (his playtest 4, the findings doc §4; his answer: "make the patience bar bigger i guess in a sense for it to
// feel right, not too big obviously but enough for it to feel good and look good"): the bar's sizes are one family set by
// one number, BarScale. 1.3 was the findings' pick (72 x 9); photographed over real heads from the game's overhead camera
// on 13 Oct (Logs/UI/day1-guide-2026-10-13_070451), 72 x 9 read as a thin dash and 84 x 10 as a bar, so 1.5 (84 x 10, the
// dot 14, the name 22). His to overrule: this one number.
//
// Playtest 7 (19 Oct 2026): the night allows one pin, "You" over Ace for the night's first seconds (ShowSelfAtNight, asked
// for by NightCycle as the night begins): the reviewers could not find Ace, a 25 px figure in an unlit doorway, on the
// night's first frame. The bars, the name and the guide's pin stay off all night, as before.
//
// His own playtest (22 Oct 2026): in first person at night that "You" bobbed at the screen's edge ("it's looking for it in
// the isometric view"): nothing asked which view was on the screen, so a pin meant for finding Ace from above was clamped
// to the edge with Ace's head behind the camera. "You" now shows only while the overhead view is on the screen
// (CafeViewMode.OverheadShown), by night as by day (Day 1's has only ever been asked for then: DayOneGuideUI). The night's
// seconds still run meanwhile: back to the overhead view inside them, it's there again.
// ---------------------------------------------------------------------------
public sealed class UiPins : MonoBehaviour
{
    public const int SortingOrder = 30;

    // ---- sizes (reference pixels, 1920 x 1080) ----
    //
    // THE BAR'S FAMILY: one number. What each scale gives at 1080p:
    //   1.0   bar 56 x 7    dot 10   name 18   Day 1's pins 17   (10 Oct)
    //   1.3   bar 72 x 9    dot 12   name 20   Day 1's pins 19   (the findings' pick)
    //   1.5   bar 84 x 10   dot 14   name 22   Day 1's pins 21
    // The bar is its 10 Oct size times the scale, rounded down to whole pixels (72.8 is 72, so at 1.3 it keeps its 8:1
    // shape). The job's dot and the words over heads grow by as many pixels as the bar grew in height, rounded up to an
    // even number: they keep with the bar, but slower, so the bar is what got bigger and every name over every head doesn't
    // get loud (a name at 1.3 times 18 would be 23 px, bigger than the Day 1 card's own words, 21, and the controls strip's,
    // 19; at 20 it sits with the HUD's reading sizes, beside the payment pop's 20). Day 1's green pin and "You" grow
    // with the name, so when the green pin hands a person over to their name (the same place, the same moment) the words
    // don't jump in size. The gaps the bar is laid out with (under the head point, between the dot and the bar) scale like
    // the bar; the strokes stay hairlines (the dark track's 1 px edge round the fill, the dot's 1.5 px paper rim).
    public const float BarScale = 1.5f;
    const int BaseBarWidth = 56, BaseBarHeight = 7, BaseTagDot = 10, BaseNameSize = 18, BasePinTextSize = 17, BaseGap = 3;
    // How much the dot and the words grow: the bar's growth in height, rounded up to an even number (2 at 1.3, 4 at 1.5).
    const int Grown = ((int)(BaseBarHeight * BarScale) - BaseBarHeight + 1) / 2 * 2;

    /// <summary>The patience bar's width and height over a head (reference pixels at 1080p).</summary>
    public const float BarWidth = (int)(BaseBarWidth * BarScale), BarHeight = (int)(BaseBarHeight * BarScale);
    /// <summary>The job colour's dot beside the bar, its paper rim included.</summary>
    public const float TagDot = BaseTagDot + Grown;
    /// <summary>The name over whoever or whatever E acts on.</summary>
    public const float NameSize = BaseNameSize + Grown;
    /// <summary>Day 1's green pin and "You".</summary>
    public const float PinTextSize = BasePinTextSize + Grown;
    const float BarGap = (int)(BaseGap * BarScale);   // from the head point down to the bar's top
    const float DotGap = (int)(BaseGap * BarScale);   // from the dot's right edge to the bar's left end
    const float TrackEdge = 1f;      // the dark track showing round the fill
    const float DotRim = 1.5f;       // the paper round the job's colour
    const float PinGap = 4f;          // from what's under a pin to the pin's point
    const float PopHeight = 62f;      // a badge over a head (Juice's 58 px icon, with room)
    const float PointerHeight = 8f;
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
        introPlaying = null;
        introLookedUp = false;
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
        // What the pin names, wherever it is drawn: the same as who and point, but for the name when it hangs below its
        // person or thing, clear of Ace's head (FocusNameUI.ClearOfAce), which draws it over a point under them.
        public Transform aboutWho;
        public Vector3 aboutPoint;
        public string text = "";
        public bool relabel;           // the label still shows the words before text (they change where it's drawn next)
        public Rect onScreen;          // where it was last drawn (screen pixels from the bottom left)
    }

    sealed class BarView
    {
        public RectTransform root, fill, rim, dot;
        public Image track, fillImage, rimImage, dotImage;
        public PatienceBar bar;
        public Rect onScreen, dotOnScreen;
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

    /// <summary>The name drawn over <paramref name="who"/> or <paramref name="point"/> but naming <paramref name="aboutWho"/>
    /// (or <paramref name="aboutPoint"/>): the name hanging below its person, clear of Ace's head. Day 1's green pin over
    /// that same person still stands aside for it (20 Oct: "Valerie" twice, the green pin over her head and the name below).</summary>
    public void ShowFocus(string text, Transform who, Vector3 point, Transform aboutWho, Vector3 aboutPoint)
    {
        Want(focus, text, who, point);
        if (focus == null) return;
        focus.aboutWho = aboutWho;
        focus.aboutPoint = aboutPoint;
    }
    public void HideFocus() { if (focus != null) focus.wanted = false; }

    /// <summary>Day 1's pin over the thing the guide's card names.</summary>
    public void ShowGuide(string text, Transform who, Vector3 point) => Want(guide, text, who, point);
    public void HideGuide() { if (guide != null) guide.wanted = false; }

    /// <summary>"You" over Ace, for the first seconds of his first day.</summary>
    public void ShowSelf(string text, Transform ace) => Want(self, text, ace, Vector3.zero);
    /// <summary>The day's "You" goes (the night's own, for its first seconds, stays until its time is up).</summary>
    public void HideSelf() { if (self != null && !SelfAtNight) self.wanted = false; }

    /// <summary>
    /// "You" over Ace for <paramref name="seconds"/> at night, the one pin the night shows (NightCycle, as the night begins):
    /// it goes by itself, and nothing else is pinned meanwhile.
    /// </summary>
    public void ShowSelfAtNight(string text, Transform ace, float seconds)
    {
        selfAtNightUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        Want(self, text, ace, Vector3.zero);
    }
    float selfAtNightUntil = -1f;
    /// <summary>The night's "You" is still allowed (its seconds aren't up).</summary>
    public bool SelfAtNight => Time.unscaledTime < selfAtNightUntil;

    // ------------------------------------------------------------------ what checks read

    public bool FocusShowing => Showing(focus);
    public bool GuideShowing => Showing(guide);
    public bool SelfShowing => Showing(self);
    /// <summary>How much of "You" is drawn now: 0 when it's off or has faded out, 1 when it's all there.</summary>
    public float SelfAlpha => self != null && self.root.gameObject.activeSelf ? self.group.alpha : 0f;
    public string FocusText => focus != null ? focus.text : "";
    public string GuideText => guide != null ? guide.text : "";
    public string SelfText => self != null ? self.text : "";
    /// <summary>The size the name's words are drawn at (NameSize), and Day 1's pins' (PinTextSize).</summary>
    public float FocusTextSize => focus != null ? focus.label.fontSize : 0f;
    public float GuideTextSize => guide != null ? guide.label.fontSize : 0f;
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
        BarView v = ViewOf(who);
        if (v == null) return false;
        onScreen = v.onScreen;
        colour = v.fillImage.color;
        dot = v.rim.gameObject.activeSelf;   // the rim holds the dot: the dot's own activeSelf never changes
        return true;
    }

    /// <summary>The job colour's dot beside the bar over <paramref name="who"/> as last drawn (screen pixels from the
    /// bottom left, its paper rim included), when it shows.</summary>
    public bool TryGetDot(Transform who, out Rect onScreen)
    {
        onScreen = default;
        BarView v = ViewOf(who);
        if (v == null || !v.rim.gameObject.activeSelf) return false;
        onScreen = v.dotOnScreen;
        return true;
    }

    BarView ViewOf(Transform who)
    {
        foreach (BarView v in views)
            if (v.bar != null && v.bar.Owner == who && v.root.gameObject.activeSelf) return v;
        return null;
    }

    /// <summary>The bar's colour at this much patience: the ticket's meter's (JobTicket), brightened for the dark track.</summary>
    public static Color ColourAt(float patience)
    {
        patience = Mathf.Clamp01(patience);
        Color low = new Color(.93f, .36f, .29f), mid = UiSkin.Gold, high = UiSkin.BrandBright;
        return patience < .5f ? Color.Lerp(low, mid, patience * 2f) : Color.Lerp(mid, high, (patience - .5f) * 2f);
    }

    /// <summary>The family as it stands, for reports: "bar 72 x 9, dot 12, name 20, Day 1's pins 19 (BarScale 1.3)".</summary>
    public static string Family() =>
        $"bar {BarWidth:0} x {BarHeight:0}, dot {TagDot:0}, name {NameSize:0}, Day 1's pins {PinTextSize:0} (BarScale {BarScale:0.0#})";

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
        v.fill.offsetMin = new Vector2(TrackEdge, TrackEdge);
        v.fill.offsetMax = new Vector2(BarWidth - TrackEdge, -TrackEdge);
        v.fillImage = UiSkin.Paint(v.fill, UiSkin.BrandBright, (BarHeight - 2f * TrackEdge) * .5f);
        // The job's colour: a dot on a paper rim, just left of the bar.
        v.rim = Box("Their job's colour", v.root);
        v.rim.anchorMin = v.rim.anchorMax = new Vector2(0f, .5f);
        v.rim.pivot = new Vector2(1f, .5f);
        v.rim.anchoredPosition = new Vector2(-DotGap, 0f);
        v.rim.sizeDelta = new Vector2(TagDot, TagDot);
        v.rimImage = v.rim.gameObject.AddComponent<Image>();
        v.rimImage.sprite = UiSkin.Circle;
        v.rimImage.color = UiSkin.Paper;
        v.rimImage.raycastTarget = false;
        v.dot = Box("Dot", v.rim);
        v.dot.anchorMin = v.dot.anchorMax = new Vector2(.5f, .5f);
        v.dot.pivot = new Vector2(.5f, .5f);
        v.dot.anchoredPosition = Vector2.zero;
        v.dot.sizeDelta = new Vector2(TagDot - 2f * DotRim, TagDot - 2f * DotRim);
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
        pin.aboutWho = who;
        pin.aboutPoint = point;
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

    // Whether everything is put away: the recap, a conversation, a close-up, the morning's intro (the night: Night()).
    bool Hidden()
    {
        if (IntroPlaying()) return true;
        if (RecapUI.Showing) return true;
        if (ConversationController.AnyOpen) return true;
        if (player == null)
        {
            PlayerCarry carry = PlayerCarry.Instance;
            if (carry != null) player = carry.GetComponent<PlayerInteractor>();
        }
        return player != null && player.InCloseUp;
    }

    static bool Night() => NightWalk.Instance != null && NightWalk.Instance.Active;

    void Place()
    {
        if (canvas == null) return;
        // At night everything is off but the night's own "You" for its first seconds (playtest 7).
        bool night = Night();
        bool selfOnly = night && SelfAtNight && !Hidden();
        hidden = Hidden() || night && !selfOnly;
        all.alpha = hidden ? 0f : 1f;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float dt = UiClock.Delta;
        PlaceBars(scale, night);
        PlacePin(focus, scale, dt, night);
        PlacePin(guide, scale, dt, night || SameAsFocus() || OnFocusOnScreen(scale));
        PlacePin(self, scale, dt, !OverheadOnScreen());
    }

    // "You" is for finding Ace from above. In first person (or anywhere the overhead view isn't the one on the screen) Ace's
    // head is at or behind the camera and the pin would sit clamped at the screen's edge, bobbing (his note, 22 Oct 2026).
    // No view found (a scene without one): as before, nothing covers it.
    CafeViewMode view;
    bool OverheadOnScreen()
    {
        if (view == null)
        {
            if (player == null)
            {
                PlayerCarry carry = PlayerCarry.Instance;
                if (carry != null) player = carry.GetComponent<PlayerInteractor>();
            }
            if (player != null) view = player.GetComponent<CafeViewMode>();
        }
        return view == null || view.OverheadShown;
    }

    // The morning's intro (12-13 Oct, the Cinematics lead's DayIntro, whose static Playing says it has the screen): nothing is
    // pinned over its shots. On Day 1 "You" is up from the first frame, and a pin whose thing is off the screen waits at its
    // edge, so without this they'd sit on the grinder's close-up. Read by name, so this file compiles and runs the same with or
    // without DayIntro (no intro: never playing); looked up once a Play session. A property is read through a delegate (no
    // garbage a frame); a plain field, if that's what it is, through its FieldInfo.
    static Func<bool> introPlaying;
    static bool introLookedUp;

    /// <summary>The morning's intro has the screen now (DayIntro.Playing; false when there is no intro). The HUD's other
    /// scripts can ask here and compile the same with or without the intro.</summary>
    public static bool DayIntroPlaying => IntroPlaying();

    static bool IntroPlaying()
    {
        if (!introLookedUp)
        {
            introLookedUp = true;
            Type intro = Type.GetType("DayIntro");
            const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
            PropertyInfo property = intro != null ? intro.GetProperty("Playing", StaticPublic) : null;
            MethodInfo get = property != null && property.PropertyType == typeof(bool) ? property.GetGetMethod() : null;
            if (get != null) introPlaying = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), get);
            else
            {
                FieldInfo field = intro != null ? intro.GetField("Playing", StaticPublic) : null;
                if (field != null && field.FieldType == typeof(bool)) introPlaying = () => (bool)field.GetValue(null);
            }
        }
        return introPlaying != null && introPlaying();
    }

    // Day 1's pin over the very thing the name is pinned over would say it twice, one pin on the other: the name is enough.
    // The thing the name is about counts, not only where it is drawn: when the name hangs below a customer at the till to
    // keep clear of Ace's head, it is still her name (20 Oct, the Day 1 lab's photo: "Valerie" in green over her head and
    // "Valerie" again under it, the commonest moment of the day, talking to whoever is at the till).
    bool SameAsFocus()
    {
        if (focus == null || guide == null || !focus.wanted || !guide.wanted || !focus.placed) return false;
        if (guide.who != null) return guide.who == focus.who || guide.who == focus.aboutWho;
        if (focus.who != null || focus.aboutWho != null) return false;
        return (focus.point - guide.point).sqrMagnitude < .36f || (focus.aboutPoint - guide.point).sqrMagnitude < .36f;
    }

    // ...or where it would be drawn on the name, whatever the two are over (15 Oct, the Day 1 guide's photo: the camera set down
    // on the bench, its name and the green "Repair bench" lay on each other from above and read "Grace's Repair bench camera";
    // the bench's point and the camera's are further apart than SameAsFocus's 0.6 m, mostly in height). The name says what E
    // acts on there, and the card says where: the green pin gives way.
    bool OnFocusOnScreen(float scale)
    {
        if (focus == null || guide == null || !focus.wanted || !guide.wanted || !focus.placed || cam == null) return false;
        Vector3 s = cam.WorldToScreenPoint(guide.who != null ? Barks.HeadPointOf(guide.who) : guide.point);
        if (s.z <= 0f) return false;
        Vector2 size = guide.root.sizeDelta * scale;
        var mine = new Rect(s.x - size.x * .5f, s.y + PinGap * scale, size.x, size.y);
        Rect theirs = focus.onScreen;
        float m = 6f * scale;
        return mine.xMin < theirs.xMax + m && mine.xMax > theirs.xMin - m && mine.yMin < theirs.yMax + m && mine.yMax > theirs.yMin - m;
    }

    void PlaceBars(float scale, bool night)
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
            bool on = bar != null && !hidden && !night && cam != null && bar.Owner != null && bar.isActiveAndEnabled;
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
            v.fill.offsetMax = new Vector2(Mathf.Max(BarHeight - 2f * TrackEdge, (BarWidth - 2f * TrackEdge) * f) + TrackEdge, -TrackEdge);
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
            // Where the bar and the dot are drawn, as drawn (on an overlay canvas a corner's world position is its screen pixel),
            // so a check measures what's on the screen rather than the sizes it was given.
            v.onScreen = DrawnRect(v.root);
            v.dotOnScreen = DrawnRect(v.rim);
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
            // "You" points at Ace: off the screen it waits rather than sitting at the edge (23 Oct 2026, the review of the night's
            // first-person fix: coming back from first person, the camera's blend still has Ace's head off the screen for a moment).
            if (pin == self) want &= s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;
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
        // The bigger words of 12 Oct make a pin a few pixels taller and wider: both clamps measure the pin as it is now.
        Vector2 size = pin.root.sizeDelta;
        Rect area = canvasRect.rect;
        at.x = Mathf.Clamp(at.x, EdgeMargin + size.x * .5f, Mathf.Max(EdgeMargin + size.x * .5f, area.width - EdgeMargin - size.x * .5f));
        at.y = Mathf.Clamp(at.y, EdgeMargin, Mathf.Max(EdgeMargin, area.height - TopReserve - size.y));
        pin.root.anchoredPosition = at;
        pin.onScreen = new Rect((at.x - size.x * .5f) * scale, at.y * scale, size.x * scale, size.y * scale);
    }

    static Rect DrawnRect(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
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
