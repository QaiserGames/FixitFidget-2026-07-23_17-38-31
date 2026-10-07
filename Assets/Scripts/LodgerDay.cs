using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS BY DAY: STALLING COSTS THE DAY, NEVER A FAIL STATE (claude/night-0-and-the-favours-spec.md §5;
// claude/session-3-favours-stalling-officer.md §4; playtest 3, 6 Oct 2026: claude/playtest-3-notes-and-plan.md §5.2)
//
// A night that ends without the favour he asked for that night is a skip (NightLedger.CameHome). The morning after costs
// the day, by skips in a row, and stops the morning after the favour is done:
//   * every skip (NightLedger.VisitDue): he takes a table from opening until about 1:30 PM (LodgerStory.VisitFrom and
//     VisitUntil), on foot, in his own look, orders nothing, pays nothing (PatronSpawner.SpawnTheMan), and says one line
//     when Ace passes close (LodgerStory.VisitPool: warm, plain or cold, by his warmth). A seat out of play all morning.
//     On a morning without his mess (the first skip), a note says so once he's sat down (LodgerStory.TableNote): a seat
//     gone is easy to miss, and the first cost has to be seen to teach anything;
//   * the second in a row and on (MessDue): the café opens with his mess, a used cup at every table seat near the door
//     (DirtyCup), each seat out of play until Ace clears its table: one press a table (MessTable), and a badge over each
//     table until then (Juice.Mark). One note at opening says so, and whose it is (LodgerStory.MessNote);
//   * the third in a row, once a favour (WordDue, NoteDue): a one-star review under a name that isn't anyone's counts in
//     the day (ReputationLedger.Record; -2 reputation, never a star back); the officer comes in that day with a harder
//     question (CustomerSpawner, OfficerStory's word); and his note is on the counter, kept in the notebook as one of his
//     pages.
// Nothing is money, nothing is permanent, nothing ends; customers never notice him. Added to the patron spawner by
// itself (PatronSpawner.Awake). Nothing happens on a day with no cost due; at night it rests.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class LodgerDay : MonoBehaviour
{
    public static LodgerDay Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>How near Ace passes for his one line (metres).</summary>
    const float Passing = 2.6f;
    /// <summary>The mess note waits this long after the day opens (the morning's card goes first), and his note a beat after it.</summary>
    const float NoteAfter = 2.5f, SecondNoteAfter = 8f;

    PatronSpawner spawner;
    int seenDay = -1;
    bool visitDue, messDue, wordDue, noteDue, tableNoteDue, opened;
    float openedAt;
    float nextLook;
    Transform ace;
    readonly List<DirtyCup> cups = new List<DirtyCup>();
    readonly List<MessTable> tables = new List<MessTable>();

    /// <summary>He's here today (his visit), or null: for the checks.</summary>
    public GameObject Visitor { get; private set; }
    /// <summary>He has said his line to Ace today.</summary>
    public bool SaidHisLine { get; private set; }
    /// <summary>His note was shown today.</summary>
    public bool NoteShown { get; private set; }
    /// <summary>His visit is still to come today.</summary>
    public bool VisitPending => visitDue;
    /// <summary>His cups laid at opening today, and how many are still on the tables.</summary>
    public int CupsLaid { get; private set; }
    public int CupsLeft
    {
        get
        {
            int n = 0;
            foreach (DirtyCup cup in cups) if (cup != null && cup.OnTheTable) n++;
            return n;
        }
    }
    /// <summary>The tables his cups were laid on today (each cleared with one press), and how many still have them.</summary>
    public IReadOnlyList<MessTable> Tables => tables;
    public int TablesLeft
    {
        get
        {
            int n = 0;
            foreach (MessTable table in tables) if (!table.Cleared) n++;
            return n;
        }
    }
    /// <summary>The note that he's taken a table was put up today (a visit without the mess).</summary>
    public bool TableNoteShown { get; private set; }
    /// <summary>His review was planted today.</summary>
    public bool ReviewPlanted { get; private set; }

    void Awake()
    {
        Instance = this;
        spawner = GetComponent<PatronSpawner>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        DayClock clock = DayClock.Instance;
        NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (clock == null || ledger == null || spawner == null) return;
        if (clock.Day != seenDay)
        {
            seenDay = clock.Day;
            visitDue = ledger.VisitDue(seenDay) && NightZeroSet.Instance != null;
            messDue = ledger.MessDue(seenDay);
            wordDue = ledger.WordDue(seenDay);
            noteDue = ledger.NoteDue(seenDay);
            tableNoteDue = visitDue && !messDue;
            opened = false;
            Visitor = null;
            SaidHisLine = NoteShown = ReviewPlanted = TableNoteShown = false;
            CupsLaid = 0;
            PutTheCupsAway();
        }
        if (NightWalk.Instance != null && NightWalk.Instance.Active)
        {
            if (cups.Count > 0) PutTheCupsAway();   // the day is over: whatever he left is cleared with the shop
            return;
        }
        if (!clock.IsOpen) return;

        // The day opens: his mess on the tables, his review in the day, and the notes (one a beat).
        if (!opened)
        {
            opened = true;
            openedAt = Time.time;
            if (messDue) LayTheMess(messDue && noteDue);
            if (wordDue) PlantTheReview();
        }
        if (noteDue && Time.time >= openedAt + (messDue ? SecondNoteAfter : NoteAfter) && !ConversationController.AnyOpen)
        {
            noteDue = false;
            ShowTheNote(ledger);
        }

        if (visitDue && clock.NormalizedDay >= LodgerStory.VisitFrom && WaitingArea.FreeSeats > 0)
        {
            Visitor = spawner.SpawnTheMan(NightZeroSet.Instance != null ? NightZeroSet.Instance.look : null, StaySeconds(clock), LodgerStory.VisitUntil);
            if (Visitor != null) visitDue = false;
        }

        if (Visitor != null && (!SaidHisLine || tableNoteDue) && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + .25f;
            if (tableNoteDue) ShowTheTableNote();
            if (!SaidHisLine) SayHisLine(ledger);
        }
    }

    // Once he's sat down, on a morning without his mess: the note that he's taken a table (not over a conversation, and
    // after any note already up: one note a beat).
    void ShowTheTableNote()
    {
        PatronBrain brain = Visitor.GetComponent<PatronBrain>();
        if (brain == null || !brain.IsSeated || ConversationController.AnyOpen) return;
        tableNoteDue = false;
        NightCycle.NoteThen(LodgerStory.TableNote, 0f, 6f);
        TableNoteShown = true;
    }

    // From now until LodgerStory.VisitUntil of the day, in seconds (the day's length comes from what's left of it): the
    // fallback for a patron brain that can't watch the clock (he watches it: PatronBrain.StayUntil).
    static float StaySeconds(DayClock clock)
    {
        float left = 1f - clock.NormalizedDay;
        float dayLength = left > .001f ? clock.TimeRemaining / left : 0f;
        return Mathf.Max(20f, (LodgerStory.VisitUntil - clock.NormalizedDay) * dayLength);
    }

    // Once, when Ace passes close while he sits: one line, in his tone.
    void SayHisLine(NightLedger ledger)
    {
        PatronBrain brain = Visitor.GetComponent<PatronBrain>();
        if (brain == null || !brain.IsSeated) return;
        if (ace == null)
        {
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
            ace = player != null ? player.transform : null;
        }
        if (ace == null) return;
        Vector3 d = ace.position - Visitor.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > Passing * Passing) return;
        SaidHisLine = Barks.SayFrom(Visitor.transform, LodgerStory.SpeakerId, LodgerStory.VisitPool(ledger.Warmth));
    }

    // His mess: a used cup at every table seat (up to LodgerStory.MessCupsAtMost, the ones nearest the door first), sorted
    // into tables with a badge over each, and the note. With his counter note due too, the mess note goes first and his a
    // beat later (one note a beat).
    void LayTheMess(bool hisNoteFollows)
    {
        PutTheCupsAway();
        var seats = new List<TableSeat>(FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude));
        seats.RemoveAll(s => s == null || !s.isActiveAndEnabled || s.IsDirty || s.Occupant != null);
        Vector3 door = spawner.transform.position;
        seats.Sort((a, b) => (a.transform.position - door).sqrMagnitude.CompareTo((b.transform.position - door).sqrMagnitude));
        for (int i = 0; i < seats.Count && cups.Count < LodgerStory.MessCupsAtMost; i++)
        {
            DirtyCup cup = DirtyCup.Place(seats[i]);
            if (cup != null) cups.Add(cup);
        }
        CupsLaid = cups.Count;
        tables.AddRange(MessTable.Group(cups));
        foreach (MessTable table in tables) table.ShowBadge();
        if (CupsLaid > 0) NightCycle.NoteThen(LodgerStory.MessNote, NoteAfter, hisNoteFollows ? 5f : 7f);
    }

    void PutTheCupsAway()
    {
        foreach (MessTable table in tables) table.Clear(false);
        foreach (DirtyCup cup in cups) if (cup != null && cup.OnTheTable) cup.Remove();
        tables.Clear();
        cups.Clear();
    }

    // His review: one star, under a name that isn't anyone's, in the day's reviews (counted at closing like the rest).
    void PlantTheReview()
    {
        ReputationLedger rep = SaveManager.Instance != null ? SaveManager.Instance.Reputation : null;
        if (rep == null) return;
        ReviewPlanted = rep.Record(new ReviewEntry
        {
            review = Review.NeverAgain,
            reason = ReviewReason.Planted,
            name = LodgerStory.PlantedName,
            line = LodgerStory.PlantedReview,
        });
    }

    void ShowTheNote(NightLedger ledger)
    {
        LodgerStory.Favour favour = LodgerStory.FindFavour(ledger.NoteFavour);
        if (favour == null || string.IsNullOrWhiteSpace(favour.note)) return;
        NotebookHooks.FoundHisNote(favour.id);
        Sfx.Play2D("notebook.page");
        NightCycle.Note($"A note on the counter: \"{favour.note}\"", 8f);
        NoteShown = true;
    }

    public string Describe() =>
        $"The man by day (Day {seenDay}): table {(Visitor != null ? "taken" + (SaidHisLine ? ", his line said" : "") : visitDue ? "to come" : "none")}; " +
        $"mess {(CupsLaid > 0 ? $"{CupsLeft} of {CupsLaid} cups left, on {TablesLeft} of {tables.Count} tables" : messDue ? "to come" : "none")}; " +
        $"table note {(TableNoteShown ? "shown" : tableNoteDue ? "to come" : "none")}; " +
        $"review {(ReviewPlanted ? "planted" : wordDue && !opened ? "to come" : "none")}; note {(NoteShown ? "shown" : noteDue ? "to come" : "none")}.";
}
