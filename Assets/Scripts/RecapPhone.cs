using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE RECAP AS ACE'S PHONE (claude/playtest-2-plan.md §4.3 and §9: the second playtest's note 5)
//
// At closing Ace checks the phone, instead of reading three columns of text. Four apps on a tab bar:
//
//   Reviews    today's takings on a dark card (the day's full numbers behind Details, and a failed
//              save in red), a summary with five bars, and every review of the day as a card, newest
//              first: an avatar, the name, 1-5 stars, "today" and the line. No average anywhere, so
//              "stars" still only ever means the café's (Franchise).
//   Franchise  the café's stars, its level, the bar to the next star, HQ's requests, and what changed
//              today (with one line about the day's worst kind of review).
//   Shop       what the café has (cups and beans, low under 10), the restock and the upgrades. Buying
//              works as it always did: only at closing, and saved at once (UpgradeShopUI.TryBuy).
//   Notes      the notebook, person by person, where they live first; today's facts marked NEW.
//
// "Close up for the night" sits above the tab bar in every app. It IS the recap's button: RecapUI takes
// it over with its own wiring (the night first, then tomorrow) and its own label ("Open Tomorrow" once
// the night has been walked). Badges keep the important things from hiding behind a tab: Franchise
// gets a dot on the day a star is earned, Shop a "!" while cups or beans are low, Notes the number of
// facts learned today.
//
// Everything on it comes from data the game already keeps (DayClock, SaveManager's reputation and
// notebook, ShopInventory, ShopEconomy, UpgradeManager): no row is made up to fill a gap. It is built
// in code while playing, like the night's screens, so the scene doesn't change: RecapUI makes it (its
// Use Phone switch, on), and the three-column recap stays in the scene as the fallback.
//
// Controls. The mouse clicks, and the wheel (or a drag) scrolls. Q/E or the left and right arrows
// switch apps, 1-4 jump to one, W/S or the up and down arrows scroll. On a pad, LB/RB switch apps, the
// right stick scrolls, the D-pad moves between the buttons (a gold ring shows which) and A presses.
// The hint for the device in use sits under the phone.
//
// Placeholder copy (the apps' names, HQ's requests, the day's lines) is Mansoor's to rewrite.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class RecapPhone : MonoBehaviour
{
    public enum App { Reviews, Franchise, Shop, Notes }
    public static readonly string[] AppNames = { "Reviews", "Franchise", "Shop", "Notes" };

    /// <summary>Over the night's notebook (40) and the straight face (45), under the circuit (80) and the night's fade (90).</summary>
    public const int SortingOrder = 60;

    // ------------------------------------------------------------------ the look (the mock-up's colours)

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    static readonly Color Ink = Hex(0x1f1b17), Soft = Hex(0x6b645c), Faint = Hex(0xa39b91), Line = Hex(0xece6de),
        CardWhite = Color.white, ScreenColour = Hex(0xf7f3ee), Accent = Hex(0xc9702d), AccentSoft = Hex(0xf6e3d3),
        Gold = Hex(0xf2a61f), GoldSoft = Hex(0xfdebd0), GoldInk = Hex(0xa8620f), StarOff = Hex(0xd9d2c8),
        Green = Hex(0x3f8a54), GreenSoft = Hex(0xdcefe1), Red = Hex(0xb8483c), Track = Hex(0xefe9e1), TabOff = Hex(0xa79d91),
        ButtonOff = Hex(0xe6dfd6), Bezel = Hex(0x111111), DarkText = Hex(0xf7efe6), DarkFaint = Hex(0xcbbfb2),
        DarkLine = Hex(0x3a332d), IconBox = Hex(0xf3ede6), Cream = Hex(0xe9dfd3), SaveFailed = Hex(0xffb3a7);
    const string FaintHex = "#A39B91", AccentHex = "#C9702D";

    static readonly Color[] AvatarColours =
    {
        Hex(0x4f7a9a), Hex(0xb8864a), Hex(0x7b6aa3), Hex(0x9a4f4f), Hex(0x5f8a6a), Hex(0x8a6f4f), Hex(0x4f8a8a), Hex(0xa0617f)
    };

    // ------------------------------------------------------------------ sizes, in the canvas's 1080-high units

    const float PhoneWidth = 470f, PhoneHeight = 960f, BezelWidth = 14f;
    const float DisplayWidth = PhoneWidth - 2f * BezelWidth, DisplayHeight = PhoneHeight - 2f * BezelWidth;
    const float StatusHeight = 54f, TabsHeight = 76f, CloseHeight = 56f, CloseGap = 10f;
    const float Pad = 18f;                                     // the apps' side margin
    const float ContentWidth = DisplayWidth - 2f * Pad;        // a card's width
    const float CardPadX = 16f, CardGap = 10f;
    const float ScrollSpeed = 1100f;                           // keys and the right stick, per second
    const float Unbounded = 100000f;
    // TextMesh Pro's bold is the regular letters thickened, which spreads them out; this pulls them back
    // together, closer to the mock-up's bold (in hundredths of an em).
    const float BoldTightening = -2.5f;

    // The rounded-corner texture: its corner radius is its slice border, in pixels.
    const int RoundPixels = 96;
    const float RoundRadius = 40f;

    // ------------------------------------------------------------------ what the rest of the game uses

    /// <summary>What opens and closes (RecapUI's panel): the dim over the café, the phone, the hint.</summary>
    public GameObject Root { get; private set; }
    /// <summary>"Close up for the night" (RecapUI's button: it sets the label and does the closing).</summary>
    public Button CloseButton { get; private set; }
    public TMP_Text CloseLabel { get; private set; }

    public App Current { get; private set; } = App.Reviews;
    public bool DetailsOpen { get; private set; }

    // What the phone showed when it was last built (for the checks and reports).
    public bool FranchiseDot { get; private set; }
    public bool ShopAlert { get; private set; }
    public int NotesNew { get; private set; }
    public int ReviewCardsShown { get; private set; }
    public int Builds { get; private set; }

    TMP_FontAsset font;
    RectTransform phoneBody, viewport, content, focusRing;
    ScrollRect scroll;
    TMP_Text clockText, hintText;
    readonly Button[] tabs = new Button[4];
    readonly Image[] tabIcons = new Image[4];
    readonly TMP_Text[] tabLabels = new TMP_Text[4];
    readonly GameObject[] badges = new GameObject[4];
    readonly TMP_Text[] badgeTexts = new TMP_Text[4];
    readonly List<Button> contentButtons = new List<Button>();
    readonly Dictionary<Button, string> buttonKeys = new Dictionary<Button, string>();
    readonly List<Object> made = new List<Object>();    // textures and sprites made while playing
    static readonly Vector3[] corners = new Vector3[4];

    bool dirty = true, scrollToTop = true, hintForPad, hintWritten;
    // Today's takings count up from $0 the first time an evening's phone shows them (6 Oct 2026, juice).
    TMP_Text todayMoney;
    int todayEarned, todayShown;
    float countSince = -1f;
    bool countDue = true;
    const float CountSeconds = .9f;
    GameObject lastRevealed;
    ReputationLedger preview;   // a made-up day (Fixit Fidget > Reputation > Preview), until the phone closes
    Sprite rounded, ring, circle, star, house, bag, note, tick, cupBody, cupLid, bean, beanCrease;

    // ------------------------------------------------------------------ making it

    /// <summary>Builds the phone on a canvas of its own, closed. RecapUI calls this once, in Start.</summary>
    public static RecapPhone Create()
    {
        var go = new GameObject("Recap phone (while playing)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // the phone is tall: it keeps fitting the screen's height at any shape
        var phone = go.AddComponent<RecapPhone>();
        phone.Build();
        return phone;
    }

    void Build()
    {
        font = HudFont();
        MakeSprites();

        RectTransform screen = Stretch("Screen", transform);
        Root = screen.gameObject;
        var dim = Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, .55f);   // it catches clicks, so none reach the café behind

        RectTransform body = Anchored("Phone", screen, new Vector2(.5f, .5f), new Vector2(0f, 14f), new Vector2(PhoneWidth, PhoneHeight));
        phoneBody = body;
        Paint(body, Bezel, 60f);
        RectTransform display = Anchored("Display", body, new Vector2(.5f, .5f), Vector2.zero, new Vector2(DisplayWidth, DisplayHeight));
        Paint(display, ScreenColour, 48f);
        display.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // the tab bar keeps to the screen's rounded corners

        BuildStatusBar(display);
        BuildContentArea(display);
        BuildCloseButton(display);
        BuildTabs(display);

        focusRing = Anchored("Selected (pad)", screen, new Vector2(.5f, .5f), Vector2.zero, new Vector2(10f, 10f));
        var ringImage = focusRing.gameObject.AddComponent<Image>();
        ringImage.sprite = ring;
        ringImage.type = Image.Type.Sliced;
        ringImage.pixelsPerUnitMultiplier = RoundRadius / 15f;
        ringImage.color = Gold;
        ringImage.raycastTarget = false;
        focusRing.gameObject.SetActive(false);

        RectTransform hint = Anchored("Controls", screen, new Vector2(.5f, 0f), new Vector2(0f, 12f), new Vector2(1600f, 30f));
        hint.pivot = new Vector2(.5f, 0f);
        hintText = hint.gameObject.AddComponent<TextMeshProUGUI>();
        Style(hintText, 19f, Cream, TextAlignmentOptions.Center, false, FontStyles.Normal, false);

        UpdateTabs();
        Root.SetActive(false);
    }

    void BuildStatusBar(RectTransform display)
    {
        Paint(Box("Notch", display, (DisplayWidth - 128f) / 2f, 12f, 128f, 32f), Bezel, 16f);
        clockText = Words(display, "", 34f, 20f, 120f, 18f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Place(clockText.rectTransform, 34f, 18f, 130f, 26f);
        clockText.alignment = TextAlignmentOptions.MidlineLeft;
        // A battery, for the look: an outline, a charge and a nub. (No number: nothing to report.)
        RectTransform battery = Box("Battery", display, DisplayWidth - 34f - 30f, 24f, 28f, 14f);
        Paint(battery, Ink, 4f);
        Paint(Box("Inside", battery, 2f, 2f, 24f, 10f), ScreenColour, 2.5f);
        Paint(Box("Charge", battery, 4f, 4f, 15f, 6f), Ink, 1.5f);
        Paint(Box("Nub", display, DisplayWidth - 34f - 1f, 28.5f, 3f, 5f), Ink, 1f);
    }

    void BuildContentArea(RectTransform display)
    {
        float top = StatusHeight, bottom = TabsHeight + CloseGap + CloseHeight + 8f;
        viewport = Box("Apps", display, 0f, top, DisplayWidth, DisplayHeight - top - bottom);
        Paint(viewport, Color.clear, 0f, hits: true);   // the wheel and a drag scroll anywhere over it
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Box("App", viewport, 0f, 0f, DisplayWidth, 10f);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 18f;
    }

    void BuildCloseButton(RectTransform display)
    {
        RectTransform rect = Box("Close up", display, Pad, DisplayHeight - TabsHeight - CloseGap - CloseHeight, ContentWidth, CloseHeight);
        Image back = Paint(rect, Ink, 16f);
        CloseButton = Pressable(rect, back, null, null, dark: true);
        CloseLabel = Words(rect, RecapUI.NightLabel, 0f, 0f, ContentWidth, 20f, Color.white, TextAlignmentOptions.Center,
            wrap: false, style: FontStyles.Bold, rich: false);
        Place(CloseLabel.rectTransform, 0f, 0f, ContentWidth, CloseHeight);
    }

    void BuildTabs(RectTransform display)
    {
        RectTransform bar = Box("Tabs", display, 0f, DisplayHeight - TabsHeight, DisplayWidth, TabsHeight);
        Paint(bar, Color.white, 0f);
        Paint(Box("Top line", bar, 0f, 0f, DisplayWidth, 1f), Line, 0f);
        float cell = DisplayWidth / 4f;
        Sprite[] icons = { star, house, bag, note };
        Color[] badgeColours = { Accent, Gold, Red, Accent };
        for (int i = 0; i < 4; i++)
        {
            RectTransform tab = Box(AppNames[i], bar, i * cell, 1f, cell, TabsHeight - 9f);
            Image hit = Paint(tab, new Color(1f, 1f, 1f, 0f), 0f, hits: true);
            var button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            int app = i;
            button.onClick.AddListener(() => Tap((App)app));
            tabs[i] = button;
            tabIcons[i] = Picture(tab, icons[i], TabOff, cell / 2f - 13f, 10f, 26f, 26f);
            tabLabels[i] = Words(tab, AppNames[i], 0f, 40f, cell, 14f, TabOff, TextAlignmentOptions.Top, style: FontStyles.Bold, rich: false);

            RectTransform badge = Box("Badge", tab, cell / 2f + 7f, 4f, 20f, 20f);
            Paint(badge, badgeColours[i], 10f);
            TMP_Text count = Words(badge, "", 0f, 0f, 20f, 12.5f, Color.white, TextAlignmentOptions.Center, wrap: false,
                style: FontStyles.Bold, rich: false);
            RectTransform countRect = count.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.pivot = new Vector2(.5f, .5f);
            countRect.offsetMin = countRect.offsetMax = Vector2.zero;
            badgeTexts[i] = count;
            badges[i] = badge.gameObject;
            badge.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ what RecapUI and the checks call

    /// <summary>The apps are built again from the game's data before the next frame is drawn (while the phone is up).</summary>
    public void Refresh() => dirty = true;

    /// <summary>Builds the current app again at once (while the phone is up; otherwise when it next opens).</summary>
    public void RebuildNow()
    {
        if (Root != null && Root.activeInHierarchy) Rebuild();
        else dirty = true;
    }

    /// <summary>A new day's recap: back to Reviews, Details closed, at the top.</summary>
    public void NewEvening()
    {
        countDue = true;
        Current = App.Reviews;
        DetailsOpen = false;
        preview = null;
        scrollToTop = true;
        dirty = true;
        UpdateTabs();
    }

    /// <summary>Opens one of the apps, at its top.</summary>
    public void Open(App app)
    {
        Current = app;
        scrollToTop = true;
        UpdateTabs();
        RebuildNow();
    }

    /// <summary>The next app to the right (1) or the left (-1), wrapping round.</summary>
    public void Step(int by) => Tap((App)((((int)Current + by) % 4 + 4) % 4));

    public void ToggleDetails()
    {
        DetailsOpen = !DetailsOpen;
        Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    /// <summary>Shows a made-up day's reviews until the phone closes (Fixit Fidget > Reputation > Preview a busy day).</summary>
    public void Preview(ReputationLedger sample)
    {
        preview = sample;
        Current = App.Reviews;
        scrollToTop = true;
        UpdateTabs();
        RebuildNow();
    }

    public Button Tab(App app) => tabs[(int)app];
    public bool BadgeShowing(App app) => badges[(int)app] != null && badges[(int)app].activeSelf;
    public string BadgeText(App app) => BadgeShowing(app) && badgeTexts[(int)app].gameObject.activeSelf ? badgeTexts[(int)app].text : "";
    public IReadOnlyList<Button> ContentButtons => contentButtons;
    /// <summary>The phone itself (its body, bezel included): where nothing else should draw while it's up.</summary>
    public RectTransform PhoneRect => phoneBody;
    public RectTransform Viewport => viewport;
    public float ScrollOffset => content != null ? content.anchoredPosition.y : 0f;
    public float ScrollRange => content != null ? Mathf.Max(0f, content.rect.height - viewport.rect.height) : 0f;
    public bool FocusRingShowing => focusRing != null && focusRing.gameObject.activeSelf;
    public string HintText => hintText != null ? hintText.text : "";

    /// <summary>A button in the current app by its key: "details", "restock", "upgrade:&lt;asset name&gt;". Null if it isn't there.</summary>
    public Button ContentButton(string key)
    {
        foreach (KeyValuePair<Button, string> pair in buttonKeys)
            if (pair.Key != null && pair.Value == key) return pair.Key;
        return null;
    }

    /// <summary>Every piece of text in the current app, one per line, as drawn (rich-text tags included).</summary>
    public string ContentText()
    {
        var all = new StringBuilder();
        if (content == null) return "";
        foreach (TMP_Text text in content.GetComponentsInChildren<TMP_Text>(false))
            all.Append(text.text).Append('\n');
        return all.ToString();
    }

    /// <summary>True when the whole of <paramref name="target"/> is inside the apps' window.</summary>
    public bool IsFullyVisible(RectTransform target)
    {
        if (target == null || viewport == null) return false;
        target.GetWorldCorners(corners);
        Vector3 low = viewport.InverseTransformPoint(corners[0]), high = viewport.InverseTransformPoint(corners[2]);
        Rect window = viewport.rect;
        return low.y >= window.yMin - .5f && high.y <= window.yMax + .5f;
    }

    public string Describe()
    {
        if (Root == null) return "Recap phone: not built.";
        return $"Recap phone: {(Root.activeInHierarchy ? "open" : "closed")} on {AppNames[(int)Current]}, built {Builds} times; " +
               $"{ReviewCardsShown} review cards when Reviews was last built; badges: Franchise {(FranchiseDot ? "dot" : "none")}, " +
               $"Shop {(ShopAlert ? "!" : "none")}, Notes {NotesNew}; the app is {content.rect.height:0} tall in a {viewport.rect.height:0} window, " +
               $"scrolled {ScrollOffset:0}.";
    }

    // ------------------------------------------------------------------ every frame, while it's up

    void Update()
    {
        if (Root == null) return;
        if (!Root.activeInHierarchy)
        {
            preview = null;
            return;
        }
        HandleInput();
        TrackSelection();
        UpdateHint();
        CountUp();
    }

    // Today's takings, counting up (the text is made again only when the figure changes).
    void CountUp()
    {
        if (countSince < 0f || todayMoney == null) return;
        float t = (Time.unscaledTime - countSince) / CountSeconds;
        if (t < 0f) return;
        t = Mathf.Clamp01(t);
        int now = Mathf.RoundToInt(todayEarned * (1f - (1f - t) * (1f - t) * (1f - t)));
        if (now != todayShown)
        {
            todayShown = now;
            todayMoney.text = "$" + now;
            Sfx.Play2D("recap.count", .5f);
        }
        if (t >= 1f)
        {
            countSince = -1f;
            Sfx.Play2D("recap.counted");
        }
    }

    void LateUpdate()
    {
        if (Root == null || !Root.activeInHierarchy) return;
        if (dirty) Rebuild();
        PlaceFocusRing();
    }

    void HandleInput()
    {
        Keyboard keys = Keyboard.current;
        int step = 0;
        if (keys != null)
        {
            if (keys.qKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame) step--;
            if (keys.eKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame) step++;
            for (int i = 0; i < 4; i++)
                if (keys[Key.Digit1 + i].wasPressedThisFrame || keys[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Tap((App)i);
                    return;
                }
        }
        if (PadInput.Pressed(PadButton.LeftShoulder)) step--;
        if (PadInput.Pressed(PadButton.RightShoulder)) step++;
        if (step != 0)
        {
            Step(step);
            return;
        }

        float move = 0f;   // positive: further down the app
        if (keys != null)
        {
            if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move += 1f;
            if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move -= 1f;
        }
        move -= PadInput.Curved(PadInput.RightStick).y;
        if (Mathf.Abs(move) > .001f) ScrollBy(move * ScrollSpeed * Time.unscaledDeltaTime);
    }

    // A pad player's selection is kept in view (the D-pad can move it to a button scrolled out of sight).
    // A keyboard-and-mouse player keeps no selection at all: a clicked button would otherwise answer
    // Enter, and the arrows would walk a highlight around the phone.
    void TrackSelection()
    {
        EventSystem events = EventSystem.current;
        if (events == null) return;
        GameObject selected = events.currentSelectedGameObject;
        bool mine = selected != null && selected.transform.IsChildOf(Root.transform);
        if (!PadInput.UsingPad)
        {
            if (mine) events.SetSelectedGameObject(null);
            lastRevealed = null;
            return;
        }
        if (!mine || selected == lastRevealed) return;
        lastRevealed = selected;
        if (selected.transform.IsChildOf(content)) Reveal((RectTransform)selected.transform);
    }

    void PlaceFocusRing()
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        RectTransform target = selected != null ? selected.transform as RectTransform : null;
        bool show = PadInput.UsingPad && target != null && selected.activeInHierarchy && target.IsChildOf(Root.transform)
                    && (!target.IsChildOf(content) || IsFullyVisible(target));
        if (focusRing.gameObject.activeSelf != show) focusRing.gameObject.SetActive(show);
        if (!show) return;
        target.GetWorldCorners(corners);
        var screen = (RectTransform)Root.transform;
        Vector2 low = screen.InverseTransformPoint(corners[0]), high = screen.InverseTransformPoint(corners[2]);
        focusRing.anchoredPosition = (low + high) / 2f;
        focusRing.sizeDelta = high - low + new Vector2(9f, 9f);
    }

    void UpdateHint()
    {
        bool pad = PadInput.UsingPad;
        if (hintWritten && pad == hintForPad) return;
        hintWritten = true;
        hintForPad = pad;
        hintText.text = pad
            ? $"{PadInput.Label(PadButton.LeftShoulder)} / {PadInput.Label(PadButton.RightShoulder)}  Switch apps        Right stick  Scroll" +
              $"        D-pad  Move        {PadInput.Label(PadButton.South)}  Press"
            : "Q / E  Switch apps        1-4  Jump to an app        W / S  Scroll        Click  Press";
    }

    void Tap(App app)
    {
        Sfx.Play2D("phone.tap");
        Open(app);
    }

    void ScrollBy(float amount)
    {
        scroll.StopMovement();
        SetScroll(content.anchoredPosition.y + amount);
    }

    void SetScroll(float offset)
    {
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(offset, 0f, range));
    }

    // Scrolls just far enough to show the whole of target, with a little room around it.
    void Reveal(RectTransform target)
    {
        target.GetWorldCorners(corners);
        float top = -content.InverseTransformPoint(corners[1]).y, bottom = -content.InverseTransformPoint(corners[0]).y;
        float offset = content.anchoredPosition.y, window = viewport.rect.height;
        const float room = 14f;
        if (top - room < offset) SetScroll(top - room);
        else if (bottom + room > offset + window) SetScroll(bottom + room - window);
    }

    // ------------------------------------------------------------------ building the current app

    ReputationLedger Ledger => preview ?? (SaveManager.Instance != null ? SaveManager.Instance.Reputation : null);
    static Notebook Book => SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
    static int Today => DayClock.Instance != null ? DayClock.Instance.Day : 0;

    void Rebuild()
    {
        dirty = false;
        Builds++;
        // A pad player keeps their place across a rebuild (after buying, say): the same button, as far down.
        string keep = SelectedKey();
        float offset = scrollToTop ? 0f : content.anchoredPosition.y;
        scrollToTop = false;
        scroll.StopMovement();

        contentButtons.Clear();
        buttonKeys.Clear();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            if (!old.activeSelf) continue;   // already going (a second rebuild in the same frame)
            old.SetActive(false);
            Destroy(old);
        }

        clockText.text = DayClock.Instance != null ? ShopUI.FormatHour(DayClock.Instance.CurrentHour) : "";
        UpdateBadges();
        UpdateTabs();

        float y = 8f;
        switch (Current)
        {
            case App.Franchise: y = BuildFranchise(y); break;
            case App.Shop: y = BuildShop(y); break;
            case App.Notes: y = BuildNotes(y); break;
            default: y = BuildReviews(y); break;
        }
        content.sizeDelta = new Vector2(DisplayWidth, y + 8f);
        SetScroll(offset);
        WireNavigation();

        EventSystem events = EventSystem.current;
        if (keep != null && PadInput.UsingPad && events != null)
        {
            Button again = ContentButton(keep);
            if (again != null && again.interactable) events.SetSelectedGameObject(again.gameObject);
        }
        lastRevealed = null;
    }

    string SelectedKey()
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null) return null;
        foreach (KeyValuePair<Button, string> pair in buttonKeys)
            if (pair.Key != null && pair.Key.gameObject == selected) return pair.Value;
        return null;
    }

    // The D-pad's routes: down the app's buttons (the ones that can be pressed), then Close up, then the
    // tab bar; up the same way back. Built after every rebuild, since the app's buttons are new each time.
    void WireNavigation()
    {
        var live = new List<Button>();
        foreach (Button button in contentButtons) if (button != null && button.interactable) live.Add(button);
        for (int i = 0; i < live.Count; i++)
            live[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? live[i - 1] : null,
                selectOnDown = i < live.Count - 1 ? live[i + 1] : CloseButton
            };
        CloseButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = live.Count > 0 ? live[live.Count - 1] : null,
            selectOnDown = tabs[(int)Current]
        };
        for (int i = 0; i < 4; i++)
            tabs[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = i > 0 ? tabs[i - 1] : null,
                selectOnRight = i < 3 ? tabs[i + 1] : null,
                selectOnUp = CloseButton
            };
    }

    void UpdateTabs()
    {
        for (int i = 0; i < 4; i++)
        {
            Color colour = i == (int)Current ? Accent : TabOff;
            if (tabIcons[i] != null) tabIcons[i].color = colour;
            if (tabLabels[i] != null) tabLabels[i].color = colour;
        }
    }

    void UpdateBadges()
    {
        ReputationLedger rep = Ledger;
        ShopInventory stock = ShopInventory.Instance;
        Notebook book = Book;
        FranchiseDot = rep != null && rep.EarnedStarToday;
        ShopAlert = stock != null && (stock.CupsLow || stock.BeansLow);
        NotesNew = book != null ? book.LearnedOn(Today).Count : 0;
        Badge(App.Reviews, false, null);
        Badge(App.Franchise, FranchiseDot, null);
        Badge(App.Shop, ShopAlert, "!");
        Badge(App.Notes, NotesNew > 0, NotesNew > 9 ? "9+" : NotesNew.ToString());
    }

    // A dot (no text) or a small count.
    void Badge(App app, bool show, string text)
    {
        int i = (int)app;
        if (badges[i] == null) return;
        badges[i].SetActive(show);
        if (!show) return;
        bool dot = string.IsNullOrEmpty(text);
        float size = dot ? 12f : 20f;
        ((RectTransform)badges[i].transform).sizeDelta = new Vector2(dot ? size : Mathf.Max(size, 11f + 7.5f * text.Length), size);
        badgeTexts[i].gameObject.SetActive(!dot);
        badgeTexts[i].text = dot ? "" : text;
    }

    // ------------------------------------------------------------------ Reviews

    float BuildReviews(float y)
    {
        y = TodayCard(y);
        y = AppHeader(y, star, GoldSoft, Gold, "Ace's Café", "Repair café · Coffee · Reviews");
        ReputationLedger rep = Ledger;
        y = SummaryCard(y, rep);
        List<ReviewCard> cards = CardsOf(rep);
        ReviewCardsShown = 0;
        for (int i = cards.Count - 1; i >= 0; i--)   // newest first
        {
            y = ReviewCardView(y, cards[i]);
            ReviewCardsShown++;
        }
        return y;
    }

    // The day's reviews as cards. A recap saved before every review had a line of its own shows its
    // quotes instead (the best, the worst and a regular's), split back into line and signature.
    static List<ReviewCard> CardsOf(ReputationLedger rep)
    {
        var list = new List<ReviewCard>();
        if (rep == null) return list;
        if (rep.Cards.Count > 0)
        {
            list.AddRange(rep.Cards);
            return list;
        }
        for (int i = 0; i < rep.Quotes.Count; i++)
        {
            Review verdict = i < rep.QuoteReviews.Count ? rep.QuoteReviews[i] : Review.None;
            if (verdict == Review.None) continue;
            bool split = ReputationRules.SplitQuote(rep.Quotes[i], out string line, out string name);
            list.Add(new ReviewCard { name = split ? name : "a customer", line = split ? line : rep.Quotes[i], review = verdict });
        }
        return list;
    }

    // "THURSDAY · DAY 4" over today's takings (6 Oct, break-ins chunk C: the days have names, Day 1 a Monday). "· CLOSED"
    // made way for the weekday: the phone only comes out at closing, and the line has 230 px.
    static string Kicker(int day) => Weekdays.Label(day).ToUpperInvariant();

    // Today's takings on a dark card, the day's full numbers behind Details, and a failed save in red.
    float TodayCard(float y)
    {
        DayClock clock = DayClock.Instance;
        RectTransform card = Box("Today", content, Pad, y, ContentWidth, 10f);
        Paint(card, Ink, 18f);
        float right = ContentWidth - CardPadX;
        TMP_Text kicker = Words(card, clock != null ? Kicker(clock.Day) : "CLOSED", CardPadX, 13f, 230f, 14f, DarkFaint,
            style: FontStyles.Bold, rich: false, spacing: 4f);
        TMP_Text earned = Words(card, "Earned today", CardPadX, Bottom(kicker.rectTransform) + 1f, 230f, 19f, DarkText,
            style: FontStyles.Bold, rich: false);
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned : 0), 0f, 0f, 0f, 24f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        Vector2 moneySize = money.rectTransform.sizeDelta;
        // Sized for the whole figure; it counts up into that space from the right.
        money.horizontalAlignment = HorizontalAlignmentOptions.Right;
        todayMoney = money;
        todayEarned = clock != null ? clock.Earned : 0;
        if (countDue)
        {
            countDue = false;
            countSince = Time.unscaledTime + .25f;
            todayShown = 0;
        }
        if (countSince >= 0f) money.text = "$" + todayShown;
        Place(money.rectTransform, right - moneySize.x, 10f, moneySize.x, moneySize.y);
        Button details = Pill(card, DetailsOpen ? "Hide details" : "Details", right, Bottom(money.rectTransform) + 3f, 14f,
            DarkLine, DarkText, "details", ToggleDetails, true);
        float bottom = Mathf.Max(Bottom(earned.rectTransform), Bottom((RectTransform)details.transform));
        if (DetailsOpen && clock != null) bottom = DetailsList(card, clock, bottom + 11f);
        string error = SaveManager.Instance != null ? SaveManager.Instance.LastSaveError : "";
        if (!string.IsNullOrEmpty(error))
            bottom = Bottom(Words(card, "<b>Save failed:</b> <noparse>" + error + "</noparse>", CardPadX, bottom + 9f,
                ContentWidth - 2f * CardPadX, 15f, SaveFailed).rectTransform);
        card.sizeDelta = new Vector2(ContentWidth, bottom + 13f);
        return y + card.sizeDelta.y + CardGap;
    }

    // The twelve numbers the three-column recap showed, in the same order.
    float DetailsList(RectTransform card, DayClock c, float y)
    {
        Paint(Box("Rule", card, CardPadX, y, ContentWidth - 2f * CardPadX, 1f), DarkLine, 0f);
        y += 9f;
        var labels = new StringBuilder();
        var values = new StringBuilder();
        void Row(string label, string value)
        {
            if (labels.Length > 0) { labels.Append('\n'); values.Append('\n'); }
            labels.Append(label);
            values.Append(value);
        }
        Row("People served", c.Visitors.ToString());
        Row("Customers lost", c.Lost.ToString());
        Row("Turned away", c.Declined.ToString());
        Row("Orders completed", c.Served.ToString());
        Row("Café walk-ins", "$" + c.PatronIncome);
        Row("Repairs completed", c.Repairs.ToString());
        Row("      Perfect", c.Perfect.ToString());
        Row("      Good", c.Good.ToString());
        Row("      Passable", c.Passable.ToString());
        Row("Tips", "$" + c.Tips);
        Row("Earned today", "$" + c.Earned);
        Row("Closing till", "$" + c.CaptureRecap().closingTill);
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text left = Words(card, labels.ToString(), CardPadX, y, width, 15.5f, DarkFaint, rich: false);
        TMP_Text right = Words(card, values.ToString(), CardPadX, y, width, 15.5f, DarkText, TextAlignmentOptions.TopRight, rich: false);
        return Mathf.Max(Bottom(left.rectTransform), Bottom(right.rectTransform));
    }

    // How many reviews, the reputation they made, and a bar for each number of stars.
    float SummaryCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Summary", y);
        int total = rep != null ? rep.ReviewCount : 0;
        TMP_Text heading = Words(card, total == 0 ? "Today: no new reviews" : $"Today: {total} new review{(total == 1 ? "" : "s")}",
            CardPadX, 13f, 0f, 17f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (total > 0)
            Tag(card, ReputationRecap.Signed(rep.TodayChange) + " reputation", CardPadX + heading.rectTransform.sizeDelta.x + 8f,
                13f + heading.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float cy = Bottom(heading.rectTransform) + 8f;
        float trackX = CardPadX + 22f, trackWidth = ContentWidth - 2f * CardPadX - 22f - 36f;
        for (int stars = 5; stars >= 1; stars--)
        {
            int count = rep != null ? rep.Count((Review)stars) : 0;
            TMP_Text label = Words(card, stars.ToString(), CardPadX, cy, 18f, 15f, Soft, rich: false);
            float row = label.rectTransform.sizeDelta.y, barTop = cy + row / 2f - 5f;
            Paint(Box("Bar", card, trackX, barTop, trackWidth, 10f), Track, 5f);
            if (count > 0 && total > 0)
                Paint(Box("Share", card, trackX, barTop, Mathf.Max(10f, trackWidth * count / total), 10f), Gold, 5f);
            Words(card, count.ToString(), trackX + trackWidth, cy, 36f, 15f, Soft, TextAlignmentOptions.TopRight, rich: false);
            cy += row + 3f;
        }
        return FinishCard(card, cy + 10f, y);
    }

    // One review: an avatar with the initial, the name (and "regular"), 1-5 stars, "today", the line.
    float ReviewCardView(float y, ReviewCard review)
    {
        RectTransform card = CardBox("Review", y);
        Avatar(card, CardPadX, 14f, 40f, review.name);
        float x = CardPadX + 52f, width = ContentWidth - x - CardPadX;
        TMP_Text name = Words(card, review.name, x, 12f, 0f, 18f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (review.regular)
            Tag(card, "regular", x + name.rectTransform.sizeDelta.x + 8f, 12f + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float starsTop = Bottom(name.rectTransform) + 3f;
        Stars(card, x, starsTop, 17f, 3f, review.Stars);
        TMP_Text when = Words(card, "today", 0f, 0f, 0f, 15f, Faint, wrap: false, rich: false);
        Vector2 whenSize = when.rectTransform.sizeDelta;
        Place(when.rectTransform, x + 5f * 20f + 4f, starsTop + 8.5f - whenSize.y / 2f, whenSize.x, whenSize.y);
        TMP_Text line = Words(card, review.line, x, starsTop + 17f + 6f, width, 18f, Ink, rich: false);
        return FinishCard(card, Mathf.Max(14f + 40f, Bottom(line.rectTransform)) + 14f, y);
    }

    // ------------------------------------------------------------------ Franchise

    float BuildFranchise(float y)
    {
        y = AppHeader(y, house, AccentSoft, Accent, "Franchise HQ", "Your café's standing");
        ReputationLedger rep = Ledger;
        if (rep == null) return MessageCard(y, "No word from HQ yet.");
        y = StandingCard(y, rep);
        y = Section(y, "REQUESTS FROM HQ");
        y = RequestsCard(y, rep);
        y = Section(y, "WHAT CHANGED TODAY");
        return ChangesCard(y, rep);
    }

    // The café's stars, its level, and the bar to the next star.
    float StandingCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Standing", y);
        int stars = Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars);
        const float size = 44f, gap = 8f;
        Stars(card, (ContentWidth - (5f * size + 4f * gap)) / 2f, 16f, size, gap, stars);
        float cy = 16f + size + 8f;
        if (rep.EarnedStarToday)
        {
            RectTransform pill = Tag(card, "New star!", 0f, 0f, GoldSoft, GoldInk, 14f);
            Place(pill, (ContentWidth - pill.sizeDelta.x) / 2f, cy, pill.sizeDelta.x, pill.sizeDelta.y);
            cy += pill.sizeDelta.y + 6f;
        }
        TMP_Text level = Words(card, ReputationRules.NameOf(stars), CardPadX, cy, ContentWidth - 2f * CardPadX, 23f, Ink,
            TextAlignmentOptions.Top, style: FontStyles.Bold, rich: false);
        cy = Bottom(level.rectTransform) + 10f;

        int next = rep.NextThreshold;
        float barWidth = ContentWidth - 2f * CardPadX;
        float share = next < 0 ? 1f : Mathf.Clamp01(rep.Reputation / (float)next);
        Paint(Box("Progress", card, CardPadX, cy, barWidth, 12f), Track, 6f);
        if (share > 0f) Paint(Box("Done", card, CardPadX, cy, Mathf.Max(12f, barWidth * share), 12f), Accent, 6f);
        cy += 12f + 7f;
        TMP_Text left = Words(card, next < 0 ? $"Reputation {rep.Reputation}" : $"{rep.Reputation} / {next} reputation",
            CardPadX, cy, 0f, 15f, Soft, wrap: false, rich: false);
        TMP_Text right = Words(card, next < 0 ? "Five stars" : "Next: " + ReputationRules.NameOf(stars + 1), 0f, cy, 0f, 15f, Soft,
            wrap: false, rich: false);
        Vector2 rightSize = right.rectTransform.sizeDelta;
        if (left.rectTransform.sizeDelta.x + 16f + rightSize.x <= barWidth)
            Place(right.rectTransform, CardPadX + barWidth - rightSize.x, cy, rightSize.x, rightSize.y);
        else
            Place(right.rectTransform, CardPadX, Bottom(left.rectTransform) + 2f, rightSize.x, rightSize.y);   // no room beside it
        cy = Mathf.Max(Bottom(left.rectTransform), Bottom(right.rectTransform));
        return FinishCard(card, cy + 15f, y);
    }

    float RequestsCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Requests", y);
        int stars = Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars);
        int next = rep.NextThreshold;
        bool five = stars >= ReputationRules.MaxStars;
        float cy = 12f;
        cy = five
            ? Request(card, cy, null, GreenSoft, Green, "All five stars earned.", $"Reputation {rep.Reputation}", false)
            : Request(card, cy, (stars + 1).ToString(), AccentSoft, Accent, $"Reach <b>{next} reputation</b> for your {Ordinal(stars + 1)} star.",
                $"{Mathf.Max(0, next - rep.Reputation)} to go · today {ReputationRecap.Signed(rep.TodayChange)}", false);
        // Always met until getting caught at night is built (claude/reputation-spec.md §5).
        cy = Request(card, cy, null, GreenSoft, Green, "Stay out of the papers: <b>no scandal</b> running.",
            "A night-time catch puts a star on hold", true);
        cy = five
            ? Request(card, cy, null, GreenSoft, Green, "Five stars: HQ is putting the franchise offer together.", "", true)
            : Request(card, cy, "5", Track, TabOff, "At <b>five stars</b>, HQ sends the franchise offer.", "With a fee to save for", true);
        return FinishCard(card, cy + 2f, y);
    }

    // One of HQ's requests: a numbered (or ticked) circle, the request, and a smaller line under it.
    float Request(RectTransform card, float y, string number, Color back, Color ink, string text, string under, bool ruleAbove)
    {
        if (ruleAbove) y = Rule(card, y);
        RectTransform mark = Box("Mark", card, CardPadX, y + 1f, 26f, 26f);
        var disc = mark.gameObject.AddComponent<Image>();
        disc.sprite = circle;
        disc.color = back;
        disc.raycastTarget = false;
        if (number != null)
        {
            TMP_Text digits = Words(mark, number, 0f, 0f, 26f, 15f, ink, TextAlignmentOptions.Center, wrap: false,
                style: FontStyles.Bold, rich: false);
            Place(digits.rectTransform, 0f, 0f, 26f, 26f);
        }
        else Picture(mark, tick, ink, 6f, 6f, 14f, 14f);
        float x = CardPadX + 38f, width = ContentWidth - x - CardPadX;
        float bottom = Bottom(Words(card, text, x, y, width, 17.5f, Ink).rectTransform);
        if (!string.IsNullOrEmpty(under)) bottom = Bottom(Words(card, under, x, bottom + 1f, width, 15f, Faint, rich: false).rectTransform);
        return Mathf.Max(bottom, y + 28f) + 10f;
    }

    // The reviews and the reputation they made, a new star, and one line about the day's worst kind of review.
    float ChangesCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Today's changes", y);
        float width = ContentWidth - 2f * CardPadX;
        int n = rep.ReviewCount;
        float cy = Bottom(Words(card, n == 0 ? "No reviews today."
            : $"{n} review{(n == 1 ? "" : "s")}: <b>{ReputationRecap.Signed(rep.TodayChange)}</b> reputation",
            CardPadX, 13f, width, 17.5f, Ink).rectTransform);
        if (rep.EarnedStarToday)
            cy = Bottom(Words(card, $"New star: <b>{ReputationRules.NameOf(rep.StarsEarned)}</b>", CardPadX, cy + 3f, width, 17.5f, Ink).rectTransform);
        // Only from real cards: a recap saved before them doesn't know why each review said what it said.
        string lesson = rep.Cards.Count > 0 ? ReputationRecap.Lesson(rep.Cards) : "";
        if (!string.IsNullOrEmpty(lesson)) cy = Bottom(Words(card, lesson, CardPadX, cy + 3f, width, 15f, Faint, rich: false).rectTransform);
        return FinishCard(card, cy + 13f, y);
    }

    static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => n + "th" };

    // ------------------------------------------------------------------ Shop

    float BuildShop(float y)
    {
        ShopInventory stock = ShopInventory.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        UpgradeManager upgrades = UpgradeManager.Instance;
        y = AppHeader(y, bag, Hex(0xe5efe7), Green, "Supplies", till != null ? "In the till: $" + till.Money : "");

        if (stock != null)
        {
            y = Section(y, "WHAT THE CAFÉ HAS");
            RectTransform card = CardBox("Stock", y);
            float cy = 12f;
            cy = StockRow(card, cy, cupBody, cupLid, Accent, Hex(0x8f4d1d), "Paper cups", "One for every drink", stock.Cups, stock.CupsLow, false);
            cy = StockRow(card, cy, bean, beanCrease, Hex(0x6b3f1f), Hex(0xc9a27a), "Coffee beans", "For the coffee", stock.Beans, stock.BeansLow, true);
            cy = Rule(card, cy);
            bool afford = till != null && till.Money >= stock.RestockCost;
            cy = BuyRow(card, cy, "Restock cups & beans", $"+{stock.RestockAdds} each, ready for tomorrow", null,
                "$" + stock.RestockCost, "restock", BuyRestock, afford);
            y = FinishCard(card, cy + 2f, y);
            if (stock.CupsLow || stock.BeansLow)
            {
                string what = stock.CupsLow && stock.BeansLow ? "cups and beans" : stock.CupsLow ? "cups" : "beans";
                y = HintBox(y, $"Low on {what}. Restock here before tomorrow.");
            }
        }

        if (upgrades != null && upgrades.Catalogue != null && upgrades.Catalogue.Length > 0)
        {
            y = Section(y, "UPGRADES");
            RectTransform card = CardBox("Upgrades", y);
            float cy = 12f;
            bool first = true;
            foreach (UpgradeDefinition def in upgrades.Catalogue)
            {
                if (def == null) continue;
                if (!first) cy = Rule(card, cy);
                first = false;
                int level = upgrades.LevelOf(def);
                bool maxed = upgrades.IsMaxed(def);
                UpgradeDefinition bought = def;
                cy = BuyRow(card, cy, def.upgradeName, def.description, level > 0 ? "Lv." + level : null,
                    maxed ? "MAX" : "$" + def.CostAt(level), "upgrade:" + def.name, () => BuyUpgrade(bought), upgrades.CanAfford(def));
            }
            y = FinishCard(card, cy + 2f, y);
        }
        return y;
    }

    // A stock row: its icon, what it is, and how many (red when low).
    float StockRow(RectTransform card, float y, Sprite icon, Sprite overlay, Color iconColour, Color overlayColour,
                   string title, string what, int count, bool low, bool ruleAbove)
    {
        if (ruleAbove) y = Rule(card, y);
        RectTransform box = Box("Icon", card, CardPadX, y, 40f, 40f);
        Paint(box, IconBox, 11f);
        Picture(box, icon, iconColour, 9f, 9f, 22f, 22f);
        if (overlay != null) Picture(box, overlay, overlayColour, 9f, 9f, 22f, 22f);
        float x = CardPadX + 52f, width = ContentWidth - x - CardPadX - 56f;
        TMP_Text name = Words(card, title, x, y + 1f, width, 18f, Ink, style: FontStyles.Bold, rich: false);
        TMP_Text sub = Words(card, what, x, Bottom(name.rectTransform), width, 15f, Soft, rich: false);
        float bottom = Mathf.Max(y + 40f, Bottom(sub.rectTransform));
        TMP_Text number = Words(card, count.ToString(), 0f, 0f, 0f, 17f, low ? Red : Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Vector2 size = number.rectTransform.sizeDelta;
        Place(number.rectTransform, ContentWidth - CardPadX - size.x, (y + bottom) / 2f - size.y / 2f, size.x, size.y);
        return bottom + 10f;
    }

    // Something to buy: its name (and level), what it does, and its price on a button (grey when it
    // can't be bought: too dear, or maxed).
    float BuyRow(RectTransform card, float y, string title, string what, string tag, string price, string key, UnityAction buy, bool canBuy)
    {
        Button button = Pill(card, price, ContentWidth - CardPadX, y, 16f, Accent, Color.white, key, buy, canBuy);
        var buttonRect = (RectTransform)button.transform;
        float width = ContentWidth - 2f * CardPadX - buttonRect.sizeDelta.x - 12f;
        TMP_Text name = Words(card, title, CardPadX, y, 0f, 18f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (name.rectTransform.sizeDelta.x > width)
        {
            Object.Destroy(name.gameObject);
            name = Words(card, title, CardPadX, y, width, 18f, Ink, style: FontStyles.Bold, rich: false);
        }
        else if (!string.IsNullOrEmpty(tag))
            Tag(card, tag, CardPadX + name.rectTransform.sizeDelta.x + 8f, y + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float bottom = Bottom(name.rectTransform);
        if (!string.IsNullOrEmpty(what)) bottom = Bottom(Words(card, what, CardPadX, bottom, width, 15f, Soft, rich: false).rectTransform);
        float middle = (y + bottom) / 2f, height = buttonRect.sizeDelta.y;
        buttonRect.anchoredPosition = new Vector2(buttonRect.anchoredPosition.x, -(middle - height / 2f));
        return Mathf.Max(bottom, middle + height / 2f) + 10f;
    }

    void BuyRestock()
    {
        if (UpgradeShopUI.TryRestock()) Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    void BuyUpgrade(UpgradeDefinition def)
    {
        if (UpgradeShopUI.TryBuy(def)) Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    // ------------------------------------------------------------------ Notes

    float BuildNotes(float y)
    {
        y = AppHeader(y, note, Hex(0xece7f5), Hex(0x6b5aa0), "Notes", "Everything Ace knows");
        List<NotebookPerson> people = NotebookRecap.People(Book);
        if (people.Count == 0) return MessageCard(y, "Nothing written down yet.");
        int day = Today;
        foreach (NotebookPerson person in people) y = PersonCard(y, person, day);
        return y;
    }

    // One person: an avatar, the name (and how much is new today), then every fact, where they live first.
    float PersonCard(float y, NotebookPerson person, int day)
    {
        RectTransform card = CardBox("Person", y);
        Avatar(card, CardPadX, 14f, 40f, person.name);
        float x = CardPadX + 52f;
        TMP_Text name = Words(card, person.name, x, 0f, 0f, 18f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Vector2 nameSize = name.rectTransform.sizeDelta;
        Place(name.rectTransform, x, 34f - nameSize.y / 2f, nameSize.x, nameSize.y);
        int fresh = person.LearnedOn(day);
        if (fresh > 0) Tag(card, fresh + " new", x + nameSize.x + 8f, 34f, AccentSoft, Accent);
        float cy = 14f + 40f + 4f;
        float textX = CardPadX + 16f, width = ContentWidth - textX - CardPadX;
        for (int i = 0; i < person.facts.Count; i++)
        {
            NotebookFactData fact = person.facts[i];
            if (i > 0) cy = Rule(card, cy - 3f) - 3f;
            string text = "<noparse>" + NotebookRecap.Sentence(fact.text) + "</noparse>";
            string sure = NotebookRecap.Sureness(fact, day);
            if (sure.Length > 0) text += $" <color={FaintHex}>({sure})</color>";
            if (fact.day == day) text += $"  <color={AccentHex}><size=78%><b>NEW</b></size></color>";
            TMP_Text line = Words(card, text, textX, cy, width, 17.5f, Ink);
            Picture(card, circle, Accent, CardPadX + 1f, cy + 7.5f, 7f, 7f);
            cy = Bottom(line.rectTransform) + 10f;
        }
        return FinishCard(card, cy + 2f, y);
    }

    // ------------------------------------------------------------------ parts every app uses

    // An app's icon, its name and a line under it.
    float AppHeader(float y, Sprite icon, Color box, Color tint, string title, string subtitle)
    {
        y += 4f;
        RectTransform square = Box("App icon", content, Pad + 2f, y + 2f, 42f, 42f);
        Paint(square, box, 11f);
        Picture(square, icon, tint, 9f, 9f, 24f, 24f);
        float x = Pad + 2f + 42f + 12f, width = ContentWidth - (x - Pad);
        float bottom = Bottom(Words(content, title, x, y - 3f, width, 26f, Ink, style: FontStyles.Bold, rich: false).rectTransform);
        if (!string.IsNullOrEmpty(subtitle)) bottom = Bottom(Words(content, subtitle, x, bottom - 3f, width, 16f, Soft, rich: false).rectTransform);
        return Mathf.Max(bottom, y + 46f) + 12f;
    }

    float Section(float y, string title)
    {
        TMP_Text words = Words(content, title, Pad + 4f, y + 6f, ContentWidth - 8f, 14.5f, Faint, style: FontStyles.Bold, rich: false, spacing: 6f);
        return Bottom(words.rectTransform) + 8f;
    }

    float MessageCard(float y, string text)
    {
        RectTransform card = CardBox("Message", y);
        TMP_Text words = Words(card, text, CardPadX, 15f, ContentWidth - 2f * CardPadX, 17f, Faint, rich: false);
        return FinishCard(card, Bottom(words.rectTransform) + 15f, y);
    }

    float HintBox(float y, string text)
    {
        RectTransform box = Box("Low stock", content, Pad, y, ContentWidth, 10f);
        Paint(box, AccentSoft, 12f);
        TMP_Text words = Words(box, text, 12f, 9f, ContentWidth - 24f, 16f, Accent, rich: false);
        box.sizeDelta = new Vector2(ContentWidth, Bottom(words.rectTransform) + 9f);
        return y + box.sizeDelta.y + CardGap;
    }

    // A thin line across a card; returns where the next row starts.
    float Rule(RectTransform card, float y)
    {
        Paint(Box("Rule", card, CardPadX, y, ContentWidth - 2f * CardPadX, 1f), Line, 0f);
        return y + 11f;
    }

    RectTransform CardBox(string name, float y)
    {
        RectTransform card = Box(name, content, Pad, y, ContentWidth, 10f);
        Paint(card, CardWhite, 18f);
        return card;
    }

    // Sizes a white card to what's on it, with a faint shadow under it; returns where the next card goes.
    float FinishCard(RectTransform card, float height, float y)
    {
        card.sizeDelta = new Vector2(ContentWidth, height);
        RectTransform shadow = Box("Shadow", content, Pad, y + 1.5f, ContentWidth, height);
        Paint(shadow, new Color(0f, 0f, 0f, .06f), 18f);
        shadow.SetSiblingIndex(card.GetSiblingIndex());
        return y + height + CardGap;
    }

    void Stars(RectTransform parent, float x, float y, float size, float gap, int filled)
    {
        for (int i = 0; i < 5; i++) Picture(parent, star, i < filled ? Gold : StarOff, x + i * (size + gap), y, size, size);
    }

    void Avatar(RectTransform parent, float x, float y, float size, string name)
    {
        Image disc = Picture(parent, circle, AvatarColour(name), x, y, size, size);
        TMP_Text letter = Words((RectTransform)disc.transform, Initial(name), 0f, 0f, size, size * .46f, Color.white,
            TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        Place(letter.rectTransform, 0f, 0f, size, size);
    }

    // "Grace" G; "a walk-in" W; "a customer" C.
    static string Initial(string name)
    {
        string said = (name ?? "").Trim();
        if (said.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) said = said.Substring(2).Trim();
        return said.Length > 0 ? char.ToUpperInvariant(said[0]).ToString() : "?";
    }

    // The same colour for the same name, every evening (a stable hash, not string.GetHashCode).
    static Color AvatarColour(string name)
    {
        uint hash = 2166136261;
        foreach (char c in name ?? "") hash = (hash ^ c) * 16777619;
        return AvatarColours[hash % (uint)AvatarColours.Length];
    }

    // A small rounded label ("regular", "Lv.1", "+2 reputation"), centred on centreY.
    RectTransform Tag(RectTransform parent, string text, float x, float centreY, Color back, Color ink, float size = 13f)
    {
        float height = Mathf.Round(size + 9f);
        RectTransform pill = Box("Tag", parent, x, centreY - height / 2f, 10f, height);
        Paint(pill, back, height / 2f);
        TMP_Text words = Words(pill, text, 0f, 0f, 0f, size, ink, TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        float width = Mathf.Ceil(words.rectTransform.sizeDelta.x) + 14f;
        pill.sizeDelta = new Vector2(width, height);
        Place(words.rectTransform, 0f, 0f, width, height);
        return pill;
    }

    // A rounded button with a word or a price on it, its right edge at right. Greyed when it can't be pressed.
    Button Pill(RectTransform parent, string label, float right, float top, float size, Color back, Color ink,
                string key, UnityAction press, bool enabled)
    {
        float height = Mathf.Round(size * 1.9f + 4f);
        RectTransform rect = Box("Button: " + key, parent, 0f, top, 10f, height);
        Image face = Paint(rect, enabled ? back : ButtonOff, height / 2f);
        TMP_Text words = Words(rect, label, 0f, 0f, 0f, size, enabled ? ink : TabOff, TextAlignmentOptions.Center, wrap: false,
            style: FontStyles.Bold, rich: false);
        float width = Mathf.Ceil(words.rectTransform.sizeDelta.x) + Mathf.Round(size * 1.6f);
        Place(rect, right - width, top, width, height);
        Place(words.rectTransform, 0f, 0f, width, height);
        Button button = Pressable(rect, face, key, press, dark: back.grayscale < .3f);
        button.interactable = enabled;
        return button;
    }

    Button Pressable(RectTransform rect, Image face, string key, UnityAction press, bool dark)
    {
        face.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        // A dark button lightens under the pointer; a light one darkens.
        colours.highlightedColor = dark ? new Color(1.45f, 1.45f, 1.45f, 1f) : new Color(.92f, .92f, .92f, 1f);
        colours.pressedColor = dark ? new Color(1.9f, 1.9f, 1.9f, 1f) : new Color(.8f, .8f, .8f, 1f);
        colours.selectedColor = Color.white;   // the gold ring shows a pad's selection
        colours.disabledColor = Color.white;   // greyed by its own colours instead
        colours.colorMultiplier = 1f;
        colours.fadeDuration = .06f;
        button.colors = colours;
        button.navigation = new Navigation { mode = Navigation.Mode.None };   // wired after each build
        if (press != null) button.onClick.AddListener(press);
        if (key != null)
        {
            contentButtons.Add(button);
            buttonKeys[button] = key;
        }
        return button;
    }

    // Text at (x, y) in its parent: wrapped to width and as tall as it needs, or on one line as wide as it needs.
    TMP_Text Words(RectTransform parent, string text, float x, float y, float width, float size, Color colour,
                   TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true,
                   FontStyles style = FontStyles.Normal, bool rich = true, float spacing = 0f)
    {
        RectTransform rect = Box("Text", parent, x, y, Mathf.Max(1f, width), 10f);
        var words = rect.gameObject.AddComponent<TextMeshProUGUI>();
        Style(words, size, colour, align, wrap, style, rich);
        words.characterSpacing = spacing != 0f ? spacing : (style & FontStyles.Bold) != 0 ? BoldTightening : 0f;
        words.text = text ?? "";
        Vector2 needs = wrap ? words.GetPreferredValues(Mathf.Max(1f, width), Unbounded) : words.GetPreferredValues(Unbounded, Unbounded);
        rect.sizeDelta = new Vector2(wrap ? Mathf.Max(1f, width) : Mathf.Ceil(needs.x) + 1f, Mathf.Ceil(needs.y));
        return words;
    }

    void Style(TMP_Text words, float size, Color colour, TextAlignmentOptions align, bool wrap, FontStyles style, bool rich)
    {
        if (font != null) words.font = font;
        words.fontSize = size;
        words.fontStyle = style;
        words.color = colour;
        words.alignment = align;
        words.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        words.overflowMode = TextOverflowModes.Overflow;
        words.richText = rich;
        words.raycastTarget = false;
        words.margin = Vector4.zero;
    }

    Image Paint(RectTransform rect, Color colour, float radius, bool hits = false)
    {
        var image = rect.gameObject.AddComponent<Image>();
        if (radius > 0f)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = RoundRadius / radius;
        }
        image.color = colour;
        image.raycastTarget = hits;
        return image;
    }

    Image Picture(RectTransform parent, Sprite sprite, Color colour, float x, float y, float width, float height)
    {
        RectTransform rect = Box(sprite != null ? sprite.name : "Picture", parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    // A box at (x, y) from its parent's top-left corner, y going down.
    static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        Place(rect, x, y, width, height);
        return rect;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static float Bottom(RectTransform rect) => -rect.anchoredPosition.y + rect.sizeDelta.y;

    static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // The HUD's own lettering, so the phone looks like part of the game; TextMesh Pro's default otherwise.
    static TMP_FontAsset HudFont()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        if (any == null) any = FindAnyObjectByType<TextMeshProUGUI>();
        return any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
    }

    // ------------------------------------------------------------------ the pictures, drawn in code

    void MakeSprites()
    {
        rounded = MakeSprite(RoundedTexture("Rounded", 0f), RoundRadius);
        ring = MakeSprite(RoundedTexture("Ring", 7f), RoundRadius);
        circle = MakeSprite(CircleTexture(128), 0f);
        star = MakeSprite(Drawn("Star", 96, new[]
        {
            V(12f, 2f), V(14.9f, 8.6f), V(22f, 9.2f), V(16.6f, 13.9f), V(18.2f, 20.9f),
            V(12f, 17.3f), V(5.8f, 20.9f), V(7.4f, 13.9f), V(2f, 9.2f), V(9.1f, 8.6f)
        }), 0f);
        house = MakeSprite(Drawn("House", 64, new[]
        {
            V(4f, 21f), V(4f, 8f), V(12f, 3f), V(20f, 8f), V(20f, 21f), V(15f, 21f), V(15f, 15f), V(9f, 15f), V(9f, 21f)
        }), 0f);
        var bagOutside = new List<Vector2> { V(6f, 7f), V(6f, 6f) };
        bagOutside.AddRange(Arc(12f, 6f, 6f, 180f, 360f, 16));
        bagOutside.AddRange(new[] { V(18f, 7f), V(21f, 7f), V(19.5f, 21f), V(4.5f, 21f), V(3f, 7f) });
        var bagHandle = new List<Vector2> { V(8f, 7f), V(16f, 7f), V(16f, 6f) };
        bagHandle.AddRange(Arc(12f, 6f, 4f, 360f, 180f, 16));
        bag = MakeSprite(Drawn("Bag", 64, bagOutside.ToArray(), bagHandle.ToArray()), 0f);
        var page = new List<Vector2> { V(5f, 2f), V(17f, 2f) };
        page.AddRange(Arc(17f, 4f, 2f, 270f, 360f, 6));
        page.Add(V(19f, 20f));
        page.AddRange(Arc(17f, 20f, 2f, 0f, 90f, 6));
        page.Add(V(5f, 22f));
        note = MakeSprite(Drawn("Note", 64, page.ToArray(), Rectangle(8f, 7f, 16f, 9f), Rectangle(8f, 11f, 16f, 13f), Rectangle(8f, 15f, 13f, 17f)), 0f);
        tick = MakeSprite(Drawn("Tick", 64, new[] { V(3.4f, 12.8f), V(5.6f, 10.6f), V(10f, 15f), V(18.4f, 6.6f), V(20.6f, 8.8f), V(10f, 19.4f) }), 0f);
        cupBody = MakeSprite(Drawn("Cup", 64, new[] { V(5f, 6f), V(19f, 6f), V(17f, 21f), V(7f, 21f) }), 0f);
        cupLid = MakeSprite(Drawn("Cup lid", 64, Rectangle(4f, 3f, 20f, 6.2f)), 0f);
        bean = MakeSprite(Drawn("Bean", 64, Ellipse(12f, 12f, 7f, 9.5f, 30f)), 0f);
        beanCrease = MakeSprite(Drawn("Bean crease", 64, Band(V(8f, 17f), V(11f, 14f), V(13f, 10f), V(15f, 6f), .9f)), 0f);
    }

    Sprite MakeSprite(Texture2D texture, float border)
    {
        made.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = texture.name;
        made.Add(sprite);
        return sprite;
    }

    static Texture2D NewTexture(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, false)
    {
        name = "Recap phone - " + name,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
    };

    // A white square with round corners (or just their outline, ring pixels thick), edges smoothed.
    static Texture2D RoundedTexture(string name, float ringWidth)
    {
        Texture2D texture = NewTexture(name, RoundPixels);
        var pixels = new Color32[RoundPixels * RoundPixels];
        for (int y = 0; y < RoundPixels; y++)
            for (int x = 0; x < RoundPixels; x++)
            {
                float cx = x + .5f, cy = y + .5f;
                float dx = Mathf.Max(RoundRadius - cx, cx - (RoundPixels - RoundRadius), 0f);
                float dy = Mathf.Max(RoundRadius - cy, cy - (RoundPixels - RoundRadius), 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - RoundRadius;   // < 0 inside; exact near every edge
                float alpha = Mathf.Clamp01(.5f - outside);
                if (ringWidth > 0f) alpha *= Mathf.Clamp01(outside + ringWidth + .5f);
                pixels[y * RoundPixels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // A white disc, its edge smoothed.
    static Texture2D CircleTexture(int size)
    {
        Texture2D texture = NewTexture("Circle", size);
        var pixels = new Color32[size * size];
        float centre = size / 2f, radius = size / 2f - .5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(centre, centre));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - d + .5f) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // Filled shapes drawn on a 24 x 24 grid (y down, like the mock-up's icons), even-odd, so a shape
    // inside another is a hole. 4 x 4 samples a pixel for smooth edges. Drawn once, when the phone is made.
    static Texture2D Drawn(string name, int size, params Vector2[][] shapes)
    {
        Texture2D texture = NewTexture(name, size);
        var pixels = new Color32[size * size];
        var bounds = new Rect[shapes.Length];
        for (int s = 0; s < shapes.Length; s++)
        {
            Vector2 low = shapes[s][0], high = shapes[s][0];
            foreach (Vector2 p in shapes[s]) { low = Vector2.Min(low, p); high = Vector2.Max(high, p); }
            bounds[s] = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }
        const int samples = 4;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float gx = (x + (sx + .5f) / samples) * 24f / size;
                        float gy = 24f - (y + (sy + .5f) / samples) * 24f / size;   // texture rows go up
                        if (Inside(shapes, bounds, gx, gy)) inside++;
                    }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(inside * 255f / (samples * samples)));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static bool Inside(Vector2[][] shapes, Rect[] bounds, float x, float y)
    {
        bool inside = false;
        for (int s = 0; s < shapes.Length; s++)
        {
            if (x < bounds[s].xMin || x > bounds[s].xMax || y < bounds[s].yMin || y > bounds[s].yMax) continue;
            Vector2[] shape = shapes[s];
            for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
                if ((shape[i].y > y) != (shape[j].y > y)
                    && x < (shape[j].x - shape[i].x) * (y - shape[i].y) / (shape[j].y - shape[i].y) + shape[i].x)
                    inside = !inside;
        }
        return inside;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static Vector2[] Rectangle(float left, float top, float right, float bottom) =>
        new[] { V(left, top), V(right, top), V(right, bottom), V(left, bottom) };

    // Points along a circle's edge, from one angle to another (degrees; 270 is straight up, y being down).
    static IEnumerable<Vector2> Arc(float cx, float cy, float radius, float from, float to, int steps)
    {
        for (int i = 1; i <= steps; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
            yield return V(cx + radius * Mathf.Cos(a), cy + radius * Mathf.Sin(a));
        }
    }

    static Vector2[] Ellipse(float cx, float cy, float rx, float ry, float turn)
    {
        var points = new Vector2[48];
        float c = Mathf.Cos(turn * Mathf.Deg2Rad), s = Mathf.Sin(turn * Mathf.Deg2Rad);
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.PI * 2f / points.Length;
            float x = rx * Mathf.Cos(a), y = ry * Mathf.Sin(a);
            points[i] = V(cx + x * c - y * s, cy + x * s + y * c);
        }
        return points;
    }

    // A curved stroke (a cubic curve, half this thick either side) as a filled shape.
    static Vector2[] Band(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float half)
    {
        const int steps = 14;
        var left = new List<Vector2>();
        var right = new List<Vector2>();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1f - t;
            Vector2 at = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            Vector2 along = (3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2)).normalized;
            var across = new Vector2(-along.y, along.x) * half;
            left.Add(at + across);
            right.Add(at - across);
        }
        right.Reverse();
        left.AddRange(right);
        return left.ToArray();
    }

    void OnDestroy()
    {
        foreach (Object thing in made) if (thing != null) Destroy(thing);
        made.Clear();
    }
}
