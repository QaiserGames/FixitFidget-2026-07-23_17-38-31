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
// (14 Oct 2026: the scene's day is 420 s now, every day: specs/days-1-3.md §7.
// The gaps are seconds, so a gap authored for 180 s brings 2.3x the arrivals
// in a 420 s day: Days 1-3 were re-authored, Days 4-5 scaled by 420/180.)
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

// A named regular who also comes in on this day at an authored time, besides the featured one (session 3: the officer's
// coffee on Day 1 and his question on Day 3). Arrives like the featured regular: the next free arrival slot after the
// time, never during the Day 1 lesson's two visits.
[System.Serializable]
public class StoryVisit
{
    [Tooltip("Who comes in.")]
    public CustomerProfile who;

    [Range(0f, 0.95f)]
    [Tooltip("Earliest point in the day they may take the next arrival slot (0.25: a quarter through the day).")]
    public float arrivesAt = 0.3f;
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

    [Tooltip("How long each new opening or featured-visit hint stays on screen.")]
    [Range(3f, 10f)]
    public float openingHintDuration = 6f;

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

    [Tooltip("Show next-action hints for this featured visit on its authored day. Does not change arrivals or gameplay rules.")]
    public bool showFeaturedVisitHints;

    public bool GuidesFeaturedVisitOn(int day) =>
        day == dayNumber && showFeaturedVisitHints && featuredRegular != null;

    [Range(0f, 0.95f)]
    [Tooltip("Earliest point in the day when the featured regular may take the " +
             "next available arrival slot. 0.15 means 15% through the day.")]
    public float featuredRegularArrivesAt = 0.15f;

    [Tooltip("Give this featured appearance a specific repair. Other arrivals and the opening lesson keep their normal jobs. Off preserves existing days.")]
    public bool useFeaturedRepair;
    public FeaturedRepairRequest featuredRepair = new FeaturedRepairRequest();

    [Header("Story visits (also today)")]
    [Tooltip("Named regulars who also come in today at an authored time (the officer). Each comes once, on this day " +
             "only (a repeated last day doesn't repeat them).")]
    public StoryVisit[] storyVisits;

    /// <summary>The story visits on <paramref name="day"/>: only on this asset's own day, never when it repeats.</summary>
    public StoryVisit[] StoryVisitsOn(int day) =>
        day == dayNumber && storyVisits != null ? storyVisits : System.Array.Empty<StoryVisit>();

    // DAYS 1-3 AS AN INTRO LEVEL (14 Oct 2026; specs/days-1-3.md §6, the Producer's call 8: yes). His words: "the playtest
    // couldn't even get passed day 1 for me". In playtest 5 a random roll gave Day 2 Walter's seized watch, a job the game had
    // never taught; here each early day names its walk-ins' repairs, in order, so a walk-in only ever brings a job the game
    // has already taught: Day 1 cleaning, Day 2 the swap, Day 3 the screwdriver. Authoring data like `devices`, not a mechanic,
    // and no save field: the count used resets every morning (CustomerSpawner).
    [Header("Walk-in repairs, in order (Days 1-3)")]
    [Tooltip("When this list isn't empty, each walk-in who brings a device brings the next entry (a device and a fault " +
             "index; leave the episode ID empty), and once they are used up walk-ins come for drinks. Empty = the old roll " +
             "from Devices. Only on this asset's own day, never when it repeats (a repeated day rolls from Devices).")]
    public FeaturedRepairRequest[] walkInRepairs = new FeaturedRepairRequest[0];

    /// <summary>Today's walk-in repairs in order: only on this asset's own day (never when it repeats); empty = the old roll.</summary>
    public FeaturedRepairRequest[] WalkInRepairsOn(int day) =>
        day == dayNumber && walkInRepairs != null ? walkInRepairs : System.Array.Empty<FeaturedRepairRequest>();

    // DAYS 1-3 THAT TEACH (24 Oct 2026; Designer P's spec, Docs/Studio/sessions/2026-10-23/designs/pacing-downtime-spec.md §6.1,
    // approved by him with his answers of 24 Oct). His words: "day one and two should really teach mostly but what i mean about
    // dead air is really the things you could be doing when waiting for a customer to approach the counter." Authoring data like
    // the walk-in list, no save field: which find is on the shelf is ThriftShelf's to save (SaveData.thrift).

    [Header("Your side project: the thrift find (24 Oct 2026)")]
    [Tooltip("The find on the thrift shelf by the bench on this day: a device and a fault, with nobody waiting (leave the episode " +
             "ID empty). Day 1's is on the shelf at the open, free; each later day's is bought at the closing before, in the " +
             "phone's Shop, and sells by itself at this day's closing. Empty: the Shop offers one from the jobs already taught " +
             "(ThriftShelf). Only on this asset's own day (a repeated day rotates).")]
    public FeaturedRepairRequest thriftFind = new FeaturedRepairRequest();

    [Tooltip("What the find costs in the Shop at the closing before, dollars (Day 1's is free: it is on the shelf at the open).")]
    [Min(0)] public int thriftBuy;

    [Tooltip("What the find sells for at this day's closing when it is Perfect, dollars (Good x0.8, Passable x0.48: JobBase's " +
             "own ratios over Perfect's). About 55-60 % of what a customer pays for the same job: a customer is always worth more.")]
    [Min(0)] public int thriftSale;

    /// <summary>This day's find, authored: only on this asset's own day (never when it repeats); null when none is authored.</summary>
    public FeaturedRepairRequest ThriftFindOn(int day) =>
        day == dayNumber && thriftFind != null && thriftFind.devicePrefab != null ? thriftFind : null;

    [Header("Chat with seated guests: gossip (24 Oct 2026)")]
    [Tooltip("Today's gossip, by id (GuestGossip's table): one seated guest per line has something to say (a '...' over them), " +
             "and a chat puts its fact in the notebook. Empty: nobody has gossip today. Only on this asset's own day.")]
    public string[] gossip = new string[0];

    /// <summary>Today's gossip ids: only on this asset's own day (never when it repeats).</summary>
    public string[] GossipOn(int day) =>
        day == dayNumber && gossip != null ? gossip : System.Array.Empty<string>();

    [Header("The recap's \"Tomorrow:\" line (24 Oct 2026)")]
    [Tooltip("What the closing before this day says about it on the phone's Tonight, after \"Tomorrow:\" (at most 8 words). " +
             "PLACEHOLDER COPY for his sister. Empty: no line.")]
    public string previewLine = "";

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
