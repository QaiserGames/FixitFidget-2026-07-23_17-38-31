using TMPro;
using UnityEngine;

// The six recipes are printed on the machine. This small caption describes only
// the current action; physical hands and the centred reticle carry the interaction.
[RequireComponent(typeof(StationInteractable))]
public sealed class BeverageStation : MonoBehaviour
{
    private PlayerInteractor player;
    private PlayerCarry carry;
    private ConversationController dialogue;
    private StationInteractable station;
    private Canvas canvas;
    private UnityEngine.UI.CanvasScaler scaler;
    private TMP_Text title, action, controls;
    private BeverageSlot[] slots;

    private void Start()
    {
        station = GetComponent<StationInteractable>();
        player = FindAnyObjectByType<PlayerInteractor>();
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        dialogue = player != null ? player.GetComponent<ConversationController>() : null;
        slots = GetComponentsInChildren<BeverageSlot>();
        canvas = RepairOverlayUI.Canvas("Drink station action", transform, 12);
        scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        var card = RepairOverlayUI.Panel("Current action", canvas.transform, Vector2.zero,
            new Vector2(526, 94), new Color(.045f, .065f, .065f, .83f)).rectTransform;
        card.anchorMin = card.anchorMax = new Vector2(.5f, 0);
        card.pivot = new Vector2(.5f, 0);
        card.anchoredPosition = new Vector2(0, 20);
        title = RepairOverlayUI.Text("Focused object", card, new Vector2(12, -5),
            new Vector2(502, 25), 20, Color.white);
        title.alignment = TextAlignmentOptions.Center;
        action = RepairOverlayUI.Text("Next action", card, new Vector2(12, -31),
            new Vector2(502, 31), 17, Color.white);
        action.alignment = TextAlignmentOptions.Center;
        action.textWrappingMode = TextWrappingModes.NoWrap;
        controls = RepairOverlayUI.Text("Controls", card, new Vector2(12, -66),
            new Vector2(502, 22), 15, RepairOverlayUI.Muted);
        controls.text = ControlsLine(true);
        controls.alignment = TextAlignmentOptions.Center;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    // In first person the card shows while the crosshair is on the dispenser (stations as reach, playtest 3), and
    // stays a moment after it slips off, so it doesn't flicker as the crosshair crosses the gaps between controls.
    private const float Linger = .5f;
    private float aimedAt = -10f;

    private void Refresh()
    {
        if (canvas == null) return;
        if (player != null && player.AimingAtDrinks) aimedAt = Time.unscaledTime;
        bool here = player != null && (player.CurrentStation == station
            || player.CurrentStation == null && Time.unscaledTime - aimedAt < Linger);
        bool show = here && Time.timeScale > 0
            && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (dialogue == null || !dialogue.InConversation);
        canvas.gameObject.SetActive(show);
        if (!show) return;
        scaler.scaleFactor = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), .6f, 1.4f);
        string line = ControlsLine(player.CurrentStation == station);
        if (controls.text != line) controls.text = line;
        var focused = player.Focused;
        var control = focused != null ? focused.GetComponentInParent<BeverageControl>() : null;
        var supply = focused != null ? focused.GetComponentInParent<BeverageCupSupply>() : null;
        var focusedCup = focused != null ? focused.GetComponentInParent<DrinkJob>() : null;
        BeverageSlot selected = control != null ? control.slot : null;
        if (selected == null && focusedCup != null)
            foreach (var slot in slots)
                if (slot.Cup == focusedCup) { selected = slot; break; }

        title.text = selected != null && selected.drink != null ? selected.drink.drinkName
            : supply != null ? supply.discard ? "Cup return / discard" : "Cup stack" : "Drink station";
        if (selected != null && selected.IsPouring)
            action.text = "Pouring " + Mathf.RoundToInt(selected.Progress * 100) + "% · prepare another cup while it fills";
        else if (focused != null && (control != null || supply != null || selected != null))
            action.text = focused.Prompt;
        else if (carry != null && carry.Carried is DrinkJob cup && cup.IsEmpty)
            action.text = "Place a cup below a nozzle, then press its named paddle";
        else if (carry != null && !carry.HasSpace)
            action.text = "Both hands occupied · serve a drink or set an item down";
        else action.text = "Aim at the cup stack to take a cup";
    }

    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }

    // The close-up is left by walking away (or B / Esc); in first person there is nothing to leave.
    private static string ControlsLine(bool closeUp) => PadInput.UsingPad
        ? $"{ControlHints.LeftHand} · left hand   {ControlHints.RightHand} · right hand   {ControlHints.Interact} · use"
          + (closeUp ? $"   {ControlHints.Back} or walk · step back" : "")
        : "Left click · left hand   Right click · right hand   E · use" + (closeUp ? "   Esc or walk · step back" : "");
}
