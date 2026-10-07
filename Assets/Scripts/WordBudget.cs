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

    /// <summary>
    /// How many words <paramref name="text"/> has: runs of anything that isn't white space, as long as the run has a
    /// letter or a digit in it (a lone "·", "›" or "—" between words isn't a word). 0 for nothing.
    /// </summary>
    public static int Words(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int words = 0;
        bool counted = false;   // this run already counted as a word
        foreach (char c in text)
        {
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
