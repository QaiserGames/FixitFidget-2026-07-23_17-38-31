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
}