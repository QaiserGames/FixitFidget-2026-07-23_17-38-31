using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Where café visitors come from and go back to.
///
/// WHY
/// Customers and patrons used to appear on the patio in front of the door and
/// vanish there again. Now every one of them comes from somewhere: out of a car
/// that just pulled into the café's lot across the street, or out of a neighbour's
/// front door down the block. They walk to the café - waiting at the kerbs for a
/// gap or a walk signal - and only when they reach the door does their brain start,
/// so everything that happens inside is exactly as before. When they leave they
/// walk back to their car (which then backs out and drives away) or home.
///
/// WHAT STAYS THE SAME
///  * Nothing inside the café changes: patience, queue slots, seats and money all
///    start at the door, where they always did.
///  * The spawners still decide who comes and when; they just keep room at the
///    counter and in the seats for people already walking over.
///  * If this component is missing or switched off, everything falls back to the
///    old appear-at-the-door behaviour.
///  * The Day 1 guided opening still starts at the door (its pacing is authored).
///
/// Cars are a small pool that drive in on their own schedule and wait parked with
/// the driver inside; when the café calls for an arrival and a car is waiting, that
/// driver gets out. Otherwise the arrival comes on foot. At closing, anyone still
/// on their way turns round (the café is closed) and waiting cars leave.
///
/// The paths, stalls and crossings are written by Fixit Fidget > Cafe parking lot.
/// </summary>
[DisallowMultipleComponent]
public sealed class CafeArrivals : MonoBehaviour
{
    public enum Kind { Customer, Patron }

    [Serializable]
    public sealed class Route
    {
        public string name = "";
        public Vector3[] points = Array.Empty<Vector3>();
        [Tooltip("One per segment (points[i] -> points[i + 1]): index into Crossings, or -1.")]
        public int[] crossingAtSegment = Array.Empty<int>();
        [Tooltip("One per segment: how far (metres) a walker may stray to the left of it without touching anything - " +
                 "a planter, a post, a parked car, the road. Measured by Fixit Fidget > Cafe parking lot > 3 - Measure walking room.")]
        public float[] roomLeft = Array.Empty<float>();
        [Tooltip("One per segment: the same to the right.")]
        public float[] roomRight = Array.Empty<float>();
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public sealed class Crossing
    {
        public string name = "";
        public Vector3 center;
        [Tooltip("x: half width across its own right, y: half length along its forward.")]
        public Vector2 halfSize = new Vector2(1.2f, 2.8f);
        public float yaw;
        [Tooltip("Junction phase of the traffic that drives over it; -1 = unsignalled (drivers give way).")]
        public int crossingTrafficPhase = -1;
        public bool keepClear = true;
    }

    [Serializable]
    public sealed class KeepClearBox
    {
        public string name = "";
        public Vector3 center;
        public Vector2 halfSize = new Vector2(2.8f, 2.8f);
        public float yaw;
    }

    [Serializable]
    public sealed class Stall
    {
        public string name = "";
        [Tooltip("From the end of the turn-in to the parked pose (x, y, z, yaw).")]
        public Vector4[] entry = Array.Empty<Vector4>();
        [Tooltip("From the parked pose back into the aisle, driven in reverse.")]
        public Vector4[] backOut = Array.Empty<Vector4>();
        [Tooltip("From the aisle to the exit, where the car waits for a gap.")]
        public Vector4[] toExit = Array.Empty<Vector4>();
        [Tooltip("True when manoeuvring in or out of this stall sweeps the spot where cars wait to leave.")]
        public bool nearExit;
        [Tooltip("The driver's walk from the car door to the lot's pedestrian gate.")]
        public Route walk = new Route();
    }

    [Header("The café door")]
    [SerializeField] private Transform door;
    [SerializeField, Min(0f)] private float doorScatter = 0.55f;

    [Header("On foot")]
    [SerializeField] private Route[] footRoutes = Array.Empty<Route>();

