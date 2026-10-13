using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE DAY INTRO (12 Oct 2026; Cinematics & Camera, its first job)
//
// Mansoor, 12 Oct, 01:19: "i want also an intro every time the day starts so that it always feels fresh, something like
// coffee grinding and screws unwinding, and then those all being blended together, idk just an idea." His call (the
// findings doc §6): every day, 6-8 s, skippable, with Short and Off in the settings.
//
// Every morning, where the opening bell used to ring (DayClock.Update: a beat into the day, never inside StartDay), the
// intro begins instead and rings the bell itself, where its sign beat lands. Four shots from a pool, cut on the beat, each
// a close-up of something the café really does, in the order of the day's work: the café (the grinder: beans, or the burr
// collar) -> the bench (a screw backing out of a phone and dropping into the tray, or the magnifier lamp coming on over the
// tools) -> a third (a latte pouring, a fixed phone's screen lighting up, or, on a morning Grace is coming early, her house
// across the street) -> the open (the storefront with its name, or the round blade sign with the street waking beyond it).
// "Blended" is the match cut: round, spinning shapes becoming the next (the hopper's beans turning -> the screw head turning
// -> the cup's rim with the pour swirling -> the round sign), and the sounds stacked into one beat. Then the pull-out: the
// brain's own 0.75 s blend to the player's camera (the last shot becomes the game) as the day's title slides in ("Tuesday ·
// Day 2" and one line about the day, PLACEHOLDER COPY for his sister), the sting under it. Never the same twice: the free
// beats are drawn from the pool and the order is never last morning's; Day 1 plays the canonical order (its line is the
// hook: what the café is); a morning Grace is coming early shows her house.
//
// What it holds, and gives back exactly: the DayClock is disabled for its length (OnDisable records the stop, OnEnable adds
// the stopped time back, as the night walk does), the two spawners are held, Ace is held (PlayerMovement.Hold: no walking,
// no E; the pause phone counts that as busy), the view ignores input (CafeViewMode.OverlayOwnsInput reads Playing), the
// screen's own clock stands still (UiClock.Hold: the Day 1 guide's hints, the HUD's corners), every other screen is faded
// out (a CanvasGroup on each root canvas, anything made meanwhile caught each frame) and faded back in on the pull-out. Its
// own camera is one CinemachineCamera made at run time at priority 50 (never saved in the scene) and moved by hand on
// unscaled time: a cut is a teleport, the only Cinemachine blend is the last one, to the player's camera, which therefore
// comes back exactly where it was. Props (the phone, the screw, the cup, the pour) are photocopies of the game's own meshes,
// made for the shot and removed after; nothing in the scene or in gameplay is touched. If a shot's subject is missing the
// shot is dropped; if the pool is empty the morning plays the Short form; if anything throws, the intro ends at once and the
// day starts.
//
// Never a hostage: any key, mouse button or pad button from the first frame skips to a shortened pull-out (0.5 s); the
// title still shows for its 1.2 s minimum (it carries the day's one line), over a game that is already yours. Short: the
// sting and the title over the player's own camera, 2.4 s. Off: the bell rings as before. In a lab session it is off
// (CafeLab.Active), unless the lab asks for it (ForceOn, or the LabKey the whole-game playtest can set): the Day 1 guide,
// the night labs and the conversation checks start their days without it. The lab: Fixit Fidget > Cinematics > Day intro.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(1500)]   // its LateUpdate after every screen's own (UiPins, BenchCards, Juice 910, NoticeMark 915, CircuitPuzzle 1000)
[DisallowMultipleComponent]
public sealed class DayIntro : MonoBehaviour
{
    public enum Mode { Full, Short, Off }

    /// <summary>A lab session that wants the intro on (the whole-game playtest sets it before Play; read once, then forgotten).</summary>
    public const string LabKey = "FixitFidget.DayIntro.Lab";
    /// <summary>Last morning's order of shots ("grinder.beans|bench.screw|drinks.pour|street.front"): the next morning is never the same.</summary>
    public const string LastOrderKey = "FixitFidget.DayIntro.LastOrder";
    public const int CameraPriority = 50;
    /// <summary>Its screen draws over everything but the pad's cursor (5000): the phone by day is 95.</summary>
    public const int SortingOrder = 200;
    /// <summary>The safe margins the title keeps from every edge, as a share of the screen.</summary>
    public const float SafeMargin = .05f;
    /// <summary>The title's least time on screen, even on a skip (it carries the day's one line).</summary>
    public const float TitleMinimumSeconds = 1.2f;
    /// <summary>Where the title's middle sits: this share of the screen's height up from the bottom (above the middle, clear of the prompt).</summary>
    public const float TitleUp = .58f;
    /// <summary>The letterbox bars' height, as a share of the screen's height (each).</summary>
    public const float BarShare = .055f;

    // ---- timing (seconds of real time)
    const float FadeIn = .25f, BarsOut = .3f;
    const float PullOutSeconds = .75f, SkipPullOutSeconds = .5f;
    const float TitleIn = .35f, TitleHold = 2.4f, TitleOut = .4f;
    const float ShortSeconds = 2.4f, ShortTitleAt = .15f, ShortStingAt = .1f, ShortBellAt = .45f;
    const float HudReturnAt = .3f, HudReturnSeconds = .35f;   // into the pull-out
    const float CurtainWaitMost = 4f;                          // the night's morning fade, if the day starts under it
    const float DayOpenAfterSign = .15f;

    // ------------------------------------------------------------------ what other code reads

    public static DayIntro Instance { get; private set; }
    /// <summary>The intro owns the screen and the controls: from its first frame until the pull-out lands (the title may outlast it).</summary>
    public static bool Playing => Instance != null && Instance.playing;
    /// <summary>A lab's hand: the intro plays in this lab session whatever the setting says (DayIntroCheck).</summary>
    public static bool ForceOn { get; set; }
    /// <summary>A lab's hand: stands in for the player's setting while set.</summary>
    public static Mode? ModeOverride { get; set; }
    /// <summary>Mornings the intro has played this session (any mode but Off).</summary>
    public static int MorningsPlayed { get; private set; }
    /// <summary>The last morning: its mode, its order of shots, how long it held the controls, whether it rang the bell, whether it was skipped.</summary>
    public static Mode LastMode { get; private set; }
    public static string LastOrder { get; private set; } = "";
    public static float LastRunSeconds { get; private set; }
    public static bool LastRangTheBell { get; private set; }
    public static bool LastSkipped { get; private set; }
    /// <summary>The title block as last laid out, in the canvas's reference pixels (width, height): the margins are checked from it.</summary>
    public static Vector2 LastTitleSize { get; private set; }
    /// <summary>A shot began (its id). The lab photographs each at its best moment.</summary>
    public static event Action<string> ShotBegan;

