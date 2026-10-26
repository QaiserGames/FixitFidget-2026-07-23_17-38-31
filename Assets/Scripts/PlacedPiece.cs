using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// ---------------------------------------------------------------------------
// ONE PIECE OF FURNITURE IN THE CAFÉ (24 Oct 2026; furniture-spec.md §4.1, §4.4, §4.11)
//
// His choice of 23 Oct, "Grow the café": the root of one set (a table and its chairs, the window counter and its stools, a
// banquette bay with its 2-top, the lounge's chairs, table and rug, a plant in its pot), placed, moved and sold as one in
// Arrange the café (ArrangeMode) at closing. Two kinds share this component:
//   * the café's own pieces: the sets the scene already has. Fixit Fidget > Cafe redesign > Furniture 1 gives each set a root
//     under 31 - furniture/The café's own pieces, its parts moved under it with their names, so every name and every reference
//     stays as it was (the settled guests point at their seats, the labs find seats by name). Their id is "scene:<root name>";
//   * bought pieces: made at closing from their kind's prefab (Furniture 2 built it from the café's own dressed set). Their id
//     is "p<n>".
//
// WHAT A PIECE HOLDS (set by the step that made it; nothing here is found by name at run time):
//   * its solids: a collider each (the kit's own boxes, the plants' pots), what Ace bumps into and what FurnitureRules keeps
//     apart. The bay's end caps aren't solids: 4 cm boards that show only where no bay joins (BanquetteRun);
//   * a carving NavMeshObstacle per solid, the same box (furniture-spec.md §4.3: the routes are baked once without the pieces,
//     NavMeshModifier Ignore From Build on this root, and every piece carves its own hole, which moves with it);
//   * its seats, ordinary TableSeats: switched on, each registers itself with WaitingArea (WaitingSpot.OnEnable), switched off
//     it leaves. Nothing else needs telling. Their tags come from SeatTags on each seat and the room's areas (Seat tags 1);
//   * its dressing (the napkin dispenser, the laptop and the tote under a seat, the rug, a corner's lamp): children, so they
//     follow. No static flags anywhere on it: a static-batched mesh would stay drawn where the scene was built (Furniture 1
//     clears them).
//
// SetOn(false) clears its cups first (a dirty cup is its own object at a seat's cup spot: DirtyCup.Place), so no cup floats
// and no seat stays dirty with nothing on it; then the whole piece goes off: hidden, its seats unregistered, its holes gone.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class PlacedPiece : MonoBehaviour
{
    [Tooltip("What it is (its asset's name is the save identity).")]
    [SerializeField] private FurnitureDefinition kind;

    [Tooltip("Its save id: \"scene:<root name>\" for the café's own, \"p<n>\" for a bought one.")]
    [SerializeField] private string id = "";

    [Tooltip("One of the café's own pieces (a scene object, switched off when sold), not a bought one (destroyed when sold).")]
    [SerializeField] private bool fromScene;

    [Tooltip("1 Workshop; 2 Marble & Brass (not sold in v1, but saved from day one: his answer of 24 Oct, f4).")]
    [SerializeField, Min(1)] private int tier = 1;

    [Tooltip("A plant's kind (\"Monstera\"); empty for everything else.")]
    [SerializeField] private string variant = "";

    [Tooltip("Its seats (in order). Empty: every TableSeat under it.")]
    [SerializeField] private TableSeat[] seats = new TableSeat[0];

    [Tooltip("Its solids: the colliders FurnitureRules keeps apart (its end caps are not among them).")]
    [SerializeField] private Collider[] solids = new Collider[0];

    [Tooltip("Its carving boxes (one per solid): its hole in the routes, which moves with it.")]
    [SerializeField] private NavMeshObstacle[] obstacles = new NavMeshObstacle[0];

    [Tooltip("A banquette bay only: the end caps at its local -X and +X ends (shown only where no bay joins it: BanquetteRun).")]
    [SerializeField] private GameObject capLow, capHigh;

    // ------------------------------------------------------------------ the contract (furniture-spec.md §4.11)

    public FurnitureDefinition Kind => kind;
    /// <summary>"scene:&lt;name&gt;" or "p&lt;n&gt;".</summary>
    public string Id => id;
    /// <summary>One of the café's own pieces.</summary>
    public bool FromScene => fromScene;
    /// <summary>Its pivot, on the floor.</summary>
    public Vector3 Pivot => transform.position;
    /// <summary>Its turn about up, degrees (0..360).</summary>
    public float Yaw => Mathf.Repeat(transform.eulerAngles.y, 360f);
    public int Tier => tier;
    public string Variant => variant ?? "";
    /// <summary>On the floor (and its seats registered).</summary>
    public bool On => gameObject.activeSelf;

    public IReadOnlyList<TableSeat> Seats
    {
        get
        {
            if (seats == null || seats.Length == 0) seats = GetComponentsInChildren<TableSeat>(true);
            return seats;
        }
    }

    /// <summary>Its colliders' world boxes (the ones that count as its solids), where it stands now.</summary>
    public void Solids(List<Bounds> into)
    {
        if (into == null) return;
        foreach (Bounds b in Local.boxes) into.Add(ToWorld(b, Pivot, Yaw));
    }

    /// <summary>Its seats' stand points, where it stands now.</summary>
    public void StandPoints(List<Vector3> into)
    {
        if (into == null) return;
        foreach (Vector3 p in Local.stands) into.Add(ToWorld(p, Pivot, Yaw));
    }

    /// <summary>Moves everything on it as one: its pivot to <paramref name="pivot"/> (on the floor), turned <paramref name="yaw"/> degrees.</summary>
    public void SetPose(Vector3 pivot, float yaw)
    {
        transform.SetPositionAndRotation(new Vector3(pivot.x, pivot.y, pivot.z), Quaternion.Euler(0f, Mathf.Repeat(yaw, 360f), 0f));
    }

    /// <summary>
    /// On: on the floor, its seats registered, its holes carved. Off: its cups cleared first, then hidden, its seats unregistered
    /// (WaitingSpot.OnDisable), its holes gone.
    /// </summary>
    public void SetOn(bool on)
    {
        if (on == gameObject.activeSelf) return;
        if (!on) ClearCups();
        gameObject.SetActive(on);
    }

    // ------------------------------------------------------------------ more, for the layout, the ghost and the steps

    /// <summary>Set by CafeLayout (a bought piece) and by Furniture 1 (the café's own).</summary>
    public void Init(FurnitureDefinition what, string saveId, bool ownPiece, int pieceTier = 1, string plant = "")
    {
        kind = what;
        id = saveId ?? "";
        fromScene = ownPiece;
        tier = Mathf.Max(1, pieceTier);
        variant = plant ?? "";
    }

    /// <summary>Set by Furniture 1 and 2: its seats, solids, carving boxes and (a bay) its end caps.</summary>
    public void Wire(TableSeat[] pieceSeats, Collider[] pieceSolids, NavMeshObstacle[] pieceObstacles, GameObject low = null, GameObject high = null)
    {
        seats = pieceSeats ?? new TableSeat[0];
        solids = pieceSolids ?? new Collider[0];
        obstacles = pieceObstacles ?? new NavMeshObstacle[0];
        capLow = low;
        capHigh = high;
        local = null;
    }

    /// <summary>The colliders that count as its solids (Furniture 1's and 2's checks, the labs).</summary>
    public IReadOnlyList<Collider> SolidColliders => solids ?? new Collider[0];
    /// <summary>Its carving boxes.</summary>
    public IReadOnlyList<NavMeshObstacle> Obstacles => obstacles ?? new NavMeshObstacle[0];
    /// <summary>A banquette bay's end caps (null for anything else).</summary>
    public GameObject CapLow => capLow;
    public GameObject CapHigh => capHigh;
    public bool IsBay => capLow != null || capHigh != null;

    /// <summary>How many seats it brings.</summary>
    public int SeatCount => Seats.Count;

    /// <summary>Its name on the plate: the kind's ("Bistro table"), else the object's.</summary>
    public string DisplayName => kind != null && !string.IsNullOrWhiteSpace(kind.displayName) ? kind.displayName : name;

    /// <summary>Its shape at a pose (the rules' candidate): its boxes and stand points as they would stand there.</summary>
    public FurnitureRules.Shape ShapeAt(Vector3 pivot, float yaw)
    {
        LocalShape l = Local;
        var shape = new FurnitureRules.Shape
        {
            name = DisplayName,
            solids = new List<Bounds>(l.boxes.Count),
            stands = new List<Vector3>(l.stands.Count),
            parts = new List<string>(l.parts),
            againstAWall = kind != null && kind.againstAWall,
        };
        foreach (Bounds b in l.boxes) shape.solids.Add(ToWorld(b, pivot, yaw));
        foreach (Vector3 p in l.stands) shape.stands.Add(ToWorld(p, pivot, yaw));
        return shape;
    }

    /// <summary>Its shape where it stands now.</summary>
    public FurnitureRules.Shape Shape => ShapeAt(Pivot, Yaw);

    /// <summary>Its footprint on the floor (the union of its solids' boxes) at a pose: what lands on a dust sheet.</summary>
    public Bounds FootprintAt(Vector3 pivot, float yaw)
    {
        LocalShape l = Local;
        if (l.boxes.Count == 0) return new Bounds(pivot, Vector3.zero);
        Bounds all = ToWorld(l.boxes[0], pivot, yaw);
        for (int i = 1; i < l.boxes.Count; i++) all.Encapsulate(ToWorld(l.boxes[i], pivot, yaw));
        return all;
    }

    // ------------------------------------------------------------------ the shape in its own frame (read once)

    /// <summary>Its solids (boxes) and stand points in its own frame (the root's), read from its colliders and seats.</summary>
    public sealed class LocalShape
    {
        public readonly List<Bounds> boxes = new List<Bounds>();
        public readonly List<string> parts = new List<string>();
        public readonly List<Vector3> stands = new List<Vector3>();
    }

    [System.NonSerialized] private LocalShape local;

    /// <summary>Its shape in its own frame. Read once from the colliders' own sizes (so it works on a prefab asset and on a
    /// piece that is off), relative to this root.</summary>
    public LocalShape Local
    {
        get
        {
            if (local != null) return local;
            local = new LocalShape();
            Matrix4x4 toRoot = transform.worldToLocalMatrix;
            if (solids != null)
                foreach (Collider c in solids)
                {
                    if (c == null) continue;
                    if (!LocalBox(c, toRoot, out Bounds box)) continue;
                    local.boxes.Add(box);
                    local.parts.Add(PartOf(c.transform));
                }
            foreach (TableSeat s in Seats)
                if (s != null) local.stands.Add(toRoot.MultiplyPoint3x4(s.StandPoint.position));
            return local;
        }
    }

    /// <summary>Forget the shape read (after a step changed the piece's parts).</summary>
    public void ForgetShape() => local = null;

    /// <summary>A collider's box in a frame's own space, read as the piece reads its solids (the steps' carving boxes).</summary>
    public static bool BoxIn(Transform frame, Collider c, out Bounds box)
    {
        box = default;
        return frame != null && c != null && LocalBox(c, frame.worldToLocalMatrix, out box);
    }

    // What a solid is, for the rules: a bay's or an end cap's boards may touch another bay's (BanquetteRun joins them).
    static string PartOf(Transform t)
    {
        string n = t != null ? t.name : "";
        if (n.StartsWith("CK_BanquetteBay", System.StringComparison.Ordinal)) return FurnitureRules.BayPart;
        if (n.StartsWith("CK_BanquetteEnd", System.StringComparison.Ordinal)) return FurnitureRules.BayPart;
        return n;
    }

    // A collider's box in the root's frame, from its own size (valid while the piece is off or a prefab asset). Round shapes
    // (the plants' pots are capsules, turned any way) give their own tight box, as Collider.bounds does, never a turned cube's.
    static bool LocalBox(Collider c, Matrix4x4 toRoot, out Bounds box)
    {
        box = default;
        Matrix4x4 m2 = toRoot * c.transform.localToWorldMatrix;
        Vector3 scale = new Vector3(m2.GetColumn(0).magnitude, m2.GetColumn(1).magnitude, m2.GetColumn(2).magnitude);
        Vector3 centre, half;
        switch (c)
        {
            case BoxCollider b: centre = b.center; half = b.size * .5f; break;
            case SphereCollider s:
            {
                float r = s.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                box = new Bounds(m2.MultiplyPoint3x4(s.center), Vector3.one * 2f * r);
                return true;
            }
            case CapsuleCollider k:
            {
                // Unity's capsule: its radius scales by the larger of the other two axes, its height by its own.
                int axis = Mathf.Clamp(k.direction, 0, 2);
                float across = axis == 0 ? Mathf.Max(scale.y, scale.z) : axis == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
                float r = k.radius * across;
                float halfLine = Mathf.Max(0f, k.height * .5f * scale[axis] - r);
                Vector3 dir = m2.MultiplyVector(axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward).normalized;
                Vector3 extents = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z)) * halfLine + Vector3.one * r;
                box = new Bounds(m2.MultiplyPoint3x4(k.center), extents * 2f);
                return true;
            }
            case MeshCollider m when m.sharedMesh != null: centre = m.sharedMesh.bounds.center; half = m.sharedMesh.bounds.extents; break;
            default: return false;
        }
        bool first = true;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = centre + new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
            Vector3 p = m2.MultiplyPoint3x4(corner);
            if (first) { box = new Bounds(p, Vector3.zero); first = false; }
            else box.Encapsulate(p);
        }
        return true;
    }

    /// <summary>A box in a piece's frame, placed at a pose: its world box (exact for quarter turns).</summary>
    public static Bounds ToWorld(Bounds local, Vector3 pivot, float yaw)
    {
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 min = local.min, max = local.max;
        var b = new Bounds(pivot + turn * min, Vector3.zero);
        for (int i = 1; i < 8; i++)
            b.Encapsulate(pivot + turn * new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
        return b;
    }

    /// <summary>A point in a piece's frame, placed at a pose.</summary>
    public static Vector3 ToWorld(Vector3 local, Vector3 pivot, float yaw) => pivot + Quaternion.Euler(0f, yaw, 0f) * local;

    // ------------------------------------------------------------------ its cups

    // Picking it up or selling it clears its cups (Ace clears them as he lifts it): furniture-spec.md §1.3; the cup's own Remove
    // frees its seat (DirtyCup.Remove). A seat left dirty by some other cup is cleaned too, so nothing stays out of play.
    void ClearCups()
    {
        IReadOnlyList<TableSeat> mine = Seats;
        if (mine.Count == 0) return;
        foreach (DirtyCup cup in FindObjectsByType<DirtyCup>(FindObjectsInactive.Exclude))
        {
            if (cup == null || cup.seat == null) continue;
            for (int i = 0; i < mine.Count; i++)
                if (cup.seat == mine[i]) { cup.Remove(); break; }
        }
        for (int i = 0; i < mine.Count; i++)
            if (mine[i] != null && mine[i].IsDirty) mine[i].Clean();
    }
}
