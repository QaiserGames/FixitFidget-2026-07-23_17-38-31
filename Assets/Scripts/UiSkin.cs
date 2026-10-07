using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// ONE UI SKIN (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.1)
//
// Mansoor's playtest: "the UI isn't game-ready". Every screen had grown its own look: seven pasted copies of the "find a
// font" code, four different text shadows, palettes copied across ten files, three different rounded-corner sprites.
// This is the one family they all draw from now:
//
//   Colours   the recap phone's (its cream screen, its white cards, its dark ink, its warm accent and gold), plus the brand
//             green from the style guide (#2E7D5B, used nowhere in the UI until now): money, OPEN, the day's progress.
//             Text over the café (barks, subtitles, tooltips, the night's notes) sits on Band, the phone's ink, see-through.
//   Font      UiSkin.Font. One swap point: a TextMesh Pro font asset at Resources/UI/UI font, if there is one (the font
//             Mansoor picks goes there); otherwise the HUD's own lettering, as before; otherwise TextMesh Pro's default.
//   Shadow    UiSkin.Shadowed(text): one soft dark underlay, on one material shared by every text that uses it (it used
//             to be a material per screen). For text drawn over the world; text on a card needs none.
//   Corners   one rounded sprite (the phone's: a 96 px square, its corner 40 px) that draws any radius by its pixels-per-
//             unit (Round). Radius is the HUD's cards and bands; the phone keeps its own sizes for its own parts.
//   Drop      UiSkin.DropShadow(card): one soft shadow under a card, a sibling drawn first.
//
// Everything here is made in code while playing, once, and never saved: nothing in the scene or the project changes.
// No screen's drawing order changes either (the checks rely on it: the night's notebook 40, the straight face 45, the
// counter phone 55, the recap phone 60, the juice 75, her mark 76, the barks and the circuit 80, the night's card 90, the
// pad's cursor 5000); the new HUD corners draw at 2, under all of them, and the phone by day at 95, over all but the cursor.
// ---------------------------------------------------------------------------
public static class UiSkin
{
    static Color Hex(int rgb, float alpha = 1f) =>
        new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

    // ------------------------------------------------------------------ the colours

    /// <summary>The phone's screen: the HUD's paper (tabs, the sign's face, cards over the café).</summary>
    public static readonly Color Paper = Hex(0xf7f3ee);
    /// <summary>A card on the paper.</summary>
    public static readonly Color Card = Color.white;
    /// <summary>Dark writing, and dark cards (the day's takings on the phone).</summary>
    public static readonly Color Ink = Hex(0x1f1b17);
    public static readonly Color InkSoft = Hex(0x6b645c);
    public static readonly Color InkFaint = Hex(0xa39b91);
    /// <summary>A rule across a card; a track under a bar.</summary>
    public static readonly Color Rule = Hex(0xece6de);
    public static readonly Color Track = Hex(0xefe9e1);

    /// <summary>The brand green (the style guide's): money, OPEN, the day so far.</summary>
    public static readonly Color Brand = Hex(0x2e7d5b);
    /// <summary>The brand green lightened for the dark: a key's letters over the café, a glow.</summary>
    public static readonly Color BrandBright = Hex(0x7fd3a8);
    public static readonly Color BrandSoft = Hex(0xdcefe1);
    /// <summary>The brand green darkened: a bill's edge, a pressed button.</summary>
    public static readonly Color BrandDeep = Hex(0x1f5a41);

    /// <summary>The phone's warm accent: low stock, losing patience.</summary>
    public static readonly Color Accent = Hex(0xc9702d);
    public static readonly Color AccentSoft = Hex(0xf6e3d3);
    /// <summary>Gold: stars, last orders.</summary>
    public static readonly Color Gold = Hex(0xf2a61f);
    public static readonly Color GoldSoft = Hex(0xfdebd0);
    public static readonly Color GoldInk = Hex(0xa8620f);
    public static readonly Color Red = Hex(0xb8483c);
    public static readonly Color RedSoft = Hex(0xf6d9d4);

    /// <summary>The band behind text over the café: the phone's ink, see-through.</summary>
    public static readonly Color Band = Hex(0x1f1b17, .86f);
    /// <summary>Writing on the band (the phone's text on its dark card).</summary>
    public static readonly Color BandText = Hex(0xf7efe6);
    public static readonly Color BandFaint = Hex(0xcbbfb2);
    public static readonly Color BandLine = Hex(0x3a332d);

