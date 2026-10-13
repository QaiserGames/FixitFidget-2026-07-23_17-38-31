using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The lived-in café's props the people use (the NPC life pass, 12-13 Oct 2026), found by name at run time and never
/// required: the three-bin station by the door (<c>LI_BinStation</c>), the laptops lying open on the window counter and
/// the communal table (<c>LI_Laptop_Open</c>…, off until a laptop patron sits at that seat) and the tote bags beside
/// those seats (<c>LI_ToteBag</c>…). The Environment lead places them this session; on a scene without them every call
/// here says "none" and the people behave as before (a guest keeps their cup to the door; a laptop patron is an ordinary
/// patron). Inactive objects count, because the props wait switched off.
///
/// Found once per Play session (the scene has thousands of transforms), on the first question asked.
/// </summary>
public static class NpcLifeProps
{
    public const string BinStationName = "LI_BinStation";
    public const string LaptopName = "LI_Laptop_Open";
    public const string BagName = "LI_ToteBag";

    private static bool searched;
    private static Transform binStation;
    private static Vector3 binMouth, binDropPoint;
    private static bool binDropKnown, binDropOk;
    private static readonly List<Transform> laptops = new List<Transform>();
    private static readonly List<Transform> bags = new List<Transform>();
    // A prop in use is held by whoever switched it on, so two patrons never share a laptop.
    private static readonly Dictionary<Transform, Component> users = new Dictionary<Transform, Component>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        searched = false;
        binStation = null;
        binDropKnown = binDropOk = false;
        laptops.Clear();
        bags.Clear();
        users.Clear();
    }

    /// <summary>Look again (a step placed props while playing, or a check wants a fresh survey).</summary>
    public static void Refresh()
    {
        searched = false;
        binDropKnown = false;
        users.Clear();
    }

    private static void Search()
    {
        if (searched) return;
        searched = true;
        binStation = null;
        laptops.Clear();
        bags.Clear();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null) continue;
            string n = t.name;
            // An imported FBX keeps a same-named mesh child under its root: only the outermost one is the prop (QA, 13 Oct).
            if (t.parent != null && t.parent.name.StartsWith(n.Split(' ')[0], System.StringComparison.Ordinal)) continue;
            if (binStation == null && n.StartsWith(BinStationName, System.StringComparison.Ordinal)) binStation = t;
            else if (n.StartsWith(LaptopName, System.StringComparison.Ordinal)) laptops.Add(t);
            else if (n.StartsWith(BagName, System.StringComparison.Ordinal)) bags.Add(t);
        }
    }

    // ------------------------------------------------------------ the bins

    /// <summary>The three-bin station inside the door, or null when the scene has none.</summary>
    public static Transform BinStation { get { Search(); return binStation; } }

    public static int LaptopCount { get { Search(); return laptops.Count; } }
    public static int BagCount { get { Search(); return bags.Count; } }

    /// <summary>
    /// Where a guest stands to drop a cup in (a NavMesh point beside the station, on the room's side) and where the cup
    /// goes (the station's top, its middle). False when there is no station or no floor beside it.
    /// </summary>
    public static bool TryGetBin(out Vector3 dropPoint, out Vector3 mouth)
    {
        Search();
        dropPoint = mouth = Vector3.zero;
        if (binStation == null) return false;
        if (!binDropKnown)
        {
            binDropKnown = true;
            binDropOk = false;
            Bounds b = BoundsOf(binStation);
            binMouth = new Vector3(b.center.x, b.max.y - .04f, b.center.z);
            // The room's side: towards the counter (people come from there), else the café's middle.
            Vector3? counter = NpcAttentionDirector.CounterPoint;
            Vector3 roomward = counter.HasValue ? counter.Value : new Vector3(0f, 0f, 9f);
            roomward.y = b.center.y;
            float best = float.MaxValue;
            float reach = Mathf.Max(b.extents.x, b.extents.z) + .42f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 probe = new Vector3(b.center.x + Mathf.Cos(a) * reach, b.min.y, b.center.z + Mathf.Sin(a) * reach);
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, .35f, NavMesh.AllAreas)) continue;
                float d = (hit.position - roomward).sqrMagnitude;
                if (d < best) { best = d; binDropPoint = hit.position; binDropOk = true; }
            }
        }
        if (!binDropOk) return false;
        dropPoint = binDropPoint;
        mouth = binMouth;
        return true;
    }

    // ------------------------------------------------------------ laptops and bags

    /// <summary>The laptop lying on this seat's table (within <paramref name="within"/> of its cup spot), not in use by anyone else; null for none.</summary>
    public static Transform LaptopFor(TableSeat seat, float within = .9f) =>
        Own(seat, LaptopName) ?? Nearest(laptops, seat != null ? seat.CupSpot.position : (Vector3?)null, within, true, seat);

    /// <summary>The tote bag beside this seat (within <paramref name="within"/> of its seat pose), not in use; null for none.</summary>
    public static Transform BagFor(TableSeat seat, float within = 1.1f) =>
        Own(seat, BagName) ?? Nearest(bags, seat != null ? seat.SeatPose.position : (Vector3?)null, within, true, seat);

    // A prop the scene step made a child of this seat (Lived in 2, 13 Oct 2026: "LI_Laptop_Open (seat …)", "LI_ToteBag (seat …)"),
    // free: a seat's own prop comes first, and another seat's own prop is never this one's (Nearest skips those).
    private static Transform Own(TableSeat seat, string prefix)
    {
        if (seat == null) return null;
        Search();
        foreach (Transform c in seat.transform)
            if (c.name.StartsWith(prefix, System.StringComparison.Ordinal) && !(users.TryGetValue(c, out Component u) && u != null)) return c;
        return null;
    }

    /// <summary>A seat with a laptop on its table that nobody is using: for a laptop patron choosing where to sit.</summary>
    public static bool HasLaptop(TableSeat seat) => LaptopFor(seat) != null;

    /// <summary>Keep a prop for <paramref name="user"/> without switching it on yet (a laptop patron on the way to the seat). False when it is someone else's.</summary>
    public static bool Reserve(Transform prop, Component user)
    {
        if (prop == null || user == null) return false;
        if (users.TryGetValue(prop, out Component other) && other != null && other != user) return false;
        users[prop] = user;
        return true;
    }

    /// <summary>Switch a prop on for <paramref name="user"/> (it is theirs until <see cref="Release"/>). False when it is someone else's.</summary>
    public static bool Use(Transform prop, Component user)
    {
        if (!Reserve(prop, user)) return false;
        if (!prop.gameObject.activeSelf) prop.gameObject.SetActive(true);
        return true;
    }

    /// <summary>
    /// Two people are already on their way to the bins or standing at them (not counting <paramref name="me"/>): one
    /// more would make a queue at the door, so that one keeps their cup.
    /// </summary>
    public static bool BinBusy(NpcLocomotion me)
    {
        if (!TryGetBin(out Vector3 drop, out _)) return false;
        int n = 0;
        foreach (NpcLocomotion other in NpcLocomotion.All)
        {
            if (other == null || other == me || !other.isActiveAndEnabled) continue;
            Vector3 d = other.transform.position - drop;
            d.y = 0f;
            if (other.Purpose == "bin" || d.sqrMagnitude < .7f * .7f) n++;
        }
        return n >= 2;
    }

    /// <summary>Switch a prop off again when its user is done (or gone).</summary>
    public static void Release(Transform prop, Component user)
    {
        if (prop == null) return;
        if (users.TryGetValue(prop, out Component other) && other != null && other != user) return;
        users.Remove(prop);
        if (prop.gameObject.activeSelf) prop.gameObject.SetActive(false);
    }

    /// <summary>Who switched a prop on, for the checks (null: nobody).</summary>
    public static Component UserOf(Transform prop) => prop != null && users.TryGetValue(prop, out Component u) ? u : null;

    private static Transform Nearest(List<Transform> props, Vector3? at, float within, bool free, TableSeat seat = null)
    {
        Search();
        if (!at.HasValue) return null;
        Transform best = null;
        float bestD = within * within;
        foreach (Transform p in props)
        {
            if (p == null) continue;
            if (free && users.TryGetValue(p, out Component u) && u != null) continue;
            // Another seat's own prop (Lived in 2 makes each one a child of its seat) is never this seat's.
            if (seat != null) { TableSeat owner = p.GetComponentInParent<TableSeat>(); if (owner != null && owner != seat) continue; }
            Vector3 d = p.position - at.Value;
            d.y *= .5f;   // a laptop sits on the table top, a bag on the floor: height matters less than where
            float sq = d.sqrMagnitude;
            if (sq < bestD) { bestD = sq; best = p; }
        }
        return best;
    }

    /// <summary>What is drawn of a prop, as one box (its renderers, inactive included); its transform's point without any.</summary>
    public static Bounds BoundsOf(Transform t)
    {
        var b = new Bounds(t.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) b = new Bounds(t.position + Vector3.up * .45f, new Vector3(.6f, .9f, .4f));
        return b;
    }
}
