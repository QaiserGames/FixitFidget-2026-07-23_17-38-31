#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// ---------------------------------------------------------------------------
// THE UI FONT: BALOO 2 (19 Oct 2026, playtest 7)
//
// Her: "your text needs to be bolder", "thicker", "poppy", "cute". The game has lettered everything in TextMesh Pro's
// default LiberationSans since the UI skin (7 Oct), whose bold is the regular letters thickened by the shader. The studio
// proposed Baloo 2 on 9-10 Oct for the cozy, chunky look (it is in the Bible's "awaiting his verdict" list). This step makes
// it the game's lettering, at UiSkin's one swap point, with real weights:
//
//   Fixit Fidget › UI › Font - Baloo 2 as the game's UI font (with Undo)
//     1. a dynamic TMP_FontAsset from Assets/Fonts/Baloo2/Baloo2-SemiBold.ttf (SDFAA, sampling 90, padding 9, a 1024 atlas,
//        dynamic, multi-atlas on), saved at Assets/Data/Resources/UI/UI font.asset: the path UiSkin.FontResource loads;
//     2. its bold weight (the font weight table's 700 entry) a second dynamic asset from Baloo2-Bold.ttf, so <b> and
//        FontStyles.Bold are the Bold cut, not a thickened semibold;
//     3. TMP's default font (LiberationSans SDF) as its fallback, for any character Baloo 2 lacks;
//     4. the face metrics fitted to the UI (below).
//   Fixit Fidget › UI › Font - Undo
//     removes both assets: UiSkin falls back to the HUD's own font as before.
//
// THE METRICS. Baloo 2's cap height is 0.602 em and its x-height 0.460 (LiberationSans: 0.729 and 0.519): at the same
// size it reads 12-17% smaller, and its line box is 1.6 em tall (Devanagari room; Liberation's is 1.15), which would push
// every wrapped line apart and out of the boxes laid out for Liberation. So the asset's face scale is 1.12 (the x-heights
// match: 0.460 x 1.12 = 0.515) and its ascender, descender and line height are Liberation's divided by that scale, so a
// "24 px" line of Baloo 2 takes the room a 24 px line took before and reads the same size, in chunkier letters. The glyphs
// are untouched; only where TextMesh Pro puts the line. The TMP Font Asset inspector shows these under Face Info.
//
// The fonts: Baloo 2 by Ek Type, SIL Open Font License 1.1 (Assets/Fonts/Baloo2/OFL.txt): the static instances
// Medium 500, SemiBold 600 and Bold 700, made with fontTools from Google Fonts' Baloo2[wght].ttf.
//
// Why Baloo 2 over keeping Liberation and thickening it: a display face with real weights reads bolder without the
// synthesized bold's smeared counters, and it is the cozy register she asked for. It is a decision his to overrule.
// ---------------------------------------------------------------------------
public static class UiFontSteps
{
    public const string FontFolder = "Assets/Fonts/Baloo2";
    public const string SemiBoldTtf = FontFolder + "/Baloo2-SemiBold.ttf";
    public const string BoldTtf = FontFolder + "/Baloo2-Bold.ttf";
    /// <summary>UiSkin's swap point (Resources/UI/UI font) as an asset path.</summary>
    public const string UiFontAsset = "Assets/Data/Resources/UI/UI font.asset";
    public const string BoldAsset = FontFolder + "/Baloo2-Bold SDF.asset";

    const string MakeMenu = "Fixit Fidget/UI/Font - Baloo 2 as the game's UI font (with Undo)";
    const string UndoMenu = "Fixit Fidget/UI/Font - Undo (remove the UI font: LiberationSans as before)";

    // The creation settings (the spec's): SDFAA, sampling 90, padding 9, a 1024 atlas, dynamic, multi-atlas on.
    const int SamplingPointSize = 90, AtlasPadding = 9, AtlasSize = 1024;
    // The face metrics (see the header): Liberation's line box, divided by the face scale that matches the x-heights.
    public const float FaceScale = 1.12f;
    const float AscentEm = .905f, DescentEm = -.212f, LineHeightEm = 1.149f;

