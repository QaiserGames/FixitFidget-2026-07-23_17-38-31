using UnityEngine;

// ---------------------------------------------------------------------------
// THE BACK SHELF: RESTOCK BY HAND (24 Oct 2026; Designer P's spec §2.5 and §6.6, one of the four things to do while waiting he
// chose)
//
// The café's stock beyond the drinks machine's 12 cups and 12 shots of beans sits here, on the back bar by the cup station
// (Fixit Fidget > Days > Props 1 places it, 2.9 m from the machine, on the staff side): what the Shop's restock adds to. When
// the machine is at 3 or fewer, the Now line says "Low on cups. Fetch more from the back shelf." (Downtime: a refill is first in
// the waiting jobs' order: it saves a stall later). E here: "Take cups and beans", a crate in one hand (StockCrate); at the
// machine's cup stack, "Refill the machine" fills it back to 12 of each. With a crate in hand, E here puts it back. Two full
// hands get the existing "Hands full". Talking at the counter needs no hand, and a device can still be picked up with the
// other one, so it never blocks a customer.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class BackShelf : Interactable
{
    public static BackShelf Instance { get; private set; }

    /// <summary>What E says here (PLACEHOLDER COPY).</summary>
    public const string TakeWords = "Take cups and beans", PutBackWords = "Put the crate back", EmptyWords = "The back shelf is empty";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake() => Instance = this;

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public override bool IsAvailable
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (clock != null && clock.DayOver || NightWalk.Instance != null && NightWalk.Instance.Active) return false;
            ShopInventory stock = ShopInventory.Instance;
            PlayerCarry carry = PlayerCarry.Current;
            if (stock == null || carry == null) return false;
            if (StockCrate.InHand(carry) != null) return true;               // put it back
            return carry.HasSpace && stock.CanRefill;                        // there's something to take to the machine
        }
    }

    public override string Prompt
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;
            ShopInventory stock = ShopInventory.Instance;
            if (StockCrate.InHand(carry) != null) return PutBackWords;
            if (carry != null && !carry.HasSpace) return "Hands full";
            if (stock != null && stock.ShelfCups <= 0 && stock.ShelfBeans <= 0) return EmptyWords;
            return TakeWords;
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;
        StockCrate held = StockCrate.InHand(carry);
        if (held != null)
        {
            // One press puts it back: the stock never left the shelf.
            Destroy(held.gameObject);
            Sfx.Play("item.putdown", transform.position);
            return;
        }
        if (!carry.HasSpace) return;
        Renderer look = GetComponentInChildren<MeshRenderer>();
        Renderer lid = transform.Find("Cup sleeve 1") != null ? transform.Find("Cup sleeve 1").GetComponent<MeshRenderer>() : null;
        StockCrate crate = StockCrate.Make(transform.position + Vector3.up * .45f, transform.rotation,
            look != null ? look.sharedMaterial : null, lid != null ? lid.sharedMaterial : null);
        if (!carry.TryPickUp(crate)) { Destroy(crate.gameObject); return; }
        Sfx.Play("item.pickup", transform.position);
    }
}
