using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

// ---------------------------------------------------------------------------
// THE PERFORMANCE CHECK (30 Sept 2026; Mansoor: the frame rate drops when he looks around with the
// mouse, and the game should run well on any system. The target he set: 4K at 60 fps.)
//
// Started by Fixit Fidget > Checks > Performance (lab, drives itself), in a café lab session (Day 5,
// the autopilot serving, so the room is busy; the playtest save is never touched). It drives the
// camera the way a player's hand does and measures every frame:
//
//   1. standing still, overhead;                    5. first person, walking and looking;
//   2. overhead, orbiting the café (middle-drag);   6. overhead again, zoomed out, orbiting.
//   3. overhead, zooming in and out;
//   4. first person, looking around (the mouse);
//
// For each phase: frame times (average, the median, the 95th and 99th percentiles, the worst frame),
// the managed memory the frame allocated (the garbage that brings a collection), how many garbage
// collections ran, and the renderer's own counts (draw calls, batches, SetPass calls, triangles,
// shadow casters: editor statistics, the same numbers as the Game view's Stats). With Frame Timing
// Stats on (the menu turns it on), the CPU main thread, render thread and GPU times per frame too.
// And the Profiler's own samplers for the usual suspects (script Updates, physics, animation, the
// render loop, shadows), so a slow frame can be laid at a door without opening the Profiler window.
//
// The verdicts are against the CPU budget of 60 fps (16.7 ms), because in the editor at 1080p the
// frame is CPU-bound: what this measures is the work per frame, which is the same at 4K. The GPU's
// share at 4K is about four times the 1080p GPU time and is reported as such (an estimate). A
// Development Build on the target machine is the only real 4K number.
//
// Report and CSV: Logs/Performance/perf-<time>/. Play Mode stops by itself when it is done.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class PerformanceCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.PerformanceCheck.Pending";
    /// <summary>Frames per phase in the report's verdict: the CPU budget of 60 fps.</summary>
    public const float BudgetMs = 16.7f;

    struct Sample
    {
        public int phase, frame;
        public float dt, cpuMain, cpuRender, gpu;
        public long alloc;
        public int drawCalls, batches, setPass, triangles, shadowCasters;
    }

    sealed class Phase
    {
        public string name;
        public float seconds;
        public Action begin;              // once, as the phase starts (a camera cut)
        public Func<float, bool> drive;   // t in seconds since the phase began; false = phase over
        public readonly List<Sample> samples = new();
        public int gcBefore, gcAfter;
        public readonly Dictionary<string, double> samplerNs = new();
        public int samplerFrames;
    }

    static readonly string[] Samplers =
    {
        "PlayerLoop", "BehaviourUpdate", "LateBehaviourUpdate", "FixedBehaviourUpdate", "Physics.Processing",
        "Physics.Simulate", "Animators.Update", "Director.ProcessFrame", "Camera.Render", "Culling",
        "RenderPipelineManager.DoRenderLoop_Internal", "UniversalRenderPipeline.RenderSingleCameraInternal",
        "Shadows.RenderShadowMap", "MainLightShadow", "AdditionalLightsShadow", "Gfx.WaitForPresentOnGfxThread",
        "Gfx.WaitForRenderThread", "WaitForTargetFPS", "GC.Collect", "GarbageCollector.Collect", "UI.Render",
        "Canvas.SendWillRenderCanvases", "TextMeshPro.GenerateText",
        // Scripts of interest (their Update/LateUpdate samplers, as the Profiler names them).
        "ShopUI.Update()", "TicketRailUI.Update()", "TicketRailUI.LateUpdate()", "PlayerInteractor.Update()",
        "CafeViewMode.Update()", "CafeViewMode.LateUpdate()", "CustomerBrain.Update()", "PatronBrain.Update()",
        "NpcJourney.Update()", "NpcLocomotion.Update()", "NpcSeating.Update()", "NpcLookAt.LateUpdate()",
        "NpcAttentionDirector.Update()", "NpcSocial.Update()", "NpcBeats.Update()", "PolygonNpcVisual.LateUpdate()",
        "StreetLife.Update()", "CafeArrivals.Update()", "CafeCar.Update()", "AceBody.LateUpdate()", "AceFace.LateUpdate()",
        "PlayerMovement.Update()", "DayClock.Update()", "CafeSoundscape.Update()", "SoundPlayer.Update()",
        "HoverTooltipUI.Update()", "Nameplate.LateUpdate()", "DrinkFreshnessRing.LateUpdate()", "CafeDaylight.Update()",
        "NpcPosture.LateUpdate()", "PersonalSpace.Update()", "WaitingArea.Update()", "CounterQueue.Update()",
    };

    static int pending;
    readonly List<Phase> phases = new();
    readonly StringBuilder report = new();
    CafeViewMode view;
    PlayerMovement mover;
    string folder;
    Phase current;
    float phaseStart;
    long lastMemory;
    int problems;
    Recorder[] recorders;
    readonly FrameTiming[] timings = new FrameTiming[1];
    bool frameTimingSeen;
    int vsyncBefore;
    Vector3 startAngle;
    float startYaw, startPitch;
    bool startedFirstPerson;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReadRequest()
    {
        pending = 0;
#if UNITY_EDITOR
        pending = PlayerPrefs.GetInt(PendingKey, 0);
        if (pending != 0)
        {
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
        }
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        if (pending == 0 || !CafeLab.Active) return;
        pending = 0;
        var go = new GameObject("Performance check (this Play session only)");
        go.AddComponent<PerformanceCheck>();
    }

    void Awake()
    {
        view = FindAnyObjectByType<CafeViewMode>();
        mover = view != null ? view.GetComponent<PlayerMovement>() : null;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Performance",
            "perf-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        recorders = new Recorder[Samplers.Length];
        for (int i = 0; i < Samplers.Length; i++) recorders[i] = Recorder.Get(Samplers[i]);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        report.AppendLine("Performance check (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
        report.AppendLine($"{SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB; {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB), {SystemInfo.graphicsDeviceType}");
        // Measured uncapped: VSync (on by default since 30 Sept) would hide everything under the monitor's refresh.
        vsyncBefore = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0;
        report.AppendLine($"Game view {Screen.width}x{Screen.height}; VSync {vsyncBefore} (off for the measurement), target frame rate {Application.targetFrameRate}; quality \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\"; " +
                          $"shadow distance {QualitySettings.shadowDistance:0} m, cascades {QualitySettings.shadowCascades}");
        report.AppendLine(Application.isEditor ? "In the Editor (the editor's own work is in these frames; a Development Build is the honest number)." : "A build.");
        if (view == null || mover == null)
        {
            Line(false, "Ace's camera and walking are in the scene");
            Finish();
            yield break;
        }
        // Let the lab settle: the autopilot fills the room.
        yield return new WaitForSeconds(6f);
        startAngle = view.OverheadAngle;
        startedFirstPerson = view.FirstPersonSelected;
        if (startedFirstPerson) view.SetFirstPerson(false);
        Profiler.enabled = true;

        float yaw0 = startAngle.x, pitch0 = startAngle.y, dist0 = startAngle.z;
        Vector4 limits = view.OverheadLimits;   // pitch min/max, distance min/max
        phases.Add(new Phase { name = "overhead, standing still", seconds = 5f, drive = t => true });
        phases.Add(new Phase { name = "overhead, orbiting (middle-drag)", seconds = 10f, drive = t =>
        {
            view.OrbitTo(yaw0 + t * 36f, pitch0, dist0);   // a full turn in 10 s
            return true;
        }});
        phases.Add(new Phase { name = "overhead, zooming in and out", seconds = 6f, drive = t =>
        {
            float u = .5f - .5f * Mathf.Cos(t / 6f * Mathf.PI * 2f);
            view.OrbitTo(yaw0, pitch0, Mathf.Lerp(limits.z, limits.w, u));
            return true;
        }});
        phases.Add(new Phase { name = "first person, looking around (mouse)", seconds = 8f,
            begin = () => { view.SetFirstPerson(true); startYaw = yaw0; startPitch = 8f; },
            drive = t =>
        {
            view.LookTo(startYaw + t * 45f, startPitch + 20f * Mathf.Sin(t * 1.3f));   // a full turn in 8 s, nodding
            return true;
        }});
        phases.Add(new Phase { name = "first person, walking and looking", seconds = 8f, drive = t =>
        {
            view.LookTo(startYaw + 360f + t * 20f, 5f);
            mover.ScriptedInput = new Vector2(Mathf.Sin(t * 1.1f) * .6f, t < 4f ? .8f : -.8f);   // back and forth along the room
            return true;
        }});
        phases.Add(new Phase { name = "overhead, zoomed out, orbiting", seconds = 8f,
            begin = () => { mover.ScriptedInput = null; view.SetFirstPerson(false); },
            drive = t =>
        {
            view.OrbitTo(yaw0 + 180f + t * 45f, limits.x, limits.w);
            return true;
        }});

        foreach (Phase p in phases)
        {
            p.begin?.Invoke();
            current = p;
            phaseStart = Time.unscaledTime;
            p.gcBefore = GC.CollectionCount(0);
            lastMemory = AllocatedSoFar();
            yield return null;   // the first frame after a camera cut is not measured
            lastMemory = AllocatedSoFar();
            float t;
            while ((t = Time.unscaledTime - phaseStart) < p.seconds)
            {
                p.drive(t);
                yield return null;   // Measure() runs in LateUpdate for this frame
            }
            p.gcAfter = GC.CollectionCount(0);
            current = null;
        }
        mover.ScriptedInput = null;
        view.SetFirstPerson(startedFirstPerson);
        view.OrbitTo(startAngle.x, startAngle.y, startAngle.z);
        Profiler.enabled = false;
        Report();
        Finish();
    }

    void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
    }

    // The frame that just ran (LateUpdate: after every script's Update; the render of this frame is
    // what the next frame's delta time includes).
    void LateUpdate()
    {
        if (current == null) return;
        var s = new Sample
        {
            phase = phases.IndexOf(current), frame = Time.frameCount, dt = Time.unscaledDeltaTime * 1000f,
        };
        long memory = AllocatedSoFar();
        s.alloc = memory >= lastMemory ? memory - lastMemory : 0;
        lastMemory = memory;
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            s.cpuMain = (float)timings[0].cpuMainThreadFrameTime;
            s.cpuRender = (float)timings[0].cpuRenderThreadFrameTime;
            s.gpu = (float)timings[0].gpuFrameTime;
            if (s.cpuMain > 0f || s.gpu > 0f) frameTimingSeen = true;
        }
        EditorStats(ref s);
        current.samples.Add(s);
        current.samplerFrames++;
        for (int i = 0; i < recorders.Length; i++)
        {
            Recorder r = recorders[i];
            if (r == null || !r.isValid) continue;
            long ns = r.elapsedNanoseconds;
            if (ns <= 0) continue;
            current.samplerNs.TryGetValue(Samplers[i], out double sum);
            current.samplerNs[Samplers[i]] = sum + ns;
        }
    }

    // Bytes the main thread has allocated so far: exact where the runtime counts them (Mono does), else
    // the heap's size, which only grows in chunks and overstates a frame's garbage.
    static bool allocatedExact = true;
    static long AllocatedSoFar()
    {
        if (allocatedExact)
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (Exception) { allocatedExact = false; }
        }
        return GC.GetTotalMemory(false);
    }

    static void EditorStats(ref Sample s)
    {
#if UNITY_EDITOR
        Type stats = Type.GetType("UnityEditor.UnityStats, UnityEditor.CoreModule") ?? Type.GetType("UnityEditor.UnityStats, UnityEditor");
        if (stats == null) return;
        s.drawCalls = Stat(stats, "drawCalls");
        s.batches = Stat(stats, "batches");
        s.setPass = Stat(stats, "setPassCalls");
        s.triangles = Stat(stats, "triangles");
        s.shadowCasters = Stat(stats, "shadowCasters");
#endif
    }

