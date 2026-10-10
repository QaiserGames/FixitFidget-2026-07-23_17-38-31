#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE CAFÉ KIT: IMPORT AND CHECK (the café redesign, 10 Oct 2026; claude/studio/cafe-sf-research-and-redesign.md)
//
//   Fixit Fidget > Cafe redesign > Kit 1 - Import and check the café kit (the models only)
//   Fixit Fidget > Cafe redesign > Kit 2 - Check the menu board against the drinks (read-only)
//
// The café's new pieces were built in Blender 5.2 by four scripts in Tools/Blender (cafe_bar.py, cafe_seating.py,
// cafe_props.py, cafe_signs.py; sources in BlenderSource/), one FBX per piece, CK_ + a name, with a manifest per kit
// (cafe_bar.json, cafe_seating.json, cafe_props.json, cafe_signs.json), all in Assets/Art/Models/CafeKit. Their textures
// (the six tiling materials with normal maps, every sign, the menu, the labels) are in Assets/Art/Textures/Cafe, drawn by
// Tools/Blender/draw_cafe_textures.py, with menu-board.json beside them.
//
// Kit 1 changes no scene. It:
//   - sets every texture's import settings (tiling colour and normal maps repeat; signs and labels clamp, sharp);
//   - makes or updates the kit's own materials (CF_*.mat in Assets/Art/Materials/Cafe, URP Lit, metallic 0) from the
//     manifests: a textured one has a white base colour (the texture carries the colour) and its tiling (1 / its tile's
//     size in metres: the meshes carry box UVs in metres); glass and water transparent; the window gilding alpha-clipped;
//     the light strip and the picture light's glow emissive;
//   - sets every FBX's import settings (scale 1, no animation, no cameras or lights, readable, no colliders) and maps
//     every material in it to a project material of the same name, except: wood takes the café's grain (CC_Wood_Counter
//     -> "Cafe grain - CC_Wood_Counter", CC_Wood_Espresso -> "Cafe grain - CC_Wood_Espresso", from the café's palette),
//     the front counter's and the back bar's espresso faces take the V-groove boards (CF_Boards), and the banquette's
//     upholstery takes the woven fabric (CF_Fabric);
//   - checks each piece (one mesh, its triangles and size as built, standing on its origin, every material mapped) and
//     photographs each kit in a preview scene with the Unity materials (the café scene isn't touched).
// Kit 2 checks that every drink on the painted menu board is the game's drink at its price (menu-board.json against
// Assets/AssetsPrefabs/Drinks), and that no drink is missing from the board. If it fails: re-run
// draw_cafe_textures.py --only menu in Tools/Blender, then Kit 1.
//
// Report and photos: Logs/CafeRedesign/kit-1-<time>/; the last word: Logs/CafeRedesign/kit-1-last.txt.
// ---------------------------------------------------------------------------
internal static class CafeKitSteps
{
    const string Tag = "[Cafe kit] ";
    const string Models = CafeRedesignShared.KitModels;
    const string MaterialFolder = CafeRedesignShared.KitMaterials;
    const string Textures = CafeRedesignShared.KitTextures;
    const string ProjectMaterials = "Assets/Art/Materials";
    const string Palette = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";
    const string MenuRecord = Textures + "/menu-board.json";
    // The signs' manifest first: it defines the textured materials (stone, tile, boards, fabric, floors, labels) fully.
    static readonly string[] Manifests = { "cafe_signs.json", "cafe_bar.json", "cafe_props.json", "cafe_seating.json" };

