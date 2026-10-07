using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Turn around world up so the pitched station view never banks when looking
// sideways. Mouse distance controls rotation independently of frame rate.
//
// The drinks close-up (from the overhead view; stations as reach, playtest 3) gets the pad's aim help: slower over a
// cup pad, a paddle, the cup stack or the tray, drawn toward its middle while turning, settled onto it when the stick
// lets go, and D-pad left / right step between them (AimAssist; PlayerInteractor asks for the steps). The mouse is
// untouched.
[DefaultExecutionOrder(-100)]
public sealed class BeverageLook : MonoBehaviour
{
    public StationInteractable station;
    public float sensitivity = .09f;
    [Tooltip("Controller look speed at full right-stick deflection, degrees per second (sideways, up/down).")]
    public Vector2 stickSpeed = new Vector2(95f, 65f);
    private PlayerInteractor player;
    private ConversationController dialogue;
    private ItemInspector inspector;
    private CounterRepairView counterRepair;
    private Unity.Cinemachine.CinemachineBrain brain;
    private Quaternion rest;
    private Vector2 angles;
    private bool wasActive;
    private bool acceptingInput;
    private readonly AimAssist.State aim = new AimAssist.State();
    private readonly List<AimAssist.Target> aimTargets = new List<AimAssist.Target>(16);

    /// <summary>The close-up's aim help as it goes (checks).</summary>
    public AimAssist.State Aim => aim;

    /// <summary>A D-pad step (PlayerInteractor): the view glides to look at <paramref name="point"/>.</summary>
    public void StepAimTo(Vector3 point) => aim.StepTo(point);

    private void Awake()
    {
        rest = transform.localRotation;
        player = FindAnyObjectByType<PlayerInteractor>();
        if (player != null)
        {
            dialogue = player.GetComponent<ConversationController>();
            inspector = player.GetComponent<ItemInspector>();
            counterRepair = player.GetComponent<CounterRepairView>();
        }
        brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
    }
    private void Update()
    {
        bool active = player != null && player.CurrentStation == station;
        if (!active) { if (wasActive) transform.localRotation = rest; wasActive = false; acceptingInput = false; angles = Vector2.zero; aim.Reset(); return; }
        if (!wasActive) { angles = Vector2.zero; transform.localRotation = rest; wasActive = true; acceptingInput = false; aim.Reset(); }
        if (counterRepair == null) counterRepair = player.GetComponent<CounterRepairView>();
        if (Time.timeScale <= 0 || Mouse.current == null && !PadInput.Connected || !Application.isFocused
            || Cursor.lockState != CursorLockMode.Locked
            || DayClock.Instance != null && DayClock.Instance.DayOver
            || inspector != null && inspector.IsHoldingItem || counterRepair != null && counterRepair.OwnsInput
            || dialogue != null && dialogue.InConversation || brain != null && brain.IsBlending)
        { acceptingInput = false; return; }
        // Cursor locking and focus changes can deliver a stale mouse delta.
        if (!acceptingInput) { acceptingInput = true; return; }
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() * sensitivity : Vector2.zero;
        // The right stick is a turn rate, so it is scaled by frame time.
        Vector2 stick = PadInput.Curved(PadInput.RightStick);
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
        Vector2 turn = new Vector2(stick.x * stickSpeed.x, stick.y * stickSpeed.y) * dt;
        if (!AimAssist.Active)
        {
            aim.Reset();
            ApplyLookDelta(delta + turn);
            return;
        }
        // The mouse first, untouched; then the stick with the help.
        ApplyLookDelta(delta);
        bool moving = stick != Vector2.zero;
        if (aim.WantsTargets(moving)) player.AimTargets(aimTargets);
        else aimTargets.Clear();
        Vector2 view = AimAssist.Steer(transform.position, View, new Vector2(turn.x, -turn.y), stick.magnitude, dt, aimTargets, aim);
        SetView(view);
    }
    private void ApplyLookDelta(Vector2 delta)
    {
        angles.x = Mathf.Clamp(angles.x + delta.x, -52, 52);
        angles.y = Mathf.Clamp(angles.y - delta.y, -28, 34);
        Apply();
    }

    // The rest's heading and tilt in the world (degrees; tilt positive looks down).
    private void RestAngles(out float yaw, out float pitch)
    {
        Quaternion worldRest = transform.parent != null ? transform.parent.rotation * rest : rest;
        Vector3 forward = worldRest * Vector3.forward;
        yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
    }

    private void Apply()
    {
        RestAngles(out float yaw, out float pitch);
        transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch + angles.y, -80, 80), yaw + angles.x, 0);
    }

    // The view as a heading and a tilt in the world, and back (within the look's limits).
    private Vector2 View
    {
        get
        {
            RestAngles(out float yaw, out float pitch);
            return new Vector2(yaw + angles.x, Mathf.Clamp(pitch + angles.y, -80, 80));
        }
    }

    private void SetView(Vector2 view)
    {
        RestAngles(out float yaw, out float pitch);
        angles.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, view.x), -52, 52);
        angles.y = Mathf.Clamp(view.y - pitch, -28, 34);
        Apply();
    }
}
