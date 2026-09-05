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

    private readonly DayOneOpening opening = new();
    private CustomerBrain openingCustomer;
    private DrinkDefinition openingDrink;
    public DayOneOpening.Step OpeningStep => opening.Current;
    public bool IsGuidedOpening => opening.IsActive;
    public CustomerBrain OpeningCustomer => openingCustomer;
    public float OpeningHintDuration => today != null
        ? Mathf.Clamp(today.openingHintDuration, 3f, 10f)
        : 6f;

    // A gap between arrivals, varied so the shop doesn't tick like a metronome.
    //
    // With a schedule, the base interval comes from wherever we are in the day
    // rather than being one number for the whole day. That single change is
    // what turns a flat drip into calm / build / rush / recover.
    private float NextGap
    {
        get
        {
            float baseInterval = spawnInterval;

            if (today != null && DayClock.Instance != null)
                baseInterval = today.IntervalAt(DayFraction) * escalation;

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
    private float DayFraction
    {
        get
        {
            DayClock c = DayClock.Instance;
            if (c == null) return 0f;

            float length = c.SecondsIntoDay + c.TimeRemaining;
            return length <= 0f ? 0f : Mathf.Clamp01(c.SecondsIntoDay / length);
        }
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
            featuredRegularSpawned = false;
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
            timer = Random.Range(openingGraceMin, openingGraceMax);

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

        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return;

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
            string phase = today.PhaseNameAt(DayFraction);
            if (phase != lastPhase)
            {
                lastPhase = phase;
                Debug.Log($"[Day {lastSeenDay}] phase: {phase} " +
                          $"(gaps ~{today.IntervalAt(DayFraction) * escalation:0.0}s)");
            }
        }

        int cap = today != null ? today.maxCustomers : maxCustomers;
        if (opening.IsActive) cap = 1;

        int living = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (living >= cap)
        {
            if (opening.IsActive) timer = blockedRetryInterval;
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
        if (counterQueue != null && !counterQueue.HasFreeSlot)
        {
            timer = blockedRetryInterval;
            return;
        }

        Spawn();
    }

    private void Spawn()
    {
        if (devicePrefabs == null || devicePrefabs.Length == 0) return;

        GameObject go = Instantiate(customerPrefab, ScatteredSpawn(), spawnPoint.rotation);

        CustomerBrain brain = go.GetComponent<CustomerBrain>();
        CustomerIdentity id = go.GetComponent<CustomerIdentity>();

        // Who is this? A featured regular takes the next available arrival
        // slot after their authored time. If the counter is full, they wait for
        // a real opening rather than spawning into nowhere.
        CustomerProfile profile = null;
        bool featuredDue = today != null
                        && today.featuredRegular != null
                        && !featuredRegularSpawned
                        && opening.AllowsFeatured(DayFraction, today.featuredRegularArrivesAt);

        if (featuredDue)
        {
            profile = today.featuredRegular;
            featuredRegularSpawned = true;
        }
        else if (!opening.IsActive)
        {
            float regChance = today != null ? today.regularChance : regularChance;
            bool isRegular = regulars != null
                          && regulars.Length > 0
                          && Random.value < regChance;

            if (isRegular)
                profile = regulars[Random.Range(0, regulars.Length)];
        }

        if (profile != null)
        {
            int relationship = SaveManager.Instance != null
                ? SaveManager.Instance.RelationshipFor(profile)
                : 0;
            bool hasMetBefore = SaveManager.Instance != null
                && SaveManager.Instance.HasMet(profile);
            id.SetupRegular(profile, relationship, hasMetBefore);
        }
        else
        {
            CustomerArchetype a = PickArchetype();
            id.SetupWalkIn(a, firstNames[Random.Range(0, firstNames.Length)]);
        }

        // What are they bringing, and what's wrong with it?
        JobKind? forcedKind = opening.IsActive
            ? (opening.Current == DayOneOpening.Step.Drink ? JobKind.Drink : JobKind.Repair)
            : null;
        Job job = RollJob(profile, forcedKind);

        // And would they like something while they wait? Rolled here, kept
        // quiet by CustomerBrain until they've sat down.
        brain.Init(counterQueue, exitPoint, job, RollDrinkWish(id, job));
        if (opening.TryStartVisit()) openingCustomer = brain;
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
}
