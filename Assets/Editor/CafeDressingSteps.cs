#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Shared = CafeRedesignShared;
using Kit = CafeLivedInSteps;

// ---------------------------------------------------------------------------
// THE CAFÉ FULL FROM ABOVE: THE SECOND DRESSING (14 Oct 2026; specs/art-bench-and-cafe.md Part B, specs/blender-wave1.md §5
// step 4; the Producer's call 9 and target T21)
//
// His notes (14 Oct): his girlfriend "pointed out that the cafe felt empty"; "for it to also look full ... add those cool
// items you know that make it feel like a repair cafe like, actual goods and items that would be at a cafe repair store.
// like bunch of pair in a bin under a desk near ace ... like adding props, like paper, bins, post it notes, tape, ruler,
// coffee cups, to go cups, coffee syrup, all these things."
//
//   Fixit Fidget > Cafe redesign > Café dressing 1 - Import and check the pieces (the models only)
//   Fixit Fidget > Cafe redesign > Café dressing 1 - Dress the café (scene)
//   Fixit Fidget > Cafe redesign > Café dressing 1 - Undo (scene)
//   Fixit Fidget > Cafe redesign > Café dressing 1 - Check from above (read-only)
//
// THE PIECES are the Blender lead's (Tools/Blender/cafe_repairside.py, cafe_cafeside.py): Assets/Art/Models/CafeKit/LI_*.fbx
// with cafe_dressing.json beside them and four pictures in Assets/Art/Textures/Cafe. Art's B3, where each goes and why:
//   R1 LI_PickupRow: fixed repairs waiting for pickup, bagged and tagged, on the back bar under the parts drawers;
//   R2 LI_DeskCorner: Ace's corner of the bench on a grey anti-static pad (parts cabinet, wire, multimeter, ticket pad,
//      post-its, tape, steel rule, his green mug), and LI_GlassNotes, two post-its on the glass on Ace's side;
//   R3 LI_DonorParts: "a bunch of parts in a bin under a desk near Ace", on the floor in the bench's knee space;
//   R4 LI_Whiteboard: "THIS WEEK" over the parts wall; R5 LI_PartsDelivery: a half-open parts box on the counter's west end;
//   S1 LI_MortgageLetter: the statement under the till, the past-due line circled; S2 LI_FinalNotice: inside the back door;
//   C1 LI_SyrupRack (the three Synty bottles there are switched off, recorded, never deleted), C2 LI_CupStation (to-go
//      cups, lids, sleeves, the milk jug), C3 LI_BarMat (upside-down mugs on a ribbed mat; the Blender lead's wave 2 adds a
//      mug tree to it: call 9).
// Décor only (his decision 9): no collider, no hover, nothing on a floor anyone walks (the routes bake from colliders, so
// they cannot change; the guards run before and after all the same).
//
// IMPORT (Café dressing 1 - Import) is Lived in 1's for this manifest: the four pictures (clamped, Kit 1's settings), the
// manifest's new materials made Kit 1's way in Assets/Art/Materials/Cafe (CafeLivedInSteps.Define), the ones it names as
// existing found by name (a material another kit defines is never redefined here), each FBX's settings (scale 1, readable,
// no colliders) with every material mapped, and each piece checked as built (meshes, triangles, size, upright, its origin at
// its bottom). Run it again whenever a piece is re-exported (the mug tree): the placed copies are the FBX's own instances,
// so they change with it.
//
// DRESS (Café dressing 1 - Dress) runs through the café redesign's shared step (CafeRedesignShared.Step: the survey, the
// guards before and after, one undo group with a record, checks, photos before and after; anything failed puts everything
// back; a second run says it is already done). Every place is SEARCHED, not assumed (Lived in 2's rule): each piece starts
// at the manifest's place (Unity world space) and, if anything is wrong there, tries the nearby places its kind allows,
// taking the first where every rule holds:
//   * a piece on a top or the floor: settled on what is really drawn under its footprint (the middle and four corners, 2 cm
//     in) within 2 cm of its planned bottom, then moved a few centimetres toward the room or sideways if it must;
//   * a hung piece (the whiteboard, the notice, the post-its): its back 0.5 mm off the flat surface drawn behind it (five
//     rays, all within 1 cm: one flat face), within 5 cm of its plan, then moved up or down in 5 cm steps if it must (the
//     back door's leaf carries the STAFF ONLY plate at about 1.46 m, where the notice was planned: CafeBarSteps);
//   * everywhere: nothing drawn passes through it (its own volume, cell by cell, so a long cluster of small things isn't
//     judged by its outer box), and no gameplay anchor moves.
// A piece with no good place is LEFT OUT and said so (the rest still go in); the read-only check lists it. Then Night walk 2
// (the night's solid list) and save, as after Lived in 2.
//
// CHECK FROM ABOVE (read-only, any time, again after the wave-2 re-import): each placed piece where it should be (on its
// top or its wall, nothing through it, no collider), the three bottles off, whether any piece shows in the bench's close-up
// (Room 1's rule), and Art's 10-pixel rule from the overhead (T21): each piece's mesh projected through the overhead's home
// view (CafeViewMode's camera as Play starts it: 43 degrees, 34 m, its 44 degree lens) at 1920 x 1080; its smaller side
// must be 12 px or more for R1, R2, R4, R5, C1, C2 and C3 (R3, S1, S2 and the glass notes are private by design). C3 is
// expected to miss by about 3 px until the mug tree (the Blender lead measured 25 x 9).
// Reports and photos: Logs/CafeRedesign/cafe-dressing-1-*; the room's 14 photos: Logs/CafeFurnishing/cafe-dressing-1-*.
// ---------------------------------------------------------------------------
internal static class CafeDressingSteps
{
    public const string Title = "Cafe dressing 1 - Dress the cafe";
    const string Label = "cafe-dressing-1";
    const string ImportTitle = "Cafe dressing 1 - Import and check the pieces (the models only)";
    const string ImportLabel = "cafe-dressing-1-import";
    const string CheckTitle = "Cafe dressing 1 - Check from above (read-only)";
    const string CheckLabel = "cafe-dressing-1-check";
    const string MenuImport = Shared.Menu + "Café dressing 1 - Import and check the pieces (the models only)";
    const string MenuDo = Shared.Menu + "Café dressing 1 - Dress the café (scene)";
    const string MenuUndo = Shared.Menu + "Café dressing 1 - Undo (scene)";
    const string MenuCheck = Shared.Menu + "Café dressing 1 - Check from above (read-only)";
    const string GroupName = "Cafe dressing 1";

