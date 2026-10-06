using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS: HIS FAVOURS, HIS PAGES, HIS LESSONS (claude/the-man-at-the-bins-story.md, agreed 6 Oct 2026;
// session 3: claude/session-3-favours-stalling-officer.md)
//
// What Ace hears him say is in the Night lines asset (Fixit Fidget > Night > Barks 1 adds his lines). This is what the
// game does with him:
//   * his pages: the notebook he hands over on Night 0 is his, its first pages in his hand (the inherited source).
//     NotebookRecap lays them out first, in italics, under his heading;
//   * his favours, one at a time, in order (Favours): Grace's gnome (asked in Night 0's deal), a sleeve of her reunion
//     cups (Night 2), the cones (when they exist). Every night after the deal opens at the bins (NightZero), where he
//     says his verdict on the day just ended, then tonight's scene (WhatTonight): the ask, the ask again (colder, after
//     a skip), a night off (Nights 4 and 7), or nothing yet (the next favour isn't in the game);
//   * what a favour pays: never money (the 25 Sept rule). A lesson (Nerve: the straight face's green is wider; Doors:
//     its edge comes with the keys) and a page (a secret for the notebook). Given to him, the thing joins his corner;
//   * stalling, never a fail state: a favour he asked for that the night ends without is a skip (NightLedger). The
//     morning after, he sits in the café for a minute (LodgerDay); after the third skip in a row he leaves a note on the
//     counter and stops asking for it.
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
    /// <summary>The gnome's return lines: he takes it; turns it to face the street; teaches the lesson; gives the page.</summary>
    public const string TakesItLine = "lodger.night1.r01", TurnsItLine = "lodger.night1.r04",
                        LessonLine = "lodger.night1.r05", PageLine = "lodger.night1.r06";
    /// <summary>What he says when Ace passes the bins: before the errand is done, with the thing in hand, and after.</summary>
    public const string Waiting = "lodger.waiting", Beckon = "lodger.beckon", Done = "lodger.done";
    /// <summary>Ace's own line of the night: after the deal, and on picking up the gnome.</summary>
    public const string AceNightZero = "ace.line.night0", AceNightOne = "ace.line.night1";

    /// <summary>His pool for the ask again, after a skip ("Tonight, then.").</summary>
    public const string Cold = "lodger.cold";
    /// <summary>A night off (Nights 4 and 7), and a night with nothing to ask yet (the next favour isn't in the game).</summary>
    public const string OffScene = "night.off", WaitScene = "night.wait";
    /// <summary>His verdict on the day just ended: a straight face held or cracked that morning; the officer's question kept or flinched at.</summary>
    public const string VerdictHeld = "lodger.verdict.held", VerdictCracked = "lodger.verdict.cracked",
                        VerdictQuiet = "lodger.verdict.quiet", VerdictFlinched = "lodger.verdict.flinched";
    /// <summary>His one line in the café the morning after a skip, by how warm he is.</summary>
    public const string Visit = "lodger.visit", VisitWarm = "lodger.visit.warm", VisitCold = "lodger.visit.cold";

    // ---------- the dials ----------

    /// <summary>From this warmth up he's warm (he volunteers the why when he asks; a kinder visit); from ColdFrom down, cold.</summary>
    public const int WarmFrom = 2, ColdFrom = -2;
    /// <summary>Skips in a row before he gives up on a favour (a note on the counter instead of a visit).</summary>
    public const int SkipsBeforeDropped = 3;
    /// <summary>How long he sits in the café the morning after a skip.</summary>
    public const float VisitSeconds = 60f;
    /// <summary>The Build phase: he comes in a quarter of the way through the day.</summary>
    public const float VisitFrom = .25f;

    /// <summary>Nights off (claude/night-0-and-the-favours-spec.md §10, call 4): nothing asked, nothing skipped.</summary>
    public static bool NightOff(int night) => night == 4 || night == 7;

    // ---------- the favours ----------

    public sealed class Favour
    {
        /// <summary>What he wants: a NightThings id (or a thing still to come). Saved: never change it once saves exist.</summary>
        public string id = "";
        /// <summary>The ask (a scene in the Night lines), and the same when he's warm (he says why). "" for the first: the deal asks.</summary>
        public string ask = "", askWarm = "";
        /// <summary>The ask's line on which Ace hears of the thing: the notebook learns where it is (NightThings.Mentioned).</summary>
        public string firstLine = "";
        /// <summary>What he wants, in a line (an id), said after his cold line when he asks again.</summary>
        public string remind = "";
        /// <summary>The return at the bins (a held scene with a choice), and its lines: he takes it, does what it was for, pays.</summary>
        public string returnScene = "";
        public string takes = "", sets = "", lesson = "", page = "";
        /// <summary>The lesson it pays (a Lessons id).</summary>
        public string teaches = "";
        /// <summary>The page it pays: a secret for the notebook, in his hand.</summary>
        public string pageId = "", pageText = "";
        /// <summary>Ace's own line as Ace takes it (Ace muttering), an id in the Night lines.</summary>
        public string aceLine = "";
        /// <summary>The night's note once he has asked: where it is. {Thing} is its name as Ace knows it, capitalised.</summary>
        public string hint = "";
        /// <summary>His note on the counter after the third skip in a row.</summary>
        public string note = "";
        /// <summary>What stands in his corner once he has it (NightZeroSet: "gnome", "cups").</summary>
        public string corner = "";
    }

    public const string Cones = "street.cones";

    static readonly Favour[] favours =
    {
        new Favour
        {
            id = NightThings.GraceGnome, ask = "", askWarm = "", firstLine = "", remind = "lodger.remind.gnome",
            returnScene = ReturnScene, takes = TakesItLine, sets = TurnsItLine, lesson = LessonLine, page = PageLine,
            teaches = "nerve", pageId = "lodger.page.grace.thursdays", pageText = "Grace. Thursdays. Find out.",
            aceLine = AceNightOne, hint = "{Thing} is on Grace's front step: the saffron house on the corner.",
            note = "Forget the gnome. I'll find my own eyes. Next time: something bigger.", corner = "gnome",
        },
        new Favour
        {
            id = NightThings.GraceCups, ask = "night2.ask", askWarm = "night2.ask.warm", firstLine = "lodger.night2.01",
            remind = "lodger.remind.cups", returnScene = "night2.return",
            takes = "lodger.night2.r01", sets = "lodger.night2.r04", lesson = "lodger.night2.r05", page = "lodger.night2.r06",
            teaches = "doors", pageId = "lodger.page.grace.photos", pageText = "Grace's photos. She's in none of them. Ask yourself why.",
            aceLine = "ace.line.night2",
            hint = "{Thing} are on Grace's kitchen worktop: the saffron house on the corner. Let yourself in at her front door.",
            note = "Forget the cups. Next time: something bigger.", corner = "cups",
        },
        new Favour
        {
            id = Cones, ask = "night3.ask", askWarm = "", firstLine = "", remind = "lodger.remind.cones", returnScene = "",
            teaches = "routines", aceLine = "ace.line.night3", hint = "Two spots on West Street. The road works have cones.",
            note = "Forget the cars. Next time: something bigger.", corner = "cones",
        },
    };

    public static IReadOnlyList<Favour> Favours => favours;

    public static Favour FindFavour(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Favour f in favours) if (f.id == id) return f;
        return null;
    }

    /// <summary>The first favour: what he asks for in Night 0's deal.</summary>
    public const string FirstErrand = NightThings.GraceGnome;

    /// <summary>
    /// The favour after <paramref name="after"/> in order ("" for the first) that isn't <paramref name="closed"/> (given
    /// to him or dropped), or "" when none is left.
    /// </summary>
    public static string NextFavour(string after, Func<string, bool> closed)
    {
        int from = 0;
        if (!string.IsNullOrEmpty(after))
            for (int i = 0; i < favours.Length; i++)
                if (favours[i].id == after) { from = i + 1; break; }
        for (int i = from; i < favours.Length; i++)
            if (closed == null || !closed(favours[i].id)) return favours[i].id;
        return "";
    }

    /// <summary>The thing Ace is out to get for him tonight, or "": his favour, once he has asked for it, until it's given.</summary>
    public static string Errand(NightLedger ledger) => ledger != null ? ledger.Errand : "";

    // ---------- tonight, at the bins ----------

    public enum Tonight { Ask, AskAgain, Off, Wait }

    /// <summary>
    /// What he says tonight, once Ace has put the bag in (every night after the deal): a night off; nothing yet (no
    /// favour left, or the next isn't <paramref name="inTheGame"/>: Night 3's cones before they exist); the ask again
    /// (he asked before and doesn't have it); or the ask.
    /// </summary>
    public static Tonight WhatTonight(NightLedger ledger, int night, Func<string, bool> inTheGame)
    {
        if (NightOff(night)) return Tonight.Off;
        Favour f = ledger != null ? FindFavour(ledger.Favour) : null;
        if (f == null || inTheGame != null && !inTheGame(f.id)) return Tonight.Wait;
        return ledger.AskedOn > 0 ? Tonight.AskAgain : Tonight.Ask;
    }

    /// <summary>The ask's scene: the warm one when he's warm and it has one.</summary>
    public static string AskScene(Favour favour, int warmth) =>
        favour == null ? "" : warmth >= WarmFrom && !string.IsNullOrEmpty(favour.askWarm) ? favour.askWarm : favour.ask;

    /// <summary>
    /// His verdict on day <paramref name="day"/> (the night after it is tonight), a pool in the Night lines, or "": the
    /// officer's question that day if there was one, else a straight face held or cracked that morning.
    /// </summary>
    public static string Verdict(NightLedger ledger, int day)
    {
        if (ledger == null) return "";
        QuestionData asked = ledger.QuestionOn(day);
        if (asked != null) return asked.cracked ? VerdictFlinched : VerdictQuiet;
        NightDeedData faced = ledger.FacedOn(day);
        if (faced != null) return faced.cracked ? VerdictCracked : VerdictHeld;
        return "";
    }

    /// <summary>His line in the café the morning after a skip: a pool by how warm he is.</summary>
    public static string VisitPool(int warmth) => warmth >= WarmFrom ? VisitWarm : warmth <= ColdFrom ? VisitCold : Visit;

    /// <summary>The night's note: where the thing is, named as Ace knows it ("Barnaby", "the reunion cups").</summary>
    public static string Hint(Favour favour, string thingName)
    {
        if (favour == null || string.IsNullOrEmpty(favour.hint)) return "";
        string name = string.IsNullOrEmpty(thingName) ? "It" : char.ToUpperInvariant(thingName[0]) + thingName.Substring(1);
        return favour.hint.Replace("{Thing}", name);
    }

    // ---------- lessons ----------

    public const string Nerve = "nerve", Doors = "doors";
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
        // Its edge (spare keys in the notebook, learned by day) comes with the keys (break-ins chunk D).
        new Lesson { id = Doors, name = "Doors", learned = "Learned: Doors. Every house has a key somewhere; people tell you where." },
    };

    public static IReadOnlyList<Lesson> Lessons => lessons;

    public static Lesson FindLesson(string id)
    {
        foreach (Lesson l in lessons) if (l.id == id) return l;
        return null;
    }

    /// <summary>The lesson bringing <paramref name="thing"/> back to him pays, or "".</summary>
    public static string LessonFor(string thing) => FindFavour(thing)?.teaches ?? "";

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
    public static NotebookFactData PageFor(string thing)
    {
        Favour f = FindFavour(thing);
        return f != null && !string.IsNullOrEmpty(f.pageId) ? Page(f.pageId, Notebook.Kinds.Secret, f.pageText) : null;
    }

    /// <summary>His note on the counter, kept as one of his pages: the favour he gave up on.</summary>
    public static NotebookFactData NotePage(string favour)
    {
        Favour f = FindFavour(favour);
        return f != null && !string.IsNullOrEmpty(f.note) ? Page("lodger.note." + f.id, Notebook.Kinds.Claim, f.note) : null;
    }

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
