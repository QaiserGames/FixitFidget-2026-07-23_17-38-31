using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// A PATRON
//
// The other population. occupancy-and-pacing.md splits the room in two:
//
//   Active customers  <= 6   Full CustomerBrain. Tab, patience, repair/drink.
//   Patrons          ~ 14    Walk in, take a seat, drink, chat, leave.
//                            No tab, no demand on your hands.
//
// This is the second one, and it has never existed. Every person in the shop
// so far has wanted something from the player, which is why an empty room and
// a stressful room have been the same room: six people all needing you is a
// job, fourteen people quietly existing is a CAFE.
//
// THEY ARE NOT DECORATION
//
// They take seats. A busy day therefore means the customer who actually needs
// you can't sit, falls through to a loiter spot, and drains at 1.15x instead
// of 0.6x. That's the cafe competing for your SPACE as well as your hands, and
// it's half a pressure model that has never fired once — 16 seats against 6
// customers means seats have never run out.
//
// DELIBERATELY NOT A CustomerBrain
//
// No counter slot, no queue, no ticket, no patience bar, no conversation, no
// job. Sharing CustomerBrain would mean every one of those systems learning to
// handle a person who wants nothing, and DayClock counts CustomerBrains to
// decide the day is over — fourteen of these would keep the day alive forever.
// ---------------------------------------------------------------------------

[RequireComponent(typeof(NavMeshAgent))]
public class PatronBrain : MonoBehaviour
{
    private enum State { Entering, Settling, Sitting, Leaving }

    [Header("Timing")]
    [Tooltip("How long they stay in their seat before leaving.")]
    [SerializeField] private float minStay = 40f;
    [SerializeField] private float maxStay = 90f;

    [Tooltip("If no seat is free they hang about briefly and go. They never " +
             "loiter properly — loiter spots belong to people who are WAITING " +
             "on you, and filling them with patrons would starve the customers " +
             "who need somewhere to stand.")]
    [SerializeField] private float noSeatLingerSeconds = 6f;

    [Header("Safety")]
    [Tooltip("Hard cap on a patron's whole life. Cheap insurance against one " +
             "getting wedged and standing in a seat for the rest of the day.")]
    [SerializeField] private float maxLifetime = 240f;

    [Tooltip("Seconds without getting closer along the route before trying a new path.")]
    [SerializeField] private float stallSeconds = 3f;
    [Tooltip("Patrons must reach their own chair marker. A large stopping distance lets adjacent seats settle in the same aisle.")]
    [SerializeField, Range(0.01f, 0.2f)] private float seatStoppingDistance = 0.08f;

    private NavMeshAgent agent;
    private Animator animator;
    // Sits them on the chair (optional; see NpcSeating).
    private NpcSeating seating;
    private Transform exitPoint;

    private State state = State.Entering;
    private WaitingSpot seat;
    private float leaveAt;
    private float bornAt;

    private float lastProgressAt;
    private float bestRemainingDistance = float.PositiveInfinity;
    private int unwedgeAttempts;
    private Vector3 destination;
    private bool walkingAnimation;
    private float walkingChangeTimer;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    public bool IsSeated => state == State.Sitting;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = Mathf.Clamp(seatStoppingDistance, 0.01f, 0.2f);
        animator = GetComponentInChildren<Animator>();
        seating = GetComponent<NpcSeating>();

