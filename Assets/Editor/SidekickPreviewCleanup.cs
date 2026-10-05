#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// SYNTY'S SIDEKICK PREVIEW CHARACTER (5 Oct 2026)
//
// The Sidekick Character Creator window (Synty.SidekickCharacters.ModularCharacterWindow, in the git-ignored
// Assets/Synty) keeps a snapshot of the character it last built, and whenever its UI is rebuilt (after every
// script compile, while the window is docked anywhere) it puts that character back into the open scene as a
// root object called "Combined Character": 92 objects and a mesh embedded in the scene file, about 9 MB. On
// 5 Oct one rode along into a saved scene that way (never into a commit).
//
//   Fixit Fidget > Recovery > Close Synty's Sidekick window and remove its preview character (scene)
//
// Closes every Sidekick Character window (it is opened again from Synty's own menu whenever it is wanted,
// and its snapshot is kept in EditorPrefs, so nothing is lost) and removes every root "Combined Character"
// from the open scene, with undo. Nothing of Synty's is touched, and nothing here refers to their code.
// ---------------------------------------------------------------------------
internal static class SidekickPreviewCleanup
{
    const string WindowType = "Synty.SidekickCharacters.ModularCharacterWindow";
    const string PreviewName = "Combined Character";

    [MenuItem("Fixit Fidget/Recovery/Close Synty's Sidekick window and remove its preview character (scene)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Sidekick preview] Edit Mode only: stop Play first."); return; }
        int closed = 0;
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window == null || window.GetType().FullName != WindowType) continue;
            // Shown (docked or floating) windows close like any other; one never shown is only destroyed.
            if (window.rootVisualElement != null && window.rootVisualElement.panel != null) window.Close();
            else Object.DestroyImmediate(window);
            closed++;
        }
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] previews = scene.GetRootGameObjects().Where(go => go.name == PreviewName).ToArray();
        int removed = 0;
        if (previews.Length > 0)
        {
            Undo.SetCurrentGroupName("Remove Synty's Sidekick preview character");
            int group = Undo.GetCurrentGroup();
            foreach (GameObject go in previews)
            {
                int parts = go.GetComponentsInChildren<Transform>(true).Length;
                Undo.DestroyObjectImmediate(go);
                removed++;
                Debug.Log($"[Sidekick preview] Removed '{PreviewName}' ({parts} objects) from the scene.");
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log($"[Sidekick preview] {closed} Sidekick window(s) closed, {removed} preview character(s) removed" +
                  (removed > 0 ? ". Save the scene to keep it (Ctrl+S); Synty > Sidekick opens the window again when it is wanted." : "."));
    }
}
#endif
