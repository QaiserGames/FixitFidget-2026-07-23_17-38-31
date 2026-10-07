using UnityEngine;

// ---------------------------------------------------------------------------
// THE SCREEN'S OWN CLOCK (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.4)
//
// What's on screen used to time itself on Time.unscaledTime: it kept running while the game was paused, so with the phone
// up a night's note, a Day 1 hint or a badge over someone's head ran out behind it. Pausing really pauses now: the phone by
// day (PausePhone) holds this clock while it's up, and the things that time themselves on the screen read it instead:
//
//   UiClock.Now     seconds, like Time.unscaledTime, that stand still while the game is paused by the phone
//   UiClock.Delta   this frame's share of them (0 while paused)
//
// The end-of-day recap holds the world with Time.timeScale, as before; that isn't a pause of this clock (the screens it
// shows have the screen to themselves).
// ---------------------------------------------------------------------------
public static class UiClock
{
    static float heldTotal, heldSince = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        heldTotal = 0f;
        heldSince = -1f;
    }

    /// <summary>Real seconds since the game started, less the time it spent paused by the phone.</summary>
    public static float Now => (heldSince >= 0f ? heldSince : Time.unscaledTime) - heldTotal;

    /// <summary>This frame's real seconds, or 0 while paused by the phone.</summary>
    public static float Delta => heldSince >= 0f ? 0f : Time.unscaledDeltaTime;

    /// <summary>True while the phone holds the game paused.</summary>
    public static bool Held => heldSince >= 0f;

    /// <summary>Stops the clock (the phone opening). Twice is the same as once.</summary>
    public static void Hold()
    {
        if (heldSince < 0f) heldSince = Time.unscaledTime;
    }

    /// <summary>Starts it again where it stopped (the phone closing).</summary>
    public static void Release()
    {
        if (heldSince < 0f) return;
        heldTotal += Mathf.Max(0f, Time.unscaledTime - heldSince);
        heldSince = -1f;
    }
}
