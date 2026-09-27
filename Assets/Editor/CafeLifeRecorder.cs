#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Café life: watching the café's people, and testing them without touching the
// real playtest save.
//
// RECORDING (Play mode)
//   Fixit Fidget > Café life > Record ...
//   Writes Logs/CafeLife/<date-time>/:
//     trace.csv  - everyone in and around the café, 20 times a second
//                  (see CafeLifeProbe for the columns);
//     video.mp4  - the Game view at 12 frames a second, with a REC clock in the
//                  corner that matches the trace's t column;
//     notes.txt  - when, which scene, lab or not, and what the lab did.
//   Logs/ is ignored by git, so recordings never reach the public repository.
//
// THE LAB (Edit mode to start, Play mode for the stress tests)
//   Fixit Fidget > Café life > Lab > Play a lab session ...
//   One Play session on a separate save (playtest-cafe-lab.json, rewritten at
//   the start of every lab session) with day logs in DayLogs/CafeLab. The real
//   playtest save and its logs are never read or written. See CafeLab.
// ---------------------------------------------------------------------------
public static class CafeLifeRecorder
{
    const string Menu = "Fixit Fidget/Café life/";
    const string LabMenu = Menu + "Lab/";
    const string Tag = "[Café life] ";
    const int VideoWidth = 960, VideoHeight = 540, VideoFps = 12;

    static CafeLifeProbe probe;
    static MediaEncoder encoder;
    static float duration;
    static string folder;
    static int encodedFrames;

