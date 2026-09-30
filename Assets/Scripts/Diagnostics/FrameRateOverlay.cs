using System.Globalization;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE FRAME-RATE OVERLAY (30 Sept 2026): F3 in any build or Play session
//
// A small readout in the top-left corner: frames a second over the last second and the slowest
// 1% of them (the number that says whether it stutters), the frame time in milliseconds, the GPU's
// time when Frame Timing Stats are on, the screen size, VSync, and the quality level. It costs
// nothing while hidden and a few strings a second while shown. Made when a scene loads, kept
// across scenes, never saved. It exists because the target is 4K at 60 fps on a player's machine,
// and the only honest number for that comes from a build on that machine. F4 cycles the quality
// presets (QualityPreset) so the three can be compared on the spot.
// ---------------------------------------------------------------------------
public sealed class FrameRateOverlay : MonoBehaviour
{
    public static FrameRateOverlay Instance { get; private set; }
    public bool Shown { get; private set; }
    /// <summary>Frames a second over the last second, and the frame rate at the slowest 1% of frames.</summary>
    public float Fps { get; private set; }
    public float OnePercentLowFps { get; private set; }

    const int Window = 240;   // frames remembered for the 1% low
    readonly float[] recent = new float[Window];
    int recentCount, recentNext;
    float accumulated, accumulatedFrames, refreshAt;
    string line = "";
    GUIStyle style;
    readonly FrameTiming[] timings = new FrameTiming[1];
    static readonly float[] sorted = new float[Window];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (Instance != null) return;
        // An ordinary object of the Play session (PlaySessionLeftovers): it ends with the session, never lingers.
        var go = new GameObject("Frame-rate overlay (F3)") { hideFlags = PlaySessionLeftovers.RuntimeFlags };
        DontDestroyOnLoad(go);
        go.AddComponent<FrameRateOverlay>();
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Show(bool on) => Shown = on;

    void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) Shown = !Shown;
        // F4: the next quality preset (Low, Medium, High), kept for next time; the readout names it.
        if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
        {
            QualityPreset.Cycle();
            refreshAt = 0f;
            if (!Shown) Debug.Log("[Quality] " + QualityPreset.Current);
        }
        float dt = Time.unscaledDeltaTime;
        recent[recentNext] = dt;
        recentNext = (recentNext + 1) % Window;
        if (recentCount < Window) recentCount++;
        accumulated += dt;
        accumulatedFrames++;
        if (!Shown) return;
        FrameTimingManager.CaptureFrameTimings();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .25f;
        if (accumulated > 0f) Fps = accumulatedFrames / accumulated;
        accumulated = 0f; accumulatedFrames = 0f;
        // The slowest 1% of the remembered frames: their average frame time, as a frame rate.
        System.Array.Copy(recent, sorted, recentCount);
        System.Array.Sort(sorted, 0, recentCount);
        int worst = Mathf.Max(1, recentCount / 100);
        float sum = 0f;
        for (int i = 0; i < worst; i++) sum += sorted[recentCount - 1 - i];
        OnePercentLowFps = sum > 0f ? worst / sum : 0f;
        float gpu = FrameTimingManager.GetLatestTimings(1, timings) > 0 ? (float)timings[0].gpuFrameTime : 0f;
        line = string.Format(CultureInfo.InvariantCulture,
            "{0:0} fps   1% low {1:0}   {2:0.0} ms{3}   {4}x{5}   VSync {6}   {7}",
            Fps, OnePercentLowFps, Fps > 0f ? 1000f / Fps : 0f, gpu > 0f ? $"   GPU {gpu:0.0} ms" : "",
            Screen.width, Screen.height, QualitySettings.vSyncCount > 0 ? "on" : "off",
            QualitySettings.names[QualitySettings.GetQualityLevel()]);
    }

    void OnGUI()
    {
        if (!Shown) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, richText = false, normal = { textColor = Color.white } };
        float scale = Mathf.Max(1f, Screen.height / 1080f);
        Matrix4x4 before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.Box(new Rect(8f, 8f, 560f, 30f), line, style);
        GUI.matrix = before;
    }
}
