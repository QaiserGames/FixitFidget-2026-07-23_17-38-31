using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// HER MARK: A "?" OVER HER HEAD THAT FILLS AND TURNS RED (break-ins chunk C, 6 Oct 2026; claude/break-ins-spec.md §6,
// "reading the danger"; claude/chunk-c-grace-at-home-plan.md §3: "drawn like the new juice badges")
//
//   NoticeMark.Show(who, amount)   0 hides it; above 0 a paper badge with a "?" stands beside the head, filling from the
//                                  bottom, amber to red as it fills. Crossing a third it pops (she says "Hm?"); full, it
//                                  pulses red (caught).
//
// One badge (only Grace has a mark so far). Built in code on its own screen canvas, sorted with the juice badges (76: over
// them, under the barks' 80 and the night's notes and cards' 90), pinned beside the head the way barks are
// (Barks.HeadPointOf), the same size at any zoom; drawn in code, so nothing in the scene changes and no asset is added.
// Nothing allocates once it's built.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(915)]
public sealed class NoticeMark : MonoBehaviour
{
    const float Size = 54f;
    // Beside the head (reference pixels), so a bark over the head never covers it.
    static readonly Vector2 Beside = new Vector2(-58f, -14f);
    static readonly Color Amber = new Color(.96f, .7f, .24f, 1f), Red = new Color(.86f, .2f, .16f, 1f);

    static NoticeMark instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    Canvas canvas;
    RectTransform badge;
    CanvasGroup group;
    Image fill;
    TextMeshProUGUI glyph;
    Camera cam;
    Transform who;
    float amount, shown, popAt = -10f, wanted;
    bool hidden;

    /// <summary>Her mark over <paramref name="speaker"/>'s head: 0 hides it, 1 is full (caught).</summary>
    public static void Show(Transform speaker, float mark)
    {
        if (!Application.isPlaying) return;
        if (instance == null)
        {
            if (mark <= 0f || speaker == null) return;
            var go = new GameObject("Her mark (while playing)");
            instance = go.AddComponent<NoticeMark>();
        }
        instance.Set(speaker, mark);
    }

    public static void Hide()
    {
        if (instance != null) instance.Set(null, 0f);
    }

    /// <summary>For checks: the mark as drawn now (0 when hidden) and where (screen pixels).</summary>
    public static float Drawn => instance != null && instance.group != null ? instance.group.alpha * instance.amount : 0f;
    public static Vector2 OnScreen => instance != null && instance.badge != null ? instance.badge.anchoredPosition * Mathf.Max(.01f, instance.canvas.scaleFactor) : Vector2.zero;

    void Set(Transform speaker, float mark)
    {
        if (speaker != null) who = speaker;
        mark = Mathf.Clamp01(mark);
        if (amount < GraceNight.Third && mark >= GraceNight.Third) popAt = UiClock.Now;
        amount = mark;
        wanted = mark > .005f && who != null ? 1f : 0f;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    void OnEnable() => Canvas.willRenderCanvases += Place;
    void OnDisable() => Canvas.willRenderCanvases -= Place;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Build()
    {
        var canvasObject = new GameObject("Her mark (screen)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 76;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        badge = new GameObject("Badge", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        badge.SetParent(canvasObject.transform, false);
        badge.anchorMin = badge.anchorMax = Vector2.zero;
        badge.pivot = new Vector2(.5f, 0f);
        badge.sizeDelta = new Vector2(Size, Size);
        group = badge.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image disc = Layer("Paper", DiscSprite(true));
        fill = Layer("Fill", DiscSprite(false));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.color = Amber;
        disc.color = Color.white;

        glyph = new GameObject("?", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        glyph.rectTransform.SetParent(badge, false);
        Stretch(glyph.rectTransform);
        glyph.text = "?";
        glyph.fontSize = 38f;
        glyph.fontStyle = FontStyles.Bold;
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.raycastTarget = false;
        glyph.color = Ink;
        TMP_FontAsset font = UiSkin.Font;
        if (font != null) glyph.font = font;
    }

    Image Layer(string name, Sprite sprite)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(badge, false);
        Stretch(image.rectTransform);
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void Update()
    {
        bool hide = RecapUI.Showing;
        if (hide != hidden)
        {
            hidden = hide;
            canvas.enabled = !hide;
        }
        // On the UI's own clock (UiClock): it holds while the phone pauses the game.
        shown = Mathf.MoveTowards(shown, wanted, UiClock.Delta / (wanted > shown ? .12f : .45f));
    }

    // Just before the canvases are drawn, after every camera has moved this frame (as the barks and the juice do).
    void Place()
    {
        if (canvas == null || hidden) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (who == null || cam == null || shown <= 0f)
        {
            group.alpha = 0f;
            return;
        }
        Vector3 s = cam.WorldToScreenPoint(Barks.HeadPointOf(who));
        if (s.z < 0f) { group.alpha = 0f; return; }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float now = UiClock.Now;
        // A pop as it passes a third; full, a slow pulse.
        float t = now - popAt;
        float grow = t < .1f ? Mathf.Lerp(1f, 1.35f, t / .1f) : t < .3f ? Mathf.Lerp(1.35f, 1f, (t - .1f) / .2f) : 1f;
        if (GraceNight.Caught(amount)) grow *= 1f + .12f * Mathf.Sin(now * 9f);
        badge.localScale = new Vector3(grow, grow, 1f);
        badge.anchoredPosition = new Vector2(s.x / scale, s.y / scale) + Beside;
        group.alpha = shown;
        fill.fillAmount = Mathf.Lerp(.1f, 1f, amount);
        fill.color = Color.Lerp(Amber, Red, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GraceNight.Third, 1f, amount)));
        glyph.color = amount >= GraceNight.Third ? Paper : Ink;
    }

    // ---------------------------------------------------------------- the badge, drawn in code (as Juice draws its own)

    const int Px = 96;
    static readonly Color Ink = UiSkin.Ink;      // the UI skin's (playtest 3, session 3)
    static readonly Color Paper = UiSkin.Paper;
    static Sprite paperSprite, fillSprite;

    // The paper disc with its dark rim and soft shadow; or (fill) the disc inside the rim, white, to be tinted.
    static Sprite DiscSprite(bool paper)
    {
        if (paper && paperSprite != null) return paperSprite;
        if (!paper && fillSprite != null) return fillSprite;
        var tex = new Texture2D(Px, Px, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "Her mark",
        };
        var pixels = new Color[Px * Px];
        float aa = 1.2f / Px;
        for (int y = 0; y < Px; y++)
            for (int x = 0; x < Px; x++)
            {
                var p = new Vector2((x + .5f) / Px - .5f, (y + .5f) / Px - .5f);
                float disc = p.magnitude - .44f;
                Color c;
                if (paper)
                {
                    float shadow = (p + new Vector2(0f, .025f)).magnitude - .45f;
                    c = new Color(0f, 0f, 0f, .28f * Mathf.Clamp01(.5f - shadow / (aa * 4f)));
                    c = Over(c, Ink, Mathf.Clamp01(.5f - disc / aa));
                    c = Over(c, Paper, Mathf.Clamp01(.5f - (disc + .035f) / aa));
                }
                else c = new Color(1f, 1f, 1f, Mathf.Clamp01(.5f - (disc + .035f) / aa));
                pixels[y * Px + x] = c;
            }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, Px, Px), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        if (paper) paperSprite = sprite; else fillSprite = sprite;
        return sprite;
    }

    static Color Over(Color under, Color over, float a)
    {
        if (a <= 0f) return under;
        float outA = a + under.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color c = (over * a + under * under.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }
}
