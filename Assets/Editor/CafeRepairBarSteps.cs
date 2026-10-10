#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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
// THE REPAIR BAR: THE BENCH MOVES INTO THE FRONT COUNTER, FACING THE ROOM (the café redesign, 10 Oct 2026)
//
// His decision (9 Oct, 23:40): "the repair bar: yes". The bench leaves the back wall, where Ace worked with his back to
// the customers, for the front counter's left third (x -5.80 to -2.60, its top z 13.66 to 14.45 at 1.005 m), behind the
// ledge and the low glass screen, Ace on the staff side facing the room
// (claude/studio/cafe-sf-research-and-redesign.md §6.5; the plan: repair-bar-move-plan.md).
//
//   Fixit Fidget > Cafe redesign > Repair bar 0 - Check the move first (read-only)
//       Finds every piece, checks the bench is where this plan measured it, that its new place on the counter is free and
//       the counter's front is solid from the west wall to the till's end, and writes where everything would land. Changes
//       nothing.
//   Fixit Fidget > Cafe redesign > Repair bar 1 - Move the repair bench into the counter (scene)
//       One rigid motion, a half turn about world up and a shift: the root "03 - repair work area" goes from
//       (-3.30, 0.50, 17.45) facing north to (-4.20, 0.50, 14.10) facing south, and with it the rubber mat Ace stands on and
//       the clamp lamp. Nothing inside the work area moves relative to the rest of it: the mat, the tray, the caddy and its
//       tools, the cloth, the inspect point, the close-up camera, the bench's slots, its stand point and its camera keep
//       every distance the bench labs were tuned on (Bench 3, 128/128). The mat lands at z 13.82 to 14.18 (17 cm behind
//       the glass), 1.000 to 1.006 m as now.
//       The Workbench's solid box (the old bench's only collision) is refitted to the new bench's footprint, its top at
//       1.006 m (the mat's top), so the crosshair finds the bench before any kit collider at 1.005 m. Switched off, never
//       deleted: the old bench's top and its two pedestals (they would stand there with nothing solid in them), the
//       empty tool stand and the decor tray that stood on it (they would float), the clamp lamp too when the kit's
//       magnifier lamp is in, and anything else left floating over the old top. Where the kit's bench (CK_RepairBench)
//       isn't in yet, a plain stand-in block (no collider) holds the stage at 1.005 m, recorded and removable.
//       The guards before and after: the layout check and the occupied-circulation check (rolled back if either gets
//       worse), the routes re-baked; the bench photographed before and after (the close-up, where Ace stands, from a
//       stool, the old place, the game's camera) and the room (CafeFurnishing.Photograph). Not saved: look at the
//       photos and the report, then save the scene.
//   Fixit Fidget > Cafe redesign > Repair bar 1 - Undo: the bench back to the back counter (scene)
//       From the record (CafeRepairBar, on the café's root): every moved piece back to its local pose, every piece
//       switched back on, the solid box as it was, the stand-in gone, the routes re-baked.
//   Fixit Fidget > Cafe redesign > Repair bar 2 - Take the stand-in bench away, the kit's bench is in (scene)
//
// Why one rigid motion and not a re-laid stage: the bench is the most tuned thing in the game (the close-up's 58° and
// 0.58 m, the slots on the mat, the caddy and the cloth); a half turn and a shift change no distance and no angle
// between its pieces, and every piece of bench code works in its own or the camera's axes (checked: ItemInspector,
// BenchStage, ToolPickup, Screw, RemovablePart, ReplaceablePart, the labs). The one cost: the bench's stand point is
// 1.30 m from the mat's middle, so with the mat on the new top it lands at z 15.30, not the plan's 14.95. It is a lab's
// marker (StationsCheck, BenchLab and AimHelpCheck stand Ace there; no play path uses it since stations became reach);
// walking up to the bench, Ace stops at about 14.80, his body against the bench.
//
// Report and photos: Logs/CafeRedesign/repair-bar-<time>/; the last word: Logs/CafeRedesign/repair-bar-last.txt.
// ---------------------------------------------------------------------------
internal static class CafeRepairBarSteps
{
    const string Tag = "[Repair bar] ";
    const string Menu = "Fixit Fidget/Cafe redesign/";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    const string UndoName = "Move the repair bench into the counter";

    // ---- the motion: the root's start (checked, never assumed), its end, the half turn ----
    static readonly Vector3 RootWas = new Vector3(-3.30f, .50f, 17.45f);
    static readonly Vector3 RootTo = new Vector3(-4.20f, .50f, 14.10f);
    static readonly Quaternion Turn = Quaternion.AngleAxis(180f, Vector3.up);

    // ---- the new bench (scene-facts, brief A): its footprint and top; the ledge and the glass in front of it ----
    const float BenchX0 = -5.80f, BenchX1 = -2.60f, BenchZ0 = 13.66f, BenchZ1 = 14.45f, BenchTop = 1.005f;
    // Where the mat must lie, wholly (5 cm inside the bench's ends, clear of the glass, short of Ace's edge).
    const float MatX0 = -5.75f, MatX1 = -2.65f, MatZ0 = 13.70f, MatZ1 = 14.40f;
    const float GlassZ = 13.65f;            // the low glass screen, on the ledge's back edge (1.095 to 1.395 m)
    const float LedgeZ0 = 13.30f;           // the counter's front, where the ledge starts
    // The station's solid, refitted: the bench's footprint, its top at the mat's top (1.006). A kit collider on the
    // bench's top (1.005) then sits a millimetre under it, and the first-person crosshair, which stops at the first solid
    // that isn't something to use, still reaches the bench (PlayerInteractor.FindFirstPersonFloorTarget).
    const float SolidTop = 1.006f;
    // A bench top under the stage is a support up to here; a solid reaching higher where the stage goes is in the way.
    const float SupportMax = 1.010f;

