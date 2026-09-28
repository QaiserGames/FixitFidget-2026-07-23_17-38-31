using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for the Night 1 slice (claude/ace-after-dark.md §3.2): the night's ledger (trophies,
// the mornings still to come, suspicion), the straight-face meter, and the gnome's words. Pure: no
// scene, no save file, no assets. Also compiled by Tests/NightRules.
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
        foreach (string line in new[] { gnome.name, gnome.unknownName, gnome.mention, gnome.complaint, gnome.held, gnome.cracked,
                     gnome.takenNote, gnome.takenNoteUnknown, gnome.notebookMention, gnome.notebookTaken, gnome.notebookCracked })
            Check(!string.IsNullOrWhiteSpace(line), "Every line is written.");
        Check(gnome.mention.Contains(gnome.name) && gnome.complaint.Contains(gnome.name), "The day and the morning after name the same gnome.");
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
        Check(NightThings.Mentioned(null, "Grace") == null && NightThings.Taken(null, null) == null, "No thing, no fact.");
        var book = new Notebook();
        Check(book.Learn(mentioned, 1) && book.Learn(taken, 1) && !book.Learn(mentioned, 2) && book.Find("grace.gnome").confirmedDay == 2,
            "The notebook learns them like any other fact.");
        return count;
    }
}
