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
// THE BENCH'S TOOLS AND GRACE'S COVER (7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §4)
//
//   Fixit Fidget > Bench > Bench tools 1 - Import and check the tools (the models only)
//   Fixit Fidget > Night > Grace's cover 1 - Import and check the pieces (the models only)
//
// Both kits are built in Blender 5.2 by scripts in Tools/Blender (bench_tools.py, grace_cover.py; sources
// BlenderSource/BenchTools_v1.blend and GraceCover_v1.blend), one FBX per piece with a manifest beside them
// (bench_tools.json in Assets/Art/Models/BenchTools; grace_cover.json in Assets/Art/Models/GraceHouse). Each step
// reads its manifest, sets every FBX's import settings (a still prop at its real size), makes the kit's own
// materials (BT_*.mat in Assets/Art/Materials/BenchTools, URP Lit; Grace's GH_ ones already exist from her
// furniture step), maps every material in the FBX files to the project's material of the same name, checks each
// model (its meshes, its size and triangles as built, upright, its origin where it was built: a tool's working tip,
// a piece's bottom centre, the tweezers' leaves at the heel), and photographs the lot in a preview scene.
//
// Nothing in the café scene changes: the tools go onto the rail in session 4 (the bench's feel), the island and the
// boxes into Grace's house in session 5 (cover). Report and photos: Logs/Bench/bench-tools-<time>/ and
// Logs/Night/grace-cover-<time>/.
// ---------------------------------------------------------------------------
internal static class BlenderKitSteps
{
    [Serializable] class Child { public string name; public float[] at; }
    [Serializable] class Piece { public string name, tool, room, what; public int tris, meshes; public Child[] children; public float[] size; public float bottom; public string[] materials; public int islands; }
    [Serializable] class Manifest { public int version; public Piece[] pieces; }

    const string CafeMaterials = "Assets/Art/Materials";
    const string GraceMaterials = "Assets/Art/Materials/GraceHouse";
    const string ToolMaterials = "Assets/Art/Materials/BenchTools";
    public const string ToolModels = "Assets/Art/Models/BenchTools";
    public const string GraceModels = "Assets/Art/Models/GraceHouse";

    [MenuItem("Fixit Fidget/Bench/Bench tools 1 - Import and check the tools (the models only)")]
    static void ImportTools()
    {
        Run("Bench tools 1 - Import and check the tools (the models only)", "[Bench tools] ",
            ToolModels + "/bench_tools.json", ToolModels, "BT_", ToolMaterials, Path.Combine("Logs", "Bench"), "bench-tools",
            PhotographTools);
    }

    [MenuItem("Fixit Fidget/Night/Grace's cover 1 - Import and check the pieces (the models only)")]
    static void ImportCover()
    {
        Run("Grace's cover 1 - Import and check the pieces (the models only)", "[Grace's cover] ",
            GraceModels + "/grace_cover.json", GraceModels, null, null, Path.Combine("Logs", "Night"), "grace-cover",
            PhotographCover);
    }

    // ------------------------------------------------------------------ the shared step

