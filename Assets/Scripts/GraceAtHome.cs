using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// GRACE AT HOME AT NIGHT (break-ins chunk C, 6 Oct 2026; claude/chunk-c-grace-at-home-plan.md, all as Mansoor took it;
// the numbers are claude/break-ins-spec.md §6's)
//
// His playtest: stealing her cups, "she just stays right at the door ... I faced right through her". The day visitor
// frozen in her doorway was fixed first (989f043); this is the rest: at night she is home, and the house is hers.
//
// Her night (GraceNight has the times): her armchair and the TV from 11 PM, the kettle at 11:35, up to bed at midnight
// (the TV and her lamp off, the landing light on the way up, her bedside lamp on, the doors shut behind her), her lamp
// off at 12:20, a glass of water at 2:40 (her lamp, the doors, the landing light, down to the sink and back), asleep
// again. Thursdays she's out till 1:30 AM, comes home along the pavement and in at her front door, makes tea, and is in
// bed by 1:50. The street reads it all from her windows: the front window lit (the TV flickering through it), then a
// bedroom bay, then dark (NightHomes lights her bays as her bedside lamp says; her front window's curtains glow while the
// front room's lamp is on).
//
// Her body: the café's patron body in her own look, put in her house at nightfall the way the man at the bins is put in
// his dumpster (no brain, no navigation: she walks the house's set ways, GraceHouseMap, with the café's walk, and sits in
// her armchair with the café's sit). In bed her body is put away and the slept-in quilt shows her.
//
// Her eyes and ears (only for Ace inside her house): she sees in a 110° cone, 7 m where it's lit and 3 m where it isn't,
// walls and furniture in the way (rays twice a second); watching TV, only the 60° toward the screen, at half the rate.
// She hears what Ace's steps already send (NightNoise: walking 4 m, sneaking 1 m), the rustle of taking something
// indoors (3 m), the creaky treads (one on each flight) and her bedroom doors opened at a walk (5 m). Her mark (the "?"
// beside her head, NoticeMark) fills as GraceNight says: at a third "Hm?", she stops and looks where she noticed Ace;
// with it at half or more and Ace gone from her view she comes to look there, looks round, and goes back to what she was
// doing; full, she has caught Ace ("Ace?! What on earth—", and the night ends on "Caught.": NightCycle.Caught). Asleep
// she sees nothing; a loud sound near her wakes her for 20 s: her lamp on, "Hello?", a look round from her bed.
//
// Ace can hide in the cupboard under the stairs and in her wardrobe (HidingPlace: E in, E out; the body and the capsule
// set aside) and opens her bedroom doors (BedroomDoorsZone: at a walk at once, with a creak; sneaking, slowly and quietly).
//
// While Ace is elsewhere nothing here costs more than her timeline and her walk. Nothing new allocates a frame.
// Built by Fixit Fidget > Night > Break-ins 6 - Put Grace at home (GraceAtHomeSteps), on her rooms' root beside GraceHouse.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
[DefaultExecutionOrder(30)]   // after NightWalk (0) moves the clock on; before her body's look and city body (120, 150)
public sealed class GraceAtHome : MonoBehaviour
{
    [Header("Built by Fixit Fidget > Night > Break-ins 6 - Put Grace at home")]
    public GraceHouse house;
    [Tooltip("Her look at night: her stand-in look by day (Character_BusinessWoman), a city look nobody else wears.")]
    public GameObject look;
    public Light standardLamp, bedsideLamp, landingLight, kitchenLight;
    [Tooltip("The TV's light over the front room (night only; it flickers while the TV is on).")]
    public Light tvLight;
    [Tooltip("The TV: its screen glows while it's on.")]
    public Renderer tv;
    [Tooltip("Her front window's curtains: they glow toward the street while the front room's lamp is on.")]
    public Renderer frontCurtains;
    public GameObject quiltMade, quiltAsleep;
    [Tooltip("The bedroom doors' hinges (Bedroom door, west leaf / east leaf).")]
    public Transform westLeaf, eastLeaf;
    public float westClosedYaw = 0f, westOpenYaw = 175f, eastClosedYaw = 180f, eastOpenYaw = 5f;
    [Tooltip("Her armchair (GH_Armchair): she sits back into it, facing the TV.")]
    public Transform armchair;
    [Tooltip("What Break-ins 6 added to the scene (the TV's light, the slept-in quilt): taken out with her.")]
    [HideInInspector] public GameObject[] addedByStep = Array.Empty<GameObject>();

    [Header("Her pace")]
    [Range(.5f, 1.6f)] public float walkSpeed = 1.2f;
    [Range(.3f, 1.2f)] public float stairsSpeed = .85f;
    [Range(90f, 720f)] public float turnSpeed = 320f;

    [Header("Thursdays: home along the pavement")]
    [Tooltip("Her way home (world points, the far end first, ending on the pavement level with her door): from the corner the " +
             "dusty rose house's neighbour comes round (NightWalk), down the street side of the pavement, clear of the stoops' " +
             "railings and the signal post at her corner (the Thursday check walks it with a capsule her size). She appears at the " +
             "nearest point of it the camera can't see, timed to be at her door at 1:30.")]
    public Vector3[] wayHome = { new Vector3(-15.9f, -.02f, 19.4f), new Vector3(-15f, -.02f, 18f), new Vector3(-15f, -.02f, -3.6f) };

    // ---- where things are, in plan metres (GraceHouseSteps; Tools/Blender/plan_v2.py AS_BUILT) ----
    static readonly Vector3 TvScreen = new Vector3(5.10f, 2.83f, .80f);
    static readonly Vector3 KettleTop = new Vector3(3.385f, .42f, 1.0f);
    static readonly Vector3 SinkTap = new Vector3(4.16f, .30f, 1.0f);
    static readonly Vector3 BedMiddle = new Vector3(4.41f, 3.28f, 2.95f);
    static readonly Vector3 DoorsMiddle = new Vector3(3.05f, 1.51f, 3.40f);
    static readonly Vector3 Pillow = new Vector3(5.05f, 3.28f, 3.15f);       // her eyes in bed (sat up a little)
    static readonly Vector3 RoomFromBed = new Vector3(3.0f, 2.2f, 3.2f);     // the way she looks from her bed
    static readonly Vector3 StoopFront = new Vector3(1.11f, 5.30f, 0f), StoopTop = new Vector3(1.11f, 4.55f, 0f);
    // The creaky treads: the upper flight's fourth (within 5 m of her pillow), the lower flight's third (just out of it).
    static readonly Vector3 UpperCreak = new Vector3(2.24f, .70f, 2.0f), LowerCreak = new Vector3(.70f, 2.00f, .60f);
    // The places: middle and size (X, Y, Z) in plan metres.
    static readonly Vector3 CupboardMiddle = new Vector3(2.22f, 1.95f, 1.0f), CupboardSize = new Vector3(1.10f, 1.00f, 2.0f);
    static readonly Vector3 WardrobeMiddle = new Vector3(1.28f, 3.55f, 3.40f), WardrobeSize = new Vector3(1.10f, .90f, 2.0f);
    static readonly Vector3 DoorwayMiddle = new Vector3(3.05f, 1.51f, 3.50f), DoorwaySize = new Vector3(1.40f, 1.60f, 2.2f);
    const float SoftCreak = 2.5f;          // a creaky tread under a sneaking foot: heard this far (not as far as her pillow)
    const float LookEvery = .5f;           // her eyes look twice a second
    const float InBedBeforeLampOff = 4f;   // seconds in bed before her lamp goes off, at the earliest
    static readonly GraceNight.Step[] OutAll = { new GraceNight.Step(23f, GraceNight.Act.Out) };

    public enum Mood { Calm, Looking, Searching, Woken, Caught }
    enum Pose { Standing, SittingDown, Seated, StandingUp, InBed }
    enum Station { Away, Armchair, Kettle, InBed, Asleep }

    public static GraceAtHome Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    // ---- for checks and reports ----
    public bool NightOn => nightOn;
    public bool Home => nightOn && body != null && !away;
    public bool Away => !nightOn || away;
    public bool InBed => pose == Pose.InBed;
    public bool Seated => pose == Pose.Seated;
    public bool Asleep => Home && station == Station.Asleep && pose == Pose.InBed && mood != Mood.Woken;
    public bool WatchingTv => Home && pose == Pose.Seated && station == Station.Armchair && tvOn && mood == Mood.Calm;
    public bool Walking => walkingNow;
    public bool OnTheStreet => street;
    public Mood Now => mood;
    public float Mark => mark;
    public bool SeesAce => seeing;
    public bool TvOn => tvOn;
    public bool DoorsShut => doors <= 0f && doorsTo <= 0f;
    public bool DoorsOpen => doors >= 1f && doorsTo >= 1f;
    public bool AceHidden => hiddenIn != null;
    public HidingPlace Cupboard => cupboard;
    public HidingPlace Wardrobe => wardrobe;
    public BedroomDoorsZone DoorsZone => doorsZone;
    public Transform Body => body != null ? body.transform : null;
    /// <summary>Where she is (plan metres: X north, Y to the street, Z up).</summary>
    public Vector3 Feet => feet;
    /// <summary>The house spot she's at or last passed (GraceHouseMap), or -1.</summary>
    public int Spot => lastSpot;
    public GraceNight.Act Step => plan != null && stepNow >= 0 && stepNow < plan.Length ? plan[stepNow].act : GraceNight.Act.Out;
    public bool Thursday => plan == GraceNight.Thursday;
    public int Notices { get; private set; }
    public int Searches { get; private set; }
    public int Wakes { get; private set; }
    public int Hides { get; private set; }
    public int Creaks { get; private set; }
    public int CreakyOpens { get; private set; }
    public int QuietOpens { get; private set; }
    public int SoundsHeard { get; private set; }
    public int Catches { get; private set; }
    public NoiseKind LastHeard { get; private set; }
    /// <summary>What woke her last (asleep, a loud sound near her).</summary>
    public NoiseKind WokeTo { get; private set; }
    public float SearchedNearest { get; private set; } = float.MaxValue;
    public string Why { get; private set; } = "";

