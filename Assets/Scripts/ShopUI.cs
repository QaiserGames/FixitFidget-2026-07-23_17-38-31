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
    [SerializeField] private bool showDebug = false;

    private void Update()
    {
        if (clockText != null && DayClock.Instance != null)
        {
            var c = DayClock.Instance;
            int mins = Mathf.FloorToInt(c.TimeRemaining / 60f);
            int secs = Mathf.FloorToInt(c.TimeRemaining % 60f);
            clockText.text = c.IsOpen ? $"Day {c.Day}   {mins}:{secs:00}" : $"Day {c.Day}   CLOSING";
        }

        // Crosshair shows whenever we're CHOOSING — hidden while manipulating an item.
        bool showCrosshair = interactor.IsAtStation && (inspector == null || !inspector.IsHoldingItem);
        if (crosshair != null) crosshair.SetActive(showCrosshair);

        if (ShopEconomy.Instance != null)
            moneyText.text = $"${ShopEconomy.Instance.Money}";

        // E does things to objects; F enters and leaves stations.
        string line = "";
        string interact = interactor.CurrentPrompt;
        string action = interactor.StationPrompt;

        if (!string.IsNullOrEmpty(interact)) line += $"[E]  {interact}";
        if (!string.IsNullOrEmpty(action))
            line += (line.Length > 0 ? "        " : "") + $"[F]  {action}";

        HoldCallJob activeCall = FindAnyObjectByType<HoldCallJob>();
        if (activeCall != null && activeCall.CurrentPhase != HoldCallJob.Phase.Done)
            line = activeCall.StatusLine + "\n" + line;

        if (showDebug)
            line += "\n" + interactor.DebugInfo;

        promptText.text = line;
    }
}