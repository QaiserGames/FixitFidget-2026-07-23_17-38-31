#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static CafeRedesignShared;

// ---------------------------------------------------------------------------
// THE CAFÉ'S NEW BAR: ONE FRONT COUNTER, ONE BACK BAR (the café redesign, 10 Oct 2026; his note of 9 Oct, 20:12: "i dont
// want one counter thats just duplicated"; claude/studio/cafe-sf-research-and-redesign.md §6.3-6.4, the bar lead's
// bar/report.md §5, the brief U-bar.md)
//
//   Fixit Fidget > Cafe redesign > Bar 1 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Bar 1 1 - Put the one counter in (scene)
//   Fixit Fidget > Cafe redesign > Bar 1 1 - Undo (scene)              (refused while the repair bar stands in it)
//       Runs BEFORE "Repair bar 1". The four old front-counter modules go off (the three "Matching counter section"s and
//       the intake's Counter_Mesh, with their cubbies, their retail, the desk sign and the pot plant on them). In their
//       place the café kit's one counter, CK_FrontCounter (12.27 m, stone top 1.095, a lowered section at 0.91, the
//       repair bar's opening and ledge, a half-round end), with a MeshCollider of its own mesh; the empty repair bench
//       (CK_RepairBench, no collider: the Workbench's box becomes its solid when the bench moves in) and its magnifier lamp
//       (CK_BenchLamp, with its spot from cafe_bar.json, a warm light). The pastry case moves to the counter's east run
//       (x 3.29 to 4.34, between the lowered section and the half-round end: Claude, 10 Oct, because at x -2.6 to -1.55 it
//       stood in the room of the intake shelf's upgrade slot 4, which "Shelf Space" level 2 unlocks), three tent cards stand
//       on it, register v4 goes on the till's staff half, the bell on the repair bar's ledge, the water station on the
//       half-round end. The counter station's colliders top out at 1.000, under the new stone:
//       a thin trigger under the station, on the stone, keeps the counter in reach of the first-person crosshair.
//   Fixit Fidget > Cafe redesign > Bar 2 0 - Check first (read-only)
//   Fixit Fidget > Cafe redesign > Bar 2 1 - Put the back bar in (scene)
//   Fixit Fidget > Cafe redesign > Bar 2 1 - Undo (scene)
//       Runs AFTER "Repair bar 1" (the bench must have left the back wall). Everything on the back counter and the back
//       wall goes off (the storage cabinet, the drinks counter's shell, the parts cabinet and what stood on it, the
//       pegboard of cartoon tools, the prints, the black rail, the coffee shelf, the three sconces, the clock, the chalk
//       menu, the old tile band and its oak trims, the loose mugs); Night 1's trophy shelf only loses its board and brackets
//       (its renderers: the shelf itself is a gameplay object, found by type at night) and its trophies stand on the new
//       shelf; the green workshop lining takes the service wall's chalk white. In: CK_BackBar (one run, top 1.00 where the
//       dispenser stands), the tile, the long shelf with its seven lights, the parts wall, the menu board with its picture
//       light, the grinder, eight bean bags, two pothos and the staff plate. The dispenser, the cup stack and the discard
//       basin never move.
//
// Both run through CafeRedesignShared.Step: the survey first (it refuses with the reason and changes nothing), the guards
// and photos before, one undo group and the record, the change and its checks in numbers, the routes re-baked, the guards
// after, everything put back if anything failed; the photos after; Logs/CafeRedesign/bar-1-<time>/ and bar-2-<time>/.
// Never saved. Never deleted: what is found is switched off; an Undo deletes only what its step made.
//
// The numbers: every expected box below is the kit's own mesh measured at its placement (steps-bar/analysis/
// kit_geometry.py, from the FBX files), and every scene position is the scene's YAML at 216cc1a (analysis/bar_scene.py).
// ---------------------------------------------------------------------------
internal static class CafeBarSteps
{
    // ================================================================== the steps and their menus

    const string Bar1Title = "Bar 1 - The front counter", Bar1Label = "bar-1", Bar1Group = "Bar 1 - the front counter";
    const string Bar2Title = "Bar 2 - The back bar", Bar2Label = "bar-2", Bar2Group = "Bar 2 - the back bar";
    const string Bar1CheckMenu = CafeRedesignShared.Menu + "Bar 1 0 - Check first (read-only)";
    const string Bar1DoMenu = CafeRedesignShared.Menu + "Bar 1 1 - Put the one counter in (scene)";
    const string Bar1UndoMenu = CafeRedesignShared.Menu + "Bar 1 1 - Undo (scene)";
    const string Bar2CheckMenu = CafeRedesignShared.Menu + "Bar 2 0 - Check first (read-only)";
    const string Bar2DoMenu = CafeRedesignShared.Menu + "Bar 2 1 - Put the back bar in (scene)";
    const string Bar2UndoMenu = CafeRedesignShared.Menu + "Bar 2 1 - Undo (scene)";

    [MenuItem(Bar1CheckMenu)]
    static void Bar1Check() => Check(Bar1Title, Bar1Label, run => SurveyBar1(run));

    [MenuItem(Bar1DoMenu)]
    static void Bar1Do()
    {
        Bar1Plan plan = null;
        Step(Bar1Title, Bar1Label, run => plan = SurveyBar1(run), run => ChangeBar1(run, plan), PhotosBar1);
    }

    [MenuItem(Bar1UndoMenu)]
    static void Bar1Undo() => UndoStep(Bar1Title, Bar1Label, run => RepairBarDone(run)
        ? "The repair bench stands in this counter's opening (Repair bar 1 has run: CafeRepairBar is on the café's root). " +
          "Undo it first (Fixit Fidget > Cafe redesign > Repair bar 1 - Undo), then this."
        : null);

    [MenuItem(Bar2CheckMenu)]
    static void Bar2Check() => Check(Bar2Title, Bar2Label, run => SurveyBar2(run));

    [MenuItem(Bar2DoMenu)]
    static void Bar2Do()
    {
        Bar2Plan plan = null;
        Step(Bar2Title, Bar2Label, run => plan = SurveyBar2(run), run => ChangeBar2(run, plan), PhotosBar2);
    }

    [MenuItem(Bar2UndoMenu)]
    static void Bar2Undo() => UndoStep(Bar2Title, Bar2Label);

