using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walks a café NPC along a fixed path outside the café: from a parked car or a
/// neighbour's front door to the café door (arriving), or back again (leaving).
/// CafeArrivals starts it. While it runs, the NPC's brain and navigation are off,
/// so everything the brain does inside the café is exactly as before - it simply
/// starts at the door instead of appearing there.
///
/// Outside there is no navigation mesh, so the path is walked by hand:
///  * everyone keeps to their own line along the path (a little left or right of it,
///    never further than the path data says there is room - planters, posts, parked
///    cars and the road stay out of reach);
///  * nobody walks into anybody: each frame the walker tries a fan of directions and
///    speeds and takes the one that keeps closest to where it wants to go without
///    touching anyone in the next second and a half - other visitors, street walkers,
///    café NPCs at the door, the player, and cars (as boxes, parked ones too). That
///    gives following at a gap, stepping round someone standing, passing oncoming
///    people (keeping right), and squeezing past only as a last resort;
///  * at a crossing people gather at the kerb in a loose group, each on their own spot
///    along it, and wait for the crossing to say go (the walk signal at junctions, a
///    safe gap at the café's zebra); then they cross side by side;
///  * the walking animation plays while moving and idles while waiting.
/// Street walkers and cars see these walkers too (StreetLife bodies), and café NPCs'
/// navigation steers round them (a non-carving NavMeshObstacle while they walk).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)] // positions are current before StreetLife solves the street
public sealed class NpcJourney : MonoBehaviour, StreetLife.IStreetBody
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly List<NpcJourney> active = new List<NpcJourney>();

    private const float BodyRadius = 0.28f;
    private const float Personal = 0.07f;        // room kept between two bodies, metres
    private const float Horizon = 1.5f;          // seconds ahead a walker looks for collisions
    private const float PeopleRange = 4.5f;      // people further than this don't matter yet
    private const float CarRange = 10f;
    private const float SpeedUp = 3.5f, SlowDown = 7f; // m/s²: easing into a new velocity
    private const float KerbZone = 2.6f;         // metres before a kerb where people gather to wait
    private const float DefaultRoom = 0.4f;      // sideways room where the path data doesn't say
    private const float CrossingSpread = 1.8f;   // over the road people spread across the crossing
    private const float HelpAfter = 14f;         // seconds with no progress before a walk is helped on
    private const float SqueezeAfter = 5f;       // ... before squeezing past people who won't move

    private Vector3[] points = Array.Empty<Vector3>();
    private StreetCrossing[] crossings = Array.Empty<StreetCrossing>();
    private float[] roomLeft = Array.Empty<float>(), roomRight = Array.Empty<float>();
    private int next;
    private float speed = 1.3f;
    private float preference;        // this person's own line: metres right (+) or left (-) of the path
    private float lateral;           // the line actually aimed at (eases towards the wanted one)
    private float crossLane;         // their line over the road, fixed while crossing
    private Action whenDone;
    private Animator animator;
    private bool hasWalkingParameter;
    private bool walkingShown;
    private float walkingChange;
    private StreetCrossing onCrossing, waitingAt;
    private Vector3 velocity;
    private float bestRemaining = float.PositiveInfinity;
    private float lastProgressAt, startedAt, nextLandingCheck;
    private NavMeshObstacle obstacle;
    private bool waiting;
    private string waitingFor = "";
    private float givingWaySince = -1f;

    /// <summary>True while walking in to the café (not yet started inside), false while leaving.</summary>
    public bool Arriving { get; private set; }
    public CafeArrivals.Kind Kind { get; private set; }
    /// <summary>Seconds spent waiting at kerbs so far (for checks).</summary>
    public float WaitedAtKerbs { get; private set; }
    /// <summary>Times this walk had to be helped past something that would not clear (for checks).</summary>
    public int Unstuck { get; private set; }
    public float Age => Time.time - startedAt;
    public StreetCrossing CurrentCrossing => onCrossing;
    /// <summary>The crossing this walker is waiting at the kerb of, or null.</summary>
    public StreetCrossing KerbCrossing => waiting ? waitingAt : null;
    public int PointsLeft => Mathf.Max(0, points.Length - next);
    public int NextPoint => next;
    public int PointCount => points.Length;
    public Vector3 NextTarget => next < points.Length ? points[next] : transform.position;
    /// <summary>Seconds since the walk last got any closer to where it is going (0 while waiting at a kerb).</summary>
    public float StuckFor => waiting ? 0f : Time.time - lastProgressAt;
    /// <summary>Who or what is holding the walker back this frame (for checks), or "".</summary>
    public string HeldBy { get; private set; } = "";
    /// <summary>This person's own line, metres right (+) or left (-) of the path.</summary>
    public float Preference => preference;
    /// <summary>Standing still on purpose: at a kerb, or letting someone out of a narrow bit first.</summary>
    public bool Waiting => waiting;
    /// <summary>What they are waiting for while <see cref="Waiting"/> (for checks).</summary>
    public string WaitingFor => waiting ? waitingFor : "";

    public Vector3 Position => transform.position;
    public Vector3 Velocity => velocity;
    public float Radius => BodyRadius;

    public static IReadOnlyList<NpcJourney> Active => active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        bodies.Clear();
        near.Clear();
        bodiesFrame = -1;
        agents = Array.Empty<NavMeshAgent>();
        nextAgentScan = 0f;
    }

    /// <summary>Walkers of a kind still on their way in (the spawners keep room for them).</summary>
    public static int OnTheWay(CafeArrivals.Kind kind)
    {
        int count = 0;
        foreach (NpcJourney j in active)
            if (j != null && j.Arriving && j.Kind == kind) count++;
        return count;
    }

    /// <summary>Starts (or replaces) the walk, with the default room either side of every segment.</summary>
    public void Begin(Vector3[] path, StreetCrossing[] segmentCrossings, float walkSpeed,
                      bool arriving, CafeArrivals.Kind kind, Action done) =>
        Begin(path, segmentCrossings, null, null, walkSpeed, arriving, kind, done);

    /// <summary>
    /// Starts (or replaces) the walk. For each segment i (points[i] -> points[i + 1]):
    /// <paramref name="segmentCrossings"/>[i] is the crossing it lies on, or null, and
    /// <paramref name="left"/>/<paramref name="right"/>[i] how far a walker may stray
    /// from it to either side (metres; missing = a default).
    /// </summary>
    public void Begin(Vector3[] path, StreetCrossing[] segmentCrossings, float[] left, float[] right,
                      float walkSpeed, bool arriving, CafeArrivals.Kind kind, Action done)
    {
        ReleaseCrossings();
        points = path ?? Array.Empty<Vector3>();
        int segments = Mathf.Max(0, points.Length - 1);
        crossings = segmentCrossings != null && segmentCrossings.Length >= segments ? segmentCrossings : new StreetCrossing[segments];
        roomLeft = left != null && left.Length >= segments ? left : Filled(segments, DefaultRoom);
        roomRight = right != null && right.Length >= segments ? right : Filled(segments, DefaultRoom);
        speed = Mathf.Max(.4f, walkSpeed);
        Arriving = arriving;
        Kind = kind;
        whenDone = done;
        next = points.Length > 1 ? 1 : points.Length;
        // Everyone walks their own line: a little left or right of the path, kept for the whole walk.
        preference = UnityEngine.Random.Range(-0.34f, 0.34f);
        lateral = 0f;
        crossLane = 0f;
        waiting = false;
        waitingFor = "";
        givingWaySince = -1f;
        HeldBy = "";
        bestRemaining = float.PositiveInfinity;
        lastProgressAt = startedAt = Time.time;
        nextLandingCheck = 0f;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            hasWalkingParameter = false;
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (AnimatorControllerParameter p in animator.parameters)
                    if (p.nameHash == IsWalkingHash && p.type == AnimatorControllerParameterType.Bool) hasWalkingParameter = true;
        }
        if (points.Length > 0) transform.position = points[0];
        if (points.Length > 1) Face(points[1] - points[0], 1f);
        velocity = Vector3.zero;
        enabled = true;
        ShowToNavigation(true);
    }

    /// <summary>
    /// Turns round where it stands and walks back the way it came (the café closed
    /// before they got there). Someone half way over a crossing goes back to the kerb
    /// they came from, still covered by the crossing.
    /// </summary>
    public void TurnBack(bool arriving, Action done)
    {
        if (next <= 0 || points.Length == 0) { Begin(new[] { transform.position }, null, speed, arriving, Kind, done); return; }
        int last = Mathf.Min(next - 1, points.Length - 1);
        var path = new List<Vector3> { transform.position };
        var cross = new List<StreetCrossing>();
        var left = new List<float>();
        var right = new List<float>();
        for (int i = last; i >= 0; i--)
        {
            path.Add(points[i]);
            // Going back from here to points[last] retraces part of segment `last` - a
            // crossing only if they are actually on it. After that, walking
            // points[i + 1] -> points[i] reverses segment i (its left is now its right).
            cross.Add(i == last
                ? (onCrossing != null && last < crossings.Length && crossings[last] == onCrossing ? onCrossing : null)
                : crossings[i]);
            left.Add(i < roomRight.Length ? roomRight[i] : DefaultRoom);
            right.Add(i < roomLeft.Length ? roomLeft[i] : DefaultRoom);
        }
        StreetCrossing keep = cross.Count > 0 ? cross[0] : null;
        float lane = -crossLane;
        Begin(path.ToArray(), cross.ToArray(), left.ToArray(), right.ToArray(), speed, arriving, Kind, done); // releases every crossing
        if (keep != null) { keep.Enter(this); onCrossing = keep; crossLane = lane; } // still on the road: stay covered
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        StreetLife.RegisterBody(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        StreetLife.UnregisterBody(this);
        ReleaseCrossings();
        velocity = Vector3.zero;
        ShowToNavigation(false);
    }

    private void ReleaseCrossings()
    {
        onCrossing?.Forget(this);
        waitingAt?.Forget(this);
        onCrossing = waitingAt = null;
        waiting = false;
    }

    // While walking, café NPCs on the NavMesh (at the door) steer round this body.
    // Never at the same time as its own NavMeshAgent.
    private void ShowToNavigation(bool show)
    {
        if (show)
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) return;
            if (obstacle == null)
            {
                obstacle = GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
                Vector3 scale = transform.lossyScale;
                float across = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
                float up = Mathf.Max(0.01f, Mathf.Abs(scale.y));
                obstacle.carving = false;
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = BodyRadius / across;
                obstacle.height = 1.8f / up;
                obstacle.center = new Vector3(0f, 0.9f / up, 0f);
            }
            obstacle.enabled = true;
        }
        else if (obstacle != null) obstacle.enabled = false;
    }

    // ------------------------------------------------------------------ frame

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (next >= points.Length) { Finish(); return; }
        GatherBodies();

        Vector3 here = transform.position;
        bool arrive = next == points.Length - 1;   // the last point: arrive there, don't sweep past
        if (arrive && Arriving) CheckLanding(here);
        // Going home or back to the car: someone standing right on the doorstep (or the car
        // door) doesn't stop them - they are through the door, or in the car, already.
        if (arrive && !Arriving && Flat(points[next] - here).sqrMagnitude < 0.75f * 0.75f
            && !FreeOfBodies(points[next], BodyRadius * 2f + Personal)) { Finish(); return; }

        int seg = next - 1;
        Vector3 from = points[seg], to = points[next];
        StreetCrossing segCrossing = seg < crossings.Length ? crossings[seg] : null;
        Vector3 segment = Flat(to - from);
        float segLength = segment.magnitude;
        Vector3 dir = segLength > 1e-4f ? segment / segLength : FlatForward();
        Vector3 right = new Vector3(dir.z, 0f, -dir.x);

        // ---- crossings: gather at the kerb and wait for the crossing to say go ----
        waiting = false;
        StreetCrossing kerbCrossing = null;
        int kerbIndex = -1;
        if (segCrossing != null && onCrossing != segCrossing) { kerbCrossing = segCrossing; kerbIndex = seg; }
        else if (onCrossing == null && next < crossings.Length && crossings[next] != null
                 && Flat(to - here).sqrMagnitude < KerbZone * KerbZone) { kerbCrossing = crossings[next]; kerbIndex = next; }

        Band(seg, out float allowLeft, out float allowRight);
        float wantedLateral = Mathf.Clamp(preference, -allowLeft, allowRight);

        if (kerbCrossing != null)
        {
            if (waitingAt != kerbCrossing)
            {
                waitingAt?.StopWaiting(this);
                waitingAt = kerbCrossing;
                kerbCrossing.StartWaiting(this);
            }
            Vector3 kerb = points[kerbIndex];
            int far = kerbIndex;
            while (far < crossings.Length && crossings[far] == kerbCrossing) far++;
            Vector3 across = Flat(points[Mathf.Min(kerbIndex + 1, points.Length - 1)] - kerb);
            across = across.sqrMagnitude > 1e-6f ? across.normalized : dir;
            Vector3 acrossRight = new Vector3(across.z, 0f, -across.x);
            // Room at the kerb: the crossing's first segment was measured with the metre of
            // pavement behind the kerb, where people stand to wait.
            Band(kerbIndex, out float kerbLeft, out float kerbRight);
            float lane = Mathf.Clamp(preference * CrossingSpread, -kerbLeft, kerbRight);
            Vector3 spot = kerb + acrossRight * lane;
            Vector3 farSpot = points[far] + acrossRight * lane;
            float seconds = (Flat(farSpot - here).magnitude + 0.4f) / speed;
            if (kerbCrossing.CanStep(spot, farSpot, seconds))
            {
                kerbCrossing.Enter(this);
                waitingAt = null;
                onCrossing = kerbCrossing;
                crossLane = lane;
            }
            else
            {
                // Wait on their own spot along the kerb. Whoever got there first stands at
                // the front; the rest gather beside and behind, not in a queue.
                waiting = true;
                waitingFor = kerbCrossing.Name;
                WaitedAtKerbs += dt;
                lastProgressAt = Time.time;   // waiting for traffic is not being stuck
                Steer(here, spot, true, from, to, kerb, acrossRight, kerbLeft, kerbRight, dt, across);
                return;
            }
        }

        // Over the road (and on the last metres up to it, once stepped on) everyone keeps
        // the line they chose at the kerb.
        bool approachingOwn = onCrossing != null && segCrossing == null && next < crossings.Length && crossings[next] == onCrossing;
        if (onCrossing != null && (segCrossing == onCrossing || approachingOwn))
        {
            wantedLateral = crossLane;
            allowLeft = Mathf.Max(allowLeft, -crossLane);
            allowRight = Mathf.Max(allowRight, crossLane);
        }
        // A narrow bit next (a front doorway, the gap between two parked cars) with someone
        // coming out of it: stand aside short of it and let them out first.
        if (onCrossing == null && GivingWay(here, to))
        {
            waiting = true;
            waitingFor = "someone coming out of a narrow bit";
            lastProgressAt = Time.time;
            Vector3 aside = to - dir * 0.9f + right * Mathf.Clamp(0.4f, -allowLeft, allowRight);
            Steer(here, aside, true, from, to, from, right, allowLeft, allowRight, dt, dir);
            return;
        }
        lateral = Mathf.MoveTowards(lateral, wantedLateral, 0.8f * dt);
        Vector3 goal = to + right * (arrive ? 0f : lateral);
        Steer(here, goal, arrive, from, to, from, right, allowLeft, allowRight, dt, dir);
    }

    // True while the next segment is too narrow for two people to pass (the path data
    // leaves under 0.3 m of room across it) and someone is in it coming this way, or
    // standing in it. Never for more than eight seconds: then carry on and squeeze past.
    private bool GivingWay(Vector3 here, Vector3 to)
    {
        int narrow = next;
        bool wait = false;
        if (narrow < points.Length - 1 && Flat(to - here).sqrMagnitude < 1.4f * 1.4f)
        {
            Band(narrow, out float left, out float right);
            if (left + right < 0.3f)
            {
                Vector3 a = points[narrow], b = points[narrow + 1];
                Vector3 d = Flat(b - a);
                float length = d.magnitude;
                if (length > 1e-3f)
                {
                    d /= length;
                    Vector3 side = new Vector3(d.z, 0f, -d.x);
                    foreach (NpcJourney other in active)
                    {
                        if (other == null || other == this || !other.isActiveAndEnabled) continue;
                        Vector3 p = Flat(other.transform.position - a);
                        float along = Vector3.Dot(p, d);
                        if (along < -0.3f || along > length + 0.3f || Mathf.Abs(Vector3.Dot(p, side)) > 0.5f) continue;
                        if (Vector3.Dot(other.velocity, d) < 0.1f) { wait = true; break; }
                    }
                }
            }
        }
        if (!wait) { givingWaySince = -1f; return false; }
        if (givingWaySince < 0f) givingWaySince = Time.time;
        return Time.time - givingWaySince < 8f;
    }

    // Picks this frame's velocity, moves, animates and advances along the path.
    private void Steer(Vector3 here, Vector3 goal, bool arrive, Vector3 from, Vector3 to,
                       Vector3 lineFrom, Vector3 lineRight, float allowLeft, float allowRight,
                       float dt, Vector3 restFacing)
    {
        Vector3 toGoal = Flat(goal - here);
        float distance = toGoal.magnitude;
        float want = arrive ? speed * Mathf.Clamp01(distance / 0.6f) : speed;
        if (distance < 0.05f) want = 0f;
        Vector3 preferred = distance > 1e-4f ? toGoal / distance * want : Vector3.zero;

        bool squeeze = !waiting && Time.time - lastProgressAt > SqueezeAfter;
        Vector3 chosen = ChooseVelocity(here, preferred, lineFrom, lineRight, allowLeft, allowRight, squeeze);
        float ease = chosen.sqrMagnitude < velocity.sqrMagnitude ? SlowDown : SpeedUp;
        velocity = Vector3.MoveTowards(velocity, chosen, ease * dt);
        if (obstacle != null && obstacle.enabled) obstacle.velocity = velocity;

        Vector3 before = here;
        Vector3 nextPosition = here + velocity * dt;
        // Height follows the path (kerbs, the lot, the patio) rather than the sidestep.
        nextPosition.y = HeightOnSegment(from, to, nextPosition);
        transform.position = nextPosition;
        float moved = Flat(nextPosition - before).magnitude;
        if (moved > 1e-4f) Face(nextPosition - before, dt);
        else if (waiting) Face(restFacing, dt);
        SetWalking(moved / dt > 0.2f, dt);

        if (!waiting) Advance(from, to, goal);
    }

    // ---- progress along the path ----
    private void Advance(Vector3 from, Vector3 to, Vector3 goal)
    {
        Vector3 segment = Flat(to - from);
        float length = segment.magnitude;
        Vector3 dir = length > 1e-4f ? segment / length : FlatForward();
        Vector3 at = transform.position;
        bool last = next == points.Length - 1;
        bool passed = Vector3.Dot(Flat(at - from), dir) >= length - (last ? 0.03f : 0.05f);
        bool close = Flat(goal - at).sqrMagnitude < (last ? 0.12f * 0.12f : 0.15f * 0.15f);
        if (passed || close) { NextSegment(); return; }

        float left = Flat(to - at).magnitude;
        if (left < bestRemaining - 0.05f) { bestRemaining = left; lastProgressAt = Time.time; }
        else if (Time.time - lastProgressAt > HelpAfter) HelpOn(to, dir);
    }

    private void NextSegment()
    {
        StreetCrossing finished = next - 1 < crossings.Length ? crossings[next - 1] : null;
        next++;
        LeaveCrossingUnlessItGoesOn(finished);
        bestRemaining = float.PositiveInfinity;
        lastProgressAt = Time.time;
        if (next >= points.Length) Finish();
    }

    // Someone (the player, a crowd at the door) would not clear the way for a long
    // time. Step past - but only onto free ground, never onto another person.
    private void HelpOn(Vector3 to, Vector3 dir)
    {
        Vector3 side = new Vector3(dir.z, 0f, -dir.x);
        Vector3[] tries = { to, to + side * 0.45f, to - side * 0.45f, to - dir * 0.6f };
        foreach (Vector3 spot in tries)
        {
            if (!FreeOfBodies(spot, BodyRadius * 2f + Personal)) continue;
            Unstuck++;
            transform.position = new Vector3(spot.x, to.y, spot.z);
            velocity = Vector3.zero;
            NextSegment();
            return;
        }
        lastProgressAt = Time.time - HelpAfter + 3f; // nowhere free yet: look again in a few seconds
    }

    // The café door is where an arriving walk hands over to the café's navigation. If
    // somebody is standing on the spot, land beside them instead of inside them.
    private void CheckLanding(Vector3 here)
    {
        if (Time.time < nextLandingCheck || points.Length < 2) return;
        nextLandingCheck = Time.time + 0.4f;
        int last = points.Length - 1;
        Vector3 end = points[last];
        if (Flat(end - here).sqrMagnitude > 2.5f * 2.5f) return;
        float clear = BodyRadius * 2f + Personal;
        if (FreeOfBodies(end, clear)) return;
        Vector3 approach = Flat(end - points[last - 1]);
        approach = approach.sqrMagnitude > 1e-6f ? approach.normalized : FlatForward();
        Vector3 side = new Vector3(approach.z, 0f, -approach.x);
        Vector3[] offsets = { side * 0.45f, -side * 0.45f, side * 0.8f, -side * 0.8f, -approach * 0.5f + side * 0.3f, -approach * 0.5f - side * 0.3f };
        foreach (Vector3 offset in offsets)
        {
            if (!NavMesh.SamplePosition(end + offset, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
            if (!FreeOfBodies(hit.position, clear)) continue;
            points[last] = hit.position;
            return;
        }
    }

    // A crossing can span several segments (kerb -> road -> far kerb): stay on it,
    // covered, until the segment after this one is no longer part of it. Leaving
    // between two of its segments would make the walker wait for a gap in the
    // middle of the road. Someone who stepped on in the last metres before the kerb
    // stays on through those metres too.
    private void LeaveCrossingUnlessItGoesOn(StreetCrossing finished)
    {
        if (onCrossing == null) return;
        StreetCrossing upcoming = next - 1 < crossings.Length ? crossings[next - 1] : null;
        if (upcoming == onCrossing || finished != onCrossing) return;
        onCrossing.Leave(this);
        onCrossing = null;
        crossLane = 0f;
    }

    // ------------------------------------------------------------------ avoidance

    private struct Body
    {
        public Vector3 position, velocity, forward, side;
        public float radius, halfLength, halfWidth;
        public bool car;
        public object owner;
    }

    private static readonly List<Body> bodies = new List<Body>(128);
    private static readonly List<Body> near = new List<Body>(32);
    private static int bodiesFrame = -1;
    private static NavMeshAgent[] agents = Array.Empty<NavMeshAgent>();
    private static float nextAgentScan;
    private static readonly float[] Angles = { 0f, 14f, -14f, 28f, -28f, 45f, -45f, 65f, -65f, 90f, -90f, 125f, -125f };
    private static readonly float[] Speeds = { 1f, 0.66f, 0.33f };

    // Everyone and everything a walker has to respect, gathered once a frame.
    private static void GatherBodies()
    {
        if (bodiesFrame == Time.frameCount) return;
        bodiesFrame = Time.frameCount;
        bodies.Clear();
        foreach (NpcJourney j in active)
            if (j != null && j.isActiveAndEnabled)
                bodies.Add(new Body { position = j.transform.position, velocity = j.velocity, radius = BodyRadius, owner = j });

        StreetLife life = StreetLife.Main;
        if (life != null)
            foreach (StreetLife.Actor a in life.actors)
            {
                if (a == null || a.actor == null || a.respawning || !a.actor.gameObject.activeInHierarchy) continue;
                if (a.isWalker)
                    bodies.Add(new Body { position = a.actor.position, velocity = a.velocity, radius = 0.3f, owner = a });
                else if (a.trafficManaged)
                {
                    Vector3 f = Flat(a.actor.forward);
                    f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
                    bodies.Add(new Body
                    {
                        position = a.actor.position, velocity = f * a.lastSpeed, forward = f, side = new Vector3(f.z, 0f, -f.x),
                        halfLength = Mathf.Max(0.5f, a.vehicleLength) * 0.5f, halfWidth = 1.0f, car = true, owner = a
                    });
                }
            }

        if (Time.unscaledTime >= nextAgentScan)
        {
            nextAgentScan = Time.unscaledTime + 0.5f;
            agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude);
        }
        foreach (NavMeshAgent agent in agents)
            if (agent != null && agent.isActiveAndEnabled)
                bodies.Add(new Body { position = agent.transform.position, velocity = agent.velocity, radius = Mathf.Clamp(agent.radius, 0.25f, 0.35f), owner = agent });
        foreach (CharacterController player in CafeArrivals.Players)
            if (player != null && player.enabled)
                bodies.Add(new Body { position = player.transform.position, velocity = player.velocity, radius = 0.3f, owner = player });

        CafeArrivals arrivals = CafeArrivals.Instance;
        if (arrivals != null)
            foreach (CafeCar car in arrivals.Cars)
            {
                // In a lane a café car is one of StreetLife's (above); in the lot, parked or
                // manoeuvring, it is a box people walk round.
                if (car == null || car.State == CafeCar.Phase.Pooled || car.InTraffic || !car.gameObject.activeInHierarchy) continue;
                Vector3 f = Flat(car.transform.forward);
                f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
                bodies.Add(new Body
                {
                    position = car.BodyCentre, velocity = car.Velocity, forward = f, side = new Vector3(f.z, 0f, -f.x),
                    halfLength = car.Length * 0.5f, halfWidth = car.Width * 0.5f, car = true, owner = car
                });
            }
    }

    private Vector3 ChooseVelocity(Vector3 here, Vector3 preferred, Vector3 lineFrom, Vector3 lineRight,
                                   float allowLeft, float allowRight, bool squeeze)
    {
        near.Clear();
        foreach (Body b in bodies)
        {
            if (ReferenceEquals(b.owner, this)) continue;
            Vector3 d = b.position - here;
            if (Mathf.Abs(d.y) > 1.6f) continue;
            float range = b.car ? CarRange : PeopleRange;
            if (d.x * d.x + d.z * d.z > range * range) continue;
            near.Add(b);
        }
        HeldBy = "";

        float prefSpeed = preferred.magnitude;
        if (near.Count == 0) return preferred;   // the goal always lies inside the room

        float top = Mathf.Max(speed, 0.5f);
        Vector3 baseDir = prefSpeed > 1e-3f ? preferred / prefSpeed
                        : velocity.sqrMagnitude > 1e-4f ? Flat(velocity).normalized : FlatForward();
        Vector3 baseRight = new Vector3(baseDir.z, 0f, -baseDir.x);

        // Someone coming the other way, close ahead: pass them keeping right.
        bool oncoming = false;
        foreach (Body b in near)
        {
            if (b.car) continue;
            Vector3 d = Flat(b.position - here);
            float along = Vector3.Dot(d, baseDir);
            if (along < 0.2f || along > 3f || Mathf.Abs(Vector3.Dot(d, baseRight)) > 1f) continue;
            if (Vector3.Dot(b.velocity, baseDir) < -0.3f) { oncoming = true; break; }
        }

        float shrink = squeeze ? 0.14f : 0f;
        Vector3 best = Vector3.zero;
        float bestCost = Cost(best, preferred, top, here, lineFrom, lineRight, allowLeft, allowRight, shrink, baseRight, oncoming, out int bestHeld);
        float moveSpeed = prefSpeed > 0.05f ? prefSpeed : 0.45f; // standing: small steps only (out of someone's way)
        foreach (float angle in Angles)
        {
            Vector3 d = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
            foreach (float f in Speeds)
            {
                Vector3 v = d * (moveSpeed * f);
                float c = Cost(v, preferred, top, here, lineFrom, lineRight, allowLeft, allowRight, shrink, baseRight, oncoming, out int held);
                if (c < bestCost) { bestCost = c; best = v; bestHeld = held; }
            }
        }
        if (bestHeld >= 0 && best.sqrMagnitude < prefSpeed * prefSpeed * 0.25f) HeldBy = Describe(near[bestHeld]);
        return best;
    }

    private float Cost(Vector3 v, Vector3 preferred, float top, Vector3 here, Vector3 lineFrom, Vector3 lineRight,
                       float allowLeft, float allowRight, float shrink, Vector3 baseRight, bool oncoming, out int heldBy)
    {
        heldBy = -1;
        float scale = 1f / (top * top);
        float cost = (v - preferred).sqrMagnitude * scale;
        cost += 0.2f * (v - Flat(velocity)).sqrMagnitude * scale;          // no dithering between choices
        float outside = OutsideRoom(here + v * 0.5f, lineFrom, lineRight, allowLeft, allowRight);
        if (outside > 0f) cost += 14f * outside * outside + 2f * outside;  // off the path: planters, posts, the road
        if (oncoming) cost += 0.35f * Mathf.Max(0f, -Vector3.Dot(v, baseRight)) / top;
        float worst = 0f;
        for (int i = 0; i < near.Count; i++)
        {
            Body b = near[i];
            float t = TimeToTouch(here, v, b, shrink);
            if (t < 0f) continue;
            float c;
            if (t == 0f)
            {
                // Already touching: anything that opens the gap beats standing in them.
                Vector3 toward = Flat(b.position - here);
                float closing = toward.sqrMagnitude > 1e-6f ? Vector3.Dot(Flat(v - b.velocity), toward.normalized) / top : 0f;
                c = 6f + 5f * closing;
            }
            else
            {
                float soon = 1f - t / Horizon;
                c = 4f * soon * soon;
            }
            cost += c;
            if (c > worst) { worst = c; heldBy = i; }
        }
        return cost;
    }

    // Seconds until our body, moving at v, first touches b (-1: not within the horizon; 0: touching now).
    private static float TimeToTouch(Vector3 here, Vector3 v, in Body b, float shrink)
    {
        if (!b.car)
        {
            float reach = BodyRadius + b.radius + Personal - shrink;
            Vector3 d = Flat(b.position - here);
            Vector3 w = Flat(v - b.velocity);
            float c = d.sqrMagnitude - reach * reach;
            if (c < 0f) return 0f;
            float a = w.sqrMagnitude;
            if (a < 1e-6f) return -1f;
            float closing = Vector3.Dot(d, w);
            if (closing <= 0f) return -1f;
            float disc = closing * closing - a * c;
            if (disc < 0f) return -1f;
            float t = (closing - Mathf.Sqrt(disc)) / a;
            return t <= Horizon ? Mathf.Max(t, 1e-3f) : -1f;
        }
        // A car: its box, grown by our body.
        Vector3 rel = Flat(here - b.position);
        Vector3 rv = Flat(v - b.velocity);
        float grow = BodyRadius + Personal - shrink;
        float px = Vector3.Dot(rel, b.forward), pz = Vector3.Dot(rel, b.side);
        float vx = Vector3.Dot(rv, b.forward), vz = Vector3.Dot(rv, b.side);
        float t0 = 0f, t1 = Horizon;
        if (!Slab(px, vx, b.halfLength + grow, ref t0, ref t1) || !Slab(pz, vz, b.halfWidth + grow, ref t0, ref t1)) return -1f;
        return t0 <= 0f ? 0f : t0;
    }

    private static bool Slab(float p, float v, float half, ref float t0, ref float t1)
    {
        if (Mathf.Abs(v) < 1e-6f) return p >= -half && p <= half;
        float a = (-half - p) / v, b = (half - p) / v;
        if (a > b) { float swap = a; a = b; b = swap; }
        if (a > t0) t0 = a;
        if (b < t1) t1 = b;
        return t0 <= t1;
    }

    // How far a point is outside the room either side of the line (0 inside it).
    private static float OutsideRoom(Vector3 point, Vector3 lineFrom, Vector3 lineRight, float allowLeft, float allowRight)
    {
        float side = Vector3.Dot(Flat(point - lineFrom), lineRight);
        if (side > allowRight) return side - allowRight;
        if (side < -allowLeft) return -allowLeft - side;
        return 0f;
    }

    private bool FreeOfBodies(Vector3 spot, float clear)
    {
        foreach (Body b in bodies)
        {
            if (ReferenceEquals(b.owner, this)) continue;
            if (Mathf.Abs(b.position.y - spot.y) > 1.6f) continue;
            if (b.car)
            {
                Vector3 r = Flat(spot - b.position);
                if (Mathf.Abs(Vector3.Dot(r, b.forward)) < b.halfLength + BodyRadius && Mathf.Abs(Vector3.Dot(r, b.side)) < b.halfWidth + BodyRadius) return false;
                continue;
            }
            Vector3 d = Flat(b.position - spot);
            if (d.sqrMagnitude < clear * clear) return false;
        }
        return true;
    }

    private static string Describe(in Body b)
    {
        switch (b.owner)
        {
            case NpcJourney j: return "visitor " + j.name;
            case CafeCar c: return "café car " + c.name;
            case NavMeshAgent a: return "café NPC " + a.name;
            case CharacterController _: return "the player";
            case StreetLife.Actor s: return (b.car ? "traffic " : "street walker ") + (s.actor != null ? s.actor.name : "");
            default: return "someone";
        }
    }

    // ------------------------------------------------------------------ helpers

    // Room to one side of a segment. It can be negative: that side of the line itself grazes
    // something, so walkers keep at least that far over to the other side.
    private static float Room(float[] room, int segment) =>
        segment >= 0 && segment < room.Length ? room[segment] : DefaultRoom;

    // The band [-left, right] a walker keeps to on a segment (never inside out).
    private void Band(int segment, out float left, out float right)
    {
        left = Room(roomLeft, segment);
        right = Room(roomRight, segment);
        if (-left > right) { float middle = (right - left) * 0.5f; left = -middle; right = middle; }
    }

    private static float[] Filled(int count, float value)
    {
        var result = new float[count];
        for (int i = 0; i < count; i++) result[i] = value;
        return result;
    }

    private static float HeightOnSegment(Vector3 from, Vector3 to, Vector3 at)
    {
        Vector3 edge = to - from;
        float flat = edge.x * edge.x + edge.z * edge.z;
        if (flat < 1e-6f) return to.y;
        float t = Mathf.Clamp01(((at.x - from.x) * edge.x + (at.z - from.z) * edge.z) / flat);
        return Mathf.Lerp(from.y, to.y, t);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private Vector3 FlatForward()
    {
        Vector3 f = Flat(transform.forward);
        return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
    }

    private void Face(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-8f) return;
        Quaternion goal = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, 420f * Mathf.Max(dt, .001f));
    }

    // Different start and stop thresholds with a short hold, so the walk clip
    // doesn't flicker while someone shuffles behind another person.
    private void SetWalking(bool wants, float dt)
    {
        if (animator == null || !hasWalkingParameter) return;
        if (wants == walkingShown) { walkingChange = 0f; return; }
        walkingChange += dt;
        if (walkingChange < 0.12f) return;
        walkingShown = wants;
        walkingChange = 0f;
        animator.SetBool(IsWalkingHash, wants);
    }

    private void Finish()
    {
        if (animator != null && hasWalkingParameter) animator.SetBool(IsWalkingHash, false);
        walkingShown = false;
        ReleaseCrossings();
        velocity = Vector3.zero;
        ShowToNavigation(false);   // before the café's navigation takes the body back
        Action done = whenDone;
        whenDone = null;
        enabled = false;
        done?.Invoke();
    }
}
