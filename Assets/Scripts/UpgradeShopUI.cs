using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeShopUI : MonoBehaviour
{
    [SerializeField] private Transform listRoot;
    [SerializeField] private UpgradeRow rowPrefab;
    [SerializeField] private Button restockButton;
    [SerializeField] private TMP_Text restockLabel;
    [SerializeField] private TMP_Text moneyLabel;

    private readonly List<UpgradeRow> rows = new();

    private void Start()
    {
        if (restockButton != null) restockButton.onClick.AddListener(OnRestock);
    }

    private void OnDestroy()
    {
        if (restockButton != null) restockButton.onClick.RemoveListener(OnRestock);
    }

    // Called by RecapUI when the day ends.
    public void Build()
    {
        foreach (UpgradeRow r in rows) if (r != null) Destroy(r.gameObject);
        rows.Clear();

        if (UpgradeManager.Instance == null || rowPrefab == null) return;

        foreach (UpgradeDefinition def in UpgradeManager.Instance.Catalogue)
        {
            if (def == null) continue;
            UpgradeRow row = Instantiate(rowPrefab, listRoot);
            row.Bind(def, this);
            rows.Add(row);
        }

        Refresh();
    }

    public void OnBuy(UpgradeDefinition def)
    {
        if (DayClock.Instance == null || !DayClock.Instance.DayOver) return;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.Buy(def))
            PersistPurchase();
    }

    private void OnRestock()
    {
        if (DayClock.Instance == null || !DayClock.Instance.DayOver) return;
        if (ShopInventory.Instance != null && ShopInventory.Instance.BuyRestock())
            PersistPurchase();
    }

    private void PersistPurchase()
    {
        // Only save after a successful transaction, with BOTH the deduction
        // and its purchased stock/level already applied. Never charge on load.
        if (SaveManager.Instance != null) SaveManager.Instance.TrySaveRecap();
        else Debug.LogError("[Save] Purchase could not be saved: no SaveManager.");
        Refresh();
    }

    private void Refresh()
    {
        foreach (UpgradeRow r in rows) if (r != null) r.Refresh();

        if (moneyLabel != null && ShopEconomy.Instance != null)
            moneyLabel.text = $"${ShopEconomy.Instance.Money}";

        if (restockLabel != null && ShopInventory.Instance != null)
        {
            var inv = ShopInventory.Instance;
            restockLabel.text = $"Restock  (${inv.RestockCost})\nCups {inv.Cups}   Beans {inv.Beans}";
        }

        if (restockButton != null && ShopInventory.Instance != null && ShopEconomy.Instance != null)
            restockButton.interactable = ShopEconomy.Instance.Money >= ShopInventory.Instance.RestockCost;
    }
}
