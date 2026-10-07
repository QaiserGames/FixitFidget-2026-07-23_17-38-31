using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ---------------------------------------------------------------------------
// NIGHT WALK, PART 3: BUILDINGS IN THE WAY TURN SEE-THROUGH
//
// While the overhead camera follows Ace at night (CafeViewMode.FollowAce), a
// building or a big tree standing between the camera and Ace fades to a quarter
// in a fifth of a second, and comes back once it has been out of the way for a
// moment. The whole building fades, never single pieces, so nothing looks like
// it is falling apart.
//
//   * What counts as one building: the nearest object named "Building ..." or
//     "... house" above a piece (POLYGON's buildings, the bay-window houses, the
//     shop houses); anything else (trees, a bus shelter) is the largest group of
//     pieces no wider than a tree. Only things taller than Ace's head and at
//     least a metre thick on both sides count: lamp posts, signs, fences, cars
//     and the road works never fade. The café room has its own cut-away walls
//     (CafeViewMode), and the ground, the hill and the skyline never fade.
//   * In the way: any of five sightlines from the camera to Ace (knees, middle,
//     head, and either side) passes through one of the building's pieces (their
//     bounding boxes; cheap enough to test every frame). Since 6 Oct 2026 also the
//     ground in front of Ace (Ahead): two more sightlines, to points 6 m and 12 m
//     from Ace toward the camera, so a roof between the camera and the street Ace
//     is walking toward fades too (at the home view and far zoom, the terrace south
//     of the car park covered the lower half of the screen with Ace on the front
//     street).
//   * How it fades: a screen-door dither (the shader "Fixit Fidget/Night
//     see-through", URP's Lit with the dots added). Each material a building
//     wears gets a copy with that shader (SeeThroughMaterials: made once per
//     Play session, shared with the café's cut-away walls, never saved): the
//     same albedo, colour, cut-out and glow (the night's lit windows). A
//     fading building draws fewer and fewer of its dots, down to one in four;
//     all its surfaces use the same dots, so what is behind shows through the
//     gaps without the murky layers a blended fade gives. It gets its own
//     materials back afterwards. Synty's own files are never touched. (Without
//     the shader, a blended copy stands in: _Surface switched to Transparent.)
//
// Night only, overhead only: in first person, at a station or by day nothing
// here runs, and Clear() puts every material back.
//
// A house Ace has walked into (the break-ins) is left alone while Ace is inside
// (Leave): it shows its own rooms, like a doll's house (GraceHouse).
// ---------------------------------------------------------------------------
[DefaultExecutionOrder(-50)]   // after CafeViewMode (-100) has placed the overhead camera this frame
[DisallowMultipleComponent]
public sealed class NightSeeThrough : MonoBehaviour
{
    [Tooltip("How much of a building in the way is still drawn: a quarter of its dots.")]
    [Range(.05f, 1f)] public float seeThroughAlpha = .25f;
    [Tooltip("Seconds to fade out, or back in.")]
    [Min(.01f)] public float fadeSeconds = .2f;
    [Tooltip("Seconds a building must be clearly out of the way before it comes back. Stops it flickering at the edge.")]
    [Min(0f)] public float backAfter = .3f;
    [Tooltip("Only things whose top is higher than this above the street can hide Ace (metres).")]
    public float minTop = 2.3f;
    [Tooltip("Thin things (lamp posts, signs) never fade: both sides of a building's footprint must be at least this wide.")]
    public float minThickness = 1f;
    [Tooltip("Anything but a named building or house groups its pieces up to this width (metres): a tree, a bus shelter.")]
    public float clusterWidth = 8f;
    [Tooltip("Wider than this is ground, not a building: it never fades (metres).")]
    public float maxWidth = 45f;
    [Tooltip("The ground in front of Ace, toward the camera, stays in view too: what stands between the camera and these two " +
             "points (metres from Ace, a metre up) fades as well. 0 switches a point off.")]
    public Vector2 ahead = new Vector2(6f, 12f);

    public const string DitherShaderName = SeeThroughMaterials.DitherShaderName;
    static readonly int SeeThroughId = SeeThroughMaterials.SeeThroughId;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // The café's own dressing (its entrance frame and brass pulls sit just outside the room) never fades:
    // Ace walks through that door, and the café has its own cut-away walls.
    static readonly string[] NeverFade = { "Hill", "City pavement", "Hazy skyline band", "12 - authored cafe interior" };

    sealed class Group
    {
        public string name;
        public Transform root;
        public Bounds bounds;
        public Renderer[] renderers;
        public Bounds[] boxes;
        public float alpha = 1f, clearFor;
        public bool blocking, worn;
        public Material[][] own;      // what each renderer wore when the fade began
        public Color[][] colours;     // _BaseColor per material slot, for the property block
    }

