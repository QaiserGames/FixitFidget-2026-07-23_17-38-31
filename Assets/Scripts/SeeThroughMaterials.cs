using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// SEE-THROUGH COPIES OF MATERIALS (30 Sept 2026; moved out of NightSeeThrough)
//
// One place that makes, keeps and hands out the see-through copy of a material: the same albedo,
// colour, cut-out and glow, drawn with the dither shader "Fixit Fidget/Night see-through" (a
// screen-door of dots set per renderer, see the shader). The night's buildings in the way
// (NightSeeThrough), the café's cut-away walls (CutawayWall) and Grace's walls (GraceHouse) all
// wear these, so a wall the night has dotted and the day has ghosted is the same copy. A copy is
// made once per source material and lives for the Play session (never saved).
//
// The shader comes from the scene (CafeViewMode holds it, so a build keeps it) or, failing that,
// by name. Without it a blended copy stands in (URP's surface switch to Transparent), which is
// murkier where surfaces overlap; the walls then hide instead (CutawayWall).
// ---------------------------------------------------------------------------
public static class SeeThroughMaterials
{
    public const string DitherShaderName = "Fixit Fidget/Night see-through";
    public static readonly int SeeThroughId = Shader.PropertyToID("_SeeThrough");
    public static readonly int SeeThroughFromId = Shader.PropertyToID("_SeeThroughFrom");
    public static readonly int SeeThroughToId = Shader.PropertyToID("_SeeThroughTo");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    static readonly int CullId = Shader.PropertyToID("_Cull");
    // Synty's POLYGON shaders (Generic_Basic) name theirs like this.
    static readonly int SyntyAlbedoId = Shader.PropertyToID("_Albedo_Map");
    static readonly int SyntyEmissionMapId = Shader.PropertyToID("_Emission_Map");
    static readonly int SyntyEmissionColorId = Shader.PropertyToID("_Emission_Color");
    static readonly int SyntyEnableEmissionId = Shader.PropertyToID("_Enable_Emission");
    static readonly int SyntyClipId = Shader.PropertyToID("_Alpha_Clip_Threshold");

