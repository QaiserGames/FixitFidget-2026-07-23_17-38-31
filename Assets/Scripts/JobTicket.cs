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

        // Subject is the device OR the drink; Detail is the fault OR "to make".
        if (jobText != null && brain.Record != null)
            jobText.text = $"{brain.Record.Subject}\n{brain.Record.Detail}";

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
        if (Target == null || patienceFill == null) return;

        float f = Target.PatienceFraction;
        patienceFill.fillAmount = f;
        patienceFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f),
                                        new Color(0.3f, 0.75f, 0.35f), f);
    }
}