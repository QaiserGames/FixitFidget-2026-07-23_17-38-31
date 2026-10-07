using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE HUD'S CORNERS (playtest 3, session 3, 7 Oct 2026; the HUD spec of 24 Aug, claude/hud-spec.md, agreed then and built
// now; claude/playtest-3-sessions-2-6-plan.md §3.2 and §3.3)
//
// Four corners, four jobs:
//
//   Top right     TODAY'S TAKINGS AS A CASH STACK: a bill on top, the rest of the stack as edges under it (one edge for
//                 every $24, ten at most), the figure beside it. It grows through the day and empties each morning: today,
//                 not ever (the till's total is on the phone). A payment: "+$6" in a green chip pops up under the stack
//                 and rises into it as it fades, the stack jolts, the top bill drops and settles, a green glow blooms behind
//                 it, the figure counts up and punches.
//   Bottom left   THE HANGING SIGN: OPEN, swaying gently; LAST ORDERS for the café's last hour (amber, a faster sway, a soft
//                 amber pulse round the screen's edge: only a warning, arrivals still stop at closing, as they always have);
//                 at closing it flips over to CLOSED, and a red glow round the edge blooms and fades. Under it the day's
//                 bar fills from opening to closing (green, amber at last orders, red once shut), and the day and the time.
//   Top left      the tabs (TicketRailUI moved them here from the top middle).
//   Bottom right  STOCK CHIPS, only when cups or beans run low (under ShopInventory.LowStock): "Low on cups · 7 left",
//                 "Out of beans".
//
// The sway and the flip are separate pieces, as the spec's note says (both turn the sign; on one piece one undoes the
// other). Everything times itself on UiClock, so it all stands still while the phone pauses the game.
//
// Hidden while the recap has the screen and at night (the café is closed: the night has its own clock, top right). The
// bottom corners step aside while a conversation or the counter phone has the bottom of the screen (the portrait box and
// Ace's replies are there). Made by ShopUI while playing, on a canvas of its own drawn under every other screen (2): nothing
// in the scene changes.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class HudCorners : MonoBehaviour
{
    public static HudCorners Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public const int SortingOrder = 2;

    // ---- sizes (reference pixels, 1920 x 1080) ----
    const float BillWidth = 70f, BillHeight = 31f, EdgeWidth = 66f, EdgeHeight = 5f, EdgeStep = 4f;
    const int DollarsPerEdge = 24, MostEdges = 10;
    const float SignWidth = 232f, SignHeight = 124f, Border = 3f, BarHeight = 12f;
    const float LabNudge = 92f;   // the café lab's banner sits in the bottom right corner (labs only)

    // ---- timing (seconds of UiClock) ----
    const float CountSeconds = .55f, PunchSeconds = .3f, JoltSeconds = .26f, DropSeconds = .44f, FlashSeconds = .5f, PopSeconds = 1.05f;
    const float PopGap = 10f;   // between the stack's lowest edge and the paid chip, before it rises
    const float FlipSeconds = .72f, ChipInSeconds = .4f, FadeRate = 8f;

    Canvas canvas;
    RectTransform root;
    CanvasGroup top, bottom;

    // The cash stack.
    RectTransform cash, stack, billRect, flash, pop;
    Image bill, billShadow, flashImage, popChip, signShadow;
    TextMeshProUGUI figure, figureLabel, popText;
    readonly List<RectTransform> edges = new List<RectTransform>();
    int shownMoney = int.MinValue, targetMoney, fromMoney, edgesShown = -1;
    float countSince = -10f, paidAt = -10f;

    // The sign.
    RectTransform sign, sway, flipper, front, back, track, fill;
    Image frontBorder, frontFace, backBorder, backFace, fillImage, cup, vignette;
    TextMeshProUGUI frontWord, frontSub, backWord, backSub, dayLine, clockLine;
    enum State { Open, LastOrders, Closed }
    State state = State.Open;
    bool stateKnown;
    float flipFrom, flipTo, flipSince = -10f, swayPhase, closedAt = -10f;
    const float ClosedGlowSeconds = 1.8f;
    int shownDay = int.MinValue, shownMinute = int.MinValue, shownClosingHour = int.MinValue;

    // The stock chips.
    RectTransform chips;
    Chip cupsChip, beansChip;

    sealed class Chip
    {
        public RectTransform rect, glowRect;
        public Image face, glow;
        public TextMeshProUGUI text;
        public bool showing;
        public float since = -10f;
        public int shownCount = int.MinValue;
    }

    PlayerInteractor player;
    CounterRepairView counterPhone;
    bool wasShown;

    // ------------------------------------------------------------------ what checks read

    /// <summary>True while the corners are on screen (by day, nothing else owning the screen).</summary>
    public bool Showing => canvas != null && canvas.enabled && top != null && top.alpha > .5f;
    /// <summary>Whether the bottom corners (the sign, the chips) are up.</summary>
    public bool BottomShowing => Showing && bottom != null && bottom.alpha > .5f;
    /// <summary>Today's takings as the figure reads now ("$142"), and what it's counting toward.</summary>
    public string TakingsShown => figure != null ? figure.text : "";
    public int TakingsTarget => targetMoney;
    /// <summary>The figure's size now (1: at rest; more during a punch).</summary>
    public float TakingsPunch => figure != null ? figure.rectTransform.localScale.x : 1f;
    /// <summary>The edges under the bill (one for every $24 taken today, ten at most).</summary>
    public int EdgesShown => Mathf.Max(0, edgesShown);
    public bool BillShowing => bill != null && bill.enabled;
    /// <summary>The sign's word as it faces the screen: OPEN, LAST ORDERS or CLOSED.</summary>
    public string SignWord => state == State.Closed ? backWord.text : frontWord.text;
    public bool LastOrders => state == State.LastOrders;
    public bool SignClosed => state == State.Closed;
    /// <summary>The day and the time under the bar ("Monday · Day 1   9:30 AM").</summary>
    public string ClockLine => dayLine != null ? dayLine.text + "   " + clockLine.text : "";
    public bool CupsChipShowing => cupsChip != null && cupsChip.showing;
    public bool BeansChipShowing => beansChip != null && beansChip.showing;
    public string ChipsText => (cupsChip != null && cupsChip.showing ? cupsChip.text.text : "") + (beansChip != null && beansChip.showing ? " | " + beansChip.text.text : "");
    public RectTransform CashRect => cash;
    public RectTransform SignRect => sign;
    public RectTransform ChipsRect => chips;
    public float VignetteAlpha => vignette != null ? vignette.color.a : 0f;

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

        // The last-orders pulse round the edge of the screen, under everything else here.
        var vignetteRect = Stretch("Last orders (round the edge)", root);
        vignette = vignetteRect.gameObject.AddComponent<Image>();
        vignette.sprite = MakeSprite(Vignette(), 0f);
        vignette.color = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0f);
        vignette.raycastTarget = false;

        top = Stretch("Top corners", root).gameObject.AddComponent<CanvasGroup>();
        bottom = Stretch("Bottom corners", root).gameObject.AddComponent<CanvasGroup>();
        foreach (CanvasGroup g in new[] { top, bottom }) { g.blocksRaycasts = false; g.interactable = false; }

        BuildCash((RectTransform)top.transform);
        BuildSign((RectTransform)bottom.transform);
        BuildChips((RectTransform)bottom.transform);
    }

    // The cash stack: the figure on the right, the stack to its left; the glow and the pop behind and over the stack.
    void BuildCash(RectTransform parent)
    {
        cash = Box("Today's takings", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-UiSkin.Margin, -18f), new Vector2(300f, 92f));

        figure = UiSkin.Text("Figure", cash, 44f, UiSkin.Paper, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        // Pivoted at its right middle, so a punch grows it out from the corner's edge.
        Place(figure.rectTransform, new Vector2(1f, 1f), new Vector2(1f, .5f), new Vector2(0f, -27f), new Vector2(196f, 54f));
        UiSkin.Shadowed(figure);
        figureLabel = UiSkin.Text("Label", cash, 14f, UiSkin.BandText, TextAlignmentOptions.TopRight, FontStyles.Bold);
        figureLabel.characterSpacing = 18f;
        figureLabel.text = "TODAY";
        Place(figureLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -54f), new Vector2(196f, 20f));
        UiSkin.Shadowed(figureLabel);

        stack = Box("Stack", cash, new Vector2(1f, 1f), new Vector2(.5f, 1f), new Vector2(-196f - 14f - 40f, -6f), new Vector2(80f, 84f));
        flash = Box("Glow", stack, new Vector2(.5f, 1f), new Vector2(.5f, .5f), new Vector2(0f, -BillHeight * .5f - 6f), new Vector2(130f, 130f));
        flashImage = flash.gameObject.AddComponent<Image>();
        flashImage.sprite = MakeSprite(Glow(), 0f);
        flashImage.color = new Color(UiSkin.BrandBright.r, UiSkin.BrandBright.g, UiSkin.BrandBright.b, 0f);
        flashImage.raycastTarget = false;

        Sprite edgeSprite = UiSkin.Rounded;
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

        // Under the stack, rising up into it (UpdateCash): the stack sits at the top of the screen, so a chip rising off
        // the top of it went straight off the screen.
        pop = Box("Paid", stack, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -StackDepth(0) - PopGap), new Vector2(80f, 30f));
        popChip = pop.gameObject.AddComponent<Image>();
        UiSkin.Round(popChip, 15f);
        popChip.color = UiSkin.Brand;
        popChip.raycastTarget = false;
        popText = UiSkin.Text("Amount", pop, 20f, UiSkin.Paper, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchInside(popText.rectTransform, 0f);
        pop.gameObject.SetActive(false);
    }

    // The sign: two strings, then the swing (the sway), the card's turn (the flip), the two faces; the day's bar under it.
    void BuildSign(RectTransform parent)
    {
        sign = Box("The sign", parent, Vector2.zero, Vector2.zero, new Vector2(UiSkin.Margin, 20f), new Vector2(SignWidth, SignHeight + 72f));

        // The day's bar and the time, along the bottom.
        dayLine = UiSkin.Text("Day", sign, 16f, UiSkin.BandText, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        Place(dayLine.rectTransform, Vector2.zero, Vector2.zero, new Vector2(2f, 0f), new Vector2(SignWidth * .62f, 22f));
        UiSkin.Shadowed(dayLine);
        clockLine = UiSkin.Text("Time", sign, 16f, UiSkin.BandText, TextAlignmentOptions.BottomRight, FontStyles.Bold);
        Place(clockLine.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-2f, 0f), new Vector2(SignWidth * .5f, 22f));
        UiSkin.Shadowed(clockLine);
        track = Box("Day bar", sign, Vector2.zero, Vector2.zero, new Vector2(0f, 26f), new Vector2(SignWidth, BarHeight));
        Image trackImage = UiSkin.Paint(track, new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .62f), BarHeight * .5f);
        UiSkin.DropShadow(track, 3f, 1f, .25f);
        fill = Box("So far", track, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(BarHeight, BarHeight));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, .5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = new Vector2(BarHeight, 0f);
        fillImage = UiSkin.Paint(fill, UiSkin.Brand, BarHeight * .5f);
        trackImage.raycastTarget = false;

        // The strings, from the sign's top up.
        float signBottom = 26f + BarHeight + 12f, signTop = signBottom + SignHeight;
        foreach (float x in new[] { .27f, .73f })
        {
            RectTransform cord = Box("String", sign, Vector2.zero, new Vector2(.5f, 0f), new Vector2(SignWidth * x, signTop - 8f), new Vector2(2f, 22f));
            Image c = cord.gameObject.AddComponent<Image>();
            c.color = new Color(UiSkin.BandText.r, UiSkin.BandText.g, UiSkin.BandText.b, .75f);
            c.raycastTarget = false;
        }

        // The swing hangs from the strings' ends; the flip turns the card about its middle.
        sway = Box("Swing", sign, Vector2.zero, new Vector2(.5f, 1f), new Vector2(SignWidth * .5f, signTop), new Vector2(SignWidth, SignHeight));
        flipper = Box("Turn", sway, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        signShadow = UiSkin.DropShadow(flipper, 9f, 4f, .38f);

        front = Box("OPEN side", flipper, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        frontBorder = UiSkin.Paint(front, UiSkin.Brand, 14f);
        RectTransform frontInner = Box("Face", front, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(SignWidth - 2f * Border, SignHeight - 2f * Border));
        frontFace = UiSkin.Paint(frontInner, UiSkin.Paper, 14f - Border);
        RectTransform cupRect = Box("Cup", front, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -14f), new Vector2(30f, 26f));
        cup = cupRect.gameObject.AddComponent<Image>();
        cup.sprite = MakeSprite(CupMark(), 0f);
        cup.raycastTarget = false;
        frontWord = UiSkin.Text("Word", front, 40f, UiSkin.Brand, TextAlignmentOptions.Center, FontStyles.Bold);
        frontWord.characterSpacing = 10f;
        Place(frontWord.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, -4f), new Vector2(SignWidth - 16f, 46f));
        frontSub = UiSkin.Text("Under", front, 12.5f, UiSkin.Brand, TextAlignmentOptions.Center, FontStyles.Bold);
        frontSub.characterSpacing = 16f;
        Place(frontSub.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 12f), new Vector2(SignWidth - 16f, 18f));

        back = Box("CLOSED side", flipper, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(SignWidth, SignHeight));
        backBorder = UiSkin.Paint(back, UiSkin.Red, 14f);
        RectTransform backInner = Box("Face", back, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(SignWidth - 2f * Border, SignHeight - 2f * Border));
        backFace = UiSkin.Paint(backInner, UiSkin.Ink, 14f - Border);
        backWord = UiSkin.Text("Word", back, 38f, UiSkin.RedSoft, TextAlignmentOptions.Center, FontStyles.Bold);
        backWord.characterSpacing = 8f;
        backWord.text = "CLOSED";
        Place(backWord.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 8f), new Vector2(SignWidth - 16f, 46f));
        backSub = UiSkin.Text("Under", back, 12.5f, UiSkin.BandFaint, TextAlignmentOptions.Center, FontStyles.Bold);
        backSub.characterSpacing = 16f;
        backSub.text = "FINISHING UP";
        Place(backSub.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 14f), new Vector2(SignWidth - 16f, 18f));
        back.gameObject.SetActive(false);
        ShowState(State.Open, false);
    }

    void BuildChips(RectTransform parent)
    {
        chips = Box("Stock", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-UiSkin.Margin, 26f), new Vector2(320f, 100f));
        cupsChip = MakeChip("Cups", 0);
        beansChip = MakeChip("Beans", 1);
    }

    Chip MakeChip(string name, int row)
    {
        var chip = new Chip();
        chip.rect = Box(name, chips, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, row * 44f), new Vector2(220f, 36f));
        chip.face = UiSkin.Paint(chip.rect, UiSkin.Accent, 18f);
        chip.glow = UiSkin.DropShadow(chip.rect, 12f, 3f, .3f);
        chip.glowRect = chip.glow.rectTransform;
        chip.text = UiSkin.Text("Words", chip.rect, 18f, UiSkin.Paper, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchInside(chip.text.rectTransform, 0f);
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
        bool show = clock != null && !clock.DayOver && !night && !RecapUI.Showing;
        if (canvas.enabled != show) canvas.enabled = show;
        if (!show)
        {
            top.alpha = 0f;
            bottom.alpha = 0f;
            wasShown = false;
            return;
        }
        float dt = UiClock.Delta;
        top.alpha = 1f;
        bool busy = BottomBusy();
        bottom.alpha = wasShown ? Mathf.MoveTowards(bottom.alpha, busy ? 0f : 1f, dt * FadeRate) : busy ? 0f : 1f;
        wasShown = true;

        UpdateCash(clock);
        UpdateSign(clock);
        UpdateChips();
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

    // ------------------------------------------------------------------ the cash stack

    void UpdateCash(DayClock clock)
    {
        int money = Mathf.Max(0, clock.Earned);
        float now = UiClock.Now;
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
            // Paid: the pop, the jolt, the drop, the glow, and the figure counting up.
            Paid(money - targetMoney, now);
            fromMoney = shownMoney;
            targetMoney = money;
            countSince = now;
            SetEdges(money);
        }

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
        float jolt = since < JoltSeconds ? -3f * Mathf.Sin(Mathf.Clamp01(since / JoltSeconds) * Mathf.PI) : 0f;
        stack.anchoredPosition = new Vector2(stack.anchoredPosition.x, -6f + jolt);
        if (since < DropSeconds)
        {
            float k = Mathf.Clamp01(since / DropSeconds);
            // From above and tilted, past its place a little and back (a settle).
            float y = k < .74f ? Mathf.Lerp(46f, -3f, Ease(k / .74f)) : k < .88f ? Mathf.Lerp(-3f, 3f, (k - .74f) / .14f) : Mathf.Lerp(3f, 0f, (k - .88f) / .12f);
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
            // In from a little lower, a settle, then up into the stack as it fades: the money goes onto the pile.
            float rise = k < .25f ? Mathf.Lerp(-12f, 0f, k / .25f) : k < .45f ? Mathf.Lerp(0f, 4f, (k - .25f) / .2f) : Mathf.Lerp(4f, 30f, (k - .45f) / .55f);
            pop.anchoredPosition = new Vector2(0f, -StackDepth(edgesShown) - PopGap + rise);
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
        float width = Mathf.Ceil(popText.GetPreferredValues(popText.text, 999f, 30f).x) + 24f;
        pop.sizeDelta = new Vector2(Mathf.Max(54f, width), 30f);
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

    // How far down the stack reaches from its top: the bill, and the edges under it.
    static float StackDepth(int edges) => edges > 0 ? BillHeight - 2f + (edges - 1) * EdgeStep + EdgeHeight : BillHeight;

    // ------------------------------------------------------------------ the sign

    void UpdateSign(DayClock clock)
    {
        float now = UiClock.Now, dt = UiClock.Delta;
        // Last orders: the café's last hour, by its own clock.
        float lastHour = Mathf.Max(0f, clock.ClosingHour - 1f);
        State want = !clock.IsOpen ? State.Closed : clock.CurrentHour >= lastHour ? State.LastOrders : State.Open;
        if (!stateKnown || want != state)
        {
            bool animate = stateKnown && !(state == State.Closed && want != State.Closed);   // a new morning just turns back
            State was = state;
            state = want;
            stateKnown = true;
            ShowState(want, animate);
            if (animate && want == State.LastOrders && was == State.Open) Sfx.Play2D("hud.lastorders");
            if (animate && want == State.Closed) closedAt = now;
        }

        // The flip (only to CLOSED, and back at a new morning), on its own piece.
        float angle;
        if (flipSince > -5f)
        {
            float t = Mathf.Clamp01((now - flipSince) / FlipSeconds);
            angle = Mathf.LerpUnclamped(flipFrom, flipTo, BackOut(t));
            if (t >= 1f) { flipSince = -10f; angle = flipTo; }
        }
        else angle = state == State.Closed ? 180f : 0f;
        float face = Mathf.Cos(angle * Mathf.Deg2Rad);
        bool backShowing = face < 0f;
        if (front.gameObject.activeSelf == backShowing) front.gameObject.SetActive(!backShowing);
        if (back.gameObject.activeSelf != backShowing) back.gameObject.SetActive(backShowing);
        flipper.localScale = new Vector3(Mathf.Max(.02f, Mathf.Abs(face)), 1f, 1f);
        UiSkin.Follow(signShadow.rectTransform, flipper, 9f, 4f);

        // The sway, on the swing: gentle when open, quicker and wider at last orders, still once closed.
        float amplitude = state == State.Open ? 1f : state == State.LastOrders ? 2.8f : 0f;
        float period = state == State.LastOrders ? 1.15f : 5.5f;
        swayPhase += dt * Mathf.PI * 2f / period;
        float wanted = amplitude * Mathf.Sin(swayPhase);
        float current = sway.localEulerAngles.z > 180f ? sway.localEulerAngles.z - 360f : sway.localEulerAngles.z;
        sway.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(current, wanted, 1f - Mathf.Exp(-10f * dt)));

        // The day's bar, and its colour by the state.
        float day = Mathf.Clamp01(clock.NormalizedDay);
        float width = Mathf.Max(BarHeight, SignWidth * day);
        fill.offsetMax = new Vector2(width, 0f);
        Color barColour = state == State.Closed ? UiSkin.Red : state == State.LastOrders ? UiSkin.Gold : UiSkin.Brand;
        fillImage.color = Color.Lerp(fillImage.color, barColour, 1f - Mathf.Exp(-6f * dt));

        // Round the edge: an amber pulse at last orders; at closing a red glow that blooms and fades.
        float sinceClosed = now - closedAt;
        Color v = vignette.color;
        if (state == State.Closed && sinceClosed < ClosedGlowSeconds)
        {
            float k = sinceClosed / ClosedGlowSeconds;
            v = new Color(UiSkin.Red.r, UiSkin.Red.g, UiSkin.Red.b, k < .12f ? .22f * k / .12f : .22f * (1f - (k - .12f) / .88f));
        }
        else
        {
            float pulse = state == State.LastOrders ? .10f + .07f * (.5f + .5f * Mathf.Sin(now * Mathf.PI * 2f / 2.2f)) : 0f;
            if (state != State.Closed && (v.r != UiSkin.Gold.r || v.g != UiSkin.Gold.g)) v = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0f);
            v.a = Mathf.MoveTowards(v.a, state == State.Closed ? 0f : pulse, dt * .6f);
        }
        vignette.color = v;

        // The day and the time, built only when the minute changes.
        int minute = Mathf.FloorToInt(Mathf.Clamp(clock.CurrentHour, 0f, 24f) * 60f + .001f);
        if (clock.Day != shownDay || minute != shownMinute)
        {
            shownDay = clock.Day;
            shownMinute = minute;
            dayLine.text = Weekdays.Label(clock.Day);
            clockLine.text = ShopUI.FormatHour(clock.CurrentHour);
        }
        int closing = Mathf.RoundToInt(clock.ClosingHour * 60f);
        if (closing != shownClosingHour)
        {
            shownClosingHour = closing;
            if (state == State.LastOrders) frontSub.text = "CLOSING AT " + ClosingWords(clock.ClosingHour);
        }
    }

    void ShowState(State s, bool animate)
    {
        bool lastOrders = s == State.LastOrders;
        frontBorder.color = lastOrders ? UiSkin.Gold : UiSkin.Brand;
        frontFace.color = lastOrders ? UiSkin.GoldSoft : UiSkin.Paper;
        frontWord.text = lastOrders ? "LAST ORDERS" : "OPEN";
        frontWord.fontSize = lastOrders ? 23f : 40f;          // "LAST ORDERS" with room either side of it on the card
        frontWord.characterSpacing = lastOrders ? 2f : 10f;
        frontWord.color = lastOrders ? UiSkin.GoldInk : UiSkin.Brand;
        frontSub.color = lastOrders ? UiSkin.GoldInk : new Color(UiSkin.Brand.r, UiSkin.Brand.g, UiSkin.Brand.b, .8f);
        DayClock clock = DayClock.Instance;
        frontSub.text = lastOrders && clock != null ? "CLOSING AT " + ClosingWords(clock.ClosingHour) : "COME IN";
        cup.color = UiSkin.Brand;
        cup.enabled = !lastOrders;
        frontWord.rectTransform.anchoredPosition = new Vector2(0f, lastOrders ? 6f : -4f);

        float target = s == State.Closed ? 180f : 0f;
        if (animate)
        {
            float current = flipSince > -5f ? flipTo : (s == State.Closed ? 0f : 180f);
            if (Mathf.Abs(current - target) > 1f)
            {
                flipFrom = current;
                flipTo = target;
                flipSince = UiClock.Now;
            }
        }
        else flipSince = -10f;
    }

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

    // ------------------------------------------------------------------ the stock chips

    void UpdateChips()
    {
        ShopInventory stock = ShopInventory.Instance;
        float now = UiClock.Now;
        bool lab = CafeLab.Active;
        chips.anchoredPosition = new Vector2(-UiSkin.Margin, 26f + (lab ? LabNudge : 0f));
        int row = 0;
        ChipFor(cupsChip, stock != null && stock.CupsLow, stock != null ? stock.Cups : 0, "cups", now, ref row);
        ChipFor(beansChip, stock != null && stock.BeansLow, stock != null ? stock.Beans : 0, "beans", now, ref row);
    }

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
            chip.text.text = count <= 0 ? $"Out of {what}" : $"Low on {what} · {count} left";
            chip.face.color = count <= 0 ? UiSkin.Red : UiSkin.Accent;
            float width = Mathf.Ceil(chip.text.GetPreferredValues(chip.text.text, 999f, 36f).x) + 32f;
            chip.rect.sizeDelta = new Vector2(width, 36f);
        }
        // In from the right with a spring, then a slow glow.
        float t = Mathf.Clamp01((now - chip.since) / ChipInSeconds);
        float u = t - 1f;
        float spring = 1f + 2.4f * u * u * u + 1.4f * u * u;
        chip.rect.anchoredPosition = new Vector2(16f * (1f - spring), row * 44f);
        float s = Mathf.LerpUnclamped(.85f, 1f, spring);
        chip.rect.localScale = new Vector3(s, s, 1f);
        UiSkin.Follow(chip.glowRect, chip.rect, 12f, 3f);
        Color tint = chip.face.color;
        float glow = .22f + .28f * (.5f + .5f * Mathf.Sin(now * Mathf.PI * 2f / 1.7f));
        chip.glow.color = new Color(tint.r * .6f, tint.g * .4f, tint.b * .3f, glow * t);
        row++;
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

    // Clear in the middle, coloured toward the screen's edge (stretched over the whole screen).
    static Texture2D Vignette()
    {
        const int s = 128;
        return Draw("Vignette", s, s, (x, y) =>
        {
            float d = new Vector2((x - s / 2f) / (s / 2f), (y - s / 2f) / (s / 2f)).magnitude;
            float a = Mathf.Clamp01((d - .62f) / .62f);
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });
    }
}
