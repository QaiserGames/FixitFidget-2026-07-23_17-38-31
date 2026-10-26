using UnityEngine;

// The café's cups and beans.
//
// RESTOCK BY HAND (24 Oct 2026; Designer P's spec §2.5 and §6.6, one of the four things to do while waiting he chose: "restocking
// by hand"). The stock is in two places now: the drinks machine holds 12 cups and 12 shots of beans (what every drink takes from:
// TakeCup, CanBrew, ConsumeBeans), and the rest waits on the back shelf (what the Shop's restock adds to, "+20 each, on
// the back shelf"). When the machine is at 3 or fewer and the shelf has stock, a refill is something to do while waiting
// (Downtime, the Now line: "Low on cups. Fetch more from the back shelf."): a crate from the back shelf (BackShelf) carried to
// the cup stack fills the machine back to 12 of each (Refill). What's left in the machine stays overnight: no free morning
// refill. The start is the old 20 of each: 12 in the machine, 8 on the shelf.
//
// Cups and Beans stay the café's WHOLE stock (the machine and the shelf together), so the recap's Shop, the HUD's stock chips,
// the save and the labs read what they always read; MachineCups / MachineBeans are what's in the machine, ShelfCups / ShelfBeans
// what's on the shelf. CupsLow / BeansLow stay the whole stock's "low" (the recap's "Low on cups. Restock in Shop."); MachineLow
// is the machine's. The save keeps the whole stock in cups / beans as before, plus the machine's share (SaveData.machineCups,
// additive: an older save fills the machine from its stock at load, ApplyTheSavedSplit). CanMake (can this order be taken, or
// must Ace apologise) reads the whole stock: a refill brings the shelf's to the machine.
public class ShopInventory : MonoBehaviour
{
    public static ShopInventory Instance { get; private set; }

    [SerializeField] private int cups = 20;
    [SerializeField] private int beans = 20;

    [Header("Restocking")]
    [SerializeField] private int restockAmount = 20;
    [SerializeField] private int restockCost = 30;

    /// <summary>What the drinks machine holds when it's full, cups and shots of beans (§2.5).</summary>
    public const int MachineHolds = 12;

    // The machine's share and the shelf's (24 Oct 2026). cups / beans above are the starting whole stock (the Inspector's).
    private int machineCups, machineBeans, shelfCups, shelfBeans;
    private bool split;

    /// <summary>The café's whole stock: the machine and the back shelf together (the Shop, the HUD's chips, the save).</summary>
    public int Cups { get { Split(); return machineCups + shelfCups; } }
    public int Beans { get { Split(); return machineBeans + shelfBeans; } }
    /// <summary>What's in the drinks machine: what drinks take from.</summary>
    public int MachineCups { get { Split(); return machineCups; } }
    public int MachineBeans { get { Split(); return machineBeans; } }
    /// <summary>What waits on the back shelf: what a refill brings to the machine.</summary>
    public int ShelfCups { get { Split(); return shelfCups; } }
    public int ShelfBeans { get { Split(); return shelfBeans; } }
    public int RestockCost => restockCost;
    public int RestockAmount => restockAmount;

    /// <summary>What one restock adds to the cups and to the beans: the amount, plus the Bulk Restock
    /// upgrade's bonus. The recap phone shows this number, and BuyRestock adds it.</summary>
    public int RestockAdds => restockAmount + (UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraRestock : 0);

    /// <summary>Under this many is "low" (the recap phone marks it, and its Shop tab gets a "!").</summary>
    public const int LowStock = 10;
    public bool CupsLow => Cups < LowStock;
    public bool BeansLow => Beans < LowStock;

    /// <summary>The machine is at Downtime.RefillAt (3) or fewer cups or beans.</summary>
    public bool MachineLow => MachineCups <= Downtime.RefillAt || MachineBeans <= Downtime.RefillAt;
    /// <summary>A refill would bring something: the machine short of full and the shelf with some of what it's short of.</summary>
    public bool CanRefill => MachineCups < MachineHolds && ShelfCups > 0 || MachineBeans < MachineHolds && ShelfBeans > 0;
    /// <summary>The machine is low and the shelf can fill it (§2.5: something to do while waiting).</summary>
    public bool RefillWanted => MachineCups <= Downtime.RefillAt && ShelfCups > 0 || MachineBeans <= Downtime.RefillAt && ShelfBeans > 0;

    private void Awake()
    {
        Instance = this;
        Split();
    }

    // The save's split, after GameBootstrap (order -100) has pushed the whole stock in with SetStock.
    private void Start() => ApplyTheSavedSplit();

    // The first look: the Inspector's whole stock, the machine filled from it.
    private void Split()
    {
        if (split) return;
        split = true;
        Fill(cups, beans);
    }

    private void Fill(int totalCups, int totalBeans)
    {
        totalCups = Mathf.Max(0, totalCups);
        totalBeans = Mathf.Max(0, totalBeans);
        machineCups = Mathf.Min(totalCups, MachineHolds);
        machineBeans = Mathf.Min(totalBeans, MachineHolds);
        shelfCups = totalCups - machineCups;
        shelfBeans = totalBeans - machineBeans;
    }

