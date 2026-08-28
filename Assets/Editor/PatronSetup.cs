#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// PATRON SETUP
//
// Builds the patron prefab from your existing customer prefab and wires a
// spawner into the scene. Doing this by hand means duplicating a prefab,
// deleting five components, remembering which five, and re-wiring three scene
// references — the exact shape of job that goes subtly wrong and then costs an
// hour to find.
//
// Everything here is scene-level and undoable except the prefab, which is
// written to disk. Ctrl+Z takes back the scene half.
// ---------------------------------------------------------------------------

public static class PatronSetup
{
    private const string PrefabFolder = "Assets/AssetsPrefabs";
    private const string PrefabPath = PrefabFolder + "/Patron.prefab";

    [MenuItem("Fixit Fidget/Content/9 · Set up patrons")]
    public static void Run()
    {
        CustomerSpawner customerSpawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (customerSpawner == null)
        {
            EditorUtility.DisplayDialog("Patron setup",
                "No CustomerSpawner in the open scene. Make sure SampleScene is open.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Set up patrons",
            "This will:\n\n" +
            "1. Build Assets/AssetsPrefabs/Patron.prefab from your customer prefab\n" +
            "2. Add a PatronSpawner to the scene, next to SpawnPoint\n" +
            "3. Wire its spawn and exit points to match the customer spawner\n\n" +
            "The prefab is written to disk. The scene half is undoable.",
            "Set it up", "Cancel")) return;

        List<string> log = new();

        GameObject patronPrefab = BuildPrefab(customerSpawner, log);
        if (patronPrefab == null)
        {
            EditorUtility.DisplayDialog("Patron setup", string.Join("\n", log), "OK");
            return;
        }

        WireSpawner(customerSpawner, patronPrefab, log);

        int seats = CountSeats();
        log.Add("");
        log.Add($"Seats marked in this scene: {seats}");

        if (seats < 6)
            log.Add("⚠ Under 6 seats. Patrons will fill them instantly and then " +
                    "stop arriving — mark more chairs with Cafe Step 2.");

        string text = string.Join("\n", log);
        Debug.Log("[Patron setup]\n" + text);
        EditorUtility.DisplayDialog("Patron setup",
            text + "\n\nPRESS CTRL+S.", "OK");
    }

    private static GameObject BuildPrefab(CustomerSpawner spawner, List<string> log)
    {
        SerializedObject so = new SerializedObject(spawner);
        GameObject source = so.FindProperty("customerPrefab").objectReferenceValue as GameObject;

        if (source == null)
        {
            log.Add("CustomerSpawner has no customer prefab assigned — nothing to copy.");
            return null;
        }

        Directory.CreateDirectory(PrefabFolder);

        GameObject temp = Object.Instantiate(source);
        temp.name = "Patron";

        // Everything that makes someone a CUSTOMER rather than a person having
        // a coffee. Left on, each of these would misbehave in its own way: a
        // CustomerBrain claims a counter slot and keeps DayClock waiting for it,
        // a CustomerInteractable offers a conversation with someone who has
        // nothing to say, and a JobMarker floats a ticket number over a person
        // with no ticket.
        Strip<CustomerBrain>(temp, log);
        Strip<CustomerInteractable>(temp, log);
        Strip<CustomerIdentity>(temp, log);

        foreach (JobMarker m in temp.GetComponentsInChildren<JobMarker>(true))
        {
            log.Add($"   removed JobMarker on '{m.gameObject.name}'");
            Object.DestroyImmediate(m.gameObject);
        }

        if (temp.GetComponent<NavMeshAgent>() == null)
            temp.AddComponent<NavMeshAgent>();

        if (temp.GetComponent<PatronBrain>() == null)
            temp.AddComponent<PatronBrain>();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);

        log.Insert(0, saved != null
            ? $"Built {PrefabPath} from '{source.name}':"
            : "FAILED to save the patron prefab.");

