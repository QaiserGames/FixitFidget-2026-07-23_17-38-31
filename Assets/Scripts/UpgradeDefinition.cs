using UnityEngine;

public enum UpgradeType
{
    BrewSpeed,        // espresso machine is faster
    ScrewSpeed,       // magnetic driver — screws come out quicker
    BenchCapacity,    // more items on the bench at once
    RestockSize,      // more stock per purchase
    ScrubSpeed,       // wider brush
    ShelfCapacity     // more room on the intake shelf — take more jobs before you're blocked
    // NOTE: only ever append to this list. Unity serialises enums by their
    // number, so inserting in the middle would silently re-point every
    // Upgrade_ asset you've already made.
}

[CreateAssetMenu(fileName = "Upgrade_", menuName = "FixitFiasco/Upgrade")]
public class UpgradeDefinition : ScriptableObject
{
    public string upgradeName = "Faster Machine";

    [TextArea(2, 3)]
    public string description = "Pulls shots in half the time.";

    public UpgradeType type;

    [Tooltip("What each level does. Meaning depends on type — see UpgradeManager.")]
    public float valuePerLevel = 0.2f;

    [Tooltip("Cost of each level, in order. Array length = max levels.")]
    public int[] costPerLevel = { 60, 140, 300 };

    public int MaxLevel => costPerLevel != null ? costPerLevel.Length : 0;

    public int CostAt(int level)
    {
        if (costPerLevel == null || level < 0 || level >= costPerLevel.Length) return 0;
        return costPerLevel[level];
    }
}
