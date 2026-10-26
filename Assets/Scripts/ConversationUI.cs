using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ---------------------------------------------------------------------------
// THE CONVERSATION ON SCREEN (the dialogue pass, claude/dialogue-skyrim-proposal.md)
//
// Talking feels like Skyrim rather than a novel:
//   * what they say is a subtitle under their face, the whole line at once (no letters typing out);
//   * a speech of several lines (a "\n" between them) plays one line after another, at reading pace,
//     one line on screen at a time (19 Oct 2026, below);
//   * after a short listening beat Ace's replies appear beside them (ConversationController builds
//     them; each is drawn on its own plate, with the keys' hint line in optionsText: the new layout below);
//   * E (SkipReveal) brings the next line up at once, and on the last line ends the beat: the first press never
//     answers, which keeps "dialogue gates the decision".
//
// ONE LINE AT A TIME (19 Oct 2026, playtest 7). His girlfriend is dyslexic: "sometimes when she speaks, it's like double
// text." The earlier lines used to stay on screen dimmed above the new one; now only the line being said is on screen, on
// a dark plate (like the night's speech boxes), 32 px, and E steps to the next line rather than showing them all at once,
// so nothing is skipped unread. And the line waits for the camera: the reviewers saw a request and the replies up while the
// conversation camera was still over the roof, so their line (and so the replies) holds until the brain's blend has landed,
// 0.8 s at the most (HoldForCamera); an E cuts the wait.
// The line and the replies are placed and given a shadow while playing (Look), so the scene is not rebuilt for it.
// The UI skin (playtest 3, session 3): the skin's lettering and its one shadow, the band in the phone's warm ink.
//
// THE NEW LAYOUT (12 Oct 2026). Mansoor's playtest: "i also noticed the dialogue choice option placement to be too far to
// the left, remember it should always be readable for the player." Measured in the labs, the replies sat bottom right in
// small type (27 px) over the busy till, and a 320 x 400 placeholder box filled the bottom left to show one letter; he saw
// the replies bottom left on his screen. Agreed (claude/playtest-4-findings-and-proposals.md §3), and placed from the middle
// of the screen, so no shape of screen can push them into a corner:
//   * their line: centred under their face (the conversation's camera holds the face a little above the middle), 32 px;
//   * Ace's replies: a column just right of their face, from face height down, each on its own soft dark plate, 32 px; the
//     highlighted one in the brand's light green, with ► and its key, and a green edge; the keys' hint line under them;
//   * the placeholder box gives way to a small name card over their line: the name, and how they feel when it isn't plain
//     (the box's letter and mood word, 28 Sept). A real portrait, once there is art for one, sits small beside the name;
//   * everything inside safe margins: 5% of the screen from each edge.
// ---------------------------------------------------------------------------
public class ConversationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image portrait;
    [SerializeField] private Sprite defaultPortrait;      // silhouette for walk-ins
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text optionsText;
    [SerializeField] private float fadeSpeed = 8f;
    [Tooltip("How fast a player reads, in characters a second: how long a line of a longer speech stays " +
             "before the next one comes up (E moves on at once).")]
    [SerializeField] private float readingSpeed = 18f;
    [Tooltip("The pause after their last line before Ace's replies appear, shortest and longest (seconds). " +
             "Longer lines get the longer pause.")]
    [SerializeField] private Vector2 listenBeat = new Vector2(.35f, 1f);

    private bool visible;
    private string speaker = "";
    private string mood = "";
    private bool realPortrait;
    private bool humanLayout;
    private Vector2 savedOptionsSize, savedOptionsPosition, savedDialoguePosition;

    // The speech on screen: its lines, how many are showing, and when the next one comes up.
    private string[] beats = new string[0];
    private int shown;
    private float nextAt = -1f;
    private float listenUntil;

    // The line waits for the conversation camera to land (see the header): at least this long after it was set (the brain
    // begins its blend in its own LateUpdate), at most this long.
    private const float HoldLeast = .15f, HoldMost = .8f;
    private bool holding;
    private float holdSince;
    private Unity.Cinemachine.CinemachineBrain brain;
    private bool brainLooked;

    /// <summary>Their line is waiting for the camera to land (labs).</summary>
    public bool HoldingForCamera => holding;

    /// <summary>
    /// The controller waits on this before offering replies: every line has been shown and the
    /// listening beat is over (or E cut it short).
    /// </summary>
    public bool LineFinished { get; private set; } = true;

    /// <summary>Everything the current speech says, one line per beat, plain text (reports and checks).</summary>
    public string Line { get; private set; } = "";

    /// <summary>How many of its lines are on screen so far.</summary>
    public int LinesShown => beats.Length == 0 ? 0 : shown + 1;

    /// <summary>How many lines the current speech has.</summary>
    public int LineCount => beats.Length;

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        Look();
    }

    private void Update()
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            HideImmediately();
            return;
        }
        TickLine();
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }

    public void Show(string who, Color tint, Sprite face)
    {
        visible = true;
        speaker = who ?? "";
        mood = "";

        if (nameText != null)
            // Their colour, lifted toward white so it reads on the dark band (a theme colour can be deep).
            nameText.color = Color.Lerp(tint, Color.white, .35f);

        SetPortrait(face, PortraitExpression.Neutral);
        RefreshNameCard();

        SetOptions("");
    }

    /// <summary>
    /// How they look now: a real portrait (when there is art for them) sits small beside their name; otherwise the name card
    /// says how they feel when it isn't plain ("Concerned"), as the old box's mood word did.
    /// </summary>
    public void SetPortrait(Sprite face, PortraitExpression expression)
    {
        string feeling = expression switch
        {
            PortraitExpression.Happy => "Pleased",
            PortraitExpression.Worried => "Concerned",
            PortraitExpression.Impatient => "Impatient",
            PortraitExpression.Surprised => "Surprised",
            _ => ""
        };
        if (feeling != mood) { mood = feeling; RefreshNameCard(); }
        if (portrait == null) return;
        Sprite chosen = face != null ? face : defaultPortrait;
        if (portrait.sprite != chosen) portrait.sprite = chosen;
        portrait.preserveAspect = true;
        portrait.color = Color.white;
        bool real = chosen != null;
        if (portrait.enabled != real) portrait.enabled = real;
        if (real != realPortrait) { realPortrait = real; PlaceNameCard(); }
    }

    public void Hide()
    {
        SetHumanLayout(false);
        SetReplies(null, -1, "");
        visible = false;
        nextAt = -1f;
        holding = false;
        LineFinished = true;
        if (DayClock.Instance != null && DayClock.Instance.DayOver && group != null)
            group.alpha = 0f;
    }

    public void HideImmediately()
    {
        Hide();
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        SetOptions("");
    }

    /// <summary>
    /// What they say now. A "\n" separates its lines: the first shows at once, the rest follow at
    /// reading pace (E: all at once), and then the listening beat runs before LineFinished.
    /// </summary>
    public void SetLine(string line)
    {
        beats = Split(line);
        Line = string.Join("\n", beats);
        shown = 0;
        nextAt = -1f;
        if (dialogueText == null) { LineFinished = true; return; }
        if (beats.Length == 0)
        {
            dialogueText.text = "";
            holding = false;
            LineFinished = true;
            PlaceLinePlate();
            return;
        }
        LineFinished = false;
        // The line waits for the camera (the header): nothing drawn until it lands, or HoldMost.
        holding = true;
        holdSince = Time.time;
        dialogueText.text = "";
        PlaceLinePlate();
        PlaceNameCard();
        LayOutReplies();
    }

    // The camera has landed: the brain isn't blending (a brain is looked for once, on the main camera).
    private bool CameraLanded()
    {
        if (!brainLooked)
        {
            brainLooked = true;
            Camera main = Camera.main;
            brain = main != null ? main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
        }
        return brain == null || !brain.IsBlending;
    }

    // Ends the wait for the camera and starts the first line's beat.
    private void EndHold()
    {
        holding = false;
        StartBeat();
    }

    /// <summary>Only a hint line where the replies go (no replies), or "" for nothing there.</summary>
    public void SetOptions(string text) => SetReplies(null, -1, text);

    /// <summary>
    /// Ace's replies, each on its own plate in the column right of their face (<paramref name="rows"/> in rich text, as
    /// ConversationController draws them; null or empty: none), <paramref name="highlighted"/> lit (-1: none), and
    /// <paramref name="footer"/> (the keys' hint line) under them.
    /// </summary>
    public void SetReplies(IReadOnlyList<string> rows, int highlighted, string footer)
    {
        int count = rows != null ? rows.Count : 0;
        footer ??= "";
        bool same = count == shownRows.Count && highlighted == shownLit && footer == shownFooter
            && (optionsText == null || optionsText.text == footer);
        for (int i = 0; same && i < count; i++) same = rows[i] == shownRows[i];
        if (same) return;
        shownRows.Clear();
        for (int i = 0; i < count; i++) shownRows.Add(rows[i] ?? "");
        shownLit = highlighted;
        shownFooter = footer;
        if (optionsText != null && optionsText.text != footer) optionsText.text = footer;
        if (count > 0) EnsureColumn();
        LayOutReplies();
    }

    // Reserve space for three short choices, then restore the authored intake
    // layout exactly. No scene/prefab UI changes or portrait assets required.
    public void SetHumanLayout(bool on)
    {
        if (on == humanLayout || optionsText == null || dialogueText == null) return;
        RectTransform choices = optionsText.rectTransform;
        RectTransform line = dialogueText.rectTransform;
        if (on)
        {
            savedOptionsSize = choices.sizeDelta;
            savedOptionsPosition = choices.anchoredPosition;
            savedDialoguePosition = line.anchoredPosition;
            choices.sizeDelta = new Vector2(savedOptionsSize.x, Mathf.Max(140f, savedOptionsSize.y));
            choices.anchoredPosition = savedOptionsPosition + Vector2.down * 28f;
            line.anchoredPosition = savedDialoguePosition + Vector2.up * 56f;
        }
        else
        {
            choices.sizeDelta = savedOptionsSize;
            choices.anchoredPosition = savedOptionsPosition;
            line.anchoredPosition = savedDialoguePosition;
        }
        humanLayout = on;
    }

    /// <summary>
    /// E while they're talking: the next line at once (one line at a time, so nothing is skipped unread); on the last line
    /// the listening beat is over, so the replies come up. It never answers anything itself.
    /// </summary>
    public void SkipReveal()
    {
        if (LineFinished) return;
        if (holding)
        {
            // A press while the line waits for the camera: the line now; on a one-line speech the beat is over too (the quick
            // second E of playtest 4 still brings the replies up at once).
            EndHold();
            if (beats.Length <= 1) { nextAt = -1f; LineFinished = true; }
            return;
        }
        nextAt = -1f;
        if (beats.Length > 0 && shown < beats.Length - 1)
        {
            shown++;
            Render();
            bool last = shown >= beats.Length - 1;
            if (!last) nextAt = Time.time + ReadTime(beats[shown]);
            else listenUntil = Time.time + ListenBeat(beats[shown]);
            return;
        }
        LineFinished = true;
    }

    // ---------- the speech, line by line ----------

    private void StartBeat()
    {
        Render();
        bool last = shown >= beats.Length - 1;
        if (last)
        {
            nextAt = -1f;
            listenUntil = Time.time + ListenBeat(beats[shown]);
        }
        else nextAt = Time.time + ReadTime(beats[shown]);
    }

    private void TickLine()
    {
        if (LineFinished || beats.Length == 0) return;
        if (holding)
        {
            float waited = Time.time - holdSince;
            if (waited >= HoldMost || waited >= HoldLeast && CameraLanded()) EndHold();
            return;
        }
        if (nextAt >= 0f)
        {
            if (Time.time < nextAt) return;
            shown++;
            StartBeat();
        }
        else if (Time.time >= listenUntil) LineFinished = true;
    }

    // The line being said, alone (19 Oct 2026: one line at a time), on its plate.
    private void Render()
    {
        if (dialogueText == null) return;
        dialogueText.text = shown >= 0 && shown < beats.Length ? beats[shown] : "";
        dialogueText.maxVisibleCharacters = 99999;
        PlaceLinePlate();
        PlaceNameCard();
        LayOutReplies();
    }

    private float ReadTime(string beat) =>
        Mathf.Clamp(.6f + (beat ?? "").Length / Mathf.Max(1f, readingSpeed), 1.4f, 4.5f);

    private float ListenBeat(string beat) =>
        Mathf.Clamp(.25f + (beat ?? "").Length / 100f, Mathf.Min(listenBeat.x, listenBeat.y), Mathf.Max(listenBeat.x, listenBeat.y));

    private static string[] Split(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return new string[0];
        string[] parts = line.Replace("\r", "").Split('\n');
        int count = 0;
        foreach (string part in parts) if (!string.IsNullOrWhiteSpace(part)) count++;
        var result = new string[count];
        count = 0;
        foreach (string part in parts) if (!string.IsNullOrWhiteSpace(part)) result[count++] = part.Trim();
        return result;
    }

    // ---------- the look (placed while playing; the scene keeps its own layout; 12 Oct, see the header) ----------

    // Reference pixels (the HUD canvas is 1920 x 1080 at 16:9; other shapes keep the height near 1080).
    private const float SafeShare = .05f;          // the margin kept clear at every edge, as a share of the screen
    private const float LineBottom = 72f;          // their line's foot, over the bottom edge
    private const float LineWidth = 1100f;
    private const float LineSize = 32f;
    private const float LinePadX = 22f, LinePadY = 10f;   // the plate round their line
    private const float NameSize = 26f;   // 26 Oct 2026 (Readability; her ask: "your text needs to be bolder"): 24 to 26, semibold
    private const float PortraitSize = 88f;
    /// <summary>The replies' left edge, right of the screen's middle (their face), reference pixels. The straight-face meter
    /// takes the same place (StraightFaceUI).</summary>
    public const float ColumnLeft = 160f;
    /// <summary>The first reply's top, over their face's height, reference pixels.</summary>
    public const float ColumnAboveFace = 34f;
    private const float ReplySize = 32f;   // the spec of 19 Oct said 30: kept at 32 (bigger is the direction, and the view probe asserts 32)
    private const float PlatePadX = 20f, PlatePadY = 10f, PlateGap = 8f, PlateMin = 320f, PlateMax = 640f;
    private const float FooterSize = 24f;   // 26 Oct 2026 (Readability): the keys' hint 22 to 24

    /// <summary>Where the conversation's camera holds their face: above the screen's middle, as a share of its height.</summary>
    public float FaceAboveMiddle { get; set; } = .07f;

    // The replies' column and its plates (made the first time there are replies), and the hint line's own plate (the lab's
    // photos, 12 Oct: over a bright window the hint line was faint without one).
    private RectTransform column, footerPlate, linePlate;
    private readonly List<RectTransform> plates = new();
    private readonly List<Image> plateImages = new();
    private readonly List<Image> plateEdges = new();
    private readonly List<TMP_Text> plateTexts = new();
    private readonly List<string> shownRows = new();
    private int shownLit = -1;
    private string shownFooter = "";
    private Vector2 laidOutFor;

    private static readonly Color PlateColour = new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .78f);
    private static readonly Color PlateLit = new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .9f);
    private static readonly Color HintPlateColour = new Color(UiSkin.Ink.r, UiSkin.Ink.g, UiSkin.Ink.b, .62f);

    /// <summary>The replies' plates on the screen, in pixels (labs: never over the face, inside the margins).</summary>
    public List<Rect> ReplyPlatesOnScreen()
    {
        var rects = new List<Rect>();
        for (int i = 0; i < plates.Count; i++)
            if (plates[i] != null && plates[i].gameObject.activeSelf) rects.Add(ScreenRect(plates[i]));
        return rects;
    }

    /// <summary>Their line as drawn, in pixels (labs).</summary>
    public Rect LineOnScreen => TextOnScreen(dialogueText);

    /// <summary>The name card as drawn, in pixels (labs).</summary>
    public Rect NameCardOnScreen => TextOnScreen(nameText);

    /// <summary>The keys' hint line under the replies, in pixels (labs).</summary>
    public Rect FooterOnScreen => TextOnScreen(optionsText);

    /// <summary>What the name card says (labs).</summary>
    public string NameCard => nameText != null ? nameText.text : "";

    /// <summary>The replies' type size and the line's, reference pixels (labs).</summary>
    public Vector2 TypeSizes => new Vector2(ReplySize, dialogueText != null ? dialogueText.fontSize : 0f);

    private void Look()
    {
        UiSkin.UseFont(nameText);
        if (dialogueText != null)
        {
            RectTransform line = dialogueText.rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(.5f, 0f);
            line.pivot = new Vector2(.5f, 0f);
            line.anchoredPosition = new Vector2(0f, LineBottom);
            line.sizeDelta = new Vector2(LineWidth, 250f);
            dialogueText.alignment = TextAlignmentOptions.Bottom;
            UiSkin.UseFont(dialogueText);
            dialogueText.fontSize = LineSize;
            dialogueText.fontStyle = UiSkin.SemiBold;
            dialogueText.color = UiSkin.BandText;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.richText = true;
            UiSkin.Shadowed(dialogueText);
            // The dark plate under their line (19 Oct 2026): a sibling drawn just before the text, sized to it (PlaceLinePlate).
            if (linePlate == null && line.parent != null)
            {
                linePlate = new GameObject("Line plate (while playing)", typeof(RectTransform)).GetComponent<RectTransform>();
                linePlate.SetParent(line.parent, false);
                linePlate.SetSiblingIndex(line.GetSiblingIndex());
                linePlate.anchorMin = linePlate.anchorMax = new Vector2(.5f, 0f);
                linePlate.pivot = new Vector2(.5f, 0f);
                UiSkin.Paint(linePlate, PlateColour, UiSkin.Radius);
                linePlate.gameObject.SetActive(false);
            }
        }
        if (nameText != null)
        {
            RectTransform name = nameText.rectTransform;
            name.anchorMin = name.anchorMax = new Vector2(.5f, 0f);
            name.pivot = new Vector2(.5f, 0f);
            name.sizeDelta = new Vector2(LineWidth, NameSize * 1.5f);
            nameText.alignment = TextAlignmentOptions.Bottom;
            nameText.fontSize = NameSize;
            nameText.fontStyle = UiSkin.SemiBold;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.richText = true;
            nameText.raycastTarget = false;
            UiSkin.Shadowed(nameText);
        }
        if (portrait != null)
        {
            RectTransform face = portrait.rectTransform;
            face.anchorMin = face.anchorMax = new Vector2(.5f, 0f);
            face.pivot = new Vector2(1f, 0f);
            face.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            portrait.raycastTarget = false;
            portrait.enabled = false;   // until there is art for someone (SetPortrait)
        }
        if (optionsText != null)
        {
            // The keys' hint line under the replies (the replies themselves are plates of their own, made when needed).
            RectTransform hint = optionsText.rectTransform;
            hint.anchorMin = hint.anchorMax = new Vector2(.5f, .5f);
            hint.pivot = new Vector2(0f, 1f);
            hint.sizeDelta = new Vector2(PlateMax, FooterSize * 1.6f);
            optionsText.alignment = TextAlignmentOptions.TopLeft;
            optionsText.text = "";   // the scene's text says "New Text" (the lab's photo, 12 Oct: it showed in the morning scene)
            UiSkin.UseFont(optionsText);
            optionsText.fontSize = FooterSize;
            optionsText.fontStyle = UiSkin.SemiBold;
            optionsText.richText = true;   // the buttons are icons (ControlHints)
            optionsText.color = UiSkin.BandText;
            optionsText.lineSpacing = 0f;
            optionsText.textWrappingMode = TextWrappingModes.NoWrap;
            optionsText.raycastTarget = false;
            UiSkin.Shadowed(optionsText);
            if (footerPlate == null && hint.parent != null)
            {
                footerPlate = new GameObject("Hint plate (while playing)", typeof(RectTransform)).GetComponent<RectTransform>();
                footerPlate.SetParent(hint.parent, false);
                footerPlate.SetSiblingIndex(hint.GetSiblingIndex());   // drawn just before the hint, so under it
                footerPlate.anchorMin = footerPlate.anchorMax = new Vector2(.5f, .5f);
                footerPlate.pivot = new Vector2(0f, 1f);
                UiSkin.Paint(footerPlate, HintPlateColour, 10f);
                footerPlate.gameObject.SetActive(false);
            }
        }
        Band();
        PlaceNameCard();
        LayOutReplies();
    }

    private void LateUpdate()
    {
        // A window resized (or the Game view changed shape): lay the column out again for the new screen.
        Vector2 size = PanelSize();
        if (size != laidOutFor) { PlaceNameCard(); LayOutReplies(); }
    }

    private Vector2 PanelSize()
    {
        RectTransform panel = (group != null ? group.transform : transform) as RectTransform;
        return panel != null ? panel.rect.size : new Vector2(1920f, 1080f);
    }

    private void RefreshNameCard()
    {
        if (nameText == null) return;
        string card = string.IsNullOrEmpty(mood) ? speaker
            : $"{speaker}<color={UiSkin.HexOf(UiSkin.BandFaint)}>  ·  {mood}</color>";
        if (nameText.text != card) nameText.text = card;
        PlaceNameCard();
    }

    // The name card sits just over their line's top (the line grows upward as the lines come), with the portrait, if any,
    // to its left.
    private void PlaceNameCard()
    {
        if (nameText == null) return;
        float lineHeight = 0f;
        if (dialogueText != null && !string.IsNullOrEmpty(dialogueText.text))
            lineHeight = dialogueText.GetPreferredValues(dialogueText.text, LineWidth, 0f).y;
        float y = LineBottom + lineHeight + LinePadY + 6f;
        nameText.rectTransform.anchoredPosition = new Vector2(0f, y);
        if (portrait != null && realPortrait)
        {
            float half = nameText.GetPreferredValues(nameText.text, LineWidth, 0f).x * .5f;
            portrait.rectTransform.anchoredPosition = new Vector2(-half - 12f, y - 6f);
        }
    }

    // The plate under their line: the line's drawn size plus padding, at the line's foot; none with no line.
    private void PlaceLinePlate()
    {
        if (linePlate == null || dialogueText == null) return;
        bool any = !string.IsNullOrEmpty(dialogueText.text);
        if (linePlate.gameObject.activeSelf != any) linePlate.gameObject.SetActive(any);
        if (!any) return;
        Vector2 size = dialogueText.GetPreferredValues(dialogueText.text, LineWidth, 0f);
        linePlate.sizeDelta = new Vector2(Mathf.Min(LineWidth, Mathf.Ceil(size.x)) + LinePadX * 2f, Mathf.Ceil(size.y) + LinePadY * 2f);
        linePlate.anchoredPosition = new Vector2(0f, LineBottom - LinePadY);
    }

    private void EnsureColumn()
    {
        if (column != null) return;
        Transform panel = group != null ? group.transform : transform;
        column = new GameObject("Replies (while playing)", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(panel, false);
        column.anchorMin = column.anchorMax = new Vector2(.5f, .5f);
        column.pivot = new Vector2(0f, 1f);
        column.sizeDelta = Vector2.zero;
        if (optionsText != null) column.SetSiblingIndex(optionsText.transform.GetSiblingIndex());
    }

    private void EnsurePlates(int count)
    {
        while (plates.Count < count)
        {
            var plate = new GameObject("Reply " + (plates.Count + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            plate.SetParent(column, false);
            plate.anchorMin = plate.anchorMax = new Vector2(0f, 1f);
            plate.pivot = new Vector2(0f, 1f);
            Image image = UiSkin.Paint(plate, PlateColour, UiSkin.Radius);
            var edge = new GameObject("Lit edge", typeof(RectTransform)).GetComponent<RectTransform>();
            edge.SetParent(plate, false);
            edge.anchorMin = new Vector2(0f, 0f);
            edge.anchorMax = new Vector2(0f, 1f);
            edge.pivot = new Vector2(0f, .5f);
            edge.offsetMin = new Vector2(0f, 6f);
            edge.offsetMax = new Vector2(5f, -6f);
            Image edgeImage = UiSkin.Paint(edge, UiSkin.BrandBright, 2.5f);
            TextMeshProUGUI text = UiSkin.Text("Words", plate, ReplySize, UiSkin.BandText, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, true);
            RectTransform words = text.rectTransform;
            words.anchorMin = Vector2.zero;
            words.anchorMax = Vector2.one;
            words.offsetMin = new Vector2(PlatePadX, PlatePadY);
            words.offsetMax = new Vector2(-PlatePadX, -PlatePadY);
            plates.Add(plate);
            plateImages.Add(image);
            plateEdges.Add(edgeImage);
            plateTexts.Add(text);
        }
    }

    /// <summary>
    /// Labs: the replies' plates as they would stand on a screen of <paramref name="size"/> (canvas units: at 16:9 1920 x
    /// 1080; a 4:3 screen is about 1663 x 1247), each box from that screen's bottom-left corner. They are laid out for that
    /// screen, measured, and laid out again for the real one.
    /// </summary>
    public List<Rect> PlatesIfTheScreenWere(Vector2 size)
    {
        var boxes = new List<Rect>();
        LayOutReplies(size);
        if (column != null)
            for (int i = 0; i < plates.Count && i < shownRows.Count; i++)
            {
                Vector2 topLeft = size * .5f + column.anchoredPosition + plates[i].anchoredPosition;
                Vector2 box = plates[i].sizeDelta;
                boxes.Add(new Rect(topLeft.x, topLeft.y - box.y, box.x, box.y));
            }
        LayOutReplies();
        return boxes;
    }

    private void LayOutReplies() => LayOutReplies(PanelSize());

    // The column: just right of the screen's middle (their face), its top a little over the face's height, the plates one
    // width (the widest reply's), the hint line under them; kept inside the margins and clear of their line.
    private void LayOutReplies(Vector2 size)
    {
        laidOutFor = PanelSize();
        int count = shownRows.Count;
        if (column == null)
        {
            if (optionsText != null) PlaceFooter(size, ColumnTop(size), 0f, PlateMin);
            return;
        }
        EnsurePlates(count);
        float safeX = size.x * SafeShare, safeY = size.y * SafeShare;
        float widest = Mathf.Max(PlateMin, Mathf.Min(PlateMax, size.x * .5f - ColumnLeft - safeX));
        float inner = widest - PlatePadX * 2f;
        float want = 0f;
        for (int i = 0; i < count; i++)
            want = Mathf.Max(want, plateTexts[i].GetPreferredValues(shownRows[i], inner, 0f).x);
        float width = Mathf.Clamp(Mathf.Ceil(want) + PlatePadX * 2f + 2f, PlateMin, widest);

        var heights = new float[count];
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            heights[i] = Mathf.Ceil(plateTexts[i].GetPreferredValues(shownRows[i], width - PlatePadX * 2f, 0f).y) + PlatePadY * 2f;
            total += heights[i] + (i > 0 ? PlateGap : 0f);
        }
        float footer = optionsText != null && !string.IsNullOrEmpty(shownFooter) ? FooterSize * 1.6f + PlateGap : 0f;

        // From a little over their face's height down; up again if it would reach their line, never above the top margin.
        float top = ColumnTop(size);
        float lineTop = -size.y * .5f + LineBottom + LineHeightNow() + LinePadY + NameSize * 1.5f + 18f;
        float bottom = top - total - footer;
        if (bottom < lineTop) top += lineTop - bottom;
        top = Mathf.Min(top, size.y * .5f - safeY);
        column.anchoredPosition = new Vector2(ColumnLeft, top);

        float y = 0f;
        for (int i = 0; i < plates.Count; i++)
        {
            bool on = i < count;
            if (plates[i].gameObject.activeSelf != on) plates[i].gameObject.SetActive(on);
            if (!on) continue;
            bool lit = i == shownLit;
            plates[i].anchoredPosition = new Vector2(0f, -y);
            plates[i].sizeDelta = new Vector2(width, heights[i]);
            plateImages[i].color = lit ? PlateLit : PlateColour;
            plateEdges[i].enabled = lit;
            if (plateTexts[i].text != shownRows[i]) plateTexts[i].text = shownRows[i];
            y += heights[i] + PlateGap;
        }
        if (optionsText != null) PlaceFooter(size, top, y, width);
    }

    private float ColumnTop(Vector2 size) => size.y * FaceAboveMiddle + ColumnAboveFace;

    // The keys' hint line under the replies, on its own lighter plate, lined up with the replies' plates.
    private void PlaceFooter(Vector2 size, float top, float below, float width)
    {
        RectTransform hint = optionsText.rectTransform;
        hint.anchoredPosition = new Vector2(ColumnLeft + 12f, top - below - 5f);
        hint.sizeDelta = new Vector2(Mathf.Max(width, PlateMin), FooterSize * 1.6f);
        if (footerPlate == null) return;
        bool any = !string.IsNullOrEmpty(shownFooter);
        if (footerPlate.gameObject.activeSelf != any) footerPlate.gameObject.SetActive(any);
        if (!any) return;
        float words = optionsText.GetPreferredValues(shownFooter, 4000f, 0f).x;
        footerPlate.anchoredPosition = new Vector2(ColumnLeft, top - below);
        footerPlate.sizeDelta = new Vector2(Mathf.Ceil(words) + 24f, FooterSize * 1.6f + 6f);
    }

    private float LineHeightNow() =>
        dialogueText != null && !string.IsNullOrEmpty(dialogueText.text)
            ? dialogueText.GetPreferredValues(dialogueText.text, LineWidth, 0f).y : 0f;

    private static Rect ScreenRect(RectTransform rect)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);   // a screen-space overlay canvas: world is screen pixels
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    // A text's drawn extent (not its box), in pixels.
    private static Rect TextOnScreen(TMP_Text text)
    {
        if (text == null || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) return default;
        text.ForceMeshUpdate();
        Bounds b = text.textBounds;
        Vector3 a = text.transform.TransformPoint(b.min), c = text.transform.TransformPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    // A dark band along the bottom of the screen, behind their line (it fades with the panel).
    private void Band()
    {
        Transform panel = group != null ? group.transform : transform;
        if (panel.Find("Subtitle band") != null) return;
        var rect = new GameObject("Subtitle band", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 380f);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = false;
        const int height = 64;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color ink = UiSkin.Ink;
        for (int y = 0; y < height; y++)
        {
            float up = y / (height - 1f);
            texture.SetPixel(0, y, new Color(ink.r, ink.g, ink.b, .66f * Mathf.Pow(1f - up, 1.6f)));
        }
        texture.Apply();
        image.sprite = Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(.5f, 0f));
        image.type = Image.Type.Simple;
        image.color = Color.white;
    }
}
