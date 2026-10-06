#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// NIGHT 0, BEFORE BUILDING: THE BIN PROPS AND THE CAR PARK'S CORNERS (6 Oct 2026; read-only)
//
//   Fixit Fidget > Night > Bins 0 - Photograph the bin props and the car park (read-only)
//
// The man at the bins (claude/the-man-at-the-bins-story.md) lives in the car park's back corner. Before
// the set is built, this photographs what there is to build it from and where it would stand:
//   * each candidate prop (POLYGON City's skips, bins, bags, doors, the wall light; a crate and a pallet)
//     beside a 1.8 m post, with its size;
//   * the car park from straight above, and its two back corners and the café's back from the night
//     camera's home angle (turn 0, tilt 50, 34 m), by day;
//   * what stands within 4 m of each corner (renderers and colliders), and the building behind it.
// Nothing in the scene changes: the props are temporary copies far below the world, and the photos come
// from a temporary camera. Written to Logs/Night/bins-survey-<time>/.
// ---------------------------------------------------------------------------
internal static class NightZeroSurvey
{
    const string Menu = "Fixit Fidget/Night/Bins 0 - Photograph the bin props and the car park (read-only)";
    const string Tag = "[Bins 0] ";
    const string City = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/Props/";

    static readonly string[] Props =
    {
        City + "SM_Prop_Skip_01.prefab", City + "SM_Prop_Skip_02.prefab",
        City + "SM_Prop_Trashbin_01.prefab", City + "SM_Prop_Trashbin_02.prefab",
        City + "SM_Prop_TrashCan_01.prefab", City + "SM_Prop_TrashCan_Lid_01.prefab",
        City + "SM_Prop_TrashBag_01.prefab", City + "SM_Prop_TrashBag_02.prefab", City + "SM_Prop_TrashBag_03.prefab",
        City + "SM_Prop_Door_01.prefab", City + "SM_Prop_Door_02.prefab",
        City + "SM_Prop_Light_Attachment_01.prefab", City + "SM_Prop_Pallet_01.prefab",
        City + "SM_Prop_CardboardBox_01.prefab", City + "SM_Prop_Cone_01.prefab",
        Gen + "SM_Gen_Prop_Crate_02.prefab", Gen + "SM_Gen_Prop_Crate_03.prefab",
    };

    // The car park's back corners (against the terrace south of it) and, for comparison, the café's back.
    static readonly (string name, Vector3 at)[] Spots =
    {
        ("car park, back corner east", new Vector3(8.6f, 0f, -22.4f)),
        ("car park, back corner west", new Vector3(-7.2f, 0f, -22.4f)),
        ("cafe, back pavement east", new Vector3(6.2f, 0f, 19.4f)),
    };

    static readonly Vector3 Away = new Vector3(600f, -400f, 600f);

