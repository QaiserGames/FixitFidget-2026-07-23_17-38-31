using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small thing somebody in the café can do that has nothing to do with Ace:
/// sit on the lounge sofa, take a book off the shelf, lean on a wall while
/// they wait.
///
/// WHY THESE ARE NOT WAITING SPOTS
/// Waiting spots are the pressure model: seats drain patience at 0.6, loiter
/// spots at 1.15, and the patron valve counts free seats so paying customers
/// can still sit. Every number there came from logged play. Moments sit
/// beside that system instead of inside it, so adding a sofa or a lean spot
/// makes the room busier to look at without quietly making the job easier.
///
///  * Couch     - points at a TableSeat (Snap To Seat on, usually Ambient Only)
///                on a sofa. Patrons claim the seat itself, so NpcSeating sits
///                them exactly as it does at the tables.
///  * Bookshelf - a patron walks to the Stand Point, steps up to the shelf,
///                reaches for a book (the Interact clip) and carries it to
///                their seat to read (NpcPose).
///  * Lean      - the body steps from the Stand Point to the Pose Point and
///                leans back against whatever is behind it. Linked to a Loiter
///                waiting spot, the customer waiting there leans instead of
///                standing on the mark; unlinked, it's a patron's place to
///                stand for a while when they'd rather not sit.
///
/// Place them with Fixit Fidget > NPC > Café moments. Nothing uses them unless
/// they exist, and a scene without any behaves exactly as before.
/// </summary>
[DisallowMultipleComponent]
public sealed class CafeMoment : MonoBehaviour
{
    public enum Kind { Couch, Bookshelf, Lean }

    [SerializeField] private Kind kind = Kind.Lean;

    [Tooltip("Where the NPC's navigation stops. Must be on the NavMesh. Empty: this object.")]
    [SerializeField] private Transform standPoint;

    [Tooltip("Lean and Bookshelf: where the feet go, facing along its blue (forward) axis. It may be up to a " +
             "metre off the NavMesh - against a wall, up at a shelf - because the body is walked there by hand " +
             "while its navigation waits on the Stand Point. Lean: face away from the wall. Bookshelf: face the " +
             "shelf. Empty: the Stand Point.")]
    [SerializeField] private Transform posePoint;

    [Tooltip("Couch: the sofa seat to sit on.")]
    [SerializeField] private TableSeat seat;

    [Tooltip("Lean: the Loiter waiting spot this lean belongs to. A customer waiting there leans here " +
             "instead of standing on the mark, and patrons never take it.")]
    [SerializeField] private WaitingSpot waitingSpot;

    [Tooltip("Bookshelf: a book to copy into the reader's hand. Empty: a plain placeholder book.")]
    [SerializeField] private GameObject bookTemplate;

    [Tooltip("Couch: where a reader leaves their book when they get up. Empty: on the seat.")]
    [SerializeField] private Transform bookRest;

