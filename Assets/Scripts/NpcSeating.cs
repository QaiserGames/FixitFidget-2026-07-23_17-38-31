using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Sits a café NPC (customer or patron) on a TableSeat's chair and stands them
/// up again.
///
/// HOW IT FITS WITH THE BRAINS
/// The brains still decide everything. They walk the NPC to the seat's stand
/// point exactly as before and then call TrySit. From there this component owns
/// the visible body: it steps round the side of the chair to the spot in front
/// of it, turns to the table and plays the sit-down clip. The navigation body
/// (the NavMeshAgent) stays parked on the stand point the whole time, so crowd
/// avoidance and the brains' own checks keep seeing a person standing there.
///
/// Standing up needs no call. The moment a brain gives the agent somewhere to
/// walk (leaving, storming out, re-pathing), this component notices, holds the
/// agent still, plays the stand-up clip, walks back round the chair to the
/// stand point and only then lets the agent go. While getting up it keeps the
/// chair reserved, so nobody heads for a chair that is still occupied.
///
/// Needs the "Seated" and "Talking" animator parameters and the sit states that
/// Fixit Fidget > NPC > Sit 2 - Wire sitting adds, plus a TableSeat with Snap
/// To Seat on. Without them TrySit returns false and the NPC stands beside the
/// chair exactly as it used to.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)] // after the brains, so this has the last word on the body each frame
public sealed class NpcSeating : MonoBehaviour
{
    public enum Phase { Standing, Approaching, SittingDown, Seated, StandingUp, Returning }
    /// <summary>Thigh thickness the sit clips were baked for (Sit 1 fits them to the café's chairs).</summary>
    public const float DefaultHipAboveSeat = .085f;
    /// <summary>
    /// Where the hip joints land along the seat: 1 cm in front of its centre. Measured with Sit 5 across every
    /// body look: from here back, the bulkier city bodies' shoulders, arms and backs pass through the timber
    /// chairs' backrest; further forward, feet and fingers start touching the table.
    /// </summary>
    public const float DefaultHipBehindSeatCentre = -.01f;

    [SerializeField] private NpcSitData data;
    [Tooltip("Walking speed while stepping round the chair, metres per second.")]
    [SerializeField, Min(.2f)] private float stepSpeed = 1.15f;
    [Tooltip("How far beside the chair's centre the NPC passes on the way in and out, metres.")]
    [SerializeField, Min(.2f)] private float sideClearance = .62f;
    [Tooltip("The hip joints sit this far above the seat surface (thigh thickness), metres at scale 1.")]
    [SerializeField, Range(0f, .2f)] private float hipAboveSeat = DefaultHipAboveSeat;
    [Tooltip("The hip joints sit this far behind the seat's centre, metres.")]
    [SerializeField, Range(-.2f, .3f)] private float hipBehindSeatCentre = DefaultHipBehindSeatCentre;
    [Tooltip("Only sit when the NPC is standing this close to the seat's stand point, metres. Someone the brains " +
             "settled further away (a crowd gave up on their spot) keeps standing where they are instead of being " +
             "walked through furniture by hand.")]
    [SerializeField, Min(.1f)] private float maxStartDistance = .75f;
    [Tooltip("Largest height change allowed to meet the chair. Beyond it the feet would visibly float or sink, " +
             "so the rest of the difference is left to the chair.")]
    [SerializeField, Range(0f, .25f)] private float maxHeightCorrection = .12f;
    [Tooltip("While the NPC sits, its navigation body waits on the stand point in the aisle beside the chair. It " +
             "shrinks to this radius there, so people can walk past the chair; the full radius comes back when " +
             "the NPC stands up.")]
    [SerializeField, Range(.05f, .35f)] private float seatedAgentRadius = .15f;
    [Tooltip("Chance that a seated NPC with company at the same table is talking, re-rolled every few seconds.")]
    [SerializeField, Range(0f, 1f)] private float chatWithCompany = .55f;
    [Tooltip("Chance that a seated NPC alone at a table is talking (on the phone, to themselves).")]
    [SerializeField, Range(0f, 1f)] private float chatAlone = .12f;

