using UnityEngine;

// Separate take and discard surfaces let the player pick up two empty cups.
// 19 Oct 2026: the hands' buttons are icons (ControlHints), and the take surface can glow while the step is "take a cup"
// (BeverageStation asks; CupStack.Glow does the work): white cups on a white counter were the least visible thing in the
// frame.
public sealed class BeverageCupSupply : Interactable
{
    public GameObject cupPrefab;
    public bool discard;
    public override string Prompt
    {
        get
        {
            var carry = PlayerCarry.Current;
            if (discard)
            {
                var cup = carry != null ? carry.Carried as DrinkJob : null;
                if (cup == null) return $"Return or discard a cup · {ControlHints.LeftHand} / {ControlHints.RightHand} picks the hand";
                return cup.IsEmpty ? "Return unused cup to stock"
                    : $"Discard {cup.Drink.drinkName} — cup and ingredients are lost";
            }
            return carry == null || !carry.HasSpace ? "Both hands full · return a spare cup at the return / discard tray"
                : ShopInventory.Instance == null || ShopInventory.Instance.Cups <= 0 ? "Out of cups" : "Take empty cup";
        }
    }

    /// <summary>The stack's glow, on while the step is "take a cup" (called every frame by BeverageStation; it breathes).</summary>
    public void SetGlow(bool on) => CupStack.Glow.Set(this, on);

    private void OnDisable() => CupStack.Glow.Set(this, false);

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