    [Header("Parking lot")]
    [SerializeField] private Stall[] stalls = Array.Empty<Stall>();
    [Tooltip("From the lot's pedestrian gate, over the zebra, to just outside the door.")]
    [SerializeField] private Route lotToDoor = new Route();
    [SerializeField] private string entryLane = "Through lane 3";
    [Tooltip("From the point where a car leaves the entry lane to the start of the aisle.")]
    [SerializeField] private Vector4[] turnInPath = Array.Empty<Vector4>();
    [SerializeField] private string exitLane = "Through lane 0";
    [Tooltip("From the exit (where cars wait for a gap) into the exit lane.")]
    [SerializeField] private Vector4[] turnOutPath = Array.Empty<Vector4>();
    [SerializeField] private CafeCar[] cars = Array.Empty<CafeCar>();

    [Header("Crossings")]
    [SerializeField] private Crossing[] crossings = Array.Empty<Crossing>();
    [Tooltip("Junction boxes: traffic never queues inside them (it waits until it can get across).")]
    [SerializeField] private KeepClearBox[] keepClearBoxes = Array.Empty<KeepClearBox>();

    [Header("Pacing")]
    [Tooltip("Cars driving in or parked with their driver waiting, kept ready for arrivals.")]
    [SerializeField, Range(0, 3)] private int carsWaitingTarget = 2;
    [SerializeField, Min(2f)] private float secondsBetweenCars = 9f;
    [SerializeField, Min(1f)] private float carSpeedInTraffic = 4.0f;
    [Tooltip("Walking speed range, metres per second.")]
    [SerializeField] private Vector2 walkSpeed = new Vector2(1.15f, 1.4f);
    [Tooltip("Backstop: a walk that takes longer than this finishes at once (and the check reports it).")]
    [SerializeField, Min(20f)] private float longestWalkSeconds = 120f;

    public static CafeArrivals Instance { get; private set; }
    /// <summary>The player's body, for cars that give way to people.</summary>
    public static readonly List<CharacterController> Players = new List<CharacterController>();

    /// <summary>
    /// Who came to the café today, from where, and where they went afterwards. For
    /// playtesting now (the live report prints it); later the raw material for Ace's
    /// nosiness - seeing which front door someone walks home to, or which car they
    /// drive, is exactly the kind of thing Ace finds out by day and follows up at night.
    /// </summary>
    public sealed class Comings
    {
        public string who = "";          // their name once the café knows it, else what they are
        public Kind kind;
        public string cameFrom = "";     // a front door, or the car park
        public string car = "";          // the car they came in, or ""
        public string wentTo = "";
        public float arrivedHour = -1f;  // café clock hours (DayClock.CurrentHour)
        public float leftHour = -1f;
    }

    private sealed class Visit
    {
        public Comings record;
        public Kind kind;
        public CafeCar car;
        public int footRoute = -1;
        public bool inside, leaving;
        public Action atDoor;
        public NpcJourney journey;
        public bool rootMotion;
        public readonly List<Collider> switchedOff = new List<Collider>();
    }

    private StreetCrossing[] live = Array.Empty<StreetCrossing>();
    private readonly List<StreetLife.RoadBlock> boxes = new List<StreetLife.RoadBlock>();
    private readonly Dictionary<GameObject, Visit> visits = new Dictionary<GameObject, Visit>();
    private readonly List<GameObject> scratch = new List<GameObject>();
    private CafeCar[] stallCar = Array.Empty<CafeCar>();
    private CafeCar lotUser, exitUser, announced;
    private float nextLaunchAt;
    private int lastDay = int.MinValue;
    private bool closedHandled;
    private float nextPlayerScan;
    private bool lanesResolved;
    private readonly List<Comings> today = new List<Comings>();
    private const int KeepRecords = 300;

    // Counters for checks and the handoff: how people actually arrived today.
    public int ArrivedByCar { get; private set; }
    public int ArrivedOnFoot { get; private set; }
    public int LeftByCar { get; private set; }
    public int LeftOnFoot { get; private set; }
    public int TurnedBack { get; private set; }
    public int CarsParked { get; private set; }
    public int CarsLeft { get; private set; }
    public int WalksCutShort { get; private set; }

    // ------------------------------------------------------------ for café cars

