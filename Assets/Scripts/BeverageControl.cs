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
                return slot.Cup.IsEmpty ? "Take empty cup · aim at the named paddle to pour"
                    : carry == null || !carry.HasSpace ? "Both hands full · set down or deliver an item"
                    : $"Take {slot.drink.drinkName} ({slot.Cup.FreshnessStage.ToString().ToLowerInvariant()})";
            bool hasEmpty = carry != null && ((carry.GetHandItem(0) is DrinkJob left && left.IsEmpty)
                || (carry.GetHandItem(1) is DrinkJob right && right.IsEmpty));
            return hasEmpty ? $"Place cup under {slot.drink.drinkName}"
                : "Take a cup · left / right click chooses that hand";
        }
    }
    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable || player == null) return;
        if (dispenseButton) slot.TryPour();
        else slot.TransferCup(player.GetComponent<PlayerCarry>());
    }
    public override void SetFocused(bool focused) { if (slot != null) slot.SetFocused(focused); }
}
