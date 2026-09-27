using System;
using System.Linq;
using System.Text.Json;

// Enum-only stand-in for a gameplay enum that lives in a Unity file. It must
// match JobGrade (JobBase.cs); the in-Editor menu Fixit Fidget > Checks >
// Night notebook rules runs the same checks against the real one, plus
// Unity's own JSON.
public enum JobGrade { Rejected, Passable, Good, Perfect }

internal static class Program
{
    private static int Main()
    {
        try
        {
            int rules = NotebookRuleChecks.RunAll();
            int json = CheckJson();
            Console.WriteLine($"Notebook PASS: {rules} rule assertions; {json} save-format assertions. Unity integration not run.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    // System.Text.Json with fields, as a stand-in for Unity's JsonUtility.
    private static int CheckJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        var options = new JsonSerializerOptions { IncludeFields = true };

        var old = JsonSerializer.Deserialize<SaveData>("{\"version\":5,\"day\":2,\"money\":139}", options);
        old.ValidateAndMigrate();
        Check(old.version == SaveData.CurrentVersion && old.day == 2 && old.money == 139 && old.notebook.Length == 0,
            "A save from before the notebook keeps its progress and gets an empty notebook.");

        var book = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) book.Learn(fact, 1);
        var save = new SaveData { day = 2, notebook = book.Snapshot() };
        var back = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(save, options), options);
        back.ValidateAndMigrate();
        var again = new Notebook();
        again.Restore(back.notebook);
        Check(again.Count == 4 && again.Facts.Select(f => f.id).SequenceEqual(book.Facts.Select(f => f.id))
              && again.Facts.All(f => f.day == 1 && f.sure == "sure"), "The notebook survives a save round trip.");
        return n;
    }
}
