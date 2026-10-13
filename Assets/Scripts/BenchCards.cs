using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE BENCH'S TWO CARDS (12 Oct 2026)
//
// Mansoor's playtest with a controller: "its impossible for me to understand how to use or whats what and how to do
// anything when doing these repairs. but rotating and just trying to flip the phone or even the pocket watch was near
// impossible." The close-up's only words were "[A] Pick up item and step back" and "[LB / RB] Hands [RT] Use [B] Put
// item down": nothing said the left stick turns the device or Y flips it. Agreed (claude/playtest-4-findings-and-
// proposals.md §1 and §5): a controls card, and the repair's checklist.
//
//   * THE CHECKLIST, top middle: whose device and what's wrong; the step to do now, with its tool and how; then every step
//     of the job in a row, done, now or to come (RepairChecklist). When the device is finished: pick it up and take it back.
//   * THE CONTROLS, bottom right: every control of the close-up in the device's own glyphs (the pad's or the mouse's). In
//     full for the first three close-ups (counted outside the labs, like a setting), then one line; holding Tab, or View on
//     a pad, shows it all again.
//
// THE SIMPLICITY PASS (12-13 Oct; his answer to 1b: "i just want it to feel easy for anyone to play it"): the pad's lines
// run in the order a stranger needs them (the hand, use, the tool, then turning and flipping, then the helpers, then the
// ways out); the turn says it is a quarter at a time; "Use" says it is a hold; the two sticks' names follow the swap
// setting (ControlHints.BenchHandStick / BenchTurnStick); and the checklist's step says "flip it over" when every part it
// wants lies on the face turned down (RepairChecklist.Step.under), instead of "hold on each one" for screws nobody can see.
// The mouse's card keeps its words (the Day 1 guide's check reads "[Click a tool]", "[Drag]" and "[Right-click]" from it).
//
// Made while playing, on a canvas of its own, between the HUD's corners and the pins: nothing is saved in the scene.
// ---------------------------------------------------------------------------
public sealed class BenchCards : MonoBehaviour
{
    const int SortingOrder = 20;          // over the HUD's corners (2), under the pins (30) and the circuit's board (80)
    const int SeenInFull = 3;             // close-ups shown with the whole controls card before it shrinks to one line
    const string SeenKey = "FixitFidget.BenchCards.CloseUpsSeen";
    const float StripWidth = 760f, CardWidth = 420f, Pad = 14f;