#if UNITY_EDITOR
    static readonly Dictionary<string, System.Reflection.PropertyInfo> statProps = new();
    static int Stat(Type stats, string name)
    {
        if (!statProps.TryGetValue(name, out var prop))
        {
            prop = stats.GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            statProps[name] = prop;
        }
        return prop != null ? Convert.ToInt32(prop.GetValue(null)) : 0;
    }
#endif

    void Line(bool ok, string what)
    {
        if (!ok) problems++;
        report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
    }

    void Report()
    {
        var csv = new StringBuilder("phase,frame,dt_ms,cpu_main_ms,cpu_render_ms,gpu_ms,alloc_bytes,draw_calls,batches,setpass,triangles,shadow_casters\n");
        report.AppendLine();
        report.AppendLine($"Budget: {BudgetMs:0.0} ms a frame (60 fps). A phase passes when 95 frames in 100 are inside it and no frame is over twice it " +
                          "(a long frame in which the game's own main thread, render thread and GPU time were all under budget is an editor hiccup: listed, not failed).");
        if (!frameTimingSeen) report.AppendLine("Frame Timing Stats were off (Player Settings > Other Settings): no CPU/GPU split this run.");
        report.AppendLine(allocatedExact ? "Garbage is counted exactly (bytes the main thread allocated)." : "Garbage is the heap's growth, which overstates it (the runtime doesn't count allocations here).");
        report.AppendLine();
        foreach (Phase p in phases)
        {
            var dts = new List<float>();
            long alloc = 0;
            double cpuMain = 0, cpuRender = 0, gpu = 0;
            int timed = 0;
            long drawCalls = 0, batches = 0, setPass = 0, tris = 0, casters = 0;
            foreach (Sample s in p.samples)
            {
                dts.Add(s.dt);
                alloc += s.alloc;
                if (s.cpuMain > 0f || s.gpu > 0f) { cpuMain += s.cpuMain; cpuRender += s.cpuRender; gpu += s.gpu; timed++; }
                drawCalls += s.drawCalls; batches += s.batches; setPass += s.setPass; tris += s.triangles; casters += s.shadowCasters;
                csv.Append(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6},{7},{8},{9},{10},{11}\n",
                    p.name.Replace(',', ';'), s.frame, s.dt, s.cpuMain, s.cpuRender, s.gpu, s.alloc, s.drawCalls, s.batches, s.setPass, s.triangles, s.shadowCasters));
            }
            int n = dts.Count;
            if (n == 0) { Line(false, $"{p.name}: no frames measured"); continue; }
            dts.Sort();
            float avg = 0f; foreach (float d in dts) avg += d; avg /= n;
            float p50 = dts[n / 2], p95 = dts[Mathf.Min(n - 1, Mathf.CeilToInt(n * .95f) - 1)], p99 = dts[Mathf.Min(n - 1, Mathf.CeilToInt(n * .99f) - 1)], max = dts[n - 1];
            int collections = p.gcAfter - p.gcBefore;
            // A frame over twice the budget fails the phase only when the game's own work was over
            // budget in it. In the Editor, a 38 ms frame whose main thread, render thread and GPU add
            // up to 4 ms is the editor's (a window repaint, an import, its own collector); the build
            // never has it (30 Sept: the build held 240 fps at 4K through the same moves). Such frames
            // are listed as hiccups, with the game's share, so nothing is hidden.
            float worstOwn = 0f;
            int hiccups = 0;
            foreach (Sample s in p.samples)
            {
                bool split = s.cpuMain > 0f || s.gpu > 0f;   // the frame has its CPU/GPU split
                float own = split ? Mathf.Max(s.cpuMain + s.cpuRender, s.gpu) : s.dt;
                if (s.dt > BudgetMs * 2f && split && own < BudgetMs) hiccups++;
                else worstOwn = Mathf.Max(worstOwn, s.dt);
            }
            bool ok = p95 <= BudgetMs && worstOwn <= BudgetMs * 2f;
            Line(ok, $"{p.name}: {n} frames, average {avg:0.00} ms ({(avg > 0 ? 1000f / avg : 0f):0} fps), median {p50:0.00}, " +
                     $"95th {p95:0.00}, 99th {p99:0.00}, worst {max:0.00} ms; garbage {alloc / 1024f / Mathf.Max(1, n):0.0} KB a frame, {collections} collection(s)" +
                     (hiccups > 0 ? $"; {hiccups} editor hiccup(s) (long frames with the game's own work under budget)" : ""));
            if (timed > 0)
                report.AppendLine($"      CPU main thread {cpuMain / timed:0.00} ms, render thread {cpuRender / timed:0.00} ms, GPU {gpu / timed:0.00} ms at {Screen.width}x{Screen.height} " +
                                  $"(about {gpu / timed * (3840f * 2160f) / Mathf.Max(1f, Screen.width * Screen.height):0.0} ms at 4K, if fill-bound)");
            report.AppendLine($"      draw calls {drawCalls / n}, batches {batches / n}, SetPass {setPass / n}, triangles {tris / n / 1000}k, shadow casters {casters / n} (averages)");
            // The samplers: what the frame was made of (averaged per frame, the ten biggest).
            var rows = new List<KeyValuePair<string, double>>(p.samplerNs);
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            var top = new StringBuilder();
            int shown = 0;
            foreach (var row in rows)
            {
                if (shown++ >= 12) break;
                top.Append(shown > 1 ? ", " : "").Append(row.Key).Append(' ').Append((row.Value / 1e6 / Mathf.Max(1, p.samplerFrames)).ToString("0.00", CultureInfo.InvariantCulture));
            }
            report.AppendLine("      samplers (ms a frame): " + (top.Length > 0 ? top.ToString() : "none seen (the Profiler wasn't recording)"));
            // The worst frames: where they were and what the renderer was doing.
            var worst = new List<Sample>(p.samples);
            worst.Sort((a, b) => b.dt.CompareTo(a.dt));
            var w = new StringBuilder();
            for (int i = 0; i < Mathf.Min(3, worst.Count); i++)
                w.Append(i > 0 ? "; " : "").Append($"frame {worst[i].frame} {worst[i].dt:0.0} ms (main thread {worst[i].cpuMain:0.0}, render {worst[i].cpuRender:0.0}, GPU {worst[i].gpu:0.0}; garbage {worst[i].alloc / 1024f:0} KB, draw calls {worst[i].drawCalls})");
            report.AppendLine("      worst: " + w);
        }
        // The scripts' share across the whole run, for the refactor list.
        report.AppendLine();
        report.AppendLine("Script samplers over the whole run (ms a frame, averaged; only those seen):");
        var total = new Dictionary<string, double>();
        int frames = 0;
        foreach (Phase p in phases)
        {
            frames += p.samplerFrames;
            foreach (var kv in p.samplerNs) { total.TryGetValue(kv.Key, out double v); total[kv.Key] = v + kv.Value; }
        }
        var all = new List<KeyValuePair<string, double>>(total);
        all.Sort((a, b) => b.Value.CompareTo(a.Value));
        foreach (var kv in all)
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "      {0,-52} {1,8:0.000}", kv.Key, kv.Value / 1e6 / Mathf.Max(1, frames)));
        File.WriteAllText(Path.Combine(folder, "frames.csv"), csv.ToString());
    }

    void Finish()
    {
        QualitySettings.vSyncCount = vsyncBefore;
        report.Insert(0, (problems == 0 ? "All clear.\n" : problems + " phase(s) over budget.\n"));
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = "[Performance] " + (problems == 0 ? "All clear. " : problems + " phase(s) over budget. ") + folder + "\n" + report;
        if (problems == 0) Debug.Log(line); else Debug.LogWarning(line);
        StartCoroutine(Stop());
    }

    IEnumerator Stop()
    {
        yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