    // ---- the night ----
    bool nightOn, away, street, outAllNight;
    GraceNight.Step[] plan;
    int stepNow = -1, stepDone = -1, jumpsSeen;
    float hour;
    Station station = Station.Away;
    Mood mood = Mood.Calm;
    readonly Stack<IEnumerator> routine = new Stack<IEnumerator>();
    readonly Stack<IEnumerator> search = new Stack<IEnumerator>();

    // ---- her body ----
    GameObject body;
    Animator animator;
    PolygonNpcVisual visual;
    NpcLookAt lookAt;
    NpcBeats beats;
    NpcPosture spine;
    NpcSeating seating;
    NpcLocomotion locomotion;
    Transform voice;
    Renderer[] bodyParts = Array.Empty<Renderer>();
    GameObject partsOf;
    bool bodyDrawn = true, hasWalkRate, hasWalking, hasSeated;
    static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    static readonly int WalkRateHash = Animator.StringToHash("WalkRate");
    static readonly int SeatedHash = Animator.StringToHash("Seated");
    static readonly int SittingState = Animator.StringToHash("Sitting");
    static readonly int IdleState = Animator.StringToHash("CharacterArmature|Idle");

    // ---- the mover (plan metres) ----
    Vector3 feet, segmentFrom, seatFeet;
    float yaw, seatYaw, walkChange;
    readonly List<Vector3> route = new List<Vector3>();
    readonly List<int> routeSpots = new List<int>();
    readonly List<int> pathBuffer = new List<int>();
    int lastSpot = -1, goal = -1;
    bool held, walkingNow, walkingShown, turning, herOnUpper, herOnLower;
    Vector3 turnTo;
    Pose pose = Pose.Standing;
    float poseT, poseSeconds;
    Vector3 poseFrom, poseTo;
    float inBedSince;

    // ---- her eyes and ears ----
    float mark, seeRate, nextLook, lastNoticed = -10f, lastSoundAt = -10f, lookUntil, wokenUntil, lastBark = -10f;
    bool seeing;
    Vector3 noticePoint, lastSeen;
    static readonly RaycastHit[] Hits = new RaycastHit[16];
    Light[] lamps = Array.Empty<Light>();

    // ---- Ace ----
    PlayerMovement ace;
    Transform aceT;
    CharacterController aceController;
    CafeViewMode view;
    AceBody aceBody;
    HidingPlace cupboard, wardrobe, hiddenIn;
    BedroomDoorsZone doorsZone;
    float hiddenSince;
    bool aceOnUpper, aceOnLower;
    readonly object doorsHold = new object();

    // ---- the house ----
    float doors = 1f, doorsTo = 1f, doorsRate = 1f;
    bool aceAtDoors;
    Collider[] leafColliders = Array.Empty<Collider>();
    bool tvOn;
    float tvLevel = 1f, tvTarget = 1f, tvNext, tvBase = 1f;
    bool tvBaseKnown;
    Color tvColour = new Color(.55f, .7f, 1f);
    Material tvScreenOwn, tvScreenLit;
    int tvSlot = -1;
    SfxLoop tvSound;
    Material[] curtainsLit, curtainsDark;
    bool curtainsGlow = true;
    bool holdingFrontDoor;
    float liveSince;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ================================================================== lifecycle

    void OnEnable()
    {
        Instance = this;
        if (house == null) house = GetComponent<GraceHouse>();
        // Her house's upstairs rooms follow her lamps, not the night's guess (NightHomes).
        if (house != null && house.house != null) NightHomes.Drive(house.house, 1, false);
        lamps = new[] { standardLamp, bedsideLamp, landingLight, kitchenLight, tvLight };
    }

    void OnDisable()
    {
        if (nightOn) EndNight();
        if (house != null && house.house != null) NightHomes.StopDriving(house.house);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        NightWalk night = NightWalk.Instance;
        bool on = night != null && night.Active && house != null;
        if (on && !nightOn) BeginNight(night);
        else if (!on && nightOn) EndNight();
        if (!nightOn) return;
        hour = night.Hour;
        if (night.Jumps != jumpsSeen)
        {
            jumpsSeen = night.Jumps;
            Settle(hour);
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Sense(dt);
        RunRoutine();
        Move(dt);
        SwingDoors(dt);
        Flicker(dt);
        Creak();
        Hidden();
        Show();
    }

    /// <summary>A lab sends her out for the night (the walk check: nobody home, so Ace can walk every room).</summary>
    public void OutAllNight(string why)
    {
        outAllNight = true;
        Why = why ?? "";
        if (!nightOn) return;
        plan = OutAll;
        Settle(hour);
    }

    void BeginNight(NightWalk night)
    {
        nightOn = true;
        liveSince = Time.time + .5f;
        FindAce();
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        plan = outAllNight ? OutAll : GraceNight.For(day);
        lamps = new[] { standardLamp, bedsideLamp, landingLight, kitchenLight, tvLight };
        if (plan != OutAll && !MakeBody())
        {
            Why = "no body to put in her house (the patron prefab is missing): she's out tonight";
            plan = OutAll;
        }
        MakeVoice();
        MakeZones();
        leafColliders = LeafColliders();
        RememberCurtains();
        FindTheScreen();
        NightNoise.Made += Heard;
        jumpsSeen = night.Jumps;
        hour = night.Hour;
        mark = 0f;
        mood = Mood.Calm;
        Settle(hour);
        Debug.Log($"[Grace at home] {Weekdays.Label(day)}, the night: " + (plan == OutAll ? "she's out all night" + (Why.Length > 0 ? $" ({Why})" : "")
            : plan == GraceNight.Thursday ? "her Thursday: out till 1:30 AM, home along the pavement, the kettle, bed at 1:50"
            : "her usual night: the TV, the kettle at 11:35, bed at midnight, her lamp off at 12:20, water at 2:40") +
            $". Now ({GraceNight.Clock(hour)}): {Doing}.");
    }

    void EndNight()
    {
        nightOn = false;
        NightNoise.Made -= Heard;
        if (hiddenIn != null) ComeOut(false);
        if (aceAtDoors) { aceAtDoors = false; PlayerMovement.Release(doorsHold); }
        ReleaseFrontDoor();
        foreach (Light l in lamps) if (l != null) l.enabled = false;
        SetTv(false, false);
        if (tvLight != null && tvBaseKnown) tvLight.intensity = tvBase;
        if (house != null && house.house != null) NightHomes.Drive(house.house, 1, false);
        RestoreCurtains();
        Quilt(false);
        doors = doorsTo = 1f;
        ApplyDoors();
        foreach (Collider c in leafColliders) if (c != null) c.enabled = true;
        RemoveZones();
        if (body != null) Destroy(body);
        body = null;
        if (voice != null) Destroy(voice.gameObject);
        voice = null;
        NoticeMark.Hide();
        if (tvScreenLit != null) Destroy(tvScreenLit);
        tvScreenLit = null;
        routine.Clear();
        search.Clear();
        mood = Mood.Calm;
        mark = 0f;
        away = true;
        street = false;
        outAllNight = false;
        pose = Pose.Standing;
        station = Station.Away;
        plan = null;
    }

    // ================================================================== her body

    // The café's patron body, made inside a switched-off holder so nothing of the café's wakes up in it (no brain, no
    // navigation agent off the navigation mesh, no café glances), in her look; under her rooms' root, so the house's
    // shell and floors never count it as theirs.
    bool MakeBody()
    {
        if (body != null) return true;
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        GameObject prefab = spawner != null ? spawner.PatronPrefab : null;
        if (prefab == null) return false;
        var holder = new GameObject("(Grace, getting ready for the night)");
        holder.SetActive(false);
        body = Instantiate(prefab, holder.transform);
        body.name = "Grace, at home (while the night runs)";
        foreach (NavMeshAgent agent in body.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (MonoBehaviour brain in new MonoBehaviour[] { body.GetComponent<CustomerBrain>(), body.GetComponent<PatronBrain>() })
            if (brain != null) brain.enabled = false;
        // Her look, not a walk-in's outfit; and nobody's glances or personal space (they'd steer her head and body).
        NpcVisualVariants outfits = body.GetComponent<NpcVisualVariants>();
        if (outfits != null) outfits.enabled = false;
        NpcSocial social = body.GetComponent<NpcSocial>();
        if (social != null) social.enabled = false;
        PersonalSpace space = body.GetComponent<PersonalSpace>();
        if (space != null) space.enabled = false;
        visual = body.GetComponent<PolygonNpcVisual>();
        if (visual != null && look != null) visual.Configure(new[] { look }, 0f, 0, 0f);
        body.transform.SetParent(transform, false);
        Destroy(holder);
        foreach (MonoBehaviour gone in new MonoBehaviour[] { body.GetComponent<CustomerBrain>(), body.GetComponent<PatronBrain>(), outfits, social, space })
            if (gone != null) Destroy(gone);
        foreach (Interactable talk in body.GetComponents<Interactable>()) Destroy(talk);
        foreach (PatienceBar bar in body.GetComponentsInChildren<PatienceBar>(true)) bar.gameObject.SetActive(false);
        foreach (Transform child in body.transform)
            if (child.name == "SpeechBubble") child.gameObject.SetActive(false);
        animator = body.GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        hasWalkRate = hasWalking = hasSeated = false;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == WalkRateHash) hasWalkRate = true;
                if (p.nameHash == IsWalkingHash) hasWalking = true;
                if (p.nameHash == SeatedHash) hasSeated = true;
            }
        lookAt = body.GetComponent<NpcLookAt>();
        if (lookAt == null) lookAt = body.AddComponent<NpcLookAt>();
        beats = body.GetComponent<NpcBeats>();
        spine = body.GetComponent<NpcPosture>();
        seating = body.GetComponent<NpcSeating>();
        locomotion = body.GetComponent<NpcLocomotion>();
        bodyParts = Array.Empty<Renderer>();
        partsOf = null;
        bodyDrawn = true;
        WorkOutTheSeat();
        return true;
    }

