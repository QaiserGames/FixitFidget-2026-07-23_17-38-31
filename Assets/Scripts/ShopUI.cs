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
    [Tooltip("A soft dark shadow behind the prompt's letters, so a bright lamp or window behind it can't wash " +
             "it out. Made while playing, on the prompt's own copy of its material: nothing is saved.")]
    [SerializeField] private bool promptShadow = true;
    private CafeViewMode viewMode;

    // WHY THE HUD REMEMBERS WHAT IT LAST SHOWED (30 Sept 2026)
    // Every frame used to build every HUD string afresh ($"Day {day}   {hour}", the prompt line, the
    // hint), about 0.7 KB of garbage a frame at 240 fps: a collection every few seconds, and each one a
    // hitch. TextMesh Pro ignores a string equal to the one it has, but the building itself is the
    // waste. So each line is built only when what it says has changed (the minute, the money, the
    // stock, the prompt's words), and otherwise nothing is allocated.
    private int shownDay = int.MinValue, shownMinute = int.MinValue, shownClosingMinute = int.MinValue, shownMoney = int.MinValue,
                shownCups = int.MinValue, shownBeans = int.MinValue, shownNightMinute = int.MinValue;
    private bool shownOpen, shownCafeTime, shownNightClock;
    private string shownHint, shownHintBase, shownNightPrompt, shownPromptLine, shownInteract, shownAction, shownToolName;
    private bool shownHintNight, shownPromptPad, shownPromptHolding, shownPromptDebug, shownPromptHand;

    private void Start()
    {
        viewMode = interactor != null ? interactor.GetComponent<CafeViewMode>() : null;
        // Runtime-only UI: existing scenes need no new inspector wiring.
        DayOneGuideUI guide = GetComponent<DayOneGuideUI>();
        if (guide == null) guide = gameObject.AddComponent<DayOneGuideUI>();
        guide.Initialize(promptText, interactor, inspector, conversation, recapPanel);
        if (promptShadow) ShadowBehind(promptText);
    }

    // TextMesh Pro's underlay: a soft, dark copy of the letters behind them. It goes on the prompt's
    // own material copy (fontMaterial), so no other text changes and no asset is written. Player
    // builds keep the shader's underlay variant because TMP's "Drop Shadow" preset, in a Resources
    // folder, uses it.
    private static void ShadowBehind(TMP_Text text)
    {
        if (text == null) return;
        Material material = text.fontMaterial;
        if (material == null || !material.HasProperty(UnderlayColor)) return;
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor(UnderlayColor, new Color(0f, 0f, 0f, .8f));
        material.SetFloat(UnderlayOffsetX, 0f);
        material.SetFloat(UnderlayOffsetY, -.3f);
        material.SetFloat(UnderlayDilate, .5f);
        material.SetFloat(UnderlaySoftness, .6f);
        text.UpdateMeshPadding();
    }

    private static readonly int UnderlayColor = Shader.PropertyToID("_UnderlayColor"),
        UnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX"),
        UnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY"),
        UnderlayDilate = Shader.PropertyToID("_UnderlayDilate"),
        UnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");

    private void Update()
    {
        // The recap owns the screen — hide the in-game HUD behind it (the recap phone, or the old panel).
        bool recapOpen = RecapUI.Showing || recapPanel != null && recapPanel.activeSelf;
        // A night walk: the café is closed. The night's hour instead of the day's
        // clock, no money or stock, and only the night's own prompts (the Night 1 slice).
        NightWalk night = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance : null;
        if (viewHintText != null)
        {
            viewHintText.gameObject.SetActive(!recapOpen && viewMode != null && viewMode.CanChangeView);
            // At night the torch and the notebook join the view's own controls.
            if (viewMode != null)
            {
                string hintBase = viewMode.ControlsHint;   // built by the view only when its state changes
                bool atNight = night != null;
                if (!ReferenceEquals(hintBase, shownHintBase) && hintBase != shownHintBase || atNight != shownHintNight)
                {
                    shownHintBase = hintBase; shownHintNight = atNight;
                    shownHint = atNight
                        ? hintBase + $"    {ControlHints.Sneak}  Sneak    {ControlHints.Torch}  Torch    {ControlHints.NotebookPage}  Notebook"
                        : hintBase;
                    viewHintText.text = shownHint;
                }
            }
        }
        if (moneyText != null) moneyText.gameObject.SetActive(!recapOpen && night == null);
        if (clockText != null) clockText.gameObject.SetActive(!recapOpen);
        if (stockText != null) stockText.gameObject.SetActive(!recapOpen && night == null);
        if (promptText != null) promptText.gameObject.SetActive(!recapOpen);
        if (recapOpen)
        {
            if (crosshair != null) crosshair.SetActive(false);
            return;
        }
        if (night != null)
        {
            // The night's own clock (it moves: 11 PM to about 4 AM), shown as it reads.
            if (clockText != null)
            {
                int minute = Mathf.FloorToInt(Mathf.Clamp(night.ClockHour, 0, 24) * 60f + .001f);
                if (minute != shownNightMinute || !shownNightClock)
                {
                    shownNightMinute = minute; shownNightClock = true; shownMinute = int.MinValue;
                    clockText.text = $"Night   {FormatHour(night.ClockHour)}";
                }
            }
            string nightPrompt = interactor != null ? interactor.CurrentPrompt : "";
            if (promptText != null && (nightPrompt != shownNightPrompt || shownPromptLine == null))
            {
                shownNightPrompt = nightPrompt; shownInteract = null;
                shownPromptLine = string.IsNullOrEmpty(nightPrompt) ? "" : $"[{ControlHints.Interact}]  {nightPrompt}";
                promptText.text = shownPromptLine;
            }
            if (crosshair != null)
                crosshair.SetActive(viewMode != null && viewMode.WalkingFirstPerson && !viewMode.PointerReleased && Time.timeScale > 0);
            return;
        }
        if (clockText != null && DayClock.Instance != null)
        {
            var c = DayClock.Instance;
            if (displayCafeTime)
            {
                int minute = Mathf.FloorToInt(Mathf.Clamp(c.CurrentHour, 0, 24) * 60f + .001f);
                int closing = Mathf.FloorToInt(Mathf.Clamp(c.ClosingHour, 0, 24) * 60f + .001f);
                if (c.Day != shownDay || minute != shownMinute || closing != shownClosingMinute || c.IsOpen != shownOpen || !shownCafeTime || shownNightClock)
                {
                    shownDay = c.Day; shownMinute = minute; shownClosingMinute = closing; shownOpen = c.IsOpen; shownCafeTime = true; shownNightClock = false;
                    clockText.text = $"Day {c.Day}   {FormatHour(c.CurrentHour)}\n<size=65%>"
                        + (c.IsOpen ? $"Closes at {FormatHour(c.ClosingHour)}" : "Closed · finishing service") + "</size>";
                }
            }
            else
            {
                int second = Mathf.FloorToInt(c.TimeRemaining);
                if (c.Day != shownDay || second != shownMinute || c.IsOpen != shownOpen || shownCafeTime || shownNightClock)
                {
                    shownDay = c.Day; shownMinute = second; shownOpen = c.IsOpen; shownCafeTime = false; shownNightClock = false;
                    int mins = Mathf.FloorToInt(c.TimeRemaining / 60f);
                    int secs = Mathf.FloorToInt(c.TimeRemaining % 60f);
                    clockText.text = c.IsOpen ? $"Day {c.Day}   {mins}:{secs:00}" : $"Day {c.Day}   CLOSING";
                }
            }
        }

        if (ShopEconomy.Instance != null && ShopEconomy.Instance.Money != shownMoney)
        {
            shownMoney = ShopEconomy.Instance.Money;
            moneyText.text = $"${shownMoney}";
        }

        if (stockText != null && ShopInventory.Instance != null
            && (ShopInventory.Instance.Cups != shownCups || ShopInventory.Instance.Beans != shownBeans))
        {
            shownCups = ShopInventory.Instance.Cups; shownBeans = ShopInventory.Instance.Beans;
            stockText.text = $"Cups {shownCups}    Beans {shownBeans}";
        }

        // The conversation panel owns the screen while it's open.
        if (conversation != null && conversation.InConversation)
        {
            ClearPrompt();
            if (crosshair != null) crosshair.SetActive(false);
            return;
        }

        // This view uses a small centre reticle and its own concise action caption.
        // Suppress the shared large E/F line while keeping the aimed controls clear.
        if (interactor.CurrentStation != null
            && interactor.CurrentStation.GetComponent<BeverageStation>() != null)
        {
            ClearPrompt();
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
            ClearPrompt();
            return;
        }

        string interact = interactor.CurrentPrompt;
        string action = interactor.StationPrompt;
        bool pad = PadInput.UsingPad;
        bool holding = pad && inspector != null && inspector.IsHoldingItem;
        string toolName = holding ? inspector.CurrentToolName : null;
        bool hand = holding && inspector.CurrentTool == ToolType.Hand;
        // Only when a word of it changes is the line built again (the debug line changes every frame).
        if (showDebug || shownPromptLine == null || interact != shownInteract || action != shownAction || pad != shownPromptPad
            || holding != shownPromptHolding || toolName != shownToolName || hand != shownPromptHand || shownPromptDebug)
        {
            shownInteract = interact; shownAction = action; shownPromptPad = pad; shownPromptHolding = holding;
            shownToolName = toolName; shownPromptHand = hand; shownPromptDebug = showDebug; shownNightPrompt = null;
            string line = "";
            // Keys follow the device in use: [E] / [F] on a keyboard, the pad's
            // own labels once a controller is being used (see ControlHints).
            if (!string.IsNullOrEmpty(interact)) line += $"[{ControlHints.Interact}]  {interact}";
            if (!string.IsNullOrEmpty(action))
                line += (line.Length > 0 ? "        " : "") + $"[{ControlHints.Station}]  {action}";
            // Working on an item with a controller: say how to change tools and put things down.
            if (holding)
                line += $"\n[{ControlHints.Tools}]  {toolName}        [{ControlHints.Use}]  Use        [{ControlHints.Back}]  "
                    + (!hand ? "Put tool down" : "Put item down");

            if (showDebug)
                line += "\n" + interactor.DebugInfo;

            shownPromptLine = line;
            promptText.text = line;
        }
    }

    // An empty prompt, and the line's memory forgotten, so the next words are built again.
    private void ClearPrompt()
    {
        if (promptText != null && shownPromptLine != "") promptText.text = "";
        shownPromptLine = "";
        shownInteract = null; shownNightPrompt = null;
    }

    public static string FormatHour(float hour)
    {
        int totalMinutes = Mathf.FloorToInt(Mathf.Clamp(hour, 0, 24) * 60f + .001f);
        int h = (totalMinutes / 60) % 24;
        int twelveHour = h % 12 == 0 ? 12 : h % 12;
        return $"{twelveHour}:{totalMinutes % 60:00} {(h < 12 ? "AM" : "PM")}";
    }
}
