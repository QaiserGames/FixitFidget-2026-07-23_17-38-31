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
    private float cardWidth = 220, cardHeight = 118;

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
                Place(jobText.rectTransform, new Vector2(8, -27), new Vector2(cardWidth - 16, 40));
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
                Place(jobText.rectTransform, new Vector2(8, -27), new Vector2(cardWidth - 16, cardHeight - 42));
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
            Place(slotText.rectTransform, new Vector2(8, -2), new Vector2(cardWidth - 16, 25));
            slotText.enableAutoSizing = false; slotText.fontSize = 21;
            slotText.overflowMode = TextOverflowModes.Ellipsis;
            slotText.textWrappingMode = TextWrappingModes.NoWrap;
            slotText.alignment = TextAlignmentOptions.TopLeft;
        }
        if (jobText != null)
        {
            jobText.enableAutoSizing = false; jobText.fontSize = 17;
            jobText.overflowMode = TextOverflowModes.Ellipsis;
            jobText.alignment = TextAlignmentOptions.TopLeft;
        }
    }
    public void SetCardSize(float width, float height)
    {
        cardWidth = width; cardHeight = height;
        ConfigureLayout();
        if (jobText != null)
            Place(jobText.rectTransform, new Vector2(8, -27), new Vector2(width - 16,
                callLine != null && callLine.gameObject.activeSelf ? 40 : height - 42));
        if (patienceFill != null)
        {
            RectTransform track = patienceFill.transform.parent as RectTransform;
            Place(track != null && track != transform ? track : patienceFill.rectTransform,
                new Vector2(8, -height + 10), new Vector2(width - 16, 6));
        }
        if (callLine != null)
        {
            Place(callLine, new Vector2(8, -68), new Vector2(width - 16, 36));
            Place(callState.rectTransform, new Vector2(34, 0), new Vector2(width - 54, 18));
            Place(callSeconds.rectTransform, new Vector2(34, -18), new Vector2(width - 54, 18));
        }
    }
    private void BuildCallLine()
    {
        callLine = RepairOverlayUI.Panel("Support call obligation", transform, new Vector2(8, -68),
            new Vector2(cardWidth - 16, 36), RepairOverlayUI.Background).rectTransform;
        var rect = RepairOverlayUI.Rect("Phone", callLine, new Vector2(0, 1), new Vector2(.5f, .5f),
            new Vector2(17, -18), new Vector2(30, 30));
        callIcon = rect.gameObject.AddComponent<SupportCallIcon>(); callIcon.raycastTarget = false;
        callState = RepairOverlayUI.Text("Call state", callLine, new Vector2(34, 0), new Vector2(cardWidth - 54, 18), 16, Color.white);
        callSeconds = RepairOverlayUI.Text("Countdown", callLine, new Vector2(34, -18), new Vector2(cardWidth - 54, 18), 16, RepairOverlayUI.Muted);
        callState.textWrappingMode = callSeconds.textWrappingMode = TextWrappingModes.NoWrap;
        if (jobText != null) { callState.font = jobText.font; callSeconds.font = jobText.font; }
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
