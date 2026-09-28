using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: THE PLAYER (claude/sound-plan.md §4.3)
//
// Plays the bank's cues for Sfx. Made the first time anything asks for a sound (never saved in the
// scene), it keeps a pool of 24 voices for one-shots and a source per loop, so nothing is created
// or thrown away while sounds come and go.
//
//   - A cue asked for again within its minimum gap is skipped (a scrub asked for every frame sounds
//     every so often), and at most maxAtOnce of it sound together: the oldest makes way.
//   - Each play picks a file at random but never the one before, with a small volume and pitch
//     wobble. Its own random numbers, never UnityEngine.Random: the café's day draws its customers
//     from that stream, and a sound must not change who walks in.
//   - Placed sounds fade from full volume at "near" to silence at "far" along a soft curve.
//   - While the game is paused (the recap) the world holds still: its voices and loops pause and new
//     ones wait; the beds carry on quietly (40%); the UI plays on.
//   - Without a file a cue is silent, but it is still counted (Requested), so a check can see that
//     the hooks fire before any sound is chosen.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public sealed class SoundPlayer : MonoBehaviour
{
    const int VoiceCount = 24;

    public static SoundPlayer Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    sealed class Voice
    {
        public AudioSource source;
        public string cue = "";
        public SoundBus bus;
        public float startedAt;
        public bool heldByPause;
    }

    sealed class Later
    {
        public string cue;
        public Vector3 at;
        public bool flat;
        public float volume, pan, due;
    }

    static readonly System.Random Rng = new System.Random();
    static readonly Dictionary<int, AnimationCurve> Curves = new Dictionary<int, AnimationCurve>();

    readonly List<Voice> voices = new List<Voice>();
    readonly List<SfxLoop> loops = new List<SfxLoop>();
    readonly List<Later> later = new List<Later>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    readonly Dictionary<string, AudioClip> lastClip = new Dictionary<string, AudioClip>();
    readonly Dictionary<SoundBus, AudioMixerGroup> groups = new Dictionary<SoundBus, AudioMixerGroup>();
    readonly Dictionary<string, int> requested = new Dictionary<string, int>();
    readonly Dictionary<string, int> heard = new Dictionary<string, int>();
    bool gamePaused;

    public SoundBank Bank { get; private set; }
    /// <summary>Times each cue was asked for (past its minimum gap), whether or not it has a file.</summary>
    public IReadOnlyDictionary<string, int> Requested => requested;
    /// <summary>Times each cue actually played a file.</summary>
    public IReadOnlyDictionary<string, int> Heard => heard;
    public int LoopsPlaying => loops.Count;
    public int GroupsFound => groups.Count;

    /// <summary>The player, made on first use while playing; null in Edit Mode.</summary>
    public static SoundPlayer Ensure()
    {
        if (Instance != null) return Instance;
        if (!Application.isPlaying) return null;
        var go = new GameObject("Sound player (while playing)") { hideFlags = HideFlags.DontSave };
        go.AddComponent<SoundPlayer>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Bank = Resources.Load<SoundBank>(SoundBank.ResourceName);
        FindGroups();
        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("Voice " + (i + 1)) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            voices.Add(new Voice { source = source });
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // The mixer's groups, found by name (the plan's layout: Master > Music, Ambience > Outside, Effects > World, Ace, UI).
    void FindGroups()
    {
        groups.Clear();
        if (Bank == null || Bank.mixer == null) return;
        AudioMixerGroup[] all = Bank.mixer.FindMatchingGroups("Master");
        foreach (SoundBus bus in (SoundBus[])System.Enum.GetValues(typeof(SoundBus)))
            foreach (AudioMixerGroup group in all)
                if (group != null && group.name == bus.ToString()) { groups[bus] = group; break; }
    }

    public AudioMixerGroup Group(SoundBus bus) => groups.TryGetValue(bus, out AudioMixerGroup group) ? group : null;

    // ------------------------------------------------------------------ one-shots

    public bool Play(string cueName, Vector3 at, bool flat, float volumeScale, float pan)
    {
        if (string.IsNullOrEmpty(cueName)) return false;
        SoundBank.Cue cue = Bank != null ? Bank.Find(cueName) : null;
        float now = Time.unscaledTime;
        float gap = cue != null ? cue.minGap : .05f;
        if (lastPlayed.TryGetValue(cueName, out float last) && now - last < gap) return false;
        lastPlayed[cueName] = now;
        Count(requested, cueName);
        if (cue == null || !cue.HasClips) return false;
        if (gamePaused && cue.bus != SoundBus.UI) return false;   // the world holds still while paused
        AudioClip clip = Pick(cue);
        if (clip == null) return false;

        Voice voice = FreeVoice(cue);
        AudioSource source = voice.source;
        source.Stop();
        Shape(source, cue, flat);
        source.clip = clip;
        source.loop = false;
        source.volume = Mathf.Clamp01(cue.volume * Wobble(cue.volumeWobble) * volumeScale);
        source.pitch = cue.pitch * Wobble(cue.pitchWobble);
        source.panStereo = flat || !cue.threeD ? Mathf.Clamp(pan, -1f, 1f) : 0f;
        source.transform.position = at;
        voice.cue = cueName;
        voice.bus = cue.bus;
        voice.startedAt = now;
        voice.heldByPause = false;
        source.Play();
        Count(heard, cueName);
        return true;
    }

    public void PlayLater(string cueName, Vector3 at, bool flat, float delay, float volumeScale, float pan)
    {
        if (string.IsNullOrEmpty(cueName)) return;
        later.Add(new Later { cue = cueName, at = at, flat = flat, volume = volumeScale, pan = pan, due = Time.unscaledTime + Mathf.Max(0f, delay) });
    }

    // This cue's own limit first (its oldest voice makes way); then a free voice; then the oldest
    // voice that isn't the UI's.
    Voice FreeVoice(SoundBank.Cue cue)
    {
        Voice oldestSame = null;
        int same = 0;
        foreach (Voice v in voices)
        {
            if (v.cue != cue.name || !(v.source.isPlaying || v.heldByPause)) continue;
            same++;
            if (oldestSame == null || v.startedAt < oldestSame.startedAt) oldestSame = v;
        }
        if (oldestSame != null && same >= cue.maxAtOnce) return oldestSame;
        foreach (Voice v in voices)
            if (!v.source.isPlaying && !v.heldByPause) return v;
        Voice oldest = null;
        foreach (Voice v in voices)
            if (v.bus != SoundBus.UI && (oldest == null || v.startedAt < oldest.startedAt)) oldest = v;
        return oldest ?? voices[0];
    }

    // ------------------------------------------------------------------ loops

    public SfxLoop Loop(string cueName, Transform follow, Vector3 at, bool flat, float volume)
    {
        if (string.IsNullOrEmpty(cueName)) return null;
        SoundBank.Cue cue = Bank != null ? Bank.Find(cueName) : null;
        Count(requested, cueName);
        if (cue == null || !cue.HasClips) return null;
        AudioClip clip = Pick(cue);
        if (clip == null) return null;

        var go = new GameObject("Loop: " + cueName) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        go.transform.position = follow != null ? follow.position : at;
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        Shape(source, cue, flat);
        source.clip = clip;
        source.loop = true;
        source.pitch = cue.pitch * Wobble(cue.pitchWobble * .5f);
        source.volume = 0f;
        // Two of the same loop never start in step.
        if (clip.samples > 1) source.timeSamples = Rng.Next(clip.samples);
        var loop = new SfxLoop(cueName, cue, source, follow, volume);
        loops.Add(loop);
        source.Play();
        Count(heard, cueName);
        return loop;
    }

    // ------------------------------------------------------------------ for components with their own source

    /// <summary>
    /// For a component that plays through its own AudioSource (the dispenser, the phones): the file to
    /// play and its volume. With a bank file for the cue, that file, the cue's volume and its reach;
    /// without one, the component's own file and volume, unchanged. Either way the source goes to the
    /// cue's mixer group.
    /// </summary>
    public AudioClip Choose(string cueName, AudioSource source, AudioClip fallback, ref float volume)
    {
        SoundBank.Cue cue = Bank != null ? Bank.Find(cueName) : null;
        if (!string.IsNullOrEmpty(cueName)) Count(requested, cueName);
        if (source != null) source.outputAudioMixerGroup = Group(cue != null ? cue.bus : SoundBus.World);
        if (cue == null || !cue.HasClips) return fallback;
        AudioClip clip = Pick(cue);
        if (clip == null) return fallback;
        if (source != null) Shape(source, cue, false);
        volume = Mathf.Clamp01(cue.volume * Wobble(cue.volumeWobble));
        Count(heard, cueName);
        return clip;
    }

    // ------------------------------------------------------------------ shared

    internal void Shape(AudioSource source, SoundBank.Cue cue, bool flat)
    {
        bool placed = cue.threeD && !flat;
        source.spatialBlend = placed ? 1f : 0f;
        source.outputAudioMixerGroup = Group(cue.bus);
        source.ignoreListenerPause = cue.bus == SoundBus.UI;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        if (!placed) return;
        float far = Mathf.Max(cue.far, cue.near + .1f);
        source.minDistance = cue.near;
        source.maxDistance = far;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(cue.near / far));
    }

    // Full volume up to "near", then a soft fall to silence at "far" (the curve's x is distance / far).
    static AnimationCurve Rolloff(float nearShare)
    {
        float n = Mathf.Clamp(nearShare, .01f, .9f);
        int key = Mathf.RoundToInt(n * 1000f);
        if (Curves.TryGetValue(key, out AnimationCurve curve)) return curve;
        float span = 1f - n;
        float middle = n + span * .4f;
        float slope = -1.1f / span;
        curve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(n, 1f, 0f, 0f),
            new Keyframe(middle, .35f, slope, slope),
            new Keyframe(1f, 0f, 0f, 0f));
        Curves[key] = curve;
        return curve;
    }

    // A file at random, never the one played last time when there's another.
    AudioClip Pick(SoundBank.Cue cue)
    {
        AudioClip before = lastClip.TryGetValue(cue.name, out AudioClip b) ? b : null;
        int total = 0, fresh = 0;
        foreach (AudioClip clip in cue.clips)
        {
            if (clip == null) continue;
            total++;
            if (clip != before) fresh++;
        }
        if (total == 0) return null;
        bool avoid = fresh > 0 && before != null;
        int pick = Rng.Next(avoid ? fresh : total);
        foreach (AudioClip clip in cue.clips)
        {
            if (clip == null || avoid && clip == before) continue;
            if (pick-- == 0)
            {
                lastClip[cue.name] = clip;
                return clip;
            }
        }
        return null;
    }

    static float Wobble(float share) => share <= 0f ? 1f : 1f + ((float)Rng.NextDouble() * 2f - 1f) * share;

    static void Count(Dictionary<string, int> counts, string cue) => counts[cue] = (counts.TryGetValue(cue, out int n) ? n : 0) + 1;

    void LateUpdate()
    {
        bool paused = Time.timeScale <= 0f;
        if (paused != gamePaused)
        {
            gamePaused = paused;
            foreach (Voice v in voices)
            {
                if (v.bus == SoundBus.UI) continue;
                if (paused && v.source.isPlaying) { v.source.Pause(); v.heldByPause = true; }
                else if (!paused && v.heldByPause) { v.source.UnPause(); v.heldByPause = false; }
            }
        }

        // Delayed sounds; the world's wait out a pause and play after it.
        for (int i = later.Count - 1; i >= 0; i--)
        {
            Later l = later[i];
            if (Time.unscaledTime < l.due) continue;
            SoundBank.Cue cue = Bank != null ? Bank.Find(l.cue) : null;
            if (paused && (cue == null || cue.bus != SoundBus.UI)) continue;
            later.RemoveAt(i);
            Play(l.cue, l.at, l.flat, l.volume, l.pan);
        }

        for (int i = loops.Count - 1; i >= 0; i--)
            if (!loops[i].Tick(Time.unscaledDeltaTime, paused)) loops.RemoveAt(i);
    }

    /// <summary>For reports: the groups found, the loops playing, and each cue asked for and heard.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"Sound player: bank {(Bank != null ? "'" + Bank.name + "'" : "not found (Assets/Data/Resources/Sound bank.asset)")}, " +
                        $"mixer {(Bank != null && Bank.mixer != null ? "'" + Bank.mixer.name + "'" : "none")}, {groups.Count} of 6 groups found, " +
                        $"{loops.Count} loops playing, game {(gamePaused ? "paused" : "running")}.");
        var names = new List<string>(requested.Keys);
        names.Sort(System.StringComparer.Ordinal);
        foreach (string name in names)
            text.AppendLine($"  {name}: asked for {requested[name]}, heard {(heard.TryGetValue(name, out int h) ? h : 0)}");
        return text.ToString();
    }
}

