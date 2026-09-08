using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public static class StorytellerRuleChecks
{
    private static int assertions;
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Storyteller timing rules")]
    public static void RunInEditor() => Debug.Log($"[Storyteller] PASS: {RunAll()} assertions.");
#endif
    public static int RunAll()
    {
        assertions = 0;
        var run = new StorytellerRun(12f, 22f, 3, false);
        Require(!run.CanRequestFocus && !run.RequestFocus(), "No invisible boundary before the first line.");
        run.MarkSpoken();
        Require(run.LinesSpoken == 0, "Cannot consume a line before it is due.");
        Require(!run.Tick(11f, true), "Initial delay is preserved.");
        Require(!run.Tick(100f, false), "Unavailable/paused time does not advance a story.");
        Require(run.Tick(1f, true), "Line becomes due after eligible time only.");
        Require(run.Tick(100f, true) && run.LinesSpoken == 0, "A busy room defers rather than spends the line.");
        run.MarkSpoken();
        Require(run.LinesSpoken == 1 && run.CanRequestFocus, "A presented line offers the boundary.");
        run.MarkSpoken();
        Require(run.LinesSpoken == 1, "Duplicate presentation cannot advance twice.");
        Require(!run.Tick(21f, true) && run.Tick(1f, true), "Next line gets the full interval, not a catch-up burst.");
        run.MarkSpoken();
        Require(run.RequestFocus() && run.Quiet && run.FocusRequested, "Polite focus request stops chatter.");
        Require(!run.RequestFocus(), "Focus cannot be requested twice.");
        for (int i = 0; i < 30; i++)
        {
            Require(!run.Tick(100f, true), "Quiet stays quiet.");
            run.MarkSpoken();
        }
        Require(run.LinesSpoken == 2, "No lines are awarded during quiet.");

        var returning = new StorytellerRun(1f, 1f, 3, true);
        Require(returning.Quiet && !returning.FocusRequested && !returning.CanRequestFocus
            && !returning.Tick(100f, true), "Remembered quiet changes behaviour without fabricating a new request.");
        var other = new StorytellerRun(1f, 1f, 2, false);
        Require(other.Tick(1f, true), "Another visit has independent timing.");
        other.MarkSpoken();
        Require(other.Tick(1f, true), "Second line arrives.");
        other.MarkSpoken();
        Require(!other.HasMore && !other.Tick(100f, true) && other.LinesSpoken == 2,
            "Exhausted story never repeats.");
        Require(other.CanRequestFocus, "The player can still set a boundary just after the final line.");

        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
        {
            var guarded = new StorytellerRun(1f, 1f, 1, false);
            Require(!guarded.Tick(bad, true) && guarded.Tick(1f, true), "Invalid delta cannot corrupt timing.");
            var authored = new StorytellerRun(bad, bad, 1, false);
            Require(!authored.Tick(0f, true) && authored.Tick(20f, true), "Invalid authored delay has a finite positive fallback.");
        }
        Require(!new StorytellerRun(1, 1, 0, false).HasMore
            && !new StorytellerRun(1, 1, -3, false).HasMore, "Empty content creates no phantom story.");
        return assertions;
    }

    private static void Require(bool ok, string why)
    {
        assertions++;
        if (!ok) throw new InvalidOperationException("Storyteller: " + why);
    }
}
