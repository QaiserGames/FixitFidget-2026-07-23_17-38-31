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

    private readonly Dictionary<string, RegularMemoryData> regularMemory =
        new(StringComparer.Ordinal);

    private string PathToFile => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        Instance = this;
        LoadFromDisk();
        RebuildRegularMemory();
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
            string json = File.ReadAllText(PathToFile);
            Loaded = JsonUtility.FromJson<SaveData>(json);

            if (Loaded == null) Loaded = new SaveData();

            // Version 1 had no regular-customer memory. Missing JSON fields load
            // as null, so migrate in place without discarding money, stock,
            // upgrades, or the current day.
            if (Loaded.regularMemories == null)
                Loaded.regularMemories = new RegularMemoryData[0];

            if (Loaded.version < 2)
                Loaded.version = 2;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Save file unreadable, starting fresh: {e.Message}");
            Loaded = new SaveData();
            HasSave = false;
        }
    }

    // Gather the current state of every system and write it out.
    public void Save()
    {
        SaveData data = new SaveData
        {
            version = 2,
            day = DayClock.Instance != null ? DayClock.Instance.Day : 1,
            money = ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0,
            cups = ShopInventory.Instance != null ? ShopInventory.Instance.Cups : 20,
            beans = ShopInventory.Instance != null ? ShopInventory.Instance.Beans : 20
        };

        if (UpgradeManager.Instance != null)
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
        Loaded = data;

        try
        {
            File.WriteAllText(PathToFile, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
        }
    }

    public int RelationshipFor(CustomerProfile profile)
    {
        RegularMemoryData memory = GetOrCreate(profile);
        return memory != null ? memory.relationship : 0;
    }

    public bool HasMet(CustomerProfile profile)
    {
        RegularMemoryData memory = GetOrCreate(profile);
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
        RegularMemoryData memory = GetOrCreate(profile);
        if (memory == null) return;

        memory.visits++;
        memory.relationship = Mathf.Clamp(
            memory.relationship + RelationshipDelta(happy, accepted, served, lossReason),
            -4,
            6);

        memory.lastSeenDay = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        memory.lastVisitHappy = happy;
        memory.lastJobAccepted = accepted;
        memory.lastVisitServed = served;
        memory.lastLossReason = happy ? "" : lossReason.ToString();
        memory.lastGrade = grade ?? "";
    }

    private RegularMemoryData GetOrCreate(CustomerProfile profile)
    {
        if (profile == null) return null;

        string id = profile.PersistentId;
        if (string.IsNullOrWhiteSpace(id)) return null;

        if (!regularMemory.TryGetValue(id, out RegularMemoryData memory))
        {
            memory = new RegularMemoryData { profileId = id };
            regularMemory.Add(id, memory);
        }

        return memory;
    }

    private void RebuildRegularMemory()
    {
        regularMemory.Clear();
        if (Loaded == null || Loaded.regularMemories == null) return;

        foreach (RegularMemoryData memory in Loaded.regularMemories)
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.profileId)) continue;
            regularMemory[memory.profileId] = memory;
        }
    }

    private RegularMemoryData[] SnapshotRegularMemory()
    {
        List<RegularMemoryData> snapshot = new(regularMemory.Values);
        snapshot.Sort((a, b) => string.CompareOrdinal(a.profileId, b.profileId));
        return snapshot.ToArray();
    }

    private static int RelationshipDelta(
        bool happy,
        bool accepted,
        bool served,
        LostReason reason)
    {
        // A successful first visit reaches relationship 2, which is the existing
        // threshold for warm dialogue on the next visit.
        if (happy && served) return 2;

        // The player could not take these jobs; remember the visit without
        // treating a capacity or stock failure like a personal betrayal.
        if (reason == LostReason.OutOfStock || reason == LostReason.ShelfFull)
            return 0;

        // Abandoning an accepted promise or ignoring somebody until they leave
        // should land harder than honestly declining at the counter.
        if (accepted || reason == LostReason.StormedOutInQueue
                     || reason == LostReason.StormedOutWaiting)
            return -2;

        return -1;
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