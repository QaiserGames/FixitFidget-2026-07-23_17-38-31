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

    // Ace's notebook (claude/night-notebook-spec.md): what Ace has learned about
    // the regulars. Filled by NotebookHooks during the day, shown in the
    // recap, saved with the checkpoint like the regulars' memory.
    private readonly Notebook notebook = new();
    // What Ace's nights have done (the Night 1 slice): trophies, the mornings still to
    // come, suspicion. Saved with the next morning's checkpoint, like the notebook.
    private readonly NightLedger night = new();
    // The draft lines from ReviewLines.cs, for scenes without a lines asset.
    // One shared copy per editor/game session.
    private static ReviewLines fallbackLines;

    // A café lab session (editor only, see CafeLab) keeps its own checkpoint,
    // so testing the café's people never touches the real playtest save.
    // A built player keeps its own as well: the editor and a build share the persistent data
    // folder, and on 30 Sept a test build left running closed its Day 3 with nobody serving and
    // saved it over the editor's playtest save. A build's file ends in "-build"
    // (playtest-aces-cafe-build.json, save-build.json), so the two never share a checkpoint.
    private string PathToFile => Path.Combine(Application.persistentDataPath, SaveFileName);

    private string SaveFileName
    {
        get
        {
            if (CafeLab.Active) return CafeLab.SaveFileName;
            string name = useInteractionPlaytestSave ? PlaytestFileName : "save.json";
            return Application.isEditor ? name : BuildFileName(name);
        }
    }

    // "playtest-aces-cafe.json" -> "playtest-aces-cafe-build.json".
    public static string BuildFileName(string editorFileName) =>
        Path.GetFileNameWithoutExtension(editorFileName) + "-build" + Path.GetExtension(editorFileName);

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
        RebuildNotebook();
        night.Restore(Loaded != null ? Loaded.night : null);
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
        // 24 Oct 2026 (Days): the thrift find sells by itself at closing, before the till is read below; once a day, like the
        // reviews (ThriftShelf.SettleAtClosing).
        if (clock.DayOver) ThriftShelf.SettleAtClosing(clock.Day);

        SaveData data = new SaveData
        {
            day = clock.Day,
            dayCompleted = clock.DayOver,
            recap = clock.DayOver ? clock.CaptureRecap() : null,
            money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0,
            cups = ShopInventory.Instance != null ? ShopInventory.Instance.Cups : 20,
            beans = ShopInventory.Instance != null ? ShopInventory.Instance.Beans : 20
        };
        // 24 Oct 2026 (Days): restock by hand. cups / beans above stay the whole stock; this is the machine's share of it (the
        // rest is on the back shelf). No stock in the scene: -1, the machine fills from the stock at load.
        if (ShopInventory.Instance != null)
        {
            data.machineCups = ShopInventory.Instance.MachineCups;
            data.machineBeans = ShopInventory.Instance.MachineBeans;
        }

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

        // 24 Oct 2026 (Days): the thrift find (which, for which day, fixed or not; never its screws). A scene without the shelf
        // keeps what was loaded.
        data.thrift = ThriftShelf.Instance != null ? ThriftShelf.Instance.Snapshot()
            : Loaded != null && Loaded.thrift != null ? Loaded.thrift.Copy() : new ThriftSaveData();
        data.regularMemories = SnapshotRegularMemory();
        // 24 Oct 2026 (Furniture): the café's furniture (CafeLayout). A scene without it carries the loaded layout forward, so a
        // save made there never drops the café's.
        data.cafe = CafeLayout.Instance != null ? CafeLayout.Instance.Snapshot() : Loaded != null && Loaded.cafe != null ? Loaded.cafe : new CafeLayoutData();
        data.notebook = notebook.Snapshot();
        data.night = night.Snapshot();
        data.reputation = reputation.Reputation;
        data.starsEarned = reputation.StarsEarned;
        if (data.recap != null) reputation.WriteRecap(data.recap);
        return data;
    }

    private bool Commit(SaveData data)
    {
        if (writesBlocked) return FailSave(LastSaveError);
        // 26 Oct 2026 (Start screen): the title never writes or changes a save. While it is up nothing reaches the disk (it
        // leaves by reloading the scene, so nothing it did is ever in a save either).
        if (TitleScreen.Up) return FailSave("Nothing is saved while the start screen is up.");

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
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        // Her memory BEFORE this visit is written (specs/days-1-3.md §5.3, R3): the photo's grade reads her clean visit
        // from it, and RecordVisit below overwrites lastVisitServed and lastGrade with this visit's.
        RegularMemoryData before = regularMemory.Read(profile.PersistentId);
        regularMemory.RecordVisit(profile.PersistentId, day,
            happy, accepted, served, lossReason, grade, focusRequested);
        // Grace's camera comes in twice (Days 1-3): only the shutter visit records the photo's attempt (R3). No save field
        // changes: the same graceCamera* fields, written on Day 2 instead of Day 1.
        if (storyJob != null && storyJob.kind == JobKind.Repair)
            regularMemory.RecordGraceCameraVisit(profile.PersistentId, storyJob.storyEpisodeId,
                GraceCameraEpisode.VisitFor(storyJob.faultType == FaultType.Cleaning), day, grade, before);
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

    /// <summary>Ace's notebook: what Ace has learned about the regulars so far.</summary>
    public Notebook Notebook => notebook;

    /// <summary>What Ace's nights have done: the trophies, the mornings still to come, who suspects Ace.</summary>
    public NightLedger Night => night;

    // The saved notebook, plus anything an older save's memories already imply
    // (Grace's camera visit before the notebook existed). Idempotent.
    private void RebuildNotebook()
    {
        notebook.Restore(Loaded != null ? Loaded.notebook : null);
        foreach ((NotebookFactData fact, int day) in NotebookEntries.Backfill(Loaded != null ? Loaded.regularMemories : null))
            notebook.Learn(fact, day);
    }

    /// <summary>Called by CustomerBrain.Depart with the same facts DayLog gets:
    /// one review per visit, counted into stars at closing.</summary>
    public void RecordReview(CustomerBrain customer, ReviewFacts facts)
    {
        if (customer == null || !reviewedToday.Add(customer)) return;
        Judgement judgement = ReputationRules.Judge(facts);
        // Written now as well as at closing (the same line both times): the phone by day shows today's reviews live.
        if (judgement.review != Review.None) reputation.Record(ReviewOf(customer, judgement), WriteCard);
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
            if (judgement.review != Review.None) reputation.Record(ReviewOf(customer, judgement), WriteCard);
        }

        reputation.Settle(day, WriteQuote, WriteCard);
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
        if (!string.IsNullOrWhiteSpace(entry.line)) return ReputationRules.Quote(entry.line, entry.name);   // written already (planted)
        ReviewLines lines = reviewLines != null ? reviewLines : FallbackLines();
        string[] pool = lines.For(entry.reason);
        if (pool == null || pool.Length == 0) return null;
        string line = ReputationRules.Fill(pool[StableIndex(reputation.Day, entry.name, position, pool.Length)],
            entry.name, entry.thing, entry.drink);
        return ReputationRules.Quote(line, entry.name);
    }

    // Every review's line on the recap phone: the same pool, plain (no quotation marks or
    // signature), always the same one for the same day and customer. It picks as the best
    // review's quote does, so the day's best review reads the same on both.
    private string WriteCard(ReviewEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.line)) return entry.line.Trim();   // written already (planted)
        ReviewLines lines = reviewLines != null ? reviewLines : FallbackLines();
        string[] pool = lines.For(entry.reason);
        if (pool == null || pool.Length == 0) return null;
        return ReputationRules.Fill(pool[StableIndex(reputation.Day, entry.name, 0, pool.Length)],
            entry.name, entry.thing, entry.drink);
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

    // ---------- 26 Oct 2026 (Start screen): New Game over a save keeps a copy first ----------

    public const string BackupsFolderName = "backups";

    /// <summary>
    /// The start screen's New Game over a save (confirmed on its card): the save is copied to
    /// &lt;its folder&gt;/backups/&lt;file&gt;.&lt;yyyy-MM-dd_HHmmss&gt;.bak and the copy proved byte for byte, exactly as the editor's
    /// "Fresh game 1" keeps his save (FreshGameMenu.BackUp), and then the save is taken away, so the scene's next boot is a real
    /// new game (no file: the half-finished café, Day 1). Nothing is written over anything. False, with nothing changed, if
    /// the copy can't be made or proved. True with <paramref name="kept"/> null when there was no file to keep.
    /// </summary>
    public bool StartNewGameKeepingACopy(out string kept, out string problem)
    {
        kept = null;
        problem = null;
        string path = PathToFile;
        if (!File.Exists(path)) return true;
        kept = KeepACopy(path, out problem);
        if (kept == null) return false;
        try { File.Delete(path); }
        catch (Exception e)
        {
            problem = $"Kept a copy at {kept}, but couldn't take the save away ({e.Message}). Your save is as it was.";
            return false;
        }
        return true;
    }

    /// <summary>Copies <paramref name="path"/> to backups/&lt;file&gt;.&lt;stamp&gt;.bak beside it (always a new name, never over an
    /// older copy) and proves the copy byte for byte. The copy's path, or null with <paramref name="problem"/> said.</summary>
    public static string KeepACopy(string path, out string problem)
    {
        problem = null;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(path) ?? "", BackupsFolderName);
            Directory.CreateDirectory(folder);
            string name = Path.GetFileName(path);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string target = Path.Combine(folder, $"{name}.{stamp}.bak");
            for (int n = 2; File.Exists(target); n++) target = Path.Combine(folder, $"{name}.{stamp}-{n}.bak");
            File.Copy(path, target, false);
            byte[] a = File.ReadAllBytes(path), b = File.ReadAllBytes(target);
            bool same = a.Length == b.Length;
            for (int i = 0; same && i < a.Length; i++) same = a[i] == b[i];
            if (!same)
            {
                problem = $"The copy at {target} doesn't match {path} byte for byte. Nothing else was done.";
                return null;
            }
            return target;
        }
        catch (Exception e)
        {
            problem = $"Couldn't copy {path} into its backups folder: {e.Message}. Nothing else was done.";
            return null;
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