    const string Models = Shared.KitModels;
    const string ManifestPath = Models + "/cafe_dressing.json";
    const string MaterialFolder = Shared.KitMaterials;
    // The kits already in the café: a material one of them defines is theirs, used as it is.
    static readonly string[] OtherManifests = { "cafe_signs.json", "cafe_bar.json", "cafe_props.json", "cafe_seating.json", "cafe_intake.json", "cafe_livedin.json" };

    enum Mount { Top, Floor, Wall }

    sealed class Spec
    {
        public readonly string piece, code, what;
        public readonly Mount mount;
        public readonly bool exempt;      // private by design: the overhead's 10-pixel rule doesn't apply
        public readonly string tuck;      // the start of the name of what it goes under by design (S1: the till), or null
        public Spec(string piece, string code, string what, Mount mount, bool exempt = false, string tuck = null)
        {
            this.piece = piece; this.code = code; this.what = what; this.mount = mount; this.exempt = exempt; this.tuck = tuck;
        }
    }

    static readonly Spec[] Pieces =
    {
        new Spec("LI_PickupRow", "R1", "fixed repairs waiting for pickup", Mount.Top),
        new Spec("LI_DeskCorner", "R2", "Ace's desk corner", Mount.Top),
        new Spec("LI_GlassNotes", "R2", "post-its on the glass, Ace's side", Mount.Wall, true),
        new Spec("LI_DonorParts", "R3", "donor parts under the bench", Mount.Floor, true),
        new Spec("LI_Whiteboard", "R4", "this week's whiteboard", Mount.Wall),
        new Spec("LI_PartsDelivery", "R5", "the parts delivery", Mount.Top),
        // "tucked under 'Register v4''s corner on Ace's side" (Art's S1): the till's walls stand on the sheet's far third by design
        new Spec("LI_MortgageLetter", "S1", "the mortgage statement under the till", Mount.Top, true, "'Register v4"),
        new Spec("LI_FinalNotice", "S2", "the final notice inside the back door", Mount.Wall, true),
        new Spec("LI_SyrupRack", "C1", "the syrup rack", Mount.Top),
        new Spec("LI_CupStation", "C2", "the cup and lid station", Mount.Top),
        new Spec("LI_BarMat", "C3", "the bar mat and mugs", Mount.Top),
    };
    const string SyrupRack = "LI_SyrupRack", BarMat = "LI_BarMat";

    // The three Synty bottles the syrup rack takes the place of (Art's B3; licensed: switched off, recorded, never deleted).
    static readonly string[] Bottles =
    {
        "16 - POLYGON cafe details/Drinks/Drinks - Bottle_01", "16 - POLYGON cafe details/Drinks/Drinks - Bottle_02",
        "16 - POLYGON cafe details/Drinks/Drinks - Bottle_03",
    };

    // ---- the rules' numbers ----
    const float SnapTop = .02f;        // a top or floor piece's planned bottom may be this far from what is drawn under it
    const float SnapWall = .05f;       // a hung piece's planned back may be this far from what it hangs on (S2 was 4 cm in the door)
    const float Flat = .01f;           // ... and what is behind it must be one face: its five rays within this of each other
    const float Proud = .0005f;        // a hung piece's back this far off its face
    const float Inset = .02f;          // the footprint's (or back's) points measured this far in from its edges
    const float Shrink = .003f;        // a cell tested for "nothing passes through", shrunk all round (what it rests on only touches it)
    const float Cell = .10f;           // the piece's volume tested in cells of about this size
    const float AnchorSlack = .001f;
    const int MinPixels = 12, ScreenW = 1920, ScreenH = 1080;   // T21

    // ================================================================== menus

    [MenuItem(MenuImport)]
    static void ImportThePieces() => Import();

    [MenuItem(MenuDo)]
    static void DressTheCafe()
    {
        hadRecord = RecordNow();
        Plan plan = null;
        // No re-bake: the routes bake from colliders (AcesCafeLayoutSetup: PhysicsColliders) and nothing here has one.
        Shared.Step(Title, Label, run => plan = Survey(run), run => Change(run, plan), run => Photos(run, plan), guards: true, rebake: false);
        SaveIfDone();
    }

    [MenuItem(MenuUndo)]
    static void UndoTheDressing()
    {
        hadRecord = RecordNow();
        Shared.UndoStep(Title, Label, rebake: false);
        if (hadRecord && !RecordNow()) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem(MenuCheck)]
    static void CheckFromAbove() => Shared.Check(CheckTitle, CheckLabel, Inspect);

