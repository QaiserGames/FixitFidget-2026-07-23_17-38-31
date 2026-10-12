using UnityEngine;

// ---------------------------------------------------------------------------
// WHAT'S DRAWN OF A DEVICE (12 Oct 2026; Mansoor's playtest with a controller: "the phone was not in the view")
//
// Everything that measures a device or a thing (the bench's slab, the name pinned over it, the "Fixed!" pop at its middle,
// the way a screw falls away from it) measures its own solid parts, and nothing else:
//   * not its job tag (JobMarker, and everything under it): the phone's marker sat 70 cm along its length, a leftover from
//     when its body was a stretched cube, and its old text's renderer, empty, still had a place there;
//   * not sprites, lines, trails or particles (a tag, a ring, a dust puff: they float about the thing, they aren't it);
//   * not a renderer that is switched off or has no mesh, and not a text.
// With the marker counted, the bench measured a customer's phone as 77 cm long and laid it 31.5 cm off the middle of the
// mat, off the screen, and every turn swung it round that far-away middle (the bench view probe, 12 Oct).
// ---------------------------------------------------------------------------
public static class DeviceBounds
{
    /// <summary>True when <paramref name="r"/> is one of the thing's own solid parts (see the header).</summary>
    public static bool Counts(Renderer r)
    {
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
        if (r is SpriteRenderer || r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) return false;
        if (r.GetComponentInParent<JobMarker>(true) != null) return false;
        if (r.GetComponent<TMPro.TMP_Text>() != null) return false;
        if (r is SkinnedMeshRenderer skinned) return skinned.sharedMesh != null;
        MeshFilter mesh = r.GetComponent<MeshFilter>();
        return mesh != null && mesh.sharedMesh != null;
    }

    /// <summary>The world box around what's drawn of <paramref name="thing"/>. False when nothing of it is drawn.</summary>
    public static bool TryGet(GameObject thing, out Bounds bounds)
    {
        bounds = default;
        if (thing == null) return false;
        bool any = false;
        foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
        {
            if (!Counts(r)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    /// <summary>The middle of what's drawn of <paramref name="thing"/>, or its own place when nothing is.</summary>
    public static Vector3 Centre(GameObject thing) =>
        TryGet(thing, out Bounds b) ? b.center : thing != null ? thing.transform.position : Vector3.zero;

    /// <summary>The top middle of what's drawn of <paramref name="thing"/>, or its own place when nothing is.</summary>
    public static Vector3 Top(GameObject thing) =>
        TryGet(thing, out Bounds b) ? new Vector3(b.center.x, b.max.y, b.center.z) : thing != null ? thing.transform.position : Vector3.zero;
}
