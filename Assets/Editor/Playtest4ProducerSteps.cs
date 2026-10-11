using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// PLAYTEST 4, the Producer's own small steps (10 Oct 2026): the seams between the leads' work.
//
//   1 - The pocket watch rests on its surface (prefab). The Tray lead measured it: the watch's back screw heads sit
//       21.7 mm under its anchor, so at JobBase.restHeight 0.01 it sinks 6.7 mm into the counter's stone (11.7 mm into
//       any surface at its spot's height) and 14.7 mm into the new intake tray's felt. At 0.025 it rests 0.3 mm over the
//       felt. restHeight is read by PlacementJitter (the intake slots) and PlayerCarry (setting a held thing down).
// ---------------------------------------------------------------------------
public static class Playtest4ProducerSteps
{
    const string Area = Playtest4Shared.Menu + "Producer/";
    const string WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab";
    const float WatchRest = 0.025f;

    [MenuItem(Area + "1 - The pocket watch rests on its surface (prefab)", false, 1)]
    static void WatchRestsOnTheFelt()
    {
        float was = float.NaN;
        Playtest4Shared.Step("Playtest 4 › Producer › 1 - The pocket watch rests on its surface (prefab)", "producer-1-watch-rest",
            survey: run =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
                run.Require(prefab != null, "No prefab at " + WatchPath + ".");
                JobBase job = prefab.GetComponent<JobBase>();
                run.Require(job != null, WatchPath + " has no JobBase on its root.");
                was = job.restHeight;
                run.Info($"{WatchPath}: restHeight {was:0.###} m (will be {WatchRest:0.###})");
            },
            change: run =>
            {
                if (Mathf.Approximately(was, WatchRest))
                {
                    run.Info("already " + WatchRest.ToString("0.###"));
                    return;
                }
                Playtest4Shared.EditPrefab(run, WatchPath,
                    root =>
                    {
                        var so = new SerializedObject(root.GetComponent<JobBase>());
                        so.FindProperty("restHeight").floatValue = WatchRest;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    },
                    root => Mathf.Approximately(root.GetComponent<JobBase>().restHeight, WatchRest));
                run.Changed($"{WatchPath} JobBase.restHeight: {was:0.###} → {WatchRest:0.###}");
            },
            checks: run =>
            {
                run.Check(!float.IsNaN(was), "the watch's rest height was read");
            });
    }

    [MenuItem(Area + "1 - The pocket watch rests on its surface (prefab)", true)]
    static bool CanRun() => Playtest4Shared.NotPlaying();
}
