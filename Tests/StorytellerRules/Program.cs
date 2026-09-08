using System;
using System.Text.Json;

// Enum-only stand-ins for files which otherwise require Unity. The in-Editor
// CustomerMemoryChecks uses the real gameplay types and Unity's JsonUtility.
public enum JobGrade { Rejected, Passable, Good, Perfect }
public enum LostReason { StormedOutInQueue, StormedOutWaiting, Declined, OutOfStock, ShelfFull, StillInShopAtClose }

internal static class Program
{
    private static int assertions;
    private static int Main()
    {
        try
        {
            int timing = StorytellerRuleChecks.RunAll();
            CheckMemory();
            Console.WriteLine($"Storyteller PASS: {timing} timing checks; {assertions} memory checks. Unity integration not run.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void CheckMemory()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<SaveData>("{\"version\":3,\"day\":2,\"regularMemories\":[{\"profileId\":\"grace\",\"visits\":1,\"relationship\":2}]}", options);
        old.ValidateAndMigrate();
        Check(!old.regularMemories[0].focusBoundarySet, "Old save defaults to no boundary.");
        var quiet = new CustomerMemoryService();
        var normal = new CustomerMemoryService();
        quiet.Restore(old.regularMemories);
        normal.Restore(old.regularMemories);
        foreach (bool happy in new[] { true, false })
        foreach (bool accepted in new[] { true, false })
        foreach (bool served in new[] { true, false })
        foreach (LostReason reason in Enum.GetValues<LostReason>())
        foreach (string grade in new[] { "", "Rejected", "Passable", "Good", "Perfect" })
        {
            // Two histories differ only by the polite focus choice.
            quiet.RecordVisit("grace", 2, happy, accepted, served, reason, grade, true);
            normal.RecordVisit("grace", 2, happy, accepted, served, reason, grade);
            var a = quiet.Read("grace"); var b = normal.Read("grace");
            Check(a.relationship == b.relationship && a.visits == b.visits && a.lastGrade == b.lastGrade
                && CustomerReturnPolicy.Classify(a) == CustomerReturnPolicy.Classify(b), "Focus changes neither trust nor outcome.");
            Check(a.focusBoundarySet && !b.focusBoundarySet, "Boundary recorded independently.");
        }
        var save = new SaveData { day = 2, dayCompleted = true, recap = new RecapSaveData { day = 2 }, regularMemories = quiet.Snapshot() };
        var restored = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(save, options), options);
        restored.ValidateAndMigrate();
        Check(restored.TryCreateNextDay(out var next) && next.regularMemories[0].focusBoundarySet, "Boundary survives JSON and next-day checkpoint.");
        quiet.Restore(next.regularMemories);
        next.regularMemories[0].focusBoundarySet = false;
        Check(quiet.Read("grace").focusBoundarySet, "Restored history is isolated.");
        var copy = quiet.Read("grace"); copy.focusBoundarySet = false;
        quiet.RecordVisit("grace", 3, false, true, false, LostReason.StormedOutWaiting, "");
        Check(quiet.Read("grace").focusBoundarySet, "Read-copy edits/later visits cannot erase the boundary.");
        quiet.RecordVisit("alex", 3, true, true, true, LostReason.StormedOutWaiting, "Good");
        Check(!quiet.Read("alex").focusBoundarySet, "Other regulars keep their own memory.");
    }

    private static void Check(bool ok, string why)
    {
        assertions++;
        if (!ok) throw new InvalidOperationException(why);
    }
}