    /// <summary>The shot playing now: its id ("" between shots and in Short), how far into it, how long it is, and the moments worth a photo (0-1).</summary>
    public string ShotId => shot != null ? shot.Id : "";
    public int ShotIndex => shotIndex;
    public float ShotTime => shotTime;
    public float ShotLength => shot != null ? shot.Length : 0f;
    public IReadOnlyList<float> ShotMoments => shot != null ? shot.Moments : Array.Empty<float>();
    /// <summary>This morning's order ("a|b|c|d"), its mode, the seconds since it began, whether a skip was asked for, whether the title is up.</summary>
    public string Order => order;
    public Mode PlayingMode => mode;
    public float Elapsed => elapsed;
    public bool SkipRequested => skipRequested;
    public bool TitleShowing => titleShown && titleGroup != null && titleGroup.alpha > .01f;
    public float TitleShownFor => titleShown ? elapsed - titleShownAt : 0f;
    public bool PullingOut => phase == Phase.PullOut;
    /// <summary>The root canvases faded out right now (checks).</summary>
    public int ScreensHidden => hidden.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        ForceOn = false;
        ModeOverride = null;
        MorningsPlayed = 0;
        LastMode = Mode.Full;
        LastOrder = "";
        LastRunSeconds = 0f;
        LastRangTheBell = false;
        LastSkipped = false;
        LastTitleSize = Vector2.zero;
        ShotBegan = null;
        labWanted = false;
#if UNITY_EDITOR
        // A lab session that asked for the intro (the whole-game playtest from Day 1, played by hand): read once, then forgotten.
        if (PlayerPrefs.GetInt(LabKey, 0) == 1)
        {
            labWanted = true;
            PlayerPrefs.DeleteKey(LabKey);
            PlayerPrefs.Save();
        }
#endif
    }

    static bool labWanted;

    // ------------------------------------------------------------------ the setting

    /// <summary>The mode this morning would play in: a lab's override, else the player's setting (GameSettings.DayIntro: Full, Short or Off).</summary>
    public static Mode ModeNow()
    {
        if (ModeOverride.HasValue) return ModeOverride.Value;
        // By name, so this compiles whether the UI lead nests DayIntroMode in GameSettings or declares it beside it.
        string setting = GameSettings.DayIntro.ToString();
        if (string.Equals(setting, "Off", StringComparison.OrdinalIgnoreCase)) return Mode.Off;
        if (string.Equals(setting, "Short", StringComparison.OrdinalIgnoreCase)) return Mode.Short;
        return Mode.Full;
    }

    /// <summary>Whether a morning that starts now would play the intro at all: not Off, and not a lab session that didn't ask for it.</summary>
    public static bool WouldPlay => ModeNow() != Mode.Off && (!CafeLab.Active || ForceOn || labWanted);

    // ------------------------------------------------------------------ beginning

    /// <summary>
    /// The clock's call where its opening bell rang (DayClock.Update). True: the intro has begun and owns the bell (it rings
    /// day.open where its sign beat lands); false: nothing plays (Off, a lab, a missing camera, or one already playing) and the
    /// clock rings the bell as before.
    /// </summary>
    public static bool TryBegin(DayClock clock)
    {
        if (clock == null || !Application.isPlaying) return false;
        if (!WouldPlay) return false;
        if (Instance != null) return false;   // one morning at a time: a second start while one plays keeps the old bell
        return Begin(ModeNow(), clock, clock.Day, null);
    }

    /// <summary>
    /// A lab's hand: a morning in the given mode, titled for <paramref name="dayForTitle"/>, with an explicit order of shot ids
    /// (null: the picker's own choice), whatever the setting and the lab state say. True when it began.
    /// </summary>
    public static bool BeginForCheck(Mode mode, int dayForTitle, string[] order)
    {
        if (!Application.isPlaying || mode == Mode.Off) return false;
        if (Instance != null) return false;
        DayClock clock = DayClock.Instance;
        if (clock == null) return false;
        return Begin(mode, clock, Mathf.Max(1, dayForTitle), order);
    }

    /// <summary>Skips as a key would: the pull-out at once (the title keeps its minimum). For checks and for anything scripted.</summary>
    public static void RequestSkip()
    {
        if (Instance != null) Instance.Skip();
    }

    static bool Begin(Mode mode, DayClock clock, int day, string[] order)
    {
        var go = new GameObject("Day intro (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        DayIntro intro = go.AddComponent<DayIntro>();
        try
        {
            intro.Setup(mode, clock, day, order);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[Day intro] It couldn't begin, so the day starts without it: " + e.Message);
            Debug.LogException(e);
            try { intro.EndNow(); } catch (Exception) { }
            Destroy(go);
            return false;
        }
    }

    // ------------------------------------------------------------------ the morning's state

    enum Phase { Curtain, Shots, PullOut, Title, Done }

    Mode mode;
    DayClock clock;
    int day;
    string order = "";
    Phase phase = Phase.Curtain;
    bool playing, skipRequested, underTheCurtain;
    float elapsed, curtainWaited;
    int startFrame;

    // the shots
    readonly List<Shot> shots = new List<Shot>();
    Shot shot;
    int shotIndex = -1;
    float shotTime;
    Transform propsRoot;

    // the camera
    Camera main;
    CinemachineBrain brain;
    CinemachineCamera cam;
    float blendTimeBefore;
    bool blendChanged;
    int blendRestoreAfterFrame = -1;   // the brain reads its default blend in its LateUpdate: put back the frame after
    float pullOutStarted = -1f, pullOutSeconds;

    // the world, held
    bool clockHeld, customerSpawnerHeld, patronSpawnerHeld, aceHeld, uiClockHeld, released;
    CustomerSpawner customerSpawner;
    PatronSpawner patronSpawner;

    // the screen
    Canvas canvas;
    Image black, barTop, barBottom;
    RectTransform titleRoot;
    CanvasGroup titleGroup;
    TextMeshProUGUI dayText, lineText, skipHint;
    float barHeight;
    bool titleShown, titleFading, titleDone, stingPlayed, bellRang;
    float titleShownAt, titleFadeAt = -1f, hudReturn = -1f;

    // the other screens, faded out
    sealed class HiddenScreen
    {
        public Canvas canvas;
        public CanvasGroup group;
        public bool mine;
        public float alphaBefore;
    }
    readonly List<HiddenScreen> hidden = new List<HiddenScreen>();
    bool hidingScreens;

    static readonly PadButton[] PadButtons = (PadButton[])Enum.GetValues(typeof(PadButton));

    void Setup(Mode wanted, DayClock dayClock, int dayNumber, string[] explicitOrder)
    {
        Instance = this;
        mode = wanted;
        clock = dayClock;
        day = dayNumber;
        startFrame = Time.frameCount;
        main = Camera.main;
        if (main == null) throw new InvalidOperationException("no main camera");
        brain = main.GetComponent<CinemachineBrain>();
        // Without a Cinemachine brain there is no camera to borrow: the sting and the title still open the day.
        if (mode == Mode.Full && brain == null) mode = Mode.Short;
        if (mode == Mode.Full)
        {
            propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(transform, false);
            PickShots(explicitOrder);
            if (shots.Count == 0) mode = Mode.Short;   // no café to film (another scene): the title alone
        }
        HoldTheWorld();
        MakeScreen();
        if (mode == Mode.Full)
        {
            MakeCamera();
            BeginShot(0);
            // A morning that opens under the night's curtain (NightCycle's fade from black): the first shot waits, framed, until
            // the curtain is up, so the night's own fade reveals the beans rather than half of the second shot (and this
            // intro's own fade from black isn't wanted on top).
            underTheCurtain = NightCycle.Instance != null && NightCycle.Instance.Running;
            phase = underTheCurtain ? Phase.Curtain : Phase.Shots;
        }
        else phase = Phase.Shots;   // Short: the title's own little timeline
        playing = true;
        MorningsPlayed++;
        LastMode = mode;
        LastOrder = order;
        LastSkipped = false;
        LastRangTheBell = false;
        StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ the sequencer

    IEnumerator Run()
    {
        // All the work of a frame happens inside Tick, where an exception can be caught: a coroutine that throws stops where
        // it is, and the day would never start.
        while (true)
        {
            bool more;
            try { more = Tick(); }
            catch (Exception e)
            {
                Debug.LogError("[Day intro] Something went wrong, so the intro ends and the day starts: " + e.Message);
                Debug.LogException(e);
                more = false;
            }
            if (!more) break;
            yield return null;
        }
        EndNow();
        Destroy(gameObject);
    }

    bool Tick()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);   // a hitch never jumps the picture
        elapsed += dt;
        // Any key, mouse button or pad button skips, from the first frame (not the frame that began it: the press that opened
        // tomorrow can't also skip it). A press once the pull-out has begun only sends the title on its way.
        if (Time.frameCount != startFrame && AnyPress()) Skip();

        // The brain reads its default blend when the live camera changes, in its own LateUpdate; the frame after, it is put back.
        if (blendRestoreAfterFrame >= 0 && Time.frameCount > blendRestoreAfterFrame) { RestoreBlend(); blendRestoreAfterFrame = -1; }

        switch (phase)
        {
            case Phase.Curtain:
                curtainWaited += dt;
                if (shot != null) shot.Frame(this, 0f, 0f);   // framed, still, under the black
                if (!(NightCycle.Instance != null && NightCycle.Instance.Running) || curtainWaited >= CurtainWaitMost) phase = Phase.Shots;
                break;

            case Phase.Shots:
                if (mode == Mode.Full) TickShots(dt);
                else TickShort();
                break;

            case Phase.PullOut:
                TickPullOut();
                break;

            case Phase.Title:
                if (titleDone) phase = Phase.Done;
                break;

            case Phase.Done:
                return false;
        }
        UpdateScreen(dt);
        return phase != Phase.Done;
    }

    void TickShots(float dt)
    {
        if (shot == null) { BeginPullOut(false); return; }
        shotTime += dt;
        shot.Frame(this, shotTime, dt);
        shot.PlayCues(this, shotTime);
        if (shotTime >= shot.Length)
        {
            EndShot();
            if (shotIndex + 1 < shots.Count) BeginShot(shotIndex + 1);
            else BeginPullOut(false);
        }
    }

    void TickShort()
    {
        if (!stingPlayed && elapsed >= ShortStingAt) { stingPlayed = true; Sfx.Play2D("intro.sting"); }
        if (!titleShown && elapsed >= ShortTitleAt) ShowTitle();
        if (!bellRang && elapsed >= ShortBellAt) RingTheBell();
        if (elapsed >= ShortSeconds)
        {
            ReleaseTheWorld();
            phase = Phase.Title;
        }
    }

    void BeginShot(int index)
    {
        shotIndex = index;
        shot = shots[index];
        shotTime = 0f;
        try
        {
            shot.Begin(this);
            shot.Frame(this, 0f, 0f);
        }
        catch (Exception e)
        {
            // One shot's trouble costs that shot, not the morning.
            Debug.LogWarning($"[Day intro] The shot {shot.Id} couldn't be set up ({e.Message}); on to the next.");
            try { shot.End(this); } catch (Exception) { }
            shot = null;
            if (index + 1 < shots.Count) BeginShot(index + 1);
            return;
        }
        ShotBegan?.Invoke(shot.Id);
    }

    void EndShot()
    {
        if (shot == null) return;
        try { shot.End(this); }
        catch (Exception e) { Debug.LogWarning($"[Day intro] Putting the shot {shot.Id} away: {e.Message}"); }
        shot = null;
    }

    // ------------------------------------------------------------------ the pull-out

    void BeginPullOut(bool skipped)
    {
        EndShot();
        phase = Phase.PullOut;
        pullOutSeconds = skipped ? SkipPullOutSeconds : PullOutSeconds;
        pullOutStarted = elapsed;
        // The brain's own blend to whichever camera the player had, over the pull-out's length; its default is put back next
        // frame. The camera stays alive and still until the blend has landed, so the brain can read it the whole way.
        if (brain != null) SetBlend(pullOutSeconds);
        if (cam != null) cam.Priority = -1000;
        if (!bellRang) RingTheBell();   // the sign beat never came (dropped or skipped): the day opens all the same
        if (!stingPlayed) { stingPlayed = true; Sfx.Play2D("intro.sting"); }
        if (!titleShown) ShowTitle();
    }

    void TickPullOut()
    {
        float into = elapsed - pullOutStarted;
        if (hudReturn < 0f && into >= HudReturnAt) hudReturn = 0f;
        bool landed = into >= pullOutSeconds + .05f || into >= .15f && brain != null && !brain.IsBlending;
        if (!landed) return;
        ReleaseTheWorld();
        if (cam != null) { Destroy(cam.gameObject); cam = null; }
        phase = Phase.Title;
    }

    // ------------------------------------------------------------------ the skip

    void Skip()
    {
        switch (phase)
        {
            case Phase.Curtain:
            case Phase.Shots:
                if (skipRequested) return;
                skipRequested = true;
                LastSkipped = true;
                if (mode == Mode.Full) BeginPullOut(true);
                else
                {
                    if (!stingPlayed) { stingPlayed = true; Sfx.Play2D("intro.sting"); }
                    if (!titleShown) ShowTitle();
                    if (!bellRang) RingTheBell();
                    ReleaseTheWorld();
                    phase = Phase.Title;
                }
                break;
            case Phase.PullOut:
            case Phase.Title:
                SkipTheTitle();   // a press once the game is coming back: the title goes once it has been read
                break;
        }
    }

    // The title goes at once if it has had its minimum; else at its minimum.
    void SkipTheTitle()
    {
        if (!titleShown || titleFading) return;
        titleFadeAt = Mathf.Max(elapsed, titleShownAt + TitleMinimumSeconds);
    }

    static bool AnyPress()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)) return true;
        foreach (PadButton button in PadButtons)
            if (PadInput.Pressed(button)) return true;
        return false;
    }

    // ------------------------------------------------------------------ the bell, the sting

    /// <summary>The day's own opening bell, once per morning: where the sign beat lands (Full), under the title (Short).</summary>
    void RingTheBell()
    {
        if (bellRang) return;
        bellRang = true;
        LastRangTheBell = true;
        Sfx.Play2D("day.open");
    }

    // ------------------------------------------------------------------ holding the world

    void HoldTheWorld()
    {
        if (clock != null && clock.enabled) { clock.enabled = false; clockHeld = true; }   // OnDisable notes the stop; OnEnable adds it back
        customerSpawner = FindAnyObjectByType<CustomerSpawner>();
        if (customerSpawner != null && customerSpawner.enabled) { customerSpawner.enabled = false; customerSpawnerHeld = true; }
        patronSpawner = FindAnyObjectByType<PatronSpawner>();
        if (patronSpawner != null && patronSpawner.enabled) { patronSpawner.enabled = false; patronSpawnerHeld = true; }
        PlayerMovement.Hold(this);
        aceHeld = true;
        if (!UiClock.Held) { UiClock.Hold(); uiClockHeld = true; }
        hidingScreens = mode == Mode.Full;
    }

    /// <summary>The controls, the clock and the spawners back, exactly as they were. Safe to call twice.</summary>
    void ReleaseTheWorld()
    {
        if (released) return;
        released = true;
        playing = false;
        LastRunSeconds = elapsed;
        if (clockHeld && clock != null) clock.enabled = true;
        if (customerSpawnerHeld && customerSpawner != null) customerSpawner.enabled = true;
        if (patronSpawnerHeld && patronSpawner != null) patronSpawner.enabled = true;
        if (aceHeld) PlayerMovement.Release(this);
        if (uiClockHeld) UiClock.Release();
        clockHeld = customerSpawnerHeld = patronSpawnerHeld = aceHeld = uiClockHeld = false;
        if (hudReturn < 0f) hudReturn = 0f;
    }

    /// <summary>Everything back and every prop gone, whatever state the morning was in (an error, Play mode ending, a scene unload).</summary>
    void EndNow()
    {
        if (!released)
        {
            if (!bellRang) { try { RingTheBell(); } catch (Exception) { } }
            try { ReleaseTheWorld(); } catch (Exception) { }
        }
        try { EndShot(); } catch (Exception) { }
        try { RestoreScreens(); } catch (Exception) { }
        if (brain != null && blendChanged) { try { RestoreBlend(); } catch (Exception) { } }
        if (cam != null) { Destroy(cam.gameObject); cam = null; }
        if (canvas != null) { Destroy(canvas.gameObject); canvas = null; }
        if (propsRoot != null) { Destroy(propsRoot.gameObject); propsRoot = null; }
        playing = false;
        phase = Phase.Done;
        if (Instance == this) Instance = null;
    }

    void OnDestroy() => EndNow();

    void LateUpdate()
    {
        if (phase == Phase.Done) return;
        if (hidingScreens) HideScreens();
        // No arrow over a film: the view unlocks the cursor while something else owns input (CafeViewMode, order -100, runs first).
        if (playing) Cursor.visible = false;
    }

    // ------------------------------------------------------------------ the camera

    void MakeCamera()
    {
        var go = new GameObject("Day intro camera (while playing)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<CinemachineCamera>();
        // The first cut is a cut: the brain would otherwise blend from the player's view over its 0.75 s. Its default is put
        // back the frame after, once the brain has read it.
        SetBlend(0f);
        cam.Priority = CameraPriority;
        LensSettings lens = cam.Lens;
        lens.FieldOfView = 40f;
        lens.NearClipPlane = .01f;
        lens.FarClipPlane = 1000f;
        cam.Lens = lens;
        go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
    }

    /// <summary>Puts the intro's camera at <paramref name="at"/>, looking at <paramref name="lookAt"/>, with this lens (vertical degrees) and roll.</summary>
    public void Place(Vector3 at, Vector3 lookAt, float fov, float roll = 0f, float near = .01f)
    {
        if (cam == null) return;
        Vector3 forward = lookAt - at;
        if (forward.sqrMagnitude < 1e-8f) forward = cam.transform.forward;
        Quaternion look = Quaternion.LookRotation(forward.normalized, Vector3.up);
        if (Mathf.Abs(roll) > .001f) look = look * Quaternion.AngleAxis(roll, Vector3.forward);
        cam.transform.SetPositionAndRotation(at, look);
        LensSettings lens = cam.Lens;
        lens.FieldOfView = Mathf.Clamp(fov, 10f, 100f);
        lens.NearClipPlane = Mathf.Max(.003f, near);
        cam.Lens = lens;
    }

    // The brain's default blend, lent for one camera change: 0 is a cut whatever the style. Put back by RestoreBlend the
    // frame after (Tick), once the brain's LateUpdate has read it.
    void SetBlend(float seconds)
    {
        if (brain == null) return;
        CinemachineBlendDefinition blend = brain.DefaultBlend;
        if (!blendChanged) { blendTimeBefore = blend.Time; blendChanged = true; }
        blend.Time = Mathf.Max(0f, seconds);
        brain.DefaultBlend = blend;
        blendRestoreAfterFrame = Time.frameCount;
    }

    void RestoreBlend()
    {
        if (brain == null || !blendChanged) return;
        CinemachineBlendDefinition blend = brain.DefaultBlend;
        blend.Time = blendTimeBefore;
        brain.DefaultBlend = blend;
        blendChanged = false;
        blendRestoreAfterFrame = -1;
    }

    // ------------------------------------------------------------------ the other screens

    // Every root canvas but the night's own (its curtain may be fading this very morning), the pad's cursor and this one: a
    // CanvasGroup on its root at alpha 0 (added when it has none, kept and re-asserted each frame when it has, since some
    // screens write their own every frame). Anything made during the intro is caught the next frame. On the pull-out they
    // all come back together, over HudReturnSeconds, to exactly the alpha they had.
    void HideScreens()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.isRootCanvas || !c.enabled) continue;
            if (canvas != null && c == canvas) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<NightCycle>(true) != null) continue;
            if (c.GetComponentInParent<PadCursor>(true) != null) continue;
            bool known = false;
            foreach (HiddenScreen h in hidden) if (h.canvas == c) { known = true; break; }
            if (known) continue;
            CanvasGroup group = c.GetComponent<CanvasGroup>();
            var entry = new HiddenScreen { canvas = c, group = group, mine = group == null, alphaBefore = group != null ? group.alpha : 1f };
            if (group == null)
            {
                group = c.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                entry.group = group;
            }
            hidden.Add(entry);
        }
        float share = hudReturn < 0f ? 0f : Mathf.Clamp01(hudReturn);
        foreach (HiddenScreen h in hidden)
            if (h.group != null) h.group.alpha = h.alphaBefore * share;
    }

    void RestoreScreens()
    {
        hidingScreens = false;
        foreach (HiddenScreen h in hidden)
        {
            if (h.group == null) continue;
            h.group.alpha = h.alphaBefore;   // exactly as before; a screen that writes its own takes over again next frame
            if (h.mine) Destroy(h.group);
        }
        hidden.Clear();
    }

    // ------------------------------------------------------------------ its own screen

    void MakeScreen()
    {
        canvas = UiSkin.ScreenCanvas("Day intro screen (while playing)", transform, SortingOrder);
        var root = (RectTransform)canvas.transform;
        if (mode == Mode.Full)
        {
            black = Stretch("Black", root).gameObject.AddComponent<Image>();
            black.color = new Color(.02f, .018f, .016f, 1f);
            black.raycastTarget = false;
            barHeight = Mathf.Round(1080f * BarShare);
            barTop = Bar("Bar (top)", root, true);
            barBottom = Bar("Bar (bottom)", root, false);
            // PLACEHOLDER COPY. Small, in the bottom bar, right; it fades in after a moment so the first frame is the picture.
            skipHint = UiSkin.Text("Skip", barBottom.rectTransform, 18f, UiSkin.BandFaint, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            RectTransform hint = skipHint.rectTransform;
            hint.anchorMin = new Vector2(1f, 0f);
            hint.anchorMax = new Vector2(1f, 1f);
            hint.pivot = new Vector2(1f, .5f);
            hint.anchoredPosition = new Vector2(-Mathf.Round(1920f * SafeMargin), 0f);
            hint.sizeDelta = new Vector2(600f, 0f);
            skipHint.text = PadInput.UsingPad ? "Skip   any button" : "Skip   any key";
            skipHint.alpha = 0f;
        }
        BuildTitle(root);
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    Image Bar(string name, RectTransform parent, bool top)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(.5f, top ? 1f : 0f);
        rect.sizeDelta = new Vector2(0f, barHeight);
        rect.anchoredPosition = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.02f, .018f, .016f, 1f);
        image.raycastTarget = false;
        return image;
    }

    // The title card: the weekday and the day, a short brand-green rule, the day's one line, on the skin's band, centred a
    // little above the middle of the screen (clear of the prompt at the bottom and the HUD's corners). The type and layout
    // are UI/UX's to restyle; the timing is here.
    const float DaySize = 60f, LineSize = 28f, PadX = 36f, PadY = 22f, Gap = 12f, RuleWidth = 120f, RuleHeight = 2f, LineMaxWidth = 1300f;

    void BuildTitle(RectTransform root)
    {
        titleRoot = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        titleRoot.SetParent(root, false);
        titleRoot.anchorMin = titleRoot.anchorMax = new Vector2(.5f, TitleUp);
        titleRoot.pivot = new Vector2(.5f, .5f);
        titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.interactable = false;
        titleGroup.blocksRaycasts = false;
        UiSkin.Paint(titleRoot, UiSkin.Band, UiSkin.Radius);

        dayText = UiSkin.Text("Day", titleRoot, DaySize, UiSkin.Paper, TextAlignmentOptions.Center, FontStyles.Bold);
        dayText.characterSpacing = 2f;
        dayText.text = Weekdays.Label(day);
        lineText = UiSkin.Text("Line", titleRoot, LineSize, UiSkin.BandText, TextAlignmentOptions.Center, FontStyles.Normal, true);
        lineText.text = LineForDay(day);
        var rule = new GameObject("Rule", typeof(RectTransform)).GetComponent<RectTransform>();
        rule.SetParent(titleRoot, false);
        var ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.color = UiSkin.BrandBright;
        ruleImage.raycastTarget = false;

        // Laid out from the texts' own sizes, so a long line or a wide font never leaves the plate.
        Vector2 daySize = dayText.GetPreferredValues(dayText.text);
        Vector2 lineSize = lineText.GetPreferredValues(lineText.text, LineMaxWidth, 0f);
        float lineWidth = Mathf.Min(LineMaxWidth, Mathf.Ceil(lineSize.x) + 4f);
        float width = Mathf.Ceil(Mathf.Max(daySize.x, lineWidth) + 2f * PadX);
        float height = Mathf.Ceil(PadY + daySize.y + Gap + RuleHeight + Gap + lineSize.y + PadY);
        titleRoot.sizeDelta = new Vector2(width, height);
        float y = -PadY;
        Lay(dayText.rectTransform, y, new Vector2(width - 2f * PadX, daySize.y));
        y -= daySize.y + Gap;
        Lay(rule, y, new Vector2(RuleWidth, RuleHeight));
        y -= RuleHeight + Gap;
        Lay(lineText.rectTransform, y, new Vector2(lineWidth, lineSize.y));
        LastTitleSize = new Vector2(width, height);
        titleRoot.anchoredPosition = new Vector2(0f, -24f);   // it slides up as it comes in
    }

    static void Lay(RectTransform rect, float top, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, top);
        rect.sizeDelta = size;
    }

    void ShowTitle()
    {
        if (titleShown) return;
        titleShown = true;
        titleShownAt = elapsed;
        titleFadeAt = elapsed + TitleHold;
    }

    void UpdateScreen(float dt)
    {
        if (canvas == null) return;
        if (mode == Mode.Full)
        {
            // The fade from black over the first shot (the night's own curtain does it on a morning after a night); the bars in
            // from the first frame and out on the pull-out.
            float shotsTime = phase == Phase.Curtain ? 0f : elapsed - curtainWaited;
            if (black != null)
            {
                Color c = black.color;
                c.a = underTheCurtain ? 0f : 1f - Mathf.Clamp01(shotsTime / FadeIn);
                if (pullOutStarted >= 0f) c.a = 0f;
                black.color = c;
            }
            float bars = pullOutStarted < 0f ? 1f : 1f - Mathf.Clamp01((elapsed - pullOutStarted) / BarsOut);
            bars = Mathf.SmoothStep(0f, 1f, bars);
            if (barTop != null) barTop.rectTransform.anchoredPosition = new Vector2(0f, (1f - bars) * barHeight);
            if (barBottom != null) barBottom.rectTransform.anchoredPosition = new Vector2(0f, -(1f - bars) * barHeight);
            if (skipHint != null)
                skipHint.alpha = pullOutStarted >= 0f ? Mathf.MoveTowards(skipHint.alpha, 0f, dt * 8f)
                    : Mathf.Clamp01((shotsTime - .8f) / .4f);
            // The other screens come back together on the pull-out (the fade itself is applied in HideScreens, each LateUpdate).
            if (hudReturn >= 0f && hudReturn < 1f)
            {
                hudReturn = Mathf.Min(1f, hudReturn + dt / HudReturnSeconds);
                if (hudReturn >= 1f) RestoreScreens();
            }
        }

        // The title: in over TitleIn (sliding up a little), held, out over TitleOut; a skip after its minimum shortens the hold.
        if (titleShown && titleGroup != null)
        {
            float age = elapsed - titleShownAt;
            float inK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / TitleIn));
            float outK = titleFadeAt >= 0f && elapsed >= titleFadeAt ? Mathf.Clamp01((elapsed - titleFadeAt) / TitleOut) : 0f;
            if (outK > 0f) titleFading = true;
            titleGroup.alpha = inK * (1f - outK);
            titleRoot.anchoredPosition = new Vector2(0f, -24f * (1f - inK));
            if (outK >= 1f && !titleDone)
            {
                titleDone = true;
                titleRoot.gameObject.SetActive(false);
            }
        }
        else if (!titleShown && phase == Phase.Title) titleDone = true;
    }

    // ------------------------------------------------------------------ the day's one line (PLACEHOLDER COPY: his sister rewrites these)

    /// <summary>
    /// One short line about the day, under its name. Day 1's is the hook of the whole game (what the café is); later days say
    /// what today brings without spoiling it; past the written days, the weekday. Never over 90 characters.
    /// </summary>
    public static string LineForDay(int day)
    {
        switch (day)
        {
            case 1: return "Fix what they bring. Pour what they order. Make the rent.";   // Learn the Shop
            case 2: return "Grace comes in early. The coffee won't wait for the bench.";   // Juggle
            case 3: return "Full house today. Some of them are in a hurry.";              // The Real Game
            case 4: return "Everyone at once. Breathe when you can.";                     // Pressure
            case 5: return "Two rushes, a lull between. Use the quiet.";                  // Two Rushes
        }
        switch (Weekdays.Of(day))
        {
            case Weekdays.Saturday: return "The weekend crowd. Coffee first, questions later.";
            case Weekdays.Sunday: return "A slow start. It won't stay slow.";
            case Weekdays.Monday: return "A new week at the bench. Same mortgage.";
            case Weekdays.Friday: return "Friday. Everyone wants it done before the weekend.";
            default: return "Doors open. Let's see who turns up.";
        }
    }

    // ------------------------------------------------------------------ the pool and the pick

    /// <summary>Every shot the pool knows, in beat order (checks and the lab's explicit orders).</summary>
    public static readonly string[] PoolIds =
    {
        "grinder.beans", "grinder.burr", "bench.screw", "bench.lamp", "drinks.pour", "bench.wake", "house.grace", "street.front", "street.sign",
    };

    List<Shot> AllShots()
    {
        return new List<Shot>
        {
            new GrinderBeans(), new GrinderBurr(), new BenchScrew(), new BenchLamp(), new DrinksPour(), new BenchWake(), new GraceHouseShot(),
            new StreetFront(), new StreetSign(),
        };
    }

    void PickShots(string[] explicitOrder)
    {
        var sceneThings = SceneThings.Find();
        List<Shot> all = AllShots();
        var available = new List<Shot>();
        foreach (Shot s in all)
        {
            bool ok;
            try { ok = s.Prepare(sceneThings); }
            catch (Exception e) { Debug.LogWarning($"[Day intro] {s.Id} can't be framed this morning: {e.Message}"); ok = false; }
            if (ok) available.Add(s);
        }
        shots.Clear();
        if (explicitOrder != null)
        {
            foreach (string id in explicitOrder)
                foreach (Shot s in available)
                    if (s.Id == id && !shots.Contains(s)) { shots.Add(s); break; }
        }
        else
        {
            string last = PlayerPrefs.GetString(LastOrderKey, "");
            var rng = new System.Random(unchecked(Environment.TickCount * 7919 + day * 131));   // its own dice: never the café's
            string tied = TiedShotFor(day);
            string picked = "";
            for (int attempt = 0; attempt < 8; attempt++)
            {
                shots.Clear();
                for (int beat = 0; beat < 4; beat++)
                {
                    var candidates = new List<Shot>();
                    foreach (Shot s in available) if (s.Beat == beat) candidates.Add(s);
                    if (candidates.Count == 0) continue;
                    Shot choice = null;
                    // Day 1 plays the canonical order (its title is the hook); a morning Grace is coming early shows her house.
                    if (day == 1) choice = candidates.Find(s => s.Canonical);
                    if (beat == 2 && tied != null) choice = candidates.Find(s => s.Id == tied) ?? choice;
                    if (choice == null) choice = candidates[rng.Next(candidates.Count)];
                    shots.Add(choice);
                }
                picked = OrderOf(shots);
                if (picked != last || shots.Count < 2 || day == 1) break;
            }
            if (picked == last && shots.Count >= 2 && day != 1)
            {
                // Eight draws the same as last morning (two options a beat makes that rare): flip the first beat with a choice.
                for (int i = 0; i < shots.Count; i++)
                {
                    Shot other = available.Find(s => s.Beat == shots[i].Beat && s != shots[i]);
                    if (other != null) { shots[i] = other; break; }
                }
            }
        }
        order = OrderOf(shots);
        if (shots.Count > 0)
        {
            PlayerPrefs.SetString(LastOrderKey, order);
            Debug.Log($"[Day intro] Day {day}, {mode}: {order}");
        }
    }

    static string OrderOf(List<Shot> list)
    {
        var ids = new List<string>(list.Count);
        foreach (Shot s in list) ids.Add(s.Id);
        return string.Join("|", ids);
    }

    /// <summary>
    /// The shot tied to this day, or null: Grace's house on a morning she comes in early (her featured visit in the first
    /// quarter of the day, Day 2's; or she is this morning's visitor with news of last night).
    /// </summary>
    public static string TiedShotFor(int day)
    {
        if (day <= 1) return null;
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null && spawner.Today != null && spawner.Today.dayNumber == day && GraceEarly(spawner.Today)) return "house.grace";
        if (spawner != null && spawner.MorningVisitor != null && spawner.MorningVisitor.PersistentId == "grace") return "house.grace";
        // Another day's definition (a lab asking about tomorrow): the schedule's assets are loaded with the spawner.
        try
        {
            foreach (DayDefinition definition in Resources.FindObjectsOfTypeAll<DayDefinition>())
                if (definition != null && definition.dayNumber == day && GraceEarly(definition)) return "house.grace";
        }
        catch (Exception) { }
        return null;
    }

    static bool GraceEarly(DayDefinition definition) =>
        definition != null && definition.featuredRegular != null && definition.featuredRegular.PersistentId == "grace"
        && definition.featuredRegularArrivesAt <= .25f;

    /// <summary>The title block if the canvas were <paramref name="canvasSize"/> reference pixels across and up (its scaler keeps the area of 1920 x 1080).</summary>
    public static Rect TitleBoxIfTheScreenWere(Vector2 canvasSize)
    {
        Vector2 size = LastTitleSize;
        return new Rect(canvasSize.x * .5f - size.x * .5f, canvasSize.y * TitleUp - size.y * .5f, size.x, size.y);
    }

    // ------------------------------------------------------------------ what there is to frame

    /// <summary>The scene's things the shots frame, found once a morning by name (the kit's pieces) or by their components.</summary>
    public sealed class SceneThings
    {
        public Transform grinder, lamp, fascia, blade;
        public Light lampLight;
        public BenchStage stage;
        public CinemachineCamera inspectCam;
        public BeverageSlot pourSlot;
        public HomeDoor graceDoor;
        public GameObject deviceWithScrews;   // a device prefab asset (the phone) the bench's shots photocopy
        public Transform toolScrewdriver;

        public static SceneThings Find()
        {
            var t = new SceneThings();
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (tr == null) continue;
                string n = tr.name;
                if (t.grinder == null && n.StartsWith("CK_Grinder", StringComparison.Ordinal)) t.grinder = tr;
                else if (t.lamp == null && n.StartsWith("CK_BenchLamp", StringComparison.Ordinal)) t.lamp = tr;
                else if (t.fascia == null && n.StartsWith("CK_Fascia", StringComparison.Ordinal)) t.fascia = tr;
                else if (t.blade == null && n.StartsWith("CK_BladeSign", StringComparison.Ordinal)) t.blade = tr;
            }
            if (t.lamp != null) t.lampLight = t.lamp.GetComponentInChildren<Light>(true);
            t.stage = BenchStage.Instance;
            if (t.stage != null)
            {
                t.inspectCam = t.stage.GetComponentInChildren<CinemachineCamera>(true);
                Transform slot = t.stage.ToolSlot(ToolType.Screwdriver);
                if (slot != null && slot != t.stage.Caddy)
                {
                    ToolPickup pickup = slot.GetComponentInChildren<ToolPickup>(true);
                    if (pickup != null && pickup.tool == ToolType.Screwdriver) t.toolScrewdriver = pickup.transform;
                }
            }
            foreach (BeverageSlot slot in UnityEngine.Object.FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude))
            {
                if (slot == null || slot.cupPoint == null || slot.stream == null) continue;
                bool latte = slot.drink != null && !string.IsNullOrEmpty(slot.drink.drinkName) && slot.drink.drinkName.ToLowerInvariant().Contains("latte");
                if (t.pourSlot == null || latte) t.pourSlot = slot;
                if (latte) break;
            }
            t.graceDoor = HomeDoor.Find("home.grace");
            t.deviceWithScrews = FindDeviceWithScrews();
            return t;
        }

        // A device prefab with screws (the phone first), as an asset: never a live job's device. The spawner's references keep
        // the prefabs loaded, so they are found among the loaded objects.
        static GameObject FindDeviceWithScrews()
        {
            GameObject phone = null, any = null;
            try
            {
                foreach (Screw screw in Resources.FindObjectsOfTypeAll<Screw>())
                {
                    if (screw == null || screw.gameObject.scene.IsValid()) continue;
                    GameObject root = screw.transform.root.gameObject;
                    if (root.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                    if (root.name.IndexOf("phone", StringComparison.OrdinalIgnoreCase) >= 0) phone = root;
                    else if (any == null) any = root;
                }
            }
            catch (Exception) { }
            return phone != null ? phone : any;
        }

        /// <summary>The way from the bench's mat toward the player's close-up (the camera's side of the bench), flat.</summary>
        public Vector3 BenchToward
        {
            get
            {
                Vector3 toward = inspectCam != null && stage != null && stage.Mat != null
                    ? inspectCam.transform.position - stage.Mat.position
                    : stage != null ? -stage.transform.forward : Vector3.back;
                toward.y = 0f;
                return toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.back;
            }
        }
    }

    // ------------------------------------------------------------------ props: photocopies of the game's own meshes

    /// <summary>
    /// A still copy of what's drawn of <paramref name="source"/> (a prefab asset or a scene object): its solid parts only (a
    /// mesh each; not its job tag, not sprites or text), as children of a new object under the intro, keeping each part's name
    /// and pose. <paramref name="include"/> decides a part by its transform and whether it is on by default (null: on by default).
    /// </summary>
    public GameObject Photocopy(GameObject source, string name, Func<Transform, bool, bool> include = null)
    {
        var copy = new GameObject(name);
        copy.transform.SetParent(propsRoot != null ? propsRoot : transform, false);
        Transform root = source.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (MeshRenderer mr in source.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null) continue;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (mr.GetComponentInParent<JobMarker>(true) != null) continue;
            if (mr.GetComponent<TMP_Text>() != null) continue;
            bool onByDefault = ActiveUpTo(mr.transform, root);
            bool wanted = include != null ? include(mr.transform, onByDefault) : onByDefault;
            if (!wanted) continue;
            var part = new GameObject(mr.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(copy.transform, false);
            Matrix4x4 m = toRoot * mr.transform.localToWorldMatrix;
            part.transform.localPosition = m.GetColumn(3);
            part.transform.localRotation = m.rotation;
            part.transform.localScale = m.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = part.GetComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = mr.shadowCastingMode;
            r.receiveShadows = mr.receiveShadows;
        }
        return copy;
    }

    static bool ActiveUpTo(Transform t, Transform root)
    {
        while (t != null)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == root) return true;
            t = t.parent;
        }
        return true;
    }

    /// <summary>The world box round a copy's parts (false when it has none).</summary>
    public static bool BoundsOf(GameObject copy, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    public Transform Props => propsRoot != null ? propsRoot : transform;

    // ------------------------------------------------------------------ the shots

    /// <summary>
    /// One shot: what it needs (Prepare finds it, false drops the shot), its props (Begin / End), its camera and motion each
    /// frame (Frame, on the shot's own clock), its sounds at their offsets (Cue). Beat 0 the café, 1 the bench, 2 the third,
    /// 3 the open. Moments are where a photo shows it best (0-1 of its length).
    /// </summary>
    public abstract class Shot
    {
        public abstract string Id { get; }
        public abstract int Beat { get; }
        public abstract float Length { get; }
        public virtual bool Canonical => false;
        public virtual IReadOnlyList<float> Moments => new[] { .5f };

        readonly List<(float at, string cue, bool fired)> cues = new List<(float, string, bool)>();

        public abstract bool Prepare(SceneThings things);
        public virtual void Begin(DayIntro intro) { }
        public abstract void Frame(DayIntro intro, float t, float dt);
        public virtual void End(DayIntro intro) { }

        protected void Cue(float at, string cue) => cues.Add((at, cue, false));

        public void PlayCues(DayIntro intro, float t)
        {
            for (int i = 0; i < cues.Count; i++)
            {
                var c = cues[i];
                if (c.fired || t < c.at) continue;
                cues[i] = (c.at, c.cue, true);
                if (c.cue == "day.open") intro.RingTheBell();
                else Sfx.Play2D(c.cue);
            }
        }

        /// <summary>A point on a circle round <paramref name="axisPoint"/>'s vertical axis: <paramref name="from"/> turned by <paramref name="degrees"/>.</summary>
        protected static Vector3 Orbit(Vector3 axisPoint, Vector3 from, float degrees)
        {
            Vector3 flat = from - axisPoint;
            float y = flat.y;
            flat.y = 0f;
            return axisPoint + Quaternion.Euler(0f, degrees, 0f) * flat + Vector3.up * y;
        }

        protected static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>
        /// Lays a prop (already turned the right way up) flat in the middle of the mat: its long side across the view (along the
        /// mat's own long side), its lowest point on the mat's top. <paramref name="toward"/> is the way from the mat to the viewer.
        /// </summary>
        protected static void LayOnTheMat(GameObject prop, Vector3 matCentre, float matTop, Vector3 toward)
        {
            prop.transform.position = new Vector3(matCentre.x, matTop + .05f, matCentre.z);
            if (BoundsOf(prop, out Bounds b))
            {
                // Its extent toward the viewer against its extent across (the mat is 60 cm across, 36 cm deep).
                float deep = Mathf.Abs(toward.x) * b.size.x + Mathf.Abs(toward.z) * b.size.z;
                float across = Mathf.Abs(toward.z) * b.size.x + Mathf.Abs(toward.x) * b.size.z;
                if (deep > across + .01f) prop.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * prop.transform.rotation;
            }
            if (BoundsOf(prop, out b))
                prop.transform.position += new Vector3(matCentre.x - b.center.x, matTop - b.min.y, matCentre.z - b.center.z);
        }
    }

    // ---------- beat 0: the café ----------

    /// <summary>Beans in the grinder's hopper, through the glass: an extreme close-up turning round it (the burrs' spin) and pushing in, the whirr.</summary>
    sealed class GrinderBeans : Shot
    {
        public override string Id => "grinder.beans";
        public override int Beat => 0;
        public override float Length => 1.5f;
        public override bool Canonical => true;
        public override IReadOnlyList<float> Moments => new[] { .55f };
        Transform grinder;

        public override bool Prepare(SceneThings things)
        {
            grinder = things.grinder;
            if (grinder == null) return false;
            Cue(.05f, "intro.grind");
            return true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Length);
            // The hopper sits 6 cm behind the base's middle; the beans' mound is about 50 cm up (cafe_props.py: hz0 + 0.110).
            Vector3 beans = grinder.TransformPoint(new Vector3(0f, .505f, -.06f));
            Vector3 from = grinder.TransformPoint(new Vector3(.10f, .70f, .12f));
            Vector3 at = Orbit(beans, from, Mathf.Lerp(-14f, 14f, k));
            at = beans + (at - beans) * Mathf.Lerp(1f, .86f, k);
            intro.Place(at, beans, 32f, 0f, .01f);
        }
    }

    /// <summary>The burr housing's knurled collar and the chute under it, close, turning round it: the grinder as a machine.</summary>
    sealed class GrinderBurr : Shot
    {
        public override string Id => "grinder.burr";
        public override int Beat => 0;
        public override float Length => 1.5f;
        public override IReadOnlyList<float> Moments => new[] { .5f };
        Transform grinder;

        public override bool Prepare(SceneThings things)
        {
            grinder = things.grinder;
            if (grinder == null) return false;
            Cue(.05f, "intro.grind");
            return true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Length);
            // The collar is 35-38 cm up on the body's axis (6 cm back); the chute's mouth leans out in front and below it.
            Vector3 collar = grinder.TransformPoint(new Vector3(0f, .345f, -.03f));
            Vector3 from = grinder.TransformPoint(new Vector3(-.17f, .43f, .23f));
            Vector3 at = Orbit(collar, from, Mathf.Lerp(-12f, 12f, k));
            at = collar + (at - collar) * Mathf.Lerp(1f, .88f, k);
            intro.Place(at, collar, 30f, 0f, .01f);
        }
    }

    // ---------- beat 1: the bench ----------

    /// <summary>
    /// A phone lies back-up in the middle of the mat; the hero screwdriver backs a screw out of it, three ticks and a turn a
    /// tick, the head spinning where the hopper's round was; it hops free and the view whips to the tray as it lands: tink.
    /// A photocopy of the phone prefab and of the caddy's screwdriver; the bench itself is untouched.
    /// </summary>
    sealed class BenchScrew : Shot
    {
        public override string Id => "bench.screw";
        public override int Beat => 1;
        public override float Length => 2f;
        public override bool Canonical => true;
        public override IReadOnlyList<float> Moments => new[] { .4f, .82f };

        const float TurnSeconds = 1.05f, Turns = 3f, Thread = .006f, HopSeconds = .45f;
        SceneThings things;
        GameObject phone, driver;
        Transform screw;
        Vector3 screwHome, screwOut, trayLanding;
        Quaternion screwRest, driverRest;
        bool freed;

        public override bool Prepare(SceneThings t)
        {
            things = t;
            if (t.stage == null || t.stage.Mat == null || t.stage.Tray == null || t.deviceWithScrews == null) return false;
            if (t.deviceWithScrews.GetComponentInChildren<Screw>(true) == null) return false;
            Cue(.17f, "screw.turn");
            Cue(.52f, "screw.turn");
            Cue(.87f, "screw.turn");
            Cue(TurnSeconds, "screw.free");
            Cue(TurnSeconds + HopSeconds, "screw.drop");
            return true;
        }

        public override void Begin(DayIntro intro)
        {
            Transform mat = things.stage.Mat;
            Renderer matRenderer = mat.GetComponent<Renderer>();
            float matTop = matRenderer != null ? matRenderer.bounds.max.y : mat.position.y;
            Vector3 toward = things.BenchToward;

            // The phone, back up: the screw's own axis (out of the body) turned to point up, its length along the mat's.
            Transform sourceScrew = things.deviceWithScrews.GetComponentInChildren<Screw>(true).transform;
            phone = intro.Photocopy(things.deviceWithScrews, "The phone (back up)");
            Vector3 outLocal = OutAxisInRoot(things.deviceWithScrews, sourceScrew);
            phone.transform.rotation = Quaternion.FromToRotation(outLocal, Vector3.up);
            LayOnTheMat(phone, mat.position, matTop, toward);
            // The screw nearest the viewer backs out (one of two on the phone's back).
            foreach (Transform part in phone.transform)
            {
                if (!part.name.StartsWith("Screw", StringComparison.Ordinal)) continue;
                if (screw == null || Vector3.Dot(part.position - mat.position, toward) > Vector3.Dot(screw.position - mat.position, toward)) screw = part;
            }
            if (screw == null) throw new InvalidOperationException("the phone's copy has no screw");
            screwHome = screw.position;
            screwRest = screw.rotation;
            screwOut = Vector3.up;

            // The screwdriver: a copy of the caddy's, its blade on the screw, the handle leaning a little away from the view
            // (the view is low and close: a handle leaning toward it would fill the frame).
            if (things.toolScrewdriver != null)
            {
                driver = intro.Photocopy(things.toolScrewdriver.gameObject, "The screwdriver");
                driverRest = Quaternion.LookRotation(-toward, Vector3.up) * Quaternion.Euler(10f, 0f, 0f);
                driver.transform.SetPositionAndRotation(screwHome + screwOut * .001f, driverRest);
            }
            Transform tray = things.stage.Tray;
            trayLanding = tray.position + tray.up * .004f + tray.right * .015f;
        }

        // Which way is out of the device for a screw: its own up (or down), whichever points away from the device's middle.
        internal static Vector3 OutAxisInRoot(GameObject device, Transform screw)
        {
            Transform root = device.transform;
            Vector3 centre = Vector3.zero;
            int n = 0;
            foreach (MeshRenderer mr in device.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponentInParent<JobMarker>(true) != null || mr.transform == screw) continue;
                centre += root.InverseTransformPoint(mr.transform.position);
                n++;
            }
            if (n > 0) centre /= n;
            Vector3 up = root.InverseTransformDirection(screw.up).normalized;
            Vector3 screwLocal = root.InverseTransformPoint(screw.position);
            return Vector3.Dot(screwLocal - centre, up) < 0f ? -up : up;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            Vector3 toward = things.BenchToward;
            Vector3 side = Vector3.Cross(toward, Vector3.up);
            // 0 .. TurnSeconds: three turns out; the head turns where the hopper turned, the camera carrying the spin on.
            float progress = Mathf.Clamp01(t / TurnSeconds);
            if (screw != null)
            {
                if (!freed)
                {
                    screw.position = screwHome + screwOut * (Thread * progress);
                    screw.rotation = Quaternion.AngleAxis(Turns * 360f * progress, screwOut) * screwRest;
                    // The driver turns with it, about its own length (its blade is in the slot).
                    if (driver != null) driver.transform.SetPositionAndRotation(screw.position + screwOut * .001f, driverRest * Quaternion.AngleAxis(Turns * 360f * progress, Vector3.up));
                }
                if (t >= TurnSeconds)
                {
                    if (!freed) { freed = true; if (driver != null) driver.SetActive(false); }
                    // The hop: up, over and into the tray, in an arc, tumbling; then it lies there.
                    float h = Mathf.Clamp01((t - TurnSeconds) / HopSeconds);
                    Vector3 start = screwHome + screwOut * Thread;
                    Vector3 p = Vector3.Lerp(start, trayLanding, h);
                    p.y += Mathf.Sin(h * Mathf.PI) * .06f;
                    screw.position = p;
                    screw.rotation = Quaternion.AngleAxis(h * 540f, side) * Quaternion.AngleAxis(Turns * 360f, screwOut) * screwRest;
                    if (h >= 1f) screw.rotation = Quaternion.AngleAxis(90f, side) * screwRest;   // on its side in the tray
                }
            }
            // The camera: close on the head, turning round it; from the pop, a whip to the tray, landing with the screw.
            Vector3 headAt = screwHome + screwOut * .002f;
            Vector3 closeFrom = headAt + side * .028f + Vector3.up * .045f + toward * .038f;
            Vector3 close = Orbit(headAt, closeFrom, Mathf.Lerp(-8f, 8f, Ease(progress)));
            Vector3 trayAt = trayLanding;
            Vector3 trayFrom = trayAt + Vector3.up * .15f + toward * .13f;
            float whip = Ease((t - TurnSeconds) / .35f);
            float settle = Mathf.Clamp01((t - TurnSeconds - .35f) / (Length - TurnSeconds - .35f));
            Vector3 at = Vector3.Lerp(close, trayFrom - toward * (.015f * settle), whip);
            Vector3 look = Vector3.Lerp(headAt, trayAt, whip);
            intro.Place(at, look, Mathf.Lerp(28f, 32f, whip), 0f, .005f);
        }

        public override void End(DayIntro intro)
        {
            if (phone != null) UnityEngine.Object.Destroy(phone);
            if (driver != null) UnityEngine.Object.Destroy(driver);
            phone = driver = null;
            screw = null;
        }
    }

    /// <summary>The magnifier lamp over the bench comes on (a blink, then steady) above the caddy of tools: the work's light.</summary>
    sealed class BenchLamp : Shot
    {
        public override string Id => "bench.lamp";
        public override int Beat => 1;
        public override float Length => 1.4f;
        public override IReadOnlyList<float> Moments => new[] { .6f };
        SceneThings things;
        float intensityBefore;
        bool lightHeld;

        public override bool Prepare(SceneThings t)
        {
            things = t;
            if (t.lamp == null || t.lampLight == null || t.stage == null || t.stage.Mat == null) return false;
            Cue(.35f, "intro.lamp");
            return true;
        }

        public override void Begin(DayIntro intro)
        {
            intensityBefore = things.lampLight.intensity;
            things.lampLight.intensity = 0f;
            lightHeld = true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            if (lightHeld)
            {
                // Off; a first flicker at 0.35; off again; steady from 0.48 (an LED ring's blink, not a bulb's warm-up).
                float level = t < .35f ? 0f : t < .41f ? .55f : t < .46f ? .05f : Mathf.Lerp(.6f, 1f, Mathf.Clamp01((t - .46f) / .12f));
                things.lampLight.intensity = intensityBefore * level;
            }
            Transform mat = things.stage.Mat;
            Vector3 toward = things.BenchToward;
            Vector3 side = Vector3.Cross(toward, Vector3.up);
            Vector3 head = things.lampLight.transform.position;   // the lens head holds the light, over the mat's middle
            Vector3 caddy = things.stage.Caddy != null ? things.stage.Caddy.position : mat.position - side * .25f;
            float k = Ease(t / Length);
            // Low over the bench on the viewer's side, between the tools and the lens head, rising a little as the light comes.
            Vector3 at = mat.position + toward * .30f - side * .34f + Vector3.up * Mathf.Lerp(.11f, .14f, k);
            Vector3 look = Vector3.Lerp(caddy + Vector3.up * .12f, head - Vector3.up * .03f, .45f);
            intro.Place(at, look, 40f, 0f, .02f);
        }

        public override void End(DayIntro intro)
        {
            if (lightHeld && things.lampLight != null) things.lampLight.intensity = intensityBefore;
            lightHeld = false;
        }
    }

    // ---------- beat 2: the third ----------

    /// <summary>
    /// A latte pouring: a paper cup (the game's own cup mesh) under the dispenser's spout, the pour stream (a copy of the
    /// slot's) and the liquid rising, seen close from above the rim, turning round it (the swirl), with the dispenser's own
    /// pour sound. Nothing is poured for real: no cup, no stock.
    /// </summary>
    sealed class DrinksPour : Shot
    {
        public override string Id => "drinks.pour";
        public override int Beat => 2;
        public override float Length => 1.4f;
        public override bool Canonical => true;
        public override IReadOnlyList<float> Moments => new[] { .6f };
        SceneThings things;
        GameObject cup;
        Transform liquid;
        LineRenderer stream;
        Material shellMaterial, liquidMaterial;
        AudioSource pour;
        float pourVolume;
        bool ownSound;

        public override bool Prepare(SceneThings t)
        {
            things = t;
            if (t.pourSlot == null || t.pourSlot.cupPoint == null || t.pourSlot.stream == null) return false;
            return true;
        }

        public override void Begin(DayIntro intro)
        {
            BeverageSlot slot = things.pourSlot;
            Color colour = DrinkLiquidVisual.LiquidColor(slot.drink);
            // The cup, as DrinkLiquidVisual draws the game's: the open paper shell and the rising disc of drink.
            cup = new GameObject("The cup");
            cup.transform.SetParent(intro.Props, false);
            cup.transform.SetPositionAndRotation(slot.cupPoint.position, Quaternion.identity);
            shellMaterial = DrinkLiquidVisual.MakeMaterial("Day intro cup (while playing)", DrinkLiquidVisual.PaperColour, .08f);
            liquidMaterial = DrinkLiquidVisual.MakeMaterial("Day intro drink (while playing)", colour, .68f);
            MeshObject("Shell", cup.transform, DrinkLiquidVisual.BuildShell(), shellMaterial);
            liquid = MeshObject("Drink", cup.transform, DrinkLiquidVisual.BuildDisc(), liquidMaterial).transform;
            SetFill(0f);
            // The pour: a copy of the slot's own stream, drawn the way BeverageSlot draws it.
            stream = UnityEngine.Object.Instantiate(slot.stream.gameObject, slot.stream.transform.parent).GetComponent<LineRenderer>();
            stream.name = "The pour (day intro)";
            stream.transform.SetPositionAndRotation(slot.stream.transform.position, slot.stream.transform.rotation);
            stream.enabled = true;
            stream.positionCount = 5;
            stream.startColor = stream.endColor = Color.Lerp(colour, Color.white, .3f);
            // Its sound: the dispenser's own pour file (the bank's drink.pour when Audio fills it), faded in and out; else the placeholder.
            BeveragePourAudio audio = slot.GetComponent<BeveragePourAudio>();
            pour = cup.AddComponent<AudioSource>();
            pour.playOnAwake = false;
            pour.spatialBlend = 0f;
            pour.loop = true;
            pour.volume = 0f;
            pourVolume = audio != null ? audio.pourVolume : .18f;
            AudioClip clip = Sfx.Choose("drink.pour", pour, audio != null ? audio.pourClip : null, ref pourVolume);
            if (clip != null)
            {
                pour.clip = clip;
                pour.spatialBlend = 0f;
                pour.Play();
                ownSound = true;
            }
            else Sfx.Play2D("intro.pour");
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // As DrinkLiquidVisual.SetFill lays the surface: 1.2 cm up empty, 9.8 cm full, a touch wider as it rises.
        void SetFill(float amount)
        {
            if (liquid == null) return;
            float height = Mathf.Lerp(.012f, .098f, amount);
            float radius = Mathf.Lerp(.028f, .036f, amount);
            liquid.localPosition = new Vector3(0f, height, 0f);
            liquid.localScale = new Vector3(radius, 1f, radius);
            liquid.gameObject.SetActive(amount > .002f);
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = t / Length;
            float fill = Mathf.Lerp(.22f, .86f, Mathf.Clamp01(k));
            SetFill(fill);
            Vector3 cupBase = cup != null ? cup.transform.position : things.pourSlot.cupPoint.position;
            Vector3 surface = cupBase + Vector3.up * Mathf.Lerp(.012f, .098f, fill);
            if (stream != null)
            {
                Vector3 start = stream.transform.position;
                for (int i = 0; i < 5; i++)
                {
                    float s = i / 4f;
                    Vector3 point = Vector3.Lerp(start, surface, s);
                    if (i > 0 && i < 4) point += stream.transform.right * (Mathf.Sin(Time.unscaledTime * 23f + i * 1.8f) * .0018f);
                    stream.SetPosition(i, stream.useWorldSpace ? point : stream.transform.InverseTransformPoint(point));
                }
                stream.startWidth = .0065f;
                stream.endWidth = .009f + Mathf.Sin(Time.unscaledTime * 29f) * .001f;
            }
            if (pour != null && ownSound)
                pour.volume = pourVolume * Mathf.Min(Mathf.Clamp01(t / .12f), Mathf.Clamp01((Length - t) / .18f)) * GameSettings.BusVolume(SoundBus.World);
            // Close over the rim from the room's side, turning round the cup as it fills (the swirl), pushing in a little.
            Vector3 toward = -things.pourSlot.transform.forward;   // the dispenser faces the room; its slots face with it
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-6f) toward = Vector3.back;
            toward.Normalize();
            Vector3 side = Vector3.Cross(toward, Vector3.up);
            Vector3 rim = cupBase + Vector3.up * .105f;
            Vector3 from = rim + toward * .16f + side * .09f + Vector3.up * .15f;
            Vector3 at = Orbit(rim, from, Mathf.Lerp(-10f, 10f, Ease(k)));
            at = rim + (at - rim) * Mathf.Lerp(1f, .9f, Ease(k));
            intro.Place(at, rim - Vector3.up * .01f, 34f, 0f, .01f);
        }

        public override void End(DayIntro intro)
        {
            if (pour != null) pour.Stop();
            if (stream != null) UnityEngine.Object.Destroy(stream.gameObject);
            if (cup != null) UnityEngine.Object.Destroy(cup);
            if (shellMaterial != null) UnityEngine.Object.Destroy(shellMaterial);
            if (liquidMaterial != null) UnityEngine.Object.Destroy(liquidMaterial);
            cup = null; stream = null; liquid = null; pour = null; shellMaterial = liquidMaterial = null;
        }
    }

    /// <summary>A fixed phone face up on the mat: its screen lights (as DeviceWake lights it), the boot chime. Yesterday's work, done.</summary>
    sealed class BenchWake : Shot
    {
        public override string Id => "bench.wake";
        public override int Beat => 2;
        public override float Length => 1.3f;
        public override IReadOnlyList<float> Moments => new[] { .75f };
        static readonly Color ScreenGlow = new Color(.55f, .68f, .95f);   // DeviceWake's
        // The phone's working screens, by the prefab's names, best first: the plain glass, the fresh one; the cracked one stays off.
        static readonly string[] WorkingScreens = { "Glass", "ScreenPlain", "Fresh", "Screen" };
        static readonly string[] BrokenScreens = { "Broken", "ScreenCracked" };
        SceneThings things;
        GameObject phone;
        Material lit;
        Color baseBefore;
        bool hasBase;
        string screenName;

        public override bool Prepare(SceneThings t)
        {
            things = t;
            if (t.stage == null || t.stage.Mat == null || t.deviceWithScrews == null) return false;
            screenName = null;
            foreach (string wanted in WorkingScreens)
            {
                foreach (MeshRenderer mr in t.deviceWithScrews.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.name == wanted && mr.GetComponent<MeshFilter>() != null) { screenName = wanted; break; }
                if (screenName != null) break;
            }
            if (screenName == null) return false;
            Cue(.3f, "phone.boot");
            return true;
        }

        static bool IsBrokenScreen(Transform t) => Array.IndexOf(BrokenScreens, t.name) >= 0;

        public override void Begin(DayIntro intro)
        {
            Transform mat = things.stage.Mat;
            Renderer matRenderer = mat.GetComponent<Renderer>();
            float matTop = matRenderer != null ? matRenderer.bounds.max.y : mat.position.y;
            phone = intro.Photocopy(things.deviceWithScrews, "The phone (fixed)", (t, onByDefault) =>
            {
                if (IsBrokenScreen(t)) return false;
                if (t.name == screenName) return true;
                if (Array.IndexOf(WorkingScreens, t.name) >= 0) return false;   // one working screen, the chosen one
                return onByDefault;
            });
            // Face up, as it came in; the screen's own axis is the prefab's up.
            phone.transform.rotation = Quaternion.identity;
            LayOnTheMat(phone, mat.position, matTop, things.BenchToward);
            foreach (Transform part in phone.transform)
            {
                if (part.name != screenName) continue;
                var r = part.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null) continue;
                lit = new Material(r.sharedMaterial) { name = "Day intro screen (while playing)" };
                r.sharedMaterial = lit;
                lit.EnableKeyword("_EMISSION");
                lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                hasBase = lit.HasProperty("_BaseColor");
                if (hasBase) baseBefore = lit.GetColor("_BaseColor");
                lit.SetColor("_EmissionColor", Color.black);
                break;
            }
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            if (lit != null)
            {
                // A beat, then the glass glows over 0.55 s, as DeviceWake does it.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .3f) / .55f));
                lit.SetColor("_EmissionColor", ScreenGlow * (k * 1.35f));
                if (hasBase) lit.SetColor("_BaseColor", Color.Lerp(baseBefore, Color.Lerp(baseBefore, ScreenGlow, .6f), k));
            }
            Transform mat = things.stage.Mat;
            Vector3 toward = things.BenchToward;
            Vector3 side = Vector3.Cross(toward, Vector3.up);
            Vector3 screen = phone != null && BoundsOf(phone, out Bounds b) ? new Vector3(b.center.x, b.max.y, b.center.z) : mat.position;
            float e = Ease(t / Length);
            Vector3 from = screen + toward * .20f + side * .06f + Vector3.up * .21f;
            Vector3 at = Orbit(screen, from, Mathf.Lerp(-5f, 5f, e));
            at = screen + (at - screen) * Mathf.Lerp(1f, .9f, e);
            intro.Place(at, screen, 32f, 0f, .01f);
        }

        public override void End(DayIntro intro)
        {
            if (phone != null) UnityEngine.Object.Destroy(phone);
            if (lit != null) UnityEngine.Object.Destroy(lit);
            phone = null;
            lit = null;
        }
    }

    /// <summary>Grace's house across the street, from her pavement, in the morning light: she's coming in today.</summary>
    sealed class GraceHouseShot : Shot
    {
        public override string Id => "house.grace";
        public override int Beat => 2;
        public override float Length => 1.4f;
        public override IReadOnlyList<float> Moments => new[] { .5f };
        Transform door;

        public override bool Prepare(SceneThings things)
        {
            door = things.graceDoor != null ? things.graceDoor.transform : null;
            return door != null;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Length);
            Vector3 outward = door.forward;      // her door faces the street
            Vector3 along = -door.right;         // along her front, toward the bay window
            Vector3 look = door.position + Vector3.up * 1.5f + along * 1.6f - outward * .2f;
            Vector3 at = door.position + outward * 2.4f + along * Mathf.Lerp(4.6f, 4f, k) + Vector3.up * 1.35f;
            intro.Place(at, look, 36f, 0f, .05f);
        }
    }

    // ---------- beat 3: the open ----------

    /// <summary>The storefront from its own pavement: the fascia with the café's name over the open door, the gilded windows, the A-frame in the foreground; the bell.</summary>
    sealed class StreetFront : Shot
    {
        public override string Id => "street.front";
        public override int Beat => 3;
        public override float Length => 1.3f;
        public override bool Canonical => true;
        public override IReadOnlyList<float> Moments => new[] { .45f };
        Transform fascia;

        public override bool Prepare(SceneThings things)
        {
            fascia = things.fascia;
            if (fascia == null) return false;
            Cue(0f, "intro.sign");
            Cue(DayOpenAfterSign, "day.open");
            return true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Length);
            Vector3 outward = fascia.forward;    // placed turned to the street: its front is outward
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-6f) outward = Vector3.back;
            outward.Normalize();
            Vector3 along = Vector3.Cross(outward, Vector3.up);   // along the facade, to the right of someone on the street facing it
            // From the pavement to the right of the door, 5 m out, eye height, walking a little toward the door; the A-frame in
            // the near corner of the picture, the gilded windows and the open door under the name.
            Vector3 look = fascia.position + Vector3.up * .1f - outward * .1f - along * .2f;
            Vector3 at = fascia.position + outward * Mathf.Lerp(4.8f, 4.2f, k) + along * Mathf.Lerp(3.1f, 2.5f, k);
            at.y = 1.55f - .05f * k;
            intro.Place(at, look, 30f, 0f, .05f);
        }
    }

    /// <summary>The round blade sign at the corner, close, with the front street waking beyond it: the circle the cup's rim became; the bell.</summary>
    sealed class StreetSign : Shot
    {
        public override string Id => "street.sign";
        public override int Beat => 3;
        public override float Length => 1.3f;
        public override IReadOnlyList<float> Moments => new[] { .5f };
        Transform blade;

        public override bool Prepare(SceneThings things)
        {
            blade = things.blade;
            if (blade == null) return false;
            Cue(0f, "intro.sign");
            Cue(DayOpenAfterSign, "day.open");
            return true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Length);
            // The disc stands 0.58 m out from the wall along the sign's -X and 0.40 up from its origin (cafe_signs.json); its faces
            // look along the street (its Z), so the view stands off along +Z, a little out from the wall and below.
            Vector3 disc = blade.TransformPoint(new Vector3(-.58f, .40f, 0f));
            Vector3 alongStreet = blade.forward;
            Vector3 outFromWall = -blade.right;
            Vector3 at = disc + alongStreet * Mathf.Lerp(2.9f, 2.6f, k) + outFromWall * 1.05f + Vector3.up * Mathf.Lerp(-.95f, -.85f, k);
            intro.Place(at, disc, 30f, 0f, .05f);
        }
    }
}
