using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One café customer's car. CafeArrivals owns a small pool of them.
///
/// Its day, in order:
///  1. Joins the entry lane at the far end of the street (a StreetLife guest: it
///     obeys the lights and keeps its distance like every other car).
///  2. Near the lot it leaves the lane and follows the authored turn-in path into its
///     stall, giving way to anyone on foot and holding up the lane behind it while
///     it turns (a StreetLife road block).
///  3. Parks with its driver still inside ("waiting"); CafeArrivals lets the driver
///     out when the café calls for a new arrival.
///  4. When its driver comes back and gets in, it backs out, drives to the exit,
///     waits for a gap and joins the exit lane, which carries it away. At the end of
///     that lane it goes back into the pool.
///
/// Paths are sampled poses (x, y, z, yaw in degrees) written by the parking-lot tool.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)] // before StreetLife, so its road block and position are current
public sealed class CafeCar : MonoBehaviour, StreetLife.IStreetBody
{
    public enum Phase { Pooled, ToLot, HeldAtLot, TurningIn, Parked, WaitingToLeave, BackingOut, ToExit, WaitingAtExit, TurningOut, DrivingAway }

    [SerializeField] private Transform[] wheels = Array.Empty<Transform>();
    [SerializeField, Min(0.05f)] private float wheelRadius = 0.35f;
    [SerializeField] private Vector3 wheelAxis = Vector3.right;
    [SerializeField, Min(1f)] private float length = 4.6f;
    [SerializeField, Min(0.5f)] private float width = 1.95f;

    [Header("Driving in the lot")]
    [SerializeField, Min(0.5f)] private float lotSpeed = 2.3f;
    [SerializeField, Min(0.3f)] private float reverseSpeed = 1.1f;
    [SerializeField, Min(0.3f)] private float acceleration = 1.6f;
    [SerializeField, Min(0.5f)] private float braking = 2.6f;

    private struct Pose { public Vector3 position; public float yaw; public float at; public int gear; }

    private readonly List<Pose> path = new List<Pose>();
    private float s, pathLength, speed;
    private Action pathDone;
    private float blockUntil;               // road block kept while s < this (turning across a lane)
    private StreetLife.RoadBlock roadBlock, holdBlock;
    private StreetLife.Actor traffic;
    private CafeArrivals owner;
    private float wheelTurn;
    private Quaternion[] wheelRest = Array.Empty<Quaternion>();
    private float waitStartedAt;
    private float centreOffset;             // the body's middle, metres ahead of the pivot

    public Phase State { get; private set; } = Phase.Pooled;
    public int Stall { get; internal set; } = -1;
    /// <summary>Parked with the driver still in the car: ready to let an arriving visitor out.</summary>
    public bool DriverWaiting { get; internal set; }
    /// <summary>The visitor this car brought, while they are away from it.</summary>
    public GameObject Owner { get; internal set; }
    public float Length => length;
    public float Width => width;
    public float PhaseAge => Time.time - waitStartedAt;
    public bool InTraffic => traffic != null;
    public float RouteDistance => traffic != null ? traffic.RouteDistance : -1f;

    /// <summary>The middle of the car's body (its pivot sits a little behind it).</summary>
    public Vector3 BodyCentre => transform.position + transform.forward * centreOffset;
    public Vector3 Position => transform.position;
    public Vector3 Velocity { get; private set; }
    public float Radius => width * 0.5f + 0.25f;

    /// <summary>Written by the parking-lot tool.</summary>
    public void Configure(Transform[] carWheels, float radius, Vector3 axis, float carLength, float carWidth)
    {
        wheels = carWheels ?? Array.Empty<Transform>();
        wheelRadius = Mathf.Max(0.05f, radius);
        wheelAxis = axis;
        length = Mathf.Max(1f, carLength);
        width = Mathf.Max(0.5f, carWidth);
    }

    private void Awake()
    {
        wheelRest = new Quaternion[wheels.Length];
        for (int i = 0; i < wheels.Length; i++) wheelRest[i] = wheels[i] != null ? wheels[i].localRotation : Quaternion.identity;
        centreOffset = MeasureCentreOffset();
    }

