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

    private SaveManager saveManager;
 
    private void Start()
    {
        panel.SetActive(false);
        nextDayButton.onClick.AddListener(OnNextDay);
 
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded += Show;

        saveManager = SaveManager.Instance;
        if (saveManager != null) saveManager.SaveStatusChanged += RefreshText;

        // Bootstrap restores state earlier in Start. Resume only the UI,
        // not EndDay, payouts, regular visits, or log generation.
        if (DayClock.Instance != null && DayClock.Instance.DayOver) Show();
    }
 
    private void OnDestroy()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded -= Show;
        if (saveManager != null) saveManager.SaveStatusChanged -= RefreshText;
        if (nextDayButton != null) nextDayButton.onClick.RemoveListener(OnNextDay);
    }
 
    private void Show()
    {
        // Get the player out of any station so they aren't stuck behind the panel.
        if (player != null) player.ExitStation();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshText();
        if (upgradeShop != null) upgradeShop.Build();
        if (nextDayButton != null) nextDayButton.interactable = true;
        panel.SetActive(true);
    }

    private void RefreshText()
    {
        var c = DayClock.Instance;
        if (c == null || !c.DayOver || text == null) return;

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
            $"Closing till          ${c.CaptureRecap().closingTill}";

        if (saveManager != null && !string.IsNullOrEmpty(saveManager.LastSaveError))
            text.text += $"\n\n<color=#FFB3A7>SAVE FAILED: {saveManager.LastSaveError}</color>";
    }
 
    private void OnNextDay()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.DayOver) return;

        if (nextDayButton != null) nextDayButton.interactable = false;
        if (clock.TryNextDay()) panel.SetActive(false);
        else
        {
            if (nextDayButton != null) nextDayButton.interactable = true;
            RefreshText();
        }
    }
}
