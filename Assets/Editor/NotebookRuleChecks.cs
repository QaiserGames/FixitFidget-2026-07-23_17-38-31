using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for Ace's notebook (claude/night-notebook-spec.md). Pure: no
// scene, no save file, no assets. Also compiled by Tests/NotebookRules.
public static class NotebookRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Night notebook rules")]
    public static void Run()
    {
        int count = RunAll() + CheckUnityJson();
        Debug.Log("[Notebook rules] PASS: " + count + " assertions. No scene, save or asset changes.");
    }

    // Unity's own serializer, which is what the real save uses.
    static int CheckUnityJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }

        var old = JsonUtility.FromJson<SaveData>("{\"version\":5,\"day\":2,\"money\":139,\"regularMemories\":[{\"profileId\":\"grace\",\"visits\":1,\"relationship\":2,\"graceCameraAttempted\":true,\"graceCameraDay\":1}]}");
        old.ValidateAndMigrate();
        Check(old.version == SaveData.CurrentVersion && old.day == 2 && old.money == 139 && old.notebook != null && old.notebook.Length == 0,
            "A save from before the notebook loads unchanged, with an empty notebook.");
        var book = new Notebook();
        book.Restore(old.notebook);
        foreach ((NotebookFactData fact, int day) in NotebookEntries.Backfill(old.regularMemories)) book.Learn(fact, day);
        Check(book.Count == 4 && book.Facts.All(f => f.day == 1 && f.who == "grace"), "Grace's Day 1 visit is backfilled, dated Day 1.");

        var save = new SaveData { day = 2, notebook = book.Snapshot() };
        var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        back.ValidateAndMigrate();
        var again = new Notebook();
        again.Restore(back.notebook);
        Check(again.Count == 4 && again.Find("grace.camera.strap")?.text == book.Find("grace.camera.strap").text
              && again.Find("grace.reunion.date")?.kind == Notebook.Kinds.Schedule,
            "The notebook survives Unity's JSON.");
        var closed = new SaveData { day = 2, dayCompleted = true, recap = new RecapSaveData { day = 2 }, notebook = book.Snapshot() };
        Check(closed.TryCreateNextDay(out SaveData next) && next.notebook.Length == 4 && next.day == 3,
            "Open Tomorrow carries the notebook forward.");
        return n;
    }
