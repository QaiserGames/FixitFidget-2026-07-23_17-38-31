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
    [SerializeField] private float wordsPerSecond = 3f;

    private bool visible;
    private Coroutine revealRoutine;

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
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }

    public void Show(string who, Color tint, Sprite face)
    {
        visible = true;

        if (nameText != null)
        {
            nameText.text = who;
            nameText.color = tint;
        }

        if (portrait != null)
            portrait.sprite = face != null ? face : defaultPortrait;

        if (optionsText != null) optionsText.text = "";
    }

    public void Hide()
    {
        visible = false;
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        LineFinished = true;
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
        dialogueText.maxVisibleWords = dialogueText.textInfo.wordCount;
        LineFinished = true;
    }

    private IEnumerator Reveal()
    {
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.wordCount;
        dialogueText.maxVisibleWords = 0;

        for (int i = 1; i <= total; i++)
        {
            dialogueText.maxVisibleWords = i;
            yield return new WaitForSeconds(1f / Mathf.Max(wordsPerSecond, 0.5f));
        }

        LineFinished = true;
        revealRoutine = null;
    }
}