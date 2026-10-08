using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A SCREW (the bench, v2; claude/bench-spec-v2.md §2.1)
//
// It backs out of ITS OWN hole along ITS OWN axis (the socket's normal: this object's up in its home pose) while the
// driver is held on it: a tick a turn, and the progress is kept if the hold lets go early. When it comes free it clicks,
// becomes a rigidbody and FALLS: onto the mat, the device, into the tray, where the stage's magnet settles it with a
// tink (BenchStage). Nothing is lost: the stage catches anything that leaves the mat. To go back in, the empty hole (a
// ScrewSocket left where the screw was) is held with the driver: the screw is fetched from wherever it lies to hover
// over its hole, and the hold screws it home (the ticks in reverse, a seat snap at the end). Mansoor's call, 7 Oct:
// the driver fetches; nobody carries screws by hand.
//
// Unscrew(bin) and Rescrew() do the whole thing by themselves (the labs' hand, the Day 1 guide): the same motions, no
// hold needed. IsOut: free of its hole (loose, or fetched and hovering). IsBusy: a fetch or a self-driven turn owns it.
// ---------------------------------------------------------------------------
public class Screw : MonoBehaviour
{
    [Tooltip("Seconds of holding from seated to free (before upgrades, which shorten it, never to nothing).")]
    [SerializeField] private float turnDuration = 0.9f;
    [Tooltip("How far the screw backs out before it is free, metres (its thread).")]
    [SerializeField] private float threadLength = 0.006f;
    [Tooltip("Turns from seated to free.")]
    [SerializeField] private float turns = 3f;
    [Tooltip("Seconds for the fetch: from where it lies to hovering over its hole.")]
    [SerializeField] private float flyDuration = 0.3f;
    [SerializeField] private float mass = 0.003f;
    [Tooltip("The pop when it comes free: speed out of the hole along its own axis, m/s (0.6 is a hop of about 2 cm).")]
    [SerializeField] private float popUp = 0.6f;
    [Tooltip("The pop when it comes free: speed across the face, away from the device's middle, m/s (it lands on the mat beside the device).")]
    [SerializeField] private float popOut = 0.16f;
    // The old serialized field, kept so older prefabs load without a warning; no longer used.
#pragma warning disable 0414
    [SerializeField, HideInInspector] private float liftHeight = 0.02f;
#pragma warning restore 0414

