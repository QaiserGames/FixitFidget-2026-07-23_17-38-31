using UnityEngine;

// ---------------------------------------------------------------------------
// A DAY, AUTHORED
//
// occupancy-and-pacing.md named the missing ingredient:
//
//   "CustomerSpawner is currently a metronome... the shop is permanently
//    saturated — which feels LESS hectic than waves, because chaos is only
//    chaos measured against calm."
//
// Three logged days confirmed it: arrivals at 15.0s, all day, every day. The
// designer, who works retail, put it better — a real shop has peaks. Lunch is
// busy because it's lunch, not because you picked Hard from a menu. A rush the
// world causes is the world's fault; a rush a difficulty slider causes is the
// menu's fault, and only one of those makes the player feel responsible.
//
// WHY FRACTIONS AND NOT TIMESTAMPS
//
// Phases are expressed as fractions of the day (0-1), never seconds. Days are
// 180s while testing and 420s at ship (GDD Part 8). A schedule written in
// seconds would have to be rewritten for every day when that changes. Written
// in fractions, "the rush is the middle quarter" stays true at any length.
// ---------------------------------------------------------------------------

[System.Serializable]
public class DayPhase
{
    [Tooltip("Shown in the inspector and the log. Calm / Build / Rush / Recover.")]
    public string phaseName = "Calm";

    [Range(0f, 1f)]
    [Tooltip("Where this phase starts, as a fraction of the day. The phase runs " +
             "until the next one starts, so these must ASCEND.")]
    public float startsAt = 0f;

    [Tooltip("Seconds between arrivals during this phase. Smaller = busier. " +
             "The measured baseline is 15s for a comfortable stretch and " +
             "roughly 7s for a genuine squeeze.")]
    public float spawnInterval = 15f;
}

