using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How a café NPC physically gets where its brain sent it.
///
/// THE SPLIT
/// CustomerBrain and PatronBrain decide WHERE to go and WHY (a counter slot,
/// a seat, the door). This component decides HOW: it drives the NavMeshAgent,
/// turns the body, feeds the walk animation, watches for stalls and recovers
/// from them. The brains ask <see cref="MoveTo"/>, <see cref="Park"/>,
/// <see cref="Face"/>, <see cref="Pause"/>/<see cref="Resume"/> and read
/// <see cref="HasArrived"/>, <see cref="GaveUp"/> and <see cref="StuckStage"/>.
/// Nothing in here knows about queues, seats, patience or orders.
///
/// WHY IT EXISTS (claude/cafe-npc-observations-2026-09-26.md)
///  * Both brains carried their own copy of walk-animation and stall logic,
///    and they disagreed (one had hysteresis, one did not).
///  * The body followed <c>agent.updateRotation</c>, so every avoidance
///    correction became a visible turn, and the brains snapped the body to
///    the destination's facing whenever the velocity dipped. That was the
///    "small circular motion". Here the body faces the ROUTE (the next path
///    corner) with smoothing and a dead zone; only a real, sustained detour
///    turns it, and a body pushed backwards keeps its heading instead of
///    spinning round. The destination's facing is applied by <see cref="Park"/>
///    or <see cref="Face"/>, i.e. on arrival, never mid-walk.
///  * The old watchdogs measured displacement (an orbit counts as progress) or
///    remainingDistance with resets (a re-path counts as progress). Progress
///    here is remainingDistance getting shorter than it has ever been on this
///    leg; re-paths rebase without resetting the ladder.
///
/// RECOVERY LADDER, when a leg makes no progress for <see cref="stallSeconds"/>:
///   1 re-path  →  2 step aside (a nearby point off the line, then back)
///   →  3 push through (a pushier avoidance priority)  →  4 give up
///   (<see cref="GaveUp"/> for the brain to decide) and keep trying gently:
///   re-path, and every other cycle a short pass-through with avoidance off
///   so a doorway blocked by a stationary body never holds anyone forever.
/// Being carried BACKWARDS is handled before the ladder: a body whose actual
/// velocity opposes its desired velocity while its route grows again (a
/// group walking through it, i.e. avoidance herding the lower priority) stops
/// for a moment and lets them pass instead of moonwalking down the aisle.
/// Nobody is teleported.
///
/// SITTING: while <see cref="NpcSeating"/> owns the body the agent is switched
/// off, so a request to move first asks the seating to stand the NPC up and
/// runs once the agent is handed back; the seating feeds the walk clip through
/// <see cref="DriveWalk"/> while it moves the body by hand.
///
/// SETTING OFF: a leg that starts more than <c>turnFirstBeyond</c> degrees off
/// the body's heading turns on the spot first, then walks, instead of sliding
/// sideways while the body catches up. The turn eases in and out at a human
/// pace and the feet step round while it happens (the walk clip at a slow
/// rate), so it reads as someone turning round, not a turret. A leg that
/// starts while the body is already walking is not stopped for a turn at all:
/// the walk curves onto the new route (pass 2b, 27 Sept 2026).
///
/// ONE MOVEMENT FROM TURN TO WALK (pass 2c, 27 Sept 2026). Measured in the
/// pass 2b recordings: every set-off after a turn on the spot flickered the
/// walk clip off and on again (Walk, Idle, Walk inside half a second - about
/// three times a minute of walking), the turn paused for a frame just as the
/// feet set off, and the body went from standing to full walking speed in a
/// quarter of a second. Now the walk clip is held on through the set-off
/// (<see cref="walkOnHold"/>), the heading keeps turning at the pace the spot
/// turn left it, and a walk from a standstill builds up over its first steps
/// (<see cref="setOffAcceleration"/>) the way people start walking.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[DefaultExecutionOrder(20)] // after the brains (0), before NpcSeating (60), PersonalSpace (80) and PolygonNpcVisual (150)
public sealed class NpcLocomotion : MonoBehaviour
{
    public enum Recovery { None, Repath, StepAside, PushThrough, GaveUp, PassThrough, Yield }

    /// <summary>How one leg should be walked.</summary>
    public struct Move
    {
        /// <summary>The agent stops this far from the goal.</summary>
        public float stoppingDistance;
        /// <summary>Within this of the goal (straight line) counts as there.</summary>
        public float arriveRadius;
        /// <summary>Avoidance priority while walking (lower = pushier; 0 never yields).</summary>
        public int priority;
        /// <summary>May escalate to the pushy priority when stuck.</summary>
        public bool allowPush;
        /// <summary>For traces and logs.</summary>
        public string purpose;

        public static Move To(float stoppingDistance, int priority, string purpose, bool allowPush = true) => new Move
        {
            stoppingDistance = stoppingDistance,
            arriveRadius = stoppingDistance + .15f,
            priority = priority,
            allowPush = allowPush,
            purpose = purpose
        };

