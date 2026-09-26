using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Tooltip("Use a separate save for the focused playtest scene. Campaign progress is kept in save.json.")]
    [SerializeField] private bool useInteractionPlaytestSave;

    [Tooltip("Separate playtest checkpoint filename. Only used when playtest saving is enabled.")]
    [SerializeField] private string interactionPlaytestSaveName = "interaction-playtest.json";

    [Tooltip("What customers write in their reviews (Assets/Data/Reputation/ReviewLines). " +
             "Optional: without it, the draft lines built into ReviewLines are used.")]
    [SerializeField] private ReviewLines reviewLines;

    // Filled during Awake so other systems can read it in their Start.
    public SaveData Loaded { get; private set; }
    public bool HasSave { get; private set; }
    public string LastSaveError { get; private set; } = "";
    public event Action SaveStatusChanged;

    private bool writesBlocked;

    private readonly CustomerMemoryService regularMemory = new();

    // The café's reputation (claude/reputation-spec.md). Reviews are collected
    // as people leave and counted into stars once, at closing. Saved with the
    // rest of the checkpoint, like regulars' memory.
    private readonly ReputationLedger reputation = new();
    // Everyone already reviewed today, so nobody counts twice.
    private readonly HashSet<CustomerBrain> reviewedToday = new();
    // The draft lines from ReviewLines.cs, for scenes without a lines asset.
    // One shared copy per editor/game session.
    private static ReviewLines fallbackLines;

    private string PathToFile => Path.Combine(Application.persistentDataPath,
        useInteractionPlaytestSave ? PlaytestFileName : "save.json");

    private string PlaytestFileName => string.IsNullOrWhiteSpace(interactionPlaytestSaveName)
        || !interactionPlaytestSaveName.StartsWith("playtest-", StringComparison.Ordinal)
        || Path.GetFileName(interactionPlaytestSaveName) != interactionPlaytestSaveName
        || !interactionPlaytestSaveName.EndsWith(".json", StringComparison.Ordinal)
        ? "interaction-playtest.json" : interactionPlaytestSaveName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        Instance = this;
        LoadFromDisk();
        RebuildRegularMemory();
        RebuildReputation();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadFromDisk()
    {
        HasSave = File.Exists(PathToFile);

        if (!HasSave)
        {
            Loaded = new SaveData();     // fresh defaults — a new game
            return;
        }

        try
        {
            Loaded = SaveCheckpointStorage.Read(PathToFile);
        }
        catch (System.Exception e)
        {
            // Keep an unreadable/newer save untouched instead of silently
            // replacing it with a fresh game's first completed day.
            Loaded = new SaveData();
            HasSave = false;
            writesBlocked = true;
            LastSaveError = "Existing save could not be loaded. It has not been overwritten.";
            Debug.LogError($"[Save] {LastSaveError} {e.Message}");
        }
    }

    // Kept as a void entry point for any existing UnityEvent wiring.
    public void Save() => TrySaveRecap();

    public bool TrySaveRecap()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.DayOver)
            return FailSave("A recap can only be saved after the day has closed.");
        return Commit(CaptureState(clock));
    }

    public bool TrySaveNextDay()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.DayOver)
            return FailSave("The current day has not finished.");

        SaveData current = CaptureState(clock);
        if (!current.TryCreateNextDay(out SaveData next)) return false;

        // Commit tomorrow BEFORE opening it. Failure leaves today's recap
        // active, with a visible error and a safe opportunity to retry.
        if (!Commit(next)) return false;

        // Tomorrow is safely on disk: start collecting its reviews.
        reputation.BeginDay(next.day);
        reviewedToday.Clear();
        return true;
    }

    private SaveData CaptureState(DayClock clock)
    {
        // A closed day's checkpoint always includes its settled reviews.
        // DayClock.EndDay settles first; this is only a safety net, and
        // settling is once per day, so later saves never count it twice.
        if (clock.DayOver) SettleReputation(clock.Day);

        SaveData data = new SaveData
        {
            day = clock.Day,
            dayCompleted = clock.DayOver,
            recap = clock.DayOver ? clock.CaptureRecap() : null,
            money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0,
            cups = ShopInventory.Instance != null ? ShopInventory.Instance.Cups : 20,
            beans = ShopInventory.Instance != null ? ShopInventory.Instance.Beans : 20
        };

        if (UpgradeManager.Instance != null && UpgradeManager.Instance.Catalogue != null)
        {
            List<string> names = new();
            List<int> levels = new();

            foreach (UpgradeDefinition def in UpgradeManager.Instance.Catalogue)
            {
                if (def == null) continue;
                int level = UpgradeManager.Instance.LevelOf(def);
                if (level <= 0) continue;

                names.Add(def.name);      // the ASSET name — the save identity
                levels.Add(level);
            }

            data.upgradeNames = names.ToArray();
            data.upgradeLevels = levels.ToArray();
        }

        data.regularMemories = SnapshotRegularMemory();
        data.reputation = reputation.Reputation;
        data.starsEarned = reputation.StarsEarned;
        if (data.recap != null) reputation.WriteRecap(data.recap);
        return data;
    }

    private bool Commit(SaveData data)
    {
        if (writesBlocked) return FailSave(LastSaveError);

        try
        {
            SaveCheckpointStorage.Write(PathToFile, data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Save] Could not write checkpoint: {e.Message}");
            return FailSave("Progress was not saved. Check the Console, then retry Continue.");
        }

        Loaded = data;
        HasSave = true;
        LastSaveError = "";
        SaveStatusChanged?.Invoke();
        return true;
    }

    private bool FailSave(string message)
    {
        LastSaveError = message;
        SaveStatusChanged?.Invoke();
        return false;
    }

    public int RelationshipFor(CustomerProfile profile)
    {
        RegularMemoryData memory = MemoryFor(profile);
        return memory != null ? memory.relationship : 0;
    }

    public bool HasMet(CustomerProfile profile)
    {
        RegularMemoryData memory = MemoryFor(profile);
        return memory != null && memory.visits > 0;
    }

    public void RecordRegularVisit(
        CustomerProfile profile,
        bool happy,
        bool accepted,
        bool served,
        LostReason lossReason,
        string grade,
        bool focusRequested = false,
        Job storyJob = null)
    {
        if (profile == null) return;
        regularMemory.RecordVisit(profile.PersistentId,
            DayClock.Instance != null ? DayClock.Instance.Day : 0,
            happy, accepted, served, lossReason, grade, focusRequested);
        if (storyJob != null && storyJob.kind == JobKind.Repair)
            regularMemory.RecordGraceCamera(profile.PersistentId, storyJob.storyEpisodeId,
                DayClock.Instance != null ? DayClock.Instance.Day : 0, grade);
    }

    public RegularMemoryData MemoryForId(string profileId) => regularMemory.Read(profileId);

    public bool AcknowledgeGraceReturn(CustomerProfile profile, out GracePhotoOutcome outcome)
    {
        outcome = GracePhotoOutcome.None;
        return profile != null && regularMemory.AcknowledgeGraceReturn(profile.PersistentId,
            DayClock.Instance != null ? DayClock.Instance.Day : 0, out outcome);
    }

    public RegularMemoryData MemoryFor(CustomerProfile profile) =>
        profile != null ? regularMemory.Read(profile.PersistentId) : null;

    private void RebuildRegularMemory()
    {
        regularMemory.Restore(Loaded != null ? Loaded.regularMemories : null);
    }

    private RegularMemoryData[] SnapshotRegularMemory() => regularMemory.Snapshot();

    // ---------- reputation (claude/reputation-spec.md) ----------

    /// <summary>The café's reputation, for the recap and the day log. Read it; don't change it.</summary>
    public ReputationLedger Reputation => reputation;

    /// <summary>Called by CustomerBrain.Depart with the same facts DayLog gets:
    /// one review per visit, counted into stars at closing.</summary>
    public void RecordReview(CustomerBrain customer, ReviewFacts facts)
    {
        if (customer == null || !reviewedToday.Add(customer)) return;
        Judgement judgement = ReputationRules.Judge(facts);
        if (judgement.review != Review.None) reputation.Record(ReviewOf(customer, judgement));
    }

    /// <summary>Reviews are posted at closing. Counts today's reviews into the
    /// café's reputation, once per day: resuming a recap, or saving it again
    /// after a purchase, never counts the day twice.</summary>
    public void SettleReputation(int day)
    {
        if (reputation.Settled) return;

        // Anyone still waiting when the day closes never reaches Depart.
        // Same rule as DayLog's sweep: they leave "still waiting at closing".
        foreach (CustomerBrain customer in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
        {
            if (customer == null || !customer.isActiveAndEnabled || customer.IsLeaving) continue;
            if (!reviewedToday.Add(customer)) continue;
            Judgement judgement = ReputationRules.Judge(new ReviewFacts
            {
                reason = LostReason.StillInShopAtClose,
                accepted = customer.WasAccepted,
                patienceAtExit = customer.PatienceFraction
            });
            if (judgement.review != Review.None) reputation.Record(ReviewOf(customer, judgement));
        }

        reputation.Settle(day, WriteQuote);
    }

    private void RebuildReputation()
    {
        reviewedToday.Clear();
        SaveData data = Loaded ?? new SaveData();
        reputation.Restore(data.reputation, data.starsEarned, data.day, data.dayCompleted, data.recap);
    }

    private static ReviewEntry ReviewOf(CustomerBrain customer, Judgement judgement)
    {
        Job job = customer.Record;
        DrinkDefinition wish = customer.WantedDrink;
        return new ReviewEntry
        {
            review = judgement.review,
            reason = judgement.reason,
            name = customer.CustomerName,
            thing = job != null ? job.Subject : "",
            drink = wish != null ? wish.drinkName : "",
            regular = customer.Identity != null && customer.Identity.IsRegular
        };
    }

    // The quoted line: one of the writers' lines for this kind of visit,
    // always the same one for the same day and customer.
    private string WriteQuote(ReviewEntry entry, int position)
    {
        ReviewLines lines = reviewLines != null ? reviewLines : FallbackLines();
        string[] pool = lines.For(entry.reason);
        if (pool == null || pool.Length == 0) return null;
        string line = ReputationRules.Fill(pool[StableIndex(reputation.Day, entry.name, position, pool.Length)],
            entry.name, entry.thing, entry.drink);
        return ReputationRules.Quote(line, entry.name);
    }

    private static ReviewLines FallbackLines()
    {
        if (fallbackLines == null)
        {
            fallbackLines = ScriptableObject.CreateInstance<ReviewLines>();
            fallbackLines.hideFlags = HideFlags.HideAndDontSave;
        }
        return fallbackLines;
    }

    // Not string.GetHashCode, which is allowed to change between runs.
    private static int StableIndex(int day, string name, int position, int count)
    {
        unchecked
        {
            uint h = 2166136261u ^ (uint)day;
            foreach (char c in name ?? "") h = (h ^ c) * 16777619u;
            h = (h ^ (uint)position) * 16777619u;
            return (int)(h % (uint)count);
        }
    }

    // Right-click the component header in the Inspector for these.
    [ContextMenu("Delete Save (New Game)")]
    private void DeleteSave()
    {
        if (File.Exists(PathToFile)) File.Delete(PathToFile);
        Debug.Log("Save deleted. Next play starts fresh.");
    }

    [ContextMenu("Print Save Path")]
    private void PrintPath()
    {
        Debug.Log(PathToFile);
    }
}
