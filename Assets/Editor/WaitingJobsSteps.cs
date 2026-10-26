#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// FIXIT FIDGET > DAYS > LAB - THE FOUR THINGS TO DO WHILE WAITING (24 Oct 2026; Designer P's spec §6.7; WaitingJobsCheck says
// what each proves). Each starts a lab session on the lab's own save (never his playtest save), the autopilot off, and the
// check drives it: hands off the mouse and keyboard until it reports.
//   Lab - The find gives way (Day 1)      Lab - Clear the tables (Day 3)
//   Lab - Chat with a seated guest (Day 2) Lab - Restock by hand (Day 2)
// ---------------------------------------------------------------------------
static class WaitingJobsSteps
{
    const string Menu = "Fixit Fidget/Days/";
    const string Tag = "[Waiting jobs] ";

    [MenuItem(Menu + "Lab - The find gives way (Day 1)")]
    static void FindGivesWay() => Start(WaitingJobsCheck.Mode.Find, 1);

    [MenuItem(Menu + "Lab - Clear the tables (Day 3)")]
    static void ClearTables() => Start(WaitingJobsCheck.Mode.Table, 3);

    [MenuItem(Menu + "Lab - Chat with a seated guest (Day 2)")]
    static void Chat() => Start(WaitingJobsCheck.Mode.Chat, 2);

    [MenuItem(Menu + "Lab - Restock by hand (Day 2)")]
    static void Restock() => Start(WaitingJobsCheck.Mode.Refill, 2);

    [MenuItem(Menu + "Lab - The find gives way (Day 1)", true)]
    [MenuItem(Menu + "Lab - Clear the tables (Day 3)", true)]
    [MenuItem(Menu + "Lab - Chat with a seated guest (Day 2)", true)]
    [MenuItem(Menu + "Lab - Restock by hand (Day 2)", true)]
    static bool CanStart() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Start(WaitingJobsCheck.Mode mode, int day)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + Playtest4Shared.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        // A fresh Day 1, or day N with the labs' usual money and stock (the Day labs' own start): the day's own thrift find.
        SaveData start = day <= 1 ? new SaveData() : new SaveData { day = day, money = 600, cups = 40, beans = 40, dayCompleted = false };
        SaveCheckpointStorage.Write(path, start);
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.DeleteKey(CafeLab.HumanSpeedKey);
        PlayerPrefs.SetInt(WaitingJobsCheck.PendingKey, 1);
        PlayerPrefs.SetInt(WaitingJobsCheck.ModeKey, (int)mode);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"{WaitingJobsCheck.Words(mode)}: a lab session on Day {day} that drives itself. Keep the Game view in front " +
                  $"and leave the mouse and keyboard alone until it reports. Lab save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!PlayerPrefs.HasKey(WaitingJobsCheck.PendingKey)) return;
        PlayerPrefs.DeleteKey(WaitingJobsCheck.PendingKey);
        PlayerPrefs.DeleteKey(WaitingJobsCheck.ModeKey);
        PlayerPrefs.Save();
    }
}
#endif
