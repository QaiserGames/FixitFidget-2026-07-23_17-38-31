#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// PLAYTEST 4, THE BENCH LEAD'S STEPS (10 Oct 2026; the two playtester agents' notes, claude/playtest-4-notes.md)
//
// Fixit Fidget > Playtest 4 > Bench >
//   0 - Check first (read-only): the numbers behind tonight's bench fixes, read from the scene, the prefabs and the code:
//       the words on their plate (each colour's contrast; "Fixed!" was pale green on a lit white screen), the covered parts'
//       hints (the cause, under 90 characters), the cloth in the hand (its pad), and the dispenser: its close-up as it is
//       (at rest, and turned onto the cup stack as a player does to take a cup) and the pose step 1 would give it, with
//       photos; first person at the stand point, looking at the middle and at the cup stack, and what moving the stand point
//       back would cost.
//   1 - Frame the whole dispenser (scene): the drinks close-up's camera (the station's own CinemachineCamera, the one its
//       BeverageLook turns; the look takes its rest from this turn) is moved back and turned onto the cup stack so that the
//       whole working face (six paddles, six cup places, the cup stack, the discard basin) is inside a 16:9 frame with a 4%
//       margin as it opens, and the cup stack and every paddle stay in it when the view is turned onto the far cup place
//       (Latte's). One undo group; checks in numbers; before and after photos; nothing saved; safe to run twice.
//   2 - The dispenser's frame - play check (lab, Play Mode): DispenserFrameCheck, in a lab session.
//
// The first-hour playtester: "Only 4 of the 6 paddles are fully in view: Americano is cut by the screen's edge and Latte is
// off it", in both photos. Both were taken with the crosshair on the cup stack (the Stations lab aims there first, as a
// player must to take a cup): at rest the close-up framed everything, but the first thing a drink needs cut its far end off.
// The close-up now opens on the cup stack, from far enough back that nothing leaves the frame.
// ---------------------------------------------------------------------------
public static class Playtest4BenchSteps
{
    const string Area = Playtest4Shared.Menu + "Bench/";
    const string Tag = "[Playtest 4 · Bench] ";
    const string FrameTitle = "Bench 1 - Frame the whole dispenser";

    // The look's own reach from its rest (BeverageLook: 52° either way, 28° up, 34° down), kept a little inside.
    const float LookYaw = 52f - 4f, LookUp = 28f - 2f, LookDown = 34f - 2f;
    // The grid the close-up's camera is placed on, in the dispenser's own frame (metres: across, up, out in front of it).
    const float GridX0 = -.15f, GridX1 = .35f, GridDX = .025f;
    const float GridY0 = .60f, GridY1 = 1.10f, GridDY = .05f;
    const float GridZ0 = -2.20f, GridZ1 = -1.10f, GridDZ = .02f;
    // Of the poses that frame it all, those within this many pixels (at 1080p) of the biggest paddles; then the nearest.
    const float PixelTie = .5f;
    // The paddles must read: the smallest at least this tall at 1080p.
    const float PaddlePixels = 48f;

    // ================================================================== menus

    [MenuItem(Area + "0 - Check first")]
    static void CheckFirst() => Playtest4Shared.Survey("Bench 0 - Check first", "bench-0-check-first", Survey);

    [MenuItem(Area + "0 - Check first", true)]
    static bool CheckFirstValid() => Playtest4Shared.NotPlaying();

    [MenuItem(Area + "1 - Frame the whole dispenser (scene)")]
    static void FrameDispenser()
    {
        DispenserPlan frame = null;
        Playtest4Shared.Step(FrameTitle, "bench-1-frame-the-dispenser",
            run => frame = SurveyFrame(run, true),
            run => ChangeFrame(run, frame),
            run => CheckFrame(run, frame));
    }

    [MenuItem(Area + "1 - Frame the whole dispenser (scene)", true)]
    static bool FrameDispenserValid() => Playtest4Shared.NotPlaying();

