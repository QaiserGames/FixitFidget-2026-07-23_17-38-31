using System;
using System.Text.Json;
public enum JobGrade { Rejected, Passable, Good, Perfect }
public enum LostReason { StormedOutInQueue, StormedOutWaiting, Declined, OutOfStock, ShelfFull, StillInShopAtClose }
internal static class Program
{
    private static int Main()
    {
        try
        {
            int checks = ContinuationRuleChecks.RunAll();
            var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
            var old = JsonSerializer.Deserialize<SaveData>("{\"version\":3,\"day\":5,\"money\":211,\"regularMemories\":[{\"profileId\":\"grace\",\"lastGrade\":\"Perfect\"}]}", jsonOptions);
            old.ValidateAndMigrate();
            if (old.version != 4 || old.day != 5 || old.money != 211 || old.regularMemories[0].graceCameraAttempted)
                throw new Exception("Old save migration altered progress or invented a camera episode.");
            Console.WriteLine($"Continuation PASS: {checks} episode/freshness assertions and old-save JSON migration.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
