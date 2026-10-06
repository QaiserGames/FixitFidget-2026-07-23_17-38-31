using UnityEngine;

// ---------------------------------------------------------------------------
// ONE THING IN ACE'S HAND AT NIGHT (the man at the bins, 6 Oct 2026; claude/foundation-pass-build-plan.md §4,
// finding 4: the day's carry holds repair jobs only and hides itself once the day is over)
//
// The bin bag on Night 0, the gnome on the way back to the bins (and, later, the cones). One at a time, held in
// Ace's right hand: it hangs from the hand and swings with the arm as Ace walks, so no new pose is needed. In first
// person it sits low on the right of the view. It is put down by whatever takes it (the dumpster, the man), never
// dropped anywhere; at the night's end it is simply gone (the night's ledger says where the thing is).
//
// Made in code on Ace while playing; nothing in the scene changes. It moves the prop just before each camera
// renders (as PlayerCarry does), so it sits in the hand after the body's animation and the camera have moved.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightCarry : MonoBehaviour
{
    public static NightCarry Current { get; private set; }

    /// <summary>The id of what Ace holds ("bag", or a NightThings id), or "".</summary>
    public string HeldId { get; private set; } = "";
    public bool Holding => held != null;
    /// <summary>The prop in Ace's hand (null when empty).</summary>
    public GameObject Held => held;

    GameObject held;
    Vector3 hang;        // the prop's grip point below the hand, in the prop's own space
    bool facesAce;       // someone, not something (Barnaby): in first person he looks back at Ace
    Transform hand;
    AceBody body;
    Transform handOf;    // the body the hand was found on (it changes when a body is put on)
    CafeViewMode view;
    Camera cam;

    /// <summary>Ace's night carry (added to Ace the first time it's needed).</summary>
    public static NightCarry Ensure()
    {
        if (Current != null) return Current;
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (ace == null) return null;
        NightCarry carry = ace.GetComponent<NightCarry>();
        if (carry == null) carry = ace.gameObject.AddComponent<NightCarry>();
        return carry;
    }

    /// <summary>
    /// Ace takes <paramref name="prop"/> in the right hand (a copy of it is made; the scene's own object is left
    /// alone). <paramref name="scale"/> sizes the copy. Whatever Ace held before is put away.
    /// </summary>
    public GameObject Hold(GameObject prop, string id, float scale = 1f)
    {
        Drop();
        if (prop == null) return null;
        held = Instantiate(prop);
        held.name = "In Ace's hand: " + (string.IsNullOrEmpty(id) ? prop.name : id);
        held.SetActive(true);
        held.transform.localScale = prop.transform.lossyScale * Mathf.Max(.05f, scale);
        // Only what's drawn comes along: nothing in Ace's hand can be used, bumped into or lit by.
        foreach (Collider c in held.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (MonoBehaviour behaviour in held.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (Light l in held.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (Renderer r in held.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        // The grip: the top of what's drawn, a little in from the back (a bag by its knot, a gnome by its hat).
        Bounds b = BoundsOf(held);
        hang = held.transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y - Mathf.Min(.08f, b.size.y * .15f), b.center.z));
        HeldId = id ?? "";
        facesAce = HeldId == NightThings.GraceGnome;
        Place();
        TrophyShelf.RefreshAll();   // a thing taken back off the shelf isn't on it while Ace holds it
        return held;
    }

    /// <summary>Whatever Ace holds goes (put into the dumpster, handed over). Returns where it was.</summary>
    public Vector3 Drop() => Drop(true);

    Vector3 Drop(bool look)
    {
        Vector3 at = held != null ? held.transform.position : transform.position;
        bool had = held != null;
        if (held != null) Destroy(held);
        held = null;
        HeldId = "";
        if (had && look) TrophyShelf.RefreshAll();   // the night ended with it in hand: it's on the shelf
        return at;
    }

    void Awake()
    {
        Current = this;
        body = GetComponent<AceBody>();
        view = GetComponent<CafeViewMode>();
    }

    void OnEnable()
    {
        Current = this;
        Application.onBeforeRender += Place;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= Place;
        if (Current == this) Current = null;
    }

    void OnDestroy() => Drop(false);

    void LateUpdate()
    {
        // The night is over: nothing stays in Ace's hand by day.
        if (held != null && (NightWalk.Instance == null || !NightWalk.Instance.Active)) Drop();
    }

    void Place()
    {
        if (held == null) return;
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        bool firstPerson = view != null && view.FirstPersonSelected && view.WalkingFirstPerson && cam != null;
        bool show = firstPerson || view == null || view.ShowsAce;
        if (held.activeSelf != show) held.SetActive(show);
        if (!show) return;
        Quaternion facing = Quaternion.Euler(0f, body != null && body.Worn ? body.BodyYaw : transform.eulerAngles.y, 0f);
        if (firstPerson)
        {
            // Low on the right of the view, upright, the way Ace would hold it out of the way. Barnaby looks back at Ace,
            // three-quarters on toward the middle of the view, rather than showing the back of his hat (6 Oct 2026,
            // Mansoor's playtest: in first person "you can see inside the head"; the hat itself was inside out).
            Vector3 at = cam.ViewportToWorldPoint(new Vector3(.74f, .14f, .7f));
            held.transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y + (facesAce ? 215f : 0f), 0f);
            held.transform.position += at - held.transform.TransformPoint(hang) + Vector3.up * .1f;
            return;
        }
        Transform grip = Hand();
        held.transform.rotation = facing;
        // In the hand: hanging straight down from it (a bag, a gnome held by the hat), so it swings with the arm.
        Vector3 point = grip != null ? grip.position : transform.TransformPoint(new Vector3(.45f, -.15f, .1f));
        held.transform.position += point - held.transform.TransformPoint(hang);
    }

    // The right hand of whichever body Ace wears (the Sidekick's Humanoid bone, or the stand-in's city look), or
    // null for the capsule: looked up again whenever the body changes.
    Transform Hand()
    {
        Transform root = body != null && body.Worn ? body.RigRoot : null;
        if (root == handOf && (hand != null || root == null)) return hand;
        handOf = root;
        hand = null;
        if (root == null) return null;
        if (body.WearsSidekick)
        {
            Animator a = root.GetComponentInChildren<Animator>();
            if (a != null && a.isHuman) hand = a.GetBoneTransform(HumanBodyBones.RightHand);
        }
        else if (body.Visual != null) hand = body.Visual.CityHand(false);
        return hand;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * .3f);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public string Describe() => held == null ? "Night carry: empty." :
        $"Night carry: {HeldId} in Ace's {(hand != null ? "right hand" : "side (no hand bone)")}.";
}
