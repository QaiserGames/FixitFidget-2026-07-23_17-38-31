using System;

public enum GracePhotoOutcome { None, Clear, Imperfect, Missed }

// A deliberately small, factual two-visit episode. No generic relationship or
// last-repair grade can unlock this story; both identities must be authored.
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

    // ---------- her words (PLACEHOLDER COPY: Mansoor rewrites it) ----------
    //
    // The dialogue pass (claude/dialogue-skyrim-proposal.md): short lines, one idea each. A "\n" starts
    // the next line on screen (the conversation shows them one after another, the earlier ones dimmed).
    // Every line shortens one she already had; nothing new about her is invented here.

    /// <summary>Day 1 at the counter: the joke, the reunion, the job and the strap (GDD v4.1: her intake).</summary>
    public static string Intake => "My camera picked a fine time to sulk. The reunion's tomorrow.\n"
        + "Jammed shutter, dirty lens. Leave the strap alone; my husband carried it everywhere.";

    /// <summary>
    /// Her thanks when Ace takes the camera: the reveal (she has avoided being in family photos). The
    /// notebook learns the reunion and that she stays behind the camera here (NotebookHooks.HeardThanks).
    /// </summary>
    public static string AcceptedLine => "Thank you. For once, I might let somebody put me in the picture.";

    /// <summary>Her return visit: how the reunion photo turned out. Said once Ace has taken her order.</summary>
    public static string ReturnLine(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "The reunion photos came out! I'm right in the middle.\n"
            + "Usually I'm safely behind the camera.",
        GracePhotoOutcome.Imperfect => "We got our reunion picture. There's a smudge\u2014I told everyone it was artistic.\n"
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

    /// <summary>Handing the camera back (a bubble over her head: one short line).</summary>
    public static string CompletionLine(JobGrade grade) => grade switch
    {
        JobGrade.Perfect => "Clean as a whistle, and his old strap's still here!",
        JobGrade.Good => "Shutter works, strap's safe. Thank you, Ace.",
        JobGrade.Passable => "It works. Could be cleaner, but thank you for leaving the strap alone.",
        _ => "The shutter still isn't right, and the reunion's tomorrow. I'll take it home."
    };

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
}
