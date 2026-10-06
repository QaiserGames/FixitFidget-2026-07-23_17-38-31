using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 4: ACE'S POCKET TORCH (claude/night-city-proposal.md §9)
//
// F (X / Square on a pad: the day's station button, free at night because the café is closed)
// switches a small torch on and off. From above it points the way Ace last walked, held at hand
// height and angled down, so a pool of light runs ahead on the pavement; in first person it
// points where Ace looks. It is off when the night begins, and it goes when the night ends.
//
// Only while a night walk runs: NightWalk adds it and calls Begin/End.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightTorch : MonoBehaviour
{
    [Tooltip("How far the beam reaches (metres).")]
    public float range = 12f;
    [Tooltip("How wide the beam is (degrees).")]
    [Range(20f, 90f)] public float angle = 50f;
    public float intensity = 16f;
    public Color colour = new Color(1f, .93f, .8f);
    [Tooltip("Degrees below level the beam points when seen from above.")]
    [Range(0f, 45f)] public float downward = 19f;

    Light beam;
    Transform ace;
    CafeViewMode view;
    PlayerMovement movement;
    Vector3 heading = Vector3.forward;

    public bool On => beam != null && beam.enabled;
    /// <summary>Times it was switched on (for reports).</summary>
    public int SwitchedOn { get; private set; }

    public void Begin()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        ace = movement != null ? movement.transform : null;
        view = ace != null ? ace.GetComponent<CafeViewMode>() : null;
        if (beam == null)
        {
            var go = new GameObject("Ace's torch (while the night runs)");
            go.transform.SetParent(transform, false);
            beam = go.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.shadows = LightShadows.None;
            beam.renderMode = LightRenderMode.ForcePixel;
        }
        beam.range = range;
        beam.spotAngle = angle;
        beam.innerSpotAngle = angle * .55f;
        beam.intensity = intensity;
        beam.color = colour;
        beam.enabled = false;
        // To start with it points away from the camera, "up" the screen.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) heading = f.normalized;
        }
    }

    public void End()
    {
        if (beam != null) Destroy(beam.gameObject);
        beam = null;
    }

    public void Switch(bool on)
    {
        if (beam == null) return;
        if (on && !beam.enabled) SwitchedOn++;
        beam.enabled = on;
        if (on) Aim(1f);
    }

    void Update()
    {
        // A scene holding Ace still owns the buttons (on a pad, X / Square picks Ace's first reply: Barks).
        if (beam == null || ace == null || Time.timeScale <= 0f || PlayerMovement.Held) return;
        bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame || PadInput.Pressed(PadButton.West);
        if (pressed) Switch(!beam.enabled);
    }

    void LateUpdate()
    {
        if (beam == null || ace == null) return;
        Aim(Time.deltaTime);
    }

    void Aim(float dt)
    {
        bool firstPerson = view != null && view.FirstPersonSelected && !view.OverheadShown;
        Camera cam = Camera.main;
        if (firstPerson && cam != null)
        {
            Transform eye = cam.transform;
            beam.transform.SetPositionAndRotation(eye.position + eye.right * .18f - Vector3.up * .25f, eye.rotation);
            Vector3 f = eye.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) heading = f.normalized;
            return;
        }
        // From above: the way Ace last walked, turning smoothly.
        Vector3 v = movement != null ? movement.CommandedVelocity : Vector3.zero;
        v.y = 0f;
        if (v.sqrMagnitude > .04f)
            heading = Vector3.Slerp(heading, v.normalized, Mathf.Clamp01(dt * 10f)).normalized;
        Vector3 feet = view != null ? view.AceFeet : ace.position - Vector3.up;
        Vector3 down = Quaternion.AngleAxis(downward, Vector3.Cross(Vector3.up, heading)) * heading;
        beam.transform.SetPositionAndRotation(feet + Vector3.up * 1.25f + heading * .3f, Quaternion.LookRotation(down, Vector3.up));
    }

    void OnDestroy() => End();

    public string Describe() => beam == null ? "Torch: not set up." :
        $"Torch: {(beam.enabled ? "on" : "off")}, switched on {SwitchedOn} times; beam {range:0} m, {angle:0}°, pointing " +
        $"({beam.transform.forward.x:0.00}, {beam.transform.forward.y:0.00}, {beam.transform.forward.z:0.00}).";
}
