using System;
using System.IO;

// What the shop remembers about one named regular between days and sessions.
// profileId is the stable CustomerProfile.PersistentId, never display text.
[Serializable]
public class RegularMemoryData
{
    public string profileId = "";
    public int visits = 0;
    public int relationship = 0;
    public int lastSeenDay = 0;

    public bool lastVisitHappy = false;
    public bool lastJobAccepted = false;
    public bool lastVisitServed = false;

    // Stored as text so appending/reordering gameplay enums cannot corrupt old saves.
    public string lastLossReason = "";
    public string lastGrade = "";

    public RegularMemoryData Copy() => (RegularMemoryData)MemberwiseClone();
}

// A completed day's figures are a snapshot, not a replay of payouts/events.
[Serializable]
public class RecapSaveData
{
    public int day;
    public int peopleServed;
    public int customersLost;
    public int turnedAway;
    public int ordersCompleted;
    public int repairs;
    public int drinks;
    public int perfect;
    public int good;
    public int passable;
    public int tips;
    public int earned;
    public int patronIncome;
    public int closingTill;
    public float elapsedSeconds;
}

// The complete contents of a save file. If it's not in here, it isn't saved.
[Serializable]
public class SaveData
{
    // Bump this when the format changes, and handle old numbers in
    // ValidateAndMigrate. This is what lets updates not destroy saves.
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;

    public int day = 1;
    public int money = 0;

    public int cups = 20;
    public int beans = 20;

    // False = a start-of-day checkpoint (also the meaning of v1/v2 saves).
    // True = resume the closed-day recap, without running that day again.
    public bool dayCompleted;
    public RecapSaveData recap;

    // JsonUtility can't do dictionaries — parallel arrays instead.
    // upgradeNames[i] owns upgradeLevels[i]. Names are asset names,
    // so upgrade assets must NEVER be renamed after shipping.
    public string[] upgradeNames = new string[0];
    public int[] upgradeLevels = new int[0];

    // Runtime lookup is a dictionary in SaveManager; the file uses an array
    // because JsonUtility cannot serialize dictionaries.
    public RegularMemoryData[] regularMemories = new RegularMemoryData[0];

    public void ValidateAndMigrate()
    {
        if (version > CurrentVersion)
            throw new InvalidDataException("This save is from a newer game version.");

        day = Math.Max(1, day);
        upgradeNames ??= new string[0];
        upgradeLevels ??= new int[0];
        regularMemories ??= new RegularMemoryData[0];

        if (version < 3)
        {
            // Old saves never recorded a completed recap. Do not invent one.
            dayCompleted = false;
            recap = null;
        }

        if (dayCompleted && (recap == null || recap.day != day))
            throw new InvalidDataException("The completed-day recap is missing or belongs to another day.");

        if (!dayCompleted) recap = null;
        version = CurrentVersion;
    }

    public bool TryCreateNextDay(out SaveData next)
    {
        next = null;
        if (!dayCompleted || recap == null || recap.day != day) return false;

        // All financial, inventory, upgrade, and memory values carry forward.
        // Neither checkpoint is used to award earnings a second time.
        next = (SaveData)MemberwiseClone();
        next.day = checked(day + 1);
        next.dayCompleted = false;
        next.recap = null;
        return true;
    }
}
