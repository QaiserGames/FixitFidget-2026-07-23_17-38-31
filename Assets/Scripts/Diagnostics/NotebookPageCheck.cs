using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE NIGHT'S NOTEBOOK PAGE, WITH PICTURES - PLAY CHECK (23 Oct 2026)
//
//   Fixit Fidget > Checks > Night notebook (Play Mode, lab session)   (NotebookPlayCheck starts this after its own checks)
//
// His note (22 Oct): the night's notes were "just a cluster of words, it's word vomit". The page is pictures first now
// (NightNotebook, NoteSubjects, NotePhotos). This drives the real page with notebooks and nights made up for the check (the
// save's notebook and night record are never touched, so the lab save isn't either), photographs it, and checks:
//   * every subject has a picture (a photo, or the drawing made in code), and every fact the game can write today
//     (NoteSubjects.EveryFact) has a subject with a picture or a declared drawing;
//   * tonight's errand is on top, in amber, in the Now line's names (Barnaby, 12 West Street; the bag at the back door);
//   * the first screen holds at most six entries (NightNotebook.MaxFirstScreen), the rest scrolls;
//   * no line on it is over 8 words (WordBudget's count, NoteSubjects.LineWords); every word is 26 px or more;
//   * the press (D-pad down through a virtual pad, Tab's twin) opens the next entry's notes, whole: the man at the bins'
//     pages too, though he sits third (the page moves an open entry up rather than cut its notes); it passes over an
//     entry with nothing behind it (the bag's back door) and walks down the list;
//   * each photo's word check (ReviewScreen) finds no S1 or S2 among the page's own words.
// By day, in the café lab (the page draws over the café the same as over the street: it is its own dark card).
// Photos and the report: Logs/Night/notebook-page-<time>/.
// ---------------------------------------------------------------------------
public sealed class NotebookPageCheck : PlayLab
{
    protected override string Title => "The night's notebook page, with pictures - play check";
    protected override string Tag => "[Notebook page]";

    NightNotebook page;
    GameObject host;

