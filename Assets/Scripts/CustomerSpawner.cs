using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private CounterQueue counterQueue;

    [Header("Pacing")]
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private int maxCustomers = 3;

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
    [Tooltip("Chance a customer wants a drink rather than a repair.")]
    [SerializeField] private float drinkChance = 0.4f;

    private float timer;

    private void Update()
    {
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = spawnInterval;

        int living = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (living >= maxCustomers) return;

        Spawn();
    }

    private void Spawn()
    {
        if (devicePrefabs == null || devicePrefabs.Length == 0) return;

        GameObject go = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);

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
        brain.Init(counterQueue, exitPoint, job);
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