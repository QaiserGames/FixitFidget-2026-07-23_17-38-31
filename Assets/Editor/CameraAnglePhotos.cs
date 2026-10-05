#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// CAMERA ANGLES, SIDE BY SIDE (30 Sept 2026; read-only)
//
// The overhead camera's home view turns 25° from the room, so no key walks along the counter
// (PlayerMovement moves camera-relative: W is "up the screen"). Mansoor asked what choosing
// another home angle would mean, so this renders the home view as it is (25°) and at 0° and 45°,
// with the same tilt, distance and lens, and writes where on each picture a spot on the floor,
// the eight key directions from it and the counter's front edge fall. The pictures are marked
// up from that (arrows, the angle each key makes with the counter) outside Unity.
//
//   Fixit Fidget > Room > Camera angles - photograph the home view at 25°, 0° and 45° (read-only)
//
// Nothing in the scene changes: the photos come from a temporary camera. Written to
// Logs/Camera/angles-<time>/ (a PNG per angle and angles.json).
// ---------------------------------------------------------------------------
internal static class CameraAnglePhotos
{
    const string Menu = "Fixit Fidget/Room/Camera angles - photograph the home view at 25°, 0° and 45° (read-only)";
    const string Tag = "[Camera angles] ";
    const int Width = 1440, Height = 900;
    // A spot on the café floor between the tables, where the arrows start (world metres).
    static readonly Vector3 Spot = new Vector3(0f, .02f, 8.5f);
    const float ArrowLength = 3f;

    static readonly (string name, Vector2 input)[] Keys =
    {
        ("W", new Vector2(0, 1)), ("W+D", new Vector2(1, 1).normalized), ("D", new Vector2(1, 0)), ("S+D", new Vector2(1, -1).normalized),
        ("S", new Vector2(0, -1)), ("S+A", new Vector2(-1, -1).normalized), ("A", new Vector2(-1, 0)), ("W+A", new Vector2(-1, 1).normalized),
    };

    [MenuItem(Menu)]
    static void Run()
    {
        try
        {
            CityPackChecks.RequireScene();
            CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("No CafeViewMode in the scene.");
            CinemachineCamera shop = view.isometricCamera ?? throw new InvalidOperationException("CafeViewMode has no overhead camera.");
            // The home view as Play starts it (CafeViewMode.Start): the authored camera's yaw and pitch,
            // its distance from the focus clamped to the zoom range, around the café's focus.
            Vector3 focus = view.isometricFocus;
            float yaw = shop.transform.eulerAngles.y;
            float pitch = Mathf.DeltaAngle(0f, shop.transform.eulerAngles.x);
            float distance = Mathf.Clamp(Vector3.Distance(shop.transform.position, focus), view.minimumDistance, view.maximumDistance);
            float fov = shop.Lens.FieldOfView > 1f ? shop.Lens.FieldOfView : 39f;

            // The counter's front edge (the customer side): the intake counter's box.
            Renderer counter = GameObject.Find("ACE'S CAFE - layout study 02/02 - intake and queue/Counter")?.GetComponent<Renderer>();
            Bounds c = counter != null ? counter.bounds : new Bounds(new Vector3(0f, .55f, 13.6f), new Vector3(4f, 1.1f, 1f));
            Vector3 edgeA = new Vector3(c.min.x, c.max.y, c.min.z), edgeB = new Vector3(c.max.x, c.max.y, c.min.z);

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Camera",
                "angles-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            var json = new StringBuilder("{\n  \"views\": [\n");
            var yaws = new List<(string label, float yaw)> { ("now", yaw), ("0", 0f), ("45", 45f) };
            for (int i = 0; i < yaws.Count; i++)
            {
                float y = Mathf.Repeat(yaws[i].yaw, 360f);
                Quaternion angle = Quaternion.Euler(pitch, y, 0f);
                Vector3 position = focus - angle * Vector3.forward * distance;
                string file = $"{i + 1}-yaw-{y:0}.png";
                CafeSecondPassSteps.Capture(Path.Combine(folder, file), position, position + angle * Vector3.forward * 30f, fov, true);

                // The same camera again, only to project points onto the picture.
                var go = new GameObject("Temporary angle projector") { hideFlags = HideFlags.HideAndDontSave };
                var rt = new RenderTexture(Width, Height, 16);
                try
                {
                    var cam = go.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.targetTexture = rt;
                    cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(angle * Vector3.forward));
                    cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 250f;
                    string P(Vector3 world)
                    {
                        Vector3 s = cam.WorldToScreenPoint(world);
                        return string.Format(CultureInfo.InvariantCulture, "[{0:0.0}, {1:0.0}]", s.x, Height - s.y);
                    }
                    // How PlayerMovement turns a key into a walk: Euler(0, cameraYaw, 0) * (x, 0, y).
                    var keys = new StringBuilder();
                    foreach (var (name, input) in Keys)
                    {
                        Vector3 walk = Quaternion.Euler(0f, y, 0f) * new Vector3(input.x, 0f, input.y);
                        float offCounter = Vector3.Angle(walk, Vector3.right);
                        keys.Append(keys.Length > 0 ? ",\n" : "").Append(string.Format(CultureInfo.InvariantCulture,
                            "        {{ \"key\": \"{0}\", \"end\": {1}, \"degreesOffCounter\": {2:0.0} }}", name, P(Spot + walk * ArrowLength), offCounter));
                    }
                    json.Append(string.Format(CultureInfo.InvariantCulture,
                        "    {{ \"label\": \"{0}\", \"yaw\": {1:0.0}, \"pitch\": {2:0.0}, \"distance\": {3:0.0}, \"fov\": {4:0.0}, \"file\": \"{5}\",\n" +
                        "      \"spot\": {6}, \"alongCounter\": {7}, \"towardBackBar\": {8}, \"counterEdge\": [{9}, {10}],\n      \"keys\": [\n{11}\n      ] }}{12}\n",
                        yaws[i].label, y, pitch, distance, fov, file, P(Spot), P(Spot + Vector3.right * ArrowLength), P(Spot + Vector3.forward * ArrowLength),
                        P(edgeA), P(edgeB), keys, i + 1 < yaws.Count ? "," : ""));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            json.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(folder, "angles.json"), json.ToString());
            Debug.Log(Tag + $"The home view ({yaw:0}°, tilt {pitch:0}°, {distance:0} m, lens {fov:0}°) and the same at 0° and 45°: {folder}");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't photograph the angles: " + e.Message);
        }
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
