#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE FIRST FIVE DAYS
//
// GDD Part 8 has described this arc since the document was written:
//
//   Day 1 — café only. Learn drinks. Calm.
//   Day 2 — the first repair walks in. One item, no pressure.
//   Day 3 — the epiphany. Both systems. The real game starts.
//
// It has never been buildable, because every day was identical. This creates
// the five assets that make it real.
//
// THE SHAPE OF A DAY, and why it isn't a ramp:
//
//   Calm -> Build -> Rush -> Recover
//
// A day that only escalates ends at maximum pressure and stays there, and a
// player under constant pressure stops caring. The Recover phase is not
// leftover time — it's the stretch where you catch up, clear the shelf, and
// finish the day thinking "that was chaotic, but I handled it." That sentence
// is the entire loop.
//
// Numbers come from six logged days, not from taste:
//   - 15s gaps produced ~9-10 arrivals against 6-8 served. Comfortable.
//   - A job occupies the player for ~66s.
//   - 6 customers is the hard ceiling (occupancy-and-pacing.md).
// ---------------------------------------------------------------------------

public static class DayScheduleSetup
{
    private const string Folder = "Assets/Data/Days";

    [MenuItem("Fixit Fidget/Content/5 · Create days 1-5")]
    public static void CreateDays()
    {
        if (!EditorUtility.DisplayDialog("Create days 1-5",
            $"Creates five DayDefinition assets in:\n{Folder}\n\n" +
            "Existing assets with the same names are OVERWRITTEN, so any hand " +
            "tuning you've done to them is lost.\n\n" +
            "Afterwards you drag them into CustomerSpawner ▸ Schedule.",
            "Create", "Cancel")) return;

        Directory.CreateDirectory(Folder);

        List<DayDefinition> made = new()
        {
            // ---- DAY 1 ----------------------------------------------------
            // Café-heavy and forgiving. One device, no fast personalities, no
            // regulars — a familiar face means nothing on the day you have no
            // history with anyone. Almost no rush: the peak here is gentler
            // than day 3's baseline.
            Make("Day_01_LearnTheShop", d =>
            {
                d.dayNumber = 1;
                d.intent = "Learn the shop. Drinks mostly, one repair type, " +
                           "nothing should go wrong. The player should finish " +
                           "thinking 'that was easy' — that's correct for day 1.";
                d.patienceMultiplier = 1.4f;
                d.maxCustomers = 3;
                d.drinkOnlyChance = 0.55f;
                d.regularChance = 0f;
                d.allowedArchetypes = new[] { "Cheerful", "Chatty" };
                d.phases = new[]
                {
                    P("Calm",    0.00f, 26f),
                    P("Build",   0.30f, 20f),
                    P("Rush",    0.55f, 16f),
                    P("Recover", 0.80f, 24f)
                };
            }),

            // ---- DAY 2 ----------------------------------------------------
            // Overlap is the lesson: you can no longer finish one customer
            // before starting the next. Sentimental arrives — patient, tips
            // well, so the first personality you meet beyond "nice" is a
            // reward rather than a threat.
            Make("Day_02_Juggle", d =>
            {
                d.dayNumber = 2;
                d.intent = "Teach overlap. The player should have to leave a " +
                           "repair to make a coffee for the first time.";
                d.patienceMultiplier = 1.2f;
                d.maxCustomers = 4;
                d.drinkOnlyChance = 0.4f;
                d.regularChance = 0.15f;
                d.allowedArchetypes = new[] { "Cheerful", "Chatty", "Sentimental" };
                d.phases = new[]
                {
                    P("Calm",    0.00f, 22f),
                    P("Build",   0.25f, 15f),
                    P("Rush",    0.50f, 11f),
                    P("Recover", 0.75f, 20f)
                };
            }),

            // ---- DAY 3 ----------------------------------------------------
            // The real game. Impatient arrives — the first person who STANDS,
            // in the way, draining at 1.15x. Rush drops to 8s, which is where
            // arrivals start outpacing a ~66s job and the player has to choose
            // who not to help.
            Make("Day_03_TheRealGame", d =>
            {
                d.dayNumber = 3;
                d.intent = "The epiphany. Full cast bar Rushed, a real midday " +
                           "rush, and the first customers who stand in your way.";
                d.patienceMultiplier = 1.0f;
                d.maxCustomers = 5;
                d.drinkOnlyChance = 0.3f;
                d.regularChance = 0.3f;
                d.allowedArchetypes = new[] { "Cheerful", "Chatty", "Sentimental", "Impatient" };
                d.phases = new[]
                {
                    P("Calm",    0.00f, 20f),
                    P("Build",   0.22f, 13f),
                    P("Rush",    0.45f, 8f),
                    P("Recover", 0.72f, 18f)
                };
            }),

            // ---- DAY 4 ----------------------------------------------------
            // Rushed joins: 0.6x patience, tips 1.3x. The upgrade day — this
            // is where "I REALLY wish I'd bought the bench upgrade" is meant
            // to land, so it has to bite without being unfair.
            Make("Day_04_Pressure", d =>
            {
                d.dayNumber = 4;
                d.intent = "Full cast. This is the day that should sell an " +
                           "upgrade — the player finishes knowing what failed them.";
                d.patienceMultiplier = 0.95f;
                d.maxCustomers = 6;
                d.drinkOnlyChance = 0.28f;
                d.regularChance = 0.35f;
                d.allowedArchetypes = new string[0];      // everyone
                d.phases = new[]
                {
                    P("Calm",    0.00f, 18f),
                    P("Build",   0.20f, 12f),
                    P("Rush",    0.42f, 7f),
                    P("Recover", 0.75f, 16f)
                };
            }),

            // ---- DAY 5 ----------------------------------------------------
            // TWO rushes with a genuine lull between them. The lull is the
            // point: it's the first day where pre-brewing during quiet, and
            // clearing the shelf before the second wave, is a real strategy.
            // This is also the definition that repeats for days 6+.
            Make("Day_05_TwoRushes", d =>
            {
                d.dayNumber = 5;
                d.intent = "Two peaks with a real lull between. Teaches using " +
                           "quiet time. Repeats for every later day, with a " +
                           "small squeeze each time.";
                d.patienceMultiplier = 0.9f;
                d.maxCustomers = 6;
                d.drinkOnlyChance = 0.25f;
                d.regularChance = 0.4f;
                d.allowedArchetypes = new string[0];
                d.phases = new[]
                {
                    P("Opening",      0.00f, 16f),
                    P("Morning rush", 0.15f, 7f),
                    P("Lull",         0.38f, 26f),
                    P("Lunch rush",   0.58f, 6f),
                    P("Last orders",  0.82f, 15f)
                };
            })
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.objects = made.ToArray();
        EditorUtility.FocusProjectWindow();

        EditorUtility.DisplayDialog("Days created",
            "Five days are in Assets/Data/Days and selected in the Project window.\n\n" +
            "NOW DO THIS:\n" +
            "1. Click SpawnPoint in the Hierarchy\n" +
            "2. Customer Spawner ▸ The day schedule ▸ Schedule\n" +
            "3. Set its size to 5\n" +
            "4. Drag Day_01 … Day_05 into the five slots, in order\n" +
            "5. Ctrl+S\n\n" +
            "Day 1 also expects only the pocket watch. Set Day_01 ▸ Devices to " +
            "size 1 and drag PocketWatch in, or leave it empty for both.", "OK");
    }

    private static DayPhase P(string name, float at, float interval) =>
        new DayPhase { phaseName = name, startsAt = at, spawnInterval = interval };

    private static DayDefinition Make(string assetName, System.Action<DayDefinition> configure)
    {
        string path = $"{Folder}/{assetName}.asset";

        DayDefinition d = AssetDatabase.LoadAssetAtPath<DayDefinition>(path);
        bool isNew = d == null;
        if (isNew) d = ScriptableObject.CreateInstance<DayDefinition>();

        configure(d);

        if (isNew) AssetDatabase.CreateAsset(d, path);
        else EditorUtility.SetDirty(d);

        return d;
    }

    // Prints the authored days as a table, so the escalation across five days
    // can be read at a glance instead of by clicking through five assets.
    [MenuItem("Fixit Fidget/Content/6 · Show the schedule")]
    public static void Show()
    {
        string[] guids = AssetDatabase.FindAssets("t:DayDefinition");
        List<DayDefinition> days = new();

        foreach (string g in guids)
        {
            DayDefinition d = AssetDatabase.LoadAssetAtPath<DayDefinition>(
                AssetDatabase.GUIDToAssetPath(g));
            if (d != null) days.Add(d);
        }

        days.Sort((a, b) => a.dayNumber.CompareTo(b.dayNumber));

        if (days.Count == 0)
        {
            EditorUtility.DisplayDialog("Schedule", "No DayDefinition assets found.", "OK");
            return;
        }

        List<string> rows = new() { "Day  patience  cap  drink%  regular%   peak gap   phases" };

        foreach (DayDefinition d in days)
        {
            float peak = 999f;
            List<string> names = new();
            if (d.phases != null)
                foreach (DayPhase p in d.phases)
                {
                    if (p.spawnInterval < peak) peak = p.spawnInterval;
                    names.Add(p.phaseName);
                }

            rows.Add($" {d.dayNumber,-3} {d.patienceMultiplier,7:0.00}  {d.maxCustomers,3}  " +
                     $"{d.drinkOnlyChance * 100f,5:0}%  {d.regularChance * 100f,7:0}%   " +
                     $"{peak,6:0.0}s   {string.Join(" > ", names)}");
        }

        string text = string.Join("\n", rows);
        Debug.Log("[Day schedule]\n" + text);
        EditorUtility.DisplayDialog("Day schedule", text, "OK");
    }
}
#endif
