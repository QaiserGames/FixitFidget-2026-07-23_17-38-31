using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BENCH'S STAGE: THE REPAIR MAT, ITS PLACES, AND THE ONE FLIGHT EVERY PIECE TAKES (the forgiving bench, 14 Oct 2026;
// specs/systems-forgiving-bench.md §4.0; contracts §2)
//
// His playtest 5 (14 Oct, 00:30): "moving it and putting them on the tray literally seems sooo hard, i kept dropping a part
// of the camera under the camera, idk it was going through the table, through the item" and "the items that are being repairs
// to automatically fall in to the tray to be used or reused". His decision 1 (05:59): taking apart stays physical and under
// your hand; everything after that is automatic: each freed piece flies home on a scripted arc and lands with its sound.
//
// So nothing at the bench is a rigidbody any more (rule 7: no physics in the core loop). The mat (Art's BM_RepairMat, or a
// stand-in with the same empties until the FBX lands) carries the places every piece goes to:
//   * six screw cups (Cup_1..6): one screw each, in the order they come out, never a cup the device covers at rest (§4.0.1's
//     slot rule: clear of all four resting quarter turns of its own device, by 6 mm; the phone's quarter turn covers the left
//     column, so its screws take cups 4-6, computed, not written down);
//   * the new-part pad (Pad_Small, Pad_Large): the fresh part waits on the smaller one it fits;
//   * the cover well (Well_Small, Well_Large): a cover lies in the smaller one it fits, outer face up;
//   * the old-parts tray (BT_ScrapTray, Slot_1..3), standing behind the device: broken parts, flat;
//   * the tool rest (BT_ToolRest, Seat_*): the five tools lie on it.
// Per device the rest and the tray move to the device's own layout (DeviceDefinition.benchLayout) when its close-up opens.
//
// THE FLIGHT (§4.0.2). One routine, advanced here in Update on game time (pausing freezes it; the clock runs, as locked; no
// coroutine per piece): an optional beat and a short lift or pop, then a quadratic arc whose middle rises 0.35 of the flat
// distance, timed by its length on screen (0.30-0.55 s; a screw's 0.22-0.35 s), eased (a quick launch, a gentle landing),
// turning from its launch pose to its landing pose (a screw tumbles one and a half times; a broken part wobbles), its launch
// cue on its first frame and its landing cue on its last (never a physics contact), then a little hop. The pose is a pure
// function of the time since launch, so 15 fps and 60 fps put it in the same place at the same moment, and the last frame
// sets the exact landing pose. A flight never blocks input: the next press can start while a screw is still in the air. In
// edit mode (the editor's checks: no frames) every flight completes at once, with the same states and the same places.
//
// THE GUARD (§4.0.4). A piece resting in its place that is found more than 2 mm from it, or anything of the job's 25 cm under
// the mat, is set back and logs an error: under this design it can't happen, so every lab fails on it.
//
// Kept from the v2 stage for the code that still reads it (DayIntro's bench shots, the repair bar's move): Mat (the mat's
// frame), Tray (the old-parts tray), Caddy (the tool rest) and ToolSlot. Built by Fixit Fidget > Bench > Bench 7 - The
// forgiving stage (ForgivingBenchSteps).
// ---------------------------------------------------------------------------
public class BenchStage : MonoBehaviour
{
    [Header("The repair mat and its places (Bench 7 wires them; contracts §2)")]
    [Tooltip("The mat's root: converts mat millimetres (Art's frame: +x the player's right, +y away from him) to the world.")]
    public Transform matFrame;
    [Tooltip("Cup_1..6: one screw each, numbered near to far, left column first.")]
    public Transform[] cups = new Transform[0];
    public Transform padSmall, padLarge, wellSmall, wellLarge;
    [Tooltip("The old-parts tray (BT_ScrapTray); its slots are Slot_1..3.")]
    public Transform scrapTray;
    public Transform[] scrapSlots = new Transform[0];
    [Tooltip("The tool rest (BT_ToolRest); its seats, in ToolType order after Hand: brush, screwdriver, pry, tweezers, cloth.")]
    public Transform toolRest;
    public Transform[] toolSlots = new Transform[0];

    [Header("The v2 stage (kept while the scene still has it: before Bench 7)")]
    [SerializeField] private Transform mat;
    [SerializeField] private Transform tray;
    [SerializeField] private Transform caddy;

    /// <summary>The mat's top above its origin (its bottom, on the bench top), metres.</summary>
    public const float MatTop = .005f;
    /// <summary>The guard: a piece this far under the mat has got away; a piece this far from its place has been moved.</summary>
    public const float FallLimit = .25f, SlotDrift = .002f;

    public static BenchStage Instance { get; private set; }

    /// <summary>The mat (its frame; the v2 slab before Bench 7).</summary>
    public Transform Mat => matFrame != null ? matFrame : mat;
    /// <summary>Where removed broken parts go: the old-parts tray (the v2 parts tray before Bench 7). Screws go to the cups.</summary>
    public Transform Tray => scrapTray != null ? scrapTray : tray;
    /// <summary>The tools' home: the tool rest (the v2 caddy before Bench 7).</summary>
    public Transform Caddy => toolRest != null ? toolRest : caddy;
    /// <summary>The device worked on now (its close-up is open), or null.</summary>
    public JobBase Current { get; private set; }
    /// <summary>The layout the stage is set for now.</summary>
    public BenchLayout CurrentLayout { get; private set; } = BenchLayout.Fallback;

    public Transform ToolSlot(ToolType tool)
    {
        int index = (int)tool - 1;
        Transform slot = toolSlots != null && index >= 0 && index < toolSlots.Length ? toolSlots[index] : null;
        return slot != null ? slot : Caddy;
    }

    // ================================================================== the mat's frame

    // The frame mat millimetres are measured in: the mat's root (Art's FBX or its stand-in: origin at the bottom middle, +x the
    // player's right, +z away), or before Bench 7 the v2 slab's bottom middle.
    void Frame(out Vector3 origin, out Quaternion rotation)
    {
        if (matFrame != null) { origin = matFrame.position; rotation = matFrame.rotation; return; }
        if (mat != null) { origin = mat.position - mat.up * (Mathf.Abs(mat.lossyScale.y) * .5f); rotation = mat.rotation; return; }
        origin = transform.position; rotation = transform.rotation;
    }

