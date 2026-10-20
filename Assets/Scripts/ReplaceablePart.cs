using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// A PART THAT IS REPLACED: THE TWO-HOLD SWAP (the forgiving bench, 14 Oct 2026; specs/systems-forgiving-bench.md §4.3)
//
// His playtest 5: "i kept dropping a part of the camera under the camera, idk it was going through the table, through the
// item". The broken part used to be carried in the tweezers at the depth it was pinched at (on screen over the tray, in 3D
// over or behind the device) and dropped where it was let go, a rigidbody that fell straight down; the fresh one had to be
// carried and let go within 1.6 cm of its seat. His decision 2 (14 Oct): no carrying, no dropping, no depth.
//
// THE BROKEN PART (this component, in its seat under its cover). Hold the tweezers on it: a quarter second in they pinch
// (part.pinch on the press), and it lifts out with a wobble (part.lift) and flies into the old-parts tray by itself, landing
// with part.drop, whether or not the button is still down: one short hold commits it. Let go before the pinch takes and it
// keeps its progress, sinking back slowly. Scrap can't be picked up again (it is finished with).
//
// THE FRESH PART (LoosePart, below), waiting on the mat's new-part pad from the moment the close-up opens. One mechanism that
// is both his "click, then hold" and UI/UX's "one hold": any press on it (with the tweezers, while the seat is empty and faces
// the camera) starts its flight to hover just over its seat (part.fresh on the press frame). If the button is still down when
// it arrives, the same hold presses it in (0.35 s: it sinks most of the way, the last part a snap; part.replace and a
// sparkle). If it was let go, it waits there, hovering and bobbing, and a hold on it (or on its seat) presses it in. Let go
// mid-press and it keeps its progress, rising back slowly. Only the seat makes the part replaced: the grade can't jump early.
//
// Activate does the whole swap by itself (the labs' hand, the Day 1 guide): the pinch, the lift, the flight to the tray, the
// fresh part's flight, a self-timed press-in. In edit mode (the editor's checks: no frames) the flights complete at once.
//
// A COVERED PART SAYS WHY (playtest 4, 10 Oct; his call 12 Oct): the hover names only what the player does: the part is HELD IN
// by the cover, and the cover comes off first (CoveredHint).
// ---------------------------------------------------------------------------
public class ReplaceablePart : BenchInteractable
{
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private GameObject freshVisual;
    [SerializeField] private RemovablePart coveredBy;
    [SerializeField] private string partName = "Cracked screen";

    [Header("The tweezers (v2)")]
    [Tooltip("Seconds the tweezers are held before the pinch takes and the part lifts.")]
    [SerializeField, Min(.05f)] private float pinchDuration = .25f;

    /// <summary>InPlace: in its seat. HeldBroken: lifting out and flying to the old-parts tray. Removed: in the tray, the seat
    /// empty. Replaced: the fresh part is in; this one is scrap in the tray.</summary>
    public enum PartState { InPlace, HeldBroken, Removed, Replaced }

    /// <summary>The lift out of the seat (§4.3: 0.25 s, by 0.6 of its thickness and 0.15 of its short side, wobbling) and the hop
    /// in the tray (0.05 of its short side).</summary>
    public const float LiftSeconds = .25f, LiftThickness = .6f, LiftShortSide = .15f, HopShare = .05f;

    public PartState State { get; private set; } = PartState.InPlace;
    public bool IsReplaced { get; private set; }
    public LoosePart Fresh => fresh;
    public RemovablePart CoveredBy => coveredBy;
    /// <summary>The tweezers' pinch so far, 0..1 (kept when let go early, sinking back at 0.5 a second).</summary>
    public float Pinch => pinch;

