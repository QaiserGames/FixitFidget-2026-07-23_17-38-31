using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// One cut-away wall in the overhead view (CafeViewMode owns these).
//
// WHAT IT DOES NOW (30 Sept 2026; Mansoor's second playtest: the walls dropping
// "looks too instant and weird", he asked for a fade, "like an actual published
// game"): a wall in the way no longer slides down. It fades to a ghost above
// the window sill, over a third of a second with an ease, and comes back the
// same way once it has been clearly out of the way for a moment.
//
//   * How: the wall wears a see-through copy of its own materials (the dither
//     shader "Fixit Fidget/Night see-through" through SeeThroughMaterials, the
//     same dots the night's buildings use) with the dots set by height: solid
//     up to the sill, feathering over the next half metre, and above that one
//     dot in five. The wall never moves, keeps its collider, and casts its whole
//     shadow throughout, so the light in the room doesn't change as you orbit.
//   * Pictures, shelves, lamps and boards fixed to the wall above the sill fade
//     with it, at the same dots and the same heights, so nothing pops or floats.
//     Things standing on the floor or on a counter stay solid.
//   * Two walls meeting at a corner may both claim a picture: it wears one copy
//     and gets its own materials back only when neither wall needs it.
//
// A wall whose materials can't be copied (no albedo to dot, no surface switch)
// falls back to the old behaviour, hiding above nothing (the whole wall casts
// only its shadow) and says why (Problem).
// ---------------------------------------------------------------------------
public sealed class CutawayWall
{
    // Fixed things further than this from the wall's faces are not "on" it.
    const float Reach = .55f;
    // A thing whose lowest point is at least this high is mounted, not standing
    // on a counter (counter tops here are 0.9-1.1 m).
    const float MountedAbove = 1.14f;
    // Or it is flat against the wall: a backsplash, a board, a frame.
    const float FlatGap = .05f, FlatDepth = .12f, FlatHeight = .35f;
    // A group of renderers counts as one object while it fits in this box.
    const float ObjectWidth = 2.6f, ObjectHeight = 3.4f;
    const int ObjectParts = 64;
    // Rescan what hangs on the wall at most this often (seconds).
    const float RescanAfter = 5f;
    // The scene is searched for candidates at most this often (seconds), for every wall at once.
    const float CandidatesFresh = 60f;
    // Only renderers within this far of a cut-away wall are candidates for hanging on one (metres).
    const float CandidateReach = 4f;

    public Renderer Wall { get; }
    /// <summary>Why this wall hides instead of fading (null when it fades).</summary>
    public string Problem { get; }
    /// <summary>The wall fades to a ghost (its materials could be copied); otherwise it hides.</summary>
    public bool Fades => Problem == null;
    /// <summary>The whole wall, as built. The real wall never moves, so this never changes.</summary>
    public Bounds FullBounds { get; }
    public float BaseY => FullBounds.min.y;
    public float FullTop => FullBounds.max.y;
    /// <summary>World height up to which the wall stays solid while it is down: the window sill.</summary>
    public float SillTop => BaseY + sillHeight;
    /// <summary>World height above which the wall is fully the ghost; it feathers between the sill and here.</summary>
    public float GhostFrom => SillTop + feather;
    /// <summary>How much of the wall above the sill is drawn when fully down (1 = all, .2 = one dot in five).</summary>
    public float Ghost => ghost;
    /// <summary>How much of the wall above the sill is drawn right now (1 = all of it).</summary>
    public float Keep => keep;
    /// <summary>0 = up (as built), 1 = fully down (the ghost).</summary>
    public float Progress => progress;
    /// <summary>Down, or on its way down or up.</summary>
    public bool Lowered => progress > 0f;
    public bool GoingDown => goingDown;
    /// <summary>The wall and what hangs on it wear their see-through copies right now.</summary>
    public bool Worn => worn;
    public IReadOnlyList<Renderer> Decor => decorRenderers;

    readonly float sillHeight, feather, ghost;
    readonly ShadowCastingMode wallShadows;
    readonly bool wallEnabled;

