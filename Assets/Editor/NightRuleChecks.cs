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
        return n;
    }
#endif

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
}
