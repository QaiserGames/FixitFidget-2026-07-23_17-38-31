using UnityEngine;

public class CupStack : Interactable
{
    [SerializeField] private GameObject cupPrefab;

    // Hands must be free, and we need cups in stock.
    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry == null || carry.IsCarrying) return false;
            return ShopInventory.Instance != null && ShopInventory.Instance.Cups > 0;
        }
    }

    public override string Prompt
    {
        get
        {
            if (ShopInventory.Instance != null && ShopInventory.Instance.Cups <= 0)
                return "Out of cups";

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry != null && carry.IsCarrying) return "Hands full";

            return "Take a cup";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (cupPrefab == null) return;
        if (ShopInventory.Instance == null || !ShopInventory.Instance.TakeCup()) return;

        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        GameObject cup = Instantiate(cupPrefab, transform.position, transform.rotation);
        DrinkJob job = cup.GetComponent<DrinkJob>();
        if (job != null) carry.PickUp(job);
    }
}