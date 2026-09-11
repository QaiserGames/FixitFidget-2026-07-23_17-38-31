using TMPro;
using UnityEngine;

// The machine stays a physical prop. Names and readiness are projected only
// while using this station; the player owns the separate hands HUD.
[RequireComponent(typeof(StationInteractable))]
public sealed class BeverageStation : MonoBehaviour
{
    private sealed class SectionLabel
    {
        public BeverageSlot slot;
        public RectTransform rect;
        public UnityEngine.UI.Image panel, accent, meter;
        public TMP_Text name, state;
    }

    private PlayerInteractor player;
    private PlayerCarry carry;
    private ConversationController dialogue;
    private StationInteractable station;
    private Camera view;
    private Canvas canvas;
    private UnityEngine.UI.CanvasScaler scaler;
    private TMP_Text title, action;
    private UnityEngine.UI.Image cardAccent;
    private SectionLabel[] sections;
    private readonly Color coldColor = new Color(.65f, .76f, .91f);

    private void Start()
    {
        station = GetComponent<StationInteractable>();
        player = FindAnyObjectByType<PlayerInteractor>();
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        dialogue = player != null ? player.GetComponent<ConversationController>() : null;
        view = Camera.main;
        canvas = RepairOverlayUI.Canvas("Drink station captions", transform, 12);
        scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();

        // Leave the guided opening toast above this card and ticket rail to its right.
        var card = RepairOverlayUI.Panel("Focused drink", canvas.transform,
            new Vector2(24, -148), new Vector2(306, 155), RepairOverlayUI.Background).rectTransform;
        cardAccent = RepairOverlayUI.Panel("Focus accent", card, Vector2.zero,
            new Vector2(4, 155), RepairOverlayUI.Mint);
        RepairOverlayUI.Text("Station heading", card, new Vector2(16, -10),
            new Vector2(274, 22), 15, RepairOverlayUI.Muted).text = "DRINK STATION";
        title = RepairOverlayUI.Text("Drink name", card, new Vector2(16, -33),
            new Vector2(274, 30), 24, Color.white);
        action = RepairOverlayUI.Text("Next action", card, new Vector2(16, -68),
            new Vector2(274, 49), 18, Color.white);
        action.textWrappingMode = TextWrappingModes.Normal;
        action.alignment = TextAlignmentOptions.TopLeft;
        RepairOverlayUI.Text("Station controls", card, new Vector2(16, -126),
            new Vector2(280, 22), 16, RepairOverlayUI.Muted).text = "Click / E · use    F · step back";

        var slots = GetComponentsInChildren<BeverageSlot>();
        sections = new SectionLabel[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var label = new SectionLabel { slot = slots[i] };
            label.panel = RepairOverlayUI.Panel("Drink label " + i, canvas.transform,
                Vector2.zero, new Vector2(112, 76), RepairOverlayUI.Background);
            label.rect = label.panel.rectTransform;
            label.rect.anchorMin = label.rect.anchorMax = Vector2.zero;
            label.rect.pivot = new Vector2(.5f, 1);
            label.accent = RepairOverlayUI.Panel("Selection accent", label.rect,
                Vector2.zero, new Vector2(112, 3), RepairOverlayUI.Muted);
            label.name = RepairOverlayUI.Text("Drink", label.rect, new Vector2(4, -5),
                new Vector2(104, 40), 17, Color.white);
            label.name.alignment = TextAlignmentOptions.Center;
            label.name.textWrappingMode = TextWrappingModes.Normal;
            label.name.text = slots[i].drink != null ? slots[i].drink.drinkName : "Unassigned";
            label.state = RepairOverlayUI.Text("Readiness", label.rect, new Vector2(3, -46),
                new Vector2(106, 21), 15, RepairOverlayUI.Muted);
            label.state.alignment = TextAlignmentOptions.Center;
            label.meter = RepairOverlayUI.Panel("Fill or freshness", label.rect,
                new Vector2(4, -72), new Vector2(104, 3), RepairOverlayUI.Mint);
            sections[i] = label;
        }
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (canvas == null) return;
        bool show = player != null && player.CurrentStation == station && Time.timeScale > 0
            && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation);
        canvas.gameObject.SetActive(show);
        if (!show) return;
        if (view == null) view = Camera.main;
        if (view == null) return;
        // Scale this overlay alone; established HUD canvases retain their own layout.
        scaler.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), .6f, 1.4f);
        float scale = scaler.scaleFactor;
        var focused = player.Focused;
        var control = focused != null ? focused.GetComponentInParent<BeverageControl>() : null;
        var supply = focused != null ? focused.GetComponentInParent<BeverageCupSupply>() : null;
        var focusedCup = focused != null ? focused.GetComponentInParent<DrinkJob>() : null;
        BeverageSlot selected = control != null ? control.slot : null;
        if (selected == null && focusedCup != null)
            foreach (var label in sections)
                if (label.slot.Cup == focusedCup) { selected = label.slot; break; }

        title.text = selected != null && selected.drink != null ? selected.drink.drinkName
            : supply != null ? supply.discard ? "Discard basin" : "Cup stack" : "Choose a drink";
        if (selected != null && selected.IsPouring)
            action.text = "Pouring " + Mathf.RoundToInt(selected.Progress * 100) + "% · you can prepare another cup.";
        else if (focused != null && (control != null || supply != null || selected != null))
            action.text = focused.Prompt;
        else if (carry != null && carry.Carried is DrinkJob cup && cup.IsEmpty)
            action.text = "Cup selected. Click a drink section to place it and pour.";
        else if (carry != null && !carry.HasSpace)
            action.text = "Both hands are occupied. C switches the selected item.";
        else action.text = "Take a cup from the stack, then click a drink section.";
        cardAccent.color = selected != null && selected.Cup != null && !selected.Cup.IsEmpty
            ? StateColor(selected.Cup) : RepairOverlayUI.Mint;

        // Size each badge to the actual spacing on screen, aligned with its nozzle.
        float spacing = 130 * scale;
        for (int i = 0; i < sections.Length; i++)
            for (int j = i + 1; j < sections.Length; j++)
                if (sections[i].slot.cupPoint != null && sections[j].slot.cupPoint != null)
                {
                    float distance = Mathf.Abs(view.WorldToScreenPoint(sections[i].slot.cupPoint.position).x
                        - view.WorldToScreenPoint(sections[j].slot.cupPoint.position).x);
                    if (distance > 1) spacing = Mathf.Min(spacing, distance);
                }
        float width = Mathf.Clamp(spacing / scale - 6, 82, 124);
        foreach (var label in sections)
        {
            BeverageSlot slot = label.slot;
            if (slot == null || slot.cupPoint == null) { label.rect.gameObject.SetActive(false); continue; }
            Vector3 screen = view.WorldToScreenPoint(slot.cupPoint.position + transform.TransformVector(0, -.025f, -.15f));
            label.rect.gameObject.SetActive(screen.z > 0);
            if (screen.z <= 0) continue;
            label.rect.sizeDelta = new Vector2(width, 76);
            label.rect.anchoredPosition = new Vector2(Mathf.Clamp(screen.x / scale, width / 2 + 8,
                Screen.width / scale - width / 2 - 8), Mathf.Max(174, screen.y / scale - 9));
            label.accent.rectTransform.sizeDelta = new Vector2(width, 3);
            label.name.rectTransform.sizeDelta = new Vector2(width - 8, 40);
            label.state.rectTransform.sizeDelta = new Vector2(width - 6, 21);
            label.panel.color = selected == slot ? new Color(.10f, .21f, .19f, .98f) : RepairOverlayUI.Background;
            label.accent.color = selected == slot ? RepairOverlayUI.Mint : new Color(.28f, .40f, .39f);
            DrinkJob readyCup = slot.Cup;
            float amount = 0;
            Color stateColor = RepairOverlayUI.Muted;
            if (slot.IsPouring)
            {
                label.state.text = "Pouring " + Mathf.RoundToInt(slot.Progress * 100) + "%";
                stateColor = RepairOverlayUI.Mint;
                amount = slot.Progress;
            }
            else if (readyCup != null && !readyCup.IsEmpty)
            {
                label.state.text = readyCup.FreshnessStage == DrinkFreshness.Stage.Cold ? "Cold · discard"
                    : readyCup.FreshnessStage == DrinkFreshness.Stage.Cooling ? "Cooling" : "Fresh · collect";
                stateColor = StateColor(readyCup);
                amount = readyCup.FreshnessRemaining;
            }
            else if (readyCup != null) label.state.text = "Cup ready";
            else label.state.text = slot.drink == null ? "Unavailable"
                : ShopInventory.Instance != null && ShopInventory.Instance.CanBrew(slot.drink) ? "Available" : "No ingredients";
            label.state.color = stateColor;
            label.meter.color = stateColor;
            label.meter.rectTransform.sizeDelta = new Vector2((width - 8) * Mathf.Clamp01(amount), 3);
            label.meter.gameObject.SetActive(amount > 0);
        }
    }

    private Color StateColor(DrinkJob cup) => cup.FreshnessStage == DrinkFreshness.Stage.Cold ? coldColor
        : cup.FreshnessStage == DrinkFreshness.Stage.Cooling ? RepairOverlayUI.Amber : RepairOverlayUI.Mint;

    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
}
