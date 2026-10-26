using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE MONEY: ACE'S JAR (26 Oct 2026 (HUD 3))
//
// His notes. 22 Oct: "I do like the way that the money icon is showing. However, I would like it to look more juicy, to
// look better." 24 Oct: "I also am not a fan of the way the cash just overall looks, and feels, lets see how other games
// make their money icon and overall feel with the juice and the animation for these cash logo, and lets replicate it with
// aces cafe in mind." 26 Oct, on round 2's three treatments: "im only picking till because its the only good option given,
// however i still am not a huge fan of the work and would like a better revised one."
//
// What the best-loved cozy games do (designer M2's research, 14 games): money is a thing (Animal Crossing's bags, Cult of
// the Lamb's coins), it travels from where it was earned to where it is kept (Cult of the Lamb), it lands with a beat and
// then counts (Stardew's wheels; Nijman's beat of stillness), the tip is its own beat a note higher (Overcooked, Balatro),
// something grows through the day (permanence), and spending looks and sounds different from earning (Balatro's red,
// PlateUp!'s calm planning). Too much is as bad as too little (Kao, 2020): medium-high juice, never extreme.
//
// So the money is a JAR: the glass jar every café keeps by the till, with his bills and gold coins in it, standing at the
// left end of a small painted board (the sign's family: deep green, notched corners, a gilt line) with the day's figure in
// 40 px cream and a gilt "$". The jar FILLS through the day (one step for every $24, ten steps to full: the permanence the
// old stack's edges carried). A payment: his bill flies from the pay pop over the customer and drops into the jar; the jar
// gives a little (a squash), the coins jostle, a gilt glow and four sparkles; the figure holds a beat, then counts up with
// a kick (Kick: 1 + 0.28 e^(-8t) sin 22t); a receipt prints the pay from the board's foot, then the tip's line in gold after
// the tip's coin has flown in and clinked (its own, smaller beat); the receipt holds so it can be read, then tears off. Spending
// (at closing: a piece of furniture, the Shop) is the same jar, quieter: the board dips instead of punching, the top bill
// lifts out of the jar and glides to the thing bought, the digits turn plum-pink and count DOWN, the receipt prints "-$80"
// in plum, the fill drops a step; no bell, a falling note. A café guest's $5 counts up quietly with no flight (playtest 4).
//
// One object everywhere (his answer h5): HudCorners' column by day (today's takings), its bench strip (small), and the
// Arrange screen's till row at closing (the till's money). It is built under any RectTransform and stepped by its owner on
// the owner's clock; the flights, ghosts and sparkles live on a full-screen layer the owner gives it, so a bill can travel
// from anywhere on the screen.
//
// THE SPEC, IN MS FROM THE PAY POP'S APPEARANCE (t0; without a pop, the landing happens at once):
//   180-660  the bill flies (a quadratic Bezier lifted 95 px; u = 0.2 OutQuad + 0.8 InQuad: it leaves gently and arrives
//            four times faster), from half size to full over the first 25 % (OutBack), turning -24° to -6°; two ghosts trail
//   660      it lands: the jar squashes (x 1.06, y 0.92, back by 220 ms, OutBack), the coins jostle 3 px, the board jolts
//            3 px down and back (220 ms), a gilt glow fades over 500 ms, 4 sparkles for 420 ms; hud.money.land
//   700-1320 the figure counts up (OutCubic), kicking 0.28 at 700 (peak 1.17 at 755); a tick a step (hud.money.tick)
//   740/860/980 the receipt prints "+$12" in three printer steps (hud.money.print)
//   500-1020 the tip's coin flies (lifted 150 px), spinning 3.2 turns a second; 1020 it lands: a smaller squash, 3 sparkles,
//            money.tip; the figure adds the tip 1020-1320 (OutCubic) with a kick of 0.12
//   1100     "tip +$3" prints under it (120 ms)
//   until 1950 the receipt holds (about 0.85 s with both lines up; 1.6 s in all); 1950-2350 it tears: -6°, 26 px down
//            (InQuad 250), fading (OutQuad 400); hud.money.tear
// SPENDING, FROM THE PURCHASE: 0-320 the board dips (Squish); 0-100 the top bill lifts 6 px; 100-580 it glides to the thing
//   (OutQuad, +30°, shrinking 30 %, fading over its last 20 %); 60-660 the figure counts down in plum-pink (OutQuad), cream
//   again 200 ms later; 120/220/320 "-$80" prints in plum; 1200-1600 the receipt tears; the fill drops; hud.money.spend.
// ---------------------------------------------------------------------------
public sealed class HudMoney
{
    public const float BoardW = 246f, BoardH = 64f, JarW = 48f, JarH = 60f, JarX = 10f, JarTop = -14f;
    public const float FigureSize = 40f, SmallFigureSize = 30f, FigureRight = 14f, FigureLeft = 66f;
    public const float ReceiptW = 104f, ReceiptX = 70f, ReceiptLine = 28f, ReceiptPad = 6f, ReceiptSize = 24f;
    public const int DollarsPerStep = 24, Steps = 10;
    const float ContentsH = 44f;   // the jar's body, where the money piles up (of its 60)

