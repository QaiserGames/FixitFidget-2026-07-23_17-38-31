#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// Night step 3, homes for regulars (claude/night-homes-spec.md), in the scene
// and the data. Decided 27 Sept 2026: Grace lives in the saffron bay-window
// house across the west street (house 1); her address is a placeholder,
// 12 West Street; street names live in one asset that can be edited any time.
//
// "Give Grace her home" (idempotent; Edit > Undo works):
//   * District streets (Assets/Data/Resources) with placeholder names, if it
//     isn't there yet. An existing one is never overwritten.
//   * Grace's profile gets Home Id home.grace.
//   * A HomeDoor on the saffron house's doorstep (12, west street).
//   * The walking route that already starts at that door becomes hers alone.
//     The walk-ins' neighbour's door moves next door, to the dusty rose house:
//     a new route down the pavement that joins hers at the kerb, so it uses the
//     same crossing. Its walking room is measured (Cafe parking lot > 3).
//   Save the scene afterwards (Ctrl+S).
//
// "Check homes" (read-only): every home has one door and one route, no
// walk-in route starts at a home, the parking-lot check still passes, and
// whether each door can be seen from the usual views. Report and photos in
// Logs/Night/homes-check-<time>/.
// ---------------------------------------------------------------------------
public static class HomeSetup
{
    const string Menu = "Fixit Fidget/Night/";
    const string Tag = "[Homes] ";
    const string StreetsFolder = "Assets/Data/Resources";
    const string StreetsPath = StreetsFolder + "/" + DistrictStreets.ResourceName + ".asset";
    const string RegularsFolder = "Assets/Data/Regulars";
    const string GracePath = RegularsFolder + "/Regular_Grace.asset";
    const string GraceHome = "home.grace";
    const string GraceHouse = "1 - Saffron bay-window house";
    const string NeighbourHouse = "2 - Dusty rose bay-window house";
    const string DoorName = "Grace's front door (home.grace)";
    public const string GraceRouteName = "From the saffron house (Grace's home, west street)";
    public const string NeighbourRouteName = "From the dusty rose house (west street)";
    const float Pavement = -.02f, SidewalkLine = -15.7f;

    // A bay-window house's front door, in the house's own space (CafeStreetUpgrade.House):
    // the doorway people step out of, the door's face, and the pavement just past the stoop.
    static readonly Vector3 Doorway = new(1.6f, .15f, .10f), DoorFace = new(1.6f, .15f, .29f), PastTheStoop = new(1.6f, 0f, 1.10f);

    // Street names show in edit mode too (inspectors, checks), not only in Play.
    [InitializeOnLoadMethod]
    static void LoadStreetNames() => EditorApplication.delayCall += () => DistrictStreets.LoadFromResources();

    // ================================================================== give Grace her home

    [MenuItem(Menu + "Give Grace her home (the saffron house, 12 West Street)")]
    static void GiveGraceHerHome()
    {
        try { Debug.Log(Tag + string.Join("\n", Apply()) + "\nSave the scene (Ctrl+S)."); }
        catch (Exception e) { Debug.LogError(Tag + "FAILED (nothing after the failure was changed): " + e.Message + "\n" + e); }
    }

