using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// PATRON SPAWNER
//
// Separate from CustomerSpawner on purpose. That one is about pressure — how
// fast work arrives against how fast you can clear it, and every number in it
// was derived from logged play. This one is about the room being full, which
// is a different question with different answers, and mixing them would mean
// every future pacing change accidentally being an atmosphere change.
//
// THE ONE RULE THAT KEEPS THIS FROM RUINING THE GAME
//
// Patrons stop arriving when free seats run low. A cafe so packed that paying
// customers can never sit is atmosphere beating gameplay — the loitering
// pressure is meant to be a squeeze the player can feel and respond to, not a
// permanent condition they can do nothing about.
//
// See reserveSeatsForCustomers below. It is the whole safety valve.
// ---------------------------------------------------------------------------

public class PatronSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private GameObject patronPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;

    /// <summary>The body patrons are made from. The night walk borrows it for the neighbours coming home (NightNeighbours).</summary>
    public GameObject PatronPrefab => patronPrefab;

    [Header("How many")]
    [Tooltip("Ceiling on patrons alive at once. occupancy-and-pacing.md sets " +
             "total occupancy at 20: ~6 customers plus ~14 patrons.")]
    [SerializeField] private int maxPatrons = 14;

    [Tooltip("Seconds between patron arrivals. Not tied to the customer " +
             "schedule — a cafe fills up on its own rhythm.")]
    [SerializeField] private float spawnInterval = 12f;

    [Range(0f, 1f)]
    [SerializeField] private float spawnJitter = 0.35f;

    [Header("⚠️ The safety valve")]
    [Tooltip("Seats always kept free for real customers. Patrons stop arriving " +
             "once free seats drop to this. Without it a full cafe means every " +
             "paying customer loiters at 1.15x forever, which is punishment " +
             "rather than pressure.")]
    [SerializeField] private int reserveSeatsForCustomers = 3;

    [Header("Money")]
    [Tooltip("What a patron pays on arrival. They bought a coffee at the " +
             "counter, conceptually — no interaction needed.\n\n" +
             "Small on purpose. At $5 a head, fourteen patrons is $70 against " +
             "a ~$700 day: enough that a full room feels good, never enough to " +
             "compete with repairs. The cafe's real payment is TIME, not money.")]
    [SerializeField] private int payPerPatron = 5;

    [Tooltip("Patrons do NOT consume beans or cups. A busy room sabotaging " +
             "your ability to serve the customers who matter would punish the " +
             "player for the cafe succeeding.")]
    [SerializeField] private bool consumesStock = false;

    [Header("Arrival")]
    [SerializeField] private float spawnScatter = 1.2f;
    [SerializeField] private float openingGraceMin = 3f;
    [SerializeField] private float openingGraceMax = 8f;

    [Header("The life pass (12 Oct 2026, visual only)")]
    [Tooltip("Share of patrons who are remote workers: they sit where a laptop lies open on a table (LI_Laptop_Open, " +
             "placed by the Environment lead, off until used), switch it on and the bag at their feet, and look at the " +
             "screen. They stay exactly as long as any patron. Without the props they are ordinary patrons.")]
    [SerializeField, Range(0f, 1f)] private float laptopShare = .25f;
    [Tooltip("Chance a patron comes in as a pair: a second one walks in just behind, they take two seats of one " +
             "table, chat, and leave together. Both count against Max Patrons and the seats kept for customers.")]
    [SerializeField, Range(0f, 1f)] private float pairChance = .3f;
    [Tooltip("The pair's second sets off for the door when the first is this close to it, metres (so they come " +
             "through the door together).")]
    [SerializeField, Range(2f, 20f)] private float pairFollowDistance = 8f;

    // SETTLED GUESTS (14 Oct 2026, NPC wave 2). His girlfriend, playtest 5: "the cafe felt empty". When the intro ends
    // (or the bell rings with it Off) a few people are already in: a reader in the reading corner, a pair talking at a
    // banquette 2-top, someone at the laptop on the communal table. They never need Ace: they came in before he opened
    // (no payment, no arrival), sit already seated (NpcSeating.SitAtOnce: no walk in through the door with Grace, who
    // arrives at 0 s), and leave over the morning at a share of the day's real length (PatronBrain.StayUntil), so seats
    // free up before lunch. Their seats are in the room's south half: none is a stool, and none is among the eight a
    // customer weighs from the counter. Days 1-3 only (the Producer's call 13); placed by Fixit Fidget > NPC > Wave 2.
    [System.Serializable]
    public class OpeningGuest
    {
        [Tooltip("Who they are, for the reports: a reader, a pair, a laptop worker.")]
        public string label = "";
        [Tooltip("Their seats: one for someone alone, two for a pair (the first leads, the second leaves with them).")]
        public TableSeat[] seats = System.Array.Empty<TableSeat>();
        [Tooltip("Another set of seats, taken on about half the mornings, and whenever the first set isn't free. Empty: none.")]
        public TableSeat[] alternative = System.Array.Empty<TableSeat>();
        [Tooltip("A remote worker: the laptop lying at their seat opens as they sit, their eyes on the screen.")]
        public bool laptop;
        [Tooltip("They leave when the day is this far through (DayClock.NormalizedDay): a share of the day's real length, " +
                 "so a longer day keeps them in proportion. At 420 s: 0.30 = 2:06, 0.40 = 2:48, 0.55 = 3:51.")]
        [Range(0f, 1f)] public float leaveAt = .3f;
        [Tooltip("The first and the last day they're there (DayClock.Day).")]
        public int firstDay = 1, lastDay = 3;
    }

    [Header("Settled guests (NPC wave 2)")]
    [Tooltip("People already seated when the café opens, who never need Ace. Filled by Fixit Fidget > NPC > Wave 2.")]
    [SerializeField] private OpeningGuest[] openingGuests = System.Array.Empty<OpeningGuest>();

    /// <summary>A lab's hand: settled guests in this lab session too (they are skipped in labs, as the intro is).</summary>
    public static bool ForceOpeningGuests { get; set; }
    /// <summary>For the checks: the settled guests seated this morning (with who they are), and the day they were seated on.</summary>
    public System.Collections.Generic.List<(PatronBrain guest, string label, TableSeat seat, float leaveAt)> OpeningGuestsSeated { get; } = new();
    public int OpeningGuestsDay => openingGuestsDay;
    /// <summary>The settled guests as authored (for the checks and the step's report).</summary>
    public OpeningGuest[] OpeningGuests => openingGuests;

    private int openingGuestsDay = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ForceOpeningGuests = false;

    private float timer;
    private int lastSeenDay = -1;
    // Pairs: the second of each waits (inactive) until the first is near the door, then walks the last metres in.
    private readonly System.Collections.Generic.List<(PatronBrain leader, PatronBrain second, float by)> pending = new();

    /// <summary>For the checks: pairs and laptop patrons sent in this session.</summary>
    public int PairsSent { get; private set; }
    public int LaptopPatronsSent { get; private set; }

    private float NextGap =>
        spawnInterval * Random.Range(1f - spawnJitter, 1f + spawnJitter);

    // The man at the bins by day (his visit after a skip, his note: session 3) lives beside the patrons.
    private void Awake()
    {
        if (GetComponent<LodgerDay>() == null) gameObject.AddComponent<LodgerDay>();
    }

    private void Update()
    {
        if (patronPrefab == null || spawnPoint == null) return;
        FollowPairs();

        DayClock clock = DayClock.Instance;

        // Last orders stops patrons too. The end of the day is supposed to be
        // "take care of everyone still here" — a stream of new faces arriving
        // after close would flatten that entirely.
        if (clock != null && !clock.IsOpen) return;

        if (clock != null && clock.Day != lastSeenDay)
        {
            lastSeenDay = clock.Day;
            // A pair's second still waiting unseen from yesterday never walks into today (13 Oct 2026, QA).
            for (int i = pending.Count - 1; i >= 0; i--) { if (pending[i].second != null) Destroy(pending[i].second.gameObject); }
            pending.Clear();
            timer = Random.Range(openingGraceMin, openingGraceMax);
            // The settled guests at the open (contract §7): here when the intro is Off (the bell rings and this is the
            // spawner's first frame of the day); with the intro on, DayIntro.HoldTheWorld has already seated them under its
            // first shot and this is a no-op (once a morning).
            SeatOpeningGuests();
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = NextGap;

        int living = FindObjectsByType<PatronBrain>(
            FindObjectsInactive.Exclude).Length;
        if (living >= maxPatrons) return;

        // The valve. Patrons still walking over (CafeArrivals) take their seat at
        // the door, so count them as seated already.
        int free = WaitingArea.FreeSeats - NpcJourney.OnTheWay(CafeArrivals.Kind.Patron) - pending.Count;
        if (free <= reserveSeatsForCustomers) return;

        // A pair now and then (12 Oct 2026): room for two, under the cap and the valve.
        bool pair = pairChance > 0f && Random.value < pairChance && living + 2 <= maxPatrons && free - 1 > reserveSeatsForCustomers;
        SpawnOne(pair);
        // Two came in at once: the next one waits a gap longer, so a day sees the same number of patrons (and the same
        // quiet income and cups from them) with or without pairs. Visual only.
        if (pair) timer += NextGap;
    }

    // One name "Spawn" only: the café lab finds it by reflection (CafeLabDirector), and a second overload would make
    // that lookup ambiguous.
    private void Spawn() => SpawnOne(false);

    private void SpawnOne(bool pair)
    {
        PatronBrain brain = Make(out GameObject go);
        if (brain == null) return;
        // A remote worker, now and then (never the second of a pair: pairs sit together).
        if (laptopShare > 0f && Random.value < laptopShare) { brain.BeLaptopPatron(); LaptopPatronsSent++; }

        if (pair)
        {
            PatronBrain second = Make(out GameObject other);
            if (second != null)
            {
                brain.PairWith(second, true);
                second.PairWith(brain, false);
                // The second waits unseen until the first is near the door, then walks the last metres in behind them
                // (CafeArrivals picks each walker's own route, so this is how two arrive as one).
                other.SetActive(false);
                pending.Add((brain, second, Time.time + 75f));
                PairsSent++;
            }
        }

        // They walk over from a car in the lot or a neighbour's door first
        // (CafeArrivals), and come in - take a seat, pay - at the door. Without
        // CafeArrivals they start at the door straight away, as before.
        if (CafeArrivals.TryArrive(go, CafeArrivals.Kind.Patron, () => ComeIn(brain))) return;
        ComeIn(brain);
    }

    // One patron body at the door, with its brain, not yet started.
    private PatronBrain Make(out GameObject go) => MakeAt(ScatteredSpawn(), spawnPoint.rotation, out go);

    // One patron body at a place (the door, or a settled guest's seat), with its brain, not yet started.
    private PatronBrain MakeAt(Vector3 at, Quaternion facing, out GameObject go)
    {
        go = Instantiate(patronPrefab, at, facing);

        // A patron prefab made by duplicating the customer prefab would still
        // carry a CustomerBrain, which would claim a counter slot, appear on the
        // ticket rail, and keep DayClock waiting for it. Strip it rather than
        // relying on remembering to delete the component by hand.
        CustomerBrain stray = go.GetComponent<CustomerBrain>();
        if (stray != null)
        {
            Debug.LogWarning("[PatronSpawner] The patron prefab has a CustomerBrain " +
                             "on it. Removing it at runtime — but fix the prefab, " +
                             "or patrons will keep half-behaving like customers.", this);
            Destroy(stray);
        }

        CustomerInteractable talkable = go.GetComponent<CustomerInteractable>();
        if (talkable != null) Destroy(talkable);

        PatronBrain brain = go.GetComponent<PatronBrain>();
        if (brain == null) brain = go.AddComponent<PatronBrain>();
        return brain;
    }

    // The pairs' seconds: each sets off when their first is near the door (or has come in), the last few metres of a
    // walking route, so the two come through the door together; straight in at the door when the arrivals system
    // can't place them, or when the first is gone.
    private void FollowPairs()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var (leader, second, by) = pending[i];
            if (second == null) { pending.RemoveAt(i); continue; }
            // After closing nobody new comes in (13 Oct 2026, QA): a second still waiting unseen is let go, not sent in.
            if (DayClock.Instance != null && !DayClock.Instance.IsOpen) { Destroy(second.gameObject); pending.RemoveAt(i); continue; }
            bool go = leader == null || leader.HasComeIn || Time.time >= by;
            if (!go)
            {
                Vector3 d = leader.transform.position - spawnPoint.position;
                d.y = 0f;
                go = d.magnitude <= pairFollowDistance;
            }
            if (!go) continue;
            pending.RemoveAt(i);
            second.gameObject.SetActive(true);
            PatronBrain s = second;
            if (CafeArrivals.TryArriveNearTheDoor(second.gameObject, CafeArrivals.Kind.Patron, 4f, () => ComeIn(s))) continue;
            ComeIn(s);
        }
    }

    /// <summary>For the checks: send one pair in now, straight through the door (no street walk).</summary>
    public PatronBrain SendPairNow()
    {
        if (patronPrefab == null || spawnPoint == null) return null;
        PatronBrain brain = Make(out _);
        PatronBrain second = Make(out _);
        if (brain == null || second == null) return brain;
        brain.PairWith(second, true);
        second.PairWith(brain, false);
        PairsSent++;
        ComeIn(brain);
        ComeIn(second);
        return brain;
    }

    /// <summary>For the checks: send one laptop patron in now, straight through the door.</summary>
    public PatronBrain SendLaptopPatronNow()
    {
        if (patronPrefab == null || spawnPoint == null) return null;
        PatronBrain brain = Make(out _);
        if (brain == null) return null;
        brain.BeLaptopPatron();
        LaptopPatronsSent++;
        ComeIn(brain);
        return brain;
    }

    // ------------------------------------------------------------ the settled guests (contract §7, 14 Oct 2026)

    /// <summary>
    /// The settled guests, seated now (contract §7): finds the scene's spawner and seats today's opening guests at once.
    /// Once a morning (a second call that day does nothing); nothing on a day with none, with the café closed, or in a lab
    /// session that didn't ask (ForceOpeningGuests). DayIntro.HoldTheWorld calls it one line before it holds the spawner,
    /// so they are in their seats under the intro's first shot; the spawner calls it at the open when the intro is Off.
    /// </summary>
    public static void SeatOpeningGuestsNow()
    {
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        if (spawner != null) spawner.SeatOpeningGuests();
    }

    /// <summary>Seats today's settled guests (see <see cref="SeatOpeningGuestsNow"/>). How many sat down at once.</summary>
    public int SeatOpeningGuests()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.IsOpen || clock.DayOver) return 0;
        // Off in a lab session unless the lab asks, as the intro is (not marked done, so a lab that asks later still gets them):
        // the whole-game playtest is the real game (DayIntro.LabWanted; review-npc-cine M1, 14 Oct).
        if (CafeLab.Active && !ForceOpeningGuests && !DayIntro.LabWanted) return 0;
        int day = clock.Day;
        if (openingGuestsDay == day) return 0;
        openingGuestsDay = day;
        OpeningGuestsSeated.Clear();
        if (patronPrefab == null || exitPoint == null || openingGuests == null || openingGuests.Length == 0) return 0;
        ReleaseStaleSeats(day);
        var today = new System.Collections.Generic.List<(OpeningGuest guest, TableSeat[] seats)>();
        int people = 0;
        foreach (OpeningGuest guest in openingGuests)
        {
            if (guest == null || day < guest.firstDay || day > guest.lastDay) continue;
            TableSeat[] seats = SeatsFor(guest);
            if (seats == null)
            {
                Debug.LogWarning($"[Settled guests] {guest.label}: none of their seats is free this morning, so they aren't in today.", this);
                continue;
            }
            today.Add((guest, seats));
            people += seats.Length;
        }
        // Their sitting idles spread round the cycle (four: a quarter apart, give or take an eighth), so no two of them,
        // placed in the same frame, ever breathe in step.
        float first = Random.value;
        int k = 0, sat = 0;
        foreach (var (guest, seats) in today)
            sat += SeatGuest(guest, seats, _ => Mathf.Repeat(first + (k++ + Random.Range(-.25f, .25f)) / Mathf.Max(1, people), 1f));
        return sat;
    }

    // Their seats this morning: the first set or the alternative, at random, and the other whenever one isn't free.
    private static TableSeat[] SeatsFor(OpeningGuest guest)
    {
        bool hasAlternative = guest.alternative != null && guest.alternative.Length > 0;
        bool alternativeFirst = hasAlternative && Random.value < .5f;
        TableSeat[] first = alternativeFirst ? guest.alternative : guest.seats;
        TableSeat[] second = alternativeFirst ? guest.seats : guest.alternative;
        if (AllFree(first)) return first;
        if (hasAlternative && AllFree(second)) return second;
        return null;
    }

    private static bool AllFree(TableSeat[] seats)
    {
        if (seats == null || seats.Length == 0) return false;
        foreach (TableSeat seat in seats)
            if (seat == null || !seat.isActiveAndEnabled || !seat.IsAvailable || !WaitingArea.HasClaimClearance(seat, null)) return false;
        return true;
    }

    // A seat still held by a patron from another day belongs to one this morning is clearing away (DayClock.StartDay
    // destroys yesterday's patrons at the end of its frame, and the open can come in that same frame): let it go now, so
    // today's guests can have it. Should that patron stay at all, it sees its seat gone and leaves.
    private static void ReleaseStaleSeats(int day)
    {
        foreach (TableSeat seat in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
            if (seat != null && seat.Occupant is PatronBrain patron && patron != null && patron.Day != day) seat.Release(patron);
    }

    // One guest, or a pair: made inside at their seats' stand points (never at the door: Grace walks in at 0 s on Day 1)
    // and sat down at once. They pay nothing (that is ComeIn's) and announce no arrival. How many sat at once.
    private int SeatGuest(OpeningGuest guest, TableSeat[] seats, System.Func<int, float> sittingPhase)
    {
        var made = new System.Collections.Generic.List<(PatronBrain brain, TableSeat seat)>();
        foreach (TableSeat seat in seats)
        {
            PatronBrain brain = MakeAt(seat.StandPoint.position, seat.StandPoint.rotation, out GameObject go);
            if (brain == null) continue;
            go.name = "Settled guest - " + guest.label + (seats.Length > 1 ? " " + (made.Count + 1) : "");
            made.Add((brain, seat));
        }
        if (made.Count == 0) return 0;
        PatronBrain leader = made[0].brain;
        if (made.Count > 1)
        {
            // A pair: they chat sooner and more often (NpcAttentionDirector.Companions), and the second leaves with the first.
            leader.PairWith(made[1].brain, true);
            made[1].brain.PairWith(leader, false);
        }
        if (guest.laptop) leader.BeLaptopPatron();
        leader.StayUntil(guest.leaveAt);
        int sat = 0;
        for (int i = 0; i < made.Count; i++)
        {
            var (brain, seat) = made[i];
            if (brain.InitSeated(exitPoint, seat, sittingPhase(i))) sat++;
            OpeningGuestsSeated.Add((brain, guest.label, seat, guest.leaveAt));
        }
        return sat;
    }

    /// <summary>
    /// The man at the bins comes in the morning after a skip (LodgerDay; claude/session-3-favours-stalling-officer.md §4;
    /// playtest 3): a patron in <paramref name="look"/>, on foot, who sits for <paramref name="stay"/> seconds, or, with
    /// <paramref name="untilDay"/> given, until the day is that far through (his table all morning); orders nothing and
    /// pays nothing. Null if there's no patron body to make him from.
    /// </summary>
    public GameObject SpawnTheMan(GameObject look, float stay, float untilDay = -1f)
    {
        if (patronPrefab == null || spawnPoint == null) return null;
        GameObject go = Instantiate(patronPrefab, ScatteredSpawn(), spawnPoint.rotation);
        go.name = "The man at the bins (his morning visit)";
        CustomerBrain stray = go.GetComponent<CustomerBrain>();
        if (stray != null) Destroy(stray);
        CustomerInteractable talkable = go.GetComponent<CustomerInteractable>();
        if (talkable != null) Destroy(talkable);
        // His own look: the walk-in outfits go before they wake (they would switch one on over it), and the city look
        // is his alone (it's kept for him: NightZeroSet).
        NpcVisualVariants outfits = go.GetComponent<NpcVisualVariants>();
        if (outfits != null)
        {
            outfits.enabled = false;
            Destroy(outfits);
        }
        PolygonNpcVisual visual = go.GetComponent<PolygonNpcVisual>();
        if (visual != null && look != null) visual.Configure(new[] { look }, 0f, 0, 0f);
        PatronBrain brain = go.GetComponent<PatronBrain>();
        if (brain == null) brain = go.AddComponent<PatronBrain>();
        if (untilDay >= 0f) brain.StayUntil(untilDay);   // the morning at his table (playtest 3): until the day is this far through
        else brain.StayFor(stay);
        // On foot, never out of a car (he lives in the bins); in at the door with no payment (he orders nothing).
        if (CafeArrivals.TryArriveOnFoot(go, CafeArrivals.Kind.Patron, () => { if (brain != null) brain.Init(exitPoint); })) return go;
        brain.Init(exitPoint);
        return go;
    }

    private void ComeIn(PatronBrain brain)
    {
        if (brain == null) return;
        brain.Init(exitPoint);

        if (payPerPatron > 0 && ShopEconomy.Instance != null)
        {
            // PLAYTEST 4 (10 Oct 2026, the first-hour playtester): "the money rises to $65 with '+$5' pops while
            // nobody is served". The café still earns from them, quietly: no "+$" pop over a patron, so the pops a
            // player sees are their own pay (a hand-back, a drink). The money and the day's record stay.
            ShopEconomy.Instance.AddMoney(payPerPatron);

            // Through DayClock as well, or the till climbs while "Earned today"
            // sits still and the recap quietly under-reports the day.
            if (DayClock.Instance != null) DayClock.Instance.RecordPatronIncome(payPerPatron);
        }

        if (consumesStock && ShopInventory.Instance != null)
            ShopInventory.Instance.TakeCup();
    }

    private Vector3 ScatteredSpawn()
    {
        if (spawnScatter <= 0f) return spawnPoint.position;

        Vector2 o = Random.insideUnitCircle * spawnScatter;
        Vector3 probe = spawnPoint.position + new Vector3(o.x, 0f, o.y);

        return NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas)
             ? hit.position
             : spawnPoint.position;
    }
}