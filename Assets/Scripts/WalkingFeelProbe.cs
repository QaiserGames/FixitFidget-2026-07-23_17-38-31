#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Cinemachine;
using UnityEngine;

// Measures what the first-person camera actually does while walking.
//
// Editor-only, and never saved in a scene. Fixit Fidget > Checks > Walking feel
// adds it to the Player during Play Mode; it restores every value, position and
// component it touched, then removes itself. (An earlier throwaway probe was
// saved into the café scene by accident and hijacked the player on every Play —
// that is exactly what this design prevents.)
//
// In first person the camera is rigidly attached to the capsule, so the
// capsule's per-frame motion IS the camera's motion. A velocity that jumps in a
// single frame, or drops to zero mid-walk, reads as a stutter. Those are the
// main things measured here, alongside collision interference, vertical pops,
// camera coupling and frame timing.
[AddComponentMenu("")]
[DefaultExecutionOrder(-500)] // scripted input must land before PlayerMovement.Update
public sealed class WalkingFeelProbe : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float SettleSeconds = 0.5f;

    // One scripted lap with human key timing: a square where each corner has a
    // different gap (release one key, then press the next) or overlap (briefly
    // both, i.e. a diagonal), then straight reversals, then a stop.
    struct Segment { public float start, end; public Vector2 input; }
    static readonly Segment[] Lap =
    {
        new Segment { start = 0.000f, end = 0.600f, input = Vector2.up },    // W
        new Segment { start = 0.650f, end = 1.250f, input = Vector2.left },  // A after a 50 ms gap
        new Segment { start = 1.200f, end = 1.800f, input = Vector2.down },  // S with a 50 ms overlap
        new Segment { start = 1.833f, end = 2.430f, input = Vector2.right }, // D after a 33 ms gap
        new Segment { start = 2.447f, end = 3.450f, input = Vector2.up },    // W after a 17 ms gap
        new Segment { start = 3.450f, end = 3.850f, input = Vector2.down },  // reversal, no gap
        new Segment { start = 3.900f, end = 4.300f, input = Vector2.up },    // reversal after a 50 ms gap
        new Segment { start = 4.300f, end = 4.700f, input = Vector2.down },  // reversal, no gap
    };
    const float LapSeconds = 5.1f; // includes the final stop
    // Moments the lap turns 90 degrees. Speed right after these shows stalls.
    static readonly float[] Corners = { 0.600f, 1.200f, 1.800f, 2.430f };
    const float CornerWindow = 0.30f;

    // Before: instant movement and the old 1 mm Min Move Distance, exactly as
    // the game shipped before 23 Sept. After: the scene's current settings.
    enum Variant { Before, After }
    const float OldMinMoveDistance = 0.001f;

    struct Row
    {
        public int frame;
        public float t, dt;
        public Vector2 input;
        public Vector3 commanded, position, camera;
        public float cameraError;
        public bool grounded;
        public int fixedSteps;
    }

    sealed class Result
    {
        public string name;
        public bool judged;
        public bool passed;
        public int frames;
        public float medianDt, p95Dt, maxDt;
        public int hitches;
        public bool scripted;
        public int frozenFrames; // asked to move, but the capsule did not move at all
        public float maxAcceleration, maxCommandDeviation, minCornerSpeed, cornerStallSeconds;
        public float maxVerticalStep, verticalRange, maxCameraError;
        public List<string> notes = new List<string>();
        public string csv;
    }

    PlayerMovement movement;
    CafeViewMode view;
    CharacterController body;
    Camera mainCamera;
    CinemachineBrain brain;
    FieldInfo moveInputField, accelerationField, brakingField;

    readonly List<Row> rows = new List<Row>();
    bool recording, injecting;
    float clock;
    int lastRecordedFrame = -1, fixedSteps;
    string folder;

    // Everything we change, so we can put it back.
    bool restored = true;
    Vector3 startPosition;
    Quaternion startRotation;
    bool startFirstPerson;
    float startAcceleration, startBraking, startMinMove;
    GameObject testFloor;

    public void BeginCornerSuite() => StartCoroutine(CornerSuite());
    public void BeginRecording(float seconds) => StartCoroutine(RecordInPlace(seconds));

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        view = GetComponent<CafeViewMode>();
        body = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        moveInputField = typeof(PlayerMovement).GetField("moveInput", Private);
        accelerationField = typeof(PlayerMovement).GetField("firstPersonAcceleration", Private);
        brakingField = typeof(PlayerMovement).GetField("firstPersonBraking", Private);
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "WalkingFeel",
            DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Application.onBeforeRender += Record;
    }

    void OnDestroy()
    {
        Application.onBeforeRender -= Record;
        Restore();
    }

    void FixedUpdate() => fixedSteps++;

    // Runs before PlayerMovement (execution order -500).
    void Update()
    {
        if (!injecting) return;
        clock += Time.deltaTime;
        Vector2 input = Vector2.zero;
        foreach (var segment in Lap)
            if (clock >= segment.start && clock < segment.end) input += segment.input;
        // The keyboard composite normalises two held keys into a diagonal.
        if (input.sqrMagnitude > 1f) input.Normalize();
        moveInputField.SetValue(movement, input);
    }

    // After every LateUpdate (including Cinemachine), before rendering:
    // this is the pose the player actually sees this frame.
    void Record()
    {
        if (!recording || Time.frameCount == lastRecordedFrame || movement == null) return;
        lastRecordedFrame = Time.frameCount;
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 position = transform.position;
        Vector3 eye = position + Vector3.up * (body.center.y - body.height * 0.5f + view.eyeHeight);
        Vector3 cameraPosition = mainCamera != null ? mainCamera.transform.position : eye;
        rows.Add(new Row
        {
            frame = Time.frameCount,
            t = clock,
            dt = Time.deltaTime,
            input = (Vector2)moveInputField.GetValue(movement),
            commanded = movement.CommandedVelocity,
            position = position,
            camera = cameraPosition,
            // Mid-blend (e.g. just after V) the camera is legitimately between views.
            cameraError = view.WalkingFirstPerson && (brain == null || !brain.IsBlending) ? Vector3.Distance(cameraPosition, eye) : 0f,
            grounded = body.isGrounded,
            fixedSteps = fixedSteps,
        });
        fixedSteps = 0;
    }

    IEnumerator CornerSuite()
    {
        if (!Prepare(out string problem)) { Abort(problem); yield break; }
        restored = false;
        startPosition = transform.position;
        startRotation = transform.rotation;
        startFirstPerson = view.FirstPersonSelected;
        startAcceleration = (float)accelerationField.GetValue(movement);
        startBraking = (float)brakingField.GetValue(movement);
        startMinMove = body.minMoveDistance;

        if (!view.SetFirstPerson(true)) { Abort("Could not switch to first person (a station, conversation or recap owns input)."); yield break; }
        // Let the isometric-to-first-person camera blend start, then finish.
        yield return null;
        yield return null;
        for (float waited = 0f; brain != null && brain.IsBlending && waited < 3f; waited += Time.deltaTime) yield return null;

        // A clean, empty floor far from the café isolates the movement code:
        // no furniture, rugs or customers can add their own bumps.
        testFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        testFloor.name = "Walking feel check floor (temporary)";
        testFloor.transform.position = new Vector3(2000f, -0.25f, 2000f);
        testFloor.transform.localScale = new Vector3(40f, 0.5f, 40f);
        Vector3 lapStart = new Vector3(2000f, 1.1f, 2000f);

        var variants = new List<Variant> { Variant.Before, Variant.After };
        var results = new List<Result>();
        Directory.CreateDirectory(folder);

        foreach (Variant variant in variants)
        {
            Apply(variant);
            Teleport(lapStart);
            moveInputField.SetValue(movement, Vector2.zero);
            yield return new WaitForSeconds(SettleSeconds);
            if (Interrupted(out problem)) { Abort(problem); yield break; }

            rows.Clear(); clock = 0f; fixedSteps = 0; lastRecordedFrame = Time.frameCount;
            injecting = true; recording = true;
            while (clock < LapSeconds)
            {
                yield return null;
                if (Interrupted(out problem)) { Abort(problem); yield break; }
            }
            injecting = false; recording = false;
            moveInputField.SetValue(movement, Vector2.zero);
            results.Add(Analyse(Name(variant), variant == Variant.After, true));
        }

        Restore();
        Report("Corner test (scripted square + reversals on an empty floor)", results);
        Destroy(this);
    }

    IEnumerator RecordInPlace(float seconds)
    {
        if (!Prepare(out string problem)) { Abort(problem); yield break; }
        Directory.CreateDirectory(folder);
        Debug.Log($"[Walking feel] Recording {seconds:0} s of your own walking — walk your square now.");
        rows.Clear(); clock = 0f; fixedSteps = 0; lastRecordedFrame = Time.frameCount;
        recording = true;
        while (clock < seconds)
        {
            yield return null;
            clock += Time.deltaTime;
        }
        recording = false;
        var result = Analyse(view.WalkingFirstPerson ? "Your walking (first person)" : "Your walking (isometric)", false, false);
        Report("Recorded walk (real keyboard input, real café)", new List<Result> { result });
        Destroy(this);
    }

    bool Prepare(out string problem)
    {
        problem = null;
        if (movement == null || view == null || body == null) problem = "Player is missing PlayerMovement, CafeViewMode or CharacterController.";
        else if (moveInputField == null || accelerationField == null || brakingField == null) problem = "PlayerMovement fields changed; update WalkingFeelProbe.";
        else if (DayClock.Instance != null && DayClock.Instance.DayOver) problem = "The day is over (recap is open). Continue to the next day, then run the check.";
        else if (Time.timeScale <= 0f) problem = "The game is paused. Unpause, then run the check.";
        return problem == null;
    }

    bool Interrupted(out string problem)
    {
        problem = null;
        if (DayClock.Instance != null && DayClock.Instance.DayOver) problem = "The day ended during the check.";
        else if (Time.timeScale <= 0f) problem = "The game was paused during the check.";
        else if (!view.WalkingFirstPerson) problem = "First-person walking was interrupted (station, conversation or Esc).";
        return problem != null;
    }

    void Apply(Variant variant)
    {
        bool before = variant == Variant.Before;
        // A huge rate makes MoveTowards snap, which is exactly the old code.
        accelerationField.SetValue(movement, before ? 1e6f : startAcceleration);
        brakingField.SetValue(movement, before ? 1e6f : startBraking);
        body.minMoveDistance = before ? OldMinMoveDistance : startMinMove;
    }

    static string Name(Variant variant) => variant == Variant.Before
        ? "Before the fix (instant movement, 1 mm Min Move Distance)"
        : "After the fix (current settings)";

    void Teleport(Vector3 position)
    {
        // The supported way to relocate a CharacterController.
        body.enabled = false;
        transform.position = position;
        body.enabled = true;
    }

    void Restore()
    {
        if (restored) return;
        restored = true;
        injecting = false; recording = false;
        if (movement != null)
        {
            moveInputField.SetValue(movement, Vector2.zero);
            accelerationField.SetValue(movement, startAcceleration);
            brakingField.SetValue(movement, startBraking);
            movement.ClearInput();
        }
        if (body != null)
        {
            body.minMoveDistance = startMinMove;
            body.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            body.enabled = true;
        }
        if (view != null && view.FirstPersonSelected != startFirstPerson) view.SetFirstPerson(startFirstPerson);
        if (testFloor != null) Destroy(testFloor);
    }

    void Abort(string problem)
    {
        Restore();
        Debug.LogWarning("[Walking feel] Check stopped: " + problem + " Nothing was changed.");
        Destroy(this);
    }

    Result Analyse(string name, bool judged, bool scripted)
    {
        var r = new Result { name = name, judged = judged, scripted = scripted, frames = rows.Count };
        if (rows.Count < 10) { r.notes.Add("Too few frames recorded."); return r; }

        var dts = new List<float>();
        foreach (var row in rows) dts.Add(row.dt);
        dts.Sort();
        r.medianDt = dts[dts.Count / 2];
        r.p95Dt = dts[Mathf.Min(dts.Count - 1, (int)(dts.Count * 0.95f))];
        r.maxDt = dts[dts.Count - 1];

        Vector3 previousVelocity = Vector3.zero;
        float minY = float.MaxValue, maxY = float.MinValue;
        float[] cornerMin = new float[Corners.Length];
        for (int c = 0; c < cornerMin.Length; c++) cornerMin[c] = float.MaxValue;

        var csv = new StringBuilder("frame,t,dt,inputX,inputY,cmdVx,cmdVz,posX,posY,posZ,vx,vz,speed,accel,cmdDeviation,dy,cameraError,grounded,fixedSteps\n");
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.dt > Mathf.Max(2f * r.medianDt, 1f / 30f)) r.hitches++;
            minY = Mathf.Min(minY, row.position.y); maxY = Mathf.Max(maxY, row.position.y);
            r.maxCameraError = Mathf.Max(r.maxCameraError, row.cameraError);

            Vector3 velocity = Vector3.zero; float acceleration = 0f, deviation = 0f, dy = 0f;
            if (i > 0 && row.dt > 0f)
            {
                Vector3 step = row.position - rows[i - 1].position;
                dy = step.y;
                velocity = new Vector3(step.x, 0f, step.z) / row.dt;
                Vector3 commanded = new Vector3(row.commanded.x, 0f, row.commanded.z);
                deviation = (velocity - commanded).magnitude;
                if (i > 1 && commanded.magnitude > 0.02f && velocity.sqrMagnitude < 1e-8f) r.frozenFrames++;
                if (i > 1)
                {
                    acceleration = (velocity - previousVelocity).magnitude / row.dt;
                    r.maxAcceleration = Mathf.Max(r.maxAcceleration, acceleration);
                    r.maxVerticalStep = Mathf.Max(r.maxVerticalStep, Mathf.Abs(dy));
                    r.maxCommandDeviation = Mathf.Max(r.maxCommandDeviation, deviation);
                }
                previousVelocity = velocity;
            }

            float speed = velocity.magnitude;
            if (scripted)
            {
                for (int c = 0; c < Corners.Length; c++)
                {
                    if (row.t < Corners[c] || row.t > Corners[c] + CornerWindow) continue;
                    cornerMin[c] = Mathf.Min(cornerMin[c], speed);
                    if (speed < 1f) r.cornerStallSeconds += row.dt;
                }
            }

            csv.Append(row.frame).Append(',').Append(F(row.t)).Append(',').Append(F(row.dt)).Append(',')
               .Append(F(row.input.x)).Append(',').Append(F(row.input.y)).Append(',')
               .Append(F(row.commanded.x)).Append(',').Append(F(row.commanded.z)).Append(',')
               .Append(F(row.position.x)).Append(',').Append(F(row.position.y)).Append(',').Append(F(row.position.z)).Append(',')
               .Append(F(velocity.x)).Append(',').Append(F(velocity.z)).Append(',').Append(F(speed)).Append(',')
               .Append(F(acceleration)).Append(',').Append(F(deviation)).Append(',').Append(F(dy)).Append(',')
               .Append(F(row.cameraError)).Append(',').Append(row.grounded ? 1 : 0).Append(',').Append(row.fixedSteps).Append('\n');
        }
        r.verticalRange = maxY - minY;
        r.minCornerSpeed = float.MaxValue;
        foreach (float m in cornerMin) r.minCornerSpeed = Mathf.Min(r.minCornerSpeed, m);
        if (!scripted) r.minCornerSpeed = -1f;

        if (!scripted) { DescribeKeyChanges(r); DescribeWorstMoments(r); }

        string file = Path.Combine(folder, Slug(name) + ".csv");
        File.WriteAllText(file, csv.ToString());
        r.csv = file;

        if (judged)
        {
            float limit = 1.5f * Mathf.Max(startAcceleration, startBraking);
            Check(r, r.maxAcceleration <= limit, $"velocity never jumps: max {r.maxAcceleration:0} m/s² (limit {limit:0})");
            Check(r, r.minCornerSpeed >= 1f, $"no stall at 90° corners: slowest {r.minCornerSpeed:0.00} m/s (need ≥ 1)");
            Check(r, r.frozenFrames == 0, $"capsule never ignores a move: {r.frozenFrames} frozen frames");
            Check(r, r.maxCommandDeviation <= 0.25f, $"capsule does what it is told on an empty floor: worst {r.maxCommandDeviation:0.000} m/s off");
            Check(r, r.maxVerticalStep <= 0.005f, $"no vertical pops: largest {r.maxVerticalStep * 1000f:0.0} mm in one frame");
            Check(r, r.maxCameraError <= 0.002f, $"camera locked to the body: {r.maxCameraError * 1000f:0.00} mm");
            r.passed = r.notes.TrueForAll(n => n.StartsWith("PASS"));
        }
        return r;
    }

    // For recorded (real keyboard) walks: how long were the gaps between
    // releasing one direction and pressing the next? Human gaps of 30–100 ms
    // are what used to freeze the old instant movement.
    void DescribeKeyChanges(Result r)
    {
        float gapStart = -1f; bool wasHeld = false; var gaps = new List<float>();
        foreach (var row in rows)
        {
            bool held = row.input.sqrMagnitude > 0.0001f;
            if (wasHeld && !held) gapStart = row.t;
            if (!wasHeld && held && gapStart >= 0f) { float gap = row.t - gapStart; if (gap < 0.3f) gaps.Add(gap); gapStart = -1f; }
            wasHeld = held;
        }
        if (gaps.Count == 0) { r.notes.Add("Key changes: no short release-then-press gaps recorded."); return; }
        gaps.Sort();
        r.notes.Add($"Key changes: {gaps.Count} short gaps between keys, median {gaps[gaps.Count / 2] * 1000f:0} ms, longest {gaps[gaps.Count - 1] * 1000f:0} ms.");
    }

    // Where in a real walk the biggest jolts and collision pushes happened, so
    // they can be matched to a place in the café (a rug edge, a chair, a door).
    void DescribeWorstMoments(Result r)
    {
        var jolts = new List<(float value, float t, Vector3 at)>();
        var pushes = new List<(float value, float t, Vector3 at)>();
        Vector3 previous = Vector3.zero;
        for (int i = 1; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.dt <= 0f) continue;
            Vector3 step = row.position - rows[i - 1].position;
            Vector3 velocity = new Vector3(step.x, 0f, step.z) / row.dt;
            if (i > 1) jolts.Add(((velocity - previous).magnitude / row.dt, row.t, row.position));
            pushes.Add(((velocity - new Vector3(row.commanded.x, 0f, row.commanded.z)).magnitude, row.t, row.position));
            previous = velocity;
        }
        jolts.Sort((a, b) => b.value.CompareTo(a.value));
        pushes.Sort((a, b) => b.value.CompareTo(a.value));
        for (int i = 0; i < Mathf.Min(3, jolts.Count); i++)
            r.notes.Add($"Jolt #{i + 1}: {jolts[i].value:0} m/s² at {jolts[i].t:0.00} s near ({jolts[i].at.x:0.0}, {jolts[i].at.z:0.0})");
        for (int i = 0; i < Mathf.Min(3, pushes.Count); i++)
            r.notes.Add($"Collision push #{i + 1}: {pushes[i].value:0.00} m/s off course at {pushes[i].t:0.00} s near ({pushes[i].at.x:0.0}, {pushes[i].at.z:0.0})");
    }

    static void Check(Result r, bool ok, string text) => r.notes.Add((ok ? "PASS " : "FAIL ") + text);

    void Report(string title, List<Result> results)
    {
        var text = new StringBuilder();
        text.AppendLine("Walking feel — " + title);
        text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            + $"   top speed {Read("moveSpeed"):0.##} m/s, acceleration {Read("firstPersonAcceleration"):0} m/s², braking {Read("firstPersonBraking"):0} m/s²");
        text.AppendLine();
        foreach (var r in results)
        {
            text.AppendLine(r.name + (r.judged ? (r.passed ? "  — PASS" : "  — FAIL") : ""));
            text.AppendLine($"  frames {r.frames}, median {r.medianDt * 1000f:0.0} ms ({(r.medianDt > 0 ? 1f / r.medianDt : 0):0} fps), 95th pct {r.p95Dt * 1000f:0.0} ms, worst {r.maxDt * 1000f:0.0} ms, hitches {r.hitches}");
            text.AppendLine($"  max velocity change {r.maxAcceleration:0} m/s², {(r.scripted ? "frozen frames" : "blocked frames (walking into something)")} {r.frozenFrames}, worst command deviation {r.maxCommandDeviation:0.000} m/s, vertical pop {r.maxVerticalStep * 1000f:0.0} mm (range {r.verticalRange * 1000f:0.0} mm), camera error {r.maxCameraError * 1000f:0.00} mm");
            if (!r.scripted) text.AppendLine("  (In a real walk, jolts and pushes also come from furniture and customers. The positions below say where.)");
            if (r.minCornerSpeed >= 0f) text.AppendLine($"  slowest speed at a 90° corner {r.minCornerSpeed:0.00} m/s, time below 1 m/s at corners {r.cornerStallSeconds * 1000f:0} ms");
            foreach (string note in r.notes) text.AppendLine("  " + note);
            text.AppendLine("  data: " + r.csv);
            text.AppendLine();
        }
        string summary = Path.Combine(folder, "summary.txt");
        File.WriteAllText(summary, text.ToString());
        bool anyFail = results.Exists(r => r.judged && !r.passed);
        string log = "[Walking feel] " + (anyFail ? "FAIL" : "Done") + " — report: " + summary + "\n" + text;
        if (anyFail) Debug.LogWarning(log); else Debug.Log(log);
    }

    // Called after Restore(), so these are the scene's own values.
    float Read(string field) => movement != null ? (float)typeof(PlayerMovement).GetField(field, Private).GetValue(movement) : 0f;

    static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    static string Slug(string s)
    {
        var b = new StringBuilder();
        foreach (char c in s) b.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        return b.ToString().Trim('-');
    }
}
#endif
