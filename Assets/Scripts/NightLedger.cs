using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// WHAT ACE'S NIGHTS HAVE DONE (claude/ace-after-dark.md §3.2; the Night 1 slice)
//
// Same shape as Notebook and ReputationLedger: this class owns the record, SaveManager decides
// when it's saved (with the next morning's checkpoint; nothing is saved during a night), and the
// game reads it:
//
//   * trophies: what's on Ace's shelf, in the order it was taken. TrophyShelf shows them, and
//     NightTrophy keeps a taken thing gone from the street, by day too. No money from theft;
//   * deeds: each thing taken, whose it was, and whether its owner has told Ace about it yet. The
//     owner comes in the next morning (CustomerSpawner) and tells Ace at the counter, and Ace has
//     to keep a straight face (MorningFace);
//   * suspicion: a cracked straight face makes that one person suspicious. It never touches stars
//     (claude/reputation-spec.md §5): one mistake, one consequence;
//   * the man at the bins (claude/the-man-at-the-bins-story.md, 6 Oct 2026): whether Ace has met him (Night 0's
//     deal), how warm he is to Ace (Ace's replies nudge it; hidden), the lessons he has taught Ace (they pay off
//     by day: Nerve widens the straight face's green) and what Ace has brought him (his corner by the bins).
//     A thing Ace gives him leaves Ace's shelf for his corner; it is still Ace's deed, so the morning is the same.
//
// No Unity types: the Night 1 rules (Fixit Fidget > Checks, and Tests/NightRules) compile this file.
// ---------------------------------------------------------------------------
public sealed class NightLedger
{
    readonly List<string> trophies = new();
    readonly List<NightDeedData> deeds = new();
    readonly Dictionary<string, int> suspicion = new(StringComparer.Ordinal);
    readonly List<string> lessons = new();
    readonly List<string> given = new();

    /// <summary>Something changed (a trophy taken, a morning scene played, a restore): shelves and streets look again.</summary>
    public event Action Changed;

    /// <summary>Nights Ace has come home from.</summary>
    public int Nights { get; private set; }
    /// <summary>On Ace's shelf, in the order taken. Treat as read-only.</summary>
    public IReadOnlyList<string> Trophies => trophies;
    /// <summary>Every thing taken, oldest first. Treat as read-only.</summary>
    public IReadOnlyList<NightDeedData> Deeds => deeds;

    public bool HasTrophy(string thing) => !string.IsNullOrEmpty(thing) && trophies.Contains(thing);

    // ---------- the man at the bins ----------

    /// <summary>Night 0's deal is behind Ace: the man at the bins has made himself known.</summary>
    public bool MetHim { get; private set; }
    /// <summary>How warm he is to Ace: Ace's replies nudge it, within plus or minus NightSaveData.MaxWarmth. Never shown.</summary>
    public int Warmth { get; private set; }
    /// <summary>What he has taught Ace, in order. Treat as read-only.</summary>
    public IReadOnlyList<string> Lessons => lessons;
    /// <summary>What Ace has brought him, in order (his corner). Treat as read-only.</summary>
    public IReadOnlyList<string> Given => given;

