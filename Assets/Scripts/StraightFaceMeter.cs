using System;

// ---------------------------------------------------------------------------
// THE STRAIGHT FACE (claude/ace-after-dark.md §3.2; the Night 1 slice)
//
// When someone tells Ace about what Ace did in the night, Ace has to keep a straight face. A needle
// sweeps across a meter, left to right and back, and the player stops it (Space, or X / Square on
// a pad) on or near the green mark:
//
//   * stopped in the green, or within "near" of it: held;
//   * stopped outside it, or not stopped before the scene's patience runs out: cracked.
//
// Difficulty follows the story (a stolen gnome is easy; being described to your face is hard): how
// long one sweep takes, how wide the green is, how much "near" counts and the patience all come
// from the thing that was taken (NightThings). Where the green sits is rolled with the caller's
// own System.Random, never UnityEngine.Random: the day's customers come from that stream.
//
// No Unity types: the Night 1 rules (Fixit Fidget > Checks, and Tests/NightRules) compile this file.
// ---------------------------------------------------------------------------
public sealed class StraightFaceMeter
{
    /// <summary>Seconds for the needle to cross the meter once (it then comes back).</summary>
    public float SweepSeconds { get; }
    /// <summary>The green mark's width, as a share of the meter (0-1).</summary>
    public float Green { get; }
    /// <summary>How far outside the green still counts as "near" it, as a share of the meter.</summary>
    public float Near { get; }
    /// <summary>Seconds before Ace cracks anyway if the needle is never stopped.</summary>
    public float Patience { get; }
    /// <summary>Where the green mark's middle is (0 is the left end, 1 the right).</summary>
    public float GreenCentre { get; }

    /// <summary>Where the needle is: 0 (left) to 1 (right).</summary>
    public float Needle { get; private set; }
    public float Elapsed { get; private set; }
    public bool Stopped { get; private set; }
    public bool Held { get; private set; }
    /// <summary>The patience ran out before the needle was stopped (a crack).</summary>
    public bool TimedOut { get; private set; }

    public float GreenLeft => GreenCentre - Green * .5f;
    public float GreenRight => GreenCentre + Green * .5f;

    public StraightFaceMeter(float sweepSeconds, float green, float near, float patience, float greenCentre)
    {
        SweepSeconds = Math.Max(.2f, Valid(sweepSeconds, 1f));
        Green = Clamp(Valid(green, .2f), .02f, .9f);
        Near = Clamp(Valid(near, 0f), 0f, .2f);
        Patience = Math.Max(1f, Valid(patience, 6f));
        float half = Green * .5f;
        GreenCentre = Clamp(Valid(greenCentre, .5f), half, 1f - half);
    }

    /// <summary>A meter whose green sits somewhere in the middle half, rolled with <paramref name="random"/>.</summary>
    public static StraightFaceMeter Rolled(float sweepSeconds, float green, float near, float patience, Random random)
    {
        float half = Clamp(Valid(green, .2f), .02f, .9f) * .5f;
        float low = Math.Max(.25f, half), high = Math.Min(.75f, 1f - half);
        float centre = high > low && random != null ? low + (float)random.NextDouble() * (high - low) : .5f;
        return new StraightFaceMeter(sweepSeconds, green, near, patience, centre);
    }

    /// <summary>True when a needle at <paramref name="at"/> would hold: in the green, or near it.</summary>
    public bool InGreen(float at) => at >= GreenLeft - Near && at <= GreenRight + Near;

    /// <summary>Move the needle on by <paramref name="seconds"/>. Past the patience, Ace cracks.</summary>
    public void Tick(float seconds)
    {
        if (Stopped || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
        Elapsed += seconds;
        Needle = PingPong(Elapsed / SweepSeconds);
        if (Elapsed >= Patience)
        {
            TimedOut = true;
            Stopped = true;
            Held = false;
        }
    }

    /// <summary>The player stops the needle where it is. True when Ace held it together. Only the first stop counts.</summary>
    public bool Stop()
    {
        if (Stopped) return Held;
        Stopped = true;
        Held = InGreen(Needle);
        return Held;
    }

    // 0 to 1 and back again, every two sweeps.
    static float PingPong(float t)
    {
        float m = t % 2f;
        return m <= 1f ? m : 2f - m;
    }

    static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    static float Valid(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
}
