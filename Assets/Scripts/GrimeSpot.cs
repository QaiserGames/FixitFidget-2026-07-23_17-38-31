using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// GRIME, AND SMUDGES ON GLASS (the bench, v2, 4b, 9 Oct 2026; claude/bench-spec-v2.md §2.4)
//
// Dirt is A LAYER ON THE SURFACE. This object is a thin patch lying on the part (a disc the size the prefab gave the
// spot, following a lens's dome where it says so) carrying its own little dirt mask, drawn in code: a blob of dust with
// its grain and flecks, or two or three fingerprints and a smear. The brush takes the dirt off WHERE THE BRISTLES
// TOUCH: each frame of a scrub erases the mask along the cursor's path under the tip, with a puff of dust where dirt
// came off. Nothing happens over clean texels, so the player turns the part to find the rest, as in ReStory. A spot
// counts as clean at 97% (the last fleck can't hold the grade hostage): a sparkle, the chime, and it is gone (RepairJob
// counts the spots that remain).
//
// A SMUDGE (Kind.Smudge) is the same thing for grease on glass (a phone's screen, a watch's crystal, a lens): the CLOTH
// wipes it, with a squeak while grease comes off and a gleam across the glass when it is clean. The spec's call: a
// paint mask on the part's material. The hero devices have no UVs or textures (flat colour per face, by design), so
// the mask lives on this patch instead of the part; from the bench it is the same thing, and nothing about the
// devices' materials had to change.
//
// Scrub(float) is the old whole-spot hand (the labs, the Day 1 guide, the editor's checks): it takes amount/health of
// the dirt off evenly, everywhere at once.
// ---------------------------------------------------------------------------
public class GrimeSpot : MonoBehaviour
{
    public enum Kind { Dirt, Smudge }

    [SerializeField] private Kind kind = Kind.Dirt;
    [Tooltip("The old whole-spot health, kept for the prefabs and the labs' hand: Scrub(amount) takes amount/health of the dirt off evenly.")]
    [SerializeField] private float scrubHealth = 100f;
    [Tooltip("A curved surface under the patch (a lens): the sphere's radius in the parent's units; 0 = flat.")]
    [SerializeField, Min(0f)] private float domeRadius = 0f;
    [Tooltip("The share of the dirt that has to come off before the spot counts as clean (the last fleck can't hold the grade hostage).")]
    [SerializeField, Range(.8f, 1f)] private float cleanAt = .97f;
    [Tooltip("0: drawn from the object's name, so a spot looks the same every time the device is spawned.")]
    [SerializeField] private int seed = 0;

    /// <summary>Pixels of cursor travel (after the inspector's scrub power, 0.5 in the scene) that make one full-strength pass
    /// of the tool under its tip: a brush moved at an ordinary pace (10 px a frame) clears the dirt under its middle in one
    /// pass, the edge of its sweep in two; the pad scrubs at a steady rate as well, so a tip held still on a stick clears
    /// what is under it in about a quarter of a second.</summary>
    const float StrokePixels = 6f;
    /// <summary>Half the width the bristles sweep clean, and the cloth's pad, in the world (the tools are real-sized whatever the device's scale).</summary>
    const float BrushRadius = .0048f, ClothRadius = .0095f;
    const int Texels = 64;
    const float Lift = .0003f;   // the patch sits this far off the surface, so it never fights the surface for the pixel

    public Kind What => kind;
    public ToolType Tool => kind == Kind.Smudge ? ToolType.Cloth : ToolType.Brush;
    public string DisplayName => kind == Kind.Smudge ? "Smudge" : "Grime";
    /// <summary>0 dirty .. 1 clean: the share of the dirt that has come off.</summary>
    public float Progress => total > 0f ? Mathf.Clamp01(cleared / total) : 0f;
    public bool IsClean => done;
    public float Health => scrubHealth;

    /// <summary>What the hover says with this tool in hand.</summary>
    public string Prompt(ToolType inHand) => inHand == Tool
        ? (kind == Kind.Smudge ? "Hold to wipe" : "Hold to scrub") + (Progress > .005f ? $" ({Progress:P0})" : "")
        : (kind == Kind.Smudge ? "Needs the cloth" : "Needs the brush");

    // For the labs: how many times dust came off under the tool, and the last point it did.
    public int Strokes { get; private set; }
    public Vector3 LastTouch { get; private set; }

