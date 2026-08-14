using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EspressoMachine : Interactable
{
    [SerializeField] private Transform outputSpot;
    [SerializeField] private float reach = 3f;

    public bool IsBrewing => brewTimer > 0f;
    public float BrewProgress => brewDuration <= 0f ? 0f : 1f - (brewTimer / brewDuration);

    private float brewTimer;
    private float brewDuration;
    private DrinkDefinition brewing;
    private CustomerBrain brewingFor;

    // Everyone currently waiting on a drink that hasn't been started.
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
            if (orders.Count == 1) return $"Make {orders[0].Record.Subject} for {orders[0].CustomerName}";

            return "Choose an order  [1-4]";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        List<CustomerBrain> orders = PendingOrders;
        if (orders.Count == 1) StartBrew(orders[0]);
        // With several pending, the player picks with number keys instead.
    }

    private void Update()
    {
        // Number keys pick between competing orders, same as the phone tree.
        if (!IsBrewing && Keyboard.current != null)
        {
            PlayerInteractor p = FindAnyObjectByType<PlayerInteractor>();
            bool nearby = p != null && Vector3.Distance(p.transform.position, transform.position) < reach;

            if (nearby)
            {
                List<CustomerBrain> orders = PendingOrders;
                if (orders.Count > 1)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame && orders.Count > 0) StartBrew(orders[0]);
                    if (Keyboard.current.digit2Key.wasPressedThisFrame && orders.Count > 1) StartBrew(orders[1]);
                    if (Keyboard.current.digit3Key.wasPressedThisFrame && orders.Count > 2) StartBrew(orders[2]);
                    if (Keyboard.current.digit4Key.wasPressedThisFrame && orders.Count > 3) StartBrew(orders[3]);
                }
            }
        }

        if (!IsBrewing) return;

        // The customer can storm out mid-brew. Keep going — the stock is spent.
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

                // Bind it to whoever ordered it — the cup carries their colour.
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