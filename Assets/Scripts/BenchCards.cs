using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE BENCH'S CARDS: CHECKLIST V2 (14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md, the Producer's feel targets T7, T17)
//
// His playtest 5, in his words: "the checklist is terrible". The First Hour counted about 120 words on the first repair's
// screen: the job said four times (the tab, the checklist's header, the prompt's "Clean: ...", the Day 1 card), a 760 px
// strip of sentences across the top, a nine-line controls card. His decision 7 (14 Oct, 05:59): "one small paper-styled card
// on screen ... the customer's name and what's wrong, then the job as three to six tool icons that tick off, the current one
// bigger; the next thing to touch glows softly on the device. The controls card on the first repair only, then behind a
// button. Under 25 words on the bench screen." So, made while playing on a canvas of its own (nothing saved in the scene):
//
//   * THE PAPER CARD, top right under today's takings (ReStory's notepad is on the right): a cream card with the job's tape,
//     the customer's own words in one line (Grace: "The lens and film path are dirty."; FaultSpeech's spoken form, at most
//     8 words), then one icon per step (the bench's own tools), the current one big on a soft disc with its verb under it
//     ("Wipe"), the done ones ticked, "2 left" for a step with several targets. A step whose work lies on the face turned
//     down says "Flip it over" after the flip key's icon. When it's fixed the row folds into "Take it to Grace" after E's.
//   * THE GLOW (BenchGlow) on the next thing to touch: the card's icon says which tool, the glow says where.
//   * TAB / VIEW: a press shows "what's left" (BenchWork.Flash: every speck still dirty flashes gold, PowerWash's dirt
//     indicator); held 0.35 s, every control while it's held.
//   * THE CONTROLS CARD, bottom right: on the first repair only (Day 1 until a repair is handed back, worked out from the
//     day, never PlayerPrefs: "Fresh game" shows it again to playtest 6's stranger), 11 words (the Producer's call 6). After
//     that one hint, the help key's icon and "Help". Stuck 20 s with no progress, the first repair's lines come back until
//     the next tick.
//   * STUCK: no progress for 12 s, the glow pulses once (BenchGlow) and the help hint with it; on the first repair "what's
//     left" fires once by itself then if nothing showed it yet (T17; the bench raises the 3 s scrub case itself).
//
// Everything times itself on UiClock (real seconds that stand still while the phone pauses), so it is the same at his Mac's
// 15 fps as at 60. The card re-reads the steps every 0.1 s and on the frame a target finishes (BenchWork.TargetDone).
// PLACEHOLDER COPY throughout: his sister writes the words (the Content role).
//
// PLAYTEST 7 (19 Oct 2026; his girlfriend, dyslexic, first time, a PlayStation pad): "What does it tell you on the right?
// Flip it over?... It's just so tiny." and "your text needs to be bolder"; "Instead of writing out square, just put the icon."
//   * READABLE: the verb under the current icon is 28 px bold (it was 17), the customer's line 22 (19), the controls card and
//     the help hint 22 (18, 17), the finished line 24. The card keeps to UI/UX's zone U3 (480 x 168): the icons' disc is 58
//     (64), the paddings 10 (12). A step with several pieces says "2 left" after its verb (22 px) instead of 6 px pips that
//     nobody could read.
//   * HONEST TICKS: a step is ticked only when it was done in this job (RepairChecklist: "Cover on" no longer ticks before
//     the cover was ever off), and only the current step wears the soft disc: one icon lit at a time, the one to do.
//   * ICONS, NOT NAMES: every button is its icon (PadInput.Label and ControlHints return a TextMesh Pro sprite tag for the
//     device in hand, builder T's contract of 19 Oct), with no "[ ]" round it and no "Press X" spelled out; the card's words
//     follow whichever device was touched last (the key for the controls card carries it). A press on the cloth and a drag
//     is one line, "Drag a tool", after the mouse's icon.
// ---------------------------------------------------------------------------
public sealed class BenchCards : MonoBehaviour
{
    const int SortingOrder = 20;          // over the HUD's corners (2) and the glow (15), under the pins (30) and the circuit (80)

    // ---- the paper card (reference pixels, 1920 x 1080; §5.2; the sizes of 19 Oct, the header) ----
    const float CardRight = UiSkin.Margin, CardTop = 132f;   // under the takings (18 + 92) with room for the "+$" pop
    const float CardMinWidth = 360f, CardMaxWidth = 480f;
    const float PadX = 14f, PadY = 10f, LineSize = 22f, LineHeight = 28f, LineGap = 4f;
    const float Disc = 58f, BigIcon = 50f, SmallIcon = 32f, IconGap = 10f, VerbSize = 28f, VerbHeight = 30f;
    const float LeftSize = 22f, BadgeSize = 20f, FinishedCheck = 40f, FinishedSize = 24f;
    const float TapeWidth = 44f, TapeHeight = 14f;
    const int MostIcons = 8;

    // ---- the controls card and the hint (§5.5) ----
    const float ControlsWidth = 400f, ControlsSize = 22f, ControlsPadX = 14f, ControlsPadY = 11f, Bottom = 26f;
    const float HintSize = 22f, HintHeight = 40f;
    const float ChipRow = 70f, LabNudge = 92f;   // HudCorners' stock chips (two lines each since 20 Oct: 62 px at a 70 px pitch) and the café lab's banner

    // ---- timing (UiClock seconds) ----
    const float RefreshEvery = .1f, HoldForControls = .35f, FlashSeconds = 1.5f;
    const float StuckPulseAfter = 12f, StuckControlsAfter = 20f, TickedFlashAfter = 1.5f, FinishedHold = .6f;
    const float InSeconds = .25f, OutSeconds = .15f, SlideFrom = 40f;
    const float PopSeconds = .22f, StampSeconds = .18f, GrowSeconds = .25f, TickSoundDelay = .12f;
    const float PulseSeconds = .6f;

    // ------------------------------------------------------------------ what the labs, the glow and the checks read

    public static BenchCards Instance { get; private set; }

    /// <summary>
    /// Labs: null for the game's rule (the first repair's controls card on Day 1 until a repair is handed back); true or false
    /// forces it on or off. Put back to null by whoever set it.
    /// </summary>
    public static bool? LabControls;