    internal string EntryLane => entryLane;
    internal string ExitLane => exitLane;
    internal Vector4[] TurnInPath => turnInPath;
    internal Vector4[] TurnOutPath => turnOutPath;
    internal float TurnInDistance { get; private set; }
    internal float JoinDistance { get; private set; }
    internal float CarSpeedInTraffic => carSpeedInTraffic;
    internal bool SkipLot { get; private set; }
    internal bool EntryExpected => announced != null && (announced.State == CafeCar.Phase.ToLot || announced.State == CafeCar.Phase.HeldAtLot);
    public IReadOnlyList<CafeCar> Cars => cars;
    public int StallCount => stalls.Length;
    public IReadOnlyList<StreetCrossing> LiveCrossings => live;
    /// <summary>Today's comings and goings, oldest first.</summary>
    public IReadOnlyList<Comings> Today => today;

    internal Vector4[] StallEntry(int stall) => stall >= 0 && stall < stalls.Length ? stalls[stall].entry : null;
    internal Vector4[] StallBackOut(int stall) => stall >= 0 && stall < stalls.Length ? stalls[stall].backOut : null;
    internal Vector4[] StallToExit(int stall) => stall >= 0 && stall < stalls.Length ? stalls[stall].toExit : null;
    private bool NearExit(CafeCar car) => car.Stall >= 0 && car.Stall < stalls.Length && stalls[car.Stall].nearExit;

    internal void AnnounceEntry(CafeCar car)
    {
        if (!EntryExpected) announced = car;
    }

    // One car manoeuvres in the lot at a time; a stall beside the exit also needs the exit clear.
    internal bool LotFreeFor(CafeCar car)
    {
        if (lotUser != null && lotUser != car) return false;
        if (car.State != CafeCar.Phase.WaitingToLeave && NearExit(car) && exitUser != null && exitUser != car) return false;
        if (car.State == CafeCar.Phase.WaitingToLeave && exitUser != null && exitUser != car) return false;
        return true;
    }

    internal void TakeLot(CafeCar car)
    {
        lotUser = car;
        if (announced == car) announced = null;
        // Leaving always ends at the exit; parking beside it needs it clear too.
        if (car.State == CafeCar.Phase.WaitingToLeave || NearExit(car)) exitUser = car;
    }

    internal void ReleaseLot(CafeCar car)
    {
        if (lotUser == car) lotUser = null;
    }

    internal bool ExitFreeFor(CafeCar car) => exitUser == null || exitUser == car;
    internal void TakeExit(CafeCar car) => exitUser = car;
    internal void ReleaseExit(CafeCar car) { if (exitUser == car) exitUser = null; }

    internal void CarParked(CafeCar car)
    {
        if (lotUser == car) lotUser = null;
        if (exitUser == car) exitUser = null;
        CarsParked++;
    }

    internal void StallVacated(CafeCar car)
    {
        if (car.Stall >= 0 && car.Stall < stallCar.Length && stallCar[car.Stall] == car) stallCar[car.Stall] = null;
    }

    internal void CarFinished(CafeCar car)
    {
        StallVacated(car);
        if (lotUser == car) lotUser = null;
        if (exitUser == car) exitUser = null;
        if (announced == car) announced = null;
        if (car.State == CafeCar.Phase.DrivingAway) CarsLeft++;
    }

