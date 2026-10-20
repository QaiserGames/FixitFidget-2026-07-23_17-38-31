using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE OUTLINE ROUND A PART AT THE BENCH (the bench, v2; claude/bench-spec-v2.md §2.1)
//
// (BenchOutline, not Outline: UnityEngine.UI has an Outline of its own, and the recap phone uses it.)
//
// BenchOutline.Set(part, true) draws a white line round every mesh under `part` (a screw, a cover, a tool in its stand):
// each renderer gets a hull child drawn with "Fixit Fidget/Bench outline" (back faces, pushed out along the normals).
// The hull uses a copy of the mesh whose normals are smoothed across shared positions, because the parts are flat
// shaded (split normals) and a hull pushed along split normals opens at every edge. Hulls are kept and switched, not
// rebuilt, so hovering costs nothing after the first time. It replaces the old highlight, which tinted one renderer's
// material (fine for a cylinder, wrong for a model with four materials).
//
// CHECKLIST V2 (14 Oct 2026; /root/cafe/specs/uiux-checklist-v2.md §5.4). His playtest 5: "the checklist is terrible". The
// next thing to touch now glows on the bench itself (BenchGlow), so a part's line has two layers:
//   * THE POINTER'S (Set): white under the cursor, gold while a hold is under way (Busy). The hover and the hold, as before.
//   * THE GUIDE'S (Glow): the breathing mint on the next thing to touch, gold for "what's left", wider or narrower, and
//     dashed where something covers it (the x-ray pass, _XRay). Drawn only while the pointer's layer is off, so a part
//     under the cursor is always the plain white hover ("Precedence on one part": the hold, then the hover, then the glow).
// Each layer carries its own colour, width (a multiple of the hover's, _Width per hull through the property block) and
// x-ray, so the glow breathing every frame never touches the hover's look.
//
// A hull is shown and hidden with Renderer.forceRenderingOff, never Renderer.enabled. PlayerCarry remembers every renderer's
// `enabled` when Ace picks a device up and sets it back while he carries it and when he puts it down: a glow lit at the
// moment of the pickup (E in the close-up takes the device mid-repair) would be switched on again every frame of the carry,
// a mint line round a cover in the café that nothing would ever switch off. forceRenderingOff is ours alone. A hull's
// bounds are its part's own (the same mesh, the same place), so DeviceBounds measures the device the same with them.
//
// TWO TONES (19 Oct 2026; playtest 7, his girlfriend: the fresh part "is highlighted, but it doesn't show. You gotta make it
// red. Or highlight it."; the cloth's highlight "hidden in the bottom left"). The mint line had a luminance contrast of 1.01:1
// on the pale new-part pad and 1.04:1 on the yellow cloth; red would have 1.03:1 on the slate mat (the 19 Oct investigation
// §5): no one flat colour reads on every surface of the bench. So the guide's line is TWO hulls now, a narrow dark one (ink,
// warmed a little toward the amber) and a wider warm amber one outside it. A narrower hull's back faces lie nearer the camera
// than a wider one's, so with both writing depth the dark line sits against the part and the amber line outside it, whatever
// the part's colour: on the pale pad the ink carries the line (9.5:1), on the slate mat the amber does (2.8:1), and on the
// yellow cloth the ink again (9.3:1). Amber, not red: red reads as "wrong" (his plan, 19 Oct). The pointer's layer (the
// white hover, the gold hold) stays one line. Where a target is hidden, only the amber hull draws the dashed x-ray pass.
// ---------------------------------------------------------------------------
public static class BenchOutline
{
    public static readonly Color Hover = Color.white;
    public static readonly Color Busy = new Color(1f, .82f, .35f);       // a hold under way
    /// <summary>The glow on the next thing to touch: a warm amber (#FFB238; 19 Oct, in place of the mint that vanished on the
    /// pale pad and the cloth). Never red: red reads as "wrong".</summary>
    public static readonly Color Guide = new Color(1f, .698f, .22f);
    /// <summary>The amber's bright end, which the glow breathes toward (#FFD27A).</summary>
    public static readonly Color GuideSoft = new Color(1f, .824f, .478f);
    /// <summary>The dark line inside the amber one: the skin's ink, warmed a quarter of the way toward the amber (about 70 %
    /// ink), so it reads as the amber's own shadow, not a black ring.</summary>
    public static readonly Color GuideInk = Color.Lerp(UiSkin.Ink, Guide, .25f);
    /// <summary>"What's left" (Tab / View): PowerWash's yellow, our gold. Drawn two-tone too (the ink inside it), so it reads on
    /// the pad and the cloth; told from the guide's amber by what it does (everything left, for 1.5 s) more than by its hue.</summary>
    public static readonly Color Left = UiSkin.Gold;
    /// <summary>The hover line's width, per metre from the camera (the shader's own default; a layer's width is a multiple of it).</summary>
    public const float BaseWidth = .0035f;
    /// <summary>The two-tone guide's widths, as multiples of the hover's: the ink line to 0.65 (about 3 px at the close-up's
    /// zoom 0), the amber line from there out to 1.7 (about 8 px in all, so the amber band is 5 px: at least 4 px at 1080p).</summary>
    public const float GuideInnerWidth = .65f, GuideOuterWidth = 1.7f;

