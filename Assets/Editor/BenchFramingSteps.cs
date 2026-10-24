#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// FRAME EACH DEVICE: THE CLOSE-UP'S FRAMING FROM DATA (one scale, 14 Oct 2026; specs/td-one-scale.md §5, §6.6)
//
//   Fixit Fidget > Bench > Bench 9 - Frame each device (prefabs)
//   Fixit Fidget > Bench > Bench 9b - Frame the watch closer (its cups and the rest only; when the clumsy hand asks)
//   Fixit Fidget > Bench > Bench 9 - Undo the near-edge rule: put back each device's framing from before it (prefabs)
//     (23 Oct 2026)
//
// At real size a framing that shows the job's compartments and the tool rest, clear of UI/UX's zones, holds the phone at
// about 38 % of the frame's height, the camera 40 %, the watch 19 % (TD §5.1; Art found the same): the wheel or D-pad up
// zooms to 60-80 %. So each device's close-up is data on its prefab (DeviceDefinition.benchLayout, the TD's fields):
//   cameraAim (mat mm) and cameraDistance (m): the layout view at zoom 0, FOV 40, pitched 58° (one camera language with
//   Cinematics); closeAim (mat mm) and fovClose (°): the zoom limit, the camera kept at the layout distance (a lens zoom).
//
// Bench 9 finds them on the real meshes, in the café scene's stage (BenchStage, wired by Bench 7):
//   * zoom 0: the aim and the distance that make the device tallest on screen (at the pose it opens in, 16:9) while, at
//     1920 × 1080 AND 2560 × 1600, 20 px in from the frame's edges (at 1080; scaled), the device at every quarter turn and on both
//     faces stays in frame and clear of U1–U6, and every compartment its faults use (the screws' cups by Systems' slot
//     rule, the pad and the well that fit its parts, the scrap tray) and the tool rest with its tools, each moved to
//     where the device's BenchLayout puts them, stay in frame and clear of U1–U5 (BenchStageContract, UI/UX §5.8);
//   * zoom 1: at that distance, the close aim and the narrowest FOV that keep the device alone (every pose, both aspects)
//     in frame and clear of U1–U6.
// The feel targets' bars are checked (T9 as restated at 11:30 after QA M1 and the real layout: zoom 0 ≥ 34 / 35 / 17 %,
// zoom 1 ≥ 55 % each, for phone / camera / watch; T8: a 3.8 mm screw head ≥ 12 px at 1080p at zoom 0, ≥ 15 px one wheel
// notch in), and photos are taken at zoom 0 and 1,
// at 16:9 and 16:10, with the zones drawn (orange), the device's box (cyan) and each compartment's (green, red if it fails):
// Logs/Bench/framing-<time>/. The search is deterministic, so a second run finds the same numbers and writes nothing.
//
// Bench 9b, the Producer's call 4 (feel targets §2: "if Bench 8's watch runs miss their bar at zoom 0, Bench 9 searches
// for the closest default that still keeps the job's cups and the rest in frame, and the watch opens there"): the same
// search for the watch with only its screws' cups and the tool rest required. It names what that framing leaves off-frame
// or under a card (the back plate's well, the new mainspring's pad, the scrap tray: T13's risk). Run it only if the clumsy
// hand says the watch is too small; Bench 9 puts the everything-in-view framing back.
//
// THE MAT'S NEAR EDGE (23 Oct 2026; his note of 22 Oct 2026: "the tray on the right and the bottom of the mat kind of look
// off"). The search above makes the device tallest; nothing said where the frame's bottom edge lands on the mat, and for the
// phone it landed in the near ruler (Art's printed strip along the mat's near edge, from its flat top's edge at y -178.5,
// inside the 1.5 mm chamfer of the mat's edge at -180, to its inner line at y -166; the centimetre numbers at y -173..-170):
// the bottom of the screen cut the numbers in half, a strip that "reads like a mistake". The rules, strictest first:
//   1. the near ruler whole with the mat's edge in view under it (the frame's bottom 3 mm past the edge: the ruler reads as
//      the mat's border), or no ruler at all (3 mm inside its inner line: the grid runs off the bottom like the sides);
//   2. the ruler's numbers whole (the frame's bottom 1.5 mm under their feet: the ticks may run off it, as Grace's camera,
//      the Day 1 repair, had them on 20 Oct 2026), or no ruler at all.
// The plain search's layout view stands when its bottom already keeps rule 1 or rule 2, so only a true half ruler is
// re-framed (the smallest fix for his note: Grace's camera keeps its framing). Else each rule is searched in turn, and the
// first framing that keeps every feel-target bar the plain one keeps, with a margin, is written: T9 at zoom 0 and at the
// limit half a point of the frame's height over its bar, T8 at zoom 0 and one notch in 0.3 px over its bar, so what ships
// doesn't hang on a hair of this step's model. Failing both, the plain search's framing (from today's inputs) is written,
// with a FAIL row naming the bar each rule would cost; the ruler's own fix is then a Blender change to the mat's print
// (bench_stage_textures.py / bench_stage.py: the near ruler moved in from the edge, or the near strip printed plain).
// The rule is for the layout view (zoom 0). Zooming in moves the frame's bottom up the mat (the narrower lens outweighs the
// few millimetres the aim drifts toward the close aim), so a frame that shows the edge passes over the ruler on the way in;
// the report gives the bottom's place one notch in and at the limit. A mat with no printed ruler (the stand-in) is left to
// the plain search; Art's mat (BM_RepairMat) with no BM_Print renderer is a FAIL row, as the rule couldn't run. Bench 9b
// leaves the rule out: it is the watch's closest default (the Producer's call 4), not a layout view.
// The undo puts back values, it doesn't search again. Just before its first change to a prefab, Bench 9 saves every
// device's four fields (cameraAim, cameraDistance, closeAim, fovClose) to Logs/Bench/framing-before-near-edge.json, once:
// the file is never written over (delete it to save afresh). "Bench 9 - Undo the near-edge rule" writes those values back,
// says so plainly when the file isn't there, and prints for each device whether today's plain search gives the stored
// framing (a NO means its inputs changed since: the zones, a device's meshes, the rest or the tray).
//
// Writes only on a real-size prefab (DeviceDefinition.realSize, set by Devices 2); on any other it is a dry run (numbers and
// photos, nothing written). One Undo group per prefab. The café scene isn't changed (the photos are a preview scene).
//
// The camera's pose, which the bench's ItemInspector must share (TD §6.7): in the mat's frame (BenchStage.matFrame; +x the
// player's right, +z away, +y up), aim = BenchStage.MatToWorld(cameraAim); the camera at aim - forward × cameraDistance,
// forward = Quaternion.Euler(58, 0, 0) × the mat's forward; FOV 40. Zoom z: aim → lerp(cameraAim, closeAim, z), the same
// distance, FOV = 2·atan(tan 20° ÷ M^z) with M = tan 20° ÷ tan(fovClose ÷ 2). The device lies as ItemInspector lays it: its
// long side up the view turned by its quarter, its box's middle over MatToWorld(rest), its lowest corner on the mat.
// ---------------------------------------------------------------------------
internal static class BenchFramingSteps
{
    const string Title9 = "Bench 9 - Frame each device (prefabs)";
    const string Title9b = "Bench 9b - Frame the watch closer (its cups and the rest only; when the clumsy hand asks)";
    const string Menu9 = "Fixit Fidget/Bench/" + Title9;
    const string Menu9b = "Fixit Fidget/Bench/" + Title9b;
    const string Title9Undo = "Bench 9 - Undo the near-edge rule: put back each device's framing from before it (prefabs)";
    const string Menu9Undo = "Fixit Fidget/Bench/" + Title9Undo;

