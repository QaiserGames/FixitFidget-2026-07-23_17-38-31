#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ---------------------------------------------------------------------------
// FIXIT FIDGET > PERFORMANCE > PERFORMANCE 2 - THE QUALITY PRESETS (30 Sept 2026)
//
// Mansoor: the game should "work good on any system". On his machine (RTX 5080) the build holds
// 240 fps at 4K with the current settings, so "any system" is about machines with far less. The
// answer is three presets the player (and the game's first-run guess, see QualityPreset) can pick:
//
//   High    the PC pipeline asset as it is: 50 m of shadow, 4 cascades, 2048 shadow maps for the
//           main light and the additional lights, soft shadows (high), SSAO at full size, HDR.
//   Medium  a copy with 40 m of shadow, 2 cascades, 2048 / 1024 shadow maps, soft shadows
//           (medium), SSAO at half size, HDR.  Aim: 4K60 on a mid-range card.
//   Low     a copy with 25 m of shadow, 1 cascade, a 1024 main shadow map, no additional light
//           shadows, hard shadows, no SSAO, no HDR (the opaque texture stays: glass and water read
//           it).  Aim: 1080p60 on integrated graphics.
//
// The step copies the PC pipeline asset and renderer (so the renderers stay identical apart from
// SSAO) into Assets/Settings/Medium_* and Low_*, and rewrites the quality levels to Low, Medium,
// High (the template's "Mobile" becomes Low, "PC" becomes High), Standalone defaulting to High.
// Run it again after changing the PC asset: Medium and Low are remade from it.
// ---------------------------------------------------------------------------
internal static class QualityPresetSteps
{
    const string Menu = "Fixit Fidget/Performance/Performance 2 - Make the quality presets (Low, Medium, High)";
    const string Tag = "[Quality] ";
    const string Folder = "Assets/Settings";

    sealed class Preset
    {
        public string name;
        public float shadowDistance;
        public int cascades, mainShadowMap, additionalShadowMap;
        public bool additionalLightShadows, softShadows, hdr, opaqueTexture, ssao, ssaoDownsample;
        public int softShadowQuality;   // 1 low, 2 medium, 3 high (URP's SoftShadowQuality)
    }

    static readonly Preset Medium = new Preset
    {
        name = "Medium", shadowDistance = 40f, cascades = 2, mainShadowMap = 2048, additionalShadowMap = 1024,
        additionalLightShadows = true, softShadows = true, softShadowQuality = 2, hdr = true, opaqueTexture = true,
        ssao = true, ssaoDownsample = true
    };

    static readonly Preset Low = new Preset
    {
        name = "Low", shadowDistance = 25f, cascades = 1, mainShadowMap = 1024, additionalShadowMap = 512,
        additionalLightShadows = false, softShadows = false, softShadowQuality = 1, hdr = false, opaqueTexture = true,
        ssao = false, ssaoDownsample = true
    };

    [MenuItem(Menu)]
    static void Run()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            int pcIndex = Array.IndexOf(QualitySettings.names, "PC");
            if (pcIndex < 0) pcIndex = Array.IndexOf(QualitySettings.names, "High");
            if (pcIndex < 0) throw new InvalidOperationException("No quality level called PC or High; the presets are made from it.");
            var high = QualitySettings.GetRenderPipelineAssetAt(pcIndex) as UniversalRenderPipelineAsset;
            if (high == null) throw new InvalidOperationException("The PC quality level has no URP pipeline asset.");
            string highPath = AssetDatabase.GetAssetPath(high);
            var highRenderer = RendererOf(high);
            if (highRenderer == null) throw new InvalidOperationException("The PC pipeline asset has no renderer.");
            string highRendererPath = AssetDatabase.GetAssetPath(highRenderer);

            var medium = MakePreset(Medium, highPath, highRendererPath);
            var low = MakePreset(Low, highPath, highRendererPath);
            AssetDatabase.SaveAssets();