/// <summary>
/// A looping sound (a bed, a hum, the pour's machine): fades in when made, follows what it's attached
/// to, can be turned up and down and muffled, and fades out when stopped. Made by Sfx.Loop.
/// </summary>
public sealed class SfxLoop
{
    const float DuckedBeds = .4f;   // the beds' share while the game is paused

    readonly SoundBank.Cue cue;
    readonly Transform follow;
    readonly bool following;
    AudioSource source;
    AudioLowPassFilter lowPass;
    float level, target, fadeSeconds = 1f, cutoff = 22000f;
    bool stopping, heldByPause;

    public string Cue { get; }
    public bool Alive => source != null && !stopping;
    /// <summary>0-1: the share of the cue's volume wanted (it fades there).</summary>
    public float Volume { get => target; set => target = Mathf.Clamp01(value); }
    /// <summary>Seconds to fade across the whole range.</summary>
    public float FadeSeconds { get => fadeSeconds; set => fadeSeconds = Mathf.Max(.01f, value); }
    public AudioSource Source => source;

    internal SfxLoop(string cueName, SoundBank.Cue cue, AudioSource source, Transform follow, float volume)
    {
        Cue = cueName;
        this.cue = cue;
        this.source = source;
        this.follow = follow;
        following = follow != null;
        target = Mathf.Clamp01(volume);
    }

