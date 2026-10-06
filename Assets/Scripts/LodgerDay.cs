using UnityEngine;

// ---------------------------------------------------------------------------
// THE MAN AT THE BINS BY DAY: STALLING, NEVER A FAIL STATE (claude/night-0-and-the-favours-spec.md §5;
// claude/session-3-favours-stalling-officer.md §4)
//
// A night that ends without the favour he asked for that night is a skip (NightLedger.CameHome). By day:
//   * the morning after a skip (NightLedger.VisitDue), a quarter of the way through the day (the Build phase), he
//     comes into the café: on foot, in his own look, to a table. He sits for a minute, orders nothing, pays nothing
//     (PatronSpawner.SpawnTheMan), and says one line when Ace passes close (LodgerStory.VisitPool: warm, plain or
//     cold, by his warmth). Customers don't notice him; nothing touches stars, money or patience;
//   * the morning after the third skip in a row (NightLedger.NoteDue) there's no visit: a note on the counter, in his
//     hand, on screen once the day has opened, and kept in the notebook as one of his pages. He has stopped asking for
//     that favour; the next one comes up.
// Added to the patron spawner by itself (PatronSpawner.Awake). Nothing happens on a day with neither; at night it rests.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class LodgerDay : MonoBehaviour
{
    public static LodgerDay Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>How near Ace passes for his one line (metres).</summary>
    const float Passing = 2.6f;
    /// <summary>The note waits this long after the day opens (the morning's card goes first).</summary>
    const float NoteAfter = 2.5f;

    PatronSpawner spawner;
    int seenDay = -1;
    bool visitDue, noteDue;
    float noteAt;
    float nextLook;
    Transform ace;

    /// <summary>He's here today (his visit), or null: for the checks.</summary>
    public GameObject Visitor { get; private set; }
    /// <summary>He has said his line to Ace today.</summary>
    public bool SaidHisLine { get; private set; }
    /// <summary>His note was shown today.</summary>
    public bool NoteShown { get; private set; }
    /// <summary>His visit is still to come today.</summary>
    public bool VisitPending => visitDue;

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
            noteDue = ledger.NoteDue(seenDay);
            noteAt = Time.time + NoteAfter;
            Visitor = null;
            SaidHisLine = NoteShown = false;
        }
        if (NightWalk.Instance != null && NightWalk.Instance.Active) return;

        if (noteDue && Time.time >= noteAt && clock.IsOpen && !ConversationController.AnyOpen)
        {
            noteDue = false;
            ShowTheNote(ledger);
        }

        if (visitDue && clock.IsOpen && clock.NormalizedDay >= LodgerStory.VisitFrom && WaitingArea.FreeSeats > 0)
        {
            Visitor = spawner.SpawnTheMan(NightZeroSet.Instance != null ? NightZeroSet.Instance.look : null, LodgerStory.VisitSeconds);
            if (Visitor != null) visitDue = false;
        }

        if (Visitor != null && !SaidHisLine && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + .25f;
            SayHisLine(ledger);
        }
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

    void ShowTheNote(NightLedger ledger)
    {
        LodgerStory.Favour favour = LodgerStory.FindFavour(ledger.NoteFavour);
        if (favour == null || string.IsNullOrWhiteSpace(favour.note)) return;
        NotebookHooks.FoundHisNote(favour.id);
        Sfx.Play2D("notebook.page");
        NightCycle.Note($"A note on the counter, in his hand: \"{favour.note}\" ({ControlHints.NotebookPage}: his pages)", 9f);
        NoteShown = true;
    }

    public string Describe() =>
        $"The man by day (Day {seenDay}): visit {(Visitor != null ? "here" + (SaidHisLine ? ", his line said" : "") : visitDue ? "to come" : "none")}; " +
        $"note {(NoteShown ? "shown" : noteDue ? "to come" : "none")}.";
}