    // Where the body's middle is along the car, from its meshes (the same measure the
    // parking-lot tool planned the paths with).
    private float MeasureCentreOffset()
    {
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 toCar = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                float z = toCar.MultiplyPoint3x4(corner).z;
                min = Mathf.Min(min, z);
                max = Mathf.Max(max, z);
            }
        }
        return float.IsInfinity(min) ? 0f : (min + max) * 0.5f * Mathf.Abs(transform.lossyScale.z);
    }

    private void OnDisable()
    {
        StreetLife.UnregisterBody(this);
        ClearBlocks();
    }

    private void SetPhase(Phase phase)
    {
        State = phase;
        waitStartedAt = Time.time;
        bool moving = phase == Phase.TurningIn || phase == Phase.BackingOut || phase == Phase.ToExit || phase == Phase.TurningOut;
        if (moving) StreetLife.RegisterBody(this); else StreetLife.UnregisterBody(this);
    }

    // ------------------------------------------------------------------ arriving

    /// <summary>Starts the drive in: joins the entry lane at its far end. False if the lane start is busy.</summary>
    internal bool Launch(CafeArrivals arrivals, StreetLife life, string lane, float speedInTraffic, int stall)
    {
        if (life == null || !life.CanJoinTraffic(lane, 0f, length, 2f)) return false;
        owner = arrivals;
        Stall = stall;
        DriverWaiting = true;
        Owner = null;
        gameObject.SetActive(true);
        traffic = life.JoinTraffic(lane, transform, length, speedInTraffic, wheels, wheelRadius, wheelAxis, 0f, MissedTheLot);
        if (traffic == null) { ReturnToPool(); return false; }
        SetPhase(Phase.ToLot);
        return true;
    }

    // It drove past (the café closed on the way, or it was told to): nothing to do but go home.
    private void MissedTheLot(StreetLife.Actor _)
    {
        traffic = null;
        owner?.CarFinished(this);
        ReturnToPool();
    }

    /// <summary>Called while in the entry lane: hold at the turn if the lot is busy, turn in when it isn't.</summary>
    private void UpdateToLot()
    {
        StreetLife life = StreetLife.Main;
        if (traffic == null || life == null || owner == null) return;
        if (owner.SkipLot)
        {
            // Closing: stay in the lane and drive past.
            if (holdBlock != null) { StreetLife.RemoveRoadBlock(holdBlock); holdBlock = null; }
            return;
        }
        float toTurn = owner.TurnInDistance - traffic.RouteDistance;
        if (toTurn > 45f) return;
        owner.AnnounceEntry(this);
        bool free = owner.LotFreeFor(this) && !PeopleIn(owner.TurnInPath, 0, owner.TurnInPath.Length);
        if (!free)
        {
            // Wait at the turn with the indicator on: a block just past it stops us (and the lane behind).
            if (holdBlock == null)
            {
                life.TryRoutePoint(owner.EntryLane, owner.TurnInDistance + length * 0.5f + 1.2f, out Vector3 p, out Vector3 dir);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                holdBlock = StreetLife.AddRoadBlock(p, new Vector2(1.2f, 0.4f), yaw, true, false, false, "Café car waiting to turn in");
            }
            SetPhase(Phase.HeldAtLot);
            return;
        }
        if (holdBlock != null) { StreetLife.RemoveRoadBlock(holdBlock); holdBlock = null; }
        if (State == Phase.HeldAtLot) SetPhase(Phase.ToLot);
        if (toTurn > 0.05f) return;

        // Off the lane and into the lot, carrying the speed it arrived with.
        owner.TakeLot(this);
        float arrivingSpeed = traffic.CurrentSpeed;
        life.LeaveTraffic(traffic);
        traffic = null;
        var poses = new List<Vector4>(owner.TurnInPath);
        Vector4[] stallPath = owner.StallEntry(Stall);
        if (stallPath != null) poses.AddRange(stallPath);
        FollowPath(poses, 1, owner.TurnInPath.Length > 0 ? PathLength(owner.TurnInPath) * 0.85f : 0f, Parked);
        speed = Mathf.Clamp(arrivingSpeed, 0f, lotSpeed + 1f);
        SetPhase(Phase.TurningIn);
    }

    private void Parked()
    {
        ClearBlocks();
        speed = 0f;
        SetPhase(Phase.Parked);
        owner?.CarParked(this);
    }

    // ------------------------------------------------------------------ leaving

    /// <summary>The driver is back in the car: leave once the lot is free.</summary>
    internal void Leave()
    {
        if (State != Phase.Parked) return;
        DriverWaiting = false;
        Owner = null;
        SetPhase(Phase.WaitingToLeave);
    }

    private void UpdateWaitingToLeave()
    {
        if (owner == null || PhaseAge < 1.2f) return;               // doors, seat belt, mirrors
        if (!owner.LotFreeFor(this) || owner.EntryExpected) return;  // arrivals turning in go first
        Vector4[] back = owner.StallBackOut(Stall), toExit = owner.StallToExit(Stall);
        if (back == null || toExit == null) return;
        if (PeopleIn(back, 0, back.Length)) return;
        owner.TakeLot(this);
        var poses = new List<Vector4>(back);
        FollowPath(poses, -1, 0f, () =>
        {
            owner.StallVacated(this);
            FollowPath(new List<Vector4>(toExit), 1, 0f, ReachedExit);
            SetPhase(Phase.ToExit);
        });
        SetPhase(Phase.BackingOut);
    }

    private void ReachedExit()
    {
        speed = 0f;
        owner?.ReleaseLot(this);   // the exit is past every stall: others may use the lot now
        SetPhase(Phase.WaitingAtExit);
    }

    private void UpdateWaitingAtExit()
    {
        StreetLife life = StreetLife.Main;
        if (owner == null) return;
        if (!owner.ExitFreeFor(this)) return;
        if (life != null && !life.CanJoinTraffic(owner.ExitLane, owner.JoinDistance, length, 2.6f)) return;
        if (PeopleIn(owner.TurnOutPath, 0, owner.TurnOutPath.Length)) return;
        owner.TakeExit(this);
        FollowPath(new List<Vector4>(owner.TurnOutPath), 1, float.PositiveInfinity, JoinExitLane);
        SetPhase(Phase.TurningOut);
    }

    private void JoinExitLane()
    {
        ClearBlocks();
        owner?.ReleaseExit(this);
        StreetLife life = StreetLife.Main;
        if (life != null && owner != null)
            traffic = life.JoinTraffic(owner.ExitLane, transform, length, owner.CarSpeedInTraffic, wheels, wheelRadius, wheelAxis,
                                       owner.JoinDistance, DroveAway);
        if (traffic == null) { DroveAway(null); return; }
        SetPhase(Phase.DrivingAway);
    }

    private void DroveAway(StreetLife.Actor _)
    {
        traffic = null;
        owner?.CarFinished(this);
        ReturnToPool();
    }

    /// <summary>Back into the pool: out of traffic, off the map, forgotten.</summary>
    internal void ReturnToPool()
    {
        StreetLife life = StreetLife.Main;
        if (traffic != null && life != null) life.LeaveTraffic(traffic);
        traffic = null;
        ClearBlocks();
        path.Clear();
        pathDone = null;
        speed = 0f;
        Velocity = Vector3.zero;
        Stall = -1;
        DriverWaiting = false;
        Owner = null;
        SetPhase(Phase.Pooled);
        gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ frame

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        switch (State)
        {
            case Phase.ToLot:
            case Phase.HeldAtLot:
                UpdateToLot();
                break;
            case Phase.WaitingToLeave:
                UpdateWaitingToLeave();
                break;
            case Phase.WaitingAtExit:
                UpdateWaitingAtExit();
                break;
            case Phase.TurningIn:
            case Phase.BackingOut:
            case Phase.ToExit:
            case Phase.TurningOut:
                Drive(dt);
                break;
            default:
                Velocity = Vector3.zero;
                break;
        }
    }

    private void FollowPath(List<Vector4> poses, int gear, float keepBlockFor, Action done)
    {
        path.Clear();
        float at = 0f;
        for (int i = 0; i < poses.Count; i++)
        {
            Vector3 p = new Vector3(poses[i].x, poses[i].y, poses[i].z);
            if (i > 0) at += Vector3.Distance(p, path[path.Count - 1].position);
            path.Add(new Pose { position = p, yaw = poses[i].w, at = at, gear = gear });
        }
        pathLength = at;
        s = 0f;
        pathDone = done;
        blockUntil = keepBlockFor;
        if (path.Count > 0) PlaceAt(0f);
    }

    private static float PathLength(Vector4[] poses)
    {
        float total = 0f;
        for (int i = 1; i < poses.Length; i++)
            total += Vector3.Distance(new Vector3(poses[i].x, poses[i].y, poses[i].z), new Vector3(poses[i - 1].x, poses[i - 1].y, poses[i - 1].z));
        return total;
    }

    private void Drive(float dt)
    {
        if (path.Count < 2) { Finish(); return; }
        int gear = path[0].gear;
        float cruise = gear < 0 ? reverseSpeed : lotSpeed;
        float remaining = pathLength - s;
        // Ease to a stop at the end of the path; stop for anyone on foot in the way.
        float target = Mathf.Min(cruise, Mathf.Sqrt(2f * braking * Mathf.Max(0f, remaining)) + 0.15f);
        float look = speed * speed / (2f * braking) + 1.0f;
        if (PersonAhead(gear, look)) target = 0f;
        speed = speed < target ? Mathf.Min(target, speed + acceleration * dt) : Mathf.Max(target, speed - braking * dt);

        Vector3 before = transform.position;
        s = Mathf.Min(pathLength, s + speed * dt);
        PlaceAt(s);
        Velocity = (transform.position - before) / dt;
        SpinWheels(speed * dt * gear);
        UpdateRoadBlock();
        if (s >= pathLength - 1e-3f) Finish();
    }

    private void Finish()
    {
        speed = 0f;
        Velocity = Vector3.zero;
        Action done = pathDone;
        pathDone = null;
        done?.Invoke();
    }

    private void PlaceAt(float at)
    {
        int i = 1;
        while (i < path.Count - 1 && path[i].at < at) i++;
        Pose a = path[i - 1], b = path[i];
        float span = b.at - a.at;
        float t = span > 1e-5f ? Mathf.Clamp01((at - a.at) / span) : 1f;
        transform.position = Vector3.Lerp(a.position, b.position, t);
        transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, t), 0f);
    }

    private void SpinWheels(float moved)
    {
        if (wheels.Length == 0) return;
        if (wheelRest.Length != wheels.Length) Awake();
        wheelTurn = Mathf.Repeat(wheelTurn + moved / wheelRadius * Mathf.Rad2Deg, 360f);
        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i] != null) wheels[i].localRotation = wheelRest[i] * Quaternion.AngleAxis(wheelTurn, wheelAxis);
    }

    // While turning across a lane the car is something in the road: traffic stops for it.
    private void UpdateRoadBlock()
    {
        bool wanted = s < blockUntil;
        if (!wanted) { if (roadBlock != null) { StreetLife.RemoveRoadBlock(roadBlock); roadBlock = null; } return; }
        if (roadBlock == null)
            roadBlock = StreetLife.AddRoadBlock(transform.position, new Vector2(width * 0.5f + 0.25f, length * 0.5f + 0.3f),
                                                transform.eulerAngles.y, true, false, false, "Café car turning");
        roadBlock.center = transform.position;
        roadBlock.yaw = transform.eulerAngles.y;
    }

    private void ClearBlocks()
    {
        if (roadBlock != null) { StreetLife.RemoveRoadBlock(roadBlock); roadBlock = null; }
        if (holdBlock != null) { StreetLife.RemoveRoadBlock(holdBlock); holdBlock = null; }
    }

    // Anyone on foot inside the car's footprint a little way along its path.
    private bool PersonAhead(int gear, float lookAhead)
    {
        float probe = Mathf.Min(pathLength, s + lookAhead);
        for (float at = s; at <= probe + 1e-3f; at += 0.5f)
        {
            FootprintAt(Mathf.Min(at, pathLength), out Vector3 centre, out Vector3 forward, out Vector3 right);
            if (PersonInRect(centre, forward, right, length * 0.5f + 0.35f, width * 0.5f + 0.3f)) return true;
        }
        return false;
    }

    private bool PeopleIn(Vector4[] poses, int from, int to)
    {
        if (poses == null) return false;
        for (int i = from; i < to; i += 3)
        {
            float yaw = poses[i].w * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 centre = new Vector3(poses[i].x, poses[i].y, poses[i].z) + forward * centreOffset;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            if (PersonInRect(centre, forward, right, length * 0.5f + 0.3f, width * 0.5f + 0.3f)) return true;
        }
        return false;
    }

    private void FootprintAt(float at, out Vector3 centre, out Vector3 forward, out Vector3 right)
    {
        int i = 1;
        while (i < path.Count - 1 && path[i].at < at) i++;
        Pose a = path[i - 1], b = path[i];
        float span = b.at - a.at;
        float t = span > 1e-5f ? Mathf.Clamp01((at - a.at) / span) : 1f;
        float yaw = Mathf.LerpAngle(a.yaw, b.yaw, t) * Mathf.Deg2Rad;
        forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        right = new Vector3(forward.z, 0f, -forward.x);
        centre = Vector3.Lerp(a.position, b.position, t) + forward * centreOffset;
    }

    private static bool PersonInRect(Vector3 centre, Vector3 forward, Vector3 right, float halfLength, float halfWidth)
    {
        foreach (NpcJourney walker in NpcJourney.Active)
            if (walker != null && Inside(walker.transform.position, centre, forward, right, halfLength, halfWidth)) return true;
        foreach (CharacterController player in CafeArrivals.Players)
            if (player != null && player.enabled && Inside(player.transform.position, centre, forward, right, halfLength, halfWidth)) return true;
        return false;
    }

    private static bool Inside(Vector3 point, Vector3 centre, Vector3 forward, Vector3 right, float halfLength, float halfWidth)
    {
        Vector3 d = point - centre;
        if (Mathf.Abs(d.y) > 2f) return false;
        return Mathf.Abs(d.x * forward.x + d.z * forward.z) <= halfLength && Mathf.Abs(d.x * right.x + d.z * right.z) <= halfWidth;
    }
}
