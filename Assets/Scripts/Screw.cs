using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A SCREW (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.1)
//
// It backs out of ITS OWN hole along ITS OWN axis (the socket's normal: this object's up in its home pose) while the driver
// is held on it, ticking every quarter turn, and the progress is kept if the hold lets go early. His playtest 5: "I honestly
// would say the screwdriver felt the best" - the turn is unchanged. What changed is what happens when it comes free. It used
// to become a 3 g rigidbody and fall: at his Mac's 15-20 fps it tunnelled through the mat, landed behind the device or under
// the camera's overhang ("moving it and putting them on the tray literally seems sooo hard"). Now (his decision 1, 14 Oct):
// at the last thread a click (screw.free), a little pop up its own axis, and it arcs into its own cup on the mat with a tink
// (screw.fly on the arc's first frame, screw.drop on its last; BenchStage's flight). Nothing physical: it can't be lost.
//
// To go back in, the empty hole (a ScrewSocket left where the screw was) or the screw in its cup is held with the driver: the
// screw is fetched from its cup on the same kind of arc (screw.fetch) to hover at its hole's mouth, and the same hold turns
// it home (the ticks, then screw.seat). Let go mid-fetch and the fetch completes; it hovers until the next hold.
//
// THE HOLE (his note: "ENOURMOUSE HOLES for the screwdrivers ... the holes are ENERMOUSe and the screws are so tiny"). The
// hole was drawn at the head's full width: a grey disc the head's diameter with a near-black core, and never under 4 mm (a 2 mm
// floor on its radius). Now it is Art's decal (A8): a small threaded opening in a slight dip, 1.15 x the head across, its
// dark bore 0.48 x the head, a bright lip round it, laid on the surface (Bench/BT_ScrewHole, or a texture drawn here to the
// same radii until that file lands). No floor: it is the head's size, whatever the head's size.
//
// Unscrew(bin) and Rescrew() do the whole thing by themselves (the labs' hand, the Day 1 guide): the same motions, no hold
// needed. IsOut: free of its hole (in its cup, flying, or fetched and hovering). IsBusy: a flight or a self-driven turn owns
// it (no press lands on it).
// ---------------------------------------------------------------------------
public class Screw : MonoBehaviour
{
    [Tooltip("Seconds of holding from seated to free (before upgrades, which shorten it, never to nothing).")]
    [SerializeField] private float turnDuration = 0.9f;
    [Tooltip("Turns from seated to free.")]
    [SerializeField] private float turns = 3f;

    /// <summary>The thread, as a share of the head's width (§4.1.5: lengths are relative, so the screw backs out the same
    /// share of itself at any scale; the old fixed 6 mm thread was tuned for the 8 mm cartoon heads).</summary>
    public const float ThreadPerHead = 2f;
    /// <summary>The pop as it comes free: up its own axis by this many of its head's heights, over PopSeconds.</summary>
    public const float PopHeads = 1.5f, PopSeconds = .06f;
    /// <summary>The settle hop in its cup, as a share of its head's width.</summary>
    public const float HopPerHead = .25f;

    /// <summary>Free of its hole: in its cup, flying, or fetched and hovering over the hole.</summary>
    public bool IsOut { get; private set; }
    /// <summary>A flight or a self-driven turn owns it: no new press lands.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>Resting in its cup: out, not flying, not fetched.</summary>
    public bool InCup => IsOut && !fetched && !fetching && !flying;
    /// <summary>The old name for resting where it went when it came out (the v2 labs).</summary>
    public bool IsLoose => InCup;
    /// <summary>Flying: to its cup, or fetched back to its hole.</summary>
    public bool Flying => flying || fetching;
    /// <summary>Fetched: hovering over its hole, ready to be screwed in.</summary>
    public bool Fetched => fetched;
    /// <summary>0 seated .. 1 free. Kept between holds.</summary>
    public float Progress { get; private set; }
    public float Turns => turns;
    public ScrewSocket Socket => socket;

    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform homeParent;
    private Collider ownCollider;
    private ScrewSocket socket;
    private JobBase job;
    private bool fetched, fetching, flying;
    private int lastTick;
    private Vector3 headSize;
    private float outSign = 1f;     // +1: the screw's local up points out of the device; -1: its local down does
    private bool awoke;

