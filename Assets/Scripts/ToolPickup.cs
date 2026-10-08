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

    public enum Motion { None, Turn, Pinch, Lever, Stroke, Circle }

    public bool InHand { get; private set; }

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

    private void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        legacyTint = rend != null && transform.childCount == 0 && rend.sharedMaterials.Length == 1;
        if (legacyTint) rend.material.color = tint;
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        leafL = transform.Find("Leaf_L");
        leafR = transform.Find("Leaf_R");
        if (leafL != null) leafLRest = leafL.localRotation;
        if (leafR != null) leafRRest = leafR.localRotation;
        colliders = GetComponentsInChildren<Collider>(true);
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
        if (on)
        {
            if (returning != null) { StopCoroutine(returning); returning = null; }
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
        // The handle up the normal, then leaned toward the camera (about the axis across both).
        Vector3 leanAxis = Vector3.Cross(n, toCamera);
        Quaternion upright = Quaternion.FromToRotation(Vector3.up, n);
        Quaternion leaned = leanAxis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(-lean, leanAxis.normalized) * upright : upright;
        // The flat tools show their flat side to the camera: turn about the handle so +Z faces the camera.
        Vector3 handleUp = leaned * Vector3.up;
        Vector3 face = Vector3.ProjectOnPlane(toCamera, handleUp);
        if (face.sqrMagnitude > 1e-6f) leaned = Quaternion.LookRotation(face.normalized, handleUp);
        // A swing against the way it moves: the handle trails the tip a little, like a thing with weight.
        Vector3 swingAxis = Vector3.Cross(n, velocity);
        float swing = Mathf.Min(12f, velocity.magnitude * 25f);
        if (swingAxis.sqrMagnitude > 1e-6f && swing > .1f) leaned = Quaternion.AngleAxis(swing, swingAxis.normalized) * leaned;
        Quaternion target = leaned * MotionRotation();
        Vector3 targetPos = easedPoint + n * (working ? 0f : .004f) + MotionOffset(n, camera);
        // The tip on a spring, critically damped: it arrives without a bounce and never jumps.
        float w = working ? 26f : 16f;
        Vector3 to = transform.position - targetPos;
        velocity += (-w * w * to - 2f * w * velocity) * dt;
        transform.position += velocity * dt;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-(working ? 22f : 14f) * dt));
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
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / .2f;
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(fromPos, homeParent.TransformPoint(homeLocalPos), c);
            transform.rotation = Quaternion.Slerp(fromRot, homeParent.rotation * homeLocalRot, c);
            yield return null;
        }
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        spin = 0f;
        returning = null;
    }
}
