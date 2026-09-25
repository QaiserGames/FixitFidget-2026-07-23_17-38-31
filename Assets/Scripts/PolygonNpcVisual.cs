using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A presentation-only POLYGON City body for walk-ins, patrons and street
/// neighbours. The actor keeps everything it already had: its own skeleton and
/// Animator, navigation, interaction targets, patience bar and speech bubble.
/// This component only hides the actor's skinned renderers and makes the
/// purchased body copy the actor's animated skeleton, bone by bone.
///
/// WHY A BONE FOLLOWER AND NOT UNITY'S HUMANOID RETARGETING
/// The café's walk, idle and interact clips come from the Quaternius rig, whose
/// feet hang from the rig root (IK-style) and whose legs are not children of
/// the hips. Unity's Humanoid avatar cannot describe that hierarchy, so those
/// clips cannot be turned into humanoid clips. Both rigs share a T-pose,
/// though, so each frame the follower measures how far every source bone has
/// turned away from its T-pose (in the character's own space) and turns the
/// matching POLYGON bone by the same amount from its T-pose. Hierarchy
/// differences stop mattering, and whatever the actor does - walk, idle,
/// interact, a head turn - the new body does too.
///
/// SITTING
/// The two skeletons share a T-pose but not their proportions: the city bodies
/// have shorter legs and longer arms and torsos, and their leg roots sit below
/// their Hips bone where the Quaternius rig's sit well above it. Copying turns
/// (and the Hips bone's travel) is right for walking, but on a chair it sinks
/// the body into the seat and the floor and pushes it back through the
/// backrest. So while NpcSeating reports the actor as seated, the follower also
/// fits the body to the chair, blending in as the rig's hips come down:
///  * its hip joints go where the rig's hip joints are (the rig's are fitted to
///    the café's chairs by the sit bake and NpcSeating);
///  * each foot is planted where the rig's foot is (two-bone leg IK);
///  * a hand that rests on the rig's thigh rests on the body's own thigh
///    (two-bone arm IK), instead of hanging through the chair;
///  * its shoulders stay square. The rig's sit clips roll its collarbones
///    back, and copied onto these longer collarbones that swung each shoulder
///    joint about 12 cm behind and 6 cm below the collarbone: the arms sank
///    into the torso and, seen from the front or from the game camera, the
///    shoulders seemed to disappear. While seated each collarbone is turned so
///    its shoulder joint keeps the direction it has in the body's own T-pose
///    (relative to the chest), and the arm IK then reaches the lap from there.
///
/// Named regulars keep their authored appearance. When the purchased art is
/// missing (for example a fresh clone of the public repository, which never
/// contains Synty files) nothing changes and the original body stays visible.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(150)]
public sealed class PolygonNpcVisual : MonoBehaviour
{
    [Tooltip("Presentation-only bodies (prefabs built by Fixit Fidget > City pack). Missing entries are skipped.")]
    [SerializeField] private GameObject[] appearancePrefabs = Array.Empty<GameObject>();
    [Tooltip("Share of anonymous walk-ins that keep the body they already have (the CC0 placeholder looks).")]
    [SerializeField, Range(0f, 1f)] private float keepOriginalShare;
    [Tooltip("-1 picks a look from the walk-in's name; 0 or more always uses that look (street neighbours).")]
    [SerializeField] private int fixedAppearance = -1;
    [Tooltip("0 sizes the new body so its hips sit at the actor's hip height (same overall height).")]
    [SerializeField, Min(0f)] private float visualScale;
    [Tooltip("While the actor sits, fit the body to the chair (hip joints, planted feet, hands resting on the thighs) " +
             "instead of only copying turns, which would sink it into the seat and push it through the backrest.")]
    [SerializeField] private bool fitWhileSeated = true;
    [Tooltip("Height of a resting wrist above the centre line of the thigh it rests on, in the actor's own units " +
             "(thigh thickness plus the hand under the wrist).")]
    [SerializeField, Range(0f, .3f)] private float restingWristAboveThigh = .14f;
    [Tooltip("How much higher than the rig's hip joints this body's sit while seated, in the actor's own units. The " +
             "city bodies' thighs are thicker than the rig's, so at exactly the rig's height they sink into the seat.")]
    [SerializeField, Range(0f, .1f)] private float seatedHipLift = .025f;
    [Tooltip("While seated, how far each collarbone is turned back to its own T-pose direction relative to the chest " +
             "(0 = copy the rig's collarbones, 1 = square shoulders). The rig's sit clips roll the collarbones back, " +
             "which on these bodies pulls the shoulders into the torso.")]
    [SerializeField, Range(0f, 1f)] private float seatedShoulderSettle = .9f;