    // A lab request that never turned into a Play session (a compile error, say)
    // must not make the next ordinary Play a lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleLabRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(CafeLab.PendingKey))
        {
            PlayerPrefs.DeleteKey(CafeLab.PendingKey);
            PlayerPrefs.Save();
        }
    }

    // ---------- recording ----------

    // Observer camera poses (position, target, field of view), all looking in
    // through the cut-away front of the café.
    static readonly (string name, Vector3 position, Vector3 target, float fov)[] Views =
    {
        ("whole café", new Vector3(8.5f, 11f, -5.5f), new Vector3(0f, 0f, 7.2f), 50f),
        ("counter", new Vector3(4.2f, 5.2f, 4.6f), new Vector3(0f, .8f, 12f), 52f),
        ("tables and door", new Vector3(6.5f, 6.5f, -3.5f), new Vector3(-.5f, .4f, 4.5f), 55f),
        ("lounge", new Vector3(-.8f, 4.4f, .8f), new Vector3(-6.4f, .5f, 5f), 58f),
    };

    [MenuItem(Menu + "Record 3 minutes (Game view)")]
    static void Record3() => Start(180f, true);

    [MenuItem(Menu + "Record 8 minutes (Game view)")]
    static void Record8() => Start(480f, true);

    [MenuItem(Menu + "Record 8 minutes (trace only)")]
    static void Record8Trace() => Start(480f, false);

    [MenuItem(Menu + "Record 8 minutes - whole café camera")]
    static void RecordWhole() => Start(480f, true, 0);

    [MenuItem(Menu + "Record 8 minutes - counter camera")]
    static void RecordCounter() => Start(480f, true, 1);

    [MenuItem(Menu + "Record 8 minutes - tables and door camera")]
    static void RecordTables() => Start(480f, true, 2);

    [MenuItem(Menu + "Record 8 minutes - lounge camera")]
    static void RecordLounge() => Start(480f, true, 3);

    [MenuItem(Menu + "Stop recording")]
    static void StopMenu() => Stop("stopped from the menu");

    [MenuItem(Menu + "Record 3 minutes (Game view)", true)]
    [MenuItem(Menu + "Record 8 minutes (Game view)", true)]
    [MenuItem(Menu + "Record 8 minutes (trace only)", true)]
    [MenuItem(Menu + "Record 8 minutes - whole café camera", true)]
    [MenuItem(Menu + "Record 8 minutes - counter camera", true)]
    [MenuItem(Menu + "Record 8 minutes - tables and door camera", true)]
    [MenuItem(Menu + "Record 8 minutes - lounge camera", true)]
    static bool CanRecord() => EditorApplication.isPlaying && probe == null;

    [MenuItem(Menu + "Stop recording", true)]
    static bool CanStop() => probe != null;

    public static bool Recording => probe != null;
    public static string Folder => folder;

    public static void Start(float seconds, bool video, int view = -1)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning(Tag + "Only in Play mode."); return; }
        if (probe != null) Stop("restarted");

        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CafeLife", stamp);
        Directory.CreateDirectory(folder);
        duration = seconds;
        encodedFrames = 0;

        if (video)
        {
            try
            {
                var attributes = new VideoTrackAttributes
                {
                    frameRate = new MediaRational(VideoFps),
                    width = VideoWidth,
                    height = VideoHeight,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High
                };
                encoder = new MediaEncoder(Path.Combine(folder, "video.mp4"), attributes);
                CafeLifeProbe.FrameReady -= OnFrame;
                CafeLifeProbe.FrameReady += OnFrame;
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "Video could not start (" + e.Message + "); recording the trace only.");
                encoder = null;
                video = false;
            }
        }

        var go = new GameObject("Café life probe (recording)") { hideFlags = HideFlags.DontSave };
        probe = go.AddComponent<CafeLifeProbe>();
        probe.frameWidth = VideoWidth;
        probe.frameHeight = VideoHeight;
        probe.frameInterval = 1f / VideoFps;
        if (view >= 0 && view < Views.Length)
        {
            probe.observer = true;
            probe.observerPosition = Views[view].position;
            probe.observerTarget = Views[view].target;
            probe.observerFov = Views[view].fov;
        }
        probe.Begin(folder, video);

        var notes = new StringBuilder();
        notes.AppendLine($"Café life recording, started {DateTime.Now:yyyy-MM-dd HH:mm:ss}, for {seconds:0} s.");
        notes.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");
        notes.AppendLine($"Lab session: {(CafeLab.Active ? "yes (test save)" : "no (real playtest save)")}" +
                         (CafeLabDirector.Instance != null ? $", autopilot {(CafeLabDirector.Instance.Autopilot ? "on" : "off")}" : ""));
        if (DayClock.Instance != null) notes.AppendLine($"Day {DayClock.Instance.Day}, {DayClock.Instance.CurrentHour:0.0}h on the café clock.");
        notes.AppendLine($"Trace: every {probe.sampleInterval:0.00} s. Video: {(video ? $"{VideoWidth}x{VideoHeight} at {VideoFps} fps, " + (view >= 0 && view < Views.Length ? Views[view].name + " camera" : "Game view") : "none")}.");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), notes.ToString(), new UTF8Encoding(false));

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        Debug.Log(Tag + $"Recording {seconds:0} s to {folder}");
    }

    static void OnFrame(NativeArray<byte> data, int width, int height)
    {
        if (encoder == null) return;
        try
        {
            encoder.AddFrame(width, height, width * 4, TextureFormat.RGBA32, data);
            encodedFrames++;
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "Video frame dropped: " + e.Message);
        }
    }

    static void Tick()
    {
        if (probe == null) { Stop("the recorder disappeared"); return; }
        if (probe.Elapsed >= duration) Stop("time's up");
        // The recap pauses time, so the clock above would never run out.
        else if (DayClock.Instance != null && DayClock.Instance.DayOver) Stop("the day closed");
    }

    static void PlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) Stop("play mode ended");
    }

    public static void Stop(string why)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        CafeLifeProbe.FrameReady -= OnFrame;
        int rows = 0, frames = 0;
        float elapsed = 0f;
        if (probe != null)
        {
            rows = probe.Rows;
            frames = probe.Frames;
            elapsed = probe.Elapsed;
            probe.End();
            Object.Destroy(probe.gameObject);
        }
        probe = null;
        if (encoder != null)
        {
            try { encoder.Dispose(); }
            catch (Exception e) { Debug.LogWarning(Tag + "Video could not be finished: " + e.Message); }
            encoder = null;
        }
        // Only the first stop of a recording writes its line (the menu, the day
        // closing and play mode ending can all ask).
        if (folder != null)
        {
            File.AppendAllText(Path.Combine(folder, "notes.txt"),
                $"Stopped {DateTime.Now:HH:mm:ss} ({why}) after {elapsed:0.0} s: {rows} trace rows, {encodedFrames} video frames.\n",
                new UTF8Encoding(false));
            Debug.Log(Tag + $"Stopped ({why}) after {elapsed:0.0} s: {rows} trace rows, {encodedFrames} of {frames} frames. {folder}");
            folder = null;
        }
    }

    // ---------- the lab ----------

    [MenuItem(LabMenu + "Play a lab session - Day 5, autopilot serves")]
    static void LabDay5Auto() => PlayLab(5, true);

    [MenuItem(LabMenu + "Play a lab session - Day 5, I serve")]
    static void LabDay5Manual() => PlayLab(5, false);

    [MenuItem(LabMenu + "Play a lab session - Day 3, autopilot serves")]
    static void LabDay3Auto() => PlayLab(3, true);

    [MenuItem(LabMenu + "Play a lab session - Day 5, autopilot serves", true)]
    [MenuItem(LabMenu + "Play a lab session - Day 5, I serve", true)]
    [MenuItem(LabMenu + "Play a lab session - Day 3, autopilot serves", true)]
    static bool CanStartLab() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void PlayLab(int day, bool autopilot)
    {
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        var start = new SaveData { day = day, money = 600, cups = 40, beans = 40, dayCompleted = false };
        SaveCheckpointStorage.Write(path, start);
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, autopilot ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log(Tag + $"Lab session: Day {day}, autopilot {(autopilot ? "on" : "off")}, save {path}. " +
                  "Your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    static CafeLabDirector Lab => CafeLabDirector.Instance;

    [MenuItem(LabMenu + "Send 3 customers in at once")]
    static void Burst() => Lab.SendCustomersNow(3);

    [MenuItem(LabMenu + "Send 6 patrons in at once")]
    static void PatronBurst() => Lab.SendPatronsNow(6);

    [MenuItem(LabMenu + "Fill every free seat with patrons")]
    static void FillSeats() => Lab.SendPatronsNow(Mathf.Max(0, WaitingArea.FreeSeats));

    [MenuItem(LabMenu + "All patrons leave now")]
    static void PatronsLeave() => Lab.PatronsLeaveNow();

    [MenuItem(LabMenu + "All customers leave now")]
    static void CustomersLeave() => Lab.CustomersLeaveNow();

    [MenuItem(LabMenu + "Block the centre aisle for 25 seconds")]
    static void BlockAisle() => Lab.BlockAisle(new Vector3(0f, 0f, 6.2f), new Vector3(1.8f, 1.1f, 1.8f), 25f);

    [MenuItem(LabMenu + "Block the door for 15 seconds")]
    static void BlockDoor() => Lab.BlockAisle(new Vector3(0f, 0f, 0.6f), new Vector3(2.6f, 1.1f, 0.8f), 15f);

    [MenuItem(LabMenu + "Put Ace in the doorway")]
    static void AceInDoor() => Lab.MoveAce(new Vector3(0f, 0.05f, 0.9f), 180f);

    [MenuItem(LabMenu + "Put Ace in front of the counter")]
    static void AceAtCounter() => Lab.MoveAce(new Vector3(0f, 0.05f, 11.2f), 180f);

    [MenuItem(LabMenu + "Autopilot on or off")]
    static void ToggleAutopilot() => Lab.Autopilot = !Lab.Autopilot;

    // Down the centre aisle to just inside the door, back up to short of the
    // queue, and down to the door again - through whoever is in the way.
    [MenuItem(LabMenu + "Walk Ace - counter to door and back")]
    static void WalkAce()
    {
        PlayerMovement ace = Object.FindAnyObjectByType<PlayerMovement>();
        if (ace != null && Vector3.Distance(ace.transform.position, new Vector3(0f, 0.05f, 9.6f)) > 4f)
            Lab.MoveAce(new Vector3(0f, 0.05f, 9.6f), 180f);
        Lab.WalkAce(new[] { new Vector3(0f, 0f, 1.3f), new Vector3(0f, 0f, 9.6f), new Vector3(0f, 0f, 1.3f) }, 2f);
    }

    [MenuItem(LabMenu + "Send 3 customers in at once", true)]
    [MenuItem(LabMenu + "Send 6 patrons in at once", true)]
    [MenuItem(LabMenu + "Fill every free seat with patrons", true)]
    [MenuItem(LabMenu + "All patrons leave now", true)]
    [MenuItem(LabMenu + "All customers leave now", true)]
    [MenuItem(LabMenu + "Block the centre aisle for 25 seconds", true)]
    [MenuItem(LabMenu + "Block the door for 15 seconds", true)]
    [MenuItem(LabMenu + "Put Ace in the doorway", true)]
    [MenuItem(LabMenu + "Put Ace in front of the counter", true)]
    [MenuItem(LabMenu + "Autopilot on or off", true)]
    [MenuItem(LabMenu + "Walk Ace - counter to door and back", true)]
    static bool LabRunning() => EditorApplication.isPlaying && CafeLabDirector.Instance != null;
}
#endif
