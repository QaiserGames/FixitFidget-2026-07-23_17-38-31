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
             "it out. Made while playing (the UI skin's one shared shadow): nothing is saved.")]
    [SerializeField] private bool promptShadow = true;
    private CafeViewMode viewMode;
    private HudCorners corners;

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

    // ONE UI SKIN AND THE HUD'S CORNERS (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3)
    // By day the corners (HudCorners) carry what the money, the clock and the stock lines used to: today's takings as a cash
    // stack (top right), the hanging sign with the day's bar and the time (bottom left), and a chip only when stock runs
    // low (bottom right). Those three scene texts stay in the scene and are hidden by day; at night the clock line comes
    // back in the top right corner with the night's hour (the corners are a café's, and the café is shut). The prompt,
    // the controls line and the night's clock are dressed in the skin while playing (its font, its one shadow, its
    // colours); the controls line moves from the bottom left (the sign's corner now) to the bottom middle, under the
    // prompt, and steps aside while a close-up shows its own controls. Nothing in the scene is saved.
    private void Start()
    {
        if (moneyText != null) moneyColour = moneyText.color;
        viewMode = interactor != null ? interactor.GetComponent<CafeViewMode>() : null;
        // Runtime-only UI: existing scenes need no new inspector wiring.
        DayOneGuideUI guide = GetComponent<DayOneGuideUI>();
        if (guide == null) guide = gameObject.AddComponent<DayOneGuideUI>();
        guide.Initialize(promptText, interactor, inspector, conversation, recapPanel);
        Skin();
        // The corners need the day's clock; a scene without one keeps the old lines.
        if (DayClock.Instance != null || FindAnyObjectByType<DayClock>() != null)
        {
            corners = FindAnyObjectByType<HudCorners>();
            if (corners == null) corners = gameObject.AddComponent<HudCorners>();
        }
        // The phone by day: Esc or Start pauses (PausePhone).
        if (FindAnyObjectByType<PausePhone>() == null) gameObject.AddComponent<PausePhone>();
    }

    // The skin on the scene's own texts (while playing; the scene keeps its layout on disk).
    private void Skin()
    {
        if (promptText != null)
        {
            UiSkin.UseFont(promptText);
            promptText.color = UiSkin.BandText;
            if (promptShadow) UiSkin.Shadowed(promptText);
        }
        if (clockText != null)
        {
            // The night's clock: the top right corner, the stack's place by day.
            UiSkin.UseFont(clockText);
            RectTransform r = clockText.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-UiSkin.Margin, -18f);
            r.sizeDelta = new Vector2(560f, 90f);
            clockText.alignment = TextAlignmentOptions.TopRight;
            clockText.fontStyle = FontStyles.Bold;
            clockText.color = UiSkin.BandText;
            UiSkin.Shadowed(clockText);
        }
        foreach (TMP_Text t in new[] { moneyText, stockText })
            if (t != null) { UiSkin.UseFont(t); UiSkin.Shadowed(t); }
        if (viewHintText != null)
        {
            UiSkin.UseFont(viewHintText);
            RectTransform r = viewHintText.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, 0f);
            r.pivot = new Vector2(.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 20f);
            r.sizeDelta = new Vector2(1240f, 36f);
            viewHintText.alignment = TextAlignmentOptions.Bottom;
            viewHintText.fontSize = 19f;
            viewHintText.color = new Color(UiSkin.BandText.r, UiSkin.BandText.g, UiSkin.BandText.b, .86f);
            viewHintText.textWrappingMode = TextWrappingModes.NoWrap;
            UiSkin.Shadowed(viewHintText);
        }
    }

    // The keys in a line in the brand's light green ("[E]  Take order"): the words stay the prompt's colour.
    private static readonly string KeyColour = UiSkin.HexOf(UiSkin.BrandBright);
    private static string Key(string label) => $"<color={KeyColour}>[{label}]</color>";

    private void Update()
    {
        // The recap owns the screen — hide the in-game HUD behind it (the recap phone, or the old panel).
        bool recapOpen = RecapUI.Showing || recapPanel != null && recapPanel.activeSelf;
        // A night walk: the café is closed. The night's hour instead of the day's
        // clock, no money or stock, and only the night's own prompts (the Night 1 slice).
        NightWalk night = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance : null;
        // The corners carry the money, the day's clock and the stock by day.
        bool cornersByDay = corners != null && night == null;
        if (viewHintText != null)
        {
            // In a close-up (a conversation, the drinks, an item at the bench, the counter phone) the close-up's own
            // controls are on screen: the view's line steps aside.
            bool closeUp = interactor != null && (interactor.InCloseUp || interactor.UsingDrinks);
            viewHintText.gameObject.SetActive(!recapOpen && !closeUp && viewMode != null && viewMode.CanChangeView);
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
        if (moneyText != null) moneyText.gameObject.SetActive(!recapOpen && night == null && !cornersByDay);
        if (clockText != null) clockText.gameObject.SetActive(!recapOpen && !cornersByDay);
        if (stockText != null) stockText.gameObject.SetActive(!recapOpen && night == null && !cornersByDay);
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
                    clockText.text = $"{NightLabel()}   {FormatHour(night.ClockHour)}";
                }
            }
            string nightPrompt = interactor != null ? interactor.CurrentPrompt : "";
            if (promptText != null && (nightPrompt != shownNightPrompt || shownPromptLine == null))
            {
                shownNightPrompt = nightPrompt; shownInteract = null;
                shownPromptLine = string.IsNullOrEmpty(nightPrompt) ? "" : $"{Key(ControlHints.Interact)}  {nightPrompt}";
                promptText.text = shownPromptLine;
                PopPrompt(shownPromptLine);
            }
            if (crosshair != null)
                crosshair.SetActive(viewMode != null && viewMode.WalkingFirstPerson && !viewMode.PointerReleased && Time.timeScale > 0);
            return;
        }
        if (clockText != null && DayClock.Instance != null && !cornersByDay)
        {
            var c = DayClock.Instance;
            if (displayCafeTime)
            {
                int minute = Mathf.FloorToInt(Mathf.Clamp(c.CurrentHour, 0, 24) * 60f + .001f);
                int closing = Mathf.FloorToInt(Mathf.Clamp(c.ClosingHour, 0, 24) * 60f + .001f);
                if (c.Day != shownDay || minute != shownMinute || closing != shownClosingMinute || c.IsOpen != shownOpen || !shownCafeTime || shownNightClock)
                {
                    shownDay = c.Day; shownMinute = minute; shownClosingMinute = closing; shownOpen = c.IsOpen; shownCafeTime = true; shownNightClock = false;
                    clockText.text = $"{Weekdays.Label(c.Day)}   {FormatHour(c.CurrentHour)}\n<size=65%>"
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
                    clockText.text = c.IsOpen ? $"{Weekdays.Label(c.Day)}   {mins}:{secs:00}" : $"{Weekdays.Label(c.Day)}   CLOSING";
                }
            }
        }
        else if (cornersByDay) shownNightClock = false;

        if (ShopEconomy.Instance != null && !cornersByDay) ShowMoney(ShopEconomy.Instance.Money);

        if (stockText != null && ShopInventory.Instance != null && !cornersByDay
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

        // The dispenser (its close-up from above, or its controls under the crosshair in first person) has a small
        // centre reticle and its own concise action caption (BeverageStation). Suppress the shared large prompt line
        // while keeping the aimed controls clear.
        if (interactor.UsingDrinks)
        {
            ClearPrompt();
            if (crosshair != null)
                crosshair.SetActive(Time.timeScale > 0 && (interactor.IsAtStation
                    || viewMode != null && viewMode.WalkingFirstPerson && !viewMode.PointerReleased));
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
        // The second verb, on a click or RT (a device on the bench in first person: "Work on it"). There is no station
        // to step up to by day any more (playtest 3), so F has no line.
        string action = interactor.WorkPrompt;
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
            // Keys follow the device in use: [E] / [Click] on a keyboard and mouse, the pad's
            // own labels once a controller is being used (see ControlHints).
            if (!string.IsNullOrEmpty(interact)) line += $"{Key(ControlHints.Interact)}  {interact}";
            if (!string.IsNullOrEmpty(action))
                line += (line.Length > 0 ? "        " : "") + $"{Key(ControlHints.Use)}  {action}";
            // Working on an item with a controller: say how to change tools and put things down.
            if (holding)
                line += $"\n<size=80%>{Key(ControlHints.Tools)}  {toolName}        {Key(ControlHints.Use)}  Use        {Key(ControlHints.Back)}  "
                    + (!hand ? "Put tool down" : "Put item down") + "</size>";

            if (showDebug)
                line += "\n" + interactor.DebugInfo;

            if (line != shownPromptLine) PopPrompt(line);
            shownPromptLine = line;
            promptText.text = line;
        }
    }

    // JUICE (6 Oct 2026, Mansoor's playtest): the money counts up (or down) to what's in the till with a bounce and a flash
    // of colour, instead of jumping; a new prompt pops in. Only the number's text is built again, and only when it changes.
    // (With the HUD's corners up, today's takings are the cash stack's, and this line is hidden: HudCorners.)
    private const float MoneyCountSeconds = .5f, PromptPopSeconds = .2f;
    private static readonly Color MoneyGain = UiSkin.BrandBright, MoneySpend = new Color(1f, .5f, .45f, 1f);
    private Color moneyColour = Color.white, moneyFlash;
    private int moneyTarget, moneyFrom;
    private float moneySince = -1f, promptSince = -1f;

    private void ShowMoney(int money)
    {
        if (moneyText == null) return;
        if (shownMoney == int.MinValue)
        {
            // The first look (a day loaded, a save restored): as it is, no counting.
            shownMoney = moneyTarget = money;
            moneyText.text = $"${money}";
            return;
        }
        if (money != moneyTarget)
        {
            moneyFrom = shownMoney;
            moneyTarget = money;
            moneySince = UiClock.Now;
            moneyFlash = money > moneyFrom ? MoneyGain : MoneySpend;
        }
        if (moneySince < 0f) return;
        float t = Mathf.Clamp01((UiClock.Now - moneySince) / MoneyCountSeconds);
        int now = Mathf.RoundToInt(Mathf.Lerp(moneyFrom, moneyTarget, 1f - (1f - t) * (1f - t)));
        if (now != shownMoney)
        {
            shownMoney = now;
            moneyText.text = $"${now}";
        }
        float punch = t < .2f ? Mathf.Lerp(1f, 1.24f, t / .2f) : Mathf.Lerp(1.24f, 1f, (t - .2f) / .8f);
        moneyText.rectTransform.localScale = new Vector3(punch, punch, 1f);
        moneyText.color = Color.Lerp(moneyFlash, moneyColour, t * t);
        if (t >= 1f)
        {
            moneySince = -1f;
            moneyText.rectTransform.localScale = Vector3.one;
            moneyText.color = moneyColour;
        }
    }

    private void PopPrompt(string line)
    {
        if (!string.IsNullOrEmpty(line)) promptSince = UiClock.Now;
    }

    // After Update's early returns: the prompt's pop plays out whatever the HUD is showing.
    private void LateUpdate()
    {
        if (promptText == null || promptSince < 0f) return;
        float t = Mathf.Clamp01((UiClock.Now - promptSince) / PromptPopSeconds);
        // From a touch small, past full size and back (a spring).
        float u = t - 1f;
        float s = Mathf.LerpUnclamped(.9f, 1f, 1f + 2.4f * u * u * u + 1.4f * u * u);
        promptText.rectTransform.localScale = new Vector3(s, s, 1f);
        if (t >= 1f)
        {
            promptSince = -1f;
            promptText.rectTransform.localScale = Vector3.one;
        }
    }

    // An empty prompt, and the line's memory forgotten, so the next words are built again.
    private void ClearPrompt()
    {
        if (promptText != null && shownPromptLine != "") promptText.text = "";
        shownPromptLine = "";
        shownInteract = null; shownNightPrompt = null;
    }

    // "Thursday night" (the night after Thursday's day: Day 1 is a Monday), or "Night" before the first day. Built when the
    // night's minute changes, as the rest of the clock line is.
    static string NightLabel()
    {
        string name = Weekdays.Name(DayClock.Instance != null ? DayClock.Instance.Day : 0);
        return name.Length > 0 ? name + " night" : "Night";
    }

    public static string FormatHour(float hour)
    {
        int totalMinutes = Mathf.FloorToInt(Mathf.Clamp(hour, 0, 24) * 60f + .001f);
        int h = (totalMinutes / 60) % 24;
        int twelveHour = h % 12 == 0 ? 12 : h % 12;
        return $"{twelveHour}:{totalMinutes % 60:00} {(h < 12 ? "AM" : "PM")}";
    }
}