    private void Awake()
    {
        if (awoke) return;
        awoke = true;
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        ownCollider = GetComponent<Collider>();
        job = GetComponentInParent<JobBase>();
        headSize = HeadSize();
        // Which way is out: away from the device's middle. The stand-in devices' screws sit on the back with their local up
        // pointing INTO the body (the old screw rose that way too, unnoticed); the head is on the outside, so out is -up.
        Vector3 up = homeParent != null ? homeParent.rotation * homeLocalRot * Vector3.up : transform.up;
        outSign = Vector3.Dot(transform.position - DeviceCentre(), up) < 0f ? -1f : 1f;
        // An older prefab may carry a rigidbody from the v2 bench: nothing at the bench is physical now (rule 7).
        var body = GetComponent<Rigidbody>();
        if (body != null) { if (Application.isPlaying) Destroy(body); else DestroyImmediate(body); }
    }

    // The head's size along its own axes (a world AABB would be inflated by whatever yaw the device lies at).
    private Vector3 HeadSize()
    {
        Vector3 scale = transform.lossyScale;
        if (ownCollider is BoxCollider box) return Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        if (ownCollider is CapsuleCollider capsule) return new Vector3(capsule.radius * 2f * scale.x, capsule.height * scale.y, capsule.radius * 2f * scale.z);
        if (ownCollider is SphereCollider sphere) return Vector3.one * (sphere.radius * 2f * Mathf.Max(scale.x, scale.y, scale.z));
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return Vector3.Scale(mf.sharedMesh.bounds.size, scale);
        return scale;
    }

    private Vector3 DeviceCentre()
    {
        Transform root = job != null ? job.transform : homeParent;
        if (root == null) return transform.position - transform.up * .01f;
        bool any = false;
        Bounds b = new Bounds(root.position, Vector3.zero);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            // The device's own solid parts (DeviceBounds): its job tag counted, a phone's screws fell toward a point 70 cm off.
            if (r.transform == transform || !DeviceBounds.Counts(r)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b.center : root.position;
    }

    // Upgrades make the driver faster, never instant.
    private float TurnTime
    {
        get
        {
            float mult = UpgradeManager.Instance != null ? UpgradeManager.Instance.ScrewTimeMultiplier : 1f;
            return Mathf.Max(.25f, turnDuration * mult);
        }
    }

    /// <summary>The screw's own axis, pointing out of the hole, in the world.</summary>
    public Vector3 Axis => (homeParent != null ? homeParent.rotation * homeLocalRot * Vector3.up : transform.up).normalized * outSign;
    /// <summary>The same axis in the screw's own frame (its up, or its down).</summary>
    public Vector3 AxisLocal => Vector3.up * outSign;
    /// <summary>One of its own sideways axes: lying on its side in a cup, this points up.</summary>
    public Vector3 LyingUpLocal => Vector3.right;
    /// <summary>The hole's centre in the world (the screw's seated position).</summary>
    public Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPos) : transform.position;
    /// <summary>The head's top, where the driver's tip goes.</summary>
    public Vector3 HeadPoint => transform.position + Axis * (headSize.y * .5f);
    /// <summary>About how wide the head is (metres), and half that.</summary>
    public float HeadDiameter => Mathf.Max(headSize.x, headSize.z);
    public float HeadRadius => HeadDiameter * .5f;
    /// <summary>The head's height (metres).</summary>
    public float HeadHeight => headSize.y;
    /// <summary>How far it backs out before it's free (metres): twice its head's width (§4.1.5).</summary>
    public float Thread => ThreadPerHead * HeadDiameter;
    /// <summary>The job it belongs to (remembered while it is off the device).</summary>
    public JobBase Job => job;

    // What the checklist and the glow call this screw while it is in: its hover target (ScrewTarget), else itself.
    private Component Target { get { var t = GetComponent<ScrewTarget>(); return t != null ? (Component)t : this; } }

    // ---------- the hold ----------

    /// <summary>
    /// One frame of the driver held on the screw. Outward backs it out; inward (after a fetch) screws it home. Returns
    /// true when the end is reached (free, or seated), which ends the hold.
    /// </summary>
    public bool HoldTurn(float deltaTime, bool outward)
    {
        if (IsBusy) return false;
        return Turn(deltaTime, outward);
    }

