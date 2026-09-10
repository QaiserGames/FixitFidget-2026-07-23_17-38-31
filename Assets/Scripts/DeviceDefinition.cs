using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DeviceFault
{
    public FaultType type = FaultType.Mechanical;

    [Tooltip("Plain language, used on the ticket and in dialogue. e.g. 'cracked screen'")]
    public string description = "broken";

    [Tooltip("Fault components to switch ON when this fault is chosen. Everything listed on OTHER faults gets switched off.")]
    public GameObject[] enableObjects;

    public int payout = 25;
}

public class DeviceDefinition : MonoBehaviour
{
    [Tooltip("Used in dialogue via the {device} token. e.g. 'pocket watch'")]
    public string displayName = "thing";

    [Tooltip("Stable episode marker for an explicitly featured request. Does not enroll random repairs in a story.")]
    public string storyEpisodeId = "";

    public DeviceFault[] faults;

    public DeviceFault GetFault(int index)
    {
        if (faults == null || faults.Length == 0) return null;
        return faults[Mathf.Clamp(index, 0, faults.Length - 1)];
    }

    public int RandomFaultIndex()
    {
        if (faults == null || faults.Length == 0) return 0;
        return Random.Range(0, faults.Length);
    }

    // Switch on this fault's parts, switch off every other fault's parts.
    public void ApplyFault(int index)
    {
        if (faults == null || faults.Length == 0) return;

        index = Mathf.Clamp(index, 0, faults.Length - 1);
        var all = new HashSet<GameObject>();
        var selected = new HashSet<GameObject>();
        for (int i = 0; i < faults.Length; i++)
        {
            if (faults[i] == null || faults[i].enableObjects == null) continue;
            foreach (GameObject g in faults[i].enableObjects)
            {
                if (g == null) continue;
                all.Add(g);
                if (i == index) selected.Add(g);
            }
        }
        // Shared objects must stay on if the chosen fault needs them, regardless
        // of where the other fault appears in the list.
        foreach (GameObject g in all) g.SetActive(selected.Contains(g));
        RepairJob repair = GetComponent<RepairJob>();
        if (repair != null) repair.CaptureTasks();
    }
}
