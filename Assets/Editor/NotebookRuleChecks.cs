using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

// Rule checks for Ace's notebook (claude/night-notebook-spec.md). Pure: no
// scene, no save file, no assets. Also compiled by Tests/NotebookRules.
// Since 6 Oct: the man at the bins' pages (the inherited source) come first, in italics.
// Since 23 Oct 2026: the night's page with pictures (NoteSubjects, NotePhotos):
// CheckPictures, in the editor only (it reads the district's street names and
// draws the pictures in memory; Tests/NotebookRules doesn't compile it). It
// changes nothing either.
public static class NotebookRuleChecks
{
#if UNITY_EDITOR
    [MenuItem("Fixit Fidget/Checks/Night notebook rules")]
    public static void Run()
    {
        int count = RunAll() + CheckUnityJson() + CheckPictures();
        Debug.Log("[Notebook rules] PASS: " + count + " assertions. No scene, save or asset changes.");
    }

    // The night's page with pictures (23 Oct 2026; his note: "it's word vomit, and I rather like it being easier to see").
    // Every subject has a picture or a drawing; every fact the game can write has a subject; every line on the page keeps
    // to 8 words; tonight's errand is on top, in the Now line's names; an old save's facts still find their subjects.
    static int CheckPictures()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        DistrictStreets.LoadFromResources();   // the page's words as they read ("12 West Street")
        string west = StreetNames.Name(LodgerStory.GraceStreet);
        var photographed = new HashSet<string>(NotePhotoSteps.Photographed);

        // ---------- the subjects ----------
        var keys = new HashSet<string>();
        foreach (NoteSubject s in NoteSubjects.All)
        {
            Check(keys.Add(s.key) && s.key.Length > 0 && s.key.All(char.IsLetter), $"Subject '{s.key}': a key of its own, letters only (it's its picture's file name).");
            Check(Array.IndexOf(NotePhotos.Drawings, s.draw) >= 0, $"Subject '{s.key}': its drawing '{s.draw}' is one NotePhotos makes.");
            Check(ColorUtility.TryParseHtmlString(s.colour, out _), $"Subject '{s.key}': its colour {s.colour} reads.");
            string caption = StreetNames.Resolve(s.caption);
            Check(WordBudget.Words(caption) > 0 && !WordBudget.Over(caption, NoteSubjects.LineWords),
                $"Subject '{s.key}': its caption keeps to {NoteSubjects.LineWords} words: \"{caption}\" is {WordBudget.Report(caption, NoteSubjects.LineWords)}.");
            Check(photographed.Contains(s.key) || s.key == NoteSubjects.Someone,
                $"Subject '{s.key}' has a line in the photo step (NotePhotoSteps.Shots); only Someone is drawn by design.");
            Sprite drawing = NotePhotos.Drawing(s.key);
            Check(drawing != null && drawing.texture != null && drawing.texture.width == 128, $"Subject '{s.key}': its drawing is made (the fallback when it has no photo).");
        }

        // ---------- every line the page can say keeps to 8 words ----------
        foreach (string line in new[] { NoteSubjects.BagLine, NoteSubjects.BinsLine, NoteSubjects.BringLine, NoteSubjects.HimLine,
                                        NoteSubjects.ShelfLine, NoteSubjects.GivenLine, NoteSubjects.HomeLine })
            Check(!WordBudget.Over(line, NoteSubjects.LineWords), $"\"{line}\" keeps to {NoteSubjects.LineWords} words ({WordBudget.Report(line, NoteSubjects.LineWords)}).");
        foreach (NoteSubjects.Errand e in NoteSubjects.Errands)
        {
            string take = StreetNames.Resolve(e.take);
            Check(!WordBudget.Over(take, NoteSubjects.LineWords) && !WordBudget.Over(e.where, NoteSubjects.LineWords),
                $"His favour {e.favour}: \"{take}\" and \"{e.where}\" keep to {NoteSubjects.LineWords} words.");
            Check(LodgerStory.FindFavour(e.favour) != null && (e.whereKey.Length == 0 || NoteSubjects.Find(e.whereKey) != null),
                $"His favour {e.favour} is one of his, and its place is a subject.");
        }
        var written = new HashSet<string>();
        int facts = 0;
        foreach (var item in NoteSubjects.EveryFact(HomeDoor.GraceLooks))
        {
            facts++;
            NotebookFactData fact = item.fact;
            Check(fact != null && !string.IsNullOrEmpty(fact.id), $"{item.from}: a fact.");
            written.Add(fact.text);
            string key = NoteSubjects.KeyOf(fact);
            Check(NoteSubjects.Find(key) != null && (key != NoteSubjects.Someone || fact.who == "tomas"),
                $"{item.from} ({fact.id}): about a subject of its own ({key}), with a picture or its drawing.");
            var one = new Notebook();
            one.Learn(fact, 1);
            // Its own entry (a page of his is also behind his entry, which has a line of its own).
            NotebookFactData kept = one.Facts[0];   // the notebook keeps a copy
            NoteEntry entry = NoteSubjects.Entries(one, null, NoteStage.None, "").FirstOrDefault(e => e.PictureKey == key && e.facts.Contains(kept));
            Check(entry != null && WordBudget.Words(entry.line) > 0 && !WordBudget.Over(entry.line, NoteSubjects.LineWords),
                $"{item.from} ({fact.id}): its line on the page keeps to {NoteSubjects.LineWords} words: \"{NoteSubjects.Plain(entry?.line)}\".");
        }
        Check(facts >= 35, $"Every fact the game can write is listed ({facts}).");
        // (A short form that is a guess's and has no room for "(hunch)" gives way to the caption: each fact's real line is
        // checked above, with its real sureness.)
        foreach (KeyValuePair<string, string> gist in NoteSubjects.Gists)
        {
            Check(!WordBudget.Over(StreetNames.Resolve(gist.Value), NoteSubjects.LineWords),
                $"The short form \"{gist.Value}\" keeps to {NoteSubjects.LineWords} words ({WordBudget.Report(StreetNames.Resolve(gist.Value), NoteSubjects.LineWords)}).");
            if (!written.Contains(gist.Key))
                Debug.LogWarning("[Notebook rules] A short form whose fact the game no longer writes (rewritten? its short form can go, or be redone): \"" + gist.Key + "\"");
        }