    // Her voice in bed (her body put away): over the middle of her pillow. Barks pins a line with nothing drawn under it
    // 2.15 m over the point it's given (as the man's voice in his bin).
    void MakeVoice()
    {
        if (voice != null) return;
        var go = new GameObject("Grace's voice, in bed (while the night runs)");
        go.transform.SetParent(transform, false);
        voice = go.transform;
        voice.position = W(new Vector3(Pillow.x, Pillow.y, 2.40f + .62f)) + Vector3.up * (.45f - 2.15f);
    }

    // Where she sits: the café's own sum (NpcSeating.Placement) for a seat at the armchair's cushion, facing the TV.
    void WorkOutTheSeat()
    {
        Vector3 standAt = SpotAt(GraceHouseMap.Armchair);
        seatFeet = standAt;
        seatYaw = YawToward(standAt, TvScreen);
        if (armchair == null) return;
        Vector3 cushion = armchair.TransformPoint(new Vector3(0f, .45f, .095f));
        Vector3 screen = W(TvScreen);
        float floor = W(Vector3.zero).y;
        if (seating != null)
        {
            seating.Placement(cushion, screen, W(standAt), floor, out Vector3 at, out Quaternion facing);
            seatFeet = house.Plan(at);
            seatYaw = facing.eulerAngles.y;
        }
        else
        {
            Vector3 toScreen = screen - cushion;
            toScreen.y = 0f;
            seatFeet = house.Plan(cushion + toScreen.normalized * .26f);
            seatFeet.z = 0f;
        }
    }

    // Drawn, or not: away, in bed (the quilt shows her), or upstairs while the house hides its first floor (Ace downstairs,
    // looking in from the street). The city body's own parts, looked up again whenever its look changes.
    void Show()
    {
        if (body == null) { NoticeMark.Hide(); return; }
        bool drawn = !away && pose != Pose.InBed && !(house.FirstFloorHidden && feet.z > 1.8f);
        GameObject instance = visual != null ? visual.VisualInstance : null;
        if (instance != partsOf || bodyParts.Length == 0)
        {
            partsOf = instance;
            bodyParts = body.GetComponentsInChildren<Renderer>(true);
            bodyDrawn = !drawn;   // apply below
        }
        if (drawn != bodyDrawn)
        {
            bodyDrawn = drawn;
            foreach (Renderer r in bodyParts) if (r != null) r.forceRenderingOff = !drawn;
        }
        // Her head: toward what caught her eye or ear, while it has; the animation's own otherwise.
        if (lookAt != null)
        {
            bool looking = mood != Mood.Calm && pose != Pose.InBed;
            if (looking) lookAt.LookAt(mood == Mood.Caught && aceT != null ? aceT.position + Vector3.up * .6f : noticePoint + Vector3.up * 1.1f, 1f);
            else lookAt.Clear();
        }
        NoticeMark.Show(Speaker, away || street ? 0f : mark);
    }

    /// <summary>Where her lines and her mark are pinned: her body, or her pillow while she's in bed.</summary>
    public Transform Speaker => pose == Pose.InBed || body == null ? voice : body.transform;

    // ================================================================== her night

    void RunRoutine()
    {
        if (mood != Mood.Calm || plan == null) return;
        if (routine.Count == 0) routine.Push(Night());
        Advance(routine);
    }

