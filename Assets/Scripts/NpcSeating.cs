using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Sits a café NPC (customer or patron) on a TableSeat's chair and stands them
/// up again.
///
/// HOW IT FITS WITH THE BRAINS
/// The brains still decide everything. They walk the NPC towards the seat's
/// stand point and call TrySit - usually while still walking, a few steps out.
/// From there this component owns the visible body: it walks the rest of the
/// way round the chair to the spot in front of it and sits down.
///
/// THE NAVIGATION BODY LEAVES THE FLOOR WHILE SEATED (pass 1, 26 Sept 2026).
/// The NavMeshAgent is switched off from the first hand-walked step towards
/// the chair until the NPC is back on the floor, so a sitter is never an
/// invisible wall on the aisle corner. The visible body is inside the table's
/// Not Walkable footprint, so nobody can path through it; the seat itself stays
/// claimed (WaitingSpot.Occupant) so nobody targets it.
///
/// Standing up is an explicit handshake: NpcLocomotion.MoveTo asks
/// <see cref="RequestStand"/>, this component plays the stand-up clip, walks
/// back out round the chair, switches the agent on and then runs the caller's
/// continuation. While getting up it keeps the chair reserved.
///
/// WALK UP, SIT DOWN (pass 2b, 27 Sept 2026). What the player saw before this:
/// people slid the last metre into the chair in the idle pose ("hovering") and
/// spun on the spot at the chair's corner. Two causes. (1) NpcLocomotion reset
/// its walk flag every frame while the agent was off, so the walk clip never
/// played during these steps (fixed there). (2) The way in was two straight
/// legs - behind the chair, beside it, in front of it - with the body turned to
/// face along each leg at 360 degrees a second: a quarter turn to the side at
/// the chair's corner, then a quarter turn back to the table. Now:
///  * the way in is ONE smooth curve from wherever the walk was handed over,
///    along the chair's side and into the space in front of the seat, walked at
///    the pace the person arrived with and easing off only over the last half
///    metre, with the walk clip following the real speed the whole way;
///  * the body turns as little as the route allows: near the end it stays as
///    close to facing the table as it can while still walking no more than
///    <see cref="travelMismatch"/> degrees off the way it is going (coming from
///    behind a chair that is almost no turn at all), and whatever is left of the
///    turn is finished while lowering into the seat, the way people swivel as
///    they sit - never a stop and a spin;
///  * standing up turns a little towards the way out while rising, and the walk
///    out turns the rest of the way over its first half metre.
///
/// OUT OF THE CHAIR TOWARDS WHERE THEY GO NEXT (pass 2c, 27 Sept 2026). The
/// pass 2b walk out always went round to the stand point behind the chair and
/// only then towards the door or the counter: measured, it turned 185-300
/// degrees more than the way out needed (a loop round the back of the chair)
/// and, when the loop ended facing away, the next leg stopped and spun round.
/// Now the way out is weighed: out on either side of the chair, joining the
/// floor beside the chair, behind it on that side, or at the stand point -
/// whichever gives the shortest walk to where they are going next, with every
/// quarter turn counted as extra distance and a seated neighbour in the way
/// counted heavily. The walk out ends heading along the route the agent then
/// follows, so it carries straight on. The hand-walk also slows for tight
/// bends (<see cref="bendTurnRate"/>), the way people slow down to turn.
///
/// STRAIGHT UP TO THE CHAIR, NOT ROUND THE BACK OF IT (pass 2c). The brains
/// walk people to the seat's stand point, behind the chair, and hand over
/// 0.7 m before it. Someone coming along the table (from in front of the
/// chair) therefore walked past their seat to its back corner and was handed
/// over there, walking away from the table: the way in then reversed at once,
/// and for a moment they slid backwards/sideways while turning round (10-25 %
/// of sits; the body up to 140 degrees off the way it moved). Now
/// <see cref="ReadyToTakeOver"/> hands over as soon as someone walking along
/// the table reaches their chair's side, and they step straight in; someone
/// walking along the aisle behind the chairs is handed over further out, so
/// the walk curves round the corner. Whenever the body still faces well away
/// from the way the route goes, it slows right down until it has turned.
///
/// BENCHES (pass 2): a sofa or banquette seat (TableSeat.Style Bench) is
/// approached along its front from whichever side is free, so the last steps
/// run along the cushions and the turn to sit is a quarter turn, not an about-turn.
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
    [Tooltip("Walking pace round the chair when the walk was not handed over already moving, metres per second.")]
    [SerializeField, Min(.2f)] private float stepSpeed = 1.15f;
    [Tooltip("Pace at the moment the sit begins, metres per second (the walk eases down to it).")]
    [SerializeField, Range(.3f, 1.2f)] private float arriveSpeed = .6f;
    [Tooltip("Over this last stretch of the way in, the pace eases down to Arrive Speed, metres.")]
    [SerializeField, Range(.2f, 1.2f)] private float slowDownDistance = .55f;
    [Tooltip("How far beside the chair's centre line the NPC passes on the way in and out, metres.")]
    [SerializeField, Min(.2f)] private float sideClearance = .62f;
    [Tooltip("How far along a sofa's front the last steps start from, metres.")]
    [SerializeField, Range(.25f, .9f)] private float benchSideStep = .45f;
    [Tooltip("Fastest the body turns while stepping in or out, degrees per second (turns are eased).")]
    [SerializeField, Range(90f, 720f)] private float maxTurnRate = 260f;
    [Tooltip("Walking along the aisle behind a chair (sideways to it), the brains hand the walk over this far from " +
             "the stand point, so the way in curves round the chair's corner instead of turning sharply at it, metres.")]
    [SerializeField, Range(.7f, 2f)] private float earlyHandover = 1.3f;
    [Tooltip("Walking along the table (level with the seat or in front of it), the walk is handed over this close to " +
             "the chair's side, and they step straight in rather than walking round to the stand point behind it, metres.")]
    [SerializeField, Range(.4f, 1.5f)] private float sideEntryReach = 1f;
    [Tooltip("Over this last stretch of the way in, the body turns towards the table, metres.")]
    [SerializeField, Range(.2f, 1.5f)] private float turnInDistance = .85f;
    [Tooltip("Near the chair the body faces the table as far as it can while walking at most this many degrees off " +
             "the way it is going. Whatever turn is left is done while sitting down.")]
    [SerializeField, Range(20f, 90f)] private float travelMismatch = 50f;
    [Tooltip("The rest of the turn to the table is finished over this first part of sitting down, seconds.")]
    [SerializeField, Range(0f, .9f)] private float finishTurnSeconds = .45f;
    [Tooltip("On the way out, the body turns from the table to the way it walks over this first stretch, metres.")]
    [SerializeField, Range(.1f, 1.2f)] private float turnOutDistance = .5f;
    [Tooltip("While rising, the body already turns this many degrees towards the way out.")]
    [SerializeField, Range(0f, 90f)] private float turnWhileRising = 35f;
    [Tooltip("Round a tight bend the hand-walked steps slow down so the body never has to turn faster than this, " +
             "degrees per second (people slow down to turn rather than whip round a chair's corner).")]
    [SerializeField, Range(90f, 360f)] private float bendTurnRate = 170f;
    [Tooltip("Pace increase of the hand-walked steps, m/s per second (the first steps out of a chair build up).")]
    [SerializeField, Range(.8f, 6f)] private float stepAcceleration = 2.2f;
    [Tooltip("Out of a chair, walk towards where the person goes next: weigh the ways out on both sides of the chair " +
             "(joining the floor beside it, behind it, or at the stand point) by the walk to the destination plus " +
             "turning. Off: always round to the stand point, as before pass 2c.")]
    [SerializeField] private bool exitTowardsDestination = true;
    [Tooltip("Weighing the ways out of a chair, every 90 degrees of turning counts as this many metres of walking.")]
    [SerializeField, Range(0f, 2f)] private float exitTurnCost = .6f;
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
    private static readonly int StandUpState = Animator.StringToHash("Stand Up");
    private static readonly List<NpcSeating> active = new();

    private NavMeshAgent agent;
    private Animator animator;
    private PolygonNpcVisual visual;
    private NpcLocomotion locomotion;
    private TableSeat seat;
    private Phase phase = Phase.Standing;
    // Where the walk was handed over (0), where the feet stay while seated (2).
    private readonly Vector3[] path = new Vector3[3];
    private Quaternion seatRotation;
    // The way being walked right now: a smooth curve, sampled, with lengths.
    private readonly List<Vector3> waypoints = new(6);
    private readonly List<Vector3> route = new(48);
    private readonly List<float> routeAt = new(48);
    private float routeLength, walked, walkSpeed, cruiseSpeed, yawVelocity;
    private bool routeIn;
    private Quaternion routeStartRotation, arrivalRotation, risingTurn;
    private Quaternion? routeEndRotation;
    private Vector3 handBackPoint;
    private float sideIn = 1f;
    // Where the person heads once standing (NpcLocomotion's destination): the
    // walk out turns towards it so it carries straight on.
    private Vector3? standHeading;
    // The direction the walk out ends in: the first leg of the route the agent follows next.
    private Vector3? exitOnward;
    private NavMeshPath exitPath;
    private readonly Vector3[] exitCorners = new Vector3[32];
    private static readonly List<TableSeat> seatsNearby = new(16);
    private float phaseStarted, phaseLength, nextChat;
    private bool reserved;
    private bool? hasSitParameters;
    // Patience bar and speech bubble ride lower while seated, over the seated head.
    private Transform[] overheads = System.Array.Empty<Transform>();
    private Vector3[] overheadRest = System.Array.Empty<Vector3>();
    private float overheadDrop;
    // Who asked us to stand up, to be told when the agent is back on the floor.
    private System.Action whenStanding;
    // The body's capsule is on the NPC layer while on its feet (Ace's controller
    // ignores it: bodies give way to him, never push him). In a chair it goes
    // back to Default so he cannot walk through a sitting person.
    private int standingLayer = -1;
    // For checks: how the last walk in or out went.
    private float lastYaw, stepTurned, stepWorstRate;

    public Phase Current => phase;
    /// <summary>True from the first step towards the chair until the agent has been handed back.</summary>
    public bool Busy => phase != Phase.Standing;
    public bool IsSeated => phase == Phase.SittingDown || phase == Phase.Seated;
    public TableSeat Seat => seat;
    public NpcSitData Data { get => data; set => data = value; }
    /// <summary>Set by NpcSocial: the seated talk flag is theirs (chats, gestures), not this component's random chatter.</summary>
    public bool ExternalChat { get; set; }
    /// <summary>Speed of the hand-walked step right now, m/s (for checks).</summary>
    public float StepSpeed => walkSpeed;
    /// <summary>Degrees the body has turned so far on the current (or last) walk in or out (for checks).</summary>
    public float StepTurned => stepTurned;
    /// <summary>Fastest turn so far on the current (or last) walk in or out, degrees per second (for checks).</summary>
    public float StepWorstTurnRate => stepWorstRate;
    /// <summary>Length of the current (or last) walk in or out, metres (for checks).</summary>
    public float StepRouteLength => routeLength;

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

    /// <summary>Starts sitting on <paramref name="target"/>. The NPC must be standing at (or walking up to) its stand point.</summary>
    public bool TrySit(TableSeat target) => TrySit(target, 0f);

    /// <summary>
    /// As <see cref="TrySit(TableSeat)"/>, carrying <paramref name="startSpeed"/>
    /// (m/s) into the walk round the chair, so a brain that hands over while the
    /// person is still walking gets one continuous walk into the chair.
    /// </summary>
    public bool TrySit(TableSeat target, float startSpeed)
    {
        if (phase != Phase.Standing || !CanSit(target)) return false;
        Vector3 fromStandPoint = transform.position - target.StandPoint.position;
        fromStandPoint.y = 0f;
        // A walk handed over early (see ReadyToTakeOver) starts further out; a
        // standing start never does (someone a crowd stopped short stays put).
        float allowed = maxStartDistance;
        float entrySide = 0f;
        if (target.Style == TableSeat.SitStyle.Chair && startSpeed > .3f)
        {
            allowed = Mathf.Max(allowed, earlyHandover + .1f);
            entrySide = SideEntry(target, transform.position, .1f);
        }
        if (fromStandPoint.sqrMagnitude > allowed * allowed && entrySide == 0f) return false;
        seat = target;
        float floor = transform.position.y;
        Placement(seat, floor, out Vector3 feet, out seatRotation);
        Vector3 forward = seatRotation * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 seatCentre = seat.SeatPose.position;
        seatCentre.y = floor;
        bool bench = seat.Style == TableSeat.SitStyle.Bench;

        path[0] = transform.position;
        path[2] = feet;
        handBackPoint = path[0];
        // Stepping in from the side they are already on (walking along the table), or
        // round the freer side.
        sideIn = entrySide != 0f ? entrySide : ChooseSide(seatCentre, side, transform.position);
        float step = bench ? benchSideStep : sideClearance;

        waypoints.Clear();
        waypoints.Add(transform.position);
        if (!bench && Vector3.Dot(Flat(transform.position - seatCentre), forward) < -.3f)
        {
            // Coming from behind the chair (where the stand points are): along its
            // side first, so the walk passes the backrest instead of cutting the corner.
            Vector3 besideBack = seatCentre + side * (sideIn * step) - forward * .35f;
            besideBack.y = floor;
            waypoints.Add(besideBack);
        }
        // Beside the space in front of the seat; the last steps run along the seat's front.
        Vector3 beside = feet + side * (sideIn * step) - forward * .05f;
        beside.y = floor;
        waypoints.Add(beside);
        waypoints.Add(feet);
        BuildRoute(Flat(transform.forward), Flat(feet - beside));
        routeIn = true;
        routeEndRotation = seatRotation;
        walkSpeed = Mathf.Clamp(startSpeed, 0f, 1.6f);
        cruiseSpeed = startSpeed > .3f ? Mathf.Clamp(startSpeed, .9f, stepSpeed * 1.2f) : stepSpeed;
        yawVelocity = 0f;
        ResetStepStats();

        overheadDrop = Mathf.Max(0f, data.standingHipHeight - data.seatedHip.y);

        // The navigation body leaves the floor: from here the visible body moves
        // by hand, and nobody else's avoidance sees a person on the stand point.
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.enabled = false;
        active.Add(this);
        standingLayer = gameObject.layer;
        gameObject.layer = 0;   // Default: solid to the player while in the chair
        // A city body fits itself to the chair while the rig's hips are down.
        if (visual != null) visual.Seated = true;
        Begin(Phase.Approaching, 0f);
        return true;
    }

    /// <summary>
    /// Should the brain hand the walk over to this component now (pass 2c)?
    /// As before when the NPC is within <paramref name="usual"/> of the seat's
    /// stand point. For a chair, while walking, also: along the aisle behind the
    /// chair (sideways to it) within <see cref="earlyHandover"/> of the stand
    /// point, so the walk curves round the corner; and along the table - level
    /// with the seat or in front of it - within <see cref="sideEntryReach"/> of
    /// the chair's side, so they step straight in instead of walking past the
    /// seat to the stand point behind it and turning back.
    /// </summary>
    public bool ReadyToTakeOver(TableSeat target, float usual)
    {
        if (target == null || phase != Phase.Standing) return false;
        Vector3 position = transform.position;
        float toStand = Flat(target.StandPoint.position - position).magnitude;
        if (toStand < usual) return true;
        if (target.Style == TableSeat.SitStyle.Bench) return false;
        Vector3 velocity = agent != null && agent.enabled ? Flat(agent.velocity) : Vector3.zero;
        if (velocity.sqrMagnitude < .09f) return false;
        Vector3 forward = SeatForward(target);
        float relF = Vector3.Dot(Flat(position - target.SeatPose.position), forward);
        if (relF < -.3f && Mathf.Abs(Vector3.Dot(velocity.normalized, forward)) < .6f && toStand < earlyHandover) return true;
        return SideEntry(target, position, 0f) != 0f;
    }

    // +1 / -1: the NPC is level with the seat (or in front of it) within reach of
    // the chair's side on that side, so the way in can step straight in from
    // there; 0 otherwise.
    private float SideEntry(TableSeat target, Vector3 position, float slack)
    {
        if (data == null || target == null) return 0f;
        Vector3 forward = SeatForward(target), side = Vector3.Cross(Vector3.up, forward);
        Vector3 rel = Flat(position - target.SeatPose.position);
        float relF = Vector3.Dot(rel, forward), relS = Vector3.Dot(rel, side);
        if (relF < -.45f || relF > 1.2f) return 0f;
        float s = relS >= 0f ? 1f : -1f;
        Placement(target, position.y, out Vector3 feet, out _);
        Vector3 entry = feet + side * (s * sideClearance) - forward * .05f;
        return Flat(position - entry).magnitude < sideEntryReach + slack ? s : 0f;
    }

    private static Vector3 SeatForward(TableSeat target)
    {
        Vector3 toTable = Flat(target.FacingPoint - target.SeatPose.position);
        if (toTable.sqrMagnitude < 1e-4f) toTable = Flat(target.SeatPose.position - target.StandPoint.position);
        return toTable.sqrMagnitude > 1e-6f ? toTable.normalized : Vector3.forward;
    }

    /// <summary>
    /// Where this NPC's feet go, and which way it faces, to sit on
    /// <paramref name="target"/>: the clip's hip joints land just behind the
    /// seat's centre, a thigh's thickness above it, facing the seat's cup spot.
    /// Also used by the editor photo check.
    /// </summary>
    public void Placement(TableSeat target, float floor, out Vector3 feet, out Quaternion facing) =>
        Placement(target.SeatPose.position, target.FacingPoint, target.StandPoint.position, floor, out feet, out facing);

    /// <summary>
    /// As above, for a seat that isn't a café table's (Grace's armchair at home, GraceAtHome): the middle of its seat, what
    /// the sitter faces, and where they'd stand before sitting.
    /// </summary>
    public void Placement(Vector3 seatCentre, Vector3 facingPoint, Vector3 standPoint, float floor, out Vector3 feet, out Quaternion facing)
    {
        Vector3 toTable = facingPoint - seatCentre;
        toTable.y = 0f;
        if (toTable.sqrMagnitude < 1e-4f) { toTable = seatCentre - standPoint; toTable.y = 0f; }
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
    /// Get up and walk back out, then run <paramref name="whenBack"/> once the
    /// agent is on the floor again. False when the NPC is not in a chair
    /// (nothing to wait for; the caller can move at once).
    /// </summary>
    public bool RequestStand(System.Action whenBack) => RequestStand(whenBack, null);

    /// <summary>
    /// As <see cref="RequestStand(System.Action)"/>; <paramref name="headingTowards"/>
    /// is where the person is going next, so the walk out already turns that way
    /// and carries straight on.
    /// </summary>
    public bool RequestStand(System.Action whenBack, Vector3? headingTowards)
    {
        if (phase == Phase.Standing) return false;
        whenStanding = whenBack;
        standHeading = headingTowards;
        StandUp();
        return true;
    }

    /// <summary>Gets up and walks back out.</summary>
    public void StandUp()
    {
        switch (phase)
        {
            case Phase.Approaching:
                // Never sat down: head straight back out from wherever the body is.
                PlanWayOut(false);
                BeginReturning();
                break;
            case Phase.SittingDown:
            case Phase.Seated:
                animator.SetBool(SeatedHash, false);
                animator.SetBool(TalkingHash, false);
                PlanWayOut(true);
                // Rising, the body already turns a little towards the way out.
                Vector3 first = route.Count > 1 ? Flat(PointAt(.35f) - path[2]) : Vector3.zero;
                risingTurn = first.sqrMagnitude > 1e-4f
                    ? Quaternion.RotateTowards(seatRotation, Quaternion.LookRotation(first.normalized), turnWhileRising)
                    : seatRotation;
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
                if (Step())
                {
                    arrivalRotation = transform.rotation;
                    animator.SetBool(SeatedHash, true);
                    Begin(Phase.SittingDown, data.enterLength);
                }
                break;
            case Phase.SittingDown:
                // Whatever is left of the turn to the table goes into the first
                // part of lowering, the way people swivel as they sit.
                float k = finishTurnSeconds > 1e-3f ? Mathf.Clamp01((Time.time - phaseStarted) / finishTurnSeconds) : 1f;
                k = 1f - (1f - k) * (1f - k);
                transform.SetPositionAndRotation(path[2], Quaternion.Slerp(arrivalRotation, seatRotation, k));
                if (t >= 1f)
                {
                    Begin(Phase.Seated, 0f);
                    nextChat = Time.time + Random.Range(1.5f, 4f);
                }
                break;
            case Phase.Seated:
                Hold();
                if (!ExternalChat) Chat();
                break;
            case Phase.StandingUp:
                // Upright enough over the last third of the clip to start turning away.
                float r = Mathf.InverseLerp(.62f, 1f, t);
                transform.SetPositionAndRotation(path[2], Quaternion.Slerp(seatRotation, risingTurn, r * r * (3f - 2f * r)));
                if (t >= 1f) BeginReturning();
                break;
            case Phase.Returning:
                if (Step()) HandBack();
                break;
        }
        PlaceOverheads();
    }

    private void Begin(Phase next, float length)
    {
        // A chair scrape as the body goes down into the seat, and again as it rises.
        if (next == Phase.SittingDown && phase != Phase.SittingDown) Sfx.Play("chair.sit", transform.position);
        else if (next == Phase.StandingUp && phase != Phase.StandingUp) Sfx.Play("chair.stand", transform.position);
        phase = next;
        phaseStarted = Time.time;
        phaseLength = Mathf.Max(0f, length);
        if (next != Phase.Approaching && next != Phase.Returning)
        {
            if (locomotion != null) locomotion.DriveWalk(0f, 0f);
            else animator.SetBool(IsWalkingHash, false);
        }
    }

    private void BeginReturning()
    {
        routeIn = false;
        routeStartRotation = transform.rotation;
        walkSpeed = phase == Phase.Approaching ? walkSpeed : 0f;
        cruiseSpeed = stepSpeed;
        walked = 0f;
        yawVelocity = 0f;
        ResetStepStats();
        Begin(Phase.Returning, 0f);
    }

    // The way out: towards where they go next when that is known (pass 2c, see
    // PlanExitTowards); otherwise round the chair on the freer side (the side
    // they came in by when both are free) back to the stand point, the last
    // steps turning towards wherever they are going next.
    private void PlanWayOut(bool fromSeat)
    {
        waypoints.Clear();
        exitOnward = null;
        Vector3 from = fromSeat ? path[2] : transform.position;
        waypoints.Add(from);
        Vector3 end = fromSeat && seat != null ? seat.StandPoint.position : path[0];
        end.y = path[0].y;   // the floor the walk was handed over on (the feet may sit a little higher at the chair)
        if (fromSeat && seat != null)
        {
            Vector3 forward = seatRotation * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 seatCentre = seat.SeatPose.position;
            seatCentre.y = from.y;
            if (seat.Style == TableSeat.SitStyle.Bench)
            {
                // Off a sofa: straight out into the room, towards the stand point.
            }
            else if (exitTowardsDestination && standHeading.HasValue && PlanExitTowards(from, seatCentre, forward, side, standHeading.Value, ref end))
            {
                // Waypoints added: out on the side facing the way they go, joining the floor where the walk is shortest.
            }
            else
            {
                float sideOut = ChooseSide(seatCentre, side, seatCentre + side * sideIn);
                Vector3 beside = path[2] + side * (sideOut * sideClearance) - forward * .05f;
                beside.y = from.y;
                waypoints.Add(beside);
                if (Vector3.Dot(Flat(end - seatCentre), forward) < -.3f)
                {
                    Vector3 besideBack = seatCentre + side * (sideOut * sideClearance) - forward * .35f;
                    besideBack.y = from.y;
                    waypoints.Add(besideBack);
                }
            }
        }
        waypoints.Add(end);
        handBackPoint = end;

        Vector3 startDir = fromSeat
            ? (waypoints.Count > 2 ? Flat(waypoints[1] - from) : seatRotation * Vector3.forward)
            : Flat(transform.forward);
        Vector3 last = waypoints[waypoints.Count - 2];
        Vector3 endDir = Flat(end - last);
        routeEndRotation = null;
        if (exitOnward.HasValue && exitOnward.Value.sqrMagnitude > 1e-4f)
        {
            // Arrive walking along the route the agent follows next, so the walk carries straight on.
            Vector3 onward = exitOnward.Value.normalized;
            routeEndRotation = Quaternion.LookRotation(onward);
            Vector3 blend = endDir.normalized + onward * 1.5f;
            endDir = blend.sqrMagnitude > 1e-3f ? blend : endDir;
        }
        else if (standHeading.HasValue)
        {
            Vector3 onward = Flat(standHeading.Value - end);
            if (onward.sqrMagnitude > .04f)
            {
                routeEndRotation = Quaternion.LookRotation(onward.normalized);
                // Blend the arrival towards the onward direction so the walk carries on.
                endDir = (endDir.normalized + onward.normalized).sqrMagnitude > 1e-3f ? (endDir.normalized + onward.normalized) : endDir;
            }
        }
        BuildRoute(startDir, endDir);
    }

    // The ways out of a chair, weighed: out on either side (the first steps
    // beside the chair), then joining the floor (a) beside the chair, (b)
    // behind it on that side, or (c) at the stand point. Cost = hand-walked
    // distance + NavMesh distance on to the destination + turning + a heavy
    // charge for a seated neighbour in the way. False when nothing works (the
    // caller falls back to the stand point).
    private bool PlanExitTowards(Vector3 from, Vector3 seatCentre, Vector3 forward, Vector3 side, Vector3 destination, ref Vector3 end)
    {
        exitPath ??= new NavMeshPath();
        float floorY = path[0].y;
        CollectSeatsNearby(seatCentre);
        float best = float.PositiveInfinity;
        Vector3 bestBeside = default, bestBack = default, bestEnd = default, bestOnward = default;
        bool bestViaBack = false, found = false;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 beside = from + side * (s * sideClearance) - forward * .05f;
            beside.y = from.y;
            Vector3 besideBack = seatCentre + side * (s * sideClearance) - forward * .35f;
            besideBack.y = from.y;
            for (int c = 0; c < 3; c++)
            {
                Vector3 probe = c switch
                {
                    0 => seatCentre + side * (s * (sideClearance + .3f)) - forward * .1f,
                    1 => seatCentre + side * (s * (sideClearance + .1f)) - forward * .75f,
                    _ => seat.StandPoint.position,
                };
                probe.y = floorY;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .3f, NavMesh.AllAreas)) continue;
                if (Flat(hit.position - probe).magnitude > .22f) continue;
                Vector3 join = hit.position;
                join.y = floorY;
                if (!NavMesh.CalculatePath(hit.position, destination, NavMesh.AllAreas, exitPath)
                    || exitPath.status != NavMeshPathStatus.PathComplete) continue;
                int n = exitPath.GetCornersNonAlloc(exitCorners);
                float onward = 0f;
                for (int i = 1; i < n; i++) onward += Flat(exitCorners[i] - exitCorners[i - 1]).magnitude;
                Vector3 onwardDir = Vector3.zero;
                for (int i = 1; i < n && onwardDir.sqrMagnitude < .04f; i++) onwardDir = Flat(exitCorners[i] - hit.position);
                if (onwardDir.sqrMagnitude < .04f) onwardDir = Flat(destination - hit.position);
                // Round the back of the chair only for the stand point behind it.
                bool viaBack = c == 2 && Vector3.Dot(Flat(join - seatCentre), forward) < -.3f
                               && Vector3.Dot(Flat(join - besideBack), forward) < 0f;
                float walk = Flat(beside - from).magnitude;
                float turn = Vector3.Angle(forward, Flat(beside - from));
                Vector3 legFrom = beside, lastLeg = Flat(beside - from);
                float crowd = Blocking(from, beside);
                if (viaBack)
                {
                    walk += Flat(besideBack - beside).magnitude;
                    turn += Vector3.Angle(lastLeg, Flat(besideBack - beside));
                    lastLeg = Flat(besideBack - beside);
                    crowd += Blocking(beside, besideBack);
                    legFrom = besideBack;
                }
                Vector3 joinLeg = Flat(join - legFrom);
                if (joinLeg.sqrMagnitude > .01f)
                {
                    walk += joinLeg.magnitude;
                    turn += Vector3.Angle(lastLeg, joinLeg);
                    lastLeg = joinLeg;
                    crowd += Blocking(legFrom, join);
                }
                turn += Vector3.Angle(lastLeg, onwardDir);
                float cost = walk + onward + exitTurnCost * turn / 90f + 3f * crowd;
                if (cost >= best) continue;
                best = cost;
                found = true;
                bestBeside = beside;
                bestBack = besideBack;
                bestViaBack = viaBack;
                bestEnd = join;
                bestOnward = onwardDir;
            }
        }
        if (!found) return false;
        waypoints.Add(bestBeside);
        if (bestViaBack) waypoints.Add(bestBack);
        end = bestEnd;
        exitOnward = bestOnward;
        return true;
    }

    private void CollectSeatsNearby(Vector3 centre)
    {
        seatsNearby.Clear();
        foreach (TableSeat other in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (other == seat || !other.IsOccupied) continue;
            if (Flat(other.SeatPose.position - centre).sqrMagnitude > 2.5f * 2.5f) continue;
            seatsNearby.Add(other);
        }
    }

    // How many occupied seats (someone sitting, or on their way to sit) the
    // straight step from a to b would brush past: within half a metre of the seat.
    private static int Blocking(Vector3 a, Vector3 b)
    {
        int count = 0;
        Vector3 ab = Flat(b - a);
        float len2 = ab.sqrMagnitude;
        foreach (TableSeat other in seatsNearby)
        {
            Vector3 p = Flat(other.SeatPose.position - a);
            float u = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p, ab) / len2) : 0f;
            if ((p - ab * u).sqrMagnitude < .5f * .5f) count++;
        }
        return count;
    }

    private void Hold() => transform.SetPositionAndRotation(path[2], seatRotation);

    // ---------- the route ----------

    // A smooth curve through the waypoints: cubic Hermite pieces with
    // Catmull-Rom tangents, leaving along startDir and arriving along endDir,
    // sampled every few centimetres and measured, so it can be walked at a
    // steady pace by distance.
    private void BuildRoute(Vector3 startDir, Vector3 endDir)
    {
        // A waypoint that would make the walk double back, or sits on top of its
        // neighbour, only adds a kink: drop it.
        for (int i = waypoints.Count - 2; i >= 1; i--)
        {
            Vector3 before = Flat(waypoints[i] - waypoints[i - 1]);
            Vector3 toEnd = Flat(waypoints[waypoints.Count - 1] - waypoints[i - 1]);
            Vector3 after = Flat(waypoints[i + 1] - waypoints[i]);
            if (before.magnitude < .2f || after.magnitude < .12f || Vector3.Dot(before, toEnd) <= 0f) waypoints.RemoveAt(i);
        }
        route.Clear();
        routeAt.Clear();
        int n = waypoints.Count;
        float fromY = waypoints[0].y, toY = waypoints[n - 1].y;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a = Flat(waypoints[i]), b = Flat(waypoints[i + 1]);
            float chord = (b - a).magnitude;
            if (chord < 1e-4f) continue;
            Vector3 ta = i == 0 ? StartTangent(startDir, (b - a) / chord) : Flat(waypoints[i + 1] - waypoints[i - 1]).normalized;
            Vector3 tb = i == n - 2 ? (endDir.sqrMagnitude > 1e-6f ? endDir.normalized : (b - a) / chord)
                                    : Flat(waypoints[i + 2] - waypoints[i]).normalized;
            int samples = Mathf.Clamp(Mathf.CeilToInt(chord / .05f), 4, 24);
            for (int k = route.Count == 0 ? 0 : 1; k <= samples; k++)
            {
                float u = k / (float)samples, u2 = u * u, u3 = u2 * u;
                route.Add((2f * u3 - 3f * u2 + 1f) * a + (u3 - 2f * u2 + u) * chord * ta
                          + (-2f * u3 + 3f * u2) * b + (u3 - u2) * chord * tb);
            }
        }
        if (route.Count == 0) { route.Add(Flat(waypoints[0])); route.Add(Flat(waypoints[n - 1])); }
        routeLength = 0f;
        routeAt.Add(0f);
        for (int i = 1; i < route.Count; i++) { routeLength += (route[i] - route[i - 1]).magnitude; routeAt.Add(routeLength); }
        // The floor height eases from start to end (a chair a little higher or lower).
        for (int i = 0; i < route.Count; i++)
        {
            Vector3 point = route[i];
            point.y = Mathf.Lerp(fromY, toY, routeLength > 1e-4f ? routeAt[i] / routeLength : 1f);
            route[i] = point;
        }
        routeLength = Mathf.Max(routeLength, .01f);
        walked = 0f;
    }

    // Leave along the way the body was already going - unless that points away
    // from where the route goes, which would make a loop: then fade it out.
    private static Vector3 StartTangent(Vector3 startDir, Vector3 chordDir)
    {
        if (startDir.sqrMagnitude < 1e-6f) return chordDir;
        Vector3 d = startDir.normalized;
        float along = Vector3.Dot(d, chordDir);
        return along <= 0f ? chordDir * .3f : Vector3.Lerp(chordDir, d, along);
    }

    private Vector3 PointAt(float s)
    {
        if (route.Count == 0) return transform.position;
        s = Mathf.Clamp(s, 0f, routeLength);
        int lo = 0, hi = routeAt.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (routeAt[mid] <= s) lo = mid; else hi = mid;
        }
        float span = routeAt[hi] - routeAt[lo];
        return Vector3.Lerp(route[lo], route[hi], span > 1e-5f ? (s - routeAt[lo]) / span : 1f);
    }

    private Vector3 TangentAt(float s)
    {
        Vector3 d = Flat(PointAt(s + .12f) - PointAt(s - .04f));
        if (d.sqrMagnitude < 1e-6f) d = Flat(PointAt(routeLength) - PointAt(routeLength - .15f));
        return d;
    }

    // Walk the route by distance: the pace carried in, easing down over the
    // last stretch into the chair (or carrying on when the agent takes over),
    // and the body turned as described at the top. True when there.
    private bool Step()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return false;
        // Out of the chair: the first step waits until the stand-up clip has
        // blended out (it leaves through Idle), or the body would glide off in
        // the standing-up pose before the walk arrives.
        if (!routeIn && walked <= 0f && Time.time - phaseStarted < .6f && StillRising())
        {
            if (locomotion != null) locomotion.DriveWalk(0f, dt);
            return false;
        }
        float remaining = routeLength - walked;
        bool carryOn = !routeIn && whenStanding != null;   // the agent continues the walk from the stand point
        float target = cruiseSpeed;
        if (!carryOn && remaining < slowDownDistance) target = Mathf.Lerp(arriveSpeed, cruiseSpeed, remaining / slowDownDistance);
        // Slow for a tight bend just ahead, so the body never whips round.
        float bend = BendAhead(walked);
        if (bend > 1e-3f) target = Mathf.Min(target, Mathf.Max(.45f, bendTurnRate * Mathf.Deg2Rad / bend));
        // On the way in, a body facing well away from the way the route goes
        // (handed over at a sharp corner) slows right down until it has turned,
        // instead of gliding sideways. (Near the chair the body is meant to be up
        // to travelMismatch off the route, so only beyond that.)
        float decelerate = 2.5f;
        if (routeIn)
        {
            Vector3 heading = TangentAt(walked);
            float mismatch = heading.sqrMagnitude > 1e-6f ? Vector3.Angle(Flat(transform.forward), heading) : 0f;
            float beyond = travelMismatch + 10f;
            if (mismatch > beyond)
            {
                target = Mathf.Min(target, Mathf.Lerp(cruiseSpeed, .3f, Mathf.InverseLerp(beyond, beyond + 50f, mismatch)));
                decelerate = 5f;
            }
        }
        walkSpeed = Mathf.MoveTowards(walkSpeed, target, (target > walkSpeed ? stepAcceleration : decelerate) * dt);
        if (routeIn) walkSpeed = Mathf.Max(walkSpeed, .25f);
        walked = Mathf.Min(routeLength, walked + Mathf.Max(walkSpeed, .05f) * dt);

        Vector3 position = PointAt(walked);
        Vector3 tangent = TangentAt(walked);
        Quaternion along = tangent.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(tangent.normalized) : transform.rotation;
        Quaternion want;
        if (routeIn)
        {
            // Near the chair: as close to facing the table as the walk allows.
            float gap = Quaternion.Angle(along, seatRotation);
            Quaternion leastTurn = gap <= travelMismatch ? seatRotation : Quaternion.RotateTowards(along, seatRotation, travelMismatch);
            float w = Smooth01(Mathf.InverseLerp(routeLength - turnInDistance, routeLength - turnInDistance * .35f, walked));
            want = Quaternion.Slerp(along, leastTurn, w);
        }
        else
        {
            // Out of the chair: from the table to the way we walk, then towards where we go next.
            want = Quaternion.Slerp(routeStartRotation, along, Smooth01(walked / Mathf.Max(.05f, turnOutDistance)));
            if (routeEndRotation.HasValue)
                want = Quaternion.Slerp(want, routeEndRotation.Value, Smooth01(Mathf.InverseLerp(routeLength - .45f, routeLength, walked)) * .7f);
        }
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, want.eulerAngles.y, ref yawVelocity, .1f, maxTurnRate, dt);
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        TrackStepStats(dt);

        // The walk clip follows the real speed (through NpcLocomotion, so the
        // stride matches the floor), all the way to the seat.
        if (locomotion != null) locomotion.DriveWalk(walkSpeed, dt);
        else animator.SetBool(IsWalkingHash, walkSpeed > .1f);
        return walked >= routeLength - 1e-4f;
    }

    // Sharpest bend on the next half metre of the route, radians per metre.
    private float BendAhead(float s)
    {
        float worst = 0f;
        Vector3 before = TangentAt(s);
        for (float ahead = .15f; ahead <= .6f; ahead += .15f)
        {
            Vector3 after = TangentAt(s + ahead);
            if (before.sqrMagnitude > 1e-6f && after.sqrMagnitude > 1e-6f)
                worst = Mathf.Max(worst, Vector3.Angle(before, after) * Mathf.Deg2Rad / .15f);
            before = after;
            if (s + ahead >= routeLength) break;
        }
        return worst;
    }

    private bool StillRising()
    {
        if (animator == null || !animator.isActiveAndEnabled) return false;
        return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == StandUpState;
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private void ResetStepStats()
    {
        lastYaw = transform.eulerAngles.y;
        stepTurned = 0f;
        stepWorstRate = 0f;
    }

    private void TrackStepStats(float dt)
    {
        float yaw = transform.eulerAngles.y;
        float turned = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
        lastYaw = yaw;
        stepTurned += turned;
        if (dt > 1e-4f) stepWorstRate = Mathf.Max(stepWorstRate, turned / dt);
    }

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

    // +1 passes the chair on the seated person's right, -1 on their left: the
    // side with fewer people sitting next to it, else the side of tieTowards.
    private float ChooseSide(Vector3 centre, Vector3 side, Vector3 tieTowards)
    {
        int right = 0, left = 0;
        // Claimed seats count too: someone on the way to a neighbouring chair will be sitting in it.
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
        return Vector3.Dot(tieTowards - centre, side) >= 0f ? 1f : -1f;
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
        transform.position = handBackPoint;
        float carried = walkSpeed;
        walkSpeed = 0f;
        if (standingLayer >= 0) { gameObject.layer = standingLayer; standingLayer = -1; }
        if (agent != null && agent.gameObject.activeInHierarchy)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(handBackPoint);
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
        standHeading = null;
        exitOnward = null;
        if (visual != null) visual.Seated = false;
        PlaceOverheads();
        System.Action back = whenStanding;
        whenStanding = null;
        // Moving already when the next leg starts: the locomotion curves onto it
        // instead of stopping to turn on the spot.
        if (back != null && carried > .3f && agent != null && agent.enabled && agent.isOnNavMesh)
            agent.velocity = transform.forward * Mathf.Min(carried, agent.speed);
        back?.Invoke();
        // The continuation has usually started the next leg: carry the walking
        // speed into it so the agent does not pull away from a standstill.
        if (back != null && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped && carried > .3f)
            agent.velocity = transform.forward * Mathf.Min(carried, agent.speed);
    }

    private bool HasParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash) return true;
        return false;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private void OnDisable()
    {
        if (phase == Phase.Approaching || phase == Phase.SittingDown || phase == Phase.Seated) handBackPoint = path[0];
        if (phase != Phase.Standing) HandBack();
    }

    private void OnDestroy()
    {
        ReleaseReservation();
        active.Remove(this);
    }
}