    [MenuItem(Menu)]
    static void Run()
    {
        var made = new List<Object>();
        try
        {
            CityPackChecks.RequireScene();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "bins-survey-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            report.AppendLine("Bins 0: the bin props and the car park's corners (read-only; nothing in the scene changed).");
            report.AppendLine();

            // ---- the props, one at a time, beside a 1.8 m post ----
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var postMaterial = new Material(unlit != null ? unlit : Shader.Find("Unlit/Color")) { hideFlags = HideFlags.HideAndDontSave, color = new Color(.85f, .2f, .2f) };
            made.Add(postMaterial);
            int n = 0;
            foreach (string path in Props)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { report.AppendLine($"  {Path.GetFileNameWithoutExtension(path)}: MISSING ({path})"); continue; }
                GameObject copy = Object.Instantiate(prefab, Away, Quaternion.identity);
                Hide(copy);
                made.Add(copy);
                Bounds b = BoundsOf(copy);
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Hide(post);
                made.Add(post);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.GetComponent<Renderer>().sharedMaterial = postMaterial;
                post.transform.localScale = new Vector3(.08f, 1.8f, .08f);
                post.transform.position = new Vector3(b.max.x + .45f, Away.y + .9f, b.center.z);
                var children = copy.GetComponentsInChildren<Transform>(true).Where(t => t != copy.transform).Select(t => t.name).ToArray();
                var meshes = copy.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh != null ? f.sharedMesh.name : "-").ToArray();
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}: {1:0.00} wide (x) x {2:0.00} tall x {3:0.00} deep (z); bottom {4:0.00}, centre offset ({5:0.00}, {6:0.00}, {7:0.00}); meshes [{8}]; children [{9}]",
                    prefab.name, b.size.x, b.size.y, b.size.z, b.min.y - Away.y, b.center.x - Away.x, b.center.y - Away.y, b.center.z - Away.z,
                    string.Join(", ", meshes), string.Join(", ", children)));
                Bounds both = b;
                both.Encapsulate(post.GetComponent<Renderer>().bounds);
                float size = Mathf.Max(both.size.x, both.size.y, both.size.z, .6f);
                Vector3 look = both.center;
                Vector3 from = look + new Vector3(-.75f, .55f, -1f).normalized * size * 2.6f;
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"prop-{++n:00}-{prefab.name}.png"), from, look, 30f, false);
                copy.SetActive(false);
                post.SetActive(false);
            }
            report.AppendLine();

            // ---- the car park and the corners, by day ----
            CafeSecondPassSteps.Capture(Path.Combine(folder, "map-1-car-park-from-above.png"), new Vector3(1f, 46f, -17.9f), new Vector3(1f, 0f, -18f), 40f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "map-2-cafe-and-car-park-from-above.png"), new Vector3(1f, 78f, -2.1f), new Vector3(1f, 0f, -2f), 45f, false);
            int k = 0;
            foreach (var (name, at) in Spots)
            {
                Vector3 focus = at + Vector3.up;
                Quaternion home = Quaternion.Euler(50f, 0f, 0f);
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"spot-{++k}a-{Slug(name)}-home-view.png"), focus - home * Vector3.forward * 34f, focus, 39f, false);
                Quaternion close = Quaternion.Euler(32f, 200f, 0f);
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"spot-{k}b-{Slug(name)}-close.png"), focus - close * Vector3.forward * 9f, focus, 45f, false);
                report.AppendLine($"Within 4 m of the {name} ({at.x:0.0}, {at.z:0.0}):");
                foreach (string line in Near(at, 4f)) report.AppendLine("  " + line);
                report.AppendLine();
            }

            // ---- the building behind the car park ----
            Transform terrace = Roots().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Building - south block terrace");
            if (terrace != null)
            {
                Bounds tb = BoundsOf(terrace.gameObject);
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "The terrace south of the car park: x {0:0.00} to {1:0.00}, z {2:0.00} to {3:0.00}, {4:0.0} m tall.",
                    tb.min.x, tb.max.x, tb.min.z, tb.max.z, tb.max.y));
            }
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(Tag + "Photographed " + n + " props and " + Spots.Length + " spots: " + folder);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The survey failed: " + e.Message + "\n" + e);
        }
        finally
        {
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
        }
    }

    static IEnumerable<GameObject> Roots() => SceneManager.GetActiveScene().GetRootGameObjects();

    // Renderers and colliders whose bounds come within <radius> of the spot (on the ground), nearest first.
    static IEnumerable<string> Near(Vector3 at, float radius)
    {
        var rows = new List<(float d, string text)>();
        foreach (GameObject root in Roots())
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                float d = Flat(r.bounds, at);
                if (d <= radius) rows.Add((d, string.Format(CultureInfo.InvariantCulture, "{0:0.0} m  renderer {1} ({2}): x {3:0.0}..{4:0.0}, z {5:0.0}..{6:0.0}, top {7:0.0}",
                    d, PathOf(r.transform), r.gameObject.activeInHierarchy && r.enabled ? "on" : "off", r.bounds.min.x, r.bounds.max.x, r.bounds.min.z, r.bounds.max.z, r.bounds.max.y)));
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                float d = Flat(c.bounds, at);
                if (d <= radius) rows.Add((d, string.Format(CultureInfo.InvariantCulture, "{0:0.0} m  collider {1} ({2}{3})", d, PathOf(c.transform),
                    c.enabled && c.gameObject.activeInHierarchy ? "on" : "off", c.isTrigger ? ", trigger" : "")));
            }
        }
        return rows.OrderBy(r => r.d).Take(40).Select(r => r.text);
    }

    static float Flat(Bounds b, Vector3 at)
    {
        float dx = Mathf.Max(0f, Mathf.Max(b.min.x - at.x, at.x - b.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(b.min.z - at.z, at.z - b.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Hide(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null && parts.Count < 4; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string Slug(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Replace("--", "-").Trim('-');

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
