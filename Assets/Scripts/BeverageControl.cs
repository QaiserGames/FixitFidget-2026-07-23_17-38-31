using UnityEngine;

public sealed class BeverageControl : Interactable
{
    public BeverageSlot slot;
    public bool dispenseButton;
    public override bool IsAvailable => isActiveAndEnabled && slot != null;
    public override string Prompt
    {
        get
        {
            if (slot == null) return "";
            if (dispenseButton) return slot.PourPrompt;
            if (slot.drink == null) return "No drink assigned";
            if (slot.IsPouring) return $"Pouring {slot.drink.drinkName} · {Mathf.RoundToInt(slot.Progress * 100)}%";
            var carry = FindAnyObjectByType<PlayerCarry>();
            if (slot.Cup != null)
                return slot.Cup.IsEmpty ? slot.PourPrompt
                    : carry == null || !carry.HasSpace ? "Both hands full — C switches held item"
                    : $"Take {slot.drink.drinkName} ({slot.Cup.FreshnessStage.ToString().ToLowerInvariant()})";
            if (ShopInventory.Instance == null || !ShopInventory.Instance.CanBrew(slot.drink)) return "Out of ingredients";
            return carry != null && carry.Carried is DrinkJob cup && cup.IsEmpty
                ? $"Place cup and pour {slot.drink.drinkName}" : "Take a cup from the stack · C switches held item";
        }
    }
    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable || player == null) return;
        if (dispenseButton) slot.TryPour();
        else slot.PlaceAndPour(player.GetComponent<PlayerCarry>());
    }
    public override void SetFocused(bool focused) { if (slot != null) slot.SetFocused(focused); }
}