    /// <summary>Starts the check in this Play session (the notebook play check's menu calls it); its report goes under Logs/Night.</summary>
    public static NotebookPageCheck Begin()
    {
        var go = new GameObject("Notebook page check (this Play session only)");
        var check = go.AddComponent<NotebookPageCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            "notebook-page-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss")));
        return check;
    }

    protected override void Restore()
    {
        if (page != null) page.End();
        if (host != null) Destroy(host);
    }

    protected override IEnumerator Run()
    {
        if (!CafeLab.Active) Note("not a lab session: the page is drawn from notebooks made up for the check either way; nothing is saved");
        // The street names the page reads (District streets: loaded before the scene in Play).
        string west = StreetNames.Name(LodgerStory.GraceStreet);

        // ---------- the pictures ----------
        int photographed = 0;
        foreach (NoteSubject s in NoteSubjects.All)
        {
            Sprite picture = NotePhotos.Picture(s.key);
            bool photo = NotePhotos.HasPhoto(s.key);
            if (photo) photographed++;
            Check(picture != null && picture.texture != null, $"{s.key}: a picture ({(photo ? "a photo" : "drawn in code: " + s.draw)})");
        }
        Note($"{photographed} of {NoteSubjects.All.Count} subjects have a photo (Fixit Fidget > Night > Notebook pictures 1 takes them); the rest are drawn.");
        var missing = new List<string>();
        int facts = 0;
        foreach (var item in NoteSubjects.EveryFact(HomeDoor.GraceLooks))
        {
            facts++;
            NotebookFactData fact = item.fact;
            string key = NoteSubjects.KeyOf(fact);
            NoteSubject subject = NoteSubjects.Find(key);
            bool photo = subject != null && NotePhotos.HasPhoto(key);
            bool drawn = subject != null && Array.IndexOf(NotePhotos.Drawings, subject.draw) >= 0;
            if (fact == null || subject == null || !photo && !drawn) missing.Add(item.from + " " + (fact != null ? fact.id : "(none)"));
            Note($"  {item.from}: {(fact != null ? fact.id : "(none)")} -> {key} ({(photo ? "photo" : drawn ? "drawn: " + subject.draw : "NO PICTURE")})");
        }
        Check(missing.Count == 0, $"every fact the game can write ({facts}) has a subject with a picture or a declared drawing" +
                                  (missing.Count > 0 ? ": not " + string.Join("; ", missing) : ""));

        // ---------- the page ----------
        host = new GameObject("Night notebook page (the check's)");
        host.transform.SetParent(transform, false);
        page = host.AddComponent<NightNotebook>();
        page.Begin();

        // Night 0, after the deal: Grace's day, his pages; the gnome to take.
        Notebook night0 = new Notebook();
        foreach (NotebookFactData f in NotebookEntries.GraceIntake(NotebookEntries.GraceName)) night0.Learn(f, 1);
        night0.Learn(NotebookEntries.HomeSeen(NotebookEntries.GraceId, NotebookEntries.GraceName, HomeDoor.GraceLooks, "12", LodgerStory.GraceStreet,
            true, Notebook.Sureness.Hunch), 1);
        night0.Learn(NightThings.Mentioned(NightThings.GnomeOfGrace, NotebookEntries.GraceName), 1);
        foreach (NotebookFactData p in LodgerStory.Pages(NightZero.InTheGame)) night0.Learn(p, 1);
        var ledger = new NightLedger();
        ledger.Meet(1);
        string errand = LodgerStory.Errand(ledger);
        Check(errand == NightThings.GraceGnome, $"after the deal his errand is the gnome ({errand})");
        page.UseForChecks(night0, ledger, NoteStage.ToTake, errand);
        page.Show(true);
        yield return Frames(3);
        Check(page.Open, "the page is open");
        string now = LodgerStory.Now(LodgerStory.FindFavour(errand), NightThings.GnomeOfGrace.name);
        Note("the Now line for this moment reads: \"" + now + "\"");
        CheckTop(NoteSubjects.Barnaby, NightThings.GnomeOfGrace.name, NoteSubjects.GraceHouse, "12 " + west, now);
        CheckScreen("the gnome to take");
        yield return PagePhoto("01-tonight-the-gnome");

        // The gnome in hand: bring it to him.
        page.UseForChecks(night0, ledger, NoteStage.InHand, errand);
        yield return Frames(2);
        Shown(0, out NightNotebook.Shown a);
        Shown(1, out NightNotebook.Shown b);
        Check(a != null && a.entry.key == NoteSubjects.Barnaby && a.entry.now && Plain(a.line.text) == NoteSubjects.BringLine
              && b != null && b.entry.key == NoteSubjects.Lodger && b.entry.now,
            $"in hand: Barnaby (\"{Plain(a?.line.text)}\"), then the man at the bins, both tonight's (the Now line: \"Bring Barnaby to the man at the bins\")");
        CheckScreen("the gnome in hand");
        yield return PagePhoto("02-the-gnome-in-hand");

        // The press: everything about an entry, then the next, then none.
        page.UseForChecks(night0, ledger, NoteStage.ToTake, errand);
        yield return Frames(2);
        page.More();
        yield return Frames(2);
        Shown(0, out NightNotebook.Shown first);
        Check(page.OpenKey == NoteSubjects.Barnaby && first != null && first.open, $"a press opens the first entry's notes ({page.OpenKey})");
        if (first != null)
        {
            int written = first.entry.facts.Count;
            Check(first.cut == 0 && first.notes.Count == written && first.notes.All(t => t.fontSize >= ReviewScreen.SmallPx),
                $"...all {written} of them, whole, at {NightNotebook.NoteSize:0} px ({first.notes.Count} shown, {first.cut} left for the phone)");
            Check(first.notes.All(t => !WordBudget.Over(Plain(t.text), WordBudget.Page)),
                $"...each within the notebook's own budget ({WordBudget.Page} words)");
        }
        yield return PagePhoto("03-pressed-for-everything");
        PlugInPad("Notebook page check pad");
        yield return Frames(2);
        string before = page.OpenKey;
        yield return PadPress(GamepadButton.DpadDown);
        yield return Frames(2);
        string second = page.Entries.Count > 1 ? page.Entries[1].key : "";
        Check(page.OpenKey == second && second.Length > 0, $"D-pad down (the pad's press) opens the next entry's ({before} -> {page.OpenKey})");
        // The third entry: the man at the bins and his three pages. Where he sits there's no room for them all; the page
        // moves him up rather than cut them (the moment of Night 0's hand-over: "His notebook is yours now. Read it.").
        page.More();
        yield return Frames(2);
        NightNotebook.Shown him = page.Visible.FirstOrDefault(v => v.open);
        Check(him != null && him.entry.key == NoteSubjects.Lodger && him.cut == 0 && him.notes.Count == him.entry.facts.Count,
            $"...the man at the bins' pages, whole wherever he sits ({him?.notes.Count} of {him?.entry.facts.Count}, {him?.cut} left for the phone)");
        for (int i = 0; i < page.Entries.Count && page.OpenKey.Length > 0; i++) page.More();
        yield return Frames(2);
        Check(page.OpenKey.Length == 0 && !page.MoreAbove, "past the last entry the press closes them all, back to the first screen");

        // Night 2: the gnome given, the cups to take.
        ledger.Take(NightThings.GraceGnome, NotebookEntries.GraceId, 1);
        ledger.Give(NightThings.GraceGnome);
        ledger.Ask(2);
        errand = LodgerStory.Errand(ledger);
        Notebook night2 = new Notebook();
        night2.Restore(night0.Snapshot());
        night2.Learn(LodgerStory.PageFor(NightThings.GraceGnome, NightZero.InTheGame), 1);
        night2.Learn(NightThings.Mentioned(NightThings.CupsOfGrace, NotebookEntries.GraceName), 2);
        page.UseForChecks(night2, ledger, NoteStage.ToTake, errand);
        yield return Frames(2);
        now = LodgerStory.Now(LodgerStory.FindFavour(errand), NightThings.CupsOfGrace.name);
        Note("the Now line for this moment reads: \"" + now + "\"");
        CheckTop(NoteSubjects.Cups, Capital(NightThings.CupsOfGrace.name), NoteSubjects.GraceHouse, "12 " + west, now);
        NoteEntry barnaby = page.Entries.FirstOrDefault(e => e.key == NoteSubjects.Barnaby);
        Check(barnaby != null && barnaby.group == NoteGroup.PlacesAndThings && barnaby.line == NoteSubjects.GivenLine,
            $"Barnaby, given, is with the man at the bins now (\"{Plain(barnaby?.line)}\")");
        CheckScreen("the cups to take");
        yield return PagePhoto("04-tonight-the-cups");

        // Night 0's first step: the bag, at the back door (before the deal: the man is no one yet).
        var fresh = new Notebook();
        foreach (NotebookFactData f in NotebookEntries.GraceIntake(NotebookEntries.GraceName)) fresh.Learn(f, 1);
        page.UseForChecks(fresh, new NightLedger(), NoteStage.BagToDoor, "");
        yield return Frames(2);
        Shown(0, out NightNotebook.Shown bag);
        Check(bag != null && bag.entry.key == NoteSubjects.BackDoor && bag.entry.now && Plain(bag.line.text).StartsWith("Take the rubbish out the back door", StringComparison.Ordinal),
            $"the bag: the back door on top, in the Now line's words (\"{Plain(bag?.line.text)}\")");
        Check(page.Entries.All(e => e.key != NoteSubjects.Lodger), "before the deal the man at the bins isn't on the page (it would give the reveal away)");
        CheckScreen("the bag");
        yield return PagePhoto("05-the-bag");
        // The back door has nothing written behind it: the press passes over it to the first entry that has (Grace).
        page.More();
        yield return Frames(2);
        Check(page.OpenKey == NoteSubjects.Grace, $"the press passes over the back door (nothing behind it) to Grace's notes ({page.OpenKey})");
        for (int i = 0; i < page.Entries.Count && page.OpenKey.Length > 0; i++) page.More();

        // A long notebook (and an old save's lines): the first screen still holds at most six; the press walks down.
        Notebook longer = new Notebook();
        longer.Restore(night2.Snapshot());
        string[] regulars = { "Tomas", "Priya", "Marcus", "June", "Ines" };
        for (int i = 0; i < regulars.Length; i++)
            longer.Learn(NotebookEntries.RegularRepair(regulars[i].ToLowerInvariant(), regulars[i], i % 2 == 0 ? "Phone" : "Pocket Watch", "cracked screen"), 3);
        foreach (OfficerStory.Question q in OfficerStory.Questions.Take(1))
        {
            longer.Learn(OfficerStory.Asked(q, OfficerStory.DefaultName), 3);
            longer.Learn(OfficerStory.Flinched(q, OfficerStory.DefaultName), 3);
        }
        longer.Learn(new NotebookFactData { id = "grace.home.old", who = NotebookEntries.GraceId, name = NotebookEntries.GraceName, kind = Notebook.Kinds.Address,
            text = "Came out of the saffron house at 12 {street:west}.", source = Notebook.Sources.Seen, sure = Notebook.Sureness.Hunch }, 1);
        page.UseForChecks(longer, ledger, NoteStage.ToTake, errand);
        yield return Frames(2);
        Check(page.Entries.Count > NightNotebook.MaxFirstScreen && page.MoreBelow,
            $"a long notebook ({page.Entries.Count} entries) doesn't fit: the rest is below");
        CheckScreen("a long notebook");
        yield return PagePhoto("06-a-long-notebook");
        // One press for each entry with something behind it (the press passes over the rest) ends on the last of them.
        int last = -1, presses = 0;
        for (int i = 0; i < page.Entries.Count; i++)
            if (page.Entries[i].facts.Count > 0) { last = i; presses++; }
        for (int i = 0; i < presses; i++) page.More();
        yield return Frames(2);
        Shown(0, out NightNotebook.Shown top);
        Check(last >= 0 && page.OpenKey == page.Entries[last].key && page.MoreAbove && page.Visible.Any(v => v.entry.key == page.OpenKey && v.open),
            $"pressed down to the last entry, the list has scrolled to it ({page.OpenKey} open, {top?.entry.key} on top)");
        Check(page.Visible.Count <= NightNotebook.MaxFirstScreen, $"...still at most {NightNotebook.MaxFirstScreen} entries on the screen ({page.Visible.Count})");
        yield return PagePhoto("07-scrolled-to-the-last");

        // Nothing written, no night: says so.
        page.UseForChecks(new Notebook(), null, NoteStage.None, "");
        yield return Frames(2);
        Check(page.Entries.Count == 0 && page.Visible.Count == 0, "an empty notebook has no entries (the page says nothing is written down yet)");

        page.UseForChecks(null, null, NoteStage.None, "");
        page.Show(false);
        yield return Frames(1);
        Check(!page.Open, "it closes");
        Note(page.Describe());
    }

    // ---------- checks ----------

    // Tonight's two on top, in amber: the thing (named as the Now line names it) and where it is (the address).
    void CheckTop(string thingKey, string thingTitle, string placeKey, string placeTitle, string nowLine)
    {
        Shown(0, out NightNotebook.Shown a);
        Shown(1, out NightNotebook.Shown b);
        Check(a != null && a.entry.key == thingKey && a.entry.now && a.entry.title == thingTitle,
            $"tonight's errand is on top: {a?.entry.key} \"{a?.entry.title}\", tonight's: {a?.entry.now}");
        Check(b != null && b.entry.key == placeKey && b.entry.now && b.entry.title == placeTitle,
            $"...then where it is: {b?.entry.key} \"{b?.entry.title}\"");
        Check(nowLine.Contains(thingTitle) || nowLine.Contains(thingTitle.ToLowerInvariant()),
            $"...named as the Now line names it (\"{thingTitle}\" in \"{nowLine}\")");
        Check(page.Headings.Count > 0 && page.Headings[0].text == "Tonight" && page.Headings[0].color == NotePhotos.Next,
            "...under \"Tonight\", in the bench's amber");
        Check(a != null && a.row.Find("Now bar") != null && a.row.Find("Polaroid - " + thingKey) != null,
            "...with the amber bar and its polaroid (an amber pin)");
        bool mapped = NotePhotos.Map(placeKey, true) != null;   // a spot to pin (or a map drawn by hand)
        Check(b != null && (b.row.Find("Map - " + placeKey) != null) == mapped,
            mapped ? "...and the place has its street map card" : "...the place has no spot yet (Notebook pictures 1 measures it): no map card, no harm");
    }

    // What's on the screen: at most six entries; each line at most 8 words; every word 26 px or more.
    void CheckScreen(string when)
    {
        int count = page.Visible.Count;
        Check(count >= 1 && count <= NightNotebook.MaxFirstScreen, $"{when}: {count} entries on the screen (at most {NightNotebook.MaxFirstScreen})");
        var over = new List<string>();
        var small = new List<string>();
        foreach (NightNotebook.Shown s in page.Visible)
        {
            if (WordBudget.Words(s.line.text) > NoteSubjects.LineWords) over.Add($"\"{Plain(s.line.text)}\" ({WordBudget.Words(s.line.text)})");
            foreach (TMP_Text t in new[] { s.title, s.line }.Concat(s.notes))
                if (t.fontSize < ReviewScreen.SmallPx) small.Add($"\"{Short(Plain(t.text))}\" {t.fontSize:0} px");
        }
        foreach (TMP_Text h in page.Headings) if (h.fontSize < ReviewScreen.SmallPx) small.Add($"heading {h.text} {h.fontSize:0} px");
        if (page.Footer.fontSize < ReviewScreen.SmallPx) small.Add($"footer {page.Footer.fontSize:0} px");
        Check(over.Count == 0, $"{when}: no line on the page over {NoteSubjects.LineWords} words" + (over.Count > 0 ? ": " + string.Join(", ", over) : ""));
        Check(small.Count == 0, $"{when}: every word on it is {ReviewScreen.SmallPx:0} px or more" + (small.Count > 0 ? ": " + string.Join(", ", small) : ""));
        Check(page.Visible.All(s => s.title.fontSize == NightNotebook.TitleSize && (s.title.fontStyle & FontStyles.Bold) != 0),
            $"{when}: each name is bold, {NightNotebook.TitleSize:0} px");
        Check(page.Footer.text.Contains("Close") && page.Footer.text.Contains(NightNotebook.MoreKey),
            $"{when}: the footer shows the press's key as an icon (\"{Short(page.Footer.text)}\")");
    }

    // A photo, then its word check, read back: no S1 or S2 among the page's own words (anything else on the screen is
    // another screen's, reported by Review › Screens 1).
    IEnumerator PagePhoto(string name)
    {
        yield return Photo(name);
        string file = Path.Combine(folder, name + ".text.json");
        if (!ReviewScreen.Enabled || !File.Exists(file))
        {
            Note($"{name}: the word check is off this session (or wrote nothing), so the page's words weren't measured");
            yield break;
        }
        ReviewScreen.PhotoCheck check = null;
        try { check = JsonUtility.FromJson<ReviewScreen.PhotoCheck>(File.ReadAllText(file)); }
        catch (Exception e) { Note($"{name}: its word check couldn't be read ({e.Message})"); }
        if (check == null) yield break;
        List<ReviewScreen.TextMeasure> mine = check.items.Where(m => m.owner == nameof(NightNotebook)).ToList();
        List<ReviewScreen.TextMeasure> bad = mine.Where(m => m.severity == 1 || m.severity == 2).ToList();
        Check(mine.Count > 0 && bad.Count == 0,
            $"{name}: the word check finds no S1 or S2 among the page's {mine.Count} texts" +
            (bad.Count > 0 ? ": " + string.Join("; ", bad.Select(m => $"S{m.severity} \"{Short(m.text)}\" {m.reasons}")) : ""));
    }

    void Shown(int index, out NightNotebook.Shown shown) => shown = index < page.Visible.Count ? page.Visible[index] : null;

    static string Plain(string rich) => NoteSubjects.Plain(rich ?? "");

    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