    /// <summary>The deal at the bins is made. True the first time.</summary>
    public bool Meet()
    {
        if (MetHim) return false;
        MetHim = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>A reply nudges how warm he is (negative: colder), kept within the limits. The new warmth.</summary>
    public int Warm(int by)
    {
        int next = Math.Max(-NightSaveData.MaxWarmth, Math.Min(NightSaveData.MaxWarmth, Warmth + by));
        if (next == Warmth) return Warmth;
        Warmth = next;
        Changed?.Invoke();
        return Warmth;
    }

    public bool Knows(string lesson) => !string.IsNullOrEmpty(lesson) && lessons.Contains(lesson);

    /// <summary>He teaches Ace <paramref name="lesson"/>. True when it's new.</summary>
    public bool Learn(string lesson)
    {
        if (string.IsNullOrEmpty(lesson) || lessons.Contains(lesson)) return false;
        lessons.Add(lesson);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Ace has brought him <paramref name="thing"/>.</summary>
    public bool HasGiven(string thing) => !string.IsNullOrEmpty(thing) && given.Contains(thing);

    /// <summary>
    /// Ace gives him <paramref name="thing"/>: it joins his corner and leaves Ace's shelf. Only a thing Ace has taken,
    /// and only once. True when it's new.
    /// </summary>
    public bool Give(string thing)
    {
        if (!HasTrophy(thing) || given.Contains(thing)) return false;
        given.Add(thing);
        Changed?.Invoke();
        return true;
    }

    /// <summary>On Ace's shelf: taken and not given to him.</summary>
    public bool OnShelf(string thing) => HasTrophy(thing) && !HasGiven(thing);

    /// <summary>
    /// Ace takes <paramref name="thing"/> from <paramref name="owner"/> on the night after day
    /// <paramref name="night"/>. True when it's new: a thing can only be taken once.
    /// </summary>
    public bool Take(string thing, string owner, int night)
    {
        if (string.IsNullOrEmpty(thing) || HasTrophy(thing)) return false;
        trophies.Add(thing);
        deeds.Add(new NightDeedData { thing = thing, owner = owner ?? "", night = Math.Max(0, night) });
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// The oldest thing <paramref name="owner"/> hasn't told Ace about yet, taken on a night before
    /// <paramref name="day"/> (so never on the night itself); or null.
    /// </summary>
    public NightDeedData Unfaced(string owner, int day)
    {
        if (string.IsNullOrEmpty(owner)) return null;
        foreach (NightDeedData deed in deeds)
            if (!deed.faced && deed.owner == owner && deed.night < day) return deed;
        return null;
    }

    /// <summary>Everyone with something to tell Ace on <paramref name="day"/>, oldest deed first, each once.</summary>
    public List<string> OwnersDue(int day)
    {
        var owners = new List<string>();
        foreach (NightDeedData deed in deeds)
            if (!deed.faced && deed.night < day && !string.IsNullOrEmpty(deed.owner) && !owners.Contains(deed.owner))
                owners.Add(deed.owner);
        return owners;
    }

    /// <summary>
    /// The owner has told Ace about <paramref name="deed"/> on <paramref name="day"/>. A crack makes
    /// them one step more suspicious. Each deed is faced once.
    /// </summary>
    public bool Faced(NightDeedData deed, bool cracked, int day)
    {
        if (deed == null || deed.faced || !deeds.Contains(deed)) return false;
        deed.faced = true;
        deed.cracked = cracked;
        deed.facedDay = Math.Max(0, day);
        if (cracked && !string.IsNullOrEmpty(deed.owner)) suspicion[deed.owner] = Suspicion(deed.owner) + 1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>How suspicious <paramref name="who"/> is of Ace: 0 is not at all.</summary>
    public int Suspicion(string who) => who != null && suspicion.TryGetValue(who, out int level) ? level : 0;

    /// <summary>Ace is home: one more night done.</summary>
    public void CameHome()
    {
        Nights++;
        Changed?.Invoke();
    }

    /// <summary>Replace the record with a saved one (copies; blanks and repeats are dropped).</summary>
    public void Restore(NightSaveData saved)
    {
        trophies.Clear();
        deeds.Clear();
        suspicion.Clear();
        lessons.Clear();
        given.Clear();
        Nights = saved != null ? Math.Max(0, saved.nights) : 0;
        MetHim = saved != null && saved.metHim;
        Warmth = saved != null ? Math.Max(-NightSaveData.MaxWarmth, Math.Min(NightSaveData.MaxWarmth, saved.warmth)) : 0;
        if (saved != null)
        {
            foreach (string thing in saved.trophies ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(thing) && !trophies.Contains(thing)) trophies.Add(thing);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (NightDeedData deed in saved.deeds ?? Array.Empty<NightDeedData>())
                if (deed != null && !string.IsNullOrEmpty(deed.thing) && seen.Add(deed.thing)) deeds.Add(deed.Copy());
            foreach (SuspicionData who in saved.suspicion ?? Array.Empty<SuspicionData>())
                if (who != null && !string.IsNullOrEmpty(who.who) && who.level > 0) suspicion[who.who] = who.level;
            foreach (string lesson in saved.lessons ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(lesson) && !lessons.Contains(lesson)) lessons.Add(lesson);
            // Only what Ace has taken can be his.
            foreach (string thing in saved.given ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(thing) && trophies.Contains(thing) && !given.Contains(thing)) given.Add(thing);
        }
        Changed?.Invoke();
    }

    /// <summary>A copy of everything, for the save.</summary>
    public NightSaveData Snapshot()
    {
        var data = new NightSaveData
        {
            nights = Nights,
            trophies = trophies.ToArray(),
            deeds = new NightDeedData[deeds.Count],
            metHim = MetHim,
            warmth = Warmth,
            lessons = lessons.ToArray(),
            given = given.ToArray(),
        };
        for (int i = 0; i < deeds.Count; i++) data.deeds[i] = deeds[i].Copy();
        var list = new List<SuspicionData>();
        foreach (KeyValuePair<string, int> pair in suspicion)
            if (pair.Value > 0) list.Add(new SuspicionData { who = pair.Key, level = pair.Value });
        list.Sort((a, b) => string.CompareOrdinal(a.who, b.who));
        data.suspicion = list.ToArray();
        return data;
    }
}
