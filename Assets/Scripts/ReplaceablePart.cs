using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A PART THAT IS REPLACED (the bench, v2; claude/bench-spec-v2.md §2.3)
//
// The broken part sits in its seat under the cover. Hold the tweezers on it: they PINCH, and the part lifts out and
// follows the cursor with a wobble while the button stays down; let go and it drops where it is (debris on the mat).
// The fresh part waits in the tray from the moment the device is first worked on (LoosePart, below): pinch it the same
// way, carry it over the empty seat and let go: it SEATS with a snap. Only then is the part replaced (the grade can't
// jump early; it used to flip on one click).
//
// Activate does the whole swap by itself (the labs' hand, the Day 1 guide): the broken part goes to the tray, the fresh
// one seats.
// ---------------------------------------------------------------------------
public class ReplaceablePart : BenchInteractable
{
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject freshVisual;
    [SerializeField] private RemovablePart coveredBy;
    [SerializeField] private string partName = "Cracked screen";

    [Header("The tweezers (v2)")]
    [Tooltip("Seconds the tweezers are held before the pinch takes and the part lifts.")]
    [SerializeField, Min(.05f)] private float pinchDuration = .25f;
    [Tooltip("Metres from the seat within which the fresh part, let go, seats.")]
    [SerializeField, Min(.004f)] private float seatDistance = .016f;

    public enum PartState { InPlace, HeldBroken, Removed, Replaced }
    public PartState State { get; private set; } = PartState.InPlace;
    public bool IsReplaced { get; private set; }
    public LoosePart Fresh => fresh;
    public RemovablePart CoveredBy => coveredBy;

    private float pinch;
    private Transform seatParent;
    private Vector3 seatLocalPos;
    private Quaternion seatLocalRot;
    private Vector3 freshLocalPos;
    private Quaternion freshLocalRot;
    private Vector3 freshLocalScale;
    private Transform freshParent;
    private JobBase job;
    private LoosePart fresh;
    private Rigidbody body;
    private Vector3 grabOffset;
    private float grabDepth, wobble;
    private bool busy;

    private bool awoke;

    protected override void Awake()
    {
        if (awoke) return;
        awoke = true;
        base.Awake();
        job = GetComponentInParent<JobBase>();
        seatParent = transform.parent;
        seatLocalPos = transform.localPosition;
        seatLocalRot = transform.localRotation;
        if (brokenVisual != null) brokenVisual.SetActive(true);
        if (freshVisual != null)
        {
            Transform f = freshVisual.transform;
            if (seatParent != null && (f.parent == transform || f.parent != null && f.parent.IsChildOf(transform)))
            {
                // The fresh visual is authored as a child of the broken part (Grace's camera: the working blade under the
                // jammed mechanism). The broken part leaves the device when it is pinched out, so the fresh one must seat
                // into the device, not into the scrap in the tray: its seat is remembered in the part's parent's frame.
                freshParent = seatParent;
                freshLocalPos = seatParent.InverseTransformPoint(f.position);
                freshLocalRot = Quaternion.Inverse(seatParent.rotation) * f.rotation;
                Vector3 ls = f.lossyScale, ps = seatParent.lossyScale;
                freshLocalScale = new Vector3(ls.x / Mathf.Max(1e-6f, ps.x), ls.y / Mathf.Max(1e-6f, ps.y), ls.z / Mathf.Max(1e-6f, ps.z));
            }
            else
            {
                freshParent = f.parent;
                freshLocalPos = f.localPosition;
                freshLocalRot = f.localRotation;
                freshLocalScale = f.localScale;
            }
            freshVisual.SetActive(false);
        }
        // Which way is out of the device from this seat: from the device's middle through the part, along the part's
        // thinnest axis (the tweezers come in from that side; the cover it sits under may be on the other face).
        Transform device = job != null ? job.transform : seatParent;
        Vector3 away = transform.position - (device != null ? device.position : transform.position - transform.up * .01f);
        Vector3 thin = ThinAxisWorld();
        Vector3 outward = Vector3.Dot(away, thin) >= 0f ? thin : -thin;
        outwardLocal = seatParent != null ? seatParent.InverseTransformDirection(outward) : outward;
    }

    private Vector3 outwardLocal = Vector3.up;

    private Vector3 ThinAxisWorld()
    {
        Vector3 s = transform.lossyScale;
        var box = GetComponent<BoxCollider>();
        if (box != null) s = Vector3.Scale(box.size, s);
        if (s.y <= s.x && s.y <= s.z) return transform.up;
        if (s.x <= s.z) return transform.right;
        return transform.forward;
    }

    /// <summary>The seat's centre in the world (where the fresh part goes).</summary>
    public Vector3 SeatPosition => seatParent != null ? seatParent.TransformPoint(seatLocalPos) : transform.position;
    public Vector3 Outward => seatParent != null ? seatParent.TransformDirection(outwardLocal).normalized : transform.up;
    private bool Uncovered => coveredBy == null || coveredBy.IsRemoved;

