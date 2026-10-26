using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

// ---------------------------------------------------------------------------
// ICONS ON NOTES (26 Oct 2026 (Icons on notes))
//
// His note (24 Oct): "the notes that are given to you from that guy, or any notes, should have icons so that its easier for
// the player to understand what to do, and please don't take that literally and do that everywhere, but only on times when
// we must." Designer I audited every note that tells Ace what to do (review24/designs/I/README.md) and he picked A on 26 Oct:
// one round paper chip with a gold rim BEFORE the line, and the same chip on the world pin over the place to go. He kept the
// eyes peeking out of the bins.
//
// The rule (the README's §4), and this file is the one place that applies it:
//   - a chip only on a line that asks Ace to act on a thing or place he has to go and find: his ASK (the line that names the
//     errand), every state of the night's Now line, the day's WHILE YOU WAIT refill, clear and thrift lines, the stock fetch
//     and carry lines, and his "Clear the tables" note (LodgerStory.MessNote);
//   - never on a line that already shows a button icon (the icon is already its picture: two in one line is the clutter he
//     warned about), nor on story, reminders, captions, toasts, lessons, status lines, the notebook page, the bench card, the
//     morning card or the phone.
//     The one exception is the night's notebook nudge (" · [N] notes", until the notebook is first opened): it is the line's
//     chrome, after the step, and the designer's A pictures (3-A-vs-B-crops.png) show the chip with it; dropping the chip
//     there would take it off Night 0's whole first errand.
//   - one chip per line, always first; the words always stay, whole. A chip is a sprite tag, never a word: WordBudget skips
//     tags, so a line's word budget is the same with or without it, and the labs that measure words read the words (the
//     Now line's NowText, a bark's Shown.text and a pin's GuideText are the words alone).
//   - a chip never costs a line: Chip() leaves it off when the words would wrap once more beside it.
//
// The chips are drawn by Tools/UI/draw_button_icons.py on the button-icon sheet (note_*) and reach TextMesh Pro through the
// same sprite asset as every button icon (Fixit Fidget › UI › Button icons). Without that asset, or with an old sheet, every
// chip is "" and the lines are words, as before. Checks: Fixit Fidget › Checks › Note icons (NoteIconChecks), and the
// Now-line, waiting-job and Night 0 / Night 2 labs.
//
// 26 Oct 2026 (Icons 2). His next note: "the icons on both the notebook and notes, i wanted icons for the furniture you can
// also purchase too", and for the tickets "nice big bold icons for each item, like a coffee, tea, etc, clock". Three more sets
// on the same sheet, in the same style, and this file still the one place that says which goes where:
//   - THE NOTEBOOK PAGE (NightNotebook). Tonight's entry wears the Now line's chip (ForStage: the same table as NightCycle's,
//     by the page's stage), before its line, so the HUD's line and the page's "Tonight" match by picture; only the first
//     tonight entry (the one that says what to do), never the second (where it is: its polaroid and map are its picture).
//     Behind the press, each fact's kind (Notebook.Kinds) is a smaller chip in place of the old bullet (ForKind): where
//     someone is, what they own, when they do something, what was said, who they are to someone, a secret of Ace's; the
//     man at the bins' pages wear the same marks on his paper with his rim (kind_*_his: his square bullet's job). Nothing
//     else on the page: the headings, the names, the entries' lines and the polaroids are words and pictures already.
//   - THE FURNITURE (ArrangeUI's strip, the phone's YOUR CAFÉ card): one picture a kind (Furniture, by the asset name that
//     is the save identity), a plain picture like the drinks', drawn to tell the pieces apart at 40-60 px.
//   - THE TICKETS' ITEMS (the HUD round that comes next builds the tickets): the repair devices (ForDevice) and a clock,
//     plain pictures beside the six drink_* cups (JobTicket.DrinkIconName).
// An Image (not a text) wants a Sprite: SpriteOf(name) cuts one from the same sheet, through TMP's default sprite asset.
// ---------------------------------------------------------------------------
public static class NoteIcons
{
    public const string BackShelf = "note_back_shelf", Table = "note_table", Thrift = "note_thrift", Gnome = "note_gnome",
        Cups = "note_cups_sleeve", Cones = "note_cones", Door = "note_door", Bins = "note_bins", House = "note_house",
        Machine = "note_machine";

