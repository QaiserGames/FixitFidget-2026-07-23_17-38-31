using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// A flight recorder for the café's people. Added at runtime by the editor's
// Café life recorder (Fixit Fidget > Café life); never saved into a scene.
//
// trace.csv  - twenty times a second, one row per person inside or near the
//              café: where they are, which way they face, what navigation is
//              asking of them and what it is doing, what their brain and their
//              animator think is happening. The analysis scripts turn this into
//              overlaps, stalls, spins, shared destinations and foot sliding.
// frames     - optional: 12 pictures a second, handed to the editor to encode
//              as an MP4. Either the Game view (what the player sees, with a
//              clock in the corner) or an observer camera of its own, placed
//              close to the café floor so people are big enough to judge.
//              frames.csv gives every picture's trace time.
//
// It only watches. Nothing about anyone's behaviour changes.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(1000)] // after every brain, seat, body and follower has moved
public sealed class CafeLifeProbe : MonoBehaviour
{
    public static CafeLifeProbe Instance { get; private set; }

    /// <summary>Raised on the main thread with each captured Game-view frame (RGBA32, bottom row first).</summary>
    public static event Action<NativeArray<byte>, int, int> FrameReady;

    public float sampleInterval = .05f;
    public bool captureFrames;
    public float frameInterval = 1f / 12f;
    public int frameWidth = 960, frameHeight = 540;
    [Tooltip("Only people inside this box (x/z, metres) are traced, plus anyone with a café brain.")]
    public Rect bounds = new Rect(-12f, -6f, 24f, 24f);

    [Header("Observer camera (instead of the Game view)")]
    public bool observer;
    public Vector3 observerPosition = new Vector3(8.5f, 11f, -5.5f);
    public Vector3 observerTarget = new Vector3(0f, 0f, 7.2f);
    public float observerFov = 50f;

    private StreamWriter writer;
    private readonly StringBuilder row = new StringBuilder(256);
    private float nextSample, nextFrame, startedAt;
    private int rows, frames, pendingReadbacks;
    private RenderTexture screenTexture, smallTexture, observerTexture;
    private bool captureFlip;
    private Camera observerCamera;
    private StreamWriter frameLog;

    // Private state read for the trace only.
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo CState = typeof(CustomerBrain).GetField("state", Any);
    private static readonly FieldInfo CPending = typeof(CustomerBrain).GetField("hasPendingDestination", Any);
    private static readonly FieldInfo CSpot = typeof(CustomerBrain).GetField("waitingSpot", Any);
    private static readonly FieldInfo PState = typeof(PatronBrain).GetField("state", Any);
    private static readonly FieldInfo PSeat = typeof(PatronBrain).GetField("seat", Any);

    private static readonly Dictionary<int, string> AnimNames = new();
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    public string Folder { get; private set; }
    public int Rows => rows;
    public int Frames => frames;
    public float Elapsed => Time.time - startedAt;

    static CafeLifeProbe()
    {
        foreach (string state in new[] { "CharacterArmature|Idle", "CharacterArmature|Walk", "CharacterArmature|Interact",
                     "Sit Down", "Sitting", "Sitting Talk", "Stand Up" })
            AnimNames[Animator.StringToHash(state)] = state.Replace("CharacterArmature|", "");
    }

    /// <summary>Starts writing to <paramref name="folder"/>. Called by the editor recorder.</summary>
    public void Begin(string folder, bool frames)
    {
        Folder = folder;
        captureFrames = frames;
        Directory.CreateDirectory(folder);
        writer = new StreamWriter(Path.Combine(folder, "trace.csv"), false, new UTF8Encoding(false));
        writer.WriteLine("t,f,kind,id,name,state,sub,x,y,z,yaw,en,mesh,stop,path,pend,pstat,vx,vz,dvx,dvz," +
                         "destx,destz,rem,sd,prio,spd,rad,urot,upos,walk,anim,stuck,info,floor");
        startedAt = Time.time;
        nextSample = nextFrame = Time.time;
        Instance = this;
        if (captureFrames)
        {
            frameLog = new StreamWriter(Path.Combine(folder, "frames.csv"), false, new UTF8Encoding(false));
            frameLog.WriteLine("frame,t,day_hour");
            if (observer) CreateObserver();
            StartCoroutine(CaptureLoop());
        }
    }

