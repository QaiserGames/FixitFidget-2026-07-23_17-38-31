using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// NIGHT WALK (night step 4; see claude/night-city-proposal.md in the project)
//
// The café's street at night, with nothing to do yet: Ace walks out into the
// lit neighbourhood and back. This component holds the night and switches it
// on and off. It is built in parts:
//
//   Part 1, the night's lighting:
//     * the hour: CafeDaylight is held at a night hour, with a moon, and the
//       café's own lights inside the room are dimmed (closed for the night);
//     * street lamps: lights set up by Fixit Fidget > Night > Night walk 1,
//       switched off in the scene and on only here;
//     * windows: POLYGON buildings get a copy of their material with one of the
//       pack's five window masks (Emissive_01-05) as its glow, chosen per
//       building and the same every night, so some windows are lit and some dark;
//     * signs glow with their own colours; the one late spot's window glows warm
//       and throws light on the pavement;
//     * a night look (more bloom, a cooler balance) on its own Volume, weight 0
//       by day;
//     * stars and the moon in the sky (NightSky, through CafeDaylight; since the
//       second playtest, 29 Sept), seen in first person;
//     * the street is quiet: the café sends nobody, the day's clock stops, and
//       the day's walkers and traffic go home.
//
//   Part 2, the night's edges and collision:
//     * solid by night: by day the streets have no collision at all (only the
//       café's people walk there, on their own routes), so while the night runs
//       every fixed mesh Ace could touch gets exact collision: solid exactly where
//       it looks solid, no invisible walls, nothing to walk through;
//     * the patio's invisible day fence (it keeps Ace in the café by day) is off;
//     * the road works at the 8 street ends are night-only objects (Night Only).
//
//   Part 3, getting about:
//     * the overhead camera follows Ace, trailing a fifth of a second behind
//       (CafeViewMode.FollowAce), at the café's own tilt and zoom since the
//       second playtest (29 Sept; it was closer and steeper before);
//     * a building or a big tree between the camera and Ace turns see-through
//       (NightSeeThrough);
//     * the café is closed: the HUD shows the night's hour instead of the day's
//       clock, money and stock, and nothing in the café offers anything to do
//       (ShopUI and PlayerInteractor ask NightWalk.Instance.Active).
//
//   Part 4a, asleep but lived-in (this step):
//     * the clock moves: 11 PM to 4 AM in four minutes (nightMinutes), then it
//       holds (part 5 will end the night there);
//     * the city goes to bed: a third of the POLYGON windows are lit at 11 and go
//       dark one by one until 3 AM, a few night owls stay up, a few flicker blue
//       like a TV. Each building part's bedtime is the same every night;
//     * the houses round the café are lit room by room (NightHomes): downstairs
//       first, upstairs later, a TV or two, someone up for a glass of water;
//       Grace's house has one warm room until about midnight, and nothing marks it;
//     * neighbours come home through their own front doors, and then a room
//       lights (NightNeighbours);
//     * a few things are a little broken (NightFlicker): a failing bulb, a
//       stuttering street lamp, a sign cutting out;
//     * Ace's pocket torch on F (NightTorch) and the notebook on N (NightNotebook).
//     House numbers and street-name signs are part of the scene, by day too
//     (HouseNumber, StreetNameSign).
//
// Nothing here runs by day. Only Begin() switches anything on, and End() (or
// leaving Play Mode) puts it all back. Material copies are made at run time and
// never saved: the scene keeps its originals.
//
// The Night 1 slice: the night now follows the day. The recap's button leads into
// it, and Ace calls it a night inside the café's door, or dawn ends it
// (NightCycle); the next morning is saved with what the night did (NightLedger).
// Night Follows The Day (below) switches that off. The editor's lab (Fixit Fidget >
// Night > Play the night walk (lab)) still starts a night as the scene loads.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightWalk : MonoBehaviour
{
    public static NightWalk Instance { get; private set; }

    /// <summary>Set by the editor before a lab Play session that should start at night.</summary>
    public const string PendingKey = "FixitFidget.NightWalk.Pending";

    /// <summary>How the houses round the café spend the night (NightHomes).</summary>
    [Serializable]
    public sealed class HomeHours
    {
        [Tooltip("Share of the houses' rooms lit when the night begins (shop fronts are always dark).")]
        [Range(0f, 1f)] public float litAtStart = .45f;
        [Tooltip("Downstairs lights are out by this hour (24 = midnight, 24.75 = 12:45 AM).")]
        public float downstairsOutBy = 24.75f;
        [Tooltip("Every room lit at the start is out by this hour (26 = 2 AM).")]
        public float lastLightsOut = 26f;
        [Tooltip("Rooms whose light flickers blue, like a TV.")]
        [Range(0, 6)] public int tvRooms = 2;
        [Tooltip("Rooms where someone gets up later in the night: a light for a few minutes.")]
        [Range(0, 6)] public int wakes = 3;
        [Tooltip("Rooms where somebody is still up when the night ends (one per house), until owlsUntil.")]
        [Range(0, 4)] public int nightOwls = 1;
        [Tooltip("When the night owls' lights go out (27.75 = 3:45 AM).")]
        public float owlsUntil = 27.75f;
        [Tooltip("Grace's house: the one with the HomeDoor of this id. One warm room, nothing marks it.")]
        public string graceHomeId = "home.grace";
        [Tooltip("Which of Grace's rooms is lit (0 is the ground floor's front window, 1 and 2 the first floor's bays).")]
        public int graceRoom = 1;
        [Tooltip("When Grace's light goes out (24 = midnight).")]
        public float graceBedtime = 24f;
        [ColorUsage(false, true)] public Color roomGlow = new Color(1.2f, .84f, .46f);
        [ColorUsage(false, true)] public Color tvGlow = new Color(.5f, .66f, 1.25f);
    }

    /// <summary>A neighbour who comes home during the night (NightNeighbours). Nobody in particular: no notebook entries.</summary>
    [Serializable]
    public sealed class Neighbour
    {
        [Tooltip("For reports only.")]
        public string name = "";
        [Tooltip("The clock hour they come home (23-28: 24.5 is 12:30 AM). Their house is dark until then.")]
        public float comesHomeAt = 24f;
        [Tooltip("The house they live in: its object's name (it must have a front door that opens).")]
        public string house = "";
        [Tooltip("Where they appear (only when the camera can't see it), then along the pavement to the front of their door (world points).")]
        public Vector3[] walk = Array.Empty<Vector3>();
        [Tooltip("Metres out from the doorway where their front step starts.")]
        public float stoop = .9f;
        [Tooltip("Hours their first room stays lit once they are in.")]
        public float downstairsFor = .8f;
        [Tooltip("Then a room upstairs, for this many hours.")]
        public float upstairsFor = .45f;
    }

    [Header("After the day (the Night 1 slice)")]
    [Tooltip("The recap's button leads into the night, and the next day opens when Ace calls it a night (NightCycle). " +
             "Off: the recap opens the next day straight away, as before the night existed.")]
    public bool followsTheDay = true;

    [Header("The hour")]
    [Tooltip("The clock hour the night begins at (0-24).")]
    [Range(0f, 24f)] public float nightHour = 23f;
    [Range(0f, 1f)] public float moon = 1f;
    [Tooltip("The café's own lights inside the room, closed for the night (1 = as by day).")]
    [Range(0f, 1f)] public float cafeInsideLights = .15f;
    [Tooltip("The hour the night ends at (28 is 4 AM): dawn, and Ace hurries home (NightCycle).")]
    public float nightEndsAt = 28f;
    [Tooltip("Part 4: real minutes from the start to the end (4: an hour every 48 seconds).")]
    [Min(.1f)] public float nightMinutes = 4f;

    [Header("Street lamps and other night-only things (inactive in the scene)")]
    [Tooltip("Objects that exist only at night: the street lamps' lights and bulbs, the late spot's light.")]
    public GameObject[] nightOnly = Array.Empty<GameObject>();
    [Tooltip("The street lamps' lights (for the report; they live under Night Only objects).")]
    public Light[] lampLights = Array.Empty<Light>();

    [Header("Windows")]
    public Renderer[] buildingRenderers = Array.Empty<Renderer>();
    [Tooltip("Per building renderer: which window mask (0-4), or -1 for a building that is dark tonight.")]
    public int[] windowPatterns = Array.Empty<int>();
    [Tooltip("POLYGON City's Emissive_01-05.")]
    public Texture[] windowMasks = Array.Empty<Texture>();
    [ColorUsage(false, true)] public Color windowGlow = new Color(1.2f, .84f, .46f);
    [Tooltip("Part 4: share of all the POLYGON building parts lit when the night begins; they go to bed one by one.")]
    [Range(0f, 1f)] public float cityLitAtStart = .3f;
    [Tooltip("Part 4: share of all the building parts that stay lit all night (night owls).")]
    [Range(0f, 1f)] public float cityNightOwls = .04f;
    [Tooltip("Part 4: every other lit window is dark by this hour (27 = 3 AM).")]
    public float cityLastLightsOut = 27f;
    [Tooltip("Part 4: building parts whose windows flicker blue, like a TV.")]
    [Range(0, 12)] public int cityTvWindows = 4;
    [ColorUsage(false, true)] public Color cityTvGlow = new Color(.55f, .7f, 1.3f);

    [Header("Signs (they glow with their own colours)")]
    public Renderer[] signRenderers = Array.Empty<Renderer>();
    [Range(0f, 3f)] public float signGlow = .55f;

    [Header("The late spot")]
    public Renderer[] lateSpotGlass = Array.Empty<Renderer>();
    public Light[] lateSpotLights = Array.Empty<Light>();
    [ColorUsage(false, true)] public Color lateSpotGlow = new Color(1.5f, 1.05f, .6f);

    [Header("Night look")]
    public Volume nightLook;

    [Header("Solid by night (part 2)")]
    [Tooltip("While the night runs, every fixed mesh Ace could touch gets exact collision, so the neighbourhood is solid exactly " +
             "where it looks solid. By day the streets have none (only the café's people walk there, on their own routes), and " +
             "nothing here changes that. The list is written by the set-up (Fixit Fidget > Night > Night walk 1).")]
    public bool solidNight = true;
    public NightCollision nightCollision;
    [Tooltip("Colliders that only keep Ace inside the café by day (the patio's invisible fence). Off while the night runs.")]
    public Collider[] dayOnlyColliders = Array.Empty<Collider>();

    [Header("The houses round the café (part 4)")]
    [Tooltip("Their rooms, window by window: written by Fixit Fidget > Night > Night walk 4a - Build the lived-in windows.")]
    public NightRooms rooms;
    public HomeHours homeHours = new HomeHours();

    [Header("Break-ins (claude/break-ins-spec.md)")]
    [Tooltip("Ace can go into Grace's house at night: E on her stoop opens her door (\"Let yourself in\"), and inside the " +
             "camera looks in from the street (GraceHouse). On in the real game since 30 Sept (Mansoor's second playtest " +
             "found her door shut); Fixit Fidget > Night > Break-ins 4 sets it in the scene. The break-ins' labs switch it " +
             "on for their session whatever the scene says.")]
    public bool breakIns = true;

    [Header("Neighbours coming home (part 4)")]
    public Neighbour[] neighbours = DefaultNeighbours();

    [Header("Broken things (part 4)")]
    [Tooltip("A night-only light (by name) whose bulb is failing: it buzzes and drops out now and then.")]
    public string flickeringLamp = "Old lamp (-17.1, 30.0)";
    [Tooltip("A night-only street lamp (by name) that stutters off and back on.")]
    public string stutteringLamp = "Lamp (-35.3, 26.9)";
    [Tooltip("The glowing sign nearest this point cuts out every so often.")]
    public Vector3 brokenSignNear = new Vector3(-20.6f, 0f, -17.4f);

    public bool Active { get; private set; }
    public int LitBuildings { get; private set; }
    public int GlowingSigns { get; private set; }
    public int QuietedActors { get; private set; }
    public int SolidMeshes { get; private set; }
    public long SolidTriangles { get; private set; }
    public float SolidSeconds { get; private set; }

    /// <summary>The night's clock, from nightHour to nightEndsAt (past 24 means after midnight: 25 is 1 AM).</summary>
    public float Hour { get; private set; }
    /// <summary>The clock as it reads: 0-24.</summary>
    public float ClockHour => Mathf.Repeat(Hour, 24f);
    public float HoursPerSecond => Mathf.Max(0f, nightEndsAt - nightHour) / Mathf.Max(6f, nightMinutes * 60f);
    /// <summary>City building parts lit when the night began, and gone to bed since.</summary>
    public int CityLitAtStart { get; private set; }
    public int CityWentToBed { get; private set; }
    public NightHomes Homes => homes;
    public NightNeighbours NeighbourWalks => neighbourWalks;
    public NightTorch Torch => torch;
    public NightNotebook NotebookPage => notebook;
    /// <summary>The see-through for buildings in the way (null by day).</summary>
    public NightSeeThrough SeeThrough => seeThrough;

    // Synty's POLYGON shaders (Generic_Basic / Generic_Standard) name their glow like this.
    static readonly int SyntyEmissionMap = Shader.PropertyToID("_Emission_Map");
    static readonly int SyntyEmissionColor = Shader.PropertyToID("_Emission_Color");
    static readonly int SyntyAlbedo = Shader.PropertyToID("_Albedo_Map");
    static readonly int SyntyEnableEmission = Shader.PropertyToID("_Enable_Emission");
    // URP Lit, for anything that isn't POLYGON.
    static readonly int UrpEmissionMap = Shader.PropertyToID("_EmissionMap");
    static readonly int UrpEmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int UrpBaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int UrpBaseColor = Shader.PropertyToID("_BaseColor");

    readonly List<(Renderer renderer, Material[] materials)> swapped = new();
    readonly Dictionary<(Material, int), Material> copies = new();
    readonly List<Behaviour> paused = new();
    readonly List<GameObject> hiddenActors = new();
    GameObject solidRoot;
    readonly List<Collider> dayOnlyOff = new();
    CafeViewMode viewMode;
    NightSeeThrough seeThrough;
    List<StreetLife.Actor> streetActors;
    StreetLife street;
    CafeDaylight daylight;
    float daylightHour;

    // Part 4: the city's bedtimes, the houses, the neighbours, the torch, the notebook, broken things.
    sealed class TvWindow
    {
        public int index;
        public readonly List<(Material material, int property)> materials = new();
        public float level = 1f, target = 1f, next;
        public Color colour;
    }
    float[] cityBedtime = Array.Empty<float>();
    bool[] cityLitNow = Array.Empty<bool>();
    bool[] cityTv = Array.Empty<bool>();
    readonly Dictionary<int, Material[]> cityOwn = new();
    readonly Dictionary<int, Material[]> cityNight = new();
    readonly List<TvWindow> tvWindows = new();
    int brokenSign = -1;
    NightHomes homes;
    NightNeighbours neighbourWalks;
    NightTorch torch;
    NightNotebook notebook;
    readonly List<NightFlicker> flickers = new();
    readonly List<GameObject> brokenParts = new();
    readonly List<Material> brokenMaterials = new();

    void Awake() => Instance = this;

    // Leaving Play Mode tears the scene down: nothing to put back then (objects being destroyed can't be
    // switched on again), only the run-time material copies to free.
    void OnDestroy()
    {
        if (solidRoot != null) Destroy(solidRoot);
        foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
        copies.Clear();
        foreach (var m in brokenMaterials) if (m != null) Destroy(m);
        brokenMaterials.Clear();
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    // A lab Play session asked for the night: start it once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) != 1) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night walk] A night walk was asked for outside a lab session; it only runs in the lab for now.");
            return;
        }
        var walk = Instance != null ? Instance : FindAnyObjectByType<NightWalk>();
        if (walk == null)
        {
            Debug.LogWarning("[Night walk] No NightWalk in this scene: run Fixit Fidget > Night > Night walk 1 first.");
            return;
        }
        walk.QuietTheStreet();
        walk.Begin();
    }
