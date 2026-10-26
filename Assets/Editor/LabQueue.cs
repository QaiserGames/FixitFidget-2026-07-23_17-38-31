#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ---------------------------------------------------------------------------
// THE LAB QUEUE: THE LABS ONE AFTER ANOTHER, BY THEMSELVES (26 Oct 2026)
//
// Until now the integrator started each lab by hand from the cloud, through a menu on his Mac, then polled its
// log every 40 s. A full run is about 40 labs and 3 hours, and every poll costs usage ("preserve usage"). The
// queue presses the house's own lab menus one after another inside Unity and writes one summary, so the
// integrator presses one menu and comes back when it's done.
//
//   Fixit Fidget > Lab queue > Run the queue (Logs › LabQueue › queue.txt). A "/" in a menu name opens a submenu, so the path uses ›
//   Fixit Fidget > Lab queue > Write the full run into Logs/LabQueue/queue.txt   (the default list below)
//   Fixit Fidget > Lab queue > Stop after the current entry
//   Fixit Fidget > Lab queue > Status (what's running)
//   Or, with no menu at all: a file Logs/LabQueue/run-request.txt (26 Oct 2026: his Mac's screen locks while he's away, and
//   then nothing can press a menu, but the editor keeps ticking). While idle the queue looks for it every 5 s, takes it
//   (renamed run-request-taken-<time>.txt, kept as the record) and runs queue.txt exactly as the menu does.
//
// The queue file: one entry per line, "#" starts a comment, blank lines are skipped, fields split by " | ":
//   reimport
//   edit    | <menu path> | <end regex> | <timeout s>
//   play    | <menu path> | <end regex> | <timeout s>
//   session | <session menu path> | <ready regex> | <check menu path> | <end regex> | <timeout s>
//
// Why it is built this way:
// - Entering Play Mode reloads the scripts, which wipes every static. So everything the queue knows lives in
//   SessionState (it survives a domain reload, not an editor restart), and this [InitializeOnLoad] class
//   re-hooks EditorApplication.update and Application.logMessageReceived after every reload.
// - A lab's end line is logged in whichever domain is live at that moment (often the Play Mode one, and some labs
//   end Play Mode a moment later), so the console listener writes what it saw straight into SessionState. The
//   update loop then closes the entry, in whichever domain it wakes up in.
// - Only lines logged after an entry starts count, so three runs of the same lab never read each other's lines.
// - Some labs end Play Mode themselves (the Day 1 guide, Hands on it): the queue reads isPlaying first and only
//   asks Unity to leave Play Mode when it is still playing. It never enters plain Play and never touches a save;
//   it only presses the house's own menus, and the labs keep his save safe themselves.
// - Run In Background (26 Oct 2026): his project has it off, so Play Mode froze whenever another app was in front. A run
//   turns it on and puts his value back when it ends, stops or finishes (kept in SessionState); never outside a run.
// - The summary is rewritten after every entry, so a stopped or crashed run still has its results.
// ---------------------------------------------------------------------------
[InitializeOnLoad]
internal static class LabQueue
{
    const string Tag = "[Lab queue] ";
    const string Menu = "Fixit Fidget/Lab queue/";
    const string ReimportMenu = "Fixit Fidget/Checks/Pick up scripts changed from outside (reimport)";
    const string PassPattern = @"All \d+ checks passed|PASS|passed, 0 failed|\bdone\b";
    const int ReimportTimeout = 900;      // seconds: a full compile on his Mac is well under this
    const double ReimportQuiet = 10;      // seconds of no compiling or updating in a row
    const double SettleSeconds = 2;       // the pause between entries, once in Edit Mode
    const double CheckMenuGrace = 30;     // how long a session's check menu may stay disabled after the ready line
    const double ExitRetrySeconds = 30;   // ask again if Play Mode still hasn't ended

    // ---------- the state, in SessionState so it survives the reload of entering Play Mode ----------

    const string K = "FixitFidget.LabQueue.";
    enum Phase { Idle = 0, Settle = 1, Begin = 2, WaitEnd = 3, WaitReady = 4, PressCheck = 5, ReimportQuietWait = 6 }

