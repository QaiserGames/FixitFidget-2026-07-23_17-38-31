#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// UPGRADE ECONOMY CHECK — 12 October 2026 (Progression & Economy, playtest 4 §10.10:
// "level 1 at about half a day")
//
// An edit-mode check, read-only. Two halves:
//
// 1. THE LADDER. Reads the six Upgrade_* assets and holds them against
//    UpgradeEconomyPass's table: the numbers are the pass's (else the pass has
//    not been run), each ladder climbs, every first level sits in the band
//    chosen for it ($100-250: 0.4-1.0 of a realistic $250 day), each next level
//    is at least twice the one before (slowing as levels climb), the structural
//    two (Shelf Space, Bigger Bench) cost at least as much as every tool at the
//    same level, the bench over the shelf; and, with the café scene open, all
//    six are in UpgradeManager's catalogue.
//
// 2. THE FIRST PURCHASE, FROM THE DAY DEFINITIONS. Plays the scene's schedule
//    (Day 1-5) through by its own numbers: the spawner's clock (phases read at
//    the counter, the walk in, the opener, the day's cap, three counter places,
//    the featured and story visits), the device prefabs' fault payouts, the
//    drinks' prices, the archetypes' patience / tip / wish, the patrons' $5,
//    the customer's patience and tip rules, with one pair of hands at a
//    person's pace (the café lab's human-speed numbers: a 30 s drink, a 67 s
//    bench repair, 4 s of talk) and again at a good player's pace. Each day is
//    played 300 times with fixed dice, so the numbers are the same every run:
//    a number, not a guess. It reports what each day earns (median, quartiles,
//    mean), the recap at which each first level is first affordable, and the
//    recap at which the whole catalogue could be owned, and fails if the
//    cheapest first level is not affordable at the first recap for a steady
//    player, or if a good player could own everything before the demo's Day 3
//    ends (the loop needs scarcity).
//
// What the model leaves out, on purpose: reassures, declines, grades below
// Perfect, walking between stations beyond the lab's bands, the restock's $30,
// the night. His own days are the ground truth; this is the arithmetic beside
// them. Report: Logs/UpgradeEconomy/check-<time>/report.txt and the Console.
// ---------------------------------------------------------------------------
public static class UpgradeEconomyChecks
{
    private const string Menu = "Fixit Fidget/Checks/Upgrade economy - the ladder and the first purchase";
    private const string ScenePath = "Assets/Playtests/AcesCafeLayoutPlaytest.unity";
    private const string Tag = "[Upgrade economy] ";

    /// <summary>The band a first level must sit in (12 Oct 2026): 0.4-1.0 of the pass's realistic day.</summary>
    public const int FirstLevelMin = 100;
    public const int FirstLevelMax = 250;
    /// <summary>Each next level costs at least this many times the one before (the pass's shape: 2.4-2.8).</summary>
    public const float SlowingRatio = 2f;
    /// <summary>The demo's last day: nobody may own the whole catalogue before this recap.</summary>
    public const int DemoLastDay = 3;

    private const int Runs = 300;
    private const int Seed = 20261012;
    private const int DaysToOwnHorizon = 40;

    private static readonly StringBuilder report = new();
    private static int checks, failures;

    [MenuItem(Menu)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(Tag + "Stop Play Mode before running this check.");
            return;
        }
        report.Clear();
        checks = failures = 0;
        report.AppendLine($"Upgrade economy check - {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"Scene: {SceneManager.GetActiveScene().path}");
        report.AppendLine();
        List<UpgradeEconomyPass.Entry> ladder = null;
        try
        {
            ladder = CheckLadder();
            CheckDays(ladder);
        }
        catch (Exception exception)
        {
            Check(false, "Check stopped by an exception: " + exception.Message);
            Debug.LogException(exception);
        }
        Finish();
    }

    // ------------------------------------------------------------------ 1. the ladder

