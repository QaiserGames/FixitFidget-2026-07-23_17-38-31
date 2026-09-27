using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Ace's notebook (claude/night-notebook-spec.md): what Ace has learned about
// the café's regulars during the day - the raw material the night's leads
// will be made of ("curiosity, never markers": every place Ace can go at night
// must first be something written in here).
//
// Same shape as ReputationLedger: this class owns the facts, SaveManager
// decides when they are saved, and the recap shows what was learned today.
// Nothing reads the notebook yet: this step of the night plan is data only.
//
// No Unity types: Tests/NotebookRules compiles this file on its own.
// ---------------------------------------------------------------------------
public sealed class Notebook
{
    /// <summary>What a fact is about. Stored as text in saves, so the list can grow.</summary>
    public static class Kinds
    {
        public const string Address = "address", Possession = "possession", Schedule = "schedule",
                            Claim = "claim", Relationship = "relationship", Secret = "secret";
    }

    /// <summary>How Ace came to know it.</summary>
    public static class Sources
    {
        public const string Told = "told", Overheard = "overheard", Seen = "seen", Read = "read", Found = "found";
    }

    /// <summary>How sure Ace is. Only ever rises.</summary>
    public static class Sureness
    {
        public const string Hunch = "hunch", Likely = "likely", Sure = "sure";
    }

    private readonly List<NotebookFactData> facts = new();
    private readonly Dictionary<string, NotebookFactData> byId = new(StringComparer.Ordinal);

    /// <summary>Every fact, in the order it was learned. Treat as read-only.</summary>
    public IReadOnlyList<NotebookFactData> Facts => facts;

    public int Count => facts.Count;

    /// <summary>How many different people the notebook has something on.</summary>
    public int PeopleCount
    {
        get
        {
            var who = new HashSet<string>(StringComparer.Ordinal);
            foreach (NotebookFactData fact in facts) who.Add(fact.who ?? "");
            return who.Count;
        }
    }

    public static int Rank(string sure) => sure switch
    {
        Sureness.Sure => 3,
        Sureness.Likely => 2,
        Sureness.Hunch => 1,
        _ => 0
    };

    /// <summary>
    /// Learn <paramref name="fact"/> on <paramref name="day"/>. True when it is
    /// new. The same id is the same fact: hearing it again on a later day only
    /// marks it confirmed, and can make Ace surer of it, never less sure.
    /// </summary>
    public bool Learn(NotebookFactData fact, int day)
    {
        if (fact == null || string.IsNullOrEmpty(fact.id)) return false;
        if (byId.TryGetValue(fact.id, out NotebookFactData known))
        {
            if (day > known.day) known.confirmedDay = Math.Max(known.confirmedDay, day);
            if (Rank(fact.sure) > Rank(known.sure)) known.sure = fact.sure;
            return false;
        }
        NotebookFactData copy = fact.Copy();
        copy.day = Math.Max(0, day);
        copy.confirmedDay = 0;
        facts.Add(copy);
        byId[copy.id] = copy;
        return true;
    }

    public bool Knows(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

    /// <summary>The fact with this id, or null. Treat as read-only.</summary>
    public NotebookFactData Find(string id) =>
        !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out NotebookFactData fact) ? fact : null;

    /// <summary>Facts first learned on <paramref name="day"/>, in the order they were learned.</summary>
    public List<NotebookFactData> LearnedOn(int day)
    {
        var list = new List<NotebookFactData>();
        foreach (NotebookFactData fact in facts)
            if (fact.day == day) list.Add(fact);
        return list;
    }

    /// <summary>Replace the notebook with a saved one (copies; duplicates and blanks are dropped).</summary>
    public void Restore(NotebookFactData[] saved)
    {
        facts.Clear();
        byId.Clear();
        if (saved == null) return;
        foreach (NotebookFactData fact in saved)
        {
            if (fact == null || string.IsNullOrEmpty(fact.id) || byId.ContainsKey(fact.id)) continue;
            NotebookFactData copy = fact.Copy();
            facts.Add(copy);
            byId[copy.id] = copy;
        }
    }

    /// <summary>A copy of every fact, for the save.</summary>
    public NotebookFactData[] Snapshot()
    {
        var copy = new NotebookFactData[facts.Count];
        for (int i = 0; i < facts.Count; i++) copy[i] = facts[i].Copy();
        return copy;
    }
}
