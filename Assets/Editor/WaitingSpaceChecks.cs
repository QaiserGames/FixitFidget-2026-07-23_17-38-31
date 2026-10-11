#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Waiting spaces: the reservation rules on synthetic spots, then (playtest 4, 10 Oct 2026) the café scene's own waiting
// rules when it is the open scene:
//   * every walk-in archetype and every regular waits in a seat (the Impatient and Rushed stood by choice, at x1.15, and
//     17 of 18 standing waiters were lost; Playtest 4 > Systems > 1 seats them);
//   * every seat drains at a seat's rate (at most 0.6), the bar stools too (a customer waits standing beside a bar stool,
//     Snap To Seat off, but it is a seat's wait);
//   * the repair bar's stools are within E's reach over the counter from the bench's stand point
//     (PlayerInteractor.acrossCounterReach), so a hand-back there is no walk round the counter.
public static class WaitingSpaceChecks
{
    const float SeatDrain = .6f;

    [MenuItem("Fixit Fidget/Checks/Waiting space reservations")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before checking waiting spaces.");
        var scene = EditorSceneManager.NewPreviewScene();
        WaitingSpot seatA = null, seatB = null;
        try
        {
            GameObject host = new GameObject("Reservation checks");
            SceneManager.MoveGameObjectToScene(host, scene);
            GameObject a = new GameObject("Seat A"), b = new GameObject("Seat B");
            GameObject first = new GameObject("First occupant"), second = new GameObject("Second occupant");
            foreach (var go in new[] { a, b, first, second }) go.transform.SetParent(host.transform);
            // Keep these synthetic spots away from the loaded gameplay scene.
            a.transform.position = b.transform.position = new Vector3(10000, 0, 10000);
            seatA = a.AddComponent<WaitingSpot>();
            seatB = b.AddComponent<WaitingSpot>();
            // Spots join WaitingArea's registry from OnEnable and leave it from
            // OnDisable. Edit mode never sends those messages to a script
            // without ExecuteAlways, so the check sends them itself — the
            // bodies that run are the real ones.
            Lifecycle(seatA, "OnEnable");
            Lifecycle(seatB, "OnEnable");
            Require(seatA.Claim(first.transform), "First claim succeeds.");
            Require(!seatA.Claim(second.transform), "One spot cannot have two occupants.");
            Require(!seatB.Claim(second.transform), "Duplicate markers cannot hold different bodies.");
            b.transform.position += Vector3.right * 0.4f;
            Require(!seatB.Claim(second.transform), "Nearby markers still overlap.");
            b.transform.position += Vector3.right * 2;
            Require(seatB.Claim(second.transform), "Separate seats remain usable.");
            seatA.Release(second.transform);
            Require(seatA.Occupant == first.transform, "Someone else cannot release a reservation.");
            seatB.Release(second.transform);
            b.transform.position = a.transform.position;
            a.SetActive(false);
            Lifecycle(seatA, "OnDisable");
            Require(seatB.Claim(second.transform), "Disabled seat releases its physical reservation.");
            Debug.Log("[Waiting space] PASS: shared occupancy, overlapping markers, release and disable.");
        }
        finally
        {
            // Leave the static registry as it was found.
            if (seatA != null) WaitingArea.Unregister(seatA);
            if (seatB != null) WaitingArea.Unregister(seatB);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        CafeScene();
    }

    // The café scene's waiting rules (playtest 4), when it is the open scene. Read-only.
    private static void CafeScene()
    {
        if (SceneManager.GetActiveScene().path != Playtest4Shared.ScenePath)
        {
            Debug.Log("[Waiting space] The café scene isn't the open scene, so its waiting rules weren't checked.");
            return;
        }
        var failures = new List<string>();
        var passes = new List<string>();
        void Expect(bool ok, string what) => (ok ? passes : failures).Add(what);

        CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
        Expect(spawner != null, "the café has a CustomerSpawner");
        if (spawner != null)
        {
            SerializedProperty archetypes = new SerializedObject(spawner).FindProperty("archetypes");
            Expect(archetypes != null && archetypes.arraySize > 0, "the spawner has walk-in archetypes");
            for (int i = 0; archetypes != null && i < archetypes.arraySize; i++)
            {
                SerializedProperty a = archetypes.GetArrayElementAtIndex(i);
                var kind = (WaitingSpot.SpotKind)a.FindPropertyRelative("preferredWaitKind").enumValueIndex;
                Expect(kind == WaitingSpot.SpotKind.Seat,
                    $"the {a.FindPropertyRelative("archetypeName").stringValue} walk-ins wait in a seat (they wait: {kind}; Playtest 4 > Systems > 1)");
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:CustomerProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (profile != null)
                Expect(profile.preferredWaitKind != WaitingSpot.SpotKind.Loiter, $"{profile.name} doesn't wait standing (waits: {profile.preferredWaitKind})");
        }

        var seats = UnityEngine.Object.FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude).Where(s => s != null && s.isActiveAndEnabled).ToList();
        Expect(seats.Count > 0, $"the café has seats ({seats.Count})");
        foreach (TableSeat seat in seats)
            Expect(seat.Kind == WaitingSpot.SpotKind.Seat && seat.DrainMultiplier <= SeatDrain + .001f,
                $"{seat.name} drains at a seat's rate (x{seat.DrainMultiplier:0.##}{(seat.SnapToSeat ? "" : ", a bar stool: they wait standing beside it")})");

        // The repair bar: its stools, across the counter from the bench, within E's reach over it.
        PlayerInteractor ace = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        StationInteractable bench = UnityEngine.Object.FindObjectsByType<StationInteractable>(FindObjectsInactive.Exclude)
            .FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        SerializedProperty reachProp = ace != null ? new SerializedObject(ace).FindProperty("acrossCounterReach") : null;
        Expect(ace != null && bench != null && reachProp != null, "Ace's reach over the counter and the repair bench are found");
        if (bench != null && reachProp != null)
        {
            float reach = reachProp.floatValue;
            Vector3 from = bench.StandPoint.position, toward = bench.StandPoint.forward;
            toward.y = 0f;
            toward.Normalize();
            var barStools = seats.Where(s => !s.SnapToSeat).Select(s => (seat: s, d: Flat(s.StandPoint.position - from)))
                                 .Where(x => Vector3.Dot(x.d, toward) > .5f && x.d.magnitude < reach + 1.5f).ToList();
            Expect(barStools.Count > 0, $"the repair bar's stools are found in front of the bench ({barStools.Count})");
            foreach (var x in barStools)
                Expect(x.d.magnitude <= reach, $"{x.seat.name} is {x.d.magnitude:0.00} m from the bench's stand point: within E's reach over the counter ({reach:0.0} m)");
        }

        string text = $"[Waiting space] The café scene: {passes.Count} passed, {failures.Count} failed.\n" +
                      string.Join("\n", failures.Select(f => "FAIL  " + f).Concat(passes.Select(p => "PASS  " + p)));
        if (failures.Count > 0)
        {
            Debug.LogError(text);
            throw new InvalidOperationException($"{failures.Count} of the café scene's waiting rules failed: " + failures[0]);
        }
        Debug.Log(text);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static void Lifecycle(WaitingSpot spot, string message)
    {
        MethodInfo method = typeof(WaitingSpot).GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null) throw new InvalidOperationException($"WaitingSpot has no {message}.");
        method.Invoke(spot, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
