using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A COVER (the bench, v2; claude/bench-spec-v2.md §2.2)
//
// Seated, it is held by its screws. With them out, the pry tool is HELD at an edge: a creak, then the cover POPS up at
// that edge (a few degrees about the far edge). A popped cover is yours to move: press on it and it follows the cursor
// (any tool, or the bare hand); let go and it drops where it is, a rigidbody on the mat, the device or the tray; drag
// it back near its seat and let go and it SNAPS home. Putting it back is allowed with work left beneath, as before
// ("close it up anyway" is advice, not a gate). IsRemoved means off the device (in a hand or lying loose): a popped
// cover still covers what is under it.
//
// THE PRY AT THE SEAM (playtest 4, 10 Oct; the veteran: "the pry tool stands upright on the middle of the back like the
// screwdriver, instead of levering at the seam"). The cover knows its seam: the outline of the plate. Over a cover it can
// pry, the pry tool sits with its blade's edge in the seam nearest the cursor, its handle out across the seam and up at a
// pry angle, its blade flat to the cover (PryPose; ItemInspector puts the tool there). Held, the blade slides in under the
// edge and the handle levers down (ToolPickup's lever), and the cover pops about the opposite edge; popped, the blade
// stays under the lifted edge. The spec's words: "press and hold at a clip: the lever goes in, a short creak, the cover
// pops up at that edge".
//
// On a finished device (every fault fixed, back in one piece: JobBase.IsFinished) a seated cover offers nothing (no verb):
// there is nothing to undo. With its screws in it says why it won't come off ("Held on by its screws"), not just "Screws
// first" (the veteran on the screen's hint: a gate reads as arbitrary, a cause doesn't).
//
// Activate does the old whole thing by itself (the labs' hand, the Day 1 guide): seated or popped, it lifts off and
// lands on the mat; in a hand or loose, it snaps home.
// ---------------------------------------------------------------------------
public class RemovablePart : BenchInteractable
{
    [SerializeField] private Screw[] heldBy;
#pragma warning disable 0414
    [SerializeField] private int traySlot = 0;              // the old rig's slot; the mat decides now (kept for the prefabs)
#pragma warning restore 0414
    [SerializeField] private float moveDuration = 0.35f;

    [Tooltip("Faults beneath this part. Used only to word the prompt — this no longer refuses to close over unfinished work.")]
    [SerializeField] private GameObject[] faultsBeneath;

    [Tooltip("Parts beneath that ought to be replaced before this closes. Advisory, not a gate.")]
    [SerializeField] private ReplaceablePart[] replacementsBeneath;

    [SerializeField] private string partName = "Back cover";

    [Header("The pry (v2)")]
    [Tooltip("Seconds the pry tool is held at the edge before the cover pops.")]
    [SerializeField, Min(.1f)] private float pryDuration = .45f;
    [Tooltip("Degrees the popped edge lifts.")]
    [SerializeField, Range(2f, 20f)] private float popAngle = 7f;
    [Tooltip("Metres from its seat within which a dragged cover snaps home when let go.")]
    [SerializeField, Min(.005f)] private float snapDistance = .03f;

    /// <summary>The pry tool's angle up from the cover at the seam, degrees (a spudger goes in low).</summary>
    public const float PryAngle = 32f;
    /// <summary>How far the blade slides in under the edge as the hold begins, metres.</summary>
    public const float BladeIn = .003f;

    public enum CoverState { Seated, Popped, Held, Loose }
    public CoverState State { get; private set; } = CoverState.Seated;

    /// <summary>Off the device: in a hand, or lying loose. A popped cover still covers what is under it.</summary>
    public bool IsRemoved => State == CoverState.Held || State == CoverState.Loose;
    public bool IsPopped => State == CoverState.Popped;
    /// <summary>Popping up at the edge the pry went in at (the moment between the creak and the pop; nobody's target).</summary>
    public bool Popping => busy && State == CoverState.Seated && hasPryEdge;

