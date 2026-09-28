using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// NIGHT WALK 0 - SURVEY THE NIGHT LIGHTING (read-only)
//
// Night step 4, "Night walk" (claude/night-city-proposal.md), lights the 9 blocks round
// the café for the night. Before anything is built, this lists what the night has to
// work with, and changes nothing in the scene:
//
//   * the streets (road surfaces) and the box the 9 blocks fill;
//   * every Light already in the scene;
//   * every lamp-like object in the box (street lamps, lanterns, wall lights), with the
//     point its bulb is most likely at (the top of its bounds);
//   * the materials the buildings in the box use, and whether their shaders can glow
//     (an emission colour and map), which decides how windows get lit at night;
//   * signs, billboards and glass;
//   * the daylight script's own lamps and glow, the café's opening hours;
//   * the post-processing volumes (bloom and tone mapping matter at night);
//   * the POLYGON shop fronts that could be the one late spot.
//
// Report, CSVs and nothing else go to Logs/Night/night-survey-<time>/.
internal static class NightWalkSurvey
{
    const string Tag = "[Night walk] ";
    const string Menu = "Fixit Fidget/Night/Night walk 0 - Survey the night lighting (read-only)";

    // Generous on purpose: the report's street lines settle the real box.
    static readonly Rect Box = Rect.MinMaxRect(-75f, -70f, 75f, 85f);

    static readonly string[] LampWords = { "lamp", "lightpole", "light_pole", "streetlight", "street light", "street_light",
                                           "lantern", "sconce", "bulb", "light_attachment", "lightpost", "light post" };
    static readonly string[] SignWords = { "sign", "billboard", "neon", "poster", "menu board" };
    static readonly string[] GlassWords = { "glass", "window" };

