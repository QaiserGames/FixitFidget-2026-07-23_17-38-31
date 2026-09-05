using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Cinemachine;
 
public class RecapUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private PlayerInteractor player;
    [SerializeField] private UpgradeShopUI upgradeShop;

    private SaveManager saveManager;
    private readonly List<CinemachineInputAxisController> pausedCameraInputs = new();
 
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
        ResumeCameraInput();
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded -= Show;
        if (saveManager != null) saveManager.SaveStatusChanged -= RefreshText;
        if (nextDayButton != null) nextDayButton.onClick.RemoveListener(OnNextDay);
    }
 
    private void Show()
    {
        SuspendCameraInput(FindObjectsByType<CinemachineInputAxisController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None));
        // Get the player out of any station so they aren't stuck behind the panel.
        if (player != null)
        {
            player.ExitStation();
            player.GetComponent<ItemInspector>()?.CancelInspection();
            player.GetComponent<ConversationController>()?.End();
            player.GetComponent<PlayerMovement>()?.ClearInput();
        }
        foreach (HoverTooltipUI tooltip in FindObjectsByType<HoverTooltipUI>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            tooltip.HideImmediately();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshText();
        if (upgradeShop != null) upgradeShop.Build();
        if (nextDayButton != null) nextDayButton.interactable = true;
        panel.SetActive(true);
    }

    private void SuspendCameraInput(IEnumerable<CinemachineInputAxisController> inputs)
    {
        // Pause camera readers, not the shared InputAction asset used by UI.
        // Capture only readers we disabled so authored disabled states survive.
        foreach (CinemachineInputAxisController input in inputs)
        {
            if (input == null || !input.enabled) continue;
            pausedCameraInputs.Add(input);
            input.enabled = false;
        }
    }

    private void ResumeCameraInput()
    {
        foreach (CinemachineInputAxisController input in pausedCameraInputs)
            if (input != null) input.enabled = true;
        pausedCameraInputs.Clear();
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
        if (clock.TryNextDay())
        {
            panel.SetActive(false);
            ResumeCameraInput();
        }
        else
        {
            if (nextDayButton != null) nextDayButton.interactable = true;
            RefreshText();
        }
    }
}
