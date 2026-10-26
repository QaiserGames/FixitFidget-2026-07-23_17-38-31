using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// HIS CUPS, LEFT ON THE TABLES AT OPENING (playtest 3, 6 Oct 2026; claude/playtest-3-notes-and-plan.md §5.2)
//
// The morning after the second skip in a row (and every skipped morning after), the café opens with the man at the bins'
// mess: a used cup at every table seat near the door (LodgerDay.LayTheMess). A seat with a cup on it is out of play
// (TableSeat.IsDirty: nobody can sit there), so people who wanted to sit fall through to a loiter spot and drain faster,
// until Ace clears the table: E (or A / Cross) at any cup on it, in either view, clears every cup on that table. One
// press a table, not one a cup (his note on the third playtest: too much button pressing); the cost is the walk and the
// minutes, not the presses. Each table with his cups on it has a used-cup badge over it until it's cleared (Juice.Mark),
// because from the overhead camera a cup is a few pixels.
//
// The cups are the café's own paper cups (DrinkLiquidVisual's shell, the one Ace serves in) with the dregs left in: built
// from one shared set of meshes and materials, so nothing is copied, nothing wakes up (a copied drink would have run its
// scripts) and nothing leaks. Made in code while playing; nothing in the scene changes.
//
// GUESTS LEAVE CUPS TOO (24 Oct 2026; Designer P's spec §2.3 and §6.2, one of the four things to do while waiting he chose:
// "clearing tables"). About one seated guest in three, customers as well as patrons, leaves the cup on the table instead of
// binning it at the door (NpcCup.LeaveOnTable, by their movement profile: the Hurried and the Distracted most). The same cup,
// the same rule: the seat is out of play until Ace clears the table, one press a table (MessTable.Join puts a guest's cup on
// the table it stands on, with its badge). That is also what spreads the drink customers (the 23 Oct run: four in a row at
// the north banquette 2-top in a quiet run): the seat someone just left is dirty, so the next one sits elsewhere. A guest's
// cups are put away at closing (PutAwayGuestsCups); the man's mess keeps its own day (LodgerDay).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class DirtyCup : Interactable
{
    /// <summary>How full the dregs are (of a full cup), and where that puts them (DrinkLiquidVisual's surface).</summary>
    const float Dregs = .1f;

    /// <summary>The seat it blocks (TableSeat.SetDirty).</summary>
    public TableSeat seat;
    /// <summary>The table it's on: the cups cleared with it (made while playing; never saved).</summary>
    [System.NonSerialized] public MessTable table;

    /// <summary>Still on the table: its seat is out of play.</summary>
    public bool OnTheTable => seat != null && seat.IsDirtyWith(gameObject);

    /// <summary>A guest left it (not the man's mess): put away at closing.</summary>
    public bool FromAGuest { get; private set; }

    // Every cup on a table now (24 Oct 2026, for the Now line and the labs: Downtime.NearestDirtyCup).
    static readonly List<DirtyCup> live = new List<DirtyCup>();
    /// <summary>Every used cup in the café now (his and the guests').</summary>
    public static IReadOnlyList<DirtyCup> Live => live;

    void Awake() { if (!live.Contains(this)) live.Add(this); }

    /// <summary>Puts one of his cups on <paramref name="seat"/>'s table, at its cup spot, turned any old way. The seat is
    /// dirty until the cup's table is cleared. <paramref name="guests"/> (24 Oct 2026): a guest's cup, left as they got up
    /// (NpcCup.LeaveOnTable): it joins the table it stands on (MessTable.Join), badge and all.</summary>
    public static DirtyCup Place(TableSeat seat, bool guests = false)
    {
        if (seat == null || seat.IsDirty) return null;
        Transform spot = seat.CupSpot;
        var go = new GameObject((guests ? "A guest's cup (" : "His cup (") + seat.name + ")");
        go.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        Look look = Shared();
        Part(go.transform, "Paper cup", look.shell, look.paper, Vector3.zero, Vector3.one);
        float radius = Mathf.Lerp(.028f, .036f, Dregs);
        Part(go.transform, "Dregs", look.disc, look.dregs, new Vector3(0f, Mathf.Lerp(.012f, .098f, Dregs), 0f), new Vector3(radius, 1f, radius));
        // Something to aim at and stand near: a saucer-sized sphere, solid so the interactor's queries find it whatever
        // the project's trigger setting (it sits in the tabletop; nobody walks through a table).
        var hit = new GameObject("Reach");
        hit.transform.SetParent(go.transform, false);
        hit.transform.localPosition = Vector3.up * .06f;
        hit.AddComponent<SphereCollider>().radius = .2f;
        DirtyCup dirty = go.AddComponent<DirtyCup>();
        dirty.seat = seat;
        seat.SetDirty(go);
        if (guests)
        {
            dirty.FromAGuest = true;
            MessTable.Join(dirty);
        }
        return dirty;
    }

    /// <summary>The guests' cups go, quietly (the café tidied at closing, 24 Oct 2026): their tables' badges come down.</summary>
    public static void PutAwayGuestsCups()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            DirtyCup cup = i < live.Count ? live[i] : null;
            if (cup == null || !cup.FromAGuest) continue;
            if (cup.table != null) cup.table.Clear(false);
            else if (cup.OnTheTable) cup.Remove();
        }
    }

    public override bool IsAvailable => OnTheTable && (NightWalk.Instance == null || !NightWalk.Instance.Active);
    public override string Prompt => LodgerStory.ClearPrompt;

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        if (table != null) { table.Clear(true); return; }
        Vector3 at = transform.position;
        Remove();
        Sfx.Play("mess.clear", at);
        Juice.Sparkle(at + Vector3.up * .1f);
    }

    /// <summary>The cup goes and its seat is back in play (MessTable.Clear).</summary>
    public void Remove()
    {
        if (OnTheTable) seat.Clean();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Gone some other way (the scene closing): its seat is free and, with the last cup, its table's badge comes down.
        // Only its own seat's mark: a newer cup laid on the same seat in the same frame keeps the seat.
        live.Remove(this);
        if (OnTheTable) seat.Clean();
        if (table != null) table.CupGone(this);
    }

    // ------------------------------------------------------------------ the look, made once

    sealed class Look
    {
        public Mesh shell, disc;
        public Material paper, dregs;
        public bool Whole => shell != null && disc != null && paper != null && dregs != null;
    }

    static Look look;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        look = null;
        live.Clear();
        MessTable.ResetOpen();
    }

    static Look Shared()
    {
        if (look != null && look.Whole) return look;
        look = new Look
        {
            shell = DrinkLiquidVisual.BuildShell(),
            disc = DrinkLiquidVisual.BuildDisc(),
            paper = DrinkLiquidVisual.MakeMaterial("His cups: paper", DrinkLiquidVisual.PaperColour, .08f),
            dregs = DrinkLiquidVisual.MakeMaterial("His cups: dregs", new Color(.15f, .08f, .04f), .5f),
        };
        return look;
    }

    static void Part(Transform parent, string label, Mesh mesh, Material material, Vector3 at, Vector3 scale)
    {
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localScale = scale;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}

