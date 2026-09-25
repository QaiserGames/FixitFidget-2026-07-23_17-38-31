using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Decorative street motion on authored routes; never changes café gameplay.
/// Cars stop for people on crossings and for café cars turning across their lane
/// (<see cref="RoadBlock"/>), never queue on a crossing or in a junction box, and can
/// lend a lane to a café customer's car at runtime (<see cref="JoinTraffic"/>).
/// </summary>
public sealed class StreetLife : MonoBehaviour
{
    public enum TrafficSignalState { Phase0Green, Phase0Amber, AllRedToPhase1, Phase1Green, Phase1Amber, AllRedToPhase0 }

    [Serializable]
    public sealed class SignalHead
    {
        [Range(0, 1)] public int signalGroup;
        public Renderer red, amber, green;
        [NonSerialized] internal MaterialPropertyBlock propertyBlock;
    }

    [Serializable]
    public sealed class Actor
    {
        public Transform actor;
        [Tooltip("World-space route markers. Closed routes connect the last marker back to the first.")]
        public Transform[] waypoints = Array.Empty<Transform>();
        [Tooltip("Continue out of the neighborhood, then reuse this actor at the far start. Both endpoints must be hidden beyond the playable camera views.")]
        public bool openRoute;
        [Min(0f)] public float respawnDelay = 4f;
        [Min(0f)] public float respawnDelayVariation = 2f;
        [Min(0f)] public float speed = 1f;
        [Range(0f, 1f)] public float startPhase;
        [Tooltip("Cars with the same nonempty group share one route and cannot overtake. Use the same waypoint transforms in the same order. Rebuild routes after changing this setup.")]
        public string trafficGroup = "";
        [Min(0.1f)] public float vehicleLength = 4.2f;
        [Tooltip("Minimum bumper-to-bumper distance. The larger gap requested by two adjacent cars wins.")]
        [Min(0f)] public float minimumGap = 2f;
        [Tooltip("Optional stop-line markers, projected onto this route when routes are rebuilt. Leave empty for uninterrupted motion.")]
        public Transform[] stopWaypoints = Array.Empty<Transform>();
        [Min(0f)] public float stopDuration = 1.2f;
        [Tooltip("-1 uses ordinary timed stops; 0 and 1 alternate junction right-of-way. Place stop markers before every crossing. Signal stops release on green without the ordinary dwell.")]
        [Range(-1, 1)] public int junctionSignalPhase = -1;
        [Tooltip("Use for airborne loops. Ground routes follow straight segments exactly.")]
        public bool smoothRoute;
        public bool alignToSlope;
        [Min(0f)] public float turnSpeed = 180f;
        [Range(0f, 30f)] public float bankAngle;
        [Tooltip("Optional existing controller with the IsWalking boolean.")]
        public Animator animator;
        public Transform[] wheels = Array.Empty<Transform>();
        [Min(0.01f)] public float wheelRadius = 0.25f;
        public Vector3 wheelAxis = Vector3.right;
        [Tooltip("Paired wings, left then right; pivots must be at their shoulders.")]
        public Transform[] wings = Array.Empty<Transform>();
        [Range(0f, 80f)] public float wingAngle = 25f;
        [Min(0f)] public float flapFrequency = 2f;
        public Vector3 wingAxis = Vector3.forward;
        [Tooltip("Alternate opposing legs; pivots must be at their hips.")]
        public Transform[] legs = Array.Empty<Transform>();
        [Range(0f, 60f)] public float legAngle = 18f;
        [Min(0f)] public float strideLength = 0.7f;
        public Vector3 legAxis = Vector3.right;

        [NonSerialized] internal Vector3[] points;
        [NonSerialized] internal float[] lengths;
        [NonSerialized] internal float distance, length, wheelRotation, phase, stridePhase;
        [NonSerialized] internal Quaternion[] wheelRest, wingRest, legRest;
        [NonSerialized] internal bool drivesAnimator;
        [NonSerialized] internal bool trafficManaged;
        [NonSerialized] internal float trafficMove;
        [NonSerialized] internal float[] stopDistances;
        [NonSerialized] internal int nextStop;
        [NonSerialized] internal float distanceToStop, stopRemaining;
        [NonSerialized] internal bool waitingForSignal, respawning;
        [NonSerialized] internal float respawnRemaining;
        [NonSerialized] internal int respawnCycle, routeOrder;
        // Personal space (walkers only): sideways step off the route line, and why.
        [NonSerialized] internal bool isWalker;
        [NonSerialized] internal float lateral, lateralTarget, heldFor, clearFor;
        [NonSerialized] internal Vector3 offsetRight, velocity;
        [NonSerialized] internal NavMeshObstacle obstacle;
        // A car borrowing a lane at runtime (a café customer's car): it leaves the
        // lane instead of respawning when it reaches the end of the route.
        [NonSerialized] internal bool guest;
        [NonSerialized] internal Action<Actor> guestReachedEnd;
        [NonSerialized] internal float lastSpeed;

        /// <summary>Metres travelled along the route (read-only view for café cars).</summary>
        public float RouteDistance => distance;
        /// <summary>Length of the whole route in metres.</summary>
        public float RouteLength => length;
        /// <summary>Speed actually driven last frame, metres per second.</summary>
        public float CurrentSpeed => lastSpeed;
    }

    public List<Actor> actors = new List<Actor>();
    public List<SignalHead> signalHeads = new List<SignalHead>();
    [Min(1f)] public float signalGreenSeconds = 14f;
    [Tooltip("All approaches hold while cars already inside a junction clear it. Author speeds and stop-line positions to clear within this interval.")]
    [Min(1f)] public float signalClearanceSeconds = 5f;
    [Tooltip("Someone waiting at a signalled crossing (a café visitor) is like a pressed button: the green of the traffic " +
             "in their way ends early once it has run this long, seconds. The amber and all-red that follow are unchanged.")]
    [Min(2f)] public float walkRequestMinGreen = 7f;

    [Header("Walkers' personal space")]
    [Tooltip("Street walkers (actors with legs or a walking Animator) never walk through anybody. They keep this " +
             "much room behind whoever is ahead of them, metres centre to centre.")]
    [Min(0.3f)] public float walkerFollowGap = 0.85f;
    [Tooltip("Half the width of a walker's path, metres. Someone further to the side than this is not in the way.")]
    [Min(0.2f)] public float walkerCorridor = 0.5f;
    [Tooltip("How far ahead a walker looks for people in its way, metres.")]
    [Min(0.5f)] public float walkerLookAhead = 1.8f;
    [Tooltip("How far a walker steps aside to pass someone, metres. Oncoming people get a little over half of it.")]
    [Range(0f, 1f)] public float walkerSidestep = 0.55f;
    [Tooltip("Seconds a walker follows someone slower (or waits behind someone standing) before stepping round them.")]
    [Min(0f)] public float walkerPatience = 1.2f;
    [Tooltip("Play Mode: walkers also make way for café NPCs and the player, and café NPCs steer round walkers.")]
    public bool walkersShareTheSidewalk = true;

    [Header("Crossings and turning cars")]
    [Tooltip("How far ahead a car looks for a crossing in use or a car turning across its lane, metres.")]
    [Min(2f)] public float roadBlockLookAhead = 14f;
    [Tooltip("Room a car leaves before a crossing in use or a car in its way, metres.")]
    [Min(0.2f)] public float roadBlockStopGap = 0.8f;
    [Tooltip("How hard a car brakes for a crossing in use, metres per second squared.")]
    [Min(0.5f)] public float roadBlockBraking = 3.5f;

    private struct Body
    {
        public Vector3 position, velocity;
        public float radius;
        public Actor owner;
    }

    /// <summary>
    /// A rectangle on the ground that traffic treats like something standing in its
    /// lane: a pedestrian crossing while people use it, or a café car turning into or
    /// out of the lot. While <see cref="active"/>, cars stop before it (braking, not
    /// stopping dead). With <see cref="letTrafficInsideClear"/> a car already on it
    /// keeps going, so nobody ends up parked on a crossing. A <see cref="keepClear"/>
    /// block is never stopped on even when nobody uses it: a car only drives onto it
    /// when there is room for its whole body beyond it (crossings, junction boxes).
    /// </summary>
    public sealed class RoadBlock
    {
        public Vector3 center;
        /// <summary>x: half width along the block's right, y: half length along its forward.</summary>
        public Vector2 halfSize;
        public float yaw;
        public bool active;
        public bool letTrafficInsideClear;
        public bool keepClear;
        public string label;
    }

