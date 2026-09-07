using UnityEngine;
using UnityEngine.UI;

// Vector handset: no font glyph, texture, number or world-space sentence.
public sealed class SupportCallIcon : MaskableGraphic
{
    private bool ringing;
    public void Show(HoldCallRun.State phase, float clock)
    {
        bool next = phase == HoldCallRun.State.Ringing;
        if (ringing != next) { ringing = next; SetVerticesDirty(); }
        bool waiting = phase == HoldCallRun.State.OnHold || phase == HoldCallRun.State.Connecting;
        float wave = Mathf.Sin(clock * Mathf.PI * 2 * (ringing ? 2.2f : .55f));
        Color tint = ringing || phase == HoldCallRun.State.Done ? new Color(.24f, 1f, .46f)
            : waiting ? new Color(1f, .65f, .16f) : new Color(.46f, .50f, .51f);
        float brightness = ringing || waiting ? .85f + .15f * wave : 1f;
        color = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, 1f);
        rectTransform.localScale = Vector3.one * (ringing ? 1f + .08f * wave : waiting ? 1f + .035f * wave : 1f);
        rectTransform.localRotation = Quaternion.Euler(0, 0, ringing ? 8f * Mathf.Sin(clock * 42f) : 0f);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Disc(vh, 43, new Color(.035f, .055f, .06f, .94f));
        Arc(vh, Vector2.down * 10, 29, 10, 25, 155, color);
        Pad(vh, new Vector2(-27, 0), 15, 23, 25, color);
        Pad(vh, new Vector2(27, 0), 15, 23, -25, color);
        if (ringing)
        {
            Arc(vh, Vector2.zero, 36, 3.5f, -30, 30, color);
            Arc(vh, Vector2.zero, 36, 3.5f, 150, 210, color);
        }
    }
    private Vector2 Point(Vector2 p)
    {
        Rect r = GetPixelAdjustedRect();
        return r.center + p * (Mathf.Min(r.width, r.height) / 100f);
    }
    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(Point(a), tint, Vector2.zero); vh.AddVert(Point(b), tint, Vector2.zero);
        vh.AddVert(Point(c), tint, Vector2.zero); vh.AddVert(Point(d), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
    private void Disc(VertexHelper vh, float radius, Color tint)
    {
        for (int i = 0; i < 32; i++)
        {
            int start = vh.currentVertCount;
            vh.AddVert(Point(Vector2.zero), tint, Vector2.zero);
            vh.AddVert(Point(Polar(radius, i * 360f / 32)), tint, Vector2.zero);
            vh.AddVert(Point(Polar(radius, (i + 1) * 360f / 32)), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
    private void Arc(VertexHelper vh, Vector2 centre, float radius, float width, float from, float to, Color tint)
    {
        for (int i = 0; i < 16; i++)
        {
            float a = Mathf.Lerp(from, to, i / 16f), b = Mathf.Lerp(from, to, (i + 1) / 16f);
            Quad(vh, centre + Polar(radius - width / 2, a), centre + Polar(radius + width / 2, a),
                centre + Polar(radius + width / 2, b), centre + Polar(radius - width / 2, b), tint);
        }
    }
    private void Pad(VertexHelper vh, Vector2 centre, float width, float height, float angle, Color tint)
    {
        Vector2 x = Polar(width / 2, angle), y = Polar(height / 2, angle + 90);
        Quad(vh, centre - x - y, centre - x + y, centre + x + y, centre + x - y, tint);
    }
    private static Vector2 Polar(float radius, float angle) =>
        new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
}
