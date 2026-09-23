#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runs the neighborhood refresh that GPT Astra (Codex) prepared on 22–23 Sept
// but never applied, one explicit step at a time. Evidence goes to
// <project>/Logs/NeighborhoodRefresh/<date-time>/.
//
//   1  Records every gameplay setting and anchor the refresh must not touch
//      (seats, waiting spots, patience drain, save/log paths, walking settings,
//      street routes, NPC brains), and photographs the areas that will change.
//   2  Gives walk-in customers and patrons five CC0 placeholder bodies
//      (Quaternius). Named regulars keep their current body. Only renderers are
//      added: the skeleton, Animator, patience bar, speech bubble, navigation
//      and interaction components stay exactly as they were, and the step
//      proves it. Writes a line-up photo of every look.
//   3  Scenery: rounded timber chairs at the 16 table seats and the patio, oak
//      trees and benches in the east courtyard, pocket gardens, rounded brass
//      entrance pulls, hooded traffic signals, and fixed looks for the six
//      street neighbors. Old visuals are hidden, never deleted, and every old
//      collider stays where it was, so navigation and seating are unchanged.
//   4  Checks it: gameplay guards, the step-1 baseline comparison, after photos.
//
// Step 2 saves the two NPC prefabs (they are assets). Steps 3 and 4 never save
// the scene: look at the photos, then save with Ctrl+S, or reopen the scene
// without saving to discard the scenery pass.
public static class NeighborhoodRefreshSteps
{
    const string Menu = "Fixit Fidget/Neighborhood refresh/";
    const string BaselineKey = "FixitFidget.NeighborhoodRefresh.Baseline";
    const string BeachModel = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    const string CustomerPrefab = "Assets/AssetsPrefabs/Customer.prefab";
    const string Tag = "[Neighborhood refresh] ";

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NeighborhoodRefresh"));

    // Review cameras: name, camera position, point looked at (world space).
    static readonly (string name, Vector3 position, Vector3 target)[] Views =
    {
        ("01-overview", new Vector3(-24f, 22f, -24f), new Vector3(0f, 0f, 8f)),
        ("02-cafe-seating", new Vector3(0f, 3.0f, 0.8f), new Vector3(0f, 0.3f, 7f)),
        ("03-table-closeup", new Vector3(-1.2f, 1.7f, 1.4f), new Vector3(-3f, 0.45f, 3.5f)),
        ("04-patio-and-entrance", new Vector3(-1.5f, 2.0f, -6.0f), new Vector3(-2.2f, 0.8f, -0.8f)),
        ("05-entrance-pulls", new Vector3(0.6f, 1.5f, -2.6f), new Vector3(0f, 1.05f, -0.1f)),
        ("06-east-courtyard", new Vector3(10.5f, 3.6f, 0.5f), new Vector3(16.5f, 1.0f, 9.5f)),
        ("07-rear-lane", new Vector3(5.5f, 3.5f, 24.5f), new Vector3(-2.5f, 0.8f, 19.5f)),
        ("08-north-garden", new Vector3(3f, 2.6f, 24.2f), new Vector3(3f, 0.3f, 28.3f)),
        ("09-south-gardens", new Vector3(4.5f, 2.8f, -10.5f), new Vector3(4.5f, 0.2f, -14f)),
        ("10-signal-closeup", new Vector3(-8.95f, 2.3f, -16.6f), new Vector3(-8.95f, 2.2f, -13.2f)),
        ("11-junction-wide", new Vector3(-12.2f, 5.5f, -26f), new Vector3(-12.2f, 1.2f, -9f)),
        ("12-west-sidewalk-neighbors", new Vector3(-11.5f, 2.4f, 0f), new Vector3(-15.9f, 1.0f, 7f)),
        ("13-front-promenade", new Vector3(-2.6f, 1.8f, -8f), new Vector3(-2.6f, 1f, -3.7f)),
        ("14-courtyard-planter", new Vector3(15.1f, 2.3f, 3.7f), new Vector3(16.5f, 0.6f, 5.6f)),
        ("15-rear-planter", new Vector3(-4.7f, 2.3f, 21.4f), new Vector3(-6.6f, 0.6f, 19.5f)),
        ("16-seated-table", new Vector3(-3.0f, 2.4f, 1.2f), new Vector3(-3.0f, 0.45f, 3.5f)),
    };