    private bool Turn(float deltaTime, bool outward)
    {
        if (outward && IsOut) return true;
        if (!outward && (!IsOut || !fetched)) return false;
        float before = Progress;
        Progress = Mathf.Clamp01(Progress + (outward ? 1f : -1f) * deltaTime / TurnTime);
        PlaceInHole();
        // A tick every QUARTER turn (Audio §7.4: 12 in a three-turn screw, the driver biting), not one a turn.
        int tick = Mathf.FloorToInt(Progress * turns * 4f + 1e-4f);
        if (tick != lastTick)
        {
            lastTick = tick;
            if (Progress > 0f && Progress < 1f) Sfx.Play("screw.turn", transform.position);
        }
        if (Progress != before && job != null) BenchWork.NoteProgress(job);
        if (outward && Progress >= 1f && before < 1f) { Free(); return true; }
        if (!outward && Progress <= 0f && before > 0f) { Seat(); return true; }
        return false;
    }

    // Seated or part-way out: on its axis, turned by how far it has come.
    private void PlaceInHole()
    {
        if (transform.parent != homeParent) transform.SetParent(homeParent, true);
        Quaternion home = homeParent != null ? homeParent.rotation * homeLocalRot : homeLocalRot;
        transform.SetPositionAndRotation(HomePosition + Axis * (Thread * Progress),
            home * Quaternion.AngleAxis(outSign * turns * 360f * Progress, Vector3.up));   // anticlockwise, seen from the head
    }

    // ---------- free: the pop and the flight to its cup ----------

    private void Free()
    {
        IsOut = true;
        fetched = false;
        Progress = 1f;
        Sfx.Play("screw.free", transform.position);
        BenchFeel.Pulse(.15f, .35f, .06f);
        // The hole it leaves behind, for the hover, the prompt and the way back.
        if (socket == null) socket = ScrewSocket.Make(this, homeParent, homeLocalPos,
            outSign > 0f ? homeLocalRot : homeLocalRot * Quaternion.AngleAxis(180f, Vector3.forward));   // the hole's up is out
        socket.gameObject.SetActive(true);
        // Leave the item (so turning it doesn't drag the screw along) but stay OWNED by the job so cleanup finds it.
        transform.SetParent(null, true);
        if (job != null) job.RegisterDetached(gameObject);
        if (job != null) BenchWork.NoteTargetDone(job, Target);
        // The pop up its own axis, then the arc into its own cup (BenchStage: the slot rule picks the cup).
        flying = true;
        IsBusy = true;
        BenchStage.SendToPlace(this, job, BenchStage.SlotKind.Cup, BenchStage.FlightKind.Screw,
            0f, PopSeconds, Axis * (PopHeads * Mathf.Max(.0005f, HeadHeight)), null, false,
            "screw.fly", "screw.drop", HopPerHead * HeadDiameter, Landed);
    }

    private void Landed()
    {
        flying = false;
        IsBusy = fetching;
    }

    // ---------- the way back ----------

    /// <summary>Starts the fetch: from its cup to hovering at its hole's mouth (the hold then screws it in).</summary>
    public void BeginFetch()
    {
        if (!IsOut || fetched || fetching || flying) return;
        fetching = true;
        IsBusy = true;
        // (still a piece off the device until it is seated: a fetched screw hovering over its hole is not in; 4b, so the
        // "back together" moment comes at the last seat, not a second early at the last fetch)
        Quaternion hoverLocal = homeLocalRot * Quaternion.AngleAxis(outSign * turns * 360f, Vector3.up);
        Vector3 mouthWorld = HomePosition + Axis * Thread;
        Vector3 mouthLocal = homeParent != null ? homeParent.InverseTransformPoint(mouthWorld) : mouthWorld;
        float px = BenchStage.ScreenPixels(transform.position, mouthWorld);
        float seconds = BenchRules.FetchSeconds(BenchRules.FlightSeconds(px, true));
        BenchStage.SendTo(this, job, homeParent, mouthLocal, homeParent != null ? hoverLocal : transform.rotation,
            BenchStage.FlightKind.Screw, seconds, "screw.fetch", null, homeParent, Fetched_);
    }

    private void Fetched_()
    {
        Progress = 1f;
        lastTick = Mathf.FloorToInt(turns * 4f);
        fetched = true;
        fetching = false;
        IsBusy = flying;
        if (job != null) BenchWork.NoteProgress(job);
    }