    /// <summary>The paper card is on screen.</summary>
    public static bool Showing => Instance != null && Instance.group != null && Instance.canvas.enabled && Instance.group.alpha > .5f;
    /// <summary>The card's line as it reads now, plain (Grace: "The lens and film path are dirty.").</summary>
    public static string JobLine => Instance != null ? Instance.plainLine : "";
    /// <summary>The words under the current icon ("Wipe", "Flip it over" after its icon), plain; "" when every step is ticked.</summary>
    public static string CurrentVerb => Instance != null ? Instance.shownVerb : "";
    /// <summary>The "2 left" after the current verb, plain ("" when the step has one piece, or none to do).</summary>
    public static string CurrentLeft => Instance != null ? Instance.shownLeft : "";
    /// <summary>The current step's icon key ("cloth", "screwdriver"); "" when every step is ticked.</summary>
    public static string CurrentIcon => Instance != null ? Instance.shownIcon : "";
    /// <summary>The icons on the card, in order (their Resources names under UI/BenchIcons).</summary>
    public static IReadOnlyList<string> Icons => Instance != null ? Instance.iconKeys : null;
    /// <summary>The card's row has folded into its finished line (the icon, then "Take it to Grace").</summary>
    public static bool FinishedShowing => Instance != null && Instance.finishedFolded;
    /// <summary>The finished line as it reads now, plain ("" before it folds). Its icon is a tag, so it isn't in the plain text:
    /// FinishedRich has it.</summary>
    public static string FinishedText => Instance != null && Instance.finishedFolded ? Plain(Instance.finishedWords) : "";
    public static string FinishedRich => Instance != null && Instance.finishedFolded ? Instance.finishedWords : "";
    /// <summary>The checklist in one line, plain: the card's line, then the verb (or the finished line).</summary>
    public static string ChecklistText => Instance == null ? "" : Instance.plainLine + " / " + (Instance.finishedFolded ? FinishedText : Instance.shownVerb);
    /// <summary>The job's steps as they stand (labs; ItemInspector's "next part" reads the current one).</summary>
    public static IReadOnlyList<RepairChecklist.Step> Steps => Instance != null ? Instance.steps : null;
    /// <summary>The step to do now (null with every step ticked, or no job).</summary>
    public static RepairChecklist.Step CurrentStep => Instance != null && Instance.currentIndex >= 0 && Instance.currentIndex < Instance.steps.Count
        ? Instance.steps[Instance.currentIndex] : null;   // Refresh keeps currentIndex = the first current step (no boxed enumerator every frame)
    /// <summary>When (UiClock) the current step became current, or the close-up began: the glow waits 0.3 s after a tick.</summary>
    public static float StepSince => Instance != null ? Instance.stepSince : -10f;
    /// <summary>When (UiClock) the stuck pulse fired (12 s with no progress), or -10: the glow's one bright pulse.</summary>
    public static float StuckPulseAt => Instance != null ? Instance.stuckPulseAt : -10f;
    /// <summary>Seconds with no progress on the job (labs).</summary>
    public static float StuckFor => Instance != null && Instance.shownFor != null ? UiClock.Now - Instance.lastProgressAt : 0f;
    /// <summary>The first repair, by the game's rule or the lab's override.</summary>
    public static bool FirstRepairNow => FirstRepairRule();

    /// <summary>A controls card is up (the first repair's, or the full one while Tab / View is held), not the one-line hint.</summary>
    public static bool ControlsShowing => Instance != null && Instance.controlsMode != ControlsMode.None && Showing;
    /// <summary>Kept for the labs that read it before checklist v2: the same as ControlsShowing.</summary>
    public static bool ControlsInFull => ControlsShowing;
    /// <summary>The full controls card is up (Tab / View held 0.35 s).</summary>
    public static bool ControlsHeld => Instance != null && Instance.controlsMode == ControlsMode.Held && Showing;
    /// <summary>The controls card's words as they read now ("" when no card is up), rich (the buttons are sprite tags).</summary>
    public static string ControlsText => ControlsShowing ? Instance.controlsText.text : "";
    /// <summary>The full card's words for the input in hand now, shown or not (labs: the sticks' icons, the flip's key).</summary>
    public static string HeldControlsText => HeldControlsWords();
    /// <summary>The help hint is up (the help key's icon, then "Help").</summary>
    public static bool HintShowing => Instance != null && Instance.hint != null && Instance.hint.gameObject.activeSelf && Showing;
    public static string HintText => HintShowing ? Instance.hintText.text : "";