        /// <summary>The door: an area, not a point, so several people can leave at once.</summary>
        public static Move Exit(int priority) => new Move
        {
            stoppingDistance = Mathf.Max(.3f, CafeArrivals.DepartureRadius - .15f),
            arriveRadius = CafeArrivals.DepartureRadius + .1f,
            priority = priority,
            allowPush = true,
            purpose = "exit"
        };
    }

    [Header("Speed")]
    [Tooltip("Walking speed, metres per second, before the per-person variation. The street walk outside is 1.15-1.4.")]
    [SerializeField, Min(.3f)] private float baseSpeed = 1.6f;
    [Tooltip("Per-person speed variation, rolled once. 0.12 = ±12 %.")]
    [SerializeField, Range(0f, .5f)] private float speedJitter = .12f;
    [SerializeField, Min(.5f)] private float acceleration = 6f;
    [Tooltip("The speed the walk clip was made for (metres per second at playback 1). The WalkRate animator parameter " +
             "is speed / this, so feet stop sliding. Measured by Fixit Fidget > Café life > Pass 1 - 3.")]
    [SerializeField, Min(.3f)] private float walkClipSpeed = 1.35f;
    [Tooltip("The walk styles (clips and their natural speeds) the animator's Walk blend tree offers (pass 2). " +
             "Without it every person uses the one walk clip above.")]
    [SerializeField] private NpcGaitData gait;

    [Header("Turning")]
    [Tooltip("Fastest the body turns, degrees per second.")]
    [SerializeField, Min(30f)] private float turnSpeed = 400f;
    [Tooltip("Smoothing time for the heading, seconds. Higher = lazier turns.")]
    [SerializeField, Range(.02f, .6f)] private float turnSmoothing = .15f;
    [Tooltip("Below this speed (m/s) the body keeps its heading rather than turning to follow a creep.")]
    [SerializeField, Range(.05f, 1f)] private float headingDeadZone = .3f;
    [Tooltip("An avoidance deflection larger than this (degrees) that lasts longer than Detour Hold turns the body to follow it.")]
    [SerializeField, Range(20f, 130f)] private float detourAngle = 70f;
    [SerializeField, Range(.1f, 1.5f)] private float detourHold = .35f;

    [Header("Setting off")]
    [Tooltip("If the route starts more than this far round from where the body faces, turn on the spot first instead of sliding off sideways (degrees).")]
    [SerializeField, Range(20f, 150f)] private float turnFirstBeyond = 60f;
    [Tooltip("Longest the body turns before it starts walking anyway, seconds.")]
    [SerializeField, Range(.1f, 1f)] private float turnFirstMax = .5f;
    [Tooltip("Fastest the body turns on the spot (setting off, or turning to a slot or a seat's facing), degrees per " +
             "second. The turn eases in and out, and the feet step round meanwhile.")]
    [SerializeField, Min(30f)] private float turnOnSpotSpeed = 230f;
    [Tooltip("Longest an on-the-spot turn before setting off may take, seconds (a relaxed person takes the longest).")]
    [SerializeField, Range(.3f, 2f)] private float turnOnSpotMax = 1.1f;
    [Tooltip("Set off once the route is within this many degrees of the heading; the rest of the turn is walked.")]
    [SerializeField, Range(10f, 90f)] private float setOffWithin = 50f;
    [Tooltip("Already walking faster than this (m/s) when a new leg starts: curve onto it instead of stopping to turn.")]
    [SerializeField, Range(.2f, 1.5f)] private float keepWalkingAbove = .6f;
    [Tooltip("Turning on the spot faster than this (degrees per second) plays the walk clip slowly, so the feet step round.")]
    [SerializeField, Range(20f, 200f)] private float stepRoundAbove = 70f;
    [Tooltip("Setting off (after a turn, or continuing a walk into a new leg), the walk clip stays on for at least this " +
             "long, so it does not drop to Idle for the moment the speed is still low, seconds.")]
    [SerializeField, Range(0f, 1f)] private float walkOnHold = .45f;
    [Tooltip("Starting to walk from a standstill, the speed builds up at this rate over the first steps, m/s per second " +
             "(the agent's own acceleration, 6, had people at full speed in a quarter of a second).")]
    [SerializeField, Range(.8f, 6f)] private float setOffAcceleration = 2.2f;
    [Tooltip("How long the gentle build-up lasts before the normal acceleration is back, seconds.")]
    [SerializeField, Range(.2f, 1.5f)] private float setOffSeconds = .8f;

    [Header("Walk animation")]
    [Tooltip("Speed above which the walk starts (m/s).")]
    [SerializeField, Range(.05f, 1f)] private float walkStart = .35f;
    [Tooltip("Speed below which the walk stops (m/s).")]
    [SerializeField, Range(.02f, .8f)] private float walkStop = .12f;
    [Tooltip("A change has to hold for this long before the clip switches, seconds.")]
    [SerializeField, Range(0f, .5f)] private float walkHold = .12f;