    private void Seat()
    {
        IsOut = false;
        fetched = false;
        Progress = 0f;
        lastTick = 0;
        if (transform.parent != homeParent) transform.SetParent(homeParent, true);
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        if (job != null) job.UnregisterDetached(gameObject);
        if (BenchStage.Instance != null) BenchStage.Instance.Release(this);
        if (socket != null) socket.gameObject.SetActive(false);
        Sfx.Play("screw.seat", transform.position);
        if (job != null) BenchWork.NoteTargetDone(job, socket != null ? (Component)socket : this);
    }

    // ---------- the whole thing by itself (the labs, the guide, older callers) ----------

    /// <summary>Backs the screw all the way out and sends it to its cup (the bin argument is the old API; the stage decides).</summary>
    public void Unscrew(Transform bin)
    {
        if (!awoke) Awake();   // the editor's checks call in before Awake has run
        if (IsOut || IsBusy) return;
        if (!Application.isPlaying) { Progress = 1f; Free(); return; }   // the editor's checks: no frames to animate over
        StartCoroutine(SelfTurn(outward: true));
    }

    /// <summary>Fetches the screw to its hole and screws it home.</summary>
    public void Rescrew()
    {
        if (!awoke) Awake();
        if (!IsOut || IsBusy) return;
        if (!Application.isPlaying)
        {
            if (BenchStage.Instance != null) BenchStage.Instance.Release(this);
            transform.SetParent(homeParent, false);
            Seat();
            return;
        }
        StartCoroutine(SelfTurn(outward: false));
    }

    private IEnumerator SelfTurn(bool outward)
    {
        if (!outward && !fetched)
        {
            BeginFetch();
            while (fetching) yield return null;
        }
        IsBusy = true;                         // no hand's press lands while the screw drives itself
        bool done = false;
        while (!done)
        {
            done = Turn(Time.deltaTime, outward);
            if (!done) yield return null;
        }
        // Out: the flight to its cup keeps it busy until it lands (Landed). In: it is seated.
        IsBusy = flying || fetching;
    }

    private void OnDestroy()
    {
        if (socket == null) return;
        if (Application.isPlaying) Destroy(socket.gameObject); else DestroyImmediate(socket.gameObject);   // the editor's checks tear jobs down in edit mode
    }
}

// ---------------------------------------------------------------------------
// THE EMPTY HOLE a screw leaves: Art's decal (A8) on the surface where the screw was, hoverable and holdable with the driver,
// which fetches the screw from its cup and screws it home. Made by the screw the first time it comes free; hidden when it is
// seated again.
// ---------------------------------------------------------------------------
public class ScrewSocket : BenchInteractable
{
    private Screw screw;
    private ScrewTarget target;

    /// <summary>The decal is this many times the head's width across (Art A8, the Producer's call 3).</summary>
    public const float DecalPerHead = 1.15f;
    /// <summary>The dark bore's diameter as a share of the head's (the fix for "ENORMOUS holes").</summary>
    public const float BorePerHead = .48f;

    public Screw Screw => screw;

    public static ScrewSocket Make(Screw screw, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(screw.name + " (hole)");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.layer = screw.gameObject.layer;
        // In metres whatever the device's scale: the socket undoes its parent's scale (the old hole mixed the head's world size
        // with the parent's local units, so a scaled device drew it k times too big; the TD's audit, td-one-scale.md §2.2).
        Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(1f / Mathf.Max(1e-5f, Mathf.Abs(ps.x)), 1f / Mathf.Max(1e-5f, Mathf.Abs(ps.y)), 1f / Mathf.Max(1e-5f, Mathf.Abs(ps.z)));
        float head = Mathf.Max(1e-4f, screw.HeadDiameter);   // no floor: the hole is the head's size, whatever that is

        // The decal: a quad 1.15 x the head across, laid 0.05 mm proud of the surface, facing out (Unity's quad faces -Z; a
        // quarter turn about X lays it face up along the hole's up).
        var decal = new GameObject("Hole");
        decal.transform.SetParent(go.transform, false);
        decal.transform.localPosition = Vector3.up * .00005f;
        decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        decal.transform.localScale = new Vector3(head * DecalPerHead, head * DecalPerHead, 1f);
        decal.layer = go.layer;
        decal.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = decal.AddComponent<MeshRenderer>();
        mr.sharedMaterial = HoleMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // A thin box stays, flush with the surface: the cursor's ray meets it for the surface point (the picker hits the hole by
        // its screen circle, BenchRules.ScrewHitRadius, so its size is no longer the hole's reach).
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(head * 1.2f, .0005f, head * 1.2f);
        box.center = Vector3.up * .00025f;
        var socket = go.AddComponent<ScrewSocket>();
        socket.screw = screw;
        socket.target = screw.GetComponent<ScrewTarget>();
        return socket;
    }