    /// <summary>Someone or something on the street that isn't a StreetLife actor (café visitors walking outside, café cars).</summary>
    public interface IStreetBody
    {
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        float Radius { get; }
    }

    private static readonly List<RoadBlock> roadBlocks = new List<RoadBlock>();
    private static readonly List<IStreetBody> streetBodies = new List<IStreetBody>();
    private static readonly List<StreetLife> instances = new List<StreetLife>();
    private static NavMeshAgent[] cachedAgents = Array.Empty<NavMeshAgent>();
    private static CharacterController[] cachedCharacters = Array.Empty<CharacterController>();
    private static float nextCacheScan;
    private static int nextGuestOrder = 100000;
    private readonly List<Actor> finishedGuests = new List<Actor>();
    private const float CarHalfWidth = 1.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        roadBlocks.Clear();
        streetBodies.Clear();
        instances.Clear();
        cachedAgents = Array.Empty<NavMeshAgent>();
        cachedCharacters = Array.Empty<CharacterController>();
        nextCacheScan = 0f;
        nextGuestOrder = 100000;
    }

    /// <summary>The street in Play Mode (the first enabled one), or null.</summary>
    public static StreetLife Main => instances.Count > 0 ? instances[0] : null;

    public static RoadBlock AddRoadBlock(Vector3 center, Vector2 halfSize, float yaw, bool active,
                                         bool letTrafficInsideClear, bool keepClear, string label)
    {
        var block = new RoadBlock
        {
            center = center, halfSize = halfSize, yaw = yaw, active = active,
            letTrafficInsideClear = letTrafficInsideClear, keepClear = keepClear, label = label
        };
        roadBlocks.Add(block);
        return block;
    }

    public static void RemoveRoadBlock(RoadBlock block) => roadBlocks.Remove(block);

    public static void RegisterBody(IStreetBody body)
    {
        if (body != null && !streetBodies.Contains(body)) streetBodies.Add(body);
    }

    public static void UnregisterBody(IStreetBody body) => streetBodies.Remove(body);
    private readonly List<Body> bodies = new List<Body>();
    private NavMeshAgent[] sidewalkAgents = Array.Empty<NavMeshAgent>();
    private CharacterController[] sidewalkCharacters = Array.Empty<CharacterController>();
    private float nextBodyScan;
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private readonly List<List<Actor>> trafficGroups = new List<List<Actor>>();
    private string trafficSetupError;
    private float signalTime;
    private readonly bool[] walkRequests = new bool[2];
    private TrafficSignalState displayedSignalState = (TrafficSignalState)(-1);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public float TrafficSignalTime => signalTime;
    public TrafficSignalState CurrentSignalState
    {
        get
        {
            float green = Mathf.Max(1f, signalGreenSeconds), halfCycle = green + Mathf.Max(1f, signalClearanceSeconds);
            float time = Mathf.Repeat(signalTime, halfCycle * 2f);
            float amber = Mathf.Min(2f, Mathf.Max(1f, signalClearanceSeconds));
            if (time < green) return TrafficSignalState.Phase0Green;
            if (time < green + amber) return TrafficSignalState.Phase0Amber;
            if (time < halfCycle) return TrafficSignalState.AllRedToPhase1;
            if (time < halfCycle + green) return TrafficSignalState.Phase1Green;
            if (time < halfCycle + green + amber) return TrafficSignalState.Phase1Amber;
            return TrafficSignalState.AllRedToPhase0;
        }
    }
    public int CurrentGreenSignalPhase => CurrentSignalState == TrafficSignalState.Phase0Green ? 0 : CurrentSignalState == TrafficSignalState.Phase1Green ? 1 : -1;

    private void Awake() => RebuildRoutes();

    /// <summary>Call after replacing actors/routes at runtime. Markers stay fixed during playback.</summary>
    public void RebuildRoutes()
    {
        // Initial clearance lets any car authored beyond a stop line leave a junction first.
        signalTime = Mathf.Max(1f, signalGreenSeconds);
        int routeOrder = 0;
        foreach (Actor entry in actors)
        {
            if (entry == null) continue;
            if (entry.respawning && entry.actor != null) entry.actor.gameObject.SetActive(true);
            entry.respawning = entry.waitingForSignal = false;
            entry.isWalker = false;
            entry.lateral = entry.lateralTarget = entry.heldFor = entry.clearFor = 0f;
            entry.offsetRight = entry.velocity = Vector3.zero;
            entry.respawnRemaining = 0f;
            entry.respawnCycle = 0;
            entry.routeOrder = routeOrder++;
            entry.length = 0f;
            entry.drivesAnimator = false;
            entry.trafficManaged = !string.IsNullOrWhiteSpace(entry.trafficGroup);
            entry.trafficMove = 0f;
            entry.stopDistances = Array.Empty<float>();
            entry.stopRemaining = entry.distanceToStop = 0f;
            entry.nextStop = 0;
            if (entry.actor == null || entry.waypoints == null) continue;
            var markers = new List<Vector3>();
            foreach (Transform marker in entry.waypoints)
                if (marker != null) markers.Add(marker.position);
            if (markers.Count < 2) continue;

            var samples = new List<Vector3>();
            int steps = entry.smoothRoute && markers.Count >= 3 ? 12 : 1;
            int segmentCount = entry.openRoute ? markers.Count - 1 : markers.Count;
            for (int segment = 0; segment < segmentCount; segment++)
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    Vector3 point = markers[segment];
                    if (steps > 1)
                    {
                        Vector3 a = markers[entry.openRoute ? Mathf.Max(0, segment - 1) : (segment + markers.Count - 1) % markers.Count];
                        Vector3 b = point;
                        Vector3 c = markers[(segment + 1) % markers.Count];
                        Vector3 d = markers[entry.openRoute ? Mathf.Min(markers.Count - 1, segment + 2) : (segment + 2) % markers.Count];
                        point = 0.5f * ((2f * b) + (-a + c) * t +
                            (2f * a - 5f * b + 4f * c - d) * t * t +
                            (-a + 3f * b - 3f * c + d) * t * t * t);
                    }
                    if (samples.Count == 0 || (point - samples[samples.Count - 1]).sqrMagnitude > 0.000001f)
                        samples.Add(point);
                }
            samples.Add(entry.openRoute ? markers[markers.Count - 1] : samples[0]);
            if (samples.Count < 2) continue;
            entry.points = samples.ToArray();
            entry.lengths = new float[entry.points.Length];
            for (int i = 1; i < entry.points.Length; i++)
                entry.lengths[i] = entry.lengths[i - 1] + Vector3.Distance(entry.points[i - 1], entry.points[i]);
            entry.length = entry.lengths[entry.lengths.Length - 1];
            if (entry.length < 0.001f) continue;

            entry.distance = (entry.openRoute ? Mathf.Clamp01(entry.startPhase) : Mathf.Repeat(entry.startPhase, 1f)) * entry.length;
            entry.phase = entry.startPhase * Mathf.PI * 2f;
            entry.stridePhase = entry.phase;
            entry.wheelRotation = 0f;
            entry.wheelRest = RestRotations(entry.wheels);
            entry.wingRest = RestRotations(entry.wings);
            entry.legRest = RestRotations(entry.legs);
            entry.actor.position = Sample(entry, entry.distance, out Vector3 direction);
            if (!entry.alignToSlope) direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
                entry.actor.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (entry.animator != null && entry.animator.runtimeAnimatorController != null)
            {
                entry.animator.applyRootMotion = false;
                entry.animator.updateMode = AnimatorUpdateMode.Normal;
                foreach (AnimatorControllerParameter parameter in entry.animator.parameters)
                    if (parameter.nameHash == IsWalking && parameter.type == AnimatorControllerParameterType.Bool)
                        entry.drivesAnimator = true;
            }
            // People on foot. Cars, birds and anything in a traffic group keep their own rules.
            entry.isWalker = !entry.trafficManaged
                && (entry.wings == null || entry.wings.Length == 0) && (entry.wheels == null || entry.wheels.Length == 0)
                && (entry.drivesAnimator || entry.legs != null && entry.legs.Length > 0);
        }
        RebuildTraffic();
        // Traffic may correct crowded starting phases. Resolve the next stop after that correction.
        foreach (Actor entry in actors)
            if (entry != null && entry.actor != null && entry.length >= 0.001f) RebuildStops(entry);
        displayedSignalState = (TrafficSignalState)(-1);
        RefreshSignalHeads();
    }

    private void Update() => Advance(Time.deltaTime);

    /// <summary>Advances decorative motion. Exposed for deterministic editor/playtest checks.</summary>
    public void Advance(float dt)
    {
        if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
        UpdateRespawns(Mathf.Min(dt, 0.25f));
        SolveTraffic(dt);
        GatherBodies();
        foreach (Actor entry in actors)
        {
            if (entry == null || entry.actor == null || entry.length < 0.001f || !entry.actor.gameObject.activeInHierarchy)
                continue;
            Vector3 before = entry.actor.position;
            float advance = entry.trafficManaged ? entry.trafficMove : ProposeMovement(entry, dt, dt);
            if (entry.isWalker) advance = MindTheWay(entry, advance, dt);
            entry.distance = entry.openRoute ? Mathf.Min(entry.distance + advance, entry.length) : Mathf.Repeat(entry.distance + advance, entry.length);
            RecordStopProgress(entry, advance);
            entry.actor.position = Sample(entry, entry.distance, out Vector3 direction);
            if (entry.isWalker)
            {
                entry.actor.position += WalkerOffset(entry, direction, dt);
                entry.velocity = (entry.actor.position - before) / dt;
                if (entry.obstacle != null) entry.obstacle.velocity = entry.velocity;
                // Face the way the feet actually go, sidesteps included, but never more
                // than about 40 degrees off the route: people sidestep, they don't turn to the kerb.
                Vector3 step = entry.actor.position - before, route = direction;
                step.y = route.y = 0f;
                if (step.sqrMagnitude > 1e-8f && route.sqrMagnitude > 1e-8f)
                {
                    route.Normalize();
                    float forward = Vector3.Dot(step, route);
                    Vector3 sideways = step - route * forward;
                    direction = route * Mathf.Max(forward, sideways.magnitude * 1.2f) + sideways;
                }
            }
            float moved = entry.trafficManaged ? advance : Vector3.Distance(before, entry.actor.position);
            entry.lastSpeed = moved / dt;
            // Walkers slowed to a shuffle behind someone stand rather than moonwalk.
            bool walking = moved / dt > (entry.isWalker ? 0.15f : 0.01f);
            if (entry.drivesAnimator && entry.animator != null)
                entry.animator.SetBool(IsWalking, walking);
            if (entry.openRoute && entry.distance >= entry.length)
            {
                // A borrowed car hands itself back rather than looping round again.
                if (entry.guest) { finishedGuests.Add(entry); continue; }
                BeginRespawn(entry);
            }
            if (!walking) continue;

            if (!entry.alignToSlope) direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
            {
                float bend = Vector3.SignedAngle(entry.actor.forward, direction, Vector3.up);
                float bank = -Mathf.Clamp(bend / 45f, -1f, 1f) * entry.bankAngle;
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
                entry.actor.rotation = Quaternion.RotateTowards(entry.actor.rotation, target, entry.turnSpeed * dt);
            }

            entry.wheelRotation = Mathf.Repeat(entry.wheelRotation + moved / Mathf.Max(0.01f, entry.wheelRadius) * Mathf.Rad2Deg, 360f);
            Pose(entry.wheels, entry.wheelRest, entry.wheelAxis, entry.wheelRotation, false);
            entry.phase = Mathf.Repeat(entry.phase + dt * Mathf.PI * 2f * Mathf.Max(0f, entry.flapFrequency), Mathf.PI * 2f);
            Pose(entry.wings, entry.wingRest, entry.wingAxis, Mathf.Sin(entry.phase) * entry.wingAngle, true);
            entry.stridePhase = Mathf.Repeat(entry.stridePhase + moved / Mathf.Max(0.05f, entry.strideLength) * Mathf.PI * 2f, Mathf.PI * 2f);
            Pose(entry.legs, entry.legRest, entry.legAxis, Mathf.Sin(entry.stridePhase) * entry.legAngle, true);
        }
        // Borrowed cars that reached the end of their lane go back to whoever lent them.
        if (finishedGuests.Count > 0)
        {
            foreach (Actor guest in finishedGuests)
            {
                RemoveFromTraffic(guest);
                guest.guestReachedEnd?.Invoke(guest);
            }
            finishedGuests.Clear();
        }
        ServeWalkRequests();
        // Gate time advances by the same bounded step as traffic, so a hitch cannot skip
        // clearance while cars inside an intersection have barely moved.
        signalTime = Mathf.Repeat(signalTime + Mathf.Min(dt, 0.25f), 2f * (Mathf.Max(1f, signalGreenSeconds) + Mathf.Max(1f, signalClearanceSeconds)));
        RefreshSignalHeads();
    }

    // ---------- walkers' personal space ----------

    // Everyone a walker has to respect this frame: the other walkers, and in Play
    // Mode the café's NavMesh agents and the player.
    private void GatherBodies()
    {
        bodies.Clear();
        bool anyWalker = false;
        bool sharing = walkersShareTheSidewalk && Application.isPlaying;
        foreach (Actor entry in actors)
        {
            if (entry == null || !entry.isWalker || entry.actor == null || !entry.actor.gameObject.activeInHierarchy) continue;
            anyWalker = true;
            if (sharing && entry.obstacle == null) entry.obstacle = AddObstacle(entry.actor);
            bodies.Add(new Body { position = entry.actor.position, velocity = entry.velocity, radius = 0.3f, owner = entry });
        }
        if (!anyWalker || !sharing) return;
        if (Time.unscaledTime >= nextBodyScan)
        {
            nextBodyScan = Time.unscaledTime + 0.5f;
            sidewalkAgents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude);
            sidewalkCharacters = FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude);
        }
        foreach (NavMeshAgent agent in sidewalkAgents)
            if (agent != null && agent.isActiveAndEnabled)
                bodies.Add(new Body { position = agent.transform.position, velocity = agent.velocity, radius = 0.3f });
        foreach (CharacterController character in sidewalkCharacters)
            if (character != null && character.enabled)
                bodies.Add(new Body { position = character.transform.position, velocity = character.velocity, radius = 0.3f });
        // Café visitors walking to and from the door, and café cars, are off the NavMesh.
        foreach (IStreetBody body in streetBodies)
            if (body != null)
                bodies.Add(new Body { position = body.Position, velocity = body.Velocity, radius = Mathf.Max(0.1f, body.Radius) });
    }

    // Street walkers never walk through anybody. Someone ahead going the same way,
    // or standing still, is followed at walkerFollowGap; after walkerPatience the
    // walker steps round them. Oncoming people get room: both step away from each
    // other's side and nobody stops, so two walkers can never hold each other up
    // face to face.
    private float MindTheWay(Actor walker, float advance, float dt)
    {
        Sample(walker, walker.distance, out Vector3 heading);
        heading.y = 0f;
        if (heading.sqrMagnitude < 1e-8f) return advance;
        heading.Normalize();
        Vector3 here = walker.actor.position;
        Vector3 right = new Vector3(heading.z, 0f, -heading.x);
        float allowed = advance;
        float blockerAlong = float.PositiveInfinity, blockerSide = 0f;
        float oncomingAlong = float.PositiveInfinity, oncomingSide = 0f;
        foreach (Body body in bodies)
        {
            if (body.owner == walker) continue;
            Vector3 offset = body.position - here;
            if (Mathf.Abs(offset.y) > 1.6f) continue;
            offset.y = 0f;
            float along = Vector3.Dot(offset, heading);
            if (along <= 0.05f || along > walkerLookAhead) continue;
            float side = Vector3.Dot(offset, right);
            // Wider bodies (a café car) take up more of the path than a person does.
            float extra = Mathf.Max(0f, body.radius - 0.3f);
            if (Vector3.Dot(body.velocity, heading) < -0.25f)
            {
                if (Mathf.Abs(side) < walkerCorridor + 0.3f + extra && along < oncomingAlong) { oncomingAlong = along; oncomingSide = side; }
                continue;
            }
            if (Mathf.Abs(side) > walkerCorridor + extra) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, along - walkerFollowGap));
            if (along < blockerAlong) { blockerAlong = along; blockerSide = side; }
        }
        if (oncomingAlong < 1.2f) allowed = Mathf.Min(allowed, advance * 0.65f);

        walker.heldFor = allowed < advance * 0.9f ? walker.heldFor + dt : 0f;
        if (!float.IsInfinity(oncomingAlong))
        {
            // Away from their side; dead ahead, keep right.
            walker.lateralTarget = (Mathf.Abs(oncomingSide) < 0.08f ? 1f : -Mathf.Sign(oncomingSide)) * walkerSidestep * 0.6f;
            walker.clearFor = 0f;
        }
        else if (!float.IsInfinity(blockerAlong))
        {
            // Round them, away from their side; dead ahead, pass on their left.
            if (walker.heldFor >= walkerPatience)
                walker.lateralTarget = (Mathf.Abs(blockerSide) < 0.08f ? -1f : -Mathf.Sign(blockerSide)) * walkerSidestep;
            walker.clearFor = 0f;
        }
        else
        {
            walker.clearFor += dt;
            if (walker.clearFor > 0.8f) walker.lateralTarget = 0f;
        }
        return allowed;
    }

    private static Vector3 WalkerOffset(Actor walker, Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 1e-8f)
        {
            direction.Normalize();
            Vector3 right = new Vector3(direction.z, 0f, -direction.x);
            // The sideways direction turns with the walker instead of snapping round at corners.
            walker.offsetRight = walker.offsetRight.sqrMagnitude < 0.5f ? right
                : Vector3.RotateTowards(walker.offsetRight, right, Mathf.Max(90f, walker.turnSpeed) * Mathf.Deg2Rad * dt, 0f);
        }
        walker.lateral = Mathf.MoveTowards(walker.lateral, walker.lateralTarget, 0.9f * dt);
        return walker.offsetRight * walker.lateral;
    }

    // Café NPCs' own avoidance steers round a walker that carries one of these.
    // Not carving: the NavMesh itself never changes.
    private static NavMeshObstacle AddObstacle(Transform actor)
    {
        NavMeshObstacle obstacle = actor.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = actor.gameObject.AddComponent<NavMeshObstacle>();
        Vector3 scale = actor.lossyScale;
        float across = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float up = Mathf.Max(0.01f, Mathf.Abs(scale.y));
        obstacle.carving = false;
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = 0.3f / across;
        obstacle.height = 1.8f / up;
        obstacle.center = new Vector3(0f, 0.9f / up, 0f);
        return obstacle;
    }

    private static float RequiredSpacing(Actor follower, Actor leader) =>
        (Mathf.Max(0.1f, follower.vehicleLength) + Mathf.Max(0.1f, leader.vehicleLength)) * 0.5f +
        Mathf.Max(0f, Mathf.Max(follower.minimumGap, leader.minimumGap)) + 0.01f;

    private static bool SameRoute(Actor a, Actor b)
    {
        if (a.openRoute != b.openRoute || a.smoothRoute != b.smoothRoute || a.waypoints.Length != b.waypoints.Length) return false;
        for (int i = 0; i < a.waypoints.Length; i++)
            if (a.waypoints[i] != b.waypoints[i]) return false;
        return true;
    }

    private void RebuildTraffic()
    {
        trafficGroups.Clear();
        trafficSetupError = null;
        var byName = new Dictionary<string, List<Actor>>(StringComparer.Ordinal);
        foreach (Actor entry in actors)
        {
            if (entry == null || !entry.trafficManaged || entry.actor == null) continue;
            if (!byName.TryGetValue(entry.trafficGroup, out List<Actor> group))
                byName.Add(entry.trafficGroup, group = new List<Actor>());
            group.Add(entry);
        }
        foreach (var pair in byName)
        {
            List<Actor> group = pair.Value;
            bool valid = group[0].length >= 0.001f;
            foreach (Actor entry in group)
                if (entry.length < 0.001f || (valid && !SameRoute(group[0], entry))) valid = false;
            if (!valid)
            {
                ReportTrafficError(pair.Key + ": cars must share one valid route in the same direction.");
                continue; // Invalid groups stay parked rather than running through one another.
            }
            group.Sort(CompareRoutePosition);
            if (group.Count > 1)
            {
                float required = 0f;
                bool crowded = false;
                bool open = group[0].openRoute;
                for (int i = 0; i < (open ? group.Count - 1 : group.Count); i++)
                {
                    Actor follower = group[i], leader = group[(i + 1) % group.Count];
                    required += RequiredSpacing(follower, leader);
                    float gap = open ? leader.distance - follower.distance : Mathf.Repeat(leader.distance - follower.distance, follower.length);
                    if (gap < RequiredSpacing(follower, leader)) crowded = true;
                }
                if (required >= group[0].length)
                {
                    ReportTrafficError(pair.Key + ": this route is too short for the configured car lengths and gaps.");
                    continue;
                }
                if (crowded)
                {
                    // Correct unsafe authored phases only when rebuilding, never by teleporting cars during playback.
                    float slack = (group[0].length - required) / group.Count;
                    float distance = open ? slack * 0.5f : group[0].distance;
                    for (int i = 0; i < group.Count; i++)
                    {
                        Actor entry = group[i];
                        entry.distance = open ? Mathf.Min(distance, entry.length) : Mathf.Repeat(distance, entry.length);
                        entry.actor.position = Sample(entry, entry.distance, out Vector3 direction);
                        if (!entry.alignToSlope) direction.y = 0f;
                        if (direction.sqrMagnitude > 0.000001f) entry.actor.rotation = Quaternion.LookRotation(direction, Vector3.up);
                        distance += RequiredSpacing(entry, group[(i + 1) % group.Count]) + slack;
                    }
                    Debug.Log("StreetLife: adjusted starting car spacing for " + pair.Key + ".", this);
                }
            }
            trafficGroups.Add(group);
        }
    }

    private void ReportTrafficError(string message)
    {
        trafficSetupError = string.IsNullOrEmpty(trafficSetupError) ? message : trafficSetupError + " " + message;
        Debug.LogWarning("StreetLife: " + message, this);
    }

    private static int CompareRoutePosition(Actor a, Actor b) =>
        a.distance != b.distance ? a.distance.CompareTo(b.distance) : a.routeOrder.CompareTo(b.routeOrder);

    private static void BeginRespawn(Actor entry)
    {
        entry.respawning = true;
        entry.respawnCycle++;
        // Repeatable variation spaces arrivals without consuming the gameplay random stream.
        float variation = ((entry.respawnCycle * 37 + entry.routeOrder * 17) % 101) / 100f;
        entry.respawnRemaining = Mathf.Max(0f, entry.respawnDelay) + Mathf.Max(0f, entry.respawnDelayVariation) * variation;
        entry.trafficMove = 0f;
        entry.actor.gameObject.SetActive(false);
    }

    private void UpdateRespawns(float dt)
    {
        foreach (Actor entry in actors)
        {
            if (entry == null || !entry.respawning || entry.actor == null) continue;
            entry.respawnRemaining = Mathf.Max(0f, entry.respawnRemaining - dt);
            if (entry.respawnRemaining > 0f) continue;
            bool clear = true;
            foreach (Actor other in actors)
            {
                if (other == null || other == entry || other.respawning || other.actor == null || !other.actor.gameObject.activeInHierarchy || !other.openRoute) continue;
                if (SameRoute(entry, other) && other.distance < RequiredSpacing(entry, other)) { clear = false; break; }
            }
            if (!clear) continue;
            entry.distance = 0f;
            entry.trafficMove = 0f;
            entry.respawning = false;
            entry.lateral = entry.lateralTarget = entry.heldFor = entry.clearFor = 0f;
            entry.offsetRight = entry.velocity = Vector3.zero;
            RebuildStops(entry);
            entry.actor.position = Sample(entry, 0f, out Vector3 direction);
            if (!entry.alignToSlope) direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f) entry.actor.rotation = Quaternion.LookRotation(direction, Vector3.up);
            entry.actor.gameObject.SetActive(true);
        }
    }

    private void SolveTraffic(float dt)
    {
        foreach (List<Actor> group in trafficGroups)
        {
            if (group[0].openRoute) group.Sort(CompareRoutePosition);
            foreach (Actor entry in group)
            {
                bool moving = !entry.respawning && entry.actor != null && entry.actor.gameObject.activeInHierarchy;
                entry.trafficMove = moving ? ProposeMovement(entry, dt, 0.25f) : 0f;
                // Someone on a crossing, or a café car turning across the lane: stop short of it.
                if (moving && roadBlocks.Count > 0) entry.trafficMove = Mathf.Min(entry.trafficMove, RoomBeforeRoadBlocks(entry, dt));
            }
            if (group.Count < 2) continue;
            if (group[0].openRoute)
            {
                Actor leader = null;
                for (int i = group.Count - 1; i >= 0; i--)
                {
                    Actor follower = group[i];
                    if (follower.respawning || follower.actor == null || !follower.actor.gameObject.activeInHierarchy) continue;
                    if (leader != null)
                    {
                        float room = leader.distance - follower.distance - RequiredSpacing(follower, leader);
                        follower.trafficMove = Mathf.Min(follower.trafficMove, Mathf.Max(0f, room + leader.trafficMove));
                        // Never end up stopped on a crossing or in a junction box behind a queue.
                        if (roadBlocks.Count > 0) follower.trafficMove = KeepClear(follower, leader, follower.trafficMove, dt);
                    }
                    leader = follower;
                }
                continue;
            }
            // All proposals use the same start-of-frame positions. N relaxation passes propagate a
            // stopped/slow leader around the whole ring without depending on the actor update order.
            for (int pass = 0; pass < group.Count; pass++)
                for (int i = 0; i < group.Count; i++)
                {
                    Actor follower = group[i], leader = group[(i + 1) % group.Count];
                    float room = Mathf.Repeat(leader.distance - follower.distance, follower.length) - RequiredSpacing(follower, leader);
                    follower.trafficMove = Mathf.Min(follower.trafficMove, Mathf.Max(0f, room + leader.trafficMove));
                }
        }
    }

    // ---------- crossings, turning cars and borrowed cars ----------

    // Where a block lies relative to a car: along its heading (from the car's centre)
    // and across it (positive to the car's right).
    private static void BlockExtent(RoadBlock block, Vector3 origin, Vector3 forward, Vector3 right,
                                    out float alongMin, out float alongMax, out float sideMin, out float sideMax)
    {
        Quaternion turn = Quaternion.Euler(0f, block.yaw, 0f);
        Vector3 bx = turn * Vector3.right * block.halfSize.x, bz = turn * Vector3.forward * block.halfSize.y;
        alongMin = sideMin = float.PositiveInfinity;
        alongMax = sideMax = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = block.center + ((i & 1) == 0 ? bx : -bx) + ((i & 2) == 0 ? bz : -bz) - origin;
            float a = c.x * forward.x + c.z * forward.z, s = c.x * right.x + c.z * right.z;
            alongMin = Mathf.Min(alongMin, a); alongMax = Mathf.Max(alongMax, a);
            sideMin = Mathf.Min(sideMin, s); sideMax = Mathf.Max(sideMax, s);
        }
    }

    private bool CarFrame(Actor car, out Vector3 here, out Vector3 forward, out Vector3 right)
    {
        here = Sample(car, car.distance, out forward);
        forward.y = 0f;
        right = Vector3.zero;
        if (forward.sqrMagnitude < 1e-8f) return false;
        forward.Normalize();
        right = new Vector3(forward.z, 0f, -forward.x);
        return true;
    }

    // How far this car may move before it would drive into an active block, braking
    // towards it rather than stopping dead.
    private float RoomBeforeRoadBlocks(Actor car, float dt)
    {
        if (!CarFrame(car, out Vector3 here, out Vector3 forward, out Vector3 right)) return float.PositiveInfinity;
        float half = Mathf.Max(0.1f, car.vehicleLength) * 0.5f;
        float room = float.PositiveInfinity;
        foreach (RoadBlock block in roadBlocks)
        {
            if (block == null || !block.active || Mathf.Abs(block.center.y - here.y) > 3f) continue;
            BlockExtent(block, here, forward, right, out float near, out float far, out float sideMin, out float sideMax);
            if (sideMax < -CarHalfWidth || sideMin > CarHalfWidth) continue;   // not in this lane
            if (far < -half) continue;                                         // already behind the car
            float gap = near - half;                                           // front bumper to the block
            if (gap < 0f)
            {
                // Already on it: a crossing is cleared, anything else waits.
                if (block.letTrafficInsideClear) continue;
                return 0f;
            }
            if (gap > roadBlockLookAhead) continue;
            room = Mathf.Min(room, Mathf.Max(0f, gap - roadBlockStopGap));
        }
        if (float.IsInfinity(room)) return room;
        float allowedSpeed = Mathf.Sqrt(2f * roadBlockBraking * room);
        return Mathf.Min(room, allowedSpeed * Mathf.Min(dt, 0.25f));
    }

    // Don't drive onto a keep-clear block (a crossing, a junction box) unless the
    // whole car fits beyond it, so a queue never ends on top of one.
    private float KeepClear(Actor car, Actor leader, float move, float dt)
    {
        if (leader == null || !CarFrame(car, out Vector3 here, out Vector3 forward, out Vector3 right)) return move;
        float half = Mathf.Max(0.1f, car.vehicleLength) * 0.5f;
        float leaderRear = leader.distance - car.distance - Mathf.Max(0.1f, leader.vehicleLength) * 0.5f;
        // A leader that is driving off will have made room by the time we get there.
        float leaderSpeed = dt > 1e-5f ? leader.trafficMove / dt : 0f;
        if (leaderSpeed > 1f) leaderRear += leaderSpeed * 1.5f;
        float gapBehindLeader = Mathf.Max(car.minimumGap, leader.minimumGap);
        foreach (RoadBlock block in roadBlocks)
        {
            if (block == null || !block.keepClear || Mathf.Abs(block.center.y - here.y) > 3f) continue;
            BlockExtent(block, here, forward, right, out float near, out float far, out float sideMin, out float sideMax);
            if (sideMax < -CarHalfWidth || sideMin > CarHalfWidth) continue;
            float gap = near - half;
            if (gap < -0.05f || gap > roadBlockLookAhead) continue;            // already on it, or far off
            if (far > leaderRear) continue;                                    // the leader itself is on or before it
            float roomBeyond = leaderRear - gapBehindLeader - far;
            if (roomBeyond >= car.vehicleLength + 0.2f) continue;              // fits after it
            float room = Mathf.Max(0f, gap - 0.3f);
            float allowedSpeed = Mathf.Sqrt(2f * roadBlockBraking * room);
            move = Mathf.Min(move, Mathf.Min(room, allowedSpeed * Mathf.Min(dt, 0.25f)));
        }
        return move;
    }

    /// <summary>True while any car's body overlaps the block grown by <paramref name="margin"/> metres.</summary>
    public bool AnyCarOn(RoadBlock block, float margin)
    {
        if (block == null) return false;
        foreach (Actor car in actors)
        {
            if (car == null || !car.trafficManaged || car.respawning || car.actor == null || !car.actor.gameObject.activeInHierarchy) continue;
            if (!CarFrame(car, out Vector3 here, out Vector3 forward, out Vector3 right)) continue;
            if (Mathf.Abs(block.center.y - here.y) > 3f) continue;
            BlockExtent(block, here, forward, right, out float near, out float far, out float sideMin, out float sideMax);
            float half = Mathf.Max(0.1f, car.vehicleLength) * 0.5f + margin;
            if (far < -half || near > half) continue;
            if (sideMax < -CarHalfWidth - margin || sideMin > CarHalfWidth + margin) continue;
            return true;
        }
        return false;
    }

    /// <summary>True when a moving car is too close to stop before the block.</summary>
    public bool CarClosingOn(RoadBlock block, float withinMetres)
    {
        if (block == null) return false;
        foreach (Actor car in actors)
        {
            if (car == null || !car.trafficManaged || car.respawning || car.actor == null || !car.actor.gameObject.activeInHierarchy) continue;
            if (car.lastSpeed < 0.3f) continue;
            if (!CarFrame(car, out Vector3 here, out Vector3 forward, out Vector3 right)) continue;
            if (Mathf.Abs(block.center.y - here.y) > 3f) continue;
            BlockExtent(block, here, forward, right, out float near, out float far, out float sideMin, out float sideMax);
            if (sideMax < -CarHalfWidth || sideMin > CarHalfWidth) continue;
            float gap = near - Mathf.Max(0.1f, car.vehicleLength) * 0.5f;
            if (gap >= -0.5f && gap <= withinMetres) return true;
        }
        return false;
    }

    /// <summary>
    /// Someone is waiting to cross alongside junction phase <paramref name="parallelPhase"/>
    /// (a café visitor at a signalled crossing - like pressing the button). Call every frame
    /// while waiting; the other phase's green then ends early, once it has run
    /// <see cref="walkRequestMinGreen"/> seconds.
    /// </summary>
    public void RequestWalk(int parallelPhase)
    {
        if (parallelPhase == 0 || parallelPhase == 1) walkRequests[parallelPhase] = true;
    }

    private void ServeWalkRequests()
    {
        bool forPhase0 = walkRequests[0], forPhase1 = walkRequests[1];
        walkRequests[0] = walkRequests[1] = false;
        if (!forPhase0 && !forPhase1) return;
        float green = Mathf.Max(1f, signalGreenSeconds), clearance = Mathf.Max(1f, signalClearanceSeconds);
        float halfCycle = green + clearance;
        float time = Mathf.Repeat(signalTime, halfCycle * 2f);
        float cycleStart = signalTime - time;
        float minGreen = Mathf.Min(walkRequestMinGreen, green);
        // Waiting to walk with phase 1 while phase 0 has its green: phase 0 goes to amber now.
        if (forPhase1 && time < green && time >= minGreen) signalTime = cycleStart + green;
        // ... and the other way round.
        else if (forPhase0 && time >= halfCycle && time < halfCycle + green && time - halfCycle >= minGreen) signalTime = cycleStart + halfCycle + green;
    }

    /// <summary>
    /// Seconds before the traffic that drives over a crossing gets its green again, while
    /// junction phase <paramref name="parallelPhase"/> (the traffic alongside the crossing)
    /// has its turn: its green, its amber and the all-reds either side, when the crossing
    /// traffic is held at its stop line. Someone may start across while they can reach the
    /// far kerb inside it (like a walk signal that turns to a flashing don't-walk in time).
    /// 0 when the crossing traffic has its green or amber.
    /// </summary>
    public float WalkTimeLeft(int parallelPhase)
    {
        float green = Mathf.Max(1f, signalGreenSeconds), clearance = Mathf.Max(1f, signalClearanceSeconds);
        float halfCycle = green + clearance, amber = Mathf.Min(2f, clearance);
        float time = Mathf.Repeat(signalTime, halfCycle * 2f);
        if (parallelPhase == 1)
        {
            // Phase 0 drives over the crossing: held from the end of its amber to the end of the cycle.
            return time >= green + amber ? halfCycle * 2f - time : 0f;
        }
        if (parallelPhase == 0)
        {
            // Phase 1 drives over it: held from the end of its amber, round the cycle, to half way.
            if (time >= halfCycle + green + amber) return halfCycle * 2f - time + halfCycle;
            return time < halfCycle ? halfCycle - time : 0f;
        }
        return 0f;
    }

    /// <summary>
    /// For someone about to walk from <paramref name="from"/> to <paramref name="to"/> over the
    /// road: true while a car's body is on that walk (grown by <paramref name="halfWidth"/>),
    /// or a moving car could not stop before reaching it. A car standing at its stop line
    /// beside a crossing is not in the way.
    /// </summary>
    public bool CarThreatens(Vector3 from, Vector3 to, float halfWidth)
    {
        foreach (Actor car in actors)
        {
            if (car == null || !car.trafficManaged || car.respawning || car.actor == null || !car.actor.gameObject.activeInHierarchy) continue;
            if (!CarFrame(car, out Vector3 here, out Vector3 forward, out Vector3 right)) continue;
            if (Mathf.Abs(here.y - from.y) > 3f) continue;
            float half = Mathf.Max(0.1f, car.vehicleLength) * 0.5f;
            // How far it could still roll before stopping (it stops short of a crossing in use).
            float roll = car.lastSpeed >= 0.3f ? car.lastSpeed * car.lastSpeed / (2f * roadBlockBraking) + roadBlockStopGap : 0f;
            Vector3 centre = here + forward * (roll * 0.5f);
            if (SegmentTouchesBox(from, to, halfWidth, centre, forward, right, half + roll * 0.5f, CarHalfWidth)) return true;
        }
        return false;
    }

    // The segment a-b, grown by `grow`, against a box on the ground (slab test in the box's frame).
    private static bool SegmentTouchesBox(Vector3 a, Vector3 b, float grow, Vector3 centre, Vector3 forward, Vector3 right,
                                          float halfLength, float halfWidth)
    {
        Vector3 pa = a - centre, pb = b - centre;
        float ax = pa.x * forward.x + pa.z * forward.z, az = pa.x * right.x + pa.z * right.z;
        float bx = pb.x * forward.x + pb.z * forward.z, bz = pb.x * right.x + pb.z * right.z;
        float t0 = 0f, t1 = 1f;
        return ClipSlab(ax, bx - ax, halfLength + grow, ref t0, ref t1) && ClipSlab(az, bz - az, halfWidth + grow, ref t0, ref t1);
    }

    private static bool ClipSlab(float p, float d, float half, ref float t0, ref float t1)
    {
        if (Mathf.Abs(d) < 1e-6f) return p >= -half && p <= half;
        float a = (-half - p) / d, b = (half - p) / d;
        if (a > b) { float swap = a; a = b; b = swap; }
        if (a > t0) t0 = a;
        if (b < t1) t1 = b;
        return t0 <= t1;
    }

    /// <summary>Seconds of green left for junction phase 0 or 1; 0 when that phase isn't green.</summary>
    public float GreenRemaining(int phase)
    {
        float green = Mathf.Max(1f, signalGreenSeconds), halfCycle = green + Mathf.Max(1f, signalClearanceSeconds);
        float time = Mathf.Repeat(signalTime, halfCycle * 2f);
        if (phase == 0) return time < green ? green - time : 0f;
        if (phase == 1) return time >= halfCycle && time < halfCycle + green ? halfCycle + green - time : 0f;
        return 0f;
    }

    private List<Actor> FindGroup(string group)
    {
        foreach (List<Actor> lane in trafficGroups)
            if (lane.Count > 0 && lane[0].trafficGroup == group) return lane;
        return null;
    }

    /// <summary>Where <paramref name="position"/> lies along a traffic group's route, metres from its start.</summary>
    public bool TryRouteDistance(string group, Vector3 position, out float distance)
    {
        distance = 0f;
        List<Actor> lane = FindGroup(group);
        if (lane == null) return false;
        Actor template = lane[0];
        if (template.points == null || template.points.Length < 2) return false;
        float nearest = float.PositiveInfinity;
        for (int segment = 1; segment < template.points.Length; segment++)
        {
            Vector3 start = template.points[segment - 1], edge = template.points[segment] - start;
            float t = edge.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(position - start, edge) / edge.sqrMagnitude) : 0f;
            float separation = (position - (start + edge * t)).sqrMagnitude;
            if (separation >= nearest) continue;
            nearest = separation;
            distance = template.lengths[segment - 1] + (template.lengths[segment] - template.lengths[segment - 1]) * t;
        }
        return true;
    }

    /// <summary>The route position and heading at <paramref name="distance"/> along a traffic group.</summary>
    public bool TryRoutePoint(string group, float distance, out Vector3 point, out Vector3 direction)
    {
        point = direction = Vector3.zero;
        List<Actor> lane = FindGroup(group);
        if (lane == null || lane[0].points == null || lane[0].points.Length < 2) return false;
        point = Sample(lane[0], Mathf.Clamp(distance, 0f, lane[0].length), out direction);
        return true;
    }

    /// <summary>Junction phase a traffic group obeys (-1 when unsignalled or unknown).</summary>
    public int SignalPhaseOf(string group)
    {
        List<Actor> lane = FindGroup(group);
        return lane != null ? lane[0].junctionSignalPhase : -1;
    }

    /// <summary>
    /// Whether a car of <paramref name="length"/> could join the lane at <paramref name="distance"/>
    /// right now without crowding anyone: every car keeps its bumper gap, and one coming up
    /// behind has another <paramref name="approachSeconds"/> of driving in hand.
    /// </summary>
    public bool CanJoinTraffic(string group, float distance, float length, float approachSeconds = 1.5f)
    {
        List<Actor> lane = FindGroup(group);
        if (lane == null) return false;
        foreach (Actor other in lane)
        {
            if (other.respawning || other.actor == null || !other.actor.gameObject.activeInHierarchy) continue;
            float spacing = (Mathf.Max(0.1f, length) + Mathf.Max(0.1f, other.vehicleLength)) * 0.5f
                          + Mathf.Max(0f, Mathf.Max(lane[0].minimumGap, other.minimumGap)) + 0.01f;
            float ahead = other.distance - distance;
            if (ahead >= 0f ? ahead < spacing : -ahead < spacing + Mathf.Max(0f, other.speed) * approachSeconds) return false;
        }
        return true;
    }

    /// <summary>
    /// Adds a car to a traffic lane at runtime (a café customer driving in or out). It
    /// obeys the lane's signals and spacing like any other car, and when it reaches the
    /// end of the route it is handed back through <paramref name="reachedEnd"/> instead
    /// of respawning. Call <see cref="LeaveTraffic"/> to take it out earlier.
    /// </summary>
    public Actor JoinTraffic(string group, Transform car, float length, float speed, Transform[] wheels, float wheelRadius,
                             Vector3 wheelAxis, float distance, Action<Actor> reachedEnd)
    {
        List<Actor> lane = FindGroup(group);
        if (lane == null || car == null) return null;
        Actor template = lane[0];
        var entry = new Actor
        {
            actor = car, waypoints = template.waypoints, openRoute = template.openRoute, speed = Mathf.Max(0.5f, speed),
            trafficGroup = template.trafficGroup, vehicleLength = Mathf.Max(0.5f, length), minimumGap = template.minimumGap,
            stopWaypoints = template.stopWaypoints, stopDuration = template.stopDuration,
            junctionSignalPhase = template.junctionSignalPhase, smoothRoute = template.smoothRoute,
            alignToSlope = template.alignToSlope, turnSpeed = template.turnSpeed,
            wheels = wheels ?? Array.Empty<Transform>(), wheelRadius = Mathf.Max(0.05f, wheelRadius), wheelAxis = wheelAxis,
        };
        entry.points = template.points;
        entry.lengths = template.lengths;
        entry.length = template.length;
        entry.distance = entry.openRoute ? Mathf.Clamp(distance, 0f, entry.length) : Mathf.Repeat(distance, entry.length);
        entry.wheelRest = RestRotations(entry.wheels);
        entry.wingRest = RestRotations(entry.wings);
        entry.legRest = RestRotations(entry.legs);
        entry.trafficManaged = true;
        entry.guest = true;
        entry.guestReachedEnd = reachedEnd;
        entry.routeOrder = nextGuestOrder++;
        RebuildStops(entry);
        car.position = Sample(entry, entry.distance, out Vector3 direction);
        if (!entry.alignToSlope) direction.y = 0f;
        if (direction.sqrMagnitude > 1e-6f) car.rotation = Quaternion.LookRotation(direction, Vector3.up);
        actors.Add(entry);
        lane.Add(entry);
        if (entry.openRoute) lane.Sort(CompareRoutePosition);
        return entry;
    }

    /// <summary>Takes a car added with <see cref="JoinTraffic"/> back out of its lane; it stays where it is.</summary>
    public void LeaveTraffic(Actor guest)
    {
        if (guest == null || !guest.guest) return;
        RemoveFromTraffic(guest);
    }

    private void RemoveFromTraffic(Actor guest)
    {
        actors.Remove(guest);
        foreach (List<Actor> lane in trafficGroups) lane.Remove(guest);
        guest.trafficMove = 0f;
        guest.lastSpeed = 0f;
    }

    /// <summary>
    /// For people walking outside the NavMesh (café visitors): the nearest body ahead of
    /// <paramref name="position"/> inside a corridor along <paramref name="heading"/> -
    /// street walkers, other registered bodies, café NavMesh agents and the player.
    /// </summary>
    public static bool NearestBodyAhead(Vector3 position, Vector3 heading, float lookAhead, float corridor, IStreetBody self,
                                        out Vector3 bodyPosition, out Vector3 bodyVelocity, out float bodyRadius)
    {
        bodyPosition = bodyVelocity = Vector3.zero;
        bodyRadius = 0f;
        heading.y = 0f;
        if (heading.sqrMagnitude < 1e-8f) return false;
        heading.Normalize();
        Vector3 right = new Vector3(heading.z, 0f, -heading.x);
        float best = float.PositiveInfinity;
        bool found = false;
        // A local function can't write out parameters: collect here, copy out at the end.
        Vector3 nearestPosition = Vector3.zero, nearestVelocity = Vector3.zero;
        float nearestRadius = 0f;

        void Consider(Vector3 p, Vector3 v, float r)
        {
            Vector3 offset = p - position;
            if (Mathf.Abs(offset.y) > 1.6f) return;
            offset.y = 0f;
            float along = Vector3.Dot(offset, heading);
            if (along <= 0.05f || along > lookAhead + r || along >= best) return;
            if (Mathf.Abs(Vector3.Dot(offset, right)) > corridor + r) return;
            best = along; nearestPosition = p; nearestVelocity = v; nearestRadius = r; found = true;
        }

        foreach (StreetLife street in instances)
            foreach (Actor walker in street.actors)
                if (walker != null && walker.isWalker && walker.actor != null && walker.actor.gameObject.activeInHierarchy)
                    Consider(walker.actor.position, walker.velocity, 0.3f);
        foreach (IStreetBody body in streetBodies)
            if (body != null && !ReferenceEquals(body, self))
                Consider(body.Position, body.Velocity, Mathf.Max(0.1f, body.Radius));
        if (Time.unscaledTime >= nextCacheScan)
        {
            nextCacheScan = Time.unscaledTime + 0.5f;
            cachedAgents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude);
            cachedCharacters = FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude);
        }
        foreach (NavMeshAgent agent in cachedAgents)
            if (agent != null && agent.isActiveAndEnabled) Consider(agent.transform.position, agent.velocity, 0.3f);
        foreach (CharacterController character in cachedCharacters)
            if (character != null && character.enabled) Consider(character.transform.position, character.velocity, 0.3f);
        bodyPosition = nearestPosition;
        bodyVelocity = nearestVelocity;
        bodyRadius = nearestRadius;
        return found;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this)) instances.Add(this);
    }

    private static void RebuildStops(Actor entry)
    {
        entry.stopRemaining = 0f;
        entry.waitingForSignal = false;
        entry.nextStop = -1;
        entry.distanceToStop = float.PositiveInfinity;
        if ((entry.junctionSignalPhase < 0 && entry.stopDuration <= 0f) || entry.stopWaypoints == null || entry.stopWaypoints.Length == 0) return;
        var stops = new List<float>();
        foreach (Transform marker in entry.stopWaypoints)
        {
            if (marker == null) continue;
            float nearest = float.PositiveInfinity, distance = 0f;
            for (int segment = 1; segment < entry.points.Length; segment++)
            {
                Vector3 start = entry.points[segment - 1], edge = entry.points[segment] - start;
                float t = edge.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(marker.position - start, edge) / edge.sqrMagnitude) : 0f;
                float separation = (marker.position - (start + edge * t)).sqrMagnitude;
                if (separation >= nearest) continue;
                nearest = separation;
                distance = entry.lengths[segment - 1] + (entry.lengths[segment] - entry.lengths[segment - 1]) * t;
            }
            distance = entry.openRoute ? Mathf.Clamp(distance, 0f, entry.length) : Mathf.Repeat(distance, entry.length);
            bool duplicate = false;
            foreach (float existing in stops)
            {
                float separation = Mathf.Abs(existing - distance);
                if ((entry.openRoute ? separation : Mathf.Min(separation, entry.length - separation)) < 0.01f) { duplicate = true; break; }
            }
            if (!duplicate) stops.Add(distance);
        }
        stops.Sort();
        entry.stopDistances = stops.ToArray();
        if (stops.Count == 0) return;
        entry.nextStop = stops.FindIndex(distance => distance >= entry.distance);
        if (entry.nextStop < 0) { if (entry.openRoute) return; entry.nextStop = 0; }
        entry.distanceToStop = entry.openRoute ? stops[entry.nextStop] - entry.distance : Mathf.Repeat(stops[entry.nextStop] - entry.distance, entry.length);
    }

    private float ProposeMovement(Actor entry, float dt, float maximumMovementTime)
    {
        float movementTime = dt;
        if (entry.stopRemaining > 0f)
        {
            movementTime = Mathf.Max(0f, dt - entry.stopRemaining);
            entry.stopRemaining = Mathf.Max(0f, entry.stopRemaining - dt);
        }
        if (entry.waitingForSignal)
        {
            if (CurrentGreenSignalPhase != entry.junctionSignalPhase) return 0f;
        }
        float move = Mathf.Max(0f, entry.speed) * Mathf.Min(movementTime, maximumMovementTime);
        if (entry.openRoute) move = Mathf.Min(move, entry.length - entry.distance);
        return entry.nextStop >= 0 ? Mathf.Min(move, entry.distanceToStop) : move;
    }

    private void RecordStopProgress(Actor entry, float moved)
    {
        // A green signal grants departure only when spacing allows actual movement.
        // A car still queued at the line must recheck the signal on the following frame.
        if (entry.waitingForSignal && moved > 0f && CurrentGreenSignalPhase == entry.junctionSignalPhase) entry.waitingForSignal = false;
        if (entry.nextStop < 0) return;
        entry.distanceToStop = Mathf.Max(0f, entry.distanceToStop - moved);
        if (entry.distanceToStop > 0f) return;

        // Commit a stop only after spacing has resolved the actual movement. A car queued behind
        // a leader must still stop when it eventually reaches the line itself.
        entry.stopRemaining = entry.junctionSignalPhase >= 0 ? 0f : Mathf.Max(0f, entry.stopDuration);
        entry.waitingForSignal = entry.junctionSignalPhase >= 0;
        float stoppedAt = entry.stopDistances[entry.nextStop];
        if (entry.openRoute && entry.nextStop + 1 >= entry.stopDistances.Length)
        { entry.nextStop = -1; entry.distanceToStop = float.PositiveInfinity; return; }
        entry.nextStop = (entry.nextStop + 1) % entry.stopDistances.Length;
        entry.distanceToStop = entry.openRoute ? entry.stopDistances[entry.nextStop] - stoppedAt : Mathf.Repeat(entry.stopDistances[entry.nextStop] - stoppedAt, entry.length);
        if (entry.distanceToStop < 0.01f) entry.distanceToStop = entry.length;
    }

    private void RefreshSignalHeads()
    {
        TrafficSignalState state = CurrentSignalState;
        if (state == displayedSignalState) return;
        displayedSignalState = state;
        if (signalHeads == null) return;
        foreach (SignalHead head in signalHeads)
        {
            if (head == null) continue;
            if (head.propertyBlock == null) head.propertyBlock = new MaterialPropertyBlock();
            bool green = head.signalGroup == CurrentGreenSignalPhase;
            bool amber = head.signalGroup == 0 ? state == TrafficSignalState.Phase0Amber : state == TrafficSignalState.Phase1Amber;
            SetSignalBulb(head.red, !green && !amber, new Color(1f, 0.055f, 0.025f), head.propertyBlock);
            SetSignalBulb(head.amber, amber, new Color(1f, 0.57f, 0.025f), head.propertyBlock);
            SetSignalBulb(head.green, green, new Color(0.12f, 1f, 0.27f), head.propertyBlock);
        }
    }

    private static void SetSignalBulb(Renderer renderer, bool illuminated, Color color, MaterialPropertyBlock block)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block);
        Color surface = color * (illuminated ? 1f : 0.09f);
        surface.a = 1f;
        block.SetColor(BaseColor, surface);
        block.SetColor(EmissionColor, illuminated ? color * 2.2f : Color.black);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>Checks the configured bumper gaps without allocating during normal frame updates.</summary>
    public bool ValidateTrafficSpacing(out string diagnostic)
    {
        if (!string.IsNullOrEmpty(trafficSetupError)) { diagnostic = trafficSetupError; return false; }
        foreach (List<Actor> group in trafficGroups)
        {
            if (group[0].openRoute)
            {
                Actor follower = null;
                foreach (Actor leader in group)
                {
                    if (leader.respawning || leader.actor == null || !leader.actor.gameObject.activeInHierarchy) continue;
                    if (follower != null && leader.distance - follower.distance < RequiredSpacing(follower, leader) - 0.001f)
                    { diagnostic = follower.trafficGroup + ": open-lane gap below the configured minimum."; return false; }
                    follower = leader;
                }
                continue;
            }
            for (int i = 0; i < group.Count && group.Count > 1; i++)
            {
                Actor follower = group[i], leader = group[(i + 1) % group.Count];
                float room = Mathf.Repeat(leader.distance - follower.distance, follower.length) - RequiredSpacing(follower, leader);
                if (room < -0.001f)
                {
                    diagnostic = follower.trafficGroup + ": gap below the configured minimum at " + (follower.actor != null ? follower.actor.name : "a removed actor") + ".";
                    return false;
                }
            }
        }
        diagnostic = "All traffic groups retain their configured bumper gaps.";
        return true;
    }

    private static Vector3 Sample(Actor entry, float distance, out Vector3 direction)
    {
        int segment = 1;
        while (segment < entry.lengths.Length - 1 && entry.lengths[segment] <= distance) segment++;
        direction = entry.points[segment] - entry.points[segment - 1];
        float span = entry.lengths[segment] - entry.lengths[segment - 1];
        float t = span > 0.000001f ? (distance - entry.lengths[segment - 1]) / span : 0f;
        return Vector3.Lerp(entry.points[segment - 1], entry.points[segment], t);
    }

    private static Quaternion[] RestRotations(Transform[] joints)
    {
        var rotations = new Quaternion[joints == null ? 0 : joints.Length];
        for (int i = 0; i < rotations.Length; i++)
            rotations[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
        return rotations;
    }

    private static void Pose(Transform[] joints, Quaternion[] rest, Vector3 axis, float angle, bool alternate)
    {
        if (joints == null || rest == null) return;
        for (int i = 0; i < joints.Length && i < rest.Length; i++)
            if (joints[i] != null)
                joints[i].localRotation = rest[i] * Quaternion.AngleAxis(angle * (alternate && i % 2 != 0 ? -1f : 1f), axis);
    }

    private void OnDisable()
    {
        instances.Remove(this);
        foreach (Actor entry in actors)
            if (entry != null && entry.drivesAnimator && entry.animator != null)
                entry.animator.SetBool(IsWalking, false);
    }
}
