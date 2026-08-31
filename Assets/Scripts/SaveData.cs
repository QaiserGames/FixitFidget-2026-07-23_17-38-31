using System;

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
}

// The complete contents of a save file. If it's not in here, it isn't saved.
[Serializable]
public class SaveData
{
    // Bump this when the format changes, and handle old numbers in
    // SaveManager.Load. This is what lets updates not destroy saves.
    public int version = 2;

    public int day = 1;
    public int money = 0;

    public int cups = 20;
    public int beans = 20;

    // JsonUtility can't do dictionaries — parallel arrays instead.
    // upgradeNames[i] owns upgradeLevels[i]. Names are asset names,
    // so upgrade assets must NEVER be renamed after shipping.
    public string[] upgradeNames = new string[0];
    public int[] upgradeLevels = new int[0];

    // Runtime lookup is a dictionary in SaveManager; the file uses an array
    // because JsonUtility cannot serialize dictionaries.
    public RegularMemoryData[] regularMemories = new RegularMemoryData[0];
}