    /// <summary>All ten, as the sheet names them (note_house is drawn but no line wears it yet).</summary>
    public static readonly string[] All = { BackShelf, Table, Thrift, Gnome, Cups, Cones, Door, Bins, House, Machine };

    // ---------- 26 Oct 2026 (Icons 2): the notebook page ----------

    /// <summary>The facts' kind chips, by Notebook.Kinds: where someone is, what they own, when they do something, what was said, who they are to someone, a secret.</summary>
    public const string KindWhere = "kind_where", KindOwns = "kind_owns", KindWhen = "kind_when", KindSaid = "kind_said",
        KindWho = "kind_who", KindSecret = "kind_secret";
    /// <summary>A page in his hand (Notebook.Sources.Inherited) wears the same mark on his paper with his rim: the name and this.</summary>
    public const string HisSuffix = "_his";

    /// <summary>The six kind chips (without the his variants), in Notebook.Kinds' order.</summary>
    public static readonly string[] Kinds = { KindWhere, KindOwns, KindWhen, KindSaid, KindWho, KindSecret };

    /// <summary>
    /// A fact's kind chip: its kind's mark, on his paper when the fact is a page of his (<paramref name="his"/>). An unknown or
    /// blank kind (an old save's fact with none) is "what was said": a fact is words somebody gave Ace, at the least.
    /// </summary>
    public static string ForKind(string kind, bool his)
    {
        string mark;
        switch (kind)
        {
            case Notebook.Kinds.Address: mark = KindWhere; break;
            case Notebook.Kinds.Possession: mark = KindOwns; break;
            case Notebook.Kinds.Schedule: mark = KindWhen; break;
            case Notebook.Kinds.Relationship: mark = KindWho; break;
            case Notebook.Kinds.Secret: mark = KindSecret; break;
            default: mark = KindSaid; break;
        }
        return his ? mark + HisSuffix : mark;
    }

    /// <summary>The kind chip for <paramref name="fact"/> (its kind, and whether it is a page of his).</summary>
    public static string ForFact(NotebookFactData fact) =>
        fact == null ? "" : ForKind(fact.kind, fact.source == Notebook.Sources.Inherited);

    /// <summary>
    /// Tonight's chip on the notebook page, by the page's stage: the Now line's own table (NightCycle.NowLine, the README's
    /// rule 4) read from NoteStage instead of the live night, so the page and the line agree (the Night 0 and Night 2 labs
    /// hold the page's chip against NightCycle.NowIcon live): the door for the bag and for calling it a night, the bins once
    /// the bag or the thing is in hand, the thing itself while it is still to take or on the shelf. "" for no night.
    /// </summary>
    public static string ForStage(NoteStage stage, string errand)
    {
        switch (stage)
        {
            case NoteStage.BagToDoor: return Door;
            case NoteStage.BagToBins: return Bins;
            case NoteStage.InHand: return Bins;
            case NoteStage.ToTake: return ForThing(errand);
            case NoteStage.OnShelf: return ForThing(errand);
            case NoteStage.Home: return Door;
            default: return "";
        }
    }

    // ---------- 26 Oct 2026 (Icons 2): the furniture ----------

    public const string FurnBistro = "furn_bistro", FurnSquare = "furn_square", FurnPlant = "furn_plant", FurnBanquette = "furn_banquette",
        FurnCounter = "furn_counter", FurnLounge = "furn_lounge", FurnCommunal = "furn_communal";

