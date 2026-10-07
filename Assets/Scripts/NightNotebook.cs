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
    // recap's own grey marks guesses ("likely", "hunch") the same way here. The UI skin's (playtest 3,
    // session 3): its band, its writing, its lettering, its corners.
    static readonly Color Backing = new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .94f);
    static readonly Color Ink = UiSkin.BandText;
    static readonly Color Faint = UiSkin.BandFaint;
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

        TMP_FontAsset font = UiSkin.Font;
        page = Rect("Page", canvas.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-36f, -20f), new Vector2(540f, 720f));
        // The page's paper and its soft shadow inside it, so they come and go with it.
        RectTransform paper = Rect("Paper", page, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(540f, 720f));
        UiSkin.Paint(paper, Backing, 18f);
        UiSkin.DropShadow(paper, 14f, 6f, .4f);

        var title = Text("Title", page, new Vector2(28f, -22f), new Vector2(484f, 44f), 30f, Ink, font);
        title.text = "<b>Notebook</b>";
        // A rule under the title, in the band's own line colour.
        RectTransform rule = Rect("Rule", page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -66f), new Vector2(484f, 2f));
        UiSkin.Paint(rule, UiSkin.BandLine, 0f);
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