    static Phase State { get => (Phase)SessionState.GetInt(K + "phase", 0); set => SessionState.SetInt(K + "phase", (int)value); }
    static int Index { get => SessionState.GetInt(K + "index", 0); set => SessionState.SetInt(K + "index", value); }
    static string Lines { get => SessionState.GetString(K + "lines", ""); set => SessionState.SetString(K + "lines", value); }
    static string QueuePath { get => SessionState.GetString(K + "path", ""); set => SessionState.SetString(K + "path", value); }
    static string SummaryPath { get => SessionState.GetString(K + "summary", ""); set => SessionState.SetString(K + "summary", value); }
    static bool StopAsked { get => SessionState.GetBool(K + "stop", false); set => SessionState.SetBool(K + "stop", value); }
    static string StopWhy { get => SessionState.GetString(K + "stopWhy", ""); set => SessionState.SetString(K + "stopWhy", value); }
    static string EndLine { get => SessionState.GetString(K + "endLine", ""); set => SessionState.SetString(K + "endLine", value); }
    static string LastLine { get => SessionState.GetString(K + "lastLine", ""); set => SessionState.SetString(K + "lastLine", value); }
    static int Exceptions { get => SessionState.GetInt(K + "exceptions", 0); set => SessionState.SetInt(K + "exceptions", value); }
    static string FirstException { get => SessionState.GetString(K + "firstException", ""); set => SessionState.SetString(K + "firstException", value); }

    // Times are DateTime.UtcNow ticks, kept as strings: SessionState has no long, and a float loses seconds.
    static long Ticks(string key) => long.TryParse(SessionState.GetString(K + key, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? t : 0;
    static void SetTicks(string key, long t) => SessionState.SetString(K + key, t.ToString(CultureInfo.InvariantCulture));
    static void Mark(string key) => SetTicks(key, DateTime.UtcNow.Ticks);
    static double Since(string key) { long t = Ticks(key); return t == 0 ? 0 : (DateTime.UtcNow.Ticks - t) / (double)TimeSpan.TicksPerSecond; }

    // One result per entry: kind \t result \t seconds \t exceptions \t end line \t first exception.
    static string ResultKey(int i) => K + "result." + i.ToString(CultureInfo.InvariantCulture);

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Folder => Path.Combine(ProjectRoot, "Logs", "LabQueue");
    static string DefaultQueue => Path.Combine(Folder, "queue.txt");

    static LabQueue()
    {
        // Every reload (entering or leaving Play Mode, a compile) runs this again: hook back in, once.
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        // 26 Oct 2026 (Mac run fix): a run that ended in a domain that never got to put Run In Background back (or a hold
        // left by an editor that quit mid-run within this session) is put right on the next reload; quitting puts it back too.
        EditorApplication.quitting -= ReleaseRunInBackground;
        EditorApplication.quitting += ReleaseRunInBackground;
        if (State == Phase.Idle && RunInBackgroundHeld) ReleaseRunInBackground();
    }

    // ---------- 26 Oct 2026 (Mac run fix): Run In Background, only while the queue runs ----------
    // His project has Run In Background off (ProjectSettings runInBackground: 0), so Play Mode freezes whenever another app
    // is in front: five labs timed out on 26 Oct while he used his Mac. A run turns it on and puts his value back when it
    // ends, stops or finishes; the value it found is kept in SessionState, so the reload of entering Play Mode can't lose it.
    // Outside a run his setting is never touched.
    static bool RunInBackgroundHeld { get => SessionState.GetBool(K + "ribHeld", false); set => SessionState.SetBool(K + "ribHeld", value); }
    static bool RunInBackgroundWas { get => SessionState.GetBool(K + "ribWas", false); set => SessionState.SetBool(K + "ribWas", value); }

    static string RunInBackgroundLine => RunInBackgroundHeld
        ? $"Run In Background is on for this run (his setting, {(RunInBackgroundWas ? "on" : "off")}, goes back when it ends), so Play Mode keeps going with another app in front."
        : "";

    static void HoldRunInBackground()
    {
        if (!RunInBackgroundHeld)
        {
            RunInBackgroundWas = PlayerSettings.runInBackground;
            RunInBackgroundHeld = true;
        }
        PlayerSettings.runInBackground = true;
        Debug.Log(Tag + RunInBackgroundLine);
    }

    static void ReleaseRunInBackground()
    {
        if (!RunInBackgroundHeld) return;
        bool was = RunInBackgroundWas;
        PlayerSettings.runInBackground = was;
        RunInBackgroundHeld = false;
        // 26 Oct 2026 (his Mac): written to ProjectSettings.asset at once. A step that saved assets during the run had written
        // "on" there, and the next reimport read it back, so the following run took "on" for his setting.
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + $"Run In Background is back to his setting ({(was ? "on" : "off")}).");
    }

    // 26 Oct 2026 (his Mac): his project ships with Run In Background off (ProjectSettings runInBackground: 0). This puts it
    // back and saves it, for when a run's hold was ever left behind. Refused while a queue runs.
    [MenuItem(Menu + "Run In Background back to off (his project setting)")]
    static void RunInBackgroundOff()
    {
        if (State != Phase.Idle) { Debug.LogWarning(Tag + "A queue is running: it puts his setting back itself when it ends."); return; }
        RunInBackgroundHeld = false;
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + "Run In Background is off again (his project setting), saved to ProjectSettings.");
    }

