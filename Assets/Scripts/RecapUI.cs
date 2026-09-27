using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Reputation (optional: Fixit Fidget > Reputation > Add stars and reviews to the recap)")]
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private Image[] reputationStars;
    [Tooltip("Sprites for earned stars and for the empty places (an outline, so a " +
             "café with no stars never reads as five). Leave either empty to keep the images' own sprite.")]
    [SerializeField] private Sprite starEarnedSprite;
    [SerializeField] private Sprite starEmptySprite;
    [SerializeField] private Color starEarnedColor = new Color(1f, 0.78f, 0.34f, 1f);
    [SerializeField] private Color starEmptyColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Notebook (optional: Fixit Fidget > Night > Add the notebook to the recap)")]
    [SerializeField] private TMP_Text notebookText;

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
        // Return camera-reader ownership before the recap takes its snapshot.
        if (player != null) player.GetComponent<CounterRepairView>()?.Close();
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

    // Controller: keep one of the recap's buttons selected while it is open, so
    // the D-pad / left stick move between Next Day and the upgrades and A
    // presses. A mouse player never gets a selection they didn't make.
    private void Update()
    {
        if (panel == null || !panel.activeInHierarchy || !PadInput.UsingPad) return;
        EventSystem events = EventSystem.current;
        if (events == null) return;
        GameObject selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(panel.transform)
            && selected.TryGetComponent(out Selectable current) && current.IsInteractable()) return;
        Selectable target = nextDayButton != null && nextDayButton.gameObject.activeInHierarchy
            && nextDayButton.IsInteractable() ? nextDayButton : null;
        if (target == null)
            foreach (Selectable candidate in panel.GetComponentsInChildren<Selectable>())
                if (candidate.IsInteractable()) { target = candidate; break; }
        if (target != null) events.SetSelectedGameObject(target.gameObject);
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

        RefreshReputation();
        RefreshNotebook();
    }

    // What Ace noted today (claude/night-notebook-spec.md). Hidden on a day
    // nothing was learned, and in scenes without the notebook block.
    private void RefreshNotebook()
    {
        if (notebookText == null) return;
        Notebook notebook = saveManager != null ? saveManager.Notebook : null;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        string block = notebook != null ? NotebookRecap.Build(notebook, day) : "";
        notebookText.gameObject.SetActive(!string.IsNullOrEmpty(block));
        notebookText.text = block;
    }

    // Stars and today's reviews (claude/reputation-spec.md). Scenes without
    // the reputation block simply show the recap as before.
    private void RefreshReputation()
    {
        ReputationLedger rep = saveManager != null ? saveManager.Reputation : null;
        if (reputationStars != null)
            for (int i = 0; i < reputationStars.Length; i++)
            {
                Image star = reputationStars[i];
                if (star == null) continue;
                star.gameObject.SetActive(rep != null);
                if (rep == null) continue;
                bool earned = i < rep.StarsEarned;
                star.color = earned ? starEarnedColor : starEmptyColor;
                Sprite sprite = earned ? starEarnedSprite : starEmptySprite;
                if (sprite != null) star.sprite = sprite;
            }
        if (reputationText == null) return;
        reputationText.gameObject.SetActive(rep != null);
        reputationText.text = rep != null ? ReputationRecap.Build(rep) : "";
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