    private bool busy;
    private float pryProgress;
    private Vector3 pryEdgeLocal;        // where the pry went in, in the device's frame
    private Vector3 pryAcrossLocal;      // the way out across the seam there, in the device's frame
    private Vector3 pryEdgeOnCover;      // the same two in the cover's own frame: they ride with it as it pops
    private Vector3 pryAcrossOnCover;
    private bool hasPryEdge;
    private Vector3 outwardLocal;        // the cover's outward normal, in the device's frame
    private Vector3 outwardOnCover;      // ...and in its own (it tilts with the cover when it pops)
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform homeParent;
    private JobBase job;
    private Rigidbody body;
    private Vector3 grabOffset;
    private float grabDepth;
    private float lastClackAt;

    private bool awoke;

    protected override void Awake()
    {
        if (awoke) return;
        awoke = true;
        base.Awake();
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        job = GetComponentInParent<JobBase>();
        // Which way is out: from the device's centre through the cover's centre, along the cover's thinnest axis.
        Vector3 deviceCentre = homeParent != null ? homeParent.position : transform.position - transform.up * .01f;
        Vector3 away = transform.position - deviceCentre;
        Vector3 thin = ThinAxisWorld();
        Vector3 outward = Vector3.Dot(away, thin) >= 0f ? thin : -thin;
        outwardLocal = homeParent != null ? homeParent.InverseTransformDirection(outward) : outward;
        outwardOnCover = transform.InverseTransformDirection(outward);

        // Tell each screw which plate it belongs to.
        if (heldBy != null) foreach (Screw s in heldBy)
        {
            if (s == null) continue;
            ScrewTarget t = s.GetComponent<ScrewTarget>();
            if (t != null) t.SetPlate(this);
        }
    }

    // The cover's thinnest world axis (a cover is a plate).
    private Vector3 ThinAxisWorld()
    {
        Vector3 s = transform.lossyScale;
        var box = GetComponent<BoxCollider>();
        if (box != null) s = Vector3.Scale(box.size, s);
        if (s.y <= s.x && s.y <= s.z) return transform.up;
        if (s.x <= s.z) return transform.right;
        return transform.forward;
    }

    private bool AllScrewsOut
    {
        get
        {
            if (heldBy != null) foreach (Screw s in heldBy)
                if (s != null && !s.IsOut) return false;
            return true;
        }
    }

    // Everything beneath is dealt with (grime destroys itself when clean).
    private bool WorkBeneathDone
    {
        get
        {
            foreach (GameObject f in faultsBeneath)
                if (f != null) return false;
            foreach (ReplaceablePart r in replacementsBeneath)
                if (r != null && !r.IsReplaced) return false;
            return true;
        }
    }

    // The device is finished: a seated cover has nothing to offer (the editor's checks build covers without Awake: no job).
    private bool DeviceFinished => job != null && job.IsFinished;