    readonly List<Renderer> decorRenderers = new();
    readonly List<bool> decorWorn = new();   // this wall holds the piece's copy (SeeThroughMaterials counts holders)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShared()
    {
        wallBounds.Clear();
        candidates = Array.Empty<MeshRenderer>();
        candidatesAt = float.NegativeInfinity;
    }

    float progress, keep = 1f, appliedKeep = -1f, clearFor, scannedAt = float.NegativeInfinity;
    bool goingDown, worn, wallHidden;

    public CutawayWall(Renderer wall, float sillHeight, float feather, float ghost)
    {
        Wall = wall;
        FullBounds = wall.bounds;
        if (!wallBounds.Contains(FullBounds)) { wallBounds.Add(FullBounds); ForgetCandidates(); }
        wallShadows = wall.shadowCastingMode;
        wallEnabled = wall.enabled;
        this.sillHeight = Mathf.Clamp(sillHeight, 0f, Mathf.Max(0f, FullBounds.size.y - .05f));
        this.feather = Mathf.Max(.01f, feather);
        this.ghost = Mathf.Clamp(ghost, .05f, 1f);
        Problem = Check(wall);
    }

    // A wall fades when every material it wears can be copied see-through.
    static string Check(Renderer wall)
    {
        if (wall is not MeshRenderer) return "it isn't a plain mesh";
        Material[] materials = wall.sharedMaterials;
        if (materials.Length == 0) return "it has no material";
        if (!SeeThroughMaterials.Dithered) return "the see-through shader wasn't found (CafeViewMode keeps it)";
        foreach (Material m in materials)
            if (m == null || !SeeThroughMaterials.CanCopy(m))
                return $"its material {(m != null ? m.name : "(none)")} has no albedo to dot";
        return null;
    }

    /// <summary>
    /// One frame. blocks: the full wall hides the room from the overhead camera.
    /// nearlyBlocks: it would still come close to hiding it (used before rising).
    /// </summary>
    public void Step(bool overhead, bool blocks, bool nearlyBlocks, float dt, float fadeSeconds, float riseDelay,
                     Transform player, HashSet<Renderer> skip)
    {
        if (!overhead)
        {
            // A close-up or first person: the wall is simply there.
            goingDown = false; clearFor = 0f; progress = 0f;
        }
        else if (blocks)
        {
            goingDown = true; clearFor = 0f;
        }
        else if (goingDown)
        {
            clearFor = nearlyBlocks ? 0f : clearFor + dt;
            if (clearFor >= riseDelay) goingDown = false;
        }
        float target = goingDown ? 1f : 0f;
        if (progress <= 0f && target > 0f) FindDecor(player, skip);
        progress = fadeSeconds <= 0f ? target : Mathf.MoveTowards(progress, target, dt / fadeSeconds);
        Apply();
    }

    /// <summary>Back to the wall as built: solid, its own materials, casting its usual shadow.</summary>
    public void Restore()
    {
        goingDown = false; clearFor = 0f; progress = 0f;
        Apply();
    }

    public void Dispose()
    {
        Restore();
        ForgetDecor();
        wallBounds.Remove(FullBounds);
    }

    void Apply()
    {
        bool down = progress > 0f;
        // An ease both ways: nothing starts or stops with a jolt.
        keep = down ? Mathf.Lerp(1f, ghost, Mathf.SmoothStep(0f, 1f, progress)) : 1f;
        if (Fades)
        {
            if (down && !worn) WearAll();
            else if (!down && worn) TakeOffAll();
            if (worn && !Mathf.Approximately(keep, appliedKeep))
            {
                appliedKeep = keep;
                ShowAll();
            }
            return;
        }
        // The fallback: the wall hides (its shadow stays) and what hangs on it hides with it.
        if (down != wallHidden)
        {
            wallHidden = down;
            if (Wall != null)
            {
                Wall.shadowCastingMode = down && wallShadows != ShadowCastingMode.Off ? ShadowCastingMode.ShadowsOnly : wallShadows;
                Wall.enabled = wallEnabled && !(down && wallShadows == ShadowCastingMode.Off);
            }
            for (int i = 0; i < decorRenderers.Count; i++) SetHidden(i, down);
        }
    }

