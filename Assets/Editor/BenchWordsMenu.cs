#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// BENCH 6 - THE WORDS ON THE BENCH SCREEN (checklist v2, 14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md §6 B)
//
//   Fixit Fidget > Bench > Bench 6 - Words on the bench screen (lab, a check)                      (BenchWordsCheck)
//
// A lab session on a fresh Day 1 (the lab's own save; your playtest save is not used), the autopilot off, the check driving
// itself: Grace's own visit up to the first repair, then lab devices for steady play, hidden work, "Fixed!", Tab held and an
// alert. It counts every word on the screen against WordBudget and checks that no word covers the device, the mat's
// compartments or the tools. Run it twice: the Game view at 1920 x 1080, then at 2560 x 1600 (his Mac's 16:10). The cursor
// and the held key need Unity in front. Nothing in the scene changes. Its edit-mode partner is Fixit Fidget > Checks >
// Bench words (BenchWordsChecks).
// ---------------------------------------------------------------------------
static class BenchWordsMenu
{
    const string Menu = "Fixit Fidget/Bench/Bench 6 - Words on the bench screen (lab, a check)";
    const string Tag = "[Bench words lab]";

    [MenuItem(Menu)]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + " Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(BenchWordsCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + " A lab session on a fresh Day 1 that checks by itself. Keep the Game view in front and leave the mouse " +
                  $"and keyboard alone until it reports (several minutes: it waits for Grace's visit). Game view {Screen.width} x {Screen.height}. " +
                  $"Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.GetInt(BenchWordsCheck.PendingKey, 0) != 0) { PlayerPrefs.DeleteKey(BenchWordsCheck.PendingKey); PlayerPrefs.Save(); }
    }
}
#endif
