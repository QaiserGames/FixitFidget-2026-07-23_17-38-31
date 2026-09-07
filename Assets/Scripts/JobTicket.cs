using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class JobTicket : MonoBehaviour
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text jobText;
    [SerializeField] private Image patienceFill;
    [SerializeField] private Image background;
    private RectTransform callLine;
    private SupportCallIcon callIcon;
    private TMP_Text callState, callSeconds;
    private bool wasSupport;

    public CustomerBrain Target { get; private set; }

    public void Bind(CustomerBrain brain)
    {
        Target = brain;
        wasSupport = false;
        ConfigureLayout();

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

        var call = Target.ActiveJob as HoldCallJob;
        if (call != null)
        {
            wasSupport = true;
            if (callLine == null) BuildCallLine();
            callLine.gameObject.SetActive(true);
            if (jobText != null)
            {
                Place(jobText.rectTransform, new Vector2(10, -34), new Vector2(200, 46));
                jobText.textWrappingMode = TextWrappingModes.NoWrap;
                jobText.text = Target.Record != null ? Target.Record.deviceName : "Support phone";
                if (Target.HasDrinkOrder && Target.WantedDrink != null)
                    jobText.text += "\n+ " + Target.WantedDrink.drinkName;
            }
            var phase = call.CurrentPhase;
            callIcon.Show(phase, Time.time);
            callState.text = phase switch
            {
                HoldCallRun.State.NeedsDialing => call.MissedCalls > 0 ? "Missed call" : "Call support",
                HoldCallRun.State.Connecting => "Connecting",
                HoldCallRun.State.OnHold => "On hold",
                HoldCallRun.State.Ringing => "Answer now",
                _ => "Resolved"
            };
            callSeconds.text = phase == HoldCallRun.State.Ringing || phase == HoldCallRun.State.OnHold
                ? Mathf.CeilToInt(call.SecondsRemaining) + "s"
                : phase == HoldCallRun.State.Done ? "Return phone"
                : phase == HoldCallRun.State.NeedsDialing ? (call.MissedCalls > 0 ? "Redial" : "Dial phone") : "…";
            callState.color = phase == HoldCallRun.State.Ringing ? new Color(.24f, 1f, .46f) : Color.white;
        }
        else
        {
            if (callLine != null) callLine.gameObject.SetActive(false);
            if (jobText != null)
            {
                Place(jobText.rectTransform, new Vector2(10, -36), new Vector2(200, 108));
                jobText.textWrappingMode = TextWrappingModes.Normal;
                // Delivery must remove the call obligation, even if a drink remains.
                jobText.text = wasSupport && Target.ActiveJob == null
                    ? Target.HasDrinkOrder && Target.WantedDrink != null ? "+ " + Target.WantedDrink.drinkName : ""
                    : Target.TabLines;
            }
        }

        if (patienceFill == null) return;

        float f = Target.PatienceFraction;
        patienceFill.fillAmount = f;
        patienceFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f),
                                        new Color(0.3f, 0.75f, 0.35f), f);
    }

    private void ConfigureLayout()
    {
        if (slotText != null)
        {
            Place(slotText.rectTransform, new Vector2(10, -3), new Vector2(200, 30));
            slotText.enableAutoSizing = false; slotText.fontSize = 24;
            slotText.overflowMode = TextOverflowModes.Ellipsis;
            slotText.textWrappingMode = TextWrappingModes.NoWrap;
            slotText.alignment = TextAlignmentOptions.TopLeft;
        }
        if (jobText != null)
        {
            jobText.enableAutoSizing = false; jobText.fontSize = 20;
            jobText.overflowMode = TextOverflowModes.Ellipsis;
            jobText.alignment = TextAlignmentOptions.TopLeft;
        }
    }
    private void BuildCallLine()
    {
        callLine = RepairOverlayUI.Panel("Support call obligation", transform, new Vector2(10, -84),
            new Vector2(200, 60), RepairOverlayUI.Background).rectTransform;
        var rect = RepairOverlayUI.Rect("Phone", callLine, new Vector2(0, 1), new Vector2(.5f, .5f),
            new Vector2(25, -31), new Vector2(44, 44));
        callIcon = rect.gameObject.AddComponent<SupportCallIcon>(); callIcon.raycastTarget = false;
        callState = RepairOverlayUI.Text("Call state", callLine, new Vector2(52, -1), new Vector2(144, 27), 24, Color.white);
        callSeconds = RepairOverlayUI.Text("Countdown", callLine, new Vector2(52, -30), new Vector2(144, 27), 24, RepairOverlayUI.Muted);
        callState.textWrappingMode = callSeconds.textWrappingMode = TextWrappingModes.NoWrap;
        if (jobText != null) { callState.font = jobText.font; callSeconds.font = jobText.font; }
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
