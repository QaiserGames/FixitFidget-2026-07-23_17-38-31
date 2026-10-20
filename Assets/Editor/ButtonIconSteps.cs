#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// ---------------------------------------------------------------------------
// BUTTON ICONS (19 Oct 2026, playtest 7)
//
// His girlfriend is dyslexic and plays on a PlayStation pad: "Instead of writing out square, just put the icon." "I like how
// you gotta see what button you press, but I can't read fast enough. So icons would look better." From today every button
// name the game shows is a TextMesh Pro sprite (PadInput.Label, ControlHints: `<sprite name="ps_cross">`), and this step
// makes the asset those tags draw from:
//
//   Fixit Fidget › UI › Button icons - Make the icon sprite asset and TextMesh Pro's default (with Undo)
//     1. sets the sheet's importer (Tools/UI/draw_button_icons.py wrote it: no compression, no mipmaps, alpha is
//        transparency, not readable);
//     2. builds a TMP_SpriteAsset at Assets/Data/Resources/UI/ButtonIcons/Button icons.asset: the sheet as its sprite
//        sheet, a material on the TextMeshPro/Sprite shader as a sub-asset, one glyph and one named character per entry
//        of button_icons.json (the JSON's rects are top-left origin; TMP's glyph rects are bottom-left, so y is flipped);
//     3. sets its metrics so an icon sits on the text line at about 1.6 x the cap height of the UI font (the icon's
//        middle on the middle of the cap band), whatever size the text is;
//     4. makes it TextMesh Pro's default sprite asset (TMP Settings' m_defaultSpriteAsset, through a SerializedObject),
//        remembering the old default for the undo.
//   Fixit Fidget › UI › Button icons - Undo
//     puts the old default back and removes the asset. The game then shows the words again (PadInput.Word: the fallback
//     when the default sprite asset is missing or lacks a name), never a raw tag.
//
// Why a sprite asset and not images beside the text: a tag inside the string goes everywhere a word went (the prompt line,
// the strip, the replies' plates, the tickets, the phone's hint), in one change, and scales with the text.
// ---------------------------------------------------------------------------
public static class ButtonIconSteps
{
    public const string Folder = "Assets/Data/Resources/UI/ButtonIcons";
    public const string PngPath = Folder + "/button_icons.png";
    public const string JsonPath = Folder + "/button_icons.json";
    /// <summary>The sprite asset the step makes (and the undo removes).</summary>
    public const string AssetPath = Folder + "/Button icons.asset";
    /// <summary>An icon's height on the line, as a share of the UI font's cap height.</summary>
    // 20 Oct, the first lab photos on his Mac: at 1.25 the discs drew about as tall as the capitals, so the letter inside one
    // (A, B, R) was ~8 px: an icon a dyslexic reader has to squint at. 1.6 makes a disc ~27 px beside 24 px text, its letter
    // ~12 px, still inside the line (a 24 px line is ~28 px tall).
    public const float IconToCapHeight = 1.6f;
    /// <summary>The tallest icon on the sheet (the discs and keycaps), sheet pixels: it is the one drawn at IconToCapHeight.</summary>
    public const float TallestIcon = 128f;

    const string MakeMenu = "Fixit Fidget/UI/Button icons - Make the icon sprite asset and TextMesh Pro's default (with Undo)";
    const string UndoMenu = "Fixit Fidget/UI/Button icons - Undo (put TMP's old default back, remove the asset)";
    // The old default sprite asset (its GUID, "" for none), kept for the undo (EditorPrefs: per machine, like the step).
    const string PreviousDefaultKey = "FixitFidget.ButtonIcons.PreviousDefault";
    const string PreviousDefaultKnownKey = "FixitFidget.ButtonIcons.PreviousDefaultKnown";

    // The JSON the Python script writes: the sheet's size and one entry per icon, top-left origin.
    [Serializable] class Entry { public string name; public int x, y, w, h; }
    [Serializable] class Sheet { public string png; public int width, height; public Entry[] icons; }

