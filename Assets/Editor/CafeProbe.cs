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
            var seen = new System.Collections.Generic.List<Material>();
            foreach (Renderer r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                if (r is ParticleSystemRenderer || !r.bounds.Intersects(box)) continue;
                if (!r.enabled && !r.gameObject.activeInHierarchy) continue;
                string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject);
                sb.AppendLine($"  {(r.gameObject.activeInHierarchy ? "" : "[off] ")}{(r.enabled ? "" : "[renderer disabled] ")}{(r.forceRenderingOff ? "[forceRenderingOff] " : "")}" +
                              $"{PathOf(r.transform)}  {B(r.bounds)}  mats: {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))}" +
                              (string.IsNullOrEmpty(src) ? "" : $"  prefab: {src}"));
                if (r.gameObject.activeInHierarchy && r.enabled)
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && !seen.Contains(m)) seen.Add(m);
            }
            // How each visible renderer's materials draw: the queue decides what is drawn over what, so a sign behind glass
            // shows whether the glass is laid over it. Read-only.
            sb.AppendLine("Materials of the active renderers (queue, surface, how it blends and how it's lit):");
            foreach (Material m in seen.OrderBy(x => x.renderQueue))
            {
                string F(string p) => m.HasProperty(p) ? m.GetFloat(p).ToString("0.###", CultureInfo.InvariantCulture) : "-";
                string C(string p) { if (!m.HasProperty(p)) return "-"; Color c = m.GetColor(p); return string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###},{2:0.###},{3:0.###})", c.r, c.g, c.b, c.a); }
                sb.AppendLine($"  {m.name}  queue {m.renderQueue}  shader {(m.shader != null ? m.shader.name : "none")}  asset {AssetDatabase.GetAssetPath(m)}");
                sb.AppendLine($"      surface {F("_Surface")} blend {F("_Blend")} src {F("_SrcBlend")} dst {F("_DstBlend")} zwrite {F("_ZWrite")} clip {F("_AlphaClip")} cutoff {F("_Cutoff")} " +
                              $"queueOffset {F("_QueueOffset")} preserveSpec {F("_BlendModePreserveSpecular")} metallic {F("_Metallic")} smooth {F("_Smoothness")} envRefl {F("_EnvironmentReflections")} " +
                              $"base {C("_BaseColor")} emission {C("_EmissionColor")} keywords [{string.Join(" ", m.shaderKeywords)}]");
            }
            sb.AppendLine("Colliders (enabled, active):");
            foreach (Collider c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(false)))
                if (c.enabled && c.bounds.Intersects(box))
                    sb.AppendLine($"  {(c.isTrigger ? "[trigger] " : "")}{PathOf(c.transform)}  {c.GetType().Name}  {B(c.bounds)}");
            // Places with no renderer or collider (a waiting spot, a seat's stand point, a slot): every object whose own
            // position is in the box, with its components. [off] = inactive in the hierarchy.
            sb.AppendLine("Objects standing in the box (by their own position, with their components):");
            foreach (Transform t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            {
                if (!box.Contains(t.position)) continue;
                Component[] parts = t.GetComponents<Component>();
                if (parts.Length > 1 && parts.All(p => p is Transform || p is MeshFilter || p is Renderer || p is Collider)) continue;   // listed above
                string kinds = parts.Length == 1 ? "a bare transform (a point something else uses)" : string.Join(", ", parts.Where(p => p != null && !(p is Transform)).Select(p =>
                    p is Behaviour b && !b.enabled ? p.GetType().Name + " (disabled)" : p.GetType().Name));
                if (parts.Length == 1 && t.parent != null)
                    kinds += "; its parent has " + string.Join(", ", t.parent.GetComponents<Component>().Where(p => p != null && !(p is Transform)).Select(p => p.GetType().Name));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}{1}  at ({2:0.000}, {3:0.000}, {4:0.000})  yaw {5:0}  [{6}]",
                    t.gameObject.activeInHierarchy ? "" : "[off] ", PathOf(t), t.position.x, t.position.y, t.position.z, t.eulerAngles.y, kinds));
            }
        }
        catch (Exception e) { sb.AppendLine("PROBE FAILED: " + e.Message); }
        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[Probe] " + output);
    }

    // ---------------------------------------------------------------------------
    // PHOTOGRAPH THE PROBE POSES (read-only)
    //
    //   Fixit Fidget > Cafe redesign > Probe - Photograph the probe poses (read-only)
    //
    // Reads Logs/CafeRedesign/probe-poses.txt, one photo a line: name x y z tx ty tz fov (world metres, degrees; "game" alone
    // on a line = the game's overhead camera, CmShopCam), and writes each to Logs/CafeRedesign/probe-photos-<time>/<name>.png
    // with the same capture the steps use (Ace's stand-in hidden). Changes nothing. For looking again after a change without
    // re-running a step, and for the playtesters' still frames of the scene.
    // ---------------------------------------------------------------------------
    const string PhotoMenu = "Fixit Fidget/Cafe redesign/Probe - Photograph the probe poses (read-only)";

    [MenuItem(PhotoMenu)]
    static void Photograph()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeRedesign"));
        string input = Path.Combine(root, "probe-poses.txt");
        string folder = Path.Combine(root, "probe-photos-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var sb = new StringBuilder();
        var hidden = new System.Collections.Generic.List<Renderer>();
        try
        {
            Directory.CreateDirectory(folder);
            GameObject player = GameObject.Find("Player");
            if (player != null)
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                    if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
            foreach (string raw in File.ReadAllLines(input))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string name = string.Concat(w[0].Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-'));
                string path = Path.Combine(folder, name + ".png");
                if (w.Length == 1 && w[0] == "game")
                {
                    GameObject shop = GameObject.Find("CmShopCam");
                    if (shop == null) { sb.AppendLine("game: no CmShopCam"); continue; }
                    CafeSecondPassSteps.Capture(path, shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
                }
                else
                {
                    float[] n = w.Skip(1).Take(7).Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    if (n.Length < 7) { sb.AppendLine($"{name}: needs x y z tx ty tz fov"); continue; }
                    CafeSecondPassSteps.Capture(path, new Vector3(n[0], n[1], n[2]), new Vector3(n[3], n[4], n[5]), n[6], false);
                }
                sb.AppendLine($"{name}: {path}");
            }
        }
        catch (Exception e) { sb.AppendLine("PHOTOS FAILED: " + e.Message); }
        finally { foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false; }
        File.WriteAllText(Path.Combine(root, "probe-photos-last.txt"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[Probe] photos: " + folder);
    }

    [MenuItem(MenuPath, true)]
    [MenuItem(PhotoMenu, true)]
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
