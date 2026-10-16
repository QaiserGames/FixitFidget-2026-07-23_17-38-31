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
        count += GraceTwoVisits();
        return count;
    }

    // DAYS 1-3 (14 Oct 2026; specs/days-1-3.md §5.3): her camera comes in twice under one episode id, Day 1 for the clean and
    // Day 2 for the shutter, and the photo comes on Day 3. R2 picks the visit from the fault's type; R3 records the photo's
    // attempt for the shutter visit only, downgraded to Passable when the clean before it wasn't Perfect or Good.
    static int GraceTwoVisits()
    {
        int count = 0;
        void Check(bool passed, string reason) { count++; if (!passed) throw new InvalidOperationException(reason); }
        const string Id = GraceCameraEpisode.EpisodeId;
        Check(GraceCameraEpisode.VisitFor(true) == GraceCameraVisit.Clean && GraceCameraEpisode.VisitFor(false) == GraceCameraVisit.Shutter,
            "R2: a cleaning fault is the clean visit; anything else is the shutter visit.");

        // R3, a clean visit records no attempt (Day 1), so no photo news comes on Day 2.
        var service = new CustomerMemoryService();
        RegularMemoryData before = service.Read("grace");
        service.RecordVisit("grace", 1, true, true, true, LostReason.Declined, "Perfect");
        Check(!service.RecordGraceCameraVisit("grace", Id, GraceCameraVisit.Clean, 1, "Perfect", before)
              && !service.Read("grace").graceCameraAttempted, "R3: Day 1's clean records no photo attempt.");
        Check(!GraceCameraEpisode.HasPendingReturn(service.Read("grace"), 2), "R3: so there's no photo news on Day 2.");
        Check(GraceCameraEpisode.CleanWentWell(service.Read("grace")) && !GraceCameraEpisode.CleanWentWell(null),
            "R2: a served Perfect clean went well (Day 2's first intake); no memory at all did not.");

        // R3, the shutter visit (Day 2) records it, read from her memory before that visit; the return comes on Day 3.
        before = service.Read("grace");
        service.RecordVisit("grace", 2, true, true, true, LostReason.Declined, "Perfect");
        Check(service.RecordGraceCameraVisit("grace", Id, GraceCameraVisit.Shutter, 2, "Perfect", before)
              && service.Read("grace").graceCameraDay == 2, "R3: Day 2's shutter records the photo's attempt, on Day 2.");
        Check(!service.AcknowledgeGraceReturn("grace", 2, out _), "The photo isn't back on the reunion day itself.");
        Check(!service.RecordGraceCameraVisit("grace", Id, GraceCameraVisit.Clean, 2, "Rejected", service.Read("grace"))
              && service.Read("grace").graceCameraGrade == "Perfect", "A clean visit never overwrites the shutter's attempt.");
        Check(service.AcknowledgeGraceReturn("grace", 3, out GracePhotoOutcome third) && third == GracePhotoOutcome.Clear,
            "The shutter visit's return comes on Day 3: a good clean and a working shutter make the clear photo.");

        // R3, the downgrade: (Day 1's clean grade, was she served, Day 2's shutter grade) -> the photo.
        var cases = new (string clean, bool served, string shutter, GracePhotoOutcome photo)[]
        {
            ("Perfect", true, "Perfect", GracePhotoOutcome.Clear),
            ("Good", true, "Perfect", GracePhotoOutcome.Clear),
            ("Passable", true, "Perfect", GracePhotoOutcome.Imperfect),    // a sloppy lens: the smudged photo
            ("Rejected", true, "Perfect", GracePhotoOutcome.Imperfect),
            ("", false, "Perfect", GracePhotoOutcome.Imperfect),           // she left unserved: "I cleaned the lens myself"
            ("Perfect", true, "Rejected", GracePhotoOutcome.Missed),       // a jammed shutter: no photo
            ("Passable", true, "Rejected", GracePhotoOutcome.Missed),
            ("Perfect", true, "", GracePhotoOutcome.Missed),               // she stormed out on Day 2
        };
        foreach (var (clean, served, shutter, photo) in cases)
        {
            service = new CustomerMemoryService();
            service.RecordVisit("grace", 1, served, true, served, served ? LostReason.Declined : LostReason.StormedOutWaiting, clean);
            before = service.Read("grace");
            service.RecordVisit("grace", 2, shutter.Length > 0, true, shutter.Length > 0,
                shutter.Length > 0 ? LostReason.Declined : LostReason.StormedOutWaiting, shutter);
            Check(service.RecordGraceCameraVisit("grace", Id, GraceCameraVisit.Shutter, 2, shutter, before),
                $"R3: the shutter visit is recorded (clean {Name(clean)}, shutter {Name(shutter)}).");
            Check(GraceCameraEpisode.PhotoOutcome(service.Read("grace")) == photo,
                $"R3: clean {Name(clean)}{(served ? "" : " (unserved)")} then shutter {Name(shutter)} make the {photo} photo.");
        }
        Check(GraceCameraEpisode.PhotoGrade("Perfect", null) == "Passable" && GraceCameraEpisode.PhotoGrade("Rejected", null) == "Rejected",
            "R3: with no clean behind it a working shutter makes a Passable photo; a jammed one stays Rejected.");

        // Her new lines keep to 90 characters a line on screen (placeholder copy; the Dialogue rules check reads them too).
        foreach (var (what, line) in GraceCameraEpisode.VisitLines())
            foreach (string beat in (line ?? "").Split('\n'))
                Check(beat.Trim().Length > 0 && beat.Trim().Length <= 90, $"Grace's {what}: \"{beat}\" is {beat.Trim().Length} characters (at most 90).");
        return count;
    }

    static string Name(string grade) => string.IsNullOrEmpty(grade) ? "(none)" : grade;
}