    // Source (Quaternius rig) bone -> POLYGON bone, parents before children.
    public static readonly string[] SourceBones =
    {
        "Hips", "Abdomen", "Torso", "Chest", "Neck", "Head",
        "Shoulder.L", "UpperArm.L", "LowerArm.L", "Wrist.L",
        "Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R",
        "UpperLeg.L", "LowerLeg.L", "Foot.L",
        "UpperLeg.R", "LowerLeg.R", "Foot.R",
    };
    public static readonly string[] TargetBones =
    {
        "Hips", "Spine_01", "Spine_02", "Spine_03", "Neck", "Head",
        "Clavicle_L", "Shoulder_L", "Elbow_L", "Hand_L",
        "Clavicle_R", "Shoulder_R", "Elbow_R", "Hand_R",
        "UpperLeg_L", "LowerLeg_L", "Ankle_L",
        "UpperLeg_R", "LowerLeg_R", "Ankle_R",
    };

    // Indexes into SourceBones / TargetBones.
    private const int Hips = 0, Chest = 3, ClavicleL = 6, UpperArmL = 7, LowerArmL = 8, WristL = 9,
        ClavicleR = 10, UpperArmR = 11, LowerArmR = 12, WristR = 13,
        UpperLegL = 14, LowerLegL = 15, FootL = 16, UpperLegR = 17, LowerLegR = 18, FootR = 19;

    private static uint anonymousSequence;
    private GameObject instance;
    private SkinnedMeshRenderer[] hiddenRenderers;
    private bool[] hiddenWasEnabled;
    private Animator sourceAnimator;
    private AnimatorCullingMode sourceCulling;
    private Transform[] source, target;
    private Quaternion[] sourceBind, targetBind;
    // T-pose positions: the actor's rig in the actor's space, the new body in its own (unscaled) space.
    private Vector3[] sourceBindPosition, targetBindPosition;
    private float hipsScale = 1f, bodyScale = 1f;

