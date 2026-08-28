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

    [Tooltip("How long to be stuck before trying to shake loose.")]
    [SerializeField] private float stallSeconds = 3f;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform exitPoint;

    private State state = State.Entering;
    private WaitingSpot seat;
    private float leaveAt;
    private float bornAt;

    private Vector3 lastPosition;
    private float lastProgressAt;
    private int unwedgeAttempts;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    public bool IsSeated => state == State.Sitting;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    public void Init(Transform exit)
    {
        exitPoint = exit;
        bornAt = Time.time;
        lastPosition = transform.position;
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
        if (animator != null && agent != null)
            animator.SetBool(IsWalkingHash, agent.velocity.sqrMagnitude > 0.05f);

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

                if (Arrived())
                {
                    state = State.Sitting;
                    leaveAt = Time.time + Random.Range(minStay, maxStay);

                    if (agent.isOnNavMesh) agent.isStopped = true;
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
                if (Arrived() || Time.time - bornAt > maxLifetime + 20f) Destroy(gameObject);
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
        agent.SetDestination(target);
        lastProgressAt = Time.time;
        lastPosition = transform.position;
    }

    private bool Arrived()
    {
        if (agent == null || !agent.isOnNavMesh) return true;
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + 0.15f;
    }

    // Twenty agents in one room is where avoidance gets hard — the occupancy
    // doc flags it. This is the cheap version of CustomerBrain's unwedging
    // ladder: nudge, then give up and go home rather than standing in a
    // doorway for the rest of the day.
    private void WatchForWedging()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if ((transform.position - lastPosition).sqrMagnitude > 0.01f)
        {
            lastPosition = transform.position;
            lastProgressAt = Time.time;
            unwedgeAttempts = 0;
            return;
        }

        if (Time.time - lastProgressAt < stallSeconds) return;

        unwedgeAttempts++;
        lastProgressAt = Time.time;

        if (unwedgeAttempts >= 3)
        {
            // A patron who can't get where they're going is worth nothing and
            // is occupying a seat claim. Cut them loose.
            Leave();
            if (state == State.Leaving && exitPoint == null) Destroy(gameObject);
            return;
        }

        Vector2 nudge = Random.insideUnitCircle.normalized * 0.6f;
        Vector3 probe = transform.position + new Vector3(nudge.x, 0f, nudge.y);

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }
}