    /// <summary>A point on the mat in the world: <paramref name="mm"/> in Art's mat millimetres, <paramref name="heightMetres"/>
    /// above the mat's origin (its bottom, on the bench top; its top is at MatTop).</summary>
    public Vector3 MatToWorld(Vector2 mm, float heightMetres = 0f)
    {
        Frame(out Vector3 origin, out Quaternion rotation);
        return origin + rotation * new Vector3(mm.x * .001f, heightMetres, mm.y * .001f);
    }

    /// <summary>A world point in mat millimetres (its height is dropped).</summary>
    public Vector2 WorldToMat(Vector3 world)
    {
        Frame(out Vector3 origin, out Quaternion rotation);
        Vector3 local = Quaternion.Inverse(rotation) * (world - origin);
        return new Vector2(local.x * 1000f, local.z * 1000f);
    }

    /// <summary>The mat's up, its +x (the player's right) and its +y (away from him), in the world.</summary>
    public Vector3 MatUp { get { Frame(out _, out Quaternion r); return r * Vector3.up; } }
    public Vector3 MatRight { get { Frame(out _, out Quaternion r); return r * Vector3.right; } }
    public Vector3 MatAway { get { Frame(out _, out Quaternion r); return r * Vector3.forward; } }
    /// <summary>The mat's turn in the world (a piece lying square on it).</summary>
    public Quaternion MatRotation { get { Frame(out _, out Quaternion r); return r; } }

    // ================================================================== the places (§4.0.1)

    public enum SlotKind { Cup, Pad, Well, Scrap }

    /// <summary>What a piece of the bench is doing (§4.0.3). None is ever a rigidbody.</summary>
    public enum PieceState { OnDevice, Flying, InSlot, Hovering, Pressing }

    // The bays' room around each empty (half sizes, mm; Art A1: the pad 80 x 166 at x 90..170, the well 80 x 166 at x 182..262;
    // each small place is its bay's near quarter).
    static readonly Vector2 PadSmallRoom = new Vector2(26f, 24f), PadLargeRoom = new Vector2(40f, 83f);
    static readonly Vector2 WellSmallRoom = new Vector2(24f, 24f), WellLargeRoom = new Vector2(40f, 83f);
    const float CupRadiusMm = 9f;

    sealed class Claim
    {
        public JobBase job;
        public Component piece;
        public Transform slot;
        public SlotKind kind;
        public int stack;
        public bool landed;
        public Vector3 restLocal;       // its resting pose in the slot's frame, once landed (the guard's measure)
        public Quaternion restLocalRot;
    }

    readonly List<Claim> claims = new List<Claim>();
    readonly Dictionary<JobBase, BenchRules.MatBox> footprints = new Dictionary<JobBase, BenchRules.MatBox>();
    readonly Dictionary<JobBase, int> openedOrder = new Dictionary<JobBase, int>();
    readonly Dictionary<JobBase, List<Component>> putAway = new Dictionary<JobBase, List<Component>>();
    int openCount;

    // A piece's own box in its own frame (metres, its scale kept in, so a rotation alone turns its corners), measured once.
    readonly Dictionary<Component, Bounds> boxes = new Dictionary<Component, Bounds>();

    /// <summary>
    /// The close-up opened on <paramref name="job"/>: the tool rest and the old-parts tray move to its layout, its footprint
    /// (half sizes, mm, lying square with its long side up the view) is remembered for the slot rule, other jobs' pieces lying
    /// where it lies move to free places, and its own stock put away earlier comes back.
    /// </summary>
    public void BeginJob(JobBase job, BenchLayout layout, Vector2 footprintHalfMm)
    {
        if (job == null) return;
        Prune();
        Current = job;
        CurrentLayout = layout != null && layout.IsSet ? layout : BenchLayout.Fallback;
        footprints[job] = new BenchRules.MatBox(CurrentLayout.rest.x, CurrentLayout.rest.y, Mathf.Max(5f, footprintHalfMm.x), Mathf.Max(5f, footprintHalfMm.y));
        openedOrder[job] = ++openCount;
        PlaceForDevice(CurrentLayout);
        MakeRoomFor(job);
        BringBackStock(job);
    }