    [MenuItem(MenuImport, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    [MenuItem(MenuCheck, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // The builders' rule: a scene step saves the scene. The shared step leaves it unsaved after a run (look at the photos
    // first); it is saved here only when this run made the record, so a refused or rolled-back run saves nothing.
    static bool hadRecord;
    static bool RecordNow()
    {
        GameObject cafe = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == Shared.CafeRootName);
        return cafe != null && Shared.RecordOf(cafe.transform, Title) != null;
    }

    static void SaveIfDone()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (hadRecord || !RecordNow()) return;
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{Title}] The scene is saved. Next: Fixit Fidget > Night > Night walk 2 (the night's solid list), then save again.");
    }


    // ================================================================== the pieces as imported

    sealed class Piece
    {
        public Spec spec;
        public Dictionary<string, object> entry;
        public string path;
        public GameObject asset;
        public readonly List<(Mesh mesh, Matrix4x4 toRoot)> meshes = new List<(Mesh mesh, Matrix4x4 toRoot)>();
        public Bounds local;                       // all its meshes about its origin (Unity axes)
        public readonly List<Bounds> cells = new List<Bounds>();   // the cells of its own volume (local)
        public Vector3 planned;                    // the manifest's place (world)
        public Quaternion rotation = Quaternion.identity;
        public Vector3 position;                   // where it goes
        public string how = "";                    // how it settled
        public string leftOut;                     // why it isn't placed (null: it is)
        public GameObject placed;
        public string Name => $"{spec.piece} ({spec.code}: {spec.what})";
        public Matrix4x4 Pose => Matrix4x4.TRS(position, rotation, Vector3.one);
        public Bounds World => BlenderKitSteps.Transformed(local, Pose);
    }

    static Dictionary<string, Dictionary<string, object>> ReadManifest(out Dictionary<string, object> root, out string problem)
    {
        root = null;
        problem = null;
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
        if (!File.Exists(file))
        {
            problem = $"{ManifestPath} isn't in the project: copy the Blender lead's cafe_dressing.json and LI_*.fbx into {Models} and the LI_*.png into " +
                      $"{Shared.KitTextures}, reimport, then {ImportTitle}.";
            return null;
        }
        try { root = Kit.Json.Parse(File.ReadAllText(file)) as Dictionary<string, object>; }
        catch (Exception e) { problem = $"{ManifestPath} doesn't read as JSON: {e.Message}"; return null; }
        if (root == null || !root.TryGetValue("pieces", out object listed) || !(listed is List<object> list))
        {
            problem = $"{ManifestPath} has no pieces list (the kit's format: version, kit, note, pieces[], materials, existing_materials).";
            return null;
        }
        var pieces = new Dictionary<string, Dictionary<string, object>>();
        foreach (object o in list)
            if (o is Dictionary<string, object> d && Kit.Str(d, "name") is string n) pieces[n] = d;
        return pieces;
    }

    // The piece as imported and as the manifest places it. problem: why it can't be used at all (the step refuses).
    static Piece Measure(Spec spec, Dictionary<string, Dictionary<string, object>> manifest, out string problem)
    {
        problem = null;
        if (!manifest.TryGetValue(spec.piece, out Dictionary<string, object> entry)) { problem = $"{spec.piece} isn't in {ManifestPath}."; return null; }
        string path = Models + "/" + spec.piece + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { problem = $"{path} isn't imported: copy the Blender lead's {spec.piece}.fbx into {Models}, reimport, then {ImportTitle}."; return null; }
        var p = new Piece { spec = spec, entry = entry, path = path, asset = asset };
        bool first = true;
        foreach (MeshFilter f in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Matrix4x4 toRoot = asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
            p.meshes.Add((f.sharedMesh, toRoot));
            Bounds b = BlenderKitSteps.Transformed(f.sharedMesh.bounds, toRoot);
            if (first) { p.local = b; first = false; } else p.local.Encapsulate(b);
        }
        if (p.meshes.Count == 0) { problem = $"{spec.piece}: no mesh in {path}."; return null; }
        float turned = Quaternion.Angle(asset.transform.localRotation, Quaternion.identity) + p.meshes.Max(m => Quaternion.Angle(m.toRoot.rotation, Quaternion.identity));
        if (turned > .5f || (asset.transform.localScale - Vector3.one).magnitude > .001f)
        {
            problem = $"{spec.piece} is imported turned or scaled ({F(turned, "0.0")} degrees off upright): {ImportTitle} sets scale 1 and the kit's axes.";
            return null;
        }
        Renderer[] renderers = asset.GetComponentsInChildren<Renderer>(true);
        bool mapped = renderers.Length > 0 && renderers.All(r => r.sharedMaterials.Length > 0 &&
                      r.sharedMaterials.All(m => m != null && !AssetDatabase.GetAssetPath(m).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)));
        if (!mapped) { problem = $"{spec.piece}'s materials aren't mapped to the project's: run {ImportTitle}."; return null; }
        float bottom = Kit.Num(entry, "bottom", 0f);
        if (Mathf.Abs(p.local.min.y - bottom) > .006f)
        {
            problem = $"{spec.piece}'s bottom is at {F(p.local.min.y)} about its origin (the manifest: {F(bottom)}): the kit's rule is the origin at its bottom.";
            return null;
        }
        if (!(entry.TryGetValue("placement", out object po) && po is Dictionary<string, object> place) || !Kit.TryVec(place, "position", out Vector3 at))
        {
            problem = $"{spec.piece}: the manifest gives no placement position.";
            return null;
        }
        p.planned = at;
        p.rotation = Quaternion.Euler(Kit.TryVec(place, "rotation", out Vector3 euler) ? euler : new Vector3(0f, 180f, 0f));
        p.position = at;
        Cells(p);
        return p;
    }

    // Its own volume as cells about Cell in size: the ones its triangles touch (vertices, edge middles and centres sampled).
    static void Cells(Piece p)
    {
        Vector3 size = p.local.size;
        int nx = Mathf.Clamp(Mathf.CeilToInt(size.x / Cell), 1, 30), ny = Mathf.Clamp(Mathf.CeilToInt(size.y / Cell), 1, 8), nz = Mathf.Clamp(Mathf.CeilToInt(size.z / Cell), 1, 8);
        var used = new bool[nx, ny, nz];
        void Mark(Vector3 v)
        {
            Vector3 u = v - p.local.min;
            int i = Mathf.Clamp((int)(u.x / Mathf.Max(size.x, 1e-4f) * nx), 0, nx - 1), j = Mathf.Clamp((int)(u.y / Mathf.Max(size.y, 1e-4f) * ny), 0, ny - 1),
                k = Mathf.Clamp((int)(u.z / Mathf.Max(size.z, 1e-4f) * nz), 0, nz - 1);
            used[i, j, k] = true;
        }
        bool read = false;
        foreach (var (mesh, toRoot) in p.meshes)
        {
            Vector3[] v;
            int[] t;
            try { v = mesh.vertices; t = mesh.triangles; }
            catch (Exception) { continue; }
            if (v.Length == 0) continue;
            read = true;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = toRoot.MultiplyPoint3x4(v[t[i]]), b = toRoot.MultiplyPoint3x4(v[t[i + 1]]), c = toRoot.MultiplyPoint3x4(v[t[i + 2]]);
                Mark(a); Mark(b); Mark(c);
                Mark((a + b) * .5f); Mark((b + c) * .5f); Mark((c + a) * .5f); Mark((a + b + c) / 3f);
            }
        }
        p.cells.Clear();
        if (!read) { p.cells.Add(p.local); return; }     // can't read the mesh: its whole box
        Vector3 step = new Vector3(size.x / nx, size.y / ny, size.z / nz);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                for (int k = 0; k < nz; k++)
                    if (used[i, j, k])
                        p.cells.Add(new Bounds(p.local.min + Vector3.Scale(step, new Vector3(i + .5f, j + .5f, k + .5f)), step));
    }

    // ================================================================== the plan

    sealed class Plan
    {
        public Kit.Scenery scene;
        public readonly HashSet<Renderer> movers = new HashSet<Renderer>();
        public readonly List<Piece> pieces = new List<Piece>();
        public readonly List<GameObject> bottles = new List<GameObject>();
        public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        public Overhead view;
        public IEnumerable<Piece> Placed => pieces.Where(x => x.leftOut == null);
        public Piece Of(string piece) => pieces.FirstOrDefault(x => x.spec.piece == piece);
    }

