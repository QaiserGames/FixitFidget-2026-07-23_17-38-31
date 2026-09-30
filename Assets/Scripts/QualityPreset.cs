using UnityEngine;

// ---------------------------------------------------------------------------
// THE QUALITY PRESET (30 Sept 2026): Low, Medium, High
//
// The three quality levels the Performance 2 step makes (see QualityPresetSteps). On the first run
// the game guesses one from the machine: a small graphics memory or an integrated GPU means Low, a
// modest card means Medium, anything else High. The guess is only a default: once the player has
// chosen (F4 cycles the presets for now; a settings screen is the phone session's), the choice is
// kept in PlayerPrefs and the guess is never made again. The editor is left alone: it runs whatever
// level is selected in Project Settings > Quality, so the labs and checks measure what they say.
//
// Why a guess at all: Mansoor's machine holds 240 fps at 4K on High, but the game should run well
// on a laptop too, and a laptop that starts on High and stutters is what a bad first impression is.
// ---------------------------------------------------------------------------
public static class QualityPreset
{
    public const string PrefKey = "FixitFidget.Quality";
    public static readonly string[] Names = { "Low", "Medium", "High" };

    /// <summary>The preset in force, as a name from <see cref="Names"/> (the quality level's name when it is not one of them).</summary>
    public static string Current => QualitySettings.names[QualitySettings.GetQualityLevel()];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnLaunch()
    {
        if (Application.isEditor) return;
        string wanted = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(wanted) || System.Array.IndexOf(Names, wanted) < 0) wanted = Suggested();
        Set(wanted, remember: false);
        Debug.Log($"[Quality] {Current} ({SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} MB; " +
                  (PlayerPrefs.HasKey(PrefKey) ? "the player's choice" : "the first-run guess") + ")");
    }

    /// <summary>The preset for this machine, from what the runtime can see: only used until the player chooses.</summary>
    public static string Suggested()
    {
        if (Application.isMobilePlatform) return "Low";
        string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
        int memory = SystemInfo.graphicsMemorySize;   // MB; integrated GPUs report little or a shared pool
        bool integrated = gpu.Contains("intel") && !gpu.Contains("arc") || gpu.Contains("iris") || gpu.Contains("uhd graphics") ||
                          gpu.Contains("radeon(tm) graphics") || gpu.Contains("radeon graphics") || gpu.Contains("vega 8") ||
                          gpu.Contains("apple m1") && !gpu.Contains("pro") && !gpu.Contains("max");
        if (integrated || memory > 0 && memory < 2048) return "Low";
        if (memory > 0 && memory < 6144 || gpu.Contains("geforce mx") || gpu.Contains("gtx 10") || gpu.Contains("gtx 16")) return "Medium";
        return "High";
    }

    /// <summary>Switch to a preset by name. Levels not made by the step (an old project state) fall back to the nearest index.</summary>
    public static void Set(string name, bool remember)
    {
        int level = System.Array.IndexOf(QualitySettings.names, name);
        if (level < 0)
        {
            int wanted = Mathf.Max(0, System.Array.IndexOf(Names, name));
            level = Mathf.Clamp(Mathf.RoundToInt(wanted * (QualitySettings.names.Length - 1) / 2f), 0, QualitySettings.names.Length - 1);
        }
        if (level != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
        if (remember)
        {
            PlayerPrefs.SetString(PrefKey, QualitySettings.names[level]);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The next preset round (Low, Medium, High, Low ...), remembered.</summary>
    public static string Cycle()
    {
        int at = System.Array.IndexOf(Names, Current);
        string next = Names[(at + 1 + Names.Length) % Names.Length];
        Set(next, remember: true);
        return Current;
    }
}
