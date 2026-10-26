#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE START SCREEN'S STEP, TOGGLE AND LAB (26 Oct 2026; review24 spec S)
//
//   Fixit Fidget › Start screen › Start screen 1 - Put the start screen in the café scene (scene, with Undo)
//     Adds one root object, "Start screen", with TitleScreen on it (the title builds everything else in code while playing,
//     and never saves any of it). Idempotent: a second run finds it and changes nothing. One Undo group (Ctrl+Z takes it
//     out), or the Undo menu below. Report: Logs/StartScreen/start-screen-1-<time>.txt. Save the scene after: the start
//     screen leaves by reloading the scene from disk, so an unsaved scene would come back without today's changes.
//   Fixit Fidget › Start screen › Start screen 1 - Undo (take it out of the scene)
//   Fixit Fidget › Start screen › Show it in the editor's Play (on/off)
//     The dev toggle (EditorPrefs, on by default). Off: the editor's own Play goes straight into the day as before. A build
//     always shows it. Labs never show it either way (TitleScreen.ShouldShow).
//   Fixit Fidget › Start screen › Lab - The start screen (drives itself)
//     StartScreenLab, in the lab's own save (a Day 3 lab save with 2 stars; his playtest save is not used): the diorama,
//     Continue, then the cold open with no save, the rewind and Day 1. About a minute; hands off. Logs/StartScreen/check-<time>/.
// ---------------------------------------------------------------------------
static class StartScreenSteps
{
    const string MenuRoot = "Fixit Fidget/Start screen/";
    const string StepMenu = MenuRoot + "Start screen 1 - Put the start screen in the café scene (scene, with Undo)";
    const string UndoMenu = MenuRoot + "Start screen 1 - Undo (take it out of the scene)";
    const string ToggleMenu = MenuRoot + "Show it in the editor's Play (on/off)";
    const string LabMenu = MenuRoot + "Lab - The start screen (drives itself)";
    const string Tag = "[Start screen] ";
    const string Title = "Start screen 1 - Put the start screen in the café scene";
    public const string ObjectName = "Start screen";

    static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "StartScreen");

    // ---------- Start screen 1 ----------

    [MenuItem(StepMenu, false, 100)]
    static void PutItIn()
    {
        var log = new StringBuilder();
        log.AppendLine(Title);
        log.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first. Nothing was changed.");
            return;
        }
        TitleScreen[] there = UnityEngine.Object.FindObjectsByType<TitleScreen>(FindObjectsInactive.Include);
        bool bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>(FindObjectsInactive.Include) != null;
        bool save = UnityEngine.Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include) != null;
        log.AppendLine($"Found: {there.Length} TitleScreen; GameBootstrap {(bootstrap ? "yes" : "NO")}; SaveManager {(save ? "yes" : "NO")}.");
        string verdict;
        if (there.Length > 1)
        {
            verdict = $"FAILED: {there.Length} TitleScreen components in the scene; keep one (Undo the extra) and run it again.";
        }
        else if (there.Length == 1)
        {
            verdict = "DONE. " + Title + ": it is already in the scene (" + PathOf(there[0].transform) + "); nothing changed.";
        }
        else
        {
            Undo.IncrementCurrentGroup();
            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, Title);
            Undo.AddComponent<TitleScreen>(go);
            Undo.SetCurrentGroupName(Title);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"Added: \"{ObjectName}\" at the scene's root with TitleScreen (nothing else; the UI and camera are made while playing).");
            int now = UnityEngine.Object.FindObjectsByType<TitleScreen>(FindObjectsInactive.Include).Length;
            verdict = now == 1 ? "DONE. " + Title + ". Save the scene (the start screen reloads it from disk when it leaves)."
                               : $"FAILED: {now} TitleScreen components after the step.";
        }
        if (!bootstrap || !save) verdict += " Warning: the café's GameBootstrap or SaveManager is missing, so the title can't tell a save from none.";
        log.AppendLine(verdict);
        Write("start-screen-1", log.ToString());
        if (verdict.StartsWith("DONE", StringComparison.Ordinal)) Debug.Log(Tag + verdict);
        else Debug.LogError(Tag + verdict);
    }

    [MenuItem(UndoMenu, false, 101)]
    static void TakeItOut()
    {
        TitleScreen[] there = UnityEngine.Object.FindObjectsByType<TitleScreen>(FindObjectsInactive.Include);
        if (there.Length == 0) { Debug.Log(Tag + "UNDONE: there was no start screen in the scene; nothing changed."); return; }
        Undo.IncrementCurrentGroup();
        foreach (TitleScreen t in there)
        {
            GameObject go = t.gameObject;
            // Only the object the step made goes whole; a TitleScreen put anywhere else loses just the component.
            if (go.name == ObjectName && go.transform.parent == null && go.GetComponents<Component>().Length == 2) Undo.DestroyObjectImmediate(go);
            else Undo.DestroyObjectImmediate(t);
        }
        Undo.SetCurrentGroupName("Start screen 1 - Undo");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(Tag + $"UNDONE: the start screen is out of the scene ({there.Length}). Save the scene.");
    }

    [MenuItem(StepMenu, true)]
    [MenuItem(UndoMenu, true)]
    [MenuItem(LabMenu, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ---------- the dev toggle ----------

    [MenuItem(ToggleMenu, false, 120)]
    static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(TitleScreen.EditorToggleKey, true);
        EditorPrefs.SetBool(TitleScreen.EditorToggleKey, on);
        UnityEditor.Menu.SetChecked(ToggleMenu, on);
        Debug.Log(Tag + (on ? "On: the editor's Play opens on the start screen (labs never do)." : "Off: the editor's Play goes straight into the day. A build still shows it."));
    }

    [MenuItem(ToggleMenu, true)]
    static bool ToggleState()
    {
        UnityEditor.Menu.SetChecked(ToggleMenu, EditorPrefs.GetBool(TitleScreen.EditorToggleKey, true));
        return true;
    }

    // ---------- the lab ----------

    [MenuItem(LabMenu, false, 140)]
    static void Lab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        if (UnityEngine.Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include) == null)
        {
            Debug.LogError(Tag + "Run Start screen 1 first (and save the scene).");
            return;
        }
        if (scene.isDirty)
        {
            Debug.LogError(Tag + "Save the scene first: the start screen reloads it from disk, and the lab checks what comes back.");
            return;
        }
        // The lab's own save: Day 3's morning, 2 stars ("Known on the street"), the café as today (no layout: format 0).
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        var data = new SaveData
        {
            day = StartScreenLab.LabDay,
            money = StartScreenLab.LabMoney,
            reputation = StartScreenLab.LabReputation,
            starsEarned = StartScreenLab.LabStars,
        };
        SaveCheckpointStorage.Write(path, data);
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(TitleScreen.LabKey, 1);
        PlayerPrefs.SetInt(StartScreenLab.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "The start screen's lab: a lab session that drives itself (about a minute). Keep the Game view in front and leave the " +
                  $"mouse, keyboard and pad alone until it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A request that never became a Play session must not ride along with the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string key in new[] { TitleScreen.LabKey, TitleScreen.SkipOnceKey, StartScreenLab.PendingKey })
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }

    // ---------- helpers ----------

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    static void Write(string label, string text)
    {
        try
        {
            Directory.CreateDirectory(LogRoot);
            File.WriteAllText(Path.Combine(LogRoot, $"{label}-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Couldn't write the report: " + e.Message); }
    }
}
#endif
