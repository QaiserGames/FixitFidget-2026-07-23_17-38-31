using UnityEngine;

[CreateAssetMenu(fileName = "Drink_", menuName = "FixitFiasco/Drink")]
public class DrinkDefinition : ScriptableObject
{
    [Tooltip("Used in dialogue via the {device} token and on the ticket.")]
    public string drinkName = "latte";

    [Tooltip("Seconds at the machine to make one.")]
    public float brewSeconds = 6f;

    public int price = 5;

    [Header("What it costs from stock")]
    public int cupsCost = 1;
    public int beansCost = 1;

    [Tooltip("The physical cup that appears when it's made.")]
    public GameObject cupPrefab;

    public Color cupColor = new Color(0.85f, 0.75f, 0.6f);
}