    Texture2D mask;
    Color32[] pixels;
    float[] alpha, start;  // the mask, as floats (the texture is its upload), and as it was painted
    float total, cleared;  // dirt there was, dirt gone
    bool done, dirty, awoke;
    Material material;
    Mesh patch;
    float surfaceY, facing;       // in the object's unit space: where the patch lies, and which way it faces (+1 local up, -1 local down)
    float lastPuffAt, lastSqueakAt;
    Vector3 lastWorld;
    bool hasLast;

    private void Awake()
    {
        if (awoke) return;
        awoke = true;
        LayThePatch();
        Paint();
    }

    private void OnEnable() { if (!awoke) Awake(); }

    // ---------- the patch: a disc on the surface, facing out, the size the prefab gave the spot ----------

    // The prefab authored each spot as a flat sphere or cube lying ON its surface: the box face toward the device's middle
    // is where the surface is, and the dirt shows on the other side. The disc goes on that face, a hair off it; a dome's
    // radius bends it down toward the edge (a lens). The collider becomes a thin slab over the disc, on the visible side.
    void LayThePatch()
    {
        JobBase job = GetComponentInParent<JobBase>();
        Vector3 centre = job != null ? job.transform.position : transform.parent != null ? transform.parent.position : transform.position - transform.up * .01f;
        float toward = Vector3.Dot(centre - transform.position, transform.up);
        surfaceY = toward >= 0f ? .5f : -.5f;    // the box face toward the device
        facing = -Mathf.Sign(surfaceY);          // the dirt shows away from it
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(1e-5f, Mathf.Abs(s.x)), sy = Mathf.Max(1e-5f, Mathf.Abs(s.y)), sz = Mathf.Max(1e-5f, Mathf.Abs(s.z));
        float lift = Lift / sy;
        // a lens's dome: the drop below the apex at a world distance d from the centre is R - sqrt(R^2 - d^2)
        float R = domeRadius > 0f && transform.parent != null ? domeRadius * Mathf.Max(1e-5f, Mathf.Abs(transform.parent.lossyScale.y)) : 0f;   // the parent's units into metres

        const int rings = 4, segments = 36;
        var verts = new Vector3[1 + rings * segments];
        var uvs = new Vector2[verts.Length];
        var norms = new Vector3[verts.Length];
        Vector3 n = Vector3.up * facing;
        verts[0] = new Vector3(0f, surfaceY + facing * lift, 0f);
        uvs[0] = new Vector2(.5f, .5f);
        norms[0] = n;
        for (int r = 1; r <= rings; r++)
        {
            float u = .5f * r / rings;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * u, z = Mathf.Sin(a) * u;
                float drop = 0f;
                if (R > 0f)
                {
                    float d = Mathf.Sqrt(x * sx * x * sx + z * sz * z * sz);
                    drop = (R - Mathf.Sqrt(Mathf.Max(0f, R * R - d * d))) / sy;
                }
                int k = 1 + (r - 1) * segments + i;
                verts[k] = new Vector3(x, surfaceY + facing * (lift - drop), z);
                uvs[k] = new Vector2(x + .5f, z + .5f);
                norms[k] = n;
            }
        }
        var tris = new System.Collections.Generic.List<int>(rings * segments * 6);
        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;
            Wind(tris, 0, 1 + i, 1 + j);
        }
        for (int r = 1; r < rings; r++)
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int a = 1 + (r - 1) * segments + i, b = 1 + (r - 1) * segments + j, c = 1 + r * segments + i, d = 1 + r * segments + j;
                Wind(tris, a, c, d);
                Wind(tris, a, d, b);
            }
        patch = new Mesh { name = name + " (dirt)", hideFlags = HideFlags.HideAndDontSave };
        patch.vertices = verts;
        patch.uv = uvs;
        patch.normals = norms;
        patch.triangles = tris.ToArray();
        patch.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = patch;
        var mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        material = NewMaterial();
        mr.sharedMaterial = material;

        // The hover's collider: a thin slab over the patch, on the visible side (the old box filled the whole authored
        // thickness, so the tool's tip rode millimetres above the dirt).
        var box = GetComponent<BoxCollider>();
        if (box == null)
        {
            foreach (Collider old in GetComponents<Collider>()) Remove(old);
            box = gameObject.AddComponent<BoxCollider>();
        }
        float slab = Mathf.Min(.3f, Mathf.Max(.08f, .0012f / sy));   // 1.2 mm, and never thicker than the authored box's near third
        box.center = new Vector3(0f, surfaceY + facing * slab * .5f, 0f);
        box.size = new Vector3(1f, slab, 1f);
    }

    // Faces wound to face `facing` (local up for +1).
    void Wind(System.Collections.Generic.List<int> tris, int a, int b, int c)
    {
        if (facing > 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
        else { tris.Add(a); tris.Add(b); tris.Add(c); }
    }

    // A transparent lit surface of its own (its texture is the mask; dust is matte, grease has a sheen).
    Material NewMaterial()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(lit) { name = name + " (dirt, runtime)", hideFlags = HideFlags.HideAndDontSave };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("SHADOWCASTER", false);
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", kind == Kind.Smudge ? .82f : .05f);
        m.SetFloat("_Metallic", 0f);
        return m;
    }

    // ---------- the mask, drawn in code ----------

    void Paint()
    {
        mask = new Texture2D(Texels, Texels, TextureFormat.RGBA32, false)
        {
            name = name + " (mask)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
        };
        pixels = new Color32[Texels * Texels];
        alpha = new float[Texels * Texels];
        var rng = new System.Random(seed != 0 ? seed : name.GetHashCode());
        if (kind == Kind.Smudge) PaintSmudge(rng); else PaintDirt(rng);
        // the faintest texels (the blob's feathered edge, under 5%) don't count: they can't be seen, so they must never be
        // what stands between the player and "clean"
        for (int i = 0; i < alpha.Length; i++) if (alpha[i] < .05f) alpha[i] = 0f;
        start = (float[])alpha.Clone();
        total = 0f;
        for (int i = 0; i < alpha.Length; i++) total += alpha[i];
        cleared = 0f;
        Upload();
        if (material != null) material.SetTexture("_BaseMap", mask);
    }

    // Dust: a blob with an irregular edge, thicker and darker in its middle, with grain and a scatter of flecks.
    void PaintDirt(System.Random rng)
    {
        float p1 = Rnd(rng) * 6.28f, p2 = Rnd(rng) * 6.28f, p3 = Rnd(rng) * 6.28f;
        float ox = Rnd(rng) * 100f, oy = Rnd(rng) * 100f, ox2 = Rnd(rng) * 100f, oy2 = Rnd(rng) * 100f;
        Color thin = new Color(.50f, .45f, .38f), thick = new Color(.30f, .26f, .21f);
        for (int y = 0; y < Texels; y++)
            for (int x = 0; x < Texels; x++)
            {
                float u = (x + .5f) / Texels - .5f, v = (y + .5f) / Texels - .5f;
                float r = Mathf.Sqrt(u * u + v * v), t = Mathf.Atan2(v, u);
                float edge = .44f + .035f * Mathf.Sin(2f * t + p1) + .03f * Mathf.Sin(3f * t + p2) + .02f * Mathf.Sin(5f * t + p3);
                float inside = Smooth(edge, edge - .14f, r);                      // 1 well inside, 0 at the edge
                float n = Mathf.PerlinNoise(u * 5.5f + ox, v * 5.5f + oy) * .6f + Mathf.PerlinNoise(u * 13f + ox2, v * 13f + oy2) * .4f;
                float grain = Smooth(.3f, .72f, n);
                float a = inside * (.35f + .65f * grain) * .9f;
                Color c = Color.Lerp(thin, thick, grain * inside);
                Set(x, y, c, a);
            }
        // flecks: little darker specks, some outside the blob's thinning edge
        int flecks = 26 + (int)(Rnd(rng) * 10);
        for (int i = 0; i < flecks; i++)
        {
            float cx = (Rnd(rng) - .5f) * .92f, cy = (Rnd(rng) - .5f) * .92f;
            if (cx * cx + cy * cy > .22f) continue;
            float fr = .012f + Rnd(rng) * .022f;
            Blot(cx, cy, fr, new Color(.26f, .22f, .18f), .85f);
        }
    }

    // Grease: two or three fingerprints (ovals with their ridges) and a smear, pale, catching the light.
    void PaintSmudge(System.Random rng)
    {
        Color grease = new Color(.87f, .89f, .91f);
        int prints = 2 + (Rnd(rng) < .5f ? 1 : 0);
        for (int i = 0; i < prints; i++)
        {
            float cx = (Rnd(rng) - .5f) * .5f, cy = (Rnd(rng) - .5f) * .5f;
            float rot = Rnd(rng) * Mathf.PI, ridge = Rnd(rng) * 6.28f;
            float ra = .13f + Rnd(rng) * .05f, rb = ra * (1.25f + Rnd(rng) * .25f);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int y = 0; y < Texels; y++)
                for (int x = 0; x < Texels; x++)
                {
                    float u = (x + .5f) / Texels - .5f - cx, v = (y + .5f) / Texels - .5f - cy;
                    float ex = (u * cs + v * sn) / ra, ey = (-u * sn + v * cs) / rb;
                    float d = Mathf.Sqrt(ex * ex + ey * ey);
                    if (d > 1.05f) continue;
                    float inside = Smooth(1.02f, .82f, d);
                    float ridges = .55f + .45f * Mathf.Sin(d * 42f + ridge);       // the print's lines
                    float a = inside * ridges * .5f;
                    Add(x, y, grease, a);
                }
        }
        // the smear: a thick bent stroke across
        float sx0 = -.38f + Rnd(rng) * .1f, sy0 = (Rnd(rng) - .5f) * .4f, sx1 = .36f - Rnd(rng) * .1f, sy1 = (Rnd(rng) - .5f) * .4f, sag = (Rnd(rng) - .5f) * .3f;
        for (int y = 0; y < Texels; y++)
            for (int x = 0; x < Texels; x++)
            {
                float u = (x + .5f) / Texels - .5f, v = (y + .5f) / Texels - .5f;
                float k = Mathf.Clamp01((u - sx0) / Mathf.Max(1e-4f, sx1 - sx0));
                float cy = Mathf.Lerp(sy0, sy1, k) + sag * 4f * k * (1f - k);
                float d = Mathf.Abs(v - cy);
                float along = Smooth(0f, .08f, k) * Smooth(1f, .92f, k);
                float a = Smooth(.075f, .03f, d) * along * .34f;
                if (a > .003f) Add(x, y, grease, a);
            }
    }

    void Blot(float cx, float cy, float r, Color c, float strength)
    {
        int x0 = Mathf.Max(0, (int)((cx - r + .5f) * Texels) - 1), x1 = Mathf.Min(Texels - 1, (int)((cx + r + .5f) * Texels) + 1);
        int y0 = Mathf.Max(0, (int)((cy - r + .5f) * Texels) - 1), y1 = Mathf.Min(Texels - 1, (int)((cy + r + .5f) * Texels) + 1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float u = (x + .5f) / Texels - .5f - cx, v = (y + .5f) / Texels - .5f - cy;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Smooth(r, r * .4f, d) * strength;
                if (a > .003f) Add(x, y, c, a);
            }
    }

    void Set(int x, int y, Color c, float a)
    {
        int i = y * Texels + x;
        alpha[i] = Mathf.Clamp01(a);
        pixels[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
    }

    void Add(int x, int y, Color c, float a)
    {
        int i = y * Texels + x;
        float before = alpha[i];
        float after = Mathf.Clamp01(before + a * (1f - before));
        if (after <= 0f) return;
        // the colour mixes toward the new layer by how much it adds
        float w = before > 0f ? a * (1f - before) / after : 1f;
        Color32 p = pixels[i];
        pixels[i] = new Color32((byte)Mathf.Lerp(p.r, c.r * 255f, w), (byte)Mathf.Lerp(p.g, c.g * 255f, w), (byte)Mathf.Lerp(p.b, c.b * 255f, w), 255);
        alpha[i] = after;
    }

    static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    static float Rnd(System.Random rng) => (float)rng.NextDouble();

    void Upload()
    {
        for (int i = 0; i < alpha.Length; i++) pixels[i].a = (byte)(Mathf.Clamp01(alpha[i]) * 255f);
        mask.SetPixels32(pixels);
        mask.Apply(false, false);
        dirty = false;
    }

    private void LateUpdate()
    {
        if (dirty) Upload();
    }

    // ---------- scrubbing where the tool touches ----------

    /// <summary>
    /// One frame of the tool held and moving on the spot: the dirt under the tip's path from the last frame's point to
    /// <paramref name="at"/> comes off, by <paramref name="travelPixels"/> of cursor travel (a full-strength pass every
    /// StrokePixels). True when dirt actually came off this frame (the caller sounds and animates on that).
    /// </summary>
    public bool Scrub(Vector3 at, float travelPixels)
    {
        if (!awoke) Awake();
        if (done || total <= 0f) return false;
        float strength = Mathf.Clamp01(travelPixels / StrokePixels);
        if (strength <= 0f) { lastWorld = at; hasLast = true; return false; }
        Vector3 from = hasLast && (lastWorld - at).sqrMagnitude < .0025f ? lastWorld : at;   // a jump across the bench is not a stroke
        // The same dirt off for the same path at any frame rate: a long frame's path is taken in pieces of half a pass at
        // most, each with its own share, as a fast machine takes it frame by frame. One long frame used to be one pass,
        // clamped, so at a low frame rate the edges of the bristles' sweep kept their dirt (9 Oct, session 6: the Mac's
        // Unity in the background left a spot at 96 %, where at full speed the same strokes cleared it).
        float radius = kind == Kind.Smudge ? ClothRadius : BrushRadius;
        int pieces = from == at ? 1 : Mathf.Clamp(Mathf.CeilToInt(travelPixels / (StrokePixels * .5f)), 1, 24);
        float share = pieces == 1 ? strength : Mathf.Clamp01(travelPixels / pieces / StrokePixels);
        float removed = 0f;
        for (int k = 0; k < pieces; k++)
            removed += Erase(Vector3.Lerp(from, at, k / (float)pieces), Vector3.Lerp(from, at, (k + 1) / (float)pieces), radius, share);
        lastWorld = at;
        hasLast = true;
        if (removed <= 0f) return false;
        cleared += removed;
        dirty = true;
        bool came = removed > total * .0012f;
        if (came)
        {
            Strokes++;
            LastTouch = at;
            float now = Time.time;
            if (kind == Kind.Dirt)
            {
                if (now - lastPuffAt > .09f) { lastPuffAt = now; Juice.Dust(at); Sfx.Play("grime.scrub", at); }
            }
            else
            {
                if (now - lastPuffAt > .1f) { lastPuffAt = now; Sfx.Play("cloth.wipe", at); }
                if (now - lastSqueakAt > .42f) { lastSqueakAt = now; Sfx.Play("cloth.squeak", at, .8f); }
            }
        }
        if (Progress >= cleanAt) Clean(false);
        return came;
    }

    /// <summary>The stroke is over (the button let go, or the cursor left the spot): the next press starts a new path.</summary>
    public void EndStroke() => hasLast = false;

    // The dirt along a segment of the tip's path, a soft capsule of `radius` metres round it: alpha goes down by
    // `strength` of what was painted there at the middle, less toward the edge. Of what was PAINTED, not of what is left:
    // a pass takes a fixed share, so two or three passes over the same place clear it, instead of each pass taking a
    // share of the remainder and the last fleck never going (the first lab run: fourteen strokes and the spot still
    // there). Returns the dirt removed (in alpha, summed).
    float Erase(Vector3 fromWorld, Vector3 toWorld, float radius, float strength)
    {
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(1e-5f, Mathf.Abs(s.x)), sz = Mathf.Max(1e-5f, Mathf.Abs(s.z));
        Vector3 a = transform.InverseTransformPoint(fromWorld), b = transform.InverseTransformPoint(toWorld);
        // in metres across the patch, from its middle
        Vector2 A = new Vector2(a.x * sx, a.z * sz), B = new Vector2(b.x * sx, b.z * sz);
        float minX = Mathf.Min(A.x, B.x) - radius, maxX = Mathf.Max(A.x, B.x) + radius;
        float minZ = Mathf.Min(A.y, B.y) - radius, maxZ = Mathf.Max(A.y, B.y) + radius;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX / sx + .5f) * Texels)), x1 = Mathf.Min(Texels - 1, Mathf.CeilToInt((maxX / sx + .5f) * Texels));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((minZ / sz + .5f) * Texels)), y1 = Mathf.Min(Texels - 1, Mathf.CeilToInt((maxZ / sz + .5f) * Texels));
        if (x0 > x1 || y0 > y1) return 0f;
        Vector2 ab = B - A;
        float abLen2 = Mathf.Max(1e-12f, ab.sqrMagnitude);
        float removed = 0f;
        for (int y = y0; y <= y1; y++)
        {
            float pz = ((y + .5f) / Texels - .5f) * sz;
            for (int x = x0; x <= x1; x++)
            {
                int i = y * Texels + x;
                float before = alpha[i];
                if (before <= 0f) continue;
                var p = new Vector2(((x + .5f) / Texels - .5f) * sx, pz);
                float h = Mathf.Clamp01(Vector2.Dot(p - A, ab) / abLen2);
                float d = (p - (A + ab * h)).magnitude;
                if (d >= radius) continue;
                float f = strength * (d < radius * .6f ? 1f : 1f - (d - radius * .6f) / (radius * .4f));
                float after = Mathf.Max(0f, before - start[i] * f);
                if (after < .01f) after = 0f;
                alpha[i] = after;
                removed += before - after;
            }
        }
        return removed;
    }

    /// <summary>What dirt is left, for the labs: how many texels still carry some, and where on the patch (texel box).</summary>
    public string Remaining()
    {
        if (alpha == null) return "unpainted";
        int n = 0, x0 = Texels, x1 = -1, y0 = Texels, y1 = -1;
        float left = 0f;
        for (int y = 0; y < Texels; y++)
            for (int x = 0; x < Texels; x++)
            {
                float a = alpha[y * Texels + x];
                if (a <= .02f) continue;
                n++; left += a;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
        if (n == 0) return "nothing left";
        // a 16 x 16 map of what is left (0-9 tenths of the dirt painted there), top row first
        var map = new System.Text.StringBuilder();
        for (int my = 15; my >= 0; my--)
        {
            map.Append('\n');
            for (int mx = 0; mx < 16; mx++)
            {
                float a = 0f, s0 = 0f;
                for (int y = my * 4; y < my * 4 + 4; y++)
                    for (int x = mx * 4; x < mx * 4 + 4; x++) { a += alpha[y * Texels + x]; s0 += start[y * Texels + x]; }
                map.Append(s0 <= .01f ? '.' : (char)('0' + Mathf.Clamp(Mathf.RoundToInt(9f * a / s0), 0, 9)));
            }
        }
        return $"{n} texels ({left / Mathf.Max(1e-6f, total):P1} of the dirt) in x {x0}..{x1}, y {y0}..{y1} of {Texels}{map}";
    }

    // ---------- the labs' hand: the whole spot at once ----------

    /// <summary>Takes amount/health of the dirt off evenly, everywhere (the old API: the labs, the Day 1 guide, the editor's
    /// checks): ten strokes of health/10 clean a spot, as they always did.</summary>
    public void Scrub(float amount)
    {
        if (!awoke) Awake();
        if (done || total <= 0f) return;
        float f = Mathf.Clamp01(amount / Mathf.Max(1f, scrubHealth));
        if (f <= 0f) return;
        float removed = 0f;
        for (int i = 0; i < alpha.Length; i++)
        {
            float before = alpha[i];
            if (before <= 0f) continue;
            float after = Mathf.Max(0f, before - start[i] * f);
            if (after < .01f) after = 0f;
            alpha[i] = after;
            removed += before - after;
        }
        cleared += removed;
        dirty = true;
        if (Application.isPlaying) Sfx.Play(kind == Kind.Smudge ? "cloth.wipe" : "grime.scrub", transform.position);
        if (Progress >= cleanAt) Clean(true);   // the hand that cleans a whole spot is a lab's or the guide's: gone at once, no fade to wait through
    }

    // ---------- clean ----------

    void Clean(bool instant)
    {
        if (done) return;
        done = true;
        Vector3 at = transform.position;
        if (Application.isPlaying)
        {
            Juice.Sparkle(at);
            if (kind == Kind.Smudge)
            {
                // the gleam sweeps the glass: across the patch and a little beyond, the way the player sees it
                Camera cam = Camera.main;
                Vector3 across = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, transform.up).normalized : transform.right;
                float half = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .9f;
                Juice.Gleam(at - across * half, at + across * half);
                Sfx.Play("cloth.gleam", at);
            }
            else Sfx.Play("grime.clean", at);
            if (instant || !gameObject.activeInHierarchy) Destroy(gameObject);
            else StartCoroutine(FadeAndGo());
        }
        else
        {
            // the editor's checks: gone at once (no frames to fade over)
            DestroyImmediate(gameObject);
        }
    }

    IEnumerator FadeAndGo()
    {
        var box = GetComponent<Collider>();
        if (box != null) box.enabled = false;
        float[] left = (float[])alpha.Clone();
        for (float t = 0f; t < 1f; t += Time.deltaTime / .28f)
        {
            float k = 1f - t;
            for (int i = 0; i < alpha.Length; i++) alpha[i] = left[i] * k;
            Upload();
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Remove(mask);
        Remove(material);
        Remove(patch);
    }

    static void Remove(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

#if UNITY_EDITOR
    /// <summary>The build steps' hand on the private fields.</summary>
    public void Configure(Kind what, float dome = 0f)
    {
        kind = what;
        domeRadius = dome;
    }
#endif
}
