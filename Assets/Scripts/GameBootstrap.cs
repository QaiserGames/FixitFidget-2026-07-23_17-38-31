using UnityEngine;

// Pushes loaded save data into every system, once, at startup.
// Runs in Start so every system's Awake (and their Instance fields)
// has already happened.
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Loaded == null) return;

        SaveData data = SaveManager.Instance.Loaded;

        if (ShopEconomy.Instance != null) ShopEconomy.Instance.SetMoney(data.money);
        if (ShopInventory.Instance != null) ShopInventory.Instance.SetStock(data.cups, data.beans);

        if (UpgradeManager.Instance != null)
        {
            int count = Mathf.Min(data.upgradeNames.Length, data.upgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                UpgradeDefinition def = UpgradeManager.Instance.FindByName(data.upgradeNames[i]);
                if (def != null)
                    UpgradeManager.Instance.SetLevel(def, data.upgradeLevels[i]);
                else
                    Debug.LogWarning($"Save references unknown upgrade '{data.upgradeNames[i]}' — was an asset renamed?");
            }
        }

        // 24 Oct 2026 (Furniture): the café's furniture from the save (a new game opens half-finished, an old save keeps today's
        // café: CafeLayout.Apply), before the clock is restored and before either spawner's first Update. Not "?.": a destroyed
        // object is Unity's fake null.
        // 26 Oct 2026 (Start screen): the first start's cold open (no save yet) shows today's full café, the 28-seat layout
        // (format 0), laid in memory only: the title never writes it, and leaves by reloading the scene, so the new game that
        // follows boots half-finished from here as always.
        if (CafeLayout.Instance != null)
        {
            if (TitleScreen.ColdOpenNow) CafeLayout.Instance.Apply(new SaveData(), true);
            else CafeLayout.Instance.Apply(data, SaveManager.Instance.HasSave);
        }

        // Apply balances/upgrades before RecapUI.Start builds its rows, and
        // restore a closed clock before either spawner gets its first Update.
        if (DayClock.Instance != null)
        {
            DayClock.Instance.SetDay(data.day);
            // 26 Oct 2026 (Start screen): not under the title (the recap would freeze the diorama and take the screen); Continue
            // reloads the scene and the recap comes back here as always.
            if (data.dayCompleted && !TitleScreen.Up) DayClock.Instance.RestoreRecap(data.recap);
        }
    }
}