        return saved;
    }

    private static void Strip<T>(GameObject go, List<string> log) where T : Component
    {
        foreach (T c in go.GetComponentsInChildren<T>(true))
        {
            log.Add($"   removed {typeof(T).Name}");
            Object.DestroyImmediate(c, true);
        }
    }

    private static void WireSpawner(CustomerSpawner customerSpawner, GameObject prefab, List<string> log)
    {
        PatronSpawner existing = Object.FindAnyObjectByType<PatronSpawner>();
        GameObject host;

        if (existing != null)
        {
            host = existing.gameObject;
            log.Add($"Reusing the PatronSpawner on '{host.name}'.");
        }
        else
        {
            host = new GameObject("PatronSpawnPoint");
            host.transform.position = customerSpawner.transform.position;
            host.transform.rotation = customerSpawner.transform.rotation;
            Undo.RegisterCreatedObjectUndo(host, "Create PatronSpawnPoint");

            existing = Undo.AddComponent<PatronSpawner>(host);
            log.Add("Created 'PatronSpawnPoint' with a PatronSpawner.");
        }

        SerializedObject cs = new SerializedObject(customerSpawner);
        Transform spawnPoint = cs.FindProperty("spawnPoint").objectReferenceValue as Transform;
        Transform exitPoint = cs.FindProperty("exitPoint").objectReferenceValue as Transform;

        SerializedObject ps = new SerializedObject(existing);
        ps.FindProperty("patronPrefab").objectReferenceValue = prefab;
        ps.FindProperty("spawnPoint").objectReferenceValue = spawnPoint != null ? spawnPoint : host.transform;
        ps.FindProperty("exitPoint").objectReferenceValue = exitPoint;
        ps.ApplyModifiedProperties();

        EditorUtility.SetDirty(existing);

        log.Add($"   prefab      -> {prefab.name}");
        log.Add($"   spawn point -> {(spawnPoint != null ? spawnPoint.name : host.name)}");
        log.Add($"   exit point  -> {(exitPoint != null ? exitPoint.name : "NONE — patrons won't leave!")}");

        if (exitPoint == null)
            log.Add("⚠ No exit point on the CustomerSpawner to copy. Set it by hand.");
    }

    private static int CountSeats()
    {
        int n = 0;
        foreach (WaitingSpot s in Object.FindObjectsByType<WaitingSpot>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s != null && s.Kind == WaitingSpot.SpotKind.Seat) n++;
        return n;
    }

    [MenuItem("Fixit Fidget/Content/10 · Check the room")]
    public static void CheckRoom()
    {
        int seats = 0, loiter = 0;
        foreach (WaitingSpot s in Object.FindObjectsByType<WaitingSpot>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s == null) continue;
            if (s.Kind == WaitingSpot.SpotKind.Seat) seats++;
            else if (s.Kind == WaitingSpot.SpotKind.Loiter) loiter++;
        }

        PatronSpawner ps = Object.FindAnyObjectByType<PatronSpawner>();
        int reserve = 3, maxP = 14;

        if (ps != null)
        {
            SerializedObject so = new SerializedObject(ps);
            reserve = so.FindProperty("reserveSeatsForCustomers").intValue;
            maxP = so.FindProperty("maxPatrons").intValue;
        }

        int usable = Mathf.Max(0, seats - reserve);

        List<string> r = new()
        {
            $"Seats:            {seats}",
            $"Loiter spots:     {loiter}",
            $"Patron spawner:   {(ps != null ? "present" : "MISSING")}",
            "",
            $"Reserved for customers: {reserve}",
            $"So patrons can fill:    {usable} seat(s), capped at {maxP}",
            ""
        };

        if (ps == null)
            r.Add("Run '9 · Set up patrons' first.");
        else if (usable <= 0)
            r.Add("⚠ Patrons can never sit — reserve is >= your seat count. " +
                  "Mark more chairs as seats, or lower the reserve.");
        else if (usable < 4)
            r.Add("⚠ Only a handful of seats for patrons. The room will look " +
                  "half-empty. Mark more chairs with Cafe Step 2.");
        else
            r.Add("Looks right. Patrons will fill the room and stop before " +
                  "squeezing your paying customers out.");

        string text = string.Join("\n", r);
        Debug.Log("[Room check]\n" + text);
        EditorUtility.DisplayDialog("Room check", text, "OK");
    }
}
#endif