#endif

    public static int RunAll()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new InvalidOperationException(why); }
        int Count(string s, string part)
        {
            int n = 0;
            for (int at = s.IndexOf(part, StringComparison.Ordinal); at >= 0; at = s.IndexOf(part, at + part.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        // ---------- learning ----------
        var book = new Notebook();
        List<NotebookFactData> intake = NotebookEntries.GraceIntake("Grace").ToList();
        Check(intake.Count == 4 && intake.Select(f => f.id).Distinct().Count() == 4, "Grace's intake gives four different facts.");
        Check(intake.All(f => f.who == "grace" && f.source == Notebook.Sources.Told && f.sure == Notebook.Sureness.Sure),
            "They are about Grace, told, and sure.");
        // The dialogue pass: her request tells two of them, her thanks the other two (each learned when it's said).
        List<string> request = NotebookEntries.GraceIntakeSaid("Grace").Select(f => f.id).ToList();
        List<string> thanks = NotebookEntries.GraceThanksSaid("Grace").Select(f => f.id).ToList();
        Check(request.Count == 2 && thanks.Count == 2 && !request.Intersect(thanks).Any()
              && request.Concat(thanks).OrderBy(id => id).SequenceEqual(intake.Select(f => f.id).OrderBy(id => id))
              && request.Contains("grace.camera.strap") && thanks.Contains("grace.reunion.date"),
            "Her request (the strap, her husband) and her thanks (the reunion, the photos) share the four facts between them.");
        Check(intake.Select(f => f.kind).OrderBy(k => k).SequenceEqual(new[] { "claim", "possession", "relationship", "schedule" }),
            "Kinds: a possession, a relationship, a schedule and a claim.");
        int learned = intake.Count(f => book.Learn(f, 1));
        Check(learned == 4 && book.Count == 4 && book.PeopleCount == 1, "Learning them adds four facts about one person.");
        Check(intake.Count(f => book.Learn(f, 1)) == 0 && book.Count == 4, "Hearing the same story again the same day adds nothing.");
        Check(book.Facts.All(f => f.day == 1 && f.confirmedDay == 0), "Learned on Day 1, not yet confirmed.");
        book.Learn(intake[0], 3);
        Check(book.Find(intake[0].id).confirmedDay == 3 && book.Find(intake[0].id).day == 1 && book.Count == 4,
            "Hearing it on a later day confirms it and keeps the day it was first learned.");
        var hunch = new NotebookFactData { id = "tomas.apartment", who = "tomas", name = "Tomas", kind = Notebook.Kinds.Address,
            text = "Fancy apartment?", source = Notebook.Sources.Overheard, sure = Notebook.Sureness.Hunch };
        book.Learn(hunch, 2);
        book.Learn(new NotebookFactData { id = "tomas.apartment", sure = Notebook.Sureness.Likely }, 4);
        Check(book.Find("tomas.apartment").sure == Notebook.Sureness.Likely, "Ace can become surer of a fact.");
        book.Learn(new NotebookFactData { id = "tomas.apartment", sure = Notebook.Sureness.Hunch }, 5);
        Check(book.Find("tomas.apartment").sure == Notebook.Sureness.Likely, "…but never less sure.");
        Check(book.Find("tomas.apartment").text == "Fancy apartment?" && book.Find("tomas.apartment").confirmedDay == 5,
            "A confirmation does not overwrite what was written.");
        Check(!book.Learn(null, 1) && !book.Learn(new NotebookFactData { id = "" }, 1) && book.Count == 5, "Blank facts are ignored.");
        Check(book.LearnedOn(1).Count == 4 && book.LearnedOn(2).Count == 1 && book.LearnedOn(3).Count == 0,
            "What was learned is listed by the day it was first learned.");
        Check(book.PeopleCount == 2, "Two people so far.");

        // ---------- Grace's return ----------
        foreach (GracePhotoOutcome outcome in new[] { GracePhotoOutcome.Clear, GracePhotoOutcome.Imperfect, GracePhotoOutcome.Missed })
        {
            NotebookFactData photo = NotebookEntries.GraceReturn("Grace", outcome);
            Check(photo != null && photo.id == "grace.reunion.photo" && photo.who == "grace" && photo.text.Length > 10,
                $"Her return adds the reunion photo ({outcome}).");
        }
        Check(NotebookEntries.GraceReturn("Grace", GracePhotoOutcome.None) == null, "No return, no photo fact.");
        Check(NotebookEntries.GraceReturn("Grace", GracePhotoOutcome.Clear).text.Contains("print")
              && NotebookEntries.GraceReturn("Grace", GracePhotoOutcome.Imperfect).text.Contains("smudge")
              && NotebookEntries.GraceReturn("Grace", GracePhotoOutcome.Missed).text.Contains("Missed"),
            "Each outcome says what happened to the photo.");

        // ---------- other regulars ----------
        NotebookFactData phone = NotebookEntries.RegularRepair("tomas", "Tomas", "Phone", "Cracked Screen");
        Check(phone.text == "Brought in a phone: cracked screen." && phone.id == "tomas.brought.phone" && phone.kind == Notebook.Kinds.Possession,
            "Another regular's repair: what they brought in and what is wrong with it.");
        Check(NotebookEntries.RegularRepair("priya", "Priya", "Espresso Machine", "leaks").text == "Brought in an espresso machine: leaks.",
            "'an' before a vowel.");
        Check(NotebookEntries.RegularRepair("", "Nobody", "Phone", "x") == null && NotebookEntries.RegularRepair("tomas", "Tomas", "", "x") == null,
            "No person or no device, no fact.");

        // ---------- backfill ----------
        var memories = new[]
        {
            new RegularMemoryData { profileId = "grace", graceCameraAttempted = true, graceCameraDay = 1 },
            new RegularMemoryData { profileId = "tomas", visits = 3 }
        };
        var filled = NotebookEntries.Backfill(memories).ToList();
        Check(filled.Count == 4 && filled.All(f => f.day == 1 && f.fact.who == "grace"), "A save with Grace's camera visit backfills her intake, dated to it.");
        memories[0].graceReturnAcknowledged = true;
        memories[0].graceCameraReturned = true;
        memories[0].graceCameraGrade = "Perfect";
        filled = NotebookEntries.Backfill(memories).ToList();
        Check(filled.Count == 5 && filled.Last().fact.id == "grace.reunion.photo" && filled.Last().day == 2 && filled.Last().fact.text.Contains("print"),
            "…and, once her return is acknowledged, the photo, dated to her next visit at the earliest.");
        Check(!NotebookEntries.Backfill(new[] { new RegularMemoryData { profileId = "grace" } }).Any() && !NotebookEntries.Backfill(null).Any(),
            "Nothing to backfill without the visit.");
        var twice = new Notebook();
        foreach ((NotebookFactData fact, int day) in NotebookEntries.Backfill(memories)) twice.Learn(fact, day);
        foreach ((NotebookFactData fact, int day) in NotebookEntries.Backfill(memories)) twice.Learn(fact, day);
        Check(twice.Count == 5 && twice.Facts.All(f => f.confirmedDay == 0), "Backfilling on every load changes nothing the second time.");

        // ---------- save data ----------
        NotebookFactData[] snapshot = book.Snapshot();
        snapshot[0].text = "changed";
        Check(book.Facts[0].text != "changed", "A snapshot is a copy.");
        var restored = new Notebook();
        restored.Restore(new[] { snapshot[1], null, new NotebookFactData { id = "" }, snapshot[1], snapshot[2] });
        Check(restored.Count == 2 && restored.Knows(snapshot[1].id) && restored.Knows(snapshot[2].id), "Restore drops blanks and duplicates.");
        var save = new SaveData { notebook = null };
        save.ValidateAndMigrate();
        Check(save.notebook != null && save.notebook.Length == 0, "A save without a notebook gets an empty one, not a null.");
        save = new SaveData { notebook = new[] { snapshot[0], null, new NotebookFactData() } };
        save.ValidateAndMigrate();
        Check(save.notebook.Length == 1, "Blank entries in a save are dropped on load.");
        Check(SaveData.CurrentVersion == 5, "The notebook is additive: the save version is unchanged, so older builds still load the save.");
        var closed = new SaveData { day = 6, dayCompleted = true, recap = new RecapSaveData { day = 6 }, notebook = book.Snapshot() };
        Check(closed.TryCreateNextDay(out SaveData tomorrow) && tomorrow.notebook.Length == book.Count, "Tomorrow keeps the notebook.");

        // ---------- the recap ----------
        var recapBook = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) recapBook.Learn(fact, 1);
        string text = NotebookRecap.Build(recapBook, 1);
        Check(text.StartsWith("<b>Notebook</b>", StringComparison.Ordinal) && text.Contains("4 new") && text.Contains("(4 facts about 1 person so far)"),
            "The recap says how many are new and the running total.");
        Check(text.Contains("Grace: a camera with a scratched strap.") && text.Contains("Grace: her husband carried that strap everywhere."),
            "Each line is the name, then the fact mid-sentence.");
        Check(NotebookRecap.Build(recapBook, 2) == "" && NotebookRecap.Build(null, 1) == "", "Nothing new today, no block.");
        for (int i = 0; i < 4; i++)
            recapBook.Learn(new NotebookFactData { id = "extra." + i, who = "p" + i, name = "P" + i, text = "Fact " + i + "." }, 1);
        text = NotebookRecap.Build(recapBook, 1);
        Check(text.Contains("8 new") && text.Contains("+3 more") && Count(text, "\n") == NotebookRecap.MaxLines + 2,
            "At most five lines, then how many more.");
        Check(text.Contains("(8 facts about 5 people so far)"), "Plural people.");
        Check(Count(text, "<b>") == Count(text, "</b>") && Count(text, "<size=") == Count(text, "</size>")
              && Count(text, "<color=") == Count(text, "</color>"), "The recap's rich-text tags are balanced.");
        Check(text.Split('\n').Skip(1).All(line => line.StartsWith("<size=", StringComparison.Ordinal) && line.EndsWith("</size>", StringComparison.Ordinal)),
            "Every line under the heading is sized on its own, so no size leaks into the next line.");

        return count;
    }
}
