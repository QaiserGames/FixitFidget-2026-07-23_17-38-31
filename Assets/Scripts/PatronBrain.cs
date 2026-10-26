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
//
// THE LIFE PASS (12 Oct 2026; findings doc §8 and §9, his "yes"). Visual only, no balance change:
//   * patrons stroll (the relaxed walk) unless their own profile is a hurried one;
//   * a laptop patron (PatronSpawner's share) sits where a laptop lies open on the table (LI_Laptop_Open, placed off by
//     the Environment lead), switches it on and the tote bag at their feet (LI_ToteBag), looks at the screen while
//     "working" (NpcSocial.Prop), and stays exactly as long as any patron; without the props they are an ordinary patron;
//   * a pair (PatronSpawner): two who came in together take two seats of one table, chat sooner and more often
//     (NpcAttentionDirector.Companions) and leave together;
//   * their coffee: a cup appears on the table a few seconds after they sit (the one the café charged them for at the
//     door), goes with them when they get up, and into the three-bin station by the door (LI_BinStation) on the way out.
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

    [Header("The life pass (12 Oct 2026)")]
    [Tooltip("Patrons stroll in and out (the relaxed walk) unless their own profile is a hurried one. Off: their profile's walk.")]
    [SerializeField] private bool stroll = true;
    [Tooltip("A cup appears on their table this long after they sit (the coffee the café charged them for at the door), " +
             "and goes into the bins by the door when they leave. 0: no cup.")]
    [SerializeField, Range(0f, 30f)] private float cupAfterSeated = 6f;

    // THE ROOM KEEPS SOME FACES (14 Oct 2026, NPC wave 2). His girlfriend, playtest 5: "as for now it seems robotic". Day 1
    // ran as a conveyor: a patron every 12 s, to the nearest chair, 40-90 s, gone. Now someone working stays longer and a
    // pair catching up a little longer, so from above some tables keep their people while others turn over (and
    // PatronSpawner's arrivals come in clumps and lulls: Spawn Jitter 0.6). Presentation only: no money changes.
    [Header("How long they stay, by who they are (NPC wave 2)")]
    [Tooltip("A laptop patron at a laptop stays this many times as long: they're working. 2 = 80-180 s.")]
    [SerializeField, Range(1f, 4f)] private float laptopStay = 2f;
    [Tooltip("A pair stays this many times as long (the second leaves with the first). 1.3 = 52-117 s.")]
    [SerializeField, Range(1f, 3f)] private float pairStay = 1.3f;

    private NavMeshAgent agent;
    // How the body gets where this brain sends it (turning, walk clip, stalls
    // and their recovery). Shared with CustomerBrain since pass 1.
    private NpcLocomotion locomotion;
    // Sits them on the chair (optional; see NpcSeating).
    private NpcSeating seating;
    // Who they are as a mover, and what they do with their eyes and hands while
    // they sit (pass 2). Presentation only.
    private NpcSocial social;
    private NpcBeats beats;
    private Transform exitPoint;

    private State state = State.Entering;
    private WaitingSpot seat;
    private float leaveAt;
    private float bornAt;
    // Lower numbers win; customers top out at 95, while variation avoids a patron tie.
    private int priority;

    // The life pass.
    private bool laptopPatron;
    private Transform laptop, bag;
    private float seatedAt = -1f;
    private PatronBrain companion;
    private bool pairLeader;
    private TableSeat reservedSeat;
    private bool binStop;
    private Vector3 binMouth;
    private float binDeadline, walkOutAt = -1f, followAt = -1f;

    public bool IsSeated => state == State.Sitting;
    /// <summary>On the way out (or out of the door).</summary>
    public bool IsLeaving => state == State.Leaving;
    /// <summary>Through the door and started (Init has run).</summary>
    public bool HasComeIn => exitPoint != null;
    /// <summary>A laptop patron who found a laptop to work at (for the checks).</summary>
    public bool AtLaptop => laptop != null;
    public bool IsLaptopPatron => laptopPatron;
    /// <summary>The one they came in with (a pair), or null.</summary>
    public PatronBrain Companion => companion;
    public bool IsPairLeader => pairLeader && companion != null;
    /// <summary>Where they sit (or are going to sit), for the checks.</summary>
    public WaitingSpot Seat => seat;
    /// <summary>The day they came in on (DayClock.Day when they started), or -1 before. A seat still held by someone from
    /// another day belongs to a patron the morning is clearing away (PatronSpawner.SeatOpeningGuests).</summary>
    public int Day { get; private set; } = -1;
    /// <summary>A settled guest: already in their seat when the café opened (InitSeated), not through the door.</summary>
    public bool SettledGuest { get; private set; }
    /// <summary>The share of the day they leave at (StayUntil), or -1 for a stay in seconds.</summary>
    public float LeavesAtDay => untilDay;

    /// <summary>
    /// Stay exactly <paramref name="seconds"/> once seated, instead of the usual 40-90 s. Call before Init.
    /// </summary>
    public void StayFor(float seconds)
    {
        minStay = maxStay = Mathf.Max(1f, seconds);
    }

    /// <summary>
    /// Stay until the day is <paramref name="dayFraction"/> through (DayClock.NormalizedDay; the man at the bins' morning
    /// at a table, until about 1:30 PM: playtest 3), however long the walk to the seat took. Call before Init.
    /// The leave time is a share of the day's real length, so the 240 s lifetime cap doesn't apply while they stay (14 Oct
    /// 2026: at the 420 s day a settled guest's 0.55 is 231 s of clock, plus the intro's held seconds: at the cap). The cap's
    /// backstop for a body that can't get out still applies once they leave (Update).
    /// </summary>
    public void StayUntil(float dayFraction)
    {
        untilDay = Mathf.Clamp01(dayFraction);
        minStay = maxStay = 1e6f;
    }

    /// <summary>A remote worker: sits at a laptop when one lies free on a table, else an ordinary patron. Call before Init.</summary>
    public void BeLaptopPatron() => laptopPatron = true;

    /// <summary>
    /// Two who came in together (PatronSpawner): the leader finds two seats of one table and keeps one for the other;
    /// they chat sooner and leave together. Call on both before Init.
    /// </summary>
    public void PairWith(PatronBrain other, bool leader)
    {
        companion = other;
        pairLeader = leader;
    }

    /// <summary>The seat the pair's leader kept for this one (PatronSpawner / the leader). Call before Init.</summary>
    public void TakeReservedSeat(TableSeat kept) => reservedSeat = kept;

    // Leave when the day reaches this (StayUntil), or -1 for the stay in seconds.
    private float untilDay = -1f;
    // When they set off for the door (Leave), and how long someone who stayed until a share of the day may take to get out.
    private float leftAt = -1f;
    private const float StayUntilWayOut = 60f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        seating = GetComponent<NpcSeating>();
        locomotion = GetComponent<NpcLocomotion>();
        if (locomotion == null) locomotion = gameObject.AddComponent<NpcLocomotion>();
        social = GetComponent<NpcSocial>();
        if (social == null) social = gameObject.AddComponent<NpcSocial>();
        beats = GetComponent<NpcBeats>();
        priority = Random.Range(96, 100);
    }

    public void Init(Transform exit)
    {
        exitPoint = exit;
        bornAt = Time.time;
        Day = DayClock.Instance != null ? DayClock.Instance.Day : -1;

        // A patron is nobody in particular: roll how they move and carry themselves.
        if (social != null && social.Profile == null) social.AssignProfile(GetComponent<CustomerIdentity>());
        // Patrons stroll (12 Oct 2026): the relaxed walk, unless their own profile is the hurried sort, which keeps
        // some variety in the room.
        if (stroll && locomotion != null && !IsHurriedSort()) locomotion.RequestWalk(NpcLocomotion.Gait.Relaxed, .94f);
        NpcAttentionDirector.NotifyArrival(transform);
        if (companion != null && social != null && companion.social != null) NpcAttentionDirector.Companions(social, companion.social);

        TryTakeSeat();
    }

    /// <summary>
    /// A settled guest (14 Oct 2026, NPC wave 2; his girlfriend: "the cafe felt empty"): already in <paramref name="at"/>
    /// when the café opens, made there by PatronSpawner.SeatOpeningGuests. As Init, but nobody came through the door (no
    /// arrival notice, no payment: that is ComeIn's), the seat is theirs at once, they sit at once (NpcSeating.SitAtOnce),
    /// and their coffee is already on the table. A laptop guest's laptop at that seat opens as they sit. Call PairWith,
    /// BeLaptopPatron and StayUntil first, as for Init. False when they couldn't sit at once: then they are on their way
    /// to the same seat from its stand point (the ordinary sit), or, with the seat gone, an ordinary patron.
    /// <paramref name="sittingPhase"/>: where in the sitting idle they start (0-1), so the morning's guests never breathe
    /// in step; negative: at random.
    /// </summary>
    public bool InitSeated(Transform exit, TableSeat at, float sittingPhase = -1f)
    {
        exitPoint = exit;
        bornAt = Time.time;
        Day = DayClock.Instance != null ? DayClock.Instance.Day : -1;
        SettledGuest = true;
        if (social != null && social.Profile == null) social.AssignProfile(GetComponent<CustomerIdentity>());
        if (stroll && locomotion != null && !IsHurriedSort()) locomotion.RequestWalk(NpcLocomotion.Gait.Relaxed, .94f);
        if (companion != null && social != null && companion.social != null) NpcAttentionDirector.Companions(social, companion.social);

        if (at == null || WaitingArea.Instance == null || !(at.Occupant == this || at.Claim(this)))
        {
            TryTakeSeat();   // their seat went (nobody should hold it at the open): the best free one for them, as a patron would
            return false;
        }
        seat = at;
        if (laptopPatron) TakeTheLaptopAt(at);
        if (seating == null || !seating.SitAtOnce(at, sittingPhase))
        {
            // Not this frame (no sit states, or a body not ready): the ordinary sit from the seat's stand point, where they
            // were made, so it's done in about a second, under the intro's first shot.
            state = State.Settling;
            locomotion.MoveTo(seat.StandPoint.position, NpcLocomotion.Move.To(seatStoppingDistance, priority, "seat"));
            return false;
        }
        Sat();
        // The coffee is already on the table (TendTheTable puts it there on the first seated frame).
        seatedAt = Time.time - cupAfterSeated;
        return true;
    }

    // A laptop guest: the laptop lying at their seat (and the bag beside it) are theirs, as ClaimLaptopSeat keeps them;
    // Sat switches them on.
    private void TakeTheLaptopAt(TableSeat at)
    {
        Transform l = NpcLifeProps.LaptopFor(at);
        if (l == null || !NpcLifeProps.Reserve(l, this)) return;
        laptop = l;
        bag = NpcLifeProps.BagFor(at);
        if (bag != null && !NpcLifeProps.Reserve(bag, this)) bag = null;
    }

    private bool IsHurriedSort() => social != null && social.Profile != null
        && (social.Profile.Name.IndexOf("Hurried", System.StringComparison.OrdinalIgnoreCase) >= 0 || social.Profile.urgency > .75f);

    private void TryTakeSeat()
    {
        if (WaitingArea.Instance == null) { Leave(); return; }

        WaitingSpot spot = null;
        // The pair's second: the seat the leader kept for them, if it is still theirs.
        if (reservedSeat != null && reservedSeat.Occupant == this) spot = reservedSeat;
        // The pair's leader: two seats of one table, one kept for the other.
        if (spot == null && pairLeader && companion != null) spot = ClaimTableForTwo();
        // A laptop patron: a seat with a laptop lying free on its table, if there is one.
        if (spot == null && laptopPatron) spot = ClaimLaptopSeat();

        // Seat ONLY. Never falls back to a loiter spot the way a customer does
        // — those exist for people waiting on the player, and a patron standing
        // in one would push a real customer out of the calmest place to wait.
        if (spot == null) spot = WaitingArea.Instance.Claim(this, WaitingSpot.SpotKind.Seat);

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
        // Now and then someone walks to their table texting (the upper body only; the legs are the walk's).
        if (beats != null) beats.TryWalkingBeat(SecondsToWalk(seat.StandPoint.position), .8f);
    }

    // Two free seats of one table, nearest to the door first; the second is claimed for the companion, who takes it when
    // they come in. Null: no table for two. One table: seat poses within 1.65 m (the banquette 2-tops' chair and bench
    // sit 1.48 m apart, the reading corner's two chairs 1.5, two people across the communal table 1.6; the communal's
    // diagonal neighbours are 1.84 and next-but-one along a row 1.8, so those don't pass as a table). 1.4 m, the first
    // rule, left a pair only the bistro 2-tops and side-by-side stools: with those taken they sat 7 m apart (13 Oct).
    private WaitingSpot ClaimTableForTwo()
    {
        TableSeat[] seats = FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude);
        TableSeat bestA = null, bestB = null;
        float best = float.MaxValue;
        for (int i = 0; i < seats.Length; i++)
        {
            TableSeat a = seats[i];
            if (a == null || !a.IsAvailable || !WaitingArea.HasClaimClearance(a, this)) continue;
            for (int j = i + 1; j < seats.Length; j++)
            {
                TableSeat b = seats[j];
                if (b == null || !b.IsAvailable || !WaitingArea.HasClaimClearance(b, companion)) continue;
                Vector3 d = a.SeatPose.position - b.SeatPose.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1.65f * 1.65f || d.sqrMagnitude < .2f * .2f) continue;
                float toDoor = (a.StandPoint.position - transform.position).sqrMagnitude + (b.StandPoint.position - transform.position).sqrMagnitude;
                if (toDoor < best) { best = toDoor; bestA = a; bestB = b; }
            }
        }
        if (bestA == null) return null;
        if (!bestA.Claim(this)) return null;
        if (!bestB.Claim(companion)) { bestA.Release(this); return null; }
        companion.TakeReservedSeat(bestB);
        return bestA;
    }

    // A free seat with a laptop lying open (off) on its table that nobody else uses, nearest first; the laptop and the
    // bag beside the seat are theirs from here. Null: none free (an ordinary patron then).
    private WaitingSpot ClaimLaptopSeat()
    {
        if (NpcLifeProps.LaptopCount == 0) return null;
        TableSeat pick = null;
        Transform pickLaptop = null;
        float best = float.MaxValue;
        foreach (TableSeat s in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (s == null || !s.IsAvailable || !WaitingArea.HasClaimClearance(s, this)) continue;
            Transform l = NpcLifeProps.LaptopFor(s);
            if (l == null) continue;
            float d = (s.StandPoint.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; pick = s; pickLaptop = l; }
        }
        if (pick == null || !pick.Claim(this)) return null;
        // Theirs from here (nobody else takes that laptop); switched on when they sit (Sat).
        if (!NpcLifeProps.Reserve(pickLaptop, this)) { pick.Release(this); return null; }
        laptop = pickLaptop;
        bag = NpcLifeProps.BagFor(pick);
        if (bag != null) NpcLifeProps.Reserve(bag, this);
        return pick;
    }

    private float SecondsToWalk(Vector3 to)
    {
        Vector3 d = to - transform.position;
        d.y = 0f;
        float speed = locomotion != null ? Mathf.Max(.5f, locomotion.WalkSpeed) : 1.3f;
        return d.magnitude * 1.15f / speed;
    }

    private void Update()
    {
        // The lifetime cap, except for someone who stays until a share of the day (StayUntil): the day's length decides
        // their stay, and the cap's backstop takes over once they leave.
        if (untilDay < 0f && Time.time - bornAt > maxLifetime && state != State.Leaving)
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
                    && seating.ReadyToTakeOver(early, sitHandoverDistance) && !locomotion.HasArrived)
                {
                    float carried = locomotion.Speed;
                    locomotion.Park(null);
                    if (seating.TrySit(early, carried))
                    {
                        Sat();
                        break;
                    }
                    // Could not sit from here: finish the walk to the stand point as before.
                    locomotion.MoveTo(seat.StandPoint.position, NpcLocomotion.Move.To(seatStoppingDistance, priority, "seat"));
                }

                if (locomotion.HasArrived)
                {
                    Sat();

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
                TendTheTable();
                // A pair leaves together: the second's time is the leader's; when the leader gets up, so do they, a
                // step behind. A leader who went some other way (removed) leaves the other an ordinary stay.
                if (!pairLeader && companion != null && companion.state == State.Leaving && followAt < 0f) followAt = Time.time + Random.Range(.4f, 1.4f);
                if (!pairLeader && companion == null && leaveAt > Time.time + 1e5f) leaveAt = Time.time + Random.Range(minStay, maxStay) * .5f;
                if (followAt > 0f && Time.time >= followAt) { Leave(); break; }
                if (untilDay >= 0f ? DayClock.Instance == null || DayClock.Instance.NormalizedDay >= untilDay : Time.time >= leaveAt) Leave();
                break;

            case State.Leaving:
                if (social != null) social.Current = NpcSocial.Situation.Leaving;
                // The cup into the bins by the door first (12 Oct 2026), then the door.
                if (binStop)
                {
                    if (locomotion.HasArrived || locomotion.GaveUp || Time.time >= binDeadline) DropTheCup(locomotion.HasArrived);
                    break;
                }
                if (walkOutAt > 0f)
                {
                    if (Time.time < walkOutAt) break;
                    walkOutAt = -1f;
                    WalkToExit();
                    break;
                }
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
                // The backstop for a body that can't get out: 20 s past the lifetime cap, or for someone who stayed until a
                // share of the day (no cap while they stayed), a minute after they set off (the bins and the door take ~20 s).
                else if (untilDay < 0f ? Time.time - bornAt > maxLifetime + 20f : Time.time - leftAt > StayUntilWayOut) Destroy(gameObject);
                break;
        }
    }

    // In the chair from now: how long they stay, and the laptop open in front of them when they have one.
    private void Sat()
    {
        state = State.Sitting;
        seatedAt = Time.time;
        // Someone working at a laptop stays longer, a pair a little longer (14 Oct 2026: the room keeps some faces).
        float stay = laptop != null ? laptopStay : companion != null ? pairStay : 1f;
        leaveAt = Time.time + Random.Range(minStay, maxStay) * stay;
        // The pair's second stays with the leader: their time is the leader's, however the walk in went.
        if (companion != null && !pairLeader) leaveAt = Time.time + 1e6f;
        // The laptop opens and the bag goes down at their feet as they sit.
        if (laptop != null) NpcLifeProps.Use(laptop, this);
        if (bag != null) NpcLifeProps.Use(bag, this);
        if (laptop != null && social != null) social.Prop = laptop;
    }

    // Seated: the laptop open (it was switched on at the claim; the look at it is NpcSocial's), and after a while the
    // cup on the table, their coffee, picked up again when they go.
    private void TendTheTable()
    {
        if (cupAfterSeated <= 0f || seatedAt < 0f || !(seat is TableSeat table)) return;
        if (seating != null && seating.Current != NpcSeating.Phase.Seated) return;
        if (Time.time - seatedAt < cupAfterSeated) return;
        NpcCup cup = NpcCup.Of(gameObject);
        if (cup == null)
        {
            cup = NpcCup.Give(gameObject, null);
            cup.PutOnTable(table);
        }
    }

    private void Leave()
    {
        if (state == State.Leaving) return;

        ReleaseSeat();
        state = State.Leaving;
        leftAt = Time.time;
        if (social != null) social.Prop = null;
        if (laptop != null) { NpcLifeProps.Release(laptop, this); laptop = null; }
        if (bag != null) { NpcLifeProps.Release(bag, this); bag = null; }
        // The pair's leader going: the other sees it and gets up a moment later (Update).

        if (exitPoint == null) { Destroy(gameObject); return; }
        // Seated patrons stand up first: the locomotion asks NpcSeating. The cup comes with them, into the bins by
        // the door when there are bins they can reach.
        NpcCup held = NpcCup.Of(gameObject);
        binStop = false;
        walkOutAt = -1f;
        // 24 Oct 2026 (Days): about one guest in three leaves the cup on the table (Designer P's §2.3, clearing tables; their
        // movement profile's chance, Day 1's settled guests always): a used cup there, the seat out of play until Ace clears it.
        if (held != null && held.OnTable && NpcCup.LeavesItBehind(gameObject, SettledGuest) && held.LeaveOnTable()) held = null;
        if (held != null && held.Held)
        {
            held.TakeInHand();
            // Not when two people are already at the bins (a burst of patrons leaving at once: the cup goes home).
            if (NpcLifeProps.TryGetBin(out Vector3 drop, out Vector3 mouth) && !NpcLifeProps.BinBusy(locomotion) && Reachable(transform.position, drop))
            {
                binStop = true;
                binMouth = mouth;
                binDeadline = Time.time + 10f;
                locomotion.MoveTo(drop, NpcLocomotion.Move.To(.25f, priority, "bin"));
                return;
            }
        }
        WalkToExit();
    }

    private void WalkToExit()
    {
        if (exitPoint == null) { Destroy(gameObject); return; }
        locomotion.MoveTo(CafeArrivals.DepartureTarget(exitPoint.position), NpcLocomotion.Move.Exit(priority));
        if (beats != null) beats.TryWalkingBeat(SecondsToWalk(exitPoint.position), .5f);
    }

    private void DropTheCup(bool atTheBin)
    {
        binStop = false;
        NpcCup cup = NpcCup.Of(gameObject);
        if (atTheBin && cup != null && cup.Held)
        {
            Vector3 to = binMouth - transform.position;
            to.y = 0f;
            locomotion.Park(to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to.normalized, Vector3.up) : (Quaternion?)null);
            cup.DropInto(binMouth);
            walkOutAt = Time.time + .8f;
            return;
        }
        walkOutAt = Time.time + .05f;
    }

    private static bool Reachable(Vector3 from, Vector3 to)
    {
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(start.position, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
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
        if (reservedSeat != null && reservedSeat.Occupant == this) reservedSeat.Release(this);
        if (laptop != null) NpcLifeProps.Release(laptop, this);
        if (bag != null) NpcLifeProps.Release(bag, this);
    }
}
