#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE HUD LAB (26 Oct 2026 (HUD 3))
//
//   Fixit Fidget > HUD > Lab - The HUD at every hour (drives itself)   (HudLabCheck, Play Mode)
//
// A lab session on the HUD's own save (Day 2, $240 in the till, one star; your playtest save is not used), the autopilot off
// so no real ticket muddles the counts. The check photographs the window at every hour, the rail at 1, 3, 5, 7 and 9
// tickets, a payment with a tip mid-animation, a star landing, the bench strip, first person, the night, the placeholder city
// and a purchase on the Arrange screen's till, measuring every word. Keep the Game view in front (1920 x 1080) and leave
// the mouse and keyboard alone until it reports. The report and its photos go to Logs/HUD/hud-<time>/.
// ---------------------------------------------------------------------------
static class HudLabSteps
{
    const string Menu = "Fixit Fidget/HUD/Lab - The HUD at every hour (drives itself)";
    const string Tag = "[HUD] ";

    [MenuItem(Menu)]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { CityPackChecks.RequireScene(); }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 2, money = 240, cups = 40, beans = 40, dayCompleted = false, reputation = 12, starsEarned = 1 });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(HudLabCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"The HUD at every hour: a lab session on a Day 2 test save ({path}); your playtest save is not used. " +
                  "Keep the Game view in front at 1920 x 1080 and leave the mouse and keyboard alone for about a minute; the photos and the report go to Logs/HUD/hud-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(HudLabCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(HudLabCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
