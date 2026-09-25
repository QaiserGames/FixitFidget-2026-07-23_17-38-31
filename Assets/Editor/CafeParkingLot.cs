#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The café's customer car park, and where visitors come from (24 Sept).
//
// WHAT THE OWNER ASKED FOR
//   "have a parking lot ... so it shows those cars ... an actual design of how a
//    cafe really is including the parking lot to get in there, just so we can make
//    it that the npcs don't just randomly spawn but actually come from somewhere."
//
// THE DESIGN (world metres; the café door is at x 0 on the front street)
//  * The three little shop houses opposite the café (Front 1-3) make way for the
//    café's own car park; they are hidden, not deleted. The planted bed in front of
//    the middle one goes too (stall 1's nose sat in it); the other bed stays in the
//    car park's corner beside a new planted island with a tree.
//  * Three 45° stalls face the café along a one-way aisle beside the terrace's front
//    wall. Cars come in from the east street (a right turn from the southbound lane)
//    and leave onto the west street (a right turn into the northbound lane, just
//    before its stop line), so no car ever turns across oncoming traffic. Small city
//    cars only: the POLYGON hatchback fits with 30 cm to spare; the sedans don't.
//  * Both driveways are proper dropped kerbs: the kerb and the sidewalk slab are cut
//    back and an asphalt crossover ramps from the road up to the car park.
//  * A zebra crossing joins the car park's pedestrian gate to the café door. Drivers
//    give way at it, and traffic never queues across it.
//  * People on foot come from two neighbours' front doors (the Saffron house on the
//    west street and the first courtyard shop on the east street) and use the
//    junction crossings with the walk signal.
//  * Every junction box, with its crossings, is kept clear: cars only drive in when
//    they can get all the way across.
//
// RUNTIME: CafeArrivals (on the new group) runs it; CafeCar drives the cars;
// NpcJourney walks the people; StreetCrossing and StreetLife make traffic give way.
//
// Nothing is saved: look at the photos, run the check, then save the scene.
// "Undo the car park" puts everything back.
public static class CafeParkingLot
{
    const string Menu = "Fixit Fidget/Cafe parking lot/";
    const string Tag = "[Parking lot] ";
    const string CafeRoot = "ACE'S CAFE - layout study 02";
    const string GroupName = "19 - Cafe parking and arrivals";
    const string MeshFolder = "Assets/Art/CafeParking";
    const string GeometryPath = MeshFolder + "/Cafe parking geometry.asset";
    const string Palette = "Assets/Playtests/AcesCafeLayout/Street palette.asset";
    const string Veh = "Assets/Synty/PolygonCity/Prefabs/Vehicles/";
    const string Prop = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string Env = "Assets/Synty/PolygonCity/Prefabs/Environments/";
    const string AtlasFolder = "Assets/Synty/PolygonCity/Materials/Alts/";
    const string CarModel = "SM_Veh_Car_Small_01";
    const string SignModel = "Assets/Art/Models/SignAcesCafe.fbx";
    const string RevertKey = "FixitFidget.CafeParkingLot.Revert";

    // ---- the block the car park sits in (as the V4 street grid built it) ----
    const float BlockWest = -9.4f, BlockEast = 10.1f, BlockSouth = -34f, BlockNorth = -10.8f;
    const float SlabTop = -.02f, SlabBottom = -.12f, KerbTop = .02f, KerbWidth = .14f;
    const float WestKerbInner = BlockWest + KerbWidth, EastKerbInner = BlockEast - KerbWidth, NorthKerbInner = BlockNorth - KerbWidth;
    // ---- the car park ----
    const float LotWest = WestKerbInner, LotEast = EastKerbInner, LotSouth = -21.45f, LotNorth = -13.4f;
    const float Surface = 0f, Pavement = -.02f, Road = -.18f;
    const float CarOnRoad = -.17f, CarInLot = .005f;
    const float AisleZ = -19.66f, StallZ = -16.03f, StallYaw = 315f, StallRadius = 4f, StallRun = 3.477f;
    static readonly float[] StallX = { 2.714f, -1.671f, -6.056f };
    const string EntryLane = "Through lane 3", ExitLane = "Through lane 0";
    const float LaneIn = 11.5f, TurnInRadius = 3.5f;     // southbound lane of the east street
    const float LaneOut = -10.8f, TurnOutRadius = 3.5f;  // northbound lane of the west street
    const float ExitWaitX = LaneOut + TurnOutRadius;     // -7.3: where leaving cars wait for a gap
    const float RampEast = 9.3f, RampWest = -8.6f;       // where the crossovers meet the car park
    const float DrivewaySouth = -21.25f, DrivewayNorth = -16.85f;
    const float CarLengthMax = 4.35f, CarWidthMax = 2.1f; // design footprint: hatchback + margins
    // ---- the zebra outside the café ----
    const float ZebraWest = -1.0f, ZebraEast = 1.4f, ZebraX = .2f;
    const float FrontRoadNorth = -5.2f, FrontRoadSouth = -10.8f;
    // Pedestrian gate: where the car park's walkers join the sidewalk.
    static readonly Vector3 Gate = new Vector3(0f, Pavement, -12.25f);
    // Things standing on the sidewalk that walks and bollards keep away from.
    static readonly Vector2 WestSignalPost = new Vector2(-8.95f, -13.2f);
    static readonly Vector2 LampPost = new Vector2(3.9f, -13.72f);

    static readonly Vector2[] Junctions = { new Vector2(-12.2f, -8f), new Vector2(12.9f, -8f), new Vector2(-12.2f, 23.5f), new Vector2(12.9f, 23.5f) };
    static readonly string[] CarAtlases = { "PolygonCity_01_A", "PolygonCity_01_B", "PolygonCity_02_A", "PolygonCity_03_A", "PolygonCity_04_C" };

    [MenuItem(Menu + "1 - Build the car park and arrivals")]
    static void BuildMenu() => Run("Build", Build);

    [MenuItem(Menu + "2 - Check paths and clearances")]
    static void CheckMenu() => Run("Check", () => Check(true));

    [MenuItem(Menu + "3 - Measure walking room")]
    static void RoomMenu() => Run("Walking room", MeasureWalkingRoomMenu);

    [MenuItem(Menu + "Photograph the car park")]
    static void PhotoMenu() => Run("Photos", () => Photograph("car-park"));

    [MenuItem(Menu + "Undo the car park")]
    static void UndoMenu() => Run("Undo", Revert);

