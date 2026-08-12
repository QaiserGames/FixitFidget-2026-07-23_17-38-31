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

    public string Pick(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "";
        return pool[Random.Range(0, pool.Length)];
    }
}