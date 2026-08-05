using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class JobTicket : MonoBehaviour
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text jobText;
    [SerializeField] private Image patienceFill;

    public CustomerBrain Target { get; private set; }

    public void Bind(CustomerBrain brain)
    {
        Target = brain;
    }

    private void Update()
    {
        if (Target == null) return;

        slotText.text = $"#{Target.SlotIndex + 1}";
        jobText.text = Target.JobCardText;

        float f = Target.PatienceFraction;
        patienceFill.fillAmount = f;
        patienceFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.75f, 0.35f), f);
    }
}