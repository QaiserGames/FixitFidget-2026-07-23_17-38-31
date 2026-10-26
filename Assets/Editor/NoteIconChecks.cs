#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// NOTE ICONS, EDIT-MODE CHECK (26 Oct 2026 (Icons on notes))
//
// His note: "the notes that are given to you from that guy, or any notes, should have icons so that its easier for the player
// to understand what to do ... only on times when we must." Designer I's table (review24/designs/I/README.md §1) says which
// lines are "yes"; NoteIcons applies it. This check proves, with no scene and no save:
//   1. the ten chips are on the icon sheet (button_icons.json) and in TextMesh Pro's default sprite asset (if not: run
//      Tools/UI/draw_button_icons.py, then Fixit Fidget › UI › Button icons, then this again);
//   2. every marked day line gets its chip: the refill and stock lines the back shelf, the crate the machine, every table
//      line (with "Nina's on the way." before it too) the table, the thrift find the thrift chip; his "Clear the tables"
//      the table; his three asks (by line id, in BarkSteps' table) the gnome, the cups and the cones;
//   3. no unmarked line gets one: the guided steps (most with a button's icon), the guest's chat, the status lines, his other
//      lines (every other line id in BarkSteps), his table note, the night's last-job note, the lessons;
//   4. a chip is never a word (WordBudget counts the same with it) and never a second icon (one <sprite> in the line).
//   5. 26 Oct 2026 (Icons 2): the furniture pictures (furn_*), the tickets' items (item_*) and the facts' kind chips (kind_*,
//      and kind_*_his) are on the sheet and in TMP's asset, and each cuts as a sprite for an Image; every Notebook.Kinds has
//      its chip and every fact the game can write gets one; the notebook's tonight chip follows the Now line's table by
//      stage; every kind the Shop sells (NoteIcons.FurnitureKinds, and the catalogue asset's kinds when it is there) has a
//      picture; every device a ticket can carry (and the six drinks) has one.
// The night's Now line (its states) and the drawn chips are proved in Play mode: Night 0, Night 2, Waiting jobs, Day 1 guide;
// the notebook page's chips by the Night notebook check, the furniture's by the Furniture lab.
// Report: <project>/Logs/NoteIcons/note-icons-check.txt. Last line: "[Note icons] All N checks passed." or "N of M FAILED".
// ---------------------------------------------------------------------------
public static class NoteIconChecks
{
    const string Tag = "[Note icons]";

