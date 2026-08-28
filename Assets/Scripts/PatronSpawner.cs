using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// PATRON SPAWNER
//
// Separate from CustomerSpawner on purpose. That one is about pressure — how
// fast work arrives against how fast you can clear it, and every number in it
// was derived from logged play. This one is about the room being full, which
// is a different question with different answers, and mixing them would mean
// every future pacing change accidentally being an atmosphere change.
//
// THE ONE RULE THAT KEEPS THIS FROM RUINING THE GAME
//
// Patrons stop arriving when free seats run low. A cafe so packed that paying
// customers can never sit is atmosphere beating gameplay — the loitering
// pressure is meant to be a squeeze the player can feel and respond to, not a
// permanent condition they can do nothing about.
//
// See reserveSeatsForCustomers below. It is the whole safety valve.
// ---------------------------------------------------------------------------

public class PatronSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private GameObject patronPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;

    [Header("How many")]
    [Tooltip("Ceiling on patrons alive at once. occupancy-and-pacing.md sets " +
             "total occupancy at 20: ~6 customers plus ~14 patrons.")]
    [SerializeField] private int maxPatrons = 14;

    [Tooltip("Seconds between patron arrivals. Not tied to the customer " +
             "schedule — a cafe fills up on its own rhythm.")]
    [SerializeField] private float spawnInterval = 12f;

    [Range(0f, 1f)]
    [SerializeField] private float spawnJitter = 0.35f;

    [Header("⚠️ The safety valve")]
    [Tooltip("Seats always kept free for real customers. Patrons stop arriving " +
             "once free seats drop to this. Without it a full cafe means every " +
             "paying customer loiters at 1.15x forever, which is punishment " +
             "rather than pressure.")]
    [SerializeField] private int reserveSeatsForCustomers = 3;

    [Header("Money")]
    [Tooltip("What a patron pays on arrival. They bought a coffee at the " +
             "counter, conceptually — no interaction needed.\n\n" +
             "Small on purpose. At $5 a head, fourteen patrons is $70 against " +
             "a ~$700 day: enough that a full room feels good, never enough to " +
             "compete with repairs. The cafe's real payment is TIME, not money.")]
    [SerializeField] private int payPerPatron = 5;

    [Tooltip("Patrons do NOT consume beans or cups. A busy room sabotaging " +
             "your ability to serve the customers who matter would punish the " +
             "player for the cafe succeeding.")]
    [SerializeField] private bool consumesStock = false;

    [Header("Arrival")]
    [SerializeField] private float spawnScatter = 1.2f;
    [SerializeField] private float openingGraceMin = 3f;
    [SerializeField] private float openingGraceMax = 8f;

    private float timer;
    private int lastSeenDay = -1;

    private float NextGap =>
        spawnInterval * Random.Range(1f - spawnJitter, 1f + spawnJitter);

    private void Update()
    {
        if (patronPrefab == null || spawnPoint == null) return;

        DayClock clock = DayClock.Instance;

        // Last orders stops patrons too. The end of the day is supposed to be
        // "take care of everyone still here" — a stream of new faces arriving
        // after close would flatten that entirely.
        if (clock != null && !clock.IsOpen) return;

        if (clock != null && clock.Day != lastSeenDay)
        {
            lastSeenDay = clock.Day;
            timer = Random.Range(openingGraceMin, openingGraceMax);
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = NextGap;

        int living = FindObjectsByType<PatronBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (living >= maxPatrons) return;

        // The valve.
        if (WaitingArea.FreeSeats <= reserveSeatsForCustomers) return;

        Spawn();
    }

    private void Spawn()
    {
        GameObject go = Instantiate(patronPrefab, ScatteredSpawn(), spawnPoint.rotation);

        // A patron prefab made by duplicating the customer prefab would still
        // carry a CustomerBrain, which would claim a counter slot, appear on the
        // ticket rail, and keep DayClock waiting for it. Strip it rather than
        // relying on remembering to delete the component by hand.
        CustomerBrain stray = go.GetComponent<CustomerBrain>();
        if (stray != null)
        {
            Debug.LogWarning("[PatronSpawner] The patron prefab has a CustomerBrain " +
                             "on it. Removing it at runtime — but fix the prefab, " +
                             "or patrons will keep half-behaving like customers.", this);
            Destroy(stray);
        }

        CustomerInteractable talkable = go.GetComponent<CustomerInteractable>();
        if (talkable != null) Destroy(talkable);

        PatronBrain brain = go.GetComponent<PatronBrain>();
        if (brain == null) brain = go.AddComponent<PatronBrain>();

        brain.Init(exitPoint);

        if (payPerPatron > 0 && ShopEconomy.Instance != null)
            ShopEconomy.Instance.AddMoney(payPerPatron);

        if (consumesStock && ShopInventory.Instance != null)
            ShopInventory.Instance.TakeCup();
    }

    private Vector3 ScatteredSpawn()
    {
        if (spawnScatter <= 0f) return spawnPoint.position;

        Vector2 o = Random.insideUnitCircle * spawnScatter;
        Vector3 probe = spawnPoint.position + new Vector3(o.x, 0f, o.y);

        return NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas)
             ? hit.position
             : spawnPoint.position;
    }
}
