using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps café NPCs from walking into one another's bodies - or into Ace's.
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
///
/// ACE (pass 2). The NPC bodies no longer collide with the player's
/// CharacterController (they are on the NPC layer, which the Player layer
/// ignores), so a crowd can never push him about. In return the NPC does all
/// of the giving way: a body that touches Ace slips SIDEWAYS past him (across
/// its own direction of travel, like water round a rock) rather than bouncing
/// straight back, and a body standing still steps aside when he walks into it.
/// The push is capped per frame and clamped to the NavMesh, so nobody is
/// pushed through a wall; in a doorway too narrow to pass, the body brushes
/// through him for a moment instead of jamming for ever.
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
    [Tooltip("Radius kept clear round the player's body, metres (his controller is 0.5; a little overlap while brushing past is fine).")]
    [SerializeField, Range(.2f, .8f)] private float playerRadius = .45f;
    [Tooltip("Fastest an NPC standing still steps aside for the player, metres per second.")]
    [SerializeField, Range(.2f, 3f)] private float stillGiveWaySpeed = .9f;

    private static readonly List<PersonalSpace> active = new();
    private static Transform player;
    private static float playerSearchedAt = -10f;

    private NavMeshAgent agent;
    private NpcSeating seating;

    /// <summary>Room this person keeps beyond the body itself (a movement profile's personal space), metres.</summary>
    public float ExtraRadius { get; set; }
    public float BodyRadius => bodyRadius + ExtraRadius;
    /// <summary>Metres this body has been moved out of the player's way since it was enabled (for checks and traces).</summary>
    public float GaveWayToPlayer { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        player = null;
        playerSearchedAt = -10f;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        seating = GetComponent<NpcSeating>();
    }

    private void OnEnable() { active.Add(this); GaveWayToPlayer = 0f; }
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

    private static Transform Player
    {
        get
        {
            if (player == null && Time.time - playerSearchedAt > 2f)
            {
                playerSearchedAt = Time.time;
                PlayerMovement found = FindAnyObjectByType<PlayerMovement>();
                player = found != null ? found.transform : null;
            }
            return player;
        }
    }

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
            float apart = away.magnitude, wanted = BodyRadius + other.BodyRadius;
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
                float apart = away.magnitude, wanted = BodyRadius + walker.Radius;
                if (apart >= wanted) continue;
                Vector3 direction = apart > 1e-3f ? away / apart : transform.right;
                push += direction * (wanted - apart);
            }

        // Ace: the NPC does all of the giving way, sideways past him.
        Vector3 playerPush = GiveWayToPlayer(me, meStill);

        float dt = Time.deltaTime;
        Vector3 total = Vector3.ClampMagnitude(push, maxSlideSpeed * dt)
                        + Vector3.ClampMagnitude(playerPush, (meStill ? stillGiveWaySpeed : maxSlideSpeed) * dt);
        if (total.sqrMagnitude < 1e-10f) return;
        Vector3 before = transform.position;
        agent.Move(total);
        if (playerPush.sqrMagnitude > 1e-10f) GaveWayToPlayer += Vector3.Distance(before, transform.position);
    }

    private Vector3 GiveWayToPlayer(Vector3 me, bool meStill)
    {
        Transform ace = Player;
        if (ace == null) return Vector3.zero;
        Vector3 away = me - ace.position;
        if (Mathf.Abs(away.y) > 1.6f) return Vector3.zero;
        away.y = 0f;
        float apart = away.magnitude, wanted = BodyRadius + playerRadius;
        if (apart >= wanted) return Vector3.zero;
        float depth = wanted - apart;
        Vector3 radial = apart > 1e-3f ? away / apart : transform.right;
        if (meStill) return radial * depth;   // standing: simply step out of his way

        // Walking: slip round him across the direction of travel. The radial
        // component that opposes the walk would only bounce the body back into
        // the crowd behind it; the sideways part gets it past.
        Vector3 travel = agent.desiredVelocity;
        travel.y = 0f;
        if (travel.sqrMagnitude < 1e-4f) travel = agent.velocity;
        travel.y = 0f;
        if (travel.sqrMagnitude < 1e-4f) return radial * depth;
        travel.Normalize();
        Vector3 side = radial - travel * Vector3.Dot(radial, travel);
        if (side.sqrMagnitude < 1e-3f)
        {
            // Head on: pick the side away from his facing (people pass behind a
            // person who is looking the other way), else this body's own right.
            Vector3 right = Vector3.Cross(Vector3.up, travel);
            side = Vector3.Dot(right, ace.forward) <= 0f ? right : -right;
        }
        side.Normalize();
        // Mostly sideways, a little of the radial so a body dead ahead still separates.
        return (side * .85f + radial * .35f).normalized * depth;
    }

    private static int GetInstanceIDHash(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
}