    private float pinch;
    private bool pinching;                 // a hold is on it this frame (the pinch doesn't sink back while held)
    private Transform seatParent;
    private Vector3 seatLocalPos;
    private Quaternion seatLocalRot;
    private Vector3 freshLocalPos;
    private Quaternion freshLocalRot;
    private Vector3 freshLocalScale;
    private Transform freshParent;
    private Vector3 outsideOnPart = Vector3.up;     // its outside, in its own frame (it lies outside up in the tray)
    private Vector3 freshOutside = Vector3.up;      // the fresh part's outside, in its own frame (it lies outside up on the pad)
    private JobBase job;
    private LoosePart fresh;
    private bool busy;

    private bool awoke;

    protected override void Awake()
    {
        if (awoke) return;
        awoke = true;
        base.Awake();
        job = GetComponentInParent<JobBase>();
        seatParent = transform.parent;
        seatLocalPos = transform.localPosition;
        seatLocalRot = transform.localRotation;
        // Which way is out of the device from this seat: from the device's middle through the part, along the part's thinnest
        // axis (the tweezers come in from that side; the cover it sits under may be on the other face).
        Transform device = job != null ? job.transform : seatParent;
        Vector3 away = transform.position - (device != null ? device.position : transform.position - transform.up * .01f);
        Vector3 thin = ThinAxisWorld();
        Vector3 outward = Vector3.Dot(away, thin) >= 0f ? thin : -thin;
        outwardLocal = seatParent != null ? seatParent.InverseTransformDirection(outward) : outward;
        outsideOnPart = transform.InverseTransformDirection(outward);
        if (brokenVisual != null) brokenVisual.SetActive(true);
        if (freshVisual != null)
        {
            Transform f = freshVisual.transform;
            if (seatParent != null && (f.parent == transform || f.parent != null && f.parent.IsChildOf(transform)))
            {
                // The fresh visual is authored as a child of the broken part (Grace's camera: the working blade under the
                // jammed mechanism). The broken part leaves the device when it is pinched out, so the fresh one must seat
                // into the device, not into the scrap in the tray: its seat is remembered in the part's parent's frame.
                freshParent = seatParent;
                freshLocalPos = seatParent.InverseTransformPoint(f.position);
                freshLocalRot = Quaternion.Inverse(seatParent.rotation) * f.rotation;
                Vector3 ls = f.lossyScale, ps = seatParent.lossyScale;
                freshLocalScale = new Vector3(ls.x / Mathf.Max(1e-6f, ps.x), ls.y / Mathf.Max(1e-6f, ps.y), ls.z / Mathf.Max(1e-6f, ps.z));
            }
            else
            {
                freshParent = f.parent;
                freshLocalPos = f.localPosition;
                freshLocalRot = f.localRotation;
                freshLocalScale = f.localScale;
            }
            freshOutside = f.InverseTransformDirection(outward);
            freshVisual.SetActive(false);
        }
        // Nothing at the bench is physical any more (rule 7): an older prefab's rigidbody goes.
        var body = GetComponent<Rigidbody>();
        if (body != null) { if (Application.isPlaying) Destroy(body); else DestroyImmediate(body); }
    }

    private Vector3 outwardLocal = Vector3.up;

    private Vector3 ThinAxisWorld()
    {
        Vector3 s = transform.lossyScale;
        var box = GetComponent<BoxCollider>();
        if (box != null) s = Vector3.Scale(box.size, s);
        if (s.y <= s.x && s.y <= s.z) return transform.up;
        if (s.x <= s.z) return transform.right;
        return transform.forward;
    }

    /// <summary>The seat's centre in the world.</summary>
    public Vector3 SeatPosition => seatParent != null ? seatParent.TransformPoint(seatLocalPos) : transform.position;
    /// <summary>Out of the device from the seat, in the world.</summary>
    public Vector3 Outward => seatParent != null ? seatParent.TransformDirection(outwardLocal).normalized : transform.up;
    /// <summary>Its outside in its own frame (it lies outside up in the old-parts tray).</summary>
    public Vector3 OutsideLocal => outsideOnPart;
    /// <summary>The fresh part's outside in the fresh part's own frame.</summary>
    public Vector3 FreshOutsideLocal => freshOutside;
    /// <summary>The job it belongs to (remembered while it is off the device).</summary>
    public JobBase Job => job;
    private bool Uncovered => coveredBy == null || coveredBy.IsRemoved;

