#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FeaturedRepairChecks
{
    [MenuItem("Fixit Fidget/Checks/Featured repair requests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running these checks.");
        var scene = EditorSceneManager.NewPreviewScene();
        DayDefinition day = ScriptableObject.CreateInstance<DayDefinition>();
        try
        {
            Require(!day.useFeaturedRepair, "Existing days retain random jobs by default.");
            var request = new FeaturedRepairRequest();
            Require(!request.TryCreateJob(out Job job, out string error) && job == null && error.Length > 0,
                "Missing device fails explicitly.");
            GameObject device = new GameObject("Featured repair check");
            SceneManager.MoveGameObjectToScene(device, scene);
            request.devicePrefab = device;
            Require(!request.TryCreateJob(out _, out _), "Missing definition fails.");
            var definition = device.AddComponent<DeviceDefinition>();
            definition.displayName = "Test camera";
            definition.faults = new[]
            {
                new DeviceFault { type = FaultType.Cleaning, description = "Dusty lens", payout = 37 },
                new DeviceFault { type = FaultType.Mechanical, description = "Shutter", payout = 51 }
            };
            Require(!request.TryCreateJob(out _, out _), "Missing bench job fails.");
            device.AddComponent<RepairJob>();
            for (int index = 0; index < 2; index++)
            {
                request.faultIndex = index;
                Require(request.TryCreateJob(out job, out error), "Valid choice resolves.");
                Require(job.devicePrefab == device && job.kind == JobKind.Repair
                    && job.deviceName == "Test camera" && job.faultIndex == index
                    && job.faultType == definition.faults[index].type
                    && job.faultDescription == definition.faults[index].description
                    && job.payout == definition.faults[index].payout, "Ticket matches the exact authored fault.");
                request.TryCreateJob(out Job other, out _);
                Require(!ReferenceEquals(job, other), "Each visitor receives an independent record.");
            }
            foreach (int invalid in new[] { -1, 2, int.MaxValue })
            {
                request.faultIndex = invalid;
                Require(!request.TryCreateJob(out job, out _) && job == null, "Invalid indices never clamp to another fault.");
            }
            request.faultIndex = 0;
            definition.faults[0] = null;
            Require(!request.TryCreateJob(out _, out _), "Deleted fault fails.");
            definition.faults = null;
            Require(!request.TryCreateJob(out _, out _), "Deleted fault list fails.");
            Debug.Log("[Featured repair] PASS: defaults, exact selection, independent records and invalid authoring.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(day);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