    /// <summary>The close-up on <paramref name="job"/> came down: flights to its device finish at once (it may be carried off),
    /// and a fresh part waiting over its seat goes back to its pad.</summary>
    public void EndJob(JobBase job)
    {
        if (job == null) return;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            if (f.job == job && f.toDevice) Complete(f);
        }
        if (Current == job) Current = null;
    }

    // The tool rest and the old-parts tray stand where this device's layout puts them (Art A1, the Blender lead's step 3): on
    // the mat's top, square to it. Only Bench 7's pieces move (the v2 caddy and tray stay where they were built).
    void PlaceForDevice(BenchLayout layout)
    {
        Quaternion square = MatRotation;
        if (toolRest != null)
            toolRest.SetPositionAndRotation(MatToWorld(new Vector2(layout.toolRestCentreX, layout.toolRestCentreY), MatTop), square);
        if (scrapTray != null)
            scrapTray.SetPositionAndRotation(MatToWorld(layout.scrapTray, MatTop), square);
    }

    /// <summary>The device's footprint at rest for the slot rule (mat mm), or false when the job never opened here.</summary>
    public bool FootprintOf(JobBase job, out BenchRules.MatBox box) => footprints.TryGetValue(job, out box);

    // Every place of a kind, in the order they're taken.
    Transform[] PlacesOf(SlotKind kind)
    {
        switch (kind)
        {
            case SlotKind.Cup: return cups ?? new Transform[0];
            case SlotKind.Pad: return new[] { padSmall, padLarge };
            case SlotKind.Well: return new[] { wellSmall, wellLarge };
            default: return scrapSlots ?? new Transform[0];
        }
    }

    Vector2 RoomOf(Transform slot)
    {
        if (slot == padSmall) return PadSmallRoom;
        if (slot == padLarge) return PadLargeRoom;
        if (slot == wellSmall) return WellSmallRoom;
        if (slot == wellLarge) return WellLargeRoom;
        return new Vector2(CupRadiusMm, CupRadiusMm);
    }

    // What lies at a place, as a box on the mat: a cup is its cup; anything else is the piece's own footprint lying there.
    BenchRules.MatBox BoxAt(Transform slot, SlotKind kind, Vector2 pieceHalfMm)
    {
        Vector2 at = WorldToMat(slot.position);
        return kind == SlotKind.Cup ? new BenchRules.MatBox(at.x, at.y, CupRadiusMm, CupRadiusMm) : new BenchRules.MatBox(at.x, at.y, pieceHalfMm.x, pieceHalfMm.y);
    }

    bool Taken(Transform slot, Component except = null)
    {
        foreach (Claim c in claims) if (c.slot == slot && c.piece != except && c.piece != null) return true;
        return false;
    }

    bool ClearOf(JobBase job, BenchRules.MatBox box)
    {
        if (job != null && footprints.TryGetValue(job, out BenchRules.MatBox device) && !BenchRules.ClearOfDevice(box, device)) return false;
        // The device in the close-up now, if it isn't this piece's own (another job's pieces must not lie under it either).
        if (Current != null && Current != job && footprints.TryGetValue(Current, out BenchRules.MatBox now) && !BenchRules.ClearOfDevice(box, now)) return false;
        return true;
    }

    /// <summary>
    /// The place a piece of <paramref name="job"/> of this kind takes now (§4.0.1): the first free one of its kind that it fits
    /// and that lies clear of its device's resting quarter turns; a fresh part on a full pad puts away the stock of the job
    /// opened longest ago; with nothing free, a cup stacks on its job's last cup and the old-parts tray on its last slot.
    /// Null when the stage has no place of that kind (a scene without the stage, an editor check).
    /// </summary>
    Claim ClaimFor(JobBase job, Component piece, SlotKind kind, Vector2 pieceHalfMm)
    {
        Claim existing = ClaimOf(piece);
        if (existing != null) claims.Remove(existing);
        Transform[] places = PlacesOf(kind);
        Transform chosen = null;
        foreach (Transform slot in places)
        {
            if (slot == null || Taken(slot)) continue;
            if ((kind == SlotKind.Pad || kind == SlotKind.Well) && !BenchRules.Fits(new BenchRules.MatBox(0f, 0f, pieceHalfMm.x, pieceHalfMm.y), RoomOf(slot).x, RoomOf(slot).y)) continue;
            if (!ClearOf(job, BoxAt(slot, kind, pieceHalfMm))) continue;
            chosen = slot;
            break;
        }
        if (chosen == null && kind == SlotKind.Pad) chosen = PadByPuttingAwayStock(job, pieceHalfMm);
        int stack = 0;
        if (chosen == null)
        {
            // Nothing free: the last place this job already has of the kind (a cup stacks: §4.0.1's fallback), else the biggest
            // place of the kind that lies clear of the device, else the last one.
            Claim mine = null;
            foreach (Claim c in claims) if (c.job == job && c.kind == kind && c.piece != null) mine = c;
            if (mine != null) chosen = mine.slot;
            else
            {
                for (int i = places.Length - 1; i >= 0 && chosen == null; i--)
                    if (places[i] != null && ClearOf(job, BoxAt(places[i], kind, pieceHalfMm))) chosen = places[i];
                if (chosen == null) for (int i = places.Length - 1; i >= 0 && chosen == null; i--) if (places[i] != null) chosen = places[i];
            }
            if (chosen != null) foreach (Claim c in claims) if (c.slot == chosen && c.piece != null) stack = Mathf.Max(stack, c.stack + 1);
            if (chosen != null && kind != SlotKind.Scrap && kind != SlotKind.Cup)
                Debug.LogWarning($"[Bench stage] No free {kind.ToString().ToLowerInvariant()} that {Name(piece)} fits ({pieceHalfMm.x * 2f:0} x {pieceHalfMm.y * 2f:0} mm): " +
                                 $"it lies on {chosen.name}{(stack > 0 ? $", stacked {stack} up" : "")} (a device not yet at real size, or a bay too small: the TD's audit).");
        }
        if (chosen == null) return null;
        var claim = new Claim { job = job, piece = piece, slot = chosen, kind = kind, stack = stack };
        claims.Add(claim);
        return claim;
    }

    // Both pads hold other jobs' stock: the stock of the job opened longest ago is put away (switched off) and comes back when its
    // close-up reopens (§4.0.1).
    Transform PadByPuttingAwayStock(JobBase job, Vector2 pieceHalfMm)
    {
        Claim oldest = null;
        int oldestOrder = int.MaxValue;
        foreach (Claim c in claims)
        {
            if (c.kind != SlotKind.Pad || c.job == job || c.piece == null) continue;
            if (!BenchRules.Fits(new BenchRules.MatBox(0f, 0f, pieceHalfMm.x, pieceHalfMm.y), RoomOf(c.slot).x, RoomOf(c.slot).y)) continue;
            int order = c.job != null && openedOrder.TryGetValue(c.job, out int o) ? o : 0;
            if (order < oldestOrder) { oldestOrder = order; oldest = c; }
        }
        if (oldest == null) return null;
        claims.Remove(oldest);
        if (!putAway.TryGetValue(oldest.job, out List<Component> list)) putAway[oldest.job] = list = new List<Component>();
        list.Add(oldest.piece);
        oldest.piece.gameObject.SetActive(false);
        return oldest.slot;
    }

    void BringBackStock(JobBase job)
    {
        if (!putAway.TryGetValue(job, out List<Component> list)) return;
        putAway.Remove(job);
        foreach (Component piece in list)
        {
            if (piece == null) continue;
            piece.gameObject.SetActive(true);
            if (piece is LoosePart fresh) fresh.BackOnItsPad();
        }
    }

    // Other jobs' pieces lying where this device lies (their places were chosen against their own devices) move to free places.
    void MakeRoomFor(JobBase job)
    {
        if (!footprints.TryGetValue(job, out BenchRules.MatBox device)) return;
        var moving = new List<Claim>();
        foreach (Claim c in claims)
        {
            if (c.job == job || c.piece == null || c.slot == null || !c.landed || c.kind == SlotKind.Scrap) continue;   // the tray moves with its contents
            Vector2 half = HalfMm(c.piece, c.kind);
            if (!BenchRules.ClearOfDevice(BoxAt(c.slot, c.kind, half), device)) moving.Add(c);
        }
        foreach (Claim c in moving)
        {
            Transform from = c.slot;
            Claim next = ClaimFor(c.job, c.piece, c.kind, HalfMm(c.piece, c.kind));
            if (next == null || next.slot == from) continue;
            Pose rest = RestPose(next, LieUpOf(c.piece));
            StartFlight(new Flight
            {
                piece = c.piece.transform, job = c.job, claim = next, speed = FlightKind.Part,
                toWorld = rest, launchCue = null, landCue = null, hop = 0f,
            });
        }
    }

    /// <summary>The claim a piece holds, or null.</summary>
    Claim ClaimOf(Component piece)
    {
        if (piece == null) return null;
        foreach (Claim c in claims) if (c.piece == piece) return c;
        return null;
    }

    /// <summary>The piece leaves its place (fetched back, flown home, seated, gone): the place is free again.</summary>
    public void Release(Component piece)
    {
        Claim c = ClaimOf(piece);
        if (c != null) claims.Remove(c);
    }

    /// <summary>The place a piece holds, or null (labs, the glow).</summary>
    public Transform PlaceOf(Component piece) => ClaimOf(piece)?.slot;

    /// <summary>True when the piece rests in its place (landed, not flying).</summary>
    public bool InPlace(Component piece) { Claim c = ClaimOf(piece); return c != null && c.landed && !IsFlying(piece); }

    void Prune()
    {
        for (int i = claims.Count - 1; i >= 0; i--) if (claims[i].piece == null || claims[i].slot == null) claims.RemoveAt(i);
        var gone = new List<JobBase>();
        foreach (JobBase j in footprints.Keys) if (j == null) gone.Add(j);
        foreach (JobBase j in gone) { footprints.Remove(j); openedOrder.Remove(j); putAway.Remove(j); }
    }

    // ================================================================== a piece's own size, and how it lies in its place

    /// <summary>A piece's box in its own frame (metres), from what is drawn of it; measured once.</summary>
    public Bounds LocalBox(Component piece)
    {
        if (piece == null) return new Bounds(Vector3.zero, Vector3.one * .01f);
        if (boxes.TryGetValue(piece, out Bounds b)) return b;
        b = Measure(piece.transform);
        boxes[piece] = b;
        return b;
    }

    /// <summary>Measures a transform's renderers in its own frame (metres; scale kept in). A thing with nothing drawn: 1 cm.</summary>
    public static Bounds Measure(Transform root)
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        Quaternion unturn = Quaternion.Inverse(root.rotation);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!DeviceBounds.Counts(r)) continue;
            var mf = r.GetComponent<MeshFilter>();
            Bounds mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : r.bounds;
            bool own = mf != null && mf.sharedMesh != null;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                if (own) corner = r.transform.TransformPoint(corner);
                Vector3 local = unturn * (corner - root.position);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }
        if (float.IsInfinity(min.x)) return new Bounds(Vector3.zero, Vector3.one * .01f);
        var box = new Bounds();
        box.SetMinMax(min, max);
        return box;
    }

    // The way a piece lies in its place: which of its own axes points up (a cover's outer face, a part's outside, a screw on its
    // side), told by the piece.
    static Vector3 LieUpOf(Component piece)
    {
        switch (piece)
        {
            case Screw screw: return screw.LyingUpLocal;
            case RemovablePart cover: return cover.OuterFaceLocal;
            case ReplaceablePart part: return part.OutsideLocal;
            case LoosePart fresh: return fresh.OutsideLocal;
            default: return Vector3.up;
        }
    }

    // A piece's footprint lying flat (half sizes, mm): its long side along the mat's y, its middle side along x.
    Vector2 HalfMm(Component piece, SlotKind kind)
    {
        if (kind == SlotKind.Cup) return new Vector2(CupRadiusMm, CupRadiusMm);
        Bounds b = LocalBox(piece);
        Vector3 up = LieUpOf(piece);
        Axes(b.size, up, out int upAxis, out int longAxis, out int midAxis);
        return new Vector2(b.size[midAxis] * 500f, b.size[longAxis] * 500f);
    }

    // The piece's axes: the one nearest its lying-up direction is up; of the other two, the longer lies along the bay.
    static void Axes(Vector3 size, Vector3 upLocal, out int upAxis, out int longAxis, out int midAxis)
    {
        Vector3 a = new Vector3(Mathf.Abs(upLocal.x), Mathf.Abs(upLocal.y), Mathf.Abs(upLocal.z));
        upAxis = a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        int p = (upAxis + 1) % 3, q = (upAxis + 2) % 3;
        if (Mathf.Abs(size[p]) >= Mathf.Abs(size[q])) { longAxis = p; midAxis = q; } else { longAxis = q; midAxis = p; }
    }

    static Vector3 Unit(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

    // A seeded wobble per piece, so a piece always lies the same way in the same place.
    static float Jitter(Component piece, int salt, float range)
    {
        // GetEntityId, not GetInstanceID (retired in Unity 6.5); stable for the session, like BeveragePourAudio's seed.
        int seed = piece != null ? (piece.GetEntityId().GetHashCode() & int.MaxValue) * 31 + salt * 977 : salt;
        var rng = new System.Random(seed);
        return ((float)rng.NextDouble() * 2f - 1f) * range;
    }

    /// <summary>
    /// The pose a piece rests at in its claimed place: lying as its kind lies (a screw on its side, its axis about radial from its
    /// device, ±30°; a cover outer face up, its long side along the bay; a part flat, long side along the bay or, in the old-parts
    /// tray, along the tray; ±4° askew on the pad), its middle over the place, its lowest point on the place's floor, stacked.
    /// </summary>
    Pose RestPose(Claim claim, Vector3 lieUpLocal)
    {
        Transform slot = claim.slot;
        Component piece = claim.piece;
        Bounds b = LocalBox(piece);
        Vector3 up = MatUp;
        Vector3 along;
        float yawJitter;
        Vector3 longLocal;
        if (claim.kind == SlotKind.Cup)
        {
            // A screw lies on its side: its own axis lies flat, pointing about away from its device (radial), ±30°.
            Vector2 cupMm = WorldToMat(slot.position);
            Vector2 deviceMm = claim.job != null && footprints.TryGetValue(claim.job, out BenchRules.MatBox d) ? new Vector2(d.x, d.y) : CurrentLayout.rest;
            Vector2 radial = (cupMm - deviceMm).sqrMagnitude > 1f ? (cupMm - deviceMm).normalized : Vector2.right;
            along = MatRight * radial.x + MatAway * radial.y;
            yawJitter = Jitter(piece, 1, 30f);
            longLocal = piece is Screw screw ? screw.AxisLocal : Vector3.up;
            Vector3 side = Vector3.Cross(longLocal, Vector3.right).sqrMagnitude > .01f ? Vector3.Cross(longLocal, Vector3.right) : Vector3.Cross(longLocal, Vector3.forward);
            lieUpLocal = side.normalized;
        }
        else
        {
            Axes(b.size, lieUpLocal, out int upAxis, out int longAxis, out _);
            // Its up, signed as the piece told it.
            Vector3 u = Unit(upAxis);
            lieUpLocal = Vector3.Dot(u, lieUpLocal) >= 0f ? u : -u;
            longLocal = Unit(longAxis);
            along = claim.kind == SlotKind.Scrap ? MatRight : MatAway;     // the old-parts tray is long across the mat; the bays run away from him
            yawJitter = claim.kind == SlotKind.Pad ? Jitter(piece, 2, 4f) : claim.kind == SlotKind.Scrap ? Jitter(piece, 3, 6f) : Jitter(piece, 4, 2f);
        }
        along = Quaternion.AngleAxis(yawJitter, up) * along;
        Vector3 longFlat = Vector3.ProjectOnPlane(longLocal, lieUpLocal);
        if (longFlat.sqrMagnitude < 1e-6f) longFlat = Vector3.Cross(lieUpLocal, Vector3.right).sqrMagnitude > 1e-6f ? Vector3.Cross(lieUpLocal, Vector3.right) : Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(along, up) * Quaternion.Inverse(Quaternion.LookRotation(longFlat.normalized, lieUpLocal.normalized));

        // Its middle over the place, its lowest corner on the floor (the empties sit on the floor of their cup, pad, well or tray).
        Vector3 centre = rotation * b.center;
        float lowest = float.PositiveInfinity, height = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            float h = Vector3.Dot(rotation * corner, up);
            lowest = Mathf.Min(lowest, h);
            height = Mathf.Max(height, h);
        }
        height -= lowest;
        Vector3 floor = slot.position;
        Vector3 position = floor - Vector3.ProjectOnPlane(centre, up) - up * lowest;
        if (claim.stack > 0)
        {
            // Stacked (§4.0.1's fallback): a screw 0.6 of its head's width up and a little aside; anything else its own height up.
            float step = claim.kind == SlotKind.Cup && piece is Screw sc ? .6f * sc.HeadDiameter : height;
            position += up * (step * claim.stack);
            if (claim.kind == SlotKind.Cup && piece is Screw s2)
                position += (MatRight * Jitter(piece, 5, .3f) + MatAway * Jitter(piece, 6, .3f)) * s2.HeadDiameter;
        }
        return new Pose(position, rotation);
    }

    // ================================================================== the flights (§4.0.2)

    /// <summary>How a flight is timed and turns: a screw's own (quicker, a tumble), a cover's or a fresh part's (smooth), a
    /// broken part's (a wobble), a tool's (0.2 s home).</summary>
    public enum FlightKind { Screw, Cover, Part, Fresh, Tool }

    sealed class Flight
    {
        public Transform piece;
        public JobBase job;
        public Claim claim;                  // the place it lands in (it is parented to it on landing), or null
        public FlightKind speed;
        public bool toDevice;                // its target rides on the device (a fetch, a cover home, a fresh part's hover)
        // Where to: a pose in a frame that may move (the device turning), or a fixed world pose.
        public Transform toFrame;
        public Vector3 toLocal;
        public Quaternion toLocalRot;
        public Pose toWorld;
        public Transform parentOnLanding;    // besides a claim's slot (a hover rides on the device)
        // The beat and the lift (or the pop) before the arc: hold for `beat`, then move by `liftBy` (world) over `lift`, eased out.
        public float beat, lift;
        public Vector3 liftBy;
        public string liftCue;
        public bool liftWobble;
        public string launchCue, landCue;
        public float hop;                    // the settle hop's height, metres (0: none)
        public float duration;               // the arc's time (worked out at launch unless set)
        public Action landed;
        // Running.
        public float t;
        public Vector3 from;
        public Quaternion fromRot;
        public Vector3 arcFrom;
        public Quaternion arcFromRot;
        public bool lifted, launched, arrived;
        public Vector3 tumbleAxis;
        public float wobbleSeed;
    }

    readonly List<Flight> flights = new List<Flight>();

    /// <summary>True while the piece is flying (or still hopping on landing).</summary>
    public bool IsFlying(Component piece)
    {
        if (piece == null) return false;
        foreach (Flight f in flights) if (f.piece == piece.transform && !f.arrived) return true;
        return false;
    }

    // Still in the flight list: in the air, or hopping on landing (the guard must not read the hop as a move).
    bool HasFlight(Component piece)
    {
        foreach (Flight f in flights) if (f.piece == piece.transform) return true;
        return false;
    }

    /// <summary>True while any flight is under way (labs).</summary>
    public bool AnyFlying { get { foreach (Flight f in flights) if (!f.arrived) return true; return false; } }

    /// <summary>
    /// Sends a freed piece of <paramref name="job"/> to its place of <paramref name="kind"/> (§4.0.1): it claims the place, and
    /// flies there after its beat and its lift (a screw's pop, a cover's lift, a part's wobbling lift), with its cues on the
    /// arc's first and last frames, and hops. <paramref name="landed"/> runs on the landing frame. With no place of the kind (a
    /// scene without the stage, an editor check), it stays where its lift leaves it and lands at once.
    /// </summary>
    public static void SendToPlace(Component piece, JobBase job, SlotKind kind, FlightKind speed, float beat, float lift, Vector3 liftBy,
        string liftCue, bool liftWobble, string launchCue, string landCue, float hop, Action landed)
    {
        if (piece == null) return;
        BenchStage stage = Instance;
        Claim claim = stage != null ? stage.ClaimFor(job, piece, kind, stage.HalfMm(piece, kind)) : null;
        Pose to = claim != null ? stage.RestPose(claim, LieUpOf(piece)) : new Pose(piece.transform.position + liftBy, piece.transform.rotation);
        var f = new Flight
        {
            piece = piece.transform, job = job, claim = claim, speed = speed, toWorld = to,
            beat = beat, lift = lift, liftBy = liftBy, liftCue = liftCue, liftWobble = liftWobble,
            launchCue = launchCue, landCue = landCue, hop = hop, landed = landed,
        };
        if (stage != null) stage.StartFlight(f);
        else Land(null, f);
    }

    /// <summary>
    /// Flies a piece to a pose in a frame that may move (the device: a fetched screw to its hole, a cover home, a fresh part to
    /// hover over its seat), releasing any place it held. It is parented to <paramref name="parentOnLanding"/> when it arrives.
    /// </summary>
    public static void SendTo(Component piece, JobBase job, Transform frame, Vector3 localPosition, Quaternion localRotation, FlightKind speed,
        float duration, string launchCue, string landCue, Transform parentOnLanding, Action landed)
        => SendTo(piece, job, frame, localPosition, localRotation, speed, duration, launchCue, landCue, parentOnLanding, landed, 0f, Vector3.zero);

    /// <summary>The same, after a lift of <paramref name="liftBy"/> (world) over <paramref name="lift"/> seconds, eased out: a
    /// tool lifting off the device before it flies home after the last spot (§4.4.1).</summary>
    public static void SendTo(Component piece, JobBase job, Transform frame, Vector3 localPosition, Quaternion localRotation, FlightKind speed,
        float duration, string launchCue, string landCue, Transform parentOnLanding, Action landed, float lift, Vector3 liftBy)
    {
        if (piece == null) return;
        BenchStage stage = Instance;
        if (stage != null) stage.Release(piece);
        var f = new Flight
        {
            piece = piece.transform, job = job, speed = speed, toDevice = frame != null && speed != FlightKind.Tool,
            toFrame = frame, toLocal = localPosition, toLocalRot = localRotation,
            toWorld = frame != null ? new Pose(frame.TransformPoint(localPosition), frame.rotation * localRotation) : new Pose(localPosition, localRotation),
            launchCue = launchCue, landCue = landCue, parentOnLanding = parentOnLanding, duration = duration, landed = landed,
            lift = Mathf.Max(0f, lift), liftBy = liftBy,
        };
        if (stage != null) stage.StartFlight(f);
        else Land(null, f);
    }

    /// <summary>Stops a piece's flight where it is, without its landing (a tool taken again on its way home).</summary>
    public static void CancelFlight(Component piece)
    {
        BenchStage stage = Instance;
        if (stage == null || piece == null) return;
        for (int i = stage.flights.Count - 1; i >= 0; i--) if (stage.flights[i].piece == piece.transform) stage.flights.RemoveAt(i);
    }

    /// <summary>Flies a piece back to the place it holds (a fresh part released from its hover goes back to its pad), or
    /// claims one.</summary>
    public static void SendBackToPlace(Component piece, JobBase job, SlotKind kind, FlightKind speed, Action landed)
        => SendToPlace(piece, job, kind, speed, 0f, 0f, Vector3.zero, null, false, null, null, 0f, landed);

    /// <summary>Puts a piece in its place of the kind at once (no flight): stock laid out as the close-up opens, an editor check.</summary>
    public static void PlaceNow(Component piece, JobBase job, SlotKind kind)
    {
        if (piece == null) return;
        BenchStage stage = Instance;
        Claim claim = stage != null ? stage.ClaimFor(job, piece, kind, stage.HalfMm(piece, kind)) : null;
        if (claim == null) return;
        Pose to = stage.RestPose(claim, LieUpOf(piece));
        piece.transform.SetParent(null, true);
        piece.transform.SetPositionAndRotation(to.position, to.rotation);
        piece.transform.SetParent(claim.slot, true);
        claim.landed = true;
        claim.restLocal = piece.transform.localPosition;
        claim.restLocalRot = piece.transform.localRotation;
    }

    void StartFlight(Flight f)
    {
        // A piece already flying is redirected from where it is now.
        for (int i = flights.Count - 1; i >= 0; i--) if (flights[i].piece == f.piece) flights.RemoveAt(i);
        f.from = f.piece.position;
        f.fromRot = f.piece.rotation;
        f.wobbleSeed = Jitter(f.piece, 9, 100f);
        if (!Application.isPlaying) { Land(this, f); return; }
        // Out of any parent while it flies (a piece in a cup, a hole's screw, a part in its seat), unless it flies to the device.
        f.piece.SetParent(null, true);
        if (f.lift <= 0f && f.beat <= 0f) Launch(f);
        flights.Add(f);
    }

    // The arc begins: from where the beat and lift left it, with its launch cue on this frame.
    void Launch(Flight f)
    {
        f.launched = true;
        f.lifted = true;
        f.arcFrom = f.piece.position;
        f.arcFromRot = f.piece.rotation;
        Pose to = Target(f);
        if (f.duration <= 0f)
        {
            float px = ScreenDistance(f.arcFrom, to.position);
            f.duration = f.speed == FlightKind.Tool ? BenchRules.ToolHomeSeconds : BenchRules.FlightSeconds(px, f.speed == FlightKind.Screw);
        }
        Vector3 flat = Vector3.ProjectOnPlane(to.position - f.arcFrom, MatUp);
        Vector3 across = Vector3.Cross(MatUp, flat);
        f.tumbleAxis = across.sqrMagnitude > 1e-8f ? across.normalized : MatRight;
        if (!string.IsNullOrEmpty(f.launchCue)) Sfx.Play(f.launchCue, f.arcFrom);
    }

    Pose Target(Flight f)
    {
        if (f.toFrame != null) return new Pose(f.toFrame.TransformPoint(f.toLocal), f.toFrame.rotation * f.toLocalRot);
        return f.toWorld;
    }

    /// <summary>The on-screen length between two world points in 1080p reference pixels (a flight's time follows what the
    /// player sees it cross); 3.4 px/mm, the layout view's scale, without a camera.</summary>
    public static float ScreenPixels(Vector3 a, Vector3 b) => ScreenDistance(a, b);

    // On-screen length in 1080p reference pixels (the flight's time follows what the player sees it cross).
    static float ScreenDistance(Vector3 a, Vector3 b)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            if (sa.z > 0f && sb.z > 0f) return ((Vector2)sa - (Vector2)sb).magnitude * 1080f / Mathf.Max(1, Screen.height);
        }
        return (a - b).magnitude * 3400f;   // 3.4 px/mm, the layout view's scale
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (flights.Count > 0) Advance(dt);
        Guard();
    }

    void Advance(float dt)
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            if (f.piece == null) { flights.RemoveAt(i); continue; }
            f.t += dt;
            // The beat, then the lift (or the pop), eased out.
            if (!f.lifted)
            {
                if (f.t < f.beat) continue;
                float s = f.lift > 0f ? Mathf.Clamp01((f.t - f.beat) / f.lift) : 1f;
                if (f.liftCue != null) { Sfx.Play(f.liftCue, f.piece.position); f.liftCue = null; }
                float e = 1f - (1f - s) * (1f - s);
                Quaternion wobble = Quaternion.identity;
                if (f.liftWobble)
                {
                    // A thing held in tweezers wobbles, strongest as it lifts, then settling (today's ±6° at 42 rad/s).
                    float w = Mathf.Sin((f.t - f.beat) * 42f + f.wobbleSeed) * 6f * (1f - s * .5f);
                    Camera cam = Camera.main;
                    wobble = Quaternion.AngleAxis(w, cam != null ? cam.transform.forward : Vector3.forward);
                }
                f.piece.SetPositionAndRotation(f.from + f.liftBy * e, wobble * f.fromRot);
                if (s < 1f) continue;
                f.piece.SetPositionAndRotation(f.from + f.liftBy, f.fromRot);
                Launch(f);
                f.t = f.beat + f.lift;      // the arc's clock starts now
            }
            float arcT = f.t - f.beat - f.lift;
            Pose to = Target(f);
            if (!f.arrived)
            {
                float u = BenchRules.Ease(arcT / Mathf.Max(.01f, f.duration));
                if (arcT >= f.duration)
                {
                    Arrive(f, to);
                    // No hop: done on its landing frame (its own landing may already have moved it on: a press-in begun with
                    // the button still down). By reference: a landing can start another flight, shifting the list.
                    if (f.hop <= 0f) flights.Remove(f);
                    continue;
                }
                Vector3 p0 = f.arcFrom, p2 = to.position;
                Vector3 flat = Vector3.ProjectOnPlane(p2 - p0, MatUp);
                Vector3 control = (p0 + p2) * .5f + MatUp * BenchRules.ControlRise(flat.magnitude);
                BenchRules.BezierWeights(u, out float w0, out float w1, out float w2);
                Vector3 position = p0 * w0 + control * w1 + p2 * w2;
                Quaternion rotation = Quaternion.Slerp(f.arcFromRot, to.rotation, u);
                if (f.speed == FlightKind.Screw)
                {
                    // One and a half tumbles across its path, blended into the landing pose over the last quarter.
                    Quaternion tumbling = Quaternion.AngleAxis(BenchRules.ScrewTumbles * 360f * u, f.tumbleAxis) * rotation;
                    rotation = Quaternion.Slerp(tumbling, to.rotation, 1f - BenchRules.TumbleLeft(u));
                }
                else if (f.speed == FlightKind.Part)
                {
                    float wob = BenchRules.PartWobbleDegrees * Mathf.Sin(arcT * 26f + f.wobbleSeed) * (1f - u);
                    rotation = Quaternion.AngleAxis(wob, f.tumbleAxis) * rotation;
                }
                f.piece.SetPositionAndRotation(position, rotation);
                continue;
            }
            // The settle hop.
            float since = arcT - f.duration;
            if (since >= BenchRules.HopSeconds || f.hop <= 0f)
            {
                Settle(f);
                flights.Remove(f);
                continue;
            }
            Pose rest = RestingPose(f);
            f.piece.SetPositionAndRotation(rest.position + MatUp * (f.hop * BenchRules.Hop(since)), rest.rotation);
        }
    }

    // The frame it arrives: the exact landing pose, its landing cue, its place (or the device) takes it, and the piece's own
    // landing (its state) runs.
    void Arrive(Flight f, Pose to)
    {
        f.arrived = true;
        f.piece.SetPositionAndRotation(to.position, to.rotation);
        Transform parent = f.claim != null ? f.claim.slot : f.parentOnLanding;
        if (parent != null) f.piece.SetParent(parent, true);
        if (f.claim != null)
        {
            f.claim.landed = true;
            f.claim.restLocal = f.piece.localPosition;
            f.claim.restLocalRot = f.piece.localRotation;
        }
        if (!string.IsNullOrEmpty(f.landCue)) Sfx.Play(f.landCue, to.position);
        if (f.job != null) BenchWork.NoteProgress(f.job);
        try { f.landed?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // The hop is over: exactly in its place.
    void Settle(Flight f)
    {
        Pose rest = RestingPose(f);
        f.piece.SetPositionAndRotation(rest.position, rest.rotation);
    }

    Pose RestingPose(Flight f)
    {
        if (f.claim != null && f.piece.parent == f.claim.slot && f.claim.slot != null)
            return new Pose(f.claim.slot.TransformPoint(f.claim.restLocal), f.claim.slot.rotation * f.claim.restLocalRot);
        return Target(f);
    }

    // At once (edit mode; a close-up coming down on a flight to the device): the same end, no frames.
    void Complete(Flight f)
    {
        flights.Remove(f);
        if (f.piece == null) return;
        if (!f.arrived) Land(this, f);
        else Settle(f);
    }

    static void Land(BenchStage stage, Flight f)
    {
        if (!f.launched)
        {
            if (f.liftCue != null) f.liftCue = null;
            f.launched = true;
        }
        Pose to = f.toFrame != null ? new Pose(f.toFrame.TransformPoint(f.toLocal), f.toFrame.rotation * f.toLocalRot) : f.toWorld;
        f.arrived = true;
        f.piece.SetParent(null, true);
        f.piece.SetPositionAndRotation(to.position, to.rotation);
        Transform parent = f.claim != null ? f.claim.slot : f.parentOnLanding;
        if (parent != null) f.piece.SetParent(parent, true);
        if (f.claim != null)
        {
            f.claim.landed = true;
            f.claim.restLocal = f.piece.localPosition;
            f.claim.restLocalRot = f.piece.localRotation;
        }
        if (Application.isPlaying && !string.IsNullOrEmpty(f.landCue)) Sfx.Play(f.landCue, to.position);
        try { f.landed?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ================================================================== the guard (§4.0.4)

    /// <summary>How many times the guard has had to set a piece back (labs: must stay 0).</summary>
    public static int GuardTrips { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; GuardTrips = 0; }

    void Guard()
    {
        if (claims.Count == 0) return;
        float floor = MatToWorld(Vector2.zero, MatTop).y - FallLimit;
        for (int i = claims.Count - 1; i >= 0; i--)
        {
            Claim c = claims[i];
            if (c.piece == null || c.slot == null) { claims.RemoveAt(i); continue; }
            if (!c.landed || HasFlight(c.piece) || !c.piece.gameObject.activeInHierarchy) continue;   // the landing hop is the flight's own motion
            Transform t = c.piece.transform;
            bool moved = t.parent != c.slot || (t.localPosition - c.restLocal).magnitude * Mathf.Max(1e-4f, c.slot.lossyScale.x) > SlotDrift;
            bool fallen = t.position.y < floor;
            if (!moved && !fallen) continue;
            GuardTrips++;
            Debug.LogError($"[Bench stage] The guard set {Name(c.piece)} back in {c.slot.name}: it was {(fallen ? "under the mat" : $"{(t.position - c.slot.TransformPoint(c.restLocal)).magnitude * 1000f:0.0} mm from its place")}. Under the forgiving bench this can't happen: a piece is never a rigidbody and only its own code moves it.");
            t.SetParent(c.slot, true);
            t.localPosition = c.restLocal;
            t.localRotation = c.restLocalRot;
        }
    }

    static string Name(Component c) => c != null ? c.name : "a piece";

    // ================================================================== a piece's state (labs, the checklist, the glow)

    /// <summary>What a bench piece is doing (§4.0.3): on its device, flying, resting in its place, hovering over its seat or
    /// hole, or being pressed in.</summary>
    public static PieceState StateOf(Component piece)
    {
        BenchStage stage = Instance;
        if (stage != null && stage.IsFlying(piece)) return PieceState.Flying;
        switch (piece)
        {
            case Screw screw:
                if (!screw.IsOut) return PieceState.OnDevice;
                return screw.Fetched ? PieceState.Hovering : PieceState.InSlot;
            case ScrewTarget target:
                return StateOf(target.GetComponent<Screw>());
            case RemovablePart cover:
                return cover.IsRemoved ? PieceState.InSlot : PieceState.OnDevice;
            case ReplaceablePart part:
                return part.State == ReplaceablePart.PartState.InPlace ? PieceState.OnDevice : part.State == ReplaceablePart.PartState.HeldBroken ? PieceState.Flying : PieceState.InSlot;
            case LoosePart fresh:
                return fresh.Pressing ? PieceState.Pressing : fresh.Hovering ? PieceState.Hovering : PieceState.InSlot;
            default:
                return PieceState.OnDevice;
        }
    }

    // ================================================================== life

    void Awake() { Instance = this; }
    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

#if UNITY_EDITOR
    /// <summary>Bench 7's hand on the stage: the mat, its places, the old-parts tray and the tool rest (contracts §2).</summary>
    public void WireStage(Transform matFrame, Transform[] cups, Transform padSmall, Transform padLarge, Transform wellSmall, Transform wellLarge,
        Transform scrapTray, Transform[] scrapSlots, Transform toolRest, Transform[] toolSlots)
    {
        this.matFrame = matFrame; this.cups = cups; this.padSmall = padSmall; this.padLarge = padLarge; this.wellSmall = wellSmall; this.wellLarge = wellLarge;
        this.scrapTray = scrapTray; this.scrapSlots = scrapSlots; this.toolRest = toolRest; this.toolSlots = toolSlots;
    }

    /// <summary>The v2 step's hand (Bench 2): the slab, the tray, the caddy and the tools' stands. The magnet zone is gone with
    /// the magnet (the forgiving bench); the argument is kept so the old step still builds.</summary>
    public void Wire(Transform mat, Transform tray, BoxCollider trayZone, Transform caddy, Transform[] toolSlots)
    {
        this.mat = mat; this.tray = tray; this.caddy = caddy; this.toolSlots = toolSlots;
    }

    /// <summary>The v2 pieces (Bench 7 switches them off and Undo puts them back).</summary>
    public Transform LegacyMat => mat;
    public Transform LegacyTray => tray;
    public Transform LegacyCaddy => caddy;
#endif
}