    private static readonly List<CafeMoment> all = new List<CafeMoment>();
    private static readonly List<CafeMoment> scratch = new List<CafeMoment>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        scratch.Clear();
    }

    public static IReadOnlyList<CafeMoment> All => all;

    public Kind MomentKind => kind;
    public TableSeat Seat => seat;
    public WaitingSpot LinkedWaitingSpot => waitingSpot;
    public GameObject BookTemplate => bookTemplate;

    public Transform StandPoint => kind == Kind.Couch && seat != null ? seat.StandPoint
                                 : standPoint != null ? standPoint : transform;
    private Transform Pose => posePoint != null ? posePoint : StandPoint;
    public Vector3 PosePosition => Pose.position;
    public Vector3 Facing => Pose.forward;
    public Transform BookRest => bookRest != null ? bookRest : seat != null ? seat.SeatPose : Pose;

    /// <summary>Who has this moment (Lean, Bookshelf). A couch's claim lives on its seat.</summary>
    public GameObject Occupant { get; private set; }

    public bool IsFree => kind == Kind.Couch
        ? seat != null && seat.isActiveAndEnabled && seat.IsAvailable
        : Occupant == null;

    public bool IsHeldBy(GameObject who)
    {
        if (who == null) return false;
        if (kind == Kind.Couch)
            return seat != null && seat.Occupant != null && seat.Occupant.gameObject == who;
        return Occupant == who;
    }

    /// <summary>Lean and Bookshelf only. A couch is claimed through its TableSeat.</summary>
    public bool Claim(GameObject who)
    {
        if (who == null || kind == Kind.Couch || !isActiveAndEnabled) return false;
        if (Occupant != null && Occupant != who) return false;
        Occupant = who;
        return true;
    }

    public void Release(GameObject who)
    {
        if (who != null && Occupant == who) Occupant = null;
    }

    /// <summary>
    /// Claims a free Lean or Bookshelf moment of <paramref name="kind"/> for
    /// <paramref name="who"/>, at random so the same one isn't always used.
    /// Leans that belong to a waiting spot are left for the customers there.
    /// </summary>
    public static CafeMoment ClaimFree(Kind kind, GameObject who)
    {
        if (kind == Kind.Couch) return null;
        scratch.Clear();
        foreach (CafeMoment m in all)
            if (m != null && m.kind == kind && m.IsFree && m.waitingSpot == null) scratch.Add(m);
        if (scratch.Count == 0) return null;
        CafeMoment pick = scratch[Random.Range(0, scratch.Count)];
        return pick.Claim(who) ? pick : null;
    }

    /// <summary>A free sofa seat claimed for <paramref name="occupant"/>, or null.</summary>
    public static CafeMoment ClaimCouch(Component occupant)
    {
        scratch.Clear();
        foreach (CafeMoment m in all)
            if (m != null && m.kind == Kind.Couch && m.IsFree) scratch.Add(m);
        while (scratch.Count > 0)
        {
            int i = Random.Range(0, scratch.Count);
            CafeMoment pick = scratch[i];
            if (pick.seat.Claim(occupant)) return pick;
            scratch.RemoveAt(i);
        }
        return null;
    }

    /// <summary>The free lean that belongs to <paramref name="spot"/>, or null.</summary>
    public static CafeMoment LeanFor(WaitingSpot spot)
    {
        if (spot == null) return null;
        foreach (CafeMoment m in all)
            if (m != null && m.kind == Kind.Lean && m.waitingSpot == spot && m.IsFree) return m;
        return null;
    }

    /// <summary>The couch moment whose seat is <paramref name="spot"/>, or null.</summary>
    public static CafeMoment CouchFor(WaitingSpot spot)
    {
        if (spot == null) return null;
        foreach (CafeMoment m in all)
            if (m != null && m.kind == Kind.Couch && m.seat == spot) return m;
        return null;
    }

    public static void ReleaseAll(GameObject who)
    {
        foreach (CafeMoment m in all)
            if (m != null) m.Release(who);
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        Occupant = null;
    }

    private void OnDrawGizmos()
    {
        Color colour = kind == Kind.Couch ? new Color(.35f, .55f, 1f)
                     : kind == Kind.Bookshelf ? new Color(.95f, .6f, .2f)
                     : new Color(.55f, .85f, .45f);
        if (Occupant != null) colour = Color.red;
        Gizmos.color = colour;
        Vector3 stand = StandPoint.position;
        Gizmos.DrawWireSphere(stand, .3f);
        if (kind != Kind.Couch)
        {
            Vector3 pose = PosePosition;
            Gizmos.DrawLine(stand, pose);
            Gizmos.DrawWireCube(pose + Vector3.up * .9f, new Vector3(.3f, 1.8f, .3f));
            Gizmos.DrawRay(pose + Vector3.up * 1.2f, Facing * .5f);
        }
    }
}
