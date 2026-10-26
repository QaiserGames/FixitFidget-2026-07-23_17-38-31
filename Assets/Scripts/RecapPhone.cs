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
//              (and a new one, or how much is still needed for the first), how many reviews and what they did; the
//              three review lines worth reading (the best, the worst, a regular's: ReputationLedger.PickCards), each
//              with its verdict; one line of notebook (today's newest fact, his pages first, under whose it is, as a
//              review line is under its name); a low-stock line only when true; Close up for the night; and under it
//              "More", which opens the apps (and says how many notes are new; the low stock is already on the screen).
//              At most WordBudget.ClosingScreen words before the review lines (the check counts).
//   Reviews    today's takings on a dark card (what your own work earned and what the café's guests paid, the day's
//              full numbers behind Details, and a failed save in red), a summary with a bar for each verdict, and
//              every review of the day as a card, newest first: an avatar, the name, its verdict, "today" and the
//              line. No average anywhere, so "stars" only ever means the café's (Franchise).
//
// PLAYTEST 4 (10 Oct 2026). A review says its verdict in the game's own words, each in its own colour ("Loved it",
// "Liked it", "Fine", "Let down", "Never again": the reputation spec §3 and §10.2, decided 25 Sept: "Reviews are verdicts
// shown as faces, not stars, so 'stars' only ever means the café"); playtest 3's phone had drifted to 1-5 stars a review.
// Under the café's stars, "3 to your first star" instead of "no stars yet" (the first hour, Day 5: nothing said what earns
// one). The takings say what your work earned apart from the café's guests, who pay without a pop now.
//
// 12-13 OCT 2026 (his answers to the findings doc). Tonight says it too (§10.7, his pick: "$X from your work · $Y from café
// guests"; the first hour's Day 1 read "$65 earned today" with nobody served, all of it the guests'): one quiet line under
// "earned today", only on an evening the guests paid, landing as the count-up lands (the day's total first, then what it
// was made of). Its 8 words count against the closing screen's budget like every other word there; RecapPhoneCheck's
// limit rises by exactly those 8, only on such an evening. Settings gains "Swap the sticks at the bench" (CONTROLS) and a
// MORNINGS section above PICTURE: "The morning", Full / Short / Off (GameSettings.SwapBenchSticks, GameSettings.DayIntro;
// §1b and §6).
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
// 24 OCT 2026 (Days; Designer P's spec §2.2, §6.3 and §6.7, his answers p1 and p2). The side project's thrift find sells by
// itself at closing (ThriftShelf): Tonight's takings count it in, its split adds "· $55 from your finds", and two lines come
// under the split: "Sold your thrift watch: +$55" and the next day's "Tomorrow: Grace again, and a customer's pocket watch."
// (DayDefinition.previewLine). RecapPhoneCheck's budget rises by exactly their words, on evenings with them. The Shop offers
// tomorrow's find, one at a time ("Thrift find for tomorrow"), once the shelf is empty.
//
// 19 OCT 2026 (playtest 7; her: "your text needs to be bolder"; the reviewers: "the phone's secondary lines ... are 12-14 px
// tall at 1080p in mid-grey on cream"). Nothing a player reads on the phone is under 18 px now (the small all-caps kickers,
// the tab labels and the badges' digits 16), and no grey on a light card is lighter than InkSoft (5.8:1 on white; the old
// InkFaint was 2.7:1); the pad's gold focus ring has a dark edge either side, so it reads on an orange button too; the
// app the tab calls Shop is headed "Shop" (it said "Supplies"), and Notes says "Press a name for everything" (nothing is
// tapped on a PC or a pad). The hint under the phone is 22 px with the buttons as icons.
//
// 26 OCT 2026 (Readability; her ask again: "your text needs to be bolder"; the day's word gather found 168 of the phone's texts
// under the floor, most at 16-18 px). One type table (TypeBody and the rest, by the sizes) in place of a size at every line:
// every line a player reads 24 px, the kickers, chips and verdicts 22, the tab names and badges' digits 20 (secondary labels),
// Close up 26, an app's title 32. So the words wrap where they did, the phone is drawn wider (620, was 470) and a little taller
// (990, was 960; it rests 28 up, so the hint under it keeps clear); Tonight is still one screen. The open tab's name is in the
// accent's ink, the badges on the price's deeper orange, a greyed price in OffInk (each 4.5:1 or more), and the hint under the
// phone 24 px with the skin's shadow (it sits over the café).
//
// THE PHONE BY DAY (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.4). A second phone, made
// by PausePhone, is the pause: Esc (or Start) when nothing else is open brings it up and holds the game still. It is
// this phone in another mode, so it looks, scrolls and steers the same, with fewer apps:
//
//   Today      (by day; the Reviews tab, renamed) today so far on the dark card: the takings, what's in the till (the
//              HUD shows only today's), the café's stars (and what's still needed for the first, today's reviews counted
//              in) and the time; then the reviews as they come in (the reputation's live cards), newest first.
//   Notes      the notebook, as at closing.
//   Settings   sound, controls (the bench's sticks among them), the morning (the day intro: Full, Short or Off) and the
//              picture (GameSettings), and Quit, which says plainly that the day so far isn't kept.
//
// At night it opens on Notes, with Settings. Its bottom button is "Back to work" ("Back to the night"), and Esc, Start
// or B put it away too (PausePhone). It draws over everything but the pad's cursor (95).
//
// THE PHONE MOVES (23 Oct 2026; his note of 22 Oct: "I also would like more juice to happen on the settings or on the phone, like
// when transitioning on, when moving to different like buttons or moving down to a different category on the phone. Like, I want
// there to be like juice, I want there to be a feeling, I want there to feel good, you know."). Every motion is quick (none over
// 0.2 s), runs on the phone's own unscaled clock (the phone pauses the game) through UiTween, and never stands in the way: a press
// during a motion finishes it at once and then acts.
//   * Out and away: the phone rises from the bottom with a slight overshoot as it comes out, its dim fading in (a game resumed
//     at closing shows it at once: it never came out of a pocket); the pause phone drops away as it is put away, while the game
//     already has its input back (PausePhone gives it back that frame). The closing recap still goes at once when Close up is
//     pressed: RecapUI switches it off itself, and the night's fade follows.
//   * Another app: it slides in from the side it was asked for (Q/LB from the left, E/RB from the right, a tab or a key by its
//     order), a third of the screen's width, as its rows (a card and its shadow, a heading) rise 12 units into place and fade in
//     a beat apart. Only the rows in the window move, and the last starts by 0.1 s: everything has settled in a quarter second.
//   * The tab bar: a soft pill behind the open tab's icon slides to the new tab, the two tabs' colours cross over, and the new
//     icon pops (1 to 1.15 and back).
//   * The pad: the gold ring glides from button to button, resizing on the way (hugging the part of a button inside the window
//     while it scrolls in, and keeping its place until some of it shows), and the app scrolls after it in a glide; LB/RB and A
//     give a short, light rumble (Settings' Vibration).
//   * A press: the button squishes to 0.94 and springs back past 1.03, with the tap. A price out of reach shakes a little when
//     clicked, and makes no sound (the bank has nothing that says "no" softly yet; Audio's to name).
//   * Scrolling: the wheel glides to where it used to jump; the keys and the stick ease in and out of their speed.
//   * Settings: a slider's knob follows its value in a glide (dragged by the mouse, it stays under the pointer); a switch's knob
//     slides across as its colour turns, with the tap.
// Settled says that nothing on the phone moves (the takings' count-up and the split's landing keep their own flags): the labs wait
// for it before they read a position, a size or a colour, or take a photo. MotionScale slows it all down for a lab's photo.
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
        Green = UiSkin.Brand, GreenSoft = UiSkin.BrandSoft, Red = UiSkin.Red, Track = UiSkin.Track, TabOff = Hex(0x756b61),
        ButtonOff = Hex(0xe6dfd6), Bezel = Hex(0x111111), DarkText = UiSkin.BandText, DarkFaint = UiSkin.BandFaint,
        DarkLine = UiSkin.BandLine, IconBox = Hex(0xf3ede6), Cream = Hex(0xe9dfd3), SaveFailed = Hex(0xffb3a7);
    const string FaintHex = "#6B645C", AccentHex = "#944915";

    // 20 OCT 2026 (Review 2's first full gather, the phone's own photos): words in a colour were still faint on their pale
    // chips. Orange on peach was 2.9:1 ("Low on beans", "+2 reputation", "Let down", "Lv.1"), the grey of a tab that isn't
    // open 2.7:1 (#A79D91, now #756B61: 5.2:1), green on mint 4.2, red on pink 3.9, gold on cream 4.1, a white price on
    // orange 3.6. The chips and boxes keep their colours; only the words on them are deeper, each 4.5:1 or more (WCAG AA
    // for small text): TextOn maps the brand's colour to its ink, and a price's pill is a deeper orange (4.6:1 under white).
    static readonly Color AccentText = Hex(0x944915), GreenText = Hex(0x256a4c), RedText = Hex(0x9e3a30),
        GoldText = Hex(0x86560a), PriceOrange = Hex(0xb35f22), LikedInk = Hex(0x5a7d2c), LikedText = Hex(0x4a6a22),
        OffInk = Hex(0x5c554e);   // 26 Oct 2026 (Readability): a greyed button's words, 5.5:1 on ButtonOff (TabOff was 3.9)

    /// <summary>The colour words take on a pale chip of the given colour: deeper, so they read (see above).</summary>
    static Color TextOn(Color ink)
    {
        if (ink == Accent) return AccentText;
        if (ink == Green) return GreenText;
        if (ink == Red) return RedText;
        if (ink == GoldInk) return GoldText;
        if (ink == LikedInk) return LikedText;   // "Liked it": olive on pale green was 4.1:1, now 5.3
        return ink;
    }

    static readonly Color[] AvatarColours =
    {
        Hex(0x4f7a9a), Hex(0xb8864a), Hex(0x7b6aa3), Hex(0x9a4f4f), Hex(0x5f8a6a), Hex(0x8a6f4f), Hex(0x4f8a8a), Hex(0xa0617f)
    };

    // ------------------------------------------------------------------ sizes, in the canvas's 1080-high units

    // 26 Oct 2026 (Readability; her ask: "your text needs to be bolder"; the word gather found the phone's lines at 16-18 px).
    // The phone is drawn wider (470 to 620) and a little taller (960 to 990), so its words could grow by a third (the type
    // table below) and still wrap where they did: a review line holds about as many words a line at 24 px in the wider phone as
    // it held at 18 in the old one, so Tonight stays one screen. 620 x 990 is still a phone's shape (about 1:1.6).
    const float PhoneWidth = 620f, PhoneHeight = 990f, BezelWidth = 14f;
    const float DisplayWidth = PhoneWidth - 2f * BezelWidth, DisplayHeight = PhoneHeight - 2f * BezelWidth;
    const float StatusHeight = 54f, TabsHeight = 76f, CloseHeight = 56f, CloseGap = 10f;
    const float Pad = 18f;                                     // the apps' side margin
    const float ContentWidth = DisplayWidth - 2f * Pad;        // a card's width
    const float CardPadX = 16f, CardGap = 10f;

    // 26 Oct 2026 (Readability): the phone's type, one table instead of a size at every line. Every line a player reads is
    // TypeBody (it was 18 px); the small all-caps kickers, chips and verdicts TypeLabel (16); the tab names and the badges'
    // digits are secondary labels, at the house's 20 px floor (16 and 14). Builders-common: "at least 24 px at 1080p (20 px
    // only for a secondary label), the most important line 28-34 px".
    const float TypeBody = 24f;     // a review line, a fact, a row's name and its line, the day's lines
    const float TypeLabel = 22f;    // WEDNESDAY · DAY 3, "Loved it", "New star!", "regular", a section's heading
    const float TypeTab = 20f;      // the tab bar's names (each under its icon)
    const float TypeBadge = 20f;    // a badge's "2" or "!"
    const float TypeButton = 26f;   // Close up for the night / Back to work
    const float TypeLead = 28f;     // the café's level under its stars
    const float TypeTitle = 32f;    // an app's title
    const float ScrollSpeed = 1100f;                           // keys and the right stick, per second
    const float Unbounded = 100000f;
    // TextMesh Pro's bold is the regular letters thickened, which spreads them out; this pulls them back
    // together, closer to the mock-up's bold (in hundredths of an em).
    const float BoldTightening = -2.5f;

    // The rounded-corner texture: its corner radius is its slice border, in pixels.
    const int RoundPixels = 96;
    const float RoundRadius = 40f;

    // ------------------------------------------------------------------ the motion (23 Oct 2026), in seconds and canvas units
    //
    // His note asked for a feeling, and Bible §4 for a phone that never slows him: so every motion is 0.2 s or less (the
    // target 0.12-0.18), an arrival eases out (quint for what must land precisely, a gentle back for what may settle), an exit
    // eases in, and the whole of an app switch is over within 0.25 s (RecapPhoneCheck holds it to that). Each is one number here.

    const float PhoneY = 28f;                                   // the phone's rest, a little above the screen's middle (26 Oct: 14 to 28, so the taller phone keeps clear of the hint under it)
    const float RiseSeconds = .18f, RiseDistance = 240f, RiseFade = .09f;      // out of the pocket: from below, a slight overshoot
    const float DropSeconds = .14f, DropDistance = 200f;                       // put away: down and out, faster as it goes
    const float SlideSeconds = .16f, SlideShare = 1f / 3f;                     // another app: in from a third of the width
    const float RowSeconds = .14f, RowStagger = .025f, RowLastStart = .10f, RowRise = 12f;   // its rows, a beat apart
    const float PillSeconds = .18f, PopSeconds = .16f, TabColourSeconds = .14f, PopScale = 1.15f;
    const float PillWidth = 58f, PillHeight = 30f;              // the open tab's pill, behind its 26-unit icon
    const float RingSeconds = .12f, RingPad = 9f, RingLeast = 8f;   // the pad's ring: its glide, its margin, the least of a button it hugs
    const float ScrollGlideSeconds = .14f, WheelStep = 18f;     // a wheel notch (the scroll rect's old sensitivity), now a glide
    const float StickEase = .04f, StickRest = 20f;              // the keys' and stick's speed eases in and out (units a second)
    const float SquishSeconds = .18f, ShakeSeconds = .18f, ShakeWidth = 5f;
    const float KnobSeconds = .14f, SliderSeconds = .10f;       // a switch's knob across; a slider's knob after its value
    const float BuzzSeconds = .05f, BuzzLow = .10f, BuzzHigh = .18f;   // a pad's press: the bench's lightest pulse (a tool picked up)

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
    /// <summary>24 Oct 2026 (Furniture): Day 1's evening, nothing bought yet: the Shop's "!" for the first table.</summary>
    public bool ShopFirstTable { get; private set; }
    public int NotesNew { get; private set; }
    public int ReviewCardsShown { get; private set; }
    public int Builds { get; private set; }
    /// <summary>Tonight, as last built: its words before the review lines (the budget), the review lines shown, the people listed in Notes.</summary>
    public int TonightWords { get; private set; }
    public int TonightLines { get; private set; }
    public int PeopleListed { get; private set; }
    /// <summary>Tonight's takings split as last built ("$132 from your work · $10 from café guests"; "" on an evening the café's
    /// guests paid nothing), and the words it added to TonightWords.</summary>
    public string TonightSplit { get; private set; } = "";
    public int TonightSplitWords { get; private set; }
    /// <summary>24 Oct 2026 (Days): Tonight's thrift lines as last built ("Sold your thrift watch: +$55", "Tomorrow: ..."; "" when
    /// none), and the words each added to TonightWords (RecapPhoneCheck's budget rises by exactly those).</summary>
    public string TonightSold { get; private set; } = "";
    public string TonightTomorrow { get; private set; } = "";
    public int TonightSoldWords { get; private set; }
    public int TonightTomorrowWords { get; private set; }
    /// <summary>Tonight's split is on screen in full (it lands as the count-up does).</summary>
    public bool TonightSplitShown => tonightSplit != null && tonightSplit.gameObject.activeInHierarchy && tonightSplit.alpha >= .99f;
    /// <summary>Today's takings are still counting up (or about to, a quarter of a second after the evening's phone opens).</summary>
    public bool TakingsCounting => countSince >= 0f;

    /// <summary>Nothing on the phone is moving: no rise or drop, slide, rows arriving, tab pill, squish, shake, ring glide, scroll
    /// glide or easing, slider or switch knob (23 Oct). The labs wait for it before they read a position, a size or a colour, or
    /// take a photo. (The takings' count-up and the split's landing are words, with their own flags: TakingsCounting,
    /// TonightSplitShown.)</summary>
    public bool Settled => !motions.Running && !ringGliding && !scrollGliding && Mathf.Abs(scrollVelocity) < StickRest && !closing
                           && !KnobsGliding && (scroll == null || scroll.velocity.sqrMagnitude < 1f);

    /// <summary>Seconds until the motion running now has ended (0 when nothing moves; the keys' and stick's easing aside), as the
    /// phone schedules it: real time, but for the frame after a build, which counts at most a 60th of a second (Now). An app
    /// switch's, read the frame it begins, is the whole of it (RecapPhoneCheck: within 0.25 s).</summary>
    public float MotionLeft
    {
        get
        {
            float now = Now(), left = motions.Left(now);
            if (ringGliding) left = Mathf.Max(left, ringSince + RingSeconds - now);
            if (scrollGliding) left = Mathf.Max(left, scrollSince + ScrollGlideSeconds - now);
            for (int i = 0; i < knobs.Count; i++) if (knobs[i].gliding) left = Mathf.Max(left, knobs[i].since + SliderSeconds - now);
            if (closing) left = Mathf.Max(left, closeAt - now);
            return Mathf.Max(0f, left) / Mathf.Max(.001f, MotionScale);
        }
    }

    /// <summary>Labs only: how fast the phone's motion runs (1 in play; a lab slows it to photograph a motion on its way).</summary>
    public float MotionScale { get; set; } = 1f;

    /// <summary>The pad's gold ring (labs: once the phone is still, it sits exactly on the selected button).</summary>
    public RectTransform FocusRing => focusRing;

    /// <summary>The pause phone is dropping away: the game has its input back already, and nothing on the phone takes any.</summary>
    public bool Closing => closing;

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
    // Tonight's split comes up as the count lands, over a quarter of a second.
    TMP_Text tonightSplit;
    const float SplitFade = .25f;
    GameObject lastRevealed;
    ReputationLedger preview;   // a made-up day (Fixit Fidget > Reputation > Preview), until the phone closes
    Sprite rounded, ring, focusRingSprite, circle, star, house, bag, note, tick, cupBody, cupLid, bean, beanCrease, moon, gear;

    // The motion (23 Oct 2026; UiTween). Its clock is the phone's own: unscaled time (the phone pauses the game), times MotionScale,
    // moved on once a frame by the first thing that asks (Now), so a motion begun anywhere in a frame starts from that frame.
    readonly UiTween.Set motions = new UiTween.Set(96);
    float motionNow;
    int motionFrame = -1, madeFrame = -1;
    int builtFrame = -10;             // the frame an app was last built (Rebuild): the next one counts at most a 60th of a second (Now)
    CanvasGroup rootGroup;            // the dim, the phone, the ring and the hint: they fade in and out together
    RectTransform tabPill;            // the soft pill behind the open tab's icon
    bool closing;                     // the pause phone dropping away (PutAway); Root goes off at closeAt
    float closeAt;
    bool cascadeDue;                  // the next build's rows arrive one after another (an app opened, the phone came out)
    int cascadeFrame = -1;            // ...and a second build in the same frame does it again
    int slideFrom;                    // the side the next app comes in from (Step: -1 the left, 1 the right; 0: by its order)
    // The pad's ring: where it's drawn (the screen's units), what it's around, and its glide from the last button.
    bool ringOn, ringGliding;
    GameObject ringFor;
    Vector2 ringCentre, ringSize, ringFromCentre, ringFromSize;
    float ringSince;
    // The scroll: a glide to a place (the wheel, the pad's ring), or the keys' and stick's speed easing in and out.
    bool scrollGliding;
    float scrollFrom, scrollTo, scrollSince, scrollVelocity, scrollWritten;
    // The last press (a button's key, and when): its squish and a switch's knob carry on into the app built again after it.
    string pressKey;
    float pressAt = -10f;
    float buzzUntil = -1f;            // a pad press's rumble stops then (real time)
    // Each slider's knob, following its value (SliderRow adds them; a build clears them).
    struct Knob
    {
        public Slider slider;
        public RectTransform handle, fill;
        public KnobDrag drag;         // the mouse dragging it: then the knob stays under the pointer (KnobFollows)
        public float from, to, since, shown;
        public bool gliding;
    }
    readonly List<Knob> knobs = new List<Knob>();

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
        // The whole of it fades in as the phone comes out and out as it drops away (23 Oct); while it drops, clicks pass through.
        rootGroup = Root.AddComponent<CanvasGroup>();

        RectTransform body = Anchored("Phone", screen, new Vector2(.5f, .5f), new Vector2(0f, PhoneY), new Vector2(PhoneWidth, PhoneHeight));
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
        // The ring is gold with a dark edge either side (19 Oct 2026: on the orange restock button the gold alone was
        // nearly invisible, the reviewers found), baked into one sprite (FocusRingTexture), so it draws as one image.
        ringImage.sprite = focusRingSprite;
        ringImage.type = Image.Type.Sliced;
        ringImage.pixelsPerUnitMultiplier = RoundRadius / 15f;
        ringImage.color = Color.white;
        ringImage.raycastTarget = false;
        focusRing.gameObject.SetActive(false);

        RectTransform hint = Anchored("Controls", screen, new Vector2(.5f, 0f), new Vector2(0f, 12f), new Vector2(1600f, 30f));
        hint.pivot = new Vector2(.5f, 0f);
        hintText = hint.gameObject.AddComponent<TextMeshProUGUI>();
        // 22 px, rich text on: the buttons are icons (19 Oct 2026, ControlHints). 26 Oct 2026 (Readability): 24 px, and the skin's
        // shadow under it: it is words over the (dimmed) café, which the gather caught at 1.3:1 where the café was bright.
        Style(hintText, TypeBody, Cream, TextAlignmentOptions.Center, false, UiSkin.SemiBold, true);
        UiSkin.Shadowed(hintText);

        UpdateTabs();
        Root.SetActive(false);
        // Added while it's off, so they wake when it first comes on (23 Oct): RecapUI switches the recap's phone on and off itself
        // (its panel is Root), and the phone hears it here; the wheel over the apps glides.
        Root.AddComponent<RootWatch>().phone = this;
        viewport.gameObject.AddComponent<WheelGlide>().phone = this;
        madeFrame = Time.frameCount;
    }

    void BuildStatusBar(RectTransform display)
    {
        Paint(Box("Notch", display, (DisplayWidth - 128f) / 2f, 12f, 128f, 32f), Bezel, 16f);
        clockText = Words(display, "", 34f, 20f, 120f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Place(clockText.rectTransform, 34f, 16f, 150f, 30f);
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
        // The wheel is the phone's own now (23 Oct: WheelGlide, the same 18 units a notch, as a glide instead of a jump); a drag
        // and its fling stay the scroll rect's.
        scroll.scrollSensitivity = 0f;
    }

    void BuildCloseButton(RectTransform display)
    {
        RectTransform rect = Box("Close up", display, Pad, DisplayHeight - TabsHeight - CloseGap - CloseHeight, ContentWidth, CloseHeight);
        closeRect = rect;
        Image back = Paint(rect, Ink, 16f);
        CloseButton = Pressable(rect, back, null, null, dark: true);
        CloseLabel = Words(rect, Kind == Mode.Recap ? RecapUI.NightLabel : "Back to work", 0f, 0f, ContentWidth, TypeButton, Color.white,
            TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        Place(CloseLabel.rectTransform, 0f, 0f, ContentWidth, CloseHeight);
    }

    void BuildTabs(RectTransform display)
    {
        RectTransform bar = Box("Tabs", display, 0f, DisplayHeight - TabsHeight, DisplayWidth, TabsHeight);
        tabBar = bar;
        Paint(bar, Color.white, 0f);
        Paint(Box("Top line", bar, 0f, 0f, DisplayWidth, 1f), Line, 0f);
        // The open tab's pill (23 Oct): a soft patch behind its icon, under the tabs so the icon draws over it; it slides to the
        // next tab as the app changes (OpenTabMoves). Its pivot is its middle, so its x is the middle of its tab; its middle is the
        // icon's (24 down the bar: the tab's top 1, the icon's 10 in it, half its 26).
        tabPill = Box("Open tab", bar, 0f, 24f - PillHeight / 2f, PillWidth, PillHeight);
        Paint(tabPill, AccentSoft, PillHeight / 2f);
        CentrePivot(tabPill);
        float cell = DisplayWidth / Visible.Length;
        Sprite[] icons = { moon, star, house, bag, note, gear };
        // 26 Oct 2026 (Readability): white on the accent was 3.6:1 (the gather's "2" and "6"); the price's deeper orange is 4.6.
        Color[] badgeColours = { PriceOrange, PriceOrange, Gold, Red, PriceOrange, PriceOrange };
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
            CentrePivot(tabIcons[i].rectTransform);   // it pops about its middle (Place keeps its corner where it was)
            tabLabels[i] = Words(tab, AppNames[i], 0f, 40f, cell, TypeTab, Soft, TextAlignmentOptions.Top, style: FontStyles.Bold, rich: false);

            RectTransform badge = Box("Badge", tab, cell / 2f + 7f, 1f, 26f, 26f);   // 26 Oct 2026 (Readability): 22 to 26, for 20 px digits
            Paint(badge, badgeColours[i], 13f);
            TMP_Text count = Words(badge, "", 0f, 0f, 26f, TypeBadge, Color.white, TextAlignmentOptions.Center, wrap: false,
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

    /// <summary>Opens one of the apps, at its top. While the phone is up, another app slides in from the side it was asked for and
    /// its rows arrive; the app already open glides back to its top (23 Oct). Whatever was moving finishes first.</summary>
    public void Open(App app)
    {
        FinishMotion();   // a press during a motion: the motion ends at once, then the press acts
        App was = Current;
        bool barWas = tabBar != null && tabBar.gameObject.activeSelf;
        float pillWas = tabPill != null ? tabPill.anchoredPosition.x : 0f;
        int side = slideFrom != 0 ? slideFrom : Math.Sign(Order(app) - Order(was));
        slideFrom = 0;
        bool moving = Root != null && Root.activeInHierarchy && !closing;
        float offset = ScrollOffset;
        Current = app;
        quitArmed = false;   // anything else opened takes Quit's question back
        scrollToTop = !moving || app != was;
        UpdateTabs();
        Layout();
        if (moving && app != was) cascadeDue = true;
        RebuildNow();
        if (!moving) return;
        float now = Now();
        if (app == was)
        {
            // Its own tab again: back to its top in a glide (it was built where it stood).
            if (offset > .5f) GlideScrollTo(0f);
            PopTab(app, now);
            return;
        }
        if (side != 0) motions.MoveX(content, side * DisplayWidth * SlideShare, 0f, now, SlideSeconds, UiTween.Ease.OutQuint);
        OpenTabMoves(was, app, barWas, pillWas, now);
    }

    /// <summary>Opens Notes on one person's facts (the closing screen's notebook line).</summary>
    public void OpenPersonIn(string who)
    {
        OpenPerson = who ?? "";
        Open(App.Notes);
    }

    /// <summary>The next app to the right (1) or the left (-1) along the tab bar, wrapping round. It slides in from that side, even
    /// when it wraps (the press says which way, not the order).</summary>
    public void Step(int by)
    {
        App[] shown = Visible;
        int at = Mathf.Max(0, Array.IndexOf(shown, Current));
        slideFrom = by > 0 ? 1 : by < 0 ? -1 : 0;
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
        // It rises out of the pocket (RootWatch hears it come on); taken out again while it was still dropping away, it rises
        // from where it had got to.
        bool wasOn = Root.activeSelf;
        Root.SetActive(true);
        if (wasOn) BeginRise();
    }

    /// <summary>Puts the pause phone away: it drops out of sight (PutAway) while the game already has its input back.</summary>
    public void ClosePause()
    {
        quitArmed = false;
        PutAway();
    }

    /// <summary>
    /// Puts the phone away: it drops and fades over DropSeconds (0.14 s) and then goes off (Root). On the way down clicks pass
    /// through it and it takes no keys or pad (23 Oct); Settled is false until it has gone. Taken out again before then, it rises
    /// from where it got to. (The pause phone's way out; RecapUI still switches the closing recap off at once.)
    /// </summary>
    public void PutAway()
    {
        if (Root == null || !Root.activeSelf || closing) return;
        // Whatever else is moving (the squish of "Back to work", a slide) carries on as it drops: the drop takes over the rise.
        float now = Now();
        closing = true;
        closeAt = now + DropSeconds;
        rootGroup.blocksRaycasts = false;   // the mouse's clicks reach the café again,
        rootGroup.interactable = false;     // and A or the D-pad on a button or slider still selected does nothing on the way down
        motions.MoveY(phoneBody, phoneBody.anchoredPosition.y, PhoneY - DropDistance, now, DropSeconds, UiTween.Ease.InCubic);
        motions.Alpha(rootGroup, rootGroup.alpha, 0f, now, DropSeconds, UiTween.Ease.InQuad);
    }

    /// <summary>Scrolls the current app just far enough to show the button or slider with this key (checks' photos). At once, not
    /// in a glide: a check reads what it scrolled to straight after.</summary>
    public bool RevealKey(string key)
    {
        Selectable target = ContentButton(key);
        if (target == null) target = ContentSlider(key);
        if (target == null) return false;
        FinishMotion();   // measured where the rows come to rest, not where they are on the way
        Canvas.ForceUpdateCanvases();
        Reveal((RectTransform)target.transform, false);
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
        DetailsOpen = !DetailsOpen;   // its button's press has tapped already (Pressed)
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

    /// <summary>A button in the current app by its key: "details", "restock", "upgrade:&lt;asset name&gt;", "arrange" (24 Oct 2026, Furniture), "more", "notebook",
    /// "stock", "person:&lt;who&gt;"; in Settings "invert", "aimhelp:&lt;Light|Strong|Off&gt;", "movehelp", "swapsticks", "morning:&lt;Full|Short|Off&gt;",
    /// "quality:&lt;Low|Medium|High&gt;", "quit". Null if it isn't there.</summary>
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
               $"{(Root.activeInHierarchy ? closing ? "dropping away" : "open" : "closed")} on {LabelOf(Current)}, built {Builds} times; " +
               $"{ReviewCardsShown} review cards when Reviews was last built; badges: Franchise {(FranchiseDot ? "dot" : "none")}, " +
               $"Shop {(ShopAlert ? "!" : "none")}, Notes {NotesNew}; the app is {content.rect.height:0} tall in a {viewport.rect.height:0} window, " +
               $"scrolled {ScrollOffset:0}; {(Settled ? "still" : $"moving ({motions.Count} motions, {MotionLeft:0.00} s left)")}.";
    }

    // ------------------------------------------------------------------ every frame, while it's up

    void Update()
    {
        Now();               // the motion's clock moves on every frame, up or not (23 Oct)
        StopBuzz(false);
        if (Root == null) return;
        if (!Root.activeInHierarchy)
        {
            preview = null;
            // Nothing moves on a phone that's away (a slider set from outside while it was off lands at once), so Settled holds.
            if (motions.Running || KnobsGliding)
            {
                motions.Finish();
                FinishKnobs();
            }
            return;
        }
        if (closing) return;   // dropping away: the game has its input back, and the phone takes none
        HandleInput();
        TrackSelection();
        UpdateHint();
        CountUp();
        LandSplit();
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

    // Tonight's split stays out of sight while the takings count up, then comes up as they land (with "recap.counted"): the
    // day's total first, then what it was made of. Built after the count (another app and back, a Preview), it's simply there.
    // The count itself is untouched: this only reads whether it is still running.
    void LandSplit()
    {
        if (tonightSplit == null) return;
        float want = countSince >= 0f ? 0f : 1f;
        if (Mathf.Approximately(tonightSplit.alpha, want)) return;
        tonightSplit.alpha = Mathf.MoveTowards(tonightSplit.alpha, want, Time.unscaledDeltaTime / SplitFade);
    }

    // After everything that builds or moves the phone this frame, before it's drawn: the app built if it must be, every motion a
    // step on (a motion begun this frame draws its start), then the ring placed on where its button now is.
    void LateUpdate()
    {
        if (Root == null || !Root.activeInHierarchy) return;
        if (dirty) Rebuild();
        float now = Now();
        motions.Step(now);
        StepKnobs(now);
        StepScroll(now);
        if (closing && now >= closeAt)
        {
            EndClose();
            return;
        }
        PlaceFocusRing(now);
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
        // The keys and the stick set a speed the scroll eases toward (StickEase, 0.04 s), so it starts and stops in a glide rather
        // than a step, coasting to rest within 0.2 s of a release (23 Oct); at an end it stops at once. Closed form in the frame's
        // time, so 15 fps and 60 agree.
        float dt = Time.unscaledDeltaTime, want = Mathf.Abs(move) > .001f ? move * ScrollSpeed : 0f;
        scrollVelocity += (want - scrollVelocity) * (1f - Mathf.Exp(-dt / StickEase));
        if (want == 0f && Mathf.Abs(scrollVelocity) < StickRest) scrollVelocity = 0f;
        if (scrollVelocity == 0f) return;
        scrollGliding = false;   // the keys or the stick have the scroll now
        float before = ScrollOffset;
        ScrollBy(scrollVelocity * dt);
        if (Mathf.Abs(ScrollOffset - before) < .01f && Mathf.Abs(scrollVelocity * dt) > .01f) scrollVelocity = 0f;   // at an end
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

    // The pad's ring, every frame: around the selected button, wherever it is now (rising with the phone, sliding in with its app,
    // scrolling into view). A new button: the ring glides there from where it was, resizing on the way (23 Oct; it used to jump),
    // and lands exactly on it. A button in an app is hugged only where it shows in the window (the scroll brings the rest in).
    // One the D-pad has just moved to while none of it shows yet (the scroll or its app's slide is bringing it in): the ring keeps
    // its last place and glides to it once some of it shows, rather than blinking out and back (the review's finding 1). Any other
    // time none of its button shows (a stick scrolled it away), the ring goes; the first time it shows, it is simply there.
    void PlaceFocusRing(float now)
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        RectTransform target = selected != null ? selected.transform as RectTransform : null;
        Vector2 centre = Vector2.zero, size = Vector2.zero;
        bool candidate = !closing && PadInput.UsingPad && target != null && selected.activeInHierarchy && target.IsChildOf(Root.transform);
        bool inView = candidate && RingAround(target, out centre, out size);
        bool waiting = candidate && !inView && ringOn && selected != ringFor
                       && (scrollGliding || selected != lastRevealed || motions.Has(content, UiTween.Kind.MoveX));
        bool show = inView || waiting;
        if (focusRing.gameObject.activeSelf != show) focusRing.gameObject.SetActive(show);
        if (!show)
        {
            ringOn = ringGliding = false;
            ringFor = null;
            return;
        }
        if (waiting)
        {
            ringGliding = false;   // drawn where it last was; its glide starts when some of the button shows
            return;
        }
        if (!ringOn)
        {
            ringOn = true;
            ringGliding = false;
            ringFor = selected;
        }
        else if (selected != ringFor)
        {
            ringFor = selected;
            ringGliding = true;
            ringSince = now;
            ringFromCentre = ringCentre;
            ringFromSize = ringSize;
        }
        float t = ringGliding ? (now - ringSince) / RingSeconds : 1f;
        if (t >= 1f)
        {
            ringGliding = false;
            ringCentre = centre;
            ringSize = size;
        }
        else
        {
            float e = UiTween.Eased(UiTween.Ease.OutQuint, t);
            ringCentre = Vector2.LerpUnclamped(ringFromCentre, centre, e);
            ringSize = Vector2.LerpUnclamped(ringFromSize, size, e);
        }
        focusRing.anchoredPosition = ringCentre;
        focusRing.sizeDelta = ringSize;
    }

    // Where the ring goes around target, in the screen's own units: the button's box (for one in an app, the part of it inside the
    // window) and RingPad more, half either side; as before, so a still ring sits exactly where it always did. False when none of
    // an app's button is in the window.
    bool RingAround(RectTransform target, out Vector2 centre, out Vector2 size)
    {
        var screen = (RectTransform)Root.transform;
        target.GetWorldCorners(corners);
        Vector2 low = screen.InverseTransformPoint(corners[0]), high = screen.InverseTransformPoint(corners[2]);
        if (target.IsChildOf(content))
        {
            viewport.GetWorldCorners(corners);
            low = Vector2.Max(low, screen.InverseTransformPoint(corners[0]));
            high = Vector2.Min(high, screen.InverseTransformPoint(corners[2]));
        }
        centre = (low + high) / 2f;
        size = high - low + new Vector2(RingPad, RingPad);
        return high.x - low.x >= RingLeast && high.y - low.y >= RingLeast;
    }

    void UpdateHint()
    {
        bool pad = PadInput.UsingPad;
        if (hintWritten && pad == hintForPad) return;
        hintWritten = true;
        hintForPad = pad;
        // Icons, not words (19 Oct 2026; her: "Instead of writing out square, just put the icon"): the pad's own buttons, or
        // the keycaps and the mouse; never in brackets (ControlHints, PadInput.Label).
        string apps = $"{PadInput.Label(PadButton.LeftShoulder)} / {PadInput.Label(PadButton.RightShoulder)}  Apps";
        string press = $"{PadInput.Label(PadButton.South)}  Press";
        string dpad = PadInput.Icon("dpad_updown", "D-pad");
        string keyApps = $"{ControlHints.Key("Q")} / {ControlHints.Key("E")}  Apps";
        string keyScroll = $"{ControlHints.Key("W")} / {ControlHints.Key("S")}  Scroll";
        string keyPress = $"{ControlHints.Key("Click")}  Press";
        if (Kind != Mode.Recap)
        {
            hintText.text = pad
                ? $"{PadInput.Label(PadButton.Start)}  Back        {apps}        {dpad}  Move, adjust        {press}"
                : $"{ControlHints.Key("Esc")}  Back        {keyApps}        {ControlHints.Key("1")}-{ControlHints.Key(Visible.Length.ToString())}  Jump        {keyScroll}        {keyPress}";
            return;
        }
        hintText.text = pad
            ? $"{apps}        {PadInput.Icon("rs", "Right stick")}  Scroll        {dpad}  Move        {press}"
            : $"{keyApps}        {ControlHints.Key("1")}-{ControlHints.Key("5")}  Jump        {keyScroll}        {keyPress}";
    }

    void Tap(App app)
    {
        FinishMotion();
        Sfx.Play2D("phone.tap");
        Buzz();   // a pad's press (LB/RB, A on a tab): a light rumble; nothing on the keys or the mouse
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
        scrollWritten = content.anchoredPosition.y;   // where the phone itself last put it (StepScroll: a drag since takes over)
    }

    // Scrolls just far enough to show the whole of target, with a little room around it: in a glide (the pad's ring moving to a
    // button out of sight, 23 Oct), or at once (a check's RevealKey). Measured from where a glide under way is going.
    void Reveal(RectTransform target, bool glide = true)
    {
        target.GetWorldCorners(corners);
        float top = -content.InverseTransformPoint(corners[1]).y, bottom = -content.InverseTransformPoint(corners[0]).y;
        float offset = scrollGliding ? scrollTo : content.anchoredPosition.y, window = viewport.rect.height;
        const float room = 14f;
        float to;
        if (top - room < offset) to = top - room;
        else if (bottom + room > offset + window) to = bottom + room - window;
        else return;
        if (glide) GlideScrollTo(to);
        else
        {
            scrollGliding = false;
            SetScroll(to);
        }
    }

    // A glide to a scroll offset (clamped to the app), from wherever the scroll is now: ScrollGlideSeconds, easing out.
    void GlideScrollTo(float offset)
    {
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        offset = Mathf.Clamp(offset, 0f, range);
        scroll.StopMovement();
        scrollVelocity = 0f;
        if (Mathf.Abs(offset - ScrollOffset) < .5f)
        {
            scrollGliding = false;
            SetScroll(offset);
            return;
        }
        scrollFrom = ScrollOffset;
        scrollTo = offset;
        scrollSince = Now();
        scrollGliding = true;
        scrollWritten = ScrollOffset;
    }

    // One frame of the scroll's glide. A drag (or its fling) moves the app itself: then the glide gives way to it.
    void StepScroll(float now)
    {
        if (!scrollGliding) return;
        if (Mathf.Abs(content.anchoredPosition.y - scrollWritten) > .01f)
        {
            scrollGliding = false;
            return;
        }
        float t = (now - scrollSince) / ScrollGlideSeconds;
        if (t >= 1f) scrollGliding = false;
        SetScroll(t >= 1f ? scrollTo : Mathf.LerpUnclamped(scrollFrom, scrollTo, UiTween.Eased(UiTween.Ease.OutCubic, t)));
    }

    // The wheel or a trackpad over the apps (WheelGlide): as far as the scroll rect used to jump for it (WheelStep a notch, the
    // same reading of the notch), as a glide; notches in a row add up.
    void WheelScroll(Vector2 delta)
    {
        if (closing || content == null || Root == null || !Root.activeInHierarchy) return;
        float d = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : -delta.y;   // the scroll rect's own reading, vertical only
        if (Mathf.Abs(d) < 1e-4f) return;
        GlideScrollTo((scrollGliding ? scrollTo : ScrollOffset) + d * WheelStep);
    }

    // ------------------------------------------------------------------ the motion (23 Oct 2026; see the header and UiTween)

    // The motion's clock: unscaled time (the phone pauses the game) times MotionScale, moved on once a frame by whatever asks first,
    // so anything begun in a frame, wherever in it, starts from that frame's time. The frame after an app is built counts at most a
    // 60th of a second (the review's finding 2): building it (dozens of texts measured) makes the frame it's built in long, and on
    // his Mac, at 15-20 fps, the whole of that time would go on the motion, so the first frame anyone saw would show the rise or the
    // slide nearly over: a pop, not a rise. (The motion runs that much behind real time; the labs' grace allows a frame for it.)
    float Now()
    {
        if (motionFrame != Time.frameCount)
        {
            motionFrame = Time.frameCount;
            float dt = Time.unscaledDeltaTime;
            if (Time.frameCount == builtFrame + 1) dt = Mathf.Min(dt, 1f / 60f);
            motionNow += dt * Mathf.Max(0f, MotionScale);
        }
        return motionNow;
    }

    // A press during a motion: everything that moves is put where it was going, at once, and then the press acts (never a wait).
    // The pause phone dropping away is put away for good.
    void FinishMotion()
    {
        if (closing)
        {
            EndClose();
            return;
        }
        motions.Finish();
        FinishKnobs();
        if (scrollGliding)
        {
            scrollGliding = false;
            SetScroll(scrollTo);
        }
        scrollVelocity = 0f;
        ringGliding = false;   // the ring lands on its button this frame (PlaceFocusRing)
    }

    // RootWatch: Root has come on (RecapUI's recap at closing, OpenPause's pause phone) or gone off (Close up, the drop's end). On
    // in the very frame it was made (RecapUI's Start: a game resumed at closing, from a save or a lab's), it is simply there: it
    // never came out of a pocket, and the labs that photograph it as they open on a recap see it whole.
    void RootShown()
    {
        if (Time.frameCount == madeFrame) return;
        BeginRise();
    }

    void RootHidden()
    {
        closing = false;
        cascadeDue = false;   // rows arrive only on a phone that's up (BeginRise asks again when it next comes out)
        motions.Finish();
        FinishKnobs();
        scrollGliding = false;
        scrollVelocity = 0f;
        ringOn = ringGliding = false;
        ringFor = null;
        if (phoneBody != null) phoneBody.anchoredPosition = new Vector2(phoneBody.anchoredPosition.x, PhoneY);
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = rootGroup.interactable = true;
        }
        // A press's rumble plays out its 0.05 s (Close up's A is felt as the recap goes): Update stops it on time, phone up or not.
    }

    // Out of the pocket: the phone rises from below the screen's foot with a slight overshoot and settles, everything on it fading
    // in, and the first build's rows arrive. Taken out again while it was still dropping away, it rises from where it got to.
    void BeginRise()
    {
        if (phoneBody == null || rootGroup == null) return;
        float now = Now();
        bool fromDrop = closing;
        float y = fromDrop ? phoneBody.anchoredPosition.y : PhoneY - RiseDistance, alpha = fromDrop ? rootGroup.alpha : 0f;
        closing = false;
        motions.Finish();
        FinishKnobs();
        scrollGliding = false;
        scrollVelocity = 0f;
        ringOn = ringGliding = false;
        rootGroup.blocksRaycasts = rootGroup.interactable = true;   // a press during the rise acts (it finishes the rise first)
        motions.MoveY(phoneBody, y, PhoneY, now, RiseSeconds, UiTween.Ease.OutBack);
        motions.Alpha(rootGroup, alpha, 1f, now, RiseFade, UiTween.Ease.OutCubic);
        cascadeDue = true;
    }

    // The drop has ended: Root goes off (RootHidden puts everything back in place for the next time it comes out).
    void EndClose()
    {
        closing = false;
        if (Root != null && Root.activeSelf) Root.SetActive(false);
        else RootHidden();
    }

    // Where an app sits along the tab bar now (-1: not on it, as Tonight isn't by day); a loop, not Array.IndexOf on an enum.
    int Order(App app)
    {
        App[] shown = Visible;
        for (int i = 0; i < shown.Length; i++) if (shown[i] == app) return i;
        return -1;
    }

    // The middle of the tab at this place along the bar (PlaceTabs' cells).
    float TabMiddle(int at) => DisplayWidth / Visible.Length * (at + .5f);

    // The tab bar as the app changes: the pill slides from the old tab to the new one (settling with a touch of overshoot), the
    // two tabs' colours cross over, and the new tab's icon pops. On a bar that has just come up (from Tonight) the pill is simply
    // there; the pop still plays.
    void OpenTabMoves(App was, App now, bool barWas, float pillWas, float t0)
    {
        if (tabBar == null || !tabBar.gameObject.activeSelf || Order(now) < 0) return;
        PopTab(now, t0);
        if (!barWas || Order(was) < 0) return;
        motions.MoveX(tabPill, pillWas, tabPill.anchoredPosition.x, t0, PillSeconds, UiTween.Ease.OutBack);
        int a = (int)was, b = (int)now;
        motions.Colour(tabIcons[a], Accent, TabOff, t0, TabColourSeconds, UiTween.Ease.OutCubic);
        motions.Colour(tabLabels[a], AccentText, TabOff, t0, TabColourSeconds, UiTween.Ease.OutCubic);
        motions.Colour(tabIcons[b], TabOff, Accent, t0, TabColourSeconds, UiTween.Ease.OutCubic);
        motions.Colour(tabLabels[b], TabOff, AccentText, t0, TabColourSeconds, UiTween.Ease.OutCubic);
    }

    // A tab's icon pops (1 to PopScale and back, about its middle).
    void PopTab(App app, float t0)
    {
        if (tabBar == null || !tabBar.gameObject.activeSelf || Order(app) < 0) return;
        motions.Scale(tabIcons[(int)app].rectTransform, 1f, PopScale, t0, PopSeconds, UiTween.Ease.Pop);
    }

    // Rows arriving: an app just opened (or the phone just out) has its rows rise into place one after another. A row is what
    // sits side by side in the app (a card and its shadow, a heading's icon and its words): the app's own pieces, in the order they
    // were built, top to bottom. Each rises RowRise units and fades in over RowSeconds, RowStagger after the row above, the last
    // starting by RowLastStart, so all of it is over within a quarter of a second however much the app holds. Only the rows in
    // the window move; the rest are out of sight and stay put. A piece fades with a canvas group of its own, which never stops a
    // press (a press finishes it all first anyway).
    void RowsArrive()
    {
        float windowTop = ScrollOffset, windowBottom = windowTop + viewport.rect.height, now = Now();
        int count = content.childCount, rows = 0;
        // Twice over the pieces: first to count the rows in the window (the beat between them shrinks if there are many), then to
        // set each one going.
        for (int pass = 0; pass < 2; pass++)
        {
            float stagger = rows > 1 ? Mathf.Min(RowStagger, RowLastStart / (rows - 1)) : 0f;
            int row = -1;
            float bottom = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var piece = content.GetChild(i) as RectTransform;
                if (piece == null || !piece.gameObject.activeSelf) continue;   // the last build's, on its way out
                float top = TopOf(piece), height = piece.rect.height;
                if (top >= windowBottom) break;                                 // below the window, and so is the rest
                if (top + height <= windowTop) continue;                        // above it (scrolled past)
                if (top >= bottom - 1f) row++;
                bottom = Mathf.Max(bottom, top + height);
                if (pass == 0) continue;
                float start = now + row * stagger, y = piece.anchoredPosition.y;
                motions.MoveY(piece, y - RowRise, y, start, RowSeconds, UiTween.Ease.OutCubic);
                if (!piece.TryGetComponent(out CanvasGroup group)) group = piece.gameObject.AddComponent<CanvasGroup>();
                motions.Alpha(group, 0f, 1f, start, RowSeconds, UiTween.Ease.OutQuad);
            }
            if (pass == 0) rows = row + 1;
            if (rows == 0) return;
        }
    }

    // How far down the app a piece's top is (its pivot may be its middle, once pressed).
    static float TopOf(RectTransform piece) => -piece.anchoredPosition.y - (1f - piece.pivot.y) * piece.rect.height;

    // Every button on the phone, pressed (the first of its listeners): what was moving finishes at once, then the button squishes and
    // springs back with the tap (Close up has its own sounds: RecapUI's and PausePhone's) and, on a pad, a light rumble. What the
    // button does comes next; if that builds the app again, the squish carries on on its new self (SquishAgain).
    void Pressed(Button button)
    {
        FinishMotion();
        float now = Now();
        pressKey = buttonKeys.TryGetValue(button, out string key) ? key : null;
        pressAt = now;
        Squish((RectTransform)button.transform, now);
        if (button != CloseButton) Sfx.Play2D("phone.tap");
        Buzz();
    }

    // The squish: down to 0.94 and back up past 1.03, about the button's middle (UiTween.Ease.Squish). A card that is a button (a
    // person, the notebook line) takes its shadow with it (FinishCard puts the shadow just before it), so no grey edge shows.
    void Squish(RectTransform box, float start)
    {
        if (box == null) return;
        CentrePivot(box);
        motions.Scale(box, 1f, UiTween.SquishLow, start, SquishSeconds, UiTween.Ease.Squish);
        int at = box.parent == content ? box.GetSiblingIndex() : 0;
        if (at <= 0) return;
        var shadow = content.GetChild(at - 1) as RectTransform;
        if (shadow == null || shadow.name != "Shadow") return;
        CentrePivot(shadow);
        motions.Scale(shadow, 1f, UiTween.SquishLow, start, SquishSeconds, UiTween.Ease.Squish);
    }

    // The app built again just after a press (a switch flipped, a person opened, a purchase): the same key's new button carries on
    // its squish from where it was. (Not on a row that's arriving: the press finished that, and a new app's keys aren't the old's.)
    void SquishAgain()
    {
        if (pressKey == null || Now() - pressAt >= SquishSeconds) return;
        Button again = ContentButton(pressKey);
        if (again == null) return;
        var box = (RectTransform)again.transform;
        if (motions.Has(box, UiTween.Kind.Scale) || motions.Has(box, UiTween.Kind.MoveY)) return;
        Squish(box, pressAt);
    }

    // A click on a button that can't be pressed now (Nudge): a tiny shake, side to side, and no sound (nothing in the bank says "no"
    // softly yet). Whatever was moving finishes first, as for any press.
    void Refuse(RectTransform box)
    {
        if (closing || box == null) return;
        FinishMotion();
        float x = box.anchoredPosition.x;
        motions.MoveX(box, x, x + ShakeWidth, Now(), ShakeSeconds, UiTween.Ease.Shake);
    }

    // A pad's press: the bench's lightest pulse, BuzzSeconds long, only with Settings' Vibration on and the pad in hand (the keys
    // and the mouse feel nothing). Stopped by StopBuzz on real time, not the motion's clock (a lab's slow motion doesn't stretch it).
    void Buzz()
    {
        if (!PadInput.UsingPad || !GameSettings.Vibration || !Application.isFocused) return;
        PadInput.Rumble(BuzzLow, BuzzHigh);
        buzzUntil = Time.unscaledTime + BuzzSeconds;
    }

    // The rumble off: once its time is up (every frame), or at once (the phone gone).
    void StopBuzz(bool atOnce)
    {
        if (buzzUntil < 0f || (!atOnce && Time.unscaledTime < buzzUntil)) return;
        buzzUntil = -1f;
        PadInput.StopRumble();
    }

    // A slider's value has changed: after a D-pad step or a click along it, its knob glides there from where it's drawn; while the
    // mouse drags it (KnobDrag), it is simply there.
    void KnobFollows(Slider slider)
    {
        for (int i = 0; i < knobs.Count; i++)
        {
            Knob k = knobs[i];
            if (k.slider != slider) continue;
            if (k.drag != null && k.drag.dragging)
            {
                // Dragged by the mouse: the knob stays under the pointer (the slider has put it at its value; a glide would trail
                // the hand by a tenth of a second).
                k.shown = k.to = slider.normalizedValue;
                k.gliding = false;
                if (k.handle != null && k.fill != null) DrawKnob(in k);
            }
            else
            {
                k.from = k.shown;
                k.to = slider.normalizedValue;
                k.since = Now();
                k.gliding = true;
            }
            knobs[i] = k;
            return;
        }
    }

    // One frame of the sliders' knobs: each drawn where its glide has got to (the slider put it at its value; this goes over that,
    // the handle and the fill together, until the glide ends at the value itself).
    void StepKnobs(float now)
    {
        for (int i = 0; i < knobs.Count; i++)
        {
            Knob k = knobs[i];
            if (!k.gliding) continue;
            if (k.slider == null || k.handle == null || k.fill == null) { k.gliding = false; knobs[i] = k; continue; }
            float t = (now - k.since) / SliderSeconds;
            if (t >= 1f) k.gliding = false;
            k.shown = t >= 1f ? k.to : Mathf.LerpUnclamped(k.from, k.to, UiTween.Eased(UiTween.Ease.OutCubic, t));
            DrawKnob(in k);
            knobs[i] = k;
        }
    }

    void FinishKnobs()
    {
        for (int i = 0; i < knobs.Count; i++)
        {
            Knob k = knobs[i];
            if (!k.gliding) continue;
            k.gliding = false;
            k.shown = k.to;
            if (k.slider != null && k.handle != null && k.fill != null) DrawKnob(in k);
            knobs[i] = k;
        }
    }

    bool KnobsGliding
    {
        get
        {
            for (int i = 0; i < knobs.Count; i++) if (knobs[i].gliding) return true;
            return false;
        }
    }

    // The knob and the fill at a share of the way along (as Unity's slider places them: the knob's anchors at the share, the fill's
    // right edge there).
    static void DrawKnob(in Knob k)
    {
        k.handle.anchorMin = new Vector2(k.shown, 0f);
        k.handle.anchorMax = new Vector2(k.shown, 1f);
        k.fill.anchorMax = new Vector2(k.shown, 1f);
    }

    // The phone's own little helpers, made in code with it (nothing in the scene changes). Unity calls them; they hand over.

    // On the screen (Root): RecapUI switches the recap's phone on and off itself (its panel is Root), so the phone hears it here.
    sealed class RootWatch : MonoBehaviour
    {
        internal RecapPhone phone;
        void OnEnable() { if (phone != null) phone.RootShown(); }
        void OnDisable() { if (phone != null) phone.RootHidden(); }
    }

    // On the apps' window: the wheel (or two fingers on a trackpad) scrolls in a glide. The scroll rect still has the drag.
    sealed class WheelGlide : MonoBehaviour, IScrollHandler
    {
        internal RecapPhone phone;
        public void OnScroll(PointerEventData data) { if (phone != null) phone.WheelScroll(data.scrollDelta); }
    }

    // On a button that can't be pressed now (a price out of reach): Unity sends it no press, but this hears the click.
    sealed class Nudge : MonoBehaviour, IPointerClickHandler
    {
        internal RecapPhone phone;
        public void OnPointerClick(PointerEventData data)
        {
            if (phone == null || data.button != PointerEventData.InputButton.Left) return;
            if (TryGetComponent(out Button button) && !button.IsInteractable()) phone.Refuse((RectTransform)transform);
        }
    }

    // On a slider: the mouse dragging it (Unity sends the slider's drag to everything on it that listens), so its knob stays under
    // the pointer while it's dragged, and glides only after a D-pad step or a click along it (KnobFollows).
    sealed class KnobDrag : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        internal bool dragging;
        public void OnBeginDrag(PointerEventData data) => dragging = true;
        public void OnEndDrag(PointerEventData data) => dragging = false;
        void OnDisable() => dragging = false;   // put away or built again mid-drag: no end comes
    }

    // ------------------------------------------------------------------ building the current app

    ReputationLedger Ledger => preview ?? (SaveManager.Instance != null ? SaveManager.Instance.Reputation : null);
    static Notebook Book => SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
    static int Today => DayClock.Instance != null ? DayClock.Instance.Day : 0;

    void Rebuild()
    {
        dirty = false;
        Builds++;
        builtFrame = Time.frameCount;   // this frame is long: the motion's clock won't count it all (Now)
        // A pad player keeps their place across a rebuild (after buying, say): the same button, as far down.
        string keep = SelectedKey();
        bool top = scrollToTop;
        float offset = top ? 0f : content.anchoredPosition.y;
        scrollToTop = false;
        scroll.StopMovement();
        if (top)
        {
            scrollGliding = false;        // a new app starts still, at its top (a glide under way carries on through a rebuild)
            scrollVelocity = 0f;
        }
        knobs.Clear();                    // the sliders are new (SliderRow adds them again)

        contentButtons.Clear();
        buttonKeys.Clear();
        navOrder.Clear();
        navRows.Clear();
        sliderKeys.Clear();
        navRow = 0;
        sameRow = false;
        tonightSplit = null;   // Tonight makes it again; another app has none
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

        // The motion (23 Oct): an app just opened, or the phone just out, has its rows arrive (a second build in the same frame
        // too); a press's squish carries on, on the same button in the app built again.
        if (cascadeDue || cascadeFrame == Time.frameCount)
        {
            cascadeDue = false;
            cascadeFrame = Time.frameCount;
            RowsArrive();
        }
        SquishAgain();
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

    // The open tab in the accent, the others grey, and the pill behind the open tab's icon (none on Tonight's, which has no
    // bar). At once: an app switch's motion runs over these (OpenTabMoves) and draws its own values until it ends.
    void UpdateTabs()
    {
        for (int i = 0; i < Apps; i++)
        {
            Color colour = i == (int)Current ? Accent : TabOff;
            if (tabIcons[i] != null) tabIcons[i].color = colour;
            // 26 Oct 2026 (Readability): the open tab's name in the accent's ink (the accent itself was 3.6:1 on white).
            if (tabLabels[i] != null) tabLabels[i].color = i == (int)Current ? AccentText : TabOff;
        }
        if (tabPill == null) return;
        int at = Order(Current);
        if (tabPill.gameObject.activeSelf != at >= 0) tabPill.gameObject.SetActive(at >= 0);
        if (at >= 0) tabPill.anchoredPosition = new Vector2(TabMiddle(at), tabPill.anchoredPosition.y);
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
        // 24 Oct 2026 (Furniture): on Day 1's evening the Shop's "!" also says a first table waits to be placed (YOUR CAFÉ).
        ShopFirstTable = FirstTableWaiting();
        Badge(App.Shop, ShopAlert || ShopFirstTable, "!");
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
        float size = dot ? 12f : 26f;   // 26 Oct 2026 (Readability): 20 to 26, the digits 14 to 20 px
        ((RectTransform)badges[i].transform).sizeDelta = new Vector2(dot ? size : Mathf.Max(size, 14f + 10.5f * text.Length), size);
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
        TMP_Text kicker = Counted(Words(card, clock != null ? Kicker(clock.Day) : "CLOSED", CardPadX, 14f, ContentWidth - 2f * CardPadX, TypeLabel, DarkFaint,
            style: FontStyles.Bold, rich: false, spacing: 4f));
        float top = Bottom(kicker.rectTransform) + 6f;

        // The takings, large, counting up into their own space from the left (24 Oct 2026: the thrift find's sale counted in).
        int finds = FindsTonight(clock);
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned + finds : 0), 0f, 0f, 0f, 40f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        Vector2 moneySize = money.rectTransform.sizeDelta;
        money.horizontalAlignment = HorizontalAlignmentOptions.Left;
        todayMoney = money;
        todayEarned = clock != null ? clock.Earned + finds : 0;
        if (countDue)
        {
            countDue = false;
            countSince = Time.unscaledTime + .25f;
            todayShown = 0;
        }
        if (countSince >= 0f) money.text = "$" + todayShown;
        Place(money.rectTransform, CardPadX, top, Mathf.Max(moneySize.x, 150f), moneySize.y);
        TonightWords += 1;
        TMP_Text earned = Counted(Words(card, "earned today", CardPadX, Bottom(money.rectTransform) - 2f, 260f, TypeBody, DarkFaint, rich: false));

        // The café's stars, top right, and under them a new star or how many.
        const float starSize = 20f, starGap = 3f;
        float starsWidth = 5f * starSize + 4f * starGap;
        int stars = rep != null ? Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        Stars(card, right - starsWidth, top + 8f, starSize, starGap, stars, dark: true);
        float underStars = top + 8f + starSize + 7f;
        if (rep != null && rep.EarnedStarToday)
        {
            RectTransform pill = Tag(card, "New star!", 0f, 0f, GoldSoft, GoldInk, TypeLabel);
            Place(pill, right - pill.sizeDelta.x, underStars, pill.sizeDelta.x, pill.sizeDelta.y);
            TonightWords += 2;
            underStars += pill.sizeDelta.y;
        }
        else
        {
            // Before the first star, how much reputation it still needs (playtest 4: "no stars yet" said nothing about it).
            TMP_Text count = Counted(Words(card, StarsLine(stars, rep != null ? rep.Reputation : 0), 0f, 0f, 0f, TypeBody, DarkFaint,
                wrap: false, rich: false));
            Place(count.rectTransform, right - count.rectTransform.sizeDelta.x, underStars, count.rectTransform.sizeDelta.x, count.rectTransform.sizeDelta.y);
            underStars = Bottom(count.rectTransform);
        }

        // What Ace's own work earned apart from the café's guests (12 Oct, his pick of the findings' §10.7: "$X from your work
        // · $Y from café guests" on Tonight too; the first hour's Day 1 read "$65 earned today" with nobody served, all of it
        // the guests'). Under "earned today", small, in the card's quiet colour: the same line, size and colour as on the
        // Reviews and Today cards. Nothing on an evening the guests paid nothing. Out of sight while the takings count up, it
        // lands with them (LandSplit). Its words count against the budget (8, always: two figures, six words).
        float cy = Mathf.Max(Bottom(earned.rectTransform), underStars);
        TonightSplit = clock != null ? TakingsSplit(clock.Earned, clock.PatronIncome, finds) : "";
        TonightSplitWords = 0;
        if (TonightSplit.Length > 0)
        {
            int before = TonightWords;
            TMP_Text split = Counted(Words(card, TonightSplit, CardPadX, cy + 6f, ContentWidth - 2f * CardPadX, TypeBody, DarkFaint, rich: false));
            TonightSplitWords = TonightWords - before;
            split.name = "Takings split";
            split.alpha = countSince >= 0f ? 0f : 1f;
            tonightSplit = split;
            cy = Bottom(split.rectTransform);
        }

        // 24 Oct 2026 (Days): the thrift find that sold tonight, and what tomorrow brings (the next day's previewLine).
        cy = ThriftLines(card, clock, cy);

        // The reviews, and what they did to the reputation.
        cy += 12f;
        Paint(Box("Rule", card, CardPadX, cy, ContentWidth - 2f * CardPadX, 1f), DarkLine, 0f);
        cy += 10f;
        int n = rep != null ? rep.ReviewCount : 0;
        string reviews = n == 0 ? "No reviews today." : $"{n} review{(n == 1 ? "" : "s")} today  ·  {ReputationRecap.Signed(rep.TodayChange)} reputation";
        cy = Bottom(Counted(Words(card, reviews, CardPadX, cy, ContentWidth - 2f * CardPadX, TypeBody, DarkText, rich: false)).rectTransform);

        string error = SaveManager.Instance != null ? SaveManager.Instance.LastSaveError : "";
        if (!string.IsNullOrEmpty(error))
            cy = Bottom(Words(card, "<b>Save failed:</b> <noparse>" + error + "</noparse>", CardPadX, cy + 9f, ContentWidth - 2f * CardPadX, TypeBody, SaveFailed).rectTransform);
        card.sizeDelta = new Vector2(ContentWidth, cy + 14f);
        return y + card.sizeDelta.y + CardGap;
    }

    // One review worth reading: the avatar, the name and its verdict on one row, the line under them.
    float ReviewLine(float y, ReviewCard review)
    {
        RectTransform card = CardBox("Review line", y);
        Avatar(card, CardPadX, 12f, 32f, review.name);
        float x = CardPadX + 44f, width = ContentWidth - x - CardPadX;
        TMP_Text name = Words(card, review.name, x, 10f, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        float nameWidth = name.rectTransform.sizeDelta.x;
        if (nameWidth > width - VerdictRoom - 10f)
        {
            Object.Destroy(name.gameObject);
            name = Words(card, review.name, x, 10f, width - VerdictRoom - 10f, TypeBody, Ink, style: FontStyles.Bold, rich: false);
            nameWidth = name.rectTransform.sizeDelta.x;
        }
        Verdict(card, review.review, x + nameWidth + 8f, 10f + name.rectTransform.sizeDelta.y / 2f, TypeLabel);
        TMP_Text line = Words(card, review.line, x, Bottom(name.rectTransform) + 2f, width, TypeBody, Soft, rich: false);
        return FinishCard(card, Mathf.Max(12f + 32f, Bottom(line.rectTransform)) + 11f, y);
    }

    // The one line of notebook: today's newest fact (his pages first), under whose it is, like a review line under its
    // name (the third playtest: a fact without a name was a riddle). Everything on Tonight is today's, so no NEW tag.
    // Tap: Notes, open on that person.
    float NotebookLine(float y, NotebookFactData fact)
    {
        RectTransform card = CardBox("Notebook line", y);
        // What the fact is about, as a picture (23 Oct 2026, NotePhotos), where the purple note square was.
        NotePhotos.Thumb(card, NoteSubjects.KeyOf(fact), CardPadX, 11f, 32f);
        float x = CardPadX + 44f, width = ContentWidth - x - CardPadX;
        bool his = fact.source == Notebook.Sources.Inherited;
        string whose = string.IsNullOrWhiteSpace(fact.name) ? fact.who ?? "" : fact.name;
        TMP_Text name = Counted(Words(card, whose, x, 10f, width, TypeBody, Ink, style: FontStyles.Bold, rich: false));
        TMP_Text words = Counted(Words(card, NotebookRecap.Sentence(fact.text), x, Bottom(name.rectTransform) + 2f, width, TypeBody, Soft,
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
        TMP_Text words = Counted(Words(box, $"Low on {what}. Restock in Shop.", 12f, 9f, ContentWidth - 24f, TypeBody, AccentText, rich: false));
        box.sizeDelta = new Vector2(ContentWidth, Bottom(words.rectTransform) + 9f);
        Pressable(box, face, "stock", () => Open(App.Shop), dark: false);   // its press taps and rumbles (Pressed), so not Tap
        return y + box.sizeDelta.y + CardGap;
    }

    // The way to the apps, and what's waiting there (the low stock has its own line above, so it isn't said twice).
    const float MoreHeight = 50f;   // 26 Oct 2026 (Readability): 44 to 50, for its 24 px words

    float MoreRow(float y)
    {
        string label = "More";
        if (NotesNew > 0) label += $"  ·  {NotesNew} new note{(NotesNew == 1 ? "" : "s")}";
        label += "  ›";
        RectTransform rect = Box("Button: more", content, Pad, y + 2f, ContentWidth, MoreHeight);
        Image face = Paint(rect, ButtonOff, 16f);
        TMP_Text words = Counted(Words(rect, label, 0f, 0f, ContentWidth, TypeBody, Soft, TextAlignmentOptions.Center, wrap: false,
            style: FontStyles.Bold, rich: false));
        Place(words.rectTransform, 0f, 0f, ContentWidth, MoreHeight);
        Pressable(rect, face, "more", () => Open(App.Reviews), dark: false);   // its press taps and rumbles (Pressed), so not Tap
        return y + 2f + MoreHeight + CardGap;
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
        TMP_Text kicker = Words(card, clock != null ? Kicker(clock.Day) : "CLOSED", CardPadX, 13f, ContentWidth - 2f * CardPadX, TypeLabel, DarkFaint,
            style: FontStyles.Bold, rich: false, spacing: 4f);
        TMP_Text earned = Words(card, "Earned today", CardPadX, Bottom(kicker.rectTransform) + 1f, 260f, TypeBody, DarkText,
            style: FontStyles.Bold, rich: false);
        int finds = FindsTonight(clock);   // 24 Oct 2026 (Days): the thrift find's sale at closing
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned + finds : 0), 0f, 0f, 0f, 24f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        Vector2 moneySize = money.rectTransform.sizeDelta;
        // Sized for the whole figure; it counts up into that space from the right.
        money.horizontalAlignment = HorizontalAlignmentOptions.Right;
        todayMoney = money;
        todayEarned = clock != null ? clock.Earned + finds : 0;
        if (countDue)
        {
            countDue = false;
            countSince = Time.unscaledTime + .25f;
            todayShown = 0;
        }
        if (countSince >= 0f) money.text = "$" + todayShown;
        Place(money.rectTransform, right - moneySize.x, 10f, moneySize.x, moneySize.y);
        Button details = Pill(card, DetailsOpen ? "Hide details" : "Details", right, Bottom(money.rectTransform) + 3f, TypeLabel,
            DarkLine, DarkText, "details", ToggleDetails, true);
        float bottom = Mathf.Max(Bottom(earned.rectTransform), Bottom((RectTransform)details.transform));
        // What the player's own work earned, apart from the café's guests (they pay at the tables without a pop now).
        string split = clock != null ? TakingsSplit(clock.Earned, clock.PatronIncome, finds) : "";
        if (split.Length > 0)
            bottom = Bottom(Words(card, split, CardPadX, bottom + 6f, ContentWidth - 2f * CardPadX, TypeBody, DarkFaint, rich: false).rectTransform);
        if (DetailsOpen && clock != null) bottom = DetailsList(card, clock, bottom + 11f);
        string error = SaveManager.Instance != null ? SaveManager.Instance.LastSaveError : "";
        if (!string.IsNullOrEmpty(error))
            bottom = Bottom(Words(card, "<b>Save failed:</b> <noparse>" + error + "</noparse>", CardPadX, bottom + 9f,
                ContentWidth - 2f * CardPadX, TypeBody, SaveFailed).rectTransform);
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
        Row("Café guests", "$" + c.PatronIncome);
        Row("Repairs completed", c.Repairs.ToString());
        Row("      Perfect", c.Perfect.ToString());
        Row("      Good", c.Good.ToString());
        Row("      Passable", c.Passable.ToString());
        Row("Tips", "$" + c.Tips);
        Row("Earned today", "$" + c.Earned);
        Row("Closing till", "$" + c.CaptureRecap().closingTill);
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text left = Words(card, labels.ToString(), CardPadX, y, width, TypeBody, DarkFaint, rich: false);
        TMP_Text right = Words(card, values.ToString(), CardPadX, y, width, TypeBody, DarkText, TextAlignmentOptions.TopRight, rich: false);
        return Mathf.Max(Bottom(left.rectTransform), Bottom(right.rectTransform));
    }

    // How many reviews, the reputation they made, and a bar for each verdict, best first, in its colour.
    float SummaryCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Summary", y);
        int total = rep != null ? rep.ReviewCount : 0;
        TMP_Text heading = Words(card, total == 0 ? "Today: no new reviews" : $"Today: {total} new review{(total == 1 ? "" : "s")}",
            CardPadX, 13f, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (total > 0)
            Tag(card, ReputationRecap.Signed(rep.TodayChange) + " reputation", CardPadX + heading.rectTransform.sizeDelta.x + 8f,
                13f + heading.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float cy = Bottom(heading.rectTransform) + 8f;
        const float labelWidth = 150f;   // "Never again" at 24 px (26 Oct 2026, Readability: was 110 at 18)
        float trackX = CardPadX + labelWidth + 6f, trackWidth = ContentWidth - 2f * CardPadX - labelWidth - 6f - 36f;
        foreach (Review verdict in VerdictOrder)
        {
            int count = rep != null ? rep.Count(verdict) : 0;
            VerdictColours(verdict, out _, out Color strong);
            TMP_Text label = Words(card, VerdictWord(verdict), CardPadX, cy, labelWidth, TypeBody, Soft, wrap: false, rich: false);
            float row = label.rectTransform.sizeDelta.y, barTop = cy + row / 2f - 5f;
            Paint(Box("Bar", card, trackX, barTop, trackWidth, 10f), Track, 5f);
            if (count > 0 && total > 0)
                Paint(Box("Share", card, trackX, barTop, Mathf.Max(10f, trackWidth * count / total), 10f), strong, 5f);
            Words(card, count.ToString(), trackX + trackWidth, cy, 36f, TypeBody, Soft, TextAlignmentOptions.TopRight, rich: false);
            cy += row + 3f;
        }
        return FinishCard(card, cy + 10f, y);
    }

    // One review: an avatar with the initial, the name (and "regular"), its verdict, "today", the line.
    float ReviewCardView(float y, ReviewCard review)
    {
        RectTransform card = CardBox("Review", y);
        Avatar(card, CardPadX, 14f, 40f, review.name);
        float x = CardPadX + 52f, width = ContentWidth - x - CardPadX;
        TMP_Text name = Words(card, review.name, x, 12f, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (review.regular)
            Tag(card, "regular", x + name.rectTransform.sizeDelta.x + 8f, 12f + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float verdictTop = Bottom(name.rectTransform) + 3f;
        RectTransform pill = Verdict(card, review.review, x, verdictTop + 16f, TypeLabel);
        TMP_Text when = Words(card, "today", 0f, 0f, 0f, TypeBody, Soft, wrap: false, rich: false);
        Vector2 whenSize = when.rectTransform.sizeDelta;
        Place(when.rectTransform, x + pill.sizeDelta.x + 8f, verdictTop + 16f - whenSize.y / 2f, whenSize.x, whenSize.y);
        TMP_Text line = Words(card, review.line, x, verdictTop + 32f + 6f, width, TypeBody, Ink, rich: false);
        return FinishCard(card, Mathf.Max(14f + 40f, Bottom(line.rectTransform)) + 14f, y);
    }

    // ------------------------------------------------------------------ the verdicts (playtest 4)

    /// <summary>The verdicts, best first: the reputation spec's order (§3: Loved it +2, Liked it +1, Fine 0, Let down -1,
    /// Never again -2).</summary>
    public static readonly Review[] VerdictOrder = { Review.LovedIt, Review.LikedIt, Review.Fine, Review.LetDown, Review.NeverAgain };

    /// <summary>A review's verdict as the phone says it: the game's own words (ReputationRules.Label).</summary>
    public static string VerdictWord(Review review) => ReputationRules.Label(review);

    // A verdict's pill: wide enough for the widest word ("Never again") at the line's size.
    const float VerdictRoom = 150f;   // "Never again" as a 22 px pill (26 Oct 2026, Readability: was 112 at 16 px)

    // Each verdict in its own colour (the reputation spec §12: "Verdicts are words for now, each in its own colour"): its
    // soft colour behind, its strong colour for the word and for its bar.
    static void VerdictColours(Review review, out Color back, out Color ink)
    {
        switch (review)
        {
            case Review.LovedIt: back = GreenSoft; ink = Green; break;
            case Review.LikedIt: back = Hex(0xe7f1d6); ink = LikedInk; break;
            case Review.LetDown: back = AccentSoft; ink = Accent; break;
            case Review.NeverAgain: back = UiSkin.RedSoft; ink = Red; break;
            default: back = Track; ink = Soft; break;   // Fine, and no review
        }
    }

    // A verdict, in words and its colour, as a pill whose left edge is at x, centred on centreY.
    RectTransform Verdict(RectTransform card, Review review, float x, float centreY, float size)
    {
        VerdictColours(review, out Color back, out Color ink);
        RectTransform pill = Tag(card, VerdictWord(review), x, centreY, back, ink, size);
        pill.name = "Verdict";
        return pill;
    }

    /// <summary>
    /// Under the café's stars: before the first star, the reputation it still needs ("3 to your first star"; playtest 4: "no
    /// stars yet" said nothing about what earns one); after it, how many ("1 star", "2 stars").
    /// </summary>
    public static string StarsLine(int stars, int reputation)
    {
        stars = Mathf.Clamp(stars, 0, ReputationRules.MaxStars);
        if (stars == 0) return $"{Mathf.Max(1, ReputationRules.NextThreshold(0) - Mathf.Max(0, reputation))} to your first star";
        return stars == 1 ? "1 star" : stars + " stars";
    }

    /// <summary>The same by day (the phone's Today), with today's reviews counted in as they will be at closing: "a new star
    /// at closing" once they reach the next one.</summary>
    public static string LiveStarsLine(ReputationLedger rep)
    {
        if (rep == null) return StarsLine(0, 0);
        int stars = Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars);
        int projected = Mathf.Max(0, rep.Settled ? rep.Reputation : rep.Reputation + rep.TodayChange);
        int next = ReputationRules.NextThreshold(stars);
        if (next >= 0 && !rep.Settled && projected >= next) return "a new star at closing";
        return StarsLine(stars, projected);
    }

    /// <summary>"$132 from your work · $10 from café guests" when the café's guests paid today; "" when they didn't.</summary>
    public static string TakingsSplit(int earned, int patronIncome) => TakingsSplit(earned, patronIncome, 0);

    /// <summary>
    /// 24 Oct 2026 (Days): with the thrift find's sale at closing, "$132 from your work · $10 from café guests · $55 from your
    /// finds" (the guests' part only when they paid); "" when neither the guests nor a find added anything.
    /// <paramref name="earned"/> is DayClock.Earned (the work and the guests), the finds apart from it.
    /// </summary>
    public static string TakingsSplit(int earned, int patronIncome, int finds)
    {
        if (patronIncome <= 0 && finds <= 0) return "";
        string split = $"${Mathf.Max(0, earned - Mathf.Max(0, patronIncome))} from your work";
        if (patronIncome > 0) split += $" · ${patronIncome} from café guests";
        if (finds > 0) split += $" · ${finds} from your finds";
        return split;
    }

    /// <summary>What the thrift find sold for at this closing (0 by day, or when none sold): ThriftShelf's record.</summary>
    static int FindsTonight(DayClock clock) => clock != null && clock.DayOver ? ThriftShelf.SoldFor(clock.Day) : 0;

    // 24 Oct 2026 (Days): Tonight's two thrift lines, under the takings' split: what sold (in the card's own colour, it is
    // money) and tomorrow's line (the quiet colour). Their words count against the budget like the split's.
    float ThriftLines(RectTransform card, DayClock clock, float cy)
    {
        TonightSold = clock != null && clock.DayOver ? ThriftShelf.SoldLine(clock.Day) : "";
        TonightTomorrow = clock != null ? TomorrowLine(clock.Day) : "";
        TonightSoldWords = TonightTomorrowWords = 0;
        float width = ContentWidth - 2f * CardPadX;
        if (TonightSold.Length > 0)
        {
            int before = TonightWords;
            TMP_Text sold = Counted(Words(card, TonightSold, CardPadX, cy + 6f, width, TypeBody, DarkText, style: FontStyles.Bold, rich: false));
            sold.name = "Thrift find sold";
            TonightSoldWords = TonightWords - before;
            cy = Bottom(sold.rectTransform);
        }
        if (TonightTomorrow.Length > 0)
        {
            int before = TonightWords;
            TMP_Text next = Counted(Words(card, TonightTomorrow, CardPadX, cy + 6f, width, TypeBody, DarkFaint, rich: false));
            next.name = "Tomorrow";
            TonightTomorrowWords = TonightWords - before;
            cy = Bottom(next.rectTransform);
        }
        return cy;
    }

    /// <summary>"Tomorrow: a full house, and a cracked screen." (the next day's DayDefinition.previewLine), or "" when it has none.</summary>
    public static string TomorrowLine(int today)
    {
        CustomerSpawner spawner = Downtime.Spawner;
        DayDefinition next = spawner != null ? spawner.AuthoredDay(today + 1) : null;
        string line = next != null ? (next.previewLine ?? "").Trim() : "";
        return line.Length == 0 ? "" : "Tomorrow: " + line;
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
            RectTransform pill = Tag(card, "New star!", 0f, 0f, GoldSoft, GoldInk, TypeLabel);
            Place(pill, (ContentWidth - pill.sizeDelta.x) / 2f, cy, pill.sizeDelta.x, pill.sizeDelta.y);
            cy += pill.sizeDelta.y + 6f;
        }
        TMP_Text level = Words(card, ReputationRules.NameOf(stars), CardPadX, cy, ContentWidth - 2f * CardPadX, TypeLead, Ink,
            TextAlignmentOptions.Top, style: FontStyles.Bold, rich: false);
        cy = Bottom(level.rectTransform) + 10f;

        int next = rep.NextThreshold;
        float barWidth = ContentWidth - 2f * CardPadX;
        float share = next < 0 ? 1f : Mathf.Clamp01(rep.Reputation / (float)next);
        Paint(Box("Progress", card, CardPadX, cy, barWidth, 12f), Track, 6f);
        if (share > 0f) Paint(Box("Done", card, CardPadX, cy, Mathf.Max(12f, barWidth * share), 12f), Accent, 6f);
        cy += 12f + 7f;
        TMP_Text left = Words(card, next < 0 ? $"Reputation {rep.Reputation}" : $"{rep.Reputation} / {next} reputation",
            CardPadX, cy, 0f, TypeBody, Soft, wrap: false, rich: false);
        TMP_Text right = Words(card, next < 0 ? "Five stars" : "Next: " + ReputationRules.NameOf(stars + 1), 0f, cy, 0f, TypeBody, Soft,
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
        RectTransform mark = Box("Mark", card, CardPadX, y + 1f, 30f, 30f);   // 26 Oct 2026 (Readability): 26 to 30, for 22 px digits
        var disc = mark.gameObject.AddComponent<Image>();
        disc.sprite = circle;
        disc.color = back;
        disc.raycastTarget = false;
        if (number != null)
        {
            TMP_Text digits = Words(mark, number, 0f, 0f, 30f, TypeLabel, TextOn(ink), TextAlignmentOptions.Center, wrap: false,
                style: FontStyles.Bold, rich: false);
            Place(digits.rectTransform, 0f, 0f, 30f, 30f);
        }
        else Picture(mark, tick, ink, 7f, 7f, 16f, 16f);
        float x = CardPadX + 42f, width = ContentWidth - x - CardPadX;
        float bottom = Bottom(Words(card, text, x, y, width, TypeBody, Ink).rectTransform);
        if (!string.IsNullOrEmpty(under)) bottom = Bottom(Words(card, under, x, bottom + 1f, width, TypeBody, Soft, rich: false).rectTransform);
        return Mathf.Max(bottom, y + 32f) + 10f;
    }

    // The reviews and the reputation they made, a new star, and one line about the day's worst kind of review.
    float ChangesCard(float y, ReputationLedger rep)
    {
        RectTransform card = CardBox("Today's changes", y);
        float width = ContentWidth - 2f * CardPadX;
        int n = rep.ReviewCount;
        float cy = Bottom(Words(card, n == 0 ? "No reviews today."
            : $"{n} review{(n == 1 ? "" : "s")}: <b>{ReputationRecap.Signed(rep.TodayChange)}</b> reputation",
            CardPadX, 13f, width, TypeBody, Ink).rectTransform);
        if (rep.EarnedStarToday)
            cy = Bottom(Words(card, $"New star: <b>{ReputationRules.NameOf(rep.StarsEarned)}</b>", CardPadX, cy + 3f, width, TypeBody, Ink).rectTransform);
        // Only from real cards: a recap saved before them doesn't know why each review said what it said.
        string lesson = rep.Cards.Count > 0 ? ReputationRecap.Lesson(rep.Cards) : "";
        if (!string.IsNullOrEmpty(lesson)) cy = Bottom(Words(card, lesson, CardPadX, cy + 3f, width, TypeBody, Soft, rich: false).rectTransform);
        return FinishCard(card, cy + 13f, y);
    }

    static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => n + "th" };

    // ------------------------------------------------------------------ Shop

    float BuildShop(float y)
    {
        ShopInventory stock = ShopInventory.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        UpgradeManager upgrades = UpgradeManager.Instance;
        y = AppHeader(y, bag, Hex(0xe5efe7), Green, "Shop", till != null ? "In the till: $" + till.Money : "");
        y = BuildYourCafe(y);   // 24 Oct 2026 (Furniture): the café's furniture first (Close up's "up" still lands on the stock's last button)

        // 26 Oct 2026 (integration): the thrift card sits before the stock card, so the restock (and the upgrades after it) stay
        // the last buttons the pad reaches going up from Close up (RecapPhoneCheck's pad walk); a free find is always pressable.
        // 24 Oct 2026 (Days): tomorrow's thrift find, one at a time (his p2: bought at closing, fixed the next day, sold at
        // closing). Once the shelf is empty: the next day's find and what it sells for; bought, it says so.
        ThriftShelf shelf = ThriftShelf.Instance;
        if (shelf != null && DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            if (shelf.TomorrowsOffer(out ThriftShelf.Offer find))
            {
                y = Section(y, "THRIFT FIND FOR TOMORROW");
                RectTransform card = CardBox("Thrift find", y);
                bool afford = till != null && till.Money >= find.buy;
                float cy = BuyRow(card, 12f, "A thrift " + find.thing, $"{Capital(find.words)}. Sells for about ${find.sale}.", find.bought ? "Bought" : null,
                    find.bought ? "Bought" : find.buy > 0 ? "$" + find.buy : "Free", "thrift", BuyThriftFind, afford && !find.bought);
                y = FinishCard(card, cy + 2f, y);
            }
            else if (shelf.Find != null)
                y = HintBox(y, $"Your thrift {shelf.Thing} waits on the shelf for tomorrow.");
        }


        if (stock != null)
        {
            y = Section(y, "WHAT THE CAFÉ HAS");
            RectTransform card = CardBox("Stock", y);
            float cy = 12f;
            cy = StockRow(card, cy, cupBody, cupLid, Accent, Hex(0x8f4d1d), "Paper cups", "One for every drink", stock.Cups, stock.CupsLow, false);
            cy = StockRow(card, cy, bean, beanCrease, Hex(0x6b3f1f), Hex(0xc9a27a), "Coffee beans", "For the coffee", stock.Beans, stock.BeansLow, true);
            cy = Rule(card, cy);
            bool afford = till != null && till.Money >= stock.RestockCost;
            // 24 Oct 2026 (Days): a restock goes onto the back shelf (restock by hand, Designer P's §2.5).
            cy = BuyRow(card, cy, "Restock cups & beans", $"+{stock.RestockAdds} each, on the back shelf", null,
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
            // Progression (13 Oct 2026): what today pointed at, in one line, so the first upgrade answers "what screwed me
            // today?" (the first hour's note). PLACEHOLDER COPY.
            DayClock today = DayClock.Instance;
            if (today != null)
            {
                string pointer = today.Lost > 0
                    ? $"{today.Lost} walked out waiting today. Faster tools buy seconds; Shelf Space takes one more job."
                    : today.Declined > 0
                        ? $"{today.Declined} turned away today. Shelf Space takes one more repair at the till."
                        : "Nobody lost today. Bigger Bench opens two devices at once.";
                y = HintBox(y, pointer);
            }
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

    // 24 Oct 2026 (Furniture): YOUR CAFÉ, the Shop's first card at closing (furniture-spec.md §1.2; his choice of 23 Oct, "Grow the
    // café"): a chair, the seats the café has, a line, and one big button, Arrange the café (ArrangeMode: buy, place, move and
    // sell furniture, only while the café is closed: his answer of 24 Oct, f1). Day 1's line says "Place your first table" until a
    // piece has been bought. Not on the pause phone, and not in a scene without a café layout (CafeLayout).
    // 26 Oct 2026 (Icons 2; his note: "i wanted icons for the furniture you can also purchase too"): under the line, what Arrange
    // sells as pictures, one a kind in the strip's order (the icon sheet's furn_*, NoteIcons.Furniture), the kinds on sale tonight
    // at full and the rest dimmed as the strip dims them: the card says what's behind its button before it is pressed. No words
    // added. YourCafeIcons says what was drawn (the Furniture lab reads it). Without the sheet the card is as it was.
    Sprite chair;
    const float FurnitureIcon = 44f, FurnitureGap = 8f;

    /// <summary>26 Oct 2026 (Icons 2): the furniture pictures on the YOUR CAFÉ card as last built: "furn_bistro on sale; furn_lounge Day 3; ..." ("" for none).</summary>
    public string YourCafeIcons { get; private set; } = "";

    float BuildYourCafe(float y)
    {
        CafeLayout layout = CafeLayout.Instance;
        ArrangeMode arrange = ArrangeMode.Instance;
        YourCafeIcons = "";
        if (Kind != Mode.Recap || layout == null || arrange == null) return y;
        if (chair == null) chair = MakeSprite(Drawn("Chair", 64, Rectangle(6f, 3f, 9.5f, 21f), Rectangle(6f, 11.5f, 18f, 14.5f), Rectangle(14.5f, 14.5f, 18f, 21f)), 0f);
        y = Section(y, "YOUR CAFÉ");
        RectTransform card = CardBox("Your café", y);
        float cy = 14f;
        RectTransform box = Box("Icon", card, CardPadX, cy, 40f, 40f);
        Paint(box, IconBox, 11f);
        Picture(box, chair, Green, 9f, 9f, 22f, 22f);
        float x = CardPadX + 52f, width = ContentWidth - x - CardPadX;
        int seats = layout.Seats;
        TMP_Text name = Words(card, $"{seats} seat{(seats == 1 ? "" : "s")}", x, cy + 1f, width, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        TMP_Text line = Words(card, FirstTableWaiting() ? "Place your first table: free today" : "Tables and plants: moving is free", x, Bottom(name.rectTransform), width, TypeBody, Soft, rich: false);
        cy = Mathf.Max(cy + 40f, Bottom(line.rectTransform)) + 12f;
        cy = FurnitureRow(card, cy, layout);   // 26 Oct 2026 (Icons 2)
        Button button = Pill(card, "Arrange the café", ContentWidth - CardPadX, cy, TypeBody, Green, Color.white, "arrange", OpenArrange, arrange.CanOpen);
        // One big button: the card's whole width.
        var rect = (RectTransform)button.transform;
        float height = rect.sizeDelta.y, wide = ContentWidth - 2f * CardPadX;
        Place(rect, CardPadX, cy, wide, height);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) Place(label.rectTransform, 0f, 0f, wide, height);
        return FinishCard(card, cy + height + 14f, y);
    }

    // 26 Oct 2026 (Icons 2): the kinds' pictures in a row, the ones on sale at full and the rest dimmed; y after the row (unchanged
    // when the sheet has none of them).
    float FurnitureRow(RectTransform card, float y, CafeLayout layout)
    {
        FurnitureDefinition[] kinds = layout != null && layout.Catalogue != null ? layout.Catalogue.kinds : null;
        if (kinds == null) return y;
        var drawn = new StringBuilder();
        float x = CardPadX;
        foreach (FurnitureDefinition k in kinds)
        {
            if (k == null) continue;
            string icon = NoteIcons.Furniture(k.name);
            Sprite sprite = NoteIcons.SpriteOf(icon);
            if (sprite == null) continue;
            if (x + FurnitureIcon > ContentWidth - CardPadX) break;   // the row keeps to the card (seven fit with room to spare)
            bool forSale = layout.ForSale(k);
            Image picture = Picture(card, sprite, forSale ? Color.white : new Color(1f, 1f, 1f, .4f), x, y, FurnitureIcon, FurnitureIcon);
            picture.preserveAspect = true;
            picture.name = "Furniture - " + icon;
            if (drawn.Length > 0) drawn.Append("; ");
            drawn.Append(icon).Append(forSale ? " on sale" : " Day " + Mathf.Max(1, k.forSaleFromDay));
            x += FurnitureIcon + FurnitureGap;
        }
        YourCafeIcons = drawn.ToString();
        return drawn.Length > 0 ? y + FurnitureIcon + 12f : y;
    }

    void OpenArrange()
    {
        ArrangeMode arrange = ArrangeMode.Instance;
        if (arrange != null) arrange.TryOpen();
    }

    // Day 1's evening and nothing bought yet (the badge and the card's line).
    static bool FirstTableWaiting()
    {
        CafeLayout layout = CafeLayout.Instance;
        DayClock clock = DayClock.Instance;
        if (layout == null || clock == null || clock.Day > 1 || ArrangeMode.Instance == null) return false;
        foreach (PlacedPiece p in layout.Pieces) if (p != null && !p.FromScene) return false;
        return true;
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
        TMP_Text name = Words(card, title, x, y + 1f, width, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        TMP_Text sub = Words(card, what, x, Bottom(name.rectTransform), width, TypeBody, Soft, rich: false);
        float bottom = Mathf.Max(y + 40f, Bottom(sub.rectTransform));
        TMP_Text number = Words(card, count.ToString(), 0f, 0f, 0f, TypeBody, low ? Red : Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Vector2 size = number.rectTransform.sizeDelta;
        Place(number.rectTransform, ContentWidth - CardPadX - size.x, (y + bottom) / 2f - size.y / 2f, size.x, size.y);
        return bottom + 10f;
    }

    // Something to buy: its name (and level), what it does, and its price on a button (grey when it
    // can't be bought: too dear, or maxed).
    float BuyRow(RectTransform card, float y, string title, string what, string tag, string price, string key, UnityAction buy, bool canBuy)
    {
        Button button = Pill(card, price, ContentWidth - CardPadX, y, TypeBody, PriceOrange, Color.white, key, buy, canBuy);
        var buttonRect = (RectTransform)button.transform;
        float width = ContentWidth - 2f * CardPadX - buttonRect.sizeDelta.x - 12f;
        TMP_Text name = Words(card, title, CardPadX, y, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        if (name.rectTransform.sizeDelta.x > width)
        {
            Object.Destroy(name.gameObject);
            name = Words(card, title, CardPadX, y, width, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        }
        else if (!string.IsNullOrEmpty(tag))
            Tag(card, tag, CardPadX + name.rectTransform.sizeDelta.x + 8f, y + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float bottom = Bottom(name.rectTransform);
        if (!string.IsNullOrEmpty(what)) bottom = Bottom(Words(card, what, CardPadX, bottom, width, TypeBody, Soft, rich: false).rectTransform);
        float middle = (y + bottom) / 2f, height = buttonRect.sizeDelta.y;
        buttonRect.anchoredPosition = new Vector2(buttonRect.anchoredPosition.x, -(middle - height / 2f));
        return Mathf.Max(bottom, middle + height / 2f) + 10f;
    }

    void BuyRestock()
    {
        if (UpgradeShopUI.TryRestock()) Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    // 24 Oct 2026 (Days): tomorrow's thrift find (ThriftShelf), saved with the next checkpoint (Open Tomorrow).
    void BuyThriftFind()
    {
        ThriftShelf shelf = ThriftShelf.Instance;
        if (shelf != null && shelf.TryBuyTomorrow()) Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    static string Capital(string words) => string.IsNullOrEmpty(words) ? "" : char.ToUpperInvariant(words[0]) + words.Substring(1);

    void BuyUpgrade(UpgradeDefinition def)
    {
        if (UpgradeShopUI.TryBuy(def)) Sfx.Play2D("phone.tap");
        RebuildNow();
    }

    // ------------------------------------------------------------------ Notes

    float BuildNotes(float y)
    {
        y = AppHeader(y, note, Hex(0xece7f5), Hex(0x6b5aa0), "Notes", "Press a name for everything.");
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
        OpenPerson = OpenPerson == who ? "" : who ?? "";   // the row's press has tapped already (Pressed)
        RebuildNow();
    }

    // One person, one line: the avatar, the name and how much is new, the latest thing known (today's if there is one),
    // in his hand for his pages. The whole row is the button that opens them.
    float PersonRow(float y, NotebookPerson person, int day)
    {
        RectTransform card = CardBox("Person", y);
        // Who it is, as a picture (23 Oct 2026, the notebook's pictures, NotePhotos): Grace, the Officer, the man at the bins;
        // other regulars keep their lettered disc.
        string pictureKey = NoteSubjects.KeyOfPerson(person.who);
        if (pictureKey != NoteSubjects.Someone) NotePhotos.Thumb(card, pictureKey, CardPadX, 12f, 36f);
        else Avatar(card, CardPadX, 12f, 36f, person.name);
        float x = CardPadX + 48f, width = ContentWidth - x - CardPadX - 18f;
        TMP_Text name = Words(card, person.name, x, 11f, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        int fresh = person.LearnedOn(day);
        if (fresh > 0) Tag(card, fresh + " new", x + name.rectTransform.sizeDelta.x + 8f, 11f + name.rectTransform.sizeDelta.y / 2f, AccentSoft, Accent);
        float bottom = Bottom(name.rectTransform);
        NotebookFactData fact = NotebookRecap.OneLine(person, day);
        if (fact != null)
        {
            bool his = fact.source == Notebook.Sources.Inherited;
            bottom = Bottom(Words(card, "<noparse>" + NotebookRecap.Sentence(fact.text) + "</noparse>", x, bottom + 1f, width, TypeBody, Soft,
                style: his ? FontStyles.Italic : FontStyles.Normal).rectTransform);
        }
        float height = Mathf.Max(12f + 36f, bottom) + 12f;
        TMP_Text chevron = Words(card, "›", 0f, 0f, 0f, 24f, Soft, wrap: false, rich: false);
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
        string pictureKey = NoteSubjects.KeyOfPerson(person.who);   // as on the row (23 Oct 2026)
        if (pictureKey != NoteSubjects.Someone) NotePhotos.Thumb(card, pictureKey, CardPadX, 14f, 40f);
        else Avatar(card, CardPadX, 14f, 40f, person.name);
        float x = CardPadX + 52f;
        TMP_Text name = Words(card, person.name, x, 0f, 0f, TypeBody, Ink, wrap: false, style: FontStyles.Bold, rich: false);
        Vector2 nameSize = name.rectTransform.sizeDelta;
        Place(name.rectTransform, x, 34f - nameSize.y / 2f, nameSize.x, nameSize.y);
        int fresh = person.LearnedOn(day);
        if (fresh > 0) Tag(card, fresh + " new", x + nameSize.x + 8f, 34f, AccentSoft, Accent);
        TMP_Text chevron = Words(card, "×", 0f, 0f, 0f, 22f, Soft, wrap: false, rich: false);
        Place(chevron.rectTransform, ContentWidth - CardPadX - chevron.rectTransform.sizeDelta.x + 2f, 34f - chevron.rectTransform.sizeDelta.y / 2f + 4f,
            chevron.rectTransform.sizeDelta.x, chevron.rectTransform.sizeDelta.y);
        float cy = 14f + 40f + 4f;
        // A fact about something other than the person carries that thing's picture where the dot was (23 Oct 2026): Barnaby,
        // Grace's house, the cups. The rest keep the dot.
        const float factPicture = 28f;
        float textX = CardPadX + factPicture + 10f, width = ContentWidth - textX - CardPadX;
        for (int i = 0; i < person.facts.Count; i++)
        {
            NotebookFactData fact = person.facts[i];
            if (i > 0) cy = Rule(card, cy - 3f) - 3f;
            string text = "<noparse>" + NotebookRecap.Sentence(fact.text) + "</noparse>";
            string sure = NotebookRecap.Sureness(fact, day);
            if (sure.Length > 0) text += $" <color={FaintHex}>({sure})</color>";
            if (fact.day == day) text += $"  <color={AccentHex}><size=85%><b>NEW</b></size></color>";
            TMP_Text line = Words(card, text, textX, cy, width, TypeBody, Ink,
                style: fact.source == Notebook.Sources.Inherited ? FontStyles.Italic : FontStyles.Normal);
            string about = NoteSubjects.KeyOf(fact);
            if (about != pictureKey && about != NoteSubjects.Someone) NotePhotos.Thumb(card, about, CardPadX, cy, factPicture);
            else Picture(card, circle, Accent, CardPadX + (factPicture - 7f) / 2f, cy + 7.5f, 7f, 7f);
            cy = Mathf.Max(Bottom(line.rectTransform), cy + factPicture) + 10f;
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
        TMP_Text kicker = Words(card, when, CardPadX, 14f, width, TypeLabel, DarkFaint, style: FontStyles.Bold, rich: false, spacing: 4f);
        float top = Bottom(kicker.rectTransform) + 6f;
        TMP_Text money = Words(card, "$" + (clock != null ? clock.Earned : 0), CardPadX, top, 0f, 40f, DarkText, wrap: false,
            style: FontStyles.Bold, rich: false);
        TMP_Text label = Words(card, "earned so far today", CardPadX, Bottom(money.rectTransform) - 2f, 300f, TypeBody, DarkFaint, rich: false);

        const float starSize = 20f, starGap = 3f;
        float starsWidth = 5f * starSize + 4f * starGap;
        int stars = rep != null ? Mathf.Clamp(rep.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        Stars(card, right - starsWidth, top + 8f, starSize, starGap, stars, dark: true);
        TMP_Text count = Words(card, LiveStarsLine(rep), 0f, 0f, 0f, TypeBody, DarkFaint, wrap: false, rich: false);
        Vector2 countSize = count.rectTransform.sizeDelta;
        Place(count.rectTransform, right - countSize.x, top + 8f + starSize + 7f, countSize.x, countSize.y);

        float cy = Mathf.Max(Bottom(label.rectTransform), Bottom(count.rectTransform)) + 12f;
        // What the player's own work earned, apart from the café's guests (they pay at the tables without a pop now).
        string split = clock != null ? TakingsSplit(clock.Earned, clock.PatronIncome) : "";
        if (split.Length > 0) cy = Bottom(Words(card, split, CardPadX, cy - 6f, width, TypeBody, DarkFaint, rich: false).rectTransform) + 12f;
        Paint(Box("Rule", card, CardPadX, cy, width, 1f), DarkLine, 0f);
        cy += 10f;
        int till = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0;
        TMP_Text tillLabel = Words(card, "In the till", CardPadX, cy, width, TypeBody, DarkText, rich: false);
        Words(card, "$" + till, CardPadX, cy, width, TypeBody, DarkText, TextAlignmentOptions.TopRight, style: FontStyles.Bold, rich: false);
        cy = Bottom(tillLabel.rectTransform) + 4f;
        int n = rep != null ? rep.ReviewCount : 0;
        string reviews = n == 0 ? "No reviews yet: they come in as people leave."
            : $"{n} review{(n == 1 ? "" : "s")} so far  ·  {ReputationRecap.Signed(rep.TodayChange)} reputation";
        cy = Bottom(Words(card, reviews, CardPadX, cy, width, TypeBody, DarkFaint, rich: false).rectTransform);
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
        // Light, Strong or Off (23 Oct 2026, his note: "helpful, but not so overpowering that it's like fighting me"): Light is the
        // default and what "on" was before; Strong the 7 Oct numbers without the fighting (AimAssist). PLACEHOLDER COPY.
        cy = ChoiceRow(controls, cy, "Aim help", "On a pad: the view slows and settles on things. Strong pulls harder.",
            AimHelpWords, GameSettings.AimLevel.ToString(), "aimhelp", word => GameSettings.AimLevel = AimLevelOf(word));
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Movement help", "On a pad: walking straightens along the café.", GameSettings.MovementHelp,
            () => Flip(() => GameSettings.MovementHelp = !GameSettings.MovementHelp), "movehelp");
        // At the bench only (12 Oct, his 1b: "i want the rotating and just the controls overall to be simple"), for players who
        // expect the right stick to turn things; off by default, since the right stick aims everywhere else. PLACEHOLDER COPY.
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Swap the sticks at the bench", "Left stick moves your hand, right stick turns the device.",
            GameSettings.SwapBenchSticks, () => Flip(() => GameSettings.SwapBenchSticks = !GameSettings.SwapBenchSticks), "swapsticks");
        // Pad rumble at the bench (14 Oct, Systems' decision 13: "feel the impact"); on by default. Since 23 Oct the phone's presses
        // rumble too (Pressed), and its line says so (the integrator's yes to builder J's question). PLACEHOLDER COPY.
        cy = Rule(controls, cy);
        cy = SwitchRow(controls, cy, "Vibration", "On a pad: the bench's tools and the phone's buttons rumble.", GameSettings.Vibration,
            () => Flip(() => GameSettings.Vibration = !GameSettings.Vibration), "vibration");
        y = FinishCard(controls, cy + 2f, y);

        // The morning's few seconds of the café waking up (12 Oct, his 6: every day, skippable, with Short and Off here). A
        // section of its own above the picture: it's the game's rhythm, not its controls or its looks. PLACEHOLDER COPY.
        y = Section(y, "MORNINGS");
        RectTransform mornings = CardBox("Mornings", y);
        cy = ChoiceRow(mornings, 12f, "The morning", "A few seconds of the café waking up, every day. Short: just the title.",
            MorningWords, GameSettings.DayIntro.ToString(), "morning", word => GameSettings.DayIntro = MorningOf(word));
        y = FinishCard(mornings, cy + 2f, y);

        y = Section(y, "PICTURE");
        RectTransform picture = CardBox("Picture", y);
        cy = QualityRow(picture, 12f);
        y = FinishCard(picture, cy + 2f, y);

        y = Section(y, "LEAVING");
        return QuitCard(y);
    }

    // The morning's three ways in the row's order (the setting's own), and their words on the pills: Full, Short, Off.
    static readonly DayIntroMode[] Mornings = { DayIntroMode.Full, DayIntroMode.Short, DayIntroMode.Off };
    static readonly string[] MorningWords = Array.ConvertAll(Mornings, mode => mode.ToString());

    static DayIntroMode MorningOf(string word)
    {
        int at = Array.IndexOf(MorningWords, word);
        return at >= 0 ? Mornings[at] : DayIntroMode.Full;
    }

    // The aim help's three settings in the row's order (23 Oct 2026), and their words on the pills: Light, Strong, Off.
    static readonly AimHelpLevel[] AimLevels = { AimHelpLevel.Light, AimHelpLevel.Strong, AimHelpLevel.Off };
    static readonly string[] AimHelpWords = Array.ConvertAll(AimLevels, level => level.ToString());

    static AimHelpLevel AimLevelOf(string word)
    {
        int at = Array.IndexOf(AimHelpWords, word);
        return at >= 0 ? AimLevels[at] : AimHelpLevel.Light;
    }

    static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    static string Times(float v) => v.ToString("0.0") + "x";

    void Flip(Action change)
    {
        change();
        quitArmed = false;   // the press has tapped already (Pressed)
        RebuildNow();
    }

    // A name and its value on one line, the slider under them: drag it, click along it, or with a pad select it and
    // press the D-pad left and right (a tenth of the way a press).
    float SliderRow(RectTransform card, float cy, string label, float value, float min, float max, Func<float, string> format,
                    Action<float> set, string key)
    {
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text name = Words(card, label, CardPadX, cy, width * .7f, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        TMP_Text shown = Words(card, format(value), CardPadX, cy, width, TypeBody, Soft, TextAlignmentOptions.TopRight, rich: false);
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
        colours.disabledColor = Color.white;   // as the buttons': the knob doesn't grey as the phone drops away (PutAway, 23 Oct)
        colours.colorMultiplier = 1f;
        colours.fadeDuration = .06f;
        slider.colors = colours;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };   // wired after each build
        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            quitArmed = false;
            shown.text = format(v);
            KnobFollows(slider);   // the knob glides after the value (23 Oct): a D-pad step, a click along it; not a mouse drag
        });
        sliderKeys[slider] = key;
        knobs.Add(new Knob { slider = slider, handle = handle, fill = fill, drag = rect.gameObject.AddComponent<KnobDrag>(),
                             shown = slider.normalizedValue });
        AddNav(slider);
        return top + height + 10f;
    }

    // A name (and a line saying what it does) with a switch on the right: green and knob right when on.
    float SwitchRow(RectTransform card, float cy, string label, string detail, bool on, UnityAction flip, string key)
    {
        float width = ContentWidth - 2f * CardPadX, textWidth = width - 70f;
        TMP_Text name = Words(card, label, CardPadX, cy, textWidth, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        float bottom = Bottom(name.rectTransform);
        if (!string.IsNullOrEmpty(detail))
            bottom = Bottom(Words(card, detail, CardPadX, bottom + 1f, textWidth, TypeBody, Soft, rich: false).rectTransform);
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
        // Just flipped (23 Oct): the knob slides across from the side it was on as the track's colour turns (the press's tap is the
        // click). Timed from the press, so a second build straight after carries on where it was.
        if (key == pressKey && Now() - pressAt < KnobSeconds)
        {
            float there = on ? w - h + 3f : 3f, was = on ? 3f : w - h + 3f;
            motions.MoveX(knob, was, there, pressAt, KnobSeconds, UiTween.Ease.OutBack);
            motions.Colour(track, on ? ButtonOff : Green, on ? Green : ButtonOff, pressAt, KnobSeconds, UiTween.Ease.OutCubic);
        }
        return Mathf.Max(bottom, mid + h / 2f) + 10f;
    }

    // Low, Medium and High side by side; the one in force in green.
    float QualityRow(RectTransform card, float cy) =>
        ChoiceRow(card, cy, "Quality", "Lower runs smoother on a laptop.", QualityPreset.Names, GameSettings.Quality, "quality",
            level => GameSettings.Quality = level);

    // A name, a line saying what it does, and its choices side by side as pills, the one in force in green: one row for the
    // D-pad (left and right along it; up and down to the rows above and under, like any row), so a pad reaches the morning
    // the way it reaches the picture. A press sets it and builds the app again (Flip), the pad keeping its place. Each pill's
    // key is "<key>:<choice>" ("quality:Medium", "morning:Short").
    float ChoiceRow(RectTransform card, float cy, string label, string detail, string[] choices, string current, string key,
                    Action<string> choose)
    {
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text name = Words(card, label, CardPadX, cy, width, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        float top = Bottom(name.rectTransform) + 2f;
        top = Bottom(Words(card, detail, CardPadX, top, width, TypeBody, Soft, rich: false).rectTransform) + 9f;
        const float gap = 8f, height = 46f;   // 26 Oct 2026 (Readability): 38 to 46, for its 24 px words
        float each = (width - (choices.Length - 1) * gap) / choices.Length;
        for (int i = 0; i < choices.Length; i++)
        {
            string choice = choices[i];
            bool on = current == choice;
            RectTransform pill = Box("Button: " + key + ":" + choice, card, CardPadX + i * (each + gap), top, each, height);
            Image face = Paint(pill, on ? Green : ButtonOff, height / 2f);
            TMP_Text words = Words(pill, choice, 0f, 0f, each, TypeBody, on ? Color.white : Soft, TextAlignmentOptions.Center, wrap: false,
                style: FontStyles.Bold, rich: false);
            Place(words.rectTransform, 0f, 0f, each, height);
            sameRow = i > 0;
            Pressable(pill, face, key + ":" + choice, () => Flip(() => choose(choice)), dark: on);
            sameRow = false;
        }
        return top + height + 12f;
    }

    // Quit, saying plainly what isn't kept. The first press asks again (in red); the second quits (PausePhone).
    float QuitCard(float y)
    {
        RectTransform card = CardBox("Quit", y);
        float width = ContentWidth - 2f * CardPadX;
        TMP_Text title = Words(card, "Quit the game", CardPadX, 14f, width, TypeBody, Ink, style: FontStyles.Bold, rich: false);
        string note = Kind == Mode.NightPause
            ? "Today and tonight aren't kept: the game saves when you open tomorrow."
            : "The day so far isn't kept: the game saves when you open tomorrow.";
        TMP_Text words = Words(card, note, CardPadX, Bottom(title.rectTransform) + 2f, width, TypeBody, quitArmed ? Red : Soft, rich: false);
        float top = Bottom(words.rectTransform) + 12f;
        RectTransform rect = Box("Button: quit", card, CardPadX, top, width, 52f);   // 26 Oct 2026 (Readability): 44 to 52
        Image face = Paint(rect, quitArmed ? Red : UiSkin.RedSoft, 16f);
        TMP_Text label = Words(rect, quitArmed ? "Press again to quit" : "Quit", 0f, 0f, width, TypeBody, quitArmed ? Color.white : Red,
            TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
        Place(label.rectTransform, 0f, 0f, width, 52f);
        Pressable(rect, face, "quit", PressQuit, dark: quitArmed);
        return FinishCard(card, top + 52f + 14f, y);
    }

    void PressQuit()
    {
        // The press has tapped already (Pressed).
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
        float bottom = Bottom(Words(content, title, x, y - 3f, width, TypeTitle, Ink, style: FontStyles.Bold, rich: false).rectTransform);
        if (!string.IsNullOrEmpty(subtitle)) bottom = Bottom(Words(content, subtitle, x, bottom - 3f, width, TypeBody, Soft, rich: false).rectTransform);
        return Mathf.Max(bottom, y + 46f) + 12f;
    }

    float Section(float y, string title)
    {
        TMP_Text words = Words(content, title, Pad + 4f, y + 6f, ContentWidth - 8f, TypeLabel, Soft, style: FontStyles.Bold, rich: false, spacing: 5f);
        return Bottom(words.rectTransform) + 8f;
    }

    float MessageCard(float y, string text)
    {
        RectTransform card = CardBox("Message", y);
        TMP_Text words = Words(card, text, CardPadX, 15f, ContentWidth - 2f * CardPadX, TypeBody, Soft, rich: false);
        return FinishCard(card, Bottom(words.rectTransform) + 15f, y);
    }

    float HintBox(float y, string text)
    {
        RectTransform box = Box("Low stock", content, Pad, y, ContentWidth, 10f);
        Paint(box, AccentSoft, 12f);
        TMP_Text words = Words(box, text, 12f, 9f, ContentWidth - 24f, TypeBody, AccentText, rich: false);
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
        TMP_Text letter = Words((RectTransform)disc.transform, Initial(name), 0f, 0f, size, Mathf.Max(TypeTab, size * .5f), Color.white,
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
    RectTransform Tag(RectTransform parent, string text, float x, float centreY, Color back, Color ink, float size = TypeLabel)
    {
        float height = Mathf.Round(size + 9f);
        RectTransform pill = Box("Tag", parent, x, centreY - height / 2f, 10f, height);
        Paint(pill, back, height / 2f);
        TMP_Text words = Words(pill, text, 0f, 0f, 0f, size, TextOn(ink), TextAlignmentOptions.Center, wrap: false, style: FontStyles.Bold, rich: false);
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
        // 26 Oct 2026 (Readability; the gather: a price out of reach, "$125", at 3.9:1): a greyed price in OffInk (5.5:1 on its grey).
        TMP_Text words = Words(rect, label, 0f, 0f, 0f, size, enabled ? ink : OffInk, TextAlignmentOptions.Center, wrap: false,
            style: FontStyles.Bold, rich: false);
        float width = Mathf.Ceil(words.rectTransform.sizeDelta.x) + Mathf.Round(size * 1.6f);
        Place(rect, right - width, top, width, height);
        Place(words.rectTransform, 0f, 0f, width, height);
        Button button = Pressable(rect, face, key, press, dark: back.grayscale < .3f);
        button.interactable = enabled;
        // Out of reach (a price too dear): a click shakes it a little instead of doing nothing (23 Oct). A pad never lands on it.
        if (!enabled) rect.gameObject.AddComponent<Nudge>().phone = this;
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
        // First, before what the button does (23 Oct): whatever was moving finishes, the button squishes and springs back, the tap,
        // and on a pad a light rumble (Pressed). Close up's own listeners (RecapUI's, PausePhone's) come after this one.
        button.onClick.AddListener(() => Pressed(button));
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

    // (x, y) is the box's top-left corner, whatever its pivot: a box that squishes or pops (23 Oct) turns about its middle, and
    // Place still puts its corner where it says. For the usual top-left pivot this is the box's anchored position, as it always was.
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        Vector2 pivot = rect.pivot;
        rect.anchoredPosition = new Vector2(x + pivot.x * width, -y - (1f - pivot.y) * height);
        rect.sizeDelta = new Vector2(width, height);
    }

    static float Bottom(RectTransform rect) => -rect.anchoredPosition.y + rect.pivot.y * rect.sizeDelta.y;

    // Turns a box about its middle from now on, without moving it (a squish or a pop shrinks toward its middle, not its corner).
    static void CentrePivot(RectTransform rect)
    {
        Vector2 middle = new Vector2(.5f, .5f), was = rect.pivot;
        if (was == middle) return;
        Vector2 size = rect.rect.size;
        rect.pivot = middle;
        rect.anchoredPosition += Vector2.Scale(middle - was, size);
    }

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
        focusRingSprite = MakeSprite(FocusRingTexture(), RoundRadius);
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

    // The pad's focus ring: a gold stroke with a dark (ink) edge either side, in one texture, so it reads on a light button
    // and on an orange one alike. 12 px wide at the texture's scale (about 4.5 px as drawn): 2.5 px of ink, 7 of gold, 2.5 of ink.
    static Texture2D FocusRingTexture()
    {
        const float width = 12f, edge = 2.5f;
        Texture2D texture = NewTexture("Focus ring", RoundPixels);
        var pixels = new Color32[RoundPixels * RoundPixels];
        Color32 gold = Gold, ink = Ink;
        for (int y = 0; y < RoundPixels; y++)
            for (int x = 0; x < RoundPixels; x++)
            {
                float cx = x + .5f, cy = y + .5f;
                float dx = Mathf.Max(RoundRadius - cx, cx - (RoundPixels - RoundRadius), 0f);
                float dy = Mathf.Max(RoundRadius - cy, cy - (RoundPixels - RoundRadius), 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - RoundRadius;   // < 0 inside the box's edge
                float alpha = Mathf.Clamp01(.5f - outside) * Mathf.Clamp01(outside + width + .5f);
                float depth = -outside;   // 0 at the outer edge, width at the inner
                bool edgePixel = depth < edge || depth > width - edge;
                Color32 c = edgePixel ? ink : gold;
                c.a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * RoundPixels + x] = c;
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

    // Switched off, its Update (which stops a press's rumble on time) no longer runs: the rumble stops now (23 Oct).
    void OnDisable() => StopBuzz(true);

    void OnDestroy()
    {
        StopBuzz(true);   // a press's rumble never outlives the phone
        motions.Clear();
        foreach (Object thing in made) if (thing != null) Destroy(thing);
        made.Clear();
    }
}