    /// <summary>Free of its hole: lying loose, or fetched and hovering over the hole.</summary>
    public bool IsOut { get; private set; }
    /// <summary>A fetch or a self-driven turn owns it: no new press lands.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>Lying somewhere with physics on (not in a hand, not fetched).</summary>
    public bool IsLoose => IsOut && body != null && !body.isKinematic && !fetched;
    /// <summary>Fetched: hovering over its hole, ready to be screwed in.</summary>
    public bool Fetched => fetched;
    /// <summary>0 seated .. 1 free. Kept between holds.</summary>
    public float Progress { get; private set; }
    public float Turns => turns;
    public ScrewSocket Socket => socket;

    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform homeParent;
    private Rigidbody body;
    private Collider ownCollider;
    private ScrewSocket socket;
    private JobBase job;
    private bool fetched, fetching;
    private int lastTick;
    private float lastBounceAt;
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
            if (r.transform == transform) continue;
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
    /// <summary>The hole's centre in the world (the screw's seated position).</summary>
    public Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPos) : transform.position;
    /// <summary>The head's top, where the driver's tip goes.</summary>
    public Vector3 HeadPoint => transform.position + Axis * (headSize.y * .5f);
    /// <summary>About how wide the head is (for the hole's ring).</summary>
    public float HeadRadius => Mathf.Max(headSize.x, headSize.z) * .5f;

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
        int tick = Mathf.FloorToInt(Progress * turns + 1e-4f);
        if (tick != lastTick)
        {
            lastTick = tick;
            if (Progress > 0f && Progress < 1f) Sfx.Play("screw.turn", transform.position);
        }
        if (outward && Progress >= 1f && before < 1f) { Free(); return true; }
        if (!outward && Progress <= 0f && before > 0f) { Seat(); return true; }
        return false;
    }

    // Seated or part-way out: on its axis, turned by how far it has come.
    private void PlaceInHole()
    {
        if (transform.parent != homeParent) transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPos + homeLocalRot * Vector3.up * (outSign * threadLength * Progress);
        transform.localRotation = homeLocalRot * Quaternion.AngleAxis(outSign * turns * 360f * Progress, Vector3.up);   // anticlockwise, seen from the head
    }

    // ---------- free, and falling ----------

    private void Free()
    {
        IsOut = true;
        fetched = false;
        Progress = 1f;
        Sfx.Play("screw.free", transform.position);
        // The hole it leaves behind, for the hover, the prompt and the way back.
        if (socket == null) socket = ScrewSocket.Make(this, homeParent, homeLocalPos,
            outSign > 0f ? homeLocalRot : homeLocalRot * Quaternion.AngleAxis(180f, Vector3.forward));   // the hole's up is out
        socket.gameObject.SetActive(true);
        // Leave the item (so turning it doesn't drag the screw along) but stay OWNED by the job so cleanup finds it.
        transform.SetParent(null, true);
        if (job != null) job.RegisterDetached(gameObject);
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = mass;
        body.linearDamping = .35f;
        body.angularDamping = .6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (ownCollider != null) ownCollider.isTrigger = false;
        // The pop: freed, the screw hops clear of its hole and tumbles down beside the device, the way ReStory's do. A
        // device lying on the mat has its screws facing UP, so without the hop a freed screw fell straight back against
        // its own hole and looked as if it were still in (the lab caught it, 8 Oct: at rest 7 mm from the hole). Up
        // about 2 cm, out toward the device's nearest side, a little spin: it lands on the mat a few centimetres off, or
        // in the tray, and sounds its landing.
        Vector3 axis = Axis;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - DeviceCentre(), axis);
        away = away.sqrMagnitude > 1e-8f ? away.normalized : Vector3.ProjectOnPlane(Random.onUnitSphere, axis).normalized;
        Vector3 across = Vector3.Cross(axis, away).normalized * Random.Range(-.04f, .04f);
        body.linearVelocity = axis * popUp + away * (popOut * Random.Range(.8f, 1.2f)) + across;
        body.angularVelocity = Random.onUnitSphere * Random.Range(6f, 14f);
        if (BenchStage.Instance != null) BenchStage.Instance.Watch(body, null);   // a screw sounds its own landing (OnCollisionEnter)
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOut || body == null || body.isKinematic) return;
        if (Time.time - lastBounceAt < .06f) return;
        lastBounceAt = Time.time;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < .04f) return;
        bool tray = BenchStage.Instance != null && BenchStage.Instance.InTray(transform.position);
        Sfx.Play(tray ? "screw.drop" : "screw.bounce", transform.position, Mathf.Clamp01(.4f + speed));
    }

    // ---------- the way back ----------

    /// <summary>Starts the fetch: from wherever it lies to hovering over its hole (the hold then screws it in).</summary>
    public void BeginFetch()
    {
        if (!IsOut || fetched || fetching) return;
        StartCoroutine(FetchRoutine());
    }

    private IEnumerator FetchRoutine()
    {
        fetching = true;
        IsBusy = true;
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
        if (body != null) { Destroy(body); body = null; }
        if (job != null) job.UnregisterDetached(gameObject);
        transform.SetParent(homeParent, true);
        Sfx.Play("screw.fetch", transform.position);
        Vector3 fromLocal = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        Vector3 hoverLocal = homeLocalPos + homeLocalRot * Vector3.up * (outSign * (threadLength + .004f));
        Quaternion hoverRot = homeLocalRot * Quaternion.AngleAxis(outSign * turns * 360f, Vector3.up);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(.05f, flyDuration);
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 p = Vector3.Lerp(fromLocal, hoverLocal, c);
            p += homeLocalRot * Vector3.up * (outSign * Mathf.Sin(c * Mathf.PI) * .02f);   // a small arc up and over
            transform.localPosition = p;
            transform.localRotation = Quaternion.Slerp(fromRot, hoverRot, c);
            yield return null;
        }
        Progress = 1f;
        lastTick = Mathf.FloorToInt(turns);
        fetched = true;
        fetching = false;
        IsBusy = false;
        // Drop the last 4 mm onto the hole's mouth: it starts the thread from here.
        transform.localPosition = homeLocalPos + homeLocalRot * Vector3.up * (outSign * threadLength);
        transform.localRotation = hoverRot;
    }

    private void Seat()
    {
        IsOut = false;
        fetched = false;
        Progress = 0f;
        lastTick = 0;
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        if (socket != null) socket.gameObject.SetActive(false);
        Sfx.Play("screw.seat", transform.position);
    }

    // ---------- the whole thing by itself (the labs, the guide, older callers) ----------

    /// <summary>Backs the screw all the way out and lets it fall (the bin argument is the old API; the stage decides where it lands).</summary>
    public void Unscrew(Transform bin)
    {
        if (!awoke) Awake();   // the editor's checks call in before Awake has run
        if (IsOut || IsBusy) return;
        if (!Application.isPlaying) { Free(); return; }   // the editor's checks: no frames to animate over
        StartCoroutine(SelfTurn(outward: true));
    }

    /// <summary>Fetches the screw to its hole and screws it home.</summary>
    public void Rescrew()
    {
        if (!IsOut || IsBusy) return;
        if (!Application.isPlaying)
        {
            if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
            if (body != null) { DestroyImmediate(body); body = null; }
            if (job != null) job.UnregisterDetached(gameObject);
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
        IsBusy = false;
    }

    private void OnDestroy()
    {
        if (socket != null) Destroy(socket.gameObject);
    }
}

