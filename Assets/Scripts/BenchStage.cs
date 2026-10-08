using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BENCH'S STAGE: THE MAT, THE MAGNETIC TRAY, THE CATCH, THE CADDY (the bench, v2; claude/bench-spec-v2.md §2.5)
//
// What a loose thing at the bench lands on. The device hangs at the inspect point; under it a cutting mat (a solid), to
// its right the parts tray, to its left the tool caddy. Screws and parts the player frees are rigidbodies: they fall,
// bounce and roll. The tray is magnetic: a loose piece inside its zone is drawn down onto the tray's floor and settles
// where it lands with a tink. Nothing is ever lost: a piece that leaves the mat (over its edge, or through a gap in the
// physics) is caught and set back down over the tray.
//
// Built in the café scene by Fixit Fidget > Bench > Bench 2 - Build the bench stage, which also makes the mat, the
// tray and the caddy out of simple shapes until the devices session models them. One per bench.
// ---------------------------------------------------------------------------
public class BenchStage : MonoBehaviour
{
    [Header("The pieces (set by the build step)")]
    [SerializeField] private Transform mat;                 // a solid slab the loose parts land on
    [SerializeField] private Transform tray;                // the tray's floor (its centre is where the magnet pulls)
    [SerializeField] private BoxCollider trayZone;          // the magnet's reach (a trigger)
    [SerializeField] private Transform caddy;               // the tools' home
    [SerializeField] private Transform[] toolSlots;         // one stand per tool, in ToolType order after Hand

    [Header("The magnet")]
    [Tooltip("How hard the tray pulls a loose piece down onto its floor, in metres per second squared (on top of gravity).")]
    [SerializeField, Min(0f)] private float pull = 2.2f;
    [Tooltip("Velocity below which a piece inside the tray is at rest.")]
    [SerializeField, Min(0f)] private float restSpeed = .02f;

    [Header("The catch")]
    [Tooltip("A piece this far under the mat has got away: it is set back down over the tray.")]
    [SerializeField, Min(0f)] private float fallLimit = .25f;

    public static BenchStage Instance { get; private set; }

    public Transform Mat => mat;
    public Transform Tray => tray;
    public Transform Caddy => caddy;
    public Transform ToolSlot(ToolType tool)
    {
        int index = (int)tool - 1;
        return toolSlots != null && index >= 0 && index < toolSlots.Length ? toolSlots[index] : caddy;
    }

    /// <summary>A point just above the tray's floor, scattered a little so pieces don't stack on one spot.</summary>
    public Vector3 TrayDropPoint(float height = .03f, float scatterRadius = .018f)
    {
        if (tray == null) return transform.position;
        Vector2 scatter = Random.insideUnitCircle * scatterRadius;
        return tray.position + tray.right * scatter.x + tray.forward * scatter.y + tray.up * height;
    }

    /// <summary>The way a long piece lies in the tray: its long side along the tray's long side (the tray's right, as built).</summary>
    public Vector3 TrayLongAxis => tray != null ? tray.right : transform.right;

    /// <summary>True when the point is inside the magnet's reach.</summary>
    public bool InTray(Vector3 point) => trayZone != null && trayZone.bounds.Contains(point);

    // ---------- the loose pieces ----------

    sealed class Loose
    {
        public Rigidbody body;
        public Collider collider;
        public float stillSince;
        public bool settled;
        public string landCue;
    }

    readonly List<Loose> loose = new List<Loose>();

    /// <summary>Starts watching a freed piece: the magnet and the catch act on it from now on.</summary>
    public void Watch(Rigidbody body, string landCue = "screw.drop")
    {
        if (body == null) return;
        foreach (Loose l in loose) if (l.body == body) return;
        loose.Add(new Loose { body = body, collider = body.GetComponent<Collider>(), landCue = landCue });
    }

