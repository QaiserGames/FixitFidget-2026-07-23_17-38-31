using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE START SCREEN'S MENU (26 Oct 2026; review24 spec S, "Feel and look")
//
// Overcooked's front end (the research, §3): chunky rounded slabs with thick dark outlines and a cream fill, one word each,
// that pop and squash when selected, over the live scene, so you're never "in a menu". Built in code on UiSkin, never saved;
// TitleScreen makes it and reacts to its choices (Chosen).
//
//   * THE SLABS: cream #F7EFE6, a 4 px dark outline #1F1B17 (at 1080p), the skin's soft drop shadow; the word Baloo 2 Bold
//     38 px. The selected slab turns honey #F0B54C with dark words (9.6:1) and pops: 1.0 -> 1.08 -> 0.97 -> 1.0 over 180 ms,
//     its settle on UiTween's out-back, with a soft click (PLACEHOLDER: the phone's own "phone.tap", the nicest existing
//     click; "ui.confirm" when a slab is chosen).
//   * THE ITEMS, bottom left: Continue (with "Day 3 · Known on the street" under it), New Game, Settings, Credits, Quit.
//     The cold open has no Continue. His girlfriend is dyslexic and plays on a PlayStation pad: the first item is selected
//     the moment the menu appears.
//   * THE LOGO, top left: "Fix It Fiasco" in Baloo 2's heaviest cut in the project (Bold, thickened by the face's dilate:
//     there is no ExtraBold file), cream with a thick dark outline and a soft warm shadow, tilted 4°. PLACEHOLDER for his
//     girlfriend's logo. In the cold open it slams in like an order ticket: drops, squashes, settles (about 300 ms).
//   * WORDS: nothing under 28 px (26 Oct 2026, Start screen polish: the house's word check flags under 26 at 1080p and a
//     16:10 screen scales 26 down to 24.7; the spec's floor is 22), every word on a cream slab or on the skin's dark band, so
//     the contrast never depends on the café behind. The controls strip uses ControlHints' icons ("✕ Choose · ○ Back"),
//     never a key written in brackets. Under the words a soft vignette darkens the frame's edges (Vignette), and each slab
//     has a darker lip along its bottom, so it reads as a thick tile.
//   * INPUT: pad up/down (D-pad or stick) to move, Cross to choose, Circle for back; mouse hover and click; keyboard arrows
//     (and W/S), Enter (and Space), Esc. On a card the left and right keys move too.
//   * CARDS: New Game over a save asks first ("Start a new café? Your café now is kept as a backup.", [Start new] [Keep
//     playing], Keep playing selected: the safe answer under the thumb); Credits ("Fix It Fiasco", "Made by Mansoor Qaiser",
//     the art credit's placeholder line). Back closes a card.
//   * "BEFORE IT GOT LIKE THIS.": the cold open's New Game, a fast-rewind made as a UI overlay: streaks and scanlines racing
//     right to left, a cool cast, a drawn "◀◀" (two triangles: no font is trusted with the glyph) and those words on a band.
// Everything runs on unscaled time: the cold open freezes the game under it.
// ---------------------------------------------------------------------------
public sealed class TitleMenuUI : MonoBehaviour
{
    public const string ContinueId = "continue", NewGameId = "new", SettingsId = "settings", CreditsId = "credits", QuitId = "quit";
    public const string StartNewId = "startnew", KeepPlayingId = "keep", BackId = "back";
    public const string RewindLine = "Before it got like this.";
    public const string ConfirmTitle = "Start a new café?";
    public const string ConfirmBody = "Your café now is kept as a backup.";
    public const string CreditsTitle = "Fix It Fiasco";
    public const string CreditsMadeBy = "Made by Mansoor Qaiser";
    public const string CreditsArt = "Art: credit to come";   // PLACEHOLDER: the art credit, his to write (no name is invented)

    public const int SortingOrder = 92;   // under the phone by day (95), so Settings opens over the title; over the HUD (2-90)
    // 26 Oct 2026 (Start screen polish): the secondary lines at 28, not 26: the canvas scales by the screen's shape (match .5),
    // so on a 16:10 Mac screen 26 measured 24.7 at 1080p, under the house's 26 px line; 28 measures 26.6 there.
    public const float WordPx = 38f, SubPx = 28f, StripPx = 28f, LogoPx = 108f, PopSeconds = .18f;

    public static readonly Color Cream = Hex(0xF7EFE6);
    public static readonly Color Outline = Hex(0x1F1B17);
    public static readonly Color Honey = Hex(0xF0B54C);
    // 26 Oct 2026 (Start screen polish): the slab's underside, a shade darker than its face, so it reads as a thick tile (the
    // Overcooked slab has depth, not just an outline); the honey's own darker edge under the selected one.
    public static readonly Color CreamLip = Hex(0xDCCDBB);
    public static readonly Color HoneyLip = Hex(0xC48F2E);

