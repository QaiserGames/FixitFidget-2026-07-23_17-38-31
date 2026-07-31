using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;
    private Vector2 moveInput;

    private void Awake()
    {
        // Runs once when the object wakes up. Grab a reference
        // to the CharacterController sitting on this same GameObject.
        controller = GetComponent<CharacterController>();
    }

    // Called automatically by the Player Input component whenever
    // the "Move" action fires (WASD, stick, d-pad — doesn't matter).
    // The name matters: "On" + the action's name.
    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        // Rotate the input 45 degrees to match the camera's angle,
        // so "up" on the stick means "up on the screen."
        move = Quaternion.Euler(0f, 45f, 0f) * move;

        controller.SimpleMove(move * moveSpeed);
    }
}