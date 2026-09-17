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
    [SerializeField] private ConversationController conversation;
    [SerializeField] private bool showDebug = false;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private GameObject recapPanel;
    [SerializeField] private bool displayCafeTime;
    [SerializeField] private TMP_Text viewHintText;
    private CafeViewMode viewMode;

    private void Start()
    {
        viewMode = interactor != null ? interactor.GetComponent<CafeViewMode>() : null;
        // Runtime-only UI: existing scenes need no new inspector wiring.
        DayOneGuideUI guide = GetComponent<DayOneGuideUI>();
        if (guide == null) guide = gameObject.AddComponent<DayOneGuideUI>();
        guide.Initialize(promptText, interactor, inspector, conversation, recapPanel);
    }

    private void Update()
    {
        // The recap owns the screen — hide the in-game HUD behind it.
        bool recapOpen = recapPanel != null && recapPanel.activeSelf;
        if (viewHintText != null)
        {
            viewHintText.gameObject.SetActive(!recapOpen && viewMode != null && viewMode.CanChangeView);
            if (viewMode != null) viewHintText.text = viewMode.ControlsHint;
        }
        if (moneyText != null) moneyText.gameObject.SetActive(!recapOpen);
        if (clockText != null) clockText.gameObject.SetActive(!recapOpen);
        if (stockText != null) stockText.gameObject.SetActive(!recapOpen);
        if (promptText != null) promptText.gameObject.SetActive(!recapOpen);
        if (recapOpen)
        {
            if (crosshair != null) crosshair.SetActive(false);
            return;
        }
        if (clockText != null && DayClock.Instance != null)
        {
            var c = DayClock.Instance;
            if (displayCafeTime)
                clockText.text = $"Day {c.Day}   {FormatHour(c.CurrentHour)}\n<size=65%>"
                    + (c.IsOpen ? $"Closes at {FormatHour(c.ClosingHour)}" : "Closed · finishing service") + "</size>";
            else
            {
                int mins = Mathf.FloorToInt(c.TimeRemaining / 60f);
                int secs = Mathf.FloorToInt(c.TimeRemaining % 60f);
                clockText.text = c.IsOpen ? $"Day {c.Day}   {mins}:{secs:00}" : $"Day {c.Day}   CLOSING";
            }
        }

        if (ShopEconomy.Instance != null)
            moneyText.text = $"${ShopEconomy.Instance.Money}";
        
        if (stockText != null && ShopInventory.Instance != null)
            stockText.text = $"Cups {ShopInventory.Instance.Cups}    Beans {ShopInventory.Instance.Beans}";

        // The conversation panel owns the screen while it's open.
        if (conversation != null && conversation.InConversation)
        {
            promptText.text = "";
            if (crosshair != null) crosshair.SetActive(false);
            return;
        }

        // This view uses a small centre reticle and its own concise action caption.
        // Suppress the shared large E/F line while keeping the aimed controls clear.
        if (interactor.CurrentStation != null
            && interactor.CurrentStation.GetComponent<BeverageStation>() != null)
        {
            if (promptText != null) promptText.text = "";
            if (crosshair != null) crosshair.SetActive(Time.timeScale > 0);
            return;
        }

        var counter = interactor.GetComponent<CounterRepairView>();
        bool showCrosshair = (interactor.IsAtStation || viewMode != null && viewMode.WalkingFirstPerson && !viewMode.PointerReleased)
            && Time.timeScale > 0 && (inspector == null || !inspector.IsHoldingItem)
            && (counter == null || !counter.IsOpen);
        if (crosshair != null) crosshair.SetActive(showCrosshair);
        if (counter != null && counter.IsOpen)
        {
            // The counter caption includes the switch and exit controls.
            promptText.text = "";
            return;
        }

        string line = "";
        string interact = interactor.CurrentPrompt;
        string action = interactor.StationPrompt;

        if (!string.IsNullOrEmpty(interact)) line += $"[E]  {interact}";
        if (!string.IsNullOrEmpty(action))
            line += (line.Length > 0 ? "        " : "") + $"[F]  {action}";

        if (showDebug)
            line += "\n" + interactor.DebugInfo;

        promptText.text = line;
    }

    public static string FormatHour(float hour)
    {
        int totalMinutes = Mathf.FloorToInt(Mathf.Clamp(hour, 0, 24) * 60f + .001f);
        int h = (totalMinutes / 60) % 24;
        int twelveHour = h % 12 == 0 ? 12 : h % 12;
        return $"{twelveHour}:{totalMinutes % 60:00} {(h < 12 ? "AM" : "PM")}";
    }
}