    [MenuItem(MakeMenu)]
    static void Make()
    {
        try
        {
            if (Application.isPlaying) { Debug.LogError("[UI font] Not during Play mode."); return; }
            var semiBold = AssetDatabase.LoadAssetAtPath<Font>(SemiBoldTtf);
            var bold = AssetDatabase.LoadAssetAtPath<Font>(BoldTtf);
            if (semiBold == null || bold == null)
            {
                Debug.LogError($"[UI font] {SemiBoldTtf} and {BoldTtf} are missing (Assets/Fonts/Baloo2 holds the static instances and OFL.txt).");
                return;
            }
            var log = new StringBuilder();
            TMP_FontAsset boldAsset = MakeAsset(bold, BoldAsset, "Baloo2-Bold SDF", log);
            TMP_FontAsset uiFont = MakeAsset(semiBold, UiFontAsset, "UI font", log);
            if (boldAsset == null || uiFont == null) return;

            // The bold weight (700) is the Bold cut; the fallback TMP's default font (LiberationSans), for anything Baloo 2 lacks.
            Undo.RecordObject(uiFont, "Baloo 2 as the UI font");
            SetBoldWeight(uiFont, boldAsset, log);
            TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null && fallback != uiFont)
            {
                List<TMP_FontAsset> fallbacks = uiFont.fallbackFontAssetTable;
                if (fallbacks == null) { fallbacks = new List<TMP_FontAsset>(); uiFont.fallbackFontAssetTable = fallbacks; }
                if (!fallbacks.Contains(fallback)) fallbacks.Add(fallback);
                log.AppendLine($"  fallback: {fallback.name}");
            }
            else log.AppendLine("  no TMP default font to fall back on (TMP Settings missing?)");
            EditorUtility.SetDirty(uiFont);
            EditorUtility.SetDirty(boldAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UI font] Baloo 2 is the game's UI font (UiSkin.Font loads {UiFontAsset}):\n{log}" +
                      "Check: any HUD photo (the lettering is Baloo 2, bold where it instructs); Fixit Fidget › UI › Button icons again afterwards " +
                      "so the icons size to this font's cap height. His to overrule: Fixit Fidget › UI › Font - Undo.");
        }
        catch (Exception e)
        {
            Debug.LogError("[UI font] " + e.Message);
            Debug.LogException(e);
        }
    }

    [MenuItem(MakeMenu, true)]
    static bool CanMake() => !Application.isPlaying;

    [MenuItem(UndoMenu)]
    static void UndoStep()
    {
        if (Application.isPlaying) { Debug.LogError("[UI font] Not during Play mode."); return; }
        bool ui = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontAsset) != null && AssetDatabase.DeleteAsset(UiFontAsset);
        bool b = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldAsset) != null && AssetDatabase.DeleteAsset(BoldAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UI font] Undone: {(ui ? "the UI font asset removed" : "no UI font asset")}, {(b ? "the bold asset removed" : "no bold asset")}. " +
                  "UiSkin falls back to the HUD's own font (LiberationSans) as before. The .ttf files and OFL.txt stay in Assets/Fonts/Baloo2.");
    }

    [MenuItem(UndoMenu, true)]
    static bool CanUndo() => !Application.isPlaying && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontAsset) != null;

    // A dynamic SDF font asset from a .ttf, saved with its atlas and material as sub-assets. A second run makes it afresh
    // (nothing references these two by GUID: UiSkin loads the UI font by its Resources path, and the UI font holds the bold).
    static TMP_FontAsset MakeAsset(Font font, string path, string name, StringBuilder log)
    {
        TMP_FontAsset fresh = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
            AtlasPopulationMode.Dynamic, true);
        if (fresh == null) { Debug.LogError("[UI font] TextMesh Pro could not make a font asset from " + font.name); return null; }
        FitTheFace(fresh);
        bool again = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null;
        if (again) AssetDatabase.DeleteAsset(path);
        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
        fresh.name = name;
        AssetDatabase.CreateAsset(fresh, path);
        AddSubAssets(fresh, path, name);
        EditorUtility.SetDirty(fresh);
        log.AppendLine($"  {path}: {(again ? "made afresh" : "made")} from {font.name} ({SamplingPointSize} pt sampling, padding {AtlasPadding}, {AtlasSize} atlas, dynamic, SDFAA)");
        return fresh;
    }

    // The atlas texture and the material go into the asset file (as TMP's own creator does).
    static void AddSubAssets(TMP_FontAsset asset, string path, string name)
    {
        Texture2D atlas = asset.atlasTexture;
        if (atlas != null && AssetDatabase.GetAssetPath(atlas) != path)
        {
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }
        Material material = asset.material;
        if (material != null && AssetDatabase.GetAssetPath(material) != path)
        {
            material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);
        }
    }

    // The face metrics (see the header): the scale that matches Liberation's x-height, and Liberation's line box under it.
    static void FitTheFace(TMP_FontAsset asset)
    {
        FaceInfo face = asset.faceInfo;
        float point = face.pointSize > 0f ? face.pointSize : SamplingPointSize;
        face.scale = FaceScale;
        face.ascentLine = point * AscentEm / FaceScale;
        face.descentLine = point * DescentEm / FaceScale;
        face.lineHeight = point * LineHeightEm / FaceScale;
        face.baseline = 0f;
        asset.faceInfo = face;
    }

    // The font weight table's 700 entry: the Bold cut (through the API; through a SerializedObject if the table isn't there).
    static void SetBoldWeight(TMP_FontAsset uiFont, TMP_FontAsset boldAsset, StringBuilder log)
    {
        TMP_FontWeightPair[] table = uiFont.fontWeightTable;
        if (table != null && table.Length > 7)
        {
            table[7].regularTypeface = boldAsset;
            log.AppendLine($"  bold weight (700): {boldAsset.name}");
            return;
        }
        var so = new SerializedObject(uiFont);
        SerializedProperty weights = so.FindProperty("m_FontWeightTable");
        if (weights == null) { Debug.LogWarning("[UI font] No font weight table on the asset: set Font Weights › 700 › Regular Typeface to " + boldAsset.name + " by hand."); return; }
        if (weights.arraySize < 10) weights.arraySize = 10;
        SerializedProperty regular = weights.GetArrayElementAtIndex(7).FindPropertyRelative("regularTypeface");
        if (regular != null) regular.objectReferenceValue = boldAsset;
        so.ApplyModifiedProperties();
        log.AppendLine($"  bold weight (700): {boldAsset.name} (through the serialized table)");
    }
}
#endif
