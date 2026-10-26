using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE THRIFT SHELF BY THE BENCH: YOUR SIDE PROJECT (24 Oct 2026; Designer P's spec §2.2, §5.2 and §6.3, approved by him with
// his answers of 24 Oct)
//
// His answers: p1 "a cleaning job, so Day 1 brings no new tool"; p2 "one at a time: bought at closing, fixed the next day, sold
// at closing"; p6, a customer reaching the counter while Ace is on it: "the close-up closes by itself".
//
// What the player does (§2.2): the shelf at the bench's end holds today's find (Day 1: a thrift pocket watch with grime on its
// glass, free). E on the shelf ("Work on your thrift watch") flies it onto the bench's own corner (ItemSlotArea's side slot,
// outside the customers' count) and opens the close-up; the bench's card, glow and controls are the usual ones. Stepping back
// leaves it on its corner with its pieces in its own cups (BenchStage keeps each job's apart). Fixed ("Fixed. It sells
// tonight." on the card), it goes back on the shelf with a tick over it. At closing it sells by itself (SaveManager's closing
// checkpoint calls SettleAtClosing before it reads the till): Perfect for its price, Good for 80 % of it; untouched, barely
// touched or in pieces, it waits for tomorrow as it is. The recap's Shop then offers tomorrow's find (TomorrowsOffer): the
// next day's authored one (DayDefinition.thriftFind, thriftBuy), or past the authored days one of the jobs taught, never the
// same two nights running.
//
// How it never blocks a customer (§2.2): its own corner, so a customer's device always has the bench's main place; when someone
// reaches the counter, the close-up closes itself once any hold in progress has finished (GiveWay); it is never on the Now line
// while anyone needs Ace (Downtime), and it has no patience and no review. A customer's job never closes by itself.
//
// It is offered (Offered, for the Now line and Downtime) once the day's teaching has begun: on Day 1 once Grace's visit is over,
// on other days once the day's first customer has been heard, so it never competes with the first card.
//
// The save (SaveData.thrift): which find is on the shelf, for which day, and whether it was fixed; never its screws. An older
// save (or a lab's) has none, and gets the day's own authored find. Placed by Fixit Fidget > Days > Props 1 (the shelf, its
// find spot and the bench's side slot); nothing in the scene changes while playing.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class ThriftShelf : Interactable
{
    public static ThriftShelf Instance { get; private set; }

    [Tooltip("Where the find rests on the shelf. Empty: a child named \"Find spot\", else the shelf itself.")]
    [SerializeField] private Transform findSpot;
    [Tooltip("The bench's slots, with the side slot (the find's corner). Empty: the ItemSlotArea in the scene that has one.")]
    [SerializeField] private ItemSlotArea bench;

    /// <summary>How long the find's flight to the bench's corner (and back to the shelf) takes, seconds.</summary>
    public const float FlySeconds = .3f;
    /// <summary>The longest the close-up waits for a hold in progress before it closes itself for a customer (§2.2: "at most about 1 s").</summary>
    public const float GiveWayAtMost = 1.2f;
    /// <summary>A find sells at closing at this grade or better (Good: 80 % of its price); less waits for tomorrow.</summary>
    public const JobGrade SellsFrom = JobGrade.Good;
    /// <summary>The find's tape on the bench card: a thrift-shop tan (it has no customer, so no customer's colour).</summary>
    public static readonly Color Tape = new Color(.74f, .55f, .33f);

    /// <summary>
    /// The jobs taught, for the finds past the authored days (§5.2's "Later" row): the device's name, its fault's label, the
    /// buy and the sale. One a night, never the same two nights running. The circuit's sale is P's rule on the prefab's real
    /// payout ($45: $56 at Perfect, so $32), not his guess.
    /// </summary>
    public static readonly (string device, string label, int buy, int sale)[] Rotation =
    {
        ("Phone", "Gunked Up, Won't Charge", 10, 30),
        ("Pocket Watch", "Debris Inside, Won't Tell Time", 20, 55),
        ("Phone", "Cracked Screen", 25, 75),
        ("Phone", "Frozen Boot Sequence - Reconnect Signal", 10, 32),
        ("Pocket Watch", "Seized Movement", 35, 110),
    };

    /// <summary>Tomorrow's find in the Shop: what it is, its device and fault, what it costs and sells for, and whether it's bought.</summary>
    public sealed class Offer
    {
        public GameObject prefab;
        public int fault, buy, sale;
        public string thing = "", words = "";
        public bool bought;
    }

    private ThriftSaveData record = new ThriftSaveData();
    private JobBase find;
    private SideProject marker;
    private int day = -1, settledDay = -1;
    private bool heardToday, finishedToday, flying, restored;
    private float heardCheckAt, giveWaySince = -1f;
    private int tickBadge;

    /// <summary>The find today (on the shelf, on the bench's corner or in the close-up), or null.</summary>
    public JobBase Find => find;
    /// <summary>What Ace calls the find ("watch"), or "".</summary>
    public string Thing => marker != null ? marker.thing : "";
    /// <summary>The find sits on the bench's corner (its side slot).</summary>
    public bool OnTheCorner => find != null && Bench != null && Bench.Holds(find);
    /// <summary>The find is in the bench's close-up now.</summary>
    public bool InTheCloseUp => find != null && ItemInspector.Instance != null && ItemInspector.Instance.FocusedItem == find;
    /// <summary>The find sits on the shelf.</summary>
    public bool OnTheShelf => find != null && !flying && !OnTheCorner && !InTheCloseUp;
    /// <summary>The find is fixed: it sells tonight.</summary>
    public bool Finished => find != null && find.IsFinished;
    /// <summary>When the close-up last closed itself for a customer (Time.time; -1 never): the lab's measure.</summary>
    public float GaveWayAt { get; private set; } = -1f;
    /// <summary>How many times today the close-up closed itself for a customer.</summary>
    public int GaveWayToday { get; private set; }

    /// <summary>The day's teaching has begun: Day 1 once Grace's visit is over, other days once the first customer was heard.</summary>
    public bool TeachingStarted
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (clock == null) return false;
            if (clock.Day <= 1)
            {
                CustomerSpawner s = Downtime.Spawner;
                return s == null || s.FeaturedVisitOver;
            }
            return heardToday;
        }
    }

    /// <summary>
    /// The find may be offered now (the Now line, Downtime): there is one, unfinished, not on its way anywhere, the day still
    /// running, and the day's teaching has begun.
    /// </summary>
    public bool Offered
    {
        get
        {
            DayClock clock = DayClock.Instance;
            return find != null && !find.IsFinished && !flying && clock != null && !clock.DayOver && TeachingStarted
                   && (NightWalk.Instance == null || !NightWalk.Instance.Active);
        }
    }

    /// <summary>A find sold at this day's closing: for how much and what it was (the recap's Tonight); 0 when none.</summary>
    public int SoldOn(int dayNumber) => record != null && record.soldDay == dayNumber ? record.soldFor : 0;
    public string SoldThing => record != null ? record.soldThing ?? "" : "";
    public string SoldGrade => record != null ? record.soldGrade ?? "" : "";

    /// <summary>What sold at this day's closing, for the recap ("Sold your thrift watch: +$55"), or "" when nothing did.</summary>
    public static string SoldLine(int dayNumber)
    {
        ThriftShelf shelf = Instance;
        int sold = shelf != null ? shelf.SoldOn(dayNumber) : 0;
        if (sold <= 0) return "";
        return $"Sold your thrift {(string.IsNullOrEmpty(shelf.SoldThing) ? "find" : shelf.SoldThing)}: +${sold}";
    }

    /// <summary>What the finds made at this day's closing (for the takings shown on the recap), 0 when nothing sold.</summary>
    public static int SoldFor(int dayNumber) => Instance != null ? Instance.SoldOn(dayNumber) : 0;

    // ------------------------------------------------------------------ the bench's corner

    private ItemSlotArea Bench
    {
        get
        {
            if (bench != null && bench.SideSlot != null) return bench;
            foreach (ItemSlotArea area in FindObjectsByType<ItemSlotArea>(FindObjectsInactive.Exclude))
                if (area != null && area.SideSlot != null) { bench = area; return bench; }
            return null;
        }
    }

    private Transform Spot
    {
        get
        {
            if (findSpot != null) return findSpot;
            Transform named = transform.Find("Find spot");
            return named != null ? named : transform;
        }
    }

    // ------------------------------------------------------------------ the day

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    // Priority 0, as the bench's own things: the shelf stands at the bench's end, inside the bench's reach, so anything higher
    // would take E from a customer's device on the bench. The find on the shelf offers nothing itself (ItemInteractable: a
    // find is worked on only from the bench's corner), so the shelf has no rival for its own E.
    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (tickBadge != 0) Juice.Unmark(tickBadge);
    }

    private void Start() => Restore();

    // The save's record (SaveManager's Loaded): read once, before the first morning.
    private void Restore()
    {
        if (restored) return;
        restored = true;
        SaveData loaded = SaveManager.Instance != null ? SaveManager.Instance.Loaded : null;
        record = loaded != null && loaded.thrift != null ? loaded.thrift.Copy() : new ThriftSaveData();
        // A closed day being resumed (the recap): its find was settled at that closing; nothing more to do tonight.
        if (loaded != null && loaded.dayCompleted) settledDay = loaded.day;
    }

    private void Update()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) return;
        if (!restored) Restore();
        if (clock.Day != day) Morning(clock);
        if (clock.DayOver) return;
        WatchTheFirstCustomer();
        WatchTheFinish();
        GiveWay();
    }

    // A new day: today's find on the shelf (bought last night, still waiting from an earlier day, or for a save from before the
    // finds the day's own), and the day's teaching not yet begun.
    private void Morning(DayClock clock)
    {
        day = clock.Day;
        heardToday = false;
        finishedToday = false;
        giveWaySince = -1f;
        GaveWayToday = 0;
        if (clock.DayOver) return;   // the recap of a closed day: tomorrow's morning puts the find out
        if (!record.known)
        {
            // A new game, an older save or a lab's: the day's own authored find, on the shelf at the open (Day 1's is free).
            record.known = true;
            FeaturedRepairRequest authored = AuthoredFind(day, out DayDefinition def);
            if (authored != null && record.fault < 0)
            {
                record.prefab = authored.devicePrefab.name;
                record.fault = authored.faultIndex;
                record.forDay = day;
                record.sale = def.thriftSale;
                record.fixedIt = false;
            }
        }
        if (find == null && record.fault >= 0 && record.forDay <= day) Spawn();
        if (find != null && find.IsFinished) finishedToday = true;
        UpdateTick();
    }

    // The day's first customer heard (or their mind made up), four times a second until it is.
    private void WatchTheFirstCustomer()
    {
        if (heardToday || Time.time < heardCheckAt) return;
        heardCheckAt = Time.time + .25f;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (c != null && (c.WasAccepted || c.CanDecide)) { heardToday = true; return; }
    }

    // Fixed: counted once, and back on the shelf with its tick once the close-up lets it go.
    private void WatchTheFinish()
    {
        if (find == null) return;
        if (find.IsFinished && !finishedToday)
        {
            finishedToday = true;
            record.fixedIt = true;
            Downtime.Today.findsFinished++;
        }
        if (find.IsFinished && !flying && OnTheCorner && !InTheCloseUp) StartCoroutine(Fly(toBench: false, null));
        UpdateTick();
    }

    // His p6: someone at the counter while Ace is on his find: the close-up closes by itself once any hold in progress has
    // finished (a screw half turned, a part in the tweezers, a flip), at most GiveWayAtMost later. A customer's repair never
    // closes by itself.
    private void GiveWay()
    {
        if (!InTheCloseUp || Downtime.AtTheCounter == null) { giveWaySince = -1f; return; }
        if (giveWaySince < 0f) giveWaySince = Time.time;
        ItemInspector bench = ItemInspector.Instance;
        bool holding = bench.HoldTarget != null || bench.GrabTarget != null || bench.Flipping;
        if (holding && Time.time - giveWaySince < GiveWayAtMost) return;
        bench.CancelInspection();
        GaveWayAt = Time.time;
        GaveWayToday++;
        giveWaySince = -1f;
    }

    private void UpdateTick()
    {
        bool want = find != null && find.IsFinished && OnTheShelf;
        if (want && tickBadge == 0) tickBadge = Juice.Mark(FocusNameUI.TopOf(find.gameObject) + Vector3.up * .12f, Juice.Icon.Tick);
        else if (!want && tickBadge != 0) { Juice.Unmark(tickBadge); tickBadge = 0; }
    }

    // ------------------------------------------------------------------ E on the shelf

    public override bool IsAvailable
    {
        get
        {
            DayClock clock = DayClock.Instance;
            if (find == null || !OnTheShelf || find.IsFinished || flying) return false;
            if (clock == null || clock.DayOver || NightWalk.Instance != null && NightWalk.Instance.Active) return false;
            if (ItemInspector.Instance != null && ItemInspector.Instance.IsHoldingItem) return false;
            ItemSlotArea area = Bench;
            return area != null && area.SideSlotFree(find);
        }
    }

    public override string Prompt => "Work on your thrift " + (string.IsNullOrEmpty(Thing) ? "find" : Thing);

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        StartCoroutine(Fly(toBench: true, player));
    }

    // To the bench's corner and into the close-up, or (fixed) back onto the shelf: a short arc, then where it rests.
    private IEnumerator Fly(bool toBench, PlayerInteractor player)
    {
        if (find == null) yield break;
        ItemSlotArea area = Bench;
        Transform to = toBench ? area != null ? area.ClaimSlot(find) : null : Spot;
        if (to == null) yield break;
        if (!toBench && area != null) area.ReleaseSlot(find);
        flying = true;
        Sfx.Play("item.pickup", find.transform.position, .7f);
        Vector3 from = find.transform.position;
        Quaternion turnFrom = find.transform.rotation;
        Vector3 land = to.position + Vector3.up * find.restHeight;
        for (float t = 0f; t < 1f && find != null; t += Time.deltaTime / FlySeconds)
        {
            float e = 1f - (1f - t) * (1f - t);
            find.transform.position = Vector3.Lerp(from, land, e) + Vector3.up * (.12f * 4f * t * (1f - t));
            find.transform.rotation = Quaternion.Slerp(turnFrom, to.rotation, e);
            yield return null;
        }
        flying = false;
        if (find == null) yield break;
        PlacementJitter.Apply(find, to, 0f, 0f);
        Sfx.Play("item.putdown", find.transform.position, .7f);
        UpdateTick();
        if (!toBench) yield break;
        ItemInteractable item = find.GetComponentInChildren<ItemInteractable>();
        if (player != null && item != null) player.WorkOn(item);   // the close-up opens; if it can't, it waits on its corner
    }

    // ------------------------------------------------------------------ the find itself

    // Today's find as a device on the shelf: the prefab with its fault, nobody's (no owner), a find (SideProject).
    private void Spawn()
    {
        GameObject prefab = PrefabNamed(record.prefab);
        DeviceDefinition def = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
        var request = new FeaturedRepairRequest { devicePrefab = prefab, faultIndex = record.fault };
        Job job = null;        // 26 Oct 2026 (compile fix): declared first, so the warning below may read the error when def is null
        string error = null;
        if (def == null || !request.TryCreateJob(out job, out error))
        {
            Debug.LogWarning($"[Thrift] The find \"{record.prefab}\" (fault {record.fault}) can't be made here: {(def == null ? "no such device" : error)}. The shelf stays empty.");
            record.fault = -1;
            record.prefab = "";
            return;
        }
        string thing = SideProject.ThingOf(def.displayName);
        job.deviceName = "thrift " + thing;   // the bench's own words: "Work on the thrift watch"
        job.color = Tape;
        job.payout = record.sale;
        GameObject device = Instantiate(prefab);
        device.name = "Thrift find (" + prefab.name + ")";
        DeviceDefinition placed = device.GetComponent<DeviceDefinition>();
        placed.ApplyFault(record.fault);
        JobBase repair = device.GetComponent<JobBase>();
        repair.Configure(job);
        marker = device.AddComponent<SideProject>();
        marker.findId = record.prefab + "#" + record.fault;
        marker.salePrice = record.sale;
        marker.thing = thing;
        find = repair;
        record.fixedIt = false;   // whole, with its fault (the save never keeps its screws)
        PlacementJitter.Apply(find, Spot, 0f, 0f);
    }

    // The find's device by its asset name: the days' finds, and the café's walk-in devices.
    private static GameObject PrefabNamed(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (GameObject g in Candidates()) if (g != null && g.name == name) return g;
        return null;
    }

    private static IEnumerable<GameObject> Candidates()
    {
        CustomerSpawner s = Downtime.Spawner;
        if (s == null) yield break;
        if (s.Schedule != null)
            foreach (DayDefinition d in s.Schedule)
                if (d != null && d.thriftFind != null && d.thriftFind.devicePrefab != null) yield return d.thriftFind.devicePrefab;
        if (s.DevicePool != null)
            foreach (GameObject g in s.DevicePool) if (g != null) yield return g;
    }

    // The find authored for this day exactly (its own day only), or null.
    private static FeaturedRepairRequest AuthoredFind(int dayNumber, out DayDefinition def)
    {
        CustomerSpawner s = Downtime.Spawner;
        def = s != null ? s.AuthoredDay(dayNumber) : null;
        return def != null ? def.ThriftFindOn(dayNumber) : null;
    }

    // ------------------------------------------------------------------ closing: the sale

    /// <summary>
    /// At closing (SaveManager's closing checkpoint, before it reads the till; once a day): a whole find done well enough sells
    /// by itself, Perfect for its price, Good for 80 % of it; less waits for tomorrow as it is.
    /// </summary>
    public static void SettleAtClosing(int dayNumber)
    {
        if (Instance != null) Instance.Settle(dayNumber);
    }

    private void Settle(int dayNumber)
    {
        if (!restored) Restore();
        if (settledDay == dayNumber) return;
        settledDay = dayNumber;
        if (find == null || record.fault < 0 || record.forDay > dayNumber) return;
        JobGrade grade = find.Grade;
        if (!find.CanHandBack || grade < SellsFrom) return;   // untouched, barely touched or in pieces: it waits
        int sold = Mathf.RoundToInt(record.sale * SideProject.SaleShare(grade));
        if (sold > 0 && ShopEconomy.Instance != null) ShopEconomy.Instance.AddMoney(sold);
        record.soldDay = dayNumber;
        record.soldFor = sold;
        record.soldThing = Thing;
        record.soldGrade = grade.ToString();
        record.lastPrefab = record.prefab;
        record.lastFault = record.fault;
        record.prefab = "";
        record.fault = -1;
        record.fixedIt = false;
        if (ItemInspector.Instance != null && ItemInspector.Instance.FocusedItem == find) ItemInspector.Instance.CancelInspection();
        if (Bench != null) Bench.ReleaseSlot(find);
        Destroy(find.gameObject);
        find = null;
        marker = null;
        UpdateTick();
        Debug.Log($"[Thrift] Day {dayNumber}: sold the thrift {record.soldThing} ({grade}) for ${sold} at closing.");
    }

    /// <summary>The record for the save (SaveManager.CaptureState).</summary>
    public ThriftSaveData Snapshot()
    {
        if (!restored) Restore();
        ThriftSaveData copy = record.Copy();
        copy.known = true;
        return copy;
    }

    // ------------------------------------------------------------------ the Shop: tomorrow's find

    /// <summary>
    /// Tomorrow's find in the recap's Shop, one at a time: offered once the shelf is empty (sold, or none), the next day's
    /// authored find or one of the jobs taught (Rotation). bought: already paid for. False when the shelf still holds an
    /// unsold find (it waits for tomorrow) or there's nothing to offer.
    /// </summary>
    public bool TomorrowsOffer(out Offer offer)
    {
        offer = null;
        DayClock clock = DayClock.Instance;
        if (clock == null) return false;
        if (!restored) Restore();
        int tomorrow = clock.Day + 1;
        if (record.fault >= 0)
        {
            if (record.forDay != tomorrow) return false;   // today's unsold find waits on the shelf
            GameObject bought = PrefabNamed(record.prefab);
            if (bought == null) return false;
            offer = Describe(bought, record.fault, 0, record.sale);
            offer.bought = true;
            return true;
        }
        FeaturedRepairRequest authored = AuthoredFind(tomorrow, out DayDefinition def);
        if (authored != null) offer = Describe(authored.devicePrefab, authored.faultIndex, def.thriftBuy, def.thriftSale);
        else
        {
            for (int k = 0; k < Rotation.Length && offer == null; k++)
            {
                var r = Rotation[(tomorrow + k) % Rotation.Length];
                GameObject prefab = DeviceCalled(r.device, r.label, out int fault);
                if (prefab == null || prefab.name == record.lastPrefab && fault == record.lastFault) continue;
                offer = Describe(prefab, fault, r.buy, r.sale);
            }
        }
        return offer != null;
    }

    /// <summary>Buys tomorrow's find (the Shop's button): the till pays, and it's on the shelf when tomorrow opens.</summary>
    public bool TryBuyTomorrow()
    {
        if (!TomorrowsOffer(out Offer offer) || offer.bought) return false;
        ShopEconomy till = ShopEconomy.Instance;
        if (till == null || till.Money < offer.buy) return false;
        if (offer.buy > 0) till.AddMoney(-offer.buy);
        record.prefab = offer.prefab.name;
        record.fault = offer.fault;
        record.forDay = DayClock.Instance.Day + 1;
        record.sale = offer.sale;
        record.fixedIt = false;
        record.known = true;
        return true;
    }

    private static Offer Describe(GameObject prefab, int fault, int buy, int sale)
    {
        DeviceDefinition def = prefab.GetComponent<DeviceDefinition>();
        string label = def != null && def.faults != null && fault >= 0 && fault < def.faults.Length && def.faults[fault] != null ? def.faults[fault].description : "";
        return new Offer
        {
            prefab = prefab, fault = fault, buy = buy, sale = sale,
            thing = SideProject.ThingOf(def != null ? def.displayName : prefab.name),
            words = SideProject.WordsFor(label).shop,
        };
    }

    // A walk-in device by its name with this fault's label (a bench device: a RepairJob, never the support call's phone).
    private static GameObject DeviceCalled(string device, string label, out int fault)
    {
        fault = -1;
        foreach (GameObject g in Candidates())
        {
            DeviceDefinition def = g != null ? g.GetComponent<DeviceDefinition>() : null;
            if (def == null || g.GetComponent<RepairJob>() == null || g.GetComponent<SideProject>() != null) continue;
            if (!string.Equals(def.displayName, device, System.StringComparison.OrdinalIgnoreCase) || def.faults == null) continue;
            for (int i = 0; i < def.faults.Length; i++)
                if (def.faults[i] != null && string.Equals(def.faults[i].description, label, System.StringComparison.Ordinal)) { fault = i; return g; }
        }
        return null;
    }
}
