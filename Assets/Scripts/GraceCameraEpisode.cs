using System;
using System.Collections.Generic;

public enum GracePhotoOutcome { None, Clear, Imperfect, Missed }

/// <summary>
/// Which of her two camera visits a job is (specs/days-1-3.md §5, 14 Oct 2026): Day 1's clean of the lens and the film path,
/// or Day 2's jammed shutter. None: not her camera episode at all.
/// </summary>
public enum GraceCameraVisit { None, Clean, Shutter }

// A deliberately small, factual two-visit episode. No generic relationship or
// last-repair grade can unlock this story; both identities must be authored.
//
// DAYS 1-3 AS AN INTRO LEVEL (14 Oct 2026; specs/days-1-3.md §5, the common brief's decision 8). His words after playtest 5:
// "god it is so hard to get the camera perfectly repaired, i stayed all day on the repair", and "the playtest couldn't even
// get passed day 1 for me". Her camera now comes in twice, under the one episode id: on Day 1 for a clean of the lens and the
// film path only (the cloth and the brush, the most forgiving verbs: the camera's fault 0), and on Day 2, the reunion day,
// for the jammed shutter (the two-hold swap: fault 1). The photo comes on Day 3. Four rules, no mechanic, no save field:
//   R1 GraceCameraRepairJob: the shutter gates the grade only when it is one of today's tasks;
//   R2 the visit comes from the job's fault type (VisitFor): her lines per visit (below; CustomerIdentity), and the notebook
//      learns her camera's story on the clean visit only (NotebookHooks);
//   R3 the photo's attempt is recorded for the shutter visit only, its grade downgraded to Passable when the clean before it
//      wasn't Perfect or Good (PhotoGrade; CustomerMemoryService.RecordGraceCameraVisit, SaveManager);
//   R4 DeviceWake: Day 1's camera only winds; on Day 2 it winds and fires.
// No Unity types: Tests/ContinuationRules compiles this file.
public static class GraceCameraEpisode
{
    public const string ProfileId = "grace";
    public const string EpisodeId = "grace_reunion_camera_v1";

    public static bool Matches(string profileId, string episodeId) =>
        string.Equals(profileId, ProfileId, StringComparison.Ordinal)
        && string.Equals(episodeId, EpisodeId, StringComparison.Ordinal);

    public static bool IsKnownGrade(string grade) =>
        grade == nameof(JobGrade.Perfect) || grade == nameof(JobGrade.Good)
        || grade == nameof(JobGrade.Passable) || grade == nameof(JobGrade.Rejected);

    public static bool HasPendingReturn(RegularMemoryData memory, int day) =>
        memory != null && memory.profileId == ProfileId && memory.graceCameraAttempted
        && day > memory.graceCameraDay && !memory.graceReturnAcknowledged;

    public static GracePhotoOutcome PhotoOutcome(RegularMemoryData memory)
    {
        if (memory == null || memory.profileId != ProfileId || !memory.graceCameraAttempted)
            return GracePhotoOutcome.None;
        if (!memory.graceCameraReturned) return GracePhotoOutcome.Missed;
        return memory.graceCameraGrade switch
        {
            nameof(JobGrade.Perfect) or nameof(JobGrade.Good) => GracePhotoOutcome.Clear,
            nameof(JobGrade.Passable) => GracePhotoOutcome.Imperfect,
            _ => GracePhotoOutcome.Missed
        };
    }

    // ---------- her two visits (R2, R3) ----------

    /// <summary>
    /// R2: which visit a camera job of the episode is, from its fault's type: <paramref name="cleaningFault"/> is
    /// job.faultType == FaultType.Cleaning. Cleaning is the clean (Day 1, fault 0); anything else is the shutter (Day 2,
    /// fault 1). The caller has already matched the episode id. (A bool, not FaultType: five console test projects compile
    /// this file without FaultType.cs.)
    /// </summary>
    public static GraceCameraVisit VisitFor(bool cleaningFault) =>
        cleaningFault ? GraceCameraVisit.Clean : GraceCameraVisit.Shutter;

