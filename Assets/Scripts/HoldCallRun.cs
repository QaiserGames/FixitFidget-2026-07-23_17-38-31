using System;

// Pure timer state. A missed answer costs another wait, never hidden quality.
// Each call owns its state; callers supply scaled time so pauses freeze it.
public sealed class HoldCallRun
{
    public enum State { NeedsDialing, Connecting, OnHold, Ringing, Done }
    public State Phase { get; private set; }
    public float Remaining { get; private set; }
    public int MissedCalls { get; private set; }
    private float hold, ring;

    public bool Dial(float holdSeconds, float answerSeconds)
    {
        if (Phase != State.NeedsDialing) return false;
        hold = SafeSeconds(holdSeconds, 25f);
        ring = SafeSeconds(answerSeconds, 15f);
        Phase = State.Connecting; Remaining = .6f;
        return true;
    }
    public bool Answer()
    {
        if (Phase != State.Ringing) return false;
        Phase = State.Done; Remaining = 0f;
        return true;
    }
    public void Tick(float delta)
    {
        if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
        // Consume overshoot, including long frames. Finite state transitions
        // ensure there is no loop at zero and no unearned extra ring window.
        while (delta > 0f && Phase != State.Done && Phase != State.NeedsDialing)
        {
            if (delta < Remaining) { Remaining -= delta; return; }
            delta -= Remaining;
            switch (Phase)
            {
                case State.Connecting: Phase = State.OnHold; Remaining = hold; break;
                case State.OnHold: Phase = State.Ringing; Remaining = ring; break;
                case State.Ringing: Phase = State.NeedsDialing; Remaining = 0f; MissedCalls++; break;
            }
        }
    }
    private static float SafeSeconds(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(.1f, value);
}
