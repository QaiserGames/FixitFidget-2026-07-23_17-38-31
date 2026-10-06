using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// NIGHT 0: THE BINS (6 Oct 2026; claude/night-0-and-the-favours-spec.md §3, claude/the-man-at-the-bins-story.md §2, §7)
//
// The first night, until Ace has met the man at the bins (NightLedger.MetHim), opens inside Night 1:
//   1. after "Close up for the night" and the Night card, Ace stands just inside the café's back door with the bin
//      bag in hand (NightCarry). The night's clock waits (it starts at 11 PM once the deal is made), and nothing
//      else at night is offered yet (NightInteractable: only the bins' own things);
//   2. E at the back door: "Take the bins out". Ace is out on Back Street, and the camera looks at the bins from
//      the street (Lodger's bins view);
//   3. E at the dumpster: "Bin it". The near lid lifts, the bag goes in, the lid drops. A beat. The lamp over the bins
//      flickers, the view pushes in (a Cinemachine blend to the set's camera and back), the far lid creaks up and a man
//      stands up out of the dumpster, and looks round;
//   4. the deal (the Night lines' held scene): who he isn't, what he knows, the leverage, the turn, the notebook
//      (his pages go in on its line: the inherited source), the first errand (Grace's gnome). Ace answers twice;
//      each reply nudges his warmth (NightLedger);
//   5. then the night proper: the clock runs, Ace's line ("I own a café and a man lives in my bins."), the night's
//      notes, and he stays standing in the dumpster all night (Lodger).
// Nothing is saved during a night: quitting part way comes back to the recap, and Night 0 again.
//
// Made by NightCycle at nightfall, only when it's due (Due); a lab that checks the night proper skips it (Skip For
// Lab: NightOneCheck). Without the set in the scene (Fixit Fidget > Night > Bins 1) there is no Night 0.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightZero : MonoBehaviour
{
    public enum Step { AtTheDoor, Outside, Binning, Reveal, Deal, Done }

    /// <summary>What Ace carries out on Night 0 (NightCarry's id).</summary>
    public const string BagId = "bag";

    public static NightZero Instance { get; private set; }
    /// <summary>For one Play session: a lab that checks the night proper (Night 1's checks) skips Night 0.</summary>
    public static bool SkipForLab { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        SkipForLab = false;
    }

    /// <summary>Does tonight open with Night 0? Not met yet, the bins are in the scene, and no lab says otherwise.</summary>
    public static bool Due => !SkipForLab && NightZeroSet.Instance != null && SaveManager.Instance != null && !SaveManager.Instance.Night.MetHim;

    /// <summary>Night 0 is under way tonight: until the deal is made, only the bins' things are offered.</summary>
    public static bool Pending => Instance != null && Instance.Now != Step.Done;

    public Step Now { get; private set; } = Step.AtTheDoor;
    /// <summary>Ace's replies tonight, in order (0 or 1 each), for the reports.</summary>
    public string Replies { get; private set; } = "";
    /// <summary>Seconds of the reveal, from the bag going in to his first line (reports).</summary>
    public float RevealSeconds { get; private set; }

    NightZeroSet set;
    Lodger man;
    PlayerMovement ace;
    AceBody aceBody;
    NightLedger ledger;
    float lampIntensity;

    /// <summary>Night 0 begins (NightCycle, at nightfall, while the screen is dark): Ace at the back door with the bag.</summary>
    public static NightZero Begin(Lodger man)
    {
        if (Instance != null) return Instance;
        NightZeroSet set = NightZeroSet.Instance;
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (set == null || ace == null || SaveManager.Instance == null) return null;
        var go = new GameObject("Night 0 (while the night runs)");
        NightZero zero = go.AddComponent<NightZero>();
        zero.Open(set, man, ace);
        return zero;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayerMovement.Release(this);
        if (set != null && set.revealCamera != null) set.revealCamera.SetActive(false);
        if (set != null && set.lamp != null && lampIntensity > 0f) set.lamp.intensity = lampIntensity;
    }

    void Open(NightZeroSet bins, Lodger lodger, PlayerMovement player)
    {
        set = bins;
        man = lodger;
        ace = player;
        aceBody = player.GetComponent<AceBody>();
        ledger = SaveManager.Instance.Night;
        if (set.lamp != null) lampIntensity = set.lamp.intensity;
        NightWalk walk = NightWalk.Instance;
        if (walk != null) walk.ClockHeld = true;
        NightCycle.Put(set.insideDoor);
        NightCarry carry = NightCarry.Ensure();
        if (carry != null && set.bag != null) carry.Hold(set.bag, BagId, set.bagScale);
        Now = Step.AtTheDoor;
    }

    void Update()
    {
        NightWalk walk = NightWalk.Instance;
        if (walk == null || !walk.Active) { Destroy(gameObject); return; }
        // A deal that stopped some other way (a check clearing the screen): the night must not stay waiting.
        if (Now == Step.Deal && !Barks.ScenePlaying) DealMade();
    }

    // ---------- the back door ----------

    /// <summary>"Take the bins out" (NightBackDoor, inside): out through the back door with the bag.</summary>
    public void TakeTheBinsOut()
    {
        if (Now != Step.AtTheDoor) return;
        if (NightCycle.Through(set.outsideDoor)) Now = Step.Outside;
    }

    // ---------- the dumpster ----------

    /// <summary>Ace has the bag, outside, by the dumpster.</summary>
    public bool CanBinIt => Now == Step.Outside && NightCarry.Current != null && NightCarry.Current.HeldId == BagId;

    /// <summary>"Bin it" (NightBins): the bag goes in, and he stands up out of the other half.</summary>
    public void BinIt()
    {
        if (!CanBinIt) return;
        Now = Step.Binning;
        StartCoroutine(TheBins());
    }

    IEnumerator TheBins()
    {
        float began = Time.time;
        PlayerMovement.Hold(this);
        if (aceBody != null && set.nearLid != null) aceBody.FaceToward(set.nearLid.position, snap: false);

        // The near lid up, the bag over the rim and in, the lid down.
        yield return Lid(set.nearLid, 0f, .65f, .2f);
        NightCarry carry = NightCarry.Current;
        Vector3 from = carry != null ? carry.Drop() : ace.transform.position + Vector3.up;
        GameObject bag = set.bag != null ? Instantiate(set.bag) : null;
        if (bag != null)
        {
            bag.name = "The bag going in (Night 0)";
            bag.transform.localScale = set.bag.transform.lossyScale * set.bagScale;
            foreach (Collider c in bag.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // Into the middle of the near half, below its front rim: under the near lid's middle, level with where he
            // stands in the other half (the hinge itself is on the back edge, against the wall).
            Vector3 into = set.nearLid != null && set.inside != null
                ? new Vector3(set.nearLid.position.x, set.inside.position.y + .6f, set.inside.position.z)
                : from;
            for (float t = 0f; t < 1f; t += Time.deltaTime / .32f)
            {
                Vector3 p = Vector3.Lerp(from, into, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .7f;
                bag.transform.position = p;
                bag.transform.rotation = Quaternion.Euler(0f, ace.transform.eulerAngles.y + 120f * t, 25f * t);
                yield return null;
            }
            Destroy(bag);
        }
        Sfx.Play("bins.bag", set.nearLid != null ? set.nearLid.position : ace.transform.position);
        yield return Lid(set.nearLid, .65f, 0f, .16f);
        Sfx.Play("bins.lid", set.nearLid != null ? set.nearLid.position : ace.transform.position);

        // A beat, in the quiet.
        yield return new WaitForSeconds(.9f);

        // The lamp over the bins stutters, the view pushes in, the far lid creaks up, and he stands.
        Now = Step.Reveal;
        yield return Flicker();
        if (set.revealCamera != null) set.revealCamera.SetActive(true);
        yield return new WaitForSeconds(.3f);
        Sfx.Play("bins.creak", set.farLid != null ? set.farLid.position : ace.transform.position);
        StartCoroutine(Lid(set.farLid, 0f, 1f, 1.1f));
        yield return new WaitForSeconds(.3f);
        if (man != null) man.StandUp(instantly: false, seconds: .9f);
        yield return new WaitForSeconds(.95f);
        if (man != null) man.Beat(NpcBeats.Clip.LookingAround, 1.4f);
        yield return new WaitForSeconds(1.1f);
        if (set.revealCamera != null) set.revealCamera.SetActive(false);
        if (man != null) man.Face(ace.transform.position);
        if (aceBody != null && man != null) aceBody.FaceToward(man.Speaker.position);
        yield return new WaitForSeconds(.7f);

        // The deal.
        RevealSeconds = Time.time - began;
        Now = Step.Deal;
        bool playing = Barks.Play(LodgerStory.DealScene, id => id == LodgerStory.SpeakerId && man != null ? man.Speaker : null,
            onDone: DealMade, onLineId: OnDealLine, onReply: Replied);
        PlayerMovement.Release(this);   // the scene holds Ace now (or, with no scene in the lines, nothing does)
        if (!playing)
        {
            OnDealLine(LodgerStory.HandOverLine);
            DealMade();
        }
    }

    // One lid from a to b (0 shut, 1 open) over the time given, eased.
    IEnumerator Lid(Transform hinge, float a, float b, float seconds)
    {
        if (hinge == null) yield break;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(.01f, seconds))
        {
            set.SetLid(hinge, Mathf.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        set.SetLid(hinge, b);
    }

    // The lamp over the bins cuts out twice and comes back.
    IEnumerator Flicker()
    {
        Light lamp = set.lamp;
        if (lamp == null) yield break;
        Sfx.Play("lamp.flicker", lamp.transform.position);
        float on = lampIntensity > 0f ? lampIntensity : lamp.intensity;
        float[] steps = { .06f, .1f, .05f, .16f };
        for (int i = 0; i < steps.Length; i++)
        {
            lamp.intensity = i % 2 == 0 ? on * .08f : on;
            yield return new WaitForSeconds(steps[i]);
        }
        lamp.intensity = on;
    }

    // ---------- the deal ----------

    void OnDealLine(string id)
    {
        // His lines: a gesture now and then (the beats only show while he holds still).
        if (id == LodgerStory.HandOverLine)
        {
            // The notebook changes hands: its first pages are his.
            Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
            int learned = 0;
            if (notebook != null)
                foreach (NotebookFactData page in LodgerStory.Pages())
                    if (notebook.Learn(page, NotebookHooks.Today)) learned++;
            Sfx.Play2D("notebook.handover");
            if (learned > 0) NightCycle.Note($"His notebook now: everything he's seen from the bins. {ControlHints.NotebookPage} to read it.", 6f);
        }
        else if (id == "lodger.night0.05" && man != null) man.Beat(NpcBeats.Clip.Dismissing);
    }

    void Replied(NightLines.Choice choice, int reply)
    {
        int warmth = reply == 0 ? choice.first.warmth : choice.second.warmth;
        ledger.Warm(warmth);
        Replies += reply.ToString();
        if (man != null) man.Heard(warmth);
    }

    void DealMade()
    {
        if (Now == Step.Done) return;
        Now = Step.Done;
        ledger.Meet();
        NightWalk walk = NightWalk.Instance;
        if (walk != null) walk.ClockHeld = false;
        StartCoroutine(After());
    }

    // Ace's line of the night, then what the night is for.
    IEnumerator After()
    {
        yield return new WaitForSeconds(.6f);
        NightLines lines = NightLines.Current;
        NightLines.Line mine = lines != null ? lines.FindLine(LodgerStory.AceNightZero) : null;
        if (mine != null) Barks.SayAce(mine.text);
        yield return new WaitForSeconds(3.2f);
        NightThing gnome = NightThings.Find(LodgerStory.FirstErrand);
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        string name = gnome == null ? "the gnome" : notebook != null && notebook.Knows(gnome.id) ? gnome.name : gnome.unknownName;
        NightCycle.Note($"{Capital(name)} is on Grace's front step: the saffron house on the corner. {ControlHints.Torch} is the torch, " +
                        $"{ControlHints.NotebookPage} the notebook. Back inside the café, {ControlHints.Interact} calls it a night.", 9f);
    }

    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    public string Describe() =>
        $"Night 0: {Now}; replies {(Replies.Length > 0 ? Replies : "none yet")}; the reveal took {RevealSeconds:0.0} s; " +
        $"his warmth {(ledger != null ? ledger.Warmth : 0)}; the clock {(NightWalk.Instance != null && NightWalk.Instance.ClockHeld ? "waiting" : "running")}.";
}
