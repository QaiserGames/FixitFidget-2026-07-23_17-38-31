#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Shared = CafeRedesignShared;

// ---------------------------------------------------------------------------
// STREET 1: THE STOREFRONT'S WORDS (the café redesign, 10 Oct 2026; brief U-Street; research
// claude/studio/cafe-sf-research-and-redesign.md §5 item 4, §6.8, §6.12, §7.5; textures report §5; props report)
//
// His note (9 Oct, 20:12): "the decorations, the lines for words on the sign and or anything just looks tacky. i really
// want an indie level cafe design done." Outside, the café was "a green glass box with no name on it, one tiny table, an
// A-frame with lines" (research §5 item 8). This step puts real words on the front and takes the lines down:
//
//   off    the old sign over the door: its plate ("Dark joinery", 2.15 x 0.47) and its yellow cup handle, three rays and
//          line ("Road paint"), each alone in a merged mesh of "V3 - cafe perimeter details" (CafeStreetUpgrade.
//          V3InteriorDetails; the cup shelf was split out of them on 27 Sept; the cream cup, saucer and lettering are
//          already gone). Switched off, never deleted.
//   paint  the patio A-frame's two white chalk lines (its DC_Chalk slot) in its own chalkboard. The A-frame stays.
//   in     CK_Fascia (ACE'S CAFÉ, 32 cm caps) on the band over the door, 5 cm over the door head, turned to the street;
//          CK_WindowGilding_L / _R (gold leaf: "Ace's Café", "Fixed while you wait") on the room side of the glass of
//          the pane either side of the door;
//          CK_BladeSign (ACE'S, both faces) on the west facade by the front corner, standing out over the pavement;
//          CK_AFrameFace (the chalk words) on each of the A-frame's boards, 1 mm proud;
//          CK_EntryMosaic (ACE'S) on the floor just inside the door;
//          CK_SidewalkBench against the west bulkhead, with a box collider (solid by day and by night).
//
// THE STOREFRONT IS MEASURED, NOT ASSUMED. Its glass, frames, door and band live inside two FBX models (CafeJoinery.fbx
// as "Layered windows, open doors and woven textiles", EntranceRefresh.fbx as "Entrance with rounded brass pulls"); the
// scene's YAML holds only their overrides, and the old box stand-ins in "01 - room and windows" are hidden. So the survey
// casts rays at the meshes themselves (readable in Edit Mode). A ray at the front counts only what lies in the front's
// own plane (z -0.40 to +0.05) on a face turned toward it: the door leaves held open on the pavement, the planters, the
// A-frame, the street's cars and walkers and, behind the glass, the room (Room 1's olive trees by the door) don't count.
// What the 9 Oct photos show, which the rays confirm or refuse on the Mac:
//   * the front has four panes a side of about 1.44 m clear, not the 3.1 m the gilding was first drawn for: it was redrawn
//     (10 Oct) as a 1.30 x 0.80 m piece for one pane, so it goes in at full size with 5 cm margins (scaled down only if a
//     pane measures narrower), its centre at 1.70 m;
//   * the front is not a cut-away wall. CafeViewMode.cutawayWalls holds the back plaster, the right plaster (not drawn)
//     and the tan rear wall (x -7.6, z 12.7 to 18.1); the front is glass and frames and stays drawn from every angle, so
//     the signs on it stay drawn with it. There is nothing to register, and the cut-away lab is unchanged (32/32);
//   * the west planter (Patio - Planter_01, x about -4.2 to -2.0) stands where the brief put the bench, so the bench goes
//     in the gap between the planter and the patio's west edge (x -7.51), centred;
//   * the blade sign's bracket reaches 0.904 m above the disc's bottom and must be fixed to solid frame below the
//     facade's top (about 3.2 m, no parapet): its height and its place along the facade come from the rays.
//
//   Fixit Fidget > Cafe redesign > Street 1 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Street 1 1 - Put the words up (scene)
//   Fixit Fidget > Cafe redesign > Street 1 1 - Undo (scene)
//
// Everything runs through CafeRedesignShared.Step: the survey refuses with its reasons and changes nothing; one undo
// group and the record; the checks after (any FAIL puts everything back); the routes re-baked and the guards before and
// after; photos before and after; never saved. Report and photos: Logs/CafeRedesign/street-1-<time>/.
// ---------------------------------------------------------------------------
internal static class CafeStreetSteps
{
    public const string Title = "Street 1 - The storefront's words";
    const string Label = "street-1";
    const string GroupName = "Street 1 - the storefront's words";
    const string BarStep = "Bar 1 - The front counter";
    const string MenuCheck = Shared.Menu + "Street 1 0 - Check first (read-only)";
    const string MenuDo = Shared.Menu + "Street 1 1 - Put the words up (scene)";
    const string MenuUndo = Shared.Menu + "Street 1 1 - Undo (scene)";

    // ================================================================== menus

    [MenuItem(MenuCheck)]
    static void CheckFirst() => Shared.Check(Title, Label + "-check", Survey);

    [MenuItem(MenuDo)]
    static void PutTheWordsUp() => Shared.Step(Title, Label, Survey, Change, Photos);

    [MenuItem(MenuUndo)]
    static void TakeTheWordsDown() => Shared.UndoStep(Title, Label);

