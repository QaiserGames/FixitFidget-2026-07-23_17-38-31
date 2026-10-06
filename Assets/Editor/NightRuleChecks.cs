using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for the Night 1 slice (claude/ace-after-dark.md §3.2): the night's ledger (trophies,
// the mornings still to come, suspicion), the straight-face meter, and the gnome's words. Pure: no
// scene, no save file, no assets. Also compiled by Tests/NightRules.
// Since 6 Oct, the man at the bins (claude/the-man-at-the-bins-story.md): meeting him, his warmth, his lessons
// (Nerve widens the straight face), what Ace gives him (his corner, off Ace's shelf), his pages and the errand.
// Session 3 (claude/session-3-favours-stalling-officer.md): his favours in order, what he says each night, skips
// (the visit, the note, a favour dropped), nights off, his verdicts, Grace's cups, and the officer's question.
public static class NightRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Night 1 rules")]
    public static void Run()
    {
        int count = RunAll() + CheckUnityJson();
        Debug.Log("[Night 1 rules] PASS: " + count + " assertions. No scene, save or asset changes.");
    }

    // Unity's own serializer, which is what the real save uses.
    static int CheckUnityJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        var ledger = new NightLedger();
        ledger.Take(NightThings.GraceGnome, "grace", 1);
        ledger.Faced(ledger.Unfaced("grace", 2), true, 2);
        ledger.CameHome();
        var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { day = 2, night = ledger.Snapshot() }));
        back.ValidateAndMigrate();
        var again = new NightLedger();
        again.Restore(back.night);
        Check(again.HasTrophy(NightThings.GraceGnome) && again.Suspicion("grace") == 1 && again.Nights == 1
              && again.Deeds.Count == 1 && again.Deeds[0].faced && again.Deeds[0].cracked && again.Deeds[0].facedDay == 2,
            "The night's record survives Unity's JSON.");
        var old = JsonUtility.FromJson<SaveData>("{\"version\":5,\"day\":4}");
        old.ValidateAndMigrate();
        Check(old.night != null && old.night.trophies.Length == 0 && old.night.deeds.Length == 0 && old.night.nights == 0,
            "A save from before the nights loads with an empty record.");
        // The man at the bins, through Unity's JSON too.
        var met = new NightLedger();
        met.Meet();
        met.Warm(2);
        met.Learn(LodgerStory.Nerve);
        met.Take(NightThings.GraceGnome, "grace", 1);
        met.Give(NightThings.GraceGnome);
        var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { day = 2, night = met.Snapshot() }));
        round.ValidateAndMigrate();
        var metAgain = new NightLedger();
        metAgain.Restore(round.night);
        Check(metAgain.MetHim && metAgain.Warmth == 2 && metAgain.Knows(LodgerStory.Nerve) && metAgain.HasGiven(NightThings.GraceGnome)
              && !metAgain.OnShelf(NightThings.GraceGnome), "The man at the bins survives Unity's JSON: met, warmth, lessons, his corner.");
        Check(!old.night.metHim && old.night.warmth == 0 && old.night.lessons.Length == 0 && old.night.given.Length == 0,
            "A save from before him: not met, no warmth, no lessons, nothing given.");
        // His favours and the officer's question, through Unity's JSON too (session 3).
        NightLedger favours = Favoured();
        var favoursBack = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { day = 4, night = favours.Snapshot() }));
        favoursBack.ValidateAndMigrate();
        var favoursAgain = new NightLedger();
        favoursAgain.Restore(favoursBack.night);
        Check(SameFavours(favours, favoursAgain), "His favours, the skips, the visit and note days and the officer's question survive Unity's JSON.");
        Check(old.night.favour == "" && old.night.skips == 0 && old.night.visitDay == 0 && old.night.dropped.Length == 0 && old.night.questions.Length == 0,
            "A save from before the favours: none of them.");
        return n;
    }
