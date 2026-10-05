#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ---------------------------------------------------------------------------
// THE TEST BUILD THAT KEPT RUNNING (30 Sept 2026)
//
// The Windows build made for the 4K frame-rate reading (Builds/Win64-2026-09-30) was meant to be
// closed with Alt+F4 before its day ended. The key never reached it (Windows' text-input helper
// held the keyboard), so the build ran on in the background, its Day 3 reached 8 PM with nobody
// serving, and DayClock.EndDay saved the recap into the real playtest save
// (playtest-aces-cafe.json: a build never runs as a café lab, so it uses the scene's save).
// SaveCheckpointStorage keeps the file it replaces as .bak, so Mansoor's checkpoint from before
// is intact beside it.
//
//   Fixit Fidget > Recovery > Close the test build (Builds folder only)
//       Sends the build's window a close (as Alt+F4 would), and ends it if it hasn't gone in 5 s.
//       Only a FixitFidget.exe inside this project's Builds folder is touched.
//   Fixit Fidget > Recovery > Show the saves (read-only)
//       Every save in the persistent data folder: day, closed or not, money, reputation, stars,
//       cups and beans, and when it was written.
//   Fixit Fidget > Recovery > Put back the playtest save the test build wrote over (30 Sept)
//       Only when the save is the build's closed Day 3 and the .bak is the same day still open:
//       keeps the build's version aside (….test-build-2026-09-30) and puts the .bak back.
// ---------------------------------------------------------------------------
internal static class TestBuildCleanup
{
    const string Menu = "Fixit Fidget/Recovery/";
    const string Tag = "[Recovery] ";
    const string PlaytestSave = "playtest-aces-cafe.json";
    const string KeptAside = PlaytestSave + ".test-build-2026-09-30";

    [MenuItem(Menu + "Close the test build (Builds folder only)")]
    static void CloseBuild()
    {
        string builds = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
        var report = new StringBuilder();
        int closed = 0;
        foreach (Process p in Process.GetProcessesByName("FixitFidget"))
        {
            try
            {
                string file = null;
                try { file = p.MainModule?.FileName; } catch (Exception) { }
                if (file == null || !Path.GetFullPath(file).StartsWith(builds, StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine($"left alone: process {p.Id} ({file ?? "path unknown"}), not in {builds}");
                    continue;
                }
                bool asked = p.CloseMainWindow();
                if (!p.WaitForExit(5000))
                {
                    p.Kill();
                    p.WaitForExit(5000);
                    report.AppendLine($"ended: process {p.Id} ({file}) didn't close when asked" + (asked ? "" : " (no window to ask)"));
                }
                else report.AppendLine($"closed: process {p.Id} ({file})");
                closed++;
            }
            catch (Exception e)
            {
                report.AppendLine($"couldn't close process {p.Id}: {e.Message}");
            }
            finally { p.Dispose(); }
        }
        Debug.Log(Tag + (closed > 0 ? $"The test build is closed ({closed}).\n" : "No test build was running.\n") + report);
    }

    [MenuItem(Menu + "Show the saves (read-only)")]
    static void ShowSaves()
    {
        string folder = Application.persistentDataPath;
        var report = new StringBuilder($"Saves in {folder}:\n");
        foreach (string path in Directory.GetFiles(folder).Where(f => f.Contains(".json")).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            report.AppendLine(Describe(path));
        Debug.Log(Tag + report);
    }

    [MenuItem(Menu + "Put back the playtest save the test build wrote over (30 Sept)")]
    static void Restore()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (Process.GetProcessesByName("FixitFidget").Length > 0)
                throw new InvalidOperationException("A FixitFidget build is still running: close it first (Recovery > Close the test build).");
            string path = Path.Combine(Application.persistentDataPath, PlaytestSave), bak = path + ".bak", aside = Path.Combine(Application.persistentDataPath, KeptAside);
            if (!File.Exists(path) || !File.Exists(bak)) throw new InvalidOperationException($"Need both {PlaytestSave} and its .bak; found {(File.Exists(path) ? "the save" : "no save")} and {(File.Exists(bak) ? "a .bak" : "no .bak")}.");
            SaveData now = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            SaveData before = JsonUtility.FromJson<SaveData>(File.ReadAllText(bak));
            if (now == null || before == null) throw new InvalidOperationException("One of the two files isn't a save.");
            // Only the case this is for: the build closed Day 3 unattended over Mansoor's Day 3 morning.
            bool buildsDay = now.day == 3 && now.dayCompleted && File.GetLastWriteTime(path) > new DateTime(2026, 9, 30, 7, 0, 0);
            bool hisMorning = before.day == 3 && !before.dayCompleted;
            if (!buildsDay || !hisMorning)
                throw new InvalidOperationException("The files aren't the ones this is for, so nothing was changed:\n  " + Describe(path) + "\n  " + Describe(bak));
            if (File.Exists(aside)) throw new InvalidOperationException(KeptAside + " already exists: this has been done once already.");
            File.Copy(path, aside);
            File.Copy(bak, path, overwrite: true);
            Debug.Log(Tag + "The playtest save is back to Mansoor's Day 3 morning. The build's unattended Day 3 is kept aside.\n  now:   " + Describe(path) + "\n  aside: " + Describe(aside));
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "Put back the playtest save: " + e.Message);
        }
    }

    [MenuItem(Menu + "Put back the playtest save the test build wrote over (30 Sept)", true)]
    static bool CanRestore() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static string Describe(string path)
    {
        string when = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        try
        {
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (d == null) return $"{Path.GetFileName(path)}  ({when}): not a save";
            return string.Format(CultureInfo.InvariantCulture,
                "{0}  ({1}): day {2} {3}, ${4}, reputation {5}, stars {6}, cups {7}, beans {8}",
                Path.GetFileName(path), when, d.day, d.dayCompleted ? "closed" : "open", d.money, d.reputation, d.starsEarned, d.cups, d.beans);
        }
        catch (Exception e)
        {
            return $"{Path.GetFileName(path)}  ({when}): unreadable ({e.Message})";
        }
    }
}
#endif
