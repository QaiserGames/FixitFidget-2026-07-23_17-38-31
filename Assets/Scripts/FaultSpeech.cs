using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// ---------------------------------------------------------------------------
// WHAT'S WRONG, SAID OUT LOUD (playtest 4, 10 Oct 2026: the first-hour playtester's #5, "Walk-ins read their fault labels
// aloud": "This is frozen boot sequence - reconnect signal. Can you do it now?")
//
// A fault's label (DeviceFault.description on the device prefab) is a work order, and the ticket, the bench, the carry line
// and the day log keep it: "Frozen Boot Sequence - Reconnect Signal". A customer says it differently: "it's frozen on the
// start-up screen". This table is the spoken form of every fault label the game can roll, and {fault} in speech is filled
// with it (CustomerIdentity.SetFault). Each spoken form:
//   * is a clause of its own (it has its own verb), in lower case, with no full stop, written to follow "my {device}:",
//     "my {device} —" or "my {device},":  "My phone: the screen's cracked." / "My pocket watch: there's grit inside and it
//     won't keep time.";
//   * keeps a word of its label where it can ("cracked", "gunked up", "frozen", "seized up", "service", "support"), so the
//     player can match what was said to the ticket;
//   * says what the player is taking on, because {fault} is how the player decides (claude/dialogue-skyrim-proposal.md §6.2).
// A label missing here is said as it always was (the label in lower case), and Fixit Fidget › Checks › Dialogue rules fails
// until it has a spoken form. Devices need no table: CustomerIdentity.SpokenName already says "Pocket Watch" as "pocket watch".
//
// PLACEHOLDER COPY: Mansoor's sister rewrites them. Keep each a lower-case clause with no full stop: the Dialogue rules check
// fills every line that says {fault} with every one of them and fails on any that stops reading.
// Pure text, no Unity types.
// ---------------------------------------------------------------------------
public static class FaultSpeech
{
    // The label exactly as on the device prefab (matched ignoring case and the spaces round it) → how a customer says it.
    static readonly (string label, string said)[] Table =
    {
        // The phone (PhoneRepair.prefab): a part, the dirt, the circuit, and the Human fault (it was never broken).
        ("Cracked Screen", "the screen's cracked"),
        ("Gunked Up, Won't Charge", "it's gunked up and won't charge"),
        ("Frozen Boot Sequence - Reconnect Signal", "it's frozen on the start-up screen"),
        // 14 Oct (the integration, review M2): the bench card's 8 words and 46 characters with "Officer"; "calls light up the
        // screen, but it never rings" was 10 words and 57 characters, so Checks › Bench words could never pass.
        ("Calls light up the screen, but the phone never rings", "calls light up, but it never rings"),
        // The phone with no service (PhoneJob.prefab): the hold call, Ace's to make.
        ("Service disconnected - call support to restore it", "the service is cut off and support won't answer"),
        // The pocket watch (PocketWatch.prefab). 14 Oct (UI/UX's checklist v2, §5.2): the bench card's line is
        // {Name}: "{Spoken}." in at most 8 words and 46 characters; "there's grit inside and it won't keep time" was 9 words
        // and 54 characters with "Beatriz".
        ("Debris Inside, Won't Tell Time", "there's grit inside; it won't tick"),
        ("Seized Movement", "it's seized up and won't wind"),
        // Grace's reunion camera (GraceReunionCamera.prefab), in two visits since 14 Oct (specs/days-1-3.md §5.2): fault 0,
        // Day 1's clean, and fault 1, Day 2's shutter. Her visits say it in her own words (GraceCameraEpisode); these are
        // for every other line that names it, and the bench card: Grace: "The lens and film path are dirty." (8 words,
        // 42 characters). Day 2's is "the shutter's jammed", not "still jammed": on Day 1 she didn't know it was.
        ("Dirty Lens and Film Path", "the lens and film path are dirty"),
        ("Jammed Shutter", "the shutter's jammed"),
    };

    static Dictionary<string, string> byLabel;

    static Dictionary<string, string> ByLabel
    {
        get
        {
            if (byLabel != null) return byLabel;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (label, said) in Table) map[label.Trim()] = said;
            byLabel = map;
            return byLabel;
        }
    }

    /// <summary>Every label the table knows, as on the device prefabs (for the checks).</summary>
    public static IEnumerable<string> Labels
    {
        get { foreach (var row in Table) yield return row.label; }
    }

    /// <summary>The spoken form of <paramref name="label"/>; false (and "") when the table has none.</summary>
    public static bool TryGet(string label, out string said)
    {
        said = "";
        if (string.IsNullOrWhiteSpace(label)) return false;
        if (!ByLabel.TryGetValue(label.Trim(), out string found)) return false;
        said = found;
        return true;
    }

    /// <summary>
    /// What a customer says for <paramref name="label"/>: its spoken form; or, for a label the table doesn't know, the label
    /// in lower case, as the game always said it.
    /// </summary>
    public static string Spoken(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;
        return TryGet(label, out string said) ? said : label.ToLowerInvariant();
    }

    // Two or more Capitalised Words in a row, with only spaces between them ("Pocket Watch", "Frozen Boot Sequence"). A
    // sentence's first word followed by a lower-case one ("My phone") is not a run, and "I'd" is one word.
    static readonly Regex TitleCaseRun = new Regex(@"\b[A-Z][a-z']+(?:[ \t]+[A-Z][a-z']+)+", RegexOptions.Compiled);

    /// <summary>
    /// Does <paramref name="line"/>, as said, read like a work order rather than speech? A fault label said word for word (any
    /// case: the table's labels and <paramref name="moreLabels"/>), a spaced hyphen (" - ", how a label joins its two
    /// halves), or two or more Capitalised Words in a row. <paramref name="why"/> says which.
    /// </summary>
    public static bool ReadsLikeALabel(string line, IEnumerable<string> moreLabels, out string why)
    {
        why = "";
        if (string.IsNullOrWhiteSpace(line)) return false;
        foreach (string label in Labels)
            if (line.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                why = $"says the label \"{label}\"";
                return true;
            }
        if (moreLabels != null)
            foreach (string label in moreLabels)
                if (!string.IsNullOrWhiteSpace(label) && label.Trim().Length > 3 && line.IndexOf(label.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    why = $"says the label \"{label.Trim()}\"";
                    return true;
                }
        if (line.Contains(" - "))
        {
            why = "has a spaced hyphen (\" - \"), the way a label joins its two halves";
            return true;
        }
        Match run = TitleCaseRun.Match(line);
        if (run.Success)
        {
            why = $"has Words In Title Case (\"{run.Value}\")";
            return true;
        }
        return false;
    }

    /// <summary>The same, with the table's labels only.</summary>
    public static bool ReadsLikeALabel(string line, out string why) => ReadsLikeALabel(line, null, out why);
}
