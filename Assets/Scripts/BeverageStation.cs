using TMPro;
using UnityEngine;

// Enter once, prepare several drinks, then leave with up to two items.
[RequireComponent(typeof(StationInteractable))]
public sealed class BeverageStation : MonoBehaviour
{
    private PlayerInteractor player;
    private PlayerCarry carry;
    private GameObject hud;
    private TMP_Text heldText;
    private void Start()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        if (carry != null) carry.SetCapacity(2);
        var canvas = RepairOverlayUI.Canvas("Hands", transform, 12);
        hud = canvas.gameObject;
        heldText = RepairOverlayUI.Text("Held item", canvas.transform, Vector2.zero, new Vector2(520, 30), 18, Color.white);
        heldText.rectTransform.anchorMin = heldText.rectTransform.anchorMax = new Vector2(.5f, 0);
        heldText.rectTransform.pivot = new Vector2(.5f, 0);
        heldText.rectTransform.anchoredPosition = new Vector2(0, 10);
        heldText.alignment = TextAlignmentOptions.Center;
    }
    private void LateUpdate()
    {
        if (hud == null) return;
        bool show = player != null && (DayClock.Instance == null || !DayClock.Instance.DayOver)
            && (player.GetComponent<ConversationController>() == null || !player.GetComponent<ConversationController>().InConversation);
        hud.SetActive(show);
        if (!show) return;
        bool atStation = player.CurrentStation == GetComponent<StationInteractable>();
        JobBase item = carry != null ? carry.Carried : null;
        string label = item is DrinkJob cup ? cup.IsEmpty ? "Empty cup" : cup.Drink.drinkName + " · " + cup.FreshnessStage.ToString().ToLowerInvariant()
            : item != null && item.Record != null ? item.Record.Subject : "Empty hands";
        heldText.text = label + (carry != null && carry.Count > 1 ? "  ·  C: switch item" : "")
            + (atStation ? "  ·  Point + E: use  ·  F: step back" : "");
    }
}
