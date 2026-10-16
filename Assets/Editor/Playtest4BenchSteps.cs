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
//       BeverageLook turns; the look takes its rest from this turn) stands straight on, at Ace's eye: on the dispenser's axis,
//       10 cm behind the stand point, the dispenser's own heading, tilted onto the working face's middle, no roll
//       (CinematicsWave2Steps.StraightOn, the rule Fixit Fidget > Cinematics > Drinks close-up - straight on uses). One undo
//       group; checks in numbers; before and after photos; nothing saved; safe to run twice.
//   2 - The dispenser's frame - play check (lab, Play Mode): DispenserFrameCheck, in a lab session.
//
// 10 Oct, the first-hour playtester: "Only 4 of the 6 paddles are fully in view: Americano is cut by the screen's edge and Latte
// is off it". Step 1 then searched a grid for a pose turned onto the cup stack that kept the whole face in frame; the pose it
// chose sat on the grid's ceiling, 44 cm above Ace's eye and turned 34.5°: the paddle row slanted 13.9°. 14 Oct, his note: "when
// in isometric view, and getting to the drink station, the overall look and feel is up and diagonal". The rule is now straight on
// (Cinematics spec §4.2): turned onto the cup stack, the far end leaves the frame, as turning your head does.
// ---------------------------------------------------------------------------
public static class Playtest4BenchSteps
{
    const string Area = Playtest4Shared.Menu + "Bench/";
    const string Tag = "[Playtest 4 · Bench] ";
    const string FrameTitle = "Bench 1 - Frame the whole dispenser";


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
            run.Info($"folded on the mat it is {folded.x * 1000f:0} x {folded.z * 1000f:0} mm, {folded.y * 1000f:0.0} mm thick ({Playtest4Shared.PathOf(cloth.gameObject)})");
            if (so.FindProperty("padSize") == null || so.FindProperty("padThickness") == null)
            {
                // The forgiving bench (14 Oct) retired the gathered pad (padSize, padThickness): in the hand the cloth is Art's held
                // wad, BT_ClothHeld, and since the Producer's call 23 it leads with its far edge at the cursor so the glass it
                // cleans shows (Bench 8 measures how much). The integration, 14 Oct: this read the deleted fields and threw.
                Transform held = cloth.transform.Find("BT_ClothHeld");
                run.Check(held != null, $"in the hand it is Art's held wad (BT_ClothHeld{(held != null ? "" : ": NOT under the cloth yet; Bench 7 puts it there")}), " +
                                        $"cleaning {BenchRules.ClothContact * 1000f:0} mm across; Bench 8 measures what it leaves in sight mid-wipe");
            }
            else
            {
                float pad = run.Prop(so, "padSize").floatValue, thick = run.Prop(so, "padThickness").floatValue;
                run.Check(folded.x * pad <= .045f && folded.z * pad <= .05f,
                    $"in the hand it is a pad of {folded.x * pad * 1000f:0} x {folded.z * pad * 1000f:0} mm ({pad:P0} of its folded size), {folded.y * thick * 1000f:0.0} mm thick");
            }
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

        // The plan: straight on, at Ace's eye (the Cinematics rule, 14 Oct; the same one Drinks close-up - straight on applies).
        float eyeHeight = CinematicsWave2Steps.EyeHeight();
        run.Require(CinematicsWave2Steps.StraightOn(f.station, f.parent, f.face, eyeHeight, out Vector3 planLocal, out Quaternion planTurn, out string why),
            "Can't work out the straight-on pose: " + why + ". Nothing changes; tell Claude.");
        f.plan = Measure(f, f.parent.TransformPoint(planLocal), f.parent.rotation * planTurn);
        f.plan.local = planLocal;
        f.plan.localTurn = planTurn;
        f.now = rest;
        f.already = Vector3.Distance(f.plan.local, f.localNow) < .001f && Quaternion.Angle(f.plan.localTurn, f.localTurnNow) < .1f;
        run.Info($"now: {f.eyeNow.y - f.station.StandPoint.position.y:0.00} m above the floor (Ace's eye is {eyeHeight:0.00}), the paddle row slants " +
                 $"{DispenserFrameCheck.RowSlant(f.eyeNow, f.turnNow, f.fov, f.face):0.0}°");
        run.Info($"the frame: {(f.already ? "already at" : forTheStep ? "it goes to" : "step 1 would put it at")} {V(f.plan.local)} in the dispenser's frame ({V(f.plan.eye)} in the world), " +
                 $"turned {V(f.plan.localTurn.eulerAngles)} (straight on, at Ace's eye): {f.plan.least:P1} in at the least ({f.plan.worst}); the row slants " +
                 $"{DispenserFrameCheck.RowSlant(f.plan.eye, f.plan.turn, f.fov, f.face):0.0}°; the smallest paddle ({f.plan.smallestName}) {f.plan.smallest:0} px tall at 1080p");
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
        if (f.already) { run.Info("the close-up already stands straight on at Ace's eye: nothing to change"); return; }
        Transform t = f.camera.transform;
        run.Record(t);
        Vector3 wasAt = t.localPosition;
        Vector3 wasTurn = t.localRotation.eulerAngles;
        t.localPosition = f.plan.local;
        t.localRotation = f.plan.localTurn;
        run.Changed($"{Playtest4Shared.PathOf(t.gameObject)}: placed at {V(wasAt)} → {V(t.localPosition)} in the dispenser's frame, turned {V(wasTurn)} → {V(t.localRotation.eulerAngles)} " +
                    $"(straight on, at Ace's eye, {Vector3.Distance(f.eyeNow, t.position):0.00} m from where it was; BeverageLook takes its rest from this turn)");
    }

    static void CheckFrame(Playtest4Shared.Run run, DispenserPlan f)
    {
        Transform t = f.camera.transform;
        Physics.SyncTransforms();
        run.Check(Vector3.Distance(t.localPosition, f.plan.local) < .0005f && Quaternion.Angle(t.localRotation, f.plan.localTurn) < .05f,
            $"the close-up's camera is where the rule put it ({V(t.localPosition)}, {V(t.localRotation.eulerAngles)})");
        CinematicsWave2Steps.CheckStraightOn(f.station, f.camera, f.face, CinematicsWave2Steps.EyeHeight(), run.Check, run.Info);
        var so = new SerializedObject(f.station);
        run.Check(so.FindProperty("stationCamera").objectReferenceValue == f.camera && f.camera.GetComponent<BeverageLook>() == f.look,
            "the station still opens this camera, and its look is on it");

        if (f.folder != null)
        {
            Vector3 eye = t.position;
            Shoot(f.folder, "2-after", eye, t.rotation, f.fov);
            Shoot(f.folder, "3-after-on-the-cup-stack", eye, Quaternion.LookRotation(f.stackCentre - eye, Vector3.up), f.fov);
            run.Info("after: " + Path.Combine(f.folder, "2-after.png") + " and 3-after-on-the-cup-stack.png (Play Mode: Bench 2, the lab, photographs the real close-up)");
        }
    }

    static Placing Measure(DispenserPlan f, Vector3 eye, Quaternion turn)
    {
        var p = new Placing { eye = eye, turn = turn };
        p.least = DispenserFrameCheck.Least(eye, turn, f.fov, f.all, out p.worst);
        p.smallest = DispenserFrameCheck.SmallestPaddle(eye, turn, f.fov, f.face, out p.smallestName);
        return p;
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
    internal static void Shoot(string folder, string name, Vector3 at, Quaternion turn, float fov)
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
