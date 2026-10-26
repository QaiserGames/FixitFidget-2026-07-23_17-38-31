using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// ---------------------------------------------------------------------------
// THE HOVER CARD AT THE BENCH (the scene's own card: its panel, its two texts and its group are wired in the scene; the look
// and the placing are set here while playing, as the skin dresses every scene text)
//
// What it says is the bench's (ItemInspector.HoverName / HoverAction, the counter's phone's when that is open): the part's
// name, and under it its own "why not" or the tool it needs.
//
// PLAYTEST 7 (19 Oct 2026; the fresh-eyes reviewers on the bench photos, and his girlfriend at the watch):
//   * "NEEDS THE TWEEZERS" ONLY WHEN THAT IS THE STEP (the reviewers: the card said "Mainspring · Needs the tweezers" while
//     the job card's step was "Brush": two cues disagreed, "she grabs the tweezers and gets refused"). A "Needs the ..." line
//     shows only when the tool it names is the current step's tool (BenchCards.CurrentStep); otherwise the name stands alone.
//   * ONE LINE TALL when the action is empty (the plate was always two lines, its lower half blank: "looks unfinished and
//     doubles the area the plate hides").
//   * ABOVE-LEFT OF THE CURSOR, and never over the device's screen rectangle or a screw cup (the plate "sits squarely on the
//     watch's back and hides its centre and one of the four screws"; "lands on the screw cups"). Four corners are tried in
//     turn (above-left, above-right, below-left, below-right); the first that covers neither the device nor a cup and stays
//     on the screen wins, else the one that covers least. The whole plate stays on the screen.
//   * READABLE: the name 26 px bold, the line under it 22 px bold (the rule of 19 Oct: nothing a player reads under 24 px
//     but a secondary label, and anything that instructs in bold).
// Sizes are reference pixels of the scene's canvas (1920 x 1080); the placing is done in screen pixels.
// ---------------------------------------------------------------------------
public class HoverTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private ItemInspector inspector;
    [SerializeField] private PlayerInteractor interactor;
    [Tooltip("How far the plate's nearest corner sits from the cursor's point, pixels (x sideways, y up or down); the scene's value.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(24f, -12f);

    [Tooltip("Hover must settle this long before the tooltip appears — stops strobing.")]
    [SerializeField] private float appearDelay = 0.12f;
    [SerializeField] private float fadeSpeed = 12f;

    // 26 Oct 2026 (Readability; the gather: "Under the back cover" 22 px at 2.5:1 in all three of its photos): the action 24 px,
    // and both lines carry the skin's shadow as well as the band, so they read wherever the plate's see-through band lands.
    const float NameSize = 26f, ActionSize = 24f, PadX = 14f, PadY = 8f, LineGap = 2f, EdgeKeep = 8f;
    const float CupRadius = .011f;   // a cup's rim, metres (9 mm cup + a little), for "never over a screw cup"

    private string lastTitle = "";
    private float hoverTime;
    private Canvas canvas;
    private Camera cam;

    // The UI skin (playtest 3, session 3): the tooltip is a card of the skin's band, its name in the skin's cream and
    // its action in the brand's light green, in the skin's lettering. Dressed once while playing; the scene keeps its own.
    private void Start()
    {
        if (panel != null)
        {
            Image card = panel.GetComponent<Image>();
            if (card != null)
            {
                UiSkin.Round(card, 10f);
                card.color = UiSkin.Band;
                card.raycastTarget = false;
            }
            // The plate's size and its texts' places are set here each frame (19 Oct): any layout group or fitter the
            // scene's card carries would fight that.
            foreach (LayoutGroup layout in panel.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
            foreach (ContentSizeFitter fitter in panel.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
        }
        if (nameText != null)
        {
            UiSkin.UseFont(nameText);
            nameText.color = UiSkin.BandText;
            nameText.fontStyle = FontStyles.Bold;
            nameText.fontSize = NameSize;
            Dress(nameText);
            UiSkin.Shadowed(nameText);   // 26 Oct 2026 (Readability)
        }
        if (actionText != null)
        {
            UiSkin.UseFont(actionText);
            actionText.color = UiSkin.BrandBright;
            actionText.fontStyle = FontStyles.Bold;
            actionText.fontSize = ActionSize;
            Dress(actionText);
            UiSkin.Shadowed(actionText);   // 26 Oct 2026 (Readability)
        }
        // Hidden from the first frame: the scene keeps the card's template words ("Name" / "Action") and a visible alpha,
        // and the fade below took a second to clear them off the floor of the first screen (13 Oct, the First Hour).
        HideImmediately();
    }

    // A text laid from the plate's top left, one line, never wrapping or catching the pointer.
    static void Dress(TMP_Text text)
    {
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        RectTransform r = text.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
    }

    private void Update()
    {
        // Update and raw mouse input still run at timeScale 0. A scaled fade
        // cannot clear yesterday's tooltip once the recap has paused the day.
        // The dispenser has its own card (BeverageStation), its close-up or its controls under the crosshair alike.
        if (Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.DayOver
            || interactor != null && interactor.UsingDrinks)
        {
            HideImmediately();
            return;
        }

        string title = "";
        string action = "";

        var counter = interactor != null ? interactor.GetComponent<CounterRepairView>() : null;
        if (counter != null && counter.IsOpen)
        {
            title = counter.HoverName;
            action = counter.HoverAction;
        }
        else if (inspector != null && inspector.IsHoldingItem && !inspector.Rubbing)
        {
            // Not mid-rub (15 Oct, Bench 8's photo of the lens wipe): the card's "Lens" sat over the very glass the cloth was
            // clearing, beside the cursor. It fades while the cloth or the brush is down on dirt and comes back when it lifts.
            title = inspector.HoverName;
            action = OnlyThisStepsTool(inspector.HoverAction);
        }
        // (Until playtest 3 a station's own camera showed "Customer" and the prompt here, which read "Customer / Pick
        // up" while choosing an item at the bench. Nothing is stepped up to now: the HUD's prompt line says it.)

        bool wantsShow = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(action);

        // Reset the settle timer whenever the target changes.
        if (title != lastTitle)
        {
            lastTitle = title;
            hoverTime = 0f;
        }

        hoverTime += Time.deltaTime;
        bool show = wantsShow && hoverTime >= appearDelay;

        // Fade rather than snap.
        float target = show ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
        group.blocksRaycasts = false;   // never intercept clicks

        if (group.alpha <= 0.01f) return;

        if (show)
        {
            nameText.text = title;
            if (actionText != null) actionText.text = action ?? "";
        }
        Lay();
    }

    /// <summary>
    /// A "Needs the ..." line only when the tool it names is the current step's (19 Oct): the mainspring's "Needs the tweezers"
    /// during the brush step sent her to the wrong tool. Any other line (a part's own "why not") passes as it is.
    /// </summary>
    public static string OnlyThisStepsTool(string action)
    {
        if (string.IsNullOrEmpty(action) || !action.StartsWith("Needs the ", System.StringComparison.Ordinal)) return action ?? "";
        RepairChecklist.Step step = BenchCards.CurrentStep;
        if (step == null || step.tool == ToolType.Hand) return action;
        string wanted = "Needs the " + RepairChecklist.ToolName(step.tool).ToLowerInvariant();
        return action == wanted ? action : "";
    }

    // The plate sized to its words (one line tall with no action), then placed at the cursor: above-left first, never over
    // the device or a screw cup when another corner is clear, always on the screen.
    private void Lay()
    {
        if (panel == null || nameText == null) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

        // The words' sizes in the canvas's units.
        string name = nameText.text ?? "", words = actionText != null ? actionText.text ?? "" : "";
        Vector2 nameSize = name.Length > 0 ? nameText.GetPreferredValues(name, 9999f, 9999f) : Vector2.zero;
        Vector2 actionSize = words.Length > 0 ? actionText.GetPreferredValues(words, 9999f, 9999f) : Vector2.zero;
        float nameH = Mathf.Max(NameSize * 1.2f, nameSize.y), actionH = words.Length > 0 ? Mathf.Max(ActionSize * 1.2f, actionSize.y) : 0f;
        float width = Mathf.Max(nameSize.x, actionSize.x) + 2f * PadX;
        float height = PadY + nameH + (words.Length > 0 ? LineGap + actionH : 0f) + PadY;
        panel.sizeDelta = new Vector2(Mathf.Ceil(width), Mathf.Ceil(height));
        RectTransform nr = nameText.rectTransform;
        nr.anchoredPosition = new Vector2(PadX, -PadY);
        nr.sizeDelta = new Vector2(Mathf.Ceil(nameSize.x) + 4f, nameH);
        if (actionText != null)
        {
            if (actionText.gameObject.activeSelf != words.Length > 0) actionText.gameObject.SetActive(words.Length > 0);
            RectTransform ar = actionText.rectTransform;
            ar.anchoredPosition = new Vector2(PadX, -PadY - nameH - LineGap);
            ar.sizeDelta = new Vector2(Mathf.Ceil(actionSize.x) + 4f, actionH);
        }

        // Follows the controller's on-screen cursor too (see GamePointer).
        Vector2 cursor = GamePointer.PadCursorActive ? GamePointer.Position
            : Cursor.visible && Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f - 60f);

        // The four corners, in order of preference: above-left, above-right, below-left, below-right. Each is where the
        // plate's corner nearest the cursor goes, and the pivot that puts the plate on that side.
        Vector2 size = panel.sizeDelta * scale;
        float dx = Mathf.Abs(cursorOffset.x), dy = Mathf.Abs(cursorOffset.y);
        Rect device = DeviceOnScreen();
        BenchStage stage = BenchStage.Instance;
        Vector2 bestPivot = new Vector2(1f, 0f), bestAt = cursor + new Vector2(-dx, dy);
        float least = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector2 pivot = new Vector2((i & 1) == 0 ? 1f : 0f, (i & 2) == 0 ? 0f : 1f);
            Vector2 at = cursor + new Vector2((i & 1) == 0 ? -dx : dx, (i & 2) == 0 ? dy : -dy);
            at = OnScreen(at, pivot, size);
            Rect plate = new Rect(at.x - pivot.x * size.x, at.y - pivot.y * size.y, size.x, size.y);
            float covered = Covered(plate, device, stage);
            if (covered < least) { least = covered; bestPivot = pivot; bestAt = at; }
            if (covered <= 0f) break;
        }
        panel.pivot = bestPivot;
        panel.position = bestAt;
    }

    // The plate's anchor moved so the whole plate stays on the screen (a margin in from each edge).
    static Vector2 OnScreen(Vector2 at, Vector2 pivot, Vector2 size)
    {
        float left = at.x - pivot.x * size.x, bottom = at.y - pivot.y * size.y;
        float minX = EdgeKeep, maxX = Mathf.Max(minX, Screen.width - EdgeKeep - size.x);
        float minY = EdgeKeep, maxY = Mathf.Max(minY, Screen.height - EdgeKeep - size.y);
        left = Mathf.Clamp(left, minX, maxX);
        bottom = Mathf.Clamp(bottom, minY, maxY);
        return new Vector2(left + pivot.x * size.x, bottom + pivot.y * size.y);
    }

    // How much of the plate lies over what it must not cover: the device's screen rectangle and the screw cups (area, px²).
    float Covered(Rect plate, Rect device, BenchStage stage)
    {
        float covered = 0f;
        if (device.width > 0f && plate.Overlaps(device))
        {
            float w = Mathf.Min(plate.xMax, device.xMax) - Mathf.Max(plate.xMin, device.xMin);
            float h = Mathf.Min(plate.yMax, device.yMax) - Mathf.Max(plate.yMin, device.yMin);
            covered += Mathf.Max(0f, w) * Mathf.Max(0f, h);
        }
        if (stage != null && stage.cups != null && cam != null)
            foreach (Transform cup in stage.cups)
            {
                if (cup == null) continue;
                Vector3 c = cam.WorldToScreenPoint(cup.position);
                if (c.z <= 0f) continue;
                Vector3 edge = cam.WorldToScreenPoint(cup.position + cam.transform.right * CupRadius);
                float r = Mathf.Max(6f, ((Vector2)edge - (Vector2)c).magnitude);
                // The nearest point of the plate to the cup's middle, against its rim.
                float nx = Mathf.Clamp(c.x, plate.xMin, plate.xMax), ny = Mathf.Clamp(c.y, plate.yMin, plate.yMax);
                float d = new Vector2(c.x - nx, c.y - ny).magnitude;
                if (d < r) covered += (r - d) * r;
            }
        return covered;
    }

    // The device's drawn box on the screen (its own solid parts: DeviceBounds), or an empty rect without one.
    Rect DeviceOnScreen()
    {
        JobBase job = inspector != null ? inspector.FocusedItem : null;
        if (job == null || cam == null || !DeviceBounds.TryGet(job.gameObject, out Bounds b)) return default;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 p = cam.WorldToScreenPoint(corner);
            if (p.z <= 0f) return default;
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    /// <summary>The plate's rectangle on the screen now (pixels, origin bottom-left), for the labs; empty while it is hidden.</summary>
    public Rect PlateOnScreen
    {
        get
        {
            if (panel == null || group == null || group.alpha <= .01f) return default;
            var c = new Vector3[4];
            panel.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }

    public void HideImmediately()
    {
        lastTitle = "";
        hoverTime = 0f;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        if (nameText != null) nameText.text = "";
        if (actionText != null) actionText.text = "";
    }
}
