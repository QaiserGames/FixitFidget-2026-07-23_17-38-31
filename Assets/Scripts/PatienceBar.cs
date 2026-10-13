using UnityEngine;

// ---------------------------------------------------------------------------
// A CUSTOMER'S PATIENCE BAR
//
// CustomerBrain shows and hides this object exactly as before (hidden on arrival, shown while they wait, never inside a
// conversation: Bible §2), and tells it how much patience is left. The night's scripts (GraceAtHome, Lodger,
// NightNeighbours) and CafeArrivals switch it off the same way, and NpcSeating still finds it by its name.
//
// PLAYTEST 4 (10 Oct 2026; the first-hour playtester's #3): the bar used to be this object's own 0.6 x 0.08 m strip in the
// world, 23-26 x 3 px from the default camera ("a red dot by the end"). While playing it is now drawn on the pins' screen
// over the person's head (UiPins), the same size at any zoom and in first person (UiPins.BarWidth x BarHeight at 1080p: 72 x 9 at BarScale 1.3, 13 Oct 2026), coloured the way the
// ticket's meter is, with the job's colour beside it once their job is taken. The strip in the world is put away while
// playing; in Edit Mode, or in a scene with nothing to draw the pins, it is drawn as it always was.
// ---------------------------------------------------------------------------
public class PatienceBar : MonoBehaviour
{
    [SerializeField] private Renderer barRenderer;

    private Vector3 fullScale;
    private bool drawnInTheWorld = true;

    /// <summary>How much patience is left (0 to 1), as last told.</summary>
    public float Fraction { get; private set; } = 1f;
    /// <summary>The colour CustomerBrain gives a full bar (green in the queue, blue once their job is taken).</summary>
    public Color FullColour { get; private set; } = Color.green;
    /// <summary>Whose bar it is: the customer, or for a body without one, whoever it hangs from.</summary>
    public Transform Owner { get; private set; }
    public CustomerBrain Customer { get; private set; }

    private void Awake()
    {
        fullScale = transform.localScale;
        Customer = GetComponentInParent<CustomerBrain>();
        Owner = Customer != null ? Customer.transform : transform.parent != null ? transform.parent : transform;
        // Drawn on the screen over the head while playing (UiPins), so the strip in the world is put away.
        if (Application.isPlaying && UiPins.Ensure() != null)
        {
            drawnInTheWorld = false;
            if (barRenderer != null) barRenderer.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying && !drawnInTheWorld) UiPins.Register(this);
    }

    private void OnDisable() => UiPins.Unregister(this);

    public void SetFraction(float f, Color fullColor)
    {
        f = Mathf.Clamp01(f);
        Fraction = f;
        FullColour = fullColor;
        if (!drawnInTheWorld || barRenderer == null) return;

        // The strip in the world (Edit Mode, or no pins): as it always was.
        // Never let the mesh shrink to literally zero — that was causing the flash.
        float width = Mathf.Max(f, 0.02f);
        transform.localScale = new Vector3(fullScale.x * width, fullScale.y, fullScale.z);
        barRenderer.material.color = Color.Lerp(Color.red, fullColor, f);
    }
}
