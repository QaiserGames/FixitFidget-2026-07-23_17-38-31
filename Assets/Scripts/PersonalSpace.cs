using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps café NPCs from walking into one another's bodies.
///
/// WHY
/// Navigation avoidance (the NavMeshAgent's own) steers people round each
/// other, but it is advice, not a wall: two people heading for neighbouring
/// chairs, a queue that shuffles forward, or a customer who has been stuck
/// long enough to "push through" can still end up standing inside each other,
/// which reads as two bodies merging into one.
///
/// WHAT IT DOES
/// After everything else has moved the NPC this frame, it checks the other
/// NPCs on their feet. When two bodies overlap it slides this one apart along
/// the navigation mesh (never through a wall or a counter), a little per frame
/// so it reads as a sidestep rather than a snap:
///  * a walker bumping into someone standing still does all of the giving way;
///  * two walkers share it;
///  * two people standing still are left alone - they are on spots the café
///    placed them on (queue slots, waiting spots), and shoving them off those
///    would break the brains' bookkeeping.
/// Seated NPCs are skipped (the chair owns their body), and nothing here
/// changes where anyone is going. Visitors walking in from outside (NpcJourney)
/// count as bodies too: a café NPC on the move slides clear of them.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(80)] // after the brains and NpcSeating (60) have moved the body
public sealed class PersonalSpace : MonoBehaviour
{
    [Tooltip("Radius of the visible body, metres. Two NPCs are kept at least the sum of their radii apart.")]
    [SerializeField, Range(.15f, .6f)] private float bodyRadius = .28f;
    [Tooltip("Fastest the body is slid apart, metres per second.")]
    [SerializeField, Range(.2f, 4f)] private float maxSlideSpeed = 1.6f;
    [Tooltip("An NPC that wants to move slower than this (metres per second) counts as standing still.")]
    [SerializeField, Range(.02f, .5f)] private float stillSpeed = .12f;

    private static readonly List<PersonalSpace> active = new();

    private NavMeshAgent agent;
    private NpcSeating seating;

    public float BodyRadius => bodyRadius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        seating = GetComponent<NpcSeating>();
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    // On its feet and moving under navigation (not sitting, not parked by a chair).
    private bool OnItsFeet =>
        agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.updatePosition
        && (seating == null || !seating.Busy);

    // Standing still = not trying to go anywhere. Judged by where the agent WANTS
    // to go, so two walkers that have ground to a halt against each other still
    // count as walkers and get separated.
    private bool IsStill =>
        agent == null || agent.isStopped || !agent.hasPath
        || agent.desiredVelocity.sqrMagnitude < stillSpeed * stillSpeed;

    private void LateUpdate()
    {
        if (!OnItsFeet) return;
        bool meStill = IsStill;
        Vector3 me = transform.position;
        Vector3 push = Vector3.zero;
        for (int i = 0; i < active.Count; i++)
        {
            PersonalSpace other = active[i];
            if (other == this || other == null || !other.OnItsFeet) continue;
            Vector3 away = me - other.transform.position;
            if (Mathf.Abs(away.y) > 1.2f) continue;
            away.y = 0f;
            float apart = away.magnitude, wanted = bodyRadius + other.bodyRadius;
            if (apart >= wanted) continue;
            bool otherStill = other.IsStill;
            float share = meStill ? 0f : (otherStill ? 1f : .5f);
            if (share <= 0f) continue;
            // Exactly on top of each other: split along a stable sideways direction.
            Vector3 direction = apart > 1e-3f ? away / apart
                : (GetInstanceIDHash(this) < GetInstanceIDHash(other) ? transform.right : -transform.right);
            push += direction * ((wanted - apart) * share);
        }
        // Visitors walking to or from the door (off the NavMesh, see NpcJourney) steer
        // round the café's people themselves; a café NPC on the move still never ends
        // up inside one of them.
        if (!meStill)
            foreach (NpcJourney walker in NpcJourney.Active)
            {
                if (walker == null || !walker.isActiveAndEnabled || walker.gameObject == gameObject) continue;
                Vector3 away = me - walker.transform.position;
                if (Mathf.Abs(away.y) > 1.2f) continue;
                away.y = 0f;
                float apart = away.magnitude, wanted = bodyRadius + walker.Radius;
                if (apart >= wanted) continue;
                Vector3 direction = apart > 1e-3f ? away / apart : transform.right;
                push += direction * (wanted - apart);
            }
        if (push.sqrMagnitude < 1e-8f) return;
        agent.Move(Vector3.ClampMagnitude(push, maxSlideSpeed * Time.deltaTime));
    }

    private static int GetInstanceIDHash(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
}
