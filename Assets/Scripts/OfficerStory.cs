using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// THE OFFICER: HIS QUESTIONS (claude/night-0-and-the-favours-spec.md §6; claude/session-3-favours-stalling-officer.md §5)
//
// A regular from Day 1 (Regular_Officer: a coffee, black, and a joke about the second batch; Fixit Fidget > Night >
// The officer 1 makes him). From Day 3, once Ace has met the man at the bins, he asks a question after his order:
// "Have you seen a man of this description?" The straight-face meter runs as its second flavour, "Say nothing"
// (MorningFace), with its own words ("Said nothing." or "You flinched."). It's harder than a complaint: being
// described to your face. Nerve helps here too.
//   * Kept: his thanks. Flinched: a line that says he noticed, and he's one step more suspicious (NightLedger); the
//     notebook notes that he's taking an interest in the café. Never stars.
//   * Either way the notebook keeps his description, and that night the man at the bins says what he thought of it
//     (LodgerStory.Verdict).
// Each question is asked once (NightLedger.Questioned). Later questions come a few days apart, each a little closer;
// the demo has the first.
//
// EVERY WORD HERE IS A PLACEHOLDER: Mansoor and his sister write him. Keep the ids (saves hold them).
// No Unity types: the Night rules (Fixit Fidget > Checks, and Tests/NightRules) compile this file.
// ---------------------------------------------------------------------------
public static class OfficerStory
{
    /// <summary>His profile's persistent id (Regular_Officer).</summary>
    public const string ProfileId = "officer";
    public const string DefaultName = "Officer";

    public sealed class Question
    {
        /// <summary>Saved: never change it once saves exist.</summary>
        public string id = "";
        /// <summary>Who asks it: a regular's profile id.</summary>
        public string asker = ProfileId;
        /// <summary>Asked on the asker's first visit on or after this day...</summary>
        public int fromDay = 3;
        /// <summary>...once Ace has met the man at the bins (it's about him).</summary>
        public bool needsHim = true;
        /// <summary>What he asks, after his order. A "\n" starts the next line on screen.</summary>
        public string question = "";
        /// <summary>His answer when Ace keeps a plain face, and when Ace flinches.</summary>
        public string held = "", cracked = "";
        /// <summary>The meter's words: what to do, and how it went.</summary>
        public string title = "Say nothing", heldWord = "Said nothing.", crackedWord = "You flinched.";
        /// <summary>The notebook: what he asked (told), and that he noticed (after a flinch).</summary>
        public string notebookAsked = "", notebookCracked = "";
        /// <summary>The meter (StraightFaceMeter): harder than a complaint.</summary>
        public float sweepSeconds = .85f, green = .17f, near = .02f, patience = 5f;
    }

    // PLACEHOLDER COPY: Mansoor rewrites it.
    static readonly Question[] questions =
    {
        new Question
        {
            id = "officer.description",
            fromDay = 3,
            question = "Before you go. Have you seen a man of this description?\nSuit. Fifties. Looks like he sleeps rough.",
            held = "Didn't think so. Thanks, Ace.",
            cracked = "Hm. You'd tell me, Ace.\nWouldn't you?",
            notebookAsked = "Looking for a man: a suit, fifties, sleeps rough. Asked if I'd seen him.",
            notebookCracked = "I flinched when he asked. He's taking an interest in the café now.",
        },
    };

    public static IReadOnlyList<Question> Questions => questions;

    public static Question Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Question q in questions) if (q.id == id) return q;
        return null;
    }

    /// <summary>The question <paramref name="asker"/> has for Ace today, if any: due, not asked yet, and (for one about him) once Ace has met the man.</summary>
    public static Question Due(string asker, int day, NightLedger ledger)
    {
        if (string.IsNullOrEmpty(asker) || ledger == null) return null;
        foreach (Question q in questions)
            if (q.asker == asker && day >= q.fromDay && (!q.needsHim || ledger.MetHim) && !ledger.HasAsked(q.id)) return q;
        return null;
    }

    // ---------- the notebook ----------

    /// <summary>What he asked: his description of the man (told).</summary>
    public static NotebookFactData Asked(Question q, string name) =>
        Fact(q, q != null ? q.id : null, name, Notebook.Kinds.Claim, q?.notebookAsked, Notebook.Sources.Told);

    /// <summary>Ace flinched: he's taking an interest in the café (seen).</summary>
    public static NotebookFactData Flinched(Question q, string name) =>
        Fact(q, q != null ? q.id + ".flinched" : null, name, Notebook.Kinds.Claim, q?.notebookCracked, Notebook.Sources.Seen);

    static NotebookFactData Fact(Question q, string id, string name, string kind, string text, string source)
    {
        if (q == null || string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(text)) return null;
        return new NotebookFactData
        {
            id = id,
            who = q.asker,
            name = string.IsNullOrWhiteSpace(name) ? DefaultName : name,
            kind = kind,
            text = text,
            source = source,
            sure = Notebook.Sureness.Sure,
        };
    }
}
