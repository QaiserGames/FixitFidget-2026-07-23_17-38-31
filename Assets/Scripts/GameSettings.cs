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
//              assist on a pad, PlayerMovement's own saved switch), and swapping the sticks at the bench (below).
//   Mornings   The day intro: Full, Short or Off (below).
//   Picture    The quality preset (QualityPreset: Low, Medium, High).
//
// Defaults are the game as it played before there were settings: every multiplier 1, the volumes full but music at
// four-fifths, nothing inverted, both helps on.
//
// 12-13 OCT 2026 (the big session after his controller playtest; claude/playtest-4-findings-and-proposals.md §1b and §6).
// Two settings the other leads read, by these names (the Producer's contract; the UI lead owns them, nobody else writes
// them but the phone):
//   SwapBenchSticks   at the bench, the left stick moves the hand and the right stick turns the device (his 1b: "i want
//                     the rotating and just the controls overall to be simple"). Default off: the right stick is the aiming
//                     stick everywhere else in the game. Kept as 0/1 under FixitFidget.Settings.SwapBenchSticks. Read by
//                     ItemInspector, PadCursor and BenchCards (Systems A).
//   DayIntro          the morning's few seconds of the café waking up (his 6: every day, skippable, Short/Off in the
//                     settings): DayIntroMode.Full, .Short (just the title) or .Off. Default Full. Kept as an int (0, 1, 2)
//                     under FixitFidget.Settings.DayIntro; a number the game doesn't know reads as Full. Read by DayIntro
//                     (Cinematics), which also stays off in the lab (CafeLab.Active) unless a lab asks for it.
// ---------------------------------------------------------------------------

/// <summary>
/// How the day intro plays each morning (Settings › MORNINGS; GameSettings.DayIntro). Declared here, outside GameSettings,
/// so other code writes it plainly: <c>GameSettings.DayIntro == DayIntroMode.Off</c>. The numbers are what PlayerPrefs
/// keeps: never reorder them.
/// </summary>
public enum DayIntroMode
{
    /// <summary>The whole intro, every morning (6-8 s, skippable).</summary>
    Full = 0,
    /// <summary>Just the sting and the day's title over the player's own camera (2-3 s).</summary>
    Short = 1,
    /// <summary>No intro: the day starts at once.</summary>
    Off = 2,
}

public static class GameSettings
{
    const string Prefix = "FixitFidget.Settings.";
    const string MasterKey = Prefix + "Master", MusicKey = Prefix + "Music", EffectsKey = Prefix + "Effects",
        LookKey = Prefix + "Look", PadLookKey = Prefix + "PadLook", InvertKey = Prefix + "InvertY";

    /// <summary>Where SwapBenchSticks is kept (0 or 1), and where DayIntro is kept (DayIntroMode as an int).</summary>
    public const string SwapBenchSticksKey = Prefix + "SwapBenchSticks", DayIntroKey = Prefix + "DayIntro";
    /// <summary>Where Vibration is kept (0 or 1).</summary>
    public const string VibrationKey = Prefix + "Vibration";

    /// <summary>The ranges the sliders cover.</summary>
    public const float LookMin = .25f, LookMax = 3f, PadLookMin = .5f, PadLookMax = 2f;

    static float? master, music, effects, look, padLook;
    static bool? invert, swapSticks, vibration;
    static DayIntroMode? dayIntro;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Reload();

    /// <summary>
    /// Forgets what was read, so each setting is read again from where it's kept (PlayerPrefs) the next time it's asked
    /// for. A new Play session does this by itself; the checks call it to see a setting read back as it was kept.
    /// </summary>
    public static void Reload()
    {
        master = music = effects = look = padLook = null;
        invert = swapSticks = vibration = null;
        dayIntro = null;
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

    static bool GetSwitch(ref bool? cache, string key, bool fallback)
    {
        cache ??= PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        return cache.Value;
    }

    static void SetSwitch(ref bool? cache, string key, bool value)
    {
        cache = value;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
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
        get => GetSwitch(ref invert, InvertKey, false);
        set => SetSwitch(ref invert, InvertKey, value);
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

    /// <summary>
    /// At the bench only: the left stick moves the hand and the right stick turns the device (the other way round from
    /// the default). Off unless the player turns it on (Settings › CONTROLS, "Swap the sticks at the bench").
    /// </summary>
    public static bool SwapBenchSticks
    {
        get => GetSwitch(ref swapSticks, SwapBenchSticksKey, false);
        set => SetSwitch(ref swapSticks, SwapBenchSticksKey, value);
    }

    /// <summary>
    /// The pad's rumble at the bench (the forgiving bench, 14 Oct 2026: "feel the impact", his decision 4: the rub under the
    /// cloth and brush, a pulse when a screw comes free, a cover pops, a part snaps in, a spot comes clean). On unless the
    /// player turns it off (Settings › CONTROLS, "Vibration"; UI/UX places the row). Read by BenchFeel.
    /// </summary>
    public static bool Vibration
    {
        get => GetSwitch(ref vibration, VibrationKey, true);
        set
        {
            SetSwitch(ref vibration, VibrationKey, value);
            if (!value) PadInput.StopRumble();
        }
    }

    // ------------------------------------------------------------------ mornings

    /// <summary>How the day intro plays each morning: Full (the default), Short (just the title) or Off (Settings › MORNINGS).</summary>
    public static DayIntroMode DayIntro
    {
        get
        {
            dayIntro ??= Known(PlayerPrefs.GetInt(DayIntroKey, (int)DayIntroMode.Full));
            return dayIntro.Value;
        }
        set
        {
            dayIntro = Known((int)value);
            PlayerPrefs.SetInt(DayIntroKey, (int)dayIntro.Value);
        }
    }

    // A kept number the game knows as a mode, or Full: a stray value never leaves the mornings in a state with no name.
    static DayIntroMode Known(int kept) =>
        kept >= (int)DayIntroMode.Full && kept <= (int)DayIntroMode.Off ? (DayIntroMode)kept : DayIntroMode.Full;

    // ------------------------------------------------------------------ picture

    /// <summary>The quality preset's name (Low, Medium, High).</summary>
    public static string Quality
    {
        get => QualityPreset.Current;
        set => QualityPreset.Set(value, remember: true);
    }

    /// <summary>
    /// Writes what changed to disk (the phone does it as it closes). Every setting above is put in PlayerPrefs the moment
    /// it changes (the bench's sticks and the morning too), so this one call covers them all.
    /// </summary>
    public static void Save() => PlayerPrefs.Save();

    /// <summary>Everything as it stands (reports).</summary>
    public static string Describe() =>
        $"Settings: master {Master:0.00}, music {Music:0.00}, effects {Effects:0.00}; look x{LookScale:0.00}, pad look x{PadLookScale:0.00}, " +
        $"invert Y {(InvertY ? "on" : "off")}, aim help {(AimHelp ? "on" : "off")}, movement help {(MovementHelp ? "on" : "off")}, " +
        $"swap the sticks at the bench {(SwapBenchSticks ? "on" : "off")}, vibration {(Vibration ? "on" : "off")}; the morning {DayIntro}; quality {Quality}.";
}