    // ---------- one entry of the queue ----------

    sealed class Entry
    {
        public string Kind, MenuPath, Ready, CheckMenu, End;
        public int Timeout;
        public string Name => Last(Kind == "session" ? CheckMenu : Kind == "reimport" ? ReimportMenu : MenuPath)
                              + (Kind == "session" ? " (in " + Last(MenuPath) + ")" : "");
    }

    static string Last(string path) { int s = path.LastIndexOf('/'); return s < 0 ? path : path.Substring(s + 1); }

    // Returns null and an error for a line that doesn't fit the format, so a bad file is caught before a 3-hour run.
    static Entry Parse(string line, out string error)
    {
        error = null;
        string[] f = line.Split(new[] { " | " }, StringSplitOptions.None);
        for (int i = 0; i < f.Length; i++) f[i] = f[i].Trim();
        string kind = f[0].ToLowerInvariant();
        var e = new Entry { Kind = kind };
        int timeoutField;
        switch (kind)
        {
            case "reimport":
                if (f.Length != 1) { error = "reimport takes no fields"; return null; }
                e.Timeout = ReimportTimeout;
                return e;
            case "edit":
            case "play":
                if (f.Length != 4) { error = kind + " needs: " + kind + " | <menu path> | <end regex> | <timeout s>"; return null; }
                e.MenuPath = f[1]; e.End = f[2]; timeoutField = 3;
                break;
            case "session":
                if (f.Length != 6) { error = "session needs: session | <session menu> | <ready regex> | <check menu> | <end regex> | <timeout s>"; return null; }
                e.MenuPath = f[1]; e.Ready = f[2]; e.CheckMenu = f[3]; e.End = f[4]; timeoutField = 5;
                break;
            default:
                error = "unknown kind \"" + f[0] + "\" (reimport, edit, play or session)";
                return null;
        }
        if (!int.TryParse(f[timeoutField], NumberStyles.Integer, CultureInfo.InvariantCulture, out e.Timeout) || e.Timeout <= 0)
        { error = "the timeout \"" + f[timeoutField] + "\" isn't a whole number of seconds"; return null; }
        try
        {
            _ = new Regex(e.End);
            if (e.Ready != null) _ = new Regex(e.Ready);
        }
        catch (ArgumentException x) { error = "a regex doesn't parse: " + x.Message; return null; }
        return e;
    }

    static string[] StoredLines => Lines.Length == 0 ? Array.Empty<string>() : Lines.Split('\n');

    static Entry Current
    {
        get
        {
            string[] lines = StoredLines;
            int i = Index;
            return i >= 0 && i < lines.Length ? Parse(lines[i], out _) : null;
        }
    }

    // ---------- the menus ----------

