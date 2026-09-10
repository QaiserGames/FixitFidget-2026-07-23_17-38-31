using UnityEngine;

public class ShopInventory : MonoBehaviour
{
    public static ShopInventory Instance { get; private set; }

    [SerializeField] private int cups = 20;
    [SerializeField] private int beans = 20;

    [Header("Restocking")]
    [SerializeField] private int restockAmount = 20;
    [SerializeField] private int restockCost = 30;

    public int Cups => cups;
    public int Beans => beans;
    public int RestockCost => restockCost;

    private void Awake()
    {
        Instance = this;
    }

    // Save system pushes restored stock in here on load.
    public void SetStock(int newCups, int newBeans)
    {
        cups = Mathf.Max(0, newCups);
        beans = Mathf.Max(0, newBeans);
    }

    public bool TakeCup()
    {
        if (cups <= 0) return false;
        cups--;
        return true;
    }

    // Putting an unwanted cup back. Without this, picking up a cup with no
    // order waiting left you holding it with nowhere to go — carry is 1, so
    // you had to dump it on a shelf slot a device needed.
    public void ReturnCup() => cups++;

    public bool CanBrew(DrinkDefinition drink)
    {
        if (drink == null) return false;
        return beans >= Mathf.Max(0, drink.beansCost);
    }

    public bool ConsumeBeans(DrinkDefinition drink)
    {
        if (!CanBrew(drink)) return false;
        beans -= Mathf.Max(0, drink.beansCost);
        return true;
    }

    public bool CanMake(DrinkDefinition drink)
    {
        if (drink == null) return false;
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
        return (cups >= Mathf.Max(1, drink.cupsCost) || emptyCup) && CanBrew(drink);
    }

    public bool BuyRestock()
    {
        if (ShopEconomy.Instance == null) return false;
        if (ShopEconomy.Instance.Money < restockCost) return false;

        ShopEconomy.Instance.AddMoney(-restockCost);

        int bonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraRestock : 0;
        cups += restockAmount + bonus;
        beans += restockAmount + bonus;
        return true;
    }
}