    /// <summary>The seat faces the camera (the fresh part can fly there and be pressed in only onto a face the player sees).</summary>
    public bool SeatFacesCamera(Camera camera)
    {
        if (camera == null) camera = Camera.main;
        if (camera == null) return true;
        return Vector3.Dot(Outward, (camera.transform.position - SeatPosition).normalized) > BenchRules.FacingDot;
    }

    /// <summary>The seat is empty: the broken part is out (flying to the tray or in it) and the fresh one isn't in yet.</summary>
    public bool SeatEmpty => !IsReplaced && (State == PartState.Removed || State == PartState.HeldBroken);

    // In its seat: reachable once whatever covers it has been lifted off. Out of it: scrap (§2's decision 15: it can't be moved).
    public override bool CanInteract => !busy && State == PartState.InPlace && !IsReplaced && Uncovered;

    /// <summary>Out of the device: flying to the tray, in it, or scrap once the fresh part is in.</summary>
    public bool Loose => State != PartState.InPlace;

    public override string DisplayName => IsReplaced ? "Old " + partName.ToLowerInvariant() : partName;

    /// <summary>
    /// The part's name without its fault word: "Cracked screen" → "Screen", "Jammed shutter mechanism" → "Shutter mechanism",
    /// "Old cracked screen" → "Screen", "Mainspring" → "Mainspring". The fresh part is named with it (LoosePart: "New screen";
    /// 19 Oct 2026, the fresh-eyes review: "New cracked screen" read as another broken one to a newcomer), and so is a missed
    /// swap ("fit the new screen"). The broken one keeps its fault word: it is cracked.
    /// </summary>
    public static string Honest(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "part";
        string[] words = name.Trim().Split(' ');
        int from = 0;
        while (from < words.Length - 1 && IsFaultWord(words[from])) from++;
        string rest = string.Join(" ", words, from, words.Length - from).Trim();
        if (rest.Length == 0) return name.Trim();
        return char.ToUpperInvariant(rest[0]) + rest.Substring(1);
    }

    static readonly string[] FaultWords =
    {
        "old", "cracked", "jammed", "broken", "dead", "seized", "worn", "burnt", "burned", "bent", "torn", "faulty", "smashed",
        "shattered", "stuck", "snapped", "blown", "corroded", "rusted", "rusty", "frayed", "damaged", "scratched", "dirty", "split",
        "loose", "leaking", "flat", "swollen", "cut", "melted", "warped", "chipped"
    };

    static bool IsFaultWord(string word)
    {
        string w = word.ToLowerInvariant().Trim(',', '.');
        foreach (string f in FaultWords) if (w == f) return true;
        return false;
    }
    public override string Prompt => State != PartState.InPlace ? ""
        : coveredBy != null && !coveredBy.IsRemoved ? CoveredHint(partName, coveredBy.DisplayName)
        : "Hold to pinch it out";

    /// <summary>
    /// Why a part waits on the cover over it, in plain words under 90 characters, naming only what the player does (12 Oct,
    /// his call on the veteran's note: the old words named a cable no verb touches): a screen is held in by the back; a
    /// spring, a mechanism, a battery or a board sits inside; anything else sits under the cover. The labs and the steps read
    /// the words from here. PLACEHOLDER COPY (his sister's to rewrite).
    /// </summary>
    public static string CoveredHint(string part, string cover)
    {
        // 14 Oct (the integration; review m2, T7): one short form for every part, "Under the back cover", 4 words, so the
        // hover over a covered part keeps the bench screen inside its 24-word worst case. The name above it says which part.
        string c = string.IsNullOrEmpty(cover) ? "cover" : cover.ToLowerInvariant();
        return $"Under the {c}";
    }
    public override ToolType RequiredTool => ToolType.Tweezers;
    public override bool Holdable => State == PartState.InPlace;
    public override bool Grabbable => false;
    public override float HoldProgress => State == PartState.InPlace ? pinch : 1f;
    public override Vector3 WorkPoint => BoundsCentre(transform) + Outward * .002f;
    public override Vector3 WorkNormal => State == PartState.InPlace ? Outward : transform.TransformDirection(outsideOnPart).normalized;