    [MenuItem(MenuCheck, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== where things are (paths under the café's root, 216cc1a)

    const string DetailsPath = "09 - neighborhood surrounds V3/V3 - cafe perimeter details";
    const string OldMarksName = "Road paint";      // the old sign's cup handle, rays and lower line
    const string OldPlateName = "Dark joinery";    // the old sign plate: alone in its merged mesh since the cup shelf got its own
    const string EntranceName = "Entrance with rounded brass pulls";
    const string AFramePath = "18 - cafe furnishing/D - Entrance and waiting nook/Patio A-frame";
    const string PlanterName = "Patio - Planter_01";
    const string PatioSetPath = "08 - front patio bistro nook";
    const string BoundariesPath = "01 - room and windows/Window collision boundaries";
    const string DoorLeavesPath = "12 - authored cafe interior/Open door collision";

    // The old sign as V3InteriorDetails built it: a board at (0, 2.85, -0.285), 2.15 x 0.47 x 0.09, its marks 5 cm in
    // front. A box round all of it with a few cm to spare: what lies wholly inside it is the old sign.
    static readonly Bounds OldSignZone = MinMax(new Vector3(-1.13f, 2.58f, -.38f), new Vector3(1.13f, 3.12f, -.22f));

    // Everything the rays look at: the front, the patio, the corner, the floor inside the door (the meshes there are read once).
    static readonly Bounds Roi = MinMax(new Vector3(-11f, -.3f, -2.8f), new Vector3(11f, 4.2f, 1.8f));

    // The front's own plane: from a cornice's or a sill's reach onto the pavement to the wall's back (its collider runs
    // z -0.21 to -0.03). Rays at the front count only hits in it, on faces within 60° of square to the ray.
    const float FrontFrom = -.40f, FrontTo = .05f, Facing = .5f;
    const float StreetZ = -1.6f, FrontReach = 2.0f;   // front rays start on the patio and look north
    const float InsideZ = .8f;                         // rays at the glass from the room start here and look south
    const float EyeY = 1.70f;                          // the panes are measured across here: where the gilding is read
    const float BehindGlass = .03f;                    // a frame member this close behind glass is a mullion seen through it
    // The west facade: rays start 0.9 m out from its collider's outer side and count hits from 0.25 m out to 0.30 m in.
    const float WestOut = .9f, WestReach = 1.5f, WestBandOut = .25f, WestBandIn = .30f;

    // ---- the pieces' rules (each with its reason) ----
    // The gilding's centre: the brief's bottom at 1.30 on its 0.80 m height, i.e. the research's "about 1.7 m up". When it
    // must shrink to a 1.5 m pane, the centre stays at eye height (its bottom rises) rather than the bottom staying put.
    const float GildingCentreY = 1.70f;
    const float GildingMargin = .05f;    // each side of the pane (the brief)
    const float GildingOffGlass = .002f; // its street face this far on the room side of the glass's room face (the brief: 2 mm)
    const float GildingSmallest = .30f;  // below this its words wouldn't read: refuse, it needs redrawing
    const float DisplayPane = .6f;       // a glass run narrower than this beside the door is a side light, not a display pane
    const float FasciaAboveHead = .05f;  // the brief: its bottom about 5 cm above the door head
    const float FasciaOffBand = .002f;   // its back this far in front of the frontmost thing it covers
    const float FasciaBridge = .06f;     // it may stand this far proud of the band's middle (held by the frame at its ends)
    const float FasciaBearing = .03f;    // what it is held by: within this of its back, on both sides
    const float FasciaSmallest = .70f;   // the brief allows scaling to fit; below this, refuse (32 cm caps would be 22)
    const float HeadTallest = .30f;      // a door head member taller than this is the band itself, flush with the head
    const float BladeBottomWanted = 2.50f;   // the research: the disc's bottom at 2.5 m
    const float BladeLowest = 2.20f;         // its lowest point at least this high: Ace's capsule is 2.0 m tall
    const float BladeZFrom = .60f, BladeZTo = 1.00f, BladeZBest = .80f;   // the brief: z about 0.6 to 1.0
    const float BladeOffWall = .001f;
    const float CarClear = 1.2f;             // a car's path to the sign's footprint: half a car's width and a margin
    const float WalkerClear = .6f;           // a street walker's path to the bench: half a person and a margin
    const float FaceInset = .005f, FaceProud = .001f;   // on the A-frame's board: inside its edges, 1 mm proud (the brief)
    const float MosaicLift = .0005f;         // its bottom half a millimetre over the floor (never in its plane); its top 2.5 mm proud
    const float MosaicProbeY = .25f;
    const float BenchOffFence = .06f, BenchGapToPlanter = .25f, BenchOffWall = .02f;
    const float NightListBelow = 2.4f;       // NightCollisionList.Below: meshes lower than this are solid by night

    // ================================================================== the kit (the manifests, in Unity axes about each origin)

    sealed class Kit
    {
        public readonly string piece;
        public readonly Vector3 min, max;
        public Kit(string piece, Vector3 min, Vector3 max) { this.piece = piece; this.min = min; this.max = max; }
        public Vector3 Size => max - min;
    }

    // textures/fbx/cafe_signs.json and the props report. Wall pieces: origin at the bottom centre of the back, the back at
    // local z 0, the reading face toward local +z. The blade sign: origin on the wall face at the disc's bottom, standing
    // out along local -x. The mosaic and the bench: bottom centre.
    static readonly Kit Fascia = new Kit("CK_Fascia", new Vector3(-1.32f, 0f, 0f), new Vector3(1.32f, .578f, .062f));
    static readonly Kit GildingL = new Kit("CK_WindowGilding_L", new Vector3(-.65f, 0f, 0f), new Vector3(.65f, .8f, .002f));
    static readonly Kit GildingR = new Kit("CK_WindowGilding_R", new Vector3(-.65f, 0f, 0f), new Vector3(.65f, .8f, .002f));
    static readonly Kit Blade = new Kit("CK_BladeSign", new Vector3(-1f, -.004f, -.04f), new Vector3(0f, .9f, .04f));
    static readonly Kit Face = new Kit("CK_AFrameFace", new Vector3(-.3f, 0f, 0f), new Vector3(.3f, .9f, .003f));
    static readonly Kit Mosaic = new Kit("CK_EntryMosaic", new Vector3(-1.2f, 0f, -.6f), new Vector3(1.2f, .002f, .6f));
    static readonly Kit Bench = new Kit("CK_SidewalkBench", new Vector3(-1.2f, 0f, -.22f), new Vector3(1.2f, .453f, .22f));
    static readonly Kit[] Kits = { Fascia, GildingL, GildingR, Blade, Face, Mosaic, Bench };
    const float KitSlack = .006f;    // bevels and FBX rounding

    // ================================================================== the plan (the survey fills it; the change and the photos use it)

    sealed class Placement
    {
        public Kit kit;
        public string name, why;
        public Vector3 origin;
        public Quaternion rotation = Quaternion.identity;
        public float scale = 1f;
        public GameObject placed;
        public Bounds Expected => WorldBox(kit.min, kit.max, origin, rotation, scale);
        public Renderer Renderer => placed != null ? placed.GetComponentInChildren<Renderer>(true) : null;
    }

    sealed class Pane
    {
        public string side;
        public float x0, x1, y0, y1, zStreet, zRoom;
        public float Width => x1 - x0;
        public float Centre => (x0 + x1) * .5f;
    }

    // openTop: the doorway's top; headTop: the door head's top; bandTop: where the band over it ends (under anything that
    // stands proud of the front's frame, a cornice or a rail, or the front's top); top: the front's top.
    sealed class DoorHead { public float openTop, headTop, bandTop, top; public string head, band; }

    sealed class Board
    {
        public Vector3 centre, normal, up, right;
        public float width, height, proud;
    }

    // What lies behind the fascia's face: a grid of rays from the street (or, after, from just behind its back).
    sealed class Backing
    {
        public readonly List<(float x, float y, Visual.Hit hit)> samples = new List<(float, float, Visual.Hit)>();
        public float half;   // the board's half width
        public IEnumerable<(float x, float y, Visual.Hit hit)> Middle => samples.Where(s => Mathf.Abs(s.x) <= half * .76f + 1e-4f);
        public IEnumerable<(float x, float y, Visual.Hit hit)> Ends => samples.Where(s => Mathf.Abs(s.x) > half * .76f + 1e-4f);
        public List<string> Holes => Middle.Where(s => !s.hit.ok).Select(s => $"({F(s.x, "0.00")}, {F(s.y, "0.00")})").ToList();
        public int Held(float back, int side) =>
            samples.Count(s => s.hit.ok && Mathf.Sign(s.x) == side && Mathf.Abs(s.x) >= half * .25f && s.hit.point.z - back <= FasciaBearing + .002f);
    }

    sealed class Plan
    {
        public Visual visual;
        public Transform player;
        public readonly HashSet<Renderer> movers = new HashSet<Renderer>();
        public readonly HashSet<Renderer> entrance = new HashSet<Renderer>();
        public readonly List<(GameObject go, string why)> off = new List<(GameObject, string)>();
        public readonly HashSet<Renderer> offRenderers = new HashSet<Renderer>();
        public Pane west, east;
        public DoorHead door;
        public readonly List<float> frameHits = new List<float>();   // the front's frame members at eye height: their street faces' z
        public float FrameZ => frameHits.Count > 0 ? frameHits.OrderBy(z => z).ElementAt(frameHits.Count / 2) : -.21f;
        public float westFace, bladeFace, floorY, bulkheadZ;
        public Renderer floor;
        public Placement fascia, gildingL, gildingR, blade, mosaic, bench;
        public readonly List<Placement> faces = new List<Placement>();
        public readonly List<Board> boards = new List<Board>();
        public MeshRenderer aFrame;
        public int boardSlot = -1, chalkSlot = -1;
        public Vector3 aFrameAt;
        public Quaternion aFrameTurn;
        public Bounds planter;
        public readonly List<Collider> patioSet = new List<Collider>();
        public readonly List<Bounds> doorLeaves = new List<Bounds>();
        public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        public readonly HashSet<Collider> lane = new HashSet<Collider>();

        public IEnumerable<Placement> Pieces => new[] { fascia, gildingL, gildingR, blade, mosaic, bench }.Concat(faces).Where(x => x != null);
        // Ace and the street's cars and walkers: never part of what the rays measure.
        public bool SkipMoving(Renderer r) => r == null || player != null && r.transform.IsChildOf(player) || movers.Contains(r);
        // What goes off doesn't count as anything's backing or floor either.
        public bool SkipOld(Renderer r) => SkipMoving(r) || offRenderers.Contains(r);
    }

    static Plan plan;

    // ================================================================== the survey (changes nothing; refuses with the reasons)

    static void Survey(Shared.Run run)
    {
        var p = plan = new Plan { player = run.Player };

        // ---- what comes first ----
        run.Require(Shared.Done(run, BarStep), $"'{BarStep}' hasn't run in this scene: Street 1 comes after it (brief U-Street).");
        List<string> steps = Shared.Records(run.Cafe).OrderBy(r => r.order).Select(r => $"{r.order}. {r.step}").ToList();
        run.Info("The redesign's steps already in this scene: " + (steps.Count > 0 ? string.Join("; ", steps) : "none") +
                 (Shared.RepairBarDone(run) ? "; the repair bar (its own record)" : "") + ".");

        // ---- the kit: imported upright, at scale 1, about the manifests' origins ----
        foreach (Kit k in Kits) KitReady(run, k);
        foreach (string name in new[] { "CF_GildingL", "CF_GildingR" }) GildingMaterial(run, name);

        // ---- the old sign over the door ----
        OldSign(run, p);

        // ---- the meshes at the front, read once (the joinery and the entrance live inside FBX models) ----
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
                if (a != null && a.actor != null)
                    foreach (Renderer r in a.actor.GetComponentsInChildren<Renderer>(true)) p.movers.Add(r);
        p.visual = new Visual(Shared.InScene<MeshRenderer>(run, false).Where(r => !p.SkipMoving(r)), Roi);
        run.Info($"Read {p.visual.Renderers} renderers' meshes at the front: {p.visual.Triangles:N0} triangles in x {F(Roi.min.x, "0.0")}..{F(Roi.max.x, "0.0")}, " +
                 $"z {F(Roi.min.z, "0.0")}..{F(Roi.max.z, "0.0")} (the street's {p.movers.Count} moving renderers left out)" +
                 (p.visual.unreadable.Count > 0 ? "; couldn't read " + string.Join(", ", p.visual.unreadable.Take(6)) : "") + ".");
        Transform entrance = Shared.Named(run, EntranceName, false).FirstOrDefault();
        run.Require(entrance != null, $"'{EntranceName}' (the café's doors, EntranceRefresh.fbx) isn't under the café's root, or it is switched off.");
        foreach (Renderer r in entrance.GetComponentsInChildren<Renderer>(false)) p.entrance.Add(r);

        // ---- each placement, measured ----
        PlanGildings(run, p);
        PlanFascia(run, p);
        PlanBlade(run, p);
        PlanAFrame(run, p);
        PlanMosaic(run, p);
        PlanBench(run, p);

        // ---- what it leaves to the rest of the game ----
        CutawayFacts(run, p, false);
        foreach (KeyValuePair<Transform, Vector3> a in Anchors(run)) p.anchors[a.Key] = a.Value;
        foreach (Collider c in LaneColliders()) p.lane.Add(c);
        run.Info($"Gameplay anchors noted (checked after, 1 mm): {p.anchors.Count}; colliders in the door lane (x -1..1, z 0..2): {p.lane.Count}.");

        // ---- the plan ----
        run.Line();
        run.Line("Where everything goes (world metres; facing = the heading of the piece's front, degrees clockwise from north):");
        foreach (Placement x in p.Pieces)
            run.Info($"{x.name}: origin {Shared.V(x.origin)}, facing {F(Shared.Facing(x.rotation), "0")}, scale {F(x.scale)}; it fills {Shared.BoundsText(x.Expected)}. {x.why}");
        foreach ((GameObject go, string why) in p.off) run.Info($"Off: {Shared.PathOf(go.transform)}: {why}.");
        if (p.aFrame != null)
            run.Info($"Repainted: {Shared.PathOf(p.aFrame.transform)}'s slot {p.chalkSlot} ({Name(p.aFrame.sharedMaterials[p.chalkSlot])}, the two white lines) " +
                     $"becomes its slot {p.boardSlot} ({Name(p.aFrame.sharedMaterials[p.boardSlot])}). The A-frame doesn't move.");
        NightFacts(run, p, false);
    }

    // Place() gives a piece's root its own turn and scale, so the mesh must already stand upright, at scale 1, about the
    // root's origin, as the manifest says. A piece Kit 1 imported lying down or at scale 100 would land wrong: refuse.
    static void KitReady(Shared.Run run, Kit k)
    {
        string path = Shared.KitModels + "/" + k.piece + ".fbx";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { run.Check(false, $"{path} isn't imported: run Fixit Fidget > Cafe redesign > Kit 1 first."); return; }
        MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        if (filters.Length == 0) { run.Check(false, $"{path} has no mesh."); return; }
        Bounds b = Transformed(filters[0].sharedMesh.bounds, asset.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix);
        foreach (MeshFilter f in filters.Skip(1))
            b.Encapsulate(Transformed(f.sharedMesh.bounds, asset.transform.worldToLocalMatrix * f.transform.localToWorldMatrix));
        bool ok = Near(b.min, k.min, KitSlack) && Near(b.max, k.max, KitSlack);
        run.Check(ok, $"{k.piece} as imported spans {Shared.BoundsText(b)} about its origin ({filters.Length} mesh(es); the manifest: {Shared.BoundsText(MinMax(k.min, k.max))})" +
                      (ok ? "" : ": Kit 1 imported it turned, scaled or off its origin, so it would land wrong. Re-import it upright at scale 1, then run this again."));
    }

    // The gilding's clear parts must stay clear: alpha-clipped (unity-common; the textures report: 0.4) or transparent.
    static void GildingMaterial(Shared.Run run, string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Shared.KitMaterials + "/" + name + ".mat");
        if (m == null) { run.Info($"{name}.mat isn't in {Shared.KitMaterials} yet (Kit 1 makes it); the piece wears whatever Kit 1 mapped."); return; }
        bool clip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > .5f;
        bool blend = m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f;
        run.Check(clip || blend, clip ? $"{name} is alpha-clipped: only the gold shows on the glass"
                               : blend ? $"{name} is transparent (it works; alpha clipping at 0.4, as the textures report recommends, sorts better against the window glass)"
                                       : $"{name} is neither alpha-clipped nor transparent: the gilding's clear parts would show as a solid slab. Kit 1 makes it alpha-clipped.");
    }

    // The old sign: its plate and its marks, each alone in a merged mesh. Refuse if a mesh holds more than the sign
    // (switching it off would take the rest with it), and if anything else lies wholly where the sign is (it would float
    // once the plate goes).
    static void OldSign(Shared.Run run, Plan p)
    {
        Transform details = Shared.Find(run, DetailsPath);
        if (details == null) run.Info($"No {DetailsPath}: the old sign over the door isn't there to take down.");
        else
            foreach ((string name, string what) in new[]
                     {
                         (OldMarksName, "the old sign's yellow cup handle, three rays and line (\"a yellow square and a line\", research §5 item 4)"),
                         (OldPlateName, "the old sign plate over the door (2.15 x 0.47, dark joinery)"),
                     })
            {
                Transform t = details.Find(name);
                if (t == null) { run.Info($"{DetailsPath}/{name}: not there (already gone)."); continue; }
                if (!t.gameObject.activeInHierarchy) { run.Info($"{Shared.PathOf(t)}: already off."); continue; }
                Renderer r = t.GetComponent<Renderer>();
                if (r == null || !r.enabled) { run.Info($"{Shared.PathOf(t)}: draws nothing."); continue; }
                bool sign = Within(r.bounds, OldSignZone);
                run.Check(sign, sign
                    ? $"{Shared.PathOf(t)} lies wholly over the door ({Shared.BoundsText(r.bounds)}): {what}"
                    : $"{Shared.PathOf(t)} reaches beyond the old sign ({Shared.BoundsText(r.bounds)}; the sign's box {Shared.BoundsText(OldSignZone)}): its merged mesh holds " +
                      "more than the sign, and switching it off would take that with it. Nothing was changed: cut the sign out of it first (CafeWallSteps' way), then run this again.");
                if (!sign) continue;
                p.off.Add((t.gameObject, what + "; CK_Fascia takes its place"));
                p.offRenderers.Add(r);
            }
        foreach (Renderer r in Shared.InScene<Renderer>(run, false))
            if (r.enabled && !p.offRenderers.Contains(r) && Within(r.bounds, OldSignZone))
                run.Check(false, $"{Shared.PathOf(r.transform)} ({Shared.BoundsText(r.bounds)}) lies on the old sign over the door and would float once the plate goes. Nothing was changed: find out what it is first.");
    }

    // ---------------------------------------------------------------- rays at the front and at the west facade

    static bool InFront(Vector3 q) => q.z >= FrontFrom && q.z <= FrontTo;

    // From the street, north: the first thing in the front's own plane.
    static Visual.Hit FrontRay(Visual v, Plan p, Vector3 from, bool glass = true, bool opaque = true, Func<Renderer, bool> skip = null) =>
        v.Ray(from, Vector3.forward, FrontReach, skip ?? p.SkipOld, glass, opaque, where: InFront, facing: Facing);

    // A sample of the front: glass ('g'), frame ('f': opaque first, or a frame member just behind the glass), or nothing in
    // the front's plane ('-': the doorway, the sky over the café); with what was hit.
    static (char what, Visual.Hit hit) FrontAt(Plan p, Vector3 from)
    {
        Visual.Hit any = FrontRay(p.visual, p, from);
        if (!any.ok) return ('-', any);
        if (!any.glass) return ('f', any);
        Visual.Hit solid = FrontRay(p.visual, p, from, glass: false);
        return solid.ok && solid.distance - any.distance <= BehindGlass ? ('f', solid) : ('g', any);
    }

    // From the pavement west of the café, east: the first thing in the west facade's own plane.
    static Visual.Hit WestRay(Plan p, Vector3 at, bool glass = true, Func<Renderer, bool> skip = null) =>
        p.visual.Ray(new Vector3(p.westFace - WestOut, at.y, at.z), Vector3.right, WestReach, skip ?? p.SkipOld, glass, true,
                     where: q => q.x >= p.westFace - WestBandOut && q.x <= p.westFace + WestBandIn, facing: Facing);

    // ---------------------------------------------------------------- the gilding: the pane either side of the door

    static void PlanGildings(Shared.Run run, Plan p)
    {
        p.west = MeasurePane(run, p, -1);
        p.east = MeasurePane(run, p, +1);
        if (p.west != null) p.gildingL = Gilding(run, p.west, GildingL, "CK_WindowGilding_L (west window by the door)");
        if (p.east != null) p.gildingR = Gilding(run, p.east, GildingR, "CK_WindowGilding_R (east window by the door)");
    }

    static Pane MeasurePane(Shared.Run run, Plan p, int side)
    {
        string where = side < 0 ? "west" : "east";
        var across = new List<(float at, char what)>();
        for (int i = 0; ; i++)
        {
            float x = side * (1.02f + i * .01f);
            if (Mathf.Abs(x) > 7.40f) break;
            (char what, Visual.Hit hit) at = FrontAt(p, new Vector3(x, EyeY, StreetZ));
            across.Add((x, at.what));
            if (at.what == 'f') p.frameHits.Add(at.hit.point.z);
        }
        run.Info($"The {where} front at {F(EyeY, "0.00")} m, from the door out (g glass, f frame, - nothing): {Profile(across)}.");
        List<(float from, float to)> runs = Runs(across, 'g');
        // The display pane nearest the door: the first glass at least DisplayPane wide (a narrow side light doesn't count).
        int index = runs.FindIndex(r => Mathf.Abs(r.to - r.from) + .01f >= DisplayPane);
        if (index < 0)
        {
            run.Check(false, $"There's no glass {F(DisplayPane, "0.0")} m wide or more in the {where} front at {F(EyeY, "0.00")} m: the storefront isn't the one this step measured, so there's no pane to gild.");
            return null;
        }
        if (index > 0) run.Info($"Passed over {index} narrower glass run(s) by the door on the {where} side (side lights, not display panes).");
        (float a, float b) = runs[index];
        var pane = new Pane { side = where, x0 = Mathf.Min(a, b) - .005f, x1 = Mathf.Max(a, b) + .005f };

        var up = new List<(float at, char what)>();
        for (int i = 0; i <= 300; i++) { float y = .5f + i * .01f; up.Add((y, FrontAt(p, new Vector3(pane.Centre, y, StreetZ)).what)); }
        (float from, float to) tall = Runs(up, 'g').FirstOrDefault(r => Mathf.Min(r.from, r.to) <= EyeY + 1e-4f && Mathf.Max(r.from, r.to) >= EyeY - 1e-4f);
        if (tall.from == 0f && tall.to == 0f)
        {
            run.Check(false, $"The {where} pane by the door has no glass up its middle at {F(EyeY, "0.00")} m ({Profile(up)}).");
            return null;
        }
        pane.y0 = Mathf.Min(tall.from, tall.to) - .005f;
        pane.y1 = Mathf.Max(tall.from, tall.to) + .005f;
        run.Info($"Up the middle of the {where} pane (x {F(pane.Centre)}), from 0.50 m: {Profile(up)}.");

        // The glass's two faces, from the street and from the room (only glass counts), and that they are flat and square
        // to the street across the pane: the gilding lies 2 mm off the room face over all of it.
        var street = new List<float>();
        var room = new List<float>();
        foreach (float fx in new[] { .1f, .5f, .9f })
            foreach (float y in new[] { pane.y0 + .25f, EyeY, pane.y1 - .25f })
            {
                float x = Mathf.Lerp(pane.x0 + .05f, pane.x1 - .05f, fx);
                Visual.Hit s = FrontRay(p.visual, p, new Vector3(x, y, StreetZ), opaque: false);
                Visual.Hit r = p.visual.Ray(new Vector3(x, y, InsideZ), Vector3.back, InsideZ - StreetZ, p.SkipOld, true, false, where: InFront, facing: Facing);
                if (s.ok) street.Add(s.point.z);
                if (r.ok) room.Add(r.point.z);
            }
        if (street.Count < 9 || room.Count < 9)
        {
            run.Check(false, $"The {where} pane's glass couldn't be found from both sides at all 9 points (street {street.Count}, room {room.Count}).");
            return null;
        }
        pane.zStreet = street.Min();
        pane.zRoom = room.Max();
        float spread = Mathf.Max(street.Max() - street.Min(), room.Max() - room.Min());
        bool flat = spread <= .003f && pane.zRoom >= pane.zStreet - .0005f;
        run.Check(flat,
            $"the {where} display pane nearest the door: x {F(pane.x0)}..{F(pane.x1)} ({F(pane.Width)} m clear), glass {F(pane.y0)} to {F(pane.y1)} m up; its street face at z {F(pane.zStreet)}, " +
            $"its room face at z {F(pane.zRoom)} ({F((pane.zRoom - pane.zStreet) * 1000f, "0.0")} mm), flat to {F(spread * 1000f, "0.0")} mm" +
            (flat ? "" : ": not flat or not square to the street, so the gilding can't lie 2 mm off it everywhere"));
        return flat ? pane : null;
    }

    static Placement Gilding(Shared.Run run, Pane pane, Kit kit, string name)
    {
        float w = kit.Size.x, h = kit.Size.y;
        float s = Mathf.Min(1f, (pane.Width - 2f * GildingMargin) / w);
        s = Mathf.Min(s, (pane.y1 - pane.y0 - 2f * GildingMargin) / h);
        if (s < GildingSmallest)
        {
            run.Check(false, $"{name}: its pane is {F(pane.Width)} x {F(pane.y1 - pane.y0)} m clear, so the 1.30 x 0.8 m gilding would shrink to {F(s)} and its words " +
                             "wouldn't read. Nothing was changed: the gilding needs redrawing for this pane.");
            return null;
        }
        float centre = Mathf.Clamp(GildingCentreY, pane.y0 + GildingMargin + h * s * .5f, pane.y1 - GildingMargin - h * s * .5f);
        float streetFace = pane.zRoom + GildingOffGlass;
        return new Placement
        {
            kit = kit, name = name, scale = s,
            rotation = Quaternion.Euler(0f, 180f, 0f),
            // Turned to the street, the leaf runs from origin.z - 0.002 s (its street face, the one that reads) to origin.z.
            origin = new Vector3(pane.Centre, centre - h * s * .5f, streetFace + kit.max.z * s),
            why = $"Gold leaf on the room side of the glass, its street face {F(GildingOffGlass * 1000f, "0")} mm off the glass's room face: real gilding is painted on the inside " +
                  "face and read through the glass (from inside it reads reversed, as the real thing does), and 2 mm apart the two never fight. " +
                  (s < 1f ? $"The pane is {F(pane.Width)} m clear, so the 1.30 m piece is scaled uniformly to {F(s)} with {F(GildingMargin * 100f, "0")} cm margins (the brief's rule): " +
                            $"its 30 cm capitals become {F(30f * s, "0")} cm. " : "") +
                  $"Its centre at {F(centre, "0.00")} m (the research's \"about 1.7 m up\").",
        };
    }

    // ---------------------------------------------------------------- the fascia: the band over the door

    // What each height meets over the door, from the street: the door's own frame ('e', EntranceRefresh), the front's frame
    // or wall ('w'), glass ('g'), or nothing in the front's plane ('-': the doorway below, the sky above). Three lines.
    static DoorHead MeasureDoor(Shared.Run run, Plan p)
    {
        DoorHead d = null;
        foreach (float x in new[] { 0f, -.6f, .6f })
        {
            var up = new List<(float at, char what, Visual.Hit hit)>();
            for (int i = 0; i <= 380; i++)
            {
                float y = 1.9f + i * .005f;
                Visual.Hit hit = FrontRay(p.visual, p, new Vector3(x, y, StreetZ));
                up.Add((y, !hit.ok ? '-' : hit.glass ? 'g' : p.entrance.Contains(hit.renderer) ? 'e' : 'w', hit));
            }
            run.Info($"Over the door at x {F(x, "0.0")}, from 1.90 m up (e the door's frame, w the front's frame or wall, g glass, - nothing): {Profile(up.Select(u => (u.at, u.what)).ToList())}.");
            int first = up.FindIndex(u => u.what != '-'), last = up.FindLastIndex(u => u.what != '-');
            if (first < 0)
            {
                run.Check(false, $"Nothing over the door at x {F(x, "0.0")} between 1.90 and 3.80 m: the door isn't where this step expects it.");
                return null;
            }
            // The head: the first member met over the opening, one surface (the same part, the same depth to 5 mm), at most
            // HeadTallest tall. Taller than that, it is the band itself, flush with the head: the opening's top is the edge.
            Visual.Hit h0 = up[first].hit;
            int k = first;
            while (k + 1 < up.Count && up[k + 1].what == up[first].what && up[k + 1].hit.renderer == h0.renderer && up[k + 1].hit.material == h0.material &&
                   Mathf.Abs(up[k + 1].hit.point.z - h0.point.z) <= .005f)
                k++;
            float open = up[first].at;
            bool flush = up[k].at - open > HeadTallest;
            float headTop = flush ? open : up[k].at;
            string head = (up[first].what == 'e' ? "the door's own frame" : up[first].what == 'g' ? "glass" : "the front's frame") +
                          (flush ? $", flush with the band (one surface from {F(open, "0.00")} to {F(up[k].at, "0.00")} m)" : "");
            float top = up[last].at;
            // The band ends under the first thing over the head that stands 4 cm or more proud of the front's frame (a
            // cornice, a rail): the fascia hangs below it, never in front of it.
            int proud = up.FindIndex(u => u.at >= headTop + FasciaAboveHead && u.hit.ok && u.hit.point.z < p.FrameZ - .04f);
            float bandTop = proud >= 0 ? up[proud].at - .005f : top;
            string band = proud >= 0 ? $"{Shared.PathOf(up[proud].hit.renderer.transform)} stands {F((p.FrameZ - up[proud].hit.point.z) * 100f, "0")} cm proud of the frame from {F(up[proud].at, "0.00")} m"
                                     : "nothing proud of the frame over it";
            if (d == null) d = new DoorHead { openTop = open, headTop = headTop, bandTop = bandTop, top = top, head = head, band = band };
            else
            {
                d.openTop = Mathf.Min(d.openTop, open);
                if (headTop > d.headTop) { d.headTop = headTop; d.head = head; }
                if (bandTop < d.bandTop) { d.bandTop = bandTop; d.band = band; }
                d.top = Mathf.Min(d.top, top);
            }
        }
        bool ok = d.headTop > 2.2f && d.headTop < 3.0f && d.bandTop > d.headTop + .3f;
        run.Check(ok, $"the door: its opening up to {F(d.openTop)} m, its head's top at {F(d.headTop)} m ({d.head}); the band over it up to {F(d.bandTop)} m ({d.band}; " +
                      $"the front's frame at z {F(p.FrameZ)}), the front's top at {F(d.top)} m" + (ok ? "" : ": not a door head with a band over it, as this step expects"));
        return ok ? d : null;
    }

    // The grid behind the fascia's face, 27 lines across by 3 heights. Before: from the street (the old sign doesn't count).
    // After: from just behind its back, the fascia itself not counting.
    static Backing BackingOf(Visual v, float s, float bottom, float fromZ, Func<Renderer, bool> skip)
    {
        var b = new Backing { half = Fascia.max.x * s };
        for (int i = -13; i <= 13; i++)
            foreach (float fy in new[] { .06f, .5f, .94f })
            {
                float x = i * .1f * s, y = bottom + Fascia.Size.y * s * fy;
                b.samples.Add((x, y, v.Ray(new Vector3(x, y, fromZ), Vector3.forward, FrontTo - fromZ, skip, where: InFront, facing: Facing)));
            }
        return b;
    }

    static void PlanFascia(Shared.Run run, Plan p)
    {
        DoorHead d = p.door = MeasureDoor(run, p);
        if (d == null) return;
        float bottom = d.headTop + FasciaAboveHead;
        float s = Mathf.Min(1f, (d.bandTop - bottom) / Fascia.Size.y);
        string fit = s < 1f ? $"The band from {F(bottom)} to {F(d.bandTop)} m is {F(d.bandTop - bottom)} m tall, so the 0.578 m board is scaled uniformly to {F(s)} (the brief allows it). " : "";

        // Its back: in front of the frontmost thing it covers, when the frame at its ends stands no more than FasciaBridge
        // proud of the band's middle (a board fixed across the band, held at its ends). Prouder than that, it narrows to
        // hang on the band between them (uniformly smaller), never cutting into the frame; then it is measured again.
        Backing g = null;
        float back = 0f;
        bool settled = false;
        for (int pass = 0; pass < 3 && s >= FasciaSmallest; pass++)
        {
            g = BackingOf(p.visual, s, bottom, StreetZ, p.SkipOld);
            List<string> holes = g.Holes;
            if (holes.Count > 0)
            {
                run.Check(false, $"There's nothing in the front's plane behind the fascia's middle at {string.Join(", ", holes.Take(8))}: it would float over the door. Nothing was changed.");
                return;
            }
            float mid = g.Middle.Min(m => m.hit.point.z);
            float all = g.samples.Where(m => m.hit.ok).Min(m => m.hit.point.z);
            if (mid - all <= FasciaBridge + 1e-4f) { back = all - FasciaOffBand; settled = true; break; }
            float half = g.Ends.Where(m => m.hit.ok && m.hit.point.z < mid - .001f).Min(m => Mathf.Abs(m.x)) - .05f;
            fit += $"The frame at its ends stands {F((mid - all) * 100f, "0")} cm proud of the band's middle, more than {F(FasciaBridge * 100f, "0")}: it narrows to {F(half * 2f)} m " +
                   $"to hang on the band between (scale {F(half / Fascia.max.x)}). ";
            s = half / Fascia.max.x;
        }
        if (s < FasciaSmallest)
        {
            run.Check(false, $"The fascia would shrink to {F(s)} to fit over the door ({fit.Trim()}): its 32 cm capitals would be {F(32f * s, "0")} cm. Nothing was changed: " +
                             "the band needs a shorter board.");
            return;
        }
        if (!settled)
        {
            run.Check(false, $"The band over the door keeps stepping: after narrowing three times the frame still stands proud of its middle ({fit.Trim()}). Nothing was changed.");
            return;
        }
        int left = g.Held(back, -1), right = g.Held(back, +1);
        if (left < 2 || right < 2)
        {
            run.Check(false, $"The fascia would be held on {left} point(s) on its left and {right} on its right (at least 2 each, within {F(FasciaBearing * 100f, "0")} cm of its back): " +
                             "it would float off the band. Nothing was changed.");
            return;
        }
        float gap = g.Middle.Max(m => m.hit.point.z) - back;
        int glass = g.Middle.Count(m => m.hit.glass);
        p.fascia = new Placement
        {
            kit = Fascia, name = "CK_Fascia (over the door)", scale = s,
            rotation = Quaternion.Euler(0f, 180f, 0f),
            origin = new Vector3(0f, bottom, back),
            why = $"On the band over the door, centred, its bottom {F(FasciaAboveHead * 100f, "0")} cm over the door head ({F(d.headTop)} m, {d.head}), its back {F(FasciaOffBand * 1000f, "0")} mm " +
                  $"in front of the frontmost thing it covers (z {F(back + FasciaOffBand)}), held on {left} and {right} points either side; the band is up to {F(gap * 100f, "0.0")} cm " +
                  $"behind it in places{(glass > 0 ? $" ({glass} of {g.Middle.Count()} points in its middle are glass: a board fixed over the transom light, as the old sign was)" : "")}. " +
                  fit + "Not on a cut-away wall: the front stays drawn, and so does the fascia.",
        };
    }

    // ---------------------------------------------------------------- the blade sign: the west facade by the front corner

    static void PlanBlade(Shared.Run run, Plan p)
    {
        // The west facade's outer side: the west wall's collider (x -7.61 to -7.43 at 216cc1a).
        Transform boundaries = Shared.Find(run, BoundariesPath);
        BoxCollider west = boundaries != null
            ? boundaries.GetComponents<BoxCollider>().FirstOrDefault(b => b.enabled && !b.isTrigger && b.bounds.center.x < -7f && b.bounds.center.z > .5f)
            : null;
        p.westFace = west != null ? west.bounds.min.x : -7.61f;
        run.Info(west != null ? $"The west wall's collider: {Shared.BoundsText(west.bounds)}; rays at the west facade start at x {F(p.westFace - WestOut)}."
                              : "No west wall collider found: the west facade's outer side is taken at x -7.61 (216cc1a).");

        // Along the facade: the brief's 0.6 to 1.0 first, nearest 0.8 first; then the nearest solid frame either way.
        var zs = new List<float>();
        for (int i = 0; i <= 40; i++) zs.Add(BladeZFrom + i * .01f);
        var others = new List<float>();
        // To z 4.5 (10 Oct, Claude): the west front is glass from the corner post (z -0.14..0.04, a stepped profile) to past
        // z 1.6, and the bracket's plate must sit on a mullion; the first one back from the corner is the nearest place.
        for (int i = 0; i <= 470; i++)
        {
            float z = -.2f + i * .01f;
            if (z < BladeZFrom - 1e-4f || z > BladeZTo + 1e-4f) others.Add(z);
        }
        zs = zs.OrderBy(z => Mathf.Abs(z - BladeZBest)).Concat(others.OrderBy(z => Mathf.Abs(z - BladeZBest))).ToList();
        string inBrief = null;
        int tried = 0;
        var why = new SortedDictionary<int, string>();                 // z in cm -> why not (10 Oct: every reason kept, for the report)
        foreach (float z in zs)
        {
            tried++;
            float top = WallTop(p, z);
            if (float.IsNaN(top)) { inBrief ??= $"at z {F(z, "0.00")} there's no solid facade face"; why[Mathf.RoundToInt(z * 100f)] = "no solid facade face"; continue; }
            float yb = Mathf.Min(BladeBottomWanted, top - Blade.max.y - .01f);
            if (yb + Blade.min.y < BladeLowest)
            {
                inBrief ??= $"at z {F(z, "0.00")} the facade's top ({F(top)} m) is too low for the bracket over a {F(BladeLowest, "0.00")} m clearance";
                why[Mathf.RoundToInt(z * 100f)] = $"top {F(top, "0.00")} too low";
                continue;
            }
            // The bracket's wall plate (0.08 wide, 0.35 tall) must sit on solid frame: never on glass.
            var xs = new List<float>();
            bool solid = true;
            foreach (float dz in new[] { -.035f, 0f, .035f })
                for (int k = 0; k <= 5 && solid; k++)
                {
                    var at = new Vector3(0f, yb + .56f + k * .066f, z + dz);
                    Visual.Hit frame = WestRay(p, at, glass: false);
                    Visual.Hit any = WestRay(p, at);
                    if (!frame.ok || any.glass && any.distance < frame.distance - .001f) solid = false;
                    else xs.Add(frame.point.x);
                }
            // The corner post (z -0.14..0.04) is the only frame on the west front tall enough for the plate (the rest is one run
            // of glass from z 0.04 to past 4.5, 10 Oct): its outer face steps by up to 5 cm, so there the plate is fixed to the
            // outermost face (nothing sinks into the post; the step behind it is 3 m up, seen edge-on from the street).
            bool cornerPost = z >= -.05f && z <= .04f;
            if (!solid || xs.Max() - xs.Min() > (cornerPost ? .055f : .006f))
            {
                inBrief ??= $"at z {F(z, "0.00")} the bracket's plate would be on glass or on an uneven face";
                why[Mathf.RoundToInt(z * 100f)] = !solid ? $"plate on glass (top {F(top, "0.00")}, plate {F(yb + .56f, "0.00")}..{F(yb + .89f, "0.00")})"
                                                         : $"plate uneven ({F((xs.Max() - xs.Min()) * 100f, "0.0")} cm)";
                continue;
            }
            float face = xs.Min();
            // The disc and the arm stand out in the open: nothing in the way.
            var box = MinMax(new Vector3(face - 1.0f, yb, z - .06f), new Vector3(face - .03f, yb + .9f, z + .06f));
            string blocked = InTheWay(p, box);
            if (blocked != null) { inBrief ??= $"at z {F(z, "0.00")} {blocked} is in the sign's way"; why[Mathf.RoundToInt(z * 100f)] = "in the way: " + blocked; continue; }
            p.bladeFace = face;
            bool brief = z >= BladeZFrom - 1e-4f && z <= BladeZTo + 1e-4f;
            p.blade = new Placement
            {
                kit = Blade, name = "CK_BladeSign (west facade, front corner)", rotation = Quaternion.identity,
                origin = new Vector3(face - BladeOffWall, yb, z),
                why = $"Its bracket on the west facade's solid frame (face at x {F(face)}), standing out 1.0 m over the pavement (no turn: the piece stands out along -x, " +
                      $"which is west), the disc's faces north and south (the south one shows from the street and the overhead camera); the facade's top here is at {F(top)} m, " +
                      (yb < BladeBottomWanted - 1e-4f ? $"so the disc's bottom is at {F(yb, "0.00")} m, not the research's 2.50, for the bracket to stay on the wall (no parapet above). "
                                                     : "so the disc's bottom is at the research's 2.50 m. ") +
                      (brief ? $"Along the facade at z {F(z, "0.00")}, in the brief's 0.6 to 1.0."
                             : $"Along the facade at z {F(z, "0.00")} ({tried} places tried): the brief's 0.6 to 1.0 had no solid frame for its plate ({inBrief})."),
            };
            break;
        }
        if (p.blade == null)
        {
            // Every reason, run-length by z, so the facade can be read from the report.
            var runs = new List<string>();
            int? start = null, last = null;
            string cur = null;
            foreach (var kv in why)
            {
                if (cur != null && kv.Value == cur && last.HasValue && kv.Key == last.Value + 1) { last = kv.Key; continue; }
                if (cur != null) runs.Add($"z {F(start.Value / 100f, "0.00")}..{F(last.Value / 100f, "0.00")}: {cur}");
                start = last = kv.Key; cur = kv.Value;
            }
            if (cur != null) runs.Add($"z {F(start.Value / 100f, "0.00")}..{F(last.Value / 100f, "0.00")}: {cur}");
            run.Info("Why no place along the west facade: " + string.Join("; ", runs));
            run.Check(false, "No solid frame on the west facade between z -0.2 and 4.5 can take the blade sign's bracket: " + (inBrief ?? "nothing measured") + ". Nothing was changed.");
            return;
        }
        // Over the pavement, clear of the cars.
        Bounds bb = p.blade.Expected;
        Visual.Hit ground = p.visual.Ray(new Vector3(bb.min.x + .02f, bb.min.y - .05f, p.blade.origin.z), Vector3.down, bb.min.y + .5f, p.SkipOld);
        run.Check(ground.ok && ground.point.y > -.06f, $"under the blade sign's outer edge (x {F(bb.min.x)}) is pavement, at {(ground.ok ? F(ground.point.y) : "nothing")} m (the road is lower)");
        float cars = CarClearance(run, bb, out string car);
        run.Check(cars >= CarClear, float.IsPositiveInfinity(cars) ? "no car routes in the scene to keep clear of"
                                                                    : $"the blade sign is {F(cars, "0.00")} m from the nearest car's path ({car}; at least {F(CarClear, "0.0")})");
    }

    // The facade's top at z: the first solid face in its plane met coming down from 3.8 m.
    static float WallTop(Plan p, float z)
    {
        for (int i = 0; i <= 180; i++)
        {
            float y = 3.8f - i * .01f;
            if (WestRay(p, new Vector3(0f, y, z), glass: false).ok) return y;
        }
        return float.NaN;
    }

    // Anything drawn inside a box: rays through it along z and down. Null when it's clear, else what's there.
    static string InTheWay(Plan p, Bounds box, Func<Renderer, bool> skip = null)
    {
        skip ??= p.SkipOld;
        foreach (float fx in new[] { .05f, .5f, .95f })
            foreach (float fy in new[] { .05f, .5f, .95f })
            {
                float x = Mathf.Lerp(box.min.x, box.max.x, fx), y = Mathf.Lerp(box.min.y, box.max.y, fy);
                Visual.Hit h = p.visual.Ray(new Vector3(x, y, box.min.z), Vector3.forward, box.size.z, skip);
                if (h.ok) return Shared.PathOf(h.renderer.transform);
            }
        foreach (float fx in new[] { .05f, .5f, .95f })
            foreach (float fz in new[] { .2f, .8f })
            {
                float x = Mathf.Lerp(box.min.x, box.max.x, fx), z = Mathf.Lerp(box.min.z, box.max.z, fz);
                Visual.Hit h = p.visual.Ray(new Vector3(x, box.max.y, z), Vector3.down, box.size.y, skip);
                if (h.ok) return Shared.PathOf(h.renderer.transform);
            }
        return null;
    }

    // The street's actors' routes: segments in order, the last back to the first only on a closed route (an open route's
    // actor reappears at its start). Cars have wheels; flyers (smooth routes) are left out.
    static IEnumerable<(string who, Vector3 a, Vector3 b)> Routes(Shared.Run run, bool cars)
    {
        foreach (StreetLife life in Shared.InScene<StreetLife>(run))
            foreach (StreetLife.Actor a in life.actors)
            {
                if (a == null || a.waypoints == null || a.smoothRoute) continue;
                bool car = a.wheels != null && a.wheels.Length > 0;
                if (car != cars) continue;
                Transform[] w = a.waypoints.Where(t => t != null).ToArray();
                string who = a.actor != null ? a.actor.name : "an actor";
                for (int i = 0; i + 1 < w.Length; i++) yield return (who, w[i].position, w[i + 1].position);
                if (!a.openRoute && w.Length > 2) yield return (who, w[w.Length - 1].position, w[0].position);
            }
    }

    static float CarClearance(Shared.Run run, Bounds box, out string which) => Nearest(Routes(run, true), box, out which);

    static float Nearest(IEnumerable<(string who, Vector3 a, Vector3 b)> segments, Bounds box, out string which)
    {
        float best = float.PositiveInfinity;
        which = "none";
        foreach ((string who, Vector3 a, Vector3 b) in segments)
        {
            float d = BoxToSegment(box, a, b);
            if (d < best) { best = d; which = who; }
        }
        return best;
    }

    // ---------------------------------------------------------------- the A-frame: its two boards

    static void PlanAFrame(Shared.Run run, Plan p)
    {
        Transform frame = Shared.Find(run, AFramePath);
        if (frame == null || !frame.gameObject.activeInHierarchy)
        {
            run.Check(false, $"{AFramePath} isn't in the scene, or it is switched off: the A-frame the chalk faces go on.");
            return;
        }
        MeshRenderer[] renderers = frame.GetComponentsInChildren<MeshRenderer>(false);
        if (renderers.Length != 1)
        {
            run.Check(false, $"The patio A-frame has {renderers.Length} renderers (MenuBoard.fbx is one mesh): it isn't the one this step measured.");
            return;
        }
        MeshRenderer r = renderers[0];
        MeshFilter f = r.GetComponent<MeshFilter>();
        Mesh m = f != null ? f.sharedMesh : null;
        Material[] mats = r.sharedMaterials;
        int board = Array.FindIndex(mats, x => x != null && Has(x.name, "Chalkboard"));
        int chalk = Array.FindIndex(mats, x => x != null && Has(x.name, "Chalk") && !Has(x.name, "Chalkboard"));
        if (m == null || board < 0 || chalk < 0 || board >= m.subMeshCount || chalk >= m.subMeshCount)
        {
            run.Check(false, $"The patio A-frame wears {string.Join(", ", mats.Select(Name))} on {(m != null ? m.subMeshCount : 0)} parts: its chalkboard and its chalk lines " +
                             "(MenuBoard.fbx: CC_Wood_Counter, DC_Chalkboard, DC_Chalk) aren't separate slots, so the lines can't be painted over alone.");
            return;
        }
        List<Vector3[]> boardTris = WorldTriangles(f, board), chalkTris = WorldTriangles(f, chalk);
        if (boardTris == null || chalkTris == null)
        {
            run.Check(false, $"The patio A-frame's mesh ({m.name}) can't be read here, so its boards can't be measured.");
            return;
        }
        p.aFrame = r;
        p.boardSlot = board;
        p.chalkSlot = chalk;
        p.aFrameAt = frame.position;
        p.aFrameTurn = frame.rotation;

        // The boards: the chalkboard's triangles grouped by the way they face (8°). Each big group's outward side is the side
        // away from the A's middle, whatever way its triangles wind (a mirrored or odd export winds them the other way);
        // groups facing out the same way are one board, and its face is the group standing farthest out (a board with
        // thickness has its back in the same line).
        var groups = new List<FaceGroup>();
        foreach (Vector3[] t in boardTris)
        {
            Vector3 n = Vector3.Cross(t[1] - t[0], t[2] - t[0]);
            float area = n.magnitude * .5f;
            if (area < 1e-7f) continue;
            n /= area * 2f;
            FaceGroup g = groups.FirstOrDefault(x => Vector3.Angle(x.Normal, n) < 8f);
            if (g == null) groups.Add(g = new FaceGroup());
            g.Add(t, n, area);
        }
        Vector3 middle = r.bounds.center;
        var boards = new List<(Vector3 outward, FaceGroup face)>();
        foreach (FaceGroup g in groups.Where(g => g.area >= .04f))
        {
            float side = Vector3.Dot(g.Normal, g.Centroid - middle);
            if (Mathf.Abs(side) < .01f) continue;   // a face through the middle isn't a board's face
            Vector3 outward = side > 0f ? g.Normal : -g.Normal;
            int k = boards.FindIndex(b => Vector3.Angle(b.outward, outward) < 8f);
            if (k < 0) boards.Add((outward, g));
            else if (Vector3.Dot(g.Centroid - boards[k].face.Centroid, outward) > 0f) boards[k] = (outward, g);
        }
        run.Info($"The patio A-frame ({Shared.PathOf(frame)}, scale {F(frame.lossyScale.x, "0.00")}): its chalkboard faces " +
                 string.Join("; ", groups.Where(g => g.area >= .01f).Select(g => $"{F(g.area)} m² turned to {F(Heading(g.Normal), "0")}")) +
                 $"; boards facing out: {string.Join(", ", boards.Select(b => F(Heading(b.outward), "0")))}.");
        if (boards.Count == 0 || boards.Count > 2)
        {
            run.Check(false, $"The patio A-frame has {boards.Count} chalkboard faces facing out (an A-frame has two): it isn't the one this step measured.");
            return;
        }
        if (boards.Count == 1) run.Info("Only one of the A-frame's boards is chalkboard (the other is plain): one chalk face goes on.");
        foreach ((Vector3 outward, FaceGroup g) in boards.OrderByDescending(b => b.face.area))
        {
            Board b = MeasureBoard(g, outward, chalkTris);
            if (b == null) { run.Check(false, "One of the A-frame's boards lies flat: it isn't a sign's face."); return; }
            float s = Mathf.Min((b.width - 2f * FaceInset) / Face.Size.x, (b.height - 2f * FaceInset) / Face.Size.y);
            bool ok = s >= .4f && s <= 1.8f;
            float heading = Heading(b.normal);
            string side = Mathf.Abs(Mathf.DeltaAngle(heading, 180f)) <= 90f ? "street side" : "café side";
            if (p.faces.Any(x => x.name.Contains(side))) side += ", the other board";
            run.Check(ok, $"the A-frame's {side} board: {F(b.width)} x {F(b.height)} m of chalkboard facing {F(heading, "0")}, leaning {F(Vector3.Angle(b.up, Vector3.up), "0.0")}°" +
                          (b.proud > .0001f ? $", its chalk lines {F(b.proud * 1000f, "0.0")} mm proud" : "") +
                          (ok ? "" : $": the 0.60 x 0.90 face would scale to {F(s)}, so it isn't the board this step expects"));
            if (!ok) return;
            p.boards.Add(b);
            p.faces.Add(new Placement
            {
                kit = Face, name = $"CK_AFrameFace (patio A-frame, {side})", scale = s,
                rotation = Quaternion.LookRotation(b.normal, b.up),
                origin = b.centre - b.up * (Face.Size.y * s * .5f) + b.normal * (Mathf.Max(0f, b.proud) + FaceProud),
                why = $"Laid on the board (measured {F(b.width)} x {F(b.height)} m, not 0.60 x 0.90), so scaled uniformly to {F(s)} and centred, {F(FaceProud * 1000f, "0")} mm proud" +
                      (b.proud > .0001f ? " of the old chalk lines" : "") + "; up along the board's lean.",
            });
        }
    }

    sealed class FaceGroup
    {
        public float area;
        Vector3 normalSum, centroidSum;
        public readonly List<Vector3> points = new List<Vector3>();
        public Vector3 Normal => normalSum.normalized;
        public Vector3 Centroid => area > 0f ? centroidSum / area : Vector3.zero;
        public void Add(Vector3[] t, Vector3 n, float a)
        {
            normalSum += n * a;
            centroidSum += (t[0] + t[1] + t[2]) / 3f * a;
            area += a;
            points.AddRange(t);
        }
    }

    // One board's chalk face: its plane, its up along the lean, its extent, and how far the chalk lines over it stand proud.
    static Board MeasureBoard(FaceGroup g, Vector3 outward, List<Vector3[]> chalk)
    {
        Vector3 n = outward.normalized;
        Vector3 up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
        if (up.magnitude < .5f) return null;
        up.Normalize();
        Vector3 right = Vector3.Cross(up, n);
        Vector3 o = g.Centroid;
        float r0 = float.PositiveInfinity, r1 = float.NegativeInfinity, u0 = float.PositiveInfinity, u1 = float.NegativeInfinity;
        foreach (Vector3 v in g.points)
        {
            float a = Vector3.Dot(v - o, right), b = Vector3.Dot(v - o, up);
            r0 = Mathf.Min(r0, a); r1 = Mathf.Max(r1, a); u0 = Mathf.Min(u0, b); u1 = Mathf.Max(u1, b);
        }
        var board = new Board
        {
            normal = n, up = up, right = right, width = r1 - r0, height = u1 - u0,
            centre = o + right * ((r0 + r1) * .5f) + up * ((u0 + u1) * .5f),
        };
        foreach (Vector3[] t in chalk)
            foreach (Vector3 v in t)
            {
                Vector3 d = v - board.centre;
                if (Mathf.Abs(Vector3.Dot(d, right)) <= board.width * .5f + .02f && Mathf.Abs(Vector3.Dot(d, up)) <= board.height * .5f + .02f && Vector3.Dot(d, n) > -.01f)
                    board.proud = Mathf.Max(board.proud, Vector3.Dot(d, n));
            }
        return board;
    }

    // ---------------------------------------------------------------- the mosaic: the floor inside the door

    static void PlanMosaic(Shared.Run run, Plan p)
    {
        var hits = new List<(float x, float z, Visual.Hit hit)>();
        foreach (float x in new[] { -1.15f, 0f, 1.15f })
            foreach (float z in new[] { .25f, .80f, 1.35f })
                hits.Add((x, z, p.visual.Ray(new Vector3(x, MosaicProbeY, z), Vector3.down, MosaicProbeY + .3f, p.SkipOld)));
        if (hits.Any(h => !h.hit.ok))
        {
            run.Check(false, "There's no floor under part of where the mosaic goes (x -1.2..1.2, z 0.2..1.4). Nothing was changed.");
            return;
        }
        // The floor is the one surface under all of it; anything else there lies where the mosaic goes.
        Renderer floor = hits.GroupBy(h => h.hit.renderer).OrderByDescending(g => g.Count()).First().Key;
        Bounds fb = floor.bounds;
        bool covers = fb.min.x <= -1.2f && fb.max.x >= 1.2f && fb.min.z <= .2f && fb.max.z >= 1.4f;
        foreach ((float x, float z, Visual.Hit hit) in hits.Where(h => h.hit.renderer != floor))
            run.Check(false, $"{Shared.PathOf(hit.renderer.transform)} lies where the mosaic goes (at x {F(x, "0.00")}, z {F(z, "0.00")}, its top at {F(hit.point.y)} m): Room 1 takes the rugs, " +
                             "the runner and the doormat away. Run it first, or find out what this is.");
        float low = hits.Min(h => h.hit.point.y), high = hits.Max(h => h.hit.point.y);
        run.Check(covers && high - low <= .003f, $"under the mosaic is one flat surface, {Shared.PathOf(floor.transform)} ({F(low)} to {F(high)} m; it spans {Shared.BoundsText(fb)})");
        run.Check(Mathf.Abs(high) <= .03f, $"that surface is the room's floor, at {F(high)} m (the floor is at 0: anything higher is a rug or a mat, which Room 1 takes away)");
        p.floor = floor;
        p.floorY = high;

        // The doors are held open, swung out onto the pavement: their leaves must stay out of the room.
        Transform leaves = Shared.Find(run, DoorLeavesPath);
        if (leaves != null)
            foreach (BoxCollider box in leaves.GetComponentsInChildren<BoxCollider>(true)) p.doorLeaves.Add(box.bounds);
        float reach = p.doorLeaves.Count > 0 ? p.doorLeaves.Max(b => b.max.z) : float.NegativeInfinity;
        run.Check(p.doorLeaves.Count > 0 && reach < .2f - .05f, p.doorLeaves.Count == 0 ? $"no door leaves under {DoorLeavesPath} to keep clear of"
            : $"the door leaves are held open outward and reach z {F(reach)} at most: the mosaic (from z 0.20) is clear of their swing");
        Rect inside = CafeDaylight.CafeInside;
        run.Check(inside.Contains(new Vector2(-1.2f, .2f)) && inside.Contains(new Vector2(1.2f, 1.4f)), "the mosaic lies inside the café room (CafeDaylight.CafeInside: from z 0.1)");
        p.mosaic = new Placement
        {
            kit = Mosaic, name = "CK_EntryMosaic (inside the door)", rotation = Quaternion.identity,
            origin = new Vector3(0f, high + MosaicLift, .8f),
            why = $"On the floor just inside the door (x -1.2..1.2, z 0.2..1.4), no turn (it reads from the door looking north and upright from the overhead camera), " +
                  $"its bottom {F(MosaicLift * 1000f, "0.0")} mm over the floor at {F(high)} m, its top {F((MosaicLift + Mosaic.max.y) * 1000f, "0.0")} mm proud. No collider: people walk over it.",
        };
    }

    // ---------------------------------------------------------------- the bench: the west bulkhead

    static void PlanBench(Shared.Run run, Plan p)
    {
        Transform planter = Shared.Named(run, PlanterName, false).Where(t => t.position.x < 0f).OrderByDescending(t => t.position.x).FirstOrDefault();
        if (planter == null) { run.Check(false, $"No west '{PlanterName}' on the patio: the bench's place is measured from it."); return; }
        p.planter = Shared.RendererBounds(planter);
        Transform boundaries = Shared.Find(run, BoundariesPath);
        BoxCollider[] boxes = boundaries != null ? boundaries.GetComponents<BoxCollider>() : new BoxCollider[0];
        BoxCollider fence = boxes.FirstOrDefault(b => b.enabled && !b.isTrigger && b.bounds.center.z < -.5f && b.bounds.center.x < -7f);
        BoxCollider wall = boxes.FirstOrDefault(b => b.enabled && !b.isTrigger && b.bounds.Contains(new Vector3(-4.3f, 1f, -.12f)));
        float west = fence != null ? fence.bounds.max.x : -7.51f;
        Transform patio = Shared.Find(run, PatioSetPath);
        if (patio != null) p.patioSet.AddRange(patio.GetComponentsInChildren<Collider>(false).Where(c => c.enabled && !c.isTrigger));

        float x0 = west + BenchOffFence, x1 = p.planter.min.x - BenchGapToPlanter;
        if (x1 - x0 < Bench.Size.x)
        {
            run.Check(false, $"The bench (2.40 m) doesn't fit between the patio's west edge (x {F(west)}) and the west planter ({Shared.BoundsText(p.planter)}): " +
                             $"{F(x1 - x0)} m with {F(BenchOffFence * 100f, "0")} cm and {F(BenchGapToPlanter * 100f, "0")} cm to spare. Nothing was changed: the planter moves, or the bench gets shorter.");
            return;
        }
        float xc = (x0 + x1) * .5f;

        // The bulkhead's street face behind it (rays from between the bench and the patio table, past the planter and the set).
        bool Skip(Renderer r) => p.SkipOld(r) || r.transform.IsChildOf(planter) || patio != null && r.transform.IsChildOf(patio);
        var faces = new List<float>();
        for (int i = 0; i <= 22; i++)
        {
            Visual.Hit h = p.visual.Ray(new Vector3(xc - 1.1f + i * .1f, .35f, -1.0f), Vector3.forward, 1.2f, Skip, glass: false, where: InFront, facing: Facing);
            if (h.ok) faces.Add(h.point.z);
        }
        if (faces.Count < 20)
        {
            run.Check(false, $"The west bulkhead couldn't be found behind the bench at 0.35 m ({faces.Count} of 23 points). Nothing was changed.");
            return;
        }
        p.bulkheadZ = faces.Min();
        float back = Mathf.Min(p.bulkheadZ, wall != null ? wall.bounds.min.z : p.bulkheadZ) - BenchOffWall;

        // The pavement under it (a solid: the bench stands on it).
        var ground = new List<float>();
        foreach (Vector3 at in new[] { new Vector3(xc, 1f, back - .22f), new Vector3(xc - 1.1f, 1f, back - .05f), new Vector3(xc + 1.1f, 1f, back - .05f),
                                       new Vector3(xc - 1.1f, 1f, back - .39f), new Vector3(xc + 1.1f, 1f, back - .39f) })
        {
            RaycastHit? h = Shared.Down(run, at, 2f, c => c.transform.IsChildOf(planter));
            if (h.HasValue) ground.Add(h.Value.point.y);
        }
        if (ground.Count < 5 || ground.Max() - ground.Min() > .01f)
        {
            run.Check(false, $"The pavement under the bench isn't one level solid ({ground.Count} of 5 points, {(ground.Count > 0 ? F(ground.Min()) + " to " + F(ground.Max()) : "none")} m). Nothing was changed.");
            return;
        }
        var bench = new Placement
        {
            kit = Bench, name = "CK_SidewalkBench (west bulkhead)", rotation = Quaternion.Euler(0f, 180f, 0f),
            origin = new Vector3(xc, ground.Max(), back - Bench.max.z),
            why = $"On the pavement against the west bulkhead (its face at z {F(p.bulkheadZ)}; the wall's solid from z {(wall != null ? F(wall.bounds.min.z) : "?")}), its back {F(BenchOffWall * 100f, "0")} cm off, " +
                  $"facing the street. The brief's x -4.3 is where the west planter stands ({Shared.BoundsText(p.planter)}), so the bench is centred in the gap between the planter " +
                  $"({F(BenchGapToPlanter * 100f, "0")} cm) and the patio's west edge (x {F(west)}): x {F(xc - 1.2f)} to {F(xc + 1.2f)}. A box collider (its solid, day and night). Decor, not a seat: no TableSeat.",
        };
        // Nothing drawn where it goes (the bottom 2 cm left out: the pavement).
        Bounds room = bench.Expected;
        room.SetMinMax(room.min + new Vector3(0f, .02f, 0f), room.max);
        string blocked = InTheWay(p, room);
        run.Check(blocked == null, blocked == null ? "nothing drawn stands where the bench goes" : $"{blocked} stands where the bench goes");
        p.bench = bench;
        BenchClearances(run, p, bench.Expected, true);
    }

    // The bench's room: clear of the planter, the patio set, the spawn, the door, the walks to the door, the street's
    // walkers and the waiting spots and seats.
    static void BenchClearances(Shared.Run run, Plan p, Bounds b, bool planned)
    {
        string when = planned ? "would be" : "is";
        run.Check(p.planter.min.x - b.max.x >= BenchGapToPlanter - .002f, $"the bench {when} {F(p.planter.min.x - b.max.x)} m from the west planter");
        float set = p.patioSet.Count > 0 ? p.patioSet.Min(c => Gap(c.bounds, b)) : float.PositiveInfinity;
        run.Check(set >= .6f, float.IsPositiveInfinity(set) ? "no patio table and chairs to keep clear of" : $"the bench {when} {F(set)} m from the patio table and chairs (a person passes between: 0.6)");
        GameObject spawn = GameObject.Find("SpawnPoint");
        if (spawn != null)
        {
            float d = Gap(new Bounds(spawn.transform.position, Vector3.zero), b);
            run.Check(d >= 1f, $"the bench {when} {F(d)} m from the customers' spawn point (at least 1 m)");
        }
        float leaves = p.doorLeaves.Count > 0 ? p.doorLeaves.Min(l => Gap(l, b)) : float.PositiveInfinity;
        run.Check(b.max.x <= -1.5f && leaves >= 1f, $"the bench {when} clear of the door lane (its east end at x {F(b.max.x)}) and {F(leaves)} m from the open door leaves");
        float walks = WalkClearance(run, b, out string walk);
        run.Check(walks >= 1f, float.IsPositiveInfinity(walks) ? "no walking routes to the door to keep clear of" : $"the bench {when} {F(walks)} m from the nearest walk to the door ({walk})");
        float walkers = Nearest(Routes(run, false), b, out string walker);
        run.Check(walkers >= WalkerClear, float.IsPositiveInfinity(walkers) ? "no street walkers' routes to keep clear of"
                                                                            : $"the bench {when} {F(walkers)} m from the nearest street walker's path ({walker}; at least {F(WalkerClear, "0.0")})");
        float spots = Shared.InScene<WaitingSpot>(run, false).Select(w => Gap(new Bounds(w.StandPoint.position, Vector3.zero), b)).DefaultIfEmpty(float.PositiveInfinity).Min();
        run.Check(spots >= .8f, $"the bench {when} {F(spots)} m from the nearest waiting spot or seat's stand point (at least 0.8)");
    }

    // The nearest of the walks people take to the café's door (CafeArrivals: from the neighbours' doors and the car park).
    static float WalkClearance(Shared.Run run, Bounds box, out string which)
    {
        var segments = new List<(string, Vector3, Vector3)>();
        foreach (CafeArrivals arrivals in Shared.InScene<CafeArrivals>(run))
        {
            var routes = new List<CafeArrivals.Route>();
            if (arrivals.EditorFootRoutes != null) routes.AddRange(arrivals.EditorFootRoutes);
            if (arrivals.EditorLotToDoor != null) routes.Add(arrivals.EditorLotToDoor);
            foreach (CafeArrivals.Route r in routes)
                if (r != null && r.points != null)
                    for (int i = 0; i + 1 < r.points.Length; i++) segments.Add((r.name, r.points[i], r.points[i + 1]));
        }
        return Nearest(segments, box, out which);
    }

    // ================================================================== the change

    static void Change(Shared.Run run)
    {
        Plan p = plan ?? throw new InvalidOperationException("The survey didn't run.");
        Transform group = Shared.StepGroup(run, GroupName);

        // 1. Off, never deleted: the old sign over the door.
        foreach ((GameObject go, string why) in p.off) Shared.SwitchOff(run, go, why);

        // 2. The A-frame's chalk lines into its own chalkboard; the A-frame stays where it is.
        Material[] mats = p.aFrame.sharedMaterials.ToArray();
        mats[p.chalkSlot] = mats[p.boardSlot];
        Shared.Repaint(run, p.aFrame, mats, "the patio A-frame's two white chalk lines (its DC_Chalk slot; research §5 item 4) painted over in its own chalkboard: the chalk faces carry the words");

        // 3. The pieces, each at its measured place (a made piece: its whole pose goes with it on an undo).
        foreach (Placement x in p.Pieces)
        {
            x.placed = Shared.Place(run, x.kit.piece, group, x.origin, 0f, x.name, Mathf.Approximately(x.scale, 1f) ? (Vector3?)null : Vector3.one * x.scale);
            x.placed.transform.SetPositionAndRotation(x.origin, x.rotation);
        }

        // 4. The bench's box: its solid, on the object that holds its mesh, so Night walk 2's list counts it as solid on
        //    its own (the props report's one mesh). Imported in several parts, the box goes on the bench's root round all
        //    of them. The routes are re-baked round it after this.
        MeshFilter[] parts = p.bench.placed.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        GameObject holder = parts.Length == 1 ? parts[0].gameObject : p.bench.placed;
        Bounds solid = parts.Length == 1 ? parts[0].sharedMesh.bounds : Transformed(parts[0].sharedMesh.bounds, holder.transform.worldToLocalMatrix * parts[0].transform.localToWorldMatrix);
        foreach (MeshFilter f in parts.Skip(1)) solid.Encapsulate(Transformed(f.sharedMesh.bounds, holder.transform.worldToLocalMatrix * f.transform.localToWorldMatrix));
        BoxCollider box = Shared.AddComponent<BoxCollider>(run, holder);
        box.center = solid.center;
        box.size = solid.size;
        if (parts.Length > 1) Shared.Note(run, $"The bench came in {parts.Length} meshes: its box is on its root, round all of them.");

        Shared.Note(run, $"Measured: the panes by the door are {F(p.west.Width)} m (west) and {F(p.east.Width)} m (east) clear; the gilding is scaled {F(p.gildingL.scale)} and {F(p.gildingR.scale)}; " +
                         $"the fascia {F(p.fascia.scale)} over a door head at {F(p.door.headTop)} m; the blade sign's disc bottom at {F(p.blade.origin.y, "0.00")} m, z {F(p.blade.origin.z, "0.00")}; " +
                         $"the bench at x {F(p.bench.origin.x)}; {p.faces.Count} chalk face(s) on the A-frame.");
        Physics.SyncTransforms();
        After(run, p);
    }

    // ================================================================== the checks after (any FAIL puts everything back)

    static void After(Shared.Run run, Plan p)
    {
        var v = new Visual(Shared.InScene<MeshRenderer>(run, false).Where(r => !p.SkipMoving(r)), Roi);

        // Every piece where it should be, and no collider on anything but the bench.
        foreach (Placement x in p.Pieces)
        {
            Bounds got = Shared.RendererBounds(x.placed.transform), want = x.Expected;
            run.Check(Near(got.min, want.min, .01f) && Near(got.max, want.max, .01f), $"{x.name} fills {Shared.BoundsText(got)} (planned {Shared.BoundsText(want)})");
            if (x.kit != Bench)
                run.Check(x.placed.GetComponentsInChildren<Collider>(true).Length == 0, $"{x.name} has no collider (it is in nobody's way)");
        }

        CheckFascia(run, p, v);
        CheckGildings(run, p, v);
        CheckBlade(run, p, v);
        CheckFaces(run, p, v);
        CheckMosaic(run, p, v);
        CheckBench(run, p);

        // The old sign off, and nothing left where it was.
        foreach ((GameObject go, string _) in p.off) run.Check(!go.activeInHierarchy, $"{Shared.PathOf(go.transform)} is off");
        var mine = new HashSet<Renderer>(p.Pieces.SelectMany(x => x.placed.GetComponentsInChildren<Renderer>(true)));
        List<Renderer> left = Shared.InScene<Renderer>(run, false).Where(r => r.enabled && !mine.Contains(r) && Within(r.bounds, OldSignZone)).ToList();
        run.Check(left.Count == 0, left.Count == 0 ? "nothing is left where the old sign was" : "left floating where the old sign was: " + string.Join(", ", left.Select(r => Shared.PathOf(r.transform))));

        // What the rest of the game sees.
        CutawayFacts(run, p, true);
        NightFacts(run, p, true);
        int moved = 0;
        foreach (KeyValuePair<Transform, Vector3> a in p.anchors)
            if (a.Key != null && (a.Key.position - a.Value).magnitude > .001f)
            {
                moved++;
                run.Check(false, $"{Shared.PathOf(a.Key)} moved from {Shared.V(a.Value)} to {Shared.V(a.Key.position)}");
            }
        run.Check(moved == 0, $"the {p.anchors.Count} gameplay anchors (the stations and their stand points, the waiting spots and seats, the queue, the intake shelf, the spawn points, the door leaves) are where they were, within 1 mm");
        HashSet<Collider> lane = LaneColliders();
        run.Check(lane.SetEquals(p.lane), lane.SetEquals(p.lane) ? $"the door lane (x -1..1, z 0..2) holds the same {lane.Count} collider(s) as before"
                                                                  : "the door lane gained or lost a collider: " + string.Join(", ", lane.Except(p.lane).Concat(p.lane.Except(lane)).Select(c => Shared.PathOf(c.transform))));
    }

    static void CheckFascia(Shared.Run run, Plan p, Visual v)
    {
        Placement x = p.fascia;
        Renderer r = x.Renderer;
        float y = x.origin.y + Fascia.Size.y * x.scale * .5f;
        int seen = 0;
        foreach (float fx in new[] { -.9f, -.45f, 0f, .45f, .9f })
        {
            Visual.Hit front = v.Ray(new Vector3(fx * x.scale, y, StreetZ), Vector3.forward, FrontReach, p.SkipMoving);
            if (front.ok && front.renderer == r) seen++;
        }
        run.Check(seen == 5, $"from the street the fascia is the first thing seen across its width ({seen}/5 points)");
        // Behind it, from just behind its back: the front's plane behind its whole middle, and held on both sides.
        Backing g = BackingOf(v, x.scale, x.origin.y, x.origin.z + .0002f, o => o == r || p.SkipMoving(o));
        List<string> holes = g.Holes;
        int left = g.Held(x.origin.z, -1), right = g.Held(x.origin.z, +1);
        run.Check(holes.Count == 0, holes.Count == 0 ? "the front's plane is behind the fascia's whole middle (it isn't over the void)"
                                                     : "nothing behind the fascia at " + string.Join(", ", holes.Take(8)));
        run.Check(left >= 2 && right >= 2, $"the fascia is held: {left} point(s) on its left and {right} on its right within {F(FasciaBearing * 100f, "0")} cm of its back");
        run.Check(r.bounds.min.y >= p.door.headTop + .04f, $"the fascia clears the door head: its bottom at {F(r.bounds.min.y)} m, the head's top at {F(p.door.headTop)} m");
        run.Check(r.bounds.max.y <= p.door.bandTop + .01f, $"the fascia stays on the band: its top at {F(r.bounds.max.y)} m, the band's top at {F(p.door.bandTop)} m ({p.door.band})");
    }

    static void CheckGildings(Shared.Run run, Plan p, Visual v)
    {
        foreach ((Placement x, Pane pane) in new[] { (p.gildingL, p.west), (p.gildingR, p.east) })
        {
            Renderer r = x.Renderer;
            Bounds b = r.bounds;
            run.Check(b.min.x >= pane.x0 + .04f && b.max.x <= pane.x1 - .04f && b.min.y >= pane.y0 + .02f && b.max.y <= pane.y1 - .02f,
                      $"{x.name} lies within its pane (x {F(pane.x0)}..{F(pane.x1)}, y {F(pane.y0)}..{F(pane.y1)}): x {F(b.min.x)}..{F(b.max.x)}, y {F(b.min.y)}..{F(b.max.y)}");
            float off = b.min.z - pane.zRoom;
            run.Check(off >= .0015f && off <= .0035f, $"{x.name} is on the room side of the glass, {F(off * 1000f, "0.0")} mm off its room face (not inside the glass, not floating off it)");
            Visual.Hit seen = v.Ray(new Vector3(b.center.x, b.center.y, StreetZ), Vector3.forward, FrontReach, p.SkipMoving, glass: false, always: r);
            run.Check(seen.ok && seen.renderer == r, $"from the street {x.name} is seen through the glass with nothing in front of it ({(seen.ok ? Shared.PathOf(seen.renderer.transform) : "nothing")})");
            // Nothing else in the 2 mm between the glass and the leaf, or touching the leaf from the room (a curtain, a rod).
            int touching = 0;
            foreach (float fx in new[] { .1f, .5f, .9f })
            {
                var at = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), b.center.y, b.max.z + .005f);
                Visual.Hit h = v.Ray(at, Vector3.back, .005f + (b.max.z - pane.zRoom) - .0005f, o => o == r || p.SkipMoving(o), glass: false);
                if (h.ok) touching++;
            }
            run.Check(touching == 0, $"nothing else touches {x.name}, between it and the glass or within 5 mm behind it ({touching}/3 points)");
        }
    }

