using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE START SCREEN (26 Oct 2026; review24 spec S, the research's Seeds 1 and 6)
//
// His ask (24 Oct): a start screen. He turned down three drawn ones ("i hate all the starting scenes… remember this follows
// ace life as he franchises. it's not just set on grace or san francisco, or a random boombox or whatever. it needs to fit the
// cozy but chaotic theme. let's see how over cooked does it") and called drawn mockups "AI art slop". So it is built from the
// game's own café scene, and he judges it by playing it ("ill trust your judgement and will assess how it looks once finished").
// Overcooked's lesson: the menu is calm and the chaos is one beat away.
//
// TWO FACES, chosen by whether a save exists (SaveManager.HasSave, read in Start once every Awake has run):
//   * A SAVE: Seed 1, "The Diorama That Grows". The café exactly as saved (CafeLayout already applied by GameBootstrap), live
//     but harmless: settled guests and walk-in patrons come and sit, the street's walkers and cars move; no customer comes to
//     the counter (the customer spawner is off), no money changes (the takings are pinned), the clock doesn't run (DayClock is
//     disabled before its first Update, so no bell and no intro), nothing is saved (SaveManager.Commit refuses while Up). The
//     camera is our own, pulled high, orbiting the café once every 120 s; how high and wide comes from the save's franchise
//     tier (Framings, one row per tier, read from the stars as the phone's Franchise app reads them).
//   * NO SAVE: Seed 6, the rush-hour cold open. A glimpse of the future: today's 28-seat café (format 0, applied in memory by
//     GameBootstrap's gate, never written), most seats taken, a queue at the counter, the drinks machine steaming, Ace idle
//     behind the counter, from a lower 3/4 angle. After 3 s it freezes (time scale 0), the logo slams in like an order ticket,
//     a warm grade tints the frame and the menu appears without Continue. New Game plays "Before it got like this." (a UI
//     rewind) and the real new game begins: the half-finished café, Day 1, its intro as today.
//
// LEAVING: Continue and New Game leave by reloading the scene (Reload), so nothing from the title's sandbox (the crowd, the
// pinned money, the held clock, the borrowed camera, the cold open's full café) can leak into the day. The game never
// reloaded its scene before this, and Unity runs the [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] hooks once a Play session,
// not once a scene: the scene-made helpers (Juice's watcher, NightCycle's home, a lab's director) would be missing after a
// reload. So Reload runs every such hook again once the new scene is in (the moment Unity first ran them). They are all
// written to be safe to run again (each finds its own first; the labs' hooks read a request key they have already deleted).
//
// THE RULE, in one place (ShouldShow): never twice in a Play session; never in a lab session (CafeLab.Active: every lab and
// PlayLab menu starts one), except this screen's own lab, which asks by LabKey; not after "Fresh game 1" (the real game, by
// SkipOnceKey: that menu presses Play to play Day 1, not to see a title); and in the editor's own Play only while
// "Fixit Fidget › Start screen › Show it in the editor's Play" is on (EditorPrefs, on by default). A build always shows it.
//
// TIME SCALE: the title sets it to 1 as it begins, the cold open freezes it, and Leave puts it back to 1 before the reload (and
// DayClock.StartDay sets 1 in the new scene's Awake anyway). The UI runs on unscaled time.
//
// The UI (TitleMenuUI) is built in code, never saved; this component is put in the scene by Fixit Fidget › Start screen ›
// Start screen 1. The lab: Fixit Fidget › Start screen › Lab - The start screen (drives itself) (StartScreenLab).
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(-150)]   // its Start before GameBootstrap's (-100): the face is chosen before the café's layout is applied
[DisallowMultipleComponent]
public sealed class TitleScreen : MonoBehaviour
{
    public enum Mode { None, Diorama, ColdOpen }

    /// <summary>The start screen's own lab asks for it in its lab session (read once as the session starts).</summary>
    public const string LabKey = "FixitFidget.StartScreen.Lab";
    /// <summary>The next Play goes straight into the day (Fresh game 1 sets it; read once).</summary>
    public const string SkipOnceKey = "FixitFidget.StartScreen.SkipOnce";
    /// <summary>EditorPrefs: show it in the editor's own Play (default on).</summary>
    public const string EditorToggleKey = "FixitFidget.StartScreen.ShowInEditor";
    /// <summary>Over the player's cameras and the day intro's (50).</summary>
    public const int CameraPriority = 60;

    public const float ColdOpenLiveSeconds = 3f;
    public const float OrbitSecondsPerTurn = 120f;
    public const float DiveSeconds = 1.2f;
    public const float FadeSeconds = .4f;
    public const float RewindSeconds = 1.8f;

    // ------------------------------------------------------------------ the franchise follows Ace: one row per tier

