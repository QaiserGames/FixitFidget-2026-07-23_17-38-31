#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// A FRESH GAME THAT KEEPS HIS SAVE (12 Oct 2026; claude/playtest-4-findings-and-proposals.md §11.2, his "yes")
//
//   Fixit Fidget > Playtest > Fresh game 0 - Check (read-only)
//   Fixit Fidget > Playtest > Fresh game 1 - Start a new game (keeps your save as a copy)
//   Fixit Fidget > Playtest > Fresh game 2 - Bring my saved game back
//
// His save is on Day 5's morning and playtest 5 is Days 1-3 on a fresh Day 1 with a controller. Step 1 copies the REAL
// save (the file the café scene's SaveManager reads: Application.persistentDataPath/<name>, the name from the scene's own
// fields: playtest-aces-cafe.json in this scene; never the lab's playtest-cafe-lab.json) to
// <same folder>/backups/<file>.<yyyy-MM-dd_HHmmss>.bak, proves the copy byte for byte, writes a new game's first morning
// over the save with the game's own atomic writer (SaveCheckpointStorage.Write, which also leaves <file>.bak beside it),
// and presses Play on the REAL game: the real save, the real day logs, no lab session (no CafeLab keys; a stale one is
// cleared). Step 2 lists the copies, offers the newest in a dialog (its date, day and money; "Choose another..." opens
// the folder), copies the save as it is now to the backups first, then puts the chosen copy back, byte for byte. Nothing
// is ever deleted. Not available while playing. Step 0 only reads: where the save is, what it holds, which copies exist,
// and a dry run of the copy into Logs/FreshGame/check-<time>/, compared byte for byte, with a report.txt.
//
// Why Edit Mode is safe here: SaveManager reads its file once, in Awake, as a Play session starts (LoadFromDisk), and
// writes it only at the recap and at Open Tomorrow (TrySaveRecap, TrySaveNextDay). It is not ExecuteAlways, so in Edit
// Mode there is no instance and nothing holds the save in memory: a file copied or replaced now is simply what the next
// Play session loads. The save's name is read from the component's serialized fields rather than asked of it: the rule
// (SaveManager.SaveFileName) has a CafeLab.Active branch, and that static can still be true in the editor's domain after
// a lab session, which would point this menu at the lab's file.
// ---------------------------------------------------------------------------
internal static class FreshGameMenu
{
    const string Menu = "Fixit Fidget/Playtest/";
    const string MenuCheck = Menu + "Fresh game 0 - Check (read-only)";
    const string MenuStart = Menu + "Fresh game 1 - Start a new game (keeps your save as a copy)";
    const string MenuRestore = Menu + "Fresh game 2 - Bring my saved game back";
    const string Tag = "[Fresh game] ";
    const string BackupsFolderName = "backups";
    const string Stamp = "yyyy-MM-dd_HHmmss";