    static void CheckBlade(Shared.Run run, Plan p, Visual v)
    {
        Placement x = p.blade;
        Renderer r = x.Renderer;
        Bounds b = r.bounds;
        Visual.Hit wall = v.Ray(new Vector3(p.bladeFace - .3f, x.origin.y + .72f, x.origin.z), Vector3.right, .6f, o => o == r || p.SkipMoving(o), glass: false);
        // On the corner post (PlanBlade, 10 Oct) the plate is fixed to the post's outermost face and the post steps back by up
        // to 5 cm behind part of it: there the wall behind may be up to 5.5 cm behind the plate, never in front of it.
        bool cornerPost = x.origin.z >= -.05f && x.origin.z <= .04f;
        float gap = wall.ok ? wall.point.x - p.bladeFace : float.NaN;
        run.Check(wall.ok && gap >= -.004f && gap <= (cornerPost ? .055f : .004f),
                  $"the blade sign's bracket is fixed to the west facade{(cornerPost ? "'s corner post" : "")}: the wall behind its plate at x {(wall.ok ? F(wall.point.x) : "nothing")} (the plate's back at {F(x.origin.x)})");
        run.Check(b.max.x <= p.bladeFace + .0005f, $"nothing of the blade sign is inside the wall (its east edge at x {F(b.max.x)}, the wall's face at {F(p.bladeFace)})");
        run.Check(b.min.y >= BladeLowest, $"the blade sign is over everyone's head: its lowest point at {F(b.min.y)} m (Ace's capsule is 2.0 m tall)");
        float cars = CarClearance(run, b, out string car);
        run.Check(cars >= CarClear, float.IsPositiveInfinity(cars) ? "no car routes to keep clear of" : $"the blade sign is {F(cars, "0.00")} m from the nearest car's path ({car})");
    }

