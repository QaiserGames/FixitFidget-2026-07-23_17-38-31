using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THE NOTEBOOK'S PICTURES, PART 1: WHO AND WHAT EACH NOTE IS ABOUT (23 Oct 2026)
//
// His note (22 Oct, night): "let's add like pictures that make it also easier for players to know what's happening,
// because whenever I open the notes in nighttime, ... it's like just a cluster of words, it's word vomit, and I rather
// like it being easier to see." His girlfriend (dyslexic, on a PlayStation pad) "felt overall lost most of the time" in
// playtest 7: notes she can't take in at a glance fail her first.
//
// So the night's page (NightNotebook) is pictures first, few words. One entry is one SUBJECT (a person, a place or a
// thing), drawn as its picture (NotePhotos: a photo the game takes of its own model, or a drawing of the same name), a
// bold title and one line of at most LineWords words. Everything written about it sits behind a press. This file holds
// the words and the rules, no pictures:
//   * the subjects (All): what each is called, its caption, what to draw when there is no photo, the street it's on;
//   * which subject a fact is about (KeyOf), worked out from the fact's id and who. Nothing new is saved: every save,
//     old or new, gets its pictures with no change to the save (Bible §2; SaveData stays at version 5);
//   * the page's entries (Entries): tonight's errand first, marked "now" (the page draws it in the bench's amber), in the
//     words NightCycle's Now line uses; then the people; then the places and things. Each line is what matters now.
//
// A fact's own words show when they fit the line. A longer fact has a short form here (Gists), keyed by its exact words.
// When Mansoor rewrites a fact, its short form drops away and the subject's caption shows until it gets a new one, so an
// old summary never stands for new words. The fact itself, whole, is always behind the press.
//
// To add a subject: one line in All (its words), and one line in the photo step's list (NotePhotoSteps.Shots: how
// to photograph it). Until it has a photo, the page draws its fallback.
//
// No Unity types: the edit-mode Night notebook rules (NotebookRuleChecks) check this file, and the notebook's page lab
// (NotebookPageCheck) uses EveryFact to prove every fact the game can write has a subject and a picture.
// ---------------------------------------------------------------------------

/// <summary>What a subject is: the page groups people apart from places and things.</summary>
public enum NoteKind { Person, Place, Thing }

/// <summary>The page's three groups, in the order they are drawn.</summary>
public enum NoteGroup { Tonight, People, PlacesAndThings }

/// <summary>
/// What the night is for right now, in the page's terms: the same state, in the same order, as NightCycle.NowLine reads
/// (the bag to the back door, the bag to the bins, his errand's thing to take, in hand, on the shelf, nothing left).
/// None: no night, or a scene of his playing (the Now line hides then too).
/// </summary>
public enum NoteStage { None, BagToDoor, BagToBins, ToTake, InHand, OnShelf, Home }

/// <summary>One person, place or thing the notebook can be about.</summary>
public sealed class NoteSubject
{
    /// <summary>Its key, and its picture's file name (Resources/UI/NotePhotos/&lt;key&gt;.png). Letters only: never rename.</summary>
    public string key = "";
    public NoteKind kind;
    /// <summary>The bold title. A street as its token ("12 {street:west}"); a thing of the night is named as Ace knows it.</summary>
    public string title = "";
    /// <summary>What it is, at most LineWords words: the line when nothing more pressing is known.</summary>
    public string caption = "";
    /// <summary>What NotePhotos draws when there is no photo: person, house, door, bins, corner, gnome, cups, crate, cone, cafe.</summary>
    public string draw = "person";
    /// <summary>The drawing's main colour (people: their profile's theme colour), "#RRGGBB".</summary>
    public string colour = "#8C7B6B";
    /// <summary>The street it is on (a District streets id): the map card lights that street. "" for none.</summary>
    public string street = "";
    /// <summary>A NightThings id (or his favour's id) for a thing of the night: its facts are its, and its name is the thing's.</summary>
    public string thing = "";
    /// <summary>A person's profile id ("grace", "officer"): their facts are theirs.</summary>
    public string profile = "";
}

