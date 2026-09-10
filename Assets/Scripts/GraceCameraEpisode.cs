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

    public static string Intake => "My family reunion is tomorrow, and this camera has picked a fine time to sulk. "
        + "Please clean the lens and film path, and fix the shutter. Leave the scratched strap exactly as it is; "
        + "my husband carried it everywhere. For once, I might let somebody put me in the picture.";

    public static string ReturnLine(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "The reunion pictures came out! That's me, right in the middle. "
            + "Usually I'm safely behind the camera. You even kept his old strap. I've brought a print for your shop.",
        GracePhotoOutcome.Imperfect => "We got our reunion picture. There's a smudge, so naturally I told everyone "
            + "it was artistic. I wish the camera had been a little cleaner, but I'm finally in the frame. This print is for you.",
        GracePhotoOutcome.Missed => "We missed the reunion photograph. I kept thinking we'd have one more minute. "
            + "I'm disappointed, but I haven't given up on that camera. Perhaps we can try again another day.",
        _ => ""
    };

    public static string CompletionLine(JobGrade grade) => grade switch
    {
        JobGrade.Perfect => "Look at that! Clean as a whistle, shutter moving, and his old strap still here. "
            + "Now I have no excuse to hide behind the camera tomorrow.",
        JobGrade.Good => "The shutter works, and you kept the strap. That'll get us our reunion photograph. Thank you, Ace.",
        JobGrade.Passable => "The shutter works, though it could use more cleaning. We'll try for a picture anyway. "
            + "Thank you for leaving the strap alone.",
        _ => "The shutter still isn't right. The reunion is tomorrow. I'll take it home, but this may be a moment we miss."
    };

    public static string HandoffLine(GracePhotoOutcome outcome) => outcome switch
    {
        GracePhotoOutcome.Clear => "Grace leaves her reunion photograph for the shop. She is finally in the frame.",
        GracePhotoOutcome.Imperfect => "Grace leaves the smudged reunion photograph for the shop, with a joke written underneath.",
        GracePhotoOutcome.Missed => "Grace keeps the empty frame. There is still room for another try.",
        _ => ""
    };
}