    // A camera of the recorder's own, so the footage frames the café floor
    // closely whatever the player is looking at. It renders into its own
    // texture and never touches the Game view.
    private void CreateObserver()
    {
        var go = new GameObject("Café life observer camera") { hideFlags = HideFlags.DontSave };
        go.transform.SetPositionAndRotation(observerPosition,
            Quaternion.LookRotation(observerTarget - observerPosition, Vector3.up));
        observerCamera = go.AddComponent<Camera>();
        Camera main = Camera.main;
        if (main != null)
        {
            observerCamera.cullingMask = main.cullingMask;
            observerCamera.clearFlags = main.clearFlags;
            observerCamera.backgroundColor = main.backgroundColor;
        }
        observerCamera.fieldOfView = observerFov;
        observerCamera.nearClipPlane = .1f;
        observerCamera.farClipPlane = 400f;
        observerCamera.depth = -50f;
        observerTexture = new RenderTexture(frameWidth, frameHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        observerCamera.targetTexture = observerTexture;
        var data = observerCamera.GetUniversalAdditionalCameraData();
        var mainData = main != null ? main.GetUniversalAdditionalCameraData() : null;
        if (data != null)
        {
            data.renderPostProcessing = mainData == null || mainData.renderPostProcessing;
            if (mainData != null) data.volumeLayerMask = mainData.volumeLayerMask;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }
    }

    public void End()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Dispose();
            writer = null;
            // What the life layer did while the tape ran (pass 2).
            try { File.WriteAllText(Path.Combine(Folder, "life.txt"), $"after {Elapsed:0.0} s: {NpcAttentionDirector.Summary()}\n"); }
            catch (Exception e) { Debug.LogWarning("[Café life probe] life.txt: " + e.Message, this); }
        }
        captureFrames = false;
        if (frameLog != null) { frameLog.Flush(); frameLog.Dispose(); frameLog = null; }
        if (observerCamera != null) { observerCamera.targetTexture = null; Destroy(observerCamera.gameObject); observerCamera = null; }
        if (observerTexture != null) { observerTexture.Release(); Destroy(observerTexture); observerTexture = null; }
        ReleaseTextures();
    }

    private void OnDestroy()
    {
        End();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (writer == null || Time.time < nextSample) return;
        nextSample = Time.time + sampleInterval;
        try { Sample(); }
        catch (Exception e) { Debug.LogWarning("[Café life probe] Sample failed: " + e.Message, this); }
    }

    // ---------- the trace ----------

    private void Sample()
    {
        float t = Time.time - startedAt;
        int f = Time.frameCount;

        // One row per frame-time sample: how smoothly the game is running.
        Begin(t, f, "F", 0, "frame", "", "");
        row.Append(',').Append(Num(Time.unscaledDeltaTime * 1000f)).Append(",,,,,,,,,,,,,,,,,,,,,,,,,,,");
        Commit();

        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (c == null) continue;
            string state = CState != null ? CState.GetValue(c)?.ToString() ?? "" : "";
            NpcSeating seating = c.GetComponent<NpcSeating>();
            var spot = CSpot?.GetValue(c) as WaitingSpot;
            string info = $"slot={c.SlotIndex};spot={(spot != null ? spot.Kind + "@" + Pos(spot.StandPoint.position) : "-")}" +
                          $";pend={(CPending != null && (bool)CPending.GetValue(c) ? 1 : 0)};acc={(c.WasAccepted ? 1 : 0)}" + Loco(c.gameObject);
            Person(t, f, "C", c.gameObject, c.CustomerName, state, seating, StuckStage(c.gameObject), info, c.enabled);
        }

        foreach (PatronBrain p in FindObjectsByType<PatronBrain>(FindObjectsInactive.Exclude))
        {
            if (p == null) continue;
            string state = PState != null ? PState.GetValue(p)?.ToString() ?? "" : "";
            NpcSeating seating = p.GetComponent<NpcSeating>();
            var seat = PSeat?.GetValue(p) as WaitingSpot;
            string info = $"seat={(seat != null ? Pos(seat.StandPoint.position) : "-")}" + Loco(p.gameObject);
            Person(t, f, "P", p.gameObject, NameOf(p.gameObject), state, seating, StuckStage(p.gameObject), info, p.enabled);
        }

        // People walking to or from the door (off the NavMesh), near the café only.
        foreach (NpcJourney w in NpcJourney.Active)
        {
            if (w == null || !w.isActiveAndEnabled) continue;
            Vector3 p = w.transform.position;
            if (!bounds.Contains(new Vector2(p.x, p.z))) continue;
            Begin(t, f, "W", Id(w.gameObject), NameOf(w.gameObject), w.Arriving ? "arriving" : "leaving",
                w.Waiting ? "waiting:" + w.WaitingFor : "");
            Vector3 v = w.Velocity;
            row.Append(',').Append(Num(p.x)).Append(',').Append(Num(p.y)).Append(',').Append(Num(p.z))
               .Append(',').Append(Num(w.transform.eulerAngles.y))
               .Append(",,,,,,,").Append(Num(v.x)).Append(',').Append(Num(v.z)).Append(",,")
               .Append(',').Append(Num(w.NextTarget.x)).Append(',').Append(Num(w.NextTarget.z))
               .Append(",,,,,").Append(Num(w.Radius)).Append(",,");
            AppendAnimator(w.GetComponentInChildren<Animator>());
            row.Append(',').Append(w.Unstuck).Append(',').Append(Csv("held=" + w.HeldBy)).Append(',');
            Commit();
        }

        // Ace.
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace != null)
        {
            Vector3 p = ace.transform.position;
            Vector3 v = ace.CommandedVelocity;
            Begin(t, f, "A", Id(ace.gameObject), "Ace", "", "");
            row.Append(',').Append(Num(p.x)).Append(',').Append(Num(p.y)).Append(',').Append(Num(p.z))
               .Append(',').Append(Num(ace.transform.eulerAngles.y))
               .Append(",,,,,,,").Append(Num(v.x)).Append(',').Append(Num(v.z))
               .Append(",,,,,,,,,,,,,,,,");
            Commit();
        }
    }

    // The shared locomotion's view of the walk (pass 1): what leg, and the last recovery step.
    private static string Loco(GameObject go)
    {
        NpcLocomotion loco = go.GetComponent<NpcLocomotion>();
        if (loco == null) return "";
        NpcLookAt look = go.GetComponent<NpcLookAt>();
        string looking = look != null && look.Looking ? $";look={look.TargetYaw:0}" : "";
        PersonalSpace space = go.GetComponent<PersonalSpace>();
        string gaveWay = space != null && space.GaveWayToPlayer > .005f ? $";gave={space.GaveWayToPlayer:0.00}" : "";
        // The life layer (pass 2): who they are as a mover, and what they are doing with themselves.
        NpcSocial social = go.GetComponent<NpcSocial>();
        string life = "";
        if (social != null)
        {
            NpcSocial.Beat beat = social.CurrentBeat;
            life = $";prof={social.ProfileName};gait={loco.WalkStyle};sit={social.Current}" +
                   (beat != NpcSocial.Beat.None ? $";beat={beat}" : "") + (social.InChat ? ";chat" : "");
            NpcSeating seating = go.GetComponent<NpcSeating>();
            if (seating != null && seating.Seat != null) life += $";chair={seating.Seat.Style}";
        }
        return $";leg={loco.Purpose};rec={loco.LastRecovery}{(loco.GaveUp ? ";gaveup" : "")}{looking}{gaveWay}{life}";
    }

    private static int StuckStage(GameObject go)
    {
        NpcLocomotion loco = go.GetComponent<NpcLocomotion>();
        return loco != null ? loco.StuckStage : 0;
    }

    private void Person(float t, int f, string kind, GameObject go, string name, string state, NpcSeating seating, int stuck, string info, bool onFloor)
    {
        Begin(t, f, kind, Id(go), name, state, seating != null ? seating.Current.ToString() : "");
        Transform tr = go.transform;
        Vector3 p = tr.position;
        row.Append(',').Append(Num(p.x)).Append(',').Append(Num(p.y)).Append(',').Append(Num(p.z))
           .Append(',').Append(Num(tr.eulerAngles.y));
        NavMeshAgent a = go.GetComponent<NavMeshAgent>();
        if (a != null && a.isActiveAndEnabled)
        {
            bool mesh = a.isOnNavMesh;
            row.Append(",1,").Append(mesh ? 1 : 0).Append(',').Append(mesh && a.isStopped ? 1 : 0)
               .Append(',').Append(a.hasPath ? 1 : 0).Append(',').Append(a.pathPending ? 1 : 0)
               .Append(',').Append((int)a.pathStatus);
            Vector3 v = a.velocity, dv = a.desiredVelocity, d = a.destination;
            row.Append(',').Append(Num(v.x)).Append(',').Append(Num(v.z))
               .Append(',').Append(Num(dv.x)).Append(',').Append(Num(dv.z))
               .Append(',').Append(Num(d.x)).Append(',').Append(Num(d.z))
               .Append(',').Append(mesh && a.hasPath ? Num(a.remainingDistance) : "")
               .Append(',').Append(Num(a.stoppingDistance)).Append(',').Append(a.avoidancePriority)
               .Append(',').Append(Num(a.speed)).Append(',').Append(Num(a.radius))
               .Append(',').Append(a.updateRotation ? 1 : 0).Append(',').Append(a.updatePosition ? 1 : 0);
        }
        else row.Append(",0,,,,,,,,,,,,,,,,,,");
        AppendAnimator(go.GetComponentInChildren<Animator>());
        row.Append(',').Append(stuck).Append(',').Append(Csv(info)).Append(',').Append(onFloor ? 1 : 0);
        Commit();
    }

    private void AppendAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            row.Append(",,");
            return;
        }
        bool walking = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == IsWalkingHash) { walking = animator.GetBool(IsWalkingHash); break; }
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        string anim = AnimNames.TryGetValue(info.shortNameHash, out string n) ? n : info.shortNameHash.ToString();
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            anim += ">" + (AnimNames.TryGetValue(next.shortNameHash, out string m) ? m : next.shortNameHash.ToString());
        }
        row.Append(',').Append(walking ? 1 : 0).Append(',').Append(anim).Append('@').Append(Num(info.normalizedTime));
    }

    private void Begin(float t, int f, string kind, int id, string name, string state, string sub)
    {
        row.Clear();
        row.Append(Num(t)).Append(',').Append(f).Append(',').Append(kind).Append(',').Append(id)
           .Append(',').Append(Csv(name)).Append(',').Append(Csv(state)).Append(',').Append(Csv(sub));
    }

    private void Commit()
    {
        writer.WriteLine(row.ToString());
        rows++;
    }

    private static string NameOf(GameObject go)
    {
        CustomerIdentity id = go.GetComponent<CustomerIdentity>();
        return id != null && !string.IsNullOrEmpty(id.DisplayName) ? id.DisplayName : go.name.Replace("(Clone)", "");
    }

    // A stable number per person for this session (GetInstanceID is retired in Unity 6.5).
    private static int Id(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);

    private static string Pos(Vector3 p) => Num(p.x) + ":" + Num(p.z);
    private static string Num(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Csv(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(',', ' ').Replace('\n', ' ');

    // ---------- the footage ----------

    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (captureFrames)
        {
            yield return endOfFrame;
            if (!captureFrames) break;
            if (Time.unscaledTime < nextFrame || pendingReadbacks > 3) continue;
            nextFrame = Time.unscaledTime + frameInterval;
            CaptureFrame();
        }
    }

    private void CaptureFrame()
    {
        if (observerCamera != null && observerTexture != null)
        {
            // Rendered this frame by the observer camera itself.
            LogFrame();
            pendingReadbacks++;
            AsyncGPUReadback.Request(observerTexture, 0, TextureFormat.RGBA32, OnReadback);
            return;
        }
        if (screenTexture == null || screenTexture.width != Screen.width || screenTexture.height != Screen.height)
        {
            ReleaseTextures();
            screenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            smallTexture = new RenderTexture(frameWidth, frameHeight, 0, RenderTextureFormat.ARGB32);
            // Direct3D and Metal hand the screen over upside down.
            captureFlip = SystemInfo.graphicsUVStartsAtTop;
        }
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);
        if (captureFlip) Graphics.Blit(screenTexture, smallTexture, new Vector2(1f, -1f), new Vector2(0f, 1f));
        else Graphics.Blit(screenTexture, smallTexture);
        LogFrame();
        pendingReadbacks++;
        AsyncGPUReadback.Request(smallTexture, 0, TextureFormat.RGBA32, OnReadback);
    }

    // Frames arrive in the order they were asked for, so the n-th line is the n-th picture.
    private void LogFrame()
    {
        if (frameLog == null) return;
        string hour = DayClock.Instance != null ? Num(DayClock.Instance.CurrentHour) : "";
        frameLog.WriteLine($"{requestedFrames},{Num(Time.time - startedAt)},{hour}");
        requestedFrames++;
    }

    private int requestedFrames;

    private void OnReadback(AsyncGPUReadbackRequest request)
    {
        pendingReadbacks--;
        if (request.hasError || !captureFrames) return;
        frames++;
        FrameReady?.Invoke(request.GetData<byte>(), frameWidth, frameHeight);
    }

    private void ReleaseTextures()
    {
        if (screenTexture != null) { screenTexture.Release(); Destroy(screenTexture); }
        if (smallTexture != null) { smallTexture.Release(); Destroy(smallTexture); }
        screenTexture = smallTexture = null;
    }

    // ---------- the clock in the picture ----------

    private GUIStyle clockStyle;

    private void OnGUI()
    {
        if (writer == null) return;
        clockStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        string day = DayClock.Instance != null ? $"  day {DayClock.Instance.Day} {DayClock.Instance.CurrentHour:00.0}h" : "";
        GUI.Box(new Rect(10, 8, 240, 28), $"REC t={Time.time - startedAt:0.0}s{day}", clockStyle);
    }
}
