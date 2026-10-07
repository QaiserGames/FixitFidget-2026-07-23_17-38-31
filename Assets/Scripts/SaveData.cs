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

// What Ace's nights have done (claude/ace-after-dark.md §3.2, the Night 1 slice).
// Owned at runtime by NightLedger. Additive: absent in older saves means no nights yet.
[Serializable]
public class NightDeedData
{
    public string thing = "";   // what was taken: a stable id from NightThings ("grace.gnome")
    public string owner = "";   // whose it was: a regular's profile id
    public int night;           // the night it happened: the night after this day
    public bool faced;          // the owner has told Ace about it (the straight-face scene ran)
    public bool cracked;        // ...and Ace's face gave something away
    public int facedDay;

    public NightDeedData Copy() => (NightDeedData)MemberwiseClone();
}

// Someone who suspects Ace (a cracked straight face). Never touches stars.
[Serializable]
public class SuspicionData
{
    public string who = "";
    public int level;

    public SuspicionData Copy() => (SuspicionData)MemberwiseClone();
}

[Serializable]
public class NightSaveData
{
    public int nights;                                        // nights Ace has come home from
    public string[] trophies = new string[0];                  // on Ace's shelf, in the order taken
    public NightDeedData[] deeds = new NightDeedData[0];
    public SuspicionData[] suspicion = new SuspicionData[0];

    // The man at the bins (claude/the-man-at-the-bins-story.md; additive, 6 Oct 2026, no version bump).
    public const int MaxWarmth = 5;                            // warmth stays within plus or minus this
    public bool metHim;                                        // Night 0 is behind Ace: the deal at the bins was made
    public int warmth;                                         // how warm he is to Ace (hidden; Ace's replies move it)
    public string[] lessons = new string[0];                   // what he has taught Ace ("nerve"), in order
    public string[] given = new string[0];                     // what Ace has brought him (his corner): thing ids

    // His favours and the officer's questions (claude/session-3-favours-stalling-officer.md; additive, 6 Oct 2026, no
    // version bump: an older save loads with none, and NightLedger.Restore picks his favour up where it was).
    public string favour = "";                                 // the favour he's asking for now (LodgerStory.Favours), "" none
    public int askedOn;                                        // the night he first asked for it (0: not yet)
    public int lastAsked;                                      // the last night he asked for it
    public int skips;                                          // nights in a row he asked and the night ended without it
    public int visitDay;                                       // the day he sits at a table all morning (the morning after a skip), 0 none
    public int messDay;                                        // the day the café opens with his mess (the second skip in a row and on), 0 none
    public int wordDay;                                        // the day of his planted review and the officer's word (the third skip), 0 none
    public int noteDay;                                        // the day his note is on the counter (the same morning), 0 none
    public string noteFavour = "";                             // ...and the favour it's about
    public string[] dropped = new string[0];                   // favours he stopped asking for (before 6 Oct 2026; nothing adds to it now)
    public QuestionData[] questions = new QuestionData[0];     // what the officer has asked Ace, oldest first
}

// A question someone asked Ace by day (the officer's "Have you seen a man of this description?": OfficerStory).
[Serializable]
public class QuestionData
{
    public string id = "";      // which question (OfficerStory): asked once
    public string who = "";     // who asked: a regular's profile id
    public int day;             // the day it was asked
    public bool cracked;        // Ace flinched (a "say nothing" meter cracked)

    public QuestionData Copy() => (QuestionData)MemberwiseClone();
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

    // Additive (playtest 2, step 2): every review of the day as the recap phone shows it, in the
    // order they were written: who signed it, what they wrote, the verdict (1-5), the kind of visit
    // (ReviewReason) and whether they're a regular. Absent in older saves, whose recap shows its
    // quotes as cards instead. No version bump: older builds skip fields they don't know.
    public string[] reviewCardNames = new string[0];
    public string[] reviewCardLines = new string[0];
    public int[] reviewCardVerdicts = new int[0];
    public int[] reviewCardReasons = new int[0];
    public bool[] reviewCardRegulars = new bool[0];
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

    // Additive (the Night 1 slice, 28 Sept 2026), the same way as the notebook: no
    // version bump. Written by the morning's checkpoint; nothing is saved during a night.
    public NightSaveData night = new NightSaveData();

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
        night ??= new NightSaveData();
        night.nights = Math.Max(0, night.nights);
        night.trophies = night.trophies == null ? new string[0] : Array.FindAll(night.trophies, thing => !string.IsNullOrEmpty(thing));
        night.deeds = night.deeds == null ? new NightDeedData[0]
            : Array.FindAll(night.deeds, deed => deed != null && !string.IsNullOrEmpty(deed.thing));
        night.suspicion = night.suspicion == null ? new SuspicionData[0]
            : Array.FindAll(night.suspicion, who => who != null && !string.IsNullOrEmpty(who.who));
        night.lessons = night.lessons == null ? new string[0] : Array.FindAll(night.lessons, lesson => !string.IsNullOrEmpty(lesson));
        night.given = night.given == null ? new string[0] : Array.FindAll(night.given, thing => !string.IsNullOrEmpty(thing));
        night.warmth = Math.Max(-NightSaveData.MaxWarmth, Math.Min(NightSaveData.MaxWarmth, night.warmth));
        night.favour ??= "";
        night.noteFavour ??= "";
        night.dropped = night.dropped == null ? new string[0] : Array.FindAll(night.dropped, favour => !string.IsNullOrEmpty(favour));
        night.questions = night.questions == null ? new QuestionData[0]
            : Array.FindAll(night.questions, q => q != null && !string.IsNullOrEmpty(q.id));
        reputation = Math.Max(0, reputation);
        starsEarned = Math.Max(0, Math.Min(5, starsEarned));
        if (recap != null)
        {
            recap.reviewQuotes ??= new string[0];
            recap.reviewQuoteVerdicts ??= new int[0];
            recap.reviewCardNames ??= new string[0];
            recap.reviewCardLines ??= new string[0];
            recap.reviewCardVerdicts ??= new int[0];
            recap.reviewCardReasons ??= new int[0];
            recap.reviewCardRegulars ??= new bool[0];
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