    public static readonly Color Gilt = HudArt.Hex(0xF4D58D), PlumPink = HudArt.Hex(0xF2C9CF), Plum = HudArt.Hex(0x7D3A44), DeepGold = HudArt.Hex(0x8C5008);
    static readonly Color Green = HudArt.Hex(0x1D4F3A), GreenLit = HudArt.Hex(0x2C6A4E), GreenDark = HudArt.Hex(0x143829);

    // ------------------------------------------------------------------ what the owner and the labs read

    public RectTransform Root { get; private set; }
    /// <summary>The figure as it reads now, plain ("$142").</summary>
    public string Figure => "$" + shown;
    public int Shown => shown;
    public int Target => target;
    /// <summary>The figure's size now (1 at rest; more during a kick).</summary>
    public float Punch => figure != null ? figure.rectTransform.localScale.x : 1f;
    /// <summary>The jar's fill in steps (one for every $24, ten at most): the old stack's edges.</summary>
    public int FillSteps => fillSteps;
    /// <summary>True once there's money in the jar (a bill shows in it).</summary>
    public bool BillShowing => fillSteps > 0 || target > 0;
    /// <summary>True while a bill or a coin is in the air.</summary>
    public bool FlightUp => billFlying || coinFlying || spendFlying;
    public bool ReceiptShowing => receiptGroup != null && receiptGroup.alpha > .05f && receipt.gameObject.activeSelf;
    /// <summary>The receipt's lines as they read ("+$12 | tip +$3"), or "".</summary>
    public string ReceiptText => ReceiptShowing ? (line1.text + (line2.gameObject.activeSelf && line2.text.Length > 0 ? " | " + line2.text : "")).Replace("−", "-") : "";
    /// <summary>The digits' colour now: cream, or plum-pink while spending.</summary>
    public Color DigitsColour => figure != null ? figure.color : Color.white;
    public RectTransform JarRect => jar;

    // ------------------------------------------------------------------ the pieces

    RectTransform board, jar, jarWobble, contentsBox, contents, figureRect, receipt, receiptBody, flightLayer, glowRect;
    Image glow;
    Image boardShadow;
    CanvasGroup boardGroup, receiptGroup;
    TextMeshProUGUI figure, line1, line2;
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    Image bill, ghost1, ghost2, coin, spendBill;

    sealed class Sparkle { public Image image; public Vector2 from, velocity; public float born; public bool live; }

    int shown = int.MinValue, target, from, fillSteps = -1;
    float fillShown, fillTarget;
    float countStart = -10f, countSeconds = .62f, kickAt = -10f, kickAmount, landAt = -10f, tipLandAt = -10f, dipAt = -10f, spendAt = -10f, plumUntil = -10f;
    bool countDown;
    UiTween.Ease countEase = UiTween.Ease.OutCubic;
    // The flights.
    bool billFlying, coinFlying, spendFlying;
    float billStart, billEnd, coinStart, coinEnd, spendStart, spendEnd;
    Vector2 billFrom, coinFrom, spendTo;
    // The receipt.
    float receiptPrintAt = -10f, receiptLine2At = -10f, receiptTearAt = -10f;
    int receiptLines;
    bool receiptPlum;
    float compact;
    float jostle;

    // ------------------------------------------------------------------ making it

    /// <summary>
    /// Built under <paramref name="parent"/>: the board's top-left corner at <paramref name="at"/> from its anchor. The flights
    /// and sparkles go on <paramref name="flights"/>, a RectTransform stretched over the whole canvas (so a bill can start anywhere).
    /// </summary>
    public void Build(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 at, RectTransform flights)
    {
        flightLayer = flights;
        Root = HudWindow.Box(name, parent, anchor, pivot, at, new Vector2(BoardW, BoardH));
        board = HudWindow.Box("Board", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BoardW, BoardH));
        boardGroup = board.gameObject.AddComponent<CanvasGroup>();
        boardGroup.blocksRaycasts = false; boardGroup.interactable = false;
        boardShadow = UiSkin.DropShadow(board, 9f, 5f, .4f);
        Image boardFace = board.gameObject.AddComponent<Image>();
        boardFace.sprite = BoardSprite();
        boardFace.raycastTarget = false;

