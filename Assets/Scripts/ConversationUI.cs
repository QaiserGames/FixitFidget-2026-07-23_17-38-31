using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConversationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image portrait;
    [SerializeField] private Sprite defaultPortrait;      // silhouette for walk-ins
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text optionsText;
    [SerializeField] private float fadeSpeed = 8f;
    [Tooltip("Characters per second. ~45 reads as a quick sweep, not a stutter.")]
    [SerializeField] private float charactersPerSecond = 45f;

    private bool visible;
    private Coroutine revealRoutine;
    private TextMeshProUGUI fallbackFace;
    private string portraitInitial = "?";
    private Color portraitTint = Color.gray;

    // The controller waits on this before offering choices.
    public bool LineFinished { get; private set; } = true;

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            HideImmediately();
            return;
        }
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
        visible = false;
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
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

    public void SetLine(string line)
    {
        if (dialogueText == null) return;

        if (revealRoutine != null) StopCoroutine(revealRoutine);

        dialogueText.text = line;
        LineFinished = string.IsNullOrEmpty(line);

        if (!LineFinished) revealRoutine = StartCoroutine(Reveal());
    }

    public void SetOptions(string text)
    {
        if (optionsText != null) optionsText.text = text;
    }

    // Dump the whole line at once — for readers faster than the reveal.
    public void SkipReveal()
    {
        if (LineFinished) return;

        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }

        dialogueText.ForceMeshUpdate();
        dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
        LineFinished = true;
    }

    // Character-by-character, fast — left-to-right sweep rather than a stutter.
    private IEnumerator Reveal()
    {
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;

        float perChar = 1f / Mathf.Max(charactersPerSecond, 1f);
        float carry = 0f;

        for (int i = 1; i <= total; i++)
        {
            dialogueText.maxVisibleCharacters = i;

            // Batch characters when they're faster than one frame.
            carry += perChar;
            if (carry >= Time.deltaTime)
            {
                yield return new WaitForSeconds(carry);
                carry = 0f;
            }
        }

        dialogueText.maxVisibleCharacters = total;
        LineFinished = true;
        revealRoutine = null;
    }
}
