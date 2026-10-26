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
// intro begins instead and rings the bell itself, where its sign beat lands.
//
// INTRO V2: NEIGHBOURHOOD B-ROLL (14 Oct 2026; the cinematics spec §4.1). His girlfriend: "WOWWWW i love the intro", but "why
// is the angle of the intro of the coffee maker or whatever so bad, and the screw and the placing on the tray, like it doesn't
// look like an intro level yet, but she loved the span on aces cafe sign"; "the views of the intro should change every day ...
// some intros should be different where it has a different b roll"; "why are we zooming on ONLY ON GRACES house ... this game
// isn't ace vs grace". The two close-ups she disliked filmed models never built to be seen that close (the grinder's beans are
// one spun solid; the tray was near empty on a plain slab); the one shot she loved was the only one at street distance. So a
// morning is four slots of B-roll, wide and mid shots unless a model was built for a close-up: the street waking (1.8 s) ->
// the café opening up (1.4) -> someone, or the day's own (1.4) -> the open, the sign and the bell (1.4). Day 1 is fixed (the
// street, the bench, the drinks, the café's name); other mornings draw one shot a slot, never yesterday's in slots 0-2, never
// any of the last six mornings' orders. Grace is in a morning only when she is really coming in early, at most one morning in
// six, never on Day 1 (Days spec §8: no window, no man by day, no police car, nothing named about the night). Each shot's
// first sound may lead its picture by 0.15 s (the J-cut). The bench close-ups (the screw, the phone waking) are parked until
// the new mat and one scale land. Then the pull-out: the brain's own 0.75 s blend to the player's camera (the last shot
// becomes the game) as the day's title slides in ("Tuesday · Day 2" and one line about the day, PLACEHOLDER COPY for his
// sister), the sting under it.
//
// What it holds, and gives back exactly: the DayClock is disabled for its length (OnDisable records the stop, OnEnable adds
// the stopped time back, as the night walk does), the two spawners are held, Ace is held (PlayerMovement.Hold: no walking,
// no E; the pause phone counts that as busy), the view ignores input (CafeViewMode.OverlayOwnsInput reads Playing), the
// screen's own clock stands still (UiClock.Hold: the Day 1 guide's hints, the HUD's corners), every other screen is faded
// out (a CanvasGroup on each root canvas, anything made meanwhile caught each frame) and faded back in on the pull-out. Its
// own camera is one CinemachineCamera made at run time at priority 50 (never saved in the scene) and moved by hand on
// unscaled time: a cut is a teleport, the only Cinemachine blend is the last one, to the player's camera, which therefore
// comes back exactly where it was. The intro does only two things to the scene: it holds a light's intensity and gives it
// back (the bench lamp, the café's own lights), and it adds its own props (the cup and the pour) under its Props root,
// removed after. The settled guests are seated as it begins (PatronSpawner.SeatOpeningGuestsNow, the NPC lead's). If a
// shot's subject is missing the shot is dropped and its slot redraws; fewer than two shots plays the Short form; if anything
// throws, the intro ends at once and the day starts.
//
// Never a hostage: any key, mouse button or pad button from the first frame skips to a shortened pull-out (0.5 s); the
// title still shows for its 1.2 s minimum (it carries the day's one line), over a game that is already yours. Short: the
// sting and the title over the player's own camera. 19 Oct 2026 (playtest 7): the title holds 6 s by itself (TitleHoldSeconds:
// a dyslexic reader's pace), or until the player first moves or presses once the controls are back; the skip prompt is
// "Skip" and the button's icon, 24 px white. Off: the bell rings as before. In a lab session it is off
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
    /// <summary>Before 14 Oct: last morning's order of shots. Read once into the history (as yesterday's), then deleted.</summary>
    public const string LastOrderKey = "FixitFidget.DayIntro.LastOrder";
    /// <summary>
    /// The last mornings, newest last: "day,grace,order;..." (cosmetic, per machine, never the save). A morning's day keys
    /// it: entries for today or later (another game, a reload) are dropped as the morning begins.
    /// </summary>
    public const string HistoryKey = "FixitFidget.DayIntro.History";
    public const int CameraPriority = 50;
    /// <summary>Its screen draws over everything but the pad's cursor (5000): the phone by day is 95.</summary>
    public const int SortingOrder = 200;
    /// <summary>The safe margins the title keeps from every edge, as a share of the screen.</summary>
    public const float SafeMargin = .05f;
    /// <summary>The title's least time on screen, even on a skip (it carries the day's one line).</summary>
    public const float TitleMinimumSeconds = 1.2f;
    /// <summary>
    /// How long the title holds by itself (19 Oct 2026: the reviewers timed the day's line at 2.8 s, about 215 words a
    /// minute; a dyslexic reader needs 5-6 s). It goes sooner the moment the player first moves or presses something once
    /// the controls are back, and never before its minimum.
    /// </summary>
    public const float TitleHoldSeconds = 6f;
    /// <summary>Where the title's middle sits: this share of the screen's height up from the bottom (above the middle, clear of the prompt).</summary>
    public const float TitleUp = .58f;
    /// <summary>The letterbox bars' height, as a share of the screen's height (each).</summary>
    public const float BarShare = .055f;

    // ---- timing (seconds of real time)
    const float FadeIn = .25f, BarsOut = .3f;
    const float PullOutSeconds = .75f, SkipPullOutSeconds = .5f;
    const float TitleIn = .35f, TitleHold = TitleHoldSeconds, TitleOut = .4f;
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
    /// <summary>The shot's length as played this morning (stretched when a slot came up empty).</summary>
    public float ShotLength => shot != null ? shot.Duration : 0f;
    public IReadOnlyList<float> ShotMoments => shot != null ? shot.Moments : Array.Empty<float>();
    /// <summary>The shot playing now, as the lab judges it: what it films (the point and the thing), how close the camera may come, whether it's a Grace shot.</summary>
    public Shot Current => shot;
    /// <summary>The intro's own camera this frame (where the next picture is taken from), and its lens (vertical degrees).</summary>
    public Vector3 CameraPosition => cam != null ? cam.transform.position : Vector3.zero;
    public Quaternion CameraRotation => cam != null ? cam.transform.rotation : Quaternion.identity;
    public float CameraFov => cam != null ? cam.Lens.FieldOfView : 40f;
    /// <summary>This morning's shots in order, and why the picker chose them (its notes: ties, Grace, drops).</summary>
    public IReadOnlyList<Shot> Planned => shots;
    public IReadOnlyList<string> PlanNotes => planNotes;

    /// <summary>
    /// While a Full morning's shots play: whether the intro's camera stands inside the café (the room's own rectangle), so the
    /// soundscape can play the street outside and the room inside (Audio's hook); null otherwise (the player's own view).
    /// </summary>
    public static bool? FilmingInsideCafe
    {
        get
        {
            DayIntro intro = Instance;
            if (intro == null || !intro.playing || intro.mode != Mode.Full || intro.cam == null || intro.phase == Phase.PullOut) return null;
            Vector3 p = intro.cam.transform.position;
            return CafeDaylight.CafeInside.Contains(new Vector2(p.x, p.z));
        }
    }
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
    /// <summary>This lab session plays as the real game (the whole-game playtest from Day 1): the morning's guests come too.</summary>
    public static bool LabWanted => labWanted;

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
    readonly List<Shot> available = new List<Shot>();   // every shot that could be framed this morning (a dropped one's slot redraws from here)
    readonly List<string> planNotes = new List<string>();
    bool redraws;                                        // false for a lab's explicit order: it asked for exactly those shots
    Shot shot;
    int shotIndex = -1;
    float shotTime;
    Transform propsRoot;
    /// <summary>The J-cut: a shot's first sound starts this long before its picture (spec §4.1.2).</summary>
    public const float LeadSeconds = .15f;
    /// <summary>A morning's shots last at least this long together, and no one shot more than ShotLongest (spec §4.1.7 rule 6).</summary>
    public const float ShotsLeast = 5.3f, ShotLongest = 2.4f;

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
            if (shots.Count < 2) mode = Mode.Short;   // no café to film (another scene), or one shot alone: the title alone
        }
        HoldTheWorld();
        MakeScreen();
        if (mode == Mode.Full)
        {
            MakeCamera();
            BeginShot(0);
            // A morning that opens under the night's curtain (NightCycle's fade from black): the first shot waits, framed, until
            // the curtain is up, so the night's own fade reveals the first shot rather than half of the second (and this
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
        // tomorrow can't also skip it). A press once the pull-out has begun only sends the title on its way, and so does the
        // player's first move (a stick or a walking key), once the controls are back (19 Oct: the title holds 6 s otherwise).
        if (Time.frameCount != startFrame && AnyPress()) Skip();
        else if ((phase == Phase.PullOut || phase == Phase.Title) && titleShown && !titleFading && AnyMove()) SkipTheTitle();

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
        if (!shot.LeadPlayed) PlayLead(shot);   // the first shot's own (or an alternate's, which no shot before it could lead)
        shotTime += dt;
        shot.Frame(this, shotTime, dt);
        shot.PlayCues(this, shotTime);
        // The J-cut: the next shot's first sound starts LeadSeconds before its picture, on this shot's clock.
        if (shotIndex + 1 < shots.Count && shotTime >= shot.Duration - LeadSeconds) PlayLead(shots[shotIndex + 1]);
        if (shotTime >= shot.Duration)
        {
            EndShot();
            if (shotIndex + 1 < shots.Count) BeginShot(shotIndex + 1);
            else BeginPullOut(false);
        }
    }

    static void PlayLead(Shot s)
    {
        if (s == null || s.LeadPlayed) return;
        s.LeadPlayed = true;
        if (!string.IsNullOrEmpty(s.LeadCue)) Sfx.Play2D(s.LeadCue);
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
        var tried = new List<Shot>();
        while (true)
        {
            try
            {
                shot.Begin(this);
                shot.Frame(this, 0f, 0f);
                break;
            }
            catch (Exception e)
            {
                // One shot's trouble costs that shot, not the morning: a subject gone (nobody on the pavement, no car near)
                // redraws its slot from the shots that could be framed this morning.
                Debug.Log($"[Day intro] The shot {shot.Id} couldn't be set up ({e.Message}).");
                try { shot.End(this); } catch (Exception) { }
                tried.Add(shot);
                Shot alternate = redraws ? Alternate(shot, tried) : null;
                if (alternate != null)
                {
                    planNotes.Add($"{shot.Id} dropped at its cut ({e.Message}): {alternate.Id} instead");
                    alternate.Duration = shot.Duration > 0f ? shot.Duration : alternate.Length;   // the slot keeps its time
                    alternate.LeadPlayed = false;
                    shots[index] = shot = alternate;
                    continue;
                }
                planNotes.Add($"{shot.Id} dropped at its cut ({e.Message}): nothing else in its slot");
                shots.RemoveAt(index);
                shot = null;
                Restretch(index);
                if (index < shots.Count) { BeginShot(index); return; }
                return;
            }
        }
        LastOrder = order = OrderOf(shots);
        ShotBegan?.Invoke(shot.Id);
    }

    // Another shot for a dropped one's slot: untied, framable this morning, not already in it, the most weight first.
    Shot Alternate(Shot dropped, List<Shot> tried)
    {
        Shot best = null;
        foreach (Shot s in available)
        {
            if (s.Slot != dropped.Slot || s.Tie != Tie.None || s.IsGrace || shots.Contains(s) || tried.Contains(s)) continue;
            if (best == null || s.Weight > best.Weight) best = s;
        }
        return best;
    }

    // A slot came up empty at its cut: the shots still to come stretch so the morning's shots still last ShotsLeast together
    // (none over ShotLongest).
    void Restretch(int from)
    {
        float played = 0f, toCome = 0f;
        for (int i = 0; i < shots.Count; i++) { if (i < from) played += shots[i].Duration; else toCome += shots[i].Duration; }
        float wanted = ShotsLeast - played;
        if (toCome <= 0f || wanted <= toCome) return;
        float k = wanted / toCome;
        for (int i = from; i < shots.Count; i++) shots[i].Duration = Mathf.Min(ShotLongest, shots[i].Duration * k);
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
        if (mode == Mode.Full && shots.Count > 0) RememberMorning(day, OrderOf(shots), shots.Exists(s => s.IsGrace));   // as played (a drop may have changed it)
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

    // The player walking: the left stick past its deadzone, or W A S D or an arrow key held.
    static bool AnyMove()
    {
        if (PadInput.LeftStick.sqrMagnitude > .25f) return true;
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed
            || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed);
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
        // The settled guests (NPC spec §2.1, contract §7): already in their seats under the first shot, so the interior shots
        // aren't empty and nobody pops into view as the controls come back. Idempotent; a no-op on a day with none.
        try { PatronSpawner.SeatOpeningGuestsNow(); }
        catch (Exception e) { Debug.LogError("[Day intro] Seating the morning's settled guests failed; the intro goes on: " + e.Message); }
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
            // In the bottom bar, right: "Skip" and the Cross (A, B) or Enter icon, 24 px white on the black (19 Oct 2026: the
            // reviewers found "Skip any key" in 14 px mid-grey the faintest text on screen, and keyboard wording for a pad).
            // Any button still skips; it fades in after a moment so the first frame is the picture.
            skipHint = UiSkin.Text("Skip", barBottom.rectTransform, 24f, Color.white, TextAlignmentOptions.MidlineRight, UiSkin.SemiBold);
            RectTransform hint = skipHint.rectTransform;
            hint.anchorMin = new Vector2(1f, 0f);
            hint.anchorMax = new Vector2(1f, 1f);
            hint.pivot = new Vector2(1f, .5f);
            hint.anchoredPosition = new Vector2(-Mathf.Round(1920f * SafeMargin), 0f);
            hint.sizeDelta = new Vector2(600f, 0f);
            skipHint.text = SkipWords();
            skipHint.alpha = 0f;
        }
        BuildTitle(root);
    }

    bool skipForPad;

    /// <summary>"Skip" and the button that does it, as its icon (the labs read it).</summary>
    public string SkipWords()
    {
        skipForPad = PadInput.UsingPad;
        return "Skip  " + ControlHints.Confirm;
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
            {
                skipHint.alpha = pullOutStarted >= 0f ? Mathf.MoveTowards(skipHint.alpha, 0f, dt * 8f)
                    : Mathf.Clamp01((shotsTime - .8f) / .4f);
                if (PadInput.UsingPad != skipForPad) skipHint.text = SkipWords();
            }
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
            // 24 Oct 2026 (Furniture): the cart lives in the intro and nowhere else (his choice of 23 Oct; furniture-spec.md §1.1).
            // Was "Fix what they bring. Pour what they order. Make the rent." (the hook: the recap could carry it, P's call).
            case 1: return "Years at a coffee cart bought this place. Now make it a café.";   // Learn the Shop
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

    // ------------------------------------------------------------------ the pool and the pick (intro v2, 14 Oct)

    /// <summary>How a shot frames its subject: Wide 4 m or more, Mid 1.2 m or more; Close only on a model built for it (the hero devices, the bench tools).</summary>
    public enum Framing { Wide, Mid, Close }

    /// <summary>
    /// What ties a shot to one morning (spec §4.1.7 rule 4), in priority order caught > today's regular > rush > rain. Caught
    /// and Rain are parked (the Producer's call 12: no police car until the night has that consequence; no weather until the
    /// scope freeze lifts): TodaysTie never returns them; their stubs stay for the day they're built.
    /// </summary>
    public enum Tie { None, Caught, Regular, Rush, Rain }

    /// <summary>Day 1's fixed order (spec §4.1.7 rule 1): what the place is. The street, the bench, the drinks, the café's name.</summary>
    public static readonly string[] Day1Order = { "street.row", "bench.ready", "drinks.pour", "street.front" };

    /// <summary>Every shot that can be built from the scene today (the storyboard), by slot; house.grace is tied (her mornings only).</summary>
    public static readonly string[] PoolIds =
    {
        "street.row", "street.storefront",
        "bench.ready", "counter.ready", "cafe.lights", "drinks.pour",
        "neighbour.front", "neighbours.row", "patio.flower", "house.grace",
        "street.front", "street.sign",
    };

    /// <summary>The day's own shots (slot 2, tied): Grace's house exists; the rest wait on other leads and never frame yet.</summary>
    public static readonly string[] TiedIds = { "house.grace", "regular.walkup", "queue.door", "police.car", "rain.window" };

    /// <summary>
    /// Parked (spec §4.1.6): the bench close-ups, out of the pool until the new mat and the one scale exist and Art Direction
    /// passes their lab photos. Their code is kept below, compiled out (DAYINTRO_BENCH_CLOSEUPS): it was written for the old
    /// tray and slab mat, which the bench builder is replacing, and must be re-aimed at the new stage before it returns.
    /// Parked 15 Oct too, from the check's photos on his Mac (their classes kept below, out of the catalogue): car.pass, whose
    /// camera follows the car along the pavement and can stop right behind the café's A-frame sign (half the frame a dark board);
    /// street.across, whose subject (the Front 2 shop house) is switched off in the scene, so it never framed.
    /// </summary>
    public static readonly string[] ParkedIds = { "bench.screw", "bench.wake", "car.pass", "street.across" };

    /// <summary>One of every shot in the pool and the tied ones, fresh and unprepared (the picker needs only their ids, slots, ties and weights).</summary>
    public static List<Shot> Catalogue() => new List<Shot>
    {
        new StreetRow(), new StreetStorefront(),
        new BenchReady(), new CounterReady(), new CafeLights(), new DrinksPour(),
        new NeighbourFront(), new NeighboursRow(), new PatioFlower(),
        new GraceHouseShot(), new RegularWalkUp(), new QueueAtDoor(), new PoliceCar(), new RainOnGlass(),
        new StreetFront(), new StreetSign(),
    };

    /// <summary>The catalogue, each prepared against the scene as it is now: the shots that could be framed this morning.</summary>
    public static List<Shot> PreparedNow(List<string> notes = null)
    {
        var things = SceneThings.Find();
        var ready = new List<Shot>();
        foreach (Shot s in Catalogue())
        {
            bool ok;
            try { ok = s.Prepare(things); }
            catch (Exception e) { notes?.Add($"{s.Id} can't be framed: {e.Message}"); ok = false; }
            if (ok) ready.Add(s);
        }
        return ready;
    }

    void PickShots(string[] explicitOrder)
    {
        planNotes.Clear();
        available.Clear();
        available.AddRange(PreparedNow(planNotes));
        shots.Clear();
        if (explicitOrder != null)
        {
            // A lab's explicit order: exactly those shots (a missing one is skipped, as before), no redraws.
            redraws = false;
            foreach (string id in explicitOrder)
            {
                Shot s = Find(available, id);
                if (s != null && !shots.Contains(s)) shots.Add(s);
                else planNotes.Add($"{id} can't be framed this morning (asked for by name)");
            }
            Stretch(shots);
        }
        else
        {
            redraws = true;
            var rng = new System.Random(unchecked(Environment.TickCount * 7919 + day * 131));   // its own dice: never the café's
            shots.AddRange(Plan(day, TodaysTie(day), available, ReadHistory(day), rng, planNotes));
        }
        order = OrderOf(shots);
        if (shots.Count > 0)
        {
            RememberMorning(day, order, shots.Exists(s => s.IsGrace));
            Debug.Log($"[Day intro] Day {day}, {mode}: {order}" + (planNotes.Count > 0 ? "  (" + string.Join("; ", planNotes) + ")" : ""));
        }
    }

    /// <summary>
    /// The picker, pure (spec §4.1.7), so the lab can run thirty mornings without a camera: the shots for <paramref name="day"/>,
    /// from those that can be framed, today's tie and the history. Day 1 is fixed. Other mornings draw one untied shot a slot,
    /// weighted: in slots 0-2 a shot used yesterday can't come and one used two mornings ago comes half as often; slot 3 draws
    /// the storefront 2:1 over the blade sign. The tie takes slot 2. Never the same order as any of the last six mornings
    /// (eight draws, then slot 0 swapped). Grace only when she is tied and in none of the last five mornings, never on Day 1.
    /// Each shot's Duration is set (stretched so the shots last ShotsLeast together, none over ShotLongest).
    /// </summary>
    public static List<Shot> Plan(int day, TieToday tie, IReadOnlyList<Shot> framable, IReadOnlyList<Morning> history, System.Random rng, List<string> notes)
    {
        var plan = new List<Shot>();
        if (day <= 1)
        {
            for (int i = 0; i < Day1Order.Length; i++)
            {
                Shot s = Find(framable, Day1Order[i]);
                if (s == null)
                {
                    s = Draw(framable, i, null, null, rng, plan);
                    notes?.Add($"Day 1's {Day1Order[i]} can't be framed: {(s != null ? s.Id : "nothing")} in its place");
                }
                if (s != null) plan.Add(s);
            }
            Stretch(plan);
            return plan;
        }
        Shot tied = TiedShot(day, tie, framable, history, notes);
        Morning yesterday = MorningOf(history, day - 1), twoAgo = MorningOf(history, day - 2);
        var recent = new List<string>();
        if (history != null)
            foreach (Morning m in history)
                if (m != null && m.day >= day - 6 && m.day < day) recent.Add(m.order);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            plan.Clear();
            for (int slot = 0; slot < 4; slot++)
            {
                Shot s = slot == 2 && tied != null ? tied : Draw(framable, slot, yesterday, twoAgo, rng, plan);
                if (s != null) plan.Add(s);
            }
            if (!recent.Contains(OrderOf(plan))) break;
        }
        if (plan.Count > 0 && recent.Contains(OrderOf(plan)))
        {
            // Eight draws all repeated a recent morning (it takes a thin pool): slot 0 swapped for another choice.
            Shot first = plan[0];
            foreach (Shot s in framable)
                if (s.Slot == first.Slot && s != first && s.Tie == Tie.None && !s.IsGrace && !plan.Contains(s)) { plan[0] = s; break; }
            notes?.Add("eight draws repeated a recent morning: slot 0 swapped");
        }
        Stretch(plan);
        return plan;
    }

    // One untied shot for a slot, weighted (the yesterday rule in slots 0-2), never one already taken this morning.
    static Shot Draw(IReadOnlyList<Shot> framable, int slot, Morning yesterday, Morning twoAgo, System.Random rng, List<Shot> taken)
    {
        var candidates = new List<Shot>();
        var weights = new List<float>();
        float sum = 0f;
        foreach (Shot s in framable)
        {
            if (s == null || s.Slot != slot || s.Tie != Tie.None || s.IsGrace || taken.Contains(s)) continue;
            float w = Mathf.Max(0f, s.Weight);
            if (slot < 3)
            {
                if (yesterday != null && yesterday.Used(s.Id)) w = 0f;
                else if (twoAgo != null && twoAgo.Used(s.Id)) w *= .5f;
            }
            candidates.Add(s);
            weights.Add(w);
            sum += w;
        }
        if (candidates.Count == 0) return null;
        if (sum <= 0f)
        {
            // Every candidate was yesterday's (a thin pool: dynamic shots dropped): the yesterday rule gives way.
            sum = 0f;
            for (int i = 0; i < candidates.Count; i++) { weights[i] = Mathf.Max(.0001f, candidates[i].Weight); sum += weights[i]; }
        }
        double roll = rng.NextDouble() * sum;
        Shot last = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (weights[i] <= 0f) continue;
            last = candidates[i];
            roll -= weights[i];
            if (roll < 0d) return candidates[i];
        }
        return last;
    }

    // The tied shot for slot 2, or null (no tie; Grace too recent; nothing built for this tie yet).
    static Shot TiedShot(int day, TieToday tie, IReadOnlyList<Shot> framable, IReadOnlyList<Morning> history, List<string> notes)
    {
        if (tie.kind == Tie.None || day <= 1) return null;
        string why = string.IsNullOrEmpty(tie.how) ? "" : $" ({tie.how})";
        if (tie.grace && history != null)
            foreach (Morning m in history)
                if (m != null && m.grace && m.day >= day - 5 && m.day < day)
                {
                    notes?.Add($"Grace is today's regular{why}, but she was in Day {m.day}'s intro: at most one morning in six, so B-roll");
                    return null;
                }
        string[] wanted = tie.kind == Tie.Regular ? (tie.grace ? new[] { "house.grace", "regular.walkup" } : new[] { "regular.walkup" })
                        : tie.kind == Tie.Caught ? new[] { "police.car" }
                        : tie.kind == Tie.Rush ? new[] { "queue.door" }
                        : new[] { "rain.window" };
        foreach (string id in wanted)
        {
            Shot s = Find(framable, id);
            if (s == null) continue;
            if (s is RegularWalkUp walkUp) walkUp.Who = tie.who;
            notes?.Add($"today's own: {id} ({tie.kind}{why})");
            return s;
        }
        notes?.Add($"today's tie, {tie.kind}{(tie.who != null ? " (" + tie.who.PersistentId + ")" : "")}{why}, has no shot built yet ({string.Join(", ", wanted)}): the normal draw");
        return null;
    }

    static void Stretch(List<Shot> plan)
    {
        float total = 0f;
        foreach (Shot s in plan) { s.Duration = s.Length; s.LeadPlayed = false; total += s.Length; }
        if (plan.Count < 2 || total <= 0f || total >= ShotsLeast) return;
        float k = ShotsLeast / total;
        foreach (Shot s in plan) s.Duration = Mathf.Min(ShotLongest, s.Length * k);
    }

    static Shot Find(IReadOnlyList<Shot> list, string id)
    {
        if (list == null) return null;
        foreach (Shot s in list) if (s != null && s.Id == id) return s;
        return null;
    }

    static Morning MorningOf(IReadOnlyList<Morning> history, int day)
    {
        if (history == null) return null;
        Morning found = null;
        foreach (Morning m in history) if (m != null && m.day == day) found = m;
        return found;
    }

    static string OrderOf(List<Shot> list)
    {
        var ids = new List<string>(list.Count);
        foreach (Shot s in list) ids.Add(s.Id);
        return string.Join("|", ids);
    }

    // ------------------------------------------------------------------ today's tie

    /// <summary>What ties this morning: the kind, the regular (Regular), whether it is Grace, and how it was found (the log).</summary>
    public struct TieToday
    {
        public Tie kind;
        public CustomerProfile who;
        public bool grace;
        public string how;
        public static TieToday None => new TieToday { kind = Tie.None, how = "" };
    }

    const string GraceId = "grace";

    /// <summary>
    /// This morning's tie (spec §4.1.7 rule 4; Days spec §8): today's regular, the first of: the featured regular arriving at
    /// 0.25 of the day or sooner; a story visit as early; this morning's visitor with news of last night; the officer's word.
    /// Else a rush morning (a phase named "...rush..." starting by 0.2 of the day). Never Day 1 (its order is fixed).
    /// </summary>
    public static TieToday TodaysTie(int day)
    {
        if (day <= 1) return TieToday.None;
        CustomerProfile who = TodaysRegular(day, out string how);
        if (who != null) return new TieToday { kind = Tie.Regular, who = who, grace = who.PersistentId == GraceId, how = how };
        DayDefinition definition = DefinitionFor(day);
        if (definition != null && definition.phases != null)
            foreach (DayPhase p in definition.phases)
                if (p != null && !string.IsNullOrEmpty(p.phaseName) && p.phaseName.IndexOf("rush", StringComparison.OrdinalIgnoreCase) >= 0 && p.startsAt <= .2f)
                    return new TieToday { kind = Tie.Rush, how = $"\"{p.phaseName}\" from {p.startsAt:0.00} of the day" };
        return TieToday.None;
    }

    static CustomerProfile TodaysRegular(int day, out string how)
    {
        how = "";
        DayDefinition definition = DefinitionFor(day);
        if (definition != null && definition.featuredRegular != null && definition.featuredRegularArrivesAt <= .25f)
        {
            how = $"featured, at {definition.featuredRegularArrivesAt:0.00} of the day";
            return definition.featuredRegular;
        }
        if (definition != null)
            foreach (StoryVisit visit in definition.StoryVisitsOn(day))
                if (visit != null && visit.who != null && visit.arrivesAt <= .25f)
                {
                    how = $"a story visit at {visit.arrivesAt:0.00} of the day";
                    return visit.who;
                }
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        bool today = spawner != null && DayClock.Instance != null && DayClock.Instance.Day == day;
        if (today && spawner.MorningVisitor != null) { how = "this morning's visitor, with news of last night"; return spawner.MorningVisitor; }
        if (today && spawner.WordVisitor != null) { how = "the officer, for the man's word"; return spawner.WordVisitor; }
        return null;
    }

    // The day's definition: the spawner's today when it is this day; else the day in the spawner's own schedule (a lab asking
    // about another day; read by name, so a renamed field only loses the lab's look-ahead); else any loaded day asset with the number.
    static DayDefinition DefinitionFor(int day)
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null && spawner.Today != null && spawner.Today.dayNumber == day) return spawner.Today;
        try
        {
            if (spawner != null)
            {
                var field = typeof(CustomerSpawner).GetField("schedule", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (field != null && field.GetValue(spawner) is DayDefinition[] schedule)
                    foreach (DayDefinition d in schedule)
                        if (d != null && d.dayNumber == day) return d;
            }
            foreach (DayDefinition d in Resources.FindObjectsOfTypeAll<DayDefinition>())
                if (d != null && d.dayNumber == day) return d;
        }
        catch (Exception) { }
        return null;
    }

    // ------------------------------------------------------------------ the history (PlayerPrefs: cosmetic, never the save)

    /// <summary>One played morning, as the history keeps it.</summary>
    public sealed class Morning
    {
        public int day;
        public string order = "";
        public bool grace;
        public bool Used(string id) => Array.IndexOf(order.Split('|'), id) >= 0;
        public override string ToString() => $"Day {day}: {order}{(grace ? " (Grace)" : "")}";
    }

    const int HistoryKept = 8;

    /// <summary>The mornings kept, oldest first; for <paramref name="day"/> (0: any), the pre-14 Oct last order read in as yesterday's.</summary>
    public static List<Morning> ReadHistory(int day = 0)
    {
        var list = new List<Morning>();
        string raw = PlayerPrefs.GetString(HistoryKey, "");
        if (!string.IsNullOrEmpty(raw))
            foreach (string entry in raw.Split(';'))
            {
                string[] parts = entry.Split(new[] { ',' }, 3);
                if (parts.Length == 3 && int.TryParse(parts[0], out int d)) list.Add(new Morning { day = d, grace = parts[1] == "1", order = parts[2] });
            }
        string old = PlayerPrefs.GetString(LastOrderKey, "");
        if (list.Count == 0 && old.Length > 0 && day > 1) list.Add(new Morning { day = day - 1, order = old, grace = old.Contains("house.grace") });
        return list;
    }

    static void RememberMorning(int day, string order, bool grace)
    {
        List<Morning> list = ReadHistory(day);
        if (PlayerPrefs.HasKey(LastOrderKey)) PlayerPrefs.DeleteKey(LastOrderKey);   // read in once, then let go
        list.RemoveAll(m => m.day >= day);   // today replaced; a later day is another game (a fresh Day 1, a reload)
        list.Add(new Morning { day = day, order = order, grace = grace });
        while (list.Count > HistoryKept) list.RemoveAt(0);
        var parts = new List<string>(list.Count);
        foreach (Morning m in list) parts.Add($"{m.day},{(m.grace ? 1 : 0)},{m.order}");
        PlayerPrefs.SetString(HistoryKey, string.Join(";", parts));
    }

    /// <summary>The title block if the canvas were <paramref name="canvasSize"/> reference pixels across and up (its scaler keeps the area of 1920 x 1080).</summary>
    public static Rect TitleBoxIfTheScreenWere(Vector2 canvasSize)
    {
        Vector2 size = LastTitleSize;
        return new Rect(canvasSize.x * .5f - size.x * .5f, canvasSize.y * TitleUp - size.y * .5f, size.x, size.y);
    }

    // ------------------------------------------------------------------ what there is to frame

    /// <summary>The scene's things the shots frame, found once a morning by name (the kit's pieces, the street's people) or by their components.</summary>
    public sealed class SceneThings
    {
        public Transform fascia, blade, lamp, repairBench, partsWall, frontCounter, gildingWest, sidewalkBench;
        public Transform bistroTable, houseRow, shopHouse, promenadeNeighbour;
        public readonly List<Transform> rowNeighbours = new List<Transform>();
        public Light lampLight;
        public BeverageSlot pourSlot;
        public StationInteractable drinks;
        public HomeDoor graceDoor;
        public StreetLife street;
        public CafeDaylight daylight;
#if DAYINTRO_BENCH_CLOSEUPS
        public BenchStage stage;
        public CinemachineCamera inspectCam;
        public GameObject deviceWithScrews;   // a device prefab asset (the phone) the bench's shots photocopy
        public Transform toolScrewdriver;
#endif

        public static SceneThings Find()
        {
            var t = new SceneThings();
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (tr == null) continue;
                string n = tr.name;
                if (n.StartsWith("CK_", StringComparison.Ordinal))
                {
                    if (t.fascia == null && n.StartsWith("CK_Fascia", StringComparison.Ordinal)) t.fascia = tr;
                    else if (t.blade == null && n.StartsWith("CK_BladeSign", StringComparison.Ordinal)) t.blade = tr;
                    else if (t.lamp == null && n.StartsWith("CK_BenchLamp", StringComparison.Ordinal)) t.lamp = tr;
                    else if (t.repairBench == null && n.StartsWith("CK_RepairBench", StringComparison.Ordinal)) t.repairBench = tr;
                    else if (t.partsWall == null && n.StartsWith("CK_PartsWall", StringComparison.Ordinal)) t.partsWall = tr;
                    else if (t.frontCounter == null && n.StartsWith("CK_FrontCounter", StringComparison.Ordinal)) t.frontCounter = tr;
                    else if (t.gildingWest == null && n.StartsWith("CK_WindowGilding_L", StringComparison.Ordinal)) t.gildingWest = tr;
                    else if (t.sidewalkBench == null && n.StartsWith("CK_SidewalkBench", StringComparison.Ordinal)) t.sidewalkBench = tr;
                }
                else if (t.bistroTable == null && n == "Outdoor bistro table") t.bistroTable = tr;
                else if (t.houseRow == null && n == "2 - Dusty rose bay-window house") t.houseRow = tr;
                else if (t.shopHouse == null && n == "Front - 2 - neighborhood shop house") t.shopHouse = tr;
                else if (t.promenadeNeighbour == null && n == "Front promenade neighbor") t.promenadeNeighbour = tr;
                else if (n.StartsWith("Walking neighbor ", StringComparison.Ordinal) && !n.EndsWith("route", StringComparison.Ordinal)) t.rowNeighbours.Add(tr);
            }
            if (t.lamp != null) t.lampLight = t.lamp.GetComponentInChildren<Light>(true);
            foreach (BeverageSlot slot in UnityEngine.Object.FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude))
            {
                if (slot == null || slot.cupPoint == null || slot.stream == null) continue;
                bool latte = slot.drink != null && !string.IsNullOrEmpty(slot.drink.drinkName) && slot.drink.drinkName.ToLowerInvariant().Contains("latte");
                if (t.pourSlot == null || latte) t.pourSlot = slot;
                if (latte) break;
            }
            foreach (StationInteractable s in StationInteractable.All)
                if (s != null && s.IsDrinks && s.StandPoint != null) { t.drinks = s; break; }
            t.graceDoor = HomeDoor.Find("home.grace");
            t.street = StreetLife.Main != null ? StreetLife.Main : UnityEngine.Object.FindAnyObjectByType<StreetLife>();
            t.daylight = UnityEngine.Object.FindAnyObjectByType<CafeDaylight>();