    [MenuItem(Menu + "1 - Build the car park and arrivals", true)]
    [MenuItem(Menu + "2 - Check paths and clearances", true)]
    [MenuItem(Menu + "3 - Measure walking room", true)]
    [MenuItem(Menu + "Photograph the car park", true)]
    [MenuItem(Menu + "Undo the car park", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(Menu + "Play - report arrivals now")]
    static void LiveMenu() => Run("Live", LiveReport);

    [MenuItem(Menu + "Play - report arrivals now", true)]
    static bool Playing() => EditorApplication.isPlaying;

    static void Run(string title, Func<string> step)
    {
        try { Debug.Log(Tag + title + ": " + step()); }
        catch (Exception e) { Debug.LogError(Tag + title + " FAILED: " + e); }
    }

    // ================================================================== build

    [Serializable]
    class RevertRecord
    {
        public List<string> hidden = new List<string>();
        public List<string> meshFilters = new List<string>();
        public List<string> originalMeshes = new List<string>();
    }

    static Dictionary<string, Material> mats;
    static readonly List<Mesh> newMeshes = new List<Mesh>();

    static string Build()
    {
        Transform cafe = FindRoot(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first.");
        if (cafe.Find(GroupName) != null) return "Already built (" + GroupName + " exists). Run \"Undo the car park\" first to rebuild.";
        Transform door = FindRoot("SpawnPoint") ?? throw new InvalidOperationException("SpawnPoint (the café door) is missing.");
        var life = Object.FindFirstObjectByType<StreetLife>() ?? throw new InvalidOperationException("No StreetLife in the scene.");
        if (!life.actors.Any(a => a != null && a.trafficGroup == EntryLane) || !life.actors.Any(a => a != null && a.trafficGroup == ExitLane))
            throw new InvalidOperationException("The street has no '" + EntryLane + "' or '" + ExitLane + "'.");
        Transform surrounds = cafe.Find("09 - neighborhood surrounds V3") ?? throw new InvalidOperationException("09 - neighborhood surrounds V3 is missing.");
        Transform grid = FindUnder(cafe, "V4 - neighborhood street grid") ?? throw new InvalidOperationException("V4 - neighborhood street grid is missing.");
        MeshFilter slabs = grid.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Sidewalk stone");
        MeshFilter kerbs = grid.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Curb concrete");
        if (slabs == null || kerbs == null) throw new InvalidOperationException("The street grid has no 'Sidewalk stone' or 'Curb concrete' mesh.");
        LoadPalette();
        var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Veh + CarModel + ".prefab");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build the café car park");
        var record = new RevertRecord();
        var notes = new StringBuilder();
        newMeshes.Clear();

        // 1  Make way: the three front shop houses, and the planted bed in front of the middle one.
        int houses = 0;
        foreach (string name in new[] { "Front - 1 - neighborhood shop house", "Front - 2 - neighborhood shop house", "Front - 3 - neighborhood shop house" })
        {
            Transform house = surrounds.Find(name);
            if (house == null || !house.gameObject.activeSelf) continue;
            Hide(house.gameObject, record);
            houses++;
        }
        int plants = 0;
        Transform gardens = FindUnder(cafe, "Sidewalk pocket gardens");
        if (gardens != null)
            foreach (Transform plant in gardens)
            {
                Vector3 p = plant.position;
                if (p.x > -.8f && p.x < 2.8f && p.z > -14.9f && p.z < -13.2f && plant.gameObject.activeSelf) { Hide(plant.gameObject, record); plants++; }
            }
        int bedTriangles = 0;
        Transform pockets = FindUnder(cafe, "V4 - small planted sidewalk pockets");
        if (pockets != null)
            foreach (MeshFilter filter in pockets.GetComponentsInChildren<MeshFilter>(true))
                bedTriangles += CutTriangles(filter, record, (a, b, c) => Inside(Centroid(a, b, c), -.65f, 2.65f, -14.75f, -13.25f));
        notes.AppendLine($"Made way: {houses} front shop houses hidden, {plants} plants and {bedTriangles} triangles of the middle planted bed removed (all kept for Undo).");

        // 2  Dropped kerbs. The block's sidewalk slab and its east and west kerbs are single
        //    boxes in the combined street meshes; they come out whole and come back in
        //    pieces with the two driveways left open for the crossover ramps.
        int slabTriangles = CutTriangles(slabs, record, (a, b, c) =>
            OnBoxCorners(a, b, c, new Vector3(BlockWest, SlabBottom, BlockSouth), new Vector3(BlockEast, SlabTop, BlockNorth)));
        int kerbTriangles = CutTriangles(kerbs, record, (a, b, c) =>
            OnBoxCorners(a, b, c, new Vector3(EastKerbInner, Road, BlockSouth), new Vector3(BlockEast, KerbTop, BlockNorth))
            || OnBoxCorners(a, b, c, new Vector3(BlockWest, Road, BlockSouth), new Vector3(WestKerbInner, KerbTop, BlockNorth)));
        if (slabTriangles == 0 || kerbTriangles == 0)
            throw new InvalidOperationException($"The street grid isn't the one this was designed on (slab {slabTriangles}, kerb {kerbTriangles} triangles found). Nothing was changed: use Edit > Undo.");

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Car park group");
        group.transform.SetParent(cafe, false);
        Transform ground = Child(group.transform, "Sidewalk, kerbs and crossovers");
        Material paving = kerbs == null ? M("Sidewalk stone") : slabs.GetComponent<MeshRenderer>().sharedMaterial;
        Material kerbMaterial = kerbs.GetComponent<MeshRenderer>().sharedMaterial;
        // The slab: south of the driveways and north of them (the car park covers the middle).
        Box(ground, "Sidewalk slab", new Vector3(WestKerbInner, -.19f, BlockSouth), new Vector3(EastKerbInner, SlabTop, DrivewaySouth), paving);
        Box(ground, "Sidewalk slab", new Vector3(WestKerbInner, -.19f, DrivewayNorth), new Vector3(EastKerbInner, SlabTop, NorthKerbInner), paving);
        foreach (var (x0, x1) in new[] { (EastKerbInner, BlockEast), (BlockWest, WestKerbInner) })
        {
            Box(ground, "Kerb", new Vector3(x0, Road, BlockSouth), new Vector3(x1, KerbTop, DrivewaySouth), kerbMaterial);
            Box(ground, "Kerb", new Vector3(x0, Road, DrivewayNorth), new Vector3(x1, KerbTop, BlockNorth), kerbMaterial);
        }
        Material asphalt = M("Asphalt");
        Ramp(ground, "Driveway crossover in (east street)", BlockEast, RampEast, asphalt);
        Ramp(ground, "Driveway crossover out (west street)", BlockWest, RampWest, asphalt);
        notes.AppendLine($"Dropped kerbs: the block's slab ({slabTriangles} triangles) and side kerbs ({kerbTriangles}) rebuilt with 4.4 m crossovers at z {DrivewaySouth:0.00}..{DrivewayNorth:0.00}.");

        // 3  The car park: asphalt, stall lines, wheel stops, aisle arrows.
        Transform lot = Child(group.transform, "Car park surface and paint");
        Box(lot, "Asphalt", new Vector3(RampWest, Surface - .05f, LotSouth), new Vector3(RampEast, Surface, LotNorth), asphalt);
        foreach (var (x0, x1) in new[] { (RampEast, LotEast), (LotWest, RampWest) })
        {
            Box(lot, "Asphalt", new Vector3(x0, Surface - .05f, LotSouth), new Vector3(x1, Surface, DrivewaySouth), asphalt);
            Box(lot, "Asphalt", new Vector3(x0, Surface - .05f, DrivewayNorth), new Vector3(x1, Surface, LotNorth), asphalt);
        }
        Material white = M("Crossing paint"), concrete = M("Curb concrete");
        Vector3 h = Heading(StallYaw);
        float pitch = StallX[0] - StallX[1];
        // Angled stalls are parallelograms: the side lines run with the cars, from the aisle
        // to the head of the stalls, and neighbouring stalls share them.
        for (int k = 0; k <= StallX.Length; k++)
        {
            float xAtStallZ = StallX[0] + pitch * .5f - k * pitch;
            Vector2 south = new Vector2(xAtStallZ + (StallZ - -18.45f), -18.45f);
            Vector2 north = new Vector2(xAtStallZ - (-13.65f - StallZ), -13.65f);
            if (north.x < LotWest + .1f) north = new Vector2(LotWest + .1f, StallZ - (LotWest + .1f - xAtStallZ));
            Vector2 mid = (south + north) * .5f;
            Paint(lot, "Stall line", new Vector3(mid.x, Surface, mid.y), new Vector2(.1f, Vector2.Distance(south, north)), StallYaw, white);
        }
        for (int i = 0; i < StallX.Length; i++)
            Slab(lot, "Wheel stop", new Vector3(StallX[i], Surface + .05f, StallZ) + h * 1.95f, new Vector3(1.5f, .1f, .16f), StallYaw, concrete);
        foreach (float x in new[] { 5.2f, -3.2f }) Arrow(lot, new Vector3(x, Surface, AisleZ), 270f, white);
        notes.AppendLine($"Car park: x {LotWest:0.00}..{LotEast:0.00}, z {LotSouth}..{LotNorth}; {StallX.Length} stalls at 45° facing the café along a one-way aisle at z {AisleZ}.");

        // 4  Planting, a lamp, the signs, bollards along the sidewalk.
        Transform dressing = Child(group.transform, "Car park dressing");
        Island(dressing, new Vector3(5.6f, Surface, -15.3f), .85f);
        TryPut(dressing, Env + "SM_Env_Tree_02.prefab", new Vector3(5.75f, Surface + .12f, -15.2f), 35f, 1.05f);
        TryPut(dressing, Prop + "SM_Prop_LightPole_Base_01.prefab", new Vector3(LampPost.x, Surface, LampPost.y), 180f, 1f);
        TryPut(dressing, Prop + "SM_Prop_Sign_Parking_01.prefab", new Vector3(9.7f, Pavement, -21.7f), 90f, 1f);
        CafeSign(dressing, new Vector3(8.9f, Surface, -14.95f));
        var pedestrianPaths = new List<Vector3[]>();
        var stallsData = BuildStalls(pedestrianPaths);
        int bollards = Bollards(dressing, pedestrianPaths);
        notes.AppendLine($"Dressing: a planted island with a tree, a lamp, the P sign, the Ace's Café customer parking sign, {bollards} bollards.");

        // 5  The zebra outside the café, with its sign.
        Transform zebra = Child(group.transform, "Zebra crossing outside the café");
        for (int i = 0; i < 8; i++)
            Paint(zebra, "Zebra stripe", new Vector3(ZebraX, Road + .012f, FrontRoadSouth + .35f + i * .7f), new Vector2(ZebraEast - ZebraWest, .36f), 0f, white);
        CrossingSign(zebra, new Vector3(ZebraWest - .55f, Pavement, FrontRoadSouth - .45f));
        notes.AppendLine($"Zebra crossing at x {ZebraWest}..{ZebraEast} from the car park's gate to the café door.");

        // 6  Cars: a small pool of hatchbacks that drive in and out.
        var cars = new List<CafeCar>();
        float bodyOffset = 0f;
        if (carPrefab != null)
        {
            Transform pool = Child(group.transform, "Customer cars (pool)");
            float carLength = 0f, carWidth = 0f;
            for (int i = 0; i < CarAtlases.Length; i++)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, pool);
                Undo.RegisterCreatedObjectUndo(car, "Customer car");
                car.name = "Customer car " + (i + 1);
                car.transform.SetPositionAndRotation(new Vector3(StallX[0], CarInLot, StallZ), Quaternion.Euler(0f, StallYaw, 0f));
                foreach (var c in car.GetComponentsInChildren<Collider>(true)) { c.enabled = false; RecordPrefab(c); }
                Recolor(car, AssetDatabase.LoadAssetAtPath<Material>(AtlasFolder + CarAtlases[i] + ".mat"));
                Bounds local = LocalBounds(car.transform);
                bodyOffset = local.center.z;
                carLength = local.size.z;
                carWidth = local.size.x;
                var wheels = car.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Wheel_")).ToArray();
                float radius = wheels.Select(w => w.GetComponent<Renderer>()).Where(r => r != null).Select(r => r.bounds.extents.y).DefaultIfEmpty(.33f).Max();
                var cafeCar = Undo.AddComponent<CafeCar>(car);
                cafeCar.Configure(wheels, radius, Vector3.right, carLength, carWidth);
                EditorUtility.SetDirty(cafeCar);
                car.SetActive(false);
                RecordPrefab(car);
                cars.Add(cafeCar);
            }
            notes.AppendLine($"Cars: {cars.Count} hatchbacks ({carLength:0.00} x {carWidth:0.00} m, body centre {bodyOffset:+0.00;-0.00} m ahead of the pivot), waiting out of sight until the café opens.");
        }
        else notes.AppendLine("Cars: the POLYGON hatchback isn't in this checkout, so every visitor comes on foot.");

        // 7  The arrival data: paths are planned for the body's centre, the car's pivot sits behind it.
        Vector4[] turnIn = Offset(TurnInPath(), bodyOffset);
        Vector4[] turnOut = Offset(TurnOutPath(), bodyOffset);
        foreach (var stall in stallsData)
        {
            stall.entry = Offset(stall.entry, bodyOffset);
            stall.backOut = Offset(stall.backOut, bodyOffset);
            stall.toExit = Offset(stall.toExit, bodyOffset);
        }
        var arrivals = Undo.AddComponent<CafeArrivals>(group);
        arrivals.EditorConfigure(door, FootRoutes(), stallsData.ToArray(), LotToDoor(), EntryLane, turnIn, ExitLane, turnOut,
                                 cars.ToArray(), Crossings(), KeepClear());
        notes.Append(MeasureWalkingRoom(arrivals, group.transform));
        EditorUtility.SetDirty(arrivals);

