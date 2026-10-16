using UnityEngine;

public enum ToolType { Hand, Brush, Screwdriver, Pry, Tweezers, Cloth }

// ---------------------------------------------------------------------------
// A TOOL ON THE REST, AND IN THE HAND (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.4.1-4.4.2;
// td-one-scale.md §6.7)
//
// His playtest 5 (14 Oct, 00:30): "i would prefer the cloth to actually open up and be used like a cloth when using the
// cloth, and i only mean like for example, when i point my crosshair at the point of the cloth, it should just grab and i just
// drag it across an object and for it to feel the impact ... it was so hard to use the cloth and or brush". The cloth used to
// trail the cursor on a spring, gathered into a pad by a scale trick, lifting off after each smudge. Now (his decision 4):
//   * pressed on at rest, a tool is IN THE HAND on that frame (tool.pick), its working tip (the model's origin) EXACTLY under
//     the cursor every frame: on the surface point, or over a dirt spot the cursor's ray on the spot's plane. No spring for
//     position: the tip never lags; only its turn and its shape ease (closed form, 14 a second), so a 15 fps frame jump is a
//     clean jump, never a run-away (13 Oct's stepped spring sent the cloth 7,000 km);
//   * held over its own dirt it presses down and rubs (ItemInspector, GrimeSpot's press pass); held elsewhere it hovers a
//     tenth of its contact's width off the surface;
//   * the cloth swaps to Art's held model (BT_ClothHeld, the pick-up motion hides the swap), lies flat on the surface with its
//     +x along the stroke's axis; Press = 1 on the device, 0.4 in the air; Bunch follows the stroke speed; at a reversal (the
//     speed near nothing, so no bunch to see) its x flips, and the bunch rises on the new leading edge: one key, both ways;
//   * the brush's bristles bend with the speed (Bend), its +x along the stroke, mirrored the same way, its handle tilting ±12°
//     against the motion;
//   * home is a short flight back to its seat on the rest (0.2 s, tool.down as it lands), never a teleport.
// ItemInspector decides when (the grab rule: a tap or a release over the device keeps it; elsewhere it goes home; it goes
// home by itself when its last spot on the device is clean).
//
// THE CLOTH SHOWS WHAT IT CLEANS (the Producer's call 23, 14 Oct 11:30; QA M3). With the wad's middle on the cursor, the
// 50 x 52 mm pressed pad covered the 19 mm lens spot on every frame of the wipe, so the smear thinning to haze and the clean
// trail (decision 4) were never seen: playtest 4's veteran said the same ("the smudge thinning and the gleam both happen
// under it"). Now the held wad sits with its far edge (up the screen, away from Ace) at the cursor (4 mm under it since
// 15 Oct: FarEdgeUnderTip): playtest 4's fold lead,
// in closed form, no lag. Only the held model moves (LeadWithFarEdge): the tool's tip, the footprint GrimeSpot cleans (centred
// on the cursor, ItemInspector.Rub) and every lab that reads the tip stay exactly where they were. The shift is worked out
// each frame from that frame's pose, its keys and its mirror, so a reversed stroke keeps it.
//
// THE LEAN (the TD's §6.7). Leaning 18° toward the camera, the driver's handle pointed within about 14° of the view ray and hid
// the screw it turned. Now a pointed tool's handle leans toward the screen's upper right (Ace's right hand) and stays at least
// 30° off the view ray, so a tip never hides its own target.
//
// THE PRY AT A PRY ANGLE (playtest 4; RemovablePart.PryPose, FollowPose): a spudger is held low, not stood on its tip.
// ---------------------------------------------------------------------------
public class ToolPickup : MonoBehaviour
{
    public ToolType tool;

    [Tooltip("Shown in the hover tooltip.")]
    public string displayName = "Tool";

    [Tooltip("Tint so tools are distinguishable at a glance (only applied to a lone primitive, never to a model).")]
    public Color tint = Color.white;

    [Tooltip("Degrees a pointed tool's handle stays off the view ray at least (the TD: 30, so the tip never hides its target).")]
    [SerializeField, Range(30f, 60f)] private float leanOffView = 34f;