    static Shader dither, provided;
    static bool looked;
    static readonly Dictionary<Material, Material> copies = new();
    static MaterialPropertyBlock block;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        // The copies of the last Play session went with it (ordinary objects, PlaySessionLeftovers).
        copies.Clear();
        worn.Clear();
        dither = provided = null;
        looked = false;
        block = null;
    }

    /// <summary>A scene component hands the shader over (a reference a build keeps); by name otherwise.</summary>
    public static void Provide(Shader shader)
    {
        if (shader == null) return;
        provided = dither = shader;
        looked = true;
    }

    /// <summary>The dither shader, or null when it can't be found (copies are then blended).</summary>
    public static Shader Dither
    {
        get
        {
            if (!looked)
            {
                looked = true;
                dither = provided != null ? provided : Shader.Find(DitherShaderName);
            }
            return dither;
        }
    }

    public static bool Dithered => Dither != null;

    /// <summary>How many copies exist right now (the night's notes).</summary>
    public static int Made
    {
        get
        {
            int n = 0;
            foreach (var pair in copies) if (pair.Value != null && pair.Value != pair.Key) n++;
            return n;
        }
    }

    /// <summary>What a copy can be made of: anything with an albedo to dot (POLYGON's or URP Lit's), or, blended, anything with URP's surface switch.</summary>
    public static bool CanCopy(Material m) => m != null && (Dither != null
        ? m.HasProperty(SyntyAlbedoId) || m.HasProperty(BaseMapId)
        : m.HasProperty(SurfaceId));

    /// <summary>
    /// The see-through copy of a material (made once): the source itself when it is already see-through
    /// (glass), or null when its shader has nothing to copy from (the caller hides the renderer instead).
    /// </summary>
    public static Material Copy(Material source)
    {
        if (source == null) return null;
        if (copies.TryGetValue(source, out var copy) && copy != null) return copy;
        copy = Dither != null ? DitherCopy(source) : BlendedCopy(source);
        copies[source] = copy;
        return copy;
    }

    // ---- wearing a copy (the cut-away walls, Grace's walls) ----
    //
    // A renderer wears one copy for as long as anyone needs it (two walls meeting at a corner may
    // both claim a picture) and gets its own materials back when the last one lets go. A piece whose
    // shader has no see-through hides instead. NightSeeThrough keeps its own books for whole
    // buildings; the two never touch the same renderer (it skips the café room and Grace's rooms).

    sealed class Wearing { public int holders; public Material[] own, copy; public bool hidden; }
    static readonly Dictionary<Renderer, Wearing> worn = new();

    /// <summary>True while this renderer wears a copy (or hides in place of one).</summary>
    public static bool IsWorn(Renderer r) => r != null && worn.ContainsKey(r);

    /// <summary>
    /// Put the see-through copy on (the first holder swaps the materials; later holders only count).
    /// hideInstead: don't ghost, hide (the fallback for a wall that can't fade). Returns false when
    /// the renderer hides rather than ghosts.
    /// </summary>
    public static bool Wear(Renderer r, bool hideInstead = false)
    {
        if (r == null) return false;
        if (worn.TryGetValue(r, out Wearing w)) { w.holders++; return !w.hidden; }
        Material[] own = r.sharedMaterials;
        var copy = new Material[own.Length];
        bool hide = hideInstead;
        for (int m = 0; m < own.Length && !hide; m++)
        {
            copy[m] = Copy(own[m]);
            if (own[m] != null && copy[m] == null) hide = true;
        }
        w = new Wearing { holders = 1, own = own, copy = hide ? own : copy, hidden = hide };
        worn[r] = w;
        if (hide) r.forceRenderingOff = true;
        else r.sharedMaterials = copy;
        return !hide;
    }

    /// <summary>Let go: the last holder gives the renderer its own materials back (or shows it again).</summary>
    public static void TakeOff(Renderer r)
    {
        if (r == null || !worn.TryGetValue(r, out Wearing w)) return;
        if (--w.holders > 0) return;
        worn.Remove(r);
        if (w.hidden) { r.forceRenderingOff = false; return; }
        // Its own materials back, unless something else has dressed it since (the evening glow, a
        // window going dark): then what it wears now is right and is left alone.
        if (StillWears(r, w.copy)) r.sharedMaterials = w.own;
        r.SetPropertyBlock(null);
    }

    static bool StillWears(Renderer r, Material[] copy)
    {
        Material[] now = r.sharedMaterials;
        if (now.Length != copy.Length) return false;
        for (int i = 0; i < now.Length; i++) if (now[i] != copy[i]) return false;
        return true;
    }

    /// <summary>
    /// The dots for one worn renderer: keep = how much is drawn (1 = all, .2 = one dot in five). With to
    /// above from, the surface is solid below from (world height), the ghost above to, feathered between.
    /// A renderer that hides instead, or wears nothing, is left alone.
    /// </summary>
    public static void Show(Renderer r, float keep, float from = 0f, float to = 0f)
    {
        if (r == null || !worn.TryGetValue(r, out Wearing w) || w.hidden) return;
        block ??= new MaterialPropertyBlock();
        block.Clear();
        block.SetFloat(SeeThroughId, keep);
        block.SetFloat(SeeThroughFromId, from);
        block.SetFloat(SeeThroughToId, to);
        r.SetPropertyBlock(block);
    }

    static Material DitherCopy(Material source)
    {
        bool synty = source.HasProperty(SyntyAlbedoId), lit = source.HasProperty(BaseMapId);
        if (!synty && !lit) return null;
        if (source.HasProperty(SurfaceId) && source.GetFloat(SurfaceId) > .5f) return source;   // glass: already see-through
        var copy = new Material(dither) { name = source.name + " (see-through)", hideFlags = PlaySessionLeftovers.RuntimeFlags };
        int albedo = synty ? SyntyAlbedoId : BaseMapId;
        copy.SetTexture(BaseMapId, source.GetTexture(albedo));
        copy.SetTextureScale(BaseMapId, source.GetTextureScale(albedo));
        copy.SetTextureOffset(BaseMapId, source.GetTextureOffset(albedo));
        copy.SetColor(BaseColorId, source.HasProperty(BaseColorId) ? source.GetColor(BaseColorId) : Color.white);
        if (source.HasProperty(SmoothnessId)) copy.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
        if (source.HasProperty(MetallicId)) copy.SetFloat(MetallicId, source.GetFloat(MetallicId));
        if (source.HasProperty(CullId)) copy.SetFloat(CullId, source.GetFloat(CullId));
        // Cut-out parts of the atlas (POLYGON clips at half alpha).
        bool clips = source.IsKeywordEnabled("_ALPHATEST_ON") || source.HasProperty(AlphaClipId) && source.GetFloat(AlphaClipId) > .5f;
        if (clips)
        {
            copy.EnableKeyword("_ALPHATEST_ON");
            copy.SetFloat(AlphaClipId, 1f);
            copy.SetFloat(CutoffId, source.HasProperty(SyntyClipId) ? source.GetFloat(SyntyClipId)
                : source.HasProperty(CutoffId) ? source.GetFloat(CutoffId) : .5f);
        }
        // The glow: POLYGON's (the night's lit windows and signs), or URP Lit's.
        if (synty && source.HasProperty(SyntyEnableEmissionId) && source.GetFloat(SyntyEnableEmissionId) > .5f && source.HasProperty(SyntyEmissionMapId))
        {
            copy.SetTexture(EmissionMapId, source.GetTexture(SyntyEmissionMapId));
            copy.SetColor(EmissionColorId, source.HasProperty(SyntyEmissionColorId) ? source.GetColor(SyntyEmissionColorId) : Color.black);
            copy.EnableKeyword("_EMISSION");
        }
        else if (!synty && source.IsKeywordEnabled("_EMISSION"))
        {
            copy.SetTexture(EmissionMapId, source.GetTexture(EmissionMapId));
            copy.SetColor(EmissionColorId, source.GetColor(EmissionColorId));
            copy.EnableKeyword("_EMISSION");
        }
        copy.renderQueue = source.renderQueue;
        return copy;
    }

    static Material BlendedCopy(Material source)
    {
        if (!source.HasProperty(SurfaceId)) return null;
        var copy = new Material(source) { name = source.name + " (see-through)", hideFlags = PlaySessionLeftovers.RuntimeFlags };
        // URP's surface options, which POLYGON's shader graph also exposes (Allow Material Override).
        copy.SetFloat(SurfaceId, 1f);                                   // Transparent
        Set(copy, "_Blend", 0f);                                         // Alpha
        Set(copy, "_SrcBlend", (float)BlendMode.SrcAlpha);
        Set(copy, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        Set(copy, "_SrcBlendAlpha", (float)BlendMode.One);
        Set(copy, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        Set(copy, "_ZWrite", 0f);
        Set(copy, "_AlphaClip", 0f);                                     // POLYGON clips at 0.5 alpha: off, or it would vanish
        Set(copy, "_AlphaToMask", 0f);
        copy.DisableKeyword("_ALPHATEST_ON");
        copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        copy.DisableKeyword("_ALPHAMODULATE_ON");
        copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        copy.SetOverrideTag("RenderType", "Transparent");
        copy.renderQueue = (int)RenderQueue.Transparent;
        return copy;
    }

    static void Set(Material m, string property, float value)
    {
        if (m.HasProperty(property)) m.SetFloat(property, value);
    }
}
