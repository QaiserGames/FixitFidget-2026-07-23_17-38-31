#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// No Test Framework assembly setup needed. This checks the actual policy and
// loaded assets in Edit Mode, without changing the scene, assets, or save data.
public static class DayOneOnboardingChecks
{
    private const string DayFolder = "Assets/Data/Days/";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Day 1 onboarding")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running onboarding checks.");

        CheckSequence();
        CheckHintTiming();
        CheckDefinitions();
        CheckJobRolls();
        Debug.Log("[Day 1 onboarding] PASS: sequence, failure-safe progression, " +
                  "day isolation, Grace timing, opening job selection, and three-second hint timing. " +
                  "Still run the in-game checklist for input, navigation, and HUD layout.");
    }

    private static void CheckHintTiming()
    {
        var timer = new DayOneHintTimer();
        Require(!timer.IsVisible(0f), "No hint before the first action.");
        Require(timer.Observe("drink:cup", 10f), "A new action starts a toast.");
        Require(timer.IsVisible(12.99f), "Hint remains visible for three seconds.");
        Require(!timer.Observe("drink:cup", 12.99f), "Polling cannot restart the timer.");
        Require(!timer.IsVisible(13f), "Hint disappears at three seconds.");
        Require(!timer.Observe("drink:cup", 50f) && !timer.IsVisible(50f), "Expired hint cannot repeat.");
        Require(timer.Observe("drink:brew", 51f), "Next action gets its own hint.");
        Require(!timer.Observe("drink:cup", 52f) && !timer.IsVisible(52f), "Returning to an old action clears stale text without replay.");
        Require(timer.Observe("drink:serve", 60f), "Delivery is a new action.");
        timer.Dismiss();
        Require(!timer.IsVisible(61f), "Conversation/disable dismisses immediately.");
        Require(!timer.Observe("drink:serve", 62f) && !timer.IsVisible(62f), "Closing dialogue does not replay the old toast.");
        Require(timer.Observe("repair:cup", 70f), "Separate lesson may teach the same action.");
        timer.Reset();
        Require(!timer.IsVisible(70f), "Reset hides the current hint.");
        Require(timer.Observe("drink:cup", 80f), "Fresh run can show hints again.");
        Require(!timer.Observe("", 81f) && !timer.IsVisible(81f), "Empty action hides the panel.");
    }

    private static void CheckSequence()
    {
        var lesson = new DayOneOpening();
        Require(!lesson.IsActive && !lesson.TryStartVisit(), "Default is inactive.");
        lesson.Reset(false);
        Require(!lesson.FinishVisit(), "Disabled cannot advance.");
        Require(lesson.AllowsFeatured(0.55f, 0.55f), "Disabled lesson keeps original featured timing.");
        Require(!lesson.AllowsFeatured(0.54f, 0.55f), "Featured cannot arrive early.");

        lesson.Reset(true);
        Require(lesson.Current == DayOneOpening.Step.Drink, "Drink is first.");
        Require(!lesson.FinishVisit(), "Cannot advance before arrival.");
        Require(!lesson.AllowsFeatured(0.9f, 0.55f), "Featured cannot replace first drink.");
        Require(lesson.TryStartVisit(), "First customer starts.");
        Require(!lesson.TryStartVisit(), "No overlapping lesson customers.");
        Require(lesson.FinishVisit(), "Served, refused, timed out, or destroyed all end a visit.");
        Require(lesson.Current == DayOneOpening.Step.Repair, "Repair follows drink.");
        Require(!lesson.FinishVisit(), "Duplicate departure cannot skip repair.");
        Require(!lesson.AllowsFeatured(0.9f, 0.55f), "Featured cannot replace first repair.");
        Require(lesson.TryStartVisit() && lesson.FinishVisit(), "Repair visit can end.");
        Require(lesson.Current == DayOneOpening.Step.Complete && !lesson.IsActive, "Lesson ends.");
        Require(!lesson.TryStartVisit(), "No third guided visit.");
        Require(!lesson.AllowsFeatured(0.54f, 0.55f), "Early completion does not summon Grace early.");
        Require(lesson.AllowsFeatured(0.55f, 0.55f), "Grace is eligible at authored time after lessons.");

        lesson.Reset(true);
        lesson.TryStartVisit();
        lesson.Reset(false);
        Require(!lesson.IsActive && !lesson.VisitInProgress, "Later day clears an unfinished lesson.");
        lesson.Reset(true);
        Require(lesson.Current == DayOneOpening.Step.Drink && !lesson.VisitInProgress, "Fresh Day 1 restarts cleanly.");
    }

    private static DayDefinition DayOne() =>
        AssetDatabase.LoadAssetAtPath<DayDefinition>(DayFolder + "Day_01_LearnTheShop.asset");

    private static void CheckDefinitions()
    {
        DayDefinition first = DayOne();
        Require(first != null, "Day 1 asset exists.");
        Require(first.GuidesOpeningOn(1) && first.SuppressesRepairDrinksOn(1), "Day 1 opts in.");
        Require(!first.GuidesOpeningOn(2) && !first.SuppressesRepairDrinksOn(2), "Repeating Day 1 cannot leak rules to Day 2.");
        Require(first.openingDrink != null && first.openingDrink.drinkName == "Coffee", "First drink is Coffee.");
        Require(first.maxCustomers == 3 && Mathf.Approximately(first.patienceMultiplier, 1.4f), "Original cap and patience retained.");
        Require(first.featuredRegular != null && Mathf.Approximately(first.featuredRegularArrivesAt, 0.55f), "Featured visit and 55% timing retained.");
        Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first.featuredRegular))
                == "dfd14ad338d46de49bd81a423294b439", "Featured profile is still Grace.");
        Require(first.devices != null && first.devices.Length > 0 && first.devices[0] != null, "Day 1 has a repair prefab.");

        string[] later = { "Day_02_Juggle", "Day_03_TheRealGame", "Day_04_Pressure", "Day_05_TwoRushes" };
        foreach (string asset in later)
        {
            DayDefinition day = AssetDatabase.LoadAssetAtPath<DayDefinition>(DayFolder + asset + ".asset");
            Require(day != null, asset + " exists.");
            Require(!day.GuidesOpeningOn(day.dayNumber) && !day.SuppressesRepairDrinksOn(day.dayNumber),
                asset + " retains normal gameplay.");
        }

        DayDefinition defaults = ScriptableObject.CreateInstance<DayDefinition>();
        try
        {
            Require(!defaults.guidedOpening && !defaults.suppressRepairDrinkWishes,
                "Old/unconfigured day assets are opt-out by default.");
        }
        finally { UnityEngine.Object.DestroyImmediate(defaults); }
    }

    private static void CheckJobRolls()
    {
        GameObject host = new GameObject("Temporary onboarding check") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false);
        UnityEngine.Random.State random = UnityEngine.Random.state;
        try
        {
            CustomerSpawner spawner = host.AddComponent<CustomerSpawner>();
            DayDefinition day = DayOne();
            Set(spawner, "today", day);
            Set(spawner, "lastSeenDay", 1);
            Set(spawner, "openingDrink", day.openingDrink);
            Set(spawner, "drinks", new[] { day.openingDrink });
            Set(spawner, "devicePrefabs", day.devices);

            // Exercise the actual job factory across different RNG states.
            for (int seed = 0; seed < 20; seed++)
            {
                UnityEngine.Random.InitState(seed);
                Job drink = (Job)Call(spawner, "RollJob", null, (JobKind?)JobKind.Drink);
                Require(drink.kind == JobKind.Drink && drink.drink == day.openingDrink, "Forced Coffee ignores random roll.");
                Job repair = (Job)Call(spawner, "RollJob", null, (JobKind?)JobKind.Repair);
                Require(repair.kind == JobKind.Repair && Array.IndexOf(day.devices, repair.devicePrefab) >= 0,
                    "Forced repair uses Day 1's device pool.");
                Require(Call(spawner, "RollDrinkWish", null, repair) == null, "Day 1 repair has no secondary drink.");
            }
        }
        finally
        {
            UnityEngine.Random.state = random;
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void Set(object target, string field, object value) =>
        typeof(CustomerSpawner).GetField(field, PrivateInstance).SetValue(target, value);

    private static object Call(object target, string method, params object[] args) =>
        typeof(CustomerSpawner).GetMethod(method, PrivateInstance).Invoke(target, args);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Day 1 onboarding] FAIL: " + message);
    }
}
#endif
