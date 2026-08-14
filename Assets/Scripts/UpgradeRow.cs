using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyLabel;

    private UpgradeDefinition def;
    private UpgradeShopUI shop;

    public void Bind(UpgradeDefinition definition, UpgradeShopUI owner)
    {
        def = definition;
        shop = owner;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => shop.OnBuy(def));
        }

        Refresh();
    }

    public void Refresh()
    {
        if (def == null || UpgradeManager.Instance == null) return;

        int level = UpgradeManager.Instance.LevelOf(def);
        bool maxed = UpgradeManager.Instance.IsMaxed(def);

        if (nameText != null)
            nameText.text = level > 0 ? $"{def.upgradeName}  Lv.{level}" : def.upgradeName;

        if (descText != null) descText.text = def.description;

        if (buyLabel != null)
            buyLabel.text = maxed ? "MAX" : $"${def.CostAt(level)}";

        if (buyButton != null)
            buyButton.interactable = UpgradeManager.Instance.CanAfford(def);
    }
}