    static void CheckFaces(Shared.Run run, Plan p, Visual v)
    {
        for (int i = 0; i < p.faces.Count; i++)
        {
            Placement x = p.faces[i];
            Board b = p.boards[i];
            Renderer r = x.Renderer;
            float lift = Mathf.Max(0f, b.proud) + FaceProud;
            Visual.Hit seen = v.Ray(b.centre + b.normal * .3f, -b.normal, .4f, p.SkipMoving);
            run.Check(seen.ok && seen.renderer == r, $"{x.name} is the first thing seen on its board ({(seen.ok ? Shared.PathOf(seen.renderer.transform) : "nothing")})");
            // Long enough to reach the board when the old chalk lines stand proud of it (61 mm on the patio A-frame, 10 Oct: the
            // 5 cm ray stopped short and the check read "? mm").
            Visual.Hit under = v.Ray(b.centre + b.normal * (lift - .0003f), -b.normal, Mathf.Max(.05f, lift + .01f), o => o == r || p.SkipMoving(o));
            run.Check(under.ok && under.renderer == p.aFrame && under.distance <= lift + .001f,
                      $"{x.name} lies on the A-frame's board, {(under.ok ? F((under.distance + .0003f) * 1000f, "0.0") : "?")} mm off it");
        }
        run.Check(p.aFrame.transform.position == p.aFrameAt && p.aFrame.transform.rotation == p.aFrameTurn, "the patio A-frame hasn't moved");
        Material[] mats = p.aFrame.sharedMaterials;
        run.Check(mats[p.chalkSlot] == mats[p.boardSlot], $"the A-frame's old chalk lines wear its chalkboard ({Name(mats[p.chalkSlot])})");
    }

