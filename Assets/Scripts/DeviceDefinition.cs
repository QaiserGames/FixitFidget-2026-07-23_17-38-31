using UnityEngine;

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

        for (int i = 0; i < faults.Length; i++)
        {
            bool on = (i == index);
            if (faults[i].enableObjects == null) continue;

            foreach (GameObject g in faults[i].enableObjects)
                if (g != null) g.SetActive(on);
        }
    }
}