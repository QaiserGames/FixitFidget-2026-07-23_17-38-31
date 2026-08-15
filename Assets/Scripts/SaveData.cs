using System;

// The complete contents of a save file. If it's not in here, it isn't saved.
[Serializable]
public class SaveData
{
    // Bump this when the format changes, and handle old numbers in
    // SaveManager.Load. This is what lets updates not destroy saves.
    public int version = 1;

    public int day = 1;
    public int money = 0;

    public int cups = 20;
    public int beans = 20;

    // JsonUtility can't do dictionaries — parallel arrays instead.
    // upgradeNames[i] owns upgradeLevels[i]. Names are asset names,
    // so upgrade assets must NEVER be renamed after shipping.
    public string[] upgradeNames = new string[0];
    public int[] upgradeLevels = new int[0];
}