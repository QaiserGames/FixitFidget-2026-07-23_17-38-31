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
// ---------------------------------------------------------------------------
public static class BenchOutline
{
    public static readonly Color Hover = Color.white;
    public static readonly Color Busy = new Color(1f, .82f, .35f);       // a hold under way
    /// <summary>The glow on the next thing to touch (checklist v2): the brand's light green, which UiSkin calls "a glow".</summary>
    public static readonly Color Guide = UiSkin.BrandBright;
    /// <summary>"What's left" (Tab / View): PowerWash's yellow, our gold.</summary>
    public static readonly Color Left = UiSkin.Gold;
    /// <summary>The hover line's width, per metre from the camera (the shader's own default; a layer's width is a multiple of it).</summary>
    public const float BaseWidth = .0035f;

    static readonly int ColourId = Shader.PropertyToID("_Color");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int XRayId = Shader.PropertyToID("_XRay");

    // One target's hulls (one per renderer under it, beside the renderer it outlines) and its two layers.
    sealed class Lines
    {
        public readonly List<MeshRenderer> hulls = new List<MeshRenderer>();
        public readonly List<Renderer> sources = new List<Renderer>();
        public bool pointer, glow;
        public Color pointerColour = Hover, glowColour = Guide;
        public float pointerWidth = 1f, glowWidth = 1f;
        public bool pointerXRay, glowXRay;
        // What was last put on the hulls, so an unchanged call costs nothing.
        public bool shownOn;
        public Color shownColour;
        public float shownWidth = -1f;
        public bool shownXRay;
    }

    static Material material;
    static MaterialPropertyBlock block;
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
        if (on)
        {
            lines.glowColour = colour;
            lines.glowWidth = widthScale;
            lines.glowXRay = xray;
        }
        Apply(lines);
    }

    /// <summary>The hulls are children of the part's renderers: when a part's mesh changes, throw them away.</summary>
    public static void Forget(GameObject target)
    {
        if (target == null || !targets.TryGetValue(target, out Lines lines)) return;
        foreach (MeshRenderer hull in lines.hulls) if (hull != null) Object.Destroy(hull.gameObject);
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
        if (on)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(ColourId, colour);
            block.SetFloat(WidthId, BaseWidth * Mathf.Max(.1f, width));
            block.SetFloat(XRayId, xray ? 1f : 0f);
        }
        for (int i = 0; i < lines.hulls.Count; i++)
        {
            MeshRenderer hull = lines.hulls[i];
            if (hull == null) continue;
            Renderer source = lines.sources[i];
            // A part's mesh switched off (a fault's other parts, a carried device's hidden pieces) has no line of its own.
            bool shown = on && source != null && source.enabled && source.gameObject.activeInHierarchy;
            bool wasHidden = hull.forceRenderingOff;
            if (wasHidden == shown) hull.forceRenderingOff = !shown;
            // A hull coming back (its part shown again) gets the look too, not the last one it had.
            if (shown && (changed || wasHidden)) hull.SetPropertyBlock(block);
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
            var go = new GameObject("Outline (bench)");
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
            lines.hulls.Add(renderer);
            lines.sources.Add(source);
        }
        found.Clear();
        return lines;
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
