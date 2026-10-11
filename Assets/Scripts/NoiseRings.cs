using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// NOISE YOU CAN SEE (playtest 4, 10 Oct 2026; the veteran: "Danger is invisible until she's already noticing ... nothing on
// screen says how loud or how lit Ace is"; Mark of the Ninja draws each sound as "a spreading blue ring that says exactly how
// far that noise will reach")
//
// Each sound Ace makes where someone could hear it is drawn as a ring on the floor at its source, at the radius the game
// already gives it (NightNoise: walking 4 m, sneaking 1 m, half that with Soft feet; the cups' rustle 3 m; a creaky tread
// 5 m, 2.5 m under a sneaking foot; her bedroom doors opened at a walk 5 m). It spreads to that radius in about a tenth of a
// second and fades out within half a second. A rule shown, not a rule added: a ring is drawn only for a noise NightNoise
// sends anyway, at the radius it is heard within (straight-line, whatever the walls, as her ears take it).
//
// Only where it matters: a noise someone could hear (GraceAtHome.Listens: inside her walls while she's home, awake or
// asleep). Her stoop and the street stay calm: nobody hears a sound there, so nothing is drawn, and the rings appearing as
// Ace steps over her threshold is itself the read ("she can hear you now"). Walking and sneaking read apart: a walking
// step's ring is four times as wide, a little brighter and a little bolder. A walk is seven steps a second, so at most one
// step's ring every 0.28 s (a sneak is about three a second: each one is drawn).
//
// Warm white, thin and faint: a ripple on her floor, not a target, the same from the overhead and in first person (depth
// tested, so her furniture hides what's under it). Drawn on the floor Ace stands on, where the sound came from. Made by
// NightWalk while the night runs (Begin, End); nothing in the scene changes, and nothing allocates once it is made (a pool
// of line renderers, one shared material, as DrinkFreshnessRing draws its ring).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NoiseRings : MonoBehaviour
{
    public static NoiseRings Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    const int Pool = 14, Segments = 56;
    /// <summary>Seconds a ring lasts; it spreads to its radius over the first Spread of them, from StartAt of it. It shows at
    /// once (the sound is made at once) and fades over its whole life.</summary>
    public const float Life = .5f, Spread = .12f, StartAt = .8f;
    /// <summary>At most one step's ring this often (seconds): a walk is seven steps a second.</summary>
    public const float StepEvery = .28f;
    /// <summary>Over the floor (metres), so the ring never sinks into it.</summary>
    const float Lift = .03f;
    static readonly Color Warm = new Color(1f, .95f, .86f, 1f);

    sealed class Ring
    {
        public LineRenderer line;
        public Vector3 centre;
        public float born = -10f, radius, drawnRadius = -1f, peak;
        public bool alive;
    }

    readonly Ring[] rings = new Ring[Pool];
    readonly Vector3[] points = new Vector3[Segments];
    static readonly Vector2[] Unit = MakeUnitCircle();
    GameObject root;
    Material material;
    CafeViewMode view;
    float lastStepRing = -10f;
    bool listening, warned;

    // ---- for checks and reports ----
    /// <summary>Rings drawn since the night began, and of each kind (indexed by NoiseKind).</summary>
    public int Shown { get; private set; }
    public readonly int[] ShownByKind = new int[System.Enum.GetValues(typeof(NoiseKind)).Length];
    /// <summary>Noises made where nobody could hear them (the street, her stoop, her house while she's out): not drawn.</summary>
    public int NotHeard { get; private set; }
    /// <summary>Steps heard but not drawn because a step's ring had just been drawn (StepEvery).</summary>
    public int StepsBetween { get; private set; }
    /// <summary>The last ring drawn: its kind, the radius it spreads to (the noise's own), its middle (world).</summary>
    public NoiseKind LastKind { get; private set; }
    public float LastRadius { get; private set; }
    public Vector3 LastCentre { get; private set; }
    /// <summary>Rings on screen now.</summary>
    public int Up
    {
        get
        {
            int n = 0;
            foreach (Ring r in rings) if (r != null && r.alive) n++;
            return n;
        }
    }

    void Awake() => Instance = this;

    /// <summary>The night begins: listen for noises.</summary>
    public void Begin()
    {
        Instance = this;
        Build();
        if (!listening)
        {
            NightNoise.Made += OnNoise;
            listening = true;
        }
        Shown = NotHeard = StepsBetween = 0;
        System.Array.Clear(ShownByKind, 0, ShownByKind.Length);
        lastStepRing = -10f;
    }

    /// <summary>The night is over: every ring goes.</summary>
    public void End()
    {
        StopListening();
        foreach (Ring r in rings)
        {
            if (r == null) continue;
            r.alive = false;
            if (r.line != null) r.line.enabled = false;
        }
    }

    void StopListening()
    {
        if (!listening) return;
        NightNoise.Made -= OnNoise;
        listening = false;
    }

    void OnDisable() => End();

    void OnDestroy()
    {
        StopListening();
        if (root != null) Destroy(root);
        if (material != null) Destroy(material);
        root = null;
        material = null;
        if (Instance == this) Instance = null;
    }

    /// <summary>Whether a noise made at <paramref name="at"/> could be heard by anyone, so it's drawn.</summary>
    public static bool Matters(Vector3 at)
    {
        GraceAtHome home = GraceAtHome.Instance;
        return home != null && home.Listens(at);
    }

    void OnNoise(NightNoiseEvent noise)
    {
        if (noise.radius <= 0f) return;
        if (!Matters(noise.position)) { NotHeard++; return; }
        bool step = noise.kind == NoiseKind.Step || noise.kind == NoiseKind.SneakStep;
        float now = Time.time;
        if (step && now - lastStepRing < StepEvery) { StepsBetween++; return; }
        if (step) lastStepRing = now;
        Build();
        Ring ring = Free();
        if (ring == null || ring.line == null) return;
        ring.centre = OnTheFloor(noise.position);
        ring.radius = noise.radius;
        ring.drawnRadius = -1f;
        ring.born = now;
        ring.alive = true;
        bool walk = noise.kind == NoiseKind.Step;
        bool sneak = noise.kind == NoiseKind.SneakStep;
        // A walking step bolder and brighter than a sneaking one; the rest (the rustle, a creak, her doors) as a walk.
        float width = sneak ? .035f : walk ? .055f : .06f;
        ring.peak = sneak ? .38f : walk ? .5f : .55f;
        ring.line.startWidth = ring.line.endWidth = width;
        Draw(ring, 0f);
        ring.line.enabled = true;
        Shown++;
        ShownByKind[(int)noise.kind]++;
        LastKind = noise.kind;
        LastRadius = noise.radius;
        LastCentre = ring.centre;
    }

    void Update()
    {
        float now = Time.time;
        foreach (Ring r in rings)
        {
            if (r == null || !r.alive) continue;
            float age = now - r.born;
            if (age >= Life || r.line == null)
            {
                r.alive = false;
                if (r.line != null) r.line.enabled = false;
                continue;
            }
            Draw(r, age);
        }
    }

    // Spreading from StartAt of its radius to all of it (easing out), at full strength at once and faded out by Life.
    void Draw(Ring r, float age)
    {
        float grow = Mathf.Clamp01(age / Spread);
        float k = 1f - (1f - grow) * (1f - grow);
        float radius = r.radius * Mathf.Lerp(StartAt, 1f, k);
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Life));
        Color c = Warm;
        c.a = r.peak * Mathf.Clamp01(alpha);
        r.line.startColor = r.line.endColor = c;
        if (Mathf.Abs(radius - r.drawnRadius) < .002f) return;
        r.drawnRadius = radius;
        for (int i = 0; i < Segments; i++) points[i] = r.centre + new Vector3(Unit[i].x * radius, 0f, Unit[i].y * radius);
        r.line.SetPositions(points);
    }

    // The floor Ace stands on when Ace made the sound (a step, the rustle at the worktop, a tread, her doors): the noise's
    // own height otherwise (a check's noise with nobody near).
    Vector3 OnTheFloor(Vector3 at)
    {
        if (view == null) view = FindAnyObjectByType<CafeViewMode>();
        float y = at.y;
        if (view != null)
        {
            Vector3 feet = view.AceFeet;
            float dx = feet.x - at.x, dz = feet.z - at.z;
            if (dx * dx + dz * dz < 4f && Mathf.Abs(feet.y - at.y) < 2.2f) y = feet.y;
        }
        return new Vector3(at.x, y + Lift, at.z);
    }

    // A ring not in use, or the oldest one.
    Ring Free()
    {
        Ring oldest = null;
        foreach (Ring r in rings)
        {
            if (r == null) continue;
            if (!r.alive) return r;
            if (oldest == null || r.born < oldest.born) oldest = r;
        }
        return oldest;
    }

    void Build()
    {
        if (root != null) return;
        if (material == null)
        {
            // As DrinkFreshnessRing's ring (unlit, its colour from the line's own); URP's unlit if that's ever missing.
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                if (!warned) Debug.LogWarning("[Noise rings] No Sprites/Default shader to draw the rings with: no rings tonight.");
                warned = true;
                return;
            }
            material = new Material(shader) { name = "Noise ring (night)", hideFlags = HideFlags.DontSave };
        }
        root = new GameObject("Noise rings (while the night runs)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        root.transform.SetParent(transform, false);
        for (int i = 0; i < Pool; i++)
        {
            var go = new GameObject("Ring " + i);
            go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            rings[i] = new Ring { line = line };
        }
    }

    static Vector2[] MakeUnitCircle()
    {
        var unit = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            unit[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        return unit;
    }

    public string Describe() =>
        $"Noise rings: {Shown} drawn this night (walking {ShownByKind[(int)NoiseKind.Step]}, sneaking {ShownByKind[(int)NoiseKind.SneakStep]}, " +
        $"rustles {ShownByKind[(int)NoiseKind.Rustle]}, creaks {ShownByKind[(int)NoiseKind.CreakyStair]}, doors {ShownByKind[(int)NoiseKind.Door]}); " +
        $"{NotHeard} noise(s) nobody could hear (not drawn), {StepsBetween} step(s) between rings; {Up} up now" +
        (Shown > 0 ? $"; the last: {LastKind}, {LastRadius:0.0} m." : ".");
}
