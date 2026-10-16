using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A COVER (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.2)
//
// Seated, it is held by its screws. With them out, the pry tool is HELD at an edge: a creak, then the cover POPS up at that
// edge (a few degrees about the far edge). That pop is the physical moment kept (his decision 1: "taking apart stays physical
// and under your hand ... the cover pops where you pried"). Everything after it is automatic: a beat at the popped angle
// (0.15 s), a lift off along its outward side (0.10 s, cover.lift), and it flies to the smaller well of the mat it fits, outer
// face up, landing with cover.clack. No drag, no drop, no physics: v2's "a popped cover is yours to move" let it land on the
// screws, slide off the mat, or be dropped anywhere ("it was going through the table"). One input for the whole cover.
//
// BACK ON. A press on the cover in its well (any tool, or the bare hand) sends it home: it flies to its seat and clips on
// (part.on). With this job's work still under it, the press must be held 0.4 s ("close it anyway": a stray click from the
// fresh-part pad beside the well can never close the device over unfinished work; hand-back needs it in one piece, so closing
// anyway must exist). With its seat facing down, nothing happens and the hover says to flip it over first.
//
// THE PRY AT THE SEAM (playtest 4, 10 Oct; the veteran: "the pry tool stands upright on the middle of the back like the
// screwdriver, instead of levering at the seam"). The cover knows its seam: the outline of the plate. Over a cover it can
// pry, the pry tool sits with its blade's edge in the seam nearest the cursor, its handle out across the seam and up at a
// pry angle, its blade flat to the cover (PryPose; ItemInspector puts the tool there). Held, the blade slides in under the
// edge and the handle levers down (ToolPickup's lever), and the cover pops about the opposite edge.
//
// On a finished device (every fault fixed, back in one piece: JobBase.IsFinished) a seated cover offers nothing (no verb):
// there is nothing to undo. With its screws in it says why it won't come off ("Held on by its screws").
//
// Activate does the whole thing by itself (the labs' hand, the Day 1 guide): seated, a self-pry, the pop, the beat and the
// flight to its well; in its well, it flies home. In edit mode (the editor's checks: no frames) every flight completes at
// once, with the same states and the same places.
//
// States: Seated (on the device), Popped (just popped, still covering), Held (flying: to its well or home; off the device),
// Loose (in its well). IsRemoved: off the device (flying or in its well).
// ---------------------------------------------------------------------------
public class RemovablePart : BenchInteractable
{
    [SerializeField] private Screw[] heldBy;

    [Tooltip("Faults beneath this part: the checklist opens it for them; the press home needs a short hold while they're left.")]
    [SerializeField] private GameObject[] faultsBeneath;

    [Tooltip("Parts beneath that ought to be replaced before this closes: the press home needs a short hold while they're left.")]
    [SerializeField] private ReplaceablePart[] replacementsBeneath;

    [SerializeField] private string partName = "Back cover";

    [Header("The pry (v2)")]
    [Tooltip("Seconds the pry tool is held at the edge before the cover pops.")]
    [SerializeField, Min(.1f)] private float pryDuration = .45f;
    [Tooltip("Degrees the popped edge lifts.")]
    [SerializeField, Range(2f, 20f)] private float popAngle = 7f;

    /// <summary>The pry tool's angle up from the cover at the seam, degrees (a spudger goes in low).</summary>
    public const float PryAngle = 32f;
    /// <summary>How far the blade slides in under the edge as the hold begins, metres.</summary>
    public const float BladeIn = .003f;
    /// <summary>After the pop: the beat at the popped angle, the lift off (§4.2: 0.15 s, then 0.10 s by a quarter of its short
    /// side), and the hop in its well (0.04 of its short side).</summary>
    public const float BeatSeconds = .15f, LiftSeconds = .10f, LiftShare = .25f, HopShare = .04f;
    /// <summary>The hold that closes it over this job's unfinished work (§4.2: "close it anyway").</summary>
    public const float CloseAnywaySeconds = BenchRules.CloseAnywaySeconds;

    public enum CoverState { Seated, Popped, Held, Loose }
    public CoverState State { get; private set; } = CoverState.Seated;

    /// <summary>Off the device: flying (to its well or home), or in its well. A popped cover still covers what is under it.</summary>
    public bool IsRemoved => State == CoverState.Held || State == CoverState.Loose;
    public bool IsPopped => State == CoverState.Popped;
    /// <summary>In its well, at rest.</summary>
    public bool InWell => State == CoverState.Loose && !busy;
    /// <summary>Flying: to its well, or home.</summary>
    public bool Flying => State == CoverState.Held;
    /// <summary>Popping up at the edge the pry went in at (the moment between the creak and the pop; nobody's target).</summary>
    public bool Popping => busy && State == CoverState.Seated && hasPryEdge;

