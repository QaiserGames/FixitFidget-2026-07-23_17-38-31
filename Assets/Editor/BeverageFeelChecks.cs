#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Deterministic, isolated checks for the actual dispenser interaction path.
// Runtime supply creation/destruction and sound are verified in the playtest scene.
public static class BeverageFeelChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Dispenser interaction and visible filling")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousStock = ShopInventory.Instance;
        var previousClock = DayClock.Instance;
        var previousUpgrades = UpgradeManager.Instance;
        float previousTimeScale = Time.timeScale;
        var recipes = new List<DrinkDefinition>();
        var cups = new List<DrinkJob>();
        try
        {
            Time.timeScale = 1;
            Instance<DayClock>(null); Instance<UpgradeManager>(null);
            var host = new GameObject("Isolated beverage feel checks");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = new Vector3(6000, 6000, 6000);
            var stock = Child(host, "Stock").AddComponent<ShopInventory>();
            Call(stock, "Awake"); stock.SetStock(8, 8);
            var playerObject = Child(host, "Player");
            var carry = playerObject.AddComponent<PlayerCarry>(); carry.SetCapacity(2);
            var player = playerObject.AddComponent<PlayerInteractor>();
            var coffee = Recipe("Coffee", 12, recipes);
            var tea = Recipe("Tea", 18, recipes);
            var first = Slot(host, "Coffee section", coffee, 0);
            var second = Slot(host, "Tea section", tea, .2f);
            second.stream.useWorldSpace = true;
            var firstCup = Cup(host, "First cup", cups);
            var secondCup = Cup(host, "Second cup", cups);
            Require(stock.TakeCup() && stock.TakeCup() && carry.TryPickUp(secondCup) && carry.TryPickUp(firstCup),
                "The fixture starts with two purchased empty cups and the first selected.");
            int cupStock = stock.Cups;
            var control = first.gameObject.AddComponent<BeverageControl>(); control.slot = first;
            var paddle = Child(first.gameObject, "Coffee paddle").AddComponent<BeverageControl>();
            paddle.slot = first; paddle.dispenseButton = true;
            var secondControl = second.gameObject.AddComponent<BeverageControl>(); secondControl.slot = second;
            var secondPaddle = Child(second.gameObject, "Tea paddle").AddComponent<BeverageControl>();
            secondPaddle.slot = second; secondPaddle.dispenseButton = true;
            control.Interact(player);
            Require(!first.IsPouring && first.Cup == firstCup && !firstCup.Locked && carry.Carried == secondCup && stock.Beans == 8,
                "Clicking the generous cup area places the selected cup without starting or charging for a pour.");
            paddle.Interact(player);
            Require(first.IsPouring && firstCup.Locked && stock.Beans == 6,
                "Clicking the named paddle begins the placed cup's pour with one ingredient debit.");
            paddle.Interact(player); control.Interact(player);
            Require(stock.Cups == cupStock && first.Cup == firstCup && carry.Carried == secondCup && stock.Beans == 6,
                "Starting a pour never buys another cup, and repeated activation cannot debit again.");
            secondControl.Interact(player); secondPaddle.Interact(player);
            Require(second.Cup == secondCup && second.IsPouring && stock.Beans == 4 && carry.Count == 0,
                "A second section starts independently while the first is filling.");
            Require(!first.TransferCup(carry) && !carry.TryPickUp(firstCup), "A filling cup cannot be collected by either pickup path.");

            SetProgress(first, .25f); SetProgress(second, .70f);
            float lowSurface = firstCup.LiquidVisual.SurfaceHeight;
            Require(Near(first.Progress, .25f) && Near(second.Progress, .70f), "Each section retains its own progress.");
            Require(firstCup.IsEmpty && !firstCup.HasFreshness && !firstCup.CanHandBack,
                "Partly filled cups do not start cooling and cannot be served.");
            Require(Near(firstCup.LiquidVisual.FillAmount, .25f) && Near(secondCup.LiquidVisual.FillAmount, .70f),
                "Physical liquid height follows each section's progress.");
            CheckStream(first); CheckStream(second);
            SetProgress(first, .65f);
            Require(firstCup.LiquidVisual.SurfaceHeight > lowSurface && Near(second.Progress, .70f),
                "Liquid visibly rises without advancing another section.");
            CheckStream(first);
            Set(first, "pourLeft", 0f); Call(first, "Update");
            Require(!first.IsPouring && first.Cup == firstCup && !firstCup.Locked && firstCup.Drink == coffee
                && firstCup.CanHandBack && firstCup.HasFreshness && firstCup.Owner == null,
                "The completed cup stays under its nozzle, fresh and available without an order.");
            Require(Near(firstCup.LiquidVisual.FillAmount, 1) && !first.stream.enabled
                && firstCup.GetComponent<DrinkFreshnessRing>() != null && Near(firstCup.FreshnessRemaining, 1),
                "Completion leaves a full physical liquid surface and a separate fully fresh cooling indicator.");
            Require(second.IsPouring && secondCup.Locked && Near(second.Progress, .70f),
                "Completing one section leaves the other pour running.");
            int beansBefore = stock.Beans;
            Require(!first.TryPour() && stock.Beans == beansBefore, "A filled cup blocks another ingredient debit.");
            stock.SetStock(0, 0);
            Require(stock.CanMake(coffee), "A prebrewed fresh drink remains usable when unspent stock is empty.");
            control.Interact(player);
            Require(carry.Carried == firstCup && first.Cup == null,
                "The same generous contextual control collects a finished cup and frees its section.");
            Set(second, "pourLeft", 0f); Call(second, "Update");
            secondControl.Interact(player);
            Require(carry.Count == 2 && second.Cup == null,
                "Both finished drinks can occupy the shared hands.");

            var freshness = (DrinkFreshness)Get(firstCup, "freshness");
            freshness.Advance(coffee.freshSeconds);
            Require(firstCup.FreshnessStage == DrinkFreshness.Stage.Cooling && firstCup.CanHandBack
                && Near(firstCup.FreshnessTipMultiplier, .5f), "Cooling remains serveable with the reduced tip.");
            freshness.Advance(coffee.coldSeconds);
            Require(firstCup.FreshnessStage == DrinkFreshness.Stage.Cold && !firstCup.CanHandBack
                && firstCup.Drink == coffee && carry.Contains(firstCup),
                "A cold drink remains a physical carried cup until manually discarded and cannot be served.");

            var shelf = Child(host, "Return shelf").transform;
            carry.PlaceAt(shelf); carry.PlaceAt(shelf);
            var thirdCup = Cup(host, "Third cup", cups);
            Require(carry.TryPickUp(thirdCup), "A free hand can hold another empty cup.");
            control.Interact(player); paddle.Interact(player);
            Require(!first.IsPouring && first.Cup == thirdCup && stock.Beans == 0,
                "An empty cup can be placed with no ingredients, but its paddle cannot start a pour or overspend.");
            control.Interact(player);
            Require(carry.Carried == thirdCup && first.Cup == null,
                "A placed empty cup can be taken back when its recipe is out of stock.");
            stock.SetStock(5, 8);
            Time.timeScale = 0;
            control.Interact(player); paddle.Interact(player);
            Require(!first.IsPouring && first.Cup == null && carry.Carried == thirdCup && stock.Beans == 8,
                "Paused placement and paddle input neither move a cup nor spend ingredients.");
            var supply = Child(host, "Paused cup supply").AddComponent<BeverageCupSupply>();
            supply.Interact(player); supply.discard = true; supply.Interact(player);
            Require(carry.Carried == thirdCup && stock.Cups == 5,
                "Paused supply and return input cannot create, consume, or refund a cup.");
            Time.timeScale = 1;
            control.Interact(player); paddle.Interact(player);
            Require(first.IsPouring && first.Cup == thirdCup, "Placement and pouring resume after pause.");
            SetProgress(first, .4f);
            float remaining = (float)Get(first, "pourLeft");
            Time.timeScale = 0; Call(first, "Update");
            Require((float)Get(first, "pourLeft") == remaining && Near(firstCup.LiquidVisual.FillAmount, 1)
                && Near(thirdCup.LiquidVisual.FillAmount, .4f) && !first.stream.enabled,
                "Pause freezes the active pour and its liquid surface while hiding the stream.");
            Time.timeScale = 1; Call(first, "RefreshPourVisual"); CheckStream(first);
            Call(first, "OnDisable");
            Require(!first.IsPouring && !thirdCup.Locked && thirdCup.IsEmpty && Near(thirdCup.LiquidVisual.FillAmount, 0),
                "An interrupted station cannot strand a locked or partly filled cup.");
            var endedDay = Child(host, "Ended day guard").AddComponent<DayClock>();
            typeof(DayClock).GetProperty("DayOver").SetValue(endedDay, true); Instance(endedDay);
            beansBefore = stock.Beans;
            control.Interact(player); secondPaddle.Interact(player);
            Require(first.Cup == thirdCup && carry.Count == 0 && !second.IsPouring && stock.Beans == beansBefore,
                "The day-end recap blocks both cup transfers and fresh pours even when timeScale is positive.");
            Instance<DayClock>(null);

            var waste = Slot(host, "Explicit cupless paddle", coffee, .4f);
            int cupsBeforeWaste = DrinkJob.Live.Count;
            beansBefore = stock.Beans;
            Require(waste.TryPour() && stock.Beans == beansBefore - coffee.beansCost && !waste.TryPour(),
                "The separate paddle still deliberately spends ingredients without a cup, once per pour.");
            Set(waste, "pourLeft", 0f); Call(waste, "Update");
            Require(waste.Cup == null && DrinkJob.Live.Count == cupsBeforeWaste,
                "Wasted ingredients do not create a replacement cup.");
            Debug.Log("[Beverage feel] PASS: separate cup placement and named paddle activation, one debit, two independent sections, rising liquid and stream endpoints, pickup locks, full-slot blocking, prebrew, collection, separate cooling indicator, cooling/cold rules, stock exhaustion, pause and interrupted pours. No scene or save changes.");
        }
        finally
        {
            foreach (var cup in cups) if (cup != null) Call(cup, "OnDisable");
            EditorSceneManager.ClosePreviewScene(scene);
            Instance(previousStock); Instance(previousClock); Instance(previousUpgrades);
            Time.timeScale = previousTimeScale;
            foreach (var recipe in recipes) if (recipe != null) UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static DrinkDefinition Recipe(string label, float seconds, List<DrinkDefinition> recipes)
    {
        var recipe = ScriptableObject.CreateInstance<DrinkDefinition>(); recipes.Add(recipe);
        recipe.drinkName = label; recipe.beansCost = 2; recipe.brewSeconds = seconds;
        recipe.freshSeconds = 10; recipe.coldSeconds = 20; return recipe;
    }
    private static DrinkJob Cup(GameObject parent, string label, List<DrinkJob> cups)
    {
        var cup = Child(parent, label).AddComponent<DrinkJob>(); cups.Add(cup);
        Call(cup, "OnEnable"); cup.EnsureDispenserVisual(); return cup;
    }
    private static BeverageSlot Slot(GameObject parent, string label, DrinkDefinition recipe, float x)
    {
        var slot = Child(parent, label).AddComponent<BeverageSlot>(); slot.drink = recipe;
        slot.transform.localPosition = new Vector3(x, 0, 0);
        slot.cupPoint = Child(slot.gameObject, "Cup pad").transform;
        slot.stream = Child(slot.gameObject, "Visible pour").AddComponent<LineRenderer>();
        slot.stream.transform.localPosition = new Vector3(0, .25f, 0);
        slot.stream.useWorldSpace = false; slot.stream.enabled = false; return slot;
    }
    private static void CheckStream(BeverageSlot slot)
    {
        Require(slot.stream.enabled && slot.stream.positionCount >= 2, "Active pouring has a visible physical stream.");
        Vector3 start = slot.stream.GetPosition(0), end = slot.stream.GetPosition(slot.stream.positionCount - 1);
        if (!slot.stream.useWorldSpace)
        { start = slot.stream.transform.TransformPoint(start); end = slot.stream.transform.TransformPoint(end); }
        Require(Vector3.Distance(start, slot.stream.transform.position) < .001f
            && Vector3.Distance(end, slot.Cup.LiquidVisual.SurfacePosition) < .001f,
            "The stream connects the nozzle to the current rising liquid surface.");
    }
    private static void SetProgress(BeverageSlot slot, float progress)
    { Set(slot, "pourLeft", (float)Get(slot, "pourDuration") * (1 - progress)); Call(slot, "RefreshPourVisual"); }
    private static GameObject Child(GameObject parent, string name)
    { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void Instance<T>(T value) => typeof(T).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