    static void CheckMosaic(Shared.Run run, Plan p, Visual v)
    {
        Placement x = p.mosaic;
        Renderer r = x.Renderer;
        int on = 0;
        foreach (float px in new[] { -1.1f, 0f, 1.1f })
            foreach (float pz in new[] { .3f, .8f, 1.3f })
            {
                Visual.Hit top = v.Ray(new Vector3(px, .3f, pz), Vector3.down, .5f, p.SkipMoving);
                Visual.Hit floor = v.Ray(new Vector3(px, .3f, pz), Vector3.down, .5f, o => o == r || p.SkipMoving(o));
                if (top.ok && top.renderer == r && floor.ok && floor.renderer == p.floor && Mathf.Abs(floor.point.y - p.floorY) <= .001f) on++;
            }
        run.Check(on == 9, $"the mosaic is the top of the floor inside the door and lies on it ({on}/9 points)");
    }

    static void CheckBench(Shared.Run run, Plan p)
    {
        Placement x = p.bench;
        Bounds b = Shared.RendererBounds(x.placed.transform);
        BoxCollider box = x.placed.GetComponentInChildren<BoxCollider>(true);
        run.Check(box != null && Near(box.bounds.min, b.min, .01f) && Near(box.bounds.max, b.max, .01f),
                  $"the bench's box is its solid: {(box != null ? Shared.BoundsText(box.bounds) : "none")} (the bench {Shared.BoundsText(b)})");
        int standing = 0;
        foreach (Vector3 at in new[] { b.center, new Vector3(b.min.x + .05f, 0f, b.min.z + .05f), new Vector3(b.max.x - .05f, 0f, b.min.z + .05f),
                                       new Vector3(b.min.x + .05f, 0f, b.max.z - .05f), new Vector3(b.max.x - .05f, 0f, b.max.z - .05f) })
        {
            RaycastHit? h = Shared.Down(run, new Vector3(at.x, b.max.y + .3f, at.z), 2f, c => c == box);
            if (h.HasValue && Mathf.Abs(h.Value.point.y - b.min.y) <= .01f) standing++;
        }
        run.Check(standing == 5, $"the bench stands on the pavement ({standing}/5 points within 1 cm of its feet)");
        Collider[] inside = Physics.OverlapBox(b.center, b.extents - Vector3.one * .01f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        List<Collider> others = (inside ?? new Collider[0]).Where(c => c != box).ToList();
        run.Check(others.Count == 0, others.Count == 0 ? "nothing solid stands inside the bench" : "solid inside the bench: " + string.Join(", ", others.Select(c => Shared.PathOf(c.transform))));
        BenchClearances(run, p, b, false);
    }

    // ================================================================== what the rest of the game sees

    // The front isn't a cut-away wall, so nothing on it fades and nothing needs registering. After the change: none of
    // the new pieces would be caught by a drawn cut-away wall (CutawayWall's own rule: near a wall's face, above its sill,
    // and hung high or flat on it), and none is an overhead fixture (they stand outside or lie on the floor: the overhead
    // view must show them).
    static void CutawayFacts(Shared.Run run, Plan p, bool after)
    {
        CafeViewMode view = Shared.InScene<CafeViewMode>(run).FirstOrDefault();
        if (view == null) { run.Info("No CafeViewMode in the scene: no cut-away walls or overhead fixtures to think about."); return; }
        List<Renderer> walls = view.cutawayWalls.Where(w => w != null).ToList();
        if (!after)
        {
            run.Info("The overhead view's cut-away walls (CafeViewMode.cutawayWalls): " +
                     string.Join(", ", walls.Select(w => $"{w.name} ({(w.enabled ? "drawn" : "not drawn, never cut")}, {Shared.BoundsText(w.bounds)})")) +
                     ". The front isn't one: it is glass and frames and stays drawn from every angle, so the signs on it stay drawn too. Nothing to register " +
                     "(research §7.5's \"every sign on the front glass in the front wall's hanging list\" and the textures report's placement table assumed a front cut-away wall the scene doesn't have).");
            return;
        }
        SerializedProperty sill = new SerializedObject(view).FindProperty("cutawayHeight");
        float cut = sill != null ? sill.floatValue : .78f;
        foreach (Placement x in p.Pieces)
        {
            Renderer r = x.Renderer;
            string wall = walls.Where(w => w.enabled && Hangs(r.bounds, w.bounds, cut)).Select(w => w.name).FirstOrDefault();
            run.Check(wall == null, wall == null ? $"{x.name} hangs on no cut-away wall (it stays drawn in the overhead view)"
                                                 : $"{x.name}: CutawayWall's rule would fade it with {wall} in the overhead view, a wall it isn't on. Find out why before going on");
            run.Check(!view.overheadFixtures.Contains(r), $"{x.name} isn't an overhead fixture (the overhead view shows it)");
        }
    }

    // CutawayWall.FindDecor's rule for what fades with a wall (Reach 0.55, MountedAbove 1.14, the flat-on-it test), for a
    // single piece (each of ours is its own object: the step's group is far wider than CutawayWall's 2.6 m object box).
    static bool Hangs(Bounds b, Bounds w, float sill)
    {
        const float Reach = .55f, MountedAbove = 1.14f, FlatGap = .05f, FlatDepth = .12f, FlatHeight = .35f, Candidates = 4f;
        Bounds near = w;
        near.Expand(Candidates * 2f);
        if (!near.Intersects(b)) return false;
        float cut = w.min.y + Mathf.Clamp(sill, 0f, Mathf.Max(0f, w.size.y - .05f));
        if (b.max.y <= cut + .02f) return false;
        bool normalX = w.size.x < w.size.z;
        float a = normalX ? b.min.x : b.min.z, e = normalX ? b.max.x : b.max.z;
        float s0 = normalX ? w.min.x : w.min.z, s1 = normalX ? w.max.x : w.max.z;
        if (e < s0 - Reach || a > s1 + Reach) return false;
        float c = normalX ? b.center.z : b.center.x, t0 = normalX ? w.min.z : w.min.x, t1 = normalX ? w.max.z : w.max.x;
        if (c < t0 - .1f || c > t1 + .1f) return false;
        float gap = Mathf.Max(0f, Mathf.Max(a - s1, s0 - e));
        float depth = Mathf.Max(0f, e - s1) + Mathf.Max(0f, s0 - a);
        float across = normalX ? b.size.x : b.size.z, along = normalX ? b.size.z : b.size.x, length = normalX ? w.size.z : w.size.x;
        if (gap > Reach || across > 1.2f || along > length + .3f || b.size.y > 3.4f) return false;
        return b.min.y >= MountedAbove || gap <= FlatGap && depth <= FlatDepth && b.size.y >= FlatHeight && b.min.y >= cut - .05f;
    }

    // What the night does with each piece: NightCollisionList's own rule, mesh by mesh (it lists a visible mesh that is low
    // enough to touch Ace, outside the café room and without a collider of its own). Before: the plan's boxes; after: the
    // placed meshes, with the number Night walk 2's rebuild should add.
    static void NightFacts(Shared.Run run, Plan p, bool after)
    {
        Rect cafe = CafeDaylight.CafeInside;
        string Fate(Bounds b, bool solid) =>
            b.min.y > NightListBelow ? "too high to touch Ace: not on the night's list"
            : b.min.x >= cafe.xMin && b.max.x <= cafe.xMax && b.min.z >= cafe.yMin && b.max.z <= cafe.yMax ? "inside the café room: not on the night's list"
            : solid ? "solid on its own (its box), by day and by night: Night walk 2 counts it with the things already solid"
            : "listed: exact collision while the night runs, as everything outside has";
        if (!after)
        {
            foreach (Placement x in p.Pieces) run.Info($"By night, {x.name}: {Fate(x.Expected, x.kit == Bench)}.");
            return;
        }
        int listed = 0;
        foreach (Placement x in p.Pieces)
            foreach (MeshFilter f in x.placed.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = f.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || f.sharedMesh == null || mr.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                // The night's rule looks only at the mesh's own object (a bench part without its own box is listed: solid by night either way).
                bool solid = f.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger);
                string fate = Fate(mr.bounds, solid);
                if (fate.StartsWith("listed", StringComparison.Ordinal)) listed++;
                run.Info($"By night, {x.name} ({f.sharedMesh.name}): {fate}.");
            }
        run.Info($"Night walk 2's rebuild should list {listed} more mesh(es) than before this step, and count the bench's with the things already solid.");
    }

