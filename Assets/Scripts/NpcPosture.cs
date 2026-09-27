using UnityEngine;

/// <summary>
/// Small, slow changes of posture for a café NPC: a lean forward or back, a
/// tilt to one side, a twist - the way someone seated shifts their weight, or
/// someone standing in a queue rocks from one foot to the other. Applied to
/// the rig's spine bones (Abdomen, Torso, Chest) after the Animator has posed
/// them and before NpcLookAt turns the head and PolygonNpcVisual copies the
/// rig onto a city body, so every look gets it.
///
/// NpcSocial decides when (it asks for a shift, then for neutral again);
/// this component only eases between the asked-for angles so nothing snaps.
/// Angles are a few degrees at most: posture, not choreography.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(118)]
public sealed class NpcPosture : MonoBehaviour
{
    [Tooltip("Largest lean or tilt this component will ever apply, degrees.")]
    [SerializeField, Range(1f, 15f)] private float maxAngle = 8f;
    [Tooltip("Smoothing time of a posture change, seconds.")]
    [SerializeField, Range(.1f, 2f)] private float smoothing = .7f;

    private static readonly float[] Shares = { .4f, .35f, .25f };   // abdomen, torso, chest

    private Transform[] bones = new Transform[3];
    private bool searched;
    private Vector3 wanted, current, velocity;   // x lean (forward +), y twist (left +), z tilt (right +)
    private float swayAmplitude, swayPeriod = 7f, swayPhase;
    private float smoothingScale = 1f;

    /// <summary>The lean/twist/tilt applied right now, degrees (for checks and traces).</summary>
    public Vector3 Applied => current;
    public bool Neutral => wanted.sqrMagnitude < 1e-4f && swayAmplitude <= 0f;

    /// <summary>Ease to this posture: lean forward (+) / back (-), twist left (+) / right (-), tilt right (+) / left (-), degrees.</summary>
    public void Shift(float lean, float twist, float tilt, float settleSeconds = -1f)
    {
        wanted = new Vector3(Mathf.Clamp(lean, -maxAngle, maxAngle), Mathf.Clamp(twist, -maxAngle, maxAngle), Mathf.Clamp(tilt, -maxAngle, maxAngle));
        smoothingScale = settleSeconds > 0f ? settleSeconds / smoothing : 1f;
    }

    public void Relax(float settleSeconds = -1f) => Shift(0f, 0f, 0f, settleSeconds);

    /// <summary>A slow side-to-side weight shift while standing; 0 switches it off.</summary>
    public void Sway(float amplitudeDegrees, float periodSeconds = 7f)
    {
        if (swayAmplitude <= 0f && amplitudeDegrees > 0f) swayPhase = Random.value * Mathf.PI * 2f;
        swayAmplitude = Mathf.Clamp(amplitudeDegrees, 0f, 3f);
        swayPeriod = Mathf.Max(2f, periodSeconds);
    }

    private void LateUpdate()
    {
        if (!searched) FindBones();
        if (bones[0] == null && bones[1] == null && bones[2] == null) return;
        float dt = Time.deltaTime;
        current = Vector3.SmoothDamp(current, wanted, ref velocity, smoothing * smoothingScale, 40f, dt);
        Vector3 apply = current;
        if (swayAmplitude > 0f)
        {
            swayPhase += dt * Mathf.PI * 2f / swayPeriod;
            apply.z += Mathf.Sin(swayPhase) * swayAmplitude;
            apply.x += Mathf.Sin(swayPhase * .5f + 1.3f) * swayAmplitude * .25f;
        }
        if (apply.sqrMagnitude < 1e-6f) return;

        Vector3 right = Flat(transform.right), forward = Flat(transform.forward);
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null) continue;
            float share = Shares[i];
            Quaternion turn = Quaternion.AngleAxis(apply.y * share, Vector3.up)
                              * Quaternion.AngleAxis(apply.x * share, right)
                              * Quaternion.AngleAxis(apply.z * share, forward);
            bone.rotation = turn * bone.rotation;
        }
    }

    // The rig's own spine bones, never a city body's copies (they are driven
    // from the rig by PolygonNpcVisual).
    private void FindBones()
    {
        searched = true;
        Animator animator = GetComponentInChildren<Animator>(true);
        Transform root = animator != null ? animator.transform : transform;
        PolygonNpcVisual visual = GetComponent<PolygonNpcVisual>();
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        string[] names = { "Abdomen", "Torso", "Chest" };
        for (int i = 0; i < names.Length; i++)
        {
            bones[i] = Find(root, names[i], skip);
            if (bones[i] == null) bones[i] = Find(transform, names[i], skip);
        }
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