            WriteQualityLevels(low, medium, high);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + $"Presets made: Low ({AssetDatabase.GetAssetPath(low)}), Medium ({AssetDatabase.GetAssetPath(medium)}), " +
                      $"High ({highPath}). Quality levels are now {string.Join(", ", QualitySettings.names)}; Standalone defaults to High. " +
                      "F4 cycles them in Play or a build; the F3 overlay names the current one.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Couldn't make the presets: " + e.Message);
        }
    }

    [MenuItem(Menu, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static ScriptableRendererData RendererOf(UniversalRenderPipelineAsset asset)
    {
        var so = new SerializedObject(asset);
        var list = so.FindProperty("m_RendererDataList");
        if (list == null || list.arraySize == 0) return null;
        return list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
    }

    static UniversalRenderPipelineAsset MakePreset(Preset p, string highPath, string highRendererPath)
    {
        // The renderer first (a copy of the PC renderer with SSAO set for this preset), then the
        // pipeline asset (a copy of the PC asset pointed at that renderer, with the shadow settings).
        string rendererPath = $"{Folder}/{p.name}_Renderer.asset";
        string assetPath = $"{Folder}/{p.name}_RPAsset.asset";
        Copy(highRendererPath, rendererPath);
        Copy(highPath, assetPath);

        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
        if (renderer == null) throw new InvalidOperationException("Couldn't copy the renderer to " + rendererPath);
        foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
        {
            if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
            feature.SetActive(p.ssao);
            var fso = new SerializedObject(feature);
            var downsample = fso.FindProperty("m_Settings.Downsample");
            if (downsample != null) downsample.boolValue = p.ssaoDownsample;
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }
        EditorUtility.SetDirty(renderer);

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null) throw new InvalidOperationException("Couldn't copy the pipeline asset to " + assetPath);
        var so = new SerializedObject(asset);
        so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        Set(so, "m_ShadowDistance", p.shadowDistance);
        Set(so, "m_ShadowCascadeCount", p.cascades);
        Set(so, "m_MainLightShadowmapResolution", p.mainShadowMap);
        Set(so, "m_AdditionalLightsShadowmapResolution", p.additionalShadowMap);
        Set(so, "m_AdditionalLightShadowsSupported", p.additionalLightShadows);
        Set(so, "m_SoftShadowsSupported", p.softShadows);
        Set(so, "m_SoftShadowQuality", p.softShadowQuality);
        Set(so, "m_SupportsHDR", p.hdr);
        Set(so, "m_RequireOpaqueTexture", p.opaqueTexture);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void Copy(string from, string to)
    {
        if (AssetDatabase.LoadMainAssetAtPath(to) != null && !AssetDatabase.DeleteAsset(to))
            throw new InvalidOperationException("Couldn't replace " + to);
        if (!AssetDatabase.CopyAsset(from, to)) throw new InvalidOperationException($"Couldn't copy {from} to {to}");
    }

    static void Set(SerializedObject so, string field, float value)
    {
        var prop = so.FindProperty(field) ?? throw new InvalidOperationException("No field " + field + " on the pipeline asset (URP changed?)");
        prop.floatValue = value;
    }

    static void Set(SerializedObject so, string field, int value)
    {
        var prop = so.FindProperty(field) ?? throw new InvalidOperationException("No field " + field + " on the pipeline asset (URP changed?)");
        prop.intValue = value;   // enums too: the underlying value, not the index
    }

    static void Set(SerializedObject so, string field, bool value)
    {
        var prop = so.FindProperty(field) ?? throw new InvalidOperationException("No field " + field + " on the pipeline asset (URP changed?)");
        prop.boolValue = value;
    }

    // The quality levels, rewritten in place: Low, Medium, High, each with its pipeline asset. The
    // built-in shadow fields of a level mean nothing under URP (the pipeline asset decides), so only
    // the name, the pipeline asset and VSync are set; everything else is copied from the PC level.
    static void WriteQualityLevels(RenderPipelineAsset low, RenderPipelineAsset medium, RenderPipelineAsset high)
    {
        UnityEngine.Object settings = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset"))
            if (o != null) { settings = o; break; }
        if (settings == null) throw new InvalidOperationException("Couldn't load ProjectSettings/QualitySettings.asset");
        var so = new SerializedObject(settings);
        var levels = so.FindProperty("m_QualitySettings");
        int pc = IndexOf(levels, "PC", "High");
        if (pc < 0) throw new InvalidOperationException("No PC/High quality level to copy from.");

        // Rebuild: keep the PC element's values as the template for all three.
        var template = levels.GetArrayElementAtIndex(pc);
        var wanted = new List<(string name, RenderPipelineAsset asset)> { ("Low", low), ("Medium", medium), ("High", high) };
        // Remove every level that isn't one of the three, then make sure the three exist in order.
        for (int i = levels.arraySize - 1; i >= 0; i--)
        {
            string n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            if (n != "Low" && n != "Medium" && n != "High" && n != "PC" && n != "Mobile") levels.DeleteArrayElementAtIndex(i);
        }
        // Map the template's old names onto the new ones.
        Rename(levels, "Mobile", "Low");
        Rename(levels, "PC", "High");
        for (int w = 0; w < wanted.Count; w++)
        {
            int at = IndexOf(levels, wanted[w].name);
            if (at < 0)
            {
                levels.InsertArrayElementAtIndex(Mathf.Min(w, levels.arraySize));
                at = Mathf.Min(w, levels.arraySize - 1);
                CopyLevel(levels.GetArrayElementAtIndex(IndexOf(levels, "High")), levels.GetArrayElementAtIndex(at));
                levels.GetArrayElementAtIndex(at).FindPropertyRelative("name").stringValue = wanted[w].name;
            }
            else if (at != w)
            {
                levels.MoveArrayElement(at, w);
            }
            var level = levels.GetArrayElementAtIndex(w);
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = wanted[w].asset;
            level.FindPropertyRelative("vSyncCount").intValue = 1;
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            if (excluded != null) excluded.arraySize = 0;
        }
        so.FindProperty("m_CurrentQuality").intValue = 2;
        var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
        if (perPlatform != null && perPlatform.isArray)
        {
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                var pair = perPlatform.GetArrayElementAtIndex(i);
                var first = pair.FindPropertyRelative("first");
                var second = pair.FindPropertyRelative("second");
                if (first == null || second == null) continue;
                string platform = first.stringValue;
                second.intValue = platform == "Android" || platform == "iPhone" || platform == "WebGL" ? 0 : 2;
            }
        }
        else Debug.LogWarning(Tag + "Couldn't reach the per-platform defaults; set Standalone to High by hand in Project Settings > Quality.");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    static int IndexOf(SerializedProperty levels, params string[] names)
    {
        for (int i = 0; i < levels.arraySize; i++)
        {
            string n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            foreach (string name in names) if (n == name) return i;
        }
        return -1;
    }

    static void Rename(SerializedProperty levels, string from, string to)
    {
        int at = IndexOf(levels, from);
        if (at >= 0 && IndexOf(levels, to) < 0) levels.GetArrayElementAtIndex(at).FindPropertyRelative("name").stringValue = to;
        else if (at >= 0) levels.DeleteArrayElementAtIndex(at);
    }

    static void CopyLevel(SerializedProperty from, SerializedProperty to)
    {
        var f = from.Copy();
        var t = to.Copy();
        var end = from.GetEndProperty();
        if (!f.NextVisible(true) || !t.NextVisible(true)) return;
        while (!SerializedProperty.EqualContents(f, end))
        {
            switch (f.propertyType)
            {
                case SerializedPropertyType.Integer: t.intValue = f.intValue; break;
                case SerializedPropertyType.Boolean: t.boolValue = f.boolValue; break;
                case SerializedPropertyType.Float: t.floatValue = f.floatValue; break;
                case SerializedPropertyType.String: t.stringValue = f.stringValue; break;
                case SerializedPropertyType.Enum: t.intValue = f.intValue; break;
                case SerializedPropertyType.ObjectReference: t.objectReferenceValue = f.objectReferenceValue; break;
            }
            bool fn = f.NextVisible(false), tn = t.NextVisible(false);
            if (!fn || !tn) break;
        }
    }
}
#endif