    // ---- wearing the see-through copies ----

    void WearAll()
    {
        worn = true;
        appliedKeep = -1f;
        SeeThroughMaterials.Wear(Wall);
        for (int i = 0; i < decorRenderers.Count; i++)
            if (!decorWorn[i]) { decorWorn[i] = true; SeeThroughMaterials.Wear(decorRenderers[i]); }
    }

    void TakeOffAll()
    {
        worn = false;
        SeeThroughMaterials.TakeOff(Wall);
        for (int i = 0; i < decorRenderers.Count; i++)
            if (decorWorn[i]) { decorWorn[i] = false; SeeThroughMaterials.TakeOff(decorRenderers[i]); }
    }

    void ShowAll()
    {
        float from = SillTop, to = GhostFrom;
        SeeThroughMaterials.Show(Wall, keep, from, to);
        for (int i = 0; i < decorRenderers.Count; i++) SeeThroughMaterials.Show(decorRenderers[i], keep, from, to);
    }

    // The fallback's hiding (a wall that can't fade): what hangs on it hides with it.
    void SetHidden(int i, bool hide)
    {
        if (decorWorn[i] == hide) return;
        decorWorn[i] = hide;
        if (hide) SeeThroughMaterials.Wear(decorRenderers[i], hideInstead: true);
        else SeeThroughMaterials.TakeOff(decorRenderers[i]);
    }

    void ForgetDecor()
    {
        for (int i = 0; i < decorRenderers.Count; i++)
            if (decorWorn[i]) { decorWorn[i] = false; SeeThroughMaterials.TakeOff(decorRenderers[i]); }
        decorRenderers.Clear();
        decorWorn.Clear();
    }

    // ---- what is fixed to the wall ----

    // WHY THE CANDIDATES ARE SHARED (30 Sept 2026)
    // Each wall used to search the whole scene (FindObjectsByType over every mesh renderer: thousands,
    // with the city in the scene) every time it started going down, at most every five seconds. Orbiting
    // the café drops one wall after another, so that was a search every couple of seconds: a spike each
    // time (Mansoor's second playtest: the frame rate drops when he looks around). Now the scene is
    // searched at most once a minute, for every wall at once, and only what stands within a few metres
    // of some cut-away wall is kept; a wall going down looks through that short list.
    static MeshRenderer[] candidates = Array.Empty<MeshRenderer>();
    static float candidatesAt = float.NegativeInfinity;
    static readonly List<Bounds> wallBounds = new();

