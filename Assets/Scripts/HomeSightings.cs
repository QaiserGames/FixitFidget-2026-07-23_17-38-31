using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// Could Ace see this person just now? (night step 3, claude/night-homes-spec.md
// §3.3). Asked a few times a second while a regular is at their own front door.
//
// Seen means: in the game camera's frame (overhead or first person, whichever
// is showing), and nothing solid between the camera and their chest or head.
// Glass counts as see-through (watching from the café's windows works), and so
// do things nobody can see: invisible blockers, a wall that is cut away in the
// overhead view, and other people passing (a passer-by only hides a doorway for
// a moment).
//
// The two halves (InFrame, Blocker) are public so the checks can say why a
// doorway wasn't seen: out of the frame, or behind what.
// ---------------------------------------------------------------------------
public static class HomeSightings
{
    const float Edge = .02f;
    static readonly RaycastHit[] hits = new RaycastHit[24];

    public static bool CanSee(Camera camera, Transform person)
    {
        if (camera == null || person == null || !camera.isActiveAndEnabled) return false;
        Vector3 feet = person.position;
        return CanSeePoint(camera, feet + Vector3.up * 1.15f, person) || CanSeePoint(camera, feet + Vector3.up * 1.6f, person);
    }

    public static bool CanSeePoint(Camera camera, Vector3 point, Transform ignore) =>
        InFrame(camera, point) && Blocker(camera, point, ignore) == null;

    /// <summary>In front of the camera, within its range, and inside the frame (a little in from the edges).</summary>
    public static bool InFrame(Camera camera, Vector3 point)
    {
        Vector3 view = camera.WorldToViewportPoint(point);
        return view.z > camera.nearClipPlane && view.z <= camera.farClipPlane
            && view.x >= Edge && view.x <= 1f - Edge && view.y >= Edge && view.y <= 1f - Edge;
    }

    /// <summary>The nearest solid thing between the camera and the point, or null when the view is clear.</summary>
    public static Collider Blocker(Camera camera, Vector3 point, Transform ignore)
    {
        Vector3 from = camera.transform.position;
        Vector3 to = point - from;
        float distance = to.magnitude;
        if (distance < .01f) return null;
        int n = Physics.RaycastNonAlloc(from, to / distance, hits, distance - .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Collider nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || ignore != null && c.transform.IsChildOf(ignore)) continue;
            if (hits[i].distance < nearestDistance && Solid(c)) { nearest = c; nearestDistance = hits[i].distance; }
        }
        return nearest;
    }

    // Something that blocks the view: drawn, not glass, not a person.
    static bool Solid(Collider c)
    {
        if (c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<NavMeshAgent>() != null
            || c.GetComponentInParent<Animator>() != null) return false;
        Renderer own = c.GetComponent<Renderer>();
        if (own != null) return Shows(own);
        // A collider on a group (a building): solid when something drawn below it is.
        foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
            if (Shows(r)) return true;
        return false;
    }

    static bool Shows(Renderer r) =>
        r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff
        && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !Glass(r);

    // Clear glass only: a building's "opaque glass" windows still hide what's inside.
    static bool Glass(Renderer r)
    {
        Material[] materials = r.sharedMaterials;
        if (materials.Length == 0) return false;
        foreach (Material m in materials)
        {
            if (m == null) continue;
            string name = m.name.ToLowerInvariant();
            if (!name.Contains("glass") || name.Contains("opaque")) return false;
        }
        return true;
    }
}
