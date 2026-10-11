using System.Collections;
using UnityEngine;

public enum ToolType { Hand, Brush, Screwdriver, Pry, Tweezers, Cloth }

// ---------------------------------------------------------------------------
// A TOOL IN THE CADDY, AND IN THE HAND (the bench, v2; claude/bench-spec-v2.md §2.5)
//
// Picked (clicked, or LB/RB on a pad) it leaves its stand in the caddy and follows the cursor over the device as the
// model itself: its working tip (the model's origin) at the cursor's point on the device, its handle up the surface
// normal and leaning toward the camera so it reads. Put down, it returns to its stand. While a hold is on, it does
// its motion at the part: the driver turns, the tweezers close, the pry tilts, the brush strokes, the cloth circles.
// The old tinted-cylinder rail used the same component; a lone primitive still gets its tint, a model does not.
//
// The cloth is the exception (9 Oct night, his note: "the cloth stuck inside the tray, it needs to sit and look
// correctly and be used correctly"). It has no handle: it is pressed flat under the fingers, so it never leans and
// never swings; its face stays on the glass. And it leads with its far fold: the cursor's point is just inside the
// edge away from the camera and the rest of the cloth lies toward Ace, so the patch it is wiping shows at its edge.
// It rests folded on the mat, not in the caddy (Playtest3Session4Steps, BenchClothSteps).
//
// And it was never in the hand at all: the cloth is one mesh with one material and no children, which is exactly how
// this component used to recognise the old tinted cylinders, so it was taken for one. It was tinted pale blue, grew a
// quarter when picked, and never followed the cursor (a stand-in stays on its rail), so the smudge came off under an
// empty cursor while the cloth sat in the caddy. A stand-in is now recognised by its mesh: one of Unity's own
// primitives.
//
// THE CLOTH AS A PAD, AND ITS LIFT (playtest 4, 10 Oct; the veteran: "the cloth covers about two-thirds of the screen
// while you wipe and when 'Fixed!' fires; the smudge thinning and the gleam both happen under it"). Folded in quarters it
// is 75 x 90 mm, and the phone's glass is 64 mm across: wherever it went it covered the glass. Picked up, it is gathered
// into a pad under the fingers (Pad Size of its folded size across the glass, 34 x 40 mm; Pad Thickness of its height,
// a little taller, as a bunched cloth is), still leading with its far fold at the cursor, and it trails the cursor on its
// spring, so the glass it has just wiped shows behind it. When a smudge comes clean (ItemInspector says so: LiftOff) the
// pad lifts off the glass, up and back toward Ace's hand, a little to his right, tipping as a hand lifts it (0.45 s), and
// stays clear for 1.1 s, so the gleam (0.15 s in) sweeps clear glass and the screen that wakes (0.3 s in, fully lit by
// 1.1 s) is seen with the one ending over it; then it comes back to the cursor (0.3 s). Wiping another smudge brings it
// down at once (Land). It spreads back out to its folded size on its way home to the mat.
//
// THE PRY AT A PRY ANGLE (playtest 4; the veteran: "the pry tool stands upright on the middle of the back like the
// screwdriver"). A spudger is held low, not stood on its tip: in the hand the pry tool's handle leans down to the right
// (Ace's hand) at a pry angle, and at a cover it sits in the seam with its handle out across it (RemovablePart.PryPose,
// FollowPose), and the lever turns its handle down toward the device.
// ---------------------------------------------------------------------------
public class ToolPickup : MonoBehaviour
{
    public ToolType tool;

    [Tooltip("Shown in the hover tooltip.")]
    public string displayName = "Tool";

    [Tooltip("Tint so tools are distinguishable at a glance (only applied to a lone primitive, never to a model).")]
    public Color tint = Color.white;

    [Tooltip("Degrees the handle leans toward the camera while the tool is held over the device.")]
    [SerializeField, Range(0f, 60f)] private float lean = 18f;

    [Header("The cloth in the hand (playtest 4)")]
    [Tooltip("In the hand the cloth is gathered into a pad under the fingers: its size across the glass, as a share of its folded size (75 x 90 mm).")]
    [SerializeField, Range(.3f, 1f)] private float padSize = .45f;
    [Tooltip("...and its height, as a share of its folded height (a gathered cloth stands a little taller).")]
    [SerializeField, Range(1f, 2f)] private float padThickness = 1.4f;

    public enum Motion { None, Turn, Pinch, Lever, Stroke, Circle }

    public bool InHand { get; private set; }

