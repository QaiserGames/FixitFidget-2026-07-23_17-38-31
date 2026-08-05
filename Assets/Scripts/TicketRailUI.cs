using System.Collections.Generic;
using UnityEngine;

public class TicketRailUI : MonoBehaviour
{
    [SerializeField] private JobTicket ticketPrefab;
    [SerializeField] private Transform rail;

    private readonly Dictionary<CustomerBrain, JobTicket> tickets = new();

    private void Update()
    {
        CustomerBrain[] all = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        // Add tickets for anyone whose job we've accepted.
        foreach (CustomerBrain b in all)
        {
            if (!b.InService || !b.HasJob) continue;
            if (tickets.ContainsKey(b)) continue;

            JobTicket t = Instantiate(ticketPrefab, rail);
            t.Bind(b);
            tickets.Add(b, t);
        }

        // Remove tickets whose customer is gone or done.
        List<CustomerBrain> stale = new();
        foreach (var kv in tickets)
            if (kv.Key == null || !kv.Key.InService || !kv.Key.HasJob) stale.Add(kv.Key);

        foreach (CustomerBrain b in stale)
        {
            if (tickets[b] != null) Destroy(tickets[b].gameObject);
            tickets.Remove(b);
        }
    }
}