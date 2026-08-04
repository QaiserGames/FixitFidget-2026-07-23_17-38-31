using UnityEngine;
using TMPro;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private ItemInspector inspector;
    [SerializeField] private bool showDebug = true;

    private void Update()
    {
        // Day and time remaining.
        if (clockText != null && DayClock.Instance != null)
        {
            var c = DayClock.Instance;
            int mins = Mathf.FloorToInt(c.TimeRemaining / 60f);
            int secs = Mathf.FloorToInt(c.TimeRemaining % 60f);
            clockText.text = c.IsOpen ? $"Day {c.Day}   {mins}:{secs:00}" : $"Day {c.Day}   CLOSING";
        }

        // Crosshair only at a station, and only when not inspecting an item.
        bool showCrosshair = interactor.IsAtStation && (inspector == null || !inspector.IsHoldingItem);
        if (crosshair != null) crosshair.SetActive(showCrosshair);

        if (ShopEconomy.Instance != null)
            moneyText.text = $"${ShopEconomy.Instance.Money}";

        string prompt = interactor.CurrentPrompt;
        string line = string.IsNullOrEmpty(prompt) ? "" : $"[E]  {prompt}";

        if (interactor.IsAtStation)
            line += (line.Length > 0 ? "        " : "") + "[Esc]  Step back";

        // An active hold call takes over the top of the prompt area.
        HoldCallJob activeCall = FindFirstObjectByType<HoldCallJob>();
        if (activeCall != null && activeCall.CurrentPhase != HoldCallJob.Phase.Done)
            line = activeCall.StatusLine + "\n" + line;

        if (showDebug)
            line += "\n" + interactor.DebugInfo;

        promptText.text = line;
    }
}