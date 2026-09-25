#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Read-only survey of the purchased POLYGON City pack (Synty). Every prefab is
// loaded into an isolated preview scene - never the cafe scene - measured, and
// photographed from both of its long sides, so placement code can align
// building fronts to the street by measurement rather than by guesswork.
// Output: <project>/Logs/CityPack/catalog-<time>/ (contact sheets + JSON).
public static class CityPackCatalog
{
    const string Menu = "Fixit Fidget/City pack/";
    const string Tag = "[City pack] ";
    internal static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CityPack"));

    static readonly (string label, string[] folders)[] Sets =
    {
        ("buildings", new[] { "Assets/Synty/PolygonCity/Prefabs/Buildings" }),
        ("generic-buildings", new[] { "Assets/Synty/PolygonGeneric/Prefabs/Building" }),
        ("environments", new[] { "Assets/Synty/PolygonCity/Prefabs/Environments" }),
        ("props", new[] { "Assets/Synty/PolygonCity/Prefabs/Props" }),
        ("generic-props", new[] { "Assets/Synty/PolygonGeneric/Prefabs/Props" }),
        ("vehicles", new[] { "Assets/Synty/PolygonCity/Prefabs/Vehicles" }),
        ("characters", new[] { "Assets/Synty/PolygonCity/Prefabs/Characters", "Assets/Synty/PolygonGeneric/Prefabs/Characters" }),
        ("generic-environment", new[] { "Assets/Synty/PolygonGeneric/Prefabs/Environment" }),
    };

    const int View = 150, Columns = 6, Rows = 8, PerPage = Columns * Rows;

