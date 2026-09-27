using UnityEngine;

/// <summary>
/// Head-and-neck look for a café NPC: turns the rig's Neck and Head bones
/// towards a point after the Animator has posed them, so a customer at the
/// counter looks at Ace instead of at the slot's fixed yaw, and a waiting
/// customer follows Ace when he brings their order over.
///
/// It is deliberately small. The brain says WHO to look at and how much
/// (<see cref="LookAt(Transform, Vector3, float)"/>, <see cref="Clear"/>);
/// this component only turns the head: yaw and pitch limited to what a neck
/// does, the turn smoothed, the weight blended in and out, and the look fading
/// out by itself when the target drifts behind the shoulder instead of pinning
/// the head at its limit. Turning the whole body stays the brain's decision
/// (see CustomerBrain.UpdateAttention), so a body never spins after the
/// player - the head does the following, and the body turns once, late, if
/// the head cannot reach.
///
/// Runs in LateUpdate between the Animator and PolygonNpcVisual (150), which
/// copies the rig's bones onto a city body, so the visible body follows too.
/// Without Neck/Head bones on the rig it does nothing.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public sealed class NpcLookAt : MonoBehaviour
{
    [Tooltip("How far the head turns to either side before the look fades out, degrees.")]
    [SerializeField, Range(20f, 90f)] private float maxYaw = 72f;
    [Tooltip("How far the head tilts up or down, degrees.")]
    [SerializeField, Range(5f, 45f)] private float maxPitch = 22f;
    [Tooltip("Share of the turn done by the head; the rest is the neck.")]
    [SerializeField, Range(0f, 1f)] private float headShare = .65f;
    [Tooltip("Smoothing time of the head turn, seconds (a glance is quick, a stare settles).")]
    [SerializeField, Range(.05f, .6f)] private float turnSmoothing = .2f;
    [Tooltip("How long the look takes to blend in or out, seconds.")]
    [SerializeField, Range(.05f, .8f)] private float blendSmoothing = .28f;

    private Transform neck, head;
    private bool searched;
    private Transform target;
    private Vector3 targetOffset, point;
    private bool hasPoint;
    private float wantedWeight;
    private float yaw, pitch, yawVelocity, pitchVelocity, weight, weightVelocity;
    // A nod: a short dip of the head and back, on top of whatever the look is doing.
    private float nodAmplitude, nodPeriod = .55f, nodStart = -10f;
    private int nodCount;

    /// <summary>Signed angle from the body's forward to the target, degrees (0 without one). The brain reads this to decide on a body turn.</summary>
    public float TargetYaw { get; private set; }
    public bool HasTarget => hasPoint || target != null;
    public bool Looking => weight > .02f;
    /// <summary>The yaw actually applied to the head right now, degrees (for checks and traces).</summary>
    public float AppliedYaw => yaw * weight;
    public float Weight => weight;
    public Transform HeadBone { get { if (!searched) FindBones(); return head; } }

    /// <summary>Look at <paramref name="t"/> plus <paramref name="offset"/> (e.g. a person's eyes) with this weight (0..1).</summary>
    public void LookAt(Transform t, Vector3 offset, float w = 1f)
    {
        target = t;
        targetOffset = offset;
        hasPoint = false;
        wantedWeight = Mathf.Clamp01(w);
    }

    public void LookAt(Vector3 worldPoint, float w = 1f)
    {
        target = null;
        point = worldPoint;
        hasPoint = true;
        wantedWeight = Mathf.Clamp01(w);
    }

    /// <summary>A small nod (down and back up), <paramref name="count"/> times; works with or without a look target.</summary>
    public void Nod(float amplitudeDegrees = 9f, int count = 1, float periodSeconds = .55f)
    {
        nodAmplitude = Mathf.Clamp(amplitudeDegrees, 1f, 20f);
        nodCount = Mathf.Clamp(count, 1, 3);
        nodPeriod = Mathf.Clamp(periodSeconds, .25f, 1.2f);
        nodStart = Time.time;
    }

    public bool Nodding => Time.time - nodStart < nodPeriod * nodCount;

    /// <summary>Let the head return to the animation.</summary>
    public void Clear()
    {
        target = null;
        hasPoint = false;
        wantedWeight = 0f;
    }

    private void LateUpdate()
    {
        if (!searched) FindBones();
        if (head == null) return;
        float dt = Time.deltaTime;
        if (target == null && !hasPoint) wantedWeight = 0f;   // the target was destroyed
        bool have = hasPoint || target != null;

        float wantYaw = 0f, wantPitch = 0f, reach = 0f;
        if (have)
        {
            Vector3 aim = target != null ? target.position + targetOffset : point;
            Vector3 to = aim - head.position;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                TargetYaw = Vector3.SignedAngle(Flat(transform.forward), flat, Vector3.up);
                wantYaw = Mathf.Clamp(TargetYaw, -maxYaw, maxYaw);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                // Full weight inside the comfortable range, fading to nothing
                // by about 1.45x the limit (~105°, an over-the-shoulder glance):
                // a target further behind is simply not looked at, rather than
                // stared at over the limit.
                reach = Mathf.Clamp01(Mathf.InverseLerp(maxYaw * 1.45f, maxYaw, Mathf.Abs(TargetYaw)));
            }
        }
        else TargetYaw = 0f;

        weight = Mathf.SmoothDamp(weight, have ? wantedWeight * reach : 0f, ref weightVelocity, blendSmoothing, 8f, dt);
        // The nod: a half sine per dip, so it starts and ends at rest.
        float nod = 0f;
        float sinceNod = Time.time - nodStart;
        if (sinceNod >= 0f && sinceNod < nodPeriod * nodCount)
            nod = nodAmplitude * Mathf.Sin(Mathf.PI * (sinceNod % nodPeriod) / nodPeriod);

        if (weight > .001f && have)
        {
            yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVelocity, turnSmoothing, 400f, dt);
            pitch = Mathf.SmoothDampAngle(pitch, wantPitch, ref pitchVelocity, turnSmoothing, 400f, dt);
        }
        else if (weight <= .001f)
        {
            yaw = pitch = 0f;
            yawVelocity = pitchVelocity = 0f;
            if (Mathf.Abs(nod) < .01f) return;
        }

        // A world-space turn about the head's own pivot: yaw about up, then
        // pitch about the turned right axis. World-space, so the rig's bone
        // axes never matter.
        Quaternion turnYaw = Quaternion.AngleAxis(yaw, Vector3.up);
        Vector3 axis = turnYaw * Flat(transform.right);
        Quaternion turn = Quaternion.AngleAxis(pitch, axis) * turnYaw;
        float neckShare = weight * (1f - headShare);
        if (neck != null && neckShare > .001f)
            neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, neckShare) * neck.rotation;
        head.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight * headShare) * head.rotation;
        if (Mathf.Abs(nod) >= .01f)
        {
            // Split like a look: a little neck, mostly head, about the head's right.
            Quaternion dip = Quaternion.AngleAxis(nod, axis);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, dip, .35f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, dip, .65f) * head.rotation;
        }
    }

    // The rig's own Neck and Head, never the city look's copies (they are
    // driven from the rig every frame by PolygonNpcVisual).
    private void FindBones()
    {
        searched = true;
        Animator animator = GetComponentInChildren<Animator>(true);
        Transform root = animator != null ? animator.transform : transform;
        PolygonNpcVisual visual = GetComponent<PolygonNpcVisual>();
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        head = Find(root, "Head", skip);
        neck = Find(root, "Neck", skip);
        if (head == null) { head = Find(transform, "Head", skip); neck = Find(transform, "Neck", skip); }
    }

    private static Transform Find(Transform node, string name, Transform skip)
    {
        if (node == skip || node.name.StartsWith("City look")) return null;
        if (node.name == name) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform hit = Find(node.GetChild(i), name, skip);
            if (hit != null) return hit;
        }
        return null;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }
}