    [Header("Stalls")]
    [Tooltip("Seconds without the route getting shorter before the next recovery step.")]
    [SerializeField, Min(.5f)] private float stallSeconds = 2.5f;
    [Tooltip("The route has to get at least this much shorter (metres) to count as progress.")]
    [SerializeField, Min(.05f)] private float progressStep = .2f;
    [Tooltip("How far to the side the step-aside recovery goes, metres.")]
    [SerializeField, Range(.3f, 1.5f)] private float stepAsideDistance = .8f;
    [SerializeField, Range(.5f, 4f)] private float stepAsideSeconds = 2f;
    [Tooltip("Avoidance priority used to push through when politeness has failed (lower = pushier).")]
    [SerializeField, Range(0, 99)] private int pushPriority = 15;
    [Tooltip("How long the last-resort pass-through (avoidance off) lasts, seconds.")]
    [SerializeField, Range(.5f, 3f)] private float passThroughSeconds = 1.5f;
    [Tooltip("Carried against the route for this long (avoidance herding) → stop and let them pass, seconds.")]
    [SerializeField, Range(.2f, 1.5f)] private float pushedBackSeconds = .4f;
    [Tooltip("How long the let-them-pass stop lasts, seconds.")]
    [SerializeField, Range(.3f, 2f)] private float yieldSeconds = .9f;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int WalkRateHash = Animator.StringToHash("WalkRate");
    private static readonly int WalkStyleHash = Animator.StringToHash("WalkStyle");

    private NavMeshAgent agent;
    private Animator animator;
    private NpcSeating seating;
    private bool hasWalkRate, hasWalkStyle, animatorChecked;
    private float speedMultiplier = 1f;
    // The movement profile's share of the numbers above (pass 2): 1 = the prefab's values.
    private float profileSpeed = 1f, profileAcceleration = 1f, profileTurn = 1f, profileUrgency = .5f;
    private int walkStyle;

    // The current leg.
    private bool hasGoal;
    private Vector3 goal;
    private Move move;
    private bool paused;
    private Action afterStanding;

    // Progress and recovery.
    private float bestRemaining = float.PositiveInfinity;
    private bool rebaseRemaining;
    private float stalledSince;
    private int stage;
    private Recovery lastRecovery;
    private float asideUntil;
    private bool steppingAside;
    private int asideSide = 1;
    private float passThroughUntil;
    private ObstacleAvoidanceType normalAvoidance;
    private float lastCycleAt;
    private int postGiveUpCycles;
    private float pushedBackSince = -1f;
    private float yieldUntil;
    private bool yielding;
    private int yieldsWithoutProgress;
    private int priorityBeforeYield;

    // Setting off: turn towards the route before the first step.
    private bool turningFirst;
    private float turnFirstUntil;
    // Turning on the spot: eased yaw, and the feet stepping round while it lasts.
    private float spotYawVelocity, lastYaw, stepRoundUntil, stepRoundRate;
    private bool haveLastYaw, crowdedTurn;
    // How fast the body is really moving, whatever moves it (the agent, the
    // street walk, the seating): a new leg started by someone already walking
    // curves on instead of stopping to turn.
    private Vector3 lastBodyPosition;
    private bool haveBodyPosition;
    private float bodySpeed;
    private static readonly List<NpcLocomotion> everyone = new();
    // Setting off: the walk clip held on, and the gentle build-up of speed.
    private float holdWalkUntil, setOffUntil;
    private NavMeshPath routeProbe;
    private readonly Vector3[] routeCorners = new Vector3[16];

    // Heading.
    private float headingVelocity;
    private float detourSince = -1f;
    private bool followingDetour;
    private Quaternion? facing;

    // Animation.
    private bool walkingAnimation;
    private float walkChangeTimer;

    /// <summary>The leg is done: close enough to the goal and no longer moving.</summary>
    public bool HasArrived { get; private set; }
    /// <summary>Politeness, a re-path, a sidestep and a push all failed; the brain decides. Sticky until the next MoveTo.</summary>
    public bool GaveUp { get; private set; }
    /// <summary>0 while progressing; the recovery ladder stage otherwise (for traces).</summary>
    public int StuckStage => hasGoal && !HasArrived ? stage : 0;
    public Recovery LastRecovery => lastRecovery;
    public bool IsMoving => hasGoal && !paused && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped;
    public float Speed => agent != null && agent.enabled ? Flat(agent.velocity).magnitude : 0f;
    public bool HasGoal => hasGoal;
    public Vector3 Goal => goal;
    public string Purpose => hasGoal ? move.purpose : "";
    /// <summary>Every café NPC's locomotion that is enabled (read only), e.g. to see who is standing where.</summary>
    public static IReadOnlyList<NpcLocomotion> All => everyone;
    public float WalkSpeed => baseSpeed * speedMultiplier * profileSpeed;
    /// <summary>The walk clip in use (NpcMovementProfile.WalkStyle), for traces.</summary>
    public int WalkStyle => walkStyle;
    /// <summary>The natural speed of the walk clip in use at this body's scale, m/s.</summary>
    public float ClipSpeed => gait != null && gait.HasWalk(walkStyle)
        ? gait.ClipSpeed(walkStyle, walkClipSpeed / Mathf.Max(.1f, transform.lossyScale.y)) * transform.lossyScale.y
        : walkClipSpeed;

