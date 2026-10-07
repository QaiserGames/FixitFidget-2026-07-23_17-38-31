using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THINGS ACE CAN TAKE AT NIGHT, AND WHAT THEIR OWNERS SAY ABOUT IT (the Night 1 slice)
//
// The loop (claude/ace-after-dark.md): by day someone mentions a thing of theirs; at night Ace can
// take it (NightTrophy) and it goes on Ace's shelf (TrophyShelf); the next morning its owner comes
// in and tells Ace about it, and Ace has to keep a straight face (MorningFace, StraightFaceMeter).
//
// Since the dialogue pass (claude/dialogue-skyrim-proposal.md) the mention is something Ace asks about:
// a line in the conversation's reply list (topic), answered with the mention. If Ace never asks, the
// owner mentions it while they wait (waitingMention). A "\n" in a line starts the next line on screen.
//
// Night 1 has one thing: Grace's garden gnome, by her front step at 12 West Street (her HomeDoor). Night 2 (6 Oct 2026,
// session 3): a sleeve of her reunion cups, on top of their box on her kitchen worktop (the man at the bins' second
// favour: LodgerStory). Ace takes it from inside her house, never through a wall (NightTrophy.needsSight). Nobody
// mentions the cups by day: Ace first hears of them from him (his ask puts them in the notebook).
//
// EVERY WORD HERE IS A PLACEHOLDER. Grace's canon is Mansoor's: the gnome's name, what she says about
// it on Day 1 (when Ace asks about her plans, at the counter over her camera), her complaint the morning
// after, her two reactions and the notebook's shorthand of them are drafts to rewrite. Change the words here; keep the ids
// (saves hold them). The meter's numbers are the thing's difficulty: a stolen gnome is easy. The notes and the notebook's
// lines keep to the word budget (WordBudget: 12 words; the Night rules check fails on more).
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

    /// <summary>What Ace asks, in the reply list, that gets the owner talking about it by day. Under 30 characters.</summary>
    public string topic = "";
    /// <summary>What the owner says about it by day: their answer to the topic.</summary>
    public string mention = "";
    /// <summary>The same, said while they wait if Ace never asked (one short line, a bubble).</summary>
    public string waitingMention = "";
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
    /// <summary>
    /// The notebook's shorthand of the complaint (told), when Ace first hears of it the morning after:
    /// only what the owner says then, since Ace never heard the mention.
    /// </summary>
    public string notebookComplaint = "";
    /// <summary>Ace's own note after taking it (a secret, found at night).</summary>
    public string notebookTaken = "";
    /// <summary>The same when Ace took it for the man at the bins (his errand: LodgerStory), not for the shelf.</summary>
    public string notebookTakenFor = "";
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
    public const string GraceCups = "grace.cups";

    // PLACEHOLDER COPY (see the header): Mansoor rewrites it.
    public static readonly NightThing GnomeOfGrace = new NightThing
    {
        id = GraceGnome,
        owner = GraceCameraEpisode.ProfileId,
        name = "Barnaby",
        unknownName = "the garden gnome",
        topic = "Big plans tonight?",
        mention = "Polishing Barnaby for the reunion. He's my garden gnome.\n"
            + "Twenty years on the front step of the saffron house, on the corner.",
        waitingMention = "Tonight I'm polishing Barnaby. My garden gnome.",
        complaint = "Somebody took Barnaby off my front step last night.\n"
            + "Twenty years he stood there. Who steals a garden gnome, Ace?",
        held = "Thank you for not laughing, dear. The postman laughed.",
        cracked = "Ace. Are you smiling?\n...Hm. I'll be keeping an eye on my front step.",
        takenNote = "Barnaby is coming home with Ace. He'll go on the shelf.",
        takenNoteUnknown = "The garden gnome's coming home with Ace. On the shelf.",
        notebookMention = "Barnaby, her gnome: twenty years on the saffron house's front step.",
        notebookComplaint = "Had a garden gnome, Barnaby, on her front step. Twenty years.",
        notebookTaken = "Took Barnaby from her front step. He's on the shelf now.",
        notebookTakenFor = "Took Barnaby from her front step, for the man at the bins.",
        notebookCracked = "Asked if you were smiling about Barnaby. Watching her front step now.",
        sweepSeconds = 1.1f,
        green = .22f,
        near = .03f,
        patience = 6f,
    };

    // PLACEHOLDER COPY (see the header): Mansoor rewrites it. A little harder to keep a straight face about than the
    // gnome (the break-ins spec, section 10: the deed is worse).
    public static readonly NightThing CupsOfGrace = new NightThing
    {
        id = GraceCups,
        owner = GraceCameraEpisode.ProfileId,
        name = "the reunion cups",
        unknownName = "a sleeve of cups",
        topic = "",
        mention = "",
        waitingMention = "",
        complaint = "Somebody's been at my reunion cups. A whole sleeve, gone.\n"
            + "Who breaks into a house for paper cups, Ace?",
        held = "You're right, it's silly. Still. Twelve cups.",
        cracked = "Ace. Are you laughing at me?\nI'll be counting my cups from now on.",
        takenNote = "A sleeve of Grace's cups is coming home with Ace.",
        takenNoteUnknown = "A sleeve of cups is coming home with Ace.",
        notebookMention = "Reunion cups in her kitchen: a box on the worktop. His word.",
        notebookComplaint = "Someone took a sleeve of her reunion cups.",
        notebookTaken = "Took a sleeve of her reunion cups. It's on the shelf.",
        notebookTakenFor = "Took a sleeve of her cups for the man at the bins.",
        notebookCracked = "Smiled when she told me about her cups. She's counting them now.",
        sweepSeconds = .95f,
        green = .19f,
        near = .025f,
        patience = 5.5f,
    };

    static readonly NightThing[] all = { GnomeOfGrace, CupsOfGrace };

    public static IReadOnlyList<NightThing> All => all;

    /// <summary>The thing with this id, or null.</summary>
    public static NightThing Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (NightThing thing in all)
            if (thing.id == id) return thing;
        return null;
    }

    /// <summary>The first thing <paramref name="owner"/> owns, or null (Grace: her gnome, the thing she talks about by day).</summary>
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

    /// <summary>
    /// The owner told Ace about it only the morning after, in their complaint (Ace took it without having
    /// heard of it). The same fact as the mention (its id is the thing's id), in the complaint's words.
    /// </summary>
    public static NotebookFactData Complained(NightThing thing, string ownerName) =>
        Fact(thing, thing?.id, ownerName, Notebook.Kinds.Possession, thing?.notebookComplaint, Notebook.Sources.Told);

    /// <summary>Ace took it at night: Ace's own secret (<paramref name="forHim"/>: for the man at the bins, not the shelf).</summary>
    public static NotebookFactData Taken(NightThing thing, string ownerName, bool forHim = false) =>
        Fact(thing, thing != null ? thing.id + ".taken" : null, ownerName, Notebook.Kinds.Secret,
            forHim && !string.IsNullOrWhiteSpace(thing?.notebookTakenFor) ? thing.notebookTakenFor : thing?.notebookTaken, Notebook.Sources.Found);

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
