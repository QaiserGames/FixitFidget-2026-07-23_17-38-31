using UnityEngine;

public class CupStack : Interactable
{
    [SerializeField] private GameObject cupPrefab;

    // Either take one, or put an unwanted one back.
    //
    // The return half exists because carry capacity is 1: pick up a cup with no
    // order waiting and you were stuck holding it, with the only way out being
    // to dump it in a shelf or bench slot that a device needed. The stack is
    // where cups live, so the stack is where they go back.
    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry == null) return false;

            if (carry.IsCarrying) return HeldCup(carry) != null;

            return ShopInventory.Instance != null && ShopInventory.Instance.Cups > 0;
        }
    }

    // Any cup you're holding, empty or brewed, as long as it isn't mid-brew.
    //
    // This deliberately accepts BREWED cups, reversing an earlier decision. The
    // argument against was that pouring away a latte quietly destroys several
    // dollars of beans. The argument that beat it: the loss isn't quiet if the
    // player chooses it. A prompt reading "Pour it away" is a decision with a
    // visible cost, whereas the alternative — no bin at all — was strictly
    // worse. An orphaned drink had nowhere to go but a shelf slot a device
    // needed, and a full shelf blocks intake entirely.
    //
    // The SinkStation from the architecture spec owns this properly later.
    private static DrinkJob HeldCup(PlayerCarry carry)
    {
        DrinkJob d = carry != null ? carry.Carried as DrinkJob : null;
        return d != null && !d.Locked ? d : null;
    }

    public override string Prompt
    {
        get
        {
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();

            if (carry != null && carry.IsCarrying)
            {
                DrinkJob cup = HeldCup(carry);
                if (cup == null) return "Hands full";

                return cup.IsEmpty
                    ? "Put the cup back"
                    : $"Pour away the {cup.Drink.drinkName}";
            }

            if (ShopInventory.Instance != null && ShopInventory.Instance.Cups <= 0)
                return "Out of cups";

            return "Take a cup";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        // Putting an unwanted one back, or pouring one away. Either way the cup
        // itself returns to stock — it's the beans that are gone.
        if (HeldCup(carry) != null)
        {
            carry.Consume();
            if (ShopInventory.Instance != null) ShopInventory.Instance.ReturnCup();
            return;
        }

        // Taking one.
        if (cupPrefab == null) return;
        if (ShopInventory.Instance == null || !ShopInventory.Instance.TakeCup()) return;

        GameObject cup = Instantiate(cupPrefab, transform.position, transform.rotation);
        DrinkJob job = cup.GetComponent<DrinkJob>();
        if (job != null) carry.PickUp(job);
    }
}