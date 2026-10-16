#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE REPAIR MAT AND THE CHECKLIST'S ICONS, INTO UNITY (14 Oct 2026; specs/blender-wave1.md §5 steps 2 and 5)
//
//   Fixit Fidget > Bench > Bench stage 1 - Import and check the mat (the model only)
//   Fixit Fidget > Bench > Bench icons 1 - Import the checklist's icons as sprites
//
// His note (14 Oct): "i would also prefer the mat to also look like a repair mat". The Blender lead's silicone repair mat
// (Tools/Blender/bench_stage.py, Art's A1): 600 x 360 x 5 mm, slate blue (#4F70A0) with a printed 1 cm grid, a ruler
// along the near edge and up the left side, six round screw cups with a steel disc in each, a pale new-part pad and a dark
// cover well. Built in the bench rig's own frame (+x his right, +z away), so it goes under the stage with no turn, and its
// ten empties (Cup_1..6, Pad_Small, Pad_Large, Well_Small, Well_Large) are where the auto-tray flies each piece: their
// names are a contract (contracts.md §8; Systems' BenchStage fields read them).
//
// BENCH STAGE 1 is Bench tools 1's import and check (BlenderKitSteps.Run: the FBX settings, every material mapped, one
// mesh, its triangles and size as built, its origin at the bottom centre, each empty where it was built within 1 mm and
// unturned), with the mat's own six materials made the way the café kit makes textured ones (CafeLivedInSteps.Define:
// URP Lit, metallic 0, a textured one white with its picture and tiling) in Assets/Art/Materials/Bench beside BM_Mat:
// BM_Silicone carries the grid (BM_Grid_color.png, repeating, tiled 20 times: a 50 mm tile on UVs in metres) and BM_Print
// the two rulers (BM_Rulers.png, clamped, kept at its full 4096 px: 7 px a millimetre). Photos: the mat from the close-up's
// 58 degrees with a 3.8 mm dark disc on each cup's place (the feel targets' screw head) and a mark on the pads and wells.
// Nothing in the scene changes: placing it under InspectRig/Stage (v2) is the bench builder's step.
//
// BENCH ICONS 1: the eight generic icons (UI/UX §5.3; contracts.md §8) as sprites, so Resources.Load<Sprite>(
// "UI/BenchIcons/<name>") finds them: Sprite (2D and UI), single, mipmaps on, bilinear, 256 at most, uncompressed. The
// same settings are applied by BenchIconImport (below) whenever a picture in that folder is imported, so an icon dropped
// in later (the device-specific part_* and cover_* of the Blender lead's next session) arrives as a sprite by itself.
// Reports and photos: Logs/Bench/bench-stage-<time>/ and Logs/Bench/bench-icons-<time>/.
// ---------------------------------------------------------------------------
internal static class BenchStageImportSteps
{
    const string Menu = "Fixit Fidget/Bench/";
    const string Models = "Assets/Art/Models/BenchStage";
    const string ManifestPath = Models + "/bench_stage.json";
    const string MaterialFolder = "Assets/Art/Materials/Bench";      // where BM_Mat lives (Playtest3Session4Steps.cs: MaterialFolder)
    public const string IconFolder = "Assets/Data/Resources/UI/BenchIcons";
    /// <summary>The screw hole's decal (contracts §8; the Blender lead's wave 2): Resources.Load&lt;Texture2D&gt;("Bench/BT_ScrewHole").</summary>
    public const string ScrewHolePath = "Assets/Data/Resources/Bench/BT_ScrewHole.png";
    // UI/UX's contract (contracts.md §5): the Resources names the checklist asks for.
    static readonly string[] IconNames = { "screwdriver", "pry", "tweezers", "brush", "cloth", "screw", "newpart", "cover" };

    [MenuItem(Menu + "Bench stage 1 - Import and check the mat (the model only)")]
    static void ImportMat()
    {
        BlenderKitSteps.Run("Bench stage 1 - Import and check the mat (the model only)", "[Bench stage] ",
            ManifestPath, Models, null, null, Path.Combine("Logs", "Bench"), "bench-stage", PhotographMat, MatMaterials);
    }

    [MenuItem(Menu + "Bench icons 1 - Import the checklist's icons as sprites")]
    static void ImportIcons() => Icons();

