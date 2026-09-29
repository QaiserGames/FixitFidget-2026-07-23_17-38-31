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
//     the earlier ones staying on screen dimmed, so what they asked for is still there when Ace decides;
//   * after a short listening beat Ace's replies appear on the right (ConversationController builds
//     them; they're drawn in optionsText);
//   * E (SkipReveal) shows everything they're saying at once and ends the beat: the first press never
//     answers, which keeps "dialogue gates the decision".
// The placeholder portrait box and the name above it stay as they were (Mansoor, 28 Sept). The line and
// the replies are placed and given a shadow while playing (Look), so the scene is not rebuilt for it.
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
    private TextMeshProUGUI fallbackFace;
    private string portraitInitial = "?";
    private Color portraitTint = Color.gray;
    private bool humanLayout;
    private Vector2 savedOptionsSize, savedOptionsPosition, savedDialoguePosition;

    // The speech on screen: its lines, how many are showing, and when the next one comes up.
    private string[] beats = new string[0];
    private int shown;
    private float nextAt = -1f;
    private float listenUntil;

    // Dimmed earlier lines, and at most this many lines on screen at once.
    private const string EarlierLine = "<size=86%><color=#B9B3A8>";
    private const int MostLinesOnScreen = 3;

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
        portraitInitial = !string.IsNullOrWhiteSpace(who) ? who.Trim().Substring(0, 1).ToUpperInvariant() : "?";
        portraitTint = Color.Lerp(Color.black, tint, 0.35f);

        if (nameText != null)
        {
            nameText.text = who;
            nameText.color = tint;
        }

        SetPortrait(face, PortraitExpression.Neutral);

        if (optionsText != null) optionsText.text = "";
    }

    public void SetPortrait(Sprite face, PortraitExpression expression)
    {
        if (portrait == null) return;
        Sprite chosen = face != null ? face : defaultPortrait;
        if (portrait.sprite != chosen) portrait.sprite = chosen;
        portrait.preserveAspect = true;
        portrait.color = chosen != null ? Color.white : portraitTint;

        // Missing art still gives the player a readable identity card. Replace
        // it simply by assigning portrait sprites; no scene rebuilding needed.
        if (chosen == null && fallbackFace == null)
        {
            var host = new GameObject("Portrait identity", typeof(RectTransform), typeof(TextMeshProUGUI));
            host.transform.SetParent(portrait.transform, false);
            fallbackFace = host.GetComponent<TextMeshProUGUI>();
            fallbackFace.font = nameText != null ? nameText.font : TMP_Settings.defaultFontAsset;
            fallbackFace.fontSize = 30f;
            fallbackFace.enableAutoSizing = true;
            fallbackFace.fontSizeMin = 12f;
            fallbackFace.fontSizeMax = 30f;
            fallbackFace.alignment = TextAlignmentOptions.Center;
            fallbackFace.color = Color.white;
            fallbackFace.raycastTarget = false;
            RectTransform rect = fallbackFace.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
        }
        if (fallbackFace == null) return;
        fallbackFace.gameObject.SetActive(chosen == null);
        if (chosen == null)
        {
            string mood = expression switch
            {
                PortraitExpression.Happy => "Pleased",
                PortraitExpression.Worried => "Concerned",
                PortraitExpression.Impatient => "Impatient",
                PortraitExpression.Surprised => "Surprised",
                _ => ""
            };
            string label = string.IsNullOrEmpty(mood) ? portraitInitial : portraitInitial + "\n" + mood;
            if (fallbackFace.text != label) fallbackFace.text = label;
        }
    }

    public void Hide()
    {
        SetHumanLayout(false);
        visible = false;
        nextAt = -1f;
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
            LineFinished = true;
            return;
        }
        LineFinished = false;
        StartBeat();
    }

    public void SetOptions(string text)
    {
        if (optionsText != null && optionsText.text != text) optionsText.text = text;
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
    /// E while they're talking: everything they're saying at once (the earlier lines dimmed), and the
    /// listening beat is over, so the replies come up. It never answers anything itself.
    /// </summary>
    public void SkipReveal()
    {
        if (LineFinished) return;
        nextAt = -1f;
        if (beats.Length > 0)
        {
            shown = beats.Length - 1;
            Render();
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
        if (nextAt >= 0f)
        {
            if (Time.time < nextAt) return;
            shown++;
            StartBeat();
        }
        else if (Time.time >= listenUntil) LineFinished = true;
    }

    // The lines so far: the latest in full, up to two before it dimmed and a little smaller.
    private void Render()
    {
        if (dialogueText == null) return;
        var text = new StringBuilder();
        int first = Mathf.Max(0, shown - (MostLinesOnScreen - 1));
        for (int i = first; i <= shown && i < beats.Length; i++)
        {
            if (i < shown) text.Append(EarlierLine).Append(beats[i]).Append("</color></size>\n");
            else text.Append(beats[i]);
        }
        dialogueText.text = text.ToString();
        dialogueText.maxVisibleCharacters = 99999;
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

    // ---------- the look (placed while playing; the scene keeps its own layout) ----------

    // Reference pixels (the HUD canvas is 1920 x 1080): the portrait box sits bottom left (x 40-360), so
    // the subtitle runs from x 390 to 1250 and the replies from 1304 to 1864, both sitting on the same
    // bottom line, over a soft dark band so they read against the bright windows.
    private void Look()
    {
        if (dialogueText != null)
        {
            RectTransform line = dialogueText.rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(.5f, 0f);
            line.pivot = new Vector2(.5f, 0f);
            line.anchoredPosition = new Vector2(-140f, 44f);
            line.sizeDelta = new Vector2(860f, 250f);
            dialogueText.alignment = TextAlignmentOptions.Bottom;
            dialogueText.fontSize = 30f;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            Shadow(dialogueText);
        }
        if (optionsText != null)
        {
            RectTransform replies = optionsText.rectTransform;
            replies.anchorMin = replies.anchorMax = new Vector2(1f, 0f);
            replies.pivot = new Vector2(1f, 0f);
            replies.anchoredPosition = new Vector2(-56f, 44f);
            replies.sizeDelta = new Vector2(560f, 330f);
            optionsText.alignment = TextAlignmentOptions.BottomLeft;
            optionsText.fontSize = 27f;
            optionsText.color = Color.white;
            optionsText.lineSpacing = 14f;
            optionsText.textWrappingMode = TextWrappingModes.Normal;
            Shadow(optionsText);
        }
        Band();
    }

    // A soft drop shadow (TextMesh Pro's underlay) on a copy of the font's material, for these two only.
    private static void Shadow(TMP_Text text)
    {
        Material shared = text.fontSharedMaterial;
        if (shared == null || !shared.HasProperty(ShaderUtilities.ID_UnderlayColor)) return;
        var material = new Material(shared) { name = shared.name + " (conversation shadow)" };
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, .85f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .45f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.45f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, .25f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .35f);
        text.fontSharedMaterial = material;
    }

    // A dark band along the bottom of the screen, behind everything in the panel (it fades with it).
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
        rect.sizeDelta = new Vector2(0f, 430f);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = false;
        const int height = 64;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < height; y++)
        {
            float up = y / (height - 1f);
            texture.SetPixel(0, y, new Color(0f, 0f, 0f, .62f * Mathf.Pow(1f - up, 1.6f)));
        }
        texture.Apply();
        image.sprite = Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(.5f, 0f));
        image.type = Image.Type.Simple;
        image.color = Color.white;
    }
}
