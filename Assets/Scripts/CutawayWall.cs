using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// One cut-away wall in the overhead view (CafeViewMode owns these).
//
// When the wall blocks the view it no longer vanishes: it slides down to a low
// stand-in at window-sill height, and back up once it is clearly out of the way.
//
//   * The stand-in is a copy of the wall's own mesh whose top edge moves. The
//     texture stays where it was (a slice, not a squash).
//   * The real wall never moves and keeps its collider. While it is down it only
//     casts its shadow, so the light in the room doesn't change when you orbit.
//   * Pictures, shelves, lamps and boards fixed to the wall above the cut hide
//     while it is down, each one as the top edge passes it, so nothing floats.
//     Things standing on the floor or on a counter stay.
//
// A wall that can't be sliced (not a simple box, or its mesh can't be read)
// falls back to the old behaviour, hiding completely, and says why (Problem).
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

    public Renderer Wall { get; }
    /// <summary>The low stand-in (null when the wall can't be sliced).</summary>
    public MeshRenderer Stub { get; private set; }
    public bool Sliceable => Stub != null;
    /// <summary>Why this wall hides instead of sliding down (null when it slides).</summary>
    public string Problem { get; }
    /// <summary>The whole wall, as built. The real wall never moves, so this never changes.</summary>
    public Bounds FullBounds { get; }
    public float BaseY => FullBounds.min.y;
    public float FullTop => FullBounds.max.y;
    /// <summary>World height of the cut: how high the wall stays when fully down.</summary>
    public float CutTop => BaseY + cutFraction * FullBounds.size.y;
    /// <summary>World height of the wall's top edge as drawn right now.</summary>
    public float ShownTop => BaseY + shown * FullBounds.size.y;
    /// <summary>0 = up, 1 = fully down.</summary>
    public float Progress => progress;
    /// <summary>Down, or on its way down or up.</summary>
    public bool Lowered => progress > 0f;
    public bool GoingDown => goingDown;
    public IReadOnlyList<Renderer> Decor => decorRenderers;

    readonly float cutFraction;
    readonly ShadowCastingMode wallShadows;
    readonly bool wallEnabled;
    readonly Mesh mesh;
    readonly Vector3[] fullVertices, vertices;
    readonly Vector2[] fullUv, uv, fullUv2, uv2;
    // Per vertex: -1 never moves (bottom edge), -2 top face (drops, texture unchanged),
    // otherwise the bottom vertex straight below it on the same face.
    readonly int[] partner;
    readonly float lowLocal, highLocal;

    readonly List<Renderer> decorRenderers = new();
    readonly List<float> decorTops = new();
    readonly List<bool> decorHidden = new();
    // How many walls are hiding each renderer right now (two walls meet at a
    // corner, and one coming up mustn't show what the other still hides).
    static readonly Dictionary<Renderer, int> HiddenBy = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHidden() => HiddenBy.Clear();
    float progress, shown = 1f, appliedShown = -1f, clearFor, scannedAt = float.NegativeInfinity;
    bool goingDown, wallDown;

    public CutawayWall(Renderer wall, float keepHeight)
    {
        Wall = wall;
        FullBounds = wall.bounds;
        wallShadows = wall.shadowCastingMode;
        wallEnabled = wall.enabled;
        cutFraction = Mathf.Clamp01(keepHeight / Mathf.Max(.01f, FullBounds.size.y));
        Problem = Check(wall, out Mesh source, out lowLocal, out highLocal, out partner);
        if (Problem != null) return;

        fullVertices = source.vertices;
        vertices = (Vector3[])fullVertices.Clone();
        fullUv = source.uv.Length == fullVertices.Length ? source.uv : null;
        uv = fullUv != null ? (Vector2[])fullUv.Clone() : null;
        fullUv2 = source.uv2.Length == fullVertices.Length ? source.uv2 : null;
        uv2 = fullUv2 != null ? (Vector2[])fullUv2.Clone() : null;
        mesh = Object.Instantiate(source);
        mesh.name = source.name + " (cut-away)";
        mesh.MarkDynamic();

        // A child with no transform of its own, so it sits exactly on the wall.
        var stub = new GameObject(wall.name + " (cut-away)") { layer = wall.gameObject.layer };
        stub.transform.SetParent(wall.transform, false);
        stub.AddComponent<MeshFilter>().sharedMesh = mesh;
        Stub = stub.AddComponent<MeshRenderer>();
        Stub.sharedMaterials = wall.sharedMaterials;
        // The real wall keeps casting the full shadow; the stand-in casts none.
        Stub.shadowCastingMode = ShadowCastingMode.Off;
        Stub.receiveShadows = wall.receiveShadows;
        Stub.lightProbeUsage = wall.lightProbeUsage;
        Stub.reflectionProbeUsage = wall.reflectionProbeUsage;
        Stub.probeAnchor = wall.probeAnchor;
        Stub.renderingLayerMask = wall.renderingLayerMask;
        Stub.motionVectorGenerationMode = wall.motionVectorGenerationMode;
        Stub.allowOcclusionWhenDynamic = wall.allowOcclusionWhenDynamic;
        if (wall.lightmapIndex >= 0 && wall.lightmapIndex < 0xFFFE)
        {
            Stub.lightmapIndex = wall.lightmapIndex;
            Stub.lightmapScaleOffset = wall.lightmapScaleOffset;
        }
        Stub.enabled = false;
    }

    // A wall slices when it is an upright box: every vertex on its bottom or top
    // edge, and every side-face top corner with a bottom corner straight below it.
    static string Check(Renderer wall, out Mesh source, out float low, out float high, out int[] partner)
    {
        source = null; low = high = 0f; partner = null;
        if (wall is not MeshRenderer) return "it isn't a plain mesh";
        if (wall.isPartOfStaticBatch) return "it is static-batched, so its mesh is shared with other objects";
        MeshFilter filter = wall.GetComponent<MeshFilter>();
        source = filter != null ? filter.sharedMesh : null;
        if (source == null) return "it has no mesh";
        if (!source.isReadable) return "its mesh can't be read at run time";
        if (Vector3.Dot(wall.transform.up, Vector3.up) < .999f) return "it isn't upright";
        Vector3[] v = source.vertices;
        Vector3[] n = source.normals;
        if (v.Length == 0 || n.Length != v.Length) return "its mesh has no normals";
        low = float.MaxValue; high = float.MinValue;
        foreach (Vector3 p in v) { low = Mathf.Min(low, p.y); high = Mathf.Max(high, p.y); }
        if (high - low < 1e-4f) return "it is flat";
        float eps = (high - low) * 1e-3f;
        partner = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            bool top = Mathf.Abs(v[i].y - high) <= eps;
            if (!top && Mathf.Abs(v[i].y - low) > eps) return "it isn't a simple box (it has corners part-way up)";
            partner[i] = -1;
            if (!top) continue;
            if (n[i].y > .5f) { partner[i] = -2; continue; }
            for (int j = 0; j < v.Length && partner[i] == -1; j++)
                if (Mathf.Abs(v[j].y - low) <= eps && Mathf.Abs(v[j].x - v[i].x) <= eps
                    && Mathf.Abs(v[j].z - v[i].z) <= eps && Vector3.Dot(n[j], n[i]) > .99f)
                    partner[i] = j;
            if (partner[i] == -1) return "one of its side faces has no bottom corner below its top corner";
        }
        return null;
    }

    /// <summary>
    /// One frame. blocks: the full wall hides the room from the overhead camera.
    /// nearlyBlocks: it would still come close to hiding it (used before rising).
    /// </summary>
    public void Step(bool overhead, bool blocks, bool nearlyBlocks, float dt, float slideSeconds, float riseDelay,
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
        progress = slideSeconds <= 0f ? target : Mathf.MoveTowards(progress, target, dt / slideSeconds);
        Apply();
    }

    /// <summary>Back to the wall as built: up, drawn, casting its usual shadow.</summary>
    public void Restore()
    {
        goingDown = false; clearFor = 0f; progress = 0f;
        Apply();
    }

    public void Dispose()
    {
        Restore();
        ForgetDecor();
        if (Stub != null) Discard(Stub.gameObject);
        if (mesh != null) Discard(mesh);
        Stub = null;
    }

    static void Discard(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    void Apply()
    {
        bool down = progress > 0f;
        shown = down && Sliceable ? Mathf.Lerp(1f, cutFraction, Mathf.SmoothStep(0f, 1f, progress)) : down ? 0f : 1f;
        if (down != wallDown)
        {
            wallDown = down;
            if (Wall != null)
            {
                // Down: the real wall only casts its shadow (or, if it never cast
                // one, doesn't draw at all). Up: exactly as it was.
                Wall.shadowCastingMode = down && wallShadows != ShadowCastingMode.Off ? ShadowCastingMode.ShadowsOnly : wallShadows;
                Wall.enabled = wallEnabled && !(down && wallShadows == ShadowCastingMode.Off);
            }
            if (Stub != null) Stub.enabled = down;
        }
        if (Mathf.Approximately(shown, appliedShown)) return;
        appliedShown = shown;
        if (Sliceable && down) Slice(shown);
        float top = ShownTop;
        for (int i = 0; i < decorRenderers.Count; i++)
            SetHidden(i, down && top < decorTops[i] - .01f);
    }

    void SetHidden(int i, bool hide)
    {
        if (decorHidden[i] == hide) return;
        decorHidden[i] = hide;
        Renderer r = decorRenderers[i];
        if (r == null) return;
        HiddenBy.TryGetValue(r, out int count);
        count += hide ? 1 : -1;
        if (count > 0) { HiddenBy[r] = count; r.forceRenderingOff = true; }
        else { HiddenBy.Remove(r); r.forceRenderingOff = false; }
    }

    void ForgetDecor()
    {
        for (int i = 0; i < decorRenderers.Count; i++) SetHidden(i, false);
        decorRenderers.Clear();
        decorTops.Clear();
        decorHidden.Clear();
    }

    // Moves the top edge to the given fraction of the wall's height; side faces
    // take their texture coordinates from the same point on the full wall.
    void Slice(float fraction)
    {
        float y = Mathf.Lerp(lowLocal, highLocal, fraction);
        for (int i = 0; i < vertices.Length; i++)
        {
            int below = partner[i];
            if (below == -1) continue;
            Vector3 top = fullVertices[i];
            vertices[i] = new Vector3(top.x, y, top.z);
            if (below < 0) continue;
            if (uv != null) uv[i] = Vector2.LerpUnclamped(fullUv[below], fullUv[i], fraction);
            if (uv2 != null) uv2[i] = Vector2.LerpUnclamped(fullUv2[below], fullUv2[i], fraction);
        }
        mesh.vertices = vertices;
        if (uv != null) mesh.uv = uv;
        if (uv2 != null) mesh.uv2 = uv2;
        mesh.RecalculateBounds();
    }

    // ---- what is fixed to the wall ----

    // Looked up each time the wall starts going down (at most every few seconds),
    // so things added or moved during the day are found too.
    void FindDecor(Transform player, HashSet<Renderer> skip)
    {
        if (Time.unscaledTime - scannedAt < RescanAfter) return;
        scannedAt = Time.unscaledTime;
        ForgetDecor();

        Bounds w = FullBounds;
        bool normalX = w.size.x < w.size.z;
        float cut = CutTop;
        var objects = new Dictionary<Transform, bool>();
        var small = new Dictionary<Transform, bool>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
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
            float top = float.MinValue;
            foreach (Renderer part in parts) top = Mathf.Max(top, part.bounds.max.y);
            foreach (Renderer part in parts)
            {
                if (skip != null && skip.Contains(part)) continue;
                decorRenderers.Add(part);
                decorTops.Add(top);
                decorHidden.Add(false);
            }
        }
        appliedShown = -1f; // re-apply to the new list
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
    // use (an interactable, a physics body, a character) is never hidden.
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