    static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    /// <summary>A slab was chosen (its id).</summary>
    public event Action<string> Chosen;

    sealed class Slab
    {
        public string id, word;
        public RectTransform root;
        public Image fill, lip;
        public TextMeshProUGUI label, sub;
        public float popAt = -10f;
    }

    RectTransform screen, menuRoot, cardRoot, rewindRoot, logo, stripPlate;
    Image grade, fade, dim, vignette, flash;
    float vignetteTarget, vignetteNow;
    static Sprite vignetteSprite;
    TextMeshProUGUI logoText, strip, problem, rewindLabel;
    CanvasGroup menuGroup, rewindGroup;
    readonly List<Slab> menu = new List<Slab>();
    readonly List<Slab> card = new List<Slab>();
    List<Slab> active;
    int selected = -1;
    bool interactive, cardHorizontal, logoSlam;
    float logoAt = -10f, menuAt = -10f, gradeTarget, gradeNow;
    int stripFrame = -2;
    Vector2 lastMouse;
    bool stickHeld;
    string returnTo;
    readonly List<RectTransform> streaks = new List<RectTransform>();
    readonly List<RectTransform> scanlines = new List<RectTransform>();
    static Sprite triangle;

    // ------------------------------------------------------------------ what the lab reads

    /// <summary>The words of the slabs the player can move between now (the menu, or a card's).</summary>
    public List<string> Items { get { var l = new List<string>(); if (active != null) foreach (Slab s in active) l.Add(s.word); return l; } }
    public int Selected => selected;
    public string SelectedId => active != null && selected >= 0 && selected < active.Count ? active[selected].id : "";
    /// <summary>Which slab was selected in the very frame the menu appeared (-1 before it has).</summary>
    public int SelectedAtShow { get; private set; } = -1;
    public string ContinueSubLine { get; private set; } = "";
    public bool MenuShowing => menuRoot != null && menuRoot.gameObject.activeSelf && active == menu;
    public bool CardShowing => cardRoot != null && cardRoot.gameObject.activeSelf;
    public string RewindWords => rewindLabel != null && rewindRoot.gameObject.activeSelf ? rewindLabel.text : "";
    public bool LogoSettled => logo != null && logo.gameObject.activeSelf && Time.unscaledTime - logoAt > .45f;
    public string StripText => strip != null ? strip.text : "";

    // ------------------------------------------------------------------ building

    public static TitleMenuUI Create(Transform parent)
    {
        Canvas canvas = UiSkin.ScreenCanvas("Start screen (while playing)", parent, SortingOrder);
        var ui = canvas.gameObject.AddComponent<TitleMenuUI>();
        ui.Build();
        return ui;
    }