    static MeshRenderer[] Candidates()
    {
        if (Time.unscaledTime - candidatesAt < CandidatesFresh) return candidates;
        candidatesAt = Time.unscaledTime;
        var kept = new List<MeshRenderer>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            for (int i = 0; i < wallBounds.Count; i++)
            {
                Bounds near = wallBounds[i];
                near.Expand(CandidateReach * 2f);
                if (near.Intersects(b)) { kept.Add(r); break; }
            }
        }
        candidates = kept.ToArray();
        return candidates;
    }

    /// <summary>Forget the shared candidates: the next wall to go down searches the scene again (after furnishing changes).</summary>
    public static void ForgetCandidates() => candidatesAt = float.NegativeInfinity;

    // Looked up each time the wall starts going down (at most every few seconds),
    // so things added or moved during the day are found too.
    void FindDecor(Transform player, HashSet<Renderer> skip)
    {
        if (Time.unscaledTime - scannedAt < RescanAfter) return;
        scannedAt = Time.unscaledTime;
        ForgetDecor();

        Bounds w = FullBounds;
        bool normalX = w.size.x < w.size.z;
        float cut = SillTop;
        var objects = new Dictionary<Transform, bool>();
        var small = new Dictionary<Transform, bool>();
        foreach (MeshRenderer r in Candidates())
        {
            if (r == null || skip != null && skip.Contains(r)) continue;
            if (player != null && r.transform.IsChildOf(player)) continue;
            Bounds b = r.bounds;
            if (b.max.y <= cut + .02f || !NearWall(b, w, normalX)) continue;
            Transform root = ObjectRoot(r.transform, small);
            if (!objects.ContainsKey(root)) objects[root] = Mounted(root, w, normalX, cut);
        }
        foreach (KeyValuePair<Transform, bool> pair in objects)
        {
            if (!pair.Value) continue;
            Renderer[] parts = pair.Key.GetComponentsInChildren<Renderer>(false);
            foreach (Renderer part in parts)
            {
                if (part == Wall || skip != null && skip.Contains(part)) continue;
                decorRenderers.Add(part);
                decorWorn.Add(false);
            }
        }
        // A wall already down wears its new list at once.
        if (worn) WearAll();
        appliedKeep = -1f;
    }

    static bool NearWall(Bounds b, Bounds w, bool normalX)
    {
        float a = normalX ? b.min.x : b.min.z, e = normalX ? b.max.x : b.max.z;
        float s0 = normalX ? w.min.x : w.min.z, s1 = normalX ? w.max.x : w.max.z;
        if (e < s0 - Reach || a > s1 + Reach) return false;
        float c = normalX ? b.center.z : b.center.x;
        float t0 = normalX ? w.min.z : w.min.x, t1 = normalX ? w.max.z : w.max.x;
        return c >= t0 - .1f && c <= t1 + .1f;
    }

    // The largest group above this renderer that is still one small thing (a
    // picture and its frame, a radio, a shelf and its cups), so an object is
    // judged as a whole: a radio's aerial goes with the radio on the counter.
    static Transform ObjectRoot(Transform t, Dictionary<Transform, bool> small)
    {
        Transform root = t;
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (!small.TryGetValue(p, out bool fits))
            {
                Renderer[] parts = p.GetComponentsInChildren<Renderer>(false);
                fits = parts.Length > 0 && parts.Length <= ObjectParts;
                if (fits)
                {
                    Bounds u = parts[0].bounds;
                    for (int i = 1; i < parts.Length; i++) u.Encapsulate(parts[i].bounds);
                    fits = u.size.x <= ObjectWidth && u.size.z <= ObjectWidth && u.size.y <= ObjectHeight;
                }
                small[p] = fits;
            }
            if (!fits) break;
            root = p;
        }
        return root;
    }

    // Fixed to the wall: close to one of its faces, along it, rising above the
    // cut, and either hung high or flat against it. Anything people or the game
    // use (an interactable, a physics body, a character) is never touched.
    static bool Mounted(Transform root, Bounds w, bool normalX, float cut)
    {
        if (root.GetComponentInParent<Interactable>(true) != null || root.GetComponentInChildren<Interactable>(true) != null
            || root.GetComponentInParent<Rigidbody>(true) != null || root.GetComponentInChildren<Rigidbody>(true) != null
            || root.GetComponentInParent<Animator>(true) != null || root.GetComponentInChildren<Animator>(true) != null
            || root.GetComponentInParent<NavMeshAgent>(true) != null || root.GetComponentInParent<CharacterController>(true) != null)
            return false;
        Renderer[] parts = root.GetComponentsInChildren<Renderer>(false);
        if (parts.Length == 0) return false;
        Bounds u = parts[0].bounds;
        for (int i = 1; i < parts.Length; i++) u.Encapsulate(parts[i].bounds);

        float a = normalX ? u.min.x : u.min.z, e = normalX ? u.max.x : u.max.z;
        float s0 = normalX ? w.min.x : w.min.z, s1 = normalX ? w.max.x : w.max.z;
        float gap = Mathf.Max(0f, Mathf.Max(a - s1, s0 - e));
        float depth = Mathf.Max(0f, e - s1) + Mathf.Max(0f, s0 - a);
        float across = normalX ? u.size.x : u.size.z, along = normalX ? u.size.z : u.size.x;
        float length = normalX ? w.size.z : w.size.x;
        if (gap > Reach || across > 1.2f || along > length + .3f || u.size.y > ObjectHeight) return false;
        if (u.max.y <= cut + .02f) return false;
        bool high = u.min.y >= MountedAbove;
        bool flat = gap <= FlatGap && depth <= FlatDepth && u.size.y >= FlatHeight && u.min.y >= cut - .05f;
        return high || flat;
    }
}
