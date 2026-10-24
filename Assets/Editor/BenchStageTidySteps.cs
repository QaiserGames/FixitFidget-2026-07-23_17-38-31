#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE PHONE'S "TRAY ON THE RIGHT": THE OLD V2 TRAY'S DARK FRAME OFF THE MAT (23 Oct 2026; review23 spec C, part 2)
//
//   Fixit Fidget > Bench > Bench 7b - The old tray's dark frame off the mat (scene)
//   Fixit Fidget > Bench > Bench 7b - Undo: the old frame back (scene)
//
// His note (22 Oct 2026, his own playtest): "I did notice that the mat shows an inconsistency when working with the phone. Like
// the tray on the right and the bottom of the mat kind of look off." The 19 Oct 2026 reviewers saw the same (bench-a S2: "the old
// square parts tray's dark rim stands on the new mat with no floor inside it, over the cover well"; bench-b S2: the new-part
// pad "runs under the dark well frame's left wall"; bench-b S3: "near-black with 30 px walls, the heaviest shape on the mat").
//
// WHAT THE BLACK "WELL" WAS. Not the well. The mat's cover well is moulded into the silicone (Art's A1, Tools/Blender/
// bench_stage.py: x 182..262, y -82..84 mm, 3 mm deep inside a raised lip 2 mm wide and 0.8 mm high, its walls and floor in
// the mat's deeper blue, BM_Recess, as the cups' walls are), 10 mm right of the pale new-part pad (x 90..170) and 36 mm
// inside the mat's right edge. The thick dark frame round it was the v2 stage's magnetic parts tray (Bench 2, 7 Oct 2026,
// Playtest3Session4Steps: the group "Parts tray", a floor, four walls 170 x 170 mm outside, 24 mm tall and 5 mm thick in
// DC_SteelDark (dark steel), and its magnet zone, a trigger; centred 225 mm right of the rig). Bench 7 (14 Oct 2026) meant to
// switch it off with the v2 slab and caddy, but BenchStage keeps the tray's FLOOR as its legacy tray (Bench 2 wired
// `trayFloor`), so only the floor went off and the four dark walls stayed standing on the new mat: mat x 140..310 (10 mm
// past its right edge, onto the bench's wood), y -85..85, the west wall across the new-part pad, round the moulded well.
// Every device's close-up had it; the phone's, framed tightest, showed it worst.
//
// THE FIX, the smallest: the whole v2 tray (the group: its walls and its magnet zone; its floor is already off) switched off,
// never deleted, as Bench 7 meant to do. What's left on the right is Art's own mat: the pad and the moulded well, each a
// compartment of the one silicone mat, like the cups. Bench 7 now switches off the whole group too (for a fresh stage). The
// step refuses if anything the bench uses is under the group (BenchStage's places, the rest, the old-parts tray, a tool, a
// device): nothing the repair needs can go off with it.
//
// Checks after (PASS / FAIL in the report): the group off; nothing else of the v2 stage still drawn; nothing drawn standing
// on the mat's right side (the pad and the well, x 88..300 mm) but the mat itself; and, for each device's layout
// (BenchLayout's tool rest and old-parts tray, Art's cups, pad and well), every place on the mat inside the mat's top with
// its margin listed (under 5 mm called tight), and which places overlap. The phone's layout is checked; the camera's and the
// watch's are listed (what they show is for the report, not this step's to change).
// Photos before and after: each device's layout view (its prefab's Bench 9 framing, the way ItemInspector places the
// close-up; the rest and the tray moved to that device's layout for the photo and put back) and the mat from above.
// Saves the scene, as Bench 7 does. Report and photos: Logs/Bench/frame-off-<time>/ (the undo: frame-back-<time>/).
// The undo switches the group back on, as it was: its pieces as they were left inside it (on a stage Bench 7 built before
// 23 Oct 2026, as his is: the walls drawn, the floor off). Unity's own Undo works too.
// ---------------------------------------------------------------------------
static class BenchStageTidySteps
{
    const string Menu = "Fixit Fidget/Bench/";
    const string Tag = "[Bench 7b] ";
    const string Title = "Bench 7b - The old tray's dark frame off the mat";
    const string UndoTitle = "Bench 7b - Undo: the old frame back";
    const string MenuDo = Menu + Title + " (scene)";
    const string MenuUndo = Menu + UndoTitle + " (scene)";
    const string StageName = "Stage (v2)";                 // ForgivingBenchSteps.StageName
    /// <summary>The v2 tray's group (Playtest3Session4Steps.BuildStage): its Floor is BenchStage's legacy tray.</summary>
    internal const string OldTrayName = "Parts tray";

