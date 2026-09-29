using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// GRACE'S FURNITURE (playtest 2, step 4; the list in claude/break-ins-spec.md section 4)
//
//   Fixit Fidget > Night > Break-ins - Grace's furniture: import and check (the models only)
//
// The pieces for her ground floor, built in Blender 5.2 by Tools/Blender/grace_house.py (source:
// BlenderSource/GraceHouse_GroundFloor.blend), and for layout v2 (the break-ins' chunk A: the quarter-turn
// stairs, her bedroom, the bedroom's doors, the 2.0 m counter) by Tools/Blender/grace_house_v2.py (source:
// BlenderSource/GraceHouse_v2.blend), one FBX each in Assets/Art/Models/GraceHouse/. This step
// sets their import settings (a still prop at its real size, no animation, cameras or lights), makes
// Grace's own materials (Assets/Art/Materials/GraceHouse/GH_*.mat, URP Lit, colours from the table below)
// and maps every material in the FBX files to the project's material of the same name: the café's
// woods, steel, brass and ceramic (Assets/Art/Materials), or hers. Then it checks each model (one mesh,
// its size and triangles as built, standing at its origin, nothing left unmapped) and photographs the
// lot laid out in a preview scene. The café scene is never opened or changed: the pieces go into her
// house in step 5. Report and photos: Logs/Night/grace-furniture-<time>/.
internal static class GraceHouseFurnitureSteps
{
    const string Tag = "[Break-ins] ";
    const string ModelFolder = "Assets/Art/Models/GraceHouse";
    const string CafeMaterials = "Assets/Art/Materials";
    const string HerMaterials = "Assets/Art/Materials/GraceHouse";

    // Grace's materials: name, sRGB colour, smoothness (the same table as GH_MATERIALS in grace_house.py).
    static readonly (string name, string hex, float smoothness)[] Hers =
    {
        ("GH_Velvet_Teal", "2F5D5A", 0.25f),
        ("GH_Fabric_Rose", "B7857C", 0.12f),
        ("GH_Pillow_Sage", "8FA58B", 0.12f),
        ("GH_Pillow_Mustard", "C99A3E", 0.12f),
        ("GH_Lace", "EFE7D6", 0.1f),
        ("GH_Rug_Field", "7E3434", 0.05f),
        ("GH_Rug_Border", "D8C39C", 0.05f),
        ("GH_Plastic_Dark", "2B2926", 0.35f),
        ("GH_TV_Screen", "1D2A2F", 0.8f),
        ("GH_Lampshade", "E7D5B0", 0.1f),
        ("GH_Photo", "BFA88A", 0.25f),
        ("GH_Kitchen_Cream", "E6DCC3", 0.35f),
        ("GH_Fridge_Mint", "BCD6C4", 0.55f),
        ("GH_Enamel_Red", "B13A2F", 0.55f),
        ("GH_Delft_Blue", "2F5B8F", 0.6f),
        ("GH_Wool_Camel", "A57B52", 0.08f),
        ("GH_Scarf_Red", "8E3130", 0.08f),
        ("GH_Paint_White", "ECE6D8", 0.3f),
        ("GH_Cardboard", "B78C5A", 0.05f),
        ("GH_Tape", "CDB57E", 0.3f),
        ("GH_Glass_Dark", "2A3438", 0.85f),
        // layout v2
        ("GH_Quilt", "8A9DC0", 0.1f),
        ("GH_Mirror", "C9D6DE", 0.95f),
    };