    CafeViewMode view;
    Shader dither;
    readonly List<Group> groups = new();
    readonly HashSet<string> everFaded = new();
    readonly HashSet<Transform> leftAlone = new();
    MaterialPropertyBlock block;
    readonly Vector3[] aim = new Vector3[7];
    int aims = 5;

    public int Groups => groups.Count;
    public int Renderers { get; private set; }
    /// <summary>Renderers in a building that can't turn see-through (their shader has no surface switch); they hide instead.</summary>
    public int HideInstead { get; private set; }
    /// <summary>Buildings see-through (or on their way) right now.</summary>
    public int FadedNow { get; private set; }
    public int MostAtOnce { get; private set; }
    public IReadOnlyCollection<string> EverFaded => everFaded;
    /// <summary>
    /// True while this renderer wears a see-through copy. Anything that would change its materials
    /// (a window going dark at bedtime) waits until it is back to its own: taking the copy off puts
    /// back what it wore when the fade began.
    /// </summary>
    public bool IsWorn(Renderer r) => r != null && wornRenderers.Contains(r);
    readonly HashSet<Renderer> wornRenderers = new();
    /// <summary>True when the dither shader was found (the see-through is dotted, not blended).</summary>
    public bool Dithered => dither != null;

    /// <summary>
    /// While <paramref name="leave"/> is true, the building never turns see-through (a house Ace is inside, which
    /// shows its own rooms: GraceHouse); if it is see-through now, it gets its own materials back at once.
    /// </summary>
    public void Leave(Transform building, bool leave)
    {
        if (building == null) return;
        if (!leave)
        {
            leftAlone.Remove(building);
            return;
        }
        leftAlone.Add(building);
        // Its own materials back at once, not after a fade: the house is about to hide parts of itself, and a dotted
        // copy left on a part hidden now would come back dotted when the house shows it again (the first walk check).
        foreach (var g in groups)
            if (g.worn && LeftAlone(g)) TakeOff(g);
    }

    bool LeftAlone(Group g)
    {
        if (leftAlone.Count == 0 || g.root == null) return false;
        foreach (Transform b in leftAlone)
            if (b != null && (g.root == b || g.root.IsChildOf(b))) return true;
        return false;
    }

