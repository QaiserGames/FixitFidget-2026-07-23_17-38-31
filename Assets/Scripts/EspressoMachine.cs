using System.Collections.Generic;
using UnityEngine;

public class EspressoMachine : Interactable
{
    [SerializeField] private Transform outputSpot;

    public bool IsBrewing => brewTimer > 0f;
    public float BrewProgress => brewDuration <= 0f ? 0f : 1f - (brewTimer / brewDuration);

    private float brewTimer;
    private float brewDuration;
    private DrinkDefinition brewing;
    private CustomerBrain brewingFor;

    // Everyone waiting on a drink that hasn't been started yet, oldest first.
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
            return list;
        }
    }

    // Always visible. The prompt explains why it can't be used right now.
    public override bool IsAvailable => true;

    public override string Prompt
    {
        get
        {
            if (IsBrewing) return $"Brewing...  {Mathf.CeilToInt(brewTimer)}s";

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry != null && carry.IsCarrying) return "Hands full";

            List<CustomerBrain> orders = PendingOrders;
            if (orders.Count == 0) return "No orders waiting";

            return $"Make {orders[0].Record.Subject} for {orders[0].CustomerName}";
        }
    }

    // One button, longest-waiting order first — no hidden number keys.
    public override void Interact(PlayerInteractor player)
    {
        List<CustomerBrain> orders = PendingOrders;
        if (orders.Count > 0) StartBrew(orders[0]);
    }

    private void Update()
    {
        if (!IsBrewing) return;

        // The customer can storm out mid-brew. Keep going — the stock is spent,
        // and the finished cup can be handed to anyone who wants that drink.
        brewTimer -= Time.deltaTime;
        if (brewTimer <= 0f) FinishBrew();
    }

    private void StartBrew(CustomerBrain customer)
    {
        if (IsBrewing || customer == null || customer.Record == null) return;

        DrinkDefinition drink = customer.Record.drink;
        if (drink == null) return;

        // Stock is consumed at the START — a wasted drink is a real loss.
        if (ShopInventory.Instance == null || !ShopInventory.Instance.Consume(drink)) return;

        brewing = drink;
        brewingFor = customer;
        brewDuration = drink.brewSeconds;
        brewTimer = brewDuration;

        customer.MarkDrinkStarted();
    }

    private void FinishBrew()
    {
        brewTimer = 0f;

        if (brewing != null && brewing.cupPrefab != null && outputSpot != null)
        {
            GameObject cup = Instantiate(brewing.cupPrefab, outputSpot.position, outputSpot.rotation);

            DrinkJob job = cup.GetComponent<DrinkJob>();
            if (job != null)
            {
                job.SetDrink(brewing);

                if (brewingFor != null)
                {
                    job.SetOwner(brewingFor);
                    job.Configure(brewingFor.Record);

                    JobMarker marker = cup.GetComponentInChildren<JobMarker>(true);
                    if (marker != null) marker.Show(brewingFor.JobNumber, brewingFor.JobColor);
                }
            }
        }

        brewing = null;
        brewingFor = null;
    }
}