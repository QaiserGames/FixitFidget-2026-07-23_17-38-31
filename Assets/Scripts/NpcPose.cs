using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Standing café moments for an NPC: leaning back against a wall, and stepping
/// up to the bookshelf to take a book. Also carries that book - in the hand
/// while walking, open on the lap while sitting - and leaves it on the table
/// or sofa when they get up to go.
///
/// SAME CONTRACT AS NpcSeating
///  * The brain walks the NPC to the moment's Stand Point and calls TryLean or
///    TryBrowse.
///  * From there this component owns the visible body: it walks the last step
///    by hand (up to maxStep, which may leave the NavMesh - a wall, a shelf)
///    and holds the pose. The NavMeshAgent stays parked on the Stand Point, so
///    avoidance and the brains' own checks still see a person standing there.
///  * The moment a brain gives the agent somewhere to walk, it steps back to
///    the Stand Point, straightens up and hands the agent back. No call needed.
///
/// Only the clips every café NPC already has are used (Idle, Walk, Interact),
/// so nothing needs baking. The lean is the whole body tipped back a few
/// degrees about the feet; a real lean clip can replace that later without
/// changing any of the bookkeeping.
/// </summary>
[DisallowMultipleComponent]
// After the brains (0), NpcSeating (60) and PersonalSpace (80), so this has the
// last word on the body each frame; after PolygonNpcVisual (150), so a carried
// book is placed on the hand you can actually see.
[DefaultExecutionOrder(160)]
public sealed class NpcPose : MonoBehaviour
{
    public enum Phase { Standing, StepIn, Holding, StepOut }

    [Tooltip("Walking speed while stepping between the navigation spot and the pose, metres per second.")]
    [SerializeField, Min(.2f)] private float stepSpeed = .9f;

    [Tooltip("Furthest the body is walked by hand off its navigation spot, metres. A pose further away is skipped.")]
    [SerializeField, Range(.2f, 1.5f)] private float maxStep = 1.1f;

    [Tooltip("How far a leaning body tips back against the wall, degrees, about the feet. The Pose Point " +
             "sets how far from the wall the feet are; together they decide whether the shoulders touch it.")]
    [SerializeField, Range(0f, 15f)] private float leanAngle = 6f;

    [Tooltip("Seconds at the shelf before the hand goes out for a book (the Interact clip).")]
    [SerializeField, Min(0f)] private float reachDelay = .35f;

    [Tooltip("Seconds into the reach when the book is in the hand.")]
    [SerializeField, Min(0f)] private float bookInHand = .55f;

    [Tooltip("An open book on a seated reader's lap tilts up towards their face by this much, degrees from flat.")]
    [SerializeField, Range(0f, 80f)] private float lapTilt = 35f;

    [Tooltip("Extra turn applied to the book, degrees, if the book model's own axes don't match " +
             "(the placeholder and the lounge's books are flat along their Y axis).")]
    [SerializeField] private Vector3 bookRotationOffset;

    [Tooltip("How long a book left on a table or sofa stays before it's tidied away, seconds.")]
    [SerializeField, Min(0f)] private float bookLingers = 45f;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int InteractHash = Animator.StringToHash("Interact");

    private NavMeshAgent agent;
    private Animator animator;
    private NpcSeating seating;
    private PolygonNpcVisual visual;
    private bool? hasWalking, hasInteract;

    private Phase phase = Phase.Standing;
    private CafeMoment moment;
    private bool browsing, reached, bookGiven;
    private Vector3 home;       // where the agent waits (the Stand Point it arrived on)
    private Vector3 spot;       // where the feet go for the pose
    private Vector3 legFrom;    // start of the step being walked
    private Quaternion holdRotation;
    private float phaseStarted, phaseLength;

    private GameObject book;
    private Transform bookRest;
    private bool bookToRest;
    private GameObject handsOn;
    private Transform handLeft, handRight;