    // ---------- the decal's look (Art A8): Bench/BT_ScrewHole, or the same radii drawn here ----------

    static Material holeMaterial;
    static Texture2D drawnHole;

    static Material HoleMaterial
    {
        get
        {
            if (holeMaterial != null) return holeMaterial;
            Texture2D texture = Resources.Load<Texture2D>("Bench/BT_ScrewHole");
            if (texture == null) texture = DrawnHole;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(lit) { name = "Screw hole decal (runtime)", hideFlags = HideFlags.HideAndDontSave };
            // Transparent, lit (the lip catches the bench lamp), never writing depth or casting a shadow (GrimeSpot's recipe).
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;   // under the dirt patches, which lie over it
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Smoothness", .35f);
            m.SetFloat("_Metallic", 0f);
            holeMaterial = m;
            return m;
        }
    }

    /// <summary>
    /// Art's A8 hole drawn in code, 128 x 128, radii as shares of the head's radius (the quad spans 1.15 of it): the bore to
    /// 0.48 near-black with two faint thread rings, a bright lip to 0.55, a dip darkening toward the middle to 0.90, a faint
    /// witness ring to 1.00, clear beyond.
    /// </summary>
    public static Texture2D DrawnHole
    {
        get
        {
            if (drawnHole != null) return drawnHole;
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Screw hole (drawn)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 2 x 2 samples a texel, so the small hole's bands don't shimmer.
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = ((x + .25f + .5f * sx) / size - .5f) * 2f * DecalPerHead, v = ((y + .25f + .5f * sy) / size - .5f) * 2f * DecalPerHead;
                            Sample(Mathf.Sqrt(u * u + v * v), out Color c, out float alpha);
                            r += c.r * alpha; g += c.g * alpha; b += c.b * alpha; a += alpha;
                        }
                    Color mixed = a > 1e-4f ? new Color(r / a, g / a, b / a) : Color.white;
                    pixels[y * size + x] = new Color32((byte)(mixed.r * 255f), (byte)(mixed.g * 255f), (byte)(mixed.b * 255f), (byte)(Mathf.Clamp01(a * .25f) * 255f));
                }
            t.SetPixels32(pixels);
            t.Apply(true, false);
            drawnHole = t;
            return t;
        }
    }

    // One sample of the hole at r head radii: its colour and its alpha (Art A8's bands).
    static void Sample(float r, out Color colour, out float alpha)
    {
        if (r < BorePerHead)
        {
            // The bore, near-black, with two faint thread rings at 0.30 and 0.40.
            bool ring = Mathf.Abs(r - .30f) < .015f || Mathf.Abs(r - .40f) < .015f;
            colour = ring ? Hex(0x3A3632) : Hex(0x1A1816);
            alpha = 1f;
        }
        else if (r < .55f) { colour = Hex(0xD8D2C6); alpha = .8f; }                       // the lip: the thread's entry catching the lamp
        else if (r < .90f) { colour = Color.black; alpha = .35f * (.90f - r) / .35f; }   // the dip, darkening toward the middle
        else if (r < 1f) { colour = Color.white; alpha = .12f; }                          // the witness ring (cleaner metal under the head)
        else { colour = Color.white; alpha = 0f; }
    }
    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    public override string DisplayName => "Screw hole";
    public override string Prompt => screw != null && screw.Fetched ? "Hold to screw in" : "Hold to screw in (the driver fetches the screw)";
    public override ToolType RequiredTool => ToolType.Screwdriver;
    public override bool CanInteract => screw != null && screw.IsOut && !screw.IsBusy && (target == null || target.PlateSeated);
    public override bool Holdable => true;
    public override float HoldProgress => screw != null ? 1f - screw.Progress : 0f;
    public override Vector3 WorkPoint => transform.position + transform.up * .0005f;
    public override Vector3 WorkNormal => transform.up;

    public override bool HoldTick(BenchHand hand)
    {
        if (screw == null) return true;
        if (!screw.Fetched) { screw.BeginFetch(); return false; }
        return screw.HoldTurn(hand.deltaTime, outward: false);
    }

    public override void Activate()
    {
        if (screw != null) screw.Rescrew();
    }
}