    [MenuItem(Bar1CheckMenu, true)]
    [MenuItem(Bar1DoMenu, true)]
    [MenuItem(Bar1UndoMenu, true)]
    [MenuItem(Bar2CheckMenu, true)]
    [MenuItem(Bar2DoMenu, true)]
    [MenuItem(Bar2UndoMenu, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the front counter's numbers

    // CK_FrontCounter (bar/report.md §5; measured from the FBX): one piece, its stone top at 1.095 everywhere but the lowered
    // section (x 0.90..1.85, at 0.91) and the repair bar's opening (x -5.80..-2.60, open from z 13.65 to the back: no top, no
    // floor; the 35 cm ledge in front of it at 1.095). The top's edge: z 13.30 (customers) to 14.24 (Ace).
    const float Top = 1.095f, LowTop = .91f, LowX0 = 2.30f, LowX1 = 3.25f;     // moved east of the intake's slots (x -1.6..1.7) by Claude, 10 Oct
    const float OpeningX0 = -5.80f, OpeningX1 = -2.60f, OpeningZ0 = 13.65f;
    const float TopZ0 = 13.30f, TopZ1 = 14.24f;
    static readonly Vector3 CounterAt = new Vector3(-1.265f, 0f, 13.77f);
    static readonly Vector3 BenchAt = new Vector3(-4.20f, 0f, 14.055f);
    static readonly Vector3 BellAt = new Vector3(-2.85f, Top, 13.47f);
    // The water station on the half-round end (Claude, 10 Oct: the pastry case took x 3.29..4.34, the water's old place).
    // The half-round: centre (4.40, 13.77), radius 0.47 (bar/report.md: the stone top's half circle from x 4.40 to 4.87).
    static readonly Vector3 WaterAt = new Vector3(4.61f, Top, 13.77f);
    static readonly Vector2 HalfRoundCentre = new Vector2(4.40f, 13.77f);
    const float HalfRoundRadius = .47f;

    // The lamp: cafe_bar.json's placement and light (re-aimed after the bar report). Read at run time; these are the
    // values the brief gives, checked within 1 cm.
    static readonly Vector3 LampAt = new Vector3(-5.30f, .85f, 14.41f);
    static readonly Vector3 LampLightLocal = new Vector3(-.266f, .55f, .412f);
    static readonly Vector3 LampLightWorld = new Vector3(-5.034f, 1.40f, 13.998f);
    static readonly Vector3 LampHeadWorld = new Vector3(-5.034f, 1.425f, 13.998f);
    const string LampColour = "#FFE4C2";
    const float LampIntensity = 1.6f, LampRange = 1.5f, LampAngle = 50f;

    // Where each piece's renderers must lie once placed (the kit's meshes at these placements; 1.5 cm allowed).
    static readonly Bounds CounterBox = Box(-7.400f, 0f, 13.160f, 4.870f, 1.095f, 14.240f);
    static readonly Bounds BenchBox = Box(-5.797f, 0f, 13.660f, -2.603f, 1.430f, 14.451f);
    static readonly Bounds LampBox = Box(-5.3285f, .85f, 13.864f, -4.944f, 1.6148f, 14.472f);
    static readonly Bounds BellBox = Box(-2.890f, 1.095f, 13.430f, -2.810f, 1.1692f, 13.510f);
    static readonly Bounds WaterBox = Box(4.410f, 1.095f, 13.645f, 4.810f, 1.383f, 13.895f);
    const float BenchTop = 1.005f;                                // the bench's chestnut top
    // The bench's glass screen and its east post (the bell must clear them) and Repair bar 1's own space for the bench
    // (CafeRepairBarSteps.Blockers: renderers within x -5.78..-2.62 count as in the way).
    const float BenchEastPost = -2.62f;

    // The pastry case: on the counter's east run, between the lowered section (to x 3.25) and the half-round end (from x 4.40),
    // centred at x 3.81 (x 3.285..4.335 for its 1.05 m); its z and facing kept. The research had it at x -1.9..-0.9 and the
    // brief at -2.6..-1.6; the intake's five slots (x -1.56..1.56: three base, two "Shelf Space" upgrades) leave no metre
    // there, and a device on upgrade slot 4 would have stood inside the case (Claude, 10 Oct).
    const float CaseCentreX = 3.81f, CaseRunX0 = LowX1 + .02f, CaseRunX1 = 4.38f;
    // Register v4 (Assets/Art/Models/CashRegister.fbx): on the till's staff half. Its x is chosen in the survey from these,
    // the first that keeps the conversation camera's view of every queue place clear and the intake's devices free.
    const string RegisterPath = "Assets/Art/Models/CashRegister.fbx";
    const float RegisterZ = 13.95f;
    static readonly float[] RegisterXs = { .10f, .15f, .05f, .20f, 0f };
    // A device waiting on an intake slot: what must stay free round the slot (24 cm square, 24 cm high).
    static readonly Vector3 DeviceRoom = new Vector3(.24f, .24f, .24f);
    // The station's reach over the stone: a thin trigger on the top, from the case's east end to the lowered section.
    const float ReachY0 = 1.096f, ReachY1 = 1.106f, ReachX1 = 2.25f;          // to the lowered section (now at x 2.30)

    // ---- the pieces' names (the night and the labs find things by name: unique, and saying what they are) ----
    const string CounterName = "CK_FrontCounter (the one front counter)";
    const string BenchName = "CK_RepairBench (the repair bar's bench, in the counter's opening)";   // Repair bar 1 finds "CK_RepairBench*"
    const string LampName = "CK_BenchLamp (magnifier lamp, clamped to the bench)";                  // Repair bar 1 finds "CK_BenchLamp*"
    const string LampLightName = "Bench lamp light (spot, cafe_bar.json)";
    const string RegisterName = "Register v4 (the till: keys and drawer to Ace, the tower to the customers)";
    static readonly string[] CardNames = { "CK_TentCard (pastry case, Morning bun)", "CK_TentCard (pastry case, Almond croissant)", "CK_TentCard (pastry case, Banana bread)" };
    const string BellName = "CK_ServiceBell (the repair bar's ledge)";
    const string WaterName = "CK_WaterStation (the counter, on the half-round end)";
    const string ReachName = "Counter - reach over the stone top (Bar 1, trigger)";

    // ---- the scene's pieces, by their paths under the café's root (YAML at 216cc1a) ----
    const string ShellPath = "Counter extensions and cabinetry";
    const string SectionName = "Matching counter section";
    const string IntakeGroupPath = "02 - intake and queue";
    const string StationPath = "02 - intake and queue/Counter";
    const string IntakeMeshPath = "02 - intake and queue/Counter/Counter_Mesh";
    const string IntakeShelfPath = "02 - intake and queue/IntakeShelf";
    const string QueuePath = "02 - intake and queue/CounterQueue";
    const string CounterStandPath = "02 - intake and queue/CounterStand";
    const string ConversationCamPath = "02 - intake and queue/CM_ConversationCam";
    const string CounterCamPath = "02 - intake and queue/CmCounterCam";
    const string WorkAreaPath = "03 - repair work area";
    const string DrinksGroupPath = "04 - drinks work area";
    const string CubbiesPath = "18 - cafe furnishing/E - Counter cubbies";
    const string DeskSignPath = "18 - cafe furnishing/G - Signs and menu/Ace's Cafe counter sign";
    const string CounterPlantPath = "16 - POLYGON cafe details/Counter/Counter - PotPlant_01";
    const string CasePath = "12 - authored cafe interior/Cafe service details/PastryDisplay";
    const string ClampLampPath = "23 - real cafe pass/Bench lamp and tool stand/Clamp lamp";

    // ================================================================== the back bar's numbers

    const float BackTop = 1.00f;
    static readonly Vector3 BackBarAt = new Vector3(-1.25f, 0f, 18.00f);
    static readonly Vector3 ShelfAt = new Vector3(.55f, 1.75f, 18.00f);
    static readonly Vector3 SplashAt = new Vector3(.55f, 1.00f, 18.00f);
    static readonly Vector3 PartsAt = new Vector3(-5.60f, 1.00f, 18.00f);
    static readonly Vector3 MenuAt = new Vector3(0f, 1.98f, 18.00f);
    static readonly Vector3 GrinderAt = new Vector3(4.25f, 1.00f, 17.50f);
    static readonly float[] GrinderXs = { 4.25f, 4.20f, 4.15f, 4.10f, 4.05f, 4.00f };      // the brief's place first, then west along the bar
    static readonly Vector3 PothosWestAt = new Vector3(-3.68f, 1.81f, 17.82f);
    static readonly Vector3 PothosEastAt = new Vector3(4.78f, 1.81f, 17.82f);
    const float BagX0 = -3.20f, BagStep = .135f, BagY = 1.81f, BagZ = 17.85f;
    const int Bags = 8;
    static readonly float[] ShelfLightXs = { -3.0f, -1.8f, -.6f, .6f, 1.8f, 3.0f, 4.2f };
    const float ShelfLightY = 1.70f, ShelfLightZ = 17.78f;
    const string ShelfLightColour = "#FFD7A8";
    const float ShelfLightIntensity = .6f, ShelfLightRange = 1.2f;
    // The menu's picture light: the textures report's spot (local (0, 1.096, 0.21), aimed 35° down at the board, #FFD7A8,
    // 0.5). Range and angle are this step's: 1.6 m reaches the board's bottom edge (1.1 m away), 120° spreads it along
    // the 2.6 m board from 0.2 m in front of it.
    static readonly Vector3 MenuLightLocal = new Vector3(0f, 1.096f, .21f);
    const float MenuLightDown = 35f, MenuLightIntensity = .5f, MenuLightRange = 1.6f, MenuLightAngle = 120f;
    const float PlateBottom = 1.46f;

    static readonly Bounds BackBarBox = Box(-7.400f, 0f, 16.8885f, 4.900f, 1.000f, 18.000f);
    static readonly Bounds ShelfBox = Box(-3.800f, 1.550f, 17.700f, 4.900f, 1.810f, 18.000f);
    static readonly Bounds SplashBox = Box(-3.800f, .998f, 17.978f, 4.906f, 1.752f, 18.000f);
    static readonly Bounds PartsBox = Box(-7.400f, .998f, 17.6815f, -3.800f, 1.750f, 17.999f);
    static readonly Bounds MenuBox = Box(-1.300f, 1.980f, 17.755f, 1.300f, 3.120f, 18.000f);
    static readonly Bounds MenuFrame = Box(-1.300f, 1.980f, 17.955f, 1.300f, 2.980f, 18.000f);   // its chestnut frame, on the wall
    static readonly Bounds GrinderBox = Box(4.130f, 1.000f, 17.295f, 4.370f, 1.595f, 17.705f);
    static readonly Bounds PothosWestBox = Box(-3.8344f, 1.1898f, 17.640f, -3.3084f, 2.0062f, 17.9422f);
    static readonly Bounds PothosEastBox = Box(4.4084f, 1.1898f, 17.640f, 4.9344f, 2.0062f, 17.9422f);
    // CK_BeanBag's own box (local, bottom centre at its origin): x ±0.0602, y 0..0.2145, z -0.036..0.0366.
    static readonly Bounds BagLocal = Box(-.0602f, 0f, -.036f, .0602f, .2145f, .0366f);
    // The colliders: the solid, never the bounds (bar/report.md §5, U-bar.md).
    static readonly Bounds BackBarSolid = Box(-7.40f, 0f, 16.90f, 4.90f, 1.00f, 18.00f);
    static readonly Bounds ShelfSolid = Box(-3.80f, 1.75f, 17.70f, 4.90f, 1.81f, 18.00f);
    static readonly Bounds PartsSolid = Box(-7.40f, 1.00f, 17.70f, -3.80f, 1.75f, 18.00f);
    static readonly Bounds GrinderSolid = Box(4.13f, 1.00f, 17.295f, 4.37f, 1.60f, 17.705f);

    // The shelf's parts that reach down toward the drinks bay: the board with its light channel, and the eight brackets
    // (4 cm wide, down to 1.55, standing off the wall from z 17.745).
    static readonly float[] BracketXs = { -3.60f, -2.40f, -1.20f, 0f, 1.20f, 2.40f, 3.60f, 4.65f };
    static IEnumerable<(string what, Bounds box)> ShelfParts() =>
        new[] { ("the shelf's board and light channel", Box(-3.80f, 1.733f, 17.70f, 4.90f, 1.81f, 18.0f)) }
            .Concat(BracketXs.Select(x => ($"the shelf's bracket at x {F(x, "0.00")}", Box(x - .02f, 1.55f, 17.745f, x + .02f, 1.752f, 17.98f))));

    const string BackBarName = "CK_BackBar (the back bar run)";
    const string ShelfName = "CK_BackBarShelf (the long shelf)";
    const string SplashName = "CK_Backsplash (tile, 1.00 to 1.75)";
    const string PartsName = "CK_PartsWall (72 drawers, the workshop end)";
    const string MenuName = "CK_MenuBoard (back wall, over the till)";
    const string MenuLightName = "Menu board picture light (spot)";
    const string GrinderName = "CK_Grinder (back bar, beside the dispenser)";
    const string PothosWestName = "CK_Pothos (shelf, west end)";
    const string PothosEastName = "CK_Pothos (shelf, east end, mirrored)";
    const string PlateName = "CK_StaffPlate (back door, STAFF ONLY)";

    const string DispenserPath = "04 - drinks work area/Beverage dispenser";
    const string CupStackPath = "04 - drinks work area/Beverage dispenser/Cup stack";
    const string BasinPath = "04 - drinks work area/Beverage dispenser/Discard basin";
    const string DrinkStandPath = "04 - drinks work area/Beverage dispenser/Drink stand point";
    const string TrophyShelfPath = "22 - Night 1 slice/Ace's trophy shelf";
    // Where the trophy shelf goes on the long shelf: the free stretch between the menu board's frame (to x 1.30) and the
    // dispenser (Claude, 10 Oct).
    const float TrophyShelfX = 1.75f;
    const string BackDoorPath = "24 - The bins (Night 0)/Back door (inside)";
    const string AceAtTheDoorPath = "24 - The bins (Night 0)/Ace - inside the back door";
    const string ChalkWallPath = "23 - real cafe pass/Wall finishes/Service wall - above the tiles";
    static readonly string[] GreenLinings =
    {
        "23 - real cafe pass/Wall finishes/Workshop wall", "23 - real cafe pass/Wall finishes/Workshop wall - below the sill",
        "23 - real cafe pass/Wall finishes/Tan wall", "23 - real cafe pass/Wall finishes/Tan wall - below the sill",
    };
    const string CeilingPath = "06 - atmosphere placeholders/Inside-only ceiling";
    // The pot plant Night 0 stood at the back counter's east end (one of four "Plants - PotPlant_02", told by where it stands).
    const string BackPotPlantGroup = "16 - POLYGON cafe details/Plants", BackPotPlantName = "Plants - PotPlant_02";
    static readonly Vector3 BackPotPlantAt = new Vector3(4.95f, .02f, 17.55f);

    // What goes off at the back, by path (one entry may stand for several pieces of the same name, told apart by where
    // they stand), and why. Research §5 items 2-3 and §7.3, U-bar.md. Every name was grepped in Assets/Editor and
    // Assets/Scripts: only one-time builders (CafeBackBar, CafeDrinksCorner, CafeInteriorPass, CafeRealismPass,
    // CafeFurnishing, AcesCafeLayoutSetup's first build), NightZeroSteps' Bins 2 (already run; it refuses to run twice)
    // and CafeCutawayCheck (its expected list, updated with this step: steps-bar/modified/CafeCutawayCheck.cs) know them.
    static readonly (string path, int count, string why)[] BackOff =
    {
        ("Counter extensions and cabinetry/Rear storage", 1, "the storage cabinet (x -1.95..+1.54): the back bar's run replaces it"),
        ("Counter extensions and cabinetry/Drink countertop", 1, "the drinks counter's top: the back bar's stone top (1.00, the same height) replaces it"),
        ("Counter extensions and cabinetry/Drink cabinet face", 1, "the drinks counter's doors: the back bar's doors replace them"),
        ("12 - authored cafe interior/Cafe service details/Drink cabinet brass pull", 4, "the drinks counter's pulls: they went with its doors"),
        ("17 - back bar finish", 1, "the old tile band, its oak cap and edges, the oak upstand and the storage top: the back bar's tile and top replace them"),
        ("18 - cafe furnishing/F - Work areas/Parts cabinet", 1, "the parts cabinet (x -5.67..-4.53): the parts-drawer wall and the back bar replace it"),
        ("18 - cafe furnishing/F - Work areas/Radio", 1, "stood on the parts cabinet"),
        ("18 - cafe furnishing/F - Work areas/Cable coil", 1, "stood on the parts cabinet"),
        ("18 - cafe furnishing/F - Work areas/Parts box", 1, "stood on the parts cabinet"),
        ("12 - authored cafe interior/Cafe service details/RepairWallTools", 1, "the pegboard of cartoon tools (\"those tools that are hanging on the wall look terrible\")"),
        ("06 - atmosphere placeholders/Bay sunset print", 1, "a print on the back wall: above the shelf there is white wall and the menu, nothing else"),
        ("09 - neighborhood surrounds V3/V3 - cafe perimeter details/Back bar cup shelf", 1, "the black rail and its brackets: the long shelf replaces it"),
        ("12 - authored cafe interior/Cafe service details/CoffeeShelf", 1, "the coffee shelf and its bags with line labels: the bean bags on the long shelf replace them"),
        ("06 - atmosphere placeholders/Lamp shade", 3, "a sconce on the back wall: the shelf's light strip and the menu's picture light replace the three"),
        ("06 - atmosphere placeholders/Warm lamp diffuser", 3, "a sconce on the back wall (its glow; it has no Light of its own)"),
        ("06 - atmosphere placeholders/Wall lamp stem", 3, "a sconce on the back wall"),
        ("16 - POLYGON cafe details/Walls/Walls - Clock_01", 1, "the clock over the back door"),
        ("18 - cafe furnishing/G - Signs and menu/Chalkboard menu", 1, "the chalk menu by the back door: the menu board over the till replaces it"),
        ("16 - POLYGON cafe details/Drinks/Drinks - Mug_01", 3, "loose mugs on the back counter, where the grinder goes (one stands in it)"),
    };
    // The trophy shelf's own board and brackets (the "floating shelf"): their renderers go, the shelf stays.
    static readonly string[] TrophyShelfParts = { "Oak board", "Bracket", "Bracket arm" };

    // ================================================================== Bar 1: the survey

    internal sealed class Bar1Plan
    {
        public readonly List<(GameObject go, string why)> off = new List<(GameObject, string)>();
        public readonly List<Bounds> oldTops = new List<Bounds>();
        public Transform station, standPoint, intake, caseHolder, conversationCam, counterCam;
        public BoxCollider stationSolid, stationReach;
        public Transform[] slots = Array.Empty<Transform>();
        public int baseSlots;
        public Transform[] queue = Array.Empty<Transform>();
        public CafeDaylight daylight;
        public Vector3 lampAt, lightLocal, lightEuler;
        public float lampYaw, lightIntensity, lightRange, lightAngle;
        public Color lightColour;
        public Vector3 lightWorld, headWorld;
        public Bounds caseWas, caseTo;
        public Vector3 caseShift;
        public readonly List<Vector3> cards = new List<Vector3>();        // where each card stands, after the move
        public string cardsHow = "";
        public Material card2, card3;
        public GameObject registerAsset;
        public Quaternion registerTurn = Quaternion.identity;
        public Bounds registerTo;
        public float registerX;
        public string registerHow = "";
        public float reachX0;
        public Snapshot kept;
    }

    static Bar1Plan SurveyBar1(Run run)
    {
        var p = new Bar1Plan();
        run.Require(!Done(run, Bar1Title), $"{Bar1Title} has already run in this scene (Bar 1 1 - Undo puts it back).");
        run.Require(!RepairBarDone(run), "The repair bench has already moved into the front counter (CafeRepairBar is on the café's root): " +
                                          "Bar 1 runs before Repair bar 1. Undo the repair bar first (Repair bar 1 - Undo).");

        // ---- the kit, the register, the manifest ----
        run.Line("The kit:");
        foreach (string piece in new[] { "CK_FrontCounter", "CK_RepairBench", "CK_BenchLamp", "CK_TentCard", "CK_ServiceBell", "CK_WaterStation" })
            run.Require(AssetDatabase.LoadAssetAtPath<GameObject>($"{KitModels}/{piece}.fbx") != null, $"{KitModels}/{piece}.fbx isn't imported: run Kit 1 - Import the café kit first.");
        p.card2 = AssetDatabase.LoadAssetAtPath<Material>($"{KitMaterials}/CF_TentCard2.mat");
        p.card3 = AssetDatabase.LoadAssetAtPath<Material>($"{KitMaterials}/CF_TentCard3.mat");
        run.Require(p.card2 != null && p.card3 != null, $"{KitMaterials}/CF_TentCard2.mat or CF_TentCard3.mat is missing: run Kit 1 first.");
        p.registerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RegisterPath);
        run.Require(p.registerAsset != null, $"{RegisterPath} (register v4) isn't in the project.");
        run.Info("CK_FrontCounter, CK_RepairBench, CK_BenchLamp, CK_TentCard, CK_ServiceBell, CK_WaterStation, CF_TentCard2/3 and register v4 are in.");
        ReadLampManifest(run, p);

        // ---- the gameplay objects: where this plan measured them ----
        run.Line("The gameplay objects (they stay exactly where they are; 2 cm allowed here, 1 mm after):");
        Transform intakeGroup = Need(run, IntakeGroupPath);
        p.station = Need(run, StationPath);
        StationInteractable station = p.station.GetComponent<StationInteractable>();
        run.Require(station != null, $"{StationPath} has no StationInteractable.");
        foreach (BoxCollider b in p.station.GetComponents<BoxCollider>())
        {
            if (!b.isTrigger && p.stationSolid == null) p.stationSolid = b;
            if (b.isTrigger && p.stationReach == null) p.stationReach = b;
        }
        run.Require(p.stationSolid != null && p.stationReach != null, "The counter station hasn't its solid box and its reach trigger.");
        p.standPoint = station.StandPoint != null ? station.StandPoint : Need(run, StationPath + "/CounterStandPoint");
        p.intake = Need(run, IntakeShelfPath);
        ItemSlotArea area = p.intake.GetComponent<ItemSlotArea>();
        run.Require(area != null && p.intake.GetComponent<IntakeShelf>() != null, "The intake shelf has no IntakeShelf or ItemSlotArea.");
        ReadSlots(area, out p.slots, out p.baseSlots);
        run.Require(p.slots.Length > 0 && p.slots.All(s => s != null), "The intake shelf's slots aren't all set.");
        Transform queue = Need(run, QueuePath);
        CounterQueue q = queue.GetComponent<CounterQueue>();
        run.Require(q != null && q.SlotCount > 0, "The counter queue has no places.");
        p.queue = Enumerable.Range(0, q.SlotCount).Select(i => q.SlotPoint(i)).Where(t => t != null).ToArray();
        p.conversationCam = Need(run, ConversationCamPath);
        p.counterCam = Need(run, CounterCamPath);
        Transform work = Need(run, WorkAreaPath);
        At(run, p.intake, new Vector3(-1.212f, 1.117f, 13.534f), "the intake shelf (IntakeShelf)");
        At(run, p.station, new Vector3(0f, .5f, 13.6f), "the counter station (StationInteractable \"Counter\")");
        BoxAt(run, p.stationSolid, Box(-2f, 0f, 13.1f, 2f, 1f, 14.1f), "its solid box");
        BoxAt(run, p.stationReach, Box(-3f, 0f, 13.35f, 3f, 1f, 15.85f), "its reach trigger");
        At(run, p.standPoint, new Vector3(-.26f, 0f, 14.54f), "the counter's stand point (CounterStandPoint)");
        At(run, queue, new Vector3(0f, 0f, 12.5f), "the queue (CounterQueue)");
        float[] queueX = { 1.6f, 0f, -1.6f };
        for (int i = 0; i < p.queue.Length && i < queueX.Length; i++) At(run, p.queue[i], new Vector3(queueX[i], 0f, 12.5f), $"queue place {i}");
        At(run, Need(run, CounterStandPath), new Vector3(0f, 0f, 12.12f), "CounterStand");
        At(run, p.conversationCam, new Vector3(-.482f, 1.468f, 15.332f), "the conversation camera (CM_ConversationCam)");
        At(run, p.counterCam, new Vector3(0f, 1.883f, 15.311f), "the counter camera (CmCounterCam)");
        if (run.Player != null) At(run, run.Player, new Vector3(0f, 1f, 15.15f), "Ace's start, behind the till (Player; the brief's \"till stand about (0, 15.15)\")");
        At(run, work, new Vector3(-3.30f, .5f, 17.45f), "the repair work area at the back wall, where Repair bar 1 takes it from");
        p.daylight = TheDaylight(run);

        // The counter station's colliders against the new top (the brief: "read how the counter station's colliders sit").
        run.Info($"The counter station's solid box tops out at {F(p.stationSolid.bounds.max.y)} and its reach trigger at {F(p.stationReach.bounds.max.y)}: " +
                 $"{(Top - p.stationSolid.bounds.max.y) * 100f:0.0} cm under the new stone ({F(Top)}). From above nothing changes (the overhead view finds the station by an overlap " +
                 "round Ace, behind the counter by where he stands). In first person the crosshair stops at the first solid that isn't something to use " +
                 "(PlayerInteractor.FindFirstPersonFloorTarget), and the stone's collider would be that: so a thin trigger under the station, on the stone, keeps it in reach.");
        run.Info($"The station's solid box (x -2..+2) reaches z {F(p.stationSolid.bounds.min.z)}: {(13.16f - p.stationSolid.bounds.min.z) * 100f:0} cm in front of the new foot rail and " +
                 $"{(13.34f - p.stationSolid.bounds.min.z) * 100f:0} cm in front of the counter's face (z 13.34); over the lowered section (x {F(LowX0, "0.00")}..{F(LowX1, "0.00")}, " +
                 $"{F(LowTop)}) it stands {(p.stationSolid.bounds.max.y - LowTop) * 100f:0} cm proud, unseen. Unchanged (it is gameplay); a refit is Systems' call.");
        run.Info("The new top is 1.095, not 1.10: nothing in the station or the delivery code puts anything on the counter by its height. A device rests on its intake " +
                 "slot (slot height 1.100 + its own rest height, PlacementJitter; no ray onto the counter), and the counter's DropSpot gives a slot (grep of " +
                 "Assets/Scripts for 1.1f / 1.10f / 1.095: only NpcAttentionDirector's look point, slot + 1.1 forward + 1.45 up).");
        CinemachineCamera counterCam = p.counterCam.GetComponent<CinemachineCamera>();
        if (counterCam != null)
        {
            float fov = counterCam.Lens.FieldOfView;
            float pitch = Vector3.Angle(Vector3.ProjectOnPlane(p.counterCam.forward, Vector3.up), p.counterCam.forward);
            float bottomEdge = pitch + fov * .5f;
            float reachZ = p.counterCam.position.z - (p.counterCam.position.y - Top) / Mathf.Tan(bottomEdge * Mathf.Deg2Rad);
            run.Info($"The counter camera looks {Facing(p.counterCam.rotation):0} from {V(p.counterCam.position)}, {pitch:0}° down, field {fov:0}°: its frame's bottom edge " +
                     $"meets the stone at z {F(reachZ, "0.00")}; the register's top shows in its lower half, under the customers' faces (checked after).");
        }

        // ---- what goes off ----
        run.Line("What goes off (world boxes of their renderers):");
        Transform shell = Need(run, ShellPath);
        List<Transform> sections = shell.Cast<Transform>().Where(t => t.name == SectionName).OrderByDescending(t => t.position.x).ToList();
        run.Require(sections.Count == 3, $"Expected the three \"{SectionName}\" modules under {ShellPath}; found {sections.Count}.");
        foreach (Transform s in sections)
        {
            run.Require(s.gameObject.activeInHierarchy, $"{PathOf(s)} at x {F(s.position.x, "0.00")} is already off: this plan measured the old counter as it stood.");
            Bounds rb = RendererBounds(s);
            BoxCollider box = s.GetComponent<BoxCollider>();
            p.oldTops.Add(rb);
            p.off.Add((s.gameObject, "an old front-counter module (with its cubbies and accent strip): CK_FrontCounter replaces the four"));
            run.Info($"{PathOf(s)} at x {F(s.position.x, "0.00")}: {BoundsText(rb)}{(box != null ? "; its box " + BoundsText(box.bounds) : "")}");
        }
        Transform intakeMesh = Need(run, IntakeMeshPath);
        run.Require(intakeMesh.gameObject.activeInHierarchy, $"{IntakeMeshPath} is already off.");
        Bounds im = RendererBounds(intakeMesh);
        p.oldTops.Add(im);
        p.off.Add((intakeMesh.gameObject, "the intake's counter module (a visual only, under the station; the station and its colliders stay): CK_FrontCounter replaces it"));
        run.Info($"{IntakeMeshPath}: {BoundsText(im)} (no collider; the station's own boxes are the solid there)");
        AddIfOn(run, p.off, CubbiesPath, "the retail and the put-away junk in the old counter's cubbies (bean jars, mugs, plants): the new counter has no cubbies, they would stand inside it");
        AddIfOn(run, p.off, DeskSignPath, "the small \"ACE'S CAFE\" desk sign on the counter (the sign goes over the door, Street 1)");
        AddIfOn(run, p.off, CounterPlantPath, "the pot plant on the counter's end");
        foreach (Bounds top in p.oldTops) run.Info($"An old top, for what would float on it: {BoundsText(top)}");

        // ---- the pastry case: where it goes ----
        run.Line("The pastry case:");
        p.caseHolder = Need(run, CasePath);
        run.Require(p.caseHolder.gameObject.activeInHierarchy, $"{CasePath} is off.");
        p.caseWas = RendererBounds(p.caseHolder);
        run.Require(p.caseWas.size.x > .1f, $"{CasePath} has no renderers on.");
        run.Check(Mathf.Abs(p.caseWas.center.x - (-4.35f)) < .05f && Mathf.Abs(p.caseWas.min.y - 1.09f) < .02f,
                  $"the pastry case stands where the plan measured it (x -4.35, on 1.09): {BoundsText(p.caseWas)}");
        float width = p.caseWas.size.x;
        float centre = CaseCentreX;
        if (centre - width * .5f < CaseRunX0) centre = CaseRunX0 + width * .5f;
        if (centre + width * .5f > CaseRunX1) centre = CaseRunX1 - width * .5f;
        p.caseShift = new Vector3(centre - p.caseWas.center.x, Top - p.caseWas.min.y, 0f);
        p.caseTo = new Bounds(p.caseWas.center + p.caseShift, p.caseWas.size);
        run.Info($"It moves {V(p.caseShift)}: {BoundsText(p.caseTo)}, the same z and facing, its bottom on the stone: the counter's east run, " +
                 "between the lowered section and the half-round end (the intake's five slots, upgrades included, leave no metre by the till).");
        run.Check(width <= CaseRunX1 - CaseRunX0, $"the case ({F(width)} m) fits the east run (x {F(CaseRunX0, "0.00")}..{F(CaseRunX1, "0.00")}, {F(CaseRunX1 - CaseRunX0, "0.00")} m)");
        run.Check(p.caseTo.min.x >= CaseRunX0 - .001f && p.caseTo.max.x <= CaseRunX1 + .001f,
                  $"it stands clear of the lowered section's step (x {F(LowX1, "0.00")}) and of the half-round (from x {F(HalfRoundCentre.x, "0.00")}): x {F(p.caseTo.min.x)}..{F(p.caseTo.max.x)}");
        run.Check(p.caseTo.max.x <= WaterBox.min.x - .03f, $"…and 3 cm or more from the water station on the half-round end (from x {F(WaterBox.min.x)})");
        run.Check(p.caseTo.min.z >= TopZ0 + .005f && p.caseTo.max.z <= TopZ1 - .005f, $"it stands on the stone's depth (z {F(TopZ0, "0.00")}..{F(TopZ1, "0.00")}): z {F(p.caseTo.min.z)}..{F(p.caseTo.max.z)}");
        CheckSlotsClear(run, p, p.caseTo, "the case");
        run.Check(p.slots.All(s => !new Bounds(s.position + Vector3.up * DeviceRoom.y * .5f, DeviceRoom + new Vector3(.2f, 0f, 0f)).Intersects(p.caseTo)),
                  "the case is clear of every intake slot, the upgrade slots too, with 10 cm more room each side than the base check");

        // ---- the tent cards: on the case's top, read off its own mesh ----
        PlanCards(run, p);

        // ---- register v4: which way it faces, and where it stands ----
        PlanRegister(run, p);
        p.reachX0 = p.stationSolid.bounds.min.x;
        run.Info($"The station's reach over the stone: a trigger x {F(p.reachX0)}..{F(ReachX1)}, y {F(ReachY0)}..{F(ReachY1)}, z {F(TopZ0, "0.00")}..{F(TopZ1, "0.00")}, " +
                 "under the station (so the crosshair finds the station), from the station's own west end to the lowered section.");

        // ---- the intake shelf against the new top ----
        run.Line("The intake shelf against the new stone (its slots stay; the shelf has no visual of its own to move):");
        for (int i = 0; i < p.slots.Length; i++)
        {
            Transform s = p.slots[i];
            float under = TopAt(s.position.x, s.position.z);
            float gap = s.position.y - under;
            string use = i < p.baseSlots ? "a base slot (used from day 1)" : "an upgrade slot";
            if (gap < -.001f) run.Check(false, $"slot {i} ({s.name}) at {V(s.position)} sits {-gap * 100f:0.0} cm inside the stone (stop: report)");
            else if (gap > .01f) run.Info($"FLAG: slot {i} ({s.name}, {use}) at {V(s.position)} hangs {gap * 100f:0.0} cm over the stone under it ({F(under)}): " +
                                           "a device on it floats. The slots are gameplay: not moved (see the report's open questions).");
            else run.Info($"slot {i} ({s.name}, {use}) at {V(s.position)}: {gap * 1000f:0} mm over the stone ({F(under)})");
        }

        // ---- the new counter's place: free of everything but what goes off ----
        var goingOff = new HashSet<GameObject>(p.off.Select(o => o.go));
        var ignore = new List<Transform> { intakeGroup, work, p.caseHolder };
        Transform clamp = Find(run, ClampLampPath);
        if (clamp != null) ignore.Add(clamp);
        List<string> blockers = Blockers(run, new[]
        {
            Box(-7.39f, .03f, 13.31f, -5.81f, Top - .01f, 14.23f),       // west of the opening
            Box(-5.79f, .03f, 13.31f, -2.61f, Top - .01f, 13.64f),       // the ledge
            Box(-2.59f, .03f, 13.31f, 4.40f, Top - .01f, 14.23f),        // the rest, to the half-round end
        }, goingOff, ignore);
        run.Check(blockers.Count == 0, blockers.Count == 0 ? "nothing else stands where the new counter goes"
                                                           : "where the new counter goes something else stands: " + string.Join("; ", blockers));

        p.kept = Keep(run, GameplayRoots(run), p.off.Select(o => o.go));
        run.Info($"Kept for the check after: {p.kept.poses.Count} gameplay transforms and {p.kept.solids.Count} of their colliders.");
        return p;
    }

    // cafe_bar.json, read as the project has it (Kit 1 copies it next to the models): CK_BenchLamp's placement and light.
    static void ReadLampManifest(Run run, Bar1Plan p)
    {
        string path = KitModels + "/cafe_bar.json";
        TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        run.Require(text != null, $"{path} isn't in the project (Kit 1 copies the four manifests next to the models).");
        KitManifest manifest = JsonUtility.FromJson<KitManifest>(text.text);
        KitPiece lamp = manifest != null && manifest.pieces != null ? manifest.pieces.FirstOrDefault(x => x != null && x.name == "CK_BenchLamp") : null;
        run.Require(lamp != null && lamp.placement != null && Is3(lamp.placement.position) && Is3(lamp.placement.rotation),
                    $"{path} has no placement for CK_BenchLamp.");
        run.Require(lamp.light != null && Is3(lamp.light.local_position) && Is3(lamp.light.local_euler) && Is3(lamp.light.world_position) && Is3(lamp.light.head_centre_world),
                    $"{path} has no light for CK_BenchLamp.");
        p.lampAt = V3(lamp.placement.position);
        p.lampYaw = lamp.placement.rotation[1];
        p.lightLocal = V3(lamp.light.local_position);
        p.lightEuler = V3(lamp.light.local_euler);
        p.lightWorld = V3(lamp.light.world_position);
        p.headWorld = V3(lamp.light.head_centre_world);
        p.lightIntensity = lamp.light.intensity;
        p.lightRange = lamp.light.range;
        p.lightAngle = lamp.light.spot_angle;
        bool colour = ColorUtility.TryParseHtmlString(lamp.light.colour, out p.lightColour);
        Vector3 composed = p.lampAt + Quaternion.Euler(0f, p.lampYaw, 0f) * p.lightLocal;
        run.Line("The bench lamp, from cafe_bar.json (the brief's numbers are the check, 1 cm):");
        run.Check(Vector3.Distance(p.lampAt, LampAt) <= .01f && Mathf.Abs(Mathf.DeltaAngle(p.lampYaw, 180f)) < .5f && Mathf.Abs(lamp.placement.rotation[0]) < .5f && Mathf.Abs(lamp.placement.rotation[2]) < .5f,
                  $"its placement {V(p.lampAt)}, turned {F(p.lampYaw, "0")} (expected {V(LampAt)}, 180)");
        run.Check(Vector3.Distance(p.lightLocal, LampLightLocal) <= .01f && Vector3.Distance(p.lightEuler, new Vector3(90f, 0f, 0f)) < .5f,
                  $"its light at local {V(p.lightLocal)}, local Euler {V(p.lightEuler)} (expected {V(LampLightLocal)}, (90, 0, 0))");
        run.Check(Vector3.Distance(p.lightWorld, LampLightWorld) <= .01f && Vector3.Distance(composed, p.lightWorld) <= .01f,
                  $"that lands at {V(composed)} in the world; the manifest says {V(p.lightWorld)} (expected {V(LampLightWorld)})");
        run.Check(Vector3.Distance(p.headWorld, LampHeadWorld) <= .01f, $"the lamp's head centre {V(p.headWorld)} (expected {V(LampHeadWorld)})");
        run.Check(colour && string.Equals(lamp.light.colour, LampColour, StringComparison.OrdinalIgnoreCase) && Mathf.Abs(p.lightIntensity - LampIntensity) < .01f &&
                  Mathf.Abs(p.lightRange - LampRange) < .01f && Mathf.Abs(p.lightAngle - LampAngle) < .1f && lamp.light.type == "Spot" && lamp.light.shadows == "None",
                  $"a {lamp.light.type} {lamp.light.colour}, {F(p.lightIntensity, "0.0")}, range {F(p.lightRange, "0.0")}, {F(p.lightAngle, "0")}°, shadows {lamp.light.shadows} " +
                  $"(expected Spot {LampColour}, 1.6, 1.5, 50°, None)");
    }

    // The case's top, read off its own meshes where each card goes (a third of its width apart), at its middle front to
    // back or wherever the top is flat; the cards stand there after the move, facing the customers.
    static void PlanCards(Run run, Bar1Plan p)
    {
        run.Line("The tent cards:");
        Bounds c = p.caseWas;
        float[] dx = { -c.size.x * .3f, 0f, c.size.x * .3f };
        float[] dz = { 0f, -c.size.z * .2f, c.size.z * .2f };
        using (var probe = new Probe(p.caseHolder))
        {
            var how = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                float x = c.center.x + dx[i];
                RaycastHit? best = null;
                foreach (float z in dz)
                {
                    RaycastHit? h = probe.Cast(new Vector3(x, c.max.y + .5f, c.center.z + z), Vector3.down, c.size.y + 1f);
                    if (h == null) continue;
                    if (best == null || h.Value.normal.y > best.Value.normal.y + .01f) best = h;
                    if (h.Value.normal.y >= .98f) { best = h; break; }
                }
                Vector3 spot = best != null ? best.Value.point : new Vector3(x, c.max.y, c.center.z);
                if (best == null) how.Add($"card {i + 1}: no surface found on the case's mesh ({probe.Parts} part(s)), its bounds' top {F(c.max.y)} used");
                else if (best.Value.normal.y < .98f) how.Add($"card {i + 1}: the top slopes there (normal up {F(best.Value.normal.y, "0.00")})");
                p.cards.Add(spot + p.caseShift);
            }
            p.cardsHow = how.Count == 0 ? "each on a flat part of the case's top" : string.Join("; ", how);
        }
        for (int i = 0; i < 3; i++)
            run.Info($"{CardNames[i]} at {V(p.cards[i])}, turned 180 (its face to the queue){(i > 0 ? $", wearing CF_TentCard{i + 1}" : "")}");
        run.Info("How: " + p.cardsHow + ".");
        run.Check(p.cards.All(x => x.y >= Top + .02f && x.y <= p.caseTo.max.y + .001f), "each card stands on the case, above the stone and no higher than the case's top");
    }

