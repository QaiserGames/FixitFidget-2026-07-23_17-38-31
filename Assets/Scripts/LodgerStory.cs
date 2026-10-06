using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS: HIS PAGES, HIS LESSONS, THE FIRST ERRAND (claude/the-man-at-the-bins-story.md, agreed 6 Oct 2026)
//
// What Ace hears him say is in the Night lines asset (Fixit Fidget > Night > Barks 1 adds his lines). This is what the
// game does with him:
//   * his pages: the notebook he hands over on Night 0 is his, its first pages in his hand (the inherited source).
//     NotebookRecap lays them out first, in italics, under his heading;
//   * what a favour pays: never money (the 25 Sept rule). A lesson, a small edge that lasts (Nerve: the straight
//     face's green is wider), and a page (a secret for the notebook);
//   * the first errand: in Night 0's deal he asks for Grace's gnome. Ace takes it from her step (NightTrophy) and
//     carries it back to the bins (NightCarry); given to him, it joins his corner (Lodger), and he pays.
// The scenes and lines named below are the asset's ids; BarkSteps' Barks 1 makes them (Bark rules checks they're there).
//
// EVERY WORD HERE IS A PLACEHOLDER: Mansoor and his sister write him. Keep the ids (saves hold them).
// No Unity types: the Night rules (Fixit Fidget > Checks, and Tests/NightRules) compile this file.
// ---------------------------------------------------------------------------
public static class LodgerStory
{
    public const string SpeakerId = "lodger";

    // ---------- the Night lines he speaks through ----------

    /// <summary>Night 0, at the bins: the deal (a held scene with two choices).</summary>
    public const string DealScene = "night0.deal";
    /// <summary>The deal's line on which the notebook changes hands: his pages go in.</summary>
    public const string HandOverLine = "lodger.night0.07";
    /// <summary>Night 1's return: Ace brings the gnome back to the bins (a held scene with a choice).</summary>
    public const string ReturnScene = "night1.return";
    /// <summary>The return's lines: he takes the gnome; turns it to face the street; teaches the lesson; gives the page.</summary>
    public const string TakesItLine = "lodger.night1.r01", TurnsItLine = "lodger.night1.r04",
                        LessonLine = "lodger.night1.r05", PageLine = "lodger.night1.r06";
    /// <summary>What he says when Ace passes the bins: before the errand is done, with the thing in hand, and after.</summary>
    public const string Waiting = "lodger.waiting", Beckon = "lodger.beckon", Done = "lodger.done";
    /// <summary>Ace's own line of the night: after the deal, and on picking up the gnome.</summary>
    public const string AceNightZero = "ace.line.night0", AceNightOne = "ace.line.night1";

    // ---------- the first errand ----------

    /// <summary>What he asks for in Night 0's deal: Grace's gnome.</summary>
    public const string FirstErrand = NightThings.GraceGnome;

    /// <summary>The thing Ace is out to get for him tonight, or "": the first errand, once met and until it's given.</summary>
    public static string Errand(NightLedger ledger) =>
        ledger != null && ledger.MetHim && !ledger.HasGiven(FirstErrand) ? FirstErrand : "";

    // ---------- lessons ----------

    public const string Nerve = "nerve";
    /// <summary>With Nerve the straight face's green, and its "near enough", are this much wider.</summary>
    public const float NerveWidens = 1.45f;

    public sealed class Lesson
    {
        /// <summary>Saved: never change it once saves exist.</summary>
        public string id = "";
        /// <summary>Its name on screen ("Nerve").</summary>
        public string name = "";
        /// <summary>The note when he teaches it (the night's note at the bottom of the screen).</summary>
        public string learned = "";
    }

    static readonly Lesson[] lessons =
    {
        new Lesson { id = Nerve, name = "Nerve", learned = "Learned: Nerve. Your straight face holds a little longer." },
    };

    public static IReadOnlyList<Lesson> Lessons => lessons;

    public static Lesson FindLesson(string id)
    {
        foreach (Lesson l in lessons) if (l.id == id) return l;
        return null;
    }

    /// <summary>The lesson bringing <paramref name="thing"/> back to him pays, or "".</summary>
    public static string LessonFor(string thing) => thing == NightThings.GraceGnome ? Nerve : "";

    /// <summary>The straight face's green for a thing whose own is <paramref name="green"/>: wider with Nerve.</summary>
    public static float Green(float green, bool nerve) => nerve ? Math.Min(.6f, green * NerveWidens) : green;

    /// <summary>The straight face's "near enough" either side of the green: wider with Nerve too.</summary>
    public static float Near(float near, bool nerve) => nerve ? Math.Min(.2f, near * NerveWidens) : near;

    // ---------- his pages ----------

    /// <summary>Whose his pages are in the notebook: a heading of their own, laid out first.</summary>
    public const string PagesWho = "lodger.pages";
    public const string PagesName = "His pages";

    /// <summary>The pages that come with the notebook on Night 0 (what he has seen from the bins in two weeks).</summary>
    public static IEnumerable<NotebookFactData> Pages()
    {
        yield return Page("lodger.page.grace", Notebook.Kinds.Schedule,
            "Grace. The saffron house on the corner. Lights out by midnight.");
        yield return Page("lodger.page.gnome", Notebook.Kinds.Possession,
            "A gnome on her step. It faces the alley. I don't like it.");
        yield return Page("lodger.page.windows", Notebook.Kinds.Schedule,
            "The street's windows go dark one by one. All of them by three.");
        yield return Page("lodger.page.parking", Notebook.Kinds.Claim,
            "Nobody parks on " + StreetNames.Token("west") + " at night. Nobody.");
    }

    /// <summary>The page bringing <paramref name="thing"/> back pays, or null.</summary>
    public static NotebookFactData PageFor(string thing) =>
        thing == NightThings.GraceGnome ? Page("lodger.page.grace.thursdays", Notebook.Kinds.Secret, "Grace. Thursdays. Find out.") : null;

    static NotebookFactData Page(string id, string kind, string text) => new NotebookFactData
    {
        id = id,
        who = PagesWho,
        name = PagesName,
        kind = kind,
        text = text,
        source = Notebook.Sources.Inherited,
        sure = Notebook.Sureness.Sure,
    };
}