    static readonly int ColourId = Shader.PropertyToID("_Color");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int XRayId = Shader.PropertyToID("_XRay");

    // One target's hulls (one per renderer under it, beside the renderer it outlines; a second, wider one for the two-tone
    // guide) and its two layers.
    sealed class Lines
    {
        public readonly List<MeshRenderer> hulls = new List<MeshRenderer>();
        public readonly List<MeshRenderer> outerHulls = new List<MeshRenderer>();
        public readonly List<Renderer> sources = new List<Renderer>();
        public bool pointer, glow;
        public Color pointerColour = Hover, glowColour = Guide;
        public float pointerWidth = 1f, glowWidth = 1f;
        public bool pointerXRay, glowXRay;
        // The guide's second line (two-tone): on, its colour and its width (a multiple of the hover's, wider than the inner).
        public bool glowOuter, glowOuterXRay;
        public Color glowOuterColour = Guide;
        public float glowOuterWidth = GuideOuterWidth;
        // What was last put on the hulls, so an unchanged call costs nothing.
        public bool shownOn;
        public Color shownColour;
        public float shownWidth = -1f;
        public bool shownXRay;
        public bool shownOuterOn;
        public Color shownOuterColour;
        public float shownOuterWidth = -1f;
        public bool shownOuterXRay;
    }

