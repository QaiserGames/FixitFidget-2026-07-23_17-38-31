#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// SYNTY'S HIDDEN DOWNLOADER WINDOW (29 Sept 2026: claude/break-ins-chunk-a-handoff.md, Mansoor's choice)
//
// Synty's Sidekick plugin (the Character Creator, in the git-ignored Assets/Synty) makes a "Sidekick Tool
// Downloader" window after every script reload, to check for a newer version, and never shows it
// (DownloaderBackgroundService in its ToolDownloader.cs). That window outlives the next reload. When Play
// starts, Unity finds an editor window that isn't in any view and logs "Invalid editor window" as an error.
// With the Console's Error Pause on, the game pauses, and every resume logs it again, so Play can't run.
//
// So this closes any Tool Downloader window that isn't showing: just before Play starts, and again for the
// first few seconds of Play, since the plugin makes a new one straight after the reload into Play Mode.
// A Tool Downloader window that is showing is left alone. Synty's own code copes with a closed one: its
// menu (Synty > Sidekick Tool Downloader) makes a new window whenever it's wanted, and its version check,
// if still waiting, makes a new one when it finishes. Synty's files aren't touched, and nothing here
// refers to their code, so a clone of the public repository without Synty has nothing to close.
[InitializeOnLoad]
internal static class SidekickHiddenWindowFix
{
    const string WindowType = "Synty.SidekickCharacters.ToolDownloader";
    const double PlayWatchSeconds = 3.0;

    static double watchUntil;

    static SidekickHiddenWindowFix()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        switch (change)
        {
            case PlayModeStateChange.ExitingEditMode:
                CloseHidden();
                break;
            case PlayModeStateChange.EnteredPlayMode:
                watchUntil = EditorApplication.timeSinceStartup + PlayWatchSeconds;
                EditorApplication.update -= Watch;
                EditorApplication.update += Watch;
                CloseHidden();
                break;
            case PlayModeStateChange.ExitingPlayMode:
                EditorApplication.update -= Watch;
                break;
        }
    }

    static void Watch()
    {
        CloseHidden();
        if (EditorApplication.timeSinceStartup > watchUntil || !EditorApplication.isPlaying) EditorApplication.update -= Watch;
    }

    /// <summary>Closes every Sidekick Tool Downloader window that isn't showing; returns how many.</summary>
    internal static int CloseHidden()
    {
        int closed = 0;
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window == null || window.GetType().FullName != WindowType) continue;
            // Showing (docked or floating): its UI is on a panel. Never shown: no panel.
            if (window.rootVisualElement != null && window.rootVisualElement.panel != null) continue;
            Object.DestroyImmediate(window);
            closed++;
        }
        return closed;
    }
}
#endif
