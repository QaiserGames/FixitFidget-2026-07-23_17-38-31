using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // Filled during Awake so other systems can read it in their Start.
    public SaveData Loaded { get; private set; }
    public bool HasSave { get; private set; }
    public string LastSaveError { get; private set; } = "";
    public event Action SaveStatusChanged;

    private bool writesBlocked;

    private readonly CustomerMemoryService regularMemory = new();

    private string PathToFile => Path.Combine(Application.persistentDataPath, "save.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        Instance = this;
        LoadFromDisk();
        RebuildRegularMemory();
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
        return Commit(next);
    }

    private SaveData CaptureState(DayClock clock)
    {
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
        string grade)
    {
        if (profile == null) return;
        regularMemory.RecordVisit(profile.PersistentId,
            DayClock.Instance != null ? DayClock.Instance.Day : 0,
            happy, accepted, served, lossReason, grade);
    }

    public RegularMemoryData MemoryFor(CustomerProfile profile) =>
        profile != null ? regularMemory.Read(profile.PersistentId) : null;

    private void RebuildRegularMemory()
    {
        regularMemory.Restore(Loaded != null ? Loaded.regularMemories : null);
    }

    private RegularMemoryData[] SnapshotRegularMemory() => regularMemory.Snapshot();

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