    static Material material;
    static MaterialPropertyBlock block, outerBlock;
    static readonly Dictionary<Mesh, Mesh> smoothed = new Dictionary<Mesh, Mesh>();
    static readonly Dictionary<GameObject, Lines> targets = new Dictionary<GameObject, Lines>();
    static readonly List<GameObject> gone = new List<GameObject>();
    static readonly List<Renderer> found = new List<Renderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // A Play session without a domain reload keeps statics: last session's hulls went with its scene.
        smoothed.Clear();
        targets.Clear();
        material = null;
        block = null;
        outerBlock = null;
    }

    // ------------------------------------------------------------------ the pointer's layer (the hover, a hold)

    /// <summary>Draws the white outline round everything under <paramref name="target"/>, or takes it away.</summary>
    public static void Set(GameObject target, bool on) => Set(target, on, Hover, 1f, false);

    public static void Set(GameObject target, bool on, Color colour) => Set(target, on, colour, 1f, false);

    /// <summary>
    /// The pointer's line: <paramref name="colour"/>, <paramref name="widthScale"/> times the hover's width, dashed where
    /// hidden when <paramref name="xray"/>. Drawn over the guide's glow on the same part.
    /// </summary>
    public static void Set(GameObject target, bool on, Color colour, float widthScale, bool xray)
    {
        Lines lines = For(target, on);
        if (lines == null) return;
        lines.pointer = on;
        if (on)
        {
            lines.pointerColour = colour;
            lines.pointerWidth = widthScale;
            lines.pointerXRay = xray;
        }
        Apply(lines);
    }

    /// <summary>The pointer's line is on <paramref name="target"/> (hovered or held): the guide's glow stands aside for it.</summary>
    public static bool PointerOn(GameObject target) => target != null && targets.TryGetValue(target, out Lines lines) && lines.pointer;

    // ------------------------------------------------------------------ the guide's layer (BenchGlow)

    /// <summary>
    /// The guide's glow round everything under <paramref name="target"/>: <paramref name="colour"/>, <paramref name="widthScale"/>
    /// times the hover's width, dashed where something covers it when <paramref name="xray"/>. Shown only while the pointer's
    /// line is off on it. Called every frame while it breathes: an unchanged look costs nothing, a changed one one property
    /// block per hull, and nothing is allocated after the hulls are made.
    /// </summary>
    public static void Glow(GameObject target, bool on, Color colour, float widthScale, bool xray)
    {
        Lines lines = For(target, on);
        if (lines == null) return;
        lines.glow = on;
        lines.glowOuter = false;
        if (on)
        {
            lines.glowColour = colour;
            lines.glowWidth = widthScale;
            lines.glowXRay = xray;
        }
        Apply(lines);
    }

    /// <summary>
    /// The guide's glow in two tones (19 Oct): a narrow <paramref name="inner"/> line against the part, <paramref name="innerWidth"/>
    /// times the hover's width, and a wider <paramref name="outer"/> line outside it to <paramref name="outerWidth"/> times. Where
    /// something covers the part, the outer line alone is drawn dashed when <paramref name="xray"/>. Shown only while the
    /// pointer's line is off on it, as the one-tone glow is.
    /// </summary>
    public static void Glow(GameObject target, bool on, Color inner, float innerWidth, Color outer, float outerWidth, bool xray)
    {
        Lines lines = For(target, on);
        if (lines == null) return;
        lines.glow = on;
        lines.glowOuter = on;
        if (on)
        {
            lines.glowColour = inner;
            lines.glowWidth = innerWidth;
            lines.glowXRay = false;   // the dashes are the outer line's: dark dashes through a cover would read as cracks
            lines.glowOuterColour = outer;
            lines.glowOuterWidth = Mathf.Max(innerWidth + .05f, outerWidth);
            lines.glowOuterXRay = xray;
        }
        Apply(lines);
    }

    /// <summary>The hulls are children of the part's renderers: when a part's mesh changes, throw them away.</summary>
    public static void Forget(GameObject target)
    {
        if (target == null || !targets.TryGetValue(target, out Lines lines)) return;
        foreach (MeshRenderer hull in lines.hulls) if (hull != null) Object.Destroy(hull.gameObject);
        foreach (MeshRenderer hull in lines.outerHulls) if (hull != null) Object.Destroy(hull.gameObject);
        targets.Remove(target);
    }

    // ------------------------------------------------------------------ drawing

    static Lines For(GameObject target, bool create)
    {
        if (target == null) return null;
        if (targets.TryGetValue(target, out Lines lines)) return lines;
        if (!create) return null;
        Tidy();
        lines = Build(target);
        targets[target] = lines;
        return lines;
    }

    static void Apply(Lines lines)
    {
        bool on = lines.pointer || lines.glow;
        Color colour = lines.pointer ? lines.pointerColour : lines.glowColour;
        float width = lines.pointer ? lines.pointerWidth : lines.glowWidth;
        bool xray = lines.pointer ? lines.pointerXRay : lines.glowXRay;
        bool changed = on != lines.shownOn || on && (colour != lines.shownColour || !Mathf.Approximately(width, lines.shownWidth) || xray != lines.shownXRay);
        lines.shownOn = on;
        lines.shownColour = colour;
        lines.shownWidth = width;
        lines.shownXRay = xray;
        // The second line: the guide's only (the pointer's white and gold stay one line), outside the first.
        bool outerOn = on && !lines.pointer && lines.glowOuter;
        bool outerChanged = outerOn != lines.shownOuterOn || outerOn && (lines.glowOuterColour != lines.shownOuterColour
            || !Mathf.Approximately(lines.glowOuterWidth, lines.shownOuterWidth) || lines.glowOuterXRay != lines.shownOuterXRay);
        lines.shownOuterOn = outerOn;
        lines.shownOuterColour = lines.glowOuterColour;
        lines.shownOuterWidth = lines.glowOuterWidth;
        lines.shownOuterXRay = lines.glowOuterXRay;
        if (on)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(ColourId, colour);
            block.SetFloat(WidthId, BaseWidth * Mathf.Max(.1f, width));
            block.SetFloat(XRayId, xray ? 1f : 0f);
        }
        if (outerOn)
        {
            if (outerBlock == null) outerBlock = new MaterialPropertyBlock();
            outerBlock.Clear();
            outerBlock.SetColor(ColourId, lines.glowOuterColour);
            outerBlock.SetFloat(WidthId, BaseWidth * Mathf.Max(.15f, lines.glowOuterWidth));
            outerBlock.SetFloat(XRayId, lines.glowOuterXRay ? 1f : 0f);
        }
        for (int i = 0; i < lines.hulls.Count; i++)
        {
            MeshRenderer hull = lines.hulls[i];
            Renderer source = lines.sources[i];
            // A part's mesh switched off (a fault's other parts, a carried device's hidden pieces) has no line of its own.
            bool sourceShown = source != null && source.enabled && source.gameObject.activeInHierarchy;
            if (hull != null)
            {
                bool shown = on && sourceShown;
                bool wasHidden = hull.forceRenderingOff;
                if (wasHidden == shown) hull.forceRenderingOff = !shown;
                // A hull coming back (its part shown again) gets the look too, not the last one it had.
                if (shown && (changed || wasHidden)) hull.SetPropertyBlock(block);
            }
            MeshRenderer outer = i < lines.outerHulls.Count ? lines.outerHulls[i] : null;
            if (outer != null)
            {
                bool shown = outerOn && sourceShown;
                bool wasHidden = outer.forceRenderingOff;
                if (wasHidden == shown) outer.forceRenderingOff = !shown;
                if (shown && (outerChanged || wasHidden)) outer.SetPropertyBlock(outerBlock);
            }
        }
    }

    static Lines Build(GameObject target)
    {
        var lines = new Lines();
        found.Clear();
        // Inactive pieces too: one shown later (a fresh part presented on its pad) is outlined from the hulls made now.
        target.GetComponentsInChildren(true, found);
        foreach (Renderer source in found)
        {
            if (source == null || source.GetComponent<BenchOutlineHull>() != null) continue;
            // Not a job tag nor a text (DeviceBounds' rule): the whole device's glow outlines the device, not its tag.
            if (source.GetComponentInParent<JobMarker>(true) != null || source.GetComponent<TMPro.TMP_Text>() != null) continue;
            // The brush and the held cloth have keys since 14 Oct: Unity draws them with a SkinnedMeshRenderer and no
            // MeshFilter (the imports builder's 5.1). Only mesh renderers get a hull, never a sprite, a line or particles.
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else if (source is MeshRenderer) { MeshFilter filter = source.GetComponent<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
            if (mesh == null) continue;
            // Two hulls a renderer: the line, and the two-tone guide's wider line outside it (hidden until a two-tone glow asks).
            lines.hulls.Add(Hull("Outline (bench)", source, mesh));
            lines.outerHulls.Add(Hull("Outline (bench, outer)", source, mesh));
            lines.sources.Add(source);
        }
        found.Clear();
        return lines;
    }

    // One hull renderer beside a source renderer: its smoothed mesh in the outline's material, hidden until it is asked for.
    static MeshRenderer Hull(string name, Renderer source, Mesh mesh)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<BenchOutlineHull>();
        go.AddComponent<MeshFilter>().sharedMesh = Smoothed(mesh);
        var renderer = go.AddComponent<MeshRenderer>();
        int subMeshes = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[subMeshes];
        for (int i = 0; i < subMeshes; i++) materials[i] = Material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        // Enabled for good; shown and hidden by forceRenderingOff (the header says why).
        renderer.forceRenderingOff = true;
        renderer.enabled = true;
        return renderer;
    }

    static Material Material
    {
        get
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Fixit Fidget/Bench outline");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");   // a wrong but visible fallback
            material = new Material(shader) { name = "Bench outline (runtime)", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor(ColourId, Hover);
            return material;
        }
    }

    // A copy of the mesh with one normal per position (the average of the faces that meet there), so the hull stays
    // closed on a flat-shaded part. Shared across parts that share a mesh.
    static Mesh Smoothed(Mesh source)
    {
        if (smoothed.TryGetValue(source, out Mesh ready) && ready != null) return ready;
        Mesh copy;
        if (!source.isReadable)
        {
            copy = source;      // can't read it (an imported mesh without Read/Write): the hull uses the mesh as it is
        }
        else
        {
            copy = Object.Instantiate(source);
            copy.name = source.name + " (outline)";
            copy.hideFlags = HideFlags.HideAndDontSave;
            Vector3[] vertices = copy.vertices;
            Vector3[] normals = copy.normals;
            if (normals == null || normals.Length != vertices.Length) { copy.RecalculateNormals(); normals = copy.normals; }
            var sums = new Dictionary<Vector3, Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 key = Key(vertices[i]);
                sums[key] = sums.TryGetValue(key, out Vector3 n) ? n + normals[i] : normals[i];
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = sums[Key(vertices[i])];
                normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : normals[i];
            }
            copy.normals = normals;
        }
        smoothed[source] = copy;
        return copy;
    }

    static Vector3 Key(Vector3 v) => new Vector3(Mathf.Round(v.x * 1e5f), Mathf.Round(v.y * 1e5f), Mathf.Round(v.z * 1e5f));

    static void Tidy()
    {
        gone.Clear();
        foreach (var pair in targets) if (pair.Key == null) gone.Add(pair.Key);
        foreach (GameObject g in gone) targets.Remove(g);
        gone.Clear();
    }
}

/// <summary>Marks a hull child so the outline never outlines its own outline.</summary>
public sealed class BenchOutlineHull : MonoBehaviour { }
