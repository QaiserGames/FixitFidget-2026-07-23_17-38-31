using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [SerializeField] private CustomerBrain customerPrefab;
    [SerializeField] private CounterQueue counterQueue;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnEvery = 10f;
    [SerializeField] private int maxCustomers = 4;

    [Header("Personalities")]
    [SerializeField] private CustomerArchetype[] archetypes;

    private float timer;

    private void Update()
    {
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return;
        timer += Time.deltaTime;
        if (timer >= spawnEvery && CountCustomers() < maxCustomers)
        {
            timer = 0f;
            var customer = Instantiate(customerPrefab, transform.position, transform.rotation);
            customer.Init(counterQueue, exitPoint, PickArchetype());
        }
    }

    private CustomerArchetype PickArchetype()
    {
        if (archetypes == null || archetypes.Length == 0) return null;
        return archetypes[Random.Range(0, archetypes.Length)];
    }

    private int CountCustomers()
    {
        return FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
    }
}