    /// <summary>The fresh part is put on the new-part pad the first time the device is worked on (no word pop: the pad and the
    /// glow say it now, §3 decision 14).</summary>
    public void PresentFresh()
    {
        if (!gameObject.activeInHierarchy && Application.isPlaying) return;   // not this job's part (its fault is off): no stock for it
        if (!awoke) Awake();   // the editor's checks call in before Awake has run
        if (fresh != null || freshVisual == null || IsReplaced) return;
        fresh = LoosePart.Make(this, freshVisual);
        // Stock, not a piece of the device: it never stops the device being handed back (the gate is reassembly).
        if (job != null) job.RegisterLoose(freshVisual);
    }

    // ---------- the broken part: the pinch, the lift, the flight to the old-parts tray ----------

    public override void HoldBegin(BenchHand hand)
    {
        if (State != PartState.InPlace) return;
        pinching = true;
        if (pinch <= 0f) Sfx.Play("part.pinch", hand.hitSomething ? hand.hit.point : transform.position);
    }

    public override bool HoldTick(BenchHand hand)
    {
        if (State != PartState.InPlace) return true;
        pinching = true;
        pinch = Mathf.Clamp01(pinch + hand.deltaTime / pinchDuration);
        if (job != null) BenchWork.NoteProgress(job);
        if (pinch < 1f) return false;
        Lift();
        return true;      // the hold ends by itself: the part goes to the tray whether or not the button is still down
    }

    public override void HoldEnd(BenchHand hand, bool finished) => pinching = false;

    // A pinch let go early keeps its progress, sinking back at 0.5 a second (§4.3; it used to reset to nothing).
    private void Update()
    {
        if (!pinching && pinch > 0f && State == PartState.InPlace)
            pinch = Mathf.Max(0f, pinch - BenchRules.KeptProgressDecay * Time.deltaTime);
        pinching = false;
    }

