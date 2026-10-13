#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE DAY INTRO'S MENU (12 Oct 2026; Cinematics & Camera)
//
//   Fixit Fidget > Cinematics > Day intro - a check (lab, drives itself)          (DayIntroCheck)
//     A lab session on a fresh Day 1 (the lab's own save; your playtest save is not used), the autopilot off, the intro
//     forced on. The first morning plays by itself; the check then plays the rest of the pool, a skip, Short and Off, and
//     photographs every shot at its best moment into Logs/DayIntro/check-<time>/ (the storyboard) with report.txt. Play
//     mode ends by itself when it has reported.
//
//   Fixit Fidget > Cinematics > Day intro - play it (lab, Day 1 by hand, intro on)
//     The same fresh Day 1 in the lab's save, played by hand, the intro on in Full (your setting is left alone): to watch it
//     with your own eyes and ears. Press Play again to stop.
// ---------------------------------------------------------------------------
static class DayIntroMenu
{
    const string CheckMenu = "Fixit Fidget/Cinematics/Day intro - a check (lab, drives itself)";
    const string PlayMenu = "Fixit Fidget/Cinematics/Day intro - play it (lab, Day 1 by hand, intro on)";
    const string Tag = "[Day intro]";

    [MenuItem(CheckMenu)]
    static void RunCheck()
    {
        if (!FreshLabDayOne()) return;
        PlayerPrefs.SetInt(DayIntroCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + " A lab session on a fresh Day 1 that checks the morning's intro by itself. Keep the Game view in front and leave " +
                  "the mouse, keyboard and pad alone until it reports (about a minute); Play mode ends by itself. Your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(PlayMenu)]
    static void PlayByHand()
    {
        if (!FreshLabDayOne()) return;
        PlayerPrefs.SetInt(DayIntro.LabKey, 1);   // this lab session wants the intro (read once as the session starts)
        PlayerPrefs.Save();
        Debug.Log(Tag + " A lab session on a fresh Day 1, by hand, with the morning's intro on (Full; any key, mouse button or pad button skips). " +
                  "Your playtest save is not used. Press Play again to stop.");
        EditorApplication.EnterPlaymode();
    }

    // A fresh Day 1 in the lab's own save, the autopilot off (as the bench and conversation checks start).
    static bool FreshLabDayOne()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + " Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return false;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.Save();
        return true;
    }

    [MenuItem(CheckMenu, true)]
    [MenuItem(PlayMenu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string key in new[] { DayIntroCheck.PendingKey, DayIntro.LabKey })
            if (PlayerPrefs.GetInt(key, 0) != 0) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }
}
#endif
