using UnityEngine;

// Pushes loaded save data into every system, once, at startup.
// Runs in Start so every system's Awake (and their Instance fields)
// has already happened.
public class GameBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Loaded == null) return;

        SaveData data = SaveManager.Instance.Loaded;

        if (ShopEconomy.Instance != null) ShopEconomy.Instance.SetMoney(data.money);
        if (DayClock.Instance != null) DayClock.Instance.SetDay(data.day);
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
    }
}