/// <summary>One entry on the night's page: a subject, its title and line, and every fact about it (behind the press).</summary>
public sealed class NoteEntry
{
    /// <summary>The subject's key; for a regular with no subject of their own, "someone:" and their profile id.</summary>
    public string key = "";
    public NoteSubject subject;
    public NoteGroup group;
    /// <summary>Tonight's errand: on top, in amber (the bench's "next").</summary>
    public bool now;
    public string title = "";
    /// <summary>One line, at most NoteSubjects.LineWords words (rich text: a guess's "(hunch)" is grey).</summary>
    public string line = "";
    /// <summary>Everything about it, as the press shows it: where it is first, then in the order learned.</summary>
    public readonly List<NotebookFactData> facts = new List<NotebookFactData>();
    /// <summary>The picture's key (the subject's: a regular with no subject of their own shares "someone").</summary>
    public string PictureKey => subject != null ? subject.key : NoteSubjects.Someone;
}

public static class NoteSubjects
{
    /// <summary>A line on the page is at most this many words (counted by WordBudget: an icon or a tag isn't a word).</summary>
    public const int LineWords = 8;

    // ---------- the subjects ----------

    public const string Grace = "grace", Lodger = "lodger", Officer = "officer", Neighbours = "neighbours", Someone = "someone";
    public const string GraceHouse = "gracehouse", BackDoor = "backdoor", Alley = "alley", Corner = "corner", Cafe = "cafe";
    public const string Barnaby = "barnaby", Cups = "cups", Crate = "crate", Cones = "cones";

    const string Grey = "#A6A6A6";   // NotebookRecap's grey: a guess says so

    // Titles and captions in the night's own words (playtest 7: Grace's house is "12 West Street, the yellow house with
    // the porch light" everywhere; "the man at the bins"; "the bins"; "your shelf, behind the counter"; "a café door").
    static readonly NoteSubject[] all =
    {
        // People.
        new NoteSubject { key = Grace, kind = NoteKind.Person, title = "Grace", caption = "Your regular. Lives at 12 {street:west}.",
                          draw = "person", colour = "#D4A23A", profile = NotebookEntries.GraceId },
        new NoteSubject { key = Lodger, kind = NoteKind.Person, title = "The man at the bins", caption = "Lives in your bins. Wants favours.",
                          draw = "person", colour = "#6B2E2E" },
        new NoteSubject { key = Officer, kind = NoteKind.Person, title = "The Officer", caption = "A regular. Keeps an eye out.",
                          draw = "person", colour = "#80A3D9", profile = OfficerStory.ProfileId },
        // Nobody in particular (NightNeighbours): what the notebook knows of them is their windows, so their picture is a house.
        new NoteSubject { key = Neighbours, kind = NoteKind.Person, title = "The neighbours", caption = "The houses along {street:west}.",
                          draw = "house", colour = "#C99A9A", street = "west" },
        // Any other regular (a repair they brought in): their name is the title.
        new NoteSubject { key = Someone, kind = NoteKind.Person, title = "", caption = "A regular at the café.",
                          draw = "person", colour = "#8C7B6B" },
        // Places.
        new NoteSubject { key = GraceHouse, kind = NoteKind.Place, title = "12 {street:west}", caption = "Grace's house. Yellow, porch light on.",
                          draw = "house", colour = "#E3B23C", street = "west" },
        new NoteSubject { key = BackDoor, kind = NoteKind.Place, title = "The back door", caption = "Out the back of the café.",
                          draw = "door", colour = "#7A5236", street = "back" },
        new NoteSubject { key = Alley, kind = NoteKind.Place, title = "The bins", caption = "His alley, behind the café.",
                          draw = "bins", colour = "#3F6B4F", street = "back" },
        new NoteSubject { key = Corner, kind = NoteKind.Place, title = "His corner", caption = "What you bring him stands here.",
                          draw = "corner", colour = "#55606B", street = "back" },
        new NoteSubject { key = Cafe, kind = NoteKind.Place, title = "Ace's café", caption = "Your café. Home.",
                          draw = "cafe", colour = "#2E7D5B", street = "front" },
        // Things.
        new NoteSubject { key = Barnaby, kind = NoteKind.Thing, title = "Barnaby", caption = "Grace's garden gnome.",
                          draw = "gnome", colour = "#C8452F", thing = NightThings.GraceGnome, street = "west" },
        new NoteSubject { key = Cups, kind = NoteKind.Thing, title = "The reunion cups", caption = "Grace's reunion cups.",
                          draw = "cups", colour = "#E8DCC4", thing = NightThings.GraceCups, street = "west" },
        new NoteSubject { key = Crate, kind = NoteKind.Thing, title = "His crate", caption = "A box by the bins.",
                          draw = "crate", colour = "#B08A5A", street = "back" },
        // His third favour (not in the game yet: nothing to take for it): kept so its page and note have a picture.
        new NoteSubject { key = Cones, kind = NoteKind.Thing, title = "The cones", caption = "From the road works on {street:west}.",
                          draw = "cone", colour = "#E5762E", thing = LodgerStory.Cones, street = "west" },
    };