    // ---- the pieces, by their paths under the café's root ----
    const string WorkAreaPath = "03 - repair work area";
    const string BenchMatPath = "18 - cafe furnishing/F - Work areas/Repair bench mat";
    const string ClampLampPath = "23 - real cafe pass/Bench lamp and tool stand/Clamp lamp";
    const string ToolStandPath = "23 - real cafe pass/Bench lamp and tool stand/Tool stand";
    const string DecorTrayPath = "12 - authored cafe interior/Cafe service details/RepairTray";
    const string ShellGroupPath = "Counter extensions and cabinetry";
    const string ShellTopName = "Repair tabletop";
    const string ShellPedestalName = "Repair cabinet";
    const string KitBenchPrefix = "CK_RepairBench";
    const string KitLampPrefix = "CK_BenchLamp";
    const string StandInName = "Repair bar - stand-in bench (temporary: Repair bar 2 takes it away when CK_RepairBench is in)";
    const float BackWallZ = 18.0f;

    // ================================================================== what the move works on

    sealed class Survey
    {
        public Scene scene;
        public Transform cafe, work, workbench, rig, standPoint, inspectPoint, inspectCam, benchCam, mat, player;
        public StationInteractable station;
        public BenchStage stage;
        public BoxCollider solid, trigger;
        public Vector3 pivot;                                                      // the root's live position: the turn's centre
        public readonly List<(Transform t, string what)> ride = new List<(Transform, string)>();
        public readonly List<(GameObject go, string why)> off = new List<(GameObject, string)>();
        public GameObject kitBench, kitLamp;
        public Material standInMaterial;
        public Bounds oldTop;                                                      // the old bench top, for what would float
        public readonly List<string> notes = new List<string>();

        public Vector3 Map(Vector3 p) => RootTo + Turn * (p - pivot);
        public Quaternion Map(Quaternion q) => Turn * q;
        // A half turn about up maps a box lying square to the world onto a box of the same size.
        public Bounds Map(Bounds b) => new Bounds(Map(b.center), b.size);
    }