        SaveMeshes();
        Undo.CollapseUndoOperations(undoGroup);
        EditorPrefs.SetString(RevertKey, JsonUtility.ToJson(record));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string check = Check(false);
        string photos = Photograph("car-park-built");
        return "\n" + notes + check + "\nPhotos: " + photos + "\nNot saved yet: look at the photos, then Ctrl+S to keep it.";
    }

    // ------------------------------------------------------------------ paths

    static Vector3 Heading(float yaw) => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

    // Car height (its wheels' contact): on the road, up the crossover, level in the car park.
    static float CarHeight(float x)
    {
        if (x >= BlockEast || x <= BlockWest) return CarOnRoad;
        if (x > RampEast) return Mathf.Lerp(CarInLot, CarOnRoad, (x - RampEast) / (BlockEast - RampEast));
        if (x < RampWest) return Mathf.Lerp(CarInLot, CarOnRoad, (RampWest - x) / (RampWest - BlockWest));
        return CarInLot;
    }

    static void ArcPoses(List<Vector4> into, Vector2 centre, float radius, float fromDeg, float toDeg)
    {
        // Clockwise seen from above (x right, z up): the angle decreases.
        int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad * radius / .1f));
        for (int i = into.Count == 0 ? 0 : 1; i <= steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
            float x = centre.x + radius * Mathf.Cos(a), z = centre.y + radius * Mathf.Sin(a);
            float yaw = Mathf.Repeat(Mathf.Atan2(Mathf.Sin(a), -Mathf.Cos(a)) * Mathf.Rad2Deg, 360f);
            into.Add(new Vector4(x, CarHeight(x), z, yaw));
        }
    }

    static void LinePoses(List<Vector4> into, Vector2 from, Vector2 to, float yaw)
    {
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / .1f));
        for (int i = into.Count == 0 ? 0 : 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            into.Add(new Vector4(p.x, CarHeight(p.x), p.y, yaw));
        }
    }

    static Vector4[] TurnInPath()
    {
        var poses = new List<Vector4>();
        ArcPoses(poses, new Vector2(LaneIn - TurnInRadius, AisleZ + TurnInRadius), TurnInRadius, 0f, -90f);
        return poses.ToArray();
    }

    static Vector4[] TurnOutPath()
    {
        var poses = new List<Vector4>();
        ArcPoses(poses, new Vector2(ExitWaitX, AisleZ + TurnOutRadius), TurnOutRadius, -90f, -180f);
        return poses.ToArray();
    }

    // Where a car leaves the aisle to swing into stall i.
    static float StallX0(int i) => StallX[i] + .7071f * StallRadius + .7071f * StallRun;

    static List<CafeArrivals.Stall> BuildStalls(List<Vector3[]> pedestrianPaths)
    {
        var stalls = new List<CafeArrivals.Stall>();
        float aisleStart = LaneIn - TurnInRadius;
        for (int i = 0; i < StallX.Length; i++)
        {
            float x0 = StallX0(i);
            var entry = new List<Vector4>();
            LinePoses(entry, new Vector2(aisleStart, AisleZ), new Vector2(x0, AisleZ), 270f);
            var turn = new List<Vector4>();
            ArcPoses(turn, new Vector2(x0, AisleZ + StallRadius), StallRadius, -90f, -135f);
            Vector4 end = turn[turn.Count - 1];
            LinePoses(turn, new Vector2(end.x, end.z), new Vector2(StallX[i], StallZ), StallYaw);
            entry.AddRange(turn.Skip(1));
            var backOut = new List<Vector4>(turn);
            backOut.Reverse();
            var toExit = new List<Vector4>();
            LinePoses(toExit, new Vector2(x0, AisleZ), new Vector2(ExitWaitX, AisleZ), 270f);

            var walk = StallWalk(i);
            pedestrianPaths.Add(walk.points);
            stalls.Add(new CafeArrivals.Stall
            {
                name = "Stall " + (i + 1),
                entry = entry.ToArray(),
                backOut = backOut.ToArray(),
                toExit = toExit.ToArray(),
                nearExit = SweepsExitWait(turn),
                walk = walk,
            });
        }
        pedestrianPaths.Add(LotToDoor().points);
        foreach (var route in FootRoutes()) pedestrianPaths.Add(route.points);
        return stalls;
    }

    // Does parking in (or backing out of) this stall sweep the spot where leaving cars wait?
    static bool SweepsExitWait(List<Vector4> turn)
    {
        var waiting = Footprint(new Vector3(ExitWaitX, 0f, AisleZ), 270f, CarLengthMax, CarWidthMax);
        foreach (var p in turn)
            if (RectGap(Footprint(new Vector3(p.x, 0f, p.z), p.w, CarLengthMax, CarWidthMax), waiting) < .25f) return true;
        return false;
    }

    // Driver's door -> along the gap between the cars -> past the nose -> the sidewalk -> the gate.
    // Left-hand drive (traffic keeps right), so the driver gets out on the car's left.
    static CafeArrivals.Route StallWalk(int i)
    {
        Vector3 c = new Vector3(StallX[i], Surface, StallZ);
        Vector3 h = Heading(StallYaw), left = new Vector3(-h.z, 0f, h.x);
        float gapMid = (StallX[0] - StallX[1]) * .7071f * .5f;   // half the perpendicular pitch: the gap's middle
        Vector3 door = c + left * gapMid + h * .4f;
        Vector3 past = c + left * gapMid + h * (CarLengthMax * .5f + .5f);
        var points = new List<Vector3> { door, past };
        if (i < StallX.Length - 1)
        {
            points.Add(new Vector3(past.x + .05f, Surface, LotNorth + .1f));
            points.Add(new Vector3(past.x + .35f, Pavement, -12.75f));
        }
        else
        {
            // The last stall's driver walks round the car's nose, clear of the junction's signal post.
            points.Add(new Vector3(-8.6f, Surface, -14.2f));
            points.Add(new Vector3(-7.6f, Surface, LotNorth + .05f));
            points.Add(new Vector3(-6.3f, Pavement, -12.75f));
        }
        points.Add(Gate);
        return new CafeArrivals.Route { name = "Stall " + (i + 1) + " driver", points = points.ToArray(), crossingAtSegment = Enumerable.Repeat(-1, points.Count - 1).ToArray() };
    }

    // Gate -> kerb -> over the zebra -> the café's patio (the door point is added at runtime).
    static CafeArrivals.Route LotToDoor()
    {
        var points = new[]
        {
            Gate,
            new Vector3(ZebraX, Pavement, FrontRoadSouth - .15f),
            new Vector3(ZebraX, Road, FrontRoadSouth + .1f),
            new Vector3(ZebraX, Road, FrontRoadNorth - .1f),
            new Vector3(ZebraX, Pavement, FrontRoadNorth + .15f),
            new Vector3(ZebraX, Surface, -3.4f),
            new Vector3(.12f, Surface, -2.35f),
        };
        return new CafeArrivals.Route { name = "Car park gate to the café door", points = points, crossingAtSegment = new[] { -1, 0, 0, 0, -1, -1 } };
    }

    static CafeArrivals.Route[] FootRoutes() => new[]
    {
        new CafeArrivals.Route
        {
            name = "From the Saffron house (west street)", weight = 1f,
            points = new[]
            {
                new Vector3(-17.45f, .15f, -3.6f), new Vector3(-16.45f, Pavement, -3.6f),
                new Vector3(-15.2f, Pavement, -3.85f), new Vector3(-14.95f, Road, -3.85f),
                new Vector3(-9.65f, Road, -3.85f), new Vector3(-9.4f, Pavement, -3.85f),
                new Vector3(-8f, Pavement, -3.95f), new Vector3(-2.2f, Surface, -2.75f), new Vector3(-.7f, Surface, -2.1f),
            },
            crossingAtSegment = new[] { -1, -1, 1, 1, 1, -1, -1, -1 },
        },
        new CafeArrivals.Route
        {
            name = "From the courtyard shop (east street)", weight = 1f,
            points = new[]
            {
                new Vector3(19.2f, Surface, 1.77f), new Vector3(18.3f, Pavement, 1.77f), new Vector3(16.4f, Pavement, -2.3f),
                new Vector3(15.95f, Pavement, -3.85f), new Vector3(15.7f, Road, -3.85f),
                new Vector3(10.35f, Road, -3.85f), new Vector3(10.1f, Pavement, -3.85f),
                new Vector3(8.5f, Pavement, -3.95f), new Vector3(2.2f, Surface, -2.75f), new Vector3(.7f, Surface, -2.1f),
            },
            crossingAtSegment = new[] { -1, -1, -1, 2, 2, 2, -1, -1, -1 },
        },
    };

    static CafeArrivals.Crossing[] Crossings() => new[]
    {
        new CafeArrivals.Crossing { name = "Zebra outside the café", center = new Vector3(ZebraX, Road, (FrontRoadNorth + FrontRoadSouth) * .5f),
                                     halfSize = new Vector2((ZebraEast - ZebraWest) * .5f, 2.8f), crossingTrafficPhase = -1, keepClear = true },
        new CafeArrivals.Crossing { name = "West street by the Saffron house", center = new Vector3(-12.2f, Road, -3.85f),
                                     halfSize = new Vector2(2.8f, 1.1f), crossingTrafficPhase = 0, keepClear = false },
        new CafeArrivals.Crossing { name = "East street by the courtyard shops", center = new Vector3(12.9f, Road, -3.85f),
                                     halfSize = new Vector2(2.8f, 1.1f), crossingTrafficPhase = 0, keepClear = false },
    };

    // Each junction's box with its four crossings, as a plus: one arm for each direction of traffic.
    static CafeArrivals.KeepClearBox[] KeepClear()
    {
        var boxes = new List<CafeArrivals.KeepClearBox>();
        foreach (Vector2 j in Junctions)
        {
            boxes.Add(new CafeArrivals.KeepClearBox { name = $"Junction {j.x:0.#},{j.y:0.#} north-south", center = new Vector3(j.x, Road, j.y), halfSize = new Vector2(2.8f, 5.05f) });
            boxes.Add(new CafeArrivals.KeepClearBox { name = $"Junction {j.x:0.#},{j.y:0.#} east-west", center = new Vector3(j.x, Road, j.y), halfSize = new Vector2(5.05f, 2.8f) });
        }
        return boxes.ToArray();
    }

    static Vector4[] Offset(Vector4[] poses, float bodyOffset)
    {
        var result = new Vector4[poses.Length];
        for (int i = 0; i < poses.Length; i++)
        {
            Vector3 f = Heading(poses[i].w) * bodyOffset;
            result[i] = new Vector4(poses[i].x - f.x, poses[i].y, poses[i].z - f.z, poses[i].w);
        }
        return result;
    }

    // ------------------------------------------------------------------ dressing helpers

    static void Island(Transform parent, Vector3 centre, float radius)
    {
        var island = Child(parent, "Planted island");
        Cylinder(island, "Island edging", centre + Vector3.up * .075f, radius, .15f, M("Cream trim"));
        Cylinder(island, "Island soil", centre + Vector3.up * .09f, radius - .07f, .14f, M("Coffee brown"));
        Cylinder(island, "Island grass", centre + Vector3.up * .165f, radius - .09f, .02f, M("Garden grass"));
        var random = new System.Random(18);
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f + .4f;
            var shrub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(shrub, "Shrub");
            shrub.name = "Low sage shrub";
            shrub.transform.SetParent(island, false);
            shrub.transform.position = centre + new Vector3(Mathf.Cos(a) * (radius - .3f), .3f, Mathf.Sin(a) * (radius - .3f));
            float s = .32f + (float)random.NextDouble() * .12f;
            shrub.transform.localScale = new Vector3(s, s * .75f, s);
            shrub.GetComponent<MeshRenderer>().sharedMaterial = M("Garden sage");
            Object.DestroyImmediate(shrub.GetComponent<Collider>());
        }
    }

    // The café's own sign on two posts, facing the café and the front street, with
    // "CUSTOMER PARKING" on a green board under it (both faces).
    static void CafeSign(Transform parent, Vector3 at)
    {
        var sign = Child(parent, "Ace's Café customer parking sign");
        sign.position = at;
        var posts = M("Ironwork");
        foreach (float dx in new[] { -.62f, .62f })
            Cylinder(sign, "Sign post", at + new Vector3(dx, 1.1f, 0f), .045f, 2.2f, posts);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SignModel);
        if (model != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, sign);
            Undo.RegisterCreatedObjectUndo(go, "Café sign");
            go.name = "SignAcesCafe";
            go.transform.rotation = Quaternion.identity;
            Bounds b = BoundsOf(go);
            // Our Blender exports lie on their backs (Z-up): stand it up, and its face
            // then looks +z, at the café and the front street.
            if (CafeFurnishingCatalog.IsZUp(new Bounds(b.center - go.transform.position, b.size)))
            {
                go.transform.rotation = CafeFurnishingCatalog.Upright;
                b = BoundsOf(go);
            }
            else if (b.size.z > b.size.x) { go.transform.rotation = Quaternion.Euler(0f, 90f, 0f); b = BoundsOf(go); }
            float wide = b.size.x;
            if (wide > 1e-3f) go.transform.localScale *= 1.45f / wide;
            b = BoundsOf(go);
            go.transform.position += at + new Vector3(0f, 1.55f, 0f) - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
        Slab(sign, "Parking board", at + new Vector3(0f, 1.3f, 0f), new Vector3(1.32f, .36f, .05f), 0f, M("Sign green"));
        foreach (float side in new[] { -1f, 1f })
        {
            var go = new GameObject("Parking board text", typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, "Sign text");
            go.transform.SetParent(sign, false);
            go.transform.SetPositionAndRotation(at + new Vector3(0f, 1.3f, .031f * side), Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f));
            var text = go.GetComponent<TextMeshPro>();
            text.text = "CUSTOMER PARKING";
            text.fontSize = 1.15f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.97f, .93f, .82f);
            text.rectTransform.sizeDelta = new Vector2(1.22f, .3f);
            text.enableWordWrapping = false;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void CrossingSign(Transform parent, Vector3 at)
    {
        var sign = Child(parent, "Pedestrian crossing sign");
        Cylinder(sign, "Pole", at + new Vector3(0f, 1.15f, 0f), .04f, 2.3f, M("Ironwork"));
        foreach (float side in new[] { -1f, 1f })
        {
            var diamond = Slab(sign, "Crossing diamond", at + new Vector3(0f, 2.15f, .035f * side), new Vector3(.5f, .5f, .02f), 0f, M("Road paint"));
            diamond.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Slab(sign, "Crossing figure", at + new Vector3(0f, 2.15f, .05f * side), new Vector3(.07f, .26f, .01f), 0f, M("Ironwork"));
            Slab(sign, "Crossing figure stride", at + new Vector3(0f, 2.07f, .05f * side), new Vector3(.2f, .05f, .01f), 0f, M("Ironwork"));
        }
    }

    static void Arrow(Transform parent, Vector3 at, float yaw, Material material)
    {
        Vector3 f = Heading(yaw);
        Paint(parent, "Aisle arrow", at, new Vector2(.18f, 1.3f), yaw, material);
        Vector3 tip = at + f * .75f;
        foreach (float spread in new[] { -35f, 35f })
        {
            Vector3 d = Quaternion.Euler(0f, spread, 0f) * f;
            Paint(parent, "Aisle arrow head", tip - d * .3f, new Vector2(.16f, .6f), yaw + spread, material);
        }
    }

    // Bollards along the car park's sidewalk edge wherever nobody walks and no car goes.
    static int Bollards(Transform parent, List<Vector3[]> pedestrianPaths)
    {
        var parked = StallX.Select(x => Footprint(new Vector3(x, 0f, StallZ), StallYaw, CarLengthMax, CarWidthMax)).ToList();
        var placed = new List<float>();
        var posts = Child(parent, "Bollards");
        for (float x = LotWest + .35f; x <= LotEast - .3f; x += .1f)
        {
            Vector3 p = new Vector3(x, Surface, LotNorth - .15f);
            if (placed.Count > 0 && x - placed[placed.Count - 1] < 1.4f) continue;
            if (x > 6.2f) continue;                                               // the planted bed and the sign
            if (Mathf.Abs(x - LampPost.x) < .7f) continue;
            if (parked.Any(r => PointRectGap(p, r) < .45f)) continue;
            if (pedestrianPaths.Any(path => SegmentsDistance(path, p) < .75f)) continue;
            if (Vector2.Distance(new Vector2(p.x, p.z), WestSignalPost) < .9f) continue;
            Cylinder(posts, "Bollard", p + Vector3.up * .42f, .08f, .84f, M("Ironwork"));
            Cylinder(posts, "Bollard band", p + Vector3.up * .7f, .085f, .06f, M("Road paint"));
            placed.Add(x);
        }
        return placed.Count;
    }

    // ------------------------------------------------------------------ geometry helpers

    static void LoadPalette()
    {
        mats = new Dictionary<string, Material>();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>()) mats[m.name] = m;
        if (mats.Count == 0) throw new InvalidOperationException("The street palette (" + Palette + ") is missing.");
    }

    static Material M(string name) =>
        mats.TryGetValue(name, out var m) ? m : throw new InvalidOperationException("The street palette has no '" + name + "' material.");

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // A world-aligned box with world-space UVs (1 unit per metre, the way the street
    // grid's combined meshes are mapped), so textured materials continue seamlessly.
    static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material material)
    {
        var mesh = new Mesh { name = name + " " + newMeshes.Count };
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector3 a = min, b = max;
        void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            int i = v.Count;
            foreach (var p in new[] { p0, p1, p2, p3 })
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(p.x, p.z) : Mathf.Abs(normal.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
            }
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        Face(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), Vector3.up);
        Face(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), Vector3.back);
        Face(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), Vector3.forward);
        Face(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), Vector3.left);
        Face(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), Vector3.right);
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return MeshObject(parent, name, mesh, material);
    }

    // The crossover: a slope from the road's edge up to the car park, closed at both sides.
    static void Ramp(Transform parent, string name, float roadEdge, float lotEdge, Material material)
    {
        var mesh = new Mesh { name = name + " " + newMeshes.Count };
        float z0 = DrivewaySouth, z1 = DrivewayNorth, low = Road + .004f;
        bool east = roadEdge > lotEdge;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2, bool flat)
        {
            // Wind each triangle so it faces up (the slope) or outwards (the sides).
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            Vector3 want = flat ? Vector3.up : (p0.z <= z0 + 1e-3f ? Vector3.back : Vector3.forward);
            if (Vector3.Dot(normal, want) < 0f) { var s = p1; p1 = p2; p2 = s; }
            int i = v.Count;
            foreach (var p in new[] { p0, p1, p2 })
            {
                v.Add(p);
                uv.Add(flat ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
            }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        Vector3 r0 = new Vector3(roadEdge, low, z0), r1 = new Vector3(roadEdge, low, z1);
        Vector3 l0 = new Vector3(lotEdge, Surface, z0), l1 = new Vector3(lotEdge, Surface, z1);
        Tri(r0, l0, l1, true); Tri(r0, l1, r1, true);
        foreach (float z in new[] { z0, z1 })
            Tri(new Vector3(roadEdge, Road, z), new Vector3(lotEdge, Surface, z), new Vector3(lotEdge, Road, z), false);
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        MeshObject(parent, name, mesh, material);
        _ = east;
    }

    static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        newMeshes.Add(mesh);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Flat paint 6 mm proud of whatever it lies on.
    static GameObject Paint(Transform parent, string name, Vector3 centre, Vector2 size, float yaw, Material material) =>
        Slab(parent, name, centre + Vector3.up * .003f, new Vector3(size.x, .006f, size.y), yaw, material);

    static GameObject Slab(Transform parent, string name, Vector3 centre, Vector3 size, float yaw, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Cylinder(Transform parent, string name, Vector3 centre, float radius, float height, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = new Vector3(radius * 2f, height * .5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Decorative purchased prop: no physics, lights, particles or scripts; static.
    static GameObject TryPut(Transform parent, string path, Vector3 position, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Car park prop");
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; RecordPrefab(c); }
        foreach (var l in go.GetComponentsInChildren<Light>(true)) { l.enabled = false; RecordPrefab(l); }
        foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) { p.gameObject.SetActive(false); RecordPrefab(p.gameObject); }
        foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) { m.enabled = false; RecordPrefab(m); }
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            RecordPrefab(t.gameObject);
        }
        RecordPrefab(go.transform);
        return go;
    }

    static void RecordPrefab(Object o)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // The pack's alternative colour atlases recolour a whole car at once.
    static void Recolor(GameObject go, Material variant)
    {
        if (go == null || variant == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var list = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].name == "PolygonCity_01_A" && list[i] != variant) { list[i] = variant; changed = true; }
            if (!changed) continue;
            r.sharedMaterials = list;
            RecordPrefab(r);
        }
    }

    static Bounds LocalBounds(Transform root)
    {
        var inverse = root.worldToLocalMatrix;
        bool any = false;
        var b = new Bounds();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var m = inverse * filter.transform.localToWorldMatrix;
            foreach (var v in filter.sharedMesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // ------------------------------------------------------------------ making way

    static void Hide(GameObject go, RevertRecord record)
    {
        Undo.RecordObject(go, "Hide " + go.name);
        go.SetActive(false);
        RecordPrefab(go);
        record.hidden.Add(GlobalObjectId.GetGlobalObjectIdSlow(go).ToString());
    }

    delegate bool TriangleTest(Vector3 a, Vector3 b, Vector3 c);

    // Copies the filter's mesh without the triangles the test picks (world space),
    // remembering the original for Undo. Returns how many triangles went.
    static int CutTriangles(MeshFilter filter, RevertRecord record, TriangleTest drop)
    {
        Mesh source = filter.sharedMesh;
        if (source == null) return 0;
        var world = filter.transform.localToWorldMatrix;
        var worldVertices = source.vertices.Select(v => world.MultiplyPoint3x4(v)).ToArray();
        int removed = 0;
        var kept = new List<int>[source.subMeshCount];
        for (int s = 0; s < source.subMeshCount; s++)
        {
            var triangles = source.GetTriangles(s);
            kept[s] = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (drop(worldVertices[triangles[i]], worldVertices[triangles[i + 1]], worldVertices[triangles[i + 2]])) { removed++; continue; }
                kept[s].Add(triangles[i]); kept[s].Add(triangles[i + 1]); kept[s].Add(triangles[i + 2]);
            }
        }
        if (removed == 0) return 0;
        var copy = Object.Instantiate(source);
        copy.name = source.name.Replace(" (car park)", "") + " (car park)";
        for (int s = 0; s < source.subMeshCount; s++) copy.SetTriangles(kept[s], s);
        copy.RecalculateBounds();
        newMeshes.Add(copy);
        string filterId = GlobalObjectId.GetGlobalObjectIdSlow(filter).ToString();
        if (!record.meshFilters.Contains(filterId))
        {
            record.meshFilters.Add(filterId);
            record.originalMeshes.Add(GlobalObjectId.GetGlobalObjectIdSlow(source).ToString());
        }
        Undo.RecordObject(filter, "Cut " + filter.name);
        filter.sharedMesh = copy;
        RecordPrefab(filter);
        return removed;
    }

    static Vector3 Centroid(Vector3 a, Vector3 b, Vector3 c) => (a + b + c) / 3f;
    static bool Inside(Vector3 p, float x0, float x1, float z0, float z1) => p.x > x0 && p.x < x1 && p.z > z0 && p.z < z1;

    // True when all three vertices are corners of the box (a box built from a cube
    // primitive has nothing but its corners as vertices).
    static bool OnBoxCorners(Vector3 a, Vector3 b, Vector3 c, Vector3 min, Vector3 max)
    {
        bool Corner(Vector3 p) =>
            (Mathf.Abs(p.x - min.x) < .004f || Mathf.Abs(p.x - max.x) < .004f)
            && (Mathf.Abs(p.y - min.y) < .004f || Mathf.Abs(p.y - max.y) < .004f)
            && (Mathf.Abs(p.z - min.z) < .004f || Mathf.Abs(p.z - max.z) < .004f);
        return Corner(a) && Corner(b) && Corner(c);
    }

    // All new meshes go into one asset next to the other café art (not the purchased packs).
    static void SaveMeshes()
    {
        if (newMeshes.Count == 0) return;
        if (!AssetDatabase.IsValidFolder(MeshFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            AssetDatabase.CreateFolder("Assets/Art", "CafeParking");
        }
        // Keep an existing container (an unsaved scene may still use its meshes); add a new one.
        string path = AssetDatabase.LoadMainAssetAtPath(GeometryPath) == null ? GeometryPath
            : MeshFolder + "/Cafe parking geometry " + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".asset";
        AssetDatabase.CreateAsset(newMeshes[0], path);
        for (int i = 1; i < newMeshes.Count; i++) AssetDatabase.AddObjectToAsset(newMeshes[i], path);
        AssetDatabase.SaveAssets();
    }

    // ================================================================== check

    // Car paths against parked cars and anything standing near them; walks against
    // anything standing along them. Only the parts of things lower than 1.9 m count
    // (tree canopies and lamp heads hang over the cars).
    static string Check(bool standalone)
    {
        Transform cafe = FindRoot(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first.");
        Transform group = cafe.Find(GroupName);
        var arrivals = group != null ? group.GetComponent<CafeArrivals>() : null;
        if (arrivals == null) return standalone ? "Nothing to check: build the car park first." : "";
        var report = new StringBuilder("\nCHECK\n");
        int problems = 0;

        CafeCar anyCar = arrivals.Cars.FirstOrDefault(c => c != null);
        float length = anyCar != null ? anyCar.Length : CarLengthMax, width = anyCar != null ? anyCar.Width : CarWidthMax;
        float bodyOffset = anyCar != null ? LocalBounds(anyCar.transform).center.z : 0f;
        var obstacles = Obstacles(group);
        report.AppendLine($"  {obstacles.Count} standing things near the car park and the walks are tested; cars {length:0.00} x {width:0.00} m.");

        var stalls = arrivals.EditorStalls;
        var parked = stalls.Select(s => s.entry.Length > 0 ? CarRect(s.entry[s.entry.Length - 1], length, width, bodyOffset) : null).ToList();
        void CarPath(string label, IEnumerable<Vector4> poses, int ownStall)
        {
            float bestCar = float.PositiveInfinity, bestThing = float.PositiveInfinity;
            string carName = "", thingName = "";
            foreach (var pose in poses)
            {
                var rect = CarRect(pose, length, width, bodyOffset);
                for (int j = 0; j < parked.Count; j++)
                {
                    if (j == ownStall || parked[j] == null) continue;
                    float g = RectGap(rect, parked[j]);
                    if (g < bestCar) { bestCar = g; carName = stalls[j].name; }
                }
                foreach (var o in obstacles)
                {
                    if (o.top < pose.y + .15f) continue;                 // low enough to drive over (a kerb)
                    float g = RectGap(rect, o.footprint);
                    if (g < bestThing) { bestThing = g; thingName = o.name; }
                }
            }
            bool bad = bestCar < .15f || bestThing < .05f;
            if (bad) problems++;
            report.AppendLine($"  {(bad ? "PROBLEM" : "ok")}  {label}: nearest parked car {Fmt(bestCar)} ({carName}), nearest thing {Fmt(bestThing)} ({thingName})");
        }
        CarPath("Turning in from the east street", arrivals.EditorTurnIn, -1);
        for (int i = 0; i < stalls.Length; i++)
        {
            CarPath("Into " + stalls[i].name, stalls[i].entry, i);
            CarPath("Out of " + stalls[i].name + " to the exit", stalls[i].backOut.Concat(stalls[i].toExit), i);
        }
        CarPath("Turning out onto the west street", arrivals.EditorTurnOut, -1);

        // Walks: a person is 0.28 m round.
        void Walk(string label, Vector3[] points, bool pastParkedCars)
        {
            float best = float.PositiveInfinity; string what = "";
            for (int s = 1; s < points.Length; s++)
            {
                foreach (var o in obstacles)
                {
                    if (o.top < Mathf.Min(points[s].y, points[s - 1].y) + .12f) continue;
                    float g = SegmentRectDistance(points[s - 1], points[s], o.footprint) - .28f;
                    if (g < best) { best = g; what = o.name; }
                }
                if (!pastParkedCars) continue;
                foreach (var p in parked)
                {
                    if (p == null) continue;
                    float g = SegmentRectDistance(points[s - 1], points[s], p) - .28f;
                    if (g < best) { best = g; what = "a parked car"; }
                }
            }
            bool bad = best < 0f;
            if (bad) problems++;
            report.AppendLine($"  {(bad ? "PROBLEM" : "ok")}  walk {label}: tightest {Fmt(best)} ({what})");
        }
        // A walk from a neighbour's house starts in their front doorway, inside the door
        // trim's and the stoop railings' outline (those are combined meshes, tested as
        // boxes), so it is tested from the pavement on; photos 11 and 12 show the doorways.
        foreach (var route in arrivals.EditorFootRoutes) Walk(route.name, route.points.Skip(1).ToArray(), true);
        foreach (var stall in stalls)
        {
            // The first leg starts at the driver's door, beside their own car: test from the second point on.
            var rest = stall.walk.points.Skip(1).ToArray();
            Walk(stall.walk.name, rest, true);
        }
        Walk(arrivals.EditorLotToDoor.name, arrivals.EditorLotToDoor.points, true);

        // The lanes the cars use must pass where the paths join them.
        var life = Object.FindFirstObjectByType<StreetLife>();
        if (life != null)
        {
            Vector4 a = arrivals.EditorTurnIn.FirstOrDefault(), b = arrivals.EditorTurnOut.LastOrDefault();
            var inLane = life.actors.FirstOrDefault(x => x != null && x.trafficGroup == EntryLane);
            var outLane = life.actors.FirstOrDefault(x => x != null && x.trafficGroup == ExitLane);
            float inMiss = inLane != null ? LaneMiss(inLane, new Vector3(a.x, a.y, a.z)) : float.PositiveInfinity;
            float outMiss = outLane != null ? LaneMiss(outLane, new Vector3(b.x, b.y, b.z)) : float.PositiveInfinity;
            bool bad = inMiss > .3f || outMiss > .3f;
            if (bad) problems++;
            report.AppendLine($"  {(bad ? "PROBLEM" : "ok")}  the turn-in starts {inMiss:0.00} m off {EntryLane}; the turn-out ends {outMiss:0.00} m off {ExitLane}");
        }
        report.AppendLine($"  Stalls next to the exit (wait for it to be clear): {string.Join(", ", stalls.Where(s => s.nearExit).Select(s => s.name))}");
        report.AppendLine(problems == 0 ? "  RESULT: all paths clear." : $"  RESULT: {problems} problem(s) above.");
        return report.ToString();
    }

    // ================================================================== walking room

    // Walkers keep to their own line along a walk and step aside for each other (see
    // NpcJourney). This measures, for every segment of every walk, how far they may
    // stray to each side without touching anything: standing things (planters, posts,
    // railings, bins), a parked car in any stall, and the road (every traffic lane is
    // 2.8 m wide). Over a crossing the limit is its painted width instead.
    static string MeasureWalkingRoomMenu()
    {
        Transform cafe = FindRoot(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first.");
        Transform group = cafe.Find(GroupName);
        var arrivals = group != null ? group.GetComponent<CafeArrivals>() : null;
        if (arrivals == null) return "Build the car park first.";
        Undo.RecordObject(arrivals, "Measure walking room");
        string report = MeasureWalkingRoom(arrivals, group);
        EditorUtility.SetDirty(arrivals);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return report + "Not saved yet: Ctrl+S keeps it.";
    }

    const float WalkerRadius = .28f, WalkerMargin = .03f, RoomStep = .05f, RoomMax = 1f, LaneHalfWidth = 1.4f, StepHeight = .18f;

    static string MeasureWalkingRoom(CafeArrivals arrivals, Transform group)
    {
        var obstacles = Obstacles(group);
        CafeCar anyCar = arrivals.Cars.FirstOrDefault(c => c != null);
        float length = anyCar != null ? anyCar.Length : CarLengthMax, width = anyCar != null ? anyCar.Width : CarWidthMax;
        float bodyOffset = anyCar != null ? LocalBounds(anyCar.transform).center.z : 0f;
        var parked = arrivals.EditorStalls.Where(st => st.entry.Length > 0)
                                          .Select(st => CarRect(st.entry[st.entry.Length - 1], length, width, bodyOffset)).ToList();
        var lanes = LaneSegments();
        var crossings = arrivals.EditorCrossings;
        var tight = new List<string>();
        var report = new StringBuilder($"\nWALKING ROOM (metres to the left | right of each segment; {obstacles.Count} standing things, {parked.Count} parked cars, {lanes.Count} lane pieces)\n");
        void Measure(CafeArrivals.Route route, bool fromADoorway)
        {
            int n = Mathf.Max(0, route.points.Length - 1);
            route.roomLeft = new float[n];
            route.roomRight = new float[n];
            for (int i = 0; i < n; i++)
            {
                int c = i < route.crossingAtSegment.Length ? route.crossingAtSegment[i] : -1;
                var crossing = c >= 0 && c < crossings.Length ? crossings[c] : null;
                // Kerbs: over a crossing, its ends that meet the pavement; elsewhere, the ends
                // that meet a crossing (they sit on the road's edge by design).
                bool before = i > 0 && i - 1 < route.crossingAtSegment.Length && route.crossingAtSegment[i - 1] >= 0;
                bool after = i + 1 < route.crossingAtSegment.Length && route.crossingAtSegment[i + 1] >= 0;
                bool kerbA = crossing != null ? !before : before, kerbB = crossing != null ? !after : after;
                if (fromADoorway && i == 0)
                {
                    // Out of a neighbour's front door: straight out through the doorway, like the
                    // Check (door trims, railings and window glows are tested as outlines).
                    route.roomLeft[i] = route.roomRight[i] = 0f;
                    continue;
                }
                Band(route.points[i], route.points[i + 1], crossing, kerbA, kerbB, obstacles, parked, lanes,
                     out route.roomLeft[i], out route.roomRight[i], out string leftBy, out string rightBy);
                if (route.roomLeft[i] < .3f) tight.Add($"{route.name} #{i} left {route.roomLeft[i]:0.00}: {leftBy}");
                if (route.roomRight[i] < .3f) tight.Add($"{route.name} #{i} right {route.roomRight[i]:0.00}: {rightBy}");
            }
            report.AppendLine($"  {route.name}: " + string.Join("  ", Enumerable.Range(0, n).Select(i =>
                (route.crossingAtSegment.Length > i && route.crossingAtSegment[i] >= 0 ? "x" : "") + $"{route.roomLeft[i]:0.00}|{route.roomRight[i]:0.00}")));
        }
        foreach (var route in arrivals.EditorFootRoutes) Measure(route, true);
        foreach (var stall in arrivals.EditorStalls) Measure(stall.walk, false);
        Measure(arrivals.EditorLotToDoor, false);
        report.AppendLine("  (x = over a crossing, measured with the metre of pavement behind each kerb, where people wait. A negative room means the line itself\n" +
                          "   grazes something: walkers keep at least that far over to the other side.)");
        if (tight.Count > 0) report.AppendLine("  Tight spots (under 0.30 m) and what limits them:\n    " + string.Join("\n    ", tight));
        return report.ToString();
    }

    // The band of sideways shifts (metres, + = right) over which the segment stays clear,
    // as room to the left and to the right of the line. When the line itself grazes
    // something, the band is the clear stretch nearest to it, and one side's room is
    // negative (keep at least that far over). Nothing clear at all: 0 | 0.
    static void Band(Vector3 a, Vector3 b, CafeArrivals.Crossing crossing, bool kerbA, bool kerbB,
                     List<Obstacle> obstacles, List<Vector2[]> parked, List<(Vector3 a, Vector3 b)> lanes,
                     out float left, out float right, out string limitedLeft, out string limitedRight)
    {
        left = right = 0f;
        limitedLeft = limitedRight = "nothing within a metre";
        Vector3 d = b - a;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-6f) { limitedLeft = limitedRight = "no length"; return; }
        d.Normalize();
        Vector3 toRight = new Vector3(d.z, 0f, -d.x);
        float limit = RoomMax;
        if (crossing != null)
        {
            limit = Mathf.Min(limit, Mathf.Min(crossing.halfSize.x, crossing.halfSize.y) - WalkerRadius - .1f);
            limitedLeft = limitedRight = "the crossing's painted width";
            // People wait on the pavement just behind the kerb: that metre counts too.
            if (kerbA) a -= d * .8f;
            if (kerbB) b += d * .8f;
        }
        // The road only counts away from a kerb end (the last 0.7 m up to a kerb touches it).
        Vector3 ra = a + (kerbA && crossing == null ? d * .7f : Vector3.zero), rb = b - (kerbB && crossing == null ? d * .7f : Vector3.zero);
        bool testRoad = crossing == null && Vector3.Dot(rb - ra, d) > .05f;
        int steps = Mathf.Max(0, Mathf.FloorToInt(limit / RoomStep + 1e-4f));
        var blocker = new string[2 * steps + 1];
        for (int k = -steps; k <= steps; k++)
        {
            Vector3 shift = toRight * (k * RoomStep);
            blocker[k + steps] = Blocker(a + shift, b + shift, ra + shift, rb + shift, testRoad, obstacles, parked, lanes);
        }
        int start = -1;
        for (int reach = 0; reach <= steps && start < 0; reach++)
        {
            if (blocker[steps + reach] == null) start = steps + reach;
            else if (blocker[steps - reach] == null) start = steps - reach;
        }
        if (start < 0) { limitedLeft = limitedRight = "nothing clear: " + blocker[steps]; return; }
        int lo = start, hi = start;
        while (lo > 0 && blocker[lo - 1] == null) lo--;
        while (hi < 2 * steps && blocker[hi + 1] == null) hi++;
        left = -(lo - steps) * RoomStep;
        right = (hi - steps) * RoomStep;
        if (lo > 0) limitedLeft = blocker[lo - 1];
        if (hi < 2 * steps) limitedRight = blocker[hi + 1];
    }

    // What a walker (0.28 m round, 3 cm to spare) on the segment would touch, or null.
    static string Blocker(Vector3 pa, Vector3 pb, Vector3 qa, Vector3 qb, bool testRoad,
                          List<Obstacle> obstacles, List<Vector2[]> parked, List<(Vector3 a, Vector3 b)> lanes)
    {
        foreach (var o in obstacles)
        {
            if (o.top < Mathf.Min(pa.y, pb.y) + StepHeight) continue;       // a step, a threshold: walked over
            if (SegmentRectDistance(pa, pb, o.footprint) - WalkerRadius < WalkerMargin)
                return $"{o.name} (x {o.footprint.Min(q => q.x):0.00}..{o.footprint.Max(q => q.x):0.00}, z {o.footprint.Min(q => q.y):0.00}..{o.footprint.Max(q => q.y):0.00}, top {o.top:0.00})";
        }
        foreach (var car in parked)
            if (SegmentRectDistance(pa, pb, car) - WalkerRadius < WalkerMargin) return "a parked car";
        if (testRoad)
            foreach (var lane in lanes)
                if (SegmentSegmentDistance(qa, qb, lane.a, lane.b) - LaneHalfWidth - WalkerRadius < WalkerMargin) return "the road";
        return null;
    }

    // Every traffic lane's centre line, piece by piece (from the street's waypoints).
    static List<(Vector3 a, Vector3 b)> LaneSegments()
    {
        var result = new List<(Vector3, Vector3)>();
        var life = Object.FindAnyObjectByType<StreetLife>();
        if (life == null) return result;
        var seen = new HashSet<string>();
        foreach (var actor in life.actors)
        {
            if (actor == null || string.IsNullOrEmpty(actor.trafficGroup) || !seen.Add(actor.trafficGroup)) continue;
            var markers = actor.waypoints.Where(w => w != null).Select(w => w.position).ToArray();
            for (int i = 1; i < markers.Length; i++) result.Add((markers[i - 1], markers[i]));
            if (!actor.openRoute && markers.Length > 2) result.Add((markers[markers.Length - 1], markers[0]));
        }
        return result;
    }

    static float SegmentSegmentDistance(Vector3 a3, Vector3 b3, Vector3 c3, Vector3 d3)
    {
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), c = new Vector2(c3.x, c3.z), d = new Vector2(d3.x, d3.z);
        if (SegmentsIntersect(a, b, c, d)) return 0f;
        return Mathf.Min(Mathf.Min(PointSegment(a, c, d), PointSegment(b, c, d)), Mathf.Min(PointSegment(c, a, b), PointSegment(d, a, b)));
    }

    static float LaneMiss(StreetLife.Actor lane, Vector3 point)
    {
        float best = float.PositiveInfinity;
        var markers = lane.waypoints.Where(w => w != null).Select(w => w.position).ToArray();
        for (int i = 1; i < markers.Length; i++)
        {
            Vector3 a = markers[i - 1], e = markers[i] - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, e) / Mathf.Max(1e-6f, e.sqrMagnitude));
            Vector3 q = a + e * t;
            best = Mathf.Min(best, new Vector2(point.x - q.x, point.z - q.z).magnitude);
        }
        return best;
    }

    static string Fmt(float v) => float.IsInfinity(v) ? "none" : v.ToString("0.00", CultureInfo.InvariantCulture) + " m";

    struct Obstacle { public string name; public Vector2[] footprint; public float top; }

    // Everything standing (taller than a kerb) in the area of the car park and the walks,
    // except ground, roads, paint, people and the cars themselves. The footprint is that
    // of the object's vertices below 1.9 m, so a tree is its trunk and a lamp its post.
    static List<Obstacle> Obstacles(Transform group)
    {
        var list = new List<Obstacle>();
        var area = new Bounds(new Vector3(0f, 1f, -9f), new Vector3(42f, 4f, 30f));
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled) continue;
            Bounds b = r.bounds;
            if (!b.Intersects(area) || b.max.y < .12f || b.min.y > 1.9f) continue;
            if (b.size.x > 14f || b.size.z > 14f) continue;                              // ground, roads, whole façades
            if (b.center.x > -7.6f && b.center.x < 7.6f && b.center.z > -.1f) continue; // inside the café
            Transform t = r.transform;
            if (t.GetComponentInParent<CafeCar>(true) != null || t.GetComponentInParent<Animator>(true) != null) continue;
            if (t.GetComponentInParent<Camera>(true) != null || t.GetComponentInParent<CharacterController>(true) != null) continue;
            if (t.IsChildOf(group) && IsGroundPart(t.name)) continue;
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            // A combined mesh (a row's railings, all the dark joinery of a corner) is many
            // separate things: each connected piece is its own obstacle, outlined by the
            // convex hull of its vertices below 1.9 m - not one box round the lot, which
            // used to swallow doorways and whole pavements.
            foreach (var (footprint, top, piece) in MeshPieces(filter.sharedMesh, t.localToWorldMatrix))
                list.Add(new Obstacle { name = PathOf(t) + (piece > 0 ? " #" + piece : ""), top = top, footprint = footprint });
        }
        return list;
    }

    // The mesh's connected pieces (vertices welded by position), each as the convex hull
    // of its vertices below 1.9 m seen from above, with its top. Pieces lower than 12 cm
    // (paint, kerbs, slabs) are left out.
    static List<(Vector2[] footprint, float top, int piece)> MeshPieces(Mesh mesh, Matrix4x4 toWorld)
    {
        var result = new List<(Vector2[], float, int)>();
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int n = vertices.Length;
        if (n == 0) return result;
        var world = new Vector3[n];
        for (int i = 0; i < n; i++) world[i] = toWorld.MultiplyPoint3x4(vertices[i]);
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
        // Weld: split normals and seams give one corner several vertices.
        var byPosition = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < n; i++)
        {
            var key = (Mathf.RoundToInt(world[i].x * 500f), Mathf.RoundToInt(world[i].y * 500f), Mathf.RoundToInt(world[i].z * 500f));
            if (byPosition.TryGetValue(key, out int first)) Union(i, first); else byPosition[key] = i;
        }
        for (int k = 0; k + 2 < triangles.Length; k += 3) { Union(triangles[k], triangles[k + 1]); Union(triangles[k + 1], triangles[k + 2]); }
        var pieces = new Dictionary<int, List<Vector2>>();
        var tops = new Dictionary<int, float>();
        for (int i = 0; i < n; i++)
        {
            Vector3 p = world[i];
            if (p.y > 1.9f) continue;
            int root = Find(i);
            if (!pieces.TryGetValue(root, out var points)) { points = new List<Vector2>(); pieces[root] = points; tops[root] = float.NegativeInfinity; }
            points.Add(new Vector2(p.x, p.z));
            tops[root] = Mathf.Max(tops[root], p.y);
        }
        int index = 0;
        foreach (var pair in pieces)
        {
            if (tops[pair.Key] < .12f) continue;
            Vector2[] hull = ConvexHull(pair.Value);
            if (hull.Length < 3)
            {
                // A post seen end-on or a flat strip: a small box round it.
                Vector2 min = pair.Value[0], max = pair.Value[0];
                foreach (var q in pair.Value) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                min -= Vector2.one * .01f; max += Vector2.one * .01f;
                hull = new[] { min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y) };
            }
            result.Add((hull, tops[pair.Key], index++));
        }
        if (result.Count == 1) result[0] = (result[0].Item1, result[0].Item2, 0);
        return result;
    }

    // Andrew's monotone chain, counter-clockwise, no repeated points.
    static Vector2[] ConvexHull(List<Vector2> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        if (sorted.Count < 3) return sorted.ToArray();
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var hull = new List<Vector2>();
        foreach (var p in sorted)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        int lower = hull.Count + 1;
        for (int i = sorted.Count - 2; i >= 0; i--)
        {
            Vector2 p = sorted[i];
            while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }

    static bool IsGroundPart(string name) =>
        name.StartsWith("Stall line") || name.StartsWith("Asphalt") || name.StartsWith("Kerb") || name.StartsWith("Sidewalk slab")
        || name.StartsWith("Driveway") || name.StartsWith("Zebra") || name.StartsWith("Aisle") || name.StartsWith("Wheel stop")
        || name.StartsWith("Island edging") || name.StartsWith("Island soil") || name.StartsWith("Island grass");

    // ---- 2D rectangles (x, z) ----
    static Vector2[] Footprint(Vector3 centre, float yaw, float length, float width)
    {
        Vector3 f = Heading(yaw), l = new Vector3(-f.z, 0f, f.x);
        var result = new Vector2[4];
        int k = 0;
        foreach (var (a, b) in new[] { (1, 1), (1, -1), (-1, -1), (-1, 1) })
        {
            Vector3 p = centre + f * (a * length * .5f) + l * (b * width * .5f);
            result[k++] = new Vector2(p.x, p.z);
        }
        return result;
    }

    static Vector2[] CarRect(Vector4 pose, float length, float width, float bodyOffset)
    {
        Vector3 f = Heading(pose.w);
        return Footprint(new Vector3(pose.x, 0f, pose.z) + f * bodyOffset, pose.w, length, width);
    }

    static bool Overlap(Vector2[] a, Vector2[] b)
    {
        foreach (var poly in new[] { a, b })
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 e = poly[(i + 1) % poly.Length] - poly[i];
                Vector2 axis = new Vector2(-e.y, e.x);
                if (axis.sqrMagnitude < 1e-12f) continue;
                float minA = float.PositiveInfinity, maxA = float.NegativeInfinity, minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
                foreach (var p in a) { float d = Vector2.Dot(p, axis); minA = Mathf.Min(minA, d); maxA = Mathf.Max(maxA, d); }
                foreach (var p in b) { float d = Vector2.Dot(p, axis); minB = Mathf.Min(minB, d); maxB = Mathf.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return false;
            }
        return true;
    }

    static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a;
        float t = e.sqrMagnitude > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, e) / e.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + e * t);
    }

    static bool InsidePoly(Vector2 p, Vector2[] poly)
    {
        bool positive = false, negative = false;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (cross > 0f) positive = true; else if (cross < 0f) negative = true;
        }
        return !(positive && negative);
    }

    static float RectGap(Vector2[] a, Vector2[] b)
    {
        if (Overlap(a, b)) return 0f;
        float best = float.PositiveInfinity;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
            {
                best = Mathf.Min(best, PointSegment(a[i], b[j], b[(j + 1) % b.Length]));
                best = Mathf.Min(best, PointSegment(b[j], a[i], a[(i + 1) % a.Length]));
            }
        return best;
    }

    static float PointRectGap(Vector3 p, Vector2[] rect)
    {
        var q = new Vector2(p.x, p.z);
        if (InsidePoly(q, rect)) return 0f;
        float best = float.PositiveInfinity;
        for (int j = 0; j < rect.Length; j++) best = Mathf.Min(best, PointSegment(q, rect[j], rect[(j + 1) % rect.Length]));
        return best;
    }

    static float SegmentRectDistance(Vector3 a3, Vector3 b3, Vector2[] rect)
    {
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z);
        if (SegmentCrosses(a, b, rect)) return 0f;
        float best = Mathf.Min(PointRectGap(a3, rect), PointRectGap(b3, rect));
        for (int j = 0; j < rect.Length; j++) best = Mathf.Min(best, PointSegment(rect[j], a, b));
        return best;
    }

    static bool SegmentCrosses(Vector2 a, Vector2 b, Vector2[] rect)
    {
        if (InsidePoly(a, rect) || InsidePoly(b, rect)) return true;
        for (int j = 0; j < rect.Length; j++)
            if (SegmentsIntersect(a, b, rect[j], rect[(j + 1) % rect.Length])) return true;
        return false;
    }

    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        float d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }

    static float SegmentsDistance(Vector3[] path, Vector3 p)
    {
        float best = float.PositiveInfinity;
        var q = new Vector2(p.x, p.z);
        for (int i = 1; i < path.Length; i++)
            best = Mathf.Min(best, PointSegment(q, new Vector2(path[i - 1].x, path[i - 1].z), new Vector2(path[i].x, path[i].z)));
        return best;
    }

    // ================================================================== undo

    static string Revert()
    {
        Transform cafe = FindRoot(CafeRoot) ?? throw new InvalidOperationException("Open the café scene first.");
        Transform group = cafe.Find(GroupName);
        string json = EditorPrefs.GetString(RevertKey, "");
        var record = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RevertRecord>(json);
        if (group == null && record == null) return "Nothing to undo.";
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Undo the café car park");
        int shown = 0, restored = 0;
        if (record != null)
        {
            foreach (string id in record.hidden)
                if (GlobalObjectId.TryParse(id, out var gid) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is GameObject go)
                {
                    Undo.RecordObject(go, "Show " + go.name);
                    go.SetActive(true);
                    RecordPrefab(go);
                    shown++;
                }
            for (int i = 0; i < record.meshFilters.Count; i++)
                if (GlobalObjectId.TryParse(record.meshFilters[i], out var fid) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(fid) is MeshFilter filter
                    && GlobalObjectId.TryParse(record.originalMeshes[i], out var mid) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(mid) is Mesh mesh)
                {
                    Undo.RecordObject(filter, "Restore " + filter.name);
                    filter.sharedMesh = mesh;
                    RecordPrefab(filter);
                    restored++;
                }
        }
        if (group != null) Undo.DestroyObjectImmediate(group.gameObject);
        Undo.CollapseUndoOperations(undoGroup);
        EditorPrefs.DeleteKey(RevertKey);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return $"Car park removed: {shown} hidden things shown again, {restored} street meshes restored. Save the scene to keep that.";
    }

    // ================================================================== photos

    static string Photograph(string prefix)
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CarPark", prefix + "-" + stamp);
        Directory.CreateDirectory(folder);
        // For the photos only: park the pooled cars in the stalls, and one more half-way in.
        Transform cafe = FindRoot(CafeRoot);
        Transform group = cafe != null ? cafe.Find(GroupName) : null;
        var arrivals = group != null ? group.GetComponent<CafeArrivals>() : null;
        var shown = new List<(GameObject go, Vector3 p, Quaternion r)>();
        if (arrivals != null)
        {
            var stalls = arrivals.EditorStalls;
            int k = 0;
            foreach (var car in arrivals.Cars)
            {
                if (car == null) continue;
                Vector4 pose;
                if (k < stalls.Length) pose = stalls[k].entry[stalls[k].entry.Length - 1];
                else if (k == stalls.Length && arrivals.EditorTurnIn.Length > 0) pose = arrivals.EditorTurnIn[arrivals.EditorTurnIn.Length / 2];
                else if (k == stalls.Length + 1 && arrivals.EditorTurnOut.Length > 0) pose = arrivals.EditorTurnOut[0];
                else break;
                shown.Add((car.gameObject, car.transform.position, car.transform.rotation));
                car.transform.SetPositionAndRotation(new Vector3(pose.x, pose.y, pose.z), Quaternion.Euler(0f, pose.w, 0f));
                car.gameObject.SetActive(true);
                k++;
            }
        }
        try
        {
            Shot(Path.Combine(folder, "1-from-the-cafe-door.png"), new Vector3(0f, 1.65f, -1.6f), new Vector3(0f, .9f, -16f), 62f);
            Shot(Path.Combine(folder, "2-over-the-street.png"), new Vector3(-2f, 8.5f, -3.5f), new Vector3(.5f, 0f, -16f), 55f);
            Shot(Path.Combine(folder, "3-from-the-east-street.png"), new Vector3(14.2f, 5.5f, -9.5f), new Vector3(3f, 0f, -18f), 55f);
            Shot(Path.Combine(folder, "4-from-the-west.png"), new Vector3(-14.5f, 6f, -10f), new Vector3(-1f, 0f, -17f), 55f);
            Shot(Path.Combine(folder, "5-plan.png"), new Vector3(.3f, 34f, -9.1f), new Vector3(.3f, 0f, -9f), 50f);
            Shot(Path.Combine(folder, "6-zebra-and-door.png"), new Vector3(4.5f, 3.2f, -13.5f), new Vector3(0f, .6f, -3f), 55f);
            Shot(Path.Combine(folder, "7-east-crossover-low.png"), new Vector3(13.2f, 1.2f, -24.5f), new Vector3(9.4f, 0f, -18.5f), 50f);
            Shot(Path.Combine(folder, "8-west-crossover-low.png"), new Vector3(-12.5f, 1.2f, -24.5f), new Vector3(-8.8f, 0f, -18.5f), 50f);
            Shot(Path.Combine(folder, "9-sign-close.png"), new Vector3(7.2f, 1.7f, -11.8f), new Vector3(8.9f, 1.4f, -14.95f), 50f);
            // Where the walkers from the neighbours come out of their front doors, and
            // their last metres over the patio to the café door.
            Shot(Path.Combine(folder, "11-saffron-house-door.png"), new Vector3(-13.3f, 2.1f, -6.6f), new Vector3(-17.45f, .9f, -3.6f), 55f);
            Shot(Path.Combine(folder, "12-courtyard-shop-door.png"), new Vector3(15.2f, 2.1f, -1.9f), new Vector3(19.2f, .9f, 1.77f), 55f);
            Shot(Path.Combine(folder, "13-patio-approach.png"), new Vector3(6.2f, 2.6f, -6.4f), new Vector3(1.2f, .5f, -2.1f), 58f);
            GameObject game = GameObject.Find("CmShopCam");
            if (game != null) Shot(Path.Combine(folder, "10-game-camera.png"), game.transform.position, game.transform.position + game.transform.forward * 30f, 40f);
        }
        finally
        {
            foreach (var s in shown)
            {
                s.go.transform.SetPositionAndRotation(s.p, s.r);
                s.go.SetActive(false);
            }
        }
        return folder;
    }

    static void Shot(string path, Vector3 position, Vector3 target, float fov)
    {
        var g = new GameObject("Temporary car park camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = g.AddComponent<Camera>();
        cam.enabled = false;
        var rt = new RenderTexture(1440, 900, 24);
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            Vector3 look = target - position;
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look, Mathf.Abs(look.normalized.y) > .98f ? Vector3.forward : Vector3.up));
            cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 300f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(g);
        }
    }

    // ================================================================== play mode

    // A snapshot of who is where, for playtesting (Play mode only).
    static string LiveReport()
    {
        var arrivals = CafeArrivals.Instance;
        if (arrivals == null) return "No CafeArrivals running.";
        var report = new StringBuilder();
        var clock = DayClock.Instance;
        var life = StreetLife.Main;
        report.AppendLine($"Day {(clock != null ? clock.Day.ToString() : "?")}, open {(clock == null || clock.IsOpen)}, {(clock != null ? clock.CurrentHour.ToString("0.00") : "?")} h; t = {Time.time:0}s; signals {(life != null ? life.CurrentSignalState.ToString() : "-")}");
        report.AppendLine($"  Arrived today: {arrivals.ArrivedByCar} by car, {arrivals.ArrivedOnFoot} on foot; left: {arrivals.LeftByCar} by car, {arrivals.LeftOnFoot} on foot; turned back {arrivals.TurnedBack}; walks cut short {arrivals.WalksCutShort}");
        report.AppendLine($"  Walking in now: {arrivals.WalkingIn(CafeArrivals.Kind.Customer)} customers, {arrivals.WalkingIn(CafeArrivals.Kind.Patron)} patrons; walking out {arrivals.WalkingOut}");
        report.AppendLine($"  Cars: parked {arrivals.CarsParked}, left {arrivals.CarsLeft}, stalls taken {arrivals.StallsTaken}/{arrivals.StallCount}");
        foreach (var car in arrivals.Cars)
            if (car != null && car.State != CafeCar.Phase.Pooled)
                report.AppendLine($"    {car.name}: {car.State} for {car.PhaseAge:0.0}s, stall {car.Stall}, driver {(car.DriverWaiting ? "waiting in the car" : car.Owner != null ? car.Owner.name : "none")}, at {car.transform.position.x:0.0},{car.transform.position.z:0.0}");
        foreach (var walker in NpcJourney.Active)
        {
            if (walker == null) continue;
            string doing = walker.Waiting ? "waiting for " + walker.WaitingFor
                         : walker.CurrentCrossing != null ? "crossing " + walker.CurrentCrossing.Name
                         : walker.HeldBy.Length > 0 ? "held by " + walker.HeldBy
                         : "walking";
            report.AppendLine($"    {walker.name}: {(walker.Arriving ? "arriving" : "leaving")} {walker.Kind} at {walker.transform.position.x:0.0},{walker.transform.position.z:0.0}, {doing}; " +
                              $"point {walker.NextPoint}/{walker.PointCount}, kerbs {walker.WaitedAtKerbs:0.0}s, no progress {walker.StuckFor:0.0}s, helped on {walker.Unstuck}");
        }
        foreach (var crossing in arrivals.LiveCrossings)
            if (crossing != null)
                report.AppendLine($"    Crossing {crossing.Name}: {crossing.Waiting} waiting, {crossing.OnIt} on it, traffic stopped {crossing.Block.active}" +
                                  (crossing.Signalled && life != null ? $", walk time left {life.WalkTimeLeft(1 - crossing.CrossingTrafficPhase):0.0}s" : ""));
        var today = arrivals.Today;
        if (today.Count > 0)
        {
            report.AppendLine($"  Comings and goings today ({today.Count}; the last 12):");
            for (int i = Mathf.Max(0, today.Count - 12); i < today.Count; i++)
            {
                var c = today[i];
                report.AppendLine($"    {c.who} ({c.kind}): from {c.cameFrom}{(c.car.Length > 0 ? " in " + c.car : "")}" +
                                  $"{(c.arrivedHour >= 0f ? $", in at {Clock(c.arrivedHour)}" : ", on the way")}" +
                                  $"{(c.leftHour >= 0f ? $", left at {Clock(c.leftHour)} to {c.wentTo}" : "")}");
            }
        }
        return report.ToString();
    }

    static string Clock(float hour)
    {
        int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{(h % 12 == 0 ? 12 : h % 12)}:{m:00}{(h < 12 ? "am" : "pm")}";
    }

    // ================================================================== small helpers

    static Transform FindRoot(string name) =>
        SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == name);

    static Transform FindUnder(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static string PathOf(Transform t)
    {
        string result = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) result = p.name + "/" + result;
        return result;
    }
}
#endif
