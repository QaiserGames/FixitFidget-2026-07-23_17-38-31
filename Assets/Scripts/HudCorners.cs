using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE HUD'S CORNERS: THE SHOP WINDOW TOP CENTRE, ONE HUNG COLUMN TOP RIGHT, THE STOCK CHIPS BOTTOM RIGHT
// (26 Oct 2026 (HUD 3): his design A built in the engine; first built 7 Oct 2026 for playtest 3, remade 19 Oct after her
// playtest 7; the HUD spec claude/hud-spec.md, M2's round 2 review24/designs/M2/README.md)
//
// Her playtest (dyslexic, her first time, a PlayStation pad): "what day, what time? Oh, this is way too small." His answers,
// 24 Oct: design A, "the Shop window"; the time top centre by day and by night; "today" gone; "Franchise at 5 stars" gone
// (Day 1's card, a 6 s hint, the star toasts and the phone say the goal); San Francisco unmistakable in the window, as the
// city the café is in, "so we can always change the hud to match the cafe ... during the franchising"; the cash redone after
// studying how other games make money feel; "one more level above". 26 Oct: the till was "the only good option given": a
// better one; one money object everywhere; keep the receipt; Seattle stays the placeholder next city.
//
// So, by day:
//   THE WINDOW         top centre: an arched painted window (HudWindow) whose glass holds the living sky and the café's
//                      city (HudCity: San Francisco; one swappable layer), the time in gilt on its sill (32 px bold) and the
//                      day in mint (22 px). The sun rises behind the houses at opening, sets behind the Golden Gate at
//                      closing, the moon comes up, the city's windows light; nothing else on the HUD tells the hour.
//   THE COLUMN         top right, hung from a brass bracket, top to bottom:
//     the sign         kept: the painted board with OPEN ("Come in"), LAST ORDERS on honey ("Closing at 8 PM", the
//                      hud.lastorders chime, a quicker sway), CLOSED on plum ("See you tomorrow", the sign's flap, a bounce);
//                      never red; a card with two faces dressed before it turns. It hangs on two brass chains now.
//     the money        ACE'S JAR on its small painted board (HudMoney), hung from the sign on two rings: today's takings in
//                      40 px cream with a gilt "$"; the jar fills a step for every $24; a payment flies in from the pay pop
//                      over the customer and lands with a beat, a count and a receipt; spending is the same jar, quieter.
//     the stars        five pips, a dotted road and a tiny Ace's at its end (the goal with no words). A star lands with a
//                      drop, a glint down the road and the café front jumping and lighting up; a toast says "New star! ·
//                      4 more stars to franchise" for 4 s; on Day 1 a 6 s hint says "Five stars to franchise".
//   STOCK CHIPS        bottom right, only when cups or beans run low (as since 19 Oct).
// At night only the window shows (top centre), with the night's hour and "Monday night"; the night's old top-right clock is
// gone (ShopUI). AT THE BENCH the column folds into a strip in zone U2 (24-110 px down): a small window, the time at 30 px
// and the jar small beside a 30 px figure; at last orders or after closing a honey or plum chip with the sign's word takes
// the small window's place. Everything times itself on UiClock, so it holds while the phone pauses the game. Hidden while
// the recap has the screen and under the day intro. Made by ShopUI while playing, on a canvas of its own drawn under every
// other screen (2): nothing in the scene changes.
//
// Material and light ("one level above", M2 §5): painted deep green with gilt lines and brass fittings for the café's own
// pieces (the window, the money, the strip), enamel on a board for the sign, paper for the receipt, the ink band for words
// over the world; the light from the top left, one soft shadow down and right; at rest only the sign and the window move.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class HudCorners : MonoBehaviour
{
    public static HudCorners Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public const int SortingOrder = 2;

    // ---- the column (reference pixels, 1920 x 1080; measured from the screen's top right corner) ----
    public const float ColumnRight = 28f, ColumnTop = 24f, ColumnWidth = 296f;
    const float Pad = 12f;
    // The sign on its chains.
    const float StringLength = 16f, SignWidth = 280f, SignHeight = 112f, Border = 3f, SignRadius = 14f;
    const float SignBottom = StringLength + SignHeight;                   // 128: the sign's foot, from the column's top
    // The money board under it, on two rings.
    const float RingGap = 20f, BoardTop = SignBottom + RingGap;           // 148: the board's top (172 px down the screen)
    // The stars' row under the board.
    const float StarsTop = BoardTop + HudMoney.BoardH + 26f;              // 238 (262 down the screen)
    const float StarSize = 22f, StarGap = 4f, RoadGap = 8f, FrontW = 26f, FrontH = 24f;
    const float ColumnBottom = StarsTop + FrontH + 4f;                    // 266: the column's bottom in the café (290 down the screen)
    // The window, top centre.
    const float WindowTop = 4f;
    // The strip at the bench (zone U2: 18-110 px down, 324 wide from the right edge).
    const float StripHeight = 86f, StripPad = 8f, StripTimeSize = 30f, MiniWindowScale = .36f;
    const float ChipWordSize = 24f, ChipPadX = 12f, ChipRow = 34f;
    // The stock chips.
    // 26 Oct 2026 (Readability; the gather: "Refill from the back shelf" 20 px at 3.3:1, paper on the accent): the chip's words 24
    // and 22, bold, on Ember (paper on it 5.6:1); the chip a little taller for them. The state chip's word 22 to 24.
    const float StockHeight = 66f, StockPitch = 74f, StockTitleSize = 24f, StockLineSize = 22f;
    static readonly Color Ember = new Color(.604f, .298f, .090f);       // #9A4C17: the stock chip's face (paper on it 5.6:1)
    const float LabNudge = 92f;   // the café lab's banner sits in the bottom right corner (labs only)
    // The toast under the column.
    const float ToastTitleSize = 26f, ToastLineSize = 24f, ToastWidth = 300f, ToastSeconds = 4f, HintSeconds = 6f;   // 26 Oct 2026 (Readability): the toast 24/22 to 26/24

    // ---- the sign's faces (never red: his words, 19 Oct) ----
    static readonly Color Honey = new Color(.941f, .710f, .298f);       // #F0B54C: the LAST ORDERS face
    static readonly Color Plum = new Color(.490f, .227f, .267f);        // #7D3A44: the CLOSED face, warm brick-plum
    static readonly Color PlumRim = new Color(.318f, .149f, .188f);     // #512630
    static readonly Color Brass = HudArt.Hex(0xC9A24A), BrassDark = HudArt.Hex(0x8C6A24), BrassLight = HudArt.Hex(0xF0D48A);

    // ---- timing (seconds of UiClock) ----
    const float FlipSeconds = .72f, ChipInSeconds = .4f, FadeRate = 8f, SignPunchSeconds = .5f;

    Canvas canvas;
    RectTransform root, column, flights;
    CanvasGroup top, bottom, columnGroup;

    // The sign.
    RectTransform sign, sway, flipper;
    Image signShadow;
    CanvasGroup signGroup;
    sealed class Face
    {
        public RectTransform rect;
        public Image border, face, cup, lipTop, lipLeft;
        public TextMeshProUGUI word, sub;
        public State state;
    }
    Face faceA, faceB, shown;
    enum State { Open, LastOrders, Closed }
    State state = State.Open;
    bool stateKnown;
    // The day intro (13 Oct 2026, Cinematics): the sign waits on CLOSED while it plays and flips over to OPEN as the HUD comes back.
    bool waitedForTheIntro;
    float angle, flipFrom, flipTo, flipSince = -10f, swayPhase, closedAt = -10f;
    int shownClosingHour = int.MinValue;

    // The window, the money, the stars.
    HudWindow window, miniWindow;
    RectTransform miniWindowRect;
    CanvasGroup miniGroup, windowGroup;
    TextMeshProUGUI stripTime;
    HudMoney money;
    RectTransform stars;
    CanvasGroup starsGroup, moneyGroup;
    readonly Image[] starImages = new Image[ReputationRules.MaxStars];
    readonly Image[] starGlows = new Image[ReputationRules.MaxStars];
    Image frontLit, frontGlow, glint;
    RectTransform frontRect;
    int shownStars = -1;
    float starLandAt = -10f;
    // The toast under the column: "New star! · 4 more stars to franchise", or Day 1's hint "Five stars to franchise".
    RectTransform toast;
    CanvasGroup toastGroup;
    TextMeshProUGUI toastText;
    float toastShownAt = -10f, toastLife;
    bool hintShown;
    // Today's takings as the money object tracks them.
    int targetMoney = int.MinValue, patronIncomeSeen, tipsSeen;
    struct Pop { public Transform who; public int amount, tip; public float at; }
    readonly List<Pop> pops = new List<Pop>();
    // The clock.
    int shownDay = int.MinValue, shownMinute = int.MinValue;
    bool shownNight;
    // The bench strip's state chip (LAST ORDERS, CLOSED), in the small window's place.
    RectTransform stateChip;
    Image stateChipFace;
    TextMeshProUGUI stateChipText;
    CanvasGroup stateChipGroup;
    State chipState = State.Open;
    float chipShown, laidOutFold = -1f, laidOutChip = -1f;

    // The stock chips.
    RectTransform chips;
    Chip cupsChip, beansChip;

    sealed class Chip
    {
        public RectTransform rect, glowRect;
        public Image face, glow;
        public TextMeshProUGUI title, line;
        public bool showing;
        public float since = -10f;
        public int shownCount = int.MinValue;
    }

    PlayerInteractor player;
    CounterRepairView counterPhone;
    bool wasShown;

    // QUIET AT THE BENCH (checklist v2, 14 Oct 2026; his playtest 5: "the checklist is terrible"). While the close-up is up the
    // column folds into its strip over 0.2 s: the sign, the money's board, the stars and the window fade; the strip's small
    // window, the time and the small jar take over.
    const float QuietSeconds = .2f;
    float quietAlpha = 1f;
    /// <summary>The column's quiet words as they show now: 1 in the café, 0 at the bench (labs).</summary>
    public float QuietAlpha => quietAlpha;

    // ---- the labs' hooks (the HUD lab: photos at hours and in forms the lab can't reach by play) ----
    float labHour = float.NaN;
    bool labNight, labFold;

    // ------------------------------------------------------------------ what checks read

    /// <summary>True while the HUD is on screen (by day the column and the window; by night the window alone).</summary>
    public bool Showing => canvas != null && canvas.enabled && top != null && top.alpha > .5f;
    /// <summary>True while the window shows the night (the night's hour, "Monday night"), the column hidden.</summary>
    public bool ShowsNight => Showing && shownNight;
    /// <summary>Whether the bottom corner (the stock chips' group) is up: it steps aside for a conversation or the counter phone.</summary>
    public bool BottomShowing => Showing && bottom != null && bottom.alpha > .5f;
    /// <summary>Today's takings as the figure reads now ("$142"), and what it's counting toward.</summary>
    public string TakingsShown => money != null ? money.Figure : "";
    public int TakingsTarget => money != null ? money.Target : 0;
    /// <summary>The figure's size now (1: at rest; more during a kick).</summary>
    public float TakingsPunch => money != null ? money.Punch : 1f;
    /// <summary>The jar's fill in steps (one for every $24 taken today, ten at most): what the old stack's edges counted.</summary>
    public int EdgesShown => money != null ? Mathf.Max(0, money.FillSteps) : 0;
    /// <summary>True once there's money in the jar today.</summary>
    public bool BillShowing => money != null && money.BillShowing;
    /// <summary>The money object itself (the jar, the figure, the receipt, the flights): the labs read its state.</summary>
    public HudMoney Money => money;
    /// <summary>The sign's word for the state it is in: OPEN, LAST ORDERS or CLOSED (its card may still be turning: FaceShowing).</summary>
    public string SignWord => WordOf(state);
    /// <summary>The words on the face that faces the screen right now (during a turn, the old face until the card is edge-on).</summary>
    public string FaceShowing => FaceFacing() != null ? FaceFacing().word.text : "";
    public bool LastOrders => state == State.LastOrders;
    public bool SignClosed => state == State.Closed;
    /// <summary>The day and the time as the window's sill reads them ("Monday · Day 1   9:30 AM"; "Monday night   11:05 PM").</summary>
    public string ClockLine => window != null ? window.DayText.text + "   " + window.TimeText.text : "";
    /// <summary>The sill's room to spare between the day and the time, the time at its widest (reference pixels).</summary>
    public float ClockGap => window != null ? window.Gap : 0f;
    /// <summary>The day line's size now (22 px; 20 px for a long weekday and day number, never less).</summary>
    public float ClockLineSize => window != null ? window.DaySizeNow : 0f;
    /// <summary>How far the sun is through its day, 0 at opening to 1 at closing (the day's clock; the window draws it).</summary>
    public float SunProgress { get; private set; }
    /// <summary>True once the sun has gone down behind the city (after closing).</summary>
    public bool SunSet => window != null && window.SunSet;
    /// <summary>True while the moon is up in the window (from closing).</summary>
    public bool MoonShowing => window != null && window.MoonShowing;
    /// <summary>How far the sky has warmed toward golden hour: 0 until the last hour, 1 at closing.</summary>
    public float DialWarmth => window != null ? window.Warmth : 0f;
    /// <summary>The café's stars as the column draws them (0-5).</summary>
    public int StarsShown => Mathf.Max(0, shownStars);
    /// <summary>The goal's words as the HUD says them now: the toast or Day 1's hint while one is up ("Five stars to franchise"), else "".</summary>
    public string FranchiseLine => toastGroup != null && toastGroup.alpha > .5f ? toastText.text.Replace("\n", " · ") : "";
    /// <summary>True while the tiny café front sits at the end of the stars' road (the goal, with no words).</summary>
    public bool CafeFrontShowing => frontRect != null && frontRect.gameObject.activeInHierarchy && starsGroup != null && starsGroup.alpha > .5f;
    /// <summary>True while the column is folded into its strip at the bench.</summary>
    public bool BenchStrip => quietAlpha < .5f;
    /// <summary>The strip's state chip at the bench ("LAST ORDERS", "CLOSED"), or "" (in the café, or while the café is open).</summary>
    public string BenchChipText => stateChipGroup != null && stateChipGroup.alpha > .5f ? stateChipText.text : "";
    public bool CupsChipShowing => cupsChip != null && cupsChip.showing;
    public bool BeansChipShowing => beansChip != null && beansChip.showing;
    /// <summary>The chips' words as they read, each chip's two lines joined ("Low on cups · 7 left · Restock at closing | ...").</summary>
    public string ChipsText => (cupsChip != null && cupsChip.showing ? ChipWords(cupsChip) : "") + (beansChip != null && beansChip.showing ? " | " + ChipWords(beansChip) : "");
    /// <summary>The money (the jar and the figure on their board) in the column.</summary>
    public RectTransform CashRect => money != null ? money.Root : null;
    /// <summary>The sign (its chains, its swing and its card), top right.</summary>
    public RectTransform SignRect => sign;
    public RectTransform ChipsRect => chips;
    /// <summary>The window piece (the time), top centre.</summary>
    public RectTransform WindowRect => window != null ? window.Rect : null;
    /// <summary>The city the window draws now.</summary>
    public string CityShown => window != null && window.City != null ? window.City.Name : "";
    /// <summary>
    /// The column's bottom edge in reference pixels from the top of the screen, as it stands now: the whole column in the
    /// café (290), the strip at the bench (110). Whatever sits under the column (the bench's job card) starts below this.
    /// </summary>
    public float TopRightBottom => ColumnTop + Mathf.Lerp(ColumnBottom, StripHeight, Fold(1f - quietAlpha));

    // ------------------------------------------------------------------ making it

    void Awake()
    {
        Instance = this;
        Build();
        Juice.Paid += OnPaid;
    }

    void OnDestroy()
    {
        Juice.Paid -= OnPaid;
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("HUD corners (while playing)", null, SortingOrder);
        root = (RectTransform)canvas.transform;

        top = Stretch("Top (the window and the column)", root).gameObject.AddComponent<CanvasGroup>();
        bottom = Stretch("Bottom right (the chips)", root).gameObject.AddComponent<CanvasGroup>();
        foreach (CanvasGroup g in new[] { top, bottom }) { g.blocksRaycasts = false; g.interactable = false; }

        // The window, top centre.
        window = new HudWindow();
        window.Build("The window", (RectTransform)top.transform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -WindowTop), 1f, true);
        windowGroup = Quiet(window.Rect);

        // The column, top right; the flights over everything.
        column = Box("The column", top.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ColumnRight, -ColumnTop), new Vector2(ColumnWidth, ColumnBottom));
        columnGroup = Quiet(column);
        flights = Stretch("Flights (bills, coins, sparkles)", top.transform);
        BuildStrip(column);
        BuildMoney(column);
        BuildStars(column);
        BuildSign(column);   // after the money, so the sign and its shadow draw over the board's rings
        BuildToast((RectTransform)top.transform);
        BuildChips((RectTransform)bottom.transform);
        flights.SetAsLastSibling();
        Layout(0f);
    }

    static CanvasGroup Quiet(Component on)
    {
        if (on == null) return null;
        CanvasGroup g = on.gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.interactable = false;
        return g;
    }

    // The sign: a brass bracket along the top, two chains, then the swing (the sway), the card's turn (the flip), the two faces.
    void BuildSign(RectTransform parent)
    {
        sign = Box("The sign", parent, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(SignWidth, StringLength + SignHeight));
        signGroup = Quiet(sign);

        // The bracket: a brass bar across the column's top from a post at the right edge, a scroll at its end.
        RectTransform bar = Box("Bracket", sign, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(11f, 2f), new Vector2(ColumnWidth - 22f, 5f));
        UiSkin.Paint(bar, Brass, 2.5f);
        RectTransform barLight = Box("Bracket light", bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(ColumnWidth - 26f, 1.5f));
        UiSkin.Paint(barLight, BrassLight, .7f);
        RectTransform post = Box("Post", sign, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(ColumnWidth * .5f - SignWidth * .5f, 10f), new Vector2(7f, 30f));
        UiSkin.Paint(post, Brass, 3f);
        RectTransform postDark = Box("Post shade", post, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(2.5f, 30f));
        UiSkin.Paint(postDark, BrassDark, 1f);
        RectTransform scroll = Box("Scroll", sign, new Vector2(0f, 1f), new Vector2(.5f, .5f), new Vector2(-ColumnWidth * .5f + SignWidth * .5f + 22f, -2f), new Vector2(22f, 22f));
        Image scrollImage = scroll.gameObject.AddComponent<Image>();
        scrollImage.sprite = HudArt.Scroll();
        scrollImage.color = Brass;
        scrollImage.raycastTarget = false;
        foreach (float x in new[] { -.23f, .23f })
        {
            RectTransform chain = Box("String", sign, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(SignWidth * x, -3f), new Vector2(3.5f, StringLength + 6f));
            UiSkin.Paint(chain, BrassDark, 1.5f);
            RectTransform link = Box("Link", sign, new Vector2(.5f, 1f), new Vector2(.5f, .5f), new Vector2(SignWidth * x, -StringLength + 1f), new Vector2(9f, 9f));
            Image linkImage = link.gameObject.AddComponent<Image>();
            linkImage.sprite = HudArt.Ring(16, 2.2f);
            linkImage.color = Brass;
            linkImage.raycastTarget = false;
        }

        // The swing hangs from the chains' ends; the flip turns the card about its middle. ("Swing" by name: the pause lab reads it.)
        sway = Box("Swing", sign, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -StringLength), new Vector2(SignWidth, SignHeight));
        flipper = Box("Turn", sway, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        signShadow = UiSkin.DropShadow(flipper, 9f, 5f, .4f);
        faceA = MakeFace("Face A", flipper);
        faceB = MakeFace("Face B", flipper);
        faceB.rect.gameObject.SetActive(false);
        shown = faceA;
        Dress(faceA, State.Open);
    }

    Face MakeFace(string name, RectTransform parent)
    {
        var f = new Face();
        f.rect = Box(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        f.border = UiSkin.Paint(f.rect, UiSkin.Brand, SignRadius);
        RectTransform inner = Box("Face", f.rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(SignWidth - 2f * Border, SignHeight - 2f * Border));
        f.face = UiSkin.Paint(inner, UiSkin.Paper, SignRadius - Border);
        // The enamel's lit lip: the light from the top left catches the paint's top and left edges.
        RectTransform lipTop = Box("Lip", inner, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -1.5f), new Vector2(SignWidth - 2f * Border - 2f * SignRadius, 2f));
        f.lipTop = UiSkin.Paint(lipTop, Color.white, 1f);
        RectTransform lipLeft = Box("Lip", inner, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(1.5f, 0f), new Vector2(2f, SignHeight - 2f * Border - 2f * SignRadius));
        f.lipLeft = UiSkin.Paint(lipLeft, Color.white, 1f);
        RectTransform cupRect = Box("Cup", f.rect, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(26f, 22f));
        f.cup = cupRect.gameObject.AddComponent<Image>();
        f.cup.sprite = HudArt.CupMark();
        f.cup.raycastTarget = false;
        f.word = UiSkin.Text("Word", f.rect, 40f, UiSkin.Brand, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(f.word.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 1f), new Vector2(SignWidth - 16f, 46f));
        // The line under the word, 22 px in sentence case (her: "your text needs to be bolder"; 26 Oct: up from 20, the HUD's floor).
        // 26 Oct 2026 (Readability): 24 px and semibold ("Come in" was the gather's most-seen small line, in 165 photos).
        f.sub = UiSkin.Text("Under", f.rect, 24f, UiSkin.Brand, TextAlignmentOptions.Center, UiSkin.SemiBold);
        Place(f.sub.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 6f), new Vector2(SignWidth - 16f, 28f));
        return f;
    }

    // The money: the jar on its board, hung from the sign on two brass rings.
    void BuildMoney(RectTransform parent)
    {
        // Hung from its top middle (it swings a beat behind the sign at last orders).
        RectTransform hanger = Box("Money", parent, new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(-HudMoney.BoardW * .5f, -BoardTop), new Vector2(HudMoney.BoardW, HudMoney.BoardH));
        moneyGroup = Quiet(hanger);
        foreach (float x in new[] { 34f, HudMoney.BoardW - 34f })
        {
            RectTransform ring = Box("Ring", hanger, new Vector2(0f, 1f), new Vector2(.5f, .5f), new Vector2(x, RingGap * .5f + 1f), new Vector2(14f, 14f));
            Image r = ring.gameObject.AddComponent<Image>();
            r.sprite = HudArt.Ring(16, 2.4f);
            r.color = Brass;
            r.raycastTarget = false;
        }
        money = new HudMoney();
        money.Build("The jar and the figure", hanger, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, flights);
    }

    // The café's stars: five pips, a dotted road, the tiny café front at its end; the glint that runs the road when a star lands.
    void BuildStars(RectTransform parent)
    {
        stars = Box("The stars", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -StarsTop), new Vector2(ColumnWidth, FrontH));
        starsGroup = Quiet(stars);
        Sprite star = HudArt.Star(), glowSprite = HudArt.Glow();
        float x = -Pad - FrontW - RoadGap - 3f * 7f;   // the front, the road, then the stars leftward
        for (int i = starImages.Length - 1; i >= 0; i--)
        {
            x -= StarSize * .5f;
            RectTransform g = Box("Star glow " + (i + 1), stars, new Vector2(1f, 1f), new Vector2(.5f, .5f), new Vector2(x, -FrontH * .5f), new Vector2(StarSize * 2.6f, StarSize * 2.6f));
            starGlows[i] = g.gameObject.AddComponent<Image>();
            starGlows[i].sprite = glowSprite;
            starGlows[i].color = HudArt.With(UiSkin.Gold, 0f);
            starGlows[i].raycastTarget = false;
            RectTransform r = Box("Star " + (i + 1), stars, new Vector2(1f, 1f), new Vector2(.5f, .5f), new Vector2(x, -FrontH * .5f), new Vector2(StarSize, StarSize));
            starImages[i] = r.gameObject.AddComponent<Image>();
            starImages[i].sprite = star;
            starImages[i].raycastTarget = false;
            starImages[i].color = new Color(UiSkin.BandFaint.r, UiSkin.BandFaint.g, UiSkin.BandFaint.b, .3f);
            x -= StarSize * .5f + StarGap;
        }
        for (int d = 0; d < 3; d++)
        {
            RectTransform dot = Box("Road dot", stars, new Vector2(1f, 1f), new Vector2(.5f, .5f), new Vector2(-Pad - FrontW - RoadGap - 3.5f - d * 7f, -FrontH * .5f), new Vector2(3f, 3f));
            Image di = dot.gameObject.AddComponent<Image>();
            di.sprite = UiSkin.Circle;
            di.color = new Color(UiSkin.BandFaint.r, UiSkin.BandFaint.g, UiSkin.BandFaint.b, .55f);
            di.raycastTarget = false;
        }
        RectTransform frontGlowRect = Box("Front glow", stars, new Vector2(1f, 1f), new Vector2(.5f, .5f), new Vector2(-Pad - FrontW * .5f, -FrontH * .5f), new Vector2(FrontW * 3f, FrontH * 3f));
        frontGlow = frontGlowRect.gameObject.AddComponent<Image>();
        frontGlow.sprite = glowSprite;
        frontGlow.color = HudArt.With(UiSkin.Gold, 0f);
        frontGlow.raycastTarget = false;
        frontRect = Box("Cafe front", stars, new Vector2(1f, 1f), new Vector2(.5f, 0f), new Vector2(-Pad - FrontW * .5f, -FrontH), new Vector2(FrontW, FrontH));
        Picture("Dark", frontRect, HudArt.CafeFront(false));
        frontLit = Picture("Lit", frontRect, HudArt.CafeFront(true));
        frontLit.color = new Color(1f, 1f, 1f, 0f);
        RectTransform glintRect = Box("Glint", stars, new Vector2(1f, 1f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(12f, 12f));
        glint = glintRect.gameObject.AddComponent<Image>();
        glint.sprite = HudArt.Sparkle();
        glint.color = HudArt.With(UiSkin.Gold, 0f);
        glint.raycastTarget = false;
    }

    static Image Picture(string name, RectTransform parent, Sprite sprite)
    {
        RectTransform r = Box(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = sprite;
        i.raycastTarget = false;
        return i;
    }

    // The toast under the column, in the Now line's family: the ink band with a gold bar; the title in amber over a cream line.
    void BuildToast(RectTransform parent)
    {
        toast = Box("Toast (the goal)", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ColumnRight, -(ColumnTop + ColumnBottom + 10f)), new Vector2(ToastWidth, 64f));
        UiSkin.Paint(toast, UiSkin.Band, UiSkin.Radius);
        var shadow = toast.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .35f);
        shadow.effectDistance = new Vector2(0f, -3f);
        RectTransform edge = Box("Edge", toast, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(8f, 0f), new Vector2(5f, 46f));
        UiSkin.Paint(edge, UiSkin.Gold, 3f);
        toastText = UiSkin.Text("Words", toast, ToastLineSize, UiSkin.BandText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, true);
        toastText.rectTransform.anchorMin = Vector2.zero; toastText.rectTransform.anchorMax = Vector2.one;
        toastText.rectTransform.offsetMin = new Vector2(24f, 8f); toastText.rectTransform.offsetMax = new Vector2(-16f, -8f);
        toastGroup = Quiet(toast);
        toastGroup.alpha = 0f;
        toast.gameObject.SetActive(false);
    }

    // The bench strip: the small window (or the state chip in its place), the time, and the money small; laid out by Layout.
    void BuildStrip(RectTransform parent)
    {
        miniWindow = new HudWindow();
        miniWindow.Build("Small window", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(StripPad, -StripPad - 2f), MiniWindowScale, false);
        miniWindowRect = miniWindow.Rect;
        miniGroup = Quiet(miniWindowRect);
        miniGroup.alpha = 0f;
        stripTime = UiSkin.Text("Strip time", parent, StripTimeSize, HudWindow.Gilt, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(stripTime.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -StripPad), new Vector2(ColumnWidth - 2f * Pad - 130f, ChipRow));
        UiSkin.Shadowed(stripTime);
        stripTime.alpha = 0f;
        stateChip = Box("State chip (the bench)", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(StripPad + 2f, -StripPad - 16f), new Vector2(150f, ChipRow));
        stateChipFace = UiSkin.Paint(stateChip, Honey, ChipRow * .5f);
        stateChipText = UiSkin.Text("Word", stateChip, ChipWordSize, UiSkin.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchInside(stateChipText.rectTransform, 0f);
        stateChipGroup = Quiet(stateChip);
        stateChipGroup.alpha = 0f;
    }

    void BuildChips(RectTransform parent)
    {
        chips = Box("Stock", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-UiSkin.Margin, 26f), new Vector2(360f, 2f * StockPitch));
        cupsChip = MakeChip("Cups", 0);
        beansChip = MakeChip("Beans", 1);
    }

    // A stock chip: what's low (24 px bold) over when to buy (22 px), on Ember (or the sign's plum once it's out).
    Chip MakeChip(string name, int row)
    {
        var chip = new Chip();
        chip.rect = Box(name, chips, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, row * StockPitch), new Vector2(300f, StockHeight));
        chip.face = UiSkin.Paint(chip.rect, Ember, 16f);
        chip.glow = UiSkin.DropShadow(chip.rect, 12f, 3f, .3f);
        chip.glowRect = chip.glow.rectTransform;
        chip.title = UiSkin.Text("Words", chip.rect, StockTitleSize, UiSkin.Paper, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(chip.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(268f, 28f));
        // Bold as well (paper on the old accent read 3.6:1; on Ember it is 5.6).
        chip.line = UiSkin.Text("When", chip.rect, StockLineSize, UiSkin.Paper, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        chip.line.text = "Restock at closing";
        Place(chip.line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -34f), new Vector2(268f, 26f));
        chip.rect.gameObject.SetActive(false);
        chip.glowRect.gameObject.SetActive(false);
        return chip;
    }

    // ------------------------------------------------------------------ every frame

    void LateUpdate()
    {
        if (canvas == null) return;
        DayClock clock = DayClock.Instance;
        bool night = NightWalk.Instance != null && NightWalk.Instance.Active;
        bool blocked = RecapUI.Showing || UiPins.DayIntroPlaying;
        // The day intro (13 Oct 2026, Cinematics): the corners wait while it plays (UiPins reads DayIntro.Playing by name).
        bool day = clock != null && !clock.DayOver && !night && !blocked;
        // By night the window alone, with the night's hour (his answer, 24 Oct: the time stays top centre).
        bool show = day || (night && !blocked && clock != null) || labNight;
        if (canvas.enabled != show) canvas.enabled = show;
        if (!show)
        {
            top.alpha = 0f;
            bottom.alpha = 0f;
            wasShown = false;
            if (UiPins.DayIntroPlaying && shown != null)
            {
                // Under the intro the sign is CLOSED (as the night left it), so that it flips over to OPEN when the HUD returns.
                if (!stateKnown || state != State.Closed) { state = State.Closed; stateKnown = true; ShowState(State.Closed, false); }
                waitedForTheIntro = true;
            }
            return;
        }
        float dt = UiClock.Delta, now = UiClock.Now;
        top.alpha = 1f;
        bool nightLook = labNight || (night && !day);
        columnGroup.alpha = nightLook ? 0f : 1f;
        bool busy = nightLook || BottomBusy();
        bottom.alpha = wasShown ? Mathf.MoveTowards(bottom.alpha, busy ? 0f : 1f, dt * FadeRate) : busy ? 0f : 1f;
        wasShown = true;
        // At the bench the column folds into its strip (see QuietSeconds); its state chip comes and goes at the same pace.
        bool folded = ItemInspector.Working || labFold;
        quietAlpha = Mathf.MoveTowards(quietAlpha, folded ? 0f : 1f, dt / QuietSeconds);
        chipShown = Mathf.MoveTowards(chipShown, chipState == State.Open ? 0f : 1f, dt / QuietSeconds);

        UpdateClock(clock, night, nightLook);
        if (!nightLook)
        {
            UpdateSign(clock, now, dt);
            UpdateCash(clock, now);
            UpdateStars(clock, now);
            UpdateToast(clock, now);
        }
        window.Tick(now);
        miniWindow.Tick(now);
        money.Tick(now, dt);
        Layout(1f - quietAlpha);
        if (!nightLook) UpdateChips(now);
    }

    // A conversation or the counter phone has the bottom of the screen.
    bool BottomBusy()
    {
        if (ConversationController.AnyOpen) return true;
        if (player == null)
        {
            PlayerCarry carry = PlayerCarry.Instance;
            if (carry != null) player = carry.GetComponent<PlayerInteractor>();
            if (player != null) counterPhone = player.GetComponent<CounterRepairView>();
        }
        return counterPhone != null && counterPhone.OwnsInput;
    }

    // ------------------------------------------------------------------ the column's two forms

    // 0 in the café, 1 at the bench, eased.
    static float Fold(float q) => q * q * (3f - 2f * Mathf.Clamp01(q));

    // Where everything sits, between the café's column (fold 0) and the bench's strip (fold 1). Run every frame; it does
    // nothing when neither the fold nor the chip has changed.
    void Layout(float q)
    {
        float e = Fold(Mathf.Clamp01(q));
        // The state chip's share of the strip: only at the bench, only at last orders and after closing.
        float k = e * chipShown;
        if (Mathf.Approximately(e, laidOutFold) && Mathf.Approximately(k, laidOutChip)) return;
        laidOutFold = e;
        laidOutChip = k;
        float cafe = 1f - e;
        // The column's pieces fade with the café's form; the strip's with the bench's.
        signGroup.alpha = cafe;
        starsGroup.alpha = cafe;
        miniGroup.alpha = e * (1f - k);
        stripTime.alpha = e;
        stateChipGroup.alpha = k;
        window.Rect.localScale = Vector3.one * Mathf.Lerp(1f, .96f, e);
        windowGroup.alpha = cafe;
        bool miniOn = e > .01f;
        if (miniWindowRect.gameObject.activeSelf != miniOn) miniWindowRect.gameObject.SetActive(miniOn);
        if (stateChip.gameObject.activeSelf != k > .01f) stateChip.gameObject.SetActive(k > .01f);
        // The money: on its board under the sign in the café; small in the strip's second row at the bench.
        money.SetCompact(e);
        RectTransform hanger = (RectTransform)money.Root.parent;
        float hangerWidth = Mathf.Lerp(HudMoney.BoardW, ColumnWidth - 150f, e);
        hanger.sizeDelta = new Vector2(hangerWidth, HudMoney.BoardH);
        hanger.anchoredPosition = new Vector2(-hangerWidth * .5f, -Mathf.Lerp(BoardTop, StripPad + ChipRow - 8f, e));
        money.Root.sizeDelta = hanger.sizeDelta;
        moneyGroup.alpha = 1f;
        foreach (Transform child in hanger) if (child.name == "Ring") child.gameObject.SetActive(cafe > .5f);
    }

    // ------------------------------------------------------------------ the takings

    void OnPaid(Transform who, int amount, int tip, float delay)
    {
        pops.Add(new Pop { who = who, amount = amount, tip = tip, at = UiClock.Now + Mathf.Max(0f, delay) });
        if (pops.Count > 6) pops.RemoveAt(0);
    }

    void UpdateCash(DayClock clock, float now)
    {
        int earned = Mathf.Max(0, clock.Earned);
        int patrons = Mathf.Max(0, clock.PatronIncome);
        int tips = Mathf.Max(0, clock.Tips);
        if (targetMoney == int.MinValue || earned < targetMoney)
        {
            // The first look, or a new morning (the jar empties): as it is, no counting.
            targetMoney = earned;
            money.Show(earned);
            patronIncomeSeen = patrons;
            tipsSeen = tips;
            pops.Clear();
        }
        else if (earned > targetMoney)
        {
            // Paid: the bill flies in from the pay pop over the customer and lands with a beat; a café guest's coffee
            // counts up quietly instead (playtest 4: "+$5" pops with nobody served).
            int quiet = Mathf.Clamp(patrons - patronIncomeSeen, 0, earned - targetMoney);
            int paid = earned - targetMoney - quiet;
            int tip = Mathf.Clamp(tips - tipsSeen, 0, paid);
            if (paid > 0)
            {
                Vector2? from = null;
                float popAt = now;
                for (int i = pops.Count - 1; i >= 0; i--)
                {
                    Pop p = pops[i];
                    if (p.at < now - 1.5f) { pops.RemoveAt(i); continue; }
                    if (p.amount + p.tip != paid) continue;
                    from = PopScreenPoint(p.who);
                    popAt = p.at;
                    pops.RemoveAt(i);
                    break;
                }
                money.Pay(earned, paid - tip, tip, from, popAt, now);
            }
            else money.Quiet(earned, now);
            targetMoney = earned;
        }
        patronIncomeSeen = patrons;
        tipsSeen = tips;
    }

    // Where the pay pop sits over someone's head (Juice: the head point, 84 px right and 34 up), in screen pixels; off
    // screen or behind the camera, the screen's edge nearest them, 40 px inside.
    static Vector2? PopScreenPoint(Transform who)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 head = who != null ? Barks.HeadPointOf(who) : cam.transform.position + cam.transform.forward * 5f;
        Vector3 screen = cam.WorldToScreenPoint(head);
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        Vector2 p = new Vector2(screen.x + 84f * scale, screen.y + 34f * scale);
        if (screen.z < 0f) p = new Vector2(Screen.width - p.x, Screen.height - p.y);
        p.x = Mathf.Clamp(p.x, 40f * scale, Screen.width - 40f * scale);
        p.y = Mathf.Clamp(p.y, 40f * scale, Screen.height - 40f * scale);
        return p;
    }

    // ------------------------------------------------------------------ the stars

    // The café's stars as the ledger holds them; a star earned while the column is up lands: it drops into its pip, a glint
    // runs the road, and the café front jumps and lights up (M2 §5).
    void UpdateStars(DayClock clock, float now)
    {
        ReputationLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Reputation : null;
        int earned = ledger != null ? Mathf.Clamp(ledger.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        if (earned != shownStars)
        {
            bool landing = shownStars >= 0 && earned > shownStars;
            shownStars = earned;
            for (int i = 0; i < starImages.Length; i++)
                starImages[i].color = i < earned ? UiSkin.Gold : new Color(UiSkin.BandFaint.r, UiSkin.BandFaint.g, UiSkin.BandFaint.b, .3f);
            if (landing)
            {
                starLandAt = now;
                Sfx.Play2D("hud.star.land");
                int left = ReputationRules.MaxStars - earned;
                Toast(now, "<color=" + UiSkin.HexOf(UiSkin.Gold) + "><size=" + ToastTitleSize + ">New star!</size></color>\n"
                    + (left > 0 ? left + " more star" + (left == 1 ? "" : "s") + " to franchise" : "Five stars: the franchise offer is coming"), ToastSeconds, .75f);
            }
        }
        float t = now - starLandAt;
        int pip = Mathf.Clamp(shownStars - 1, 0, starImages.Length - 1);
        if (starLandAt > -5f && t < 1.3f)
        {
            // The drop: from 46 px above, 1.9x to 1x (OutQuad 245 ms); one overshoot to 1.26 at 131 ms after, settled by 700 ms.
            RectTransform r = starImages[pip].rectTransform;
            float drop = Mathf.Clamp01(t / .245f);
            float e = UiTween.Eased(UiTween.Ease.OutQuad, drop);
            float scale = Mathf.Lerp(1.9f, 1f, e);
            if (t > .245f) { float tau = t - .245f; scale = 1f + .26f * Mathf.Exp(-6f * tau) * Mathf.Sin(tau * 12f); }
            r.localScale = new Vector3(scale, scale, 1f);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, -FrontH * .5f + 46f * (1f - e));
            float bloom = t < .7f ? Mathf.Sin(Mathf.Clamp01((t - .2f) / .5f) * Mathf.PI) : 0f;
            starGlows[pip].color = HudArt.With(UiSkin.Gold, .7f * bloom);
            // The glint runs the road (450-850 ms), then the café front jumps and lights up.
            float run = Mathf.Clamp01((t - .45f) / .4f);
            if (t >= .45f && t < .9f)
            {
                Vector2 a = starImages[pip].rectTransform.anchoredPosition, b = new Vector2(-Pad - FrontW * .5f, -FrontH * .5f);
                glint.rectTransform.anchoredPosition = Vector2.Lerp(a, b, run);
                glint.color = HudArt.With(UiSkin.Gold, Mathf.Sin(run * Mathf.PI));
            }
            else glint.color = HudArt.With(UiSkin.Gold, 0f);
            if (t >= .85f)
            {
                float tau = t - .85f;
                float kick = 1f + .55f * Mathf.Exp(-8f * tau) * Mathf.Sin(22f * tau);
                frontRect.localScale = new Vector3(kick, kick, 1f);
                frontLit.color = new Color(1f, 1f, 1f, Mathf.Clamp01(tau / .25f));
                frontGlow.color = HudArt.With(UiSkin.Gold, .8f * (1f - Mathf.Clamp01(tau / 1f)));
            }
        }
        else
        {
            starImages[pip].rectTransform.localScale = Vector3.one;
            starImages[pip].rectTransform.anchoredPosition = new Vector2(starImages[pip].rectTransform.anchoredPosition.x, -FrontH * .5f);
            starGlows[pip].color = HudArt.With(UiSkin.Gold, 0f);
            glint.color = HudArt.With(UiSkin.Gold, 0f);
            frontRect.localScale = Vector3.one;
            frontGlow.color = HudArt.With(UiSkin.Gold, 0f);
            // The front stays lit once the café has a star.
            frontLit.color = new Color(1f, 1f, 1f, shownStars > 0 ? 1f : 0f);
        }
    }

    // ------------------------------------------------------------------ the toast

    void Toast(float now, string words, float seconds, float delay)
    {
        toastText.text = words;
        toastShownAt = now + delay;
        toastLife = seconds;
        Vector2 size = toastText.GetPreferredValues(words, ToastWidth - 40f, 0f);
        toast.sizeDelta = new Vector2(Mathf.Min(ToastWidth, Mathf.Ceil(size.x) + 42f), Mathf.Max(46f, Mathf.Ceil(size.y) + 18f));
    }

    void UpdateToast(DayClock clock, float now)
    {
        // Day 1's hint, once: the goal said once, for 6 s, as the column first shows (his chosen set: Day 1's card, a 6 s
        // hint, the star toasts, the phone).
        if (!hintShown && clock.Day == 1 && clock.IsOpen)
        {
            hintShown = true;
            Toast(now, "Five stars to franchise", HintSeconds, .6f);
        }
        if (toastShownAt < -5f) return;
        float t = now - toastShownAt;
        if (t < 0f) return;
        if (!toast.gameObject.activeSelf) toast.gameObject.SetActive(true);
        float rise = UiTween.Eased(UiTween.Ease.OutBack, Mathf.Clamp01(t / .25f));
        float fade = t > toastLife - .3f ? Mathf.Clamp01((toastLife - t) / .3f) : 1f;
        toastGroup.alpha = Mathf.Min(rise, fade) * quietAlpha;
        toast.anchoredPosition = new Vector2(-ColumnRight, -(ColumnTop + ColumnBottom + 10f) + 10f * (1f - rise));
        if (t >= toastLife) { toastShownAt = -10f; toast.gameObject.SetActive(false); toastGroup.alpha = 0f; }
    }

    // ------------------------------------------------------------------ the sign

    void UpdateSign(DayClock clock, float now, float dt)
    {
        // Last orders: the café's last hour, by its own clock.
        float lastHour = Mathf.Max(0f, clock.ClosingHour - 1f);
        State want = !clock.IsOpen ? State.Closed : clock.CurrentHour >= lastHour ? State.LastOrders : State.Open;
        if (!stateKnown || want != state)
        {
            bool animate = waitedForTheIntro || stateKnown && !(state == State.Closed && want != State.Closed);   // a new morning just turns back (after the intro: it flips)
            waitedForTheIntro = false;
            State was = state;
            state = want;
            stateKnown = true;
            ShowState(want, animate);
            if (animate && want == State.LastOrders && was == State.Open) Sfx.Play2D("hud.lastorders");
            if (animate && want == State.Closed)
            {
                // The sign's own wooden flap (the intro's cue) as it turns to CLOSED; the day's bell rings from DayClock.
                Sfx.Play2D("intro.sign");
                closedAt = now;
            }
        }

        // The turn: a half turn for every change of face, on its own piece.
        if (flipSince > -5f)
        {
            float t = Mathf.Clamp01((now - flipSince) / FlipSeconds);
            angle = Mathf.LerpUnclamped(flipFrom, flipTo, BackOut(t));
            if (t >= 1f) { flipSince = -10f; angle = Mathf.Repeat(flipTo, 360f); }
        }
        float face = Mathf.Cos(angle * Mathf.Deg2Rad);
        bool aShowing = face >= 0f;
        if (faceA.rect.gameObject.activeSelf != aShowing) faceA.rect.gameObject.SetActive(aShowing);
        if (faceB.rect.gameObject.activeSelf == aShowing) faceB.rect.gameObject.SetActive(!aShowing);
        flipper.localScale = new Vector3(Mathf.Max(.02f, Mathf.Abs(face)), 1f, 1f);
        UiSkin.Follow(signShadow.rectTransform, flipper, 9f, 5f);

        // The sway, on the swing: gentle when open, quicker and wider at last orders, still once closed.
        float amplitude = state == State.Open ? 1f : state == State.LastOrders ? 2.8f : 0f;
        float period = state == State.LastOrders ? 1.15f : 5.5f;
        swayPhase += dt * Mathf.PI * 2f / period;
        float wanted = amplitude * Mathf.Sin(swayPhase);
        float current = sway.localEulerAngles.z > 180f ? sway.localEulerAngles.z - 360f : sway.localEulerAngles.z;
        sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(current, wanted, 1f - Mathf.Exp(-10f * dt)));
        // The money's board swings a beat (90 ms) behind the sign at last orders: hung from it, it follows.
        if (money != null && money.Root != null && money.Root.parent != null)
        {
            float lag = amplitude * .35f * Mathf.Sin(swayPhase - .09f * Mathf.PI * 2f / period);
            ((RectTransform)money.Root.parent).localRotation = Quaternion.Euler(0f, 0f, lag);
        }
        // The bounce at closing: the card grows from its chains and settles, half a second.
        float bounce = now - closedAt < SignPunchSeconds ? 1f + .08f * Mathf.Sin(Mathf.Clamp01((now - closedAt) / SignPunchSeconds) * Mathf.PI) : 1f;
        sway.localScale = new Vector3(bounce, bounce, 1f);

        // The closing hour's words follow the clock (a scene may change it).
        int closing = Mathf.RoundToInt(clock.ClosingHour * 60f);
        if (closing != shownClosingHour)
        {
            shownClosingHour = closing;
            if (shown.state == State.LastOrders) shown.sub.text = "Closing at " + ClosingWords(clock.ClosingHour);
        }
    }

    // The faces are set BEFORE the card turns: the hidden face is dressed with the new words, then the card makes a half
    // turn to show it. So the turn never shows a stale face (the reviewers saw "OPEN · COME IN" at 8:00 PM, mid-turn).
    void ShowState(State s, bool animate)
    {
        if (animate)
        {
            // The face the screen can't see gets the words (even mid-turn: the face facing the screen never changes under
            // the player's eyes); then the card turns on to the next angle at which that face lies flat to the screen.
            Face hidden = FaceFacing() == faceA ? faceB : faceA;
            Dress(hidden, s);
            shown = hidden;
            flipFrom = angle;
            flipTo = hidden == faceB ? 180f : 0f;
            while (flipTo <= angle + 1f) flipTo += 360f;
            flipSince = UiClock.Now;
        }
        else
        {
            Dress(shown, s);
            flipSince = -10f;
            angle = shown == faceA ? 0f : 180f;
        }
        chipState = s;
        if (stateChipText != null)
        {
            stateChipText.text = WordOf(s);
            stateChipText.color = s == State.Closed ? UiSkin.Paper : UiSkin.Ink;
            stateChipFace.color = s == State.Closed ? Plum : Honey;
            float width = Mathf.Ceil(stateChipText.GetPreferredValues(stateChipText.text, 999f, ChipRow).x) + 2f * ChipPadX;
            stateChip.sizeDelta = new Vector2(width, ChipRow);
            // Its coming and going is chipShown (LateUpdate), eased in Layout, so the small window makes way smoothly.
        }
    }

    // One face's words and colours for a state.
    void Dress(Face f, State s)
    {
        f.state = s;
        DayClock clock = DayClock.Instance;
        switch (s)
        {
            case State.LastOrders:
                f.border.color = UiSkin.GoldInk;
                f.face.color = Honey;
                f.word.text = "LAST ORDERS";
                f.word.fontSize = 28f;
                f.word.characterSpacing = 3f;
                f.word.color = UiSkin.Ink;
                f.sub.text = "Closing at " + ClosingWords(clock != null ? clock.ClosingHour : 20f);
                f.sub.color = UiSkin.Ink;
                f.cup.enabled = false;
                f.word.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                break;
            case State.Closed:
                f.border.color = PlumRim;
                f.face.color = Plum;
                f.word.text = "CLOSED";
                f.word.fontSize = 36f;
                f.word.characterSpacing = 8f;
                f.word.color = UiSkin.Paper;
                f.sub.text = "See you tomorrow";
                f.sub.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .85f);
                f.cup.enabled = false;
                f.word.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                break;
            default:
                f.border.color = UiSkin.Brand;
                f.face.color = UiSkin.Paper;
                f.word.text = "OPEN";
                f.word.fontSize = 40f;
                f.word.characterSpacing = 10f;
                f.word.color = UiSkin.Brand;
                f.sub.text = "Come in";
                // 20 Oct (Review 2): the brand green at 85 % on the paper was 3.5:1 for 20 px words; the deep green is 7:1.
                f.sub.color = UiSkin.BrandDeep;
                f.cup.enabled = true;
                f.cup.color = UiSkin.Brand;
                f.word.rectTransform.anchoredPosition = new Vector2(0f, 1f);
                break;
        }
        // The lit lip on the paint: white at a touch over the paper, warmer over the honey and the plum.
        Color lip = Color.Lerp(f.face.color, Color.white, .55f);
        f.lipTop.color = new Color(lip.r, lip.g, lip.b, .7f);
        f.lipLeft.color = new Color(lip.r, lip.g, lip.b, .45f);
    }

    static string WordOf(State s) => s == State.Closed ? "CLOSED" : s == State.LastOrders ? "LAST ORDERS" : "OPEN";

    Face FaceFacing() => faceA == null ? null : Mathf.Cos(angle * Mathf.Deg2Rad) >= 0f ? faceA : faceB;

    static string ClosingWords(float hour)
    {
        string time = ShopUI.FormatHour(hour);
        return time.EndsWith(":00 PM") || time.EndsWith(":00 AM") ? time.Replace(":00", "") : time;
    }

    // Past the end and back (a flip with weight).
    static float BackOut(float t)
    {
        const float s = 1.4f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    // ------------------------------------------------------------------ the day and the time

    // THE DAY AND THE TIME on the window's sill (her playtest 7: "what day, what time? Oh, this is way too small."): the time 32
    // px bold in gilt at the sill's left, the day 22 px in mint at its right (20 px only when a long weekday and day number
    // wouldn't leave the gap, never less); both measured, the time at its widest, and the room to spare is what the pause lab
    // checks. By night the night's hour and "Monday night". Built only when the minute changes.
    void UpdateClock(DayClock clock, bool night, bool nightLook)
    {
        float hour;
        string label;
        if (!float.IsNaN(labHour)) { hour = labHour; label = labNight ? NightLabel(clock) : Weekdays.Label(clock != null ? clock.Day : 1); }
        else if (night && NightWalk.Instance != null) { hour = NightWalk.Instance.ClockHour; label = NightLabel(clock); }
        else { hour = clock != null ? clock.CurrentHour : 9f; label = Weekdays.Label(clock != null ? clock.Day : 0); }
        int minute = Mathf.FloorToInt(Mathf.Clamp(hour, 0f, 24f) * 60f + .001f);
        int day = clock != null ? clock.Day : 0;
        if (day != shownDay || minute != shownMinute || nightLook != shownNight)
        {
            shownDay = day;
            shownMinute = minute;
            shownNight = nightLook;
            string time = ShopUI.FormatHour(hour);
            window.SetTime(time);
            if (stripTime.text != time) stripTime.text = time;
        }
        window.Set(hour, label, nightLook);
        miniWindow.Set(hour, label, nightLook);
        SunProgress = clock != null ? Mathf.Clamp01(clock.NormalizedDay) : 0f;
    }

    // "Thursday night" (the night after Thursday's day: Day 1 is a Monday), or "Night" before the first day.
    static string NightLabel(DayClock clock)
    {
        string name = Weekdays.Name(clock != null ? clock.Day : 0);
        return name.Length > 0 ? name + " night" : "Night";
    }

    /// <summary>The room the sill would have to spare with <paramref name="dayLabel"/> on the day line and <paramref name="time"/>
    /// beside it (checks: every weekday). The lines on screen are left as they are.</summary>
    public float GapFor(string dayLabel, string time) => window != null ? window.GapForDay(dayLabel, time) : 0f;

    // ------------------------------------------------------------------ the stock chips

    void UpdateChips(float now)
    {
        ShopInventory stock = ShopInventory.Instance;
        bool lab = CafeLab.Active;
        chips.anchoredPosition = new Vector2(-UiSkin.Margin, 26f + (lab ? LabNudge : 0f));
        int row = 0;
        // 24 Oct 2026 (Days; restock by hand, P's §2.5): the machine at 3 or fewer with stock on the back shelf says so.
        bool cupsMachine = stock != null && !stock.CupsLow && stock.MachineCups <= Downtime.RefillAt && stock.ShelfCups > 0;
        bool beansMachine = stock != null && !stock.BeansLow && stock.MachineBeans <= Downtime.RefillAt && stock.ShelfBeans > 0;
        ChipFor(cupsChip, stock != null && (stock.CupsLow || cupsMachine), stock == null ? 0 : cupsMachine ? stock.MachineCups : stock.Cups, "cups", cupsMachine, now, ref row);
        ChipFor(beansChip, stock != null && (stock.BeansLow || beansMachine), stock == null ? 0 : beansMachine ? stock.MachineBeans : stock.Beans, "beans", beansMachine, now, ref row);
    }

    // What's low and how many are left, and under it when to buy (the Shop is on the phone at closing: her "Oh, I wouldn't
    // know"). Within WordBudget.BenchAlert (12 words), both lines together.
    void ChipFor(Chip chip, bool low, int count, string what, bool atMachine, float now, ref int row)
    {
        if (low != chip.showing)
        {
            chip.showing = low;
            chip.since = now;
            chip.rect.gameObject.SetActive(low);
            chip.glowRect.gameObject.SetActive(low);
            chip.shownCount = int.MinValue;
        }
        if (!low) return;
        int shownNow = atMachine ? -1000 - count : count;   // the machine's count and the whole stock's never read as the same
        if (shownNow != chip.shownCount)
        {
            chip.shownCount = shownNow;
            chip.title.text = atMachine ? $"Low at the machine · {count} {what}" : count <= 0 ? $"Out of {what}" : $"Low on {what} · {count} left";
            chip.line.text = atMachine ? "Refill from the back shelf" : "Restock at closing";
            chip.face.color = !atMachine && count <= 0 ? Plum : Ember;
            float width = Mathf.Max(Mathf.Ceil(chip.title.GetPreferredValues(chip.title.text, 999f, 28f).x),
                                    Mathf.Ceil(chip.line.GetPreferredValues(chip.line.text, 999f, 26f).x)) + 32f;
            chip.rect.sizeDelta = new Vector2(width, StockHeight);
            chip.title.rectTransform.sizeDelta = new Vector2(width - 32f, 28f);
            chip.line.rectTransform.sizeDelta = new Vector2(width - 32f, 26f);
        }
        // In from the right with a spring, then a slow glow.
        float t = Mathf.Clamp01((now - chip.since) / ChipInSeconds);
        float u = t - 1f;
        float spring = 1f + 2.4f * u * u * u + 1.4f * u * u;
        chip.rect.anchoredPosition = new Vector2(16f * (1f - spring), row * StockPitch);
        float s = Mathf.LerpUnclamped(.85f, 1f, spring);
        chip.rect.localScale = new Vector3(s, s, 1f);
        UiSkin.Follow(chip.glowRect, chip.rect, 12f, 3f);
        Color tint = chip.face.color;
        float glow = .22f + .28f * (.5f + .5f * Mathf.Sin(now * Mathf.PI * 2f / 1.7f));
        chip.glow.color = new Color(tint.r * .6f, tint.g * .4f, tint.b * .3f, glow * t);
        row++;
    }

    static string ChipWords(Chip chip) => chip.title.text + " · " + chip.line.text;

    // ------------------------------------------------------------------ the labs' hooks

    /// <summary>The HUD lab: the window at an hour (and by night) for a photo; float.NaN gives the clock back.</summary>
    public void LabPreview(float hour, bool night)
    {
        labHour = hour;
        labNight = night;
        shownMinute = int.MinValue;
    }

    /// <summary>The HUD lab: the column folded into its bench strip (or not), without the bench.</summary>
    public void LabFold(bool folded) => labFold = folded;

    /// <summary>The HUD lab: the next frame lands the café's newest star again (the drop, the glint, the café front).</summary>
    public void LabStarLands()
    {
        if (shownStars > 0) shownStars--;
    }

    /// <summary>The HUD lab: the window's city swapped (San Francisco, Seattle: HudCity.CityName).</summary>
    public void LabCity(string city)
    {
        HudCity.CityName = city;
        window.SwapCity(city);
        miniWindow.SwapCity(city);
        shownMinute = int.MinValue;
    }

    // ------------------------------------------------------------------ boxes

    static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, anchor, pivot, position, size);
        return rect;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        StretchInside(rect, 0f);
        return rect;
    }

    static void StretchInside(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