        // The receipt hangs from the board's foot, under the figure's start; it prints downward inside its clip.
        receipt = HudWindow.Box("Receipt", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ReceiptX, -BoardH + 2f), new Vector2(ReceiptW, 0f));
        receiptGroup = receipt.gameObject.AddComponent<CanvasGroup>();
        receiptGroup.blocksRaycasts = false; receiptGroup.interactable = false;
        receipt.gameObject.AddComponent<RectMask2D>();
        receiptBody = HudWindow.Box("Paper", receipt, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ReceiptW, 2f * ReceiptLine + ReceiptPad));
        Image receiptPaper = receiptBody.gameObject.AddComponent<Image>();
        receiptPaper.color = UiSkin.Paper;
        receiptPaper.raycastTarget = false;
        RectTransform foot = HudWindow.Box("Torn foot", receiptBody, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ReceiptW, 10f));
        Image receiptFoot = foot.gameObject.AddComponent<Image>();
        receiptFoot.sprite = HudArt.TornFoot();
        receiptFoot.type = Image.Type.Tiled;
        receiptFoot.color = UiSkin.Paper;
        receiptFoot.raycastTarget = false;
        line1 = UiSkin.Text("Line 1", receiptBody, ReceiptSize, UiSkin.BrandDeep, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        HudWindow.Place(line1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(ReceiptW - 20f, ReceiptLine));
        line2 = UiSkin.Text("Line 2", receiptBody, ReceiptSize, DeepGold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        HudWindow.Place(line2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -2f - ReceiptLine), new Vector2(ReceiptW - 20f, ReceiptLine));
        line2.gameObject.SetActive(false);
        receipt.gameObject.SetActive(false);

        // The jar: its back, the contents (clipped from the bottom up by the day's share), its front; it wobbles about its foot.
        jar = HudWindow.Box("Jar", Root, new Vector2(0f, 1f), new Vector2(.5f, 0f), new Vector2(JarX + JarW * .5f, JarTop - JarH), new Vector2(JarW, JarH));
        glowRect = HudWindow.Box("Glow", jar, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 4f), new Vector2(120f, 120f));
        glow = glowRect.gameObject.AddComponent<Image>();
        glow.sprite = HudArt.Glow();
        glow.color = HudArt.With(Gilt, 0f);
        glow.raycastTarget = false;
        jarWobble = HudWindow.Box("Wobble", jar, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(JarW, JarH));
        Picture("Jar back", jarWobble, HudArt.JarBack());
        contentsBox = HudWindow.Box("Contents (the day's share)", jarWobble, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(JarW, 0f));
        contentsBox.gameObject.AddComponent<RectMask2D>();
        contents = HudWindow.Box("Contents", contentsBox, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(JarW, JarH));
        Image contentsImage = contents.gameObject.AddComponent<Image>();
        contentsImage.sprite = HudArt.JarContents();
        contentsImage.raycastTarget = false;
        Picture("Jar front", jarWobble, HudArt.JarFront());

        // The figure: cream digits with a gilt "$", pivoted at its right middle, so a kick grows it out from the board's edge.
        figure = UiSkin.Text("Figure", board, FigureSize, UiSkin.Paper, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        figureRect = figure.rectTransform;
        HudWindow.Place(figureRect, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-FigureRight, 0f), new Vector2(BoardW - FigureLeft - FigureRight, BoardH));

        // The flights: a bill, its two ghosts, a coin, and the bill that leaves when spending; sparkles made as needed.
        ghost2 = Flyer("Bill ghost 2", HudArt.Bill(), new Vector2(58f, 28f) * .72f, .16f);
        ghost1 = Flyer("Bill ghost 1", HudArt.Bill(), new Vector2(58f, 28f) * .86f, .26f);
        bill = Flyer("Bill in flight", HudArt.Bill(), new Vector2(58f, 28f), 1f);
        coin = Flyer("Coin in flight", HudArt.Coin(), new Vector2(22f, 22f), 1f);
        spendBill = Flyer("Bill leaving", HudArt.Bill(), new Vector2(58f, 28f), 1f);
    }

    static Image Picture(string name, RectTransform parent, Sprite sprite)
    {
        RectTransform r = HudWindow.Box(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = sprite;
        i.raycastTarget = false;
        return i;
    }

    Image Flyer(string name, Sprite sprite, Vector2 size, float alpha)
    {
        RectTransform r = HudWindow.Box(name, flightLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size);
        var i = r.gameObject.AddComponent<Image>();
        i.sprite = sprite;
        i.color = new Color(1f, 1f, 1f, alpha);
        i.raycastTarget = false;
        r.gameObject.SetActive(false);
        return i;
    }

    // The board: deep green with notched corners (the sign's, in miniature), a gilt line inset, a lit lip top-left, a dark edge
    // bottom-right, a slot at its foot where the receipt comes out.
    static Sprite BoardSprite() => HudArt.Cached("Money board", () => HudArt.SpriteOf(HudArt.Draw("Money board", (int)BoardW, (int)BoardH, (x, y) =>
    {
        const float notch = 7f;
        float box = HudArt.RoundBox(x, y, BoardW * .5f, BoardH * .5f, BoardW * .5f - .5f, BoardH * .5f - .5f, 2f);
        // The notches: the corners cut at 45°.
        float cornerCut = Mathf.Min(Mathf.Min(x + y, (BoardW - x) + y), Mathf.Min(x + (BoardH - y), (BoardW - x) + (BoardH - y))) - notch;
        float shape = Mathf.Max(box, -cornerCut);
        if (shape > 1f) return Color.clear;
        float a = HudArt.Inside(shape);
        Color c = HudArt.With(Green, a);
        float edge = Mathf.Clamp01(1f - (-shape) / 2f);
        bool lit = y > BoardH * .5f || x < 12f;
        c = HudArt.Over(c, HudArt.With(lit ? GreenLit : GreenDark, .75f * edge));
        // The gilt line, 4 px in, following the notches.
        float inner = Mathf.Max(HudArt.RoundBox(x, y, BoardW * .5f, BoardH * .5f, BoardW * .5f - 4.5f, BoardH * .5f - 4.5f, 1.5f), -(cornerCut - 4f * 1.414f));
        c = HudArt.Over(c, HudArt.With(Gilt, .9f * HudArt.Line(inner, .7f)));
        // The slot at the foot, under where the receipt hangs.
        float slot = HudArt.RoundBox(x, y, ReceiptX + ReceiptW * .5f, 3f, ReceiptW * .5f - 6f, 1f, 1f);
        c = HudArt.Over(c, HudArt.With(GreenDark, .9f * HudArt.Inside(slot)));
        return HudArt.With(c, a);
    })));

    // ------------------------------------------------------------------ what the owner tells it

    /// <summary>The first look (a day loaded): the figure as it is, no counting.</summary>
    public void Show(int money)
    {
        shown = target = from = Mathf.Max(0, money);
        countStart = -10f;
        WriteFigure();
        SetFill(money, true);
    }

    /// <summary>A quiet rise (a café guest's coffee): counts up over 0.55 s, no beat, no flight (playtest 4).</summary>
    public void Quiet(int money, float now)
    {
        if (shown == int.MinValue) { Show(money); return; }
        if (!landed && landAt > -5f && now < landAt)
        {
            // A guest's coffee while a bill is in the air: it joins the landing's count.
            pendingFrom += Mathf.Max(0, money) - target;
            target = Mathf.Max(0, money);
            SetFill(money, false, landAt);
            return;
        }
        from = shown; target = Mathf.Max(0, money);
        countStart = now; countSeconds = .55f; countDown = false; countEase = UiTween.Ease.OutQuad;
        SetFill(money, false);
    }

    /// <summary>
    /// A payment of <paramref name="amount"/> (and <paramref name="tip"/>) landing on <paramref name="money"/>. With a pop on the
    /// screen (<paramref name="fromScreen"/>, pixels; <paramref name="popAt"/> the time it appears on the owner's clock) the bill
    /// and the coin fly from it; without, the landing happens now.
    /// </summary>
    public void Pay(int money, int amount, int tip, Vector2? fromScreen, float popAt, float now)
    {
        if (shown == int.MinValue) Show(Mathf.Max(0, money - amount - tip));
        if (!landed && landAt > -5f && now < landAt)
        {
            // A second payment before the first has landed (a rush): it rides the same landing and the same receipt.
            pendingAmount += amount;
            pendingTip += tip;
            target = Mathf.Max(0, money);
            line1.text = "+$" + pendingAmount;
            line2.text = pendingTip > 0 ? "tip +$" + pendingTip : "";
            if (pendingTip > 0 && tipLandAt < -5f) { tipLandAt = landAt + .36f; receiptLine2At = landAt + .44f; receiptTearAt = landAt + 1.29f; }
            SetFill(money, false, landAt);
            return;
        }
        int before = target;
        target = Mathf.Max(0, money);
        float t0 = fromScreen.HasValue ? popAt : now - .66f;
        if (fromScreen.HasValue && ToLayer(fromScreen.Value, out Vector2 at))
        {
            billFrom = at;
            billFlying = true;
            billStart = t0 + .18f; billEnd = t0 + .66f;
            if (tip > 0)
            {
                coinFrom = at + new Vector2(40f, -8f);   // the pop's "+$3 tip" sits to the right, a little lower
                coinFlying = true;
                coinStart = t0 + .5f; coinEnd = t0 + 1.02f;
            }
        }
        landAt = t0 + .66f;
        pendingAmount = amount; pendingTip = tip; pendingFrom = before;
        tipLandAt = tip > 0 ? t0 + 1.02f : -10f;
        landed = false; tipLanded = false;
        // The receipt: the pay's line, then the tip's, then the tear.
        receiptLines = 0;
        receiptPlum = false;
        line1.text = "+$" + amount;
        line1.color = UiSkin.BrandDeep;
        line2.text = tip > 0 ? "tip +$" + tip : "";
        line2.color = DeepGold;
        receiptPrintAt = t0 + .74f;
        receiptLine2At = tip > 0 ? t0 + 1.1f : -10f;
        receiptTearAt = t0 + (tip > 0 ? 1.95f : 1.6f);
        receiptGroup.alpha = 1f;
        receipt.localRotation = Quaternion.identity;
        receipt.anchoredPosition = new Vector2(ReceiptX, -BoardH + 2f);
        SetFill(money, false, landAt);
    }

    int pendingAmount, pendingTip, pendingFrom;
    bool landed, tipLanded;
    float fillAt = -10f;

    /// <summary>Spending <paramref name="amount"/> (the figure goes to <paramref name="money"/>); the bill leaves for <paramref name="toScreen"/> if given.</summary>
    public void Spend(int money, int amount, Vector2? toScreen, float now)
    {
        if (shown == int.MinValue) Show(money + amount);
        from = shown; target = Mathf.Max(0, money);
        spendAt = now;
        dipAt = now;
        countStart = now + .06f; countSeconds = .6f; countDown = true; countEase = UiTween.Ease.OutQuad;
        plumUntil = now + .86f;
        figure.color = PlumPink;
        if (toScreen.HasValue && ToLayer(toScreen.Value, out Vector2 at))
        {
            spendTo = at;
            spendFlying = true;
            spendStart = now + .1f; spendEnd = now + .58f;
        }
        receiptLines = 0;
        receiptPlum = true;
        line1.text = "−$" + amount;
        line1.color = Plum;
        line2.text = "";
        receiptPrintAt = now + .12f;
        receiptLine2At = -10f;
        receiptTearAt = now + 1.2f;
        receiptGroup.alpha = 1f;
        receipt.localRotation = Quaternion.identity;
        receipt.anchoredPosition = new Vector2(ReceiptX, -BoardH + 2f);
        Sfx.Play2D("hud.money.spend");
        SetFill(money, false, now + .1f);
    }

    /// <summary>The bench's small form (0: the board by day; 1: the jar at half size beside a 30 px figure, no board, no receipt).</summary>
    public void SetCompact(float k)
    {
        k = Mathf.Clamp01(k);
        if (Mathf.Approximately(k, compact)) return;
        compact = k;
        boardGroup.alpha = 1f - k;
        boardShadow.color = new Color(0f, 0f, 0f, .4f * (1f - k));
        float js = Mathf.Lerp(1f, .5f, k);
        jar.localScale = new Vector3(js, js, 1f);
        jar.anchoredPosition = Vector2.Lerp(new Vector2(JarX + JarW * .5f, JarTop - JarH), new Vector2(JarW * .25f + 2f, -BoardH * .5f - JarH * .25f), k);
        figure.fontSize = Mathf.Lerp(FigureSize, SmallFigureSize, k);
        figureRect.anchoredPosition = new Vector2(-FigureRight, 0f);
        figureRect.sizeDelta = new Vector2(Mathf.Lerp(BoardW - FigureLeft - FigureRight, BoardW - JarW * .5f - 8f, k), BoardH);
        if (k > .5f && receipt.gameObject.activeSelf) receipt.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ every frame

    public void Tick(float now, float dt)
    {
        // Landing: the beat, then the count.
        if (!landed && landAt > -5f && now >= landAt)
        {
            landed = true;
            Land(now, 1f, 4);
            Sfx.Play2D("hud.money.land");
            from = shown;
            target = pendingTip > 0 ? Mathf.Max(0, pendingFrom + pendingAmount) : Mathf.Max(target, pendingFrom + pendingAmount);
            countStart = now + .04f; countSeconds = .62f; countDown = false; countEase = UiTween.Ease.OutCubic;
            kickAt = now + .04f; kickAmount = .28f;
        }
        if (!tipLanded && tipLandAt > -5f && now >= tipLandAt)
        {
            tipLanded = true;
            Land(now, .8f, 3);
            Sfx.Play2D("money.tip");
            from = shown;
            target = Mathf.Max(target, pendingFrom + pendingAmount + pendingTip);
            countStart = now; countSeconds = .3f; countDown = false; countEase = UiTween.Ease.OutCubic;
            kickAt = now; kickAmount = .12f;
        }
        // The count.
        if (countStart > -5f && now >= countStart)
        {
            float t = Mathf.Clamp01((now - countStart) / countSeconds);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, target, UiTween.Eased(countEase, t)));
            if (value != shown)
            {
                if (!countDown && kickAt > -5f && value > shown) Sfx.Play2D("hud.money.tick", .6f);
                shown = value;
                WriteFigure();
            }
            if (t >= 1f) { countStart = -10f; shown = target; WriteFigure(); }
        }
        if (plumUntil > -5f && now >= plumUntil) { plumUntil = -10f; figure.color = UiSkin.Paper; }
        else if (plumUntil > -5f) figure.color = Color.Lerp(PlumPink, UiSkin.Paper, Mathf.Clamp01((now - (plumUntil - .2f)) / .2f));
        // The kick and the dip.
        float scale = 1f;
        if (kickAt > -5f)
        {
            float tau = now - kickAt;
            if (tau >= 0f && tau < .6f) scale *= 1f + kickAmount * Mathf.Exp(-8f * tau) * Mathf.Sin(22f * tau);
            else if (tau >= .6f) kickAt = -10f;
        }
        float boardScale = 1f;
        if (dipAt > -5f)
        {
            float t = (now - dipAt) / .32f;
            if (t >= 0f && t < 1f) boardScale = Mathf.LerpUnclamped(1f, UiTween.SquishLow, UiTween.Eased(UiTween.Ease.Squish, t));
            else if (t >= 1f) dipAt = -10f;
        }
        figureRect.localScale = new Vector3(scale, scale, 1f);
        // The jar's squash and the coins' jostle, from the last landing.
        float since = now - lastLandAt;
        float sx = 1f, sy = 1f;
        if (lastLandAt > -5f && since < .3f)
        {
            float k = UiTween.Eased(UiTween.Ease.OutBack, Mathf.Clamp01(since / .22f));
            sx = Mathf.Lerp(1f + .06f * lastLandStrength, 1f, k);
            sy = Mathf.Lerp(1f - .08f * lastLandStrength, 1f, k);
            jostle = Mathf.Sin(Mathf.Clamp01(since / .22f) * Mathf.PI) * 3f * lastLandStrength;
        }
        else jostle = 0f;
        jarWobble.localScale = new Vector3(sx, sy, 1f);
        contents.anchoredPosition = new Vector2(0f, jostle);
        float jolt = lastLandAt > -5f && since < .22f ? -3f * Mathf.Sin(Mathf.Clamp01(since / .22f) * Mathf.PI) * lastLandStrength : 0f;
        board.anchoredPosition = new Vector2(0f, jolt);
        board.localScale = new Vector3(boardScale, boardScale, 1f);
        UiSkin.Follow(boardShadow.rectTransform, board, 9f, 5f);
        float glowA = lastLandAt > -5f && since < .5f ? (1f - since / .5f) * .9f * lastLandStrength : 0f;
        glow.color = HudArt.With(Gilt, glowA);
        glowRect.localScale = Vector3.one * Mathf.Lerp(.5f, 1.3f, lastLandAt > -5f ? Mathf.Clamp01(since / .5f) : 0f);
        // The fill.
        if (fillAt > -5f && now >= fillAt)
        {
            float t = Mathf.Clamp01((now - fillAt) / .3f);
            fillShown = Mathf.Lerp(fillFrom, fillTarget, UiTween.Eased(UiTween.Ease.OutCubic, t));
            if (t >= 1f) fillAt = -10f;
            contentsBox.sizeDelta = new Vector2(JarW, ContentsH * fillShown + (fillShown > 0f ? 10f : 0f));
        }
        TickFlights(now);
        TickReceipt(now);
        TickSparkles(now);
    }

    float lastLandAt = -10f, lastLandStrength = 1f, fillFrom;

    void Land(float now, float strength, int sparks)
    {
        lastLandAt = now;
        lastLandStrength = strength;
        Vector2 mouth = Mouth();
        for (int i = 0; i < sparks; i++)
        {
            Sparkle s = TakeSparkle();
            float a = Mathf.PI * (.25f + .5f * i / Mathf.Max(1, sparks - 1)) + (i % 2 == 0 ? .2f : -.2f);
            s.from = mouth;
            s.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (150f + 40f * (i % 2));
            s.born = now;
            s.live = true;
            s.image.gameObject.SetActive(true);
        }
    }

    // The jar's mouth, in the flight layer's space.
    Vector2 Mouth()
    {
        if (flightLayer == null || jar == null) return Vector2.zero;
        Vector3 world = jar.TransformPoint(new Vector3(0f, JarH - 6f, 0f));
        Vector3 local = flightLayer.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    bool ToLayer(Vector2 screen, out Vector2 local)
    {
        local = Vector2.zero;
        return flightLayer != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(flightLayer, screen, null, out local);
    }

    // The flight's way along its curve: it leaves gently and arrives four times faster, pulled into the jar.
    static float FlightU(float k) => .2f * UiTween.Eased(UiTween.Ease.OutQuad, k) + .8f * UiTween.Eased(UiTween.Ease.InQuad, k);

    static Vector2 Bezier(Vector2 a, Vector2 b, float lift, float u)
    {
        Vector2 c = Vector2.Lerp(a, b, .33f) + new Vector2(0f, Mathf.Max(a.y, b.y) - Vector2.Lerp(a, b, .33f).y + lift);
        float v = 1f - u;
        return v * v * a + 2f * v * u * c + u * u * b;
    }

    void TickFlights(float now)
    {
        Vector2 mouth = Mouth();
        if (billFlying)
        {
            float k = Mathf.Clamp01((now - billStart) / (billEnd - billStart));
            if (now >= billStart)
            {
                PlaceBill(bill, k, mouth, 1f, 0f);
                PlaceBill(ghost1, Mathf.Max(0f, k - .06f), mouth, .86f, 0f);
                PlaceBill(ghost2, Mathf.Max(0f, k - .12f), mouth, .72f, 0f);
                // Into the jar: over the last stretch it shrinks and sinks in.
                if (k >= 1f)
                {
                    float sink = Mathf.Clamp01((now - billEnd) / .16f);
                    bill.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f, .35f, sink);
                    bill.rectTransform.anchoredPosition = mouth + new Vector2(0f, -10f * sink);
                    bill.color = new Color(1f, 1f, 1f, 1f - sink);
                    ghost1.gameObject.SetActive(false); ghost2.gameObject.SetActive(false);
                    if (sink >= 1f) { billFlying = false; bill.gameObject.SetActive(false); bill.color = Color.white; }
                }
            }
        }
        if (coinFlying)
        {
            if (now >= coinStart)
            {
                float k = Mathf.Clamp01((now - coinStart) / (coinEnd - coinStart));
                RectTransform r = coin.rectTransform;
                if (!coin.gameObject.activeSelf) coin.gameObject.SetActive(true);
                r.anchoredPosition = Bezier(coinFrom, mouth, 150f, FlightU(k));
                float spin = Mathf.Cos((now - coinStart) * 3.2f * 2f * Mathf.PI);
                r.localScale = new Vector3(Mathf.Max(.12f, Mathf.Abs(spin)), 1f, 1f);
                if (k >= 1f)
                {
                    float sink = Mathf.Clamp01((now - coinEnd) / .14f);
                    r.anchoredPosition = mouth + new Vector2(0f, -8f * sink);
                    coin.color = new Color(1f, 1f, 1f, 1f - sink);
                    if (sink >= 1f) { coinFlying = false; coin.gameObject.SetActive(false); coin.color = Color.white; }
                }
            }
        }
        if (spendFlying)
        {
            RectTransform r = spendBill.rectTransform;
            if (now < spendStart)
            {
                // Lifting out of the jar.
                float lift = Mathf.Clamp01((now - (spendStart - .1f)) / .1f);
                if (!spendBill.gameObject.activeSelf) spendBill.gameObject.SetActive(true);
                r.anchoredPosition = mouth + new Vector2(0f, 6f * UiTween.Eased(UiTween.Ease.OutQuad, lift));
                r.localScale = Vector3.one * .6f;
                r.localRotation = Quaternion.identity;
                spendBill.color = Color.white;
            }
            else
            {
                float k = Mathf.Clamp01((now - spendStart) / (spendEnd - spendStart));
                float e = UiTween.Eased(UiTween.Ease.OutQuad, k);
                r.anchoredPosition = Vector2.Lerp(mouth + new Vector2(0f, 6f), spendTo, e);
                r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 30f, e));
                r.localScale = Vector3.one * Mathf.Lerp(.6f, .42f, e);
                spendBill.color = new Color(1f, 1f, 1f, k > .8f ? 1f - (k - .8f) / .2f : 1f);
                if (k >= 1f) { spendFlying = false; spendBill.gameObject.SetActive(false); }
            }
        }
    }

    void PlaceBill(Image image, float k, Vector2 mouth, float size, float extra)
    {
        RectTransform r = image.rectTransform;
        if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
        r.anchoredPosition = Bezier(billFrom, mouth, 95f, FlightU(k));
        float grow = UiTween.Eased(UiTween.Ease.OutBack, Mathf.Clamp01(k / .25f));
        float s = Mathf.Lerp(.5f, 1f, grow) * size * (k > .85f ? Mathf.Lerp(1f, .92f, (k - .85f) / .15f) : 1f);
        r.localScale = new Vector3(s, s, 1f);
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-24f, -6f, UiTween.Eased(UiTween.Ease.OutQuad, k)));
    }

    void TickReceipt(float now)
    {
        if (receiptPrintAt < -5f) { if (receipt.gameObject.activeSelf) receipt.gameObject.SetActive(false); return; }
        if (compact > .5f) return;
        if (now < receiptPrintAt) return;
        if (!receipt.gameObject.activeSelf)
        {
            receipt.gameObject.SetActive(true);
            line2.gameObject.SetActive(false);
            Sfx.Play2D("hud.money.print", .7f);
        }
        // Line 1 in three printer steps of 60 ms; line 2 in one 120 ms step; the foot of the paper follows.
        float step = Mathf.Clamp01((now - receiptPrintAt) / .18f);
        float steps = Mathf.Floor(step * 3f) / 3f + Mathf.Clamp01((step * 3f - Mathf.Floor(step * 3f)) / .5f) / 3f;
        float lines = receiptLine2At > -5f && now >= receiptLine2At ? 1f + Mathf.Clamp01((now - receiptLine2At) / .12f) : 0f;
        if (lines > 0f && !line2.gameObject.activeSelf) { line2.gameObject.SetActive(true); Sfx.Play2D("hud.money.print", .5f); }
        // The paper comes out of the slot top line first; its torn foot (10 px of teeth under the body) comes last.
        float height = ReceiptLine * Mathf.Min(1f, steps) + (lines > 0f ? ReceiptLine * (lines - 1f) : 0f) + ReceiptPad + 10f;
        receipt.sizeDelta = new Vector2(ReceiptW, height);
        receiptBody.sizeDelta = new Vector2(ReceiptW, (lines > 0f ? 2f : 1f) * ReceiptLine + ReceiptPad);
        receiptBody.anchoredPosition = Vector2.zero;
        // The tear.
        if (receiptTearAt > -5f && now >= receiptTearAt)
        {
            float t = Mathf.Clamp01((now - receiptTearAt) / .4f);
            receipt.localRotation = Quaternion.Euler(0f, 0f, -6f * UiTween.Eased(UiTween.Ease.OutQuad, Mathf.Clamp01(t / .6f)));
            receipt.anchoredPosition = new Vector2(ReceiptX, -BoardH + 2f - 26f * UiTween.Eased(UiTween.Ease.InQuad, Mathf.Clamp01(t / .62f)));
            receiptGroup.alpha = 1f - UiTween.Eased(UiTween.Ease.OutQuad, t);
            if (t <= 0f + 1e-4f) Sfx.Play2D("hud.money.tear", .5f);
            if (t >= 1f) { receiptPrintAt = -10f; receiptTearAt = -10f; receipt.gameObject.SetActive(false); }
        }
    }

    Sparkle TakeSparkle()
    {
        foreach (Sparkle s in sparkles) if (!s.live) return s;
        var made = new Sparkle();
        RectTransform r = HudWindow.Box("Sparkle", flightLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(16f, 16f));
        made.image = r.gameObject.AddComponent<Image>();
        made.image.sprite = HudArt.Sparkle();
        made.image.color = Gilt;
        made.image.raycastTarget = false;
        r.gameObject.SetActive(false);
        sparkles.Add(made);
        return made;
    }

    void TickSparkles(float now)
    {
        foreach (Sparkle s in sparkles)
        {
            if (!s.live) continue;
            float t = (now - s.born) / .42f;
            if (t >= 1f) { s.live = false; s.image.gameObject.SetActive(false); continue; }
            float e = UiTween.Eased(UiTween.Ease.OutQuad, t);
            s.image.rectTransform.anchoredPosition = s.from + s.velocity * e * .42f + new Vector2(0f, -120f * t * t);
            float size = Mathf.Lerp(18f, 6f, t);
            s.image.rectTransform.sizeDelta = new Vector2(size, size);
            s.image.color = HudArt.With(Gilt, 1f - t * t);
        }
    }

    void SetFill(int money, bool atOnce, float at = -10f)
    {
        int steps = money <= 0 ? 0 : Mathf.Min(Steps, money / DollarsPerStep);
        fillSteps = steps;
        fillTarget = steps / (float)Steps;
        if (atOnce)
        {
            fillShown = fillTarget;
            fillAt = -10f;
            contentsBox.sizeDelta = new Vector2(JarW, ContentsH * fillShown + (fillShown > 0f ? 10f : 0f));
        }
        else
        {
            fillFrom = fillShown;
            fillAt = at > -5f ? at : 0f;
        }
    }

    void WriteFigure()
    {
        figure.text = "<color=" + UiSkin.HexOf(Gilt) + ">$</color>" + Mathf.Max(0, shown);
    }
}
