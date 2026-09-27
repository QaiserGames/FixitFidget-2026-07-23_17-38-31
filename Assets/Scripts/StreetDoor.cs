using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// A front door on the street that really opens (27 Sept; Mansoor: "lets now
// add doors for these houses. so we can see them leave and enter more
// believably").
//
// Built by Fixit Fidget > Night > Doors 2 (StreetDoorSteps): the house's own
// door leaf, glass, panel and knob on a hinge, a real doorway cut into the
// front wall and a short dark hall behind it. This component swings the leaf.
//
// People open it; nobody else does. CafeArrivals holds a door open while
// someone walking to or from it is in its hall or on its doorstep, and lets
// go once they are clear or inside: the door then closes by itself. Someone
// coming out waits in the dark hall until the door is open (NpcJourney gate);
// someone going in stands in the hall while it closes behind them, and only
// then leaves the game.
//
// Markers (children, not turned by the hinge):
//   Doorway: the middle of the opening at floor level, in the door's plane;
//   Hall:    where people stand inside, out of sight, before and after.
// The door's forward (blue axis) faces the street.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class StreetDoor : MonoBehaviour
{
    [Tooltip("The part that swings (the door leaf and what's on it). Turns about its own up axis.")]
    public Transform hinge;
    [Tooltip("Degrees the door swings in when fully open (inward, away from the street).")]
    [Range(30f, 110f)] public float openAngle = 88f;
    [Tooltip("Seconds from closed to fully open.")]
    [Range(.1f, 2f)] public float openSeconds = .35f;
    [Tooltip("Seconds from fully open to closed.")]
    [Range(.1f, 2f)] public float closeSeconds = .6f;
    [Tooltip("The middle of the doorway at floor level, in the door's plane.")]
    public Transform doorway;
    [Tooltip("Where people stand inside, out of sight, before coming out and after going in.")]
    public Transform hall;

    // Editor record (Fixit Fidget > Night > Doors - Put the old doors back): the
    // building's merged meshes the doorway was cut into, and what they were before.
    [HideInInspector] public MeshFilter[] cutParts = System.Array.Empty<MeshFilter>();
    [HideInInspector] public Mesh[] originalMeshes = System.Array.Empty<Mesh>();

    private static readonly List<StreetDoor> doors = new();
    private readonly Dictionary<Object, float> holds = new();
    private readonly List<Object> expired = new();
    private Quaternion closedRotation = Quaternion.identity;
    private bool hasClosedRotation;
    private float amount;       // 0 closed .. 1 fully open

    public static IReadOnlyList<StreetDoor> All => doors;

    /// <summary>0 when closed, 1 when fully open.</summary>
    public float OpenAmount => amount;
    /// <summary>Open far enough to walk through.</summary>
    public bool IsOpen => amount >= .9f;
    public bool IsClosed => amount <= .001f;
    /// <summary>Anyone holding it open right now.</summary>
    public bool Held => holds.Count > 0;

    public Vector3 DoorwayPoint => doorway != null ? doorway.position : transform.position;
    public Vector3 HallPoint => hall != null ? hall.position : transform.position - transform.forward;
    /// <summary>Towards the street, flat.</summary>
    public Vector3 Outward
    {
        get { Vector3 f = transform.forward; f.y = 0f; return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward; }
    }

    /// <summary>
    /// Metres in front of the door's plane (negative: inside the doorway or the hall).
    /// </summary>
    public float Outside(Vector3 point) => Vector3.Dot(point - DoorwayPoint, Outward);

    /// <summary>Keeps the door open for <paramref name="who"/> for the next few seconds (call again to keep it).</summary>
    public void Hold(Object who, float seconds = .3f)
    {
        if (who == null) return;
        holds[who] = Time.time + Mathf.Max(0f, seconds);
    }

    /// <summary><paramref name="who"/> no longer needs the door; it closes once nobody does.</summary>
    public void Release(Object who)
    {
        if (who != null) holds.Remove(who);
    }

    /// <summary>The door whose doorway is nearest <paramref name="point"/>, within <paramref name="within"/> metres; null if none.</summary>
    public static StreetDoor Near(Vector3 point, float within)
    {
        StreetDoor best = null;
        float bestSq = within * within;
        foreach (StreetDoor door in doors)
        {
            if (door == null || !door.isActiveAndEnabled) continue;
            Vector3 d = door.DoorwayPoint - point;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq <= bestSq) { bestSq = sq; best = door; }
        }
        return best;
    }

    private void Awake() => RememberClosed();

    private void RememberClosed()
    {
        if (hasClosedRotation || hinge == null) return;
        closedRotation = hinge.localRotation;
        hasClosedRotation = true;
    }

    private void OnEnable() { if (!doors.Contains(this)) doors.Add(this); }

    private void OnDisable()
    {
        doors.Remove(this);
        holds.Clear();
        amount = 0f;
        Pose();
    }

    private void Update()
    {
        if (hinge == null) return;
        RememberClosed();
        float now = Time.time;
        expired.Clear();
        foreach (KeyValuePair<Object, float> hold in holds)
            if (hold.Key == null || hold.Value < now) expired.Add(hold.Key);
        foreach (Object who in expired) holds.Remove(who);

        float target = holds.Count > 0 ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) return;
        float seconds = target > amount ? openSeconds : closeSeconds;
        amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.01f, seconds));
        Pose();
    }

    private void Pose()
    {
        if (hinge == null || !hasClosedRotation) return;
        // Quick near the frame, slow near fully open: opening is a push that swings on
        // and slows; closing starts gently and ends with the door shutting.
        float t = amount;
        float eased = 1f - (1f - t) * (1f - t);
        hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * eased, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.3f, .8f, 1f, .9f);
        Vector3 d = DoorwayPoint, h = HallPoint;
        Gizmos.DrawWireSphere(d, .08f);
        Gizmos.DrawWireSphere(h, .12f);
        Gizmos.DrawLine(d, h);
        Gizmos.DrawLine(d, d + Outward * 1.5f);
    }
}
