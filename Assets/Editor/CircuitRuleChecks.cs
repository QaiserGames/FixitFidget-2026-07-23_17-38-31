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
        RouteBandsAndFallback();
        TimingAndFailure();
        FastRetryTiming();
        PulseBoost();
        RouteClearHint();
        GradeCeiling();
        return assertions;
    }

    private static void RouteClearHint()
    {
        var run = new CircuitRun(4, 1, 0, 4f, 123);
        Require(run.RouteClear && !run.Finished, "Aligned route may show boost hint before verification.");
        run.Turn(3);
        Require(!run.RouteClear, "Hint checks the far end, not only the current tile.");
        run.Turn(3);
        Require(run.RouteClear && run.Reached == 0 && run.Credits == 0,
            "Symmetric aligned route is clear, but reading the hint cannot award credits.");
        run.Tick(4f, true);
        Require(run.RouteClear && run.Reached == 1, "Verified locked prefix keeps the route-clear hint valid.");
        run.Turn(2);
        Require(!run.RouteClear && run.Credits == 1, "Changing an unverified tile removes the hint without changing earned credit.");
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
            int possible = w + (w - 1) * (h - 1);
            Require(run.Count >= Math.Max(w, Math.Min(6, possible))
                && run.Count <= Math.Max(w, Math.Min(9, possible)), "Default length band adapts to feasible grid size.");
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

    private static void RouteBandsAndFallback()
    {
        for (int w = 2; w <= 6; w++)
        for (int h = 1; h <= 6; h++)
        for (int count = w; count <= w + (w - 1) * (h - 1); count++)
        {
            // Exercise the rare fallback directly as well as the public generator.
            // Reflection keeps this check usable from Unity's separate Editor assembly.
            var fallback = typeof(CircuitRun).GetMethod("CreateFallbackRoute",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var route = (CircuitRun.Cell[])fallback.Invoke(null, new object[] { w, h, count });
            Require(route.Length == count && route[0].X == 0 && route[count - 1].X == w - 1,
                "Fallback has exact requested length and reaches output column.");
            var seen = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                var p = route[i];
                Require(p.X >= 0 && p.X < w && p.Y >= 0 && p.Y < h && seen.Add(p.X + ":" + p.Y),
                    "Fallback stays inside grid without revisiting cells.");
                if (i > 0) Require(Math.Abs(p.X - route[i - 1].X) + Math.Abs(p.Y - route[i - 1].Y) == 1,
                    "Fallback keeps every connection adjacent.");
            }
            var run = new CircuitRun(w, h, 1, 4, count, count, count);
            Require(run.Count == count, "Even an exact-length band always generates a board.");
            for (int i = 0; i < run.Count; i++) { Align(run, i); run.Tick(4, true); }
            Require(run.Finished && run.Credits == count, "Narrow-band generation remains solvable.");
        }
        var reversed = new CircuitRun(4, 4, 4, 4, 12, 9, 6);
        Require(reversed.Count == 9, "Reversed bounds collapse to the feasible minimum.");
        var outside = new CircuitRun(4, 4, 4, 4, 12, 999, -999);
        Require(outside.Count == 13, "Impossible bounds clamp without looping forever.");
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

    private static void FastRetryTiming()
    {
        var run = new CircuitRun(6, 1, 0, 4, 9);
        for (int i = 0; i < 4; i++) run.Tick(4, true);
        run.Turn(4); run.Tick(4, true);
        Require(run.Halted && run.BestReached == 4, "Failure after four verified tiles.");
        Align(run, 4); run.Retry();
        run.Tick(0.15f, true);
        Require(run.Reached == 0 && Math.Abs(run.StepProgress - 0.5f) < 0.001f,
            "Retry marker uses the short interval.");
        run.Tick(100, false);
        Require(run.Reached == 0 && Math.Abs(run.StepProgress - 0.5f) < 0.001f,
            "Leaving during fast replay preserves its remaining time.");
        run.Tick(0.15f, true);
        Require(run.Reached == 1 && run.BestReached == 4 && run.Credits == 3,
            "Replay takes 0.3 seconds and never awards duplicate credit.");
        for (int i = 1; i < 4; i++) run.Tick(0.3f, true);
        Require(run.Reached == 4 && run.StepProgress == 0, "Verified prefix replays quickly without starting next tile early.");
        run.Tick(2, true);
        Require(run.Reached == 4 && Math.Abs(run.StepProgress - 0.5f) < 0.001f,
            "First unverified tile restores the full four seconds.");
        run.Tick(100, false); run.Tick(2, true);
        Require(run.Reached == 5 && run.BestReached == 5, "Normal interval also survives interruption.");
        run.Turn(5); run.Tick(4, true); run.Retry();
        for (int i = 0; i < 5; i++) run.Tick(0.3f, true);
        Require(run.Reached == 5 && run.Retries == 2, "Second retry includes newly verified prefix.");
        Align(run, 5); run.Tick(4, true);
        Require(run.Finished && run.Credits == 4, "Replay does not erase retry penalties.");

        var first = new CircuitRun(4, 1, 0, 4, 1);
        first.Turn(0); first.Tick(4, true); Align(first, 0); first.Retry();
        first.Tick(0.3f, true);
        Require(first.Reached == 0, "Failure at entry has no prefix to fast-forward.");
        first.Tick(4, true);
        Require(first.Reached == 1, "Entry still gets a full normal interval.");
    }

    private static void PulseBoost()
    {
        var run = new CircuitRun(4, 1, 0, 4, 31);
        run.Tick(0.25f, true, 4);
        Require(run.Reached == 0 && Math.Abs(run.StepProgress - 0.25f) < 0.001f,
            "Boost visibly moves through a tile instead of instantly resolving it.");
        run.Tick(1, true);
        Require(run.Reached == 0 && Math.Abs(run.StepProgress - 0.5f) < 0.001f,
            "Releasing boost changes speed without resetting or jumping progress.");
        run.Tick(100, false, 4);
        Require(Math.Abs(run.StepProgress - 0.5f) < 0.001f, "Boost cannot bypass inspection pause.");
        run.Tick(0.5f, true, 4);
        Require(run.Reached == 1 && run.Credits == 1 && run.Retries == 0,
            "Boost verifies normally without charging an extra penalty.");
        run.Turn(1); run.Tick(1, true, 4);
        Require(run.Halted && run.Reached == 1 && run.Retries == 0,
            "Boost stops at an incorrect connection and does not auto-retry.");
        run.Tick(100, true, 4);
        Require(run.Halted && run.Retries == 0, "Holding boost on a blockage cannot spend retries.");
        Align(run, 1); run.Retry();
        run.Tick(0.125f, true, 4);
        Require(run.Reached == 0 && Math.Abs(run.StepProgress - 0.5f) < 0.001f,
            "Already-fast replay retains at least a quarter-second of visible travel.");
        run.Tick(0.125f, true, 4);
        Require(run.Reached == 1 && run.Credits == 0, "Boosted replay cannot award duplicate credit.");
        for (int i = 1; i < run.Count; i++) run.Tick(1, true, 4);
        Require(run.Finished && run.Credits == 3, "Boosted completion retains the ordinary retry grade.");
        run.Tick(100, true, 4);
        Require(run.Credits == 3, "Boost after completion cannot change the result.");

        var normal = new CircuitRun(4, 4, 4, 4, 37);
        var boosted = new CircuitRun(4, 4, 4, 4, 37);
        for (int i = 0; i < normal.Count; i++)
        {
            Align(normal, i); Align(boosted, i);
            normal.Tick(4, true); boosted.Tick(1, true, 4);
        }
        Require(normal.Finished && boosted.Finished && normal.Credits == boosted.Credits,
            "Identical solutions earn identical credit at either speed.");
        var invalid = new CircuitRun(4, 1, 0, 4, 41);
        invalid.Tick(1, true, float.NaN); invalid.Tick(1, true, float.PositiveInfinity);
        invalid.Tick(1, true, -5);
        Require(invalid.Reached == 0 && Math.Abs(invalid.StepProgress - 0.75f) < 0.001f,
            "Invalid boost settings safely use normal speed.");
        invalid.Tick(100, true, 999);
        Require(invalid.Reached == 1, "Boosted hitch still cannot skip multiple tiles.");
    }

    private static void GradeCeiling()
    {
        // Unfinished work can have zero credit; finishing always earns at least one.
        var run = new CircuitRun(4, 1, 0, 1, 7);
        run.Turn(0);
        for (int i = 0; i < 8; i++)
        {
            run.Tick(1, true);
            Require(run.Halted && run.NextRetryCeiling >= 1 && run.Credits == 0,
                "Completion ceiling has a floor; unfinished work gets no free credit.");
            run.Retry();
        }
        for (int i = 0; i < run.Count; i++) Align(run, i);
        for (int i = 0; i < run.Count; i++) run.Tick(1, true);
        Require(run.Finished && run.Credits == 1 && run.RemainingTasks == run.Count - 1,
            "Finished circuit retains one credit after repeated retries.");
        Require(run.Ceiling == 1 && run.NextRetryCeiling == 1, "HUD ceilings agree with the completion floor.");
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
