#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// First-person walking smoothness, measured in the real scene during Play Mode.
// The probe is added at runtime only, so nothing here can be saved into a
// scene. Results go to the Console and to <project>/Logs/WalkingFeel/.
public static class WalkingFeelChecks
{
    const string Menu = "Fixit Fidget/Checks/Walking feel (Play Mode)/";

    // Scripted square with human-like key gaps, then reversals, on an empty
    // temporary floor. Compares movement as it was before 23 Sept (instant, 1 mm
    // Min Move Distance) with the current settings, and judges the current
    // settings. Takes about 15 seconds; the player is put back afterwards.
    [MenuItem(Menu + "Corner test")]
    public static void CornerTest()
    {
        var probe = Attach();
        if (probe != null) probe.BeginCornerSuite();
    }

    // Records ten seconds of your own walking where you are standing, with
    // your real keyboard input. Useful for anything the empty floor can't show:
    // rug edges, furniture, crowd collisions, frame hitches.
    [MenuItem(Menu + "Record my walking for 10 seconds")]
    public static void RecordWalking()
    {
        var probe = Attach();
        if (probe != null) probe.BeginRecording(10f);
    }

    [MenuItem(Menu + "Corner test", true)]
    [MenuItem(Menu + "Record my walking for 10 seconds", true)]
    static bool CanRun() => EditorApplication.isPlaying;

    static WalkingFeelProbe Attach()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Walking feel] Enter Play Mode first.");
            return null;
        }
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("[Walking feel] No PlayerMovement in the open scene.");
            return null;
        }
        if (movement.GetComponent<WalkingFeelProbe>() != null)
        {
            Debug.LogWarning("[Walking feel] A measurement is already running.");
            return null;
        }
        return movement.gameObject.AddComponent<WalkingFeelProbe>();
    }
}
#endif
