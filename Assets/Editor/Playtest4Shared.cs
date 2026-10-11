using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4 FIXES: what every fix step shares (10 Oct 2026, after the playtester agents' first run:
// Docs/Studio/playtests/*-2026-10-10.md, claude/playtest-4-notes.md).
//
// A fix step (menu Fixit Fidget › Playtest 4 › …):
//   1. Edit Mode only, the café scene open and saved (so the step's changes are its own);
//   2. the survey finds everything it touches and refuses with the reason (run.Require), changing nothing;
//   3. one undo group holds the whole change (record every object with run.Record before changing it,
//      or use the Set… helpers, which record and report before → after);
//   4. the checks (run.Check) say in numbers that the change is what it meant; ANY failed check undoes
//      the whole change (scene and assets) before anything is saved;
//   5. assets the step changed (a ScriptableObject: run.SaveAsset) are saved only after the checks pass,
//      so Git shows them; prefabs go through EditPrefab, which saves only when its own checks pass;
//   6. the scene is NEVER saved here: Claude reads the report (Logs/Playtest4/<label>-<time>.txt and
//      <label>-last.txt), looks at the scene, and saves it himself;
//   7. nothing in the scene is ever deleted: switch it off (run.SwitchOff) and say so.
// A read-only "0 - Check first" item runs only the survey (Survey(...)).
// ---------------------------------------------------------------------------
public static class Playtest4Shared
{
    public const string Menu = "Fixit Fidget/Playtest 4/";
    public const string ScenePath = "Assets/Playtests/AcesCafeLayoutPlaytest.unity";

    public static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Playtest4");

    /// <summary>Thrown by Require: the step changes nothing and says why.</summary>
    public sealed class Refused : Exception
    {
        public Refused(string why) : base(why) { }
    }

    public sealed class Run
    {
        public readonly string Title, Label;
        public readonly StringBuilder Text = new StringBuilder();
        public int Passes, Failures, Changes;
        internal readonly List<Object> assetsToSave = new List<Object>();
        internal readonly List<Action> afterPass = new List<Action>();
        internal readonly List<Action> onFail = new List<Action>();

        public Run(string title, string label)
        {
            Title = title;
            Label = label;
        }

        /// <summary>Refuse (nothing changes) unless ok.</summary>
        public void Require(bool ok, string why)
        {
            if (!ok) throw new Refused(why);
        }

        /// <summary>A check in the report. Any failure undoes the whole step.</summary>
        public bool Check(bool ok, string what)
        {
            if (ok) Passes++;
            else Failures++;
            Text.AppendLine((ok ? "  PASS   " : "  FAIL   ") + what);
            return ok;
        }

        public void Info(string line) => Text.AppendLine("  " + line);

        public void Section(string name) => Text.AppendLine().AppendLine(name);

        /// <summary>A change, as one report line ("what: before → after").</summary>
        public void Changed(string what)
        {
            Changes++;
            Text.AppendLine("  CHANGE " + what);
        }

        /// <summary>Record an object (scene object, component or asset) in the step's undo group before changing it.</summary>
        public void Record(Object o)
        {
            if (o != null) Undo.RecordObject(o, Title);
        }

        /// <summary>An asset (ScriptableObject, material) to save once every check has passed.</summary>
        public void SaveAsset(Object asset)
        {
            if (asset != null && !assetsToSave.Contains(asset)) assetsToSave.Add(asset);
        }

        /// <summary>Something to do only after every check has passed (e.g. saving a prefab).</summary>
        public void AfterPass(Action action)
        {
            if (action != null) afterPass.Add(action);
        }

        /// <summary>Something to do if the step fails after this point (e.g. unloading a prefab's contents unsaved).</summary>
        public void OnFail(Action action)
        {
            if (action != null) onFail.Add(action);
        }

        // ---- Serialized-property helpers: record, set, and report before → after. ----

        public SerializedProperty Prop(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            Require(p != null, $"{Describe(so.targetObject)} has no serialized field '{path}' (let Unity finish compiling, then run this again).");
            return p;
        }

        public void SetFloat(Object target, string path, float value, string label = null)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            float was = p.floatValue;
            if (Mathf.Approximately(was, value)) { Info($"{label ?? Describe(target) + "." + path}: already {value:0.###}"); return; }
            Record(target);
            p.floatValue = value;
            so.ApplyModifiedProperties();
            Changed($"{label ?? Describe(target) + "." + path}: {was:0.###} → {value:0.###}");
        }

        public void SetInt(Object target, string path, int value, string label = null)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            int was = p.propertyType == SerializedPropertyType.Enum ? p.enumValueIndex : p.intValue;
            if (was == value) { Info($"{label ?? Describe(target) + "." + path}: already {value}"); return; }
            Record(target);
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value;
            else p.intValue = value;
            so.ApplyModifiedProperties();
            Changed($"{label ?? Describe(target) + "." + path}: {was} → {value}");
        }

        public void SetBool(Object target, string path, bool value, string label = null)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            bool was = p.boolValue;
            if (was == value) { Info($"{label ?? Describe(target) + "." + path}: already {value}"); return; }
            Record(target);
            p.boolValue = value;
            so.ApplyModifiedProperties();
            Changed($"{label ?? Describe(target) + "." + path}: {was} → {value}");
        }

        public void SetString(Object target, string path, string value, string label = null)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            string was = p.stringValue;
            if (was == value) { Info($"{label ?? Describe(target) + "." + path}: already \"{value}\""); return; }
            Record(target);
            p.stringValue = value;
            so.ApplyModifiedProperties();
            Changed($"{label ?? Describe(target) + "." + path}: \"{was}\" → \"{value}\"");
        }

        public void SetObject(Object target, string path, Object value, string label = null)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, path);
            Object was = p.objectReferenceValue;
            if (was == value) { Info($"{label ?? Describe(target) + "." + path}: already {Describe(value)}"); return; }
            Record(target);
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            Changed($"{label ?? Describe(target) + "." + path}: {Describe(was)} → {Describe(value)}");
        }

        /// <summary>Switch a scene object off (never delete), recorded.</summary>
        public void SwitchOff(GameObject go, string why)
        {
            if (go == null || !go.activeSelf) { Info($"{PathOf(go)}: already off"); return; }
            Undo.RecordObject(go, Title);
            go.SetActive(false);
            Changed($"{PathOf(go)}: switched off ({why})");
        }

        /// <summary>Make a new scene object under a parent, registered for undo.</summary>
        public GameObject Make(string name, Transform parent, string why)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, Title);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, Title);
            Changed($"{PathOf(go)}: made ({why})");
            return go;
        }

        /// <summary>Add a component to a scene object, registered for undo.</summary>
        public T Add<T>(GameObject go, string why) where T : Component
        {
            T c = Undo.AddComponent<T>(go);
            Changed($"{PathOf(go)}: added {typeof(T).Name} ({why})");
            return c;
        }
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>Run a fix step: survey → change (one undo group) → checks → save its assets, or undo it all.</summary>
    public static void Step(string title, string label, Action<Run> survey, Action<Run> change, Action<Run> checks)
    {
        var run = new Run(title, label);
        run.Text.AppendLine($"{title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        int group = -1;
        bool began = false;
        try
        {
            Scene scene = RequireCafeScene(run);

            run.Section("Survey");
            survey?.Invoke(run);

            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            began = true;

            run.Section("Change");
            change?.Invoke(run);

            run.Section("Checks");
            checks?.Invoke(run);
            if (run.Failures > 0) throw new Refused($"{run.Failures} check(s) failed, so the whole change is undone.");

            Undo.CollapseUndoOperations(group);
            foreach (Object asset in run.assetsToSave)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                run.Info("saved asset " + AssetDatabase.GetAssetPath(asset));
            }
            foreach (Action a in run.afterPass) a();
            if (run.Changes > 0) EditorSceneManager.MarkSceneDirty(scene);

            run.Text.AppendLine().AppendLine(run.Changes == 0
                ? $"NOTHING TO CHANGE: already done. {run.Passes} check(s) passed."
                : $"DONE: {run.Changes} change(s); {run.Passes} check(s) passed. The scene is NOT saved: read this report, look, then File › Save.");
            Debug.Log($"[Playtest 4] {title}: {(run.Changes == 0 ? "nothing to change" : "done")} ({run.Changes} change(s), {run.Passes} check(s) passed). Report: {Write(run)}");
        }
        catch (Exception e)
        {
            if (began)
            {
                Undo.RevertAllDownToGroup(group);
                run.Text.AppendLine("  (every change of this step is undone; no asset was saved)");
            }
            foreach (Action a in run.onFail)
            {
                try { a(); }
                catch (Exception cleanup) { run.Text.AppendLine("  (cleanup: " + cleanup.Message + ")"); }
            }
            bool refused = e is Refused;
            run.Text.AppendLine().AppendLine((refused ? "REFUSED: " : "ERROR: ") + e.Message);
            if (!refused) run.Text.AppendLine(e.ToString());
            Debug.LogError($"[Playtest 4] {title}: {(refused ? "refused" : "error")}: {e.Message}  Report: {Write(run)}");
        }
    }

    /// <summary>A read-only survey ("0 - Check first"): nothing changes.</summary>
    public static void Survey(string title, string label, Action<Run> survey)
    {
        var run = new Run(title, label);
        run.Text.AppendLine($"{title}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})   read-only");
        try
        {
            RequireCafeScene(run, allowDirty: true);
            run.Section("Survey");
            survey?.Invoke(run);
            run.Text.AppendLine().AppendLine($"READ-ONLY: {run.Passes} passed, {run.Failures} failed.");
            Debug.Log($"[Playtest 4] {title}: {run.Passes} passed, {run.Failures} failed (read-only). Report: {Write(run)}");
        }
        catch (Exception e)
        {
            run.Text.AppendLine().AppendLine((e is Refused ? "REFUSED: " : "ERROR: ") + e.Message);
            if (!(e is Refused)) run.Text.AppendLine(e.ToString());
            Debug.LogError($"[Playtest 4] {title}: {e.Message}  Report: {Write(run)}");
        }
    }

    /// <summary>
    /// Edit a prefab asset outside the scene's undo (Unity has none for prefab contents): edit → checks on the loaded
    /// contents → saved only when the step passes (after the scene's checks too). The report holds before → after.
    /// </summary>
    public static void EditPrefab(Run run, string assetPath, Action<GameObject> edit, Func<GameObject, bool> checks)
    {
        run.Require(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null, $"No prefab at {assetPath}.");
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        bool keep = false;
        try
        {
            edit(root);
            keep = checks == null || checks(root);
            run.Check(keep, $"{assetPath}: its own checks");
        }
        finally
        {
            if (!keep) PrefabUtility.UnloadPrefabContents(root);
        }
        if (keep)
        {
            run.AfterPass(() =>
            {
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                PrefabUtility.UnloadPrefabContents(root);
                run.Info("saved prefab " + assetPath);
            });
            run.OnFail(() => PrefabUtility.UnloadPrefabContents(root));
        }
    }

    // ---------------------------------------------------------------------------------------------

    public static Scene RequireCafeScene(Run run, bool allowDirty = false)
    {
        run.Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode first (Edit › Play Mode › Play).");
        Scene scene = SceneManager.GetActiveScene();
        run.Require(scene.path == ScenePath, $"Open the café scene ({ScenePath}) first.");
        run.Require(allowDirty || !scene.isDirty,
            "The café scene has unsaved changes. Save or revert them first, so this step's changes are its own.");
        return scene;
    }

    /// <summary>The one component of a type in the open scene (inactive included), or refuse.</summary>
    public static T One<T>(Run run) where T : Object
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        run.Require(all.Length == 1, $"Expected one {typeof(T).Name} in the café scene, found {all.Length}.");
        return all[0];
    }

    /// <summary>A scene object by its full path from the root ("Root/Child/Grandchild"), inactive included, or refuse.</summary>
    public static GameObject ByPath(Run run, string path)
    {
        GameObject found = FindByPath(path);
        run.Require(found != null, $"No object at '{path}' in the café scene.");
        return found;
    }

    public static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
            {
                Transform next = null;
                foreach (Transform child in t)
                    if (child.name == parts[i]) { next = child; break; }
                t = next;
            }
            if (t != null) return t.gameObject;
        }
        return null;
    }

    public static string PathOf(GameObject go)
    {
        if (go == null) return "(none)";
        var names = new List<string>();
        for (Transform t = go.transform; t != null; t = t.parent) names.Add(t.name);
        names.Reverse();
        return string.Join("/", names);
    }

    public static string Describe(Object o)
    {
        if (o == null) return "(none)";
        if (o is Component c) return PathOf(c.gameObject) + " · " + c.GetType().Name;
        if (o is GameObject g) return PathOf(g);
        string path = AssetDatabase.GetAssetPath(o);
        return string.IsNullOrEmpty(path) ? o.name : path;
    }

    /// <summary>Write the report: Logs/Playtest4/&lt;label&gt;-&lt;time&gt;.txt and &lt;label&gt;-last.txt. Returns the path.</summary>
    public static string Write(Run run)
    {
        Directory.CreateDirectory(LogRoot);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string path = Path.Combine(LogRoot, $"{run.Label}-{stamp}.txt");
        File.WriteAllText(path, run.Text.ToString());
        File.WriteAllText(Path.Combine(LogRoot, $"{run.Label}-last.txt"), run.Text.ToString());
        return path;
    }

    /// <summary>Greyed in Play Mode (use as the menu item's validate function).</summary>
    public static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