    [MenuItem(Menu + "1 - Record gameplay baseline and before photos")]
    static void RecordBaseline() => Run("Step 1 (baseline)", () =>
    {
        string folder = Path.Combine(LogRoot, DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        string baseline = Path.Combine(folder, "gameplay-baseline.json");
        string result = NeighborhoodRefreshChecks.CaptureBaseline(baseline);
        EditorPrefs.SetString(BaselineKey, baseline);
        Photograph(folder, "before");
        return result + "\nBefore photos: " + folder;
    });

    [MenuItem(Menu + "2 - Placeholder neighbors for walk-ins and patrons")]
    static void PlaceholderNeighbors() => Run("Step 2 (placeholder neighbors)", () =>
    {
        var text = new StringBuilder();
        text.AppendLine(NpcVisualVariantSetup.PrepareModels());
        text.AppendLine(NpcVisualVariantSetup.ApplyToPrefabs());
        text.AppendLine(NpcVisualVariantSetup.ValidatePrefabs());
        string photo = Path.Combine(EvidenceFolder(), "npc-lineup.png");
        LineUp(photo);
        text.Append("Line-up photo (original, then each placeholder look): " + photo);
        return text.ToString();
    });

    [MenuItem(Menu + "3 - Apply scenery refresh to the cafe")]
    static void Scenery() => Run("Step 3 (scenery)", () =>
    {
        var text = new StringBuilder();
        text.AppendLine(NeighborhoodVisualRefresh.Apply());
        text.AppendLine(DressStreetNeighbors());
        text.Append("Recorded " + RecordInstanceChanges() + " prefab-instance edits so they survive saving. Scene NOT saved yet.");
        return text.ToString();
    });

    [MenuItem(Menu + "4 - Verify: gameplay guards, baseline, after photos")]
    static void Verify() => Run("Step 4 (verify)", () =>
    {
        var text = new StringBuilder();
        text.AppendLine(NeighborhoodRefreshChecks.Validate());
        string baseline = EditorPrefs.GetString(BaselineKey, "");
        if (baseline.Length > 0 && File.Exists(baseline)) text.AppendLine(NeighborhoodRefreshChecks.CompareBaseline(baseline));
        else text.AppendLine("No step-1 baseline found, so there was nothing to compare against.");
        string folder = EvidenceFolder();
        Photograph(folder, "after");
        text.Append("After photos: " + folder);
        return text.ToString();
    });

    [MenuItem(Menu + "Photograph the review views now")]
    static void PhotographNow() => Run("Photos", () =>
    {
        string folder = EvidenceFolder();
        string label = "now-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        Photograph(folder, label);
        return "Photos (" + label + "): " + folder;
    });

    [MenuItem(Menu + "Photograph the NPC line-up now")]
    static void LineUpNow() => Run("NPC line-up", () =>
    {
        string photo = Path.Combine(EvidenceFolder(), "npc-lineup-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".png");
        LineUp(photo);
        return photo;
    });

    // If the rounded Kenney chairs are not wanted, this puts the authored café
    // chairs back (the 16 table seats and the two patio chairs): it removes the
    // new chairs and shows the old chair meshes again. Their colliders and seat
    // anchors never changed. One Ctrl+Z undoes it; save the scene to keep it.
    [MenuItem(Menu + "Put the original chairs back")]
    static void RestoreOriginalChairs() => Run("Original chairs", () =>
    {
        var refresh = InScene<Transform>().FirstOrDefault(t => t.name == "13 - CC0 neighborhood refresh");
        var chairs = refresh != null ? refresh.Find("Warm timber cafe chairs") : null;
        if (chairs == null) return "The rounded chairs are not in this scene, so nothing changed.";
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Put the original chairs back");
        int shown = 0;
        var seating = InScene<Transform>().FirstOrDefault(t => t.name == "05 - seating - four neighborhood tables");
        if (seating != null)
            foreach (var seat in seating.GetComponentsInChildren<TableSeat>(true))
                foreach (var renderer in seat.GetComponentsInChildren<Renderer>(true))
                { Undo.RecordObject(renderer, "Show original chair"); renderer.enabled = true; shown++; }
        var patio = InScene<Transform>().FirstOrDefault(t => t.name == "08 - front patio bistro nook");
        if (patio != null)
            foreach (Transform old in patio)
                if (old.name == "Outdoor chair")
                    foreach (var renderer in old.GetComponentsInChildren<Renderer>(true))
                    { Undo.RecordObject(renderer, "Show original chair"); renderer.enabled = true; shown++; }
        Undo.DestroyObjectImmediate(chairs.gameObject);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Removed the rounded chairs and showed " + shown + " original chair renderers again. Save the scene (Ctrl+S) to keep this.";
    });

    // Throws away every unsaved change in the café scene (for example a scenery
    // pass that should be redone) by reopening it from disk. Asks first.
    [MenuItem(Menu + "Discard unsaved scene changes (reopen the cafe scene)")]
    static void DiscardUnsaved()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != AcesCafeLayoutSetup.ScenePath) { Debug.LogError(Tag + "Open the cafe scene first."); return; }
        if (!scene.isDirty) { Debug.Log(Tag + "The cafe scene has no unsaved changes."); return; }
        if (!EditorUtility.DisplayDialog("Discard unsaved scene changes?",
            "Reopen " + scene.path + " from disk, throwing away every change made since it was last saved?", "Discard", "Cancel")) return;
        string path = scene.path;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log(Tag + "Reopened " + path + " from disk; unsaved changes discarded.");
    }

