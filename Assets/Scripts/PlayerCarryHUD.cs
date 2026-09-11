using TMPro;
using UnityEngine;

// Shared carrying feedback belongs to the player, including scenes without a dispenser.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCarry))]
public sealed class PlayerCarryHUD : MonoBehaviour
{
    private PlayerCarry carry;
    private ConversationController dialogue;
    private ItemInspector inspector;
    private CounterRepairView counterRepair;
    private Canvas canvas;
    private UnityEngine.UI.CanvasScaler scaler;
    private TMP_Text title, hint;
    private readonly TMP_Text[] labels = new TMP_Text[2];
    private readonly TMP_Text[] states = new TMP_Text[2];
    private readonly UnityEngine.UI.Image[] rows = new UnityEngine.UI.Image[2];
    private readonly UnityEngine.UI.Image[] selection = new UnityEngine.UI.Image[2];

    private void Start()
    {
        carry = GetComponent<PlayerCarry>();
        dialogue = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        canvas = RepairOverlayUI.Canvas("Carried items", transform, 14);
        scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        var panel = RepairOverlayUI.Panel("Hands panel", canvas.transform, Vector2.zero,
            new Vector2(328, 178), RepairOverlayUI.Background).rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(1, 0);
        panel.pivot = new Vector2(1, 0); panel.anchoredPosition = new Vector2(-18, 18);
        title = RepairOverlayUI.Text("Hands count", panel, new Vector2(14, -7), new Vector2(300, 25), 20, Color.white);
        for (int i = 0; i < 2; i++)
        {
            rows[i] = RepairOverlayUI.Panel("Hand " + (i + 1), panel,
                new Vector2(10, -37 - i * 54), new Vector2(308, 49), new Color(.11f, .14f, .15f));
            selection[i] = RepairOverlayUI.Panel("Selected marker", rows[i].transform,
                Vector2.zero, new Vector2(4, 49), RepairOverlayUI.Mint);
            labels[i] = RepairOverlayUI.Text("Item", rows[i].transform,
                new Vector2(12, -1), new Vector2(284, 25), 20, Color.white);
            states[i] = RepairOverlayUI.Text("State", rows[i].transform,
                new Vector2(12, -25), new Vector2(284, 22), 18, RepairOverlayUI.Muted);
        }
        hint = RepairOverlayUI.Text("Switch hint", panel, new Vector2(14, -148), new Vector2(300, 24), 18, RepairOverlayUI.Muted);
        Refresh();
    }

    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (canvas == null || carry == null) return;
        bool visible = Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation) && (inspector == null || !inspector.IsHoldingItem)
            && (counterRepair == null || !counterRepair.OwnsInput);
        canvas.gameObject.SetActive(visible);
        if (!visible) return;
        // Scale only this new overlay; established ticket and repair canvases keep their settings.
        scaler.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), .8f, 1.35f);
        title.text = "HANDS  " + carry.Count + " / " + carry.Capacity;
        for (int i = 0; i < 2; i++)
        {
            rows[i].gameObject.SetActive(i < carry.Capacity);
            JobBase item = carry.GetItem(i);
            bool selected = item != null && i == carry.SelectedIndex;
            selection[i].gameObject.SetActive(selected);
            rows[i].color = selected ? new Color(.12f, .24f, .22f) : new Color(.11f, .14f, .15f);
            labels[i].text = (i + 1) + "  " + ItemLabel(item);
            labels[i].color = item != null ? Color.white : RepairOverlayUI.Muted;
            states[i].text = item == null ? "Available" : (selected ? "Selected" : "Carrying") + " · " + ItemState(item);
            states[i].color = selected ? RepairOverlayUI.Mint : RepairOverlayUI.Muted;
        }
        hint.text = carry.Count > 1 ? "C · switch selected item" : carry.Count == 0 ? "Pick up a cup or device"
            : carry.HasSpace ? "One shared slot still available" : "Set down the selected item";
    }

    private static string ItemLabel(JobBase item)
    {
        if (item == null) return "Empty hand";
        if (item is DrinkJob drink) return drink.IsEmpty ? "Empty cup" : drink.Drink.drinkName;
        if (item.Record != null && !string.IsNullOrWhiteSpace(item.Record.Subject)) return item.Record.Subject;
        return item.name.Replace("(Clone)", "").Trim();
    }
    private static string ItemState(JobBase item)
    {
        if (item is DrinkJob drink)
        {
            if (drink.IsEmpty) return "ready to place";
            if (drink.FreshnessStage == DrinkFreshness.Stage.Cold) return "cold — discard";
            return drink.FreshnessStage == DrinkFreshness.Stage.Cooling ? "cooling" : "fresh";
        }
        return "ready to set down";
    }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
}