    /// <summary>
    /// Each kind the Shop sells, by its asset name (FurnitureDefinition: THE SAVE IDENTITY, never renamed; FurnitureSteps'
    /// table), and its picture. A kind not listed here shows no picture (its card keeps its words), and the Note icons check
    /// says so.
    /// </summary>
    public static readonly (string kind, string icon)[] FurnitureKinds =
    {
        ("Bistro table", FurnBistro), ("Square table", FurnSquare), ("Plant", FurnPlant), ("Banquette bay", FurnBanquette),
        ("Window counter", FurnCounter), ("Lounge corner", FurnLounge), ("Communal table", FurnCommunal),
    };

    /// <summary>The picture for a furniture kind's asset name, or "".</summary>
    public static string Furniture(string kindName)
    {
        if (string.IsNullOrEmpty(kindName)) return "";
        foreach (var (kind, icon) in FurnitureKinds) if (kind == kindName) return icon;
        return "";
    }

    // ---------- 26 Oct 2026 (Icons 2): the tickets' items ----------

    public const string ItemPhone = "item_phone", ItemWatch = "item_watch", ItemCamera = "item_camera", ItemClock = "item_clock";

    /// <summary>The ticket items drawn on the sheet besides the drinks: the three devices and the clock.</summary>
    public static readonly string[] TicketItems = { ItemPhone, ItemWatch, ItemCamera, ItemClock };

    /// <summary>
    /// The picture for a repair device by its name (Job.deviceName: a DeviceDefinition's displayName, "Phone", "Pocket Watch",
    /// "reunion film camera", or the side project's "thrift watch"), or "" for one the sheet doesn't know.
    /// </summary>
    public static string ForDevice(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return "";
        string n = deviceName.ToLowerInvariant();
        if (n.Contains("camera")) return ItemCamera;
        if (n.Contains("watch")) return ItemWatch;
        if (n.Contains("phone")) return ItemPhone;
        return "";
    }

