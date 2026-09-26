#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ---------------------------------------------------------------------------
// ONE-OFF RECOVERY, 26 SEPT 2026
//
// What happened: on 26 Sept the project was switched to the old `main` branch
// (5 Sept) in GitHub Desktop. That branch's .gitignore predates the Synty
// purchase, so GitHub Desktop counted the Synty packs (and the other folders
// the newer .gitignore hides) as "changes" and, when "leave my changes" was
// chosen, put them in a stash. A stash is a backup inside git; making it
// removes the files from disk. Everything built from Synty went missing or
// pink.
//
// What this does: copies those folders back out of the stashes into the
// project folder, and nothing else.
//   - Working folder only (git restore --worktree --overlay): nothing is
//     staged or committed, and no existing file is deleted.
//   - The stashes are left in place as a backup.
//   - Folders the .gitignore hides stay hidden from GitHub Desktop, as before.
//
// Safe to delete this file once everything is back.
// ---------------------------------------------------------------------------
public static class StashRecovery
{
    // GitHub Desktop stash made on `main` at 05:10 (26 Sept): only these folders.
    const string MainStash = "a33d42ae7b89b1c2bde996e23828a42d60764db2";
    static readonly string[] MainStashPaths =
    {
        "Assets/Synty", "Assets/Synty.meta",
        "Assets/Art/CityNeighbors", "Assets/Art/CityNeighbors.meta",
        "Assets/LightingOptimizationTutorial", "Assets/LightingOptimizationTutorial.meta"
    };

    // GitHub Desktop stash made on codex/grace-showcase-baseline at 04:47: the
    // uncommitted leftovers that were deliberately kept out of the 25 Sept commit.
    const string BaselineStash = "8c92557abfa8d808b0b185ea32141005dadea9bf";
    static readonly string[] BaselineStashPaths =
    {
        "Assets/_Recovery", "Assets/_Recovery.meta",
        "Claude outputs",
        "DayLogs"
    };

    const string ExpectedHead = "94262854e642a84d20d3975d293a2a79db96e115";

    [MenuItem("Fixit Fidget/Recovery/Restore files swept into GitHub Desktop stashes (26 Sept)")]
    public static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Restore", "Stop Play Mode first.", "OK");
            return;
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        var log = new StringBuilder("[Stash recovery] project " + root + "\n");

        string git = FindGit(log);
        if (git == null)
        {
            Debug.LogError(log + "Could not find git.exe (checked PATH, Git for Windows and GitHub Desktop). Nothing was changed.");
            EditorUtility.DisplayDialog("Restore", "Could not find git on this computer. Nothing was changed.", "OK");
            return;
        }

        // Sanity checks before touching anything.
        var head = Run(git, root, "rev-parse HEAD");
        log.AppendLine($"HEAD: {head.output.Trim()}");
        foreach (string stash in new[] { MainStash, BaselineStash })
        {
            var kind = Run(git, root, $"cat-file -t {stash}");
            if (kind.exitCode != 0 || kind.output.Trim() != "commit")
            {
                Debug.LogError(log + $"Stash {stash} is not in this repository ({kind.output.Trim()}). Nothing was changed.");
                EditorUtility.DisplayDialog("Restore", "The stashes this tool expects are not in the repository. Nothing was changed.", "OK");
                return;
            }
        }
        if (head.output.Trim() != ExpectedHead)
            log.AppendLine($"Note: HEAD is not {ExpectedHead}. That's fine: only untracked folders and logs are restored.");

        string message =
            "This copies these back into the project folder from GitHub Desktop's stashes:\n\n" +
            "From the stash made on main:\n  " + string.Join("\n  ", MainStashPaths.Where(p => !p.EndsWith(".meta"))) + "\n\n" +
            "From the stash made on codex/grace-showcase-baseline:\n  " + string.Join("\n  ", BaselineStashPaths.Where(p => !p.EndsWith(".meta"))) + "\n\n" +
            "Nothing is staged or committed, nothing is deleted, and the stashes stay as a backup. " +
            "Unity will import the restored assets afterwards, which can take a few minutes.";
        if (!EditorUtility.DisplayDialog("Restore files from the stashes", message, "Restore", "Cancel"))
        {
            Debug.Log(log + "Cancelled. Nothing was changed.");
            return;
        }

