using UnityEngine;

// ---------------------------------------------------------------------------
// THE BINS BEHIND THE CAFÉ: WHERE NIGHT 0 HAPPENS (6 Oct 2026; claude/the-man-at-the-bins-story.md)
//
// Put in the scene by Fixit Fidget > Night > Bins 1 (NightZeroSteps): the café's back door, a dumpster with two
// lids on Back Street against the café's back wall (POLYGON City's Skip_02, its lids cut free to lift), a lamp on
// the wall beside it (lit at night), a trash can at its other end (his corner: what Ace brings him stands on its lid),
// and a camera for the moment the lid lifts. This component holds what the night's code needs from it; nothing here
// runs by day except keeping his look for him (nobody else in the city wears it).
//
// Placed by hand? Move the whole group (this object) and everything moves with it; Bins 1 never moves it back.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightZeroSet : MonoBehaviour
{
    public static NightZeroSet Instance { get; private set; }

    [Header("The back door")]
    [Tooltip("Just inside the café's back door, facing it: where Ace stands with the bin bag on Night 0.")]
    public Transform insideDoor;
    [Tooltip("Just outside it on Back Street, facing away from the door: where Ace comes out.")]
    public Transform outsideDoor;

    [Header("The dumpster")]
    [Tooltip("Where Ace stands to put the bag in (in front of the near half).")]
    public Transform binSpot;
    [Tooltip("The near lid's hinge: it lifts for the bag. Turns about its own x axis.")]
    public Transform nearLid;
    [Tooltip("The far lid's hinge: it lifts when he stands up under it.")]
    public Transform farLid;
    [Tooltip("Degrees a lid turns about its hinge's x axis to stand open. The hinges are on the dumpster's high back edge, " +
             "against the wall, with the lids sloping down to the front (their +z): a negative turn lifts the front edge. " +
             "-104 leaves a lid just short of upright, clear of the wall.")]
    public float lidOpen = -104f;
    [Tooltip("Where he stands inside the far half (his feet on the dumpster's floor), facing the street.")]
    public Transform inside;
    [Tooltip("Where Ace stands to hand him something (in front of the far half).")]
    public Transform giveSpot;

    [Header("The lamp over the bins (night only)")]
    public Light lamp;

    [Header("The moment the lid lifts")]
    [Tooltip("A Cinemachine camera, switched off: switched on for the lid's moment, the view blends to it and back.")]
    public GameObject revealCamera;
    [Tooltip("The overhead view while Ace is round the back (CafeViewMode's house view): turn, tilt and distance.")]
    public float viewYaw = 205f, viewPitch = 46f, viewDistance = 14f;
    [Tooltip("Within this many metres of the dumpster (outside the café) the camera looks at the bins from the street.")]
    public float viewRadius = 8f;

    [Header("His corner: what Ace has brought him")]
    [Tooltip("Barnaby on the trash can's lid, at night once Ace has given him the gnome. Switched off in the scene.")]
    public GameObject cornerGnome;
    [Tooltip("Degrees the corner gnome turns from facing the alley to facing the street when he sets it down.")]
    public float gnomeTurn = 180f;

    [Header("What's carried and worn")]
    [Tooltip("The bin bag Ace carries out (POLYGON City's SM_Prop_TrashBag_03).")]
    public GameObject bag;
    [Tooltip("The bag's size in Ace's hand (it is a café's kitchen bag, not a skip bag).")]
    [Range(.2f, 1f)] public float bagScale = .55f;
    [Tooltip("His look: a city look nobody else wears (Character_BusinessMan_Suit: the other fixer, in a slept-in suit).")]
    public GameObject look;

    /// <summary>The middle of the dumpster on the ground (where the bins view centres): between the lids, level with his spot.</summary>
    public Vector3 Bins => nearLid != null && farLid != null && inside != null
        ? new Vector3((nearLid.position.x + farLid.position.x) * .5f, transform.position.y, inside.position.z)
        : transform.position;

    void OnEnable()
    {
        Instance = this;
        // Nobody else in the city wears his look: walk-ins, patrons and street neighbours skip it (as a regular's).
        if (look != null) CustomerProfile.ReserveStandInLook(this, look.name);
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        CustomerProfile.ReleaseStandInLook(this);
    }

    /// <summary>A lid's turn about its hinge: 0 shut, 1 open.</summary>
    public void SetLid(Transform hinge, float open)
    {
        if (hinge != null) hinge.localRotation = Quaternion.Euler(Mathf.Clamp01(open) * lidOpen, 0f, 0f);
    }

    public string Describe() =>
        $"The bins: back door {(insideDoor != null && outsideDoor != null ? "set" : "MISSING")}, dumpster lids {(nearLid != null && farLid != null ? "set" : "MISSING")}, " +
        $"lamp {(lamp != null ? "set" : "none")}, reveal camera {(revealCamera != null ? "set" : "none")}, corner gnome {(cornerGnome != null ? "set" : "none")}, " +
        $"bag {(bag != null ? bag.name : "none")}, his look {(look != null ? look.name : "none")}.";
}