    [MenuItem(Area + "2 - The dispenser's frame - play check (lab, Play Mode)")]
    static void FrameLab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play first."); return; }
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.LogError(Tag + $"Open the café scene ({Playtest4Shared.ScenePath}) first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(DispenserFrameCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "The dispenser's frame - play check: a lab session on a fresh Day 1; Ace opens the drinks close-up and stands at it in " +
                  $"first person by himself. Keep the Game view in front and leave the mouse and keyboard alone until it reports (about 25 s). Test save {path}; " +
                  "your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Area + "2 - The dispenser's frame - play check (lab, Play Mode)", true)]
    static bool FrameLabValid() => Playtest4Shared.NotPlaying();

    // A check asked for that never became a Play session must not run in the next lab session.
    [InitializeOnLoadMethod]
    static void ClearStaleRequests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PlayerPrefs.GetInt(DispenserFrameCheck.PendingKey, 0) != 0) { PlayerPrefs.DeleteKey(DispenserFrameCheck.PendingKey); PlayerPrefs.Save(); }
    }

    // ================================================================== 0 - Check first

    static void Survey(Playtest4Shared.Run run)
    {
        // ---- the words on their plate (B4) ----
        run.Section("The words on their plate (\"Fixed!\" was pale green on a lit white screen)");
        void Contrast(string what, Color colour)
        {
            float c = Juice.ContrastOnPlate(colour);
            run.Check(c >= 4.5f, $"{what}: {c:0.0}:1 on the plate over white, its worst case (4.5:1 is the bar)");
        }
        Contrast("\"Fixed!\" in its pale green", Juice.FixedWordsColour);
        Contrast("the grade after it, Perfect (gold)", Juice.GradeColour(JobGrade.Perfect));
        Contrast("Good (the brand's light green)", Juice.GradeColour(JobGrade.Good));
        Contrast("Passable (warm grey)", Juice.GradeColour(JobGrade.Passable));
        Contrast("\"Back together\" (the band's text)", UiSkin.BandText);
        Contrast("\"New part\" (the brand's light green)", UiSkin.BrandBright);

        // ---- the covered parts' hints (B6) ----
        run.Section("What a covered part says (the cause, not a gate; 90 characters at most)");
        int covered = 0;
        string[] folders = new[] { "Assets/AssetsPrefabs", "Assets/GraceShowcase" }.Where(AssetDatabase.IsValidFolder).ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (ReplaceablePart part in prefab.GetComponentsInChildren<ReplaceablePart>(true))
            {
                if (part.CoveredBy == null) continue;
                covered++;
                string words = ReplaceablePart.CoveredHint(part.DisplayName, part.CoveredBy.DisplayName);
                run.Check(words.Length <= 90 && !words.StartsWith("Remove"),
                    $"{Path.GetFileNameWithoutExtension(path)}: \"{part.DisplayName}: {words}\" ({words.Length} characters)");
            }
        }
        run.Info(covered == 0 ? "No covered part found in the device prefabs." : $"{covered} covered part(s) in the device prefabs.");

        // ---- the cloth in the hand (B3) ----
        run.Section("The cloth in the hand (it covered about two-thirds of the glass)");
        ToolPickup cloth = Object.FindObjectsByType<ToolPickup>(FindObjectsInactive.Include).FirstOrDefault(t => t.tool == ToolType.Cloth);
        if (cloth == null) run.Check(false, "the cloth is on the bench (Fixit Fidget > Bench > Bench 2b puts it there)");
        else
        {
            MeshFilter mf = cloth.GetComponent<MeshFilter>();
            Vector3 folded = mf != null && mf.sharedMesh != null ? Vector3.Scale(mf.sharedMesh.bounds.size, Abs(cloth.transform.lossyScale)) : Vector3.zero;
            var so = new SerializedObject(cloth);
            float pad = run.Prop(so, "padSize").floatValue, thick = run.Prop(so, "padThickness").floatValue;
            run.Info($"folded on the mat it is {folded.x * 1000f:0} x {folded.z * 1000f:0} mm, {folded.y * 1000f:0.0} mm thick ({Playtest4Shared.PathOf(cloth.gameObject)})");
            run.Check(folded.x * pad <= .045f && folded.z * pad <= .05f,
                $"in the hand it is a pad of {folded.x * pad * 1000f:0} x {folded.z * pad * 1000f:0} mm ({pad:P0} of its folded size), {folded.y * thick * 1000f:0.0} mm thick");
        }
        run.Check(PlaceholderSounds.Has("cloth.lift"), "the cloth's lift has its placeholder sound (cloth.lift)");
        run.Info($"the pry goes in at the seam {RemovablePart.PryAngle:0}° up from the cover, its blade {RemovablePart.BladeIn * 1000f:0} mm in under the edge while held (Bench 3 measures it)");

        // ---- the dispenser's close-up (B7) ----
        DispenserPlan f = SurveyFrame(run, false);
        string folder = PhotoFolder(run.Label);
        Shoot(folder, "1-close-up-now-at-rest", f.eyeNow, f.turnNow, f.fov);
        Shoot(folder, "2-close-up-now-on-the-cup-stack", f.eyeNow, Quaternion.LookRotation(f.stackCentre - f.eyeNow, Vector3.up), f.fov);
        if (f.plan != null) Shoot(folder, "3-close-up-as-step-1-would-frame-it", f.plan.eye, f.plan.turn, f.fov);
        run.Info("photos (1920 x 1080): " + folder);

        // ---- first person at the stand point (B7) ----
        run.Section("First person at the dispenser's stand point");
        FirstPerson(run, f, folder);
    }

    // ================================================================== 1 - Frame the whole dispenser

    sealed class DispenserPlan
    {
        public StationInteractable station;
        public CinemachineCamera camera;
        public BeverageLook look;
        public Transform parent, player;
        public DispenserFrameCheck.Face face;
        public List<(string name, Interactable thing)> all, stays;
        public float fov;
        public Vector3 stackCentre, farCentre, eyeNow, localNow;
        public Quaternion turnNow, localTurnNow;
        public Placing now, plan;
        public bool already;
        public string folder;
    }

    sealed class Placing
    {
        public Vector3 local, eye;
        public Quaternion localTurn, turn;
        public float least, kept, smallest, moved;
        public string worst, worstKept, smallestName;
    }

    static DispenserPlan SurveyFrame(Playtest4Shared.Run run, bool forTheStep)
    {
        run.Section("The dispenser's close-up");
        BeverageStation drinks = Playtest4Shared.One<BeverageStation>(run);
        var f = new DispenserPlan { station = drinks.GetComponent<StationInteractable>() };
        run.Require(f.station != null, $"{Playtest4Shared.PathOf(drinks.gameObject)} has no StationInteractable.");
        run.Require(f.station.StandPoint != null, "The dispenser's station has no stand point.");
        var so = new SerializedObject(f.station);
        f.camera = run.Prop(so, "stationCamera").objectReferenceValue as CinemachineCamera;
        run.Require(f.camera != null, "The dispenser's station has no close-up camera (its Station Camera is empty).");
        f.look = f.camera.GetComponent<BeverageLook>();
        run.Require(f.look != null, $"{Playtest4Shared.PathOf(f.camera.gameObject)} has no BeverageLook (the look that turns the close-up, and takes its rest from this turn).");
        f.parent = f.camera.transform.parent;
        run.Require(f.parent != null, "The close-up's camera has no parent to place it in.");
        GameObject player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        f.player = player != null ? player.transform : null;
        f.fov = f.camera.Lens.FieldOfView;
        run.Require(f.fov > 10f && f.fov < 120f, $"The close-up's lens is {f.fov:0.0}° tall: not a field of view to frame with.");

        Physics.SyncTransforms();
        f.face = DispenserFrameCheck.Face.Of(f.station);
        run.Require(f.face.Complete, $"The dispenser's working face isn't whole: {f.face.paddles.Count} paddles, {f.face.places.Count} cup places, " +
                                     $"{(f.face.stack != null ? "a" : "no")} cup stack, {(f.face.basin != null ? "a" : "no")} discard basin (each with a collider).");
        f.all = f.face.All.ToList();
        f.stays = f.face.paddles.Append((name: "the cup stack", thing: (Interactable)f.face.stack)).ToList();
        f.stackCentre = DispenserFrameCheck.Box(f.face.stack).center;
        f.farCentre = DispenserFrameCheck.Box(f.face.FarPlace).center;
        f.eyeNow = f.camera.transform.position;
        f.turnNow = f.camera.transform.rotation;
        f.localNow = f.camera.transform.localPosition;
        f.localTurnNow = f.camera.transform.localRotation;
        run.Info($"{Playtest4Shared.PathOf(f.camera.gameObject)}: at {V(f.localNow)} in the dispenser's frame ({V(f.eyeNow)} in the world), " +
                 $"turned {V(f.localTurnNow.eulerAngles)}, {f.fov:0}° tall");
        run.Info("its face, left to right: " + string.Join(", ", f.face.paddles.Select(p => p.name)) + $"; the far cup place is {f.face.FarName}'s");

        // As it is: at rest, and turned onto the cup stack (where a drink starts: the crosshair picks from the screen's middle).
        Placing rest = Measure(f, f.eyeNow, f.turnNow);
        run.Info($"now, at rest: {rest.least:P1} in from the edge at the least ({rest.worst}); the smallest paddle {rest.smallest:0} px tall at 1080p");
        Quaternion onStack = Quaternion.LookRotation(f.stackCentre - f.eyeNow, Vector3.up);
        Placing stack = Measure(f, f.eyeNow, onStack);
        var cut = f.all.Where(t => DispenserFrameCheck.Inside(f.eyeNow, onStack, f.fov, DispenserFrameCheck.Box(t.thing)) < 0f).Select(t => t.name).ToList();
        run.Info($"now, turned onto the cup stack (the playtesters' photos): {stack.least:P1} at the least ({stack.worst})" +
                 (cut.Count > 0 ? "; cut by the edge or off it: " + string.Join(", ", cut) : ""));

        // The plan: every pose on the grid, judged as the lab judges the frame.
        f.now = Judge(f, f.localNow);
        f.plan = Search(f);
        run.Require(f.plan != null, "No pose on the grid frames the whole dispenser with the margin from the cup stack and keeps the cup stack and the paddles " +
                                    "in view turned onto the far cup place, within the look's own reach, with nothing in the way. Nothing changes; tell Claude.");
        f.already = Vector3.Distance(f.plan.local, f.localNow) < .001f && Quaternion.Angle(f.plan.localTurn, f.localTurnNow) < .1f;
        run.Info($"the frame: {(f.already ? "already at" : forTheStep ? "it goes to" : "step 1 would put it at")} {V(f.plan.local)} in the dispenser's frame ({V(f.plan.eye)} in the world), " +
                 $"turned {V(f.plan.localTurn.eulerAngles)} (onto the cup stack, {Vector3.Distance(f.plan.eye, f.stackCentre):0.00} m from it): " +
                 $"{f.plan.least:P1} in at the least ({f.plan.worst}); turned onto {f.face.FarName}'s cup place the cup stack and the paddles {f.plan.kept:P1} in " +
                 $"({f.plan.worstKept}); the smallest paddle ({f.plan.smallestName}) {f.plan.smallest:0} px tall at 1080p");
        if (forTheStep)
        {
            f.folder = PhotoFolder(run.Label);
            Shoot(f.folder, "1-before", f.eyeNow, f.turnNow, f.fov);
            run.Info("before: " + Path.Combine(f.folder, "1-before.png"));
        }
        return f;
    }

    static void ChangeFrame(Playtest4Shared.Run run, DispenserPlan f)
    {
        if (f.already) { run.Info("the close-up already opens on the cup stack with the whole dispenser in frame: nothing to change"); return; }
        Transform t = f.camera.transform;
        run.Record(t);
        Vector3 wasAt = t.localPosition;
        Vector3 wasTurn = t.localRotation.eulerAngles;
        t.localPosition = f.plan.local;
        t.localRotation = f.plan.localTurn;
        run.Changed($"{Playtest4Shared.PathOf(t.gameObject)}: placed at {V(wasAt)} → {V(t.localPosition)} in the dispenser's frame, turned {V(wasTurn)} → {V(t.localRotation.eulerAngles)} " +
                    $"(onto the cup stack, {Vector3.Distance(f.eyeNow, t.position):0.00} m from where it was; BeverageLook takes its rest from this turn)");
    }

    static void CheckFrame(Playtest4Shared.Run run, DispenserPlan f)
    {
        Transform t = f.camera.transform;
        Physics.SyncTransforms();
        Vector3 eye = t.position;
        Quaternion turn = t.rotation;
        run.Check(Vector3.Distance(t.localPosition, f.plan.local) < .0005f && Quaternion.Angle(t.localRotation, f.plan.localTurn) < .05f,
            $"the close-up's camera is where the plan put it ({V(t.localPosition)}, {V(t.localRotation.eulerAngles)})");
        run.Check(Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(turn * Vector3.right, Vector3.up), turn * Vector3.right, turn * Vector3.forward)) < .05f,
            "it isn't rolled (the look keeps a level horizon)");

        Placing p = Measure(f, eye, turn);
        foreach (var target in f.all)
        {
            float m = DispenserFrameCheck.Inside(eye, turn, f.fov, DispenserFrameCheck.Box(target.thing));
            run.Check(m >= DispenserFrameCheck.Margin, $"as it opens, {target.name} is in the 16:9 frame, {m:P1} in from the edge");
        }
        Interactable first = UnderCrosshair(f, eye, turn);
        run.Check(first == f.face.stack, $"it opens with the crosshair on the cup stack, where a drink starts (under it: {Name(first)})");

        Quaternion onFar = Quaternion.LookRotation(f.farCentre - eye, Vector3.up);
        float kept = DispenserFrameCheck.Least(eye, onFar, f.fov, f.stays, out string worstKept);
        run.Check(kept >= DispenserFrameCheck.Margin,
            $"turned onto {f.face.FarName}'s cup place (the far end), the cup stack and every paddle are still in the frame, {kept:P1} in at the least ({worstKept})");
        Interactable onFarThing = UnderCrosshair(f, eye, onFar);
        run.Check(onFarThing == f.face.FarPlace, $"...and the crosshair is then on {f.face.FarName}'s cup place (under it: {Name(onFarThing)})");

        Vector2 rest = AimAssist.Angles(eye, f.stackCentre);
        foreach (var target in f.all)
        {
            Vector2 a = AimAssist.Angles(eye, DispenserFrameCheck.Box(target.thing).center);
            float yaw = Mathf.DeltaAngle(rest.x, a.x), pitch = a.y - rest.y;
            run.Check(Mathf.Abs(yaw) <= LookYaw && pitch >= -LookUp && pitch <= LookDown,
                $"the look reaches {target.name}: {yaw:0}° across, {pitch:0}° {(pitch >= 0f ? "down" : "up")} from its rest (it turns 52° either way, 28° up, 34° down)");
        }
        run.Check(Clear(f, eye, out string inTheWay), "nothing solid stands between the camera and any of them, and the camera is clear of everything" + (inTheWay.Length > 0 ? $" ({inTheWay})" : ""));
        run.Check(p.smallest >= PaddlePixels,
            $"the paddles read: the smallest ({p.smallestName}) is {p.smallest:0} px tall at 1080p ({PaddlePixels:0} is the bar; at the old pose turned onto the cup stack they were bigger, but two were off the screen)");
        run.Check(Mathf.Approximately(f.camera.Lens.FieldOfView, f.fov), $"the lens is untouched ({f.fov:0}° tall)");
        var so = new SerializedObject(f.station);
        run.Check(so.FindProperty("stationCamera").objectReferenceValue == f.camera && f.camera.GetComponent<BeverageLook>() == f.look,
            "the station still opens this camera, and its look is on it");

        if (f.folder != null)
        {
            Shoot(f.folder, "2-after", eye, turn, f.fov);
            Shoot(f.folder, "3-after-on-the-far-cup-place", eye, onFar, f.fov);
            run.Info("after: " + Path.Combine(f.folder, "2-after.png") + " and 3-after-on-the-far-cup-place.png (Play Mode: Bench 2, the lab, photographs the real close-up)");
        }
    }

    // ================================================================== the search

    // Every pose on the grid, each turned onto the cup stack's middle with a level horizon; kept when it frames the whole face
    // with the margin, keeps the cup stack and the paddles in frame turned onto the far cup place, reaches every target within
    // the look's own limits and sees each with nothing solid in the way. Of those, the ones whose smallest paddle is within half
    // a pixel of the biggest; of those, the nearest to where the camera is now (so a second run finds it where the first left it).
    static Placing Search(DispenserPlan f)
    {
        var kept = new List<Placing>();
        int nx = Mathf.RoundToInt((GridX1 - GridX0) / GridDX), ny = Mathf.RoundToInt((GridY1 - GridY0) / GridDY), nz = Mathf.RoundToInt((GridZ1 - GridZ0) / GridDZ);
        for (int ix = 0; ix <= nx; ix++)
            for (int iy = 0; iy <= ny; iy++)
                for (int iz = 0; iz <= nz; iz++)
                {
                    Placing p = Judge(f, new Vector3(GridX0 + GridDX * ix, GridY0 + GridDY * iy, GridZ0 + GridDZ * iz));
                    if (p != null) kept.Add(p);
                }
        if (f.now != null) kept.Add(f.now);   // where it is now, if that frames it already
        if (kept.Count == 0) return null;
        float best = kept.Max(p => p.smallest);
        return kept.Where(p => p.smallest >= best - PixelTie).OrderBy(p => p.moved).First();
    }

    // A pose (in the dispenser's frame) turned onto the cup stack, judged; null when it fails any rule.
    static Placing Judge(DispenserPlan f, Vector3 local)
    {
        Vector3 eye = f.parent.TransformPoint(local);
        Vector3 toStack = f.stackCentre - eye;
        if (toStack.sqrMagnitude < 1e-4f) return null;
        Quaternion turn = Quaternion.LookRotation(toStack, Vector3.up);
        Placing p = Measure(f, eye, turn);
        if (p.least < DispenserFrameCheck.Margin) return null;
        Quaternion onFar = Quaternion.LookRotation(f.farCentre - eye, Vector3.up);
        p.kept = DispenserFrameCheck.Least(eye, onFar, f.fov, f.stays, out p.worstKept);
        if (p.kept < DispenserFrameCheck.Margin) return null;
        Vector2 rest = AimAssist.Angles(eye, f.stackCentre);
        foreach (var t in f.all)
        {
            Vector2 a = AimAssist.Angles(eye, DispenserFrameCheck.Box(t.thing).center);
            float pitch = a.y - rest.y;
            if (Mathf.Abs(Mathf.DeltaAngle(rest.x, a.x)) > LookYaw || pitch < -LookUp || pitch > LookDown) return null;
        }
        if (!Clear(f, eye, out _)) return null;
        p.local = local;
        p.localTurn = Quaternion.Inverse(f.parent.rotation) * turn;
        p.moved = Vector3.Distance(local, f.localNow);
        return p;
    }

    static Placing Measure(DispenserPlan f, Vector3 eye, Quaternion turn)
    {
        var p = new Placing { eye = eye, turn = turn };
        p.least = DispenserFrameCheck.Least(eye, turn, f.fov, f.all, out p.worst);
        p.smallest = DispenserFrameCheck.SmallestPaddle(eye, turn, f.fov, f.face, out p.smallestName);
        return p;
    }

    // Nothing solid (triggers aside) between the eye and any target's middle, and the eye clear of everything; the dispenser's
    // own colliders and Ace's never count (Ace is hidden in the close-up).
    static bool Clear(DispenserPlan f, Vector3 eye, out string inTheWay)
    {
        inTheWay = "";
        foreach (Collider c in Physics.OverlapSphere(eye, .08f, ~0, QueryTriggerInteraction.Ignore))
            if (!Ours(f, c)) { inTheWay = "the camera is inside " + Playtest4Shared.PathOf(c.gameObject); return false; }
        foreach (var t in f.all)
        {
            Vector3 to = DispenserFrameCheck.Box(t.thing).center - eye;
            float length = to.magnitude;
            if (length < .01f) continue;
            foreach (RaycastHit h in Physics.RaycastAll(eye, to / length, length - .01f, ~0, QueryTriggerInteraction.Ignore))
                if (!Ours(f, h.collider)) { inTheWay = $"{Playtest4Shared.PathOf(h.collider.gameObject)} hides {t.name}"; return false; }
        }
        return true;
    }

    static bool Ours(DispenserPlan f, Collider c) =>
        c.transform.IsChildOf(f.station.transform) || f.player != null && c.transform.IsChildOf(f.player);

    // What the close-up's crosshair would pick from this view: the first thing to use along the screen's middle, seeing past
    // what isn't one, as PlayerInteractor.FindStationTarget does (its "is it available" aside: that is the game's own state).
    static Interactable UnderCrosshair(DispenserPlan f, Vector3 eye, Quaternion turn)
    {
        foreach (RaycastHit h in Physics.RaycastAll(eye, turn * Vector3.forward, 10f).OrderBy(hit => hit.distance))
        {
            Interactable it = h.collider.GetComponentInParent<Interactable>();
            if (it == null || it is StationInteractable) continue;
            return it;
        }
        return null;
    }

    // ================================================================== first person at the stand point

    static void FirstPerson(Playtest4Shared.Run run, DispenserPlan f, string folder)
    {
        CafeViewMode view = Object.FindObjectsByType<CafeViewMode>(FindObjectsInactive.Include).FirstOrDefault();
        if (view == null) { run.Info("No CafeViewMode in the scene: first person isn't judged here (Bench 2, the lab, does it in Play Mode)."); return; }
        float fov = view.firstPersonCamera != null ? view.firstPersonCamera.Lens.FieldOfView : 0f;
        if (fov <= 10f) { run.Info("No first-person camera on CafeViewMode: first person isn't judged here."); return; }
        Transform stand = f.station.StandPoint;
        Vector3 eye = new Vector3(stand.position.x, stand.position.y + view.eyeHeight, stand.position.z);
        run.Info($"the stand point {V(stand.position)}, facing {stand.eulerAngles.y:0}°; the eye {view.eyeHeight:0.00} m up; {fov:0}° tall");
        Vector3 middle = f.face.Middle;
        Quaternion onMiddle = Quaternion.LookRotation(middle - eye, Vector3.up);
        float least = DispenserFrameCheck.Least(eye, onMiddle, fov, f.stays, out string worst);
        run.Check(least >= DispenserFrameCheck.Margin,
            $"looking at the dispenser's middle, all six paddles and the cup stack are in view, {least:P1} in at the least ({worst})");
        Quaternion onStack = Quaternion.LookRotation(f.stackCentre - eye, Vector3.up);
        var inView = f.face.paddles.Where(p => DispenserFrameCheck.Inside(eye, onStack, fov, DispenserFrameCheck.Box(p.thing)) >= 0f).Select(p => p.name).ToList();
        run.Info($"aimed at the cup stack (the Stations lab's photo), {inView.Count} of 6 paddles are wholly in view" +
                 (inView.Count < 6 ? "; off or cut: " + string.Join(", ", f.face.paddles.Select(p => p.name).Where(n => !inView.Contains(n))) : ""));
        Shoot(folder, "4-first-person-looking-at-the-middle", eye, onMiddle, fov);
        Shoot(folder, "5-first-person-on-the-cup-stack", eye, onStack, fov);

        // What it would take to frame them aimed at the cup stack instead: the stand point moved back (not done: see the report).
        Vector3 back = -Vector3.ProjectOnPlane(stand.forward, Vector3.up).normalized;
        float needed = -1f;
        for (float d = .02f; d <= 1.2f; d += .02f)
        {
            Vector3 e = eye + back * d;
            if (DispenserFrameCheck.Least(e, Quaternion.LookRotation(f.stackCentre - e, Vector3.up), fov, f.stays, out _) >= DispenserFrameCheck.Margin) { needed = d; break; }
        }
        StationInteractable counter = Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Include)
            .FirstOrDefault(s => s != null && !s.IsWorkSurface && s.StandPoint != null && s.GetComponent<BeverageStation>() == null);
        PlayerInteractor interactor = Object.FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include).FirstOrDefault();
        if (needed > 0f && counter != null && interactor != null)
        {
            var so = new SerializedObject(interactor);
            SerializedProperty enter = so.FindProperty("stationEnterRange"), range = so.FindProperty("counterRange");
            if (enter != null && range != null)
            {
                float Flat(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
                float now = enter.floatValue + range.floatValue - Flat(stand.position, counter.StandPoint.position);
                float then = enter.floatValue + range.floatValue - Flat(stand.position + back * needed, counter.StandPoint.position);
                run.Info($"to frame them aimed at the cup stack, the stand point would go {needed:0.00} m back; the dispenser's E offer ({enter.floatValue:0.0} m round it) " +
                         $"would then overlap \"behind the counter\" ({range.floatValue:0.0} m round the till) by {then:0.00} m instead of {now:0.00} m, and in that strip E opens " +
                         "the dispenser rather than talking to the queue. Not done: in first person the look is the player's own, and from above Ace is hidden in the close-up.");
            }
        }
        else if (needed <= 0f) run.Info("no stand point within 1.2 m back frames them aimed at the cup stack");
    }

    // ================================================================== photos and words

    static string PhotoFolder(string label) =>
        Path.Combine(Playtest4Shared.LogRoot, $"{label}-photos-{DateTime.Now:yyyy-MM-dd_HHmmss}");

    // A photo of a view at 1920 x 1080 (16:9: the frame the dispenser is judged in), Ace's stand-in hidden.
    static void Shoot(string folder, string name, Vector3 at, Quaternion turn, float fov)
    {
        Directory.CreateDirectory(folder);
        var hidden = new List<Renderer>();
        GameObject player = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        if (player != null)
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        var go = new GameObject("Temporary dispenser photo camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(at, turn);
            cam.fieldOfView = fov;
            cam.nearClipPlane = .03f;
            cam.farClipPlane = 250f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    static string Name(Interactable i) => i == null ? "nothing" : Playtest4Shared.PathOf(i.gameObject).Split('/').Last();

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
}
#endif