        bool ok = true;
        try
        {
            EditorUtility.DisplayProgressBar("Restoring from stash", "Synty, CityNeighbors, lighting tutorial…", 0.2f);
            ok &= RestoreFrom(git, root, MainStash, MainStashPaths, log);
            EditorUtility.DisplayProgressBar("Restoring from stash", "_Recovery, Claude outputs, DayLogs…", 0.7f);
            ok &= RestoreFrom(git, root, BaselineStash, BaselineStashPaths, log);
        }
        finally { EditorUtility.ClearProgressBar(); }

        foreach (string path in MainStashPaths.Concat(BaselineStashPaths))
        {
            string full = Path.Combine(root, path);
            if (Directory.Exists(full)) log.AppendLine($"  {path}/: {Directory.GetFiles(full, "*", SearchOption.AllDirectories).Length} files");
            else if (File.Exists(full)) log.AppendLine($"  {path}: present");
            else log.AppendLine($"  {path}: MISSING");
        }

        var status = Run(git, root, "status --porcelain --untracked-files=no");
        int staged = status.output.Split('\n').Count(l => l.Length > 1 && l[0] != ' ' && l[0] != '?');
        log.AppendLine($"Staged changes after restore: {staged} (expected 0)");

        if (ok) Debug.Log(log + "Done. Unity will now import the restored assets.");
        else Debug.LogError(log + "Finished with errors (see above).");

        AssetDatabase.Refresh();
    }

    static bool RestoreFrom(string git, string root, string stash, string[] paths, StringBuilder log)
    {
        string args = $"restore --source={stash} --worktree --overlay -- " + string.Join(" ", paths.Select(Quote));
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var result = Run(git, root, args, 30 * 60 * 1000);
            log.AppendLine($"git {args}\n  exit {result.exitCode}{(string.IsNullOrWhiteSpace(result.output) ? "" : "\n  " + result.output.Trim().Replace("\n", "\n  "))}");
            if (result.exitCode == 0) return true;
            // GitHub Desktop may be holding the index lock for a moment.
            if (!result.output.Contains("index.lock")) return false;
            System.Threading.Thread.Sleep(2000);
        }
        return false;
    }

    static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

    static string FindGit(StringBuilder log)
    {
        var candidates = new List<string> { "git" };
        candidates.Add(@"C:\Program Files\Git\cmd\git.exe");
        candidates.Add(@"C:\Program Files\Git\bin\git.exe");
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string desktop = Path.Combine(local, "GitHubDesktop");
        if (Directory.Exists(desktop))
        {
            foreach (string app in Directory.GetDirectories(desktop, "app-*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(app, "resources", "app", "git", "cmd", "git.exe"));
                candidates.Add(Path.Combine(app, "resources", "app", "git", "mingw64", "bin", "git.exe"));
            }
        }
        foreach (string candidate in candidates)
        {
            if (candidate != "git" && !File.Exists(candidate)) continue;
            try
            {
                var version = Run(candidate, Environment.CurrentDirectory, "--version", 20000);
                if (version.exitCode == 0 && version.output.StartsWith("git version"))
                {
                    log.AppendLine($"Using {candidate}: {version.output.Trim()}");
                    return candidate;
                }
            }
            catch (Exception) { /* not this one */ }
        }
        return null;
    }

    struct Result { public int exitCode; public string output; }

    static Result Run(string exe, string workingDirectory, string arguments, int timeoutMs = 60000)
    {
        var info = new ProcessStartInfo(exe, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        var output = new StringBuilder();
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(); } catch (Exception) { }
            return new Result { exitCode = -1, output = output + "\n(timed out)" };
        }
        process.WaitForExit();
        return new Result { exitCode = process.ExitCode, output = output.ToString() };
    }
}
#endif
