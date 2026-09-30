#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// THE WHOLE GAME, FROM DAY 1 (29 Sept 2026: Mansoor, "i just want to playtest everything as one whole")
//
//   Fixit Fidget > Playtest > Play the whole game from Day 1 (test save)
//
// A new game on the café lab's own test save (playtest-cafe-lab.json; the playtest save is never used), played
// by hand from Day 1's morning, as the real game runs it: Grace brings her camera in and mentions Barnaby, the
// recap's "Close up for the night" leads into Night 1 (Barnaby on her step), calling it a night inside the café's
// door (or dawn) opens Day 2 with her complaint and the straight face, and on, day after day and night after
// night. The one difference from the real game is Grace's house: her door opens for Ace in every night of this
// session (NightWalk.breakIns, off in the real game until the break-ins are ready). Every run starts afresh.
internal static class WholeGamePlaytest
{
    const string MenuPath = "Fixit Fidget/Playtest/Play the whole game from Day 1 (test save)";
    const string Tag = "[Whole game] ";

    [MenuItem(MenuPath)]
    static void Play()
    {
        try
        {
            CityPackChecks.RequireScene();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        // A new game: the lab's test save is cleared (every lab writes over it anyway). The playtest save isn't touched.
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + $"Couldn't clear the test save ({path}): {e.Message}. Nothing was started.");
            return;
        }
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(GraceHouse.LabKey, GraceHouse.WholeGameLab);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"A new game from Day 1 on the test save ({path}); your playtest save is not used. Each day's recap " +
                  "leads into the night, and Grace's door opens for Ace every night of this session. Press Play again to stop.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(MenuPath, true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
