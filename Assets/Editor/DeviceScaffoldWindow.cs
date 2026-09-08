#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates an independent prefab; never edits a template or enrolls a device in a day.
public sealed class DeviceScaffoldWindow : EditorWindow
{
    private GameObject template;
    private string deviceName = "New device";
    private int playtestFault;
    private Vector2 scroll;
    private string report = "Choose a bench repair prefab, then inspect its wiring.";

    [MenuItem("Fixit Fidget/Content/Device scaffold")]
    private static void Open() => GetWindow<DeviceScaffoldWindow>("Device scaffold");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Copy a working bench device as a starting point. Models, fault descriptions and payouts are inherited and need review before scheduling customers.", MessageType.Info);
        template = (GameObject)EditorGUILayout.ObjectField("Template prefab", template, typeof(GameObject), false);
        deviceName = EditorGUILayout.TextField("Device display name", deviceName);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Inspect template")) Inspect();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(deviceName)))
                if (GUILayout.Button("Create separate prefab…")) Create();
        }
        DrawPlaytest();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel,
            GUILayout.MinHeight(250), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void DrawPlaytest()
    {
        DeviceDefinition definition = template != null ? template.GetComponent<DeviceDefinition>() : null;
        if (definition?.faults == null || definition.faults.Length == 0) return;
        playtestFault = Mathf.Clamp(playtestFault, 0, definition.faults.Length - 1);
        string[] choices = new string[definition.faults.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            DeviceFault fault = definition.faults[i];
            choices[i] = fault == null ? i + " · Missing fault" : i + " · " + fault.type + " · " + fault.description;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Playtest copied prefab", EditorStyles.boldLabel);
        playtestFault = EditorGUILayout.Popup("Fault", playtestFault, choices);
        EditorGUILayout.HelpBox("During an open day, spawn a zero-base-payout practice customer through the normal queue. This affects the run's logs/recap and CAN enter the day-end save. Stop Play Mode before the day closes to retain your previous checkpoint.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            if (GUILayout.Button("Spawn practice customer")) SpawnPractice();
    }

    private void SpawnPractice()
    {
        if (!Inspect()) return;
        try
        {
            HumanIntegrationChecks.SpawnPractice(AssetDatabase.GetAssetPath(template), playtestFault);
            report += "\n\nPractice customer spawned for fault " + playtestFault
                + ". Stop Play Mode BEFORE the day closes if you do not want this test saved.";
        }
        catch (System.InvalidOperationException ex) { report += "\n\nNot spawned: " + ex.Message; }
    }

    private bool Inspect()
    {
        string path = AssetDatabase.GetAssetPath(template);
        if (template == null || !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
            || AssetDatabase.LoadAssetAtPath<GameObject>(path) != template)
        {
            report = "Choose the root of a .prefab asset from the Project window (not a model or a child object).";
            return false;
        }
        // Inspect the asset without instantiating it or running repair initialization.
        DeviceScaffoldValidation.Result result = DeviceScaffoldValidation.Inspect(template);
        report = path + "\n\n" + result;
        return result.Errors == 0;
    }

    private void Create()
    {
        if (!Inspect()) return;
        string target = EditorUtility.SaveFilePanelInProject("Create device scaffold",
            "NewDevice", "prefab", "Choose a NEW prefab path. Existing assets cannot be overwritten.");
        if (string.IsNullOrEmpty(target)) return;
        if (!target.StartsWith("Assets/", System.StringComparison.Ordinal)
            || File.Exists(target) || File.Exists(target + ".meta")
            || AssetDatabase.LoadMainAssetAtPath(target) != null)
        {
            report += "\n\nNot created: choose an unused path inside Assets.";
            return;
        }

        bool ownsCopy = false;
        GameObject contents = null;
        try
        {
            // Copy the WHOLE prefab so Unity remaps its internal object references.
            // Shared materials/configuration remain shared; no per-part borrowing.
            ownsCopy = AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), target);
            if (!ownsCopy) throw new System.InvalidOperationException("Unity could not copy the template.");
            contents = PrefabUtility.LoadPrefabContents(target);
            // Detach a copied variant from its base before editing it. Nested visual
            // prefabs can stay connected; the new device root is independent.
            if (PrefabUtility.IsPartOfPrefabInstance(contents))
                PrefabUtility.UnpackPrefabInstance(contents, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            contents.name = Path.GetFileNameWithoutExtension(target);
            contents.GetComponent<DeviceDefinition>().displayName = deviceName.Trim();
            DeviceScaffoldValidation.Result result = DeviceScaffoldValidation.Inspect(contents);
            if (result.Errors != 0) throw new System.InvalidOperationException(result.ToString());
            PrefabUtility.SaveAsPrefabAsset(contents, target, out bool saved);
            if (!saved) throw new System.InvalidOperationException("Unity could not save the new prefab.");
            PrefabUtility.UnloadPrefabContents(contents);
            contents = null;
            result = DeviceScaffoldValidation.Inspect(AssetDatabase.LoadAssetAtPath<GameObject>(target));
            if (result.Errors != 0) throw new System.InvalidOperationException(result.ToString());
            report = "Created " + target + "\n\n" + result
                + "\nNext: open the new prefab, author its model and fault descriptions, then bench-test every fault before adding arrivals.";
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        catch (System.Exception ex)
        {
            if (contents != null) { PrefabUtility.UnloadPrefabContents(contents); contents = null; }
            // Roll back only the new copy this operation owns.
            bool removed = !ownsCopy || AssetDatabase.DeleteAsset(target);
            report = "Creation failed: " + ex.Message
                + (removed ? "\nNo new prefab was retained." : "\nCleanup failed; inspect the new asset at " + target);
            Debug.LogError("[Device scaffold] " + report);
        }
        finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
    }
}

public static class DeviceScaffoldValidation
{
    public sealed class Result
    {
        public int Errors { get; private set; }
        private readonly List<string> lines = new();
        public void Error(string text) { Errors++; lines.Add("ERROR: " + text); }
        public void Note(string text) => lines.Add(text);
        public override string ToString() => Errors + " wiring error(s)\n" + string.Join("\n", lines);
    }

    public static Result Inspect(GameObject root)
    {
        Result result = new();
        if (root == null) { result.Error("Missing prefab root."); return result; }
        DeviceDefinition def = root.GetComponent<DeviceDefinition>();
        if (def == null) { result.Error("DeviceDefinition must be on the root."); return result; }
        if (!def.enabled) result.Error("DeviceDefinition is disabled.");
        if (root.GetComponent<RepairJob>() == null || root.GetComponentsInChildren<JobBase>(true).Length != 1)
            result.Error("A bench template needs exactly one JobBase, a RepairJob on its root. Support-call and drink prefabs use different workflows.");
        else if (!root.GetComponent<RepairJob>().enabled) result.Error("RepairJob is disabled.");
        if (root.GetComponent<InspectableItem>() == null) result.Error("Missing root InspectableItem.");
        else if (!root.GetComponent<InspectableItem>().enabled) result.Error("InspectableItem is disabled.");
        if (root.GetComponent<ItemInteractable>() == null) result.Error("Missing root ItemInteractable.");
        else if (!root.GetComponent<ItemInteractable>().enabled) result.Error("ItemInteractable is disabled.");
        if (!root.activeSelf) result.Error("The device root is inactive.");
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                result.Error(child.name + " has a missing script.");
        CheckReferences(root, result);
        if (def.faults == null || def.faults.Length == 0)
        { result.Error("No faults configured."); return result; }

        HashSet<GameObject> controlled = new();
        foreach (DeviceFault fault in def.faults)
            if (fault?.enableObjects != null)
                foreach (GameObject obj in fault.enableObjects)
                    if (obj != null)
                    {
                        controlled.Add(obj);
                        if (obj == root) result.Error("A fault must not switch the device root off.");
                    }

        for (int i = 0; i < def.faults.Length; i++)
        {
            DeviceFault fault = def.faults[i];
            string prefix = "Fault " + i + ": ";
            if (fault == null) { result.Error(prefix + "empty entry."); continue; }
            HashSet<GameObject> selected = new();
            if (fault.enableObjects != null)
                foreach (GameObject obj in fault.enableObjects)
                    if (obj == null) result.Error(prefix + "missing enableObjects reference.");
                    else selected.Add(obj);
            foreach (GameObject obj in selected)
                if (!IsActiveForFault(obj.transform, root.transform, controlled, selected))
                    result.Error(prefix + obj.name + " cannot become active; inspect its parent chain.");

            int tasks = 0;
            int human = 0;
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || !IsActiveForFault(component.transform, root.transform, controlled, selected)) continue;
                if (component is GrimeSpot || component is ReplaceablePart || component is CircuitPuzzle || component is HumanFault)
                {
                    tasks++;
                    if (!component.enabled) result.Error(prefix + component.name + " has a disabled task component.");
                    if (component is HumanFault) human++;
                }
            }
            if (tasks == 0) result.Error(prefix + "no active scored task components (can produce a free Perfect).");
            if (fault.type == FaultType.Human && human == 0) result.Error(prefix + "Human fault needs an active HumanFault.");
            if (fault.type == FaultType.Bureaucratic) result.Error(prefix + "use the support-call workflow, not a bench scaffold.");
            bool pickup = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && IsActiveForFault(collider.transform, root.transform, controlled, selected))
                    pickup = true;
            if (!pickup) result.Error(prefix + "no active collider is available for pickup.");
            result.Note(prefix + fault.type + " / " + fault.description + " / " + tasks + " task component(s) / $" + fault.payout);
        }
        result.Note("Component counts are not grade credits: circuits and Human tasks own their scoring. No repair initialization was run.");
        result.Note("Review inherited descriptions, payout, visuals and collider reachability in Play Mode. Structural checks cannot prove the job is fun or reachable from the camera.");
        return result;
    }

    // Predict ApplyFault without changing activeSelf, calling Awake, or generating a puzzle.
    public static bool IsActiveForFault(Transform node, Transform root,
        HashSet<GameObject> controlled, HashSet<GameObject> selected)
    {
        if (node != root && !node.IsChildOf(root)) return false;
        for (Transform current = node; current != null; current = current.parent)
        {
            bool active = controlled.Contains(current.gameObject) ? selected.Contains(current.gameObject) : current.gameObject.activeSelf;
            if (!active) return false;
            if (current == root) return true;
        }
        return false;
    }

    private static void CheckReferences(GameObject root, Result result)
    {
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            using SerializedObject serialized = new(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                UnityEngine.Object reference = property.objectReferenceValue;
                Transform target = reference is GameObject go ? go.transform : (reference as Component)?.transform;
                // Materials, textures and authored ScriptableObject profiles are shared intentionally.
                if (target != null && target != root.transform && !target.IsChildOf(root.transform))
                    result.Error(component.name + "." + property.propertyPath + " references an object outside this prefab.");
            }
        }
    }
}
#endif
