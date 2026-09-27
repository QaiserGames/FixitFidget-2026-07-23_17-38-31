using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// What goes into Ace's notebook, and when (claude/night-notebook-spec.md §3).
//
// Only facts from lines that are already in the game, written as Ace's
// shorthand of what the person actually said - no new character canon.
// Wording approved by Mansoor, 27 Sept 2026. To change what the notebook says,
// change it here (the ids must stay the same, or saved notebooks would learn
// the same thing twice).
//
// No Unity types: Tests/NotebookRules compiles this file.
// ---------------------------------------------------------------------------
public static class NotebookEntries
{
    public const string GraceId = GraceCameraEpisode.ProfileId;
    // Her profile's characterName; used when a save's memory is all there is to go on.
    public const string GraceName = "Grace";

    /// <summary>Grace's camera intake (GraceCameraEpisode.Intake): four things she tells Ace.</summary>
    public static IEnumerable<NotebookFactData> GraceIntake(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? GraceName : name;
        yield return Told("grace.camera.strap", GraceId, name, Notebook.Kinds.Possession,
            "A camera with a scratched strap. Wants the strap left exactly as it is.");
        yield return Told("grace.husband.strap", GraceId, name, Notebook.Kinds.Relationship,
            "Her husband carried that strap everywhere.");
        yield return Told("grace.reunion.date", GraceId, name, Notebook.Kinds.Schedule,
            "Family reunion tomorrow. First time she might let someone put her in the picture.");
        yield return Told("grace.behind.camera", GraceId, name, Notebook.Kinds.Claim,
            "Usually stays behind the camera.");
    }

    /// <summary>What Grace's return visit adds, by how the reunion photo turned out (GraceCameraEpisode.ReturnLine).</summary>
    public static NotebookFactData GraceReturn(string name, GracePhotoOutcome outcome)
    {
        name = string.IsNullOrWhiteSpace(name) ? GraceName : name;
        return outcome switch
        {
            GracePhotoOutcome.Clear => Told("grace.reunion.photo", GraceId, name, Notebook.Kinds.Possession,
                "In the middle of the reunion photo. Left a print for the shop."),
            GracePhotoOutcome.Imperfect => Told("grace.reunion.photo", GraceId, name, Notebook.Kinds.Claim,
                "Reunion photo has a smudge. Told everyone it was artistic."),
            GracePhotoOutcome.Missed => Told("grace.reunion.photo", GraceId, name, Notebook.Kinds.Claim,
                "Missed the reunion photo. Hasn't given up on the camera."),
            _ => null
        };
    }

    /// <summary>
    /// Any other regular's repair request: what they brought in and what is
    /// wrong with it, in their own intake's words ({device}: {fault}).
    /// </summary>
    public static NotebookFactData RegularRepair(string who, string name, string device, string fault)
    {
        if (string.IsNullOrWhiteSpace(who) || string.IsNullOrWhiteSpace(device)) return null;
        string thing = device.Trim().ToLowerInvariant();
        string problem = string.IsNullOrWhiteSpace(fault) ? "broken" : fault.Trim().ToLowerInvariant();
        return Told(who + ".brought." + Slug(thing), who, string.IsNullOrWhiteSpace(name) ? who : name,
            Notebook.Kinds.Possession, $"Brought in {Article(thing)} {thing}: {problem}.");
    }

    /// <summary>
    /// Ace saw a regular come out of, or go into, their own front door (night
    /// step 3, claude/night-homes-spec.md §3.3). One fact per person,
    /// "{who}.home": the first sighting is what gets written down; a sighting on
    /// a later day confirms it and can make Ace surer (HomeRules.SightingSureness).
    /// <paramref name="looks"/> is the house as seen ("the saffron house"); the
    /// street goes in as a token, so renaming the street renames it here too.
    /// </summary>
    public static NotebookFactData HomeSeen(string who, string name, string looks, string number, string streetId,
                                            bool cameOut, string sure)
    {
        if (string.IsNullOrWhiteSpace(who)) return null;
        string house = string.IsNullOrWhiteSpace(looks) ? "a house" : looks.Trim();
        string where = string.IsNullOrWhiteSpace(streetId) ? ""
            : string.IsNullOrWhiteSpace(number) ? " on " + StreetNames.Token(streetId)
            : " at " + number.Trim() + " " + StreetNames.Token(streetId);
        return new NotebookFactData
        {
            id = who + ".home",
            who = who,
            name = string.IsNullOrWhiteSpace(name) ? who : name,
            kind = Notebook.Kinds.Address,
            text = (cameOut ? "Came out of " : "Went into ") + house + where + ".",
            source = Notebook.Sources.Seen,
            sure = string.IsNullOrWhiteSpace(sure) ? Notebook.Sureness.Hunch : sure
        };
    }

    /// <summary>
    /// Facts an existing save already implies, with the day each happened: a
    /// save made before the notebook existed, whose regulars' memory says Grace
    /// has been in with her camera, gets what she said that day. Learning is
    /// idempotent, so running this on every load is harmless.
    /// </summary>
    public static IEnumerable<(NotebookFactData fact, int day)> Backfill(RegularMemoryData[] memories)
    {
        if (memories == null) yield break;
        foreach (RegularMemoryData memory in memories)
        {
            if (memory == null || memory.profileId != GraceId || !memory.graceCameraAttempted) continue;
            int day = Math.Max(1, memory.graceCameraDay);
            foreach (NotebookFactData fact in GraceIntake(GraceName)) yield return (fact, day);
            if (!memory.graceReturnAcknowledged) continue;
            // The return day is not in the memory; it was the next visit at the earliest.
            NotebookFactData photo = GraceReturn(GraceName, GraceCameraEpisode.PhotoOutcome(memory));
            if (photo != null) yield return (photo, day + 1);
        }
    }

    private static NotebookFactData Told(string id, string who, string name, string kind, string text) => new()
    {
        id = id,
        who = who,
        name = name,
        kind = kind,
        text = text,
        source = Notebook.Sources.Told,
        sure = Notebook.Sureness.Sure
    };

    private static string Article(string word) =>
        word.Length > 0 && "aeiou".IndexOf(word[0]) >= 0 ? "an" : "a";

    private static string Slug(string text)
    {
        var chars = new char[text.Length];
        int n = 0;
        foreach (char c in text)
            chars[n++] = char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-';
        return new string(chars, 0, n).Trim('-');
    }
}