    // Register v4's front, from its own mesh. The catalog's rule (CafeFurnishingCatalog.IsZUp / Upright): our Blender
    // exports may arrive standing on their backs; stood up, Blender (x, y, z) reads as Unity (-x, z, -y). The art guide's
    // rule 9 and the FBX itself (steps-bar/analysis/register_front.py): the customer side is Blender -Y (the tower, the
    // price window, the crown, 0.40-0.47 m high), the operator side +Y (the keys, the drawer's pull, 0.22-0.26 m high), so
    // the tower is the model's local +Z and a half turn puts it toward the queue. Measured here rather than trusted: the
    // heights over its footprint, read off its meshes, say where the tall half is, and the turn puts it to the south.
    static void PlanRegister(Run run, Bar1Plan p)
    {
        run.Line("Register v4:");
        GameObject temp = Object.Instantiate(p.registerAsset);
        try
        {
            foreach (Transform t in temp.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.SetPositionAndRotation(new Vector3(0f, -200f, 0f), Quaternion.identity);
            Bounds b = RendererBounds(temp.transform);
            Bounds local = new Bounds(b.center - temp.transform.position, b.size);
            bool zUp = CafeFurnishingCatalog.IsZUp(local);
            Quaternion upright = zUp ? CafeFurnishingCatalog.Upright : Quaternion.identity;
            temp.transform.rotation = upright;
            b = RendererBounds(temp.transform);
            run.Info($"{RegisterPath}: {(zUp ? "it arrives Z-up (lying on its back), stood up with CafeFurnishingCatalog.Upright" : "it arrives standing")}; " +
                     $"{F(b.size.x)} wide (x), {F(b.size.z)} deep (z), {F(b.size.y)} tall.");
            Vector2 tall = Vector2.zero;
            float weight = 0f;
            int hits = 0;
            using (var probe = new Probe(temp.transform))
            {
                for (int i = 0; i < 9; i++)
                for (int k = 0; k < 9; k++)
                {
                    float x = Mathf.Lerp(b.min.x + b.size.x * .08f, b.max.x - b.size.x * .08f, i / 8f);
                    float z = Mathf.Lerp(b.min.z + b.size.z * .08f, b.max.z - b.size.z * .08f, k / 8f);
                    RaycastHit? h = probe.Cast(new Vector3(x, b.max.y + .5f, z), Vector3.down, b.size.y + 1f);
                    if (h == null) continue;
                    float height = h.Value.point.y - b.min.y;
                    float w = height * height;                                // the tall parts count most
                    tall += new Vector2(x - b.center.x, z - b.center.z) * w;
                    weight += w;
                    hits++;
                }
            }
            float yaw = 180f;
            if (hits >= 20 && weight > 0f)
            {
                Vector2 d = tall / weight;
                float heading = Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);
                float turn = Mathf.Repeat(180f - heading, 360f);
                yaw = Mathf.Repeat(Mathf.Round(turn / 90f) * 90f, 360f);
                p.registerHow = $"its tall half lies toward heading {heading:0}° stood up unturned ({hits} heights read off its mesh), so it turns {yaw:0}° to face the queue " +
                                $"(the analysis said the tower is the model's +Z, a turn of 180°{(Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) < 1f ? ": agreed" : ": NOT agreed, look at the photos")})";
                if (Mathf.Abs(Mathf.DeltaAngle(turn, yaw)) > 10f) run.Info($"FLAG: the register's tall half isn't square to its axes ({F(turn, "0")}° wanted, {F(yaw, "0")}° used).");
            }
            else p.registerHow = $"its mesh couldn't be read for heights ({hits} hit(s)): turned 180° from the analysis (rule 9 and the FBX: the tower is the model's +Z)";
            p.registerTurn = Quaternion.Euler(0f, yaw, 0f) * upright;
            temp.transform.rotation = p.registerTurn;
            Bounds turned = RendererBounds(temp.transform);
            float bottom = turned.min.y - temp.transform.position.y;

            // Where on the till: the first x that keeps the conversation camera's view of every queue place's head clear and
            // the intake's base slots free; the till's staff half (z 13.95), its bottom on the stone.
            Vector3 cam = p.conversationCam.position;
            var report = new List<string>();
            bool placed = false;
            foreach (float x in RegisterXs)
            {
                var at = new Bounds(new Vector3(x, Top + (turned.center.y - temp.transform.position.y - bottom), RegisterZ), turned.size);
                var room = new Bounds(at.center, at.size + new Vector3(.04f, .04f, .04f));
                var blocked = new List<string>();
                for (int i = 0; i < p.queue.Length; i++)
                {
                    Vector3 head = p.queue[i].position + Vector3.up * 1.55f;
                    if (Crosses(cam, head, room)) blocked.Add($"the conversation camera's view of queue place {i}");
                }
                for (int i = 0; i < p.baseSlots && i < p.slots.Length; i++)
                    if (new Bounds(p.slots[i].position + Vector3.up * DeviceRoom.y * .5f, DeviceRoom).Intersects(at)) blocked.Add($"a device on base slot {i}");
                // The till's top: west of the lowered section's step and 5 cm or more from the pastry case (which stood west of
                // the till in the brief and stands on the east run since 10 Oct: the test is its box, wherever it is).
                if (at.max.x > LowX0 - .02f || new Bounds(p.caseTo.center, p.caseTo.size + new Vector3(.10f, 0f, .10f)).Intersects(at))
                    blocked.Add("the till's top (west of the lowered section, clear of the pastry case)");
                if (at.min.z < TopZ0 + .005f || at.max.z > TopZ1 - .005f) blocked.Add("the stone's depth");
                report.Add($"x {F(x, "0.00")}: {(blocked.Count == 0 ? "clear" : string.Join(", ", blocked))}");
                if (blocked.Count == 0 && !placed)
                {
                    placed = true;
                    p.registerX = x;
                    p.registerTo = at;
                }
            }
            run.Info("Its front: " + p.registerHow + ".");
            run.Info("Its place, tried in order: " + string.Join("; ", report) + ".");
            run.Check(placed, placed ? $"it stands at x {F(p.registerX, "0.00")}, z {F(RegisterZ, "0.00")} (the staff half), its bottom on {F(Top)}: {BoundsText(p.registerTo)}; no collider"
                                     : "no place on the till keeps the conversation camera's view clear and the intake's devices free");
            if (placed)
            {
                Vector3 eye = p.standPoint.position + Vector3.up * 1.65f;
                for (int i = 0; i < p.queue.Length; i++)
                {
                    Vector3 chest = p.queue[i].position + Vector3.up * 1.25f;
                    if (Crosses(eye, chest, p.registerTo)) run.Info($"From Ace's eye at the till the register stands between him and the chest of someone at queue place {i} " +
                                                                    "(their head stays in view; it has no collider, so neither the crosshair nor the aim help is stopped by it).");
                }
            }
        }
        finally { Object.DestroyImmediate(temp); }
    }