    // ---------- 26 Oct 2026 (his Mac, screen locked): a run asked for by a file ----------
    // With his screen locked nothing can press a menu, but the editor keeps ticking. While the queue is idle it looks every
    // 5 s for Logs/LabQueue/run-request.txt; when there is one, it takes it (renamed, kept as the record of who asked when)
    // and runs queue.txt exactly as the menu does. Nothing else reads the file; it starts only what queue.txt lists.
    const string RequestName = "run-request.txt";
    static double nextRequestLook;

    static void LookForARequest()
    {
        if (EditorApplication.timeSinceStartup < nextRequestLook) return;
        nextRequestLook = EditorApplication.timeSinceStartup + 5.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string request = Path.Combine(Folder, RequestName);
        if (!File.Exists(request)) return;
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        // A request older than half an hour is stale (asked for while this code wasn't compiled yet, say): it never starts a run
        // under him when he comes back to his Mac.
        if ((DateTime.Now - File.GetLastWriteTime(request)).TotalMinutes > 30.0)
        {
            try { File.Move(request, Path.Combine(Folder, "run-request-stale-" + stamp + ".txt")); } catch (Exception) { }
            Debug.LogWarning(Tag + "A run request older than 30 minutes was put aside, not run (" + RequestName + ").");
            return;
        }
        string taken = Path.Combine(Folder, "run-request-taken-" + stamp + ".txt");
        try { File.Move(request, taken); }
        catch (Exception ex) { Debug.LogWarning(Tag + "A run request was found but couldn't be taken: " + ex.Message); return; }
        Debug.Log(Tag + "A run was asked for by " + RequestName + " (taken as " + Path.GetFileName(taken) + "): running queue.txt.");
        Run();
    }

    [MenuItem(Menu + "Run the queue (Logs › LabQueue › queue.txt)")]
    static void Run()
    {
        if (State != Phase.Idle) { Debug.LogWarning(Tag + "Already running (entry " + (Index + 1) + "). Use Status, or Stop after the current entry."); return; }
        string path = DefaultQueue;
        if (!File.Exists(path)) { Debug.LogError(Tag + "No queue file at " + path + ". Write the full run first, or write your own."); return; }

        var kept = new List<string>();
        var errors = new StringBuilder();
        string[] raw = File.ReadAllLines(path, Encoding.UTF8);
        for (int n = 0; n < raw.Length; n++)
        {
            string line = raw[n].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            if (Parse(line, out string error) == null) errors.AppendLine($"  line {n + 1}: {error}\n    {line}");
            else kept.Add(line);
        }
        if (errors.Length > 0) { Debug.LogError(Tag + "The queue file has lines it can't read, so nothing ran:\n" + errors); return; }
        if (kept.Count == 0) { Debug.LogError(Tag + "The queue file has no entries: " + path); return; }

        for (int i = 0; i < kept.Count + 64; i++) SessionState.EraseString(ResultKey(i));
        Lines = string.Join("\n", kept);
        QueuePath = path;
        Index = 0;
        StopAsked = false;
        StopWhy = "";
        Directory.CreateDirectory(Folder);
        SummaryPath = Path.Combine(Folder, "run-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        Mark("runStart");
        SetTicks("exitAsked", 0);
        SetTicks("quietSince", 0);
        HoldRunInBackground();   // 26 Oct 2026 (Mac run fix)
        State = Phase.Settle;
        WriteSummary();
        Debug.Log(Tag + $"Started: {kept.Count} entries from {path}. Summary: {SummaryPath}");
    }

    [MenuItem(Menu + "Write the full run into Logs › LabQueue › queue.txt")]
    static void WriteFullRun()
    {
        Directory.CreateDirectory(Folder);
        string path = DefaultQueue;
        // Keep a hand-made queue rather than lose it: there's no dialog, because the integrator presses this remotely.
        if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) != FullRun)
        {
            string backup = Path.Combine(Folder, "queue-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".bak.txt");
            File.Copy(path, backup, true);
            Debug.Log(Tag + "The old queue file is kept as " + backup);
        }
        File.WriteAllText(path, FullRun, new UTF8Encoding(false)); // UTF-8: the Café life menu has an "é"
        Debug.Log(Tag + "Wrote the full run (" + CountEntries(FullRun) + " entries) to " + path);
    }