    public int AppearanceCount => appearancePrefabs.Length;
    public int FixedAppearance => fixedAppearance;
    public float KeepOriginalShare => keepOriginalShare;
    public int ActiveAppearance { get; private set; } = -1;
    public string ActiveAppearanceName => instance != null ? appearancePrefabs[ActiveAppearance].name : "Original";
    public GameObject VisualInstance => instance;
    /// <summary>
    /// Set by NpcSeating from the first step towards a chair until the NPC is back on its feet (and by the
    /// editor's sit tools). The chair fitting only blends in while this is on and the rig's hips are low.
    /// </summary>
    public bool Seated { get; set; }
    /// <summary>How much higher the body's hips sit than they would from copying alone, metres (0 standing).</summary>
    public float SeatedLift { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSequence() => anonymousSequence = 0;

    public void Configure(GameObject[] choices, float keepOriginal, int fixedIndex, float scale = 0f)
    {
        appearancePrefabs = choices ?? Array.Empty<GameObject>();
        keepOriginalShare = Mathf.Clamp01(keepOriginal);
        fixedAppearance = fixedIndex;
        visualScale = Mathf.Max(0f, scale);
    }

    private void Start()
    {
        // Spawners assign identity immediately after Instantiate, before Start.
        CustomerIdentity identity = GetComponent<CustomerIdentity>();
        if (identity != null && identity.IsRegular) return;
        int index = fixedAppearance;
        if (index < 0)
        {
            int count = appearancePrefabs.Length;
            if (count == 0) return;
            string name = identity != null ? identity.DisplayName : null;
            uint choice = !string.IsNullOrEmpty(name) && name != "Customer"
                ? StableHash(name) : (anonymousSequence++ + 1u) * 2654435761u;
            if (choice % 1000u < (uint)Mathf.RoundToInt(keepOriginalShare * 1000f)) return;
            index = (int)(choice / 1000u % (uint)count);
        }
        ApplyAppearance(index);
    }

    /// <summary>Swaps in look <paramref name="index"/>. Returns false (and changes nothing) when it can't.</summary>
    public bool ApplyAppearance(int index)
    {
        CustomerIdentity identity = GetComponent<CustomerIdentity>();
        if (identity != null && identity.IsRegular) return false;
        if (index < 0 || index >= appearancePrefabs.Length || appearancePrefabs[index] == null) return false;
        RemoveAppearance();

        var renderers = new List<SkinnedMeshRenderer>(GetComponentsInChildren<SkinnedMeshRenderer>(true));
        if (!BindSkeleton(transform, renderers, SourceBones, out source, out sourceBind, out sourceBindPosition)) return false;

        instance = Instantiate(appearancePrefabs[index], transform, false);
        instance.name = "City look - " + appearancePrefabs[index].name;
        // Editor previews and photos must never write the body into the scene file.
        if (!Application.isPlaying)
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.DontSave;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        var visualRenderers = new List<SkinnedMeshRenderer>(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        if (!BindSkeleton(instance.transform, visualRenderers, TargetBones, out target, out targetBind, out targetBindPosition)
            || targetBindPosition[Hips].y < 1e-3f || sourceBindPosition[Hips].y < 1e-3f)
        {
            DestroyInstance();
            return false;
        }
        // Bind poses are in each root's own units, so sizing the body afterwards
        // leaves them valid; hipsScale converts the actor's hip sway into them.
        float scale = visualScale > 0f ? visualScale : sourceBindPosition[Hips].y / targetBindPosition[Hips].y;
        instance.transform.localScale = Vector3.one * scale;
        hipsScale = 1f / scale;
        bodyScale = scale;

        hiddenRenderers = renderers.ToArray();
        hiddenWasEnabled = new bool[hiddenRenderers.Length];
        for (int i = 0; i < hiddenRenderers.Length; i++)
        {
            hiddenWasEnabled[i] = hiddenRenderers[i].enabled;
            hiddenRenderers[i].enabled = false;
        }
        // Invisible skinned meshes would otherwise let the source Animator stop
        // writing bones, freezing the new body mid-stride.
        sourceAnimator = GetComponentInChildren<Animator>(true);
        if (sourceAnimator != null && sourceAnimator.gameObject.GetComponentInParent<PolygonNpcVisual>() == this
            && !sourceAnimator.transform.IsChildOf(instance.transform))
        {
            sourceCulling = sourceAnimator.cullingMode;
            sourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else sourceAnimator = null;
        ActiveAppearance = index;
        Follow();
        return true;
    }

    private void LateUpdate() => Follow();

    /// <summary>Poses the new body from the actor's skeleton. Public for editor line-ups.</summary>
    public void Follow()
    {
        SeatedLift = 0f;
        if (instance == null || source == null || target == null) return;
        Quaternion inverseRoot = Quaternion.Inverse(transform.rotation);
        Quaternion visualRoot = instance.transform.rotation;
        if (source[Hips] != null && target[Hips] != null)
        {
            Vector3 now = transform.InverseTransformPoint(source[Hips].position);
            target[Hips].position = instance.transform.TransformPoint(targetBindPosition[Hips] + (now - sourceBindPosition[Hips]) * hipsScale);
        }
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == null || target[i] == null) continue;
            Quaternion turned = inverseRoot * source[i].rotation * Quaternion.Inverse(sourceBind[i]);
            target[i].rotation = visualRoot * turned * targetBind[i];
        }
        float seated = SeatedWeight();
        if (seated > 0f) FitToChair(seated);
    }

    // 0 while standing or walking, rising to 1 as the rig's hip joints come
    // down towards a seat (they end at about half their standing height).
    private float SeatedWeight()
    {
        if (!fitWhileSeated || !Seated) return 0f;
        if (source[UpperLegL] == null || source[UpperLegR] == null || target[UpperLegL] == null || target[UpperLegR] == null) return 0f;
        float standing = (sourceBindPosition[UpperLegL].y + sourceBindPosition[UpperLegR].y) * .5f;
        if (standing < 1e-3f) return 0f;
        float now = transform.InverseTransformPoint((source[UpperLegL].position + source[UpperLegR].position) * .5f).y;
        float t = Mathf.InverseLerp(standing * .9f, standing * .62f, now);
        return t * t * (3f - 2f * t);
    }

    private void FitToChair(float weight)
    {
        // Hip joints onto the rig's, which sit a thigh's thickness above the seat
        // (plus a little, for this body's thicker thighs).
        Vector3 rigHips = (source[UpperLegL].position + source[UpperLegR].position) * .5f
                        + transform.up * (seatedHipLift * transform.lossyScale.y);
        Vector3 bodyHips = (target[UpperLegL].position + target[UpperLegR].position) * .5f;
        Vector3 shift = (rigHips - bodyHips) * weight;
        target[Hips].position += shift;
        SeatedLift = Vector3.Dot(shift, transform.up);

        PlantFoot(UpperLegL, LowerLegL, FootL, weight);
        PlantFoot(UpperLegR, LowerLegR, FootR, weight);
        SettleShoulder(ClavicleL, UpperArmL, LowerArmL, WristL, weight);
        SettleShoulder(ClavicleR, UpperArmR, LowerArmR, WristR, weight);
        RestHand(UpperArmL, LowerArmL, WristL, UpperLegL, LowerLegL, weight);
        RestHand(UpperArmR, LowerArmR, WristR, UpperLegR, LowerLegR, weight);
    }

    // Turn the collarbone (swing only) so the shoulder joint lies in the direction
    // it has in this body's T-pose, measured from the chest as it is now. The arm
    // is the collarbone's child and would swing with it, so it is solved back to
    // reach the hand it had: gestures keep their place (and stay off the table),
    // and RestHand then moves a resting hand onto the lap from there.
    private void SettleShoulder(int clavicle, int upperArm, int lowerArm, int wrist, float weight)
    {
        float w = weight * seatedShoulderSettle;
        Transform collar = target[clavicle], shoulder = target[upperArm], chest = target[Chest];
        Transform elbow = target[lowerArm], hand = target[wrist];
        if (w <= 0f || collar == null || shoulder == null || chest == null || elbow == null || hand == null) return;
        Vector3 now = shoulder.position - collar.position;
        if (now.sqrMagnitude < 1e-10f) return;
        Quaternion bodyRoot = instance.transform.rotation;
        // How far the chest has turned from its own T-pose, in world space.
        Quaternion chestTurn = chest.rotation * Quaternion.Inverse(bodyRoot * targetBind[Chest]);
        Vector3 want = chestTurn * (bodyRoot * (targetBindPosition[upperArm] - targetBindPosition[clavicle]));
        if (want.sqrMagnitude < 1e-10f) return;
        Vector3 handBefore = hand.position;
        Quaternion handTurn = hand.rotation;
        Quaternion swing = Quaternion.FromToRotation(now, want);
        collar.rotation = Quaternion.Slerp(Quaternion.identity, swing, w) * collar.rotation;
        SolveTwoBone(shoulder, elbow, hand, handBefore, -transform.forward);
        hand.rotation = handTurn;
    }

    // The body's ankle goes over the rig's foot (which stands on the floor):
    // the rig's foot bone plus the T-pose offset from it to this body's ankle,
    // turned with the foot. The foot keeps the turn it copied.
    private void PlantFoot(int upperLeg, int lowerLeg, int foot, float weight)
    {
        Transform hip = target[upperLeg], knee = target[lowerLeg], ankle = target[foot];
        if (hip == null || knee == null || ankle == null || source[foot] == null) return;
        Quaternion turned = Quaternion.Inverse(transform.rotation) * source[foot].rotation * Quaternion.Inverse(sourceBind[foot]);
        Vector3 offset = turned * (targetBindPosition[foot] * bodyScale - sourceBindPosition[foot]);
        Vector3 goal = transform.TransformPoint(transform.InverseTransformPoint(source[foot].position) + offset);
        Quaternion footTurn = ankle.rotation;
        SolveTwoBone(hip, knee, ankle, Vector3.Lerp(ankle.position, goal, weight), transform.forward);
        ankle.rotation = footTurn;
    }

    // When the rig's wrist lies on its thigh (a hand resting on the lap), put
    // this body's wrist on the same spot of its own thigh, lifted by the thigh's
    // thickness. Gestures (wrist well away from the thigh) are left as copied.
    private void RestHand(int upperArm, int lowerArm, int wrist, int upperLeg, int lowerLeg, float weight)
    {
        Transform shoulder = target[upperArm], elbow = target[lowerArm], hand = target[wrist];
        Transform bodyHip = target[upperLeg], bodyKnee = target[lowerLeg];
        if (shoulder == null || elbow == null || hand == null || bodyHip == null || bodyKnee == null) return;
        if (source[wrist] == null || source[upperLeg] == null || source[lowerLeg] == null) return;
        float unit = Mathf.Max(1e-4f, transform.lossyScale.y);
        Vector3 up = transform.up;

        Vector3 rigHip = source[upperLeg].position, rigThigh = source[lowerLeg].position - rigHip, rigWrist = source[wrist].position;
        if (rigThigh.sqrMagnitude < 1e-8f) return;
        float along = Mathf.Clamp01(Vector3.Dot(rigWrist - rigHip, rigThigh) / rigThigh.sqrMagnitude);
        Vector3 rigOnThigh = rigHip + rigThigh * along;
        float resting = Mathf.InverseLerp(.24f, .16f, Vector3.Distance(rigWrist, rigOnThigh) / unit);
        float w = weight * resting * resting * (3f - 2f * resting);
        if (w <= 0f) return;
        ThighFrame(rigThigh, up, out _, out Vector3 rigSide);
        float sideways = Vector3.Dot(rigWrist - rigOnThigh, rigSide);

        Vector3 thigh = bodyKnee.position - bodyHip.position;
        if (thigh.sqrMagnitude < 1e-8f) return;
        ThighFrame(thigh, up, out Vector3 lift, out Vector3 side);
        Vector3 goal = bodyHip.position + thigh * Mathf.Clamp(along, .2f, .85f)
                     + lift * (restingWristAboveThigh * unit) + side * sideways;
        Quaternion handTurn = hand.rotation;
        SolveTwoBone(shoulder, elbow, hand, Vector3.Lerp(hand.position, goal, w), -transform.forward);
        hand.rotation = handTurn;
    }

    // "Up" square to the thigh (towards the sky) and "sideways" square to both.
    private static void ThighFrame(Vector3 thigh, Vector3 up, out Vector3 lift, out Vector3 side)
    {
        Vector3 axis = thigh.normalized;
        lift = up - Vector3.Dot(up, axis) * axis;
        lift = lift.sqrMagnitude > 1e-8f ? lift.normalized : up;
        side = Vector3.Cross(lift, axis);
    }

    // Two-bone IK: bend at the middle joint so the end reaches the goal, keeping
    // the middle joint on the side it already bends towards.
    private static void SolveTwoBone(Transform upper, Transform middle, Transform end, Vector3 goal, Vector3 fallbackBend)
    {
        Vector3 a = upper.position, b = middle.position, c = end.position;
        float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
        if (l1 < 1e-5f || l2 < 1e-5f) return;
        Vector3 toGoal = goal - a;
        float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        Vector3 n = toGoal.sqrMagnitude > 1e-10f ? toGoal.normalized : (c - a).normalized;
        Vector3 bend = (b - a) - Vector3.Dot(b - a, n) * n;
        if (bend.sqrMagnitude < 1e-8f) bend = fallbackBend - Vector3.Dot(fallbackBend, n) * n;
        if (bend.sqrMagnitude < 1e-8f) return;
        bend.Normalize();
        float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 joint = a + n * x + bend * h;
        upper.rotation = Quaternion.FromToRotation(b - a, joint - a) * upper.rotation;
        Vector3 reached = end.position;
        middle.rotation = Quaternion.FromToRotation(reached - middle.position, a + n * d - middle.position) * middle.rotation;
    }

    public void RemoveAppearance()
    {
        if (hiddenRenderers != null)
            for (int i = 0; i < hiddenRenderers.Length; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = hiddenWasEnabled[i];
        hiddenRenderers = null;
        if (sourceAnimator != null) sourceAnimator.cullingMode = sourceCulling;
        sourceAnimator = null;
        DestroyInstance();
        source = target = null;
        ActiveAppearance = -1;
    }

    private void OnDestroy() => RemoveAppearance();

    private void DestroyInstance()
    {
        if (instance == null) return;
        instance.SetActive(false);
        if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
        instance = null;
    }

    // T-pose of each named bone, in <root>'s space, taken from the skinned
    // meshes' bind poses - exact regardless of what pose the rig is in now.
    private static bool BindSkeleton(Transform root, List<SkinnedMeshRenderer> renderers, string[] names,
        out Transform[] bones, out Quaternion[] bind, out Vector3[] bindPosition)
    {
        bones = new Transform[names.Length];
        bind = new Quaternion[names.Length];
        bindPosition = new Vector3[names.Length];
        int found = 0;
        for (int n = 0; n < names.Length; n++)
        {
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                Transform[] rendererBones = renderer.bones;
                Matrix4x4[] poses = renderer.sharedMesh.bindposes;
                int b = Array.FindIndex(rendererBones, t => t != null && t.name == names[n]);
                if (b < 0 || b >= poses.Length) continue;
                Matrix4x4 model = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix * poses[b].inverse;
                bones[n] = rendererBones[b];
                bind[n] = model.rotation;
                bindPosition[n] = model.GetColumn(3);
                found++;
                break;
            }
        }
        // Hips, spine, arms and legs must all be present for a faithful copy.
        return found == names.Length;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
