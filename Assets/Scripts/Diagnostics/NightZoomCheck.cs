using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE NIGHT ZOOM: A PLAY CHECK (14 Oct 2026, Cinematics; the spec's §4.3.4, feel target T20)
//
// His note (14 Oct): "There is also a weird camera close up bug at night when i zoom in in isometric view when talking to the
// random guy in the dumpster, i can zoom up pretty close to ace which is awesome but when i walk towards to graces house for
// example, the zoom INSTANTLY SHOOTS up ONLY if i try to zoom or touch the zoom button at all".
//
// Fixit Fidget > Night > Night zoom - from the bins to Grace's house (lab). A lab night (the man met, the night's clock held),
// a virtual pad driving Ace and the camera at whatever frame rate the machine gives (his Mac: 15-20 fps):
//   A. from far (40 m) into the bins view, the right stick held through the turn: the distance eases into 8-22 m (the old
//      code left it above 22 until a zoom press snapped it down);
//   B. at the bins, RT for 1 s: 8 m;
//   C. walking away toward Grace's pavement, the right stick held for 1 s from the frame the bins view ends: the distance
//      stays at 8 m (close zoom outside at night, everywhere);
//   D. at her pavement: RT (no change: 8 m is the night's minimum), LT for 0.5 s (about 11 m), R3 (home within 1.5 s);
//   E. into her house from 40 m with the right stick held through the turn: into 8-22 m within 1.5 s (skipped, with a note,
//      if her house or door isn't in the scene);
//   F. the day after the night: 24-48 m round the café's middle again, and R3 eases.
// Every frame is traced (Logs/Night/night-zoom-<time>/trace.csv): the distance, the limits, the state. The pass (T20 as the
// Producer restated it at 11:30 after QA M2: at 15 fps 35 m/s is 2.3 m a frame): on a frame where a limit eases, the distance
// moves at most 35 m/s × dt + 0.05 m; on a frame with no zoom input it never moves more than 3 m (the snap was 16 m in one
// frame); never outside the limits for longer than 1.5 s; the limits read 8-48 m outside at night and 8-22 m in a view.
// The mouse's wheel too (QA M2: the scroll path was one of the two that snapped, and a pad never drives it): a virtual
// mouse's notch out and back in at the bins (B), and a notch in at her pavement (D), where the old code snapped to 24 m.
// Timings are generous guesses until the Mac runs it (13 Oct lesson).
// ---------------------------------------------------------------------------
public sealed class NightZoomCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Night.ZoomCheck";

    protected override string Title => "Night zoom - from the bins to Grace's house";
    protected override string Tag => "[Night zoom check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Night zoom check] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Night zoom check (this Play session only)");
        go.AddComponent<NightZoomCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            $"night-zoom-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // The bars (T20 as restated 14 Oct 11:30, and the spec's §4.3.4).
    const float EaseSlack = .05f;          // a frame where a limit eases: at most LimitSpeed × dt + this, metres
    const float StillMost = 3f;            // a frame with no zoom input: at most this, metres
    const float OutOfRangeMost = 1.5f;
    const float WheelNotch = 1.6f;         // CafeViewMode: a notch of 120 is 1.6 m

    CafeViewMode view;
    PlayerMovement movement;
    CharacterController capsule;
    NightWalk night;
    NightZeroSet bins;
    GraceHouse house;
    bool clockHeldBefore;

    // ---------- the trace: every frame, after the view's own LateUpdate (it runs at -100) ----------

    struct Sample
    {
        public float t, dt, distance, lo, hi;
        public bool following, inHouse, binsView, angleEasing, distanceEasing, zoomInput;
        public string phase;
        public Vector3 ace;
    }

    readonly List<Sample> trace = new List<Sample>(8192);
    bool tracing, zoomHeld;
    string phase = "setup";
    float traceStart;

    void LateUpdate()
    {
        if (!tracing || view == null) return;
        Vector4 limits = view.OverheadLimits;
        trace.Add(new Sample
        {
            t = Time.realtimeSinceStartup - traceStart,
            dt = Time.unscaledDeltaTime,
            distance = view.OverheadAngle.z,
            lo = limits.z,
            hi = limits.w,
            following = view.Following,
            inHouse = view.InHouseView,
            binsView = Lodger.Instance != null && Lodger.Instance.InBinsView,
            angleEasing = view.AngleEasing,
            distanceEasing = view.DistanceEasing,
            zoomInput = zoomHeld,
            phase = phase,
            ace = view.AceFeet,
        });
    }

    float Distance => view.OverheadAngle.z;
    int Mark => trace.Count;

    // ---------- the run ----------

    protected override IEnumerator Run()
    {
        yield return Until(() => NightWalk.Instance != null && NightWalk.Instance.Active && Camera.main != null, 20f,
            "the lab's night is on (a night walk lab session)");
        if (!lastWait) yield break;
        night = NightWalk.Instance;
        movement = FindAnyObjectByType<PlayerMovement>();
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        capsule = movement != null ? movement.GetComponent<CharacterController>() : null;
        bins = NightZeroSet.Instance;
        house = GraceHouse.Instance;
        Check(movement != null && view != null && capsule != null, "Ace is in the scene, with the café's view and his capsule");
        Check(bins != null, "the bins are behind the café (NightZeroSet)");
        if (movement == null || view == null || capsule == null || bins == null) yield break;
        for (float until = Time.realtimeSinceStartup + 5f; !Application.isFocused && Time.realtimeSinceStartup < until;) yield return null;
        Check(Application.isFocused, "the Game view has focus (the view takes no input without it: click in it and run again)");
        if (!Application.isFocused) yield break;

        clockHeldBefore = night.ClockHeld;
        night.ClockHeld = true;   // the night's clock stands still: the lab can't run into dawn
        if (Lodger.Instance == null)
        {
            Lodger.Arrive(hidden: true);   // the man met: the night walk lab skips the dusk that brings him
            Note("the man at the bins was brought for this lab (the night walk lab skips the dusk that brings him)");
        }
        yield return Seconds(.5f);
        Check(Lodger.Instance != null, "the man is at the bins tonight (his bins view needs him)");
        if (Lodger.Instance == null) yield break;
        // Nobody home at Grace's: walking into her house must not end the night with "Caught." (the walk check's own way).
        if (GraceAtHome.Instance != null) GraceAtHome.Instance.OutAllNight("the night zoom check: nobody home, so Ace can walk in");
        if (view.FirstPersonSelected) view.SetFirstPerson(false);
        PlugInPad("Night zoom check pad");
        yield return PadPress(GamepadButton.LeftShoulder);   // a touch of the pad: the game takes it as the one in hand
        yield return Frames(3);

        Check(view.Following, "the overhead view follows Ace at night");
        Vector4 l = view.OverheadLimits;
        Check(Mathf.Abs(l.z - view.NightZoomMin) < .01f && Mathf.Abs(l.w - view.maximumDistance) < .01f,
            $"outside at night the limits read {view.NightZoomMin:0}-{view.maximumDistance:0} m ({l.z:0.0}-{l.w:0.0})");
        Note($"frame time at the start: {Time.unscaledDeltaTime * 1000f:0} ms ({1f / Mathf.Max(.001f, Time.unscaledDeltaTime):0} fps); his Mac runs labs at 15-20 fps");

        Vector3 binsAt = bins.Bins;
        tracing = true;
        traceStart = Time.realtimeSinceStartup;

        yield return FromFarIntoTheBins(binsAt);
        yield return AtTheBins();
        yield return WalkAwayToGrace(binsAt);
        yield return AtHerPavement();
        yield return IntoHerHouse();
        yield return TheDayAfter();

        tracing = false;
        PadRelease();
        Judge();
        WriteTrace();
    }

    // ---------- A. from far into the bins view, the right stick held through the turn ----------

    IEnumerator FromFarIntoTheBins(Vector3 binsAt)
    {
        phase = "A from far into the bins";
        // 13 m west of the dumpster along the rear lane (snapped to the navmesh: the lane, never a roof), well outside the 8 m view.
        Vector3 start = OnWalkway(binsAt + new Vector3(-13f, 0f, 1.2f));
        PutAce(start, 90f);
        yield return Frames(6);
        Check(!Lodger.Instance.InBinsView && !view.InHouseView, $"Ace starts {Flat(view.AceFeet - binsAt):0.0} m from the bins, outside their view");
        // LT out to 40 m (or the farthest the limits allow).
        float want = Mathf.Min(40f, view.OverheadLimits.w - .05f);
        yield return HoldZoom(left: true, until: () => Distance >= want - .05f, most: 3f);
        Check(Distance >= want - .3f, $"LT takes the view out to {want:0} m ({Distance:0.0} m)");
        // Walk toward the bins with the right stick held left; once the bins view begins, keep it held through the turn.
        float viewBegan = -1f;
        int began = -1;
        Vector3 binSpot = bins.binSpot != null ? OnGround(bins.binSpot.position) : binsAt;   // where Ace stands to put the bag in
        yield return WalkTo(binSpot, .8f, 12f, () => new Vector2(-1f, 0f), () =>
        {
            if (viewBegan < 0f && Lodger.Instance.InBinsView) { viewBegan = Time.realtimeSinceStartup; began = Mark; }
            return viewBegan >= 0f && Time.realtimeSinceStartup - viewBegan >= 1.6f;
        });
        PadRelease();
        Check(viewBegan >= 0f, "walking up to the bins brings the bins view (Lodger.BinsView)");
        if (viewBegan < 0f) yield break;
        float arrived = ArrivedWithin(began, 8f - .01f, 22f + .01f);
        Check(arrived >= 0f && arrived <= OutOfRangeMost,
            $"from {want:0} m, with the right stick held through the turn, the distance eases into the view's 8-22 m within {OutOfRangeMost:0.0} s " +
            $"({(arrived >= 0f ? arrived.ToString("0.00") + " s" : "never")}; it is {Distance:0.0} m)");
        yield return Seconds(.6f);
    }

    // ---------- B. at the bins: RT for 1 s ----------

    IEnumerator AtTheBins()
    {
        phase = "B at the bins";
        Check(Lodger.Instance.InBinsView && view.InHouseView, "Ace is at the bins, in their view");
        Vector4 l = view.OverheadLimits;
        Check(Mathf.Abs(l.z - 8f) < .01f && Mathf.Abs(l.w - 22f) < .01f, $"in the bins view the limits read 8-22 m ({l.z:0.0}-{l.w:0.0})");
        yield return HoldZoom(left: false, until: () => false, most: 1f);
        yield return Frames(3);
        Check(Mathf.Abs(Distance - 8f) <= .05f, $"RT for a second brings the view to 8.0 m ({Distance:0.00} m)");
        // The wheel (QA M2): a notch out, a notch back in.
        yield return Scroll(-1f);
        float notchOut = Distance;
        yield return Scroll(1f);
        Check(Mathf.Abs(notchOut - (8f + WheelNotch)) <= .1f && Mathf.Abs(Distance - 8f) <= .05f,
            $"the mouse's wheel at the bins: a notch out {notchOut:0.00} m ({8f + WheelNotch:0.0}), a notch back in {Distance:0.00} m (8.0)");
        yield return Photo("1-at-the-bins-8m");
    }

    // One wheel notch (or more) from a virtual mouse, as a wheel sends it (120 a notch), read for exactly one frame; the frames
    // are marked as zoom input in the trace. Hands off the real mouse meanwhile (it would take Mouse.current back).
    UnityEngine.InputSystem.Mouse wheel;
    IEnumerator Scroll(float notches)
    {
        if (wheel == null) wheel = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Night zoom check mouse");
        wheel.MakeCurrent();
        var at = new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f);
        zoomHeld = true;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(wheel, new MouseState { position = at, scroll = new Vector2(0f, 120f * notches) });
        yield return null;   // the next frame's Update reads it
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(wheel, new MouseState { position = at });
        yield return Frames(2);
        zoomHeld = false;
        yield return Frames(1);
    }

    // ---------- C. walking away toward Grace's pavement ----------

    IEnumerator WalkAwayToGrace(Vector3 binsAt)
    {
        phase = "C walking to Grace's";
        Vector3 pavement = GracePavement();
        List<Vector3> route = Route(view.AceFeet, pavement);
        Note($"the walk to Grace's pavement: {route.Count} turn(s), {Length(view.AceFeet, route):0} m (the spec's estimate: about 43 m)");
        float ended = -1f;
        int endedAt = -1;
        float least = float.PositiveInfinity, most = float.NegativeInfinity;
        bool photo = false;
        Func<Vector2> right = () =>
        {
            if (ended < 0f && !Lodger.Instance.InBinsView) { ended = Time.realtimeSinceStartup; endedAt = Mark; }
            return ended >= 0f && Time.realtimeSinceStartup - ended <= 1f ? new Vector2(-1f, 0f) : Vector2.zero;
        };
        foreach (Vector3 corner in route)
        {
            yield return WalkTo(corner, 1.2f, 25f, right, () =>
            {
                if (ended >= 0f) { least = Mathf.Min(least, Distance); most = Mathf.Max(most, Distance); }
                return false;
            });
            if (!photo && ended >= 0f && Time.realtimeSinceStartup - ended > 2f)
            {
                photo = true;
                yield return Photo("2-walking-away-8m");
            }
        }
        PadRelease();
        Check(ended >= 0f, "walking away ends the bins view");
        if (ended < 0f) yield break;
        if (!photo) yield return Photo("2-walking-away-8m");
        Check(least >= 8f - .1f && most <= 8f + .1f,
            $"from the frame the bins view ended to her pavement, with no zoom pressed (the right stick held for the first second), the distance stays at 8.0 m " +
            $"({least:0.00}-{most:0.00} m; the old code snapped to 24 m at the first touch of a trigger)");
        Check(Flat(view.AceFeet - pavement) <= 3f, $"Ace reached Grace's pavement ({Flat(view.AceFeet - pavement):0.0} m from it)");
        if (endedAt >= 0) Note($"the bins view ended {(trace.Count > endedAt ? trace[endedAt].t : 0f):0.0} s into the trace");
    }

    // ---------- D. at her pavement ----------

    IEnumerator AtHerPavement()
    {
        phase = "D at her pavement";
        yield return Seconds(.3f);
        float before = Distance;
        yield return HoldZoom(left: false, until: () => false, most: .3f);
        yield return Frames(3);
        Check(Mathf.Abs(Distance - before) <= .05f && Mathf.Abs(Distance - view.NightZoomMin) <= .1f,
            $"RT for 0.3 s changes nothing: {view.NightZoomMin:0} m is the night's nearest ({before:0.00} -> {Distance:0.00} m)");
        before = Distance;
        yield return Scroll(1f);   // the wheel's touch, where the old clamp snapped 8 m to 24 m
        Check(Mathf.Abs(Distance - before) <= .05f, $"a wheel notch in changes nothing either, no snap ({before:0.00} -> {Distance:0.00} m)");
        before = Distance;
        int from = Mark;
        yield return HoldZoom(left: true, until: () => false, most: .5f);
        yield return Frames(3);
        // The rate over the frames the zoom really moved (the press takes a frame or two to arrive and to let go; a frame over
        // 0.1 s moves only 0.1 s worth, as every view input does after a hitch).
        float moved = 0f, movingTime = 0f;
        for (int i = Mathf.Max(1, from); i < trace.Count; i++)
        {
            float step = trace[i].distance - trace[i - 1].distance;
            if (step > .001f) { moved += step; movingTime += Mathf.Min(trace[i].dt, .1f); }
        }
        float rate = moved / Mathf.Max(.01f, movingTime);
        Check(rate >= 22f * .9f && rate <= 22f * 1.1f && Distance - before >= 8f,
            $"LT for 0.5 s zooms out at the trigger's 22 m/s: {before:0.0} -> {Distance:0.0} m ({rate:0.0} m/s over {movingTime:0.00} s of movement; ±10%; the spec's \"about +11 m\")");
        yield return Photo("3-her-pavement-after-LT");

        Vector3 home = view.OverheadHome;
        float pressed = Time.realtimeSinceStartup;
        yield return PadPress(GamepadButton.RightStick);
        yield return Await(() => AtHome(home), 2.5f);
        float took = Time.realtimeSinceStartup - pressed;
        Check(lastWait && took <= 1.5f + .2f,
            $"R3 eases home ({home.z:0} m, {home.x:0}°, {home.y:0}°) within 1.5 s ({took:0.00} s, from the press; the pad's press takes a few frames)");
        yield return Photo("4-her-pavement-after-R3");
    }

    bool AtHome(Vector3 home)
    {
        Vector3 a = view.OverheadAngle;
        return Mathf.Abs(Mathf.DeltaAngle(a.x, home.x)) < .5f && Mathf.Abs(a.y - home.y) < .5f && Mathf.Abs(a.z - home.z) < .05f && !view.AngleEasing && !view.DistanceEasing;
    }

    // ---------- E. into her house from 40 m, the right stick held through the turn ----------

    IEnumerator IntoHerHouse()
    {
        phase = "E into her house";
        if (house == null || house.door == null || !GraceHouse.BreakInsOn)
        {
            Note($"her house, her door or the break-ins aren't here tonight ({(house == null ? "no GraceHouse" : house.door == null ? "no door" : "break-ins off")}): " +
                 "the entry from 40 m was proved at the bins (A) instead");
            yield break;
        }
        StreetDoor door = house.door;
        Vector3 stoop = OnGround(door.DoorwayPoint + door.Outward * 1.3f);
        Vector3 inward = -door.Outward;
        PutAce(stoop, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg);
        yield return Frames(6);
        float want = Mathf.Min(40f, view.OverheadLimits.w - .05f);
        yield return HoldZoom(left: true, until: () => Distance >= want - .05f, most: 3f);
        house.LetAceIn();   // E on her stoop: "Let yourself in"
        yield return Frames(3);
        float began = -1f;
        int beganAt = -1;
        Vector3 inside = door.DoorwayPoint + inward * 3f;
        yield return WalkTo(inside, .8f, 8f, () => new Vector2(-1f, 0f), () =>
        {
            if (began < 0f && house.Viewing) { began = Time.realtimeSinceStartup; beganAt = Mark; }
            return began >= 0f && Time.realtimeSinceStartup - began >= 1.6f;
        });
        // The walk can end at its goal before the ease has had its time (15 Oct: 0.35 s after the house view began, the distance
        // at 33.6 m and easing; the check read "never"): he stands there, the right stick still held, until 1.6 s have passed.
        while (began >= 0f && Time.realtimeSinceStartup - began < 1.6f)
        {
            PadHold(new GamepadState { rightStick = new Vector2(-1f, 0f) });
            yield return null;
        }
        PadRelease();
        if (began < 0f)
        {
            Note("Ace didn't get into her house this time (her door or a wall in the way): the entry from 40 m was proved at the bins (A)");
            yield break;
        }
        Vector4 l = view.OverheadLimits;
        Check(Mathf.Abs(l.z - 8f) < .01f && Mathf.Abs(l.w - 22f) < .01f, $"in her house the limits read 8-22 m ({l.z:0.0}-{l.w:0.0})");
        float arrived = ArrivedWithin(beganAt, 8f - .01f, 22f + .01f);
        Check(arrived >= 0f && arrived <= OutOfRangeMost,
            $"walking in from {want:0} m with the right stick held through the turn, the distance eases into 8-22 m within {OutOfRangeMost:0.0} s " +
            $"({(arrived >= 0f ? arrived.ToString("0.00") + " s" : "never")}; it is {Distance:0.0} m)");
        yield return Photo("5-in-her-house");
        // Out again: he didn't zoom inside, so the view's own zoom is undone (it eases back out).
        yield return WalkTo(OnGround(door.DoorwayPoint + door.Outward * 3f), .8f, 8f, () => Vector2.zero, () => false);
        PadRelease();
        yield return Seconds(1.6f);
        Check(!view.InHouseView, "walking out ends the house view");
    }

    // ---------- F. the day after the night ----------

    IEnumerator TheDayAfter()
    {
        phase = "F the day after";
        night.ClockHeld = clockHeldBefore;
        night.End();   // the night over: the day's view comes back exactly (a cut, as under the morning's curtain)
        yield return Frames(6);
        Check(!view.Following, "the night is over: the view no longer follows Ace");
        Vector4 l = view.OverheadLimits;
        Check(Mathf.Abs(l.z - view.minimumDistance) < .01f && Mathf.Abs(l.w - view.maximumDistance) < .01f && view.OverheadFocus == view.isometricFocus,
            $"by day the limits read {view.minimumDistance:0}-{view.maximumDistance:0} m round the café's middle ({l.z:0.0}-{l.w:0.0}, focus {view.OverheadFocus})");
        // Off home a little (orbit and zoom), then R3: it eases back.
        PadHold(new GamepadState { rightStick = new Vector2(1f, 0f) });
        yield return Seconds(.4f);
        PadRelease();
        yield return HoldZoom(left: true, until: () => false, most: .3f);
        Vector3 home = view.OverheadHome;
        float pressed = Time.realtimeSinceStartup;
        int from = Mark;
        yield return PadPress(GamepadButton.RightStick);
        yield return Await(() => AtHome(home), 2.5f);
        float took = Time.realtimeSinceStartup - pressed;
        Check(lastWait && took <= 1.5f + .2f, $"by day R3 eases home too, within 1.5 s ({took:0.00} s)");
        float worst = float.NegativeInfinity;
        for (int i = Mathf.Max(1, from); i < trace.Count; i++) worst = Mathf.Max(worst, Moved(i) - EaseBar(i));
        Check(worst <= 0f, $"...no frame of it moves more than {CafeViewMode.LimitSpeed:0} m/s × its frame + {EaseSlack:0.00} m " +
                           $"({(worst > float.NegativeInfinity ? (worst > 0f ? $"{worst:0.00} m over" : $"{-worst:0.00} m to spare at the closest") : "no frames")})");
        yield return Photo("6-the-day-after-R3");
    }

    // ---------- the verdict over every frame ----------

    float Speed(int i)
    {
        if (i <= 0 || i >= trace.Count) return 0f;
        return Moved(i) / Mathf.Max(1e-4f, trace[i].dt);
    }

    // How far the distance moved on frame i (0 across the night's end: a cut under the morning's curtain, never an ease).
    float Moved(int i)
    {
        if (i <= 0 || i >= trace.Count) return 0f;
        Sample a = trace[i - 1], b = trace[i];
        return a.following != b.following ? 0f : Mathf.Abs(b.distance - a.distance);
    }

    // T20 restated: the most a frame where a limit eases may move, LimitSpeed × its dt + 5 cm.
    float EaseBar(int i) => CafeViewMode.LimitSpeed * trace[i].dt + EaseSlack;

    // A limit (or a turn's distance part) was easing over frame i: at its start or its end (the ease's last step ends it).
    bool Easing(int i) => i > 0 && i < trace.Count && (trace[i].distanceEasing || trace[i - 1].distanceEasing);

    // Zoom input reached frame i: marked on it or on the frame before (a press lands a frame after the lab sends it).
    bool ZoomFrame(int i) => i >= 0 && i < trace.Count && (trace[i].zoomInput || i > 0 && trace[i - 1].zoomInput);

    void Judge()
    {
        float fastest = 0f, fastestAt = 0f;
        int frames = trace.Count;
        string fastestPhase = "";
        float worstEase = float.NegativeInfinity, worstEaseAt = 0f, biggestStill = 0f, biggestStillAt = 0f;
        int easeFrames = 0, easeOver = 0, stillOver = 0;
        string worstEasePhase = "", biggestStillPhase = "";
        float outStarted = -1f, longestOut = 0f, worstLimit = 0f;
        int wrongLimits = 0;
        string wrongLimit = "";
        float slowest = 0f, sumDt = 0f;
        for (int i = 0; i < trace.Count; i++)
        {
            Sample s = trace[i];
            sumDt += s.dt;
            slowest = Mathf.Max(slowest, s.dt);
            if (i > 0)
            {
                float v = Speed(i), moved = Moved(i);
                if (v > fastest) { fastest = v; fastestAt = s.t; fastestPhase = s.phase; }
                if (Easing(i) && !ZoomFrame(i))
                {
                    easeFrames++;
                    float over = moved - EaseBar(i);
                    if (over > 0f) easeOver++;
                    if (over > worstEase) { worstEase = over; worstEaseAt = s.t; worstEasePhase = s.phase; }
                }
                if (!ZoomFrame(i))
                {
                    if (moved > StillMost) stillOver++;
                    if (moved > biggestStill) { biggestStill = moved; biggestStillAt = s.t; biggestStillPhase = s.phase; }
                }
            }
            bool outside = s.distance < s.lo - .01f || s.distance > s.hi + .01f;
            if (outside) { if (outStarted < 0f) outStarted = s.t; longestOut = Mathf.Max(longestOut, s.t - outStarted); }
            else outStarted = -1f;
            if (s.following)
            {
                bool view8to22 = s.inHouse, ok = view8to22 ? Mathf.Abs(s.lo - 8f) < .01f && Mathf.Abs(s.hi - 22f) < .01f
                                                          : Mathf.Abs(s.lo - view.NightZoomMin) < .01f && Mathf.Abs(s.hi - view.maximumDistance) < .01f;
                if (!ok) { wrongLimits++; if (wrongLimit.Length == 0) wrongLimit = $"{s.lo:0.0}-{s.hi:0.0} at {s.t:0.0} s ({s.phase})"; }
                worstLimit = Mathf.Max(worstLimit, outside ? Mathf.Max(s.lo - s.distance, s.distance - s.hi) : 0f);
            }
        }
        Note($"{frames} frames traced over {(frames > 0 ? trace[frames - 1].t : 0f):0.0} s; mean frame {(frames > 0 ? sumDt / frames * 1000f : 0f):0} ms, the longest {slowest * 1000f:0} ms");
        Note($"the fastest frame of all, zoom input included: {fastest:0.0} m/s, {fastestAt:0.0} s in ({fastestPhase}); a wheel notch is {WheelNotch:0.0} m in one frame by design");
        Check(easeFrames > 0 && easeOver == 0,
            $"T20: on every frame where a limit eases ({easeFrames} frames), the distance moves at most {CafeViewMode.LimitSpeed:0} m/s × the frame + {EaseSlack:0.00} m " +
            $"({easeOver} over; {(worstEase > float.NegativeInfinity ? (worstEase > 0f ? $"the worst {worstEase:0.00} m over" : $"{-worstEase:0.00} m to spare at the closest") : "-")}" +
            $"{(worstEase > float.NegativeInfinity ? $", {worstEaseAt:0.0} s in ({worstEasePhase})" : "")}; at 15 fps that bar is 2.4 m a frame)");
        Check(stillOver == 0,
            $"T20: on a frame with no zoom input the distance never moves more than {StillMost:0} m ({stillOver} over; the biggest {biggestStill:0.00} m, " +
            $"{biggestStillAt:0.0} s in ({biggestStillPhase}); the bug was 16 m in one 67 ms frame, 8 m to 24 m)");
        Check(longestOut <= OutOfRangeMost, $"never outside the limits for longer than {OutOfRangeMost:0.0} s (the longest: {longestOut:0.00} s)");
        Check(wrongLimits == 0, $"the limits read {view.NightZoomMin:0}-{view.maximumDistance:0} m outside at night and 8-22 m in a view on every frame " +
                                $"({wrongLimits} frame(s) otherwise{(wrongLimit.Length > 0 ? "; first: " + wrongLimit : "")})");
    }

    void WriteTrace()
    {
        var csv = new StringBuilder();
        csv.AppendLine("t,dt,distance,min,max,speed,phase,following,house_view,bins_view,angle_easing,distance_easing,zoom_input,ace_x,ace_z");
        for (int i = 0; i < trace.Count; i++)
        {
            Sample s = trace[i];
            csv.Append(F(s.t)).Append(',').Append(F(s.dt)).Append(',').Append(F(s.distance)).Append(',').Append(F(s.lo)).Append(',').Append(F(s.hi)).Append(',')
               .Append(F(Speed(i))).Append(',').Append(s.phase).Append(',').Append(s.following ? 1 : 0).Append(',').Append(s.inHouse ? 1 : 0).Append(',')
               .Append(s.binsView ? 1 : 0).Append(',').Append(s.angleEasing ? 1 : 0).Append(',').Append(s.distanceEasing ? 1 : 0).Append(',')
               .Append(s.zoomInput ? 1 : 0).Append(',').Append(F(s.ace.x)).Append(',').Append(F(s.ace.z)).AppendLine();
        }
        try
        {
            File.WriteAllText(Path.Combine(folder, "trace.csv"), csv.ToString());
            Note($"the trace: {Path.Combine(folder, "trace.csv")} ({trace.Count} frames)");
        }
        catch (Exception e) { Note("couldn't write the trace: " + e.Message); }
    }

    static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    // How long after the frame <paramref name="from"/> the distance first came inside [lo, hi] and stayed there to the end of
    // what's traced, seconds; -1 if it never did.
    float ArrivedWithin(int from, float lo, float hi)
    {
        if (from < 0 || from >= trace.Count) return -1f;
        float start = trace[from].t;
        for (int i = from; i < trace.Count; i++)
        {
            float d = trace[i].distance;
            if (d < lo || d > hi) continue;
            bool stays = true;
            for (int j = i; j < trace.Count; j++) if (trace[j].distance < lo || trace[j].distance > hi) { stays = false; break; }
            if (stays) return trace[i].t - start;
        }
        return -1f;
    }

    // ---------- moving Ace and the camera with the pad ----------

    // A trigger held (LT out, RT in) until the condition holds or for at most this long; the zoom input is marked in the trace.
    IEnumerator HoldZoom(bool left, Func<bool> until, float most)
    {
        zoomHeld = true;
        PadHold(left ? new GamepadState { leftTrigger = 1f } : new GamepadState { rightTrigger = 1f });
        float end = Time.realtimeSinceStartup + most;
        yield return Frames(1);
        while (Time.realtimeSinceStartup < end && !Safe(until)) yield return null;
        PadRelease();
        zoomHeld = false;
        yield return Frames(2);
    }

    // Walks Ace to a point with the left stick (steered every frame for the camera's turn, as a thumb would), the right stick as
    // asked each frame; stops within <paramref name="within"/> metres, when <paramref name="stop"/> says so, or after
    // <paramref name="giveUp"/> seconds. Stuck against something for 1.5 s: a 1.5 m nudge along the way (noted, not failed:
    // what's measured is the camera, not the walk).
    IEnumerator WalkTo(Vector3 goal, float within, float giveUp, Func<Vector2> right, Func<bool> stop)
    {
        float end = Time.realtimeSinceStartup + giveUp;
        Vector3 lastPlace = view.AceFeet;
        float lastProgress = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup < end)
        {
            Vector3 to = goal - view.AceFeet;
            to.y = 0f;
            bool stopNow = Safe(stop);
            if (stopNow || to.magnitude <= within) break;
            PadHold(new GamepadState { leftStick = StickFor(to, view.MovementYaw), rightStick = right != null ? right() : Vector2.zero });
            if (Flat(view.AceFeet - lastPlace) > .3f) { lastPlace = view.AceFeet; lastProgress = Time.realtimeSinceStartup; }
            else if (Time.realtimeSinceStartup - lastProgress > 1.5f)
            {
                Note($"Ace stuck at {view.AceFeet} on the way to {goal} ({phase}): nudged 1.5 m along the way");
                PutAce(OnGround(view.AceFeet + to.normalized * Mathf.Min(1.5f, to.magnitude)), Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                lastPlace = view.AceFeet;
                lastProgress = Time.realtimeSinceStartup;
            }
            yield return null;
        }
        // One more frame of the stop condition's own bookkeeping (it may count time from a frame inside the walk).
        Safe(stop);
    }

    // The stick that walks toward a world direction, given the camera's turn (PlayerMovement: Euler(0, yaw, 0) * (x, 0, y)).
    static Vector2 StickFor(Vector3 direction, float cameraYaw)
    {
        float rad = (Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg - cameraYaw) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    // The way from here to there on the café's navmesh (the pavements the customers walk), else straight.
    static List<Vector3> Route(Vector3 from, Vector3 to)
    {
        var corners = new List<Vector3>();
        try
        {
            if (NavMesh.SamplePosition(from, out NavMeshHit a, 3f, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out NavMeshHit b, 3f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
                    for (int i = 1; i < path.corners.Length; i++) corners.Add(path.corners[i]);
            }
        }
        catch (Exception) { corners.Clear(); }
        if (corners.Count == 0 || Flat(corners[corners.Count - 1] - to) > 2f) corners.Add(to);
        return corners;
    }

    static float Length(Vector3 from, List<Vector3> route)
    {
        float sum = 0f;
        Vector3 at = from;
        foreach (Vector3 c in route) { sum += Flat(c - at); at = c; }
        return sum;
    }

    // Grace's pavement: 2.4 m out from her doorway (where the Grace at home lab stands Ace), else her door's own place.
    Vector3 GracePavement()
    {
        if (house != null && house.door != null) return OnWalkway(house.door.DoorwayPoint + house.door.Outward * 2.4f);
        HomeDoor door = HomeDoor.Find("home.grace");
        if (door != null) return OnWalkway(door.transform.position + door.transform.forward * 2.4f);
        Note("Grace's door wasn't found: walking to her side of the west street by the spec's numbers instead");
        return OnWalkway(new Vector3(-14.9f, 0f, -3.6f));
    }

    // A point on the café's navmesh within 4 m (the pavements and lanes the customers walk), else on whatever is below it.
    static Vector3 OnWalkway(Vector3 p)
    {
        try { if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return hit.position; }
        catch (Exception) { }
        return OnGround(p);
    }

    static Vector3 OnGround(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y;
        return p;
    }

    // Ace's feet put at a point (the capsule's own way: off, moved, on).
    void PutAce(Vector3 feet, float yaw)
    {
        float lift = capsule.height * .5f - capsule.center.y + .03f;
        bool was = capsule.enabled;
        capsule.enabled = false;
        movement.transform.SetPositionAndRotation(feet + Vector3.up * lift, Quaternion.Euler(0f, yaw, 0f));
        capsule.enabled = was;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // The virtual mouse out again (stopped mid-run too), so his own takes Mouse.current back.
    void RemoveWheel()
    {
        try { if (wheel != null && wheel.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(wheel); } catch (Exception) { }
        wheel = null;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        RemoveWheel();
    }

    protected override void Restore()
    {
        tracing = false;
        PadRelease();
        RemoveWheel();
        if (night != null && night.Active) night.ClockHeld = clockHeldBefore;
#if UNITY_EDITOR
        // Play mode ends by itself once the report is written (as the night labs and the day intro's check do).
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
