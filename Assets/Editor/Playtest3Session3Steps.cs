#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// PLAYTEST 3, SESSION 3: THE LAB (7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.5)
//
//   Fixit Fidget > Playtest > The phone by day - pause and HUD check (lab, drives itself)   (PauseCheck)
//
// It starts a lab session on a fresh Day 1 (the lab's own save; your playtest save is not used), the lab's autopilot off
// (customers still come in and wait; Ace stays where the check puts him), and a check that drives the phone by itself. Keep the Game view in front and leave the mouse
// and keyboard alone until it reports. The report and its photos go to Logs/Pause/.
// ---------------------------------------------------------------------------
static class Playtest3Session3Steps
{
    const string Menu = "Fixit Fidget/Playtest/";
    const string Tag = "[Playtest 3] ";

    [MenuItem(Menu + "The phone by day - pause and HUD check (lab, drives itself)")]
    static void Pause()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(PauseCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "The phone by day: a lab session on a fresh Day 1 that drives itself. Keep the Game view in front and " +
                  $"leave the mouse and keyboard alone until it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "The phone by day - pause and HUD check (lab, drives itself)", true)]
    static bool CanStart() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ---------- the pocket watch's new mainspring (found by the labs, 7 Oct) ----------
    //
    // The watch's mainspring (Part_Extra_Mainspring, made by Fault Expansion from the phone's screen) kept the phone's
    // "Fresh" as its new part: a reference into PhoneRepair.prefab. So a mended watch showed no new mainspring at all, and
    // every mended watch switched the phone prefab's own Fresh on, in the asset, which the next save of assets wrote to
    // disk (the stray "m_IsActive" in PhoneRepair.prefab, seen twice). This gives the watch its own new mainspring (the
    // same shape as the broken one, in the phone's new-part material, hidden until it's fitted) and hides the phone's again.
    const string WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab";
    const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab";

    [MenuItem(Menu + "The pocket watch's new mainspring - give it its own (prefab)")]
    static void WatchFreshPart()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play mode first."); return; }
        var log = new StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(WatchPath);
        try
        {
            int made = 0;
            foreach (ReplaceablePart part in root.GetComponentsInChildren<ReplaceablePart>(true))
            {
                var so = new SerializedObject(part);
                SerializedProperty freshProp = so.FindProperty("freshVisual"), brokenProp = so.FindProperty("brokenVisual");
                var fresh = freshProp.objectReferenceValue as GameObject;
                var broken = brokenProp.objectReferenceValue as GameObject;
                if (fresh == null || fresh.transform.IsChildOf(root.transform)) continue;   // none, or its own already
                if (broken == null) { log.AppendLine($"{part.name}: no broken part to shape a new one from; left alone."); continue; }
                var shape = broken.GetComponent<MeshFilter>();
                var foreignLook = fresh.GetComponent<MeshRenderer>();
                var ownLook = broken.GetComponent<MeshRenderer>();
                var piece = new GameObject("Fresh");
                piece.transform.SetParent(broken.transform.parent, false);
                piece.transform.localPosition = broken.transform.localPosition;
                piece.transform.localRotation = broken.transform.localRotation;
                piece.transform.localScale = broken.transform.localScale;
                piece.transform.SetSiblingIndex(broken.transform.GetSiblingIndex() + 1);
                piece.layer = broken.layer;
                piece.AddComponent<MeshFilter>().sharedMesh = shape != null ? shape.sharedMesh : null;
                var look = piece.AddComponent<MeshRenderer>();
                look.sharedMaterials = foreignLook != null ? foreignLook.sharedMaterials : ownLook != null ? ownLook.sharedMaterials : new Material[0];
                piece.SetActive(false);
                freshProp.objectReferenceValue = piece;
                so.ApplyModifiedPropertiesWithoutUndo();
                made++;
                log.AppendLine($"{part.name}: its new part was \"{fresh.name}\" in another prefab ({AssetDatabase.GetAssetPath(fresh)}); " +
                               $"now its own \"Fresh\" beside it (the same shape, {(look.sharedMaterial != null ? look.sharedMaterial.name : "no material")}), hidden until it's fitted.");
            }
            if (made > 0) PrefabUtility.SaveAsPrefabAsset(root, WatchPath);
            else log.AppendLine("The watch's parts already have new parts of their own: nothing changed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // The phone's own new screen hidden again, as authored (the watch's repairs had switched it on).
        GameObject phone = PrefabUtility.LoadPrefabContents(PhonePath);
        try
        {
            bool changed = false;
            foreach (ReplaceablePart part in phone.GetComponentsInChildren<ReplaceablePart>(true))
            {
                var fresh = new SerializedObject(part).FindProperty("freshVisual").objectReferenceValue as GameObject;
                if (fresh == null || !fresh.transform.IsChildOf(phone.transform) || !fresh.activeSelf) continue;
                fresh.SetActive(false);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(phone, PhonePath);
                log.AppendLine("The phone's own new screen (\"Fresh\") is hidden again, as authored.");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(phone); }
        Debug.Log(Tag + "The pocket watch's new mainspring:\n" + log + "\n" + DevicesKeepToThemselves());
    }

    // Read-only: every device prefab's parts point only at things inside their own prefab (a part's broken and new pieces,
    // a cover's screws and what's beneath it, a fault's objects). A reference into another prefab is the watch's bug above.
    [MenuItem("Fixit Fidget/Checks/Device parts keep to their own prefab (read-only)")]
    static void CheckDevices() => Debug.Log(DevicesKeepToThemselves());

    static string DevicesKeepToThemselves()
    {
        var found = new StringBuilder();
        int prefabs = 0, references = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AssetsPrefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<RepairJob>(true) == null && prefab.GetComponentInChildren<BenchInteractable>(true) == null) continue;
            prefabs++;
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var so = new SerializedObject(behaviour);
                SerializedProperty property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object target = property.objectReferenceValue;
                    if (!(target is GameObject) && !(target is Component)) continue;
                    references++;
                    string targetPath = AssetDatabase.GetAssetPath(target);
                    if (string.IsNullOrEmpty(targetPath) || targetPath == path) continue;
                    if (!targetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;
                    found.AppendLine($"  {path}: {behaviour.GetType().Name} on {behaviour.name}, {property.propertyPath} -> {target.name} in {targetPath}");
                }
            }
        }
        return found.Length == 0
            ? $"[Device parts] PASS: {prefabs} device prefabs, {references} references to their own pieces, none into another prefab. Read-only."
            : $"[Device parts] FAIL: a device points into another prefab (a mended part would change that prefab):\n{found}";
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!PlayerPrefs.HasKey(PauseCheck.PendingKey)) return;
        PlayerPrefs.DeleteKey(PauseCheck.PendingKey);
        PlayerPrefs.Save();
    }
}
#endif
