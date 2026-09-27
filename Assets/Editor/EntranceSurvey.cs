#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// Read-only close-ups of the café's entrance doors, for the 23 Sept second-pass
// item "the door-pull gap" (the brass pulls look as if they float).
//
// Nothing in the scene changes: the photos are rendered by a temporary camera
// (CafeSecondPassSteps.Capture). Photos and a parts list go to
// <project>/Logs/NeighborhoodRefresh/entrance-<time>/.
// ---------------------------------------------------------------------------
public static class EntranceSurvey
{
    const string EntranceName = "Entrance with rounded brass pulls";

    // Measured in Assets/Art/CC0Neighborhood/Authored/EntranceRefresh.fbx
    // (Blender axes, metres: x across the opening, y out of it, z up). The two
    // pull bars and the glass of the left-hand door.
    static readonly Vector3 LeftPullOuter = new(-1.420f, 1.036f, 1.180f);
    static readonly Vector3 LeftPullInner = new(-1.185f, 1.084f, 1.180f);
    static readonly Vector3 LeftGlass = new(-1.215f, 0.648f, 1.595f);
    const float BrassHalfWidth = 1.478f;   // half the brass mesh's width in the file

    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/Photograph the entrance doors (read-only)")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Entrance survey] Stop Play Mode first.");
            return;
        }
        Transform entrance = CafeSecondPassSteps.InScene<Transform>().FirstOrDefault(t => t.name == EntranceName);
        if (entrance == null)
        {
            Debug.LogError("[Entrance survey] No '" + EntranceName + "' in the open scene.");
            return;
        }
        MeshFilter brass = entrance.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(f => f.name.EndsWith("InteriorBrass", StringComparison.Ordinal));
        if (brass == null || brass.sharedMesh == null)
        {
            Debug.LogError("[Entrance survey] The entrance has no brass mesh.");
            return;
        }

        try
        {
            // 1 when the imported mesh keeps the file's metres.
            float unit = brass.sharedMesh.bounds.extents.x / BrassHalfWidth;
            Matrix4x4 toWorld = brass.transform.localToWorldMatrix;
            Vector3 World(Vector3 file) => toWorld.MultiplyPoint3x4(file * unit);

            Vector3 outer = World(LeftPullOuter), inner = World(LeftPullInner), glass = World(LeftGlass);
            Vector3 through = (outer - inner).normalized;                                   // through the door leaf
            Vector3 outward = (World(LeftGlass + Vector3.up) - glass).normalized;           // out of the doorway
            Vector3 middle = (outer + inner) * 0.5f;

            Renderer[] parts = entrance.GetComponentsInChildren<Renderer>(true);
            Bounds all = parts[0].bounds;
            foreach (Renderer part in parts) all.Encapsulate(part.bounds);

            var log = new StringBuilder();
            log.AppendLine($"Entrance '{EntranceName}' at {entrance.position:F2}, rotation {entrance.eulerAngles:F1}; all parts: centre {all.center:F2}, size {all.size:F2}");
            log.AppendLine($"Mesh unit factor {unit:F4}. Left door: outer pull {outer:F3}, inner pull {inner:F3}, glass centre {glass:F3}; through the leaf {through:F2}; out of the doorway {outward:F2}");
            foreach (Renderer part in parts)
                log.AppendLine($"  {part.name}: centre {part.bounds.center:F3}, size {part.bounds.size:F3}, {(part.enabled ? "drawn" : "hidden")}");

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NeighborhoodRefresh", "entrance-" + stamp));
            Directory.CreateDirectory(folder);

            CafeSecondPassSteps.Capture(Path.Combine(folder, "e1-left-door-pulls-one-side.png"),
                middle + through * 0.95f + Vector3.up * 0.12f, middle, 42f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e2-left-door-pulls-other-side.png"),
                middle - through * 0.95f + Vector3.up * 0.12f, middle, 42f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e3-left-door-from-above.png"),
                glass + Vector3.up * 2.4f + through * 0.05f, new Vector3(glass.x, middle.y, glass.z), 55f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "e4-both-doors-from-outside.png"),
                all.center + outward * 4.2f + Vector3.up * 0.3f, all.center, 50f, false);

            File.WriteAllText(Path.Combine(folder, "parts.txt"), log.ToString());
            Debug.Log("[Entrance survey] Photos and parts list: " + folder + "\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError("[Entrance survey] FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem("Fixit Fidget/Neighborhood refresh/Second pass/Photograph the entrance doors (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
