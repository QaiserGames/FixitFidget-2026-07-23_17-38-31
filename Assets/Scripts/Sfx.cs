using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: WHAT GAME CODE CALLS (claude/sound-plan.md §4.3)
//
// Game code names a cue and where it happens; everything else (which file, how loud, how far it
// carries, which mixer group) lives in the sound bank. Every call is safe anywhere: in Edit Mode,
// without a bank or without files it does nothing (a fresh clone of the public repository has no
// sound files at all).
//
//   Sfx.Play("cup.set", cup.position);          a one-shot placed in the world
//   Sfx.Play2D("ticket.new");                   a flat one-shot (the UI)
//   Sfx.PlayLater("money.tip", at, .5f);        after a moment
//   var hum = Sfx.Loop("drink.machine", slot);  a loop that follows a transform; hum.Stop() fades it
//
// SoundRig (below) gives Ace's scene its ears, Ace's footsteps and the café's soundscape when a
// scene with Ace loads; all three are made while playing and never saved in the scene.
// ---------------------------------------------------------------------------
public static class Sfx
{
    public static bool Play(string cue, Vector3 at, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null && player.Play(cue, at, false, volume, 0f);
    }

    /// <summary>A flat one-shot (the UI, far-off night sounds); pan -1 left to 1 right.</summary>
    public static bool Play2D(string cue, float volume = 1f, float pan = 0f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null && player.Play(cue, Vector3.zero, true, volume, pan);
    }

    public static void PlayLater(string cue, Vector3 at, float delay, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        if (player != null) player.PlayLater(cue, at, false, delay, volume, 0f);
    }

    public static void Play2DLater(string cue, float delay, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        if (player != null) player.PlayLater(cue, Vector3.zero, true, delay, volume, 0f);
    }

    /// <summary>A loop that follows a transform (stops by itself if that goes); null without a file.</summary>
    public static SfxLoop Loop(string cue, Transform follow, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null ? player.Loop(cue, follow, follow != null ? follow.position : Vector3.zero, false, volume) : null;
    }

    public static SfxLoop LoopAt(string cue, Vector3 at, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null ? player.Loop(cue, null, at, false, volume) : null;
    }

    /// <summary>A flat loop: a bed (the room, the street, the city at night).</summary>
    public static SfxLoop Loop2D(string cue, float volume = 1f)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null ? player.Loop(cue, null, Vector3.zero, true, volume) : null;
    }

    /// <summary>
    /// For a component with its own AudioSource: a bank file for the cue (with the cue's volume and
    /// reach) if there is one, else the component's own file and volume. The source also goes to the
    /// cue's mixer group.
    /// </summary>
    public static AudioClip Choose(string cue, AudioSource source, AudioClip fallback, ref float volume)
    {
        SoundPlayer player = SoundPlayer.Ensure();
        return player != null ? player.Choose(cue, source, fallback, ref volume) : fallback;
    }

    /// <summary>Sends a component's own AudioSource through a mixer group.</summary>
    public static void Route(AudioSource source, SoundBus bus)
    {
        if (source == null) return;
        SoundPlayer player = SoundPlayer.Ensure();
        if (player != null) source.outputAudioMixerGroup = player.Group(bus);
    }

    public static int Requested(string cue)
    {
        SoundPlayer player = SoundPlayer.Instance;
        return player != null && player.Requested.TryGetValue(cue, out int n) ? n : 0;
    }

    public static int Heard(string cue)
    {
        SoundPlayer player = SoundPlayer.Instance;
        return player != null && player.Heard.TryGetValue(cue, out int n) ? n : 0;
    }
}

/// <summary>
/// When a scene with Ace (CafeViewMode) loads while playing: the ears (ListenerRig) and the
/// footsteps (AceFootsteps) join Ace, and the soundscape (CafeSoundscape) starts. Made while
/// playing only, so the scene itself never changes.
/// </summary>
public static class SoundRig
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
        Fit();
    }

    static void Loaded(Scene scene, LoadSceneMode mode) => Fit();

    public static void Fit()
    {
        if (!Application.isPlaying) return;
        CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>();
        if (view == null) return;
        if (view.GetComponent<ListenerRig>() == null) view.gameObject.AddComponent<ListenerRig>();
        if (view.GetComponent<AceFootsteps>() == null) view.gameObject.AddComponent<AceFootsteps>();
        // By its Instance: one soundscape per Play session. It is an ordinary object of the session, so it
        // ends with it (PlaySessionLeftovers).
        if (CafeSoundscape.Instance == null)
        {
            var go = new GameObject("Soundscape (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
            go.AddComponent<CafeSoundscape>();
        }
    }
}
