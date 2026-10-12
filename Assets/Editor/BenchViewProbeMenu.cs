#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE CLOSE-UP'S AND THE CONVERSATION'S VIEW: TWO PLAY CHECKS (12 Oct 2026; Mansoor's playtest: the phone out of the
// close-up's view, the conversation's camera and its replies). They began the night before as diagnostic probes:
//
//   Fixit Fidget > Bench > Bench 5 - The close-up's view (lab, a check)                            (BenchViewProbe)
//   Fixit Fidget > Dialogue > Conversation view - the camera and the replies (lab, a check)        (ConversationViewProbe)
//
// A lab session on a fresh Day 1 (the lab's own save; your playtest save is not used), the autopilot off, the check
// driving itself. Each measures and photographs; neither changes anything in the scene. Bench 5's controller part only
// runs with Unity in front (a virtual pad's presses don't arrive otherwise).
// ---------------------------------------------------------------------------
static class BenchViewProbeMenu
{
    const string Menu = "Fixit Fidget/Bench/Bench 5 - The close-up's view (lab, a check)";
    const string TalkMenu = "Fixit Fidget/Dialogue/Conversation view - the camera and the replies (lab, a check)";

    [MenuItem(Menu)]
    static void Run() => Start(BenchViewProbe.PendingKey, "[Bench view check]");

    [MenuItem(TalkMenu)]
    static void RunTalk() => Start(ConversationViewProbe.PendingKey, "[Conversation view check]");

    static void Start(string key, string tag)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(tag + " Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Debug.Log(tag + " A lab session on a fresh Day 1 that checks by itself. Keep the Game view in front and leave the mouse " +
                  $"and keyboard alone until it reports. Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu, true)]
    [MenuItem(TalkMenu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (string key in new[] { BenchViewProbe.PendingKey, ConversationViewProbe.PendingKey })
            if (PlayerPrefs.GetInt(key, 0) != 0) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }
}
#endif
