using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// ACE TURNING ROUND: THE PLAY CHECK (Fixit Fidget > Night > Ace's body 6; a night walk lab session only)
//
// Mansoor's report (5 Oct 2026): in the overhead view, by day and at night, on a controller, Ace "runs in a
// diagonal animation when I move the analog stick from up to down or vice versa"; the body points sideways.
// Out on the front street at night (the body's code is the same by day), with a virtual controller plugged in
// (the same path as a thumb: the Input System's dead zone, the game's, the assist), it measures every frame where
// Ace goes against where his body faces:
//   * running and walking steadily up, down, left and right on screen: the rig, and the body as drawn (its hips,
//     its shoulders), averaged over one whole stride. More than a couple of degrees
//     would mean the clip itself sits turned on Ace's body;
//   * the stick flicked from up to down and back, and from right to left and back (a quick flick, the thumb passing
//     the centre a little to one side), and wiggled up and down: how long Ace runs side-on (the body more than 45°
//     off the way he's going while the run shows), and how soon he's round. Each twice: the way it was before
//     5 Oct (Quick Turn 0, Pivot Step off) and as it is now;
//   * the stick pushed a little off straight up (5° to 25°): where the assist lets go;
// with photos of a flick, before and now, close up at the overhead view's own angle.
// Game time runs a fixed 1/120 s a frame while it measures (Time.captureDeltaTime), so the numbers don't depend
// on the frame rate and the photos don't disturb them.
// Report, photos and every frame (frames.csv): Logs/Night/ace-turn-check-<time>/. Nothing is saved to the scene
// or to any save; AceBody's settings are put back after.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(500)]   // samples after AceBody (120) has turned, posed and placed the body
public sealed class AceTurnCheck : MonoBehaviour
{
    public const string LabKey = "FixitFidget.AceTurn.Check";

    const float Frame = 1f / 120f;     // game time a frame while the check runs
    const float LegSeconds = 1.45f;    // each steady run along a line (about 7 m at 5 m/s: 0.35 s to get going, then a stride)
    const float FlickSeconds = .08f;   // a quick flick from one way to the opposite
    const float FlickArc = .35f;       // how far to the side of the centre the thumb passes
    const float WalkPush = .5f;        // a half push walks (about 1.7 m/s)
    const float LineLength = 8f;       // metres of clear, level street each line needs
    const float SideOn = 45f;          // degrees off the way Ace goes that read as running sideways
    const float RoundWithin = 10f;     // degrees off that count as turned round
    const float SteadyDrawn = 2.5f;    // degrees the body as drawn may sit off, over a stride

    struct Sample
    {
        public string pass;
        public float t;                    // seconds into the pass (game time)
        public Vector2 fed, read;          // the stick as pushed (applied the next frame), and as the game read it
        public float cmdYaw, cmdSpeed;     // where PlayerMovement sent the capsule
        public float travelYaw, travelSpeed;   // where the capsule really went this frame
        public float rigYaw, hipsYaw, shouldersYaw;
        public float facing, pivot, gait, wIdle, wWalk, wRun, phase;
        public float screenOff;            // on screen: degrees from the way he moves to the way the body faces
        public bool jump, bones;
    }

    string folder;
    readonly StringBuilder report = new StringBuilder();
    readonly List<Sample> samples = new List<Sample>();
    readonly List<string> table = new List<string>();
    int checks, failures, photos;
    float started;
    PlayerMovement movement;
    CafeViewMode view;
    AceBody body;
    Animator animator;
    CharacterController capsule;
    Transform ace;
    Gamepad pad;
    bool usePad, recording, settingsSaved, haveUp, haveAcross, restored;
    Vector2 fed;
    string pass = "";
    float passTime, savedQuickTurn;
    bool savedPivot;
    Vector3 lastAce, upWorld = Vector3.forward, acrossWorld = Vector3.right, lineUp, lineAcross;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(LabKey, 0) != 1) return;
        PlayerPrefs.DeleteKey(LabKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Ace's body] The turning-round check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        new GameObject("Ace turn check (this Play session only)") { hideFlags = PlaySessionLeftovers.RuntimeFlags }.AddComponent<AceTurnCheck>();
    }
