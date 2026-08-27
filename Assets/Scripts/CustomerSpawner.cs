using UnityEngine;
using UnityEngine.AI;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private CounterQueue counterQueue;

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

    // -1 so the very first Update of the very first day counts as "the day
    // changed" and gets its opening grace like every other morning.
    private int lastSeenDay = -1;

    // A gap between arrivals, varied so the shop doesn't tick like a metronome.
    private float NextGap =>
        spawnInterval * Random.Range(1f - spawnJitter, 1f + spawnJitter);

    private void Update()
    {
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return;

        // A new morning. Opening the shop shouldn't mean someone is already at
        // the counter — there's a beat where the room is yours, and then the
        // first person wanders in.
        //
        // Watched here rather than driven by an event on DayClock: this is the
        // only system that cares, so the knowledge stays where it's used.
        if (DayClock.Instance != null && DayClock.Instance.Day != lastSeenDay)
        {
            lastSeenDay = DayClock.Instance.Day;
            timer = Random.Range(openingGraceMin, openingGraceMax);
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = NextGap;

        int living = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (living >= maxCustomers) return;

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

        // Who is this?
        CustomerProfile profile = null;
        bool isRegular = regulars != null && regulars.Length > 0 && Random.value < regularChance;

        if (isRegular)
        {
            profile = regulars[Random.Range(0, regulars.Length)];
            id.SetupRegular(profile);
        }
        else
        {
            CustomerArchetype a = archetypes != null && archetypes.Length > 0
                ? archetypes[Random.Range(0, archetypes.Length)] : null;
            id.SetupWalkIn(a, firstNames[Random.Range(0, firstNames.Length)]);
        }

        // What are they bringing, and what's wrong with it?
        Job job = RollJob(profile);

        // And would they like something while they wait? Rolled here, kept
        // quiet by CustomerBrain until they've sat down.
        brain.Init(counterQueue, exitPoint, job, RollDrinkWish(id, job));
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
        if (drinks == null || drinks.Length == 0) return null;

        float chance = id != null ? id.DrinkWishChance : 0f;
        if (Random.value >= chance) return null;

        return drinks[Random.Range(0, drinks.Length)];
    }

    private Job RollJob(CustomerProfile profile)
    {
        // Café or repair?
        bool wantsDrink = drinks != null && drinks.Length > 0 && Random.value < drinkChance;

        if (wantsDrink)
        {
            DrinkDefinition d = drinks[Random.Range(0, drinks.Length)];
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
            device = devicePrefabs[Random.Range(0, devicePrefabs.Length)];
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