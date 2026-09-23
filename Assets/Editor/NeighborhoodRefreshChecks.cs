#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Read-only guards for a visual refresh. Baselines live outside Assets and
// include gameplay settings and delivery/route anchors, not decorative meshes.
public static class NeighborhoodRefreshChecks
{
    const string ScenePath = "Assets/Playtests/AcesCafeLayoutPlaytest.unity";
    [Serializable] sealed class Entry { public string key, value; }
    [Serializable] sealed class Snapshot { public string scene; public List<Entry> entries = new List<Entry>(); }
    [Serializable] sealed class Pose { public Vector3 position, scale; public Quaternion rotation; public bool active; }

    [MenuItem("Fixit Fidget/Checks/Neighborhood refresh gameplay guards")]
    public static void Run() => Debug.Log(Validate());

    public static string Validate()
    {
        RequireStopped();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var allObjects = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Require(allObjects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0,
            "The cafe contains a missing script.");
        Require(!InScene<MonoBehaviour>().Any(c => c.GetType().Name.EndsWith("Probe", StringComparison.Ordinal)),
            "A temporary probe is attached to the cafe scene.");

        var seats = InScene<TableSeat>().Where(s => s.isActiveAndEnabled).ToArray();
        var spots = InScene<WaitingSpot>().Where(s => s.isActiveAndEnabled).ToArray();
        Require(seats.Length == 16, "Expected the existing 16 active table seats.");
        Require(spots.Any(s => s.Kind == WaitingSpot.SpotKind.Loiter), "No active loiter spots remain.");
        // Loiter spots have no separate stand point by design: WaitingSpot.StandPoint
        // falls back to the spot itself (six loiter spots, recorded that way in the
        // 23 Sept baseline). Seats must keep all three anchors.
        foreach (var spot in spots)
            Require(spot.DrainMultiplier > 0, spot.name + " has invalid patience drain.");
        foreach (var seat in seats)
        {
            var serialized = new SerializedObject(seat);
            Require(seat.Kind == WaitingSpot.SpotKind.Seat && Mathf.Approximately(seat.DrainMultiplier, .6f),
                seat.name + " lost its seat/patience settings.");
            Require(serialized.FindProperty("standPoint").objectReferenceValue != null
                && serialized.FindProperty("cupSpot").objectReferenceValue != null
                && serialized.FindProperty("seatPose").objectReferenceValue != null, seat.name + " lost a stand, cup or sitting anchor.");
        }

        var movement = Single<PlayerMovement>();
        var controller = movement.GetComponent<CharacterController>();
        Require(controller != null && controller.minMoveDistance == 0f, "The saved walking controller must keep Min Move Distance at zero.");
        var walking = new SerializedObject(movement);
        Require(walking.FindProperty("firstPersonAcceleration").floatValue > 0
            && walking.FindProperty("firstPersonBraking").floatValue > 0, "First-person easing settings are invalid.");
        var save = new SerializedObject(Single<SaveManager>());
        Require(save.FindProperty("useInteractionPlaytestSave").boolValue
            && save.FindProperty("interactionPlaytestSaveName").stringValue == "playtest-aces-cafe.json", "The cafe save path is still overridden for a test.");
        var log = new SerializedObject(Single<DayLog>());
        Require(log.FindProperty("folderName").stringValue.Replace('\\', '/') == "DayLogs/AcesCafeLayout", "The cafe day-log path is still overridden for a test.");

        var streets = InScene<StreetLife>();
        var signals = streets.SelectMany(s => s.signalHeads).ToArray();
        Require(signals.Length == 16 && signals.All(s => s != null), "Expected the existing 16 traffic signal heads.");
        var lenses = signals.SelectMany(s => new[] { s.red, s.amber, s.green }).ToArray();
        Require(lenses.All(r => r != null && r.enabled && r.gameObject.activeInHierarchy)
            && lenses.Distinct().Count() == 48, "All 48 traffic lenses must retain unique, active renderer references.");
        Require(signals.All(s => s.signalGroup == 0 || s.signalGroup == 1)
            && signals.Select(s => s.signalGroup).Distinct().Count() == 2, "Both junction signal phases must remain connected.");
        foreach (var street in streets)
            foreach (var actor in street.actors)
                Require(actor != null && actor.actor != null && actor.waypoints != null
                    && actor.waypoints.Length >= 2 && actor.waypoints.All(t => t != null), "A street actor lost its body or route.");

        // Both existing checks query the current NavMesh/colliders; neither bakes,
        // moves the player nor changes a saved scene or checkpoint.
        string paths = AcesCafeLayoutSetup.ValidateLayout();
        string circulation = AcesCafeLayoutSetup.ValidateOccupiedCirculationV2();
        Require(paths.Contains("PASS"), paths);
        Require(circulation.Contains("PASS"), circulation);
        return "Neighborhood refresh: PASS — 16 seats, " + spots.Length
            + " waiting spots, original save/log paths, no probes or missing scripts, preserved walking settings, 16 signal heads/48 lenses.\n"
            + paths + "\n" + circulation;
    }

    public static string CaptureBaseline(string path)
    {
        RequireStopped();
        path = ExternalPath(path);
        Require(!File.Exists(path), "Baseline already exists; choose a new filename to avoid replacing preservation evidence.");
        var snapshot = Capture();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
        return "Captured " + snapshot.entries.Count + " gameplay records at " + path;
    }

