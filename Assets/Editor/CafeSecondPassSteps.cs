#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Second refresh pass, 23 Sept (afternoon): photos used to review the café's
// walls and windows from the game camera and from the street. Rendering is
// read-only: renderers hidden for the isometric view are switched off with
// Renderer.forceRenderingOff, which is not saved, so nothing marks the scene
// modified. Photos go to <project>/Logs/NeighborhoodRefresh/walls-<time>/.
public static class CafeSecondPassSteps
{
    const string Menu = "Fixit Fidget/Neighborhood refresh/Second pass/";
    const string Tag = "[Neighborhood refresh 2] ";

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "NeighborhoodRefresh"));

    // name, camera position, looked-at point, vertical field of view, isometric presentation
    static readonly (string name, Vector3 position, Vector3 target, float fov, bool isometric)[] WallViews =
    {
        ("w01-game-camera-as-played", new Vector3(-10.51f, 23.79f, -13.54f), new Vector3(-1.24f, 3.33f, 6.35f), 44f, true),
        ("w02-game-camera-everything", new Vector3(-10.51f, 23.79f, -13.54f), new Vector3(-1.24f, 3.33f, 6.35f), 44f, false),
        ("w03-west-facade-from-avenue", new Vector3(-13.2f, 1.7f, 4f), new Vector3(-7.5f, 1.6f, 9f), 62f, false),
        ("w04-west-facade-long", new Vector3(-12.6f, 2.2f, -4f), new Vector3(-7.5f, 1.6f, 12f), 62f, false),
        ("w05-west-wall-from-inside", new Vector3(2f, 1.6f, 7f), new Vector3(-7.5f, 1.5f, 9f), 62f, false),
        ("w06-rear-west-corner-inside", new Vector3(1f, 1.6f, 9f), new Vector3(-7.5f, 1.4f, 16f), 62f, false),
        ("w07-back-wall-from-inside", new Vector3(0f, 1.6f, 5f), new Vector3(0f, 1.5f, 18f), 62f, false),
        ("w08-east-facade-from-street", new Vector3(12.5f, 1.7f, 4f), new Vector3(7.5f, 1.6f, 9f), 62f, false),
        ("w09-rear-facade-from-lane", new Vector3(2f, 2f, 24f), new Vector3(0f, 1.6f, 18f), 62f, false),
        ("w10-game-camera-orbit-east", new Vector3(22f, 22f, -12f), new Vector3(0f, 1f, 8f), 44f, true),
    };

    [MenuItem(Menu + "Photograph the cafe walls and windows")]
    static void PhotographWalls()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play Mode first."); return; }
        try
        {
            string folder = Path.Combine(LogRoot, "walls-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            foreach (var view in WallViews)
                Capture(Path.Combine(folder, view.name + ".png"), view.position, view.target, view.fov, view.isometric);
            Debug.Log(Tag + "Wall photos: " + folder + "\n" + DescribeWindowsAndWalls());
        }
        catch (Exception e) { Debug.LogError(Tag + "Wall photos FAILED: " + e.Message + "\n" + e); }
    }

    [MenuItem(Menu + "Photograph the cafe walls and windows", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Which café wall and window pieces exist, whether they draw, and which the
    // isometric view hides (CafeViewMode's cutaway walls and overhead fixtures).
    static string DescribeWindowsAndWalls()
    {
        var lines = new List<string>();
        var mode = InScene<CafeViewMode>().FirstOrDefault();
        if (mode != null)
        {
            lines.Add("Cutaway walls: " + string.Join(", ", mode.cutawayWalls.Where(r => r != null).Select(r => PathOf(r.transform))));
            lines.Add("Overhead fixtures: " + string.Join(", ", mode.overheadFixtures.Where(r => r != null).Select(r => PathOf(r.transform))));
        }
        var room = InScene<Transform>().FirstOrDefault(t => t.name == "01 - room and windows");
        if (room != null)
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
                lines.Add((r.enabled ? "  on  " : "  off ") + PathOf(r.transform) + "  centre " + r.bounds.center.ToString("F2") + " size " + r.bounds.size.ToString("F2"));
        var joinery = InScene<Transform>().FirstOrDefault(t => t.name == "Layered windows, open doors and woven textiles");
        if (joinery != null)
            foreach (var r in joinery.GetComponentsInChildren<Renderer>(true))
                lines.Add((r.enabled ? "  on  " : "  off ") + PathOf(r.transform) + "  centre " + r.bounds.center.ToString("F2") + " size " + r.bounds.size.ToString("F2"));
        return string.Join("\n", lines);
    }

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (var p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names.Skip(Math.Max(0, names.Count - 3)));
    }

    // isometric: hide what CafeViewMode hides in the overhead presentation —
    // overhead fixtures always, and cutaway walls that block the sightline from
    // the camera to the room centre (a simplified version of its test).
    internal static void Capture(string path, Vector3 position, Vector3 target, float fov, bool isometric)
    {
        var hidden = new List<Renderer>();
        if (isometric)
        {
            var mode = InScene<CafeViewMode>().FirstOrDefault();
            if (mode != null)
            {
                hidden.AddRange(mode.overheadFixtures.Where(r => r != null && r.enabled));
                Vector3 centre = new Vector3(0f, 1.2f, 9f);
                foreach (var wall in mode.cutawayWalls.Where(r => r != null && r.enabled))
                {
                    Vector3 toCentre = centre - position;
                    if (wall.bounds.IntersectRay(new Ray(position, toCentre.normalized), out float hit) && hit < toCentre.magnitude - .02f)
                        hidden.Add(wall);
                }
            }
        }
        foreach (var r in hidden) r.forceRenderingOff = true;

        var g = new GameObject("Temporary wall review camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = g.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1440, 900, 24);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 250f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
            Object.DestroyImmediate(g);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    internal static T[] InScene<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).Where(c => c != null).ToArray();
}
#endif