    static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "FreshGame");

    // The real save, as the café scene names it.
    sealed class TheSave
    {
        public string name, folder, path, backups;
        public bool exists;
    }

    // ---------- 0 - Check (read-only) ----------

    [MenuItem(MenuCheck, false, 300)]
    static void Check()
    {
        var report = new StringBuilder();
        int passes = 0, failures = 0;
        void Line(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        void Info(string what) => report.AppendLine("      " + what);

        report.AppendLine($"Fresh game 0 - Check (read-only)   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        TheSave save = Resolve(out string problem);
        if (save == null)
        {
            Debug.LogError(Tag + problem);
            return;
        }
        Info($"The real save: {save.path}");
        Info($"Copies go to: {save.backups}");
        Line(save.name != CafeLab.SaveFileName, $"the save is the real game's ({save.name}), not the lab's ({CafeLab.SaveFileName})");
        if (save.exists) Info($"It holds: {Describe(save.path)} ({new FileInfo(save.path).Length} bytes, written {File.GetLastWriteTime(save.path):yyyy-MM-dd HH:mm:ss})");
        else Info("There is no save file yet: a new game already; Fresh game 1 would have nothing to keep.");
        List<string> copies = Backups(save);
        Info($"{copies.Count} cop{(copies.Count == 1 ? "y" : "ies")} in the backups folder" + (copies.Count > 0 ? ":" : "."));
        foreach (string copy in copies.Take(12))
            Info($"   {Path.GetFileName(copy)}  {File.GetLastWriteTime(copy):yyyy-MM-dd HH:mm:ss}  {Describe(copy)}");
        if (copies.Count > 12) Info($"   ... and {copies.Count - 12} more");
        Info($"Lab keys waiting on the next Play: {(PlayerPrefs.HasKey(CafeLab.PendingKey) ? "a lab session is pending (Fresh game 1 clears it)" : "none")}.");

        // The dry run: the copy as step 1 would make it, into the project's Logs instead of the backups, compared byte for byte.
        string folder = Path.Combine(LogRoot, $"check-{DateTime.Now.ToString(Stamp)}");
        Directory.CreateDirectory(folder);
        if (save.exists)
        {
            try
            {
                string dry = Path.Combine(folder, $"{save.name}.{DateTime.Now.ToString(Stamp)}.bak");
                File.Copy(save.path, dry, false);
                bool same = SameBytes(save.path, dry);
                Line(same, $"a dry-run copy is byte for byte the save ({new FileInfo(dry).Length} bytes): {dry}");
                try
                {
                    SaveData data = SaveCheckpointStorage.Read(dry);
                    Line(data != null, $"the copy reads back as a save (Day {(data != null ? data.day : 0)})");
                }
                catch (Exception e) { Line(false, "the copy reads back as a save (" + e.Message + ")"); }
            }
            catch (Exception e) { Line(false, "the dry-run copy could be made (" + e.Message + ")"); }
        }
        try
        {
            Directory.CreateDirectory(save.backups);
            Line(Directory.Exists(save.backups), $"the backups folder exists or could be made: {save.backups}");
        }
        catch (Exception e) { Line(false, "the backups folder could be made (" + e.Message + ")"); }

        report.AppendLine();
        report.AppendLine($"{passes} passed, {failures} failed. Nothing was changed.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning(Tag + "Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log(Tag + $"Check: {passes} passed. {folder}\n{text}");
        else Debug.LogError(Tag + $"Check: {failures} of {passes + failures} FAILED. {folder}\n{text}");
    }

    // ---------- 1 - Start a new game (keeps your save as a copy) ----------

    [MenuItem(MenuStart, false, 301)]
    static void Start()
    {
        TheSave save = Resolve(out string problem);
        if (save == null)
        {
            Debug.LogError(Tag + problem);
            return;
        }
        string now = save.exists ? Describe(save.path) : "no save yet (a new game already)";
        bool go = EditorUtility.DisplayDialog("Start a new game",
            $"Your save: {now}\n{save.path}\n\n" +
            (save.exists ? $"It is kept first, byte for byte, as a copy in\n{save.backups}\n(Fresh game 2 brings it back.)\n\n" : "") +
            "Then the game starts on Day 1's morning on the real save, with the real day logs. Not a lab session.",
            "Start a new game", "Cancel");
        if (!go) return;

        string kept = null;
        if (save.exists)
        {
            kept = BackUp(save, out problem);
            if (kept == null)
            {
                Debug.LogError(Tag + problem);
                return;
            }
            Debug.Log(Tag + $"Your save ({now}) is kept at {kept}, byte for byte.");
        }
        else Debug.Log(Tag + $"No save at {save.path}: a new game already, nothing to keep.");

        try
        {
            // A new game's first morning, written the way the game writes every checkpoint (atomic; <file>.bak kept beside it).
            SaveCheckpointStorage.Write(save.path, new SaveData());
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + $"Couldn't write a new game's first morning to {save.path}: {e.Message}. " +
                           (kept != null ? $"Your save is untouched at its own path and copied at {kept}. " : "") + "Nothing was started.");
            return;
        }

        // The real game, not a lab: a lab request left over from a Play session that never started must not ride along.
        if (PlayerPrefs.HasKey(CafeLab.PendingKey))
        {
            PlayerPrefs.DeleteKey(CafeLab.PendingKey);
            PlayerPrefs.Save();
            Debug.Log(Tag + "A stale lab request was cleared: this is the real game.");
        }
        Debug.Log(Tag + $"A new game from Day 1 on your real save ({save.path}); the day logs are the real ones. " +
                  (kept != null ? $"Your old game is at {kept}: Fresh game 2 brings it back. " : "") + "Press Play again to stop.");
        EditorApplication.EnterPlaymode();
    }

    // ---------- 2 - Bring my saved game back ----------

    [MenuItem(MenuRestore, false, 302)]
    static void Restore()
    {
        TheSave save = Resolve(out string problem);
        if (save == null)
        {
            Debug.LogError(Tag + problem);
            return;
        }
        List<string> copies = Backups(save);
        if (copies.Count == 0)
        {
            EditorUtility.DisplayDialog("Bring my saved game back",
                $"There are no copies in\n{save.backups}\n\nFresh game 1 makes one each time it starts a new game.", "OK");
            return;
        }
        string now = save.exists ? Describe(save.path) : "no save (a new game)";
        string chosen = copies[0];
        int answer = EditorUtility.DisplayDialogComplex("Bring my saved game back",
            $"Newest copy: {File.GetLastWriteTime(chosen):yyyy-MM-dd HH:mm:ss}\n{Describe(chosen)}\n{Path.GetFileName(chosen)}\n\n" +
            $"The game as it is now ({now}) is kept as a copy first.\n\n" +
            $"{copies.Count} cop{(copies.Count == 1 ? "y" : "ies")} in {save.backups}",
            "Bring it back", "Cancel", "Choose another...");
        if (answer == 1) return;
        if (answer == 2)
        {
            string picked = EditorUtility.OpenFilePanel("Choose a copy of your saved game", save.backups, "bak");
            if (string.IsNullOrEmpty(picked)) return;
            chosen = picked;
            bool sure = EditorUtility.DisplayDialog("Bring my saved game back",
                $"{Path.GetFileName(chosen)}\n{File.GetLastWriteTime(chosen):yyyy-MM-dd HH:mm:ss}\n{Describe(chosen)}\n\n" +
                $"The game as it is now ({now}) is kept as a copy first.",
                "Bring it back", "Cancel");
            if (!sure) return;
        }

        // A copy that doesn't read as a save never goes back over the real one.
        try { SaveCheckpointStorage.Read(chosen); }
        catch (Exception e)
        {
            Debug.LogError(Tag + $"{chosen} doesn't read as a save ({e.Message}); nothing was changed.");
            return;
        }

        if (save.exists)
        {
            string kept = BackUp(save, out problem);
            if (kept == null)
            {
                Debug.LogError(Tag + problem);
                return;
            }
            Debug.Log(Tag + $"The game as it was ({now}) is kept at {kept}, byte for byte.");
        }

        try
        {
            // Into place the way the game's own writer does it: a temporary file beside the save, then one swap, so a
            // failure part way leaves the save as it was (and <file>.bak holds what it replaced, as every checkpoint does).
            string temporary = save.path + ".restore.tmp";
            File.Copy(chosen, temporary, true);
            if (File.Exists(save.path)) File.Replace(temporary, save.path, save.path + ".bak");
            else File.Move(temporary, save.path);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + $"Couldn't put {chosen} back as {save.path}: {e.Message}. Your save is as it was; its copies are in {save.backups}.");
            return;
        }
        if (!SameBytes(chosen, save.path))
        {
            Debug.LogError(Tag + $"The save at {save.path} is not byte for byte the copy {chosen}: look at both before playing.");
            return;
        }
        string back = Describe(save.path);
        Debug.Log(Tag + $"Back: {back}, from {Path.GetFileName(chosen)}, byte for byte. Press Play to play it.");
        EditorUtility.DisplayDialog("Bring my saved game back", $"Done: {back} is your save again.\n\nPress Play to play it.", "OK");
    }

    [MenuItem(MenuCheck, true)]
    [MenuItem(MenuStart, true)]
    [MenuItem(MenuRestore, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ---------- the save ----------

    // The real save, as the café scene's SaveManager names it (its own rule, SaveManager.SaveFileName, in the editor: the
    // playtest file when Use Interaction Playtest Save is on and the name is a valid "playtest-*.json", else save.json).
    static TheSave Resolve(out string problem)
    {
        problem = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { problem = "Stop Play Mode first (Edit > Play Mode > Play)."; return null; }
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            problem = $"Open the café scene ({Playtest4Shared.ScenePath}) first: the save's name is read from its SaveManager.";
            return null;
        }
        SaveManager manager = UnityEngine.Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (manager == null) { problem = "The café scene has no SaveManager."; return null; }
        var so = new SerializedObject(manager);
        SerializedProperty use = so.FindProperty("useInteractionPlaytestSave"), named = so.FindProperty("interactionPlaytestSaveName");
        if (use == null || named == null)
        {
            problem = "SaveManager's save-name fields (useInteractionPlaytestSave, interactionPlaytestSaveName) weren't found: the rule moved; tell Claude.";
            return null;
        }
        string name = "save.json";
        if (use.boolValue)
        {
            string n = named.stringValue;
            bool valid = !string.IsNullOrWhiteSpace(n) && n.StartsWith("playtest-", StringComparison.Ordinal)
                         && Path.GetFileName(n) == n && n.EndsWith(".json", StringComparison.Ordinal);
            name = valid ? n : "interaction-playtest.json";
        }
        if (name == CafeLab.SaveFileName) { problem = "The scene's save is the lab's own file; this menu is for the real game."; return null; }
        string folder = Application.persistentDataPath;
        var save = new TheSave { name = name, folder = folder, path = Path.Combine(folder, name), backups = Path.Combine(folder, BackupsFolderName) };
        save.exists = File.Exists(save.path);
        return save;
    }

    // Copies the save to backups/<file>.<stamp>.bak (always a new name, never over an older copy) and proves the copy
    // byte for byte. Null, with the reason, when it couldn't.
    static string BackUp(TheSave save, out string problem)
    {
        problem = null;
        try
        {
            Directory.CreateDirectory(save.backups);
            string stamp = DateTime.Now.ToString(Stamp);
            string target = Path.Combine(save.backups, $"{save.name}.{stamp}.bak");
            for (int n = 2; File.Exists(target); n++) target = Path.Combine(save.backups, $"{save.name}.{stamp}-{n}.bak");
            File.Copy(save.path, target, false);
            if (!SameBytes(save.path, target))
            {
                problem = $"The copy at {target} is not byte for byte the save; nothing else was done.";
                return null;
            }
            return target;
        }
        catch (Exception e)
        {
            problem = $"Couldn't copy {save.path} into {save.backups}: {e.Message}. Nothing else was done.";
            return null;
        }
    }

    // The copies of this save in the backups folder, newest first.
    static List<string> Backups(TheSave save)
    {
        if (!Directory.Exists(save.backups)) return new List<string>();
        return Directory.GetFiles(save.backups)
            .Where(p => Path.GetFileName(p).StartsWith(save.name + ".", StringComparison.Ordinal) && p.EndsWith(".bak", StringComparison.Ordinal))
            .OrderByDescending(File.GetLastWriteTimeUtc).ThenByDescending(p => p, StringComparer.Ordinal).ToList();
    }

    static bool SameBytes(string a, string b)
    {
        byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
        return x.Length == y.Length && x.SequenceEqual(y);
    }

    // What a save file holds, for the dialogs and the report (read only; a file that won't read says so).
    static string Describe(string path)
    {
        try
        {
            SaveData data = SaveCheckpointStorage.Read(path);
            return $"Day {data.day}{(data.dayCompleted ? " (closed, at its recap)" : "'s morning")}, ${data.money}, " +
                   $"{data.starsEarned} star{(data.starsEarned == 1 ? "" : "s")}";
        }
        catch (Exception e) { return "unreadable (" + e.Message + ")"; }
    }
}
#endif