    // ------------------------------------------------------------ lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CafeArrivals] More than one in the scene; this one is switched off.", this);
            enabled = false;
            return;
        }
        Instance = this;
        stallCar = new CafeCar[stalls.Length];
        foreach (CafeCar car in cars)
            if (car != null) car.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
        live = new StreetCrossing[crossings.Length];
        for (int i = 0; i < crossings.Length; i++)
        {
            Crossing c = crossings[i];
            live[i] = new StreetCrossing(c.name, c.center, c.halfSize, c.yaw, c.crossingTrafficPhase, c.keepClear);
        }
        boxes.Clear();
        foreach (KeepClearBox box in keepClearBoxes)
            boxes.Add(StreetLife.AddRoadBlock(box.center, box.halfSize, box.yaw, false, true, true, "Keep clear - " + box.name));
    }

    private void OnDisable()
    {
        foreach (StreetCrossing c in live) c?.Dispose();
        live = Array.Empty<StreetCrossing>();
        foreach (StreetLife.RoadBlock block in boxes) StreetLife.RemoveRoadBlock(block);
        boxes.Clear();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!lanesResolved) ResolveLanes();
        if (Time.unscaledTime >= nextPlayerScan)
        {
            nextPlayerScan = Time.unscaledTime + 2f;
            Players.Clear();
            Players.AddRange(FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude));
        }

        DayClock clock = DayClock.Instance;
        if (clock != null && clock.Day != lastDay)
        {
            if (lastDay != int.MinValue) NewDay();
            lastDay = clock.Day;
        }

        bool open = clock == null || clock.IsOpen;
        SkipLot = !open;
        if (!open && !closedHandled) { closedHandled = true; Closing(); }
        if (open) closedHandled = false;
        foreach (CafeCar car in cars)
        {
            if (car == null || car.State != CafeCar.Phase.Parked) continue;
            // Its visitor was removed some other way (a timeout, a new day): drive home empty.
            bool ownerGone = !ReferenceEquals(car.Owner, null) && car.Owner == null;
            // After closing, nobody is getting out any more.
            bool closedWaiting = !open && car.DriverWaiting && ReferenceEquals(car.Owner, null);
            if (ownerGone || closedWaiting) car.Leave();
        }
        if (open) LaunchCars();

        WatchWalks();
    }

    private void ResolveLanes()
    {
        StreetLife life = StreetLife.Main;
        if (life == null) return;
        bool entry = turnInPath.Length > 0 && life.TryRouteDistance(entryLane, Pose(turnInPath[0]), out float turn);
        bool exit = turnOutPath.Length > 0 && life.TryRouteDistance(exitLane, Pose(turnOutPath[turnOutPath.Length - 1]), out float join);
        if (!entry || !exit)
        {
            // No lanes to drive: everyone walks.
            if (cars.Length > 0) Debug.LogWarning("[CafeArrivals] The entry or exit lane isn't on the street; arrivals come on foot only.", this);
            cars = Array.Empty<CafeCar>();
            lanesResolved = true;
            return;
        }
        life.TryRouteDistance(entryLane, Pose(turnInPath[0]), out turn);
        life.TryRouteDistance(exitLane, Pose(turnOutPath[turnOutPath.Length - 1]), out join);
        TurnInDistance = turn;
        JoinDistance = join;
        lanesResolved = true;
    }

    private static Vector3 Pose(Vector4 p) => new Vector3(p.x, p.y, p.z);

    // ------------------------------------------------------------ spawner / brain hooks

    /// <summary>
    /// Sends a newly created visitor to walk to the door first. <paramref name="atDoor"/>
    /// runs when they get there (start the brain there). False: no arrival system,
    /// so the caller starts them at the door as before.
    /// </summary>
    public static bool TryArrive(GameObject npc, Kind kind, Action atDoor)
    {
        CafeArrivals arrivals = Instance;
        if (arrivals == null || !arrivals.isActiveAndEnabled || npc == null) return false;
        return arrivals.BeginArrival(npc, kind, atDoor);
    }

    /// <summary>
    /// Called by a brain that reached the exit: walks them back to their car or home
    /// instead of vanishing. False: the caller destroys them as before.
    /// </summary>
    public static bool TryDepart(GameObject npc)
    {
        CafeArrivals arrivals = Instance;
        if (arrivals == null || !arrivals.isActiveAndEnabled || npc == null) return false;
        return arrivals.BeginDeparture(npc);
    }

    // ------------------------------------------------------------ arriving

    private bool BeginArrival(GameObject npc, Kind kind, Action atDoor)
    {
        if (DayClock.Instance != null && !DayClock.Instance.IsOpen) return false;
        var path = new List<Vector3>();
        var cross = new List<StreetCrossing>();
        var left = new List<float>();
        var right = new List<float>();
        var visit = new Visit { kind = kind, atDoor = atDoor, record = new Comings { kind = kind, who = kind == Kind.Patron ? "a patron" : "a customer" } };

        CafeCar car = ReadyCar();
        if (car != null && car.Stall >= 0 && car.Stall < stalls.Length && stalls[car.Stall].walk.points.Length > 0 && lotToDoor.points.Length > 0)
        {
            Append(stalls[car.Stall].walk, false, path, cross, left, right);
            Append(lotToDoor, false, path, cross, left, right);
            car.DriverWaiting = false;
            car.Owner = npc;
            visit.car = car;
            visit.record.cameFrom = "the car park (" + stalls[car.Stall].name + ")";
            visit.record.car = car.name;
        }
        else
        {
            int route = PickFootRoute();
            if (route < 0) return false;
            Append(footRoutes[route], false, path, cross, left, right);
            visit.footRoute = route;
            visit.record.cameFrom = footRoutes[route].name;
        }
        AppendPoint(DoorPoint(), path, cross, left, right);

        StepOffTheFloor(npc, visit);
        visits[npc] = visit;
        Record(visit.record);
        NpcJourney journey = npc.GetComponent<NpcJourney>();
        if (journey == null) journey = npc.AddComponent<NpcJourney>();
        visit.journey = journey;
        journey.Begin(path.ToArray(), cross.ToArray(), left.ToArray(), right.ToArray(), RandomWalkSpeed(), true, kind, () => ReachedTheDoor(npc));
        if (visit.car != null) ArrivedByCar++; else ArrivedOnFoot++;
        return true;
    }

    private void ReachedTheDoor(GameObject npc)
    {
        if (npc == null || !visits.TryGetValue(npc, out Visit visit)) return;
        visit.inside = true;
        if (visit.record != null) visit.record.arrivedHour = ClockHour();
        StepOntoTheFloor(npc, visit);
        Action start = visit.atDoor;
        visit.atDoor = null;
        start?.Invoke();
    }

    private Vector3 DoorPoint()
    {
        Vector3 at = door != null ? door.position : transform.position;
        Vector2 scatter = UnityEngine.Random.insideUnitCircle * doorScatter;
        Vector3 probe = at + new Vector3(scatter.x, 0f, scatter.y * 0.5f);
        return NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : at;
    }

    // Off the NavMesh and out of the game until the door: brain, navigation,
    // targeting and the patience bar all off; only the body walks.
    private static void StepOffTheFloor(GameObject npc, Visit visit)
    {
        foreach (MonoBehaviour brain in Brains(npc)) brain.enabled = false;
        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        visit.switchedOff.Clear();
        foreach (Collider c in npc.GetComponentsInChildren<Collider>())
            if (c.enabled) { c.enabled = false; visit.switchedOff.Add(c); }
        HideOverheads(npc);
        Animator animator = npc.GetComponentInChildren<Animator>();
        if (animator != null) { visit.rootMotion = animator.applyRootMotion; animator.applyRootMotion = false; }
    }

    private static void StepOntoTheFloor(GameObject npc, Visit visit)
    {
        Vector3 at = npc.transform.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
        npc.transform.position = at;
        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(at);
        }
        foreach (Collider c in visit.switchedOff) if (c != null) c.enabled = true;
        visit.switchedOff.Clear();
        Animator animator = npc.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = visit.rootMotion;
        foreach (MonoBehaviour brain in Brains(npc)) brain.enabled = true;
    }

    private static IEnumerable<MonoBehaviour> Brains(GameObject npc)
    {
        CustomerBrain customer = npc.GetComponent<CustomerBrain>();
        if (customer != null) yield return customer;
        PatronBrain patron = npc.GetComponent<PatronBrain>();
        if (patron != null) yield return patron;
    }

    private static void HideOverheads(GameObject npc)
    {
        foreach (PatienceBar bar in npc.GetComponentsInChildren<PatienceBar>(true)) bar.gameObject.SetActive(false);
        foreach (JobMarker marker in npc.GetComponentsInChildren<JobMarker>(true)) marker.Hide();
        foreach (Transform child in npc.transform)
            if (child.name == "SpeechBubble") child.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ leaving

    private bool BeginDeparture(GameObject npc)
    {
        visits.TryGetValue(npc, out Visit visit);
        if (visit == null)
        {
            Kind kind = npc.GetComponent<PatronBrain>() != null ? Kind.Patron : Kind.Customer;
            visit = new Visit { kind = kind, inside = true, record = new Comings { kind = kind, cameFrom = "already inside (no walk in)" } };
            Record(visit.record);
        }
        visit.record ??= new Comings { kind = visit.kind };
        visit.record.who = NameOf(npc, visit.kind);
        var path = new List<Vector3> { npc.transform.position };
        var cross = new List<StreetCrossing>();
        var left = new List<float>();
        var right = new List<float>();
        Action done;
        CafeCar car = visit.car;
        if (car != null && car.Owner == npc && car.State == CafeCar.Phase.Parked
            && car.Stall >= 0 && car.Stall < stalls.Length && lotToDoor.points.Length > 0)
        {
            Append(lotToDoor, true, path, cross, left, right);
            Append(stalls[car.Stall].walk, true, path, cross, left, right);
            done = () => GetIn(npc, car);
            visit.record.wentTo = "their car (" + car.name + ")";
            LeftByCar++;
        }
        else
        {
            if (car != null && car.Owner == npc) car.Owner = null; // their car is gone: walk home
            visit.car = null;
            int route = visit.footRoute >= 0 ? visit.footRoute : PickFootRoute();
            if (route < 0) return false;
            Append(footRoutes[route], true, path, cross, left, right);
            done = () => Vanish(npc);
            visit.record.wentTo = footRoutes[route].name.Replace("From the ", "back to the ");
            LeftOnFoot++;
        }
        visit.record.leftHour = ClockHour();
        Retire(npc, visit);
        visit.inside = false;
        visit.leaving = true;
        visits[npc] = visit;
        NpcJourney journey = npc.GetComponent<NpcJourney>();
        if (journey == null) journey = npc.AddComponent<NpcJourney>();
        visit.journey = journey;
        journey.Begin(path.ToArray(), cross.ToArray(), left.ToArray(), right.ToArray(), RandomWalkSpeed(), false, visit.kind, done);
        return true;
    }

    private void Record(Comings record)
    {
        today.Add(record);
        if (today.Count > KeepRecords) today.RemoveAt(0);
    }

    private static float ClockHour() => DayClock.Instance != null ? DayClock.Instance.CurrentHour : -1f;

    // The name the café knows them by (a regular's, or the walk-in's generated one).
    private static string NameOf(GameObject npc, Kind kind)
    {
        CustomerIdentity identity = npc.GetComponent<CustomerIdentity>();
        if (identity != null && !string.IsNullOrEmpty(identity.DisplayName) && identity.DisplayName != "Customer") return identity.DisplayName;
        return kind == Kind.Patron ? "a patron" : "a customer";
    }

    // From visitor to passer-by: nothing about them is game any more. Destroying the
    // brain component runs its OnDestroy (seat, spot and conversation released), and
    // the spawners and day clock stop counting them - exactly as if they'd vanished.
    private static void Retire(GameObject npc, Visit visit)
    {
        foreach (Collider c in npc.GetComponentsInChildren<Collider>()) c.enabled = false;
        visit.switchedOff.Clear();
        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        HideOverheads(npc);
        foreach (Interactable interactable in npc.GetComponents<Interactable>()) Destroy(interactable);
        CustomerStoryteller storyteller = npc.GetComponent<CustomerStoryteller>();
        if (storyteller != null) Destroy(storyteller);
        foreach (MonoBehaviour brain in Brains(npc)) { brain.enabled = false; Destroy(brain); }
        Animator animator = npc.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
    }

    private void GetIn(GameObject npc, CafeCar car)
    {
        visits.Remove(npc);
        if (npc != null) Destroy(npc);
        if (car != null && car.State == CafeCar.Phase.Parked) car.Leave();
    }

    private void Vanish(GameObject npc)
    {
        visits.Remove(npc);
        if (npc != null) Destroy(npc);
    }

    // ------------------------------------------------------------ the day

    // Last orders: anyone still walking over turns round, waiting cars go home.
    private void Closing()
    {
        scratch.Clear();
        foreach (var pair in visits) scratch.Add(pair.Key);
        foreach (GameObject npc in scratch)
        {
            if (npc == null || !visits.TryGetValue(npc, out Visit visit)) continue;
            if (visit.inside || visit.leaving || visit.journey == null || !visit.journey.isActiveAndEnabled) continue;
            Retire(npc, visit);
            visit.leaving = true;
            if (visit.record != null) { visit.record.wentTo = "turned back (the café closed)"; visit.record.leftHour = ClockHour(); }
            TurnedBack++;
            CafeCar car = visit.car;
            if (car != null && car.Owner == npc) visit.journey.TurnBack(false, () => GetIn(npc, car));
            else visit.journey.TurnBack(false, () => Vanish(npc));
        }
    }

    // DayClock.StartDay has already removed yesterday's customers and patrons.
    // Clear the walkers and cars that were theirs.
    private void NewDay()
    {
        foreach (var pair in visits)
            if (pair.Key != null) Destroy(pair.Key);
        visits.Clear();
        foreach (CafeCar car in cars)
            if (car != null && car.State != CafeCar.Phase.Pooled) car.ReturnToPool();
        Array.Clear(stallCar, 0, stallCar.Length);
        lotUser = exitUser = announced = null;
        closedHandled = false;
        nextLaunchAt = Time.time + 2f;
        ArrivedByCar = ArrivedOnFoot = LeftByCar = LeftOnFoot = TurnedBack = CarsParked = CarsLeft = WalksCutShort = 0;
        today.Clear();
    }

    // Backstop against a walk that never ends (someone boxed in for good).
    private void WatchWalks()
    {
        scratch.Clear();
        foreach (var pair in visits)
        {
            if (pair.Key == null) { scratch.Add(pair.Key); continue; }
            Visit visit = pair.Value;
            if (visit.journey == null || !visit.journey.isActiveAndEnabled || visit.journey.Age < longestWalkSeconds) continue;
            scratch.Add(pair.Key);
        }
        foreach (GameObject npc in scratch)
        {
            if (npc == null) { visits.Remove(npc); continue; }
            Visit visit = visits[npc];
            WalksCutShort++;
            Debug.LogWarning($"[CafeArrivals] {npc.name} took over {longestWalkSeconds:0}s to walk " +
                             (visit.leaving ? "away" : "to the door") + "; finishing the walk at once.", npc);
            visit.journey.enabled = false;
            if (visit.leaving)
            {
                CafeCar car = visit.car;
                if (car != null && car.Owner == npc) GetIn(npc, car); else Vanish(npc);
            }
            else
            {
                npc.transform.position = DoorPoint();
                ReachedTheDoor(npc);
            }
        }
    }

    // ------------------------------------------------------------ cars

    private CafeCar ReadyCar()
    {
        CafeCar best = null;
        foreach (CafeCar car in cars)
            if (car != null && car.State == CafeCar.Phase.Parked && car.DriverWaiting && car.Owner == null
                && (best == null || car.PhaseAge > best.PhaseAge)) best = car;
        return best;
    }

    private int CarsWaitingOrComing()
    {
        int count = 0;
        foreach (CafeCar car in cars)
        {
            if (car == null) continue;
            if (car.State == CafeCar.Phase.ToLot || car.State == CafeCar.Phase.HeldAtLot || car.State == CafeCar.Phase.TurningIn) count++;
            else if (car.State == CafeCar.Phase.Parked && car.DriverWaiting && car.Owner == null) count++;
        }
        return count;
    }

    private void LaunchCars()
    {
        if (!lanesResolved || cars.Length == 0 || stalls.Length == 0 || Time.time < nextLaunchAt) return;
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return;
        if (CarsWaitingOrComing() >= carsWaitingTarget) return;
        // Stalls away from the exit first: they never wait on a car leaving.
        int stall = -1;
        for (int pass = 0; pass < 2 && stall < 0; pass++)
            for (int i = 0; i < stalls.Length; i++)
                if (stallCar[i] == null && stalls[i].nearExit == (pass == 1)) { stall = i; break; }
        if (stall < 0) return;
        CafeCar pick = null;
        int pooled = 0;
        foreach (CafeCar car in cars)
            if (car != null && car.State == CafeCar.Phase.Pooled && UnityEngine.Random.Range(0, ++pooled) == 0) pick = car;
        if (pick == null) return;
        if (pick.Launch(this, StreetLife.Main, entryLane, carSpeedInTraffic, stall))
        {
            stallCar[stall] = pick;
            nextLaunchAt = Time.time + secondsBetweenCars * UnityEngine.Random.Range(0.8f, 1.4f);
        }
        else nextLaunchAt = Time.time + 1f; // lane start busy: try again shortly
    }

    // ------------------------------------------------------------ paths

    private int PickFootRoute()
    {
        float total = 0f;
        foreach (Route route in footRoutes) if (route != null && route.points.Length > 1) total += route.weight;
        if (total <= 0f) return -1;
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < footRoutes.Length; i++)
        {
            Route route = footRoutes[i];
            if (route == null || route.points.Length < 2) continue;
            roll -= route.weight;
            if (roll <= 0f) return i;
        }
        return footRoutes.Length - 1;
    }

    private float RandomWalkSpeed() => UnityEngine.Random.Range(Mathf.Min(walkSpeed.x, walkSpeed.y), Mathf.Max(walkSpeed.x, walkSpeed.y));

    // Adds a route's points (and, per segment, its crossing and the room either side;
    // walking a route backwards swaps its left and right).
    private void Append(Route route, bool reversed, List<Vector3> path, List<StreetCrossing> cross, List<float> left, List<float> right)
    {
        if (route == null || route.points.Length == 0) return;
        int n = route.points.Length;
        for (int k = 0; k < n; k++)
        {
            int i = reversed ? n - 1 - k : k;
            if (path.Count > 0)
            {
                // Segment from the previous point to this one: inside the route it may be
                // a crossing; the joint between two routes never is.
                StreetCrossing c = null;
                float l = JointRoom, r = JointRoom;
                if (k > 0)
                {
                    int segment = reversed ? i : i - 1;
                    c = CrossingFor(route, segment);
                    float segmentLeft = RoomOf(route.roomLeft, segment), segmentRight = RoomOf(route.roomRight, segment);
                    l = reversed ? segmentRight : segmentLeft;
                    r = reversed ? segmentLeft : segmentRight;
                }
                if ((path[path.Count - 1] - route.points[i]).sqrMagnitude < 1e-4f && k == 0) continue; // same joint point
                cross.Add(c);
                left.Add(l);
                right.Add(r);
            }
            path.Add(route.points[i]);
        }
    }

    private static void AppendPoint(Vector3 point, List<Vector3> path, List<StreetCrossing> cross, List<float> left, List<float> right)
    {
        if (path.Count > 0) { cross.Add(null); left.Add(JointRoom); right.Add(JointRoom); }
        path.Add(point);
    }

    // Where the path data says nothing (a route measured before walking room existed,
    // the step between two routes, the last metres to the door).
    private const float JointRoom = 0.4f;

    // (Negative room: that side of the line grazes something; see NpcJourney.)
    private static float RoomOf(float[] room, int segment) =>
        room != null && segment >= 0 && segment < room.Length ? room[segment] : JointRoom;

    private StreetCrossing CrossingFor(Route route, int segment)
    {
        if (segment < 0 || segment >= route.crossingAtSegment.Length) return null;
        int index = route.crossingAtSegment[segment];
        return index >= 0 && index < live.Length ? live[index] : null;
    }

