using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;
    private Vector2 moveInput;
    private ConversationController conversation;
    private CafeViewMode viewMode;

    private void Awake()
    {
        // Runs once when the object wakes up. Grab a reference
        // to the CharacterController sitting on this same GameObject.
        controller = GetComponent<CharacterController>();
        conversation = GetComponent<ConversationController>();
        viewMode = GetComponent<CafeViewMode>();
    }

    // Called automatically by the Player Input component whenever
    // the "Move" action fires (WASD, stick, d-pad — doesn't matter).
    // The name matters: "On" + the action's name.
    private void OnMove(InputValue value)
    {
        moveInput = DayClock.Instance != null && DayClock.Instance.DayOver
            ? Vector2.zero : value.Get<Vector2>();
    }

    private void OnDisable() => ClearInput();

    public void ClearInput() => moveInput = Vector2.zero;

    private void Update()
    {
        if (Time.timeScale <= 0 || (DayClock.Instance != null && DayClock.Instance.DayOver)
            || (conversation != null && conversation.InConversation))
        {
            ClearInput();
            return;
        }

        if (viewMode != null && viewMode.SuppressWalkingMovement) return;

        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        // Existing scenes keep their 45-degree controls. The optional walking
        // camera supplies its yaw so orbiting never reverses screen movement.
        float cameraYaw = viewMode != null && viewMode.isActiveAndEnabled ? viewMode.MovementYaw : 45;
        move = Quaternion.Euler(0f, cameraYaw, 0f) * move;

        controller.SimpleMove(move * moveSpeed);
    }
}