// ---------------------------------------------------------------------------
// THE CUPS ON ONE TABLE: cleared together, with one badge over them.
//
// A table is the cups whose cup spots are within TableReach of another cup's (single linkage: four cups round a square
// table chain together; the next table over is further than that). The seats don't know their tables, so this is the
// honest measure; NightTwoCheck lists the tables it finds, seat by seat, so a wrong guess shows in its report.
// 24 Oct 2026 (Days): a guest's cup joins the table it stands on (Join): a table with a cup already on it (by the same
// rule), else a new one with its badge. With the furniture build's placed pieces (F2: PlacedPiece), two seats of one piece
// are one table whatever the distance, and seats of two pieces never are (read by name, so this compiles without it).
// ---------------------------------------------------------------------------
public sealed class MessTable
{
    /// <summary>Cup spots this near (metres) are on the same table.</summary>
    public const float TableReach = 1f;
    /// <summary>The badge floats this far over the cups (metres).</summary>
    const float BadgeAbove = .3f;

    public readonly List<DirtyCup> cups = new List<DirtyCup>();
    /// <summary>The badge over it (Juice.Mark), or 0: for the checks.</summary>
    public int Badge { get; private set; }

    // The tables with a cup on them now, his and the guests' (24 Oct 2026): what a guest's cup joins.
    static readonly List<MessTable> open = new List<MessTable>();
    /// <summary>The tables with cups on them now.</summary>
    public static IReadOnlyList<MessTable> Open => open;
    internal static void ResetOpen() => open.Clear();

    // F2's placed furniture piece (24 Oct 2026, the furniture build), found by name so this file compiles before it lands.
    static System.Type pieceType;
    static bool pieceLooked;
    static Component PieceOf(DirtyCup cup)
    {
        if (cup == null || cup.seat == null) return null;
        if (!pieceLooked) { pieceLooked = true; pieceType = System.Type.GetType("PlacedPiece"); }
        return pieceType != null ? cup.seat.GetComponentInParent(pieceType) : null;
    }