    public enum Motion { None, Turn, Pinch, Lever, Stroke, Circle }

    public bool InHand { get; private set; }
    /// <summary>The cloth: pressed flat on the surface, no lean.</summary>
    public bool PressedFlat => tool == ToolType.Cloth;
    /// <summary>On its way home to its seat.</summary>
    public bool Returning { get; private set; }
    /// <summary>The cloth's and the brush's shape keys now, 0..1 (labs): Press, Bunch, Bend; and which way its x points (±1).</summary>
    public float PressShown => pressKey;
    public float BunchShown => bunchKey;
    public float BendShown => bendKey;
    public float Mirror => mirror;

    /// <summary>A tool's contact width (metres): the brush's chisel, the held cloth's pad (Art A4-A5); a pointed tool's tip.</summary>
    public float ContactWidth => tool == ToolType.Brush ? BenchRules.BrushContact : tool == ToolType.Cloth ? BenchRules.ClothContact : .003f;
    /// <summary>The height the tip hovers off a surface when not pressed: a tenth of its contact width (§4.4.2).</summary>
    public float HoverHeight => Mathf.Max(.0015f, .1f * ContactWidth);

    // The pry held low, as a spudger is: its handle this far up from the surface it is over, degrees (the seam's own angle is
    // RemovablePart.PryAngle).
    private const float PryHeld = 38f;
    // Rotation and shape keys ease at this rate (closed form): turns can lag, the tip never does.
    private const float TurnRate = 14f;

    private Vector3 baseScale;
    private Renderer rend;
    private bool legacyTint;
    private Transform homeParent;
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Transform leafL, leafR;
    private Quaternion leafLRest, leafRRest;
    private float spin, pinch, lever;
    private Collider[] colliders;
    private Vector3 heldScale = Vector3.one;
    private bool placedOnce;
    // The cloth's held model and its keys; the brush's bend.
    private GameObject heldCloth;
    private Vector3 heldLocalPos;          // where the held model sits under the tool (Bench 7), put back each frame before the lead
    private Quaternion heldLocalRot = Quaternion.identity;
    private Vector3[] clothVerts, clothPress, clothBunch;   // the held mesh's vertices and its keys' full deltas (its own space)
    private float clothPressPer = 1f, clothBunchPer = 1f;   // a key's 0..1 as a share of its last frame's delta
    private bool clothMeasured;
    private Renderer[] restRenderers;
    private SkinnedMeshRenderer keyed;
    private int pressIndex = -1, bunchIndex = -1, bendIndex = -1;
    private float pressKey, bunchKey, bendKey, mirror = 1f;
    private Vector3 strokeAxis;            // the stroke's axis on the surface (direction modulo 180°), eased
    private float brushTilt;
    private Bounds restBox = new Bounds(Vector3.zero, Vector3.one * .01f);

    private void Awake()
    {
        baseScale = transform.localScale;
        rend = GetComponent<Renderer>();
        MeshFilter filter = GetComponent<MeshFilter>();
        string mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "";
        legacyTint = rend != null && transform.childCount == 0 && rend.sharedMaterials.Length == 1
                     && (mesh == "Cylinder" || mesh == "Cube" || mesh == "Capsule" || mesh == "Sphere");
        if (legacyTint) rend.material.color = tint;
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        leafL = transform.Find("Leaf_L");
        leafR = transform.Find("Leaf_R");
        if (leafL != null) leafLRest = leafL.localRotation;
        if (leafR != null) leafRRest = leafR.localRotation;
        colliders = GetComponentsInChildren<Collider>(true);
        restBox = BenchStage.Measure(transform);
        // The cloth's held model (Bench 7 puts BT_ClothHeld under it, hidden): its keys Press and Bunch. The folded cloth's own
        // renderers are what hide while it is held.
        Transform held = transform.Find("BT_ClothHeld");
        if (held != null)
        {
            heldCloth = held.gameObject;
            heldLocalPos = held.localPosition;
            heldLocalRot = held.localRotation;
            var rest = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (!r.transform.IsChildOf(held)) rest.Add(r);
            restRenderers = rest.ToArray();
            keyed = held.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }
        else keyed = GetComponentInChildren<SkinnedMeshRenderer>(true);   // the brush's own (Bend)
        if (keyed != null && keyed.sharedMesh != null)
        {
            pressIndex = keyed.sharedMesh.GetBlendShapeIndex("Press");
            bunchIndex = keyed.sharedMesh.GetBlendShapeIndex("Bunch");
            bendIndex = keyed.sharedMesh.GetBlendShapeIndex("Bend");
        }
    }

