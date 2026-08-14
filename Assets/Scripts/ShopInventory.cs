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

    // Can we make this drink right now?
    public bool CanMake(DrinkDefinition drink)
    {
        if (drink == null) return false;
        return cups >= drink.cupsCost && beans >= drink.beansCost;
    }

    public bool Consume(DrinkDefinition drink)
    {
        if (!CanMake(drink)) return false;

        cups -= drink.cupsCost;
        beans -= drink.beansCost;
        return true;
    }

    // Bought at end of day. Returns false if they can't afford it.
    public bool BuyRestock()
    {
        if (ShopEconomy.Instance == null) return false;
        if (ShopEconomy.Instance.Money < restockCost) return false;

        ShopEconomy.Instance.AddMoney(-restockCost);
        cups += restockAmount;
        beans += restockAmount;
        return true;
    }
}