    private static readonly int SeatedHash = Animator.StringToHash("Seated");
    private static readonly int TalkingHash = Animator.StringToHash("Talking");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly List<NpcSeating> active = new();

    private NavMeshAgent agent;
    private Animator animator;
    private PolygonNpcVisual visual;
    private TableSeat seat;
    private Phase phase = Phase.Standing;
    // Stand point, beside the chair, in front of the chair (where the feet stay while seated).
    private readonly Vector3[] path = new Vector3[3];
    private Quaternion seatRotation;
    private float phaseStarted, phaseLength, nextChat;
    private bool reserved;
    private bool? hasSitParameters;
    private float standingRadius = -1f;
    // Patience bar and speech bubble ride lower while seated, over the seated head.
    private Transform[] overheads = System.Array.Empty<Transform>();
    private Vector3[] overheadRest = System.Array.Empty<Vector3>();
    private float overheadDrop;

    public Phase Current => phase;
    /// <summary>True from the first step towards the chair until the agent has been handed back.</summary>
    public bool Busy => phase != Phase.Standing;
    public bool IsSeated => phase == Phase.SittingDown || phase == Phase.Seated;
    public TableSeat Seat => seat;
    public NpcSitData Data { get => data; set => data = value; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        visual = GetComponent<PolygonNpcVisual>();
        var found = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name == "PatienceBar" || child.name == "SpeechBubble") found.Add(child);
        overheads = found.ToArray();
        overheadRest = new Vector3[overheads.Length];
        for (int i = 0; i < overheads.Length; i++) overheadRest[i] = overheads[i].localPosition;
    }

    public bool CanSit(TableSeat target)
    {
        if (target == null || !target.SnapToSeat || data == null || animator == null) return false;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
        if (hasSitParameters == null && animator.isInitialized)
            hasSitParameters = HasParameter(SeatedHash) && HasParameter(TalkingHash);
        return hasSitParameters == true;
    }

    /// <summary>Starts sitting on <paramref name="target"/>. The NPC must be standing at its stand point.</summary>
    public bool TrySit(TableSeat target)
    {
        if (phase != Phase.Standing || !CanSit(target)) return false;
        Vector3 fromStandPoint = transform.position - target.StandPoint.position;
        fromStandPoint.y = 0f;
        if (fromStandPoint.sqrMagnitude > maxStartDistance * maxStartDistance) return false;
        seat = target;
        float floor = transform.position.y;
        Placement(seat, floor, out Vector3 feet, out seatRotation);
        Vector3 seatCentre = seat.SeatPose.position;
        Vector3 forward = seatRotation * Vector3.forward;

        // Round whichever side of the chair has nobody sitting next to it.
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 beside = seatCentre + side * (ChooseSide(seatCentre, side) * sideClearance) + forward * .08f;
        beside.y = floor;
        path[0] = transform.position;
        path[1] = beside;
        path[2] = feet;
        overheadDrop = Mathf.Max(0f, data.standingHipHeight - data.seatedHip.y);

        // Park the navigation body on the stand point; the visible body moves by hand from here.
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updatePosition = false;
        agent.updateRotation = false;
        standingRadius = agent.radius;
        agent.radius = Mathf.Min(agent.radius, seatedAgentRadius);
        active.Add(this);
        // A city body fits itself to the chair while the rig's hips are down.
        if (visual != null) visual.Seated = true;
        Begin(Phase.Approaching, PathLength() / stepSpeed);
        return true;
    }

    /// <summary>
    /// Where this NPC's feet go, and which way it faces, to sit on
    /// <paramref name="target"/>: the clip's hip joints land just behind the
    /// seat's centre, a thigh's thickness above it, facing the seat's cup spot.
    /// Also used by the editor photo check.
    /// </summary>
    public void Placement(TableSeat target, float floor, out Vector3 feet, out Quaternion facing)
    {
        Vector3 seatCentre = target.SeatPose.position;
        Vector3 toTable = target.CupSpot.position - seatCentre;
        toTable.y = 0f;
        if (toTable.sqrMagnitude < 1e-4f) { toTable = seatCentre - target.StandPoint.position; toTable.y = 0f; }
        if (toTable.sqrMagnitude < 1e-4f) toTable = transform.forward;
        Vector3 forward = toTable.normalized;
        facing = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 scale = transform.lossyScale;
        Vector3 hipOffset = facing * Vector3.Scale(data != null ? data.seatedHip : new Vector3(0f, .5f, -.26f), scale);
        Vector3 hipTarget = seatCentre - forward * hipBehindSeatCentre;
        hipTarget.y = seatCentre.y + hipAboveSeat * scale.y;
        feet = hipTarget - hipOffset;
        feet.y = floor + Mathf.Clamp(feet.y - floor, -maxHeightCorrection, maxHeightCorrection);
    }

    /// <summary>Gets up and walks back to the stand point. Called automatically when the agent is given a path.</summary>
    public void StandUp()
    {
        switch (phase)
        {
            case Phase.Approaching:
                // Never sat down: head straight back from wherever the body is.
                path[1] = path[2] = transform.position;
                Begin(Phase.Returning, PathLength() / stepSpeed);
                break;
            case Phase.SittingDown:
            case Phase.Seated:
                animator.SetBool(SeatedHash, false);
                animator.SetBool(TalkingHash, false);
                Begin(Phase.StandingUp, data.exitLength);
                break;
        }
    }

    private void Update()
    {
        if (phase == Phase.Standing) return;
        if (agent == null || !agent.isActiveAndEnabled || animator == null) { HandBack(); return; }

        // A brain that gives the agent somewhere to go wants the NPC on their feet.
        // Hold the navigation body still until it is handed back.
        bool wantsToWalk = agent.isOnNavMesh && (agent.hasPath || agent.pathPending);
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (wantsToWalk) StandUp();
        KeepSeatReserved();

        float t = phaseLength <= 1e-4f ? 1f : Mathf.Clamp01((Time.time - phaseStarted) / phaseLength);
        switch (phase)
        {
            case Phase.Approaching:
                FollowPath(t, true);
                animator.SetBool(IsWalkingHash, t < 1f);
                if (t >= 1f)
                {
                    animator.SetBool(SeatedHash, true);
                    Begin(Phase.SittingDown, data.enterLength);
                }
                break;
            case Phase.SittingDown:
                Hold();
                if (t >= 1f)
                {
                    Begin(Phase.Seated, 0f);
                    nextChat = Time.time + Random.Range(1.5f, 4f);
                }
                break;
            case Phase.Seated:
                Hold();
                Chat();
                break;
            case Phase.StandingUp:
                Hold();
                if (t >= 1f) Begin(Phase.Returning, PathLength() / stepSpeed);
                break;
            case Phase.Returning:
                FollowPath(t, false);
                animator.SetBool(IsWalkingHash, t < 1f);
                if (t >= 1f) HandBack();
                break;
        }
        PlaceOverheads();
    }

    private void Begin(Phase next, float length)
    {
        phase = next;
        phaseStarted = Time.time;
        phaseLength = Mathf.Max(0f, length);
        if (next != Phase.Approaching && next != Phase.Returning) animator.SetBool(IsWalkingHash, false);
    }

    private void Hold() => transform.SetPositionAndRotation(path[2], seatRotation);

    // Walk the two legs of the path by arc length, facing the way of travel and
    // turning to (or from) the table near the chair.
    private void FollowPath(float t, bool towardSeat)
    {
        float a = Vector3.Distance(path[0], path[1]);
        float b = Vector3.Distance(path[1], path[2]);
        float s = (towardSeat ? t : 1f - t) * (a + b);
        Vector3 position = s <= a
            ? Vector3.Lerp(path[0], path[1], a > 1e-4f ? s / a : 1f)
            : Vector3.Lerp(path[1], path[2], b > 1e-4f ? (s - a) / b : 1f);
        Vector3 travel = s <= a ? path[1] - path[0] : path[2] - path[1];
        if (!towardSeat) travel = -travel;
        travel.y = 0f;
        Quaternion facing = travel.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(travel.normalized) : transform.rotation;
        float settle = towardSeat ? Mathf.InverseLerp(.72f, 1f, t) : Mathf.InverseLerp(.28f, 0f, t);
        Quaternion target = Quaternion.Slerp(facing, seatRotation, settle);
        transform.position = position;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
    }

    private float PathLength() => Mathf.Max(.15f, Vector3.Distance(path[0], path[1]) + Vector3.Distance(path[1], path[2]));

    private void Chat()
    {
        if (Time.time < nextChat) return;
        nextChat = Time.time + Random.Range(4f, 9f);
        bool company = false;
        foreach (NpcSeating other in active)
        {
            if (other == this || !other.IsSeated || other.seat == null || seat == null) continue;
            if ((other.seat.SeatPose.position - seat.SeatPose.position).sqrMagnitude < 2.2f * 2.2f) { company = true; break; }
        }
        animator.SetBool(TalkingHash, Random.value < (company ? chatWithCompany : chatAlone));
    }

    // +1 passes the chair on the seated person's right, -1 on their left.
    private float ChooseSide(Vector3 centre, Vector3 side)
    {
        int right = 0, left = 0;
        foreach (TableSeat other in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (other == seat || !other.IsOccupied) continue;
            Vector3 d = other.SeatPose.position - centre;
            d.y = 0f;
            if (d.sqrMagnitude > 1.8f * 1.8f || d.sqrMagnitude < 1e-4f) continue;
            float along = Vector3.Dot(d.normalized, side);
            if (along > .2f) right++;
            else if (along < -.2f) left++;
        }
        if (right != left) return right < left ? 1f : -1f;
        return Vector3.Dot(transform.position - centre, side) >= 0f ? 1f : -1f;
    }

    private void KeepSeatReserved()
    {
        if (seat == null || reserved) return;
        if ((phase == Phase.StandingUp || phase == Phase.Returning) && seat.Occupant == null && seat.Claim(this))
            reserved = true;
    }

    private void ReleaseReservation()
    {
        if (reserved && seat != null) seat.Release(this);
        reserved = false;
    }

    private void PlaceOverheads()
    {
        float amount = phase switch
        {
            Phase.SittingDown => Mathf.Clamp01((Time.time - phaseStarted) / Mathf.Max(.01f, phaseLength)),
            Phase.Seated => 1f,
            Phase.StandingUp => 1f - Mathf.Clamp01((Time.time - phaseStarted) / Mathf.Max(.01f, phaseLength)),
            _ => 0f,
        };
        // A city body fitted to the chair sits higher than the rig; keep the bar over its head.
        float drop = overheadDrop * amount;
        if (visual != null) drop = Mathf.Max(0f, drop - visual.SeatedLift / Mathf.Max(1e-4f, transform.lossyScale.y));
        for (int i = 0; i < overheads.Length; i++)
            if (overheads[i] != null)
                overheads[i].localPosition = overheadRest[i] + Vector3.down * drop;
    }

    // Returns the body to the navigation agent, which never left the stand point.
    private void HandBack()
    {
        if (phase == Phase.Standing) return;
        phase = Phase.Standing;
        transform.position = path[0];
        if (agent != null && standingRadius > 0f) agent.radius = standingRadius;
        standingRadius = -1f;
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh) agent.nextPosition = transform.position;
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (agent.isOnNavMesh) agent.isStopped = !(agent.hasPath || agent.pathPending);
        }
        if (animator != null)
        {
            animator.SetBool(SeatedHash, false);
            animator.SetBool(TalkingHash, false);
        }
        ReleaseReservation();
        active.Remove(this);
        seat = null;
        if (visual != null) visual.Seated = false;
        PlaceOverheads();
    }

    private bool HasParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash) return true;
        return false;
    }

    private void OnDisable()
    {
        if (phase != Phase.Standing) HandBack();
    }

    private void OnDestroy()
    {
        ReleaseReservation();
        active.Remove(this);
    }
}