    static readonly HashSet<string> AnchorNames = new HashSet<string> { "IntakeShelf", "CounterQueue", "SpawnPoint", "PatronSpawnPoint", "Open door collision" };

    static Dictionary<Transform, Vector3> Anchors(Shared.Run run)
    {
        var a = new Dictionary<Transform, Vector3>();
        void Add(Transform t) { if (t != null && !a.ContainsKey(t)) a[t] = t.position; }
        foreach (StationInteractable s in Shared.InScene<StationInteractable>(run)) { Add(s.transform); Add(s.StandPoint); }
        foreach (WaitingSpot w in Shared.InScene<WaitingSpot>(run)) { Add(w.transform); Add(w.StandPoint); }
        foreach (Transform t in Shared.InScene<Transform>(run))
            if (AnchorNames.Contains(t.name))
            {
                Add(t);
                foreach (Transform c in t) Add(c);
            }
        return a;
    }

    static readonly Vector3 LaneCentre = new Vector3(0f, 1f, 1f), LaneHalf = new Vector3(1f, .95f, 1f);

    static HashSet<Collider> LaneColliders() =>
        new HashSet<Collider>(Physics.OverlapBox(LaneCentre, LaneHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) ?? new Collider[0]);

    // ================================================================== photos (before and after, from the same places)

