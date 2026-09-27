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

    // Additive v3 field: absent in existing saves means no boundary was set.
    // Keyed by this record's stable profileId (Grace: grace_focus_boundary in the GDD).
    public bool focusBoundarySet = false;

    // Additive v4 episode facts. Generic lastGrade is deliberately insufficient:
    // repairing a watch or serving coffee cannot create a reunion photograph.
    public bool graceCameraAttempted;
    public bool graceCameraReturned;
    public string graceCameraGrade = "";
    public int graceCameraDay;
    public bool graceReturnAcknowledged;
    public bool gracePhotoClaimed;
    public string gracePhotoVariant = "";

    public RegularMemoryData Copy() => (RegularMemoryData)MemberwiseClone();
}

// One thing Ace has learned about a regular (claude/night-notebook-spec.md).
// Kind, source and certainty are stored as text, so their lists can grow
// without breaking saves. Owned at runtime by Notebook.
[Serializable]
public class NotebookFactData
{
    public string id = "";            // stable key: the same fact heard twice is one entry
    public string who = "";           // the regular's stable profile id, never display text
    public string name = "";          // what Ace calls them, for the recap
    public string kind = "";          // address, possession, schedule, claim, relationship, secret
    public string text = "";          // what the notebook says
    public string source = "";        // told, overheard, seen, read, found
    public string sure = "";          // hunch, likely, sure
    public int day;                   // the day it was learned
    public int confirmedDay;          // the last later day it was heard or seen again (0 = never)
    public int surerDay;              // the last day Ace became surer of it (0 = never); additive, night step 3
    public string conflictsWith = ""; // the id of a fact it contradicts (a question), or ""

    public NotebookFactData Copy() => (NotebookFactData)MemberwiseClone();
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

    // Additive v5: the day's reviews (claude/reputation-spec.md). Absent in
    // older saves, which simply show no reviews for that day.
    public int reviewsLoved;
    public int reviewsLiked;
    public int reviewsFine;
    public int reviewsLetDown;
    public int reviewsNeverAgain;
    public int reputationChange;
    public int starsBefore;
    // The quoted reviews exactly as shown, so a resumed recap reads the same.
    public string[] reviewQuotes = new string[0];
    public int[] reviewQuoteVerdicts = new int[0];
}

// The complete contents of a save file. If it's not in here, it isn't saved.
[Serializable]
public class SaveData
{
    // Bump this when the format changes, and handle old numbers in
    // ValidateAndMigrate. This is what lets updates not destroy saves.
    public const int CurrentVersion = 5;
    public int version = CurrentVersion;

    public int day = 1;
    public int money = 0;

    public int cups = 20;
    public int beans = 20;

    // Additive v5: the café's reputation (claude/reputation-spec.md). Older
    // saves start at zero. starsEarned is kept separately because earned stars
    // stay earned even if reputation later dips below their threshold.
    public int reputation = 0;
    public int starsEarned = 0;

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

    // Additive (the night notebook, 27 Sept 2026). No version bump on purpose:
    // older builds skip a field they do not know, so this save still loads on
    // a branch without the notebook (which drops it when it saves). Absent
    // means an empty notebook; SaveManager backfills what the memories imply.
    public NotebookFactData[] notebook = new NotebookFactData[0];

    public void ValidateAndMigrate()
    {
        if (version > CurrentVersion)
            throw new InvalidDataException("This save is from a newer game version.");

        day = Math.Max(1, day);
        upgradeNames ??= new string[0];
        upgradeLevels ??= new int[0];
        regularMemories ??= new RegularMemoryData[0];
        notebook = notebook == null ? new NotebookFactData[0]
            : Array.FindAll(notebook, fact => fact != null && !string.IsNullOrEmpty(fact.id));
        reputation = Math.Max(0, reputation);
        starsEarned = Math.Max(0, Math.Min(5, starsEarned));
        if (recap != null)
        {
            recap.reviewQuotes ??= new string[0];
            recap.reviewQuoteVerdicts ??= new int[0];
        }

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