#endif

    /// <summary>Switch the night on. Safe to call twice.</summary>
    public void Begin()
    {
        if (Active) return;
        Active = true;
        Hour = nightHour;
        daylight = FindAnyObjectByType<CafeDaylight>();
        daylightHour = Hour;
        if (daylight != null) daylight.SetNight(ClockHour, moon, cafeInsideLights);
        foreach (var go in nightOnly) if (go != null) go.SetActive(true);
        LightTheWindows();
        LightTheSigns();
        LightTheLateSpot();
        if (nightLook != null) nightLook.weight = 1f;
        MakeTheNightSolid();
        foreach (var c in dayOnlyColliders)
            if (c != null && c.enabled) { c.enabled = false; dayOnlyOff.Add(c); }
        // Part 4: the houses' rooms, made before the see-through looks for what can fade (it takes only
        // renderers that are switched on), and set to the hour after.
        homes = GetOrAdd<NightHomes>();
        homes.Build(rooms, homeHours, Hour, NightNeighbours.AwayAt(neighbours, Hour));
        // Part 3: the overhead camera follows Ace, and what stands in the way turns see-through.
        viewMode = FindAnyObjectByType<CafeViewMode>();
        if (viewMode != null) viewMode.FollowAce(true);
        seeThrough = GetOrAdd<NightSeeThrough>();
        seeThrough.enabled = true;
        seeThrough.Build(viewMode, transform);
        homes.Settle(Hour);
        // Part 4: the rest of the night's life.
        BreakThings();
        torch = GetOrAdd<NightTorch>();
        torch.Begin();
        notebook = GetOrAdd<NightNotebook>();
        notebook.Begin();
        neighbourWalks = GetOrAdd<NightNeighbours>();
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        neighbourWalks.Begin(neighbours, homes, spawner != null ? spawner.PatronPrefab : null, Hour);
        Debug.Log($"[Night walk] Night at {Clock(Hour)} (to {Clock(nightEndsAt)} in {nightMinutes:0.#} minutes): {Count(lampLights)} street lamps, " +
                  $"{LitBuildings} building parts with lit windows (going to bed until {Clock(cityLastLightsOut)}, {TvCount()} TVs), " +
                  $"{GlowingSigns} signs glowing, the late spot {(lateSpotGlass.Length > 0 ? "lit" : "not set")}; " +
                  $"{QuietedActors} of the day's walkers and cars sent home; {SolidMeshes} meshes made solid ({SolidTriangles:N0} triangles, " +
                  $"{SolidSeconds:0.00} s); {dayOnlyOff.Count} day-only colliders off; the overhead camera " +
                  $"{(viewMode != null ? "follows Ace" : "was not found")}; {seeThrough.Groups} buildings and trees can turn see-through " +
                  $"({seeThrough.Renderers} pieces, {seeThrough.HideInstead} of them hide instead); houses: {homes.Rooms} rooms in {homes.Houses} houses, " +
                  $"{homes.LitNow} lit{(string.IsNullOrEmpty(homes.Problems) ? "" : " (" + homes.Problems + ")")}; {NightNeighbours.AwayAt(neighbours, Hour).Count} neighbours still out; " +
                  $"{flickers.Count} broken things.", this);
    }

    /// <summary>Put the day back exactly as it was.</summary>
    public void End()
    {
        if (!Active) return;
        Active = false;
        // The see-through buildings first: they wear copies of the night's materials, which come off below.
        if (seeThrough != null) { seeThrough.Clear(); Destroy(seeThrough); seeThrough = null; }
        if (neighbourWalks != null) { neighbourWalks.Clear(); Destroy(neighbourWalks); neighbourWalks = null; }
        if (homes != null) { homes.Clear(); Destroy(homes); homes = null; }
        if (torch != null) { torch.End(); Destroy(torch); torch = null; }
        if (notebook != null) { notebook.End(); Destroy(notebook); notebook = null; }
        MendThings();
        if (viewMode != null) viewMode.FollowAce(false);
        viewMode = null;
        if (daylight != null) daylight.SetNight(null, 0f, 1f);
        foreach (var go in nightOnly) if (go != null) go.SetActive(false);
        // Newest first: a renderer swapped twice (windows, then the late spot's glass) gets its own materials back last.
        for (int i = swapped.Count - 1; i >= 0; i--)
            if (swapped[i].renderer != null) swapped[i].renderer.sharedMaterials = swapped[i].materials;
        swapped.Clear();
        foreach (var copy in copies.Values) if (copy != null) Destroy(copy);
        copies.Clear();
        cityOwn.Clear();
        cityNight.Clear();
        tvWindows.Clear();
        if (nightLook != null) nightLook.weight = 0f;
        if (solidRoot != null) Destroy(solidRoot);
        solidRoot = null;
        SolidMeshes = 0;
        SolidTriangles = 0;
        foreach (var c in dayOnlyOff) if (c != null) c.enabled = true;
        dayOnlyOff.Clear();
        foreach (var b in paused) if (b != null) b.enabled = true;
        paused.Clear();
        foreach (var go in hiddenActors) if (go != null) go.SetActive(true);
        hiddenActors.Clear();
        if (street != null && streetActors != null)
        {
            street.actors = streetActors;
            street.RebuildRoutes();
        }
        streetActors = null;
        QuietedActors = LitBuildings = GlowingSigns = CityLitAtStart = CityWentToBed = 0;
    }

    /// <summary>
    /// The café is closed and the street has gone to bed: the café sends nobody, the day's clock
    /// stops, and StreetLife's walkers and cars leave (part 4 brings a few neighbours home).
    /// CafeArrivals (the café's visitors arriving by car or on foot) rests with the spawners: with
    /// the street's routes gone it would find no lanes, warn, and send its cars away for the rest
    /// of the session. Paused, it finds the lanes again when End() brings the street back.
    /// </summary>
    public void QuietTheStreet()
    {
        foreach (Behaviour b in new Behaviour[]
                 {
                     FindAnyObjectByType<CustomerSpawner>(), FindAnyObjectByType<PatronSpawner>(), FindAnyObjectByType<CafeArrivals>(),
                     FindAnyObjectByType<DayClock>(),
                 })
            if (b != null && b.enabled) { b.enabled = false; paused.Add(b); }
        street = StreetLife.Main != null ? StreetLife.Main : FindAnyObjectByType<StreetLife>();
        if (street == null) return;
        streetActors = street.actors;
        var kept = new List<StreetLife.Actor>();
        foreach (var actor in streetActors)
        {
            if (actor == null) continue;
            if (actor.actor != null && actor.actor.gameObject.activeSelf)
            {
                actor.actor.gameObject.SetActive(false);
                hiddenActors.Add(actor.actor.gameObject);
            }
            QuietedActors++;
        }
        street.actors = kept;
        street.RebuildRoutes();
    }

    // ---------- the night moves on (part 4) ----------

    void Update()
    {
        if (!Active) return;
        if (Time.deltaTime > 0f && Hour < nightEndsAt) Hour = Mathf.Min(nightEndsAt, Hour + Time.deltaTime * HoursPerSecond);
        FollowTheClock();
        UpdateCity();
        if (homes != null) homes.Tick(Hour);
        if (neighbourWalks != null) neighbourWalks.Tick(Hour);
    }

    /// <summary>
    /// Move the night's clock to <paramref name="hour"/> (23-28) at once, for photos and checks: windows,
    /// rooms and neighbours are set as they would be by then (a window whose building is see-through right
    /// now follows once it is back).
    /// </summary>
    public void SetHour(float hour)
    {
        if (!Active) return;
        Hour = Mathf.Clamp(hour, nightHour, Mathf.Max(nightHour, nightEndsAt));
        FollowTheClock();
        UpdateCity();
        if (neighbourWalks != null) neighbourWalks.SkipTo(Hour);
        if (homes != null) homes.Settle(Hour);
    }

    // The day's lighting is the same all night (23:00 to 5:48 are all "night"): the hour is handed on
    // now and then so that anything reading it agrees with the HUD.
    void FollowTheClock()
    {
        if (daylight == null || Mathf.Abs(Hour - daylightHour) < .25f) return;
        daylightHour = Hour;
        daylight.SetNight(ClockHour, moon, cafeInsideLights);
    }

    // ---------- solid by night ----------

    /// <summary>
    /// Exact collision, for the night only, on every fixed mesh Ace could touch (the set-up's list):
    /// the neighbourhood is solid exactly where it looks solid, no more (no invisible walls) and no
    /// less (nothing to walk through, no ground to fall through). POLYGON's own rough convex collision
    /// stays off. Plain objects in the Play scene, so they go with it; End() removes them sooner.
    /// </summary>
    void MakeTheNightSolid()
    {
        SolidMeshes = 0;
        SolidTriangles = 0;
        SolidSeconds = 0f;
        if (!solidNight || nightCollision == null) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // At the scene's root, so each piece's scale is its world scale.
        solidRoot = new GameObject("Solid by night (while the night runs)");
        foreach (var piece in nightCollision.pieces)
        {
            if (piece.mesh == null) continue;
            var go = new GameObject(piece.from);
            go.transform.SetParent(solidRoot.transform, false);
            go.transform.SetPositionAndRotation(piece.position, piece.rotation);
            go.transform.localScale = piece.scale;
            go.AddComponent<MeshCollider>().sharedMesh = piece.mesh;
            SolidMeshes++;
            for (int s = 0; s < piece.mesh.subMeshCount; s++) SolidTriangles += piece.mesh.GetSubMesh(s).indexCount / 3;
        }
        SolidSeconds = (float)watch.Elapsed.TotalSeconds;
    }

    // ---------- windows (and their bedtimes), signs, the late spot ----------

    void LightTheWindows()
    {
        LitBuildings = CityLitAtStart = CityWentToBed = 0;
        cityOwn.Clear();
        cityNight.Clear();
        tvWindows.Clear();
        int n = buildingRenderers.Length;
        cityBedtime = new float[n];
        cityLitNow = new bool[n];
        cityTv = new bool[n];
        for (int i = 0; i < n; i++) cityBedtime[i] = float.NegativeInfinity;
        if (windowMasks.Length == 0) return;
        PlanCityBedtimes();
        for (int i = 0; i < n; i++) if (cityBedtime[i] > Hour) SetCityLit(i, true);
        CityLitAtStart = LitBuildings;
    }

    bool CanLight(int i)
    {
        int pattern = i < windowPatterns.Length ? windowPatterns[i] : -1;
        return buildingRenderers[i] != null && pattern >= 0 && pattern < windowMasks.Length && windowMasks[pattern] != null;
    }

    // Each lit building part's bedtime, the same every night: about cityLitAtStart of them lit at the start
    // hour (of those the set-up gave a window mask), going dark one by one until cityLastLightsOut; a few
    // night owls never do. The late spot is open all night.
    void PlanCityBedtimes()
    {
        int n = buildingRenderers.Length, candidates = 0;
        for (int i = 0; i < n; i++) if (CanLight(i)) candidates++;
        if (candidates == 0) return;
        float share = Mathf.Clamp01(cityLitAtStart * n / candidates);
        float owls = cityLitAtStart > 0f ? Mathf.Clamp01(cityNightOwls / cityLitAtStart) : 0f;
        float lastOut = Mathf.Max(nightHour + .1f, cityLastLightsOut);
        var lateSpot = new HashSet<Renderer>(lateSpotGlass);
        var tvChoices = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (!CanLight(i)) continue;
            Renderer r = buildingRenderers[i];
            if (lateSpot.Contains(r)) { cityBedtime[i] = float.PositiveInfinity; continue; }
            float h = NightHomes.Hash01(CityKey(r), i, "city");
            if (h >= share) continue;
            float u = h / Mathf.Max(1e-5f, share);
            cityBedtime[i] = u < owls ? float.PositiveInfinity
                : Mathf.Lerp(nightHour + .05f, lastOut, (u - owls) / Mathf.Max(1e-5f, 1f - owls));
            if (cityBedtime[i] >= nightHour + 1.5f) tvChoices.Add(i);
        }
        tvChoices.Sort((a, b) => NightHomes.Hash01(CityKey(buildingRenderers[a]), a, "tv").CompareTo(NightHomes.Hash01(CityKey(buildingRenderers[b]), b, "tv")));
        for (int k = 0; k < tvChoices.Count && k < cityTvWindows; k++) cityTv[tvChoices[k]] = true;
    }

    static string CityKey(Renderer r)
    {
        if (r == null) return "";
        Vector3 c = r.bounds.center;
        return string.Format(CultureInfo.InvariantCulture, "{0}@{1:0.0},{2:0.0},{3:0.0}", r.name, c.x, c.y, c.z);
    }

    // Lights a building part's windows, or puts them out. False while the building is see-through:
    // taking the see-through off puts back what it wore when it began to fade, so it waits for that.
    bool SetCityLit(int i, bool on)
    {
        Renderer r = buildingRenderers[i];
        if (r == null || cityLitNow[i] == on) return true;
        if (seeThrough != null && seeThrough.IsWorn(r)) return false;
        if (on)
        {
            if (!cityNight.TryGetValue(i, out Material[] night))
            {
                int pattern = windowPatterns[i];
                Material[] own = r.sharedMaterials;
                night = (Material[])own.Clone();
                bool changed = false;
                TvWindow tv = cityTv[i] ? new TvWindow { index = i, colour = windowGlow } : null;
                for (int m = 0; m < night.Length; m++)
                {
                    // The masks are drawn for POLYGON City's own atlas; other materials on a building keep theirs.
                    if (!IsCityAtlas(night[m])) continue;
                    Material copy = GlowCopy(night[m], tv != null ? 1000 + i : pattern, windowMasks[pattern], windowGlow, false);
                    if (copy == null) continue;
                    night[m] = copy;
                    changed = true;
                    if (tv != null) tv.materials.Add((copy, copy.HasProperty(SyntyEmissionColor) ? SyntyEmissionColor : UrpEmissionColor));
                }
                if (!changed) { cityBedtime[i] = float.NegativeInfinity; return true; }   // nothing to light here
                cityOwn[i] = own;
                cityNight[i] = night;
                swapped.Add((r, own));   // End() puts it back
                if (tv != null) tvWindows.Add(tv);
            }
            r.sharedMaterials = night;
            LitBuildings++;
        }
        else
        {
            if (cityOwn.TryGetValue(i, out Material[] own)) r.sharedMaterials = own;
            LitBuildings--;
            CityWentToBed++;
        }
        cityLitNow[i] = on;
        return true;
    }

    void UpdateCity()
    {
        for (int i = 0; i < cityBedtime.Length; i++)
        {
            bool want = cityBedtime[i] > Hour;
            if (want != cityLitNow[i]) SetCityLit(i, want);
        }
        // Televisions: the light jumps as the picture cuts, a little bluer or whiter each time.
        float now = Time.time;
        foreach (TvWindow tv in tvWindows)
        {
            if (!cityLitNow[tv.index]) continue;
            if (now >= tv.next)
            {
                tv.target = UnityEngine.Random.Range(.4f, 1f);
                tv.next = now + (UnityEngine.Random.value < .2f ? UnityEngine.Random.Range(.05f, .15f) : UnityEngine.Random.Range(.3f, 1.6f));
                tv.colour = Color.Lerp(windowGlow, cityTvGlow, UnityEngine.Random.Range(.55f, .85f));
            }
            tv.level = Mathf.MoveTowards(tv.level, tv.target, Time.deltaTime * 12f);
            foreach (var (material, property) in tv.materials) if (material != null) material.SetColor(property, tv.colour * tv.level);
        }
    }

    int TvCount()
    {
        int n = 0;
        foreach (bool tv in cityTv) if (tv) n++;
        return n;
    }

    void LightTheSigns()
    {
        GlowingSigns = 0;
        // The broken one gets its own copies (key 150), so its cutting out touches no other sign.
        brokenSign = -1;
        float best = float.MaxValue;
        for (int i = 0; i < signRenderers.Length; i++)
        {
            if (signRenderers[i] == null) continue;
            Vector3 d = signRenderers[i].bounds.center - brokenSignNear;
            d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; brokenSign = i; }
        }
        Color glow = Color.white * signGlow;
        for (int i = 0; i < signRenderers.Length; i++)
        {
            Renderer r = signRenderers[i];
            int key = i == brokenSign ? 150 : 100;
            if (r != null && Swap(r, m => GlowCopy(m, key, null, glow, true))) GlowingSigns++;
        }
    }

    void LightTheLateSpot()
    {
        foreach (var r in lateSpotGlass)
            if (r != null) Swap(r, m => IsGlass(m) ? GlowCopy(m, 200, Texture2D.whiteTexture, lateSpotGlow, false) : null);
    }

    static bool IsGlass(Material m) => m != null && m.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;

    static bool IsCityAtlas(Material m)
    {
        if (m == null || !m.HasProperty(SyntyAlbedo)) return false;
        Texture albedo = m.GetTexture(SyntyAlbedo);
        return albedo != null && albedo.name.StartsWith("PolygonCity_", StringComparison.Ordinal);
    }

    /// <summary>Replace the renderer's materials with their night copies (null: keep that one). True if any changed.</summary>
    bool Swap(Renderer r, Func<Material, Material> night)
    {
        Material[] originals = r.sharedMaterials;
        Material[] replaced = (Material[])originals.Clone();
        bool changed = false;
        for (int i = 0; i < replaced.Length; i++)
        {
            if (replaced[i] == null) continue;
            Material copy = night(replaced[i]);
            if (copy == null) continue;
            replaced[i] = copy;
            changed = true;
        }
        if (!changed) return false;
        swapped.Add((r, originals));
        r.sharedMaterials = replaced;
        return true;
    }

    /// <summary>
    /// A run-time copy of a material that glows: with the mask given, or (ownColours) with its own
    /// albedo, so a sign lights up in its own colours. One copy per material and key. Null when the
    /// shader has no glow to set.
    /// </summary>
    Material GlowCopy(Material source, int key, Texture mask, Color glow, bool ownColours)
    {
        if (copies.TryGetValue((source, key), out var existing)) return existing;
        Material copy = null;
        if (source.HasProperty(SyntyEmissionMap) && source.HasProperty(SyntyEmissionColor))
        {
            Texture map = ownColours ? (source.HasProperty(SyntyAlbedo) ? source.GetTexture(SyntyAlbedo) : null) : mask;
            if (map != null)
            {
                copy = new Material(source) { name = source.name + " (night)", hideFlags = HideFlags.DontSave };
                copy.SetTexture(SyntyEmissionMap, map);
                copy.SetColor(SyntyEmissionColor, glow);
                // POLYGON materials ship with their glow switched off (Enable Emission 0).
                if (copy.HasProperty(SyntyEnableEmission)) copy.SetFloat(SyntyEnableEmission, 1f);
            }
        }
        else if (source.HasProperty(UrpEmissionMap) && source.HasProperty(UrpEmissionColor))
        {
            Texture map = ownColours ? (source.HasProperty(UrpBaseMap) ? source.GetTexture(UrpBaseMap) : null) : mask;
            copy = new Material(source) { name = source.name + " (night)", hideFlags = HideFlags.DontSave };
            copy.EnableKeyword("_EMISSION");
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (map != null) copy.SetTexture(UrpEmissionMap, map);
            copy.SetColor(UrpEmissionColor, glow);
        }
        copies[(source, key)] = copy;
        return copy;
    }

    // ---------- broken things (part 4) ----------

    void BreakThings()
    {
        // A failing bulb in one of the old lamps: while it is out, a dark cap covers its glowing glass.
        GameObject lamp = NightOnlyNamed(flickeringLamp);
        if (lamp != null)
        {
            NightFlicker f = NewFlicker(NightFlicker.Kind.Buzz, $"the failing bulb '{flickeringLamp}'");
            foreach (Light l in lamp.GetComponentsInChildren<Light>(true)) f.AddLight(l);
            foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>(true)) f.AddWhenOn(r);
            Renderer cap = MakeCap(flickeringLamp);
            if (cap != null) f.AddWhenOff(cap);
            f.Begin(11);
        }
        // A street lamp that stutters: its bulb goes with it.
        GameObject stutter = NightOnlyNamed(stutteringLamp);
        if (stutter != null)
        {
            NightFlicker f = NewFlicker(NightFlicker.Kind.Stutter, $"the stuttering street lamp '{stutteringLamp}'");
            foreach (Light l in stutter.GetComponentsInChildren<Light>(true)) f.AddLight(l);
            foreach (Renderer r in stutter.GetComponentsInChildren<Renderer>(true)) f.AddWhenOn(r);
            f.Begin(23);
        }
        // A sign cutting out: its own glow copies fade with it.
        if (brokenSign >= 0 && signRenderers[brokenSign] != null)
        {
            Renderer sign = signRenderers[brokenSign];
            NightFlicker f = NewFlicker(NightFlicker.Kind.CutOut, $"the sign '{SignName(sign)}'");
            var own = new HashSet<Material>(copies.Values);
            foreach (Material m in sign.sharedMaterials)
                if (m != null && own.Contains(m)) f.AddGlow(m, m.HasProperty(SyntyEmissionColor) ? SyntyEmissionColor : UrpEmissionColor);
            f.Begin(37);
        }
    }

    void MendThings()
    {
        foreach (NightFlicker f in flickers) if (f != null) { f.Restore(); Destroy(f.gameObject); }
        flickers.Clear();
        foreach (GameObject go in brokenParts) if (go != null) Destroy(go);
        brokenParts.Clear();
        foreach (Material m in brokenMaterials) if (m != null) Destroy(m);
        brokenMaterials.Clear();
    }

    NightFlicker NewFlicker(NightFlicker.Kind kind, string what)
    {
        var go = new GameObject($"Broken: {what} (while the night runs)");
        go.transform.SetParent(transform, false);
        var f = go.AddComponent<NightFlicker>();
        f.kind = kind;
        f.what = what;
        flickers.Add(f);
        return f;
    }

    GameObject NightOnlyNamed(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (GameObject go in nightOnly) if (go != null && go.name == name.Trim()) return go;
        return null;
    }

    // The dark cap over a lamp's glass (the rooms list keeps its shape): the glass's own material, unlit.
    Renderer MakeCap(string name)
    {
        if (rooms == null || rooms.caps == null) return null;
        foreach (NightRooms.Cap cap in rooms.caps)
        {
            if (cap == null || cap.mesh == null || cap.name != name) continue;
            Transform host = NightHomes.FindByPath(cap.rendererPath);
            Renderer glass = host != null ? host.GetComponent<Renderer>() : null;
            if (glass == null || glass.sharedMaterial == null) return null;
            var go = new GameObject($"Dark cap over '{name}' (while it is out)");
            go.transform.SetParent(host, false);   // the cap is in the glass's own space
            go.AddComponent<MeshFilter>().sharedMesh = cap.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var dark = new Material(glass.sharedMaterial) { name = "Lamp glass, out (night)", hideFlags = HideFlags.DontSave };
            if (dark.HasProperty(UrpEmissionColor)) dark.SetColor(UrpEmissionColor, Color.black);
            dark.DisableKeyword("_EMISSION");
            if (dark.HasProperty(UrpBaseColor)) dark.SetColor(UrpBaseColor, new Color(.16f, .14f, .11f));
            mr.sharedMaterial = dark;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            brokenParts.Add(go);
            brokenMaterials.Add(dark);
            return mr;
        }
        return null;
    }

    static string SignName(Renderer r)
    {
        Transform t = r.transform;
        while (t.parent != null && t.parent.GetComponent<Renderer>() == null && t.name.StartsWith("SM_", StringComparison.Ordinal) == false) t = t.parent;
        return t.name;
    }

    T GetOrAdd<T>() where T : Component
    {
        T c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    static string Clock(float hour) => NightHomes.Clock(hour);

    // A walk home that Ace can watch, then two more later in the night. Placeholders to be moved or
    // retimed freely: the pavement points were checked clear of lamp posts, planters and steps.
    static Neighbour[] DefaultNeighbours() => new[]
    {
        new Neighbour
        {
            name = "the dusty rose house's neighbour", comesHomeAt = 23.6f, house = "2 - Dusty rose bay-window house",
            walk = new[] { new Vector3(-15.9f, -.02f, 19.4f), new Vector3(-15.6f, -.02f, 18f), new Vector3(-15.7f, -.02f, 2.4f) },
            stoop = .9f, downstairsFor = .8f, upstairsFor = .45f,
        },
        new Neighbour
        {
            name = "the lavender house's neighbour", comesHomeAt = 24.55f, house = "4 - Lavender bay-window house",
            walk = new[] { new Vector3(-15.2f, -.02f, -3.85f), new Vector3(-15.7f, -.02f, -2.9f), new Vector3(-15.7f, -.02f, 14.4f) },
            stoop = .9f, downstairsFor = .7f, upstairsFor = .5f,
        },
        new Neighbour
        {
            name = "the courtyard shop's neighbour", comesHomeAt = 25.67f, house = "Courtyard - 1 - neighborhood shop house",
            walk = new[] { new Vector3(15.7f, -.18f, -3.85f), new Vector3(15.95f, -.02f, -3.85f), new Vector3(16.4f, -.02f, -2.3f) },
            stoop = .6f, downstairsFor = .6f, upstairsFor = .4f,
        },
    };

    /// <summary>What is switched on right now, for the night photos' notes.</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        int active = 0;
        foreach (var go in nightOnly) if (go != null && go.activeInHierarchy) active++;
        sb.AppendLine($"Night walk {(Active ? "on" : "off")}: the clock reads {Clock(Hour)} (started {Clock(nightHour)}, stops at {Clock(nightEndsAt)}, " +
                      $"{nightMinutes:0.#} real minutes for the night), moon {moon:0.00}, café lights inside x{cafeInsideLights:0.00}.");
        sb.AppendLine($"Night-only objects active: {active} of {nightOnly.Length}; street lamp lights {Count(lampLights)}.");
        sb.AppendLine($"Building parts lit now: {LitBuildings} of {buildingRenderers.Length} (lit at the start {CityLitAtStart}, gone to bed since {CityWentToBed}, " +
                      $"all but the night owls dark by {Clock(cityLastLightsOut)}); TVs {TvCount()}; signs glowing: {GlowingSigns} of {signRenderers.Length}; " +
                      $"material copies: {copies.Count}; renderers swapped: {swapped.Count}; street actors sent home: {QuietedActors}.");
        sb.AppendLine($"Solid by night: {SolidMeshes} meshes with exact collision ({SolidTriangles:N0} triangles, made in {SolidSeconds:0.00} s); " +
                      $"day-only colliders off: {dayOnlyOff.Count} of {dayOnlyColliders.Length}.");
        if (viewMode != null)
        {
            Vector3 a = viewMode.OverheadAngle;
            Vector4 limits = viewMode.OverheadLimits;
            sb.AppendLine($"Overhead camera: {(viewMode.Following ? "following Ace" : "the café's own view")}, turn {a.x:0}°, tilt {a.y:0}°, " +
                          $"{a.z:0.0} m away (tilt {limits.x:0}-{limits.y:0}°, zoom {limits.z:0}-{limits.w:0} m); " +
                          $"Ace {(viewMode.AceInsideCafe ? "inside" : "outside")} the café.");
        }
        if (daylight != null) sb.AppendLine(daylight.DescribeNightSky());
        if (seeThrough != null) sb.AppendLine(seeThrough.Describe());
        if (homes != null) sb.AppendLine(homes.Describe(Hour));
        if (neighbourWalks != null) sb.AppendLine(neighbourWalks.Describe());
        foreach (NightFlicker f in flickers) if (f != null) sb.AppendLine("Broken: " + f.Describe());
        if (brokenSign < 0) sb.AppendLine("Broken: no sign to cut out.");
        if (torch != null) sb.AppendLine(torch.Describe());
        if (notebook != null) sb.AppendLine(notebook.Describe());
        foreach (var kv in copies)
        {
            var m = kv.Value;
            if (m == null) { sb.AppendLine($"  {kv.Key.Item1.name} [{kv.Key.Item2}]: no glow (shader has none)"); continue; }
            string enable = m.HasProperty(SyntyEnableEmission) ? $", Enable Emission {m.GetFloat(SyntyEnableEmission):0}" : "";
            string map = m.HasProperty(SyntyEmissionMap) && m.GetTexture(SyntyEmissionMap) != null ? m.GetTexture(SyntyEmissionMap).name
                       : m.HasProperty(UrpEmissionMap) && m.GetTexture(UrpEmissionMap) != null ? m.GetTexture(UrpEmissionMap).name : "-";
            sb.AppendLine($"  {m.name} [{kv.Key.Item2}]: shader {m.shader.name}, glow map {map}{enable}");
        }
        foreach (var l in lateSpotLights)
            if (l != null) sb.AppendLine($"  late spot light at ({l.transform.position.x:0.0}, {l.transform.position.y:0.0}, {l.transform.position.z:0.0}), {(l.gameObject.activeInHierarchy ? "on" : "off")}");
        return sb.ToString();
    }

    static int Count(Light[] lights)
    {
        int n = 0;
        foreach (var l in lights) if (l != null) n++;
        return n;
    }
}
