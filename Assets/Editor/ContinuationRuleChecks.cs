using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public static class ContinuationRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Grace episode and drink freshness rules")]
    public static void Run() => Debug.Log("[Continuation rules] PASS: " + RunAll() + " assertions. No scene or save changes.");
#endif
    public static int RunAll()
    {
        int count = 0;
        void Check(bool passed, string reason) { count++; if (!passed) throw new InvalidOperationException(reason); }
        var fresh = new DrinkFreshness(30, 60);
        Check(fresh.Current == DrinkFreshness.Stage.Fresh && fresh.CanServe, "Just filled is fresh.");
        fresh.Advance(29.99f); Check(fresh.Current == DrinkFreshness.Stage.Fresh, "Fresh before boundary.");
        fresh.Advance(.01f); Check(fresh.Current == DrinkFreshness.Stage.Cooling && fresh.CanServe, "Cooling begins at boundary.");
        Check(fresh.TipMultiplier == .5f, "Cooling halves the tip, not the base price.");
        fresh.Advance(30); Check(fresh.Current == DrinkFreshness.Stage.Cold && !fresh.CanServe && fresh.RemainingFraction == 0, "Cold is unservable.");
        fresh.Advance(999); Check(fresh.Age == 60, "Cold age saturates.");
        var paused = new DrinkFreshness(5, 10);
        foreach (float bad in new[] { 0f, -1, float.NaN, float.PositiveInfinity }) paused.Advance(bad);
        Check(paused.Age == 0, "Pause or invalid delta cannot advance/rejuvenate a cup.");
        var another = new DrinkFreshness(5, 10); another.Advance(6);
        Check(paused.Age == 0 && another.Current == DrinkFreshness.Stage.Cooling, "Cup timers are independent.");
        var settings = new DrinkFreshness(-1, -3);
        Check(settings.ColdSeconds > settings.FreshSeconds && settings.FreshSeconds > 0, "Invalid authored durations remain ordered.");

        var service = new CustomerMemoryService();
        Check(!service.RecordGraceCamera("other", GraceCameraEpisode.EpisodeId, 1, "Perfect"), "Another regular cannot grant Grace's photo.");
        Check(!service.RecordGraceCamera("grace", "", 1, "Perfect"), "Generic perfect repair is not a camera episode.");
        var old = new RegularMemoryData { profileId = "grace", visits = 5, lastGrade = "Perfect" };
        Check(GraceCameraEpisode.PhotoOutcome(old) == GracePhotoOutcome.None, "Old saves do not invent a camera fact.");
        foreach (string grade in new[] { "Perfect", "Good", "Passable", "Rejected", "", "Unknown" })
        {
            service = new CustomerMemoryService();
            service.RecordVisit("grace", 1, true, true, true, LostReason.Declined, grade, true);
            Check(service.RecordGraceCamera("grace", GraceCameraEpisode.EpisodeId, 1, grade), "Explicit request recorded.");
            GracePhotoOutcome expected = grade == "Perfect" || grade == "Good" ? GracePhotoOutcome.Clear
                : grade == "Passable" ? GracePhotoOutcome.Imperfect : GracePhotoOutcome.Missed;
            Check(GraceCameraEpisode.PhotoOutcome(service.Read("grace")) == expected, "Grade selects honest photo.");
            Check(!service.AcknowledgeGraceReturn("grace", 1, out _), "No same-day photo grant.");
            // A later unrelated failure must not erase a real camera repair.
            service.RecordVisit("grace", 2, false, true, false, LostReason.StormedOutWaiting, "");
            Check(service.AcknowledgeGraceReturn("grace", 2, out var outcome) && outcome == expected, "Episode survives unrelated last visit.");
            Check(!service.AcknowledgeGraceReturn("grace", 3, out _), "No duplicate handoff.");
            var memory = service.Read("grace");
            Check(memory.gracePhotoClaimed == (expected != GracePhotoOutcome.Missed), "No photo from an unfinished camera.");
            Check(memory.focusBoundarySet, "Camera handoff preserves the focus boundary.");
            var restored = new CustomerMemoryService(); restored.Restore(service.Snapshot());
            Check(restored.Read("grace").gracePhotoVariant == memory.gracePhotoVariant, "Checkpoint preserves photo variant.");
            memory.gracePhotoVariant = "tampered";
            Check(service.Read("grace").gracePhotoVariant != "tampered", "Reads are independent copies.");
        }
        return count;
    }
}
