using System;

// Pure timing rules: filling does not spend freshness, and pause supplies no delta.
public sealed class DrinkFreshness
{
    public enum Stage { Fresh, Cooling, Cold }
    public float Age { get; private set; }
    public float FreshSeconds { get; }
    public float ColdSeconds { get; }
    public DrinkFreshness(float freshSeconds, float coldSeconds)
    {
        FreshSeconds = Math.Max(1, freshSeconds);
        ColdSeconds = Math.Max(FreshSeconds + 1, coldSeconds);
    }
    public Stage Current => Age >= ColdSeconds ? Stage.Cold : Age >= FreshSeconds ? Stage.Cooling : Stage.Fresh;
    public bool CanServe => Current != Stage.Cold;
    public float RemainingFraction => Math.Max(0, 1 - Age / ColdSeconds);
    public float TipMultiplier => Current == Stage.Fresh ? 1 : Current == Stage.Cooling ? .5f : 0;
    public void Advance(float seconds)
    {
        if (!float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0)
            Age = Math.Min(ColdSeconds, Age + seconds);
    }
}
