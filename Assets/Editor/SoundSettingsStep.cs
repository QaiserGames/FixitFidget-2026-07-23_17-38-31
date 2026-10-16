using UnityEditor;
using UnityEngine;

// The run order's step 9 (Audio §6 step 8), as a menu item instead of by hand (15 Oct): the DSP buffer at Good latency
// (512 samples, about 11 ms at 48 kHz, so a tool's click lands with the touch) and 48 real voices (the bench, the café's
// murmur and the drinks can overlap without a cue being stolen). The undo puts both back if the Mac crackles.
static class SoundSettingsStep
{
    const string Set = "Fixit Fidget/Sound/Sound settings - Good latency and 48 real voices (Project Settings)";
    const string Back = "Fixit Fidget/Sound/Sound settings - Undo: Default buffer and 32 voices (if the sound crackles)";

    [MenuItem(Set)] static void Apply() => Write(512, 48);
    [MenuItem(Back)] static void Undo() => Write(0, 32);

    static void Write(int buffer, int voices)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Sound settings] Edit Mode only: stop Play first."); return; }
        Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
        if (all == null || all.Length == 0 || all[0] == null) { Debug.LogError("[Sound settings] ProjectSettings/AudioManager.asset wasn't found."); return; }
        var so = new SerializedObject(all[0]);
        SerializedProperty asked = so.FindProperty("m_RequestedDSPBufferSize"), real = so.FindProperty("m_RealVoiceCount");
        if (asked == null || real == null) { Debug.LogError("[Sound settings] The audio settings have no m_RequestedDSPBufferSize / m_RealVoiceCount here; set them by hand in Edit > Project Settings > Audio."); return; }
        int wasBuffer = asked.intValue, wasVoices = real.intValue;
        if (wasBuffer == buffer && wasVoices == voices)
        {
            Debug.Log($"[Sound settings] Already done: DSP buffer {Name(buffer)}, {voices} real voices.");
            return;
        }
        asked.intValue = buffer;
        real.intValue = voices;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(all[0]);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Sound settings] DSP buffer {Name(wasBuffer)} -> {Name(buffer)}, real voices {wasVoices} -> {voices}; saved in ProjectSettings/AudioManager.asset. " +
                  "If the sound crackles, run 'Sound settings - Undo'.");
    }

    static string Name(int samples) => samples switch
    {
        0 => "Default",
        256 => "Best latency (256)",
        512 => "Good latency (512)",
        1024 => "Best performance (1024)",
        _ => samples + " samples",
    };
}
