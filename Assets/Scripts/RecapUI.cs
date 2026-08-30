using UnityEngine;
using UnityEngine.UI;
using TMPro;
 
public class RecapUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private PlayerInteractor player;
    [SerializeField] private UpgradeShopUI upgradeShop;
 
    private void Start()
    {
        panel.SetActive(false);
        nextDayButton.onClick.AddListener(OnNextDay);
 
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded += Show;
    }
 
    private void OnDestroy()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded -= Show;
    }
 
    private void Show()
    {
        var c = DayClock.Instance;
 
        // Get the player out of any station so they aren't stuck behind the panel.
        if (player != null) player.ExitStation();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
 
        text.text =
            $"DAY {c.Day} — CLOSED\n\n" +
            $"People served         {c.Visitors}\n" +
            $"Customers lost        {c.Lost}\n" +
            $"Turned away           {c.Declined}\n" +
            $"Orders completed      {c.Served}\n" +
            $"Cafe walk-ins         ${c.PatronIncome}\n" +
            $"Repairs completed     {c.Repairs}\n" +
            $"   Perfect            {c.Perfect}\n" +
            $"   Good               {c.Good}\n" +
            $"   Passable           {c.Passable}\n\n" +
            $"Tips                  ${c.Tips}\n" +
            $"Earned today          ${c.Earned}\n\n" +
            $"Till                  ${(ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0)}";
            if (upgradeShop != null) upgradeShop.Build();
           
 
        panel.SetActive(true);
    }
 
    private void OnNextDay()
    {
        
        panel.SetActive(false);
        if (DayClock.Instance != null) DayClock.Instance.NextDay();
        if (SaveManager.Instance != null) SaveManager.Instance.Save();
    }
}