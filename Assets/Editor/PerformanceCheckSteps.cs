#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// FIXIT FIDGET > CHECKS > PERFORMANCE (30 Sept 2026)
//
//   Performance (lab, drives itself): a café lab session on the Day 5 test save with the autopilot
//   serving (a busy room), in which PerformanceCheck drives the camera through six phases (standing,
//   orbiting, zooming, first person looking, first person walking, zoomed out orbiting) and measures
//   every frame. Report and CSV in Logs/Performance/perf-<time>/. Play Mode stops by itself.
//
// Frame Timing Stats (Player Settings > Other Settings) are switched on the first time, so the check
// can split a frame into CPU main thread, render thread and GPU; it costs nothing measurable.
// ---------------------------------------------------------------------------
internal static class PerformanceCheckSteps
{
    const string Menu = "Fixit Fidget/Checks/Performance (lab, drives itself)";
    const string Tag = "[Performance] ";

    [MenuItem(Menu)]
    static void Run()
    {
        try
        {
            CityPackChecks.RequireScene();
            if (!PlayerSettings.enableFrameTimingStats)
            {
                PlayerSettings.enableFrameTimingStats = true;
                Debug.Log(Tag + "Frame Timing Stats switched on (Player Settings > Other Settings), so the check can split a frame into CPU and GPU time.");
            }
            string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
            SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
            PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
            PlayerPrefs.SetInt(CafeLab.AutopilotKey, 1);
            PlayerPrefs.SetInt(PerformanceCheck.PendingKey, 1);
            PlayerPrefs.Save();
            Debug.Log(Tag + $"Lab session on the Day 5 test save ({path}); your playtest save is not used. The check drives the camera by itself: " +
                      "leave the mouse and keyboard alone for about a minute.");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't start: " + e.Message);
        }
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A request that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(PerformanceCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(PerformanceCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