    /// <summary>"#RRGGBB" for rich text.</summary>
    public static string HexOf(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // ------------------------------------------------------------------ the sizes (reference pixels, 1920 x 1080)

    /// <summary>The HUD's corner radius: cards, bands, chips.</summary>
    public const float Radius = 12f;
    /// <summary>The space kept clear at the screen's edges by the HUD's corners.</summary>
    public const float Margin = 24f;

    // ------------------------------------------------------------------ the font

    /// <summary>Where the chosen font goes: a TextMesh Pro font asset at Assets/.../Resources/UI/UI font.</summary>
    public const string FontResource = "UI/UI font";

    static TMP_FontAsset font;
    static bool fontFromResources;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        font = null;
        fontFromResources = false;
        shadowMaterial = null;
        shadowFor = null;
        rounded = null;
        soft = null;
        circle = null;
    }

    /// <summary>The game's lettering (see the header).</summary>
    public static TMP_FontAsset Font
    {
        get
        {
            if (font != null) return font;
            font = Resources.Load<TMP_FontAsset>(FontResource);
            fontFromResources = font != null;
            if (font == null)
            {
                ShopUI hud = Object.FindAnyObjectByType<ShopUI>();
                TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
                if (any == null) any = Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (any != null && any.font != null) font = any.font;
            }
            if (font == null) font = TMP_Settings.defaultFontAsset;
            return font;
        }
    }

    /// <summary>True when the font came from the swap point (Resources/UI/UI font), not the HUD's own.</summary>
    public static bool FontChosen { get { _ = Font; return fontFromResources; } }

    /// <summary>Puts the skin's font on a text (scene texts too: the HUD's prompt, the conversation's lines).</summary>
    public static void UseFont(TMP_Text text)
    {
        if (text == null) return;
        TMP_FontAsset f = Font;
        if (f != null && text.font != f) text.font = f;
    }

    /// <summary>A text in the skin: its font, size, colour and style; no wrapping unless asked; never catching the pointer.</summary>
    public static TextMeshProUGUI Text(string name, Transform parent, float size, Color colour,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal, bool wrap = false)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        UseFont(text);
        text.fontSize = size;
        text.color = colour;
        text.alignment = align;
        text.fontStyle = style;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    // ------------------------------------------------------------------ the one shadow

    static Material shadowMaterial;
    static TMP_FontAsset shadowFor;

    /// <summary>
    /// The skin's soft dark underlay behind a text over the world, on one material shared by every such text in the skin's
    /// font. A text in another font (a scene text not moved onto the skin) gets its own copy.
    /// </summary>
    public static void Shadowed(TMP_Text text)
    {
        if (text == null) return;
        TMP_FontAsset f = text.font != null ? text.font : Font;
        if (f == null || f.material == null) return;
        Material material;
        if (f == Font)
        {
            if (shadowMaterial == null || shadowFor != f)
            {
                shadowMaterial = ShadowCopy(f.material, "UI skin shadow (while playing)");
                shadowFor = f;
            }
            material = shadowMaterial;
        }
        else material = ShadowCopy(f.material, f.name + " (UI skin shadow, while playing)");
        if (material != null)
        {
            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
        }
    }

