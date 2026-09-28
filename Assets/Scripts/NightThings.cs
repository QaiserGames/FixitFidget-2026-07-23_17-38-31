using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THINGS ACE CAN TAKE AT NIGHT, AND WHAT THEIR OWNERS SAY ABOUT IT (the Night 1 slice)
//
// The loop (claude/ace-after-dark.md): by day someone mentions a thing of theirs; at night Ace can
// take it (NightTrophy) and it goes on Ace's shelf (TrophyShelf); the next morning its owner comes
// in and tells Ace about it, and Ace has to keep a straight face (MorningFace, StraightFaceMeter).
//
// Night 1 has one thing: Grace's garden gnome, by her front step at 12 West Street (her HomeDoor).
//
// EVERY WORD HERE IS A PLACEHOLDER. Grace's canon is Mansoor's: the gnome's name, what she says about
// it on Day 1 (after Ace takes her camera job), her complaint the morning after, her two reactions
// and the notebook's shorthand of them are drafts to rewrite. Change the words here; keep the ids
// (saves hold them). The meter's numbers are the thing's difficulty: a stolen gnome is easy.
//
// No Unity types: the Night 1 rules (Fixit Fidget > Checks, and Tests/NightRules) compile this file.
// ---------------------------------------------------------------------------
public sealed class NightThing
{
    /// <summary>Saved in trophies and deeds: never change it once saves exist.</summary>
    public string id = "";
    /// <summary>Whose it is: a regular's profile id.</summary>
    public string owner = "";
    /// <summary>What Ace calls it once the owner has mentioned it (the prompt: "Take Barnaby").</summary>
    public string name = "";
    /// <summary>What Ace calls it before hearing about it ("the garden gnome").</summary>
    public string unknownName = "";

    /// <summary>What the owner says about it by day (added after a line of theirs).</summary>
    public string mention = "";
    /// <summary>The morning after: the owner tells Ace about it, and then the meter runs.</summary>
    public string complaint = "";
    /// <summary>The owner's reaction when Ace keeps a straight face.</summary>
    public string held = "";
    /// <summary>The owner's reaction when Ace cracks.</summary>
    public string cracked = "";
    /// <summary>A one-line note after Ace takes it (the HUD, at night).</summary>
    public string takenNote = "";
    /// <summary>The same, when Ace hasn't heard of it (whose it is waits for the morning).</summary>
    public string takenNoteUnknown = "";

    /// <summary>The notebook's shorthand of the mention (told).</summary>
    public string notebookMention = "";
    /// <summary>Ace's own note after taking it (a secret, found at night).</summary>
    public string notebookTaken = "";
    /// <summary>After a crack: what the owner said (told).</summary>
    public string notebookCracked = "";

    // The meter (StraightFaceMeter).
    public float sweepSeconds = 1.1f;
    public float green = .22f;
    public float near = .03f;
    public float patience = 6f;
}

public static class NightThings
{
    public const string GraceGnome = "grace.gnome";

    // PLACEHOLDER COPY (see the header): Mansoor rewrites it.
    public static readonly NightThing GnomeOfGrace = new NightThing
    {
        id = GraceGnome,
        owner = GraceCameraEpisode.ProfileId,
        name = "Barnaby",
        unknownName = "the garden gnome",
        mention = "I'll be home tonight polishing Barnaby for the reunion. He's my garden gnome: "
            + "twenty years on the front step of the saffron house on the corner.",
        complaint = "Before anything else: somebody took Barnaby off my front step last night. "
            + "Twenty years he stood there. Who steals a garden gnome, Ace? What sort of person does that?",
        held = "Thank you for not laughing, dear. The postman laughed.",
        cracked = "Ace. Are you smiling? ...Hm. I'll be keeping an eye on my front step.",
        takenNote = "Barnaby is coming home with Ace. He'll go on the shelf.",
        takenNoteUnknown = "The garden gnome is coming home with Ace. It'll go on the shelf.",
        notebookMention = "Has a garden gnome, Barnaby, on the front step of the saffron house on the corner. Twenty years. Polishes him.",
        notebookTaken = "Took Barnaby from her front step. He's on the shelf now.",
        notebookCracked = "Asked if you were smiling about Barnaby. Watching her front step now.",
        sweepSeconds = 1.1f,
        green = .22f,
        near = .03f,
        patience = 6f,
    };

    static readonly NightThing[] all = { GnomeOfGrace };

    public static IReadOnlyList<NightThing> All => all;

    /// <summary>The thing with this id, or null.</summary>
    public static NightThing Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (NightThing thing in all)
            if (thing.id == id) return thing;
        return null;
    }

    /// <summary>The first thing <paramref name="owner"/> owns, or null.</summary>
    public static NightThing OwnedBy(string owner)
    {
        if (string.IsNullOrEmpty(owner)) return null;
        foreach (NightThing thing in all)
            if (thing.owner == owner) return thing;
        return null;
    }

    // ---------- the notebook (claude/night-notebook-spec.md): the same shape as NotebookEntries ----------

    /// <summary>The owner told Ace about it. The fact's id is the thing's id.</summary>
    public static NotebookFactData Mentioned(NightThing thing, string ownerName) =>
        Fact(thing, thing?.id, ownerName, Notebook.Kinds.Possession, thing?.notebookMention, Notebook.Sources.Told);

    /// <summary>Ace took it at night: Ace's own secret.</summary>
    public static NotebookFactData Taken(NightThing thing, string ownerName) =>
        Fact(thing, thing != null ? thing.id + ".taken" : null, ownerName, Notebook.Kinds.Secret, thing?.notebookTaken, Notebook.Sources.Found);

    /// <summary>Ace cracked when the owner told the story: they said they'd be watching.</summary>
    public static NotebookFactData Suspects(NightThing thing, string ownerName) =>
        Fact(thing, thing != null ? thing.id + ".suspects" : null, ownerName, Notebook.Kinds.Claim, thing?.notebookCracked, Notebook.Sources.Told);

    static NotebookFactData Fact(NightThing thing, string id, string ownerName, string kind, string text, string source)
    {
        if (thing == null || string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(text)) return null;
        return new NotebookFactData
        {
            id = id,
            who = thing.owner,
            name = string.IsNullOrWhiteSpace(ownerName) ? thing.owner : ownerName,
            kind = kind,
            text = text,
            source = source,
            sure = Notebook.Sureness.Sure
        };
    }
}