    /// <summary>The cloth: pressed flat on the surface, no lean, no swing (see the header).</summary>
    public bool PressedFlat => tool == ToolType.Cloth;

    // The cloth's far fold leads by this much past the cursor's point: of the 9.5 mm the cloth cleans
    // (GrimeSpot.ClothRadius) about a third shows beyond the fold, the rest is under it.
    private const float FoldLead = .003f;

    // The cloth's lift (playtest 4): up off the glass and back toward the hand, over Rise seconds, held for Hold, back in
    // Fall. Metres and degrees.
    private const float LiftRise = .45f, LiftHold = 1.1f, LiftFall = .3f;
    private const float LiftUp = .035f, LiftBack = .06f, LiftTip = 12f;

    // The pry held low, as a spudger is: its handle this far up from the surface it is over, degrees (the seam's own
    // angle is RemovablePart.PryAngle).
    private const float PryHeld = 38f;

    private Vector3 baseScale;
    private Renderer rend;
    private bool legacyTint;
    private Transform homeParent;
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform leafL, leafR;
    private Quaternion leafLRest, leafRRest;
    private float spin, pinch, lever;
    private Coroutine returning;
    private Collider[] colliders;
    // The follow (8 Oct, "more fluid"): the tip rides a spring to a target that is itself eased, so a jump of the cursor
    // from the device to the mat is a glide, not a pop, and the handle swings a little against the way it is moving.
    private Vector3 velocity;
    private Vector3 easedPoint, easedNormal;
    private bool easing;
    // The cloth as a pad: how gathered it is (1: folded as it rests; padSize: in the hand), its scale in the world when it
    // left the mat (it has no parent in the hand), and how deep it is from its middle to its far fold (its own mesh).
    private float pad = 1f;
    private Vector3 heldScale = Vector3.one;
    private float clothFarFold = .045f;
    private float liftAt = -1f;

    /// <summary>True for the old tinted stand-ins (one of Unity's primitives); false for every modelled tool.</summary>
    public bool IsStandIn => legacyTint;

    /// <summary>The cloth is lifted off the glass (a smudge just came clean), or on its way up or back down.</summary>
    public bool Lifting => liftAt >= 0f && Time.time - liftAt < LiftRise + LiftHold + LiftFall;

    /// <summary>How far through its lift the cloth is now: 0 on the glass, 1 fully up (for the labs).</summary>
    public float Lifted => LiftAmount(Time.time);

    /// <summary>The cloth gathered into a pad: its size across the glass now, as a share of its folded size (1 at rest).</summary>
    public float PadShown => pad;