    // Save system pushes restored stock in here on load: the whole stock, the machine filled from it (an older save's way; the
    // saved machine share, when there is one, follows in Start).
    public void SetStock(int newCups, int newBeans)
    {
        split = true;
        Fill(newCups, newBeans);
    }

    /// <summary>The whole stock and how much of it is in the machine (24 Oct 2026; -1: fill the machine from the stock).</summary>
    public void SetStock(int newCups, int newBeans, int inMachineCups, int inMachineBeans)
    {
        SetStock(newCups, newBeans);
        if (inMachineCups >= 0)
        {
            machineCups = Mathf.Clamp(inMachineCups, 0, Mathf.Min(MachineHolds, newCups));
            shelfCups = Mathf.Max(0, newCups) - machineCups;
        }
        if (inMachineBeans >= 0)
        {
            machineBeans = Mathf.Clamp(inMachineBeans, 0, Mathf.Min(MachineHolds, newBeans));
            shelfBeans = Mathf.Max(0, newBeans) - machineBeans;
        }
    }

    private void ApplyTheSavedSplit()
    {
        SaveData loaded = SaveManager.Instance != null ? SaveManager.Instance.Loaded : null;
        if (loaded == null || loaded.machineCups < 0 && loaded.machineBeans < 0) return;
        SetStock(Cups, Beans, loaded.machineCups, loaded.machineBeans);
    }

    public bool TakeCup()
    {
        Split();
        if (machineCups <= 0) return false;
        machineCups--;
        return true;
    }

    // Putting an unwanted cup back. Without this, picking up a cup with no
    // order waiting left you holding it with nowhere to go — carry is 1, so
    // you had to dump it on a shelf slot a device needed.
    // (24 Oct 2026: back into the machine's stack, or onto the shelf when the machine is full.)
    public void ReturnCup()
    {
        Split();
        if (machineCups < MachineHolds) machineCups++;
        else shelfCups++;
    }

    public bool CanBrew(DrinkDefinition drink)
    {
        if (drink == null) return false;
        Split();
        return machineBeans >= Mathf.Max(0, drink.beansCost);
    }

    public bool ConsumeBeans(DrinkDefinition drink)
    {
        if (!CanBrew(drink)) return false;
        machineBeans -= Mathf.Max(0, drink.beansCost);
        return true;
    }

    public bool CanMake(DrinkDefinition drink)
    {
        if (drink == null) return false;
        Split();
        // Prebrewing has already spent stock. A finished drink remains sellable
        // when the inventory is empty; a purchased empty cup can still be filled.
        bool emptyCup = false;
        foreach (DrinkJob cup in DrinkJob.Live)
        {
            if (cup == null) continue;
            if (cup.Drink == drink && cup.CanHandBack) return true;
            if (cup.IsEmpty && !cup.Locked) emptyCup = true;
        }
        foreach (BeverageSlot slot in FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude))
            if (slot.drink == drink && slot.IsPouring && slot.Cup != null) return true;
        // 24 Oct 2026 (Days): the WHOLE stock, the back shelf's too. A refill (one trip, about 8 s) brings it to the machine, so a
        // customer is never turned away, nor apologised to, while the shelf has what their drink takes (P's §2.5: "now that
        // only happens when the shelf is empty too"). Taking the cup and brewing read the machine (TakeCup, CanBrew).
        return (machineCups + shelfCups >= Mathf.Max(1, drink.cupsCost) || emptyCup)
               && machineBeans + shelfBeans >= Mathf.Max(0, drink.beansCost);
    }

    /// <summary>
    /// A crate from the back shelf into the machine (24 Oct 2026): it fills back to MachineHolds of each from the shelf. True
    /// when anything moved.
    /// </summary>
    public bool Refill()
    {
        Split();
        int cupsIn = Mathf.Min(shelfCups, MachineHolds - machineCups), beansIn = Mathf.Min(shelfBeans, MachineHolds - machineBeans);
        cupsIn = Mathf.Max(0, cupsIn);
        beansIn = Mathf.Max(0, beansIn);
        machineCups += cupsIn;
        shelfCups -= cupsIn;
        machineBeans += beansIn;
        shelfBeans -= beansIn;
        if (cupsIn + beansIn > 0) Downtime.Today.refills++;
        return cupsIn + beansIn > 0;
    }

    public bool BuyRestock()
    {
        if (ShopEconomy.Instance == null) return false;
        if (ShopEconomy.Instance.Money < restockCost) return false;

        ShopEconomy.Instance.AddMoney(-restockCost);

        // 24 Oct 2026 (Days): onto the back shelf (P's §2.5): the machine is filled from it by hand.
        Split();
        int adds = RestockAdds;
        shelfCups += adds;
        shelfBeans += adds;
        return true;
    }
}
