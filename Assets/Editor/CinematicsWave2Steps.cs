#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// CINEMATICS, WAVE 2 (14 Oct 2026; the cinematics spec, claude cinematics-intro-drinks-zoom §4.2, §4.3, §5)
//
//   Fixit Fidget > Cinematics > Drinks close-up - straight on (scene)
//       His note (14 Oct): "when in isometric view, and getting to the drink station, the overall look and feel is up and
//       diagonal". The cause: on 10 Oct, Playtest 4 > Bench > 1 put the close-up's camera at the top of its search grid,
//       2.09 m up (44 cm above Ace's eye) and turned 34.5° onto the cup stack, so the paddle row slanted 13.9° and the
//       counter's edge ran corner to corner. Now the camera stands where Ace's eyes are: on the dispenser's axis, 10 cm
//       behind the stand point, at CafeViewMode's eye height, the dispenser's own heading, tilted onto the working face's
//       middle, no roll (the lens stays 58°). BeverageLook takes its rest from this turn. The trade-off (spec §4.2.3): turned
//       onto the cup stack, the far end leaves the frame, as turning your head does.
//       One undo group; the checks in numbers (any failure undoes it all and saves nothing); before and after photos and a
//       report in Logs/Cinematics/; the scene saved when they pass; a second run says "already done" and changes nothing.
//       Edit Mode only, the café scene open and saved first (so the change is its own).
//
//   Fixit Fidget > Night > Night zoom - from the bins to Grace's house (lab)
//       NightZoomCheck in a lab night (the man met; your playtest save is not used). Play mode ends by itself.
//
// Playtest 4 > Bench > 1 frames the dispenser by the same rule (StraightOn below), so re-running it can never bring the
// diagonal back.
// ---------------------------------------------------------------------------
public static class CinematicsWave2Steps
{
    const string DrinksMenu = "Fixit Fidget/Cinematics/Drinks close-up - straight on (scene)";
    const string NightZoomMenu = "Fixit Fidget/Night/Night zoom - from the bins to Grace's house (lab)";
    const string Tag = "[Cinematics] ";
    const string DrinksTitle = "Drinks close-up - straight on";

    /// <summary>How far behind the stand point the close-up's camera stands, metres (where Ace's eyes are, leaning back a touch).</summary>
    public const float BehindStand = .10f;
    /// <summary>The paddles must read: the smallest at least this tall at 1080p (it was 70 px at the diagonal pose; the 10 Oct bar was 48).</summary>
    public const float PaddlePixels = 96f;
    /// <summary>The paddle row's slant on a 16:9 screen, degrees at most (it was 13.9).</summary>
    public const float SlantMost = .5f;
    // BeverageLook's own reach from its rest (52° either way, 28° up, 34° down), kept a little inside.
    const float LookYaw = 52f - 4f, LookUp = 28f - 2f, LookDown = 34f - 2f;

    static string LogRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Cinematics");

    // ================================================================== the rule (shared with Playtest 4 > Bench > 1)

    /// <summary>Ace's eye height: CafeViewMode's (1.65 m in the scene), else 1.65.</summary>
    public static float EyeHeight()
    {
        CafeViewMode view = Object.FindObjectsByType<CafeViewMode>(FindObjectsInactive.Include).FirstOrDefault();
        return view != null ? view.eyeHeight : 1.65f;
    }