    [MenuItem(Menu + "Stop after the current entry")]
    static void Stop()
    {
        if (State == Phase.Idle) { Debug.Log(Tag + "Nothing is running."); return; }
        StopAsked = true;
        StopWhy = "stopped by hand";
        Debug.Log(Tag + "Will stop after entry " + (Index + 1) + ".");
    }

    [MenuItem(Menu + "Status (what's running)")]
    static void Status()
    {
        if (State == Phase.Idle)
        {
            Debug.Log(Tag + "Not running." + (SummaryPath.Length > 0 ? " The last summary: " + SummaryPath : ""));
            return;
        }
        Entry e = Current;
        Debug.Log(Tag + $"Running entry {Index + 1} of {StoredLines.Length}: {e?.Kind} {e?.Name}, {State}, " +
                  $"{Since("entryStart"):0} s in (timeout {e?.Timeout} s), the whole run {Since("runStart") / 60:0} min. " +
                  $"Exceptions this entry: {Exceptions}. Last line: {LastLine}. Summary: {SummaryPath}" +
                  (StopAsked ? " (stopping after this entry)" : ""));
    }

    // ---------- the console listener: live in both Edit and Play Mode ----------

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        Phase phase = State;
        if (phase == Phase.Idle || phase == Phase.Settle) return;
        if (condition == null || condition.StartsWith(Tag, StringComparison.Ordinal)) return;
        string line = OneLine(condition);
        LastLine = line;

        if (type == LogType.Exception)
        {
            if (Exceptions == 0) FirstException = line;
            Exceptions = Exceptions + 1;
        }

