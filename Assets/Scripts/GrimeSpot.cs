using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// GRIME, AND SMUDGES ON GLASS (the bench, v2, 4b, 9 Oct 2026; the forgiving bench, 14 Oct 2026: specs/systems-forgiving-bench.md
// §4.4.4-4.4.5, §4.5)
//
// Dirt is A LAYER ON THE SURFACE. This object is a thin patch lying on the part (a disc the size the prefab gave the spot,
// following a lens's dome where it says so) carrying its own little dirt mask, drawn in code: a blob of dust with its grain
// and flecks, or two or three fingerprints and a smear. The tool takes the dirt off WHERE IT TOUCHES. His playtest 5: "i also
// like how we ARE ALMOST THERE with how the grime and the dust looks and feels like" - the look stays; how it comes off is new.
//
// THE PRESS PASS (§4.4.4). The tool's footprint is a disc of clean radius r sweeping the segment from last frame's contact to
// this frame's. A speck within r of the segment loses start x strength x falloff x (the segment's chord through the speck's
// r-circle / the whole chord): one full pass over a speck takes exactly strength x falloff of what was painted there, at any
// frame rate, speed or zoom. (It used to depend on how many 6 px pieces of cursor travel fitted under the tool, so it changed
// with the screen and the zoom.)
//
// THE WIDTH (§4.4.5). Relative to the spot, not fixed metres (the old 4.8 mm brush cleaned the 2x camera and the 3x watch at a
// half and a third): the brush 0.75 x the spot's radius taking all of the painted dirt a pass at its core, the cloth 1.0 x at
// 80 %, never under 10 px on screen, never over 0.6 of the tool's contact width (Wide Brush widens it). Calibrated on these
// masks: a median of 3 strokes a spot, 4 at the 90th percentile.
//
// SMEAR FIRST (grease): a pass over a speck not yet smeared takes only 0.15 of the normal amount, whitens it toward a haze and
// draws it out along the stroke (alpha moved, not lost, so the grade stays honest); the passes after clear it. The prints turn
// to streaky haze, then go.
//
// THE 90 % FINISHER: past 90 % the spot is armed; it finishes once the footprint's middle has travelled 0.6 of the spot's
// width over it (or a pad has rubbed it in place 0.4 s). At 97 % it finishes at once. Clean: the gleam and its chime on the
// same frame (the feel targets' T14), a sparkle, a rumble pulse, and it goes (RepairJob counts the spots that remain).
//
// WHAT'S LEFT (§4.5, PowerWash's dirt indicator): Flash lights the spot's dirt gold for 1.5 s with its faintest specks raised.
//
// Scrub(float) is the old whole-spot hand (the labs, the Day 1 guide, the editor's checks): it takes amount/health of the dirt
// off evenly, everywhere at once.
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

    const int Texels = 64;
    const float Lift = .0003f;   // the patch sits this far off the surface, so it never fights the surface for the pixel
    /// <summary>Seconds after a smudge comes clean that its gleam sweeps: none now (T14: the gleam and its chime on the frame the
    /// spot is done; the cloth goes home by itself, so nothing covers it).</summary>
    public const float GleamAfter = 0f;
    /// <summary>"What's left"'s gold (UiSkin.Gold, #F2A61F) and its peak (emissive 1.5).</summary>
    static readonly Color FlashGold = new Color(.949f, .651f, .122f);

    public Kind What => kind;
    public ToolType Tool => kind == Kind.Smudge ? ToolType.Cloth : ToolType.Brush;
    public string DisplayName => kind == Kind.Smudge ? "Smudge" : "Grime";
    /// <summary>
    /// What the hover calls it (UI/UX §5.1 row 9 and §5.10 item 7: the part's name only, "Lens"): the part it lies on, read from
    /// its own name with the dirt's word taken out ("Lens grime" → "Lens", "Film-path grime" → "Film path", "Smudge_Crystal" →
    /// "Crystal", "Grime_Extra_Phone_2" → "Phone"); the dirt's word when nothing is left.
    /// </summary>
    public string PartName
    {
        get
        {
            if (partName != null) return partName;
            string words = name.Replace('_', ' ').Replace('-', ' ');
            var kept = new System.Text.StringBuilder();
            foreach (string word in words.Split(' '))
            {
                string w = word.Trim();
                if (w.Length == 0) continue;
                string lower = w.ToLowerInvariant();
                if (lower == "grime" || lower == "smudge" || lower == "smudges" || lower == "dirt" || lower == "dust" || lower == "extra"
                    || lower == "(clone)" || char.IsDigit(w[0])) continue;
                if (kept.Length > 0) kept.Append(' ');
                kept.Append(kept.Length == 0 ? char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant() : lower);
            }
            partName = kept.Length > 0 ? kept.ToString() : DisplayName;
            return partName;
        }
    }
    string partName;
    /// <summary>0 dirty .. 1 clean: the share of the dirt that has come off.</summary>
    public float Progress => total > 0f ? Mathf.Clamp01(cleared / total) : 0f;
    public bool IsClean => done;
    public float Health => scrubHealth;

    /// <summary>What the hover says with this tool in hand: nothing when it's the right tool (UI/UX §5.10 item 7: the hover is
    /// the spot's name), the tool it needs when it isn't.</summary>
    public string Prompt(ToolType inHand) => inHand == Tool ? "" : (kind == Kind.Smudge ? "Needs the cloth" : "Needs the brush");

    // For the labs: how many frames dirt came off under the tool, the passes made over it (a new pass at each contact and each
    // turn of the stroke), and the last point dirt came off.
    public int Strokes { get; private set; }
    public int Passes { get; private set; }
    public Vector3 LastTouch { get; private set; }
    /// <summary>When dirt (or grease) last came off it (Time.time), and whether it is armed for the finisher.</summary>
    public float LastCameOffAt { get; private set; } = -10f;
    public bool Armed => !done && Progress >= BenchRules.ArmAt;
    /// <summary>The spot's radius on the part (metres): the geometric mean of the patch's two scales.</summary>
    public float Radius => BenchRules.SpotRadius(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
    /// <summary>The patch's own up (out of the surface, where the dirt shows), in the world. Which of its two faces that is
    /// is worked out as it wakes (LayThePatch), so a reader that comes first (the checklist built in the editor's checks, before
    /// any Awake) wakes it, as Contact and Pass do: the answer is the same whoever asks first (19 Oct, the watch's crystal).</summary>
    public Vector3 Normal
    {
        get
        {
            if (!awoke) Awake();
            return (transform.up * facing).normalized;
        }
    }
    /// <summary>The patch's middle on the surface, in the world.</summary>
    public Vector3 Centre => awoke ? transform.TransformPoint(new Vector3(0f, surfaceY, 0f)) : transform.position;

    Texture2D mask;
    Color32[] pixels;
    float[] alpha, start, smear;   // the mask, as floats (the texture is its upload), as it was painted, and how smeared (grease)
    float total, cleared;          // dirt there was, dirt gone
    bool done, dirty, awoke;
    Material material;
    Mesh patch;
    float surfaceY, facing;       // in the object's unit space: where the patch lies, and which way it faces (+1 local up, -1 local down)
    float lastPuffAt, lastReportedProgress;
    Vector3 lastWorld;
    Vector2 lastDirection;
    bool hasLast;
    float pathOver, padRub;       // the finisher: the footprint's travel over the spot once armed; a pad's rub in place
    float flashFrom = -10f, flashSeconds;
    bool flashing;

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
        smear = new float[alpha.Length];
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
        // While "what's left" flashes, every speck still there shows at 0.6 at least, so the tiniest is seen.
        for (int i = 0; i < alpha.Length; i++)
        {
            float a = Mathf.Clamp01(alpha[i]);
            if (flashing && a > .02f) a = Mathf.Max(a, .6f);
            pixels[i].a = (byte)(a * 255f);
        }
        mask.SetPixels32(pixels);
        mask.Apply(false, false);
        dirty = false;
    }

    private void LateUpdate()
    {
        if (flashing && material != null)
        {
            float t = Time.unscaledTime - flashFrom;
            material.SetColor("_EmissionColor", FlashGold * FlashLevel(t));
            if (t >= flashSeconds)
            {
                flashing = false;
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
                dirty = true;          // the true mask again
            }
        }
        if (dirty) Upload();
    }

    // ---------- the press pass, where the tool touches (§4.4.4) ----------

    /// <summary>
    /// The clean radius of <paramref name="tool"/> on this spot (metres, §4.4.5): the brush 0.75 x the spot's radius, the cloth
    /// 1.0 x (Wide Brush widens both), never under 10 px on screen at the spot's depth, never over 0.6 of the tool's contact.
    /// <paramref name="reach"/> widens it as Wide Brush does, the floor too (a pad's rub: BenchRules.PadRubReach).
    /// </summary>
    public float CleanRadius(ToolType tool, Camera cam, float reach = 1f)
    {
        reach = Mathf.Max(1f, reach);
        float upgrade = (UpgradeManager.Instance != null ? UpgradeManager.Instance.ScrubSpeedMultiplier : 1f) * reach;
        float floor = 0f;
        if (cam != null && !cam.orthographic)
        {
            float depth = Mathf.Max(.05f, Vector3.Dot(Centre - cam.transform.position, cam.transform.forward));
            floor = BenchRules.CleanFloorPx * reach * 2f * depth * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) / 1080f;
        }
        return BenchRules.CleanRadius(Radius, tool == ToolType.Cloth, upgrade, floor);
    }

    /// <summary>The point where a ray meets the spot's surface plane (the rub's contact: a tiny spot and a 15 fps jump both
    /// behave), or false when the ray runs along it.</summary>
    public bool Contact(Ray ray, out Vector3 point)
    {
        if (!awoke) Awake();
        var plane = new Plane(Normal, Centre);
        if (plane.Raycast(ray, out float enter) && enter > 0f) { point = ray.GetPoint(enter); return true; }
        point = Centre;
        return false;
    }

    /// <summary>True when a footprint of radius <paramref name="radius"/> at <paramref name="contact"/> overlaps the patch.</summary>
    public bool Under(Vector3 contact, float radius)
    {
        Vector2 p = PatchPoint(contact);
        Vector3 s = transform.lossyScale;
        float rx = Mathf.Abs(s.x) * .5f + radius, rz = Mathf.Abs(s.z) * .5f + radius;
        return p.x * p.x / (rx * rx) + p.y * p.y / (rz * rz) <= 1f;
    }

    /// <summary>
    /// One frame of the tool pressed on the spot: its footprint (a disc of <paramref name="radius"/>) sweeps from last frame's
    /// contact to <paramref name="contact"/> (the ray on the spot's plane), and takes the dirt along it (the press pass). True
    /// when dirt actually came off this frame (the caller sounds, rumbles and animates on that).
    /// </summary>
    public bool Pass(Vector3 contact, float radius)
    {
        if (!awoke) Awake();
        if (done || total <= 0f) return false;
        Vector3 from = hasLast && (lastWorld - contact).sqrMagnitude < .0025f ? lastWorld : contact;   // a jump across the bench is not a stroke
        bool newStroke = !hasLast;
        lastWorld = contact;
        hasLast = true;
        Vector2 a = PatchPoint(from), b = PatchPoint(contact);
        Vector2 ab = b - a;
        float length = ab.magnitude;
        if (length < 1e-6f) return false;
        // A new pass at a new contact, and at each turn of the stroke (the labs count the strokes a spot took).
        Vector2 dir = ab / length;
        if (newStroke || lastDirection != Vector2.zero && Vector2.Dot(dir, lastDirection) < -.2f) Passes++;
        lastDirection = dir;
        float strength = kind == Kind.Smudge ? BenchRules.ClothStrength : BenchRules.BrushStrength;
        float removed = Erase(a, b, radius, strength);
        // The finisher (§4.4.5): armed past 90 %, it counts the footprint's travel over the spot.
        if (Armed) pathOver += TravelOverSpot(a, b);
        bool came = removed > total * .0012f;
        if (removed > 0f)
        {
            cleared += removed;
            dirty = true;
        }
        if (came)
        {
            Strokes++;
            LastTouch = contact;
            LastCameOffAt = Time.time;
            float now = Time.time;
            // Dust puffs where the brush lifts some, with its grains (grime.scrub: "dirt is coming off here"). The cloth's own
            // sound is its rub loop now (BenchFeel); cloth.wipe a frame is retired (Audio §7.3).
            if (kind == Kind.Dirt && now - lastPuffAt > .09f && Application.isPlaying) { lastPuffAt = now; Juice.Dust(contact); Sfx.Play("grime.scrub", contact); }
        }
        ReportProgress();
        if (Progress >= cleanAt) Clean(false);
        else if (Armed && BenchRules.Finished(pathOver, 2f * Radius, padRub)) Clean(false);
        return came;
    }

    /// <summary>A pad rubbing in place over the spot this frame (the stick held still): counts toward the finisher.</summary>
    public void RubInPlace(float seconds)
    {
        if (done || !Armed) return;
        padRub += Mathf.Max(0f, seconds);
        if (BenchRules.Finished(pathOver, 2f * Radius, padRub)) Clean(false);
    }

    /// <summary>The old stroke: the dirt under the tip from the last frame's point to <paramref name="at"/> (the labs' and older
    /// callers'; travel is ignored but for none at all). It is the press pass at the brush's or the cloth's clean radius.</summary>
    public bool Scrub(Vector3 at, float travelPixels)
    {
        if (!awoke) Awake();
        if (travelPixels <= 0f) { lastWorld = at; hasLast = true; return false; }
        return Pass(at, CleanRadius(Tool, Camera.main));
    }

    /// <summary>The stroke is over (the button let go, or the tool left the spot): the next press starts a new path.</summary>
    public void EndStroke()
    {
        hasLast = false;
        lastDirection = Vector2.zero;
    }

    /// <summary>
    /// A stroke that comes onto the spot from outside it: its path starts where the tool was the frame before, so the
    /// strip it crossed coming on is taken too. At a low frame rate the cursor lands well inside the spot in one frame,
    /// and a stroke that started there left that strip's dirt (9 Oct night: Bench 3 with Unity behind other windows left
    /// the brushed spot at 93 %, all of it along one side). Only the patch's own texels are touched, so the part of the
    /// path outside the spot takes nothing.
    /// </summary>
    public void StrokeFrom(Vector3 from)
    {
        lastWorld = from;
        hasLast = true;
    }

    // A world point on the patch, in metres across it from its middle (x along its local x, y along its local z).
    Vector2 PatchPoint(Vector3 world)
    {
        Vector3 s = transform.lossyScale;
        Vector3 p = transform.InverseTransformPoint(world);
        return new Vector2(p.x * Mathf.Abs(s.x), p.z * Mathf.Abs(s.z));
    }

    // The length of a segment (patch metres) that lies over the spot's disc.
    float TravelOverSpot(Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float length = ab.magnitude;
        if (length < 1e-7f) return 0f;
        Vector2 dir = ab / length;
        float along = Vector2.Dot(-a, dir);
        float d = Mathf.Abs(dir.x * -a.y - dir.y * -a.x);
        return BenchRules.ChordShare(along, d, Radius, length) * 2f * Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - d * d));
    }

    // The press pass along one segment (patch metres): each speck within r of it loses start x strength x falloff x its chord
    // share (BenchRules.ChordShare), grease smearing first. Returns the dirt removed (alpha, summed). The smear's blur moves
    // alpha from speck to speck along the stroke; it never removes any.
    float Erase(Vector2 a, Vector2 b, float radius, float strength)
    {
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Max(1e-5f, Mathf.Abs(s.x)), sz = Mathf.Max(1e-5f, Mathf.Abs(s.z));
        Vector2 ab = b - a;
        float length = ab.magnitude;
        Vector2 dir = ab / Mathf.Max(1e-7f, length);
        float minX = Mathf.Min(a.x, b.x) - radius, maxX = Mathf.Max(a.x, b.x) + radius;
        float minZ = Mathf.Min(a.y, b.y) - radius, maxZ = Mathf.Max(a.y, b.y) + radius;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX / sx + .5f) * Texels)), x1 = Mathf.Min(Texels - 1, Mathf.CeilToInt((maxX / sx + .5f) * Texels));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((minZ / sz + .5f) * Texels)), y1 = Mathf.Min(Texels - 1, Mathf.CeilToInt((maxZ / sz + .5f) * Texels));
        if (x0 > x1 || y0 > y1) return 0f;
        // The smear's blur pulls from one texel back along the stroke's main direction.
        int bx = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? (dir.x > 0f ? -1 : 1) : 0;
        int by = bx == 0 ? (dir.y > 0f ? -1 : 1) : 0;
        bool greasy = kind == Kind.Smudge;
        float removed = 0f;
        for (int y = y0; y <= y1; y++)
        {
            float pz = ((y + .5f) / Texels - .5f) * sz;
            for (int x = x0; x <= x1; x++)
            {
                int i = y * Texels + x;
                if (start[i] <= 0f) continue;
                float px = ((x + .5f) / Texels - .5f) * sx;
                float rx = px - a.x, rz = pz - a.y;
                float along = rx * dir.x + rz * dir.y;
                float d = Mathf.Abs(rx * dir.y - rz * dir.x);
                float share = BenchRules.ChordShare(along, d, radius, length);
                if (share <= 0f) continue;
                float w = BenchRules.Falloff(d, radius) * share;
                if (w <= 0f) continue;
                float smearing = 0f, cleaning = w;
                if (greasy && smear[i] < 1f)
                {
                    BenchRules.SmearSplit(smear[i], w, out smearing, out cleaning);
                    smear[i] = Mathf.Min(1f, smear[i] + BenchRules.SmearGain * smearing);
                    if (smearing > 0f) Smear(i, x + bx, y + by, smearing);
                }
                float before = alpha[i];
                if (before <= 0f) continue;
                float after = Mathf.Max(0f, before - start[i] * BenchRules.Removed(strength, smearing, cleaning));
                if (after < .01f) after = 0f;
                alpha[i] = after;
                removed += before - after;
            }
        }
        return removed;
    }

    // Grease smeared: the speck whitens toward a haze (30 % over the whole smear), and its alpha is drawn out along the stroke
    // with the speck behind it (moved, not lost: the grade counts what came off, never what was spread).
    const float HazeR = 158f, HazeG = 148f, HazeB = 132f;   // 26 Oct 2026 (Art fixes) a26: the smeared grease's colour

    void Smear(int i, int bx, int by, float amount)
    {
        float k = Mathf.Clamp01(.3f * BenchRules.SmearGain * amount);
        Color32 p = pixels[i];
        // 26 Oct 2026 (Art fixes) a26: the haze is a see-through grey-brown, not near-white (242, 245, 247). The art audit, 23 Oct:
        // "one leftover smudge is a flat white cloud with a fuzzy edge. It looks like a sticker, while the rest of the dirt looks
        // real." White at the same opacity reads as paint on the dark glass; a warm grey-brown at it reads as grease you see through.
        pixels[i] = new Color32((byte)Mathf.Lerp(p.r, HazeR, k), (byte)Mathf.Lerp(p.g, HazeG, k), (byte)Mathf.Lerp(p.b, HazeB, k), 255);
        if (bx < 0 || bx >= Texels || by < 0 || by >= Texels) return;
        int j = by * Texels + bx;
        if (start[j] <= 0f) return;
        float flow = .4f * Mathf.Clamp01(BenchRules.SmearGain * amount) * (alpha[j] - alpha[i]) * .5f;
        alpha[i] += flow;
        alpha[j] -= flow;
    }

    // Progress for the stuck timer: an event every 2 % (BenchWork, throttled).
    void ReportProgress()
    {
        if (Progress - lastReportedProgress < .02f) return;
        lastReportedProgress = Progress;
        JobBase job = GetComponentInParent<JobBase>();
        if (job != null) BenchWork.NoteProgress(job);
    }

    // ---------- under a cover (§4.6) ----------

    /// <summary>
    /// The cover of its job that lies over it now (seated or popped): the one that lists it as a fault beneath, or else the one
    /// whose box, grown inward by half its thickness, holds the spot's middle. Null when nothing covers it (or its cover is off).
    /// The picker, the glow and "what's left" never reach a spot under a cover.
    /// </summary>
    public RemovablePart CoveredBy
    {
        get
        {
            JobBase job = GetComponentInParent<JobBase>();
            if (job == null) return null;
            Vector3 middle = Centre;
            RemovablePart byGeometry = null, listed = null;
            // Asked every frame by the picker and the rub: no allocation (a shared list, emptied before it returns).
            job.GetComponentsInChildren(coverBuffer);
            foreach (RemovablePart cover in coverBuffer)
            {
                if (cover == null || cover.IsRemoved) continue;
                foreach (GameObject f in cover.FaultsBeneath) if (f == gameObject) { listed = cover; break; }
                if (listed != null) break;
                if (byGeometry == null && Encloses(cover, middle)) byGeometry = cover;
            }
            coverBuffer.Clear();
            return listed != null ? listed : byGeometry;
        }
    }

    static readonly System.Collections.Generic.List<RemovablePart> coverBuffer = new System.Collections.Generic.List<RemovablePart>();

    // The cover's box, grown inward (away from its outside) by half its thickness, holds the point.
    static bool Encloses(RemovablePart cover, Vector3 world)
    {
        var box = cover.GetComponent<BoxCollider>();
        Vector3 centre = box != null ? box.center : Vector3.zero, size = box != null ? box.size : Vector3.one;
        Vector3 local = cover.transform.InverseTransformPoint(world) - centre;
        Vector3 inward = cover.transform.InverseTransformDirection(-cover.Outward).normalized;
        // The thin axis is the one most along the inward direction: the box grows that way by half its thickness (§4.6).
        Vector3 half = size * .5f;
        int thin = Mathf.Abs(inward.x) >= Mathf.Abs(inward.y) && Mathf.Abs(inward.x) >= Mathf.Abs(inward.z) ? 0 : Mathf.Abs(inward.y) >= Mathf.Abs(inward.z) ? 1 : 2;
        float grow = Mathf.Abs(half[thin]);      // half the thickness
        float lo = -Mathf.Abs(half[thin]), hi = Mathf.Abs(half[thin]);
        if (inward[thin] > 0f) hi += grow; else lo -= grow;
        for (int k = 0; k < 3; k++)
        {
            float v = local[k];
            if (k == thin) { if (v < lo || v > hi) return false; }
            else if (Mathf.Abs(v) > Mathf.Abs(half[k])) return false;
        }
        return true;
    }

    // ---------- what's left (§4.5: PowerWash's dirt indicator) ----------

    /// <summary>
    /// Lights the spot's dirt gold for <paramref name="seconds"/>: its faintest specks raised to be seen (alpha 0.6 at least
    /// where there's any), the gold rising over 0.1 s, held to 0.5 s, easing out; only the dirt glows (its own alpha carries it).
    /// A second call restarts it. Unscaled time (a pause doesn't stop it).
    /// </summary>
    public void Flash(float seconds = 1.5f)
    {
        if (!awoke) Awake();
        if (done || material == null) return;
        flashFrom = Time.unscaledTime;
        flashSeconds = Mathf.Max(.6f, seconds);
        flashing = true;
        material.EnableKeyword("_EMISSION");
        Upload();
    }

    /// <summary>The flash's gold now, 0..1.5 (labs).</summary>
    public float Flashed => flashing ? FlashLevel(Time.unscaledTime - flashFrom) : 0f;

    float FlashLevel(float t)
    {
        if (t < 0f || t >= flashSeconds) return 0f;
        if (t < .1f) return 1.5f * t / .1f;
        if (t < .5f) return 1.5f;
        float k = (t - .5f) / Mathf.Max(.01f, flashSeconds - .5f);
        return 1.5f * (1f - k) * (1f - k);
    }

    /// <summary>The middle of the dirt still drawn (its alpha-weighted centre, in the world): where a player's eye goes when
    /// a spot is half clean (the clumsy hand aims half its strokes there). The patch's middle when nothing is drawn.</summary>
    public Vector3 DirtLeftCentre
    {
        get
        {
            if (alpha == null || done) return Centre;
            float sx = 0f, sz = 0f, w = 0f;
            for (int y = 0; y < Texels; y++)
                for (int x = 0; x < Texels; x++)
                {
                    float a = alpha[y * Texels + x];
                    if (a <= .02f) continue;
                    sx += ((x + .5f) / Texels - .5f) * a;
                    sz += ((y + .5f) / Texels - .5f) * a;
                    w += a;
                }
            return w > 0f ? transform.TransformPoint(new Vector3(sx / w, surfaceY, sz / w)) : Centre;
        }
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
                // The gleam sweeps the glass, across the patch and a little beyond, the way the player sees it, with its chime on
                // the same frame (T14). The cloth goes home by itself after its last spot (ToolPickup), so it sweeps clear glass.
                Camera cam = Camera.main;
                Vector3 across = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, transform.up).normalized : transform.right;
                float half = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .9f;
                Juice.Gleam(at - across * half, at + across * half, GleamAfter);
                Sfx.Play("cloth.gleam", at);
            }
            else Sfx.Play("grime.clean", at);
            BenchFeel.Pulse(.20f, .50f, .12f);
            JobBase job = GetComponentInParent<JobBase>();
            if (job != null) BenchWork.NoteTargetDone(job, this);
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
