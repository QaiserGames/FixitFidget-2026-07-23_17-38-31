using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ---------------------------------------------------------------------------
// BENCH 8: THE CLUMSY HAND (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §5)
//
// Fixit Fidget > Bench > Bench 8 - The clumsy hand (lab, drives itself). His playtest 5 said the bench was too hard to get
// past Day 1. This lab is the gate: a first-timer's hand (ClumsyHand: slow to react, overshooting, pressing early, letting go
// early, taking the wrong tool, scrubbing blind) plays the four repairs through the REAL input path, a virtual mouse (his
// own mouse switched off for the run, so a nudge can't take over) or the lab's virtual pad moving the real pad cursor, at 60
// fps and at a jittered 12-18 fps (never better than his Mac). It never calls a part's method: it sees only what a player
// sees (the card's step and its glowing targets, the hover, the tools on the rest, the dirt still drawn, the cursor).
//
//   1. Grace's camera, Day 1's clean (fault 0);   2. Grace's camera, Day 2's swap (fault 1);
//   3. the phone's cracked screen;                 4. the watch's seized movement.
//
// Each with the mouse and the pad, at 60 and at 15 fps: 16 runs (Full: three seeds each, 48). The same seed plays a repair
// at both frame rates (the hand's choices are the same; only the frames differ), so their steps can be compared.
//
// The bars (every run): finished, Perfect, within 45 / 30 / 75 / 110 s; no piece lost or hidden for a single frame (a piece
// at rest off the device out of the frame, at zoom ≤ 0.3, or behind something; the tool in the hand and flights don't
// count); every near-miss press inside a target's hit area acted on it; no step with more than 3 misses; no early-released
// hold lost its progress; strokes per spot median ≤ 3, every spot ≤ 6, ≥ 90 % within 4; 60 and 15 fps within 25 % per step
// (beyond whole frames); no guard trips; nothing logged an error. And the cloth shows what it cleans (the Producer's call
// 23, QA M3): over the middle of every cloth wipe (the spot 25-75 % clean), on average at least 40 % of the spot's disc (its
// real disc on its plane, on screen) lies outside the held cloth's box on screen (its drawn vertices at that frame's keys);
// the worst single frame is reported, and the first wipe of each run is photographed mid-wipe.
//
// Report, timeline.csv and a photo at each step's end: Logs/BenchClumsy/<time>/.
// ---------------------------------------------------------------------------
public sealed class ClumsyBenchLab : PlayLab
{
    public const string PendingKey = "FixitFidget.Bench.Clumsy";
    public const string FullKey = "FixitFidget.Bench.ClumsyFull";

    public bool full;

    protected override string Title => full ? "The clumsy hand (full: three seeds)" : "The clumsy hand";
    protected override string Tag => "[Clumsy hand]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        bool wantFull = PlayerPrefs.GetInt(FullKey, 0) != 0;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(FullKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Clumsy hand] The lab was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Clumsy hand (this Play session only)");
        var lab = go.AddComponent<ClumsyBenchLab>();
        lab.full = wantFull;
        lab.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BenchClumsy", $"{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // ---------- the four repairs ----------

    enum Repair { CameraClean, CameraSwap, PhoneScreen, WatchSeized }
    static readonly string[] RepairName = { "Camera clean", "Camera swap", "Phone screen", "Watch seized" };
    /// <summary>The bars (seconds, finished and Perfect within): §5's table.</summary>
    static readonly float[] Bar = { 45f, 30f, 75f, 110f };
    const string PhonePath = "Assets/AssetsPrefabs/PhoneRepair.prefab", WatchPath = "Assets/AssetsPrefabs/PocketWatch.prefab",
                 CameraPath = "Assets/GraceShowcase/GraceReunionCamera.prefab";

    sealed class StepLog
    {
        public string verb;
        public int index;
        public float started, took = -1f;
        public int presses, misses, retries, nearMisses, nearInside, nearActed;
        public readonly List<float> nearPx = new List<float>();
    }

    sealed class RunLog
    {
        public Repair repair;
        public bool pad;
        public int fps, seed;
        public float seconds = -1f;
        public bool finished;
        public string grade = "-";
        public int lostFrames, hiddenFrames, guardTrips, resets, errors, bounces, wipes;
        public readonly HashSet<string> lost = new HashSet<string>(), hidden = new HashSet<string>();
        public readonly List<StepLog> steps = new List<StepLog>();
        public readonly List<int> strokes = new List<int>();
        public readonly List<float> frames = new List<float>();
        // Call 23: per cloth wipe, the mean share of the spot left in sight over its middle; the worst frame; wipes unmeasured.
        public readonly List<float> clothSeen = new List<float>();
        public float clothSeenWorst = 1f;
        public int clothWipes, clothUnmeasured;
        public bool clothPhoto;
        public string note = "";
        public string Label => $"{RepairName[(int)repair]} / {(pad ? "pad" : "mouse")} / {fps} fps";
    }

    readonly List<RunLog> runs = new List<RunLog>();

    // ---------- the scene ----------

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    CustomerSpawner spawner;
    bool spawnerWas = true;
    StationInteractable bench;
    Camera cam;
    GameObject phonePrefab, watchPrefab, cameraPrefab;
    readonly List<GameObject> made = new List<GameObject>();

    // What the lab changed, to put back (Restore).
    Mouse mouse;
    Keyboard keyboard;
    readonly List<InputDevice> switchedOff = new List<InputDevice>();
    bool swapWas, aimWas, settingsTouched;
    int vsyncWas, targetWas;
    StreamWriter timeline;

    // ---------- the run under way ----------

    RunLog run;
    ClumsyHand hand;
    bool usePad, watching;
    int fps;
    JobBase job;
    GameObject device;
    float runStarted, lastFrameEnd;
    System.Random capJitter = new System.Random(1);

    // The hand's input this frame (Send queues it, once a frame).
    Vector2 cursor, lastSent;
    bool left, keyR;
    Vector2 stick;
    bool aDown, rtDown, lbDown, rbDown, yDown, dpadRight;
    bool holdOnA = true;
    string mark = "";

    // A reach under way (the mouse's plan, or the stick's), and whether it stopped early for a near-miss press.
    ClumsyHand.Reach reach;
    float reachStarted;
    bool reachLeft, nearMissNow;
    ClumsyHand.StickReach stickReach;

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null, "Ace is in the scene");
        if (interactor == null || view == null || carry == null || inspector == null) yield break;
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);

        BenchStage stage = BenchStage.Instance;
        Check(stage != null && stage.matFrame != null && stage.cups != null && stage.cups.Length == 6 && stage.scrapTray != null && stage.toolRest != null,
            "the forgiving stage is wired: the mat with its cups, pad and well, the old-parts tray, the tool rest (Bench 7)");
        if (stage == null || stage.matFrame == null) yield break;
        bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(bench != null, "the repair bench has a stand point");
        if (bench == null) yield break;
#if UNITY_EDITOR
        phonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePath);
        watchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatchPath);
        cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath);
