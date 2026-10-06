using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE STRAIGHT-FACE METER ON SCREEN (the Night 1 slice; MorningFace runs it)
//
// A small panel in the conversation's bottom row, beside the person's line, where Ace's replies and their
// key hints go (bottom right, since the dialogue pass): it takes their place while it's up, so the
// speaker's face and what they're saying both stay clear.
// Its title gives the key and "Keep a straight face"; below it a bar with its green mark (and a paler
// band either side, the "near enough"), and the needle sweeping across. When the needle stops, the
// title says how it went, and then the key to go on. HUD only: the reaction itself is in the
// conversation, on the person's face and in what they say (claude/ace-after-dark.md §3.2).
//
// Made the first time it's needed, while playing; never saved in the scene.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class StraightFaceUI : MonoBehaviour
{
    static readonly Color Backing = new Color(.075f, .07f, .065f, .92f);
    static readonly Color Ink = new Color(.95f, .92f, .85f);
    static readonly Color Track = new Color(.24f, .22f, .21f, 1f);
    static readonly Color GreenMark = new Color(.46f, .74f, .44f, 1f);
    static readonly Color NearBand = new Color(.46f, .74f, .44f, .38f);
    static readonly Color HeldInk = new Color(.62f, .9f, .58f);
    static readonly Color CrackedInk = new Color(1f, .62f, .52f);
    static string InkHex => ColorUtility.ToHtmlStringRGB(Ink);

    static StraightFaceUI instance;

    GameObject panel;
    TMP_Text title;
    RectTransform near, green, needle;
    StraightFaceMeter shown;

    /// <summary>True while the meter is on screen (reports and checks).</summary>
    public static bool Showing => instance != null && instance.panel != null && instance.panel.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    /// <summary>
    /// Show <paramref name="meter"/> as it is now (call every frame while it runs), titled
    /// <paramref name="title"/> (the key and what to do), or plain "Keep a straight face".
    /// </summary>
    public static void Draw(StraightFaceMeter meter, string title = null)
    {
        if (meter == null || !Application.isPlaying) return;
        Ensure().Place(meter, null, title);
    }

    /// <summary>
    /// The needle has stopped: say how it went until Hide, followed by <paramref name="then"/> (the key
    /// to go on) once there is one.
    /// </summary>
    public static void Result(StraightFaceMeter meter, string then = null) => Result(meter, null, null, then);

    /// <summary>
    /// The same, in other words (the officer's "Say nothing": "Said nothing." or "You flinched."); null keeps the usual
    /// "Straight face!" and "You cracked.".
    /// </summary>
    public static void Result(StraightFaceMeter meter, string heldWord, string crackedWord, string then = null)
    {
        if (meter == null || !Application.isPlaying) return;
        Ensure().Place(meter, meter.Held ? heldWord ?? "Straight face!" : crackedWord ?? "You cracked.", then);
    }

    public static void Hide()
    {
        if (instance != null && instance.panel != null) instance.panel.SetActive(false);
    }

    static StraightFaceUI Ensure()
    {
        if (instance != null) return instance;
        // An ordinary object of the Play session, so a meter on screen when Play stops goes with it
        // (PlaySessionLeftovers: marked DontSave, it stayed in the Game view and over the next session).
        var go = new GameObject("Straight-face meter (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        instance = go.AddComponent<StraightFaceUI>();
        instance.Build();
        return instance;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Place(StraightFaceMeter meter, string outcome, string extra)
    {
        if (!panel.activeSelf) panel.SetActive(true);
        if (shown != meter)
        {
            shown = meter;
            near.anchorMin = new Vector2(Mathf.Clamp01(meter.GreenLeft - meter.Near), 0f);
            near.anchorMax = new Vector2(Mathf.Clamp01(meter.GreenRight + meter.Near), 1f);
            green.anchorMin = new Vector2(meter.GreenLeft, 0f);
            green.anchorMax = new Vector2(meter.GreenRight, 1f);
            near.offsetMin = near.offsetMax = green.offsetMin = green.offsetMax = Vector2.zero;
        }
        needle.anchorMin = needle.anchorMax = new Vector2(meter.Needle, .5f);
        needle.anchoredPosition = Vector2.zero;
        if (outcome == null)
        {
            title.text = string.IsNullOrEmpty(extra) ? "Keep a straight face" : extra;
            title.color = Ink;
        }
        else
        {
            // The outcome in its colour, then the key to go on in the usual ink.
            title.text = string.IsNullOrEmpty(extra) ? outcome : $"{outcome}<color=#{InkHex}>      {extra}</color>";
            title.color = meter.Held ? HeldInk : CrackedInk;
        }
    }

    void Build()
    {
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        // Since the dialogue pass the person's line is a subtitle, x 390-1250 and from 44 up (bottom
        // aligned, up to three lines of it), and Ace's replies sit to its right, x 1304-1864 from 44 up
        // (ConversationUI.Look). The meter takes the replies' place, which the morning scene leaves empty:
        // x 1284-1864, 44 to 122 up, on the subtitle's baseline. (Under the old layout it sat in the key-hint
        // row under the line, which is where the subtitle is now; on top of the line it covered the
        // speaker's face.)
        RectTransform box = Rect("Meter", canvasObject.transform, new Vector2(1f, 0f), new Vector2(-56f, 44f), new Vector2(580f, 78f));
        box.pivot = new Vector2(1f, 0f);
        Image backing = box.gameObject.AddComponent<Image>();
        backing.color = Backing;
        backing.raycastTarget = false;
        panel = box.gameObject;

        RectTransform titleRect = Rect("Title", box, new Vector2(.5f, 1f), new Vector2(0f, -21f), new Vector2(560f, 34f));
        title = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = HudFont();
        if (font != null) title.font = font;
        title.fontSize = 25f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Ink;
        title.raycastTarget = false;

        RectTransform bar = Rect("Bar", box, new Vector2(.5f, 0f), new Vector2(0f, 22f), new Vector2(540f, 20f));
        Fill(bar, Track);
        near = Band("Near enough", bar, NearBand);
        green = Band("Green", bar, GreenMark);
        needle = Rect("Needle", bar, new Vector2(0f, .5f), Vector2.zero, new Vector2(6f, 34f));
        Fill(needle, Ink);
        panel.SetActive(false);
    }

    static RectTransform Band(string name, RectTransform bar, Color colour)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(bar, false);
        rect.anchorMin = new Vector2(.4f, 0f);
        rect.anchorMax = new Vector2(.6f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Fill(rect, colour);
        return rect;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Fill(RectTransform rect, Color colour)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
    }

    // The HUD's own lettering (as NightNotebook does).
    static TMP_FontAsset HudFont()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        if (any == null) any = FindAnyObjectByType<TextMeshProUGUI>();
        return any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
    }
}
