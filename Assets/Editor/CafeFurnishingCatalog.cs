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

// Furnishing pass, step 0 (24 Sept): measure the pieces before placing them.
//
// For every candidate asset already in the project (our own furniture library
// in Assets/Art/Models, the reading bookcase, and the POLYGON / Kenney props we
// might dress with) this records its size, its lowest point, which way a seat
// faces (worked out from where the backrest's vertices are, so nothing has to be
// guessed from a thumbnail), its triangle count and its materials, and photographs
// every piece from the front and from the game camera's angle.
//
// Read-only: the pieces are built as hidden, unsaved objects far above the city
// and destroyed again. Nothing is added to the scene. Results go to
// <project>/Logs/CafeFurnishing/catalog-<time>/ (git ignores Logs).
public static class CafeFurnishingCatalog
{
    const string Menu = "Fixit Fidget/Cafe furnishing/";

    internal static readonly string[] Candidates =
    {
        // Our own furniture library (house style, CC_/DC_ materials).
        "Assets/Art/Models/Chair_TubChair.fbx", "Assets/Art/Models/T2_Chair_TubChair.fbx",
        "Assets/Art/Models/Table_Lounge.fbx", "Assets/Art/Models/T2_Table_Lounge.fbx",
        "Assets/Art/Models/Stool_Bar.fbx", "Assets/Art/Models/T2_Stool_Bar.fbx",
        "Assets/Art/Models/Table_Bar.fbx", "Assets/Art/Models/T2_Table_Bar.fbx",
        "Assets/Art/Models/Stool_Low.fbx", "Assets/Art/Models/Bench_Communal.fbx",
        "Assets/Art/Models/Table_Communal.fbx", "Assets/Art/Models/Table_Square2.fbx",
        "Assets/Art/Models/Chair_Spindle.fbx", "Assets/Art/Models/Chair_Cafe.fbx",
        "Assets/Art/Models/MenuBoard.fbx", "Assets/Art/Models/SignAcesCafe.fbx",
        "Assets/Art/Models/Mug.fbx", "Assets/Art/Models/Plant.fbx",
        "Assets/Art/Models/CashRegister.fbx", "Assets/Art/Models/EspressoMachine.fbx",
        "Assets/Playtests/AcesCafeLayout/CafeBookcase.fbx",
        // POLYGON City / Generic props (purchased; git-ignored).
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PotPlant_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PotPlant_02.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PlanterWindow_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PlanterWindow_02.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Planter_02.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_LargeSign_Coffee_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_LargeSign_Donut_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Soda_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_SmartPhone_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Newspaper_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Poster_Frame_01.prefab",
        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_ShopInterior_Shelf_04.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Mug_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Plate_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Food_Bread_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Food_Bread_02.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Sack_Stack_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Sack_04.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Sack_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Pot_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Pot_04.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Pot_05.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_03.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Rope_03.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Cardboard_Box_04.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Bottle_04.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Ivy_Draped_02.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Flowers_05.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Flowers_06.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Fern_01.prefab",
        "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Shrub_01.prefab",
        // Kenney CC0 plants already in the project.
        "Assets/Art/CC0Neighborhood/Kenney/Nature/flower_purpleA.fbx",
        "Assets/Art/CC0Neighborhood/Kenney/Nature/flower_redC.fbx",
        "Assets/Art/CC0Neighborhood/Kenney/Nature/flower_yellowA.fbx",
        "Assets/Art/CC0Neighborhood/Kenney/Nature/plant_bushDetailed.fbx",
        // Repair jobs, to see whether their bodies could serve as display pieces.
        "Assets/AssetsPrefabs/PocketWatch.prefab", "Assets/AssetsPrefabs/PhoneRepair.prefab",
        "Assets/GraceShowcase/GraceReunionCamera.prefab",
    };

    [MenuItem(Menu + "0 - Measure the pieces (sizes, facing, line-up photos)")]
    static void MeasureMenu()
    {
        try { Debug.Log("[Cafe furnishing] Measure: " + Measure()); }
        catch (Exception e) { Debug.LogError("[Cafe furnishing] Measure FAILED: " + e); }
    }

    [MenuItem(Menu + "0 - Measure the pieces (sizes, facing, line-up photos)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    internal static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeFurnishing"));

