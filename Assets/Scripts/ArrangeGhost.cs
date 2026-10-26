using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// ARRANGE THE CAFÉ: WHAT IT DRAWS IN THE ROOM (24 Oct 2026; furniture-spec.md §1.3 "The ghost", "Green or red, and why")
//
// Made while playing, never saved, in its own object outside the café (nothing the café's steps or the night look for):
//   * THE GHOST: the piece itself, a see-through copy of its kind's prefab (or of the piece picked up), every renderer in one
//     flat see-through colour: the skin's bright brand green when it fits, a lighter red when it doesn't. The copy is made
//     under a switched-off holder and stripped to its meshes before it ever wakes, so no seat on it registers, nothing on it
//     collides or carves, no light on it shines;
//   * ITS FOOTPRINT on the floor, filled, over a dark under-stroke so it reads on the light floor; a ring with a dot at each of
//     its seats' stand points (where people will stand to sit down);
//   * WHAT'S IN THE WAY when it doesn't fit (the rules' "in the way" box: the aisle, the queue's apron, the chair it would
//     block), lit red and breathing;
//   * the amber outline round a placed piece the cursor points at, and the dashed outline of where a piece in hand came from;
//   * the floor's 25 cm grid over the customer floor, faint, while Arrange is up.
// Its materials are copies of the catalogue's ghost material (URP Lit, transparent, made by Furniture 2), so nothing here
// asks for a shader by name in a build; without it (Furniture 2 not run yet) the editor's URP Lit stands in.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class ArrangeGhost : MonoBehaviour
{
    const float GridY = .010f, UnderY = .012f, FillY = .014f, RingY = .017f, LineY = .019f, WayY = .016f;
    const float Under = .04f, RingSize = .46f, Line = .035f, Dash = .12f, Gap = .08f;
    // The floor's grid: lines every 25 cm, x from the door's middle and z from the shop front, over the customer floor.
    const float GridX0 = -7.5f, GridX1 = 7.5f, GridZ0 = 0f, GridZ1 = 13f;

    static readonly Color Green = new Color(UiSkin.BrandBright.r, UiSkin.BrandBright.g, UiSkin.BrandBright.b, .45f);
    static readonly Color RedTint = new Color(1f, .56f, .50f, .45f);
    static readonly Color FillGreen = new Color(UiSkin.BrandBright.r, UiSkin.BrandBright.g, UiSkin.BrandBright.b, .34f);
    static readonly Color FillRed = new Color(1f, .46f, .40f, .34f);
    static readonly Color UnderColour = new Color(.12f, .10f, .09f, .42f);
    static readonly Color Amber = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, .95f);
    static readonly Color WayColour = new Color(.95f, .26f, .20f, .30f);
    static readonly Color HomeColour = new Color(1f, 1f, 1f, .75f);
    static readonly Color GridColour = new Color(1f, 1f, 1f, .20f);

    Material ghostGreen, ghostRed, fillGreen, fillRed, underMat, ringGreen, ringRed, amberMat, wayMat, homeMat, gridMat;
    Texture2D ringTexture, gridTexture;
    Mesh quad;
    GameObject holder;
    Transform grid, way;
    GameObject copy;
    Object copyOf;
    string copyKey = "";
    MeshRenderer[] copyRenderers = new MeshRenderer[0];
    bool copyRed, shown;
    readonly List<Transform> fills = new List<Transform>(), unders = new List<Transform>(), rings = new List<Transform>();
    readonly List<Transform> outline = new List<Transform>(), home = new List<Transform>();
    Vector3 top;

    /// <summary>Where the plate hangs: over the ghost's (or the pointed piece's) highest point.</summary>
    public Vector3 Top => top;
    public bool GhostShowing => copy != null && copy.activeSelf;
    /// <summary>Red (doesn't fit) or green (labs).</summary>
    public bool GhostRed => copyRed;
    public int StandRings { get; private set; }
    public bool InTheWayShowing => way != null && way.gameObject.activeSelf;
    public bool GridShowing => grid != null && grid.gameObject.activeSelf;

    public static ArrangeGhost Create(Material ghostMaterial)
    {
        var go = new GameObject("Arrange the café (world, runtime)");
        var g = go.AddComponent<ArrangeGhost>();
        g.Build(ghostMaterial);
        return g;
    }

    void Build(Material ghostMaterial)
    {
        Material basis = ghostMaterial != null ? ghostMaterial : Fallback();
        ghostGreen = Tinted(basis, "Ghost (fits)", Green, .5f);
        ghostRed = Tinted(basis, "Ghost (doesn't fit)", RedTint, .5f);
        fillGreen = Tinted(basis, "Footprint (fits)", FillGreen, .6f);
        fillRed = Tinted(basis, "Footprint (doesn't fit)", FillRed, .6f);
        underMat = Tinted(basis, "Footprint under-stroke", UnderColour, 0f);
        amberMat = Tinted(basis, "Pointed piece's outline", Amber, .8f);
        wayMat = Tinted(basis, "In the way", WayColour, .7f);
        homeMat = Tinted(basis, "Where it came from", HomeColour, .6f);
        ringTexture = RingTexture();
        ringGreen = Tinted(basis, "Stand ring (fits)", new Color(Green.r, Green.g, Green.b, .95f), .8f, ringTexture);
        ringRed = Tinted(basis, "Stand ring (doesn't fit)", new Color(RedTint.r, RedTint.g, RedTint.b, .95f), .8f, ringTexture);
        gridTexture = GridTexture();
        gridMat = Tinted(basis, "Floor grid", GridColour, .5f, gridTexture);
        gridMat.SetTextureScale("_BaseMap", new Vector2((GridX1 - GridX0) / FurnitureRules.Grid, (GridZ1 - GridZ0) / FurnitureRules.Grid));
        quad = Quad();
        holder = new GameObject("Ghost holder (asleep)");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        grid = Overlay("Floor grid", gridMat);
        SetQuad(grid, GridX0, GridX1, GridZ0, GridZ1, GridY);
        way = Overlay("In the way", wayMat);
        Show(false);
    }

    /// <summary>Everything up (the grid) or away.</summary>
    public void Show(bool on)
    {
        shown = on;
        if (grid != null) grid.gameObject.SetActive(on);
        if (!on)
        {
            NoGhost();
            Hover(false, default);
            Home(false, default);
        }
    }

    // ------------------------------------------------------------------ the ghost

    /// <summary>
    /// The ghost of <paramref name="source"/> (a kind's prefab, or the piece in hand) at a pose, its footprint and stand rings from
    /// <paramref name="shape"/> (its shape there), green when it <paramref name="fits"/>, with what's in the way lit when not.
    /// </summary>
    public void Ghost(GameObject source, string key, Vector3 pivot, float yaw, FurnitureRules.Shape shape, bool fits, Bounds inTheWay)
    {
        if (!shown || source == null) { NoGhost(); return; }
        if (copy == null || copyOf != source || copyKey != key) MakeCopy(source, key);
        if (copy == null) return;
        copy.transform.SetPositionAndRotation(new Vector3(pivot.x, 0f, pivot.z), Quaternion.Euler(0f, yaw, 0f));
        if (!copy.activeSelf) copy.SetActive(true);
        bool red = !fits;
        if (red != copyRed)
        {
            copyRed = red;
            Paint(red ? ghostRed : ghostGreen);
        }
        Footprint(shape, red);
        top = TopOf(shape, pivot);
        bool showWay = red && inTheWay.size.x > .01f && inTheWay.size.z > .01f;
        way.gameObject.SetActive(showWay);
        if (showWay)
        {
            SetQuad(way, inTheWay.min.x, inTheWay.max.x, inTheWay.min.z, inTheWay.max.z, WayY);
            float breathe = .22f + .12f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f));
            if (wayMat.HasProperty("_BaseColor")) wayMat.SetColor("_BaseColor", new Color(WayColour.r, WayColour.g, WayColour.b, breathe));
        }
    }

    /// <summary>No ghost (nothing in hand).</summary>
    public void NoGhost()
    {
        if (copy != null && copy.activeSelf) copy.SetActive(false);
        Pool(fills, 0, null);
        Pool(unders, 0, null);
        Pool(rings, 0, null);
        StandRings = 0;
        if (way != null) way.gameObject.SetActive(false);
    }

    /// <summary>The amber outline round a placed piece the cursor points at (its solids, where they stand).</summary>
    public void Hover(bool on, FurnitureRules.Shape shape)
    {
        int n = 0;
        if (on && shown && shape.solids != null)
        {
            foreach (Bounds b in shape.solids)
            {
                float x0 = b.min.x - .03f, x1 = b.max.x + .03f, z0 = b.min.z - .03f, z1 = b.max.z + .03f;
                Pool(outline, n + 4, amberMat);
                SetQuad(outline[n++], x0 - Line, x1 + Line, z0 - Line, z0, LineY);
                SetQuad(outline[n++], x0 - Line, x1 + Line, z1, z1 + Line, LineY);
                SetQuad(outline[n++], x0 - Line, x0, z0, z1, LineY);
                SetQuad(outline[n++], x1, x1 + Line, z0, z1, LineY);
            }
            top = TopOf(shape, shape.solids.Count > 0 ? shape.solids[0].center : Vector3.zero);
        }
        Pool(outline, n, amberMat);
    }

    /// <summary>The dashed outline of where the piece in hand came from.</summary>
    public void Home(bool on, FurnitureRules.Shape shape)
    {
        int n = 0;
        if (on && shown && shape.solids != null)
            foreach (Bounds b in shape.solids)
            {
                n = Dashes(n, b.min.x, b.max.x, b.min.z, true);
                n = Dashes(n, b.min.x, b.max.x, b.max.z, true);
                n = Dashes(n, b.min.z, b.max.z, b.min.x, false);
                n = Dashes(n, b.min.z, b.max.z, b.max.x, false);
            }
        Pool(home, n, homeMat);
    }

    int Dashes(int n, float from, float to, float at, bool alongX)
    {
        for (float s = from; s < to - .01f; s += Dash + Gap)
        {
            float e = Mathf.Min(to, s + Dash);
            Pool(home, n + 1, homeMat);
            if (alongX) SetQuad(home[n], s, e, at - Line * .5f, at + Line * .5f, LineY);
            else SetQuad(home[n], at - Line * .5f, at + Line * .5f, s, e, LineY);
            n++;
        }
        return n;
    }

    void Footprint(FurnitureRules.Shape shape, bool red)
    {
        int solids = shape.solids != null ? shape.solids.Count : 0, stands = shape.stands != null ? shape.stands.Count : 0;
        Pool(unders, solids, underMat);
        Pool(fills, solids, red ? fillRed : fillGreen);
        for (int i = 0; i < solids; i++)
        {
            Bounds b = shape.solids[i];
            SetQuad(unders[i], b.min.x - Under, b.max.x + Under, b.min.z - Under, b.max.z + Under, UnderY);
            SetQuad(fills[i], b.min.x, b.max.x, b.min.z, b.max.z, FillY);
            fills[i].GetComponent<MeshRenderer>().sharedMaterial = red ? fillRed : fillGreen;
        }
        Pool(rings, stands, red ? ringRed : ringGreen);
        for (int i = 0; i < stands; i++)
        {
            Vector3 s = shape.stands[i];
            SetQuad(rings[i], s.x - RingSize * .5f, s.x + RingSize * .5f, s.z - RingSize * .5f, s.z + RingSize * .5f, RingY);
            rings[i].GetComponent<MeshRenderer>().sharedMaterial = red ? ringRed : ringGreen;
        }
        StandRings = stands;
    }

    static Vector3 TopOf(FurnitureRules.Shape shape, Vector3 at)
    {
        float y = .9f;
        if (shape.solids != null) foreach (Bounds b in shape.solids) y = Mathf.Max(y, b.max.y);
        return new Vector3(at.x, y + .25f, at.z);
    }

    // The copy: made asleep under the holder, stripped to its meshes, then woken in the room as the ghost.
    void MakeCopy(GameObject source, string key)
    {
        if (copy != null) Destroy(copy);
        copy = Instantiate(source, holder.transform);
        copy.name = "Ghost - " + source.name;
        copy.SetActive(true);                          // still asleep: the holder is off
        Strip(copy);
        copy.transform.SetParent(transform, false);    // awake now, with nothing on it but its meshes
        copyOf = source;
        copyKey = key ?? "";
        copyRenderers = copy.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer r in copyRenderers)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        copyRed = false;
        Paint(ghostGreen);
    }

    void Paint(Material m)
    {
        foreach (MeshRenderer r in copyRenderers)
        {
            if (r == null) continue;
            int n = Mathf.Max(1, r.sharedMaterials.Length);
            var mats = new Material[n];
            for (int i = 0; i < n; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    // Everything but its meshes, later components first (one that needs another was added after it).
    static void Strip(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            Component[] all = t.GetComponents<Component>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Component c = all[i];
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                DestroyImmediate(c);
            }
        }
    }

    // ------------------------------------------------------------------ floor quads

    Transform Overlay(string name, Material m)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = quad;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    // Exactly n of a pool on (made as needed), the rest off.
    void Pool(List<Transform> pool, int n, Material m)
    {
        while (pool.Count < n) pool.Add(Overlay("Mark", m));
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < n;
            if (pool[i].gameObject.activeSelf != on) pool[i].gameObject.SetActive(on);
            if (on && m != null)
            {
                var r = pool[i].GetComponent<MeshRenderer>();
                if (r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }
    }

    static void SetQuad(Transform t, float x0, float x1, float z0, float z1, float y)
    {
        t.SetPositionAndRotation(new Vector3((x0 + x1) * .5f, y, (z0 + z1) * .5f), Quaternion.identity);
        t.localScale = new Vector3(Mathf.Max(.001f, x1 - x0), 1f, Mathf.Max(.001f, z1 - z0));
    }

    // ------------------------------------------------------------------ made once

    static Mesh Quad()
    {
        var m = new Mesh { name = "Arrange - floor quad", hideFlags = HideFlags.DontSave };
        m.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    static Material Tinted(Material basis, string name, Color colour, float glow, Texture texture = null)
    {
        var m = new Material(basis) { name = "Arrange - " + name, hideFlags = HideFlags.DontSave };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
        if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
        if (glow > 0f && m.HasProperty("_EmissionColor"))
        {
            // A little light of its own, so it reads in the evening's light as by day.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(colour.r, colour.g, colour.b) * glow);
            if (texture != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", texture);
        }
        m.renderQueue = (int)RenderQueue.Transparent + 10;
        return m;
    }

    // Without the catalogue's material (Furniture 2 not run yet): URP Lit made see-through, as Furniture 2 makes it.
    static Material Fallback()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(lit != null ? lit : Shader.Find("Sprites/Default")) { name = "Arrange - ghost (fallback)", hideFlags = HideFlags.DontSave };
        void Set(string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        Set("_Surface", 1f);
        Set("_Blend", 0f);
        Set("_SrcBlend", (float)BlendMode.SrcAlpha);
        Set("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        Set("_SrcBlendAlpha", (float)BlendMode.One);
        Set("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        Set("_ZWrite", 0f);
        Set("_AlphaClip", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    // A ring with a dot in its middle: where a seat's person stands.
    static Texture2D RingTexture()
    {
        const int size = 64;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Arrange - stand ring", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        float c = size * .5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(c, c)) / c;
                float ring = Mathf.Clamp01((.97f - d) * 24f) * Mathf.Clamp01((d - .80f) * 24f);
                float dot = Mathf.Clamp01((.22f - d) * 24f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Max(ring, dot) * 255f));
            }
        t.SetPixels32(px);
        t.Apply(true, true);
        return t;
    }

    // One grid cell: a line along its west and south edges (tiled, they make the 25 cm grid).
    static Texture2D GridTexture()
    {
        const int size = 32;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Arrange - floor grid", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool line = x < 2 || y < 2;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(line ? 255 : 0));
            }
        t.SetPixels32(px);
        t.Apply(true, true);
        return t;
    }

    void OnDestroy()
    {
        foreach (Material m in new[] { ghostGreen, ghostRed, fillGreen, fillRed, underMat, ringGreen, ringRed, amberMat, wayMat, homeMat, gridMat })
            if (m != null) Destroy(m);
        if (ringTexture != null) Destroy(ringTexture);
        if (gridTexture != null) Destroy(gridTexture);
        if (quad != null) Destroy(quad);
    }
}
