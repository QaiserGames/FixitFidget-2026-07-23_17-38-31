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

    [Tooltip("Patrons must reach their own chair marker. A large stopping distance lets adjacent seats settle in the same aisle.")]
    [SerializeField, Range(0.01f, 0.2f)] private float seatStoppingDistance = 0.08f;

    [Tooltip("Within this of the stand point, still walking, the seating takes over and the walk carries on into " +
             "the chair (no stop, no idle, no hover). Pass 2.")]
    [SerializeField, Range(0.3f, 1f)] private float sitHandoverDistance = 0.7f;

    private NavMeshAgent agent;
    // How the body gets where this brain sends it (turning, walk clip, stalls
    // and their recovery). Shared with CustomerBrain since pass 1.
    private NpcLocomotion locomotion;
    // Sits them on the chair (optional; see NpcSeating).
    private NpcSeating seating;
    // Who they are as a mover, and what they do with their eyes and hands while
    // they sit (pass 2). Presentation only.
    private NpcSocial social;
    private Transform exitPoint;

    private State state = State.Entering;
    private WaitingSpot seat;
    private float leaveAt;
    private float bornAt;
    // Lower numbers win; customers top out at 95, while variation avoids a patron tie.
    private int priority;

    public bool IsSeated => state == State.Sitting;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        seating = GetComponent<NpcSeating>();
        locomotion = GetComponent<NpcLocomotion>();
        if (locomotion == null) locomotion = gameObject.AddComponent<NpcLocomotion>();
        social = GetComponent<NpcSocial>();
        if (social == null) social = gameObject.AddComponent<NpcSocial>();
        priority = Random.Range(96, 100);
    }

    public void Init(Transform exit)
    {
        exitPoint = exit;
        bornAt = Time.time;

        // A patron is nobody in particular: roll how they move and carry themselves.
        if (social != null && social.Profile == null) social.AssignProfile(GetComponent<CustomerIdentity>());
        NpcAttentionDirector.NotifyArrival(transform);

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
        locomotion.MoveTo(seat.StandPoint.position, NpcLocomotion.Move.To(seatStoppingDistance, priority, "seat"));
    }

    private void Update()
    {
        if (Time.time - bornAt > maxLifetime && state != State.Leaving)
        {
            Leave();
            return;
        }

        switch (state)
        {
            case State.Settling:
                if (social != null) social.Current = seat == null ? NpcSocial.Situation.StandingWait : NpcSocial.Situation.Walking;
                if (seat == null)
                {
                    if (Time.time >= leaveAt) Leave();
                    break;
                }

                if (locomotion.GaveUp) { Leave(); break; }

                // Close to the chair and still walking: hand the walk to the
                // seating now, so the last steps, the turn and the sit are one
                // movement rather than walk - stop - idle - hover - sit.
                if (seating != null && seat is TableSeat early && seating.CanSit(early) && locomotion.IsMoving
                    && locomotion.DistanceToGoal < sitHandoverDistance && !locomotion.HasArrived)
                {
                    float carried = locomotion.Speed;
                    locomotion.Park(null);
                    if (seating.TrySit(early, carried))
                    {
                        state = State.Sitting;
                        leaveAt = Time.time + Random.Range(minStay, maxStay);
                        break;
                    }
                    // Could not sit from here: finish the walk to the stand point as before.
                    locomotion.MoveTo(seat.StandPoint.position, NpcLocomotion.Move.To(seatStoppingDistance, priority, "seat"));
                }

                if (locomotion.HasArrived)
                {
                    state = State.Sitting;
                    leaveAt = Time.time + Random.Range(minStay, maxStay);

                    // Stand on the spot facing the table; then actually sit on
                    // the chair when the seat allows it. Leave() needs no
                    // change: the locomotion asks NpcSeating to stand them up
                    // before walking to the door.
                    locomotion.Park(seat.StandPoint.rotation);
                    if (seating != null && seat is TableSeat tableSeat) seating.TrySit(tableSeat);
                }
                break;

            case State.Sitting:
                if (social != null)
                    social.Current = seating != null && seating.IsSeated ? NpcSocial.Situation.Seated
                        : seating != null && seating.Busy ? NpcSocial.Situation.Walking : NpcSocial.Situation.StandingWait;
                // Seats can be switched off in the Inspector mid-run, and a
                // disabled spot clears its occupant — so re-check rather than
                // trusting the reference to still mean anything.
                if (seat == null || seat.Occupant != this) { Leave(); break; }
                if (Time.time >= leaveAt) Leave();
                break;

            case State.Leaving:
                if (social != null) social.Current = NpcSocial.Situation.Leaving;
                // Out of the door they walk back to their car or home (CafeArrivals
                // removes this brain there); without it they vanish at the door as
                // before. The lifetime backstop still removes one who can't get out.
                if (locomotion.HasArrived) { if (!CafeArrivals.TryDepart(gameObject)) Destroy(gameObject); }
                else if (locomotion.GaveUp && locomotion.DistanceToGoal <= CafeArrivals.DepartureRadius + 2f)
                {
                    // Boxed in within a couple of metres of the door: hand the
                    // walk over to the street from here (NpcJourney steers itself).
                    if (!CafeArrivals.TryDepart(gameObject)) Destroy(gameObject);
                }
                else if (Time.time - bornAt > maxLifetime + 20f) Destroy(gameObject);
                break;
        }
    }

    private void Leave()
    {
        if (state == State.Leaving) return;

        ReleaseSeat();
        state = State.Leaving;

        // Seated patrons stand up first: the locomotion asks NpcSeating.
        if (exitPoint != null)
            locomotion.MoveTo(CafeArrivals.DepartureTarget(exitPoint.position), NpcLocomotion.Move.Exit(priority));
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
}
