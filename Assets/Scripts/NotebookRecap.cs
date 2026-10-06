using System.Collections.Generic;
using System.Text;

// ---------------------------------------------------------------------------
// The notebook's block in the end-of-day recap (claude/night-notebook-spec.md
// §4): what Ace noted today, then the running total. Empty on a day nothing
// was learned, so the recap only shows it when there is something to show.
//
// Styled like the reviews above it (ReputationRecap): a bold heading at the
// block's size, the lines a little smaller, the running total small and grey.
//
// Night step 3 (claude/night-homes-spec.md §3.4): a fact Ace isn't sure of
// says so ("(hunch)", "(likely)"), and a fact Ace became surer of today is
// listed after the new ones ("(likely now)"). Street names are filled in as
// the block is built (StreetNames), so a renamed street reads renamed.
//
// The recap phone's Notes app (playtest 2, step 2) lays the same notebook out
// person by person from People, the grouping the night's page (Page) uses too.
//
// The man at the bins' pages (6 Oct 2026; the inherited source, LodgerStory) come first wherever the
// notebook is laid out: they are the notebook's first pages, in his hand, so the page shows them in
// italics under his heading. What Ace learned sits under them.
//
// No Unity types: Tests/NotebookRules and Tests/HomeRules compile this file.
// ---------------------------------------------------------------------------

/// <summary>One person in Ace's notebook: what Ace calls them, and every fact about them,
/// where they live first, then the rest in the order they were learned.</summary>
public sealed class NotebookPerson
{
    public string who = "";
    public string name = "";
    public readonly List<NotebookFactData> facts = new List<NotebookFactData>();

    /// <summary>How many of their facts were first learned on <paramref name="day"/>.</summary>
    public int LearnedOn(int day)
    {
        int n = 0;
        foreach (NotebookFactData fact in facts) if (fact.day == day) n++;
        return n;
    }
}

public static class NotebookRecap
{
    /// <summary>At most this many of today's facts are listed; the rest are counted.</summary>
    public const int MaxLines = 5;

    const string Grey = "#A6A6A6";          // ReputationRecap's grey
    const string Small = "<size=92%>";      // ReputationRecap's line size

