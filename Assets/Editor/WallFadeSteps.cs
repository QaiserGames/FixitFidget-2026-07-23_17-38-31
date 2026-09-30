#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// WALL FADE 1: THE CUT-AWAY WALLS FADE INSTEAD OF SLIDING (30 Sept 2026)
//
// Mansoor's second playtest: the walls dropping to sill height "looks too instant and weird"; he
// asked for a fade, "whichever makes the game feel more like an actual published game". CutawayWall
// and GraceHouse now fade a wall in the way to a ghost above the sill (SeeThroughMaterials, the
// dither shader the night's buildings use), over a third of a second with an ease, and bring it
// back only once it has been clearly out of the way for half a second.
//
//   Fixit Fidget > Room > Wall fade 1 - The walls fade, not slide: shader and timings (scene)
//
// The step gives CafeViewMode the shader itself (a reference the build keeps: found by name, it
// would be missing from a build) and writes the new timings into the scene's CafeViewMode and
// GraceHouse (their defaults in code changed, but a scene saved before today holds the old values
// and wins), then saves the scene.
// ---------------------------------------------------------------------------
internal static class WallFadeSteps
{
    const string Menu = "Fixit Fidget/Room/Wall fade 1 - The walls fade, not slide: shader and timings (scene)";
    const string Tag = "[Wall fade] ";
    const string ShaderPath = "Assets/Playtests/AcesCafeLayout/Night walk - see-through.shader";

    [MenuItem(Menu)]
    static void Run()
    {
        try
        {
            CityPackChecks.RequireScene();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) shader = Shader.Find(SeeThroughMaterials.DitherShaderName);
            if (shader == null) throw new InvalidOperationException("The see-through shader wasn't found at " + ShaderPath);

            CafeViewMode view = UnityEngine.Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
            if (view == null) throw new InvalidOperationException("No CafeViewMode in the scene.");
            var so = new SerializedObject(view);
            Set(so, "seeThroughShader", shader);
            Set(so, "cutawaySlideSeconds", .35f);
            Set(so, "cutawayRiseDelay", .5f);
            Set(so, "cutawayRiseMargin", .4f);
            Set(so, "cutawayFeather", .45f);
            Set(so, "cutawayGhost", .2f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            int houses = 0;
            foreach (GraceHouse house in UnityEngine.Object.FindObjectsByType<GraceHouse>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(house, "Wall fade 1");
                house.slideSeconds = .35f;
                house.riseDelay = .5f;
                house.ghost = .2f;
                house.feather = .45f;
                EditorUtility.SetDirty(house);
                houses++;
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + $"The overhead view holds the see-through shader ({shader.name}); the café's walls fade in 0.35 s, come back after 0.5 s clear, " +
                      $"feather over 0.45 m above the sill to a ghost of 0.2; {houses} house(s) the same." +
                      (saved ? " The scene is saved." : " The scene could not be saved: save it by hand."));
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Wall fade 1 FAILED: " + e.Message);
        }
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Set(SerializedObject so, string field, float value)
    {
        var p = so.FindProperty(field) ?? throw new InvalidOperationException("CafeViewMode has no field " + field);
        p.floatValue = value;
    }

    static void Set(SerializedObject so, string field, UnityEngine.Object value)
    {
        var p = so.FindProperty(field) ?? throw new InvalidOperationException("CafeViewMode has no field " + field);
        p.objectReferenceValue = value;
    }
}
#endif