    [MenuItem(Menu + "Catalog - measure and photograph the pack")]
    static void Catalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play Mode first."); return; }
        string folder = Path.Combine(LogRoot, "catalog-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var summary = new StringBuilder();
        var preview = new PreviewRenderUtility();
        try
        {
            preview.camera.fieldOfView = 28f;
            preview.camera.nearClipPlane = .05f;
            preview.camera.farClipPlane = 3000f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.80f, .82f, .85f);
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(42f, 35f, 0f);
            preview.lights[1].intensity = .55f;
            preview.lights[1].transform.rotation = Quaternion.Euler(18f, 215f, 0f);
            preview.ambientColor = new Color(.42f, .42f, .46f);

            foreach (var set in Sets)
            {
                var paths = set.folders.Where(AssetDatabase.IsValidFolder)
                    .SelectMany(f => AssetDatabase.FindAssets("t:Prefab", new[] { f }))
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
                var json = new StringBuilder("[\n");
                int pages = (paths.Count + PerPage - 1) / PerPage;
                for (int page = 0; page < pages; page++)
                {
                    var sheet = new Texture2D(Columns * View * 2, Rows * View, TextureFormat.RGB24, false);
                    var fill = Enumerable.Repeat(new Color(.25f, .25f, .27f), sheet.width * sheet.height).ToArray();
                    sheet.SetPixels(fill);
                    for (int slot = 0; slot < PerPage; slot++)
                    {
                        int index = page * PerPage + slot;
                        if (index >= paths.Count) break;
                        string entry = Photograph(preview, paths[index], index, sheet, slot);
                        json.Append(entry).Append(index < paths.Count - 1 ? ",\n" : "\n");
                    }
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(folder, set.label + "-" + (page + 1) + ".png"), sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                }
                json.Append("]\n");
                File.WriteAllText(Path.Combine(folder, set.label + ".json"), json.ToString());
                summary.AppendLine(set.label + ": " + paths.Count + " prefabs on " + pages + " sheet(s)");
            }
            Debug.Log(Tag + "Catalog written: " + folder + "\n" + summary);
        }
        catch (Exception e) { Debug.LogError(Tag + "Catalog FAILED: " + e.Message + "\n" + e); }
        finally { preview.Cleanup(); }
    }

    // Two views per prefab: A looks at its +Z face, B at its -Z face. The
    // number stamped in each cell is the prefab's index in the JSON list.
    static string Photograph(PreviewRenderUtility preview, string path, int index, Texture2D sheet, int slot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sb = new StringBuilder();
        GameObject instance = null;
        try
        {
            instance = preview.InstantiatePrefabInScene(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var p in instance.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
            var renderers = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            int tris = 0;
            foreach (var f in instance.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
            foreach (var s in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) if (s.sharedMesh != null && s.gameObject.activeInHierarchy) tris += s.sharedMesh.triangles.Length / 3;
            var mats = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name + "|" + (m.shader != null ? m.shader.name : "none")).Distinct().Take(4);
            int colliders = instance.GetComponentsInChildren<Collider>(true).Length;
            int lights = instance.GetComponentsInChildren<Light>(true).Length;
            bool animator = instance.GetComponentInChildren<Animator>(true) != null;
            int col = slot % Columns, row = Rows - 1 - slot / Columns;
            for (int side = 0; side < 2; side++)
            {
                float radius = Mathf.Max(.2f, b.extents.magnitude);
                Vector3 dir = new Vector3(.62f, .5f, side == 0 ? 1f : -1f).normalized;
                float distance = radius / Mathf.Sin(preview.camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.02f;
                preview.camera.transform.position = b.center + dir * distance;
                preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane = Mathf.Max(.01f, distance - radius * 2f);
                preview.camera.farClipPlane = distance + radius * 2f;
                preview.BeginStaticPreview(new Rect(0, 0, View, View));
                preview.Render(true);
                var shot = preview.EndStaticPreview();
                sheet.SetPixels((col * 2 + side) * View, row * View, View, View, shot.GetPixels());
                Object.DestroyImmediate(shot);
            }
            Stamp(sheet, col * 2 * View + 4, row * View + View - 4, index);
            sb.Append("  {\"i\":").Append(index)
              .Append(",\"name\":\"").Append(Path.GetFileNameWithoutExtension(path)).Append('"')
              .Append(",\"path\":\"").Append(path).Append('"')
              .Append(",\"size\":").Append(V(b.size))
              .Append(",\"center\":").Append(V(b.center))
              .Append(",\"min\":").Append(V(b.min))
              .Append(",\"tris\":").Append(tris)
              .Append(",\"renderers\":").Append(renderers.Length)
              .Append(",\"colliders\":").Append(colliders)
              .Append(",\"lights\":").Append(lights)
              .Append(",\"animator\":").Append(animator ? "true" : "false")
              .Append(",\"materials\":[").Append(string.Join(",", mats.Select(m => "\"" + m.Replace("\"", "'") + "\""))).Append("]}");
        }
        catch (Exception e)
        {
            sb.Append("  {\"i\":").Append(index).Append(",\"name\":\"").Append(Path.GetFileNameWithoutExtension(path))
              .Append("\",\"error\":\"").Append(e.Message.Replace("\"", "'")).Append("\"}");
        }
        finally { if (instance != null) Object.DestroyImmediate(instance); }
        return sb.ToString();
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "[{0:0.###},{1:0.###},{2:0.###}]", v.x, v.y, v.z);

    // 3x5 digit glyphs, drawn 3x scaled, white on black, from the top-left corner.
    static readonly string[] Digits =
    {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
    };

    static void Stamp(Texture2D sheet, int x, int top, int number)
    {
        string text = number.ToString(CultureInfo.InvariantCulture);
        int scale = 3, width = text.Length * 4 * scale + scale, height = 7 * scale;
        for (int px = 0; px < width; px++)
            for (int py = 0; py < height; py++)
                sheet.SetPixel(x + px, top - py, Color.black);
        for (int d = 0; d < text.Length; d++)
        {
            string glyph = Digits[text[d] - '0'];
            for (int gy = 0; gy < 5; gy++)
                for (int gx = 0; gx < 3; gx++)
                    if (glyph[gy * 3 + gx] == '1')
                        for (int sx = 0; sx < scale; sx++)
                            for (int sy = 0; sy < scale; sy++)
                                sheet.SetPixel(x + scale + d * 4 * scale + gx * scale + sx, top - scale - gy * scale - sy, Color.white);
        }
    }
}
#endif
