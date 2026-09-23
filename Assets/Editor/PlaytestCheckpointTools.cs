#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// The café playtest scene keeps its own checkpoint (playtest-aces-cafe.json) in
// Application.persistentDataPath, and every save keeps the checkpoint before it
// beside it as playtest-aces-cafe.json.bak (see SaveCheckpointStorage.Write).
//
// These tools show both, and step back one checkpoint when a playtest day went
// wrong — for example a day that closed while nobody was playing. Nothing is
// ever deleted: the checkpoint being replaced is kept as
// playtest-aces-cafe.replaced-<date-time>.json next to the others.
public static class PlaytestCheckpointTools
{
    const string Menu = "Fixit Fidget/Playtest/Café playtest save/";
    const string FileName = "playtest-aces-cafe.json";

    static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    static string BackupPath => SavePath + ".bak";

    [MenuItem(Menu + "Show checkpoint and previous checkpoint")]
    public static void Show()
    {
        var text = new StringBuilder("[Café save] " + Application.persistentDataPath + "\n");
        text.AppendLine("Current:  " + Describe(SavePath));
        text.AppendLine("Previous: " + Describe(BackupPath));

        // Copies for reading outside Unity (the save folder is under AppData).
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "SaveInspection",
            DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        if (File.Exists(SavePath)) File.Copy(SavePath, Path.Combine(folder, "current.json"));
        if (File.Exists(BackupPath)) File.Copy(BackupPath, Path.Combine(folder, "previous.json"));
        text.AppendLine("Copies: " + folder);
        Debug.Log(text.ToString());
    }

    [MenuItem(Menu + "Step back to previous checkpoint")]
    public static void StepBack()
    {
        if (!File.Exists(BackupPath))
        {
            EditorUtility.DisplayDialog("Café playtest save", "There is no previous checkpoint to step back to.", "OK");
            return;
        }
        string replaced = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.replaced-"
            + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".json");
        string message = "Current checkpoint:\n" + Describe(SavePath)
            + "\n\nPrevious checkpoint:\n" + Describe(BackupPath)
            + "\n\nStep back to the previous checkpoint? The current one is kept as\n" + Path.GetFileName(replaced);
        if (!EditorUtility.DisplayDialog("Café playtest save", message, "Step back", "Cancel")) return;

        if (File.Exists(SavePath)) File.Move(SavePath, replaced);
        File.Copy(BackupPath, SavePath);
        Debug.Log("[Café save] Stepped back. Now: " + Describe(SavePath) + "\nKept the replaced checkpoint as " + replaced);
    }

    [MenuItem(Menu + "Show checkpoint and previous checkpoint", true)]
    [MenuItem(Menu + "Step back to previous checkpoint", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static string Describe(string path)
    {
        if (!File.Exists(path)) return "(none)";
        try
        {
            // Raw read on purpose: show what is on disk even if it would not load.
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null) return "(empty)";
            var s = new StringBuilder();
            s.Append($"Day {data.day} {(data.dayCompleted ? "closed (recap)" : "start")}, ${data.money}, cups {data.cups}, beans {data.beans}, saved {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm}");
            if (data.regularMemories != null)
                foreach (var m in data.regularMemories)
                    if (m != null)
                        s.Append($"\n    {m.profileId}: visits {m.visits}, relationship {m.relationship}, last seen day {m.lastSeenDay}, "
                            + $"last served {(m.lastVisitServed ? "yes" : "no")}{(string.IsNullOrEmpty(m.lastLossReason) ? "" : ", lost: " + m.lastLossReason)}, "
                            + $"camera {(string.IsNullOrEmpty(m.graceCameraGrade) ? "-" : m.graceCameraGrade)}, photo claimed {(m.gracePhotoClaimed ? "yes" : "no")}");
            return s.ToString();
        }
        catch (Exception e)
        {
            return "(unreadable: " + e.Message + ")";
        }
    }
}
#endif
