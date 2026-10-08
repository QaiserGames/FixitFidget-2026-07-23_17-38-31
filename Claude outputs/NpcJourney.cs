using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks a café NPC along a fixed path outside the café: from a parked car or a
/// neighbour's front door to the café door (arriving), or back again (leaving).
/// CafeArrivals starts it. While it runs, the NPC's brain and navigation are off,
/// so everything the brain does inside the café is exactly as before - it simply
/// starts at the door instead of appearing there.
///
/// Outside there is no navigation mesh, so the path is walked by hand:
///  * at the kerb of a crossing the walker waits for the crossing to say go
///    (a walk signal at junctions, a gap in traffic at the café's zebra); cars in
///    turn stop for people on (and, mid-block, waiting at) a crossing;
///  * nobody walks into anybody: a walker follows someone slower at a polite gap,
///    steps round someone standing still, and keeps right for oncoming people -
///    street walkers, other visitors, café NPCs at the door and the player alike;
///  * the walking animation plays while moving and idles while waiting.
/// Street walkers and cars see these walkers too (StreetLife bodies).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)] // positions are current before StreetLife solves the street
public sealed class NpcJourney : MonoBehaviour, StreetLife.IStreetBody
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly List<NpcJourney> active = new List<NpcJourney>();

    private const float BodyRadius = 0.28f;
    private const float LookAhead = 1.5f;
    private const float Corridor = 0.32f;
    private const float FollowGap = 0.3f;     // clear space kept behind someone, metres
    private const float MaxSidestep = 0.38f;

    private Vector3[] points = Array.Empty<Vector3>();
    private StreetCrossing[] crossings = Array.Empty<StreetCrossing>();
    private int next;
    private float speed = 1.3f;
    private Action whenDone;
    private Animator animator;
    private bool hasWalkingParameter;
    private bool walkingShown;
    private float walkingChange;
    private StreetCrossing onCrossing, waitingAt;
    private float lateral, lateralTarget, heldFor, clearFor;
    private Vector3 velocity;
    private float bestRemaining = float.PositiveInfinity;
    private float lastProgressAt;
    private float startedAt;

    /// <summary>True while walking in to the café (not yet started inside), false while leaving.</summary>
    public bool Arriving { get; private set; }
    public CafeArrivals.Kind Kind { get; private set; }
    /// <summary>Seconds spent waiting at kerbs so far (for checks).</summary>
    public float WaitedAtKerbs { get; private set; }
    /// <summary>Times this walk had to be helped past something that would not clear (for checks).</summary>
    public int Unstuck { get; private set; }
    public float Age => Time.time - startedAt;
    public StreetCrossing CurrentCrossing => onCrossing;
    public int PointsLeft => Mathf.Max(0, points.Length - next);

    public Vector3 Position => transform.position;
    public Vector3 Velocity => velocity;
    public float Radius => BodyRadius;

    public static IReadOnlyList<NpcJourney> Active => active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    /// <summary>Walkers of a kind still on their way in (the spawners keep room for them).</summary>
    public static int OnTheWay(CafeArrivals.Kind kind)
    {
        int count = 0;
        foreach (NpcJourney j in active)
            if (j != null && j.Arriving && j.Kind == kind) count++;
        return count;
    }

    /// <summary>
    /// Starts (or replaces) the walk. <paramref name="segmentCrossings"/>[i] is the
    /// crossing between points[i] and points[i + 1], or null.
    /// </summary>
    public void Begin(Vector3[] path, StreetCrossing[] segmentCrossings, float walkSpeed,
                      bool arriving, CafeArrivals.Kind kind, Action done)
    {
        ReleaseCrossings();
        points = path ?? Array.Empty<Vector3>();
        crossings = segmentCrossings != null && segmentCrossings.Length >= Mathf.Max(0, points.Length - 1)
            ? segmentCrossings : new StreetCrossing[Mathf.Max(0, points.Length - 1)];
        speed = Mathf.Max(.4f, walkSpeed);
        Arriving = arriving;
        Kind = kind;
        whenDone = done;
        next = points.Length > 1 ? 1 : points.Length;
        lateral = lateralTarget = heldFor = clearFor = 0f;
        bestRemaining = float.PositiveInfinity;
        lastProgressAt = startedAt = Time.time;
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
        enabled = true;
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
        for (int i = last; i >= 0; i--)
        {
            path.Add(points[i]);
            // Going back from here to points[last] retraces part of segment `last` - a
            // crossing only if they are actually on it. After that, walking
            // points[i + 1] -> points[i] reverses segment i.
            cross.Add(i == last
                ? (onCrossing != null && last < crossings.Length && crossings[last] == onCrossing ? onCrossing : null)
                : crossings[i]);
        }
        StreetCrossing keep = cross.Count > 0 ? cross[0] : null;
        Begin(path.ToArray(), cross.ToArray(), speed, arriving, Kind, done); // releases every crossing
        if (keep != null) { keep.Enter(this); onCrossing = keep; }           // still on the road: stay covered
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
    }

    private void ReleaseCrossings()
    {
        onCrossing?.Forget(this);
        waitingAt?.Forget(this);
        onCrossing = waitingAt = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (next >= points.Length) { Finish(); return; }

        Vector3 from = points[next - 1], to = points[next];
        StreetCrossing crossing = next - 1 < crossings.Length ? crossings[next - 1] : null;
        Vector3 here = transform.position;
        Vector3 segment = to - from;
        segment.y = 0f;
        Vector3 segmentDir = segment.sqrMagnitude > 1e-6f ? segment.normalized : transform.forward;

        // ---- kerb: a crossing segment starts where the last one ended, at the kerb.
        //      Wait there for the crossing to say go. ----
        if (crossing != null && onCrossing != crossing)
        {
            if (waitingAt != crossing)
            {
                waitingAt?.StopWaiting(this);
                waitingAt = crossing;
                crossing.StartWaiting(this);
            }
            if (!crossing.CanStep(segment.magnitude / speed))
            {
                WaitHere(segmentDir, dt);
                WaitedAtKerbs += dt;
                lastProgressAt = Time.time; // waiting for traffic is not being stuck
                return;
            }
            crossing.Enter(this);
            waitingAt = null;
            onCrossing = crossing;
        }

        // ---- steering: keep our lane, give way, never walk into anyone ----
        bool crossingRoad = onCrossing != null;
        Vector3 rightOfPath = new Vector3(segmentDir.z, 0f, -segmentDir.x);
        Vector3 target = to + rightOfPath * lateral;
        Vector3 heading = Flat(target - here);
        float remaining = heading.magnitude;
        float step = speed * dt;
        float allowed = step;

        if (remaining > 1e-4f && StreetLife.NearestBodyAhead(here, heading, LookAhead, Corridor, this,
                out Vector3 bodyPosition, out Vector3 bodyVelocity, out float bodyRadius))
        {
            Vector3 offset = Flat(bodyPosition - here);
            Vector3 dir = heading / remaining;
            float along = Vector3.Dot(offset, dir);
            float side = Vector3.Dot(offset, new Vector3(dir.z, 0f, -dir.x));
            float clear = along - bodyRadius - BodyRadius;
            bool oncoming = Vector3.Dot(bodyVelocity, dir) < -0.3f;
            if (oncoming)
            {
                // Keep right of them (away from their side); slow a little, never stop.
                if (!crossingRoad) lateralTarget = (Mathf.Abs(side) < 0.08f ? 1f : -Mathf.Sign(side)) * MaxSidestep;
                if (clear < FollowGap) allowed = Mathf.Min(allowed, step * 0.35f);
                else allowed = Mathf.Min(allowed, step * 0.75f);
                heldFor = 0f;
            }
            else
            {
                // Same way or standing: keep a gap; after a moment, step round them.
                allowed = Mathf.Min(allowed, Mathf.Max(0f, clear - FollowGap));
                heldFor += allowed < step * 0.5f ? dt : 0f;
                if (!crossingRoad && heldFor > 1.2f && bodyVelocity.sqrMagnitude < 0.2f)
                    lateralTarget = (Mathf.Abs(side) < 0.08f ? -1f : -Mathf.Sign(side)) * MaxSidestep;
                // Someone crossing the road never stands in it for long.
                if (crossingRoad && clear > 0.05f) allowed = Mathf.Max(allowed, step * 0.3f);
            }
            clearFor = 0f;
        }
        else
        {
            heldFor = 0f;
            clearFor += dt;
            if (clearFor > 0.8f) lateralTarget = 0f;
        }
        // On the road, and the last metre and a half before a kerb: walk the line.
        bool kerbAhead = next < crossings.Length && crossings[next] != null && remaining < 1.5f;
        if (crossingRoad || kerbAhead) lateralTarget = 0f;
        lateral = Mathf.MoveTowards(lateral, lateralTarget, 0.8f * dt);

        // ---- move ----
        Vector3 before = here;
        float move = Mathf.Min(allowed, remaining);
        Vector3 nextPosition = remaining > 1e-4f ? here + heading / remaining * move : here;
        // Height follows the path (kerbs, the lot, the patio) rather than the sidestep.
        nextPosition.y = HeightOnSegment(from, to, nextPosition);
        transform.position = nextPosition;
        velocity = (nextPosition - before) / dt;
        if (move > 1e-4f) Face(nextPosition - before, dt);
        SetWalking(move / dt > 0.2f, dt);

        // ---- progress along the path ----
        if (Flat(to + rightOfPath * lateral - transform.position).sqrMagnitude < 0.03f * 0.03f
            || Vector3.Dot(Flat(transform.position - from), segmentDir) >= segment.magnitude - 0.02f)
        {
            LeaveCrossingUnlessItGoesOn(crossing);
            next++;
            bestRemaining = float.PositiveInfinity;
            lastProgressAt = Time.time;
            return;
        }

        float left = Flat(to - transform.position).magnitude;
        if (left < bestRemaining - 0.05f) { bestRemaining = left; lastProgressAt = Time.time; }
        else if (Time.time - lastProgressAt > 12f)
        {
            // Someone would not clear the way for twelve seconds (a crowd at the
            // door, the player standing in the path). Step past rather than stand
            // there for ever; the checks count how often this happens.
            Unstuck++;
            transform.position = new Vector3(to.x, to.y, to.z);
            LeaveCrossingUnlessItGoesOn(crossing);
            next++;
            bestRemaining = float.PositiveInfinity;
            lastProgressAt = Time.time;
        }
    }

    // A crossing can span several segments (kerb -> road -> far kerb): stay on it,
    // covered, until the segment after this one is no longer part of it. Leaving
    // between two of its segments would make the walker wait for a gap in the
    // middle of the road.
    private void LeaveCrossingUnlessItGoesOn(StreetCrossing finished)
    {
        if (onCrossing == null || onCrossing != finished) return;
        StreetCrossing upcoming = next < crossings.Length ? crossings[next] : null;
        if (upcoming == onCrossing) return;
        onCrossing.Leave(this);
        onCrossing = null;
    }

    private void WaitHere(Vector3 facing, float dt)
    {
        velocity = Vector3.zero;
        Face(facing, dt);
        SetWalking(false, dt);
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
        Action done = whenDone;
        whenDone = null;
        enabled = false;
        done?.Invoke();
    }
}
