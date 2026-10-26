using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private CounterQueue counterQueue;

    [Header("The day schedule")]
    [Tooltip("One DayDefinition per authored day, in order. Empty = fall back " +
             "to the flat spawnInterval below, exactly as before.\n\n" +
             "Runs past the end of this list reuse the LAST definition, with a " +
             "small escalation per day — see EscalationPerDay.")]
    [SerializeField] private DayDefinition[] schedule;

    [Tooltip("Each day past the end of the schedule shortens gaps by this " +
             "fraction. 0.04 = 4% busier per day. Compounds, but is clamped so " +
             "day 50 doesn't become a firehose.")]
    [Range(0f, 0.15f)]
    [SerializeField] private float escalationPerDay = 0.04f;

    [Tooltip("Gaps never fall below this, however far past the schedule you " +
             "get. The floor that stops escalation running away.")]
    [SerializeField] private float minimumGap = 5f;

    [Header("Pacing")]
    // TUNING, 2026-08-27. Scene value was 6s, which put 18-19 arrivals into a
    // 180s day against a shop that demonstrably serves 6-8. Eleven of those
    // people existed only to time out unseen. 15s gives ~11 arrivals, so
    // over-demand is mild and the ones you lose are ones you actually met.
    //
    // NOTE: SerializeField — the SCENE value wins over this default.
    [SerializeField] private float spawnInterval = 15f;
    [SerializeField] private int maxCustomers = 3;

    [Tooltip("How soon to look again when we held someone back because the " +
             "counter queue was full. Shorter than spawnInterval so the door " +
             "reopens promptly once a slot frees, rather than idling.")]
    [SerializeField] private float blockedRetryInterval = 1.5f;

    [Header("Opening rhythm")]

    [Tooltip("Quiet moment after opening before the first customer of the day. " +
             "Rolled fresh each morning, so no two days start the same way.")]
    [SerializeField] private float openingGraceMin = 4f;
    [SerializeField] private float openingGraceMax = 9f;

    [Header("The day's shape at the counter (playtest 4)")]

    // PLAYTEST 4 (10 Oct 2026). The veteran playtester measured that walk-ins
    // appear 18-21 m down the street or in the car park and take 25-52 s
    // (median 38) to reach the till, so Day 5's authored Opening / Morning
    // rush / Lull / Lunch rush / Last orders reached the counter as
    // 0 / 2 / 3 / 1 / 3: the lull was the busiest stretch, and every day
    // opened with about 40 s of nothing. People still walk in from the street
    // (the city's life is part of the look); the schedule now accounts for the
    // walk. The phases are authored as what the COUNTER sees, so the spawner
    // reads the phase at "now + the walk", in seconds (the same at 180 s and
    // at the 420 s ship length).
    [Tooltip("Seconds from a walk-in appearing outside to reaching the counter, used until today's arrivals have measured " +
             "it (each one who reaches the counter teaches it). Measured 10 Oct on the Day 5 lab: 25-52 s, median 38.")]
    [SerializeField, Min(0f)] private float walkLeadSeconds = 36f;

    [Tooltip("How far each customer who reaches the counter moves the measured walk (0 = never learn, 1 = only the last).")]
    [SerializeField, Range(0f, 1f)] private float leadLearning = .3f;

    [Tooltip("At the open, the first arrival is already on their way: this many metres short of the café's door along " +
             "their route, and they walk straight to the counter. At the till about 12 s after opening (was 39-41 s). " +
             "0 = the old opening (a quiet 4-9 s, then the whole walk in).")]
    [SerializeField, Min(0f)] private float openerMetresOut = 3f;

    // LAST ORDERS (12 Oct 2026; the playtesters' #9, the Producer's call: nobody new reaches the till after closing,
    // everyone already inside is served). On the human-speed Day 5 two walk-ins who set off before 8 PM reached the
    // till 6.5 and 11.7 s after it, and service ran 67 s past closing. The clock already stops arrivals at closing and
    // CafeArrivals turns back anyone still on the street then; what let them through was the walk itself: a walk-in
    // needs 25-61 s (median about 38) from the street to the till, through the door and a look round the room. So no
    // walk-in sets off with less than the longest walk left on the clock. The authored phases are read at the till
    // (CounterFraction), so Last orders still gets its arrivals: only the ones who would have come after 8 PM go.
    [Tooltip("Last orders for walk-ins: none sets off with less than this long left on the clock (seconds), or less than " +
             "today's longest measured walk to the till if that is longer: the walk from the street to the till, through the " +
             "door and the look round (measured 10-11 Oct: 25-61 s, the far door about 45-51 s). Named visits keep their own " +
             "rules; whoever is inside is served. The LAST ORDERS sign is its own, earlier warning.")]
    [SerializeField, Min(0f)] private float lastOrdersLeadSeconds = 45f;   // 55 in the first draft; 45 keeps more of a 180 s day hectic (QA, 13 Oct), the longest walk raises it

    // 24 Oct 2026 (Days): LAST ORDERS FOR REPAIRS COME EARLIER. On 23 Oct "the day ended by 7:30" failed 3 times in 9 Day-lab runs,
    // each time someone who set off just before last orders: Nina's drink (7:57), then Alishba's repair twice (8:06, 7:36). A
    // repair takes about 90 s at human speed after the walk and the intake, a drink a fraction of that, so walk-ins who set off in
    // the last stretch before drinks' last orders come for a drink: a repair setting off then would still be on the bench long
    // after closing. Named visits keep their own rules; the authored walk-in list keeps its place for another day.
    [Tooltip("Last orders for walk-in REPAIRS come this much earlier than for drinks, seconds: about a repair's length at human " +
             "speed (90 s). A walk-in who sets off later than that brings a drink order instead. 0: repairs and drinks alike.")]
    [SerializeField, Min(0f)] private float repairLastOrdersExtraSeconds = 90f;

    // 26 Oct 2026 (Days and first table): LAST ORDERS FOR DRINKS COUNT A DRINK'S SERVICE TOO. P2's rule "walk-in repairs stop 90 s
    // before drinks do, so the day ends by 7:30" held for repairs, but the three late days on his Mac (7:42, 7:44 on Day 2;
    // 7:44 on Day 3) were each a DRINK walk-in who set off 357-367 s in, just before drinks' last orders (375 s: the 45 s walk
    // alone), reached the till at 407-411 s and needed 54-57 s more to be heard, served and out of the door (Walter, Mansoor,
    // Yuki). Last orders for drinks only made sure they reached the till by closing; nothing budgeted the drink. A drink takes
    // about 40-55 s from the till to the door at human speed, and the day may run half an hour (30 s) past closing, so drinks'
    // last orders now come this much before the walk's: 45 + 25 = 70 s before closing. Repairs' stay where they were (the walk
    // + 90 s). New field: the scene has no value for it, so this default is what plays.
    [Tooltip("Last orders for walk-in DRINKS come this much earlier than the walk to the till alone, seconds: a drink's service " +
             "from the till to the door at human speed (40-55 s) less the half hour the day may run past closing (30 s). 0: as before.")]
    [SerializeField, Min(0f)] private float drinkServiceSeconds = 25f;

    // 26 Oct 2026 (Days and first table): THE FIRST WALK-IN IS PLANNED ON THE SHORTEST WALK. With the scene's spawn jitter of 0 the
    // day's first walk-in sets off one gap after the opener (60.6 s into Day 2 on all three of his Mac's runs), and the gap was
    // authored with the walk the spawner expects (36 s): a 29.3 s walk reached the till at 89.9 s, against Day 2's "no sooner
    // than 1:30". So the gap after the opener is lengthened by the planned walk less the shortest walk anyone has made (22 s on
    // his Mac, 26 Oct; or shorter, once one is measured): however short the walk, the first walk-in reaches the till no sooner
    // than the opener's gap plus the planned walk (Day 2: 0.5 + 60 + 36 = 96.5 s). Only the first walk-in after the opener moves.
    [Tooltip("The shortest walk from the street to the till anyone makes, seconds (26 Oct 2026, his Mac: 22 s). The first walk-in " +
             "after the opener sets off the planned walk less this later, so a short walk never brings them early. A shorter " +
             "measured walk lowers it.")]
    [SerializeField, Min(0f)] private float shortestWalkSeconds = 22f;
    // 26 Oct 2026 (integration, his Mac's afternoon runs): ...BUT NEVER MORE THAN A FEW SECONDS. The whole 14 s moved every
    // later arrival with it: in 2 of 2 Day 2 runs no walk-in brought the day's watch before repairs' last orders (bench jobs 1
    // customer's + the find), and one run had 35 s with nothing to do. The miss it answered was 0.1 s (89.9 s against 1:30), and
    // the shortest walk measured on his Mac's Day 2 runs was 29.3 s, 6.7 s under the planned 36: so the first walk-in waits at
    // most this long, which keeps every walk measured there at or after its bar without slowing the day.
    [Tooltip("The most the first walk-in after the opener waits for a short walk, seconds (26 Oct 2026: 6).")]
    [SerializeField, Min(0f)] private float firstWalkInCushionAtMost = 6f;

    // 26 Oct 2026 (integration, his Mac's afternoon runs): THE LAST CALL. With drinks' last orders 70 s before closing, a gap that
    // would have ended just after them left the café empty for its last half minute (24-28 s with nothing to do after last
    // orders, against P's 20). So when the next arrival would set off after last orders, it sets off just before them instead:
    // the day's last walk-in comes at last orders, as a last-minute customer does, and is still out of the door by half past.
    [Tooltip("Bring the next arrival forward to just before last orders when its gap would end after them (26 Oct 2026).")]
    [SerializeField] private bool lastCall = true;

    // 26 Oct 2026 (integration, his Mac's afternoon runs): THE DAY'S TAUGHT REPAIR ALWAYS COMES. A walk-in brings today's next
    // authored repair only if their dice say "a device" (the day's drink-only chance), so a day with few walk-ins before repairs'
    // last orders could pass with none: Day 2's watch, the payoff of Day 1's thrift-watch practice, didn't come in 2 runs of 2.
    // So once repairs' last orders are less than this far off and an authored repair is still to come, the next walk-in (not a
    // named visit, not a regular) brings it.
    [Tooltip("Seconds before repairs' last orders from which a walk-in always brings today's next authored repair (26 Oct 2026).")]
    [SerializeField, Min(0f)] private float authoredRepairDueWithin = 75f;

    // 24 Oct 2026 (Days, P's spec §3.1 part 2): THE QUIET NUDGE. When nobody needs Ace, nobody is walking in, and it has been so
    // this long, the next arrival sets off now. Within today's limit and before last orders, and never before today's featured
    // visit is over (Day 1 protects its first repair: the days spec of 14 Oct, call 4). It only acts with nothing left for Ace to
    // do, so a player who is behind never gets an extra customer from it (P's slow-player runs: no change beyond the noise).
    [Tooltip("The quiet nudge: with nobody needing Ace and nobody walking in for this long, the next arrival sets off now " +
             "(within today's limit, before last orders, never before today's featured visit is over). Seconds; 0 = off.")]
    [SerializeField, Min(0f)] private float quietNudgeSeconds = 5f;

    [Tooltip("Radius around the spawn point that arrivals appear within.")]
    [SerializeField] private float spawnScatter = 1.2f;

    [Tooltip("How much each gap between arrivals varies. 0 = a metronome. " +
             "0.35 turns a 6s interval into a 3.9-8.1s spread.\n\n" +
             "Set to 0 while tuning — jitter makes two playtests " +
             "non-comparable, which is exactly what you don't want when " +
             "chasing a number.")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnJitter = 0.35f;

    [Header("Devices they can bring")]
    [SerializeField] private GameObject[] devicePrefabs;

    [Header("Walk-ins")]
    [SerializeField] private CustomerArchetype[] archetypes;
    [SerializeField] private string[] firstNames =
    {
        "Marisol", "Deshawn", "Priya", "Tomas", "Ingrid", "Kenji",
        "Rosa", "Abdi", "Nina", "Walter", "Yuki", "Leandro"
    };
    [Tooltip("Beside First Names, one per name: the body that name walks in wearing (Any, Woman, Man). His sister's to " +
             "correct with the names; a missing entry is Any. NPC wave 2 (14 Oct 2026): a man's name no longer walks in " +
             "wearing a woman's body. Filled by Fixit Fidget > NPC > Wave 2 3.")]
    [SerializeField] private PolygonNpcVisual.NameSex[] firstNameSexes = System.Array.Empty<PolygonNpcVisual.NameSex>();

    [Header("Regulars")]
    [SerializeField] private CustomerProfile[] regulars;
    [Range(0f, 1f)]
    [SerializeField] private float regularChance = 0.35f;

    [Header("Café")]
    [SerializeField] private DrinkDefinition[] drinks;
    [Range(0f, 1f)]
    [Tooltip("Chance a customer came ONLY for a drink and brings no device. " +
             "Lowered from 0.4 now that repair customers can order a coffee too " +
             "— otherwise the bench dries up.")]
    [SerializeField] private float drinkChance = 0.25f;

    private float timer;

    // Today's authored day, resolved once each morning rather than looked up
    // every frame.
    private DayDefinition today;
    private float escalation = 1f;
    private string lastPhase = "";

    // -1 so the very first Update of the very first day counts as "the day
    // changed" and gets its opening grace like every other morning.
    private int lastSeenDay = -1;
    private bool featuredRegularSpawned;
    // The Night 1 slice: someone whose thing Ace took last night comes in first
    // thing to tell Ace about it (MorningFace runs at the counter).
    private CustomerProfile morningVisitor;
    private bool morningVisitorSpawned;
    // Whoever has something to tell Ace about last night comes in first: while they're still walking
    // over (CafeArrivals keeps their brain off until the door), the next arrival waits. A minute at most.
    private CustomerBrain firstThing;
    private float firstThingUntil;
    // Session 3: named regulars who also come in today at an authored time (DayDefinition.storyVisits: the officer),
    // each once; which of today's have come.
    private bool[] storySpawned = System.Array.Empty<bool>();
    // Playtest 3 (6 Oct 2026): the morning the man at the bins has had a word with the officer (NightLedger.WordDue), the
    // officer comes in for his harder question, like the morning's visitor, after 30% of the day; not on a day he's
    // already due by the schedule.
    private CustomerProfile wordVisitor;
    private bool wordVisitorSpawned;
    private const float WordVisitorArrivesAt = .3f;
    private const float FirstThingWaitsAtMost = 60f;
    private readonly CustomerVisitRoster roster = new();
    // Days 1-3 (14 Oct 2026, specs/days-1-3.md §6): how many of today's authored walk-in repairs (DayDefinition.walkInRepairs)
    // have walked in. Reset each morning; never saved (a day's jobs end with the day).
    private int walkInRepairsUsed;

    // Playtest 4: the walk in, learned from the people who make it (spawned -> at the counter, seconds), kept across the
    // session's days; -1 until the first one arrives. Who is still on the way, and when they set off.
    private float measuredLead = -1f;
    private int leadSamples;
    private readonly List<(CustomerBrain brain, float since)> walkingIn = new();
    private const float LongestLeadSeconds = 90f;    // a walk cut short or a day that stood still: not a measurement
    // Last orders (12 Oct): the longest walk to the till measured this session (0 until one is), and when today's last
    // orders for walk-ins were called (seconds into the day; -1 until they are).
    private float longestLead;
    private float shortestLead;   // 26 Oct 2026: the shortest walk to the till measured this session (0 until one is)
    private float lastOrdersCalledAt = -1f;
    // The day's first arrival, already on the way (openerMetresOut): spawned a beat into the day, never on the frame the
    // day changes (CafeArrivals clears its walkers when it sees a new day).
    private bool openerPending;
    private const float OpenerDelay = .5f;
    private const float OpenerWithin = 15f;          // seconds into the day: after that, an ordinary arrival
    // occupancy-and-pacing.md: never more than six customers in the café at once. A named visit the day was written
    // around may go past the day's own cap (the walk-ins' cap), never past this.
    private const int MostCustomersEver = 6;

    // 24 Oct 2026 (Days): the quiet nudge's stretch (Time.time it began, -1 none); last orders for repairs (seconds into the day,
    // -1 until called); the roll in hand is drinks only (repairs closed: RollJob reads it, its signature unchanged for the
    // checks that call it by reflection).
    private float quietSince = -1f;
    private float repairLastOrdersCalledAt = -1f;
    private bool rollDrinksOnly;
    // The cap's hold, measured for the Day labs' bar (§6.8: "a walk-in held back by the cap > 2 s while Ace had <= 1 job: 0"):
    // a walk-in was due and the day's limit held them; when a place frees with Ace nearly free, how long until the next one sets
    // off. Reset each morning.
    private bool capHoldPending;
    private float capFreedAt = -1f;
    private float capCheckAt;
    private int capWaitsOver2s;
    private float longestCapWait;
    private int quietNudges;

    private readonly DayOneOpening opening = new();
    private CustomerBrain openingCustomer;
    private CustomerBrain featuredCustomer;
    private DrinkDefinition openingDrink;
    public DayOneOpening.Step OpeningStep => opening.Current;
    public bool IsGuidedOpening => opening.IsActive;
    public CustomerBrain OpeningCustomer => openingCustomer;
    public CustomerProfile FeaturedHintProfile => today != null && today.GuidesFeaturedVisitOn(lastSeenDay)
        ? today.featuredRegular : null;
    public CustomerBrain FeaturedCustomer => featuredCustomer;
    public bool FeaturedVisitStarted => featuredRegularSpawned;
    public float OpeningHintDuration => today != null
        ? Mathf.Clamp(today.openingHintDuration, 3f, 10f)
        : 6f;

    // A gap between arrivals, varied so the shop doesn't tick like a metronome.
    //
    // With a schedule, the base interval comes from wherever we are in the day
    // rather than being one number for the whole day. That single change is
    // what turns a flat drip into calm / build / rush / recover.
    //
    // Playtest 4: "wherever we are in the day" is where the day will be when
    // the person spawned now reaches the counter (CounterFraction), because
    // that is where the authored shape is felt.
    private float NextGap
    {
        get
        {
            float baseInterval = spawnInterval;

            if (today != null && DayClock.Instance != null)
                baseInterval = today.IntervalAt(CounterFraction) * escalation;

            float gap = baseInterval * Random.Range(1f - spawnJitter, 1f + spawnJitter);

            // Against minimumGap itself, not half of it. The field says "gaps
            // never fall below this"; halving it here made the real floor 2.5s
            // against a stated 5s. Harmless on days 1-5, but day 5 repeats and
            // squeezes 4% per day forever after, so the floor is the only thing
            // stopping the door becoming a firehose around day 30.
            return Mathf.Max(minimumGap, gap);
        }
    }

    // How far through the day we are, 0 at opening and 1 at close.
    private float DayFraction => FractionAt(SecondsNow);

    // Where the day will be when someone spawned now reaches the counter (playtest 4): the authored phases, featured and
    // story visits are all read here, so they happen at the counter when they were written for.
    public float CounterFraction => FractionAt(SecondsNow + LeadSeconds);

    private static float SecondsNow => DayClock.Instance != null ? DayClock.Instance.SecondsIntoDay : 0f;

    // A moment of today, as a fraction of the day (0 at opening, 1 at close).
    private static float FractionAt(float seconds)
    {
        DayClock c = DayClock.Instance;
        if (c == null) return 0f;

        float length = c.SecondsIntoDay + c.TimeRemaining;
        return length <= 0f ? 0f : Mathf.Clamp01(seconds / length);
    }

    /// <summary>
    /// How long a walk-in takes from appearing outside to reaching the counter, seconds: today's measured walk once anyone
    /// has made it, else Walk Lead Seconds (playtest 4). The spawner looks this far ahead.
    /// </summary>
    public float LeadSeconds => measuredLead > 0f ? measuredLead : walkLeadSeconds;

    /// <summary>How many walks in have been measured this session (reports).</summary>
    public int LeadSamples => leadSamples;

    /// <summary>The longest walk from the street to the till measured this session, seconds (0 until one is).</summary>
    public float LongestWalkMeasured => longestLead;

    /// <summary>
    /// Last orders for walk-ins (12 Oct, #9): how long before closing the last walk-in may set off, seconds: the longest
    /// walk to the till (Last Orders Lead Seconds, or today's longest measured walk if that is longer), and since 26 Oct 2026
    /// a drink's service on top (Drink Service Seconds: so the last drink is out of the door by half past).
    /// </summary>
    public float LastOrdersLead => WalkLastOrdersLead + drinkServiceSeconds;   // 26 Oct 2026: and a drink's service

    /// <summary>The longest walk to the till (Last Orders Lead Seconds, or today's longest measured walk if longer), seconds.</summary>
    public float WalkLastOrdersLead => Mathf.Max(lastOrdersLeadSeconds, longestLead);

    /// <summary>26 Oct 2026: how much earlier than the walk drinks' last orders come (a drink's service less the half hour's grace).</summary>
    public float DrinkServiceSeconds => drinkServiceSeconds;

    /// <summary>26 Oct 2026: the shortest walk to the till the first walk-in is planned on (the field, or a shorter one measured).</summary>
    public float ShortestWalk => shortestLead > 0f ? Mathf.Min(shortestLead, shortestWalkSeconds) : shortestWalkSeconds;

    /// <summary>26 Oct 2026: how much later than its gap the first walk-in after the opener sets off: the planned walk less the shortest.</summary>
    public float FirstWalkInCushion => Mathf.Min(firstWalkInCushionAtMost, Mathf.Max(0f, LeadSeconds - ShortestWalk));   // integration: at most 6 s

    /// <summary>True once no walk-in may set off any more today: less than the longest walk and a drink's service is left.</summary>
    public bool LastOrdersForWalkIns => DayClock.Instance != null && DayClock.Instance.TimeRemaining <= LastOrdersLead;

    /// <summary>When today's last orders for walk-ins were called, seconds into the day; -1 until they are (reports).</summary>
    public float LastOrdersCalledAt => lastOrdersCalledAt;

    /// <summary>
    /// Last orders for walk-in REPAIRS (24 Oct 2026): how long before closing the last walk-in who brings a device may set off,
    /// seconds: drinks' lead plus a repair's length at human speed.
    /// </summary>
    public float RepairLastOrdersLead => Mathf.Max(WalkLastOrdersLead + repairLastOrdersExtraSeconds, LastOrdersLead);   // 26 Oct 2026: from the walk, as before

    /// <summary>True once no walk-in may set off with a repair any more today (they come for a drink instead).</summary>
    public bool LastOrdersForRepairs => DayClock.Instance != null && DayClock.Instance.TimeRemaining <= RepairLastOrdersLead;

    /// <summary>When today's last orders for repairs were called, seconds into the day; -1 until they are (reports).</summary>
    public float RepairLastOrdersCalledAt => repairLastOrdersCalledAt;

    /// <summary>
    /// Today's featured visit is over (24 Oct 2026; the quiet nudge and the thrift find wait for it): the day has none, the
    /// regular can't visit today, or they came in and have left (or are leaving).
    /// </summary>
    public bool FeaturedVisitOver
    {
        get
        {
            if (today == null || today.featuredRegular == null) return true;
            if (!featuredRegularSpawned) return !roster.CanVisit(today.featuredRegular.PersistentId);
            return featuredCustomer == null || featuredCustomer.IsLeaving;
        }
    }

    /// <summary>The day's cap held a walk-in back and, with Ace nearly free once a place freed, the next set off this many times
    /// more than 2 s later, and the longest such wait (seconds; the Day labs' bar, §6.8). Reset each morning.</summary>
    public int CapWaitsOver2s => capWaitsOver2s;
    public float LongestCapWait => longestCapWait;

    /// <summary>How many times today the quiet nudge set the next arrival off early (reports).</summary>
    public int QuietNudges => quietNudges;

    // Awake (24 Oct 2026): the downtime meter rides on the spawner (no scene change), and so does the gossip at the tables.
    private void Awake()
    {
        if (GetComponent<DowntimeMeter>() == null) gameObject.AddComponent<DowntimeMeter>();
        if (GetComponent<GuestGossip>() == null) gameObject.AddComponent<GuestGossip>();
    }

    // 24 Oct 2026 (Days): at closing the café is tidied: the cups guests left on the tables go (the man's mess keeps its own
    // morning, LodgerDay), so a new day never opens on yesterday's guests' cups.
    private DayClock closingClock;

    private void Start()
    {
        closingClock = DayClock.Instance;
        if (closingClock != null) closingClock.OnDayEnded += TidyAtClosing;
    }

    private void OnDestroy()
    {
        if (closingClock != null) closingClock.OnDayEnded -= TidyAtClosing;
    }

    private static void TidyAtClosing()
    {
        DirtyCup.PutAwayGuestsCups();
        // A crate of cups and beans still in hand goes back too (the stock never left the shelf: StockCrate).
        foreach (StockCrate crate in FindObjectsByType<StockCrate>(FindObjectsInactive.Exclude)) Destroy(crate.gameObject);
    }

    /// <summary>Today's authored day, or null (reports and the day-shape check).</summary>
    public DayDefinition Today => today;

    /// <summary>The days as authored, in the schedule's order (24 Oct 2026: the thrift shelf looks its finds up here).</summary>
    public IReadOnlyList<DayDefinition> Schedule => schedule;

    /// <summary>The walk-ins' devices (24 Oct 2026: the thrift shelf finds its devices here by name).</summary>
    public IReadOnlyList<GameObject> DevicePool => devicePrefabs;

    /// <summary>The day asset authored for exactly this day number, or null (a repeated last day has no find of its own:
    /// 24 Oct 2026, the thrift shelf's tomorrow).</summary>
    public DayDefinition AuthoredDay(int dayNumber)
    {
        if (schedule == null) return null;
        foreach (DayDefinition d in schedule) if (d != null && d.dayNumber == dayNumber) return d;
        return null;
    }

    // The authored day for this day number, or the last one repeated with a
    // compounding squeeze. You'll author five or eight days; the arc runs to
    // fifty. Repeating the last one is the honest default — it keeps playing
    // rather than falling off a cliff, and you can author more whenever.
    private void ResolveToday(int dayNumber)
    {
        today = null;
        escalation = 1f;

        if (schedule == null || schedule.Length == 0) return;

        foreach (DayDefinition d in schedule)
            if (d != null && d.dayNumber == dayNumber) { today = d; return; }

        DayDefinition last = null;
        foreach (DayDefinition d in schedule)
            if (d != null && (last == null || d.dayNumber > last.dayNumber)) last = d;

        if (last == null) return;

        today = last;

        int past = Mathf.Max(0, dayNumber - last.dayNumber);
        escalation = Mathf.Pow(1f - escalationPerDay, past);

        // Don't let fifty days of compounding turn the door into a firehose.
        float floor = last.IntervalAt(0.5f) > 0f ? minimumGap / last.IntervalAt(0.5f) : 0.4f;
        escalation = Mathf.Max(escalation, Mathf.Clamp01(floor));
    }

    private void Update()
    {
        // A new morning. Opening the shop shouldn't mean someone is already at
        // the counter — there's a beat where the room is yours, and then the
        // first person wanders in.
        //
        // Watched here rather than driven by an event on DayClock: this is the
        // only system that cares, so the knowledge stays where it's used.
        if (DayClock.Instance != null && DayClock.Instance.Day != lastSeenDay)
        {
            lastSeenDay = DayClock.Instance.Day;
            ResolveToday(lastSeenDay);
            ResetRoster();
            featuredRegularSpawned = false;
            featuredCustomer = null;
            morningVisitor = ResolveMorningVisitor(lastSeenDay);
            morningVisitorSpawned = false;
            storySpawned = today != null ? new bool[today.StoryVisitsOn(lastSeenDay).Length] : System.Array.Empty<bool>();
            wordVisitor = ResolveWordVisitor(lastSeenDay);
            wordVisitorSpawned = false;
            walkInRepairsUsed = 0;
            firstThing = null;
            openingCustomer = null;
            openingDrink = ResolveOpeningDrink();
            bool guide = today != null && today.GuidesOpeningOn(lastSeenDay);
            if (guide && openingDrink == null)
            {
                Debug.LogWarning("Day 1 introduction needs a drink definition. " +
                                 "Using the normal schedule instead.", this);
                guide = false;
            }
            opening.Reset(guide);
            lastPhase = "";
            walkingIn.Clear();
            lastOrdersCalledAt = -1f;
            // 24 Oct 2026 (Days): the day's own measures start again.
            repairLastOrdersCalledAt = -1f;
            quietSince = -1f;
            capHoldPending = false;
            capFreedAt = -1f;
            capWaitsOver2s = 0;
            longestCapWait = 0f;
            quietNudges = 0;
            // Playtest 4: the day's first arrival is already on the street when the café opens (TryArriveNearTheDoor),
            // so the till sees them about 12 s in instead of 40. Without the arrivals system they would appear at the
            // door in view, and the guided Day 1 lesson paces itself: both keep the old quiet opening.
            openerPending = openerMetresOut > 0f && !opening.IsActive
                            && CafeArrivals.Instance != null && CafeArrivals.Instance.isActiveAndEnabled;
            timer = openerPending ? OpenerDelay : Random.Range(openingGraceMin, openingGraceMax);

            DayClock.Instance.PatienceMultiplier =
                today != null ? today.patienceMultiplier : 1f;

            if (today != null)
                Debug.Log($"[Day {lastSeenDay}] {today.name} — {today.intent}" +
                          (escalation < 0.999f ? $"  (repeated, {(1f - escalation) * 100f:0}% busier)" : ""));

            return;
        }

        // Observe real departure, including refusal/time-out, even after the
        // door closes. The lesson never consumes input or changes the clock.
        if (opening.VisitInProgress &&
            (openingCustomer == null || openingCustomer.IsLeaving))
        {
            opening.FinishVisit();
            openingCustomer = null;
            timer = opening.IsActive ? blockedRetryInterval : NextGap;
        }

        LearnTheWalk();

        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return;

        // Someone coming in to tell Ace about last night is still on their way over: the next
        // arrival's countdown waits for them to reach the door (the Night 1 slice).
        if (firstThing != null && !firstThing.enabled && Time.time < firstThingUntil) return;
        firstThing = null;

        // 24 Oct 2026 (Days, P's spec §3.1): a place the cap was holding frees with Ace nearly free (at most one customer step
        // left): the held walk-in comes in Blocked Retry Interval, not a whole gap later. With two or more steps left it waits as
        // it always did. Looked at four times a second while a hold is pending (a scene count).
        WatchTheCap();
        // The quiet nudge (part 2 of the rule): nobody needs Ace and nobody is on the way: the next arrival sets off now.
        if (QuietNudgeDue() && timer > 0f)
        {
            timer = 0f;
            quietNudges++;
        }
        // 26 Oct 2026 (integration): the last call (see the field): a gap that would end after last orders ends just before them.
        if (lastCall && !LastOrdersForWalkIns && DayClock.Instance != null)
        {
            float untilLastOrders = DayClock.Instance.TimeRemaining - LastOrdersLead;
            if (untilLastOrders > 0f && timer > untilLastOrders) timer = Mathf.Max(0f, untilLastOrders - .5f);
        }

        timer -= Time.deltaTime;
        if (opening.VisitInProgress) return;
        if (timer > 0f) return;

        timer = NextGap;

        // Crossing into a new phase is the day's shape becoming visible. Logged
        // rather than surfaced in the UI for now — the hanging sign in
        // hud-spec.md is where a player should eventually feel this, by
        // watching the clock walk toward midday.
        if (today != null)
        {
            // The phase the people setting off now will reach the counter in (playtest 4: the spawner works that far ahead).
            string phase = today.PhaseNameAt(CounterFraction);
            if (phase != lastPhase)
            {
                lastPhase = phase;
                Debug.Log($"[Day {lastSeenDay}] phase at the counter: {phase} " +
                          $"(gaps ~{today.IntervalAt(CounterFraction) * escalation:0.0}s; " +
                          $"{DayFraction * 100f:0}% through the day, the walk in ~{LeadSeconds:0} s)");
            }
        }

        int cap = today != null ? today.maxCustomers : maxCustomers;
        if (opening.IsActive) cap = 1;
        else
        {
            // PLAYTEST 4 (10 Oct 2026): a named visit the day was written around - the officer's coffee on Day 1, a
            // regular with news of last night - comes when it is due; the day's cap limits walk-ins. Day 1's cap of 2
            // kept the officer's coffee (written for 35% of the day) out until 6:47 PM, behind Grace and one walk-in.
            // Never past six in the café (occupancy-and-pacing.md), and never without a place at the counter (below).
            WhoIsDue(out bool featuredDue, out bool morningDue, out bool wordDue, out int storyDue);
            bool named = featuredDue || morningDue || wordDue || storyDue >= 0;
            if (named) cap = Mathf.Max(cap, MostCustomersEver);

            // Last orders for walk-ins (12 Oct, #9): with less than the longest walk left on the clock, a walk-in setting
            // off now would reach the till after closing, so none does. A named visit keeps its own rule (it is due by
            // the day's authored time, read at the till, so it is normally in long before this).
            if (!named && LastOrdersForWalkIns)
            {
                if (lastOrdersCalledAt < 0f)
                {
                    lastOrdersCalledAt = SecondsNow;
                    Debug.Log($"[Day {lastSeenDay}] last orders for walk-ins: {DayClock.Instance.TimeRemaining:0} s before closing " +
                              $"(the walk to the till takes up to {WalkLastOrdersLead:0} s, a drink {drinkServiceSeconds:0} s more" +   // 26 Oct 2026
                              $"{(longestLead > 0f ? $", the longest measured today {longestLead:0} s" : "")}). " +
                              "Nobody new reaches the till after closing; everyone inside is served.");
                }
                return;
            }
        }

        int living = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude).Length;
        if (living >= cap)
        {
            if (opening.IsActive) timer = blockedRetryInterval;
            // 24 Oct 2026 (Days, P's spec §3.1 part 1; CustomerSpawner used to lose a whole gap here): the cap held a walk-in
            // back. With Ace nearly free, look again soon; WatchTheCap brings them in once a place frees.
            else
            {
                if (RetrySoon(Downtime.CustomerSteps())) timer = blockedRetryInterval;
                if (!capHoldPending) { capHoldPending = true; capFreedAt = -1f; }
            }
            return;
        }

        // Don't create someone who has nowhere to stand.
        //
        // CustomerBrain.Init claims a counter slot, and with none free it sends
        // them straight back to the exit — where they're destroyed without ever
        // being counted. The player sees a customer walk in, turn around and
        // leave, with no explanation and no mark on the recap.
        //
        // Hold them at the door instead and check again shortly.
        //
        // Customers still walking over from the car park or a neighbour's door
        // (CafeArrivals) each claim a slot the moment they reach the door, so
        // each of them counts against the free slots too. With nobody walking
        // over this is exactly the old HasFreeSlot test.
        if (counterQueue != null
            && counterQueue.FreeSlotCount <= NpcJourney.OnTheWay(CafeArrivals.Kind.Customer))
        {
            timer = blockedRetryInterval;
            return;
        }

        Spawn();
    }

    // 24 Oct 2026 (Days, P's spec §3.1 part 1): while the cap is holding a walk-in back, watch for a place to free. When one does
    // and Ace has at most one customer step left, the held walk-in sets off within Blocked Retry Interval (the moment is kept for
    // the Day labs' measure, ended in Spawn). With two or more steps left, the countdown runs on as it always did.
    private void WatchTheCap()
    {
        if (!capHoldPending || opening.IsActive) return;
        if (Time.time < capCheckAt) return;
        capCheckAt = Time.time + .25f;
        int cap = today != null ? today.maxCustomers : maxCustomers;
        int living = FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude).Length;
        if (living >= cap) { capFreedAt = -1f; return; }
        if (capFreedAt >= 0f) return;                     // already freed and timed
        if (RetrySoon(Downtime.CustomerSteps()))
        {
            capFreedAt = Time.time;
            timer = Mathf.Min(timer, blockedRetryInterval);
        }
        else capHoldPending = false;                      // freed while Ace is busy: the old rule, and nothing to measure
    }

    // 24 Oct 2026 (Days, P's spec §3.1 part 2): the quiet nudge is due (once a quiet stretch): nobody needs Ace and nobody is
    // walking in, for Quiet Nudge Seconds; never during the guided opening, before today's featured visit is over, or once last
    // orders for walk-ins are called. The cap still applies when the arrival is due (Update), as for any arrival.
    private bool QuietNudgeDue()
    {
        if (quietNudgeSeconds <= 0f || !NudgeAllowed(opening.IsActive, opening.VisitInProgress, FeaturedVisitOver, LastOrdersForWalkIns))
        {
            quietSince = -1f;
            return false;
        }
        bool quiet = Downtime.NobodyNeedsAce && Downtime.WalkingIn == 0;
        if (!quiet) { quietSince = -1f; return false; }
        if (quietSince < 0f) { quietSince = Time.time; return false; }
        if (Time.time - quietSince < quietNudgeSeconds) return false;
        quietSince = -1f;
        return true;
    }

    /// <summary>The cap's retry (P's §3.1 part 1) acts only while Ace has at most this many customer steps left.</summary>
    public const int RetryWhileStepsAtMost = 1;

    /// <summary>24 Oct 2026 (Days): whether the cap's retry acts with this many customer steps left (pure: the Downtime rules check it).</summary>
    public static bool RetrySoon(int customerSteps) => customerSteps <= RetryWhileStepsAtMost;

    /// <summary>
    /// 24 Oct 2026 (Days): whether the quiet nudge may act at all (P's §3.1 part 2): never in the guided opening or its visit,
    /// never before today's featured visit is over (Day 1 protects the first repair), never after last orders (pure: the
    /// Downtime rules check it).
    /// </summary>
    public static bool NudgeAllowed(bool openingActive, bool visitInProgress, bool featuredVisitOver, bool lastOrders) =>
        !openingActive && !visitInProgress && featuredVisitOver && !lastOrders;

    private void Spawn()
    {
        if (devicePrefabs == null || devicePrefabs.Length == 0) return;

        // 24 Oct 2026 (Days): the cap's hold ends here; how long the walk-in waited once a place freed (the Day labs' bar).
        if (capHoldPending)
        {
            if (capFreedAt >= 0f)
            {
                float waited = Time.time - capFreedAt;
                longestCapWait = Mathf.Max(longestCapWait, waited);
                if (waited > 2f) capWaitsOver2s++;
            }
            capHoldPending = false;
            capFreedAt = -1f;
        }

        GameObject go = Instantiate(customerPrefab, ScatteredSpawn(), spawnPoint.rotation);

        CustomerBrain brain = go.GetComponent<CustomerBrain>();
        CustomerIdentity id = go.GetComponent<CustomerIdentity>();

        // The day's first arrival, already on the way (playtest 4): only at the very start of the day.
        bool opener = openerPending && SecondsNow < OpenerWithin;
        openerPending = false;
        // 26 Oct 2026 (Days and first table): the first walk-in after the opener is planned on the shortest walk (see the field).
        if (opener) timer += FirstWalkInCushion;

        // Who is this? A featured regular takes the next available arrival
        // slot after their authored time. If the counter is full, they wait for
        // a real opening rather than spawning into nowhere.
        CustomerProfile profile = null;
        WhoIsDue(out bool featuredDue, out bool morningDue, out bool wordDue, out int storyDue);

        if (featuredDue)
        {
            profile = today.featuredRegular;
            featuredRegularSpawned = true;
        }
        else if (morningDue)
        {
            profile = morningVisitor;
            morningVisitorSpawned = true;
        }
        else if (wordDue)
        {
            profile = wordVisitor;
            wordVisitorSpawned = true;
        }
        else if (storyDue >= 0)
        {
            profile = today.StoryVisitsOn(lastSeenDay)[storyDue].who;
            storySpawned[storyDue] = true;
        }
        // 24 Oct 2026 (Days): after last orders for repairs, a walk-in comes for a drink (rollDrinksOnly, below), so no random
        // regular either: their own reasons for a visit can be a repair.
        else if (!opening.IsActive && !LastOrdersForRepairs)
        {
            float regChance = today != null ? today.regularChance : regularChance;
            bool isRegular = regulars != null
                          && regulars.Length > 0
                          && Random.value < regChance;

            if (isRegular) profile = PickAvailableRegular();
        }

        if (profile != null)
        {
            RegularMemoryData memory = SaveManager.Instance != null
                ? SaveManager.Instance.MemoryFor(profile) : null;
            id.SetupRegular(profile, memory);
        }
        else
        {
            CustomerArchetype a = PickArchetype();
            int nameIndex = roster.RemainingNames > 0 ? Random.Range(0, roster.RemainingNames) : 0;
            id.SetupWalkIn(a, roster.TakeName(nameIndex));
        }

        // What are they bringing, and what's wrong with it?
        JobKind? forcedKind = opening.IsActive
            ? (opening.Current == DayOneOpening.Step.Drink ? JobKind.Drink : JobKind.Repair)
            : null;
        Job job = null;
        if (featuredDue && today.useFeaturedRepair)
        {
            string error = "Choose a featured repair request.";
            if (today.featuredRepair == null || !today.featuredRepair.TryCreateJob(out job, out error))
                Debug.LogWarning($"{today.name}: featured repair is invalid ({error}). Using the regular's normal job.", today);
        }
        // 24 Oct 2026 (Days): last orders for walk-in repairs (a repair takes about 90 s at human speed): a walk-in setting off
        // now comes for a drink. Named visits and the guided opening keep their own reasons.
        bool named = featuredDue || morningDue || wordDue || storyDue >= 0;
        rollDrinksOnly = !named && !opening.IsActive && profile == null && LastOrdersForRepairs;
        if (rollDrinksOnly && repairLastOrdersCalledAt < 0f)
        {
            repairLastOrdersCalledAt = SecondsNow;
            Debug.Log($"[Day {lastSeenDay}] last orders for walk-in repairs: {(DayClock.Instance != null ? DayClock.Instance.TimeRemaining : 0f):0} s " +
                      $"before closing (a repair takes about {repairLastOrdersExtraSeconds:0} s after the walk); walk-ins come for drinks from here.");
        }
        if (job == null) job = RollJob(profile, forcedKind);
        rollDrinksOnly = false;

        // And would they like something while they wait? Rolled here, kept
        // quiet by CustomerBrain until they've sat down.
        DrinkDefinition wish = RollDrinkWish(id, job);

        // Where they come from. Outside the guided Day 1 opening (whose pacing is
        // authored), a new customer gets out of a car in the café's lot or leaves
        // a neighbour's front door and walks over; they only start - counter slot,
        // patience, drift into the room - once they reach the door. Without
        // CafeArrivals in the scene they start at the door, as they always did.
        //
        // Playtest 4: the day's first is already on the café's own pavement when
        // it opens, a few metres short of the door, and walks straight to the
        // counter (no look round the room): at the till about 12 s in.
        bool walkingOver = false;
        if (!opening.IsActive)
        {
            if (opener)
            {
                brain.StraightToCounter = true;
                walkingOver = CafeArrivals.TryArriveNearTheDoor(go, CafeArrivals.Kind.Customer, openerMetresOut,
                    () => brain.Init(counterQueue, exitPoint, job, wish));
            }
            else walkingOver = CafeArrivals.TryArrive(go, CafeArrivals.Kind.Customer,
                () => brain.Init(counterQueue, exitPoint, job, wish));
        }
        if (!walkingOver) brain.Init(counterQueue, exitPoint, job, wish);
        // The opener's walk is a short one on purpose: it would teach the spawner the wrong walk.
        if (walkingOver && !opener) walkingIn.Add((brain, SecondsNow));
        if (featuredDue) featuredCustomer = brain;
        if (walkingOver && HasNightNews(profile))
        {
            firstThing = brain;
            firstThingUntil = Time.time + FirstThingWaitsAtMost;
        }
        if (profile != null) roster.RecordArrival(profile.PersistentId);
        if (opening.TryStartVisit()) openingCustomer = brain;
    }

    // Who the next arrival is, by the day's authored visits: a featured regular, the morning's visitor (news of last
    // night), the officer after the man's word, then a story visit; at most one of them (all false and -1: a walk-in or
    // a random regular). Their times are read at the counter (CounterFraction, playtest 4): a visit written for 35% of the
    // day is at the counter at about 35%, not a walk later.
    private void WhoIsDue(out bool featuredDue, out bool morningDue, out bool wordDue, out int storyDue)
    {
        featuredDue = today != null
                      && today.featuredRegular != null
                      && !featuredRegularSpawned
                      && roster.CanVisit(today.featuredRegular.PersistentId)
                      && opening.AllowsFeatured(CounterFraction, today.featuredRegularArrivesAt);

        morningDue = !featuredDue && morningVisitor != null && !morningVisitorSpawned
                     && !opening.IsActive && roster.CanVisit(morningVisitor.PersistentId);

        // The officer, on the morning the man at the bins has had a word with him: the next slot after those two.
        wordDue = !featuredDue && !morningDue && wordVisitor != null && !wordVisitorSpawned && !opening.IsActive
                  && roster.CanVisit(wordVisitor.PersistentId) && opening.AllowsFeatured(CounterFraction, WordVisitorArrivesAt);

        // A story visit (the officer) whose time has come takes the next slot after those.
        storyDue = featuredDue || morningDue || wordDue ? -1 : StoryVisitDue();
    }

    // Today's first story visit whose time has come and who hasn't been in yet, or -1. Never during the Day 1 lesson's
    // two visits (the same rule as the featured regular).
    private int StoryVisitDue()
    {
        if (today == null || opening.IsActive) return -1;
        StoryVisit[] visits = today.StoryVisitsOn(lastSeenDay);
        for (int i = 0; i < visits.Length && i < storySpawned.Length; i++)
        {
            StoryVisit visit = visits[i];
            if (visit == null || visit.who == null || storySpawned[i]) continue;
            if (roster.CanVisit(visit.who.PersistentId) && opening.AllowsFeatured(CounterFraction, visit.arrivesAt)) return i;
        }
        return -1;
    }

    // The walk in, measured (playtest 4): each customer who set off from the street and has now reached the counter
    // moves the spawner's look-ahead towards how long it took them. Walkers who never got there (turned back at closing,
    // or removed) teach nothing.
    private void LearnTheWalk()
    {
        for (int i = walkingIn.Count - 1; i >= 0; i--)
        {
            CustomerBrain who = walkingIn[i].brain;
            if (who == null) { walkingIn.RemoveAt(i); continue; }
            if (who.ReachedCounterAt < 0f) continue;
            float took = who.ReachedCounterAt - walkingIn[i].since;
            walkingIn.RemoveAt(i);
            if (took <= 0f || took > LongestLeadSeconds || leadLearning <= 0f) continue;
            measuredLead = measuredLead > 0f ? Mathf.Lerp(measuredLead, took, leadLearning) : took;
            leadSamples++;
            // The longest walk is what last orders go by (12 Oct): someone CAN take this long to the till.
            longestLead = Mathf.Max(longestLead, took);
            shortestLead = shortestLead > 0f ? Mathf.Min(shortestLead, took) : took;   // 26 Oct 2026: the first walk-in's plan
        }
    }

    /// <summary>Who is still to come in today on a story visit, in order (reports and checks).</summary>
    public IEnumerable<CustomerProfile> StoryVisitorsToCome
    {
        get
        {
            if (today == null) yield break;
            StoryVisit[] visits = today.StoryVisitsOn(lastSeenDay);
            for (int i = 0; i < visits.Length && i < storySpawned.Length; i++)
                if (visits[i] != null && visits[i].who != null && !storySpawned[i]) yield return visits[i].who;
        }
    }

    // The Night 1 slice: the first regular with something to tell Ace this morning (NightLedger),
    // found among the regulars or the schedule's featured ones. Today's featured regular is left
    // out: their authored visit brings them anyway.
    private CustomerProfile ResolveMorningVisitor(int day)
    {
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (night == null) return null;
        string featuredId = today != null && today.featuredRegular != null ? today.featuredRegular.PersistentId : null;
        foreach (string owner in night.OwnersDue(day))
        {
            if (owner == featuredId) continue;
            if (regulars != null)
                foreach (CustomerProfile profile in regulars)
                    if (profile != null && profile.PersistentId == owner) return profile;
            if (schedule != null)
                foreach (DayDefinition authored in schedule)
                    if (authored != null && authored.featuredRegular != null && authored.featuredRegular.PersistentId == owner)
                        return authored.featuredRegular;
        }
        return null;
    }

    /// <summary>Who is coming in this morning to tell Ace about last night, or null (reports).</summary>
    public CustomerProfile MorningVisitor => morningVisitorSpawned ? null : morningVisitor;

    /// <summary>The officer, still to come in today for the man's word (NightLedger.WordDue), or null (reports and checks).</summary>
    public CustomerProfile WordVisitor => wordVisitorSpawned ? null : wordVisitor;
    /// <summary>The officer has come in today for the man's word.</summary>
    public bool WordVisitorCame => wordVisitorSpawned;

    // The officer's profile, on a word's day only, found among the regulars or the schedule's story visits; null when the
    // schedule brings him in today anyway (his question comes with that visit).
    private CustomerProfile ResolveWordVisitor(int day)
    {
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (night == null || !night.WordDue(day)) return null;
        if (today != null)
            foreach (StoryVisit visit in today.StoryVisitsOn(day))
                if (visit != null && visit.who != null && visit.who.PersistentId == OfficerStory.ProfileId) return null;
        if (regulars != null)
            foreach (CustomerProfile profile in regulars)
                if (profile != null && profile.PersistentId == OfficerStory.ProfileId) return profile;
        if (schedule != null)
            foreach (DayDefinition authored in schedule)
            {
                if (authored == null || authored.storyVisits == null) continue;
                foreach (StoryVisit visit in authored.storyVisits)
                    if (visit != null && visit.who != null && visit.who.PersistentId == OfficerStory.ProfileId) return visit.who;
            }
        return null;
    }

    // A regular with a deed of Ace's still to tell (NightLedger): the morning's visitor, or today's
    // featured regular whose thing went missing last night.
    private bool HasNightNews(CustomerProfile profile)
    {
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        return profile != null && night != null && night.Unfaced(profile.PersistentId, lastSeenDay) != null;
    }

    private void ResetRoster()
    {
        List<string> reserved = new();
        if (regulars != null)
            foreach (CustomerProfile profile in regulars)
                if (profile != null) reserved.Add(profile.characterName);
        // Reserve scheduled names on every day, not just their featured day (story visitors' too).
        if (schedule != null)
            foreach (DayDefinition day in schedule)
            {
                if (day == null) continue;
                if (day.featuredRegular != null) reserved.Add(day.featuredRegular.characterName);
                if (day.storyVisits != null)
                    foreach (StoryVisit visit in day.storyVisits)
                        if (visit != null && visit.who != null) reserved.Add(visit.who.characterName);
            }
        roster.Reset(firstNames, reserved);
        // Which body each name walks in wearing (NPC wave 2): PolygonNpcVisual deals named walk-ins their name's looks.
        PolygonNpcVisual.RegisterNameSexes(firstNames, firstNameSexes);
    }

    private CustomerProfile PickAvailableRegular()
    {
        List<CustomerProfile> eligible = new();
        HashSet<string> seenIds = new(System.StringComparer.Ordinal);
        string featuredId = today != null && today.featuredRegular != null ? today.featuredRegular.PersistentId : null;
        foreach (CustomerProfile profile in regulars)
        {
            if (profile == null || !roster.CanVisitRandomly(profile.PersistentId, featuredId)) continue;
            if (seenIds.Add(profile.PersistentId)) eligible.Add(profile);
        }
        return eligible.Count > 0 ? eligible[Random.Range(0, eligible.Count)] : null;
    }

    private DrinkDefinition ResolveOpeningDrink()
    {
        if (today != null && today.openingDrink != null) return today.openingDrink;
        if (drinks != null)
            foreach (DrinkDefinition drink in drinks)
                if (drink != null) return drink;
        return null;
    }

    // Everyone materialising on the exact same tile is the first half of why
    // arrivals look like a queue at a school canteen. Nudge each one somewhere
    // slightly different, then pull it back onto walkable floor so nobody
    // spawns inside the door frame.
    private Vector3 ScatteredSpawn()
    {
        if (spawnScatter <= 0f) return spawnPoint.position;

        Vector2 o = Random.insideUnitCircle * spawnScatter;
        Vector3 probe = spawnPoint.position + new Vector3(o.x, 0f, o.y);

        return NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas)
             ? hit.position
             : spawnPoint.position;
    }

    // Only repair customers get a wish — someone who came for a latte already
    // has one. Null when the archetype says no, or there's nothing to make.
    private DrinkDefinition RollDrinkWish(CustomerIdentity id, Job job)
    {
        if (job == null || job.kind != JobKind.Repair) return null;
        if (today != null && today.SuppressesRepairDrinksOn(lastSeenDay)) return null;
        if (drinks == null || drinks.Length == 0) return null;

        float chance = id != null ? id.DrinkWishChance : 0f;
        if (Random.value >= chance) return null;

        if (id != null && id.Profile != null && id.Profile.preferredDrink != null)
            return id.Profile.preferredDrink;

        return drinks[Random.Range(0, drinks.Length)];
    }

    // Today's allowed personalities, or all of them if the day doesn't say.
    //
    // This is the pressure budget as an authoring decision. Day 1 has no
    // Impatient and no Rushed — not because the game noticed you struggling and
    // backed off, but because you wrote a gentle first day. A game that eases
    // up when you're drowning takes the credit for your recovery, and the whole
    // point is that the player owns the chaos.
    private CustomerArchetype PickArchetype()
    {
        if (archetypes == null || archetypes.Length == 0) return null;

        if (today == null || today.allowedArchetypes == null || today.allowedArchetypes.Length == 0)
            return archetypes[Random.Range(0, archetypes.Length)];

        List<CustomerArchetype> allowed = new();
        foreach (CustomerArchetype a in archetypes)
            if (a != null && today.AllowsArchetype(a.archetypeName)) allowed.Add(a);

        // A day that names archetypes the spawner doesn't have would otherwise
        // spawn faceless customers with no dialogue at all. Fall back loudly.
        if (allowed.Count == 0)
        {
            Debug.LogWarning($"[Day {lastSeenDay}] none of the allowed archetypes " +
                             $"({string.Join(", ", today.allowedArchetypes)}) exist on " +
                             "the spawner. Using all of them instead.", this);
            return archetypes[Random.Range(0, archetypes.Length)];
        }

        return allowed[Random.Range(0, allowed.Count)];
    }

    private Job RollJob(CustomerProfile profile, JobKind? forcedKind = null)
    {
        // Café or repair? Walk-ins follow the day. A named regular may carry
        // an authored reason for visiting so their story cannot randomly turn
        // into "my hot chocolate is broken."
        float dChance = today != null ? today.drinkOnlyChance : drinkChance;
        bool hasDrinks = drinks != null && drinks.Length > 0;
        bool wantsDrink = profile != null
            ? profile.primaryVisitKind switch
            {
                RegularVisitKind.RepairOnly => false,
                RegularVisitKind.DrinkOnly  => hasDrinks,
                _                           => hasDrinks && Random.value < dChance
            }
            : hasDrinks && Random.value < dChance;

        if (forcedKind.HasValue) wantsDrink = forcedKind.Value == JobKind.Drink;
        // 24 Oct 2026 (Days): last orders for walk-in repairs (Spawn sets this for the one roll): a drink, whatever the day's mix.
        else if (rollDrinksOnly && profile == null && hasDrinks) wantsDrink = true;
        // 26 Oct 2026 (integration): the day's taught repair always comes (see the field): close to repairs' last orders, a
        // walk-in brings today's next authored repair whatever their dice said.
        else if (profile == null && AuthoredRepairDue) wantsDrink = false;

        // DAYS 1-3 (14 Oct 2026, specs/days-1-3.md §6; the Producer's call 8). On a day with authored walk-in repairs a
        // walk-in who brings a device brings the next one in order, so nobody walks in with a job the game hasn't taught yet
        // (playtest 5: a random roll gave Day 2 Walter's seized watch, and Grace and Walter both stormed out). Once they are
        // used up, walk-ins come for drinks. Named visits keep their own reasons (above).
        if (!wantsDrink && profile == null)
        {
            Job listed = NextWalkInRepair(out bool authored);
            if (listed != null) return listed;
            if (authored && hasDrinks && forcedKind != JobKind.Repair) wantsDrink = true;
        }

        if (wantsDrink)
        {
            DrinkDefinition d = forcedKind == JobKind.Drink ? openingDrink
                : profile != null && profile.preferredDrink != null
                ? profile.preferredDrink
                : drinks[Random.Range(0, drinks.Length)];
            return new Job
            {
                kind = JobKind.Drink,
                drink = d,
                deviceName = d.drinkName,
                payout = d.price
            };
        }

        GameObject device = null;

        if (profile != null && profile.preferredDevice != null
            && Random.value < profile.preferredDeviceChance)
        {
            device = profile.preferredDevice;
        }
        else
        {
            GameObject[] pool = today != null && today.devices != null && today.devices.Length > 0
                              ? today.devices : devicePrefabs;

            device = pool[Random.Range(0, pool.Length)];
        }

        Job job = new Job { kind = JobKind.Repair, devicePrefab = device };

        DeviceDefinition def = device.GetComponent<DeviceDefinition>();
        if (def != null)
        {
            job.deviceName = def.displayName;
            job.faultIndex = def.RandomFaultIndex();

            DeviceFault fault = def.GetFault(job.faultIndex);
            if (fault != null)
            {
                job.faultType = fault.type;
                job.faultDescription = fault.description;
                job.payout = fault.payout;
            }
        }

        return job;
    }

    /// <summary>How many of today's authored walk-in repairs have walked in so far (reports and the Days 1-3 checks).</summary>
    public int WalkInRepairsUsed => walkInRepairsUsed;

    /// <summary>
    /// 26 Oct 2026 (integration): an authored walk-in repair is still to come today and repairs' last orders are less than
    /// Authored Repair Due Within away, so the next walk-in brings it (RollJob): the day's taught repair always comes.
    /// </summary>
    private bool AuthoredRepairDue
    {
        get
        {
            if (today == null || DayClock.Instance == null || LastOrdersForRepairs) return false;
            FeaturedRepairRequest[] list = today.WalkInRepairsOn(lastSeenDay);
            if (list == null || walkInRepairsUsed >= list.Length) return false;
            return DayClock.Instance.TimeRemaining - RepairLastOrdersLead <= authoredRepairDueWithin;
        }
    }

    // Today's next authored walk-in repair (DayDefinition.walkInRepairs), or null: none are authored today (authored false),
    // or they are all used. An entry that can't make a job (no prefab, a fault index past the end) is skipped, loudly: the
    // Days 1-3 check (Fixit Fidget > Checks > Days 1-3) fails on it first.
    private Job NextWalkInRepair(out bool authored)
    {
        FeaturedRepairRequest[] list = today != null ? today.WalkInRepairsOn(lastSeenDay) : System.Array.Empty<FeaturedRepairRequest>();
        authored = list.Length > 0;
        while (walkInRepairsUsed < list.Length)
        {
            FeaturedRepairRequest entry = list[walkInRepairsUsed];
            walkInRepairsUsed++;
            string error = "the entry is empty";
            if (entry != null && entry.TryCreateJob(out Job job, out error)) return job;
            Debug.LogWarning($"{today.name}: walk-in repair {walkInRepairsUsed} of {list.Length} is invalid ({error}); skipped.", today);
        }
        return null;
    }
}