[CreateAssetMenu(fileName = "Day_", menuName = "FixitFiasco/Day Definition")]
public class DayDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Which day this is. Day 1 is the tutorial day.")]
    public int dayNumber = 1;

    [TextArea(2, 4)]
    [Tooltip("What this day is FOR. Not shown to the player — it's a note to " +
             "yourself, so a day that stops working can be judged against what " +
             "it was supposed to teach.")]
    public string intent = "Learn the shop. Nothing should go wrong.";

    // NOT HERE: day length.
    //
    // It belongs on a day, and a shorter first day would be a good idea. But
    // DayClock calls StartDay in Awake, before the spawner has resolved which
    // day this is — so the length would apply from day 2 onward and silently
    // not on day 1, which is the one day it matters for. A field that works
    // everywhere except where you need it is worse than no field.
    //
    // Doing it properly means DayClock owning the schedule lookup. Worth doing;
    // not worth doing halfway in the same pass as everything else.

    [Header("Arrival rhythm")]
    [Tooltip("Phases in ASCENDING order of startsAt. The first should start at " +
             "0. Each runs until the next begins.")]
    public DayPhase[] phases =
    {
        new DayPhase { phaseName = "Calm",    startsAt = 0.00f, spawnInterval = 20f },
        new DayPhase { phaseName = "Build",   startsAt = 0.25f, spawnInterval = 15f },
        new DayPhase { phaseName = "Rush",    startsAt = 0.45f, spawnInterval = 8f  },
        new DayPhase { phaseName = "Recover", startsAt = 0.70f, spawnInterval = 18f }
    };

    [Header("Difficulty dials")]
    [Tooltip("Multiplies everyone's patience. Above 1 is forgiving — day 1 " +
             "should be generous so the player learns the shop rather than " +
             "learning it's cruel.")]
    [Range(0.5f, 2f)]
    public float patienceMultiplier = 1f;

    [Tooltip("How many customers can be in the shop at once. occupancy-and-" +
             "pacing.md fixes the ceiling at 6 for the whole game; early days " +
             "sit below it.")]
    [Range(1, 6)]
    public int maxCustomers = 6;

    [Range(0f, 1f)]
    [Tooltip("Chance a customer came ONLY for a drink. Day 1 leans on the café " +
             "because the café is the simpler half to learn.")]
    public float drinkOnlyChance = 0.25f;

    [Header("Day 1 introduction (opt-in)")]
    [Tooltip("On Day 1 only, open with one drink visit, then one repair visit. " +
             "These two customers arrive one at a time; the clock still runs.")]
    public bool guidedOpening;

    [Tooltip("The first lesson's recipe. Empty uses the first valid spawner drink.")]
    public DrinkDefinition openingDrink;

    [Tooltip("No secondary beverage requests from repair customers on this " +
             "authored day. Does not change their profiles or later visits.")]
    public bool suppressRepairDrinkWishes;

    // A short/missing schedule can repeat its last definition. Never replay a
    // Day 1 lesson (or its no-secondary-drinks rule) on a later day by accident.
    public bool GuidesOpeningOn(int day) => day == 1 && dayNumber == 1 && guidedOpening;
    public bool SuppressesRepairDrinksOn(int day) =>
        day == dayNumber && suppressRepairDrinkWishes;

    [Header("What's unlocked")]
    [Tooltip("Devices that can walk in today. EMPTY = everything the spawner " +
             "has. This is how Part 8's 'devices unlock over time' finally " +
             "becomes real.")]
    public GameObject[] devices;

    [Tooltip("Personalities that can walk in today. EMPTY = all of them.\n\n" +
             "This is the pressure budget as an AUTHORING tool rather than a " +
             "runtime throttle: you decide day 1 has no Rushed customers. The " +
             "game never quietly eases off while you're drowning, because a " +
             "game that rescues you takes the credit for the recovery.")]
    public string[] allowedArchetypes;

    [Range(0f, 1f)]
    [Tooltip("Chance an arrival is a named regular rather than a walk-in. " +
             "Zero on day 1 — a familiar face means nothing on the day you " +
             "have no history.")]
    public float regularChance = 0.35f;

    [Header("Featured regular")]
    [Tooltip("A named customer guaranteed to appear once on this day. " +
             "Leave empty for no authored appearance.")]
    public CustomerProfile featuredRegular;

    [Range(0f, 0.95f)]
    [Tooltip("Earliest point in the day when the featured regular may take the " +
             "next available arrival slot. 0.15 means 15% through the day.")]
    public float featuredRegularArrivesAt = 0.15f;

    // ---------- lookups ----------

    /// <summary>Seconds between arrivals at this point in the day.</summary>
    public float IntervalAt(float dayFraction)
    {
        if (phases == null || phases.Length == 0) return 15f;

        DayPhase chosen = phases[0];
        foreach (DayPhase p in phases)
            if (dayFraction >= p.startsAt) chosen = p;

        return Mathf.Max(0.5f, chosen.spawnInterval);
    }

    /// <summary>Which phase we're in, for the log.</summary>
    public string PhaseNameAt(float dayFraction)
    {
        if (phases == null || phases.Length == 0) return "";

        DayPhase chosen = phases[0];
        foreach (DayPhase p in phases)
            if (dayFraction >= p.startsAt) chosen = p;

        return chosen.phaseName;
    }

    public bool AllowsArchetype(string archetypeName)
    {
        if (allowedArchetypes == null || allowedArchetypes.Length == 0) return true;
        if (string.IsNullOrEmpty(archetypeName)) return false;

        foreach (string a in allowedArchetypes)
            if (string.Equals(a, archetypeName, System.StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    // Phases out of order silently produce the wrong rhythm — IntervalAt takes
    // the LAST phase whose start has passed, so an unsorted array means a phase
    // that never fires. Caught here rather than in a playtest.
    private void OnValidate()
    {
        if (phases == null) return;

        for (int i = 1; i < phases.Length; i++)
        {
            if (phases[i].startsAt < phases[i - 1].startsAt)
            {
                Debug.LogWarning(
                    $"[{name}] Phase '{phases[i].phaseName}' starts at " +
                    $"{phases[i].startsAt:0.00}, before '{phases[i - 1].phaseName}' " +
                    $"at {phases[i - 1].startsAt:0.00}. Phases must ascend, or " +
                    "one of them will never happen.", this);
            }
        }
    }
}