    static Material ShadowCopy(Material from, string name)
    {
        if (from == null || !from.HasProperty(ShaderUtilities.ID_UnderlayColor)) return null;
        var material = new Material(from) { name = name, hideFlags = HideFlags.DontSave };
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, .82f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .3f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.45f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, .35f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .45f);
        return material;
    }

    // ------------------------------------------------------------------ the corners

    // The rounded sprite: a 96 px square whose corner is a 40 px arc, sliced at 40 px. Drawn at a radius r by
    // pixelsPerUnitMultiplier = 40 / r (the canvas's reference pixels per unit is Unity's default, 100, as the sprite's).
    const int RoundPixels = 96;
    const float RoundRadius = 40f;
    static Sprite rounded, soft, circle;

    /// <summary>The skin's rounded box (9-sliced; see Round for a radius).</summary>
    public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded();

    /// <summary>A soft-edged rounded box for drop shadows and glows (9-sliced).</summary>
    public static Sprite Soft => soft != null ? soft : soft = MakeSoft();

    /// <summary>A disc, its edge smoothed.</summary>
    public static Sprite Circle => circle != null ? circle : circle = MakeCircle(128);

    /// <summary>Makes an Image the skin's rounded box at <paramref name="radius"/> reference pixels.</summary>
    public static void Round(Image image, float radius = Radius)
    {
        if (image == null) return;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = RoundRadius / Mathf.Max(.5f, radius);
    }

    /// <summary>A rounded box in <paramref name="colour"/> on <paramref name="rect"/> (added, or the one there restyled).</summary>
    public static Image Paint(RectTransform rect, Color colour, float radius = Radius, bool hits = false)
    {
        if (rect == null) return null;
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        if (radius > 0f) Round(image, radius);
        else { image.sprite = null; image.type = Image.Type.Simple; }
        image.color = colour;
        image.raycastTarget = hits;
        return image;
    }

    /// <summary>
    /// The skin's one soft shadow under a card: a sibling drawn just before it, the card's size grown by
    /// <paramref name="spread"/> and dropped by <paramref name="drop"/> (reference pixels). It follows the card's anchors, so a
    /// card that moves or resizes takes it along (as long as they share a parent).
    /// </summary>
    public static Image DropShadow(RectTransform card, float spread = 10f, float drop = 4f, float strength = .32f)
    {
        if (card == null || card.parent == null) return null;
        var rect = new GameObject(card.name + " (shadow)", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(card.parent, false);
        rect.SetSiblingIndex(card.GetSiblingIndex());
        Follow(rect, card, spread, drop);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = Soft;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = new Color(0f, 0f, 0f, strength);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Lays <paramref name="shadow"/> under <paramref name="card"/> again (after the card moved or changed size).</summary>
    public static void Follow(RectTransform shadow, RectTransform card, float spread = 10f, float drop = 4f)
    {
        if (shadow == null || card == null) return;
        shadow.anchorMin = card.anchorMin;
        shadow.anchorMax = card.anchorMax;
        shadow.pivot = card.pivot;
        shadow.anchoredPosition = card.anchoredPosition + new Vector2(0f, -drop);
        shadow.sizeDelta = card.sizeDelta + new Vector2(spread * 2f, spread * 2f);
        shadow.localScale = card.localScale;
        shadow.localRotation = card.localRotation;
        // A pivot off the middle moves the grown box's middle off the card's: put it back.
        Vector2 off = new Vector2((card.pivot.x - .5f) * spread * 2f, (card.pivot.y - .5f) * spread * 2f);
        shadow.anchoredPosition += off;
    }

    /// <summary>A screen-space canvas for a code-built screen: 1920 x 1080 reference, half width half height.</summary>
    public static Canvas ScreenCanvas(string name, Transform parent, int order, bool raycasts = false)
    {
        var go = raycasts
            ? new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            : new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        if (parent != null) go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        return canvas;
    }

    // ------------------------------------------------------------------ drawing them

    static Texture2D NewTexture(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, false)
    {
        name = "UI skin - " + name,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
    };

    static Sprite Sliced(Texture2D texture, float border)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // A white square with round corners, edges smoothed (the phone's).
    static Sprite MakeRounded()
    {
        Texture2D texture = NewTexture("Rounded", RoundPixels);
        var pixels = new Color32[RoundPixels * RoundPixels];
        for (int y = 0; y < RoundPixels; y++)
            for (int x = 0; x < RoundPixels; x++)
            {
                float cx = x + .5f, cy = y + .5f;
                float dx = Mathf.Max(RoundRadius - cx, cx - (RoundPixels - RoundRadius), 0f);
                float dy = Mathf.Max(RoundRadius - cy, cy - (RoundPixels - RoundRadius), 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - RoundRadius;
                pixels[y * RoundPixels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - outside) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sliced(texture, RoundRadius);
    }

    // A rounded box whose edge fades out over 16 px (a drop shadow, a glow), sliced past the fade.
    static Sprite MakeSoft()
    {
        const int size = 96, inset = 18;
        const float radius = 16f, fade = 16f;
        Texture2D texture = NewTexture("Soft", size);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + .5f, cy = y + .5f;
                float lo = inset + radius, hi = size - inset - radius;
                float dx = Mathf.Max(lo - cx, cx - hi, 0f), dy = Mathf.Max(lo - cy, cy - hi, 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = Mathf.Clamp01(1f - (outside + fade * .5f) / fade);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sliced(texture, inset + radius);
    }

    static Sprite MakeCircle(int size)
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
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