        // ---------- the order: tonight's errand on top, in the Now line's names ----------
        var night0 = new Notebook();
        foreach (NotebookFactData f in NotebookEntries.GraceIntake("Grace")) night0.Learn(f, 1);
        night0.Learn(NotebookEntries.HomeSeen("grace", "Grace", HomeDoor.GraceLooks, "12", "west", true, Notebook.Sureness.Hunch), 1);
        night0.Learn(NightThings.Mentioned(NightThings.GnomeOfGrace, "Grace"), 1);
        foreach (NotebookFactData p in LodgerStory.Pages(null)) night0.Learn(p, 1);
        var ledger = new NightLedger();
        ledger.Meet(1);
        List<NoteEntry> take2 = NoteSubjects.Entries(night0, ledger, NoteStage.ToTake, LodgerStory.Errand(ledger));
        Check(take2.Count >= 4 && take2[0].key == NoteSubjects.Barnaby && take2[0].now && take2[0].title == "Barnaby"
              && take2[1].key == NoteSubjects.GraceHouse && take2[1].now && take2[1].title == "12 " + west,
            "The gnome to take: Barnaby on top, then 12 " + west + ", both tonight's (the Now line: \"Barnaby: Grace's step, 12 " + west + "\").");
        Check(take2[2].key == NoteSubjects.Lodger && take2[2].group == NoteGroup.People
              && take2[2].facts.Count(f => f.source == Notebook.Sources.Inherited) == 4,
            "Then the man at the bins, first of the people, his pages behind him.");
        int lastPerson = take2.FindLastIndex(e => e.group == NoteGroup.People), firstPlace = take2.FindIndex(e => e.group == NoteGroup.PlacesAndThings);
        Check(firstPlace < 0 || lastPerson < firstPlace, "People before places and things.");
        List<NoteEntry> hand = NoteSubjects.Entries(night0, ledger, NoteStage.InHand, NightThings.GraceGnome);
        Check(hand[0].key == NoteSubjects.Barnaby && hand[0].line == NoteSubjects.BringLine && hand[1].key == NoteSubjects.Lodger && hand[1].now,
            "In hand: Barnaby, \"" + NoteSubjects.BringLine + "\", then the man at the bins.");
        List<NoteEntry> bag = NoteSubjects.Entries(night0, new NightLedger(), NoteStage.BagToDoor, "");
        Check(bag[0].key == NoteSubjects.BackDoor && bag[0].line.StartsWith("Take the rubbish out the back door", StringComparison.Ordinal),
            "Night 0's bag: the back door on top, in the Now line's words.");
        var noTake = new Notebook();
        Check(NoteSubjects.Entries(noTake, ledger, NoteStage.ToTake, NightThings.GraceGnome)[0].title == "The garden gnome",
            "Before Grace mentions him, the gnome is \"The garden gnome\", as the Now line calls him.");
        ledger.Take(NightThings.GraceGnome, "grace", 1);
        ledger.Give(NightThings.GraceGnome);
        ledger.Ask(2);
        List<NoteEntry> cups = NoteSubjects.Entries(night0, ledger, NoteStage.ToTake, LodgerStory.Errand(ledger));
        Check(cups[0].key == NoteSubjects.Cups && cups[1].key == NoteSubjects.GraceHouse
              && cups.First(e => e.key == NoteSubjects.Barnaby).line == NoteSubjects.GivenLine,
            "Night 2: the cups on top, then her house; Barnaby, given, \"" + NoteSubjects.GivenLine + "\"");
        Check(NoteSubjects.Entries(night0, ledger, NoteStage.Home, "")[0].key == NoteSubjects.Cafe, "Nothing left tonight: the café on top (call it a night).");
        Check(NoteSubjects.Entries(night0, ledger, NoteStage.None, "").All(e => !e.now), "No night: nothing on top in amber.");