    private void Lift()
    {
        transform.SetParent(null, true);
        // Out of its seat the device has a hole in it: not handable until the fresh part is in (then this is scrap).
        if (job != null) job.RegisterDetached(gameObject);
        State = PartState.HeldBroken;
        pinch = 1f;
        Sfx.Play("part.lift", transform.position);
        if (job != null) BenchWork.NoteTargetDone(job, this);
        Bounds b = BenchStage.Instance != null ? BenchStage.Instance.LocalBox(this) : BenchStage.Measure(transform);
        Vector3 s = b.size;
        float thickness = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float shortSide = Mathf.Max(.002f, s.x + s.y + s.z - thickness - Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
        BenchStage.SendToPlace(this, job, BenchStage.SlotKind.Scrap, BenchStage.FlightKind.Part,
            0f, LiftSeconds, Outward * (LiftThickness * thickness + LiftShortSide * shortSide), null, true,
            null, "part.drop", HopShare * shortSide, InTheTray);
    }

    private void InTheTray()
    {
        if (State == PartState.HeldBroken) State = PartState.Removed;    // (the fresh part may already be in: then it stays Replaced)
    }

    // ---------- the fresh part seats ----------

    /// <summary>The fresh part's pose over its seat, raised along the outside by half its thickness and a tenth of its short side
    /// (§4.3), in the frame it seats into.</summary>
    public void HoverPose(out Transform frame, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (!awoke) Awake();
        frame = freshParent;
        Bounds b = fresh != null ? (BenchStage.Instance != null ? BenchStage.Instance.LocalBox(fresh) : BenchStage.Measure(fresh.transform)) : new Bounds(Vector3.zero, Vector3.one * .01f);
        Vector3 s = b.size;
        float thickness = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float shortSide = Mathf.Max(.002f, s.x + s.y + s.z - thickness - Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
        Vector3 seatWorld = freshParent != null ? freshParent.TransformPoint(freshLocalPos) : freshLocalPos;
        Vector3 hoverWorld = seatWorld + Outward * (.5f * thickness + .1f * shortSide);
        localPosition = freshParent != null ? freshParent.InverseTransformPoint(hoverWorld) : hoverWorld;
        localRotation = freshLocalRot;
    }

    /// <summary>The fresh part's seat, in the frame it seats into.</summary>
    public void SeatPose(out Transform frame, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (!awoke) Awake();
        frame = freshParent;
        localPosition = freshLocalPos;
        localRotation = freshLocalRot;
    }

    /// <summary>The fresh part goes into the seat: the part is replaced.</summary>
    public void SeatFresh()
    {
        if (!awoke) Awake();
        if (IsReplaced || freshVisual == null) return;
        if (BenchStage.Instance != null && fresh != null) BenchStage.Instance.Release(fresh);
        Component freshTarget = fresh != null ? (Component)fresh : this;
        if (fresh != null) fresh.Seated();
        if (job != null)
        {
            job.UnregisterLoose(freshVisual);
            job.RegisterLoose(gameObject);      // the broken part is scrap now: owned for cleanup, no longer a hole
        }
        Transform f = freshVisual.transform;
        f.SetParent(freshParent, false);
        f.localPosition = freshLocalPos;
        f.localRotation = freshLocalRot;
        f.localScale = freshLocalScale;
        freshVisual.SetActive(true);
        IsReplaced = true;
        State = PartState.Replaced;
        Juice.Sparkle(f.position);   // in it goes: a little burst of sparks
        Sfx.Play("part.replace", f.position);   // "pull-out click, then magnetic snap"
        BenchFeel.Pulse(.45f, .60f, .06f);
        if (job != null) BenchWork.NoteTargetDone(job, freshTarget);
        // Grace's camera: its new shutter fires once, so you hear the camera work again. Unless this seat was its last task:
        // then the camera wakes (DeviceWake winds it and fires the shutter) and one shutter is the beat, not two.
        GraceCameraRepairJob camera = (freshParent != null ? freshParent.GetComponentInParent<GraceCameraRepairJob>() : null);
        if (camera != null && camera.Shutter == this && !(Application.isPlaying && camera.Quality >= .999f && camera.GetComponent<DeviceWake>() != null))
            Sfx.PlayLater("camera.shutter", f.position, .45f);
    }

    // ---------- the whole swap by itself (the labs' hand, the Day 1 guide, older callers) ----------

    public override void Activate()
    {
        if (!awoke) Awake();
        if (!CanInteract) return;
        if (!Application.isPlaying) { PresentFresh(); Lift(); SeatFresh(); return; }   // the editor's checks: no frames to animate over
        StartCoroutine(SwapBySelf());
    }

    private IEnumerator SwapBySelf()
    {
        busy = true;
        PresentFresh();
        Sfx.Play("part.pinch", transform.position);
        float t = 0f;
        while (t < pinchDuration) { t += Time.deltaTime; pinch = Mathf.Clamp01(t / pinchDuration); yield return null; }
        busy = false;
        Lift();
        // The fresh one's flight and its press-in, self-timed (the same beats a player's one hold makes).
        if (fresh != null) yield return fresh.SeatBySelf();
        else SeatFresh();
    }
}

// ---------------------------------------------------------------------------
// THE FRESH PART on the mat's new-part pad: one press flies it over its seat; a held press (or a hold on it hovering) presses
// it in (§4.3). Never a rigidbody, never dropped.
// ---------------------------------------------------------------------------
public class LoosePart : BenchInteractable
{
    private ReplaceablePart part;
    private bool seated, flying, hovering, holding;
    private float press;                 // 0..1: the press-in so far (kept when let go, rising back at 0.5 a second)
    private float bobT;

    /// <summary>Its hover's gentle bob (§4.3: ±0.04 of its thickness at 2 Hz).</summary>
    public const float BobShare = .04f, BobHz = 2f;

    public static LoosePart Make(ReplaceablePart part, GameObject freshVisual)
    {
        freshVisual.transform.SetParent(null, true);
        freshVisual.SetActive(true);
        // A collider for the cursor's ray (the surface point, the sight line): the hit itself is the picker's (its outline).
        if (freshVisual.GetComponent<Collider>() == null)
        {
            var box = freshVisual.AddComponent<BoxCollider>();
            var mf = freshVisual.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
        }
        var body = freshVisual.GetComponent<Rigidbody>();
        if (body != null) { if (Application.isPlaying) Destroy(body); else DestroyImmediate(body); }
        var loose = freshVisual.GetComponent<LoosePart>();
        if (loose == null) loose = freshVisual.AddComponent<LoosePart>();
        loose.part = part;
        // On the smaller pad it fits, flat, a touch askew, clear of every resting quarter turn of its device (§4.0.1). Laid
        // there at once: the close-up is just opening, and it is stock that was always there.
        BenchStage.PlaceNow(loose, part != null ? part.Job : null, BenchStage.SlotKind.Pad);
        return loose;
    }

    public ReplaceablePart Part => part;
    /// <summary>Its outside in its own frame (it lies outside up on the pad).</summary>
    public Vector3 OutsideLocal => part != null ? part.FreshOutsideLocal : Vector3.up;
    /// <summary>Flying to its seat, hovering over it, or being pressed in.</summary>
    public bool Flying => flying;
    public bool Hovering => hovering && !flying;
    public bool Pressing => hovering && press > 0f;
    /// <summary>The press-in so far, 0..1.</summary>
    public float PressProgress => press;
    /// <summary>On its pad, waiting.</summary>
    public bool OnPad => !seated && !flying && !hovering;

    /// <summary>"New screen", "New shutter mechanism": the part's name without its fault word (ReplaceablePart.Honest; 19 Oct:
    /// "New cracked screen" told a newcomer she had picked another broken one).</summary>
    public override string DisplayName => part != null ? "New " + ReplaceablePart.Honest(part.DisplayName).ToLowerInvariant() : "New part";
    // Its own "why not" when it can't act; otherwise the hover is its name only (UI/UX §5.10 item 7).
    public override string Prompt => part == null ? ""
        : part.State == ReplaceablePart.PartState.InPlace ? "The broken one comes out first"
        : !part.SeatFacesCamera(null) ? "Flip it over first"
        : hovering ? "Hold to press it in" : "Click to send it over";
    /// <summary>The seat is empty (the broken one is out): seating this is the step to do now (the aim help's order, 12 Oct).</summary>
    public bool SeatEmpty => part != null && part.SeatEmpty;
    public override ToolType RequiredTool => ToolType.Tweezers;
    public override bool CanInteract => !seated && part != null && !part.IsReplaced && part.SeatEmpty && part.SeatFacesCamera(null);
    public override bool Holdable => true;
    public override float HoldProgress => hovering ? press : 0f;
    public override Vector3 WorkNormal => part != null ? part.Outward : Vector3.up;

    // ---------- the press: one mechanism, a click or a hold ----------

    public override void HoldBegin(BenchHand hand)
    {
        holding = true;
        if (OnPad && CanInteract) SendOver();
    }

    public override bool HoldTick(BenchHand hand)
    {
        holding = true;
        if (seated) return true;
        if (OnPad) { if (CanInteract) SendOver(); return false; }
        if (flying) return false;                              // on its way: the hold carries on into the press-in
        if (!part.SeatFacesCamera(hand.camera)) return false;
        press = Mathf.Clamp01(press + hand.deltaTime / BenchRules.PressInSeconds);
        PoseOverSeat();
        if (part.Job != null) BenchWork.NoteProgress(part.Job);
        if (press < 1f) return false;
        part.SeatFresh();
        return true;
    }

    public override void HoldEnd(BenchHand hand, bool finished) => holding = false;

    // Off the pad, over to its seat: part.fresh on this (the press) frame; the flight never blocks the next input.
    private void SendOver()
    {
        if (part == null || seated) return;
        part.HoverPose(out Transform frame, out Vector3 local, out Quaternion rotation);
        flying = true;
        hovering = false;
        press = 0f;
        BenchStage.SendTo(this, part.Job, frame, local, rotation, BenchStage.FlightKind.Fresh, 0f, "part.fresh", null, frame, Arrived);
    }

    private void Arrived()
    {
        flying = false;
        hovering = true;
        bobT = 0f;
        if (part != null && part.Job != null) BenchWork.NoteProgress(part.Job);
    }

    // Hovering: bobbing gently while nobody presses; pressed, sinking toward the seat, the last 15 % a snap at the end.
    private void Update()
    {
        if (seated || !hovering || flying || part == null) { holding = false; return; }
        if (!holding && press > 0f) press = Mathf.Max(0f, press - BenchRules.KeptProgressDecay * Time.deltaTime);
        bobT += Time.deltaTime;
        PoseOverSeat();
        holding = false;
    }

    private void PoseOverSeat()
    {
        part.HoverPose(out Transform frame, out Vector3 hoverLocal, out Quaternion rotation);
        part.SeatPose(out _, out Vector3 seatLocal, out _);
        Vector3 local = Vector3.Lerp(hoverLocal, seatLocal, (1f - BenchRules.PressSnapShare) * press);
        Vector3 world = frame != null ? frame.TransformPoint(local) : local;
        if (press <= 0f)
        {
            Bounds b = BenchStage.Instance != null ? BenchStage.Instance.LocalBox(this) : BenchStage.Measure(transform);
            float thickness = Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z));
            world += part.Outward * (BobShare * thickness * Mathf.Sin(bobT * BobHz * 2f * Mathf.PI));
        }
        transform.SetPositionAndRotation(world, frame != null ? frame.rotation * rotation : rotation);
    }

