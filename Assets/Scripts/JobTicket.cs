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

        // Stamp the identity once — it never changes for a job's lifetime.
        if (slotText != null)
        {
            slotText.text = brain.CustomerName;
            slotText.color = brain.JobColor;
        }

        if (background != null)
        {
            // A pale wash of the job colour so the paper itself reads as "the orange one".
            Color c = brain.JobColor;
            background.color = new Color(
                Mathf.Lerp(1f, c.r, 0.25f),
                Mathf.Lerp(1f, c.g, 0.25f),
                Mathf.Lerp(1f, c.b, 0.25f),
                1f);
        }
    }

    private void Update()
    {
        if (Target == null) return;

        if (jobText != null) jobText.text = Target.JobCardText;

        if (patienceFill != null)
        {
            float f = Target.PatienceFraction;
            patienceFill.fillAmount = f;
            patienceFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.75f, 0.35f), f);
        }
    }
}