        Entry e = Current;
        if (e == null) return;
        try
        {
            if (phase == Phase.WaitReady && EditorApplication.isPlaying && Regex.IsMatch(condition, e.Ready))
            {
                // A session's ready line counts only once Play Mode has started; the editor's own "Lab session" line
                // just before entering Play Mode doesn't.
                Mark("readyAt");
                SetTicks("lastPress", 0);
                State = Phase.PressCheck;
            }
            else if (phase == Phase.WaitEnd && EndLine.Length == 0 && Regex.IsMatch(condition, e.End))
            {
                // Written now, in this domain: the lab may end Play Mode (and reload the scripts) a moment later.
                EndLine = line;
            }
        }
        catch (Exception) { /* a regex timing out or failing must never break the console */ }
    }

    static string OneLine(string s)
    {
        int nl = s.IndexOfAny(new[] { '\n', '\r' });
        if (nl >= 0) s = s.Substring(0, nl);
        s = s.Replace('\t', ' ');
        return s.Length > 400 ? s.Substring(0, 400) + "…" : s;
    }

    // ---------- the driver ----------

    static void Tick()
    {
        Phase phase = State;
        if (phase == Phase.Idle) { LookForARequest(); return; }   // 26 Oct 2026: a run asked for by a file (see the header)
        Entry e = Current;
        if (e == null) { Finish(); return; }

        try
        {
            switch (phase)
            {
                case Phase.Settle: Settle(); return;
                case Phase.Begin: Begin(e); return;
            }

            // An entry is running: the timeout first.
            if (Since("entryStart") > e.Timeout)
            {
                string why = phase == Phase.WaitReady ? "the ready line never came" : phase == Phase.PressCheck ? "the check never ran" : "no end line";
                Record(e, "TIMEOUT", $"{why}; last line: {LastLine}");
                return;
            }

            switch (phase)
            {
                case Phase.WaitEnd:
                    if (EndLine.Length > 0)
                    {
                        string end = EndLine;
                        bool pass = Regex.IsMatch(end, PassPattern) && end.IndexOf("FAIL", StringComparison.Ordinal) < 0;
                        Record(e, pass ? "PASS" : "FAIL", end);
                    }
                    return;
                case Phase.WaitReady:
                    return; // the listener moves it on
                case Phase.PressCheck:
                    PressCheck(e);
                    return;
                case Phase.ReimportQuietWait:
                    ReimportWait(e);
                    return;
            }
        }
        catch (Exception x)
        {
            // The queue's own fault must not leave it stuck: record it and go on.
            Debug.LogWarning(Tag + "The queue hit an error on entry " + (Index + 1) + ": " + x.Message);
            if (State != Phase.Settle && State != Phase.Idle) Record(e, "FAIL", "the queue's own error: " + OneLine(x.Message));
        }
    }

    // Between entries: out of Play Mode (only if still playing), nothing compiling, then 2 s.
    static void Settle()
    {
        if (EditorApplication.isPlaying)
        {
            long asked = Ticks("exitAsked");
            if (asked == 0 || Since("exitAsked") > ExitRetrySeconds)
            {
                Mark("exitAsked");
                EditorApplication.ExitPlaymode();
            }
            Mark("quietSince");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Mark("quietSince");
            return;
        }
        if (Ticks("quietSince") == 0) Mark("quietSince");
        if (Since("quietSince") < SettleSeconds) return;
        SetTicks("exitAsked", 0);
        SetTicks("quietSince", 0);
        State = Phase.Begin;
    }

    static void Begin(Entry e)
    {
        EndLine = "";
        LastLine = "";
        Exceptions = 0;
        FirstException = "";
        Mark("entryStart");
        Debug.Log(Tag + $"Entry {Index + 1} of {StoredLines.Length}: {e.Kind} {e.Name}");

        // The phase is set before the menu runs: an edit lab logs its end line inside ExecuteMenuItem itself.
        switch (e.Kind)
        {
            case "reimport":
                State = Phase.ReimportQuietWait;
                SetTicks("quietSince", 0);
                if (!EditorApplication.ExecuteMenuItem(ReimportMenu)) Record(e, "MISSING", "no menu: " + ReimportMenu);
                return;
            case "edit":
            case "play":
                State = Phase.WaitEnd;
                if (!EditorApplication.ExecuteMenuItem(e.MenuPath)) Record(e, "MISSING", "the menu doesn't exist or is disabled: " + e.MenuPath);
                return;
            case "session":
                State = Phase.WaitReady;
                if (!EditorApplication.ExecuteMenuItem(e.MenuPath)) Record(e, "MISSING", "the session menu doesn't exist or is disabled: " + e.MenuPath);
                return;
        }
    }

    // A session's ready line has come: press the check's menu. Its validator wants Play Mode (and sometimes the lab
    // director awake), so a disabled answer is tried again each second for a while before it counts as MISSING.
    static void PressCheck(Entry e)
    {
        if (Since("readyAt") < SettleSeconds) return;
        if (Ticks("lastPress") != 0 && Since("lastPress") < 1) return;
        Mark("lastPress");
        State = Phase.WaitEnd; // before the menu runs, so an end line logged inside it is caught
        if (EditorApplication.isPlaying && EditorApplication.ExecuteMenuItem(e.CheckMenu)) return;
        State = Phase.PressCheck;
        if (Since("readyAt") > CheckMenuGrace)
            Record(e, "MISSING", "the check's menu doesn't exist or stayed disabled: " + e.CheckMenu);
    }

    // After the reimport: wait for 10 s in a row of no compiling or updating, then read the newest compile result.
    static void ReimportWait(Entry e)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Mark("quietSince");
            return;
        }
        if (Ticks("quietSince") == 0) Mark("quietSince");
        if (Since("quietSince") < ReimportQuiet) return;

        string file = Path.Combine(ProjectRoot, "Logs", "Compile", "last.txt");
        string first = "";
        try { if (File.Exists(file)) using (var r = new StreamReader(file, Encoding.UTF8)) first = r.ReadLine() ?? ""; }
        catch (IOException) { }
        if (first.Length == 0)
        {
            Record(e, EditorUtility.scriptCompilationFailed ? "FAIL" : "PASS",
                   "no Logs/Compile/last.txt; the editor says scripts " + (EditorUtility.scriptCompilationFailed ? "failed to compile" : "compiled"));
            if (EditorUtility.scriptCompilationFailed) StopRun("the scripts don't compile");
            return;
        }
        Match m = Regex.Match(first, @"(\d+) error\(s\)");
        int errors = m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        bool failed = errors > 0 || EditorUtility.scriptCompilationFailed;
        Record(e, failed ? "FAIL" : "PASS", first);
        // Labs on code that doesn't compile would test the old build: stop the queue.
        if (failed) StopRun("the compile has errors (Logs/Compile/last.txt)");
    }

    static void StopRun(string why)
    {
        StopAsked = true;
        StopWhy = why;
        Debug.LogError(Tag + "Stopping the queue: " + why + ".");
    }

    static void Record(Entry e, string result, string endLine)
    {
        double seconds = Since("entryStart");
        string row = string.Join("\t", e.Kind, result, seconds.ToString("0.0", CultureInfo.InvariantCulture),
                                 Exceptions.ToString(CultureInfo.InvariantCulture), OneLine(endLine ?? ""), FirstException);
        SessionState.SetString(ResultKey(Index), row);
        Debug.Log(Tag + $"Entry {Index + 1}: {result} in {Duration(seconds)}. {e.Name}: {OneLine(endLine ?? "")}");

        Index = Index + 1;
        EndLine = "";
        SetTicks("quietSince", 0);
        SetTicks("exitAsked", 0);
        WriteSummary();
        if (StopAsked || Index >= StoredLines.Length) Finish();
        else State = Phase.Settle;
    }

    static void Finish()
    {
        State = Phase.Idle;
        // Leave the editor as it was found: in Edit Mode.
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        Totals(out int n, out int p, out int f, out int t, out int m);
        WriteSummary();
        ReleaseRunInBackground();   // 26 Oct 2026 (Mac run fix): after the summary, which still says it was held for the run
        Debug.Log(Tag + $"Done: {n} entries, {p} passed, {f} failed, {t} timed out, {m} missing. {SummaryPath}");
    }

    // ---------- the summary ----------

    static void Totals(out int n, out int p, out int f, out int t, out int m)
    {
        n = p = f = t = m = 0;
        int count = StoredLines.Length;
        for (int i = 0; i < count; i++)
        {
            string row = SessionState.GetString(ResultKey(i), "");
            if (row.Length == 0) continue;
            n++;
            string r = row.Split('\t')[1];
            if (r == "PASS") p++; else if (r == "FAIL") f++; else if (r == "TIMEOUT") t++; else m++;
        }
    }

    static void WriteSummary()
    {
        if (SummaryPath.Length == 0) return;
        try
        {
            string[] lines = StoredLines;
            var sb = new StringBuilder();
            sb.AppendLine($"Lab queue run started {new DateTime(Ticks("runStart"), DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm:ss}, " +
                          $"{lines.Length} entries from {QueuePath}. Written {DateTime.Now:HH:mm:ss}.");
            sb.AppendLine();
            for (int i = 0; i < lines.Length; i++)
            {
                Entry e = Parse(lines[i], out _);
                string name = e?.Name ?? lines[i];
                string row = SessionState.GetString(ResultKey(i), "");
                if (row.Length == 0)
                {
                    sb.AppendLine($"{i + 1,3}. {name}  —  {(State != Phase.Idle && i == Index ? "running" : "not run")}");
                    continue;
                }
                string[] c = row.Split('\t');
                double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s);
                sb.Append($"{i + 1,3}. {name}  —  {c[1]}  —  {c[4]}  —  {Duration(s)}");
                if (c[3] != "0") sb.Append($"  —  {c[3]} exception(s), the first: {c[5]}");
                sb.AppendLine();
            }
            Totals(out int n, out int p, out int f, out int t, out int m);
            sb.AppendLine();
            sb.AppendLine($"Totals: {n} entries, {p} passed, {f} failed, {t} timed out, {m} missing" +
                          (n < lines.Length ? $" ({lines.Length - n} not run)" : "") + $". The run so far: {Duration(Since("runStart"))}.");
            if (StopAsked && StopWhy.Length > 0) sb.AppendLine("Stopped early: " + StopWhy + ".");
            if (RunInBackgroundHeld) sb.AppendLine(RunInBackgroundLine);   // 26 Oct 2026 (Mac run fix)
            Directory.CreateDirectory(Folder);
            File.WriteAllText(SummaryPath, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception x)
        {
            Debug.LogWarning(Tag + "Couldn't write the summary: " + x.Message);
        }
    }

    static string Duration(double s) =>
        s < 60 ? s.ToString("0.0", CultureInfo.InvariantCulture) + " s"
               : $"{(int)(s / 60)} min {(int)(s % 60):00} s";

    static int CountEntries(string text)
    {
        int n = 0;
        foreach (string l in text.Split('\n'))
        {
            string t = l.Trim();
            if (t.Length > 0 && !t.StartsWith("#", StringComparison.Ordinal)) n++;
        }
        return n;
    }

    // ---------- the default full run (every menu path checked against the [MenuItem] strings, 26 Oct 2026) ----------

    const string FullRun =
