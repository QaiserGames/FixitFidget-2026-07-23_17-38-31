#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// BREAK-INS 4: GRACE'S DOOR IN THE REAL GAME (30 Sept 2026)
//
// Until now the break-ins were off in the real game (NightWalk.breakIns false in the scene) and on
// only in their labs, so Mansoor's second playtest (Days 1-3, on the whole-game entry or his own
// save) found her door shut on Night 2. He wants to explore inside, so the door opens for Ace in
// every night from now on: E on her stoop, "Let yourself in" (GraceDoorZone). The key under the mat
// and the lock-pick (break-ins spec, section 5; chunk D) narrow it later.
//
//   Fixit Fidget > Night > Break-ins 4 - Grace's door opens for Ace in the real game (scene)
//   Fixit Fidget > Night > Break-ins 4 - Lock it again (the labs still open it)
//
// Each sets NightWalk.breakIns on the night group in the café scene and saves the scene: the field's
// default in code is on, but a scene saved before 30 Sept holds the old value and wins.
// ---------------------------------------------------------------------------
internal static class BreakInsRealGameSteps
{
    const string Menu = "Fixit Fidget/Night/";
    const string Tag = "[Break-ins] ";

    [MenuItem(Menu + "Break-ins 4 - Grace's door opens for Ace in the real game (scene)")]
    static void Open() => Set(true);

    [MenuItem(Menu + "Break-ins 4 - Lock it again (the labs still open it)")]
    static void Lock() => Set(false);

    [MenuItem(Menu + "Break-ins 4 - Grace's door opens for Ace in the real game (scene)", true)]
    [MenuItem(Menu + "Break-ins 4 - Lock it again (the labs still open it)", true)]
    static bool CanSet() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Set(bool on)
    {
        try
        {
            CityPackChecks.RequireScene();
            NightWalk walk = UnityEngine.Object.FindAnyObjectByType<NightWalk>(FindObjectsInactive.Include);
            if (walk == null) throw new InvalidOperationException("No NightWalk in the scene: run Night walk 1 (the set-up) first.");
            if (walk.breakIns == on)
            {
                Debug.Log(Tag + (on ? "Grace's door already opens for Ace in the real game (NightWalk.breakIns is on)."
                                    : "The break-ins are already off in the scene."));
                return;
            }
            Undo.RecordObject(walk, on ? "Break-ins 4 - Grace's door opens for Ace" : "Break-ins 4 - lock Grace's door");
            walk.breakIns = on;
            EditorUtility.SetDirty(walk);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + (on ? "Grace's door now opens for Ace in every night of the real game: E on her stoop, \"Let yourself in\"."
                                : "The break-ins are off in the real game again; their labs still switch them on.")
                          + (saved ? " The scene is saved." : " The scene could not be saved: save it by hand."));
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Break-ins 4 FAILED: " + e.Message);
        }
    }
}
#endif
