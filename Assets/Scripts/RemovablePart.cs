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

    public enum CoverState { Seated, Popped, Held, Loose }
    public CoverState State { get; private set; } = CoverState.Seated;

    /// <summary>Off the device: in a hand, or lying loose. A popped cover still covers what is under it.</summary>
    public bool IsRemoved => State == CoverState.Held || State == CoverState.Loose;
    public bool IsPopped => State == CoverState.Popped;

    private bool busy;
    private float pryProgress;
    private Vector3 pryEdgeLocal;        // where the pry went in, in the device's frame
    private Vector3 outwardLocal;        // the cover's outward normal, in the device's frame
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
        CoverState.Seated => AllScrewsOut ? "Hold to pry it up" : "Screws first",
        CoverState.Popped => "Lift it off (drag)",
        CoverState.Loose => WorkBeneathDone ? "Pick up (drag it home to refit)" : "Pick up (close it up anyway: drag it home)",
        _ => ""
    };

    public override ToolType RequiredTool => State == CoverState.Seated ? ToolType.Pry : ToolType.Hand;

    // ---------- the pry (a hold) ----------

    public override bool Holdable => State == CoverState.Seated;
    public override float HoldProgress => pryProgress;
    public override Vector3 WorkPoint => State == CoverState.Seated && homeParent != null && pryEdgeLocal != Vector3.zero
        ? homeParent.TransformPoint(pryEdgeLocal) : BoundsCentre(transform) + Outward * .002f;
    public override Vector3 WorkNormal => Outward;

    public override void HoldBegin(BenchHand hand)
    {
        if (State != CoverState.Seated) return;
        // The edge nearest the press: the pry goes in there, and that side lifts.
        Vector3 centre = BoundsCentre(transform);
        Vector3 at = hand.hitSomething ? hand.hit.point : centre;
        Vector3 d = Vector3.ProjectOnPlane(at - centre, Outward);
        if (d.sqrMagnitude < 1e-8f) d = Vector3.ProjectOnPlane(transform.forward, Outward);
        d.Normalize();
        Vector3 edge = centre + d * ExtentAlong(d) + Outward * (ExtentAlong(Outward) * .5f);
        pryEdgeLocal = homeParent != null ? homeParent.InverseTransformPoint(edge) : edge;
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
        Vector3 d = Vector3.ProjectOnPlane(edge - centre, n).normalized;
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
            // Along the cursor's ray, at the depth it was picked up at, and 15 mm toward the camera so it clears what it lay on.
            float depth = Mathf.Max(.05f, grabDepth - .015f);
            float along = depth / Mathf.Max(.2f, Vector3.Dot(hand.ray.direction, cam.transform.forward));
            target = hand.ray.origin + hand.ray.direction * along + grabOffset;
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
        if (BenchStage.Instance != null) BenchStage.Instance.Watch(body, "cover.clack");
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
            Vector3 centre = BoundsCentre(transform);
            pryEdgeLocal = homeParent != null ? homeParent.InverseTransformPoint(centre + Vector3.ProjectOnPlane(transform.forward, Outward).normalized * ExtentAlong(transform.forward)) : centre;
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