@"# The full lab run. One entry per line; # starts a comment; fields split by "" | "".
#   reimport
#   edit    | <menu path> | <end regex> | <timeout s>
#   play    | <menu path> | <end regex> | <timeout s>
#   session | <session menu path> | <ready regex> | <check menu path> | <end regex> | <timeout s>
reimport
edit | Fixit Fidget/Checks/Night notebook rules | \[Notebook rules\] (PASS|FAIL) | 120
edit | Fixit Fidget/Checks/Seat choice rules | \[Seat choice rules\] (All \d+ checks passed|.*FAILED) | 120
edit | Fixit Fidget/Checks/Waiting space reservations | \[Waiting space\] The café scene: \d+ passed, \d+ failed | 120
edit | Fixit Fidget/Playtest 4/UI/2 - Settings: the bench's sticks and the morning (edit mode, instant) | \[Playtest 4\] UI - settings: (PASS|FAIL) | 60
edit | Fixit Fidget/Checks/Probe - the bench's hand-over reach (edit mode, read-only) | \[Bench hand-over probe\] done | 120
play | Fixit Fidget/Playtest/Aim help - play check (lab, drives itself) | \[Aim help check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/Stations as reach - play check, first person (lab, drives itself) | \[Stations check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/Stations as reach - play check, overhead (lab, drives itself) | \[Stations check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest 4/UI/1 - Day 1 guide - play check (lab, drives itself) | \[Day 1 guide check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Playtest/Juice - Photograph the feedback (lab, Play Mode) | \[Juice check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest 4/Bench/2 - The dispenser's frame - play check (lab, Play Mode) | \[Dispenser frame\] .*checks (passed|FAILED) | 300
play | Fixit Fidget/Bench/Bench 3 - The bench - play check (lab, drives itself) | \[Bench check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Bench/Bench 5 - The close-up's view (lab, a check) | \[Bench view check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Bench/Bench 6 - Words on the bench screen (lab, a check) | \[Bench words lab\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Bench/Bench 8 - The clumsy hand (lab, drives itself) | \[Clumsy hand\] .*checks (passed|FAILED) | 1800
play | Fixit Fidget/Recap phone/Play check (lab, drives itself) | \[Recap phone check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/The phone by day - pause and HUD check (lab, drives itself) | \[Pause check\] .*checks (passed|FAILED) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 3, autopilot serves | \[Café lab\] Lab session | Fixit Fidget/Checks/Night notebook (Play Mode, lab session) | \[Notebook page\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Night/Night 0 - Play check (lab, drives itself) | \[Night 0 check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Night/Night 1 - Play check, keeping a straight face (lab, drives itself) | \[Night 1 check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
session | Fixit Fidget/NPC/Life 4 0 - Play its lab session (Day 5, I serve) | \[Café lab\] Lab session | Fixit Fidget/NPC/Life 4 - Stools, settled guests, looks (Play Mode, Life 4's lab session) | Life 4 - Stools, settled guests, looks: (PASS|FAIL) | 900
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/NPC/Sit 4 - Live sit check (Play Mode) | \[Sit check\] (PASS|FAIL) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/NPC/Café life check (Play Mode) | \[Café life check\] (PASS|FAIL) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/Checks/Controller (Play Mode)/Run controller check | \[Controller check\] (PASS|FAIL) | 600
play | Fixit Fidget/Checks/Hands on it - play check (lab, drives itself) | \[Hands on check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Dialogue/Conversation - play check (lab, drives itself) | \[Conversation check\] .*checks (passed|FAILED) | 600
edit | Fixit Fidget/Review/Screens 1 - Gather the newest run of every lab's word checks (edit mode, read-only) | \[Review screens\] | 300
";
}
#endif
