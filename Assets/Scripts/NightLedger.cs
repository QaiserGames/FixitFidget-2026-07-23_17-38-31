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
//     A thing Ace gives him leaves Ace's shelf for his corner; it is still Ace's deed, so the morning is the same;
//   * his favours (session 3): the one he's asking for now (LodgerStory.Favours, in order), the night he first asked
//     and last asked, and the skips in a row. A night that ends without the favour he asked for that night is a skip,
//     and the morning after costs the day (playtest 3, 6 Oct 2026): he sits at a table all morning (VisitDay, every
//     skip); the café opens with his mess (MessDay, from the second); a planted review, a word to the officer and his
//     note on the counter (WordDay and NoteDay, at the third, once a favour). The favour stays until it's given (Give),
//     which brings the next one up, not yet asked, and ends the costs. (Until 6 Oct the third skip dropped the favour:
//     Dropped keeps what older saves dropped, and nothing is added to it now.)
//   * the officer's questions (session 3): each asked once, on a day, kept or flinched at; a flinch makes him one
//     step more suspicious, like a cracked straight face;
//   * caught (break-ins chunk C, until getting caught has its own chunk): what Ace took that night goes back (PutBack).
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
    readonly List<string> dropped = new();
    readonly List<QuestionData> questions = new();

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

    /// <summary>
    /// The deal at the bins is made on night <paramref name="night"/>: he has asked for his first favour (the gnome).
    /// True the first time.
    /// </summary>
    public bool Meet(int night = 1)
    {
        if (MetHim) return false;
        MetHim = true;
        if (Favour.Length == 0) Favour = LodgerStory.NextFavour("", Closed);
        if (Favour.Length > 0) AskedOn = LastAsked = Math.Max(1, night);
        Changed?.Invoke();
        return true;
    }

    // ---------- his favours ----------

    /// <summary>The favour he's asking for now (a LodgerStory.Favours id); "" before the deal, or when none is left.</summary>
    public string Favour { get; private set; } = "";
    /// <summary>The night he first asked for it (0: not yet: the ritual's next ask is for it).</summary>
    public int AskedOn { get; private set; }
    /// <summary>The last night he asked for it (a night that ends without it, after he asked, is a skip).</summary>
    public int LastAsked { get; private set; }
    /// <summary>Nights in a row he asked for it and the night ended without it.</summary>
    public int Skips { get; private set; }
    /// <summary>The day he sits at a table in the café all morning (the morning after any skip), or 0.</summary>
    public int VisitDay { get; private set; }
    /// <summary>The day the café opens with his mess, a dirty cup on every seat (the morning after the second skip in a row and on), or 0.</summary>
    public int MessDay { get; private set; }
    /// <summary>The day of his planted review and the officer's word (the morning after the third skip in a row), or 0.</summary>
    public int WordDay { get; private set; }
    /// <summary>The day his note is on the counter (the same morning as WordDay), or 0; and the favour it's about.</summary>
    public int NoteDay { get; private set; }
    public string NoteFavour { get; private set; } = "";
    /// <summary>Favours he stopped asking for (saves from before 6 Oct's rewrite; nothing adds to it now). Treat as read-only.</summary>
    public IReadOnlyList<string> Dropped => dropped;

    /// <summary>The thing Ace is out to get for him: his favour, once asked for, until it's given; or "".</summary>
    public string Errand => MetHim && Favour.Length > 0 && AskedOn > 0 && !HasGiven(Favour) ? Favour : "";

    /// <summary>Done with: given to him, or dropped.</summary>
    public bool Closed(string favour) => !string.IsNullOrEmpty(favour) && (given.Contains(favour) || dropped.Contains(favour));

    /// <summary>He asks for his favour tonight (the ask, or the ask again). False with nothing to ask for.</summary>
    public bool Ask(int night)
    {
        if (!MetHim || Favour.Length == 0 || night <= 0) return false;
        if (AskedOn == 0) AskedOn = night;
        LastAsked = night;
        Changed?.Invoke();
        return true;
    }

    public bool VisitDue(int day) => day > 0 && VisitDay == day;
    public bool MessDue(int day) => day > 0 && MessDay == day;
    public bool WordDue(int day) => day > 0 && WordDay == day;
    public bool NoteDue(int day) => day > 0 && NoteDay == day;
    /// <summary>The officer's word question for the favour of <paramref name="day"/>'s word, or "" (its id is one a favour).</summary>
    public string WordQuestion(int day) => WordDue(day) && NoteFavour.Length > 0 ? OfficerStory.WordQuestionId(NoteFavour) : "";

    // The favour is done or dropped: the next one comes up, not asked yet.
    void MoveOn()
    {
        Favour = LodgerStory.NextFavour(Favour, Closed);
        AskedOn = LastAsked = Skips = 0;
    }

    // ---------- the officer's questions ----------

    /// <summary>Every question the officer has asked, oldest first. Treat as read-only.</summary>
    public IReadOnlyList<QuestionData> Questions => questions;

    public bool HasAsked(string question)
    {
        if (string.IsNullOrEmpty(question)) return false;
        foreach (QuestionData q in questions) if (q.id == question) return true;
        return false;
    }

    /// <summary>
    /// <paramref name="who"/> asked Ace <paramref name="question"/> on <paramref name="day"/>; a flinch makes them one step
    /// more suspicious (never stars). Each question is asked once. True when it's new.
    /// </summary>
    public bool Questioned(string question, string who, int day, bool cracked)
    {
        if (string.IsNullOrEmpty(question) || HasAsked(question)) return false;
        questions.Add(new QuestionData { id = question, who = who ?? "", day = Math.Max(0, day), cracked = cracked });
        if (cracked && !string.IsNullOrEmpty(who)) suspicion[who] = Suspicion(who) + 1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The question asked on <paramref name="day"/> (the latest, if more than one), or null.</summary>
    public QuestionData QuestionOn(int day)
    {
        QuestionData found = null;
        foreach (QuestionData q in questions) if (q.day == day && day > 0) found = q;
        return found;
    }

    /// <summary>The deed someone told Ace about on <paramref name="day"/> (the latest), or null.</summary>
    public NightDeedData FacedOn(int day)
    {
        NightDeedData found = null;
        foreach (NightDeedData deed in deeds) if (deed.faced && deed.facedDay == day && day > 0) found = deed;
        return found;
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
        // His favour done: the next one comes up (asked for at the bins, a night from now).
        if (thing == Favour) MoveOn();
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
    /// Ace was caught on night <paramref name="night"/> (break-ins chunk C's placeholder, until getting caught has its own
    /// chunk): whatever Ace took that night goes back, off the shelf and out of the deeds, so nobody comes in about it in
    /// the morning. What Ace already gave the man at the bins stays his (his favour has moved on). The things put back,
    /// oldest first.
    /// </summary>
    public List<string> PutBack(int night)
    {
        var back = new List<string>();
        for (int i = 0; i < deeds.Count; i++)
        {
            NightDeedData deed = deeds[i];
            if (deed == null || deed.night != night || deed.faced || given.Contains(deed.thing)) continue;
            back.Add(deed.thing);
            trophies.Remove(deed.thing);
            deeds.RemoveAt(i);
            i--;
        }
        if (back.Count > 0) Changed?.Invoke();
        return back;
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

    /// <summary>
    /// Ace is home from night <paramref name="night"/>: one more night done. If he asked for his favour tonight and the
    /// night ends without it, that's a skip, and the morning after costs the day by skips in a row: his table (every
    /// skip), his mess (from the second), and once a favour (the third) the planted review, the officer's word and his
    /// note on the counter. The favour stays his ask.
    /// </summary>
    public void CameHome(int night = 0)
    {
        Nights++;
        if (night > 0 && MetHim && Favour.Length > 0 && LastAsked == night && !HasGiven(Favour))
        {
            Skips++;
            VisitDay = night + 1;
            if (Skips >= LodgerStory.MessFromSkips) MessDay = night + 1;
            if (Skips == LodgerStory.WordAtSkips)
            {
                WordDay = NoteDay = night + 1;
                NoteFavour = Favour;
            }
        }
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
        dropped.Clear();
        questions.Clear();
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
            foreach (string favour in saved.dropped ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(favour) && !dropped.Contains(favour)) dropped.Add(favour);
            var asked = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestionData q in saved.questions ?? Array.Empty<QuestionData>())
                if (q != null && !string.IsNullOrEmpty(q.id) && asked.Add(q.id)) questions.Add(q.Copy());
        }
        RestoreFavour(saved);
        Changed?.Invoke();
    }

    // His favour as saved, tidied: an unknown or finished one moves on; a save from before the favours (met, no favour)
    // picks up at the first one not given, which the deal asked for if it's the gnome.
    void RestoreFavour(NightSaveData saved)
    {
        Favour = saved?.favour ?? "";
        AskedOn = saved != null ? Math.Max(0, saved.askedOn) : 0;
        LastAsked = saved != null ? Math.Max(0, saved.lastAsked) : 0;
        Skips = saved != null ? Math.Max(0, Math.Min(LodgerStory.SkipsCap, saved.skips)) : 0;
        VisitDay = saved != null ? Math.Max(0, saved.visitDay) : 0;
        MessDay = saved != null ? Math.Max(0, saved.messDay) : 0;
        WordDay = saved != null ? Math.Max(0, saved.wordDay) : 0;
        NoteDay = saved != null ? Math.Max(0, saved.noteDay) : 0;
        NoteFavour = saved?.noteFavour ?? "";
        if (!MetHim)
        {
            Favour = "";
            AskedOn = LastAsked = Skips = 0;
            return;
        }
        if (Favour.Length > 0 && (LodgerStory.FindFavour(Favour) == null || Closed(Favour))) Favour = "";
        if (Favour.Length == 0)
        {
            Favour = LodgerStory.NextFavour("", Closed);
            AskedOn = LastAsked = Skips = 0;
        }
        if (Favour == LodgerStory.FirstErrand && AskedOn == 0) AskedOn = 1;
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
            favour = Favour,
            askedOn = AskedOn,
            lastAsked = LastAsked,
            skips = Skips,
            visitDay = VisitDay,
            messDay = MessDay,
            wordDay = WordDay,
            noteDay = NoteDay,
            noteFavour = NoteFavour,
            dropped = dropped.ToArray(),
            questions = new QuestionData[questions.Count],
        };
        for (int i = 0; i < questions.Count; i++) data.questions[i] = questions[i].Copy();
        for (int i = 0; i < deeds.Count; i++) data.deeds[i] = deeds[i].Copy();
        var list = new List<SuspicionData>();
        foreach (KeyValuePair<string, int> pair in suspicion)
            if (pair.Value > 0) list.Add(new SuspicionData { who = pair.Key, level = pair.Value });
        list.Sort((a, b) => string.CompareOrdinal(a.who, b.who));
        data.suspicion = list.ToArray();
        return data;
    }
}
