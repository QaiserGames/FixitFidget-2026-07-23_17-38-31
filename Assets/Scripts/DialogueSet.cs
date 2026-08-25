using UnityEngine;

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

    public string Pick(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "";
        return pool[Random.Range(0, pool.Length)];
    }
}