    [MenuItem(Menu + "Give Grace her home (the saffron house, 12 West Street)", true)]
    [MenuItem(Menu + "Check homes (read-only)", true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static List<string> Apply()
    {
        RequireScene();
        var log = new List<string>();

        // 1. The street names.
        DistrictStreets streets = AssetDatabase.LoadAssetAtPath<DistrictStreets>(StreetsPath);
        if (streets == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(StreetsFolder)) AssetDatabase.CreateFolder("Assets/Data", "Resources");
            streets = ScriptableObject.CreateInstance<DistrictStreets>();
            AssetDatabase.CreateAsset(streets, StreetsPath);
            AssetDatabase.SaveAssets();
            log.Add($"Created {StreetsPath} with placeholder names: {string.Join(", ", streets.streets.Select(s => s.id + " = " + s.name))}.");
        }
        else log.Add($"Kept {StreetsPath} as it is ({string.Join(", ", streets.streets.Select(s => s.id + " = " + s.name))}).");
        streets.Apply();

        // 2. Grace's profile.
        CustomerProfile grace = AssetDatabase.LoadAssetAtPath<CustomerProfile>(GracePath)
                                ?? throw new InvalidOperationException("Grace's profile is missing: " + GracePath);
        var profile = new SerializedObject(grace);
        SerializedProperty homeId = profile.FindProperty("homeId")
                                    ?? throw new InvalidOperationException("CustomerProfile has no homeId field (recompile first).");
        if (homeId.stringValue != GraceHome)
        {
            homeId.stringValue = GraceHome;
            profile.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            log.Add("Grace's profile: Home Id = " + GraceHome + ".");
        }
        else log.Add("Grace's profile already has Home Id " + GraceHome + ".");

        // 3. Her front door.
        Transform saffron = Find(GraceHouse), rose = Find(NeighbourHouse);
        HomeDoor door = SceneObjects<HomeDoor>().FirstOrDefault(d => d.homeId == GraceHome);
        if (door == null)
        {
            var go = new GameObject(DoorName);
            Undo.RegisterCreatedObjectUndo(go, "Give Grace her home");
            go.transform.SetParent(saffron, false);
            door = go.AddComponent<HomeDoor>();
            log.Add("Added " + DoorName + " on the saffron house's doorstep.");
        }
        else log.Add("Kept " + door.name + ".");
        Undo.RecordObject(door.transform, "Give Grace her home");
        door.transform.SetPositionAndRotation(saffron.TransformPoint(DoorFace), Quaternion.LookRotation(Flat(saffron.forward), Vector3.up));
        Undo.RecordObject(door, "Give Grace her home");
        door.homeId = GraceHome;
        door.looks = "the saffron house";
        door.houseNumber = "12";
        door.streetId = "west";
        log.Add($"  at {door.transform.position:F2}, facing {Flat(door.transform.forward):F0}; address {door.Address}.");

        // 4. The walking routes.
        CafeArrivals arrivals = SceneObjects<CafeArrivals>().FirstOrDefault()
                                ?? throw new InvalidOperationException("No CafeArrivals in the scene (Cafe parking lot > 1 - Build).");
        Undo.RecordObject(arrivals, "Give Grace her home");
        var routes = arrivals.EditorFootRoutes.Where(r => r != null).ToList();
        Vector3 graceDoorway = saffron.TransformPoint(Doorway);
        CafeArrivals.Route hers = routes.FirstOrDefault(r => r.homeId == GraceHome)
                                  ?? routes.FirstOrDefault(r => r.points.Length > 1 && Flat(r.points[0] - graceDoorway).magnitude < .3f)
                                  ?? throw new InvalidOperationException("No walking route starts at the saffron house's door " + graceDoorway.ToString("F2"));
        hers.homeId = GraceHome;
        hers.name = GraceRouteName;
        log.Add($"Route \"{hers.name}\" ({hers.points.Length} points) is Grace's alone now.");
        CafeArrivals.Route theirs = NeighbourRoute(rose, hers);
        int existing = routes.FindIndex(r => r.name == NeighbourRouteName);
        if (existing < 0)
        {
            routes.Add(theirs);
            log.Add($"Added \"{NeighbourRouteName}\" for walk-ins: down the pavement from the dusty rose house's door, joining Grace's route at the kerb.");
        }
        else
        {
            routes[existing] = theirs;
            log.Add($"Updated \"{NeighbourRouteName}\" to the current path.");
        }
        arrivals.EditorSetFootRoutes(routes.ToArray());
        EditorUtility.SetDirty(arrivals);

        // 5. Walking room for every route (the parking lot's own measure).
        log.Add(CallParkingLot("MeasureWalkingRoomMenu").Trim());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return log;
    }

    // Out of the dusty rose house's door, down the west pavement past the saffron
    // house's stoop, and onto Grace's route where it reaches the kerb: the same
    // crossing and the same last metres to the café door.
    static CafeArrivals.Route NeighbourRoute(Transform house, CafeArrivals.Route hers)
    {
        const int kerb = 2; // hers: doorway, past the stoop, the kerb, across…
        if (hers.points.Length <= kerb + 1) throw new InvalidOperationException("Grace's route is too short to join.");
        Vector3 doorway = house.TransformPoint(Doorway);
        Vector3 pastStoop = house.TransformPoint(PastTheStoop);
        pastStoop.y = Pavement;
        Vector3 join = hers.points[kerb];
        // Straight out past the stoop's railings, then along the pavement: turning
        // any earlier brushes the railing's end.
        var points = new List<Vector3>
        {
            doorway,
            pastStoop,
            new(SidewalkLine, Pavement, pastStoop.z),
            new(SidewalkLine, Pavement, join.z + .95f),
        };
        points.AddRange(hers.points.Skip(kerb));
        var crossings = new List<int> { -1, -1, -1, -1 };
        crossings.AddRange(hers.crossingAtSegment.Skip(kerb));
        return new CafeArrivals.Route { name = NeighbourRouteName, points = points.ToArray(), crossingAtSegment = crossings.ToArray(), weight = 1f, homeId = "" };
    }

    // ================================================================== check

    [MenuItem(Menu + "Check homes (read-only)")]
    static void CheckMenu()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "homes-check-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        try
        {
            Directory.CreateDirectory(folder);
            (string report, int problems) = Check(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report);
            if (problems > 0) Debug.LogError(Tag + $"Check: {problems} problem(s). {folder}\n{report}");
            else Debug.Log(Tag + $"Check: all clear. {folder}\n{report}");
        }
        catch (Exception e) { Debug.LogError(Tag + "Check FAILED: " + e.Message + "\n" + e); }
    }