    public void Stop(float fade = .5f)
    {
        stopping = true;
        target = 0f;
        fadeSeconds = Mathf.Max(.01f, fade);
    }

    /// <summary>A low-pass on this loop (through a wall, a window): 22000 Hz is open.</summary>
    public void Muffle(float cutoffHz)
    {
        cutoff = Mathf.Clamp(cutoffHz, 200f, 22000f);
        if (lowPass == null && source != null && cutoff < 21000f)
        {
            lowPass = source.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }
    }

    internal bool Tick(float dt, bool gamePaused)
    {
        if (source == null) return false;
        if (following)
        {
            if (follow == null) Stop(.3f);   // what it belonged to has gone
            else source.transform.position = follow.position;
        }
        bool bed = cue.bus == SoundBus.Ambience || cue.bus == SoundBus.Outside || cue.bus == SoundBus.Music;
        if (!bed && cue.bus != SoundBus.UI)
        {
            // The world's loops hold still while the game is paused.
            if (gamePaused && !heldByPause && source.isPlaying) { source.Pause(); heldByPause = true; }
            else if (!gamePaused && heldByPause) { source.UnPause(); heldByPause = false; }
            if (gamePaused && !stopping) return true;
        }
        float want = target * (gamePaused && bed ? DuckedBeds : 1f);
        level = Mathf.MoveTowards(level, want, dt / fadeSeconds);
        source.volume = cue.volume * level;
        if (lowPass != null) lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, cutoff, 1f - Mathf.Exp(-dt * 4f));
        if (stopping && level <= .0001f)
        {
            Object.Destroy(source.gameObject);
            source = null;
            return false;
        }
        return true;
    }
}
