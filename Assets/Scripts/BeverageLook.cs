using UnityEngine;
using UnityEngine.InputSystem;

// Turn around world up so the pitched station view never banks when looking
// sideways. Mouse distance controls rotation independently of frame rate.
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
        if (!active) { if (wasActive) transform.localRotation = rest; wasActive = false; acceptingInput = false; angles = Vector2.zero; return; }
        if (!wasActive) { angles = Vector2.zero; transform.localRotation = rest; wasActive = true; acceptingInput = false; }
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
        delta += new Vector2(stick.x * stickSpeed.x, stick.y * stickSpeed.y) * dt;
        ApplyLookDelta(delta);
    }
    private void ApplyLookDelta(Vector2 delta)
    {
        angles.x = Mathf.Clamp(angles.x + delta.x, -52, 52);
        angles.y = Mathf.Clamp(angles.y - delta.y, -28, 34);
        Quaternion worldRest = transform.parent != null ? transform.parent.rotation * rest : rest;
        Vector3 forward = worldRest * Vector3.forward;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch + angles.y, -80, 80), yaw + angles.x, 0);
    }
}
