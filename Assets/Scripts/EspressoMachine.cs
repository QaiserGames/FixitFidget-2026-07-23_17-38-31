using System.Collections.Generic;
using UnityEngine;

public class EspressoMachine : Interactable
{
    [SerializeField] private Transform cupSlot;

    public bool IsBrewing => brewTimer > 0f;
    public bool HasCup => loadedCup != null;

    private float brewTimer;
    private DrinkJob loadedCup;
    private CustomerBrain brewingFor;

    // Everyone waiting on a drink that hasn't been started, oldest order first.
    //
    // THE BUG THIS FIXES: this used to claim "oldest first" while relying on
    // whatever order FindObjectsByType handed back — and FindObjectsSortMode.None
    // explicitly guarantees NO order, stable or otherwise. So orders[0] was
    // arbitrary, and could differ between the frame that drew the prompt and
    // the frame you pressed E. The prompt said "Make Latte for Priya" and the
    // machine brewed for Tomas.
    //
    // It mostly went unnoticed because everyone wants one of two drinks. It
    // stops being harmless the moment repair customers queue here too.
    public List<CustomerBrain> PendingOrders
    {
        get
        {
            List<CustomerBrain> list = new();
            foreach (CustomerBrain b in FindObjectsByType<CustomerBrain>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (b.AwaitingDrink) list.Add(b);
            }

            list.Sort((a, b) => a.DrinkOrderedAt.CompareTo(b.DrinkOrderedAt));
            return list;
        }
    }

    public override bool IsAvailable => true;

    public override string Prompt
    {
        get
        {
            if (IsBrewing) return $"Brewing...  {Mathf.CeilToInt(brewTimer)}s";

            // Finished drink sitting in the machine.
            if (loadedCup != null && !loadedCup.IsEmpty)
            {
                PlayerCarry c = FindAnyObjectByType<PlayerCarry>();
                if (c != null && c.IsCarrying) return "Hands full";
                return $"Take the {loadedCup.Drink.drinkName}";
            }

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            DrinkJob held = carry != null ? carry.Carried as DrinkJob : null;

            if (held == null || !held.IsEmpty) return "Needs an empty cup";

            List<CustomerBrain> orders = PendingOrders;
            if (orders.Count == 0) return "No orders waiting";

            DrinkDefinition want = orders[0].WantedDrink;   // repair customers wish too
            if (ShopInventory.Instance != null && !ShopInventory.Instance.CanBrew(want))
                return "Out of beans";

            return $"Make {want.drinkName} for {orders[0].CustomerName}";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null || IsBrewing) return;

        // Collecting a finished drink.
        if (loadedCup != null && !loadedCup.IsEmpty)
        {
            if (carry.IsCarrying) return;
            carry.PickUp(loadedCup);
            loadedCup = null;
            brewingFor = null;
            return;
        }

        // Loading an empty cup.
        DrinkJob held = carry.Carried as DrinkJob;
        if (held == null || !held.IsEmpty) return;

        List<CustomerBrain> orders = PendingOrders;
        if (orders.Count == 0) return;

        CustomerBrain customer = orders[0];
        DrinkDefinition drink = customer.WantedDrink;

        if (ShopInventory.Instance == null || !ShopInventory.Instance.ConsumeBeans(drink)) return;

        // The cup leaves your hands and sits in the machine — go do something else.
        carry.PlaceAt(cupSlot);

        loadedCup = held;
        loadedCup.Locked = true;
        brewingFor = customer;
        float speed = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.BrewTimeMultiplier : 1f;
        brewTimer = drink.brewSeconds * speed;
        pendingDrink = drink;

        customer.MarkDrinkStarted();
    }

    private DrinkDefinition pendingDrink;

    private void Update()
    {
        if (!IsBrewing) return;

        brewTimer -= Time.deltaTime;
        if (brewTimer > 0f) return;

        brewTimer = 0f;
        FinishBrew();
    }

    private void FinishBrew()
    {
        if (loadedCup == null) return;

        loadedCup.SetDrink(pendingDrink);

        // Bind it to whoever ordered it, so the cup carries their colour.
        if (brewingFor != null)
        {
            loadedCup.SetOwner(brewingFor);
            loadedCup.Configure(brewingFor.Record);

            JobMarker marker = loadedCup.GetComponentInChildren<JobMarker>(true);
            if (marker != null) marker.Show(brewingFor.JobNumber, brewingFor.JobColor);
        }
        loadedCup.Locked = false;
    }
}