#endif

    /// <summary>A ledger part way through the favours (for the save round trips): the gnome given, the cups asked and skipped once,
    /// the officer's question flinched at.</summary>
    public static NightLedger Favoured()
    {
        var ledger = new NightLedger();
        ledger.Meet(1);
        ledger.Take(NightThings.GraceGnome, GraceCameraEpisode.ProfileId, 1);
        ledger.Give(NightThings.GraceGnome);
        ledger.CameHome(1);
        ledger.Ask(2);
        ledger.CameHome(2);
        ledger.Questioned("officer.description", OfficerStory.ProfileId, 3, true);
        return ledger;
    }

    /// <summary>Two ledgers agree on his favours and the questions.</summary>
    public static bool SameFavours(NightLedger a, NightLedger b) =>
        a.Favour == b.Favour && a.AskedOn == b.AskedOn && a.LastAsked == b.LastAsked && a.Skips == b.Skips && a.VisitDay == b.VisitDay
        && a.NoteDay == b.NoteDay && a.NoteFavour == b.NoteFavour && a.Dropped.Count == b.Dropped.Count && a.Questions.Count == b.Questions.Count
        && (a.Questions.Count == 0 || a.Questions[0].id == b.Questions[0].id && a.Questions[0].cracked == b.Questions[0].cracked
            && a.Questions[0].day == b.Questions[0].day && a.Questions[0].who == b.Questions[0].who)
        && a.Suspicion(OfficerStory.ProfileId) == b.Suspicion(OfficerStory.ProfileId);

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }

        // ---------- the ledger ----------
        var ledger = new NightLedger();
        int changes = 0;
        ledger.Changed += () => changes++;
        Check(ledger.Trophies.Count == 0 && ledger.Unfaced("grace", 5) == null && ledger.Nights == 0, "A new ledger is empty.");
        Check(ledger.Take("grace.gnome", "grace", 1), "Taking a thing is new the first time.");
        Check(!ledger.Take("grace.gnome", "grace", 2), "A thing can only be taken once.");
        Check(!ledger.Take("", "grace", 1) && !ledger.Take(null, "grace", 1), "Nothing without an id is taken.");
        Check(ledger.HasTrophy("grace.gnome") && ledger.Trophies.Count == 1 && changes == 1, "The trophy is on the shelf, and the shelf heard once.");
        Check(ledger.Unfaced("grace", 1) == null, "The owner can't tell Ace about it on the night it happened.");
        NightDeedData deed = ledger.Unfaced("grace", 2);
        Check(deed != null && deed.thing == "grace.gnome" && deed.night == 1, "The next morning, Grace has something to tell Ace.");
        Check(ledger.Unfaced("thomas", 2) == null && ledger.Unfaced(null, 2) == null, "Only the owner has.");
        Check(ledger.OwnersDue(2).Count == 1 && ledger.OwnersDue(2)[0] == "grace" && ledger.OwnersDue(1).Count == 0,
            "Who is due in the morning follows the same rule.");
        Check(ledger.Faced(deed, false, 2) && deed.faced && !deed.cracked && deed.facedDay == 2 && ledger.Suspicion("grace") == 0,
            "A straight face costs nothing.");
        Check(!ledger.Faced(deed, true, 3) && ledger.Suspicion("grace") == 0, "Each deed is faced once.");
        Check(ledger.Unfaced("grace", 9) == null && ledger.OwnersDue(9).Count == 0, "Nothing is left to tell once it's been told.");

        var cracked = new NightLedger();
        cracked.Take("grace.gnome", "grace", 1);
        cracked.Take("grace.birdbath", "grace", 3);
        Check(cracked.Unfaced("grace", 2)?.thing == "grace.gnome", "The oldest deed is told first.");
        cracked.Faced(cracked.Unfaced("grace", 2), true, 2);
        Check(cracked.Suspicion("grace") == 1 && cracked.Unfaced("grace", 3) == null && cracked.Unfaced("grace", 4)?.thing == "grace.birdbath",
            "A crack makes the owner suspicious; the next deed waits for its own morning.");
        cracked.Faced(cracked.Unfaced("grace", 4), true, 4);
        Check(cracked.Suspicion("grace") == 2 && cracked.Suspicion("thomas") == 0, "Suspicion adds up, per person.");
        Check(!cracked.Faced(new NightDeedData { thing = "x", owner = "grace" }, true, 5) && cracked.Suspicion("grace") == 2,
            "A deed the ledger doesn't hold changes nothing.");

        cracked.CameHome();
        cracked.CameHome();
        NightSaveData saved = cracked.Snapshot();
        Check(saved.nights == 2 && saved.trophies.Length == 2 && saved.deeds.Length == 2 && saved.suspicion.Length == 1
              && saved.suspicion[0].who == "grace" && saved.suspicion[0].level == 2, "The snapshot holds everything.");
        saved.deeds[0].faced = false;
        Check(cracked.Deeds[0].faced, "The snapshot is a copy: changing it doesn't change the ledger.");
        var restored = new NightLedger();
        int restores = 0;
        restored.Changed += () => restores++;
        restored.Restore(cracked.Snapshot());
        Check(restored.Nights == 2 && restored.HasTrophy("grace.birdbath") && restored.Suspicion("grace") == 2
              && restored.Deeds.Count == 2 && restores == 1, "A restore brings it all back and tells the shelves once.");
        restored.Restore(new NightSaveData
        {
            nights = -3,
            trophies = new[] { "a", "a", "", null },
            deeds = new[] { new NightDeedData { thing = "a", owner = "grace" }, new NightDeedData { thing = "a" }, null, new NightDeedData() },
            suspicion = new[] { new SuspicionData { who = "grace", level = 0 }, null, new SuspicionData { who = "", level = 3 } },
        });
        Check(restored.Nights == 0 && restored.Trophies.Count == 1 && restored.Deeds.Count == 1 && restored.Suspicion("grace") == 0,
            "A damaged save's repeats, blanks and nonsense are dropped.");
        restored.Restore(null);
        Check(restored.Trophies.Count == 0 && restored.Deeds.Count == 0 && restored.Nights == 0, "Restoring nothing empties the ledger.");

        // ---------- the save ----------
        var data = new SaveData { day = 2, dayCompleted = true, recap = new RecapSaveData { day = 2 }, night = cracked.Snapshot() };
        Check(data.TryCreateNextDay(out SaveData next) && next.night != null && next.night.trophies.Length == 2 && next.day == 3,
            "Tomorrow's checkpoint carries the night's record.");
        var blank = new SaveData { night = null };
        blank.ValidateAndMigrate();
        Check(blank.night != null && blank.night.deeds.Length == 0, "A save without the record gets an empty one.");

        // ---------- the meter ----------
        var meter = new StraightFaceMeter(1f, .2f, .03f, 6f, .5f);
        Check(Math.Abs(meter.GreenLeft - .4f) < 1e-4f && Math.Abs(meter.GreenRight - .6f) < 1e-4f, "The green sits where it's put.");
        Check(meter.Needle == 0f && !meter.Stopped, "The needle starts at the left, running.");
        meter.Tick(.5f);
        Check(Math.Abs(meter.Needle - .5f) < 1e-4f && meter.Stop() && meter.Held && meter.Stopped, "Stopped in the green: held.");
        meter.Tick(.3f);
        Check(Math.Abs(meter.Needle - .5f) < 1e-4f, "A stopped needle stays put.");
        Check(meter.Stop() && meter.Held, "Only the first stop counts.");

        var miss = new StraightFaceMeter(1f, .2f, .03f, 6f, .5f);
        miss.Tick(.2f);
        Check(!miss.Stop() && !miss.Held && !miss.TimedOut, "Stopped far from the green: cracked.");

        var near = new StraightFaceMeter(1f, .2f, .03f, 6f, .5f);
        near.Tick(.38f);
        Check(near.Stop(), "Stopped just short of the green: near enough holds.");
        var tooFar = new StraightFaceMeter(1f, .2f, .03f, 6f, .5f);
        tooFar.Tick(.36f);
        Check(!tooFar.Stop(), "A little further out doesn't.");

        var back = new StraightFaceMeter(1f, .2f, 0f, 6f, .5f);
        back.Tick(1.5f);
        Check(Math.Abs(back.Needle - .5f) < 1e-4f && back.Stop(), "The needle comes back: at 1.5 sweeps it's in the middle again.");
        var right = new StraightFaceMeter(1f, .2f, 0f, 6f, .5f);
        right.Tick(1f);
        Check(Math.Abs(right.Needle - 1f) < 1e-4f && !right.Stop(), "At the right end after one sweep.");

        var slow = new StraightFaceMeter(1f, .2f, .03f, 6f, .5f);
        for (int i = 0; i < 59; i++) slow.Tick(.1f);
        Check(!slow.Stopped, "Still running just before the patience runs out.");
        slow.Tick(.2f);
        Check(slow.Stopped && slow.TimedOut && !slow.Held && !slow.Stop(), "Never stopped: Ace cracks when the patience runs out.");

        var odd = new StraightFaceMeter(float.NaN, 5f, -1f, 0f, 2f);
        Check(odd.SweepSeconds >= .2f && odd.Green <= .9f && odd.Near >= 0f && odd.Patience >= 1f
              && odd.GreenLeft >= 0f && odd.GreenRight <= 1f, "Nonsense settings are made safe.");
        odd.Tick(float.NaN);
        odd.Tick(-1f);
        odd.Tick(float.PositiveInfinity);
        Check(odd.Elapsed == 0f && !odd.Stopped, "Nonsense time moves nothing.");

        var rolls = new List<float>();
        var random = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            StraightFaceMeter rolled = StraightFaceMeter.Rolled(1.1f, .22f, .03f, 6f, random);
            rolls.Add(rolled.GreenCentre);
            Check(rolled.GreenCentre >= .25f - 1e-4f && rolled.GreenCentre <= .75f + 1e-4f && rolled.GreenLeft >= 0f && rolled.GreenRight <= 1f,
                "The green is rolled somewhere in the middle half.");
        }
        rolls.Sort();
        Check(rolls[199] - rolls[0] > .3f, "The green moves around from one morning to the next.");
        var sameA = StraightFaceMeter.Rolled(1.1f, .22f, .03f, 6f, new System.Random(3));
        var sameB = StraightFaceMeter.Rolled(1.1f, .22f, .03f, 6f, new System.Random(3));
        Check(sameA.GreenCentre == sameB.GreenCentre, "The same seed rolls the same green (its own random numbers).");
        Check(StraightFaceMeter.Rolled(1.1f, .22f, .03f, 6f, null).GreenCentre == .5f, "No random numbers: the green sits in the middle.");

        // An easy meter really is easy: a steady player gets two chances a sweep.
        NightThing gnome = NightThings.GnomeOfGrace;
        var easy = new StraightFaceMeter(gnome.sweepSeconds, gnome.green, gnome.near, gnome.patience, .5f);
        float window = (easy.Green + 2f * easy.Near) * easy.SweepSeconds;
        Check(window >= .25f && window <= .5f, $"The gnome's window is a fair {window:0.00} s each pass.");
        Check(gnome.patience >= 3f * gnome.sweepSeconds, "There's time for a few sweeps before Ace cracks anyway.");

        // ---------- the gnome's words (placeholders, but never missing) ----------
        Check(NightThings.Find(NightThings.GraceGnome) == gnome && NightThings.OwnedBy("grace") == gnome
              && NightThings.Find("nope") == null && NightThings.Find(null) == null && NightThings.OwnedBy("") == null,
            "The gnome is found by its id and by its owner.");
        Check(gnome.owner == GraceCameraEpisode.ProfileId && gnome.id == "grace.gnome", "It's Grace's, and its id is the saved one.");
        foreach (string line in new[] { gnome.name, gnome.unknownName, gnome.topic, gnome.mention, gnome.waitingMention, gnome.complaint,
                     gnome.held, gnome.cracked, gnome.takenNote, gnome.takenNoteUnknown, gnome.notebookMention, gnome.notebookComplaint,
                     gnome.notebookTaken, gnome.notebookCracked })
            Check(!string.IsNullOrWhiteSpace(line), "Every line is written.");
        Check(gnome.mention.Contains(gnome.name) && gnome.complaint.Contains(gnome.name), "The day and the morning after name the same gnome.");
        // The dialogue pass: Ace asks about it (a short reply), and if Ace never does, she mentions it while she waits.
        Check(gnome.topic.Length <= 30 && gnome.waitingMention.Contains(gnome.name) && !gnome.waitingMention.Contains("\n"),
            "Ace's question is a short reply; her waiting line names the gnome in one line.");
        foreach (string line in new[] { gnome.mention, gnome.waitingMention, gnome.complaint, gnome.held, gnome.cracked })
        {
            Check(!line.Contains("\n\n") && !line.StartsWith("\n") && !line.EndsWith("\n"), "Separate lines, never a paragraph.");
            foreach (string beat in line.Split('\n'))
                Check(beat.Trim().Length > 0 && beat.Length <= 150, $"Each line is short (\"{beat}\").");
        }
        foreach (NightThing thing in NightThings.All)
            Check(!string.IsNullOrEmpty(thing.id) && NightThings.Find(thing.id) == thing, "Every thing's id is unique and found.");

        // ---------- the notebook's shorthand ----------
        NotebookFactData mentioned = NightThings.Mentioned(gnome, "Grace");
        NotebookFactData taken = NightThings.Taken(gnome, "Grace");
        NotebookFactData suspects = NightThings.Suspects(gnome, null);
        Check(mentioned.id == "grace.gnome" && mentioned.who == "grace" && mentioned.name == "Grace"
              && mentioned.kind == Notebook.Kinds.Possession && mentioned.source == Notebook.Sources.Told && mentioned.sure == Notebook.Sureness.Sure,
            "What she said about it is something she told Ace.");
        Check(taken.id == "grace.gnome.taken" && taken.kind == Notebook.Kinds.Secret && taken.source == Notebook.Sources.Found,
            "Taking it is Ace's own secret, found at night.");
        Check(suspects.id == "grace.gnome.suspects" && suspects.name == "grace" && suspects.kind == Notebook.Kinds.Claim,
            "A crack is written down as what she said.");
        Check(NightThings.Mentioned(null, "Grace") == null && NightThings.Taken(null, null) == null
              && NightThings.Complained(null, "Grace") == null, "No thing, no fact.");
        // Taken before Ace had heard of it: the morning's complaint is how Ace learns whose it was, and the
        // notebook keeps what she said then (never the day's mention, which Ace didn't hear).
        NotebookFactData complained = NightThings.Complained(gnome, "Grace");
        Check(complained.id == mentioned.id && complained.who == "grace" && complained.name == "Grace"
              && complained.kind == Notebook.Kinds.Possession && complained.source == Notebook.Sources.Told
              && complained.text == gnome.notebookComplaint && complained.text != gnome.notebookMention,
            "Heard of only in the morning's complaint: the same fact, in the complaint's words.");
        var lateBook = new Notebook();
        Check(lateBook.Learn(complained, 3) && lateBook.Knows(gnome.id) && !lateBook.Learn(mentioned, 3)
              && lateBook.Find(gnome.id).text == gnome.notebookComplaint,
            "Once known from the complaint, the mention's words don't replace it.");
        var book = new Notebook();
        Check(book.Learn(mentioned, 1) && book.Learn(taken, 1) && !book.Learn(mentioned, 2) && book.Find("grace.gnome").confirmedDay == 2,
            "The notebook learns them like any other fact.");
        NotebookFactData takenFor = NightThings.Taken(gnome, "Grace", forHim: true);
        Check(takenFor.id == taken.id && takenFor.text == gnome.notebookTakenFor && takenFor.text != taken.text,
            "Taken for the man at the bins: the same secret, saying who it was for (not the shelf).");

        count += TheManAtTheBins();
        count += TheFavours();
        count += TheOfficer();
        return count;
    }

    // ---------- the man at the bins (6 Oct 2026) ----------
    static int TheManAtTheBins()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }

        var ledger = new NightLedger();
        int changes = 0;
        ledger.Changed += () => changes++;
        Check(!ledger.MetHim && ledger.Warmth == 0 && ledger.Lessons.Count == 0 && ledger.Given.Count == 0, "Nobody has met him yet.");
        Check(LodgerStory.Errand(ledger) == "", "No errand before the deal.");
        Check(ledger.Meet() && !ledger.Meet() && ledger.MetHim && changes == 1, "The deal is made once.");
        Check(LodgerStory.Errand(ledger) == NightThings.GraceGnome, "After the deal his errand is Grace's gnome.");

        // Warmth: nudged by Ace's replies, kept within the limits, never shown.
        Check(ledger.Warm(1) == 1 && ledger.Warm(-2) == -1, "A reply nudges his warmth up or down.");
        for (int i = 0; i < 20; i++) ledger.Warm(1);
        Check(ledger.Warmth == NightSaveData.MaxWarmth, "Warmth stops at the top.");
        for (int i = 0; i < 40; i++) ledger.Warm(-1);
        Check(ledger.Warmth == -NightSaveData.MaxWarmth, "And at the bottom.");
        int before = changes;
        ledger.Warm(-1);
        Check(changes == before, "A nudge that changes nothing tells nobody.");

        // Giving him something: only what Ace has taken, once; it leaves Ace's shelf for his corner.
        Check(!ledger.Give(NightThings.GraceGnome), "Ace can't give him what Ace hasn't taken.");
        ledger.Take(NightThings.GraceGnome, "grace", 1);
        Check(ledger.OnShelf(NightThings.GraceGnome), "Taken and not given: it's on Ace's shelf.");
        Check(ledger.Give(NightThings.GraceGnome) && !ledger.Give(NightThings.GraceGnome), "Given once.");
        Check(ledger.HasGiven(NightThings.GraceGnome) && !ledger.OnShelf(NightThings.GraceGnome) && ledger.HasTrophy(NightThings.GraceGnome),
            "Given: in his corner, not on Ace's shelf, and still Ace's deed.");
        Check(ledger.Unfaced("grace", 2) != null, "Grace still comes in the next morning: it was Ace who took it.");
        Check(LodgerStory.Errand(ledger) == "", "The errand is done once he has it.");

        // Lessons.
        Check(ledger.Learn(LodgerStory.Nerve) && !ledger.Learn(LodgerStory.Nerve) && ledger.Knows(LodgerStory.Nerve), "A lesson is learned once.");
        Check(!ledger.Learn("") && !ledger.Learn(null), "Nothing without an id is learned.");
        Check(LodgerStory.LessonFor(NightThings.GraceGnome) == LodgerStory.Nerve && LodgerStory.FindLesson(LodgerStory.Nerve) != null,
            "Bringing the gnome back teaches Nerve.");
        Check(LodgerStory.Green(.22f, false) == .22f && Math.Abs(LodgerStory.Green(.22f, true) - .22f * LodgerStory.NerveWidens) < 1e-5f,
            "Nerve widens the straight face's green.");
        Check(LodgerStory.Green(.5f, true) <= .6f && LodgerStory.Near(.15f, true) <= .2f, "Never so wide the meter is pointless.");
        var steady = new StraightFaceMeter(1f, LodgerStory.Green(.22f, true), LodgerStory.Near(.03f, true), 6f, .5f);
        var shaky = new StraightFaceMeter(1f, .22f, .03f, 6f, .5f);
        float edge = .5f + .22f * .5f + .03f + .01f;   // just outside the plain meter's "near enough"
        Check(steady.InGreen(edge) && !shaky.InGreen(edge), "With Nerve, a stop just outside the old green still holds.");

        // Round trip through the record.
        var back = new NightLedger();
        back.Restore(ledger.Snapshot());
        Check(back.MetHim && back.Warmth == ledger.Warmth && back.Knows(LodgerStory.Nerve) && back.HasGiven(NightThings.GraceGnome),
            "The record keeps all of it.");
        var odd = new NightSaveData { metHim = true, warmth = 99, lessons = new[] { "nerve", "", "nerve" }, given = new[] { NightThings.GraceGnome } };
        var oddBack = new NightLedger();
        oddBack.Restore(odd);
        Check(oddBack.Warmth == NightSaveData.MaxWarmth && oddBack.Lessons.Count == 1 && !oddBack.HasGiven(NightThings.GraceGnome),
            "A restored record is tidied: warmth in its limits, lessons once, nothing given that was never taken.");

        // His pages: the notebook's first pages, in his hand.
        var pages = new List<NotebookFactData>(LodgerStory.Pages());
        Check(pages.Count >= 3, "He hands over a few pages.");
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (NotebookFactData page in pages)
            Check(page.source == Notebook.Sources.Inherited && page.who == LodgerStory.PagesWho && page.sure == Notebook.Sureness.Sure
                  && !string.IsNullOrWhiteSpace(page.text) && pageIds.Add(page.id), "Each page is his (inherited), sure, and unique.");
        NotebookFactData thursdays = LodgerStory.PageFor(NightThings.GraceGnome);
        Check(thursdays != null && thursdays.source == Notebook.Sources.Inherited && !pageIds.Contains(thursdays.id),
            "The gnome pays a page of its own, not one of the first.");
        return count;
    }

    // ---------- his favours (session 3) ----------
    static int TheFavours()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }
        bool Everything(string id) => true;

        // The list: in order, each once; what a return needs is all there.
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (LodgerStory.Favour f in LodgerStory.Favours)
        {
            Check(!string.IsNullOrEmpty(f.id) && ids.Add(f.id) && LodgerStory.FindFavour(f.id) == f, $"Favour '{f.id}' is listed once and found.");
            Check(!string.IsNullOrEmpty(f.remind) && !string.IsNullOrEmpty(f.hint) && !string.IsNullOrWhiteSpace(f.note) && !string.IsNullOrEmpty(f.corner),
                $"Favour '{f.id}': the ask again, the night's note, his note on the counter and his corner are all named.");
            if (string.IsNullOrEmpty(f.returnScene)) continue;
            Check(!string.IsNullOrEmpty(f.takes) && !string.IsNullOrEmpty(f.sets) && !string.IsNullOrEmpty(f.lesson) && !string.IsNullOrEmpty(f.page),
                $"Favour '{f.id}': its return names the lines things happen on.");
            Check(LodgerStory.FindLesson(f.teaches) != null && LodgerStory.LessonFor(f.id) == f.teaches, $"Favour '{f.id}' teaches a lesson he knows.");
            NotebookFactData page = LodgerStory.PageFor(f.id);
            Check(page != null && page.source == Notebook.Sources.Inherited && page.who == LodgerStory.PagesWho && page.kind == Notebook.Kinds.Secret,
                $"Favour '{f.id}' pays a page: a secret, in his hand.");
            Check(NightThings.Find(f.id) != null, $"Favour '{f.id}' is a thing Ace can take at night.");
        }
        Check(LodgerStory.Favours[0].id == LodgerStory.FirstErrand && string.IsNullOrEmpty(LodgerStory.Favours[0].ask),
            "The first favour is the gnome, asked in the deal (no ask of its own).");
        Check(LodgerStory.NextFavour("", null) == NightThings.GraceGnome && LodgerStory.NextFavour(NightThings.GraceGnome, null) == NightThings.GraceCups
              && LodgerStory.NextFavour(NightThings.GraceCups, null) == LodgerStory.Cones && LodgerStory.NextFavour(LodgerStory.Cones, null) == "",
            "The favours come in order: the gnome, the cups, the cones, then none.");
        Check(LodgerStory.NextFavour("", id => id == NightThings.GraceGnome) == NightThings.GraceCups, "A favour done with is passed over.");
        Check(LodgerStory.NightOff(4) && LodgerStory.NightOff(7) && !LodgerStory.NightOff(2) && !LodgerStory.NightOff(3), "Nights 4 and 7 are off.");

        // The deal asks for the gnome; a night that ends without it is a skip.
        var ledger = new NightLedger();
        Check(ledger.Favour == "" && ledger.Errand == "" && !ledger.Ask(1), "Before the deal, no favour, nothing to ask.");
        ledger.Meet(1);
        Check(ledger.Favour == NightThings.GraceGnome && ledger.AskedOn == 1 && ledger.LastAsked == 1 && ledger.Errand == NightThings.GraceGnome,
            "The deal on Night 1 asks for the gnome.");
        ledger.CameHome(1);
        Check(ledger.Skips == 1 && ledger.VisitDay == 2 && ledger.VisitDue(2) && !ledger.VisitDue(3) && ledger.NoteDay == 0,
            "Night 1 ends without the gnome: a skip, and he sits in the café on Day 2.");
        Check(LodgerStory.WhatTonight(ledger, 2, Everything) == LodgerStory.Tonight.AskAgain, "On Night 2 he asks again, colder.");
        Check(ledger.Ask(2) && ledger.AskedOn == 1 && ledger.LastAsked == 2, "Asking again keeps the night he first asked.");
        ledger.Take(NightThings.GraceGnome, GraceCameraEpisode.ProfileId, 2);
        Check(ledger.Give(NightThings.GraceGnome) && ledger.Favour == NightThings.GraceCups && ledger.AskedOn == 0 && ledger.Skips == 0 && ledger.Errand == "",
            "Given: the cups come up, not asked yet, and the skips start again.");
        ledger.CameHome(2);
        Check(ledger.Skips == 0 && !ledger.VisitDue(3), "A night that ends with his favour given is no skip.");

        // The cups: the ask (warm: he says why), skips, a night off, the third skip drops it.
        Check(LodgerStory.WhatTonight(ledger, 3, Everything) == LodgerStory.Tonight.Ask, "Night 3 asks for the cups.");
        LodgerStory.Favour cups = LodgerStory.FindFavour(NightThings.GraceCups);
        Check(LodgerStory.AskScene(cups, 0) == cups.ask && LodgerStory.AskScene(cups, LodgerStory.WarmFrom) == cups.askWarm
              && LodgerStory.AskScene(LodgerStory.Favours[0], 5) == "", "Warm, he asks in his warm words; the gnome has no ask of its own.");
        ledger.Ask(3);
        Check(ledger.Errand == NightThings.GraceCups && ledger.AskedOn == 3, "Asked: the cups are Ace's errand.");
        ledger.CameHome(3);
        Check(ledger.Skips == 1 && ledger.VisitDay == 4, "Skipped once: a visit on Day 4.");
        Check(LodgerStory.WhatTonight(ledger, 4, Everything) == LodgerStory.Tonight.Off, "Night 4 is off.");
        ledger.CameHome(4);
        Check(ledger.Skips == 1 && ledger.VisitDay == 4, "A night off is never a skip.");
        Check(LodgerStory.WhatTonight(ledger, 5, Everything) == LodgerStory.Tonight.AskAgain, "Night 5 asks again.");
        ledger.Ask(5);
        ledger.CameHome(5);
        Check(ledger.Skips == 2 && ledger.VisitDay == 6 && ledger.NoteDay == 0, "Skipped twice: a visit on Day 6.");
        ledger.Ask(6);
        ledger.CameHome(6);
        Check(ledger.NoteDay == 7 && ledger.NoteDue(7) && ledger.NoteFavour == NightThings.GraceCups && ledger.Dropped.Count == 1
              && ledger.Dropped[0] == NightThings.GraceCups && ledger.VisitDay == 6,
            "The third skip in a row: a note on the counter on Day 7, no visit, and the cups are dropped.");
        Check(ledger.Favour == LodgerStory.Cones && ledger.AskedOn == 0 && ledger.Skips == 0 && ledger.Errand == "", "The next favour comes up.");
        Check(LodgerStory.WhatTonight(ledger, 8, id => id != LodgerStory.Cones) == LodgerStory.Tonight.Wait
              && LodgerStory.WhatTonight(ledger, 8, Everything) == LodgerStory.Tonight.Ask,
            "A favour not in the game yet (the cones): nothing tonight; once it is, he asks.");
        ledger.Take(NightThings.GraceCups, GraceCameraEpisode.ProfileId, 9);
        Check(ledger.Give(NightThings.GraceCups) && ledger.Favour == LodgerStory.Cones, "A dropped favour given late changes nothing he asks for.");
        NotebookFactData note = LodgerStory.NotePage(NightThings.GraceCups);
        Check(note != null && note.source == Notebook.Sources.Inherited && note.who == LodgerStory.PagesWho && note.text == cups.note,
            "His note on the counter goes in the notebook as one of his pages.");
        var none = new NightLedger();
        none.Restore(new NightSaveData { metHim = true, trophies = new[] { NightThings.GraceGnome, NightThings.GraceCups },
            given = new[] { NightThings.GraceGnome, NightThings.GraceCups }, dropped = new[] { LodgerStory.Cones } });
        Check(none.Favour == "" && LodgerStory.WhatTonight(none, 9, Everything) == LodgerStory.Tonight.Wait && none.Errand == "",
            "Every favour done with: nothing to ask.");

        // Saves: a save from before the favours picks up where it was; nonsense is tidied.
        var before = new NightLedger();
        before.Restore(new NightSaveData { metHim = true });
        Check(before.Favour == NightThings.GraceGnome && before.AskedOn == 1 && before.Errand == NightThings.GraceGnome,
            "Met before the favours existed, gnome not given: the gnome is still his errand (the deal asked).");
        var after = new NightLedger();
        after.Restore(new NightSaveData { metHim = true, trophies = new[] { NightThings.GraceGnome }, given = new[] { NightThings.GraceGnome } });
        Check(after.Favour == NightThings.GraceCups && after.AskedOn == 0 && after.Errand == "", "Met and the gnome given: the cups come up next.");
        var stranger = new NightLedger();
        stranger.Restore(new NightSaveData { metHim = false, favour = NightThings.GraceCups, askedOn = 2, skips = 1 });
        Check(stranger.Favour == "" && stranger.AskedOn == 0 && stranger.Skips == 0, "Not met: no favour, whatever the save says.");
        var odd = new NightLedger();
        odd.Restore(new NightSaveData { metHim = true, favour = "nonsense", skips = 99, askedOn = -4, visitDay = -1,
            dropped = new[] { "", null, "x", "x" }, questions = new[] { null, new QuestionData { id = "q" }, new QuestionData { id = "q" }, new QuestionData() } });
        Check(odd.Favour == NightThings.GraceGnome && odd.Skips == 0 && odd.VisitDay == 0 && odd.Dropped.Count == 1 && odd.Questions.Count == 1,
            "A damaged save's favour, skips, days, drops and questions are tidied.");
        var midway = NightRuleChecks.Favoured();
        var copy = new NightLedger();
        copy.Restore(midway.Snapshot());
        Check(NightRuleChecks.SameFavours(midway, copy) && copy.Favour == NightThings.GraceCups && copy.Skips == 1 && copy.VisitDay == 3,
            "The record keeps his favours: the cups asked on Night 2, skipped, a visit on Day 3.");

        // His verdict on the day just ended: the officer's question first, else a straight face.
        var day = new NightLedger();
        day.Take(NightThings.GraceGnome, GraceCameraEpisode.ProfileId, 1);
        Check(LodgerStory.Verdict(day, 2) == "" && LodgerStory.Verdict(null, 2) == "", "Nothing happened: no verdict.");
        day.Faced(day.Unfaced(GraceCameraEpisode.ProfileId, 2), false, 2);
        Check(LodgerStory.Verdict(day, 2) == LodgerStory.VerdictHeld && LodgerStory.Verdict(day, 3) == "", "A straight face held that morning.");
        day.Take(NightThings.GraceCups, GraceCameraEpisode.ProfileId, 2);
        day.Faced(day.Unfaced(GraceCameraEpisode.ProfileId, 3), true, 3);
        Check(LodgerStory.Verdict(day, 3) == LodgerStory.VerdictCracked, "Cracked that morning.");
        day.Questioned("officer.description", OfficerStory.ProfileId, 3, false);
        Check(LodgerStory.Verdict(day, 3) == LodgerStory.VerdictQuiet, "The officer's question comes first: Ace said nothing.");
        var flinch = new NightLedger();
        flinch.Questioned("officer.description", OfficerStory.ProfileId, 3, true);
        Check(LodgerStory.Verdict(flinch, 3) == LodgerStory.VerdictFlinched, "Ace flinched.");

        // His tone in the café, and the night's note.
        Check(LodgerStory.VisitPool(LodgerStory.WarmFrom) == LodgerStory.VisitWarm && LodgerStory.VisitPool(0) == LodgerStory.Visit
              && LodgerStory.VisitPool(LodgerStory.ColdFrom) == LodgerStory.VisitCold, "Warm, plain or cold, by his warmth.");
        Check(LodgerStory.Hint(LodgerStory.Favours[0], "Barnaby").StartsWith("Barnaby is", StringComparison.Ordinal)
              && LodgerStory.Hint(cups, "the reunion cups").StartsWith("The reunion cups are", StringComparison.Ordinal)
              && LodgerStory.Hint(null, "x") == "", "The night's note names the thing as Ace knows it.");

        // Grace's cups: hers, written, and a little harder to keep a straight face about than the gnome.
        NightThing gnome = NightThings.GnomeOfGrace, sleeve = NightThings.CupsOfGrace;
        Check(NightThings.Find(NightThings.GraceCups) == sleeve && sleeve.owner == GraceCameraEpisode.ProfileId && NightThings.OwnedBy("grace") == gnome,
            "The cups are Grace's; by day she still talks about her gnome.");
        foreach (string line in new[] { sleeve.name, sleeve.unknownName, sleeve.complaint, sleeve.held, sleeve.cracked, sleeve.takenNote,
                     sleeve.takenNoteUnknown, sleeve.notebookMention, sleeve.notebookComplaint, sleeve.notebookTaken, sleeve.notebookTakenFor,
                     sleeve.notebookCracked })
            Check(!string.IsNullOrWhiteSpace(line), "Every line about the cups is written.");
        foreach (string line in new[] { sleeve.complaint, sleeve.held, sleeve.cracked })
            foreach (string beat in line.Split('\n'))
                Check(beat.Trim().Length > 0 && beat.Length <= 150, $"Each line is short (\"{beat}\").");
        var cupsMeter = new StraightFaceMeter(sleeve.sweepSeconds, sleeve.green, sleeve.near, sleeve.patience, .5f);
        float cupsWindow = (cupsMeter.Green + 2f * cupsMeter.Near) * cupsMeter.SweepSeconds;
        float gnomeWindow = (gnome.green + 2f * gnome.near) * gnome.sweepSeconds;
        Check(cupsWindow < gnomeWindow && cupsWindow >= .2f, $"The cups' window ({cupsWindow:0.00} s) is a little shorter than the gnome's, and still fair.");
        return count;
    }

    // ---------- the officer's question (session 3) ----------
    static int TheOfficer()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (OfficerStory.Question q in OfficerStory.Questions)
        {
            Check(!string.IsNullOrEmpty(q.id) && ids.Add(q.id) && OfficerStory.Find(q.id) == q, $"Question '{q.id}' is listed once and found.");
            foreach (string line in new[] { q.question, q.held, q.cracked, q.title, q.heldWord, q.crackedWord, q.notebookAsked, q.notebookCracked })
                Check(!string.IsNullOrWhiteSpace(line), $"Question '{q.id}': every line is written.");
            foreach (string line in new[] { q.question, q.held, q.cracked })
                foreach (string beat in line.Split('\n'))
                    Check(beat.Trim().Length > 0 && beat.Length <= 150, $"Each line is short (\"{beat}\").");
            var meter = new StraightFaceMeter(q.sweepSeconds, q.green, q.near, q.patience, .5f);
            float window = (meter.Green + 2f * meter.Near) * meter.SweepSeconds;
            Check(window >= .15f && q.green < NightThings.GnomeOfGrace.green, $"Saying nothing is harder than a complaint, but fair ({window:0.00} s).");
            Check(q.patience >= 3f * q.sweepSeconds, "There's time for a few sweeps.");
        }

        var ledger = new NightLedger();
        Check(OfficerStory.Due(OfficerStory.ProfileId, 3, ledger) == null, "Before Ace has met the man, he has nothing to ask about him.");
        ledger.Meet(1);
        Check(OfficerStory.Due(OfficerStory.ProfileId, 2, ledger) == null, "Not before Day 3.");
        OfficerStory.Question due = OfficerStory.Due(OfficerStory.ProfileId, 3, ledger);
        Check(due != null && due.id == "officer.description" && OfficerStory.Due("grace", 3, ledger) == null && OfficerStory.Due(null, 3, ledger) == null,
            "On Day 3 he asks about the man; nobody else does.");
        Check(ledger.Questioned(due.id, due.asker, 3, false) && !ledger.Questioned(due.id, due.asker, 4, true), "Each question is asked once.");
        Check(OfficerStory.Due(OfficerStory.ProfileId, 5, ledger) == null && ledger.HasAsked(due.id) && ledger.QuestionOn(3).id == due.id
              && ledger.QuestionOn(4) == null && ledger.Suspicion(OfficerStory.ProfileId) == 0, "Asked and kept: nothing more, no suspicion.");
        var flinched = new NightLedger();
        flinched.Meet(1);
        flinched.Questioned(due.id, due.asker, 3, true);
        Check(flinched.Suspicion(OfficerStory.ProfileId) == 1 && flinched.Questions[0].cracked, "A flinch makes him one step more suspicious.");
        Check(!flinched.Questioned("", "officer", 3, true) && !flinched.Questioned(null, "officer", 3, true), "Nothing without an id is asked.");

        NotebookFactData asked = OfficerStory.Asked(due, "Officer"), noticed = OfficerStory.Flinched(due, null);
        Check(asked.id == due.id && asked.who == OfficerStory.ProfileId && asked.name == "Officer" && asked.source == Notebook.Sources.Told
              && asked.text == due.notebookAsked, "What he asked goes in the notebook, as told.");
        Check(noticed.id == due.id + ".flinched" && noticed.name == OfficerStory.DefaultName && noticed.source == Notebook.Sources.Seen
              && noticed.text == due.notebookCracked, "That he noticed goes in as seen.");
        Check(OfficerStory.Asked(null, "x") == null && OfficerStory.Flinched(null, "x") == null, "No question, no fact.");
        return count;
    }
}
