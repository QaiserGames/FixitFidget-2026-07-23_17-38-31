using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: THE NOTEBOOK AT NIGHT (claude/night-city-proposal.md §9)
//
// N (D-pad up on a pad) opens a page of Ace's notebook at the side of the screen: everything
// Ace has written down about the regulars, person by person, where they live first (see
// NotebookRecap.Page). Guesses say so ("likely", "hunch"). It only reads: nothing is written
// at night yet. Ace can keep walking with it open; the same key closes it.
//
// Only while a night walk runs: NightWalk adds it and calls Begin/End.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightNotebook : MonoBehaviour
{
    // A dark page like the game's other panels, with warm cream writing: thin lettering stays readable
    // at any window size (dark writing on paper went faint when the game view was small), and the
    // recap's own grey marks guesses ("likely", "hunch") the same way here.
    static readonly Color Backing = new Color(.075f, .07f, .065f, .93f);
    static readonly Color Ink = new Color(.95f, .92f, .85f);
    static readonly Color Faint = new Color(.62f, .6f, .56f);
    const string Grey = "#A6A6A6";

    GameObject canvas;
    RectTransform page;
    TMP_Text body, footer;
    float nextRefresh;

    public bool Open => page != null && page.gameObject.activeSelf;
    /// <summary>Times it was opened (for reports).</summary>
    public int Opened { get; private set; }
    /// <summary>How many facts the page showed when it was last drawn.</summary>
    public int FactsShown { get; private set; }

    public void Begin()
    {
        if (canvas != null) return;
        canvas = new GameObject("Night notebook (while the night runs)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(transform, false);
        var c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        TMP_FontAsset font = HudFont();
        page = Rect("Page", canvas.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-36f, -20f), new Vector2(540f, 720f));
        var backing = page.gameObject.AddComponent<Image>();
        backing.color = Backing;
        backing.raycastTarget = false;

        var title = Text("Title", page, new Vector2(28f, -22f), new Vector2(484f, 44f), 30f, Ink, font);
        title.text = "<b>Notebook</b>";
        body = Text("Facts", page, new Vector2(28f, -76f), new Vector2(484f, 580f), 23f, Ink, font);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.lineSpacing = 6f;
        footer = Text("Close", page, new Vector2(28f, -670f), new Vector2(484f, 32f), 18f, Faint, font);
        page.gameObject.SetActive(false);
    }

    public void End()
    {
        if (canvas != null) Destroy(canvas);
        canvas = null;
        page = null;
    }

    public void Show(bool open)
    {
        if (page == null) return;
        if (open && !Open) Opened++;
        page.gameObject.SetActive(open);
        if (open) Refresh();
    }

    void Update()
    {
        if (page == null || Time.timeScale <= 0f) return;
        bool pressed = Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame || PadInput.Pressed(PadButton.DpadUp);
        if (pressed) Show(!Open);
        if (Open && Time.unscaledTime >= nextRefresh) Refresh();
    }

    void Refresh()
    {
        nextRefresh = Time.unscaledTime + 1f;
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        FactsShown = notebook != null ? notebook.Count : 0;
        string text = NotebookRecap.Page(notebook);
        body.text = string.IsNullOrEmpty(text) ? "<color=" + Grey + ">Nothing written down yet.</color>" : text;
        footer.text = $"{ControlHints.NotebookPage}  Close";
    }

    void OnDestroy() => End();

    // The HUD's own lettering, so the page looks like part of the game; TextMesh Pro's default otherwise.
    static TMP_FontAsset HudFont()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        TMP_Text any = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
        if (any == null) any = FindAnyObjectByType<TextMeshProUGUI>();
        return any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color colour, TMP_FontAsset font)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.color = colour;
        text.raycastTarget = false;
        text.richText = true;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    public string Describe() => page == null ? "Notebook page: not set up." :
        $"Notebook page: {(Open ? "open" : "closed")}, opened {Opened} times; {FactsShown} facts on it when last drawn.";
}