    // One frame of a stack of steps (a step may hand over to another by yielding it).
    static void Advance(Stack<IEnumerator> steps)
    {
        for (int guard = 0; steps.Count > 0 && guard < 32; guard++)
        {
            IEnumerator step = steps.Peek();
            bool more;
            try { more = step.MoveNext(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                steps.Clear();
                return;
            }
            if (!more) { steps.Pop(); continue; }
            if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
            return;   // a frame's wait
        }
    }

    IEnumerator Night()
    {
        while (true)
        {
            int i = GraceNight.Now(plan, hour);
            if (i != stepDone)
            {
                stepNow = i;
                yield return Do(plan[i].act);
                stepDone = i;
                continue;
            }
            if (!AtStation()) yield return BackToStation();
            yield return null;
        }
    }

    IEnumerator Do(GraceNight.Act act)
    {
        switch (act)
        {
            case GraceNight.Act.Out: AtStationNow(Station.Away); break;
            case GraceNight.Act.Sit: yield return GoSit(); break;
            case GraceNight.Act.Tea: yield return GoMakeTea(); break;
            case GraceNight.Act.Bed: yield return GoToBed(); break;
            case GraceNight.Act.Sleep: yield return FallAsleep(); break;
            case GraceNight.Act.Water: yield return GetWater(); break;
            case GraceNight.Act.ComeHome: yield return ComeHome(); break;
        }
    }

    IEnumerator GoSit()
    {
        station = Station.Armchair;
        if (pose == Pose.InBed) GetOutOfBed();
        Lamp(standardLamp, true);
        yield return SitInHerChair();
        Lamp(kitchenLight, false);   // already off: it goes off behind her as she leaves the kitchen (Passed)
        SetTv(true, true);
    }

    IEnumerator SitInHerChair()
    {
        while (pose != Pose.Seated)
        {
            if (pose == Pose.Standing)
            {
                if (lastSpot != GraceHouseMap.Armchair || route.Count > 0) yield return GoTo(GraceHouseMap.Armchair);
                else
                {
                    yield return Face(W(TvScreen));
                    SitDown();
                }
            }
            yield return null;
        }
    }

    IEnumerator GoMakeTea()
    {
        station = Station.Kettle;
        yield return IntoTheKitchen(GraceHouseMap.Kettle);
        yield return Face(W(KettleTop));
        Sfx.Play("grace.kettle", W(KettleTop));
        if (spine != null) spine.Sway(1.2f, 6f);
    }

    // On her way to a spot in her kitchen: the kitchen light goes on as she steps into the kitchen (when the next spot on
    // her way is in it), as anyone's would at the switch by the door, not once she's at the worktop.
    IEnumerator IntoTheKitchen(int spot)
    {
        IEnumerator going = GoTo(spot);
        while (going.MoveNext())
        {
            if (!Lit(kitchenLight) && routeSpots.Count > 0 && InTheKitchen(routeSpots[0])) Lamp(kitchenLight, true);
            yield return going.Current;
        }
        Lamp(kitchenLight, true);
    }

    static bool InTheKitchen(int spot) =>
        spot == GraceHouseMap.Kettle || spot == GraceHouseMap.Kitchen || spot == GraceHouseMap.Sink || spot == GraceHouseMap.Fridge
        || spot == GraceHouseMap.CupboardCorner;

    // As she passes a spot on her way: out of the kitchen on her routine and not on her way to the kettle, its light goes
    // off behind her (going to look where she heard something leaves it on: she's coming back).
    void Passed(int spot)
    {
        if (spot >= 0 && mood == Mood.Calm && station != Station.Kettle && !InTheKitchen(spot) && Lit(kitchenLight)) Lamp(kitchenLight, false);
    }

    IEnumerator GoToBed()
    {
        station = Station.InBed;
        if (pose == Pose.InBed) yield break;
        if (pose == Pose.Seated || pose == Pose.SittingDown) SetTv(false, true);
        yield return GoTo(GraceHouseMap.FootOfStairs);
        Lamp(kitchenLight, false);   // already off if she came from the kettle (Passed)
        SetTv(false, true);
        Lamp(landingLight, true);
        Lamp(standardLamp, false);
        yield return GoTo(GraceHouseMap.LandingUpstairs);
        if (!DoorsOpen)
        {
            yield return Face(W(DoorsMiddle));
            SwingTheDoors(true, .8f, true);
            while (!DoorsOpen) yield return null;
        }
        yield return GoTo(GraceHouseMap.InsideBedroomDoor);
        Lamp(bedsideLamp, true);
        Lamp(landingLight, false);
        yield return Face(W(DoorsMiddle));
        SwingTheDoors(false, 1.1f, true);
        while (!DoorsShut) yield return null;
        yield return GoTo(GraceHouseMap.Bedside);
        yield return Face(W(BedMiddle));
        GetInBed();
    }

    IEnumerator FallAsleep()
    {
        station = Station.Asleep;
        if (pose != Pose.InBed)
        {
            station = Station.InBed;
            yield return GoToBed();
            station = Station.Asleep;
        }
        while (Time.time - inBedSince < InBedBeforeLampOff) yield return null;
        Lamp(bedsideLamp, false);
    }

    IEnumerator GetWater()
    {
        station = Station.InBed;
        if (pose == Pose.InBed)
        {
            Lamp(bedsideLamp, true);
            float sitUp = Time.time + 1.2f;
            while (Time.time < sitUp) yield return null;
            GetOutOfBed();
        }
        yield return GoTo(GraceHouseMap.InsideBedroomDoor);
        if (!DoorsOpen)
        {
            yield return Face(W(DoorsMiddle));
            SwingTheDoors(true, .9f, true);
            while (!DoorsOpen) yield return null;
        }
        Lamp(landingLight, true);
        yield return IntoTheKitchen(GraceHouseMap.Sink);
        yield return Face(W(SinkTap));
        Sfx.Play("grace.tap", W(SinkTap));
        float drinking = Time.time + 4f;
        while (Time.time < drinking) yield return null;
        yield return GoTo(GraceHouseMap.InsideBedroomDoor);   // the kitchen light goes off behind her (Passed)
        Lamp(kitchenLight, false);
        Lamp(landingLight, false);
        yield return Face(W(DoorsMiddle));
        SwingTheDoors(false, 1.1f, true);
        while (!DoorsShut) yield return null;
        yield return GoTo(GraceHouseMap.Bedside);
        yield return Face(W(BedMiddle));
        GetInBed();
    }

    // Thursdays: along the pavement from up the street, up her steps, in at her front door at 1:30 (it opens for her as for
    // anyone coming home), through to the kitchen and the kettle. Until she has to set off she's out of sight; she appears at
    // the nearest point of her way home the camera can't see, never in view (at once if the clock has been moved on).
    IEnumerator ComeHome()
    {
        station = Station.Kettle;
        if (away)
        {
            int segment = WhereSheComesFrom(out Vector3 from, out float seconds);
            while (hour < GraceNight.ThursdayHome - seconds * HoursPerSecond)
            {
                yield return null;
                segment = WhereSheComesFrom(out from, out seconds);
            }
            BuildTheWayHome(from, segment);
            SetOffFrom = from;
            away = false;
            street = true;
            pose = Pose.Standing;
            Quilt(false);
            while (route.Count > 0 || street)
            {
                HoldTheFrontDoor();
                if (route.Count == 0) street = false;
                yield return null;
            }
            ReleaseFrontDoor();
        }
        street = false;
        yield return IntoTheKitchen(GraceHouseMap.Kettle);
        yield return Face(W(KettleTop));
        Sfx.Play("grace.kettle", W(KettleTop));
        if (spine != null) spine.Sway(1.2f, 6f);
    }

    // Where on her way home she appears: the nearest point of it, from her steps outward a metre at a time, that the camera
    // can't see (the far end if it sees all of it); which stretch of the way that is, and the seconds from there to inside her
    // door at her pace (a second for the door).
    int WhereSheComesFrom(out Vector3 from, out float seconds)
    {
        int last = wayHome != null ? wayHome.Length - 1 : -1;
        if (last < 0)
        {
            from = W(StoopFront);
            seconds = StoopWay(from) / Mathf.Max(.1f, walkSpeed) + 1f;
            return -1;
        }
        from = wayHome[0];
        int segment = 0;
        bool found = false;
        for (int k = last - 1; k >= 0 && !found; k--)
        {
            Vector3 a = wayHome[k], b = wayHome[k + 1];   // walked from a to b
            float length = Vector3.Distance(a, b);
            for (float back = 0f; back <= length + .01f; back += 1f)
            {
                Vector3 p = Vector3.MoveTowards(b, a, back);
                if (InView(p)) continue;
                from = p;
                segment = k;
                found = true;
                break;
            }
        }
        if (last == 0) segment = -1;
        float way = 0f;
        Vector3 at = from;
        for (int i = segment + 1; i <= last; i++)
        {
            way += Vector3.Distance(at, wayHome[i]);
            at = wayHome[i];
        }
        seconds = (way + StoopWay(at)) / Mathf.Max(.1f, walkSpeed) + 1f;
        return segment;
    }

    // From the pavement in front of her steps, up them and in at her door.
    float StoopWay(Vector3 from) =>
        Vector3.Distance(from, W(StoopFront)) + Vector3.Distance(W(StoopFront), W(StoopTop)) + Vector3.Distance(W(StoopTop), SpotWorld(GraceHouseMap.Doorway))
        + Vector3.Distance(SpotWorld(GraceHouseMap.Doorway), SpotWorld(GraceHouseMap.Entry));

    Vector3 SpotWorld(int spot) => W(SpotAt(spot));

    static float HoursPerSecond
    {
        get
        {
            NightWalk walk = NightWalk.Instance;
            return walk != null && walk.HoursPerSecond > 0f ? walk.HoursPerSecond : 1f / 48f;
        }
    }

    /// <summary>Thursdays: where she appeared on her way home (for the checks).</summary>
    public Vector3 SetOffFrom { get; private set; }

    /// <summary>The foot of her front steps (world): where her way home leaves the pavement.</summary>
    public Vector3 StepsFront => house != null ? W(StoopFront) : Vector3.zero;

    void BuildTheWayHome(Vector3 from, int segment)
    {
        route.Clear();
        routeSpots.Clear();
        feet = house.Plan(Ground(from));
        segmentFrom = feet;
        for (int i = segment + 1; wayHome != null && i < wayHome.Length; i++) AddFree(house.Plan(Ground(wayHome[i])), -1);
        AddFree(house.Plan(Ground(W(StoopFront))), -1);
        AddFree(house.Plan(Ground(W(StoopTop))), -1);
        AddFree(SpotAt(GraceHouseMap.Doorway), GraceHouseMap.Doorway);
        AddFree(SpotAt(GraceHouseMap.Entry), GraceHouseMap.Entry);
        goal = GraceHouseMap.Entry;
        lastSpot = -1;
        yaw = route.Count > 0 ? YawToward(feet, route[0]) : yaw;
        Place();
    }

    void AddFree(Vector3 plan, int spot)
    {
        route.Add(plan);
        routeSpots.Add(spot);
    }

    // Her front door opens for her as she comes up to it, and stays open until she's in (StreetDoor holds; at 1:30 AM
    // nobody else is using it).
    void HoldTheFrontDoor()
    {
        StreetDoor door = house != null ? house.door : null;
        if (door == null || body == null) return;
        Vector3 at = body.transform.position - door.DoorwayPoint;
        at.y = 0f;
        bool near = at.magnitude < 2.4f && feet.y > SpotAt(GraceHouseMap.Entry).y - .3f;
        if (near) { door.Hold(this, .4f); holdingFrontDoor = true; }
        else ReleaseFrontDoor();
    }

    void ReleaseFrontDoor()
    {
        if (!holdingFrontDoor) return;
        holdingFrontDoor = false;
        if (house != null && house.door != null) house.door.Release(this);
    }

    // ---------------------------------------------------------------- where each step leaves her

    bool AtStation()
    {
        switch (station)
        {
            case Station.Away: return true;
            case Station.Armchair: return pose == Pose.Seated;
            case Station.Kettle: return pose == Pose.Standing && lastSpot == GraceHouseMap.Kettle && route.Count == 0;
            default: return pose == Pose.InBed;
        }
    }

    // After a look round elsewhere: back to what she was doing.
    IEnumerator BackToStation()
    {
        switch (station)
        {
            case Station.Armchair:
                yield return SitInHerChair();
                break;
            case Station.Kettle:
                yield return GoTo(GraceHouseMap.Kettle);
                yield return Face(W(KettleTop));
                break;
            case Station.InBed:
            case Station.Asleep:
                yield return GoTo(GraceHouseMap.Bedside);
                yield return Face(W(BedMiddle));
                GetInBed();
                break;
        }
    }

    // A jump in the clock (a check's SetHour, the night's start): where the step under way leaves her, at once.
    void Settle(float h)
    {
        routine.Clear();
        search.Clear();
        if (plan == null) return;
        int i = GraceNight.Now(plan, h);
        stepNow = stepDone = i;
        bool usual = plan == GraceNight.Usual;
        switch (plan[i].act)
        {
            case GraceNight.Act.Out: AtStationNow(Station.Away); break;
            case GraceNight.Act.Sit: AtStationNow(Station.Armchair); break;
            case GraceNight.Act.Tea: AtStationNow(Station.Kettle, usual); break;
            case GraceNight.Act.ComeHome:
                // Before 1:30 she's still out (on her way: ComeHome brings her from out of sight); after, in at the kettle.
                if (h < GraceNight.ThursdayHome)
                {
                    AtStationNow(Station.Away);
                    stepDone = i - 1;
                }
                else AtStationNow(Station.Kettle, false);
                break;
            case GraceNight.Act.Bed:
            case GraceNight.Act.Water: AtStationNow(Station.InBed); break;
            case GraceNight.Act.Sleep: AtStationNow(Station.Asleep); break;
        }
        if (mood != Mood.Caught) mood = Mood.Calm;
        mark = mood == Mood.Caught ? 1f : 0f;
        held = false;
    }

    void AtStationNow(Station s, bool frontRoomLit = true)
    {
        station = s;
        route.Clear();
        routeSpots.Clear();
        goal = -1;
        turning = false;
        street = false;
        ReleaseFrontDoor();
        bool live = Time.time >= liveSince;
        if (spine != null) spine.Sway(0f);
        switch (s)
        {
            case Station.Away:
                away = true;
                StandNow(GraceHouseMap.Entry);
                Lights(false, false, false, false, false);
                SetTv(false, false);
                DoorsNow(true);
                Quilt(false);
                lastSpot = -1;
                break;
            case Station.Armchair:
                away = false;
                SeatNow();
                Lights(true, false, false, false, live);
                SetTv(true, false);
                DoorsNow(true);
                Quilt(false);
                break;
            case Station.Kettle:
                away = false;
                StandNow(GraceHouseMap.Kettle);
                yaw = YawToward(feet, KettleTop);
                Lights(frontRoomLit, false, false, true, live);
                SetTv(frontRoomLit, false);
                DoorsNow(true);
                Quilt(false);
                break;
            case Station.InBed:
            case Station.Asleep:
                away = false;
                StandNow(GraceHouseMap.Bedside);
                pose = Pose.InBed;
                inBedSince = Time.time - InBedBeforeLampOff;
                Lights(false, s == Station.InBed, false, false, live);
                SetTv(false, false);
                DoorsNow(false);
                Quilt(true);
                break;
        }
        Windows();
        Place();
    }

    void Lights(bool standard, bool bedside, bool landing, bool kitchen, bool click)
    {
        Lamp(standardLamp, standard, click);
        Lamp(bedsideLamp, bedside, click);
        Lamp(landingLight, landing, click);
        Lamp(kitchenLight, kitchen, click);
    }

    // ================================================================== moving her

    Vector3 SpotAt(int i)
    {
        GraceHouseMap.Spot s = GraceHouseMap.Spots[i];
        return new Vector3(s.X, s.Y, s.Z);
    }

    Vector3 W(Vector3 plan) => house.World(plan.x, plan.y, plan.z);

    // World yaw from one plan point toward another.
    float YawToward(Vector3 fromPlan, Vector3 toPlan)
    {
        Vector3 d = W(toPlan) - W(fromPlan);
        d.y = 0f;
        return d.sqrMagnitude < 1e-6f ? yaw : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    void StandNow(int spot)
    {
        if (pose != Pose.Standing && animator != null && animator.isActiveAndEnabled)
        {
            if (hasSeated) animator.SetBool(SeatedHash, false);
            if (animator.HasState(0, IdleState)) animator.Play(IdleState, 0, 0f);
        }
        if (visual != null) visual.Seated = false;
        pose = Pose.Standing;
        feet = segmentFrom = SpotAt(spot);
        lastSpot = spot;
    }

    void SeatNow()
    {
        StandNow(GraceHouseMap.Armchair);
        feet = seatFeet;
        yaw = seatYaw;
        pose = Pose.Seated;
        if (visual != null) visual.Seated = true;
        if (animator != null && animator.isActiveAndEnabled)
        {
            if (hasSeated) animator.SetBool(SeatedHash, true);
            if (animator.HasState(0, SittingState)) animator.Play(SittingState, 0, 0f);
        }
    }

    // On her way to a spot of the house, from wherever she is (a spot, or partway between two).
    IEnumerator GoTo(int spot)
    {
        while (true)
        {
            if (pose == Pose.Seated) StandUp();
            else if (pose == Pose.InBed) GetOutOfBed();
            else if (pose == Pose.Standing)
            {
                if (route.Count == 0 && lastSpot == spot) yield break;
                if (goal != spot || route.Count == 0) Route(spot);
            }
            yield return null;
        }
    }

    // The shortest way to a spot: from the last spot she passed, or the one she was heading for, whichever is shorter
    // from where she stands (she's always on a straight way between the two).
    void Route(int spot)
    {
        int next = routeSpots.Count > 0 ? routeSpots[0] : -1;
        int from = -1;
        float best = float.MaxValue;
        foreach (int start in new[] { lastSpot, next })
        {
            if (start < 0) continue;
            float d = Vector3.Distance(feet, SpotAt(start)) + GraceHouseMap.PathLength(start, spot);
            if (d < best) { best = d; from = start; }
        }
        if (from < 0) from = GraceHouseMap.Nearest(feet.x, feet.y, feet.z);
        route.Clear();
        routeSpots.Clear();
        goal = spot;
        if (from < 0 || !GraceHouseMap.Path(from, spot, pathBuffer)) return;
        segmentFrom = feet;
        foreach (int s in pathBuffer)
        {
            if (route.Count == 0 && Vector3.Distance(SpotAt(s), feet) < .02f) { lastSpot = s; continue; }
            route.Add(SpotAt(s));
            routeSpots.Add(s);
        }
    }

    IEnumerator Face(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - (body != null ? body.transform.position : W(feet));
        d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) yield break;
        turnTo = worldPoint;
        turning = true;
        while (turning) yield return null;
    }

    void SitDown()
    {
        if (pose != Pose.Standing) return;
        pose = Pose.SittingDown;
        poseT = 0f;
        poseSeconds = seating != null && seating.Data != null ? seating.Data.enterLength : 1.3f;
        poseFrom = feet;
        poseTo = seatFeet;
        if (animator != null && hasSeated) animator.SetBool(SeatedHash, true);
        if (visual != null) visual.Seated = true;
        Sfx.Play("chair.sit", W(feet), .6f);
    }

    void StandUp()
    {
        if (pose != Pose.Seated) return;
        pose = Pose.StandingUp;
        poseT = 0f;
        poseSeconds = seating != null && seating.Data != null ? seating.Data.exitLength : 1.03f;
        poseFrom = feet;
        poseTo = SpotAt(GraceHouseMap.Armchair);
        if (animator != null && hasSeated) animator.SetBool(SeatedHash, false);
        Sfx.Play("chair.stand", W(feet), .6f);
    }

    void GetInBed()
    {
        pose = Pose.InBed;
        inBedSince = Time.time;
        route.Clear();
        routeSpots.Clear();
        goal = -1;
        lastSpot = GraceHouseMap.Bedside;
        Quilt(true);
        Sfx.Play("grace.bed", W(BedMiddle), .7f);
    }

    void GetOutOfBed()
    {
        if (pose != Pose.InBed) return;
        StandNow(GraceHouseMap.Bedside);
        yaw = YawToward(BedMiddle, feet);
        // Placeholder until a quilt thrown back: the made one shows while she's up (the slept-in one has her in it).
        Quilt(false);
        Sfx.Play("grace.bed", W(BedMiddle), .7f);
        Place();
    }

    void Move(float dt)
    {
        if (body == null) return;
        walkingNow = false;
        float speedNow = 0f;
        switch (pose)
        {
            case Pose.SittingDown:
            case Pose.StandingUp:
                poseT += dt / Mathf.Max(.1f, poseSeconds);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(poseT));
                feet = Vector3.Lerp(poseFrom, poseTo, k);
                yaw = Mathf.MoveTowardsAngle(yaw, seatYaw, turnSpeed * dt);
                if (poseT >= 1f)
                {
                    if (pose == Pose.SittingDown) pose = Pose.Seated;
                    else
                    {
                        pose = Pose.Standing;
                        if (visual != null) visual.Seated = false;
                        lastSpot = GraceHouseMap.Armchair;
                        segmentFrom = feet;
                    }
                }
                break;
            case Pose.Standing:
                bool wait = held || mood == Mood.Caught || street && AceInTheWay();
                if (!wait && route.Count > 0)
                {
                    float speed = Mathf.Abs(route[0].z - segmentFrom.z) > .3f ? stairsSpeed : walkSpeed;
                    float left = speed * dt;
                    for (int guard = 0; left > 0f && route.Count > 0 && guard < 4; guard++)
                    {
                        Vector3 target = route[0];
                        Vector3 delta = target - feet;
                        float distance = delta.magnitude;
                        if (distance <= left)
                        {
                            feet = target;
                            left -= distance;
                            lastSpot = routeSpots[0];
                            segmentFrom = target;
                            route.RemoveAt(0);
                            routeSpots.RemoveAt(0);
                            Passed(lastSpot);
                            if (lastSpot >= 0 && lastSpot == GraceHouseMap.Doorway) street = false;
                            continue;
                        }
                        feet += delta / distance * left;
                        left = 0f;
                    }
                    if (route.Count > 0) yaw = Mathf.MoveTowardsAngle(yaw, YawToward(feet, route[0]), 420f * dt);
                    walkingNow = true;
                    speedNow = speed;
                    if (route.Count == 0) goal = goal >= 0 && lastSpot == goal ? goal : -1;
                    HerCreaks();
                }
                else if (turning)
                {
                    Vector3 d = turnTo - body.transform.position;
                    d.y = 0f;
                    float want = d.sqrMagnitude > 1e-6f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : yaw;
                    yaw = Mathf.MoveTowardsAngle(yaw, want, turnSpeed * dt);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, want)) < 2f) turning = false;
                }
                break;
        }
        Animate(walkingNow, speedNow, dt);
        Place();
    }

    // Different start and stop thresholds with a short hold, so the walk clip doesn't flicker at a corner (as NpcJourney).
    void Animate(bool walking, float speed, float dt)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (hasWalkRate && walking)
        {
            float clip = locomotion != null ? locomotion.ClipSpeed : 1.35f;
            animator.SetFloat(WalkRateHash, Mathf.Clamp(speed / Mathf.Max(.3f, clip), .6f, 1.7f));
        }
        if (walking == walkingShown) { walkChange = 0f; return; }
        walkChange += dt;
        if (walkChange < (walking ? .05f : .12f)) return;
        walkingShown = walking;
        walkChange = 0f;
        if (hasWalking) animator.SetBool(IsWalkingHash, walking);
        if (spine != null && walking) spine.Sway(0f);
    }

    void Place()
    {
        if (body == null || house == null) return;
        body.transform.SetPositionAndRotation(W(feet), Quaternion.Euler(0f, yaw, 0f));
    }

    // On the street (Thursday), she waits for Ace to step out of her way rather than walk through.
    bool AceInTheWay()
    {
        if (aceT == null || route.Count == 0) return false;
        Vector3 here = W(feet), ahead = W(route[0]);
        Vector3 d = ahead - here;
        d.y = 0f;
        Vector3 probe = here + (d.sqrMagnitude > 1e-4f ? d.normalized * .7f : Vector3.zero);
        Vector3 a = aceT.position - probe;
        a.y = 0f;
        return a.magnitude < .75f;
    }

    // The ground under a point (the night's solid pavement and her steps), looked for from just above it: the bay over her
    // door is 2.4 m up, and a ray from higher would land on it. The point itself when nothing is there.
    static Vector3 Ground(Vector3 at)
    {
        if (Physics.Raycast(at + Vector3.up * .6f, Vector3.down, out RaycastHit hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point;
        return at;
    }

    /// <summary>In the game camera's view (with a margin), at the feet or the head: somewhere she mustn't appear out of nowhere.</summary>
    public static bool InView(Vector3 point)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        for (int i = 0; i < 2; i++)
        {
            Vector3 v = cam.WorldToViewportPoint(point + Vector3.up * (i == 0 ? .1f : 1.8f));
            if (v.z > 0f && v.x > -.03f && v.x < 1.03f && v.y > -.03f && v.y < 1.03f) return true;
        }
        return false;
    }

    // ================================================================== her lamps, the TV, the windows, the quilt

    void Lamp(Light l, bool on, bool click = true)
    {
        if (l == null || l.enabled == on) return;
        l.enabled = on;
        if (click && nightOn && Time.time >= liveSince) Sfx.Play("grace.lamp", l.transform.position, .5f);
        Windows();
    }

    // The street reads her lamps: a bedroom bay lit while her bedside lamp is on (NightHomes), her front window's curtains
    // glowing while the front room's lamp is on (and the real rooms behind its clear glass, lit or dark).
    void Windows()
    {
        if (house != null && house.house != null) NightHomes.Drive(house.house, 1, bedsideLamp != null && bedsideLamp.enabled);
        bool glow = standardLamp == null || standardLamp.enabled;
        if (frontCurtains != null && curtainsLit != null && glow != curtainsGlow)
        {
            // Only over what was put on them here: if anything else has changed them since, they're left as they are.
            if (Same(frontCurtains.sharedMaterials, glow ? curtainsDark : curtainsLit))
                frontCurtains.sharedMaterials = glow ? curtainsLit : curtainsDark;
            curtainsGlow = glow;
        }
    }

    static bool Same(Material[] a, Material[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    void RememberCurtains()
    {
        curtainsLit = curtainsDark = null;
        curtainsGlow = true;
        if (frontCurtains == null) return;
        Material[] lit = frontCurtains.sharedMaterials;
        // Break-ins 5 split them: the faces toward the street wear the glow (the first half), the faces into the room plain
        // fabric (the second half). Dark: the fabric on both.
        if (lit.Length < 2 || lit.Length % 2 != 0) return;
        int half = lit.Length / 2;
        var dark = new Material[lit.Length];
        for (int i = 0; i < lit.Length; i++) dark[i] = i < half ? lit[i + half] : lit[i];
        curtainsLit = lit;
        curtainsDark = dark;
    }

    void RestoreCurtains()
    {
        if (frontCurtains != null && curtainsDark != null && Same(frontCurtains.sharedMaterials, curtainsDark)) frontCurtains.sharedMaterials = curtainsLit;
        curtainsGlow = true;
        curtainsLit = curtainsDark = null;
    }

    void FindTheScreen()
    {
        tvSlot = -1;
        tvScreenOwn = null;
        if (tv == null) return;
        Material[] m = tv.sharedMaterials;
        for (int i = 0; i < m.Length; i++)
            if (m[i] != null && m[i].name.StartsWith("GH_TV_Screen", StringComparison.Ordinal)) { tvSlot = i; tvScreenOwn = m[i]; break; }
        if (tvLight != null && !tvBaseKnown)
        {
            tvBase = tvLight.intensity > 0f ? tvLight.intensity : 1f;
            tvBaseKnown = true;
        }
    }

    void SetTv(bool on, bool click)
    {
        if (tvOn == on && (on || tvLight == null || !tvLight.enabled)) return;
        tvOn = on;
        if (tvLight != null) tvLight.enabled = on;
        if (tv != null && tvSlot >= 0)
        {
            if (on && tvScreenLit == null && tvScreenOwn != null)
            {
                tvScreenLit = new Material(tvScreenOwn) { name = "GH_TV_Screen (on, while the night runs)", hideFlags = HideFlags.DontSave };
                tvScreenLit.EnableKeyword("_EMISSION");
            }
            Material[] m = tv.sharedMaterials;
            if (tvSlot < m.Length)
            {
                m[tvSlot] = on && tvScreenLit != null ? tvScreenLit : tvScreenOwn;
                tv.sharedMaterials = m;
            }
        }
        if (on)
        {
            if (tvSound == null || !tvSound.Alive) tvSound = Sfx.LoopAt("grace.tv", W(TvScreen), .5f);
        }
        else if (tvSound != null) { tvSound.Stop(.4f); tvSound = null; }
        if (click && nightOn && Time.time >= liveSince) Sfx.Play("grace.lamp", W(TvScreen), .4f);
    }

    // A television: the light jumps as the picture cuts, a little bluer or whiter each time (as the street's TV rooms).
    void Flicker(float dt)
    {
        if (!tvOn) return;
        float now = Time.time;
        if (now >= tvNext)
        {
            tvTarget = UnityEngine.Random.Range(.35f, 1f);
            tvNext = now + (UnityEngine.Random.value < .2f ? UnityEngine.Random.Range(.05f, .15f) : UnityEngine.Random.Range(.3f, 1.6f));
            tvColour = Color.Lerp(new Color(.55f, .7f, 1f), new Color(.75f, .8f, .9f), UnityEngine.Random.value);
        }
        tvLevel = Mathf.MoveTowards(tvLevel, tvTarget, dt * 12f);
        if (tvLight != null)
        {
            tvLight.intensity = tvBase * tvLevel;
            tvLight.color = tvColour;
        }
        if (tvScreenLit != null) tvScreenLit.SetColor(EmissionColorId, tvColour * (.6f + .9f * tvLevel));
    }

    void Quilt(bool asleep)
    {
        if (quiltMade != null && quiltMade.activeSelf == asleep) quiltMade.SetActive(!asleep);
        if (quiltAsleep != null && quiltAsleep.activeSelf != asleep) quiltAsleep.SetActive(asleep);
    }

    // ================================================================== her bedroom doors

    Collider[] LeafColliders()
    {
        var list = new List<Collider>();
        foreach (Transform hinge in new[] { westLeaf, eastLeaf })
            if (hinge != null) list.AddRange(hinge.GetComponentsInChildren<Collider>(true));
        return list.ToArray();
    }

    void DoorsNow(bool open)
    {
        doors = doorsTo = open ? 1f : 0f;
        ApplyDoors();
    }

    void SwingTheDoors(bool open, float seconds, bool hers)
    {
        doorsTo = open ? 1f : 0f;
        doorsRate = 1f / Mathf.Max(.1f, seconds);
        if (hers) Sfx.Play("grace.door", W(DoorsMiddle), .6f);
    }

    void SwingDoors(float dt)
    {
        if (!Mathf.Approximately(doors, doorsTo))
        {
            doors = Mathf.MoveTowards(doors, doorsTo, dt * doorsRate);
            ApplyDoors();
        }
        else if (aceAtDoors)
        {
            aceAtDoors = false;
            PlayerMovement.Release(doorsHold);
        }
        // A leaf is solid only while it stands still, and never into Ace (it swings through the room, where Ace may stand).
        bool still = Mathf.Approximately(doors, doorsTo);
        foreach (Collider c in leafColliders)
        {
            if (c == null) continue;
            bool want = still && !(aceController != null && aceController.enabled && c.bounds.Intersects(aceController.bounds));
            if (c.enabled != want) c.enabled = want;
        }
    }

    void ApplyDoors()
    {
        float e = Mathf.SmoothStep(0f, 1f, doors);
        if (westLeaf != null) westLeaf.localRotation = Quaternion.Euler(0f, Mathf.Lerp(westClosedYaw, westOpenYaw, e), 0f);
        if (eastLeaf != null) eastLeaf.localRotation = Quaternion.Euler(0f, Mathf.Lerp(eastClosedYaw, eastOpenYaw, e), 0f);
    }

    /// <summary>Ace can open the doors: shut and still, Ace in the doorway (either side), nothing else going on.</summary>
    public bool CanOpenDoors(BedroomDoorsZone zone) =>
        nightOn && DoorsShut && !aceAtDoors && hiddenIn == null && mood != Mood.Caught && aceT != null && zone != null && zone.Holds(aceT.position);

    /// <summary>
    /// E in the doorway: at a walk the doors swing open at once and creak (heard 5 m away: her bed is nearer than that);
    /// sneaking, Ace eases them open over about 2 s, quietly. Ace stands still while they swing.
    /// </summary>
    public void AceOpensTheDoors()
    {
        if (!CanOpenDoors(doorsZone)) return;
        bool quietly = ace != null && ace.Sneaking;
        doorsTo = 1f;
        doorsRate = 1f / (quietly ? 2f : .6f);
        aceAtDoors = true;
        PlayerMovement.Hold(doorsHold);
        Vector3 at = W(DoorsMiddle);
        if (quietly)
        {
            QuietOpens++;
            Sfx.Play("night.door.soft", at, .35f);
        }
        else
        {
            CreakyOpens++;
            Sfx.Play("night.door.creak", at);
            NightNoise.Make(at, NoiseKind.Door);
        }
    }

    // ================================================================== the places in her house (made while the night runs)

    void MakeZones()
    {
        RemoveZones();
        cupboard = MakeZone<HidingPlace>("Ace's hiding place: the cupboard under her stairs (while the night runs)", CupboardMiddle, CupboardSize);
        cupboard.home = this;
        cupboard.what = "the cupboard under the stairs";
        wardrobe = MakeZone<HidingPlace>("Ace's hiding place: her wardrobe (while the night runs)", WardrobeMiddle, WardrobeSize);
        wardrobe.home = this;
        wardrobe.what = "her wardrobe";
        doorsZone = MakeZone<BedroomDoorsZone>("Ace's way through her bedroom doors (while the night runs)", DoorwayMiddle, DoorwaySize);
        doorsZone.home = this;
    }

    // A trigger at a place of her house: its middle and size in plan metres, turned with the house.
    T MakeZone<T>(string name, Vector3 middle, Vector3 size) where T : Component
    {
        var go = new GameObject(name) { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        go.SetActive(false);
        go.transform.SetPositionAndRotation(W(middle), transform.rotation);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(size.x, size.z, size.y);   // the rooms' own space is (-X, z, Y)
        T zone = go.AddComponent<T>();
        go.SetActive(true);
        return zone;
    }

    void RemoveZones()
    {
        foreach (Component zone in new Component[] { cupboard, wardrobe, doorsZone })
            if (zone != null) Destroy(zone.gameObject);
        cupboard = wardrobe = null;
        doorsZone = null;
    }

    // ================================================================== Ace hiding

    /// <summary>Ace can hide here: standing in front of it, not hidden already, not caught.</summary>
    public bool CanHide(HidingPlace place) =>
        nightOn && place != null && hiddenIn == null && mood != Mood.Caught && !aceAtDoors && aceT != null && place.Holds(aceT.position);

    /// <summary>
    /// E in front of the cupboard or the wardrobe: Ace's body and capsule are set aside (the view stays where it is) and a
    /// note says how to come out. If her mark was past a third, she comes to where she last saw or heard Ace.
    /// </summary>
    public void Hide(HidingPlace place)
    {
        if (!CanHide(place)) return;
        hiddenIn = place;
        hiddenSince = Time.unscaledTime;
        Hides++;
        PlayerMovement.Hold(this);
        if (aceController != null) aceController.enabled = false;
        if (view != null) view.AceSetAside = true;
        seeing = false;
        Sfx.Play("night.hide", place.transform.position, .6f);
        NightCycle.Note($"Hidden in {place.what}. [{ControlHints.Interact}] Come out", 9999f);
        if (mark > GraceNight.Third && CanSearch && mood != Mood.Searching) StartSearch(noticePoint);
    }

    /// <summary>Ace comes out (E again, or the night ending: the morning finds Ace at home, whole).</summary>
    public void ComeOut() => ComeOut(true);

    void ComeOut(bool sound)
    {
        if (hiddenIn == null) return;
        HidingPlace was = hiddenIn;
        hiddenIn = null;
        if (aceController != null) aceController.enabled = true;
        if (view != null) view.AceSetAside = false;
        PlayerMovement.Release(this);
        NightCycle.ClearNote();
        if (sound) Sfx.Play("night.unhide", was.transform.position, .6f);
    }

    void Hidden()
    {
        if (hiddenIn == null) return;
        bool nightGoesOn = NightCycle.Instance == null || NightCycle.Instance.Now == NightCycle.Phase.Night;
        if (!nightGoesOn) { ComeOut(false); return; }
        if (Time.unscaledTime - hiddenSince < .35f) return;
        Keyboard keys = Keyboard.current;
        bool pressed = Application.isFocused && keys != null && keys.eKey.wasPressedThisFrame || PadInput.Pressed(PadButton.South);
        if (pressed) ComeOut(true);
    }

    // ================================================================== the creaky treads

    void Creak()
    {
        if (view == null || hiddenIn != null || house == null) return;
        Vector3 f = house.Plan(view.AceFeet);
        bool upper = OnUpperCreak(f), lower = OnLowerCreak(f);
        if (upper && !aceOnUpper) Creaked(UpperCreak);
        if (lower && !aceOnLower) Creaked(LowerCreak);
        aceOnUpper = upper;
        aceOnLower = lower;
    }

    static bool OnUpperCreak(Vector3 f) => Mathf.Abs(f.x - UpperCreak.x) < .13f && Mathf.Abs(f.y - UpperCreak.y) < .7f && Mathf.Abs(f.z - UpperCreak.z) < .5f;
    static bool OnLowerCreak(Vector3 f) => Mathf.Abs(f.y - LowerCreak.y) < .13f && Mathf.Abs(f.x - LowerCreak.x) < .7f && Mathf.Abs(f.z - LowerCreak.z) < .5f;

    // Under Ace: heard 5 m away at a walk (her pillow is 4 m from the upper one), 2.5 m under a sneaking foot.
    void Creaked(Vector3 tread)
    {
        bool soft = ace != null && ace.Sneaking;
        Vector3 at = W(tread);
        Creaks++;
        Sfx.Play(soft ? "night.stair.creak.soft" : "night.stair.creak", at, soft ? .5f : 1f);
        NightNoise.Make(at, soft ? SoftCreak : NightNoise.CreakyStairRadius, NoiseKind.CreakyStair);
    }

    // Under her own feet: the same board, only the sound (once the files are in, the player hears her on the stairs).
    void HerCreaks()
    {
        bool upper = OnUpperCreak(feet), lower = OnLowerCreak(feet);
        if (upper && !herOnUpper || lower && !herOnLower) Sfx.Play("night.stair.creak.soft", W(feet), .6f);
        herOnUpper = upper;
        herOnLower = lower;
    }

    // ================================================================== her eyes and ears

    bool CanNotice => Home && !street;
    bool CanSee => CanNotice && !Asleep;
    bool CanSearch => Home && !street && pose != Pose.InBed;

    void Sense(float dt)
    {
        if (mood == Mood.Caught) return;
        float now = Time.time;
        if (now >= nextLook)
        {
            nextLook = now + LookEvery;
            Look();
        }
        if (seeing)
        {
            mark = GraceNight.Seen(mark, seeRate, dt);
            lastNoticed = now;
            noticePoint = lastSeen;
        }
        else if (now - lastNoticed > GraceNight.DrainAfter) mark = GraceNight.Drained(mark, dt);
        if (GraceNight.Caught(mark) && CanNotice) { Catch(); return; }

        switch (mood)
        {
            case Mood.Calm:
                if (mark >= GraceNight.Third && CanNotice) StartLooking();
                break;
            case Mood.Looking:
                if (seeing) lookUntil = Mathf.Max(lookUntil, now + .5f);
                if (now < lookUntil) break;
                if (mark >= GraceNight.SearchFrom && CanSearch) StartSearch(noticePoint);
                else if (mark < GraceNight.Third * .75f) BackToCalm();
                else lookUntil = now + .25f;
                break;
            case Mood.Searching:
                Advance(search);
                if (search.Count == 0) BackToCalm();
                break;
            case Mood.Woken:
                if (now >= wokenUntil && mark < GraceNight.Third * .75f)
                {
                    BackToCalm();
                    if (station == Station.Asleep && pose == Pose.InBed) Lamp(bedsideLamp, false);
                }
                break;
        }
    }

    void BackToCalm()
    {
        mood = Mood.Calm;
        held = false;
        search.Clear();
    }

    // "Hm?": she stops, and looks where she noticed Ace (her head; standing, she turns to face it if it's behind her).
    void StartLooking()
    {
        mood = Mood.Looking;
        lookUntil = Time.time + GraceNight.LookSeconds;
        held = true;
        Notices++;
        Say(Barks.PoolLine("grace", "grace.notice"), "Hm?");
        Sfx.Play("grace.notice", Speaker != null ? Speaker.position : W(feet), .7f);
        if (pose == Pose.Standing && body != null)
        {
            Vector3 d = noticePoint - body.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f && Vector3.Angle(body.transform.forward, d) > 70f)
            {
                turnTo = noticePoint;
                turning = true;
            }
        }
    }

    // She comes to where she last saw or heard Ace: the nearest spot of her house to it, on its floor; looks round there,
    // and goes back to what she was doing.
    void StartSearch(Vector3 point)
    {
        mood = Mood.Searching;
        held = false;
        Searches++;
        search.Clear();
        search.Push(Search(point));
    }

    IEnumerator Search(Vector3 point)
    {
        Vector3 p = house.Plan(point);
        int spot = GraceHouseMap.Nearest(p.x, p.y, p.z);
        if (spot < 0) yield break;
        yield return GoTo(spot);
        SearchedNearest = Mathf.Min(SearchedNearest, Vector3.Distance(W(feet), point));
        yield return Face(point);
        if (beats != null) beats.PlayClip(NpcBeats.Clip.LookingAround, GraceNight.SearchSeconds);
        float until = Time.time + GraceNight.SearchSeconds;
        while (Time.time < until) yield return null;
    }

    // Asleep, a loud sound near her: her lamp on, "Hello?", and a look round from her bed for 20 s.
    void Wake(Vector3 at)
    {
        mood = Mood.Woken;
        wokenUntil = Time.time + GraceNight.WakeSeconds;
        Wakes++;
        Lamp(bedsideLamp, true);
        mark = GraceNight.Heard(mark);
        lastSoundAt = lastNoticed = Time.time;
        noticePoint = at;
        NightLines lines = NightLines.Current;
        Say(lines != null ? lines.FindLine("grace.notice.03")?.text : null, "Hello?");
        Sfx.Play("grace.woken", W(Pillow), .7f);
    }

    void Heard(NightNoiseEvent noise)
    {
        if (!nightOn || body == null || away || street || mood == Mood.Caught || house == null) return;
        if (!Inside(noise.position)) return;
        if (!noise.HeardAt(Ear())) return;
        if (Asleep)
        {
            if (!Loud(noise.kind)) return;
            WokeTo = noise.kind;
            Wake(noise.position);
            return;
        }
        float now = Time.time;
        if (!seeing) noticePoint = noise.position;
        lastNoticed = now;
        if (mood == Mood.Woken) wokenUntil = now + GraceNight.WakeSeconds;
        if (now - lastSoundAt < GraceNight.SoundGap) return;
        lastSoundAt = now;
        SoundsHeard++;
        LastHeard = noise.kind;
        mark = GraceNight.Heard(mark);
    }

    static bool Loud(NoiseKind kind) =>
        kind == NoiseKind.Step || kind == NoiseKind.CreakyStair || kind == NoiseKind.Door || kind == NoiseKind.Knock || kind == NoiseKind.LockPick;

    // Twice a second: can she see Ace? Ace's head and chest (lower while Ace is crouched), from her eyes: in her view, within
    // her range for the light where Ace is, and nothing solid between.
    void Look()
    {
        seeing = false;
        seeRate = 0f;
        if (!CanSee || aceT == null || view == null || hiddenIn != null) return;
        Vector3 aceFeet = view.AceFeet;
        if (!Inside(aceFeet + Vector3.up * .5f)) return;
        Vector3 eye = Eye(out Vector3 forward, out float half, out bool tvHasHer);
        bool crouched = ace != null && ace.Crouch > .5f;
        float tall = aceBody != null && aceBody.Worn && aceBody.Height > .5f
            ? (crouched ? aceBody.CrouchedHeight : aceBody.Height)
            : (crouched ? 1.3f : 2f);
        bool sneaking = ace != null && ace.Sneaking;
        NightTorch torchNow = NightWalk.Instance != null ? NightWalk.Instance.Torch : null;
        bool torch = torchNow != null && torchNow.On;
        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        for (int k = 0; k < 2; k++)
        {
            Vector3 point = aceFeet + Vector3.up * tall * (k == 0 ? .88f : .55f);
            Vector3 to = point - eye;
            float distance = to.magnitude;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float off = flat.sqrMagnitude > 1e-6f && flatForward.sqrMagnitude > 1e-6f ? Vector3.Angle(flatForward, flat) : 0f;
            bool lit = Lit(point);
            float rate = GraceNight.SeeRate(lit, distance, GraceNight.Range(lit, torch), off, half, sneaking, torch, tvHasHer);
            if (rate <= 0f || !Clear(eye, point)) continue;
            seeing = true;
            seeRate = Mathf.Max(seeRate, rate);
        }
        if (seeing) lastSeen = aceFeet;
    }

    // Her eyes, which way they look, and how wide: from her pillow in bed (a look round the room); in her armchair with the
    // TV on and nothing on her mind, the slice toward the screen; otherwise her head's way (seated, the head turns no
    // further than a neck does; standing, she turns).
    Vector3 Eye(out Vector3 forward, out float half, out bool tvHasHer)
    {
        tvHasHer = false;
        if (pose == Pose.InBed)
        {
            Vector3 pillow = W(Pillow);
            forward = W(RoomFromBed) - pillow;
            half = 80f;
            return pillow;
        }
        Transform b = body.transform;
        Transform head = lookAt != null ? lookAt.HeadBone : null;
        bool sat = pose == Pose.Seated || pose == Pose.SittingDown;
        Vector3 eye = head != null ? head.position + b.forward * .08f : b.position + Vector3.up * (sat ? 1.35f : 1.85f);
        forward = b.forward;
        if (WatchingTv)
        {
            tvHasHer = true;
            half = GraceNight.TvCone * .5f;
            return eye;
        }
        half = GraceNight.Cone * .5f;
        if (mood != Mood.Calm)
        {
            Vector3 want = (mood == Mood.Caught && aceT != null ? aceT.position : noticePoint) - b.position;
            want.y = 0f;
            if (want.sqrMagnitude > 1e-4f)
            {
                float turn = Vector3.SignedAngle(b.forward, want, Vector3.up);
                float neck = sat ? 72f : 180f;
                forward = Quaternion.AngleAxis(Mathf.Clamp(turn, -neck, neck), Vector3.up) * b.forward;
            }
        }
        return eye;
    }

    Vector3 Ear()
    {
        if (pose == Pose.InBed || body == null) return W(Pillow);
        Transform head = lookAt != null ? lookAt.HeadBone : null;
        return head != null ? head.position : body.transform.position + Vector3.up * 1.7f;
    }

    // Lit where Ace is: one of her lights on and near (its reach, less a little: the edge of a lamp's pool is dark), on
    // that floor.
    bool Lit(Vector3 point)
    {
        foreach (Light l in lamps)
        {
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
            Vector3 d = point - l.transform.position;
            if (Mathf.Abs(d.y) > 2.2f) continue;
            float reach = l.range * (l == tvLight ? .6f : .8f);
            if (d.sqrMagnitude < reach * reach) return true;
        }
        return false;
    }

    // Nothing solid between her eyes and the point but Ace's own capsule (her own body is never solid).
    bool Clear(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        if (length < .01f) return true;
        int n = Physics.RaycastNonAlloc(from, d / length, Hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = Hits[i].collider;
            if (c == null) continue;
            if (aceT != null && c.transform.IsChildOf(aceT)) continue;
            if (body != null && c.transform.IsChildOf(body.transform)) continue;
            return false;
        }
        return true;
    }

    // In her house: inside its walls, from the cellar of the ground floor to the top of the first.
    bool Inside(Vector3 world)
    {
        Vector3 p = house.Plan(world);
        return p.x > -.05f && p.x < 5.47f && p.y > -.05f && p.y < 4.07f && p.z > -.6f && p.z < 5f;
    }

    // Full: she has caught Ace. "Ace?! What on earth—", and the night ends on "Caught." (NightCycle.Caught: the placeholder
    // until getting caught has its own chunk; whatever Ace took tonight goes back).
    void Catch()
    {
        mood = Mood.Caught;
        mark = 1f;
        held = true;
        Catches++;
        if (pose == Pose.Standing && aceT != null)
        {
            turnTo = aceT.position;
            turning = true;
        }
        Say(Barks.PoolLine("grace", "grace.caught"), "Ace?! What on earth—", 3f, force: true);
        Sfx.Play2D("grace.caught");
        bool ended = NightCycle.Instance != null && NightCycle.Instance.Caught();
        Debug.Log($"[Grace at home] She caught Ace at {GraceNight.Clock(hour)} ({Doing}); " +
                  (ended ? "the night ends on \"Caught.\"" : "no night cycle to end the night (a lab without one)") + ".");
    }

    // A line from her (over her head; from her pillow in bed). Not twice within a moment, unless it must be said (caught).
    void Say(string line, string fallback, float seconds = 0f, bool force = false)
    {
        string text = string.IsNullOrWhiteSpace(line) ? fallback : line;
        if (Speaker == null || !force && Time.time - lastBark < .8f) return;
        lastBark = Time.time;
        Barks.Say(Speaker, "grace", text, seconds);
    }

    void FindAce()
    {
        if (ace != null) return;
        ace = FindAnyObjectByType<PlayerMovement>();
        if (ace == null) return;
        aceT = ace.transform;
        aceController = ace.GetComponent<CharacterController>();
        view = ace.GetComponent<CafeViewMode>();
        aceBody = ace.GetComponent<AceBody>();
    }

    // ================================================================== reports

    /// <summary>What she's doing, in a few words (reports, the checks' notes).</summary>
    public string Doing
    {
        get
        {
            if (!nightOn) return "not at night";
            if (away) return outAllNight ? "out all night" : "out (her house dark)";
            if (street) return "on her way home along the pavement";
            if (mood == Mood.Caught) return "has caught Ace";
            if (mood == Mood.Woken) return "woken, her lamp on, looking round from her bed";
            if (mood == Mood.Searching) return "come to look where she noticed Ace";
            if (mood == Mood.Looking) return "stopped to look (\"Hm?\")";
            switch (pose)
            {
                case Pose.Seated: return tvOn ? "in her armchair, watching TV" : "in her armchair";
                case Pose.SittingDown: return "sitting down in her armchair";
                case Pose.StandingUp: return "getting up from her armchair";
                case Pose.InBed: return station == Station.Asleep ? (bedsideLamp != null && bedsideLamp.enabled ? "in bed, about to turn her lamp off" : "asleep") : "in bed, her lamp on";
            }
            if (route.Count > 0) return $"walking ({Step}), to {(goal >= 0 ? GraceHouseMap.Spots[goal].name : "the next spot")}";
            return lastSpot >= 0 ? GraceHouseMap.Spots[lastSpot].name : "standing";
        }
    }

    /// <summary>Which of her lights are on (reports).</summary>
    public string LightsNow()
    {
        var on = new List<string>();
        if (standardLamp != null && standardLamp.enabled) on.Add("the standard lamp");
        if (tvOn) on.Add("the TV");
        if (kitchenLight != null && kitchenLight.enabled) on.Add("the kitchen light");
        if (landingLight != null && landingLight.enabled) on.Add("the landing light");
        if (bedsideLamp != null && bedsideLamp.enabled) on.Add("her bedside lamp");
        return on.Count == 0 ? "all dark" : string.Join(", ", on);
    }

    public bool Lit(Light l) => l != null && l.enabled;
    public bool FrontWindowGlows => curtainsLit == null || curtainsGlow;

    public string Describe() =>
        $"Grace at home: {Doing} at {GraceNight.Clock(hour)} ({(plan == null ? "no night" : plan == GraceNight.Thursday ? "her Thursday" : plan == OutAll ? "out all night" : "her usual night")}, " +
        $"step {stepNow}: {Step}); lights: {LightsNow()}; her bedroom doors {(DoorsShut ? "shut" : DoorsOpen ? "open" : "swinging")}; " +
        $"her mark {mark:0.00}{(seeing ? " (she sees Ace)" : "")}; noticed {Notices}, came to look {Searches}, woken {Wakes}, heard {SoundsHeard}, " +
        $"creaks {Creaks}, doors opened {CreakyOpens} creaking and {QuietOpens} quietly, Ace hid {Hides}, caught {Catches}.";
}
