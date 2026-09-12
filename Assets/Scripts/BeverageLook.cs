using UnityEngine;
using UnityEngine.InputSystem;

// Turn around world up so the pitched station view never banks when looking
// sideways. Mouse distance controls rotation independently of frame rate.
[DefaultExecutionOrder(-100)]
public sealed class BeverageLook : MonoBehaviour
{
    public StationInteractable station;
    public float sensitivity = .09f;
    private PlayerInteractor player;
    private Quaternion rest;
    private Vector2 angles;
    private bool wasActive;
    private bool acceptingInput;
    private void Awake() { rest = transform.localRotation; player = FindAnyObjectByType<PlayerInteractor>(); }
    private void Update()
    {
        bool active = player != null && player.CurrentStation == station;
        if (!active) { if (wasActive) transform.localRotation = rest; wasActive = false; acceptingInput = false; angles = Vector2.zero; return; }
        if (!wasActive) { angles = Vector2.zero; transform.localRotation = rest; wasActive = true; acceptingInput = false; }
        var dialogue = player.GetComponent<ConversationController>();
        var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
        if (Time.timeScale <= 0 || Mouse.current == null || !Application.isFocused
            || DayClock.Instance != null && DayClock.Instance.DayOver
            || dialogue != null && dialogue.InConversation || brain != null && brain.IsBlending)
        { acceptingInput = false; return; }
        // Cursor locking and focus changes can deliver a stale mouse delta.
        if (!acceptingInput) { acceptingInput = true; return; }
        ApplyLookDelta(Mouse.current.delta.ReadValue());
    }
    private void ApplyLookDelta(Vector2 mouseDelta)
    {
        Vector2 delta = mouseDelta * sensitivity;
        angles.x = Mathf.Clamp(angles.x + delta.x, -52, 52);
        angles.y = Mathf.Clamp(angles.y - delta.y, -28, 34);
        Quaternion worldRest = transform.parent != null ? transform.parent.rotation * rest : rest;
        Vector3 forward = worldRest * Vector3.forward;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch + angles.y, -80, 80), yaw + angles.x, 0);
    }
}
