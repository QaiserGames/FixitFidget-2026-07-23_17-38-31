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

// ---------------------------------------------------------------------------
// THE HOUSE DRESSING KIT, v1 (5 Oct 2026; claude/house-interiors-plan.md §9, route A)
//
//   Fixit Fidget > Night > House kit 1 - Import and check the kit (the models only)
//
// The kit's pieces are built in Blender 5.2 by Tools/Blender/house_kit.py (source: BlenderSource/HouseKit_v1.blend),
// one FBX each in Assets/Art/Models/HouseKit/, with a manifest beside them (house_kit.json: each piece's size and
// triangles as built, and the kit's materials). This step reads the manifest, sets every FBX's import settings
// (a still prop at its real size), makes the kit's materials (Assets/Art/Materials/HouseKit/HK_*.mat, URP Lit;
// the paintings get their textures, the lantern's glass its glow), maps every material in the FBX files to the
// project's material of the same name (the kit's, Grace's, or the café's), checks each model (one mesh, its size
// and triangles as built, upright, standing on its origin or hanging from it, nothing unmapped), and photographs
// the lot in a preview scene. The café scene is never opened or changed: the pieces go into Grace's house with
// Break-ins 6. Report and photos: Logs/Night/house-kit-<time>/.
// ---------------------------------------------------------------------------
internal static class HouseKitSteps
{
    const string Tag = "[House kit] ";
    public const string ModelFolder = "Assets/Art/Models/HouseKit";
    const string ManifestPath = ModelFolder + "/house_kit.json";
    const string CafeMaterials = "Assets/Art/Materials";
    const string KitMaterials = "Assets/Art/Materials/HouseKit";
    const string GraceMaterials = "Assets/Art/Materials/GraceHouse";

    [Serializable] class Piece { public string name, group, what; public bool stretch; public int tris; public float[] size; public float bottom, back; public string[] materials; public int islands; }
    [Serializable] class Manifest { public int version; public Piece[] pieces; }