    // ================================================================== Bar 1: the change

    static void ChangeBar1(Run run, Bar1Plan p)
    {
        Transform group = StepGroup(run, Bar1Group);

        // 1. The old counter off (never deleted), and what stood on or in it.
        foreach (var (go, why) in p.off) SwitchOff(run, go, why);
        Physics.SyncTransforms();

        // 2. The one counter, solid by its own mesh (non-convex, static).
        GameObject counter = Place(run, "CK_FrontCounter", group, CounterAt, 180f, CounterName);
        var counterSolids = new List<MeshCollider>();
        foreach (MeshFilter f in counter.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            MeshCollider mc = CafeRedesignShared.AddComponent<MeshCollider>(run, f.gameObject);
            mc.sharedMesh = f.sharedMesh;
            mc.convex = false;
            counterSolids.Add(mc);
        }
        if (counterSolids.Count == 0) throw new InvalidOperationException("CK_FrontCounter has no mesh to be solid with.");
        Physics.SyncTransforms();

        // 3. The repair bench, empty (no collider: the Workbench's box becomes its solid), and its lamp with its light.
        GameObject bench = Place(run, "CK_RepairBench", group, BenchAt, 180f, BenchName);
        GameObject lamp = Place(run, "CK_BenchLamp", group, p.lampAt, p.lampYaw, LampName);
        Light light = MakeLight(run, lamp.transform, LampLightName, lamp.transform.TransformPoint(p.lightLocal), lamp.transform.rotation * Quaternion.Euler(p.lightEuler),
                                LightType.Spot, p.lightColour, p.lightIntensity, p.lightRange, p.lightAngle, LightShadows.None);
        // Not in CafeDaylight.warmLights (Claude, 10 Oct): those are evening lamps (0.4 of their strength by day); the bench lamp
        // is the repair's task light, at its full strength whenever Ace works, as the old clamp lamp was.

        // 4. The pastry case, on the counter's east run.
        Move(run, p.caseHolder, p.caseHolder.position + p.caseShift, p.caseHolder.rotation, "the pastry case on the counter's east run (x 3.29 to 4.34), between the lowered section and the half-round end, clear of the intake's upgrade slots");

        // 5. Register v4 on the till (not a kit piece: the project's model, placed as the catalog stands it up).
        var register = (GameObject)PrefabUtility.InstantiatePrefab(p.registerAsset, group);
        register.name = RegisterName;
        register.transform.SetPositionAndRotation(Vector3.zero, p.registerTurn);
        Bounds rb = RendererBounds(register.transform);
        register.transform.position = new Vector3(p.registerX - rb.center.x, Top - rb.min.y, RegisterZ - rb.center.z);
        int registerColliders = 0;
        foreach (Collider c in register.GetComponentsInChildren<Collider>(true)) { c.enabled = false; registerColliders++; }
        Made(run, register);
        if (registerColliders > 0) Note(run, $"The register's model brought {registerColliders} collider(s): switched off (the crosshair aims at the counter top there).");

        // 6. Three tent cards on the case, facing the queue; the 2nd and 3rd wear their own words.
        var cards = new List<GameObject>();
        for (int i = 0; i < 3; i++)
        {
            GameObject card = Place(run, "CK_TentCard", group, p.cards[i], 180f, CardNames[i]);
            cards.Add(card);
            if (i == 0) continue;
            Renderer r = card.GetComponentInChildren<Renderer>();
            if (r == null) throw new InvalidOperationException($"{card.name} has no renderer to wear CF_TentCard{i + 1}.");
            Repaint(run, r, Swap(r.sharedMaterials, "CF_TentCard1", i == 1 ? p.card2 : p.card3), $"tent card {i + 1}: {(i == 1 ? "Almond croissant" : "Banana bread")}");
        }

        // 7. The bell on the repair bar's ledge, the water station on the half-round end.
        GameObject bell = Place(run, "CK_ServiceBell", group, BellAt, 180f, BellName);
        GameObject water = Place(run, "CK_WaterStation", group, WaterAt, 180f, WaterName);

        // 8. The counter station's reach over the stone: a thin trigger under the station, on the top.
        var reach = new GameObject(ReachName);
        reach.transform.SetParent(p.station, false);
        reach.transform.localRotation = Quaternion.identity;
        reach.transform.localScale = Vector3.one;
        var reachBox = new Bounds();
        reachBox.SetMinMax(new Vector3(p.reachX0, ReachY0, TopZ0), new Vector3(ReachX1, ReachY1, TopZ1));
        reach.transform.position = reachBox.center;
        BoxCollider trigger = reach.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = LocalSize(reach.transform, reachBox.size);
        Made(run, reach);
        Physics.SyncTransforms();

        // 9. Anything else that stood on the old tops and would float now (the new stone holds up what stands at 1.095).
        var gameplay = new List<GameObject>();
        foreach (GameObject go in Floating(run, p.oldTops, new HashSet<GameObject>(p.off.Select(o => o.go)), gameplay))
            SwitchOff(run, go, "stood on the old counter's top: with the old modules off it would float");
        Physics.SyncTransforms();

        CheckBar1(run, p, counter, counterSolids, bench, lamp, light, register, cards, bell, water, trigger, gameplay);
    }

    // ================================================================== Bar 1: the checks after

    static void CheckBar1(Run run, Bar1Plan p, GameObject counter, List<MeshCollider> solids, GameObject bench, GameObject lamp, Light light, GameObject register,
                          List<GameObject> cards, GameObject bell, GameObject water, BoxCollider trigger, List<GameObject> gameplayFloating)
    {
        run.Line("The pieces where they belong (their renderers' boxes, 1.5 cm):");
        Expect(run, counter, CounterBox);
        Expect(run, bench, BenchBox);
        Expect(run, lamp, LampBox);
        Expect(run, bell, BellBox);
        Expect(run, water, WaterBox);

        run.Line("The top's height (a ray down onto the counter's own collider):");
        foreach (float x in new[] { -7.0f, -6.0f, -2.4f, -1.5f, 0f, 1.3f, 3.5f, 4.4f })
        {
            float? y = RayOnto(solids, new Vector3(x, 3f, 13.95f));
            run.Check(y.HasValue && Mathf.Abs(y.Value - Top) <= .002f, $"at x {F(x, "0.0")}, z 13.95: {(y.HasValue ? F(y.Value) : "no top")} (expected {F(Top)})");
        }
        float? low = RayOnto(solids, new Vector3((LowX0 + LowX1) * .5f, 3f, 13.95f));
        run.Check(low.HasValue && Mathf.Abs(low.Value - LowTop) <= .002f, $"at x {F((LowX0 + LowX1) * .5f, "0.00")} (the lowered section): {(low.HasValue ? F(low.Value) : "no top")} (expected {F(LowTop)})");
        float? ledge = RayOnto(solids, new Vector3(-4.2f, 3f, 13.47f));
        run.Check(ledge.HasValue && Mathf.Abs(ledge.Value - Top) <= .002f, $"on the repair bar's ledge at x -4.2, z 13.47: {(ledge.HasValue ? F(ledge.Value) : "no top")}");

        run.Line("The repair bar's opening (Repair bar 1's own tests, run now):");
        List<string> blockers = OpeningBlockers(run);
        run.Check(blockers.Count == 0, blockers.Count == 0 ? "nothing solid above 1.010 and nothing small in x -5.75..-2.65, z 13.72..14.40: the bench's place is free"
                                                           : "in the bench's place: " + string.Join("; ", blockers));
        List<float> gaps = CounterGaps(run);
        run.Check(gaps.Count == 0, gaps.Count == 0 ? "the counter's front is solid at x -7.30 to +4.70 every 0.25 m (z 13.30..13.65, y 0.55): nobody walks behind it"
                                                   : "the counter's front is OPEN at x " + string.Join(", ", gaps.Select(x => F(x, "0.00"))));

        run.Line("The bench and its lamp:");
        using (var probe = new Probe(bench.transform))
        {
            var tops = new List<string>();
            bool ok = probe.Parts > 0;
            foreach (Vector2 at in new[] { new Vector2(-4.2f, 14.06f), new Vector2(-5.5f, 14.3f), new Vector2(-2.9f, 14.3f) })
            {
                float? y = probe.Down(at.x, at.y);
                tops.Add($"({F(at.x, "0.0")}, {F(at.y, "0.00")}) {(y.HasValue ? F(y.Value) : "none")}");
                ok &= y.HasValue && Mathf.Abs(y.Value - BenchTop) <= .002f;
            }
            run.Check(ok, $"the bench's chestnut top at {F(BenchTop)} (read off its mesh: {string.Join(", ", tops)})");
        }
        using (var probe = new Probe(lamp.transform))
        {
            var reads = new List<string>();
            bool ok = probe.Parts > 0;
            foreach (Vector2 d in new[] { Vector2.zero, new Vector2(.055f, 0f), new Vector2(-.055f, 0f), new Vector2(0f, .055f), new Vector2(0f, -.055f) })
            {
                float? y = probe.Down(p.headWorld.x + d.x, p.headWorld.z + d.y);
                reads.Add(y.HasValue ? F(y.Value) : "none");
                ok &= y.HasValue && Mathf.Abs(y.Value - p.headWorld.y) <= .004f;
            }
            run.Check(ok, $"the lamp's lens lies level round the manifest's head centre {V(p.headWorld)}, so the head is within about 1 cm of it " +
                          $"(the lens read at the centre and 5.5 cm each way: {string.Join(", ", reads)})");
        }
        run.Check(Vector3.Distance(light.transform.position, p.lightWorld) <= .01f && Vector3.Dot(light.transform.forward, Vector3.down) > .999f,
                  $"its spot at {V(light.transform.position)} (the manifest: {V(p.lightWorld)}), pointing straight down");
        run.Check(light.type == LightType.Spot && Mathf.Abs(light.intensity - p.lightIntensity) < .001f && Mathf.Abs(light.range - p.lightRange) < .001f &&
                  Mathf.Abs(light.spotAngle - p.lightAngle) < .01f && light.shadows == LightShadows.None,
                  $"a spot #{ColorUtility.ToHtmlStringRGB(light.color)}, {F(light.intensity, "0.0")}, range {F(light.range, "0.0")}, {F(light.spotAngle, "0")}°, no shadows");
        run.Check(!p.daylight.warmLights.Contains(light), "it is a task light, not one of CafeDaylight's evening lamps: full strength by day");

        run.Line("What was moved and placed on the stone (a ray down from each: standing, not floating, not sunk):");
        Bounds caseNow = RendererBounds(p.caseHolder);
        run.Check(MaxError(caseNow, p.caseTo) <= .003f, $"the pastry case: {BoundsText(caseNow)} (planned {BoundsText(p.caseTo)})");
        Standing(run, "the pastry case", caseNow, solids);
        run.Check(caseNow.min.x >= CaseRunX0 - .002f && caseNow.max.x <= CaseRunX1 + .002f, $"it stands on the east run, clear of the lowered section's step and the half-round: x {F(caseNow.min.x)}..{F(caseNow.max.x)}");
        Bounds reg = RendererBounds(register.transform);
        run.Check(Mathf.Abs(reg.center.x - p.registerX) <= .003f && Mathf.Abs(reg.center.z - RegisterZ) <= .003f && Mathf.Abs(reg.min.y - Top) <= .003f,
                  $"register v4: {BoundsText(reg)} (centred at x {F(p.registerX, "0.00")}, z {F(RegisterZ, "0.00")}, its bottom on {F(Top)})");
        Standing(run, "register v4", reg, solids);
        run.Check(register.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "the register has no collider on");
        run.Check(reg.center.z > (TopZ0 + TopZ1) * .5f, $"it stands on the stone's staff half (its middle at z {F(reg.center.z)}, the top's middle {F((TopZ0 + TopZ1) * .5f)})");
        using (var probe = new Probe(register.transform))
        {
            float south = 0f, north = 0f;
            for (int i = 0; i < 5; i++)
            {
                float x = Mathf.Lerp(reg.min.x + reg.size.x * .15f, reg.max.x - reg.size.x * .15f, i / 4f);
                south = Mathf.Max(south, probe.Down(x, reg.min.z + reg.size.z * .2f) ?? 0f);
                north = Mathf.Max(north, probe.Down(x, reg.max.z - reg.size.z * .2f) ?? 0f);
            }
            run.Check(south > north + .05f, $"its tall side (the display tower) faces the queue: {F(south - Top)} m high on the customers' side, {F(north - Top)} m on Ace's");
        }
        Vector3 cam = p.conversationCam.position;
        for (int i = 0; i < p.queue.Length; i++)
        {
            Vector3 head = p.queue[i].position + Vector3.up * 1.55f;
            run.Check(!Crosses(cam, head, reg), $"the conversation camera sees the head of whoever stands at queue place {i} past the register");
        }
        CheckSlotsClear(run, p, caseNow, "the case");
        CheckSlotsClear(run, p, reg, "the register");
        for (int i = 0; i < cards.Count; i++)
        {
            Bounds cb = RendererBounds(cards[i].transform);
            run.Check(Mathf.Abs(cb.min.y - p.cards[i].y) <= .003f && cb.min.x >= caseNow.min.x && cb.max.x <= caseNow.max.x && cb.min.z >= caseNow.min.z && cb.max.z <= caseNow.max.z,
                      $"{cards[i].name}: {BoundsText(cb)}, on the case's top ({F(p.cards[i].y)}), within its footprint");
            run.Check(Mathf.Abs(Mathf.DeltaAngle(Facing(cards[i].transform.rotation), 180f)) < .5f, $"…facing the queue (its front heading {Facing(cards[i].transform.rotation):0})");
        }
        run.Check(cards[1].GetComponentInChildren<Renderer>().sharedMaterials.Contains(p.card2) && cards[2].GetComponentInChildren<Renderer>().sharedMaterials.Contains(p.card3),
                  "the 2nd card wears CF_TentCard2 and the 3rd CF_TentCard3");
        Bounds bellNow = RendererBounds(bell.transform);
        Standing(run, "the service bell", bellNow, solids);
        run.Check(bellNow.min.z >= TopZ0 && bellNow.max.z <= 13.662f - .02f && bellNow.max.x <= BenchEastPost - .02f,
                  $"the bell is on the ledge, clear of the glass's shoe (z 13.662) and the east post: {BoundsText(bellNow)}");
        Bounds waterNow = RendererBounds(water.transform);
        Standing(run, "the water station", waterNow, solids);
        run.Check(waterNow.min.x >= LowX1 + .01f && waterNow.min.z >= TopZ0 && waterNow.max.z <= TopZ1, $"the water station is on the main top, clear of the lowered section: {BoundsText(waterNow)}");
        float waterReach = 0f;
        foreach (float wx in new[] { waterNow.min.x, waterNow.max.x })
            foreach (float wz in new[] { waterNow.min.z, waterNow.max.z })
                if (wx > HalfRoundCentre.x) waterReach = Mathf.Max(waterReach, new Vector2(wx - HalfRoundCentre.x, wz - HalfRoundCentre.y).magnitude);
        run.Check(waterReach <= HalfRoundRadius - .02f, $"…on the half-round end: its farthest corner {F(waterReach)} m from the curve's centre (the stone's edge at {F(HalfRoundRadius, "0.00")})");
        run.Check(waterNow.min.x >= caseNow.max.x + .03f, $"…and clear of the pastry case: {F(waterNow.min.x - caseNow.max.x, "0.00")} m between them");
        float queueRoom = p.queue.Min(q => Flat(waterNow.ClosestPoint(q.position) - q.position).magnitude);
        run.Check(queueRoom >= .5f, $"…and {F(queueRoom, "0.00")} m from the nearest queue place");

        run.Line("The counter station's reach over the stone:");
        run.Check(trigger.isTrigger && trigger.GetComponentInParent<StationInteractable>() == p.station.GetComponent<StationInteractable>() &&
                  MaxError(trigger.bounds, Box(p.reachX0, ReachY0, TopZ0, ReachX1, ReachY1, TopZ1)) <= .001f,
                  $"a trigger under the station, {BoundsText(trigger.bounds)}, 1 mm over the stone");
        Vector3 eye = p.standPoint.position + Vector3.up * 1.65f;
        Vector3 aim = new Vector3(0f, Top, 13.9f);
        string first = FirstUse(run, eye, aim);
        run.Check(first == p.station.name, $"in first person from the stand point, aiming at the stone in front of the till, the crosshair finds {first} (the counter station)");

        run.Line("The intake shelf on the new stone (its slots never move):");
        for (int i = 0; i < p.slots.Length; i++)
        {
            Transform s = p.slots[i];
            float? under = RayOnto(solids, new Vector3(s.position.x, 3f, s.position.z));
            float gap = under.HasValue ? s.position.y - under.Value : float.NaN;
            run.Check(under.HasValue && gap >= -.001f, $"slot {i} ({s.name}) at {V(s.position)}: the stone under it at {(under.HasValue ? F(under.Value) : "none")} (not inside it)");
            if (under.HasValue && gap > .01f) run.Info($"FLAG: slot {i} hangs {gap * 100f:0.0} cm over the stone: {(i < p.baseSlots ? "a base slot: the first device of the day floats there" : "an upgrade slot")}.");
        }

        run.Line("What stays where it was, what went, what's clear:");
        Unchanged(run, p.kept);
        List<GameObject> floating = Floating(run, p.oldTops, new HashSet<GameObject>(p.off.Select(o => o.go)), null);
        run.Check(floating.Count == 0, floating.Count == 0 ? "nothing floats over the old counter's tops" : "still floating over the old tops: " + string.Join(", ", floating.Select(g => PathOf(g.transform))));
        run.Check(gameplayFloating.Count == 0, gameplayFloating.Count == 0 ? "no gameplay object stood on the old tops without a new top under it"
                                                                           : "gameplay objects left without support (not switched off): " + string.Join(", ", gameplayFloating.Select(g => PathOf(g.transform))));
        run.Check(p.off.All(o => !o.go.activeInHierarchy), "the old modules, the intake's module, the cubbies, the desk sign and the pot plant are off");
        var mine = new List<GameObject> { counter, bench, lamp, register, bell, water };
        mine.AddRange(cards);
        Clear(run, mine);
        float standRoom = Flat(CounterBox.ClosestPoint(p.standPoint.position) - p.standPoint.position).magnitude;
        run.Check(standRoom >= .25f && !mine.Any(m => RendererBounds(m.transform).Contains(p.standPoint.position + Vector3.up * .5f)),
                  $"the counter's stand point is clear: {F(standRoom, "0.00")} m behind the counter's back");
        if (standRoom < AceRadius)
            run.Info($"FLAG: Ace's body ({F(AceRadius, "0.00")} m) put exactly on the stand point reaches {(AceRadius - standRoom) * 100f:0} cm into the stone's back edge " +
                     "(the old counter's solid there stopped at the station's box, z 14.10): a lab that puts him there sees him nudged back that much. The stand point is gameplay: not moved.");
    }

