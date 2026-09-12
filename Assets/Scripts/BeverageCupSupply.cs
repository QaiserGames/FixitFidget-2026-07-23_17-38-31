using UnityEngine;

// Separate take and discard surfaces let the player pick up two empty cups.
public sealed class BeverageCupSupply : Interactable
{
    public GameObject cupPrefab;
    public bool discard;
    public override string Prompt
    {
        get
        {
            var carry = FindAnyObjectByType<PlayerCarry>();
            if (discard)
            {
                var cup = carry != null ? carry.Carried as DrinkJob : null;
                if (cup == null) return "Return or discard a cup · click its hand";
                return cup.IsEmpty ? "Return unused cup to stock"
                    : $"Discard {cup.Drink.drinkName} — cup and ingredients are lost";
            }
            return carry == null || !carry.HasSpace ? "Both hands full · place a cup first"
                : ShopInventory.Instance == null || ShopInventory.Instance.Cups <= 0 ? "Out of cups" : "Take empty cup";
        }
    }
    public override void Interact(PlayerInteractor player)
    {
        if (Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        var carry = player.GetComponent<PlayerCarry>();
        var stock = ShopInventory.Instance;
        if (carry == null || stock == null) return;
        if (discard)
        {
            if (!(carry.Carried is DrinkJob cup) || cup.Locked) return;
            if (cup.IsEmpty) stock.ReturnCup();
            carry.Consume(); return;
        }
        if (!carry.HasSpace || cupPrefab == null || cupPrefab.GetComponent<DrinkJob>() == null || !stock.TakeCup()) return;
        var made = Instantiate(cupPrefab).GetComponent<DrinkJob>();
        made.EnsureDispenserVisual();
        if (!carry.TryPickUp(made)) { Destroy(made.gameObject); stock.ReturnCup(); }
    }
}