        // ---------- an old save's facts still find their subjects (no field was added: the key comes from id and who) ----------
        var old = new Notebook();
        old.Learn(new NotebookFactData { id = "grace.home", who = "grace", name = "Grace", kind = Notebook.Kinds.Address,
            text = "Came out of the saffron house at 12 {street:west}.", source = Notebook.Sources.Seen, sure = Notebook.Sureness.Hunch }, 2);
        old.Learn(new NotebookFactData { id = "tomas.apartment", who = "tomas", name = "Tomas", text = "Fancy apartment?", sure = Notebook.Sureness.Hunch }, 2);
        old.Learn(new NotebookFactData { id = "mystery" }, 2);
        List<NoteEntry> olds = NoteSubjects.Entries(old, null, NoteStage.None, "");
        Check(olds.Any(e => e.key == NoteSubjects.GraceHouse) && olds.Any(e => e.key == NoteSubjects.Someone + ":tomas" && e.title == "Tomas")
              && olds.Any(e => e.title == "Someone") && olds.All(e => e.line.Length > 0 && !WordBudget.Over(e.line, NoteSubjects.LineWords)),
            "An old save's facts (the saffron wording, a regular with no subject, a fact with no who) each find an entry and a line.");

        // ---------- the page's own numbers ----------
        foreach (float size in new[] { NightNotebook.PageTitleSize, NightNotebook.TitleSize, NightNotebook.LineSize, NightNotebook.HeadingSize,
                                       NightNotebook.FooterSize, NightNotebook.NoteSize })
            Check(size >= ReviewScreen.SmallPx, $"Every word on the page is {ReviewScreen.SmallPx:0} px or more ({size:0}).");
        Check(NightNotebook.TitleSize >= 26f && NightNotebook.TitleSize <= 28f, "Entry names are 26-28 px (the spec).");
        Check(NightNotebook.PolaroidWidth >= 96f && NightNotebook.PolaroidWidth <= 120f, "A polaroid is 96-120 px wide (the spec).");
        Check(NightNotebook.MaxFirstScreen <= 6, "At most six entries on the first screen.");
        return n;
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

