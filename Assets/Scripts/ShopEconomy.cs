using UnityEngine;

public class ShopEconomy : MonoBehaviour
{
    public static ShopEconomy Instance { get; private set; }

    public int Money { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void AddMoney(int amount)
    {
        Money += amount;
        Debug.Log($"+${amount}  |  Till: ${Money}");
    }

    // Used by the save system on load. Not for gameplay — money changes
    // in play should always go through AddMoney so future effects hook in.
    public void SetMoney(int amount)
    {
        Money = Mathf.Max(0, amount);
    }



}