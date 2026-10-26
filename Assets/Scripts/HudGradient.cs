using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 26 Oct 2026 (HUD 3): moved out of HudSky.cs into a file of its own name, so Unity knows it as a script.
/// <summary>A vertical gradient of three colours drawn as two quads (the sky).</summary>
public sealed class HudGradient : MaskableGraphic
{
    public Color top = Color.white, middle = Color.white, bottom = Color.white;
    /// <summary>Where the middle colour sits, 0 at the bottom, 1 at the top.</summary>
    public float middleAt = .45f;

    public void Set(Color t, Color m, Color b)
    {
        if (t == top && m == middle && b == bottom) return;
        top = t; middle = m; bottom = b;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float my = Mathf.Lerp(r.yMin, r.yMax, Mathf.Clamp01(middleAt));
        Color32 ct = top, cm = middle, cb = bottom;
        vh.AddVert(new Vector3(r.xMin, r.yMin), cb, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), cb, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, my), cm, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, my), cm, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), ct, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), ct, Vector2.zero);
        vh.AddTriangle(0, 3, 2); vh.AddTriangle(2, 1, 0);
        vh.AddTriangle(3, 5, 4); vh.AddTriangle(4, 2, 3);
    }
}
