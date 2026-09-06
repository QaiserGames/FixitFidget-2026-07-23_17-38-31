using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Exercises production CircuitRun directly; also usable by the headless .NET runner.
public static class CircuitRuleChecks
{
    private static int assertions;
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Circuit rules")]
    public static void RunInEditor()
    {
        int count = RunAll();
        Debug.Log($"[Circuit rules] PASS: {count} assertions. Run Circuit integration next.");
    }
#endif
    public static int RunAll()
    {
        assertions = 0;
        Symmetry();
        Generation();
        TimingAndFailure();
        GradeCeiling();
        return assertions;
    }

    private static void Symmetry()
    {
        var straight = CircuitRun.Side.Left | CircuitRun.Side.Right;
        Require(CircuitRun.Rotate(straight, 2) == straight, "A straight connected at 180 degrees must pass.");
        Require(CircuitRun.Rotate(straight, 1) != straight, "A perpendicular straight must fail.");
        var elbow = CircuitRun.Side.Up | CircuitRun.Side.Right;
        for (int i = 1; i < 4; i++) Require(CircuitRun.Rotate(elbow, i) != elbow, "Elbow orientations differ.");
        Require(CircuitRun.Rotate(elbow, -1) == CircuitRun.Rotate(elbow, 3), "Negative rotations wrap.");
        var run = new CircuitRun(4, 1, 0, 1, 123);
        run.Turn(0); run.Turn(0);
        run.Tick(1, true);
        Require(run.Reached == 1 && !run.Halted, "Actual run accepts symmetric straight, not just helper.");
        Require(!run.Turn(0), "Verified wire locks.");
    }

    private static void Generation()
    {
        for (int w = 2; w <= 6; w++)
        for (int h = 1; h <= 6; h++)
        for (int seed = 1; seed <= 40; seed++)
        {
            var run = new CircuitRun(w, h, 4, 0.25f, seed);
            var duplicate = new CircuitRun(w, h, 4, 0.25f, seed);
            var seen = new HashSet<string>();
            int wrong = 0;
            for (int i = 0; i < run.Count; i++)
            {
                var p = run.Position(i);
                Require(p.X >= 0 && p.X < w && p.Y >= 0 && p.Y < h, "Route inside bounds.");
                Require(seen.Add(p.X + ":" + p.Y), "No revisited cells.");
                Require(p.X == duplicate.Position(i).X && p.Y == duplicate.Position(i).Y
                    && run.Openings(i) == duplicate.Openings(i), "Seed reproduces layout and scramble.");
                if (!run.IsAligned(i)) wrong++;
                if (i == 0) Require((run.RequiredOpenings(i) & CircuitRun.Side.Left) != 0, "Entry from left.");
                if (i == run.Count - 1) Require((run.RequiredOpenings(i) & CircuitRun.Side.Right) != 0, "Exit to right.");
                if (i > 0)
                {
                    var a = run.Position(i - 1);
                    int dx = p.X - a.X, dy = p.Y - a.Y;
                    Require(Math.Abs(dx) + Math.Abs(dy) == 1, "Route uses adjacent tiles.");
                    var direction = dx > 0 ? CircuitRun.Side.Right : dx < 0 ? CircuitRun.Side.Left
                        : dy > 0 ? CircuitRun.Side.Up : CircuitRun.Side.Down;
                    Require((run.RequiredOpenings(i - 1) & direction) != 0
                        && (run.RequiredOpenings(i) & CircuitRun.Rotate(direction, 2)) != 0, "Consecutive ports reciprocate.");
                }
                Align(run, i);
            }
            Require(wrong == Math.Min(4, run.Count), "Requested scrambles must be visibly wrong.");
            for (int i = 0; i < run.Count; i++) run.Tick(0.25f, true);
            Require(run.Finished && run.RemainingTasks == 0 && run.Credits == run.Count, "Every generated board solvable to Perfect.");
        }
        var invalid = new CircuitRun(-10, 99, 999, float.NaN, 1);
        Require(invalid.Count >= 2 && invalid.Count <= 12, "Invalid dimensions clamp to 2 x 6.");
        var clean = new CircuitRun(4, 4, 0, 1, 5);
        for (int i = 0; i < clean.Count; i++) Require(clean.IsAligned(i), "Zero scramble is respected.");
    }

    private static void TimingAndFailure()
    {
        var run = new CircuitRun(4, 1, 0, 4, 1);
        Require(run.Credits == 0 && !run.Finished, "Aligned but unverified board has no free grade.");
        run.Tick(2, true);
        float progress = run.StepProgress;
        run.Tick(1000, false); run.Tick(0, true); run.Tick(float.NaN, true);
        Require(run.Reached == 0 && run.StepProgress == progress, "Away/paused/bad input cannot consume time.");
        run.Tick(2, true);
        Require(run.Reached == 1, "Resumes remaining interval, not whole interval.");
        run.Turn(1);
        run.Tick(4, true);
        Require(run.Halted && run.BestReached == 1 && run.Credits == 1, "Blocked charge retains verified prefix.");
        run.Tick(10000, true);
        Require(run.Retries == 0 && run.Credits == 1, "No automatic retry/penalty spiral.");
        Align(run, 1);
        Require(run.Halted, "Fixing tile alone does not charge a paid retry.");
        Require(run.Retry() && run.Retries == 1 && run.Ceiling == 3, "Explicit retry costs exactly one credit.");
        Require(!run.Retry() && run.Retries == 1, "Double click cannot spend twice.");
        Require(run.IsLocked(0), "Retry retains verified locks.");
        run.Tick(1000, true);
        Require(run.Reached == 1, "One hitch cannot skip many tiles.");
        for (int i = 1; i < run.Count; i++) run.Tick(4, true);
        Require(run.Finished && run.Credits == 3, "Retry can finish at reduced grade.");
        Require(!run.Turn(3) && !run.Retry(), "Completed board is immutable.");
    }

    private static void GradeCeiling()
    {
        // Repeated voluntary retries eventually reach zero credit, as in the submitted design.
        // The HUD must disclose this; solving after that is not a Perfect payout.
        var run = new CircuitRun(4, 1, 0, 1, 7);
        run.Turn(0);
        for (int i = 0; i < 8; i++)
        {
            run.Tick(1, true);
            Require(run.Halted && run.NextRetryCeiling >= 0, "Retry floor never negative.");
            run.Retry();
        }
        for (int i = 0; i < run.Count; i++) Align(run, i);
        for (int i = 0; i < run.Count; i++) run.Tick(1, true);
        Require(run.Finished && run.Credits == 0 && run.RemainingTasks == run.Count, "No overflow or grade resurrection after repeated retries.");
    }

    private static void Align(CircuitRun run, int i)
    {
        for (int turn = 0; turn < 4 && !run.IsAligned(i); turn++) run.Turn(i);
        Require(run.IsAligned(i), "Every tile has a reachable visible solution.");
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Circuit check: " + message);
    }
}