    static Plan Survey(Shared.Run run)
    {
        var p = new Plan();
        Physics.SyncTransforms();
        foreach (string step in new[] { "Bar 1 - The front counter", "Bar 2 - The back bar" })
            if (!Shared.Done(run, step)) run.Info($"'{step}' isn't recorded in this scene: the places are the Blender lead's, measured against the scene as it is.");
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
                if (a != null && a.actor != null)
                    foreach (Renderer r in a.actor.GetComponentsInChildren<Renderer>(true)) p.movers.Add(r);
        p.scene = new Kit.Scenery(run, p.movers);
        run.Info($"The drawn scene read for the measuring: {p.scene.Count} renderers round the café.");

        // The bottles the syrup rack replaces: found, and left out of the measuring (they go off with it).
        foreach (string path in Bottles)
        {
            Transform t = Shared.Find(run, path);
            if (t == null) run.Info($"'{path}' isn't under the café's root (already gone or renamed): nothing to switch off there.");
            else { p.bottles.Add(t.gameObject); run.Info($"'{path}': {(t.gameObject.activeSelf ? "on" : "already off")}, at {V(t.position)}."); }
        }

        run.Line();
        run.Line($"The pieces ({ManifestPath}; world metres; the planned place, then where it settles):");
        Dictionary<string, Dictionary<string, object>> manifest = ReadManifest(out _, out string problem);
        run.Require(manifest != null, problem);
        foreach (Spec spec in Pieces)
        {
            Piece piece = Measure(spec, manifest, out string why);
            run.Require(piece != null, why);
            p.pieces.Add(piece);
            Settle(p, piece);
            run.Info(piece.leftOut == null
                ? $"{piece.Name}: planned {V(piece.planned)}, goes at {V(piece.position)} turned {F(Shared.Facing(piece.rotation), "0")} ({piece.how}); box {Shared.BoundsText(piece.World)}"
                : $"{piece.Name}: LEFT OUT: {piece.leftOut}");
        }
        foreach (KeyValuePair<Transform, Vector3> a in Kit.Anchors(run)) p.anchors[a.Key] = a.Value;
        run.Info($"Gameplay anchors noted (checked after, 1 mm): {p.anchors.Count}.");

        // From above, at the planned places (Art's 10-pixel rule; reported here, judged by the read-only check).
        p.view = HomeView(run);
        run.Line();
        run.Line($"From above ({p.view.how}): each piece's smaller side at {ScreenW} x {ScreenH} (12 px reads as a shape):");
        foreach (Piece x in p.Placed) run.Info(PixelLine(p.view, x, x.Pose));
        int left = p.pieces.Count(x => x.leftOut != null);
        run.Require(left < p.pieces.Count, "No piece has a good place: nothing to dress (see each piece's reason above).");
        if (left > 0) run.Info($"{left} piece(s) LEFT OUT (the rest go in; the check from above lists them).");
        return p;
    }

    // Where a piece goes: its planned place, settled on what holds it; if anything is wrong there, the nearby places its
    // kind allows, in order; the first where every rule holds.
    static void Settle(Plan p, Piece x)
    {
        string first = null;
        foreach (Vector3 offset in Candidates(x))
        {
            string why = TryAt(p, x, x.planned + offset, out Vector3 at, out string how);
            if (why == null)
            {
                x.position = at;
                x.how = how + (offset.sqrMagnitude > 1e-8f ? $"; moved {V(offset)} from its plan: {first}" : "");
                x.leftOut = null;
                return;
            }
            if (first == null) first = why;
        }
        x.leftOut = first + " (and no place nearby was better)";
    }

    // The nearby places: a top piece a few cm toward the room (away from the wall at its back), then sideways; a floor piece
    // sideways and forward; a hung piece up or down in 5 cm steps (and a little sideways for the small ones).
    static IEnumerable<Vector3> Candidates(Piece x)
    {
        yield return Vector3.zero;
        Vector3 front = x.rotation * Vector3.forward, side = x.rotation * Vector3.right;
        if (x.spec.mount == Mount.Wall)
        {
            for (int i = 1; i <= 8; i++)
            {
                yield return Vector3.up * (.05f * i);
                yield return Vector3.down * (.05f * i);
            }
            foreach (float s in new[] { .05f, -.05f, .1f, -.1f }) yield return side * s;
            yield break;
        }
        foreach (float f in new[] { .01f, .02f, .03f, .04f, .05f }) yield return front * f;
        foreach (float s in new[] { .02f, -.02f, .04f, -.04f })
        {
            yield return side * s;
            yield return side * s + front * .02f;
        }
        if (x.spec.mount == Mount.Floor)
            foreach (float s in new[] { .06f, -.06f, .1f, -.1f }) yield return side * s;
    }

    // One place: settled on what holds it there, then the rules. Null when it holds; else why not.
    static string TryAt(Plan p, Piece x, Vector3 planned, out Vector3 at, out string how)
    {
        at = planned;
        how = "";
        Func<Transform, bool> aside = t => p.bottles.Any(b => b != null && t.IsChildOf(b.transform)) || Tucked(x, t);
        if (x.spec.mount == Mount.Wall)
        {
            // Its back plane (local min z) and five points on it; rays from 8 cm in front, toward its back.
            Vector3 back = x.rotation * Vector3.back;
            const float reach = .08f;
            var hits = new List<float>();
            foreach (Vector3 local in BackPoints(x.local))
            {
                Vector3 onBack = planned + x.rotation * local;
                float? hit = p.scene.Along(onBack - back * reach, back, reach + SnapWall + .02f, aside);
                if (hit.HasValue) hits.Add(hit.Value - reach);
            }
            if (hits.Count < 5) return $"nothing drawn behind {5 - hits.Count} of the five points of its back, within {F(SnapWall * 100f, "0")} cm of where it was planned";
            float near = hits.Min(), spread = hits.Max() - near;
            if (spread > Flat) return $"what is behind it isn't one flat face (its five points find it {F(near * 100f, "0.0")} to {F(hits.Max() * 100f, "0.0")} cm behind its back)";
            if (Mathf.Abs(near) > SnapWall) return $"the face behind it is {F(near * 100f, "0.0")} cm from its planned back (more than {F(SnapWall * 100f, "0")} cm)";
            at = planned + back * (near - Proud);
            how = $"its back {F(Proud * 1000f, "0.0")} mm off the face behind it ({(near >= 0f ? "moved back " : "brought forward ")}{F(Mathf.Abs(near) * 100f, "0.0")} cm)";
        }
        else
        {
            // What is drawn under its footprint (the middle and four corners, 2 cm in), at most 2 cm over its planned bottom.
            float bottom = planned.y + x.local.min.y;
            var heights = new List<float>();
            foreach (Vector3 local in FootPoints(x.local))
            {
                Vector3 w = planned + x.rotation * local;
                float y = p.scene.TopAt(new Vector2(w.x, w.z), bottom + SnapTop, null);
                if (!float.IsNegativeInfinity(y) && y >= bottom - .25f) heights.Add(y);
            }
            if (heights.Count == 0) return $"nothing drawn under it near its planned bottom at {F(bottom)}";
            float top = heights.Max();
            if (top < bottom - SnapTop) return $"what is under it is {F((bottom - top) * 100f, "0.0")} cm below its planned bottom (more than {F(SnapTop * 100f, "0")} cm)";
            at = new Vector3(planned.x, planned.y + (top - bottom), planned.z);
            float low = heights.Min();
            how = $"on what is drawn under it at {F(top)} ({(Mathf.Abs(top - bottom) < .0005f ? "as planned" : (top > bottom ? "up " : "down ") + F(Mathf.Abs(top - bottom) * 1000f, "0") + " mm")})" +
                  (top - low > .01f ? $"; one point of its footprint finds {F((top - low) * 100f, "0.0")} cm less under it (an edge over a lip)" : "");
            if (heights.Count < 5) how += $"; {5 - heights.Count} of its five points find nothing under them";
        }
        // Nothing drawn passes through it (cell by cell), the bottles it replaces aside.
        List<string> through = Through(p, x, at, aside);
        if (through.Count > 0) return "it would pass through " + string.Join("; ", through.Take(4));
        return null;
    }