    // Ace's capsule as the game runs it (PlayerMovement.RadiusFor: 0.35 since playtest 3).
    const float AceRadius = .35f;

    static void PhotosBar1(Run run)
    {
        Photo(run, "1-the-counter-from-the-door", new Vector3(0f, 1.65f, .6f), new Vector3(0f, 1.1f, 13.8f), 62f);
        Photo(run, "2-from-the-queue", new Vector3(0f, 1.6f, 11.6f), new Vector3(-1.5f, 1.1f, 14.0f), 62f);
        Photo(run, "3-the-repair-bar-from-a-stool", new Vector3(-4.2f, 1.45f, 12.85f), new Vector3(-4.6f, 1.0f, 14.0f), 62f);
        Photo(run, "4-the-till-from-aces-place", new Vector3(0f, 1.65f, 15.15f), new Vector3(0f, 1.0f, 13.5f), 62f);
        Photo(run, "5-the-pastry-case-and-the-half-round-end", new Vector3(3.0f, 1.55f, 11.4f), new Vector3(4.0f, 1.15f, 13.75f), 58f);
        GameCameraPhoto(run, "6-the-game-camera");
    }

    // ================================================================== Bar 2: the survey

    internal sealed class Bar2Plan
    {
        public readonly List<(GameObject go, string why)> off = new List<(GameObject, string)>();
        public readonly List<(Component c, string why)> hide = new List<(Component, string)>();
        public readonly List<Bounds> oldTops = new List<Bounds>();
        public readonly List<Renderer> linings = new List<Renderer>();
        public readonly List<GameObject> trophies = new List<GameObject>();
        public Material[] chalk;
        public CafeDaylight daylight;
        public Transform dispenser, cupStack, basin, drinkStand, trophyShelf, door, aceAtTheDoor, ceiling;
        public Bounds doorBox;
        public Vector3 plateAt;
        public string plateHow = "";
        public Transform potPlant;
        public Vector3 potPlantTo;
        public bool potPlantMoves, potPlantOff;
        public float grinderShift;                                   // along x from the brief's 4.25 (0 when it fits there)
        public Snapshot kept;
    }