    static void Photos(Shared.Run run)
    {
        Plan p = plan;
        Shared.Photo(run, "01-street-front-13m", new Vector3(0f, 1.65f, -13f), new Vector3(0f, 2f, 0f), 50f);
        Shared.Photo(run, "02-street-front-6m", new Vector3(0f, 1.65f, -6f), new Vector3(0f, 2.1f, 0f), 55f);
        float fasciaY = p?.fascia != null ? p.fascia.Expected.center.y : 2.9f;
        Shared.Photo(run, "03-fascia", new Vector3(0f, 1.65f, -3.2f), new Vector3(0f, fasciaY, -.3f), 50f);
        Vector3 blade = p?.blade != null ? p.blade.Expected.center : new Vector3(-7.4f, 2.6f, .8f);
        Shared.Photo(run, "04-corner-and-blade-sign", new Vector3(-10f, 1.65f, -8f), blade, 45f);
        Vector3 frame = p?.aFrame != null ? p.aFrame.bounds.center : new Vector3(3.6f, .5f, -2f);
        Shared.Photo(run, "05-a-frame", new Vector3(2.5f, 1.4f, -3.5f), frame, 50f);
        Vector3 bench = p?.bench != null ? p.bench.Expected.center : new Vector3(-5.9f, .25f, -.5f);
        Shared.Photo(run, "06-bench", new Vector3(bench.x + 2.8f, 1.65f, -4.2f), bench, 50f);
        Shared.GameCameraPhoto(run, "07-game-camera");
        Shared.Photo(run, "08-mosaic-from-the-door", new Vector3(0f, 1.65f, -.9f), new Vector3(0f, 0f, 1.6f), 60f);
        Vector3 gilding = p?.gildingL != null ? p.gildingL.Expected.center : new Vector3(-2.7f, 1.7f, -.1f);
        Shared.Photo(run, "09-gilding-from-inside", new Vector3(0f, 1.65f, 3f), gilding, 60f);
        // Room 1's olive tree at (-1.6, 0.5) may stand in the brief's view above; this one looks past it, from the west.
        Shared.Photo(run, "10-gilding-from-inside-past-the-tree", new Vector3(gilding.x - 1.4f, 1.65f, 1.8f), gilding, 55f);
        Shared.Photo(run, "11-gilding-from-the-street", new Vector3(gilding.x + .6f, 1.6f, -2.4f), gilding, 45f);
    }

