using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// ARRANGE THE CAFÉ: THE SCREEN (24 Oct 2026; furniture-spec.md §1.2, §1.3; his answers of 24 Oct)
//
// What Arrange mode (ArrangeMode) shows over the café at closing, built in code while playing and never saved, in the one UI
// skin (UiSkin) and moved on real time (UiTween: the world holds still at closing, time scale 0):
//   * THE STRIP of cards along the bottom: every kind the Shop sells (FurnitureCatalogue's order), its name, its seats and its
//     price. The chosen card lifts and wears an amber ring; a price the till can't meet turns red; a kind not on sale yet is
//     dimmed with a gold "Day 2" pill, so the player sees what's coming (§2.2);
//   * THE NOW CARD at the top left, in the Now line's look (DayOneGuideUI: the dark band, the amber edge, the title in gold):
//     "DAY 1 · YOUR CAFÉ" over "Place your first table", then "Customers can sit there tomorrow." (§1.2);
//   * THE TILL at the top right: the money (it counts down as a piece is paid for) and the seats ("+2" pops beside them);
//     26 Oct 2026 (HUD 3; his answer h5, "one till everywhere"): the money is the HUD's own jar on its board (HudMoney), the
//     same object as the day's takings top right: a purchase plays its spend (the board dips, the bill leaves the jar for the
//     piece, the figure counts down in plum, the receipt prints "-$80"); a piece sold back plays its earning from the piece
//     (no tip). The seats keep their line under it, on the dark band.
//   * THE CONTROLS STRIP at the foot: only what works now, three or four buttons as icons (ControlHints: never a button's
//     name in words), a ghost that doesn't fit greying Place;
//   * THE PLATE over the ghost or the piece pointed at: the reason first when it doesn't fit, then name · seats · price, bold
//     26 px on the dark band; pointing at a placed piece, "Hold to sell it back: +$40" with the ring that fills in 0.6 s;
//   * a puff of dust where a piece goes down or is sold.
// Text: 24 px and up (20 for a secondary line: a card's seats, the Now card's title, the Day pill), bold or semibold; over
// the world always on a plate. Inks: #4F4943 on the white cards (8.9:1), #8C4F0A on the gold pill (5.6:1), white on the
// green and red price pills (5.1:1 and 4.6:1, bold 24 px).
//
// 26 Oct 2026 (Icons 2; his note: "i wanted icons for the furniture you can also purchase too"). Each card wears its kind's
// picture (NoteIcons.Furniture, by the asset name; the icon sheet's furn_*, cut as a sprite by NoteIcons.SpriteOf) at the
// top left, 60 px, with the name under it and the seats and the price where they were: the card is taller for it (CardTallH).
// The pictures are drawn to tell the pieces apart at that size (the round bistro on its pedestal, the square table on legs,
// the plant, the bay's green booth, the window counter's pane and stools, the lounge's sofa, the communal table's row of
// chairs). Without the sheet (or a kind the sheet doesn't know) the cards are as they were: words, 120 px.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class ArrangeUI : MonoBehaviour
{
    /// <summary>Over the recap phone (60, hidden while arranging), under the juice (75) and the night's card (90).</summary>
    public const int SortingOrder = 64;

    const float CardW = 204f, CardH = 120f, CardGap = 14f, StripBottom = 100f, Lift = 14f;
    const float CardTallH = 180f, IconSize = 60f;   // 26 Oct 2026 (Icons 2): a card with its kind's picture over the name
    float cardH = CardH;                            // the cards' height as built: CardTallH with the pictures
    const float ControlsBottom = 26f, ControlsH = 58f;
    const float NowWidth = 560f, TillWidth = 260f, TillHeight = 156f;
    const float PlateMost = 620f, PlatePad = 16f, RingSize = 40f;
    const float NameSize = 24f, SmallSize = 22f,   /* 26 Oct 2026 (his Mac): 22, the house floor */ PriceSize = 24f, MoneySize = 34f, SeatsSize = 28f, ControlsSize = 24f, PlateSize = 26f, SellSize = 24f;
    const float NowSize = 26f, NowTitleSize = 20f, NowPadLeft = 26f, NowPadRight = 18f, NowPad = 12f;
    const float CountSeconds = .55f, PopSeconds = .9f, PuffSeconds = .55f, LiftSeconds = .22f, FadeSeconds = .18f;

    static readonly Color CardInk = new Color32(0x4F, 0x49, 0x43, 255);
    static readonly Color DayInk = new Color32(0x8C, 0x4F, 0x0A, 255);
    static readonly Color PuffColour = new Color(.93f, .89f, .82f, .62f);

    Canvas canvas;
    RectTransform root;
    CanvasGroup group;
    RectTransform nowCard;
    TextMeshProUGUI nowText;
    RectTransform till;
    TextMeshProUGUI tillLabel, tillSeats, popSeats;
    HudMoney jar;                      // 26 Oct 2026 (HUD 3): the money object, the HUD's own
    RectTransform flights;
    Vector2 lastPuff;                  // where the last puff was (screen pixels): where a bill leaves for, or comes from
    float lastPuffAt = -10f;
    RectTransform strip;
    RectTransform controls;
    TextMeshProUGUI controlsText;
    RectTransform plate;
    TextMeshProUGUI plateText;
    Image holdRing;
    readonly List<Card> cards = new List<Card>();
    readonly List<Image> puffs = new List<Image>();
    readonly List<float> puffEnds = new List<float>();
    readonly UiTween.Set motions = new UiTween.Set(64);
    static Sprite ringSprite;

    int moneyTo = int.MinValue, seatsShown = -1;
    bool built;

    sealed class Card
    {
        public FurnitureDefinition kind;
        public RectTransform rect, ring;
        public Image back, pill;
        public TextMeshProUGUI name, seats, price;
        public CanvasGroup group;
        public bool chosen;
        public string icon = "";   // 26 Oct 2026 (Icons 2): the picture it wears (a NoteIcons furn_* name), or ""
    }

    // ------------------------------------------------------------------ what's on screen (the labs read these)

    public bool Showing => root != null && root.gameObject.activeSelf;
    public string NowTitle { get; private set; } = "";
    public string NowLine { get; private set; } = "";
    public string ControlsLine => controlsText != null ? controlsText.text : "";
    public bool PlateShowing => plate != null && plate.gameObject.activeSelf;
    public string PlateLine => PlateShowing ? plateText.text : "";
    public float HoldShown => holdRing != null && holdRing.gameObject.activeInHierarchy ? holdRing.fillAmount : 0f;
    public int MoneyShown => jar != null ? jar.Shown : 0;
    /// <summary>26 Oct 2026 (HUD 3): the money object (the jar, the figure, the receipt, the flights), for the labs.</summary>
    public HudMoney Jar => jar;
    public int SeatsShown => seatsShown;
    public RectTransform Strip => strip;

    /// <summary>The strip, card by card: "Bistro table $80 chosen; Square table $90; Window counter Day 2"... (labs, reports).</summary>
    public string CardsLine
    {
        get
        {
            var b = new StringBuilder();
            foreach (Card c in cards)
            {
                if (c.kind == null) continue;
                if (b.Length > 0) b.Append("; ");
                b.Append(c.kind.displayName).Append(' ').Append(Plain(c.price.text));
                if (c.chosen) b.Append(" chosen");
            }
            return b.ToString();
        }
    }

    /// <summary>26 Oct 2026 (Icons 2): the strip's pictures, card by card: "Bistro table furn_bistro; Plant furn_plant; ..." ("(none)" for a card without).</summary>
    public string IconsLine
    {
        get
        {
            var b = new StringBuilder();
            foreach (Card c in cards)
            {
                if (c.kind == null) continue;
                if (b.Length > 0) b.Append("; ");
                b.Append(c.kind.displayName).Append(' ').Append(c.icon.Length > 0 ? c.icon : "(none)");
            }
            return b.ToString();
        }
    }

    /// <summary>26 Oct 2026 (Icons 2): the cards' height as built (CardTallH with their pictures, else CardH).</summary>
    public float CardHeight => cardH;

    static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<.*?>", "");

    // ------------------------------------------------------------------ made, shown, put away

    public static ArrangeUI Create()
    {
        var go = new GameObject("Arrange the café (screen, runtime)");
        var ui = go.AddComponent<ArrangeUI>();
        ui.Build();
        return ui;
    }

    void Build()
    {
        if (built) return;
        built = true;
        canvas = UiSkin.ScreenCanvas("Arrange canvas", transform, SortingOrder);
        root = (RectTransform)canvas.transform;
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // The Now card, in the Now line's look.
        nowCard = Box("Now card", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(UiSkin.Margin, -UiSkin.Margin), new Vector2(NowWidth, 96f));
        UiSkin.Paint(nowCard, UiSkin.Band, UiSkin.Radius);
        var shadow = nowCard.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .35f);
        shadow.effectDistance = new Vector2(0f, -3f);
        RectTransform edge = Box("Edge", nowCard, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        edge.pivot = new Vector2(0f, .5f);
        edge.offsetMin = new Vector2(8f, 9f);
        edge.offsetMax = new Vector2(13f, -9f);
        UiSkin.Paint(edge, UiSkin.Gold, 3f);
        nowText = UiSkin.Text("Now", nowCard, NowSize, UiSkin.BandText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, true);
        Stretch(nowText.rectTransform, new Vector2(NowPadLeft, NowPad), new Vector2(-NowPadRight, -NowPad));

        // The till: 26 Oct 2026 (HUD 3): the HUD's jar on its board (HudMoney), with the seats on a dark band under it.
        till = Box("Till", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-UiSkin.Margin, -UiSkin.Margin), new Vector2(TillWidth, TillHeight));
        flights = Box("Flights", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        flights.offsetMin = flights.offsetMax = Vector2.zero;
        jar = new HudMoney();
        jar.Build("The jar and the figure", till, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(TillWidth - HudMoney.BoardW) * .5f, -12f), flights);
        RectTransform seatsBand = Box("Seats band", till, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(TillWidth - HudMoney.BoardW) * .5f, -(12f + HudMoney.BoardH + 14f)), new Vector2(HudMoney.BoardW, 44f));
        UiSkin.Paint(seatsBand, UiSkin.Band, UiSkin.Radius);
        tillLabel = UiSkin.Text("Label", seatsBand, SmallSize, UiSkin.BandFaint, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Place(tillLabel.rectTransform, new Vector2(16f, -8f), new Vector2(110f, 28f));
        tillLabel.text = "YOUR CAFÉ";
        tillSeats = UiSkin.Text("Seats", seatsBand, SeatsSize, UiSkin.BandText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Place(tillSeats.rectTransform, new Vector2(HudMoney.BoardW - 16f - 120f, -4f), new Vector2(120f, 36f));
        popSeats = UiSkin.Text("Seats pop", till, SeatsSize, UiSkin.Brand, TextAlignmentOptions.TopRight, FontStyles.Bold);
        UiSkin.Shadowed(popSeats);   // 26 Oct 2026 (his Mac): the green "+2" over the band read 3.9:1
        Place(popSeats.rectTransform, new Vector2(TillWidth - 110f, -(12f + HudMoney.BoardH + 14f + 46f)), new Vector2(90f, 38f));
        popSeats.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        flights.SetAsLastSibling();

        // The strip and the controls.
        strip = Box("Strip", root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, StripBottom), new Vector2(1600f, CardH + Lift));
        strip.pivot = new Vector2(.5f, 0f);
        controls = Box("Controls", root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, ControlsBottom), new Vector2(600f, ControlsH));
        controls.pivot = new Vector2(.5f, 0f);
        UiSkin.Paint(controls, UiSkin.Band, ControlsH * .5f);
        controlsText = UiSkin.Text("Words", controls, ControlsSize, UiSkin.BandText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(controlsText.rectTransform, new Vector2(24f, 0f), new Vector2(-24f, 0f));

        // The plate over the ghost.
        plate = Box("Plate", root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(400f, 60f));
        plate.pivot = new Vector2(.5f, 0f);
        UiSkin.Paint(plate, UiSkin.Band, UiSkin.Radius);
        plateText = UiSkin.Text("Words", plate, PlateSize, UiSkin.BandText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, true);
        holdRing = new GameObject("Hold ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform ringRect = holdRing.rectTransform;
        ringRect.SetParent(plate, false);
        ringRect.anchorMin = ringRect.anchorMax = new Vector2(1f, 0f);
        ringRect.pivot = new Vector2(1f, 0f);
        ringRect.sizeDelta = new Vector2(RingSize, RingSize);
        ringRect.anchoredPosition = new Vector2(-PlatePad, PlatePad - 4f);
        holdRing.sprite = Ring;
        holdRing.type = Image.Type.Filled;
        holdRing.fillMethod = Image.FillMethod.Radial360;
        holdRing.fillOrigin = (int)Image.Origin360.Top;
        holdRing.fillClockwise = true;
        holdRing.color = UiSkin.Gold;
        holdRing.raycastTarget = false;
        plate.gameObject.SetActive(false);

        canvas.gameObject.SetActive(false);
    }

    /// <summary>Up, fading in (the strip rises).</summary>
    public void Open()
    {
        Build();
        canvas.gameObject.SetActive(true);
        float now = Time.unscaledTime;
        motions.Alpha(group, 0f, 1f, now, FadeSeconds, UiTween.Ease.OutCubic);
        motions.MoveY(strip, StripBottom - 60f, StripBottom, now, .32f, UiTween.Ease.OutQuint);
        seatsShown = -1;
        moneyTo = int.MinValue;
    }

    /// <summary>Gone at once (the phone comes back up over it).</summary>
    public void Close()
    {
        motions.Finish();
        if (canvas != null) canvas.gameObject.SetActive(false);
        HidePlate();
    }

    // ------------------------------------------------------------------ the strip

    /// <summary>The cards, chosen and priced against the till (built once for the kinds; the rest is restyled).</summary>
    public void Cards(IReadOnlyList<FurnitureDefinition> kinds, FurnitureDefinition chosen, int money, CafeLayout layout)
    {
        if (kinds == null) return;
        bool same = cards.Count == kinds.Count;
        for (int i = 0; same && i < kinds.Count; i++) same = cards[i].kind == kinds[i];
        if (!same) BuildCards(kinds);
        float now = Time.unscaledTime;
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            FurnitureDefinition k = c.kind;
            if (k == null) continue;
            bool forSale = layout != null && layout.ForSale(k);
            bool afford = money >= k.price;
            c.group.alpha = forSale ? 1f : .62f;
            if (forSale)
            {
                c.pill.color = afford ? UiSkin.Brand : UiSkin.Red;
                c.price.color = Color.white;
                c.price.fontSize = PriceSize;
                c.price.text = "$" + k.price;
            }
            else
            {
                c.pill.color = UiSkin.GoldSoft;
                c.price.color = DayInk;
                c.price.fontSize = SmallSize;
                c.price.text = "Day " + Mathf.Max(1, k.forSaleFromDay);
            }
            bool isChosen = k == chosen;
            if (isChosen != c.chosen)
            {
                c.chosen = isChosen;
                c.ring.gameObject.SetActive(isChosen);
                float y = c.rect.anchoredPosition.y;
                motions.MoveY(c.rect, y, isChosen ? Lift : 0f, now, LiftSeconds, isChosen ? UiTween.Ease.OutBack : UiTween.Ease.OutCubic);
            }
        }
    }

    void BuildCards(IReadOnlyList<FurnitureDefinition> kinds)
    {
        foreach (Card c in cards) if (c.rect != null) Destroy(c.rect.gameObject);
        cards.Clear();
        // 26 Oct 2026 (Icons 2): the cards are taller when the sheet has the kinds' pictures (all of them or none, so the
        // strip is one height); a kind the sheet doesn't know keeps its words in the room the picture would have.
        bool pictures = false;
        foreach (FurnitureDefinition k in kinds)
            if (k != null && NoteIcons.SpriteOf(NoteIcons.Furniture(k.name)) != null) { pictures = true; break; }
        cardH = pictures ? CardTallH : CardH;
        float total = kinds.Count * CardW + Mathf.Max(0, kinds.Count - 1) * CardGap;
        strip.sizeDelta = new Vector2(total, cardH + Lift);
        for (int i = 0; i < kinds.Count; i++)
        {
            FurnitureDefinition k = kinds[i];
            var c = new Card { kind = k };
            c.rect = Box("Card " + (k != null ? k.name : i.ToString()), strip, new Vector2(0f, 0f), new Vector2(0f, 0f),
                         new Vector2(i * (CardW + CardGap), 0f), new Vector2(CardW, cardH));
            c.rect.pivot = new Vector2(0f, 0f);
            c.group = c.rect.gameObject.AddComponent<CanvasGroup>();
            c.ring = Box("Chosen ring", c.rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            c.ring.offsetMin = new Vector2(-5f, -5f);
            c.ring.offsetMax = new Vector2(5f, 5f);
            UiSkin.Paint(c.ring, UiSkin.Accent, UiSkin.Radius + 5f);
            c.ring.gameObject.SetActive(false);
            RectTransform back = Box("Card", c.rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            c.back = UiSkin.Paint(back, UiSkin.Card, UiSkin.Radius);
            // 26 Oct 2026 (Icons 2): the kind's picture at the top left, the name under it.
            float nameTop = 10f;
            if (pictures)
            {
                string icon = k != null ? NoteIcons.Furniture(k.name) : "";
                Sprite sprite = NoteIcons.SpriteOf(icon);
                if (sprite != null)
                {
                    RectTransform box = Box("Picture - " + icon, c.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(IconSize, IconSize));
                    box.pivot = new Vector2(0f, 1f);
                    var image = box.gameObject.AddComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    c.icon = icon;
                }
                nameTop = 10f + IconSize + 6f;
            }
            c.name = UiSkin.Text("Name", c.rect, NameSize, UiSkin.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold, true);
            Place(c.name.rectTransform, new Vector2(14f, -nameTop), new Vector2(CardW - 28f, 60f));
            c.name.text = k != null ? k.displayName : "";
            c.seats = UiSkin.Text("Seats", c.rect, SmallSize, CardInk, TextAlignmentOptions.BottomLeft, UiSkin.SemiBold);
            Place(c.seats.rectTransform, new Vector2(14f, -(cardH - 40f)), new Vector2(96f, 28f));
            c.seats.text = k == null ? "" : k.seats > 0 ? $"{k.seats} seat{(k.seats == 1 ? "" : "s")}" : "Looks only";
            RectTransform pill = Box("Price", c.rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 10f), new Vector2(84f, 36f));
            pill.pivot = new Vector2(1f, 0f);
            c.pill = UiSkin.Paint(pill, UiSkin.Brand, 18f);
            c.price = UiSkin.Text("Words", pill, PriceSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(c.price.rectTransform, Vector2.zero, Vector2.zero);
            cards.Add(c);
        }
    }

    /// <summary>The kind whose card is under a screen point (a click on the strip), or null.</summary>
    public FurnitureDefinition CardAt(Vector2 screen)
    {
        if (!Showing) return null;
        foreach (Card c in cards)
            if (c.rect != null && RectTransformUtility.RectangleContainsScreenPoint(c.rect, screen, null)) return c.kind;
        return null;
    }

    /// <summary>Whether a screen point is over the strip, the controls, the till or the Now card (a click there isn't on the floor).</summary>
    public bool OverUI(Vector2 screen)
    {
        if (!Showing) return false;
        return Inside(strip, screen) || Inside(controls, screen) || Inside(till, screen) || Inside(nowCard, screen);
    }

    static bool Inside(RectTransform r, Vector2 screen) => r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null);

    // ------------------------------------------------------------------ the till, the Now card, the controls

    /// <summary>The till's money (the jar: it counts down to a new figure, up for a sale) and the seats ("+2" pops on a change).</summary>
    public void Till(int money, int seats)
    {
        float now = Time.unscaledTime;
        if (moneyTo == int.MinValue) { moneyTo = money; jar.Show(money); }
        else if (money != moneyTo)
        {
            // 26 Oct 2026 (HUD 3): spending leaves the jar for the piece just put down (the last puff); a sale comes from it.
            Vector2? at = now - lastPuffAt < .6f ? lastPuff : (Vector2?)null;
            if (money < moneyTo) jar.Spend(money, moneyTo - money, at, now);
            else jar.Pay(money, money - moneyTo, 0, at, now - .18f, now);
            moneyTo = money;
        }
        if (seatsShown < 0) { seatsShown = seats; WriteSeats(); }
        else if (seats != seatsShown)
        {
            Pop(popSeats, (seats > seatsShown ? "+" : "") + (seats - seatsShown), seats > seatsShown ? UiSkin.Brand : UiSkin.Red);
            seatsShown = seats;
            WriteSeats();
        }
    }

    void Pop(TextMeshProUGUI text, string words, Color colour)
    {
        float now = Time.unscaledTime;
        text.text = words;
        text.color = colour;
        // Held for a moment, then gone: the alpha's motion shows its start (1) until it begins.
        motions.Alpha(text.GetComponent<CanvasGroup>(), 1f, 0f, now + .35f, PopSeconds - .35f, UiTween.Ease.InQuad);
        motions.Scale(text.rectTransform, 1.25f, 1f, now, .3f, UiTween.Ease.OutBack);
    }

    void WriteSeats() { if (tillSeats != null) tillSeats.text = $"{seatsShown} seat{(seatsShown == 1 ? "" : "s")}"; }

    /// <summary>The Now card: the title in gold over the line (DayOneGuideUI's look); one pulse when it changes.</summary>
    public void Now(string title, string line)
    {
        title ??= "";
        line ??= "";
        if (title == NowTitle && line == NowLine) return;
        bool changed = NowLine.Length > 0;
        NowTitle = title;
        NowLine = line;
        string text = (title.Length > 0 ? $"<size={NowTitleSize}><color={UiSkin.HexOf(UiSkin.Gold)}><b>{title}</b></color></size>\n" : "") + $"<b>{line}</b>";
        nowText.text = text;
        Vector2 words = nowText.GetPreferredValues(text, NowWidth - NowPadLeft - NowPadRight, 0f);
        nowCard.sizeDelta = new Vector2(NowWidth, Mathf.Max(72f, Mathf.Ceil(words.y) + 2f * NowPad));
        if (changed)
        {
            motions.Scale(nowCard, 1f, 1.06f, Time.unscaledTime, .45f, UiTween.Ease.Pop);
            Sfx.Play2D(DayOneGuideUI.ChangeCue);
        }
    }

    /// <summary>The controls strip: only what works now (icons from ControlHints and their verbs).</summary>
    public void Controls(string text)
    {
        if (controlsText == null || controlsText.text == text) return;
        controlsText.text = text ?? "";
        Vector2 words = controlsText.GetPreferredValues(controlsText.text, 1600f, ControlsH);
        controls.sizeDelta = new Vector2(Mathf.Ceil(words.x) + 56f, ControlsH);
    }

    // ------------------------------------------------------------------ the plate

    /// <summary>The plate above a screen point: the reason first (red), the name line, and the sell line with its hold ring.</summary>
    public void Plate(Vector2 screen, string reason, string line, string sell, float hold)
    {
        if (plate == null) return;
        var b = new StringBuilder();
        if (!string.IsNullOrEmpty(reason)) b.Append($"<color={UiSkin.HexOf(UiSkin.RedSoft)}>{reason}</color>\n");
        b.Append(line ?? "");
        bool selling = !string.IsNullOrEmpty(sell);
        if (selling) b.Append($"\n<size={SellSize}>{sell}</size>");
        string text = b.ToString();
        if (plateText.text != text)
        {
            plateText.text = text;
            float room = selling ? RingSize + 12f : 0f;
            Vector2 words = plateText.GetPreferredValues(text, PlateMost - 2f * PlatePad - room, 0f);
            plate.sizeDelta = new Vector2(Mathf.Min(PlateMost, Mathf.Ceil(words.x) + 2f * PlatePad + room + 2f), Mathf.Ceil(words.y) + 2f * PlatePad - 4f);
            Stretch(plateText.rectTransform, new Vector2(PlatePad, PlatePad - 4f), new Vector2(-PlatePad - room, -PlatePad + 2f));
        }
        holdRing.gameObject.SetActive(selling);
        holdRing.fillAmount = Mathf.Clamp01(hold);
        if (!plate.gameObject.activeSelf) plate.gameObject.SetActive(true);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local))
        {
            // Kept on the screen: the plate never runs off an edge.
            Vector2 half = root.rect.size * .5f, size = plate.sizeDelta;
            local.x = Mathf.Clamp(local.x, -half.x + size.x * .5f + 12f, half.x - size.x * .5f - 12f);
            local.y = Mathf.Clamp(local.y, -half.y + StripBottom + cardH + Lift + 16f, half.y - size.y - 12f);   // 26 Oct 2026 (Icons 2): the cards' height as built
            plate.anchoredPosition = local;
        }
    }

    public void HidePlate()
    {
        if (plate != null && plate.gameObject.activeSelf) plate.gameObject.SetActive(false);
    }

    /// <summary>A refused press: the plate gives a small shake.</summary>
    public void Refuse()
    {
        if (!PlateShowing) return;
        float x = plate.anchoredPosition.x;
        motions.MoveX(plate, x, x + 10f, Time.unscaledTime, .32f, UiTween.Ease.Shake);
    }

    // ------------------------------------------------------------------ the dust puff

    /// <summary>A puff of dust at a screen point (a piece put down, or sold back).</summary>
    public void Puff(Vector2 screen)
    {
        if (!Showing || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local)) return;
        float now = Time.unscaledTime;
        lastPuff = screen;      // 26 Oct 2026 (HUD 3): the jar's bill leaves for (or comes from) here
        lastPuffAt = now;
        for (int i = 0; i < 7; i++)
        {
            Image p = PuffImage();
            RectTransform r = p.rectTransform;
            float a = i * Mathf.PI * 2f / 7f + .4f;
            Vector2 from = local + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .5f) * 14f, to = local + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .5f) * 62f;
            r.anchoredPosition = from;
            p.gameObject.SetActive(true);
            CanvasGroup g = p.GetComponent<CanvasGroup>();
            motions.MoveX(r, from.x, to.x, now, PuffSeconds, UiTween.Ease.OutCubic);
            motions.MoveY(r, from.y, to.y + 10f, now, PuffSeconds, UiTween.Ease.OutCubic);
            motions.Scale(r, .35f, 1.3f, now, PuffSeconds, UiTween.Ease.OutCubic);
            motions.Alpha(g, 1f, 0f, now + .1f, PuffSeconds - .1f, UiTween.Ease.InQuad);
            puffEnds[puffs.IndexOf(p)] = now + PuffSeconds + .05f;
        }
    }

    Image PuffImage()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < puffs.Count; i++)
            if (!puffs[i].gameObject.activeSelf || puffEnds[i] < now) return puffs[i];
        var go = new GameObject("Puff", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(root, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(46f, 46f);
        Image image = go.GetComponent<Image>();
        image.sprite = UiSkin.Circle;
        image.color = PuffColour;
        image.raycastTarget = false;
        go.SetActive(false);
        puffs.Add(image);
        puffEnds.Add(0f);
        return image;
    }

    // ------------------------------------------------------------------ every frame (real time)

    void LateUpdate()
    {
        if (!Showing) return;
        float now = Time.unscaledTime;
        jar.Tick(now, Time.unscaledDeltaTime);   // 26 Oct 2026 (HUD 3): the jar runs on real time here, as the screen does
        motions.Step(now);
        for (int i = 0; i < puffs.Count; i++)
            if (puffs[i].gameObject.activeSelf && puffEnds[i] < now) puffs[i].gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ the plumbing

    static RectTransform Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 at, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(.5f, .5f);
        r.anchoredPosition = at;
        r.sizeDelta = size;
        return r;
    }

    static void Stretch(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(.5f, .5f);
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    // A box from its parent's top-left corner.
    static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    // The hold ring: a ring 6 px thick in 64, smoothed (made once, never saved).
    static Sprite Ring
    {
        get
        {
            if (ringSprite != null) return ringSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Arrange - hold ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float c = size * .5f, outer = c - 1f, inner = outer - 6f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(c, c));
                    float a = Mathf.Clamp01(outer - d + .5f) * Mathf.Clamp01(d - inner + .5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(px);
            texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            ringSprite.name = texture.name;
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
