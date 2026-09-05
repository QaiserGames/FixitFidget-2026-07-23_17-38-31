#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Pure save data and storage checks. No scene objects, real save paths,
// asset changes, rewards, or customer events are involved.
public static class RecapSaveChecks
{
    [MenuItem("Fixit Fidget/Checks/Recap save checkpoint")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running recap-save checks.");

        CheckMigration();
        CheckValidation();
        CheckMemoryCopy();

        string directory = Path.Combine(Path.GetTempPath(), "FixitFidget-RecapCheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { CheckStorage(Path.Combine(directory, "save.json")); }
        finally { Directory.Delete(directory, true); }

        Debug.Log("[Recap save] PASS: old-save migration, validation, memory copies, " +
                  "recap/purchase round-trips, next-day transition, backups, and failed-write protection. " +
                  "Still run recap-save-checklist.md for scene startup, UI input, purchases, and log preservation.");
    }

    private static void CheckMigration()
    {
        SaveData fresh = new SaveData();
        fresh.ValidateAndMigrate();
        Require(fresh.day == 1 && !fresh.dayCompleted && fresh.recap == null, "New game starts on Day 1, not a recap.");

        foreach (int version in new[] { 1, 2 })
        {
            string json = "{\"version\":" + version + ",\"day\":5,\"money\":725,\"cups\":7,\"beans\":9," +
                          "\"upgradeNames\":[\"TestUpgrade\"],\"upgradeLevels\":[2]}";
            SaveData old = JsonUtility.FromJson<SaveData>(json);
            old.ValidateAndMigrate();
            Require(old.version == SaveData.CurrentVersion && old.day == 5, "Old save migrates without advancing.");
            Require(!old.dayCompleted && old.recap == null, "Old save keeps morning-checkpoint meaning.");
            Require(old.money == 725 && old.cups == 7 && old.beans == 9, "Old balances and stock are preserved.");
            Require(old.upgradeNames[0] == "TestUpgrade" && old.upgradeLevels[0] == 2, "Old upgrades survive.");
            Require(old.regularMemories != null, "Older saves may omit regular memories.");
        }

        SaveData nullable = new SaveData { upgradeNames = null, upgradeLevels = null, regularMemories = null };
        nullable.ValidateAndMigrate();
        Require(nullable.upgradeNames.Length == 0 && nullable.upgradeLevels.Length == 0 && nullable.regularMemories.Length == 0,
            "Missing arrays are safe for bootstrap.");
    }

    private static void CheckValidation()
    {
        Expect<InvalidDataException>(() => new SaveData { version = SaveData.CurrentVersion + 1 }.ValidateAndMigrate(),
            "A newer save must not be treated as this version.");
        Expect<InvalidDataException>(() => new SaveData { dayCompleted = true }.ValidateAndMigrate(),
            "A completed day needs its recap.");
        Expect<InvalidDataException>(() => new SaveData { day = 5, dayCompleted = true, recap = new RecapSaveData { day = 4 } }.ValidateAndMigrate(),
            "The recap must belong to the saved day.");
        Require(!new SaveData().TryCreateNextDay(out _), "An open morning cannot advance through Continue.");
    }

    private static void CheckMemoryCopy()
    {
        RegularMemoryData memory = new RegularMemoryData { profileId = "test-regular", visits = 2, relationship = 3, lastGrade = "Good" };
        RegularMemoryData copy = memory.Copy();
        memory.visits++;
        memory.lastGrade = "Perfect";
        Require(copy.visits == 2 && copy.lastGrade == "Good" && copy.relationship == 3,
            "Later visits cannot mutate a captured memory record.");
    }

    private static void CheckStorage(string path)
    {
        // Synthetic fixture, not production playtest figures.
        SaveData closed = new SaveData
        {
            day = 5, dayCompleted = true, money = 900, cups = 7, beans = 9,
            recap = new RecapSaveData
            {
                day = 5, peopleServed = 6, customersLost = 3, turnedAway = 1,
                ordersCompleted = 8, repairs = 3, drinks = 5, perfect = 2,
                good = 1, passable = 0, tips = 42, earned = 350,
                patronIncome = 25, closingTill = 900, elapsedSeconds = 205.5f
            },
            regularMemories = new[] { new RegularMemoryData { profileId = "test-regular", visits = 2, relationship = 3, lastSeenDay = 5 } }
        };
        SaveCheckpointStorage.Write(path, closed);
        SaveData restored = SaveCheckpointStorage.Read(path);
        Require(restored.dayCompleted && restored.day == 5 && restored.money == 900, "Completed Day 5 reopens as its recap.");
        Require(JsonUtility.ToJson(restored.recap) == JsonUtility.ToJson(closed.recap), "Every recap figure round-trips.");
        Require(restored.regularMemories[0].visits == 2 && restored.regularMemories[0].relationship == 3, "Visits are restored, not replayed.");

        // Simulate the state AFTER a restock transaction, then AFTER an upgrade.
        // Actual UI transactions and prices are covered by the playtest.
        restored.money -= 50;
        restored.cups += 10;
        restored.beans += 10;
        SaveCheckpointStorage.Write(path, restored);
        SaveData stocked = SaveCheckpointStorage.Read(path);
        Require(stocked.money == 850 && stocked.cups == 17 && stocked.beans == 19 && stocked.dayCompleted,
            "Restock deduction and stock persist in the same checkpoint.");
        Require(SaveCheckpointStorage.Read(path + ".bak").money == 900, "Previous checkpoint is retained as backup.");

        stocked.money -= 200;
        stocked.upgradeNames = new[] { "TestUpgrade" };
        stocked.upgradeLevels = new[] { 1 };
        SaveCheckpointStorage.Write(path, stocked);
        SaveData purchased = SaveCheckpointStorage.Read(path);
        Require(purchased.money == 650 && purchased.upgradeNames[0] == "TestUpgrade" && purchased.upgradeLevels[0] == 1,
            "Upgrade level and deduction persist together.");
        Require(purchased.recap.closingTill == 900 && purchased.recap.earned == 350, "Shopping does not rewrite day-close figures.");
        Require(SaveCheckpointStorage.Read(path + ".bak").money == 850, "Repeated writes replace the backup safely.");
        Require(SaveCheckpointStorage.Read(path).money == 650, "Another read does not pay or charge anything again.");

        Require(purchased.TryCreateNextDay(out SaveData next), "Continue creates tomorrow's checkpoint.");
        Require(next.day == 6 && !next.dayCompleted && next.recap == null, "Tomorrow starts open with no old recap.");
        Require(purchased.day == 5 && purchased.dayCompleted, "Preparing tomorrow does not mutate today's snapshot.");
        Require(next.money == 650 && next.cups == 17 && next.beans == 19 && next.upgradeLevels[0] == 1,
            "Continue carries purchases forward without charging twice.");
        Require(next.regularMemories[0].visits == 2 && !next.TryCreateNextDay(out _), "Tomorrow cannot advance again as a completed day.");
        SaveCheckpointStorage.Write(path, next);
        SaveData morning = SaveCheckpointStorage.Read(path);
        Require(morning.day == 6 && !morning.dayCompleted && morning.money == 650, "Reload after Continue starts Day 6, not Day 7.");

        string beforeFailure = File.ReadAllText(path);
        Directory.CreateDirectory(path + ".tmp"); // Prevent a temporary file from opening; no permissions are changed.
        bool failed = false;
        try { SaveCheckpointStorage.Write(path, closed); }
        catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Require(failed, "A blocked temporary write reports failure.");
        Require(File.ReadAllText(path) == beforeFailure, "Failed writing leaves the primary checkpoint byte-for-byte intact.");
    }

    private static void Expect<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("[Recap save] FAIL: " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Recap save] FAIL: " + message);
    }
}
#endif