    public static BenchCards Instance { get; private set; }
    /// <summary>The controls card as it reads now (labs).</summary>
    public static string ControlsText => Instance != null && Instance.cardText != null ? Instance.cardText.text : "";
    /// <summary>The checklist as it reads now (labs).</summary>
    public static string ChecklistText => Instance != null && Instance.stripText != null ? Instance.stripText.text : "";
    /// <summary>The cards are on screen.</summary>
    public static bool Showing => Instance != null && Instance.group != null && Instance.group.alpha > .5f;
    /// <summary>The controls card is in full (not the one line).</summary>
    public static bool ControlsInFull => Instance != null && Instance.full;
    /// <summary>The job's steps as they stand (labs).</summary>
    public static IReadOnlyList<RepairChecklist.Step> Steps => Instance != null ? Instance.steps : null;
    /// <summary>The two cards on the screen, in pixels (labs: nothing may sit on the device).</summary>
    public static Rect StripOnScreen => Instance != null ? ScreenRect(Instance.strip) : default;
    public static Rect CardOnScreen => Instance != null ? ScreenRect(Instance.card) : default;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<BenchCards>() != null) return;
        var host = new GameObject("Bench cards (runtime)");
        DontDestroyOnLoad(host);
        host.AddComponent<BenchCards>();
    }

    Canvas canvas;
    CanvasGroup group;
    RectTransform strip, card;
    TMP_Text stripText, cardText;
    Image stripShadow, cardShadow;
    readonly List<RepairChecklist.Step> steps = new();
    JobBase shownFor;
    bool full;
    string lastStrip, lastCard;
    float nextRefresh;

    static readonly string Key = UiSkin.HexOf(UiSkin.BrandBright), Faint = UiSkin.HexOf(UiSkin.BandFaint),
        Gold = UiSkin.HexOf(UiSkin.Gold), Done = UiSkin.HexOf(UiSkin.BrandBright);

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        ItemInspector inspector = ItemInspector.Instance;
        JobBase job = inspector != null ? inspector.FocusedItem : null;
        bool want = job != null && Time.timeScale > 0f && !RecapUI.Showing;
        if (job != shownFor)
        {
            shownFor = job;
            if (job != null && !CafeLab.Active) PlayerPrefs.SetInt(SeenKey, PlayerPrefs.GetInt(SeenKey, 0) + 1);
            nextRefresh = 0f;
        }
        if (want && canvas == null) Build();
        if (canvas == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, want ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        canvas.enabled = group.alpha > .001f;
        if (!want) return;

        bool help = Keyboard.current != null && Keyboard.current.tabKey.isPressed || PadInput.Held(PadButton.Select);
        bool nowFull = CafeLab.Active || help || PlayerPrefs.GetInt(SeenKey, 0) <= SeenInFull;
        if (nowFull != full) { full = nowFull; nextRefresh = 0f; }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .15f;
        RepairChecklist.Build(job, steps);
        Show(stripText, strip, stripShadow, StripWidth, Checklist(inspector, job), ref lastStrip);
        Show(cardText, card, cardShadow, CardWidth, full ? Controls(inspector) : OneLine(), ref lastCard);
    }

    // ------------------------------------------------------------------ the words

    string Checklist(ItemInspector inspector, JobBase job)
    {
        var text = new StringBuilder();
        string who = job.Owner != null ? job.Owner.CustomerName : "";
        string device = job.Record != null ? job.Record.deviceName : "";
        string fault = job.JobCard;
        string title = (string.IsNullOrEmpty(who) ? "" : Possessive(who) + " ") + (device ?? "").ToLowerInvariant();
        if (!string.IsNullOrEmpty(fault)) title += " · " + fault;
        text.Append($"<size=72%><color={Faint}>{title.Trim()}</color></size>\n");

        RepairChecklist.Step now = RepairChecklist.Current(steps);
        if (now != null)
        {
            bool inHand = inspector.CurrentTool == now.tool;
            string tool = RepairChecklist.ToolName(now.tool);
            string change = inHand ? " in hand" : ControlHints.Pad ? $" ({ControlHints.Tools})" : now.tool == ToolType.Hand ? " (right-click the tool away)" : " (click it in the caddy)";
            // The step's parts lie on the face turned down (the phone's screws are in its back): say to flip it, not how to
            // hold on screws nobody can see (12 Oct: nothing had said when to flip). PLACEHOLDER COPY.
            string how = now.under ? $"on the other side: flip it over ({ControlHints.Flip})" : now.how;
            text.Append($"<color={Gold}>►</color> <b>{now.text}</b>\n");
            text.Append($"<size=80%><color={Key}>{tool}</color>{change} · {how}</size>");
        }
        else if (job.IsFinished)
            text.Append($"<color={Done}>Done.</color> <b>Pick it up and take it back</b>\n<size=80%><color={Key}>[{ControlHints.TakeFromBench}]</color> Pick it up and step back</size>");
        else
            text.Append("<b>Look it over</b>\n<size=80%>Turn it and look underneath: something here isn't right yet</size>");

        if (steps.Count > 1)
        {
            text.Append("\n<size=70%>");
            for (int i = 0; i < steps.Count; i++)
            {
                RepairChecklist.Step s = steps[i];
                if (i > 0) text.Append("    ");
                if (s.done) text.Append($"<color={Done}>●</color> <color={Faint}>{s.chip}</color>");
                else if (s.current) text.Append($"<color={Gold}>►</color> {s.chip}");
                else text.Append($"<color={Faint}>○ {s.chip}</color>");
            }
            text.Append("</size>");
        }
        return text.ToString();
    }

    static string K(string label) => $"<color={Key}>[{label}]</color>";

    // The pad's lines in the order a stranger needs them (the simplicity pass, 12-13 Oct): the hand and what it does, the
    // tool, then turning the device, then the helpers, then the two ways out. The sticks' names follow the swap setting.
    string Controls(ItemInspector inspector)
    {
        bool hand = inspector.CurrentTool == ToolType.Hand;
        string back = hand ? "Step back" : "Put the tool down";
        if (ControlHints.Pad)
            return $"{K(ControlHints.BenchHandStick)}  Move your hand\n"
                 + $"{K(ControlHints.BenchUse)}  Use (hold)\n"
                 + $"{K(ControlHints.Tools)}  Change tool <color={Faint}>({inspector.CurrentToolName})</color>\n"
                 + $"{K(ControlHints.BenchTurnStick)}  Turn it a quarter · up/down: peek under\n"
                 + $"{K(PadInput.Label(PadButton.North))}  Flip it over\n"
                 + $"{K("D-pad left/right")}  Next part\n"
                 + $"{K("D-pad up/down")}  Zoom\n"
                 + $"{K(ControlHints.Back)}  {back}\n"
                 + $"{K(ControlHints.TakeFromBench)}  Pick it up and step back";
        return $"{K("Click a tool")}  Take it <color={Faint}>(now: {inspector.CurrentToolName})</color>\n"
             + $"{K("Hold click")}  Use it\n"
             + $"{K("Drag")}  Turn it · up/down: peek under\n"
             + $"{K(ControlHints.Flip)}  Flip it over\n"
             + $"{K("Scroll")}  Zoom\n"
             + $"{K("Right-click")}  {back}\n"
             + $"{K(ControlHints.TakeFromBench)}  Pick it up and step back";
    }

    static string OneLine() => $"{K(ControlHints.BenchHelp)}  <color={Faint}>Hold for the controls</color>";

    static string Possessive(string name) => name.EndsWith("s") ? name + "'" : name + "'s";

    // ------------------------------------------------------------------ the cards

    void Show(TMP_Text text, RectTransform rect, Image shadow, float width, string words, ref string last)
    {
        if (words == last) return;
        last = words;
        text.text = words;
        float height = text.GetPreferredValues(words, width - Pad * 2f, 0f).y;
        rect.sizeDelta = new Vector2(width, Mathf.Ceil(height) + Pad * 1.6f);
        if (shadow != null) UiSkin.Follow(shadow.rectTransform, rect, 8f, 3f);
    }

    void Build()
    {
        canvas = UiSkin.ScreenCanvas("Bench cards (while playing)", transform, SortingOrder);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        strip = Plate("The checklist", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -18f), StripWidth, out stripShadow);
        stripText = Words(strip, 22f, TextAlignmentOptions.TopLeft);
        // Low in the corner, clear of the parts tray (the lab's photos, 12 Oct: at 150 px up it covered the tray's corner) and
        // beside the bottom prompt, which sits in the middle.
        card = Plate("The controls", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 96f), CardWidth, out cardShadow);
        cardText = Words(card, 18f, TextAlignmentOptions.TopLeft);
        cardText.lineSpacing = 2f;
    }

    RectTransform Plate(string name, Vector2 anchor, Vector2 pivot, Vector2 at, float width, out Image shadow)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = at;
        rect.sizeDelta = new Vector2(width, 60f);
        UiSkin.Paint(rect, UiSkin.Band, UiSkin.Radius);
        shadow = UiSkin.DropShadow(rect, 8f, 3f, .3f);
        return rect;
    }

    static TMP_Text Words(RectTransform plate, float size, TextAlignmentOptions align)
    {
        TextMeshProUGUI text = UiSkin.Text("Words", plate, size, UiSkin.BandText, align, FontStyles.Normal, true);
        RectTransform r = text.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(Pad, Pad * .8f);
        r.offsetMax = new Vector2(-Pad, -Pad * .8f);
        return text;
    }

    static Rect ScreenRect(RectTransform rect)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);   // a screen-space overlay canvas: world is screen pixels
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