    // ================================================================== the meshes, as rays see them

    // The visible meshes in a box, read once into world-space triangles (Edit Mode hands any mesh's data over), with a grid
    // over the ground for speed. A ray finds the nearest triangle it crosses, either face, optionally skipping glass, opaque
    // surfaces or whole renderers, and counting only hits where `where` holds, on faces within acos(facing) of square to it.
    // Glass: a material named "...Glass", a transparent surface, or a transparent queue.
    sealed class Visual
    {
        public struct Hit
        {
            public bool ok;
            public float distance;
            public Vector3 point;
            public Renderer renderer;
            public Material material;
            public bool glass;
        }

        struct Tri { public Vector3 a, e1, e2, n, min, max; public int part; }
        sealed class Part { public Renderer renderer; public Material material; public bool glass; }

        const float Cell = .25f;
        readonly List<Tri> tris = new List<Tri>();
        readonly List<Part> parts = new List<Part>();
        readonly Bounds roi;
        readonly int nx, nz;
        readonly List<int>[] cells;
        readonly int[] seen;
        int stamp;
        public int Renderers { get; private set; }
        public int Triangles => tris.Count;
        public readonly List<string> unreadable = new List<string>();

        public Visual(IEnumerable<MeshRenderer> renderers, Bounds roi)
        {
            this.roi = roi;
            nx = Mathf.Max(1, Mathf.CeilToInt(roi.size.x / Cell));
            nz = Mathf.Max(1, Mathf.CeilToInt(roi.size.z / Cell));
            cells = new List<int>[nx * nz];
            foreach (MeshRenderer r in renderers) Add(r);
            seen = new int[tris.Count];
        }

        int CellX(float x) => Mathf.Clamp(Mathf.FloorToInt((x - roi.min.x) / Cell), 0, nx - 1);
        int CellZ(float z) => Mathf.Clamp(Mathf.FloorToInt((z - roi.min.z) / Cell), 0, nz - 1);

        void Add(MeshRenderer r)
        {
            if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return;
            if (!r.bounds.Intersects(roi)) return;
            MeshFilter f = r.GetComponent<MeshFilter>();
            Mesh m = f != null ? f.sharedMesh : null;
            if (m == null) return;
            Vector3[] v;
            try { v = m.vertices; }
            catch (Exception) { v = null; }
            if (v == null || v.Length != m.vertexCount) { unreadable.Add(Shared.PathOf(r.transform)); return; }
            Matrix4x4 toWorld = f.transform.localToWorldMatrix;
            var world = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) world[i] = toWorld.MultiplyPoint3x4(v[i]);
            Material[] mats = r.sharedMaterials;
            Renderers++;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                Material mat = mats.Length == 0 ? null : mats[Mathf.Min(s, mats.Length - 1)];
                int part = parts.Count;
                parts.Add(new Part { renderer = r, material = mat, glass = IsGlass(mat) });
                int[] t = m.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = world[t[i]], b = world[t[i + 1]], c = world[t[i + 2]];
                    Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
                    if (hi.x < roi.min.x || lo.x > roi.max.x || hi.y < roi.min.y || lo.y > roi.max.y || hi.z < roi.min.z || lo.z > roi.max.z) continue;
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-16f) continue;   // no area: nothing to hit
                    int index = tris.Count;
                    tris.Add(new Tri { a = a, e1 = b - a, e2 = c - a, n = n.normalized, min = lo, max = hi, part = part });
                    for (int cx = CellX(lo.x); cx <= CellX(hi.x); cx++)
                        for (int cz = CellZ(lo.z); cz <= CellZ(hi.z); cz++)
                        {
                            int k = cx * nz + cz;
                            if (cells[k] == null) cells[k] = new List<int>();
                            cells[k].Add(index);
                        }
                }
            }
        }

        // always: a renderer that counts whatever its surface (the gilding through the glass).
        public Hit Ray(Vector3 origin, Vector3 direction, float reach, Func<Renderer, bool> skip = null, bool glass = true, bool opaque = true,
                       Renderer always = null, Func<Vector3, bool> where = null, float facing = 0f)
        {
            Vector3 d = direction.normalized, end = origin + d * reach;
            Vector3 lo = Vector3.Min(origin, end), hi = Vector3.Max(origin, end);
            const float slack = 1e-4f;
            stamp++;
            var best = new Hit { distance = float.PositiveInfinity };
            for (int cx = CellX(lo.x); cx <= CellX(hi.x); cx++)
                for (int cz = CellZ(lo.z); cz <= CellZ(hi.z); cz++)
                {
                    List<int> list = cells[cx * nz + cz];
                    if (list == null) continue;
                    foreach (int i in list)
                    {
                        if (seen[i] == stamp) continue;
                        seen[i] = stamp;
                        Tri t = tris[i];
                        if (t.max.x < lo.x - slack || t.min.x > hi.x + slack || t.max.y < lo.y - slack || t.min.y > hi.y + slack || t.max.z < lo.z - slack || t.min.z > hi.z + slack) continue;
                        Part part = parts[t.part];
                        if (part.renderer != always)
                        {
                            if (part.glass ? !glass : !opaque) continue;
                            if (skip != null && skip(part.renderer)) continue;
                        }
                        if (facing > 0f && Mathf.Abs(Vector3.Dot(t.n, d)) < facing) continue;
                        // Möller-Trumbore, both faces.
                        Vector3 pv = Vector3.Cross(d, t.e2);
                        float det = Vector3.Dot(t.e1, pv);
                        if (Mathf.Abs(det) < 1e-12f) continue;
                        float inv = 1f / det;
                        Vector3 tv = origin - t.a;
                        float u = Vector3.Dot(tv, pv) * inv;
                        if (u < -1e-5f || u > 1f + 1e-5f) continue;
                        Vector3 qv = Vector3.Cross(tv, t.e1);
                        float w = Vector3.Dot(d, qv) * inv;
                        if (w < -1e-5f || u + w > 1f + 1e-5f) continue;
                        float distance = Vector3.Dot(t.e2, qv) * inv;
                        if (distance < 0f || distance > reach || distance >= best.distance) continue;
                        Vector3 point = origin + d * distance;
                        if (where != null && !where(point)) continue;
                        best = new Hit { ok = true, distance = distance, point = point, renderer = part.renderer, material = part.material, glass = part.glass };
                    }
                }
            return best.ok ? best : new Hit();
        }
    }

    static bool IsGlass(Material m) =>
        m != null && (Has(m.name, "Glass") || m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f || m.renderQueue >= 2900);

    // One sub-mesh's triangles in world space; null when the mesh can't be read.
    static List<Vector3[]> WorldTriangles(MeshFilter f, int submesh)
    {
        Mesh m = f.sharedMesh;
        Vector3[] v;
        try { v = m.vertices; }
        catch (Exception) { return null; }
        if (v == null || v.Length != m.vertexCount || m.GetTopology(submesh) != MeshTopology.Triangles) return null;
        Matrix4x4 toWorld = f.transform.localToWorldMatrix;
        int[] t = m.GetTriangles(submesh);
        var list = new List<Vector3[]>(t.Length / 3);
        for (int i = 0; i + 2 < t.Length; i += 3)
            list.Add(new[] { toWorld.MultiplyPoint3x4(v[t[i]]), toWorld.MultiplyPoint3x4(v[t[i + 1]]), toWorld.MultiplyPoint3x4(v[t[i + 2]]) });
        return list;
    }

    // ================================================================== geometry

    static Bounds MinMax(Vector3 min, Vector3 max)
    {
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    static Bounds WorldBox(Vector3 min, Vector3 max, Vector3 origin, Quaternion rotation, float scale) =>
        Transformed(MinMax(min, max), Matrix4x4.TRS(origin, rotation, Vector3.one * scale));

    static Bounds Transformed(Bounds b, Matrix4x4 m)
    {
        Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 w = m.MultiplyPoint3x4(corner);
            lo = Vector3.Min(lo, w);
            hi = Vector3.Max(hi, w);
        }
        return MinMax(lo, hi);
    }

    static bool Within(Bounds inner, Bounds outer) =>
        inner.min.x >= outer.min.x && inner.max.x <= outer.max.x && inner.min.y >= outer.min.y && inner.max.y <= outer.max.y && inner.min.z >= outer.min.z && inner.max.z <= outer.max.z;

    static bool Near(Vector3 a, Vector3 b, float slack) => Mathf.Abs(a.x - b.x) <= slack && Mathf.Abs(a.y - b.y) <= slack && Mathf.Abs(a.z - b.z) <= slack;

    // The gap between two boxes across the ground (x and z); 0 when their footprints overlap.
    static float Gap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The distance across the ground from a box's footprint to a segment: 0 when the segment crosses it.
    static float BoxToSegment(Bounds box, Vector3 a, Vector3 b)
    {
        float x0 = box.min.x, x1 = box.max.x, z0 = box.min.z, z1 = box.max.z;
        // Liang-Barsky: does the segment cross the rectangle?
        float t0 = 0f, t1 = 1f, dx = b.x - a.x, dz = b.z - a.z;
        bool crosses = true;
        foreach ((float pp, float q) in new[] { (-dx, a.x - x0), (dx, x1 - a.x), (-dz, a.z - z0), (dz, z1 - a.z) })
        {
            if (Mathf.Abs(pp) < 1e-9f) { if (q < 0f) { crosses = false; break; } continue; }
            float r = q / pp;
            if (pp < 0f) { if (r > t1) { crosses = false; break; } if (r > t0) t0 = r; }
            else { if (r < t0) { crosses = false; break; } if (r < t1) t1 = r; }
        }
        if (crosses) return 0f;
        float best = Mathf.Min(PointToBox(a.x, a.z, x0, x1, z0, z1), PointToBox(b.x, b.z, x0, x1, z0, z1));
        foreach ((float cx, float cz) in new[] { (x0, z0), (x0, z1), (x1, z0), (x1, z1) })
            best = Mathf.Min(best, PointToSegment(cx, cz, a.x, a.z, b.x, b.z));
        return best;
    }

    static float PointToBox(float x, float z, float x0, float x1, float z0, float z1)
    {
        float dx = Mathf.Max(0f, Mathf.Max(x0 - x, x - x1)), dz = Mathf.Max(0f, Mathf.Max(z0 - z, z - z1));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float PointToSegment(float px, float pz, float ax, float az, float bx, float bz)
    {
        float dx = bx - ax, dz = bz - az, len = dx * dx + dz * dz;
        float t = len < 1e-12f ? 0f : Mathf.Clamp(((px - ax) * dx + (pz - az) * dz) / len, 0f, 1f);
        float ex = ax + dx * t - px, ez = az + dz * t - pz;
        return Mathf.Sqrt(ex * ex + ez * ez);
    }

    // A direction's heading, degrees clockwise from north (+z), 0..360.
    static float Heading(Vector3 v) => Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f);

    // ================================================================== words

    static string F(float f, string format = "0.000") => Shared.F(f, format);
    static string Name(Material m) => m != null ? m.name : "(none)";
    static bool Has(string text, string part) => text != null && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // Consecutive samples of one kind, in the order scanned: (the first sample's place, the last's).
    static List<(float from, float to)> Runs(List<(float at, char what)> samples, char kind)
    {
        var runs = new List<(float, float)>();
        int start = -1;
        for (int i = 0; i <= samples.Count; i++)
        {
            bool match = i < samples.Count && samples[i].what == kind;
            if (match && start < 0) start = i;
            if (!match && start >= 0) { if (i - start >= 3) runs.Add((samples[start].at, samples[i - 1].at)); start = -1; }
        }
        return runs;
    }

    // A scan in a line: "g -1.02..-2.54 | f -2.55..-2.63 | ..." (at most 16 stretches).
    static string Profile(List<(float at, char what)> samples)
    {
        var parts = new List<string>();
        int start = 0;
        for (int i = 1; i <= samples.Count; i++)
        {
            if (i < samples.Count && samples[i].what == samples[start].what) continue;
            parts.Add($"{samples[start].what} {F(samples[start].at, "0.00")}..{F(samples[i - 1].at, "0.00")}");
            start = i;
        }
        return parts.Count <= 16 ? string.Join(" | ", parts) : string.Join(" | ", parts.Take(16)) + $" | ... ({parts.Count - 16} more)";
    }
}
#endif
