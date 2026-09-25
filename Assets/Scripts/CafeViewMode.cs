using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

// One optional walking-camera owner. Station, inspection and conversation
// cameras keep their existing higher priorities and their own look controls.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CafeViewMode : MonoBehaviour
{
    public CinemachineCamera isometricCamera;
    public CinemachineCamera firstPersonCamera;
    public Vector3 isometricFocus = new Vector3(0, .6f, 9);
    public Renderer bodyRenderer;
    [Tooltip("Only these authored wall pieces can disappear in an overhead view.")]
    public Renderer[] cutawayWalls = System.Array.Empty<Renderer>();
    [Tooltip("Hanging fixture meshes hidden in the overhead view. Assign renderers, not lights.")]
    public Renderer[] overheadFixtures = System.Array.Empty<Renderer>();
    [Range(.02f, .3f)] public float lookSensitivity = .09f;
    [Range(1.3f, 1.9f)] public float eyeHeight = 1.65f;
    [Range(20, 60)] public float minimumDistance = 24;
    [Range(25, 80)] public float maximumDistance = 48;

    [Header("Controller")]
    [Tooltip("First-person turn speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(60, 360)] private float padLookYawSpeed = 170f;
    [Tooltip("First-person look up/down speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(40, 240)] private float padLookPitchSpeed = 110f;
    [SerializeField] private bool invertPadLookY;
    [Tooltip("Overhead orbit speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(30, 240)] private float padOrbitSpeed = 110f;
    [Tooltip("Overhead tilt speed at full right-stick deflection, degrees per second.")]
    [SerializeField, Range(10, 120)] private float padTiltSpeed = 45f;
    [Tooltip("Overhead zoom speed on the triggers, metres per second.")]
    [SerializeField, Range(4, 60)] private float padZoomSpeed = 22f;

    PlayerInteractor interactor;
    ConversationController conversation;
    ItemInspector inspector;
    CounterRepairView counterRepair;
    CharacterController capsule;
    CinemachineBrain brain;
    bool firstPerson, pointerReleased, acceptingLook, bodyWasVisible;
    bool[] wallVisibility, fixtureVisibility;
    float yaw, pitch = 8, isoYaw = 45, isoPitch = 50, isoDistance = 34;
    float homeIsoYaw = 45, homeIsoPitch = 50, homeIsoDistance = 34;
    int resumedAtFrame = -1;

    public bool FirstPersonSelected => firstPerson;
    public bool WalkingFirstPerson => isActiveAndEnabled && firstPerson && !AtStation && !OverlayOwnsInput;
    public bool PointerReleased => pointerReleased;
    public bool CanChangeView => isActiveAndEnabled && !AtStation && !OverlayOwnsInput;
    public string ControlsHint => !CanChangeView ? "" : PadInput.UsingPad
        ? firstPerson ? $"{ControlHints.View}  Isometric"
            : $"{ControlHints.View}  First person    Right stick  Orbit    {ControlHints.Zoom}  Zoom"
        : firstPerson
            ? pointerReleased ? "Click to look around    V  Isometric" : "V  Isometric    Esc  Free cursor"
            : "V  First person    Middle-drag  Orbit    Scroll  Zoom";
    public bool SuppressWalkingInteraction => WalkingFirstPerson
        && (pointerReleased || Time.frameCount <= resumedAtFrame || brain != null && brain.IsBlending);
    public bool SuppressWalkingMovement => WalkingFirstPerson && pointerReleased;
    public float MovementYaw => WalkingFirstPerson ? yaw : isoYaw;
    bool AtStation => interactor != null && interactor.IsAtStation;
    bool OverlayOwnsInput => Time.timeScale <= 0 || DayClock.Instance != null && DayClock.Instance.DayOver
        || conversation != null && conversation.InConversation || inspector != null && inspector.IsHoldingItem
        || counterRepair != null && counterRepair.OwnsInput;
    // Pausing or opening the end-of-day recap does not change the camera's
    // presentation. Keep the cutaway consistent until a close-up owns it.
    bool OverheadPresentation => !firstPerson && !AtStation
        && !(conversation != null && conversation.InConversation)
        && !(inspector != null && inspector.IsHoldingItem)
        && !(counterRepair != null && counterRepair.OwnsInput);

    void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        conversation = GetComponent<ConversationController>();
        inspector = GetComponent<ItemInspector>();
        counterRepair = GetComponent<CounterRepairView>();
        capsule = GetComponent<CharacterController>();
        var main = Camera.main;
        brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
        bodyWasVisible = bodyRenderer != null && bodyRenderer.enabled;
        wallVisibility = new bool[cutawayWalls.Length];
        for (int i = 0; i < cutawayWalls.Length; i++)
            wallVisibility[i] = cutawayWalls[i] != null && cutawayWalls[i].enabled;
        fixtureVisibility = new bool[overheadFixtures.Length];
        for (int i = 0; i < overheadFixtures.Length; i++)
            fixtureVisibility[i] = overheadFixtures[i] != null && overheadFixtures[i].enabled;
        if (isometricCamera != null)
        {
            isoYaw = isometricCamera.transform.eulerAngles.y;
            isoPitch = Mathf.DeltaAngle(0, isometricCamera.transform.eulerAngles.x);
            isoDistance = Mathf.Clamp(Vector3.Distance(isometricCamera.transform.position, isometricFocus), minimumDistance, maximumDistance);
        }
        // Where the controller's R3 returns the overhead view to.
        homeIsoYaw = isoYaw; homeIsoPitch = isoPitch; homeIsoDistance = isoDistance;
        // Let the authored walking camera face the cafe on first entry.
        yaw = firstPersonCamera != null ? firstPersonCamera.transform.eulerAngles.y : isoYaw;
        if (firstPersonCamera != null) firstPersonCamera.Priority = 0;
    }

    void Update()
    {
        if (counterRepair == null) counterRepair = GetComponent<CounterRepairView>();
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (!Application.isFocused || !CanChangeView) { acceptingLook = false; return; }
        // V, or the controller's View / Share / Minus button.
        if (keyboard != null && keyboard.vKey.wasPressedThisFrame || PadInput.Pressed(PadButton.Select))
        {
            SetFirstPerson(!firstPerson);
            return;
        }
        // A hitch must not turn one held stick into a huge jump.
        float padDelta = Mathf.Min(Time.unscaledDeltaTime, .1f);
        if (firstPerson)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            { pointerReleased = true; acceptingLook = false; RefreshCursor(); return; }
            if (pointerReleased)
            {
                bool clickResume = mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI();
                // A controller never needs a free cursor: touching a stick resumes looking.
                bool padResume = PadInput.UsingPad
                    && (PadInput.RightStick != Vector2.zero || PadInput.LeftStick != Vector2.zero);
                if (clickResume || padResume)
                { pointerReleased = false; resumedAtFrame = Time.frameCount; RefreshCursor(); }
                acceptingLook = false;
                return;
            }
            if (brain != null && brain.IsBlending)
            { acceptingLook = false; return; }
            // Ignore the first delta after a blend, cursor lock or focus change.
            if (!acceptingLook) { acceptingLook = true; return; }
            Vector2 delta = mouse != null ? mouse.delta.ReadValue() * lookSensitivity : Vector2.zero;
            // The stick is a turn RATE (degrees per second), unlike the mouse's
            // travelled distance, so it is scaled by frame time.
            Vector2 stick = PadInput.Curved(PadInput.RightStick);
            if (stick != Vector2.zero)
            {
                delta.x += stick.x * padLookYawSpeed * padDelta;
                delta.y += stick.y * padLookPitchSpeed * padDelta * (invertPadLookY ? -1f : 1f);
            }
            yaw = Mathf.Repeat(yaw + delta.x, 360);
            pitch = Mathf.Clamp(pitch - delta.y, -75, 75);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        else
        {
            if (mouse != null && !PointerOverUI())
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    isoYaw = Mathf.Repeat(isoYaw + delta.x * .18f, 360);
                    isoPitch = Mathf.Clamp(isoPitch + delta.y * .12f, 38, 68);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                    isoDistance = Mathf.Clamp(isoDistance - Mathf.Clamp(scroll / 120f, -3, 3) * 1.6f, minimumDistance, maximumDistance);
            }
            // Controller: right stick orbits and tilts, triggers zoom (RT in,
            // LT out), R3 returns to the authored overhead angle.
            Vector2 orbit = PadInput.Curved(PadInput.RightStick, 1.4f);
            if (orbit != Vector2.zero)
            {
                isoYaw = Mathf.Repeat(isoYaw + orbit.x * padOrbitSpeed * padDelta, 360);
                isoPitch = Mathf.Clamp(isoPitch - orbit.y * padTiltSpeed * padDelta, 38, 68);
            }
            float zoom = PadInput.RightTrigger - PadInput.LeftTrigger;
            if (Mathf.Abs(zoom) > .01f)
                isoDistance = Mathf.Clamp(isoDistance - zoom * padZoomSpeed * padDelta, minimumDistance, maximumDistance);
            if (PadInput.Pressed(PadButton.RightStickPress))
            {
                isoYaw = homeIsoYaw;
                isoPitch = homeIsoPitch;
                isoDistance = homeIsoDistance;
            }
        }
    }

    // Public so the existing scene recipe and play-mode validation can use the
    // same transition as V; this never forces an exit from a repair or dialog.
    public bool SetFirstPerson(bool enabled)
    {
        if (!CanChangeView || enabled && firstPersonCamera == null || !enabled && isometricCamera == null) return false;
        firstPerson = enabled;
        pointerReleased = false;
        acceptingLook = false;
        resumedAtFrame = Time.frameCount;
        if (firstPerson) transform.rotation = Quaternion.Euler(0, yaw, 0);
        RefreshCameraPose();
        RefreshCursor();
        return true;
    }

    void LateUpdate()
    {
        RefreshCameraPose();
        RefreshCursor();
        if (bodyRenderer != null)
            bodyRenderer.enabled = bodyWasVisible && !AtStation && !firstPerson;
        RefreshCutawayWalls();
        RefreshOverheadFixtures();
    }

    void RefreshCameraPose()
    {
        if (firstPersonCamera != null)
        {
            // Player origin is at the capsule centre, not its feet.
            float floorOffset = capsule != null ? capsule.center.y - capsule.height * .5f : -1;
            Vector3 eye = transform.position + Vector3.up * (floorOffset + eyeHeight);
            firstPersonCamera.transform.SetPositionAndRotation(eye, Quaternion.Euler(pitch, yaw, 0));
            firstPersonCamera.Priority = firstPerson ? 15 : 0;
        }
        if (isometricCamera != null)
        {
            Quaternion angle = Quaternion.Euler(isoPitch, isoYaw, 0);
            isometricCamera.transform.SetPositionAndRotation(isometricFocus - angle * Vector3.forward * isoDistance, angle);
        }
    }

    void RefreshCursor()
    {
        bool locked = Application.isFocused && !OverlayOwnsInput && (AtStation || firstPerson && !pointerReleased);
        CursorLockMode mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != mode) { Cursor.lockState = mode; acceptingLook = false; }
        // With a controller in hand the mouse arrow is only clutter; views that
        // need a pointer draw the controller's own cursor (PadCursor).
        Cursor.visible = !locked && !PadInput.UsingPad;
    }

    void RefreshCutawayWalls()
    {
        if (wallVisibility == null || isometricCamera == null) return;
        Vector3 origin = isometricCamera.transform.position;
        bool overhead = OverheadPresentation;
        for (int i = 0; i < Mathf.Min(cutawayWalls.Length, wallVisibility.Length); i++)
        {
            var wall = cutawayWalls[i];
            if (wall == null) continue;
            Bounds bounds = wall.bounds;
            bool obscures = overhead && BlocksInterior(bounds, origin);
            wall.enabled = wallVisibility[i] && !obscures;
        }
    }

    bool BlocksInterior(Bounds wall, Vector3 origin)
    {
        // A camera being outside a wall does not mean that wall obscures the
        // room: the view may clear its top or its end. Test finite sightlines
        // to the room centre and three points just inside this wall instead.
        // The edge probes keep occupied perimeter seats readable when orbiting.
        Vector3 target = wall.center;
        target.y = isometricFocus.y + .55f;
        bool normalX = wall.size.x < wall.size.z;
        float inward = normalX ? isometricFocus.x - wall.center.x : isometricFocus.z - wall.center.z;
        float direction = inward < 0 ? -1 : 1;
        Vector3 along;
        if (normalX)
        {
            target.x += direction * (wall.extents.x + 1.25f);
            along = Vector3.forward * Mathf.Max(0, wall.extents.z - 1.25f);
        }
        else
        {
            target.z += direction * (wall.extents.z + 1.25f);
            along = Vector3.right * Mathf.Max(0, wall.extents.x - 1.25f);
        }
        return BlocksSightline(wall, origin, isometricFocus)
            || BlocksSightline(wall, origin, target)
            || BlocksSightline(wall, origin, target - along)
            || BlocksSightline(wall, origin, target + along);
    }

    static bool BlocksSightline(Bounds wall, Vector3 origin, Vector3 target)
    {
        Vector3 segment = target - origin;
        float length = segment.magnitude;
        return length > .01f && wall.IntersectRay(new Ray(origin, segment / length), out float hitDistance)
            && hitDistance < length - .02f;
    }

    void RefreshOverheadFixtures()
    {
        if (fixtureVisibility == null) return;
        bool overhead = OverheadPresentation;
        for (int i = 0; i < Mathf.Min(overheadFixtures.Length, fixtureVisibility.Length); i++)
            if (overheadFixtures[i] != null)
                overheadFixtures[i].enabled = fixtureVisibility[i] && !overhead;
    }

    static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    void OnApplicationFocus(bool focused) { acceptingLook = false; if (!focused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    void OnDisable()
    {
        if (firstPersonCamera != null) firstPersonCamera.Priority = 0;
        if (bodyRenderer != null) bodyRenderer.enabled = bodyWasVisible;
        if (wallVisibility != null)
            for (int i = 0; i < Mathf.Min(cutawayWalls.Length, wallVisibility.Length); i++)
                if (cutawayWalls[i] != null) cutawayWalls[i].enabled = wallVisibility[i];
        if (fixtureVisibility != null)
            for (int i = 0; i < Mathf.Min(overheadFixtures.Length, fixtureVisibility.Length); i++)
                if (overheadFixtures[i] != null) overheadFixtures[i].enabled = fixtureVisibility[i];
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
