#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// JUICE STEPS (6 Oct 2026; Mansoor's playtest: "it needs to start feeling a lot better in terms of juice")
//
//   Fixit Fidget > Playtest > Juice - Photograph the feedback (lab, Play Mode): a lab session on the Day 5 test save (your
//   playtest save is not used), the lab's autopilot keeping the café busy. The check (JuiceCheck) shows every kind of
//   feedback over the people sitting in view (the badges, a served drink's reactions, paying, the money counting up, a cup
//   handed over, a repair's sparkle), photographs each, and reports to Logs/Juice/juice-check-<time>/.
// ---------------------------------------------------------------------------
static class JuiceSteps
{
    const string Menu = "Fixit Fidget/Playtest/Juice - Photograph the feedback (lab, Play Mode)";
    const string Tag = "[Juice] ";

    [MenuItem(Menu)]
    static void Photograph()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { CityPackChecks.RequireScene(); }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData { day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 1);
        PlayerPrefs.SetInt(JuiceCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Juice check: a lab session on the Day 5 test save ({path}); your playtest save is not used. " +
                  "Leave the mouse and keyboard alone for a minute or two; the photos and the report go to Logs/Juice/juice-check-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    static bool CanPhotograph() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(JuiceCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(JuiceCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