    // Only reachable once whatever covers it has been lifted off.
    public override bool CanInteract => !IsReplaced && !busy && State == PartState.InPlace && Uncovered;

    public override string DisplayName => partName;
    public override string Prompt => IsReplaced ? "Already replaced"
        : State == PartState.Removed ? "Out (seat the fresh part from the tray)"
        : State == PartState.HeldBroken ? ""
        : coveredBy != null && !coveredBy.IsRemoved ? $"Remove the {coveredBy.DisplayName.ToLowerInvariant()} first"
        : "Hold to pinch it out";
    public override ToolType RequiredTool => ToolType.Tweezers;
    public override bool Holdable => true;
    public override float HoldProgress => State == PartState.InPlace ? pinch : 1f;
    public override Vector3 WorkPoint => BoundsCentre(transform) + Outward * .002f;
    public override Vector3 WorkNormal => Outward;

    /// <summary>The fresh part is put in the tray the first time the device is worked on.</summary>
    public void PresentFresh()
    {
        if (!awoke) Awake();   // the editor's checks call in before Awake has run
        if (fresh != null || freshVisual == null || IsReplaced) return;
        fresh = LoosePart.Make(this, freshVisual);
        // Stock, not a piece of the device: it never stops the device being handed back (the gate is reassembly).
        if (job != null) job.RegisterLoose(freshVisual);
    }

    // ---------- the broken part: pinch, lift, drop ----------

    public override void HoldBegin(BenchHand hand)
    {
        if (State != PartState.InPlace) return;
        Vector3 at = hand.hitSomething ? hand.hit.point : transform.position;
        grabOffset = transform.position - at;
        grabDepth = hand.camera != null ? Vector3.Dot(at - hand.camera.transform.position, hand.camera.transform.forward) : .5f;
        if (pinch <= 0f) Sfx.Play("part.pinch", at);
    }

    public override bool HoldTick(BenchHand hand)
    {
        if (State == PartState.InPlace)
        {
            pinch = Mathf.Clamp01(pinch + hand.deltaTime / pinchDuration);
            if (pinch < 1f) return false;
            Lift();
        }
        if (State == PartState.HeldBroken) Follow(hand);
        return false;      // the hold goes on until the button is let go
    }

    public override void HoldEnd(BenchHand hand, bool finished)
    {
        if (State == PartState.HeldBroken) Drop();
        else if (State == PartState.InPlace) pinch = 0f;
    }

    private void Lift()
    {
        transform.SetParent(null, true);
        // Out of its seat the device has a hole in it: not handable until the fresh part is in (then this is scrap).
        if (job != null) job.RegisterDetached(gameObject);
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        State = PartState.HeldBroken;
        wobble = 1f;
        Sfx.Play("part.lift", transform.position);
    }

    private void Follow(BenchHand hand)
    {
        Camera cam = hand.camera;
        if (cam == null) return;
        float depth = Mathf.Max(.05f, grabDepth - .02f);
        float along = depth / Mathf.Max(.2f, Vector3.Dot(hand.ray.direction, cam.transform.forward));
        Vector3 target = hand.ray.origin + hand.ray.direction * along + grabOffset;
        float k = 1f - Mathf.Exp(-16f * hand.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, k);
        // The wobble of a thing held in tweezers: strongest just after it lifts, then settling.
        wobble = Mathf.Max(0f, wobble - hand.deltaTime * 1.4f);
        float w = Mathf.Sin(Time.time * 42f) * 6f * wobble;
        transform.rotation = Quaternion.AngleAxis(w, cam.transform.forward) * transform.rotation;
    }

    private void Drop()
    {
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = .01f;
        body.linearDamping = .25f;
        body.angularDamping = .6f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        State = PartState.Removed;
        if (BenchStage.Instance != null) BenchStage.Instance.Watch(body, "part.drop");
        Sfx.Play("part.drop", transform.position);
    }

    // ---------- the fresh part seats ----------

    /// <summary>
    /// True when the fresh part, held at <paramref name="freshPosition"/>, is over the empty seat. Over, as the player
    /// sees it: the held part rides at the depth it was picked up at (the tray is lower and nearer than the device), so it
    /// is slid along its own sight line to the seat's depth before the distance is measured; let go with the cursor on
    /// the seat, it seats.
    /// </summary>
    public bool SeatWouldTake(Vector3 freshPosition, Camera camera = null)
    {
        if (State != PartState.Removed || IsReplaced || !Uncovered) return false;
        return Vector3.Distance(AtDepthOf(freshPosition, SeatPosition, camera), SeatPosition) <= seatDistance;
    }

