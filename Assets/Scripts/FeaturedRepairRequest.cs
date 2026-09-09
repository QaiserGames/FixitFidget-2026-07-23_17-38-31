using UnityEngine;

// A day owns its story request; the regular's preferences still govern random visits.
[System.Serializable]
public sealed class FeaturedRepairRequest
{
    public GameObject devicePrefab;
    [Min(0)] public int faultIndex;

    public bool TryCreateJob(out Job job, out string error)
    {
        job = null;
        if (devicePrefab == null)
        {
            error = "Choose a repair device prefab.";
            return false;
        }
        DeviceDefinition definition = devicePrefab.GetComponent<DeviceDefinition>();
        // GetFault clamps indices. Story authoring must fail visibly instead of
        // silently changing a reunion-camera request into another fault.
        if (definition == null || definition.faults == null || faultIndex < 0
            || faultIndex >= definition.faults.Length || definition.faults[faultIndex] == null)
        {
            error = "The selected device has no fault at that index.";
            return false;
        }
        if (devicePrefab.GetComponent<RepairJob>() == null)
        {
            error = "The device needs a RepairJob on its root.";
            return false;
        }
        DeviceFault fault = definition.faults[faultIndex];
        job = new Job
        {
            kind = JobKind.Repair,
            devicePrefab = devicePrefab,
            deviceName = definition.displayName,
            faultIndex = faultIndex,
            faultType = fault.type,
            faultDescription = fault.description,
            payout = fault.payout
        };
        error = "";
        return true;
    }
}
