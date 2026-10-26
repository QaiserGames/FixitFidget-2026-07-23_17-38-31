using UnityEngine;

// A day owns its story request; the regular's preferences still govern random visits.
[System.Serializable]
public sealed class FeaturedRepairRequest
{
    public GameObject devicePrefab;
    [Min(0)] public int faultIndex;
    [Tooltip("Optional exact story ID. Must match the device's authored episode marker.")]
    public string storyEpisodeId = "";

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
        // 24 Oct 2026 (Days): a support call (PhoneJob.prefab: a HoldCallJob, no RepairJob) can be a day's walk-in too (his
        // answer p4: "the support call on Day 3, once it has its real phone model"). Everything else still needs a RepairJob.
        if (devicePrefab.GetComponent<RepairJob>() == null && devicePrefab.GetComponent<HoldCallJob>() == null)
        {
            error = "The device needs a RepairJob (or a support call's HoldCallJob) on its root.";
            return false;
        }
        DeviceFault fault = definition.faults[faultIndex];
        if (!string.IsNullOrEmpty(storyEpisodeId)
            && !string.Equals(storyEpisodeId, definition.storyEpisodeId, System.StringComparison.Ordinal))
        {
            error = "The featured episode ID does not match this device.";
            return false;
        }
        job = new Job
        {
            kind = JobKind.Repair,
            devicePrefab = devicePrefab,
            deviceName = definition.displayName,
            faultIndex = faultIndex,
            faultType = fault.type,
            faultDescription = fault.description,
            storyEpisodeId = storyEpisodeId ?? "",
            payout = fault.payout
        };
        error = "";
        return true;
    }
}