    [MenuItem("Fixit Fidget/Night/House kit 1 - Import and check the kit (the models only)")]
    static void ImportAndCheck()
    {
        var report = new StringBuilder("House kit 1 - Import and check the kit (the models only)\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        string folder = LogFolder("house-kit");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            string manifestFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
            if (!File.Exists(manifestFile)) throw new InvalidOperationException($"No manifest at {ManifestPath} (Tools/Blender/export_house_kit.py writes it).");
            string json = File.ReadAllText(manifestFile);
            Manifest manifest = JsonUtility.FromJson<Manifest>(json);
            Dictionary<string, (string hex, float smoothness, string texture, string emission)> kitMaterials = ReadMaterials(json);
            Dictionary<string, Material> materials = MakeMaterials(kitMaterials, report);

            var models = new List<(Piece piece, string path)>();
            foreach (Piece piece in manifest.pieces)
            {
                string path = ModelFolder + "/" + piece.name + ".fbx";
                bool there = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                Check(there, $"{piece.name}.fbx is in {ModelFolder}");
                if (there) models.Add((piece, path));
            }
            var unmapped = new SortedSet<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var model in models) Configure(model.path, materials, unmapped);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Check(unmapped.Count == 0, "every material in the FBX files has a project material of the same name" +
                                       (unmapped.Count > 0 ? " (not found: " + string.Join(", ", unmapped) + ")" : ""));

            report.AppendLine();
            report.AppendLine("The pieces (size in metres: across, up, deep):");
            int totalTris = 0;
            foreach (var (piece, path) in models)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true);
                bool one = filters.Length == 1 && filters[0].sharedMesh != null;
                if (!one) { Check(false, $"{piece.name}: one mesh ({filters.Length} found)"); continue; }
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
                var built = new Vector3(piece.size[0], piece.size[1], piece.size[2]);
                bool size = (b.size - built).magnitude < .012f;
                bool standing = Mathf.Abs(b.min.y - piece.bottom) < .006f;
                Check(mapped && upright && size && standing && tris == piece.tris,
                      $"{piece.name}: {piece.what}; {tris} triangles{(tris == piece.tris ? "" : " (built with " + piece.tris + ")")}, " +
                      $"{V(b.size)}{(size ? "" : " (built " + V(built) + ")")}, {(upright ? "upright" : "TURNED " + V(toRoot.rotation.eulerAngles))}, " +
                      $"{(standing ? (piece.bottom < -.001f ? "hangs from its origin" : "on its origin") : "bottom at " + b.min.y.ToString("0.000", CultureInfo.InvariantCulture))}, " +
                      $"{used.Length} material(s){(mapped ? "" : " (NOT all project materials: " + string.Join(", ", used.Select(m => m != null ? m.name : "none")) + ")")}");
            }
            report.AppendLine($"{models.Count} pieces, {totalTris} triangles in all.");
            report.AppendLine();
            try { Photograph(models.Select(m => m.path).ToList(), folder, report); }
            catch (Exception e) { Check(false, "the photos: " + e.Message); }
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = Tag + (failures == 0 ? "The kit is imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n" + report;
            if (failures == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The kit's import FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // The manifest's materials, read by hand (JsonUtility can't read a dictionary): "name": {"hex": .., "smoothness": .., "texture"?: .., "emission"?: ..}
    static Dictionary<string, (string hex, float smoothness, string texture, string emission)> ReadMaterials(string json)
    {
        var result = new Dictionary<string, (string, float, string, string)>();
        foreach (string section in new[] { "\"materials\": {", "\"grace_materials\": {" })
        {
            // The dictionaries at the top level (each piece's "materials" is a list, not these).
            int at = json.IndexOf(section, StringComparison.Ordinal);
            if (at < 0) continue;
            int open = at + section.Length - 1;
            int depth = 0, end = open;
            for (int i = open; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}' && --depth == 0) { end = i; break; }
            }
            string body = json.Substring(open + 1, end - open - 1);
            // entries: "NAME": { ... }
            int pos = 0;
            while (true)
            {
                int q = body.IndexOf('"', pos);
                if (q < 0) break;
                int q2 = body.IndexOf('"', q + 1);
                string name = body.Substring(q + 1, q2 - q - 1);
                int o = body.IndexOf('{', q2);
                int c = body.IndexOf('}', o);
                string entry = body.Substring(o + 1, c - o - 1);
                string hex = Field(entry, "hex"), smooth = Field(entry, "smoothness"), texture = Field(entry, "texture"), emission = Field(entry, "emission");
                if (!string.IsNullOrEmpty(hex))
                    result[name] = (hex, float.Parse(string.IsNullOrEmpty(smooth) ? "0.3" : smooth, CultureInfo.InvariantCulture), texture, emission);
                pos = c + 1;
            }
        }
        return result;
    }

    static string Field(string entry, string key)
    {
        int at = entry.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (at < 0) return null;
        int colon = entry.IndexOf(':', at);
        int i = colon + 1;
        while (i < entry.Length && char.IsWhiteSpace(entry[i])) i++;
        if (i < entry.Length && entry[i] == '"') { int e = entry.IndexOf('"', i + 1); return entry.Substring(i + 1, e - i - 1); }
        int end = i;
        while (end < entry.Length && (char.IsDigit(entry[end]) || entry[end] == '.' || entry[end] == '-' || entry[end] == 'e' || entry[end] == 'E')) end++;
        return entry.Substring(i, end - i);
    }

    static Dictionary<string, Material> MakeMaterials(Dictionary<string, (string hex, float smoothness, string texture, string emission)> kit, StringBuilder report)
    {
        if (!AssetDatabase.IsValidFolder(KitMaterials)) AssetDatabase.CreateFolder(CafeMaterials, "HouseKit");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
        var result = new Dictionary<string, Material>();
        int made = 0, textured = 0;
        foreach (var pair in kit)
        {
            string name = pair.Key;
            bool kits = name.StartsWith("HK_", StringComparison.Ordinal);
            string path = (kits ? KitMaterials : GraceMaterials) + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!kits) continue;   // Grace's materials come from her own step; only hers that exist are mapped
                material = new Material(lit) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
                made++;
            }
            if (kits)
            {
                if (!ColorUtility.TryParseHtmlString("#" + pair.Value.hex, out Color colour)) throw new InvalidOperationException("Bad colour for " + name);
                if (material.shader != lit) material.shader = lit;
                material.SetFloat("_Smoothness", pair.Value.smoothness);
                material.SetFloat("_Metallic", 0f);
                if (!string.IsNullOrEmpty(pair.Value.texture))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value.texture);
                    if (tex != null)
                    {
                        material.SetTexture("_BaseMap", tex);
                        material.SetColor("_BaseColor", Color.white);
                        textured++;
                    }
                    else
                    {
                        material.SetColor("_BaseColor", colour);
                        report.AppendLine($"  (no texture at {pair.Value.texture} for {name}: flat colour for now)");
                    }
                }
                else material.SetColor("_BaseColor", colour);
                if (!string.IsNullOrEmpty(pair.Value.emission) && ColorUtility.TryParseHtmlString("#" + pair.Value.emission, out Color glow))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", glow * 1.6f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(material);
            }
            result[name] = material;
        }
        AssetDatabase.SaveAssets();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { CafeMaterials }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path)?.Replace('\\', '/') != CafeMaterials) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !result.ContainsKey(m.name)) result[m.name] = m;
        }
        report.AppendLine($"Materials: {result.Count(p => p.Key.StartsWith("HK_"))} of the kit's in {KitMaterials} ({made} new, {textured} with a painting), " +
                          $"{result.Count(p => p.Key.StartsWith("GH_"))} of Grace's, {result.Count(p => !p.Key.StartsWith("HK_") && !p.Key.StartsWith("GH_"))} of the café's.");
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

    // ------------------------------------------------------------------ the photos (a wall of hung pieces, a floor, a table)

    static readonly Dictionary<string, (Vector3 at, Vector3 euler, Vector3 scale)> Layout = new Dictionary<string, (Vector3, Vector3, Vector3)>
    {
        // hung on a wall at z = 0 (the pieces' backs), the room toward +z
        ["HK_Skirting"] = (new Vector3(-5.8f, 0f, 0f), Vector3.zero, new Vector3(1.6f, 1f, 1f)),
        ["HK_DadoRail"] = (new Vector3(-5.8f, .75f, 0f), Vector3.zero, new Vector3(1.6f, 1f, 1f)),
        ["HK_Cornice"] = (new Vector3(-5.8f, 2.42f, 0f), Vector3.zero, new Vector3(1.6f, 1f, 1f)),
        ["HK_Architrave"] = (new Vector3(-5.8f, 1.4f, 0f), Vector3.zero, new Vector3(1.6f, 1f, 1f)),
        ["HK_WindowSill"] = (new Vector3(-5.8f, 1.0f, 0f), Vector3.zero, new Vector3(1.6f, 1f, 1f)),
        ["HK_LiningBoard"] = (new Vector3(-4.3f, 0f, .2f), new Vector3(-90f, 0f, 0f), Vector3.one),
        ["HK_Radiator"] = (new Vector3(-4.3f, 0f, 0f), Vector3.zero, Vector3.one),
        ["HK_Picture_Hills"] = (new Vector3(-3.1f, 1.35f, 0f), Vector3.zero, Vector3.one),
        ["HK_Picture_Harbour"] = (new Vector3(-2.3f, 1.35f, 0f), Vector3.zero, Vector3.one),
        ["HK_Picture_Still"] = (new Vector3(-1.6f, 1.33f, 0f), Vector3.zero, Vector3.one),
        ["HK_Picture_Portrait"] = (new Vector3(-1.05f, 1.33f, 0f), Vector3.zero, Vector3.one),
        ["HK_Clock"] = (new Vector3(-.45f, 1.5f, 0f), Vector3.zero, Vector3.one),
        ["HK_HookRail"] = (new Vector3(.4f, 1.65f, 0f), Vector3.zero, Vector3.one),
        ["HK_Coat"] = (new Vector3(.4f, 1.714f, .06f), Vector3.zero, Vector3.one),
        ["HK_PorchLantern"] = (new Vector3(1.2f, 1.5f, 0f), Vector3.zero, Vector3.one),
        ["HK_CurtainRod"] = (new Vector3(2.5f, 2.3f, 0f), Vector3.zero, new Vector3(1.3f, 1f, 1f)),
        ["HK_CurtainRod_End"] = (new Vector3(2.5f + .65f, 2.3f, 0f), Vector3.zero, Vector3.one),
        ["HK_CurtainPanel"] = (new Vector3(2.5f - .47f, 2.3f, .09f), Vector3.zero, Vector3.one),
        ["HK_Blind"] = (new Vector3(4.3f, 2.3f, 0f), Vector3.zero, Vector3.one),
        ["HK_Door_Panelled"] = (new Vector3(5.2f, 0f, .01f), Vector3.zero, Vector3.one),
        ["HK_Pendant"] = (new Vector3(3.5f, 2.6f, 1.6f), Vector3.zero, Vector3.one),
        ["HK_StripLight"] = (new Vector3(4.3f, 1.5f, 1.2f), Vector3.zero, Vector3.one),
        // the floor
        ["HK_Threshold"] = (new Vector3(-5.8f, 0f, 1.0f), Vector3.zero, new Vector3(1.3f, 1f, 1f)),
        ["HK_Doormat"] = (new Vector3(-5.8f, 0f, 1.7f), Vector3.zero, Vector3.one),
        ["HK_Rug_Round"] = (new Vector3(-3.6f, 0f, 2.4f), Vector3.zero, Vector3.one),
        ["HK_Rug_Runner"] = (new Vector3(-1.0f, 0f, 2.6f), Vector3.zero, Vector3.one),
        ["HK_Plant"] = (new Vector3(-3.6f, 0f, .9f), Vector3.zero, Vector3.one),
        ["HK_Shoes"] = (new Vector3(-2.6f, 0f, 1.0f), new Vector3(0f, 20f, 0f), Vector3.one),
        ["HK_Slippers"] = (new Vector3(-2.0f, 0f, 1.0f), new Vector3(0f, -15f, 0f), Vector3.one),
        ["HK_Bin"] = (new Vector3(-4.9f, 0f, 1.0f), Vector3.zero, Vector3.one),
        ["HK_Cushion"] = (new Vector3(1.0f, 0f, 1.2f), new Vector3(0f, 30f, 0f), Vector3.one),
        // a table at 0.75
        ["HK_Books_Row"] = (new Vector3(1.5f, .75f, 2.0f), Vector3.zero, Vector3.one),
        ["HK_Book_Open"] = (new Vector3(2.1f, .75f, 2.0f), new Vector3(0f, -20f, 0f), Vector3.one),
        ["HK_Mug"] = (new Vector3(2.5f, .75f, 2.0f), Vector3.zero, Vector3.one),
        ["HK_Plates"] = (new Vector3(2.9f, .75f, 2.0f), Vector3.zero, Vector3.one),
        ["HK_Post_Pile"] = (new Vector3(3.4f, .75f, 2.0f), new Vector3(0f, 15f, 0f), Vector3.one),
        ["HK_Letter"] = (new Vector3(3.8f, .75f, 2.0f), new Vector3(0f, -10f, 0f), Vector3.one),
        ["HK_Newspaper"] = (new Vector3(4.3f, .75f, 2.0f), new Vector3(0f, 25f, 0f), Vector3.one),
        ["HK_Toaster"] = (new Vector3(4.8f, .75f, 2.0f), Vector3.zero, Vector3.one),
    };

    static void Photograph(List<string> paths, string folder, StringBuilder report)
    {
        var utility = new PreviewRenderUtility();
        var made = new List<Object>();
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

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material ground = new Material(lit) { color = new Color(.72f, .69f, .64f) };
            Material plaster = new Material(lit) { color = new Color(.86f, .80f, .70f) };
            Material wood = new Material(lit) { color = new Color(.56f, .36f, .20f) };
            made.AddRange(new Object[] { ground, plaster, wood });
            void Slab(string name, Vector3 centre, Vector3 size, Material m)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
                var mesh = new Mesh { name = name };
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
                // six faces of a box
                Vector3 h = size * .5f;
                void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
                {
                    int s = v.Count;
                    v.AddRange(new[] { centre + a, centre + b, centre + c, centre + d });
                    n.AddRange(new[] { normal, normal, normal, normal });
                    t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                }
                Face(new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Vector3.up);
                Face(new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), Vector3.left);
                Face(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z), Vector3.right);
                Face(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.forward);
                Face(new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), Vector3.back);
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
                made.Add(mesh);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
                utility.AddSingleGO(go);
            }
            Slab("Floor (photo)", new Vector3(0f, -.01f, 1.5f), new Vector3(14f, .02f, 7f), ground);
            Slab("Wall (photo)", new Vector3(0f, 1.4f, -.05f), new Vector3(14f, 2.8f, .1f), plaster);
            Slab("Table (photo)", new Vector3(3.15f, .73f, 2.0f), new Vector3(4.0f, .04f, .7f), wood);

            int placed = 0;
            foreach (string path in paths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !Layout.TryGetValue(asset.name, out var spot)) continue;
                GameObject go = utility.InstantiatePrefabInScene(asset);
                go.transform.SetPositionAndRotation(spot.at, Quaternion.Euler(spot.euler));
                go.transform.localScale = spot.scale;
                placed++;
                if (asset.name == "HK_CurtainPanel" || asset.name == "HK_CurtainRod_End")
                {
                    // the second panel, and the rod's other end, mirrored
                    GameObject other = utility.InstantiatePrefabInScene(asset);
                    float across = asset.name == "HK_CurtainPanel" ? .94f : -1.3f;
                    other.transform.SetPositionAndRotation(spot.at + new Vector3(across, 0f, 0f), Quaternion.identity);
                    other.transform.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
            foreach (var (name, position, target, fov) in new[]
            {
                ("1-all-of-it.png", new Vector3(0f, 4.6f, 9.5f), new Vector3(0f, 1.1f, .5f), 50f),
                ("2-the-finish.png", new Vector3(-4.6f, 1.9f, 3.6f), new Vector3(-4.6f, 1.1f, 0f), 46f),
                ("3-pictures-coat-lantern.png", new Vector3(-1.2f, 1.8f, 3.4f), new Vector3(-1.2f, 1.35f, 0f), 44f),
                ("4-window-and-door.png", new Vector3(4.0f, 1.9f, 4.4f), new Vector3(4.0f, 1.3f, 0f), 50f),
                ("5-the-table.png", new Vector3(3.2f, 1.9f, 3.6f), new Vector3(3.2f, .75f, 2.0f), 36f),
                ("6-the-floor.png", new Vector3(-3.0f, 2.8f, 4.6f), new Vector3(-3.0f, .2f, 1.6f), 46f),
            })
            {
                cam.fieldOfView = fov;
                cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                utility.BeginStaticPreview(new Rect(0f, 0f, 1600f, 900f));
                utility.Render(true);
                utility.Render(true);
                Texture2D picture = utility.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(folder, name), picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
            report.AppendLine($"Photos (a preview scene, {placed} pieces on a wall, a floor and a table; the café scene isn't touched): 1-all-of-it .. 6-the-floor, in {folder}");
        }
        finally
        {
            utility.Cleanup();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
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
#endif
