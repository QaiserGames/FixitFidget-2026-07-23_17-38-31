#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// No Test Framework assembly setup needed. This checks the actual policy and
// loaded assets in Edit Mode, without changing the scene, assets, or save data.
//
// PLAYTEST 4 (10 Oct 2026; the first-hour playtester's #1 and #2):
//   - A step's card stays until the step is done (DayOneHintTimer): in full, then compact, then in full again with one
//     pulse when the step is stuck; a step seen before comes back compact; a note shows once. ("Expired hint cannot repeat"
//     was the rule that left Grace unheard at the counter with no card on screen.)
//   - The Day 1 the café scene actually plays is tested: its spawner's schedule[0] (Day_01_GraceCamera, the Grace
//     showcase), as well as Day_01_LearnTheShop (the built lesson the scene doesn't schedule). Open the café scene first.
//   - The words: the first card says who you are, the device waits on the intake tray (not the shelf), the card after the
//     featured visit says something true, the impatience card says how to reassure. Read from DayOneGuideUI, where the
//     cards get them.
//   - Arrival times, patience and the customer cap are the Systems lead's numbers: not asserted here any more.
public static class DayOneOnboardingChecks
{
    private const string DayFolder = "Assets/Data/Days/";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Fixit Fidget/Checks/Day 1 onboarding")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running onboarding checks.");

        // The wording checked here is the keyboard's ("Press E"). PadInput remembers the device touched last in a static
        // that outlives Play mode, so right after a lab that drove the bench with its virtual pad this check read the
        // pad's wording and failed (9 Oct night). The keyboard for the check, then whatever was there before.
        MethodInfo setPad = typeof(PadInput).GetProperty("UsingPad", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true);
        bool padWas = PadInput.UsingPad;
        string scheduled;
        try
        {
            setPad?.Invoke(null, new object[] { false });
            CheckSequence();
            CheckHintTiming();
            CheckDefinitions();
            scheduled = CheckScheduledDayOne();
            CheckFeaturedHints();
            CheckWords();
            CheckJobRolls();
        }
        finally
        {
            setPad?.Invoke(null, new object[] { padWas });
        }
        Debug.Log("[Day 1 onboarding] PASS: sequence, failure-safe progression, day isolation, cards that stay until their " +
                  $"step is done (compact, then back in full when stuck), the scene's own Day 1 ({scheduled}), Grace's visit " +
                  "cards, pending drink guidance, the cards' words (who you are, the intake tray, a true end of the visit, " +
                  "reassuring), and opening job selection. Still run the Day 1 guide lab for the cards and pins on screen.");
    }

    private static void CheckHintTiming()
    {
        var full = DayOneHintTimer.Look.Full;
        var compact = DayOneHintTimer.Look.Compact;
        var hidden = DayOneHintTimer.Look.Hidden;
        var timer = new DayOneHintTimer();
        Require(!timer.IsVisible(0f), "No card before the first step.");
        Require(timer.Observe("talk", 10f, 6f), "A new step starts a card.");
        Require(timer.LookAt(15.99f) == full, "In full for six seconds.");
        Require(!timer.Observe("talk", 15.99f, 6f), "Polling the same step changes nothing.");
        Require(timer.LookAt(16f) == compact && timer.IsVisible(16f), "Then compact: it stays on screen until the step is done.");
        Require(timer.LookAt(25.99f) == compact && !timer.Reminding(25.99f), "Compact while the player gets on with it.");
        Require(timer.LookAt(26f) == full && timer.Reminding(26f) && timer.PulseAt(26.3f) > .5f,
            "Stuck ten seconds more: back in full, with one pulse.");
        Require(timer.PulseAt(26.7f) == 0f && timer.LookAt(60f) == full, "One pulse only; then it stays in full.");
        Require(timer.Observe("carry", 61f, 6f) && timer.LookAt(61f) == full, "The next step gets its own card, in full.");
        Require(timer.Observe("talk", 62f, 6f) && timer.LookAt(62f) == compact, "Going back to a step seen before: it comes back compact.");
        Require(timer.LookAt(72f) == full && timer.Reminding(72f), "...and in full again if it's stuck there.");
        timer.Dismiss();
        Require(!timer.IsVisible(73f), "A conversation dismisses the card at once.");
        Require(timer.Observe("talk", 74f, 6f) && timer.LookAt(74f) == compact, "After the conversation the same step comes back, compact.");
        Require(timer.Observe("wait", 80f, 6f, DayOneHintTimer.Kind.Wait) && timer.LookAt(86f) == compact
                && timer.LookAt(200f) == compact && !timer.Reminding(200f), "A waiting card settles and never pulses.");
        Require(timer.Observe("over", 210f, 6f, DayOneHintTimer.Kind.Note) && timer.LookAt(215.99f) == full && timer.LookAt(216f) == hidden,
            "A note shows for its time, then goes.");
        Require(!timer.Observe("over", 220f, 6f, DayOneHintTimer.Kind.Note) && !timer.IsVisible(220f), "A note never comes back.");
        Require(timer.Observe("custom", 230f, 7f) && timer.LookAt(236.99f) == full && timer.LookAt(237f) == compact, "A seven-second setting is honoured.");
        Require(timer.Observe("short", 240f, 1f) && timer.LookAt(242.99f) == full && timer.LookAt(243f) == compact, "Durations are kept within 3-10 s.");
        timer.Reset();
        Require(!timer.IsVisible(250f) && timer.Observe("talk", 250f, 6f) && timer.LookAt(250f) == full, "A fresh day shows every step in full again.");
        Require(!timer.Observe("", 251f, 6f) && !timer.IsVisible(251f), "An empty step hides the card.");
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

    // The Day 1 the café scene plays: its spawner's schedule[0] (Day_01_GraceCamera since the Grace showcase; the first-hour
    // playtester found this check testing Day_01_LearnTheShop, which the scene doesn't schedule).
    private static string CheckScheduledDayOne()
    {
        CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        Require(spawner != null, "The café scene is open: this check reads its spawner's schedule (open AcesCafeLayoutPlaytest first).");
        var serialized = new SerializedObject(spawner);
        SerializedProperty schedule = serialized.FindProperty("schedule");
        Require(schedule != null && schedule.isArray && schedule.arraySize > 0, "The scene's spawner has a schedule.");
        DayDefinition first = schedule.GetArrayElementAtIndex(0).objectReferenceValue as DayDefinition;
        Require(first != null, "The schedule's first day is set.");
        string path = AssetDatabase.GetAssetPath(first);
        Require(first.dayNumber == 1, path + " (the scene's first day) is Day 1.");
        Require(first.GuidesOpeningOn(1) || first.GuidesFeaturedVisitOn(1), path + " guides its first day (the opening lesson or its featured visit).");
        Require(first.openingHintDuration >= 3f && first.openingHintDuration <= 10f, path + ": a card is in full for 3-10 s before it settles.");
        if (first.GuidesFeaturedVisitOn(1))
        {
            Require(!first.GuidesFeaturedVisitOn(2), path + ": its featured hints don't repeat on a later day.");
            string name = first.featuredRegular.characterName;
            string card = DayOneGuideUI.FirstCard(name, true);
            Require(card.StartsWith("You're Ace.") && card.Contains(name) && card.Length <= 90,
                $"{path}: the first card says who you are and who is coming (\"{card}\", {card.Length} characters).");
        }
        // Whoever else comes in on a story visit that day is guided too (the Officer's coffee): said, for the report.
        foreach (StoryVisit visit in first.StoryVisitsOn(1))
            if (visit != null && visit.who != null)
                Debug.Log($"[Day 1 onboarding] {path}: {visit.who.characterName} comes in too (a {visit.who.primaryVisitKind} visit" +
                          $"{(visit.who.preferredDrink != null ? ", " + visit.who.preferredDrink.drinkName : "")}); the guide's cards follow it.");
        return path;
    }

    private static void CheckFeaturedHints()
    {
        string[] paths = { "Assets/GraceShowcase/Day_01_GraceCamera.asset", "Assets/GraceShowcase/Day_02_GraceReturn.asset" };
        foreach (string path in paths)
        {
            DayDefinition day = AssetDatabase.LoadAssetAtPath<DayDefinition>(path);
            Require(day != null && day.GuidesFeaturedVisitOn(day.dayNumber), path + " enables featured hints.");
            Require(!day.guidedOpening, "Featured hints do not restore the old opening sequence.");
            Require(!day.GuidesFeaturedVisitOn(day.dayNumber + 1), "Repeating a day asset cannot repeat its featured hints.");
        }
        CustomerProfile grace = AssetDatabase.LoadAssetAtPath<DayDefinition>(paths[0]).featuredRegular;
        var defaults = ScriptableObject.CreateInstance<DayDefinition>();
        var host = new GameObject("Temporary featured hint check") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false);
        try
        {
            Require(!defaults.showFeaturedVisitHints && !defaults.GuidesFeaturedVisitOn(1), "Unconfigured days remain opt-out.");
            var guide = host.AddComponent<DayOneGuideUI>();
            MethodInfo featuredAction = typeof(DayOneGuideUI).GetMethod("FeaturedAction", PrivateInstance);

            // Before Grace comes in on Day 1: who you are, and who is coming (no Ace here, so "go behind the counter").
            typeof(DayOneGuideUI).GetField("hintDay", PrivateInstance).SetValue(guide, 1);
            string welcome = (string)featuredAction.Invoke(guide, new object[] { null, grace });
            Require(welcome == DayOneGuideUI.FirstCard(grace.characterName, false),
                $"Day 1's first card, before Grace comes in, names Ace (\"{welcome}\").");
            typeof(DayOneGuideUI).GetField("hintDay", PrivateInstance).SetValue(guide, -1);

            var customer = host.AddComponent<CustomerBrain>();
            typeof(CustomerBrain).GetField("wasAccepted", PrivateInstance).SetValue(customer, true);
            typeof(CustomerBrain).GetField("repairReturned", PrivateInstance).SetValue(customer, true);
            typeof(CustomerBrain).GetField("drinkOrdered", PrivateInstance).SetValue(customer, true);
            string next = (string)featuredAction.Invoke(guide, new object[] { customer, null });
            Require(next.StartsWith("Repair returned.") && !next.Contains("intake"),
                "Returning the repair cannot hide an outstanding drink behind a missing-item hint.");
            var job = host.AddComponent<RepairJob>();
            var inspector = host.AddComponent<ItemInspector>();
            typeof(CustomerBrain).GetField("activeJob", PrivateInstance).SetValue(customer, job);
            typeof(ItemInspector).GetField("focusedItem", PrivateInstance).SetValue(inspector, job);
            typeof(DayOneGuideUI).GetField("inspector", PrivateInstance).SetValue(guide, inspector);
            string collection = (string)typeof(DayOneGuideUI).GetMethod("RepairAction", PrivateInstance)
                .Invoke(guide, new object[] { customer });
            Require(collection.Contains("Press E") && !collection.Contains("Right-click"),
                "Completed inspection explains direct E collection, without the old exit-first sequence.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(defaults);
        }
    }

    // The cards' words, read from DayOneGuideUI (where the cards get them): short, true, and in the player's words.
    private static void CheckWords()
    {
        string there = DayOneGuideUI.FirstCard("Grace", true), away = DayOneGuideUI.FirstCard("Grace", false);
        Require(there.StartsWith("You're Ace.") && there.Contains("Grace") && !there.Contains("behind the counter") && there.Length <= 90,
            $"Behind the counter already, the first card doesn't send Ace there (\"{there}\").");
        Require(away.StartsWith("You're Ace.") && away.Contains("behind the counter") && away.Length <= 90,
            $"Elsewhere, it does (\"{away}\").");
        string over = DayOneGuideUI.VisitOver("Grace", 0), open = DayOneGuideUI.VisitOver("Grace", 2);
        Require(!over.Contains("request") && !over.Contains("order") && over.Length <= 90,
            $"With nothing open, the card after the visit says nothing about requests (\"{over}\").");
        Require(open.Contains("open orders") && open.Length <= 90, $"With orders open, it says to finish them (\"{open}\").");
        string tray = DayOneGuideUI.PickUpFromTray("Grace", "reunion film camera");
        Require(tray.Contains("intake tray") && !tray.Contains("shelf") && tray.StartsWith("Press E") && tray.Length <= 90,
            $"The device waits on the intake tray (\"{tray}\").");
        string calm = DayOneGuideUI.ReassureWords("Walter");
        Require(calm.Contains("Walter") && calm.Contains("press E") && calm.Length <= 90, $"The impatience card says how to reassure (\"{calm}\").");
        string atBench = DayOneGuideUI.BenchReassureWords("Walter");
        Require(atBench.Contains("Walter") && atBench.Contains("[Right-click]") && atBench.Contains("[E]") && WordBudget.Words(atBench) <= WordBudget.BenchAlert,
            $"At the bench the impatience card says to step back first, in the bench's keys (\"{atBench}\").");
        Require(DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, true) == DropInteractable.PutBackWords
                && DropInteractable.TrayWords(DropSpot.SpotKind.Counter, true, false) == DropInteractable.IntakeTrayWords,
            "At the intake tray a device lifted from it reads \"Put it back\", any other \"Put it on the intake tray\".");
        Require(DayOneGuideUI.TalkWords("the Officer", false, true).Contains("E takes the order")
                && DayOneGuideUI.TalkWords("Grace", false, false).Contains("E takes the job")
                && DayOneGuideUI.TalkWords("Grace", true, false).StartsWith("Look at Grace."),
            "Talking takes the job (or the order, for a drink), in the words of the view in use.");
        Require(DayOneGuideUI.Possessive("Grace") == "Grace's" && DayOneGuideUI.Possessive("the Officer") == "the Officer's"
                && DayOneGuideUI.Possessive("Agnes") == "Agnes'", "Names take their possessive.");
        Require(FocusNameUI.BenchName == "Repair bench" && FocusNameUI.TrayName == "Intake tray" && FocusNameUI.DrinksName == "Drinks",
            "The stations' names are words a player knows.");
    }

    private static DayDefinition DayOne() =>
        AssetDatabase.LoadAssetAtPath<DayDefinition>(DayFolder + "Day_01_LearnTheShop.asset");

    // The built lesson (Day_01_LearnTheShop: one drink, then one repair). The scene schedules the Grace showcase instead
    // (CheckScheduledDayOne), but the lesson stays built and correct. Its arrival time, patience and cap are the Systems
    // lead's numbers (not asserted here any more).
    private static void CheckDefinitions()
    {
        DayDefinition first = DayOne();
        Require(first != null, "Day 1 asset exists.");
        Require(first.GuidesOpeningOn(1) && first.SuppressesRepairDrinksOn(1), "Day 1 opts in.");
        Require(!first.GuidesOpeningOn(2) && !first.SuppressesRepairDrinksOn(2), "Repeating Day 1 cannot leak rules to Day 2.");
        Require(first.openingDrink != null && first.openingDrink.drinkName == "Coffee", "First drink is Coffee.");
        Require(Mathf.Approximately(first.openingHintDuration, 6f), "Day 1's cards are in full for six seconds before they settle.");
        Require(first.featuredRegular != null, "Featured visit retained.");
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
