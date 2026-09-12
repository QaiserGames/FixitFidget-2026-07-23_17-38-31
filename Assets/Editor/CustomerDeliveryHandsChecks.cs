#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CustomerDeliveryHandsChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Customer delivery from either hand")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        DrinkDefinition coffee = null, tea = null;
        try
        {
            var host = new GameObject("Isolated delivery checks"); SceneManager.MoveGameObjectToScene(host, scene);
            host.SetActive(false);
            var carry = Child(host, "Player").AddComponent<PlayerCarry>(); carry.SetCapacity(2);
            var player = carry.gameObject.AddComponent<PlayerInteractor>();
            var customer = Customer(host, player);
            coffee = Recipe("Coffee"); tea = Recipe("Tea");
            Set(customer, "drinkWish", coffee);
            var left = Cup(host, "Coffee", coffee); var right = Cup(host, "Tea", tea);
            Require(carry.TryPickUp(left) && carry.TryPickUp(right), "Fixture carries different recipes in both hands.");
            carry.SelectHand(1);
            Require(customer.CanReceiveDrink && !customer.HasColdDrinkForOrder && carry.Carried == right,
                "A coffee in the left hand is deliverable while tea remains selected in the right.");
            Require(FindCup(customer, carry) == left && carry.SelectedHandIndex == 1,
                "Checking the matching cup does not select a different hand.");
            Require(Select(carry, left) && carry.Carried == left && carry.GetHandItem(1) == right,
                "Committing delivery selects the matching cup without moving the other cup.");

            ((DrinkFreshness)Get(left, "freshness")).Advance(coffee.coldSeconds + 1);
            right.SetDrink(coffee, true); carry.SelectHand(0);
            Require(customer.CanReceiveDrink && !customer.HasColdDrinkForOrder && FindCup(customer, carry) == right,
                "A cold selected cup cannot suppress a warm matching cup in the other hand.");
            right.Locked = true;
            Require(!customer.CanReceiveDrink && customer.HasColdDrinkForOrder,
                "A locked cup is never served and the remaining matching cold cup reports cold.");
            right.Locked = false;
            right.SetDrink(tea, true);
            Require(!customer.CanReceiveDrink && customer.HasColdDrinkForOrder,
                "An unrelated warm drink cannot make the cold order deliverable.");

            carry.SelectHand(0); carry.PlaceAt(Child(host, "Left return").transform);
            right.SetDrink(coffee, true); carry.SelectHand(0);
            Require(carry.Carried == null && customer.CanReceiveDrink && !customer.HasColdDrinkForOrder,
                "An explicitly empty selected hand does not hide a drink held in the other hand.");
            Require(carry.Carried == null && carry.SelectedHandIndex == 0,
                "Repeated customer prompts leave an empty selected hand empty.");
            Set(customer, "state", CustomerBrain.State.Leaving);
            Require(!customer.CanReceiveDrink && !customer.HasColdDrinkForOrder, "Departing customers cannot receive drinks.");
            Set(customer, "state", CustomerBrain.State.Waiting);
            Set(customer, "drinkOrdered", false);
            Require(!customer.CanReceiveDrink && !customer.HasColdDrinkForOrder, "Customers without an order cannot receive drinks.");

            var device = Child(host, "Customer device").AddComponent<RepairJob>(); device.SetOwner(customer);
            Set(customer, "activeJob", device);
            carry.UseAutomaticHand(); Require(carry.TryPickUp(device), "The device fits the empty left hand beside the right cup.");
            carry.SelectHand(1);
            Require(customer.JobReady && carry.Carried == right, "A customer's assembled device is returnable from the unselected hand.");
            var detached = Child(host, "Detached cover"); device.RegisterDetached(detached);
            Require(!customer.JobReady, "Automatic delivery still requires reassembly.");
            device.UnregisterDetached(detached);
            var other = Customer(host, player); Set(other, "activeJob", Child(host, "Other device").AddComponent<RepairJob>());
            Require(!other.JobReady, "Another customer's device cannot satisfy this customer's handback.");
            Require(Select(carry, device) && carry.Carried == device && carry.GetHandItem(1) == right,
                "Repair delivery selects the exact device while preserving the other hand's drink.");
            Debug.Log("[Customer hands] PASS: read-only prompts, both hands, empty selected hand, mixed recipes, warm-over-cold choice, locked cup rejection and exact assembled repair selection. No scenes, payments or saves changed.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (coffee != null) UnityEngine.Object.DestroyImmediate(coffee);
            if (tea != null) UnityEngine.Object.DestroyImmediate(tea);
        }
    }

    // Run synchronously during the isolated service playtest. Fixture objects
    // stay inactive, and singleton economy/day references are restored before
    // control returns, so the campaign and current day's totals are untouched.
    public static void RunTransactions()
    {
        Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ServicePlaytestSetup.ScenePath,
            "Run transactions only in the isolated service interaction playtest.");
        var previousEconomy = ShopEconomy.Instance; var previousClock = DayClock.Instance;
        GameObject host = null; DrinkDefinition coffee = null;
        try
        {
            host = new GameObject("Isolated delivery transactions"); host.SetActive(false);
            var economy = host.AddComponent<ShopEconomy>();
            SetSingleton(typeof(ShopEconomy), economy); SetSingleton(typeof(DayClock), null);
            var carry = Child(host, "Player").AddComponent<PlayerCarry>(); carry.SetCapacity(2);
            var player = carry.gameObject.AddComponent<PlayerInteractor>();
            var customer = Customer(host, player); coffee = Recipe("Coffee"); Set(customer, "drinkWish", coffee);
            var device = Child(host, "Customer device").AddComponent<RepairJob>();
            device.SetOwner(customer); device.Configure(new Job { payout = 20 }); Set(customer, "activeJob", device);
            var cup = Cup(host, "Warm coffee", coffee);
            Require(carry.TryPickUp(cup) && carry.TryPickUp(device), "Two different deliveries occupy both hands.");
            carry.SelectHand(1); customer.ServeDrink(carry);
            Require(carry.Count == 1 && carry.GetHandItem(1) == device && !carry.Contains(cup),
                "Serving consumes only the matching cup, even with the repair selected.");
            Require(economy.Money == 10 && customer.ActiveJob == device && !customer.HasDrinkOrder && !customer.IsLeaving,
                "Serving pays the expected base and tip once, and the customer stays for the repair.");
            customer.ServeDrink(carry); Require(economy.Money == 10, "Repeated drink handoff cannot pay twice.");
            Set(customer, "patienceLeft", 50f); carry.SelectHand(0); customer.CompleteJob();
            Require(carry.Count == 0 && customer.ActiveJob == null && economy.Money == 41,
                "Repair return from the other hand consumes the exact device and preserves Perfect-grade payment.");
            customer.CompleteJob(); Require(economy.Money == 41, "Repeated repair return cannot pay twice.");
            Debug.Log("[Customer delivery transactions] PASS: automatic cup and device consumption, two-track customer stays, exact base/tip/grade payment and duplicate delivery rejection. Campaign economy and day totals untouched.");
        }
        finally
        {
            SetSingleton(typeof(ShopEconomy), previousEconomy); SetSingleton(typeof(DayClock), previousClock);
            if (host != null) UnityEngine.Object.Destroy(host);
            if (coffee != null) UnityEngine.Object.Destroy(coffee);
        }
    }

    private static CustomerBrain Customer(GameObject host, PlayerInteractor player)
    {
        var customer = Child(host, "Waiting customer").AddComponent<CustomerBrain>();
        Set(customer, "player", player); Set(customer, "state", CustomerBrain.State.Waiting);
        Set(customer, "drinkOrdered", true); Set(customer, "serviceMax", 100f); Set(customer, "patienceLeft", 50f);
        Set(customer, "maxTipFraction", .5f); return customer;
    }
    private static DrinkDefinition Recipe(string name)
    {
        var recipe = ScriptableObject.CreateInstance<DrinkDefinition>();
        recipe.drinkName = name; recipe.price = 8; recipe.freshSeconds = 10; recipe.coldSeconds = 20; return recipe;
    }
    private static DrinkJob Cup(GameObject host, string name, DrinkDefinition recipe)
    { var cup = Child(host, name).AddComponent<DrinkJob>(); cup.SetDrink(recipe, true); return cup; }
    private static GameObject Child(GameObject parent, string name)
    { var obj = new GameObject(name); obj.transform.SetParent(parent.transform, false); return obj; }
    private static DrinkJob FindCup(CustomerBrain customer, PlayerCarry carry)
        => (DrinkJob)typeof(CustomerBrain).GetMethod("FindServeableDrink", Private).Invoke(customer, new object[] { carry });
    private static bool Select(PlayerCarry carry, JobBase item)
        => (bool)typeof(CustomerBrain).GetMethod("SelectDeliveryItem", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { carry, item });
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void SetSingleton(Type type, object value)
        => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
