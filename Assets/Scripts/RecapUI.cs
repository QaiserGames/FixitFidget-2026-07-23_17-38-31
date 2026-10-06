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

    [Header("The recap as Ace's phone (playtest 2, step 2: claude/playtest-2-plan.md §9)")]
    [Tooltip("Show the recap as Ace's phone, built while playing (RecapPhone): Reviews, Franchise, Shop and Notes, " +
             "with Close up for the night under every app. Off: the three-column recap in the scene, as before.")]
    [SerializeField] private bool usePhone = true;

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

    // The recap phone takes over the panel and the button (the rest of this class works as before on
    // whichever is in use); the scene's three-column panel stays closed behind it.
    private RecapPhone phone;
    private GameObject scenePanel;
    private static RecapUI current;

    /// <summary>True while the end-of-day recap is on screen: the phone, or the three-column panel.</summary>
    public static bool Showing => current != null && current.panel != null && current.panel.activeInHierarchy;

    /// <summary>The recap phone, while Use Phone is on (null before Start, and with it off).</summary>
    public RecapPhone Phone => phone;

    /// <summary>The scene's three-column panel (it stays closed while the phone is in use).</summary>
    public GameObject ScenePanel => scenePanel != null ? scenePanel : panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    // The Night 1 slice: the recap's button leads into the night first (NightCycle), then
    // tomorrow. Once this recap's night has been walked it goes straight to tomorrow.
    private bool nightDone;
    private TMP_Text buttonLabel;
    private string openLabel = "";
    public const string NightLabel = "Close up for the night";
 
    private void Start()
    {
        current = this;
        scenePanel = panel;
        panel.SetActive(false);
        buttonLabel = nextDayButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null) openLabel = buttonLabel.text;
        if (usePhone)
        {
            phone = RecapPhone.Create();
            panel = phone.Root;
            nextDayButton = phone.CloseButton;
            buttonLabel = phone.CloseLabel;
            if (string.IsNullOrWhiteSpace(openLabel)) openLabel = "Open Tomorrow";
        }
        nextDayButton.onClick.AddListener(OnNextDay);
 
        if (DayClock.Instance != null)
            DayClock.Instance.OnDayEnded += DayEnded;

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
            DayClock.Instance.OnDayEnded -= DayEnded;
        if (saveManager != null) saveManager.SaveStatusChanged -= RefreshText;
        if (nextDayButton != null) nextDayButton.onClick.RemoveListener(OnNextDay);
        if (phone != null) Destroy(phone.gameObject);
        if (current == this) current = null;
    }
 
    // A new day's recap: its night hasn't been walked yet.
    private void DayEnded()
    {
        nightDone = false;
        if (phone != null) phone.NewEvening();   // each evening's phone opens on Reviews
        Show();
    }

    private void Show()
    {
        Sfx.Play2D("recap.open");
        if (buttonLabel != null)
            buttonLabel.text = !nightDone && NightCycle.FollowsTheDay ? NightLabel : openLabel;
        // Return camera-reader ownership before the recap takes its snapshot.
        if (player != null) player.GetComponent<CounterRepairView>()?.Close();
        SuspendCameraInput(FindObjectsByType<CinemachineInputAxisController>(
            FindObjectsInactive.Include));
        // Get the player out of any station so they aren't stuck behind the panel.
        if (player != null)
        {
            player.ExitStation();
            player.GetComponent<ItemInspector>()?.CancelInspection();
            player.GetComponent<ConversationController>()?.End();
            player.GetComponent<PlayerMovement>()?.ClearInput();
        }
        foreach (HoverTooltipUI tooltip in FindObjectsByType<HoverTooltipUI>(
            FindObjectsInactive.Exclude))
            tooltip.HideImmediately();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (phone != null)
        {
            // The phone builds its apps while it is up: TextMesh Pro measures only live text.
            if (nextDayButton != null) nextDayButton.interactable = true;
            panel.SetActive(true);
            RefreshText();
            return;
        }
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
        if (phone != null)
        {
            // The phone reads the day, the reviews, the notebook and the shop itself (and the save's error).
            if (c != null && c.DayOver) phone.Refresh();
            return;
        }
        if (c == null || !c.DayOver || text == null) return;

        text.text =
            $"{Weekdays.Label(c.Day).ToUpperInvariant()} — CLOSED\n\n" +
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

        // The Night 1 slice: the night comes first (NightCycle), then tomorrow.
        if (!nightDone && NightCycle.FollowsTheDay)
        {
            if (nextDayButton != null) nextDayButton.interactable = false;
            if (NightCycle.BeginAfterRecap(this))
            {
                nightDone = true;
                Sfx.Play2D("ui.confirm");
                panel.SetActive(false);
                ResumeCameraInput();
                return;
            }
            if (nextDayButton != null) nextDayButton.interactable = true;
        }
        OpenTomorrow(true);
    }

    /// <summary>
    /// Called by NightCycle once Ace is home: this recap's Open Tomorrow. False when the morning
    /// couldn't be saved: the recap comes back with the error, and its button goes straight to tomorrow.
    /// </summary>
    public bool ContinueAfterNight()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.DayOver) return false;
        nightDone = true;
        if (OpenTomorrow(false)) return true;
        Time.timeScale = 0f;   // the recap holds the world still, as at closing
        Show();
        return false;
    }

    /// <summary>
    /// Called by NightCycle when the morning couldn't open (an error, in the Console): this recap
    /// again, holding the world still, its button now going straight to tomorrow.
    /// </summary>
    public void ShowAgain()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.DayOver) return;
        nightDone = true;
        Time.timeScale = 0f;
        Show();
    }

    private bool OpenTomorrow(bool confirmSound)
    {
        DayClock clock = DayClock.Instance;
        if (nextDayButton != null) nextDayButton.interactable = false;
        if (clock.TryNextDay())
        {
            if (confirmSound) Sfx.Play2D("ui.confirm");
            panel.SetActive(false);
            ResumeCameraInput();
            return true;
        }
        if (nextDayButton != null) nextDayButton.interactable = true;
        RefreshText();
        return false;
    }
}