#if DAYINTRO_BENCH_CLOSEUPS
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
            t.deviceWithScrews = FindDeviceWithScrews();
#endif
            return t;
        }

        /// <summary>The café's own lights (CafeDaylight's warm lights inside the room), switched on.</summary>
        public List<Light> CafeLights()
        {
            var lights = new List<Light>();
            if (daylight == null || daylight.warmLights == null) return lights;
            foreach (Light l in daylight.warmLights)
            {
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                Vector3 p = l.transform.position;
                if (CafeDaylight.CafeInside.Contains(new Vector2(p.x, p.z))) lights.Add(l);
            }
            return lights;
        }

        /// <summary>The street's moving people and cars (StreetLife), each with what it is.</summary>
        public IEnumerable<StreetLife.Actor> Actors()
        {
            if (street == null || street.actors == null) yield break;
            foreach (StreetLife.Actor a in street.actors)
                if (a != null && a.actor != null && a.actor.gameObject.activeInHierarchy) yield return a;
        }

#if DAYINTRO_BENCH_CLOSEUPS
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
#endif
    }

    // ------------------------------------------------------------------ props: photocopies of the game's own meshes

    /// <summary>
    /// A still copy of what's drawn of <paramref name="source"/> (a prefab asset or a scene object): its solid parts only (a
    /// mesh each; not its job tag, not sprites or text), as children of a new object under the intro, keeping each part's name
    /// and pose. <paramref name="include"/> decides a part by its transform and whether it is on by default (null: on by default).
    /// Kept for the parked bench shots and, later, the regular's cameo or the day's own props; no live B-roll shot copies or
    /// moves a gameplay object.
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
    /// One shot: what it needs (Prepare finds it, false drops the shot), its props or held lights (Begin / End; Begin may throw
    /// when a moving subject isn't in reach at its cut: its slot redraws), its camera and motion each frame (Frame, on the shot's
    /// own clock), its sounds at their offsets (Cue) and its J-cut lead (LeadCue). Slots: 0 the street waking, 1 the café opening
    /// up, 2 someone or the day's own, 3 the open. Moments are where a photo shows it best (0-1 of its duration). The lab judges
    /// every frame against Subject, MinSubjectDistance and IsGrace.
    /// </summary>
    public abstract class Shot
    {
        public abstract string Id { get; }
        public abstract int Slot { get; }
        /// <summary>Its authored length, seconds.</summary>
        public abstract float Length { get; }
        /// <summary>As played this morning: its length, stretched when a slot came up empty (the picker sets it).</summary>
        public float Duration { get; internal set; }
        public virtual Framing Framing => Framing.Mid;
        /// <summary>How close the camera may come to the subject, metres (Wide 4.0, Mid 1.2; the dispenser 0.6).</summary>
        public virtual float MinSubjectDistance => Framing == Framing.Wide ? 4f : Framing == Framing.Mid ? 1.2f : .3f;
        public virtual Tie Tie => Tie.None;
        public virtual bool IsGrace => false;
        /// <summary>Its weight in the draw (1; the storefront 2 against the blade sign's 1).</summary>
        public virtual float Weight => 1f;
        /// <summary>The cue that starts LeadSeconds before its picture (the J-cut), or null.</summary>
        public virtual string LeadCue => null;
        /// <summary>The subject is the frame's aim (a street, a room), not one thing: the lab doesn't test it for being hidden.</summary>
        public virtual bool SubjectIsComposition => false;
        /// <summary>The point the lab measures to, as of the last frame, and the thing it belongs to (null for a composition).</summary>
        public Vector3 Subject { get; protected set; }
        public Transform SubjectRoot { get; protected set; }
        public virtual IReadOnlyList<float> Moments => new[] { .5f };
        internal bool LeadPlayed;

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

        /// <summary>The camera at <paramref name="at"/> looking at <paramref name="look"/>; <paramref name="subject"/> is what the lab measures to.</summary>
        protected void Film(DayIntro intro, Vector3 at, Vector3 look, float fov, Vector3 subject, float near = .05f)
        {
            Subject = subject;
            intro.Place(at, look, fov, 0f, near);
        }

        /// <summary>A point on a circle round <paramref name="axisPoint"/>'s vertical axis: <paramref name="from"/> turned by <paramref name="degrees"/>.</summary>
        protected static Vector3 Orbit(Vector3 axisPoint, Vector3 from, float degrees)
        {
            Vector3 flat = from - axisPoint;
            float y = flat.y;
            flat.y = 0f;
            return axisPoint + Quaternion.Euler(0f, degrees, 0f) * flat + Vector3.up * y;
        }

        /// <summary>
        /// Where to look from <paramref name="at"/> along <paramref name="direction"/>, panned by <paramref name="yawDegrees"/>
        /// (positive to the right) and tilted up by <paramref name="upDegrees"/>.
        /// </summary>
        protected static Vector3 Aim(Vector3 at, Vector3 direction, float yawDegrees, float upDegrees)
        {
            Vector3 d = Quaternion.AngleAxis(yawDegrees, Vector3.up) * direction;
            Vector3 right = Vector3.Cross(Vector3.up, d);
            if (right.sqrMagnitude > 1e-6f) d = Quaternion.AngleAxis(-upDegrees, right.normalized) * d;
            return at + d;
        }

        /// <summary><paramref name="from"/> pushed <paramref name="metres"/> along the level way toward <paramref name="toward"/>.</summary>
        protected static Vector3 Push(Vector3 from, Vector3 toward, float metres)
        {
            Vector3 flat = toward - from;
            flat.y = 0f;
            return flat.sqrMagnitude > 1e-6f ? from + flat.normalized * metres : from;
        }

        protected static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>A first-order follow toward <paramref name="target"/> with a time constant of <paramref name="seconds"/>: the same at any frame rate.</summary>
        protected static Vector3 Follow(Vector3 current, Vector3 target, float seconds, float dt) =>
            dt <= 0f ? current : Vector3.Lerp(current, target, 1f - Mathf.Exp(-dt / Mathf.Max(.01f, seconds)));
    }

    // ---------- slot 0: the street waking ----------

    /// <summary>
    /// The painted row across the west street from the café's south-west corner: house 2 (Dusty rose, 12 m), 3 (Sea green,
    /// 18 m) and 4 (Lavender, 24 m) on the left, the café's west wall and blade sign on the right, the lanes and the neighbours'
    /// pavement between, the hills beyond. A dolly of 0.8 m north, tilting up 2°. Grace's door is 52° off axis. Day 1's opener.
    /// </summary>
    sealed class StreetRow : Shot
    {
        public override string Id => "street.row";
        public override int Slot => 0;
        public override float Length => 1.8f;
        public override Framing Framing => Framing.Wide;
        public override bool SubjectIsComposition => true;
        public override IReadOnlyList<float> Moments => new[] { .55f };
        static readonly Vector3 From = new Vector3(-9.0f, 1.7f, -6.5f), Look = new Vector3(-13.0f, 3.5f, 14.0f);

        public override bool Prepare(SceneThings things) => things.houseRow != null;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 at = From + Vector3.forward * (.8f * k);
            Vector3 look = Aim(at, Look - From, 0f, 2f * k);
            Film(intro, at, look, 38f, look);
        }
    }

    /// <summary>
    /// Down the café's own pavement from its east end: the A-frame and the east olive tree near, the door and the fascia, the
    /// patio table and the blade sign, the painted row past the junction; the promenade neighbour; a pan left of 6°. Grace's
    /// house only as background at 25.7 m.
    /// </summary>
    sealed class StreetStorefront : Shot
    {
        public override string Id => "street.storefront";
        public override int Slot => 0;
        public override float Length => 1.8f;
        public override Framing Framing => Framing.Wide;
        public override bool SubjectIsComposition => true;
        static readonly Vector3 From = new Vector3(9.0f, 1.8f, -5.5f), Look = new Vector3(-4.0f, 1.6f, -1.5f);

        public override bool Prepare(SceneThings things) => things.fascia != null;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 look = Aim(From, Look - From, -6f * k, 0f);
            Film(intro, From, look, 40f, look);
        }
    }

    /// <summary>Across the street from the patio west of the door: both lanes, the far pavement, the café's customer parking and its board, the three shop houses (13.8 m); a push of 0.5 m.</summary>
    sealed class StreetAcross : Shot
    {
        public override string Id => "street.across";
        public override int Slot => 0;
        public override float Length => 1.8f;
        public override Framing Framing => Framing.Wide;
        public override bool SubjectIsComposition => true;
        static readonly Vector3 From = new Vector3(-2.0f, 1.6f, -3.0f), Look = new Vector3(2.0f, 2.5f, -16.6f);

        public override bool Prepare(SceneThings things) => things.shopHouse != null;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 at = Push(From, Look, .5f * k);
            Film(intro, at, Look, 40f, Look);
        }
    }

    // ---------- slot 1: the café opening up ----------

    /// <summary>
    /// Ace's side of the repair bar, a step to the left of his place (1.9 m): the bench top's two mats (the blue repair mat with
    /// its tool tray, the dark work mat with its tape and tickets), the magnifier lamp blinking on over them (an LED ring: a
    /// flicker, then steady; the light held and given back), the room beyond the counter's guard; a push of 0.3 m. Until 15 Oct
    /// it looked from a seated customer's eye through that guard, and on Day 1's very first morning the bench was a blur behind
    /// frosted glass (the check's photo on his Mac; Checks › Probe - the intro's bench shot rendered five framings: this is D).
    /// </summary>
    sealed class BenchReady : Shot
    {
        public override string Id => "bench.ready";
        public override int Slot => 1;
        public override float Length => 1.4f;
        public override bool SubjectIsComposition => true;
        public override IReadOnlyList<float> Moments => new[] { .6f };
        static readonly Vector3 From = new Vector3(-3.64f, 1.52f, 15.06f), Look = new Vector3(-5.09f, 1.04f, 13.91f);
        Light lampLight;
        float intensityBefore;
        bool lightHeld;

        public override bool Prepare(SceneThings things)
        {
            lampLight = things.lampLight;
            if (things.lamp == null || lampLight == null || things.repairBench == null) return false;
            Cue(.35f, "intro.lamp");
            return true;
        }

        public override void Begin(DayIntro intro)
        {
            intensityBefore = lampLight.intensity;
            lampLight.intensity = 0f;
            lightHeld = true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            if (lightHeld)
            {
                // Off; a first flicker at 0.35; off again; steady from 0.48 (an LED ring's blink, not a bulb's warm-up).
                float level = t < .35f ? 0f : t < .41f ? .55f : t < .46f ? .05f : Mathf.Lerp(.6f, 1f, Mathf.Clamp01((t - .46f) / .12f));
                lampLight.intensity = intensityBefore * level;
            }
            float k = Ease(t / Duration);
            Vector3 at = Push(From, Look, .3f * k);
            Film(intro, at, Look + (at - From), 40f, Look);
        }

        public override void End(DayIntro intro)
        {
            if (lightHeld && lampLight != null) lampLight.intensity = intensityBefore;
            lightHeld = false;
        }
    }

    /// <summary>The room's length from just inside the door: the communal table, the pastry case, the till, the bench and its REPAIRS sign, the dispenser, the menu board; a cup set down; a dolly of 0.6 m in.</summary>
    sealed class CounterReady : Shot
    {
        public override string Id => "counter.ready";
        public override int Slot => 1;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override bool SubjectIsComposition => true;
        static readonly Vector3 From = new Vector3(.6f, 1.6f, 1.4f), Look = new Vector3(0f, 1.45f, 14.5f);

        public override bool Prepare(SceneThings things)
        {
            if (things.frontCounter == null) return false;
            Cue(.4f, "intro.cup");
            return true;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 dolly = (Look - From).normalized * (.6f * k);
            Film(intro, From + dolly, Look + dolly, 42f, Look);
        }
    }

    /// <summary>
    /// Through the west gilded window from the pavement: the olive tree near, the communal table, the counter and back bar
    /// beyond, the café's own lights coming up (held at 15%, raised to full over 0.5 s from 0.3 s, given back at the cut); a
    /// breaker's clunk. Static.
    /// </summary>
    sealed class CafeLights : Shot
    {
        public override string Id => "cafe.lights";
        public override int Slot => 1;
        public override float Length => 1.4f;
        public override bool SubjectIsComposition => true;
        public override IReadOnlyList<float> Moments => new[] { .7f };
        static readonly Vector3 From = new Vector3(-3.2f, 1.55f, -3.2f), Look = new Vector3(-1.6f, 1.4f, 4.0f);
        const float Low = .15f, UpAt = .3f, UpSeconds = .5f;
        List<Light> lights;
        readonly List<float> before = new List<float>();
        bool held;

        public override bool Prepare(SceneThings things)
        {
            if (things.gildingWest == null) return false;
            lights = things.CafeLights();
            if (lights.Count == 0) return false;
            Cue(UpAt, "intro.lights");
            return true;
        }

        public override void Begin(DayIntro intro)
        {
            before.Clear();
            foreach (Light l in lights) before.Add(l != null ? l.intensity : 0f);
            held = true;
            SetLevel(Low);
        }

        void SetLevel(float level)
        {
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] != null) lights[i].intensity = before[i] * level;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            if (held) SetLevel(Mathf.Lerp(Low, 1f, Ease((t - UpAt) / UpSeconds)));
            Film(intro, From, Look, 36f, Look);
        }

        public override void End(DayIntro intro)
        {
            if (held) SetLevel(1f);   // exactly as they were (record, hold, restore: BenchReady's pattern)
            held = false;
        }
    }

    /// <summary>
    /// A latte pouring, seen straight on from the dispenser's axis 0.5 m behind Ace's stand point (the drinks close-up's own
    /// language since 14 Oct): the whole face square, Latte's cup filling on the right. The cup and the pour are the game's own
    /// procedural ones (nothing is poured for real: no cup, no stock); the dispenser's own pour sound. Static. On Day 1 it
    /// plays in slot 2.
    /// </summary>
    sealed class DrinksPour : Shot
    {
        public override string Id => "drinks.pour";
        public override int Slot => 1;
        public override float Length => 1.4f;
        public override float MinSubjectDistance => .6f;   // the dispenser was built for its own 1 m close-up (spec §4.1.5)
        public override IReadOnlyList<float> Moments => new[] { .6f };
        BeverageSlot pourSlot;
        StationInteractable drinks;
        GameObject cup;
        Transform liquid;
        LineRenderer stream;
        Material shellMaterial, liquidMaterial;
        AudioSource pour;
        float pourVolume;
        bool ownSound;

        public override bool Prepare(SceneThings things)
        {
            pourSlot = things.pourSlot;
            drinks = things.drinks;
            return pourSlot != null && pourSlot.cupPoint != null && pourSlot.stream != null && drinks != null && drinks.StandPoint != null;
        }

        public override void Begin(DayIntro intro)
        {
            BeverageSlot slot = pourSlot;
            SubjectRoot = drinks.transform;
            Color colour = DrinkLiquidVisual.LiquidColor(slot.drink);
            // The cup, as DrinkLiquidVisual draws the game's: the open paper shell and the rising disc of drink.
            cup = new GameObject("The cup");
            cup.transform.SetParent(intro.Props, false);
            cup.transform.SetPositionAndRotation(slot.cupPoint.position, Quaternion.identity);
            shellMaterial = DrinkLiquidVisual.MakeMaterial("Day intro cup (while playing)", DrinkLiquidVisual.PaperColour, .08f);
            liquidMaterial = DrinkLiquidVisual.MakeMaterial("Day intro drink (while playing)", colour, DrinkLiquidVisual.LiquidSmoothness);   // 26 Oct 2026 (Art fixes) a08: matt enough to show its colour
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
            // Its sound: the bank's drink.pour (the real SoftPour.wav), else the dispenser's own file, faded in and out.
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
            float k = t / Duration;
            float fill = Mathf.Lerp(.22f, .86f, Mathf.Clamp01(k));
            SetFill(fill);
            Vector3 cupBase = cup != null ? cup.transform.position : pourSlot.cupPoint.position;
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
                pour.volume = pourVolume * Mathf.Min(Mathf.Clamp01(t / .12f), Mathf.Clamp01((Duration - t) / .18f)) * GameSettings.BusVolume(SoundBus.World);
            // Straight on from the dispenser's axis, 0.5 m behind the stand point at 1.6 m, onto the working face's middle
            // (0.26 m up, 0.14 m out from the dispenser's origin, as the installer measures it).
            Transform d = drinks.transform;
            Vector3 ahead = Vector3.ProjectOnPlane(d.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-6f) ahead = Vector3.forward;
            ahead.Normalize();
            Vector3 stand = drinks.StandPoint.position;
            Vector3 at = new Vector3(stand.x, stand.y + 1.6f, stand.z) - ahead * .5f;
            Vector3 face = d.TransformPoint(new Vector3(0f, .257f, -.14f));
            Film(intro, at, face, 40f, face, .03f);
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

    // ---------- slot 2: someone, or the day's own ----------

    /// <summary>
    /// The neighbour who loops the café's own pavement, from the far pavement: the camera where he is (within x ±5), panning
    /// with him, the look trailing 0.2 s; at 8 m he fills about 44% of the frame, the café's windows and fascia behind him;
    /// a car may cross the foreground. Pavement footsteps lead the picture.
    /// </summary>
    sealed class NeighbourFront : Shot
    {
        public override string Id => "neighbour.front";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override string LeadCue => "intro.steps";
        Transform walker;
        Vector3 at, look;

        public override bool Prepare(SceneThings things)
        {
            walker = things.promenadeNeighbour;
            return walker != null && walker.gameObject.activeInHierarchy;
        }

        public override void Begin(DayIntro intro)
        {
            if (walker == null || !walker.gameObject.activeInHierarchy) throw new InvalidOperationException("the promenade neighbour isn't out");
            SubjectRoot = walker;
            at = new Vector3(Mathf.Clamp(walker.position.x, -5f, 5f), 1.5f, -11.8f);
            look = walker.position + Vector3.up * 1.3f;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            Vector3 chest = walker.position + Vector3.up * 1.3f;
            look = Follow(look, chest, .2f, dt);
            Film(intro, at, look, 28f, chest);
        }
    }

    /// <summary>
    /// A neighbour on their own street: one of the three walking the west street's far pavement, only while one is in front of
    /// houses 2-4 (z 4-17, which keeps Grace's door at least 34° off axis), the painted fronts behind; panning with them. No one
    /// there at the cut: the shot drops and its slot redraws.
    /// </summary>
    sealed class NeighboursRow : Shot
    {
        public override string Id => "neighbours.row";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override string LeadCue => "intro.steps";
        // The window, a little inside z 4-17 so a walker at either end still is inside it when the shot ends.
        const float WindowSouth = 4.6f, WindowNorth = 16.4f;
        List<Transform> walkers;
        Transform walker;
        Vector3 at, look;

        public override bool Prepare(SceneThings things)
        {
            walkers = things.rowNeighbours;
            return walkers != null && walkers.Count > 0;
        }

        public override void Begin(DayIntro intro)
        {
            walker = null;
            foreach (Transform w in walkers)
            {
                if (w == null || !w.gameObject.activeInHierarchy) continue;
                float z = w.position.z;
                if (z < WindowSouth || z > WindowNorth) continue;
                if (walker == null || Mathf.Abs(z - 10.5f) < Mathf.Abs(walker.position.z - 10.5f)) walker = w;
            }
            if (walker == null) throw new InvalidOperationException("nobody on the west street's far pavement in front of houses 2-4");
            SubjectRoot = walker;
            at = new Vector3(-8.4f, 1.5f, walker.position.z - 1.5f);
            look = walker.position + Vector3.up * 1.3f;
        }

        public override void Frame(DayIntro intro, float t, float dt)
        {
            Vector3 chest = walker.position + Vector3.up * 1.3f;
            look = Follow(look, chest, .2f, dt);
            Film(intro, at, look, 30f, chest);
        }
    }

    /// <summary>
    /// A car passing along the front street, from the café's pavement: the nearest moving car (over 2 m/s) on the westbound
    /// lane (z -6.6) or the eastbound (z -9.4) that will be in front of the café 0.7 s after the cut; the camera waits where it
    /// will cross (4.6 or 7.4 m away), the far pavement, the parking and the shop houses behind. Static. No car in reach: the
    /// shot drops and its slot redraws. Its pass-by leads the picture.
    /// </summary>
    sealed class CarPass : Shot
    {
        public override string Id => "car.pass";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override string LeadCue => "intro.car";
        const float Westbound = -6.6f, Eastbound = -9.4f, LaneHalf = 1.4f, Reach = 10f, Peak = .7f;
        SceneThings things;
        Transform car;
        Vector3 at, look;

        public override bool Prepare(SceneThings t)
        {
            things = t;
            if (t.street == null) return false;
            foreach (StreetLife.Actor a in t.Actors()) if (IsCar(a)) return true;
            return false;
        }

        // StreetLife's own split: not a walker and no wings (the birds aren't walkers either).
        static bool IsCar(StreetLife.Actor a) => !a.isWalker && (a.wings == null || a.wings.Length == 0);

        public override void Begin(DayIntro intro)
        {
            car = null;
            float best = float.PositiveInfinity, passX = 0f;
            foreach (StreetLife.Actor a in things.Actors())
            {
                if (!IsCar(a) || a.CurrentSpeed < 2f) continue;
                Vector3 p = a.actor.position;
                bool west = Mathf.Abs(p.z - Westbound) <= LaneHalf, east = Mathf.Abs(p.z - Eastbound) <= LaneHalf;
                if (!west && !east) continue;
                // Which way it drives is the lane's (spec §4.1.3: lane 5 westbound, lane 4 eastbound), not the model's facing.
                float x = p.x + (west ? -1f : 1f) * a.CurrentSpeed * Peak;
                if (Mathf.Abs(x) > Reach || Mathf.Abs(x) >= best) continue;
                best = Mathf.Abs(x);
                passX = x;
                car = a.actor;
            }
            if (car == null) throw new InvalidOperationException("no car on the front street will pass the café at the right moment");
            SubjectRoot = car;
            at = new Vector3(passX, 1.2f, -2.0f);
            look = new Vector3(passX, .9f, -7.5f);
        }

        public override void Frame(DayIntro intro, float t, float dt) =>
            Film(intro, at, look, 40f, car != null ? car.position + Vector3.up * .7f : look);
    }

    /// <summary>The outdoor bistro table's single flower in its bud vase (2.1 m), the painted row soft behind (15-24 m); a push of 0.3 m. A settled guest there becomes the shot.</summary>
    sealed class PatioFlower : Shot
    {
        public override string Id => "patio.flower";
        public override int Slot => 2;
        public override float Length => 1.4f;
        static readonly Vector3 From = new Vector3(-2.6f, .95f, -3.4f);
        Transform table;

        public override bool Prepare(SceneThings things)
        {
            table = things.bistroTable;
            return table != null;
        }

        public override void Begin(DayIntro intro) => SubjectRoot = table;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 top = table.position + Vector3.up * .8f;
            Vector3 at = Push(From, top, .3f * k);
            Film(intro, at, top, 38f, top);
        }
    }

    /// <summary>
    /// Grace's front from across the west street at the café's corner: her door (8.6 m), the bay (8.2 m) and her step, Barnaby
    /// on it unless Ace took him (the game hides a taken thing: the morning after Night 1 tells itself without words); a push of
    /// 0.4 m. It never looks into her window. Tied: a morning she is really coming in early, at most one in six, never Day 1.
    /// </summary>
    sealed class GraceHouseShot : Shot
    {
        public override string Id => "house.grace";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override Tie Tie => Tie.Regular;
        public override bool IsGrace => true;
        // 26 Oct 2026 (Art fixes) a69: "a traffic signal post stands across her door and window, in the middle of the shot" (the art
        // audit, 23 Oct). The junction's signal (V4 - through traffic, at x -15.45, z -2.8) stood half a metre off the line from the
        // camera to her door. The shot moves 1 m north along West Street's pavement, its aim with it: the post is 1.5 m off the
        // line now, out from in front of her door, and the house is framed as before.
        static readonly Vector3 From = new Vector3(-8.6f, 1.6f, -1.5f), Look = new Vector3(-17.4f, 2.3f, -1.2f);
        Transform door;

        public override bool Prepare(SceneThings things)
        {
            door = things.graceDoor != null ? things.graceDoor.transform : null;
            return door != null;
        }

        public override void Begin(DayIntro intro) => SubjectRoot = door;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            Vector3 at = Push(From, Look, .4f * k);
            Film(intro, at, Look + (at - From), 32f, door.position + Vector3.up * 1f);
        }
    }

    /// <summary>
    /// Waiting on the NPC lead: today's regular walking up the front pavement from the sidewalk bench toward the door, a cameo
    /// (their look, their walk by mood, no brain, removed at the cut). Never frames until it's built.
    /// </summary>
    sealed class RegularWalkUp : Shot
    {
        public override string Id => "regular.walkup";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override Tie Tie => Tie.Regular;
        public override bool IsGrace => Who != null && Who.PersistentId == GraceId;
        public override string LeadCue => "intro.steps";
        public CustomerProfile Who;
        public override bool Prepare(SceneThings things) => false;
        public override void Frame(DayIntro intro, float t, float dt) { }
    }

    /// <summary>Waiting on the NPC lead, Systems and the Director: three or four of a rush morning's first customers at the door. Never frames until it's built.</summary>
    sealed class QueueAtDoor : Shot
    {
        public override string Id => "queue.door";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override Tie Tie => Tie.Rush;
        public override bool Prepare(SceneThings things) => false;
        public override void Frame(DayIntro intro, float t, float dt) { }
    }

    /// <summary>Parked (the Producer's call 12): a fictional police car in the customer parking the morning after a caught night. Never frames until the night has that consequence.</summary>
    sealed class PoliceCar : Shot
    {
        public override string Id => "police.car";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Framing Framing => Framing.Wide;
        public override Tie Tie => Tie.Caught;
        public override bool Prepare(SceneThings things) => false;
        public override void Frame(DayIntro intro, float t, float dt) { }
    }

    /// <summary>Parked (the scope freeze): rain on the gilded window, the street blurred behind. There is no weather yet.</summary>
    sealed class RainOnGlass : Shot
    {
        public override string Id => "rain.window";
        public override int Slot => 2;
        public override float Length => 1.4f;
        public override Tie Tie => Tie.Rain;
        public override bool Prepare(SceneThings things) => false;
        public override void Frame(DayIntro intro, float t, float dt) { }
    }

    // ---------- slot 3: the open ----------

    /// <summary>The storefront from its own pavement: the fascia with the café's name over the open door, the gilded windows, the A-frame in the foreground; the bell. The shot she loved ("the span on aces cafe sign"): unchanged but for its length.</summary>
    sealed class StreetFront : Shot
    {
        public override string Id => "street.front";
        public override int Slot => 3;
        public override float Length => 1.4f;
        public override float Weight => 2f;
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

        public override void Begin(DayIntro intro) => SubjectRoot = fascia;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
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
            Film(intro, at, look, 30f, look);
        }
    }

    /// <summary>The round blade sign at the corner, close, with the front street waking beyond it; the bell.</summary>
    sealed class StreetSign : Shot
    {
        public override string Id => "street.sign";
        public override int Slot => 3;
        public override float Length => 1.4f;
        Transform blade;

        public override bool Prepare(SceneThings things)
        {
            blade = things.blade;
            if (blade == null) return false;
            Cue(0f, "intro.sign");
            Cue(DayOpenAfterSign, "day.open");
            return true;
        }

        public override void Begin(DayIntro intro) => SubjectRoot = blade;

        public override void Frame(DayIntro intro, float t, float dt)
        {
            float k = Ease(t / Duration);
            // The disc stands 0.58 m out from the wall along the sign's -X and 0.40 up from its origin (cafe_signs.json); its faces
            // look along the street (its Z), so the view stands off along +Z, a little out from the wall and below.
            Vector3 disc = blade.TransformPoint(new Vector3(-.58f, .40f, 0f));
            Vector3 alongStreet = blade.forward;
            Vector3 outFromWall = -blade.right;
            Vector3 at = disc + alongStreet * Mathf.Lerp(2.9f, 2.6f, k) + outFromWall * 1.05f + Vector3.up * Mathf.Lerp(-.95f, -.85f, k);
            Film(intro, at, disc, 30f, disc);
        }
    }

    // ---------- parked: the bench close-ups (spec §4.1.6; compiled out until the new mat and one scale land) ----------