    static RectTransform Box(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Box(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void Corner(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 at, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
    }

    void Build()
    {
        screen = Stretch("Screen", transform);

        // The cold open's warm grade over the frozen frame (under everything else).
        grade = UiSkin.Paint(Stretch("Warm grade", screen), new Color(1f, .62f, .28f, 0f), 0f);
        // 26 Oct 2026 (Start screen polish): soft dark edges over the scene (a radial falloff drawn in code), under the words:
        // the frame reads as a title, not the game paused, and the menu's corner gains contrast whatever is behind it.
        RectTransform edges = Stretch("Vignette", screen);
        vignette = edges.gameObject.AddComponent<Image>();
        vignette.sprite = VignetteSprite();
        vignette.type = Image.Type.Simple;
        vignette.color = new Color(.08f, .05f, .03f, 0f);
        vignette.raycastTarget = false;

        BuildLogo();

        menuRoot = Box("Menu", screen);
        Corner(menuRoot, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        menuGroup = menuRoot.gameObject.AddComponent<CanvasGroup>();
        menuRoot.gameObject.SetActive(false);

        // The controls strip, bottom left, on the skin's dark band.
        stripPlate = Box("Controls strip", screen);
        Corner(stripPlate, Vector2.zero, Vector2.zero, new Vector2(84f, 34f), new Vector2(360f, 48f));
        UiSkin.Paint(stripPlate, UiSkin.Band, 14f);
        strip = UiSkin.Text("Controls", stripPlate, StripPx, UiSkin.BandText, TextAlignmentOptions.Midline, UiSkin.SemiBold);
        strip.rectTransform.anchorMin = Vector2.zero;
        strip.rectTransform.anchorMax = Vector2.one;
        strip.rectTransform.offsetMin = strip.rectTransform.offsetMax = Vector2.zero;
        stripPlate.gameObject.SetActive(false);

        // The cards (New Game's question, Credits) over a dim.
        cardRoot = Stretch("Card", screen);
        dim = UiSkin.Paint(cardRoot, new Color(0f, 0f, 0f, .5f), 0f);
        cardRoot.gameObject.SetActive(false);

        BuildRewind();

        fade = UiSkin.Paint(Stretch("Fade", screen), new Color(0f, 0f, 0f, 0f), 0f);
        fade.gameObject.SetActive(false);
    }

    void BuildLogo()
    {
        logo = Box("Logo (PLACEHOLDER: her logo goes here)", screen);
        Corner(logo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -64f), new Vector2(900f, 150f));
        logo.localRotation = Quaternion.Euler(0f, 0f, 4f);
        logoText = UiSkin.Text("Fix It Fiasco", logo, LogoPx, Cream, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        logoText.text = "Fix It Fiasco";
        logoText.rectTransform.anchorMin = Vector2.zero;
        logoText.rectTransform.anchorMax = Vector2.one;
        logoText.rectTransform.offsetMin = logoText.rectTransform.offsetMax = Vector2.zero;
        // Baloo 2's Bold cut as the face itself (the font's weight table, entry 700), so the outline and shadow below are on the
        // material that draws it; the regular semibold, bolded, where there is no table.
        TMP_FontAsset bold = BoldFace();
        if (bold != null) logoText.font = bold;
        else logoText.fontStyle = FontStyles.Bold;
        Material m = logoText.fontMaterial;   // this text's own copy
        if (m != null)
        {
            if (m.HasProperty(ShaderUtilities.ID_FaceDilate)) m.SetFloat(ShaderUtilities.ID_FaceDilate, .1f);
            if (m.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, .2f);
                m.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
            }
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(.36f, .17f, .05f, .55f));   // soft and warm, not black
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .5f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.7f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, .25f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .55f);
            }
            logoText.UpdateMeshPadding();
        }
        logo.gameObject.SetActive(false);
    }

    static TMP_FontAsset BoldFace()
    {
        TMP_FontAsset regular = UiSkin.Font;
        TMP_FontWeightPair[] table = regular != null ? regular.fontWeightTable : null;
        return table != null && table.Length > 7 ? table[7].regularTypeface : null;
    }

    Slab MakeSlab(Transform parent, string id, string word, string subLine, Vector2 size)
    {
        var s = new Slab { id = id, word = word };
        s.root = Box("Slab - " + word, parent);
        s.root.sizeDelta = size;
        RectTransform edge = Box("Outline", s.root);
        edge.anchorMin = Vector2.zero; edge.anchorMax = Vector2.one;
        edge.offsetMin = edge.offsetMax = Vector2.zero;
        UiSkin.Paint(edge, Outline, 20f, true);
        UiSkin.DropShadow(edge, 10f, 7f, .42f);
        // The underside (26 Oct 2026, Start screen polish): the darker lip shows 4 px along the bottom under the face.
        RectTransform under = Box("Lip", edge);
        under.anchorMin = Vector2.zero; under.anchorMax = Vector2.one;
        under.offsetMin = new Vector2(4f, 4f); under.offsetMax = new Vector2(-4f, -4f);
        s.lip = UiSkin.Paint(under, CreamLip, 16f);
        RectTransform inner = Box("Fill", edge);
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(4f, 8f); inner.offsetMax = new Vector2(-4f, -4f);   // the 4 px outline, the lip below
        s.fill = UiSkin.Paint(inner, Cream, 16f);
        bool two = !string.IsNullOrEmpty(subLine);
        s.label = UiSkin.Text("Word", inner, WordPx, Outline, two ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        s.label.text = word;
        Fill(s.label.rectTransform, 26f, two ? new Vector2(0f, size.y * .42f) : Vector2.zero, Vector2.zero);
        if (two)
        {
            s.sub = UiSkin.Text("Sub-line", inner, SubPx, Outline, TextAlignmentOptions.TopLeft, UiSkin.SemiBold);
            s.sub.text = subLine;
            Fill(s.sub.rectTransform, 26f, Vector2.zero, new Vector2(0f, size.y * .56f));
        }
        return s;
    }

    static void Fill(RectTransform rect, float padX, Vector2 bottomInset, Vector2 topInset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padX, bottomInset.y);
        rect.offsetMax = new Vector2(-padX, -topInset.y);
    }

    // ------------------------------------------------------------------ showing

    public void ShowLogo(bool slam)
    {
        logo.gameObject.SetActive(true);
        logoSlam = slam;
        logoAt = Time.unscaledTime;
        if (slam) Sfx.Play2DLater("ticket.new", .1f);   // PLACEHOLDER: the order ticket's own sound, as it lands
        StepLogo();
    }

    public void WarmGrade(bool on) => gradeTarget = on ? .2f : 0f;

    /// <summary>The five slabs (four without Continue), the first selected at once.</summary>
    public void ShowMenu(bool withContinue, string continueLine)
    {
        foreach (Slab s in menu) if (s.root != null) Destroy(s.root.gameObject);
        menu.Clear();
        var items = new List<(string id, string word)>();
        if (withContinue) items.Add((ContinueId, "Continue"));
        items.Add((NewGameId, "New Game"));
        items.Add((SettingsId, "Settings"));
        items.Add((CreditsId, "Credits"));
        items.Add((QuitId, "Quit"));
        ContinueSubLine = withContinue ? continueLine ?? "" : "";
        // 26 Oct 2026 (Start screen polish): 500 wide, so "Day 12 · Neighbourhood favourite" (the longest sub-line) fits at
        // 28 px with the 26 px pads; the strip below keeps its own width.
        const float width = 500f, plain = 72f, tall = 108f, gap = 16f, left = 84f, bottom = 120f;
        float y = bottom;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            bool sub = items[i].id == ContinueId;
            float h = sub ? tall : plain;
            Slab s = MakeSlab(menuRoot, items[i].id, items[i].word, sub ? ContinueSubLine : null, new Vector2(width, h));
            // Pivot in the middle, so the pop grows from the slab's centre.
            Corner(s.root, Vector2.zero, new Vector2(.5f, .5f), new Vector2(left + width / 2f, y + h / 2f), new Vector2(width, h));
            menu.Insert(0, s);
            y += h + gap;
        }
        menuRoot.gameObject.SetActive(true);
        stripPlate.gameObject.SetActive(true);
        menuAt = Time.unscaledTime;
        active = menu;
        cardHorizontal = false;
        interactive = true;
        Select(0, false);
        SelectedAtShow = selected;
        RefreshStrip(true);
        lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    public void ShowConfirm()
    {
        returnTo = NewGameId;
        OpenCard(ConfirmTitle, ConfirmBody, null, new[] { (StartNewId, "Start new"), (KeepPlayingId, "Keep playing") }, 1);
    }

    public void ShowCredits()
    {
        returnTo = CreditsId;
        OpenCard(CreditsTitle, CreditsMadeBy, CreditsArt, new[] { (BackId, "Back") }, 0);
    }

    public void ShowProblem(string line)
    {
        if (problem == null) return;
        problem.text = line;
        problem.gameObject.SetActive(true);
        interactive = true;   // the card stays, and Keep playing still works
        Refuse();
    }

    void OpenCard(string title, string body, string extra, (string id, string word)[] buttons, int first)
    {
        foreach (Transform child in cardRoot) Destroy(child.gameObject);
        card.Clear();
        problem = null;
        float height = extra != null ? 400f : 340f;
        RectTransform box = Box("Card box", cardRoot);
        Corner(box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(860f, height));
        UiSkin.Paint(box, Outline, 28f);
        UiSkin.DropShadow(box, 12f, 8f, .4f);
        RectTransform inner = Box("Fill", box);
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(5f, 5f); inner.offsetMax = new Vector2(-5f, -5f);
        UiSkin.Paint(inner, Cream, 24f);
        float top = 36f;
        top = Line(inner, title, 44f, FontStyles.Bold, top) + 10f;
        top = Line(inner, body, 32f, UiSkin.SemiBold, top) + 6f;
        if (extra != null) top = Line(inner, extra, 28f, UiSkin.SemiBold, top) + 6f;
        problem = UiSkin.Text("Problem", inner, 28f, UiSkin.Red, TextAlignmentOptions.Top, FontStyles.Bold);
        problem.rectTransform.anchorMin = new Vector2(0f, 1f);
        problem.rectTransform.anchorMax = new Vector2(1f, 1f);
        problem.rectTransform.pivot = new Vector2(.5f, 1f);
        problem.rectTransform.anchoredPosition = new Vector2(0f, -top);
        problem.rectTransform.sizeDelta = new Vector2(-40f, 36f);
        problem.gameObject.SetActive(false);
        const float w = 300f, h = 72f, gap = 28f;
        float total = buttons.Length * w + (buttons.Length - 1) * gap;
        for (int i = 0; i < buttons.Length; i++)
        {
            Slab s = MakeSlab(inner, buttons[i].id, buttons[i].word, null, new Vector2(w, h));
            Corner(s.root, new Vector2(.5f, 0f), new Vector2(.5f, .5f), new Vector2(-total / 2f + w / 2f + i * (w + gap), 40f + h / 2f), new Vector2(w, h));
            s.label.alignment = TextAlignmentOptions.Midline;
            card.Add(s);
        }
        cardRoot.gameObject.SetActive(true);
        active = card;
        cardHorizontal = true;
        Select(first, false);
        RefreshStrip(true);
    }

    float Line(RectTransform parent, string words, float px, FontStyles style, float top)
    {
        TextMeshProUGUI t = UiSkin.Text("Line", parent, px, Outline, TextAlignmentOptions.Top, style, true);
        t.text = words;
        t.rectTransform.anchorMin = new Vector2(0f, 1f);
        t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.pivot = new Vector2(.5f, 1f);
        t.rectTransform.anchoredPosition = new Vector2(0f, -top);
        float h = Mathf.Ceil(px * 1.3f);
        t.rectTransform.sizeDelta = new Vector2(-60f, h);
        return top + h;
    }

    void CloseCard()
    {
        cardRoot.gameObject.SetActive(false);
        foreach (Transform child in cardRoot) Destroy(child.gameObject);
        card.Clear();
        active = menu;
        cardHorizontal = false;
        int back = menu.FindIndex(s => s.id == returnTo);
        Select(back >= 0 ? back : 0, false);
        RefreshStrip(true);
    }

    public void Lock() => interactive = false;

    public void Refuse()
    {
        if (active != null && selected >= 0 && selected < active.Count) active[selected].popAt = Time.unscaledTime;
        Sfx.Play2D("bench.showleft");   // PLACEHOLDER: the bench's soft "not that" as the refusal
    }

    public void ToSettings() => interactive = false;

    public void BackFromSettings()
    {
        interactive = true;
        int at = menu.FindIndex(s => s.id == SettingsId);
        if (active == menu && at >= 0) Select(at, false);
        lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : lastMouse;
        skipFrame = Time.frameCount;   // the press that put the phone away does nothing here
    }

    int skipFrame = -10;

    public IEnumerator FadeToBlack(float seconds)
    {
        fade.gameObject.SetActive(true);
        float into = 0f;
        while (into < seconds)
        {
            into += Time.unscaledDeltaTime;
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(into / seconds));
            yield return null;
        }
        fade.color = Color.black;
    }

    // ---------- 26 Oct 2026 (Start screen polish) ----------

    /// <summary>Up from black as the title begins (the furniture and the crowd are laid in the first frames).</summary>
    public void FadeFromBlack(float seconds) => StartCoroutine(FadeIn(seconds));

    IEnumerator FadeIn(float seconds)
    {
        fade.gameObject.SetActive(true);
        fade.color = Color.black;
        float into = 0f;
        while (into < seconds)
        {
            into += Time.unscaledDeltaTime;
            fade.color = new Color(0f, 0f, 0f, 1f - UiTween.Eased(UiTween.Ease.OutQuad, Mathf.Clamp01(into / seconds)));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.gameObject.SetActive(false);
    }

    /// <summary>The menu, the strip and any card go away (the dive has begun).</summary>
    public void HideMenu()
    {
        interactive = false;
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
        if (stripPlate != null) stripPlate.gameObject.SetActive(false);
        if (cardRoot != null) cardRoot.gameObject.SetActive(false);
    }

    /// <summary>How dark the frame's edges are (0 none; the diorama .22, the frozen rush .34), eased over 0.4 s.</summary>
    public void Vignette(float strength) => vignetteTarget = Mathf.Clamp01(strength);

    // A radial falloff: clear in the middle, dark at the corners (a 128 px texture stretched over the screen).
    static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Start screen - vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);   // 0 in the middle, about 1.41 at a corner
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - .55f) / .85f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        vignetteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        vignetteSprite.name = texture.name;
        vignetteSprite.hideFlags = HideFlags.DontSave;
        return vignetteSprite;
    }

    void StepVignette()
    {
        if (vignette == null) return;
        vignetteNow = Mathf.MoveTowards(vignetteNow, vignetteTarget, Time.unscaledDeltaTime / .4f * .34f);
        Color c = vignette.color;
        c.a = vignetteNow;
        vignette.color = c;
    }

    // ------------------------------------------------------------------ every frame

    void Update()
    {
        StepLogo();
        StepSlabs();
        StepGrade();
        StepVignette();
        StepRewind();
        RefreshStrip(false);
        if (!interactive || active == null || active.Count == 0) return;
        if (PausePhone.Paused || Time.frameCount <= skipFrame + 1) return;

        Keyboard keys = Keyboard.current;
        bool upKey = keys != null && (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame);
        bool downKey = keys != null && (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame);
        bool leftKey = keys != null && (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame);
        bool rightKey = keys != null && (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame);
        bool choose = keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame);
        bool back = keys != null && keys.escapeKey.wasPressedThisFrame;

        bool padUp = PadInput.Pressed(PadButton.DpadUp), padDown = PadInput.Pressed(PadButton.DpadDown);
        bool padLeft = PadInput.Pressed(PadButton.DpadLeft), padRight = PadInput.Pressed(PadButton.DpadRight);
        // The left stick as a D-pad: one step per push past 0.6, ready again once it is back under 0.4.
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            float strongest = Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y));
            if (!stickHeld && strongest > .6f)
            {
                stickHeld = true;
                if (Mathf.Abs(stick.y) >= Mathf.Abs(stick.x)) { if (stick.y > 0f) padUp = true; else padDown = true; }
                else { if (stick.x < 0f) padLeft = true; else padRight = true; }
            }
            else if (stickHeld && strongest < .4f) stickHeld = false;
        }
        choose |= PadInput.Pressed(PadButton.South);
        back |= PadInput.Pressed(PadButton.East);

        int step = 0;
        if (upKey || padUp) step = -1;
        if (downKey || padDown) step = 1;
        if (cardHorizontal)
        {
            if (leftKey || padLeft) step = -1;
            if (rightKey || padRight) step = 1;
        }
        if (step != 0) Select(Mathf.Clamp(selected + step, 0, active.Count - 1), true);

        // The mouse: hover selects (only when it moves, so a resting pointer never fights the pad); a click chooses.
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 at = mouse.position.ReadValue();
            int under = SlabUnder(at);
            if ((at - lastMouse).sqrMagnitude > 1f && under >= 0 && under != selected) Select(under, true);
            lastMouse = at;
            if (mouse.leftButton.wasPressedThisFrame && under >= 0) { Select(under, false); choose = true; }
        }

        if (choose) { Choose(); return; }
        if (back && CardShowing) { Sfx.Play2D("phone.close"); CloseCard(); }
    }

    int SlabUnder(Vector2 screenPoint)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].root != null && RectTransformUtility.RectangleContainsScreenPoint(active[i].root, screenPoint, null)) return i;
        return -1;
    }

    void Select(int index, bool withSound)
    {
        if (active == null || active.Count == 0) return;
        index = Mathf.Clamp(index, 0, active.Count - 1);
        bool changed = index != selected;
        selected = index;
        for (int i = 0; i < active.Count; i++)
        {
            bool on = i == selected;
            active[i].fill.color = on ? Honey : Cream;
            if (active[i].lip != null) active[i].lip.color = on ? HoneyLip : CreamLip;
        }
        if (changed || !withSound) active[selected].popAt = Time.unscaledTime;
        // PLACEHOLDER (26 Oct 2026, Start screen polish): the research's cup-clink per move, from the café's own cup set down
        // on wood ("cup.set", flat and soft), until a menu tick is made; "phone.tap" was the first cut's.
        if (changed && withSound) Sfx.Play2D("cup.set", .55f);
    }

    void Choose()
    {
        Slab s = active[selected];
        s.popAt = Time.unscaledTime;
        Sfx.Play2D("ui.confirm");
        if (s.id == KeepPlayingId || s.id == BackId) { CloseCard(); return; }
        if (s.id == StartNewId) interactive = false;
        Chosen?.Invoke(s.id);
    }

    // 1.0 -> 1.08 -> 0.97 -> 1.0 over 180 ms: up fast, a squash, then settle on UiTween's out-back.
    public static float PopScale(float seconds)
    {
        float u = seconds / PopSeconds;
        if (u < 0f || u >= 1f) return 1f;
        if (u < .35f) return Mathf.Lerp(1f, 1.08f, UiTween.Eased(UiTween.Ease.OutQuad, u / .35f));
        if (u < .65f) return Mathf.Lerp(1.08f, .97f, Mathf.SmoothStep(0f, 1f, (u - .35f) / .3f));
        return Mathf.LerpUnclamped(.97f, 1f, UiTween.Eased(UiTween.Ease.OutBack, (u - .65f) / .35f));
    }

    void StepSlabs()
    {
        float now = Time.unscaledTime;
        StepList(menu, now, true);
        StepList(card, now, false);
    }

    void StepList(List<Slab> list, float now, bool slideIn)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Slab s = list[i];
            if (s.root == null) continue;
            float k = PopScale(now - s.popAt);
            s.root.localScale = new Vector3(k, k, 1f);
            if (slideIn && menuGroup != null)
            {
                // The slabs arrive from the left, one after another (40 ms apart); the selection is there from the first frame.
                float t = Mathf.Clamp01((now - menuAt - i * .04f) / .22f);
                float x = Mathf.Lerp(-80f, 0f, UiTween.Eased(UiTween.Ease.OutCubic, t));
                Vector2 p = s.root.anchoredPosition;
                float home = 84f + s.root.sizeDelta.x / 2f;
                s.root.anchoredPosition = new Vector2(home + x + (i == selected && active == menu ? 10f : 0f), p.y);
            }
        }
        if (slideIn && menuGroup != null) menuGroup.alpha = Mathf.Clamp01((now - menuAt) / .12f);
    }

    void StepLogo()
    {
        if (logo == null || !logo.gameObject.activeSelf) return;
        float t = Time.unscaledTime - logoAt;
        Vector2 home = new Vector2(84f, -64f);
        if (logoSlam)
        {
            // Like an order ticket slapped on the rail: a drop (120 ms), a squash (to 1.12 x 0.84), a settle (to 300 ms).
            if (t < .12f)
            {
                float d = UiTween.Eased(UiTween.Ease.InQuad, t / .12f);
                logo.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(320f, 0f, d));
                logo.localScale = Vector3.one;
            }
            else if (t < .19f)
            {
                float s = Mathf.SmoothStep(0f, 1f, (t - .12f) / .07f);
                logo.anchoredPosition = home;
                logo.localScale = new Vector3(Mathf.Lerp(1f, 1.12f, s), Mathf.Lerp(1f, .84f, s), 1f);
            }
            else
            {
                float s = UiTween.Eased(UiTween.Ease.OutBack, Mathf.Clamp01((t - .19f) / .11f));
                logo.anchoredPosition = home;
                logo.localScale = new Vector3(Mathf.LerpUnclamped(1.12f, 1f, s), Mathf.LerpUnclamped(.84f, 1f, s), 1f);
            }
            logoText.alpha = 1f;
        }
        else
        {
            float s = UiTween.Eased(UiTween.Ease.OutCubic, Mathf.Clamp01(t / .5f));
            logo.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(24f, 0f, s));
            logo.localScale = Vector3.one;
            logoText.alpha = s;
        }
    }

    void StepGrade()
    {
        gradeNow = Mathf.MoveTowards(gradeNow, gradeTarget, Time.unscaledDeltaTime / .3f * .2f);
        Color c = grade.color;
        c.a = gradeNow;
        grade.color = c;
    }

    // The strip says what the hands do now, in icons for the device in hand (rebuilt when the device changes).
    void RefreshStrip(bool force)
    {
        if (strip == null || !stripPlate.gameObject.activeSelf) return;
        if (!force && stripFrame == PadInput.SwitchedAtFrame) return;
        stripFrame = PadInput.SwitchedAtFrame;
        strip.text = $"{ControlHints.Confirm} Choose   ·   {ControlHints.Back} Back";
        Vector2 size = strip.GetPreferredValues(strip.text);
        stripPlate.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 44f, 50f);
    }

    // ------------------------------------------------------------------ "Before it got like this."

    void BuildRewind()
    {
        rewindRoot = Stretch("Rewind", screen);
        rewindGroup = rewindRoot.gameObject.AddComponent<CanvasGroup>();
        rewindGroup.blocksRaycasts = false;
        UiSkin.Paint(Stretch("Cool cast", rewindRoot), new Color(.55f, .7f, .95f, .22f), 0f);
        for (int i = 0; i < 46; i++)
        {
            RectTransform line = Box("Scanline", rewindRoot);
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(.5f, .5f);
            line.sizeDelta = new Vector2(0f, 3f);
            line.anchoredPosition = new Vector2(0f, i * 24f);
            UiSkin.Paint(line, new Color(0f, 0f, 0f, .16f), 0f);
            scanlines.Add(line);
        }
        var rng = new System.Random(7);
        for (int i = 0; i < 16; i++)
        {
            RectTransform streak = Box("Streak", rewindRoot);
            streak.anchorMin = streak.anchorMax = Vector2.zero;
            streak.pivot = new Vector2(0f, .5f);
            streak.sizeDelta = new Vector2(240f + (float)rng.NextDouble() * 700f, 3f + (float)rng.NextDouble() * 8f);
            streak.anchoredPosition = new Vector2((float)rng.NextDouble() * 1920f, 60f + (float)rng.NextDouble() * 960f);
            UiSkin.Paint(streak, new Color(1f, 1f, 1f, .18f + (float)rng.NextDouble() * .22f), 2f);
            streaks.Add(streak);
        }
        RectTransform plate = Box("Band", rewindRoot);
        Corner(plate, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(820f, 120f));
        UiSkin.Paint(plate, UiSkin.Band, 22f);
        for (int i = 0; i < 2; i++)
        {
            RectTransform tri = Box("Rewind arrow", plate);
            Corner(tri, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(34f + i * 34f, 0f), new Vector2(46f, 52f));
            Image image = tri.gameObject.AddComponent<Image>();
            image.sprite = Triangle();
            image.color = Cream;
            image.raycastTarget = false;
        }
        rewindLabel = UiSkin.Text("Words", plate, 48f, Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        rewindLabel.text = RewindLine;
        rewindLabel.rectTransform.anchorMin = Vector2.zero;
        rewindLabel.rectTransform.anchorMax = Vector2.one;
        rewindLabel.rectTransform.offsetMin = new Vector2(140f, 0f);
        rewindLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);
        // 26 Oct 2026 (Start screen polish): the punch. A white flash on the first frame of the rewind (gone in 0.2 s), so the
        // cut from the frozen rush to the running tape is felt; the band drops in over the first 0.25 s rather than being there.
        flash = UiSkin.Paint(Stretch("Flash", rewindRoot), new Color(1f, 1f, 1f, 0f), 0f);
        rewindBand = plate;
        rewindRoot.gameObject.SetActive(false);
    }

    RectTransform rewindBand;

    float rewindAt = -10f, rewindLength = 1.8f;

    public IEnumerator PlayRewind(float seconds)
    {
        rewindLength = Mathf.Max(.5f, seconds);
        rewindAt = Time.unscaledTime;
        rewindRoot.gameObject.SetActive(true);
        menuRoot.gameObject.SetActive(false);
        stripPlate.gameObject.SetActive(false);
        gradeTarget = 0f;
        StepRewind();
        while (Time.unscaledTime - rewindAt < rewindLength) yield return null;
    }

    void StepRewind()
    {
        if (rewindRoot == null || !rewindRoot.gameObject.activeSelf) return;
        float t = Time.unscaledTime - rewindAt;
        float dt = Time.unscaledDeltaTime;
        rewindGroup.alpha = Mathf.Clamp01(t / .12f);
        // The streaks race right to left (a tape running back), the scanlines roll, the frame shivers.
        foreach (RectTransform s in streaks)
        {
            Vector2 p = s.anchoredPosition;
            p.x -= 4200f * dt;
            if (p.x < -s.sizeDelta.x) p.x = 1920f + UnityEngine.Random.value * 400f;
            s.anchoredPosition = p;
        }
        float roll = Mathf.Repeat(t * 260f, 24f);
        for (int i = 0; i < scanlines.Count; i++) scanlines[i].anchoredPosition = new Vector2(0f, i * 24f - roll);
        // The shiver is strongest as the tape catches (the first half second) and settles to a tremor.
        float shiver = Mathf.Lerp(5f, 2f, Mathf.Clamp01(t / .5f));
        screen.anchoredPosition = new Vector2(Mathf.Sin(t * 90f) * shiver, Mathf.Sin(t * 61f) * shiver * .4f);
        if (flash != null) flash.color = new Color(1f, 1f, 1f, .7f * (1f - Mathf.Clamp01(t / .2f)));
        if (rewindBand != null)
        {
            float d = UiTween.Eased(UiTween.Ease.OutBack, Mathf.Clamp01(t / .25f));
            rewindBand.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(60f, 0f, d));
            rewindBand.localScale = Vector3.one * Mathf.LerpUnclamped(.92f, 1f, d);
        }
    }

    // A triangle pointing left, edges smoothed (the "◀" of the rewind).
    static Sprite Triangle()
    {
        if (triangle != null) return triangle;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Start screen - rewind arrow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f, py = y + .5f;
                // Inside the triangle (4, 32), (60, 4), (60, 60): the distance to its nearest edge, antialiased over a pixel.
                float d = Mathf.Min(Edge(px, py, 4f, 32f, 60f, 4f), Mathf.Min(Edge(px, py, 60f, 4f, 60f, 60f), Edge(px, py, 60f, 60f, 4f, 32f)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(d + .5f) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        triangle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        triangle.name = texture.name;
        triangle.hideFlags = HideFlags.DontSave;
        return triangle;
    }

    // Signed distance from (px, py) to the edge a->b, positive on the inside of a counter-clockwise triangle.
    static float Edge(float px, float py, float ax, float ay, float bx, float by)
    {
        float ex = bx - ax, ey = by - ay;
        float len = Mathf.Sqrt(ex * ex + ey * ey);
        return ((px - ax) * ey - (py - ay) * ex) / Mathf.Max(.0001f, len) * -1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { triangle = null; vignetteSprite = null; }
}
