using UnityEngine;

// ---------------------------------------------------------------------------
// THE PLAYER'S SETTINGS (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.4)
//
// What the phone's Settings app changes, kept between sessions (PlayerPrefs, written as each one changes):
//
//   Sound      Master (the whole game: AudioListener.volume), Music (the music bus) and Effects (everything else the
//              sound bank plays: the world, Ace, the screens, the ambience). Music and Effects scale what SoundPlayer
//              plays on their buses, live for loops (SfxLoop) and from the next play for one-shots. (The plan said
//              the mixer's own exposed volumes; scaling in code does the same for the player without editing the
//              mixer asset, so nothing in the project changes.)
//   Controls   Look sensitivity (the mouse, a multiplier on the view's own), pad look speed (the right stick, the same),
//              invert Y (mouse and stick alike), aim help (AimAssist, its own saved switch), movement help (the walk's
//              assist on a pad, PlayerMovement's own saved switch).
//   Picture    The quality preset (QualityPreset: Low, Medium, High).
//
// Defaults are the game as it played before there were settings: every multiplier 1, the volumes full but music at
// four-fifths, nothing inverted, both helps on.
// ---------------------------------------------------------------------------
public static class GameSettings
{
    const string Prefix = "FixitFidget.Settings.";
    const string MasterKey = Prefix + "Master", MusicKey = Prefix + "Music", EffectsKey = Prefix + "Effects",
        LookKey = Prefix + "Look", PadLookKey = Prefix + "PadLook", InvertKey = Prefix + "InvertY";

    /// <summary>The ranges the sliders cover.</summary>
    public const float LookMin = .25f, LookMax = 3f, PadLookMin = .5f, PadLookMax = 2f;

    static float? master, music, effects, look, padLook;
    static bool? invert;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        master = music = effects = look = padLook = null;
        invert = null;
    }

    // The master volume is the listener's, so it's set as the game starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnLaunch() => AudioListener.volume = Master;

    static float Get(ref float? cache, string key, float fallback, float min, float max)
    {
        cache ??= Mathf.Clamp(PlayerPrefs.GetFloat(key, fallback), min, max);
        return cache.Value;
    }

    static void Set(ref float? cache, string key, float value, float min, float max)
    {
        cache = Mathf.Clamp(value, min, max);
        PlayerPrefs.SetFloat(key, cache.Value);
    }

    // ------------------------------------------------------------------ sound

    /// <summary>The whole game's volume, 0-1 (the listener's).</summary>
    public static float Master
    {
        get => Get(ref master, MasterKey, 1f, 0f, 1f);
        set { Set(ref master, MasterKey, value, 0f, 1f); AudioListener.volume = master.Value; }
    }

    /// <summary>The music bus, 0-1.</summary>
    public static float Music
    {
        get => Get(ref music, MusicKey, .8f, 0f, 1f);
        set => Set(ref music, MusicKey, value, 0f, 1f);
    }

    /// <summary>Everything else the sound bank plays, 0-1.</summary>
    public static float Effects
    {
        get => Get(ref effects, EffectsKey, 1f, 0f, 1f);
        set => Set(ref effects, EffectsKey, value, 0f, 1f);
    }

    /// <summary>What a bus's sounds are scaled by (the master is the listener's, on top).</summary>
    public static float BusVolume(SoundBus bus) => bus == SoundBus.Music ? Music : Effects;

    // ------------------------------------------------------------------ controls

    /// <summary>The mouse's look speed, a multiplier on the view's own (1: as it was).</summary>
    public static float LookScale
    {
        get => Get(ref look, LookKey, 1f, LookMin, LookMax);
        set => Set(ref look, LookKey, value, LookMin, LookMax);
    }

    /// <summary>The right stick's look speed, a multiplier on the view's own (1: as it was).</summary>
    public static float PadLookScale
    {
        get => Get(ref padLook, PadLookKey, 1f, PadLookMin, PadLookMax);
        set => Set(ref padLook, PadLookKey, value, PadLookMin, PadLookMax);
    }

    /// <summary>Looking up and down turned over, for the mouse and the stick alike.</summary>
    public static bool InvertY
    {
        get
        {
            invert ??= PlayerPrefs.GetInt(InvertKey, 0) == 1;
            return invert.Value;
        }
        set
        {
            invert = value;
            PlayerPrefs.SetInt(InvertKey, value ? 1 : 0);
        }
    }

    /// <summary>1, or -1 when Y is inverted: what a look's up-and-down is multiplied by.</summary>
    public static float YSign => InvertY ? -1f : 1f;

    /// <summary>The aim help on a pad (AimAssist's own saved switch).</summary>
    public static bool AimHelp
    {
        get => AimAssist.Enabled;
        set => AimAssist.Enabled = value;
    }

    /// <summary>The walk's help on a pad (PlayerMovement's own saved switch; on when there's no Ace to ask).</summary>
    public static bool MovementHelp
    {
        get
        {
            PlayerMovement walk = Object.FindAnyObjectByType<PlayerMovement>();
            return walk != null ? walk.MovementAssist : PlayerPrefs.GetInt(PlayerMovement.AssistPrefKey, 1) == 1;
        }
        set
        {
            PlayerMovement walk = Object.FindAnyObjectByType<PlayerMovement>();
            if (walk != null) walk.MovementAssist = value;
            else PlayerPrefs.SetInt(PlayerMovement.AssistPrefKey, value ? 1 : 0);
        }
    }

    // ------------------------------------------------------------------ picture

    /// <summary>The quality preset's name (Low, Medium, High).</summary>
    public static string Quality
    {
        get => QualityPreset.Current;
        set => QualityPreset.Set(value, remember: true);
    }

    /// <summary>Writes what changed to disk (the phone does it as it closes).</summary>
    public static void Save() => PlayerPrefs.Save();

    /// <summary>Everything as it stands (reports).</summary>
    public static string Describe() =>
        $"Settings: master {Master:0.00}, music {Music:0.00}, effects {Effects:0.00}; look x{LookScale:0.00}, pad look x{PadLookScale:0.00}, " +
        $"invert Y {(InvertY ? "on" : "off")}, aim help {(AimHelp ? "on" : "off")}, movement help {(MovementHelp ? "on" : "off")}; quality {Quality}.";
}
