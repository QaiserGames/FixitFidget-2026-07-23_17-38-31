using UnityEngine;

// 19 Oct 2026: no percentage while pouring (the station's card shows a ring that fills instead; BeverageStation), and the
// hands' buttons are icons (ControlHints), never words in brackets.
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
            if (slot.IsPouring) return $"Pouring {slot.drink.drinkName}";
            var carry = PlayerCarry.Current;
            if (slot.Cup != null)
                return slot.Cup.IsEmpty ? "Take empty cup · aim at the drink's button to pour"   // 26 Oct 2026 (Drink buttons): the buttons carry pictures, not names
                    : carry == null || !carry.HasSpace ? "Both hands full · set down or deliver an item"
                    : $"Take {slot.drink.drinkName} ({slot.Cup.FreshnessStage.ToString().ToLowerInvariant()})";
            bool hasEmpty = carry != null && ((carry.GetHandItem(0) is DrinkJob left && left.IsEmpty)
                || (carry.GetHandItem(1) is DrinkJob right && right.IsEmpty));
            return hasEmpty ? $"Place cup under {slot.drink.drinkName}"
                : $"Take a cup · {ControlHints.LeftHand} / {ControlHints.RightHand} chooses that hand";
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
