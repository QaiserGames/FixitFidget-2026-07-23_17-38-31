#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// REVIEW 2 - THE WORD CHECKS OF EVERY LAB PHOTO, GATHERED (his girlfriend's playtest, 19 Oct 2026)
//
//   Fixit Fidget › Review › Screens 1 - Gather the newest run of every lab's word checks (edit mode, read-only)
//   Fixit Fidget › Review › Screens 2 - Photograph the Game view now and check its words (Play mode)
//   Fixit Fidget › Review › Screens 3 - Gather every run of the last three days, the history (edit mode, read-only)
//
// Every lab photo now measures the words on the screen at its instant (ReviewScreen; <name>.text.json beside each photo).
// Screens 1 reads every one of those files under Logs/ written in the last three days and turns them into one list a
// person can act on: the same words seen in many photos are one finding (she saw the clock too small in every photo of
// the day, not once per photo), ranked S1 → S3 and then by how many photos show it, each with who draws it and up to five
// photos to open (the .text.jpg, with the box drawn on). It writes Logs/Review/text-<time>/report.txt and findings.csv
// and changes nothing in the project, the scene or the Logs it reads. Screens 2 photographs the Game view in Play mode
// for a sweep by hand (ReviewScreen.SnapNow), into Logs/Review/screens-<date>/, where Screens 1 picks it up too.
//
// 20 OCT 2026 (the first full gather: 543 photos, 93 S1s, most of them not the UI's). Three changes so the list is the
// build as it stands and every line on it is worth opening:
//   * the newest run of each lab only (Screens 1): a lab run before and after a fix is one lab, and the fixed run is the
//     one that counts. A folder's lab is its path without the date and time ("Logs/Recap/recap-phone-check-"). Screens 3
//     keeps the old way, every run of the last three days, for the history;
//   * words in the world (street names, house numbers, the drinks machine's paddles, a sign by the kerb) are listed on
//     their own, not graded with the UI: how big they are is the camera's distance, not a font size anyone chose. What
//     matters there is whether a word the player is sent to look for ever reads: each is listed with the largest it was
//     seen, and one never seen at 18 px or more is called out;
//   * a list's rows scrolled out of its window are no longer measured (ReviewScreen skips what a RectMask2D culls).
// ---------------------------------------------------------------------------
static class ReviewScreenGather
{
    const string Tag = "[Review screens]";
    const string GatherMenu = "Fixit Fidget/Review/Screens 1 - Gather the newest run of every lab's word checks (edit mode, read-only)";
    const string SnapMenu = "Fixit Fidget/Review/Screens 2 - Photograph the Game view now and check its words (Play mode)";
    const string HistoryMenu = "Fixit Fidget/Review/Screens 3 - Gather every run of the last three days, the history (edit mode, read-only)";
    const float ReadablePx = 18f; // a word in the world the player must read should reach this somewhere (ReviewScreen.TinyPx)
    // A run folder's date and time: taken out, what is left names the lab ("recap-phone-check-2026-10-20_015041").
    static readonly Regex Stamp = new Regex(@"\d{4}-\d{2}-\d{2}(?:[_ ]\d{4,6})?", RegexOptions.CultureInvariant);
    const int Days = 3;           // photos older than this are another build's
    const int PhotosListed = 5;   // photo paths per finding in the report

    // The same words, wherever they were seen.
    sealed class Finding
    {
        public string text = "";
        public int severity;                      // re-graded from the worst measures below (1 worst; 0 never flagged)
        public string why = "";
        public float px = float.MaxValue;         // the smallest it was seen (one em at 1080p)
        public float contrast = float.MaxValue;   // the lowest known contrast it was seen at
        // Every sighting's size and contrast, for the typical (median) value beside the worst: one photo of a sign
        // mid-flip or a card half faded gives a text its worst number, and a reader must see that it is the exception.
        public readonly List<float> sizes = new List<float>(), contrasts = new List<float>();
        public bool thin, outlined, lowConfidence;
        public string buttonWords = "";
        public int words;
        public readonly SortedSet<string> owners = new SortedSet<string>(StringComparer.Ordinal);
        public readonly List<string> photos = new List<string>();   // marked photos where it was flagged, up to PhotosListed
        public int seen, flagged;                 // photos it appeared in; photos that flagged it
        public int uguiSeen, worldSeen;           // sightings on a canvas (the UI) / drawn in the scene (a sign, a label)
        public float pxMax;                       // the largest it was seen
        public bool InTheWorld => uguiSeen == 0 && worldSeen > 0;
    }

