using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: THE NOTEBOOK AT NIGHT (claude/night-city-proposal.md §9), WITH PICTURES (23 Oct 2026)
//
// N (D-pad up on a pad) opens Ace's notebook at the side of the screen. Ace can keep walking with it open; the same key
// closes it. It only reads: nothing is written at night from here. Only while a night walk runs: NightWalk adds it and
// calls Begin/End.
//
// 23 Oct 2026, his note of 22 Oct: "whenever I open the notes in nighttime, I it's like just a cluster of words, it's word
// vomit, and I rather like it being easier to see." His girlfriend (dyslexic) "felt overall lost most of the time" in
// playtest 7. So the page is pictures first, few words. Each entry is a subject (NoteSubjects: a person, a place, a thing):
//   * its polaroid (NotePhotos: the game's own model photographed, or a drawing put in its place; never blank), tilted,
//     taped on; a place also has a tiny street map card with a pin, turned the way the overhead camera looked when the
//     page was laid out (orbiting with it open doesn't redraw it);
//   * its name in bold (28 px) and one line of at most 8 words (26 px): what matters now.
// Tonight's errand comes first, in the bench's amber (#FFB238: a bar, an amber pin, the "Tonight" heading), and says what
// the Now line says (NightCycle.NowLine: the same state, read in the same order, with the same names). Then "People", then
// "What you've learned" (the places and things). Everything written about an entry sits behind a press, like the phone's
// "Press a name for everything": Tab or D-pad down opens the next entry's notes (passing over any with nothing written
// behind them, such as tonight's back door), each one whole (a guess still says "(hunch)"; his pages have his square
// bullet, not italics, which are harder for a dyslexic reader; an open entry moves up to the top of the screen if that's
// what it takes, and only if more are written than a whole screen holds does its last line say how many more the phone's
// Notes has), and after the last entry closes them. The press plays the notebook's sound, quieter (at full volume it says
// "a new line in the notebook"). Four entries fit the first screen, five at most; the rest scrolls as the press walks down
// the list (dots say there is more).
// While his scene plays (the Now line hides then), nothing is "tonight's" on the page either.
// Every word on the page is 26 px or more at 1080p (the facts were 24 and the footer 20: "your text needs to be bolder"),
// light on the dark page: the labs' word check (ReviewScreen) flags anything under 26.
//
// Playtest 7 (19 Oct 2026; his girlfriend: "you should give me a tip to do that"): the night's Now line (NightCycle.NowLine)
// ends with the notebook's icon and "notes" until the page has been opened once this Play session (EverOpened). And while
// Ace is choosing a reply at night (Barks.Choosing: up and down move the highlight), the D-pad moves the highlight and
// neither opens the page over the chips nor turns its notes.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightNotebook : MonoBehaviour
{
    /// <summary>The page has been opened at least once this Play session (the Now line's nudge goes once it has).</summary>
    public static bool EverOpened { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => EverOpened = false;

    // ---------- sizes: reference pixels at 1920 x 1080 ----------

    /// <summary>The words' sizes: the page's title, an entry's name, its line, the group headings, the footer, a note behind the press.</summary>
    public const float PageTitleSize = 32f, TitleSize = 28f, LineSize = 26f, HeadingSize = 26f, FooterSize = 26f, NoteSize = 26f;
    /// <summary>A polaroid's width (the spec's 96-120 px), and a place's map card's.</summary>
    public const float PolaroidWidth = 100f, MapWidth = 70f;
    /// <summary>The first screen holds at most this many entries (the spec: "no more than 5-6").</summary>
    public const int MaxFirstScreen = 6;

    const float PageWidth = 560f, PageHeight = 860f, EdgeMargin = 28f, Pad = 24f;
    const float ViewTop = 76f, ViewHeight = 712f, FooterTop = 800f;
    const float RowWidth = PageWidth - 2f * Pad;
    const float TextX = 156f;                 // an entry's words, from its row's left: the polaroid and a map card before them
    const float RowMin = 124f, RowGap = 8f, HeadingHeight = 38f;
    const float NoteIndent = 34f;             // a note behind the press, from the row's left (its bullet before it)

    // A dark page like the game's other panels, with warm cream writing: thin lettering stays readable at any window size
    // (dark writing on paper went faint when the game view was small). The UI skin's (playtest 3, session 3): its band, its
    // writing, its lettering, its corners. The pictures are the light things on it, so they are what the eye finds first.
    static readonly Color Backing = new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .94f);
    static readonly Color Ink = UiSkin.BandText;
    static readonly Color Faint = UiSkin.BandFaint;
    static readonly Color Panel = UiSkin.BandLine;
    static readonly Color HisInk = new Color(.62f, .32f, .30f);   // his pages' bullet: the man's suit

    GameObject canvas;
    RectTransform page, view, rows;
    TMP_Text footer;
    RectTransform moreAbove, moreBelow;
    float nextRefresh;
    string drawnFor = "";

    List<NoteEntry> entries = new List<NoteEntry>();
    int open = -1;   // the entry whose notes are shown, or -1
    int first;       // the first entry on the screen

    bool testing;
    Notebook testBook;
    NightLedger testLedger;
    NoteStage testStage;
    string testErrand = "";

    /// <summary>One entry as it is on the screen now: its row, its name and line, and its notes when it's pressed open.</summary>
    public sealed class Shown
    {
        public NoteEntry entry;
        public RectTransform row;
        public TMP_Text title, line;
        public bool open;
        /// <summary>Its notes as shown (with the "+N more on the phone" line last, when there is one).</summary>
        public readonly List<TMP_Text> notes = new List<TMP_Text>();
        /// <summary>How many of its facts didn't fit on the screen (the "+N more" line's N); 0 when every one shows.</summary>
        public int cut;
    }

    readonly List<Shown> shown = new List<Shown>();
    readonly List<TMP_Text> headings = new List<TMP_Text>();

    public bool Open => page != null && page.gameObject.activeSelf;
    /// <summary>Times it was opened (for reports).</summary>
    public int Opened { get; private set; }
    /// <summary>How many facts the notebook held when the page was last drawn.</summary>
    public int FactsShown { get; private set; }
    /// <summary>The page's entries, in order (tonight's first).</summary>
    public IReadOnlyList<NoteEntry> Entries => entries;
    /// <summary>The entries on the screen now, top to bottom.</summary>
    public IReadOnlyList<Shown> Visible => shown;
    /// <summary>The group headings on the screen now.</summary>
    public IReadOnlyList<TMP_Text> Headings => headings;
    /// <summary>The footer (its keys and words).</summary>
    public TMP_Text Footer => footer;
    /// <summary>The key of the entry whose notes are shown, or "".</summary>
    public string OpenKey => open >= 0 && open < entries.Count ? entries[open].key : "";
    /// <summary>There are entries above (scrolled past) or below (not on the screen yet).</summary>
    public bool MoreAbove => moreAbove != null && moreAbove.gameObject.activeSelf;
    public bool MoreBelow => moreBelow != null && moreBelow.gameObject.activeSelf;

    public void Begin()
    {
        if (canvas != null) return;
        Canvas c = UiSkin.ScreenCanvas("Night notebook (while the night runs)", transform, 40);
        // Scaled by the screen's height alone, so a 26 px word is 26 px at 1080p whatever the window's shape (the word check
        // measures against the height; half width, half height made the page's words smaller on a 16:10 screen).
        c.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        canvas = c.gameObject;

        page = Anchored("Page", canvas.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-EdgeMargin, -8f), new Vector2(PageWidth, PageHeight));
        // The page's paper and its soft shadow inside it, so they come and go with it.
        RectTransform paper = Anchored("Paper", page, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(PageWidth, PageHeight));
        UiSkin.Paint(paper, Backing, 18f);
        UiSkin.DropShadow(paper, 14f, 6f, .4f);

        TMP_Text title = Words("Title", page, "Notebook", PageTitleSize, Ink, FontStyles.Bold, Pad, 18f, RowWidth, false);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        // A rule under the title, in the band's own line colour.
        UiSkin.Paint(Box("Rule", page, Pad, 66f, RowWidth, 2f), Panel, 0f);
        view = Box("Entries", page, 0f, ViewTop, PageWidth, ViewHeight);
        UiSkin.Paint(Box("Rule (footer)", page, Pad, FooterTop - 6f, RowWidth, 2f), Panel, 0f);
        footer = Words("Close", page, "", FooterSize, Faint, UiSkin.SemiBold, Pad, FooterTop, RowWidth, false);
        footer.alignment = TextAlignmentOptions.MidlineLeft;
        Place(footer.rectTransform, Pad, FooterTop, RowWidth, 44f);
        moreAbove = Dots("More above", page, ViewTop - 4f);
        moreBelow = Dots("More below", page, ViewTop + ViewHeight - 10f);
        page.gameObject.SetActive(false);
    }

    public void End()
    {
        if (canvas != null) Destroy(canvas);
        canvas = null;
        page = null;
        view = rows = null;
        shown.Clear();
        headings.Clear();
    }

    public void Show(bool show)
    {
        if (page == null) return;
        if (show && !Open)
        {
            Opened++;
            EverOpened = true;
            open = -1;   // each time it opens: the first screen, nothing pressed open
            first = 0;
        }
        page.gameObject.SetActive(show);
        if (show) Refresh(true);
    }

    /// <summary>
    /// The press (Tab, D-pad down): the next entry's notes; after the last, none. An entry with nothing written behind it
    /// (tonight's back door, the bins, the café) is passed over: a press always shows something or closes. The screen
    /// follows the open entry.
    /// </summary>
    public void More()
    {
        if (!Open || entries.Count == 0) return;
        int next = open;
        do next = next + 1 >= entries.Count ? -1 : next + 1;
        while (next >= 0 && entries[next].facts.Count == 0);
        if (next == open) return;   // nothing written anywhere: nothing to turn to
        open = next;
        // Quieter than the notebook's own sound ("a new line in the notebook", played when a fact is learned): turning a
        // page mustn't sound like learning something.
        Sfx.Play2D("notebook.page", .4f);
        Layout();
    }

    /// <summary>
    /// For the checks: draw from this notebook, record and stage instead of the game's (the lab never touches the save).
    /// A null notebook goes back to the game's.
    /// </summary>
    public void UseForChecks(Notebook notebook, NightLedger ledger, NoteStage stage, string errand)
    {
        testing = notebook != null;
        testBook = notebook;
        testLedger = ledger;
        testStage = stage;
        testErrand = errand ?? "";
        drawnFor = "";
        if (Open) Refresh(true);
    }

    void Update()
    {
        if (page == null || Time.timeScale <= 0f) return;
        // While Ace is choosing a reply, the D-pad moves the highlight (Barks): it mustn't also open the page or turn its notes.
        bool toggle = Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame || PadInput.Pressed(PadButton.DpadUp) && !Barks.Choosing;
        if (toggle) Show(!Open);
        else if (Open && (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame || PadInput.Pressed(PadButton.DpadDown) && !Barks.Choosing))
            More();
        if (Open && Time.unscaledTime >= nextRefresh) Refresh(false);
    }

    // ---------- what's on it ----------

    void Refresh(bool force)
    {
        nextRefresh = Time.unscaledTime + 1f;
        SaveManager saves = SaveManager.Instance;
        Notebook notebook = testing ? testBook : saves != null ? saves.Notebook : null;
        NightLedger ledger = testing ? testLedger : saves != null ? saves.Night : null;
        string errand;
        NoteStage stage;
        if (testing) { stage = testStage; errand = testErrand; }
        else stage = StageNow(ledger, out errand);
        FactsShown = notebook != null ? notebook.Count : 0;
        string state = State(notebook, ledger, stage, errand);
        if (!force && state == drawnFor) return;
        drawnFor = state;
        string keep = OpenKey;
        entries = NoteSubjects.Entries(notebook, ledger, stage, errand);
        open = keep.Length > 0 ? entries.FindIndex(e => e.key == keep) : -1;
        if (first >= entries.Count) first = 0;
        Layout();
    }

    /// <summary>
    /// What the night is for right now, read as NightCycle.NowLine reads it, in its order: the bag to the back door, the bag
    /// to the bins, nothing while his scene plays out, his errand's thing in hand, on Ace's shelf, still to take, or
    /// nothing left (call it a night). None when no night is on.
    /// </summary>
    public static NoteStage StageNow(NightLedger ledger, out string errand)
    {
        errand = "";
        NightWalk walk = NightWalk.Instance;
        NightCycle cycle = NightCycle.Instance;
        if (walk == null || !walk.Active || cycle == null || cycle.Now != NightCycle.Phase.Night) return NoteStage.None;
        NightZero bins = NightZero.Instance;
        NightZero.Step step = bins != null && bins.Now != NightZero.Step.Done ? bins.Now : NightZero.Step.Done;
        bool scene = Barks.ScenePlaying || Lodger.Instance != null && Lodger.Instance.Returning;
        NightCarry carry = NightCarry.Current;
        string held = carry != null && carry.Holding ? carry.HeldId : "";
        if (step == NightZero.Step.AtTheDoor && held == NightZero.BagId) return NoteStage.BagToDoor;
        if (step == NightZero.Step.Outside && held == NightZero.BagId) return NoteStage.BagToBins;
        if (step != NightZero.Step.Done || scene) return NoteStage.None;   // the bag going in, the reveal, his lines
        string wanted = LodgerStory.Errand(ledger);
        if (NightThings.Find(wanted) == null) return NoteStage.Home;   // as the Now line: no thing, nothing to fetch
        errand = wanted;
        if (held == wanted) return NoteStage.InHand;
        if (ledger != null && ledger.OnShelf(wanted)) return NoteStage.OnShelf;
        return NoteStage.ToTake;
    }

    // Everything the page shows depends on, in one string: the page is laid out again only when it changes (once a second at
    // most; nothing is built otherwise). The camera's turn is left out on purpose: orbiting with the page open mustn't
    // rebuild it, or draw a new map at every 15 degrees. A map takes the camera's turn when the page is laid out (opened,
    // pressed, or what it shows changes).
    readonly StringBuilder stateText = new StringBuilder(96);

    string State(Notebook notebook, NightLedger ledger, NoteStage stage, string errand)
    {
        stateText.Clear();
        stateText.Append(stage).Append('|').Append(errand).Append('|').Append(PadInput.UsingPad ? 'p' : 'k');
        if (notebook != null)
        {
            int days = 0;
            foreach (NotebookFactData f in notebook.Facts) if (f != null) days += f.day + 7 * f.surerDay + 13 * f.confirmedDay + (f.sure ?? "").Length;
            stateText.Append('|').Append(notebook.Count).Append('|').Append(days);
        }
        if (ledger != null) stateText.Append('|').Append(ledger.Given.Count).Append('|').Append(ledger.Trophies.Count);
        return stateText.ToString();
    }

    // ---------- laying it out ----------

    // From the top: the first screen when nothing is open; else as it was, unless the open entry (and its notes) wouldn't be
    // whole on the screen, when it moves to the top.
    void Layout()
    {
        if (view == null) return;
        if (open < 0) first = 0;
        else if (open < first) first = open;
        if (!Build(first) && open > first)
        {
            first = open;
            Build(first);
        }
    }

    // Lays the entries out from entry <from> while they fit; false when the open entry didn't fit whole.
    bool Build(int from)
    {
        if (rows != null)
        {
            rows.gameObject.SetActive(false);   // gone from this frame's photos and checks at once
            Destroy(rows.gameObject);
        }
        rows = Box("Rows", view, 0f, 0f, PageWidth, ViewHeight);
        shown.Clear();
        headings.Clear();
        float y = 0f;
        bool openFits = open < 0;
        bool below = false;
        if (entries.Count == 0)
            Words("Nothing yet", rows, "Nothing written down yet.", LineSize, Faint, UiSkin.SemiBold, Pad, 8f, RowWidth, true);
        NoteGroup? group = from > 0 ? entries[from - 1].group : (NoteGroup?)null;
        for (int i = from; i < entries.Count; i++)
        {
            NoteEntry e = entries[i];
            bool heading = e.group != group || i == from;
            float top = y + (heading ? HeadingHeight : 0f);
            // An open entry's notes are laid out for the room it would have at the top of the screen. If that doesn't fit
            // where it sits, it goes (below) and Layout moves it to the top: its notes are cut only when there are more
            // than a whole screen holds (the man at the bins' pages, third on Night 0, show whole).
            Shown s = Row(e, i == open, top, ViewHeight - HeadingHeight, out float height);
            if (top + height > ViewHeight && shown.Count > 0)
            {
                s.row.gameObject.SetActive(false);
                Destroy(s.row.gameObject);
                below = true;
                break;
            }
            if (heading) Heading(e.group, y);
            shown.Add(s);
            if (i == open) openFits = top + height <= ViewHeight + .5f;
            y = top + height + RowGap;
            group = e.group;
        }
        moreAbove.gameObject.SetActive(from > 0);
        moreBelow.gameObject.SetActive(below);
        footer.text = entries.Count > 0
            ? $"{ControlHints.NotebookPage}  Close      {MoreKey}  More"
            : $"{ControlHints.NotebookPage}  Close";
        return openFits;
    }

    /// <summary>The press's key on the device in hand: D-pad down on a pad, Tab on a keyboard.</summary>
    public static string MoreKey => ControlHints.Pad ? PadInput.Label(PadButton.DpadDown) : ControlHints.Key("Tab");

    // The spec's own words for the three groups: tonight's errand, the people, what you've learned (the places and things).
    static readonly string[] HeadingWords = { "Tonight", "People", "What you've learned" };

    void Heading(NoteGroup group, float y)
    {
        TMP_Text words = Words("Heading - " + group, rows, HeadingWords[(int)group], HeadingSize, group == NoteGroup.Tonight ? NotePhotos.Next : Faint,
            UiSkin.SemiBold, Pad + 2f, y + 2f, RowWidth, false);
        Place(words.rectTransform, Pad + 2f, y + 2f, RowWidth, HeadingHeight - 4f);
        words.alignment = TextAlignmentOptions.MidlineLeft;
        headings.Add(words);
    }

    // One entry: an amber bar if it's tonight's; the polaroid (a place: its street map pinned over the photo's lower corner);
    // the name and the line; and, pressed open, every note about it on a lighter panel (as many as there is room for in
    // <room>, then a line saying how many more the phone has).
    Shown Row(NoteEntry e, bool isOpen, float top, float room, out float height)
    {
        RectTransform row = Box("Entry - " + e.key, rows, Pad, top, RowWidth, RowMin);
        var s = new Shown { entry = e, row = row, open = isOpen };
        string picture = e.PictureKey;
        float tilt = NotePhotos.Tilt(picture);
        NotePhotos.Polaroid(row, picture, 14f, 6f, PolaroidWidth, tilt, e.now);
        if (e.subject != null && e.subject.kind == NoteKind.Place)
            NotePhotos.MapCard(row, picture, 14f + PolaroidWidth - 40f, 6f + PolaroidWidth * NotePhotos.PolaroidAspect - 64f, MapWidth, 4f - tilt * 1.5f, e.now);

        s.title = Words("Name", row, "<noparse>" + e.title + "</noparse>", TitleSize, Ink, FontStyles.Bold, TextX, 8f, RowWidth - TextX, true);
        float lineTop = 8f + s.title.rectTransform.sizeDelta.y + 2f;
        s.line = Words("Line", row, e.line, LineSize, Ink, UiSkin.SemiBold, TextX, lineTop, RowWidth - TextX, true);
        height = Mathf.Max(RowMin, lineTop + s.line.rectTransform.sizeDelta.y + 12f);

        if (isOpen && e.facts.Count > 0)
        {
            const float Gap = 8f, Foot = 2f + 6f;   // between notes; under the last (the panel's edge, then the row's)
            float panelTop = height;
            RectTransform panel = Box("Notes", row, 4f, panelTop, RowWidth - 4f, 10f);
            UiSkin.Paint(panel, Panel, 10f);
            float y = 10f;
            float width = RowWidth - 4f - NoteIndent - 12f;
            // The "+N more" line, measured first and put away: under every note with others after it, room is kept for it,
            // so the row never runs past <room> (and so past the screen, once Layout has put it at the top).
            TMP_Text more = Words("More", panel, MoreLine(e.facts.Count), NoteSize, Faint, UiSkin.SemiBold, NoteIndent, y, width, true);
            float moreHeight = more.rectTransform.sizeDelta.y;
            more.gameObject.SetActive(false);
            for (int i = 0; i < e.facts.Count; i++)
            {
                NotebookFactData fact = e.facts[i];
                TMP_Text note = Words("Note", panel, NoteSubjects.FactLine(fact), NoteSize, Ink, UiSkin.SemiBold, NoteIndent, y, width, true);
                float bottom = y + note.rectTransform.sizeDelta.y;
                bool last = i == e.facts.Count - 1;
                // No room for it and the line after it: it goes, and that line says how many more the phone has.
                if (panelTop + bottom + Gap + (last ? 0f : moreHeight + Gap) + Foot > room && s.notes.Count > 0)
                {
                    note.gameObject.SetActive(false);
                    Destroy(note.gameObject);
                    s.cut = e.facts.Count - i;
                    more.text = MoreLine(s.cut);
                    Place(more.rectTransform, NoteIndent, y, width, moreHeight);
                    more.gameObject.SetActive(true);
                    s.notes.Add(more);
                    more = null;
                    y += moreHeight + Gap;
                    break;
                }
                Bullet(panel, fact.source == Notebook.Sources.Inherited, y + NoteSize * .55f);
                s.notes.Add(note);
                y = bottom + Gap;
            }
            if (more != null) Destroy(more.gameObject);   // every note fitted
            panel.sizeDelta = new Vector2(RowWidth - 4f, y + 2f);
            height = panelTop + y + Foot;
        }
        row.sizeDelta = new Vector2(RowWidth, height);
        if (e.now)
        {
            // Tonight's: an amber bar down its left, like the Now line's.
            UiSkin.Paint(Box("Now bar", row, 0f, 4f, 6f, Mathf.Min(height, RowMin) - 8f), NotePhotos.Next, 3f).transform.SetAsFirstSibling();
        }
        return s;
    }

    // What the phone's Notes has that the screen has no room for.
    static string MoreLine(int count) => $"+{count} more on the phone";

    // A note's bullet: a dot in the page's writing, or his square (a page in his hand).
    static void Bullet(RectTransform panel, bool his, float centreY)
    {
        float size = his ? 10f : 9f;
        RectTransform dot = Box(his ? "His page" : "Dot", panel, NoteIndent - 18f, centreY - size * .5f, size, size);
        if (his) UiSkin.Paint(dot, HisInk, 1.5f);
        else
        {
            Image image = dot.gameObject.AddComponent<Image>();
            image.sprite = UiSkin.Circle;
            image.color = NotePhotos.Next;
            image.raycastTarget = false;
        }
    }

    // Three small dots: there is more this way.
    static RectTransform Dots(string name, RectTransform page, float y)
    {
        RectTransform dots = Box(name, page, PageWidth * .5f - 22f, y - 4f, 44f, 8f);
        for (int i = 0; i < 3; i++)
        {
            RectTransform dot = Box("Dot", dots, i * 18f, 0f, 8f, 8f);
            Image image = dot.gameObject.AddComponent<Image>();
            image.sprite = UiSkin.Circle;
            image.color = Faint;
            image.raycastTarget = false;
        }
        dots.gameObject.SetActive(false);
        return dots;
    }

    void OnDestroy() => End();

    // ---------- pieces ----------

    // A box at (x, y) from its parent's top-left corner, y going down.
    static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, x, y, width, height);
        return rect;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Words in the skin's font at (x, y) from the parent's top-left: wrapped to the width and as tall as they need.
    static TMP_Text Words(string name, Transform parent, string text, float size, Color colour, FontStyles style,
                          float x, float y, float width, bool wrap)
    {
        TextMeshProUGUI words = UiSkin.Text(name, parent, size, colour, TextAlignmentOptions.TopLeft, style, wrap);
        words.text = text ?? "";
        float tall = words.GetPreferredValues(words.text, Mathf.Max(1f, width), 10000f).y;
        Place(words.rectTransform, x, y, width, Mathf.Ceil(Mathf.Max(tall, size * 1.2f)));
        return words;
    }

    public string Describe() => page == null ? "Notebook page: not set up." :
        $"Notebook page: {(Open ? "open" : "closed")}, opened {Opened} times; {FactsShown} facts in {entries.Count} entries, " +
        $"{shown.Count} on the screen{(OpenKey.Length > 0 ? ", " + OpenKey + " pressed open" : "")}.";
}
