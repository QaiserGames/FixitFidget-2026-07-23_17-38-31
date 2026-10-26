using UnityEngine;

// Separate take and discard surfaces let the player pick up two empty cups.
// 24 Oct 2026 (Days; Designer P's restock by hand, §2.5): the cups come from the machine's own stack (ShopInventory.MachineCups),
// and with a crate from the back shelf in hand the take surface says "Refill the machine" and fills it back to 12 of each.
// Empty with stock on the shelf, it says "No cups at the machine" (the Now line says where they are).
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
            if (StockCrate.InHand(carry) != null) return RefillWords;
            return carry == null || !carry.HasSpace ? "Both hands full · return a spare cup at the return / discard tray"
                : ShopInventory.Instance == null || ShopInventory.Instance.MachineCups <= 0 ? NoCupsWords(ShopInventory.Instance) : "Take empty cup";
        }
    }

    /// <summary>With a crate from the back shelf in hand (24 Oct 2026). PLACEHOLDER COPY.</summary>
    public const string RefillWords = "Refill the machine";

    /// <summary>"No cups at the machine" while the back shelf has some (a refill brings them); "Out of cups" when it's empty too.</summary>
    public static string NoCupsWords(ShopInventory stock) => stock != null && stock.ShelfCups > 0 ? "No cups at the machine" : "Out of cups";

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
        // 24 Oct 2026 (Days): the crate from the back shelf fills the machine back to 12 of each.
        StockCrate crate = StockCrate.InHand(carry);
        if (crate != null)
        {
            stock.Refill();
            Destroy(crate.gameObject);
            Sfx.Play("cup.return", transform.position);
            Juice.Sparkle(transform.position + Vector3.up * .1f);
            return;
        }
        if (!carry.HasSpace || cupPrefab == null || cupPrefab.GetComponent<DrinkJob>() == null || !stock.TakeCup()) return;
        var made = Instantiate(cupPrefab).GetComponent<DrinkJob>();
        made.EnsureDispenserVisual();
        if (!carry.TryPickUp(made)) { Destroy(made.gameObject); stock.ReturnCup(); }
    }
}