    [MenuItem("Fixit Fidget/Checks/Note icons (edit mode)")]
    public static void Run()
    {
        var report = new StringBuilder();
        int checks = 0, failures = 0;
        void Check(bool ok, string what)
        {
            checks++;
            if (!ok) failures++;
            report.AppendLine((ok ? "pass  " : "FAIL  ") + what);
        }

        try
        {
            // 1. The sheet and the sprite asset.
            string json = File.Exists(ButtonIconSteps.JsonPath) ? File.ReadAllText(ButtonIconSteps.JsonPath) : "";
            TMP_SpriteAsset sheet = TMP_Settings.instance != null ? TMP_Settings.defaultSpriteAsset : null;
            foreach (string name in NoteIcons.All)
            {
                Check(json.Contains("\"" + name + "\""), $"{name} is on the sheet ({ButtonIconSteps.JsonPath})");
                Check(sheet != null && sheet.GetSpriteIndexFromName(name) >= 0 && NoteIcons.Tag(name).Length > 0,
                    $"{name} is in TMP's default sprite asset ({(sheet != null ? sheet.name : "none")}): if not, run Fixit Fidget › UI › Button icons");
            }

            // 2 and 4. The marked lines.
            var marked = new List<(string line, string icon)>
            {
                (DayOneGuideUI.RefillWords(true, false), NoteIcons.BackShelf),
                (DayOneGuideUI.RefillWords(false, true), NoteIcons.BackShelf),
                (DayOneGuideUI.RefillWords(true, true), NoteIcons.BackShelf),
                (DayOneGuideUI.NoCupsLeftWords, NoteIcons.BackShelf),
                (DayOneGuideUI.NoBeansLeftWords, NoteIcons.BackShelf),
                (DayOneGuideUI.CarryCrateWords, NoteIcons.Machine),
                (DayOneGuideUI.FindWords(1, "watch"), NoteIcons.Thrift),
                (DayOneGuideUI.FindWords(2, "phone"), NoteIcons.Thrift),
                (DayOneGuideUI.OnTheWayWords("Nina") + " " + DayOneGuideUI.TableWords(SeatTag.Window), NoteIcons.Table),
            };
            foreach (SeatTag t in new[] { SeatTag.Window, SeatTag.Lounge, SeatTag.Bar, SeatTag.Communal, SeatTag.Quiet, SeatTag.NearDoor, SeatTag.None })
                marked.Add((DayOneGuideUI.TableWords(t), NoteIcons.Table));
            foreach (var (line, icon) in marked)
            {
                string got = NoteIcons.ForDayLine(line);
                string drawn = NoteIcons.Lead(got) + line;
                Check(got == icon, $"\"{line}\" wears {icon} (got \"{got}\")");
                Check(WordBudget.Words(drawn) == WordBudget.Words(line) && !WordBudget.Over(line, WordBudget.NowLine),
                    $"...its chip is no word: {WordBudget.Report(drawn, WordBudget.NowLine)}");
                Check(Count(drawn, "<sprite") == 1, "...and the one icon in the line");
            }
            Check(NoteIcons.ForNote(LodgerStory.MessNote) == NoteIcons.Table, $"\"{LodgerStory.MessNote}\" wears {NoteIcons.Table}");
            Check(NoteIcons.ForThing(NightThings.GraceGnome) == NoteIcons.Gnome && NoteIcons.ForThing(NightThings.GraceCups) == NoteIcons.Cups
                  && NoteIcons.ForThing(LodgerStory.Cones) == NoteIcons.Cones && NoteIcons.ForThing("") == "",
                "the night's things: the gnome, the reunion cups, the cones (the notebook's \"now\" subjects)");

            // His lines, by id, from BarkSteps' table (the asks, and only the asks).
            FieldInfo field = typeof(BarkSteps).GetField("Lines", BindingFlags.NonPublic | BindingFlags.Static);
            var lines = field != null ? field.GetValue(null) as ValueTuple<string, string, string, string>[] : null;
            Check(lines != null && lines.Length > 0, "BarkSteps' line table is readable");
            if (lines != null)
            {
                foreach (var (askId, icon) in NoteIcons.Asks)
                    Check(Array.Exists(lines, l => l.Item1 == askId && l.Item2 == LodgerStory.SpeakerId), $"his ask {askId} is one of his lines ({icon})");
                int others = 0;
                var wrong = new List<string>();
                foreach (var l in lines)
                {
                    bool ask = Array.Exists(NoteIcons.Asks, a => a.id == l.Item1);
                    string got = NoteIcons.ForLineId(l.Item1);
                    if (!ask) others++;
                    if (ask ? got.Length == 0 : got.Length > 0) wrong.Add($"{l.Item1} \"{got}\"");
                }
                Check(wrong.Count == 0, $"only the {NoteIcons.Asks.Length} asks wear a chip; the other {others} lines are words ({(wrong.Count == 0 ? "none wrong" : string.Join(", ", wrong))})");
            }

            // 3. The unmarked lines.
            var unmarked = new List<string>
            {
                DayOneGuideUI.FirstNow("Grace", true), DayOneGuideUI.TalkWords("Grace", false, false), DayOneGuideUI.TakeDrinkWords("latte", "Grace"),
                DayOneGuideUI.PickUpFromTray("Grace", "film camera"), DayOneGuideUI.ReassureWords("Grace"), DayOneGuideUI.WalkOverReassureWords("Grace"),
                DayOneGuideUI.GossipWords(SeatTag.Lounge), DayOneGuideUI.GossipWords(SeatTag.None), DayOneGuideUI.OnTheWayWords("Nina"),
                DayOneGuideUI.ClosingWords, DayOneGuideUI.OnHoldLead + " Work on another job.", "Walk to the drinks machine on the back bar.",
                "Wait for Nina to reach the counter.",
            };
            foreach (string line in unmarked) Check(NoteIcons.ForDayLine(line) == "", $"no chip: \"{line}\" (got \"{NoteIcons.ForDayLine(line)}\")");
            foreach (string note in new[] { LodgerStory.TableNote, NightCycle.LastJobNote })
                Check(NoteIcons.ForNote(note) == "" && NoteIcons.ForDayLine(note) == "", $"no chip: \"{note}\"");
            foreach (LodgerStory.Lesson lesson in LodgerStory.Lessons)
                Check(NoteIcons.ForNote(lesson.learned) == "" && NoteIcons.ForDayLine(lesson.learned) == "", $"no chip on a lesson: \"{lesson.learned}\"");

            // 5. 26 Oct 2026 (Icons 2): the furniture, the tickets' items, the facts' kinds.
            var more = new List<string>();
            foreach (var pair in NoteIcons.FurnitureKinds) more.Add(pair.icon);
            more.AddRange(NoteIcons.TicketItems);
            foreach (string kind in NoteIcons.Kinds) { more.Add(kind); more.Add(kind + NoteIcons.HisSuffix); }
            foreach (string name in more)
            {
                Check(json.Contains("\"" + name + "\""), $"{name} is on the sheet");
                Check(sheet != null && sheet.GetSpriteIndexFromName(name) >= 0 && NoteIcons.Tag(name).Length > 0, $"{name} is in TMP's default sprite asset");
            }
            foreach (var pair in NoteIcons.FurnitureKinds)
            {
                Sprite cut = NoteIcons.SpriteOf(pair.icon);
                Check(cut != null && cut.rect.width >= 100f && cut.rect.height >= 100f, $"{pair.icon} cuts as a sprite for an Image ({(cut != null ? cut.rect.width + " x " + cut.rect.height : "null")})");
            }
            foreach (string item in NoteIcons.TicketItems)
                Check(NoteIcons.SpriteOf(item) != null, $"{item} cuts as a sprite for an Image");
            // The kinds: every Notebook.Kinds value, a blank kind (an old save's), and his pages on his paper.
            var kinds = new (string kind, string icon)[]
            {
                (Notebook.Kinds.Address, NoteIcons.KindWhere), (Notebook.Kinds.Possession, NoteIcons.KindOwns), (Notebook.Kinds.Schedule, NoteIcons.KindWhen),
                (Notebook.Kinds.Claim, NoteIcons.KindSaid), (Notebook.Kinds.Relationship, NoteIcons.KindWho), (Notebook.Kinds.Secret, NoteIcons.KindSecret),
            };
            foreach (var (kind, icon) in kinds)
                Check(NoteIcons.ForKind(kind, false) == icon && NoteIcons.ForKind(kind, true) == icon + NoteIcons.HisSuffix,
                    $"a {kind} fact wears {icon} (his page: {icon}{NoteIcons.HisSuffix})");
            Check(NoteIcons.ForKind("", false) == NoteIcons.KindSaid && NoteIcons.ForKind(null, true) == NoteIcons.KindSaid + NoteIcons.HisSuffix,
                "a fact with no kind (an old save's) is \"what was said\"");
            int factsChecked = 0;
            var noChip = new List<string>();
            foreach (var item in NoteSubjects.EveryFact(HomeDoor.GraceLooks))
            {
                factsChecked++;
                string chip = NoteIcons.ForFact(item.fact);
                if (chip.Length == 0 || NoteIcons.Tag(chip).Length == 0 || (item.fact.source == Notebook.Sources.Inherited) != chip.EndsWith(NoteIcons.HisSuffix, StringComparison.Ordinal))
                    noChip.Add(item.from + " " + item.fact.id + " -> \"" + chip + "\"");
            }
            Check(factsChecked > 0 && noChip.Count == 0, $"every fact the game can write ({factsChecked}) has its kind's chip on the sheet, his pages on his paper"
                                                       + (noChip.Count > 0 ? ": not " + string.Join("; ", noChip) : ""));
            // Tonight's chip on the notebook page: the Now line's table (NightCycle.NowLine's icons), by stage.
            Check(NoteIcons.ForStage(NoteStage.BagToDoor, "") == NoteIcons.Door && NoteIcons.ForStage(NoteStage.BagToBins, "") == NoteIcons.Bins
                  && NoteIcons.ForStage(NoteStage.InHand, NightThings.GraceGnome) == NoteIcons.Bins && NoteIcons.ForStage(NoteStage.ToTake, NightThings.GraceGnome) == NoteIcons.Gnome
                  && NoteIcons.ForStage(NoteStage.ToTake, NightThings.GraceCups) == NoteIcons.Cups && NoteIcons.ForStage(NoteStage.OnShelf, NightThings.GraceGnome) == NoteIcons.Gnome
                  && NoteIcons.ForStage(NoteStage.Home, "") == NoteIcons.Door && NoteIcons.ForStage(NoteStage.None, NightThings.GraceGnome) == "",
                "the notebook's tonight chip follows the Now line's table: the door for the bag and for calling it a night, the bins in hand, the thing to take or on the shelf, none with no night");
            // The furniture: every kind the Shop sells has a picture (the asset names are the save identity; FurnitureSteps' table).
            foreach (string kindName in new[] { "Bistro table", "Square table", "Plant", "Banquette bay", "Window counter", "Lounge corner", "Communal table" })
                Check(NoteIcons.Furniture(kindName).Length > 0, $"the kind \"{kindName}\" has a picture ({NoteIcons.Furniture(kindName)})");
            var catalogue = AssetDatabase.LoadAssetAtPath<FurnitureCatalogue>(FurnitureSteps.CataloguePath);
            if (catalogue != null && catalogue.kinds != null)
                foreach (FurnitureDefinition k in catalogue.kinds)
                    Check(k == null || NoteIcons.SpriteOf(NoteIcons.Furniture(k.name)) != null, $"the catalogue's \"{(k != null ? k.name : "null")}\" has its picture on the sheet");
            else report.AppendLine("note  no furniture catalogue asset yet (" + FurnitureSteps.CataloguePath + "): the seven kinds checked by name");
            // The tickets' items: the devices a ticket can carry, and the six drinks the tickets already show.
            Check(NoteIcons.ForDevice("Phone") == NoteIcons.ItemPhone && NoteIcons.ForDevice("Pocket Watch") == NoteIcons.ItemWatch
                  && NoteIcons.ForDevice("reunion film camera") == NoteIcons.ItemCamera && NoteIcons.ForDevice("thrift watch") == NoteIcons.ItemWatch
                  && NoteIcons.ForDevice("thrift phone") == NoteIcons.ItemPhone && NoteIcons.ForDevice("") == "",
                "the devices a ticket can carry have their pictures: Phone, Pocket Watch, the reunion film camera (and the thrift finds)");
            foreach (string drink in new[] { "drink_latte", "drink_coffee", "drink_tea", "drink_espresso", "drink_hotchocolate", "drink_americano" })
                Check(sheet != null && sheet.GetSpriteIndexFromName(drink) >= 0, $"{drink} is on the sheet beside the items");
        }
        catch (Exception e)
        {
            Check(false, "threw: " + e.Message);
            Debug.LogException(e);
        }

        string summary = failures == 0 ? $"{Tag} All {checks} checks passed." : $"{Tag} {failures} of {checks} checks FAILED. Logs/NoteIcons";
        string text = report + summary;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "NoteIcons");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "note-icons-check.txt"), text);
        }
        catch (Exception e) { Debug.LogWarning($"{Tag} Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log(text);
        else Debug.LogError(text);
    }

    static int Count(string text, string part)
    {
        int n = 0;
        for (int i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
#endif