    [MenuItem(Menu)]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(Tag + "Run the survey in Edit Mode (it reads the saved scene).");
            return;
        }
        var report = new StringBuilder();
        try
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "night-survey-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)
                .Where(r => r != null && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToList();
            var inBox = renderers.Where(r => Inside(r.bounds.center)).ToList();
            report.AppendLine($"Night walk survey, {DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
            report.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");
            report.AppendLine($"Box surveyed: x {Box.xMin:0}..{Box.xMax:0}, z {Box.yMin:0}..{Box.yMax:0}. Renderers: {renderers.Count} in the scene, {inBox.Count} in the box.");
            report.AppendLine();

            Streets(report, inBox);
            var lights = Lights(report, folder);
            Lamps(report, folder, inBox, lights);
            Materials(report, folder, inBox);
            Signs(report, inBox);
            Daylight(report);
            Volumes(report);
            Pipeline(report);
            Shops(report, inBox);

            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(Tag + "Survey written to " + folder + "\n" + report.ToString().Substring(0, Math.Min(4000, report.Length)));
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Survey FAILED (it changes nothing, so nothing needs undoing): " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // ---------- the streets ----------
    static void Streets(StringBuilder report, List<Renderer> inBox)
    {
        report.AppendLine("== STREETS (renderers named road / street / crossing, flat and wide) ==");
        var roads = inBox.Where(r => Has(Label(r), "road", "street", "crossing", "asphalt", "tarmac")
                                     && r.bounds.size.y < 1.2f && Mathf.Max(r.bounds.size.x, r.bounds.size.z) > 6f).ToList();
        report.AppendLine($"{roads.Count} road-like renderers.");
        // Long thin ones say where the streets run.
        foreach (var r in roads.OrderByDescending(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z)).Take(40))
        {
            Bounds b = r.bounds;
            string run = b.size.x > b.size.z * 2.5f ? "east-west" : b.size.z > b.size.x * 2.5f ? "north-south" : "patch";
            report.AppendLine($"  {run,-11} x {b.min.x,7:0.0}..{b.max.x,7:0.0}  z {b.min.z,7:0.0}..{b.max.z,7:0.0}  y {b.max.y,5:0.00}  {PathOf(r.transform)}");
        }
        report.AppendLine();
    }

    // ---------- lights already in the scene ----------
    static List<Light> Lights(StringBuilder report, string folder)
    {
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Where(l => l != null).ToList();
        report.AppendLine($"== LIGHTS ALREADY IN THE SCENE ({lights.Count}) ==");
        var csv = new StringBuilder("path,type,enabled,active,x,y,z,intensity,range,spot,r,g,b,shadows,mode\n");
        foreach (var l in lights.OrderBy(l => PathOf(l.transform)))
        {
            Vector3 p = l.transform.position;
            report.AppendLine($"  {l.type,-11} {(l.enabled && l.gameObject.activeInHierarchy ? "on " : "off")} int {l.intensity,6:0.00} range {l.range,5:0.0} " +
                              $"shadows {l.shadows,-4} {Fmt(p)}  {PathOf(l.transform)}");
            csv.AppendLine(string.Join(",", Csv(PathOf(l.transform)), l.type, l.enabled, l.gameObject.activeInHierarchy,
                F(p.x), F(p.y), F(p.z), F(l.intensity), F(l.range), F(l.spotAngle), F(l.color.r), F(l.color.g), F(l.color.b), l.shadows, l.lightmapBakeType));
        }
        File.WriteAllText(Path.Combine(folder, "lights.csv"), csv.ToString());
        report.AppendLine();
        return lights;
    }

    // ---------- lamps ----------
    static void Lamps(StringBuilder report, string folder, List<Renderer> inBox, List<Light> lights)
    {
        var lamps = inBox.Where(r => Has(Label(r), LampWords)).ToList();
        report.AppendLine($"== LAMP-LIKE OBJECTS IN THE BOX ({lamps.Count} renderers) ==");
        var csv = new StringBuilder("path,source,mesh,cx,cy,cz,sx,sy,sz,topx,topy,topz,materials,shaders,nearestLight\n");
        // One line per kind: how many, how tall, what they're made of.
        foreach (var g in lamps.GroupBy(r => Source(r)).OrderByDescending(g => g.Count()))
        {
            var first = g.First();
            float h = g.Average(r => r.bounds.size.y);
            report.AppendLine($"  {g.Count(),3} x {g.Key}  (about {h:0.0} m tall; materials: {string.Join(", ", first.sharedMaterials.Where(m => m != null).Select(m => m.name).Distinct())})");
        }
        report.AppendLine("  Each one (the top of its bounds is where a bulb most likely is):");
        foreach (var r in lamps.OrderBy(r => r.bounds.center.x).ThenBy(r => r.bounds.center.z))
        {
            Bounds b = r.bounds;
            Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
            Light near = lights.OrderBy(l => (l.transform.position - top).sqrMagnitude).FirstOrDefault();
            float nearD = near != null ? Vector3.Distance(near.transform.position, top) : -1f;
            report.AppendLine($"    {Fmt(b.center)} size {Fmt(b.size)}  {Source(r)}  {PathOf(r.transform)}" +
                              (near != null && nearD < 3f ? $"  (a light {nearD:0.0} m away: {near.name})" : ""));
            csv.AppendLine(string.Join(",", Csv(PathOf(r.transform)), Csv(Source(r)), Csv(MeshName(r)),
                F(b.center.x), F(b.center.y), F(b.center.z), F(b.size.x), F(b.size.y), F(b.size.z), F(top.x), F(top.y), F(top.z),
                Csv(string.Join("|", r.sharedMaterials.Where(m => m != null).Select(m => m.name))),
                Csv(string.Join("|", r.sharedMaterials.Where(m => m != null && m.shader != null).Select(m => m.shader.name).Distinct())),
                Csv(near != null && nearD < 3f ? near.name : "")));
        }
        File.WriteAllText(Path.Combine(folder, "lamps.csv"), csv.ToString());
        report.AppendLine();
    }

    // ---------- materials: can the buildings glow? ----------
    static void Materials(StringBuilder report, string folder, List<Renderer> inBox)
    {
        report.AppendLine("== MATERIALS IN THE BOX (tall things first: buildings) ==");
        var csv = new StringBuilder("material,shader,renderers,tall,emissionColor,emissionMap,baseMap,keywords\n");
        var uses = new Dictionary<Material, (int all, int tall)>();
        foreach (var r in inBox)
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                uses.TryGetValue(m, out var u);
                bool tall = r.bounds.size.y > 3f;
                uses[m] = (u.all + 1, u.tall + (tall ? 1 : 0));
            }
        foreach (var kv in uses.OrderByDescending(k => k.Value.tall).ThenByDescending(k => k.Value.all))
        {
            Material m = kv.Key;
            string shader = m.shader != null ? m.shader.name : "(none)";
            bool ec = m.HasProperty("_EmissionColor"), em = m.HasProperty("_EmissionMap");
            string tex = TextureName(m);
            report.AppendLine($"  {kv.Value.all,4} renderers ({kv.Value.tall,3} tall)  {m.name}  [{shader}]  emission colour {(ec ? "yes" : "no")}, map {(em ? "yes" : "no")}, base {tex}");
            csv.AppendLine(string.Join(",", Csv(m.name), Csv(shader), kv.Value.all, kv.Value.tall, ec, em, Csv(tex), Csv(string.Join(" ", m.shaderKeywords))));
        }
        File.WriteAllText(Path.Combine(folder, "materials.csv"), csv.ToString());
        // Shaders: every property a night material could use.
        report.AppendLine("  Shaders used by tall things, and their colour/texture properties:");
        foreach (var shader in uses.Where(k => k.Value.tall > 0 && k.Key.shader != null).Select(k => k.Key.shader).Distinct())
        {
            var props = new List<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var type = shader.GetPropertyType(i);
                if (type == UnityEngine.Rendering.ShaderPropertyType.Color || type == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    props.Add(shader.GetPropertyName(i));
            }
            report.AppendLine($"    {shader.name}: {string.Join(", ", props)}");
        }
        report.AppendLine();

        report.AppendLine("== GLASS AND WINDOWS (materials named glass / window) ==");
        foreach (var kv in uses.Where(k => Has(k.Key.name.ToLowerInvariant(), GlassWords)).OrderByDescending(k => k.Value.all))
            report.AppendLine($"  {kv.Value.all,4} renderers  {kv.Key.name}  [{(kv.Key.shader != null ? kv.Key.shader.name : "")}]");
        report.AppendLine();
    }

    // ---------- signs ----------
    static void Signs(StringBuilder report, List<Renderer> inBox)
    {
        var signs = inBox.Where(r => Has(Label(r), SignWords)).ToList();
        report.AppendLine($"== SIGNS, BILLBOARDS AND POSTERS IN THE BOX ({signs.Count}) ==");
        foreach (var g in signs.GroupBy(r => Source(r)).OrderByDescending(g => g.Count()))
            report.AppendLine($"  {g.Count(),3} x {g.Key}  e.g. {Fmt(g.First().bounds.center)}  materials: {string.Join(", ", g.First().sharedMaterials.Where(m => m != null).Select(m => m.name).Distinct())}");
        report.AppendLine();
    }

    // ---------- the daylight script and the day's hours ----------
    static void Daylight(StringBuilder report)
    {
        report.AppendLine("== DAYLIGHT AND HOURS ==");
        var daylight = UnityEngine.Object.FindAnyObjectByType<CafeDaylight>(FindObjectsInactive.Include);
        if (daylight == null) report.AppendLine("  No CafeDaylight in the scene.");
        else
        {
            report.AppendLine($"  CafeDaylight on {PathOf(daylight.transform)}: sun {(daylight.sun != null ? PathOf(daylight.sun.transform) : "-")}, sky {(daylight.sky != null ? daylight.sky.name : "-")}");
            report.AppendLine($"  daylight intensity {daylight.daylightIntensity:0.00}, daytime lamp strength {daylight.daytimeLampStrength:0.00}, evening glow {daylight.eveningGlow}, haze {daylight.hazeStart:0}..{daylight.hazeEnd:0} m ({(daylight.useDistanceHaze ? "on" : "off")})");
            report.AppendLine($"  warm lights ({daylight.warmLights.Length}): {string.Join("; ", daylight.warmLights.Where(l => l != null).Select(l => $"{PathOf(l.transform)} {l.intensity:0.00}"))}");
            report.AppendLine($"  glowing renderers ({daylight.emissiveRenderers.Length}): {string.Join("; ", daylight.emissiveRenderers.Where(r => r != null).Select(r => PathOf(r.transform)))}");
            report.AppendLine($"  glowing materials ({daylight.emissiveMaterials.Length}): {string.Join("; ", daylight.emissiveMaterials.Where(m => m != null).Select(m => m.name))}");
            foreach (float hour in new[] { 9f, 17f, 19f, 21f, 23f, 1f, 4f, 5.5f })
            {
                var s = CafeDaylight.EvaluateAtHour(hour);
                report.AppendLine($"    {hour,5:0.0}h: night {s.night:0.00}, dusk {s.dusk:0.00}, sun altitude {s.altitude,6:0.0} deg, sun strength {s.sunStrength:0.00}, sky exposure {s.skyExposure:0.00}, ambient sky {s.ambientSky}");
            }
        }
        var clock = UnityEngine.Object.FindAnyObjectByType<DayClock>(FindObjectsInactive.Include);
        if (clock != null)
            report.AppendLine($"  DayClock: opens {clock.OpeningHour:0.00}h, closes {clock.ClosingHour:0.00}h.");
        report.AppendLine($"  RenderSettings: ambient {RenderSettings.ambientMode}, fog {(RenderSettings.fog ? "on" : "off")}, skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "-")}, sun {(RenderSettings.sun != null ? RenderSettings.sun.name : "-")}");
        report.AppendLine();
    }

    // ---------- post-processing ----------
    static void Volumes(StringBuilder report)
    {
        report.AppendLine("== POST-PROCESSING VOLUMES ==");
        foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            var p = v.sharedProfile;
            report.AppendLine($"  {PathOf(v.transform)}: {(v.isGlobal ? "global" : "local")}, weight {v.weight:0.00}, priority {v.priority:0.0}, profile {(p != null ? p.name : "-")}");
            if (p == null) continue;
            foreach (var c in p.components)
            {
                string extra = "";
                if (c is Bloom bloom) extra = $" threshold {bloom.threshold.value:0.00}, intensity {bloom.intensity.value:0.00}, scatter {bloom.scatter.value:0.00}";
                else if (c is Tonemapping tm) extra = $" mode {tm.mode.value}";
                else if (c is ColorAdjustments ca) extra = $" post exposure {ca.postExposure.value:0.00}, contrast {ca.contrast.value:0}, saturation {ca.saturation.value:0}";
                else if (c is Vignette vg) extra = $" intensity {vg.intensity.value:0.00}";
                report.AppendLine($"    {c.GetType().Name} ({(c.active ? "on" : "off")}){extra}");
            }
        }
        report.AppendLine();
    }

    static void Pipeline(StringBuilder report)
    {
        report.AppendLine("== RENDER PIPELINE ==");
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) { report.AppendLine("  Not URP?"); report.AppendLine(); return; }
        report.AppendLine($"  {urp.name}: HDR {urp.supportsHDR}, shadow distance {urp.shadowDistance:0}, " +
                          $"additional light shadows {urp.supportsAdditionalLightShadows}, max additional lights per object {urp.maxAdditionalLightsCount}");
        report.AppendLine();
    }

    // ---------- the late spot ----------
    static void Shops(StringBuilder report, List<Renderer> inBox)
    {
        report.AppendLine("== SHOP FRONTS IN THE BOX (candidates for the one late spot) ==");
        var roots = new HashSet<GameObject>();
        foreach (var r in inBox)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (root == null) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (src != null && src.name.StartsWith("SM_Bld_Shop", StringComparison.OrdinalIgnoreCase)) roots.Add(root);
        }
        foreach (var root in roots.OrderBy(g => g.transform.position.x).ThenBy(g => g.transform.position.z))
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
            Vector3 f = root.transform.forward;
            report.AppendLine($"  {src.name,-24} at {Fmt(root.transform.position)} facing ({f.x:0.00}, {f.z:0.00})  {PathOf(root.transform)}");
        }
        report.AppendLine();
    }

    // ---------- helpers ----------
    static bool Inside(Vector3 p) => Box.Contains(new Vector2(p.x, p.z));

    static string Label(Renderer r)
    {
        var sb = new StringBuilder();
        sb.Append(r.name).Append(' ');
        if (r.transform.parent != null) sb.Append(r.transform.parent.name).Append(' ');
        sb.Append(MeshName(r)).Append(' ').Append(Source(r));
        return sb.ToString().ToLowerInvariant();
    }

    static string MeshName(Renderer r)
    {
        var f = r.GetComponent<MeshFilter>();
        if (f != null && f.sharedMesh != null) return f.sharedMesh.name;
        if (r is SkinnedMeshRenderer s && s.sharedMesh != null) return s.sharedMesh.name;
        return "";
    }

    static string Source(Renderer r)
    {
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
        var src = root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        return src != null ? src.name : "(" + (MeshName(r).Length > 0 ? MeshName(r) : r.name) + ")";
    }

    static string TextureName(Material m)
    {
        Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
        return t != null ? t.name : "-";
    }

    static bool Has(string text, params string[] words)
    {
        foreach (var w in words) if (text.Contains(w)) return true;
        return false;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string Fmt(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
}
