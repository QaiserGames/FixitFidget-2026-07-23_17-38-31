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
// At closing Ace checks the phone, instead of reading three columns of text. It opens on one screen (playtest 3,
// 6 Oct 2026, claude/playtest-3-notes-and-plan.md §5.3: "WAYYY too many words"), with four apps behind a tap:
//
//   Tonight    the closing screen, no scrolling, no tab bar: the day and its takings (counting up), the café's stars
//              (and a new one), how many reviews and what they did; the three review lines worth reading (the best,
//              the worst, a regular's: ReputationLedger.PickCards); one line of notebook (today's newest fact, his
//              pages first, under whose it is, as a review line is under its name); a low-stock line only when true;
//              Close up for the night; and under it "More", which opens the apps (and says how many notes are new; the
//              low stock is already on the screen). At most WordBudget.ClosingScreen words before the review lines
//              (the check counts).
//   Reviews    today's takings on a dark card (the day's full numbers behind Details, and a failed
//              save in red), a summary with five bars, and every review of the day as a card, newest
//              first: an avatar, the name, 1-5 stars, "today" and the line. No average anywhere, so
//              "stars" still only ever means the café's (Franchise).
//   Franchise  the café's stars, its level, the bar to the next star, HQ's requests, and what changed
//              today (with one line about the day's worst kind of review).
//   Shop       what the café has (cups and beans, low under 10), the restock and the upgrades. Buying
//              works as it always did: only at closing, and saved at once (UpgradeShopUI.TryBuy).
//   Notes      the notebook as a list of people, one line each (today's newest fact, else the latest known) and how
//              much is new; tap a person for all their facts, where they live first, today's marked NEW; tap again
//              to close. His pages first.
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
//
// THE PHONE BY DAY (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.4). A second phone, made
// by PausePhone, is the pause: Esc (or Start) when nothing else is open brings it up and holds the game still. It is
// this phone in another mode, so it looks, scrolls and steers the same, with fewer apps:
//
//   Today      (by day; the Reviews tab, renamed) today so far on the dark card: the takings, what's in the till (the
//              HUD shows only today's), the café's stars and the time; then the reviews as they come in (the
//              reputation's live cards), newest first.
//   Notes      the notebook, as at closing.
//   Settings   sound, controls and picture (GameSettings), and Quit, which says plainly that the day so far isn't kept.
//
// At night it opens on Notes, with Settings. Its bottom button is "Back to work" ("Back to the night"), and Esc, Start
// or B put it away too (PausePhone). It draws over everything but the pad's cursor (95).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class RecapPhone : MonoBehaviour
{
    public enum App { Tonight, Reviews, Franchise, Shop, Notes, Settings }
    public static readonly string[] AppNames = { "Tonight", "Reviews", "Franchise", "Shop", "Notes", "Settings" };
    public const int Apps = 6;

    /// <summary>What this phone is for: the closing recap, or the pause by day or at night (PausePhone).</summary>
    public enum Mode { Recap, Pause, NightPause }
    public Mode Kind { get; private set; } = Mode.Recap;

    // The apps each mode shows, in tab order.
    static readonly App[] RecapApps = { App.Tonight, App.Reviews, App.Franchise, App.Shop, App.Notes };
    static readonly App[] DayPauseApps = { App.Reviews, App.Notes, App.Settings };
    static readonly App[] NightPauseApps = { App.Notes, App.Settings };
    App[] Visible => Kind == Mode.Recap ? RecapApps : Kind == Mode.Pause ? DayPauseApps : NightPauseApps;
    /// <summary>The apps on the tab bar now, in order.</summary>
    public IReadOnlyList<App> VisibleApps => Visible;
    /// <summary>An app's name on its tab: the Reviews app is "Today" on the phone by day.</summary>
    public string LabelOf(App app) => Kind != Mode.Recap && app == App.Reviews ? "Today" : AppNames[(int)app];

    /// <summary>Over the night's notebook (40) and the straight face (45), under the circuit (80) and the night's fade (90).</summary>
    public const int SortingOrder = 60;
    /// <summary>The phone by day (the pause): over everything but the pad's cursor (5000).</summary>
    public const int PauseSortingOrder = 95;

    // ------------------------------------------------------------------ the look (the mock-up's colours)

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    // The UI skin (playtest 3, session 3; UiSkin) was made from these; the shared ones come from it now, so the phone and
    // the rest of the game can't drift apart. Its green is the brand's (#2E7D5B): money, as on the HUD's cash stack.
    static readonly Color Ink = UiSkin.Ink, Soft = UiSkin.InkSoft, Faint = UiSkin.InkFaint, Line = UiSkin.Rule,
        CardWhite = UiSkin.Card, ScreenColour = UiSkin.Paper, Accent = UiSkin.Accent, AccentSoft = UiSkin.AccentSoft,
        Gold = UiSkin.Gold, GoldSoft = UiSkin.GoldSoft, GoldInk = UiSkin.GoldInk, StarOff = Hex(0xd9d2c8),
        StarOffDark = new Color(1f, 1f, 1f, .16f),
        Green = UiSkin.Brand, GreenSoft = UiSkin.BrandSoft, Red = UiSkin.Red, Track = UiSkin.Track, TabOff = Hex(0xa79d91),
        ButtonOff = Hex(0xe6dfd6), Bezel = Hex(0x111111), DarkText = UiSkin.BandText, DarkFaint = UiSkin.BandFaint,
        DarkLine = UiSkin.BandLine, IconBox = Hex(0xf3ede6), Cream = Hex(0xe9dfd3), SaveFailed = Hex(0xffb3a7);
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

    public App Current { get; private set; } = App.Tonight;
    public bool DetailsOpen { get; private set; }
    /// <summary>Whose facts are open in Notes (a NotebookPerson.who), or "" for the list alone.</summary>
    public string OpenPerson { get; private set; } = "";

    // What the phone showed when it was last built (for the checks and reports).
    public bool FranchiseDot { get; private set; }
    public bool ShopAlert { get; private set; }
    public int NotesNew { get; private set; }
    public int ReviewCardsShown { get; private set; }
    public int Builds { get; private set; }
    /// <summary>Tonight, as last built: its words before the review lines (the budget), the review lines shown, the people listed in Notes.</summary>
    public int TonightWords { get; private set; }
    public int TonightLines { get; private set; }
    public int PeopleListed { get; private set; }

    TMP_FontAsset font;
    RectTransform phoneBody, viewport, content, focusRing, tabBar, closeRect;
    ScrollRect scroll;
    TMP_Text clockText, hintText;
    readonly Button[] tabs = new Button[Apps];
    readonly Image[] tabIcons = new Image[Apps];
    readonly TMP_Text[] tabLabels = new TMP_Text[Apps];
    readonly GameObject[] badges = new GameObject[Apps];
    readonly TMP_Text[] badgeTexts = new TMP_Text[Apps];
    readonly List<Button> contentButtons = new List<Button>();
    readonly Dictionary<Button, string> buttonKeys = new Dictionary<Button, string>();
    readonly List<Object> made = new List<Object>();    // textures and sprites made while playing
    static readonly Vector3[] corners = new Vector3[4];

    bool dirty = true, scrollToTop = true, hintForPad, hintWritten;
    // The pad's routes: every pressable thing in the current app in build order, and which row it's on (a row of pills
    // shares one: the D-pad goes along it left and right).
    readonly List<Selectable> navOrder = new List<Selectable>();
    readonly List<int> navRows = new List<int>();
    readonly Dictionary<Slider, string> sliderKeys = new Dictionary<Slider, string>();
    int navRow;
    bool sameRow;
    // Today's takings count up from $0 the first time an evening's phone shows them (6 Oct 2026, juice).
    TMP_Text todayMoney;
    int todayEarned, todayShown;
    float countSince = -1f;
    bool countDue = true;
    const float CountSeconds = .9f;
    GameObject lastRevealed;
    ReputationLedger preview;   // a made-up day (Fixit Fidget > Reputation > Preview), until the phone closes
    Sprite rounded, ring, circle, star, house, bag, note, tick, cupBody, cupLid, bean, beanCrease, moon, gear;

    // ------------------------------------------------------------------ making it

    /// <summary>Builds the phone on a canvas of its own, closed. RecapUI calls this once, in Start.</summary>
    public static RecapPhone Create() => Create(Mode.Recap);

    /// <summary>Builds a phone for <paramref name="kind"/>, closed: the recap's (RecapUI) or the pause's (PausePhone).</summary>
    public static RecapPhone Create(Mode kind)
    {
        bool recap = kind == Mode.Recap;
        var go = new GameObject(recap ? "Recap phone (while playing)" : "Pause phone (while playing)",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = recap ? SortingOrder : PauseSortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // the phone is tall: it keeps fitting the screen's height at any shape
        var phone = go.AddComponent<RecapPhone>();
        phone.Kind = kind;
        if (!recap) phone.Current = App.Reviews;
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
        Layout();

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
        closeRect = rect;
        Image back = Paint(rect, Ink, 16f);
        CloseButton = Pressable(rect, back, null, null, dark: true);
        CloseLabel = Words(rect, Kind == Mode.Recap ? RecapUI.NightLabel : "Back to work", 0f, 0f, ContentWidth, 20f, Color.white,
            TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        Place(CloseLabel.rectTransform, 0f, 0f, ContentWidth, CloseHeight);
    }

    void BuildTabs(RectTransform display)
    {
        RectTransform bar = Box("Tabs", display, 0f, DisplayHeight - TabsHeight, DisplayWidth, TabsHeight);
        tabBar = bar;
        Paint(bar, Color.white, 0f);
        Paint(Box("Top line", bar, 0f, 0f, DisplayWidth, 1f), Line, 0f);
        float cell = DisplayWidth / Visible.Length;
        Sprite[] icons = { moon, star, house, bag, note, gear };
        Color[] badgeColours = { Accent, Accent, Gold, Red, Accent, Accent };
        for (int i = 0; i < Apps; i++)
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
        PlaceTabs();
    }

    // The mode's apps along the bar, evenly; the rest put away. (The pause phone has three by day and two at night.)
    void PlaceTabs()
    {
        App[] shown = Visible;
        float cell = DisplayWidth / shown.Length;
        for (int i = 0; i < Apps; i++)
        {
            if (tabs[i] == null) continue;
            int at = Array.IndexOf(shown, (App)i);
            GameObject tab = tabs[i].gameObject;
            if (tab.activeSelf != at >= 0) tab.SetActive(at >= 0);
            if (at < 0) continue;
            Place((RectTransform)tab.transform, at * cell, 1f, cell, TabsHeight - 9f);
            Place(tabIcons[i].rectTransform, cell / 2f - 13f, 10f, 26f, 26f);
            tabLabels[i].text = LabelOf((App)i);
            Place(tabLabels[i].rectTransform, 0f, 40f, cell, tabLabels[i].rectTransform.sizeDelta.y);
            var badge = (RectTransform)badges[i].transform;
            Place(badge, cell / 2f + 7f, 4f, badge.sizeDelta.x, badge.sizeDelta.y);
        }
    }

    // Tonight has no tab bar: the closing screen is the whole phone, and Close up sits at its foot. The apps have the bar.
    void Layout()
    {
        bool tonight = Current == App.Tonight;
        if (tabBar != null && tabBar.gameObject.activeSelf != !tonight) tabBar.gameObject.SetActive(!tonight);
        float closeTop = tonight ? DisplayHeight - 14f - CloseHeight : DisplayHeight - TabsHeight - CloseGap - CloseHeight;
        if (closeRect != null) Place(closeRect, Pad, closeTop, ContentWidth, CloseHeight);
        if (viewport != null) Place(viewport, 0f, StatusHeight, DisplayWidth, closeTop - 8f - StatusHeight);
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

    /// <summary>A new day's recap: back to Tonight, Details closed, Notes' list closed, at the top.</summary>
    public void NewEvening()
    {
        countDue = true;
        Current = App.Tonight;
        DetailsOpen = false;
        OpenPerson = "";
        preview = null;
        scrollToTop = true;
        dirty = true;
        UpdateTabs();
        Layout();
    }

    /// <summary>Opens one of the apps, at its top.</summary>
    public void Open(App app)
    {
        Current = app;
        quitArmed = false;   // anything else opened takes Quit's question back
        scrollToTop = true;
        UpdateTabs();
        Layout();
        RebuildNow();
    }

    /// <summary>Opens Notes on one person's facts (the closing screen's notebook line).</summary>
    public void OpenPersonIn(string who)
    {
        OpenPerson = who ?? "";
        Open(App.Notes);
    }

    /// <summary>The next app to the right (1) or the left (-1) along the tab bar, wrapping round.</summary>
    public void Step(int by)
    {
        App[] shown = Visible;
        int at = Mathf.Max(0, Array.IndexOf(shown, Current));
        Tap(shown[((at + by) % shown.Length + shown.Length) % shown.Length]);
    }

    // ------------------------------------------------------------------ the phone by day (PausePhone)

    /// <summary>"Quit" pressed twice in Settings (PausePhone quits).</summary>
    public event Action QuitPressed;
    bool quitArmed;
    /// <summary>True after Quit's first press, until anything else is opened.</summary>
    public bool QuitArmed => quitArmed;

    /// <summary>Opens the pause phone: by day on Today, at night on Notes.</summary>
    public void OpenPause(bool night)
    {
        if (Kind == Mode.Recap) return;
        Kind = night ? Mode.NightPause : Mode.Pause;
        Current = night ? App.Notes : App.Reviews;
        DetailsOpen = false;
        OpenPerson = "";
        quitArmed = false;
        scrollToTop = true;
        dirty = true;
        hintWritten = false;
        CloseLabel.text = night ? "Back to the night" : "Back to work";
        PlaceTabs();
        UpdateTabs();
        Layout();
        Root.SetActive(true);
    }

    /// <summary>Puts the pause phone away.</summary>
    public void ClosePause()
    {
        quitArmed = false;
        if (Root != null) Root.SetActive(false);
    }

    /// <summary>Scrolls the current app just far enough to show the button or slider with this key (checks' photos).</summary>
    public bool RevealKey(string key)
    {
        Selectable target = ContentButton(key);
        if (target == null) target = ContentSlider(key);
        if (target == null) return false;
        Canvas.ForceUpdateCanvases();
        Reveal((RectTransform)target.transform);
        return true;
    }

    /// <summary>A slider in the current app by its key ("master", "music", "effects", "look", "padlook"), or null.</summary>
    public Slider ContentSlider(string key)
    {
        foreach (KeyValuePair<Slider, string> pair in sliderKeys)
            if (pair.Key != null && pair.Value == key) return pair.Key;
        return null;
    }

    /// <summary>The first thing a pad can press in the current app (or the bottom button).</summary>
    public Selectable FirstSelectable
    {
        get
        {
            foreach (Selectable s in navOrder) if (s != null && s.IsInteractable() && s.gameObject.activeInHierarchy) return s;
            return CloseButton;
        }
    }

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
        Current = App.Tonight;
        scrollToTop = true;
        UpdateTabs();
        Layout();
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

    /// <summary>A button in the current app by its key: "details", "restock", "upgrade:&lt;asset name&gt;", "more", "notebook",
    /// "stock", "person:&lt;who&gt;". Null if it isn't there.</summary>
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
        return $"{(Kind == Mode.Recap ? "Recap" : Kind == Mode.Pause ? "Pause (day)" : "Pause (night)")} phone: " +
               $"{(Root.activeInHierarchy ? "open" : "closed")} on {LabelOf(Current)}, built {Builds} times; " +
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
            App[] shown = Visible;
            for (int i = 0; i < shown.Length; i++)
                if (keys[Key.Digit1 + i].wasPressedThisFrame || keys[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Tap(shown[i]);
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
        if (Kind != Mode.Recap)
        {
            hintText.text = pad
                ? $"{PadInput.Label(PadButton.Start)}  Back        {PadInput.Label(PadButton.LeftShoulder)} / {PadInput.Label(PadButton.RightShoulder)}  Apps" +
                  $"        D-pad  Move, adjust        {PadInput.Label(PadButton.South)}  Press"
                : $"Esc  Back        Q / E  Apps        1-{Visible.Length}  Jump        W / S  Scroll        Click  Press";
            return;
        }
        hintText.text = pad
            ? $"{PadInput.Label(PadButton.LeftShoulder)} / {PadInput.Label(PadButton.RightShoulder)}  Apps        Right stick  Scroll" +
              $"        D-pad  Move        {PadInput.Label(PadButton.South)}  Press"
            : "Q / E  Apps        1-5  Jump        W / S  Scroll        Click  Press";
    }

    void Tap(App app)
    {
        Sfx.Play2D("phone.tap");
        quitArmed = false;
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
        navOrder.Clear();
        navRows.Clear();
        sliderKeys.Clear();
        navRow = 0;
        sameRow = false;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            if (!old.activeSelf) continue;   // already going (a second rebuild in the same frame)
            old.SetActive(false);
            Destroy(old);
        }

        // The status bar's time: the night's own while the night runs (the pause phone at night).
        NightWalk night = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance : null;
        clockText.text = night != null ? ShopUI.FormatHour(night.ClockHour)
            : DayClock.Instance != null ? ShopUI.FormatHour(DayClock.Instance.CurrentHour) : "";
        UpdateBadges();
        UpdateTabs();

        float y = 8f;
        switch (Current)
        {
            case App.Reviews: y = Kind == Mode.Recap ? BuildReviews(y) : BuildToday(y); break;
            case App.Franchise: y = BuildFranchise(y); break;
            case App.Shop: y = BuildShop(y); break;
            case App.Notes: y = BuildNotes(y); break;
            case App.Settings: y = BuildSettings(y); break;
            default: y = BuildTonight(y); break;
        }
        content.sizeDelta = new Vector2(DisplayWidth, y + 8f);
        SetScroll(offset);
        WireNavigation();

        EventSystem events = EventSystem.current;
        if (keep != null && PadInput.UsingPad && events != null)
        {
            Selectable again = ContentButton(keep);
            if (again == null) again = ContentSlider(keep);
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
        foreach (KeyValuePair<Slider, string> pair in sliderKeys)
            if (pair.Key != null && pair.Key.gameObject == selected) return pair.Value;
        return null;
    }

    // A pressable thing joins the D-pad's routes: on a row of its own, or on the row before it (sameRow: pills side by side).
    void AddNav(Selectable selectable)
    {
        navOrder.Add(selectable);
        navRows.Add(sameRow && navRows.Count > 0 ? navRows[navRows.Count - 1] : ++navRow);
    }

    // The D-pad's routes: down the app's rows of pressable things (the ones that can be pressed), then the bottom
    // button, then the tab bar; up the same way back; along a row left and right. A slider is a row of its own with no
    // neighbours either side, so left and right move it (Unity's slider does that when nothing is there). Built after
    // every rebuild, since the app's buttons are new each time.
    void WireNavigation()
    {
        var rows = new List<List<Selectable>>();
        int lastRow = int.MinValue;
        for (int i = 0; i < navOrder.Count; i++)
        {
            Selectable s = navOrder[i];
            if (s == null || !s.interactable) continue;
            if (navRows[i] != lastRow) { rows.Add(new List<Selectable>()); lastRow = navRows[i]; }
            rows[rows.Count - 1].Add(s);
        }
        for (int r = 0; r < rows.Count; r++)
        {
            List<Selectable> row = rows[r];
            for (int c = 0; c < row.Count; c++)
                row[c].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = r > 0 ? rows[r - 1][Mathf.Min(c, rows[r - 1].Count - 1)] : null,
                    selectOnDown = r < rows.Count - 1 ? rows[r + 1][Mathf.Min(c, rows[r + 1].Count - 1)] : CloseButton,
                    selectOnLeft = c > 0 ? row[c - 1] : null,
                    selectOnRight = c < row.Count - 1 ? row[c + 1] : null
                };
        }
        CloseButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = rows.Count > 0 ? rows[rows.Count - 1][0] : null,
            selectOnDown = tabBar != null && tabBar.gameObject.activeSelf ? tabs[(int)Current] : null   // Tonight has no tab bar
        };
        App[] shown = Visible;
        for (int i = 0; i < shown.Length; i++)
            tabs[(int)shown[i]].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = i > 0 ? tabs[(int)shown[i - 1]] : null,
                selectOnRight = i < shown.Length - 1 ? tabs[(int)shown[i + 1]] : null,
                selectOnUp = CloseButton
            };
    }

    void UpdateTabs()
    {
        for (int i = 0; i < Apps; i++)
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
        Badge(App.Tonight, false, null);
        Badge(App.Reviews, false, null);
        Badge(App.Franchise, FranchiseDot, null);
        Badge(App.Shop, ShopAlert, "!");
        Badge(App.Notes, NotesNew > 0, NotesNew > 9 ? "9+" : NotesNew.ToString());
        Badge(App.Settings, false, null);
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

    // ------------------------------------------------------------------ Tonight (the closing screen)

    // One screen, no scrolling: the day's three numbers, the three review lines worth reading, one line of notebook, the
    // stock only when it's low, and the way to the apps. Everything but the review lines is counted against the budget.
    float BuildTonight(float y)
    {
        TonightWords = 0;
        TonightLines = 0;
        DayClock clock = DayClock.Instance;
        ReputationLedger rep = Ledger;
        y = TonightCard(y, clock, rep);
        List<ReviewCard> three = ReputationLedger.PickCards(CardsOf(rep));
        foreach (ReviewCard card in three)
        {
            y = ReviewLine(y, card);
            TonightLines++;
        }
        NotebookFactData fact = NotebookRecap.Tonight(Book, Today);
        if (fact != null) y = NotebookLine(y, fact);
        ShopInventory stock = ShopInventory.Instance;
        if (stock != null && (stock.CupsLow || stock.BeansLow)) y = StockLine(y, stock);
        return MoreRow(y);
    }

    // The day on a dark card: what it earned (counting up), the café's stars (a new one in gold), the reviews and
    // what they did; and a failed save in red.
    float TonightCard(float y, DayClock clock, ReputationLedger rep)
    {
        RectTransform card = Box("Tonight", content, Pad, y, ContentWidth, 10f);
        Paint(card, Ink, 18f);
        float right = ContentWidth - CardPadX;
        TMP_Text kicker = Counted(Words(card, clock != null ? Kicker(clock.Day) : "CLOSED", CardPadX, 14f, 230f, 14f, DarkFaint,
            style: FontStyles.Bold, rich: false, spacing: 4f));
        float top = Bottom(kicker.rectTransform) + 6f;

        // The takings, large, counting up into their own space from the left.
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned : 0), 0f, 0f, 0f, 40f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        Vector2 moneySize = money.rectTransform.sizeDelta;
        money.horizontalAlignment = HorizontalAlignmentOptions.Left;
        todayMoney = money;
        todayEarned = clock != null ? clock.Earned : 0;
        if (countDue)
        {
            countDue = false;
            countSince = Time.unscaledTime + .25f;
            todayShown = 0;
        }
        if (countSince >= 0f) money.text = "$" + todayShown;
        Place(money.rectTransform, CardPadX, top, Mathf.Max(moneySize.x, 150f), moneySize.y);
        TonightWords += 1;
        TMP_Text earned = Counted(Words(card, "earned today", CardPadX, Bottom(money.rectTransform) - 2f, 180f, 15f, DarkFaint, rich: false));

        // The café's stars, top right, and under them a new star or how many.
        const float starSize = 20f, starGap = 3f;
        float starsWidth = 5f * starSize + 4f * starGap;
        int stars = rep != null ? Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        Stars(card, right - starsWidth, top + 8f, starSize, starGap, stars, dark: true);
        float underStars = top + 8f + starSize + 7f;
        if (rep != null && rep.EarnedStarToday)
        {
            RectTransform pill = Tag(card, "New star!", 0f, 0f, GoldSoft, GoldInk, 13f);
            Place(pill, right - pill.sizeDelta.x, underStars, pill.sizeDelta.x, pill.sizeDelta.y);
            TonightWords += 2;
            underStars += pill.sizeDelta.y;
        }
        else
        {
            TMP_Text count = Counted(Words(card, stars == 0 ? "no stars yet" : stars == 1 ? "1 star" : stars + " stars", 0f, 0f, 0f, 15f, DarkFaint,
                wrap: false, rich: false));
            Place(count.rectTransform, right - count.rectTransform.sizeDelta.x, underStars, count.rectTransform.sizeDelta.x, count.rectTransform.sizeDelta.y);
            underStars = Bottom(count.rectTransform);
        }

        // The reviews, and what they did to the reputation.
        float cy = Mathf.Max(Bottom(earned.rectTransform), underStars) + 12f;
        Paint(Box("Rule", card, CardPadX, cy, ContentWidth - 2f * CardPadX, 1f), DarkLine, 0f);
        cy += 10f;
        int n = rep != null ? rep.ReviewCount : 0;
        string reviews = n == 0 ? "No reviews today." : $"{n} review{(n == 1 ? "" : "s")} today  ·  {ReputationRecap.Signed(rep.TodayChange)} reputation";
        cy = Bottom(Counted(Words(card, reviews, CardPadX, cy, ContentWidth - 2f * CardPadX, 16.5f, DarkText, rich: false)).rectTransform);

        string error = SaveManager.Instance != null ? SaveManager.Instance.LastSaveError : "";
        if (!string.IsNullOrEmpty(error))
            cy = Bottom(Words(card, "<b>Save failed:</b> <noparse>" + error + "</noparse>", CardPadX, cy + 9f, ContentWidth - 2f * CardPadX, 15f, SaveFailed).rectTransform);
        card.sizeDelta = new Vector2(ContentWidth, cy + 14f);
        return y + card.sizeDelta.y + CardGap;
    }

    // One review worth reading: the avatar, the name and its stars on one row, the line under them.
    float ReviewLine(float y, ReviewCard review)
    {
        RectTransform card = CardBox("Review line", y);
        Avatar(card, CardPadX, 12f, 32f, review.name);
        float x = CardPadX + 44f, width = ContentWidth - x - CardPadX;
        TMP_Text name = Words(card, review.name, x, 10f, 0f, 15.5f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        float nameWidth = name.rectTransform.sizeDelta.x;
        if (nameWidth > width - 5f * 16f - 10f)
        {
            Object.Destroy(name.gameObject);
            name = Words(card, review.name, x, 10f, width - 5f * 16f - 10f, 15.5f, Ink, style: FontStyles.Bold, rich: false);
            nameWidth = name.rectTransform.sizeDelta.x;
        }
        Stars(card, x + nameWidth + 8f, 10f + name.rectTransform.sizeDelta.y / 2f - 6.5f, 13f, 3f, review.Stars);
        TMP_Text line = Words(card, review.line, x, Bottom(name.rectTransform) + 2f, width, 15.5f, Soft, rich: false);
        return FinishCard(card, Mathf.Max(12f + 32f, Bottom(line.rectTransform)) + 11f, y);
    }

    // The one line of notebook: today's newest fact (his pages first), under whose it is, like a review line under its
    // name (the third playtest: a fact without a name was a riddle). Everything on Tonight is today's, so no NEW tag.
    // Tap: Notes, open on that person.
    float NotebookLine(float y, NotebookFactData fact)
    {
        RectTransform card = CardBox("Notebook line", y);
        RectTransform square = Box("Icon", card, CardPadX, 11f, 32f, 32f);
        Paint(square, Hex(0xece7f5), 9f);
        Picture(square, note, Hex(0x6b5aa0), 7f, 7f, 18f, 18f);
        float x = CardPadX + 44f, width = ContentWidth - x - CardPadX;
        bool his = fact.source == Notebook.Sources.Inherited;
        string whose = string.IsNullOrWhiteSpace(fact.name) ? fact.who ?? "" : fact.name;
        TMP_Text name = Counted(Words(card, whose, x, 10f, width, 15.5f, Ink, style: FontStyles.Bold, rich: false));
        TMP_Text words = Counted(Words(card, NotebookRecap.Sentence(fact.text), x, Bottom(name.rectTransform) + 2f, width, 15.5f, Soft,
            style: his ? FontStyles.Italic : FontStyles.Normal, rich: false));
        string who = fact.who ?? "";
        Pressable(card, card.GetComponent<Image>(), "notebook", () => OpenPersonIn(who), dark: false);
        return FinishCard(card, Mathf.Max(11f + 32f, Bottom(words.rectTransform)) + 12f, y);
    }

    // Only when the café is low: one line, and a tap opens the Shop.
    float StockLine(float y, ShopInventory stock)
    {
        string what = stock.CupsLow && stock.BeansLow ? "cups and beans" : stock.CupsLow ? "cups" : "beans";
        RectTransform box = Box("Low stock", content, Pad, y, ContentWidth, 10f);
        Image face = Paint(box, AccentSoft, 12f);
        TMP_Text words = Counted(Words(box, $"Low on {what}. Restock in Shop.", 12f, 9f, ContentWidth - 24f, 15.5f, Accent, rich: false));
        box.sizeDelta = new Vector2(ContentWidth, Bottom(words.rectTransform) + 9f);
        Pressable(box, face, "stock", () => Tap(App.Shop), dark: false);
        return y + box.sizeDelta.y + CardGap;
    }

    // The way to the apps, and what's waiting there (the low stock has its own line above, so it isn't said twice).
    float MoreRow(float y)
    {
        string label = "More";
        if (NotesNew > 0) label += $"  ·  {NotesNew} new note{(NotesNew == 1 ? "" : "s")}";
        label += "  ›";
        RectTransform rect = Box("Button: more", content, Pad, y + 2f, ContentWidth, 44f);
        Image face = Paint(rect, ButtonOff, 16f);
        TMP_Text words = Counted(Words(rect, label, 0f, 0f, ContentWidth, 15.5f, Soft, TextAlignmentOptions.Center, wrap: false,
            style: FontStyles.Bold, rich: false));
        Place(words.rectTransform, 0f, 0f, ContentWidth, 44f);
        Pressable(rect, face, "more", () => Tap(App.Reviews), dark: false);
        return y + 2f + 44f + CardGap;
    }

    // Words on the closing screen count against the budget (the review lines don't: they're the day's own words).
    TMP_Text Counted(TMP_Text words)
    {
        TonightWords += WordBudget.Words(Plain(words.text));
        return words;
    }

    /// <summary>Text without its rich-text tags (for counting words as they read).</summary>
    public static string Plain(string rich)
    {
        if (string.IsNullOrEmpty(rich)) return "";
        var plain = new StringBuilder(rich.Length);
        bool inTag = false;
        foreach (char c in rich)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; plain.Append(' '); continue; }
            if (!inTag) plain.Append(c);
        }
        return plain.ToString();
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
        y = AppHeader(y, note, Hex(0xece7f5), Hex(0x6b5aa0), "Notes", "One line a person. Tap for everything.");
        List<NotebookPerson> people = NotebookRecap.People(Book);
        PeopleListed = people.Count;
        if (people.Count == 0) return MessageCard(y, "Nothing written down yet.");
        int day = Today;
        foreach (NotebookPerson person in people)
            y = person.who == OpenPerson ? PersonCard(y, person, day) : PersonRow(y, person, day);
        return y;
    }

    void TogglePerson(string who)
    {
        OpenPerson = OpenPerson == who ? "" : who ?? "";
        Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    // One person, one line: the avatar, the name and how much is new, the latest thing known (today's if there is one),
    // in his hand for his pages. The whole row is the button that opens them.
    float PersonRow(float y, NotebookPerson person, int day)
    {
        RectTransform card = CardBox("Person", y);
        Avatar(card, CardPadX, 12f, 36f, person.name);
        float x = CardPadX + 48f, width = ContentWidth - x - CardPadX - 18f;
        TMP_Text name = Words(card, person.name, x, 11f, 0f, 17f, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        int fresh = person.LearnedOn(day);
        if (fresh > 0) Tag(card, fresh + " new", x + name.rectTransform.sizeDelta.x + 8f, 11f + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float bottom = Bottom(name.rectTransform);
        NotebookFactData fact = NotebookRecap.OneLine(person, day);
        if (fact != null)
        {
            bool his = fact.source == Notebook.Sources.Inherited;
            bottom = Bottom(Words(card, "<noparse>" + NotebookRecap.Sentence(fact.text) + "</noparse>", x, bottom + 1f, width, 15.5f, Soft,
                style: his ? FontStyles.Italic : FontStyles.Normal).rectTransform);
        }
        float height = Mathf.Max(12f + 36f, bottom) + 12f;
        TMP_Text chevron = Words(card, "›", 0f, 0f, 0f, 24f, Faint, wrap: false, rich: false);
        Place(chevron.rectTransform, ContentWidth - CardPadX - chevron.rectTransform.sizeDelta.x + 2f, height / 2f - chevron.rectTransform.sizeDelta.y / 2f,
            chevron.rectTransform.sizeDelta.x, chevron.rectTransform.sizeDelta.y);
        string who = person.who;
        Pressable(card, card.GetComponent<Image>(), "person:" + who, () => TogglePerson(who), dark: false);
        return FinishCard(card, height, y);
    }

    // One person, open: an avatar, the name (and how much is new today), then every fact, where they live first. The
    // card is the button that closes it again.
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
        TMP_Text chevron = Words(card, "×", 0f, 0f, 0f, 22f, Faint, wrap: false, rich: false);
        Place(chevron.rectTransform, ContentWidth - CardPadX - chevron.rectTransform.sizeDelta.x + 2f, 34f - chevron.rectTransform.sizeDelta.y / 2f + 4f,
            chevron.rectTransform.sizeDelta.x, chevron.rectTransform.sizeDelta.y);
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
            TMP_Text line = Words(card, text, textX, cy, width, 17.5f, Ink,
                style: fact.source == Notebook.Sources.Inherited ? FontStyles.Italic : FontStyles.Normal);
            Picture(card, circle, Accent, CardPadX + 1f, cy + 7.5f, 7f, 7f);
            cy = Bottom(line.rectTransform) + 10f;
        }
        string who = person.who;
        Pressable(card, card.GetComponent<Image>(), "person:" + who, () => TogglePerson(who), dark: false);
        return FinishCard(card, cy + 2f, y);
    }

    // ------------------------------------------------------------------ Today (the phone by day)

    // Today so far: the dark card; then, once there are any, the reviews' bars and every review as it came in, newest
    // first. Before the first one the dark card says so in one line (it used to be said three times, with five empty bars).
    float BuildToday(float y)
    {
        y = LiveCard(y);
        ReputationLedger rep = Ledger;
        IReadOnlyList<ReviewCard> cards = rep != null ? rep.TodayCards : null;
        ReviewCardsShown = 0;
        if (cards == null || cards.Count == 0) return y;
        y = SummaryCard(y, rep);
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            y = ReviewCardView(y, cards[i]);
            ReviewCardsShown++;
        }
        return y;
    }

    // The day so far on the dark card: today's takings (the HUD's cash stack), what's in the till (the HUD shows only
    // today's now), the café's stars as they stand, and the reviews so far (the time is the status bar's).
    float LiveCard(float y)
    {
        DayClock clock = DayClock.Instance;
        ReputationLedger rep = Ledger;
        RectTransform card = Box("Today so far", content, Pad, y, ContentWidth, 10f);
        Paint(card, Ink, 18f);
        float right = ContentWidth - CardPadX, width = ContentWidth - 2f * CardPadX;
        string when = clock != null ? Kicker(clock.Day) : "TODAY";   // the time is the status bar's, just above
        TMP_Text kicker = Words(card, when, CardPadX, 14f, width, 14f, DarkFaint, style: FontStyles.Bold, rich: false, spacing: 4f);
        float top = Bottom(kicker.rectTransform) + 6f;
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned : 0), CardPadX, top, 0f, 40f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        TMP_Text label = Words(card, "earned so far today", CardPadX, Bottom(money.rectTransform) - 2f, 220f, 15f, DarkFaint, rich: false);

        const float starSize = 20f, starGap = 3f;
        float starsWidth = 5f * starSize + 4f * starGap;
        int stars = rep != null ? Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        Stars(card, right - starsWidth, top + 8f, starSize, starGap, stars, dark: true);
        TMP_Text count = Words(card, stars == 0 ? "no stars yet" : stars == 1 ? "1 star" : stars + " stars", 0f, 0f, 0f, 15f, DarkFaint,
            wrap: false, rich: false);
        Vector2 countSize = count.rectTransform.sizeDelta;
        Place(count.rectTransform, right - countSize.x, top + 8f + starSize + 7f, countSize.x, countSize.y);

        float cy = Mathf.Max(Bottom(label.rectTransform), Bottom(count.rectTransform)) + 12f;
        Paint(Box("Rule", card, CardPadX, cy, width, 1f), DarkLine, 0f);
        cy += 10f;
        int till = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0;
        TMP_Text tillLabel = Words(card, "In the till", CardPadX, cy, width, 16.5f, DarkText, rich: false);
        Words(card, "$" + till, CardPadX, cy, width, 16.5f, DarkText, TextAlignmentOptions.TopRight, style: FontStyles.Bold, rich: false);
        cy = Bottom(tillLabel.rectTransform) + 4f;
        int n = rep != null ? rep.ReviewCount : 0;
        string reviews = n == 0 ? "No reviews yet: they come in as people leave."
            : $"{n} review{(n == 1 ? "" : "s")} so far  ·  {ReputationRecap.Signed(rep.TodayChange)} reputation";
        cy = Bottom(Words(card, reviews, CardPadX, cy, width, 15.5f, DarkFaint, rich: false).rectTransform);
        card.sizeDelta = new Vector2(ContentWidth, cy + 14f);
        return y + card.sizeDelta.y + CardGap;
    }

    // ------------------------------------------------------------------ Settings (the phone by day and at night)

    // Sound, controls and the picture, each kept as it changes (GameSettings); then Quit.
    float BuildSettings(float y)
    {
        y = AppHeader(y, gear, Hex(0xe6edf0), Hex(0x4f6a7a), "Settings", "Kept for next time.");

        y = Section(y, "SOUND");
        RectTransform sound = CardBox("Sound", y);
        float cy = 12f;
        cy = SliderRow(sound, cy, "Everything", GameSettings.Master, 0f, 1f, Percent, v => GameSettings.Master = v, "master");
        cy = Rule(sound, cy);
        cy = SliderRow(sound, cy, "Music", GameSettings.Music, 0f, 1f, Percent, v => GameSettings.Music = v, "music");
        cy = Rule(sound, cy);
        cy = SliderRow(sound, cy, "Effects", GameSettings.Effects, 0f, 1f, Percent, v => GameSettings.Effects = v, "effects");
        y = FinishCard(sound, cy + 2f, y);

        y = Section(y, "CONTROLS");
        RectTransform controls = CardBox("Controls", y);
        cy = 12f;
        cy = SliderRow(controls, cy, "Look sensitivity", GameSettings.LookScale, GameSettings.LookMin, GameSettings.LookMax, Times,
            v => GameSettings.LookScale = v, "look");
        cy = Rule(controls, cy);
        cy = SliderRow(controls, cy, "Pad look speed", GameSettings.PadLookScale, GameSettings.PadLookMin, GameSettings.PadLookMax, Times,
            v => GameSettings.PadLookScale = v, "padlook");
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Invert Y", "Looking up and down, turned over.", GameSettings.InvertY,
            () => Flip(() => GameSettings.InvertY = !GameSettings.InvertY), "invert");
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Aim help", "On a pad: the view slows and settles on things.", GameSettings.AimHelp,
            () => Flip(() => GameSettings.AimHelp = !GameSettings.AimHelp), "aimhelp");
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Movement help", "On a pad: walking straightens along the café.", GameSettings.MovementHelp,
            () => Flip(() => GameSettings.MovementHelp = !GameSettings.MovementHelp), "movehelp");
        y = FinishCard(controls, cy + 2f, y);

        y = Section(y, "PICTURE");
        RectTransform picture = CardBox("Picture", y);
        cy = QualityRow(picture, 12f);
        y = FinishCard(picture, cy + 2f, y);

        y = Section(y, "LEAVING");
        return QuitCard(y);
    }

    static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    static string Times(float v) => v.ToString("0.0") + "x";

    void Flip(Action change)
    {
        change();
        quitArmed = false;
        Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    // A name and its value on one line, the slider under them: drag it, click along it, or with a pad select it and
    // press the D-pad left and right (a tenth of the way a press).
    float SliderRow(RectTransform card, float cy, string label, float value, float min, float max, Func<float, string> format,
                    Action<float> set, string key)
    {
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text name = Words(card, label, CardPadX, cy, width * .7f, 17f, Ink, style: FontStyles.Bold, rich: false);
        TMP_Text shown = Words(card, format(value), CardPadX, cy, width, 16f, Soft, TextAlignmentOptions.TopRight, rich: false);
        float top = Bottom(name.rectTransform) + 6f;

        const float height = 30f, knob = 26f, trackHeight = 8f;
        RectTransform rect = Box("Slider: " + key, card, CardPadX, top, width, height);
        Paint(rect, new Color(1f, 1f, 1f, 0f), 0f, hits: true);   // the whole strip takes the pointer
        Paint(Box("Track", rect, 0f, (height - trackHeight) / 2f, width, trackHeight), Track, trackHeight / 2f);
        RectTransform fillArea = Box("Fill area", rect, 0f, (height - trackHeight) / 2f, width, trackHeight);
        RectTransform fill = Box("Fill", fillArea, 0f, 0f, 0f, trackHeight);
        Paint(fill, Green, trackHeight / 2f);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(.5f, .5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        // The Slider stretches its handle over the handle area's height (it sets the handle's anchors to (v, 0)-(v, 1)), so
        // the area is a line along the track's middle and the knob's own size is all of its height: a round knob, not a
        // tall one reaching up into the label.
        RectTransform handleArea = Box("Handle area", rect, knob / 2f, height / 2f, width - knob, 0f);
        RectTransform handle = Box("Handle", handleArea, 0f, 0f, knob, knob);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.pivot = new Vector2(.5f, .5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(knob, knob);
        Image face = handle.gameObject.AddComponent<Image>();
        face.sprite = circle;
        face.color = Color.white;
        var lift = handle.gameObject.AddComponent<Shadow>();
        lift.effectColor = new Color(0f, 0f, 0f, .28f);
        lift.effectDistance = new Vector2(0f, -2f);
        var edge = handle.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, .12f);
        edge.effectDistance = new Vector2(1f, -1f);

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = face;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
        ColorBlock colours = slider.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(.94f, .94f, .94f, 1f);
        colours.pressedColor = new Color(.86f, .86f, .86f, 1f);
        colours.selectedColor = Color.white;   // the gold ring shows a pad's selection
        colours.colorMultiplier = 1f;
        colours.fadeDuration = .06f;
        slider.colors = colours;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };   // wired after each build
        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            quitArmed = false;
            shown.text = format(v);
        });
        sliderKeys[slider] = key;
        AddNav(slider);
        return top + height + 10f;
    }

    // A name (and a line saying what it does) with a switch on the right: green and knob right when on.
    float SwitchRow(RectTransform card, float cy, string label, string detail, bool on, UnityAction flip, string key)
    {
        float width = ContentWidth - 2f * CardPadX, textWidth = width - 70f;
        TMP_Text name = Words(card, label, CardPadX, cy, textWidth, 17f, Ink, style: FontStyles.Bold, rich: false);
        float bottom = Bottom(name.rectTransform);
        if (!string.IsNullOrEmpty(detail))
            bottom = Bottom(Words(card, detail, CardPadX, bottom + 1f, textWidth, 14.5f, Soft, rich: false).rectTransform);
        const float w = 52f, h = 30f;
        float mid = (cy + bottom) / 2f;
        RectTransform toggle = Box("Button: " + key, card, CardPadX + width - w, mid - h / 2f, w, h);
        Image track = Paint(toggle, on ? Green : ButtonOff, h / 2f);
        RectTransform knob = Box("Knob", toggle, on ? w - h + 3f : 3f, 3f, h - 6f, h - 6f);
        Image dot = Paint(knob, Color.white, (h - 6f) / 2f);
        var lift = dot.gameObject.AddComponent<Shadow>();
        lift.effectColor = new Color(0f, 0f, 0f, .25f);
        lift.effectDistance = new Vector2(0f, -1.5f);
        Pressable(toggle, track, key, flip, dark: on);
        return Mathf.Max(bottom, mid + h / 2f) + 10f;
    }

    // Low, Medium and High side by side (one row for the D-pad: left and right along it); the one in force in green.
    float QualityRow(RectTransform card, float cy)
    {
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text name = Words(card, "Quality", CardPadX, cy, width, 17f, Ink, style: FontStyles.Bold, rich: false);
        float top = Bottom(name.rectTransform) + 2f;
        top = Bottom(Words(card, "Lower runs smoother on a laptop.", CardPadX, top, width, 14.5f, Soft, rich: false).rectTransform) + 9f;
        const float gap = 8f, height = 38f;
        float each = (width - 2f * gap) / 3f;
        string current = GameSettings.Quality;
        for (int i = 0; i < QualityPreset.Names.Length; i++)
        {
            string level = QualityPreset.Names[i];
            bool on = current == level;
            RectTransform pill = Box("Button: quality:" + level, card, CardPadX + i * (each + gap), top, each, height);
            Image face = Paint(pill, on ? Green : ButtonOff, height / 2f);
            TMP_Text words = Words(pill, level, 0f, 0f, each, 15.5f, on ? Color.white : Soft, TextAlignmentOptions.Center, wrap: false,
                style: FontStyles.Bold, rich: false);
            Place(words.rectTransform, 0f, 0f, each, height);
            sameRow = i > 0;
            Pressable(pill, face, "quality:" + level, () => Flip(() => GameSettings.Quality = level), dark: on);
            sameRow = false;
        }
        return top + height + 12f;
    }

    // Quit, saying plainly what isn't kept. The first press asks again (in red); the second quits (PausePhone).
    float QuitCard(float y)
    {
        RectTransform card = CardBox("Quit", y);
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text title = Words(card, "Quit the game", CardPadX, 14f, width, 17f, Ink, style: FontStyles.Bold, rich: false);
        string note = Kind == Mode.NightPause
            ? "Today and tonight aren't kept: the game saves when you open tomorrow."
            : "The day so far isn't kept: the game saves when you open tomorrow.";
        TMP_Text words = Words(card, note, CardPadX, Bottom(title.rectTransform) + 2f, width, 15.5f, quitArmed ? Red : Soft, rich: false);
        float top = Bottom(words.rectTransform) + 12f;
        RectTransform rect = Box("Button: quit", card, CardPadX, top, width, 44f);
        Image face = Paint(rect, quitArmed ? Red : UiSkin.RedSoft, 16f);
        TMP_Text label = Words(rect, quitArmed ? "Press again to quit" : "Quit", 0f, 0f, width, 16f, quitArmed ? Color.white : Red,
            TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        Place(label.rectTransform, 0f, 0f, width, 44f);
        Pressable(rect, face, "quit", PressQuit, dark: quitArmed);
        return FinishCard(card, top + 44f + 14f, y);
    }

    void PressQuit()
    {
        Sfx.Play2D("phone.tap");
        if (!quitArmed)
        {
            quitArmed = true;
            RebuildNow();
            return;
        }
        quitArmed = false;
        QuitPressed?.Invoke();
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

    // Five stars, the first filled in gold. On the dark cards an empty star is a faint outline of light, not the
    // light cards' grey (which reads as five white stars on the dark: "no stars yet" under five stars).
    void Stars(RectTransform parent, float x, float y, float size, float gap, int filled, bool dark = false)
    {
        for (int i = 0; i < 5; i++) Picture(parent, star, i < filled ? Gold : dark ? StarOffDark : StarOff, x + i * (size + gap), y, size, size);
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
            AddNav(button);
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

    // The game's lettering: the UI skin's (its swap point, else the HUD's own, else TextMesh Pro's default).
    static TMP_FontAsset HudFont() => UiSkin.Font;

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
        // A crescent (Tonight): the night side of a disc, its inner edge another disc's.
        var crescent = new List<Vector2>();
        crescent.AddRange(Arc(11f, 12f, 9f, 62.34f, 297.66f, 28));
        crescent.AddRange(Arc(14.5f, 12f, 8f, 274.87f, 85.13f, 24));
        moon = MakeSprite(Drawn("Moon", 64, crescent.ToArray()), 0f);
        // A gear (Settings): eight teeth round a ring, a hole in the middle (even-odd).
        var teeth = new List<Vector2>();
        for (int t = 0; t < 8; t++)
        {
            float a = t * 45f;
            foreach (float d in new[] { -15f, -9f, 9f, 15f })
            {
                float r = Mathf.Abs(d) < 10f ? 10.5f : 8.2f;
                float rad = (a + d) * Mathf.Deg2Rad;
                teeth.Add(V(12f + r * Mathf.Cos(rad), 12f + r * Mathf.Sin(rad)));
            }
        }
        var hole = new List<Vector2>(Arc(12f, 12f, 3.6f, 0f, 360f, 28));
        gear = MakeSprite(Drawn("Gear", 64, teeth.ToArray(), hole.ToArray()), 0f);
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