    static (string, int) Check(string folder)
    {
        RequireScene();
        var report = new StringBuilder();
        int problems = 0;
        void Line(bool ok, string what) { if (!ok) problems++; report.AppendLine((ok ? "ok       " : "PROBLEM  ") + what); }

        // Street names.
        DistrictStreets streets = AssetDatabase.LoadAssetAtPath<DistrictStreets>(StreetsPath);
        Line(streets != null && DistrictStreets.LoadFromResources(), $"District streets is at {StreetsPath} and loads from Resources");
        if (streets != null)
            report.AppendLine("         " + string.Join("; ", streets.streets.Select(s => $"{s.id} = \"{s.name}\"")));

        // Homes: every profile's home has exactly one door, on a known street.
        HomeDoor[] doors = SceneObjects<HomeDoor>();
        var profiles = AssetDatabase.FindAssets("t:CustomerProfile", new[] { RegularsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToArray();
        foreach (CustomerProfile p in profiles.Where(p => p.HomeId.Length > 0))
        {
            HomeDoor[] mine = doors.Where(d => d.homeId == p.HomeId).ToArray();
            Line(mine.Length == 1, $"{p.characterName}'s home {p.HomeId} has one front door in the scene ({mine.Length})");
        }
        Line(profiles.Any(p => p.name == "Regular_Grace" && p.HomeId == GraceHome), "Grace's profile has Home Id " + GraceHome);
        foreach (HomeDoor d in doors)
        {
            bool known = streets != null && !string.IsNullOrWhiteSpace(streets.NameOf(d.streetId));
            Line(known, $"{d.name}: {d.looks}, {d.Address} (street id \"{d.streetId}\" {(known ? "is" : "is NOT")} in District streets), at {d.transform.position:F2}");
        }

        // Routes.
        CafeArrivals arrivals = SceneObjects<CafeArrivals>().FirstOrDefault();
        Line(arrivals != null, "The scene has CafeArrivals");
        if (arrivals != null)
        {
            CafeArrivals.Route[] routes = arrivals.EditorFootRoutes.Where(r => r != null).ToArray();
            foreach (HomeDoor d in doors)
            {
                CafeArrivals.Route[] theirs = routes.Where(r => r.homeId == d.homeId).ToArray();
                Line(theirs.Length == 1, $"{d.homeId} has one walking route ({theirs.Length})");
                foreach (CafeArrivals.Route r in theirs)
                {
                    float gap = r.points.Length > 0 ? Flat(r.points[0] - d.DoorPoint).magnitude : float.PositiveInfinity;
                    Line(gap < 1f, $"  \"{r.name}\" starts in that doorway ({gap:0.00} m from the door)");
                }
            }
            foreach (CafeArrivals.Route r in routes.Where(r => string.IsNullOrWhiteSpace(r.homeId)))
            {
                HomeDoor near = doors.FirstOrDefault(d => r.points.Length > 0 && Flat(r.points[0] - d.DoorPoint).magnitude < 2f);
                Line(near == null, $"Walk-in route \"{r.name}\" starts at nobody's home{(near != null ? " (it starts at " + near.homeId + ")" : "")}");
            }
            Line(routes.Any(r => string.IsNullOrWhiteSpace(r.homeId) && r.points.Length > 1), "Walk-ins still have a neighbour's door to come from");
            foreach (CafeArrivals.Route r in routes)
            {
                int segments = Math.Max(0, r.points.Length - 1);
                Line(r.roomLeft.Length == segments && r.roomRight.Length == segments && r.crossingAtSegment.Length == segments,
                    $"\"{r.name}\": {r.points.Length} points, walking room measured for all {segments} segments");
            }
            // The parking lot's own clearance check, walks included.
            string parking = CallParkingLot("Check", true);
            bool clear = parking.Contains("RESULT: all paths clear.");
            Line(clear, "Cafe parking lot > 2 - Check paths and clearances: " + (clear ? "all paths clear" : "see below"));
            report.AppendLine(string.Join("\n", parking.Split('\n').Where(l => l.Contains("walk ") || l.Contains("RESULT") || l.Contains("PROBLEM"))));
        }

        // Can the doors be seen? (Information for playtesting, not a pass/fail.)
        CafeViewMode view = SceneObjects<CafeViewMode>().FirstOrDefault();
        if (view != null && view.isometricCamera != null)
        {
            float fov = view.isometricCamera.Lens.FieldOfView;
            Vector3 home = view.isometricCamera.transform.eulerAngles;
            float distance = Vector3.Distance(view.isometricCamera.transform.position, view.isometricFocus);
            foreach (HomeDoor d in doors)
            {
                var seen = new List<string>();
                Vector3 target = d.DoorPoint + Vector3.up * 1.2f;
                foreach ((float yaw, float dist, string label) in new[] { (home.y, distance, "the usual overhead view"), (home.y, 48f, "zoomed right out"),
                                                                           (home.y - 25f, distance, "turned 25° left"), (home.y + 25f, distance, "turned 25° right") })
                {
                    Quaternion angle = Quaternion.Euler(Mathf.DeltaAngle(0, home.x), yaw, 0);
                    seen.Add(label + ": " + Sight(view.isometricFocus - angle * Vector3.forward * dist, angle, fov, target));
                }
                report.AppendLine($"info     {d.homeId}'s doorstep from overhead: " + string.Join("; ", seen));
                Vector3 sofa = new(-5.6f, 1.65f, 6f);
                report.AppendLine($"info     …and from the café's sofas in first person, looking out of the west windows: " +
                                  Sight(sofa, Quaternion.LookRotation(target - sofa), 60f, target));
                CafeSecondPassSteps.Capture(Path.Combine(folder, d.homeId + "-1-front.png"), d.DoorPoint + Flat(d.transform.forward) * 7f + Vector3.up * 1.6f,
                    d.DoorPoint + Vector3.up * 1.2f, 60f, false);
                CafeSecondPassSteps.Capture(Path.Combine(folder, d.homeId + "-2-from-the-cafe-sofas.png"), sofa + Vector3.up * .45f, d.DoorPoint + Vector3.up * 1.2f, 60f, false);
            }
            CafeSecondPassSteps.Capture(Path.Combine(folder, "3-west-street-from-above.png"), new Vector3(-9f, 18f, -1f), new Vector3(-16f, 0f, 0.5f), 55f, false);
        }
        report.Insert(0, (problems == 0 ? "Homes: all clear" : $"Homes: {problems} problem(s)") + "\n\n");
        return (report.ToString(), problems);
    }

    // The game's own test (HomeSightings) from a camera placed here, 16:9:
    // "seen", "out of the frame", or "hidden behind <what>".
    static string Sight(Vector3 position, Quaternion rotation, float fov, Vector3 point)
    {
        var go = new GameObject("Temporary home view") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.aspect = 16f / 9f;
            go.transform.SetPositionAndRotation(position, rotation);
            if (!HomeSightings.InFrame(cam, point)) return "out of the frame";
            Collider blocker = HomeSightings.Blocker(cam, point, null);
            if (blocker == null) return "seen";
            Transform t = blocker.transform;
            return "hidden behind " + (t.root != t ? $"{t.name} ({t.root.name})" : t.name);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ================================================================== helpers

    // The parking lot's own measure and check (private there), so this step can't drift from them.
    static string CallParkingLot(string method, params object[] args)
    {
        MethodInfo m = typeof(CafeParkingLot).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                       ?? throw new InvalidOperationException("CafeParkingLot." + method + " is missing.");
        return m.Invoke(null, args) as string ?? "";
    }

    static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
    }

    static T[] SceneObjects<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    static Transform Find(string name) => SceneObjects<Transform>().FirstOrDefault(t => t.name == name)
                                          ?? throw new InvalidOperationException(name + " is missing from the scene.");

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
#endif