    [MenuItem(MakeMenu)]
    static void Make()
    {
        try
        {
            if (Application.isPlaying) { Debug.LogError("[Button icons] Not during Play mode."); return; }
            var log = new StringBuilder();
            if (!File.Exists(JsonPath) || !File.Exists(PngPath))
            {
                Debug.LogError($"[Button icons] {PngPath} and {JsonPath} are made by Tools/UI/draw_button_icons.py (python3, Pillow). Run it, then this step.");
                return;
            }
            Sheet sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(JsonPath));
            if (sheet == null || sheet.icons == null || sheet.icons.Length == 0 || sheet.width <= 0 || sheet.height <= 0)
            {
                Debug.LogError("[Button icons] The JSON has no icons (or no size).");
                return;
            }

            // 1. The sheet's importer.
            ImportSheet(log);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            if (texture == null) { Debug.LogError("[Button icons] The sheet did not import as a texture: " + PngPath); return; }
            if (texture.width != sheet.width || texture.height != sheet.height)
                Debug.LogWarning($"[Button icons] The texture is {texture.width} x {texture.height} but the JSON says {sheet.width} x {sheet.height}: the rects are flipped by the JSON's height.");

            // 2. The sprite asset (made once; rebuilt in place after a new sheet, so references to it hold).
            bool made = false;
            var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (spriteAsset == null)
            {
                spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(spriteAsset, AssetPath);
                made = true;
            }
            Undo.RecordObject(spriteAsset, "Button icons");
            // The version first: UpdateLookupTables treats an asset with a material and no version as a legacy one and
            // rebuilds its tables from the (empty) legacy list, which would throw the entries below away.
            var so = new SerializedObject(spriteAsset);
            SerializedProperty version = so.FindProperty("m_Version");
            if (version != null) { version.stringValue = "1.1.0"; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning("[Button icons] TMP_SpriteAsset has no m_Version field in this TextMesh Pro: if the icons come out empty, say so.");

            spriteAsset.spriteSheet = texture;
            Material material = spriteAsset.material;
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null) { Debug.LogError("[Button icons] The TextMeshPro/Sprite shader is missing (TMP Essentials not imported?)."); return; }
            if (material == null)
            {
                material = new Material(shader) { name = "Button icons (sprite material)", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(material, spriteAsset);
                spriteAsset.material = material;
                log.AppendLine("  a material on TextMeshPro/Sprite, as a sub-asset");
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);

            // 3. The metrics. The sprite asset's point size is the "em" its glyph units are read against: a glyph of
            // height h draws at h * (fontSize / pointSize) px. The tallest icon (128) should draw at IconToCapHeight x the
            // UI font's cap height, so pointSize = 128 / (IconToCapHeight x capHeight in em). Each icon is centred on the cap band:
            // its top sits at h/2 + capHeight/2 above the baseline (bearingY), which for a disc puts a tenth of it under
            // the line, like a lower-case descender would.
            float capEm = CapHeightEm(out string capFrom);
            float pointSize = TallestIcon / (IconToCapHeight * capEm);
            float capPx = TallestIcon / IconToCapHeight;   // the cap height in sheet pixels at that point size
            FaceInfo face = spriteAsset.faceInfo;
            face.pointSize = pointSize;
            face.scale = 1f;
            face.ascentLine = TallestIcon / 2f + capPx / 2f;
            face.descentLine = -(TallestIcon / 2f - capPx / 2f);
            face.baseline = 0f;
            face.lineHeight = TallestIcon * 1.1f;
            spriteAsset.faceInfo = face;

            // The glyphs and the characters, one each per entry; TMP's rects are bottom-left origin.
            List<TMP_SpriteGlyph> glyphs = spriteAsset.spriteGlyphTable;
            List<TMP_SpriteCharacter> characters = spriteAsset.spriteCharacterTable;
            glyphs.Clear();
            characters.Clear();
            var seen = new HashSet<string>();
            uint index = 0;
            foreach (Entry e in sheet.icons)
            {
                if (e == null || string.IsNullOrEmpty(e.name) || e.w <= 0 || e.h <= 0) continue;
                if (!seen.Add(e.name)) { Debug.LogWarning("[Button icons] Duplicate icon name skipped: " + e.name); continue; }
                var glyph = new TMP_SpriteGlyph();
                glyph.index = index;
                glyph.metrics = new GlyphMetrics(e.w, e.h, 0f, e.h / 2f + capPx / 2f, e.w);
                glyph.glyphRect = new GlyphRect(e.x, sheet.height - (e.y + e.h), e.w, e.h);
                glyph.scale = 1f;
                glyph.atlasIndex = 0;
                glyphs.Add(glyph);
                var character = new TMP_SpriteCharacter(0xFFFE, glyph);
                character.glyphIndex = glyph.index;
                character.name = e.name;
                character.scale = 1f;
                characters.Add(character);
                index++;
            }
            spriteAsset.UpdateLookupTables();
            int probe = spriteAsset.GetSpriteIndexFromName("ps_cross");
            EditorUtility.SetDirty(spriteAsset);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            log.AppendLine($"  {characters.Count} icons on {texture.width} x {texture.height}; point size {pointSize:0.0} (cap height {capEm:0.000} em from {capFrom}): a disc draws at {IconToCapHeight:0.00} x the cap height");
            if (probe < 0) Debug.LogError("[Button icons] The lookup can't find \"ps_cross\" after the build: the names won't resolve. Say so.");

            // 4. TextMesh Pro's default sprite asset, with the old one remembered for the undo.
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
                Debug.LogError("[Button icons] TMP Settings (Assets/TextMesh Pro/Resources/TMP Settings.asset) is missing: the tags won't resolve until the asset is TMP's default sprite asset.");
            else
            {
                var settingsObject = new SerializedObject(settings);
                SerializedProperty defaultSprite = settingsObject.FindProperty("m_defaultSpriteAsset");
                if (defaultSprite == null)
                    Debug.LogError("[Button icons] TMP Settings has no m_defaultSpriteAsset field in this TextMesh Pro: set Default Sprite Asset to Button icons by hand (Edit › Project Settings › TextMesh Pro › Settings).");
                else
                {
                    if (!EditorPrefs.GetBool(PreviousDefaultKnownKey, false))
                    {
                        var old = defaultSprite.objectReferenceValue;
                        string oldPath = old != null ? AssetDatabase.GetAssetPath(old) : "";
                        EditorPrefs.SetString(PreviousDefaultKey, string.IsNullOrEmpty(oldPath) ? "" : AssetDatabase.AssetPathToGUID(oldPath));
                        EditorPrefs.SetBool(PreviousDefaultKnownKey, true);
                        log.AppendLine("  TMP's default sprite asset was " + (old != null ? old.name : "none") + " (remembered for the undo)");
                    }
                    Undo.RecordObject(settings, "Button icons as TMP's default sprite asset");
                    defaultSprite.objectReferenceValue = spriteAsset;
                    settingsObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    log.AppendLine("  TMP Settings › Default Sprite Asset = Button icons");
                }
            }
            Debug.Log($"[Button icons] {(made ? "Made" : "Rebuilt")} {AssetPath}:\n{log}" +
                      "Check: Fixit Fidget › Checks › Controller (Play Mode) and any HUD photo: button names are icons, never words or raw <sprite> tags.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Button icons] " + e.Message);
            Debug.LogException(e);
        }
    }

