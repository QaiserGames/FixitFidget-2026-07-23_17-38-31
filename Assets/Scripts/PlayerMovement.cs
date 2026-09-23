using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    // WHY FIRST-PERSON WALKING IS SMOOTHED
    //
    // The first-person camera is bolted to the capsule, so every change in the
    // capsule's velocity is a change in what the player sees. Keyboard movement
    // used to be instant: walking a square, you release W a few milliseconds
    // before pressing A, and for those frames the input is zero. Instant
    // movement turned that gap into a dead stop and then a full-speed jump —
    // a visible stutter at every corner, and a hard jolt on every reversal.
    //
    // So first-person walking eases the *input*, not the camera: the view stays
    // attached to the body (no lag, no swimming), and only speed and direction
    // change over about a tenth of a second. The easing happens in camera space,
    // so turning the mouse still turns your walking instantly.
    //
    // Isometric movement keeps its immediate response on purpose — the overhead
    // camera doesn't move with the body, so the same jolt isn't visible there.
    [Header("First-person walking feel")]
    [Tooltip("How quickly walking speeds up or changes direction, in metres per second squared. " +
             "50 reaches full speed in a tenth of a second. Higher is snappier, lower is floatier.")]
    [SerializeField, Min(1f)] private float firstPersonAcceleration = 50f;

    [Tooltip("How quickly walking slows once every movement key is released, in metres per second squared. " +
             "50 stops from full speed in a tenth of a second, sliding about a quarter of a metre. It also sets " +
             "how much speed survives the short gap between letting go of one key and pressing the next at a corner.")]
    [SerializeField, Min(1f)] private float firstPersonBraking = 50f;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 walkInput;
    private ConversationController conversation;
    private CafeViewMode viewMode;

    // The horizontal velocity handed to the CharacterController this frame.
    // Read by the walking-feel check to separate "what we asked for" from
    // "what the capsule actually did" (collisions, stalls, pops).
    public Vector3 CommandedVelocity { get; private set; }

    private void Awake()
    {
        // Runs once when the object wakes up. Grab a reference
        // to the CharacterController sitting on this same GameObject.
        controller = GetComponent<CharacterController>();
        conversation = GetComponent<ConversationController>();
        viewMode = GetComponent<CafeViewMode>();

        // MIN MOVE DISTANCE MUST BE ZERO.
        //
        // The controller silently ignores any Move() shorter than this. Unity's
        // default is 1 mm, which sounds harmless — but this script moves every
        // frame, and frames are short. At ~250 fps the gravity SimpleMove adds
        // while standing still is about 0.15 mm, so it was being thrown away:
        // the capsule stopped counting as grounded, then did a small catch-up
        // drop every few frames. Starting to walk during one of those
        // "ungrounded" frames lost the first ~20 ms of movement, and then the
        // view lurched to full speed. Measured on 23 Sept with
        // Fixit Fidget > Checks > Walking feel. Unity's own guidance is to
        // leave this at 0, so it is enforced here rather than trusted to every
        // scene's Inspector.
        if (controller != null) controller.minMoveDistance = 0f;
    }

    // Called automatically by the Player Input component whenever
    // the "Move" action fires (WASD, stick, d-pad — doesn't matter).
    // The name matters: "On" + the action's name.
    private void OnMove(InputValue value)
    {
        moveInput = DayClock.Instance != null && DayClock.Instance.DayOver
            ? Vector2.zero : value.Get<Vector2>();
    }

    // Stations disable this component. Coming back, walking starts from rest
    // rather than resuming whatever was held when you stepped up to the bench.
    private void OnDisable() => ClearInput();

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ClearInput();
    }

    public void ClearInput()
    {
        moveInput = Vector2.zero;
        walkInput = Vector2.zero;
        CommandedVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || (DayClock.Instance != null && DayClock.Instance.DayOver)
            || (conversation != null && conversation.InConversation))
        {
            ClearInput();
            return;
        }

        if (viewMode != null && viewMode.SuppressWalkingMovement)
        {
            // Esc released the cursor: stand still, and start from rest after.
            walkInput = Vector2.zero;
            CommandedVelocity = Vector3.zero;
            return;
        }

        // A stick can report slightly more than 1 on its diagonals.
        Vector2 target = Vector2.ClampMagnitude(moveInput, 1f);

        if (viewMode != null && viewMode.WalkingFirstPerson)
        {
            // Rates are authored in m/s² but applied to input (0–1), so divide
            // by top speed. MoveTowards keeps it frame-rate independent: the same
            // corner feels the same at 60 fps and at 240.
            bool held = target.sqrMagnitude > 0.0001f;
            float rate = (held ? firstPersonAcceleration : firstPersonBraking) / Mathf.Max(moveSpeed, 0.01f);
            walkInput = Vector2.MoveTowards(walkInput, target, rate * Time.deltaTime);
        }
        else
        {
            walkInput = target;
        }

        // Existing scenes keep their 45-degree controls. The optional walking
        // camera supplies its yaw so orbiting never reverses screen movement.
        float cameraYaw = viewMode != null && viewMode.isActiveAndEnabled ? viewMode.MovementYaw : 45;
        Vector3 move = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(walkInput.x, 0f, walkInput.y);

        CommandedVelocity = move * moveSpeed;
        controller.SimpleMove(CommandedVelocity);
    }
}
