using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecapUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private PlayerInteractor player;

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
            $"Customers served      {c.Served}\n" +
            $"Customers lost        {c.Lost}\n" +
            $"Repairs completed     {c.Repairs}\n\n" +
            $"Tips                  ${c.Tips}\n" +
            $"Earned today          ${c.Earned}\n\n" +
            $"Till                  ${(ShopEconomy.Instance != null ? ShopEconomy.Instance.Money : 0)}";

        panel.SetActive(true);
    }

    private void OnNextDay()
    {
        panel.SetActive(false);
        if (DayClock.Instance != null) DayClock.Instance.NextDay();
    }
}