    // The pieces as built in Blender: name, what it is, triangles, size in Unity's axes (x, y up, z).
    static readonly (string name, string what, int tris, Vector3 size)[] Pieces =
    {
        ("GH_Armchair", "her armchair (seat 0.45)", 488, new Vector3(0.806f, 1.02f, 0.902f)),
        ("GH_Sofa", "the sofa under the window", 576, new Vector3(1.75f, 0.872f, 0.948f)),
        ("GH_TV", "the television", 382, new Vector3(0.62f, 0.809f, 0.525f)),
        ("GH_TVCabinet", "the low cabinet it stands on", 328, new Vector3(1.05f, 0.5f, 0.489f)),
        ("GH_CoffeeTable", "coffee table", 292, new Vector3(0.95f, 0.423f, 0.55f)),
        ("GH_Sideboard", "sideboard (photos on it)", 672, new Vector3(1.39f, 0.865f, 0.508f)),
        ("GH_Frame_S", "photo frame, small, standing", 68, new Vector3(0.14f, 0.178f, 0.093f)),
        ("GH_Frame_M", "photo frame, medium, standing", 68, new Vector3(0.2f, 0.247f, 0.122f)),
        ("GH_Frame_L", "photo frame, large, for a wall", 68, new Vector3(0.36f, 0.44f, 0.025f)),
        ("GH_Frame_XL", "photo frame, very large, for a wall", 68, new Vector3(0.52f, 0.66f, 0.025f)),
        ("GH_StandardLamp", "standard lamp", 380, new Vector3(0.43f, 1.57f, 0.419f)),
        ("GH_Rug", "rug", 120, new Vector3(2.1f, 0.014f, 1.4f)),
        ("GH_KitchenCounter", "counter with sink, hob and oven (worktop 0.92)", 1134, new Vector3(2.61f, 1.169f, 0.661f)),
        ("GH_WallCupboards", "wall cupboards (hang the bottom at 1.45)", 372, new Vector3(2.04f, 0.72f, 0.374f)),
        ("GH_Fridge", "fridge", 388, new Vector3(0.62f, 1.62f, 0.69f)),
        ("GH_Kettle", "kettle (on the hob)", 408, new Vector3(0.294f, 0.238f, 0.21f)),
        ("GH_Teapot", "teapot", 440, new Vector3(0.286f, 0.159f, 0.173f)),
        ("GH_CoatStand", "coat stand with her coat", 600, new Vector3(0.531f, 1.89f, 0.438f)),
        ("GH_Stairs", "the stairs, with the cupboard under them", 1156, new Vector3(1.043f, 3.505f, 2.763f)),
        ("GH_UnderStairsDoor", "the cupboard door under the stairs", 168, new Vector3(0.05f, 1.144f, 0.644f)),
        ("GH_InteriorDoor_Frame", "doorway (the landing door)", 312, new Vector3(0.976f, 2.108f, 0.178f)),
        ("GH_InteriorDoor_Leaf", "door", 404, new Vector3(0.804f, 2.01f, 0.164f)),
        ("GH_CupBox", "the reunion cups: a box of three sleeves (the stash)", 528, new Vector3(0.644f, 0.175f, 0.587f)),
        ("GH_CupSleeve", "one sleeve of twelve cups", 164, new Vector3(0.382f, 0.094f, 0.096f)),
        // layout v2 (29 Sept): for a 1.0 m wide Ace, and her bedroom
        ("GH_Stairs_L", "the quarter-turn stairs, 1.40 m flights, the cupboard under them", 1180, new Vector3(2.66f, 3.405f, 2.68f)),
        ("GH_BedroomDoor_Frame", "the bedroom's doorway, 1.40 x 2.20", 312, new Vector3(1.556f, 2.278f, 0.138f)),
        ("GH_BedroomDoor_Leaf", "one of the bedroom's pair of doors (0.70 m)", 416, new Vector3(0.699f, 2.19f, 0.164f)),
        ("GH_Bed", "her bed, 1.35 x 1.90, mattress at 0.55", 848, new Vector3(1.464f, 1.108f, 2.014f)),
        ("GH_Quilt_Made", "the quilt, made (it sits on the bed: its bottom is 0.30 up)", 268, new Vector3(1.41f, 0.298f, 1.4f)),
        ("GH_Quilt_Asleep", "the quilt with her asleep under it (0.30 up, like the other)", 408, new Vector3(1.41f, 0.395f, 1.52f)),
        ("GH_BedsideTable", "bedside table", 260, new Vector3(0.43f, 0.55f, 0.388f)),
        ("GH_BedsideLamp", "bedside lamp", 330, new Vector3(0.26f, 0.42f, 0.26f)),
        ("GH_Wardrobe", "the wardrobe (a place to hide)", 276, new Vector3(0.99f, 1.98f, 0.635f)),
        ("GH_DressingTable", "the dressing table with its mirror", 816, new Vector3(1.1f, 1.46f, 0.546f)),
        ("GH_KitchenCounter_Short", "the counter, 2.0 m: cupboard, cooker, sink (worktop 0.92)", 1026, new Vector3(2.01f, 1.169f, 0.661f)),
    };

