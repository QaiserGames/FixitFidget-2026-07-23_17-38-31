#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DeviceScaffoldChecks
{
    [MenuItem("Fixit Fidget/Content/Check device scaffold rules")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Temporary fixtures are moved into a preview scene and closed together.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        int assertions = 0;
        try
        {
            GameObject root = new("ScaffoldCheck");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<RepairJob>();
            root.AddComponent<InspectableItem>();
            root.AddComponent<ItemInteractable>();
            root.AddComponent<BoxCollider>();
            DeviceDefinition definition = root.AddComponent<DeviceDefinition>();
            GameObject parent = new("Parent");
            parent.transform.SetParent(root.transform);
            GameObject task = new("Task");
            task.transform.SetParent(parent.transform);
            task.AddComponent<GrimeSpot>();
            GameObject outside = new("Outside");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(outside, scene);
            definition.faults = new[] {
                new DeviceFault { type = FaultType.Cleaning, enableObjects = new[] { task } },
                new DeviceFault { type = FaultType.Cleaning, enableObjects = new[] { task } }
            };
            Check(DeviceScaffoldValidation.Inspect(root).Errors == 0, "Shared task should validate", ref assertions);
            parent.SetActive(false);
            Check(DeviceScaffoldValidation.Inspect(root).Errors > 0, "Inactive parent must block the task", ref assertions);
            parent.SetActive(true);
            definition.faults[0].enableObjects = new GameObject[0];
            Check(DeviceScaffoldValidation.Inspect(root).Errors > 0, "Empty selected fault must not award free completion", ref assertions);
            definition.faults[0].enableObjects = new[] { outside };
            Check(DeviceScaffoldValidation.Inspect(root).Errors > 0, "Foreign object must be rejected", ref assertions);
            definition.faults[0].enableObjects = new[] { task };
            definition.faults[0].type = FaultType.Human;
            Check(DeviceScaffoldValidation.Inspect(root).Errors > 0, "Human needs a HumanFault", ref assertions);
            definition.faults[0].type = FaultType.Cleaning;
            root.GetComponent<BoxCollider>().enabled = false;
            Check(DeviceScaffoldValidation.Inspect(root).Errors > 0, "Missing pickup collider must be caught", ref assertions);
            root.GetComponent<BoxCollider>().enabled = true;

            GameObject[] nodes = { root, parent, task };
            for (int initial = 0; initial < 8; initial++)
            for (int controlledMask = 0; controlledMask < 8; controlledMask++)
            for (int selectedMask = 0; selectedMask < 8; selectedMask++)
            {
                HashSet<GameObject> controlled = new();
                HashSet<GameObject> selected = new();
                for (int i = 0; i < 3; i++)
                {
                    nodes[i].SetActive((initial & (1 << i)) != 0);
                    if ((controlledMask & (1 << i)) != 0) controlled.Add(nodes[i]);
                    if ((selectedMask & (1 << i)) != 0) selected.Add(nodes[i]);
                }
                bool predicted = DeviceScaffoldValidation.IsActiveForFault(task.transform, root.transform, controlled, selected);
                foreach (GameObject node in controlled) node.SetActive(selected.Contains(node));
                Check(predicted == task.activeInHierarchy, "Prediction disagrees with Unity hierarchy", ref assertions);
            }
            Debug.Log("[Device scaffold] " + assertions + " assertions passed. Preview scene closed; no assets edited.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Check(bool passed, string message, ref int assertions)
    {
        assertions++;
        if (!passed) throw new System.InvalidOperationException("[Device scaffold] " + message);
    }
}
#endif
