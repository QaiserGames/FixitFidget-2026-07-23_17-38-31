using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // Filled during Awake so other systems can read it in their Start.
    public SaveData Loaded { get; private set; }
    public bool HasSave { get; private set; }

    private string PathToFile => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        Instance = this;
        LoadFromDisk();
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

            // Future migrations happen here:
            // if (Loaded.version < 2) { ...upgrade the data... Loaded.version = 2; }
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
            version = 1,
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

        try
        {
            File.WriteAllText(PathToFile, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
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