    private static List<UpgradeEconomyPass.Entry> CheckLadder()
    {
        Line("== 1. The ladder (the assets against UpgradeEconomyPass's table) ==");
        List<UpgradeEconomyPass.Entry> plan = UpgradeEconomyPass.PlannedLadder();

        Dictionary<string, UpgradeDefinition> assets = new();
        foreach (string guid in AssetDatabase.FindAssets("t:UpgradeDefinition"))
        {
            UpgradeDefinition u = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (u != null && !assets.ContainsKey(u.name)) assets.Add(u.name, u);
        }

        int cheapestFirst = int.MaxValue, dearestFirst = 0, dearestToolFirst = 0, whole = 0, firstLevels = 0;
        UpgradeDefinition shelf = null, bench = null;
        HashSet<string> planned = new();
        // The ladder as the player meets it: the assets' numbers where the asset exists, the pass's where it doesn't yet.
        List<UpgradeEconomyPass.Entry> effective = new();
        foreach (UpgradeEconomyPass.Entry e in plan)
        {
            planned.Add(e.assetName);
            assets.TryGetValue(e.assetName, out UpgradeDefinition u);
            if (!Check(u != null, $"{e.displayName}: the asset {UpgradeEconomyPass.Folder}/{e.assetName}.asset exists (else run Content › 7)"))
            {
                effective.Add(e);
                continue;
            }

            int[] costs = u.costPerLevel ?? Array.Empty<int>();
            UpgradeEconomyPass.Entry inPlay = e;
            inPlay.costs = (int[])costs.Clone();
            effective.Add(inPlay);
            bool same = costs.Length == e.costs.Length;
            for (int i = 0; same && i < costs.Length; i++) same = costs[i] == e.costs[i];
            Check(same, $"{e.displayName}: the asset holds the pass's numbers ({Costs(costs)}; the pass says {Costs(e.costs)}; else run Content › 7)");
            Check(u.type == e.type, $"{e.displayName}: type {u.type} is the pass's {e.type}");
            Check(u.valuePerLevel > 0f, $"{e.displayName}: value per level {u.valuePerLevel:0.##} is above zero (a purchase that changes nothing is a bug)");
            if (costs.Length == 0) { Check(false, $"{e.displayName}: has at least one level"); continue; }

            Check(costs[0] >= FirstLevelMin && costs[0] <= FirstLevelMax,
                $"{e.displayName}: level 1 ${costs[0]} is inside the band ${FirstLevelMin}-{FirstLevelMax} (0.4-1.0 of a ${UpgradeEconomyPass.ReferenceDay} day)");
            for (int i = 1; i < costs.Length; i++)
            {
                Check(costs[i] > costs[i - 1], $"{e.displayName}: level {i + 1} ${costs[i]} costs more than level {i} ${costs[i - 1]}");
                Check(costs[i] >= costs[i - 1] * SlowingRatio,
                    $"{e.displayName}: level {i + 1} is at least {SlowingRatio:0.#}x level {i} (${costs[i]} against ${costs[i - 1]}: slowing as levels climb)");
            }

            cheapestFirst = Mathf.Min(cheapestFirst, costs[0]);
            dearestFirst = Mathf.Max(dearestFirst, costs[0]);
            if (!e.structural) dearestToolFirst = Mathf.Max(dearestToolFirst, costs[0]);
            firstLevels += costs[0];
            foreach (int c in costs) whole += c;
            if (e.type == UpgradeType.ShelfCapacity) shelf = u;
            if (e.type == UpgradeType.BenchCapacity) bench = u;
        }

        foreach (KeyValuePair<string, UpgradeDefinition> stray in assets)
            if (!planned.Contains(stray.Key))
                Check(false, $"{stray.Key} is an UpgradeDefinition the pass does not know (unpriced; add it to the table or remove it)");

        if (shelf != null && bench != null && shelf.costPerLevel != null && bench.costPerLevel != null)
        {
            Check(shelf.costPerLevel[0] >= dearestToolFirst, $"Shelf Space level 1 ${shelf.costPerLevel[0]} is at least every tool's level 1 (${dearestToolFirst}): structural costs more");
            Check(bench.costPerLevel[0] >= shelf.costPerLevel[0], $"Bigger Bench level 1 ${bench.costPerLevel[0]} is at least Shelf Space's ${shelf.costPerLevel[0]}: the dearest first level");
            int shared = Mathf.Min(shelf.costPerLevel.Length, bench.costPerLevel.Length);
            for (int i = 1; i < shared; i++)
                Check(bench.costPerLevel[i] >= shelf.costPerLevel[i], $"Bigger Bench level {i + 1} ${bench.costPerLevel[i]} is at least Shelf Space's ${shelf.costPerLevel[i]}");
        }

        Line($"Level 1s add up to ${firstLevels}; cheapest ${cheapestFirst}, dearest ${dearestFirst}; the whole catalogue ${whole:n0} " +
             $"({whole / (float)UpgradeEconomyPass.ReferenceDay:0.0} realistic days of ${UpgradeEconomyPass.ReferenceDay}).");

        // The catalogue in the open scene (the pass no longer wires it).
        if (SceneManager.GetActiveScene().path == ScenePath)
        {
            UpgradeManager manager = Object.FindAnyObjectByType<UpgradeManager>();
            if (Check(manager != null, "The café scene has an UpgradeManager"))
            {
                UpgradeDefinition[] catalogue = manager.Catalogue ?? Array.Empty<UpgradeDefinition>();
                HashSet<UpgradeDefinition> seen = new();
                int nulls = 0, duplicates = 0;
                foreach (UpgradeDefinition c in catalogue)
                {
                    if (c == null) nulls++;
                    else if (!seen.Add(c)) duplicates++;
                }
                Check(nulls == 0 && duplicates == 0, $"The catalogue has no empty or repeated entries ({nulls} empty, {duplicates} repeated)");
                foreach (UpgradeEconomyPass.Entry e in plan)
                {
                    assets.TryGetValue(e.assetName, out UpgradeDefinition u);
                    Check(u != null && seen.Contains(u), $"{e.displayName} is in UpgradeManager's catalogue (the Shop can sell it)");
                }
            }
        }
        else Line("(The café scene is not open: the catalogue's wiring was not checked.)");
        Line("");
        return effective;
    }

    // ------------------------------------------------------------------ 2. the days