    public Phase Current => phase;
    /// <summary>True from the first step towards the pose until the agent has been handed back.</summary>
    public bool Busy => phase != Phase.Standing;
    public bool CarryingBook => book != null && !bookToRest;
    public CafeMoment Moment => moment;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        seating = GetComponent<NpcSeating>();
        visual = GetComponent<PolygonNpcVisual>();
    }

    /// <summary>Lean against whatever is behind <paramref name="target"/>'s Pose Point.</summary>
    public bool TryLean(CafeMoment target) =>
        target != null && target.MomentKind == CafeMoment.Kind.Lean && Begin(target, false);

    /// <summary>Step up to the shelf, reach, and come away with a book.</summary>
    public bool TryBrowse(CafeMoment target) =>
        target != null && target.MomentKind == CafeMoment.Kind.Bookshelf && Begin(target, true);

    private bool Begin(CafeMoment target, bool browse)
    {
        if (phase != Phase.Standing) return false;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
        if (seating != null && seating.Busy) return false;

        Vector3 to = target.PosePosition;
        to.y = transform.position.y;
        if (Flat(to - transform.position).magnitude > maxStep) return false;
        if (!target.IsHeldBy(gameObject) && !target.Claim(gameObject)) return false;

        moment = target;
        browsing = browse;
        reached = bookGiven = false;
        home = transform.position;
        spot = to;

        Vector3 face = Flat(target.Facing);
        if (face.sqrMagnitude < 1e-4f) face = Flat(transform.forward);
        if (face.sqrMagnitude < 1e-4f) face = Vector3.forward;
        holdRotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        // Negative about the local X axis tips the top of the body backwards.
        if (!browse && leanAngle > 0f) holdRotation *= Quaternion.Euler(-leanAngle, 0f, 0f);

        // Park the navigation body on the stand point; the visible body moves by hand from here.
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updatePosition = false;
        agent.updateRotation = false;
        Enter(Phase.StepIn, Flat(spot - home).magnitude / stepSpeed);
        return true;
    }

    /// <summary>
    /// Leave the carried book at <paramref name="rest"/> (a table's cup spot, a
    /// sofa seat). A seated reader puts it down as they start to stand. Null
    /// just tidies it away.
    /// </summary>
    public void PutBookDown(Transform rest)
    {
        if (book == null) return;
        bookRest = rest;
        bookToRest = true;
        if (seating == null || !seating.IsSeated) LeaveBook();
    }

    private void Update()
    {
        if (phase == Phase.Standing) return;
        if (agent == null || !agent.isActiveAndEnabled) { HandBack(); return; }

        // A brain that gives the agent somewhere to go wants them on their
        // way. Hold the navigation body still until it is handed back.
        bool wantsToWalk = agent.isOnNavMesh && (agent.hasPath || agent.pathPending);
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        if (wantsToWalk && phase != Phase.StepOut)
            Enter(Phase.StepOut, Flat(home - transform.position).magnitude / stepSpeed);

        float t = phaseLength <= 1e-4f ? 1f : Mathf.Clamp01((Time.time - phaseStarted) / phaseLength);
        switch (phase)
        {
            case Phase.StepIn:
                Walk(spot, t, holdRotation);
                SetWalking(t < 1f && phaseLength > .05f);
                if (t >= 1f) Enter(Phase.Holding, 0f);
                break;

            case Phase.Holding:
                transform.SetPositionAndRotation(spot, holdRotation);
                if (browsing) Browse();
                break;

            case Phase.StepOut:
                Vector3 away = Flat(home - legFrom);
                Quaternion upright = away.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(away.normalized, Vector3.up)
                    : Quaternion.LookRotation(Upright(transform.forward), Vector3.up);
                Walk(home, t, upright);
                SetWalking(t < 1f && phaseLength > .05f);
                if (t >= 1f) HandBack();
                break;
        }
    }

    private void LateUpdate()
    {
        if (book == null) return;

        // Seated readers put the book down as they get up.
        if (bookToRest && (seating == null || !seating.IsSeated)) { LeaveBook(); return; }

        FindHands();
        Vector3 forward = Upright(transform.forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Quaternion offset = Quaternion.Euler(bookRotationOffset);

        if (seating != null && seating.IsSeated && handLeft != null && handRight != null)
        {
            // Open on the lap between the resting hands, tipped up towards the face.
            Quaternion tip = Quaternion.AngleAxis(-lapTilt, right);
            Vector3 up = tip * Vector3.up;
            Vector3 along = tip * forward;
            Vector3 at = (handLeft.position + handRight.position) * .5f + up * .03f;
            book.transform.SetPositionAndRotation(at, Quaternion.LookRotation(along, up) * offset);
        }
        else if (handRight != null)
        {
            // Carried at the side, gripped near the top edge.
            book.transform.SetPositionAndRotation(handRight.position - Vector3.up * .06f,
                                                  Quaternion.LookRotation(Vector3.up, right) * offset);
        }
        else
        {
            book.transform.SetPositionAndRotation(transform.position + Vector3.up + forward * .25f,
                                                  Quaternion.LookRotation(forward, Vector3.up) * offset);
        }
    }

    private void Browse()
    {
        float held = Time.time - phaseStarted;
        if (!reached && held >= reachDelay)
        {
            reached = true;
            if (HasParameter(ref hasInteract, InteractHash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(InteractHash);
        }
        if (!bookGiven && held >= reachDelay + bookInHand)
        {
            bookGiven = true;
            if (book == null) book = MakeBook(moment != null ? moment.BookTemplate : null);
        }
    }

    private void Enter(Phase next, float length)
    {
        phase = next;
        phaseStarted = Time.time;
        phaseLength = Mathf.Max(0f, length);
        legFrom = transform.position;
        if (next == Phase.Holding) SetWalking(false);
    }

    // Walk the current leg by hand, facing the way of travel and turning to
    // `arrive` over the last part of it.
    private void Walk(Vector3 to, float t, Quaternion arrive)
    {
        transform.position = Vector3.Lerp(legFrom, to, t);
        Vector3 travel = Flat(to - legFrom);
        Quaternion target = arrive;
        if (travel.sqrMagnitude > .0025f)
        {
            Quaternion facing = Quaternion.LookRotation(travel.normalized, Vector3.up);
            target = Quaternion.Slerp(facing, arrive, Mathf.InverseLerp(.6f, 1f, t));
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
    }

    // Returns the body to the navigation agent, which never left the stand point.
    private void HandBack()
    {
        if (phase == Phase.Standing) return;
        phase = Phase.Standing;
        transform.SetPositionAndRotation(home, Quaternion.LookRotation(Upright(transform.forward), Vector3.up));

        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh) agent.nextPosition = transform.position;
            agent.updatePosition = true;
            agent.updateRotation = true;
            if (agent.isOnNavMesh) agent.isStopped = !(agent.hasPath || agent.pathPending);
        }
        SetWalking(false);

        if (moment != null) moment.Release(gameObject);
        moment = null;
    }

    private void LeaveBook()
    {
        if (book == null) return;
        if (bookRest != null)
        {
            Vector3 f = Upright(bookRest.forward);
            Quaternion lying = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
            book.transform.SetPositionAndRotation(bookRest.position + Vector3.up * .02f,
                                                  lying * Quaternion.Euler(bookRotationOffset));
            Destroy(book, bookLingers);
        }
        else Destroy(book);

        book = null;
        bookRest = null;
        bookToRest = false;
    }

    private GameObject MakeBook(GameObject template)
    {
        GameObject made;
        if (template != null)
        {
            made = Instantiate(template);
            made.transform.localScale = template.transform.lossyScale;
        }
        else
        {
            made = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.transform.localScale = new Vector3(.15f, .03f, .21f);
            Renderer look = made.GetComponent<Renderer>();
            if (look != null) look.material.color = new Color(.55f, .2f, .15f);
        }
        made.name = name + " - book";

        // A prop, not something to bump into or aim at.
        foreach (Collider c in made.GetComponentsInChildren<Collider>(true)) Destroy(c);
        made.SetActive(true);
        return made;
    }

    // The hands you can see: the city body's when it has one, else the rig's.
    private void FindHands()
    {
        GameObject body = visual != null ? visual.VisualInstance : null;
        GameObject source = body != null ? body : gameObject;
        if (handsOn == source && handRight != null) return;

        handsOn = source;
        handLeft = FindDeep(source.transform, body != null ? "Hand_L" : "Wrist.L");
        handRight = FindDeep(source.transform, body != null ? "Hand_R" : "Wrist.R");
        if (handRight == null)
        {
            handLeft = FindDeep(transform, "Wrist.L");
            handRight = FindDeep(transform, "Wrist.R");
        }
    }

    private static Transform FindDeep(Transform root, string wanted)
    {
        if (root.name == wanted) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, wanted);
            if (found != null) return found;
        }
        return null;
    }

    private void SetWalking(bool walking)
    {
        if (HasParameter(ref hasWalking, IsWalkingHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(IsWalkingHash, walking);
    }

    private bool HasParameter(ref bool? cached, int hash, AnimatorControllerParameterType type)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        if (cached == null && animator.isInitialized)
        {
            cached = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == hash && p.type == type) { cached = true; break; }
        }
        return cached == true;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static Vector3 Upright(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
    }

    private void OnDisable()
    {
        if (phase != Phase.Standing) HandBack();
    }

    private void OnDestroy()
    {
        if (moment != null) moment.Release(gameObject);
        if (book != null) Destroy(book);
    }
}
