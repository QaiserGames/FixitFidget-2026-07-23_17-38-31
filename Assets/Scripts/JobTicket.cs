using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class JobTicket : MonoBehaviour
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text jobText;
    [SerializeField] private Image patienceFill;
    [SerializeField] private Image background;

    public CustomerBrain Target { get; private set; }

    public void Bind(CustomerBrain brain)
    {
        Target = brain;

        if (slotText != null)
        {
            slotText.text = brain.CustomerName;
            slotText.color = brain.JobColor;
        }

        if (background != null)
        {
            Color c = brain.JobColor;
            background.color = new Color(
                Mathf.Lerp(1f, c.r, 0.25f),
                Mathf.Lerp(1f, c.g, 0.25f),
                Mathf.Lerp(1f, c.b, 0.25f), 1f);
        }
    }

    private void Update()
    {
        if (Target == null) return;

        // THE TEXT MOVED OUT OF Bind().
        //
        // Bind runs once, when the ticket is created. A repair customer who
        // asks for a coffee five seconds after sitting down would never have
        // shown it — the card would have been describing a tab that had since
        // changed. One customer, one card, refreshed live.
        //
        // Deliberately plain: no icons, no animation, no fixed width. This is
        // here so the loop can be seen working end to end. The real rail is a
        // UI pass of its own — see claude/hud-spec.md.
        if (jobText != null) jobText.text = Target.TabLines;

        if (patienceFill == null) return;

        float f = Target.PatienceFraction;
        patienceFill.fillAmount = f;
        patienceFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f),
                                        new Color(0.3f, 0.75f, 0.35f), f);
    }
}