    /// <summary>A held point slid along its sight line to another point's depth (the camera's forward).</summary>
    public static Vector3 AtDepthOf(Vector3 held, Vector3 target, Camera camera)
    {
        if (camera == null) camera = Camera.main;
        if (camera == null) return held;
        Transform c = camera.transform;
        Vector3 h = c.InverseTransformPoint(held);
        Vector3 t = c.InverseTransformPoint(target);
        if (h.z <= 1e-3f || t.z <= 1e-3f) return held;
        return c.TransformPoint(h * (t.z / h.z));
    }

    /// <summary>The fresh part goes into the seat: the part is replaced.</summary>
    public void SeatFresh()
    {
        if (!awoke) Awake();
        if (IsReplaced || freshVisual == null) return;
        if (fresh != null) fresh.Seated();
        if (job != null)
        {
            job.UnregisterLoose(freshVisual);
            job.RegisterLoose(gameObject);      // the broken part is scrap now: owned for cleanup, no longer a hole
        }
        Transform f = freshVisual.transform;
        f.SetParent(freshParent, false);
        f.localPosition = freshLocalPos;
        f.localRotation = freshLocalRot;
        f.localScale = freshLocalScale;
        freshVisual.SetActive(true);
        IsReplaced = true;
        State = PartState.Replaced;
        Juice.Sparkle(f.position);   // in it goes: a little burst of sparks
        Sfx.Play("part.replace", f.position);   // "pull-out click, then magnetic snap"
        // Grace's camera: its new shutter fires once, so you hear the camera work again.
        GraceCameraRepairJob camera = (freshParent != null ? freshParent.GetComponentInParent<GraceCameraRepairJob>() : null);
        if (camera != null && camera.Shutter == this) Sfx.PlayLater("camera.shutter", f.position, .45f);
    }

    // ---------- the whole swap by itself (the labs' hand, the Day 1 guide, older callers) ----------

    public override void Activate()
    {
        if (!awoke) Awake();
        if (!CanInteract) return;
        if (!Application.isPlaying) { PresentFresh(); SeatFresh(); return; }   // the editor's checks: no frames to animate over
        StartCoroutine(SwapBySelf());
    }

    private IEnumerator SwapBySelf()
    {
        busy = true;
        PresentFresh();
        Lift();
        // Out and over to the tray, then the fresh one in.
        Vector3 from = transform.position;
        Vector3 to = BenchStage.Instance != null ? BenchStage.Instance.TrayDropPoint() : from + Vector3.up * .05f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .35f;
            float c = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, to, c);
            p.y += Mathf.Sin(c * Mathf.PI) * .05f;
            transform.position = p;
            yield return null;
        }
        Drop();
        yield return new WaitForSeconds(.15f);
        if (fresh != null) yield return fresh.FlyToSeat();
        SeatFresh();
        busy = false;
    }

    private void OnDestroy()
    {
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
    }
}

// ---------------------------------------------------------------------------
// THE FRESH PART IN THE TRAY: pinched with the tweezers, carried, and seated when let go over the empty seat; let go
// anywhere else, it drops where it is (the stage catches it if it leaves the mat).
// ---------------------------------------------------------------------------
public class LoosePart : BenchInteractable
{
    private ReplaceablePart part;
    private Rigidbody body;
    private bool held, seated;
    private float pinch, wobble;
    private Vector3 grabOffset;
    private float grabDepth;