    // Pieces whose lowest point isn't on the floor, as built: the quilts lie on the bed.
    static readonly Dictionary<string, float> Bottoms = new Dictionary<string, float>
    {
        ["GH_Quilt_Made"] = .30f,
        ["GH_Quilt_Asleep"] = .30f,
    };

    [MenuItem("Fixit Fidget/Night/Break-ins - Grace's furniture: import and check (the models only)")]
    static void ImportAndCheck()
    {
        var report = new StringBuilder("Break-ins - Grace's furniture: import and check (the models only)\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        string folder = LogFolder("grace-furniture");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");

            // Her materials, made or brought up to date.
            Dictionary<string, Material> materials = MakeHerMaterials(report);

            // Every FBX: settings and materials.
            var models = new List<(string name, string what, int tris, Vector3 size, string path)>();
            foreach (var piece in Pieces)
            {
                string path = ModelFolder + "/" + piece.name + ".fbx";
                bool there = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                Check(there, $"{piece.name}.fbx is in {ModelFolder}");
                if (there) models.Add((piece.name, piece.what, piece.tris, piece.size, path));
            }
            var unmapped = new SortedSet<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var model in models)
                    Configure(model.path, materials, unmapped);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Check(unmapped.Count == 0, "every material in the FBX files has a project material of the same name" +
                                       (unmapped.Count > 0 ? " (not found: " + string.Join(", ", unmapped) + ")" : ""));