    /// <summary>Find everything that can hide Ace. Call once, when the night begins.</summary>
    public void Build(CafeViewMode cafeView, Transform nightGroup)
    {
        view = cafeView;
        block ??= new MaterialPropertyBlock();
        // The scene's own reference (CafeViewMode keeps it for the build), or found by name (SeeThroughMaterials).
        dither = SeeThroughMaterials.Dither;
        groups.Clear();
        wornRenderers.Clear();
        Renderers = HideInstead = 0;

        // Never: Ace, anyone or anything that moves, the night's own objects (lamps, road works),
        // the café's cut-away walls and what hangs from its ceiling.
        var skipRoots = new List<Transform>();
        if (nightGroup != null) skipRoots.Add(nightGroup);
        if (view != null) skipRoots.Add(view.transform);
        foreach (var c in FindObjectsByType<CharacterController>(FindObjectsInactive.Include)) skipRoots.Add(c.transform);
        foreach (var a in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsInactive.Include)) skipRoots.Add(a.transform);
        foreach (var b in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include)) if (!b.isKinematic) skipRoots.Add(b.transform);
        foreach (var v in FindObjectsByType<PolygonNpcVisual>(FindObjectsInactive.Include)) skipRoots.Add(v.transform);
        foreach (var car in FindObjectsByType<CafeCar>(FindObjectsInactive.Include)) skipRoots.Add(car.transform);
        // Grace's rooms show themselves (GraceHouse: cut-away walls, the floor above hidden); only her house's shell
        // can turn see-through, while Ace is outside it.
        foreach (var rooms in FindObjectsByType<GraceHouse>(FindObjectsInactive.Include)) skipRoots.Add(rooms.transform);
        var skipRenderers = new HashSet<Renderer>();
        if (view != null)
        {
            foreach (var r in view.cutawayWalls) if (r != null) skipRenderers.Add(r);
            foreach (var r in view.overheadFixtures) if (r != null) skipRenderers.Add(r);
        }

        // Every candidate piece, and the box round each object's candidate pieces.
        var pieces = new List<MeshRenderer>();
        var subtree = new Dictionary<Transform, Bounds>();
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || skipRenderers.Contains(r)) continue;
            if (r.sharedMaterials.Length == 0 || Under(r.transform, skipRoots) || UnderNamed(r.transform, NeverFade)) continue;
            Bounds b = r.bounds;
            if (b.size.x > maxWidth || b.size.z > maxWidth) continue;   // ground, a road, a hillside
            pieces.Add(r);
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                if (subtree.TryGetValue(t, out Bounds s)) { s.Encapsulate(b); subtree[t] = s; }
                else subtree[t] = b;
            }
        }

        // Group the pieces by object.
        var byRoot = new Dictionary<Transform, List<MeshRenderer>>();
        foreach (var r in pieces)
        {
            Transform root = GroupRoot(r.transform, subtree);
            if (!byRoot.TryGetValue(root, out var list)) byRoot[root] = list = new List<MeshRenderer>();
            list.Add(r);
        }
        Rect cafe = CafeDaylight.CafeInside;
        foreach (var pair in byRoot)
        {
            Bounds all = pair.Value[0].bounds;
            foreach (var r in pair.Value) all.Encapsulate(r.bounds);
            if (all.max.y < minTop || Mathf.Min(all.size.x, all.size.z) < minThickness) continue;
            if (all.size.x > maxWidth || all.size.z > maxWidth) continue;
            if (cafe.Contains(new Vector2(all.center.x, all.center.z))) continue;   // the café room: its own cut-away walls
            var g = new Group
            {
                name = PathOf(pair.Key),
                root = pair.Key,
                bounds = all,
                renderers = pair.Value.ToArray(),
                boxes = new Bounds[pair.Value.Count],
            };
            for (int i = 0; i < g.renderers.Length; i++) g.boxes[i] = g.renderers[i].bounds;
            groups.Add(g);
            Renderers += g.renderers.Length;
            foreach (var r in g.renderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null && !CanSeeThrough(m)) { HideInstead++; break; }
        }
    }

    // What a copy can be made of: anything with an albedo to dot (POLYGON's or URP Lit's),
    // or, blended, anything with URP's surface switch.
    static bool CanSeeThrough(Material m) => SeeThroughMaterials.CanCopy(m);

    void LateUpdate()
    {
        bool looking = view != null && view.Following && view.OverheadShown && view.isometricCamera != null;
        if (looking) FindBlocking(); else foreach (var g in groups) g.blocking = false;

        float dt = Time.unscaledDeltaTime;
        float speed = (1f - seeThroughAlpha) / Mathf.Max(.01f, fadeSeconds);
        int faded = 0;
        foreach (var g in groups)
        {
            g.clearFor = g.blocking ? 0f : g.clearFor + dt;
            float target = g.blocking || g.clearFor < backAfter && g.alpha < 1f ? seeThroughAlpha : 1f;
            if (Mathf.Approximately(g.alpha, target) && (target < 1f) == g.worn) { if (g.worn) faded++; continue; }
            g.alpha = Mathf.MoveTowards(g.alpha, target, speed * dt);
            if (g.alpha < 1f)
            {
                if (!g.worn) Wear(g);
                Show(g);
                faded++;
                everFaded.Add(g.name);
            }
            else if (g.worn) TakeOff(g);
        }
        FadedNow = faded;
        if (faded > MostAtOnce) MostAtOnce = faded;
    }

    void FindBlocking()
    {
        Vector3 eye = view.isometricCamera.transform.position;
        Vector3 feet = view.AceFeet;
        Vector3 across = Vector3.Cross(Vector3.up, feet - eye);
        // Ace's sides: his capsule's radius (0.35 since playtest 3; these were 0.45 when it was 0.5).
        float side = PlayerMovement.CapsuleRadius;
        across = across.sqrMagnitude > 1e-6f ? across.normalized * side : Vector3.right * side;
        aim[0] = feet + Vector3.up * .4f;
        aim[1] = feet + Vector3.up * 1f;
        aim[2] = feet + Vector3.up * 1.7f;
        aim[3] = aim[1] + across;
        aim[4] = aim[1] - across;
        // The ground ahead of Ace on screen: toward the camera, along the ground.
        aims = 5;
        Vector3 toward = eye - feet;
        toward.y = 0f;
        if (toward.sqrMagnitude > 1e-4f)
        {
            toward.Normalize();
            if (ahead.x > 0f) aim[aims++] = feet + toward * ahead.x + Vector3.up;
            if (ahead.y > 0f) aim[aims++] = feet + toward * ahead.y + Vector3.up;
        }
        foreach (var g in groups)
        {
            g.blocking = false;
            if (LeftAlone(g) || !AnyHit(g.bounds, eye)) continue;
            foreach (var box in g.boxes)
                if (AnyHit(box, eye)) { g.blocking = true; break; }
        }
    }

    bool AnyHit(Bounds box, Vector3 eye)
    {
        for (int i = 0; i < aims; i++)
        {
            Vector3 target = aim[i];
            Vector3 d = target - eye;
            float length = d.magnitude;
            // Something right at Ace (a wall Ace stands against) counts; the ground under Ace doesn't reach this high.
            if (length > .01f && box.IntersectRay(new Ray(eye, d / length), out float hit) && hit < length - .15f) return true;
        }
        return false;
    }

    // ---------- the see-through copies ----------

    void Wear(Group g)
    {
        g.own = new Material[g.renderers.Length][];
        g.colours = new Color[g.renderers.Length][];
        for (int i = 0; i < g.renderers.Length; i++)
        {
            Renderer r = g.renderers[i];
            if (r == null) continue;
            Material[] own = r.sharedMaterials;
            g.own[i] = own;
            var wear = new Material[own.Length];
            var colours = new Color[own.Length];
            bool hide = false;
            for (int m = 0; m < own.Length; m++)
            {
                wear[m] = SeeThroughMaterials.Copy(own[m]);
                if (own[m] != null && wear[m] == null) hide = true;
                colours[m] = own[m] != null && own[m].HasProperty(BaseColorId) ? own[m].GetColor(BaseColorId) : Color.white;
            }
            g.colours[i] = colours;
            wornRenderers.Add(r);
            if (hide) { r.forceRenderingOff = true; continue; }   // a shader with no see-through: it hides instead
            r.sharedMaterials = wear;
        }
        g.worn = true;
    }

    void Show(Group g)
    {
        for (int i = 0; i < g.renderers.Length; i++)
        {
            Renderer r = g.renderers[i];
            if (r == null || r.forceRenderingOff || g.colours[i] == null) continue;
            if (dither != null)
            {
                // The dots: one value for the whole renderer.
                block.Clear();
                block.SetFloat(SeeThroughId, g.alpha);
                r.SetPropertyBlock(block);
                continue;
            }
            for (int m = 0; m < g.colours[i].Length; m++)
            {
                Color c = g.colours[i][m];
                c.a = g.alpha;
                block.Clear();
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block, m);
            }
        }
    }

    void TakeOff(Group g)
    {
        for (int i = 0; i < g.renderers.Length; i++)
        {
            Renderer r = g.renderers[i];
            if (r == null || g.own == null || g.own[i] == null) continue;
            wornRenderers.Remove(r);
            if (r.forceRenderingOff) { r.forceRenderingOff = false; continue; }
            r.sharedMaterials = g.own[i];   // its own materials first: nothing below can leave it see-through
            r.SetPropertyBlock(null);
            if (dither != null) continue;
            for (int m = 0; m < g.own[i].Length; m++)
            {
                try { r.SetPropertyBlock(null, m); }
                catch (ArgumentException) { block.Clear(); r.SetPropertyBlock(block, m); }
            }
        }
        g.own = null;
        g.colours = null;
        g.worn = false;
        g.alpha = 1f;
    }

    /// <summary>Every building back as it was: its own materials, no property blocks.</summary>
    public void Clear()
    {
        foreach (var g in groups) if (g.worn) TakeOff(g);
        FadedNow = 0;
    }

    void OnDisable() => Clear();

    // The copies are the Play session's (SeeThroughMaterials): the café's walls may still wear one
    // after the night ends, so nothing is destroyed here.
    void OnDestroy() => Clear();

    // ---------- grouping ----------

    Transform GroupRoot(Transform piece, Dictionary<Transform, Bounds> subtree)
    {
        for (Transform t = piece; t != null; t = t.parent)
            if (IsBuilding(t.name)) return t;
        Transform best = piece;
        for (Transform t = piece.parent; t != null; t = t.parent)
        {
            if (!subtree.TryGetValue(t, out Bounds b) || b.size.x > clusterWidth || b.size.z > clusterWidth) break;
            best = t;
        }
        return best;
    }

    static bool IsBuilding(string name) =>
        name.StartsWith("Building", StringComparison.OrdinalIgnoreCase)
        || name.IndexOf(" house", StringComparison.OrdinalIgnoreCase) >= 0;

    static bool Under(Transform t, List<Transform> roots)
    {
        foreach (var root in roots) if (root != null && t.IsChildOf(root)) return true;
        return false;
    }

    static bool UnderNamed(Transform t, string[] names)
    {
        for (Transform x = t; x != null; x = x.parent)
            foreach (var n in names) if (x.name == n) return true;
        return false;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform x = t; x != null && parts.Count < 3; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>For the night's notes.</summary>
    public string Describe() =>
        $"See-through ({(dither != null ? "dotted" : "blended: the dither shader was not found")}): " +
        $"{groups.Count} buildings and trees ({Renderers} pieces, {HideInstead} of them hide instead) can fade; " +
        $"{FadedNow} faded now, {MostAtOnce} at most at once, {everFaded.Count} different ones so far; {SeeThroughMaterials.Made} see-through materials.";
}
