using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public static class HoldCallRuleChecks
{
    private static int assertions;
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Support call rules")]
    public static void RunInEditor() => Debug.Log($"[Support call] PASS: {RunAll()} assertions.");
#endif
    public static int RunAll()
    {
        assertions = 0;
        var call = new HoldCallRun();
        Check(!call.Answer(), "Cannot answer before connecting.");
        Check(call.Dial(20, 15) && !call.Dial(20, 15), "Dial is single-shot.");
        call.Tick(.6f);
        Check(call.Phase == HoldCallRun.State.OnHold, "Connection enters unattended hold.");
        call.Tick(20);
        Check(call.Phase == HoldCallRun.State.Ringing && call.Remaining == 15, "Hold exposes a full answer window.");
        Check(call.Answer() && !call.Answer(), "Answer resolves once.");
        call.Tick(1000);
        Check(call.Phase == HoldCallRun.State.Done && !call.Dial(20, 15), "Completed call cannot restart.");

        call = new HoldCallRun();
        call.Dial(20, 15); call.Tick(36);
        Check(call.Phase == HoldCallRun.State.NeedsDialing && call.MissedCalls == 1, "Misses are recoverable.");
        Check(call.Dial(20, 15), "Redial works.");
        call.Tick(.6f); call.Tick(20);
        Check(call.Answer() && call.MissedCalls == 1, "A miss never prevents a later successful answer.");

        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            call = new HoldCallRun(); call.Dial(20, 15);
            float before = call.Remaining; call.Tick(invalid);
            Check(call.Remaining == before && call.Phase == HoldCallRun.State.Connecting, "Invalid/paused time does not advance.");
        }
        foreach (float duration in new[] { -100f, 0f, float.NaN, float.PositiveInfinity })
        {
            call = new HoldCallRun(); call.Dial(duration, duration); call.Tick(1000);
            Check(call.Phase == HoldCallRun.State.NeedsDialing && call.MissedCalls == 1, "Malformed durations cannot hang.");
        }
        var first = new HoldCallRun(); var second = new HoldCallRun();
        first.Dial(20, 15); second.Dial(30, 15); first.Tick(21);
        Check(first.Phase == HoldCallRun.State.Ringing && second.Phase == HoldCallRun.State.Connecting, "Call state is per instance.");

        var random = new System.Random(7291);
        for (int i = 0; i < 2000; i++)
        {
            float hold = 10 + random.Next(40), ring = 5 + random.Next(20);
            call = new HoldCallRun(); call.Dial(hold, ring);
            double elapsed = 0;
            while (call.Phase != HoldCallRun.State.NeedsDialing)
            {
                float delta = (float)(random.NextDouble() * .4 + .001);
                call.Tick(delta); elapsed += delta;
                Check(call.Remaining >= 0 && !float.IsNaN(call.Remaining), "Timer stays finite and nonnegative.");
                Check(elapsed <= hold + ring + 2, "Finite call never deadlocks.");
            }
            Check(call.MissedCalls == 1 && elapsed >= hold + ring + .59, "One miss after the actual time budget.");
        }
        return assertions;
    }
    private static void Check(bool ok, string why)
    {
        assertions++;
        if (!ok) throw new InvalidOperationException(why);
    }
}