    /// <summary>The piece is in a hand or back on its device: the stage lets it go.</summary>
    public void Forget(Rigidbody body)
    {
        for (int i = loose.Count - 1; i >= 0; i--) if (loose[i].body == body || loose[i].body == null) loose.RemoveAt(i);
    }

    /// <summary>Every piece the stage is minding that is at rest in the tray.</summary>
    public IEnumerable<Rigidbody> SettledInTray()
    {
        foreach (Loose l in loose) if (l.body != null && l.settled && InTray(l.body.position)) yield return l.body;
    }

    void Awake() { Instance = this; }
    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    void FixedUpdate()
    {
        if (loose.Count == 0) return;
        float matTop = mat != null ? mat.position.y : transform.position.y;
        for (int i = loose.Count - 1; i >= 0; i--)
        {
            Loose l = loose[i];
            if (l.body == null) { loose.RemoveAt(i); continue; }
            if (l.body.isKinematic) continue;   // held by a hand, or at rest
            Vector3 p = l.body.position;

            // The catch: fallen out of the world of the bench.
            if (p.y < matTop - fallLimit)
            {
                l.body.linearVelocity = Vector3.zero;
                l.body.angularVelocity = Vector3.zero;
                l.body.position = TrayDropPoint();
                l.settled = false;
                continue;
            }

            // The magnet: inside the zone, a pull DOWN onto the floor and a damping of the slide, so a piece stops where it
            // lands instead of bouncing out. Not a pull to the middle: that heaped every piece on one spot, the fresh
            // part under the broken one (7 Oct).
            if (trayZone != null && trayZone.bounds.Contains(p))
            {
                l.body.AddForce(-tray.up * pull, ForceMode.Acceleration);
                l.body.linearVelocity *= 1f - Mathf.Clamp01(6f * Time.fixedDeltaTime);
                l.body.angularVelocity *= 1f - Mathf.Clamp01(8f * Time.fixedDeltaTime);
                if (l.body.linearVelocity.magnitude < restSpeed)
                {
                    l.stillSince += Time.fixedDeltaTime;
                    if (l.stillSince > .25f && !l.settled)
                    {
                        l.settled = true;
                        l.body.linearVelocity = Vector3.zero;
                        l.body.angularVelocity = Vector3.zero;
                        l.body.Sleep();
                    }
                }
                else l.stillSince = 0f;
            }
            else
            {
                l.stillSince = 0f;
                l.settled = false;
            }
        }
    }

    /// <summary>A free spot on the mat for a piece of about this size (the legacy "lift off" puts a cover down here).</summary>
    public Vector3 FreeSpotOnMat(float size)
    {
        if (mat == null) return transform.position;
        Vector3 top = mat.position + Vector3.up * (MatHalfHeight() + .05f);
        // Try a ring of spots round the mat's far half, front to back, left to right.
        var candidates = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            float x = Mathf.Lerp(-.16f, .16f, (i % 4) / 3f);
            float z = i < 4 ? .07f : -.07f;
            candidates.Add(top + mat.right * x + mat.forward * z);
        }
        foreach (Vector3 c in candidates)
        {
            bool clear = true;
            foreach (Loose l in loose)
                if (l.body != null && Vector3.Distance(new Vector3(l.body.position.x, 0f, l.body.position.z), new Vector3(c.x, 0f, c.z)) < size)
                { clear = false; break; }
            if (clear) return c;
        }
        return candidates[Random.Range(0, candidates.Count)];
    }

    float MatHalfHeight()
    {
        var box = mat != null ? mat.GetComponent<BoxCollider>() : null;
        return box != null ? box.size.y * .5f * mat.lossyScale.y : .003f;
    }

#if UNITY_EDITOR
    /// <summary>The build step's hand on the private fields.</summary>
    public void Wire(Transform mat, Transform tray, BoxCollider trayZone, Transform caddy, Transform[] toolSlots)
    {
        this.mat = mat; this.tray = tray; this.trayZone = trayZone; this.caddy = caddy; this.toolSlots = toolSlots;
    }
#endif
}
