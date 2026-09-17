using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class StationInteractable : Interactable
{
    [SerializeField] private CinemachineCamera stationCamera;
    [SerializeField] private string label = "Work here";
    [SerializeField] private DropSpot dropSpot;
    [Tooltip("Where the player stands while using this station. Keeps the view consistent.")]
    [SerializeField] private Transform standPoint;
    [SerializeField, Range(.02f, .3f)] private float mouseLookSensitivity = .09f;
    [SerializeField, Min(1f)] private float stickLookSpeed = 90f;

    private CinemachineInputAxisController lookInput;
    private CinemachineBrain brain;
    private PlayerInteractor player;
    private ItemInspector inspector;
    private ConversationController conversation;
    private CounterRepairView counterRepair;
    private bool lookReady;

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        if (player != null)
        {
            inspector = player.GetComponent<ItemInspector>();
            conversation = player.GetComponent<ConversationController>();
            counterRepair = player.GetComponent<CounterRepairView>();
        }
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        ConfigureLookInput();
        if (lookInput != null) lookInput.enabled = false;
    }

    // Also used by the scene setup to make the mouse defaults visible in the
    // inspector. Mouse distance is already accumulated over a frame; scaling
    // it by frame time again made slow frames turn farther than fast frames.
    public void ConfigureLookInput()
    {
        lookInput = stationCamera != null ? stationCamera.GetComponent<CinemachineInputAxisController>() : null;
        if (lookInput == null) return;
        lookInput.SuppressInputWhileBlending = true;
        lookInput.IgnoreTimeScale = false;
        foreach (var control in lookInput.Controllers)
        {
            if (!(control.Owner is CinemachinePanTilt)) continue;
            control.Input.Gain = control.Name == "Look Y (Tilt)" ? -mouseLookSensitivity : mouseLookSensitivity;
            control.Input.CancelDeltaTime = true;
            control.Driver.AccelTime = control.Driver.DecelTime = 0;
        }
    }

    private void Update()
    {
        if (lookInput == null) return; // BeverageLook owns the dispenser view.
        // CounterRepairView can be added by PlayerInteractor after our Awake.
        if (counterRepair == null && player != null) counterRepair = player.GetComponent<CounterRepairView>();
        bool ownsLook = player != null && player.CurrentStation == this && Application.isFocused
            && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked
            && !(DayClock.Instance != null && DayClock.Instance.DayOver)
            && !(inspector != null && inspector.IsHoldingItem)
            && !(conversation != null && conversation.InConversation)
            && !(counterRepair != null && counterRepair.OwnsInput)
            && !(brain != null && brain.IsBlending);
        // Skip the first frame after locking/focus/blending so a stale mouse
        // delta cannot jerk the view. No smoothing or delayed mouse response.
        lookInput.enabled = ownsLook && lookReady;
        lookReady = ownsLook;
        if (!lookInput.enabled) return;
        foreach (var control in lookInput.Controllers)
        {
            if (!(control.Owner is CinemachinePanTilt)) continue;
            var action = control.Input.InputAction != null ? control.Input.InputAction.action : null;
            bool pointer = action == null || action.activeControl == null || action.activeControl.device is Pointer;
            float gain = pointer ? mouseLookSensitivity : stickLookSpeed;
            control.Input.Gain = control.Name == "Look Y (Tilt)" ? -gain : gain;
            // Stick deflection is a rate, unlike a mouse's travelled distance.
            control.Input.CancelDeltaTime = pointer;
        }
    }

    private void OnDisable()
    {
        if (lookInput != null) lookInput.enabled = false;
        lookReady = false;
    }

    public Transform StandPoint => standPoint;

    [Tooltip("Can items be picked up and repaired at this station? Bench yes, counter no.")]
    [SerializeField] private bool isWorkSurface = false;

    public string StationLabel => label;
    public bool IsWorkSurface => isWorkSurface;
    // When carrying another device, an item already on this bench must not
    // steal the bench's set-down action. Customer actions keep their own priority.
    public bool PrefersPlacementOver(Interactable candidate, PlayerCarry carry)
    {
        if (!isWorkSurface || dropSpot == null || carry == null || !carry.IsCarrying
            || !dropSpot.CanAccept(carry.Carried) || !(candidate is ItemInteractable)) return false;
        var placed = candidate.GetComponentInParent<JobBase>();
        return placed != null && dropSpot.Holds(placed);
    }
    public void ConfigureBeverageView(CinemachineCamera camera, Transform stand)
    {
        stationCamera = camera; standPoint = stand; label = "Prepare drinks";
        isWorkSurface = false; dropSpot = null;
    }

    // Only offered to Interact when we're carrying something to put down.
    public override bool IsAvailable
    {
        get
        {
            PlayerCarry c = FindAnyObjectByType<PlayerCarry>();
            return c != null && c.IsCarrying && dropSpot != null;
        }
    }

    public override string Prompt
    {
        get
        {
            PlayerCarry c = FindAnyObjectByType<PlayerCarry>();
            if (c == null || !c.IsCarrying || dropSpot == null) return "";
            if (!dropSpot.CanAccept(c.Carried)) return "No room here";
            return dropSpot.Kind == DropSpot.SpotKind.Counter ? "Return item" : "Set down";
        }
    }

    // Dropping just drops. The player presses F when THEY decide to start working —
    // we don't guess, because "still ferrying" and "ready to work" are both valid.
    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry c = player.GetComponent<PlayerCarry>();
        if (c == null || !c.IsCarrying || dropSpot == null) return;

        Transform point = dropSpot.ResolvePoint(c.Carried);
        if (point == null) return;      // full — the prompt already said so

        c.PlaceAt(point);
    }

    public void ActivateCamera(bool on)
    {
        stationCamera.Priority = on ? 20 : 0;
    }

    // Always arrive facing forward, never at whatever angle we left it.
    public void ResetView()
    {
        if (stationCamera == null) return;

        var panTilt = stationCamera.GetComponent<CinemachinePanTilt>();
        if (panTilt == null) return;

        panTilt.PanAxis.Value = panTilt.PanAxis.Center;
        panTilt.TiltAxis.Value = panTilt.TiltAxis.Center;
    }
}
