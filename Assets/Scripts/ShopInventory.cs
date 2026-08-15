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

    public bool CanBrew(DrinkDefinition drink)
    {
        if (drink == null) return false;
        return beans >= drink.beansCost;
    }

    public bool ConsumeBeans(DrinkDefinition drink)
    {
        if (!CanBrew(drink)) return false;
        beans -= drink.beansCost;
        return true;
    }

    public bool CanMake(DrinkDefinition drink)
    {
        if (drink == null) return false;
        return cups >= drink.cupsCost && beans >= drink.beansCost;
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