    private bool busy;
    private float pryProgress, closeProgress;
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
        // Nothing at the bench is physical any more (rule 7): an older prefab's rigidbody goes.
        var body = GetComponent<Rigidbody>();
        if (body != null) { if (Application.isPlaying) Destroy(body); else DestroyImmediate(body); }

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

    // The device is finished: a seated cover has nothing to offer (the editor's checks build covers without Awake: no job).
    private bool DeviceFinished => job != null && job.IsFinished;

    /// <summary>The screws that hold it on (the bench's checklist counts them, RepairChecklist).</summary>
    public System.Collections.Generic.IReadOnlyList<Screw> HeldBy => heldBy != null ? heldBy : System.Array.Empty<Screw>();

    /// <summary>The faults it lies over (GrimeSpot.CoveredBy reads them).</summary>
    public System.Collections.Generic.IReadOnlyList<GameObject> FaultsBeneath => faultsBeneath != null ? faultsBeneath : System.Array.Empty<GameObject>();

    /// <summary>
    /// This job's work lies under it, not done yet: a fault of this job still there (a fault the device doesn't have today is
    /// switched off; grime destroys itself when clean), or a part of this job not replaced yet. The checklist's reason to
    /// open it (12 Oct), and the reason the press home must be held (§4.2).
    /// </summary>
    public bool WorkBeneathToDo
    {
        get
        {
            if (faultsBeneath != null) foreach (GameObject f in faultsBeneath)
                if (f != null && f.activeInHierarchy) return true;
            if (replacementsBeneath != null) foreach (ReplaceablePart r in replacementsBeneath)
                if (r != null && r.gameObject.activeInHierarchy && !r.IsReplaced) return true;
            // A part of this job that names this cover as its own, not replaced yet: in its seat, or out of it (lifted out and
            // waiting for the new one: off the device, so found among the job's pieces).
            if (job != null)
            {
                foreach (ReplaceablePart r in job.GetComponentsInChildren<ReplaceablePart>())
                    if (r != null && r.CoveredBy == this && !r.IsReplaced) return true;
                foreach (GameObject piece in job.LooseParts)
                    if (piece != null && piece.TryGetComponent(out ReplaceablePart r) && r.CoveredBy == this && !r.IsReplaced) return true;
            }
            return false;
        }
    }