    /// <summary>How high and wide the diorama's camera sits, one row per franchise tier.</summary>
    public struct Framing
    {
        public string what;
        public float pitch, distance, fov;
        public Framing(string what, float pitch, float distance, float fov) { this.what = what; this.pitch = pitch; this.distance = distance; this.fov = fov; }
    }

    /// <summary>
    /// The four steps (tune here). 0: the first days, the café and its street; 1: the block; 2: the district; 3: the franchise
    /// offer, the widest. A MAP OF CITIES, LATER: when branches exist (the phone's Franchise app after the offer), a fifth row
    /// goes here and FramingFor picks it from the save's branches; past the district the scene ends, so that row will want a
    /// map of cities (pins on a drawn map) rather than this camera pulled further back.
    /// </summary>
    // 26 Oct 2026 (Start screen polish): the pitch is the overhead's own kind of angle (the 20 Oct photos read as a diorama at
    // about 43°; steeper reads as a map), and the distances stop short of the built world's edge: the detailed blocks end at
    // about ±40 m, the ring of towers at ±60-70 m and the hill at z 41-82, so the widest row (half-width about 60 m at 112 m)
    // keeps the towers at the frame's edge and never shows the bare ground beyond them.
    public static readonly Framing[] Framings =
    {
        new Framing("the café and its street", 47f, 40f, 38f),
        new Framing("the block", 53f, 62f, 38f),
        new Framing("the district", 59f, 88f, 36f),
        new Framing("the franchise offer", 63f, 112f, 34f),
    };

    /// <summary>The café sits right of the frame's middle by this share of the frame's half-width (Pose), clear of the menu's
    /// column at the bottom left and under the logo's line, so the slabs never cover it as it turns (26 Oct 2026, Start screen
    /// polish).</summary>
    public const float SideShiftShare = .14f;

    /// <summary>The tier's row from the café's stars, as the Franchise app reads them (ReputationLedger.StarsEarned; the names
    /// are ReputationRules.StarNames): up to 2 stars ("Known on the street") the café; 3 ("Neighbourhood favourite") the block;
    /// 4 ("Talk of the district") the district; 5 (the franchise offer) the widest.</summary>
    public static int TierFor(int stars) => stars >= ReputationRules.MaxStars ? 3 : stars == 4 ? 2 : stars == 3 ? 1 : 0;

    public static int StarsNow => SaveManager.Instance != null && SaveManager.Instance.Reputation != null
        ? Mathf.Clamp(SaveManager.Instance.Reputation.StarsEarned, 0, ReputationRules.MaxStars) : 0;

    /// <summary>The row for this save. The one place that reads the save's progress for the camera.</summary>
    public static Framing FramingFor(int stars) => Framings[Mathf.Clamp(TierFor(stars), 0, Framings.Length - 1)];

    /// <summary>Continue's sub-line: "Day 3 · Known on the street" (the tier's name as the Franchise app writes it).</summary>
    public static string ContinueLine()
    {
        int day = SaveManager.Instance != null && SaveManager.Instance.Loaded != null ? Mathf.Max(1, SaveManager.Instance.Loaded.day) : 1;
        return $"Day {day} · {ReputationRules.NameOf(StarsNow)}";
    }

    // ------------------------------------------------------------------ the rule, and the session's state

    public static TitleScreen Instance { get; private set; }
    static bool upNow, passed, labAsked, skipOnce;
    static Mode current = Mode.None;

    /// <summary>The title is on the screen: nothing saves, Ace and the clock are held.</summary>
    public static bool Up => upNow;
    /// <summary>Which face it is showing (None when it isn't).</summary>
    public static Mode Current => upNow ? current : Mode.None;
    /// <summary>The cold open is up: GameBootstrap lays today's full café in memory instead of the save's.</summary>
    public static bool ColdOpenNow => upNow && Instance != null && Instance.Decide() == Mode.ColdOpen;
    /// <summary>The title was left (or never wanted) this Play session.</summary>
    public static bool Passed => passed;
    /// <summary>Scene reloads made by the title this session (labs).</summary>
    public static int Reloads { get; private set; }
    /// <summary>The cold open has frozen.</summary>
    public static bool Frozen => upNow && Instance != null && Instance.frozen;
    /// <summary>"Before it got like this." is playing.</summary>
    public static bool Rewinding => upNow && Instance != null && Instance.rewinding;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        upNow = passed = labAsked = skipOnce = false;
        current = Mode.None;
        Reloads = 0;
        hooks = null;
#if UNITY_EDITOR
        labAsked = TakeKey(LabKey);
        skipOnce = TakeKey(SkipOnceKey);
#endif
    }

#if UNITY_EDITOR
    static bool TakeKey(string key)
    {
        if (PlayerPrefs.GetInt(key, 0) != 1) return false;
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        return true;
    }