            // Each model, as imported.
            report.AppendLine();
            report.AppendLine("The pieces (size in metres: across, up, deep):");
            int totalTris = 0;
            foreach (var model in models)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(model.path);
                MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true);
                bool one = filters.Length == 1 && filters[0].sharedMesh != null;
                if (!one)
                {
                    Check(false, $"{model.name}: one mesh ({filters.Length} found)");
                    continue;
                }
                Mesh mesh = filters[0].sharedMesh;
                Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
                long indices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                int tris = (int)(indices / 3);
                totalTris += tris;
                Bounds b = Transformed(mesh.bounds, toRoot);
                Material[] used = filters[0].GetComponent<Renderer>().sharedMaterials;
                bool mapped = used.Length > 0 && used.All(m => m != null && AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.Ordinal));
                bool upright = Quaternion.Angle(toRoot.rotation, Quaternion.identity) < .5f && (toRoot.lossyScale - Vector3.one).magnitude < .001f;
                bool size = (b.size - model.size).magnitude < .012f;
                float bottom = Bottoms.TryGetValue(model.name, out float raised) ? raised : 0f;
                bool standing = Mathf.Abs(b.min.y - bottom) < .005f;
                Check(mapped && upright && size && standing && tris == model.tris,
                      $"{model.name}: {model.what}; {tris} triangles{(tris == model.tris ? "" : " (built with " + model.tris + ")")}, " +
                      $"{V(b.size)}{(size ? "" : " (built " + V(model.size) + ")")}, " +
                      $"{(upright ? "upright" : "TURNED " + V(toRoot.rotation.eulerAngles))}, {(standing ? (bottom > 0f ? "its bottom " + bottom.ToString("0.00", CultureInfo.InvariantCulture) + " up, as built" : "on its origin") : "bottom at " + b.min.y.ToString("0.000", CultureInfo.InvariantCulture))}, " +
                      $"{used.Length} materials{(mapped ? "" : " (NOT all project materials: " + string.Join(", ", used.Select(m => m != null ? m.name : "none")) + ")")}");
            }
            report.AppendLine($"{models.Count} pieces, {totalTris} triangles in all.");

            // A photograph of the lot, laid out in a preview scene (the café scene isn't touched).
            report.AppendLine();
            try
            {
                Photograph(models.Select(m => m.path).ToList(), folder, report);
            }
            catch (Exception e)
            {
                Check(false, "the photos: " + e.Message);
            }
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = Tag + "Grace's furniture: " + (failures == 0 ? "imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n" + report;
            if (failures == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Grace's furniture FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    static Dictionary<string, Material> MakeHerMaterials(StringBuilder report)
    {
        if (!AssetDatabase.IsValidFolder(HerMaterials)) AssetDatabase.CreateFolder(CafeMaterials, "GraceHouse");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
        var result = new Dictionary<string, Material>();
        int made = 0;
        foreach (var (name, hex, smoothness) in Hers)
        {
            string path = HerMaterials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
                made++;
            }
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color colour)) throw new InvalidOperationException("Bad colour for " + name);
            if (material.shader != lit) material.shader = lit;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            result[name] = material;
        }
        AssetDatabase.SaveAssets();
        // The café's own, by name, from Assets/Art/Materials (not its sub-folders).
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { CafeMaterials }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path)?.Replace('\\', '/') != CafeMaterials) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !result.ContainsKey(m.name)) result[m.name] = m;
        }
        report.AppendLine($"Her materials: {Hers.Length} in {HerMaterials} ({made} new); the café's: {result.Count - Hers.Length} in {CafeMaterials}.");
        return result;
    }

    static void Configure(string path, Dictionary<string, Material> materials, SortedSet<string> unmapped)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (Renderer r in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (materials.TryGetValue(m.name, out Material target))
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
                else unmapped.Add(m.name);
            }
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ the photos

    // Where each piece goes in the photo (Unity's axes; every piece faces +z, toward the camera).
    static readonly Dictionary<string, (Vector3 at, float turn)> Layout = new Dictionary<string, (Vector3, float)>
    {
        // the front room
        ["GH_Armchair"] = (new Vector3(-5.6f, 0f, 0f), 0f),
        ["GH_Sofa"] = (new Vector3(-3.9f, 0f, 0f), 0f),
        ["GH_TVCabinet"] = (new Vector3(-2.1f, 0f, 0f), 0f),
        ["GH_TV"] = (new Vector3(-2.1f, .50f, 0f), 0f),
        ["GH_Rug"] = (new Vector3(-.3f, 0f, .2f), 0f),
        ["GH_CoffeeTable"] = (new Vector3(-.3f, .014f, .2f), 0f),
        ["GH_Sideboard"] = (new Vector3(1.6f, 0f, 0f), 0f),
        ["GH_Frame_S"] = (new Vector3(1.35f, .865f, 0f), -8f),
        ["GH_Frame_M"] = (new Vector3(1.75f, .865f, 0f), 6f),
        ["GH_StandardLamp"] = (new Vector3(2.8f, 0f, 0f), 0f),
        ["GH_Frame_L"] = (new Vector3(3.6f, 0f, 0f), 0f),
        ["GH_Frame_XL"] = (new Vector3(4.3f, 0f, 0f), 0f),
        // the kitchen
        ["GH_KitchenCounter"] = (new Vector3(-3.6f, 0f, -3.2f), 0f),
        ["GH_WallCupboards"] = (new Vector3(-3.6f, 1.45f, -3.2f - .31f), 0f),
        ["GH_Kettle"] = (new Vector3(-3.2f, .925f, -3.25f), -20f),
        ["GH_Teapot"] = (new Vector3(-3.75f, .92f, -3.3f), 30f),
        ["GH_Fridge"] = (new Vector3(-1.8f, 0f, -3.2f), 0f),
        ["GH_CupBox"] = (new Vector3(-.5f, 0f, -3.0f), -15f),
        ["GH_CupSleeve"] = (new Vector3(.5f, 0f, -2.9f), -60f),
        // the hall
        ["GH_Stairs"] = (new Vector3(2.4f, 0f, -3.4f), 0f),
        ["GH_UnderStairsDoor"] = (new Vector3(2.4f - .485f, 0f, -3.4f - 1.553f), 0f),
        ["GH_CoatStand"] = (new Vector3(3.8f, 0f, -3.2f), 0f),
        ["GH_InteriorDoor_Frame"] = (new Vector3(5.2f, 0f, -3.2f), 0f),
        ["GH_InteriorDoor_Leaf"] = (new Vector3(5.2f + .40f, 0f, -3.2f), 30f),
        // layout v2: the stairs, her bedroom, its doors, the 2.0 m counter
        ["GH_Stairs_L"] = (new Vector3(-3.0f, 0f, -10.3f), 0f),
        ["GH_BedroomDoor_Frame"] = (new Vector3(-1.4f, 0f, -7.3f), 0f),
        ["GH_BedroomDoor_Leaf"] = (new Vector3(-.7f, 0f, -7.3f), 35f),
        ["GH_Bed"] = (new Vector3(.4f, 0f, -8.9f), 0f),
        ["GH_Quilt_Made"] = (new Vector3(.4f, 0f, -8.9f), 0f),
        ["GH_Quilt_Asleep"] = (new Vector3(2.2f, -.30f, -8.9f), 0f),
        ["GH_BedsideTable"] = (new Vector3(3.6f, 0f, -9.6f), 0f),
        ["GH_BedsideLamp"] = (new Vector3(3.6f, .55f, -9.6f), 0f),
        ["GH_Wardrobe"] = (new Vector3(4.7f, 0f, -9.7f), 0f),
        ["GH_DressingTable"] = (new Vector3(6.1f, 0f, -9.6f), 0f),
        ["GH_KitchenCounter_Short"] = (new Vector3(5.5f, 0f, -6.3f), 0f),
    };

    static void Photograph(List<string> paths, string folder, StringBuilder report)
    {
        var utility = new PreviewRenderUtility();
        Material ground = null;
        Mesh floorMesh = null;
        try
        {
            Camera cam = utility.camera;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.83f, .82f, .79f);
            utility.lights[0].intensity = 1.25f;
            utility.lights[0].color = new Color(1f, .95f, .88f);
            utility.lights[0].transform.rotation = Quaternion.Euler(48f, 160f, 0f);
            utility.lights[1].intensity = .55f;
            utility.lights[1].transform.rotation = Quaternion.Euler(35f, -60f, 0f);
            utility.ambientColor = new Color(.34f, .33f, .31f);

            // A floor to stand on: made hidden and unsaved, so the open scene is never marked changed.
            var floor = EditorUtility.CreateGameObjectWithHideFlags("Floor (photo)", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            floorMesh = new Mesh { name = "Floor (photo)" };
            floorMesh.SetVertices(new[] { new Vector3(-7f, -.002f, -11.2f), new Vector3(7.2f, -.002f, -11.2f), new Vector3(7.2f, -.002f, 2.5f), new Vector3(-7f, -.002f, 2.5f) });
            floorMesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            floorMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            floor.GetComponent<MeshFilter>().sharedMesh = floorMesh;
            ground = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.72f, .69f, .64f) };
            floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
            utility.AddSingleGO(floor);

            int placed = 0;
            foreach (string path in paths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !Layout.TryGetValue(asset.name, out var spot)) continue;
                GameObject go = utility.InstantiatePrefabInScene(asset);
                go.transform.SetPositionAndRotation(spot.at, Quaternion.Euler(0f, spot.turn, 0f));
                placed++;
            }

            foreach (var (name, position, target, fov) in new[]
            {
                ("1-all-of-it.png", new Vector3(-.6f, 8.2f, 6.4f), new Vector3(-.6f, .4f, -1.9f), 46f),
                ("2-front-room.png", new Vector3(-1.2f, 3.4f, 5.2f), new Vector3(-1.2f, .5f, 0f), 44f),
                ("3-kitchen-and-hall.png", new Vector3(.8f, 4.2f, 3.4f), new Vector3(.8f, .9f, -3.4f), 50f),
                ("4-bedroom-and-stairs.png", new Vector3(.3f, 6.2f, -2.2f), new Vector3(.3f, .7f, -8.7f), 58f),
            })
            {
                cam.fieldOfView = fov;
                cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                // Rendered twice: the first frame of a fresh preview comes out empty under URP.
                utility.BeginStaticPreview(new Rect(0f, 0f, 1600f, 900f));
                utility.Render(true);
                utility.Render(true);
                Texture2D picture = utility.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(folder, name), picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
            report.AppendLine($"Photos (a preview scene, {placed} pieces laid out by room; the café scene isn't touched): 1-all-of-it, 2-front-room, 3-kitchen-and-hall, 4-bedroom-and-stairs, in {folder}");
        }
        finally
        {
            utility.Cleanup();
            if (ground != null) Object.DestroyImmediate(ground);
            if (floorMesh != null) Object.DestroyImmediate(floorMesh);
        }
    }

    // ------------------------------------------------------------------ helpers

    static Bounds Transformed(Bounds b, Matrix4x4 m)
    {
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    static string LogFolder(string name)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            name + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
}
