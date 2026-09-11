#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ServicePlaytestSetup
{
    public const string ScenePath = "Assets/Playtests/ServiceInteractionPlaytest.unity";

    [MenuItem("Fixit Fidget/Playtest/Open service interaction playtest")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before opening the playtest scene.");
        var active = SceneManager.GetActiveScene();
        if (active.isDirty)
            throw new InvalidOperationException("Save your current scene changes before opening the playtest scene.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            ConfigureIsolation(EditorSceneManager.OpenScene(ScenePath));
            return;
        }
        if (UnityEngine.Object.FindAnyObjectByType<BeverageStation>() == null)
            throw new InvalidOperationException("Install the updated dispenser in the shop scene first.");
        if (!AssetDatabase.IsValidFolder("Assets/Playtests")) AssetDatabase.CreateFolder("Assets", "Playtests");
        if (!EditorSceneManager.SaveScene(active, ScenePath, true))
            throw new InvalidOperationException("Could not create the playtest scene copy.");
        ConfigureIsolation(EditorSceneManager.OpenScene(ScenePath));
    }

    private static void ConfigureIsolation(Scene scene)
    {
        var save = UnityEngine.Object.FindAnyObjectByType<SaveManager>();
        if (save == null) throw new InvalidOperationException("Playtest scene needs the existing SaveManager.");
        var serializedSave = new SerializedObject(save);
        serializedSave.FindProperty("useInteractionPlaytestSave").boolValue = true;
        serializedSave.ApplyModifiedPropertiesWithoutUndo();
        var log = UnityEngine.Object.FindAnyObjectByType<DayLog>();
        if (log != null)
        {
            var serializedLog = new SerializedObject(log);
            serializedLog.FindProperty("folderName").stringValue = "DayLogs/InteractionPlaytest";
            serializedLog.ApplyModifiedPropertiesWithoutUndo();
        }
        var clock = UnityEngine.Object.FindAnyObjectByType<DayClock>();
        if (clock != null)
        {
            var serializedClock = new SerializedObject(clock);
            serializedClock.FindProperty("startingDay").intValue = 1;
            serializedClock.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the isolated playtest configuration. Do not enter Play Mode.");
        Debug.Log("[Service playtest] Ready. Press Play to start or resume the isolated playtest: carrying and dispenser checks, then Grace's two visits. "
            + "This scene uses interaction-playtest.json and separate day logs. Your campaign save is unchanged.");
    }
}
#endif
