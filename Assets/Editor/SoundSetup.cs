using System;
using System.Collections.Generic;
using System.Globalization;
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
//
// 14 Oct 2026, the first real sounds (playtest 5: "god the sounds are just terrible"; specs/audio-bench-sounds.md §6-§8):
// Audio's 96 CC0 files for 45 cues sit in Assets/Audio/CC0/ with its manifest.json and licences/ (CC0: committed, unlike
// the Sonniss files in Assets/Audio/Licensed/).
//
//   Sound bank 1 - Import the CC0 sounds and fill the bank: every cue the game names made sure of (as Create or update
//     does), the files' import settings (§6: mono, Decompress On Load, ADPCM; the tonal ones Vorbis 80, where ADPCM's hiss
//     shows on a pure tone; the 30 s room tone Compressed In Memory, Vorbis 60; preload on, normalize and load in
//     background off), then each of the manifest's cues given its files (in the manifest's order) and Audio's starting
//     numbers (volume, reach, gap, voices, wobble, 2D or 3D, bus). One undo; a second run says "nothing changed". It never
//     touches a cue the manifest doesn't name. Report: Logs/Sound/sound-bank-fill-*.txt.
//   Sound bank 2 - Check the bank against Audio's table (read-only): the "Sound bank" check of Audio §8 and the feel
//     target T18 ("every cue Audio supplied a file for plays the file; 0 synthesized placeholders on the bench, the till,
//     the drinks and the intro; every bench verb has a cue"). Report: Logs/Sound/sound-bank-check-*.txt.
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
                // a bed of 30 s or so may stay in memory compressed (Audio §6: the room tone); longer ones stream
                if (clip.length > 30.5f && load != AudioClipLoadType.Streaming && load != AudioClipLoadType.CompressedInMemory) advice.Add("a long bed: Streaming");
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

    // ================================================================== the CC0 files (Audio, 14 Oct 2026)

    public const string Cc0Folder = "Assets/Audio/CC0";
    const string Cc0Manifest = Cc0Folder + "/manifest.json";
    // Audio §6 step 4: Vorbis 80 for these, where ADPCM's hiss would show on a pure tone.
    static readonly HashSet<string> Tonal = new HashSet<string>(StringComparer.Ordinal)
    {
        "repair.fixed", "repair.ready", "door.bell", "day.open", "intro.sting", "grime.clean", "cloth.gleam", "juice.sparkle",
        "juice.sparkle.big", "phone.boot", "bench.showleft",
    };
    const string RoomTone = "cafe.room";     // §6 step 5: 30 s, Compressed In Memory, Vorbis 60

    // Audio's cue table (§3), exactly as written there: the names the bench, the checklist and the intro call.
    static readonly string[] AudioTable =
    {
        "screw.turn", "screw.free", "screw.fly", "screw.drop", "screw.seat", "screw.fetch", "screw.bounce",
        "cover.creak", "cover.pop", "cover.lift", "cover.clack", "part.on",
        "part.pinch", "part.lift", "part.drop", "part.fresh", "part.replace",
        "device.snap", "device.flip", "phone.boot", "watch.wind", "watch.tick", "camera.wind", "camera.shutter",
        "tool.pick", "tool.down", "brush.rub", "grime.scrub", "cloth.rub", "cloth.squeak", "cloth.lift", "cloth.gleam", "grime.clean",
        "juice.sparkle", "bench.showleft",
        "checklist.tick", "repair.fixed", "juice.sparkle.big", "repair.ready",
        "door.bell", "day.open", "money.paid", "cafe.room", "intro.sting", "intro.sign", "intro.lamp",
    };
    // Every bench verb's cues (Audio §7.5: each action has its sound); each must play a file.
    static readonly string[] BenchVerbs =
    {
        "screw.free", "screw.fly", "screw.drop", "screw.fetch", "screw.turn", "screw.seat",
        "cover.creak", "cover.pop", "cover.lift", "cover.clack", "part.on",
        "part.pinch", "part.lift", "part.drop", "part.fresh", "part.replace",
        "tool.pick", "tool.down", "brush.rub", "grime.scrub", "cloth.rub", "cloth.squeak", "cloth.gleam", "grime.clean",
        "device.snap", "device.flip", "checklist.tick", "bench.showleft", "repair.fixed", "repair.ready",
    };
    static readonly string[] Loops = { "brush.rub", "cloth.rub", "cafe.room" };
    // Why a cue with no file still has a placeholder (the check names them; T18 counts only the cues Audio supplied).
    static readonly Dictionary<string, string> NoFileWhy = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "screw.bounce", "retired (rule 7): the bench builder takes its call out" },
        { "cloth.wipe", "retired at the bench (cloth.rub replaces it): only the labs' whole-spot hand may ask for it" },
        { "part.off", "named, never played" },
        { "intro.grind", "the intro v2 leaves it out (Cinematics)" },
        { "intro.pour", "only when the dispenser's own pour file (Assets/Audio/Dispenser/SoftPour.wav) isn't there" },
    };
    // The Producer's integration call 4 (14 Oct): intro v2's new beats. The café's lights coming up borrow the lamp's switch
    // until Audio gives them a file of their own (Sound bank 1 copies it in, only while the manifest has none for them); the
    // other beats and the street's bed have no file yet: silent, listed by Sound bank 2, never a FAIL.
    static readonly Dictionary<string, string> Borrowed = new Dictionary<string, string>(StringComparer.Ordinal) { { "intro.lights", "intro.lamp" } };
    static readonly string[] NoFileYet = { "intro.cup", "intro.steps", "intro.car", "street.day" };

    [Serializable] sealed class ManifestFile { public string file = ""; public float seconds; }
    [Serializable] sealed class ManifestBank
    {
        public float volume = -1f, near = 1.5f, far = 12f, minGap = .05f, pitchWobble = .04f, volumeWobble = .08f;
        public string bus = "World";
        public bool threeD = true, loop;
        public int maxAtOnce = 4;
    }
    [Serializable] sealed class ManifestCue { public string cue = "", when = ""; public ManifestFile[] files = new ManifestFile[0]; public ManifestBank bank = new ManifestBank(); }
    [Serializable] sealed class CueManifest { public ManifestCue[] cues = new ManifestCue[0]; }

    static CueManifest ReadCc0(out string problem)
    {
        problem = null;
        string file = Path.Combine(ProjectRoot, Cc0Manifest);
        if (!File.Exists(file))
        {
            problem = $"No {Cc0Manifest}: copy Audio's out/files/*.wav, manifest.json and licences/ into {Cc0Folder} (Audio §6 step 2).";
            return null;
        }
        try
        {
            CueManifest m = JsonUtility.FromJson<CueManifest>(File.ReadAllText(file));
            if (m == null || m.cues == null || m.cues.Length == 0) { problem = $"{Cc0Manifest} lists no cues."; return null; }
            return m;
        }
        catch (Exception e) { problem = $"{Cc0Manifest} doesn't read: {e.Message}"; return null; }
    }

    // Every sound file under Assets/Audio/CC0, by its file name (the manifest names its files "files/<cue>_<nn>.wav").
    static Dictionary<string, string> Cc0Files()
    {
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!AssetDatabase.IsValidFolder(Cc0Folder)) return byName;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Cc0Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileName(path);
            if (!byName.ContainsKey(name)) byName[name] = path;
        }
        return byName;
    }

    // Audio §6 steps 3-5 for one file: mono, Decompress On Load, ADPCM, the sample rate kept, preloaded, not normalized, not
    // loaded in the background; the tonal ones Vorbis 80; the room tone Compressed In Memory, Vorbis 60. True if anything changed.
    static bool ConfigureClip(AudioImporter importer, string cue)
    {
        bool bed = cue == RoomTone, tonal = Tonal.Contains(cue);
        AudioClipLoadType load = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        AudioCompressionFormat format = bed || tonal ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        float quality = bed ? .6f : .8f;
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        bool changed = s.loadType != load || s.compressionFormat != format || s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                       format == AudioCompressionFormat.Vorbis && Mathf.Abs(s.quality - quality) > .005f;
        if (changed)
        {
            s.loadType = load;
            s.compressionFormat = format;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (format == AudioCompressionFormat.Vorbis) s.quality = quality;
            importer.defaultSampleSettings = s;
        }
        if (!importer.forceToMono) { importer.forceToMono = true; changed = true; }
        if (importer.loadInBackground) { importer.loadInBackground = false; changed = true; }
        // Normalize and Preload Audio Data by their serialized names (Unity 6 keeps preload in the sample settings).
        var so = new SerializedObject(importer);
        SerializedProperty normalize = so.FindProperty("normalize"), preload = so.FindProperty("defaultSettings.preloadAudioData");
        bool serialized = false;
        if (normalize != null && normalize.boolValue) { normalize.boolValue = false; serialized = true; }
        if (preload != null && !preload.boolValue) { preload.boolValue = true; serialized = true; }
        if (serialized) { so.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
        return changed;
    }

    // How a file's settings differ from Audio §6 (empty: as asked).
    static List<string> ClipAdvice(AudioImporter importer, string cue)
    {
        var advice = new List<string>();
        if (importer == null) { advice.Add("not imported as a sound"); return advice; }
        bool bed = cue == RoomTone, tonal = Tonal.Contains(cue);
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        AudioClipLoadType load = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        AudioCompressionFormat format = bed || tonal ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        if (s.loadType != load) advice.Add("Load Type " + load);
        if (s.compressionFormat != format) advice.Add("Compression " + format);
        if (s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate) advice.Add("Preserve Sample Rate");
        if (!importer.forceToMono) advice.Add("Force To Mono");
        if (importer.loadInBackground) advice.Add("Load In Background off");
        SerializedProperty preload = new SerializedObject(importer).FindProperty("defaultSettings.preloadAudioData");
        if (preload != null && !preload.boolValue) advice.Add("Preload Audio Data on");
        return advice;
    }

    [MenuItem(Menu + "Sound bank 1 - Import the CC0 sounds and fill the bank")]
    static void FillTheBank()
    {
        var head = new StringBuilder("Sound bank 1 - Import the CC0 sounds and fill the bank\n\n");
        var body = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CueManifest manifest = ReadCc0(out string problem);
            if (manifest == null) throw new InvalidOperationException(problem);
            Dictionary<string, string> files = Cc0Files();
            List<string> absent = manifest.cues.SelectMany(c => c.files).Select(f => Path.GetFileName(f.file)).Where(n => !files.ContainsKey(n)).ToList();
            if (absent.Count > 0)
                throw new InvalidOperationException($"{absent.Count} of the manifest's files aren't under {Cc0Folder} (first: {string.Join(", ", absent.Take(5))}): copy all of Audio's out/files/*.wav in, then run this again. Nothing was changed.");
            body.AppendLine($"{Cc0Manifest}: {manifest.cues.Length} cues, {manifest.cues.Sum(c => c.files.Length)} files, all found under {Cc0Folder}.");

            // 1. The files' import settings (Audio §6 steps 3-5).
            int set = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (ManifestCue mc in manifest.cues)
                    foreach (ManifestFile f in mc.files)
                        if (AssetImporter.GetAtPath(files[Path.GetFileName(f.file)]) is AudioImporter importer && ConfigureClip(importer, mc.cue))
                        {
                            importer.SaveAndReimport();
                            set++;
                        }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            body.AppendLine(set == 0 ? "Import settings: every file already as Audio §6 asks." : $"Import settings: {set} file(s) set as Audio §6 asks (mono, Decompress On Load, ADPCM; the tonal ones Vorbis 80; the room tone Compressed In Memory, Vorbis 60).");

            // 2. The bank, with every cue the game names (Create or update's rule: a cue already there keeps everything of its own).
            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            int added = 0;
            if (bank == null)
            {
                Directory.CreateDirectory(Path.Combine(ProjectRoot, Path.GetDirectoryName(BankPath)));
                bank = ScriptableObject.CreateInstance<SoundBank>();
                bank.cues = SoundBank.Defaults();
                AssetDatabase.CreateAsset(bank, BankPath);
                body.AppendLine($"Made '{BankPath}' with {bank.cues.Count} cues.");
            }
            Undo.RecordObject(bank, "Sound bank 1 - fill the bank");
            var names = new HashSet<string>(bank.cues.Where(c => c != null).Select(c => c.name), StringComparer.Ordinal);
            List<SoundBank.Cue> defaults = SoundBank.Defaults();
            foreach (SoundBank.Cue cue in defaults)
                if (names.Add(cue.name)) { bank.cues.Add(cue); added++; }
            body.AppendLine($"The bank: {added} cue(s) the game names added; {bank.cues.Count} in all.");

            // 3. Each of the manifest's cues: its files in the manifest's order. A cue given files for the first time (or new
            //    ones) takes Audio's starting numbers too; a cue that already has exactly these files keeps its numbers, which
            //    may have been set by ear since (Audio: "starting points ... to be set by ear").
            int filled = 0, swapped = 0, kept = 0;
            var lines = new List<string>();
            foreach (ManifestCue mc in manifest.cues)
            {
                SoundBank.Cue cue = bank.cues.FirstOrDefault(c => c != null && c.name == mc.cue);
                if (cue == null) { cue = new SoundBank.Cue { name = mc.cue }; bank.cues.Add(cue); added++; }
                AudioClip[] clips = mc.files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(files[Path.GetFileName(f.file)])).ToArray();
                if (clips.Any(c => c == null)) throw new InvalidOperationException($"{mc.cue}: a file of its isn't imported as a sound yet (reimport {Cc0Folder}, then run this again).");
                bool had = cue.HasClips, same = SameClips(cue.clips, clips);
                if (same) { kept++; continue; }
                cue.clips = clips;
                SoundBank.Cue code = defaults.FirstOrDefault(d => d.name == mc.cue);
                cue.when = code != null && !string.IsNullOrWhiteSpace(code.when) ? code.when : mc.when;
                ManifestBank b = mc.bank;
                if (b.volume >= 0f) cue.volume = Mathf.Clamp01(b.volume);
                cue.near = Mathf.Max(.1f, b.near);
                cue.far = Mathf.Max(.5f, b.far);
                cue.minGap = Mathf.Max(0f, b.minGap);
                cue.maxAtOnce = Mathf.Clamp(b.maxAtOnce, 1, 16);
                cue.pitchWobble = Mathf.Clamp(b.pitchWobble, 0f, .25f);
                cue.volumeWobble = Mathf.Clamp(b.volumeWobble, 0f, .5f);
                cue.threeD = b.threeD;
                if (Enum.TryParse(b.bus, out SoundBus bus)) cue.bus = bus;
                if (had) swapped++; else filled++;
                lines.Add($"  {mc.cue}: {clips.Length} file(s){(had ? " (swapped in)" : "")}, volume {cue.volume:0.000}, " +
                          $"{(cue.threeD ? $"placed, full to {cue.near:0.#} m, silent past {cue.far:0.#} m" : "flat")}, {cue.bus}, gap {cue.minGap:0.00} s, {cue.maxAtOnce} at once");
            }

            // 4. A cue that borrows another's files for now (call 4: intro.lights takes intro.lamp's), only while Audio's manifest
            //    has none of its own; it keeps its own numbers (SoundBank.Defaults).
            int borrowed = 0;
            foreach (KeyValuePair<string, string> pair in Borrowed)
            {
                if (manifest.cues.Any(c => c.cue == pair.Key)) continue;   // Audio gave it its own: step 3 filled it
                SoundBank.Cue to = bank.cues.FirstOrDefault(c => c != null && c.name == pair.Key);
                ManifestCue source = manifest.cues.FirstOrDefault(c => c.cue == pair.Value);
                if (to == null || source == null) { body.AppendLine($"{pair.Key}: can't borrow {pair.Value}'s file ({(to == null ? "not in the bank" : "not in Audio's manifest")})."); continue; }
                AudioClip[] clips = source.files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(files[Path.GetFileName(f.file)])).ToArray();
                if (clips.Length == 0 || clips.Any(c => c == null) || SameClips(to.clips, clips)) continue;
                to.clips = clips;
                borrowed++;
                lines.Add($"  {pair.Key}: {pair.Value}'s {clips.Length} file(s) for now (the Producer's call 4), its own numbers kept");
            }
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            if (filled + swapped + added + set + borrowed == 0) head.AppendLine($"Already done: all {manifest.cues.Length} of Audio's cues have their files; nothing changed.");
            else head.AppendLine($"{filled} cue(s) filled, {swapped} given new files, {kept} already had theirs (their numbers kept); {added} cue(s) added to the bank; {borrowed} given another cue's file for now; {set} file setting(s) changed. Undo: Edit > Undo.");
            body.AppendLine();
            if (lines.Count > 0) { body.AppendLine("Filled (Audio's starting numbers, 3 dB under target: set them by ear):"); foreach (string l in lines) body.AppendLine(l); }
            body.AppendLine();
            body.AppendLine("Next: Fixit Fidget > Sound > Sound bank 2 - Check the bank against Audio's table (read-only). In Edit > Project Settings > Audio (Audio §6 step 8): DSP Buffer Size Good latency, Max Real Voices 48.");
            if (Application.isPlaying) body.AppendLine("(Play Mode: the running game keeps the bank it loaded; the change counts from the next Play.)");
            Write("sound-bank-fill", head.Append(body.ToString()));
            Selection.activeObject = bank;
        }
        catch (Exception e)
        {
            Debug.LogError("[Sound] Sound bank 1 FAILED: " + e.Message + "\n" + head + body + "\n" + e);
        }
    }

    static bool SameClips(AudioClip[] a, AudioClip[] b)
    {
        AudioClip[] x = (a ?? Array.Empty<AudioClip>()).Where(c => c != null).ToArray();
        return x.Length == b.Length && b.All(x.Contains);
    }

    // The "Sound bank" check (Audio §8; T18): read-only.
    [MenuItem(Menu + "Sound bank 2 - Check the bank against Audio's table (read-only)")]
    static void CheckTheBank()
    {
        var head = new StringBuilder("Sound bank 2 - Check the bank against Audio's table (read-only)\n\n");
        var body = new StringBuilder();
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            body.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        void Info(string what) => body.AppendLine("      " + what);
        try
        {
            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null) throw new InvalidOperationException($"No bank at '{BankPath}': run Fixit Fidget > Sound > Sound bank 1.");
            CueManifest manifest = ReadCc0(out string problem);
            if (manifest == null) throw new InvalidOperationException(problem);
            Dictionary<string, string> files = Cc0Files();
            List<SoundBank.Cue> defaults = SoundBank.Defaults();

            // 1. The names (contracts.md §8: code calls cues only by Audio's names).
            List<string> notInBank = AudioTable.Where(n => bank.Find(n) == null).ToList();
            Check(notInBank.Count == 0, $"every cue in Audio's table ({AudioTable.Length}) is in the bank" + (notInBank.Count > 0 ? " (MISSING: " + string.Join(", ", notInBank) + "; run Sound bank 1)" : ""));
            List<string> notInCode = AudioTable.Where(n => defaults.All(d => d.name != n)).ToList();
            Check(notInCode.Count == 0, "SoundBank.Defaults() names every cue in Audio's table" + (notInCode.Count > 0 ? " (MISSING: " + string.Join(", ", notInCode) + ")" : ""));

            // 2. T18: each cue Audio supplied plays its files (SoundPlayer.UsesFile, the rule Play itself follows), each file
            //    imported as Audio §6 asks.
            int playing = 0, advised = 0;
            foreach (ManifestCue mc in manifest.cues)
            {
                SoundBank.Cue cue = bank.Find(mc.cue);
                List<string> wanted = mc.files.Select(f => Path.GetFileName(f.file)).ToList();
                List<string> have = cue != null && cue.clips != null ? cue.clips.Where(c => c != null).Select(c => Path.GetFileName(AssetDatabase.GetAssetPath(c))).ToList() : new List<string>();
                bool all = have.Count == wanted.Count && wanted.All(have.Contains);
                bool file = SoundPlayer.UsesFile(cue);
                if (file && all) playing++;
                string plays = file ? "plays its file" : PlaceholderSounds.Has(mc.cue) ? "PLAYS ITS SYNTHESIZED PLACEHOLDER" : "SILENT";
                Check(file && all, $"{mc.cue}: {plays} ({have.Count} of its {wanted.Count} file(s) in the bank{(all || have.Count == 0 ? "" : ", NOT Audio's set")})");
                var advice = new List<string>();
                foreach (string n in wanted)
                {
                    if (!files.TryGetValue(n, out string path)) { advice.Add(n + " isn't under " + Cc0Folder); continue; }
                    List<string> a = ClipAdvice(AssetImporter.GetAtPath(path) as AudioImporter, mc.cue);
                    if (a.Count > 0) advice.Add(n + ": " + string.Join(", ", a));
                }
                if (advice.Count > 0) { advised++; body.AppendLine("      <- " + string.Join("; ", advice)); }
                if (cue != null && file && mc.bank.volume >= 0f && Mathf.Abs(cue.volume - mc.bank.volume) > .001f)
                    Info($"{mc.cue}: volume {cue.volume:0.000}, set by ear since (Audio's start: {mc.bank.volume:0.000}).");
            }
            Check(playing == manifest.cues.Length, $"T18: {playing} of the {manifest.cues.Length} cues Audio supplied play their files; " +
                                                   $"{manifest.cues.Length - playing} would play a synthesized placeholder or nothing");
            Check(advised == 0, $"every file imported as Audio §6 asks{(advised > 0 ? $" ({advised} cue(s) with '<-' lines: run Sound bank 1)" : "")}");

            // 3. Every bench verb has its cue, with its file (§7.5).
            List<string> verbless = BenchVerbs.Where(n => !SoundPlayer.UsesFile(bank.Find(n))).ToList();
            Check(verbless.Count == 0, $"every bench verb has its cue with a file ({BenchVerbs.Length} cues: the screw out and back, the cover, the swap, the tools, " +
                                       $"the rubs, the device, the checklist, the finish){(verbless.Count > 0 ? " (WITHOUT: " + string.Join(", ", verbless) + ")" : "")}");

            // 4. The loops: one file each, long enough to loop (they play through Sfx.Loop, which has no placeholder).
            foreach (string loop in Loops)
            {
                SoundBank.Cue cue = bank.Find(loop);
                AudioClip clip = cue != null && cue.clips != null ? cue.clips.FirstOrDefault(c => c != null) : null;
                Check(clip != null && clip.length >= 1.5f, $"{loop}: a loop {(clip != null ? $"of {clip.length:0.0} s" : "with NO FILE")}");
            }

            // 5. The drinks, the till, the intro (T18's list): what each plays.
            Check(!PlaceholderSounds.Has("drink.pour") && !PlaceholderSounds.Has("drink.ready") && !PlaceholderSounds.Has("drink.machine"),
                  "the drinks never synthesize: no placeholder exists for drink.*, the dispenser's own files play (Assets/Audio/Dispenser)");
            Check(SoundPlayer.UsesFile(bank.Find("money.paid")), "the till (money.paid) plays its file");
            List<string> introNoFile = new[] { "intro.sting", "intro.sign", "intro.lamp", "day.open" }.Where(n => !SoundPlayer.UsesFile(bank.Find(n))).ToList();
            Check(introNoFile.Count == 0, "the intro's kept cues (intro.sting, intro.sign, intro.lamp, day.open) play their files" + (introNoFile.Count > 0 ? " (WITHOUT: " + string.Join(", ", introNoFile) + ")" : ""));
            // Intro v2's new beats (the Producer's call 4): the lights play the lamp's file for now; the rest have none yet.
            foreach (KeyValuePair<string, string> pair in Borrowed)
            {
                SoundBank.Cue cue = bank.Find(pair.Key);
                Check(SoundPlayer.UsesFile(cue), $"{pair.Key} plays a file ({pair.Value}'s for now, unless Audio gave it its own)" +
                                                 (cue == null ? " (NOT IN THE BANK: run Sound bank 1)" : cue.HasClips ? "" : " (NO FILE: run Sound bank 1)"));
            }
            List<string> waiting = NoFileYet.Where(n => !SoundPlayer.UsesFile(bank.Find(n))).ToList();
            Info(waiting.Count == 0 ? "intro v2's other beats and the street's bed all have files now"
                : "no file yet (silent, by design until Audio's next pass; never a FAIL): " +
                  string.Join(", ", waiting.Select(n => n + (bank.Find(n) == null ? " (not in the bank yet: Sound bank 1 adds it)" : ""))));

            // 6. What still synthesizes when asked: the cues with a placeholder and no file, and why (none of Audio's).
            List<SoundBank.Cue> synth = bank.cues.Where(c => c != null && !c.HasClips && PlaceholderSounds.Has(c.name)).ToList();
            Info($"{synth.Count} cue(s) with no file still have a synthesized placeholder (none is one Audio supplied): " +
                 string.Join("; ", synth.Select(c => c.name + (NoFileWhy.TryGetValue(c.name, out string why) ? " (" + why + ")" : " (not in Audio's first pass)"))));

            // 7. The licences beside the files; his recordings kept out of git (Audio §5, flag 7).
            string licences = Path.Combine(ProjectRoot, Cc0Folder, "licences");
            int licenceFiles = Directory.Exists(licences) ? Directory.GetFiles(licences).Count(f => !f.EndsWith(".meta", StringComparison.Ordinal)) : 0;
            Check(licenceFiles >= 5, $"Audio's licences are beside the files ({Cc0Folder}/licences: {licenceFiles} file(s), 5 sources)");
            string ignore = Path.Combine(ProjectRoot, ".gitignore");
            bool recordings = File.Exists(ignore) && File.ReadAllLines(ignore).Any(l => l.Trim() == "/Recordings/" || l.Trim() == "/[Rr]ecordings/");
            Check(recordings, ".gitignore keeps his recordings folder (Recordings/ at the repository's root) out of git");

            head.AppendLine(failures == 0 ? $"PASS: all {manifest.cues.Length} of Audio's cues play their files (T18); every bench verb has its cue; every file imported as Audio asked."
                                          : $"{failures} check(s) FAILED (T18 and Audio §8): see the FAIL lines.");
            Write("sound-bank-check", head.Append(body.ToString()));
            if (failures > 0) Debug.LogError("[Sound] Sound bank 2: " + failures + " check(s) failed.\n" + body);
        }
        catch (Exception e)
        {
            Debug.LogError("[Sound] Sound bank 2 FAILED: " + e.Message + "\n" + body + "\n" + e);
        }
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