    [MenuItem(Menu + "Put the original chairs back", true)]
    [MenuItem(Menu + "Discard unsaved scene changes (reopen the cafe scene)", true)]
    [MenuItem(Menu + "1 - Record gameplay baseline and before photos", true)]
    [MenuItem(Menu + "2 - Placeholder neighbors for walk-ins and patrons", true)]
    [MenuItem(Menu + "3 - Apply scenery refresh to the cafe", true)]
    [MenuItem(Menu + "4 - Verify: gameplay guards, baseline, after photos", true)]
    [MenuItem(Menu + "Photograph the review views now", true)]
    [MenuItem(Menu + "Photograph the NPC line-up now", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string title, Func<string> step)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + title + ": stop Play Mode first."); return; }
        try { Debug.Log(Tag + title + ": PASS\n" + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED: " + e.Message + "\n" + e); }
    }

    // The folder of the most recent step-1 baseline, so every photo from one
    // pass lands together; a fresh folder if step 1 has not been run.
    static string EvidenceFolder()
    {
        string baseline = EditorPrefs.GetString(BaselineKey, "");
        string folder = baseline.Length > 0 ? Path.GetDirectoryName(baseline)
            : Path.Combine(LogRoot, DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static void Photograph(string folder, string label)
    {
        foreach (var view in Views)
            NeighborhoodVisualRefresh.Capture(Path.Combine(folder, label + "-" + view.name + ".png"), view.position, view.target);
    }

    // The six Beach-model street neighbors each get one fixed placeholder look,
    // so the sidewalks stop being six copies of the same person. Their routes,
    // Animators and speeds are untouched.
    static string DressStreetNeighbors()
    {
        var bodies = new List<GameObject>();
        foreach (var street in InScene<StreetLife>())
            foreach (var actor in street.actors)
            {
                if (actor == null || actor.animator == null) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(actor.animator.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == BeachModel && !bodies.Contains(actor.animator.gameObject))
                    bodies.Add(actor.animator.gameObject);
            }
        var lines = new List<string>();
        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.GetComponent<NpcVisualVariants>() != null) { lines.Add(body.transform.parent.name + ": already dressed"); continue; }
            NpcVisualVariantSetup.ApplyToActor(body, i % 5);
            var selector = body.GetComponent<NpcVisualVariants>();
            Undo.RegisterCreatedObjectUndo(body.transform.Find("Temporary CC0 walk-in appearances").gameObject, "Dress street neighbor");
            lines.Add((body.transform.parent != null ? body.transform.parent.name : body.name) + " -> " + selector.ActiveAppearanceName);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Street neighbors (" + bodies.Count + "): " + string.Join(", ", lines);
    }

    // Script edits to prefab instances (the Kenney models, the entrance, the
    // street neighbors) must be recorded as instance overrides or Unity can
    // drop them when the scene is saved and reopened.
    static int RecordInstanceChanges()
    {
        var roots = new List<GameObject>();
        var refresh = InScene<Transform>().FirstOrDefault(t => t.name == "13 - CC0 neighborhood refresh");
        if (refresh != null) roots.Add(refresh.gameObject);
        var entrance = InScene<Transform>().FirstOrDefault(t => t.name == "Entrance with rounded brass pulls");
        if (entrance != null) roots.Add(entrance.gameObject);
        foreach (var street in InScene<StreetLife>())
            foreach (var actor in street.actors)
                if (actor != null && actor.animator != null && actor.animator.GetComponent<NpcVisualVariants>() != null)
                    roots.Add(actor.animator.gameObject);

        int recorded = 0;
        foreach (var root in roots)
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(transform.gameObject)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                foreach (var renderer in transform.GetComponents<Renderer>())
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                recorded++;
            }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return recorded;
    }

    // Original body, then every placeholder look, side by side in an idle pose.
    static void LineUp(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefab);
        var looks = prefab != null ? prefab.GetComponent<NpcVisualVariants>() : null;
        if (looks == null) throw new InvalidOperationException("The Customer prefab has no placeholder looks yet (run step 2).");

        var scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(1600, 700, 24);
        var texture = new Texture2D(1600, 700, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            var sun = new GameObject("Line-up sun");
            SceneManager.MoveGameObjectToScene(sun, scene);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            int count = looks.AppearanceCount;
            for (int i = -1; i < count; i++)
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                actor.transform.SetPositionAndRotation(new Vector3((i + 1 - count / 2f) * 0.95f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                actor.GetComponent<NpcVisualVariants>().ApplyAppearance(i);
                var animator = actor.GetComponentInChildren<Animator>();
                var idle = animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    : null;
                if (idle != null) idle.SampleAnimation(animator.gameObject, idle.length * 0.25f);
            }

            var cameraObject = new GameObject("Line-up camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var cam = cameraObject.AddComponent<Camera>();
            cam.enabled = false; cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.82f, 0.86f, 0.84f);
            cam.fieldOfView = 34f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 50f;
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.25f, -7.2f), Quaternion.Euler(4f, 0f, 0f));
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    static T[] InScene<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).Where(c => c != null).ToArray();
}
#endif