#endif
        Check(phonePrefab != null && watchPrefab != null && cameraPrefab != null, "the phone, the watch and Grace's camera load");

        // The settings the hand plays under (put back in Restore): the game's own stick layout, aim help on.
        swapWas = GameSettings.SwapBenchSticks;
        aimWas = AimAssist.Enabled;
        settingsTouched = true;
        GameSettings.SwapBenchSticks = false;
        AimAssist.Enabled = true;
        vsyncWas = QualitySettings.vSyncCount;
        targetWas = Application.targetFrameRate;

        try
        {
            timeline = new StreamWriter(Path.Combine(folder, "timeline.csv"));
            timeline.WriteLine("run,t,frame,cursor_x,cursor_y,buttons,hovered,event");
        }
        catch (Exception e) { Note("no timeline: " + e.Message); }

        int seeds = full ? 3 : 1;
        int baseSeed = Environment.TickCount & 0x3fffffff;
        Note($"base seed {baseSeed} (a run's seed is the base + 1000 x repair + 100 x input + its number)");
        foreach (Repair repair in Enum.GetValues(typeof(Repair)))
            for (int input = 0; input < 2; input++)
                for (int s = 0; s < seeds; s++)
                {
                    int seed = baseSeed + 1000 * (int)repair + 100 * input + s;
                    foreach (int rate in new[] { 60, 15 })
                        yield return OneRun(repair, input == 1, rate, seed);
                }

        Report();
    }

    // ---------- one run ----------

    IEnumerator OneRun(Repair repair, bool withPad, int rate, int seed)
    {
        // A run that stopped short leaves its device: it goes first, so the bench is free.
        if (device != null)
        {
            if (inspector.IsHoldingItem) inspector.CancelInspection();
            if (job != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
            made.Remove(device);
            Destroy(device);
            device = null;
            job = null;
            yield return Frames(3);
        }
        var log = new RunLog { repair = repair, pad = withPad, fps = rate, seed = seed };
        runs.Add(log);
        run = log;
        hand = new ClumsyHand(seed, withPad);
        capJitter = new System.Random(seed ^ 0x5bd1e995);
        usePad = withPad;
        fps = rate;
        Note($"--- {log.Label}, seed {seed}");

        // The frame rate: 60, or 15 jittered to 12-18 (a sleep at each frame's end caps it whatever the editor allows).
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = rate;

        // The device, by the lab's hand: on the bench, its fault on, with a full day ahead.
        GameObject prefab = repair == Repair.PhoneScreen ? phonePrefab : repair == Repair.WatchSeized ? watchPrefab : cameraPrefab;
        int fault = prefab != null ? FaultFor(repair, prefab.GetComponent<DeviceDefinition>()) : -1;
        if (prefab == null || fault < 0)
        {
            log.note = prefab == null ? "no prefab" : repair == Repair.CameraSwap ? "the camera has one fault: the days builder's split (step B) isn't in yet" : "no such fault on the device";
            Check(false, $"{log.Label}: can't be played ({log.note})");
            yield break;
        }
        KeepTheDayOpen();
        device = Instantiate(prefab, bench.StandPoint.position + Vector3.up * 1.2f, Quaternion.identity);
        device.name = $"Clumsy hand: {RepairName[(int)repair]} ({seed})";
        made.Add(device);
        var definition = device.GetComponent<DeviceDefinition>();
        job = device.GetComponent<JobBase>();
        DeviceFault f = definition.GetFault(fault);
        definition.ApplyFault(fault);
        job.Configure(new Job
        {
            kind = JobKind.Repair, devicePrefab = prefab, deviceName = definition.displayName, faultIndex = fault,
            faultType = f.type, faultDescription = f.description, payout = f.payout, number = 90 + (int)repair, color = Color.white
        });
        yield return Frames(2);

        // The input: the pad (its touch makes it the one in hand), or the virtual mouse with his own switched off.
        yield return UseInput(withPad);
        if (!carry.TryPickUp(job)) { Check(false, $"{log.Label}: Ace couldn't take the device"); yield break; }
        view.SetFirstPerson(true);
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Seconds(.3f);
        bench.Interact(interactor);
        yield return Seconds(.3f);
        if (!StationInteractable.BenchHolds(job) || !inspector.BeginInspection(job))
        {
            Check(false, $"{log.Label}: the close-up didn't open on it");
            yield break;
        }
        yield return AfterBlend();
        cam = Camera.main;
        if (usePad && !PadCursor.IsActive) { yield return Frames(3); }
        cursor = usePad ? PadCursor.Position : new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f);
        lastSent = cursor;

        int guardBefore = BenchStage.GuardTrips, errorsBefore = errors;
        int bouncesBefore = Sfx.Requested("screw.bounce"), wipesBefore = Sfx.Requested("cloth.wipe");
        runStarted = Time.time;
        lastFrameEnd = Time.realtimeSinceStartup;
        watching = true;
        yield return Work(log, Bar[(int)repair]);
        watching = false;
        log.finished = job != null && job.IsFinished;
        log.grade = job != null ? job.Grade.ToString() : "-";
        log.guardTrips = BenchStage.GuardTrips - guardBefore;
        log.errors = errors - errorsBefore;
        log.bounces = Sfx.Requested("screw.bounce") - bouncesBefore;
        log.wipes = Sfx.Requested("cloth.wipe") - wipesBefore;
        yield return Photo($"{(int)repair}{(withPad ? "p" : "m")}{rate}-{seed}-end");

        // Hands off, step back, the device goes.
        LetGoOfEverything();
        Send();
        yield return Frames(2);
        if (inspector.IsHoldingItem) inspector.CancelInspection();
        yield return AfterBlend();
        foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        made.Remove(device);
        Destroy(device);
        device = null;
        job = null;
        yield return Frames(2);
        Application.targetFrameRate = targetWas;
        Note($"{log.Label}: {(log.finished ? $"finished in {log.seconds:0.0} s, {log.grade}" : "NOT finished")}; lost {log.lost.Count}, hidden {log.hidden.Count}; {log.steps.Count} steps");
    }

    // The fault each repair plays: the camera's clean and its swap (faults 0 and 1, the days builder's split); the phone's
    // cracked screen; the watch's seized movement.
    static int FaultFor(Repair repair, DeviceDefinition definition)
    {
        if (definition == null || definition.faults == null || definition.faults.Length == 0) return -1;
        switch (repair)
        {
            case Repair.CameraClean: return 0;
            case Repair.CameraSwap: return definition.faults.Length >= 2 ? 1 : -1;
            case Repair.PhoneScreen: return Find(definition, "crack", "screen");
            default: return Find(definition, "seiz", "spring");
        }
    }

    static int Find(DeviceDefinition definition, params string[] words)
    {
        for (int i = 0; i < definition.faults.Length; i++)
        {
            string d = (definition.faults[i]?.description ?? "").ToLowerInvariant();
            foreach (string w in words) if (d.Contains(w)) return i;
        }
        return -1;
    }

    // The lab's hand on the clock: a whole day ahead of each run (16 runs outlast a 180 s day; the day must not end mid-run).
    void KeepTheDayOpen()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) return;
        try
        {
            System.Reflection.PropertyInfo remaining = typeof(DayClock).GetProperty("TimeRemaining");
            remaining?.GetSetMethod(true)?.Invoke(clock, new object[] { Mathf.Max(clock.TimeRemaining, 600f) });
        }
        catch (Exception e) { Note("couldn't keep the day open: " + e.Message); }
    }

    // ---------- the input path ----------

    IEnumerator UseInput(bool withPad)
    {
        LetGoOfEverything();
        if (withPad)
        {
            // The real mouse back (a pad run needs nothing of it) and the virtual one out; the pad in, touched once.
            BackOn();
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            PlugInPad("Clumsy hand pad");
            yield return PadPress(GamepadButton.LeftShoulder);   // before the close-up: the game takes it as the one in hand
            yield return Frames(2);
            Check(PadInput.UsingPad, $"{run.Label}: the pad is the one in hand");
        }
        else
        {
            // The pad out; a virtual mouse (and keyboard, for R) made current, and every other mouse switched off for the run,
            // so a nudge of his own can't take Mouse.current (restored in Restore).
            if (pad != null && pad.added) { InputSystem.RemoveDevice(pad); pad = null; }
            if (mouse == null) mouse = InputSystem.AddDevice<Mouse>("Clumsy hand mouse");
            if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>("Clumsy hand keyboard");
            foreach (InputDevice d in InputSystem.devices)
                if (d is Mouse && d != mouse && d.enabled) { InputSystem.DisableDevice(d); switchedOff.Add(d); }
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            cursor = new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f);
            lastSent = cursor + new Vector2(30f, 0f);   // a first move: the game reads the mouse as the device in hand
            Send();
            yield return Frames(3);
            Check(!PadInput.UsingPad && Mouse.current == mouse, $"{run.Label}: the virtual mouse is the one in hand (Mouse.current)");
        }
    }

    void BackOn()
    {
        foreach (InputDevice d in switchedOff) if (d != null) InputSystem.EnableDevice(d);
        switchedOff.Clear();
    }

    void LetGoOfEverything()
    {
        left = keyR = aDown = rtDown = lbDown = rbDown = yDown = dpadRight = false;
        stick = Vector2.zero;
    }

    // Queues the hand's input for this frame: the mouse (its position with the hand's tremor, its delta, its button) and R,
    // or the pad (the hand's stick with its noise, the buttons).
    void Send()
    {
        float dt = Time.unscaledDeltaTime;
        if (!usePad)
        {
            if (mouse == null) return;
            Vector2 at = cursor + hand.Tremor(dt);
            at.x = Mathf.Clamp(at.x, 0f, UnityEngine.Screen.width - 1f);
            at.y = Mathf.Clamp(at.y, 0f, UnityEngine.Screen.height - 1f);
            var state = new MouseState { position = at, delta = at - lastSent }.WithButton(MouseButton.Left, left);
            InputSystem.QueueStateEvent(mouse, state);
            lastSent = at;
            if (keyboard != null) InputSystem.QueueStateEvent(keyboard, keyR ? new KeyboardState(Key.R) : new KeyboardState());
        }
        else
        {
            if (pad == null) return;
            Vector2 s = stick + hand.StickNoise();
            if (s.magnitude > 1f) s.Normalize();
            var state = new GamepadState { rightStick = s, rightTrigger = rtDown ? 1f : 0f }
                .WithButton(GamepadButton.South, aDown).WithButton(GamepadButton.LeftShoulder, lbDown).WithButton(GamepadButton.RightShoulder, rbDown)
                .WithButton(GamepadButton.North, yDown).WithButton(GamepadButton.DpadRight, dpadRight);
            InputSystem.QueueStateEvent(pad, state);
        }
    }

    // One frame of the hand: its input sent, the frame passes.
    IEnumerator Frame()
    {
        Send();
        yield return null;
    }

    // Holding still (thinking, watching): the hand rests where it is.
    IEnumerator Idle(float seconds)
    {
        float until = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
        stick = Vector2.zero;
        while (Time.realtimeSinceStartup < until) yield return Frame();
    }

    Vector2 CursorNow() => usePad ? PadCursor.Position : cursor;
    static float Scale => Mathf.Max(.5f, UnityEngine.Screen.height / 1080f);

    // ---------- the hand's reach: the mouse's two submovements, or the stick's pushes ----------

    // Moves to a point on screen. With the mouse, a near-miss plan stops at the primary submovement's end (the press fires
    // there; FinishMove completes the correction). With the pad, a near miss presses as the stick lets go, before the snap's
    // glide lands.
    IEnumerator Move(Vector2 to, float visualWidth)
    {
        nearMissNow = false;
        if (!usePad)
        {
            reach = hand.PlanReach(cursor, to, visualWidth);
            reachStarted = Time.realtimeSinceStartup;
            reachLeft = false;
            while (true)
            {
                float t = Time.realtimeSinceStartup - reachStarted;
                if (reach.nearMiss && t >= reach.primary)
                {
                    cursor = reach.aimEnd;
                    nearMissNow = true;
                    reachLeft = true;
                    yield break;
                }
                cursor = ClumsyHand.At(reach, t);
                if (t >= reach.Seconds) { cursor = reach.end; yield break; }
                yield return Frame();
            }
        }
        stickReach = hand.PlanStick(to);
        bool early = hand.NearMiss();
        float giveUp = Time.realtimeSinceStartup + 8f;
        while (!stickReach.Done && Time.realtimeSinceStartup < giveUp)
        {
            stick = stickReach.Update(PadCursor.Position, Time.realtimeSinceStartup, Time.unscaledDeltaTime, Scale, PadCursor.Gliding);
            if (early && stickReach.JustLetGo && PadCursor.Gliding) { nearMissNow = true; stick = Vector2.zero; yield break; }
            yield return Frame();
        }
        stick = Vector2.zero;
    }

    // The rest of a reach that stopped early for a near-miss press (the button may be down).
    IEnumerator FinishMove()
    {
        if (usePad || !reachLeft) yield break;
        while (true)
        {
            float t = Time.realtimeSinceStartup - reachStarted;
            cursor = ClumsyHand.At(reach, t);
            if (t >= reach.Seconds) { cursor = reach.end; break; }
            yield return Frame();
        }
        reachLeft = false;
    }

    // ---------- presses ----------

    void Down()
    {
        if (!usePad) { left = true; return; }
        holdOnA = hand.PadHoldsA();
        if (holdOnA) aDown = true; else rtDown = true;
    }

    // The release: the cursor slips 4-12 px over its last two frames (a pad: a nudge of 0.15), then the button comes up.
    IEnumerator Up()
    {
        if (!usePad)
        {
            Vector2 slip = hand.Slip();
            cursor += slip * .5f;
            yield return Frame();
            cursor += slip * .5f;
            yield return Frame();
            left = false;
        }
        else
        {
            Vector2 nudge = hand.Slip().normalized * .15f;
            stick = nudge;
            yield return Frame();
            yield return Frame();
            stick = Vector2.zero;
            aDown = rtDown = false;
        }
        yield return Frame();
    }

    IEnumerator Click(float seconds)
    {
        Down();
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return Frame();
        yield return Up();
    }

    IEnumerator PadButton(Action<bool> set, float seconds)
    {
        set(true);
        float until = Time.realtimeSinceStartup + Mathf.Max(seconds, .05f);
        while (Time.realtimeSinceStartup < until) yield return Frame();
        set(false);
        yield return Frame();
        yield return Frame();
    }

    // ---------- the work: what a player does, step by step ----------

    IEnumerator Work(RunLog log, float bar)
    {
        float giveUp = Time.time + bar * 3f + 20f;
        StepLog step = null;
        int lastIndex = -2;
        string lastVerb = null;
        var verbsSeen = new HashSet<string>();
        bool wrongToolTried = false, nextPartTried = false;
        while (Time.time < giveUp && job != null)
        {
            if (job.IsFinished)
            {
                if (log.seconds < 0f) log.seconds = Time.time - runStarted;
                break;
            }
            RepairChecklist.Step now = inspector.CurrentStep;
            int index = inspector.CurrentStepIndex;
            string verb = now != null ? now.verb : null;
            if (index != lastIndex || verb != lastVerb)
            {
                // The step just ticked: its time, and a photo of the moment.
                if (step != null && step.took < 0f)
                {
                    step.took = Time.time - step.started;
                    yield return Photo($"{(int)run.repair}{(run.pad ? "p" : "m")}{run.fps}-{run.seed}-step{log.steps.Count:00}-{Safe(step.verb)}");
                }
                lastIndex = index;
                lastVerb = verb;
                step = null;
                if (now == null) { yield return Idle(.2f); continue; }
                step = new StepLog { verb = now.verb, index = index, started = Time.time };
                log.steps.Add(step);
                wrongToolTried = nextPartTried = false;
                mark = "step " + now.verb;
                // Thinking: a new step's reaction (longer the first time its verb shows).
                yield return Idle(hand.Reaction(!verbsSeen.Contains(now.verb ?? "")));
                verbsSeen.Add(now.verb ?? "");
                continue;
            }
            if (now == null) { yield return Idle(.2f); continue; }

            // Face down: the card says flip (R, or Y on a pad).
            if (now.under) { yield return FlipIt(); continue; }

            // Its tool, if it isn't in the hand.
            if (now.tool != ToolType.Hand && inspector.CurrentTool != now.tool)
            {
                bool wrong = !wrongToolTried && hand.WrongTool();
                wrongToolTried = true;
                yield return TakeTool(now.tool, step, wrong);
                yield return Idle(hand.Reaction(false) * .5f);
                continue;
            }

            // A pad sometimes asks the D-pad for "next part" (25 % of new steps).
            if (usePad && !nextPartTried && now.targets != null && now.targets.Count > 0 && !(now.targets[0] is GrimeSpot))
            {
                nextPartTried = true;
                if (hand.PadUsesNextPart())
                {
                    yield return PadButton(v => dpadRight = v, hand.ClickSeconds());
                    float until = Time.realtimeSinceStartup + .6f;
                    while (PadCursor.Gliding && Time.realtimeSinceStartup < until) yield return Frame();
                }
            }

            Component target = NextTarget(now);
            if (target == null) { yield return Idle(.15f); continue; }
            if (target is GrimeSpot spot) yield return ScrubSpot(spot, step);
            else yield return HoldOn(target, step);
            yield return Idle(hand.Reaction(false) * .6f);   // a beat between targets of one step
        }
        if (step != null && step.took < 0f) step.took = Time.time - step.started;
        if (job != null && job.IsFinished && log.seconds < 0f) log.seconds = Time.time - runStarted;
    }

    static string Safe(string s) => string.IsNullOrEmpty(s) ? "step" : new string(s.Where(char.IsLetterOrDigit).ToArray());

    // The step's target the hand goes to: the one nearest the cursor among those it can see glowing.
    Component NextTarget(RepairChecklist.Step step)
    {
        if (step.targets == null) return null;
        Component best = null;
        float bestD = float.PositiveInfinity;
        Vector2 at = CursorNow();
        foreach (Component t in step.targets)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (!OnScreen(t, out Vector2 s, out _)) continue;
            float d = (s - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // A target's middle on screen and its visual width (what the eye aims at: a screw's head, a spot's patch, a part's
    // narrower side), pixels.
    bool OnScreen(Component target, out Vector2 centre, out float width)
    {
        centre = default;
        width = 10f;
        if (target == null || cam == null) return false;
        Vector3 middle;
        float radius;
        switch (target)
        {
            case GrimeSpot spot: middle = spot.Centre; radius = spot.Radius; break;
            case Screw screw: middle = screw.HeadPoint; radius = screw.HeadRadius; break;
            case ScrewSocket hole: middle = hole.WorkPoint; radius = hole.Screw != null ? hole.Screw.HeadRadius * ScrewSocket.DecalPerHead : .002f; break;
            default:
                if (DeviceBounds.TryGet(target.gameObject, out Bounds b))
                {
                    middle = b.center;
                    Vector3 e = b.extents;
                    radius = Mathf.Min(e.x, Mathf.Min(e.y, e.z)) > .001f ? Mathf.Min(Mathf.Max(e.x, e.z), Mathf.Max(Mathf.Min(e.x, e.z), e.y)) : Mathf.Max(e.x, e.z);
                }
                else { middle = BenchInteractable.BoundsCentre(target.transform); radius = .01f; }
                break;
        }
        Vector3 s = cam.WorldToScreenPoint(middle);
        if (s.z <= 0f) return false;
        centre = s;
        Vector3 edge = cam.WorldToScreenPoint(middle + cam.transform.right * radius);
        width = Mathf.Max(4f, 2f * ((Vector2)edge - centre).magnitude);
        return centre.x >= 0f && centre.y >= 0f && centre.x <= UnityEngine.Screen.width && centre.y <= UnityEngine.Screen.height;
    }

    // ---------- the verbs, as a hand does them ----------

    // Over it goes: R (Y on a pad), then a look.
    IEnumerator FlipIt()
    {
        mark = "flip";
        if (usePad) yield return PadButton(v => yDown = v, hand.ClickSeconds());
        else
        {
            keyR = true;
            float until = Time.realtimeSinceStartup + hand.ClickSeconds();
            while (Time.realtimeSinceStartup < until) yield return Frame();
            keyR = false;
            yield return Frame();
        }
        float giveUp = Time.realtimeSinceStartup + 1.5f;
        while ((inspector.Flipping || Time.realtimeSinceStartup < giveUp - 1.3f) && Time.realtimeSinceStartup < giveUp) yield return Frame();
        yield return Idle(hand.Reaction(false));
    }

    // The tool the step wants into the hand. The mouse clicks it on the rest (15 % of new steps the neighbour first, put right
    // once the hover shows it); a pad uses LB / RB (70 %; 15 % of new steps one press off) or the cursor and A.
    IEnumerator TakeTool(ToolType want, StepLog step, bool wrongFirst)
    {
        mark = "take " + want;
        if (!usePad || !hand.PadPicksByShoulders())
        {
            if (wrongFirst)
            {
                ToolPickup neighbour = Neighbour(want);
                if (neighbour != null)
                {
                    yield return ClickOn(neighbour, step);
                    yield return Idle(hand.Notice());
                    step.retries++;
                }
            }
            ToolPickup tool = RestingTool(want);
            if (tool == null) { yield return Idle(.3f); yield break; }
            yield return ClickOn(tool, step);
            yield break;
        }
        int presses = ShoulderPresses(want);
        if (wrongFirst && presses != 0)
        {
            int off = presses + Math.Sign(presses) * hand.Miscount();
            presses = off == 0 ? Math.Sign(presses) * 2 : off;
            step.retries++;
        }
        for (int i = 0; i < Mathf.Abs(presses); i++)
        {
            if (presses > 0) yield return PadButton(v => rbDown = v, hand.ClickSeconds());
            else yield return PadButton(v => lbDown = v, hand.ClickSeconds());
            yield return Idle(.08f);
        }
        if (inspector.CurrentTool != want) yield return Idle(hand.Notice());   // the hover shows the wrong one: the loop puts it right
    }

    // A click on a tool at rest (the mouse's reach, or the pad's stick and A): in the hand on the press (a tap keeps it).
    IEnumerator ClickOn(ToolPickup tool, StepLog step)
    {
        if (!OnScreen(tool, out Vector2 at, out float width)) yield break;
        yield return Move(at, width);
        if (nearMissNow) yield return FinishMove();   // a tool's tap: the correction first (a near miss there would only pick nothing)
        int before = inspector.Presses;
        yield return Click(hand.ClickSeconds());
        step.presses += inspector.Presses - before;
        if (!inspector.LastPressActed) step.misses++;
    }

    // The tools on the rest in the order LB / RB cycles them (bare hands first), and how many presses (RB +, LB -) take the
    // hand from what it holds to the one wanted, the shorter way round.
    int ShoulderPresses(ToolType want)
    {
        var order = new List<ToolType> { ToolType.Hand };
        foreach (ToolType t in Enum.GetValues(typeof(ToolType)))
            if (t != ToolType.Hand && RestingOrHeld(t)) order.Add(t);
        int from = order.IndexOf(inspector.CurrentTool), to = order.IndexOf(want);
        if (from < 0 || to < 0) return 0;
        int forward = (to - from + order.Count) % order.Count, back = forward - order.Count;
        return forward <= -back ? forward : back;
    }

    static bool RestingOrHeld(ToolType t) => FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).Any(p => p.tool == t);

    static ToolPickup RestingTool(ToolType t) => FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).FirstOrDefault(p => p.tool == t && !p.InHand);

    // The tool lying next to the one wanted on the rest (by its seat's order): the one a hand takes by mistake.
    ToolPickup Neighbour(ToolType want)
    {
        int w = (int)want;
        foreach (int d in new[] { 1, -1 })
        {
            int n = w + d;
            if (n <= 0 || n >= Enum.GetValues(typeof(ToolType)).Length) continue;
            ToolPickup p = RestingTool((ToolType)n);
            if (p != null) return p;
        }
        return null;
    }

    // A hold on a part: the reach, the press (maybe early: a near miss), held until the hand sees it done and 0.2 s more; 10 %
    // let go early at 40-80 % of the need and hold again 0.3 s later. A press that took nothing is a miss: the hand lets go,
    // finishes its reach and the loop comes back to it.
    IEnumerator HoldOn(Component target, StepLog step)
    {
        if (!OnScreen(target, out Vector2 at, out float width)) { yield return Idle(.2f); yield break; }
        mark = "reach " + target.name;
        yield return Move(at, width);
        bool near = nearMissNow;
        yield return Frame();                      // the game sees the cursor where the hand stopped (the hover the eye reads)
        Vector2 pressedAt = CursorNow();
        Component hovered = inspector.Hovered;
        Func<bool> done = DoneTest(target);
        int before = inspector.Presses;
        Down();
        mark = (near ? "near-miss press " : "press ") + target.name;
        yield return Frame();
        yield return Frame();
        step.presses += inspector.Presses - before;
        bool onIt = Same(inspector.HoldTarget, target) || Same(inspector.LastPressTarget, target) || Safe(done);
        bool acted = inspector.LastPressActed && onIt;
        if (near)
        {
            step.nearMisses++;
            OnScreen(target, out Vector2 now, out _);
            step.nearPx.Add((pressedAt - now).magnitude / Scale);
            if (Same(hovered, target)) { step.nearInside++; if (acted) step.nearActed++; }
        }
        if (!acted)
        {
            step.misses++;
            yield return Up();
            if (near) yield return FinishMove();
            yield return Idle(hand.Reaction(false) * .5f);
            yield break;
        }
        // Held: the rest of an early reach goes on under the hold (the hold owns its target, wherever the hand drifts).
        if (near) yield return FinishMove();
        bool early = hand.LetsGoEarly();
        float earlyAt = hand.EarlyAt();
        float giveUp = Time.realtimeSinceStartup + 10f;
        while (!Safe(done) && Time.realtimeSinceStartup < giveUp)
        {
            if (!usePad) cursor += hand.Drift(Time.unscaledDeltaTime);
            float progress = Progress(target);
            if (early && progress >= earlyAt)
            {
                early = false;
                mark = "let go early " + target.name;
                yield return Up();
                step.retries++;
                yield return Idle(.3f);
                float kept = Progress(target);
                // A reset is the work gone back to nothing (the pry's designed slip of 0.35 and a pinch's slow decay aren't).
                if (progress > .2f && kept < .02f && !Safe(done)) run.resets++;
                if (Safe(done)) break;
                before = inspector.Presses;
                Down();
                mark = "hold again " + target.name;
                yield return Frame();
                yield return Frame();
                step.presses += inspector.Presses - before;
                if (!inspector.LastPressActed) { step.misses++; yield return Up(); yield break; }
                continue;
            }
            yield return Frame();
        }
        // Seen done: 0.2 s more, then let go.
        float more = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < more) yield return Frame();
        yield return Up();
    }

    // When the hand sees the target done: a screw out; a hole's screw home; a cover popped (or home, from its well); the
    // broken part out; the fresh part in.
    Func<bool> DoneTest(Component target)
    {
        switch (target)
        {
            case Screw screw: return () => screw == null || screw.IsOut;
            case ScrewSocket hole: { Screw s = hole.Screw; return () => s == null || !s.IsOut; }
            case RemovablePart cover:
                if (cover.State == RemovablePart.CoverState.Seated) return () => cover == null || cover.IsPopped || cover.IsRemoved;
                return () => cover == null || cover.State == RemovablePart.CoverState.Seated || cover.Flying;
            case ReplaceablePart part: return () => part == null || part.State != ReplaceablePart.PartState.InPlace;
            case LoosePart fresh: { ReplaceablePart p = fresh.Part; return () => p == null || p.IsReplaced; }
            case BenchInteractable b: return () => b == null || !b.CanInteract;
        }
        return () => true;
    }

    // What the hand sees of a hold's progress (the screw backing out, the pry going in, the pinch, the press-in).
    float Progress(Component target)
    {
        BenchInteractable part = target is Screw screw ? screw.GetComponent<ScrewTarget>() : target as BenchInteractable;
        if (inspector.HoldTarget != null) return inspector.HoldTarget.HoldProgress;
        return part != null ? part.HoldProgress : 0f;
    }

    static bool Same(Component a, Component b)
    {
        if (a == null || b == null) return false;
        return a == b || a.gameObject == b.gameObject;
    }

    // Scrubbing a spot: back and forth across it until it is gone, and 0.25 s more.
    IEnumerator ScrubSpot(GrimeSpot spot, StepLog step)
    {
        if (!OnScreen(spot, out Vector2 at, out float width)) { yield return Idle(.2f); yield break; }
        mark = "reach " + spot.name;
        yield return Move(at, width);
        bool near = nearMissNow;
        yield return Frame();
        Component hovered = inspector.Hovered;
        int before = inspector.Presses;
        Down();
        mark = (near ? "near-miss press " : "press ") + spot.name;
        yield return Frame();
        step.presses += inspector.Presses - before;
        if (near)
        {
            step.nearMisses++;
            step.nearPx.Add((CursorNow() - at).magnitude / Scale);
            if (Same(hovered, spot)) { step.nearInside++; if (inspector.LastPressActed) step.nearActed++; }
            yield return FinishMove();
        }
        var scrub = hand.PlanScrub(at, width * .5f, Time.realtimeSinceStartup, Scale);
        float goneAt = -1f, giveUp = Time.realtimeSinceStartup + 15f;
        bool counted = false;
        // Call 23 (QA M3): over the wipe's middle, how much of the spot the held cloth leaves in sight, frame by frame.
        bool clothWipe = spot != null && spot.Tool == ToolType.Cloth;
        float seenSum = 0f;
        int seenFrames = 0;
        while (Time.realtimeSinceStartup < giveUp)
        {
            bool gone = spot == null || spot.IsClean;
            if (gone)
            {
                if (!counted)
                {
                    // T6 counts the hand's own strokes (its half-cycles) until the spot came clean: GrimeSpot.Passes misses a
                    // stroke's start and the hand's sideways turn (review-bench M3, 14 Oct).
                    float hz = usePad ? ClumsyHand.PadWaggleHz : ClumsyHand.ScrubHz;
                    run.strokes.Add(Mathf.FloorToInt((Time.realtimeSinceStartup - scrub.StartedAt) * 2f * hz) + 1);
                    counted = true;
                }
                if (goneAt < 0f) goneAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - goneAt >= .25f) break;
            }
            else if (OnScreen(spot, out Vector2 c, out _)) scrub.Centre = c;    // the hand follows the spot it sees
            if (clothWipe && !gone && spot.Progress >= .25f && spot.Progress <= .75f && ClothPressed(out ToolPickup cloth))
            {
                float seen = SpotInSight(spot, cloth);
                seenSum += seen;
                seenFrames++;
                run.clothSeenWorst = Mathf.Min(run.clothSeenWorst, seen);
                if (!run.clothPhoto && spot.Progress >= .45f)
                {
                    run.clothPhoto = true;
                    yield return Photo($"{(int)run.repair}{(run.pad ? "p" : "m")}{run.fps}-{run.seed}-mid-wipe-{Safe(spot.name)}-{seen * 100f:0}pct");
                }
            }
            Vector2 dirt = spot != null && !gone ? (Vector2)cam.WorldToScreenPoint(spot.DirtLeftCentre) : scrub.Centre;
            if (usePad) stick = scrub.PadStick(Time.realtimeSinceStartup, PadCursor.Position);
            else cursor = scrub.At(Time.realtimeSinceStartup, dirt);
            mark = "";
            yield return Frame();
        }
        if (clothWipe)
        {
            run.clothWipes++;
            if (seenFrames > 0) run.clothSeen.Add(seenSum / seenFrames);
            else run.clothUnmeasured++;   // it went from under a quarter clean to more than three quarters in one frame
        }
        if (!counted) Note($"{run.Label}: {spot?.name ?? "a spot"} wasn't clean after 15 s of scrubbing");
        stick = Vector2.zero;
        yield return Up();
    }

    // ---------- the cloth shows what it cleans (the Producer's call 23; QA M3) ----------

    ToolPickup clothTool;
    Mesh clothBaked;
    readonly List<Vector3> clothVerts = new List<Vector3>();

    // The cloth in the hand, pressed on the device this frame (its Press key near full: rubbing, not hovering over it).
    bool ClothPressed(out ToolPickup cloth)
    {
        if (clothTool == null)
            clothTool = FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.tool == ToolType.Cloth && !t.IsStandIn);
        cloth = clothTool;
        return cloth != null && cloth.InHand && !cloth.Returning && cloth.PressShown > .8f;
    }

    // The share of the spot's disc (its real disc on its plane, sampled evenly by area) outside the held cloth's box on screen.
    float SpotInSight(GrimeSpot spot, ToolPickup cloth)
    {
        if (cam == null || !ClothOnScreen(cloth, out Rect box)) return 1f;
        Vector3 n = spot.Normal;
        Vector3 a = Vector3.ProjectOnPlane(cam.transform.right, n);
        if (a.sqrMagnitude < 1e-8f) a = Vector3.ProjectOnPlane(cam.transform.up, n);
        a.Normalize();
        Vector3 b = Vector3.Cross(n, a);
        float r = spot.Radius;
        int seen = 0, all = 0;
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 24; j++)
            {
                float rr = r * Mathf.Sqrt((i + .5f) / 8f), th = (j + .5f) / 24f * 2f * Mathf.PI;
                Vector3 s = cam.WorldToScreenPoint(spot.Centre + (a * Mathf.Cos(th) + b * Mathf.Sin(th)) * rr);
                if (s.z <= 0f) continue;
                all++;
                if (!box.Contains(new Vector2(s.x, s.y))) seen++;
            }
        return all > 0 ? seen / (float)all : 1f;
    }

    // The held cloth's box on screen: every drawn vertex of it (a keyed mesh baked at this frame's keys, at scale 1, then
    // through its own transform, mirror and all), the outline's hulls left out.
    bool ClothOnScreen(ToolPickup cloth, out Rect box)
    {
        box = default;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        bool any = false;
        foreach (Renderer r in cloth.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.GetComponent<BenchOutlineHull>() != null) continue;
            clothVerts.Clear();
            if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                if (clothBaked == null) clothBaked = new Mesh { name = "Clumsy hand: the cloth as drawn", hideFlags = HideFlags.HideAndDontSave };
                skinned.BakeMesh(clothBaked, false);
                clothBaked.GetVertices(clothVerts);
            }
            else
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (filter.sharedMesh.isReadable) filter.sharedMesh.GetVertices(clothVerts);
                else
                {
                    Bounds lb = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                        clothVerts.Add(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                }
            }
            foreach (Vector3 v in clothVerts)
            {
                Vector3 s = cam.WorldToScreenPoint(r.transform.TransformPoint(v));
                if (s.z <= 0f) continue;
                x0 = Mathf.Min(x0, s.x); y0 = Mathf.Min(y0, s.y); x1 = Mathf.Max(x1, s.x); y1 = Mathf.Max(y1, s.y);
                any = true;
            }
        }
        if (any) box = Rect.MinMaxRect(x0, y0, x1, y1);
        return any;
    }

    // ---------- watching every frame: pieces lost or hidden, the timeline, the frame time and its cap ----------

    readonly RaycastHit[] sight = new RaycastHit[16];
    readonly List<Component> pieces = new List<Component>();

    void LateUpdate()
    {
        if (!watching || run == null) return;
        run.frames.Add(Time.unscaledDeltaTime);
        if (job != null && cam != null && inspector != null && inspector.IsHoldingItem) WatchPieces();
        WriteRow();
        Cap();
    }

    void WatchPieces()
    {
        BenchStage stage = BenchStage.Instance;
        pieces.Clear();
        foreach (Screw s in job.GetComponentsInChildren<Screw>()) pieces.Add(s);
        foreach (RemovablePart c in job.GetComponentsInChildren<RemovablePart>()) pieces.Add(c);
        foreach (GameObject loose in job.LooseParts)
        {
            if (loose == null) continue;
            foreach (Screw s in loose.GetComponentsInChildren<Screw>()) if (!pieces.Contains(s)) pieces.Add(s);
            foreach (RemovablePart c in loose.GetComponentsInChildren<RemovablePart>()) if (!pieces.Contains(c)) pieces.Add(c);
            foreach (ReplaceablePart p in loose.GetComponentsInChildren<ReplaceablePart>()) if (!pieces.Contains(p)) pieces.Add(p);
            foreach (LoosePart f in loose.GetComponentsInChildren<LoosePart>()) if (!pieces.Contains(f)) pieces.Add(f);
        }
        foreach (Component piece in pieces)
        {
            if (piece == null || !piece.gameObject.activeInHierarchy) continue;
            if (stage != null && stage.IsFlying(piece)) continue;          // flights are under 0.6 s and end in view
            bool atRest;
            bool valid = State(piece, stage, out atRest);
            if (!valid) { run.lostFrames++; run.lost.Add(piece.name); mark = "LOST " + piece.name; continue; }
            if (!atRest) continue;
            if (Hidden(piece)) { run.hiddenFrames++; if (run.hidden.Add(piece.name)) mark = "HIDDEN " + piece.name; }
        }
    }

    // A piece's state: valid or not (lost), and whether it is at rest off the device (in its place, or hovering over its seat).
    static bool State(Component piece, BenchStage stage, out bool atRest)
    {
        atRest = false;
        switch (piece)
        {
            case Screw s:
                if (!s.IsOut) return true;
                if (s.Flying) return true;
                atRest = s.InCup || s.Fetched;
                return s.InCup || s.Fetched || s.IsBusy;
            case RemovablePart c:
                if (c.State == RemovablePart.CoverState.Seated || c.State == RemovablePart.CoverState.Popped) return true;
                if (c.State == RemovablePart.CoverState.Held) return false;            // "flying" with no flight
                atRest = c.InWell;
                return true;
            case ReplaceablePart p:
                if (p.State == ReplaceablePart.PartState.InPlace) return true;
                if (p.State == ReplaceablePart.PartState.HeldBroken) return false;    // lifting, with no flight
                atRest = stage == null || stage.PlaceOf(p) != null;
                return stage == null || stage.PlaceOf(p) != null || p.IsReplaced && p.transform.parent != null;
            case LoosePart f:
                atRest = f.OnPad || f.Hovering;
                return f.OnPad || f.Hovering || f.Pressing || f.Flying;
        }
        return true;
    }

    // Out of the frame (unless zoomed past 0.3), or something other than itself (and the tool in the hand) nearer the camera on
    // the line to its middle, and on the line to its side toward the camera too. The second line since 15 Oct: a screw fetched
    // back over its hole sits with its shank in the hole, the middle of its bounds under the plate's top, and the watch's four
    // counted "hidden" for 24 frames as they went back in, their heads in plain view.
    bool Hidden(Component piece)
    {
        Renderer r = null;
        foreach (Renderer x in piece.GetComponentsInChildren<Renderer>()) if (x.enabled && x.GetComponent<BenchOutlineHull>() == null) { r = x; break; }
        Vector3 middle = r != null ? r.bounds.center : piece.transform.position;
        Vector3 v = cam.WorldToViewportPoint(middle);
        bool inFrame = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
        if (!inFrame) return inspector.Zoom <= .3f;
        if (!Blocked(piece, middle)) return false;
        if (r == null) return true;
        Vector3 toCamera = (cam.transform.position - middle).normalized, e = r.bounds.extents;
        float exit = Mathf.Min(e.x / Mathf.Max(1e-6f, Mathf.Abs(toCamera.x)), Mathf.Min(e.y / Mathf.Max(1e-6f, Mathf.Abs(toCamera.y)), e.z / Mathf.Max(1e-6f, Mathf.Abs(toCamera.z))));
        return Blocked(piece, middle + toCamera * (exit * .9f));
    }

    // Something other than the piece itself (and the tool in the hand, and anything flying) on the line from the camera to `point`.
    bool Blocked(Component piece, Vector3 point)
    {
        Vector3 from = cam.transform.position, to = point - from;
        float length = to.magnitude;
        if (length < 1e-5f) return false;
        int n = Physics.RaycastNonAlloc(from, to / length, sight, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = sight[i].collider;
            if (c == null || sight[i].distance > length - .003f) continue;
            if (c.transform.IsChildOf(piece.transform)) continue;
            if (c.GetComponentInParent<ToolPickup>() != null) continue;
            BenchStage stage = BenchStage.Instance;
            Component owner = c.GetComponentInParent<BenchInteractable>();
            if (owner != null && stage != null && stage.IsFlying(owner)) continue;
            return true;
        }
        return false;
    }

    void WriteRow()
    {
        if (timeline == null) return;
        Vector2 at = GamePointer.Position;
        string buttons = usePad ? (aDown ? "A" : "") + (rtDown ? "RT" : "") + (lbDown ? "LB" : "") + (rbDown ? "RB" : "") + (yDown ? "Y" : "") : (left ? "L" : "") + (keyR ? "R" : "");
        Component hovered = inspector != null ? inspector.Hovered : null;
        timeline.WriteLine($"{runs.Count},{Time.time - runStarted:0.000},{Time.frameCount},{at.x:0.0},{at.y:0.0},{buttons},{(hovered != null ? hovered.name.Replace(',', ' ') : "")},{mark.Replace(',', ' ')}");
        mark = "";
    }

    // The frame's cap: 60 fps, or 15 jittered to 12-18 (a sleep, whatever the editor does with targetFrameRate).
    void Cap()
    {
        float budget = 1f / Mathf.Max(1, fps);
        if (fps <= 15) budget += (float)(capJitter.NextDouble() * .027 - .011);
        float elapsed = Time.realtimeSinceStartup - lastFrameEnd;
        int ms = Mathf.FloorToInt((budget - elapsed) * 1000f);
        if (ms > 0) Thread.Sleep(ms);
        lastFrameEnd = Time.realtimeSinceStartup;
    }

    // ---------- the report ----------

    void Report()
    {
        var table = new StringBuilder();
        table.AppendLine();
        table.AppendLine("The runs side by side:");
        string Row(string name, Func<RunLog, string> cell) => $"{name,-28}" + string.Concat(runs.Select(r => $"{Clip(cell(r), 22),-24}"));
        table.AppendLine(Row("run", r => r.Label));
        table.AppendLine(Row("seed", r => r.seed.ToString()));
        table.AppendLine(Row("time / bar (s)", r => $"{(r.seconds >= 0f ? r.seconds.ToString("0.0") : "-")} / {Bar[(int)r.repair]:0}"));
        table.AppendLine(Row("finished, grade", r => $"{(r.finished ? "yes" : "NO")}, {r.grade}"));
        table.AppendLine(Row("lost / hidden pieces", r => $"{r.lost.Count} / {r.hidden.Count}"));
        table.AppendLine(Row("steps", r => r.steps.Count.ToString()));
        table.AppendLine(Row("presses / misses (worst)", r => $"{r.steps.Sum(s => s.presses)} / {r.steps.Sum(s => s.misses)} ({(r.steps.Count > 0 ? r.steps.Max(s => s.misses) : 0)})"));
        table.AppendLine(Row("near misses: in / acted", r => $"{r.steps.Sum(s => s.nearMisses)}: {r.steps.Sum(s => s.nearInside)} / {r.steps.Sum(s => s.nearActed)}"));
        table.AppendLine(Row("near-miss px (mean)", r => { var px = r.steps.SelectMany(s => s.nearPx).ToList(); return px.Count > 0 ? px.Average().ToString("0") : "-"; }));
        table.AppendLine(Row("retries / resets", r => $"{r.steps.Sum(s => s.retries)} / {r.resets}"));
        table.AppendLine(Row("strokes: median, max, ≤4", r => r.strokes.Count == 0 ? "-" : $"{Median(r.strokes)}, {r.strokes.Max()}, {r.strokes.Count(s => s <= 4) * 100 / r.strokes.Count}%"));
        table.AppendLine(Row("fps mean / p95 frame ms", r => r.frames.Count == 0 ? "-" : $"{1f / Mathf.Max(1e-4f, r.frames.Average()):0} / {P95(r.frames) * 1000f:0}"));
        table.AppendLine(Row("guard trips, errors", r => $"{r.guardTrips}, {r.errors}"));
        table.AppendLine(Row("screw.bounce, cloth.wipe", r => $"{r.bounces}, {r.wipes}"));
        table.AppendLine(Row("cloth: spot seen mid-wipe", r => r.clothWipes == 0 ? "-" : r.clothSeen.Count == 0 ? $"{r.clothWipes} unmeasured"
            : $"{r.clothSeen.Min() * 100f:0}% (worst frame {r.clothSeenWorst * 100f:0}%)"));
        foreach (RunLog r in runs)
        {
            table.AppendLine();
            table.AppendLine($"{r.Label} (seed {r.seed}){(r.note.Length > 0 ? ": " + r.note : "")}");
            foreach (StepLog s in r.steps)
                table.AppendLine($"    {s.index,2} {s.verb,-12} {(s.took >= 0f ? s.took.ToString("0.0") : "-"),6} s  presses {s.presses}, misses {s.misses}, retries {s.retries}, near misses {s.nearMisses} ({s.nearInside} in, {s.nearActed} acted{(s.nearPx.Count > 0 ? $", {string.Join("/", s.nearPx.Select(p => p.ToString("0")))} px" : "")})");
            if (r.lost.Count > 0) table.AppendLine("    lost: " + string.Join(", ", r.lost));
            if (r.hidden.Count > 0) table.AppendLine($"    hidden ({r.hiddenFrames} frames): " + string.Join(", ", r.hidden));
        }
        report.Append(table);

        // The bars, run by run.
        foreach (RunLog r in runs)
        {
            float bar = Bar[(int)r.repair];
            Check(r.finished && r.grade == JobGrade.Perfect.ToString() && r.seconds >= 0f && r.seconds <= bar,
                $"{r.Label}: finished, Perfect, within {bar:0} s ({(r.seconds >= 0f ? r.seconds.ToString("0.0") + " s" : "not finished")}, {r.grade})");
            Check(r.lost.Count == 0 && r.hidden.Count == 0, $"{r.Label}: no piece lost or hidden for a frame ({r.lost.Count} lost, {r.hidden.Count} hidden over {r.hiddenFrames} frames)");
            int nearIn = r.steps.Sum(s => s.nearInside), nearActed = r.steps.Sum(s => s.nearActed);
            Check(nearActed == nearIn, $"{r.Label}: every near-miss press inside a target's hit area acted on it ({nearActed} of {nearIn})");
            int worst = r.steps.Count > 0 ? r.steps.Max(s => s.misses) : 0;
            Check(worst <= 3, $"{r.Label}: no step had more than 3 misses (worst {worst})");
            Check(r.resets == 0, $"{r.Label}: early-released holds kept their progress ({r.resets} reset)");
            if (r.strokes.Count > 0)
            {
                float within4 = r.strokes.Count(s => s <= 4) / (float)r.strokes.Count;
                Check(Median(r.strokes) <= 3 && r.strokes.Max() <= 6 && within4 >= .9f,
                    $"{r.Label}: strokes per spot median {Median(r.strokes)} (≤ 3), most {r.strokes.Max()} (≤ 6), {within4:P0} within 4 (≥ 90 %)");
            }
            Check(r.guardTrips == 0 && r.errors == 0, $"{r.Label}: no guard trips ({r.guardTrips}), nothing logged an error ({r.errors})");
            Check(r.bounces == 0 && r.wipes == 0, $"{r.Label}: the retired cues stayed quiet (screw.bounce {r.bounces}, cloth.wipe {r.wipes})");
            if (r.clothWipes > 0)
            {
                // Call 23: the cloth shows what it cleans. A wipe that skipped its whole middle in one frame is noted, not failed.
                Check(r.clothSeen.Count > 0 && r.clothSeen.Min() >= .40f,
                    $"{r.Label}: the cloth shows what it cleans: over each wipe's middle at least 40 % of the spot in sight " +
                    $"({(r.clothSeen.Count > 0 ? string.Join(", ", r.clothSeen.Select(s => (s * 100f).ToString("0") + "%")) : "none measured")}; worst frame {r.clothSeenWorst * 100f:0}%)");
                if (r.clothUnmeasured > 0) Note($"{r.Label}: {r.clothUnmeasured} cloth wipe(s) passed their middle in a single frame (unmeasured)");
            }
            if (r.fps == 60 && r.frames.Count > 0 && r.frames.Average() > 1f / 40f) Note($"{r.Label}: not at 60 ({1f / r.frames.Average():0} fps mean): it must pass all the same");
        }
        // 60 against 15, step by step (the same seed: the same choices), within 25 % beyond whole frames.
        foreach (RunLog slow in runs.Where(r => r.fps == 15))
        {
            RunLog quick = runs.FirstOrDefault(r => r.fps == 60 && r.repair == slow.repair && r.pad == slow.pad && r.seed == slow.seed);
            if (quick == null || !quick.finished || !slow.finished) continue;
            int n = Mathf.Min(quick.steps.Count, slow.steps.Count);
            var off = new List<string>();
            for (int i = 0; i < n; i++)
            {
                float a = quick.steps[i].took, b = slow.steps[i].took;
                if (a < 0f || b < 0f) continue;
                float frames = 2f / 15f;
                if (Mathf.Abs(a - b) > .25f * Mathf.Max(a, b) + frames) off.Add($"{quick.steps[i].verb} {a:0.0}/{b:0.0} s");
            }
            Check(off.Count == 0, $"{slow.Label.Replace(" / 15 fps", "")}: 60 and 15 fps within 25 % per step beyond whole frames{(off.Count > 0 ? ": " + string.Join(", ", off) : "")}");
        }
    }

    static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";
    static int Median(List<int> xs) { var o = xs.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
    static float P95(List<float> xs) { var o = xs.OrderBy(x => x).ToList(); return o[Mathf.Clamp(Mathf.CeilToInt(o.Count * .95f) - 1, 0, o.Count - 1)]; }

    // ---------- the scene's hands ----------

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        view.LookTo(yaw, 10f);
    }

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    protected override void Restore()
    {
        watching = false;
        LetGoOfEverything();
        try { Send(); } catch (Exception) { }
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (job != null) foreach (DropSpot spot in FindObjectsByType<DropSpot>()) spot.Release(job);
        foreach (GameObject g in made) if (g != null) Destroy(g);
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        BackOn();
        PutSettingsBack();
        if (spawner != null) spawner.enabled = spawnerWas;
        if (timeline != null) { try { timeline.Flush(); timeline.Dispose(); } catch (Exception) { } timeline = null; }
        if (clothBaked != null) { Destroy(clothBaked); clothBaked = null; }
    }

    void PutSettingsBack()
    {
        if (!settingsTouched) return;
        settingsTouched = false;
        GameSettings.SwapBenchSticks = swapWas;
        AimAssist.Enabled = aimWas;
        QualitySettings.vSyncCount = vsyncWas;
        Application.targetFrameRate = targetWas;
    }

    // Play stopped mid-run (Restore runs only when the lab reaches its end): his own mouse back on, the lab's mouse and keyboard
    // out, the frame rate and the settings as they were, whatever happens. A quality setting changed in Play mode stays changed
    // in the editor, and a mouse left switched off would leave the next Play session deaf to his.
    protected override void OnDisable()
    {
        base.OnDisable();
        try { BackOn(); } catch (Exception) { }
        try { if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); } catch (Exception) { }
        mouse = null;
        try { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); } catch (Exception) { }
        keyboard = null;
        try { PutSettingsBack(); } catch (Exception) { }
        if (timeline != null) { try { timeline.Flush(); timeline.Dispose(); } catch (Exception) { } timeline = null; }
    }
}