    /// <summary>The paper card on the screen, in pixels from the bottom left.</summary>
    public static Rect CardOnScreen => Instance != null && Showing ? ScreenRect(Instance.card) : default;
    /// <summary>Kept for the labs that read it before checklist v2 (the old strip): the paper card.</summary>
    public static Rect StripOnScreen => CardOnScreen;
    public static Rect ControlsOnScreen => ControlsShowing ? ScreenRect(Instance.controls) : default;
    public static Rect HintOnScreen => HintShowing ? ScreenRect(Instance.hint) : default;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        LabControls = null;
        icons.Clear();
        check = flipArrow = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<BenchCards>() != null) return;
        var host = new GameObject("Bench cards (runtime)");
        DontDestroyOnLoad(host);
        host.AddComponent<BenchCards>();
    }

    // ------------------------------------------------------------------ the words (pure: the edit check reads them here)

    static readonly string KeyOnBand = UiSkin.HexOf(UiSkin.BrandBright), FaintOnBand = UiSkin.HexOf(UiSkin.BandFaint),
        KeyOnPaper = UiSkin.HexOf(UiSkin.Brand);

    // A button on the band and on the paper: its icon (a sprite tag from PadInput / ControlHints; the colour only shows if the
    // icon ever falls back to a word), with no brackets round it (19 Oct: an icon needs none).
    static string K(string label) => $"<color={KeyOnBand}>{label}</color>";
    static string KP(string label) => $"<color={KeyOnPaper}><b>{label}</b></color>";

    /// <summary>
    /// The card's line: the customer's name and what's wrong in their own words (FaultSpeech), a capital and a full stop:
    /// Grace: "The lens and film path are dirty." A device with no owner (a lab's) gets the quote alone. Plain text.
    /// </summary>
    public static string CardLine(string name, string faultLabel)
    {
        string said = string.IsNullOrWhiteSpace(faultLabel) ? "" : (FaultSpeech.Spoken(faultLabel) ?? "").Trim();
        if (said.Length > 0)
        {
            said = char.ToUpperInvariant(said[0]) + said.Substring(1);
            char last = said[said.Length - 1];
            if (last != '.' && last != '!' && last != '?') said += ".";
            said = "\"" + said + "\"";
        }
        if (string.IsNullOrWhiteSpace(name)) return said;
        return said.Length > 0 ? name.Trim() + ": " + said : name.Trim();
    }

    /// <summary>
    /// The first repair's controls card (the Producer's call 6: 11 words with either input; "(hold: controls)" only on the
    /// held card). Direct grab (14 Oct): the cloth and the brush are taken by pressing on them and dragging, so taking a tool
    /// and using it are one line: the mouse's button, "Drag a tool" (19 Oct: the icon says which button; the words say what).
    /// </summary>
    public static string FirstRepairControlsWords()
    {
        if (ControlHints.Pad)
            return $"{K(ControlHints.BenchHandStick)}  Move\n"
                 + $"{K(PadInput.Label(PadButton.South))}  Use\n"
                 + $"{K(ControlHints.Back)}  Step back\n"
                 + $"{K(ControlHints.BenchHelp)}  What's left";
        return $"{K(ControlHints.Use)}  Drag a tool\n"
             + $"{K(ControlHints.Cancel)}  Step back\n"
             + $"{K(ControlHints.BenchHelp)}  What's left";
    }

    /// <summary>The full card (Tab / View held): every control of the close-up, in the order a stranger needs them. No word budget.</summary>
    public static string HeldControlsWords()
    {
        string help = $"{K(ControlHints.BenchHelp)}  What's left <color={FaintOnBand}>(hold: controls)</color>";
        if (ControlHints.Pad)
            return $"{K(ControlHints.BenchHandStick)}  Move your hand\n"
                 + $"{K(ControlHints.Tools)}  Pick a tool\n"
                 + $"{K(ControlHints.BenchUse)}  Hold to use\n"
                 + $"{K(ControlHints.BenchTurnStick)}  Turn it a quarter · up/down tilts it\n"
                 + $"{K(ControlHints.Flip)}  Flip it over\n"
                 + $"{K(PadInput.Label(PadButton.DpadLeft))} {K(PadInput.Label(PadButton.DpadRight))}  Next part\n"
                 + $"{K(ControlHints.BenchZoom)}  Zoom\n"
                 + $"{K(ControlHints.Back)}  Step back\n"
                 + $"{K(ControlHints.TakeFromBench)}  Pick it up\n"
                 + help;
        return $"{K(ControlHints.Use)}  Drag a tool to use it\n"
             + $"{K(ControlHints.Use)}  Drag the mat to turn it · up/down tilts it\n"
             + $"{K(ControlHints.Flip)}  Flip it over\n"
             + $"{K(ControlHints.BenchZoom)}  Zoom\n"
             + $"{K(ControlHints.Cancel)}  Step back\n"
             + $"{K(ControlHints.TakeFromBench)}  Pick it up\n"
             + help;
    }

    /// <summary>The one hint after the first repair: the help key's icon, then "Help". The phone can't open at the bench, so this is where the controls are found.</summary>
    public static string HintWords() => $"{K(ControlHints.BenchHelp)}  Help";

    /// <summary>The verb for a step whose work lies on the face turned down: the flip key's icon, then "Flip it over"; 3 words.</summary>
    public static string FlipWords() => $"{KP(ControlHints.Flip)} Flip it over";

    /// <summary>
    /// The finished line: the pick-up key's icon, then "Take it to Grace" (E; Square on a PlayStation pad: Cross uses at the
    /// bench); "Hands full: free a hand first"; "Pick it up" for a device with no owner. <paramref name="called"/> is the owner
    /// as a sentence says them ("Grace", "the Officer": DayOneGuideUI.Called), or "" for none.
    /// </summary>
    public static string FinishedWords(string called, bool handsFull)
    {
        if (handsFull) return "Hands full: free a hand first";
        return string.IsNullOrWhiteSpace(called) ? $"{KP(ControlHints.TakeFromBench)} Pick it up" : $"{KP(ControlHints.TakeFromBench)} Take it to {called}";
    }

    /// <summary>"2 left" after the current verb for a step with several pieces (19 Oct: the 6 px pips were unreadable); ""
    /// for one piece, or none left.</summary>
    public static string LeftWords(RepairChecklist.Step step) =>
        step == null || step.done || step.total <= 1 || step.left <= 0 ? "" : step.left + " left";

    /// <summary>The words under a step's icon: its verb (RepairChecklist fills it), or one from its tool while that's missing.</summary>
    public static string VerbOf(RepairChecklist.Step step)
    {
        if (step == null) return "";
        if (!string.IsNullOrWhiteSpace(step.verb)) return step.verb;
        return step.tool switch
        {
            ToolType.Screwdriver => "Unscrew",
            ToolType.Pry => "Pry off",
            ToolType.Tweezers => "Lift out",
            ToolType.Brush => "Brush",
            ToolType.Cloth => "Wipe",
            _ => "Cover on",
        };
    }

    /// <summary>A step's icon (its Resources name under UI/BenchIcons): RepairChecklist's, or its tool's while that's missing.</summary>
    public static string IconOf(RepairChecklist.Step step)
    {
        if (step == null) return "";
        if (!string.IsNullOrWhiteSpace(step.icon)) return step.icon;
        return step.tool switch
        {
            ToolType.Screwdriver => "screwdriver",
            ToolType.Pry => "pry",
            ToolType.Tweezers => "tweezers",
            ToolType.Brush => "brush",
            ToolType.Cloth => "cloth",
            _ => "cover",
        };
    }

    /// <summary>Rich text to plain: the tags out, line breaks as " / ".</summary>
    public static string Plain(string rich) => Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " / ");

    static bool FirstRepairRule()
    {
        if (LabControls.HasValue) return LabControls.Value;
        DayClock clock = DayClock.Instance;
        return clock != null && clock.Day == 1 && clock.Repairs == 0;
    }

    // ------------------------------------------------------------------ state

    enum ControlsMode { None, Short, Held }

    sealed class Slot
    {
        public RectTransform root, iconRect, discRect, badgeRect;
        public Image disc, icon, checkDisc, checkMark, flipDisc, flipMark;
        public float sizeFrom = SmallIcon, sizeTo = SmallIcon, sizeSince = -10f, tickSince = -10f;
        public string key;
        public bool used, done, current, flip, real;
    }

    // A step as the card knows it from one refresh to the next (its icon and verb and which of its kind it is): a step done
    // now that wasn't before ticks.
    struct StepKey
    {
        public string icon, verb;
        public int nth;
        public bool Same(StepKey o) => nth == o.nth && icon == o.icon && verb == o.verb;
    }

    Canvas canvas;
    CanvasGroup group;
    RectTransform card, tape, controls, hint, row;
    Image cardShadow, controlsShadow, hintEdge, controlsEdge, finishedCheckImage;
    TextMeshProUGUI lineText, verbText, leftText, finishedText, controlsText, hintText;
    readonly Slot[] slots = new Slot[MostIcons];
    readonly List<RepairChecklist.Step> steps = new();
    readonly List<string> iconKeys = new();
    readonly List<StepKey> lastKeys = new(), keys = new();
    readonly List<bool> lastDone = new();

    JobBase shownFor;
    float nextRefresh, inSince = -10f, outSince = -10f, cardAlpha, stepSince = -10f, lastProgressAt, stuckPulseAt = -10f;
    float tickedSince = -1f, finishedAt = -1f, helpDownSince = -1f, pulseAt = -10f;
    bool refreshNow, wasWanted, stuckPulsed, stuckControls, tickedFlashed, finishedFolded;
    float lineH = LineHeight;   // the customer's line as measured in the skin's font (one line, or two when it wraps)
    JobBase flashedJob;   // the last job "what's left" showed for, kept across close-ups: T17's automatic flash is once a job
    int currentIndex = -1;
    StepKey currentKey;
    string plainLine = "", shownVerb = "", shownLeft = "", shownIcon = "", finishedWords = "";
    int controlsKey = -1, hintKey = -1;   // ints, so the bench screen allocates nothing per frame (review m3)
    float cardWidth = CardMinWidth;
    ControlsMode controlsMode;
    bool layoutDirty = true;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        BenchWork.Progressed += OnProgressed;
        BenchWork.TargetDone += OnTargetDone;
        BenchWork.Flashed += OnFlashed;
    }

    void OnDisable()
    {
        BenchWork.Progressed -= OnProgressed;
        BenchWork.TargetDone -= OnTargetDone;
        BenchWork.Flashed -= OnFlashed;
    }

    // The bench's own events (Systems §4.5): any progress resets the stuck clock; a target finishing re-reads the steps on
    // that frame (the card ticks with the world, not up to 0.1 s after it).
    void OnProgressed(JobBase job)
    {
        if (job != null && job == shownFor) lastProgressAt = UiClock.Now;
    }

    void OnTargetDone(JobBase job, Component target)
    {
        if (job == null || job != shownFor) return;
        lastProgressAt = UiClock.Now;
        refreshNow = true;
    }

    void OnFlashed(JobBase job, float seconds)
    {
        if (job != null) flashedJob = job;
    }

    // ------------------------------------------------------------------ every frame

    void LateUpdate()
    {
        ItemInspector inspector = ItemInspector.Instance;
        JobBase job = inspector != null ? inspector.FocusedItem : null;
        bool want = job != null && Time.timeScale > 0f && !RecapUI.Showing;
        float now = UiClock.Now;
        if (job != shownFor) Begin(job, now);
        if (want && canvas == null) Build();
        if (canvas == null) return;

        // In: the card slides 40 px down and fades in (0.25 s, ease-out-back); out: it fades (0.15 s).
        if (want != wasWanted)
        {
            if (want) inSince = now; else outSince = now;
            wasWanted = want;
        }
        cardAlpha = want ? Mathf.Clamp01((now - inSince) / InSeconds) : Mathf.Min(cardAlpha, 1f - Mathf.Clamp01((now - outSince) / OutSeconds));
        group.alpha = cardAlpha;
        canvas.enabled = cardAlpha > .001f;
        if (!want)
        {
            helpDownSince = -1f;
            return;
        }

        HelpKey(job, now);
        if (refreshNow || now >= nextRefresh) Refresh(job, now);
        Stuck(job, now);
        Controls(job, now);
        Animate(now);
    }

    void Begin(JobBase job, float now)
    {
        shownFor = job;
        steps.Clear();
        lastKeys.Clear();
        lastDone.Clear();
        iconKeys.Clear();
        currentIndex = -1;
        currentKey = default;
        stepSince = now - 1f;   // the first step's glow needs no tick to wait for
        lastProgressAt = now;
        stuckPulseAt = -10f;
        stuckPulsed = stuckControls = tickedFlashed = finishedFolded = false;
        tickedSince = finishedAt = -1f;
        nextRefresh = 0f;
        refreshNow = true;
        plainLine = shownVerb = shownLeft = shownIcon = finishedWords = "";
        layoutDirty = true;
        foreach (Slot s in slots)
            if (s != null) { s.used = false; s.key = null; s.tickSince = -10f; s.sizeFrom = s.sizeTo = SmallIcon; s.sizeSince = -10f; }
        if (job == null || lineText == null) return;
        SetLine(job);
    }

    // Tab, or View on a pad: a press shows what's left at once (not on release); held 0.35 s, every control while held. The
    // edges are read each frame and the hold is timed on the UI's clock, so 15 fps reads it as 60 does.
    void HelpKey(JobBase job, float now)
    {
        Keyboard keyboard = Keyboard.current;
        bool down = keyboard != null && keyboard.tabKey.isPressed || PadInput.Held(PadButton.Select);
        bool pressed = keyboard != null && keyboard.tabKey.wasPressedThisFrame || PadInput.Pressed(PadButton.Select);
        if (pressed)
        {
            helpDownSince = now;
            BenchWork.Flash(job, FlashSeconds);
        }
        if (!down) helpDownSince = -1f;
    }

    bool HelpHeld(float now) => helpDownSince >= 0f && now - helpDownSince >= HoldForControls;

    // ------------------------------------------------------------------ the steps

    void Refresh(JobBase job, float now)
    {
        refreshNow = false;
        nextRefresh = now + RefreshEvery;
        bool firstLook = lastKeys.Count == 0 && currentIndex < 0;
        RepairChecklist.Build(job, steps);

        // Which steps ticked since the last look (the same step by its icon, its verb and which of them it is).
        keys.Clear();
        bool ticked = false;
        int newCurrent = -1;
        for (int i = 0; i < steps.Count; i++)
        {
            RepairChecklist.Step step = steps[i];
            var key = new StepKey { icon = IconOf(step), verb = VerbOf(step) };
            for (int j = 0; j < i; j++) if (keys[j].icon == key.icon && keys[j].verb == key.verb) key.nth++;
            keys.Add(key);
            if (step.current && newCurrent < 0) newCurrent = i;
            if (!step.done) continue;
            for (int j = 0; j < lastKeys.Count; j++)
                if (lastKeys[j].Same(key))
                {
                    if (!lastDone[j]) { ticked = true; if (i < MostIcons) Tick(i, now); }
                    break;
                }
        }
        lastKeys.Clear();
        lastDone.Clear();
        for (int i = 0; i < steps.Count; i++) { lastKeys.Add(keys[i]); lastDone.Add(steps[i].done); }

        if (ticked)
        {
            // The world's own sound first (the last screw's tink), then the card's pencil tick (Audio: checklist.tick).
            Sfx.Play2DLater("checklist.tick", TickSoundDelay);
            lastProgressAt = now;
            stuckControls = false;
            stuckPulsed = false;
            stuckPulseAt = -10f;
        }

        StepKey nowKey = newCurrent >= 0 ? keys[newCurrent] : default;
        if (newCurrent != currentIndex || newCurrent >= 0 && !nowKey.Same(currentKey))
        {
            currentIndex = newCurrent;
            currentKey = nowKey;
            if (!firstLook) stepSince = now;   // the glow waits 0.3 s after a tick; the close-up's first step glows at once
            lastProgressAt = now;
            stuckPulsed = false;
            stuckPulseAt = -10f;
            layoutDirty = true;
        }

        // Every step ticked but the job not finished ("look it over"): all checks, no words; 1.5 s of it shows what's left.
        bool finished = job.IsFinished;
        if (newCurrent < 0 && !finished && steps.Count > 0)
        {
            if (tickedSince < 0f) tickedSince = now;
            if (!tickedFlashed && now - tickedSince >= TickedFlashAfter)
            {
                tickedFlashed = true;
                pulseAt = now;   // the key that does this by hand pulses with it
                BenchWork.Flash(job, FlashSeconds);
            }
        }
        else { tickedSince = -1f; tickedFlashed = false; }

        // Finished: the checks hold 0.6 s, then the row folds into "Take it to Grace" (after E's icon).
        if (finished && finishedAt < 0f) finishedAt = now;
        if (!finished) { finishedAt = -1f; if (finishedFolded) { finishedFolded = false; layoutDirty = true; } }
        if (finished && !finishedFolded && now - finishedAt >= FinishedHold) { finishedFolded = true; layoutDirty = true; }
        if (finished)
        {
            PlayerCarry carry = PlayerCarry.Current;
            string words = FinishedWords(job.Owner != null ? DayOneGuideUI.Called(job.Owner) : "", carry != null && !carry.HasSpace);
            if (words != finishedWords) { finishedWords = words; layoutDirty = true; }
        }

        // The icons.
        iconKeys.Clear();
        for (int i = 0; i < steps.Count && i < MostIcons; i++)
        {
            RepairChecklist.Step step = steps[i];
            Slot s = slots[i];
            string key = IconOf(step);
            iconKeys.Add(key);
            bool current = i == newCurrent;
            bool flip = current && step.under;
            if (!s.used || s.key != key || s.done != step.done || s.current != current || s.flip != flip) layoutDirty = true;
            if (!s.used || s.key != key)
            {
                s.icon.sprite = Icon(key, out bool real);
                s.real = real;
            }
            s.used = true;
            s.key = key;
            s.done = step.done;
            s.flip = flip;
            float size = current ? BigIcon : SmallIcon;
            if (!Mathf.Approximately(s.sizeTo, size) || s.current != current)
            {
                s.sizeFrom = s.sizeSince < -5f ? size : SizeOf(s, now);
                s.sizeTo = size;
                s.sizeSince = now;
            }
            s.current = current;
        }
        for (int i = steps.Count; i < MostIcons; i++)
            if (slots[i] != null && slots[i].used) { slots[i].used = false; layoutDirty = true; }

        RepairChecklist.Step doing = newCurrent >= 0 ? steps[newCurrent] : null;
        string verb = doing == null ? "" : doing.under ? FlipWords() : VerbOf(doing);
        if (verb != verbText.text) { verbText.text = verb; layoutDirty = true; }
        // "2 left" after the verb for a step with several pieces (not while it asks for the flip: one thing at a time).
        string left = doing == null || doing.under ? "" : LeftWords(doing);
        if (left != leftText.text) { leftText.text = left; layoutDirty = true; }
        shownVerb = Plain(verb);
        shownLeft = left;
        shownIcon = doing != null ? IconOf(doing) : "";
    }

    void Tick(int i, float now)
    {
        Slot s = slots[i];
        if (s == null) return;
        s.tickSince = now;
        layoutDirty = true;
    }

    // ------------------------------------------------------------------ stuck (§5.4)

    void Stuck(JobBase job, float now)
    {
        RepairChecklist.Step step = CurrentStep;
        // Nothing to be stuck on: fixed, everything ticked, or the circuit (its board has the screen and its own clock).
        bool idle = job.IsFinished || step == null || step.tool == ToolType.Hand && (step.targets == null || step.targets.Count == 0);
        if (idle)
        {
            lastProgressAt = now;
            return;
        }
        float stuck = now - lastProgressAt;
        if (!stuckPulsed && stuck >= StuckPulseAfter)
        {
            stuckPulsed = true;
            stuckPulseAt = now;
            pulseAt = now;   // the help hint (or the first repair's card) pulses with the glow
            // T17: on the first repair "what's left" fires once by itself, here if nothing has shown it yet (the bench
            // raises the 3 s scrub-with-no-progress case itself; either way, once).
            if (FirstRepairRule() && flashedJob != job) BenchWork.Flash(job, FlashSeconds);
        }
        if (!stuckControls && stuck >= StuckControlsAfter) stuckControls = true;
    }

    // ------------------------------------------------------------------ the controls card and the hint

    void Controls(JobBase job, float now)
    {
        bool finished = job.IsFinished;
        ControlsMode mode = HelpHeld(now) ? ControlsMode.Held
            : !finished && (FirstRepairRule() || stuckControls) ? ControlsMode.Short
            : ControlsMode.None;
        bool pad = ControlHints.Pad;
        int key = mode == ControlsMode.None ? 0
            : 1 + (int)mode * 2 + (pad ? 8 : 0) + (GameSettings.SwapBenchSticks ? 16 : 0) + ((int)PadInput.Kind << 5);
        if (key != controlsKey)
        {
            controlsKey = key;
            if (mode != ControlsMode.None)
            {
                string words = mode == ControlsMode.Held ? HeldControlsWords() : FirstRepairControlsWords();
                controlsText.text = words;
                float height = controlsText.GetPreferredValues(words, ControlsWidth - 2f * ControlsPadX, 0f).y;
                controls.sizeDelta = new Vector2(ControlsWidth, Mathf.Ceil(height) + 2f * ControlsPadY);
            }
        }
        controlsMode = mode;
        if (controls.gameObject.activeSelf != (mode != ControlsMode.None)) controls.gameObject.SetActive(mode != ControlsMode.None);
        if (controlsShadow.gameObject.activeSelf != (mode != ControlsMode.None)) controlsShadow.gameObject.SetActive(mode != ControlsMode.None);

        bool hintOn = mode == ControlsMode.None;
        int hintNow = hintOn ? 1 + (pad ? 2 : 0) + ((int)PadInput.Kind << 2) : 0;
        if (hintNow != hintKey)
        {
            hintKey = hintNow;
            if (hintOn)
            {
                string hintWords = HintWords();
                hintText.text = hintWords;
                float width = hintText.GetPreferredValues(hintWords, 999f, HintHeight).x;
                hint.sizeDelta = new Vector2(Mathf.Ceil(width) + 28f, HintHeight);
            }
        }
        if (hint.gameObject.activeSelf != hintOn) hint.gameObject.SetActive(hintOn);

        // Bottom right, 26 px up: above the stock chips when they show, and above the café lab's banner in a lab.
        float y = Bottom + (CafeLab.Active ? LabNudge : 0f);
        HudCorners corners = HudCorners.Instance;
        if (corners != null) y += ((corners.CupsChipShowing ? 1 : 0) + (corners.BeansChipShowing ? 1 : 0)) * ChipRow;
        controls.anchoredPosition = new Vector2(-UiSkin.Margin, y);
        hint.anchoredPosition = new Vector2(-UiSkin.Margin, y);
        UiSkin.Follow(controlsShadow.rectTransform, controls, 8f, 3f);

        // The pulse (stuck, or "what's left" fired by itself): 1.0 → 1.15 → 1.0 with a gold edge, 0.6 s.
        float k = now - pulseAt < PulseSeconds ? Mathf.Sin(Mathf.Clamp01((now - pulseAt) / PulseSeconds) * Mathf.PI) : 0f;
        float hs = 1f + .15f * k, cs = 1f + .05f * k;
        hint.localScale = new Vector3(hs, hs, 1f);
        controls.localScale = new Vector3(cs, cs, 1f);
        hintEdge.color = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, k);
        controlsEdge.color = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, k);
    }

    // ------------------------------------------------------------------ drawing the paper card

    void SetLine(JobBase job)
    {
        string name = job.Owner != null ? job.Owner.CustomerName : "";
        string fault = job.Record != null ? job.Record.faultDescription : job.JobCard;
        plainLine = CardLine(name, fault);
        string rich = plainLine;
        if (!string.IsNullOrEmpty(name) && rich.StartsWith(name.Trim() + ":"))
            rich = "<b>" + name.Trim() + ":</b>" + rich.Substring(name.Trim().Length + 1);
        lineText.text = rich;
        Vector2 size = lineText.GetPreferredValues(rich, CardMaxWidth - 2f * PadX, 0f);
        // Its height as the font draws it (a font with tall ascenders, Baloo 2 say, needs more than LiberationSans): the layout
        // keeps to the measure, never to a guess of one or two lines.
        lineH = Mathf.Max(LineHeight, Mathf.Ceil(size.y));
        // The tape: the job's colour, as on its tab and its tag.
        Color c = job.Owner != null ? job.Owner.JobColor : job.Record != null ? job.Record.color : UiSkin.Brand;
        tape.GetComponent<Image>().color = new Color(c.r, c.g, c.b, .92f);
        lineWidth = Mathf.Min(size.x, CardMaxWidth - 2f * PadX);
        layoutDirty = true;
    }

    float lineWidth;

    static float SizeOf(Slot s, float now)
    {
        float t = Mathf.Clamp01((now - s.sizeSince) / GrowSeconds);
        return Mathf.Lerp(s.sizeFrom, s.sizeTo, 1f - (1f - t) * (1f - t));
    }

    void Animate(float now)
    {
        // The slide in.
        float slideT = Mathf.Clamp01((now - inSince) / InSeconds);
        float slide = SlideFrom * (1f - BackOut(slideT));
        card.anchoredPosition = new Vector2(-CardRight, -CardTop + slide);

        bool moving = slideT < 1f || layoutDirty;
        foreach (Slot s in slots)
        {
            if (s == null || !s.used) continue;
            if (now - s.sizeSince < GrowSeconds || now - s.tickSince < Mathf.Max(PopSeconds, StampSeconds)) moving = true;
        }
        if (!moving) { UiSkin.Follow(cardShadow.rectTransform, card, 8f, 3f); return; }
        layoutDirty = false;
        Layout(now);
        UiSkin.Follow(cardShadow.rectTransform, card, 8f, 3f);
    }

    // Everything on the card placed for this moment: the line, the icons (sizes easing), the verb and "2 left", or the
    // finished line. Only while something moves (a tick, a grow, the slide in, new words); still frames cost nothing.
    void Layout(float now)
    {
        float lineH = this.lineH;
        Place(lineText.rectTransform, new Vector2(PadX, -PadY), new Vector2(CardMaxWidth - 2f * PadX, lineH));
        float rowTop = -PadY - lineH - LineGap;
        float centreY = rowTop - Disc * .5f;

        row.gameObject.SetActive(!finishedFolded);
        finishedText.gameObject.SetActive(finishedFolded);
        finishedCheckImage.gameObject.SetActive(finishedFolded);
        float width, height;
        if (finishedFolded)
        {
            finishedText.text = finishedWords;
            Place(finishedCheckImage.rectTransform, new Vector2(PadX, rowTop), new Vector2(FinishedCheck, FinishedCheck));
            float words = finishedText.GetPreferredValues(finishedWords, 999f, FinishedCheck).x;
            Place(finishedText.rectTransform, new Vector2(PadX + FinishedCheck + 10f, rowTop), new Vector2(Mathf.Ceil(words) + 4f, FinishedCheck));
            width = Mathf.Max(lineWidth, FinishedCheck + 10f + words) + 2f * PadX;
            height = PadY + lineH + LineGap + FinishedCheck + PadY;
        }
        else
        {
            float x = PadX, finalX = PadX;
            int used = 0;
            Slot current = null;
            float currentLeft = PadX;
            for (int i = 0; i < MostIcons; i++)
            {
                Slot s = slots[i];
                if (s == null) continue;
                if (!s.used) { s.root.gameObject.SetActive(false); continue; }
                s.root.gameObject.SetActive(true);
                used++;
                float size = SizeOf(s, now);
                float k = Mathf.InverseLerp(SmallIcon, BigIcon, size);
                float w = Mathf.Lerp(SmallIcon, Disc, k);
                if (s.current) { current = s; currentLeft = x; }
                PlaceSlot(s, x, centreY, w, size, k, now);
                x += w + IconGap;
                finalX += (s.current ? Disc : SmallIcon) + IconGap;
            }
            // The verb under the current icon (centred on it when it fits, else from its left edge), "2 left" after it.
            float verbWidth = verbText.text.Length > 0 ? verbText.GetPreferredValues(verbText.text, 999f, VerbHeight).x : 0f;
            float leftWidth = leftText.text.Length > 0 ? leftText.GetPreferredValues(leftText.text, 999f, VerbHeight).x : 0f;
            float verbLeft = current == null ? PadX : verbWidth <= Disc + 16f ? currentLeft + Disc * .5f - verbWidth * .5f : currentLeft;
            verbLeft = Mathf.Max(PadX, verbLeft);
            Place(verbText.rectTransform, new Vector2(verbLeft, rowTop - Disc), new Vector2(Mathf.Ceil(verbWidth) + 4f, VerbHeight));
            float verbRight = verbLeft + verbWidth;
            leftText.gameObject.SetActive(leftWidth > 0f);
            if (leftWidth > 0f)
            {
                Place(leftText.rectTransform, new Vector2(verbRight + 10f, rowTop - Disc), new Vector2(Mathf.Ceil(leftWidth) + 4f, VerbHeight));
                verbRight += 10f + leftWidth;
            }
            width = Mathf.Max(lineWidth + 2f * PadX, finalX - IconGap + PadX, verbRight + PadX);
            height = PadY + lineH + LineGap + Disc + VerbHeight + PadY;
            if (used == 0) height = PadY + lineH + PadY;
        }
        cardWidth = Mathf.Clamp(Mathf.Ceil(width), CardMinWidth, CardMaxWidth);
        card.sizeDelta = new Vector2(cardWidth, Mathf.Ceil(height));
        lineText.rectTransform.sizeDelta = new Vector2(cardWidth - 2f * PadX, lineH);
    }

    void PlaceSlot(Slot s, float left, float centreY, float width, float size, float k, float now)
    {
        Place(s.root, new Vector2(left, centreY + Disc * .5f), new Vector2(width, Disc));
        // The soft disc behind the current icon grows in with it, and only the current one wears it (19 Oct: one icon lit at a
        // time; the one shrinking away showed a fading disc beside the one growing).
        s.discRect.sizeDelta = new Vector2(width, width);
        s.disc.color = new Color(UiSkin.BrandSoft.r, UiSkin.BrandSoft.g, UiSkin.BrandSoft.b, s.current ? k : 0f);
        // The tick's pop: 1.0 → 1.18 → 1.0 over 0.22 s.
        float pop = now - s.tickSince < PopSeconds ? 1f + .18f * Mathf.Sin(Mathf.Clamp01((now - s.tickSince) / PopSeconds) * Mathf.PI) : 1f;
        s.iconRect.sizeDelta = new Vector2(size, size);
        s.iconRect.localScale = new Vector3(pop, pop, 1f);
        // Done: greyed (the check carries it, not the colour); to do: 55 %; current: full colour. A missing icon is a faint
        // disc, smaller (the verb and the glow carry the step).
        Color tint = s.real ? Color.white : UiSkin.InkFaint;
        float alpha = s.done ? .8f : s.current ? 1f : .55f;
        s.icon.color = s.done && s.real ? new Color(.8f, .8f, .8f, alpha) : new Color(tint.r, tint.g, tint.b, alpha);
        if (!s.real) s.iconRect.sizeDelta = new Vector2(size * .6f, size * .6f);
        // The check stamps in (1.6 → 1.0, 0.18 s) at the lower right.
        s.checkDisc.gameObject.SetActive(s.done);
        if (s.done)
        {
            float t = Mathf.Clamp01((now - s.tickSince) / StampSeconds);
            float stamp = s.tickSince < -5f ? 1f : Mathf.Lerp(1.6f, 1f, 1f - (1f - t) * (1f - t));
            RectTransform c = s.checkDisc.rectTransform;
            c.anchoredPosition = new Vector2(size * .5f - 5f, -size * .5f + 5f);
            c.localScale = new Vector3(stamp, stamp, 1f);
            s.checkDisc.color = new Color(UiSkin.Brand.r, UiSkin.Brand.g, UiSkin.Brand.b, s.tickSince < -5f ? 1f : t);
            s.checkMark.color = new Color(1f, 1f, 1f, s.tickSince < -5f ? 1f : t);
        }
        // Flip: the step's icon with a badge (the turn-over arrow) at its upper right.
        s.flipDisc.gameObject.SetActive(s.flip);
        if (s.flip) s.badgeRect.anchoredPosition = new Vector2(size * .5f - 4f, size * .5f - 4f);
    }

    static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    // ------------------------------------------------------------------ making it (once, while playing)

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("Bench cards (while playing)", transform, SortingOrder);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var root = (RectTransform)canvas.transform;

        // The paper card, top right.
        card = new GameObject("The job card", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(root, false);
        card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-CardRight, -CardTop);
        card.sizeDelta = new Vector2(CardMinWidth, 142f);
        UiSkin.Paint(card, new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .96f), 8f);
        cardShadow = UiSkin.DropShadow(card, 8f, 3f, .3f);

        // The tape, in the job's colour, across the top-left corner (as on its tab: JobTicket).
        tape = new GameObject("Tape (the job's colour)", typeof(RectTransform)).GetComponent<RectTransform>();
        tape.SetParent(card, false);
        tape.anchorMin = tape.anchorMax = new Vector2(0f, 1f);
        tape.pivot = new Vector2(.5f, .5f);
        tape.anchoredPosition = new Vector2(10f + TapeWidth * .5f, 6f - TapeHeight * .5f);
        tape.sizeDelta = new Vector2(TapeWidth, TapeHeight);
        tape.localRotation = Quaternion.Euler(0f, 0f, -7f);
        UiSkin.Paint(tape, UiSkin.Brand, 2f);

        // The customer's line at 22 px (19 Oct: "your text needs to be bolder"; the name in bold, SetLine).
        lineText = UiSkin.Text("The customer's words", card, LineSize, UiSkin.Ink, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);

        row = new GameObject("The steps", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(card, false);
        Place(row, Vector2.zero, Vector2.zero);
        for (int i = 0; i < MostIcons; i++) slots[i] = MakeSlot(i);
        // The verb at 28 px bold: the one line on the bench screen that says what to do now (her "it's just so tiny").
        verbText = UiSkin.Text("The verb", row, VerbSize, UiSkin.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        leftText = UiSkin.Text("How many left", row, LeftSize, UiSkin.InkSoft, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        leftText.gameObject.SetActive(false);

        finishedCheckImage = MakeImage("Fixed (check)", card, Check, UiSkin.Brand);
        finishedText = UiSkin.Text("Take it back", card, FinishedSize, UiSkin.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        finishedCheckImage.gameObject.SetActive(false);
        finishedText.gameObject.SetActive(false);

        // The controls card and the hint, bottom right (22 px, semibold: they instruct).
        controls = BandCard("The controls", root, ControlsWidth, out controlsShadow, out controlsEdge, out _);
        controlsText = UiSkin.Text("Words", controls, ControlsSize, UiSkin.BandText, TextAlignmentOptions.TopLeft, FontStyles.Bold, true);
        controlsText.lineSpacing = 4f;
        RectTransform ct = controlsText.rectTransform;
        ct.anchorMin = Vector2.zero;
        ct.anchorMax = Vector2.one;
        ct.offsetMin = new Vector2(ControlsPadX, ControlsPadY);
        ct.offsetMax = new Vector2(-ControlsPadX, -ControlsPadY);
        controls.gameObject.SetActive(false);
        controlsShadow.gameObject.SetActive(false);

        hint = BandCard("The help hint", root, 120f, out Image hintShadow, out hintEdge, out Image hintFace);
        hint.sizeDelta = new Vector2(120f, HintHeight);
        UiSkin.Round(hintFace, HintHeight * .5f);
        UiSkin.Round(hintEdge, HintHeight * .5f + 3f);
        hintShadow.gameObject.SetActive(false);
        hintText = UiSkin.Text("Words", hint, HintSize, UiSkin.BandText, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform ht = hintText.rectTransform;
        ht.anchorMin = Vector2.zero;
        ht.anchorMax = Vector2.one;
        ht.offsetMin = ht.offsetMax = Vector2.zero;
        hint.gameObject.SetActive(false);

        if (shownFor != null) SetLine(shownFor);
        layoutDirty = true;
    }

    // A dark band card in the bottom right corner, pivoted at its bottom right: a bare root holding the pulse's gold edge (a
    // rounded box 3 px larger, drawn first), then the band's face; the caller adds the words after them. Hiding the root
    // hides all three. The shadow is the root's sibling (UiSkin.DropShadow), shown and hidden with it by the caller.
    RectTransform BandCard(string name, RectTransform root, float width, out Image shadow, out Image edge, out Image face)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-UiSkin.Margin, Bottom);
        rect.sizeDelta = new Vector2(width, 60f);
        shadow = UiSkin.DropShadow(rect, 8f, 3f, .3f);
        edge = UiSkin.Paint(Stretched("Edge (pulse)", rect, -3f), new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0f), UiSkin.Radius + 3f);
        face = UiSkin.Paint(Stretched("Face", rect, 0f), UiSkin.Band, UiSkin.Radius);
        return rect;
    }

    static RectTransform Stretched(string name, RectTransform parent, float inset)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    Slot MakeSlot(int i)
    {
        var s = new Slot();
        s.root = new GameObject("Step " + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
        s.root.SetParent(row, false);
        s.disc = MakeImage("Disc", s.root, UiSkin.Circle, UiSkin.BrandSoft);
        s.discRect = s.disc.rectTransform;
        Centre(s.discRect, Disc);
        s.icon = MakeImage("Icon", s.root, null, Color.white);
        s.icon.preserveAspect = true;
        s.iconRect = s.icon.rectTransform;
        Centre(s.iconRect, SmallIcon);
        s.checkDisc = MakeImage("Done", s.iconRect, UiSkin.Circle, UiSkin.Brand);
        Centre(s.checkDisc.rectTransform, 16f);
        s.checkMark = MakeImage("Check", s.checkDisc.rectTransform, Check, Color.white);
        Centre(s.checkMark.rectTransform, 12f);
        s.checkDisc.gameObject.SetActive(false);
        s.flipDisc = MakeImage("Flip it over", s.iconRect, UiSkin.Circle, UiSkin.Gold);
        s.badgeRect = s.flipDisc.rectTransform;
        Centre(s.badgeRect, BadgeSize);
        s.flipMark = MakeImage("Arrow", s.badgeRect, FlipArrow, UiSkin.Ink);
        Centre(s.flipMark.rectTransform, BadgeSize - 4f);
        s.flipDisc.gameObject.SetActive(false);
        s.root.gameObject.SetActive(false);
        return s;
    }

    static void Centre(RectTransform r, float size)
    {
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(size, size);
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite, Color colour)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    // ------------------------------------------------------------------ the icons and the little pictures

    // The bench's own tools, rendered (the Blender lead's render_bench_icons.py; Resources/UI/BenchIcons/<name>.png). A
    // missing one is a plain faint disc: the verb and the glow still say it.
    static readonly Dictionary<string, Sprite> icons = new();

    static Sprite Icon(string key, out bool real)
    {
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(key) && !icons.TryGetValue(key, out sprite))
        {
            sprite = Resources.Load<Sprite>("UI/BenchIcons/" + key);
            // A device's own part or cover ("part_watch") falls back to the generic one ("newpart", "cover").
            if (sprite == null && key.StartsWith("part_")) sprite = Resources.Load<Sprite>("UI/BenchIcons/newpart");
            if (sprite == null && key.StartsWith("cover_")) sprite = Resources.Load<Sprite>("UI/BenchIcons/cover");
            icons[key] = sprite;
        }
        real = sprite != null;
        return real ? sprite : UiSkin.Circle;
    }

    /// <summary>Whether the bench's icon <paramref name="key"/> is in the project (Resources/UI/BenchIcons), its fallbacks included (checks).</summary>
    public static bool HasIcon(string key)
    {
        Icon(key, out bool real);
        return real;
    }

    static Sprite check, flipArrow;
    static Sprite Check => check != null ? check : check = Draw("Check", 64, (x, y) =>
        Mathf.Min(Segment(x, y, 15f, 33f, 27f, 21f), Segment(x, y, 27f, 21f, 49f, 45f)) - 4.2f);
    static Sprite FlipArrow => flipArrow != null ? flipArrow : flipArrow = Draw("Flip it over", 64, (x, y) =>
    {
        // Three quarters of a circle, and its arrowhead at the open end.
        float dx = x - 32f, dy = y - 32f;
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        float ring = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 18f) - 4f;
        if (angle > 300f) ring = 99f;
        float head = Triangle(x, y, 50f, 30f, 60f, 44f, 40f, 44f);
        return Mathf.Min(ring, head);
    });

    static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        Vector2 p = new Vector2(x - ax, y - ay), d = new Vector2(bx - ax, by - ay);
        float t = Mathf.Clamp01(Vector2.Dot(p, d) / Mathf.Max(1e-4f, d.sqrMagnitude));
        return (p - d * t).magnitude;
    }

    // Negative inside the triangle (a, b, c), positive outside: the largest of the three edges' signed distances.
    static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float Edge(float px, float py, float qx, float qy)
        {
            float ex = qx - px, ey = qy - py;
            float length = Mathf.Max(1e-4f, Mathf.Sqrt(ex * ex + ey * ey));
            return ((x - px) * ey - (y - py) * ex) / length;
        }
        float e1 = Edge(ax, ay, bx, by), e2 = Edge(bx, by, cx, cy), e3 = Edge(cx, cy, ax, ay);
        bool clockwise = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax) < 0f;
        return clockwise ? Mathf.Max(e1, Mathf.Max(e2, e3)) : Mathf.Max(-e1, Mathf.Max(-e2, -e3));
    }

    // A white picture from a signed distance (negative inside), two by two samples a pixel so every edge is smooth.
    static Sprite Draw(string name, int size, System.Func<float, float, float> distance)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Bench cards - " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        a += Mathf.Clamp01(.5f - distance(x + .25f + sx * .5f, y + .25f + sy * .5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * .25f * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // ------------------------------------------------------------------ helpers

    // Past the end and back (a slide with weight), as the HUD's sign flips.
    static float BackOut(float t)
    {
        const float s = 1.4f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    static Rect ScreenRect(RectTransform rect)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);   // a screen-space overlay canvas: world is screen pixels
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}

