using System;
using System.Text.Json;

// Enum-only stand-in for a gameplay enum that lives in a Unity file. It must
// match JobGrade (JobBase.cs); the in-Editor menu Fixit Fidget > Checks >
// Night 1 rules runs the same checks, plus Unity's own JSON.
public enum JobGrade { Rejected, Passable, Good, Perfect }

internal static class Program
{
    private static int Main()
    {
        try
        {
            int rules = NightRuleChecks.RunAll();
            int json = CheckJson();
            Console.WriteLine($"Night 1 PASS: {rules} rule assertions; {json} save-format assertions. Unity integration not run.");
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

        var ledger = new NightLedger();
        ledger.Take(NightThings.GraceGnome, "grace", 1);
        ledger.Faced(ledger.Unfaced("grace", 2), true, 2);
        var back = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(new SaveData { day = 2, night = ledger.Snapshot() }, options), options);
        back.ValidateAndMigrate();
        var again = new NightLedger();
        again.Restore(back.night);
        Check(again.HasTrophy(NightThings.GraceGnome) && again.Suspicion("grace") == 1 && again.Deeds[0].cracked,
            "The night's record survives a save round trip.");

        var old = JsonSerializer.Deserialize<SaveData>("{\"version\":5,\"day\":4}", options);
        old.ValidateAndMigrate();
        Check(old.night != null && old.night.deeds.Length == 0 && SaveData.CurrentVersion == 5,
            "A save from before the nights loads with an empty record, and the save version is unchanged.");

        // The man at the bins (6 Oct 2026): added fields, no version bump.
        var met = new NightLedger();
        met.Meet();
        met.Warm(-1);
        met.Learn(LodgerStory.Nerve);
        met.Take(NightThings.GraceGnome, "grace", 1);
        met.Give(NightThings.GraceGnome);
        var metBack = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(new SaveData { day = 2, night = met.Snapshot() }, options), options);
        metBack.ValidateAndMigrate();
        var metAgain = new NightLedger();
        metAgain.Restore(metBack.night);
        Check(metAgain.MetHim && metAgain.Warmth == -1 && metAgain.Knows(LodgerStory.Nerve) && metAgain.HasGiven(NightThings.GraceGnome),
            "The man at the bins survives a save round trip: met, warmth, lessons, his corner.");
        Check(!old.night.metHim && old.night.warmth == 0 && old.night.lessons.Length == 0 && old.night.given.Length == 0,
            "A save from before him: not met, no warmth, nothing learned or given.");

        // His favours and the officer's question (session 3): added fields, no version bump.
        NightLedger favours = NightRuleChecks.Favoured();
        var favoursBack = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(new SaveData { day = 4, night = favours.Snapshot() }, options), options);
        favoursBack.ValidateAndMigrate();
        var favoursAgain = new NightLedger();
        favoursAgain.Restore(favoursBack.night);
        Check(NightRuleChecks.SameFavours(favours, favoursAgain), "His favours, the skips, the visit and note days and the officer's question survive a save round trip.");
        Check(old.night.favour == "" && old.night.skips == 0 && old.night.dropped.Length == 0 && old.night.questions.Length == 0,
            "A save from before the favours: none of them.");
        return n;
    }
}
