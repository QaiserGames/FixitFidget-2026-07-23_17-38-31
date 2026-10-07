#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// PLAYTEST 3, SESSION 2: THE LABS (7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.5)
//
// Each starts a lab session on a fresh Day 1 (the lab's own save; your playtest save is not used), the lab's autopilot
// off, and a check that drives the game by itself with a virtual gamepad. Keep the Game view in front and leave the
// mouse and keyboard alone until it reports. Reports and photos go to Logs/.
//
//   Fixit Fidget > Playtest > Stations as reach - play check, overhead (lab, drives itself)      (StationsCheck)
//   Fixit Fidget > Playtest > Stations as reach - play check, first person (lab, drives itself)  (StationsCheck)
//   Fixit Fidget > Playtest > Aim help - play check (lab, drives itself)                        (AimHelpCheck)
//   Fixit Fidget > Playtest > Slimmer Ace - capsule and crouch check (lab, drives itself)        (CapsuleCheck)
// ---------------------------------------------------------------------------
static class Playtest3Session2Steps
{
    const string Menu = "Fixit Fidget/Playtest/";
    const string Tag = "[Playtest 3] ";

    [MenuItem(Menu + "Stations as reach - play check, overhead (lab, drives itself)")]
    static void StationsOverhead() => Start(StationsCheck.PendingKey, "Stations as reach, the overhead view", () => PlayerPrefs.SetInt(StationsCheck.FirstPersonKey, 0));

    [MenuItem(Menu + "Stations as reach - play check, first person (lab, drives itself)")]
    static void StationsFirstPerson() => Start(StationsCheck.PendingKey, "Stations as reach, first person", () => PlayerPrefs.SetInt(StationsCheck.FirstPersonKey, 1));

    [MenuItem(Menu + "Aim help - play check (lab, drives itself)")]
    static void AimHelp() => Start(AimHelpCheck.PendingKey, "Aim help on a pad", null);

    [MenuItem(Menu + "Slimmer Ace - capsule and crouch check (lab, drives itself)")]
    static void Capsule() => Start(CapsuleCheck.PendingKey, "The slimmer Ace and the crouch", null);

    [MenuItem(Menu + "Stations as reach - play check, overhead (lab, drives itself)", true)]
    [MenuItem(Menu + "Stations as reach - play check, first person (lab, drives itself)", true)]
    [MenuItem(Menu + "Aim help - play check (lab, drives itself)", true)]
    [MenuItem(Menu + "Slimmer Ace - capsule and crouch check (lab, drives itself)", true)]
    static bool CanStart() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Start(string pendingKey, string what, Action more)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(pendingKey, 1);
        more?.Invoke();
        PlayerPrefs.Save();
        Debug.Log(Tag + what + ": a lab session on a fresh Day 1 that drives itself with a virtual gamepad. Keep the Game view " +
                  $"in front and leave the mouse and keyboard alone until it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool any = false;
        foreach (string key in new[] { StationsCheck.PendingKey, StationsCheck.FirstPersonKey, AimHelpCheck.PendingKey, CapsuleCheck.PendingKey })
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); any = true; }
        if (any) PlayerPrefs.Save();
    }
}
#endif