        // ---------- the recap phone's Notes (playtest 2, step 2) ----------
        var phoneBook = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) phoneBook.Learn(fact, 1);
        phoneBook.Learn(new NotebookFactData { id = "tomas.job", who = "tomas", name = "Tomas", kind = Notebook.Kinds.Schedule,
            text = "works nights.", source = Notebook.Sources.Overheard, sure = Notebook.Sureness.Hunch }, 2);
        phoneBook.Learn(new NotebookFactData { id = "grace.home", who = "grace", name = "Grace", kind = Notebook.Kinds.Address,
            text = "Came out of the saffron house.", source = Notebook.Sources.Seen, sure = Notebook.Sureness.Hunch }, 2);
        phoneBook.Learn(new NotebookFactData { id = "grace.home", sure = Notebook.Sureness.Likely }, 3);
        phoneBook.Learn(NotebookEntries.GraceReturn("Grace", GracePhotoOutcome.Clear), 3);
        List<NotebookPerson> people = NotebookRecap.People(phoneBook);
        Check(people.Count == 2 && people[0].who == "grace" && people[0].name == "Grace" && people[1].who == "tomas",
            "Notes lists people in the order Ace first learned about them.");
        Check(people[0].facts.Count == 6 && people[0].facts[0].id == "grace.home" && people[0].facts[1].id == "grace.camera.strap"
              && people[0].facts[5].id == "grace.reunion.photo", "Where they live comes first, then the rest in the order learned.");
        Check(people.Sum(p => p.facts.Count) == phoneBook.Count && people[1].facts.Single().id == "tomas.job", "Every fact is on exactly one person's page.");
        Check(people[0].LearnedOn(3) == 1 && people[0].LearnedOn(1) == 4 && people[1].LearnedOn(3) == 0, "Each page counts what was learned on a day.");
        Check(NotebookRecap.Sureness(phoneBook.Find("grace.home"), 3) == "likely now" && NotebookRecap.Sureness(phoneBook.Find("grace.home"), 4) == "likely"
              && NotebookRecap.Sureness(phoneBook.Find("tomas.job"), 3) == "hunch" && NotebookRecap.Sureness(phoneBook.Find("grace.camera.strap"), 1) == ""
              && NotebookRecap.Sureness(null, 1) == "", "Sureness in words: a guess says so, a fact made surer today says \"now\", a told fact says nothing.");
        Check(NotebookRecap.Sentence("works nights.") == "Works nights." && NotebookRecap.Sentence(null) == "", "A fact reads as a sentence of its own.");
        Check(NotebookRecap.People(null).Count == 0 && NotebookRecap.People(new Notebook()).Count == 0, "No notebook, no pages.");
        // One line a person (the phone's Notes list, playtest 3): today's newest, else the newest of all.
        Check(NotebookRecap.OneLine(people[0], 3).id == "grace.reunion.photo" && NotebookRecap.OneLine(people[0], 2).id == "grace.home"
              && NotebookRecap.OneLine(people[0], 9).id == "grace.reunion.photo" && NotebookRecap.OneLine(people[1], 3).id == "tomas.job"
              && NotebookRecap.OneLine(null, 3) == null && NotebookRecap.OneLine(new NotebookPerson(), 3) == null,
            "One line a person: what was learned today, else the latest thing known.");
        Check(NotebookRecap.Tonight(phoneBook, 3).id == "grace.reunion.photo" && NotebookRecap.Tonight(phoneBook, 7) == null && NotebookRecap.Tonight(null, 3) == null,
            "The closing screen's one line is today's newest fact, or nothing.");
        Check(NotebookRecap.Page(phoneBook).StartsWith("<b>Grace</b>\n<size=92%><indent=4%>Came out of the saffron house. <color=#A6A6A6>(likely)</color></indent></size>\n",
            StringComparison.Ordinal), "The notebook as one page of text (the night's page until 23 Oct) still reads: the name, then where they live, marked as a guess.");

        // ---------- his pages (the man at the bins, 6 Oct 2026): the notebook's first pages, in his hand ----------
        var handed = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) handed.Learn(fact, 1);
        handed.Learn(new NotebookFactData { id = "lodger.page.test", who = "lodger.pages", name = "His pages", kind = Notebook.Kinds.Schedule,
            text = "Lights out by midnight.", source = Notebook.Sources.Inherited, sure = Notebook.Sureness.Sure }, 1);
        List<NotebookPerson> handedPeople = NotebookRecap.People(handed);
        Check(handedPeople.Count == 2 && handedPeople[0].who == "lodger.pages" && handedPeople[0].name == "His pages" && handedPeople[1].who == "grace"
              && handedPeople.Sum(p => p.facts.Count) == handed.Count, "His pages come first, though Ace learned about Grace before the notebook changed hands.");
        string handedPage = NotebookRecap.Page(handed);
        Check(handedPage.StartsWith("<b>His pages</b>\n<size=92%><indent=4%><i>Lights out by midnight.</i></indent></size>\n<b>Grace</b>", StringComparison.Ordinal),
            "On the page of text his lines are in his hand (italics), under his heading, before everyone else.");
        Check(Count(handedPage, "<i>") == 1 && Count(handedPage, "</i>") == 1, "Only his lines are in italics.");

        // ---------- the word budget (playtest 3): a notebook line reads at a glance, as it reads on the phone ----------
        var written = new List<NotebookFactData>(NotebookEntries.GraceIntake("Grace"));
        foreach (GracePhotoOutcome outcome in new[] { GracePhotoOutcome.Clear, GracePhotoOutcome.Imperfect, GracePhotoOutcome.Missed })
            written.Add(NotebookEntries.GraceReturn("Grace", outcome));
        written.Add(NotebookEntries.RegularRepair("priya", "Priya", "Espresso Machine", "Leaks under pressure"));
        written.Add(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", true, Notebook.Sureness.Hunch));
        written.Add(NotebookEntries.HomeSeen("tomas", "Tomas", "the blue house", "", "west", false, Notebook.Sureness.Hunch));
        foreach (NotebookFactData line in written)
        {
            string said = NotebookRecap.Sentence(line.text);
            Check(!WordBudget.Over(said, WordBudget.Page),
                $"A notebook line is at most {WordBudget.Page} words: \"{said}\" is {WordBudget.Report(said, WordBudget.Page)}.");
        }

        return count;
    }
}