    // What a piece goes under by design (S1 and the till): it and everything under it.
    static bool Tucked(Piece x, Transform t)
    {
        if (x.spec.tuck == null) return false;
        for (Transform up = t; up != null; up = up.parent)
            if (up.name.StartsWith(x.spec.tuck, StringComparison.Ordinal)) return true;
        return false;
    }

    static List<string> Through(Plan p, Piece x, Vector3 at, Func<Transform, bool> aside)
    {
        Matrix4x4 pose = Matrix4x4.TRS(at, x.rotation, Vector3.one);
        var found = new List<string>();
        // Its whole box first (one test): nothing there, nothing in any of its cells. Only then cell by cell.
        var whole = new Bounds(x.local.center, Vector3.Max(x.local.size - Vector3.one * (2f * Shrink), Vector3.one * .001f));
        if (p.scene.Through(whole, pose, aside).Count == 0) return found;
        foreach (Bounds c in x.cells)
        {
            var shrunk = new Bounds(c.center, Vector3.Max(c.size - Vector3.one * (2f * Shrink), Vector3.one * .001f));
            foreach (string hit in p.scene.Through(shrunk, pose, aside))
                if (!found.Contains(hit)) found.Add(hit);
        }
        return found;
    }

    static IEnumerable<Vector3> FootPoints(Bounds b)
    {
        float y = b.min.y, x0 = Mathf.Min(b.min.x + Inset, b.center.x), x1 = Mathf.Max(b.max.x - Inset, b.center.x),
              z0 = Mathf.Min(b.min.z + Inset, b.center.z), z1 = Mathf.Max(b.max.z - Inset, b.center.z);
        yield return new Vector3(b.center.x, y, b.center.z);
        yield return new Vector3(x0, y, z0);
        yield return new Vector3(x1, y, z0);
        yield return new Vector3(x0, y, z1);
        yield return new Vector3(x1, y, z1);
    }

    static IEnumerable<Vector3> BackPoints(Bounds b)
    {
        float z = b.min.z, x0 = Mathf.Min(b.min.x + Inset, b.center.x), x1 = Mathf.Max(b.max.x - Inset, b.center.x),
              y0 = Mathf.Min(b.min.y + Inset, b.center.y), y1 = Mathf.Max(b.max.y - Inset, b.center.y);
        yield return new Vector3(b.center.x, b.center.y, z);
        yield return new Vector3(x0, y0, z);
        yield return new Vector3(x1, y0, z);
        yield return new Vector3(x0, y1, z);
        yield return new Vector3(x1, y1, z);
    }

    // ================================================================== the change

    static void Change(Shared.Run run, Plan p)
    {
        if (p == null) throw new InvalidOperationException("The survey didn't run.");
        Transform group = Shared.StepGroup(run, GroupName);
        foreach (Piece x in p.Placed)
        {
            GameObject go = Shared.Place(run, x.spec.piece, group, x.position, 0f, x.Name);
            go.transform.SetPositionAndRotation(x.position, x.rotation);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            x.placed = go;
        }
        Piece rack = p.Of(SyrupRack);
        if (rack != null && rack.placed != null)
            foreach (GameObject bottle in p.bottles)
                Shared.SwitchOff(run, bottle, "the syrup rack (C1, Café dressing 1) stands where it stood; a Synty bottle, switched off, never deleted");
        Physics.SyncTransforms();
        Shared.Note(run, $"Café dressing 1: {p.Placed.Count()} of {p.pieces.Count} pieces placed under {GroupName}" +
                         (p.pieces.Any(x => x.leftOut != null) ? $"; LEFT OUT: {string.Join(", ", p.pieces.Where(x => x.leftOut != null).Select(x => x.spec.piece))}" : "") +
                         $"; {(rack != null && rack.placed != null ? p.bottles.Count + " Synty bottle(s) switched off" : "the bottles left on (no syrup rack)")}.");
        After(run, p);
    }

    // The checks after the change (any FAIL puts everything back).
    static void After(Shared.Run run, Plan p)
    {
        p.scene = new Kit.Scenery(run, p.movers);
        foreach (Piece x in p.Placed)
        {
            GameObject go = x.placed;
            run.Check(go != null && go.name == x.Name && go.activeInHierarchy, $"{x.Name}: placed and on");
            if (go == null) continue;
            run.Check(Vector3.Distance(go.transform.position, x.position) <= .001f && Quaternion.Angle(go.transform.rotation, x.rotation) <= .1f,
                      $"{x.spec.piece}: at {V(go.transform.position)}, turned {F(Shared.Facing(go.transform.rotation), "0")}");
            int colliders = go.GetComponentsInChildren<Collider>(true).Length;
            run.Check(colliders == 0, $"{x.spec.piece}: no collider, no hover (décor) ({colliders})");
            List<string> through = Through(p, x, x.position, t => t.IsChildOf(go.transform) || Tucked(x, t));
            run.Check(through.Count == 0, $"{x.spec.piece}: nothing passes through it{(through.Count > 0 ? ": " + string.Join("; ", through.Take(6)) : "")}");
        }
        Piece rack = p.Of(SyrupRack);
        if (rack != null && rack.placed != null)
            run.Check(p.bottles.All(b => b == null || !b.activeSelf), $"the Synty bottles under the syrup rack are off ({p.bottles.Count(b => b != null && !b.activeSelf)} of {p.bottles.Count})");
        var moved = p.anchors.Where(a => a.Key == null || Vector3.Distance(a.Key.position, a.Value) > AnchorSlack).Select(a => a.Key != null ? Shared.PathOf(a.Key) : "(gone)").ToList();
        run.Check(moved.Count == 0, moved.Count == 0 ? $"No gameplay anchor moved ({p.anchors.Count}, 1 mm)." : "Moved: " + string.Join(", ", moved));
        CloseUps(run, p.Placed.Where(x => x.placed != null).Select(x => (x.spec.piece, x.World)));
    }