#endif

    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "ace-turn-check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        report.AppendLine("Ace's body 6 - Check turning round (lab)");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        report.AppendLine("Angles are + when the body is turned to Ace's right of the way he's going, - to his left.");
        report.AppendLine();

        float until = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < until)
        {
            body = FindAnyObjectByType<AceBody>();
            if (NightWalk.Instance != null && NightWalk.Instance.Active && Camera.main != null && body != null && body.Worn && body.RigRoot != null) break;
            yield return null;
        }
        movement = FindAnyObjectByType<PlayerMovement>();
        view = FindAnyObjectByType<CafeViewMode>();
        Check(NightWalk.Instance != null && NightWalk.Instance.Active && movement != null && view != null && body != null && body.Worn,
              "The night walk is on, with Ace, the camera and Ace's body.");
        if (failures > 0) { Finish(); yield break; }
        ace = movement.transform;
        capsule = movement.GetComponent<CharacterController>();
        FindAnimator();
        Check(body.WearsSidekick, $"Ace wears his Sidekick body ({body.BodyName}).");
        if (animator == null || !animator.isHuman)
            Note("The body has no Humanoid bones, so only the rig's facing is measured (not the hips and shoulders).");
        savedQuickTurn = body.quickTurn;
        savedPivot = body.pivotStep;
        settingsSaved = true;
        Note($"Ace's body now: Turn Speed {body.turnSpeed:0}°/s, Quick Turn {body.quickTurn:0.#}, Pivot Step {(body.pivotStep ? "on" : "off")} " +
             $"(from {body.pivotAngles.x:0}° to {body.pivotAngles.y:0}° off, the legs at {body.pivotGait:0.0} m/s); Ace's top speed {movement.TopSpeed:0.0} m/s.");
        Note("Before 5 Oct means Quick Turn 0 and Pivot Step off, set for this check only and put back after.");
        Time.captureDeltaTime = Frame;
        Note($"Game time runs {Frame * 1000f:0.00} ms a frame while the check runs (Game view {Screen.width} x {Screen.height}).");
        yield return null;

        var sections = new List<(Func<IEnumerator> run, string name)>
        {
            (new Func<IEnumerator>(SetUp), "setting up"),
            (new Func<IEnumerator>(Steady), "steady running and walking"),
            (new Func<IEnumerator>(Reversals), "flicks"),
            (new Func<IEnumerator>(Wiggles), "wiggles"),
            (new Func<IEnumerator>(Thumb), "the thumb"),
            (new Func<IEnumerator>(Photos), "photos"),
        };
        foreach (var (run, name) in sections) yield return Guard(run(), name);
        Finish();
    }

    // ================================================================== the sections

    IEnumerator SetUp()
    {
        view.SetFirstPerson(false);
        Vector3 home = view.OverheadHome;
        view.OrbitTo(home.x, home.y, home.z);
        yield return Frames(3);
        // Up and across the screen, in the world: where the stick sends Ace.
        float yaw = view.MovementYaw;
        upWorld = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        acrossWorld = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Note($"The overhead view at home: yaw {home.x:0.#}°, pitch {home.y:0.#}°, {home.z:0.#} m. The stick's up is world yaw {yaw:0.#}°.");

        // Two clear, level lines of street near the tour's spot: one up the screen, one across it.
        bool was = capsule.enabled;
        capsule.enabled = false;
        Vector3 spot = new Vector3(.12f, ace.position.y, -7.62f);
        haveUp = FindLine(spot, upWorld, out lineUp);
        haveAcross = FindLine(spot, acrossWorld, out lineAcross);
        capsule.enabled = was;
        Check(haveUp, haveUp ? $"A clear, level line {LineLength:0} m long up the screen, from {P(lineUp)}."
                             : "No clear, level line up the screen within 14 m of the tour's spot.");
        Check(haveAcross, haveAcross ? $"A clear, level line {LineLength:0} m long across the screen, from {P(lineAcross)}."
                                     : "No clear, level line across the screen within 14 m of the tour's spot.");
        if (!haveUp && !haveAcross) yield break;

        // The controller: a virtual one, read exactly as a real one (the Input System's dead zone, the game's, the assist).
        pad = InputSystem.AddDevice<Gamepad>("Ace turn check pad");
        usePad = true;
        Vector3 line = haveUp ? lineUp : lineAcross, way = haveUp ? upWorld : acrossWorld;
        Vector2 push = haveUp ? Vector2.up : Vector2.right;
        yield return Place(line);
        Vector3 from = ace.position;
        lastAce = ace.position;
        recording = true;
        Pass("controller test");
        yield return Hold(push, .3f);
        float went = Vector3.Dot(ace.position - from, way);
        yield return Hold(Vector2.zero, .2f);
        usePad = went > .5f;
        if (usePad) Note($"The virtual controller walks Ace ({went:0.0} m in 0.3 s): the same path as a thumb on a stick.");
        else Note($"The virtual controller didn't move Ace ({went:0.00} m in 0.3 s), so the stick is fed as the game would read it " +
                  "(both dead zones applied here) through PlayerMovement.ScriptedInput, which skips the assist; the thumb test is skipped.");
    }

    IEnumerator Steady()
    {
        // Each way from standing at one end of its line; measured over one whole stride once Ace is going (0.35 s in).
        if (haveUp)
        {
            yield return Place(lineUp);
            yield return Leg("run up", Vector2.up, LegSeconds);
            yield return Leg("run down", Vector2.down, LegSeconds);
        }
        if (haveAcross)
        {
            yield return Place(lineAcross);
            yield return Leg("run right", Vector2.right, LegSeconds);
            yield return Leg("run left", Vector2.left, LegSeconds);
        }
        if (haveUp)
        {
            yield return Place(lineUp);
            yield return Leg("walk up", Vector2.up * WalkPush, 2.2f);
            yield return Leg("walk down", Vector2.down * WalkPush, 2.2f);
        }
        yield return Hold(Vector2.zero, .2f);
        if (haveUp)
        {
            SteadyResult("run up", upWorld);
            SteadyResult("run down", -upWorld);
        }
        if (haveAcross)
        {
            SteadyResult("run right", acrossWorld);
            SteadyResult("run left", -acrossWorld);
        }
        if (haveUp)
        {
            SteadyResult("walk up", upWorld);
            SteadyResult("walk down", -upWorld);
        }
    }

    IEnumerator Reversals()
    {
        foreach (bool now in new[] { false, true })
        {
            Settings(now);
            string when = now ? "now" : "before";
            // Along each line: out one way, flick back, then flick out again (start, +6 m, back past the start, out again).
            foreach ((bool have, Vector3 line, Vector2 out1, string a, string b) in new[]
                     {
                         (haveUp, lineUp, Vector2.up, "up", "down"),
                         (haveAcross, lineAcross, Vector2.right, "right", "left"),
                     })
            {
                if (!have) continue;
                yield return Place(line);
                Pass("lead-in");
                yield return Hold(out1, 1.2f);
                Pass($"flick {a} to {b} ({when})");
                yield return Flick(out1, 1f);
                yield return Hold(-out1, 1.25f);
                Pass($"flick {b} to {a} ({when})");
                yield return Flick(-out1, -1f);
                yield return Hold(out1, .9f);
                yield return Hold(Vector2.zero, .3f);
            }
        }
        Settings(true);
        foreach ((bool have, string a, string b) in new[] { (haveUp, "up", "down"), (haveAcross, "right", "left") })
        {
            if (!have) continue;
            FlickResult($"flick {a} to {b}");
            FlickResult($"flick {b} to {a}");
        }
    }

    IEnumerator Wiggles()
    {
        if (!haveUp) yield break;
        foreach (bool now in new[] { false, true })
        {
            Settings(now);
            string when = now ? "now" : "before";
            yield return Place(lineUp);
            Pass("lead-in");
            yield return Hold(Vector2.up, .6f);
            // Down and up again, twice, a flick every 0.35 s: the thumb going back and forth.
            Pass($"wiggle ({when})");
            Vector2 way = Vector2.up;
            for (int i = 0; i < 4; i++)
            {
                yield return Flick(way, i % 2 == 0 ? 1f : -1f);
                way = -way;
                yield return Hold(way, .35f);
            }
            yield return Hold(Vector2.zero, .3f);
        }
        Settings(true);
        WiggleResult();
    }

    IEnumerator Thumb()
    {
        if (!haveUp) yield break;
        if (!usePad)
        {
            Note("The thumb test needs the virtual controller (the assist only straightens a real stick); skipped.");
            yield break;
        }
        // The stick pushed a little to the right of straight up: where does Ace go? The assist straightens it within
        // its core, lets go smoothly to its edge, and leaves it alone past that.
        Vector4 bands = movement.AssistBands;
        Note($"The assist's bands: along the walls straight within {bands.x:0.#}°, free past {bands.y:0.#}°; " +
             $"screen axes straight within {bands.z:0.#}°, free past {bands.w:0.#}°.");
        var line = new StringBuilder("The thumb, pushed off straight up (stick -> where Ace went):");
        foreach (float off in new[] { 5f, 10f, 12.5f, 15f, 20f, 25f })
        {
            yield return Place(lineUp);
            string name = $"thumb {off:0.#}";
            Pass(name);
            float rad = off * Mathf.Deg2Rad;
            yield return Hold(new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)), .45f);
            yield return Hold(Vector2.zero, .1f);
            var went = samples.Where(s => s.pass == name && !s.jump && s.t >= .15f && s.t <= .45f && s.travelSpeed > 1f).ToList();
            float path = went.Count > 0 ? MeanAngle(went.Select(s => Mathf.DeltaAngle(YawOf(upWorld), s.travelYaw))) : float.NaN;
            line.Append($"  {off:0.#}° -> {path:0.0}°");
            // Inside both cores Ace must go dead straight, whichever line (a wall's or the screen's) the assist picks.
            if (off <= Mathf.Min(bands.x, bands.z))
                Check(Mathf.Abs(path) <= .6f, $"Pushed {off:0.#}° off straight up (inside the assist's core), Ace goes straight up ({path:+0.0;-0.0}°).");
        }
        Note(line.ToString());
    }

    IEnumerator Photos()
    {
        if (!haveUp) yield break;
        // Close up, at the overhead view's own angle: the camera's nearest zoom.
        Vector3 home = view.OverheadHome;
        Vector4 limits = view.OverheadLimits;
        view.OrbitTo(home.x, home.y, limits.z);
        float[] at = { .04f, .08f, .12f, .16f, .24f };
        int number = 1;
        foreach (bool now in new[] { false, true })
        {
            Settings(now);
            string when = now ? "now" : "before";
            yield return Place(lineUp);
            Pass("photo lead-in");
            yield return Hold(Vector2.up, 1.2f);
            if (now) yield return Photo($"{number++:00}-running-up");
            // The flick from up to down, a photo at each moment (seconds from the flick's start).
            Pass($"photo flick ({when})");
            int next = 0, frames = Mathf.RoundToInt(.3f / Frame);
            for (int i = 1; i <= frames; i++)
            {
                float t = i * Frame;
                Feed(t <= FlickSeconds ? Flicked(Vector2.up, 1f, t / FlickSeconds) : Vector2.down);
                yield return null;
                if (next < at.Length && t >= at[next] - 1e-4f)
                {
                    yield return Photo($"{number++:00}-flick-{when}-{at[next]:0.00}s");
                    next++;
                }
            }
            yield return Hold(Vector2.down, .5f);
            if (now) yield return Photo($"{number++:00}-running-down");
            yield return Hold(Vector2.zero, .3f);
        }
        if (haveAcross)
        {
            yield return Place(lineAcross);
            yield return Hold(Vector2.right, 1f);
            yield return Photo($"{number++:00}-running-right");
            yield return Hold(Vector2.zero, .25f);
            yield return Hold(Vector2.left, 1f);
            yield return Photo($"{number++:00}-running-left");
            yield return Hold(Vector2.zero, .3f);
        }
        Settings(true);
        view.OrbitTo(home.x, home.y, home.z);
    }

    // ================================================================== the results

    void SteadyResult(string name, Vector3 way)
    {
        List<Sample> stride = Stride(name);
        if (stride == null)
        {
            Check(false, $"{name}: couldn't measure a whole stride (the leg was too short, or Ace didn't move).");
            return;
        }
        float want = YawOf(way);
        float path = MeanAngle(stride.Select(s => Mathf.DeltaAngle(want, s.travelYaw)));
        float rig = MeanAngle(stride.Select(s => Mathf.DeltaAngle(s.travelYaw, s.rigYaw)));
        float speed = stride.Average(s => s.travelSpeed);
        float run = stride.Average(s => s.wRun);
        Check(Mathf.Abs(path) <= 1f, $"{name}: Ace goes straight ({path:+0.0;-0.0}° off) at {speed:0.0} m/s (the run showing {run:0.00}).");
        Check(Mathf.Abs(rig) <= 1f, $"{name}: the body's rig faces the way he goes ({rig:+0.0;-0.0}°).");
        if (!stride.All(s => s.bones)) return;
        float hips = MeanAngle(stride.Select(s => Mathf.DeltaAngle(s.travelYaw, s.hipsYaw)));
        float shoulders = MeanAngle(stride.Select(s => Mathf.DeltaAngle(s.travelYaw, s.shouldersYaw)));
        float drawn = MeanAngle(stride.Select(s => Mathf.DeltaAngle(s.travelYaw, Mid(s.hipsYaw, s.shouldersYaw))));
        float screen = MeanAngle(stride.Select(s => s.screenOff));
        float swing = stride.Max(s => Mathf.Abs(Mathf.DeltaAngle(s.travelYaw, Mid(s.hipsYaw, s.shouldersYaw))));
        Check(Mathf.Abs(drawn) <= SteadyDrawn,
              $"{name}: the body as drawn faces the way he goes, over one stride: {drawn:+0.0;-0.0}° (hips {hips:+0.0;-0.0}°, " +
              $"shoulders {shoulders:+0.0;-0.0}°; on screen {screen:+0.0;-0.0}°; " +
              $"swinging up to {swing:0}° within the stride)" +
              (Mathf.Abs(drawn) > SteadyDrawn ? $". The clip itself sits turned on Ace's body (more than {SteadyDrawn}°)" : ""));
        table.Add($"{name,-10}  path {path,5:+0.0;-0.0}°   rig {rig,5:+0.0;-0.0}°   drawn {drawn,5:+0.0;-0.0}°   hips {hips,5:+0.0;-0.0}°   " +
                  $"shoulders {shoulders,5:+0.0;-0.0}°   on screen {screen,5:+0.0;-0.0}°");
    }

    struct Turn { public float sideOn, sideOnDrawn, roundBy, worst, moving; public bool any; }

    Turn Measure(string name)
    {
        var turn = new Turn();
        foreach (Sample s in samples)
        {
            if (s.pass != name || s.jump || float.IsNaN(s.travelYaw) || s.travelSpeed < 1f) continue;
            turn.any = true;
            turn.moving += Frame;
            float rig = Mathf.Abs(Mathf.DeltaAngle(s.travelYaw, s.rigYaw));
            float drawn = s.bones ? Mathf.Abs(Mathf.DeltaAngle(s.travelYaw, Mid(s.hipsYaw, s.shouldersYaw))) : rig;
            bool running = s.wRun > .5f;
            if (running && rig > SideOn) turn.sideOn += Frame;
            if (running && drawn > SideOn) turn.sideOnDrawn += Frame;
            if (running) turn.worst = Mathf.Max(turn.worst, rig);
            if (rig > RoundWithin) turn.roundBy = s.t + Frame;
        }
        return turn;
    }

    void FlickResult(string name)
    {
        Turn before = Measure(name + " (before)"), now = Measure(name + " (now)");
        if (!before.any || !now.any)
        {
            Check(false, $"{name}: Ace didn't move ({(before.any ? "" : "before ")}{(now.any ? "" : "now")}).");
            return;
        }
        Note($"{name}, before: running side-on for {before.sideOn:0.000} s (as drawn {before.sideOnDrawn:0.000} s), " +
             $"turned round {before.roundBy:0.00} s after the flick began, up to {before.worst:0}° off while the run showed.");
        Check(now.sideOn <= .0251f && now.sideOnDrawn <= .0251f,   // at most 3 frames (1/120 s each)
              $"{name}, now: running side-on for {now.sideOn:0.000} s (as drawn {now.sideOnDrawn:0.000} s; before {before.sideOn:0.000} s), " +
              $"up to {now.worst:0}° off while the run showed.");
        Check(now.roundBy <= .25f, $"{name}, now: turned round {now.roundBy:0.00} s after the flick began (before {before.roundBy:0.00} s; the flick itself takes {FlickSeconds:0.00} s).");
        table.Add($"{name,-19}  side-on running {before.sideOn,6:0.000} s -> {now.sideOn,6:0.000} s   round by {before.roundBy,5:0.00} s -> {now.roundBy,5:0.00} s");
    }

    void WiggleResult()
    {
        Turn before = Measure("wiggle (before)"), now = Measure("wiggle (now)");
        if (!before.any || !now.any) { Check(false, "wiggle: Ace didn't move."); return; }
        float b = before.sideOn / Mathf.Max(.001f, before.moving), n = now.sideOn / Mathf.Max(.001f, now.moving);
        Note($"wiggle, before: running side-on for {before.sideOn:0.00} s of {before.moving:0.00} s on the move ({b:P0}).");
        Check(n <= .05f, $"wiggle, now: running side-on for {now.sideOn:0.00} s of {now.moving:0.00} s on the move ({n:P0}; before {b:P0}).");
        table.Add($"{"wiggle up-down",-19}  side-on running {b,6:P0} -> {n,6:P0} of the time on the move");
    }

    // One whole stride of a steady leg: from 0.35 s in, until the stride phase has gone round once.
    List<Sample> Stride(string name)
    {
        var window = new List<Sample>();
        float round = 0f, last = float.NaN;
        foreach (Sample s in samples)
        {
            if (s.pass != name || s.jump || s.t < .35f || float.IsNaN(s.travelYaw) || s.travelSpeed < .5f) continue;
            if (!float.IsNaN(last)) round += Mathf.Repeat(s.phase - last, 1f);
            last = s.phase;
            window.Add(s);
            if (round >= 1f) return window;
        }
        return null;
    }

    // ================================================================== sampling (every frame)

    void LateUpdate()
    {
        if (!recording || ace == null || body == null) return;
        if (animator == null) FindAnimator();
        float dt = Time.deltaTime;
        Vector3 now = ace.position, moved = now - lastAce;
        lastAce = now;
        moved.y = 0f;
        var s = new Sample { pass = pass, t = passTime, fed = fed };
        passTime += dt;
        s.jump = moved.magnitude > 1f;
        s.read = usePad ? PadInput.LeftStick : movement.ScriptedInput ?? Vector2.zero;
        Vector3 cmd = movement.CommandedVelocity;
        cmd.y = 0f;
        s.cmdSpeed = cmd.magnitude;
        s.cmdYaw = s.cmdSpeed > .01f ? YawOf(cmd) : float.NaN;
        s.travelSpeed = dt > 0f && !s.jump ? moved.magnitude / dt : 0f;
        s.travelYaw = s.travelSpeed > .05f ? YawOf(moved) : float.NaN;
        s.rigYaw = body.RigRoot != null ? body.RigRoot.eulerAngles.y : body.BodyYaw;
        s.facing = body.FacingError;
        s.pivot = body.Pivot;
        s.gait = body.GaitSpeed;
        Vector3 gait = body.Gait;
        s.wIdle = gait.x;
        s.wWalk = gait.y;
        s.wRun = gait.z;
        s.phase = body.StridePhase;
        s.bones = Bones(out s.hipsYaw, out s.shouldersYaw);
        s.screenOff = s.bones && !float.IsNaN(s.travelYaw) ? ScreenOffset(s.travelYaw, Mid(s.hipsYaw, s.shouldersYaw)) : float.NaN;
        samples.Add(s);
    }

    void FindAnimator() => animator = body != null && body.RigRoot != null ? body.RigRoot.GetComponentInChildren<Animator>() : null;

    // Where the hips and the shoulders face: square to the line from the left one to the right one.
    // (Not Animator.bodyRotation: Unity only allows reading it inside OnAnimatorIK, and warns every frame otherwise.)
    bool Bones(out float hips, out float shoulders)
    {
        hips = shoulders = float.NaN;
        if (animator == null || !animator.isHuman) return false;
        Transform lh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform ls = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), rs = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (lh == null || rh == null || ls == null || rs == null) return false;
        hips = YawOf(Vector3.Cross(rh.position - lh.position, Vector3.up));
        shoulders = YawOf(Vector3.Cross(rs.position - ls.position, Vector3.up));
        return true;
    }

    // On screen, from the way Ace moves to the way the body faces (what the player sees).
    float ScreenOffset(float travelYaw, float facingYaw)
    {
        Camera cam = Camera.main;
        if (cam == null || float.IsNaN(facingYaw)) return float.NaN;
        Vector3 at = ace.position;
        Vector3 p0 = cam.WorldToScreenPoint(at), pt = cam.WorldToScreenPoint(at + Dir(travelYaw) * .5f), pb = cam.WorldToScreenPoint(at + Dir(facingYaw) * .5f);
        Vector2 a = (Vector2)(pt - p0), b = (Vector2)(pb - p0);
        if (a.sqrMagnitude < 1e-4f || b.sqrMagnitude < 1e-4f) return float.NaN;
        return -Vector2.SignedAngle(a, b);   // screen y is up, so clockwise on screen is a turn to the right
    }

    // ================================================================== the stick

    void Feed(Vector2 stick)
    {
        fed = stick;
        if (usePad && pad != null && pad.added) InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
        else if (movement != null) movement.ScriptedInput = stick.sqrMagnitude < 1e-6f ? (Vector2?)null : AsRead(stick);
    }

    // The stick as the game reads it (the Input System's dead zone, 0.125 to 0.925, then PadInput's 0.2), for when the
    // virtual controller can't be used.
    static Vector2 AsRead(Vector2 stick)
    {
        float m = Mathf.Min(1f, stick.magnitude);
        if (m < 1e-4f) return Vector2.zero;
        float a = Mathf.Clamp01((m - .125f) / (.925f - .125f));
        if (a < .2f) return Vector2.zero;
        return stick / stick.magnitude * Mathf.Clamp01((a - .2f) / .8f);
    }

    IEnumerator Hold(Vector2 stick, float seconds)
    {
        int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / Frame));
        for (int i = 0; i < frames; i++)
        {
            Feed(stick);
            yield return null;
        }
    }

    // From one way to the opposite in FlickSeconds, the thumb passing the centre FlickArc to one side (+1: to the
    // right of the way it was pushed, -1: to the left).
    IEnumerator Flick(Vector2 from, float side)
    {
        int frames = Mathf.Max(2, Mathf.RoundToInt(FlickSeconds / Frame));
        for (int i = 1; i <= frames; i++)
        {
            Feed(Flicked(from, side, (float)i / frames));
            yield return null;
        }
    }

    static Vector2 Flicked(Vector2 from, float side, float u)
    {
        Vector2 across = new Vector2(from.y, -from.x) * side;
        return from * Mathf.Cos(Mathf.PI * u) + across * FlickArc * Mathf.Sin(Mathf.PI * u);
    }

    IEnumerator Leg(string name, Vector2 stick, float seconds)
    {
        Pass(name);
        yield return Hold(stick, seconds);
        Pass("between");
        yield return Hold(Vector2.zero, .25f);
    }

    void Pass(string name)
    {
        pass = name;
        passTime = 0f;
    }

    void Settings(bool now)
    {
        if (body == null || !settingsSaved) return;
        body.quickTurn = now ? savedQuickTurn : 0f;
        body.pivotStep = now && savedPivot;
    }

    // ================================================================== the street

    // Ace on his feet at the start of a line, the stick let go.
    IEnumerator Place(Vector3 feet)
    {
        Feed(Vector2.zero);
        bool was = capsule.enabled;
        capsule.enabled = false;
        float lift = capsule.height * .5f - capsule.center.y + .03f;
        ace.position = feet + Vector3.up * lift;
        capsule.enabled = was;
        yield return Frames(36);   // 0.3 s standing: the gait settles to the idle
    }

    bool FindLine(Vector3 spot, Vector3 dir, out Vector3 start)
    {
        // Points round the spot, nearest first, 1 m apart, out to 14 m; the line is centred on the point.
        var points = new List<Vector3>();
        for (int x = -14; x <= 14; x++)
            for (int z = -14; z <= 14; z++)
                points.Add(new Vector3(x, 0f, z));
        points.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        foreach (Vector3 offset in points)
            if (Clear(spot + offset - dir * (LineLength * .5f), dir, out start)) return true;
        start = spot;
        return false;
    }

    // Level ground all along (within 0.12 m), and Ace's capsule (above the height it steps up by) sweeps the whole line,
    // a little behind its start, and 1.2 m to either side of it, without touching anything.
    bool Clear(Vector3 from, Vector3 dir, out Vector3 feet)
    {
        feet = from;
        if (!Ground(from, out float y0)) return false;
        feet = new Vector3(from.x, y0, from.z);
        for (float d = -.5f; d <= LineLength + .51f; d += .5f)
            if (!Ground(feet + dir * d, out float y) || Mathf.Abs(y - y0) > .12f) return false;
        float r = capsule.radius * .95f;
        Vector3 low = feet + Vector3.up * (capsule.stepOffset + r + .05f);
        Vector3 high = feet + Vector3.up * Mathf.Max(capsule.stepOffset + r + .1f, capsule.height - r);
        Vector3 back = -dir * .6f, side = Vector3.Cross(Vector3.up, dir).normalized * 1.2f;
        foreach (Vector3 shift in new[] { Vector3.zero, side, -side })
        {
            Vector3 a = low + back + shift, b = high + back + shift;
            if (Physics.CheckCapsule(a, b, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.CapsuleCast(a, b, r, dir, LineLength + 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        }
        return true;
    }

    static bool Ground(Vector3 at, out float y)
    {
        y = at.y;
        if (!Physics.Raycast(new Vector3(at.x, at.y + 2.5f, at.z), Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)) return false;
        y = hit.point.y;
        return true;
    }

    // ================================================================== helpers

    static float YawOf(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    static Vector3 Dir(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    static float Mid(float a, float b) => float.IsNaN(a) || float.IsNaN(b) ? float.NaN : a + Mathf.DeltaAngle(a, b) * .5f;
    static string P(Vector3 v) => $"({v.x:0.0}, {v.y:0.00}, {v.z:0.0})";

    static float MeanAngle(IEnumerable<float> angles)
    {
        double sin = 0d, cos = 0d;
        int n = 0;
        foreach (float a in angles)
        {
            if (float.IsNaN(a)) continue;
            sin += Math.Sin(a * Mathf.Deg2Rad);
            cos += Math.Cos(a * Mathf.Deg2Rad);
            n++;
        }
        return n == 0 ? float.NaN : (float)(Math.Atan2(sin, cos) * Mathf.Rad2Deg);
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    IEnumerator Guard(IEnumerator section, string name)
    {
        while (true)
        {
            object current;
            try
            {
                if (!section.MoveNext()) yield break;
                current = section.Current;
            }
            catch (Exception e)
            {
                Check(false, $"{name}: threw {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
                yield break;
            }
            yield return current;
        }
    }

    IEnumerator Photo(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        try
        {
            File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), shot.EncodeToJPG(88));
            // And close up: a square half the picture's height, round Ace.
            Camera cam = Camera.main;
            if (cam != null && ace != null)
            {
                Vector3 p = cam.WorldToScreenPoint(ace.position);
                float sx = shot.width / (float)Mathf.Max(1, Screen.width), sy = shot.height / (float)Mathf.Max(1, Screen.height);
                int size = Mathf.Min(shot.width, shot.height) / 2;
                int x = Mathf.Clamp(Mathf.RoundToInt(p.x * sx) - size / 2, 0, shot.width - size);
                int y = Mathf.Clamp(Mathf.RoundToInt(p.y * sy) - size / 2, 0, shot.height - size);
                var close = new Texture2D(size, size, TextureFormat.RGB24, false);
                close.SetPixels(shot.GetPixels(x, y, size, size));
                close.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + "-close.jpg"), close.EncodeToJPG(90));
                Destroy(close);
            }
        }
        finally { Destroy(shot); }
    }

    void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    void Note(string what) => report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");

    void Restore()
    {
        if (restored) return;
        restored = true;
        recording = false;
        Time.captureDeltaTime = 0f;
        if (body != null && settingsSaved)
        {
            body.quickTurn = savedQuickTurn;
            body.pivotStep = savedPivot;
        }
        if (movement != null) movement.ScriptedInput = null;
        if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        pad = null;
        if (view != null)
        {
            Vector3 home = view.OverheadHome;
            view.OrbitTo(home.x, home.y, home.z);
        }
    }

    void Finish()
    {
        Restore();
        if (table.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("In short (steady: degrees off over one stride; flicks: before -> now):");
            foreach (string row in table) report.AppendLine("  " + row);
        }
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {samples.Count} frames in frames.csv; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try
        {
            File.WriteAllText(Path.Combine(folder, "report.txt"), text);
            File.WriteAllText(Path.Combine(folder, "frames.csv"), Csv());
        }
        catch (Exception e) { Debug.LogWarning("[Ace's body] Could not write the turning-round report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Ace's body] Turning round: all {checks} checks passed. {folder}\n{text}");
        else Debug.LogWarning($"[Ace's body] Turning round: {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    string Csv()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("pass,t,fed_x,fed_y,read_x,read_y,cmd_yaw,cmd_speed,travel_yaw,travel_speed,rig_yaw,hips_yaw,shoulders_yaw," +
                                    "facing_error,pivot,gait_speed,w_idle,w_walk,w_run,stride_phase,screen_off,jump\n");
        foreach (Sample s in samples)
        {
            csv.Append(s.pass.Replace(',', ';')).Append(',');
            csv.AppendLine(string.Join(",", new[]
            {
                s.t, s.fed.x, s.fed.y, s.read.x, s.read.y, s.cmdYaw, s.cmdSpeed, s.travelYaw, s.travelSpeed, s.rigYaw, s.hipsYaw, s.shouldersYaw,
                s.facing, s.pivot, s.gait, s.wIdle, s.wWalk, s.wRun, s.phase, s.screenOff, s.jump ? 1f : 0f,
            }.Select(v => float.IsNaN(v) ? "" : v.ToString("0.####", c))));
        }
        return csv.ToString();
    }

    // Leaving Play in the middle puts everything back too.
    void OnDestroy() => Restore();
}
