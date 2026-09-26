using System;
using System.Text.Json;

// Enum-only stand-ins for two gameplay enums that live in Unity files. They
// must match JobGrade (JobBase.cs) and LostReason (DayClock.cs); the in-Editor
// menu Fixit Fidget > Checks > Reputation rules runs the same checks against
// the real ones, plus Unity's own JSON.
public enum JobGrade { Rejected, Passable, Good, Perfect }
public enum LostReason { StormedOutInQueue, StormedOutWaiting, Declined, OutOfStock, ShelfFull, StillInShopAtClose }

internal static class Program
{
    private static int Main()
    {
        try
        {
            int rules = ReputationRuleChecks.RunAll();
            int json = CheckJson();
            Console.WriteLine($"Reputation PASS: {rules} rule assertions; {json} save-format assertions. Unity integration not run.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static int CheckJson()
    {
        int n = 0;
        void Check(bool ok, string why) { n++; if (!ok) throw new InvalidOperationException(why); }
        var options = new JsonSerializerOptions { IncludeFields = true };

        var old = JsonSerializer.Deserialize<SaveData>("{\"version\":4,\"day\":5,\"money\":211,\"dayCompleted\":true,\"recap\":{\"day\":5}}", options);
        old.ValidateAndMigrate();
        Check(old.version == SaveData.CurrentVersion && old.day == 5 && old.money == 211 && old.reputation == 0 && old.starsEarned == 0,
            "A v4 save keeps its progress and starts reputation at zero.");
        Check(old.recap.reviewQuotes != null && old.recap.reviewQuotes.Length == 0, "A v4 recap has no quotes, not a null.");

        var ledger = new ReputationLedger();
        ledger.Restore(118, 2, 11, false, null);
        ledger.Record(new ReviewEntry { review = Review.LovedIt, reason = ReviewReason.LovedRepair, name = "Grace", thing = "camera", regular = true });
        ledger.Settle(11, (e, i) => "“My camera works again.” — " + e.name);
        var save = new SaveData { day = 11, dayCompleted = true, recap = new RecapSaveData { day = 11 }, reputation = ledger.Reputation, starsEarned = ledger.StarsEarned };
        ledger.WriteRecap(save.recap);
        var back = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(save, options), options);
        back.ValidateAndMigrate();
        Check(back.reputation == 120 && back.starsEarned == 3 && back.recap.starsBefore == 2 && back.recap.reviewQuotes[0].EndsWith("Grace"),
            "A new star survives the save: reputation, stars, stars before, the quote.");

        var resumed = new ReputationLedger();
        resumed.Restore(back.reputation, back.starsEarned, back.day, back.dayCompleted, back.recap);
        Check(resumed.EarnedStarToday && ReputationRecap.Build(resumed).Contains("New star!"), "A resumed recap still celebrates the new star.");
        Check(back.TryCreateNextDay(out var next) && next.reputation == 120 && next.starsEarned == 3, "Open Tomorrow keeps it.");
        return n;
    }
}