    /// <summary>The old tinted stand-ins (one of Unity's primitives) stay on their rail; every modelled tool is held.</summary>
    public bool IsStandIn => legacyTint;

    /// <summary>Its box as it lies at rest, in its own frame (metres, scale kept in; measured as it woke on the rest): the
    /// picker's outline of it at rest, and "its own place" for the grab rule.</summary>
    public Bounds RestBox => restBox;
    /// <summary>Where it lies at home on the rest, in the world.</summary>
    public Pose HomePose => homeParent != null
        ? new Pose(homeParent.TransformPoint(homeLocalPos), homeParent.rotation * homeLocalRot)
        : new Pose(transform.position, transform.rotation);

    /// <summary>
    /// Into the hand (tool.pick on this frame; a flight home under way stops) or out of it (home by its flight, tool.down as it
    /// lands; after the last spot of the device is clean it lifts first, <paramref name="liftFirst"/>).
    /// </summary>
    public void SetSelected(bool on, bool liftFirst = false)
    {
        if (on && !InHand) Sfx.Play("tool.pick", transform.position);
        bool was = InHand;
        InHand = on;
        if (legacyTint)
        {
            transform.localScale = on ? baseScale * 1.25f : baseScale;
            if (rend != null) rend.material.color = on ? Color.Lerp(tint, Color.white, 0.5f) : tint;
        }
        // In the hand its colliders are off: the cursor's ray must reach the part under the tip, not the tool over it. On the
        // rest they're on, so it can be picked again.
        if (colliders != null) foreach (Collider c in colliders) if (c != null) c.enabled = !on;
        if (on)
        {
            BenchStage.CancelFlight(this);
            Returning = false;
            // Its size in the world as it lies at home (in the hand it has no parent to carry that).
            Vector3 parentScale = homeParent != null ? homeParent.lossyScale : Vector3.one;
            heldScale = Vector3.Scale(baseScale, parentScale);
            placedOnce = false;
            ShowHeldModel(true);
            BenchFeel.Pulse(.10f, .20f, .04f);
        }
        else if (was) GoHome(liftFirst);
    }

    // The cloth's held wad in the hand, its folded self at rest (Art A4; the swap is hidden by the motion).
    private void ShowHeldModel(bool held)
    {
        if (heldCloth == null) return;
        heldCloth.SetActive(held);
        if (!held) { heldCloth.transform.localPosition = heldLocalPos; heldCloth.transform.localRotation = heldLocalRot; }   // the lead undone
        if (restRenderers != null) foreach (Renderer r in restRenderers) if (r != null) r.enabled = !held;
    }

    // Home: a short flight to its seat (tool.down as it lands), a lift first after the last spot.
    private void GoHome(bool liftFirst)
    {
        if (legacyTint) return;
        Returning = true;
        pressKey = bunchKey = bendKey = 0f;
        ApplyKeys();
        Vector3 up = BenchStage.Instance != null ? BenchStage.Instance.MatUp : Vector3.up;
        ShowHeldModel(false);
        transform.localScale = heldScale;       // the mirror undone before it flies
        if (homeParent == null) { Returning = false; return; }
        BenchStage.SendTo(this, null, homeParent, homeLocalPos, homeLocalRot, BenchStage.FlightKind.Tool, BenchRules.ToolHomeSeconds,
            null, "tool.down", homeParent, Home, liftFirst ? .15f : 0f, liftFirst ? up * .03f : Vector3.zero);
    }

    private void Home()
    {
        Returning = false;
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        if (!legacyTint) transform.localScale = baseScale;
        spin = 0f;
        mirror = 1f;
    }

    // ---------- in the hand ----------