    private static void CheckDays(List<UpgradeEconomyPass.Entry> ladder)
    {
        Line("== 2. The first purchase, from the day definitions ==");
        if (!Check(SceneManager.GetActiveScene().path == ScenePath, "The café scene is open (its spawner holds the schedule, the archetypes, the drinks and the devices)"))
            return;

        World world = ReadWorld();
        if (world == null) return;
        Line(world.Describe());
        Line(HisLoggedDays());
        Line("");

        int cheapest = int.MaxValue, dearest = 0, whole = 0, firstLevels = 0;
        foreach (UpgradeEconomyPass.Entry e in ladder)
        {
            if (e.costs == null || e.costs.Length == 0) continue;
            cheapest = Mathf.Min(cheapest, e.costs[0]);
            dearest = Mathf.Max(dearest, e.costs[0]);
            firstLevels += e.costs[0];
            foreach (int c in e.costs) whole += c;
        }

        Pace[] paces = { world.steady, world.good };
        float[] medianWholeRecap = new float[2];
        float[] shareWholeByDemo = new float[2];
        float[] shareCheapestAtFirst = new float[2];
        float[] medianDearestRecap = new float[2];
        for (int p = 0; p < paces.Length; p++)
        {
            Pace pace = paces[p];
            Line($"-- {pace.name}: {pace.Describe()} --");
            Line($"{"day",-24} {"at the till",11} {"served",7} {"lost",5} {"work $",14} {"tips $",7} {"patrons $",9} {"TOTAL $ (q1 / median / q3, mean)",34}");
            int days = world.schedule.Count;
            // runs x days: every run plays each day once, the same dice every time the check runs
            int[][] totals = new int[Runs][];
            for (int r = 0; r < Runs; r++) totals[r] = new int[days];
            for (int d = 0; d < days; d++)
            {
                DayDefinition day = world.schedule[d];
                List<Result> results = new();
                for (int r = 0; r < Runs; r++)
                {
                    var rng = new System.Random(Seed + 100000 * p + 1000 * r + d);
                    Result result = DaySim.Play(world, day, day.dayNumber, pace, rng);
                    results.Add(result);
                    totals[r][d] = result.total;
                }
                Line($"{day.name,-24} {Mean(results, x => x.arrived),11:0.0} {Mean(results, x => x.served),7:0.0} {Mean(results, x => x.lost),5:0.0} " +
                     $"{Quartiles(results, x => x.work),14} {Median(results, x => x.tips),7:0} {Median(results, x => x.patrons),9:0} " +
                     $"{Quartiles(results, x => x.total),28}, {Mean(results, x => x.total),5:0}");
            }

            // Cumulative takings at each recap, the last day repeated past the schedule (the spawner repeats it).
            int[][] cumulative = new int[Runs][];
            for (int r = 0; r < Runs; r++)
            {
                cumulative[r] = new int[DaysToOwnHorizon];
                int sum = 0;
                for (int d = 0; d < DaysToOwnHorizon; d++)
                {
                    sum += totals[r][Mathf.Min(d, days - 1)];
                    cumulative[r][d] = sum;
                }
            }
            StringBuilder cumLine = new("Cumulative at the recap (median): ");
            for (int d = 0; d < days; d++) cumLine.Append($"Day {d + 1} ${MedianAt(cumulative, d)}  ");
            Line(cumLine.ToString());

            foreach (UpgradeEconomyPass.Entry e in ladder)
                if (e.costs != null && e.costs.Length > 0)
                    Line($"  {e.displayName,-16} level 1 ${e.costs[0],4}: {RecapWords(cumulative, e.costs[0])}");
            Line($"  {"all six level 1s",-16}         ${firstLevels,4}: {RecapWords(cumulative, firstLevels)}");
            Line($"  {"the whole catalogue",-16}      ${whole,4}: {RecapWords(cumulative, whole)}");

            shareCheapestAtFirst[p] = ShareAffordableBy(cumulative, cheapest, 1);
            medianDearestRecap[p] = MedianRecap(cumulative, dearest);
            medianWholeRecap[p] = MedianRecap(cumulative, whole);
            shareWholeByDemo[p] = ShareAffordableBy(cumulative, whole, DemoLastDay);
            Line("");
        }

        // The verdicts his call asked for.
        Check(shareCheapestAtFirst[0] >= .5f,
            $"The cheapest first level (${cheapest}) is affordable at the FIRST recap for a steady player in {shareCheapestAtFirst[0] * 100f:0}% of Day 1s (the first purchase lands about half a day in)");
        Check(medianDearestRecap[0] > 0 && medianDearestRecap[0] <= 2,
            $"The dearest first level (${dearest}) is affordable by the second recap for a steady player (median recap {medianDearestRecap[0]:0})");
        Check(medianDearestRecap[1] > 0 && medianDearestRecap[1] <= 1,
            $"The dearest first level (${dearest}) is affordable at the first recap for a good player (median recap {medianDearestRecap[1]:0}: no first level is more than a day)");
        Check(shareWholeByDemo[1] <= .01f,
            $"A good player cannot own the whole catalogue (${whole:n0}) by the Day {DemoLastDay} recap ({shareWholeByDemo[1] * 100f:0}% of runs could; median recap {RecapOrNever(medianWholeRecap[1])})");
        Line($"Expected first purchase: recap 1 (the end of Day 1) at either pace. Everything owned: about recap {RecapOrNever(medianWholeRecap[1])} for a good player, " +
             $"{RecapOrNever(medianWholeRecap[0])} for a steady one.");
    }

    // ------------------------------------------------------------------ what the check reads

    private sealed class Pace
    {
        public string name;
        public float talk, drink, bench, counter, dial, answer;
        public string Describe() => $"talk {talk:0.#} s, a drink {drink:0.#} s, a bench repair {bench:0.#} s, the counter fault {counter:0.#} s, a support call {dial:0.#} s to dial and {answer:0.#} s to answer and hand back, one pair of hands";
    }

    private sealed class Arch
    {
        public string name;
        public float patience = 1f, tip = 1f, wish = .5f;
    }

    private sealed class Fault
    {
        public int payout;
        public FaultType type;
        public float holdMin = 20f, holdMax = 30f;   // a support call's hold (HoldCallJob), only for Bureaucratic faults
    }

    private sealed class Device
    {
        public GameObject prefab;
        public List<Fault> faults = new();
    }

    private sealed class World
    {
        public float dayLength = 180f, lead = 36f, jitter, minimumGap = 5f, openerMetresOut = 3f, openerWalk = 14f, openingGraceMin = 4f, openingGraceMax = 9f;
        public int counterSlots = 3;
        public const int MostEver = 6;
        public float queuePatience = 40f, servicePatience = 45f, maxTipFraction = .6f, serveBump = .08f, orderTopUp = .1f, handbackBump = .08f;
        public float drinkingSeconds = 20f, drinkingDrain = .5f, satisfiedDrain = .8f, conversationDrain = .1f, seatDrain = .6f;
        public int patronPay = 5, maxPatrons = 14;
        public float patronInterval = 12f, patronJitter = .35f, patronGraceMin = 3f, patronGraceMax = 8f;
        public readonly List<Arch> archetypes = new();
        public readonly List<Device> spawnerDevices = new();
        public readonly List<int> drinkPrices = new();
        public readonly Dictionary<DrinkDefinition, int> priceOf = new();
        public readonly List<DayDefinition> schedule = new();
        public readonly Dictionary<GameObject, Device> deviceOf = new();
        public Pace steady, good;
        public string notes = "";

