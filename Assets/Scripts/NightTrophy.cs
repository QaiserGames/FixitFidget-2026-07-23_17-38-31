using UnityEngine;

// ---------------------------------------------------------------------------
// A THING ACE CAN TAKE AT NIGHT (the Night 1 slice; its words are in NightThings)
//
// Part of the scene, in the street: Grace's gnome by her front step (Fixit Fidget > Night > Night 1 -
// Put Grace's gnome and Ace's trophy shelf in the scene). By day it's scenery. At night Ace can take
// it (E, or A / Cross): it goes into the night's ledger, off the street for good (by day too, from
// then on) and onto Ace's shelf in the café (TrophyShelf). Ace notes it down (once Ace knows whose it
// is: otherwise the morning does that), and its owner will come in the next morning to tell Ace all
// about it (MorningFace).
//
// The prompt names it the way Ace knows it: "Take Barnaby" once Grace has mentioned him, "Take the
// garden gnome" before. While it's the thing E would take, a soft light catches it (small things are
// hard to see from above at night).
//
// The man at the bins (6 Oct 2026): when it's the thing he asked for (LodgerStory.Errand), Ace carries it in hand
// (NightCarry) back to the bins to give him, and mutters the night's line; it only goes on Ace's shelf if the night
// ends with it still in hand. Grace's morning is the same either way: it was Ace who took it.
//
// Indoors (Grace's cups on her kitchen worktop, session 3), a thing needs to be seen to be taken (Needs Sight): from
// overhead the interactor offers whatever is nearest within reach, which would include the cups through her kitchen
// wall. One ray from Ace's eyes to the thing, only while it's a candidate (Ace within reach), once a frame at most;
// anything that stops the ray right at the thing (its box, the worktop) doesn't hide it.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightTrophy : NightInteractable
{
    [Tooltip("The thing's id in NightThings. Saved: never change it once saves exist.")]
    public string thingId = NightThings.GraceGnome;
    [Tooltip("What's seen in the street. Hidden once it has been taken.")]
    public GameObject visual;
    [Tooltip("Only offered when Ace can see it: a thing indoors is never taken through a wall.")]
    public bool needsSight;

    NightThing thing;
    NightLedger ledger;
    bool listening;
    Light glint;

    public NightThing Thing => thing ??= NightThings.Find(thingId);
    public bool Taken => Ledger != null && Ledger.HasTrophy(thingId);

    NightLedger Ledger
    {
        get
        {
            if (ledger == null && SaveManager.Instance != null) ledger = SaveManager.Instance.Night;
            return ledger;
        }
    }

    protected override bool AvailableTonight => Thing != null && !Taken && (!needsSight || Seen);

    // ---------- line of sight ----------

    // A ray that stops this close to the thing has reached it (its own box, the worktop under it).
    const float NearEnough = .3f;
    // Ace's eyes above Ace's middle (the capsule's middle is 1 m up; the eyes about 1.55 m).
    const float EyeAboveMiddle = .55f;

    static Transform ace;
    int seenFrame = -1;
    bool seen;
    bool haveSightPoint;
    Vector3 sightPoint;

    /// <summary>Ace can see it from where Ace stands now (always true unless it needs sight).</summary>
    public bool Seen
    {
        get
        {
            if (!needsSight) return true;
            if (seenFrame == Time.frameCount) return seen;
            seenFrame = Time.frameCount;
            seen = LineOfSight();
            return seen;
        }
    }

    bool LineOfSight()
    {
        if (ace == null)
        {
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
            ace = player != null ? player.transform : null;
        }
        if (ace == null) return true;
        Vector3 target = SightPoint;
        Vector3 eye = ace.position + Vector3.up * EyeAboveMiddle;
        if (!Physics.Linecast(eye, target, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        if (hit.collider.transform.IsChildOf(transform)) return true;
        return (hit.point - target).sqrMagnitude <= NearEnough * NearEnough;
    }

    // The middle of what's seen (it never moves: worked out once).
    Vector3 SightPoint
    {
        get
        {
            if (haveSightPoint) return sightPoint;
            sightPoint = transform.position + Vector3.up * .1f;
            if (visual == null || !visual.activeInHierarchy) return sightPoint;
            haveSightPoint = true;
            Renderer[] parts = visual.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0) return sightPoint;
            Bounds b = parts[0].bounds;
            foreach (Renderer r in parts) b.Encapsulate(r.bounds);
            sightPoint = b.center;
            return sightPoint;
        }
    }

    // The interactor reads the prompt every frame while Ace is near: made again only when the name changes.
    string promptFor, prompt = "";

    public override string Prompt
    {
        get
        {
            if (Thing == null) return "";
            Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
            string name = notebook != null && notebook.Knows(Thing.id) ? Thing.name : Thing.unknownName;
            if (!ReferenceEquals(name, promptFor)) { promptFor = name; prompt = "Take " + name; }
            return prompt;
        }
    }

    void OnEnable()
    {
        Listen();
        Refresh();
    }

    // The save manager may wake after this does.
    void Start()
    {
        Listen();
        Refresh();
    }

    void OnDisable()
    {
        if (listening && ledger != null) ledger.Changed -= Refresh;
        listening = false;
    }

    void Listen()
    {
        if (listening || Ledger == null) return;
        ledger.Changed += Refresh;
        listening = true;
    }

    /// <summary>Show it in the street, or not once it's been taken.</summary>
    public void Refresh()
    {
        bool here = !Taken;
        if (visual != null && visual.activeSelf != here) visual.SetActive(here);
        foreach (Collider found in GetComponents<Collider>()) found.enabled = here;
    }

    public override void SetFocused(bool focused)
    {
        if (focused && glint == null && Application.isPlaying)
        {
            var go = new GameObject("Glint (while it can be taken)") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, .95f, .55f);
            glint = go.AddComponent<Light>();
            glint.type = LightType.Point;
            glint.range = 1.8f;
            glint.intensity = 1.4f;
            glint.color = new Color(1f, .9f, .72f);
            glint.shadows = LightShadows.None;
        }
        if (glint != null) glint.enabled = focused && !Taken;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable || Ledger == null || Thing == null) return;
        int night = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        bool heardOfIt = notebook != null && notebook.Knows(Thing.id);
        bool forHim = LodgerStory.Errand(Ledger) == thingId;
        if (!Ledger.Take(thingId, Thing.owner, night)) return;
        if (heardOfIt) NotebookHooks.TookAtNight(Thing, forHim);
        Sfx.Play("night.take", transform.position + Vector3.up * .2f);
        NightCarry carry = forHim && visual != null ? NightCarry.Ensure() : null;
        if (carry == null)
        {
            NightCycle.Note(heardOfIt ? Thing.takenNote : Thing.takenNoteUnknown);
            return;
        }
        // His errand: in Ace's hand, back to the bins.
        carry.Hold(visual, thingId);
        NightCycle.Note($"Bring {(heardOfIt ? Thing.name : Thing.unknownName)} back to the man at the bins.");
        // Ace's line of the night for this favour ("It's a gnome. It's just a gnome.").
        NightLines lines = NightLines.Current;
        LodgerStory.Favour favour = LodgerStory.FindFavour(thingId);
        NightLines.Line mine = lines != null ? lines.FindLine(favour != null && favour.aceLine.Length > 0 ? favour.aceLine : LodgerStory.AceNightOne) : null;
        if (mine != null) Barks.SayAce(mine.text);
    }
}
