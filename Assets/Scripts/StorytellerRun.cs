using System;

// Timing only. No repair, economy, input or save side effects. A due line is
// consumed only after presentation accepts it; pauses never queue a burst.
public sealed class StorytellerRun
{
    private readonly float interval;
    private readonly int lineLimit;
    private float remaining;
    public int LinesSpoken { get; private set; }
    public bool Quiet { get; private set; }
    public bool FocusRequested { get; private set; }
    public bool HasMore => !Quiet && LinesSpoken < lineLimit;
    public bool CanRequestFocus => !Quiet && LinesSpoken > 0;

    public StorytellerRun(float firstDelay, float interval, int lineLimit, bool remembersFocus)
    {
        remaining = SafeDelay(firstDelay);
        this.interval = SafeDelay(interval);
        this.lineLimit = Math.Max(0, lineLimit);
        Quiet = remembersFocus;
    }

    public bool Tick(float deltaTime, bool maySpeak)
    {
        if (!HasMore || !maySpeak || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
            return false;
        remaining = Math.Max(0f, remaining - deltaTime);
        return remaining <= 0f;
    }

    public void MarkSpoken()
    {
        if (!HasMore || remaining > 0f) return;
        LinesSpoken++;
        remaining = interval;
    }

    public bool RequestFocus()
    {
        if (!CanRequestFocus) return false;
        Quiet = true;
        FocusRequested = true;
        return true;
    }

    private static float SafeDelay(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 15f : Math.Max(1f, value);
}