    /// <summary>
    /// Did her visit before this one go well: served, and graded Perfect or Good (her memory as it was before this visit:
    /// lastVisitServed, lastGrade)? On Day 2 that visit is Day 1's clean. False for no memory at all.
    /// </summary>
    public static bool CleanWentWell(RegularMemoryData before) =>
        before != null && before.lastVisitServed
        && (before.lastGrade == nameof(JobGrade.Perfect) || before.lastGrade == nameof(JobGrade.Good));

    /// <summary>
    /// R3: the grade the photo's attempt stores for the shutter visit: the shutter's own grade, downgraded to Passable when her
    /// clean before it wasn't Perfect or Good (<paramref name="before"/>: her memory read before this visit is written). So a
    /// jammed shutter is no photo (Missed), a sloppy lens is the smudged photo (Imperfect), and both days' work count.
    /// </summary>
    public static string PhotoGrade(string shutterGrade, RegularMemoryData before)
    {
        bool usable = shutterGrade == nameof(JobGrade.Perfect) || shutterGrade == nameof(JobGrade.Good);
        return usable && !CleanWentWell(before) ? nameof(JobGrade.Passable) : shutterGrade ?? "";
    }

    // ---------- her words (PLACEHOLDER COPY: Mansoor and his sister rewrite it) ----------
    //
    // The dialogue pass (claude/dialogue-skyrim-proposal.md): short lines, one idea each. A "\n" starts
    // the next line on screen (the conversation shows them one after another, the earlier ones dimmed).
    // Every line keeps to 90 characters (Fixit Fidget > Checks > Days 1-3, and > Dialogue rules). The reunion is Day 2's evening
    // (specs/days-1-3.md §2.3): "the reunion's tomorrow" on Day 1, "tonight" on Day 2, the photo on Day 3.

    /// <summary>Day 1 at the counter, the clean visit: the camera, the reunion, just a clean, the strap (her intake).</summary>
    public static string Intake => "My husband's old camera. The reunion's tomorrow, and the lens is filthy.\n"
        + "Just a clean, please: lens and film path. Leave the strap; he carried it everywhere.";

    /// <summary>
    /// Her thanks when Ace takes the camera on Day 1: the reveal (she has avoided being in family photos). The
    /// notebook learns the reunion and that she stays behind the camera here (NotebookHooks.HeardThanks).
    /// </summary>
    public static string AcceptedLine => "Thank you. For once, I might let somebody put me in the picture.";

    /// <summary>
    /// Day 2 at the counter, the shutter visit: after a Perfect or Good clean she thanks Ace for it; otherwise (a Passable or
    /// Rejected clean, or she left unserved) she cleaned it herself. Either way the shutter jammed and the reunion is tonight.
    /// </summary>
    public static string ShutterIntake(bool cleanWentWell) => (cleanWentWell
            ? "You cleaned it beautifully. Then the shutter jammed on the first frame.\n"
            : "I cleaned the lens myself in the end. Now the shutter's jammed.\n")
        + "The reunion's tonight, Ace.";

    /// <summary>Her thanks when Ace takes the camera on Day 2.</summary>
    public static string ShutterAcceptedLine => "Bless you. My nephew takes the pictures tonight. I'm standing in the middle.";

    /// <summary>The intake for <paramref name="visit"/> (<paramref name="before"/>: her memory before this visit).</summary>
    public static string IntakeFor(GraceCameraVisit visit, RegularMemoryData before) =>
        visit == GraceCameraVisit.Shutter ? ShutterIntake(CleanWentWell(before)) : Intake;

    /// <summary>The thanks for <paramref name="visit"/>, when Ace takes the camera.</summary>
    public static string AcceptedFor(GraceCameraVisit visit) =>
        visit == GraceCameraVisit.Shutter ? ShutterAcceptedLine : AcceptedLine;

