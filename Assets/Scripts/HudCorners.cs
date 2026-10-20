using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE HUD'S CORNERS: ONE COLUMN TOP RIGHT, THE STOCK CHIPS BOTTOM RIGHT (19 Oct 2026, after his girlfriend's playtest 7;
// first built 7 Oct 2026 for playtest 3, session 3: claude/hud-spec.md, claude/playtest-3-sessions-2-6-plan.md §3.2-3.3)
//
// Her playtest (dyslexic, her first time, a PlayStation pad): "what day, what time? Oh, this is way too small." "The UI
// looks like the closed bar being on the bottom left rather than the top right." "open and then the amount." His answers
// (19 Oct): "the getting ready to close orange tint and the closing red tint, doesnt fit the overall cozy aesthetic";
// "the cash icon is spaced so far away from the number and both the closed and open sign should be on the top right corner
// with the cash underneath the sign, and the day green meter bar is ugly." On the franchise goal, her: "You have no idea,
// right? No."
//
// So, one column in the top right corner (28 px from the right edge, 24 from the top, 296 wide), top to bottom:
//
//   THE HANGING SIGN   kept from the bottom left with its strings, its sway and its flip. OPEN on paper in the brand green
//                      ("Come in"); LAST ORDERS on a honey-amber face for the café's last hour ("Closing at 8 PM", the
//                      hud.lastorders chime, a quicker sway); CLOSED on a warm brick-plum face ("See you tomorrow"), with
//                      the sign's own wooden flap (intro.sign) and a small bounce. Never red. The sign is a card with two
//                      faces: the face that is hidden is dressed with the new words BEFORE the card turns, so a turn never
//                      shows a stale face (the reviewers saw "OPEN · COME IN" at 8:00 PM, mid-turn to CLOSED).
//   THE SHELF          a dark plate (UiSkin.Band) under the sign, so the numbers read over any background (the reviewers:
//                      "$0 TODAY" white on a white tile wall; the clock white on the counter). On it:
//     the day row      a SUN DIAL in place of the green bar: a paper half-arc, a gold sun travelling from its left end
//                      (opening) to its right end (closing); in the last hour the arc warms from pale gold to honey and the
//                      sun with it; at closing the sun sets below the right end and a small moon comes up. Beside it the time
//                      (30 px bold), under them the day ("Monday · Day 1", 22 px).
//     the takings      the cash stack (a bill, an edge under it for every $24, ten at most) 8 px from the figure ("$142",
//                      40 px bold; "today" under it). A payment: the figure counts up and punches, the stack jolts, the bill
//                      drops and settles, a green glow blooms, and "+$6" pops up just under the figure and rises into it as
//                      it fades. A patron's coffee counts up quietly (playtest 4).
//     the stars        the café's five stars (ReputationLedger.StarsEarned, gold once earned) and "Franchise at 5 stars":
//                      the goal, on screen from Day 1.
//   STOCK CHIPS        bottom right, only when cups or beans run low: "Low on cups · 7 left" over "Restock at closing" (her:
//                      "Low on beans... How do you know? ... You buy it up through your phone. Oh, I wouldn't know.").
//
// NO SCREEN TINT: the amber pulse at last orders and the red bloom at closing are gone. The last hour is told by the sign,
// the dial and the café's own light (CafeDaylight keeps its last hour golden a little longer).
//
// AT THE BENCH the column folds into a strip 24-110 px down (the takings' zone, U2 of BenchStageContract, so the job card
// keeps its place under it and the mat stays clear): the sign, the day, "today" and the stars fade out over 0.2 s; the dial
// shrinks beside the time, the figure goes under them; at last orders and after closing a small honey or plum chip with the
// sign's word takes the dial's place, the time steps down beside the figure and the stack steps aside (a 30 px time and the
// chip can't share one row). The clock runs at the bench (the lock) and the takings stay, as before.
//
// The sway and the flip are separate pieces (both turn the sign; on one piece one undoes the other). Everything times
// itself on UiClock, so it all stands still while the phone pauses the game. Hidden while the recap has the screen and at
// night (the café is closed: the night has its own clock, top right). The chips step aside while a conversation or the
// counter phone has the bottom of the screen. Made by ShopUI while playing, on a canvas of its own drawn under every other
// screen (2): nothing in the scene changes.
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
    // The sign.
    const float StringLength = 16f, SignWidth = 280f, SignHeight = 112f, Border = 3f, SignRadius = 14f;
    // The shelf under it, in the café: the day row, the takings, the stars.
    const float ShelfTop = StringLength + SignHeight + 8f;   // 136
    const float ShelfPad = 8f, RowGap = 6f;
    const float DialWidth = 96f, DialHeight = 48f, DialRadius = 44f, DialStroke = 4f, SunSize = 18f, HaloSize = 44f, MoonSize = 14f;
    const float TimeSize = 30f, TimeBox = 34f, DaySize = 22f, DaySizeMin = 20f, DayBox = 26f;
    const float FigureSize = 40f, FigureBox = 46f, TodaySize = 20f, TodayBox = 22f;
    const float StarSize = 22f, StarGap = 4f, StarsBox = 24f, LineSize = 20f, LineBox = 22f;
    const float DayRowTop = ShelfTop + ShelfPad;                      // 144
    const float DayTop = DayRowTop + DialHeight + 4f;                 // 196
    const float FigureTop = DayTop + DayBox + RowGap;                 // 228
    const float TodayTop = FigureTop + FigureBox;                     // 274
    const float StarsTop = TodayTop + TodayBox + RowGap;              // 302
    const float LineTop = StarsTop + StarsBox + 2f;                   // 328
    const float ShelfBottom = LineTop + LineBox + ShelfPad;           // 358: the column's bottom in the café
    // The strip at the bench (zone U2: 18-110 px down).
    const float StripHeight = 86f, StripRow = 34f, StripPad = 8f, StripFigureSize = 30f, BenchStackScale = .7f;
    const float BenchDialScale = 28f / DialHeight;
    const float ChipWordSize = 20f, ChipPadX = 12f;
    // The cash stack (the icon against the figure).
    const float BillWidth = 60f, BillHeight = 27f, EdgeWidth = 56f, EdgeHeight = 4f, EdgeStep = 3f, StackGap = 8f;
    const int DollarsPerEdge = 24, MostEdges = 10;
    // The stock chips.
    const float StockHeight = 62f, StockPitch = 70f, StockTitleSize = 22f, StockLineSize = 20f;
    const float LabNudge = 92f;   // the café lab's banner sits in the bottom right corner (labs only)

    // ---- the sign's faces (never red: his words, 19 Oct) ----
    static readonly Color Honey = new Color(.941f, .710f, .298f);       // #F0B54C: the LAST ORDERS face
    static readonly Color Plum = new Color(.490f, .227f, .267f);        // #7D3A44: the CLOSED face, warm brick-plum
    static readonly Color PlumRim = new Color(.318f, .149f, .188f);     // #512630

    // ---- timing (seconds of UiClock) ----
    const float CountSeconds = .55f, PunchSeconds = .3f, JoltSeconds = .26f, DropSeconds = .44f, FlashSeconds = .5f, PopSeconds = 1.05f;
    const float PopRise = 26f;   // how far the paid chip climbs into the figure
    const float FlipSeconds = .72f, ChipInSeconds = .4f, FadeRate = 8f, SignPunchSeconds = .5f, SunSetSeconds = .8f;

    Canvas canvas;
    RectTransform root, column;
    CanvasGroup top, bottom;

    // The sign.
    RectTransform sign, sway, flipper;
    Image signShadow;
    CanvasGroup signGroup;
    sealed class Face
    {
        public RectTransform rect;
        public Image border, face, cup;
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

    // The shelf: the day row, the takings, the stars.
    RectTransform shelf, dayRow, dial, sunRect, haloRect, cash, stack, billRect, flash, pop, stars;
    Image shelfShadow, dialTrack, sun, halo, moon, bill, billShadow, flashImage, popChip;
    TextMeshProUGUI clockLine, dayLine, figure, figureLabel, popText, franchiseLine;
    CanvasGroup dayGroup, todayGroup, starsGroup;
    readonly Image[] starImages = new Image[ReputationRules.MaxStars];
    readonly List<RectTransform> edges = new List<RectTransform>();
    int shownMoney = int.MinValue, targetMoney, fromMoney, edgesShown = -1;
    int patronIncomeSeen;   // Playtest 4 (Systems S7): today's patron money already counted, quietly (DayClock.PatronIncome)
    float countSince = -10f, paidAt = -10f, figureWidth, laidOutFigureSize = -1f;
    string laidOutFigure;
    TMP_FontAsset laidOutFigureFont;
    int shownDay = int.MinValue, shownMinute = int.MinValue, shownStars = -1;
    float starPopAt = -10f;
    string laidOutDay;
    TMP_FontAsset laidOutFont;
    float clockGap;
    float sunProgress, sunDrop, moonUp, dialWarmth;
    // The bench strip's state chip (LAST ORDERS, CLOSED), in the dial's place.
    RectTransform stateChip;
    Image stateChipFace;
    TextMeshProUGUI stateChipText;
    CanvasGroup stateChipGroup;
    State chipState = State.Open;
    float chipShown, laidOutFold = -1f, laidOutChip = -1f;
    CanvasGroup stackGroup;

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

    // QUIET AT THE BENCH (checklist v2, 14 Oct 2026; his playtest 5: "the checklist is terrible", the First Hour's count of
    // about 120 words on the first repair's screen). While the close-up is up the column folds into its strip over 0.2 s (see
    // the header): the sign, the day line, "today" and the stars fade out; the dial, the time and the takings stay.
    const float QuietSeconds = .2f;
    float quietAlpha = 1f;
    /// <summary>The column's quiet words as they show now: 1 in the café, 0 at the bench (labs).</summary>
    public float QuietAlpha => quietAlpha;

    // ------------------------------------------------------------------ what checks read

    /// <summary>True while the column is on screen (by day, nothing else owning the screen).</summary>
    public bool Showing => canvas != null && canvas.enabled && top != null && top.alpha > .5f;
    /// <summary>Whether the bottom corner (the stock chips' group) is up: it steps aside for a conversation or the counter phone.</summary>
    public bool BottomShowing => Showing && bottom != null && bottom.alpha > .5f;
    /// <summary>Today's takings as the figure reads now ("$142"), and what it's counting toward.</summary>
    public string TakingsShown => figure != null ? figure.text : "";
    public int TakingsTarget => targetMoney;
    /// <summary>The figure's size now (1: at rest; more during a punch).</summary>
    public float TakingsPunch => figure != null ? figure.rectTransform.localScale.x : 1f;
    /// <summary>The edges under the bill (one for every $24 taken today, ten at most).</summary>
    public int EdgesShown => Mathf.Max(0, edgesShown);
    public bool BillShowing => bill != null && bill.enabled;
    /// <summary>The sign's word for the state it is in: OPEN, LAST ORDERS or CLOSED (its card may still be turning: FaceShowing).</summary>
    public string SignWord => WordOf(state);
    /// <summary>The words on the face that faces the screen right now (during a turn, the old face until the card is edge-on).</summary>
    public string FaceShowing => FaceFacing() != null ? FaceFacing().word.text : "";
    public bool LastOrders => state == State.LastOrders;
    public bool SignClosed => state == State.Closed;
    /// <summary>The day and the time as the day row reads them ("Monday · Day 1   9:30 AM"): the day under the time, beside the dial.</summary>
    public string ClockLine => dayLine != null ? dayLine.text + "   " + clockLine.text : "";
    /// <summary>The room the day row has to spare: the day line across the shelf, the time beside the dial, the time at its widest (reference pixels).</summary>
    public float ClockGap => clockGap;
    /// <summary>The day line's size now (22 px; 20 px for a long weekday and day number, never less).</summary>
    public float ClockLineSize => dayLine != null ? dayLine.fontSize : 0f;
    /// <summary>The sun's place along the dial's arc, 0 at the left end (opening) to 1 at the right end (closing).</summary>
    public float SunProgress => sunProgress;
    /// <summary>True once the sun has set below the dial's right end (after closing).</summary>
    public bool SunSet => sunDrop > .5f;
    /// <summary>True while the small moon is up on the dial (after closing).</summary>
    public bool MoonShowing => moonUp > .5f;
    /// <summary>How far the dial has warmed toward honey: 0 until the last hour, 1 at closing.</summary>
    public float DialWarmth => dialWarmth;
    /// <summary>The café's stars as the column draws them (0-5).</summary>
    public int StarsShown => Mathf.Max(0, shownStars);
    /// <summary>The line under the stars ("Franchise at 5 stars").</summary>
    public string FranchiseLine => franchiseLine != null ? franchiseLine.text : "";
    /// <summary>True while the column is folded into its strip at the bench.</summary>
    public bool BenchStrip => quietAlpha < .5f;
    /// <summary>The strip's state chip at the bench ("LAST ORDERS", "CLOSED"), or "" (in the café, or while the café is open).</summary>
    public string BenchChipText => stateChipGroup != null && stateChipGroup.alpha > .5f ? stateChipText.text : "";
    public bool CupsChipShowing => cupsChip != null && cupsChip.showing;
    public bool BeansChipShowing => beansChip != null && beansChip.showing;
    /// <summary>The chips' words as they read, each chip's two lines joined ("Low on cups · 7 left · Restock at closing | ...").</summary>
    public string ChipsText => (cupsChip != null && cupsChip.showing ? ChipWords(cupsChip) : "") + (beansChip != null && beansChip.showing ? " | " + ChipWords(beansChip) : "");
    /// <summary>The takings row (the stack and the figure) in the column.</summary>
    public RectTransform CashRect => cash;
    /// <summary>The sign (its strings, its swing and its card), top right.</summary>
    public RectTransform SignRect => sign;
    public RectTransform ChipsRect => chips;
    /// <summary>
    /// The column's bottom edge in reference pixels from the top of the screen, as it stands now: the whole column in the
    /// café (382), the strip at the bench (110). Whatever sits under the column (the bench's job card) starts below this.
    /// </summary>
    public float TopRightBottom => ColumnTop + Mathf.Lerp(ShelfBottom, StripHeight, Fold(1f - quietAlpha));

    // ------------------------------------------------------------------ making it

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
        foreach (Object made in drawn) if (made != null) Destroy(made);
    }

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("HUD corners (while playing)", null, SortingOrder);
        root = (RectTransform)canvas.transform;

        top = Stretch("Top right (the column)", root).gameObject.AddComponent<CanvasGroup>();
        bottom = Stretch("Bottom right (the chips)", root).gameObject.AddComponent<CanvasGroup>();
        foreach (CanvasGroup g in new[] { top, bottom }) { g.blocksRaycasts = false; g.interactable = false; }

        column = Box("The column", top.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ColumnRight, -ColumnTop), new Vector2(ColumnWidth, ShelfBottom));
        BuildShelf(column);
        BuildSign(column);   // after the shelf, so the sign and its shadow draw over the shelf's top edge
        BuildChips((RectTransform)bottom.transform);
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

    // The sign: two strings, then the swing (the sway), the card's turn (the flip), the two faces.
    void BuildSign(RectTransform parent)
    {
        sign = Box("The sign", parent, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(SignWidth, StringLength + SignHeight));
        signGroup = Quiet(sign);

        foreach (float x in new[] { -.23f, .23f })
        {
            RectTransform cord = Box("String", sign, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(SignWidth * x, 0f), new Vector2(2f, StringLength + 6f));
            Image c = cord.gameObject.AddComponent<Image>();
            c.color = new Color(UiSkin.BandText.r, UiSkin.BandText.g, UiSkin.BandText.b, .75f);
            c.raycastTarget = false;
        }

        // The swing hangs from the strings' ends; the flip turns the card about its middle. ("Swing" by name: the pause lab reads it.)
        sway = Box("Swing", sign, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -StringLength), new Vector2(SignWidth, SignHeight));
        flipper = Box("Turn", sway, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        signShadow = UiSkin.DropShadow(flipper, 9f, 4f, .38f);
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
        RectTransform cupRect = Box("Cup", f.rect, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(26f, 22f));
        f.cup = cupRect.gameObject.AddComponent<Image>();
        f.cup.sprite = cupSprite != null ? cupSprite : cupSprite = MakeSprite(CupMark(), 0f);
        f.cup.raycastTarget = false;
        f.word = UiSkin.Text("Word", f.rect, 40f, UiSkin.Brand, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(f.word.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 1f), new Vector2(SignWidth - 16f, 46f));
        // The line under the word, 20 px in sentence case (her: "your text needs to be bolder"; the 12.5 px spaced caps are gone).
        f.sub = UiSkin.Text("Under", f.rect, 20f, UiSkin.Brand, TextAlignmentOptions.Center, FontStyles.Normal);
        Place(f.sub.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 8f), new Vector2(SignWidth - 16f, 24f));
        return f;
    }

    Sprite cupSprite, glowSprite;

    Sprite GlowSprite() => glowSprite != null ? glowSprite : glowSprite = MakeSprite(Glow(), 0f);

    // The shelf: the dark plate, and on it the day row (the dial, the time, the day), the takings and the stars; the bench
    // strip's state chip too (hidden in the café).
    void BuildShelf(RectTransform parent)
    {
        shelf = Box("The shelf", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -ShelfTop), new Vector2(ColumnWidth, ShelfBottom - ShelfTop));
        UiSkin.Paint(shelf, UiSkin.Band, UiSkin.Radius);
        shelfShadow = UiSkin.DropShadow(shelf, 10f, 4f, .3f);

        // The day row: the dial at the left, the time at the right, the day under them.
        dayRow = Box("The day", shelf, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -ShelfPad), new Vector2(ColumnWidth, DialHeight));
        dial = Box("Sun dial", dayRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad, 0f), new Vector2(DialWidth, DialHeight));
        RectTransform trackRect = Box("Arc", dial, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(DialWidth, DialHeight));
        dialTrack = trackRect.gameObject.AddComponent<Image>();
        dialTrack.sprite = MakeSprite(Arc(), 0f);
        dialTrack.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .55f);
        dialTrack.raycastTarget = false;
        haloRect = Box("Halo", dial, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(HaloSize, HaloSize));
        halo = haloRect.gameObject.AddComponent<Image>();
        halo.sprite = GlowSprite();
        halo.color = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, .3f);
        halo.raycastTarget = false;
        sunRect = Box("Sun", dial, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(SunSize, SunSize));
        sun = sunRect.gameObject.AddComponent<Image>();
        sun.sprite = UiSkin.Circle;
        sun.color = UiSkin.Gold;
        sun.raycastTarget = false;
        // Inside the dome, up and to the left of where the sun goes down.
        RectTransform moonRect = Box("Moon", dial, Vector2.zero, new Vector2(.5f, .5f), new Vector2(DialWidth * .5f + 12f, 28f), new Vector2(MoonSize, MoonSize));
        moon = moonRect.gameObject.AddComponent<Image>();
        moon.sprite = MakeSprite(Moon(), 0f);
        moon.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, 0f);
        moon.raycastTarget = false;

        clockLine = UiSkin.Text("Time", dayRow, TimeSize, UiSkin.BandText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(clockLine.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -(DialHeight - TimeBox) * .5f),
            new Vector2(ColumnWidth - 2f * Pad - DialWidth - 10f, TimeBox));
        dayLine = UiSkin.Text("Day", shelf, DaySize, UiSkin.BandFaint, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        Place(dayLine.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -(DayTop - ShelfTop)), new Vector2(ColumnWidth - 2f * Pad, DayBox));
        dayGroup = Quiet(dayLine);

        // The bench strip's state chip, in the dial's place (hidden in the café, and while the café is open).
        stateChip = Box("State chip (the bench)", shelf, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -StripPad), new Vector2(150f, StripRow));
        stateChipFace = UiSkin.Paint(stateChip, Honey, StripRow * .5f);
        stateChipText = UiSkin.Text("Word", stateChip, ChipWordSize, UiSkin.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchInside(stateChipText.rectTransform, 0f);
        stateChipGroup = Quiet(stateChip);
        stateChipGroup.alpha = 0f;

        BuildCash(shelf);
        BuildStars(shelf);
    }

    // The takings: the figure at the right, the stack against it; the glow and the pop behind and under the stack.
    void BuildCash(RectTransform parent)
    {
        cash = Box("Today's takings", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -(FigureTop - ShelfTop)), new Vector2(ColumnWidth, FigureBox + TodayBox));

        figure = UiSkin.Text("Figure", cash, FigureSize, UiSkin.Paper, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        // Pivoted at its right middle, so a punch grows it out from the column's edge.
        Place(figure.rectTransform, new Vector2(1f, 1f), new Vector2(1f, .5f), new Vector2(-Pad, -FigureBox * .5f), new Vector2(ColumnWidth - 2f * Pad, FigureBox));
        figureLabel = UiSkin.Text("Label", cash, TodaySize, UiSkin.BandFaint, TextAlignmentOptions.TopRight, FontStyles.Normal);
        figureLabel.text = "today";
        Place(figureLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -FigureBox), new Vector2(ColumnWidth - 2f * Pad, TodayBox));
        todayGroup = Quiet(figureLabel);

        // The stack hangs from its top right corner, 8 px left of the figure (placed every frame: the figure's width changes).
        stack = Box("Stack", cash, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad - 90f - StackGap, -4f), new Vector2(BillWidth, BillHeight));
        stackGroup = Quiet(stack);   // it steps aside at the bench while the state chip has the strip's first row
        flash = Box("Glow", stack, new Vector2(.5f, 1f), new Vector2(.5f, .5f), new Vector2(0f, -BillHeight * .5f - 6f), new Vector2(120f, 120f));
        flashImage = flash.gameObject.AddComponent<Image>();
        flashImage.sprite = GlowSprite();
        flashImage.color = new Color(UiSkin.BrandBright.r, UiSkin.BrandBright.g, UiSkin.BrandBright.b, 0f);
        flashImage.raycastTarget = false;

        for (int i = 0; i < MostEdges; i++)
        {
            RectTransform edge = Box("Edge " + (i + 1), stack, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(i % 2 == 0 ? -1.5f : 1.5f, -BillHeight + 2f - i * EdgeStep), new Vector2(EdgeWidth, EdgeHeight));
            edge.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -.6f : .6f);
            Image e = edge.gameObject.AddComponent<Image>();
            UiSkin.Round(e, 2f);
            e.color = Color.Lerp(UiSkin.Brand, UiSkin.BrandDeep, i % 2 == 0 ? .15f : .45f);
            e.raycastTarget = false;
            edge.gameObject.SetActive(false);
            edges.Add(edge);
        }
        // The bill last, so it lies on the edges.
        billRect = Box("Bill", stack, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(BillWidth, BillHeight));
        bill = billRect.gameObject.AddComponent<Image>();
        bill.sprite = MakeSprite(Bill(), 0f);
        bill.raycastTarget = false;
        billShadow = UiSkin.DropShadow(billRect, 4f, 2f, .35f);
        bill.enabled = false;
        billShadow.enabled = false;

        // "+$6" pops up just under the figure and rises into it as it fades (UpdateCash); "today" steps aside while it's up.
        pop = Box("Paid", cash, new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(-Pad - 40f, -FigureBox + 4f), new Vector2(80f, 32f));
        popChip = pop.gameObject.AddComponent<Image>();
        UiSkin.Round(popChip, 16f);
        popChip.color = UiSkin.Brand;
        popChip.raycastTarget = false;
        popText = UiSkin.Text("Amount", pop, 22f, UiSkin.Paper, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchInside(popText.rectTransform, 0f);
        pop.gameObject.SetActive(false);
    }

    // The café's stars, and the goal under them.
    void BuildStars(RectTransform parent)
    {
        stars = Box("The stars", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -(StarsTop - ShelfTop)), new Vector2(ColumnWidth, StarsBox + 2f + LineBox));
        starsGroup = Quiet(stars);
        Sprite star = MakeSprite(Star(), 0f);
        for (int i = 0; i < starImages.Length; i++)
        {
            RectTransform r = Box("Star " + (i + 1), stars, new Vector2(1f, 1f), new Vector2(.5f, .5f),
                new Vector2(-Pad - StarSize * .5f - (starImages.Length - 1 - i) * (StarSize + StarGap), -StarsBox * .5f), new Vector2(StarSize, StarSize));
            starImages[i] = r.gameObject.AddComponent<Image>();
            starImages[i].sprite = star;
            starImages[i].raycastTarget = false;
            starImages[i].color = new Color(UiSkin.BandFaint.r, UiSkin.BandFaint.g, UiSkin.BandFaint.b, .3f);
        }
        franchiseLine = UiSkin.Text("Franchise", stars, LineSize, UiSkin.BandText, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        franchiseLine.text = "Franchise at 5 stars";
        Place(franchiseLine.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -StarsBox - 2f), new Vector2(ColumnWidth - 2f * Pad, LineBox));
    }

    void BuildChips(RectTransform parent)
    {
        chips = Box("Stock", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-UiSkin.Margin, 26f), new Vector2(360f, 2f * StockPitch));
        cupsChip = MakeChip("Cups", 0);
        beansChip = MakeChip("Beans", 1);
    }

    // A stock chip: what's low (22 px bold) over when to buy (20 px), on the accent (or the sign's plum once it's out).
    Chip MakeChip(string name, int row)
    {
        var chip = new Chip();
        chip.rect = Box(name, chips, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, row * StockPitch), new Vector2(300f, StockHeight));
        chip.face = UiSkin.Paint(chip.rect, UiSkin.Accent, 16f);
        chip.glow = UiSkin.DropShadow(chip.rect, 12f, 3f, .3f);
        chip.glowRect = chip.glow.rectTransform;
        chip.title = UiSkin.Text("Words", chip.rect, StockTitleSize, UiSkin.Paper, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(chip.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -7f), new Vector2(268f, 26f));
        // Bold as well: paper on the accent reads at 3.6:1, enough for bold text at this size, not for a light line.
        chip.line = UiSkin.Text("When", chip.rect, StockLineSize, UiSkin.Paper, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        chip.line.text = "Restock at closing";
        Place(chip.line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -33f), new Vector2(268f, 24f));
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
        // The day intro (13 Oct 2026, Cinematics): the corners wait while it plays (UiPins reads DayIntro.Playing by name).
        bool show = clock != null && !clock.DayOver && !night && !RecapUI.Showing && !UiPins.DayIntroPlaying;
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
        bool busy = BottomBusy();
        bottom.alpha = wasShown ? Mathf.MoveTowards(bottom.alpha, busy ? 0f : 1f, dt * FadeRate) : busy ? 0f : 1f;
        wasShown = true;
        // At the bench the column folds into its strip (see QuietSeconds); its state chip comes and goes at the same pace.
        quietAlpha = Mathf.MoveTowards(quietAlpha, ItemInspector.Working ? 0f : 1f, dt / QuietSeconds);
        chipShown = Mathf.MoveTowards(chipShown, chipState == State.Open ? 0f : 1f, dt / QuietSeconds);

        UpdateSign(clock, now, dt);
        UpdateDial(clock, now, dt);
        UpdateClockLines(clock);
        UpdateCash(clock, now);
        UpdateStars(now);
        Layout(1f - quietAlpha);
        UpdateChips(now);
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

    // Where everything sits, between the café's column (fold 0) and the bench's strip (fold 1). Run every frame: the figure's
    // width and the fold both move; it does nothing when neither has changed.
    void Layout(float q)
    {
        float e = Fold(Mathf.Clamp01(q));
        float figureSize = Mathf.Lerp(FigureSize, StripFigureSize, e);
        if (!Mathf.Approximately(figure.fontSize, figureSize)) figure.fontSize = figureSize;
        if (figure.text != laidOutFigure || !Mathf.Approximately(figure.fontSize, laidOutFigureSize) || figure.font != laidOutFigureFont)
        {
            laidOutFigure = figure.text;
            laidOutFigureSize = figure.fontSize;
            laidOutFigureFont = figure.font;
            figureWidth = Mathf.Ceil(figure.GetPreferredValues(figure.text, 999f, 999f).x);
        }
        float punch = figure.rectTransform.localScale.x;
        float jolt = stackJolt;
        // The state chip's share of the strip: only at the bench, only at last orders and after closing.
        float k = e * chipShown;

        if (!Mathf.Approximately(e, laidOutFold) || !Mathf.Approximately(k, laidOutChip))
        {
            laidOutFold = e;
            laidOutChip = k;
            // The shelf: from under the sign to the top of the column, and to the strip's height.
            float shelfTop = Mathf.Lerp(ShelfTop, 0f, e), shelfHeight = Mathf.Lerp(ShelfBottom - ShelfTop, StripHeight, e);
            shelf.anchoredPosition = new Vector2(0f, -shelfTop);
            shelf.sizeDelta = new Vector2(ColumnWidth, shelfHeight);
            UiSkin.Follow(shelfShadow.rectTransform, shelf, 10f, 4f);
            // The day row: the dial shrinks to the strip's first row; the time keeps its size and moves with the row. While
            // the state chip has the first row (it is too wide to share it with a 30 px time), the time goes to the second
            // row's left, reading from the left, and the stack steps aside to leave the figure its room.
            float rowHeight = Mathf.Lerp(DialHeight, StripRow, e);
            dayRow.anchoredPosition = new Vector2(0f, -Mathf.Lerp(ShelfPad, StripPad, e));
            dayRow.sizeDelta = new Vector2(ColumnWidth, rowHeight);
            float dialScale = Mathf.Lerp(1f, BenchDialScale, e);
            dial.localScale = new Vector3(dialScale, dialScale, 1f);
            dial.anchoredPosition = new Vector2(Mathf.Lerp(Pad, 10f, e), -(rowHeight - DialHeight * dialScale) * .5f);
            Vector2 timeInRow = new Vector2(-Pad, -(rowHeight - TimeBox) * .5f);
            Vector2 timeBelow = new Vector2(-(ColumnWidth - 10f - TimeRoom), -(StripRow + 2f));
            clockLine.rectTransform.anchoredPosition = Vector2.Lerp(timeInRow, timeBelow, k);
            TextAlignmentOptions timeAlign = k > .5f ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            if (clockLine.alignment != timeAlign) clockLine.alignment = timeAlign;
            // The takings: under the day line in the café, the strip's second row at the bench.
            cash.anchoredPosition = new Vector2(0f, -Mathf.Lerp(FigureTop - ShelfTop, StripPad + StripRow + 2f, e));
            float figureBox = Mathf.Lerp(FigureBox, StripRow, e);
            figure.rectTransform.sizeDelta = new Vector2(ColumnWidth - 2f * Pad, figureBox);
            figure.rectTransform.anchoredPosition = new Vector2(-Pad, -figureBox * .5f);
            figureLabel.rectTransform.anchoredPosition = new Vector2(-Pad, -figureBox);
            float stackScale = Mathf.Lerp(1f, BenchStackScale, e);
            stack.localScale = new Vector3(stackScale, stackScale, 1f);
            stackGroup.alpha = 1f - k;
            // The quiet words: the sign, the day, "today", the stars go with the café's form; the state chip with the bench's.
            float cafe = 1f - e;
            signGroup.alpha = cafe;
            dayGroup.alpha = cafe;
            starsGroup.alpha = cafe;
            stateChipGroup.alpha = k;
            // At the bench the state chip takes the dial's place.
            bool dialOn = k < .5f;
            if (dial.gameObject.activeSelf != dialOn) dial.gameObject.SetActive(dialOn);
        }
        todayGroup.alpha = (1f - e) * (pop.gameObject.activeSelf ? 1f - popChip.color.a : 1f);

        // The stack against the figure (8 px, however many digits), jolted by a payment; the glow behind it.
        float figureBoxNow = figure.rectTransform.sizeDelta.y;
        stack.anchoredPosition = new Vector2(-Pad - figureWidth * punch - StackGap, -(figureBoxNow - BillHeight * stack.localScale.y) * .5f + 2f + jolt);
        // "+$6" under the figure's middle.
        pop.anchoredPosition = new Vector2(-Pad - figureWidth * .5f, -figureBoxNow + 6f + popRise);
    }

    // ------------------------------------------------------------------ the takings

    float stackJolt, popRise;

    void UpdateCash(DayClock clock, float now)
    {
        int money = Mathf.Max(0, clock.Earned);
        int patrons = Mathf.Max(0, clock.PatronIncome);
        if (shownMoney == int.MinValue || money < targetMoney)
        {
            // The first look, or a new morning (the stack empties): as it is, no counting.
            shownMoney = targetMoney = fromMoney = money;
            countSince = -10f;
            figure.text = "$" + money;
            SetEdges(money);
        }
        else if (money > targetMoney)
        {
            // Paid: the pop, the jolt, the drop, the glow, and the figure counting up. A patron's coffee counts up in
            // the figure with no pop, jolt or drop: those are the player's own pay (playtest 4: "+$5" pops with nobody served).
            int quiet = Mathf.Clamp(patrons - patronIncomeSeen, 0, money - targetMoney);
            if (money - targetMoney - quiet > 0) Paid(money - targetMoney - quiet, now);
            fromMoney = shownMoney;
            targetMoney = money;
            countSince = now;
            SetEdges(money);
        }
        patronIncomeSeen = patrons;

        if (countSince > -5f)
        {
            float t = Mathf.Clamp01((now - countSince) / CountSeconds);
            int value = Mathf.RoundToInt(Mathf.Lerp(fromMoney, targetMoney, 1f - (1f - t) * (1f - t)));
            if (value != shownMoney)
            {
                shownMoney = value;
                figure.text = "$" + value;
            }
            if (t >= 1f) countSince = -10f;
        }

        // The punch (from the payment), the jolt and the drop, the glow, the pop: all from paidAt.
        float since = now - paidAt;
        float punch = since < PunchSeconds ? 1f + .22f * Mathf.Sin(Mathf.Clamp01(since / PunchSeconds) * Mathf.PI) : 1f;
        figure.rectTransform.localScale = new Vector3(punch, punch, 1f);
        stackJolt = since < JoltSeconds ? -3f * Mathf.Sin(Mathf.Clamp01(since / JoltSeconds) * Mathf.PI) : 0f;
        if (since < DropSeconds)
        {
            float k = Mathf.Clamp01(since / DropSeconds);
            // From above and tilted, past its place a little and back (a settle).
            float y = k < .74f ? Mathf.Lerp(40f, -3f, Ease(k / .74f)) : k < .88f ? Mathf.Lerp(-3f, 3f, (k - .74f) / .14f) : Mathf.Lerp(3f, 0f, (k - .88f) / .12f);
            float tilt = k < .74f ? Mathf.Lerp(-15f, -2f, k / .74f) : k < .88f ? Mathf.Lerp(-2f, 1f, (k - .74f) / .14f) : Mathf.Lerp(1f, 0f, (k - .88f) / .12f);
            billRect.anchoredPosition = new Vector2(0f, y);
            billRect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            bill.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k / .5f));
        }
        else
        {
            billRect.anchoredPosition = Vector2.zero;
            billRect.localRotation = Quaternion.identity;
            bill.color = Color.white;
        }
        billShadow.enabled = bill.enabled;
        if (bill.enabled) UiSkin.Follow(billShadow.rectTransform, billRect, 4f, 2f);
        float glow = since < FlashSeconds ? 1f - since / FlashSeconds : 0f;
        flashImage.color = new Color(UiSkin.BrandBright.r, UiSkin.BrandBright.g, UiSkin.BrandBright.b, .85f * glow);
        flash.localScale = Vector3.one * Mathf.Lerp(.45f, 1.35f, 1f - glow);
        if (since < PopSeconds)
        {
            float k = since / PopSeconds;
            float scale = k < .25f ? Mathf.Lerp(.7f, 1.18f, k / .25f) : k < .45f ? Mathf.Lerp(1.18f, 1f, (k - .25f) / .2f) : 1f;
            // In from a little lower, a settle, then up into the figure as it fades: the money goes onto the pile.
            popRise = k < .25f ? Mathf.Lerp(-10f, 0f, k / .25f) : k < .45f ? Mathf.Lerp(0f, 3f, (k - .25f) / .2f) : Mathf.Lerp(3f, PopRise, (k - .45f) / .55f);
            pop.localScale = new Vector3(scale, scale, 1f);
            popChip.color = new Color(UiSkin.Brand.r, UiSkin.Brand.g, UiSkin.Brand.b, k < .25f ? k / .25f : k > .7f ? (1f - k) / .3f : 1f);
            popText.alpha = popChip.color.a;
            if (!pop.gameObject.activeSelf) pop.gameObject.SetActive(true);
        }
        else if (pop.gameObject.activeSelf) pop.gameObject.SetActive(false);
    }

    void Paid(int amount, float now)
    {
        paidAt = now;
        popText.text = "+$" + amount;
        float width = Mathf.Ceil(popText.GetPreferredValues(popText.text, 999f, 32f).x) + 26f;
        pop.sizeDelta = new Vector2(Mathf.Max(58f, width), 32f);
        // No sound of its own: the customer's payment already rings (money.paid, money.tip).
    }

    void SetEdges(int money)
    {
        int n = money <= 0 ? 0 : Mathf.Min(MostEdges, money / DollarsPerEdge);
        bill.enabled = money > 0;
        if (n == edgesShown) return;
        edgesShown = n;
        for (int i = 0; i < edges.Count; i++) edges[i].gameObject.SetActive(i < n);
    }

    static float Ease(float t) => 1f - (1f - t) * (1f - t);

    // ------------------------------------------------------------------ the stars

    // The café's stars as the ledger holds them; a star earned while the column is up pops once.
    void UpdateStars(float now)
    {
        ReputationLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Reputation : null;
        int earned = ledger != null ? Mathf.Clamp(ledger.StarsEarned, 0, ReputationRules.MaxStars) : 0;
        if (earned != shownStars)
        {
            if (shownStars >= 0 && earned > shownStars) starPopAt = now;
            shownStars = earned;
            for (int i = 0; i < starImages.Length; i++)
                starImages[i].color = i < earned ? UiSkin.Gold : new Color(UiSkin.BandFaint.r, UiSkin.BandFaint.g, UiSkin.BandFaint.b, .3f);
            franchiseLine.text = earned >= ReputationRules.MaxStars ? "Five stars · the franchise offer is coming" : "Franchise at 5 stars";
        }
        float k = now - starPopAt < .45f ? Mathf.Sin(Mathf.Clamp01((now - starPopAt) / .45f) * Mathf.PI) : 0f;
        int popping = Mathf.Clamp(shownStars - 1, 0, starImages.Length - 1);
        starImages[popping].rectTransform.localScale = Vector3.one * (1f + .5f * k);
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
        UiSkin.Follow(signShadow.rectTransform, flipper, 9f, 4f);

        // The sway, on the swing: gentle when open, quicker and wider at last orders, still once closed.
        float amplitude = state == State.Open ? 1f : state == State.LastOrders ? 2.8f : 0f;
        float period = state == State.LastOrders ? 1.15f : 5.5f;
        swayPhase += dt * Mathf.PI * 2f / period;
        float wanted = amplitude * Mathf.Sin(swayPhase);
        float current = sway.localEulerAngles.z > 180f ? sway.localEulerAngles.z - 360f : sway.localEulerAngles.z;
        sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(current, wanted, 1f - Mathf.Exp(-10f * dt)));
        // The bounce at closing: the card grows from its strings and settles, half a second.
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
            float width = Mathf.Ceil(stateChipText.GetPreferredValues(stateChipText.text, 999f, StripRow).x) + 2f * ChipPadX;
            stateChip.sizeDelta = new Vector2(width, StripRow);
            // Its coming and going is chipShown (LateUpdate), eased in Layout, so the dial and the time make way smoothly.
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

    // ------------------------------------------------------------------ the sun dial

    // The sun travels the arc with the day's clock; the arc and the sun warm through the last hour; at closing the sun
    // drops below the right end and the moon comes up (and the next morning the sun comes up again at the left end).
    void UpdateDial(DayClock clock, float now, float dt)
    {
        sunProgress = Mathf.Clamp01(clock.NormalizedDay);
        float lastHour = Mathf.Max(0f, clock.ClosingHour - 1f);
        float warmthWanted = state == State.Closed ? 1f : state == State.LastOrders ? Mathf.Clamp01((clock.CurrentHour - lastHour) / Mathf.Max(.01f, clock.ClosingHour - lastHour)) : 0f;
        dialWarmth = Mathf.MoveTowards(dialWarmth, warmthWanted, dt * 2f);
        bool closed = state == State.Closed;
        sunDrop = Mathf.MoveTowards(sunDrop, closed ? 1f : 0f, dt / SunSetSeconds);
        moonUp = Mathf.MoveTowards(moonUp, closed && sunDrop > .35f ? 1f : 0f, dt / SunSetSeconds);

        float theta = Mathf.PI * (1f - sunProgress);
        Vector2 centre = new Vector2(DialWidth * .5f + DialRadius * Mathf.Cos(theta), 2f + DialRadius * Mathf.Sin(theta));
        float drop = Ease(sunDrop) * (SunSize + 2f);
        Vector2 at = centre + new Vector2(0f, -drop);
        sunRect.anchoredPosition = at;
        haloRect.anchoredPosition = at;
        float sunAlpha = 1f - Ease(sunDrop);
        Color sunColour = Color.Lerp(UiSkin.Gold, UiSkin.Accent, dialWarmth * .7f);
        sun.color = new Color(sunColour.r, sunColour.g, sunColour.b, sunAlpha);
        halo.color = new Color(sunColour.r, sunColour.g, sunColour.b, (.3f + .3f * dialWarmth) * sunAlpha);
        float haloScale = 1f + .25f * dialWarmth;
        haloRect.localScale = new Vector3(haloScale, haloScale, 1f);
        moon.color = new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .9f * Ease(moonUp));

        // The track: paper by day; pale gold to honey through the last hour.
        Color trackWanted = state == State.Open
            ? new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .55f)
            : Color.Lerp(new Color(UiSkin.GoldSoft.r, UiSkin.GoldSoft.g, UiSkin.GoldSoft.b, .95f), new Color(UiSkin.Accent.r, UiSkin.Accent.g, UiSkin.Accent.b, .95f), dialWarmth);
        dialTrack.color = Color.Lerp(dialTrack.color, trackWanted, 1f - Mathf.Exp(-6f * dt));
    }

    // ------------------------------------------------------------------ the day and the time

    void UpdateClockLines(DayClock clock)
    {
        // Built only when the minute changes.
        int minute = Mathf.FloorToInt(Mathf.Clamp(clock.CurrentHour, 0f, 24f) * 60f + .001f);
        if (clock.Day != shownDay || minute != shownMinute)
        {
            shownDay = clock.Day;
            shownMinute = minute;
            dayLine.text = Weekdays.Label(clock.Day);
            clockLine.text = ShopUI.FormatHour(clock.CurrentHour);
        }
        if (dayLine.text != laidOutDay || dayLine.font != laidOutFont) LayoutClockLine(dayLine.text);
    }

    // THE DAY AND THE TIME (playtest 4: "Wednesday · Day 3 9:09 AM" ran together at 16 px; her playtest 7: "what day, what
    // time? Oh, this is way too small."). The time is 30 px bold beside the dial; the day line is 22 px across the shelf
    // under them, 20 px only when a long weekday and day number wouldn't fit, never less. Both are measured, the time at its
    // widest, and the room to spare is what the pause lab checks. Laid out again only when the day's words (or the font) change.
    const float DayRoom = ColumnWidth - 2f * Pad;                     // the day line, across the shelf
    const float TimeRoom = ColumnWidth - 2f * Pad - DialWidth - 10f;  // the time, beside the dial
    const float LineGap = 10f;
    static readonly string[] WidestTimes = { "12:00 PM", "10:00 AM", "10:00 PM" };

    void LayoutClockLine(string dayLabel)
    {
        laidOutDay = dayLabel;
        laidOutFont = dayLine.font;
        dayLine.fontSize = SizeFor(dayLabel);
        clockGap = Mathf.Min(DayRoom - Measure(dayLine, dayLabel), TimeRoom - WidestTime());
    }

    // 22 px when the day line leaves the gap at 22; else the largest size in half pixels down to 20; 20 when none does.
    float SizeFor(string dayLabel)
    {
        for (float size = DaySize; size > DaySizeMin; size -= .5f)
        {
            dayLine.fontSize = size;
            if (DayRoom - Measure(dayLine, dayLabel) >= LineGap) return size;
        }
        return DaySizeMin;
    }

    float WidestTime()
    {
        float most = 0f;
        foreach (string time in WidestTimes) most = Mathf.Max(most, Measure(clockLine, time));
        return most;
    }

    // A text's width for some words at its size now. TextMesh Pro measures a text's own words (measuring other words leaves
    // them in its buffers): other words are set in for the measure and put back, within the frame, so they're never drawn.
    static float Measure(TMP_Text text, string words)
    {
        string was = text.text;
        if (was == words) return text.GetPreferredValues(999f, 999f).x;
        text.text = words;
        float width = text.GetPreferredValues(999f, 999f).x;
        text.text = was;
        return width;
    }

    /// <summary>The room the day row would have to spare with <paramref name="dayLabel"/> on the day line and <paramref name="time"/>
    /// beside the dial (checks: every weekday). The lines on screen are left as they are.</summary>
    public float GapFor(string dayLabel, string time)
    {
        if (dayLine == null || clockLine == null) return 0f;
        float was = dayLine.fontSize;
        float size = SizeFor(dayLabel);
        dayLine.fontSize = size;
        float gap = Mathf.Min(DayRoom - Measure(dayLine, dayLabel), TimeRoom - Measure(clockLine, time));
        dayLine.fontSize = was;
        return gap;
    }

    // ------------------------------------------------------------------ the stock chips

    void UpdateChips(float now)
    {
        ShopInventory stock = ShopInventory.Instance;
        bool lab = CafeLab.Active;
        chips.anchoredPosition = new Vector2(-UiSkin.Margin, 26f + (lab ? LabNudge : 0f));
        int row = 0;
        ChipFor(cupsChip, stock != null && stock.CupsLow, stock != null ? stock.Cups : 0, "cups", now, ref row);
        ChipFor(beansChip, stock != null && stock.BeansLow, stock != null ? stock.Beans : 0, "beans", now, ref row);
    }

    // What's low and how many are left, and under it when to buy (the Shop is on the phone at closing: her "Oh, I wouldn't
    // know"). Within WordBudget.BenchAlert (12 words), both lines together.
    void ChipFor(Chip chip, bool low, int count, string what, float now, ref int row)
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
        if (count != chip.shownCount)
        {
            chip.shownCount = count;
            chip.title.text = count <= 0 ? $"Out of {what}" : $"Low on {what} · {count} left";
            chip.face.color = count <= 0 ? Plum : UiSkin.Accent;
            float width = Mathf.Max(Mathf.Ceil(chip.title.GetPreferredValues(chip.title.text, 999f, 26f).x),
                                    Mathf.Ceil(chip.line.GetPreferredValues(chip.line.text, 999f, 24f).x)) + 32f;
            chip.rect.sizeDelta = new Vector2(width, StockHeight);
            chip.title.rectTransform.sizeDelta = new Vector2(width - 32f, 26f);
            chip.line.rectTransform.sizeDelta = new Vector2(width - 32f, 24f);
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

    // ------------------------------------------------------------------ the pictures, drawn in code once

    readonly List<Object> drawn = new List<Object>();

    Sprite MakeSprite(Texture2D texture, float border)
    {
        drawn.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        drawn.Add(sprite);
        return sprite;
    }

    delegate Color Painter(float x, float y);

    // Two by two samples a pixel, so every edge is smooth.
    static Texture2D Draw(string name, int width, int height, Painter paint)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD - " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Color c = paint(x + .25f + sx * .5f, y + .25f + sy * .5f);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                sum /= 4f;
                pixels[y * width + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f) return Color.clear;
        Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
        c.a = a;
        return c;
    }

    // Signed distance to a rounded box centred at (cx, cy) with half sizes (hx, hy) and corner radius r (negative inside).
    static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry) =>
        (new Vector2((x - cx) / rx, (y - cy) / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    // Signed distance to a five-pointed star centred at (cx, cy), its points r out, its inner corners at rf of that
    // (negative inside; Inigo Quilez's sdStar5).
    static float StarShape(float x, float y, float cx, float cy, float r, float rf)
    {
        const float k1x = .809016994f, k1y = -.587785252f, k2x = -k1x, k2y = k1y;
        float px = Mathf.Abs(x - cx), py = y - cy;
        float d1 = Mathf.Max(k1x * px + k1y * py, 0f);
        px -= 2f * d1 * k1x; py -= 2f * d1 * k1y;
        float d2 = Mathf.Max(k2x * px + k2y * py, 0f);
        px -= 2f * d2 * k2x; py -= 2f * d2 * k2y;
        px = Mathf.Abs(px);
        py -= r;
        float bax = rf * -k1y, bay = rf * k1x - 1f;
        float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
        float dx = px - bax * h, dy = py - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(py * bax - px * bay);
    }

    static float Inside(float d) => Mathf.Clamp01(.5f - d);
    static float Line(float d, float half) => Mathf.Clamp01(.5f - (Mathf.Abs(d) - half));

    // A banknote in the brand's green: a dark edge, a light frame inside it, an oval with a portrait's shape in the middle.
    static Texture2D Bill()
    {
        const int w = 140, h = 62;
        Color paper = Color.Lerp(UiSkin.Brand, Color.white, .28f), edge = UiSkin.BrandDeep;
        Color frame = new Color(1f, 1f, 1f, .38f), oval = new Color(1f, 1f, 1f, .2f), ring = new Color(1f, 1f, 1f, .55f);
        Color face = new Color(UiSkin.BrandDeep.r, UiSkin.BrandDeep.g, UiSkin.BrandDeep.b, .42f);
        return Draw("Bill", w, h, (x, y) =>
        {
            float outer = RoundBox(x, y, w / 2f, h / 2f, w / 2f - 1f, h / 2f - 1f, 6f);
            if (outer > 1f) return Color.clear;
            Color c = new Color(edge.r, edge.g, edge.b, Inside(outer));
            c = Over(c, new Color(paper.r, paper.g, paper.b, Inside(outer + 3f)));
            c = Over(c, new Color(frame.r, frame.g, frame.b, frame.a * Line(RoundBox(x, y, w / 2f, h / 2f, w / 2f - 9f, h / 2f - 9f, 3f), .9f)));
            float o = Ellipse(x, y, w / 2f, h / 2f, 19f, 17f);
            c = Over(c, new Color(oval.r, oval.g, oval.b, oval.a * Inside(o)));
            c = Over(c, new Color(ring.r, ring.g, ring.b, ring.a * Line(o, .9f)));
            // A head and shoulders in the oval.
            c = Over(c, new Color(face.r, face.g, face.b, face.a * Inside(Ellipse(x, y, w / 2f, h / 2f + 4f, 5.5f, 6f))));
            c = Over(c, new Color(face.r, face.g, face.b, face.a * Inside(Mathf.Max(Ellipse(x, y, w / 2f, h / 2f - 11f, 10f, 7f), (h / 2f - 9f) - y))));
            // The corners' marks.
            foreach (Vector2 m in new[] { new Vector2(17f, h - 17f), new Vector2(w - 17f, 17f) })
                c = Over(c, new Color(1f, 1f, 1f, .5f * Inside(Ellipse(x, y, m.x, m.y, 6f, 6f))));
            return c;
        });
    }

    // The sign's cup: a cup and its handle, three wisps of steam over it (white; the Image tints it).
    static Texture2D CupMark()
    {
        const int w = 60, h = 52;
        return Draw("Cup mark", w, h, (x, y) =>
        {
            float body = Mathf.Abs(RoundBox(x, y, 25f, 16f, 15f, 12f, 6f)) - 2.2f;
            float handle = Mathf.Abs(Ellipse(x, y, 42f, 17f, 6f, 6f)) - 2.2f;
            if (x < 39f) handle = Mathf.Max(handle, 2.5f);
            float d = Mathf.Min(body, handle);
            for (int i = -1; i <= 1; i++)
            {
                float sx = 25f + i * 8f;
                float wave = Mathf.Abs(x - (sx + 2f * Mathf.Sin((y - 32f) * .35f))) - 1.6f;
                if (y > 32f && y < 48f) d = Mathf.Min(d, wave);
            }
            return new Color(1f, 1f, 1f, Inside(d));
        });
    }

    // The dial's track: a half-arc with round ends, drawn at twice the dial's size (white; the Image tints it).
    static Texture2D Arc()
    {
        const int w = (int)DialWidth * 2, h = (int)DialHeight * 2;
        const float cx = DialWidth, cy = 4f, r = DialRadius * 2f, half = DialStroke;   // the dial's units, doubled
        return Draw("Arc", w, h, (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            float d = dy >= 0f
                ? Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - half
                : Mathf.Min(new Vector2(x - (cx - r), dy).magnitude, new Vector2(x - (cx + r), dy).magnitude) - half;
            return new Color(1f, 1f, 1f, Inside(d));
        });
    }

    // A crescent: a disc with another disc taken out of its right side (white; the Image tints it).
    static Texture2D Moon()
    {
        const int s = 40;
        return Draw("Moon", s, s, (x, y) =>
        {
            float disc = Ellipse(x, y, 18f, 20f, 15f, 15f);
            float bite = Ellipse(x, y, 27f, 23f, 13f, 13f);
            return new Color(1f, 1f, 1f, Inside(Mathf.Max(disc, -bite)));
        });
    }

    // A five-pointed star (white; the Image tints it: gold once earned, a ghost of paper before).
    static Texture2D Star()
    {
        const int s = 64;
        return Draw("Star", s, s, (x, y) => new Color(1f, 1f, 1f, Inside(StarShape(x, y, s / 2f, s / 2f - 2f, 29f, .48f))));
    }

    // A soft round glow, bright in the middle.
    static Texture2D Glow()
    {
        const int s = 96;
        return Draw("Glow", s, s, (x, y) =>
        {
            float d = new Vector2(x - s / 2f, y - s / 2f).magnitude / (s / 2f);
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        });
    }
}
