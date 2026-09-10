using UnityEngine;

// A physical section owns its cup and pour, never an order or a customer.
public sealed class BeverageSlot : MonoBehaviour
{
    public DrinkDefinition drink;
    public Transform cupPoint;
    public LineRenderer stream;
    private DrinkJob cup;
    private float pourLeft;
    private bool pouring;
    private bool caughtPour;
    public bool IsPouring => pouring;
    public void Release(DrinkJob item) { if (cup == item && !pouring) cup = null; }
    public DrinkJob Cup
    {
        get
        {
            // Pick-up can happen via the cup's own ItemInteractable as well as the pad.
            if (cup != null)
            {
                var carry = FindAnyObjectByType<PlayerCarry>();
                if (carry != null && carry.Contains(cup)) cup = null;
            }
            return cup;
        }
    }
    public string PourPrompt => pouring ? $"Pouring {drink.drinkName}…"
        : drink == null ? "No drink assigned"
        : Cup != null && !Cup.IsEmpty ? "Lift the filled cup first"
        : ShopInventory.Instance == null || !ShopInventory.Instance.CanBrew(drink) ? "Out of ingredients"
        : Cup == null ? $"Dispense {drink.drinkName} — no cup; ingredients will be wasted"
        : $"Dispense {drink.drinkName}";
    public bool TryPour()
    {
        if (pouring || drink == null || (DayClock.Instance != null && DayClock.Instance.DayOver)
            || Time.timeScale <= 0 || Cup != null && !Cup.IsEmpty) return false;
        if (ShopInventory.Instance == null || !ShopInventory.Instance.ConsumeBeans(drink)) return false;
        // Debit exactly once at the button press, even if there is no cup.
        caughtPour = Cup != null;
        if (caughtPour) cup.Locked = true;
        float multiplier = UpgradeManager.Instance != null ? UpgradeManager.Instance.BrewTimeMultiplier : 1;
        pourLeft = Mathf.Max(.1f, drink.brewSeconds * multiplier);
        pouring = true;
        return true;
    }
    public bool TransferCup(PlayerCarry carry)
    {
        if (carry == null || pouring || cupPoint == null || Time.timeScale <= 0
            || DayClock.Instance != null && DayClock.Instance.DayOver) return false;
        if (Cup != null)
        {
            if (!carry.TryPickUp(cup)) return false;
            cup = null; return true;
        }
        if (!(carry.Carried is DrinkJob held) || !held.IsEmpty || held.Locked) return false;
        carry.PlaceAt(cupPoint);
        if (carry.Contains(held)) return false;
        cup = held; cup.SetOwner(null); return true;
    }
    private void Update()
    {
        if (stream != null) stream.enabled = pouring;
        if (!pouring || DayClock.Instance != null && DayClock.Instance.DayOver) return;
        pourLeft -= Time.deltaTime;
        if (pourLeft > 0) return;
        pouring = false;
        // A lost/destroyed cup does not spawn a replacement or refund stock.
        if (caughtPour && cup != null) { cup.SetDrink(drink, true); cup.Locked = false; }
        caughtPour = false;
    }
    private void OnDisable()
    {
        // Interrupting a station wastes spent ingredients, but never strands a locked cup.
        pouring = false; caughtPour = false;
        if (cup != null) cup.Locked = false;
        if (stream != null) stream.enabled = false;
    }
}
