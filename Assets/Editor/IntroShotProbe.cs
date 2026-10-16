using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 15 Oct, the day intro's check on his Mac: Day 1's bench shot (bench.ready) looks at the bench from a seated customer's eye,
// through the counter's frosted guard, so the mat and its tools read as a blur behind glass on the very first morning. This
// renders that framing and four others (the staff side, Ace's eye; over the guard) from the scene as it stands, with what lies
// on each line of sight, so the shot's new framing is chosen from pictures. Read-only: a temporary camera, nothing saved.
static class IntroShotProbe
{
    [MenuItem("Fixit Fidget/Checks/Probe - the intro's bench shot, five framings (renders, read-only)")]
    static void Run()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "DayIntro", $"probe-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        var notes = new StringBuilder();

        StationInteractable bench = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude)
            .FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        BenchStage stage = Object.FindAnyObjectByType<BenchStage>();
        Transform lamp = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("CK_BenchLamp", StringComparison.Ordinal));
        if (bench == null || stage == null || stage.matFrame == null)
        {
            Debug.LogError("[Intro probe] The bench's stand point or the stage's mat isn't in the open scene.");
            return;
        }
        Vector3 stand = bench.StandPoint.position;
        Vector3 mat = stage.matFrame.position;
        notes.AppendLine($"stand point {stand:0.00}, its forward {bench.StandPoint.forward:0.00}; the mat {mat:0.00}; the lamp {(lamp != null ? lamp.position.ToString("0.00") : "none")}");

        // Ace's eye over the stand point; "toward" is from him to the mat, flat; "left" is his left.
        Vector3 toward = Vector3.ProjectOnPlane(mat - stand, Vector3.up).normalized;
        if (toward.sqrMagnitude < .5f) toward = Vector3.ProjectOnPlane(bench.StandPoint.forward, Vector3.up).normalized;
        Vector3 left = Vector3.Cross(toward, Vector3.up).normalized;
        float floor = stand.y;
        Vector3 eye = new Vector3(stand.x, floor + 1.62f, stand.z);
        Vector3 matTop = mat + Vector3.up * .03f;

        var poses = new (string name, Vector3 from, Vector3 look, float fov)[]
        {
            ("A-now-seated-customer", new Vector3(-3.0f, 1.35f, 12.2f), new Vector3(-4.5f, 1.15f, 14.6f), 40f),
            ("B-over-the-guard", new Vector3(-3.0f, 1.35f, 12.2f) + Vector3.up * .65f, matTop, 40f),
            ("C-ace-eye", eye - toward * .15f, matTop, 40f),
            ("D-ace-left", eye + left * .6f - toward * .1f - Vector3.up * .1f, matTop + toward * .1f, 40f),
            ("E-ace-right-low", eye - left * .55f - Vector3.up * .3f, matTop, 40f),
        };

        var go = new GameObject("Intro shot probe (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture rt = null;
        Texture2D picture = null;
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = .03f;
            cam.farClipPlane = 400f;
            rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var p in poses)
            {
                cam.fieldOfView = p.fov;
                go.transform.SetPositionAndRotation(p.from, Quaternion.LookRotation(p.look - p.from));
                cam.Render();
                RenderTexture.active = rt;
                picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                picture.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, p.name + ".jpg"), picture.EncodeToJPG(88));
                // What the line of sight to the mat meets first (colliders: a drawn thing without one isn't listed).
                Vector3 d = p.look - p.from;
                RaycastHit[] hits = Physics.RaycastAll(p.from, d.normalized, d.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                notes.AppendLine($"{p.name}: from {p.from:0.00} to {p.look:0.00} ({d.magnitude:0.00} m, {p.fov:0}°): " +
                                 (hits.Length == 0 ? "nothing on the line" : string.Join("; ", hits.Take(4).Select(h => $"{h.collider.name} at {h.distance:0.00} m"))));
            }
        }
        finally
        {
            if (picture != null) Object.DestroyImmediate(picture);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText(Path.Combine(folder, "notes.txt"), notes.ToString());
        Debug.Log("[Intro probe] Five framings of the bench shot rendered: " + folder + "\n" + notes);
    }
}
