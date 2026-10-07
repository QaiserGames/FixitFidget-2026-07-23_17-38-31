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
// The word (playtest 3, 6 Oct 2026; claude/playtest-3-notes-and-plan.md §5.2): the morning after Ace's third skip in a
// row the man at the bins has had a word with him, and he comes in that day (CustomerSpawner brings him) with a harder
// question: "Someone says you keep odd company." One such question a favour (its id carries the favour), only on that day.
//
// EVERY WORD HERE IS A PLACEHOLDER: Mansoor and his sister write him. Keep the ids (saves hold them). Notebook lines keep
// to the word budget (WordBudget.Page).
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
        /// <summary>Asked only on the day the man at the bins has had a word with him (NightLedger.WordDue), once a favour.</summary>
        public bool afterWord;
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

        /// <summary>The same question under another id (the word's, one a favour).</summary>
        public Question WithId(string newId)
        {
            var copy = (Question)MemberwiseClone();
            copy.id = newId ?? "";
            return copy;
        }
    }

    /// <summary>The word question's id for <paramref name="favour"/> ("officer.word.grace.cups").</summary>
    public const string WordPrefix = "officer.word.";
    public static string WordQuestionId(string favour) => string.IsNullOrEmpty(favour) ? "" : WordPrefix + favour;
    public static bool IsWord(string questionId) => !string.IsNullOrEmpty(questionId) && questionId.StartsWith(WordPrefix, StringComparison.Ordinal);

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
            notebookAsked = "Looking for a man: suit, fifties, sleeps rough. Asked me.",
            notebookCracked = "I flinched. He's taking an interest in the café now.",
        },
        // The word: harder still (the green is narrower), and only on the day it's due.
        new Question
        {
            id = WordPrefix + "favour", afterWord = true, fromDay = 1,
            question = "Someone says you keep odd company, Ace.\nAnything to that?",
            held = "Didn't think so. Forget I asked.",
            cracked = "Hm. People talk, Ace.\nI listen.",
            notebookAsked = "Asked if I keep odd company. Someone's been talking.",
            notebookCracked = "I flinched. He listens to whoever talks about the café.",
            sweepSeconds = .85f, green = .15f, near = .02f, patience = 5f,
        },
    };

    public static IReadOnlyList<Question> Questions => questions;

    public static Question Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Question q in questions) if (q.id == id) return q;
        // A word's id carries its favour: the same question, under that id.
        if (IsWord(id)) return Word(id.Substring(WordPrefix.Length));
        return null;
    }

    /// <summary>The word question, as asked about <paramref name="favour"/> (its id carries the favour), or null.</summary>
    public static Question Word(string favour)
    {
        if (string.IsNullOrEmpty(favour)) return null;
        foreach (Question q in questions) if (q.afterWord) return q.WithId(WordQuestionId(favour));
        return null;
    }

    /// <summary>
    /// The question <paramref name="asker"/> has for Ace today, if any: due, not asked yet, and (for one about him) once Ace
    /// has met the man. On a word's day (NightLedger.WordDue) the word comes first, about that favour, once a favour.
    /// </summary>
    public static Question Due(string asker, int day, NightLedger ledger)
    {
        if (string.IsNullOrEmpty(asker) || ledger == null) return null;
        string word = ledger.WordQuestion(day);
        if (word.Length > 0 && !ledger.HasAsked(word))
            foreach (Question q in questions)
                if (q.afterWord && q.asker == asker && (!q.needsHim || ledger.MetHim)) return q.WithId(word);
        foreach (Question q in questions)
            if (!q.afterWord && q.asker == asker && day >= q.fromDay && (!q.needsHim || ledger.MetHim) && !ledger.HasAsked(q.id)) return q;
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