    /// <summary>
    /// Take on a movement profile (pass 2): speed, acceleration, turning and
    /// the walk clip. Presentation only; where the person goes is unchanged.
    /// </summary>
    public void ApplyProfile(NpcMovementProfile profile)
    {
        if (profile == null) return;
        profileSpeed = profile.walkSpeed;
        profileAcceleration = profile.acceleration;
        profileTurn = profile.turnSpeed;
        profileUrgency = profile.urgency;
        walkStyle = gait != null && gait.HasWalk((int)profile.walkStyle) ? (int)profile.walkStyle : 0;
        ApplyAgentSettings();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            CheckAnimator();
            if (hasWalkStyle) animator.SetFloat(WalkStyleHash, walkStyle);
        }
    }

    private float TurnSpeed => turnSpeed * profileTurn;
    private float TurnSmoothing => Mathf.Clamp(turnSmoothing / Mathf.Max(.5f, profileTurn), .02f, .6f);
    // A hurried person steps off sooner; a relaxed one finishes the turn first.
    private float TurnFirstMax => Mathf.Max(turnFirstMax, turnOnSpotMax) * (1.2f - .5f * profileUrgency);
    private float SpotTurnSpeed => turnOnSpotSpeed * profileTurn;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        seating = GetComponent<NpcSeating>();
        animator = GetComponentInChildren<Animator>();
        speedMultiplier = UnityEngine.Random.Range(1f - speedJitter, 1f + speedJitter);
        normalAvoidance = agent.obstacleAvoidanceType;
        ApplyAgentSettings();
    }

    private void OnEnable()
    {
        ApplyAgentSettings();
        if (!everyone.Contains(this)) everyone.Add(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => everyone.Clear();

    // Someone else within arm's reach: a doorway or a busy aisle. Turning on the
    // spot there is brisk, and the walk sets off sooner - nobody holds the door.
    private bool Crowded()
    {
        Vector3 me = transform.position;
        foreach (NpcLocomotion other in everyone)
        {
            if (other == null || other == this || !other.isActiveAndEnabled) continue;
            Vector3 d = other.transform.position - me;
            d.y = 0f;
            if (d.sqrMagnitude < 1.1f * 1.1f) return true;
        }
        return false;
    }

    private void ApplyAgentSettings()
    {
        if (agent == null) return;
        agent.updateRotation = false;   // the body's heading is this component's
        agent.autoBraking = true;
        agent.speed = baseSpeed * speedMultiplier * profileSpeed;
        agent.acceleration = setOffUntil > 0f ? Mathf.Min(acceleration, setOffAcceleration) * profileAcceleration : acceleration * profileAcceleration;
    }

    // The feet are about to carry the body off (a turn has finished, or a walk
    // carries on into a new leg): keep the walk clip on through the moment the
    // speed is still near zero, and from a standstill build the speed up gently.
    private void BeginSetOff(bool fromStandstill)
    {
        holdWalkUntil = Time.time + walkOnHold;
        walkingAnimation = true;
        walkChangeTimer = 0f;
        if (fromStandstill && agent != null)
        {
            setOffUntil = Time.time + setOffSeconds;
            agent.acceleration = Mathf.Min(acceleration, setOffAcceleration) * profileAcceleration;
        }
    }

    private void EndSetOff()
    {
        if (setOffUntil <= 0f) return;
        setOffUntil = 0f;
        if (agent != null) agent.acceleration = acceleration * profileAcceleration;
    }

    // ------------------------------------------------------------ requests

    /// <summary>
    /// Walk to <paramref name="destination"/>. Seated NPCs stand up first and
    /// start walking when the agent is handed back. False only when there is
    /// no agent to drive (off the NavMesh and not seated).
    /// </summary>
    public bool MoveTo(Vector3 destination, Move options)
    {
        if (seating != null && seating.Busy)
        {
            // The chair owns the body: get up, then go.
            Vector3 where = destination;
            Move how = options;
            afterStanding = () => MoveTo(where, how);
            // The seating turns the body towards the destination on the way
            // back from the chair, so the walk carries straight on from there.
            if (!seating.RequestStand(() => { Action go = afterStanding; afterStanding = null; go?.Invoke(); }, destination))
            {
                afterStanding = null;
                return StartLeg(destination, options);
            }
            return true;
        }
        return StartLeg(destination, options);
    }

    private bool StartLeg(Vector3 destination, Move options)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        goal = destination;
        move = options;
        hasGoal = true;
        paused = false;
        HasArrived = false;
        GaveUp = false;
        facing = null;
        stage = 0;
        postGiveUpCycles = 0;
        lastRecovery = Recovery.None;
        steppingAside = false;
        yielding = false;
        pushedBackSince = -1f;
        yieldsWithoutProgress = 0;
        EndPassThrough();
        bestRemaining = float.PositiveInfinity;
        rebaseRemaining = true;
        stalledSince = Time.time;
        lastCycleAt = Time.time;
        agent.isStopped = false;
        agent.stoppingDistance = Mathf.Max(0f, options.stoppingDistance);
        agent.avoidancePriority = Mathf.Clamp(options.priority, 0, 99);
        agent.SetDestination(destination);
        BeginTurnFirst();
        return true;
    }

    // A person facing the wrong way turns, then walks; an agent released
    // straight away accelerates along the path while the body is still
    // turning, which reads as a sideways slide out of every chair and slot.
    private void BeginTurnFirst()
    {
        Vector3 route = RouteDirection();
        float angle = route.sqrMagnitude > 1e-4f ? Vector3.Angle(Flat(transform.forward), route) : 0f;
        float moving = Mathf.Max(Speed, bodySpeed);
        if (angle < turnFirstBeyond)
        {
            turningFirst = false;
            // Straight off: from a standstill the first steps build up; already
            // walking, the clip just carries on into the new leg.
            BeginSetOff(moving < .3f);
            return;
        }
        // Already on the move (a step back from the counter handing over to the
        // walk to a seat, the street walk handing over at the door, the walk out
        // of a chair): stopping dead to turn would be a stop-and-spin in the
        // middle of one walk. The walk curves onto the new route instead - only a
        // real about-turn still stops.
        if (moving > keepWalkingAbove && angle < 120f)
        {
            turningFirst = false;
            if (Speed < moving * .5f) agent.velocity = Flat(transform.forward) * Mathf.Min(moving, agent.speed);
            BeginSetOff(false);
            return;
        }
        crowdedTurn = Crowded();
        turningFirst = true;
        turnFirstUntil = Time.time + TurnFirstMax;
        spotYawVelocity = 0f;
        agent.isStopped = true;
    }

    // Which way the walk really starts: towards the first corner of the path
    // (the straight line to the goal can point through a table while the path
    // goes round it). Falls back to the straight line.
    private Vector3 RouteDirection()
    {
        Vector3 here = transform.position;
        if (agent != null && agent.isOnNavMesh)
        {
            routeProbe ??= new NavMeshPath();
            if (NavMesh.CalculatePath(here, goal, agent.areaMask, routeProbe) && routeProbe.status != NavMeshPathStatus.PathInvalid)
            {
                int n = routeProbe.GetCornersNonAlloc(routeCorners);
                for (int i = 1; i < n; i++)
                {
                    Vector3 d = Flat(routeCorners[i] - here);
                    if (d.sqrMagnitude > .04f) return d;
                }
            }
        }
        return Flat(goal - here);
    }

    private void TurnFirst(float dt)
    {
        Vector3 route = agent.hasPath && !agent.pathPending ? Flat(agent.steeringTarget - transform.position) : Flat(goal - transform.position);
        if (route.sqrMagnitude < 1e-4f) route = Flat(goal - transform.position);
        float angle = route.sqrMagnitude > 1e-4f ? Vector3.Angle(Flat(transform.forward), route) : 0f;
        if ((angle <= (crowdedTurn ? 75f : setOffWithin) && !agent.pathPending) || Time.time >= turnFirstUntil)
        {
            turningFirst = false;
            agent.isStopped = false;
            stalledSince = Time.time;
            // The walk picks the turn up where the spot turn left it, the feet
            // already stepping carry straight on into the walk, and the speed
            // builds up over the first steps.
            headingVelocity = spotYawVelocity;
            BeginSetOff(true);
            return;
        }
        agent.isStopped = true;
        if (route.sqrMagnitude > 1e-4f)
        {
            // Eased in and out, at a human pace: a person turning round, not a turret.
            float targetYaw = Mathf.Atan2(route.x, route.z) * Mathf.Rad2Deg;
            float yaw = crowdedTurn
                ? Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref spotYawVelocity, .08f, TurnSpeed * .9f, dt)
                : Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref spotYawVelocity, .16f, SpotTurnSpeed, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    /// <summary>
    /// Stand here. The path is dropped, the agent stops steering and stops
    /// yielding (priority 0: a body on a spot the café placed it on is not
    /// pushed off it). <paramref name="faceRotation"/> is turned to smoothly
    /// (a slot's or a seat's facing); null keeps the current heading.
    /// </summary>
    public void Park(Quaternion? faceRotation)
    {
        hasGoal = false;
        HasArrived = false;
        GaveUp = false;
        steppingAside = false;
        yielding = false;
        turningFirst = false;
        pushedBackSince = -1f;
        EndPassThrough();
        afterStanding = null;
        facing = faceRotation;
        holdWalkUntil = 0f;
        EndSetOff();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.avoidancePriority = 0;
        }
    }

    /// <summary>Turn towards this rotation while standing (arrival or interaction beat). Ignored mid-walk.</summary>
    public void Face(Quaternion rotation) => facing = rotation;

    /// <summary>Hold still without forgetting where we were going (a conversation).</summary>
    public void Pause()
    {
        paused = true;
        if (yielding) { yielding = false; agent.avoidancePriority = priorityBeforeYield; }
        pushedBackSince = -1f;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    /// <summary>Carry on after <see cref="Pause"/>.</summary>
    public void Resume()
    {
        if (!paused) return;
        paused = false;
        stalledSince = Time.time;
        if (hasGoal && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            if (!agent.hasPath && !agent.pathPending) agent.SetDestination(steppingAside ? agent.destination : goal);
            BeginTurnFirst();
        }
    }

    /// <summary>Straight-line distance to the current goal, or 0 without one.</summary>
    public float DistanceToGoal => hasGoal ? Flat(goal - transform.position).magnitude : 0f;

    // ------------------------------------------------------------ each frame

    private void Update()
    {
        TrackBodySpeed(Time.deltaTime);
        // NpcSeating moves the body by hand round the chair (agent off) and feeds
        // the clip through DriveWalk. It has to come before the agent check:
        // that branch used to reset the walk flag every frame while the agent
        // was off, so the steps to and from the seat played in Idle (a slide).
        if (seating != null && seating.Busy) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            // Off the floor (street walk): NpcJourney drives the clip.
            walkingAnimation = false;
            walkChangeTimer = 0f;
            return;
        }

        float dt = Time.deltaTime;
        if (setOffUntil > 0f && Time.time >= setOffUntil) EndSetOff();
        if (hasGoal && !paused && turningFirst) TurnFirst(dt);
        if (hasGoal && !paused) { WatchProgress(); WatchStall(); }
        UpdateHeading(dt);

        // Turning on the spot faster than a weight shift: the feet step round
        // (the walk clip, slowly) instead of the body swivelling on still legs.
        float yawNow = transform.eulerAngles.y;
        float yawRate = haveLastYaw && dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastYaw, yawNow)) / dt : 0f;
        lastYaw = yawNow;
        haveLastYaw = true;
        bool onSpot = Speed < .25f;
        if (onSpot && yawRate > stepRoundAbove)
        {
            stepRoundUntil = Time.time + .15f;
            stepRoundRate = Mathf.Clamp(yawRate / 330f, .5f, .85f);
        }
        bool steppingRound = onSpot && Time.time < stepRoundUntil;
        Animate(Speed, hasGoal && !paused && !agent.isStopped, agent.isStopped, dt, steppingRound);
    }

    private void TrackBodySpeed(float dt)
    {
        Vector3 p = transform.position;
        if (haveBodyPosition && dt > 1e-5f)
        {
            float v = Mathf.Min(3f, Flat(p - lastBodyPosition).magnitude / dt);   // a warp is not a walk
            bodySpeed = Mathf.Lerp(bodySpeed, v, 1f - Mathf.Exp(-dt / .12f));
        }
        lastBodyPosition = p;
        haveBodyPosition = true;
    }

    /// <summary>True while the body is turning on the spot with its feet stepping round (for checks and traces).</summary>
    public bool SteppingRound => Time.time < stepRoundUntil && Speed < .25f;

    /// <summary>How far the body still has to turn to the facing it was given (Park, Face), degrees; 0 without one.</summary>
    public float FacingLeft => facing.HasValue ? Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, facing.Value.eulerAngles.y)) : 0f;

    /// <summary>
    /// For a body that is moved by hand (NpcSeating's steps round a chair):
    /// feed the walk clip from this speed so the stride still matches the floor.
    /// </summary>
    public void DriveWalk(float speed, float dt)
    {
        // A hand-walked speed is smooth (no agent jitter), so the clip follows it
        // at once rather than through the hold timer that filters agent noise.
        walkingAnimation = speed > (walkingAnimation ? .08f : .2f);
        walkChangeTimer = 0f;
        if (animator == null) return;
        CheckAnimator();
        animator.SetBool(IsWalkingHash, walkingAnimation);
        if (hasWalkRate)
            animator.SetFloat(WalkRateHash, walkingAnimation ? Mathf.Clamp(speed / Mathf.Max(.3f, ClipSpeed), .5f, 1.7f) : 1f);
    }

    /// <summary>True while the walk clip is on (the agent's walk, a hand-walked step, or stepping round).</summary>
    public bool WalkClipOn => walkingAnimation || SteppingRound;

    private float RemainingToGoal()
    {
        if (agent.pathPending) return float.NaN;
        float remaining = agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathInvalid ? agent.remainingDistance : float.PositiveInfinity;
        if (float.IsInfinity(remaining) || float.IsNaN(remaining)) remaining = Flat(agent.destination - transform.position).magnitude;
        return remaining;
    }

    private void WatchProgress()
    {
        if (yielding)
        {
            // Standing still while the group walks past; then carry on.
            if (Time.time < yieldUntil) { agent.velocity = Vector3.zero; return; }
            yielding = false;
            agent.avoidancePriority = priorityBeforeYield;
            agent.isStopped = false;
            rebaseRemaining = true;
            return;
        }
        if (agent.isStopped || agent.pathPending) return;
        float remaining = RemainingToGoal();
        if (float.IsNaN(remaining)) return;

        // A fresh path (after a re-path or a sidestep) starts a new baseline
        // without counting as progress, so a longer route can't reset the ladder.
        if (rebaseRemaining) { bestRemaining = remaining; rebaseRemaining = false; }
        else if (remaining < bestRemaining - progressStep)
        {
            bestRemaining = remaining;
            stalledSince = Time.time;
            yieldsWithoutProgress = 0;
            if (!steppingAside && !GaveUp) stage = 0;
        }

        if (!steppingAside && WatchPushedBack(remaining)) return;

        bool pathComplete = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (steppingAside)
        {
            // The sidestep is over when we got there or ran out of time: back to the goal.
            bool there = pathComplete && remaining <= agent.stoppingDistance + .1f && Speed < .2f;
            if (there || Time.time >= asideUntil) ResumeGoal();
            return;
        }

        // A partial path ends short of the goal; standing at its end is not arriving.
        float toGoal = Flat(goal - transform.position).magnitude;
        bool closeEnough = toGoal <= move.arriveRadius || (pathComplete && remaining <= agent.stoppingDistance + .05f);
        if (closeEnough && Speed < .15f)
        {
            HasArrived = true;
            stage = 0;
        }
        else HasArrived = false;
    }

    // Avoidance can carry the less important body along in front of a group
    // walking the other way: actual velocity against desired velocity, route
    // getting longer, at walking speed. A person stops and lets them pass.
    private bool WatchPushedBack(float remaining)
    {
        Vector3 v = Flat(agent.velocity), want = Flat(agent.desiredVelocity);
        bool herded = v.magnitude > .4f && want.magnitude > .4f
                      && Vector3.Dot(v.normalized, want.normalized) < -.3f
                      && remaining > bestRemaining + .3f
                      && passThroughUntil <= 0f;
        if (!herded) { pushedBackSince = -1f; return false; }
        if (pushedBackSince < 0f) pushedBackSince = Time.time;
        if (Time.time - pushedBackSince < pushedBackSeconds) return false;
        pushedBackSince = -1f;
        // Twice in a row without gaining ground: leave it to the ladder
        // (re-path, sidestep, push) rather than stopping for ever.
        if (yieldsWithoutProgress >= 2) return false;
        yieldsWithoutProgress++;
        lastRecovery = Recovery.Yield;
        yielding = true;
        yieldUntil = Time.time + yieldSeconds * UnityEngine.Random.Range(.8f, 1.3f);
        // Stopped AND not yielding to avoidance (priority 0), so the group
        // flows round a standing person instead of carrying them along.
        priorityBeforeYield = agent.avoidancePriority;
        agent.avoidancePriority = 0;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        return true;
    }

    private void WatchStall()
    {
        if (HasArrived || yielding || agent.isStopped || agent.pathPending || steppingAside) return;
        if (passThroughUntil > 0f && Time.time >= passThroughUntil) EndPassThrough();
        if (Time.time - stalledSince < stallSeconds) return;
        stalledSince = Time.time;
        lastCycleAt = Time.time;

        if (GaveUp)
        {
            // Keep trying gently until the brain does something (it usually
            // picks another spot or hands the walk over at the door).
            postGiveUpCycles++;
            agent.isStopped = false;
            if (move.allowPush && postGiveUpCycles % 2 == 0) BeginPassThrough();
            Repath();
            return;
        }

        stage++;
        switch (stage)
        {
            case 1:
                lastRecovery = Recovery.Repath;
                Repath();
                break;
            case 2:
                if (!TryStepAside()) { lastRecovery = Recovery.Repath; Repath(); }
                break;
            case 3:
                if (move.allowPush)
                {
                    lastRecovery = Recovery.PushThrough;
                    agent.avoidancePriority = Mathf.Clamp(pushPriority + UnityEngine.Random.Range(-5, 6), 0, 99);
                }
                Repath();
                break;
            default:
                lastRecovery = Recovery.GaveUp;
                GaveUp = true;
                Repath();
                break;
        }
    }

    private void Repath()
    {
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(goal);
        rebaseRemaining = true;
    }

    // A point beside the line we are trying to walk, alternating sides, that
    // is on the NavMesh and reachable. Somebody stepping aside is what breaks
    // a face-to-face standoff or a corner block; the goal is resumed after.
    private bool TryStepAside()
    {
        Vector3 forward = Flat(agent.steeringTarget - transform.position);
        if (forward.sqrMagnitude < 1e-4f) forward = Flat(goal - transform.position);
        if (forward.sqrMagnitude < 1e-4f) return false;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            asideSide = -asideSide;
            Vector3 probe = transform.position + side * (asideSide * stepAsideDistance) - forward * .2f;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .6f, NavMesh.AllAreas)) continue;
            if (Flat(hit.position - transform.position).magnitude < .35f) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            lastRecovery = Recovery.StepAside;
            steppingAside = true;
            asideUntil = Time.time + stepAsideSeconds;
            agent.isStopped = false;
            agent.avoidancePriority = Mathf.Clamp(Mathf.Max(move.priority, 80), 0, 99); // yield while stepping aside
            agent.SetDestination(hit.position);
            rebaseRemaining = true;
            return true;
        }
        return false;
    }

    private void ResumeGoal()
    {
        steppingAside = false;
        agent.avoidancePriority = Mathf.Clamp(move.priority, 0, 99);
        stalledSince = Time.time;
        Repath();
    }

    private void BeginPassThrough()
    {
        lastRecovery = Recovery.PassThrough;
        passThroughUntil = Time.time + passThroughSeconds;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void EndPassThrough()
    {
        if (passThroughUntil <= 0f) return;
        passThroughUntil = 0f;
        if (agent != null) agent.obstacleAvoidanceType = normalAvoidance;
    }

    // ------------------------------------------------------------ heading

    private void UpdateHeading(float dt)
    {
        Vector3 velocity = Flat(agent.velocity);
        float speed = velocity.magnitude;
        // Just set off: the speed is still low, but the heading carries on
        // turning towards the route (it used to stop dead for a frame here).
        bool walking = hasGoal && !paused && !agent.isStopped && (speed > headingDeadZone || Time.time < holdWalkUntil);

        if (walking)
        {
            // The route's own direction: the next corner. Avoidance wiggles do
            // not live in it, so they do not turn the body.
            Vector3 route = Flat(agent.steeringTarget - transform.position);
            if (route.sqrMagnitude < 1e-4f) route = Flat(agent.desiredVelocity);
            if (route.sqrMagnitude < 1e-4f) route = velocity;

            float deviation = Vector3.Angle(route, velocity);
            if (deviation > detourAngle)
            {
                if (detourSince < 0f) detourSince = Time.time;
                followingDetour = Time.time - detourSince >= detourHold;
            }
            else { detourSince = -1f; followingDetour = false; }

            Vector3 want;
            if (deviation > 135f) want = Vector3.zero;                 // pushed backwards: hold the heading, don't spin
            else if (followingDetour) want = velocity;                 // a real detour: face the way we are going
            else want = route;

            if (want.sqrMagnitude > 1e-6f)
            {
                float targetYaw = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg;
                // The first steps finish the turn at the on-the-spot pace, not
                // the brisk walking one (a snap just as the feet set off).
                bool settingOff = Time.time < holdWalkUntil;
                float smoothing = settingOff ? Mathf.Max(TurnSmoothing, .2f) : TurnSmoothing;
                float fastest = settingOff ? Mathf.Min(TurnSpeed, SpotTurnSpeed) : TurnSpeed;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref headingVelocity, smoothing, fastest, dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
        else
        {
            detourSince = -1f;
            followingDetour = false;
            if (!turningFirst) headingVelocity = 0f;
            if (facing.HasValue && !turningFirst)
            {
                // Eased, and the feet step round (Update) - not a constant-rate swivel.
                float targetYaw = facing.Value.eulerAngles.y;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref spotYawVelocity, .18f, SpotTurnSpeed, dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else if (!turningFirst) spotYawVelocity = 0f;
        }
    }

    // ------------------------------------------------------------ animation

    private void CheckAnimator()
    {
        if (animatorChecked || animator == null) return;
        animatorChecked = true;
        if (animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == WalkRateHash && p.type == AnimatorControllerParameterType.Float) hasWalkRate = true;
            if (p.nameHash == WalkStyleHash && p.type == AnimatorControllerParameterType.Float) hasWalkStyle = true;
        }
        if (hasWalkStyle) animator.SetFloat(WalkStyleHash, walkStyle);
    }

    private void Animate(float speed, bool mayWalk, bool stoppedNow, float dt, bool steppingRound = false)
    {
        if (animator == null) return;
        CheckAnimator();
        bool wants = mayWalk && (speed > (walkingAnimation ? walkStop : walkStart) || Time.time < holdWalkUntil);
        if (wants == walkingAnimation) walkChangeTimer = 0f;
        else
        {
            walkChangeTimer += dt;
            if (walkChangeTimer >= walkHold || (!wants && stoppedNow))
            {
                walkingAnimation = wants;
                walkChangeTimer = 0f;
            }
        }
        if (steppingRound && !walkingAnimation)
        {
            animator.SetBool(IsWalkingHash, true);
            if (hasWalkRate) animator.SetFloat(WalkRateHash, stepRoundRate);
            return;
        }
        animator.SetBool(IsWalkingHash, walkingAnimation);
        if (hasWalkRate)
            animator.SetFloat(WalkRateHash, walkingAnimation ? Mathf.Clamp(speed / Mathf.Max(.3f, ClipSpeed), .6f, 1.7f) : 1f);
    }

    private void OnDisable()
    {
        everyone.Remove(this);
        EndPassThrough();
        EndSetOff();
        holdWalkUntil = 0f;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(IsWalkingHash, false);
        walkingAnimation = false;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