#if UNITY_EDITOR
    /// <summary>Written by Fixit Fidget > Cafe parking lot (editor only).</summary>
    public void EditorConfigure(Transform doorPoint, Route[] foot, Stall[] lotStalls, Route lotWalk,
                                string inLane, Vector4[] turnIn, string outLane, Vector4[] turnOut,
                                CafeCar[] carPool, Crossing[] crossingSetup, KeepClearBox[] boxes)
    {
        door = doorPoint;
        footRoutes = foot ?? Array.Empty<Route>();
        stalls = lotStalls ?? Array.Empty<Stall>();
        lotToDoor = lotWalk ?? new Route();
        entryLane = inLane;
        turnInPath = turnIn ?? Array.Empty<Vector4>();
        exitLane = outLane;
        turnOutPath = turnOut ?? Array.Empty<Vector4>();
        cars = carPool ?? Array.Empty<CafeCar>();
        crossings = crossingSetup ?? Array.Empty<Crossing>();
        keepClearBoxes = boxes ?? Array.Empty<KeepClearBox>();
    }

    public Route[] EditorFootRoutes => footRoutes;
    public Stall[] EditorStalls => stalls;
    public Route EditorLotToDoor => lotToDoor;
    public Vector4[] EditorTurnIn => turnInPath;
    public Vector4[] EditorTurnOut => turnOutPath;
    public Crossing[] EditorCrossings => crossings;
#endif

    // ------------------------------------------------------------ for checks

    public int WalkingIn(Kind kind) => NpcJourney.OnTheWay(kind);

    public int WalkingOut
    {
        get
        {
            int count = 0;
            foreach (var pair in visits) if (pair.Key != null && pair.Value.leaving) count++;
            return count;
        }
    }

    public int StallsTaken
    {
        get
        {
            int count = 0;
            foreach (CafeCar car in stallCar) if (car != null) count++;
            return count;
        }
    }
}
