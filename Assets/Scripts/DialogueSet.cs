using UnityEngine;

// One person's lines for each moment of a visit.
//
// A line can be several lines on screen (the dialogue pass, claude/dialogue-skyrim-proposal.md): put
// each on its own line (Enter in the box). The conversation shows them one after another, the earlier
// ones dimmed, like Skyrim's subtitles. Keep each one short: aim for 90 characters, never over 150
// (Fixit Fidget > Checks > Dialogue rules). Lines said out loud in the room (bubbles) are one line each.
[System.Serializable]
public class DialogueSet
{
    [TextArea(2, 3)] public string[] intake;        // what's wrong, walking in
    [TextArea(2, 3)] public string[] accepted;      // you took the job
    [TextArea(2, 3)] public string[] completed;     // handed back
    [TextArea(2, 3)] public string[] declined;      // you turned them away
    [TextArea(2, 3)] public string[] reassured;     // you calmed them
    [TextArea(2, 3)] public string[] stormedOut;    // patience ran out

    // Said a few seconds after they've settled, when someone waiting on a
    // repair decides they'd like a coffee too. Leave empty and CustomerBrain
    // falls back to a placeholder line — see orderFallback there, and delete
    // that field's use once these are written.
    [TextArea(2, 3)] public string[] orderedDrink;

    // How someone who came in ONLY for a drink orders it at the counter ("Hi! Could I get {a drink}?";
    // {a drink} and {drink} are filled in: "a latte", "latte"). Empty: a shared placeholder pool
    // (CustomerIdentity). A regular orders with their orderedDrink lines instead.
    [TextArea(2, 3)] public string[] drinkOrder;

    public string Pick(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "";
        return pool[Random.Range(0, pool.Length)];
    }
}