    const float Pitch = 58f, LayoutFov = 40f;
    const float MarginAt1080 = 20f;      // px from the frame's edges and from the zones (TD §6.6)
    const float SmallPiece = .046f;      // a part or cover this size or less on both sides takes the small pad or well
    const float DistanceMin = .20f, DistanceMax = 1.40f, FovMin = 4f;
    static readonly Vector2Int[] Aspects = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1600) };
    static readonly Vector2Int[] PhotoSizes = { new Vector2Int(960, 540), new Vector2Int(960, 600) };   // 16:9, 16:10

    // T9 (feel targets §1, restated 14 Oct 11:30 with the Producer's call 21): the device's share of the frame's height at
    // zoom 0 (x) and zoom 1 (y). Bench 5 checks the same bars in play (BenchViewProbe).
    static readonly Dictionary<string, Vector2> Bars = new Dictionary<string, Vector2>
    {
        ["phone"] = new Vector2(.34f, .55f), ["camera"] = new Vector2(.35f, .55f), ["watch"] = new Vector2(.17f, .55f),
    };

    static readonly Quaternion Look = Quaternion.Euler(Pitch, 0f, 0f);
    static readonly Quaternion Unlook = Quaternion.Inverse(Quaternion.Euler(Pitch, 0f, 0f));

    // The mat's near edge and its printed ruler (Art's A1: Tools/Blender/bench_stage.py, D = 360; bench_stage_textures.py: the
    // strip from the top's edge at y -178.5 to its inner line at -166, ticks rising 2.5 and 4.5 mm from the edge, the numbers
    // 3 mm tall standing 1 mm over the long ticks, so their feet at -173 and their tops at -170). If the ruler moves in
    // Blender, these follow.
    const float MatNearEdgeMm = -180f, NearRulerInnerMm = -166f, NumbersFootMm = -173f, NumbersTopMm = -170f;
    const float EdgeClearMm = 3f;          // the frame's bottom 3 mm past the edge, or 3 mm inside the inner line (about 12 px)
    const float NumbersClearMm = 1.5f;     // the second choice: 1.5 mm under the numbers' feet (the long ticks' tips may show)
    const string RulerMaterial = "BM_Print";   // the rulers' material on Art's mat (bench_stage.py BM_MATERIALS)
    const string ArtsMatName = "BM_RepairMat"; // Art's mat as Bench 7 names it (its stand-in has a suffix and no print)
    // A framing under a rule is taken only this far over each bar the plain one meets (23 Oct 2026: the phone's rule 1 framing
    // was 12.05 px against T8's 12 in a model of this search, too fine a line to decide what ships). 0.2 px, not the review's
    // 0.3, after the first run on his Mac (23 Oct 2026): the phone's rule 2 framing (the ruler's numbers whole, his "the bottom
    // of the mat kind of look off") came out at 15.22 px one notch in against T8's 15, refused at 0.3; rule 1 (12.12 px against
    // 12) is still refused at 0.2. Bench 5 measures the phone's real screws, which read above this search's number.
    const float RuleMarginFill = .005f, RuleMarginPx = .2f;

    // The framings as they were before the near-edge rule first changed a prefab (23 Oct 2026), for its undo to put back.
    // Saved once, never written over; per prefab path, the four fields Write writes.
    static string BeforePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Bench", "framing-before-near-edge.json"));
    [Serializable] sealed class StoredFraming { public string prefab; public Vector2 cameraAim; public float cameraDistance; public Vector2 closeAim; public float fovClose; }
    [Serializable] sealed class StoredFramings { public string saved, why; public List<StoredFraming> devices = new List<StoredFraming>(); }

    // Where the frame's bottom may meet the mat's top: at or nearer than nearTo (mat y, mm), or at or further than farFrom.
    sealed class NearEdge
    {
        public string name;
        public float nearTo, farFrom;
        public bool Keeps(float cut) => cut <= nearTo || cut >= farFrom;
    }

    static readonly NearEdge Strict = new NearEdge
    {
        name = "the near ruler whole with the mat's edge under it, or no ruler at all",
        nearTo = MatNearEdgeMm - EdgeClearMm, farFrom = NearRulerInnerMm + EdgeClearMm,
    };
    static readonly NearEdge NumbersWhole = new NearEdge
    {
        name = "the ruler's numbers whole (their ticks may run off the bottom), or no ruler at all",
        nearTo = NumbersFootMm - NumbersClearMm, farFrom = NearRulerInnerMm + EdgeClearMm,
    };

    [MenuItem(Menu9)]
    static void FrameEach() => Frame(false);

    // 9b without the near-edge rule (23 Oct 2026): it is the watch's closest default, not a layout view to push back.
    [MenuItem(Menu9b)]
    static void FrameWatchCloser() => Frame(true, false);

    // The near-edge rule's undo (23 Oct 2026): the framings saved just before the rule's first change, written back as they
    // were (not searched again: today's inputs may differ). Unity's own Undo covers the session ("Bench 9 - frame the
    // phone"); this covers after it.
    [MenuItem(Menu9Undo)]
    static void PutBackTheFramings() => Frame(false, false, true);

    [MenuItem(Menu9, true)]
    [MenuItem(Menu9b, true)]
    [MenuItem(Menu9Undo, true)]
    static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ======================================================================= what the search works on

    // The mat's frame (scale 1) and MatToWorld's own convention, so this step aims exactly where the bench will.
    sealed class Mat
    {
        public BenchStage stage;
        public Matrix4x4 toMat;
        public Vector3 origin, ex, ey;
        public float top;
        public bool ruler;                   // Art's printed rulers are on it (a stand-in mat has none)
        public Vector3 Point(Vector2 mm) => origin + ex * (mm.x / 1000f) + ey * (mm.y / 1000f);
        public Vector3 In(Vector3 world) => toMat.MultiplyPoint3x4(world);
    }

    // Something that must stay in view: its points (mat frame) and the same turned into the camera's axes (the camera only
    // ever pitches in the mat's frame, so each point is turned once and a camera's place is one subtraction).
    sealed class Cloud
    {
        public string name;
        public Vector3[] pts, q;
        public Cloud(string name, List<Vector3> points)
        {
            this.name = name;
            pts = points.ToArray();
            q = pts.Select(p => Unlook * p).ToArray();
        }
    }

    // A piece of the stage that moves with the device's layout (the tool rest, the scrap tray): its points, its looks, its centre.
    sealed class Group
    {
        public string name;
        public Vector3 centre;
        public bool standIn;
        public readonly List<Vector3> pts = new List<Vector3>();
        public readonly List<(Mesh mesh, Material[] mats, Matrix4x4 m)> looks = new List<(Mesh, Material[], Matrix4x4)>();
    }

    sealed class Shape
    {
        public readonly List<Vector3> corners = new List<Vector3>();   // its counted renderers' boxes, root frame (ItemInspector.MeasureSlab)
        public Vector3 min, max, thin, along;
        public int screws;
        public readonly List<(string name, Vector3 size)> covers = new List<(string, Vector3)>(), parts = new List<(string, Vector3)>();
    }

    sealed class Framing
    {
        public BenchScale.Device d;
        public DeviceDefinition def;
        public Shape shape;
        public Vector3 rest;                 // mat frame: where its box's middle lies
        public float openYaw;
        public Vector3 openRoot;
        public Quaternion openRot;
        public Cloud open;                   // the pose it opens in (face up, its yaw as set down on the bench)
        public readonly List<Cloud> poses = new List<Cloud>();
        public readonly List<Cloud> targets = new List<Cloud>();       // what must stay in view at zoom 0
        public readonly List<Cloud> allTargets = new List<Cloud>();    // every compartment it uses (9b reports what it leaves out)
        public Vector3 restDelta, trayDelta;
    }

    sealed class View
    {
        public float w, h, m;
        public Rect[] zones;
        public bool[] u6;
    }

    struct Box2
    {
        public float x0, y0, x1, y1;
        public bool ok;
    }

    // ======================================================================= the run

    // closer: Bench 9b (the watch, its cups and the rest only). nearEdge: the near-edge rule (Bench 9 itself). restore: the
    // rule's undo, which writes back the framings saved before its first change (the plain search only runs to compare).
    static void Frame(bool closer, bool nearEdge = true, bool restore = false)
    {
        var run = new BenchScale.Run(restore ? Title9Undo : closer ? Title9b : Title9, restore ? "framing-put-back" : closer ? "framing-watch-closer" : "framing");
        bool error = false;
        string nothingToPutBack = null;      // the undo with no saved framings: said in the Console's own line, as an error
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (SceneManager.GetActiveScene().path != BenchScale.CafeScene) throw new InvalidOperationException($"Open the café scene ({BenchScale.CafeScene}) first: the stage is read from it.");
            BenchStage[] stages = Object.FindObjectsByType<BenchStage>(FindObjectsInactive.Include);
            if (stages.Length != 1) throw new InvalidOperationException($"Expected one BenchStage in the café scene, found {stages.Length}.");
            BenchStage stage = stages[0];
            if (stage.matFrame == null) throw new InvalidOperationException("BenchStage.matFrame isn't set: run Bench 7 (the forgiving stage) first.");

            Mat mat = MakeMat(stage, run);
            bool theRule = nearEdge && !closer && !restore;
            // 23 Oct 2026: on Art's own mat a missing print would switch the rule off with only an info line, and the phone would
            // keep its half ruler under an all-PASS report. A FAIL row instead.
            if (theRule && !mat.ruler && stage.matFrame.name == ArtsMatName)
                run.Check(false, $"Art's mat has no {RulerMaterial} renderer: the near-edge rule couldn't run (the near ruler is printed in {RulerMaterial}; is the mat's import missing a material?)");
            if (!theRule && !restore && mat.ruler)
            {
                mat.ruler = false;
                run.Info("the near-edge rule is left out for this run (Bench 9b: the watch's closest default, as the Producer's call 4 has it)");
            }
            View[] views = Aspects.Select(a => MakeView(a.x, a.y)).ToArray();
            var rest = Gather("the tool rest", stage.toolRest, ToolRestRoots(stage), mat, new Vector3(.110f, .030f, .230f));
            var tray = Gather("the scrap tray", stage.scrapTray, new[] { stage.scrapTray }, mat, new Vector3(.160f, .022f, .090f));
            run.Info($"{rest.name}: {(rest.standIn ? "nothing drawn: Art's 110 × 230 × 30 mm box stands in" : rest.looks.Count + " meshes")}; {tray.name}: {(tray.standIn ? "nothing drawn: Art's 160 × 90 × 22 mm box stands in" : tray.looks.Count + " meshes")}");

            // The undo: the framings saved before the rule's first change, or a plain "nothing to put back".
            StoredFramings stored = null;
            if (restore)
            {
                bool there = File.Exists(BeforePath);
                stored = there ? JsonUtility.FromJson<StoredFramings>(File.ReadAllText(BeforePath)) : null;
                if (stored == null || stored.devices == null || stored.devices.Count == 0)
                {
                    stored = null;
                    nothingToPutBack = $"NOTHING TO PUT BACK: {BeforePath} {(there ? "holds no framings" : "isn't there")}. Bench 9 writes it once, just before the near-edge " +
                                       "rule's first change to a prefab; without it there is nothing to go back to. Nothing was changed.";
                    run.Check(false, nothingToPutBack);
                }
                else run.Info($"the framings from before the near-edge rule: {BeforePath} (saved {stored.saved}, {stored.devices.Count} prefab(s)); each is written back as it was");
            }
            // Bench 9 itself: every prefab's framing as it is now, saved to that file just before the first change (once ever).
            Action beforeFirstChange = null;
            if (theRule)
            {
                StoredFramings now = CurrentFramings();
                bool saved = false;
                beforeFirstChange = () =>
                {
                    if (saved) return;
                    saved = true;
                    SaveBefore(now, run);
                };
            }

            if (!restore || stored != null)
                foreach (BenchScale.Device d in BenchScale.Devices)
                {
                    if (closer && d.label != "watch") continue;
                    FrameDevice(d, mat, views, rest, tray, closer, run, stored, beforeFirstChange);
                }
        }
        catch (Exception e)
        {
            error = true;
            run.Info("ERROR " + e.Message);
            run.Info(e.ToString());
        }
        run.Finish(error ? "ERROR: stopped (see above). Nothing was written after the error."
                         : nothingToPutBack ?? $"{run.Changes} prefab(s) framed, {run.Passes} PASS, {run.Fails} FAIL (a FAIL row names the bar it misses).",
                   error || nothingToPutBack != null);
    }

    static Mat MakeMat(BenchStage stage, BenchScale.Run run)
    {
        var mat = new Mat { stage = stage };
        Transform frame = stage.matFrame;
        mat.toMat = Matrix4x4.TRS(frame.position, frame.rotation, Vector3.one).inverse;
        mat.origin = mat.In(stage.MatToWorld(Vector2.zero));
        mat.ex = mat.In(stage.MatToWorld(new Vector2(1000f, 0f))) - mat.origin;
        mat.ey = mat.In(stage.MatToWorld(new Vector2(0f, 1000f))) - mat.origin;
        if ((mat.ex - Vector3.right).magnitude > .01f || (mat.ey - Vector3.forward).magnitude > .01f)
            run.Info($"NOTE MatToWorld's axes aren't the mat frame's +x and +z (1 m along x → {BenchScale.V(mat.ex)}, along y → {BenchScale.V(mat.ey)}): this step follows MatToWorld");
        // The mat's top: its drawn surface where MatToWorld's height 0 is under it (the device's lowest corner lies there).
        float top = float.MinValue;
        foreach (MeshRenderer r in frame.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !r.enabled || !r.gameObject.activeInHierarchy || Excluded(stage, r.transform)) continue;
            var pts = new List<Vector3>();
            Corners(mf.sharedMesh.bounds, mat.toMat * mf.transform.localToWorldMatrix, pts);
            top = Mathf.Max(top, pts.Max(p => p.y));
        }
        mat.top = top > float.MinValue && top - mat.origin.y < .02f ? Mathf.Max(top, mat.origin.y) : mat.origin.y;
        run.Info($"the mat's frame: {stage.matFrame.name}; MatToWorld(0, 0) sits {1000f * (mat.origin.y - mat.top):+0.0;-0.0} mm against the mat's top; a device's lowest corner lies on the top");
        mat.ruler = frame.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null && m.name == RulerMaterial));
        run.Info(mat.ruler ? $"its printed rulers ({RulerMaterial}) are on it: Bench 9 frames each layout view under the near-edge rule (the header)"
                           : $"no printed ruler on it ({RulerMaterial}): the near-edge rule can't apply");
        return mat;
    }

    static View MakeView(int w, int h)
    {
        BenchStageContract.Zone[] all = BenchStageContract.All;
        return new View
        {
            w = w, h = h, m = MarginAt1080 * h / 1080f,
            zones = all.Select(z => BenchStageContract.Rect(z, w, h)).ToArray(),
            u6 = all.Select(z => z == BenchStageContract.Zone.U6).ToArray(),
        };
    }

    // ======================================================================= the stage

    static IEnumerable<Transform> ToolRestRoots(BenchStage stage)
    {
        if (stage.toolRest != null) yield return stage.toolRest;
        if (stage.toolSlots != null) foreach (Transform seat in stage.toolSlots) if (seat != null) yield return seat;
        // the tools themselves, wherever Bench 7 parented them: every tool within 30 cm of the rest
        if (stage.toolRest != null)
            foreach (ToolPickup tool in Object.FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude))
                if (tool != null && (tool.transform.position - stage.toolRest.position).magnitude < .30f) yield return tool.transform;
    }

    static bool Excluded(BenchStage stage, Transform t)
    {
        if (stage.toolRest != null && t.IsChildOf(stage.toolRest)) return true;
        if (stage.scrapTray != null && t.IsChildOf(stage.scrapTray)) return true;
        if (t.GetComponentInParent<ToolPickup>(true) != null) return true;
        if (stage.toolSlots != null) foreach (Transform seat in stage.toolSlots) if (seat != null && t.IsChildOf(seat)) return true;
        return false;
    }

    static Group Gather(string name, Transform anchor, IEnumerable<Transform> roots, Mat mat, Vector3 standIn)
    {
        var g = new Group { name = name, centre = anchor != null ? mat.In(anchor.position) : mat.origin };
        var seen = new HashSet<Renderer>();
        foreach (Transform root in roots)
        {
            if (root == null) continue;
            // The keyed tools draw through a SkinnedMeshRenderer (15 Oct: the brush's Bend needs one), so both kinds count:
            // with MeshRenderers only, the brush was missing from the rest's group and from the photos.
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (r is MeshRenderer)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null || !seen.Add(r) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Matrix4x4 m = mat.toMat * r.transform.localToWorldMatrix;
                Corners(mesh.bounds, m, g.pts);
                g.looks.Add((mesh, r.sharedMaterials, m));
            }
        }
        if (g.pts.Count == 0)
        {
            g.standIn = true;
            Box(new Vector3(g.centre.x, mat.top + standIn.y * .5f, g.centre.z), standIn, g.pts);
        }
        return g;
    }

    static void Corners(Bounds b, Matrix4x4 m, List<Vector3> into)
    {
        for (int i = 0; i < 8; i++)
            into.Add(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
    }

    static void Box(Vector3 centre, Vector3 size, List<Vector3> into) => Corners(new Bounds(centre, size), Matrix4x4.identity, into);

    // ======================================================================= the device

    static Shape Measure(string prefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            var s = new Shape();
            Quaternion unturn = Quaternion.Inverse(root.transform.rotation);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!DeviceBounds.Counts(r)) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                bool own = mf != null && mf.sharedMesh != null;
                Bounds b = own ? mf.sharedMesh.bounds : r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    if (own) corner = r.transform.TransformPoint(corner);
                    Vector3 local = unturn * (corner - root.transform.position);
                    s.corners.Add(local);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            if (s.corners.Count == 0) throw new InvalidOperationException(prefab + " draws nothing the bench would measure.");
            s.min = min;
            s.max = max;
            Vector3 size = max - min;
            s.thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            s.along = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            if (s.along == s.thin) s.along = s.thin == Vector3.up ? Vector3.forward : Vector3.up;
            s.screws = root.GetComponentsInChildren<Screw>(true).Length;
            foreach (RemovablePart cover in root.GetComponentsInChildren<RemovablePart>(true))
                if (BenchScale.PieceBounds(root.transform, cover.transform, out Bounds cb)) s.covers.Add((cover.name, cb.size));
            foreach (ReplaceablePart part in root.GetComponentsInChildren<ReplaceablePart>(true))
                if (BenchScale.PieceBounds(root.transform, part.transform, out Bounds pb)) s.parts.Add((part.name, pb.size));
            return s;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ItemInspector.Lay: the long side up the view (the mat's +z), the given face up.
    static Quaternion Lay(Shape s, float face)
    {
        Vector3 up = s.thin * face, along = s.along;
        if (Mathf.Abs(Vector3.Dot(up, along)) > .99f) along = Vector3.Cross(up, Vector3.right).sqrMagnitude > .01f ? Vector3.Cross(up, Vector3.right) : Vector3.Cross(up, Vector3.forward);
        return Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(along, up));
    }

    // ItemInspector.Place: turned, its box's middle over the rest point, its lowest corner on the mat.
    static List<Vector3> Posed(Shape s, float face, float yaw, Vector3 rest, float top, out Vector3 rootPos, out Quaternion rotation)
    {
        rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Lay(s, face);
        Vector3 centre = rotation * ((s.min + s.max) * .5f);
        float lowest = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? s.min.x : s.max.x, (i & 2) == 0 ? s.min.y : s.max.y, (i & 4) == 0 ? s.min.z : s.max.z);
            lowest = Mathf.Min(lowest, (rotation * corner).y);
        }
        rootPos = new Vector3(rest.x - centre.x, top - lowest, rest.z - centre.z);
        var pts = new List<Vector3>(s.corners.Count);
        foreach (Vector3 c in s.corners) pts.Add(rootPos + rotation * c);
        return pts;
    }

    static Framing MakeJob(BenchScale.Device d, DeviceDefinition def, Mat mat, Group rest, Group tray, bool closer, BenchScale.Run run)
    {
        var layout = def.benchLayout;
        var job = new Framing { d = d, def = def, shape = Measure(d.prefab) };
        Shape s = job.shape;
        Vector3 restPoint = mat.Point(layout.rest);
        job.rest = new Vector3(restPoint.x, mat.top, restPoint.z);

        // the pose it opens in: as set down on a bench slot (the rig's turn: its local axes the mat's), squared to the view
        Vector3 flat = Vector3.ProjectOnPlane(s.along, Vector3.up);
        job.openYaw = flat.sqrMagnitude > 1e-6f ? Mathf.Round(Vector3.SignedAngle(Vector3.forward, flat, Vector3.up) / 90f) * 90f : 0f;
        foreach (float face in new[] { 1f, -1f })
            for (int quarter = 0; quarter < 4; quarter++)
            {
                float yaw = job.openYaw + 90f * quarter;
                List<Vector3> pts = Posed(s, face, yaw, job.rest, mat.top, out Vector3 at, out Quaternion turn);
                var cloud = new Cloud($"{(face > 0f ? "face up" : "face down")}, turned {Mathf.Repeat(yaw, 360f):0}°", pts);
                job.poses.Add(cloud);
                if (face > 0f && quarter == 0) { job.open = cloud; job.openRoot = at; job.openRot = turn; }
            }
        run.Info($"its slab {BenchScale.MmV(s.max - s.min)}; it opens face up turned {job.openYaw:0}° (its long side {(Mathf.Abs(job.openYaw) % 180f < 1f ? "up the view" : "across")}); its middle at mat ({layout.rest.x:0}, {layout.rest.y:0}) mm");

        // where the stage's moving pieces go for it (BenchLayout), as offsets in the mat's plane
        Vector3 restAt = mat.Point(new Vector2(layout.toolRestCentreX, layout.toolRestCentreY)) - rest.centre;
        Vector3 trayAt = mat.Point(layout.scrapTray) - tray.centre;
        job.restDelta = new Vector3(restAt.x, 0f, restAt.z);
        job.trayDelta = new Vector3(trayAt.x, 0f, trayAt.z);

        // ---- the compartments its faults use (every fault: the union) ----
        BenchStage stage = mat.stage;
        var inView = new List<Cloud> { new Cloud($"the tool rest at ({layout.toolRestCentreX:0}, {layout.toolRestCentreY:0})", rest.pts.Select(p => p + job.restDelta).ToList()) };
        var cups = new List<Cloud>();
        if (s.screws > 0 && stage.cups != null)
        {
            // Systems' slot rule: a cup is free when its footprint (Ø 18) plus 6 mm lies outside the device's footprint at all four
            // resting quarter turns; the screws take the first free cups, numbered near to far, left column first (Cup_1..6).
            var footprints = job.poses.Take(4).Select(p => (p.pts.Min(v => v.x), p.pts.Max(v => v.x), p.pts.Min(v => v.z), p.pts.Max(v => v.z))).ToList();
            var free = new List<int>();
            for (int i = 0; i < stage.cups.Length; i++)
            {
                if (stage.cups[i] == null) continue;
                Vector3 c = mat.In(stage.cups[i].position);
                bool clear = footprints.All(f =>
                {
                    float dx = Mathf.Max(f.Item1 - c.x, 0f, c.x - f.Item2), dz = Mathf.Max(f.Item3 - c.z, 0f, c.z - f.Item4);
                    return Mathf.Sqrt(dx * dx + dz * dz) > .009f + .006f;
                });
                if (clear) free.Add(i);
            }
            foreach (int i in free.Take(s.screws))
            {
                Vector3 c = mat.In(stage.cups[i].position);
                var pts = new List<Vector3>();
                Box(c + new Vector3(0f, .002f, 0f), new Vector3(.020f, .008f, .020f), pts);
                cups.Add(new Cloud($"cup {i + 1} (a screw)", pts));
            }
            run.Info($"{s.screws} screws; the cups clear of its footprint at every quarter turn: {(free.Count == 0 ? "none" : string.Join(", ", free.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture))))}" +
                     (free.Count < s.screws ? " (fewer than its screws: the rest stack in the last)" : ""));
        }
        inView.AddRange(cups);
        foreach (var (name, size) in s.covers) inView.Add(Bay($"the cover well for {name}", Small(size) ? stage.wellSmall : stage.wellLarge, size, mat));
        foreach (var (name, size) in s.parts) inView.Add(Bay($"the new-part pad for {name}", Small(size) ? stage.padSmall : stage.padLarge, size, mat));
        if (s.parts.Count > 0) inView.Add(new Cloud($"the scrap tray at ({layout.scrapTray.x:0}, {layout.scrapTray.y:0})", tray.pts.Select(p => p + job.trayDelta).ToList()));

        job.allTargets.AddRange(inView.Where(c => c != null));
        // Bench 9b (call 4): only the screws' cups and the tool rest must stay in view.
        job.targets.AddRange(closer ? job.allTargets.Where(c => c.name.StartsWith("the tool rest", StringComparison.Ordinal) || c.name.StartsWith("cup ", StringComparison.Ordinal)) : job.allTargets);
        run.Info("in view at zoom 0, clear of U1–U5: " + string.Join("; ", job.targets.Select(c => c.name)));
        return job;
    }

    static bool Small(Vector3 size) => size.x <= SmallPiece && size.z <= SmallPiece;

    // A pad or a well with the piece lying in it, flat (its two flat sides about the empty, its thickness up).
    static Cloud Bay(string name, Transform empty, Vector3 size, Mat mat)
    {
        if (empty == null) return null;
        Vector3 c = mat.In(empty.position);
        var pts = new List<Vector3>();
        Box(c + new Vector3(0f, size.y * .5f + .001f, 0f), new Vector3(size.x + .004f, size.y + .002f, size.z + .004f), pts);
        return new Cloud($"{name} ({empty.name}, {size.x * 1000f:0} × {size.z * 1000f:0} mm)", pts);
    }

    // ======================================================================= the projection

    static Vector3 CameraAt(Mat mat, Vector2 aimMm, float distance) => mat.Point(aimMm) - Look * Vector3.forward * distance;

    static Box2 Project(Vector3[] q, Vector3 camQ, float focal, float w, float h)
    {
        var b = new Box2 { x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, ok = true };
        for (int i = 0; i < q.Length; i++)
        {
            float vz = q[i].z - camQ.z;
            if (vz < .02f) { b.ok = false; return b; }
            float sx = w * .5f + (q[i].x - camQ.x) / vz * focal;
            float sy = h * .5f + (q[i].y - camQ.y) / vz * focal;
            if (sx < b.x0) b.x0 = sx;
            if (sx > b.x1) b.x1 = sx;
            if (sy < b.y0) b.y0 = sy;
            if (sy > b.y1) b.y1 = sy;
        }
        return b;
    }

    // -1: clear; 0..5: under that zone (BenchStageContract.All's order); 6: off the frame; 7: behind the camera. The 20 px margin
    // is the frame's edges'; a zone is clear when the box doesn't overlap it (the TD's own model, whose numbers T9's bars are).
    static int Code(Box2 b, View v, bool device)
    {
        if (!b.ok) return 7;
        if (b.x0 < v.m || b.y0 < v.m || b.x1 > v.w - v.m || b.y1 > v.h - v.m) return 6;
        for (int i = 0; i < v.zones.Length; i++)
        {
            if (!device && v.u6[i]) continue;
            Rect z = v.zones[i];
            if (b.x0 < z.xMax && b.x1 > z.xMin && b.y0 < z.yMax && b.y1 > z.yMin) return i;
        }
        return -1;
    }

    static string Words(int code) => code == 7 ? "behind the camera" : code == 6 ? "off the frame (or within 20 px of its edge)" : code >= 0 ? "under " + BenchStageContract.All[code] : "clear";

    static float Focal(float h, float fov) => h * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);

    static bool Fits(Vector3 camQ, float fov, Framing job, View[] views, bool withTargets)
    {
        foreach (View v in views)
        {
            float focal = Focal(v.h, fov);
            foreach (Cloud pose in job.poses) if (Code(Project(pose.q, camQ, focal, v.w, v.h), v, true) >= 0) return false;
            if (withTargets) foreach (Cloud t in job.targets) if (Code(Project(t.q, camQ, focal, v.w, v.h), v, false) >= 0) return false;
        }
        return true;
    }

    // What fails first (for the report): the first pose or compartment that isn't clear, with where.
    static string Why(Vector3 camQ, float fov, Framing job, View[] views, IEnumerable<Cloud> targets)
    {
        foreach (View v in views)
        {
            float focal = Focal(v.h, fov);
            foreach (Cloud pose in job.poses)
            {
                int c = Code(Project(pose.q, camQ, focal, v.w, v.h), v, true);
                if (c >= 0) return $"the {job.d.label} {pose.name}, at {v.w:0} × {v.h:0}: {Words(c)}";
            }
            foreach (Cloud t in targets)
            {
                int c = Code(Project(t.q, camQ, focal, v.w, v.h), v, false);
                if (c >= 0) return $"{t.name}, at {v.w:0} × {v.h:0}: {Words(c)}";
            }
        }
        return "nothing";
    }

    // The device's share of the frame's height at the pose it opens in, 16:9 (TD §5.1).
    static float Fill(Vector3 camQ, float fov, Framing job)
    {
        Box2 b = Project(job.open.q, camQ, Focal(1080f, fov), 1920f, 1080f);
        return b.ok ? (b.y1 - b.y0) / 1080f : 0f;
    }

    static Vector3 CamQ(Mat mat, Vector2 aim, float distance) => Unlook * CameraAt(mat, aim, distance);

    // Pixels a millimetre at a point (1080 tall, a 1 mm step across the mat), for the screw heads (T8).
    static float PxPerMm(Vector3 at, Vector3 camQ, float fov)
    {
        Vector3 a = Unlook * at - camQ, b = Unlook * (at + new Vector3(.001f, 0f, 0f)) - camQ;
        float focal = Focal(1080f, fov);
        return new Vector2((b.x / b.z - a.x / a.z) * focal, (b.y / b.z - a.y / a.z) * focal).magnitude;
    }

    // ======================================================================= the mat's near edge (23 Oct 2026)

    // Where the frame's bottom edge meets the mat's printed top (mat y, mm; the rulers are printed on its flat top, BenchStage.
    // MatTop over its origin) for the camera at this aim and distance with this lens: the bottom edge looks down at Pitch +
    // fov / 2 in the mat's frame (the camera only pitches there, as ItemInspector places it). The same for every aspect: the
    // lens's angle is the frame's height.
    static float NearCut(Mat mat, Vector2 aimMm, float distance, float fov)
    {
        Vector3 cam = CameraAt(mat, aimMm, distance);
        Vector3 down = Quaternion.Euler(Pitch + fov * .5f, 0f, 0f) * Vector3.forward;
        float printed = mat.origin.y + BenchStage.MatTop;
        if (down.y > -1e-4f || cam.y <= printed) return float.NegativeInfinity;
        Vector3 hit = cam + down * ((printed - cam.y) / down.y);
        return Vector3.Dot(hit - mat.origin, mat.ey) / mat.ey.sqrMagnitude * 1000f;
    }

    // The distance at this aim, from the targets' own nearest `d`, that keeps the rule: `d` when its bottom edge already falls
    // where the rule allows; else just far enough back that it passes the rule's near limit (it moves toward Ace in a straight
    // line with the distance). NaN when no distance up to the search's farthest does.
    static float WithRule(Mat mat, Vector2 aim, float d, NearEdge rule)
    {
        float cut = NearCut(mat, aim, d, LayoutFov);
        if (rule.Keeps(cut)) return d;
        float perTenth = NearCut(mat, aim, d + .1f, LayoutFov) - cut;          // mm the bottom moves per 10 cm further back
        if (!(perTenth < -1e-3f)) return float.NaN;
        float need = d + .1f * (rule.nearTo - cut) / perTenth;
        need = Mathf.Ceil(need * 1000f - 1e-3f) / 1000f;                         // whole millimetres, on the side that keeps it
        return need <= DistanceMax ? need : float.NaN;
    }

    // What the frame's bottom shows of the near ruler, in the report's words. The first three keep a rule; the last three don't.
    static string EdgeWords(float cut)
    {
        if (cut >= NearRulerInnerMm + EdgeClearMm) return $"cleanly cut: grid only, the frame's bottom {cut - NearRulerInnerMm:0.0} mm inside the near ruler";
        if (cut <= MatNearEdgeMm - EdgeClearMm) return $"the near ruler whole, the mat's edge in view with {MatNearEdgeMm - cut:0.0} mm of bench under it";
        if (cut <= NumbersFootMm - NumbersClearMm) return "the ruler's numbers whole, its ticks running off the frame's bottom";
        if (cut > NearRulerInnerMm) return $"grid only, but the near ruler's inner line just {cut - NearRulerInnerMm:0.0} mm under the frame's bottom (the rules want {EdgeClearMm:0})";
        if (cut > NumbersTopMm) return "the near ruler's inner line and its plain strip at the frame's bottom, its numbers just out of view";
        return "A HALF RULER: the frame's bottom crosses the near ruler's numbers, or runs within 1.5 mm under them";
    }

    // One framing: the layout view found under a near-edge rule (null: the plain search), the zoom limit at its distance, and
    // the feel targets' numbers (FrameDevice's own, worked out the same way).
    sealed class Framed
    {
        public NearEdge rule;
        public Vector2 aim, closeAim;
        public float distance, fill, head, fovClose = -1f, fill1, headNotch, notchFov, cut;
        public bool t9, t8, t9Close, t8Notch;
        public bool HasClose => fovClose > 0f;
        /// <summary>Every bar <paramref name="plain"/> meets, this meets too.</summary>
        public bool Keeps(Framed plain) => (t9 || !plain.t9) && (t8 || !plain.t8) && (t9Close || !plain.t9Close) && (t8Notch || !plain.t8Notch);
    }

    static Framed Solve(Mat mat, View[] views, Framing job, NearEdge rule, Vector2 bar, bool screws)
    {
        if (!SearchLayout(mat, views, job, rule, out Vector2 aim, out float distance, out float fill)) return null;
        var f = new Framed { rule = rule, aim = aim, distance = distance, fill = fill };
        Vector3 top = TopOf(job);
        f.head = PxPerMm(top, CamQ(mat, aim, distance), LayoutFov) * BenchScale.ScrewHead * 1000f;
        f.cut = NearCut(mat, aim, distance, LayoutFov);
        if (SearchClose(mat, views, job, distance, out Vector2 closeAim, out float fovClose, out float fill1))
        {
            f.closeAim = closeAim;
            f.fovClose = fovClose;
            f.fill1 = fill1;
            float m = Mathf.Tan(LayoutFov * .5f * Mathf.Deg2Rad) / Mathf.Tan(fovClose * .5f * Mathf.Deg2Rad);
            f.notchFov = 2f * Mathf.Atan(Mathf.Tan(LayoutFov * .5f * Mathf.Deg2Rad) / Mathf.Pow(m, .25f)) * Mathf.Rad2Deg;
            f.headNotch = PxPerMm(top, CamQ(mat, Vector2.Lerp(aim, closeAim, .25f), distance), f.notchFov) * BenchScale.ScrewHead * 1000f;
        }
        // Under a rule, each bar with a margin (23 Oct 2026): a rule's framing is taken only if it clears the bar by more than a
        // hair of this step's model (the plain one is checked at the bar itself, as before).
        float s = rule != null ? 1f : 0f;
        f.t9 = fill >= bar.x + RuleMarginFill * s;
        f.t8 = !screws || f.head >= 12f + RuleMarginPx * s;
        f.t9Close = f.HasClose && f.fill1 >= bar.y + RuleMarginFill * s;
        f.t8Notch = !screws || f.HasClose && f.headNotch >= 15f + RuleMarginPx * s;
        return f;
    }

    // The point T8 is measured at: the device's top, over its rest point.
    static Vector3 TopOf(Framing job) => new Vector3(job.rest.x, job.open.pts.Max(p => p.y), job.rest.z);

    // The bars a framing under a rule would lose against the plain one, in words (with what a rule's framing needs: the bar
    // and its margin).
    static string Lost(Framed f, Framed plain, Vector2 bar)
    {
        var lost = new List<string>();
        if (plain.t9 && !f.t9) lost.Add($"T9 at zoom 0 ({f.fill * 100f:0.0} %; a rule's framing needs {(bar.x + RuleMarginFill) * 100f:0.0})");
        if (plain.t8 && !f.t8) lost.Add($"T8 at zoom 0 ({f.head:0.00} px; it needs {12f + RuleMarginPx:0.0})");
        if (plain.t9Close && !f.t9Close) lost.Add(f.HasClose ? $"T9 at zoom 1 ({f.fill1 * 100f:0.0} %; it needs {(bar.y + RuleMarginFill) * 100f:0.0})" : "the zoom limit");
        if (plain.t8Notch && !f.t8Notch) lost.Add(f.HasClose ? $"T8 one notch in ({f.headNotch:0.00} px; it needs {15f + RuleMarginPx:0.0})" : "T8 one notch in");
        return lost.Count > 0 ? string.Join(", ", lost) : "nothing";
    }

    // ======================================================================= the searches

    // Zoom 0: the aim and distance that make the device tallest (TD §6.6), every pose and compartment clear; under a near-edge
    // rule (23 Oct 2026; null for none), the frame's bottom where the rule allows too.
    static bool SearchLayout(Mat mat, View[] views, Framing job, NearEdge rule, out Vector2 aim, out float distance, out float fill)
    {
        Vector2 bestAim = default;
        float bestD = 0f, bestFill = -1f;
        void Try(Vector2 a)
        {
            if (!Fits(CamQ(mat, a, DistanceMax), LayoutFov, job, views, true)) return;
            float lo = DistanceMin, hi = DistanceMax;
            for (int i = 0; i < 18; i++)
            {
                float mid = (lo + hi) * .5f;
                if (Fits(CamQ(mat, a, mid), LayoutFov, job, views, true)) hi = mid; else lo = mid;
            }
            float d = Mathf.Ceil(hi * 1000f - 1e-3f) / 1000f;      // whole millimetres, on the side that fits
            for (int i = 0; i < 4 && !Fits(CamQ(mat, a, d), LayoutFov, job, views, true); i++) d += .001f;
            if (rule != null)
            {
                // The nearest distance the targets allow, unless the frame's bottom then falls where the rule doesn't: then just
                // far enough back that it passes the rule's near limit (further back, the bottom edge comes toward Ace).
                float e = WithRule(mat, a, d, rule);
                if (float.IsNaN(e)) return;
                if (e > d)
                {
                    d = e;
                    for (int i = 0; i < 4 && !Fits(CamQ(mat, a, d), LayoutFov, job, views, true); i++) d += .001f;
                    if (!Fits(CamQ(mat, a, d), LayoutFov, job, views, true) || !rule.Keeps(NearCut(mat, a, d, LayoutFov))) return;
                }
            }
            float f = Fill(CamQ(mat, a, d), LayoutFov, job);
            if (f > bestFill + 1e-5f || Mathf.Abs(f - bestFill) <= 1e-5f && a.sqrMagnitude < bestAim.sqrMagnitude) { bestAim = a; bestD = d; bestFill = f; }
        }
        for (int x = -240; x <= 240; x += 20) for (int y = -180; y <= 180; y += 20) Try(new Vector2(x, y));
        if (bestFill > 0f)
        {
            Vector2 c = bestAim;
            for (int x = -25; x <= 25; x += 5) for (int y = -25; y <= 25; y += 5) Try(c + new Vector2(x, y));
            c = bestAim;
            for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++) Try(c + new Vector2(x, y));
        }
        aim = bestAim;
        distance = bestD;
        fill = bestFill;
        return bestFill > 0f;
    }

    // Zoom 1: at the layout's distance, the close aim and the narrowest FOV that keep the device alone in view, every pose.
    static bool SearchClose(Mat mat, View[] views, Framing job, float distance, out Vector2 closeAim, out float fov, out float fill)
    {
        Vector2 around = new Vector2(Mathf.Round(job.def.benchLayout.rest.x), Mathf.Round(job.def.benchLayout.rest.y));
        Vector2 bestAim = around;
        float bestFov = 0f, bestFill = -1f;
        void Try(Vector2 a)
        {
            Vector3 camQ = CamQ(mat, a, distance);
            if (!Fits(camQ, LayoutFov, job, views, false)) return;
            float lo = FovMin, hi = LayoutFov;
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * .5f;
                if (Fits(camQ, mid, job, views, false)) hi = mid; else lo = mid;
            }
            float v = Mathf.Ceil(hi * 10f - 1e-3f) / 10f;          // tenths of a degree, on the side that fits
            for (int i = 0; i < 4 && !Fits(camQ, v, job, views, false); i++) v += .1f;
            float f = Fill(camQ, v, job);
            if (f > bestFill + 1e-5f || Mathf.Abs(f - bestFill) <= 1e-5f && (a - around).sqrMagnitude < (bestAim - around).sqrMagnitude) { bestAim = a; bestFov = v; bestFill = f; }
        }
        for (int x = -80; x <= 80; x += 5) for (int y = -80; y <= 80; y += 5) Try(around + new Vector2(x, y));
        if (bestFill > 0f)
        {
            Vector2 c = bestAim;
            for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++) Try(c + new Vector2(x, y));
        }
        closeAim = bestAim;
        fov = bestFov;
        fill = bestFill;
        return bestFill > 0f;
    }

    // ======================================================================= one device

    // stored: the near-edge rule's undo (its saved framings are written back instead of a search's). beforeFirstChange: Bench
    // 9 itself, called by Write just before it changes a prefab (the framings as they were are saved then, once).
    static void FrameDevice(BenchScale.Device d, Mat mat, View[] views, Group rest, Group tray, bool closer, BenchScale.Run run,
                            StoredFramings stored = null, Action beforeFirstChange = null)
    {
        run.Section($"{d.label}: {d.prefab}");
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
        DeviceDefinition def = asset != null ? asset.GetComponent<DeviceDefinition>() : null;
        if (def == null) { run.Check(false, $"{d.prefab} has a DeviceDefinition"); return; }
        var layout = def.benchLayout;
        if (layout.rest == Vector2.zero && layout.scrapTray == Vector2.zero)
        {
            run.Check(false, $"its BenchLayout is set (rest, tool rest, scrap tray: Systems' step 3 / Bench 7 writes Art's values); nothing done");
            return;
        }
        // The search frames the layout the bench will use: the Producer's call 21 (every tool rest at y 0; the phone's tray at
        // (6, 133), the watch's at (26, 107)), which Bench 7 writes onto the prefab. A prefab still on the old one is framed
        // for nothing: say so and stop (run Bench 7 again first).
        BenchLayout want = BenchLayout.ForKind(d.label);
        if (!layout.SamePlaceAs(want))
        {
            run.Check(false, $"its BenchLayout is the Producer's call 21 ({want.rest.x:0}, {want.rest.y:0} / tool rest ({want.toolRestCentreX:0}, {want.toolRestCentreY:0}) / tray ({want.scrapTray.x:0}, {want.scrapTray.y:0})): it has {layout}. Run Bench 7 again (it writes it), then Bench 9; nothing done");
            return;
        }
        bool real = def.realSize;
        run.Info($"realSize {real}{(real ? "" : ": a dry run (numbers and photos; nothing is written until Devices 2 has baked it)")}");
        Framing job = MakeJob(d, def, mat, rest, tray, closer, run);
        Vector2 bar = Bars[d.label];

        // ---- the plain search, then (on Art's mat) the near-edge rule: the framing written is the one chosen here (23 Oct 2026) ----
        bool screws = d.label != "camera";
        Framed plain = Solve(mat, views, job, null, bar, screws);
        if (stored != null)
        {
            PutBack(d, asset, def, real, mat, views, job, rest, tray, plain, stored, run);
            return;
        }
        if (plain == null)
        {
            run.Check(false, $"a layout view exists: no aim and distance up to {DistanceMax} m keeps everything in frame and clear (at {DistanceMax} m: {Why(CamQ(mat, layout.cameraAim, DistanceMax), LayoutFov, job, views, job.targets)})");
            return;
        }
        Framed chosen = NearEdgeFraming(mat, views, job, bar, screws, plain, run);
        Vector2 aim = chosen.aim;
        float distance = chosen.distance, fill = chosen.fill, head = chosen.head;

        // ---- zoom 0 ----
        Vector3 camQ = CamQ(mat, aim, distance);
        Vector3 top = TopOf(job);
        float pxmm = PxPerMm(top, camQ, LayoutFov);
        run.Info($"zoom 0 (the layout view): aim ({aim.x:0}, {aim.y:0}) mm, {distance:0.000} m, FOV {LayoutFov:0}: {fill * 100f:0.0} % of the frame's height; {pxmm:0.00} px/mm at its top (1080p)");
        string binds = Why(CamQ(mat, aim, distance - .002f), LayoutFov, job, views, job.targets);
        if (binds == "nothing" && chosen.rule != null && !chosen.rule.Keeps(NearCut(mat, aim, distance - .002f, LayoutFov))) binds = $"the near-edge rule ({chosen.rule.name})";
        run.Info($"  what binds it 2 mm closer: {binds}");
        run.Check(fill >= bar.x, $"T9 at zoom 0: {fill * 100f:0.0} % ≥ {bar.x * 100f:0} %{(closer ? "" : " with every compartment it uses and the tool rest in view")}");
        if (screws) run.Check(head >= 12f, $"T8: a {BenchScale.ScrewHead * 1000f:0.0} mm screw head is {head:0.0} px at 1080p at zoom 0 (≥ 12)");
        foreach (Cloud t in job.allTargets)
        {
            Box2 b = Project(t.q, camQ, Focal(1080f, LayoutFov), 1920f, 1080f);
            int code = Code(b, views[0], false), code10 = Code(Project(t.q, camQ, Focal(views[1].h, LayoutFov), views[1].w, views[1].h), views[1], false);
            string where = $"{t.name}: x {b.x0:0}–{b.x1:0}, y {b.y0:0}–{b.y1:0} at 1080p ({Words(code)}; 16:10 {Words(code10)})";
            if (job.targets.Contains(t)) run.Info("  " + where);
            else run.Check(false, "LEFT OUT by the closer framing (T13's risk: a piece flown there is out of sight at zoom 0): " + where);
        }

        // ---- zoom 1 (searched at the chosen distance by Solve) ----
        if (!chosen.HasClose)
        {
            run.Check(false, "a zoom limit exists: the device alone doesn't fit even at FOV 40 from the layout distance");
            return;
        }
        Vector2 closeAim = chosen.closeAim;
        float fovClose = chosen.fovClose, fill1 = chosen.fill1, notchFov = chosen.notchFov, headNotch = chosen.headNotch;
        Vector3 camQ1 = CamQ(mat, closeAim, distance);
        float pxmm1 = PxPerMm(top, camQ1, fovClose);
        float m = Mathf.Tan(LayoutFov * .5f * Mathf.Deg2Rad) / Mathf.Tan(fovClose * .5f * Mathf.Deg2Rad);
        run.Info($"zoom 1 (the limit): aim ({closeAim.x:0}, {closeAim.y:0}) mm at the same {distance:0.000} m, FOV {fovClose:0.0}: {fill1 * 100f:0.0} %; {pxmm1:0.00} px/mm; magnification {m:0.00}");
        run.Info($"  what binds it 0.5° narrower: {Why(camQ1, fovClose - .5f, job, views, Array.Empty<Cloud>())}");
        run.Info($"  one wheel notch (z 0.25): FOV {notchFov:0.0}, a screw head {headNotch:0.0} px");
        run.Check(fill1 >= bar.y, $"T9 at zoom 1: {fill1 * 100f:0.0} % ≥ {bar.y * 100f:0} %");
        if (screws) run.Check(headNotch >= 15f, $"T8: one notch in, a screw head is {headNotch:0.0} px (≥ 15)");
        if (mat.ruler)
            run.Info($"  the frame's bottom on the mat: y {chosen.cut:0.0} mm at zoom 0, {NearCut(mat, Vector2.Lerp(aim, closeAim, .25f), distance, notchFov):0.0} one notch in, " +
                     $"{NearCut(mat, closeAim, distance, fovClose):0.0} at the limit (the near ruler's inner line is y {NearRulerInnerMm:0}, the mat's edge {MatNearEdgeMm:0})");

        // ---- write (one Undo group per prefab; nothing when it is already so) ----
        if (!real) run.Info("NOT WRITTEN: the prefab isn't real size yet (Devices 2).");
        else Write(asset, def, aim, distance, closeAim, fovClose, d.label, run, beforeFirstChange);

        Photos(d, asset, mat, views, job, rest, tray, aim, distance, closeAim, fovClose, run);
    }

    // The near edge (23 Oct 2026): the plain search's framing is kept when its bottom edge already keeps rule 1 or rule 2 (or
    // the mat has no printed ruler), so only a true half ruler is re-framed; else the first rule, strictest first, whose
    // framing keeps every bar the plain one keeps, with the margin; else the plain one, with a FAIL row that names what each
    // rule would cost (the ruler's own fix is then a Blender change).
    static Framed NearEdgeFraming(Mat mat, View[] views, Framing job, Vector2 bar, bool screws, Framed plain, BenchScale.Run run)
    {
        if (!mat.ruler) return plain;
        run.Info($"the near edge: the plain search puts the frame's bottom at mat y {plain.cut:0.0} mm: {EdgeWords(plain.cut)}");
        // A bottom that already reads stays as it is: Grace's camera (the Day 1 repair) has the numbers whole, and its framing is
        // the one the spec held up as the set the phone's should match.
        if (Strict.Keeps(plain.cut) || NumbersWhole.Keeps(plain.cut))
        {
            run.Check(true, $"the near edge reads at zoom 0: {EdgeWords(plain.cut)}");
            return plain;
        }
        foreach (NearEdge rule in new[] { Strict, NumbersWhole })
        {
            Framed f = Solve(mat, views, job, rule, bar, screws);
            if (f != null && f.Keeps(plain))
            {
                run.Info($"  under the rule \"{rule.name}\": aim ({f.aim.x:0}, {f.aim.y:0}) mm, {f.distance:0.000} m, {f.fill * 100f:0.0} % (plain {plain.fill * 100f:0.0} %), " +
                         $"a screw head {f.head:0.0} px (plain {plain.head:0.0}): every bar the plain framing meets, still met. Taken.");
                run.Check(true, $"the near edge reads at zoom 0: {EdgeWords(f.cut)} (y {f.cut:0.0} mm)");
                return f;
            }
            run.Info($"  under the rule \"{rule.name}\": " + (f == null ? "no layout view keeps it"
                     : $"aim ({f.aim.x:0}, {f.aim.y:0}) mm, {f.distance:0.000} m, {f.fill * 100f:0.0} % would cost {Lost(f, plain, bar)}: not taken"));
        }
        run.Check(false, $"the near edge reads at zoom 0: left as the plain search frames it from today's inputs ({EdgeWords(plain.cut)}), since each rule costs a feel-target bar " +
                         "for this device (with its margin); the ruler's own fix is then a Blender change to the mat's print (bench_stage_textures.py / bench_stage.py: the near ruler " +
                         "moved in from the edge, or the near strip printed plain)");
        return plain;
    }

    // ======================================================================= the near-edge rule's undo (23 Oct 2026)

    // Every device's framing on its prefab as it is now (the four fields Write writes), to save before the first change.
    static StoredFramings CurrentFramings()
    {
        var all = new StoredFramings
        {
            saved = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            why = "the devices' close-up framings just before Bench 9's near-edge rule (23 Oct 2026) first changed a prefab; " +
                  "Fixit Fidget > Bench > " + Title9Undo + " writes them back",
        };
        foreach (BenchScale.Device d in BenchScale.Devices)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
            DeviceDefinition def = asset != null ? asset.GetComponent<DeviceDefinition>() : null;
            if (def == null || def.benchLayout == null) continue;
            BenchLayout l = def.benchLayout;
            all.devices.Add(new StoredFraming { prefab = d.prefab, cameraAim = l.cameraAim, cameraDistance = l.cameraDistance, closeAim = l.closeAim, fovClose = l.fovClose });
        }
        return all;
    }

    // Saved once, ever: a file that is there already holds the framings from before the rule, and is kept as it is.
    static void SaveBefore(StoredFramings before, BenchScale.Run run)
    {
        string path = BeforePath;
        if (File.Exists(path))
        {
            run.Info($"the framings from before the near-edge rule stay as first saved: {path} (written once; delete it to save today's instead)");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(before, true));
        run.Info($"saved the framings as they were, before the first change: {path} ({before.devices.Count} prefab(s)); \"{Title9Undo}\" writes them back");
    }

    // The undo for one device: today's plain search compared with the stored framing (so the report says whether the undo
    // equals what the plain search would give now), then the stored framing written back as it was, and its photos.
    static void PutBack(BenchScale.Device d, GameObject asset, DeviceDefinition def, bool real, Mat mat, View[] views, Framing job, Group rest, Group tray,
                        Framed plain, StoredFramings stored, BenchScale.Run run)
    {
        StoredFraming s = stored.devices.FirstOrDefault(x => x != null && x.prefab == d.prefab);
        if (s == null)
        {
            run.Check(false, $"a framing stored for {d.prefab} in {BeforePath}: none, so nothing is put back for it");
            return;
        }
        string storedWords = FramingWords(s.cameraAim, s.cameraDistance, s.closeAim, s.fovClose);
        bool same = plain != null && plain.HasClose && SameFraming(plain.aim, plain.distance, plain.closeAim, plain.fovClose, s.cameraAim, s.cameraDistance, s.closeAim, s.fovClose);
        run.Info("the plain search gives the stored framing: " + (same ? $"yes ({storedWords})"
                 : $"NO: stored {storedWords}; today's plain search gives " +
                   (plain == null ? "no layout view" : plain.HasClose ? FramingWords(plain.aim, plain.distance, plain.closeAim, plain.fovClose) : "no zoom limit") +
                   " (its inputs changed since, or the stored one was Bench 9b's: the stored framing is the one written back)"));
        if (mat.ruler && s.cameraDistance > 0f)
        {
            float cut = NearCut(mat, s.cameraAim, s.cameraDistance, LayoutFov);
            run.Info($"  the stored framing's bottom on the mat: y {cut:0.0} mm: {EdgeWords(cut)}");
        }
        if (!real)
        {
            run.Info("NOT WRITTEN: the prefab isn't real size yet (Devices 2).");
            return;
        }
        Write(asset, def, s.cameraAim, s.cameraDistance, s.closeAim, s.fovClose, d.label, run);
        if (s.cameraDistance > 0f && s.fovClose > 0f) Photos(d, asset, mat, views, job, rest, tray, s.cameraAim, s.cameraDistance, s.closeAim, s.fovClose, run);
    }

    // Write's own tolerances (half a millimetre, half a millimetre of distance, a twentieth of a degree).
    static bool SameFraming(Vector2 aim, float distance, Vector2 closeAim, float fovClose, Vector2 aim2, float distance2, Vector2 closeAim2, float fovClose2) =>
        (aim - aim2).magnitude <= .5f && Mathf.Abs(distance - distance2) <= .0005f && (closeAim - closeAim2).magnitude <= .5f && Mathf.Abs(fovClose - fovClose2) <= .05f;

    static string FramingWords(Vector2 aim, float distance, Vector2 closeAim, float fovClose) =>
        $"aim ({aim.x:0}, {aim.y:0}) at {distance:0.000} m, close ({closeAim.x:0}, {closeAim.y:0}) at FOV {fovClose:0.0}";

    // beforeFirstChange: called once something will change, before the change (Bench 9 saves the framings as they were then).
    static void Write(GameObject asset, DeviceDefinition def, Vector2 aim, float distance, Vector2 closeAim, float fovClose, string label, BenchScale.Run run,
                      Action beforeFirstChange = null)
    {
        var so = new SerializedObject(def);
        SerializedProperty a = so.FindProperty("benchLayout.cameraAim"), dist = so.FindProperty("benchLayout.cameraDistance");
        SerializedProperty c = so.FindProperty("benchLayout.closeAim"), f = so.FindProperty("benchLayout.fovClose");
        if (a == null || dist == null || c == null || f == null) { run.Check(false, "BenchLayout has cameraAim, cameraDistance, closeAim and fovClose (the bench builder's DeviceDefinition)"); return; }
        if ((a.vector2Value - aim).magnitude <= .5f && Mathf.Abs(dist.floatValue - distance) <= .0005f && (c.vector2Value - closeAim).magnitude <= .5f && Mathf.Abs(f.floatValue - fovClose) <= .05f)
        {
            run.Info("already framed so: nothing written");
            return;
        }
        string was = $"aim ({a.vector2Value.x:0}, {a.vector2Value.y:0}) at {dist.floatValue:0.000} m, close ({c.vector2Value.x:0}, {c.vector2Value.y:0}) at FOV {f.floatValue:0.0}";
        beforeFirstChange?.Invoke();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bench 9 - frame the " + label);
        a.vector2Value = aim;
        dist.floatValue = distance;
        c.vector2Value = closeAim;
        f.floatValue = fovClose;
        so.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssetIfDirty(asset);
        run.Changed($"{AssetDatabase.GetAssetPath(asset)} BenchLayout: {was} → aim ({aim.x:0}, {aim.y:0}) at {distance:0.000} m, close ({closeAim.x:0}, {closeAim.y:0}) at FOV {fovClose:0.0} (Undo: \"Bench 9 - frame the {label}\")");
    }

    // ======================================================================= photos

    static void Photos(BenchScale.Device d, GameObject asset, Mat mat, View[] views, Framing job, Group rest, Group tray,
                       Vector2 aim, float distance, Vector2 closeAim, float fovClose, BenchScale.Run run)
    {
        try
        {
            using (var s = new BenchScale.Studio())
            {
                Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                s.Proxy("Bench top (photo)", cube, new[] { s.Flat(new Color(.42f, .27f, .16f)) }, new Vector3(0f, mat.top - .025f, 0f), Quaternion.identity, new Vector3(1.4f, .04f, .9f));
                foreach (MeshRenderer r in mat.stage.matFrame.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || !r.enabled || !r.gameObject.activeInHierarchy || Excluded(mat.stage, r.transform)) continue;
                    Matrix4x4 m = mat.toMat * mf.transform.localToWorldMatrix;
                    s.Proxy(r.name + " (photo)", mf.sharedMesh, r.sharedMaterials, m.GetColumn(3), m.rotation, m.lossyScale);
                }
                foreach (var (mesh, mats, m) in rest.looks) s.Proxy("Tool rest (photo)", mesh, mats, (Vector3)m.GetColumn(3) + job.restDelta, m.rotation, m.lossyScale);
                foreach (var (mesh, mats, m) in tray.looks) s.Proxy("Scrap tray (photo)", mesh, mats, (Vector3)m.GetColumn(3) + job.trayDelta, m.rotation, m.lossyScale);
                GameObject device = s.Place(asset);
                if (device != null)
                {
                    foreach (Renderer r in device.GetComponentsInChildren<Renderer>(true)) if (!DeviceBounds.Counts(r)) r.enabled = false;
                    device.transform.SetPositionAndRotation(job.openRoot, job.openRot);
                }
                var shots = new List<string>();
                for (int zoom = 0; zoom <= 1; zoom++)
                    foreach (Vector2Int size in PhotoSizes)
                    {
                        Vector3 cam = CameraAt(mat, zoom == 0 ? aim : closeAim, distance);
                        float fov = zoom == 0 ? LayoutFov : fovClose;
                        Texture2D picture = s.Shoot(cam, Look, fov, size.x, size.y);
                        Overlay(picture, Unlook * cam, fov, job, zoom == 0);
                        string file = Path.Combine(run.Folder, $"{d.label}-zoom{zoom}-{(size.y * 16 == size.x * 9 ? "16x9" : "16x10")}.png");
                        BenchScale.SavePng(picture, file);
                        Object.DestroyImmediate(picture);
                        shots.Add(Path.GetFileName(file));
                    }
                run.Info($"photos (zones orange, the device cyan, compartments green or red): {string.Join(", ", shots)}");
            }
        }
        catch (Exception e) { run.Info($"the photos weren't taken: {e.Message}"); }
    }

    // The zones, the device's box (every pose: dim; the pose it opens in: bright) and, at zoom 0, each compartment's box.
    static void Overlay(Texture2D picture, Vector3 camQ, float fov, Framing job, bool compartments)
    {
        int w = picture.width, h = picture.height;
        View v = MakeView(w, h);
        int thick = Mathf.Max(2, h / 400);
        float focal = Focal(h, fov);
        Color32[] px = picture.GetPixels32();
        for (int i = 0; i < v.zones.Length; i++)
        {
            Rect z = v.zones[i];
            BenchScale.Outline(px, w, h, z.xMin, z.yMin, z.xMax, z.yMax, v.u6[i] ? new Color32(255, 210, 120, 255) : new Color32(255, 150, 30, 255), thick);
        }
        foreach (Cloud pose in job.poses)
        {
            Box2 b = Project(pose.q, camQ, focal, w, h);
            if (b.ok && pose != job.open) BenchScale.Outline(px, w, h, b.x0, b.y0, b.x1, b.y1, new Color32(40, 120, 150, 255), 1);
        }
        Box2 o = Project(job.open.q, camQ, focal, w, h);
        if (o.ok) BenchScale.Outline(px, w, h, o.x0, o.y0, o.x1, o.y1, new Color32(40, 230, 255, 255), thick);
        if (compartments)
            foreach (Cloud t in job.allTargets)
            {
                Box2 b = Project(t.q, camQ, focal, w, h);
                if (!b.ok) continue;
                bool clear = Code(b, v, false) < 0;
                BenchScale.Outline(px, w, h, b.x0, b.y0, b.x1, b.y1, clear ? new Color32(60, 220, 90, 255) : new Color32(240, 60, 60, 255), thick);
            }
        picture.SetPixels32(px);
        picture.Apply(false);
    }
}
#endif
