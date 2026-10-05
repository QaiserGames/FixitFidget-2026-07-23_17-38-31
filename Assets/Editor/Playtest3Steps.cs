#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// MANSOOR'S CALLS OF 5 OCT 2026, AS SCENE STEPS
//
//   Fixit Fidget > Playtest > Playtest 3 - Camera home square to the counter, 0 degrees (scene)
//       The overhead camera's authored view (CmShopCam) turned to 0°: the same tilt and distance round the
//       café's focus, so W walks straight at the counter and D along it, on the keys and on the stick alike
//       (PlayerMovement is camera-relative; the stick assist pulls onto the screen's axes, which at 25° were
//       diagonal to every wall: Mansoor's "the player runs diagonally, never straight"). CafeViewMode reads
//       the camera's angle as Play starts, so this is the whole change. "… back to 25 degrees" undoes it.
//   Fixit Fidget > Playtest > Playtest 3 - Sneak speed 1.2 m/s (scene)
//       At 1.6 m/s the crouch walk played at 2.0x its own pace (hurried); at 1.2 m/s it plays at 1.6x.
//   Fixit Fidget > Night > Slop audit - photograph the houses up close at night (lab, read-only)
//       A night lab at Grace's door (the break-ins on, a test save; the playtest save is never used) in which
//       HouseCloseUps moves Ace in first person to every street door, eight fronts and Grace's rooms and
//       photographs each: the pictures the "AI slop" judgement is made on, before and after any pass.
// ---------------------------------------------------------------------------
internal static class Playtest3Steps
{
    const string Tag = "[Playtest 3] ";

    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Camera home square to the counter, 0 degrees (scene)")]
    static void CameraHomeZero() => TurnHome(0f);

    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Camera home back to 25 degrees (scene)")]
    static void CameraHomeBack() => TurnHome(25f);

    static void TurnHome(float yaw)
    {
        try
        {
            CityPackChecks.RequireScene();
            CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("No CafeViewMode in the scene.");
            CinemachineCamera cam = view.isometricCamera ?? throw new InvalidOperationException("CafeViewMode has no overhead camera.");
            Transform t = cam.transform;
            float pitch = Mathf.DeltaAngle(0f, t.eulerAngles.x);
            float was = t.eulerAngles.y;
            float distance = Mathf.Clamp(Vector3.Distance(t.position, view.isometricFocus), view.minimumDistance, view.maximumDistance);
            Undo.RecordObject(t, "Turn the home view");
            Quaternion angle = Quaternion.Euler(pitch, yaw, 0f);
            t.SetPositionAndRotation(view.isometricFocus - angle * Vector3.forward * distance, angle);
            EditorUtility.SetDirty(t);
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Debug.Log(Tag + $"The home view is turned to {yaw:0}° (was {was:0}°): tilt {pitch:0}°, {distance:0} m from the café's focus. " +
                      "W now walks " + (Mathf.Approximately(yaw, 0f) ? "straight at the counter and D along it" : "at the camera's angle") +
                      ". Save the scene to keep it (Ctrl+S).");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't turn the home view: " + e.Message);
        }
    }

    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Sneak speed 1.2 m per second (scene)")]
    static void SneakSpeed()
    {
        try
        {
            CityPackChecks.RequireScene();
            PlayerMovement ace = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("No PlayerMovement in the scene.");
            var so = new SerializedObject(ace);
            SerializedProperty speed = so.FindProperty("sneakSpeed") ?? throw new InvalidOperationException("PlayerMovement has no sneakSpeed.");
            float was = speed.floatValue;
            speed.floatValue = 1.2f;
            so.ApplyModifiedProperties();   // records undo
            EditorSceneManager.MarkSceneDirty(ace.gameObject.scene);
            Debug.Log(Tag + $"Sneak speed {was:0.0} -> 1.2 m/s (the crouch walk plays at about 1.6x instead of 2x). Save the scene to keep it (Ctrl+S).");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't set the sneak speed: " + e.Message);
        }
    }

    [MenuItem("Fixit Fidget/Night/Slop audit - photograph the houses up close at night (lab, read-only)")]
    static void HouseCloseUpsLab()
    {
        try
        {
            CityPackChecks.RequireScene();
            if (Object.FindAnyObjectByType<GraceHouse>(FindObjectsInactive.Include) == null)
                throw new InvalidOperationException("Grace's house isn't built inside yet (Break-ins 1).");
            // The same test save as the break-ins lab: Day 5, closed for the night, Barnaby on the shelf, her facts known.
            string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
            var facts = new List<NotebookFactData>();
            foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName)) { fact.day = 1; facts.Add(fact); }
            SaveCheckpointStorage.Write(path, new SaveData
            {
                day = 5, money = 600, cups = 40, beans = 40, dayCompleted = false, notebook = facts.ToArray(),
                night = new NightSaveData { nights = 1, trophies = new[] { NightThings.GraceGnome } },
            });
            PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
            PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
            PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
            PlayerPrefs.SetInt(GraceHouse.LabKey, 1);
            PlayerPrefs.SetInt(HouseCloseUps.Key, 1);
            PlayerPrefs.Save();
            Debug.Log(Tag + "Slop audit: a night at Grace's door; Ace is moved from house to house in first person and each is photographed. " +
                      "Keep the Game view in front and leave the mouse and keyboard alone. Photos: Logs/Night/house-close-ups-<time>/.");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't start the slop audit: " + e.Message);
        }
    }

    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Camera home square to the counter, 0 degrees (scene)", true)]
    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Camera home back to 25 degrees (scene)", true)]
    [MenuItem("Fixit Fidget/Playtest/Playtest 3 - Sneak speed 1.2 m per second (scene)", true)]
    [MenuItem("Fixit Fidget/Night/Slop audit - photograph the houses up close at night (lab, read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A request that never became a Play session must not make the next Play an audit.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(HouseCloseUps.Key))
        {
            PlayerPrefs.DeleteKey(HouseCloseUps.Key);
            PlayerPrefs.Save();
        }
    }
}
#endif