    /// <summary>Her return visit: how the reunion photo turned out. Said once Ace has taken her order.</summary>
    public static string ReturnLine(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "The reunion photos came out! I'm right in the middle.\n"
            + "Usually I'm safely behind the camera.",
        GracePhotoOutcome.Imperfect => "We got our reunion picture. There's a smudge—I told everyone it was artistic.\n"
            + "But I'm finally in the frame.",
        GracePhotoOutcome.Missed => "We missed the reunion photograph.\n"
            + "I haven't given up on that camera, though.",
        _ => ""
    };

    /// <summary>What she gives the shop with that news (a print), or nothing.</summary>
    public static string ReturnGift(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "I've brought a print for your shop.",
        GracePhotoOutcome.Imperfect => "This print is for you.",
        _ => ""
    };

    /// <summary>The news and the gift, one after another: what she says once her order is taken.</summary>
    public static string ReturnNews(GracePhotoOutcome outcome)
    {
        string news = ReturnLine(outcome);
        string gift = ReturnGift(outcome);
        if (string.IsNullOrEmpty(gift)) return news;
        return string.IsNullOrEmpty(news) ? gift : news + "\n" + gift;
    }

    /// <summary>Handing the camera back after Day 1's clean (a bubble over her head: one short line).</summary>
    public static string CompletionLine(JobGrade grade) => grade switch
    {
        JobGrade.Perfect => "Clean as a whistle, and his old strap's still here!",
        JobGrade.Good => "Much brighter. Thank you, Ace.",
        JobGrade.Passable => "A little cloudy still, but it'll do. Thank you for minding the strap.",
        _ => "Still grubby. I'll try a soft cloth at home."
    };

    /// <summary>
    /// Handing the camera back after Day 2's shutter (a bubble: one short line). The swap is one task, so it grades Perfect or
    /// Rejected; Good and Passable are here for safety.
    /// </summary>
    public static string ShutterCompletionLine(JobGrade grade) => grade switch
    {
        JobGrade.Perfect or JobGrade.Good or JobGrade.Passable => "It clicks! Shutter works, strap's safe. Thank you, Ace.",
        _ => "Still jammed. I'll take it tonight and hope."
    };

    /// <summary>The hand-back line for <paramref name="visit"/> at <paramref name="grade"/>.</summary>
    public static string CompletionFor(GraceCameraVisit visit, JobGrade grade) =>
        visit == GraceCameraVisit.Shutter ? ShutterCompletionLine(grade) : CompletionLine(grade);

    /// <summary>
    /// What happened, as a short line on screen once the conversation closes (not speech: no narrator
    /// talks in the speech panel).
    /// </summary>
    public static string HandoffLine(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "Grace left the reunion photo for the shop.",
        GracePhotoOutcome.Imperfect => "Grace left the smudged reunion photo for the shop.",
        GracePhotoOutcome.Missed => "Grace kept the empty frame, for another try.",
        _ => ""
    };

    /// <summary>Every line of her two camera visits, named (for the Days 1-3 check's 90-character rule).</summary>
    public static IEnumerable<(string what, string line)> VisitLines()
    {
        yield return ("Day 1 intake (the clean)", Intake);
        yield return ("Day 1 thanks", AcceptedLine);
        yield return ("Day 2 intake after a good clean", ShutterIntake(true));
        yield return ("Day 2 intake otherwise", ShutterIntake(false));
        yield return ("Day 2 thanks", ShutterAcceptedLine);
        foreach (JobGrade grade in new[] { JobGrade.Perfect, JobGrade.Good, JobGrade.Passable, JobGrade.Rejected })
        {
            yield return ($"Day 1 hand-back ({grade})", CompletionLine(grade));
            yield return ($"Day 2 hand-back ({grade})", ShutterCompletionLine(grade));
        }
    }
}