    [MenuItem(Menu + "Bench stage 1 - Import and check the mat (the model only)", true)]
    [MenuItem(Menu + "Bench icons 1 - Import the checklist's icons as sprites", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ------------------------------------------------------------------ the mat's materials

    // The manifest's six materials, made or updated in Assets/Art/Materials/Bench (Kit 1's way), their pictures set up
    // first. A picture the manifest names that isn't in the project stops the step (a grey mat would pass every other check).
    static Dictionary<string, Material> MatMaterials(StringBuilder report)
    {
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
        if (!(CafeLivedInSteps.Json.Parse(File.ReadAllText(file)) is Dictionary<string, object> root) ||
            !root.TryGetValue("materials", out object listed) || !(listed is Dictionary<string, object> defs))
            throw new InvalidOperationException($"{ManifestPath} has no materials section (the kit's format: version, kit, note, pieces[], per_device, materials).");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "Bench");

        // The pictures: Kit 1's settings (CafeLivedInSteps.ConfigureTexture: a *_color picture repeats, any other clamps),
        // the manifest's own "wrap" when it gives one, and never smaller than the picture itself (the rulers are 4096 wide).
        var pictures = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (KeyValuePair<string, object> entry in defs)
            {
                if (!(entry.Value is Dictionary<string, object> def)) continue;
                string texture = CafeLivedInSteps.Str(def, "texture");
                if (texture == null) continue;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(texture) == null)
                    throw new InvalidOperationException($"{entry.Key}'s picture {texture} isn't in the project: copy the Blender lead's Assets/Art/Textures/Bench/*.png in, reimport, run this again.");
                CafeLivedInSteps.ConfigureTexture(texture, false);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texture);
                string wrap = CafeLivedInSteps.Str(def, "wrap");
                if (wrap != null) importer.wrapMode = wrap.Equals("Repeat", StringComparison.OrdinalIgnoreCase) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                int full = FullSize(texture);
                if (full > importer.maxTextureSize) importer.maxTextureSize = full;
                importer.SaveAndReimport();
                pictures.Add($"{Path.GetFileName(texture)} ({importer.wrapMode}, up to {importer.maxTextureSize} px)");
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        Dictionary<string, Material> project = CafeLivedInSteps.ProjectMaterials();
        var made = new Dictionary<string, Material>();
        int fresh = 0;
        var lines = new List<string>();
        foreach (KeyValuePair<string, object> entry in defs)
        {
            if (!(entry.Value is Dictionary<string, object> def)) continue;
            string path = MaterialFolder + "/" + entry.Key + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = entry.Key };
                AssetDatabase.CreateAsset(material, path);
                fresh++;
            }
            CafeLivedInSteps.Define(material, def, lit, project);
            EditorUtility.SetDirty(material);
            made[entry.Key] = material;
            Texture picture = material.GetTexture("_BaseMap");
            Vector2 tiles = material.GetTextureScale("_BaseMap");
            lines.Add(picture != null ? $"{entry.Key}: white under {picture.name}, tiled {tiles.x.ToString("0.##", CultureInfo.InvariantCulture)}"
                                      : $"{entry.Key}: #{CafeLivedInSteps.Str(def, "hex")}");
        }
        AssetDatabase.SaveAssets();
        report.AppendLine($"The mat's materials in {MaterialFolder} ({fresh} new, {made.Count - fresh} updated): {string.Join("; ", lines)}.");
        report.AppendLine($"Its pictures: {string.Join(", ", pictures)}.");
        return made;
    }

    // A PNG's own width or height, whichever is larger, as a power of two up to 4096 (its header: IHDR at byte 16).
    static int FullSize(string path)
    {
        try
        {
            using (var stream = File.OpenRead(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))))
            {
                var head = new byte[24];
                if (stream.Read(head, 0, 24) < 24) return 0;
                int w = head[16] << 24 | head[17] << 16 | head[18] << 8 | head[19];
                int h = head[20] << 24 | head[21] << 16 | head[22] << 8 | head[23];
                return Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(w, h)), 32, 4096);
            }
        }
        catch (Exception) { return 0; }
    }

    // The mat as the close-up sees it: from 58 degrees, the whole of it, then the cups, the pads and the well closer; on
    // each cup's empty a 3.8 mm dark disc (the screw head of the feel targets), on each pad and well a small orange mark.
    static void PhotographMat(List<string> paths, string folder, StringBuilder report)
    {
        string mat = paths.FirstOrDefault(p => p.EndsWith("/BM_RepairMat.fbx", StringComparison.Ordinal));
        if (mat == null) return;
        using (var s = new BlenderKitSteps.Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Bench top (photo)", new Vector3(0f, -.02f, 0f), new Vector3(.9f, .04f, .6f), s.Flat(new Color(.56f, .36f, .20f)));
            GameObject placed = s.Place(mat, Vector3.zero, Vector3.zero);
            int marked = 0;
            if (placed != null)
                foreach (Transform t in placed.transform)
                {
                    bool cup = t.name.StartsWith("Cup_", StringComparison.Ordinal);
                    if (!cup && !t.name.StartsWith("Pad_", StringComparison.Ordinal) && !t.name.StartsWith("Well_", StringComparison.Ordinal)) continue;
                    // Unity's cylinder is 2 units tall at scale 1: 0.0003 makes a 0.6 mm disc
                    s.Primitive(cup ? PrimitiveType.Cylinder : PrimitiveType.Cube, t.name + " (photo)", t.position + Vector3.up * .0003f,
                                cup ? new Vector3(.0038f, .0003f, .0038f) : new Vector3(.01f, .0006f, .01f),
                                s.Flat(cup ? new Color(.14f, .13f, .12f) : new Color(.95f, .55f, .15f)));
                    marked++;
                }
            float pitch = 58f * Mathf.Deg2Rad;
            s.Shoot(Path.Combine(folder, "1-the-mat-from-58-degrees.png"), new Vector3(0f, Mathf.Sin(pitch) * .78f, -Mathf.Cos(pitch) * .78f), Vector3.zero, 40f);
            s.Shoot(Path.Combine(folder, "2-the-cups-pads-and-well-close.png"), new Vector3(.13f, Mathf.Sin(pitch) * .32f, -.02f - Mathf.Cos(pitch) * .32f), new Vector3(.13f, 0f, -.02f), 40f);
            report.AppendLine($"Photos (a preview scene; the café scene isn't touched): the mat from the close-up's 58 degrees, a mark on each of its {marked} empties " +
                              $"(10 expected: a 3.8 mm dark disc on each cup, an orange square on the pads and wells): 1-the-mat-from-58-degrees, 2-the-cups-pads-and-well-close, in {folder}");
        }
    }

    // ------------------------------------------------------------------ the icons

    static void Icons()
    {
        string title = "Bench icons 1 - Import the checklist's icons as sprites";
        var report = new StringBuilder(title + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        string folder = BlenderKitSteps.LogFolder(Path.Combine("Logs", "Bench"), "bench-icons");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This one is for Edit Mode: stop Play first.");
            if (!AssetDatabase.IsValidFolder(IconFolder))
                throw new InvalidOperationException($"{IconFolder} isn't in the project: copy the Blender lead's Assets/Data/Resources/UI/BenchIcons/*.png in, then run this again.");
            List<string> pictures = AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }).Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => Path.GetDirectoryName(p)?.Replace('\\', '/') == IconFolder).OrderBy(p => p, StringComparer.Ordinal).ToList();
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in pictures)
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer && BenchIconImport.Apply(importer))
                    {
                        importer.SaveAndReimport();
                        changed++;
                    }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            report.AppendLine(changed == 0 ? $"Already sprites with UI/UX's settings: all {pictures.Count} pictures in {IconFolder}; nothing changed."
                                           : $"Set to sprites with UI/UX's settings: {changed} of the {pictures.Count} pictures in {IconFolder}.");
            foreach (string name in IconNames)
            {
                Sprite sprite = Resources.Load<Sprite>("UI/BenchIcons/" + name);
                Check(sprite != null && sprite.rect.width >= 64f && Mathf.Approximately(sprite.rect.width, sprite.rect.height),
                      $"Resources.Load<Sprite>(\"UI/BenchIcons/{name}\") {(sprite == null ? "finds NOTHING" : $"finds a {sprite.rect.width:0} x {sprite.rect.height:0} sprite")}");
            }
            List<string> others = pictures.Select(p => Path.GetFileNameWithoutExtension(p)).Where(n => !IconNames.Contains(n)).ToList();
            if (others.Count > 0) report.AppendLine($"Also in the folder (device-specific icons, same settings): {string.Join(", ", others)}.");
            // The screw hole's decal (the Blender lead's wave 2, §5.4), if it's in: its own settings (BenchIconImport.ApplyDecal).
            if (AssetImporter.GetAtPath(ScrewHolePath) is TextureImporter decal)
            {
                bool set = BenchIconImport.ApplyDecal(decal);
                if (set) decal.SaveAndReimport();
                Check(Resources.Load<Texture2D>("Bench/BT_ScrewHole") != null, $"the screw hole's decal {(set ? "set to" : "already has")} wave 2's settings (Default, Clamp, mipmaps, at most 256, uncompressed), and Resources.Load<Texture2D>(\"Bench/BT_ScrewHole\") finds it");
            }
            else report.AppendLine($"The screw hole's decal ({ScrewHolePath}) isn't in yet: the bench draws its own until it is.");
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : failures + " check(s) failed.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = "[Bench icons] " + (failures == 0 ? "The icons are sprites. " : failures + " check(s) FAILED. ") + folder + "\n" + report;
            if (failures == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            Debug.LogError("[Bench icons] The import FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }
}

// Any picture imported into the checklist's icon folder comes in as a UI sprite with UI/UX's settings (§5.3), so
// Resources.Load<Sprite> finds it from its first import on the Mac, before or without Bench icons 1. Only that folder, and
// the screw hole's decal (Resources/Bench/BT_ScrewHole.png), which comes in with the Blender lead's wave 2 settings.
internal sealed class BenchIconImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        // The screw hole's decal too (the integration of the Blender lead's wave 2, 14 Oct): its own settings, on its first import.
        if (assetPath == BenchStageImportSteps.ScrewHolePath) { if (assetImporter is TextureImporter decal) ApplyDecal(decal); return; }
        if (!assetPath.StartsWith(BenchStageImportSteps.IconFolder + "/", StringComparison.Ordinal)) return;
        if (assetImporter is TextureImporter importer) Apply(importer);
    }

    // The Blender lead's wave 2 §5.4 for BT_ScrewHole.png (a quad 1.15 x the head, laid on the surface): Default, sRGB, alpha
    // as transparency, mipmaps on, bilinear, Clamp (its rim never wraps), at most 256, uncompressed. True when anything changed.
    internal static bool ApplyDecal(TextureImporter t)
    {
        bool changed = false;
        if (t.textureType != TextureImporterType.Default) { t.textureType = TextureImporterType.Default; changed = true; }
        if (!t.sRGBTexture) { t.sRGBTexture = true; changed = true; }
        if (!t.alphaIsTransparency) { t.alphaIsTransparency = true; changed = true; }
        if (!t.mipmapEnabled) { t.mipmapEnabled = true; changed = true; }
        if (t.filterMode != FilterMode.Bilinear) { t.filterMode = FilterMode.Bilinear; changed = true; }
        if (t.wrapMode != TextureWrapMode.Clamp) { t.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (t.maxTextureSize != 256) { t.maxTextureSize = 256; changed = true; }
        if (t.textureCompression != TextureImporterCompression.Uncompressed) { t.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        return changed;
    }

    // UI/UX §5.3: Sprite (2D and UI), single, mipmaps on (shown at 36-56 px from 256), bilinear, at most 256, uncompressed,
    // clamped, alpha as transparency. True when anything had to change.
    internal static bool Apply(TextureImporter t)
    {
        bool changed = false;
        if (t.textureType != TextureImporterType.Sprite) { t.textureType = TextureImporterType.Sprite; changed = true; }
        if (t.spriteImportMode != SpriteImportMode.Single) { t.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!t.mipmapEnabled) { t.mipmapEnabled = true; changed = true; }
        if (t.filterMode != FilterMode.Bilinear) { t.filterMode = FilterMode.Bilinear; changed = true; }
        if (t.maxTextureSize != 256) { t.maxTextureSize = 256; changed = true; }
        if (t.textureCompression != TextureImporterCompression.Uncompressed) { t.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (t.wrapMode != TextureWrapMode.Clamp) { t.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (!t.alphaIsTransparency) { t.alphaIsTransparency = true; changed = true; }
        if (!t.sRGBTexture) { t.sRGBTexture = true; changed = true; }
        return changed;
    }
}
#endif
