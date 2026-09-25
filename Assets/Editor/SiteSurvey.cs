#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Measures the café's surroundings for planning: straight-down orthographic
// photos with known scale, and the walkable NavMesh as a list of triangles.
// Read-only: nothing in the scene changes.
//
// Output: <project>/Logs/Site/survey-<time>/
//   top-wide.png   x -64..64, z -64..64 at 10 px per metre (north = +z = up)
//   top-cafe.png   x -32..32, z -28..36 at 20 px per metre
//   navmesh.txt    "v x y z" and "t a b c area" lines
public static class SiteSurvey
{
    private const string Menu = "Fixit Fidget/Site/Survey - top-down photos and NavMesh map";

    [MenuItem(Menu)]
    private static void Run()
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", "survey-" + stamp);
        Directory.CreateDirectory(folder);
        TopDown(Path.Combine(folder, "top-wide.png"), new Rect(-64f, -64f, 128f, 128f), 10);
        TopDown(Path.Combine(folder, "top-cafe.png"), new Rect(-32f, -28f, 64f, 64f), 20);
        WriteNavMesh(Path.Combine(folder, "navmesh.txt"));
        WriteBlocks(Path.Combine(folder, "blocks.txt"));
        Debug.Log("[Site survey] Written to " + folder);
    }

    [MenuItem("Fixit Fidget/Site/Photograph - default view and the four sides")]
    private static void Photograph()
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", "photos-" + stamp);
        Directory.CreateDirectory(folder);
        Vector3 cafe = new Vector3(0f, 0f, 9f);
        GameObject shop = GameObject.Find("CmShopCam");
        if (shop != null)
            CafeSecondPassSteps.Capture(Path.Combine(folder, "0-default.png"), shop.transform.position,
                shop.transform.position + shop.transform.forward * 30f, 39f, true);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "1-from-south.png"), new Vector3(0f, 22f, -34f), cafe + Vector3.back * 6f, 50f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "2-from-west.png"), new Vector3(-40f, 22f, 9f), cafe + Vector3.left * 8f, 50f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "3-from-east.png"), new Vector3(40f, 22f, 9f), cafe + Vector3.right * 8f, 50f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "4-from-north.png"), new Vector3(0f, 22f, 52f), cafe + Vector3.forward * 8f, 50f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "5-high-south-west.png"), new Vector3(-46f, 48f, -40f), new Vector3(-10f, 0f, -4f), 55f, false);
        Debug.Log("[Site photos] Written to " + folder);
    }

    [MenuItem("Fixit Fidget/Site/Photograph - café back counter")]
    private static void PhotographBackCounter()
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", "back-counter-" + stamp);
        Directory.CreateDirectory(folder);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "1-straight-on.png"), new Vector3(0f, 1.7f, 11.2f), new Vector3(0f, 1.3f, 17.9f), 62f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "2-drink-side.png"), new Vector3(-0.6f, 1.6f, 14.9f), new Vector3(2.4f, 1.1f, 17.2f), 60f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "3-bench-side.png"), new Vector3(0.8f, 1.6f, 14.9f), new Vector3(-2.6f, 1.1f, 17.2f), 60f, false);
        CafeSecondPassSteps.Capture(Path.Combine(folder, "4-above.png"), new Vector3(0f, 4.2f, 12.6f), new Vector3(0f, 0.8f, 16.8f), 58f, true);
        Debug.Log("[Site photos] Back counter written to " + folder);
    }

    // The back counters from every side, the player's placeholder body hidden,
    // plus a measured plan (cut below the ceiling) of the area behind the counter.
    [MenuItem("Fixit Fidget/Site/Photograph - back counters, all angles and plan")]
    private static void PhotographBackCounters()
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", "back-counters-" + stamp);
        Directory.CreateDirectory(folder);
        var hidden = new System.Collections.Generic.List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (var r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try
        {
            CafeSecondPassSteps.Capture(Path.Combine(folder, "1-front-left.png"), new Vector3(-3.6f, 2.5f, 12.6f), new Vector3(1.6f, 1.0f, 17.2f), 58f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "2-front-right.png"), new Vector3(5.2f, 2.5f, 12.8f), new Vector3(-0.6f, 1.0f, 17.3f), 58f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "3-junction-close.png"), new Vector3(0.6f, 1.75f, 15.1f), new Vector3(1.8f, 1.0f, 17.1f), 55f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "4-straight-on.png"), new Vector3(0f, 1.75f, 12.4f), new Vector3(0f, 1.25f, 18f), 64f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "5-drink-counter-back.png"), new Vector3(5.6f, 1.9f, 17.6f), new Vector3(1.5f, 0.9f, 16.6f), 60f, false);
            PlanBelowCeiling(Path.Combine(folder, "6-plan.png"), new Rect(-7.5f, 12.5f, 15f, 6f), 2.9f, 80);
        }
        finally { foreach (var r in hidden) if (r != null) r.forceRenderingOff = false; }
        Debug.Log("[Site photos] Back counters written to " + folder + " (plan: x -7.5..7.5, z 12.5..18.5, 80 px per metre, north up)");
    }

    // Straight down from 'cut' metres, so the ceiling and lights above the cut are left out.
    private static void PlanBelowCeiling(string path, Rect area, float cut, int pixelsPerMetre)
    {
        int width = Mathf.RoundToInt(area.width * pixelsPerMetre), height = Mathf.RoundToInt(area.height * pixelsPerMetre);
        var host = new GameObject("Temporary plan camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.orthographic = true;
            camera.orthographicSize = area.height * .5f;
            camera.aspect = area.width / area.height;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = cut + 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.SetPositionAndRotation(new Vector3(area.center.x, cut, area.center.y), Quaternion.Euler(90f, 0f, 0f));
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    [MenuItem("Fixit Fidget/Site/List everything on the back wall")]
    private static void ListBackWall()
    {
        var zone = new Bounds(new Vector3(0f, 1.6f, 17.8f), new Vector3(15.2f, 3.4f, 1.4f));
        var text = new StringBuilder();
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (!renderer.bounds.Intersects(zone)) continue;
            Bounds b = renderer.bounds;
            var mesh = renderer.GetComponent<MeshFilter>();
            text.Append(PathOf(renderer.transform))
                .Append(renderer.enabled && renderer.gameObject.activeInHierarchy ? "" : " (hidden)")
                .Append(" | x ").Append(F(b.min.x)).Append("..").Append(F(b.max.x))
                .Append(" y ").Append(F(b.min.y)).Append("..").Append(F(b.max.y))
                .Append(" z ").Append(F(b.min.z)).Append("..").Append(F(b.max.z))
                .Append(" | mesh ").Append(mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.name + " @" + AssetDatabase.GetAssetPath(mesh.sharedMesh) : "-")
                .Append(" | materials ").Append(string.Join(", ", System.Array.ConvertAll(renderer.sharedMaterials, m => m != null ? m.name : "none")))
                .Append('\n');
        }
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string path = System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Site", "back-wall-" + stamp + ".txt");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        File.WriteAllText(path, text.ToString());
        Debug.Log("[Site] Back wall renderers written to " + path);
    }

    private static string PathOf(Transform t)
    {
        string result = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) result = p.name + "/" + result;
        return result;
    }

    // Combined renderer bounds of every child of the big scenery groups, so a
    // plan can be drawn with real numbers.
    private static void WriteBlocks(string path)
    {
        var text = new StringBuilder();
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform group in root.transform)
                foreach (Transform child in group)
                {
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(false);
                    if (renderers.Length == 0) continue;
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    int colliders = child.GetComponentsInChildren<Collider>(false).Length;
                    text.Append(root.name).Append(" / ").Append(group.name).Append(" / ").Append(child.name)
                        .Append(child.gameObject.activeInHierarchy ? "" : " (inactive)")
                        .Append(" | x ").Append(F(bounds.min.x)).Append("..").Append(F(bounds.max.x))
                        .Append(" z ").Append(F(bounds.min.z)).Append("..").Append(F(bounds.max.z))
                        .Append(" y ").Append(F(bounds.min.y)).Append("..").Append(F(bounds.max.y))
                        .Append(" | colliders ").Append(colliders).Append('\n');
                }
        File.WriteAllText(path, text.ToString());
    }

    private static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    private static void TopDown(string path, Rect area, int pixelsPerMetre)
    {
        int width = Mathf.RoundToInt(area.width * pixelsPerMetre), height = Mathf.RoundToInt(area.height * pixelsPerMetre);
        var host = new GameObject("Temporary site survey camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.orthographic = true;
            camera.orthographicSize = area.height * .5f;
            camera.aspect = area.width / area.height;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            // Looking straight down with +z at the top of the image.
            camera.transform.SetPositionAndRotation(new Vector3(area.center.x, 200f, area.center.y), Quaternion.Euler(90f, 0f, 0f));
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    private static void WriteNavMesh(string path)
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        var text = new StringBuilder();
        foreach (Vector3 v in mesh.vertices)
            text.Append("v ").Append(v.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append(v.y.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append(v.z.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
        for (int i = 0; i + 2 < mesh.indices.Length; i += 3)
            text.Append("t ").Append(mesh.indices[i]).Append(' ').Append(mesh.indices[i + 1]).Append(' ')
                .Append(mesh.indices[i + 2]).Append(' ').Append(mesh.areas[i / 3]).Append('\n');
        File.WriteAllText(path, text.ToString());
    }
}
#endif