    // Art's mat (Tools/Blender/bench_stage.py, mm in the mat's frame: +x the player's right, +y away): 600 x 360, its flat top
    // inside a 1.5 mm chamfer; the new-part pad x 90..170, y -82..84; the cover well x 182..262, y -82..84 inside a 2 mm lip;
    // each cup 9 mm round with a 1 mm rim.
    const float MatTopHalfX = 298.5f, MatTopHalfY = 178.5f;
    static readonly Rect PadMm = Rect.MinMaxRect(90f, -82f, 170f, 84f);
    static readonly Rect WellMm = Rect.MinMaxRect(180f, -84f, 264f, 86f);
    const float CupOuterMm = 10f;
    const float TightMm = 5f;
    // The mat's right side, where only the mat itself may stand: the pad and the well with 2 mm round them, up to 40 mm.
    static readonly Rect RightSideMm = Rect.MinMaxRect(88f, -90f, 300f, 90f);
    const float RightSideUp = .04f;

    [MenuItem(MenuDo)]
    static void TakeTheFrameOff() => Run(false);

    [MenuItem(MenuUndo)]
    static void PutTheFrameBack() => Run(true);

    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the step

    static void Run(bool back)
    {
        string title = back ? UndoTitle : Title;
        var report = new StringBuilder();
        report.AppendLine(title + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        report.AppendLine();
        int fails = 0;
        void Check(bool ok, string what)
        {
            if (!ok) fails++;
            report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }
        void Info(string what) => report.AppendLine("  info  " + what);
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first: this step changes the scene.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != AcesCafeLayoutSetup.ScenePath) throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            BenchRig rig = Object.FindAnyObjectByType<BenchRig>(FindObjectsInactive.Include);
            BenchStage stage = rig != null ? rig.GetComponent<BenchStage>() : null;
            Transform stageRoot = rig != null ? rig.transform.Find(StageName) : null;
            if (rig == null || stage == null || stageRoot == null)
                throw new InvalidOperationException("No bench stage (the Workbench's InspectRig with its BenchStage and " + StageName + ").");
            if (stage.matFrame == null || stage.toolRest == null || stage.scrapTray == null)
                throw new InvalidOperationException("The bench has no repair mat, tool rest or old-parts tray yet: run Fixit Fidget > Bench > Bench 7 - The forgiving stage (scene) first.");

            Transform group = OldTray(stage, stageRoot);
            if (group == null)
            {
                Debug.Log(Tag + $"No v2 tray ('{OldTrayName}') under InspectRig/{StageName}: nothing to {(back ? "put back" : "take off")}.");
                return;
            }
            string holds = Holds(stage, group);
            if (holds != null) throw new InvalidOperationException($"'{PathOf(group)}' holds {holds}: switching it off would hide what the bench uses. Nothing changed.");

            report.AppendLine("What it is:");
            Describe(stage, group, Info);
            report.AppendLine();
            if (back ? group.gameObject.activeSelf : !group.gameObject.activeSelf)
            {
                Debug.Log(Tag + $"'{PathOf(group)}' is already {(back ? "on" : "off")}: nothing changed.\n{report}");
                return;
            }

            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Bench",
                (back ? "frame-back-" : "frame-off-") + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            Photos(folder, "1-before", stage, report);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(group.gameObject, title);
            group.gameObject.SetActive(back);
            Undo.CollapseUndoOperations(undoGroup);
            Physics.SyncTransforms();

            report.AppendLine("Checks after:");
            if (back)
                Check(group.gameObject.activeSelf, $"'{PathOf(group)}' is back on, as it was (its pieces as they were left inside it: on a stage Bench 7 built before 23 Oct 2026, its walls drawn and its floor off)");
            else
            {
                Check(!group.gameObject.activeInHierarchy, $"'{PathOf(group)}' is off (not deleted): its four dark walls and its magnet zone with it");
                List<string> left = StillDrawn(stage, stageRoot);
                Check(left.Count == 0, $"nothing else of the v2 stage is still drawn (the mat, the rest, the old-parts tray and the tools aside)" +
                                       (left.Count > 0 ? ": " + string.Join("; ", left) : ""));
                List<string> onRight = OnTheRightSide(stage);
                Check(onRight.Count == 0, $"nothing but the mat itself stands on its right side (the pad and the moulded well, mat x {RightSideMm.xMin:0}..{RightSideMm.xMax:0}, " +
                                          $"y {RightSideMm.yMin:0}..{RightSideMm.yMax:0} mm, up to {RightSideUp * 1000f:0} mm)" + (onRight.Count > 0 ? ": " + string.Join("; ", onRight) : ""));
                report.AppendLine();
                report.AppendLine("The places on the mat, per device layout (mm; the mat's flat top is x ±298.5, y ±178.5):");
                Layouts(stage, Check, Info);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Photos(folder, "2-after", stage, report);
            report.AppendLine();
            report.AppendLine(back ? $"Saved. To take it off again: {MenuDo.Substring(Menu.Length)}."
                                   : $"Saved. Undo: Edit > Undo \"{Title}\" (this session), or {MenuUndo.Substring(Menu.Length)}.");
            report.AppendLine(fails == 0 ? "All good." : fails + " check(s) FAILED (the change is kept: each FAIL names what to look at).");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            string line = Tag + (back ? "The old frame is back on the mat. " : "The old tray's dark frame is off the mat. ") +
                          (fails == 0 ? "" : fails + " check(s) FAILED. ") + "Report and photos: " + folder + "\n" + report;
            if (fails == 0) Debug.Log(line); else Debug.LogError(line);
        }
        catch (Exception e)
        {
            if (folder != null) { try { File.WriteAllText(Path.Combine(folder, "report.txt"), report + "\nFAILED: " + e.Message); } catch (Exception) { } }
            Debug.LogError(Tag + title + " FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // ================================================================== what it finds

    /// <summary>The v2 tray's group: the parent of BenchStage's legacy tray when that is it, else a child of the v2 stage by name.</summary>
    internal static Transform OldTray(BenchStage stage, Transform stageRoot)
    {
        Transform floor = stage != null ? stage.LegacyTray : null;
        if (floor != null && floor.parent != null && floor.parent.name == OldTrayName && floor.parent.IsChildOf(stageRoot)) return floor.parent;
        foreach (Transform child in stageRoot) if (child.name == OldTrayName) return child;
        return null;
    }

    // Anything the bench uses under the group (null: nothing).
    static string Holds(BenchStage stage, Transform group)
    {
        var used = new List<(string what, Transform t)>
        {
            ("the mat", stage.matFrame), ("the tool rest", stage.toolRest), ("the old-parts tray", stage.scrapTray),
            ("the small pad", stage.padSmall), ("the large pad", stage.padLarge), ("the small well", stage.wellSmall), ("the large well", stage.wellLarge),
        };
        if (stage.cups != null) for (int i = 0; i < stage.cups.Length; i++) used.Add(($"cup {i + 1}", stage.cups[i]));
        if (stage.scrapSlots != null) for (int i = 0; i < stage.scrapSlots.Length; i++) used.Add(($"scrap slot {i + 1}", stage.scrapSlots[i]));
        if (stage.toolSlots != null) for (int i = 0; i < stage.toolSlots.Length; i++) used.Add(($"tool seat {i + 1}", stage.toolSlots[i]));
        foreach (var (what, t) in used) if (t != null && t.IsChildOf(group)) return what;
        ToolPickup tool = group.GetComponentInChildren<ToolPickup>(true);
        if (tool != null) return "a tool (" + tool.name + ")";
        JobBase job = group.GetComponentInChildren<JobBase>(true);
        if (job != null) return "a device (" + job.name + ")";
        return null;
    }

    // Each child of the group: on or off, its material, its solid, where it is on the mat.
    static void Describe(BenchStage stage, Transform group, Action<string> info)
    {
        info($"'{PathOf(group)}': {(group.gameObject.activeSelf ? "on" : "off")}, {group.childCount} piece(s) " +
             "(Bench 2's v2 parts tray, 7 Oct 2026: Bench 7 switched off only its floor, which BenchStage keeps as its legacy tray).");
        foreach (Transform t in group)
        {
            Renderer r = t.GetComponent<Renderer>();
            Collider c = t.GetComponent<Collider>();
            string drawn = r != null ? $"drawn in {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))}" : "nothing drawn";
            string solid = c != null ? $"{c.GetType().Name}{(c.isTrigger ? " (a trigger)" : "")}{(c.enabled ? "" : ", off")}" : "no collider";
            string where = r != null ? " " + OnMat(stage, r.bounds) : "";
            info($"  {t.name}: {(t.gameObject.activeSelf ? "on" : "off")}, {drawn}, {solid}{where}");
        }
    }

    // A world box as it lies on the mat: its footprint in mat mm and its top over the mat's top.
    static string OnMat(BenchStage stage, Bounds b)
    {
        Rect r = FootprintMm(stage, b, out float up);
        string past = r.xMax > 300f || r.xMin < -300f || r.yMax > 180f || r.yMin < -180f ? " (PAST THE MAT'S EDGE)" : "";
        return $"at mat x {r.xMin:0}..{r.xMax:0}, y {r.yMin:0}..{r.yMax:0} mm, its top {up * 1000f:0} mm over the mat{past}";
    }

    static Rect FootprintMm(BenchStage stage, Bounds b, out float topUp)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        topUp = float.MinValue;
        Vector3 top = stage.MatToWorld(Vector2.zero, BenchStage.MatTop);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector2 mm = stage.WorldToMat(corner);
            x0 = Mathf.Min(x0, mm.x); x1 = Mathf.Max(x1, mm.x); y0 = Mathf.Min(y0, mm.y); y1 = Mathf.Max(y1, mm.y);
            topUp = Mathf.Max(topUp, Vector3.Dot(corner - top, stage.MatUp));
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    // What is still drawn under the v2 stage that isn't the forgiving stage's own (the mat, the rest, the tray, the tools).
    static List<string> StillDrawn(BenchStage stage, Transform stageRoot)
    {
        var left = new List<string>();
        foreach (Renderer r in stageRoot.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || Ours(stage, r.transform)) continue;
            left.Add($"{PathOf(r.transform)} {OnMat(stage, r.bounds)}");
        }
        return left;
    }

    static bool Ours(BenchStage stage, Transform t) =>
        stage.matFrame != null && t.IsChildOf(stage.matFrame) || stage.toolRest != null && t.IsChildOf(stage.toolRest)
        || stage.scrapTray != null && t.IsChildOf(stage.scrapTray) || t.GetComponentInParent<ToolPickup>(true) != null;

    // Anything drawn standing on the mat's right side (the pad and the well) that isn't the forgiving stage's own (the mat, the
    // rest, the old-parts tray, the tools) or a device. Things the size of a piece on a mat only: a box under 0.3 m tall and
    // 0.8 m across (the bench's own mesh, a lamp reaching over it, the counter round it all have boxes that take in the mat).
    static List<string> OnTheRightSide(BenchStage stage)
    {
        var found = new List<string>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r == null || !r.enabled || r.gameObject.scene != stage.gameObject.scene) continue;
            if (r is ParticleSystemRenderer || r.GetComponent<BenchOutlineHull>() != null) continue;
            if (Ours(stage, r.transform) || r.GetComponentInParent<JobBase>(true) != null) continue;
            Rect foot = FootprintMm(stage, r.bounds, out float up);
            float tall = Vector3.Dot(r.bounds.size, Abs(stage.MatUp));
            if (tall > .3f || foot.width > 800f || foot.height > 800f) continue;
            if (!foot.Overlaps(RightSideMm) || up < 0f || up - tall > RightSideUp) continue;
            found.Add($"{PathOf(r.transform)} {OnMat(stage, r.bounds)}");
        }
        return found;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ================================================================== the places on the mat, per device layout

    sealed class Place
    {
        public string what;
        public Rect mm;
        public Place(string what, Rect mm) { this.what = what; this.mm = mm; }
    }

    // For each device's layout: the cups, the pad and the well (Art's, where the mat's empties put them) and the tool rest
    // and the old-parts tray (their drawn footprints, moved to the layout): inside the mat's top, and overlapping nothing.
    static void Layouts(BenchStage stage, Action<bool, string> check, Action<string> info)
    {
        Rect restFoot = Footprint(stage, stage.toolRest, true), trayFoot = Footprint(stage, stage.scrapTray, false);
        Vector2 padAt = stage.padLarge != null ? stage.WorldToMat(stage.padLarge.position) : new Vector2(130f, 0f);
        Vector2 wellAt = stage.wellLarge != null ? stage.WorldToMat(stage.wellLarge.position) : new Vector2(222f, 0f);
        // Art's pad and well about their empties (the empties: Pad_Large (130, 0), Well_Large (222, 0)), so a mat whose empties
        // moved is measured where they are.
        Rect pad = Shift(PadMm, padAt - new Vector2(130f, 0f)), well = Shift(WellMm, wellAt - new Vector2(222f, 0f));
        Rect cups = Rect.zero;
        bool anyCup = false;
        if (stage.cups != null)
            foreach (Transform cup in stage.cups)
            {
                if (cup == null) continue;
                Vector2 c = stage.WorldToMat(cup.position);
                Rect one = Rect.MinMaxRect(c.x - CupOuterMm, c.y - CupOuterMm, c.x + CupOuterMm, c.y + CupOuterMm);
                cups = anyCup ? Rect.MinMaxRect(Mathf.Min(cups.xMin, one.xMin), Mathf.Min(cups.yMin, one.yMin), Mathf.Max(cups.xMax, one.xMax), Mathf.Max(cups.yMax, one.yMax)) : one;
                anyCup = true;
            }
        foreach (BenchScale.Device d in BenchScale.Devices)
        {
            BenchLayout layout = BenchLayout.ForKind(d.label);
            var places = new List<Place> { new Place("the new-part pad", pad), new Place("the cover well (with its lip)", well) };
            if (anyCup) places.Insert(0, new Place("the six cups", cups));
            places.Add(new Place($"the tool rest at ({layout.toolRestCentreX:0}, {layout.toolRestCentreY:0})", Shift(restFoot, new Vector2(layout.toolRestCentreX, layout.toolRestCentreY))));
            places.Add(new Place($"the old-parts tray at ({layout.scrapTray.x:0}, {layout.scrapTray.y:0})", Shift(trayFoot, layout.scrapTray)));
            bool phone = d.label == "phone";
            var margins = new List<string>();
            bool inside = true;
            foreach (Place p in places)
            {
                float margin = Mathf.Min(Mathf.Min(p.mm.xMin + MatTopHalfX, MatTopHalfX - p.mm.xMax), Mathf.Min(p.mm.yMin + MatTopHalfY, MatTopHalfY - p.mm.yMax));
                if (margin < 0f) inside = false;
                margins.Add($"{p.what} x {p.mm.xMin:0}..{p.mm.xMax:0}, y {p.mm.yMin:0}..{p.mm.yMax:0}: {margin:0.0} mm in{(margin < 0f ? " (OVER THE EDGE)" : margin < TightMm ? " (tight)" : "")}");
            }
            var overlaps = new List<string>();
            for (int i = 0; i < places.Count; i++)
                for (int j = i + 1; j < places.Count; j++)
                {
                    Rect a = places[i].mm, b = places[j].mm;
                    float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (ox > 0f && oy > 0f) overlaps.Add($"{places[i].what} and {places[j].what} ({ox:0} x {oy:0} mm)");
                }
            info($"{d.label}: " + string.Join("; ", margins));
            string inWords = $"{d.label}: every place lies on the mat's top, nothing past its edge";
            string overWords = $"{d.label}: no two places overlap" + (overlaps.Count > 0 ? ": " + string.Join("; ", overlaps) : "");
            if (phone) { check(inside, inWords); check(overlaps.Count == 0, overWords); }
            else
            {
                // Not this step's to change (BenchLayout's presets are Systems' data): listed for the report.
                info((inside ? "" : "NOTE ") + inWords + (inside ? "" : ": NOT SO"));
                info((overlaps.Count == 0 ? "" : "NOTE ") + overWords);
            }
        }
    }

    // A stage piece's drawn footprint about its own place on the mat (mm), its tools included for the rest; Art's sizes for an
    // empty one (A2: the rest 110 x 230; A3: the tray's rim 159 x 89).
    static Rect Footprint(BenchStage stage, Transform piece, bool rest)
    {
        Vector2 at = stage.WorldToMat(piece.position);
        bool any = false;
        Bounds all = default;
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r.GetComponent<BenchOutlineHull>() != null) continue;
            if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
        }
        if (!any) return rest ? Rect.MinMaxRect(-55f, -115f, 55f, 115f) : Rect.MinMaxRect(-79.6f, -44.6f, 79.6f, 44.6f);
        Rect foot = FootprintMm(stage, all, out _);
        return Shift(foot, -at);
    }

    static Rect Shift(Rect r, Vector2 by) => new Rect(r.position + by, r.size);

    // ================================================================== photos

    // Each device's layout view (its prefab's framing, placed as ItemInspector.PlaceCamera places the close-up; the scene's
    // close-up camera when a prefab has no framing yet), with the rest and the tray where that device's layout puts them for the
    // photo only (BenchStage.PlaceForDevice's way), then put back; and the mat from straight above.
    static void Photos(string folder, string prefix, BenchStage stage, StringBuilder report)
    {
        Transform rest = stage.toolRest, tray = stage.scrapTray;
        // Kept as local poses, so putting them back is exact (nothing in the scene moves for a photo).
        Vector3 restAt = rest.localPosition, trayAt = tray.localPosition;
        Quaternion restTurn = rest.localRotation, trayTurn = tray.localRotation;
        var taken = new List<string>();
        try
        {
            foreach (BenchScale.Device d in BenchScale.Devices)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab);
                DeviceDefinition def = prefab != null ? prefab.GetComponent<DeviceDefinition>() : null;
                BenchLayout layout = def != null && def.benchLayout != null && def.benchLayout.IsSet ? def.benchLayout : BenchLayout.ForKind(d.label);
                rest.SetPositionAndRotation(stage.MatToWorld(new Vector2(layout.toolRestCentreX, layout.toolRestCentreY), BenchStage.MatTop), stage.MatRotation);
                tray.SetPositionAndRotation(stage.MatToWorld(layout.scrapTray, BenchStage.MatTop), stage.MatRotation);
                Vector3 at;
                Quaternion turn;
                if (layout.HasFraming)
                {
                    turn = stage.MatRotation * Quaternion.Euler(ItemInspector.Pitch, 0f, 0f);
                    at = stage.MatToWorld(layout.cameraAim, 0f) - turn * Vector3.forward * layout.cameraDistance;
                }
                else
                {
                    Transform cam = stage.transform.Find("CM_InspectCam");
                    if (cam == null) continue;
                    at = cam.position;
                    turn = cam.rotation;
                }
                string name = $"{prefix}-{d.label}-layout-view";
                Shoot(Path.Combine(folder, name + ".png"), at, turn, ItemInspector.LayoutFov);
                taken.Add(name);
            }
        }
        finally
        {
            rest.localPosition = restAt;
            rest.localRotation = restTurn;
            tray.localPosition = trayAt;
            tray.localRotation = trayTurn;
        }
        Vector3 middle = stage.MatToWorld(new Vector2(0f, 0f), BenchStage.MatTop);
        Shoot(Path.Combine(folder, prefix + "-the-mat-from-above.png"), middle + stage.MatUp * .62f, Quaternion.LookRotation(-stage.MatUp, stage.MatAway), 45f);
        taken.Add(prefix + "-the-mat-from-above");
        report.AppendLine($"  info  photos {prefix}: {string.Join(", ", taken)}");
    }

    static void Shoot(string path, Vector3 at, Quaternion turn, float fov)
    {
        var go = new GameObject("Temporary bench photo camera (Bench 7b)") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(at, turn);
            cam.fieldOfView = fov;
            cam.nearClipPlane = .02f;
            cam.farClipPlane = 60f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        catch (Exception e) { Debug.LogWarning(Tag + "A photo failed: " + e.Message); }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (Transform p = t; p != null && names.Count < 5; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
#endif