    public Vector3 Outward => homeParent != null ? homeParent.TransformDirection(outwardLocal).normalized : transform.up;
    public Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPos) : transform.position;
    /// <summary>Its turn at home on the device (the picker's "its seat": a press there in the Cover on step sends it home).</summary>
    public Quaternion HomeRotation => homeParent != null ? homeParent.rotation * homeLocalRot : transform.rotation;
    /// <summary>Its outer face in its own frame (it lies in its well with this up).</summary>
    public Vector3 OuterFaceLocal => outwardOnCover;
    /// <summary>The job it belongs to (remembered while it is off the device).</summary>
    public JobBase Job => job;

    /// <summary>The seat faces the camera: a cover can go home only onto a face the player can see (§4.2).</summary>
    public bool SeatFacesCamera(Camera camera)
    {
        if (camera == null) camera = Camera.main;
        if (camera == null) return true;
        return Vector3.Dot(Outward, (camera.transform.position - HomePosition).normalized) > BenchRules.FacingDot;
    }

    public override bool CanInteract
    {
        get
        {
            if (busy) return false;
            if (State == CoverState.Seated) return AllScrewsOut && !DeviceFinished;   // pry it
            return State == CoverState.Loose;                                          // home from its well
        }
    }

    public override string DisplayName => partName;

    // Its own "why not" when it can't act (UI/UX §5.10 item 7: the hover is the part's name, plus this only when it's blocked).
    // Back on with the work under it done, the screws going back in: no "why not" (15 Oct, Bench 8 on his Mac: the screwdriver
    // passing over the watch's cover while it put the screws back showed "Take them out first", the step before, not the next).
    public override string Prompt => State switch
    {
        CoverState.Seated => DeviceFinished ? "" : AllScrewsOut ? "Hold to pry it up" : WorkBeneathToDo ? "Held on by its screws. Take them out first" : "",
        CoverState.Loose => !SeatFacesCamera(null) ? "Flip it over first" : WorkBeneathToDo ? "Work left inside: hold to close it anyway" : "Click to put it back on",
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
    /// False when the cover is off the device: the tool follows as any other.
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

    // The plate's shorter side across its face (metres): the lift's and the hop's measure.
    private float ShortSide
    {
        get
        {
            Box(out _, out Vector3 s);
            Vector3 scale = transform.lossyScale;
            Vector3 m = new Vector3(Mathf.Abs(s.x * scale.x), Mathf.Abs(s.y * scale.y), Mathf.Abs(s.z * scale.z));
            float thinnest = Mathf.Min(m.x, Mathf.Min(m.y, m.z));
            float largest = Mathf.Max(m.x, Mathf.Max(m.y, m.z));
            return Mathf.Max(.005f, m.x + m.y + m.z - thinnest - largest);
        }
    }

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

    // ---------- the pry, and the press home (both holds) ----------

    public override bool Holdable => State == CoverState.Seated || State == CoverState.Loose;
    public override float HoldProgress => State == CoverState.Loose ? closeProgress / CloseAnywaySeconds : pryProgress;
    public override Vector3 WorkPoint => State == CoverState.Seated && homeParent != null && pryEdgeLocal != Vector3.zero
        ? homeParent.TransformPoint(pryEdgeLocal) : BoundsCentre(transform) + WorkNormal * .002f;
    /// <summary>The face the tool works on: the seat's outside on the device; its outer face, up, in its well.</summary>
    public override Vector3 WorkNormal => State == CoverState.Loose || State == CoverState.Held ? transform.TransformDirection(outwardOnCover).normalized : Outward;

    public override void HoldBegin(BenchHand hand)
    {
        if (State == CoverState.Loose) { closeProgress = 0f; return; }
        if (State != CoverState.Seated) return;
        // The seam nearest the press: the pry goes in there, and that side lifts.
        Vector3 at = hand.hitSomething ? hand.hit.point : BoundsCentre(transform);
        SeamAt(at, out Vector3 edge, out Vector3 across);
        RememberPry(edge, across);
        if (pryProgress <= 0f) Sfx.Play("cover.creak", edge);
    }

    public override bool HoldTick(BenchHand hand)
    {
        if (State == CoverState.Loose)
        {
            // Home from its well: at once when nothing of this job is left under it; held 0.4 s when work is ("close it anyway").
            if (busy || !SeatFacesCamera(hand.camera)) return false;
            if (WorkBeneathToDo)
            {
                closeProgress += hand.deltaTime;
                if (job != null) BenchWork.NoteProgress(job);
                if (closeProgress < CloseAnywaySeconds) return false;
            }
            GoHome();
            return true;
        }
        if (State != CoverState.Seated) return true;
        pryProgress = Mathf.Clamp01(pryProgress + hand.deltaTime / pryDuration);
        if (job != null) BenchWork.NoteProgress(job);
        if (pryProgress < 1f) return false;
        if (Application.isPlaying) StartCoroutine(Pop(true));
        else { PopAtOnce(); GoToWell(); }
        return true;
    }

    public override void HoldEnd(BenchHand hand, bool finished)
    {
        if (finished) return;
        if (State == CoverState.Seated) pryProgress = Mathf.Max(0f, pryProgress - .35f);   // the lever slips back a little when let go
        if (State == CoverState.Loose) closeProgress = 0f;
    }

    // How far the cover reaches from its centre along a world direction.
    private float ExtentAlong(Vector3 direction)
    {
        var box = GetComponent<BoxCollider>();
        Vector3 size = box != null ? Vector3.Scale(box.size, transform.lossyScale) : transform.lossyScale;
        Vector3 local = transform.InverseTransformDirection(direction.normalized);
        return Mathf.Abs(local.x) * size.x * .5f + Mathf.Abs(local.y) * size.y * .5f + Mathf.Abs(local.z) * size.z * .5f;
    }

    // The pop's hinge and turn: about the edge opposite the seam the pry went in at (playtest 4).
    private void PopTurn(out Vector3 hinge, out Vector3 axis)
    {
        Vector3 centre = BoundsCentre(transform);
        Vector3 n = Outward;
        Vector3 edge = homeParent != null ? homeParent.TransformPoint(pryEdgeLocal) : centre;
        Vector3 acrossWorld = homeParent != null ? homeParent.TransformDirection(pryAcrossLocal) : pryAcrossLocal;
        Vector3 d = hasPryEdge && Vector3.ProjectOnPlane(acrossWorld, n).sqrMagnitude > 1e-8f
            ? Vector3.ProjectOnPlane(acrossWorld, n).normalized : Vector3.ProjectOnPlane(edge - centre, n).normalized;
        if (d.sqrMagnitude < 1e-8f) d = Vector3.ProjectOnPlane(transform.forward, n).normalized;
        hinge = centre - d * ExtentAlong(d);       // the far edge stays where it is
        axis = Vector3.Cross(d, n).normalized;      // turning about this lifts the pried edge outward
    }

    private IEnumerator Pop(bool thenToTheWell)
    {
        busy = true;
        PopTurn(out Vector3 hinge, out Vector3 axis);
        Vector3 edge = homeParent != null ? homeParent.TransformPoint(pryEdgeLocal) : BoundsCentre(transform);
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
        BenchFeel.Pulse(.35f, .45f, .06f);
        if (thenToTheWell) GoToWell();
    }

    // The pop with no frames (the editor's checks): the popped pose at once.
    private void PopAtOnce()
    {
        PopTurn(out Vector3 hinge, out Vector3 axis);
        Quaternion settled = Quaternion.AngleAxis(popAngle, axis);
        Vector3 fromPos = transform.position;
        transform.rotation = settled * transform.rotation;
        transform.position = hinge + settled * (fromPos - hinge);
        State = CoverState.Popped;
        pryProgress = 0f;
    }

    // ---------- off to its well, and home (BenchStage's flights) ----------

    // After the pop: a beat at the popped angle, the lift off with cover.lift, the arc to its well, cover.clack on landing.
    private void GoToWell()
    {
        if (State != CoverState.Popped) return;
        Detach();
        State = CoverState.Held;
        busy = true;
        if (job != null) BenchWork.NoteTargetDone(job, this);
        float shortSide = ShortSide;
        BenchStage.SendToPlace(this, job, BenchStage.SlotKind.Well, BenchStage.FlightKind.Cover,
            BeatSeconds, LiftSeconds, transform.TransformDirection(outwardOnCover).normalized * (LiftShare * shortSide), "cover.lift", false,
            null, "cover.clack", HopShare * shortSide, InTheWell);
    }

    private void InTheWell()
    {
        State = CoverState.Loose;
        busy = false;
        closeProgress = 0f;
    }

    // Home from its well: the flight to its seat, and it clips on (part.on).
    private void GoHome()
    {
        if (State != CoverState.Loose) return;
        State = CoverState.Held;
        busy = true;
        closeProgress = 0f;
        BenchStage.SendTo(this, job, homeParent, homeLocalPos, homeLocalRot, BenchStage.FlightKind.Cover, 0f, null, "part.on", homeParent, ClippedOn);
    }

    private void ClippedOn()
    {
        if (homeParent != null && transform.parent != homeParent) transform.SetParent(homeParent, true);
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        if (job != null) job.UnregisterDetached(gameObject);
        State = CoverState.Seated;
        pryProgress = 0f;
        closeProgress = 0f;
        hasPryEdge = false;      // home again: the next pry goes in wherever it is pressed
        busy = false;
        if (job != null) BenchWork.NoteTargetDone(job, this);
        // Screws are NOT auto-returned: the player screws each one in again.
    }

    private void Detach()
    {
        if (transform.parent == null) return;
        transform.SetParent(null, true);
        if (job != null) job.RegisterDetached(gameObject);
    }

    // ---------- the whole thing by itself (the labs' hand, the Day 1 guide, older callers) ----------

    public override void Activate()
    {
        if (!awoke) Awake();
        if (busy) return;
        if (!Application.isPlaying)
        {
            // The editor's checks: off or on at once (the flights complete at once; the same states, the same places).
            if (State == CoverState.Seated && !AllScrewsOut) return;
            if (State == CoverState.Seated)
            {
                SeamAt(BoundsCentre(transform) + Vector3.ProjectOnPlane(transform.forward, Outward).normalized * ExtentAlong(transform.forward), out Vector3 edge, out Vector3 across);
                RememberPry(edge, across);
                PopAtOnce();
            }
            if (State == CoverState.Popped) GoToWell();
            else if (State == CoverState.Loose) GoHome();
            return;
        }
        if (State == CoverState.Seated) StartCoroutine(PryBySelf());
        else if (State == CoverState.Popped) GoToWell();
        else if (State == CoverState.Loose) GoHome();
    }

    private IEnumerator PryBySelf()
    {
        if (!AllScrewsOut) yield break;
        // The seam at the cover's forward end, as the self-driven lift always pried it.
        Vector3 centre = BoundsCentre(transform);
        SeamAt(centre + Vector3.ProjectOnPlane(transform.forward, Outward).normalized * ExtentAlong(transform.forward), out Vector3 edge, out Vector3 across);
        RememberPry(edge, across);
        Sfx.Play("cover.creak", edge);
        yield return Pop(true);
    }

#if UNITY_EDITOR
    /// <summary>The build steps' hand on what it lies over.</summary>
    public void ConfigureBeneath(GameObject[] faults, ReplaceablePart[] parts) { faultsBeneath = faults; replacementsBeneath = parts; }
#endif
}