    // ================================================================== the check from above (read-only, any time)

    static void Inspect(Shared.Run run)
    {
        if (!Shared.Done(run, Title))
        {
            run.Info($"'{Title}' hasn't run in this scene: what the survey would place, and how it would read from above.");
            Survey(run);
            return;
        }
        Physics.SyncTransforms();
        var p = new Plan();
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
                if (a != null && a.actor != null)
                    foreach (Renderer r in a.actor.GetComponentsInChildren<Renderer>(true)) p.movers.Add(r);
        p.scene = new Kit.Scenery(run, p.movers);
        p.view = HomeView(run);
        Transform group = Shared.Find(run, Shared.RedesignGroup + "/" + GroupName);
        run.Require(group != null, $"'{Shared.RedesignGroup}/{GroupName}' isn't under the café's root, though the step's record is: undo it and dress again.");
        Dictionary<string, Dictionary<string, object>> manifest = ReadManifest(out _, out string problem);
        run.Require(manifest != null, problem);
        run.Line($"The pieces as placed (the overhead: {p.view.how}; 12 px or more in the smaller side reads as a shape):");
        foreach (Spec spec in Pieces)
        {
            Piece x = Measure(spec, manifest, out string why);
            if (x == null) { run.Check(false, why); continue; }
            Transform t = group.Cast<Transform>().FirstOrDefault(c => c.name == x.Name);
            if (t == null) { run.Check(false, $"{x.Name}: NOT PLACED (left out when the café was dressed; its reason is in that run's report)"); continue; }
            x.placed = t.gameObject;
            x.position = t.position;
            x.rotation = t.rotation;
            p.pieces.Add(x);
            List<string> through = Through(p, x, x.position, c => c.IsChildOf(t) || Tucked(x, c));
            run.Check(through.Count == 0, $"{x.spec.piece}: nothing passes through it{(through.Count > 0 ? ": " + string.Join("; ", through.Take(6)) : "")}");
            run.Check(t.GetComponentsInChildren<Collider>(true).Length == 0, $"{x.spec.piece}: no collider (décor)");
            Rect r = OnScreen(p.view, x, x.Pose);
            float small = Mathf.Min(r.width, r.height);
            string line = PixelLine(p.view, x, x.Pose);
            if (x.spec.exempt) run.Info(line);
            else run.Check(small >= MinPixels, line + (x.spec.piece == BarMat && small < MinPixels ? " (expected until the Blender lead's wave-2 mug tree: the Producer's call 9)" : ""));
        }
        Piece rack = p.Of(SyrupRack);
        if (rack != null)
            foreach (string path in Bottles)
            {
                Transform b = Shared.Find(run, path);
                run.Check(b == null || !b.gameObject.activeSelf, $"'{path}': {(b == null ? "not in the scene" : b.gameObject.activeSelf ? "ON under the syrup rack" : "off")}");
            }
        CloseUps(run, p.pieces.Select(x => (x.spec.piece, x.World)));
    }

