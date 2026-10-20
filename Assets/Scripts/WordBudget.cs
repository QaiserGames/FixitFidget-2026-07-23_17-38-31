using System;

// ---------------------------------------------------------------------------
// THE WORD BUDGET (playtest 3, 6 Oct 2026: claude/playtest-3-notes-and-plan.md §4 and §5)
//
// Mansoor's note on the third playtest: the phone has "WAYYY too many words", and the man's notes come "bombarded".
// So the budget is a rule in code, checked by the Bark rules (every line in the Night lines asset) and the Night rules
// (every hint, note, lesson, page and question in the story data), and it can't creep back:
//
//   * a bark (a line in a pool the world says on its own) is at most 7 words;
//   * a line in a scene is at most 10 words; a scene is at most 5 of its speaker's lines (Ace's replies and the lines said
//     back aren't counted: they are the player's), except Night 0's deal, the reveal, which has 8;
//   * a note on screen is at most 12 words; so is a page of the notebook;
//   * the phone's closing screen is at most 40 words before the day's review lines.
//
// One number each, here, if his sister wants more room. A word is anything between spaces with a letter or a digit in it
// (a "·" between two words isn't one); "{Thing}" and "N" count as one each, so a note is measured as it reads.
// Since 19 Oct 2026 a button is an icon, a TextMesh Pro sprite tag ("<sprite name="ps_cross">"), and a rich-text tag is not
// a word: tags are dropped before counting, so a line is still measured as it reads (the icon is a picture, not a word).
// No Unity types: Tests/BarkRules and Tests/NightRules compile this file.
// ---------------------------------------------------------------------------
public static class WordBudget
{
    public const int Bark = 7;
    public const int SceneLine = 10;
    public const int Scene = 5;
    public const int Deal = 8;
    public const int Note = 12;
    public const int Page = 12;
    public const int ClosingScreen = 40;

    // THE NOW LINE (playtest 7, 19 Oct 2026; his girlfriend "felt overall lost most of the time"): the one objective line at the
    // top left, day and night (DayOneGuideUI; NightCycle.NowLine at night). One step, at most 10 words, its button as an icon.
    public const int NowLine = 10;

    // THE BENCH SCREEN (playtest 5, 14 Oct 2026). His note: "the checklist is terrible"; the First Hour counted about 120 words
    // on the first repair's screen; his decision 7: "Under 25 words on the bench screen". Counted as the player sees them
    // (Fixit Fidget › Bench › Bench 6 counts every word on screen; Fixit Fidget › Checks › Bench words checks each source):
    //   * the first repair, with its controls card up, mouse or pad: at most 25 (the Producer's T7 and call 6, not UI/UX's 39);
    //   * steady play aims at 15 and is never over 24 (the longest card line, a flip, a blocked part hovered, last orders);
    //   * an alert (the bench's impatience card, a stock chip) has its own 12, on top;
    //   * the card's line, "Grace: "The lens and film path are dirty."", at most 8 words and 46 characters with the longest name;
    //   * a step's verb at most 2 words ("Unscrew", "Press in"), the flip at most 4 ("[Y] Flip it over");
    //   * the first repair's controls card 11 words (three lines with a mouse, four with a pad), a line at most 6;
    //   * the help hint 2 ("[Tab] Help"), the finished row 6 ("[E] Take it to the Officer").
    public const int BenchFirstRepair = 25;
    public const int BenchSteady = 24;
    public const int BenchSteadyAim = 15;
    public const int BenchAlert = 12;
    public const int CardLineWords = 8;
    public const int CardLineChars = 46;
    public const int BenchVerb = 2;
    public const int BenchFlip = 4;
    public const int FirstRepairControls = 11;
    public const int ControlsLine = 6;
    public const int HelpHint = 2;
    public const int FinishedRow = 6;

    /// <summary>
    /// How many words <paramref name="text"/> has: runs of anything that isn't white space, as long as the run has a
    /// letter or a digit in it (a lone "·", "›" or "—" between words isn't a word). A tag ("&lt;sprite name=...&gt;", "&lt;b&gt;")
    /// is skipped: an icon is not a word. 0 for nothing.
    /// </summary>
    public static int Words(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int words = 0;
        bool counted = false;   // this run already counted as a word
        bool inTag = false;     // inside "<...>": a sprite tag (an icon) or a rich-text tag, never a word
        foreach (char c in text)
        {
            if (inTag) { if (c == '>') inTag = false; continue; }
            if (c == '<') { inTag = true; continue; }
            if (char.IsWhiteSpace(c)) { counted = false; continue; }
            if (!counted && char.IsLetterOrDigit(c)) { words++; counted = true; }
        }
        return words;
    }

    /// <summary>True when <paramref name="text"/> has more than <paramref name="limit"/> words.</summary>
    public static bool Over(string text, int limit) => Words(text) > Math.Max(0, limit);

    /// <summary>"12 words (the rule is 7)": for a check's message.</summary>
    public static string Report(string text, int limit) => $"{Words(text)} word{(Words(text) == 1 ? "" : "s")} (the rule is {limit})";
}