    // ---------- 26 Oct 2026 (Icons 2): an icon as a Sprite, for an Image ----------

    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    static TMP_SpriteAsset spritesFrom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sprites.Clear(); spritesFrom = null; }

    /// <summary>
    /// The icon <paramref name="name"/> cut from the sheet as a Sprite (for a uGUI Image: the strip's cards, the phone), or
    /// null without the sheet or the name. Cached per name; made again if TMP's default sprite asset changes (the step rebuilt it).
    /// </summary>
    public static Sprite SpriteOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        TMP_SpriteAsset sheet = TMP_Settings.instance != null ? TMP_Settings.defaultSpriteAsset : null;
        if (!ReferenceEquals(sheet, spritesFrom)) { sprites.Clear(); spritesFrom = sheet; }
        if (sprites.TryGetValue(name, out Sprite made) && made != null) return made;
        Sprite sprite = null;
        Texture2D texture = sheet != null ? sheet.spriteSheet as Texture2D : null;
        int index = texture != null ? sheet.GetSpriteIndexFromName(name) : -1;
        if (index >= 0 && index < sheet.spriteCharacterTable.Count)
        {
            TMP_SpriteCharacter character = sheet.spriteCharacterTable[index];
            Glyph glyph = character != null ? character.glyph : null;
            if (glyph != null && glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0)
            {
                var rect = new Rect(glyph.glyphRect.x, glyph.glyphRect.y, glyph.glyphRect.width, glyph.glyphRect.height);
                sprite = UnityEngine.Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f);
                sprite.name = "Icon - " + name;
                sprite.hideFlags = HideFlags.DontSave;
            }
        }
        sprites[name] = sprite;
        return sprite;
    }

    /// <summary>His asks, by line id: the one line of each scene that names the errand (the README's table).</summary>
    public static readonly (string id, string icon)[] Asks =
    {
        ("lodger.night0.09", Gnome),   // "Grace's gnome. It watches my alley. Bring it here."
        ("lodger.night2.01", Cups),    // "Grace has a box of cups. Get me a sleeve." (both the plain and the warm ask)
        ("lodger.night3.02", Cones),   // "Nobody parks on West Street. Cones. Both spots."
    };

    /// <summary>The sprite tag for a chip, or "" (no name, or the sheet in TMP's default sprite asset has no such icon).</summary>
    public static string Tag(string icon) => string.IsNullOrEmpty(icon) ? "" : PadInput.Icon(icon, "");

    /// <summary>True when <paramref name="text"/> already shows an icon (a button's, a drink's): such a line gets no chip.</summary>
    public static bool HasIcon(string text) => !string.IsNullOrEmpty(text) && text.IndexOf("<sprite", StringComparison.Ordinal) >= 0;

    /// <summary>The chip and a space, for a line drawn where nothing measures it (a note: one line of 1100 px), or "".</summary>
    public static string Lead(string icon)
    {
        string tag = Tag(icon);
        return tag.Length > 0 ? tag + " " : "";
    }

    /// <summary>
    /// The chip and a space for <paramref name="rich"/> drawn by <paramref name="text"/> in <paramref name="width"/> px, or ""
    /// when the words would wrap once more with the chip beside them. (Which lines may wear one is ForDayLine's, ForLineId's
    /// and the night's call; the night's notebook nudge after " · " is the line's chrome, not its step, so it doesn't stop it.) The test is on the
    /// words alone, narrowed by the chip's width (stricter than the chip on the first line only, so it can never cost a line).
    /// </summary>
    public static string Chip(TMP_Text text, string icon, string rich, float width)
    {
        string tag = Tag(icon);
        if (tag.Length == 0 || text == null || width <= 0f) return "";
        float chip = text.GetPreferredValues(tag + " x").x - text.GetPreferredValues("x").x;   // unconstrained: one line each
        float lines = text.GetPreferredValues(rich, width, 0f).y;
        float narrower = text.GetPreferredValues(rich, Mathf.Max(1f, width - chip), 0f).y;
        return narrower <= lines + .5f ? tag + " " : "";
    }

    /// <summary>The night's thing, as the notebook's "now" subject shows it: the gnome, the reunion cups, the cones.</summary>
    public static string ForThing(string thingId)
    {
        if (thingId == NightThings.GraceGnome) return Gnome;
        if (thingId == NightThings.GraceCups) return Cups;
        if (thingId == LodgerStory.Cones) return Cones;
        return "";
    }

    /// <summary>His line <paramref name="id"/>: its chip if it is an ask, else "".</summary>
    public static string ForLineId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        foreach (var ask in Asks) if (ask.id == id) return ask.icon;
        return "";
    }

    /// <summary>A day note: only his "Clear the tables" (LodgerStory.MessNote) asks for anything.</summary>
    public static string ForNote(string note) => note == LodgerStory.MessNote ? Table : "";

    // The tags a table line can carry (DayOneGuideUI.TableWords), each once.
    static readonly SeatTag[] TableTags = { SeatTag.Window, SeatTag.Lounge, SeatTag.Bar, SeatTag.Communal, SeatTag.Quiet, SeatTag.NearDoor, SeatTag.None };

    /// <summary>
    /// The day's Now line <paramref name="step"/>: the back shelf for every "Fetch more from the back shelf." (the waiting
    /// refill and the stock lines), the machine for the crate in hand, a table for every "Clear the ..." table line, the thrift
    /// find for its line; "" for everything else, and for any line already showing a button icon. Matched on the line's
    /// ending, so "Nina's on the way." before it changes nothing (the README: "gets the table icon only").
    /// </summary>
    public static string ForDayLine(string step)
    {
        if (string.IsNullOrEmpty(step) || HasIcon(step)) return "";
        if (step.EndsWith("Fetch more from the back shelf.", StringComparison.Ordinal)) return BackShelf;
        if (step.EndsWith(DayOneGuideUI.CarryCrateWords, StringComparison.Ordinal)) return Machine;
        if (step.IndexOf("Your thrift ", StringComparison.Ordinal) >= 0
            && (step.EndsWith(": clean it while you wait.", StringComparison.Ordinal) || step.EndsWith(": fix it between customers.", StringComparison.Ordinal)))
            return Thrift;
        foreach (SeatTag tag in TableTags)
            if (step.EndsWith(DayOneGuideUI.TableWords(tag), StringComparison.Ordinal)) return Table;
        return "";
    }
}