    // Room 1's rule: no dressing piece in a device's close-up. The bench's close-up camera as it stands (CM_InspectCam under
    // each BenchRig), its frustum at 16:9 against each piece's box. Reported, not judged: the TD's new framings change it.
    static void CloseUps(Shared.Run run, IEnumerable<(string piece, Bounds box)> pieces)
    {
        List<(string piece, Bounds box)> list = pieces.ToList();
        int cameras = 0;
        foreach (BenchRig rig in Shared.InScene<BenchRig>(run))
        {
            Transform cam = rig.transform.Find("CM_InspectCam");
            if (cam == null) continue;
            cameras++;
            CinemachineCamera cm = cam.GetComponent<CinemachineCamera>();
            float fov = cm != null && cm.Lens.FieldOfView > 1f ? cm.Lens.FieldOfView : 40f;
            Matrix4x4 view = Matrix4x4.TRS(cam.position, cam.rotation, new Vector3(1f, 1f, -1f)).inverse;   // view space looks down -z (CafeRoomSteps.Frustum)
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(fov, 16f / 9f, .02f, 3f) * view);
            List<string> seen = list.Where(x => GeometryUtility.TestPlanesAABB(planes, x.box)).Select(x => x.piece).ToList();
            run.Info($"The close-up at {Shared.PathOf(cam)} (FOV {F(fov, "0")}, 16:9, to 3 m): " +
                     (seen.Count == 0 ? "no dressing piece in its frame (Room 1's rule holds)." : "IN ITS FRAME: " + string.Join(", ", seen) + " (Room 1's rule: tell Art; the TD's framings are tighter)."));
        }
        if (cameras == 0) run.Info("No bench close-up camera (CM_InspectCam under a BenchRig) found: Room 1's rule not measured.");
    }

    // ================================================================== from above (Art's B1; CameraAnglePhotos' home view)

    sealed class Overhead
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public string how;
    }

    // The overhead as Play starts it (CafeViewMode.Start): the camera's yaw and pitch, its distance from isometricFocus clamped
    // to the zoom range, its lens. Without the camera: Art's numbers (43 degrees, 34 m from (0, 0.6, 9), FOV 44).
    static Overhead HomeView(Shared.Run run)
    {
        CafeViewMode view = Shared.InScene<CafeViewMode>(run).FirstOrDefault();
        CinemachineCamera shop = view != null ? view.isometricCamera : null;
        if (view != null && shop != null)
        {
            float yaw = shop.transform.eulerAngles.y, pitch = Mathf.DeltaAngle(0f, shop.transform.eulerAngles.x);
            float distance = Mathf.Clamp(Vector3.Distance(shop.transform.position, view.isometricFocus), view.minimumDistance, view.maximumDistance);
            Quaternion angle = Quaternion.Euler(pitch, yaw, 0f);
            float fov = shop.Lens.FieldOfView > 1f ? shop.Lens.FieldOfView : 44f;
            return new Overhead
            {
                position = view.isometricFocus - angle * Vector3.forward * distance, rotation = angle, fov = fov,
                how = $"the home view: {F(pitch, "0")} degrees down, {F(distance, "0.0")} m from {V(view.isometricFocus)}, FOV {F(fov, "0")}"
            };
        }
        Quaternion art = Quaternion.Euler(43f, 0f, 0f);
        return new Overhead { position = new Vector3(0f, .6f, 9f) - art * Vector3.forward * 34f, rotation = art, fov = 44f, how = "Art's numbers (no overhead camera found): 43 degrees, 34 m, FOV 44" };
    }

    // The piece's mesh through that view at 1920 x 1080: the rectangle round its silhouette, in pixels.
    static Rect OnScreen(Overhead o, Piece x, Matrix4x4 pose)
    {
        float t = Mathf.Tan(o.fov * .5f * Mathf.Deg2Rad), aspect = ScreenW / (float)ScreenH;
        Quaternion un = Quaternion.Inverse(o.rotation);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        void Add(Vector3 world)
        {
            Vector3 c = un * (world - o.position);
            if (c.z < .1f) return;
            float sx = (c.x / (c.z * t * aspect) * .5f + .5f) * ScreenW, sy = (c.y / (c.z * t) * .5f + .5f) * ScreenH;
            x0 = Mathf.Min(x0, sx); x1 = Mathf.Max(x1, sx); y0 = Mathf.Min(y0, sy); y1 = Mathf.Max(y1, sy);
        }
        foreach (var (mesh, toRoot) in x.meshes)
        {
            Matrix4x4 m = pose * toRoot;
            Vector3[] v;
            try { v = mesh.vertices; } catch (Exception) { v = new Vector3[0]; }
            if (v.Length == 0)
            {
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                    Add(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            }
            else foreach (Vector3 local in v) Add(m.MultiplyPoint3x4(local));
        }
        return x0 > x1 ? new Rect(0f, 0f, 0f, 0f) : Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static string PixelLine(Overhead o, Piece x, Matrix4x4 pose)
    {
        Rect r = OnScreen(o, x, pose);
        float small = Mathf.Min(r.width, r.height);
        return $"{x.spec.code} {x.spec.piece}: {F(r.width, "0")} x {F(r.height, "0")} px at ({F(r.center.x, "0")}, {F(ScreenH - r.center.y, "0")}) from above" +
               (x.spec.exempt ? " (private by design: the 10-pixel rule doesn't apply)" : small >= MinPixels ? $" (reads: {F(small, "0")} px >= {MinPixels})" : $" (UNDER {MinPixels} px: {F(small, "0")})");
    }

    // ================================================================== photos (before and after, from the same places)

    static void Photos(Shared.Run run, Plan p)
    {
        Shared.GameCameraPhoto(run, "01-the-game-camera");
        Overhead o = p != null && p.view != null ? p.view : HomeView(run);
        // the back bar twice as close, from the overhead's own place: the repair side, then the café side
        Shared.Photo(run, "02-from-above-x2-repair-side", o.position, new Vector3(-4.6f, 1.1f, 15.8f), o.fov * .5f, true);
        Shared.Photo(run, "03-from-above-x2-cafe-side", o.position, new Vector3(.9f, 1.1f, 17.2f), o.fov * .5f, true);
        Shared.Photo(run, "04-ace-desk-corner-and-glass-notes", new Vector3(-4.75f, 1.6f, 15.25f), new Vector3(-3.8f, 1.05f, 14.0f), 55f);
        Shared.Photo(run, "05-pickup-row-and-whiteboard", new Vector3(-4.4f, 1.65f, 15.0f), new Vector3(-5.7f, 1.55f, 17.7f), 62f);
        Shared.Photo(run, "06-syrups-cups-and-bar-mat", new Vector3(.6f, 1.6f, 15.2f), new Vector3(.7f, 1.2f, 17.8f), 62f);
        Shared.Photo(run, "07-mortgage-letter-at-the-till", new Vector3(.15f, 1.62f, 14.8f), new Vector3(.3f, 1.1f, 14.15f), 50f);
        Shared.Photo(run, "08-final-notice-inside-the-back-door", new Vector3(6.35f, 1.6f, 16.2f), new Vector3(6.63f, 1.5f, 17.9f), 45f);
        Shared.Photo(run, "09-donor-parts-under-the-bench", new Vector3(-3.5f, .62f, 15.1f), new Vector3(-3.375f, .14f, 13.9f), 60f);
        Shared.Photo(run, "10-parts-delivery-on-the-counter", new Vector3(-5.5f, 1.7f, 12.3f), new Vector3(-6.6f, 1.2f, 13.95f), 55f);
    }

    // ================================================================== IMPORT: the pictures, the materials, the models (no scene)

    static void Import()
    {
        var report = new StringBuilder(ImportTitle + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        void Info(string what) => report.AppendLine("      " + what);
        string folder = Path.Combine(Shared.LogRoot, ImportLabel + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only: stop Play first.");
            Dictionary<string, Dictionary<string, object>> manifest = ReadManifest(out Dictionary<string, object> root, out string problem);
            if (manifest == null) throw new InvalidOperationException(problem);
            var defs = root.TryGetValue("materials", out object mo) && mo is Dictionary<string, object> md ? md : new Dictionary<string, object>();
            var existing = root.TryGetValue("existing_materials", out object eo) && eo is Dictionary<string, object> ed ? ed : new Dictionary<string, object>();
            Info($"{ManifestPath}: {manifest.Count} pieces, {defs.Count} material(s) defined, {existing.Count} existing one(s) named.");
            foreach (Spec spec in Pieces) Check(manifest.ContainsKey(spec.piece), $"{spec.piece} ({spec.code}) is in the manifest");

            // ---- the pictures (Kit 1's settings: these clamp) ----
            var configured = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<string, object> m in defs)
                {
                    string tex = m.Value is Dictionary<string, object> def ? Kit.Str(def, "texture") : null;
                    if (tex == null) continue;
                    bool there = AssetDatabase.LoadAssetAtPath<Texture2D>(tex) != null;
                    Check(there, $"{tex} is in the project ({m.Key}'s picture)");
                    if (there && Kit.ConfigureTexture(tex, false)) configured.Add(Path.GetFileName(tex));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            Info($"Pictures set up: {configured.Count} ({string.Join(", ", configured)}).");

            // ---- the materials: the manifest's new ones made Kit 1's way; another kit's, and the existing ones, by name ----
            Dictionary<string, Material> materials = Kit.ProjectMaterials();
            HashSet<string> others = OtherKitsMaterials(out string othersNote);
            Info(othersNote);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader wasn't found.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(MaterialFolder)?.Replace('\\', '/'), Path.GetFileName(MaterialFolder));
            int made = 0, updated = 0, kept = 0;
            foreach (KeyValuePair<string, object> m in defs)
            {
                if (!(m.Value is Dictionary<string, object> def)) continue;
                if (others.Contains(m.Key))
                {
                    kept++;
                    Check(materials.ContainsKey(m.Key), $"{m.Key}: another kit's material, used as it is{(materials.ContainsKey(m.Key) ? "" : " (NOT FOUND)")}");
                    continue;
                }
                string path = MaterialFolder + "/" + m.Key + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = m.Key };
                    AssetDatabase.CreateAsset(material, path);
                    made++;
                }
                else updated++;
                Kit.Define(material, def, lit, materials);
                EditorUtility.SetDirty(material);
                materials[m.Key] = material;
            }
            AssetDatabase.SaveAssets();
            Info($"Materials: {made} made and {updated} updated in {MaterialFolder}; {kept} of another kit's used as they are.");
            foreach (string name in existing.Keys) Check(materials.ContainsKey(name), $"the existing material {name} is in the project");

            // ---- the models ----
            var unmapped = new SortedSet<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (KeyValuePair<string, Dictionary<string, object>> piece in manifest)
                {
                    string path = Models + "/" + piece.Key + ".fbx";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Check(false, $"{path} is in the project"); continue; }
                    List<string> sources = piece.Value.TryGetValue("materials", out object ml) && ml is List<object> l ? l.OfType<string>().ToList() : new List<string>();
                    Kit.ConfigureModel(path, piece.Key, sources, materials, unmapped);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Check(unmapped.Count == 0, "every material in the FBX files maps to a project material" + (unmapped.Count > 0 ? " (not found: " + string.Join(", ", unmapped) + ")" : ""));

            // ---- each piece, as imported ----
            report.AppendLine();
            report.AppendLine("The pieces (metres: across, up, deep):");
            var pieces = new List<Piece>();
            int total = 0;
            foreach (Spec spec in Pieces)
            {
                Piece p = Measure(spec, manifest, out string why);
                if (p == null) { Check(false, why); continue; }
                pieces.Add(p);
                int tris = p.meshes.Sum(m => { long n = 0; for (int i = 0; i < m.mesh.subMeshCount; i++) n += m.mesh.GetIndexCount(i); return (int)(n / 3); });
                total += tris;
                int built = Mathf.RoundToInt(Kit.Num(p.entry, "tris", tris));
                int meshes = Mathf.Max(1, Mathf.RoundToInt(Kit.Num(p.entry, "meshes", 1f)));
                Kit.TryVec(p.entry, "size", out Vector3 size);
                bool whole = Mathf.Abs(tris - built) <= Mathf.Max(2, built / 200);
                bool sized = size == Vector3.zero || (p.local.size - size).magnitude < .012f;
                Check(whole && sized && p.meshes.Count == meshes,
                      $"{spec.code} {spec.piece}: {p.meshes.Count} mesh(es) ({meshes} built), {tris} triangles (built {built}), {V(p.local.size)} (built {V(size)}), " +
                      $"bottom {F(p.local.min.y)}, {p.cells.Count} cell(s) of its own volume; placed at {V(p.planned)} turned {F(Shared.Facing(p.rotation), "0")} by the manifest");
            }
            Info($"{pieces.Count} pieces, {total} triangles in all (the Blender lead: 12,510).");

            report.AppendLine();
            try { report.AppendLine(PhotographKit(folder, pieces)); }
            catch (Exception e) { Check(false, "the photos: " + e.Message); }
            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good. Next: Café dressing 1 - Dress the café (scene), with the café scene open." : failures + " check(s) failed.");
            Keep(folder, (failures == 0 ? "Imported and checked. " : failures + " check(s) FAILED. ") + folder + "\n\n" + report, failures > 0);
        }
        catch (Exception e)
        {
            Keep(folder, "The import FAILED: " + e.Message + "\n\n" + report + "\n" + e, true);
        }
    }

    static HashSet<string> OtherKitsMaterials(out string note)
    {
        var names = new HashSet<string>();
        int read = 0;
        foreach (string manifest in OtherManifests)
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Models, manifest));
            if (!File.Exists(file)) continue;
            try
            {
                if (Kit.Json.Parse(File.ReadAllText(file)) is Dictionary<string, object> root && root.TryGetValue("materials", out object mats) && mats is Dictionary<string, object> md)
                    foreach (string key in md.Keys) names.Add(key);
                read++;
            }
            catch (Exception) { }
        }
        note = $"The other kits' manifests read: {read} of {OtherManifests.Length} ({names.Count} materials of theirs, never redefined here).";
        return names;
    }

    // The kit in a preview scene (the café isn't touched): the pieces in a row on a floor, from the front and from 43 degrees.
    static string PhotographKit(string folder, List<Piece> pieces)
    {
        using (var s = new BlenderKitSteps.Studio())
        {
            s.Primitive(PrimitiveType.Cube, "Floor (photo)", new Vector3(0f, -.01f, 0f), new Vector3(30f, .02f, 6f), s.Flat(new Color(.62f, .58f, .52f)));
            float x = 0f;
            foreach (Piece p in pieces) x += p.local.size.x + .25f;
            x = -x * .5f;
            foreach (Piece p in pieces)
            {
                // facing the camera (+Z toward it at -Z: turned 180 as in the café), standing on the floor
                s.Place(p.path, new Vector3(x + p.local.size.x * .5f, -p.local.min.y, 0f), new Vector3(0f, 180f, 0f));
                x += p.local.size.x + .25f;
            }
            float span = -x * 2f;
            s.Shoot(Path.Combine(folder, "1-the-kit-from-the-front.png"), new Vector3(0f, .9f, -Mathf.Max(4f, span * .62f)), new Vector3(0f, .35f, 0f), 38f);
            float pitch = 43f * Mathf.Deg2Rad, d = Mathf.Max(5f, span * .7f);
            s.Shoot(Path.Combine(folder, "2-the-kit-from-43-degrees.png"), new Vector3(0f, Mathf.Sin(pitch) * d, -Mathf.Cos(pitch) * d), new Vector3(0f, .2f, 0f), 38f);
        }
        return $"Photos (a preview scene: the {pieces.Count} pieces in a row, turned to the room as in the café): 1-the-kit-from-the-front, 2-the-kit-from-43-degrees, in {folder}.";
    }

    static void Keep(string folder, string text, bool error)
    {
        try
        {
            Directory.CreateDirectory(Shared.LogRoot);
            File.WriteAllText(Path.Combine(Shared.LogRoot, ImportLabel + "-last.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" + text);
            if (folder != null && Directory.Exists(folder)) File.WriteAllText(Path.Combine(folder, "report.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning("[Café dressing] Couldn't write the report: " + e.Message); }
        if (error) Debug.LogError("[Café dressing] " + text); else Debug.Log("[Café dressing] " + text);
    }

    static string V(Vector3 v) => Shared.V(v);
    static string F(float f, string format = "0.000") => Shared.F(f, format);
}
#endif
