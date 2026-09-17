using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Decorative street motion on authored routes; never joins café gameplay.</summary>
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
    }

    public List<Actor> actors = new List<Actor>();
    public List<SignalHead> signalHeads = new List<SignalHead>();
    [Min(1f)] public float signalGreenSeconds = 14f;
    [Tooltip("All approaches hold while cars already inside a junction clear it. Author speeds and stop-line positions to clear within this interval.")]
    [Min(1f)] public float signalClearanceSeconds = 5f;
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private readonly List<List<Actor>> trafficGroups = new List<List<Actor>>();
    private string trafficSetupError;
    private float signalTime;
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
        foreach (Actor entry in actors)
        {
            if (entry == null || entry.actor == null || entry.length < 0.001f || !entry.actor.gameObject.activeInHierarchy)
                continue;
            Vector3 before = entry.actor.position;
            float advance = entry.trafficManaged ? entry.trafficMove : ProposeMovement(entry, dt, dt);
            entry.distance = entry.openRoute ? Mathf.Min(entry.distance + advance, entry.length) : Mathf.Repeat(entry.distance + advance, entry.length);
            RecordStopProgress(entry, advance);
            entry.actor.position = Sample(entry, entry.distance, out Vector3 direction);
            float moved = entry.trafficManaged ? advance : Vector3.Distance(before, entry.actor.position);
            bool walking = moved / dt > 0.01f;
            if (entry.drivesAnimator && entry.animator != null)
                entry.animator.SetBool(IsWalking, walking);
            if (entry.openRoute && entry.distance >= entry.length) BeginRespawn(entry);
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
        // Gate time advances by the same bounded step as traffic, so a hitch cannot skip
        // clearance while cars inside an intersection have barely moved.
        signalTime = Mathf.Repeat(signalTime + Mathf.Min(dt, 0.25f), 2f * (Mathf.Max(1f, signalGreenSeconds) + Mathf.Max(1f, signalClearanceSeconds)));
        RefreshSignalHeads();
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
                entry.trafficMove = !entry.respawning && entry.actor != null && entry.actor.gameObject.activeInHierarchy ?
                    ProposeMovement(entry, dt, 0.25f) : 0f;
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
        foreach (Actor entry in actors)
            if (entry != null && entry.drivesAnimator && entry.animator != null)
                entry.animator.SetBool(IsWalking, false);
    }
}