        // Lower numbers win; customers top out at 95, while variation avoids a patron tie.
        agent.avoidancePriority = Random.Range(96, 100);
    }

    public void Init(Transform exit)
    {
        exitPoint = exit;
        bornAt = Time.time;
        lastProgressAt = Time.time;

        TryTakeSeat();
    }

    private void TryTakeSeat()
    {
        if (WaitingArea.Instance == null) { Leave(); return; }

        // Seat ONLY. Never falls back to a loiter spot the way a customer does
        // — those exist for people waiting on the player, and a patron standing
        // in one would push a real customer out of the calmest place to wait.
        WaitingSpot spot = WaitingArea.Instance.Claim(this, WaitingSpot.SpotKind.Seat);

        if (spot == null || spot.Kind != WaitingSpot.SpotKind.Seat)
        {
            if (spot != null) spot.Release(this);

            // Nowhere to sit. Hover a moment so it reads as someone looking
            // around and deciding against it, rather than a spawn that
            // instantly turns around.
            state = State.Settling;
            leaveAt = Time.time + noSeatLingerSeconds;
            return;
        }

        seat = spot;
        state = State.Settling;
        SetDestination(seat.StandPoint.position);
    }

    private void Update()
    {
        UpdateWalkingAnimation();

        if (Time.time - bornAt > maxLifetime && state != State.Leaving)
        {
            Leave();
            return;
        }

        switch (state)
        {
            case State.Settling:
                if (seat == null)
                {
                    if (Time.time >= leaveAt) Leave();
                    break;
                }

                WatchForWedging();
                if (state != State.Settling) break;

                if (Arrived())
                {
                    state = State.Sitting;
                    leaveAt = Time.time + Random.Range(minStay, maxStay);

                    if (agent.isOnNavMesh)
                    {
                        agent.ResetPath();
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        agent.avoidancePriority = 0;
                    }
                    // Actually sit on the chair when the seat allows it; stand
                    // facing the table otherwise. Leave() needs no change:
                    // NpcSeating stands them up when the exit path arrives.
                    if (seating == null || !(seat is TableSeat tableSeat) || !seating.TrySit(tableSeat))
                        FaceTable();
                }
                break;

            case State.Sitting:
                // Seats can be switched off in the Inspector mid-run, and a
                // disabled spot clears its occupant — so re-check rather than
                // trusting the reference to still mean anything.
                if (seat == null || seat.Occupant != this) { Leave(); break; }
                if (Time.time >= leaveAt) Leave();
                break;

            case State.Leaving:
                WatchForWedging();
                // Out of the door they walk back to their car or home (CafeArrivals
                // removes this brain there); without it they vanish at the door as
                // before. The lifetime backstop still removes one who can't get out.
                if (Arrived()) { if (!CafeArrivals.TryDepart(gameObject)) Destroy(gameObject); }
                else if (Time.time - bornAt > maxLifetime + 20f) Destroy(gameObject);
                break;
        }
    }

    private void FaceTable()
    {
        if (seat == null) return;

        Vector3 look = seat.StandPoint.forward;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
    }

    private void UpdateWalkingAnimation()
    {
        if (animator == null || agent == null) return;

        // Different start/stop speeds and a short hold prevent tiny avoidance
        // corrections from restarting the walk clip every other frame.
        bool canWalk = agent.isOnNavMesh && !agent.isStopped
            && (state == State.Settling || state == State.Leaving);
        float threshold = walkingAnimation ? 0.08f : 0.25f;
        bool wantsWalk = canWalk && agent.velocity.sqrMagnitude > threshold * threshold;
        if (wantsWalk == walkingAnimation) walkingChangeTimer = 0f;
        else
        {
            walkingChangeTimer += Time.deltaTime;
            if (!canWalk || walkingChangeTimer >= 0.12f)
            {
                walkingAnimation = wantsWalk;
                walkingChangeTimer = 0f;
            }
        }
        animator.SetBool(IsWalkingHash, walkingAnimation);
    }

    private void Leave()
    {
        if (state == State.Leaving) return;

        ReleaseSeat();
        state = State.Leaving;

        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;

        if (exitPoint != null) SetDestination(exitPoint.position);
        else Destroy(gameObject);
    }

    private void ReleaseSeat()
    {
        if (seat != null) seat.Release(this);
        seat = null;
    }

    // Releasing on destroy as well as on leaving, because a seat held by a
    // deleted patron is a chair nobody can ever sit in again — and with the day
    // reset destroying everyone, that would leak a seat per patron per day.
    private void OnDestroy()
    {
        ReleaseSeat();
    }

    private void SetDestination(Vector3 target)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.avoidancePriority = Random.Range(96, 100);
        destination = target;
        agent.SetDestination(target);
        lastProgressAt = Time.time;
        bestRemainingDistance = float.PositiveInfinity;
        unwedgeAttempts = 0;
    }

    private bool Arrived()
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        // Still getting up from a chair: the walk hasn't started yet.
        if (seating != null && seating.Busy) return false;
        if (agent.pathPending) return false;
        return agent.pathStatus == NavMeshPathStatus.PathComplete
            && Vector3.Distance(transform.position, destination) <= agent.stoppingDistance + 0.15f;
    }

    // Re-plan normally when crowded; never teleport a visible patron. Only
    // shortening the route counts as progress, so rocking from side to side
    // cannot keep a blocked seat claim alive indefinitely.
    private void WatchForWedging()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        if (agent.pathPending) return;

        float remaining = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete
            ? agent.remainingDistance : float.PositiveInfinity;
        if (!float.IsInfinity(remaining) && !float.IsNaN(remaining)
            && remaining < bestRemainingDistance - 0.15f)
        {
            bestRemainingDistance = remaining;
            lastProgressAt = Time.time;
            unwedgeAttempts = 0;
            return;
        }

        if (Time.time - lastProgressAt < stallSeconds) return;

        unwedgeAttempts = Mathf.Min(unwedgeAttempts + 1, 3);
        lastProgressAt = Time.time;

        if (unwedgeAttempts >= 3 && state != State.Leaving)
        {
            // Two ordinary re-paths did not help. Release the seat and try the
            // exit, keeping the existing overall lifetime backstop in Update.
            Leave();
            return;
        }

        // Retain the progress baseline and attempt count across a re-path.
        // Merely obtaining a fresh path must not reset the stall watchdog.
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(destination);
    }
}