    /// <summary>
    /// The straight-on pose, in the camera's parent's frame (the dispenser): on the dispenser's axis, <see cref="BehindStand"/>
    /// behind the stand point, at <paramref name="eyeHeight"/> above the stand point's floor, the dispenser's own heading, tilted
    /// down onto the working face's middle, no roll. False (with why) when something it needs is missing.
    /// </summary>
    public static bool StraightOn(StationInteractable station, Transform parent, DispenserFrameCheck.Face face, float eyeHeight,
                                  out Vector3 local, out Quaternion localTurn, out string why)
    {
        local = Vector3.zero;
        localTurn = Quaternion.identity;
        why = "";
        if (station == null || station.StandPoint == null) { why = "the dispenser's station has no stand point"; return false; }
        if (parent == null) { why = "the close-up's camera has no parent (the dispenser)"; return false; }
        if (face == null || !face.Complete) { why = "the dispenser's working face isn't whole"; return false; }
        Vector3 ahead = Vector3.ProjectOnPlane(parent.forward, Vector3.up);
        if (ahead.sqrMagnitude < 1e-6f) { why = "the dispenser is turned on its side"; return false; }
        ahead.Normalize();
        Vector3 stand = station.StandPoint.position;
        // On the axis through the dispenser's origin along its heading, level with the stand point, 10 cm further back.
        float along = Vector3.Dot(stand - parent.position, ahead) - BehindStand;
        Vector3 eye = parent.position + ahead * along;
        eye.y = stand.y + eyeHeight;
        Vector3 middle = face.Middle;
        float forward = Vector3.Dot(middle - eye, ahead);
        if (forward < .2f) { why = "the working face isn't in front of the stand point"; return false; }
        float tilt = Mathf.Atan2(eye.y - middle.y, forward) * Mathf.Rad2Deg;   // down is positive, as Euler's x
        Quaternion turn = Quaternion.LookRotation(ahead, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
        local = parent.InverseTransformPoint(eye);
        localTurn = Quaternion.Inverse(parent.rotation) * turn;
        return true;
    }

    /// <summary>
    /// The checks a straight-on close-up must pass, in numbers (spec §4.2.4): eye height ±5 cm, heading within 1° of the
    /// dispenser's, roll under 0.05°; the paddle row level (0.5° at most); the smallest paddle 96 px or more; the whole face in
    /// the 16:9 frame with the 4% margin; turned onto the cup stack, the stack, Coffee and Tea in frame and the crosshair on the
    /// stack; the look reaches every target; nothing in the way.
    /// </summary>
    public static void CheckStraightOn(StationInteractable station, CinemachineCamera camera, DispenserFrameCheck.Face face, float eyeHeight,
                                       Func<bool, string, bool> check, Action<string> info)
    {
        Transform t = camera.transform;
        Vector3 eye = t.position;
        Quaternion turn = t.rotation;
        float fov = camera.Lens.FieldOfView;
        Transform parent = t.parent;
        Vector3 ahead = parent != null ? Vector3.ProjectOnPlane(parent.forward, Vector3.up).normalized : Vector3.forward;
        float height = eye.y - station.StandPoint.position.y;
        Vector3 flatForward = Vector3.ProjectOnPlane(turn * Vector3.forward, Vector3.up);
        float heading = Vector3.SignedAngle(ahead, flatForward, Vector3.up);
        float roll = Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(turn * Vector3.right, Vector3.up), turn * Vector3.right, turn * Vector3.forward));
        check(Mathf.Abs(height - eyeHeight) <= .05f && Mathf.Abs(heading) <= 1f && roll < .05f,
            $"the close-up's camera is at Ace's eye ({height:0.000} m above the stand point's floor; {eyeHeight:0.00} ±0.05), heading {heading:+0.00;-0.00}° " +
            $"from the dispenser's (1° at most), roll {roll:0.000}° (under 0.05)");
        float slant = DispenserFrameCheck.RowSlant(eye, turn, fov, face);
        check(Mathf.Abs(slant) <= SlantMost, $"the paddle row is level on a 16:9 screen ({slant:+0.00;-0.00}°; {SlantMost:0.0}° at most; it was 13.9° at the diagonal pose)");
        float smallest = DispenserFrameCheck.SmallestPaddle(eye, turn, fov, face, out string which);
        check(smallest >= PaddlePixels, $"the paddles read: the smallest ({which}) is {smallest:0} px tall at 1080p ({PaddlePixels:0} is the bar; it was 70)");
        foreach (var target in face.All)
        {
            float m = DispenserFrameCheck.Inside(eye, turn, fov, DispenserFrameCheck.Box(target.thing));
            check(m >= DispenserFrameCheck.Margin, $"as it opens, {target.name} is in the 16:9 frame, {m:P1} in from the edge");
        }
        // Turned onto the cup stack, where a drink starts (the card says "Aim at the cup stack to take a cup").
        Vector3 stack = DispenserFrameCheck.Box(face.stack).center;
        Quaternion onStack = Quaternion.LookRotation(stack - eye, Vector3.up);
        var near = DispenserFrameCheck.NearStack(face);
        var keep = new List<(string name, Interactable thing)> { ("the cup stack", face.stack) };
        keep.AddRange(near);
        float kept = DispenserFrameCheck.Least(eye, onStack, fov, keep, out string worst);
        check(kept >= 0f, $"turned onto the cup stack, the stack, {string.Join(" and ", near.Select(n => n.name))} are in the frame ({kept:P1} in at the least: {worst})");
        Interactable under = UnderCrosshair(station, eye, onStack);
        check(under == face.stack, $"...and the crosshair is then on the cup stack (under it: {Name(under)})");
        var gone = face.All.Where(x => DispenserFrameCheck.Inside(eye, onStack, fov, DispenserFrameCheck.Box(x.thing)) < 0f).Select(x => x.name).ToList();
        info($"turned onto the cup stack, out of the frame (the trade-off, as turning your head does): {(gone.Count > 0 ? string.Join(", ", gone) : "nothing")}");
        // The look reaches every target from its rest.
        Vector2 rest = AimAssist.Angles(eye, eye + turn * Vector3.forward);
        foreach (var target in face.All)
        {
            Vector2 a = AimAssist.Angles(eye, DispenserFrameCheck.Box(target.thing).center);
            float yaw = Mathf.DeltaAngle(rest.x, a.x), pitch = a.y - rest.y;
            check(Mathf.Abs(yaw) <= LookYaw && pitch >= -LookUp && pitch <= LookDown,
                $"the look reaches {target.name}: {yaw:0}° across, {Mathf.Abs(pitch):0}° {(pitch >= 0f ? "down" : "up")} from its rest (it turns 52° either way, 28° up, 34° down)");
        }
        check(Clear(station, eye, face, out string inTheWay), "nothing solid stands between the camera and any target, and the camera is clear of everything" +
                                                               (inTheWay.Length > 0 ? $" ({inTheWay})" : ""));
        check(Mathf.Abs(fov - 58f) < .01f, $"the lens is untouched ({fov:0}° tall)");
    }

    // What the close-up's crosshair would pick: the first thing to use along the screen's middle, seeing past what isn't one
    // (PlayerInteractor.FindStationTarget's way, its "is it available" aside).
    static Interactable UnderCrosshair(StationInteractable station, Vector3 eye, Quaternion turn)
    {
        foreach (RaycastHit h in Physics.RaycastAll(eye, turn * Vector3.forward, 10f).OrderBy(hit => hit.distance))
        {
            Interactable it = h.collider.GetComponentInParent<Interactable>();
            if (it == null || it is StationInteractable) continue;
            return it;
        }
        return null;
    }

    // Nothing solid (triggers aside) between the eye and any target's middle, and the eye clear of everything; the dispenser's
    // own colliders and Ace's never count (Ace is hidden in the close-up).
    static bool Clear(StationInteractable station, Vector3 eye, DispenserFrameCheck.Face face, out string inTheWay)
    {
        inTheWay = "";
        GameObject player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        bool Ours(Collider c) => c.transform.IsChildOf(station.transform) || player != null && c.transform.IsChildOf(player.transform);
        foreach (Collider c in Physics.OverlapSphere(eye, .08f, ~0, QueryTriggerInteraction.Ignore))
            if (!Ours(c)) { inTheWay = "the camera is inside " + PathOf(c.gameObject); return false; }
        foreach (var t in face.All)
        {
            Vector3 to = DispenserFrameCheck.Box(t.thing).center - eye;
            float length = to.magnitude;
            if (length < .01f) continue;
            foreach (RaycastHit h in Physics.RaycastAll(eye, to / length, length - .01f, ~0, QueryTriggerInteraction.Ignore))
                if (!Ours(h.collider)) { inTheWay = $"{PathOf(h.collider.gameObject)} hides {t.name}"; return false; }
        }
        return true;
    }

    // ================================================================== Drinks close-up - straight on (scene)

    [MenuItem(DrinksMenu)]
    static void DrinksStraightOn()
    {
        var report = new StringBuilder();
        report.AppendLine($"{DrinksTitle}   ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        int passes = 0, failures = 0, group = -1;
        bool began = false, changed = false;
        bool Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            report.AppendLine((ok ? "  PASS   " : "  FAIL   ") + what);
            return ok;
        }
        void Info(string line) => report.AppendLine("  " + line);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != AcesCafeLayoutSetup.ScenePath) throw new InvalidOperationException($"Open the café scene ({AcesCafeLayoutSetup.ScenePath}) first.");
            if (scene.isDirty) throw new InvalidOperationException("The café scene has unsaved changes: save or revert them first, so this step's change is its own.");

            report.AppendLine().AppendLine("Survey");
            BeverageStation[] drinks = Object.FindObjectsByType<BeverageStation>(FindObjectsInactive.Include);
            if (drinks.Length != 1) throw new InvalidOperationException($"Expected one BeverageStation in the café scene, found {drinks.Length}.");
            StationInteractable station = drinks[0].GetComponent<StationInteractable>();
            if (station == null || station.StandPoint == null) throw new InvalidOperationException("The dispenser has no StationInteractable with a stand point.");
            var so = new SerializedObject(station);
            var camera = so.FindProperty("stationCamera")?.objectReferenceValue as CinemachineCamera;
            if (camera == null) throw new InvalidOperationException("The dispenser's station has no close-up camera (its Station Camera is empty).");
            if (camera.GetComponent<BeverageLook>() == null) throw new InvalidOperationException($"{PathOf(camera.gameObject)} has no BeverageLook.");
            Transform t = camera.transform;
            Physics.SyncTransforms();
            var face = DispenserFrameCheck.Face.Of(station);
            if (!face.Complete) throw new InvalidOperationException($"The dispenser's working face isn't whole: {face.paddles.Count} paddles, {face.places.Count} cup places.");
            float eyeHeight = EyeHeight();
            if (!StraightOn(station, t.parent, face, eyeHeight, out Vector3 local, out Quaternion localTurn, out string why))
                throw new InvalidOperationException("Can't work out the straight-on pose: " + why + ".");

            float fov = camera.Lens.FieldOfView;
            Vector3 eyeNow = t.position;
            Quaternion turnNow = t.rotation;
            Info($"{PathOf(t.gameObject)}: at {V(t.localPosition)} in the dispenser's frame ({V(eyeNow)} in the world), turned {V(t.localRotation.eulerAngles)}, {fov:0}° tall");
            Info($"now: {eyeNow.y - station.StandPoint.position.y:0.00} m above the floor (Ace's eye is {eyeHeight:0.00}), the paddle row slants " +
                 $"{DispenserFrameCheck.RowSlant(eyeNow, turnNow, fov, face):0.0}°, the smallest paddle {DispenserFrameCheck.SmallestPaddle(eyeNow, turnNow, fov, face, out _):0} px");
            Vector3 eyeThen = t.parent.TransformPoint(local);
            Quaternion turnThen = t.parent.rotation * localTurn;
            Info($"straight on: {V(local)} in the dispenser's frame ({V(eyeThen)} in the world), turned {V(localTurn.eulerAngles)}: the row slants " +
                 $"{DispenserFrameCheck.RowSlant(eyeThen, turnThen, fov, face):0.0}°, the smallest paddle {DispenserFrameCheck.SmallestPaddle(eyeThen, turnThen, fov, face, out _):0} px");
            bool already = Vector3.Distance(t.localPosition, local) < .001f && Quaternion.Angle(t.localRotation, localTurn) < .05f;
            string folder = Path.Combine(LogRoot, $"drinks-close-up-{DateTime.Now:yyyy-MM-dd_HHmmss}");
            if (!already) Shoot(folder, "1-before", eyeNow, turnNow, fov, report);

            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(DrinksTitle);
            began = true;
            report.AppendLine().AppendLine("Change");
            if (already) Info("the close-up already stands straight on at Ace's eye: nothing to change");
            else
            {
                Undo.RecordObject(t, DrinksTitle);
                Vector3 wasAt = t.localPosition, wasTurn = t.localRotation.eulerAngles;
                t.localPosition = local;
                t.localRotation = localTurn;
                changed = true;
                report.AppendLine($"  CHANGE {PathOf(t.gameObject)}: placed {V(wasAt)} → {V(local)}, turned {V(wasTurn)} → {V(t.localRotation.eulerAngles)} " +
                                  $"({Vector3.Distance(eyeNow, t.position):0.00} m from where it was; BeverageLook takes its rest from this turn)");
            }

            report.AppendLine().AppendLine("Checks");
            Physics.SyncTransforms();
            CheckStraightOn(station, camera, face, eyeHeight, Check, Info);
            if (failures > 0) throw new InvalidOperationException($"{failures} check(s) failed, so the change is undone and nothing is saved.");
            Undo.CollapseUndoOperations(group);
            if (changed)
            {
                Shoot(folder, "2-after", t.position, t.rotation, fov, report);
                Vector3 stack = DispenserFrameCheck.Box(face.stack).center;
                Shoot(folder, "3-after-turned-onto-the-cup-stack", t.position, Quaternion.LookRotation(stack - t.position, Vector3.up), fov, report);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Info("the scene is saved");
            }
            report.AppendLine().AppendLine(changed
                ? $"DONE: the close-up stands straight on; {passes} check(s) passed; the scene is saved. Photos: {folder}. " +
                  "Then Playtest 4 > Bench > 2 (the lab) photographs the real close-up, its blend and the overhead before E."
                : $"NOTHING TO CHANGE: already done. {passes} check(s) passed.");
            Debug.Log(Tag + $"{DrinksTitle}: {(changed ? "done" : "already done")} ({passes} check(s) passed). Report: {Write("drinks-close-up", report)}");
        }
        catch (Exception e)
        {
            if (began) { Undo.RevertAllDownToGroup(group); report.AppendLine("  (the change is undone; nothing is saved)"); }
            report.AppendLine().AppendLine("REFUSED: " + e.Message);
            if (!(e is InvalidOperationException)) report.AppendLine(e.ToString());
            Debug.LogError(Tag + $"{DrinksTitle}: {e.Message}  Report: {Write("drinks-close-up", report)}");
        }
    }

    [MenuItem(DrinksMenu, true)]
    static bool DrinksStraightOnValid() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== Night zoom - the lab

    [MenuItem(NightZoomMenu)]
    static void NightZoomLab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + $"Open the café scene ({AcesCafeLayoutSetup.ScenePath}) first.");
            return;
        }
        if (Object.FindObjectsByType<NightZeroSet>(FindObjectsInactive.Include).Length == 0)
        {
            Debug.LogError(Tag + "There are no bins behind the café in this scene (Fixit Fidget > Night > Bins 1).");
            return;
        }
        if (Object.FindObjectsByType<NightWalk>(FindObjectsInactive.Include).Length == 0)
        {
            Debug.LogError(Tag + "No NightWalk in this scene (Fixit Fidget > Night > Night walk 1).");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        // A lab game on Day 2's evening: the man met on Night 0 (so he's at the bins), nothing else of the story.
        SaveCheckpointStorage.Write(path, new SaveData
        {
            day = 2, money = 600, cups = 40, beans = 40, dayCompleted = false,
            night = new NightSaveData { nights = 1, metHim = true },
        });
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightWalk.PendingKey, 1);
        PlayerPrefs.SetInt(NightZoomCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Night zoom - from the bins to Grace's house: a lab night (test save " + path + "; your playtest save is not used). " +
                  "A virtual controller drives Ace and the camera: keep the Game view in front and leave the mouse, keyboard and your own " +
                  "controller alone for about two minutes. Play mode ends by itself; the report, photos and trace.csv go to Logs/Night/night-zoom-<time>/.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(NightZoomMenu, true)]
    static bool NightZoomLabValid() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.GetInt(NightZoomCheck.PendingKey, 0) != 0) { PlayerPrefs.DeleteKey(NightZoomCheck.PendingKey); PlayerPrefs.Save(); }
    }

    // ================================================================== photos, words, the report

    // A view at 1920 x 1080 (16:9: the frame the dispenser is judged in), Ace's stand-in hidden (he is hidden in the close-up).
    static void Shoot(string folder, string name, Vector3 at, Quaternion turn, float fov, StringBuilder report)
    {
        try
        {
            Playtest4BenchSteps.Shoot(folder, name, at, turn, fov);
            report.AppendLine($"  photo {Path.Combine(folder, name + ".png")}");
        }
        catch (Exception e) { report.AppendLine($"  (photo {name} failed: {e.Message})"); }
    }

    static string Write(string label, StringBuilder report)
    {
        Directory.CreateDirectory(LogRoot);
        string path = Path.Combine(LogRoot, $"{label}-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(path, report.ToString());
        File.WriteAllText(Path.Combine(LogRoot, $"{label}-last.txt"), report.ToString());
        return path;
    }

    static string PathOf(GameObject go) => Playtest4Shared.PathOf(go);

    static string Name(Interactable i) => i == null ? "nothing" : PathOf(i.gameObject).Split('/').Last();

    static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
}
#endif
