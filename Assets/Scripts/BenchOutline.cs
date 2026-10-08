using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE OUTLINE ROUND THE PART UNDER THE CURSOR (the bench, v2; claude/bench-spec-v2.md §2.1)
//
// (BenchOutline, not Outline: UnityEngine.UI has an Outline of its own, and the recap phone uses it.)
//
// BenchOutline.Set(part, true) draws a white line round every mesh under `part` (a screw, a cover, a tool in the caddy):
// each renderer gets a hull child drawn with "Fixit Fidget/Bench outline" (back faces, pushed out along the normals).
// The hull uses a copy of the mesh whose normals are smoothed across shared positions, because the parts are flat
// shaded (split normals) and a hull pushed along split normals opens at every edge. Hulls are kept and switched, not
// rebuilt, so hovering costs nothing after the first time. It replaces the old highlight, which tinted one renderer's
// material (fine for a cylinder, wrong for a model with four materials).
// ---------------------------------------------------------------------------
public static class BenchOutline
{
    public static readonly Color Hover = Color.white;
    public static readonly Color Busy = new Color(1f, .82f, .35f);       // a hold under way

    static Material material;
    static readonly Dictionary<Mesh, Mesh> smoothed = new Dictionary<Mesh, Mesh>();
    static readonly Dictionary<GameObject, List<MeshRenderer>> hulls = new Dictionary<GameObject, List<MeshRenderer>>();
    static readonly List<GameObject> gone = new List<GameObject>();

    /// <summary>Draws the outline round everything under <paramref name="target"/>, or takes it away.</summary>
    public static void Set(GameObject target, bool on) => Set(target, on, Hover);

    public static void Set(GameObject target, bool on, Color colour)
    {
        if (target == null) return;
        if (!hulls.TryGetValue(target, out List<MeshRenderer> list))
        {
            if (!on) return;
            list = Build(target);
            hulls[target] = list;
        }
        MaterialPropertyBlock block = null;
        foreach (MeshRenderer hull in list)
        {
            if (hull == null) continue;
            hull.enabled = on;
            if (!on) continue;
            if (block == null) { block = new MaterialPropertyBlock(); block.SetColor("_Color", colour); }
            hull.SetPropertyBlock(block);
        }
        Tidy();
    }

    /// <summary>The hulls are children of the part's renderers: when a part's mesh changes, throw them away.</summary>
    public static void Forget(GameObject target)
    {
        if (target == null || !hulls.TryGetValue(target, out List<MeshRenderer> list)) return;
        foreach (MeshRenderer hull in list) if (hull != null) Object.Destroy(hull.gameObject);
        hulls.Remove(target);
    }

    static List<MeshRenderer> Build(GameObject target)
    {
        var list = new List<MeshRenderer>();
        foreach (MeshRenderer source in target.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (source.GetComponent<BenchOutlineHull>() != null) continue;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var go = new GameObject("Outline (hover)");
            go.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<BenchOutlineHull>();
            go.AddComponent<MeshFilter>().sharedMesh = Smoothed(filter.sharedMesh);
            var renderer = go.AddComponent<MeshRenderer>();
            int subMeshes = Mathf.Max(1, filter.sharedMesh.subMeshCount);
            var materials = new Material[subMeshes];
            for (int i = 0; i < subMeshes; i++) materials[i] = Material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.enabled = false;
            list.Add(renderer);
        }
        return list;
    }

    static Material Material
    {
        get
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Fixit Fidget/Bench outline");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");   // a wrong but visible fallback
            material = new Material(shader) { name = "Bench outline (runtime)", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_Color", Hover);
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
        foreach (var pair in hulls) if (pair.Key == null) gone.Add(pair.Key);
        foreach (GameObject g in gone) hulls.Remove(g);
    }
}

/// <summary>Marks a hull child so the outline never outlines its own outline.</summary>
public sealed class BenchOutlineHull : MonoBehaviour { }
