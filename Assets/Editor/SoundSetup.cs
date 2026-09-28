using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: SET-UP AND CHECK (claude/sound-plan.md §4 and §9)
//
// Fixit Fidget > Sound:
//
//   Create or update the sound bank: makes Assets/Data/Resources/Sound bank.asset with every cue the
//     game names (SoundBank.Defaults), or adds the missing ones to the bank already there. A cue that's
//     already in the bank is never changed: its files, loudness and reach stay as chosen. Links
//     Assets/Audio/Game.mixer when it exists and the bank has no mixer yet.
//
//   Check the sound setup (read-only): the bank (cues with and without files, and the import settings
//     of the files it uses), the mixer's groups, the files under Assets/Audio/Licensed, the git-ignore
//     rule and the scene's listeners. In Play Mode also: where the ears are and how far the game's own
//     sound sources are from them, Ace's steps and what's underfoot, the soundscape, and every cue
//     asked for and heard so far. Report: Logs/Sound/sound-check-*.txt.
// ---------------------------------------------------------------------------
internal static class SoundSetup
{
    const string Menu = "Fixit Fidget/Sound/";
    public const string BankPath = "Assets/Data/Resources/Sound bank.asset";
    public const string MixerPath = "Assets/Audio/Game.mixer";
    public const string LicensedFolder = "Assets/Audio/Licensed";
    static readonly string[] BusNames = { "World", "Ace", "UI", "Ambience", "Outside", "Music" };

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    [MenuItem(Menu + "Create or update the sound bank")]
    static void CreateOrUpdate()
    {
        var report = new StringBuilder("Sound - create or update the sound bank\n\n");
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, Path.GetDirectoryName(BankPath)));
            bank = ScriptableObject.CreateInstance<SoundBank>();
            bank.cues = SoundBank.Defaults();
            AssetDatabase.CreateAsset(bank, BankPath);
            report.AppendLine($"Made '{BankPath}' with {bank.cues.Count} cues, none with files yet.");
        }
        else
        {
            Undo.RecordObject(bank, "Update the sound bank");
            int added = 0, notes = 0;
            foreach (SoundBank.Cue cue in SoundBank.Defaults())
            {
                SoundBank.Cue existing = bank.cues.FirstOrDefault(c => c != null && c.name == cue.name);
                if (existing == null) { bank.cues.Add(cue); added++; }
                else if (string.IsNullOrWhiteSpace(existing.when)) { existing.when = cue.when; notes++; }
            }
            report.AppendLine($"'{BankPath}': {added} cue(s) added and {notes} empty note(s) filled; the cues already there are unchanged. {bank.cues.Count} cues in all.");
        }
        if (bank.mixer == null)
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null) { bank.mixer = mixer; report.AppendLine($"Linked the mixer '{MixerPath}'."); }
            else report.AppendLine($"No mixer at '{MixerPath}' yet: until there is one, every sound plays straight to the speakers.");
        }
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        if (Application.isPlaying) report.AppendLine("(Play Mode: the running game keeps the bank it loaded; the change counts from the next Play.)");
        Write("sound-bank", report);
        Selection.activeObject = bank;
    }

    [MenuItem(Menu + "Check the sound setup (read-only)")]
    static void Check()
    {
        var report = new StringBuilder("Sound - check the sound setup (read-only)\n\n");
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null) report.AppendLine($"Bank: none at '{BankPath}'. Make it with Fixit Fidget > Sound > Create or update the sound bank.");
        else DescribeBank(bank, report);

        if (AssetDatabase.IsValidFolder(LicensedFolder))
            report.AppendLine($"'{LicensedFolder}': {AssetDatabase.FindAssets("t:AudioClip", new[] { LicensedFolder }).Length} sound file(s).");
        else report.AppendLine($"'{LicensedFolder}': not made yet (it comes with the first chosen files).");

        string ignore = Path.Combine(ProjectRoot, ".gitignore");
        bool ruled = File.Exists(ignore) && File.ReadAllLines(ignore).Any(line => line.Trim() == "/" + LicensedFolder + "/");
        report.AppendLine($".gitignore: {(ruled ? "keeps " + LicensedFolder + "/ out of git" : "NO rule for " + LicensedFolder + "/ yet")}.");

        // The rig's ears are never saved, and FindObjectsByType never lists such objects: they're
        // counted through the rig.
        ListenerRig rig = Application.isPlaying ? Object.FindAnyObjectByType<ListenerRig>() : null;
        AudioListener rigEars = rig != null ? rig.Ears : null;
        List<AudioListener> listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include).Where(l => l != rigEars).ToList();
        var named = new List<string>();
        if (rigEars != null) named.Add("the rig's ears" + (Hearing(rigEars) ? "" : " (not hearing)"));
        named.AddRange(listeners.Select(l => $"'{l.name}'{(Hearing(l) ? "" : " (resting)")}"));
        int hearing = listeners.Count(Hearing) + (Hearing(rigEars) ? 1 : 0);
        report.AppendLine($"Listeners: {hearing} hearing: {(named.Count > 0 ? string.Join(", ", named) : "none")}" +
                          (hearing == 1 ? "." : "  <- Unity hears through exactly one listener"));

        if (Application.isPlaying) DescribePlay(report);
        else report.AppendLine("\n(In Play Mode this also reports the ears, Ace's steps, the soundscape and every cue asked for.)");
        Write("sound-check", report);
    }

    static bool Hearing(AudioListener listener) => listener != null && listener.enabled && listener.gameObject.activeInHierarchy;

    static void DescribeBank(SoundBank bank, StringBuilder report)
    {
        List<SoundBank.Cue> cues = bank.cues.Where(c => c != null).ToList();
        int filled = cues.Count(c => c.HasClips);
        report.AppendLine($"Bank '{BankPath}': {cues.Count} cues, {filled} with files, {cues.Count - filled} silent.");
        foreach (SoundBank.Cue missing in SoundBank.Defaults().Where(d => bank.Find(d.name) == null))
            report.AppendLine($"  missing: '{missing.name}' (Create or update the sound bank adds it)");
        foreach (IGrouping<string, SoundBank.Cue> twice in cues.GroupBy(c => c.name).Where(g => g.Count() > 1))
            report.AppendLine($"  twice: '{twice.Key}' (only the first is used)");
        foreach (SoundBank.Cue cue in cues)
        {
            int files = cue.clips != null ? cue.clips.Count(c => c != null) : 0;
            report.AppendLine($"  {cue.name}: {(files > 0 ? files + " file(s)" : "silent")}, {cue.bus}, " +
                              $"{(cue.threeD ? $"placed, full to {cue.near:0.#} m, silent past {cue.far:0.#} m" : "flat")}, volume {cue.volume:0.00}");
            if (files == 0) continue;
            foreach (AudioClip clip in cue.clips.Where(c => c != null))
            {
                string path = AssetDatabase.GetAssetPath(clip);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                var advice = new List<string>();
                AudioClipLoadType load = importer != null ? importer.defaultSampleSettings.loadType : clip.loadType;
                if (cue.threeD && clip.channels > 1 && (importer == null || !importer.forceToMono)) advice.Add("placed but stereo: Force To Mono");
                if (clip.length > 30f && load != AudioClipLoadType.Streaming) advice.Add("a long bed: Streaming");
                else if (clip.length < 2.5f && load != AudioClipLoadType.DecompressOnLoad) advice.Add("a short one-shot: Decompress On Load");
                report.AppendLine($"      {path}: {clip.length:0.00} s, {clip.channels} ch, {clip.frequency} Hz, {load}" +
                                  (advice.Count > 0 ? "  <- " + string.Join("; ", advice) : ""));
            }
        }
        if (bank.mixer == null)
        {
            report.AppendLine($"Mixer: none linked (make '{MixerPath}', then Create or update the sound bank links it).");
            return;
        }
        AudioMixerGroup[] all = bank.mixer.FindMatchingGroups("Master");
        List<string> found = BusNames.Where(n => all.Any(g => g != null && g.name == n)).ToList();
        report.AppendLine($"Mixer '{AssetDatabase.GetAssetPath(bank.mixer)}': groups {string.Join(", ", all.Where(g => g != null).Select(g => g.name))}; " +
                          $"{found.Count} of the 6 the bank uses" + (found.Count < BusNames.Length ? $" (missing: {string.Join(", ", BusNames.Except(found))})" : "") + ".");
    }

    static void DescribePlay(StringBuilder report)
    {
        report.AppendLine();
        ListenerRig rig = Object.FindAnyObjectByType<ListenerRig>();
        report.AppendLine(rig != null ? rig.Describe() : "Ears: no ListenerRig (no CafeViewMode in this scene?).");
        Camera cam = Camera.main;
        if (rig != null)
        {
            // How far the game's own sources are from the ears now, and from the camera (where the
            // scene's listener used to be).
            Near("The drinks dispensers (the pour carries 4.5 m with its own file)",
                 Object.FindObjectsByType<BeveragePourAudio>().Select(p => p.transform.position), rig, cam, report);
            Near("The support line's phone (carries 16 m)",
                 Object.FindObjectsByType<HoldCallPresentation>().Select(p => p.transform.position), rig, cam, report);
        }
        AceFootsteps steps = Object.FindAnyObjectByType<AceFootsteps>();
        report.AppendLine(steps != null ? steps.Describe() : "Ace's steps: not running.");
        CafeSoundscape scape = CafeSoundscape.Instance;   // never saved, so FindAnyObjectByType can't see it
        report.AppendLine(scape != null ? scape.Describe() : "Soundscape: not running.");
        report.Append(SoundPlayer.Instance != null ? SoundPlayer.Instance.Describe() : "Sound player: not made yet (nothing has asked for a sound).\n");
    }

    static void Near(string what, IEnumerable<Vector3> points, ListenerRig rig, Camera cam, StringBuilder report)
    {
        List<Vector3> list = points.ToList();
        if (list.Count == 0) { report.AppendLine($"{what}: none in the scene."); return; }
        float toEars = list.Min(p => Vector3.Distance(p, rig.EarsPosition));
        float toCamera = cam != null ? list.Min(p => Vector3.Distance(p, cam.transform.position)) : -1f;
        report.AppendLine($"{what}: the nearest is {toEars:0.0} m from the ears, {toCamera:0.0} m from the camera.");
    }

    static void Write(string prefix, StringBuilder report)
    {
        string folder = Path.Combine(ProjectRoot, "Logs", "Sound");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"{prefix}-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(path, report.ToString());
        string first = report.ToString().Split('\n').Skip(2).FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        Debug.Log($"[Sound] {first.Trim()} ({path})");
    }
}