#endif

    /// <summary>
    /// THE RULE: whether this scene load shows the start screen. Never twice a Play session; never in a lab session
    /// (CafeLab.Active) but its own lab's; never after Fresh game 1; in the editor only while the dev toggle is on.
    /// </summary>
    public static bool ShouldShow
    {
        get
        {
            if (passed || !Application.isPlaying) return false;
#if UNITY_EDITOR
            if (labAsked) return CafeLab.Active;   // the start screen's own lab, in its own lab session (its own save) only
            if (CafeLab.Active || skipOnce) return false;
            return UnityEditor.EditorPrefs.GetBool(EditorToggleKey, true);
#else
            return !CafeLab.Active;
#endif
        }
    }

    // ------------------------------------------------------------------ this scene's title

    Mode decided = Mode.None;
    bool frozen, rewinding, leaving, uiClockHeld, aceHeld, settingsOpen, frozenBeforeSettings;
    int pinnedMoney = int.MinValue;
    TitleMenuUI ui;
    CinemachineBrain brain;
    CinemachineCamera cam;
    CafeViewMode view;
    Vector3 focus = new Vector3(0f, .6f, 9f);
    float yaw, pitch, distance, fov, orbitSince;
    // 26 Oct 2026 (Start screen polish): a screen is hidden by a CanvasGroup at alpha 0 on its root, re-asserted late each
    // frame (DayIntro's way), not by canvas.enabled: HudCorners sets its canvas enabled again every LateUpdate (at order 0,
    // after this component's -150), so the corners showed over the diorama.
    sealed class HiddenScreen { public Canvas canvas; public CanvasGroup group; public bool mine; public float alphaBefore; }
    readonly List<HiddenScreen> hidden = new List<HiddenScreen>();
    readonly List<GameObject> crowd = new List<GameObject>();
    readonly List<RectTransform> steam = new List<RectTransform>();
    Transform steamRoot;

    /// <summary>The camera's turn round the café now (degrees; labs).</summary>
    public float OrbitYaw => yaw;
    /// <summary>The camera's pose now (labs).</summary>
    public Transform CameraTransform => cam != null ? cam.transform : null;
    /// <summary>The diorama's tier row in use (labs, the report).</summary>
    public int Tier { get; private set; }
    /// <summary>The cold open's crowd: seated patrons, and the people standing in the queue (labs).</summary>
    public int CrowdSeated { get; private set; }
    public int CrowdQueued { get; private set; }
    public TitleMenuUI Menu => ui;

    void Awake()
    {
        // A second one in this same scene only (the old scene's may not have gone yet while a reload brings this one in).
        if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene) { Destroy(this); return; }
        Instance = this;
        if (!ShouldShow) { enabled = false; return; }   // disabled before Start: nothing below ever runs this session
        upNow = true;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (upNow && !leaving && enabled) Leave();   // Play stopped under the title: Ace's hold and the clock's are given back
    }

    Mode Decide()
    {
        if (decided == Mode.None)
            decided = SaveManager.Instance != null && SaveManager.Instance.HasSave ? Mode.Diorama : Mode.ColdOpen;
        return decided;
    }

    void Start()
    {
        current = Decide();
        HoldTheGame();
        ui = TitleMenuUI.Create(transform);
        ui.Chosen += OnChosen;
        MakeCamera();
        // 26 Oct 2026 (Start screen polish): the other screens go under in this first frame, after every HUD has written its
        // own (LateHider runs at 1600, past DayIntro's 1500), and come up from black rather than popping in with the furniture.
        gameObject.AddComponent<LateHider>().owner = this;
        ui.FadeFromBlack(current == Mode.Diorama ? .8f : .5f);
        Debug.Log($"[Start screen] Up: {(current == Mode.Diorama ? "the diorama (a save exists)" : "the cold open (no save yet)")}. Nothing is saved while it shows.");
        StartCoroutine(current == Mode.Diorama ? Diorama() : ColdOpen());
    }

    // ------------------------------------------------------------------ holding the game still (all of it undone by the reload)

    void HoldTheGame()
    {
        // The clock, before its first Update: no bell, no intro, no time passing (OnDisable clears its opening bell).
        DayClock clock = DayClock.Instance;
        if (clock != null) clock.enabled = false;
        CustomerSpawner customers = FindAnyObjectByType<CustomerSpawner>();
        if (customers != null) customers.enabled = false;
        if (current == Mode.ColdOpen)
        {
            // The cold open lays its own crowd at once; the spawner would walk more in from the street.
            PatronSpawner patrons = FindAnyObjectByType<PatronSpawner>();
            if (patrons != null) patrons.enabled = false;
        }
        PlayerMovement.Hold(this);
        aceHeld = true;
        if (!UiClock.Held) { UiClock.Hold(); uiClockHeld = true; }
        // 26 Oct 2026 (his Mac): the money is pinned at the first Update, once GameBootstrap (default order) has loaded the save;
        // pinned here at -150 it held $0.
        view = FindAnyObjectByType<CafeViewMode>();
        if (view != null) focus = view.isometricFocus;
    }

    void Update()
    {
        if (!upNow) return;
        if (pinnedMoney == int.MinValue && ShopEconomy.Instance != null) pinnedMoney = ShopEconomy.Instance.Money;   // 26 Oct 2026 (his Mac)
        // No money changes on the title (a patron's quiet income would move the till): pinned to what was loaded.
        if (pinnedMoney != int.MinValue && ShopEconomy.Instance != null && ShopEconomy.Instance.Money != pinnedMoney)
            ShopEconomy.Instance.SetMoney(pinnedMoney);
        // Back from Settings (the pause phone gave the game back at time scale 1): the cold open freezes again.
        if (settingsOpen && !PausePhone.Paused)
        {
            settingsOpen = false;
            if (frozenBeforeSettings) Time.timeScale = 0f;
            // 26 Oct 2026 (Start screen polish): the phone's Resume released the UI clock's hold (UiClock is one hold for all):
            // held again, as it was before Settings.
            if (uiClockHeld) UiClock.Hold();
            ui.BackFromSettings();
        }
    }

    void LateUpdate()
    {
        if (!upNow) return;
        HideOtherScreens();   // early (-150) too: belt and braces, should the nested hider's order ever not apply
        StepSteam();
    }

    /// <summary>Runs after every screen has written its own alpha for the frame (DayIntro hides at 1500; this at 1600):
    /// UiPins writes its root group's alpha every frame from order 0.</summary>
    [DefaultExecutionOrder(1600)]
    sealed class LateHider : MonoBehaviour
    {
        public TitleScreen owner;
        void LateUpdate() { if (owner != null && upNow) owner.HideOtherScreens(); }
    }

    // Every screen-space canvas but the title's own (and the phone with its pad cursor, for Settings): a CanvasGroup on its
    // root at alpha 0 (added when it has none; kept and re-asserted each frame when it has, since some screens write their
    // own every frame). A screen made meanwhile is caught the next frame. The scene's go with the reload; one that outlives
    // it (the bench cards' host, the frame-rate overlay) is put back to the alpha it had (RestoreScreens, from Leave).
    void HideOtherScreens()
    {
        Transform phone = PausePhone.Instance != null && PausePhone.Instance.Phone != null ? PausePhone.Instance.Phone.transform : null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (ui != null && canvas.transform.IsChildOf(ui.transform)) continue;
            if (phone != null && canvas.transform.IsChildOf(phone)) continue;
            if (canvas.GetComponentInParent<PadCursor>() != null) continue;
            bool known = false;
            foreach (HiddenScreen h in hidden) if (h.canvas == canvas) { known = true; break; }
            if (known) continue;
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            var entry = new HiddenScreen { canvas = canvas, group = group, mine = group == null, alphaBefore = group != null ? group.alpha : 1f };
            if (group == null)
            {
                group = canvas.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                entry.group = group;
            }
            hidden.Add(entry);
        }
        foreach (HiddenScreen h in hidden) if (h.group != null) h.group.alpha = 0f;
    }

    void RestoreScreens()
    {
        foreach (HiddenScreen h in hidden)
        {
            if (h.group == null) continue;
            h.group.alpha = h.alphaBefore;
            if (h.mine) Destroy(h.group);
        }
        hidden.Clear();
    }

    // ------------------------------------------------------------------ the camera

    void MakeCamera()
    {
        Camera main = Camera.main;
        brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            // A cut, not the brain's blend from the player's view (the reload puts the scene's brain back as it was).
            CinemachineBlendDefinition blend = brain.DefaultBlend;
            blend.Time = 0f;
            brain.DefaultBlend = blend;
        }
        var go = new GameObject("Start screen camera (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<CinemachineCamera>();
        cam.Priority = CameraPriority;
        LensSettings lens = cam.Lens;
        lens.FieldOfView = 38f;
        lens.NearClipPlane = .1f;
        lens.FarClipPlane = 1500f;
        cam.Lens = lens;
        yaw = view != null ? view.OverheadAngle.x : 0f;
    }

    void Pose(float atYaw, float atPitch, float atDistance, float atFov, Vector3 around)
    {
        if (cam == null) return;
        Quaternion angle = Quaternion.Euler(atPitch, atYaw, 0f);
        // 26 Oct 2026 (Start screen polish): the frame's middle sits a little left of the café (a share of the half-width at
        // this distance), so the café stands right of the menu's column whatever the bearing.
        float halfWidth = atDistance * Mathf.Tan(atFov * .5f * Mathf.Deg2Rad) * (16f / 9f);
        around -= angle * Vector3.right * (halfWidth * SideShiftShare);
        cam.transform.SetPositionAndRotation(around - angle * Vector3.forward * atDistance, angle);
        LensSettings lens = cam.Lens;
        lens.FieldOfView = atFov;
        cam.Lens = lens;
        // The café's walls fade as they do for the overhead camera: the player's overhead turned to the same bearing (its own
        // limits clamp the pitch and the distance; only the side the walls are seen from matters).
        if (view != null) view.OrbitTo(atYaw, atPitch, atDistance);
    }

    void PoseLookingAt(Vector3 at, Vector3 lookAt, float atFov)
    {
        if (cam == null) return;
        Vector3 forward = lookAt - at;
        if (forward.sqrMagnitude < 1e-6f) forward = cam.transform.forward;
        cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward.normalized, Vector3.up));
        LensSettings lens = cam.Lens;
        lens.FieldOfView = atFov;
        cam.Lens = lens;
    }

    // The orbit: one turn every 120 s, its speed eased in over the first 3 s (never a jerk), held while Settings is up.
    void StepOrbit()
    {
        if (settingsOpen || PausePhone.Paused) return;
        float into = Time.unscaledTime - orbitSince;
        float speed = 360f / OrbitSecondsPerTurn * Mathf.SmoothStep(0f, 1f, into / 3f);
        yaw = Mathf.Repeat(yaw + speed * Mathf.Min(Time.unscaledDeltaTime, .1f), 360f);
        Pose(yaw, pitch, distance, fov, focus);
    }

    // The café's door, from the patrons' way out (PatronSpawner's exit point), else the front of the café.
    Vector3 DoorPoint()
    {
        PatronSpawner patrons = FindAnyObjectByType<PatronSpawner>();
        FieldInfo exit = typeof(PatronSpawner).GetField("exitPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        Transform t = patrons != null && exit != null ? exit.GetValue(patrons) as Transform : null;
        if (t != null) return t.position;
        Rect inside = CafeDaylight.CafeInside;
        return new Vector3(inside.center.x, 1f, inside.yMin);
    }

    // ------------------------------------------------------------------ Seed 1: the diorama

    IEnumerator Diorama()
    {
        Tier = TierFor(StarsNow);
        Framing f = FramingFor(StarsNow);
        pitch = f.pitch; distance = f.distance; fov = f.fov;
        orbitSince = Time.unscaledTime;
        Pose(yaw, pitch, distance, fov, focus);
        yield return null;   // GameBootstrap (after this Start) lays the café's furniture first
        // The settled guests take their seats at once (as at the open); walk-in patrons come on the spawner's own clock.
        try { PatronSpawner.SeatOpeningGuestsNow(); }
        catch (Exception e) { Debug.LogWarning("[Start screen] The settled guests couldn't sit: " + e.Message); }
        ui.Vignette(.22f);   // 26 Oct 2026 (Start screen polish): soft dark edges, as a title has; the menu's corner reads better
        ui.ShowLogo(false);
        ui.ShowMenu(true, ContinueLine());
        while (upNow && !leaving)
        {
            StepOrbit();
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Seed 6: the cold open

    IEnumerator ColdOpen()
    {
        // A livelier, lower 3/4 view than the diorama's: across the counter from the front corner, pushing in slowly (26 Oct
        // 2026, Start screen polish: a touch lower and nearer than the first cut, so the queue and the steam read as people and
        // weather, not as a map; the push-in eases, so the freeze lands on a camera that was still moving).
        float startYaw = yaw + 28f;
        Vector3 at = focus + Vector3.up * .4f;
        Pose(startYaw, 31f, 22f, 42f, at);
        yield return null;   // GameBootstrap (after this Start) lays today's full café first; then its seats are filled
        FillTheCafe();
        MakeSteam();
        ui.Vignette(.12f);
        float into = 0f;
        while (into < ColdOpenLiveSeconds && upNow)
        {
            float t = Mathf.Clamp01(into / ColdOpenLiveSeconds);
            float e = Mathf.SmoothStep(0f, 1f, t);
            Pose(startYaw + 6f * e, Mathf.Lerp(31f, 30f, e), Mathf.Lerp(22f, 19.5f, e), 42f, at);
            into += Time.unscaledDeltaTime;
            yield return null;
        }
        // The freeze: the rush holds mid-action and becomes the backdrop. The frame jolts a hair on the freeze (a 0.3 m drop
        // back, settled in 120 ms) so the stop is felt, then the logo slams in and the menu follows.
        Time.timeScale = 0f;
        frozen = true;
        yaw = startYaw + 6f;
        ui.WarmGrade(true);
        ui.Vignette(.34f);
        Sfx.Play2D("cup.set", .7f);   // PLACEHOLDER: the rush stops dead; a cup set down hard, until a freeze sound is made
        float jolt = 0f;
        while (jolt < .12f)
        {
            jolt += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(jolt / .12f);
            Pose(yaw, 30f, 19.5f + .3f * k, 42f, at);
            yield return null;
        }
        ui.ShowLogo(true);
        yield return Wait(.34f);
        ui.ShowMenu(false, null);
    }

    static IEnumerator Wait(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }

    // Most seats taken (seated at once, as the settled guests are), and a queue standing at the counter: patron bodies, never
    // the day's customers (no story visitor, so never Grace).
    void FillTheCafe()
    {
        PatronSpawner spawner = FindAnyObjectByType<PatronSpawner>();
        GameObject prefab = spawner != null ? spawner.PatronPrefab : null;
        FieldInfo exitField = typeof(PatronSpawner).GetField("exitPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        Transform exit = spawner != null && exitField != null ? exitField.GetValue(spawner) as Transform : null;
        if (prefab == null || exit == null) { Debug.LogWarning("[Start screen] No patron to fill the café with (PatronSpawner's prefab or exit missing)."); return; }

        var seats = new List<TableSeat>();
        if (CafeLayout.Instance != null) CafeLayout.Instance.SeatsOn(seats);
        if (seats.Count == 0) seats.AddRange(FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude));
        var rng = new System.Random(26102026);   // the same rush every first start: he judges the frame by eye
        int keepFree = Mathf.Max(1, seats.Count / 6);
        int free = 0;
        foreach (TableSeat seat in seats)
        {
            if (seat == null || !seat.isActiveAndEnabled || !seat.IsAvailable) continue;
            if (free < keepFree && rng.NextDouble() < .2) { free++; continue; }
            GameObject go = Instantiate(prefab, seat.StandPoint.position, seat.StandPoint.rotation);
            go.name = "Start screen crowd - seated";
            StripCustomer(go);
            PatronBrain brain = go.GetComponent<PatronBrain>();
            if (brain == null) { Destroy(go); continue; }
            brain.StayUntil(.99f);   // the clock never runs here, so they never leave
            try
            {
                if (brain.InitSeated(exit, seat, (float)rng.NextDouble())) { crowd.Add(go); CrowdSeated++; }
                else Destroy(go);
            }
            catch (Exception e) { Destroy(go); Debug.LogWarning("[Start screen] A seat couldn't take a patron: " + e.Message); }
        }

        CounterQueue queue = FindAnyObjectByType<CounterQueue>();
        int slots = queue != null ? Mathf.Min(queue.SlotCount, 4) : 0;
        for (int i = 0; i < slots; i++)
        {
            Transform at = queue.SlotPoint(i);
            if (at == null) continue;
            GameObject go = Instantiate(prefab, at.position, at.rotation);
            go.name = "Start screen crowd - in the queue";
            StripCustomer(go);
            // Standing, waiting their turn: no brain, no walking (their animator's resting pose).
            foreach (Behaviour b in go.GetComponents<Behaviour>())
                if (b is PatronBrain || b is NavMeshAgent || b is NpcJourney || b is NpcLocomotion || b is NpcSeating || b is PersonalSpace || b is NpcSocial)
                    b.enabled = false;
            crowd.Add(go);
            CrowdQueued++;
        }
    }

    static void StripCustomer(GameObject go)
    {
        CustomerBrain stray = go.GetComponent<CustomerBrain>();
        if (stray != null) Destroy(stray);
        CustomerInteractable talkable = go.GetComponent<CustomerInteractable>();
        if (talkable != null) Destroy(talkable);
    }

    // The drinks machine steams: soft puffs rising off it (a world-space canvas, so it draws in any pipeline).
    void MakeSteam()
    {
        BeverageStation station = FindAnyObjectByType<BeverageStation>();
        if (station == null) return;
        var go = new GameObject("Start screen steam (while playing)", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.transform.position = station.transform.position + Vector3.up * 1.45f;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(100f, 100f);
        rect.localScale = Vector3.one * .01f;
        steamRoot = rect;
        for (int i = 0; i < 6; i++)
        {
            var puff = new GameObject("Puff", typeof(RectTransform)).GetComponent<RectTransform>();
            puff.SetParent(rect, false);
            var image = puff.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = UiSkin.Circle;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            steam.Add(puff);
        }
    }

    void StepSteam()
    {
        if (steamRoot == null || Camera.main == null) return;
        steamRoot.rotation = Quaternion.LookRotation(steamRoot.position - Camera.main.transform.position);
        float now = Time.time;   // scaled: the steam freezes with the rush
        for (int i = 0; i < steam.Count; i++)
        {
            float t = Mathf.Repeat(now * .55f + i / (float)steam.Count, 1f);
            RectTransform puff = steam[i];
            puff.anchoredPosition = new Vector2(Mathf.Sin((t + i) * 5f) * 10f, t * 70f);
            float size = Mathf.Lerp(14f, 44f, t);
            puff.sizeDelta = new Vector2(size, size);
            var image = puff.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = new Color(1f, 1f, 1f, .55f * Mathf.Sin(t * Mathf.PI));
        }
    }

    // ------------------------------------------------------------------ the menu's choices

    void OnChosen(string id)
    {
        if (leaving) return;
        switch (id)
        {
            case TitleMenuUI.ContinueId: StartCoroutine(ContinueGame()); break;
            case TitleMenuUI.NewGameId:
                if (current == Mode.Diorama) ui.ShowConfirm();
                else StartCoroutine(RewindIntoDayOne());
                break;
            case TitleMenuUI.StartNewId: StartCoroutine(NewCafeOverSave()); break;
            case TitleMenuUI.SettingsId: OpenSettings(); break;
            case TitleMenuUI.CreditsId: ui.ShowCredits(); break;
            case TitleMenuUI.QuitId: Quit(); break;
        }
    }

    // Continue: the camera dives toward the café's door (1.2 s, easing in), a fade, and the day boots exactly as today.
    IEnumerator ContinueGame()
    {
        leaving = true;
        ui.Lock();
        yield return DiveAndFade();
        Leave();
        Reload();
    }

    // 26 Oct 2026 (Start screen polish): the fade begins while the camera is still diving (its last 0.4 s), so the picture
    // never stops dead at the door before it goes black; the menu slips away as the dive begins.
    IEnumerator DiveAndFade()
    {
        ui.HideMenu();
        StartCoroutine(Dive());
        yield return Wait(Mathf.Max(0f, DiveSeconds - FadeSeconds));
        yield return ui.FadeToBlack(FadeSeconds);
    }

    IEnumerator Dive()
    {
        if (cam == null) yield break;
        Vector3 fromPos = cam.transform.position;
        Quaternion fromRot = cam.transform.rotation;
        float fromFov = cam.Lens.FieldOfView;
        Vector3 door = DoorPoint();
        Vector3 outward = door - new Vector3(focus.x, door.y, focus.z);
        outward = outward.sqrMagnitude > .01f ? outward.normalized : Vector3.back;
        Vector3 toPos = door + outward * 2.2f + Vector3.up * 1.7f;
        Vector3 lookAt = door - outward * 3f + Vector3.up * 1.2f;
        Quaternion toRot = Quaternion.LookRotation((lookAt - toPos).normalized, Vector3.up);
        float into = 0f;
        while (into < DiveSeconds)
        {
            into += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(into / DiveSeconds);
            float e = t * t * t;   // ease in: it gathers speed into the door
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
            LensSettings lens = cam.Lens;
            lens.FieldOfView = Mathf.Lerp(fromFov, 52f, e);
            cam.Lens = lens;
            yield return null;
        }
    }

    // New Game over a save (confirmed on the card): the save is copied to backups first, the way Fresh game 1 keeps it, then put
    // aside so the next boot is a real new game (no file: the half-finished café, Day 1). If the copy fails, nothing changes.
    IEnumerator NewCafeOverSave()
    {
        SaveManager save = SaveManager.Instance;
        string kept = null, problem = null;
        bool ok = save == null || save.StartNewGameKeepingACopy(out kept, out problem);
        if (!ok)
        {
            Debug.LogError("[Start screen] " + problem);
            ui.ShowProblem("Couldn't keep a copy, so nothing changed.");
            yield break;
        }
        if (!string.IsNullOrEmpty(kept)) Debug.Log($"[Start screen] New game: your café is kept at {kept}, byte for byte.");
        leaving = true;
        ui.Lock();
        yield return DiveAndFade();
        Leave();
        Reload();
    }

    // "Before it got like this.": the frozen rush rewinds (a UI fast-rewind), the crowd goes, black, and Day 1 begins.
    IEnumerator RewindIntoDayOne()
    {
        leaving = true;
        rewinding = true;
        ui.Lock();
        Sfx.Play2D("intro.grind");   // PLACEHOLDER: the grinder's 1.3 s whirr as the rewind's, until a rewind sound is made
        StartCoroutine(ui.PlayRewind(RewindSeconds));
        // The crowd goes, last in first out, over the rewind's first 70%; the camera pulls back a touch.
        float into = 0f;
        int gone = 0;
        Vector3 fromPos = cam != null ? cam.transform.position : Vector3.zero;
        while (into < RewindSeconds)
        {
            into += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(into / RewindSeconds);
            int shouldBeGone = Mathf.RoundToInt(Mathf.Clamp01(t / .7f) * crowd.Count);
            while (gone < shouldBeGone && gone < crowd.Count)
            {
                GameObject body = crowd[crowd.Count - 1 - gone];
                if (body != null) body.SetActive(false);
                gone++;
            }
            if (cam != null)
            {
                Vector3 back = -cam.transform.forward * 3f * Mathf.SmoothStep(0f, 1f, t);
                cam.transform.position = fromPos + back + (Vector3)(UnityEngine.Random.insideUnitCircle * .04f * (1f - t));
            }
            yield return null;
        }
        if (steamRoot != null) steamRoot.gameObject.SetActive(false);
        yield return ui.FadeToBlack(FadeSeconds);
        rewinding = false;
        Leave();
        Reload();
    }

    // Settings: the game's own (the phone's Settings app), over the title. Back puts it away and returns to the menu.
    void OpenSettings()
    {
        PausePhone phone = PausePhone.Instance;
        if (phone == null || phone.Phone == null) { ui.Refuse(); return; }
        float scale = Time.timeScale;
        frozenBeforeSettings = scale <= 0f;
        // The phone stays in the pocket while something holds Ace or the game is frozen (PausePhone.Busy): let go for the call.
        PlayerMovement.Release(this);
        if (frozenBeforeSettings) Time.timeScale = 1f;
        bool opened = phone.Open();
        PlayerMovement.Hold(this);
        if (!opened)
        {
            Time.timeScale = scale;
            ui.Refuse();
            return;
        }
        phone.Phone.Open(RecapPhone.App.Settings);
        if (phone.Phone.CloseLabel != null) phone.Phone.CloseLabel.text = "Back";
        settingsOpen = true;
        ui.ToSettings();
    }

    void Quit()
    {
        leaving = true;
        Debug.Log("[Start screen] Quit.");
        Leave();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ leaving

    /// <summary>Gives back what the title holds that would outlive the scene (Ace's hold and the UI clock's are statics; a
    /// screen that isn't the scene's), time scale 1, and marks the title passed for the rest of this Play session.</summary>
    void Leave()
    {
        passed = true;
        upNow = false;
        if (aceHeld) { PlayerMovement.Release(this); aceHeld = false; }
        if (uiClockHeld) { UiClock.Release(); uiClockHeld = false; }
        RestoreScreens();
        Time.timeScale = 1f;
    }

    // Reloads the café scene; once it is in, runs the AfterSceneLoad hooks again (see the header).
    static void Reload()
    {
        Reloads++;
        CutawayWall.ForgetCandidates();
        // 26 Oct 2026 (Start screen polish): NpcLifeProps surveys the scene once a Play session (searched) and keeps the
        // laptops, bags and bin station it found; after a reload those are the old scene's, so it is asked to look again.
        NpcLifeProps.Refresh();
        SceneManager.sceneLoaded -= AfterReload;
        SceneManager.sceneLoaded += AfterReload;
        Scene active = SceneManager.GetActiveScene();
        Debug.Log($"[Start screen] Leaving: the scene reloads ({active.name}) and the day boots as it always has.");
        if (active.buildIndex >= 0) SceneManager.LoadScene(active.buildIndex, LoadSceneMode.Single);
        else
        {
#if UNITY_EDITOR
            // A scene not in the Build Settings (the editor's playtest scene): loaded by its path.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(active.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(active.name, LoadSceneMode.Single);
#endif
        }
    }

    static List<MethodInfo> hooks;

    static void AfterReload(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= AfterReload;
        NpcLifeProps.Refresh();   // 26 Oct 2026 (Start screen polish): again now the new scene is in (see Reload)
        if (hooks == null)
        {
            hooks = new List<MethodInfo>();
            const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type[] types;
            try { types = typeof(TitleScreen).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            foreach (Type type in types)
            {
                if (type == null || type == typeof(TitleScreen) || type.ContainsGenericParameters) continue;
                foreach (MethodInfo method in type.GetMethods(Statics))
                {
                    if (method.GetParameters().Length != 0) continue;
                    var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    if (attribute != null && attribute.loadType == RuntimeInitializeLoadType.AfterSceneLoad) hooks.Add(method);
                }
            }
        }
        foreach (MethodInfo hook in hooks)
        {
            try { hook.Invoke(null, null); }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                Debug.LogWarning($"[Start screen] After the reload, {hook.DeclaringType.Name}.{hook.Name} failed: {inner.Message}");
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>The start screen's lab only: shows the title once more in this lab session (the scene reloads). False anywhere
    /// else, or while it is already up.</summary>
    public static bool AgainForLab()
    {
        if (!labAsked || !CafeLab.Active || upNow) return false;
        passed = false;
        Reload();
        return true;
    }
#endif
}
