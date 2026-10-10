#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// WHAT STANDS IN A BOX (read-only; Claude, 10 Oct 2026, for the café redesign's checks)
//
//   Fixit Fidget > Cafe redesign > Probe - List what stands in the probe box (read-only)
//
// Reads Logs/CafeRedesign/probe-box.txt (one line: x0 y0 z0 x1 y1 z1, world metres) and writes every enabled renderer
// and collider in the open scene whose world bounds meet that box, with its path, its bounds, whether it is active, its
// materials and its prefab source, to Logs/CafeRedesign/probe-last.txt. Changes nothing.
// ---------------------------------------------------------------------------
internal static class CafeProbe
{
    const string MenuPath = "Fixit Fidget/Cafe redesign/Probe - List what stands in the probe box (read-only)";

    [MenuItem(MenuPath)]
    static void Probe()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeRedesign"));
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "probe-box.txt"), output = Path.Combine(root, "probe-last.txt");
        var sb = new StringBuilder();
        try
        {
            float[] n = File.ReadAllText(input).Split(new[] { ' ', '\t', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Take(6).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            if (n.Length < 6) throw new InvalidOperationException("probe-box.txt needs six numbers: x0 y0 z0 x1 y1 z1");
            var box = new Bounds();
            box.SetMinMax(new Vector3(Mathf.Min(n[0], n[3]), Mathf.Min(n[1], n[4]), Mathf.Min(n[2], n[5])),
                          new Vector3(Mathf.Max(n[0], n[3]), Mathf.Max(n[1], n[4]), Mathf.Max(n[2], n[5])));
            Scene scene = SceneManager.GetActiveScene();
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {scene.name}  box {B(box)}");
            sb.AppendLine("Renderers (enabled ones; [off] = its GameObject is inactive in the hierarchy):");
            foreach (Renderer r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                if (r is ParticleSystemRenderer || !r.bounds.Intersects(box)) continue;
                if (!r.enabled && !r.gameObject.activeInHierarchy) continue;
                string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject);
                sb.AppendLine($"  {(r.gameObject.activeInHierarchy ? "" : "[off] ")}{(r.enabled ? "" : "[renderer disabled] ")}{(r.forceRenderingOff ? "[forceRenderingOff] " : "")}" +
                              $"{PathOf(r.transform)}  {B(r.bounds)}  mats: {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))}" +
                              (string.IsNullOrEmpty(src) ? "" : $"  prefab: {src}"));
            }
            sb.AppendLine("Colliders (enabled, active):");
            foreach (Collider c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(false)))
                if (c.enabled && c.bounds.Intersects(box))
                    sb.AppendLine($"  {(c.isTrigger ? "[trigger] " : "")}{PathOf(c.transform)}  {c.GetType().Name}  {B(c.bounds)}");
        }
        catch (Exception e) { sb.AppendLine("PROBE FAILED: " + e.Message); }
        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[Probe] " + output);
    }

    [MenuItem(MenuPath, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static string PathOf(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (Transform x = t; x != null; x = x.parent) parts.Insert(0, x.name);
        return string.Join("/", parts);
    }

    static string B(Bounds b) => string.Format(CultureInfo.InvariantCulture, "x {0:0.000}..{1:0.000}, y {2:0.000}..{3:0.000}, z {4:0.000}..{5:0.000}",
                                               b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z);
}
#endif
