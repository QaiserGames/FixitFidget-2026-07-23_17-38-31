using UnityEngine;

// ---------------------------------------------------------------------------
// GRACE'S BEDROOM DOORS, FOR ACE (break-ins chunk C, 6 Oct 2026; claude/break-ins-spec.md §6, "her bedroom doors creak";
// claude/chunk-c-grace-at-home-plan.md §4)
//
// She shuts her pair of doors behind her at bedtime, and after her glass of water. One shut leaf is enough to stop Ace
// (Ace is 1.16 m wide with the capsule's skin; the doorway 1.40), so in the doorway, on either side, "[E] Open the doors":
// at a walk they swing open at once and creak (heard 5 m away: that wakes her); sneaking, Ace eases them open over about
// 2 s, quietly. Made by GraceAtHome while a night runs (a trigger over the doorway, never saved); GraceAtHome swings them.
//
// A place (IsZone), offered only while Ace stands in the doorway and the doors are shut. Priority -1.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class BedroomDoorsZone : NightInteractable
{
    [HideInInspector] public GraceAtHome home;

    public override bool IsZone => true;

    void Awake() => SetPriority(-1);

    protected override bool AvailableTonight => home != null && home.CanOpenDoors(this);

    public override string Prompt => "Open the doors";

    public override void Interact(PlayerInteractor player)
    {
        if (home != null) home.AceOpensTheDoors();
    }

    /// <summary>Whether a point (Ace's middle) is in the doorway: inside its trigger.</summary>
    public bool Holds(Vector3 point)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return false;
        Vector3 local = transform.InverseTransformPoint(point) - box.center;
        Vector3 half = box.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }
}
