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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE REAL CAFÉ PASS (30 Sept 2026; Mansoor's second playtest: "the work bench is randomly above,
// and not towards the wall, and the back wall's white finish wasn't even done, some of the
// decorations seem half assed. Make the cafe actually resemble what a cafe would look like.")
//
// What the reference cafés have that the room lacked (claude/cafe-realism-pass.md has the sources):
// a back bar that is one finished wall, tile to eye height and paint above; the work bench
// against the wall under its tools, with a task light; walls with a finish, not bare plaster;
// every decoration standing on something and belonging to a station.
//
//   Fixit Fidget > Cafe furnishing > 2 - Real cafe pass (workshop wall, wall finishes, decor audit)
//   Fixit Fidget > Cafe furnishing > Undo: the real cafe pass
//   Fixit Fidget > Cafe furnishing > Audit the decor (what floats, what belongs to nothing)
//
// WHAT THE PASS DOES
//  1  The repair work area (bench, its slots, rig, stand point and bench camera) moves 0.9 m
//     back so the bench stands against the back wall under the pegboard and the tool rail; the
//     bench mat follows the stand point. The parts cabinet, radio, cable and box slide 1.8 m
//     left, against the wall beside the bench. A clamp lamp on the bench lights the work (a real
//     spot light, warm, no shadow). The three wall lamps' stems, which stopped 10 cm short of the
//     wall, now reach it.
//  2  The back wall gets its finish: the service side (from the back bar's left edge to the
//     right wall) is painted chalk white above the tiles and full height past the drink counter;
//     the workshop side and the tan side wall are painted a deep green, so the tools and the
//     cream pegboard read against it. The paint is a lining a centimetre in front of the plaster,
//     split at the window sill so the cut-away walls fade above the sill exactly as before.
//  3  The abstract print that overlapped the coffee shelf is switched off (a placeholder).
//  4  The decor audit lists every furnishing piece whose bottom floats above what is under it,
//     or sinks into it, and every wall piece that hangs off no wall.
//
// The gameplay guard from the furnishing pass: the routes are re-baked and the layout and
// occupied-circulation checks must not get worse, or everything is put back.
// Nothing is saved: look at the photos, then save the scene (Ctrl+S).
// ---------------------------------------------------------------------------
public static class CafeRealismPass
{
    const string Menu = "Fixit Fidget/Cafe furnishing/";
    const string Tag = "[Real cafe] ";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    public const string GroupName = "23 - real cafe pass";
    const string WorkAreaName = "03 - repair work area";
    const string FurnishingName = "18 - cafe furnishing";
    const string PlaceholdersName = "06 - atmosphere placeholders";
    const string PrintToHide = "Neighborhood color study";
    const string PalettePath = "Assets/Playtests/AcesCafeLayout/Cafe palette.asset";
    const string MaterialFolder = "Assets/Art/CafeFurnishing/Materials";

    // The moves (undone exactly).
    static readonly Vector3 BenchMove = new Vector3(0f, 0f, .9f);
    static readonly Vector3 CabinetMove = new Vector3(-1.8f, 0f, 0f);
    static readonly string[] CabinetPieces = { "Parts cabinet", "Radio", "Cable coil", "Parts box" };
    const string BenchMat = "Repair bench mat";

    // The room, as measured (claude/cafe-realism-pass.md): the back wall's inner face, the tan wall's, the sill.
    const float BackWallZ = 18.0f, TanWallX = -7.4f, Sill = .78f, Ceiling = 3.2f, LiningDepth = .012f;
    const float TileLeft = -1.985f, TileRight = 4.63f, CapTop = 1.535f, RightWallX = 7.4f, TanWallFrom = 12.7f;

    static Dictionary<string, Material> palette;
    static readonly StringBuilder audit = new StringBuilder();

    [MenuItem(Menu + "2 - Real cafe pass (workshop wall, wall finishes, decor audit)")]
    static void ApplyMenu() => Run("Real cafe pass", Apply);

    [MenuItem(Menu + "Undo: the real cafe pass")]
    static void UndoMenu() => Run("Undo the real cafe pass", Revert);

    [MenuItem(Menu + "Audit the decor (what floats, what belongs to nothing)")]
    static void AuditMenu() => Run("Decor audit", () => { string folder = WriteAudit("decor-audit"); return "Audit written: " + folder + "\n" + audit; });

    [MenuItem(Menu + "2 - Real cafe pass (workshop wall, wall finishes, decor audit)", true)]
    [MenuItem(Menu + "Undo: the real cafe pass", true)]
    [MenuItem(Menu + "Audit the decor (what floats, what belongs to nothing)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Run(string title, Func<string> step)
    {
        try { Debug.Log(Tag + title + ": " + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED (nothing half-done was kept): " + e); }
    }

    // ------------------------------------------------------------------ apply

    static string Apply()
    {
        CityPackChecks.RequireScene();
        Transform cafe = Cafe();
        if (cafe.Find(GroupName) != null) return "Already done (" + GroupName + " exists). Run \"Undo: the real cafe pass\" first to redo it.";
        Transform work = cafe.Find(WorkAreaName) ?? throw new InvalidOperationException(WorkAreaName + " is missing.");
        Transform zoneF = cafe.Find(FurnishingName + "/F - Work areas") ?? throw new InvalidOperationException("The furnishing pass's work areas are missing (run Cafe furnishing 1 first).");
        Transform placeholders = cafe.Find(PlaceholdersName);
        LoadMaterials();

        string layoutBefore = AcesCafeLayoutSetup.ValidateLayout();
        string circulationBefore = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        string photosBefore = CafeFurnishing.Photograph("real-cafe-before");
        string workshopBefore = PhotographWorkshop("real-cafe-before");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Real cafe pass");
        Transform root = null;
        string layoutAfter, circulationAfter;
        try
        {
            // 1. The workshop, against the wall.
            Move(work, BenchMove);
            foreach (string piece in CabinetPieces) Move(Child(zoneF, piece), CabinetMove);
            Move(Child(zoneF, BenchMat), BenchMove);

            root = new GameObject(GroupName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Real cafe pass");
            root.SetParent(cafe, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform benchT = work.Find("Workbench") ?? throw new InvalidOperationException(WorkAreaName + "/Workbench is missing.");
            Renderer benchR = benchT.GetComponent<Renderer>();
            Bounds bench = benchR != null ? benchR.bounds : RendererBounds(benchT);   // the bench top itself, not the tools on it
            Transform benchZone = Zone(root, "Bench lamp and tool stand");
            TaskLamp(benchZone, new Vector3(bench.min.x + .22f, bench.max.y, BackWallZ - .22f), new Vector3(bench.center.x, bench.max.y, bench.center.z));
            // The four tool pick-ups hang in a column 0.2-0.5 m over the bench top (InspectRig): a small
            // oak stand on the bench behind them, so they read as tools on a rack, not tools in the air.
            ToolStand(benchZone, new Vector3(-3.08f, bench.max.y, BackWallZ - .455f));
            // The three wall lamps' stems stopped 10 cm short of the wall; now they reach it.
            if (placeholders != null)
                foreach (Transform stem in placeholders)
                    if (stem.name == "Wall lamp stem") StretchStem(stem, +1);

            // 2. The wall finishes.
            WallFinishes(Zone(root, "Wall finishes"));

            // 3. The placeholder print over the coffee shelf.
            Transform print = placeholders != null ? placeholders.Find(PrintToHide) : null;
            if (print != null && print.gameObject.activeSelf)
            {
                Undo.RecordObject(print.gameObject, "Real cafe pass");
                print.gameObject.SetActive(false);
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            layoutAfter = AcesCafeLayoutSetup.ValidateLayout();
            circulationAfter = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
            bool layoutOk = layoutAfter.Contains("PASS") || !layoutBefore.Contains("PASS");
            bool circulationOk = circulationAfter.Contains("PASS") || !circulationBefore.Contains("PASS");
            if (!layoutOk || !circulationOk)
                throw new InvalidOperationException("The pass made a customer route or delivery approach worse, so nothing was kept.\n"
                    + "Before: " + layoutBefore + "\n        " + circulationBefore + "\nAfter:  " + layoutAfter + "\n        " + circulationAfter);
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            Physics.SyncTransforms();
            AcesCafeLayoutSetup.BakeRoutes();
            throw;
        }
        CutawayWall.ForgetCandidates();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        string photosAfter = CafeFurnishing.Photograph("real-cafe-after");
        string workshopAfter = PhotographWorkshop("real-cafe-after");
        string auditFolder = WriteAudit("decor-audit");

        var report = new StringBuilder("\n");
        report.AppendLine($"Bench moved {BenchMove.z:0.0} m back to the wall (its slots, rig, stand point, camera and mat with it); parts cabinet, radio, cable and box {-CabinetMove.x:0.0} m left; a clamp lamp with a real light on the bench; the wall lamps' stems reach the wall.");
        report.AppendLine("Wall finishes: chalk white over the service wall (above the tiles, full height past the drink counter), deep green on the workshop wall and the tan wall; the abstract print over the coffee shelf is off.");
        report.AppendLine("Layout check before: " + layoutBefore);
        report.AppendLine("Layout check after:  " + layoutAfter);
        report.AppendLine("Circulation before:  " + circulationBefore);
        report.AppendLine("Circulation after:   " + circulationAfter);
        report.AppendLine("Photos: " + photosBefore + " and " + photosAfter + "; workshop close-ups " + workshopBefore + " and " + workshopAfter);
        report.AppendLine("Decor audit: " + auditFolder);
        report.Append("Not saved yet: look at the photos, then save the scene to keep it.");
        return report.ToString();
    }

    static string Revert()
    {
        CityPackChecks.RequireScene();
        Transform cafe = Cafe();
        Transform root = cafe.Find(GroupName);
        if (root == null) return "Nothing to undo: " + GroupName + " is not in the scene.";
        Transform work = cafe.Find(WorkAreaName);
        Transform zoneF = cafe.Find(FurnishingName + "/F - Work areas");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Undo the real cafe pass");
        if (work != null) Move(work, -BenchMove);
        if (zoneF != null)
        {
            foreach (string piece in CabinetPieces) { Transform t = zoneF.Find(piece); if (t != null) Move(t, -CabinetMove); }
            Transform mat = zoneF.Find(BenchMat);
            if (mat != null) Move(mat, -BenchMove);
        }
        Transform print = cafe.Find(PlaceholdersName + "/" + PrintToHide);
        if (print != null && !print.gameObject.activeSelf) { Undo.RecordObject(print.gameObject, "Undo the real cafe pass"); print.gameObject.SetActive(true); }
        Transform ph = cafe.Find(PlaceholdersName);
        if (ph != null)
            foreach (Transform stem in ph)
                if (stem.name == "Wall lamp stem") StretchStem(stem, -1);
        Undo.DestroyObjectImmediate(root.gameObject);
        Undo.CollapseUndoOperations(group);
        Physics.SyncTransforms();
        AcesCafeLayoutSetup.BakeRoutes();
        CutawayWall.ForgetCandidates();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "The bench, cabinet and print are back where they were, the finishes are gone, the routes re-baked. " + AcesCafeLayoutSetup.ValidateLayout() + " Nothing saved.";
    }

    // ------------------------------------------------------------------ the pieces

    static void WallFinishes(Transform zone)
    {
        Material white = own["Chalk white"], green = own["Workshop green"];
        float z = BackWallZ - LiningDepth * .5f;
        // Service wall: above the tile band's oak cap between the tiles' ends, full height (split at the sill) past them.
        Lining(zone, "Service wall - above the tiles", white, new Vector3((TileLeft + TileRight) * .5f, (CapTop + Ceiling) * .5f, z), new Vector3(TileRight - TileLeft, Ceiling - CapTop, LiningDepth));
        Lining(zone, "Service wall - corner, below the sill", white, new Vector3((TileRight + RightWallX) * .5f, Sill * .5f, z), new Vector3(RightWallX - TileRight, Sill, LiningDepth));
        Lining(zone, "Service wall - corner", white, new Vector3((TileRight + RightWallX) * .5f, (Sill + Ceiling) * .5f, z), new Vector3(RightWallX - TileRight, Ceiling - Sill, LiningDepth));
        // Workshop wall: the back wall left of the tiles, and the tan wall, deep green (split at the sill).
        Lining(zone, "Workshop wall - below the sill", green, new Vector3((TanWallX + TileLeft) * .5f, Sill * .5f, z), new Vector3(TileLeft - TanWallX, Sill, LiningDepth));
        Lining(zone, "Workshop wall", green, new Vector3((TanWallX + TileLeft) * .5f, (Sill + Ceiling) * .5f, z), new Vector3(TileLeft - TanWallX, Ceiling - Sill, LiningDepth));
        float x = TanWallX + LiningDepth * .5f;
        Lining(zone, "Tan wall - below the sill", green, new Vector3(x, Sill * .5f, (TanWallFrom + BackWallZ) * .5f), new Vector3(LiningDepth, Sill, BackWallZ - TanWallFrom));
        Lining(zone, "Tan wall", green, new Vector3(x, (Sill + Ceiling) * .5f, (TanWallFrom + BackWallZ) * .5f), new Vector3(LiningDepth, Ceiling - Sill, BackWallZ - TanWallFrom));
    }

    static void Lining(Transform zone, string name, Material material, Vector3 centre, Vector3 size)
    {
        var go = Box(zone, name, centre, size, material);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // A clamp lamp on the bench: base, upright, arm, a shade tipped toward the work, the bulb, and its light.
    static void TaskLamp(Transform zone, Vector3 at, Vector3 work)
    {
        var lamp = Node(zone, "Clamp lamp", at, 0f);
        Material steel = M("InteriorInk"), brass = M("InteriorBrass");
        Cyl(lamp, "Base", new Vector3(0f, .014f, 0f), .20f, .028f, steel);
        Cyl(lamp, "Upright", new Vector3(0f, .30f, 0f), .026f, .56f, steel);
        Vector3 toWork = work - at; toWork.y = 0f;
        Vector3 dir = toWork.sqrMagnitude > 1e-4f ? toWork.normalized : Vector3.forward;
        Vector3 elbow = new Vector3(0f, .58f, 0f), shadeAt = elbow + dir * .46f + Vector3.down * .06f;
        var arm = Cyl(lamp, "Arm", (elbow + shadeAt) * .5f, .022f, (shadeAt - elbow).magnitude, steel);
        arm.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (shadeAt - elbow).normalized);
        Cyl(lamp, "Joint", elbow, .046f, .046f, brass);
        Quaternion tip = Quaternion.FromToRotation(Vector3.up, (Vector3.up * .78f - dir * .3f).normalized);
        var shade = Cyl(lamp, "Shade", shadeAt + Vector3.down * .06f, .22f, .14f, steel);
        shade.transform.localRotation = tip;
        var bulb = Cyl(lamp, "Bulb", shadeAt + Vector3.down * .125f, .08f, .014f, own["Bulb"]);
        bulb.transform.localRotation = tip;
        var light = new GameObject("Task light").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.transform.localPosition = shadeAt + Vector3.down * .15f;
        light.transform.localRotation = Quaternion.LookRotation((Vector3.down * .85f + dir * .35f).normalized);
        light.type = LightType.Spot;
        light.spotAngle = 75f; light.innerSpotAngle = 45f;
        light.range = 2.8f;
        light.intensity = 2.6f;
        light.color = new Color(1f, .86f, .68f);
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    // A bench-top tool rack: an oak board on a foot, with a brass rail across the top.
    static void ToolStand(Transform zone, Vector3 at)
    {
        var stand = Node(zone, "Tool stand", at, 0f);
        Material oak = M("Cafe grain - CC_Wood_Espresso"), brass = M("InteriorBrass");
        Box(stand, "Foot", new Vector3(0f, .01f, .02f), new Vector3(.18f, .02f, .10f), oak);
        Box(stand, "Board", new Vector3(0f, .26f, .04f), new Vector3(.14f, .50f, .025f), oak);
        Box(stand, "Rail", new Vector3(0f, .49f, .022f), new Vector3(.14f, .012f, .012f), brass);
    }

    // ------------------------------------------------------------------ the audit

    // Every furnishing and placeholder piece: does it stand on something, or hang on a wall?
    static string WriteAudit(string label)
    {
        audit.Clear();
        Transform cafe = Cafe();
        var roots = new List<Transform>();
        foreach (string groupName in new[] { FurnishingName, PlaceholdersName, GroupName, "22 - Night 1 slice", "17 - back bar finish" })
        {
            Transform g = cafe.Find(groupName);
            if (g == null) continue;
            foreach (Transform zone in g)
            {
                if (zone.GetComponentInChildren<Renderer>(true) == null) continue;
                if (groupName == FurnishingName || groupName == GroupName) foreach (Transform piece in zone) roots.Add(piece);
                else roots.Add(zone);
            }
        }
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        int floating = 0, sunk = 0, loose = 0, fine = 0;
        foreach (Transform piece in roots)
        {
            if (!piece.gameObject.activeInHierarchy) continue;
            var mine = piece.GetComponentsInChildren<Renderer>(false);
            if (mine.Length == 0) continue;
            Bounds b = mine[0].bounds;
            foreach (var r in mine) b.Encapsulate(r.bounds);
            string where = $"{piece.parent.name}/{piece.name} (x {b.center.x:0.00}, z {b.center.z:0.00}, bottom {b.min.y:0.00})";
            // On a wall: within 25 cm of a wall's inner face and higher than a table.
            bool onWall = b.max.z > BackWallZ - .25f && b.min.y > .6f || b.min.x < TanWallX + .25f && b.min.y > .6f || b.max.x > RightWallX - .25f && b.min.y > .6f;
            // Hung from the ceiling.
            bool hung = b.max.y > Ceiling - .05f;
            if (onWall || hung) { fine++; continue; }
            // The highest surface under it: the top of another renderer's box that overlaps it in plan and is not above its bottom.
            float surface = 0f;   // the floor
            foreach (var r in all)
            {
                if (r.transform.IsChildOf(piece)) continue;
                Bounds o = r.bounds;
                if (o.max.x < b.min.x + .02f || o.min.x > b.max.x - .02f || o.max.z < b.min.z + .02f || o.min.z > b.max.z - .02f) continue;
                if (o.max.y > b.min.y + .03f || o.size.y > 3.5f) continue;   // above its bottom (a wall, a neighbour), or the room itself
                if (o.max.y > surface) surface = o.max.y;
            }
            float gap = b.min.y - surface;
            if (gap > .035f) { floating++; audit.AppendLine($"FLOATS {gap:0.00} m  {where}"); }
            else if (gap < -.06f) { sunk++; audit.AppendLine($"SUNK   {-gap:0.00} m  {where}"); }
            else fine++;
        }
        // Wall pieces hanging off no wall: anything in the placeholders group that is high up but not near a wall.
        Transform ph = cafe.Find(PlaceholdersName);
        if (ph != null)
            foreach (Transform piece in ph)
            {
                var mine = piece.GetComponentsInChildren<Renderer>(false);
                if (mine.Length == 0 || !piece.gameObject.activeInHierarchy) continue;
                Bounds b = mine[0].bounds;
                foreach (var r in mine) b.Encapsulate(r.bounds);
                bool nearWall = b.max.z > BackWallZ - .3f || b.min.x < TanWallX + .3f || b.max.x > RightWallX - .3f;
                if (b.min.y > 1.0f && b.max.y < Ceiling - .05f && !nearWall) { loose++; audit.AppendLine($"LOOSE  {piece.name} hangs at {b.min.y:0.00} m off no wall (x {b.center.x:0.00}, z {b.center.z:0.00})"); }
            }
        audit.Insert(0, $"Decor audit: {fine} pieces stand on something or hang on a wall; {floating} float, {sunk} sink, {loose} hang off no wall.\n");
        string folder = Path.Combine(CafeFurnishingCatalog.LogRoot, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "audit.txt"), audit.ToString());
        return folder;
    }

    // ------------------------------------------------------------------ photos

    static string PhotographWorkshop(string label)
    {
        string folder = Path.Combine(CafeFurnishingCatalog.LogRoot, label + "-workshop-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var hidden = new List<Renderer>();
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (var r in player.GetComponentsInChildren<Renderer>())
                if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
        try
        {
            CafeSecondPassSteps.Capture(Path.Combine(folder, "w1-bench-from-the-counter.png"), new Vector3(-.6f, 1.65f, 14.2f), new Vector3(-3.4f, 1.1f, 17.6f), 58f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "w2-bench-from-the-stand-point.png"), new Vector3(-3.3f, 1.62f, 15.6f), new Vector3(-3.3f, 1.2f, 17.9f), 66f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "w3-back-bar-from-the-counter.png"), new Vector3(-1.2f, 1.6f, 14.6f), new Vector3(3.2f, 1.4f, 17.8f), 60f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "w4-whole-back-wall.png"), new Vector3(0f, 2.2f, 11.5f), new Vector3(0f, 1.3f, 18f), 74f, false);
            CafeSecondPassSteps.Capture(Path.Combine(folder, "w5-workshop-corner.png"), new Vector3(-1.4f, 1.7f, 15.8f), new Vector3(-6.2f, 1.0f, 17.4f), 64f, false);
        }
        finally { foreach (var r in hidden) if (r != null) r.forceRenderingOff = false; }
        return folder;
    }

    // ------------------------------------------------------------------ helpers

    static Transform Cafe() => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == CafeRootName)?.transform
        ?? throw new InvalidOperationException(CafeRootName + " is missing.");

    static Transform Child(Transform parent, string name) => parent.Find(name) ?? throw new InvalidOperationException(parent.name + "/" + name + " is missing.");

    // The wall lamp stems are 0.5 m cubes lying along z that ended 0.1 m short of the wall.
    static void StretchStem(Transform stem, int sign)
    {
        Undo.RecordObject(stem, "Real cafe pass");
        Vector3 scale = stem.localScale, position = stem.position;
        scale.z += .1f * sign;
        position.z += .05f * sign;
        stem.localScale = scale;
        stem.position = position;
    }

    static void Move(Transform t, Vector3 by)
    {
        Undo.RecordObject(t, "Real cafe pass");
        t.position += by;
        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    static Transform Zone(Transform root, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    static Transform Node(Transform parent, string name, Vector3 position, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material) =>
        Primitive(parent, name, PrimitiveType.Cube, centre, size, material);

    // Unity's cylinder is 1 wide and 2 tall.
    static GameObject Cyl(Transform parent, string name, Vector3 centre, float diameter, float height, Material material) =>
        Primitive(parent, name, PrimitiveType.Cylinder, centre, new Vector3(diameter, height * .5f, diameter), material);

    static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 centre, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centre;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static void FloorCollider(Transform piece, Vector3 centre, Vector3 size)
    {
        var box = piece.gameObject.AddComponent<BoxCollider>();
        box.center = centre;
        box.size = size;
    }

    static Bounds RendererBounds(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static readonly Dictionary<string, Material> own = new Dictionary<string, Material>();

    static Material M(string name) => palette.TryGetValue(name, out var m) ? m
        : throw new InvalidOperationException("Café palette material missing: " + name);

    static void LoadMaterials()
    {
        palette = AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Material>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
        foreach (string need in new[] { "Cafe grain - CC_Wood_Espresso", "InteriorInk", "InteriorBrass", "InteriorCream", "InteriorMoss" }) M(need);
        own.Clear();
        // Chalk white: a warm off-white, matte, so the tiles and the oak read against it.
        own["Chalk white"] = DerivedMaterial("Wall paint - chalk white", "InteriorCream", m =>
        {
            m.SetColor("_BaseColor", new Color(.93f, .91f, .87f));
            m.SetFloat("_Smoothness", .06f);
        });
        // Workshop green: deep and grey enough to sit behind the sage joinery and the cream pegboard.
        own["Workshop green"] = DerivedMaterial("Wall paint - workshop green", "InteriorMoss", m =>
        {
            m.SetColor("_BaseColor", new Color(.29f, .39f, .33f));
            m.SetFloat("_Smoothness", .08f);
        });
        own["Bulb"] = DerivedMaterial("Task lamp bulb", "InteriorCream", m =>
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, .8f, .5f) * 1.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
    }

    static Material DerivedMaterial(string name, string from, Action<Material> setup)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) throw new InvalidOperationException("Missing folder " + MaterialFolder + " (run Cafe furnishing 1 first).");
            material = new Material(M(from)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        setup(material);
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
