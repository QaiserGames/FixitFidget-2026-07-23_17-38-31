using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BANQUETTE'S END CAPS (24 Oct 2026; furniture-spec.md §1.3 "a bay next to a bay snaps to it and they join into one run",
// §4.8 "each bay has two caps; after every change a cap shows only where no bay joins it")
//
// The café's banquette is three CK_BanquetteBay boards in one run along the west wall, capped by a CK_BanquetteEnd at each end
// (Seats 1). Now every bay is a piece of its own with a cap at both ends (Furniture 1 gives the run's middle bays the caps
// they never had), so a bay alone is finished both ends, and two bays side by side read as one run: the caps between them
// hide. Asked after every buy, move, sale and load (CafeLayout). The caps are boards 4 cm thick, not solids: FurnitureRules
// never sees them, so two bays can touch.
// ---------------------------------------------------------------------------
public static class BanquetteRun
{
    /// <summary>How close two bays' ends must be to count as joined, metres.</summary>
    public const float Joined = .02f;

    struct Bay { public PlacedPiece piece; public bool west; public float min, max; }

    static readonly List<Bay> bays = new List<Bay>();
    static readonly List<Bounds> boxes = new List<Bounds>();

    /// <summary>Every bay's caps: shown at an end no other bay on the same wall joins, hidden where one does.</summary>
    public static void Refresh(IReadOnlyList<PlacedPiece> pieces)
    {
        bays.Clear();
        if (pieces == null) return;
        foreach (PlacedPiece p in pieces)
        {
            if (p == null || !p.On || !p.IsBay) continue;
            if (!Span(p, out float min, out float max)) continue;
            bays.Add(new Bay { piece = p, west = p.Pivot.x < 0f, min = min, max = max });
        }
        foreach (Bay b in bays)
        {
            bool joinedMin = false, joinedMax = false;
            foreach (Bay o in bays)
            {
                if (o.piece == b.piece || o.west != b.west) continue;
                if (Mathf.Abs(o.max - b.min) <= Joined) joinedMin = true;
                if (Mathf.Abs(o.min - b.max) <= Joined) joinedMax = true;
            }
            Show(b, b.piece.CapLow, joinedMin, joinedMax);
            Show(b, b.piece.CapHigh, joinedMin, joinedMax);
        }
    }

    // A cap belongs to the end it stands at.
    static void Show(Bay b, GameObject cap, bool joinedMin, bool joinedMax)
    {
        if (cap == null) return;
        float z = cap.transform.position.z;
        bool atMin = Mathf.Abs(z - b.min) <= Mathf.Abs(z - b.max);
        bool show = atMin ? !joinedMin : !joinedMax;
        if (cap.activeSelf != show) cap.SetActive(show);
    }

    // A bay's run along its wall (its bay boards' z extent), world metres.
    static bool Span(PlacedPiece p, out float min, out float max)
    {
        min = float.PositiveInfinity;
        max = float.NegativeInfinity;
        FurnitureRules.Shape s = p.Shape;
        for (int i = 0; i < s.solids.Count; i++)
        {
            if (!s.PartIsBay(i)) continue;
            min = Mathf.Min(min, s.solids[i].min.z);
            max = Mathf.Max(max, s.solids[i].max.z);
        }
        return min < max;
    }

    /// <summary>
    /// A wall piece in hand, snapped to touch a bay already on the same wall when it comes within a grid cell of it (so two bays
    /// can join although 2.10 m isn't a whole number of 25 cm cells). The z to use (the grid's own when there's nothing to meet).
    /// </summary>
    public static float SnapToRun(IReadOnlyList<PlacedPiece> pieces, PlacedPiece template, Vector3 pivot, float yaw, PlacedPiece moving)
    {
        if (template == null || pieces == null) return pivot.z;
        FurnitureRules.Shape s = template.ShapeAt(pivot, yaw);
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < s.solids.Count; i++)
        {
            if (!s.PartIsBay(i)) continue;
            min = Mathf.Min(min, s.solids[i].min.z);
            max = Mathf.Max(max, s.solids[i].max.z);
        }
        if (!(min < max)) return pivot.z;
        float best = pivot.z, bestShift = FurnitureRules.Grid + .001f;
        bool west = pivot.x < 0f;
        foreach (PlacedPiece p in pieces)
        {
            if (p == null || p == moving || !p.On || !p.IsBay || (p.Pivot.x < 0f) != west) continue;
            if (!Span(p, out float oMin, out float oMax)) continue;
            float toAfter = oMax - min, toBefore = oMin - max;   // the shift that makes it touch on either side
            if (Mathf.Abs(toAfter) < bestShift) { bestShift = Mathf.Abs(toAfter); best = pivot.z + toAfter; }
            if (Mathf.Abs(toBefore) < bestShift) { bestShift = Mathf.Abs(toBefore); best = pivot.z + toBefore; }
        }
        return best;
    }
}