    static void Run(string title, string tag, string manifestPath, string modelFolder, string ownPrefix, string ownMaterialFolder,
                    string logRoot, string logName, Action<List<string>, string, StringBuilder> photograph)
    {
        var report = new StringBuilder(title + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        string folder = LogFolder(logRoot, logName);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            string manifestFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", manifestPath));
            if (!File.Exists(manifestFile)) throw new InvalidOperationException($"No manifest at {manifestPath} (the Tools/Blender export script writes it).");
            string json = File.ReadAllText(manifestFile);
            Manifest manifest = JsonUtility.FromJson<Manifest>(json);
            Dictionary<string, Material> materials = GatherMaterials(json, ownPrefix, ownMaterialFolder, report);

            var models = new List<(Piece piece, string path)>();
            foreach (Piece piece in manifest.pieces)
            {
                string path = modelFolder + "/" + piece.name + ".fbx";
                bool there = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                Check(there, $"{piece.name}.fbx is in {modelFolder}");
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
                MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                int wanted = Mathf.Max(1, piece.meshes);
                if (filters.Length != wanted) { Check(false, $"{piece.name}: {wanted} mesh(es) ({filters.Length} found)"); continue; }
                int tris = 0;
                var whole = new Bounds();
                bool first = true, upright = true, mapped = true;
                var names = new List<string>();
                foreach (MeshFilter f in filters)
                {
                    Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                    long indices = 0;
                    for (int i = 0; i < f.sharedMesh.subMeshCount; i++) indices += f.sharedMesh.GetIndexCount(i);
                    tris += (int)(indices / 3);
                    Bounds b = Transformed(f.sharedMesh.bounds, toRoot);
                    if (first) { whole = b; first = false; } else whole.Encapsulate(b);
                    upright &= Quaternion.Angle(toRoot.rotation, Quaternion.identity) < .5f && (toRoot.lossyScale - Vector3.one).magnitude < .001f;
                    Material[] used = f.GetComponent<Renderer>().sharedMaterials;
                    mapped &= used.Length > 0 && used.All(m => m != null && AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.Ordinal));
                    foreach (Material m in used) if (m != null && !names.Contains(m.name)) names.Add(m.name);
                }
                totalTris += tris;
                var built = new Vector3(piece.size[0], piece.size[1], piece.size[2]);
                bool size = (whole.size - built).magnitude < .012f;
                bool standing = Mathf.Abs(whole.min.y - piece.bottom) < .006f;
                // a jointed piece: each named child where it was built (Unity's axes in the manifest)
                var joints = new StringBuilder();
                bool jointed = true;
                if (piece.children != null)
                    foreach (Child c in piece.children)
                    {
                        Transform t = asset.transform.Find(c.name);
                        var at = new Vector3(c.at[0], c.at[1], c.at[2]);
                        bool ok = t != null && (t.localPosition - at).magnitude < .001f && Quaternion.Angle(t.localRotation, Quaternion.identity) < .5f;
                        jointed &= ok;
                        joints.Append(ok ? $"; {c.name} at {V(at)}" : $"; {c.name} {(t == null ? "MISSING" : "at " + V(t.localPosition) + " (built at " + V(at) + ")")}");
                    }
                Check(mapped && upright && size && standing && jointed && tris == piece.tris,
                      $"{piece.name}: {piece.what}; {tris} triangles{(tris == piece.tris ? "" : " (built with " + piece.tris + ")")}, " +
                      $"{V(whole.size)}{(size ? "" : " (built " + V(built) + ")")}, {(upright ? "upright" : "TURNED or scaled")}, " +
                      $"{(standing ? "origin where it was built" : "bottom at " + whole.min.y.ToString("0.000", CultureInfo.InvariantCulture) + " (built " + piece.bottom.ToString("0.000", CultureInfo.InvariantCulture) + ")")}" +
                      $"{joints}, {names.Count} material(s){(mapped ? "" : " (NOT all project materials: " + string.Join(", ", names) + ")")}");
            }
            report.AppendLine($"{models.Count} pieces, {totalTris} triangles in all.");
            report.AppendLine();
            try { photograph(models.Select(m => m.path).ToList(), folder, report); }
            catch (Exception e) { Check(false, "the photos: " + e.Message); }
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = tag + (failures == 0 ? "Imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n" + report;
            if (failures == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            Debug.LogError(tag + "The import FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // The kit's own materials from the manifest ("materials": {"NAME": {"hex": .., "smoothness": ..}}), made or updated
    // (URP Lit, flat colour); then Grace's and the café's that already exist, by name.
    static Dictionary<string, Material> GatherMaterials(string json, string ownPrefix, string ownFolder, StringBuilder report)
    {
        var result = new Dictionary<string, Material>();
        int made = 0;
        if (ownPrefix != null)
        {
            if (!AssetDatabase.IsValidFolder(ownFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(ownFolder)?.Replace('\\', '/'), Path.GetFileName(ownFolder));
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
            foreach (var pair in ReadMaterials(json, "\"materials\": {"))
            {
                string name = pair.Key;
                if (!name.StartsWith(ownPrefix, StringComparison.Ordinal)) continue;
                string path = ownFolder + "/" + name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = name, enableInstancing = true };
                    AssetDatabase.CreateAsset(material, path);
                    made++;
                }
                if (!ColorUtility.TryParseHtmlString("#" + pair.Value.hex, out Color colour)) throw new InvalidOperationException("Bad colour for " + name);
                if (material.shader != lit) material.shader = lit;
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Smoothness", pair.Value.smoothness);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
                result[name] = material;
            }
            AssetDatabase.SaveAssets();
        }
        foreach (string root in new[] { GraceMaterials, CafeMaterials })
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != root) continue;     // not the sub-folders (the dispenser's BF_ family)
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && !result.ContainsKey(m.name)) result[m.name] = m;
            }
        report.AppendLine($"Materials: {(ownPrefix != null ? result.Count(p => p.Key.StartsWith(ownPrefix)) + " of the kit's in " + ownFolder + " (" + made + " new), " : "")}" +
                          $"{result.Count(p => p.Key.StartsWith("GH_"))} of Grace's, {result.Count(p => !p.Key.StartsWith("GH_") && (ownPrefix == null || !p.Key.StartsWith(ownPrefix)))} of the café's, by name.");
        return result;
    }

