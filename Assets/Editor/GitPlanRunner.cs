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

// Runs a short, pre-written list of git commands against this project and
// writes what happened to Logs/Git/. Made so that commits can be prepared as a
// file (Logs/Git/plan.json), reviewed, and run with one click, with the full
// output kept.
//
// Only a small set of git commands is allowed (status, add, commit, branch,
// checkout -b, switch -c, log, diff, show, rev-parse, config reads), plus one
// shape of fetch: "fetch . <branch>:<branch>", which fast-forwards a local
// branch to another local branch without switching to it (git refuses it
// unless it is a pure fast-forward, and refuses the checked-out branch) - how
// a finished branch is merged into main here. Nothing here can push, reset,
// clean, stash, delete branches or discard changes.
//
// plan.json:
//   { "steps": [ { "args": "add -- \"Assets/Scripts/Foo.cs\"" },
//                { "args": "commit -F Logs/Git/message.txt" } ] }
public static class GitPlanRunner
{
    const string Menu = "Fixit Fidget/Git/";
    const string Tag = "[Git] ";

    static readonly string[] AllowedFirstWords =
        { "status", "add", "commit", "branch", "checkout", "switch", "log", "diff", "show", "rev-parse", "config", "ls-files", "check-ignore", "fetch" };

    // "fetch . src:dst" with plain local branch names only: no '+' (force), no remote.
    static readonly System.Text.RegularExpressions.Regex LocalFastForward =
        new System.Text.RegularExpressions.Regex(@"^fetch \. [A-Za-z0-9._/-]+:[A-Za-z0-9._/-]+$");

    static string Root => Directory.GetParent(Application.dataPath).FullName;
    static string LogFolder => Path.Combine(Root, "Logs", "Git");

    [Serializable] class Plan { public Step[] steps = Array.Empty<Step>(); public bool stopOnError = true; }
    [Serializable] class Step { public string args = ""; }

    [MenuItem(Menu + "Status (writes Logs - Git - status.txt)")]
    public static void Status()
    {
        var log = new StringBuilder();
        string git = FindGit(log);
        if (git == null) { Debug.LogError(Tag + "git not found"); return; }
        foreach (string args in new[]
                 {
                     "rev-parse --abbrev-ref HEAD", "rev-parse --short HEAD", "config user.name", "config user.email",
                     "status --porcelain=v1 --untracked-files=all", "log --oneline -8", "branch --list"
                 })
        {
            var r = Run(git, Root, args);
            log.AppendLine($"$ git {args}\n(exit {r.exitCode})\n{r.output}");
        }
        Directory.CreateDirectory(LogFolder);
        File.WriteAllText(Path.Combine(LogFolder, "status.txt"), log.ToString(), new UTF8Encoding(false));
        Debug.Log(Tag + "Status written to Logs/Git/status.txt");
    }

    [MenuItem(Menu + "Run plan (Logs - Git - plan.json)")]
    public static void RunPlan()
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(LogFolder);
        string planPath = Path.Combine(LogFolder, "plan.json");
        string resultPath = Path.Combine(LogFolder, "result.txt");
        if (!File.Exists(planPath)) { Debug.LogError(Tag + "No Logs/Git/plan.json"); return; }
        Plan plan;
        try { plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planPath)); }
        catch (Exception e) { Debug.LogError(Tag + "plan.json could not be read: " + e.Message); return; }
        string git = FindGit(log);
        if (git == null) { Debug.LogError(Tag + "git not found"); return; }
        bool ok = true;
        foreach (Step step in plan.steps ?? Array.Empty<Step>())
        {
            string args = (step.args ?? "").Trim();
            if (args.Length == 0) continue;
            string first = args.Split(' ')[0];
            bool allowed = AllowedFirstWords.Contains(first)
                && !(first == "checkout" && !args.StartsWith("checkout -b "))
                && !(first == "switch" && !args.StartsWith("switch -c "))
                && !(first == "branch" && (args.Contains(" -d") || args.Contains(" -D") || args.Contains(" -m") || args.Contains(" -f")))
                && !(first == "config" && args.Split(' ').Length > 2)
                && !(first == "fetch" && !LocalFastForward.IsMatch(args))
                && !args.Contains(" -i");
            if (!allowed)
            {
                log.AppendLine($"$ git {args}\nREFUSED: not in the allowed list.");
                ok = false;
                break;
            }
            var r = Run(git, Root, args, 10 * 60 * 1000);
            log.AppendLine($"$ git {args}\n(exit {r.exitCode})\n{r.output}");
            if (r.exitCode != 0) { ok = false; if (plan.stopOnError) break; }
        }
        log.AppendLine(ok ? "PLAN OK" : "PLAN STOPPED");
        File.WriteAllText(resultPath, log.ToString(), new UTF8Encoding(false));
        // The plan is consumed so it cannot be run twice by accident.
        File.Move(planPath, Path.Combine(LogFolder, $"plan-{DateTime.Now:yyyyMMdd-HHmmss}.done.json"));
        if (ok) Debug.Log(Tag + "Plan ran. Logs/Git/result.txt");
        else Debug.LogWarning(Tag + "Plan stopped. Logs/Git/result.txt");
    }

    static string FindGit(StringBuilder log)
    {
        var candidates = new List<string> { "git", @"C:\Program Files\Git\cmd\git.exe", @"C:\Program Files\Git\bin\git.exe" };
        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
        if (Directory.Exists(desktop))
            foreach (string app in Directory.GetDirectories(desktop, "app-*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(app, "resources", "app", "git", "cmd", "git.exe"));
                candidates.Add(Path.Combine(app, "resources", "app", "git", "mingw64", "bin", "git.exe"));
            }
        foreach (string candidate in candidates)
        {
            if (candidate != "git" && !File.Exists(candidate)) continue;
            try
            {
                var version = Run(candidate, Root, "--version", 20000);
                if (version.exitCode == 0 && version.output.StartsWith("git version"))
                {
                    log.AppendLine($"Using {candidate}: {version.output.Trim()}");
                    return candidate;
                }
            }
            catch (Exception) { }
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