    // ------------------------------------------------------------------ Screens 1

    [MenuItem(GatherMenu)]
    static void GatherNewest() => Gather(true);

    [MenuItem(HistoryMenu)]
    static void GatherHistory() => Gather(false);

    static void Gather(bool newestOnly)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError($"{Tag} Stop Play mode first: this gathers in Edit mode (Screens 2 is the one for Play mode).");
            return;
        }
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string logs = Path.Combine(project, "Logs");
        if (!Directory.Exists(logs))
        {
            Debug.LogWarning($"{Tag} No Logs folder at {logs}: run a lab first.");
            return;
        }

        // Every photo's check from the last three days, oldest folder first (so the report's photo lists read in order).
        DateTime since = DateTime.Now.AddDays(-Days);
        var files = new List<string>();
        try
        {
            foreach (string f in Directory.EnumerateFiles(logs, "*.text.json", SearchOption.AllDirectories))
                if (File.GetLastWriteTime(f) >= since) files.Add(f);
        }
        catch (Exception e) { Debug.LogWarning($"{Tag} Part of Logs/ couldn't be read ({e.Message}); gathering what was found."); }
        if (files.Count == 0)
        {
            Debug.LogWarning($"{Tag} No *.text.json under Logs/ from the last {Days} days: run a lab (or Screens 2 in Play mode) first.");
            return;
        }
        files.Sort(StringComparer.Ordinal);

        // The newest run of each lab: every folder's latest file time, then per lab the folder with the latest.
        int runsLeftOut = 0, labs = 0;
        if (newestOnly)
        {
            var folderTime = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            foreach (string f in files)
            {
                string folder = Path.GetDirectoryName(f) ?? "";
                DateTime when = File.GetLastWriteTime(f);
                if (!folderTime.TryGetValue(folder, out DateTime t) || when > t) folderTime[folder] = when;
            }
            var newest = new Dictionary<string, string>(StringComparer.Ordinal);   // lab -> its newest folder
            foreach (var pair in folderTime)
            {
                string lab = LabOf(pair.Key, project);
                if (!newest.TryGetValue(lab, out string best) || pair.Value > folderTime[best]) newest[lab] = pair.Key;
            }
            var keep = new HashSet<string>(newest.Values, StringComparer.Ordinal);
            labs = newest.Count;
            runsLeftOut = folderTime.Count - keep.Count;
            files = files.Where(f => keep.Contains(Path.GetDirectoryName(f) ?? "")).ToList();
        }

        var findings = new Dictionary<string, Finding>(StringComparer.Ordinal);
        var folders = new HashSet<string>(StringComparer.Ordinal);
        int photos = 0, unreadable = 0, sightings = 0;
        DateTime earliest = DateTime.MaxValue, latest = DateTime.MinValue;
        foreach (string file in files)
        {
            ReviewScreen.PhotoCheck photo = null;
            try { photo = JsonUtility.FromJson<ReviewScreen.PhotoCheck>(File.ReadAllText(file)); }
            catch (Exception) { }
            if (photo == null || photo.items == null) { unreadable++; continue; }
            photos++;
            string folder = Path.GetDirectoryName(file) ?? logs;
            folders.Add(folder);
            DateTime when = File.GetLastWriteTime(file);
            if (when < earliest) earliest = when;
            if (when > latest) latest = when;
            string marked = Path.Combine(folder, photo.name + ".text.jpg");
            if (!File.Exists(marked)) marked = Path.Combine(folder, photo.name + ".jpg");
            marked = Relative(marked, project);
            foreach (ReviewScreen.TextMeasure m in photo.items)
            {
                if (m == null || string.IsNullOrEmpty(m.text)) continue;
                sightings++;
                // The same words on the UI and in the world are two findings ("12" on the phone's Shop and over Grace's door).
                string key = (m.kind == "world" ? "world:" : "ui:") + m.text;
                if (!findings.TryGetValue(key, out Finding f)) findings[key] = f = new Finding { text = m.text, words = m.words };
                f.seen++;
                if (m.kind == "world") f.worldSeen++; else f.uguiSeen++;
                if (m.severity > 0) f.flagged++;
                if (m.px1080 > 0f) { f.sizes.Add(m.px1080); if (m.px1080 < f.px) f.px = m.px1080; if (m.px1080 > f.pxMax) f.pxMax = m.px1080; }
                if (m.backgroundKnown && m.contrast > 0f) { f.contrasts.Add(m.contrast); if (m.contrast < f.contrast) f.contrast = m.contrast; }
                f.thin |= m.thin;
                f.outlined |= m.outlined;
                if (m.words > f.words) f.words = m.words;
                if (!string.IsNullOrEmpty(m.buttonWords)) { f.buttonWords = m.buttonWords; f.lowConfidence = m.buttonLowConfidence; }
                f.owners.Add(!string.IsNullOrEmpty(m.owner) ? m.owner : "no script above it: " + (!string.IsNullOrEmpty(m.root) ? m.root : "?"));
                if (m.severity > 0 && f.photos.Count < PhotosListed && !f.photos.Contains(marked)) f.photos.Add(marked);
            }
        }

        // One grade per finding, from its worst measures, by the same rule every photo used.
        var all = findings.Values.ToList();
        foreach (Finding f in all)
        {
            var worst = new ReviewScreen.TextMeasure
            {
                text = f.text, words = f.words,
                px1080 = f.px < float.MaxValue ? f.px : 999f,
                contrast = f.contrast < float.MaxValue ? f.contrast : -1f, backgroundKnown = f.contrast < float.MaxValue,
                thin = f.thin, outlined = f.outlined, buttonWords = f.buttonWords, buttonLowConfidence = f.lowConfidence,
            };
            ReviewScreen.Grade(worst);
            f.severity = worst.severity;
            f.why = worst.reasons;
        }
        // Words in the world are listed on their own (the header): the UI is graded, the scene's words are measured.
        List<Finding> world = all.Where(f => f.InTheWorld).OrderBy(f => f.pxMax).ThenBy(f => f.text, StringComparer.Ordinal).ToList();
        List<Finding> ui = all.Where(f => !f.InTheWorld).ToList();
        List<Finding> flagged = ui.Where(f => f.severity > 0)
            .OrderBy(f => f.severity).ThenByDescending(f => f.flagged).ThenBy(f => f.text, StringComparer.Ordinal).ToList();
        List<Finding> fine = ui.Where(f => f.severity == 0).OrderBy(f => f.text, StringComparer.Ordinal).ToList();
        List<Finding> neverReadable = world.Where(f => f.pxMax < ReadablePx).ToList();
        int s1 = flagged.Count(f => f.severity == 1), s2 = flagged.Count(f => f.severity == 2), s3 = flagged.Count(f => f.severity == 3);

        // ---- report.txt ----
        var r = new StringBuilder();
        r.AppendLine("Review 2 - every lab photo checks its own words: gathered");
        r.AppendLine(Inv($"{DateTime.Now:yyyy-MM-dd HH:mm}; {photos} photos in {folders.Count} folders, written {earliest:yyyy-MM-dd HH:mm} to {latest:yyyy-MM-dd HH:mm} (the last {Days} days under Logs/)")
                     + (unreadable > 0 ? Inv($"; {unreadable} check files unreadable") : ""));
        r.AppendLine(newestOnly
            ? Inv($"The newest run of each of {labs} labs ({runsLeftOut} older run{(runsLeftOut == 1 ? "" : "s")} of the same labs left out: Screens 3 gathers every run).")
            : "Every run of the last three days, older runs of a lab beside its newest (the history; Screens 1 keeps the newest run of each).");
        r.AppendLine("A finding is one text (the same words wherever they were seen); its px is the smallest it was seen and its contrast the lowest known,");
        r.AppendLine("with the typical (median) value beside it when they differ: a worst number from one photo (a sign mid-flip, a card half faded) is the exception, not the text.");
        r.AppendLine("S1: under 18 px, or contrast under 3 with no outline. S2: under 26 px (XAG 101), contrast under 4.5 with no outline, or a pad button as a word. S3: thin and under 32 px, or over 25 words.");
        r.AppendLine();
        r.AppendLine(Inv($"Totals: {flagged.Count} texts flagged of {ui.Count} on the UI ({sightings} sightings in all): S1 {s1}, S2 {s2}, S3 {s3}; {fine.Count} fine."));
        r.AppendLine(Inv($"Words in the world (signs, labels, lines drawn in the scene): {world.Count}, listed at the end; {neverReadable.Count} never seen at {ReadablePx:0} px or more."));
        r.AppendLine("By owner script (texts flagged):");
        var byOwner = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (Finding f in flagged)
        {
            string owner = string.Join(" + ", f.owners);
            if (!byOwner.TryGetValue(owner, out int[] counts)) byOwner[owner] = counts = new int[4];
            counts[f.severity]++;
        }
        foreach (var pair in byOwner.OrderByDescending(p => p.Value[1]).ThenByDescending(p => p.Value[2]).ThenByDescending(p => p.Value[3]).ThenBy(p => p.Key, StringComparer.Ordinal))
            r.AppendLine(Inv($"  {pair.Key}: S1 {pair.Value[1]}, S2 {pair.Value[2]}, S3 {pair.Value[3]}"));
        r.AppendLine();
        r.AppendLine("The findings, worst first, then by how many photos show it:");
        r.AppendLine();
        foreach (Finding f in flagged)
        {
            r.AppendLine(Inv($"[S{f.severity}] {f.px:0} px{Typical(f.px, Median(f.sizes), "0", " px")}, contrast {ContrastText(f.contrast)}{Typical(f.contrast, Median(f.contrasts), "0.0", "")}, {(f.thin ? "thin" : "not thin")}, {(f.outlined ? "outlined" : "no outline")}, {f.words} word{(f.words == 1 ? "" : "s")}; flagged in {f.flagged} of {f.seen} photos"));
            r.AppendLine("     \"" + f.text + "\"");
            r.AppendLine("     drawn by " + string.Join(", ", f.owners));
            r.AppendLine("     why: " + f.why);
            foreach (string p in f.photos) r.AppendLine("     " + p);
            r.AppendLine();
        }
        r.AppendLine(Inv($"Fine, never flagged ({fine.Count} texts): in findings.csv with severity 0."));
        r.AppendLine();
        r.AppendLine(Inv($"Words in the world ({world.Count}): not graded with the UI, since the camera's distance decides their size. Each is listed"));
        r.AppendLine(Inv($"with the largest it was seen; one never seen at {ReadablePx:0} px or more can't be read in any of these photos, which matters"));
        r.AppendLine("only for a word the player is sent to look for (a house number, a street name, a drink's name at the machine).");
        r.AppendLine();
        foreach (Finding f in world)
        {
            r.AppendLine(Inv($"  {(f.pxMax < ReadablePx ? "never readable here" : "readable somewhere")}: largest {f.pxMax:0} px, smallest {f.px:0} px, in {f.seen} photo{(f.seen == 1 ? "" : "s")}  \"{f.text}\"  ({string.Join(", ", f.owners)})"));
        }

        // ---- findings.csv ----
        var c = new StringBuilder();
        c.AppendLine("severity,photosFlagged,photosSeen,px1080Smallest,px1080Typical,contrastLowest,contrastTypical,thin,outlined,buttonWords,lowConfidence,words,owners,text,why,photo,kind,px1080Largest");
        foreach (Finding f in flagged.Concat(fine).Concat(world))
            c.AppendLine(string.Join(",", new[]
            {
                f.severity.ToString(CultureInfo.InvariantCulture), f.flagged.ToString(CultureInfo.InvariantCulture), f.seen.ToString(CultureInfo.InvariantCulture),
                f.px < float.MaxValue ? f.px.ToString("0.0", CultureInfo.InvariantCulture) : "",
                f.sizes.Count > 0 ? Median(f.sizes).ToString("0.0", CultureInfo.InvariantCulture) : "",
                f.contrast < float.MaxValue ? f.contrast.ToString("0.00", CultureInfo.InvariantCulture) : "",
                f.contrasts.Count > 0 ? Median(f.contrasts).ToString("0.00", CultureInfo.InvariantCulture) : "",
                f.thin ? "1" : "0", f.outlined ? "1" : "0", Csv(f.buttonWords), f.lowConfidence ? "1" : "0",
                f.words.ToString(CultureInfo.InvariantCulture), Csv(string.Join(" + ", f.owners)), Csv(f.text), Csv(f.why),
                Csv(f.photos.Count > 0 ? f.photos[0] : ""),
                f.InTheWorld ? "world" : f.worldSeen > 0 ? "both" : "ui",
                f.pxMax.ToString("0.0", CultureInfo.InvariantCulture),
            }));

        string outFolder = Path.Combine(logs, "Review", "text-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(outFolder);
            File.WriteAllText(Path.Combine(outFolder, "report.txt"), r.ToString());
            File.WriteAllText(Path.Combine(outFolder, "findings.csv"), c.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} The report couldn't be written to {outFolder} ({e.Message}).");
            return;
        }
        Debug.Log($"{Tag} {flagged.Count} findings on the UI (S1 {s1}, S2 {s2}, S3 {s3}) and {neverReadable.Count} of {world.Count} words in the world never readable, from {photos} photos in {folders.Count} folders" +
                  (newestOnly ? $" (the newest run of {labs} labs). " : " (every run). ") + outFolder);
    }

    [MenuItem(GatherMenu, true)]
    static bool CanGather() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(HistoryMenu, true)]
    static bool CanGatherHistory() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // The lab a run folder belongs to: its path under the project with the date and time taken out.
    static string LabOf(string folder, string project) => Stamp.Replace(Relative(folder, project), "");

    // ------------------------------------------------------------------ Screens 2

    [MenuItem(SnapMenu)]
    static void Snap()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError($"{Tag} Enter Play mode first: there is nothing drawn to photograph in Edit mode.");
            return;
        }
        ReviewScreen.SnapNow("manual");
    }

    [MenuItem(SnapMenu, true)]
    static bool CanSnap() => EditorApplication.isPlaying;

    // ------------------------------------------------------------------ helpers

    static string ContrastText(float contrast) => contrast < float.MaxValue ? contrast.ToString("0.0", CultureInfo.InvariantCulture) : "?";

    // " (typically 30 px)" beside a worst number, only when the typical one differs enough to matter.
    static string Typical(float worst, float typical, string format, string unit)
    {
        if (worst >= float.MaxValue || float.IsNaN(typical) || Mathf.Abs(typical - worst) < .5f) return "";
        return " (typically " + typical.ToString(format, CultureInfo.InvariantCulture) + unit + ")";
    }

    static float Median(List<float> values)
    {
        if (values == null || values.Count == 0) return float.NaN;
        var sorted = new List<float>(values);
        sorted.Sort();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * .5f;
    }

    // Logs/... rather than the Mac's whole path, so the report reads the same on both machines.
    static string Relative(string path, string project)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length).Replace('\\', '/') : full;
    }

    // A CSV field: quoted, a quote doubled, so the words can hold commas, quotes and line breaks.
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
#endif
