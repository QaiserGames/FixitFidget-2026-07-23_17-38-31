#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Synthetic visits and an inactive identity object. Never loads/writes the
// player's save, spawns visitors, edits assets, or awards money.
public static class CustomerMemoryChecks
{
    [MenuItem("Fixit Fidget/Checks/Customer memory and identity")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running customer-memory checks.");

        UnityEngine.Random.State random = UnityEngine.Random.state;
        try
        {
            CheckStorageAndTrust();
            CheckOutcomes();
            CheckRoster();
            CheckAuthoredIdentity();
        }
        finally { UnityEngine.Random.state = random; }

        Debug.Log("[Customer memory] PASS: save compatibility, snapshot isolation, outcome callbacks, " +
                  "name reservations, repeat intake, and portrait fallbacks. " +
                  "Use grace-showcase-checklist.md for the two-visit Play Mode check.");
    }

    private static void CheckStorageAndTrust()
    {
        var service = new CustomerMemoryService();
        Require(service.Read("grace") == null && service.Snapshot().Length == 0, "Reading an unknown regular creates no visit.");
        service.RecordVisit("grace", 1, true, true, true, LostReason.StormedOutWaiting, "Perfect");
        RegularMemoryData first = service.Read("grace");
        Require(first.visits == 1 && first.relationship == 2 && first.lastSeenDay == 1 && first.lastLossReason == "",
            "A completed first visit preserves the existing trust tuning.");

        SaveData save = new SaveData { regularMemories = service.Snapshot() };
        SaveData roundTrip = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        roundTrip.ValidateAndMigrate();
        var restored = new CustomerMemoryService();
        restored.Restore(roundTrip.regularMemories);
        Require(restored.Read("grace").lastGrade == "Perfect" && restored.Read("grace").visits == 1,
            "Existing v3 memory survives JSON and restoration without replaying a visit.");

        first.visits = 99;
        roundTrip.regularMemories[0].lastGrade = "Rejected";
        save.regularMemories[0].relationship = -4;
        Require(service.Read("grace").visits == 1 && service.Read("grace").relationship == 2
                && restored.Read("grace").lastGrade == "Perfect", "Reads, snapshots, and restored input cannot mutate live history.");

        for (int day = 2; day <= 5; day++)
            service.RecordVisit("grace", day, true, true, true, LostReason.StormedOutWaiting, "Good");
        Require(service.Read("grace").relationship == 6, "Trust retains its upper bound.");
        service.RecordVisit("grace", 6, false, true, false, LostReason.StormedOutWaiting, "");
        RegularMemoryData missed = service.Read("grace");
        Require(missed.relationship == 4 && missed.visits == 6 && missed.lastSeenDay == 6,
            "A failed visit updates the same regular once and retains the existing penalty.");
        Require(!CustomerReturnPolicy.AllowsWarmDialogue(CustomerReturnPolicy.Classify(missed)),
            "High accumulated trust cannot erase a failed latest visit.");

        service.RecordVisit("partial", 1, true, true, true, LostReason.OutOfStock, "Good");
        Require(service.Read("partial").lastLossReason == "OutOfStock"
                && CustomerReturnPolicy.Classify(service.Read("partial")) == CustomerReturnOutcome.IncompleteService,
            "A returned repair plus an apologised-for missing drink retains both facts.");

        service.Restore(null);
        Require(service.Snapshot().Length == 0, "Old saves with no regular history stay empty.");
    }

    private static RegularMemoryData Visit(bool happy, bool served, string grade = "", string reason = "") =>
        new RegularMemoryData { profileId = "grace", visits = 1, relationship = 4,
            lastVisitHappy = happy, lastVisitServed = served, lastGrade = grade, lastLossReason = reason };

    private static void CheckOutcomes()
    {
        Require(CustomerReturnPolicy.Classify(null) == CustomerReturnOutcome.FirstVisit, "No record means a first visit.");
        var cases = new (RegularMemoryData memory, CustomerReturnOutcome expected)[]
        {
            (Visit(true, true, "Perfect"), CustomerReturnOutcome.SuccessfulRepair),
            (Visit(true, true, "Good"), CustomerReturnOutcome.SuccessfulRepair),
            (Visit(true, true, "Passable"), CustomerReturnOutcome.ImperfectRepair),
            (Visit(true, true, "Rejected"), CustomerReturnOutcome.RejectedRepair),
            (Visit(false, true, "", "StormedOutWaiting"), CustomerReturnOutcome.IncompleteService),
            (Visit(false, true, "Good", "StormedOutWaiting"), CustomerReturnOutcome.IncompleteService),
            (Visit(true, true, "Good", "OutOfStock"), CustomerReturnOutcome.IncompleteService),
            (Visit(false, false, "", "StormedOutInQueue"), CustomerReturnOutcome.MissedVisit),
            (Visit(false, false, "", "Declined"), CustomerReturnOutcome.DeclinedVisit),
            (Visit(false, false, "", "ShelfFull"), CustomerReturnOutcome.CapacityRefusal),
            (Visit(false, false, "", "OutOfStock"), CustomerReturnOutcome.CapacityRefusal),
            (Visit(true, true), CustomerReturnOutcome.ServedVisit),
            (Visit(true, false), CustomerReturnOutcome.UnknownReturn)
        };
        foreach (var entry in cases)
            Require(CustomerReturnPolicy.Classify(entry.memory) == entry.expected, "Classify " + entry.expected);
    }