        public string Describe() =>
            $"Day {dayLength:0} s; the walk in {lead:0} s (the opener at the till about {openerWalk:0} s in); {counterSlots} counter places; " +
            $"patience: queue {queuePatience:0} s, service {servicePatience:0} s, seated x{seatDrain:0.##}, a drink x{drinkingDrain:0.##} for {drinkingSeconds:0} s then x{satisfiedDrain:0.##}; " +
            $"tips up to {maxTipFraction * 100f:0}% x patience left x mood; patrons ${patronPay} every {patronInterval:0} s, {maxPatrons} at most.\n" +
            $"Schedule: {string.Join(", ", schedule.ConvertAll(d => d.name))}. Drinks: {string.Join(", ", drinkPrices.ConvertAll(p => "$" + p))}. " +
            $"Archetypes: {string.Join(", ", archetypes.ConvertAll(a => $"{a.name} (patience x{a.patience:0.##}, tip x{a.tip:0.##}, wish {a.wish * 100f:0}%)"))}.\n" +
            DescribeDevices() + notes;

        private string DescribeDevices()
        {
            StringBuilder sb = new("Devices: ");
            foreach (KeyValuePair<GameObject, Device> kv in deviceOf)
            {
                sb.Append(kv.Key.name).Append(" (");
                List<string> parts = new();
                foreach (Fault f in kv.Value.faults) parts.Add($"{Kind(f.type)} ${f.payout}");
                sb.Append(string.Join(", ", parts)).Append(")  ");
            }
            return sb.ToString();
        }
    }

    private static string Kind(FaultType type) => type == FaultType.Human ? "counter" : type == FaultType.Bureaucratic ? "call" : "bench";

    private static World ReadWorld()
    {
        World w = new();

        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (!Check(spawner != null, "The café scene has a CustomerSpawner")) return null;
        var so = new SerializedObject(spawner);
        SerializedProperty schedule = so.FindProperty("schedule");
        for (int i = 0; schedule != null && i < schedule.arraySize; i++)
            if (schedule.GetArrayElementAtIndex(i).objectReferenceValue is DayDefinition day) w.schedule.Add(day);
        w.schedule.Sort((a, b) => a.dayNumber.CompareTo(b.dayNumber));
        if (!Check(w.schedule.Count > 0, "The spawner's schedule names at least one day")) return null;
        Check(w.schedule.Count >= DemoLastDay, $"The schedule covers the demo's Days 1-{DemoLastDay} ({w.schedule.Count} days)");

        w.lead = Prop(so, "walkLeadSeconds", w.lead);
        w.jitter = Prop(so, "spawnJitter", 0f);
        w.minimumGap = Prop(so, "minimumGap", w.minimumGap);
        w.openerMetresOut = Prop(so, "openerMetresOut", w.openerMetresOut);
        w.openingGraceMin = Prop(so, "openingGraceMin", w.openingGraceMin);
        w.openingGraceMax = Prop(so, "openingGraceMax", w.openingGraceMax);

        SerializedProperty archetypes = so.FindProperty("archetypes");
        for (int i = 0; archetypes != null && i < archetypes.arraySize; i++)
        {
            SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
            w.archetypes.Add(new Arch
            {
                name = a.FindPropertyRelative("archetypeName").stringValue,
                patience = a.FindPropertyRelative("patienceMultiplier").floatValue,
                tip = a.FindPropertyRelative("tipMultiplier").floatValue,
                wish = a.FindPropertyRelative("drinkWishChance").floatValue
            });
        }
        Check(w.archetypes.Count > 0, $"The spawner has walk-in archetypes ({w.archetypes.Count})");

        SerializedProperty drinks = so.FindProperty("drinks");
        for (int i = 0; drinks != null && i < drinks.arraySize; i++)
            if (drinks.GetArrayElementAtIndex(i).objectReferenceValue is DrinkDefinition drink)
            {
                w.drinkPrices.Add(drink.price);
                w.priceOf[drink] = drink.price;
            }
        Check(w.drinkPrices.Count > 0, $"The spawner has drinks ({w.drinkPrices.Count})");

        SerializedProperty devices = so.FindProperty("devicePrefabs");
        for (int i = 0; devices != null && i < devices.arraySize; i++)
            if (devices.GetArrayElementAtIndex(i).objectReferenceValue is GameObject prefab)
            {
                Device device = DeviceFor(w, prefab);
                if (device != null) w.spawnerDevices.Add(device);
            }
        Check(w.spawnerDevices.Count > 0, $"The spawner has devices with faults and payouts ({w.spawnerDevices.Count})");
        foreach (DayDefinition day in w.schedule)
        {
            if (day.devices != null) foreach (GameObject prefab in day.devices) if (prefab != null) DeviceFor(w, prefab);
            if (day.useFeaturedRepair && day.featuredRepair != null && day.featuredRepair.devicePrefab != null) DeviceFor(w, day.featuredRepair.devicePrefab);
            if (day.featuredRegular != null && day.featuredRegular.preferredDrink != null && !w.priceOf.ContainsKey(day.featuredRegular.preferredDrink))
                w.priceOf[day.featuredRegular.preferredDrink] = day.featuredRegular.preferredDrink.price;
            foreach (StoryVisit visit in day.storyVisits ?? Array.Empty<StoryVisit>())
                if (visit != null && visit.who != null && visit.who.preferredDrink != null && !w.priceOf.ContainsKey(visit.who.preferredDrink))
                    w.priceOf[visit.who.preferredDrink] = visit.who.preferredDrink.price;
        }

        DayClock clock = Object.FindAnyObjectByType<DayClock>();
        if (clock != null) w.dayLength = Prop(new SerializedObject(clock), "dayLengthSeconds", w.dayLength);
        CounterQueue queue = Object.FindAnyObjectByType<CounterQueue>();
        if (queue != null && queue.SlotCount > 0) w.counterSlots = queue.SlotCount;

        PatronSpawner patrons = Object.FindAnyObjectByType<PatronSpawner>();
        if (patrons != null)
        {
            var ps = new SerializedObject(patrons);
            w.patronPay = PropInt(ps, "payPerPatron", w.patronPay);
            w.maxPatrons = PropInt(ps, "maxPatrons", w.maxPatrons);
            w.patronInterval = Prop(ps, "spawnInterval", w.patronInterval);
            w.patronJitter = Prop(ps, "spawnJitter", w.patronJitter);
            w.patronGraceMin = Prop(ps, "openingGraceMin", w.patronGraceMin);
            w.patronGraceMax = Prop(ps, "openingGraceMax", w.patronGraceMax);
        }
        else w.notes += "\n(No PatronSpawner in the scene: patrons counted at the code's defaults.)";

        var customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AssetsPrefabs/Customer.prefab");
        CustomerBrain brain = customerPrefab != null ? customerPrefab.GetComponent<CustomerBrain>() : null;
        if (Check(brain != null, "Customer.prefab has a CustomerBrain (its patience and tip rules)"))
        {
            var bs = new SerializedObject(brain);
            w.queuePatience = Prop(bs, "queuePatience", w.queuePatience);
            w.servicePatience = Prop(bs, "servicePatience", w.servicePatience);
            w.maxTipFraction = Prop(bs, "maxTipFraction", w.maxTipFraction);
            w.serveBump = Prop(bs, "serveBump", w.serveBump);
            w.orderTopUp = Prop(bs, "orderTopUp", w.orderTopUp);
            w.handbackBump = Prop(bs, "handbackBump", w.handbackBump);
            w.drinkingSeconds = Prop(bs, "drinkingSeconds", w.drinkingSeconds);
            w.drinkingDrain = Prop(bs, "drinkingDrain", w.drinkingDrain);
            w.satisfiedDrain = Prop(bs, "satisfiedDrain", w.satisfiedDrain);
            w.conversationDrain = Prop(bs, "conversationDrainMultiplier", w.conversationDrain);
        }

        // The seats' drain: the commonest value among the café's active seats (0.6; the lounge's 0.55).
        Dictionary<float, int> drains = new();
        foreach (TableSeat seat in Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (!seat.isActiveAndEnabled) continue;
            float d = Mathf.Round(seat.DrainMultiplier * 100f) / 100f;
            drains[d] = drains.TryGetValue(d, out int n) ? n + 1 : 1;
        }
        int best = 0;
        foreach (KeyValuePair<float, int> kv in drains) if (kv.Value > best) { best = kv.Value; w.seatDrain = kv.Key; }

        ReadPaces(w);
        return w;
    }

    // The person's pace: the café lab's human-speed numbers (CafeLabDirector: repairs 45-90 s, drinks 20-40 s, a 3-6 s
    // reaction, 4 s of talk), read from a director made in a preview scene so a retune of the lab is a retune here; the
    // code's own numbers if that fails. The good player's pace is fixed: his 7 Oct repairs ran 41-84 s (median 63) on an
    // older bench, a drink under 20 s of hands.
    private static void ReadPaces(World w)
    {
        float talk = 4f, drink = 30f, bench = 67.5f, reaction = 4.5f, confirm = 1.2f;
        Scene preview = default;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Upgrade economy check (lab numbers)");
            SceneManager.MoveGameObjectToScene(go, preview);
            CafeLabDirector lab = go.AddComponent<CafeLabDirector>();
            talk = lab.humanConversation;
            drink = (lab.humanDrinkVisit.x + lab.humanDrinkVisit.y) / 2f;
            bench = (lab.humanRepairVisit.x + lab.humanRepairVisit.y) / 2f;
            reaction = (lab.humanReaction.x + lab.humanReaction.y) / 2f;
            confirm = lab.phoneConfirm;
            Object.DestroyImmediate(go);
        }
        catch (Exception exception)
        {
            w.notes += "\n(The lab's human-speed numbers could not be read: " + exception.Message + "; the code's own used.)";
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
        w.steady = new Pace { name = "A steady player (the lab's human speed)", talk = talk, drink = drink, bench = bench,
                              counter = reaction + confirm + 2f, dial = reaction, answer = reaction + 2f };
        w.good = new Pace { name = "A good player (knows the shop)", talk = 3f, drink = 18f, bench = 50f, counter = 6f, dial = 3f, answer = 4f };
    }

    private static Device DeviceFor(World w, GameObject prefab)
    {
        if (prefab == null) return null;
        if (w.deviceOf.TryGetValue(prefab, out Device known)) return known;
        DeviceDefinition definition = prefab.GetComponent<DeviceDefinition>();
        if (definition == null || definition.faults == null || definition.faults.Length == 0)
        {
            Check(false, $"{prefab.name} has a DeviceDefinition with faults and payouts");
            return null;
        }
        Device device = new() { prefab = prefab };
        HoldCallJob call = prefab.GetComponent<HoldCallJob>();
        float holdMin = 20f, holdMax = 30f;
        if (call != null)
        {
            var cs = new SerializedObject(call);
            holdMin = Prop(cs, "holdMin", holdMin);
            holdMax = Prop(cs, "holdMax", holdMax);
        }
        foreach (DeviceFault fault in definition.faults)
        {
            if (fault == null) continue;
            device.faults.Add(new Fault { payout = fault.payout, type = fault.type, holdMin = holdMin, holdMax = holdMax });
        }
        w.deviceOf[prefab] = device;
        return device;
    }

    // His own days, for the arithmetic to sit beside: the "Earned" line of DayLogs/AcesCafeLayout/Day0N_summary.txt. These files
    // are overwritten by whichever game last played that day number (a lab never writes here), so they are context, not a check.
    private static string HisLoggedDays()
    {
        StringBuilder sb = new("His logged days (DayLogs/AcesCafeLayout, whichever game last played that day): ");
        int found = 0;
        for (int day = 1; day <= 5; day++)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "DayLogs", "AcesCafeLayout", $"Day{day:00}_summary.txt");
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("Earned", StringComparison.Ordinal)) continue;
                int dollar = trimmed.IndexOf('$');
                if (dollar < 0) continue;
                int end = dollar + 1;
                while (end < trimmed.Length && (char.IsDigit(trimmed[end]) || trimmed[end] == ',')) end++;
                sb.Append($"Day {day} {trimmed.Substring(dollar, end - dollar)}  ");
                found++;
                break;
            }
        }
        return found == 0 ? sb.Append("none found.").ToString() : sb.ToString();
    }

    private static float Prop(SerializedObject so, string name, float fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null && p.propertyType == SerializedPropertyType.Float ? p.floatValue : fallback;
    }

    private static int PropInt(SerializedObject so, string name, int fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null && p.propertyType == SerializedPropertyType.Integer ? p.intValue : fallback;
    }

    // ------------------------------------------------------------------ the day, played through

    private sealed class Result
    {
        public int arrived, served, lost, turned, work, tips, patrons, total;
    }

    private sealed class Person
    {
        public string name;
        public float patienceMult = 1f, tipMult = 1f;
        public float spawned, walk;
        public bool hasRepair;
        public int payout;
        public FaultType faultType;
        public float hold;                 // a support call's hold, seconds (Bureaucratic only)
        public int drinkPrice = -1;        // -1: no drink
        public float atCounter = -1f, heard = -1f;
        public float queueMax, serviceMax, patience;
        public float drinkServedAt = -1f;
        public bool repairDone, drinkDone, gone, lost, turnedBack;
        public int paidBase, paidTip;
        public bool WantsDrink => drinkPrice >= 0;
    }

    private static class DaySim
    {
        private const float Dt = .1f;
        private const float StopAt = 900f;

        public static Result Play(World w, DayDefinition day, int dayNumber, Pace pace, System.Random rng)
        {
            List<Person> people = new();
            List<Tuple<Person, string>> tasks = new();
            Dictionary<Person, float> ringing = new();
            bool guided = day.GuidesOpeningOn(dayNumber);
            int openingVisits = 0;                 // the Day 1 lesson: a drink visit, then a repair visit, one at a time
            Person openingVisitor = null;
            bool openerPending = w.openerMetresOut > 0f && !guided;
            float timer = openerPending ? .5f : Range(rng, w.openingGraceMin, w.openingGraceMax);
            bool featuredSpawned = day.featuredRegular == null;
            StoryVisit[] stories = day.StoryVisitsOn(dayNumber);
            bool[] storySpawned = new bool[stories.Length];
            float busyUntil = 0f;
            Person current = null;
            string currentKind = null;
            float patronTimer = Range(rng, w.patronGraceMin, w.patronGraceMax);
            int patronsIn = 0, patronMoney = 0;
            float suppressWish = day.SuppressesRepairDrinksOn(dayNumber) ? 1f : 0f;

            for (float t = 0f; t < StopAt; t += Dt)
            {
                bool open = t < w.dayLength;
                int living = 0, onTheWay = 0, atCounterUnheard = 0;
                foreach (Person p in people)
                {
                    if (p.gone) continue;
                    living++;
                    if (p.atCounter < 0f) onTheWay++;
                    else if (p.heard < 0f) atCounterUnheard++;
                }
                if (living == 0 && t > w.dayLength) break;

                // ---- the patrons: $5 a head on arrival, while the café is open ----
                if (open)
                {
                    patronTimer -= Dt;
                    if (patronTimer <= 0f)
                    {
                        patronTimer = w.patronInterval * Range(rng, 1f - w.patronJitter, 1f + w.patronJitter);
                        if (patronsIn < w.maxPatrons) { patronsIn++; patronMoney += w.patronPay; }
                    }
                }

                // ---- the spawner (CustomerSpawner.Update) ----
                if (open)
                {
                    bool openingActive = guided && openingVisits < 2;
                    bool visitInProgress = openingVisitor != null && !openingVisitor.gone;
                    if (openingActive && openingVisitor != null && openingVisitor.gone) { openingVisits++; openingVisitor = null; visitInProgress = false; openingActive = guided && openingVisits < 2; }
                    timer -= Dt;
                    if (!visitInProgress && timer <= 0f)
                    {
                        float fraction = Mathf.Clamp01((t + w.lead) / w.dayLength);
                        timer = Mathf.Max(w.minimumGap, day.IntervalAt(fraction) * Range(rng, 1f - w.jitter, 1f + w.jitter));
                        bool featuredDue = !openingActive && !featuredSpawned && day.featuredRegular != null && fraction >= day.featuredRegularArrivesAt;
                        int storyDue = -1;
                        if (!openingActive && !featuredDue)
                            for (int i = 0; i < stories.Length; i++)
                                if (!storySpawned[i] && stories[i] != null && stories[i].who != null && fraction >= stories[i].arrivesAt) { storyDue = i; break; }
                        int cap = openingActive ? 1 : day.maxCustomers;
                        if (featuredDue || storyDue >= 0) cap = Mathf.Max(cap, World.MostEver);
                        if (living >= cap) { if (openingActive) timer = 1.5f; }
                        else if (w.counterSlots - atCounterUnheard <= onTheWay) timer = 1.5f;
                        else
                        {
                            bool opener = openerPending && t < 15f;
                            openerPending = false;
                            Person p;
                            if (featuredDue)
                            {
                                featuredSpawned = true;
                                p = Regular(w, day, day.featuredRegular, rng, suppressWish, true);
                            }
                            else if (storyDue >= 0)
                            {
                                storySpawned[storyDue] = true;
                                p = Regular(w, day, stories[storyDue].who, rng, suppressWish, false);
                            }
                            else if (openingActive)
                            {
                                p = WalkIn(w, day, rng, suppressWish, openingVisits == 0 ? 1 : 2);
                                p.drinkPrice = openingVisits == 0 ? OpeningDrinkPrice(w, day) : p.drinkPrice;
                                openingVisitor = p;
                            }
                            else p = WalkIn(w, day, rng, suppressWish, 0);
                            p.spawned = t;
                            p.walk = opener ? w.openerWalk : w.lead;
                            people.Add(p);
                        }
                    }
                }

                // ---- arriving at the counter; nobody new after 8 PM ----
                foreach (Person p in people)
                {
                    if (p.gone || p.atCounter >= 0f || t < p.spawned + p.walk) continue;
                    if (!open) { p.gone = true; p.turnedBack = true; continue; }
                    p.atCounter = t;
                    p.queueMax = w.queuePatience * p.patienceMult * day.patienceMultiplier;
                    p.serviceMax = w.servicePatience * p.patienceMult * day.patienceMultiplier;
                    p.patience = p.queueMax;
                }

                // ---- one pair of hands: a ringing call first, then whoever is at the counter, then the next job ----
                if (t >= busyUntil)
                {
                    Person ring = null;
                    foreach (KeyValuePair<Person, float> kv in ringing) if (t >= kv.Value && !kv.Key.gone) { ring = kv.Key; break; }
                    Person next = null;
                    foreach (Person p in people)
                        if (!p.gone && p.atCounter >= 0f && p.heard < 0f && (next == null || p.atCounter < next.atCounter)) next = p;
                    if (ring != null)
                    {
                        ringing.Remove(ring);
                        current = ring; currentKind = "answer"; busyUntil = t + pace.answer;
                    }
                    else if (next != null)
                    {
                        next.heard = t;
                        next.patience = next.serviceMax;
                        if (next.hasRepair && next.WantsDrink) next.patience = Mathf.Min(next.patience + next.serviceMax * w.orderTopUp, next.serviceMax);
                        current = next; currentKind = "talk"; busyUntil = t + pace.talk;
                        if (next.WantsDrink) tasks.Add(Tuple.Create(next, "drink"));
                        if (next.hasRepair) tasks.Add(Tuple.Create(next, Kind(next.faultType)));
                    }
                    else if (tasks.Count > 0)
                    {
                        Tuple<Person, string> task = tasks[0];
                        tasks.RemoveAt(0);
                        if (task.Item1.gone) { current = null; currentKind = null; }
                        else
                        {
                            current = task.Item1; currentKind = task.Item2;
                            switch (currentKind)
                            {
                                case "drink": busyUntil = t + pace.drink; break;
                                case "bench": busyUntil = t + pace.bench; break;
                                case "counter": busyUntil = t + pace.counter; break;
                                default:
                                    busyUntil = t + pace.dial;
                                    ringing[current] = t + pace.dial + current.hold;
                                    break;
                            }
                        }
                    }
                    else { current = null; currentKind = null; }
                }
                else if (current != null && t + Dt >= busyUntil)
                {
                    Person p = current;
                    if (!p.gone)
                    {
                        float speed = Mathf.Clamp01(p.patience / p.serviceMax);
                        if (currentKind == "drink")
                        {
                            int basePay = p.drinkPrice;
                            int tip = Mathf.RoundToInt(basePay * w.maxTipFraction * speed * p.tipMult);
                            p.paidBase += basePay; p.paidTip += tip;
                            p.drinkDone = true; p.drinkServedAt = t;
                            p.patience = Mathf.Min(p.patience + p.serviceMax * w.serveBump, p.serviceMax);
                        }
                        else if (currentKind == "bench" || currentKind == "counter" || currentKind == "answer")
                        {
                            int basePay = Mathf.RoundToInt(p.payout * JobBase.PayMultiplier(JobGrade.Perfect));
                            int tip = Mathf.RoundToInt(basePay * w.maxTipFraction * speed * p.tipMult);
                            p.paidBase += basePay; p.paidTip += tip;
                            p.repairDone = true;
                            if (p.WantsDrink && !p.drinkDone) p.patience = Mathf.Min(p.patience + p.serviceMax * w.handbackBump, p.serviceMax);
                        }
                        bool owedDrink = p.WantsDrink && !p.drinkDone;
                        bool owedRepair = p.hasRepair && !p.repairDone;
                        if (!owedDrink && !owedRepair && currentKind != "talk" && currentKind != "call") p.gone = true;
                    }
                    current = null; currentKind = null;
                }

                // ---- patience ----
                foreach (Person p in people)
                {
                    if (p.gone || p.atCounter < 0f) continue;
                    float rate;
                    if (current == p && (currentKind == "talk" || currentKind == "answer" || currentKind == "counter")) rate = w.conversationDrain;
                    else if (p.heard < 0f) rate = 1f;
                    else
                    {
                        rate = w.seatDrain;
                        if (p.drinkServedAt >= 0f) rate *= t - p.drinkServedAt <= w.drinkingSeconds ? w.drinkingDrain : w.satisfiedDrain;
                    }
                    p.patience -= Dt * rate;
                    if (p.patience <= 0f)
                    {
                        p.gone = true; p.lost = true;
                        tasks.RemoveAll(x => x.Item1 == p);
                        ringing.Remove(p);
                    }
                }
            }

            Result r = new() { patrons = patronMoney };
            foreach (Person p in people)
            {
                if (p.atCounter >= 0f) r.arrived++;
                if (p.paidBase > 0) r.served++;
                if (p.lost) r.lost++;
                if (p.turnedBack) r.turned++;
                r.work += p.paidBase + p.paidTip;
                r.tips += p.paidTip;
            }
            r.total = r.work + r.patrons;
            return r;
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static int OpeningDrinkPrice(World w, DayDefinition day)
        {
            if (day.openingDrink != null) return day.openingDrink.price;
            return w.drinkPrices.Count > 0 ? w.drinkPrices[0] : 4;
        }

        // A walk-in: the day's archetypes, its drink-only share, its device pool (else the spawner's), a wish while waiting.
        // forced: 0 the day's mix, 1 a drink visit, 2 a repair visit (the Day 1 lesson).
        private static Person WalkIn(World w, DayDefinition day, System.Random rng, float suppressWish, int forced)
        {
            List<Arch> allowed = new();
            foreach (Arch a in w.archetypes) if (day.AllowsArchetype(a.name)) allowed.Add(a);
            if (allowed.Count == 0) allowed.AddRange(w.archetypes);
            Arch arch = allowed.Count > 0 ? allowed[rng.Next(allowed.Count)] : new Arch { name = "walk-in" };
            Person p = new() { name = arch.name, patienceMult = arch.patience, tipMult = arch.tip };
            bool drinkOnly = forced == 1 || (forced == 0 && rng.NextDouble() < day.drinkOnlyChance);
            if (drinkOnly)
            {
                p.drinkPrice = w.drinkPrices.Count > 0 ? w.drinkPrices[rng.Next(w.drinkPrices.Count)] : 4;
                return p;
            }
            List<Device> pool = new();
            if (day.devices != null) foreach (GameObject g in day.devices) if (g != null && w.deviceOf.TryGetValue(g, out Device d)) pool.Add(d);
            if (pool.Count == 0) pool.AddRange(w.spawnerDevices);
            if (pool.Count > 0)
            {
                Device device = pool[rng.Next(pool.Count)];
                Fault fault = device.faults[rng.Next(device.faults.Count)];
                p.hasRepair = true; p.payout = fault.payout; p.faultType = fault.type;
                p.hold = Range(rng, fault.holdMin, fault.holdMax);
            }
            if (suppressWish < 1f && rng.NextDouble() < arch.wish && w.drinkPrices.Count > 0)
                p.drinkPrice = w.drinkPrices[rng.Next(w.drinkPrices.Count)];
            return p;
        }

        // A named visit: the day's featured repair when it has one (Grace's camera), else the regular's own visit kind.
        private static Person Regular(World w, DayDefinition day, CustomerProfile who, System.Random rng, float suppressWish, bool featured)
        {
            Person p = new() { name = who.characterName, patienceMult = who.patienceMultiplier, tipMult = who.tipMultiplier };
            int preferred = who.preferredDrink != null && w.priceOf.TryGetValue(who.preferredDrink, out int price) ? price
                          : w.drinkPrices.Count > 0 ? w.drinkPrices[rng.Next(w.drinkPrices.Count)] : 4;
            if (featured && day.useFeaturedRepair && day.featuredRepair != null && day.featuredRepair.TryCreateJob(out Job job, out _))
            {
                p.hasRepair = true; p.payout = job.payout; p.faultType = job.faultType;
                if (suppressWish < 1f && rng.NextDouble() < who.drinkWishChance) p.drinkPrice = preferred;
                return p;
            }
            bool wantsDrink = who.primaryVisitKind switch
            {
                RegularVisitKind.RepairOnly => false,
                RegularVisitKind.DrinkOnly => true,
                _ => rng.NextDouble() < day.drinkOnlyChance
            };
            if (wantsDrink) { p.drinkPrice = preferred; return p; }
            List<Device> pool = new();
            if (who.preferredDevice != null && w.deviceOf.TryGetValue(who.preferredDevice, out Device own) && rng.NextDouble() < who.preferredDeviceChance) pool.Add(own);
            if (pool.Count == 0 && day.devices != null) foreach (GameObject g in day.devices) if (g != null && w.deviceOf.TryGetValue(g, out Device d)) pool.Add(d);
            if (pool.Count == 0) pool.AddRange(w.spawnerDevices);
            if (pool.Count > 0)
            {
                Device device = pool[rng.Next(pool.Count)];
                Fault fault = device.faults[rng.Next(device.faults.Count)];
                p.hasRepair = true; p.payout = fault.payout; p.faultType = fault.type;
                p.hold = Range(rng, fault.holdMin, fault.holdMax);
            }
            if (suppressWish < 1f && rng.NextDouble() < who.drinkWishChance) p.drinkPrice = preferred;
            return p;
        }
    }

    // ------------------------------------------------------------------ the numbers

    private static float Mean(List<Result> results, Func<Result, int> of)
    {
        if (results.Count == 0) return 0f;
        double sum = 0;
        foreach (Result r in results) sum += of(r);
        return (float)(sum / results.Count);
    }

    private static float Median(List<Result> results, Func<Result, int> of)
    {
        List<int> xs = new();
        foreach (Result r in results) xs.Add(of(r));
        return Quantile(xs, .5f);
    }

    private static string Quartiles(List<Result> results, Func<Result, int> of)
    {
        List<int> xs = new();
        foreach (Result r in results) xs.Add(of(r));
        return $"{Quantile(xs, .25f):0} / {Quantile(xs, .5f):0} / {Quantile(xs, .75f):0}";
    }

    private static float Quantile(List<int> xs, float q)
    {
        if (xs.Count == 0) return 0f;
        xs.Sort();
        int i = Mathf.Clamp(Mathf.RoundToInt(q * (xs.Count - 1)), 0, xs.Count - 1);
        return xs[i];
    }

    private static int MedianAt(int[][] cumulative, int day)
    {
        List<int> xs = new();
        foreach (int[] row in cumulative) xs.Add(row[day]);
        return Mathf.RoundToInt(Quantile(xs, .5f));
    }

    // The recap (1 = the end of Day 1) at which `price` is first affordable from the run's cumulative takings; 0 = not within the horizon.
    private static int FirstRecap(int[] cumulative, int price)
    {
        for (int d = 0; d < cumulative.Length; d++) if (cumulative[d] >= price) return d + 1;
        return 0;
    }

    private static float MedianRecap(int[][] cumulative, int price)
    {
        List<int> xs = new();
        foreach (int[] row in cumulative) xs.Add(FirstRecap(row, price) == 0 ? DaysToOwnHorizon + 1 : FirstRecap(row, price));
        float m = Quantile(xs, .5f);
        return m > DaysToOwnHorizon ? 0f : m;
    }

    private static float ShareAffordableBy(int[][] cumulative, int price, int recap)
    {
        int n = 0;
        foreach (int[] row in cumulative) { int first = FirstRecap(row, price); if (first > 0 && first <= recap) n++; }
        return cumulative.Length == 0 ? 0f : n / (float)cumulative.Length;
    }

    private static string RecapWords(int[][] cumulative, int price)
    {
        float median = MedianRecap(cumulative, price);
        StringBuilder sb = new($"median recap {RecapOrNever(median)}  (");
        List<string> parts = new();
        int shown = 0;
        for (int recap = 1; recap <= DaysToOwnHorizon && shown < 4; recap++)
        {
            int n = 0;
            foreach (int[] row in cumulative) if (FirstRecap(row, price) == recap) n++;
            if (n * 200 < cumulative.Length) continue;      // under half a percent
            parts.Add($"recap {recap}: {n * 100f / cumulative.Length:0}%");
            shown++;
        }
        float never = 1f - ShareAffordableBy(cumulative, price, DaysToOwnHorizon);
        if (never > 0f) parts.Add($"not within {DaysToOwnHorizon} days: {never * 100f:0}%");
        sb.Append(string.Join(", ", parts)).Append(')');
        return sb.ToString();
    }

    private static string RecapOrNever(float recap) => recap <= 0f ? $"beyond {DaysToOwnHorizon} days" : recap.ToString("0", CultureInfo.InvariantCulture);

    private static string Costs(int[] c)
    {
        if (c == null || c.Length == 0) return "(none)";
        string[] parts = new string[c.Length];
        for (int i = 0; i < c.Length; i++) parts[i] = "$" + c[i];
        return string.Join(" / ", parts);
    }

    // ------------------------------------------------------------------ the report

    private static bool Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        return ok;
    }

    private static void Line(string text) => report.AppendLine(text);

    private static void Finish()
    {
        string summary = $"{Tag}{(failures == 0 ? "PASS" : "FAIL")}: {checks - failures}/{checks} checks passed.";
        string text = summary + "\n" + report;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "UpgradeEconomy",
                "check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), text);
            text += "\nReport: " + folder;
        }
        catch (Exception exception) { Debug.LogWarning(Tag + "Could not write the report: " + exception.Message); }
        if (failures == 0) Debug.Log(text);
        else Debug.LogError(text);
    }
}
#endif