    public static IReadOnlyList<NoteSubject> All => all;

    /// <summary>The subject with this key, or null.</summary>
    public static NoteSubject Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (NoteSubject s in all) if (s.key == key) return s;
        return null;
    }

    // ---------- tonight: the errand's words (the Now line's, NightCycle.NowLine, kept to LineWords) ----------

    public const string BagLine = "Take the rubbish out the back door.";
    public const string BinsLine = "Put the bag in the bins.";
    public const string BringLine = "Bring it to the man at the bins.";
    public const string HimLine = "At the bins, behind the café.";
    public const string ShelfLine = "On your shelf, behind the counter.";
    public const string GivenLine = "With the man at the bins now.";
    public const string HomeLine = "Call it a night at a café door.";

    /// <summary>One of his favours on the page: what to say of the thing while it's still to take, and where it is.</summary>
    public sealed class Errand
    {
        public string favour = "", take = "", where = "", whereKey = "";
    }

    // His favours in order (LodgerStory.Favours). The where-line names the place the way the Now line does (LodgerStory's
    // "now": "Barnaby: Grace's step, 12 West Street"; "The reunion cups: Grace's kitchen. Go in once she's asleep."); the
    // place's title is the address. A favour not listed here gets its subject's caption and no place.
    static readonly Errand[] errands =
    {
        new Errand { favour = NightThings.GraceGnome, take = "Grace's gnome. Bring it to him.",
                     whereKey = GraceHouse, where = "Her front step. Yellow house, porch light." },
        new Errand { favour = NightThings.GraceCups, take = "Grace's cups. Bring him a sleeve.",
                     whereKey = GraceHouse, where = "Her kitchen. Go in once she's asleep." },
        new Errand { favour = LodgerStory.Cones, take = "From the road works on {street:west}.", whereKey = "", where = "" },
    };

    public static IReadOnlyList<Errand> Errands => errands;

    public static Errand FindErrand(string favour)
    {
        if (string.IsNullOrEmpty(favour)) return null;
        foreach (Errand e in errands) if (e.favour == favour) return e;
        return null;
    }

    // ---------- which subject a fact is about ----------

    // His pages (LodgerStory.Pages): what each one is about.
    static readonly Dictionary<string, string> pageSubjects = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lodger.page.grace"] = GraceHouse,     // her house, and when she's asleep
        ["lodger.page.gnome"] = Barnaby,
        ["lodger.page.windows"] = Neighbours,   // the street's windows going dark
        ["lodger.page.parking"] = Cones,        // where the cones would go (it waits for them)
    };

    /// <summary>
    /// The subject <paramref name="fact"/> is about, from its id and who (nothing saved): a thing of the night's facts are
    /// the thing's (its mention, the morning's complaint, taking it, its owner's suspicion); his pages are what each is
    /// about, his notes his; where someone lives is their house; anything else is the person it's about; a regular with
    /// no subject of their own is Someone.
    /// </summary>
    public static string KeyOf(NotebookFactData fact)
    {
        if (fact == null) return Someone;
        string id = fact.id ?? "", who = fact.who ?? "";
        foreach (NoteSubject s in all)
            if (s.thing.Length > 0 && (id == s.thing || id.StartsWith(s.thing + ".", StringComparison.Ordinal))) return s.key;
        if (pageSubjects.TryGetValue(id, out string page)) return page;
        if (id.StartsWith("lodger.page." + NotebookEntries.GraceId + ".", StringComparison.Ordinal)) return Grace;   // her Thursdays, her photos
        if (who == LodgerStory.PagesWho) return Lodger;                                                              // his note on the counter
        if (who.Length > 0 && id == who + ".home") return HouseOf(who);                                             // NotebookEntries.HomeSeen
        return KeyOfPerson(who);
    }

    /// <summary>A person's subject by their profile id ("grace", "officer"); his pages are his; anyone else is Someone.
    /// The phone's Notes lists people by this id (NotebookRecap.People).</summary>
    public static string KeyOfPerson(string who)
    {
        if (string.IsNullOrEmpty(who)) return Someone;
        if (who == LodgerStory.PagesWho) return Lodger;
        foreach (NoteSubject s in all) if (s.profile.Length > 0 && s.profile == who) return s.key;
        return Someone;
    }

    /// <summary>Where <paramref name="who"/> lives, as a subject: Grace's house; anyone else's home is theirs (no picture of their own yet).</summary>
    public static string HouseOf(string who) => who == NotebookEntries.GraceId ? GraceHouse : KeyOfPerson(who);

    /// <summary>The thing of the night (or favour) <paramref name="id"/> is, as a subject; "" for none.</summary>
    public static string KeyOfThing(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        foreach (NoteSubject s in all) if (s.thing == id) return s.key;
        return "";
    }

    // The entry a fact goes under: its subject, or, for a regular with none of their own, theirs alone.
    static string EntryKey(NotebookFactData fact)
    {
        string key = KeyOf(fact);
        return key == Someone ? Someone + ":" + (fact.who ?? "") : key;
    }

    // ---------- the short forms (Gists) ----------

    // A fact over LineWords words, shortened for the page's one line, keyed by its exact words as written (street tokens as
    // tokens). A rewritten fact no longer matches and shows the caption instead (see the header). The whole fact is behind
    // the press either way.
    static readonly Dictionary<string, string> gists = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Grace (NotebookEntries).
        ["A camera with a scratched strap. Wants the strap left alone."] = "Her camera's strap: leave it alone.",
        ["Family reunion tomorrow. She might let someone put her in the picture."] = "Family reunion tomorrow.",
        ["In the middle of the reunion photo. Left the shop a print."] = "In the reunion photo. Left a print.",
        ["Reunion photo has a smudge. Told everyone it was artistic."] = "Her reunion photo has a smudge.",
        ["Missed the reunion photo. Hasn't given up on the camera."] = "Missed the reunion photo.",
        ["Came out of the yellow house at 12 {street:west}."] = "Grace lives here. Seen at her door.",
        ["Went into the yellow house at 12 {street:west}."] = "Grace lives here. Seen at her door.",
        // His pages (LodgerStory).
        ["Grace: 12 {street:west}, the yellow house, porch light. Asleep by 1:45."] = "Grace's house. Asleep by 1:45.",
        ["A gnome on her step. It faces the alley."] = "On her step. It faces the alley.",
        ["The street's windows go dark one by one. All by three."] = "Their windows go dark. All by three.",
        // The officer (OfficerStory).
        ["Looking for a man: suit, fifties, sleeps rough. Asked me."] = "Looking for the man at the bins.",
        ["I flinched. He's taking an interest in the café now."] = "I flinched. He's watching the café now.",
        ["Asked if I keep odd company. Someone's been talking."] = "Asked if I keep odd company.",
        ["I flinched. He listens to whoever talks about the café."] = "I flinched. He listens to café talk.",
        // Grace's things (NightThings).
        ["Barnaby, her gnome: on her step, 12 {street:west}. Porch light on."] = "Her gnome, on her front step.",
        ["Had a garden gnome, Barnaby, on her front step. Twenty years."] = "Her gnome. Twenty years on her step.",
        ["Took Barnaby from her front step. He's on the shelf now."] = "Took him from her front step.",
        ["Took Barnaby from her front step, for the man at the bins."] = "Took him for the man at the bins.",
        ["Asked if you were smiling about Barnaby. Watching her front step now."] = "Grace is watching her step now.",
        ["Reunion cups in her kitchen: a box on the worktop. His word."] = "In her kitchen, a box on the worktop.",
        ["Took a sleeve of her reunion cups. It's on the shelf."] = "Took a sleeve. On my shelf now.",
        ["Took a sleeve of her cups for the man at the bins."] = "Took a sleeve for the man.",
        ["Smiled when she told me about her cups. She's counting them now."] = "She's counting her cups now.",
    };

    /// <summary>The short form of a fact's words, or null when it has none (checks: every short form keeps to LineWords).</summary>
    public static string Gist(string text) => text != null && gists.TryGetValue(text, out string gist) ? gist : null;

    /// <summary>Every short form, for the checks.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Gists => gists;

    // ---------- the entries ----------

    /// <summary>
    /// The page, in order: tonight's errand (<paramref name="stage"/>, for <paramref name="errand"/>: his favour, LodgerStory.Errand)
    /// on top, marked now; then the people (the man at the bins first once his pages are in, then as Ace met them); then the
    /// places and things (as Ace learned of them). Each with its title, its one line and every fact about it. The man at the
    /// bins' facts are his notes and, behind them, every page in his hand. Empty for no notebook and no errand.
    /// </summary>
    public static List<NoteEntry> Entries(Notebook notebook, NightLedger ledger, NoteStage stage, string errand)
    {
        var byKey = new Dictionary<string, NoteEntry>(StringComparer.Ordinal);
        var learned = new List<NoteEntry>();   // in the order Ace first learned something about each
        bool hisPages = false;
        if (notebook != null)
            foreach (NotebookFactData fact in notebook.Facts)
            {
                if (fact == null) continue;
                if (fact.source == Notebook.Sources.Inherited) hisPages = true;
                string key = EntryKey(fact);
                if (!byKey.TryGetValue(key, out NoteEntry entry))
                {
                    entry = NewEntry(key, fact);
                    byKey[key] = entry;
                    learned.Add(entry);
                }
                entry.facts.Add(fact);
            }
        // The notebook was his: once his pages are in, he has an entry, and his pages are behind it.
        NoteEntry him = null;
        if (hisPages || byKey.ContainsKey(Lodger))
        {
            him = Get(byKey, Lodger);
            foreach (NotebookFactData fact in notebook.Facts)
                if (fact != null && fact.source == Notebook.Sources.Inherited && !him.facts.Contains(fact)) him.facts.Add(fact);
        }

        // Tonight, in the Now line's order: what to do, and where (the thing's place while it's still to take; the man at the
        // bins while it's in hand). A favour with no subject of its own puts nothing on top.
        var tonight = new List<NoteEntry>();
        NoteSubject thingSubject = Find(KeyOfThing(errand));
        Errand words = FindErrand(errand);
        if (stage == NoteStage.BagToDoor) tonight.Add(Tonight(byKey, BackDoor, BagLine));
        else if (stage == NoteStage.BagToBins) tonight.Add(Tonight(byKey, Alley, BinsLine));
        else if (stage == NoteStage.Home) tonight.Add(Tonight(byKey, Cafe, HomeLine));
        else if (thingSubject != null && stage == NoteStage.ToTake)
        {
            tonight.Add(Tonight(byKey, thingSubject.key, words != null && words.take.Length > 0 ? words.take : thingSubject.caption));
            if (words != null && words.whereKey.Length > 0) tonight.Add(Tonight(byKey, words.whereKey, words.where));
        }
        else if (thingSubject != null && stage == NoteStage.InHand)
        {
            tonight.Add(Tonight(byKey, thingSubject.key, BringLine));
            tonight.Add(Tonight(byKey, Lodger, HimLine));
        }
        else if (thingSubject != null && stage == NoteStage.OnShelf) tonight.Add(Tonight(byKey, thingSubject.key, ShelfLine));

        var entries = new List<NoteEntry>(learned.Count + 2);
        foreach (NoteEntry e in tonight)
            if (!entries.Contains(e)) { e.group = NoteGroup.Tonight; entries.Add(e); }
        if (him != null && !him.now) { him.group = NoteGroup.People; entries.Add(him); }
        foreach (NoteEntry e in learned)
            if (!e.now && e != him && (e.subject == null || e.subject.kind == NoteKind.Person)) { e.group = NoteGroup.People; entries.Add(e); }
        foreach (NoteEntry e in learned)
            if (!e.now && e.subject != null && e.subject.kind != NoteKind.Person) { e.group = NoteGroup.PlacesAndThings; entries.Add(e); }

        foreach (NoteEntry e in entries)
        {
            e.title = TitleOf(e, notebook);
            if (!e.now) e.line = LineOf(e, ledger);
            SortFacts(e.facts);
        }
        return entries;
    }

    static NoteEntry NewEntry(string key, NotebookFactData first)
    {
        NoteSubject subject = Find(key);
        if (subject == null && key.StartsWith(Someone + ":", StringComparison.Ordinal)) subject = Find(Someone);
        var entry = new NoteEntry { key = key, subject = subject };
        // A regular with no subject of their own is called what Ace calls them (NotebookRecap.People's rule), never nothing.
        if (subject != null && subject.key == Someone && first != null)
            entry.title = !string.IsNullOrWhiteSpace(first.name) ? first.name.Trim() : !string.IsNullOrWhiteSpace(first.who) ? first.who.Trim() : "Someone";
        return entry;
    }

    // The entry for a subject, made if nothing is written about it yet (tonight's back door, the café; the man at the bins
    // once his pages are in): such an entry is placed by the caller, never among those learned.
    static NoteEntry Get(Dictionary<string, NoteEntry> byKey, string key)
    {
        if (byKey.TryGetValue(key, out NoteEntry entry)) return entry;
        entry = NewEntry(key, null);
        byKey[key] = entry;
        return entry;
    }

    static NoteEntry Tonight(Dictionary<string, NoteEntry> byKey, string key, string line)
    {
        NoteEntry entry = Get(byKey, key);
        entry.now = true;
        entry.line = StreetNames.Resolve(line ?? "");
        return entry;
    }

    /// <summary>The entry's title: a thing of the night named as Ace knows it (as the Now line names it), a street by name.</summary>
    static string TitleOf(NoteEntry e, Notebook notebook)
    {
        NoteSubject s = e.subject;
        if (s == null) return e.title;
        if (s.key == Someone) return e.title;
        NightThing thing = NightThings.Find(s.thing);
        if (thing != null)
        {
            string name = notebook != null && notebook.Knows(thing.id) ? thing.name : thing.unknownName;
            if (!string.IsNullOrEmpty(name)) return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
        return StreetNames.Resolve(s.title);
    }

    /// <summary>
    /// An entry's line when it isn't tonight's: a thing of the night says where it is now (with him, or on Ace's shelf);
    /// otherwise the newest fact about it (the man at the bins: his own notes, not his pages about others) in its own words
    /// if they fit, else its short form, else the subject's caption. A guess says so ("(hunch)", grey), inside the budget.
    /// </summary>
    static string LineOf(NoteEntry e, NightLedger ledger)
    {
        NoteSubject s = e.subject;
        if (s != null && s.thing.Length > 0 && ledger != null)
        {
            if (ledger.HasGiven(s.thing)) return GivenLine;
            if (ledger.OnShelf(s.thing)) return ShelfLine;
        }
        NotebookFactData newest = null;
        foreach (NotebookFactData fact in e.facts)
        {
            if (fact == null) continue;
            if (s != null && s.key == Lodger && KeyOf(fact) != Lodger) continue;   // his pages are about others
            if (newest == null || fact.day >= newest.day) newest = fact;
        }
        string caption = s != null ? StreetNames.Resolve(s.caption) : "";
        if (newest == null) return caption;
        string marker = newest.sure == Notebook.Sureness.Hunch || newest.sure == Notebook.Sureness.Likely ? newest.sure : "";
        string own = Fit(NotebookRecap.Sentence(newest.text), marker);
        if (own != null) return own;
        string gist = Gist(newest.text);
        string shortened = gist != null ? Fit(StreetNames.Resolve(gist), marker) : null;
        return shortened ?? caption;
    }

    // The words, with a guess's mark in grey, if all of it keeps to LineWords; else null.
    static string Fit(string words, string marker)
    {
        if (string.IsNullOrWhiteSpace(words)) return null;
        string line = "<noparse>" + words + "</noparse>" + (marker.Length > 0 ? " <color=" + Grey + ">(" + marker + ")</color>" : "");
        return WordBudget.Words(line) <= LineWords ? line : null;
    }

    // Behind the press: where it is first (the night is for going places), then in the order learned (NotebookRecap.People's rule).
    static void SortFacts(List<NotebookFactData> facts)
    {
        var address = new List<NotebookFactData>();
        var rest = new List<NotebookFactData>();
        foreach (NotebookFactData f in facts) (f != null && f.kind == Notebook.Kinds.Address ? address : rest).Add(f);
        facts.Clear();
        facts.AddRange(address);
        facts.AddRange(rest);
    }

    /// <summary>A fact as the press shows it: its own words, street names filled in, and "(hunch)" or "(likely)" in grey.</summary>
    public static string FactLine(NotebookFactData fact)
    {
        if (fact == null) return "";
        string line = "<noparse>" + NotebookRecap.Sentence(fact.text) + "</noparse>";
        if (fact.sure == Notebook.Sureness.Hunch || fact.sure == Notebook.Sureness.Likely) line += " <color=" + Grey + ">(" + fact.sure + ")</color>";
        return line;
    }

    /// <summary>
    /// Every fact the game can write in the notebook today, with where it comes from, for the checks (each must have a
    /// subject with a picture, or a declared drawing, and a line within the budget): Grace's camera and her return
    /// (NotebookEntries, by day), another regular's repair, seeing her at her door (<paramref name="graceLooks"/>: her
    /// house's looks, HomeDoor.GraceLooks), each thing of the night (NightThings: mentioned, complained of, taken for the
    /// shelf or for him, the owner's suspicion), his pages, the page each favour pays and his note on the counter
    /// (LodgerStory: NightZero's hand-over, Lodger's return, LodgerDay), and the officer's questions (OfficerStory, via
    /// MorningFace). The night itself only takes facts away (NightCycle: caught, the taken thing crossed out); GraceAtHome
    /// writes none.
    /// </summary>
    public static IEnumerable<(string from, NotebookFactData fact)> EveryFact(string graceLooks)
    {
        foreach (NotebookFactData f in NotebookEntries.GraceIntake(NotebookEntries.GraceName))
            yield return ("NotebookEntries.GraceIntake (her camera, Day 1)", f);
        foreach (GracePhotoOutcome o in new[] { GracePhotoOutcome.Clear, GracePhotoOutcome.Imperfect, GracePhotoOutcome.Missed })
            yield return ("NotebookEntries.GraceReturn (" + o + ")", NotebookEntries.GraceReturn(NotebookEntries.GraceName, o));
        yield return ("NotebookEntries.RegularRepair (another regular's repair)", NotebookEntries.RegularRepair("tomas", "Tomas", "Phone", "Cracked Screen"));
        yield return ("NotebookEntries.HomeSeen (came out of her door)", NotebookEntries.HomeSeen(NotebookEntries.GraceId, NotebookEntries.GraceName,
            graceLooks, "12", LodgerStory.GraceStreet, true, Notebook.Sureness.Hunch));
        yield return ("NotebookEntries.HomeSeen (went in at her door)", NotebookEntries.HomeSeen(NotebookEntries.GraceId, NotebookEntries.GraceName,
            graceLooks, "12", LodgerStory.GraceStreet, false, Notebook.Sureness.Likely));
        foreach (NightThing t in NightThings.All)
        {
            yield return ("NightThings.Mentioned (" + t.id + ")", NightThings.Mentioned(t, NotebookEntries.GraceName));
            yield return ("NightThings.Complained (" + t.id + ")", NightThings.Complained(t, NotebookEntries.GraceName));
            yield return ("NightThings.Taken (" + t.id + ", for the shelf)", NightThings.Taken(t, NotebookEntries.GraceName, false));
            yield return ("NightThings.Taken (" + t.id + ", for him)", NightThings.Taken(t, NotebookEntries.GraceName, true));
            yield return ("NightThings.Suspects (" + t.id + ")", NightThings.Suspects(t, NotebookEntries.GraceName));
        }
        foreach (NotebookFactData p in LodgerStory.Pages(null)) yield return ("LodgerStory.Pages (Night 0's hand-over)", p);
        foreach (LodgerStory.Favour f in LodgerStory.Favours)
        {
            NotebookFactData page = LodgerStory.PageFor(f.id, null);
            if (page != null) yield return ("LodgerStory.PageFor (" + f.id + "'s return)", page);
            NotebookFactData note = LodgerStory.NotePage(f.id);
            if (note != null) yield return ("LodgerStory.NotePage (" + f.id + ": his note, the third skip)", note);
        }
        foreach (OfficerStory.Question q in OfficerStory.Questions)
        {
            yield return ("OfficerStory.Asked (" + q.id + ")", OfficerStory.Asked(q, OfficerStory.DefaultName));
            yield return ("OfficerStory.Flinched (" + q.id + ")", OfficerStory.Flinched(q, OfficerStory.DefaultName));
        }
        foreach (LodgerStory.Favour f in LodgerStory.Favours)
        {
            OfficerStory.Question word = OfficerStory.Word(f.id);
            if (word == null) continue;
            yield return ("OfficerStory.Asked (" + word.id + ")", OfficerStory.Asked(word, OfficerStory.DefaultName));
            yield return ("OfficerStory.Flinched (" + word.id + ")", OfficerStory.Flinched(word, OfficerStory.DefaultName));
        }
        // 24 Oct 2026 (Days): a seated guest's gossip (GuestGossip), filed under who it's about.
        foreach (NotebookEntries.Gossip g in NotebookEntries.GossipTable)
            yield return ("NotebookEntries.Overheard (" + g.id + ": a guest's gossip)", NotebookEntries.Overheard(g));
    }

    /// <summary>A short report of the entries: "now barnaby 'Barnaby': Grace's gnome. Bring it to him. (2 facts)"; checks and logs.</summary>
    public static string Describe(IReadOnlyList<NoteEntry> entries)
    {
        if (entries == null || entries.Count == 0) return "(no entries)";
        var lines = new List<string>(entries.Count);
        foreach (NoteEntry e in entries)
            lines.Add($"{(e.now ? "now " : "")}{e.group} {e.key} '{e.title}': {Plain(e.line)} ({e.facts.Count} fact{(e.facts.Count == 1 ? "" : "s")})");
        return string.Join("\n", lines);
    }

    /// <summary>The words of a rich-text line (tags dropped), for logs and checks.</summary>
    public static string Plain(string rich)
    {
        if (string.IsNullOrEmpty(rich)) return "";
        var chars = new char[rich.Length];
        int n = 0;
        bool inTag = false;
        foreach (char c in rich)
        {
            if (inTag) { if (c == '>') inTag = false; continue; }
            if (c == '<') { inTag = true; continue; }
            chars[n++] = c;
        }
        return new string(chars, 0, n);
    }
}