    static Dictionary<string, (string hex, float smoothness)> ReadMaterials(string json, string section)
    {
        var result = new Dictionary<string, (string, float)>();
        int at = json.IndexOf(section, StringComparison.Ordinal);
        if (at < 0) return result;
        int open = at + section.Length - 1;
        int depth = 0, end = open;
        for (int i = open; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}' && --depth == 0) { end = i; break; }
        }
        string body = json.Substring(open + 1, end - open - 1);
        int pos = 0;
        while (true)
        {
            int q = body.IndexOf('"', pos);
            if (q < 0) break;
            int q2 = body.IndexOf('"', q + 1);
            string name = body.Substring(q + 1, q2 - q - 1);
            int o = body.IndexOf('{', q2);
            int c = body.IndexOf('}', o);
            if (o < 0 || c < 0) break;
            string entry = body.Substring(o + 1, c - o - 1);
            string hex = Field(entry, "hex"), smooth = Field(entry, "smoothness");
            if (!string.IsNullOrEmpty(hex))
                result[name] = (hex, float.Parse(string.IsNullOrEmpty(smooth) ? "0.3" : smooth, CultureInfo.InvariantCulture));
            pos = c + 1;
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

    sealed class Studio : IDisposable
    {
        public readonly PreviewRenderUtility Utility = new PreviewRenderUtility();
        readonly List<Object> made = new List<Object>();
        readonly Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        public Studio()
        {
            Camera cam = Utility.camera;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.83f, .82f, .79f);
            Utility.lights[0].intensity = 1.25f;
            Utility.lights[0].color = new Color(1f, .95f, .88f);
            Utility.lights[0].transform.rotation = Quaternion.Euler(48f, 160f, 0f);
            Utility.lights[1].intensity = .55f;
            Utility.lights[1].transform.rotation = Quaternion.Euler(35f, -60f, 0f);
            Utility.ambientColor = new Color(.34f, .33f, .31f);
        }

        public Material Flat(Color c)
        {
            var m = new Material(lit) { color = c };
            made.Add(m);
            return m;
        }

        // A stand-in from Unity's built-in meshes (never GameObject.CreatePrimitive, which would land in the open scene).
        public GameObject Primitive(PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m, Vector3? euler = null)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(euler ?? Vector3.zero));
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Utility.AddSingleGO(go);
            return go;
        }

        public GameObject Place(string path, Vector3 at, Vector3 euler, float scale = 1f)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            GameObject go = Utility.InstantiatePrefabInScene(asset);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(euler));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public void Shoot(string file, Vector3 from, Vector3 at, float fov, int width = 1600, int height = 900)
        {
            Camera cam = Utility.camera;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            Utility.BeginStaticPreview(new Rect(0f, 0f, width, height));
            Utility.Render(true);
            Utility.Render(true);
            Texture2D picture = Utility.EndStaticPreview();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
        }

        public void Dispose()
        {
            Utility.Cleanup();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
        }
    }

    // The tools as the inspection camera will see them: points down on a bench top, leaning back 20°, 0.55 m away.
    static void PhotographTools(List<string> paths, string folder, StringBuilder report)
    {
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Bench top (photo)", new Vector3(0f, -.02f, 0f), new Vector3(.8f, .04f, .5f), s.Flat(new Color(.56f, .36f, .20f)));
            string[] order = { "BT_Screwdriver", "BT_Tweezers", "BT_Brush", "BT_PryTool", "BT_Cloth" };
            float x = -.11f;
            int placed = 0;
            foreach (string name in order)
            {
                string path = paths.FirstOrDefault(p => p.EndsWith("/" + name + ".fbx", StringComparison.Ordinal));
                if (path == null) continue;
                // Blender's -Y (the tool's front) is Unity's +Z: lean the handle back, away from the camera on +Z
                if (s.Place(path, new Vector3(x, 0f, 0f), new Vector3(-20f, 0f, 0f)) != null) placed++;
                x += .055f;
            }
            s.Shoot(Path.Combine(folder, "1-on-the-rail.png"), new Vector3(0f, .25f, .55f), new Vector3(0f, .06f, 0f), 40f);
            s.Shoot(Path.Combine(folder, "2-close.png"), new Vector3(.14f, .14f, .30f), new Vector3(0f, .07f, 0f), 34f);
            s.Shoot(Path.Combine(folder, "3-from-above.png"), new Vector3(0f, .45f, .12f), new Vector3(0f, .05f, 0f), 36f);
            report.AppendLine($"Photos (a preview scene, {placed} tools points-down on a bench top as the rail will hold them; the café scene isn't touched): 1-on-the-rail, 2-close, 3-from-above, in {folder}");
        }
    }

    // The island and the boxes, and the cover test: a crouched Ace (his 1.0 m capsule) 0.45 m behind each piece,
    // seen from Grace's eye (1.70 m) at 1.5 m and 2.5 m from the piece's face.
    static void PhotographCover(List<string> paths, string folder, StringBuilder report)
    {
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(.7f, -.01f, 0f), new Vector3(10f, .02f, 10f), s.Flat(new Color(.73f, .66f, .55f)));
            string island = paths.FirstOrDefault(p => p.EndsWith("/GH_KitchenIsland.fbx", StringComparison.Ordinal));
            string boxes = paths.FirstOrDefault(p => p.EndsWith("/GH_BoxStack.fbx", StringComparison.Ordinal));
            int placed = 0;
            const float islandDeep = .56f, boxesDeep = .472f;
            // the fronts face +Z (Blender's -Y); Grace stands on the +Z side, Ace crouches on the -Z side
            if (island != null && s.Place(island, Vector3.zero, Vector3.zero) != null) placed++;
            if (boxes != null && s.Place(boxes, new Vector3(1.4f, 0f, -.1f), Vector3.zero) != null) placed++;
            Material ace = s.Flat(new Color(.85f, .54f, .35f));
            // a crouched Ace: radius 0.35, 1.0 m tall (the capsule primitive is 2 m tall at radius 0.5)
            s.Primitive(PrimitiveType.Capsule, "Ace crouched, behind the island (photo)", new Vector3(0f, .5f, -(islandDeep / 2f + .45f)), new Vector3(.7f, .5f, .7f), ace);
            s.Primitive(PrimitiveType.Capsule, "Ace crouched, behind the boxes (photo)", new Vector3(1.4f, .5f, -.1f - (boxesDeep / 2f + .45f)), new Vector3(.7f, .5f, .7f), ace);
            s.Shoot(Path.Combine(folder, "1-the-pieces.png"), new Vector3(3.2f, 1.9f, 3.0f), new Vector3(.7f, .55f, 0f), 40f);
            foreach (float dist in new[] { 1.5f, 2.5f })
                s.Shoot(Path.Combine(folder, $"2-grace-at-{dist:0.0}m.png"), new Vector3(0f, 1.70f, islandDeep / 2f + dist), new Vector3(0f, 1.0f, 0f), 50f);
            s.Shoot(Path.Combine(folder, "3-grace-at-1.5m-boxes.png"), new Vector3(1.4f, 1.70f, -.1f + boxesDeep / 2f + 1.5f), new Vector3(1.4f, 1.0f, -.1f), 50f);
            s.Shoot(Path.Combine(folder, "4-from-the-side.png"), new Vector3(-3.2f, 1.4f, -.3f), new Vector3(.7f, .7f, -.3f), 40f);
            report.AppendLine($"Photos (a preview scene, {placed} pieces with a crouched Ace 0.45 m behind each, seen from Grace's eye at 1.70 m; the café scene isn't touched): 1-the-pieces .. 4-from-the-side, in {folder}");
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

    static string LogFolder(string root, string name)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", root,
            name + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
}
#endif