    /// <summary>
    /// One frame of the tool in the hand: its tip at <paramref name="tip"/> (a surface point; exactly, every frame), lifted by
    /// its hover height unless <paramref name="pressing"/>; its handle up <paramref name="normal"/>, leaned off the view ray (a
    /// pointed tool) or lying flat (the cloth); the cloth's and the brush's keys from the stroke (BenchFeel), pressing on its
    /// own dirt when <paramref name="rubbing"/>.
    /// </summary>
    public void HoldAt(Vector3 tip, Vector3 normal, Camera camera, bool pressing, bool rubbing, float deltaTime)
    {
        if (!InHand || legacyTint || Returning) return;
        if (transform.parent != null) transform.SetParent(null, true);
        float dt = Mathf.Max(0f, deltaTime);
        Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        Quaternion target;
        Vector3 velocity = StrokeOnSurface(camera, n);
        if (PressedFlat || tool == ToolType.Brush)
        {
            // The stroke's axis on the surface (direction modulo 180°, eased), and which way along it the hand is going.
            if (velocity.sqrMagnitude > 1e-6f)
            {
                Vector3 dir = velocity.normalized;
                if (strokeAxis == Vector3.zero) strokeAxis = dir;
                Vector3 want = Vector3.Dot(dir, strokeAxis) >= 0f ? dir : -dir;
                strokeAxis = Vector3.Slerp(strokeAxis, want, Ease(dt)).normalized;
                // At a reversal the mesh mirrors, so the bunch (or the bend) rises on the new leading edge: one key for both
                // ways. A hand's reversal passes the speed through nothing, so the key is already about 0 here; a 15 fps frame
                // can skip that moment, so the key restarts from 0 on the flip rather than showing the old edge's folds.
                float sign = Vector3.Dot(dir, strokeAxis) >= 0f ? 1f : -1f;
                if (sign != mirror) { mirror = sign; bunchKey = 0f; bendKey = 0f; }
            }
            Vector3 x = Vector3.ProjectOnPlane(strokeAxis == Vector3.zero ? (camera != null ? camera.transform.right : Vector3.right) : strokeAxis, n);
            if (x.sqrMagnitude < 1e-6f) x = Vector3.ProjectOnPlane(Vector3.right, n);
            x.Normalize();
            if (PressedFlat) target = Quaternion.LookRotation(Vector3.Cross(x, n), n);
            else
            {
                // The brush: its handle up, leaned off the view ray like the pointed tools, tilting up to 12° against the motion
                // (its top trailing, as the cloth's tail leans in Art's A4). A turn about +z takes the handle toward -x.
                Vector3 handle = Handle(n, camera);
                float tilt = pressing || rubbing ? 12f * BenchRules.Bend(BenchFeel.StrokeSpeed) * mirror : 0f;
                brushTilt = Mathf.Lerp(brushTilt, tilt, Ease(dt));
                Vector3 bx = Vector3.ProjectOnPlane(x, handle).normalized;
                Vector3 bz = Vector3.Cross(bx, handle);
                target = Quaternion.AngleAxis(brushTilt, bz) * Quaternion.LookRotation(bz, handle);
            }
        }
        else if (tool == ToolType.Pry)
        {
            // Held low, as a spudger is: the handle down to the right (the hand), the blade flat toward the surface.
            Vector3 side = Vector3.ProjectOnPlane(camera != null ? camera.transform.right : Vector3.right, n);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, n);
            target = PryRotation(n, side.normalized, PryHeld);
        }
        else
        {
            // A pointed tool: its handle leaned off the view ray toward the screen's upper right; its flat side to the camera.
            Vector3 handle = Handle(n, camera);
            Vector3 toCamera = camera != null ? (camera.transform.position - tip).normalized : Vector3.back;
            Vector3 face = Vector3.ProjectOnPlane(toCamera, handle);
            target = face.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(face.normalized, handle) : Quaternion.FromToRotation(Vector3.up, handle);
        }
        target *= MotionRotation();
        bool down = pressing || rubbing;
        Vector3 position = tip + n * (down ? 0f : HoverHeight);
        if (!placedOnce) { transform.rotation = target; placedOnce = true; }
        transform.SetPositionAndRotation(position, Quaternion.Slerp(transform.rotation, target, Ease(dt)));
        transform.localScale = new Vector3(heldScale.x * ((PressedFlat || tool == ToolType.Brush) ? mirror : 1f), heldScale.y, heldScale.z);

