using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// GRACE'S HOUSE, INSIDE (the break-ins, chunk A: claude/break-ins-spec.md sections 4 and 8)
//
// Her ground floor and her bedroom, built inside the saffron house's shell by
// Fixit Fidget > Night > Break-ins 1 - Build Grace's house inside (GraceHouseSteps).
// This component runs them:
//
//   * at night her lamps are hers: with Grace at home (GraceAtHome, chunk C, 6 Oct) she switches them one by one as her
//     night goes; without her (no GraceAtHome) they are all on all night, as before;
//   * with the break-ins switched on (NightWalk.breakIns: on in the real game since 30 Sept;
//     a lab can switch it off), her front door is Ace's way in: on the stoop the prompt reads
//     "Let yourself in" (GraceDoorZone, a place made here while the night runs), E opens the
//     door, and it minds Ace's capsule from there (open while Ace is in the doorway or just
//     inside, shut behind Ace on the pavement). Until 30 Sept it opened by itself as Ace
//     stepped up, with nothing on screen to say so. Walking in turns the camera to look in
//     from the street, like a doll's house with its front open: the house's outer shell steps
//     aside (it still casts its shadow), the walls toward the camera fade to a ghost above sill
//     height as the café's do (30 Sept: a fade, not a slide; SeeThroughMaterials), and the floor
//     above Ace is hidden. The player can still orbit and zoom. Walking out puts it all back;
//   * on her stairs, Ace keeps to the flight going down (a capsule walking down a 40°
//     flight would otherwise leave it at the top and land at the bottom).
//
// First person (V) shows the house as it is. By day nothing here runs but the rooms
// themselves, seen through her window and her open door.
//
// The rooms are built in "plan" metres (claude/break-ins-spec.md section 4): X along
// West Street from the south wall (north is +X), Y from the back wall toward the street,
// z up from the ground floor. This object sits at the inside corner of the back and
// south walls, on the ground floor, so its own space is (-X, z, Y).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GraceHouse : MonoBehaviour
{
    [Serializable]
    public sealed class Wall
    {
        public string name = "";
        [Tooltip("0: the ground floor, 1: the first floor.")]
        public int storey;
        [Tooltip("The wall up to the cut: always drawn.")]
        public Renderer lower;
        [Tooltip("The wall above the cut: what fades to a ghost. Null when the whole wall is below the cut.")]
        public Transform upper;
        [Tooltip("An outside wall: it comes down whenever the camera is outside it.")]
        public bool perimeter;
        [Tooltip("Toward the outside (perimeter walls), in this object's space.")]
        public Vector3 outward;
        [Tooltip("Things hung on it above the cut: they fade with it.")]
        public Renderer[] hanging = Array.Empty<Renderer>();

        [NonSerialized] public float progress, clearFor, keep = 1f;
        [NonSerialized] public bool down, measured, worn;
        [NonSerialized] public Renderer upperRenderer;
        [NonSerialized] public Bounds full;              // the whole wall as built, in the world
        [NonSerialized] public float cutY;               // world height of the cut: solid below, the ghost above
    }

    [Header("Built by Fixit Fidget > Night > Break-ins 1")]
    public Transform house;
    public StreetDoor door;
    public Transform groundFloor, firstFloor;
    [Tooltip("Ceilings: hidden in the overhead view while Ace is inside, drawn in first person.")]
    public Renderer[] ceilings = Array.Empty<Renderer>();
    public Wall[] walls = Array.Empty<Wall>();
    [Tooltip("Her lamps: on at night (switched one by one by GraceAtHome when she's at home).")]
    public Light[] nightLights = Array.Empty<Light>();
    [Tooltip("The first floor, above the ground floor (metres).")]
    public float firstFloorAt = 2.4f;
    [Tooltip("The house inside its walls, in this object's space (x -5.42..0, z 0..4.02).")]
    public Vector2 insideX = new Vector2(-5.42f, 0f), insideZ = new Vector2(0f, 4.02f);
    [Tooltip("Her stairs, in this object's space: where Ace is kept to the flight going down.")]
    public Vector2 stairsX = new Vector2(-2.62f, .02f), stairsZ = new Vector2(-.02f, 2.95f);

    [Header("Looking in (the camera turns to this while Ace is inside)")]
    [Range(0f, 360f)] public float houseYaw = 258f;
    [Range(38f, 68f)] public float housePitch = 52f;
    [Range(8f, 22f)] public float houseDistance = 13f;
    [Tooltip("Seconds a wall takes to fade to its ghost, or back.")]
    [Range(.05f, 1f)] public float slideSeconds = .35f;
    [Tooltip("Seconds a wall waits, clearly out of the way, before it comes back.")]
    [Range(0f, 2f)] public float riseDelay = .5f;
    [Tooltip("How much of a wall above the cut is still drawn while it is out of the way: .2 is one dot in five.")]
    [Range(.05f, .6f)] public float ghost = .2f;
    [Tooltip("Over how many metres above the cut the wall feathers from solid to the ghost.")]
    [Range(.05f, 1.5f)] public float feather = .45f;

    [Header("Her door, for Ace (break-ins only)")]
    [Tooltip("How far to either side of the doorway's middle Ace can be for the door to open (metres).")]
    public float doorReach = .9f;
    [Tooltip("How far inside (Ace's middle, behind the door's plane) the door stays open: the capsule must be clear of the leaf before it shuts.")]
    public float doorInside = .65f;
    [Tooltip("How far out in front of it (on the stoop) Ace can be let in from, and how far the way-in zone reaches.")]
    public float doorOutside = 1.6f;
    [Tooltip("In front of the door's plane by less than this, Ace is in the doorway: the door holds itself open there and just inside (Door Inside). Further out, on the stoop, it opens only once Ace is let in (E).")]
    public float doorwayDepth = .35f;
    [Tooltip("Once let in (E on the stoop), the door stays open this long for Ace to walk through (seconds); walking away off the stoop ends it sooner.")]
    public float letInFor = 8f;

    // ---- the undo record (Fixit Fidget > Night > Break-ins 1 - Take Grace's house back out) ----
    [HideInInspector] public MeshFilter[] changedParts = Array.Empty<MeshFilter>();
    [HideInInspector] public Mesh[] meshesBefore = Array.Empty<Mesh>();
    [HideInInspector] public GameObject[] addedToHouse = Array.Empty<GameObject>();
    [HideInInspector] public GameObject[] hiddenInHouse = Array.Empty<GameObject>();
    [HideInInspector] public Transform[] movedMarkers = Array.Empty<Transform>();
    [HideInInspector] public Vector3[] markersBefore = Array.Empty<Vector3>();
    [HideInInspector] public Vector3 doorBefore;
    [HideInInspector] public Vector3 leafColliderCentreBefore, leafColliderSizeBefore;

    /// <summary>The lab asks for the night at her door: 1 to play it, 2 to walk the house by itself (a check); or 3
    /// (<see cref="WholeGameLab"/>), the whole game from Day 1: her door opens in every night, and Ace isn't moved.</summary>
    public const string LabKey = "FixitFidget.GraceHouse.Lab";
    /// <summary>Fixit Fidget > Playtest > Play the whole game from Day 1: the break-ins on for the whole session.</summary>
    public const int WholeGameLab = 3;
    /// <summary>The part of her house over the door (split off the trim by the build): solid from 2.35 m up, by night.</summary>
    public const string BracketsName = "Bay brackets over the door";

    public static GraceHouse Instance { get; private set; }
    /// <summary>True while Ace is inside and the camera looks in from the street.</summary>
    public bool Viewing => viewing;
    /// <summary>True while the first floor is the one shown (Ace is upstairs).</summary>
    public bool Upstairs => upstairs;
    /// <summary>The first floor is hidden right now (Ace downstairs, the camera looking in from the street): anyone up there
    /// is hidden with it (GraceAtHome).</summary>
    public bool FirstFloorHidden => viewing && !upstairs && view != null && view.OverheadShown;
    public bool LightsOn => lightsOn;
    /// <summary>How often the stairs kept Ace on a flight going down, this session.</summary>
    public int StairCatches { get; private set; }
    /// <summary>How often Ace was let in at her door (E on the stoop), this session.</summary>
    public int LetIns { get; private set; }
    /// <summary>How often E found her door locked (the break-ins off), this session.</summary>
    public int FoundLocked { get; private set; }
    /// <summary>Ace stands on her stoop, in front of the door (within Door Reach and Door Outside, outside the doorway).</summary>
    public bool AceOnStoop => onStoop;
    /// <summary>The way in is offered: Ace on the stoop, the door not yet open for Ace.</summary>
    public bool DoorPromptShown => aceT != null && door != null && onStoop && !(door.IsOpen || Time.time < letInUntil);
    /// <summary>What the stoop's prompt says: the way in, or a door to try when the break-ins are off.</summary>
    public string DoorPrompt => BreakInsOn ? "Let yourself in" : "Try the door";
    /// <summary>The break-ins are on tonight (NightWalk.breakIns, the night running).</summary>
    public static bool BreakInsOn => NightWalk.Instance != null && NightWalk.Instance.Active && NightWalk.Instance.breakIns;
    public const string LockedNote = "Locked. Her door doesn't open for Ace tonight.";
    /// <summary>Plan metres (X, Y, z) to the world.</summary>
    public Vector3 World(float X, float Y, float z = 0f) => transform.TransformPoint(new Vector3(-X, z, Y));
    /// <summary>The world to plan metres (X, Y, z).</summary>
    public Vector3 Plan(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return new Vector3(-p.x, p.z, p.y);
    }

    static int labRequest;
    GraceAtHome resident;
    CafeViewMode view;
    CafeDaylight daylight;
    CharacterController ace;
    Transform aceT;
    Collider leafCollider;
    GraceDoorZone wayIn;
    float letInUntil;
    bool lightsOn, viewing, upstairs, shellHidden, holdingDoor, aceWasGrounded, labStarted, onStoop, wasNight, saidTonight;
    readonly List<(Renderer renderer, ShadowCastingMode mode)> shell = new();
    Renderer[] upstairsParts = Array.Empty<Renderer>();
    readonly Dictionary<Renderer, int> hideReasons = new();
    const int HiddenStorey = 1, HiddenCeiling = 2, HiddenOnWall = 4, HiddenShell = 8, HiddenWallTop = 16;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReadLabRequest()
    {
        labRequest = 0;
#if UNITY_EDITOR
        labRequest = PlayerPrefs.GetInt(LabKey, 0);
        if (labRequest != 0)
        {
            PlayerPrefs.DeleteKey(LabKey);
            PlayerPrefs.Save();
        }
#endif
    }

    void OnEnable()
    {
        Instance = this;
        resident = GetComponent<GraceAtHome>();
        if (house != null) NightHomes.RealGroundFloors.Add(house);
        leafCollider = door != null && door.hinge != null ? door.hinge.GetComponentInChildren<Collider>(true) : null;
        SetLights(false);
        SetSolid(false);
    }

    void OnDisable()
    {
        if (viewing) Leave();
        ReleaseDoor();
        RemoveTheWayIn();
        wasNight = false;
        if (leafCollider != null) leafCollider.enabled = true;
        SetLights(false);
        SetSolid(true);
        if (house != null) NightHomes.RealGroundFloors.Remove(house);
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        NightWalk night = NightWalk.Instance;
        bool nightNow = night != null && night.Active;
        if (nightNow != lightsOn) SetLights(nightNow);
        if (nightNow != solidOn) SetSolid(nightNow);
        if (labRequest == WholeGameLab && night != null && CafeLab.Active)
        {
            // The whole game from Day 1: nothing moves; her door simply opens for Ace in every night of this session.
            labRequest = 0;
            night.breakIns = true;
            Debug.Log("[Break-ins] The whole game from Day 1: Grace's door opens for Ace in every night of this session.");
        }
        if (labRequest != 0 && !labStarted && nightNow && CafeLab.Active) StartLab(night);

        bool breakIns = nightNow && night.breakIns;
        FindAce();
        // The way in stands on her stoop for the whole night (with the break-ins off it says the door is locked).
        if (nightNow && !wasNight) { MakeTheWayIn(); saidTonight = false; }
        else if (!nightNow && wasNight) RemoveTheWayIn();
        wasNight = nightNow;
        if (nightNow && !saidTonight && aceT != null)
        {
            saidTonight = true;
            Debug.Log("[Break-ins] Tonight Grace's door " + (breakIns
                ? "opens for Ace: E on her stoop (\"Let yourself in\")."
                : "is locked for Ace: the break-ins are off (NightWalk.breakIns; Fixit Fidget > Night > Break-ins 4)."));
        }
        if (!breakIns || aceT == null)
        {
            if (viewing) Leave();
            ReleaseDoor();
            letInUntil = 0f;
            onStoop = breakIns == false && aceT != null && door != null && OnStoop(aceT.position);
            if (leafCollider != null && !leafCollider.enabled) leafCollider.enabled = true;
            return;
        }

        DoorForAce();
        Vector3 local = transform.InverseTransformPoint(aceT.position);
        KeepToTheStairs(local);
        // In once Ace's middle is well past the door's plane; out again once it is back over the threshold.
        bool insideNow = local.x > insideX.x - .1f && local.x < insideX.y + .1f && local.y > -1f && local.y < firstFloorAt + 3f
                         && local.z > insideZ.x - .1f && local.z < insideZ.y + (viewing ? .1f : -.3f);
        if (insideNow && !viewing) Enter();
        else if (!insideNow && viewing) Leave();
        if (viewing) Present();
    }

    // ------------------------------------------------------------------ her lamps

    void SetLights(bool on)
    {
        lightsOn = on;
        // With Grace at home her lamps are hers to switch, one by one (GraceAtHome).
        if (resident != null && resident.isActiveAndEnabled) return;
        foreach (Light l in nightLights) if (l != null) l.enabled = on;
    }

    // ------------------------------------------------------------------ solid by night only

    // Her rooms are solid only while the night runs, the only time anyone walks in them. By day nothing should find
    // them: the day's sightings of her doorstep (HomeSightings) look through her house exactly as they did before
    // (with the rooms solid by day, her own ceilings hid her doorstep from 2-6 more of the camera's 36 turns).
    Collider[] solid;
    bool solidOn = true;

    void SetSolid(bool on)
    {
        if (solid == null)
        {
            var list = new List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (c.enabled) list.Add(c);
            // And the brackets over her door (a part of the house, not of the rooms): at 2.35 m they only matter to
            // Ace's head at night; by day they hid her doorstep from the zoomed-out camera at 4 of 36 turns.
            Transform brackets = house != null ? house.Find(BracketsName) : null;
            if (brackets != null)
                foreach (Collider c in brackets.GetComponents<Collider>()) if (c.enabled) list.Add(c);
            solid = list.ToArray();
        }
        solidOn = on;
        foreach (Collider c in solid) if (c != null) c.enabled = on;
    }

    // ------------------------------------------------------------------ her door, for Ace

    void FindAce()
    {
        if (aceT != null) return;
        view = FindAnyObjectByType<CafeViewMode>();
        if (view == null) return;
        aceT = view.transform;
        ace = view.GetComponent<CharacterController>();
    }

    // On the stoop the door opens for Ace only once Ace is let in (E: LetAceIn), and stays open for a
    // moment for Ace to walk through. In the doorway or just inside it holds itself open, as it does for
    // the people who live here, so Ace is never shut in the leaf's way; on the pavement it shuts behind
    // Ace. Its leaf is solid only while it stands still: a moving leaf would shove the capsule, so while it
    // swings Ace passes through it (it swings into the hall, where Ace stands, whichever way Ace is going).
    void DoorForAce()
    {
        if (door == null) return;
        Vector3 p = aceT.position;
        float outside = door.Outside(p);
        bool inReach = InReach(p, outside);
        onStoop = inReach && outside >= doorwayDepth;
        if (!inReach) letInUntil = 0f;   // walked away: next time it is E again
        bool wants = inReach && (outside < doorwayDepth || Time.time < letInUntil);
        if (wants) { door.Hold(this, .3f); holdingDoor = true; }
        else ReleaseDoor();
        if (leafCollider != null)
        {
            bool still = door.IsClosed || door.OpenAmount >= .999f;
            if (leafCollider.enabled != still) leafCollider.enabled = still;
        }
    }

    // Within Door Reach of the doorway's middle, from just inside (Door Inside) to the front of the stoop (Door Outside).
    bool InReach(Vector3 p, float outside)
    {
        Vector3 fromDoor = p - door.DoorwayPoint;
        fromDoor.y = 0f;
        float across = Vector3.Dot(fromDoor, door.transform.right);
        return Mathf.Abs(across) < doorReach && outside > -doorInside && outside < doorOutside;
    }

    bool OnStoop(Vector3 p)
    {
        float outside = door.Outside(p);
        return InReach(p, outside) && outside >= doorwayDepth;
    }

    /// <summary>
    /// E on her stoop ("Let yourself in", GraceDoorZone; also the walk check's press): with the break-ins on the
    /// door opens and stays open for Ace to walk through (Let In For); off, it is locked and a note says so.
    /// </summary>
    public void LetAceIn()
    {
        if (door == null) return;
        if (!BreakInsOn)
        {
            FoundLocked++;
            NightCycle.Note(LockedNote);
            Sfx.Play("night.door.locked", door.DoorwayPoint);
            return;
        }
        FindAce();
        if (aceT == null || !OnStoop(aceT.position)) return;
        letInUntil = Time.time + letInFor;
        LetIns++;
        Sfx.Play("night.door.open", door.DoorwayPoint);
    }

    // The way in: a trigger over the stoop and the doorway, so the interactor offers it wherever Ace stands
    // there (a zone, as the café's "Call it a night" is). Made while the night runs, never saved.
    void MakeTheWayIn()
    {
        if (wayIn != null || door == null) return;
        var go = new GameObject("Ace's way in at Grace's door (while the night runs)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        Vector3 outward = door.Outward;
        float depth = doorOutside + doorInside;
        go.transform.SetPositionAndRotation(door.DoorwayPoint + outward * (doorOutside - doorInside) * .5f, Quaternion.LookRotation(outward, Vector3.up));
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.1f, 0f);
        box.size = new Vector3(doorReach * 2f, 2.2f, depth);
        wayIn = go.AddComponent<GraceDoorZone>();
        wayIn.house = this;
    }

    void RemoveTheWayIn()
    {
        if (wayIn == null) return;
        if (Application.isPlaying) Destroy(wayIn.gameObject); else DestroyImmediate(wayIn.gameObject);
        wayIn = null;
    }

    void ReleaseDoor()
    {
        if (!holdingDoor || door == null) return;
        door.Release(this);
        holdingDoor = false;
    }

    // ------------------------------------------------------------------ her stairs

    // Walking down a flight, the capsule leaves the slope at the top: it is moved sideways, then gravity starts
    // from nothing. On her stairs only, a capsule that was on the flight last frame and is just off it now is put
    // back down on it (if it is within half a metre).
    void KeepToTheStairs(Vector3 local)
    {
        if (ace == null || !ace.enabled)
        {
            aceWasGrounded = false;
            return;
        }
        bool onStairs = local.x > stairsX.x && local.x < stairsX.y && local.z > stairsZ.x && local.z < stairsZ.y
                        && local.y > -.5f && local.y < firstFloorAt + 1.5f;
        bool grounded = ace.isGrounded;
        if (onStairs && !grounded && aceWasGrounded && ace.velocity.y <= .05f)
        {
            float radius = ace.radius * Mathf.Abs(aceT.lossyScale.x);
            Vector3 centre = aceT.TransformPoint(ace.center);
            float half = Mathf.Max(0f, ace.height * .5f * Mathf.Abs(aceT.lossyScale.y) - radius);
            if (Physics.CapsuleCast(centre + Vector3.up * half, centre - Vector3.up * half, radius * .95f, Vector3.down,
                                    out RaycastHit hit, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                ace.Move(Vector3.down * (hit.distance + .01f));
                grounded = ace.isGrounded;
                StairCatches++;
            }
        }
        aceWasGrounded = grounded;
    }

    // ------------------------------------------------------------------ looking in

    void Enter()
    {
        viewing = true;
        upstairs = AceFeetLocalY() > firstFloorAt - .3f;
        if (view != null) view.EnterHouseView(houseYaw, housePitch, houseDistance);
        // Indoors the night's ambient goes down (CafeDaylight.indoorAmbient): the rooms are dark between her lamps.
        if (daylight == null) daylight = FindAnyObjectByType<CafeDaylight>();
        if (daylight != null) daylight.SetIndoors(true);
        NightSeeThrough seeThrough = NightWalk.Instance != null ? NightWalk.Instance.SeeThrough : null;
        if (seeThrough != null) seeThrough.Leave(house, true);
        // The shell: every renderer of the house but the rooms (and the lit panes the night added to it).
        shell.Clear();
        if (house != null)
            foreach (Renderer r in house.GetComponentsInChildren<Renderer>(true))
                if (r != null && !r.transform.IsChildOf(transform)) shell.Add((r, r.shadowCastingMode));
        upstairsParts = firstFloor != null ? firstFloor.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        foreach (Wall w in walls) Measure(w);
    }

    void Leave()
    {
        viewing = false;
        if (view != null) view.ExitHouseView();
        if (daylight != null) daylight.SetIndoors(false);
        HideShell(false);
        shell.Clear();
        foreach (Wall w in walls)
        {
            w.progress = 0f; w.down = false; w.clearFor = 0f;
            Apply(w);
        }
        foreach (Renderer r in new List<Renderer>(hideReasons.Keys)) SetHidden(r, ~0, false);
        hideReasons.Clear();
        // Only now, with everything shown as it was, may the night's see-through turn her house dotted again.
        NightSeeThrough seeThrough = NightWalk.Instance != null ? NightWalk.Instance.SeeThrough : null;
        if (seeThrough != null) seeThrough.Leave(house, false);
    }

    void Present()
    {
        bool overhead = view != null && view.OverheadShown;
        HideShell(overhead);
        float feet = AceFeetLocalY();
        if (!upstairs && feet > firstFloorAt - .3f) upstairs = true;
        else if (upstairs && feet < firstFloorAt - .5f) upstairs = false;
        foreach (Renderer r in upstairsParts) SetHidden(r, HiddenStorey, overhead && !upstairs);
        foreach (Renderer c in ceilings) SetHidden(c, HiddenCeiling, overhead);
        StepWalls(overhead);
    }

    float AceFeetLocalY()
    {
        if (aceT == null) return 0f;
        Vector3 feet = view != null ? view.AceFeet : aceT.position;
        return transform.InverseTransformPoint(feet).y;
    }

    // The shell still casts its shadow while it is out of the way, so the moon doesn't pour in through a
    // missing roof; what never cast one is simply not drawn.
    void HideShell(bool hide)
    {
        if (hide == shellHidden && !hide) return;
        shellHidden = hide;
        foreach (var (r, mode) in shell)
        {
            if (r == null) continue;
            if (mode == ShadowCastingMode.Off) SetHidden(r, HiddenShell, hide);
            else r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : mode;
        }
    }

    // A renderer can be hidden for more than one reason at once (its floor, its wall, the ceiling rule): it is
    // drawn again only once none of them holds.
    void SetHidden(Renderer r, int reason, bool hide)
    {
        if (r == null) return;
        hideReasons.TryGetValue(r, out int mask);
        int next = hide ? mask | reason : mask & ~reason;
        if (next == mask && (next != 0) == r.forceRenderingOff) return;
        if (next == 0) hideReasons.Remove(r); else hideReasons[r] = next;
        r.forceRenderingOff = next != 0;
    }

    // Called while every wall stands as built (on the way in, before any has faded).
    void Measure(Wall w)
    {
        if (w.measured || w.upper == null) return;
        w.upperRenderer = w.upper.GetComponent<Renderer>();
        w.full = w.upperRenderer != null ? w.upperRenderer.bounds : new Bounds(w.upper.position, Vector3.zero);
        w.cutY = w.full.min.y;
        if (w.lower != null) w.full.Encapsulate(w.lower.bounds);
        w.measured = true;
    }

    // A wall comes down when it stands between the camera and Ace, and an outside wall also whenever the camera is
    // outside it, so the rooms read, as the café's do. It goes back up only once it has been clearly out of the
    // way for a moment, so it never flickers at the edge.
    void StepWalls(bool overhead)
    {
        float dt = Time.unscaledDeltaTime;
        Transform eyeT = view != null && view.isometricCamera != null ? view.isometricCamera.transform : null;
        Vector3 eye = eyeT != null ? eyeT.position : transform.position + Vector3.up * 20f;
        Vector3 feet = view != null ? view.AceFeet : aceT.position;
        int storey = upstairs ? 1 : 0;
        foreach (Wall w in walls)
        {
            if (w == null || w.upper == null) continue;
            Measure(w);
            bool mine = overhead && w.storey == storey;
            bool blocks = false;
            if (mine)
                blocks = BlocksAce(w.full, eye, feet)
                         || w.perimeter && Vector3.Dot(eye - w.full.center, transform.TransformDirection(w.outward)) > 0f;
            if (blocks) { w.down = true; w.clearFor = 0f; }
            else if (w.down)
            {
                w.clearFor += dt;
                if (!mine || w.clearFor >= riseDelay) w.down = false;
            }
            float target = w.down ? 1f : 0f;
            w.progress = !mine ? target : Mathf.MoveTowards(w.progress, target, dt / Mathf.Max(.01f, slideSeconds));
            Apply(w);
        }
    }

    static bool BlocksAce(Bounds wall, Vector3 eye, Vector3 feet) =>
        Blocks(wall, eye, feet + Vector3.up * .4f) || Blocks(wall, eye, feet + Vector3.up * 1f) || Blocks(wall, eye, feet + Vector3.up * 1.7f);

    static bool Blocks(Bounds wall, Vector3 eye, Vector3 target)
    {
        Vector3 d = target - eye;
        float length = d.magnitude;
        return length > .01f && wall.IntersectRay(new Ray(eye, d / length), out float hit) && hit < length - .15f;
    }

    // The part above the cut fades to a ghost (the dither shader's dots, feathered up from the cut), and what
    // hangs on the wall fades with it at the same dots: nothing moves, nothing pops. A part whose material
    // can't be copied hides instead (SeeThroughMaterials).
    void Apply(Wall w)
    {
        if (w.upper == null || !w.measured) return;
        bool down = w.progress > 0f;
        w.keep = down ? Mathf.Lerp(1f, ghost, Mathf.SmoothStep(0f, 1f, w.progress)) : 1f;
        if (down && !w.worn)
        {
            w.worn = true;
            SeeThroughMaterials.Wear(w.upperRenderer);
            foreach (Renderer r in w.hanging) SeeThroughMaterials.Wear(r);
        }
        else if (!down && w.worn)
        {
            w.worn = false;
            SeeThroughMaterials.TakeOff(w.upperRenderer);
            foreach (Renderer r in w.hanging) SeeThroughMaterials.TakeOff(r);
        }
        if (!w.worn) return;
        float from = w.cutY, to = w.cutY + feather;
        SeeThroughMaterials.Show(w.upperRenderer, w.keep, from, to);
        foreach (Renderer r in w.hanging) SeeThroughMaterials.Show(r, w.keep, from, to);
    }

    // ------------------------------------------------------------------ the lab

    // Fixit Fidget > Night > Break-ins - Play the night at Grace's door (lab): the night has begun; switch the
    // break-ins on and stand Ace on the pavement in front of her stoop, facing her door.
    void StartLab(NightWalk night)
    {
        labStarted = true;
        int request = labRequest;
        labRequest = 0;
        night.breakIns = true;
        FindAce();
        if (door == null || aceT == null)
        {
            Debug.LogWarning("[Break-ins] The lab couldn't put Ace at Grace's door (no door or no Ace).");
            return;
        }
        Vector3 at = door.DoorwayPoint + door.Outward * 2.4f;
        if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            at.y = ground.point.y;
        float lift = ace != null ? ace.height * .5f - ace.center.y + .03f : 1.03f;
        bool wasOn = ace != null && ace.enabled;
        if (ace != null) ace.enabled = false;
        aceT.SetPositionAndRotation(at + Vector3.up * lift, Quaternion.LookRotation(-door.Outward, Vector3.up));
        if (ace != null) ace.enabled = wasOn;
        if (view != null)
        {
            // Start looking from the street, behind Ace, straight at her door.
            view.FollowAce(false);
            view.FollowAce(true);
            Vector3 f = -door.Outward;
            view.OrbitTo(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 50f, 26f);
        }
        // The walk check walks every room at a run: she's out tonight (chunk C), so nobody sees or hears it.
        if (request == 2 && resident != null) resident.OutAllNight("the walk check: nobody home, so Ace can walk every room");
        if (request == 2 && GetComponent<GraceHouseWalkCheck>() == null) gameObject.AddComponent<GraceHouseWalkCheck>();
        Debug.Log($"[Break-ins] Lab: the night at Grace's door, 12 West Street (the break-ins are on). Walk in: her door opens for Ace. " +
                  $"{(request == 2 ? "The walk check drives Ace round the house by itself: leave the mouse and keyboard alone." : "")}");
    }
}
