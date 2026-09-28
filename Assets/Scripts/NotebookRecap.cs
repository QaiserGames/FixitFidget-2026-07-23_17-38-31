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
// No Unity types: Tests/NotebookRules and Tests/HomeRules compile this file.
// ---------------------------------------------------------------------------
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
        var people = new List<string>();
        var names = new Dictionary<string, string>();
        foreach (NotebookFactData fact in notebook.Facts)
        {
            string who = fact.who ?? "";
            if (names.ContainsKey(who)) continue;
            people.Add(who);
            names[who] = string.IsNullOrWhiteSpace(fact.name) ? who : fact.name;
        }
        var text = new StringBuilder();
        foreach (string who in people)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("<b>").Append(names[who]).Append("</b>");
            // Where they live first: that is what the night is for.
            for (int pass = 0; pass < 2; pass++)
                foreach (NotebookFactData fact in notebook.Facts)
                {
                    if ((fact.who ?? "") != who) continue;
                    bool address = fact.kind == Notebook.Kinds.Address;
                    if (address != (pass == 0)) continue;
                    text.Append('\n').Append(Small).Append("<indent=4%>").Append(Sentence(fact.text)).Append(Marker(fact.sure, ""))
                        .Append("</indent></size>");
                }
        }
        return text.ToString();
    }

    // A fact on its own line: street names filled in, and a capital to start.
    private static string Sentence(string text)
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