    public static LoosePart Make(ReplaceablePart part, GameObject freshVisual)
    {
        freshVisual.transform.SetParent(null, true);
        freshVisual.SetActive(true);
        if (freshVisual.GetComponent<Collider>() == null)
        {
            var box = freshVisual.AddComponent<BoxCollider>();
            var mf = freshVisual.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
        }
        var loose = freshVisual.GetComponent<LoosePart>();
        if (loose == null) loose = freshVisual.AddComponent<LoosePart>();
        loose.part = part;
        loose.body = freshVisual.GetComponent<Rigidbody>();
        if (loose.body == null) loose.body = freshVisual.AddComponent<Rigidbody>();
        loose.body.mass = .01f;
        loose.body.linearDamping = .25f;
        loose.body.angularDamping = .6f;
        loose.body.interpolation = RigidbodyInterpolation.Interpolate;
        loose.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        loose.body.isKinematic = false;
        loose.body.useGravity = true;
        if (BenchStage.Instance != null)
        {
            // Laid in the tray the long way (a 12 cm screen in a 16 cm tray, dropped at any angle, caught a wall and slid
            // out, 7 Oct), a touch askew, from just above the floor.
            BenchStage stage = BenchStage.Instance;
            Vector3 extents = loose.GetComponent<Collider>() is BoxCollider b ? Vector3.Scale(b.size, freshVisual.transform.lossyScale) : freshVisual.transform.lossyScale;
            Vector3 longLocal = Mathf.Abs(extents.x) >= Mathf.Abs(extents.z) ? Vector3.right : Vector3.forward;
            Quaternion lay = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up)
                             * Quaternion.FromToRotation(longLocal, Vector3.ProjectOnPlane(stage.TrayLongAxis, Vector3.up).normalized);
            Vector3 at = stage.TrayDropPoint(.012f, .01f);
            // Through the rigidbody as well as the transform: an interpolating body snaps the transform back to its own pose
            // the next frame, and the part was left inside the device, shoved out and dropped on the mat (7 Oct).
            freshVisual.transform.SetPositionAndRotation(at, lay);
            loose.body.position = at;
            loose.body.rotation = lay;
            loose.body.linearVelocity = Vector3.zero;
            loose.body.angularVelocity = Vector3.zero;
            stage.Watch(loose.body, "part.drop");
        }
        return loose;
    }

    public override string DisplayName => part != null ? "Fresh " + part.DisplayName.ToLowerInvariant() : "Fresh part";
    public override string Prompt => part != null && part.State == ReplaceablePart.PartState.Removed
        ? "Hold to pinch it up, carry it to the seat" : "Hold to pick it up (the broken one comes out first)";
    public override ToolType RequiredTool => ToolType.Tweezers;
    public override bool CanInteract => !seated && !held && part != null && !part.IsReplaced;
    public override bool Holdable => true;
    public override float HoldProgress => held ? 1f : pinch;
    public override Vector3 WorkNormal => Vector3.up;

    public override void HoldBegin(BenchHand hand)
    {
        Vector3 at = hand.hitSomething ? hand.hit.point : transform.position;
        grabOffset = transform.position - at;
        grabDepth = hand.camera != null ? Vector3.Dot(at - hand.camera.transform.position, hand.camera.transform.forward) : .5f;
        if (pinch <= 0f) Sfx.Play("part.pinch", at);
    }

    public override bool HoldTick(BenchHand hand)
    {
        if (!held)
        {
            pinch = Mathf.Clamp01(pinch + hand.deltaTime / .2f);
            if (pinch < 1f) return false;
            held = true;
            wobble = 1f;
            if (BenchStage.Instance != null) BenchStage.Instance.Forget(body);
            body.isKinematic = true;
            body.useGravity = false;
            Sfx.Play("part.lift", transform.position);
        }
        Camera cam = hand.camera;
        if (cam != null)
        {
            float depth = Mathf.Max(.05f, grabDepth - .02f);
            float along = depth / Mathf.Max(.2f, Vector3.Dot(hand.ray.direction, cam.transform.forward));
            Vector3 target = hand.ray.origin + hand.ray.direction * along + grabOffset;
            float k = 1f - Mathf.Exp(-16f * hand.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            wobble = Mathf.Max(0f, wobble - hand.deltaTime * 1.4f);
            transform.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 42f) * 6f * wobble, cam.transform.forward) * transform.rotation;
        }
        // Near the seat: a hint in the outline's colour is the tool-in-hand's job; here only the seat decides on release.
        return false;
    }

    public override void HoldEnd(BenchHand hand, bool finished)
    {
        pinch = 0f;
        if (!held) return;
        held = false;
        if (part != null && part.SeatWouldTake(transform.position, hand.camera)) { part.SeatFresh(); return; }
        body.isKinematic = false;
        body.useGravity = true;
        if (BenchStage.Instance != null) BenchStage.Instance.Watch(body, "part.drop");
        Sfx.Play("part.drop", transform.position);
    }

    /// <summary>The part has been seated: this behaviour's work is done (the fresh visual stays as the part).</summary>
    public void Seated()
    {
        seated = true;
        held = false;
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        // The fresh visual is the part now; nothing more to pick up. (The editor's checks seat parts in edit mode.)
        if (Application.isPlaying) { if (body != null) Destroy(body); Destroy(this); }
        else { if (body != null) DestroyImmediate(body); DestroyImmediate(this); }
    }

    /// <summary>The legacy swap: the fresh part flies from the tray to its seat.</summary>
    public IEnumerator FlyToSeat()
    {
        if (part == null) yield break;
        if (BenchStage.Instance != null && body != null) BenchStage.Instance.Forget(body);
        if (body != null) { body.isKinematic = true; body.useGravity = false; }
        Vector3 from = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .3f;
            float c = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(from, part.SeatPosition, c);
            p.y += Mathf.Sin(c * Mathf.PI) * .05f;
            transform.position = p;
            yield return null;
        }
    }

    public override void Activate()
    {
        // A press without a hold (the labs): carry it straight to the seat if the seat is empty.
        if (part != null && part.State == ReplaceablePart.PartState.Removed) StartCoroutine(SeatBySelf());
    }

    private IEnumerator SeatBySelf()
    {
        yield return FlyToSeat();
        if (part != null) part.SeatFresh();
    }
}