    /// <summary>Whether two cups stand on one table: one placed piece (F2's), else within TableReach of each other.</summary>
    public static bool SameTable(DirtyCup a, DirtyCup b)
    {
        if (a == null || b == null) return false;
        Component pa = PieceOf(a), pb = PieceOf(b);
        if (pa != null || pb != null) return pa == pb;
        return (a.transform.position - b.transform.position).sqrMagnitude <= TableReach * TableReach;
    }

    /// <summary>
    /// A guest's cup goes on the table it stands on (24 Oct 2026): one with a cup on it already (his or a guest's), else a new
    /// table with its badge. One press still clears the whole table.
    /// </summary>
    public static MessTable Join(DirtyCup cup)
    {
        if (cup == null) return null;
        open.RemoveAll(t => t == null || t.Cleared);
        foreach (MessTable t in open)
            foreach (DirtyCup other in t.cups)
                if (other != null && other != cup && other.OnTheTable && SameTable(cup, other))
                {
                    t.cups.Add(cup);
                    cup.table = t;
                    t.ShowBadge();
                    return t;
                }
        var table = new MessTable();
        table.cups.Add(cup);
        cup.table = table;
        open.Add(table);
        table.ShowBadge();
        return table;
    }

    /// <summary>His cups still on it.</summary>
    public int CupsLeft
    {
        get
        {
            int n = 0;
            foreach (DirtyCup cup in cups) if (cup != null && cup.OnTheTable) n++;
            return n;
        }
    }

    public bool Cleared => CupsLeft == 0;

    /// <summary>The middle of its cups, at the highest cup's height.</summary>
    public Vector3 Centre
    {
        get
        {
            Vector3 sum = Vector3.zero;
            float top = float.MinValue;
            int n = 0;
            foreach (DirtyCup cup in cups)
            {
                if (cup == null) continue;
                Vector3 p = cup.transform.position;
                sum += p;
                top = Mathf.Max(top, p.y);
                n++;
            }
            if (n == 0) return Vector3.zero;
            Vector3 mid = sum / n;
            mid.y = top;
            return mid;
        }
    }

    /// <summary>Sorts <paramref name="cups"/> into tables (each cup's table is set).</summary>
    public static List<MessTable> Group(IList<DirtyCup> cups)
    {
        var tables = new List<MessTable>();
        var placed = new HashSet<DirtyCup>();
        float reach = TableReach * TableReach;
        foreach (DirtyCup first in cups)
        {
            if (first == null || placed.Contains(first)) continue;
            var table = new MessTable();
            var open = new Queue<DirtyCup>();
            open.Enqueue(first);
            placed.Add(first);
            while (open.Count > 0)
            {
                DirtyCup cup = open.Dequeue();
                table.cups.Add(cup);
                cup.table = table;
                foreach (DirtyCup other in cups)
                {
                    if (other == null || placed.Contains(other)) continue;
                    if ((other.transform.position - cup.transform.position).sqrMagnitude > reach) continue;
                    placed.Add(other);
                    open.Enqueue(other);
                }
            }
            tables.Add(table);
            if (!MessTable.open.Contains(table)) MessTable.open.Add(table);   // a guest's cup on the same table joins it (24 Oct 2026; 26 Oct compile fix: the live list, not the local queue)
        }
        return tables;
    }

    /// <summary>Puts its badge up (once, while a cup is on it).</summary>
    public void ShowBadge()
    {
        if (Badge == 0 && !Cleared) Badge = Juice.Mark(Centre + Vector3.up * BadgeAbove, Juice.Icon.Mess);
    }

    void HideBadge()
    {
        Juice.Unmark(Badge);
        Badge = 0;
    }

    /// <summary>Every cup on it goes and its seats are back in play: cleared by Ace (one sound, one sparkle) or put away
    /// with the shop at night (quietly).</summary>
    public void Clear(bool byAce)
    {
        Vector3 centre = Centre;
        bool any = false;
        foreach (DirtyCup cup in cups)
        {
            if (cup == null || !cup.OnTheTable) continue;
            cup.Remove();
            any = true;
        }
        HideBadge();
        open.Remove(this);
        if (byAce && any)
        {
            Sfx.Play("mess.clear", centre);
            Juice.Sparkle(centre + Vector3.up * .1f);
            Downtime.Today.tablesCleared++;   // the day's count, for the Day labs (24 Oct 2026)
        }
    }

    // A cup went some other way (DirtyCup.OnDestroy): with none left on the table, the badge comes down.
    internal void CupGone(DirtyCup gone)
    {
        foreach (DirtyCup cup in cups)
            if (cup != null && cup != gone && cup.OnTheTable) return;
        HideBadge();
    }
}