    // Everything is found and checked before anything changes; any doubt throws with the reason, and nothing has changed.
    static Survey Look(StringBuilder report)
    {
        CityPackChecks.RequireScene();
        var s = new Survey { scene = SceneManager.GetActiveScene() };
        GameObject cafeGo = s.scene.GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
        if (cafeGo == null) throw new InvalidOperationException($"'{CafeRootName}' isn't in the open scene.");
        s.cafe = cafeGo.transform;
        GameObject playerGo = s.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        s.player = playerGo != null ? playerGo.transform : null;
        Physics.SyncTransforms();

        // ---- the work area, its station, its rig ----
        s.work = s.cafe.Find(WorkAreaPath) ?? throw new InvalidOperationException($"{WorkAreaPath} isn't under the café's root.");
        s.workbench = s.work.Find("Workbench") ?? throw new InvalidOperationException($"{WorkAreaPath}/Workbench is missing.");
        s.station = s.workbench.GetComponent<StationInteractable>();
        if (s.station == null || !s.station.IsWorkSurface) throw new InvalidOperationException("The Workbench has no work-surface StationInteractable.");
        BenchRig rig = s.workbench.GetComponentInChildren<BenchRig>(true) ?? throw new InvalidOperationException("No BenchRig (the InspectRig) under the Workbench.");
        s.rig = rig.transform;
        s.stage = rig.GetComponent<BenchStage>();
        if (s.stage == null || s.stage.Mat == null || s.stage.Tray == null || s.stage.Caddy == null)
            throw new InvalidOperationException("No bench stage on the rig (Fixit Fidget > Bench > Bench 2 builds it).");
        s.mat = s.stage.Mat;
        s.inspectPoint = s.rig.Find("InspectPoint");
        s.inspectCam = s.rig.Find("CM_InspectCam");
        s.standPoint = s.station.StandPoint;
        s.benchCam = s.work.Find("CM_BenchCam");
        if (s.inspectPoint == null || s.inspectCam == null || s.standPoint == null)
            throw new InvalidOperationException("The rig's InspectPoint or CM_InspectCam, or the bench's stand point, is missing.");
        foreach (BoxCollider box in s.workbench.GetComponents<BoxCollider>())
        {
            if (!box.isTrigger && s.solid == null) s.solid = box;
            if (box.isTrigger && s.trigger == null) s.trigger = box;
        }
        if (s.solid == null) throw new InvalidOperationException("The Workbench has no solid box (the bench's collision).");

        // Every working piece must ride with the root, or it would be left behind at the back wall.
        foreach (Transform t in new[] { s.mat, s.stage.Tray, s.stage.Caddy, s.inspectPoint, s.inspectCam, s.standPoint })
            if (!t.IsChildOf(s.work)) throw new InvalidOperationException($"{PathOf(t)} isn't under {WorkAreaPath}: it would stay behind.");
        ItemInspector inspector = Object.FindAnyObjectByType<ItemInspector>(FindObjectsInactive.Include);
        if (inspector != null)
        {
            var so = new SerializedObject(inspector);
            Object point = so.FindProperty("inspectPoint") != null ? so.FindProperty("inspectPoint").objectReferenceValue : null;
            Object cam = so.FindProperty("inspectCam") != null ? so.FindProperty("inspectCam").objectReferenceValue : null;
            Transform pointT = point as Transform;
            Transform camT = cam is Component c ? c.transform : null;
            if (pointT == null || camT == null || !pointT.IsChildOf(s.work) || !camT.IsChildOf(s.work))
                throw new InvalidOperationException("Ace's ItemInspector looks at an inspect point or a close-up camera outside the work area: the close-up would stay behind.");
        }
        else s.notes.Add("No ItemInspector found (Ace's): the close-up's wiring wasn't checked.");

        // ---- the start: where this plan measured the bench (scene at 216cc1a). A bench moved since needs a new plan. ----
        s.pivot = s.work.position;
        float turned = Quaternion.Angle(s.work.rotation, Quaternion.identity);
        if (Vector3.Distance(s.pivot, RootWas) > .05f || turned > .5f || (s.work.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
            throw new InvalidOperationException($"The work area stands at {V(s.pivot)}, turned {turned:0.0}°, scale {V(s.work.lossyScale)}; the plan was measured " +
                                                $"with it at {V(RootWas)}, facing north, unscaled. Nothing was changed: re-plan from the scene as it is.");
        if (Vector3.Distance(s.pivot, RootWas) > .005f)
            s.notes.Add($"The work area's root is {Vector3.Distance(s.pivot, RootWas) * 100f:0.0} cm from the plan's start: everything lands as far from the plan's numbers (checked below).");

        // ---- what rides along ----
        s.ride.Add((s.work, "the repair work area: the station, the rig and its stage, both cameras, the slots, the stand point"));
        Transform benchMat = s.cafe.Find(BenchMatPath);
        if (benchMat != null) s.ride.Add((benchMat, "the rubber mat Ace stands on at the bench (it follows the stand point, as in the real-café pass)"));
        else s.notes.Add($"No {BenchMatPath}: nothing to carry there.");
        Transform clamp = s.cafe.Find(ClampLampPath);
        if (clamp != null) s.ride.Add((clamp, "the clamp lamp and its light (the bench's task light, from the real-café pass)"));
        else s.notes.Add($"No {ClampLampPath}: nothing to carry there.");
        foreach (var (t, _) in s.ride)
            foreach (var (u, _) in s.ride)
                if (t != u && t.IsChildOf(u)) throw new InvalidOperationException($"{PathOf(t)} is inside {PathOf(u)}: it would be moved twice.");

        // ---- the kit, if it is in ----
        s.kitBench = ActiveNamed(s.scene, KitBenchPrefix);
        s.kitLamp = ActiveNamed(s.scene, KitLampPrefix);

        // ---- what goes off ----
        Transform shell = s.cafe.Find(ShellGroupPath);
        Transform top = shell != null ? shell.Find(ShellTopName) : null;
        Renderer topR = top != null ? top.GetComponent<Renderer>() : null;
        s.oldTop = topR != null && top.gameObject.activeInHierarchy ? topR.bounds : s.solid.bounds;
        s.standInMaterial = topR != null ? topR.sharedMaterial : null;
        if (s.standInMaterial == null) s.standInMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/CC_Wood_Counter.mat");
        if (shell != null)
            foreach (Transform piece in shell.Cast<Transform>().Where(x => (x.name == ShellTopName || x.name == ShellPedestalName) && x.gameObject.activeSelf))
                s.off.Add((piece.gameObject, "the old bench's shell: its only collision was the Workbench's box, which leaves with the bench, so it would stand there with nothing solid in it"));
        Transform toolStand = s.cafe.Find(ToolStandPath);
        if (toolStand != null && toolStand.gameObject.activeSelf)
            s.off.Add((toolStand.gameObject, "the oak tool stand on the old bench: empty since Bench 2 put the tools in the caddy, and with the bench gone it would float"));
        Transform decorTray = s.cafe.Find(DecorTrayPath);
        if (decorTray != null && decorTray.gameObject.activeSelf)
            s.off.Add((decorTray.gameObject, "the decor tray that stood on the old bench: with the bench gone it would float (the stage's magnetic tray is the bench's tray)"));
        if (clamp != null && s.kitLamp != null && clamp.gameObject.activeSelf)
            s.off.Add((clamp.gameObject, $"the clamp lamp: the kit's magnifier lamp ({s.kitLamp.name}) replaces it on the new bench (moved with the bench all the same, so switching it back on puts it there)"));

        // ---- the new place: free, and the counter's front solid ----
        List<string> blockers = Blockers(s);
        List<float> gaps = CounterGaps(s);
        if (blockers.Count > 0 || gaps.Count > 0)
        {
            var why = new StringBuilder("The bench's new place isn't ready, so nothing was changed.");
            if (blockers.Count > 0)
                why.Append($"\nWhere the bench goes (x {BenchX0:0.00} to {BenchX1:0.00}, z {BenchZ0:0.00} to {BenchZ1:0.00}) something stands above the bench's top: " +
                           string.Join("; ", blockers) + ".\nPlace the café kit's front counter first (CK_FrontCounter with its repair-bar opening; its step switches the old counter sections off), or switch these off.");
            if (gaps.Count > 0)
                why.Append($"\nThe counter's front has no solid at x {string.Join(", ", gaps.Select(x => x.ToString("0.00", CultureInfo.InvariantCulture)))} (z {LedgeZ0:0.00} to {GlassZ:0.00}): " +
                           "customers could walk behind it. The bench may only move into a closed counter (give the kit's counter its collider).");
            throw new InvalidOperationException(why.ToString());
        }
        return s;
    }

    // Things standing where the bench goes, above its top: solids by rays straight down (a bench top at up to 1.010 m is a
    // support, not a blocker), and small things without a collider by their bounds. The kit's bench and its lamp are made
    // around the stage; anything of theirs reaching above the top counts as a solid like any other, but for the lamp,
    // which reaches over the mat by design.
    static List<string> Blockers(Survey s)
    {
        var found = new List<string>();
        for (int ix = 0; ix <= 20; ix++)
        for (int iz = 0; iz <= 4; iz++)
        {
            float x = Mathf.Lerp(BenchX0 + .05f, BenchX1 - .05f, ix / 20f);
            float z = Mathf.Lerp(MatZ0 + .02f, BenchZ1 - .05f, iz / 4f);
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 1.9f, z), Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = h.collider.transform;
                if (t.IsChildOf(s.work) || IsPlayer(s, t) || Under(t, KitLampPrefix)) continue;
                if (h.point.y <= SupportMax) continue;
                string what = $"{PathOf(t)} (solid to {h.point.y:0.000} m)";
                if (!found.Contains(what)) found.Add(what);
            }
        }
        var space = new Bounds(new Vector3((BenchX0 + BenchX1) * .5f, .71f, (BenchZ0 + BenchZ1) * .5f + .01f),
                               new Vector3(BenchX1 - BenchX0 - .04f, 1.38f, BenchZ1 - BenchZ0 - .04f));
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || r.gameObject.scene != s.scene) continue;
            if (r.transform.IsChildOf(s.work) || IsPlayer(s, r.transform) || Under(r.transform, KitBenchPrefix) || Under(r.transform, KitLampPrefix)) continue;
            Bounds b = r.bounds;
            if (b.size.x > 1.2f || b.size.z > 1.2f || !b.Intersects(space)) continue;      // big: structure, judged by its collider
            string what = $"{PathOf(r.transform)} (a piece at {b.min.y:0.00} to {b.max.y:0.00} m)";
            if (!found.Contains(what)) found.Add(what);
        }
        return found;
    }

    // The counter's front, from the west wall to the till's end: a solid under the ledge every 25 cm, or customers could
    // walk behind the counter (the guards would not see it: they count what can be reached, and an open counter reaches more).
    static List<float> CounterGaps(Survey s)
    {
        var gaps = new List<float>();
        for (int i = 0; i <= 48; i++)
        {
            float x = -7.30f + i * .25f;
            Collider[] hits = Physics.OverlapBox(new Vector3(x, .55f, (LedgeZ0 + GlassZ) * .5f), new Vector3(.05f, .05f, (GlassZ - LedgeZ0) * .5f - .005f),
                                                 Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (!hits.Any(c => c != null && !IsPlayer(s, c.transform) && c.GetComponentInParent<CustomerBrain>() == null)) gaps.Add(x);
        }
        return gaps;
    }

    // ================================================================== 0: the check (read-only)

    [MenuItem(Menu + "Repair bar 0 - Check the move first (read-only)")]
    static void CheckTheMove()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            CityPackChecks.RequireScene();
            GameObject cafeGo = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
            if (cafeGo != null && cafeGo.GetComponent<CafeRepairBar>() != null)
            {
                Debug.Log(Tag + "The bench is already at the repair bar (Repair bar 1 - Undo puts it back).");
                Keep(null, "The bench is already at the repair bar (Repair bar 1 - Undo puts it back).");
                return;
            }
            Survey s = Look(report);
            folder = Folder("repair-bar-check");
            Landing(s, report);
            report.AppendLine("Nothing in the scene changed. Run Repair bar 1 to move it.");
            Debug.Log(Tag + "The repair bar move, checked:\n" + report);
            Keep(folder, "The repair bar move, checked:\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The check FAILED: " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "The check FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // Where everything would land, and the checks on it, from the scene as it is (the plan's table, live).
    static void Landing(Survey s, StringBuilder report)
    {
        report.AppendLine($"The motion: a half turn about world up and a shift, the work area's root from {V(s.pivot)} facing north to {V(RootTo)} facing south.");
        report.AppendLine($"The kit: {(s.kitBench != null ? $"its bench is in ({PathOf(s.kitBench.transform)}): no stand-in" : "its bench (CK_RepairBench) isn't in: a stand-in block will hold the stage at 1.005 m")}; " +
                          $"{(s.kitLamp != null ? $"its lamp is in ({PathOf(s.kitLamp.transform)})" : "its lamp isn't in")}.");
        foreach (string n in s.notes) report.AppendLine("Note: " + n);
        report.AppendLine("Carried (world position and facing, before -> after):");
        foreach (var (t, what) in s.ride)
            report.AppendLine($"  {PathOf(t)}: {V(t.position)} facing {Facing(t.rotation):0} -> {V(s.Map(t.position))} facing {Facing(s.Map(t.rotation)):0}  ({what})");
        report.AppendLine("Inside the work area (they ride with its root):");
        foreach (Transform part in new[] { s.mat, s.stage.Tray, s.stage.Caddy, s.inspectPoint, s.inspectCam, s.standPoint, s.benchCam })
            if (part != null) report.AppendLine($"  {part.name}: {V(part.position)} -> {V(s.Map(part.position))}, facing {Facing(s.Map(part.rotation)):0}");
        foreach (Transform slot in s.workbench.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Slot_", StringComparison.Ordinal)))
            report.AppendLine($"  bench slot {slot.name}: {V(slot.position)} -> {V(s.Map(slot.position))}");

        // The mat and the stage, on the bench top, clear of the glass.
        Renderer matR = s.mat.GetComponent<Renderer>();
        Bounds mat = s.Map(matR != null ? matR.bounds : new Bounds(s.mat.position, new Vector3(.6f, .006f, .36f)));
        bool matIn = mat.min.x >= MatX0 && mat.max.x <= MatX1 && mat.min.z >= MatZ0 && mat.max.z <= MatZ1;
        report.AppendLine($"{(matIn ? "PASS" : "FAIL")} the mat lies wholly in x {MatX0:0.00}..{MatX1:0.00}, z {MatZ0:0.00}..{MatZ1:0.00}: it lands at x {mat.min.x:0.000}..{mat.max.x:0.000}, " +
                          $"z {mat.min.z:0.000}..{mat.max.z:0.000}, {mat.min.y:0.000} to {mat.max.y:0.000} m ({mat.min.z - GlassZ:0.000} m behind the glass).");
        Bounds stage = StageBounds(s, true);
        bool stageIn = stage.min.x >= BenchX0 && stage.max.x <= BenchX1 && stage.min.z >= GlassZ + .02f && stage.max.z <= BenchZ1;
        report.AppendLine($"{(stageIn ? "PASS" : "FAIL")} the whole rig (stage, tools, cloth) lands on the bench top: x {stage.min.x:0.000}..{stage.max.x:0.000}, z {stage.min.z:0.000}..{stage.max.z:0.000}, up to {stage.max.y:0.000} m.");
        Transform clamp = s.ride.Select(r => r.t).FirstOrDefault(x => x.name == "Clamp lamp");
        if (clamp != null && clamp.gameObject.activeInHierarchy && !s.off.Any(o => o.go == clamp.gameObject))
        {
            Bounds lamp = s.Map(RendererBounds(clamp, false));
            bool lampIn = lamp.min.x >= BenchX0 && lamp.max.x <= BenchX1 && lamp.min.z >= GlassZ + .005f && lamp.max.z <= BenchZ1;
            report.AppendLine($"{(lampIn ? "PASS" : "FAIL")} the clamp lamp lands on the bench top: x {lamp.min.x:0.000}..{lamp.max.x:0.000}, z {lamp.min.z:0.000}..{lamp.max.z:0.000} ({lamp.min.z - GlassZ:0.000} m behind the glass).");
        }
        Vector3 stand = s.Map(s.standPoint.position);
        report.AppendLine($"The stand point: {V(stand)} facing {Facing(s.Map(s.standPoint.rotation)):0} ({stand.z - BenchZ1:0.00} m behind the bench's back edge; the plan's 14.95 can't be met with the mat on the top: " +
                          $"the stand point is {Flat(s.standPoint.position - s.inspectPoint.position).magnitude:0.000} m from the inspect point).");
        report.AppendLine($"The Workbench's solid box: now {BoundsText(s.solid.bounds)}; moved and refitted: x {BenchX0:0.00}..{BenchX1:0.00}, y 0.000..{SolidTop:0.000}, z {BenchZ0:0.00}..{BenchZ1:0.00}.");
        if (s.trigger != null) report.AppendLine($"Its reach trigger, moved: {BoundsText(s.Map(s.trigger.bounds))}.");
        report.AppendLine("Switched off:");
        foreach (var (go, why) in s.off) report.AppendLine($"  {PathOf(go.transform)}: {why}");
        report.AppendLine($"The old bench top, for what would float on it: {BoundsText(s.oldTop)}.");
    }

    // ================================================================== 1: the move

    [MenuItem(Menu + "Repair bar 1 - Move the repair bench into the counter (scene)")]
    static void MoveTheBench()
    {
        var report = new StringBuilder();
        string folder = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            GameObject cafeGo = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
            if (cafeGo != null && cafeGo.GetComponent<CafeRepairBar>() != null)
            {
                Debug.Log(Tag + "The bench is already at the repair bar (Repair bar 1 - Undo puts it back).");
                Keep(null, "The bench is already at the repair bar (Repair bar 1 - Undo puts it back).");
                return;
            }
            Survey s = Look(report);
            folder = Folder("repair-bar");
            Landing(s, report);

            string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
            string circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
            string roomBefore = CafeFurnishing.Photograph("repair-bar-before");
            Photos(folder, "1-before", s);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            string layoutAfter, circulationAfter;
            var after = new StringBuilder();
            try
            {
                CafeRepairBar record = Undo.AddComponent<CafeRepairBar>(s.cafe.gameObject);
                record.rootWasAt = s.pivot;
                record.rootMovedTo = RootTo;
                record.turnDegrees = 180f;

                // 1. The rigid motion: each carried root by the same half turn and shift (its local pose kept for the undo).
                record.moved = s.ride.Select(r => r.t).ToArray();
                record.movedFromLocalPosition = record.moved.Select(t => t.localPosition).ToArray();
                record.movedFromLocalRotation = record.moved.Select(t => t.localRotation).ToArray();
                foreach (Transform t in record.moved)
                {
                    Vector3 to = s.Map(t.position);
                    Quaternion facing = s.Map(t.rotation);
                    Undo.RecordObject(t, UndoName);
                    t.SetPositionAndRotation(to, facing);
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
                Physics.SyncTransforms();

                // 2. The station's solid, refitted to the new bench (its local frame is the turned, stretched Workbench).
                if (Quaternion.Angle(s.workbench.rotation, Turn) > .5f)
                    throw new InvalidOperationException($"The Workbench faces {Facing(s.workbench.rotation):0.0} after the turn, not 180: its box can't be refitted square.");
                record.refitted = s.solid;
                record.refittedFromCenter = s.solid.center;
                record.refittedFromSize = s.solid.size;
                Vector3 scale = s.workbench.lossyScale;
                Undo.RecordObject(s.solid, UndoName);
                s.solid.center = s.workbench.InverseTransformPoint(new Vector3((BenchX0 + BenchX1) * .5f, SolidTop * .5f, (BenchZ0 + BenchZ1) * .5f));
                s.solid.size = new Vector3((BenchX1 - BenchX0) / Mathf.Abs(scale.x), SolidTop / Mathf.Abs(scale.y), (BenchZ1 - BenchZ0) / Mathf.Abs(scale.z));

                // 3. Off, never deleted: the old shell and what stood on it; then anything else left floating over the old top.
                var off = new List<(GameObject go, string why)>(s.off);
                foreach (var (go, _) in off)
                {
                    Undo.RecordObject(go, UndoName);
                    go.SetActive(false);
                }
                Physics.SyncTransforms();
                foreach (GameObject go in LeftFloating(s))
                {
                    if (off.Any(o => o.go == go)) continue;
                    Undo.RecordObject(go, UndoName);
                    go.SetActive(false);
                    off.Add((go, "stood on the old bench top: with the bench gone it would float"));
                }
                record.switchedOff = off.Select(o => o.go).ToArray();
                record.why = off.Select(o => o.why).ToArray();

                // 4. Where the kit's bench isn't in: a plain block under the stage, at the bench's height, no collider (the
                //    station's refitted box is the bench's solid), named for what it is.
                if (s.kitBench == null)
                {
                    GameObject standIn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(standIn.GetComponent<Collider>());
                    standIn.name = StandInName;
                    standIn.transform.SetParent(s.work, false);
                    standIn.transform.SetPositionAndRotation(new Vector3((BenchX0 + BenchX1) * .5f, BenchTop * .5f, (BenchZ0 + BenchZ1) * .5f), s.work.rotation);
                    standIn.transform.localScale = new Vector3(BenchX1 - BenchX0, BenchTop, BenchZ1 - BenchZ0);   // the root is unscaled (checked)
                    if (s.standInMaterial != null) standIn.GetComponent<MeshRenderer>().sharedMaterial = s.standInMaterial;
                    Undo.RegisterCreatedObjectUndo(standIn, UndoName);
                    record.standIn = standIn;
                }
                EditorUtility.SetDirty(record);
                Physics.SyncTransforms();

                // 5. The bench as the labs will find it, in numbers; any failure puts everything back.
                SelfCheck(s, after);

                AcesCafeLayoutSetup.BakeRoutes();
                layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
                circulationAfter = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
                // Not worse: as many paths and served places as before, and a PASS kept if there was one (CafeTidySteps' rule).
                bool layoutOk = Reached(layoutAfter) >= Reached(layoutBefore) && (layoutAfter.Contains("PASS") || !layoutBefore.Contains("PASS"));
                bool circulationOk = Reached(circulationAfter) >= Reached(circulationBefore) && (circulationAfter.Contains("PASS") || !circulationBefore.Contains("PASS"));
                if (!layoutOk || !circulationOk)
                    throw new InvalidOperationException("The move made a customer route or a delivery approach worse, so nothing was kept.\nBefore: " + layoutBefore +
                                                        "\n        " + circulationBefore + "\nAfter:  " + layoutAfter + "\n        " + circulationAfter);
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Physics.SyncTransforms();
                AcesCafeLayoutSetup.BakeRoutes();
                CutawayWall.ForgetCandidates();
                throw;
            }
            CutawayWall.ForgetCandidates();      // the back wall's fading decor is looked for afresh: the lamp left it
            EditorSceneManager.MarkSceneDirty(s.cafe.gameObject.scene);
            Photos(folder, "2-after", s);
            string roomAfter = CafeFurnishing.Photograph("repair-bar-after");

            CafeRepairBar done = s.cafe.GetComponent<CafeRepairBar>();
            report.AppendLine();
            report.AppendLine($"MOVED. The work area's root at {V(s.work.position)} facing {Facing(s.work.rotation):0}; {done.moved.Length} piece(s) carried, " +
                              $"{done.switchedOff.Length} switched off, the Workbench's solid refitted to {BoundsText(s.solid.bounds)}" +
                              (done.standIn != null ? "; a stand-in block holds the stage until the kit's bench is in." : "; the kit's bench holds the stage."));
            for (int i = 0; i < done.switchedOff.Length; i++) report.AppendLine($"  off: {PathOf(done.switchedOff[i].transform)} ({done.why[i]})");
            report.Append(after.ToString());
            report.AppendLine("Layout check before: " + layoutBefore);
            report.AppendLine("Layout check after:  " + layoutAfter);
            report.AppendLine("Circulation before:  " + circulationBefore);
            report.AppendLine("Circulation after:   " + circulationAfter);
            report.AppendLine($"Photos: {folder} (1-before-*, 2-after-*: the close-up, where Ace stands, from a stool, the old place, the game's camera); the room {roomBefore} and {roomAfter}.");
            report.Append("The routes are re-baked. Look at the photos, then save the scene (Ctrl+S). Next: the labs, in the plan's order (repair-bar-move-plan.md §8).");
            Debug.Log(Tag + "The bench is at the repair bar.\n" + report);
            Keep(folder, "The bench is at the repair bar.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Moving the bench FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
            Keep(folder, "Moving the bench FAILED (nothing half-done was kept): " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // After the move: the numbers the labs depend on. Throws on the first that is wrong (the caller puts everything back).
    static void SelfCheck(Survey s, StringBuilder after)
    {
        var fails = new List<string>();
        void Check(bool ok, string text) { after.AppendLine((ok ? "  PASS  " : "  FAIL  ") + text); if (!ok) fails.Add(text); }
        after.AppendLine("Checked after the move:");

        // The mat, on the top, where the brief wants it, its height unchanged.
        Renderer matR = s.mat.GetComponent<Renderer>();
        Bounds mat = matR != null ? matR.bounds : new Bounds(s.mat.position, new Vector3(.6f, .006f, .36f));
        Check(mat.min.x >= MatX0 && mat.max.x <= MatX1 && mat.min.z >= MatZ0 && mat.max.z <= MatZ1 && Mathf.Abs(mat.max.y - 1.006f) < .003f,
              $"the mat lies on the new top at x {mat.min.x:0.000}..{mat.max.x:0.000}, z {mat.min.z:0.000}..{mat.max.z:0.000}, its top at {mat.max.y:0.000} m");
        Bounds stage = StageBounds(s, false);
        Check(stage.min.x >= BenchX0 && stage.max.x <= BenchX1 && stage.min.z >= GlassZ + .02f && stage.max.z <= BenchZ1,
              $"the rig's pieces (stage, tools, cloth) lie on the bench top, {stage.min.z - GlassZ:0.000} m behind the glass at the nearest");

        // What the stage stands on (BenchLab's own way: a ray down through the mat, past the stage's pieces).
        float support = float.NegativeInfinity;
        string supportBy = "nothing";
        foreach (RaycastHit h in Physics.RaycastAll(s.mat.position + Vector3.up * .3f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(s.stage.transform) || h.collider.GetComponentInParent<JobBase>() != null) continue;
            if (h.point.y > support) { support = h.point.y; supportBy = PathOf(h.collider.transform); }
        }
        Check(support > .995f && support <= SupportMax,
              $"under the stage a bench top at {support:0.000} m ({supportBy}); the mat's underside at {mat.min.y:0.000} (Bench 3's \"the stage sits on the bench top\")");

        // The close-up keeps its pose on the mat.
        Vector3 rel = s.inspectCam.position - s.inspectPoint.position;
        float pitch = Vector3.Angle(Vector3.ProjectOnPlane(s.inspectCam.forward, Vector3.up), s.inspectCam.forward);
        Check(Mathf.Abs(rel.magnitude - .58f) < .005f && Mathf.Abs(pitch - 58f) < .5f && Vector3.Dot(Flat(rel), Vector3.forward) > 0f,
              $"the close-up camera is {rel.magnitude:0.000} m from the inspect point, {pitch:0.0}° down, on Ace's side (z {s.inspectCam.position.z:0.000}), looking toward the room");
        CinemachineCamera close = s.inspectCam.GetComponent<CinemachineCamera>();
        if (close != null)
        {
            // Its frame's far edge on the bench top (the default zoom): short of the bench's glass-side edge, so the room never shows.
            float fov = close.Lens.FieldOfView;
            Vector3 topRay = Quaternion.AngleAxis(-fov * .5f, s.inspectCam.right) * s.inspectCam.forward;
            float reachZ = topRay.y < -1e-4f ? s.inspectCam.position.z + topRay.z * ((BenchTop - s.inspectCam.position.y) / topRay.y) : float.NegativeInfinity;
            after.AppendLine($"  info  the close-up's frame (field {fov:0}°) reaches the bench top at z {reachZ:0.000} at its far edge (the bench's glass-side edge at {BenchZ0:0.00})");
        }

        // The stand point: south-facing, off the bench, on the walkable floor's side (the layout guard checks the path).
        float standYaw = Facing(s.standPoint.rotation);
        Check(Mathf.Abs(Mathf.DeltaAngle(standYaw, 180f)) < .5f && s.standPoint.position.z > BenchZ1 + .5f,
              $"the bench's stand point at {V(s.standPoint.position)} faces {standYaw:0} (the room), {s.standPoint.position.z - BenchZ1:0.00} m behind the bench's back edge");

        // First person from the stand point, as StationsCheck aims (at the top of the station's first collider): the first
        // thing the crosshair meets must be the bench (or a trigger before it), never another solid.
        CafeViewMode view = Object.FindAnyObjectByType<CafeViewMode>(FindObjectsInactive.Include);
        Vector3 eye = new Vector3(s.standPoint.position.x, s.standPoint.position.y + (view != null ? view.eyeHeight : 1.65f), s.standPoint.position.z);
        Collider first = s.station.GetComponent<Collider>();
        Vector3 aim = first != null ? new Vector3(first.bounds.center.x, first.bounds.max.y - .01f, first.bounds.center.z) : s.workbench.position;
        Vector3 d = aim - eye;
        string met = "nothing";
        bool bench = false;
        foreach (RaycastHit h in Physics.RaycastAll(eye, d.normalized, d.magnitude + .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide).OrderBy(x => x.distance))
        {
            if (IsPlayer(s, h.collider.transform)) continue;
            Interactable it = h.collider.GetComponentInParent<Interactable>();
            if (it == s.station) { bench = true; met = PathOf(h.collider.transform); break; }
            if (!h.collider.isTrigger) { met = PathOf(h.collider.transform) + " (a solid that isn't the bench)"; break; }
        }
        Check(bench && d.magnitude <= 2.2f, $"in first person from the stand point the crosshair meets the bench first ({met}, {d.magnitude:0.00} m; reach 2.2)");
        foreach (Transform slot in s.workbench.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Slot_", StringComparison.Ordinal)))
            after.AppendLine($"  info  bench slot {slot.name} at {V(slot.position)}, {Vector3.Distance(eye, slot.position):0.00} m from the eye");

        // The counter's front is still closed, and nothing floats over the old top.
        List<float> gaps = CounterGaps(s);
        Check(gaps.Count == 0, gaps.Count == 0 ? "the counter's front is solid from the west wall to the till's end" : "the counter's front is OPEN at x " + string.Join(", ", gaps.Select(x => x.ToString("0.00", CultureInfo.InvariantCulture))));
        List<GameObject> floating = LeftFloating(s);
        Check(floating.Count == 0, floating.Count == 0 ? "nothing floats over the old bench top" : "floating over the old top: " + string.Join(", ", floating.Select(g => PathOf(g.transform))));

        if (fails.Count > 0) throw new InvalidOperationException("After the move: " + string.Join("; ", fails) + "\n" + after);
    }

    // The rig's renderers (the stage, the tools in the caddy, the cloth), as they stand or, before the move, as they would land.
    static Bounds StageBounds(Survey s, bool mapped)
    {
        Bounds b = RendererBounds(s.rig, false);
        return mapped ? s.Map(b) : b;
    }

    // Active renderers over the old bench top, standing on it (bottom within 3 cm under to 15 cm over its top), that don't
    // hang on the back wall and don't ride with the bench: with the shell off they would float.
    static List<GameObject> LeftFloating(Survey s)
    {
        var found = new List<GameObject>();
        Bounds top = s.oldTop;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || r.gameObject.scene != s.scene) continue;
            if (IsPlayer(s, r.transform) || s.ride.Any(x => r.transform.IsChildOf(x.t))) continue;
            if (s.off.Any(o => r.transform.IsChildOf(o.go.transform))) continue;
            Bounds b = r.bounds;
            if (b.max.x < top.min.x + .01f || b.min.x > top.max.x - .01f || b.max.z < top.min.z + .01f || b.min.z > top.max.z - .01f) continue;
            if (b.min.y < top.max.y - .03f || b.min.y > top.max.y + .15f) continue;
            bool onWall = b.max.z > BackWallZ - .25f && b.min.y > .6f;          // the real-café pass's audit rule: it hangs on the wall
            if (onWall) continue;
            if (!found.Contains(r.gameObject)) found.Add(r.gameObject);
        }
        return found;
    }

    // ================================================================== 1: undo

    [MenuItem(Menu + "Repair bar 1 - Undo: the bench back to the back counter (scene)")]
    static void PutTheBenchBack()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            GameObject cafeGo = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
            if (cafeGo == null) throw new InvalidOperationException($"'{CafeRootName}' isn't in the open scene.");
            CafeRepairBar record = cafeGo.GetComponent<CafeRepairBar>();
            if (record == null) { Keep(null, "The bench was never moved in this scene (no CafeRepairBar record on the café's root)."); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Put the repair bench back");
            int back = 0, on = 0, missing = 0;
            for (int i = 0; i < record.moved.Length; i++)
            {
                Transform t = record.moved[i];
                if (t == null || i >= record.movedFromLocalPosition.Length || i >= record.movedFromLocalRotation.Length) { missing++; continue; }
                Undo.RecordObject(t, "Put the repair bench back");
                t.localPosition = record.movedFromLocalPosition[i];
                t.localRotation = record.movedFromLocalRotation[i];
                if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                back++;
            }
            foreach (GameObject go in record.switchedOff)
            {
                if (go == null) { missing++; continue; }
                Undo.RecordObject(go, "Put the repair bench back");
                go.SetActive(true);
                on++;
            }
            if (record.refitted != null)
            {
                Undo.RecordObject(record.refitted, "Put the repair bench back");
                record.refitted.center = record.refittedFromCenter;
                record.refitted.size = record.refittedFromSize;
            }
            bool standIn = record.standIn != null;
            if (standIn) Undo.DestroyObjectImmediate(record.standIn);   // made by the move: the one thing it may delete
            Undo.DestroyObjectImmediate(record);
            Undo.CollapseUndoOperations(group);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            CutawayWall.ForgetCandidates();
            EditorSceneManager.MarkSceneDirty(cafeGo.scene);
            string done = $"Put back {back} moved piece(s) where they stood, switched {on} back on, the Workbench's solid box as it was" +
                          (standIn ? ", the stand-in bench gone" : "") + (missing > 0 ? $"; {missing} recorded piece(s) no longer in the scene" : "") +
                          $"; the routes re-baked. {AcesCafeLayoutSetup.ValidateLayout()} Save the scene.";
            Debug.Log(Tag + done);
            Keep(null, done);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting the bench back FAILED: " + e.Message + "\n" + e);
            Keep(null, "Putting the bench back FAILED: " + e.Message + "\n" + e);
        }
    }

    // ================================================================== 2: the stand-in away, once the kit's bench is in

    [MenuItem(Menu + "Repair bar 2 - Take the stand-in bench away, the kit's bench is in (scene)")]
    static void TakeTheStandInAway()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            CityPackChecks.RequireScene();
            Scene scene = SceneManager.GetActiveScene();
            GameObject cafeGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName);
            CafeRepairBar record = cafeGo != null ? cafeGo.GetComponent<CafeRepairBar>() : null;
            if (record == null || record.standIn == null) { Keep(null, "There is no stand-in bench in this scene."); return; }
            GameObject kit = ActiveNamed(scene, KitBenchPrefix);
            if (kit == null) throw new InvalidOperationException("The kit's bench (CK_RepairBench) isn't in the scene: the stand-in is all that holds the stage up.");
            Renderer standR = record.standIn.GetComponent<Renderer>();
            Bounds was = standR != null ? standR.bounds : new Bounds(record.standIn.transform.position, Vector3.zero);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Take the stand-in bench away");
            Undo.RecordObject(record, "Take the stand-in bench away");
            Undo.DestroyObjectImmediate(record.standIn);
            record.standIn = null;
            EditorUtility.SetDirty(record);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            string done = $"The stand-in bench ({BoundsText(was)}) is gone; {PathOf(kit.transform)} holds the stage. Run Bench 3 to see the stage still sits on a bench top. Save the scene.";
            Debug.Log(Tag + done);
            Keep(null, done);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Taking the stand-in away FAILED: " + e.Message + "\n" + e);
            Keep(null, "Taking the stand-in away FAILED: " + e.Message + "\n" + e);
        }
    }

    [MenuItem(Menu + "Repair bar 0 - Check the move first (read-only)", true)]
    [MenuItem(Menu + "Repair bar 1 - Move the repair bench into the counter (scene)", true)]
    [MenuItem(Menu + "Repair bar 1 - Undo: the bench back to the back counter (scene)", true)]
    [MenuItem(Menu + "Repair bar 2 - Take the stand-in bench away, the kit's bench is in (scene)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== photos

    // The bench before and after from the same places relative to it (the close-up, where Ace stands) and from the same
    // places in the room (a stool at the repair bar, the old place on the back counter, the game's camera).
    static void Photos(string folder, string when, Survey s)
    {
        CinemachineCamera close = s.inspectCam.GetComponent<CinemachineCamera>();
        Snap(Path.Combine(folder, when + "-1-the-close-up.png"), s.inspectCam.position, s.inspectCam.position + s.inspectCam.forward, close != null ? close.Lens.FieldOfView : 40f, false);
        Vector3 eye = s.standPoint.position + Vector3.up * 1.65f;
        Snap(Path.Combine(folder, when + "-2-where-ace-stands.png"), eye, s.inspectPoint.position, 60f, false);
        Snap(Path.Combine(folder, when + "-3-from-a-stool.png"), new Vector3(-4.20f, 1.25f, 12.85f), new Vector3(-4.60f, 1.00f, 14.00f), 62f, false);
        Snap(Path.Combine(folder, when + "-4-the-old-place.png"), new Vector3(-.60f, 1.65f, 14.90f), new Vector3(-3.20f, 1.00f, 17.50f), 62f, false);
        GameObject shop = GameObject.Find("CmShopCam");
        if (shop != null)
            Snap(Path.Combine(folder, when + "-5-the-game-camera.png"), shop.transform.position, shop.transform.position + shop.transform.forward * 30f, 39f, true);
    }

    // A photo with Ace's stand-in capsule hidden (as CafeTidySteps and CafeFurnishing.Photograph do).
    static void Snap(string path, Vector3 from, Vector3 to, float fov, bool isometric)
    {
        var hidden = new List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try { CafeSecondPassSteps.Capture(path, from, to, fov, isometric); }
        finally { foreach (Renderer r in hidden) if (r != null) r.forceRenderingOff = false; }
    }

    // ================================================================== helpers

    static string LogRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeRedesign"));

    static string Folder(string label)
    {
        string folder = Path.Combine(LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }

    // Each menu's last word, kept as a file as well as in the Console: Logs/CafeRedesign/repair-bar-last.txt, and the run's
    // report.txt beside its photos.
    static void Keep(string folder, string text)
    {
        try
        {
            Directory.CreateDirectory(LogRoot);
            File.WriteAllText(Path.Combine(LogRoot, "repair-bar-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning(Tag + "Couldn't write the report: " + e.Message); }
    }

    // The first active GameObject in the scene whose name starts with the prefix (the café kit's pieces are CK_ + a name).
    static GameObject ActiveNamed(Scene scene, string prefix) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(false))
            .Select(t => t.gameObject).FirstOrDefault(g => g.name.StartsWith(prefix, StringComparison.Ordinal));

    // Inside a piece whose name starts with the prefix (itself or an ancestor).
    static bool Under(Transform t, string prefix)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    static bool IsPlayer(Survey s, Transform t) => s.player != null && t.IsChildOf(s.player);

    static Bounds RendererBounds(Transform root, bool includeInactive)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // The first "n/m" in a check's result (paths complete, positions served); -1 if there is none.
    static int Reached(string result)
    {
        var m = System.Text.RegularExpressions.Regex.Match(result ?? "", @"(\d+)/(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    // The heading of a rotation's forward, degrees clockwise from north (+Z), 0..360.
    static float Facing(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        return Mathf.Repeat(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 360f);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (Transform p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        return string.Join("/", names.Skip(Math.Max(0, names.Count - 4)));
    }

    static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

    static string BoundsText(Bounds b) => $"x {b.min.x:0.000}..{b.max.x:0.000}, y {b.min.y:0.000}..{b.max.y:0.000}, z {b.min.z:0.000}..{b.max.z:0.000}";
}
#endif
