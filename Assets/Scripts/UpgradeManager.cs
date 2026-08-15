using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private UpgradeDefinition[] catalogue;

    public UpgradeDefinition[] Catalogue => catalogue;

    private readonly Dictionary<UpgradeDefinition, int> owned = new();

    private void Awake()
    {
        Instance = this;
    }

    public int LevelOf(UpgradeDefinition def)
    {
        if (def == null) return 0;
        return owned.TryGetValue(def, out int level) ? level : 0;
    }

    public bool IsMaxed(UpgradeDefinition def) => def != null && LevelOf(def) >= def.MaxLevel;

    public bool CanAfford(UpgradeDefinition def)
    {
        if (def == null || IsMaxed(def)) return false;
        if (ShopEconomy.Instance == null) return false;
        return ShopEconomy.Instance.Money >= def.CostAt(LevelOf(def));
    }

    public bool Buy(UpgradeDefinition def)
    {
        if (!CanAfford(def)) return false;

        int cost = def.CostAt(LevelOf(def));
        ShopEconomy.Instance.AddMoney(-cost);
        owned[def] = LevelOf(def) + 1;
        return true;
    }

    // ---------- save / load ----------

    // Restore one upgrade to a saved level. Clamped to the definition's max,
    // in case a later patch reduced the level cap.
    public void SetLevel(UpgradeDefinition def, int level)
    {
        if (def == null) return;
        owned[def] = Mathf.Clamp(level, 0, def.MaxLevel);
    }

    // Find a definition by its asset name — the identity used in save files.
    public UpgradeDefinition FindByName(string assetName)
    {
        if (catalogue == null) return null;
        foreach (UpgradeDefinition def in catalogue)
            if (def != null && def.name == assetName) return def;
        return null;
    }

    // ---------- what the rest of the game asks ----------

    private float TotalValue(UpgradeType type)
    {
        float total = 0f;
        if (catalogue == null) return total;

        foreach (UpgradeDefinition def in catalogue)
        {
            if (def == null || def.type != type) continue;
            total += def.valuePerLevel * LevelOf(def);
        }
        return total;
    }

    public float BrewTimeMultiplier  => Mathf.Max(0.25f, 1f - TotalValue(UpgradeType.BrewSpeed));
    public float ScrewTimeMultiplier => Mathf.Max(0.25f, 1f - TotalValue(UpgradeType.ScrewSpeed));
    public float ScrubSpeedMultiplier => 1f + TotalValue(UpgradeType.ScrubSpeed);

    public int ExtraBenchSlots => Mathf.RoundToInt(TotalValue(UpgradeType.BenchCapacity));
    public int ExtraRestock    => Mathf.RoundToInt(TotalValue(UpgradeType.RestockSize));
}