// ---------------------------------------------------------------------------
// THE EMPTY HOLE a screw leaves: a dark ring where the head was, hoverable and holdable with the driver, which fetches the
// screw and screws it home. Made by the screw the first time it comes free; hidden when it is seated again.
// ---------------------------------------------------------------------------
public class ScrewSocket : BenchInteractable
{
    private Screw screw;
    private ScrewTarget target;
    static Material ringMaterial;

    public Screw Screw => screw;

    public static ScrewSocket Make(Screw screw, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(screw.name + " (hole)");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.layer = screw.gameObject.layer;
        float r = Mathf.Max(.002f, screw.HeadRadius);
        // The ring: a very flat cylinder, dark, sitting a hair proud of the surface so it shows.
        var ring = new GameObject("Ring");
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = Vector3.up * .0003f;
        ring.transform.localScale = new Vector3(r * 2f, .0003f, r * 2f);
        ring.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var mr = ring.AddComponent<MeshRenderer>();
        mr.sharedMaterial = RingMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // The hover: a box the size of the head over the hole. Thin and flush: the hover ray wants its top face only, and
        // anything proud of the surface is a step for the freed screw to land on and lean against.
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(r * 2.2f, .002f, r * 2.2f);
        box.center = Vector3.up * .001f;
        var socket = go.AddComponent<ScrewSocket>();
        socket.screw = screw;
        socket.target = screw.GetComponent<ScrewTarget>();
        return socket;
    }

    static Material RingMaterial
    {
        get
        {
            if (ringMaterial != null) return ringMaterial;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            ringMaterial = new Material(lit) { name = "Screw hole (runtime)", hideFlags = HideFlags.HideAndDontSave };
            ringMaterial.SetColor("_BaseColor", new Color(.09f, .08f, .07f));
            ringMaterial.SetFloat("_Smoothness", .1f);
            return ringMaterial;
        }
    }

    public override string DisplayName => "Screw hole";
    public override string Prompt => screw != null && screw.Fetched ? "Hold to screw in" : "Hold to screw in (the driver fetches the screw)";
    public override ToolType RequiredTool => ToolType.Screwdriver;
    public override bool CanInteract => screw != null && screw.IsOut && !screw.IsBusy && (target == null || target.PlateSeated);
    public override bool Holdable => true;
    public override float HoldProgress => screw != null ? 1f - screw.Progress : 0f;
    public override Vector3 WorkPoint => transform.position + transform.up * .002f;
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
