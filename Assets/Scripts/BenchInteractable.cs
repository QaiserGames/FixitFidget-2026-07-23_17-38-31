using UnityEngine;

// ---------------------------------------------------------------------------
// A PART THAT CAN BE WORKED ON AT THE BENCH (the bench, v2; claude/bench-spec-v2.md §2)
//
// Three ways a part is worked on, decided by the part:
//   * a press (Activate): a circuit tile, the phone's mute switch; and the legacy path every lab's hand and the Day 1
//     guide still use ("do the whole thing") — a screw's Activate unscrews it all the way, a cover's lifts it off;
//   * a hold (Holdable): press and keep pressing while the work happens (a screw backing out, a cover being pried,
//     tweezers pinching); let go early and the part keeps what was done (a screw stays part-way out);
//   * a grab (Grabbable): the part follows the cursor while pressed and is dropped where you let go (a popped cover, a
//     part in the tweezers).
// The hand passed in is the cursor's ray and what it hit, the tool in use and the tool model, so a part can place the
// tool on itself (the driver on the screw head) and read where the press landed (which edge of a cover).
// The highlight is an outline round the whole part (BenchOutline.cs), not a tinted material.
// ---------------------------------------------------------------------------
public struct BenchHand
{
    public Ray ray;
    public RaycastHit hit;
    public bool hitSomething;
    public ToolType tool;
    public ToolPickup toolModel;
    public Camera camera;
    public float deltaTime;

    /// <summary>
    /// The point on the cursor's ray this deep in front of the camera (its distance along the camera's forward). A held
    /// part rides here at the depth it was picked up at. Measured from the camera, not from the ray's own origin: a
    /// screen ray starts on the near clip plane, 5 cm out, and a part placed "depth along the ray" from there rode 5 cm
    /// too deep and was carried in under the tray's floor (8 Oct).
    /// </summary>
    public Vector3 PointAtDepth(float depth)
    {
        Vector3 forward = camera != null ? camera.transform.forward : ray.direction;
        Vector3 eye = camera != null ? camera.transform.position : ray.origin;
        float cos = Mathf.Max(.2f, Vector3.Dot(ray.direction, forward));
        float startDepth = Vector3.Dot(ray.origin - eye, forward);
        return ray.origin + ray.direction * ((depth - startDepth) / cos);
    }
}

public abstract class BenchInteractable : MonoBehaviour
{
    private bool highlighted;

    protected virtual void Awake() { }

    // What this part is called, for the hover tooltip.
    public virtual string DisplayName => "Part";

    // What pressing it would do.
    public virtual string Prompt => "Interact";

    // Can the player act on this right now?
    public abstract bool CanInteract { get; }

    // Which tool does this need? Hand means "any".
    public abstract ToolType RequiredTool { get; }

    /// <summary>The whole action at once: the legacy press, and what the labs' hand and the Day 1 guide call.</summary>
    public abstract void Activate();

    // ---------- holds ----------

    /// <summary>True when a press on this part is the start of a hold rather than an instant action.</summary>
    public virtual bool Holdable => false;
    /// <summary>Called on the press that begins a hold.</summary>
    public virtual void HoldBegin(BenchHand hand) { }
    /// <summary>Called every frame the hold goes on. Returns true when the work is finished and the hold ends by itself.</summary>
    public virtual bool HoldTick(BenchHand hand) => true;
    /// <summary>Called when the hold ends: finished, or let go early (<paramref name="finished"/> says which).</summary>
    public virtual void HoldEnd(BenchHand hand, bool finished) { }
    /// <summary>How far the current hold has got, 0..1, for the tool's animation and the prompt.</summary>
    public virtual float HoldProgress => 0f;

    // ---------- grabs ----------

    /// <summary>True when a press on this part picks it up to be moved with the cursor.</summary>
    public virtual bool Grabbable => false;
    public virtual void GrabBegin(BenchHand hand) { }
    public virtual void GrabMove(BenchHand hand) { }
    public virtual void GrabEnd(BenchHand hand) { }

    // ---------- where the tool goes ----------

    /// <summary>Where the tool's tip sits while working on this part.</summary>
    public virtual Vector3 WorkPoint => BoundsCentre(transform);
    /// <summary>The direction the tool's handle points away from the part (the surface normal at the work point).</summary>
    public virtual Vector3 WorkNormal => transform.up;

    public virtual void SetHighlight(bool on)
    {
        if (highlighted == on) return;
        highlighted = on;
        BenchOutline.Set(gameObject, on);
    }

    public static Vector3 BoundsCentre(Transform t)
    {
        var c = t.GetComponentInChildren<Collider>();
        if (c != null && c.enabled) return c.bounds.center;
        var r = t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }
}
