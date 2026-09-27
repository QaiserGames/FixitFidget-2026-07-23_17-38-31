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
// No Unity types: Tests/NotebookRules compiles this file.
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
        if (today.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("<b>Notebook</b>   ").Append(today.Count).Append(" new");
        for (int i = 0; i < today.Count && i < MaxLines; i++)
            text.Append('\n').Append(Small).Append(Line(today[i])).Append("</size>");
        if (today.Count > MaxLines)
            text.Append('\n').Append(Small).Append("<color=").Append(Grey).Append(">+")
                .Append(today.Count - MaxLines).Append(" more</color></size>");

        int facts = notebook.Count, people = notebook.PeopleCount;
        text.Append("\n<size=80%><color=").Append(Grey).Append(">(")
            .Append(facts).Append(facts == 1 ? " fact about " : " facts about ")
            .Append(people).Append(people == 1 ? " person" : " people")
            .Append(" so far)</color></size>");
        return text.ToString();
    }

    /// <summary>"Grace: a camera with a scratched strap. …"</summary>
    public static string Line(NotebookFactData fact)
    {
        string name = string.IsNullOrWhiteSpace(fact.name) ? fact.who : fact.name;
        string said = fact.text ?? "";
        // Mid-sentence after the name: "Grace: her husband…", not "Grace: Her husband…".
        if (said.Length > 1 && char.IsUpper(said[0]) && !char.IsUpper(said[1]))
            said = char.ToLowerInvariant(said[0]) + said.Substring(1);
        return name + ": " + said;
    }
}