    private void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        MeshFilter filter = GetComponent<MeshFilter>();
        string mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "";
        legacyTint = rend != null && transform.childCount == 0 && rend.sharedMaterials.Length == 1
                     && (mesh == "Cylinder" || mesh == "Cube" || mesh == "Capsule" || mesh == "Sphere");
        if (legacyTint) rend.material.color = tint;
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        leafL = transform.Find("Leaf_L");
        leafR = transform.Find("Leaf_R");
        if (leafL != null) leafLRest = leafL.localRotation;
        if (leafR != null) leafRRest = leafR.localRotation;
        colliders = GetComponentsInChildren<Collider>(true);
        // The cloth's far fold is its -Z edge (its mesh, in its own units): from its middle to there.
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds b = filter.sharedMesh.bounds;
            clothFarFold = Mathf.Max(.005f, b.extents.z - b.center.z);
        }
    }

    public void SetSelected(bool on)
    {
        if (on && !InHand) Sfx.Play("tool.pick", transform.position);
        if (!on && InHand) Sfx.Play("tool.down", transform.position);
        InHand = on;
        if (legacyTint)
        {
            transform.localScale = on ? baseScale * 1.25f : baseScale;
            if (rend != null) rend.material.color = on ? Color.Lerp(tint, Color.white, 0.5f) : tint;
        }
        // In the hand its colliders are off: the cursor's ray must reach the part under the tip, not the tool over it,
        // and a held tool shouldn't knock the loose screws about. Back in the caddy they're on, so it can be picked again.
        if (colliders != null) foreach (Collider c in colliders) if (c != null) c.enabled = !on;
        liftAt = -1f;
        if (on)
        {
            if (returning != null) { StopCoroutine(returning); returning = null; }
            // Its size in the world as it lies at home (in the hand it has no parent to carry that).
            Vector3 parentScale = homeParent != null ? homeParent.lossyScale : Vector3.one;
            heldScale = Vector3.Scale(baseScale, parentScale);
        }
        else
        {
            Rest();
            if (gameObject.activeInHierarchy) returning = StartCoroutine(ReturnHome());
        }
    }

    /// <summary>
    /// Where the tool goes this frame while in the hand: its tip at <paramref name="point"/>, handle up <paramref name="normal"/>,
    /// leaning toward <paramref name="camera"/>. <paramref name="working"/>: a hold is on, so the tip sits exactly on the point.
    /// </summary>
    public void Follow(Vector3 point, Vector3 normal, Camera camera, bool working, float deltaTime)
    {
        if (!InHand || legacyTint) return;
        if (transform.parent != null) transform.SetParent(null, true);
        float dt = Mathf.Min(deltaTime, .05f);
        Vector3 wantNormal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        // The target itself eases (the cursor's point and the surface's normal jump between surfaces).
        if (!easing) { easedPoint = point; easedNormal = wantNormal; velocity = Vector3.zero; easing = true; }
        float e = 1f - Mathf.Exp(-(working ? 40f : 16f) * dt);
        easedPoint = Vector3.Lerp(easedPoint, point, e);
        easedNormal = Vector3.Slerp(easedNormal, wantNormal, e).normalized;
        Vector3 n = easedNormal;
        Vector3 toCamera = camera != null ? (camera.transform.position - easedPoint).normalized : Vector3.back;
        Quaternion leaned;
        if (tool == ToolType.Pry)
        {
            // Held low, as a spudger is: the handle down to the right (the hand), the blade flat toward the surface.
            Vector3 side = Vector3.ProjectOnPlane(camera != null ? camera.transform.right : Vector3.right, n);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(toCamera, n);
            leaned = side.sqrMagnitude > 1e-6f ? PryRotation(n, side.normalized, PryHeld) : Quaternion.FromToRotation(Vector3.up, n);
        }
        else
        {
            // The handle up the normal, then leaned toward the camera (about the axis across both). The cloth doesn't lean.
            Vector3 leanAxis = Vector3.Cross(n, toCamera);
            Quaternion upright = Quaternion.FromToRotation(Vector3.up, n);
            leaned = !PressedFlat && leanAxis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(-lean, leanAxis.normalized) * upright : upright;
            // The flat tools show their flat side to the camera: turn about the handle so +Z faces the camera.
            Vector3 handleUp = leaned * Vector3.up;
            Vector3 face = Vector3.ProjectOnPlane(toCamera, handleUp);
            if (face.sqrMagnitude > 1e-6f) leaned = Quaternion.LookRotation(face.normalized, handleUp);
            // A swing against the way it moves: the handle trails the tip a little, like a thing with weight. Not the cloth:
            // the fingers hold it flat.
            Vector3 swingAxis = Vector3.Cross(n, velocity);
            float swing = Mathf.Min(12f, velocity.magnitude * 25f);
            if (!PressedFlat && swingAxis.sqrMagnitude > 1e-6f && swing > .1f) leaned = Quaternion.AngleAxis(swing, swingAxis.normalized) * leaned;
        }
        Quaternion target = leaned * MotionRotation();
        Vector3 targetPos = easedPoint + n * (working ? 0f : .004f) + MotionOffset(n, camera);
        bool lifting = false;
        if (PressedFlat)
        {
            // Gathered into a pad under the fingers (playtest 4), in about a sixth of a second.
            pad = Mathf.MoveTowards(pad, padSize, dt / .15f * Mathf.Max(.05f, 1f - padSize));
            ApplyPad();
            // The cloth leads with its far fold: its middle sits toward the camera, along the surface, by the pad's own depth
            // to the fold less the lead. When the camera looks straight down the normal (the cloth held up in the air, over
            // nothing) there is no "toward", and the shift fades out rather than jumping sideways.
            Vector3 along = Vector3.ProjectOnPlane(toCamera, n);
            float reach = Mathf.Max(0f, clothFarFold * heldScale.z * pad - FoldLead);
            targetPos += along.normalized * (reach * Mathf.Clamp01(along.magnitude / .3f));
            // The lift: up off the glass and back toward the hand, a little to its right, tipping as a hand lifts it.
            float lift = LiftAmount(Time.time);
            if (lift > 0f)
            {
                lifting = true;
                Vector3 side = Vector3.ProjectOnPlane(camera != null ? camera.transform.right : Vector3.right, n);
                Vector3 back = along.sqrMagnitude > 1e-6f ? along.normalized : side.sqrMagnitude > 1e-6f ? -side.normalized : Vector3.forward;
                Vector3 away = (back + (side.sqrMagnitude > 1e-6f ? side.normalized * .5f : Vector3.zero)).normalized;
                targetPos += n * (LiftUp * lift) + away * (LiftBack * lift);
                Vector3 tipAxis = Vector3.Cross(away, n);
                if (tipAxis.sqrMagnitude > 1e-6f) target = Quaternion.AngleAxis(LiftTip * lift, tipAxis.normalized) * target;
            }
        }
        Spring(targetPos, target, working || lifting, dt);
    }

    /// <summary>
    /// Where the tool goes this frame when the work decides its angle (the pry tool at a cover's seam, playtest 4): its tip
    /// at <paramref name="tip"/>, its handle along <paramref name="handle"/>, its flat side's normal (+Z) toward
    /// <paramref name="flat"/>; hovering 3 mm off <paramref name="surfaceNormal"/> unless a hold is on. The same spring as
    /// Follow, with the tool's motion (the lever) on top.
    /// </summary>
    public void FollowPose(Vector3 tip, Vector3 handle, Vector3 flat, Vector3 surfaceNormal, Camera camera, bool working, float deltaTime)
    {
        if (!InHand || legacyTint) return;
        if (transform.parent != null) transform.SetParent(null, true);
        float dt = Mathf.Min(deltaTime, .05f);
        Vector3 h = handle.sqrMagnitude > 1e-6f ? handle.normalized : Vector3.up;
        Vector3 f = Vector3.ProjectOnPlane(flat, h);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(camera != null ? camera.transform.position - tip : Vector3.back, h);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, h);
        Vector3 n = surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.up;
        if (!easing) { easedPoint = tip; easedNormal = n; velocity = Vector3.zero; easing = true; }
        float e = 1f - Mathf.Exp(-(working ? 40f : 16f) * dt);
        easedPoint = Vector3.Lerp(easedPoint, tip, e);
        easedNormal = Vector3.Slerp(easedNormal, n, e).normalized;   // kept in step, so a Follow after this carries on smoothly
        Quaternion target = Quaternion.LookRotation(f.normalized, h) * MotionRotation();
        Spring(easedPoint + (working ? Vector3.zero : n * .003f), target, working, dt);
    }

    // The tip on a spring, critically damped: it arrives without a bounce and never jumps; the turn eased toward the target.
    private void Spring(Vector3 targetPos, Quaternion target, bool working, float dt)
    {
        float w = working ? 26f : 16f;
        Vector3 to = transform.position - targetPos;
        velocity += (-w * w * to - 2f * w * velocity) * dt;
        transform.position += velocity * dt;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-(working ? 22f : 14f) * dt));
    }

    // The pry's rotation for a handle that rises `degrees` from a surface (normal n) toward `across` (in the surface): its
    // blade's width along the surface, across `across`; its +Z (the flat side) out and down, so the lever (about its X)
    // turns the handle down toward the surface and the blade's edge up.
    private static Quaternion PryRotation(Vector3 n, Vector3 across, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        Vector3 handle = n * Mathf.Sin(a) + across * Mathf.Cos(a);
        Vector3 flat = across * Mathf.Sin(a) - n * Mathf.Cos(a);
        return Quaternion.LookRotation(flat.normalized, handle.normalized);
    }

    /// <summary>The cloth lifts off the glass (a smudge has just come clean), with its swish. Nothing for the other tools.</summary>
    public void LiftOff()
    {
        if (!InHand || !PressedFlat) return;
        liftAt = Time.time;
        Sfx.Play("cloth.lift", transform.position);
    }

    /// <summary>The cloth comes back down now (another smudge is being wiped): the lift's hold ends, its fall begins.</summary>
    public void Land()
    {
        if (!Lifting) return;
        float t = Time.time - liftAt;
        // Already coming down: nothing to do. Still rising or held: the fall starts now, from where it is (a rising pad
        // part-way up comes down from part-way, not from the top).
        if (t < LiftRise + LiftHold) liftAt = Time.time - (LiftRise + LiftHold) - LiftFall * (1f - LiftAmount(Time.time));
    }

    // The lift's height, 0..1: up quickly (easing out), held, then down (easing in and out).
    private float LiftAmount(float now)
    {
        if (liftAt < 0f) return 0f;
        float t = now - liftAt;
        if (t < 0f) return 0f;
        if (t < LiftRise) { float u = 1f - t / LiftRise; return 1f - u * u * u; }
        if (t < LiftRise + LiftHold) return 1f;
        if (t < LiftRise + LiftHold + LiftFall) return 1f - Mathf.SmoothStep(0f, 1f, (t - LiftRise - LiftHold) / LiftFall);
        return 0f;
    }

    // The cloth's scale for how gathered it is: across the glass (X, Z) by the pad, its height (Y) by the thickness. Its
    // origin is its underside, so it stays on the glass as it changes.
    private void ApplyPad()
    {
        float k = padSize < .999f ? Mathf.Clamp01((1f - pad) / (1f - padSize)) : 0f;
        transform.localScale = new Vector3(heldScale.x * pad, heldScale.y * Mathf.Lerp(1f, padThickness, k), heldScale.z * pad);
    }

    /// <summary>The tool's motion while a hold is on. direction: +1 outward / forward, -1 inward / back.</summary>
    public void Animate(Motion motion, float progress, float direction, float deltaTime)
    {
        switch (motion)
        {
            case Motion.Turn:   spin += -direction * 420f * deltaTime; break;              // counter-clockwise backs a screw out
            case Motion.Pinch:  pinch = Mathf.MoveTowards(pinch, 1f, deltaTime / .12f); break;
            case Motion.Lever:  lever = Mathf.Lerp(0f, 16f, progress); break;
            case Motion.Stroke: strokePhase += deltaTime * 6f * Mathf.PI * 2f; break;
            case Motion.Circle: strokePhase += deltaTime * 2f * Mathf.PI * 2f; break;
        }
        ApplyLeaves();
    }

    /// <summary>No hold: the motions ease back to rest.</summary>
    public void Rest()
    {
        pinch = Mathf.MoveTowards(pinch, 0f, Time.deltaTime / .1f);
        lever = Mathf.MoveTowards(lever, 0f, Time.deltaTime * 90f);
        ApplyLeaves();
    }

    private float strokePhase;

    private Quaternion MotionRotation()
    {
        Quaternion r = Quaternion.identity;
        if (tool == ToolType.Screwdriver) r = Quaternion.AngleAxis(spin, Vector3.up);
        else if (tool == ToolType.Pry) r = Quaternion.AngleAxis(lever, Vector3.right);      // the blade is flat along X: it levers about X
        return r;
    }

    private Vector3 MotionOffset(Vector3 n, Camera camera)
    {
        if (tool == ToolType.Brush && strokePhase > 0f)
        {
            Vector3 across = Vector3.Cross(n, camera != null ? camera.transform.right : Vector3.right).normalized;
            return across * (Mathf.Sin(strokePhase) * .004f);
        }
        if (tool == ToolType.Cloth && strokePhase > 0f)
        {
            Vector3 a = Vector3.Cross(n, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(n, Vector3.up).normalized : Vector3.right;
            Vector3 b = Vector3.Cross(n, a).normalized;
            return (a * Mathf.Cos(strokePhase) + b * Mathf.Sin(strokePhase)) * .006f;
        }
        return Vector3.zero;
    }

    private void ApplyLeaves()
    {
        // The tweezers' leaves close about the heel (their origin), each toward the other, until the points meet.
        if (leafL != null) leafL.localRotation = leafLRest * Quaternion.AngleAxis(1.1f * pinch, Vector3.forward);
        if (leafR != null) leafR.localRotation = leafRRest * Quaternion.AngleAxis(-1.1f * pinch, Vector3.forward);
    }

    private void LateUpdate()
    {
        if (!InHand) strokePhase = 0f;
    }

    private IEnumerator ReturnHome()
    {
        easing = false;
        velocity = Vector3.zero;
        if (homeParent == null) yield break;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        transform.SetParent(homeParent, true);
        // The cloth spreads back out to its folded size on the way (it was gathered into a pad in the hand).
        Vector3 fromScale = transform.localScale;
        bool rescale = !legacyTint && (fromScale - baseScale).sqrMagnitude > 1e-10f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .2f;
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(fromPos, homeParent.TransformPoint(homeLocalPos), c);
            transform.rotation = Quaternion.Slerp(fromRot, homeParent.rotation * homeLocalRot, c);
            if (rescale) transform.localScale = Vector3.Lerp(fromScale, baseScale, c);
            yield return null;
        }
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        if (!legacyTint) transform.localScale = baseScale;
        pad = 1f;
        spin = 0f;
        returning = null;
    }
}
