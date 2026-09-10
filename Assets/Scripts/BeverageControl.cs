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
            if (slot.IsPouring) return "Filling — wait for the pour";
            var carry = FindAnyObjectByType<PlayerCarry>();
            if (slot.Cup != null)
                return carry == null || !carry.HasSpace ? "Both hands full — C switches held item"
                    : slot.Cup.IsEmpty ? "Take empty cup"
                    : $"Take {slot.drink.drinkName} ({slot.Cup.FreshnessStage.ToString().ToLowerInvariant()})";
            return carry != null && carry.Carried is DrinkJob cup && cup.IsEmpty
                ? $"Place cup under {slot.drink.drinkName}" : "Select an empty cup — C switches held item";
        }
    }
    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        if (dispenseButton) slot.TryPour();
        else slot.TransferCup(player.GetComponent<PlayerCarry>());
    }
}