    private static void CheckRoster()
    {
        var roster = new CustomerVisitRoster();
        roster.Reset(new[] { "Grace", " grace ", "Alex", "alex", "Cody", "", null }, new[] { "Grace", "Walk-in 1" });
        Require(roster.RemainingNames == 2, "Regular names, blank names, and duplicate walk-in names are excluded.");
        Require(roster.TakeName(0) == "Alex" && roster.TakeName(0) == "Cody", "Walk-in names are consumed once each.");
        Require(roster.TakeName(0) == "Walk-in 2" && roster.TakeName(0) == "Walk-in 3", "Exhaustion yields unique reserved-safe names.");
        Require(!roster.CanVisitRandomly("grace", "grace") && roster.CanVisit("grace"),
            "Random arrivals cannot steal the featured regular's scheduled visit.");
        roster.RecordArrival("grace");
        Require(!roster.CanVisit("grace") && !roster.CanVisitRandomly("grace", null), "A regular visits at most once in a day.");
        roster.Reset(null, new[] { "Grace" });
        Require(roster.CanVisit("grace") && roster.TakeName(0) == "Walk-in 1", "The next day releases arrival locks safely.");
    }

    private static void CheckAuthoredIdentity()
    {
        CustomerProfile grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>("Assets/Data/Regulars/Regular_Grace.asset");
        Require(grace != null && grace.PersistentId == "grace", "Grace's stable save identity is preserved.");
        var root = new GameObject("CustomerMemoryCheck") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        CustomerProfile copy = UnityEngine.Object.Instantiate(grace);
        Sprite neutral = null;
        Sprite surprise = null;
        try
        {
            var identity = root.AddComponent<CustomerIdentity>();
            identity.SetDevice("pocket watch");
            identity.SetFault("Jammed Gears");
            identity.SetupRegular(copy, Visit(false, false, "", "StormedOutWaiting"));
            string line = identity.Say(CustomerIdentity.Beat.Intake);
            Require(line.Contains("ran out of time") && line.Contains("pocket watch") && line.Contains("jammed gears")
                    && !line.Contains("{device}") && !line.Contains("{fault}"), "A missed visit uses the factual callback with current job tokens.");

            identity.SetupRegular(copy, Visit(true, true, "Rejected"));
            Require(identity.Say(CustomerIdentity.Beat.Intake).Contains("unfinished"), "A rejected repair cannot receive the warm success callback.");
            identity.SetupRegular(copy, Visit(true, true));
            Require(identity.Say(CustomerIdentity.Beat.Intake).Contains("familiar counter"), "Drink-only service does not invent a repaired device.");
            Require(identity.SayRepairCompleted(JobGrade.Rejected).Contains("still needs work")
                    && identity.Expression == PortraitExpression.Impatient, "Rejected handback has a matching response.");

            foreach (CustomerReturnOutcome outcome in new[] { CustomerReturnOutcome.SuccessfulRepair, CustomerReturnOutcome.ImperfectRepair,
                CustomerReturnOutcome.RejectedRepair, CustomerReturnOutcome.IncompleteService, CustomerReturnOutcome.MissedVisit,
                CustomerReturnOutcome.DeclinedVisit, CustomerReturnOutcome.CapacityRefusal, CustomerReturnOutcome.ServedVisit })
                Require(Array.Exists(copy.returnMemoryLines.For(outcome) ?? Array.Empty<string>(), s => !string.IsNullOrWhiteSpace(s)),
                    "Grace has authored text for " + outcome);

            // Re-entering intake must retain the exact line already heard.
            var brain = root.AddComponent<CustomerBrain>();
            SetField(brain, "identity", identity);
            SetField(brain, "state", CustomerBrain.State.WaitingInQueue);
            string firstIntake = brain.HearIntake();
            Require(!string.IsNullOrWhiteSpace(firstIntake) && brain.HearIntake() == firstIntake,
                "Walking away and reopening intake never returns an empty box or a rerolled callback.");

            copy.portraitNeutral = copy.portraitHappy = copy.portraitAnnoyed = copy.portraitSad = copy.portraitSurprised = null;
            foreach (PortraitExpression expression in Enum.GetValues(typeof(PortraitExpression)))
                Require(copy.PortraitFor(expression) == null, "No portrait art is required for " + expression);
            neutral = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            surprise = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            copy.portraitNeutral = neutral;
            copy.portraitSurprised = surprise;
            Require(copy.PortraitFor(PortraitExpression.Happy) == neutral && copy.PortraitFor(PortraitExpression.Surprised) == surprise,
                "Missing expressions fall back to neutral; assigned expressions are used.");
            identity.Say(CustomerIdentity.Beat.Intake);
            Require(identity.ExpressionAt(0.2f) == PortraitExpression.Impatient, "Low patience can change the intake portrait.");
            identity.Say(CustomerIdentity.Beat.Accepted);
            Require(identity.ExpressionAt(0.2f) == PortraitExpression.Happy, "The response takes precedence over low-patience intake.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(copy);
            if (neutral != null) UnityEngine.Object.DestroyImmediate(neutral);
            if (surprise != null) UnityEngine.Object.DestroyImmediate(surprise);
        }
    }

    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Customer memory] FAIL: " + message);
    }
}
#endif
