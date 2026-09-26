#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WaitingSpaceChecks
{
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
