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
    private bool shownHintNight, shownPromptPad, shownPromptHolding, shownPromptDebug, shownPromptHand, shownHintPad;

    /// <summary>The controls strip at the bottom middle, as it reads now (labs).</summary>
    public string ViewHint => shownHint ?? "";
    /// <summary>The prompt line, as it reads now (labs).</summary>
    public string PromptLine => shownPromptLine ?? "";

    // ONE UI SKIN AND THE HUD'S CORNERS (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3)
    // By day the corners (HudCorners) carry what the money, the clock and the stock lines used to: today's takings as a cash
    // stack (top right), the hanging sign with the day's bar and the time (bottom left), and a chip only when stock runs
    // low (bottom right). Those three scene texts stay in the scene and are hidden by day; at night the clock line comes
    // back in the top right corner with the night's hour (the corners are a café's, and the café is shut). The prompt,
    // the controls line and the night's clock are dressed in the skin while playing (its font, its one shadow, its
    // colours); the controls line moves from the bottom left (the sign's corner now) to the bottom middle, under the
    // prompt, and steps aside while a close-up shows its own controls. Nothing in the scene is saved.
    //
    // 19 OCT 2026 (playtest 7, his girlfriend: dyslexic, on a PlayStation pad). Every button in the prompt line and the strip
    // is its icon (ControlHints; no "[ ]" round it), the strip is three hints on a dark plate at 22 px (StripWords), and the
    // prompt line's rich text carries the icon tags.
    private void Start()
    {
        if (moneyText != null) moneyColour = moneyText.color;
        viewMode = interactor != null ? interactor.GetComponent<CafeViewMode>() : null;
        // Runtime-only UI: existing scenes need no new inspector wiring.
        DayOneGuideUI guide = GetComponent<DayOneGuideUI>();
        if (guide == null) guide = gameObject.AddComponent<DayOneGuideUI>();
        guide.Initialize(promptText, interactor, inspector, conversation, recapPanel);
        // The names, the patience bars and Day 1's pins over the café (playtest 4): made now, so the first frame has them.
        UiPins.Ensure();
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

    // The prompt line's place, as the scene has it; at the bench it drops to the bottom edge (below) so it doesn't sit across the mat.
    private Vector2 promptHome;
    private bool promptHomeKnown;
    private float promptDrop;
    const float BenchPromptDrop = 78f;
    private PlayerCarry carry;
    private string shownInHand;

    // The skin on the scene's own texts (while playing; the scene keeps its layout on disk).
    private void Skin()
    {
        if (promptText != null)
        {
            UiSkin.UseFont(promptText);
            promptText.color = UiSkin.BandText;
            if (promptShadow) UiSkin.Shadowed(promptText);
            promptHome = promptText.rectTransform.anchoredPosition;
            promptHomeKnown = true;
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
            // The strip: 22 px semibold on a dark rounded plate (19 Oct; see StripWords). The plate is a sibling drawn just
            // under the text, sized to its words each time they change (Strip).
            UiSkin.UseFont(viewHintText);
            RectTransform r = viewHintText.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, 0f);
            r.pivot = new Vector2(.5f, 0f);
            r.anchoredPosition = new Vector2(0f, StripBottom);
            r.sizeDelta = new Vector2(1240f, StripHeight);
            viewHintText.alignment = TextAlignmentOptions.Center;
            viewHintText.fontSize = StripSize;
            viewHintText.fontStyle = UiSkin.SemiBold;
            viewHintText.color = UiSkin.BandText;
            viewHintText.textWrappingMode = TextWrappingModes.NoWrap;
            viewHintText.richText = true;
            if (stripPlate == null && r.parent != null)
            {
                stripPlate = new GameObject("Controls plate (while playing)", typeof(RectTransform)).GetComponent<RectTransform>();
                stripPlate.SetParent(r.parent, false);
                stripPlate.SetSiblingIndex(r.GetSiblingIndex());   // just before the text: under it
                stripPlate.anchorMin = stripPlate.anchorMax = new Vector2(.5f, 0f);
                stripPlate.pivot = new Vector2(.5f, 0f);
                stripPlate.anchoredPosition = new Vector2(0f, StripBottom - StripPadY);
                UiSkin.Paint(stripPlate, UiSkin.Band, UiSkin.Radius);
                stripPlate.gameObject.SetActive(false);
            }
        }
    }

    // The strip's size and place (reference pixels): 22 px, 16 px of plate either side of the words, 8 above and below.
    const float StripSize = 22f, StripHeight = 30f, StripBottom = 20f, StripPadX = 18f, StripPadY = 7f;
    private RectTransform stripPlate;

    // Lays the plate under the strip's words (after the words changed).
    private void Strip(string words)
    {
        if (viewHintText == null) return;
        viewHintText.text = words;
        if (stripPlate == null) return;
        bool any = !string.IsNullOrEmpty(words);
        if (!any) { if (stripPlate.gameObject.activeSelf) stripPlate.gameObject.SetActive(false); return; }
        float width = viewHintText.GetPreferredValues(words, 4000f, 0f).x;
        stripPlate.sizeDelta = new Vector2(Mathf.Ceil(width) + StripPadX * 2f, StripHeight + StripPadY * 2f);
    }

    // The key in a line is its icon now (19 Oct 2026: ControlHints), icon + two spaces + verb; the brackets and the green went
    // with the words (an icon needs neither). The word is still the prompt's colour.
    private static string Key(string label) => label;

    // AT THE BENCH (playtest 4; 12 Oct): the close-up's controls, the mouse's and the pad's, are on their own card (BenchCards).

    // THE CONTROLS STRIP (19 Oct 2026, playtest 7). Her: "your text needs to be bolder"; the reviewers: 6-7 hints, 14-16 words,
    // in one thin 19 px line with no plate, over a moving world; "the controls are unreadable" at the counter. Now at most
    // three hints for the state Ace is in, each an icon and a word, 22 px on a dark rounded plate (Strip):
    //   overhead       Move, Use, Pause
    //   first person   Move, Use, and the way back to the overhead view
    //   at night       the notebook first (her: "you should give me a tip to do that"), then the torch, then sneak
    // The view's own line (CafeViewMode.ControlsHint: orbit, zoom...) is no longer shown: those are discoverable, and six
    // hints were a wall for a dyslexic reader. (Day 1's "walk and use first" is the whole strip now, every day.)
    private static string StripWords(bool atNight, bool firstPerson)
    {
        if (atNight)
            return Hint(ControlHints.NotebookPage, "Notebook") + Hint(ControlHints.Torch, "Torch") + Hint(ControlHints.Sneak, "Sneak", last: true);
        return Hint(ControlHints.Walk, "Move") + Hint(ControlHints.Interact, "Use")
             + (firstPerson ? Hint(ControlHints.View, "Overhead view", last: true) : Hint(ControlHints.Pause, "Pause", last: true));
    }

    private static string Hint(string icon, string verb, bool last = false) => icon + "  " + verb + (last ? "" : "      ");

    private void Update()
    {
        // The recap owns the screen — hide the in-game HUD behind it (the recap phone, or the old panel).
        // The day intro (13 Oct 2026, Cinematics) hides the strip too, as the recap does.
        bool recapOpen = RecapUI.Showing || recapPanel != null && recapPanel.activeSelf || UiPins.DayIntroPlaying;
        // A night walk: the café is closed. The night's hour instead of the day's
        // clock, no money or stock, and only the night's own prompts (the Night 1 slice).
        NightWalk night = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance : null;
        // The corners carry the money, the day's clock and the stock by day.
        bool cornersByDay = corners != null && night == null;
        if (viewHintText != null)
        {
            // In a close-up (a conversation, the drinks, an item at the bench, the counter phone) the close-up's own
            // controls are on screen: the strip steps aside, plate and all.
            bool closeUp = interactor != null && (interactor.InCloseUp || interactor.UsingDrinks);
            bool stripOn = !recapOpen && !closeUp && viewMode != null && viewMode.CanChangeView;
            viewHintText.gameObject.SetActive(stripOn);
            if (stripPlate != null && stripPlate.gameObject.activeSelf != (stripOn && !string.IsNullOrEmpty(shownHint)))
                stripPlate.gameObject.SetActive(stripOn && !string.IsNullOrEmpty(shownHint));
            // Three hints for the state Ace is in (StripWords); built again only when the state or the device changes.
            if (viewMode != null)
            {
                bool atNight = night != null;
                bool firstPerson = viewMode.FirstPersonSelected;
                bool padNow = PadInput.UsingPad;
                string hintBase = firstPerson ? "first person" : "overhead";   // the state, for the memory below
                if (hintBase != shownHintBase || atNight != shownHintNight || padNow != shownHintPad)
                {
                    shownHintBase = hintBase; shownHintNight = atNight; shownHintPad = padNow;
                    shownHint = StripWords(atNight, firstPerson);
                    Strip(shownHint);
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

        // AT THE BENCH, NO PROMPT LINE (checklist v2, 14 Oct 2026; his playtest 5: "the checklist is terrible"). Its 36 px
        // "[E] Pick up item and step back" was the largest words on the first repair's screen, and its "Clean: lens, film
        // path ..." said the job a third time. The bench's card carries the way out once the job is fixed ("[E] Take it to
        // Grace"), and the controls card or "[Tab] Help" the rest (BenchCards).
        if (inspector != null && inspector.IsHoldingItem)
        {
            ClearPrompt();
            return;
        }

        string interact = interactor.CurrentPrompt;
        // The second verb, on a click or RT (a device on the bench in first person: "Work on it"). There is no station
        // to step up to by day any more (playtest 3), so F has no line.
        string action = interactor.WorkPrompt;
        bool pad = PadInput.UsingPad;
        // At the bench: the tool in hand (it still keys the line below; the controls themselves are on BenchCards' card).
        bool holding = inspector != null && inspector.IsHoldingItem;
        string toolName = holding ? inspector.CurrentToolName : null;
        bool hand = holding && inspector.CurrentTool == ToolType.Hand;
        // What's in hand, named, over the prompt (8 Oct: from above you couldn't tell you'd picked a phone up).
        if (carry == null) carry = interactor.GetComponent<PlayerCarry>();
        string inHand = carry != null && (inspector == null || !inspector.IsHoldingItem) ? carry.Summary : "";
        // Only when a word of it changes is the line built again (the debug line changes every frame).
        if (showDebug || shownPromptLine == null || interact != shownInteract || action != shownAction || pad != shownPromptPad
            || holding != shownPromptHolding || toolName != shownToolName || hand != shownPromptHand || inHand != shownInHand || shownPromptDebug)
        {
            shownInteract = interact; shownAction = action; shownPromptPad = pad; shownPromptHolding = holding;
            shownToolName = toolName; shownPromptHand = hand; shownPromptDebug = showDebug; shownNightPrompt = null; shownInHand = inHand;
            string line = "";
            if (!string.IsNullOrEmpty(inHand)) line += $"<size=72%><color={UiSkin.HexOf(UiSkin.BrandBright)}>In hand</color>  {inHand}</size>\n";
            // Keys follow the device in use: [E] / [Click] on a keyboard and mouse, the pad's
            // own labels once a controller is being used (see ControlHints). At the bench, X on a pad (12 Oct: A uses there).
            if (!string.IsNullOrEmpty(interact)) line += $"{Key(holding ? ControlHints.TakeFromBench : ControlHints.Interact)}  {interact}";
            if (!string.IsNullOrEmpty(action))
                line += (line.Length > 0 ? "        " : "") + $"{Key(ControlHints.Use)}  {action}";
            // Working on an item: the controls are on their own card now, every one of them, in the device's own glyphs
            // (BenchCards, 12 Oct: the pad's line said "[LB / RB] Hands  [RT] Use  [B] Put item down", nothing of the left
            // stick's turn or Y's flip, and "Hands" read as if LB / RB meant hands).

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
        // At the bench (the tabletop, 8 Oct) the prompt line sits at the bottom edge, out of the mat's way; it eases down and back.
        if (promptText != null && promptHomeKnown)
        {
            float want = inspector != null && inspector.IsHoldingItem ? BenchPromptDrop : 0f;
            promptDrop = Mathf.MoveTowards(promptDrop, want, Time.unscaledDeltaTime * 400f);
            promptText.rectTransform.anchoredPosition = promptHome - new Vector2(0f, promptDrop);
        }
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