    public static string CompareBaseline(string path)
    {
        RequireStopped();
        var before = JsonUtility.FromJson<Snapshot>(File.ReadAllText(ExternalPath(path)));
        var after = Capture();
        Require(before != null && before.scene == after.scene, "The baseline is for a different scene.");
        var old = before.entries.ToDictionary(e => e.key, e => e.value);
        var current = after.entries.ToDictionary(e => e.key, e => e.value);
        var changes = old.Keys.Union(current.Keys).OrderBy(k => k, StringComparer.Ordinal)
            .Where(k => !old.ContainsKey(k) || !current.ContainsKey(k) || old[k] != current[k]).ToArray();
        Require(changes.Length == 0, "Gameplay preservation differs from baseline:\n" + string.Join("\n", changes));
        return "Gameplay preservation: PASS — " + current.Count + " settings and anchor records unchanged.";
    }

    static Snapshot Capture()
    {
        var result = new Snapshot { scene = SceneManager.GetActiveScene().path };
        foreach (var component in InScene<MonoBehaviour>().Where(c => c is WaitingSpot || c is StreetLife
            || c is PlayerMovement || c is SaveManager || c is DayLog))
            Add(component);
        foreach (var spot in InScene<WaitingSpot>())
        {
            Anchor(spot.transform); Anchor(spot.StandPoint);
            if (spot is TableSeat seat) { Anchor(seat.CupSpot); Anchor(seat.SeatPose); }
        }
        foreach (var street in InScene<StreetLife>())
            foreach (var actor in street.actors)
                if (actor != null)
                {
                    foreach (var marker in actor.waypoints ?? Array.Empty<Transform>()) Anchor(marker);
                    foreach (var marker in actor.stopWaypoints ?? Array.Empty<Transform>()) Anchor(marker);
                }
        foreach (string path in new[] { "Assets/AssetsPrefabs/Customer.prefab", "Assets/AssetsPrefabs/Patron.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Missing NPC prefab: " + path);
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is CustomerBrain || component is PatronBrain || component is CustomerIdentity || component is CustomerInteractable)
                    Add(component);
        }
        result.entries = result.entries.GroupBy(e => e.key).Select(g => g.First()).OrderBy(e => e.key, StringComparer.Ordinal).ToList();
        return result;

        void Add(Component component)
        {
            result.entries.Add(new Entry { key = "component:" + Key(component), value = Describe(component) });
        }
        void Anchor(Transform anchor)
        {
            if (anchor == null) return;
            result.entries.Add(new Entry { key = "anchor:" + Key(anchor), value = JsonUtility.ToJson(new Pose
                { position = anchor.position, rotation = anchor.rotation, scale = anchor.lossyScale, active = anchor.gameObject.activeInHierarchy }) });
        }
    }

    // Every serialized value of a component, one "path=value" line each.
    // References are written as Unity's stable GlobalObjectIds: instance IDs
    // change between editor sessions, and Unity 6.5 no longer compiles the old
    // instance-ID lookup (EditorUtility.InstanceIDToObject), which is what
    // stopped the whole Editor assembly from compiling on 23 Sept.
    static string Describe(Object target)
    {
        var text = new StringBuilder();
        using (var serialized = new SerializedObject(target))
        {
            var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                bool leaf = IsLeaf(property);
                enterChildren = !leaf;
                if (leaf) text.Append(property.propertyPath).Append('=').Append(Value(property)).Append('\n');
                else if (property.isArray) text.Append(property.propertyPath).Append(".count=").Append(property.arraySize).Append('\n');
            }
        }
        return text.ToString();
    }

    static bool IsLeaf(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.String:
            case SerializedPropertyType.ObjectReference:
            case SerializedPropertyType.ManagedReference:
                return true;
            default:
                return !property.hasChildren;
        }
    }

    static string Value(SerializedProperty property)
    {
        var invariant = CultureInfo.InvariantCulture;
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == null ? "null" : Key(property.objectReferenceValue);
            case SerializedPropertyType.ManagedReference: return property.managedReferenceFullTypename;
            case SerializedPropertyType.String: return property.stringValue;
            case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float: return property.doubleValue.ToString("R", invariant);
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.ArraySize: return property.longValue.ToString(invariant);
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Character: return property.intValue.ToString(invariant);
            default:
                try { return Convert.ToString(property.boxedValue, invariant); }
                catch (Exception) { return "(" + property.propertyType + ")"; }
        }
    }

    static string Key(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
    static T[] InScene<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).Where(c => c != null).ToArray();
    static T Single<T>() where T : Component
    {
        var values = InScene<T>();
        Require(values.Length == 1, "Expected one " + typeof(T).Name + ", found " + values.Length + ".");
        return values[0];
    }
    static void RequireStopped()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().path == ScenePath,
            "Open the stopped Ace's Cafe scene before checking the neighborhood.");
    }
    static string ExternalPath(string path)
    {
        string full = Path.GetFullPath(path);
        string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase), "Write validation baselines outside Assets.");
        return full;
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Neighborhood refresh] " + message);
    }
}
#endif
