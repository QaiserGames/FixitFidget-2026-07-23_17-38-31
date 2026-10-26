// ---------------------------------------------------------------------------
// Where the café's day feeds Ace's notebook (claude/night-notebook-spec.md).
// Things Ace has just been told at the counter:
//   * a regular's intake: Grace's camera request (Day 1's clean visit only, since 14 Oct), or any other regular's repair;
//   * Grace's thanks when Ace takes the camera (the rest of her story, since the dialogue pass; Day 1's only);
//   * Grace's return, once her job is accepted and she leaves the print;
//   * something Ace asked about (a topic in the reply list), or heard while they waited;
//   * since 24 Oct 2026 (Days), a seated guest's gossip (GuestGossip, Designer P's §2.4): overheard, filed under who it's about.
// And, since night step 3, one thing Ace has seen: a regular coming out of or
// going into their own front door (claude/night-homes-spec.md).
// Walk-ins are anonymous by design and never get entries. Without a
// SaveManager (a bare test scene) nothing is recorded.
// ---------------------------------------------------------------------------
public static class NotebookHooks
{
    public static int Today => DayClock.Instance != null ? DayClock.Instance.Day : 0;

    /// <summary>The player has just heard <paramref name="identity"/>'s intake for <paramref name="job"/>.</summary>
    public static int HeardIntake(CustomerIdentity identity, Job job)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || identity == null || !identity.IsRegular || identity.Profile == null) return 0;

        int learned = 0;
        if (identity.IsGraceCameraRequest)
        {
            // Her request: the strap and whose it was. The rest comes with her thanks (HeardThanks). Days 1-3 (14 Oct, R2):
            // only on the clean visit (Day 1), where she says it; Day 2's shutter visit adds nothing (a player who never
            // heard Day 1 would otherwise learn "Family reunion tomorrow" on the reunion day).
            if (identity.GraceVisit == GraceCameraVisit.Clean)
                foreach (NotebookFactData fact in NotebookEntries.GraceIntakeSaid(identity.DisplayName))
                    if (notebook.Learn(fact, Today)) learned++;
        }
        else if (job != null && job.kind == JobKind.Repair)
        {
            NotebookFactData fact = NotebookEntries.RegularRepair(identity.Profile.PersistentId, identity.DisplayName,
                job.deviceName, job.faultDescription);
            if (fact != null && notebook.Learn(fact, Today)) learned++;
        }
        return learned;
    }

    /// <summary>
    /// Ace has just taken <paramref name="identity"/>'s job and heard their thanks. For Grace's camera that
    /// is the rest of her story: the reunion, and that she usually stays behind the camera. The number of
    /// new facts.
    /// </summary>
    public static int HeardThanks(CustomerIdentity identity)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || identity == null || !identity.IsRegular || !identity.IsGraceCameraRequest) return 0;
        // Days 1-3 (R2): her thanks on Day 1's clean visit tells the reunion; Day 2's ("my nephew takes the pictures
        // tonight") adds nothing the notebook keeps.
        if (identity.GraceVisit != GraceCameraVisit.Clean) return 0;
        int learned = 0;
        foreach (NotebookFactData fact in NotebookEntries.GraceThanksSaid(identity.DisplayName))
            if (notebook.Learn(fact, Today)) learned++;
        return learned;
    }

    /// <summary>
    /// Ace has just seen <paramref name="identity"/> come out of (or go into)
    /// their own front door (night step 3; CafeArrivals watches for it). True when
    /// the notebook changed: a new fact, or Ace became surer of the old one.
    /// </summary>
    public static bool SawAtHome(CustomerIdentity identity, HomeDoor home, bool cameOut)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || home == null || identity == null || !identity.IsRegular || identity.Profile == null) return false;
        string who = identity.Profile.PersistentId;
        NotebookFactData known = notebook.Find(who + ".home");
        string before = known?.sure;
        NotebookFactData fact = NotebookEntries.HomeSeen(who, identity.DisplayName, home.looks, home.houseNumber, home.streetId,
            cameOut, HomeRules.SightingSureness(known, Today));
        if (fact == null) return false;
        return notebook.Learn(fact, Today) || known != null && known.sure != before;
    }

    // ---------- the Night 1 slice (NightThings: the words are placeholders) ----------

    /// <summary>A regular has just told Ace about a thing of theirs (the day's mention).</summary>
    public static bool HeardMention(string ownerName, NightThing thing)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NightThings.Mentioned(thing, ownerName);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }

    /// <summary>
    /// The morning after, the owner has just told Ace about <paramref name="thing"/> for the first time
    /// (Ace took it without having heard of it): what they said then.
    /// </summary>
    public static bool HeardComplaint(string ownerName, NightThing thing)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NightThings.Complained(thing, ownerName);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }

    /// <summary>
    /// The man at the bins told Ace about <paramref name="thing"/> (his ask: Grace's cups, and where they are), under its
    /// owner's name. True when it's new to the notebook.
    /// </summary>
    public static bool HeardFromHim(NightThing thing)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || thing == null || notebook.Knows(thing.id)) return false;
        NotebookFactData fact = NightThings.Mentioned(thing, NameOf(notebook, thing.owner));
        return fact != null && notebook.Learn(fact, Today);
    }

    /// <summary>The officer asked Ace a question (OfficerStory): his description of the man; and, after a flinch, that he noticed.</summary>
    public static bool AskedBy(string name, OfficerStory.Question question, bool flinched)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || question == null) return false;
        bool learned = false;
        NotebookFactData asked = OfficerStory.Asked(question, name);
        if (asked != null) learned |= notebook.Learn(asked, Today);
        NotebookFactData noticed = flinched ? OfficerStory.Flinched(question, name) : null;
        if (noticed != null) learned |= notebook.Learn(noticed, Today);
        return learned;
    }

    /// <summary>His note on the counter (the third skip): kept as one of his pages.</summary>
    public static bool FoundHisNote(string favour)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData page = LodgerStory.NotePage(favour);
        return notebook != null && page != null && notebook.Learn(page, Today);
    }

    /// <summary>Ace took <paramref name="thing"/> at night: Ace's own secret (<paramref name="forHim"/>: for the man at the bins).</summary>
    public static bool TookAtNight(NightThing thing, bool forHim = false)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NightThings.Taken(thing, NameOf(notebook, thing != null ? thing.owner : null), forHim);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }

    /// <summary>Ace cracked when the owner told the story: they say they'll be watching.</summary>
    public static bool Suspects(string ownerName, NightThing thing)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NightThings.Suspects(thing, ownerName);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }

    // What Ace already calls this person in the notebook ("Grace"), or their id.
    private static string NameOf(Notebook notebook, string who)
    {
        if (notebook != null && !string.IsNullOrEmpty(who))
            foreach (NotebookFactData fact in notebook.Facts)
                if (fact.who == who && !string.IsNullOrWhiteSpace(fact.name)) return fact.name;
        return who == NotebookEntries.GraceId ? NotebookEntries.GraceName : who;
    }

    /// <summary>
    /// 24 Oct 2026 (Days): a seated guest has just told Ace a line of gossip (GuestGossip; NotebookEntries' gossip table): the
    /// fact, overheard and likely, under the person it's about (never the guest: walk-ins and patrons never get entries).
    /// True when it's new to the notebook.
    /// </summary>
    public static bool HeardGossip(string id)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NotebookEntries.Overheard(NotebookEntries.GossipFor(id));
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }

    /// <summary>Grace's return has been accepted and her photo outcome settled.</summary>
    public static bool GraceReturned(string name, GracePhotoOutcome outcome)
    {
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        NotebookFactData fact = NotebookEntries.GraceReturn(name, outcome);
        return notebook != null && fact != null && notebook.Learn(fact, Today);
    }
}