    /// <summary>The part has been seated: this behaviour's work is done (the fresh visual stays as the part).</summary>
    public void Seated()
    {
        SetHighlight(false);   // the outline off before this component goes; ItemInspector can't reach a destroyed one (review-bench m1)
        seated = true;
        flying = hovering = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        // The fresh visual is the part now; nothing more to pick up. (The editor's checks seat parts in edit mode.)
        if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
    }

    /// <summary>Back on its pad at once (the close-up came down while it hovered, or its stock was put away and is back).</summary>
    public void BackOnItsPad()
    {
        if (seated) return;
        flying = hovering = false;
        press = 0f;
        BenchStage.PlaceNow(this, part != null ? part.Job : null, BenchStage.SlotKind.Pad);
    }

    /// <summary>The labs' hand and the Day 1 guide's: over to its seat, then a self-timed press-in.</summary>
    public IEnumerator SeatBySelf()
    {
        if (part == null || seated) yield break;
        while (!part.SeatEmpty && !part.IsReplaced) yield return null;
        if (OnPad) SendOver();
        while (flying) yield return null;
        while (!seated && part != null && !part.IsReplaced)
        {
            press = Mathf.Clamp01(press + Time.deltaTime / BenchRules.PressInSeconds);
            holding = true;
            PoseOverSeat();
            if (press >= 1f) { part.SeatFresh(); yield break; }
            yield return null;
        }
    }

    public override void Activate()
    {
        // A press without a hold (the labs): over to the seat and in, if the seat is empty.
        if (part == null || !part.SeatEmpty) return;
        if (!Application.isPlaying) { part.SeatFresh(); return; }
        StartCoroutine(SeatBySelf());
    }
}