        // The keys (Art A4-A5): the cloth's Press (pressed on the device 1, in the air 0.4) and Bunch (the stroke's speed); the
        // brush's Bend (the speed, while pressed). In the air nothing drags, so nothing bunches or bends.
        float wantPress = down ? 1f : .4f;
        float wantBunch = down ? BenchRules.Bunch(BenchFeel.StrokeSpeed) : 0f;
        float wantBend = down ? BenchRules.Bend(BenchFeel.StrokeSpeed) : 0f;
        float k = Ease(dt);
        pressKey = Mathf.Lerp(pressKey, wantPress, k);
        bunchKey = Mathf.Lerp(bunchKey, wantBunch, k);
        bendKey = Mathf.Lerp(bendKey, wantBend, k);
        ApplyKeys();
        if (PressedFlat) LeadWithFarEdge(tip, n, camera);
    }

    // ---------- the cloth shows what it cleans (call 23; the header) ----------

    /// <summary>
    /// The held wad moved toward Ace along the surface, so its far edge on screen (its highest point up the screen, the tail
    /// and the folds included, at this frame's keys and mirror) sits at <paramref name="tip"/>, the cursor's point. Closed form:
    /// worked out from where it sits this frame, never eased, so it can't lag or overshoot at 15 fps.
    /// </summary>
    private void LeadWithFarEdge(Vector3 tip, Vector3 n, Camera camera)
    {
        if (heldCloth == null || !heldCloth.activeSelf) return;
        Transform held = heldCloth.transform;
        held.localPosition = heldLocalPos;   // measured from where it sits on the tip
        held.localRotation = heldLocalRot;
        if (camera == null) return;
        Vector3 ray = tip - camera.transform.position;
        if (ray.sqrMagnitude < 1e-8f) return;
        ray.Normalize();
        // Up the screen at the tip (square to the line of sight), and that way along the surface (away from Ace).
        Vector3 up = camera.transform.up - Vector3.Dot(camera.transform.up, ray) * ray;
        if (up.sqrMagnitude < 1e-8f) return;
        up.Normalize();
        Vector3 along = Vector3.ProjectOnPlane(up, n);
        if (along.sqrMagnitude < 1e-6f) return;
        along.Normalize();
        float rise = Vector3.Dot(along, up);   // how far up the screen a step along the surface goes
        if (rise < .2f) return;                // looking almost straight down the normal: there is no "far" side to lead with
        float far = FarEdge(tip, up) + FarEdgeUnderTip;
        if (far > 0f) held.position -= along * (far / rise);
    }

    // The far edge sits this far under the cursor (screen-up metres at the tip; 15 Oct). At the cursor itself, the bunch
    // rising on the leading edge and a mouse's strokes a little above the lens's middle left 26-30 % of Grace's lens in sight
    // over the wipe (Bench 8 on his Mac; the call's bar is 40 %; a pad's strokes, through the middle, 50 %). 4 mm is under half
    // the lens spot's radius: the cloth's top edge still runs through the stroke the footprint takes.
    private const float FarEdgeUnderTip = .004f;

    // How far up the screen (along `up`, metres) the held wad reaches past `from` as it sits now: its vertices at this frame's
    // Press and Bunch, through its own transform (the mirror included). A mesh that can't be read falls back to its bounds.
    private float FarEdge(Vector3 from, Vector3 up)
    {
        if (!clothMeasured) MeasureCloth();
        if (keyed == null || clothVerts == null)
        {
            // No readable keyed mesh: the held model's drawn boxes (a little more than its outline; never less).
            float most = 0f;
            foreach (Renderer r in heldCloth.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponent<BenchOutlineHull>() != null) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    most = Mathf.Max(most, Vector3.Dot(corner - from, up));
                }
            }
            return most;
        }
        // up taken into the mesh's own space through the transpose of its matrix (scale and mirror kept): one dot a vertex.
        Matrix4x4 m = keyed.transform.localToWorldMatrix;
        var local = new Vector3(m.m00 * up.x + m.m10 * up.y + m.m20 * up.z,
                                m.m01 * up.x + m.m11 * up.y + m.m21 * up.z,
                                m.m02 * up.x + m.m12 * up.y + m.m22 * up.z);
        float press = pressKey * clothPressPer, bunch = bunchKey * clothBunchPer;
        float best = float.NegativeInfinity;
        for (int i = 0; i < clothVerts.Length; i++)
        {
            float d = Vector3.Dot(clothVerts[i], local);
            if (clothPress != null) d += press * Vector3.Dot(clothPress[i], local);
            if (clothBunch != null) d += bunch * Vector3.Dot(clothBunch[i], local);
            if (d > best) best = d;
        }
        return best + Vector3.Dot(keyed.transform.position - from, up);
    }

    // Once: the held mesh's vertices and its two keys' deltas (Bench tools 1 makes the keyed meshes readable).
    private void MeasureCloth()
    {
        clothMeasured = true;
        Mesh mesh = keyed != null ? keyed.sharedMesh : null;
        if (mesh == null || !mesh.isReadable) return;
        clothVerts = mesh.vertices;
        clothPress = KeyDeltas(mesh, pressIndex, clothVerts.Length, out clothPressPer);
        clothBunch = KeyDeltas(mesh, bunchIndex, clothVerts.Length, out clothBunchPer);
    }

    // A key's deltas at its last frame, and what share of them a key at 1 (a weight of 100) gives.
    private static Vector3[] KeyDeltas(Mesh mesh, int shape, int count, out float perUnit)
    {
        perUnit = 1f;
        if (shape < 0 || shape >= mesh.blendShapeCount) return null;
        int frames = mesh.GetBlendShapeFrameCount(shape);
        if (frames < 1) return null;
        var deltas = new Vector3[count];
        mesh.GetBlendShapeFrameVertices(shape, frames - 1, deltas, new Vector3[count], new Vector3[count]);
        float weight = mesh.GetBlendShapeFrameWeight(shape, frames - 1);
        perUnit = weight > 1e-3f ? 100f / weight : 1f;
        return deltas;
    }

    /// <summary>The old follow (the labs' and older callers'): the tip on a point, pressing when working.</summary>
    public void Follow(Vector3 point, Vector3 normal, Camera camera, bool working, float deltaTime)
        => HoldAt(point, normal, camera, working, false, deltaTime);

    /// <summary>
    /// Where the tool goes this frame when the work decides its angle (the pry tool at a cover's seam, playtest 4): its tip
    /// at <paramref name="tip"/> exactly, its handle along <paramref name="handle"/>, its flat side's normal (+Z) toward
    /// <paramref name="flat"/>; hovering off <paramref name="surfaceNormal"/> unless a hold is on; the lever on top.
    /// </summary>
    public void FollowPose(Vector3 tip, Vector3 handle, Vector3 flat, Vector3 surfaceNormal, Camera camera, bool working, float deltaTime)
    {
        if (!InHand || legacyTint || Returning) return;
        if (transform.parent != null) transform.SetParent(null, true);
        Vector3 h = handle.sqrMagnitude > 1e-6f ? handle.normalized : Vector3.up;
        Vector3 f = Vector3.ProjectOnPlane(flat, h);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(camera != null ? camera.transform.position - tip : Vector3.back, h);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, h);
        Vector3 n = surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.up;
        Quaternion target = Quaternion.LookRotation(f.normalized, h) * MotionRotation();
        if (!placedOnce) { transform.rotation = target; placedOnce = true; }
        transform.SetPositionAndRotation(tip + (working ? Vector3.zero : n * HoverHeight), Quaternion.Slerp(transform.rotation, target, Ease(deltaTime)));
        transform.localScale = heldScale;
    }

    // A pointed tool's handle: up the surface's normal, leaned toward the screen's upper right (Ace's right hand), and never
    // within leanOffView of the line to the camera (the TD's §6.7: the handle hid the screw it turned).
    private Vector3 Handle(Vector3 n, Camera camera)
    {
        if (camera == null) return n;
        Vector3 upperRight = Vector3.ProjectOnPlane(camera.transform.right + camera.transform.up, n);
        Vector3 handle = upperRight.sqrMagnitude > 1e-6f ? (n * Mathf.Cos(30f * Mathf.Deg2Rad) + upperRight.normalized * Mathf.Sin(30f * Mathf.Deg2Rad)).normalized : n;
        Vector3 toCamera = -camera.transform.forward;
        float angle = Vector3.Angle(handle, toCamera);
        if (angle < leanOffView)
        {
            Vector3 axis = Vector3.Cross(toCamera, handle);
            if (axis.sqrMagnitude < 1e-8f) axis = camera.transform.right;
            handle = Quaternion.AngleAxis(leanOffView - angle, axis.normalized) * handle;
        }
        return handle.normalized;
    }

    // The stroke's velocity on the surface (world), from the pointer's smoothed velocity on screen (BenchFeel).
    private static Vector3 StrokeOnSurface(Camera camera, Vector3 n)
    {
        Vector2 v = BenchFeel.StrokeVelocity;
        if (camera == null || v.sqrMagnitude < 400f) return Vector3.zero;        // under 20 px/s: no direction to follow
        Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, n).normalized;
        Vector3 up = Vector3.ProjectOnPlane(camera.transform.up, n).normalized;
        return right * v.x + up * v.y;
    }

    private static float Ease(float dt) => 1f - Mathf.Exp(-TurnRate * Mathf.Max(0f, dt));

    private void ApplyKeys()
    {
        if (keyed == null) return;
        if (pressIndex >= 0) keyed.SetBlendShapeWeight(pressIndex, pressKey * 100f);
        if (bunchIndex >= 0) keyed.SetBlendShapeWeight(bunchIndex, bunchKey * 100f);
        if (bendIndex >= 0) keyed.SetBlendShapeWeight(bendIndex, bendKey * 100f);
    }

    // The pry's rotation for a handle that rises `degrees` from a surface (normal n) toward `across` (in the surface): its
    // blade's width along the surface, across `across`; its +Z (the flat side) out and down, so the lever (about its X)
    // turns the handle down toward the surface and the blade's edge up.
    private static Quaternion PryRotation(Vector3 n, Vector3 across, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        Vector3 handle = n * Mathf.Sin(a) + across * Mathf.Cos(a);
        Vector3 flat = across * Mathf.Sin(a) - n * Mathf.Cos(a);
        return Quaternion.LookRotation(flat.normalized, handle.normalized);
    }

    /// <summary>The tool's motion while a hold is on. direction: +1 outward / forward, -1 inward / back.</summary>
    public void Animate(Motion motion, float progress, float direction, float deltaTime)
    {
        switch (motion)
        {
            case Motion.Turn:   spin += -direction * 420f * deltaTime; break;              // counter-clockwise backs a screw out
            case Motion.Pinch:  pinch = Mathf.MoveTowards(pinch, 1f, deltaTime / .12f); break;
            case Motion.Lever:  lever = Mathf.Lerp(0f, 16f, progress); break;
        }
        ApplyLeaves();
    }

    /// <summary>No hold: the motions ease back to rest.</summary>
    public void Rest()
    {
        pinch = Mathf.MoveTowards(pinch, 0f, Time.deltaTime / .1f);
        lever = Mathf.MoveTowards(lever, 0f, Time.deltaTime * 90f);
        ApplyLeaves();
    }

    private Quaternion MotionRotation()
    {
        Quaternion r = Quaternion.identity;
        if (tool == ToolType.Screwdriver) r = Quaternion.AngleAxis(spin, Vector3.up);
        else if (tool == ToolType.Pry) r = Quaternion.AngleAxis(lever, Vector3.right);      // the blade is flat along X: it levers about X
        return r;
    }

    private void ApplyLeaves()
    {
        // The tweezers' leaves close about the heel (their origin), each toward the other, until the points meet.
        if (leafL != null) leafL.localRotation = leafLRest * Quaternion.AngleAxis(1.1f * pinch, Vector3.forward);
        if (leafR != null) leafR.localRotation = leafRRest * Quaternion.AngleAxis(-1.1f * pinch, Vector3.forward);
    }
}
