using System;
using System.Linq;
using System.Text.Json;

// Enum-only stand-in for a gameplay enum that lives in a Unity file. It must
// match JobGrade (JobBase.cs); the in-Editor menu Fixit Fidget > Checks >
// Home rules runs the same checks, plus Unity's own JSON.
public enum JobGrade { Rejected, Passable, Good, Perfect }

internal static class Program
{
    private static int Main()
    {
        try
        {
            int homes = HomeRuleChecks.RunAll();
            int notebook = NotebookRuleChecks.RunAll();
            int json = CheckJson();
            Console.WriteLine($"Homes PASS: {homes} home rule assertions; {notebook} notebook rule assertions still pass; " +
                              $"{json} save-format assertions. Unity integration not run.");
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

        var book = new Notebook();
        book.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", true, "hunch"), 2);
        book.Learn(NotebookEntries.HomeSeen("grace", "Grace", "the saffron house", "12", "west", false, "likely"), 3);
        var back = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(new SaveData { day = 3, notebook = book.Snapshot() }, options), options);
        back.ValidateAndMigrate();
        var again = new Notebook();
        again.Restore(back.notebook);
        NotebookFactData home = again.Find("grace.home");
        Check(home != null && home.sure == "likely" && home.surerDay == 3 && home.day == 2, "A sighting survives a save round trip.");

        var old = JsonSerializer.Deserialize<SaveData>("{\"version\":5,\"day\":4,\"notebook\":[{\"id\":\"grace.camera.strap\",\"who\":\"grace\",\"sure\":\"sure\",\"day\":1}]}", options);
        old.ValidateAndMigrate();
        Check(old.notebook.Length == 1 && old.notebook[0].surerDay == 0 && SaveData.CurrentVersion == 5,
            "An older notebook loads with no 'surer' day, and the save version is unchanged.");
        return n;
    }
}
