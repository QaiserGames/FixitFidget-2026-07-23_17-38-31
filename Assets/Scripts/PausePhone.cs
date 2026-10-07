using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE PHONE BY DAY: THE PAUSE (playtest 3, session 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §3.4)
//
// Esc, or the pad's Start (Options, Plus), when nothing else is open: Ace takes out the phone and the game holds still.
// Esc still steps back out of things first: a conversation, the drinks close-up, an item at the bench and the counter
// phone keep their own Esc and B (the phone won't come up over them), and at night Esc closes the notebook's page if
// it's open. The phone is the recap's phone in its pause mode (RecapPhone.Mode): by day Today, Notes and Settings; at
// night Notes and Settings. Esc, Start or B, or its bottom button ("Back to work"), put it away.
//
// Pausing really pauses: Time.timeScale goes to 0 (the sound player already holds the world's sounds and ducks the
// music under a pause), the UI's own clock stands still (UiClock: the night's notes, the Day 1 guide's hints, the juice
// and the HUD's corners hold), the cameras stop reading the mouse and stick, and the pointer is free. Putting it away
// gives everything back exactly as it was, at the end of that frame, so the key that closed it does nothing else.
//
// Not while the recap has the screen, during the night's fades and blinks, while a scene holds Ace (the bins' reveal),
// or while anything else has already paused the game. Quit (Settings, pressed twice) says plainly what isn't kept: the
// game saves at Open Tomorrow, by design. Made by ShopUI while playing; nothing in the scene changes.
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(-450)]
[DisallowMultipleComponent]
public sealed class PausePhone : MonoBehaviour
{
    public static PausePhone Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>True while the phone is up and the game is held still.</summary>
    public static bool Paused => Instance != null && Instance.open;

    /// <summary>The phone itself (its apps; checks).</summary>
    public RecapPhone Phone => phone;
    /// <summary>Times it came up this session (reports).</summary>
    public int Opened { get; private set; }

    RecapPhone phone;
    bool open, closeDue, quitDue;
    float scaleBefore = 1f;
    int busyFrame = -10;
    readonly List<CinemachineInputAxisController> heldInputs = new List<CinemachineInputAxisController>();
    PlayerInteractor player;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        phone = RecapPhone.Create(RecapPhone.Mode.Pause);
        phone.CloseButton.onClick.AddListener(() => closeDue = true);
        phone.QuitPressed += () => quitDue = true;
    }

    void OnDestroy()
    {
        if (open) Resume();
        if (phone != null) Destroy(phone.gameObject);
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ every frame

    void Update()
    {
        Keyboard keys = Keyboard.current;
        bool esc = keys != null && keys.escapeKey.wasPressedThisFrame;
        bool start = PadInput.Pressed(PadButton.Start);
        if (open)
        {
            // Esc, Start or B put it away (at the end of the frame: see Resume).
            if (esc || start || PadInput.Pressed(PadButton.East)) closeDue = true;
            KeepSelection();
            return;
        }
        if (!esc && !start) return;
        // Esc steps back out of things first: the night's notebook page closes before anything comes up.
        NightNotebook page = NightWalk.Instance != null && NightWalk.Instance.Active ? NightWalk.Instance.NotebookPage : null;
        if (esc && page != null && page.Open) { page.Show(false); return; }
        // Something had the screen this frame or the last (its own Esc may have just closed it): not this press.
        if (Busy() || busyFrame >= Time.frameCount - 1) return;
        Open();
    }

    void LateUpdate()
    {
        if (open)
        {
            if (quitDue) { quitDue = false; Quit(); return; }
            if (closeDue) { closeDue = false; Resume(); }
            return;
        }
        closeDue = quitDue = false;
        if (Busy()) busyFrame = Time.frameCount;
    }

    // Whether something else has the screen or the game: then the phone stays in Ace's pocket.
    bool Busy()
    {
        if (phone == null || phone.Root == null) return true;
        if (Time.timeScale <= 0f || RecapUI.Showing) return true;
        DayClock clock = DayClock.Instance;
        if (clock != null && clock.RecapOwnsInput) return true;
        if (ConversationController.AnyOpen || PlayerMovement.Held || NightCycle.Blinking) return true;
        NightCycle cycle = NightCycle.Instance;
        if (cycle != null && (cycle.Now == NightCycle.Phase.Dusk || cycle.Now == NightCycle.Phase.Dawn)) return true;
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        return player != null && player.InCloseUp;
    }

    // ------------------------------------------------------------------ up and away

    /// <summary>Takes the phone out and holds the game still (also for checks). False when something else has the screen.</summary>
    public bool Open()
    {
        if (open || Busy()) return false;
        bool night = NightWalk.Instance != null && NightWalk.Instance.Active;
        scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        UiClock.Hold();
        HoldCameras();
        PlayerMovement walk = player != null ? player.GetComponent<PlayerMovement>() : FindAnyObjectByType<PlayerMovement>();
        if (walk != null) walk.ClearInput();
        foreach (HoverTooltipUI tooltip in FindObjectsByType<HoverTooltipUI>(FindObjectsInactive.Exclude)) tooltip.HideImmediately();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !PadInput.UsingPad;
        phone.OpenPause(night);
        open = true;
        Opened++;
        Sfx.Play2D("phone.open");
        if (PadInput.UsingPad) Select(phone.CloseButton);
        return true;
    }

    /// <summary>Puts the phone away and gives the game back exactly as it was (also for checks).</summary>
    public void Resume()
    {
        if (!open) return;
        open = false;
        phone.ClosePause();
        Time.timeScale = scaleBefore > 0f ? scaleBefore : 1f;
        UiClock.Release();
        ReleaseCameras();
        GameSettings.Save();
        EventSystem events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(phone.transform))
            events.SetSelectedGameObject(null);
        busyFrame = Time.frameCount;   // the closing key mustn't open it again
        Sfx.Play2D("phone.close");
    }

    void Quit()
    {
        Resume();
        GameSettings.Save();
        Debug.Log("[Pause phone] Quit from the phone: the day so far isn't kept (the game saves at Open Tomorrow).");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // The cameras stop reading the mouse and the stick while it's up (only the ones on, so authored states survive).
    void HoldCameras()
    {
        heldInputs.Clear();
        foreach (CinemachineInputAxisController input in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include))
        {
            if (input == null || !input.enabled) continue;
            heldInputs.Add(input);
            input.enabled = false;
        }
    }

    void ReleaseCameras()
    {
        foreach (CinemachineInputAxisController input in heldInputs) if (input != null) input.enabled = true;
        heldInputs.Clear();
    }

    // A pad always has something selected on the phone (the D-pad moves from it; A presses it).
    void KeepSelection()
    {
        if (!PadInput.UsingPad) return;
        EventSystem events = EventSystem.current;
        if (events == null) return;
        GameObject selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(phone.Root.transform)
            && selected.TryGetComponent(out Selectable current) && current.IsInteractable()) return;
        Select(phone.FirstSelectable);
    }

    static void Select(Selectable target)
    {
        EventSystem events = EventSystem.current;
        if (events != null && target != null && target.gameObject.activeInHierarchy) events.SetSelectedGameObject(target.gameObject);
    }

    public string Describe() =>
        $"Pause phone: {(open ? "up" : "away")}, came up {Opened} time(s); {(phone != null ? phone.Describe() : "not built")} {GameSettings.Describe()}";
}