    // Per piece, a material that maps to something other than its own name.
    static readonly Dictionary<string, Dictionary<string, string>> PieceRemaps = new Dictionary<string, Dictionary<string, string>>
    {
        { "CK_FrontCounter", new Dictionary<string, string> { { "CC_Wood_Espresso", "CF_Boards" } } },
        { "CK_BackBar", new Dictionary<string, string> { { "CC_Wood_Espresso", "CF_Boards" } } },
        { "CK_BanquetteBay", new Dictionary<string, string> { { "DC_Fabric", "CF_Fabric" } } },
    };
    // Everywhere else, wood takes the café's grain.
    static readonly Dictionary<string, string> GrainRemaps = new Dictionary<string, string>
    {
        { "CC_Wood_Counter", "Cafe grain - CC_Wood_Counter" },
        { "CC_Wood_Espresso", "Cafe grain - CC_Wood_Espresso" },
    };

    [MenuItem(CafeRedesignShared.Menu + "Kit 1 - Import and check the café kit (the models only)")]
    static void ImportTheKit()
    {
        var report = new StringBuilder("Kit 1 - Import and check the café kit (the models only)\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        string folder = NewFolder("kit-1");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            if (!AssetDatabase.IsValidFolder(Models)) throw new InvalidOperationException($"{Models} is missing: copy the kit's FBX files and manifests there first.");
            if (!AssetDatabase.IsValidFolder(Textures)) throw new InvalidOperationException($"{Textures} is missing: copy the kit's textures there first.");

            // ---- the manifests ----
            var pieces = new List<(Dictionary<string, object> piece, string kit)>();
            var definitions = new Dictionary<string, Dictionary<string, object>>();
            foreach (string name in Manifests)
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Models, name));
                if (!File.Exists(path)) { Check(false, $"the manifest {name} is in {Models}"); continue; }
                var manifest = (Dictionary<string, object>)Json.Parse(File.ReadAllText(path));
                foreach (object p in (List<object>)manifest["pieces"]) pieces.Add(((Dictionary<string, object>)p, name));
                if (manifest.TryGetValue("materials", out object mats))
                    foreach (var pair in (Dictionary<string, object>)mats)
                    {
                        var def = (Dictionary<string, object>)pair.Value;
                        if (!definitions.TryGetValue(pair.Key, out var had)) definitions[pair.Key] = new Dictionary<string, object>(def);
                        else foreach (var field in def) if (!had.ContainsKey(field.Key)) had[field.Key] = field.Value;     // the first kit wins; the others fill gaps
                    }
            }
            report.AppendLine($"Manifests: {Manifests.Length}, {pieces.Count} pieces, {definitions.Count} materials named.");

            // ---- the textures ----
            int textures = 0;
            var leftAlone = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Textures }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // Only the kit's own textures (CF_*): the folder also holds textures the café used before the kit (the
                    // old floor's WoodFloor051), and their import settings are theirs (10 Oct: the first run set the old
                    // floor's texture to Clamp, which would have smeared the floor's tiles; restored from git).
                    if (!Path.GetFileName(path).StartsWith("CF_", StringComparison.Ordinal)) { leftAlone.Add(Path.GetFileName(path)); continue; }
                    if (ConfigureTexture(path)) textures++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            report.AppendLine($"Textures: {textures} set up in {Textures}" +
                (leftAlone.Count > 0 ? $"; left alone (not the kit's): {string.Join(", ", leftAlone)}." : "."));

            // ---- the materials ----
            var materials = GatherMaterials(definitions, report, Check);

            // ---- the models ----
            var unmapped = new SortedSet<string>();
            var models = new List<(Dictionary<string, object> piece, string kit, string path)>();
            foreach (var (piece, kit) in pieces)
            {
                string name = (string)piece["name"];
                string path = Models + "/" + name + ".fbx";
                bool there = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                Check(there, $"{name}.fbx is in {Models} ({kit})");
                if (there) models.Add((piece, kit, path));
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var m in models)
                    Configure(m.path, (string)m.piece["name"], ((List<object>)m.piece["materials"]).Select(x => (string)x).ToList(), materials, unmapped);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Check(unmapped.Count == 0, "every material in the FBX files maps to a project material" + (unmapped.Count > 0 ? " (not found: " + string.Join(", ", unmapped) + ")" : ""));

            report.AppendLine();
            report.AppendLine("The pieces (size in metres: across, up, deep):");
            int total = 0;
            foreach (var (piece, kit, path) in models)
            {
                string name = (string)piece["name"];
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                if (filters.Length != 1) { Check(false, $"{name}: one mesh ({filters.Length} found)"); continue; }
                MeshFilter f0 = filters[0];
                Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * f0.transform.localToWorldMatrix;
                long indices = 0;
                for (int i = 0; i < f0.sharedMesh.subMeshCount; i++) indices += f0.sharedMesh.GetIndexCount(i);
                int tris = (int)(indices / 3);
                total += tris;
                Bounds b = Transformed(f0.sharedMesh.bounds, toRoot);
                int builtTris = Convert.ToInt32(piece["tris"], CultureInfo.InvariantCulture);
                var size = (List<object>)piece["size"];
                var built = new Vector3(Convert.ToSingle(size[0], CultureInfo.InvariantCulture), Convert.ToSingle(size[1], CultureInfo.InvariantCulture), Convert.ToSingle(size[2], CultureInfo.InvariantCulture));
                float bottom = piece.TryGetValue("bottom", out object bo) ? Convert.ToSingle(bo, CultureInfo.InvariantCulture) : 0f;
                bool whole = Mathf.Abs(tris - builtTris) <= Mathf.Max(2, builtTris / 200);
                bool sized = (b.size - built).magnitude < .012f;
                bool standing = Mathf.Abs(b.min.y - bottom) < .006f;
                bool upright = Quaternion.Angle(toRoot.rotation, Quaternion.identity) < .5f && (toRoot.lossyScale - Vector3.one).magnitude < .001f;
                Material[] used = f0.GetComponent<Renderer>().sharedMaterials;
                bool mapped = used.Length > 0 && used.All(m => m != null && AssetDatabase.Contains(m) && !AssetDatabase.GetAssetPath(m).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
                Check(whole && sized && standing && upright && mapped,
                      $"{name}: {tris} triangles{(tris == builtTris ? "" : " (built " + builtTris + ")")}, {V(b.size)}{(sized ? "" : " (built " + V(built) + ")")}, " +
                      $"{(upright ? "upright" : "TURNED or scaled")}, {(standing ? "on its origin" : "bottom at " + F(b.min.y) + " (built " + F(bottom) + ")")}, " +
                      $"materials: {string.Join(", ", used.Select(m => m == null ? "NONE" : m.name))}");
            }
            report.AppendLine($"{models.Count} pieces, {total} triangles in all.");

            // ---- the menu board against the drinks ----
            report.AppendLine();
            report.AppendLine("The menu board:");
            int menuFails = CheckMenu(report);
            failures += menuFails;

            // ---- the photos ----
            report.AppendLine();
            try { Photograph(folder, models.Select(m => m.path).ToList(), report); }
            catch (Exception e) { Check(false, "the photos: " + e.Message); }

            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            Keep(folder, (failures == 0 ? "Imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n\n" + report, failures > 0);
        }
        catch (Exception e)
        {
            Keep(folder, "The import FAILED: " + e.Message + "\n\n" + report + "\n" + e, true);
        }
    }

    [MenuItem(CafeRedesignShared.Menu + "Kit 2 - Check the menu board against the drinks (read-only)")]
    static void CheckTheMenu()
    {
        var report = new StringBuilder("Kit 2 - Check the menu board against the drinks (read-only)\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            int fails = CheckMenu(report);
            KeepMenu((fails == 0 ? "PASS: the board shows every drink at its price." : $"FAIL: {fails} problem(s). Re-run draw_cafe_textures.py --only menu (Tools/Blender), then Kit 1.") + "\n\n" + report, fails > 0);
        }
        catch (Exception e) { KeepMenu("The check FAILED: " + e.Message + "\n\n" + report + "\n" + e, true); }
    }

    [MenuItem(CafeRedesignShared.Menu + "Kit 1 - Import and check the café kit (the models only)", true)]
    [MenuItem(CafeRedesignShared.Menu + "Kit 2 - Check the menu board against the drinks (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Each entry of menu-board.json is the drink at that asset, by name and price; and every drink asset is on the board.
    static int CheckMenu(StringBuilder report)
    {
        int fails = 0;
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MenuRecord));
        if (!File.Exists(file)) { report.AppendLine($"FAIL  {MenuRecord} is missing (draw_cafe_textures.py writes it beside CF_Menu.png)."); return 1; }
        var record = (Dictionary<string, object>)Json.Parse(File.ReadAllText(file));
        string drinksFolder = (string)record["drinks_folder"];
        var onBoard = new HashSet<string>();
        foreach (object o in (List<object>)record["drinks"])
        {
            var entry = (Dictionary<string, object>)o;
            string asset = (string)entry["asset"], name = (string)entry["name"];
            int price = Convert.ToInt32(entry["price"], CultureInfo.InvariantCulture);
            onBoard.Add(asset);
            var drink = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(asset);
            bool ok = drink != null && string.Equals((drink.drinkName ?? "").Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) && drink.price == price;
            if (!ok) fails++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + $"line {entry["line"]}: \"{entry["printed"]}\" is {asset}" +
                              (drink == null ? " (MISSING)" : ok ? "" : $" (the game has \"{drink.drinkName}\" at {drink.price})"));
        }
        foreach (string guid in AssetDatabase.FindAssets("t:DrinkDefinition", new[] { drinksFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (onBoard.Contains(path)) continue;
            fails++;
            report.AppendLine($"FAIL  {path} is a drink the board doesn't show.");
        }
        report.AppendLine($"Board drawn {record["drawn"]}; {onBoard.Count} drinks on it.");
        return fails;
    }

    // ================================================================== textures

    static bool ConfigureTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        string file = Path.GetFileNameWithoutExtension(path);
        if (!file.StartsWith("CF_", StringComparison.Ordinal)) return false;      // never a texture that isn't the kit's
        bool normal = file.EndsWith("_normal", StringComparison.Ordinal);
        bool tiling = normal || file.EndsWith("_color", StringComparison.Ordinal);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.anisoLevel = tiling ? 4 : 8;
        importer.maxTextureSize = tiling ? 1024 : file == "CF_DrawerLabels" ? 4096 : 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = file.StartsWith("CF_Gilding", StringComparison.Ordinal);
        importer.SaveAndReimport();
        return true;
    }

    // ================================================================== materials

    static Dictionary<string, Material> GatherMaterials(Dictionary<string, Dictionary<string, object>> definitions, StringBuilder report, Action<bool, string> check)
    {
        var result = new Dictionary<string, Material>();
        // The project's own materials, by name (the folder itself, not its sub-folders).
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ProjectMaterials }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path)?.Replace('\\', '/') != ProjectMaterials) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && !result.ContainsKey(m.name)) result[m.name] = m;
        }
        // The café's grain (a scene palette: sub-assets of one file).
        int grain = 0;
        foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>())
            if (m.name.StartsWith("Cafe grain - ", StringComparison.Ordinal)) { result[m.name] = m; grain++; }
        check(GrainRemaps.Values.All(result.ContainsKey), $"the café's grain materials are in {Palette} ({grain} found)");
        Material glassModel = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().FirstOrDefault(m => m.name == "InteriorGlass");

        // The kit's own: every CF_ name (made or updated), and any other name the project lacks.
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialFolder)?.Replace('\\', '/'), Path.GetFileName(MaterialFolder));
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader wasn't found.");
        int made = 0, updated = 0;
        foreach (var pair in definitions)
        {
            string name = pair.Key;
            if (!name.StartsWith("CF_", StringComparison.Ordinal) && result.ContainsKey(name)) continue;     // an existing project material
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name };
                AssetDatabase.CreateAsset(material, path);
                made++;
            }
            else updated++;
            Define(material, name, pair.Value, lit, glassModel);
            EditorUtility.SetDirty(material);
            result[name] = material;
        }
        AssetDatabase.SaveAssets();
        report.AppendLine($"Materials: {made} made and {updated} updated in {MaterialFolder}; the project's by name; the café's grain from its palette.");
        return result;
    }

    static void Define(Material m, string name, Dictionary<string, object> def, Shader lit, Material glassModel)
    {
        if (m.shader != lit) m.shader = lit;
        m.enableInstancing = true;
        Color colour = Hex(Str(def, "hex") ?? "FFFFFF");
        string texture = Str(def, "texture"), normal = Str(def, "normal"), emission = Str(def, "emission");
        float tiling = Num(def, "tiling", 1f);
        bool transparent = Bool(def, "transparent");
        bool clipped = transparent && name.StartsWith("CF_Gilding", StringComparison.Ordinal);
        Texture2D baseMap = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texture) : null;
        Texture2D bump = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
        if (texture != null && baseMap == null) Debug.LogWarning(Tag + $"{name}: its texture {texture} isn't imported.");

        m.SetTexture("_BaseMap", baseMap);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        Color baseColour = baseMap != null ? Color.white : colour;
        m.SetFloat("_Smoothness", Num(def, "smoothness", .3f));
        m.SetFloat("_Metallic", 0f);
        if (bump != null)
        {
            m.SetTexture("_BumpMap", bump);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }

        // Surface: opaque, the gilding (gold leaf on the glass) or transparent (glass, water).
        m.SetFloat("_AlphaClip", clipped ? 1f : 0f);
        m.SetFloat("_Cutoff", clipped ? .05f : .4f);
        if (clipped) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
        m.SetFloat("_QueueOffset", 0f);
        if (clipped)
        {
            // The gilding draws AFTER the window glass (10 Oct, Claude). Drawn before it (the alpha-test queue, 2450), the
            // glass's reflection was laid over the letters and veiled the gold grey from the street, where real gold leaf
            // reads gold through its pane. URP's Lit shader works its queue out from the surface type and the sorting
            // priority (a queue set by hand is put back on the next validation: the probe found 2450 in memory with 3001 in
            // the file), so the gilding is a transparent surface one place after the glass (3000 + 1). Specular isn't kept
            // where the leaf is see-through, so the slab's empty part doesn't add a second reflection, and the near-empty
            // pixels are discarded (cutoff 0.05) so the letters' soft edges stay. Gold is a metal: half metallic, so the
            // leaf takes the street's light back in gold, as gilding does.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaToMask", 0f);
            m.SetFloat("_QueueOffset", 1f);
            m.SetFloat("_Metallic", Num(def, "metallic", .5f));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent + 1;
        }
        else if (transparent)
        {
            if (glassModel != null && name == "CF_Glass")
            {
                // The café's own glass: the same surface set-up as the pastry case and the windows.
                m.CopyPropertiesFromMaterial(glassModel);
                m.shaderKeywords = glassModel.shaderKeywords;
                m.renderQueue = glassModel.renderQueue;
                m.SetOverrideTag("RenderType", glassModel.GetTag("RenderType", false, "Transparent"));
                m.SetTexture("_BaseMap", null);
                m.SetFloat("_Smoothness", Num(def, "smoothness", .9f));
                m.SetFloat("_Metallic", 0f);
                Color g = glassModel.GetColor("_BaseColor");
                baseColour = new Color(colour.r, colour.g, colour.b, g.a);
            }
            else
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                baseColour.a = name == "CF_Water" ? .55f : .22f;
            }
        }
        else
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = -1;
        }
        m.SetColor("_BaseColor", baseColour);

        // Emission: the light strip, the LED ring, the picture light's glow.
        if (emission != null)
        {
            m.SetColor("_EmissionColor", Hex(emission) * 1.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    // ================================================================== models

    // The FBX's own material names are the manifest's (Blender's names): each is remapped by that name, so a second run
    // finds the same names whatever they were remapped to before.
    static void Configure(string path, string piece, List<string> sources, Dictionary<string, Material> materials, SortedSet<string> unmapped)
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
        importer.isReadable = true;             // a MeshCollider (the front counter) and the redesign's checks read the meshes
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var old in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            if (!sources.Contains(old.name)) importer.RemoveRemap(old);
        PieceRemaps.TryGetValue(piece, out var own);
        foreach (string source in sources)
        {
            string target = own != null && own.TryGetValue(source, out string o) ? o : GrainRemaps.TryGetValue(source, out string g) ? g : source;
            if (materials.TryGetValue(target, out Material mat))
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), mat);
            else unmapped.Add($"{piece}: {source} (wanted {target})");
        }
        importer.SaveAndReimport();
    }

    // ================================================================== photos (a preview scene: the café isn't touched)

    static void Photograph(string folder, List<string> paths, StringBuilder report)
    {
        string P(string piece) => paths.FirstOrDefault(p => p.EndsWith("/" + piece + ".fbx", StringComparison.Ordinal));
        int shots = 0;
        using (var s = new Studio())
        {
            Material floor = s.Flat(new Color(.80f, .72f, .60f));
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, 8f), new Vector3(30f, .02f, 30f), floor);
            // The bar at its places (Unity world, turned to face the room), as the bar lead placed them.
            var bar = new (string piece, Vector3 at)[]
            {
                ("CK_FrontCounter", new Vector3(-1.265f, 0f, 13.77f)), ("CK_RepairBench", new Vector3(-4.20f, 0f, 14.055f)),
                ("CK_BackBar", new Vector3(-1.25f, 0f, 18.00f)), ("CK_BackBarShelf", new Vector3(.55f, 1.75f, 18.00f)),
                ("CK_Backsplash", new Vector3(.55f, 1.00f, 18.00f)), ("CK_PartsWall", new Vector3(-5.60f, 1.00f, 18.00f)),
                ("CK_BenchLamp", new Vector3(-5.30f, .85f, 14.41f)), ("CK_MenuBoard", new Vector3(0f, 1.98f, 18.00f)),
                ("CK_Grinder", new Vector3(4.25f, 1.00f, 17.50f)),
            };
            foreach (var (piece, at) in bar) if (P(piece) != null) s.Place(P(piece), at, new Vector3(0f, 180f, 0f));
            for (int i = 0; i < 8; i++) if (P("CK_BeanBag") != null) s.Place(P("CK_BeanBag"), new Vector3(-3.20f + .135f * i, 1.81f, 17.85f), new Vector3(0f, 180f, 0f));
            s.Shoot(Path.Combine(folder, "01-the-bar-from-above.png"), new Vector3(0f, 12.5f, 4.5f), new Vector3(0f, .5f, 15.5f), 55f); shots++;
            s.Shoot(Path.Combine(folder, "02-the-bar-from-the-room.png"), new Vector3(1.0f, 1.65f, 9.0f), new Vector3(-1.0f, 1.1f, 15.0f), 62f); shots++;
            s.Shoot(Path.Combine(folder, "03-the-repair-bar.png"), new Vector3(-4.2f, 1.45f, 12.6f), new Vector3(-4.6f, 1.0f, 14.0f), 60f); shots++;
            s.Shoot(Path.Combine(folder, "04-the-parts-wall-labels.png"), new Vector3(-5.6f, 1.40f, 16.9f), new Vector3(-5.6f, 1.38f, 18.0f), 50f); shots++;
            s.Shoot(Path.Combine(folder, "05-the-menu-board.png"), new Vector3(0f, 1.80f, 13.0f), new Vector3(0f, 2.45f, 18.0f), 40f); shots++;
            s.Shoot(Path.Combine(folder, "06-the-counter-front-boards.png"), new Vector3(3.2f, 1.0f, 12.0f), new Vector3(4.2f, .6f, 13.4f), 50f); shots++;
        }
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, 0f), new Vector3(30f, .02f, 30f), s.Flat(new Color(.80f, .72f, .60f)));
            string[] row = { "CK_Chair_Cafe", "CK_Chair_Spindle", "CK_Stool_Bar", "CK_Stool_Low", "CK_Table_BistroRound", "CK_Table_Square2" };
            float x = -3.2f;
            foreach (string piece in row) { if (P(piece) != null) s.Place(P(piece), new Vector3(x, 0f, 0f), new Vector3(0f, 200f, 0f)); x += 1.1f; }
            if (P("CK_BanquetteBay") != null) s.Place(P("CK_BanquetteBay"), new Vector3(-1.0f, 0f, 2.2f), new Vector3(0f, 180f, 0f));
            if (P("CK_CommunalTable") != null) s.Place(P("CK_CommunalTable"), new Vector3(3.2f, 0f, 2.6f), new Vector3(0f, 90f, 0f));
            s.Shoot(Path.Combine(folder, "07-the-seats.png"), new Vector3(0f, 2.3f, -4.6f), new Vector3(0f, .5f, .8f), 55f); shots++;
            s.Shoot(Path.Combine(folder, "08-the-banquette-close.png"), new Vector3(-.4f, 1.1f, .7f), new Vector3(-1.0f, .5f, 2.0f), 55f); shots++;
        }
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, 0f), new Vector3(30f, .02f, 30f), s.Flat(new Color(.80f, .72f, .60f)));
            string[] row = { "CK_FiddleLeafFig", "CK_Monstera", "CK_OliveTree", "CK_RubberPlant", "CK_WaterStation", "CK_ServiceBell", "CK_BeanBag", "CK_SidewalkBench" };
            float x = -4.0f;
            foreach (string piece in row) { if (P(piece) != null) s.Place(P(piece), new Vector3(x, 0f, 0f), new Vector3(0f, 180f, 0f)); x += piece == "CK_SidewalkBench" ? 0f : 1.15f; }
            s.Shoot(Path.Combine(folder, "09-the-plants-and-props.png"), new Vector3(0f, 1.6f, -5.2f), new Vector3(0f, .8f, 0f), 60f); shots++;
            if (P("CK_TentCard") != null) s.Place(P("CK_TentCard"), new Vector3(.9f, 0f, -1.0f), new Vector3(0f, 180f, 0f));
            s.Shoot(Path.Combine(folder, "10-the-small-things-close.png"), new Vector3(1.6f, .55f, -1.9f), new Vector3(1.0f, .12f, -.6f), 40f); shots++;
        }
        using (var s = new Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Wall (photo)", new Vector3(0f, 2f, .05f), new Vector3(12f, 4f, .1f), s.Flat(new Color(.18f, .42f, .32f)));
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, -4f), new Vector3(20f, .02f, 10f), s.Flat(new Color(.55f, .53f, .50f)));
            if (P("CK_Fascia") != null) s.Place(P("CK_Fascia"), new Vector3(0f, 2.7f, 0f), new Vector3(0f, 180f, 0f));
            if (P("CK_WindowGilding_L") != null) s.Place(P("CK_WindowGilding_L"), new Vector3(-3.0f, 1.3f, -.02f), new Vector3(0f, 180f, 0f));
            if (P("CK_WindowGilding_R") != null) s.Place(P("CK_WindowGilding_R"), new Vector3(3.0f, 1.3f, -.02f), new Vector3(0f, 180f, 0f));
            if (P("CK_EnamelSign") != null) s.Place(P("CK_EnamelSign"), new Vector3(0f, 3.9f, -1.0f), new Vector3(0f, 180f, 0f));
            if (P("CK_EntryMosaic") != null) s.Place(P("CK_EntryMosaic"), new Vector3(0f, 0f, -2.5f), Vector3.zero);
            s.Shoot(Path.Combine(folder, "11-the-storefront-words.png"), new Vector3(0f, 1.7f, -9.0f), new Vector3(0f, 1.9f, 0f), 50f); shots++;
            s.Shoot(Path.Combine(folder, "12-the-mosaic-from-the-door.png"), new Vector3(0f, 1.65f, -5.0f), new Vector3(0f, 0f, -2.5f), 50f); shots++;
        }
        report.AppendLine($"Photos (a preview scene with the Unity materials; the café scene isn't touched): {shots}, in {folder}.");
    }

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
            Utility.lights[0].intensity = 1.2f;
            Utility.lights[0].color = new Color(1f, .95f, .88f);
            Utility.lights[0].transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            Utility.lights[1].intensity = .6f;
            Utility.lights[1].transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            Utility.ambientColor = new Color(.40f, .38f, .36f);
        }

        public Material Flat(Color c)
        {
            var m = new Material(lit) { color = c };
            made.Add(m);
            return m;
        }

        public GameObject Primitive(PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            go.transform.position = at;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Utility.AddSingleGO(go);
            return go;
        }

        public GameObject Place(string path, Vector3 at, Vector3 euler)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            GameObject go = Utility.InstantiatePrefabInScene(asset);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(euler));
            return go;
        }

        public void Shoot(string file, Vector3 from, Vector3 at, float fov)
        {
            Camera cam = Utility.camera;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            Utility.BeginStaticPreview(new Rect(0f, 0f, 1600f, 900f));
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

    // ================================================================== helpers

    static Bounds Transformed(Bounds b, Matrix4x4 m)
    {
        var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        return result;
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c) ? c : Color.magenta;
    static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out object v) && v is string s && s.Length > 0 ? s : null;
    static float Num(Dictionary<string, object> d, string key, float fallback) => d.TryGetValue(key, out object v) && v != null && !(v is string) && !(v is bool) ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : fallback;
    static bool Bool(Dictionary<string, object> d, string key) => d.TryGetValue(key, out object v) && (v is bool b ? b : v is double n && n != 0);

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);
    static string F(float f) => f.ToString("0.000", CultureInfo.InvariantCulture);

    static string NewFolder(string label)
    {
        string folder = Path.Combine(CafeRedesignShared.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static void Keep(string folder, string text, bool error)
    {
        try
        {
            Directory.CreateDirectory(CafeRedesignShared.LogRoot);
            File.WriteAllText(Path.Combine(CafeRedesignShared.LogRoot, "kit-1-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (folder != null && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Couldn't write the report: " + e.Message); }
        if (error) Debug.LogError(Tag + text); else Debug.Log(Tag + text);
    }

    static void KeepMenu(string text, bool error)
    {
        try
        {
            Directory.CreateDirectory(CafeRedesignShared.LogRoot);
            File.WriteAllText(Path.Combine(CafeRedesignShared.LogRoot, "kit-2-menu-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Couldn't write the report: " + e.Message); }
        if (error) Debug.LogError(Tag + text); else Debug.Log(Tag + text);
    }

    // A small JSON reader (objects, arrays, strings, numbers, true/false/null): the manifests are plain JSON.
    static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = Value(text, ref i);
            return v;
        }

        static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON ended early.");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true", StringComparison.Ordinal)) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false", StringComparison.Ordinal)) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null", StringComparison.Ordinal)) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"JSON: unexpected '{c}' at {i}.");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new FormatException($"JSON: ':' expected at {i}.");
                i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException($"JSON: ',' or '}}' expected at {i}.");
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException($"JSON: ',' or ']' expected at {i}.");
            }
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"JSON: a string expected at {i}.");
            i++;
            var b = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'u') { b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else b.Append(s[i]);
                i++;
            }
            i++;
            return b.ToString();
        }
    }
}
#endif
