using UnityEngine;

// ---------------------------------------------------------------------------
// THE DOWNTIME METER (24 Oct 2026; P's spec §3.2 and §6.1): samples Downtime ten times a second while the day runs, into the
// day's counters the Day labs judge (DayShapeCheck, §6.8). New counters each morning. Read-only: it changes nothing in the
// game. CustomerSpawner adds it to itself (no scene change); it stands still while the morning's intro plays, at night, while
// paused and once the day is over. It samples LATE in the frame, after the Now line has read the room (DayOneGuideUI's
// LateUpdate, which swaps a waiting job for a customer's step in the same frame someone needs Ace), so a sample never catches
// the line a frame behind (the bar: no waiting job on the line while somebody needs Ace, §6.8).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class DowntimeMeter : MonoBehaviour
{
    /// <summary>One sample every this many seconds of the day (10 Hz).</summary>
    public const float Step = .1f;

    private float pending;
    private float stretch;
    private int day = -1;

    private void LateUpdate()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null) return;
        if (clock.Day != day)
        {
            day = clock.Day;
            Downtime.BeginDay(day);
            stretch = 0f;
            pending = 0f;
        }
        if (clock.DayOver || Time.timeScale <= 0f || !clock.isActiveAndEnabled || UiPins.DayIntroPlaying) return;
        if (NightWalk.Instance != null && NightWalk.Instance.Active) return;
        pending += Time.deltaTime;
        // At most a second's worth in one frame (a hitch must not invent a long stretch out of one reading).
        if (pending > 1f) pending = 1f;
        while (pending >= Step)
        {
            pending -= Step;
            Downtime.Sample(Step, ref stretch);
        }
    }
}
