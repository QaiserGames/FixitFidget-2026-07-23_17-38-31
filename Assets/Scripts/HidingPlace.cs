using UnityEngine;

// ---------------------------------------------------------------------------
// A PLACE TO HIDE IN GRACE'S HOUSE (break-ins chunk C, 6 Oct 2026; claude/break-ins-spec.md §6, "hiding and reading the
// danger"; claude/chunk-c-grace-at-home-plan.md §4)
//
// The cupboard under her stairs (its door in the pocket beside the lower flight) and her wardrobe. Made by GraceAtHome
// while a night runs (a trigger in front of each, never saved), like the way in at her front door. Standing in front of
// one, "[E] Hide in the cupboard under the stairs" (or her wardrobe): Ace's body and capsule are set aside, the view stays
// where it is, and a note says "Hidden. [E] Come out". She never opens either. If her mark was past a third, she comes to
// where she last saw or heard Ace, looks round, and goes back to what she was doing (GraceAtHome does all of that).
//
// A place (IsZone), offered only while Ace stands in front of it (not from the kitchen through the wall): priority -1, so
// whatever else is in reach (the cups) comes first.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class HidingPlace : NightInteractable
{
    [HideInInspector] public GraceAtHome home;
    [Tooltip("What it is, as the prompt says it: \"the cupboard under the stairs\".")]
    public string what = "";

    string prompt = "";

    public override bool IsZone => true;

    void Awake() => SetPriority(-1);

    protected override bool AvailableTonight => home != null && home.CanHide(this);

    public override string Prompt
    {
        get
        {
            if (prompt.Length == 0 && what.Length > 0) prompt = "Hide in " + what;
            return prompt;
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (home != null) home.Hide(this);
    }

    /// <summary>Whether a point (Ace's middle) is in front of it: inside its trigger.</summary>
    public bool Holds(Vector3 point)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return false;
        Vector3 local = transform.InverseTransformPoint(point) - box.center;
        Vector3 half = box.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }
}
