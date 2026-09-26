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
/// of it, turns to the table and plays the sit-down clip.
///
/// THE NAVIGATION BODY LEAVES THE FLOOR WHILE SEATED (pass 1, 26 Sept 2026).
/// It used to stay parked on the stand point in the aisle - stopped, at the
/// highest avoidance priority - for the whole sit. Stand points sit on the
/// corners everyone else has to walk round, so a seated person was an
/// invisible wall on the corner: the cause of most of the stalls and the
/// orbiting seen in the observation recordings. Now the NavMeshAgent is
/// switched off from the first step towards the chair until the NPC is back
/// on the stand point. The visible body is inside the table's Not Walkable
/// footprint, so nobody can path through it; the seat itself stays claimed
/// (WaitingSpot.Occupant) so nobody targets it.
///
/// Standing up is an explicit handshake: NpcLocomotion.MoveTo asks
/// <see cref="RequestStand"/>, this component plays the stand-up clip, walks
/// back round the chair to the stand point, switches the agent on there and
/// then runs the caller's continuation. While getting up it keeps the chair
/// reserved, so nobody heads for a chair that is still occupied.
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
    private NpcLocomotion locomotion;
    private Vector3 lastStepPosition;
    private TableSeat seat;
    private Phase phase = Phase.Standing;
    // Stand point, beside the chair, in front of the chair (where the feet stay while seated).
    private readonly Vector3[] path = new Vector3[3];
    private Quaternion seatRotation;
    private float phaseStarted, phaseLength, nextChat;
    private bool reserved;
    private bool? hasSitParameters;
    // Patience bar and speech bubble ride lower while seated, over the seated head.
    private Transform[] overheads = System.Array.Empty<Transform>();
    private Vector3[] overheadRest = System.Array.Empty<Vector3>();
    private float overheadDrop;
    // Who asked us to stand up, to be told when the agent is back on the floor.
    private System.Action whenStanding;

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
        locomotion = GetComponent<NpcLocomotion>();
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

        // The navigation body leaves the floor: from here the visible body moves
        // by hand, and nobody else's avoidance sees a person on the stand point.
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.enabled = false;
        active.Add(this);
        // A city body fits itself to the chair while the rig's hips are down.
        if (visual != null) visual.Seated = true;
        Begin(Phase.Approaching, StepTime());
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

    /// <summary>
    /// Get up and walk back to the stand point, then run <paramref name="whenBack"/>
    /// once the agent is on the floor again. False when the NPC is not in a chair
    /// (nothing to wait for; the caller can move at once).
    /// </summary>
    public bool RequestStand(System.Action whenBack)
    {
        if (phase == Phase.Standing) return false;
        whenStanding = whenBack;
        StandUp();
        return true;
    }

    /// <summary>Gets up and walks back to the stand point.</summary>
    public void StandUp()
    {
        switch (phase)
        {
            case Phase.Approaching:
                // Never sat down: head straight back from wherever the body is.
                path[1] = path[2] = transform.position;
                Begin(Phase.Returning, StepTime());
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
        if (agent == null || animator == null) { HandBack(); return; }
        KeepSeatReserved();

        float t = phaseLength <= 1e-4f ? 1f : Mathf.Clamp01((Time.time - phaseStarted) / phaseLength);
        switch (phase)
        {
            case Phase.Approaching:
                FollowPath(t, true);
                WalkClip();
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
                if (t >= 1f) Begin(Phase.Returning, StepTime());
                break;
            case Phase.Returning:
                FollowPath(t, false);
                WalkClip();
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
        lastStepPosition = transform.position;
        if (next != Phase.Approaching && next != Phase.Returning) animator.SetBool(IsWalkingHash, false);
    }

    private void Hold() => transform.SetPositionAndRotation(path[2], seatRotation);

    // Walk the two legs of the path by arc length - easing in from standing and
    // out to a stop, the way a person takes two steps - facing the way of
    // travel and turning to (or from) the table over the last part of the way.
    private void FollowPath(float t, bool towardSeat)
    {
        float a = Vector3.Distance(path[0], path[1]);
        float b = Vector3.Distance(path[1], path[2]);
        float eased = t * t * (3f - 2f * t);
        float s = (towardSeat ? eased : 1f - eased) * (a + b);
        Vector3 position = s <= a
            ? Vector3.Lerp(path[0], path[1], a > 1e-4f ? s / a : 1f)
            : Vector3.Lerp(path[1], path[2], b > 1e-4f ? (s - a) / b : 1f);
        Vector3 travel = s <= a ? path[1] - path[0] : path[2] - path[1];
        if (!towardSeat) travel = -travel;
        travel.y = 0f;
        Quaternion facing = travel.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(travel.normalized) : transform.rotation;
        float settle = towardSeat ? Mathf.InverseLerp(.6f, 1f, t) : Mathf.InverseLerp(.42f, 0f, t);
        Quaternion target = Quaternion.Slerp(facing, seatRotation, settle);
        transform.position = position;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
    }

    // The walk clip follows the body's real speed (through NpcLocomotion, so
    // the stride matches the floor), not a flag that flips at the ends.
    private void WalkClip()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float speed = Vector3.Distance(transform.position, lastStepPosition) / dt;
        lastStepPosition = transform.position;
        if (locomotion != null) locomotion.DriveWalk(speed, dt);
        else animator.SetBool(IsWalkingHash, speed > .1f);
    }

    private float PathLength() => Mathf.Max(.15f, Vector3.Distance(path[0], path[1]) + Vector3.Distance(path[1], path[2]));

    // Eased in and out, so the middle of the path runs ~1.5x the average; the
    // extra fifth keeps that peak at an ordinary walking pace.
    private float StepTime() => PathLength() / stepSpeed * 1.2f;

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

    // Back on the stand point: the navigation agent returns to the floor there,
    // stopped, and whoever asked for the stand-up is told.
    private void HandBack()
    {
        if (phase == Phase.Standing) return;
        phase = Phase.Standing;
        transform.position = path[0];
        if (agent != null && agent.gameObject.activeInHierarchy)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(path[0]);
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
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
        System.Action back = whenStanding;
        whenStanding = null;
        back?.Invoke();
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