    [MenuItem(MakeMenu, true)]
    static bool CanMake() => !Application.isPlaying;

    [MenuItem(UndoMenu)]
    static void UndoStep()
    {
        try
        {
            if (Application.isPlaying) { Debug.LogError("[Button icons] Not during Play mode."); return; }
            TMP_Settings settings = TMP_Settings.instance;
            if (settings != null)
            {
                var settingsObject = new SerializedObject(settings);
                SerializedProperty defaultSprite = settingsObject.FindProperty("m_defaultSpriteAsset");
                if (defaultSprite != null)
                {
                    TMP_SpriteAsset old = null;
                    string guid = EditorPrefs.GetString(PreviousDefaultKey, "");
                    if (!string.IsNullOrEmpty(guid)) old = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    Undo.RecordObject(settings, "Button icons undone");
                    defaultSprite.objectReferenceValue = old;
                    settingsObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
                EditorPrefs.DeleteKey(PreviousDefaultKey);
                EditorPrefs.DeleteKey(PreviousDefaultKnownKey);
            }
            bool removed = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath) != null && AssetDatabase.DeleteAsset(AssetPath);
            Debug.Log($"[Button icons] Undone: TMP's default sprite asset put back; {(removed ? "the asset removed" : "no asset to remove")}. The game shows the words again (PadInput.Word).");
        }
        catch (Exception e)
        {
            Debug.LogError("[Button icons] " + e.Message);
        }
    }

    [MenuItem(UndoMenu, true)]
    static bool CanUndo() => !Application.isPlaying && AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath) != null;

    // The sheet as a flat 2D texture: no compression (flat colours and hard edges would ring), no mipmaps (it is drawn
    // near its size; TMP's sprite shader has none), its alpha the icons' edges, never read back by code.
    static void ImportSheet(StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(PngPath); importer = AssetImporter.GetAtPath(PngPath) as TextureImporter; }
        if (importer == null) throw new InvalidOperationException("No texture importer for " + PngPath);
        bool changed = importer.textureType != TextureImporterType.Default || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled || !importer.alphaIsTransparency || importer.isReadable || importer.npotScale != TextureImporterNPOTScale.None
            || importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear || importer.maxTextureSize < 2048 || !importer.sRGBTexture;
        importer.textureType = TextureImporterType.Default;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.sRGBTexture = true;
        if (changed) { importer.SaveAndReimport(); log.AppendLine("  the sheet's importer: uncompressed, no mipmaps, alpha is transparency, not readable"); }
        else log.AppendLine("  the sheet's importer was already right");
    }

    // The UI font's cap height in em: the swap point's font (Resources/UI/UI font, UiSkin), else TMP's default font, else
    // 0.70 (LiberationSans is 0.729; Baloo 2 is 0.602, drawn at a face scale of 1.12: 0.674).
    static float CapHeightEm(out string from)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(UiSkin.FontResource);
        from = font != null ? "the UI font (" + font.name + ")" : "";
        if (font == null) { font = TMP_Settings.defaultFontAsset; from = font != null ? "TMP's default font (" + font.name + ")" : ""; }
        // The face scale counts: the UI font draws its glyphs at faceInfo.scale (UiFontSteps fits Baloo 2 to Liberation's x-height
        // with it), and a sprite's size doesn't carry that scale.
        if (font != null && font.faceInfo.pointSize > 0f && font.faceInfo.capLine > 0f)
            return Mathf.Clamp(font.faceInfo.capLine / font.faceInfo.pointSize * (font.faceInfo.scale > 0f ? font.faceInfo.scale : 1f), .5f, .9f);
        from = "the 0.70 em assumption (no font found)";
        return .7f;
    }
}
#endif