#if DAYINTRO_BENCH_CLOSEUPS
        /// <summary>
        /// Lays a prop (already turned the right way up) flat in the middle of the mat: its long side across the view (along the
        /// mat's own long side), its lowest point on the mat's top. <paramref name="toward"/> is the way from the mat to the viewer.
        /// </summary>
        static void LayOnTheMat(GameObject prop, Vector3 matCentre, float matTop, Vector3 toward)
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

    /// <summary>
    /// A phone lies back-up in the middle of the mat; the hero screwdriver backs a screw out of it, three ticks and a turn a
    /// tick, the head spinning where the hopper's round was; it hops free and the view whips to the tray as it lands: tink.
    /// A photocopy of the phone prefab and of the caddy's screwdriver; the bench itself is untouched.
    /// </summary>
    sealed class BenchScrew : Shot
    {
        public override string Id => "bench.screw";
        public override int Slot => 1;
        public override float Length => 2f;
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
            float settle = Mathf.Clamp01((t - TurnSeconds - .35f) / (Duration - TurnSeconds - .35f));
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

    /// <summary>A fixed phone face up on the mat: its screen lights (as DeviceWake lights it), the boot chime. Yesterday's work, done.</summary>
    sealed class BenchWake : Shot
    {
        public override string Id => "bench.wake";
        public override int Slot => 2;
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
            float e = Ease(t / Duration);
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
#endif
}