    static string Measure()
    {
        string folder = Path.Combine(LogRoot, "catalog-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var report = new StringBuilder();
        report.AppendLine("# path\tsize(x,y,z)\tmin\tbackDir(x,z)\ttris\tmaterials\tcomponents");
        var built = new List<(string name, GameObject go, Bounds b)>();
        Vector3 origin = new Vector3(0f, 120f, 0f);
        int column = 0, row = 0;
        try
        {
            foreach (string path in Candidates)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) { report.AppendLine(path + "\tMISSING"); continue; }
                var go = Object.Instantiate(asset);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0) { report.AppendLine(path + "\tNO RENDERERS"); Object.DestroyImmediate(go); continue; }
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                // Our Blender exports arrive Z-up (standing on their backs); the scene
                // stands them up with a -90 degree turn about x, exactly as the round
                // tables are placed. Do the same before measuring.
                bool zUp = IsZUp(b);
                if (zUp)
                {
                    go.transform.rotation = Upright;
                    b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                }
                // Backrest direction: centroid of the vertices in the top quarter of the
                // piece, relative to the footprint centre (seats face the other way).
                var high = new List<Vector3>();
                int tris = 0;
                foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null) continue;
                    tris += f.sharedMesh.triangles.Length / 3;
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        Vector3 w = f.transform.TransformPoint(v);
                        if (w.y > b.min.y + b.size.y * .75f) high.Add(w);
                    }
                }
                foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s.sharedMesh != null) tris += s.sharedMesh.triangles.Length / 3;
                string back = "n/a";
                if (high.Count > 0)
                {
                    Vector3 c = high.Aggregate(Vector3.zero, (a, v) => a + v) / high.Count;
                    Vector2 d = new Vector2(c.x - b.center.x, c.z - b.center.z);
                    back = d.magnitude < .02f ? "centred" : F(d.x) + "," + F(d.y) + " (" + F(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg) + " deg)";
                }
                string mats = string.Join(",", renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct());
                string comps = string.Join(",", go.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer))
                    .Select(c => c.GetType().Name).Distinct());
                report.Append(path).Append(zUp ? " [z-up, stood up]" : "").Append('\t').Append(V(b.size)).Append('\t').Append(V(b.min)).Append('\t').Append(back).Append('\t')
                      .Append(tris).Append('\t').Append(mats).Append('\t').Append(comps).AppendLine();
                // Line it up for the photos: 1.6 m apart (bigger pieces get more room).
                float spacing = Mathf.Max(1.6f, Mathf.Max(b.size.x, b.size.z) + .6f);
                Vector3 slot = origin + new Vector3(column * 1.9f, 0f, -row * 2.2f);
                go.transform.position = slot - new Vector3(b.center.x, b.min.y, b.center.z);
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
                foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                built.Add((Path.GetFileNameWithoutExtension(path), go, b));
                if (++column == 8) { column = 0; row++; }
            }
            File.WriteAllText(Path.Combine(folder, "pieces.tsv"), report.ToString());
            // Photos: each row from the front (-z looking +z... the pieces face whatever
            // way they were modelled, so shoot from both sides), plus close-ups.
            int rows = row + 1;
            for (int r = 0; r < rows; r++)
            {
                Vector3 centre = origin + new Vector3(3.5f * 1.9f, .5f, -r * 2.2f);
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"row{r + 1}-from-minus-z.png"), centre + new Vector3(0f, 1.6f, -6.5f), centre, 62f, false);
                CafeSecondPassSteps.Capture(Path.Combine(folder, $"row{r + 1}-from-plus-z.png"), centre + new Vector3(0f, 1.6f, 6.5f), centre, 62f, false);
            }
            foreach (var p in built.Take(21))
            {
                Vector3 c = p.go.transform.position + new Vector3(0f, p.b.size.y * .5f, 0f);
                float dist = Mathf.Max(1.2f, p.b.size.magnitude * 1.6f);
                CafeSecondPassSteps.Capture(Path.Combine(folder, "piece-" + p.name + ".png"), c + new Vector3(dist * .55f, dist * .45f, -dist * .7f), c, 50f, false);
            }
        }
        finally
        {
            foreach (var p in built) if (p.go != null) Object.DestroyImmediate(p.go);
        }
        return built.Count + " pieces measured; " + folder;
    }

    internal static readonly Quaternion Upright = Quaternion.Euler(-90f, 0f, 0f);

    // A Blender export that was not axis-converted: its height runs along z from 0,
    // and it is centred on y (its depth).
    internal static bool IsZUp(Bounds b) =>
        Mathf.Abs(b.min.z) < .01f && b.min.y < -.01f && Mathf.Abs(b.min.y + b.size.y * .5f) < .03f;

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
}
#endif