    public Vector3 Outward => homeParent != null ? homeParent.TransformDirection(outwardLocal).normalized : transform.up;
    public Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPos) : transform.position;

    public override bool CanInteract
    {
        get
        {
            if (busy) return false;
            if (State == CoverState.Seated) return AllScrewsOut;   // pry it
            return State != CoverState.Held;                          // lift a popped one, pick up a loose one
        }
    }

    public override string DisplayName => partName;

    public override string Prompt => State switch
    {
        CoverState.Seated => DeviceFinished ? "" : AllScrewsOut ? "Hold to pry it up" : "Held on by its screws. Take them out first",
        CoverState.Popped => "Lift it off (drag)",
        CoverState.Loose => WorkBeneathDone ? "Pick up (drag it home to refit)" : "Pick up (close it up anyway: drag it home)",
        _ => ""
    };

    public override ToolType RequiredTool => State == CoverState.Seated ? ToolType.Pry : ToolType.Hand;

    // ---------- the seam, and where the pry tool sits on it (playtest 4) ----------

    /// <summary>
    /// The point on this cover's seam nearest <paramref name="near"/>: on the plate's outline, a quarter of its thickness
    /// out from its middle (where the pry has always gone in), kept 6 mm in from the corners (a lever goes in along an
    /// edge, not at a corner); and the way out across the seam there, in the world.
    /// </summary>
    public void SeamAt(Vector3 near, out Vector3 seam, out Vector3 across)
    {
        Box(out Vector3 c, out Vector3 s);
        Vector3 scale = transform.lossyScale;
        var abs = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 half = Vector3.Scale(s, abs) * .5f;                       // metres along the box's own axes
        half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
        int thin = half.x <= half.y && half.x <= half.z ? 0 : half.y <= half.z ? 1 : 2;
        int a = thin == 0 ? 1 : 0, b = thin == 2 ? 1 : 2;
        Vector3 local = Vector3.Scale(transform.InverseTransformPoint(near) - c, abs);   // metres from the box's middle
        float pa = Mathf.Clamp(local[a], -half[a], half[a]), pb = Mathf.Clamp(local[b], -half[b], half[b]);
        Vector3 point = Vector3.zero, outward;
        if (half[a] - Mathf.Abs(pa) <= half[b] - Mathf.Abs(pb))
        {
            float side = pa >= 0f ? 1f : -1f;
            point[a] = side * half[a];
            point[b] = Mathf.Clamp(pb, -half[b] + Corner(half[b]), half[b] - Corner(half[b]));
            outward = Axis(a) * side;
        }
        else
        {
            float side = pb >= 0f ? 1f : -1f;
            point[b] = side * half[b];
            point[a] = Mathf.Clamp(pa, -half[a] + Corner(half[a]), half[a] - Corner(half[a]));
            outward = Axis(b) * side;
        }
        point[thin] = (Vector3.Dot(Outward, Axis(thin)) >= 0f ? 1f : -1f) * half[thin] * .5f;
        seam = transform.TransformPoint(c) + Axis(0) * point.x + Axis(1) * point.y + Axis(2) * point.z;
        across = outward.normalized;
    }

    /// <summary>
    /// Where the pry tool sits on this cover: its blade's edge in the seam (nearest <paramref name="near"/>, or where it went
    /// in), its handle out across the seam and up at <see cref="PryAngle"/>, its flat side's normal (the tool's +Z) out and
    /// down, so the lever turns the handle down toward the device and the blade's edge up. While the hold goes on the
    /// blade slides in under the edge; popped, it stays under the lifted edge (the cover's own frame carries it).
    /// False when the cover is off the device (in a hand or lying loose): the tool follows as any other.
    /// </summary>
    public bool PryPose(Vector3 near, out Vector3 tip, out Vector3 handle, out Vector3 flat, out Vector3 normal)
    {
        tip = handle = flat = normal = Vector3.zero;
        if (State == CoverState.Held || State == CoverState.Loose) return false;
        if (!awoke) Awake();
        Vector3 seam, across;
        if (hasPryEdge && (State == CoverState.Popped || pryProgress > 0f))
        {
            seam = transform.TransformPoint(pryEdgeOnCover);
            across = transform.TransformDirection(pryAcrossOnCover);
        }
        else SeamAt(near, out seam, out across);
        normal = transform.TransformDirection(outwardOnCover);
        normal = normal.sqrMagnitude > .25f ? normal.normalized : Outward;
        across = Vector3.ProjectOnPlane(across, normal);
        if (across.sqrMagnitude < 1e-8f) return false;
        across.Normalize();
        float into = State == CoverState.Popped ? 1f : Mathf.Clamp01(pryProgress / .3f);
        tip = seam - across * (BladeIn * into) - normal * (.0006f * into);
        float rise = PryAngle * Mathf.Deg2Rad;
        handle = (normal * Mathf.Sin(rise) + across * Mathf.Cos(rise)).normalized;
        flat = (across * Mathf.Sin(rise) - normal * Mathf.Cos(rise)).normalized;
        return true;
    }

    // The cover's solid in its own frame: its box collider (a cover is a plate), else its mesh's bounds, else a unit cube.
    private void Box(out Vector3 centre, out Vector3 size)
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) { centre = box.center; size = box.size; return; }
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) { centre = mf.sharedMesh.bounds.center; size = mf.sharedMesh.bounds.size; return; }
        centre = Vector3.zero;
        size = Vector3.one;
    }

    // The cover's own axes in the world, unit length (a mirrored scale flips its axis, as it flips the box).
    private Vector3 Axis(int i)
    {
        Vector3 scale = transform.lossyScale;
        return i == 0 ? transform.right * Mathf.Sign(scale.x) : i == 1 ? transform.up * Mathf.Sign(scale.y) : transform.forward * Mathf.Sign(scale.z);
    }

    private static float Corner(float half) => Mathf.Min(.006f, half * .5f);

    // Where the pry went in, kept in the device's frame (the pop's hinge, the tool's work point) and in the cover's own
    // (the tool's blade under the lifted edge).
    private void RememberPry(Vector3 edge, Vector3 across)
    {
        pryEdgeLocal = homeParent != null ? homeParent.InverseTransformPoint(edge) : edge;
        pryAcrossLocal = homeParent != null ? homeParent.InverseTransformDirection(across) : across;
        pryEdgeOnCover = transform.InverseTransformPoint(edge);
        pryAcrossOnCover = transform.InverseTransformDirection(across);
        hasPryEdge = true;
    }

    // ---------- the pry (a hold) ----------

    public override bool Holdable => State == CoverState.Seated;
    public override float HoldProgress => pryProgress;
    public override Vector3 WorkPoint => State == CoverState.Seated && homeParent != null && pryEdgeLocal != Vector3.zero
        ? homeParent.TransformPoint(pryEdgeLocal) : BoundsCentre(transform) + Outward * .002f;
    public override Vector3 WorkNormal => Outward;

    public override void HoldBegin(BenchHand hand)
    {
        if (State != CoverState.Seated) return;
        // The seam nearest the press: the pry goes in there, and that side lifts.
        Vector3 at = hand.hitSomething ? hand.hit.point : BoundsCentre(transform);
        SeamAt(at, out Vector3 edge, out Vector3 across);
        RememberPry(edge, across);
        if (pryProgress <= 0f) Sfx.Play("cover.creak", edge);
    }

    public override bool HoldTick(BenchHand hand)
    {
        if (State != CoverState.Seated) return true;
        pryProgress = Mathf.Clamp01(pryProgress + hand.deltaTime / pryDuration);
        if (pryProgress < 1f) return false;
        StartCoroutine(Pop());
        return true;
    }

    public override void HoldEnd(BenchHand hand, bool finished)
    {
        if (!finished) pryProgress = Mathf.Max(0f, pryProgress - .35f);   // the lever slips back a little when let go
    }

    // How far the cover reaches from its centre along a world direction.
    private float ExtentAlong(Vector3 direction)
    {
        var box = GetComponent<BoxCollider>();
        Vector3 size = box != null ? Vector3.Scale(box.size, transform.lossyScale) : transform.lossyScale;
        Vector3 local = transform.InverseTransformDirection(direction.normalized);
        return Mathf.Abs(local.x) * size.x * .5f + Mathf.Abs(local.y) * size.y * .5f + Mathf.Abs(local.z) * size.z * .5f;
    }

    private IEnumerator Pop()
    {
        busy = true;
        Vector3 centre = BoundsCentre(transform);
        Vector3 n = Outward;
        Vector3 edge = homeParent != null ? homeParent.TransformPoint(pryEdgeLocal) : centre;
        // About the edge opposite the seam the pry went in at (playtest 4: the seam's own outward side; an edge's middle
        // gives the same hinge as before, a press near a corner no longer turns it about a diagonal).
        Vector3 acrossWorld = homeParent != null ? homeParent.TransformDirection(pryAcrossLocal) : pryAcrossLocal;
        Vector3 d = hasPryEdge && Vector3.ProjectOnPlane(acrossWorld, n).sqrMagnitude > 1e-8f
            ? Vector3.ProjectOnPlane(acrossWorld, n).normalized : Vector3.ProjectOnPlane(edge - centre, n).normalized;
        Vector3 hinge = centre - d * ExtentAlong(d);       // the far edge stays where it is
        Vector3 axis = Vector3.Cross(d, n).normalized;      // turning about this lifts the pried edge outward
        Quaternion from = transform.rotation;
        Vector3 fromPos = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .15f;
            float c = Mathf.Clamp01(t);
            float angle = popAngle * (1f - Mathf.Pow(1f - c, 3f)) * (1f + .25f * Mathf.Sin(c * Mathf.PI));   // overshoots a touch
            Quaternion turn = Quaternion.AngleAxis(angle, axis);
            transform.rotation = turn * from;
            transform.position = hinge + turn * (fromPos - hinge);
            yield return null;
        }
        Quaternion settled = Quaternion.AngleAxis(popAngle, axis);
        transform.rotation = settled * from;
        transform.position = hinge + settled * (fromPos - hinge);
        State = CoverState.Popped;
        pryProgress = 0f;
        busy = false;
        Sfx.Play("cover.pop", edge);
    }

    // ---------- the lift, the drop, the way home (a grab) ----------

    public override bool Grabbable => State == CoverState.Popped || State == CoverState.Loose;

    public override void GrabBegin(BenchHand hand)
    {
        if (!Grabbable || busy) return;
        Camera cam = hand.camera;
        Vector3 at = hand.hitSomething ? hand.hit.point : transform.position;
        grabOffset = transform.position - at;
        grabDepth = cam != null ? Vector3.Dot(at - cam.transform.position, cam.transform.forward) : .5f;
        Detach();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        if (BenchStage.Instance != null) BenchStage.Instance.Forget(body);
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        State = CoverState.Held;
        Sfx.Play("cover.lift", transform.position);
    }

    public override void GrabMove(BenchHand hand)
    {
        if (State != CoverState.Held) return;
        Camera cam = hand.camera;
        Vector3 target;
        if (cam != null)
        {
            // Along the cursor's ray, at the depth it was picked up at, and 3 cm toward the camera so it clears what it lay on
            // and the tray's walls (a cover dropped in the tray is fine).
            target = hand.PointAtDepth(Mathf.Max(.05f, grabDepth - .03f)) + grabOffset;
        }
        else target = transform.position;
        float k = 1f - Mathf.Exp(-18f * hand.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, k);
        // In the hand it turns to sit the way it sits on the device (a cover picked up off the mat lies flat; its seat
        // may be face-on to the camera): by the time it is over its seat it is the right way round.
        Quaternion home = homeParent != null ? homeParent.rotation * homeLocalRot : homeLocalRot;
        transform.rotation = Quaternion.Slerp(transform.rotation, home, 1f - Mathf.Exp(-8f * hand.deltaTime));
    }

    public override void GrabEnd(BenchHand hand)
    {
        if (State != CoverState.Held) return;
        if (NearHome(hand.camera)) StartCoroutine(SnapHome());
        else Drop();
    }

    // Over its seat, as the player sees it (the held cover slid along its sight line to the seat's depth), and near enough
    // the right way round.
    private bool NearHome(Camera camera)
    {
        Vector3 at = ReplaceablePart.AtDepthOf(transform.position, HomePosition, camera);
        if (Vector3.Distance(at, HomePosition) > snapDistance) return false;
        Quaternion home = homeParent != null ? homeParent.rotation * homeLocalRot : homeLocalRot;
        return Quaternion.Angle(transform.rotation, home) < 30f;
    }

    private void Detach()
    {
        if (transform.parent == null) return;
        transform.SetParent(null, true);
        if (job != null) job.RegisterDetached(gameObject);
    }

    private void Drop()
    {
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = .02f;
        body.linearDamping = .2f;
        body.angularDamping = .5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        State = CoverState.Loose;
        if (BenchStage.Instance != null) BenchStage.Instance.Watch(body, null);   // a cover sounds its own landing (OnCollisionEnter)
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (State != CoverState.Loose || body == null || body.isKinematic) return;
        if (Time.time - lastClackAt < .12f || collision.relativeVelocity.magnitude < .05f) return;
        lastClackAt = Time.time;
        Sfx.Play("cover.clack", transform.position, Mathf.Clamp01(.5f + collision.relativeVelocity.magnitude));
    }

    private IEnumerator SnapHome()
    {
        busy = true;
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
        if (body != null) { Destroy(body); body = null; }
        transform.SetParent(homeParent, true);
        if (job != null) job.UnregisterDetached(gameObject);
        Vector3 from = transform.localPosition;
        Quaternion rotFrom = transform.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .12f;
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localPosition = Vector3.Lerp(from, homeLocalPos, c);
            transform.localRotation = Quaternion.Slerp(rotFrom, homeLocalRot, c);
            yield return null;
        }
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        State = CoverState.Seated;
        pryProgress = 0f;
        hasPryEdge = false;      // home again: the next pry goes in wherever it is pressed
        busy = false;
        Sfx.Play("part.on", transform.position);
        // Screws are NOT auto-returned — the player screws each one in again.
    }

    // ---------- the whole thing by itself (the labs' hand, the Day 1 guide, older callers) ----------

    public override void Activate()
    {
        if (!awoke) Awake();
        if (busy) return;
        if (!Application.isPlaying)
        {
            // The editor's checks: off or on at once, no frames to animate over.
            if (State == CoverState.Seated && !AllScrewsOut) return;
            if (State == CoverState.Seated || State == CoverState.Popped) { Detach(); State = CoverState.Loose; }
            else if (State == CoverState.Loose)
            {
                transform.SetParent(homeParent, false);
                transform.localPosition = homeLocalPos;
                transform.localRotation = homeLocalRot;
                if (job != null) job.UnregisterDetached(gameObject);
                State = CoverState.Seated;
                hasPryEdge = false;
            }
            return;
        }
        if (State == CoverState.Seated || State == CoverState.Popped) StartCoroutine(LiftOffBySelf());
        else if (State == CoverState.Loose) StartCoroutine(SnapHome());
    }

    private IEnumerator LiftOffBySelf()
    {
        if (State == CoverState.Seated)
        {
            if (!AllScrewsOut) yield break;
            // The seam at the cover's forward end, as the self-driven lift always pried it.
            Vector3 centre = BoundsCentre(transform);
            SeamAt(centre + Vector3.ProjectOnPlane(transform.forward, Outward).normalized * ExtentAlong(transform.forward), out Vector3 edge, out Vector3 across);
            RememberPry(edge, across);
            yield return Pop();
        }
        busy = true;
        Detach();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        State = CoverState.Held;
        Sfx.Play("cover.lift", transform.position);
        Vector3 from = transform.position;
        Quaternion rotFrom = transform.rotation;
        Vector3 to = BenchStage.Instance != null ? BenchStage.Instance.FreeSpotOnMat(.1f) : from + Vector3.up * .05f;
        Quaternion rotTo = Quaternion.LookRotation(Vector3.ProjectOnPlane(rotFrom * Vector3.forward, Vector3.up).sqrMagnitude > .01f
            ? Vector3.ProjectOnPlane(rotFrom * Vector3.forward, Vector3.up) : Vector3.forward, Vector3.up);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float c = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, to, c);
            p.y += Mathf.Sin(c * Mathf.PI) * .05f;
            transform.position = p;
            transform.rotation = Quaternion.Slerp(rotFrom, rotTo, c);
            yield return null;
        }
        busy = false;
        Drop();
    }

    private void OnDestroy()
    {
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
    }
}
