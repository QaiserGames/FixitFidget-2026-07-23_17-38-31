using UnityEngine;

// The master record of a transaction: who, what device, what's wrong.
// Created when a customer spawns. The physical item is spawned later, on accept.
public class Job
{
    public GameObject devicePrefab;
    public string deviceName = "thing";

    public int faultIndex;
    public FaultType faultType;
    public string faultDescription = "broken";

    public int payout = 25;

    // Assigned on accept, for the ticket / item marker.
    public int number;
    public Color color = Color.white;
}