    static Bar2Plan SurveyBar2(Run run)
    {
        var p = new Bar2Plan();
        run.Require(!Done(run, Bar2Title), $"{Bar2Title} has already run in this scene (Bar 2 1 - Undo puts it back).");
        run.Require(Done(run, Bar1Title), "Bar 1 - The front counter hasn't run in this scene: the back bar comes after the front counter and the repair bar.");
        run.Require(RepairBarDone(run), "The repair bench is still at the back wall (no CafeRepairBar on the café's root): run Repair bar 1 first; the back bar goes where the bench stood.");

        // ---- the kit ----
        run.Line("The kit:");
        foreach (string piece in new[] { "CK_BackBar", "CK_BackBarShelf", "CK_Backsplash", "CK_PartsWall", "CK_MenuBoard", "CK_Grinder", "CK_BeanBag", "CK_Pothos", "CK_StaffPlate" })
            run.Require(AssetDatabase.LoadAssetAtPath<GameObject>($"{KitModels}/{piece}.fbx") != null, $"{KitModels}/{piece}.fbx isn't imported: run Kit 1 - Import the café kit first.");
        run.Info("CK_BackBar, CK_BackBarShelf, CK_Backsplash, CK_PartsWall, CK_MenuBoard, CK_Grinder, CK_BeanBag, CK_Pothos and CK_StaffPlate are in.");

        // ---- the gameplay objects ----
        run.Line("The gameplay objects (they never move; 2 cm allowed here, 1 mm after):");
        Transform work = Need(run, WorkAreaPath);
        At(run, work, new Vector3(-4.20f, .5f, 14.10f), "the repair work area, at the repair bar (Repair bar 1 moved it there)");
        run.Check(Mathf.Abs(Mathf.DeltaAngle(Facing(work.rotation), 180f)) < .5f, $"…facing the room ({Facing(work.rotation):0})");
        p.dispenser = Need(run, DispenserPath);
        p.cupStack = Need(run, CupStackPath);
        p.basin = Need(run, BasinPath);
        p.drinkStand = Need(run, DrinkStandPath);
        run.Require(p.dispenser.GetComponent<BeverageStation>() != null, $"{DispenserPath} has no BeverageStation.");
        At(run, p.dispenser, new Vector3(3.065f, .993f, 17.430f), "the beverage dispenser");
        At(run, p.cupStack, new Vector3(2.431f, 1.163f, 17.323f), "the cup stack");
        At(run, p.basin, new Vector3(3.701f, 1.098f, 17.324f), "the discard basin");
        At(run, p.drinkStand, new Vector3(3.071f, 0f, 16.480f), "the drinks station's stand point (scene-facts' \"about (3.1, 16.3)\" is 18 cm off: the YAML's value is used)");
        p.trophyShelf = Need(run, TrophyShelfPath);
        TrophyShelf shelf = p.trophyShelf.GetComponent<TrophyShelf>();
        run.Require(shelf != null, $"{TrophyShelfPath} has no TrophyShelf.");
        At(run, p.trophyShelf, new Vector3(.35f, 1.85f, 17.99f), "Night 1's trophy shelf (TrophyShelf)");
        p.door = Need(run, BackDoorPath);
        p.aceAtTheDoor = Need(run, AceAtTheDoorPath);
        At(run, p.door, new Vector3(6.629f, 0f, 17.964f), "the back door's leaf (Back door (inside), Bins 2 put it there)");
        At(run, p.aceAtTheDoor, new Vector3(6.629f, 1f, 17.35f), "where Ace stands inside the back door at night");
        p.daylight = TheDaylight(run);
        p.ceiling = Find(run, CeilingPath);
        Bounds dispenserWas = Footing(p.dispenser);
        run.Info($"The dispenser's renderers: {BoundsText(dispenserWas)}; its bottom {(dispenserWas.min.y - BackTop) * 1000f:0} mm off the drinks top ({F(BackTop)}), which the back bar's top keeps.");

        // ---- what goes off ----
        run.Line("What goes off (world boxes of their renderers):");
        var stays = new[] { p.dispenser, p.cupStack, p.basin, p.drinkStand, p.trophyShelf, p.door };
        foreach (var (path, count, why) in BackOff)
        {
            int slash = path.LastIndexOf('/');
            Transform parent = slash < 0 ? run.Cafe : Find(run, path.Substring(0, slash));
            string name = slash < 0 ? path : path.Substring(slash + 1);
            List<Transform> found = parent == null ? new List<Transform>() : parent.Cast<Transform>().Where(t => t.name == name).ToList();
            List<Transform> on = found.Where(t => t.gameObject.activeSelf).ToList();
            if (on.Count == 0)
            {
                run.Info(found.Count == 0 ? $"{path}: not in the scene (nothing to switch off)" : $"{path}: already off");
                continue;
            }
            if (on.Count != count) run.Info($"{path}: {on.Count} on (the plan counted {count})");
            foreach (Transform t in on)
            {
                Bounds b = RendererBounds(t);
                bool gameplay = HasGameplay(t) || stays.Any(s => s != null && (s.IsChildOf(t)));
                if (gameplay)
                {
                    bool Theirs(Component c) => HasGameplay(c.transform) || stays.Any(s => s != null && c.transform.IsChildOf(s));
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && !Theirs(x))) p.hide.Add((r, why + " (only its renderers: something the game uses is inside it)"));
                    foreach (Collider c in t.GetComponentsInChildren<Collider>(true).Where(x => x.enabled && !x.isTrigger && !Theirs(x))) p.hide.Add((c, why + " (only its colliders: something the game uses is inside it)"));
                    run.Info($"{PathOf(t)} holds something the game uses: its own renderers and colliders go, not the object. {BoundsText(b)}");
                }
                else
                {
                    p.off.Add((t.gameObject, why));
                    run.Info($"{PathOf(t)} at {V(t.position)}: {BoundsText(b)}");
                }
                if (b.size.y > 0f && b.max.y < 1.2f) p.oldTops.Add(b);           // a counter or cabinet top: what stood on it must not float
                foreach (Transform part in t.GetComponentsInChildren<Transform>(false))
                {
                    Renderer pr = part.GetComponent<Renderer>();
                    if (pr != null && pr.enabled && pr.bounds.size.y < .1f && pr.bounds.max.y < 1.2f && pr.bounds.size.x > .3f) p.oldTops.Add(pr.bounds);   // a top inside a group
                }
            }
        }
        run.Require(p.off.Any(o => o.go.name == "Rear storage") || p.hide.Any(h => h.c.transform.name == "Rear storage"),
                    "The storage cabinet (Counter extensions and cabinetry/Rear storage) is the back bar's place: it must be on to go off. Look at the scene.");

        // ---- the trophy shelf: its board and brackets (renderers only), its trophies onto the new shelf ----
        foreach (Transform part in p.trophyShelf.Cast<Transform>().Where(t => TrophyShelfParts.Contains(t.name)))
            foreach (Renderer r in part.GetComponents<Renderer>().Where(x => x.enabled))
                p.hide.Add((r, "Night 1's trophy shelf's own board and brackets (the \"floating shelf\"): only its renderers go; the shelf is a gameplay object " +
                               "(TrophyShelf, found by type at night), so it stays on, and its trophies stand on the long shelf"));
        run.Check(p.hide.Count(h => h.c is Renderer r && r.transform.parent == p.trophyShelf) == 5, "the trophy shelf's board, two brackets and two bracket arms are found (5 renderers)");
        foreach (TrophyShelf.Slot slot in shelf.slots ?? Array.Empty<TrophyShelf.Slot>())
            if (slot != null && slot.shown != null) p.trophies.Add(slot.shown);
        run.Info($"Its trophies ({p.trophies.Count}): " + string.Join("; ", p.trophies.Select(t => $"{t.name} at {V(t.transform.position)}, {BoundsText(MeshBox(t))}{(t.activeSelf ? "" : " (hidden until earned)")}")) +
                 $". The shelf moves as one to x {F(TrophyShelfX, "0.00")} (from {F(p.trophyShelf.position.x, "0.00")}), clear of the menu board, and down onto the long shelf's top (1.81).");
        foreach (GameObject t in p.trophies)
        {
            Bounds tb = MeshBox(t);
            tb.center += new Vector3(TrophyShelfX - p.trophyShelf.position.x, 1.81f - p.trophyShelf.position.y, 0f);
            run.Check(!(tb.max.x > MenuFrame.min.x && tb.min.x < MenuFrame.max.x && tb.max.y > MenuFrame.min.y),
                      $"{t.name} will stand clear of the menu board: x {F(tb.min.x, "0.00")}..{F(tb.max.x, "0.00")} (the board's frame x {F(MenuFrame.min.x, "0.00")}..{F(MenuFrame.max.x, "0.00")})");
        }

        // ---- the green lining takes the service wall's chalk white ----
        Renderer chalk = Need(run, ChalkWallPath).GetComponent<Renderer>();
        run.Require(chalk != null && chalk.sharedMaterials.Length > 0 && chalk.sharedMaterials.All(m => m != null), $"{ChalkWallPath} has no material to copy.");
        p.chalk = chalk.sharedMaterials;
        foreach (string path in GreenLinings)
        {
            Renderer r = Need(run, path).GetComponent<Renderer>();
            run.Require(r != null, $"{path} has no renderer.");
            p.linings.Add(r);
            run.Info($"{path}: {BoundsText(r.bounds)}, wears {string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name))} -> {string.Join(", ", p.chalk.Select(m => m.name))}");
        }

        // ---- the staff plate: on the back door's room face ----
        p.doorBox = RendererBounds(p.door);
        run.Require(p.doorBox.size.x > .3f, "The back door's leaf has no renderers on.");
        float doorX = p.doorBox.center.x;
        using (var probe = new Probe(p.door))
        {
            RaycastHit? face = probe.Cast(new Vector3(doorX, PlateBottom + .04f, p.doorBox.min.z - .5f), Vector3.forward, 1.5f);
            if (face != null && face.Value.normal.z < -.9f)
            {
                p.plateAt = new Vector3(doorX, PlateBottom, face.Value.point.z);
                p.plateHow = $"the door's room face read off its mesh at z {F(face.Value.point.z)}";
            }
            else
            {
                p.plateAt = new Vector3(doorX, PlateBottom, p.doorBox.min.z);
                p.plateHow = $"the door's mesh couldn't be read there: its box's room side (z {F(p.doorBox.min.z)})";
            }
        }
        run.Info($"The back door: {BoundsText(p.doorBox)} (Bins 2 moved it to x {F(p.doorBox.min.x, "0.00")}..{F(p.doorBox.max.x, "0.00")}; the brief's 5.6..6.6 was before). " +
                 $"No hinge or animator: it doesn't swing, so the plate goes in this step's group. {PlateName} at {V(p.plateAt)}, turned 180 ({p.plateHow}).");
        run.Check(p.door.GetComponentInChildren<Animator>(true) == null && p.door.GetComponentInChildren<HingeJoint>(true) == null, "the back door's leaf has no animator or hinge");

        // ---- the pot plant at the back bar's east end ----
        Transform plants = Find(run, BackPotPlantGroup);
        p.potPlant = plants == null ? null : plants.Cast<Transform>().FirstOrDefault(t => t.name == BackPotPlantName && t.gameObject.activeInHierarchy && Flat(t.position - BackPotPlantAt).magnitude < .1f);
        if (p.potPlant == null) run.Info($"No {BackPotPlantName} at the back counter's east end ({V(BackPotPlantAt)}): nothing to clear there.");
        if (p.potPlant != null)
        {
            Bounds pb = RendererBounds(p.potPlant);
            if (pb.Intersects(Shrink(BackBarBox, .005f)))
            {
                float shift = BackBarBox.max.x + .03f - pb.min.x;
                p.potPlantTo = p.potPlant.position + new Vector3(shift, 0f, 0f);
                if (pb.max.x + shift <= p.doorBox.min.x - .10f) { p.potPlantMoves = true; run.Info($"{PathOf(p.potPlant)} ({BoundsText(pb)}) stands in the back bar's end: it moves {F(shift)} m east, clear of the back door."); }
                else { p.potPlantOff = true; p.off.Add((p.potPlant.gameObject, "the pot plant at the back bar's east end: no room between the bar and the back door")); run.Info($"{PathOf(p.potPlant)} has no room between the back bar and the door: it goes off."); }
            }
            else run.Info($"{PathOf(p.potPlant)} ({BoundsText(pb)}) clears the back bar's end: it stays.");
        }

        // ---- the grinder: at x 4.25 if it clears the basin (5 cm), the dispenser (2 cm), the drinks stand point (0.6 m) and
        // the east pothos's hanging vines (from x 4.408); else along the bar, west, to the first place that does ----
        Bounds basinWas = Footing(p.basin);
        var tried = new List<string>();
        p.grinderShift = float.NaN;
        foreach (float x in GrinderXs)
        {
            float dx = x - GrinderAt.x;
            Bounds g = new Bounds(GrinderBox.center + new Vector3(dx, 0f, 0f), GrinderBox.size);
            var why = new List<string>();
            if (g.min.x - basinWas.max.x < .05f) why.Add($"{(g.min.x - basinWas.max.x) * 100f:0} cm from the basin");
            if (g.min.x - dispenserWas.max.x < .02f || g.Intersects(dispenserWas)) why.Add("against the dispenser");
            if (Flat(g.ClosestPoint(p.drinkStand.position) - p.drinkStand.position).magnitude < .6f) why.Add("too near the drinks stand point");
            if (g.max.x > PothosEastBox.min.x - .015f) why.Add("in the east pothos's vines");
            tried.Add($"x {F(x, "0.00")}: {(why.Count == 0 ? "clear" : string.Join(", ", why))}");
            if (why.Count == 0) { p.grinderShift = dx; break; }
        }
        run.Check(!float.IsNaN(p.grinderShift), $"the grinder finds a place beside the dispenser ({string.Join("; ", tried)})");
        if (float.IsNaN(p.grinderShift)) p.grinderShift = 0f;
        else if (Mathf.Abs(p.grinderShift) > .001f) run.Info($"FLAG: the grinder goes at x {F(GrinderAt.x + p.grinderShift, "0.00")}, not 4.25 (the brief's): {tried[0]}.");

        // ---- the new pieces' places: free of everything but what goes ----
        var goingOff = new HashSet<GameObject>(p.off.Select(o => o.go));
        var ignore = new List<Transform> { Need(run, DrinksGroupPath), Need(run, IntakeGroupPath), work, p.trophyShelf, p.door };
        ignore.AddRange(p.hide.Select(h => h.c.transform));
        if (p.potPlant != null) ignore.Add(p.potPlant);
        Transform clamp = Find(run, ClampLampPath);
        if (clamp != null) ignore.Add(clamp);
        List<string> blockers = Blockers(run, new[]
        {
            Shrink(BackBarBox, .01f), Shrink(PartsBox, .01f), Shrink(ShelfBox, .01f), Shrink(MenuBox, .01f),
            Shrink(new Bounds(GrinderBox.center + new Vector3(p.grinderShift, 0f, 0f), GrinderBox.size), .01f),
        }, goingOff, ignore);
        run.Check(blockers.Count == 0, blockers.Count == 0 ? "nothing else stands where the back bar, the parts wall, the shelf, the menu and the grinder go"
                                                           : "something else stands where the new pieces go: " + string.Join("; ", blockers));
        foreach (var (what, box) in ShelfParts().Concat(new[] { ("the backsplash", SplashBox), ("the menu board", MenuBox) }))
            run.Check(!dispenserWas.Intersects(box), $"the dispenser ({BoundsText(dispenserWas)}) clears {what} ({BoundsText(box)})");

        p.kept = Keep(run, GameplayRoots(run), p.off.Select(o => o.go));
        run.Info($"Kept for the check after: {p.kept.poses.Count} gameplay transforms and {p.kept.solids.Count} of their colliders.");
        return p;
    }

    // ================================================================== Bar 2: the change

    static void ChangeBar2(Run run, Bar2Plan p)
    {
        Transform group = StepGroup(run, Bar2Group);

        // 1. Off, never deleted; renderers only where something the game uses is inside.
        foreach (var (go, why) in p.off) SwitchOff(run, go, why);
        foreach (var (c, why) in p.hide) Toggle(run, c, false, why);
        foreach (Renderer r in p.linings) Repaint(run, r, p.chalk, "the green workshop lining: the back wall is one white wall now (the service wall's chalk white)");
        Physics.SyncTransforms();

        // 2. The back bar's run, solid as one box (its top at 1.00 where the dispenser stands); the tile; the shelf, solid on
        //    its board, with its seven lights; the parts wall on the stone, solid as a box.
        GameObject backBar = Place(run, "CK_BackBar", group, BackBarAt, 180f, BackBarName);
        BoxCollider backBarSolid = SolidBox(run, backBar, BackBarSolid);
        GameObject splash = Place(run, "CK_Backsplash", group, SplashAt, 180f, SplashName);
        GameObject shelf = Place(run, "CK_BackBarShelf", group, ShelfAt, 180f, ShelfName);
        BoxCollider shelfSolid = SolidBox(run, shelf, ShelfSolid);
        ColorUtility.TryParseHtmlString(ShelfLightColour, out Color warm);
        var lights = new List<Light>();
        foreach (float x in ShelfLightXs)
        {
            Light l = MakeLight(run, shelf.transform, $"Shelf light (x {F(x, "0.0")})", new Vector3(x, ShelfLightY, ShelfLightZ), Quaternion.identity, LightType.Point, warm, ShelfLightIntensity, ShelfLightRange);
            AddToList(run, p.daylight, "warmLights", l, "a light under the long shelf, grazing the tile");
            lights.Add(l);
        }
        GameObject parts = Place(run, "CK_PartsWall", group, PartsAt, 180f, PartsName);
        BoxCollider partsSolid = SolidBox(run, parts, PartsSolid);

        // 3. The menu board over the till, and its picture light (35° down at the board).
        GameObject menu = Place(run, "CK_MenuBoard", group, MenuAt, 180f, MenuName);
        Light menuLight = MakeLight(run, menu.transform, MenuLightName, menu.transform.TransformPoint(MenuLightLocal), menu.transform.rotation * Quaternion.Euler(MenuLightDown, 180f, 0f),
                                    LightType.Spot, warm, MenuLightIntensity, MenuLightRange, MenuLightAngle);
        AddToList(run, p.daylight, "warmLights", menuLight, "the menu board's picture light");

        // 4. The grinder beside the dispenser, solid as its box; the beans, the plants and the plate (no colliders).
        Vector3 slide = new Vector3(p.grinderShift, 0f, 0f);
        GameObject grinder = Place(run, "CK_Grinder", group, GrinderAt + slide, 180f, GrinderName);
        BoxCollider grinderSolid = SolidBox(run, grinder, new Bounds(GrinderSolid.center + slide, GrinderSolid.size));
        var bags = new List<GameObject>();
        for (int i = 0; i < Bags; i++)
            bags.Add(Place(run, "CK_BeanBag", group, new Vector3(BagX0 + BagStep * i, BagY, BagZ), 180f, $"CK_BeanBag (shelf, {i + 1} of {Bags})"));
        GameObject pothosWest = Place(run, "CK_Pothos", group, PothosWestAt, 180f, PothosWestName);
        GameObject pothosEast = Place(run, "CK_Pothos", group, PothosEastAt, 180f, PothosEastName, new Vector3(-1f, 1f, 1f));
        GameObject plate = Place(run, "CK_StaffPlate", group, p.plateAt, 180f, PlateName);
        Physics.SyncTransforms();

        // 5. Night 1's trophy shelf (with its trophies) along the wall to the free stretch of the long shelf between the
        //    menu board and the dispenser (x 1.45..2.30), and down onto the long shelf's top, so no trophy stands in front
        //    of the menu (Claude, 10 Oct: Barnaby would have covered the drinks' lower lines). The shelf moves as one: its
        //    slots and copies keep their places on it.
        RaycastHit shelfTop;
        float? newTop = shelfSolid.Raycast(new Ray(new Vector3(TrophyShelfX, 3f, 17.875f), Vector3.down), out shelfTop, 3f) ? shelfTop.point.y : (float?)null;
        if (newTop == null) throw new InvalidOperationException("The long shelf's box isn't under the trophy shelf's new place.");
        float drop = newTop.Value - p.trophyShelf.position.y;
        Vector3 shift = new Vector3(TrophyShelfX - p.trophyShelf.position.x, drop, 0f);
        var trophiesTo = p.trophies.Select(t => t.transform.position + shift).ToList();
        Move(run, p.trophyShelf, p.trophyShelf.position + shift, p.trophyShelf.rotation,
             $"Night 1's trophy shelf along the wall to x {F(TrophyShelfX, "0.00")} (clear of the menu board) and {F(-drop * 100f, "0.0")} cm down onto the long shelf's top ({F(newTop.Value)})");
        foreach (GameObject t in p.trophies.Where(t => !t.transform.IsChildOf(p.trophyShelf)))
            Move(run, t.transform, t.transform.position + shift, t.transform.rotation, "a trophy of Night 1's shelf (not its child): moved with it");

        // 6. The pot plant clear of the bar's end; then anything left floating where the old pieces stood.
        if (p.potPlantMoves) Move(run, p.potPlant, p.potPlantTo, p.potPlant.rotation, "the pot plant stood in the back bar's east end: moved east, clear of the back door");
        Physics.SyncTransforms();
        var gameplay = new List<GameObject>();
        foreach (GameObject go in Floating(run, p.oldTops, new HashSet<GameObject>(p.off.Select(o => o.go)), gameplay))
            SwitchOff(run, go, "stood on the old back counter: with it off it would float");
        Physics.SyncTransforms();

        CheckBar2(run, p, backBar, backBarSolid, splash, shelf, shelfSolid, lights, parts, partsSolid, menu, menuLight, grinder, grinderSolid, bags,
                  pothosWest, pothosEast, plate, newTop.Value, trophiesTo, gameplay);
    }

    // ================================================================== Bar 2: the checks after

    static void CheckBar2(Run run, Bar2Plan p, GameObject backBar, BoxCollider backBarSolid, GameObject splash, GameObject shelf, BoxCollider shelfSolid, List<Light> lights,
                          GameObject parts, BoxCollider partsSolid, GameObject menu, Light menuLight, GameObject grinder, BoxCollider grinderSolid, List<GameObject> bags,
                          GameObject pothosWest, GameObject pothosEast, GameObject plate, float shelfTop, List<Vector3> trophiesTo, List<GameObject> gameplayFloating)
    {
        run.Line("The pieces where they belong (their renderers' boxes, 1.5 cm):");
        Expect(run, backBar, BackBarBox);
        Expect(run, splash, SplashBox);
        Expect(run, shelf, ShelfBox);
        Expect(run, parts, PartsBox);
        Expect(run, menu, MenuBox);
        Vector3 slide = new Vector3(p.grinderShift, 0f, 0f);
        Expect(run, grinder, new Bounds(GrinderBox.center + slide, GrinderBox.size));
        Expect(run, pothosWest, PothosWestBox);
        Expect(run, pothosEast, PothosEastBox);
        for (int i = 0; i < bags.Count; i++)
        {
            Vector3 at = new Vector3(BagX0 + BagStep * i, BagY, BagZ);
            Expect(run, bags[i], Box(at.x - BagLocal.max.x, at.y + BagLocal.min.y, at.z - BagLocal.max.z, at.x - BagLocal.min.x, at.y + BagLocal.max.y, at.z - BagLocal.min.z));
        }
        Expect(run, plate, Box(p.plateAt.x - .125f, PlateBottom, p.plateAt.z - .004f, p.plateAt.x + .125f, PlateBottom + .08f, p.plateAt.z));

        run.Line("The colliders where the solids are (5 mm):");
        foreach (var (what, box, solid) in new[] { ("the back bar", backBarSolid, BackBarSolid), ("the shelf's board", shelfSolid, ShelfSolid), ("the parts wall", partsSolid, PartsSolid),
                                                   ("the grinder", grinderSolid, new Bounds(GrinderSolid.center + slide, GrinderSolid.size)) })
            run.Check(MaxError(box.bounds, solid) <= .005f && box.enabled && !box.isTrigger, $"{what}: {BoundsText(box.bounds)}");

        run.Line("The drinks top (a ray down onto the back bar's box) and what stands on it:");
        foreach (float x in new[] { -7.0f, -5.6f, -3.0f, 0f, 4.6f })
        {
            float? y = RayOnto(new Collider[] { backBarSolid }, new Vector3(x, 3f, 17.45f));
            run.Check(y.HasValue && Mathf.Abs(y.Value - BackTop) <= .002f, $"at x {F(x, "0.0")}, z 17.45: {(y.HasValue ? F(y.Value) : "no top")}");
        }
        foreach (var (what, t) in new[] { ("the dispenser", p.dispenser), ("the cup stack", p.cupStack), ("the discard basin", p.basin) })
        {
            Bounds b = Footing(t);
            float? y = RayOnto(new Collider[] { backBarSolid }, new Vector3(b.center.x, 3f, b.center.z));
            run.Check(y.HasValue && Mathf.Abs(y.Value - BackTop) <= .002f, $"under {what} ({V(new Vector3(b.center.x, b.min.y, b.center.z))}) the back bar's top at {(y.HasValue ? F(y.Value) : "none")}");
            run.Check(y.HasValue && b.min.y - y.Value >= -.015f && b.min.y - y.Value <= .025f, $"…and it stands on it: its bottom at {F(b.min.y)} ({(y.HasValue ? $"{(b.min.y - y.Value) * 1000f:0} mm" : "?")} off the top)");
        }

        run.Line("The lights:");
        foreach (Light l in lights)
            run.Check(p.daylight.warmLights.Contains(l) && l.type == LightType.Point && Mathf.Abs(l.intensity - ShelfLightIntensity) < .001f && Mathf.Abs(l.range - ShelfLightRange) < .001f,
                      $"{l.name} at {V(l.transform.position)}: a point light #{ColorUtility.ToHtmlStringRGB(l.color)}, {F(l.intensity, "0.0")}, range {F(l.range, "0.0")}, in warmLights");
        Vector3 toBoard = new Vector3(0f, 2.45f, 17.97f) - menuLight.transform.position;
        run.Check(p.daylight.warmLights.Contains(menuLight) && Vector3.Distance(menuLight.transform.position, new Vector3(0f, 3.076f, 17.79f)) <= .01f &&
                  Mathf.Abs(Vector3.Angle(menuLight.transform.forward, Vector3.ProjectOnPlane(menuLight.transform.forward, Vector3.up)) - MenuLightDown) < .5f &&
                  menuLight.transform.forward.z > 0f && Vector3.Angle(menuLight.transform.forward, toBoard) < MenuLightAngle * .5f,
                  $"the menu's picture light at {V(menuLight.transform.position)}, {MenuLightDown:0}° down toward the board, in warmLights");

        run.Line("The back wall's pieces against each other:");
        Bounds menuNow = RendererBounds(menu.transform), shelfNow = RendererBounds(shelf.transform), dispenserNow = RendererBounds(p.dispenser);
        run.Check(menuNow.min.y - shelfNow.max.y >= .15f, $"the menu board clears the shelf: {(menuNow.min.y - shelfNow.max.y) * 100f:0} cm between");
        if (p.ceiling != null && p.ceiling.gameObject.activeInHierarchy)
        {
            Bounds ceil = RendererBounds(p.ceiling);
            run.Check(ceil.min.y - menuNow.max.y >= .05f, $"…and the ceiling ({F(ceil.min.y)}): {(ceil.min.y - menuNow.max.y) * 100f:0} cm under it");
        }
        else run.Info("No inside ceiling found on: the menu's top is 3.12 under the room's 3.20.");
        run.Check(!menuNow.Intersects(dispenserNow) && menuNow.min.y - dispenserNow.max.y >= .2f, $"…and the dispenser (its top {F(dispenserNow.max.y)})");
        Bounds grinderNow = RendererBounds(grinder.transform), basinNow = Footing(p.basin);
        run.Check(grinderNow.min.x - basinNow.max.x >= .05f && grinderNow.min.x - dispenserNow.max.x >= .02f && !grinderNow.Intersects(dispenserNow),
                  $"the grinder clears the basin by {(grinderNow.min.x - basinNow.max.x) * 100f:0} cm and the dispenser by {(grinderNow.min.x - dispenserNow.max.x) * 100f:0} cm");
        float standRoom = Flat(grinderNow.ClosestPoint(p.drinkStand.position) - p.drinkStand.position).magnitude;
        run.Check(standRoom >= .6f, $"…and the drinks station's stand point by {F(standRoom, "0.00")} m");
        Bounds partsNow = RendererBounds(parts.transform);
        string labels = SubmeshBox(parts, "CF_DrawerLabels", out Bounds labelBox) ? $"; its drawer labels at z {F(labelBox.min.z)}..{F(labelBox.max.z)}" : "";
        run.Check(Mathf.Abs(partsNow.min.z - PartsBox.min.z) <= .01f && Mathf.Abs(partsNow.max.z - PartsBox.max.z) <= .01f &&
                  (labels.Length == 0 || labelBox.max.z <= partsNow.min.z + .04f),
                  $"the parts wall's front faces the room: its drawers' fronts at z {F(partsNow.min.z)}, its back on the wall at {F(partsNow.max.z)}{labels}");

        run.Line("The trophy shelf:");
        TrophyShelf trophyShelf = p.trophyShelf.GetComponent<TrophyShelf>();
        run.Check(p.trophyShelf.gameObject.activeInHierarchy && trophyShelf.enabled, "Night 1's trophy shelf is on (the night finds it by type)");
        run.Check(p.hide.Where(h => h.c is Renderer r && r.transform.parent == p.trophyShelf).All(h => !((Renderer)h.c).enabled), "its own board and brackets are hidden (renderers off)");
        for (int i = 0; i < p.trophies.Count; i++)
        {
            Bounds tb = MeshBox(p.trophies[i]);
            run.Check(Vector3.Distance(p.trophies[i].transform.position, trophiesTo[i]) <= .001f,
                      $"{p.trophies[i].name} at {V(p.trophies[i].transform.position)}: on the long shelf's top ({F(shelfTop)}), {BoundsText(tb)}");
            if (tb.Intersects(MenuFrame)) run.Info($"FLAG: {p.trophies[i].name} touches the menu board's frame (the shelf is too shallow to stand it clear): see the open questions.");
            else if (tb.max.y > MenuFrame.min.y && tb.max.x > MenuFrame.min.x && tb.min.x < MenuFrame.max.x)
                run.Info($"FLAG: {p.trophies[i].name}, once earned, stands in front of the menu board's lower part (its top at {F(tb.max.y, "0.00")}, the board's bottom at {F(MenuFrame.min.y, "0.00")}).");
        }

        run.Line("What stays where it was, what went, what's clear:");
        Unchanged(run, p.kept);
        run.Check(p.linings.All(r => r.sharedMaterials.SequenceEqual(p.chalk)), "the green lining wears the service wall's chalk white");
        List<GameObject> floating = Floating(run, p.oldTops, new HashSet<GameObject>(p.off.Select(o => o.go)), null);
        run.Check(floating.Count == 0, floating.Count == 0 ? "nothing floats where the old back counter and cabinets stood" : "still floating: " + string.Join(", ", floating.Select(g => PathOf(g.transform))));
        run.Check(gameplayFloating.Count == 0, gameplayFloating.Count == 0 ? "no gameplay object stood on the old back counter without the new top under it"
                                                                           : "gameplay objects left without support: " + string.Join(", ", gameplayFloating.Select(g => PathOf(g.transform))));
        run.Check(p.off.All(o => !o.go.activeInHierarchy) && p.hide.All(h => !IsOn(h.c)), "everything on the back counter and the back wall that goes is off");
        Bounds wallDecor = Box(-7.40f, 1.14f, 17.45f, 7.40f, 3.20f, 18.0f);
        var leftOnWall = InScene<Renderer>(run, false).Where(r => r.enabled && r.gameObject.activeInHierarchy && !Mine(run, r.transform) && Small(r.bounds) &&
                                                                   r.bounds.min.y >= wallDecor.min.y && r.bounds.Intersects(wallDecor) && !r.transform.IsChildOf(p.trophyShelf) &&
                                                                   !r.transform.IsChildOf(p.dispenser) && !r.transform.IsChildOf(p.door) &&
                                                                   !(run.Player != null && r.transform.IsChildOf(run.Player))).ToList();
        foreach (Renderer r in leftOnWall) run.Info($"Still hung on the back wall (not this step's): {PathOf(r.transform)} ({BoundsText(r.bounds)})");
        if (p.potPlant != null)
        {
            Bounds pb = RendererBounds(p.potPlant);
            run.Check(!p.potPlant.gameObject.activeInHierarchy || !pb.Intersects(Shrink(BackBarBox, .005f)) && pb.max.x <= p.doorBox.min.x - .05f,
                      $"the pot plant clears the back bar's end and the door: {BoundsText(pb)}");
        }
        var mine = new List<GameObject> { backBar, splash, shelf, parts, menu, grinder, pothosWest, pothosEast, plate };
        mine.AddRange(bags);
        // The way to the back door: the door as it stands (Bins 2 moved it) and the brief's x 5.6..6.6, from the back bar's
        // front line to the door.
        Bounds doorWay = Box(Mathf.Min(5.6f, p.doorBox.min.x - .05f), .05f, 16.90f, Mathf.Max(6.6f, p.doorBox.max.x + .05f), 2.10f, p.doorBox.min.z - .01f);
        var inTheWay = mine.Where(m => m != plate && RendererBounds(m.transform).Intersects(doorWay)).Select(m => m.name).ToList();
        inTheWay.AddRange(new Collider[] { backBarSolid, shelfSolid, partsSolid, grinderSolid }.Where(c => c.bounds.Intersects(doorWay)).Select(c => c.name));
        run.Check(inTheWay.Count == 0, inTheWay.Count == 0 ? $"the way to the back door (x {F(doorWay.min.x, "0.00")}..{F(doorWay.max.x, "0.00")}, z 16.90 to the door) is clear"
                                                           : "in the way to the back door: " + string.Join(", ", inTheWay));
        run.Check(!Physics.CheckSphere(p.aceAtTheDoor.position, .3f, ~0, QueryTriggerInteraction.Ignore) ||
                  !Physics.OverlapSphere(p.aceAtTheDoor.position, .3f, ~0, QueryTriggerInteraction.Ignore).Any(c => Mine(run, c.transform)),
                  $"nothing of this step's is where Ace stands inside the back door ({V(p.aceAtTheDoor.position)})");
        Bounds plateNow = RendererBounds(plate.transform);
        run.Check(Mathf.Abs(plateNow.max.z - p.plateAt.z) <= .003f && Mathf.Abs(plateNow.min.y - PlateBottom) <= .003f && plateNow.min.x >= p.doorBox.min.x && plateNow.max.x <= p.doorBox.max.x,
                  $"the staff plate on the door's face: {BoundsText(plateNow)} ({p.plateHow})");
        Clear(run, mine);
    }

    static void PhotosBar2(Run run)
    {
        Photo(run, "1-the-back-bar-from-the-counter", new Vector3(0f, 1.65f, 15.0f), new Vector3(-1.0f, 1.4f, 18.0f), 70f);
        Photo(run, "2-the-parts-wall-close", new Vector3(-5.6f, 1.45f, 16.6f), new Vector3(-5.6f, 1.35f, 18.0f), 55f);
        Photo(run, "3-the-menu-from-the-queue", new Vector3(0f, 1.6f, 12.6f), new Vector3(0f, 2.45f, 18.0f), 50f);
        Photo(run, "4-the-drinks-corner", new Vector3(2.4f, 1.65f, 15.6f), new Vector3(3.6f, 1.2f, 17.6f), 62f);
        GameCameraPhoto(run, "5-the-game-camera");
    }

    // ================================================================== shared by both steps

    // The gameplay objects that must not move: the intake and queue, the work area (wherever it is), the drinks station,
    // where people start, wait and sit, Ace's start, the back door and Night 1's shelf (its root only: its trophies may
    // move onto the new shelf).
    static List<(Transform t, bool deep)> GameplayRoots(Run run)
    {
        var roots = new List<(Transform, bool)>();
        foreach (string path in new[] { IntakeGroupPath, DrinksGroupPath })
        {
            Transform t = Find(run, path);
            if (t != null) roots.Add((t, true));
        }
        foreach (string path in new[] { WorkAreaPath, TrophyShelfPath, BackDoorPath, AceAtTheDoorPath, "24 - The bins (Night 0)/Back door (outside)" })
        {
            Transform t = Find(run, path);
            if (t != null) roots.Add((t, false));
        }
        foreach (GameObject g in run.Scene.GetRootGameObjects())
            if (g.name == "SpawnPoint" || g.name == "PatronSpawnPoint" || g.name == "WaitingArea") roots.Add((g.transform, true));
        if (run.Player != null) roots.Add((run.Player, false));
        foreach (WaitingSpot s in InScene<WaitingSpot>(run, false)) roots.Add((s.transform, false));
        return roots;
    }

    // Where each gameplay transform and collider stands now (what this step switches off itself aside: Counter_Mesh, under
    // the counter station, goes off on purpose).
    static Snapshot Keep(Run run, List<(Transform t, bool deep)> roots, IEnumerable<GameObject> leaving)
    {
        var kept = new Snapshot();
        var seen = new HashSet<Transform>();
        List<GameObject> goes = leaving.Where(g => g != null).ToList();
        foreach (var (root, deep) in roots)
        {
            IEnumerable<Transform> all = deep ? root.GetComponentsInChildren<Transform>(true) : new[] { root };
            foreach (Transform t in all)
            {
                if (!seen.Add(t) || goes.Any(g => t.IsChildOf(g.transform))) continue;
                kept.poses.Add(new KeptPose { t = t, p = t.position, r = t.rotation });
                foreach (Collider c in t.GetComponents<Collider>())
                    if (c.enabled && t.gameObject.activeInHierarchy) kept.solids.Add(new KeptSolid { c = c, b = c.bounds });
            }
        }
        return kept;
    }

    static void Unchanged(Run run, Snapshot kept)
    {
        var moved = new List<string>();
        foreach (KeptPose k in kept.poses)
            if (k.t == null) moved.Add("(gone)");
            else if (Vector3.Distance(k.t.position, k.p) > .001f || Quaternion.Angle(k.t.rotation, k.r) > .1f)
                moved.Add($"{PathOf(k.t)} {V(k.p)} -> {V(k.t.position)}");
        foreach (KeptSolid s in kept.solids)
            if (s.c == null || !s.c.enabled || !s.c.gameObject.activeInHierarchy || MaxError(s.c.bounds, s.b) > .001f)
                moved.Add($"{(s.c != null ? PathOf(s.c.transform) : "(gone)")}'s collider");
        run.Check(moved.Count == 0, moved.Count == 0 ? $"the {kept.poses.Count} gameplay transforms and {kept.solids.Count} colliders stand where they stood (1 mm, 0.1°): " +
                                                       "the intake shelf and its slots, the counter station and its boxes, its stand point, the queue, the cameras, the work area, " +
                                                       "the dispenser, the cup stack, the basin, the drinks stand point, the waiting spots and seats, the spawn points, the doors, Ace's start"
                                                     : "MOVED: " + string.Join("; ", moved));
    }

    // Clear of the door lane (x -1..+1, z 0..2), of the waiting spots (0.8 m) and the seats (0.75 m).
    static void Clear(Run run, List<GameObject> mine)
    {
        Bounds lane = Box(-1f, 0f, 0f, 1f, 2.2f, 2f);
        var inLane = mine.Where(m => RendererBounds(m.transform).Intersects(lane)).Select(m => m.name).ToList();
        run.Check(inLane.Count == 0, inLane.Count == 0 ? "the door lane (x -1..+1, z 0..2) is clear of this step's pieces" : "in the door lane: " + string.Join(", ", inLane));
        var close = new List<string>();
        foreach (WaitingSpot s in InScene<WaitingSpot>(run, false))
        {
            float need = s is TableSeat ? .75f : .8f;
            foreach (GameObject m in mine)
            {
                float d = Flat(RendererBounds(m.transform).ClosestPoint(s.transform.position) - s.transform.position).magnitude;
                if (d < need) close.Add($"{m.name} {F(d, "0.00")} m from {PathOf(s.transform)}");
            }
        }
        run.Check(close.Count == 0, close.Count == 0 ? "every waiting spot keeps 0.8 m and every seat 0.75 m from this step's pieces" : "too close: " + string.Join("; ", close));
    }

    static CafeDaylight TheDaylight(Run run)
    {
        CafeDaylight[] all = InScene<CafeDaylight>(run, true);
        run.Require(all.Length == 1, $"Expected one CafeDaylight in the scene (its warmLights list); found {all.Length}.");
        run.Info($"CafeDaylight on {PathOf(all[0].transform)}: {all[0].warmLights.Length} warm light(s) now; by day they burn at {F(all[0].daytimeLampStrength, "0.0")} of their own intensity.");
        return all[0];
    }

    // ---- Repair bar 1's own tests (CafeRepairBarSteps.Blockers and CounterGaps), so this step proves the bench's place ----
    // and the counter's front before the bench moves in.

    static List<string> OpeningBlockers(Run run)
    {
        const float x0 = -5.80f, x1 = -2.60f, z0 = 13.66f, z1 = 14.45f, matZ0 = 13.70f, support = 1.010f;
        var found = new List<string>();
        Transform work = Find(run, WorkAreaPath);
        for (int ix = 0; ix <= 20; ix++)
        for (int iz = 0; iz <= 4; iz++)
        {
            float x = Mathf.Lerp(x0 + .05f, x1 - .05f, ix / 20f);
            float z = Mathf.Lerp(matZ0 + .02f, z1 - .05f, iz / 4f);
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 1.9f, z), Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = h.collider.transform;
                if (work != null && t.IsChildOf(work) || run.Player != null && t.IsChildOf(run.Player) || Under(t, "CK_BenchLamp")) continue;
                if (h.point.y <= support) continue;
                string what = $"{PathOf(t)} (solid to {F(h.point.y)} m)";
                if (!found.Contains(what)) found.Add(what);
            }
        }
        var space = new Bounds(new Vector3((x0 + x1) * .5f, .71f, (z0 + z1) * .5f + .01f), new Vector3(x1 - x0 - .04f, 1.38f, z1 - z0 - .04f));
        foreach (Renderer r in InScene<Renderer>(run, false))
        {
            if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
            if (work != null && r.transform.IsChildOf(work) || run.Player != null && r.transform.IsChildOf(run.Player) || Under(r.transform, "CK_RepairBench") || Under(r.transform, "CK_BenchLamp")) continue;
            Bounds b = r.bounds;
            if (b.size.x > 1.2f || b.size.z > 1.2f || !b.Intersects(space)) continue;
            string what = $"{PathOf(r.transform)} (a piece at {F(b.min.y, "0.00")} to {F(b.max.y, "0.00")} m)";
            if (!found.Contains(what)) found.Add(what);
        }
        return found;
    }

    static List<float> CounterGaps(Run run)
    {
        var gaps = new List<float>();
        for (int i = 0; i <= 48; i++)
        {
            float x = -7.30f + i * .25f;
            Collider[] hits = Physics.OverlapBox(new Vector3(x, .55f, (13.30f + 13.65f) * .5f), new Vector3(.05f, .05f, (13.65f - 13.30f) * .5f - .005f),
                                                 Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (!hits.Any(c => c != null && !(run.Player != null && c.transform.IsChildOf(run.Player)) && c.GetComponentInParent<CustomerBrain>() == null)) gaps.Add(x);
        }
        return gaps;
    }

    // ---- what is in the way of a new piece: solids by an overlap, small pieces by their renderers (big ones are walls and
    // floors, judged by their colliders), less what goes off and what the game owns ----
    static List<string> Blockers(Run run, IEnumerable<Bounds> volumes, HashSet<GameObject> goingOff, List<Transform> ignore)
    {
        var found = new List<string>();
        bool Skip(Transform t) =>
            Mine(run, t) || run.Player != null && t.IsChildOf(run.Player) || goingOff.Any(g => g != null && t.IsChildOf(g.transform)) || ignore.Any(i => i != null && t.IsChildOf(i));
        Renderer[] renderers = InScene<Renderer>(run, false);
        foreach (Bounds v in volumes)
        {
            foreach (Collider c in Physics.OverlapBox(v.center, v.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || Skip(c.transform)) continue;
                string what = $"{PathOf(c.transform)} (solid: {BoundsText(c.bounds)})";
                if (!found.Contains(what)) found.Add(what);
            }
            foreach (Renderer r in renderers)
            {
                if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy || Skip(r.transform)) continue;
                Bounds b = r.bounds;
                if (b.size.x > 1.2f || b.size.z > 1.2f || !b.Intersects(v)) continue;
                string what = $"{PathOf(r.transform)} ({BoundsText(b)})";
                if (!found.Contains(what)) found.Add(what);
            }
        }
        return found;
    }

    // ---- what would float: an active renderer (not the redesign's, not hung flat on a wall, not big) whose bottom lies on
    // one of the old tops (3 cm under to 15 cm over it, over its footprint), with nothing solid within 3 cm under it any more.
    // CafeRepairBarSteps.LeftFloating's idea, with the new top counted: what the new counter or back bar holds up stays.
    // Gameplay objects are never switched off: they go into `gameplay` (when given) and the checks fail on them. ----
    static List<GameObject> Floating(Run run, IList<Bounds> tops, HashSet<GameObject> goingOff, List<GameObject> gameplay)
    {
        var found = new List<GameObject>();
        if (tops.Count == 0) return found;
        foreach (Renderer r in InScene<Renderer>(run, false))
        {
            if (!r.enabled || r.forceRenderingOff || r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
            Transform t = r.transform;
            if (Mine(run, t) || run.Player != null && t.IsChildOf(run.Player) || goingOff.Any(g => g != null && t.IsChildOf(g.transform))) continue;
            // What this step moved was put where it is on purpose and has checks of its own (10 Oct: the pastry case's pastries,
            // moved over the old section at x 1.72..4.80, were taken for floaters and switched off; the run was rolled back).
            if (run.Record != null && run.Record.moved.Any(m => m != null && m.target != null && t.IsChildOf(m.target))) continue;
            Bounds b = r.bounds;
            if (!Small(b) || OnAWall(b)) continue;
            bool onTop = tops.Any(top => b.max.x > top.min.x + .01f && b.min.x < top.max.x - .01f && b.max.z > top.min.z + .01f && b.min.z < top.max.z - .01f &&
                                         b.min.y >= top.max.y - .03f && b.min.y <= top.max.y + .15f);
            if (!onTop) continue;
            GameObject prop = PropOf(r);
            if (found.Contains(prop) || gameplay != null && gameplay.Contains(prop)) continue;
            if (Held(run, prop.transform, b)) continue;
            if (HasGameplay(prop.transform)) { if (gameplay != null) gameplay.Add(prop); continue; }
            found.Add(prop);
        }
        return found;
    }

    // Something solid within 3 cm under its bottom, at its middle or at a point toward each corner (its own colliders aside).
    static bool Held(Run run, Transform prop, Bounds b)
    {
        float dx = b.extents.x * .6f, dz = b.extents.z * .6f;
        foreach (Vector3 d in new[] { Vector3.zero, new Vector3(dx, 0f, dz), new Vector3(-dx, 0f, dz), new Vector3(dx, 0f, -dz), new Vector3(-dx, 0f, -dz) })
        {
            RaycastHit? h = Down(run, new Vector3(b.center.x + d.x, b.min.y + .01f, b.center.z + d.z), .045f, c => c.transform.IsChildOf(prop));
            if (h != null) return true;
        }
        return false;
    }

    // A thing a renderer belongs to: its outermost prefab instance when that is one small thing, else the renderer's own object.
    static GameObject PropOf(Renderer r)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
        if (root != null && root.transform != r.transform.root)
        {
            Bounds b = RendererBounds(root.transform);
            if (b.size.x <= 2.6f && b.size.z <= 2.6f && b.size.y <= 3.4f) return root;
        }
        return r.gameObject;
    }

    // Something the game uses (never switched off by this step): an interactable, a slot area, a job, a customer, a physics
    // body, an animated thing, Night 1's shelf, the drinks station.
    static bool HasGameplay(Transform t) =>
        t.GetComponentInParent<Interactable>(true) != null || t.GetComponentInChildren<Interactable>(true) != null ||
        t.GetComponentInParent<ItemSlotArea>(true) != null || t.GetComponentInChildren<ItemSlotArea>(true) != null ||
        t.GetComponentInParent<JobBase>(true) != null || t.GetComponentInParent<CustomerBrain>(true) != null ||
        t.GetComponentInParent<Rigidbody>(true) != null || t.GetComponentInChildren<Rigidbody>(true) != null ||
        t.GetComponentInParent<Animator>(true) != null || t.GetComponentInParent<TrophyShelf>(true) != null || t.GetComponentInChildren<TrophyShelf>(true) != null ||
        t.GetComponentInParent<BeverageStation>(true) != null || t.GetComponentInChildren<BeverageStation>(true) != null;

    // Under the redesign's group (any step's pieces).
    static bool Mine(Run run, Transform t)
    {
        Transform redesign = run.Cafe.Find(RedesignGroup);
        return redesign != null && t.IsChildOf(redesign);
    }

    // Hung flat on a wall (the café's inner faces: x ±7.40, z 18.00), above the skirting.
    static bool OnAWall(Bounds b) =>
        b.min.y > .3f && (b.max.z >= 17.96f && b.size.z <= .12f || b.min.x <= -7.36f && b.size.x <= .12f || b.max.x >= 7.36f && b.size.x <= .12f);

    static bool Small(Bounds b) => b.size.x <= 2.6f && b.size.z <= 2.6f && b.size.y <= 3.4f;

    // The stone's height under a point, from the counter's shape (for the survey, before the counter is in).
    static float TopAt(float x, float z)
    {
        if (x >= LowX0 && x <= LowX1) return LowTop;
        if (x > OpeningX0 && x < OpeningX1 && z >= OpeningZ0) return 0f;
        return Top;
    }

    // Standing on the stone: its bottom on 1.095 (3 mm) and the stone under it by a ray down at its middle.
    static void Standing(Run run, string what, Bounds b, IEnumerable<Collider> solids)
    {
        float? y = RayOnto(solids, new Vector3(b.center.x, b.min.y + .3f, b.center.z));
        run.Check(y.HasValue && Mathf.Abs(b.min.y - y.Value) <= .003f,
                  $"{what} stands on the stone: its bottom at {F(b.min.y)}, the stone under its middle at {(y.HasValue ? F(y.Value) : "none")} (not floating, not sunk)");
    }

    // The base slots' devices (24 cm round each slot) clear of a piece; the upgrade slots, said.
    static void CheckSlotsClear(Run run, Bar1Plan p, Bounds piece, string what)
    {
        for (int i = 0; i < p.slots.Length; i++)
        {
            var room = new Bounds(p.slots[i].position + Vector3.up * DeviceRoom.y * .5f, DeviceRoom);
            bool clear = !room.Intersects(piece);
            if (i < p.baseSlots) run.Check(clear, $"{what} leaves base slot {i} ({p.slots[i].name} at x {F(p.slots[i].position.x, "0.00")}) room for its device");
            else if (!clear) run.Info($"FLAG: {what} reaches into the room of upgrade slot {i} ({p.slots[i].name} at x {F(p.slots[i].position.x, "0.00")}): a device there would touch it.");
        }
    }

    // The first thing the first-person crosshair would take on the way from the eye to a point (PlayerInteractor's rule: the
    // first available interactable, seeing past triggers; a solid that isn't one stops it).
    static string FirstUse(Run run, Vector3 eye, Vector3 aim)
    {
        Vector3 d = aim - eye;
        foreach (RaycastHit h in Physics.RaycastAll(eye, d.normalized, d.magnitude + .3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide).OrderBy(x => x.distance))
        {
            if (run.Player != null && h.collider.transform.IsChildOf(run.Player)) continue;
            Interactable it = h.collider.GetComponentInParent<Interactable>();
            if (it != null) return it.name;
            if (!h.collider.isTrigger) return PathOf(h.collider.transform) + " (a solid)";
        }
        return "nothing";
    }

    // ================================================================== small helpers

    // A box collider fitted to a world box on a placed piece (turned square to the world: its local frame maps the box).
    static BoxCollider SolidBox(Run run, GameObject piece, Bounds world)
    {
        BoxCollider box = CafeRedesignShared.AddComponent<BoxCollider>(run, piece);
        box.center = piece.transform.InverseTransformPoint(world.center);
        box.size = LocalSize(piece.transform, world.size);
        return box;
    }

    // A world size in a transform's own axes (square to the world: the turn is a multiple of 90° about up).
    static Vector3 LocalSize(Transform t, Vector3 world)
    {
        Vector3 s = t.InverseTransformVector(world);
        return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    static void Expect(Run run, GameObject piece, Bounds expected, float tolerance = .015f)
    {
        Bounds b = RendererBounds(piece.transform);
        float err = MaxError(b, expected);
        run.Check(err <= tolerance, $"{piece.name}: {BoundsText(b)} ({err * 1000f:0} mm from the kit's {BoundsText(expected)})");
    }

    static float MaxError(Bounds a, Bounds b) =>
        Mathf.Max(Mathf.Max(Mathf.Abs(a.min.x - b.min.x), Mathf.Abs(a.min.y - b.min.y), Mathf.Abs(a.min.z - b.min.z)),
                  Mathf.Max(Mathf.Abs(a.max.x - b.max.x), Mathf.Abs(a.max.y - b.max.y), Mathf.Abs(a.max.z - b.max.z)));

    static void At(Run run, Transform t, Vector3 expected, string what, float tolerance = .02f)
    {
        float d = Vector3.Distance(t.position, expected);
        run.Check(d <= tolerance, $"{what} at {V(t.position)} ({d * 100f:0.0} cm from {V(expected)})");
    }

    static void BoxAt(Run run, Collider c, Bounds expected, string what)
    {
        float err = MaxError(c.bounds, expected);
        run.Check(err <= .02f, $"{what}: {BoundsText(c.bounds)} ({err * 100f:0.0} cm from {BoundsText(expected)})");
    }

    static void AddIfOn(Run run, List<(GameObject, string)> off, string path, string why)
    {
        Transform t = Find(run, path);
        if (t == null) { run.Info($"{path}: not in the scene"); return; }
        if (!t.gameObject.activeSelf) { run.Info($"{path}: already off"); return; }
        off.Add((t.gameObject, why));
        run.Info($"{path}: {BoundsText(RendererBounds(t))}");
    }

    static void ReadSlots(ItemSlotArea area, out Transform[] slots, out int baseSlots)
    {
        var so = new SerializedObject(area);
        SerializedProperty list = so.FindProperty("slots");
        SerializedProperty count = so.FindProperty("baseSlots");
        slots = list != null && list.isArray ? Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as Transform).ToArray() : Array.Empty<Transform>();
        baseSlots = count != null ? count.intValue : 0;
    }

    // The first height a ray down meets on these colliders only (no other thing in the way counts).
    static float? RayOnto(IEnumerable<Collider> colliders, Vector3 from)
    {
        float? best = null;
        var ray = new Ray(from, Vector3.down);
        foreach (Collider c in colliders)
            if (c != null && c.enabled && c.Raycast(ray, out RaycastHit h, 10f) && (best == null || h.point.y > best.Value)) best = h.point.y;
        return best;
    }

    // What a thing stands on with: its renderers (or its solid colliders when it draws nothing of its own).
    static Bounds Footing(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length > 0) return RendererBounds(t);
        Collider[] cs = t.GetComponentsInChildren<Collider>(false).Where(c => c.enabled && !c.isTrigger).ToArray();
        if (cs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = cs[0].bounds;
        foreach (Collider c in cs) b.Encapsulate(c.bounds);
        return b;
    }

    // The world box of one material's part of a piece (its submesh's bounds), if the piece wears that material.
    static bool SubmeshBox(GameObject piece, string material, out Bounds box)
    {
        box = default;
        foreach (MeshRenderer r in piece.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length && i < f.sharedMesh.subMeshCount; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith(material, StringComparison.Ordinal)) continue;
                Bounds local = f.sharedMesh.GetSubMesh(i).bounds;
                if (local.size == Vector3.zero) return false;
                box = WorldBox(f.transform, local);
                return true;
            }
        }
        return false;
    }

    // A thing's world box from its meshes' own bounds: unlike its renderers' bounds, known while it is hidden (the trophies
    // wait hidden until they are earned).
    static Bounds MeshBox(GameObject go)
    {
        var boxes = new List<Bounds>();
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
            if (f.sharedMesh != null) boxes.Add(WorldBox(f.transform, f.sharedMesh.bounds));
        foreach (SkinnedMeshRenderer s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (s.sharedMesh != null) boxes.Add(WorldBox(s.transform, s.localBounds));
        if (boxes.Count == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds all = boxes[0];
        foreach (Bounds b in boxes) all.Encapsulate(b);
        return all;
    }

    // A local box's eight corners in the world, boxed square to the world.
    static Bounds WorldBox(Transform t, Bounds local)
    {
        var box = new Bounds(t.TransformPoint(local.center), Vector3.zero);
        for (int c = 0; c < 8; c++)
            box.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
        return box;
    }

    // A segment from a to b passing through a box.
    static bool Crosses(Vector3 a, Vector3 b, Bounds box)
    {
        Vector3 d = b - a;
        return box.IntersectRay(new Ray(a, d), out float t) && t <= d.magnitude;
    }

    static Material[] Swap(Material[] materials, string name, Material to)
    {
        Material[] copy = (Material[])materials.Clone();
        int i = Array.FindIndex(copy, m => m != null && m.name.StartsWith(name, StringComparison.Ordinal));
        if (i < 0 && copy.Length == 1) i = 0;
        if (i < 0) throw new InvalidOperationException($"No {name} slot to swap for {to.name}.");
        copy[i] = to;
        return copy;
    }

    static bool IsOn(Component c) => c is Renderer r ? r.enabled : c is Collider col ? col.enabled : c is Behaviour b ? b.enabled : c.gameObject.activeSelf;

    static bool Under(Transform t, string prefix)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    static Bounds Box(float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
        return b;
    }

    static Bounds Shrink(Bounds b, float by)
    {
        b.Expand(-2f * by);
        return b;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static bool Is3(float[] a) => a != null && a.Length == 3;
    static Vector3 V3(float[] a) => new Vector3(a[0], a[1], a[2]);

    // ================================================================== the read-only probe

    // The shape of things that have no collider of their own, read off their meshes: each mesh copied into a MeshCollider
    // on a hidden, unsaved object (NightZeroSteps.TopAt's way, without touching the piece), asked with Collider.Raycast
    // (nothing else in the scene answers), and destroyed at once. Nothing in the scene changes.
    sealed class Probe : IDisposable
    {
        readonly GameObject holder;
        readonly List<Collider> parts = new List<Collider>();

        public Probe(Transform root)
        {
            holder = new GameObject("Cafe bar steps - probe (hidden, unsaved, destroyed at once)") { hideFlags = HideFlags.HideAndDontSave };
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(false))
            {
                Renderer r = f.GetComponent<Renderer>();
                if (f.sharedMesh == null || r == null || !r.enabled) continue;
                var part = new GameObject(f.name) { hideFlags = HideFlags.HideAndDontSave };
                part.transform.SetParent(holder.transform, false);
                part.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
                part.transform.localScale = f.transform.lossyScale;
                var c = part.AddComponent<MeshCollider>();
                c.sharedMesh = f.sharedMesh;
                parts.Add(c);
            }
            Physics.SyncTransforms();
        }

        public int Parts => parts.Count;

        public RaycastHit? Cast(Vector3 from, Vector3 direction, float distance)
        {
            RaycastHit? best = null;
            var ray = new Ray(from, direction);
            foreach (Collider c in parts)
                if (c != null && c.Raycast(ray, out RaycastHit h, distance) && (best == null || h.distance < best.Value.distance)) best = h;
            return best;
        }

        public float? Down(float x, float z)
        {
            RaycastHit? h = Cast(new Vector3(x, 10f, z), Vector3.down, 20f);
            return h.HasValue ? h.Value.point.y : (float?)null;
        }

        public void Dispose()
        {
            if (holder != null) Object.DestroyImmediate(holder);
        }
    }

    // ================================================================== the snapshot and the manifest's shape

    internal sealed class KeptPose { public Transform t; public Vector3 p; public Quaternion r; }
    internal sealed class KeptSolid { public Collider c; public Bounds b; }
    internal sealed class Snapshot
    {
        public readonly List<KeptPose> poses = new List<KeptPose>();
        public readonly List<KeptSolid> solids = new List<KeptSolid>();
    }

    // cafe_bar.json, as much of it as this step reads (JsonUtility ignores the rest).
    [Serializable] internal sealed class KitManifest { public KitPiece[] pieces; }
    [Serializable] internal sealed class KitPiece { public string name; public KitPlacement placement; public KitLight light; }
    [Serializable] internal sealed class KitPlacement { public float[] position; public float[] rotation; }
    [Serializable] internal sealed class KitLight
    {
        public string type, colour, shadows;
        public float[] world_position, local_position, local_euler, head_centre_world;
        public float intensity, range, spot_angle;
    }
}
#endif