    public static string Build(Notebook notebook, int day)
    {
        if (notebook == null) return "";
        List<NotebookFactData> today = notebook.LearnedOn(day);
        List<NotebookFactData> surer = notebook.SurerOn(day);
        if (today.Count == 0 && surer.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("<b>Notebook</b>   ");
        if (today.Count > 0) text.Append(today.Count).Append(" new");
        if (surer.Count > 0) text.Append(today.Count > 0 ? ", " : "").Append(surer.Count).Append(" surer");
        int shown = 0;
        for (int i = 0; i < today.Count && shown < MaxLines; i++, shown++)
            text.Append('\n').Append(Small).Append(Line(today[i])).Append("</size>");
        for (int i = 0; i < surer.Count && shown < MaxLines; i++, shown++)
            text.Append('\n').Append(Small).Append(SurerLine(surer[i])).Append("</size>");
        int rest = today.Count + surer.Count - shown;
        if (rest > 0)
            text.Append('\n').Append(Small).Append("<color=").Append(Grey).Append(">+")
                .Append(rest).Append(" more</color></size>");

        int facts = notebook.Count, people = notebook.PeopleCount;
        text.Append("\n<size=80%><color=").Append(Grey).Append(">(")
            .Append(facts).Append(facts == 1 ? " fact about " : " facts about ")
            .Append(people).Append(people == 1 ? " person" : " people")
            .Append(" so far)</color></size>");
        return text.ToString();
    }

    /// <summary>
    /// The whole notebook as one page (the night walk opens it with a key; NightNotebook):
    /// person by person, in the order Ace first learned about them, where they live first,
    /// then everything else in the order it was learned. Guesses say so, as in the recap.
    /// Empty when there is nothing yet.
    /// </summary>
    public static string Page(Notebook notebook)
    {
        if (notebook == null || notebook.Count == 0) return "";
        var text = new StringBuilder();
        foreach (NotebookPerson person in People(notebook))
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("<b>").Append(person.name).Append("</b>");
            foreach (NotebookFactData fact in person.facts)
            {
                bool his = fact.source == Notebook.Sources.Inherited;
                text.Append('\n').Append(Small).Append("<indent=4%>").Append(his ? "<i>" : "").Append(Sentence(fact.text))
                    .Append(his ? "</i>" : "").Append(Marker(fact.sure, "")).Append("</indent></size>");
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// The notebook person by person, in the order Ace first learned about them (the recap
    /// phone's Notes, and the night's page). Each person's facts: where they live first, since
    /// that is what the night is for, then everything else in the order it was learned. Empty
    /// for no notebook or an empty one.
    /// </summary>
    public static List<NotebookPerson> People(Notebook notebook)
    {
        var people = new List<NotebookPerson>();
        if (notebook == null) return people;
        var byWho = new Dictionary<string, NotebookPerson>();
        // His pages first (they are the notebook's first pages), then everyone else as Ace met them.
        for (int pass = 0; pass < 2; pass++)
            foreach (NotebookFactData fact in notebook.Facts)
            {
                if ((fact.source == Notebook.Sources.Inherited) != (pass == 0)) continue;
                string who = fact.who ?? "";
                if (byWho.ContainsKey(who)) continue;
                var person = new NotebookPerson { who = who, name = string.IsNullOrWhiteSpace(fact.name) ? who : fact.name };
                byWho[who] = person;
                people.Add(person);
            }
        for (int pass = 0; pass < 2; pass++)
            foreach (NotebookFactData fact in notebook.Facts)
            {
                bool address = fact.kind == Notebook.Kinds.Address;
                if (address != (pass == 0)) continue;
                byWho[fact.who ?? ""].facts.Add(fact);
            }
        return people;
    }

    /// <summary>
    /// How sure Ace is of <paramref name="fact"/>, in the notebook's words, or "" for a fact Ace was
    /// told: "hunch" or "likely" for a guess, and for a fact Ace became surer of on
    /// <paramref name="day"/> (learned earlier), "likely now" or "sure now". The words the recap
    /// shows in brackets, plain, for the phone to colour.
    /// </summary>
    public static string Sureness(NotebookFactData fact, int day)
    {
        if (fact == null) return "";
        bool surer = fact.surerDay == day && fact.day < day;
        if (fact.sure == Notebook.Sureness.Hunch || fact.sure == Notebook.Sureness.Likely)
            return surer ? fact.sure + " now" : fact.sure;
        return surer && fact.sure == Notebook.Sureness.Sure ? "sure now" : "";
    }

    /// <summary>A fact as a sentence of its own: street names filled in, and a capital to start.</summary>
    public static string Sentence(string text)
    {
        string said = StreetNames.Resolve(text ?? "");
        if (said.Length > 0 && char.IsLower(said[0])) said = char.ToUpperInvariant(said[0]) + said.Substring(1);
        return said;
    }

    /// <summary>"Grace: a camera with a scratched strap. …", and "(hunch)" or "(likely)" when Ace isn't sure.</summary>
    public static string Line(NotebookFactData fact) => Said(fact) + Marker(fact.sure, "");

    /// <summary>A fact Ace became surer of today: "… (likely now)".</summary>
    public static string SurerLine(NotebookFactData fact) => Said(fact) + Marker(fact.sure, " now");

    private static string Said(NotebookFactData fact)
    {
        string name = string.IsNullOrWhiteSpace(fact.name) ? fact.who : fact.name;
        string said = StreetNames.Resolve(fact.text ?? "");
        // Mid-sentence after the name: "Grace: her husband…", not "Grace: Her husband…".
        if (said.Length > 1 && char.IsUpper(said[0]) && !char.IsUpper(said[1]))
            said = char.ToLowerInvariant(said[0]) + said.Substring(1);
        return name + ": " + said;
    }

    // Sure facts (everything Ace is told) carry no mark; guesses say so, in grey.
    private static string Marker(string sure, string suffix) =>
        sure == Notebook.Sureness.Hunch || sure == Notebook.Sureness.Likely
            ? " <color=" + Grey + ">(" + sure + suffix + ")</color>"
            : suffix.Length > 0 && sure == Notebook.Sureness.Sure ? " <color=" + Grey + ">(sure now)</color>" : "";
}
