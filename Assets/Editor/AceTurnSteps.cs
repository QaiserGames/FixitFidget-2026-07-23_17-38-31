#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// ACE TURNING ROUND (5 Oct 2026: Mansoor's report that Ace "runs in a diagonal animation" when the stick goes from
// up to down, and his call: a quick turn, and the legs step round instead of running sideways; see AceBody)
//
//   Fixit Fidget > Night > Ace's body 6 - Check turning round (lab, Play Mode, photos)
//
// Starts a night walk lab session (the Day 5 test save, never the playtest save) with AceTurnCheck, which plugs in
// a virtual controller and measures, every frame, where Ace's body faces against where he goes: running and walking
// straight, quick flicks from up to down and back, a wiggle, and the stick a little off straight. The flicks run
// twice, the way they were before 5 Oct and as they are now. Report, photos and frames.csv go to
// Logs/Night/ace-turn-check-<time>/. Nothing in the scene or any save changes.
// ---------------------------------------------------------------------------
internal static class AceTurnSteps
{
    const string Tag = "[Ace's body] ";
    const string MenuName = "Fixit Fidget/Night/Ace's body 6 - Check turning round (lab, Play Mode, photos)";

    [MenuItem(MenuName)]
    static void CheckTurning()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.SetInt(AceTurnCheck.LabKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Turning-round check: a night walk lab session (Day 5 test save {path}; your playtest save is not used). " +
                  "A virtual controller is plugged in for it, so leave the mouse, the keyboard and your own controller alone for about a minute. " +
                  "The report, photos and frames.csv go to Logs/Night/ace-turn-check-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(MenuName, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next one.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(AceTurnCheck.LabKey))
        {
            PlayerPrefs.DeleteKey(AceTurnCheck.LabKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
