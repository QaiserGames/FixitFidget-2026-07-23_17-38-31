using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BINS AT NIGHTFALL: NIGHT 0, AND EVERY NIGHT AFTER IT (6 Oct 2026; claude/night-0-and-the-favours-spec.md §3,
// claude/the-man-at-the-bins-story.md §2, §7; session 3: claude/session-3-favours-stalling-officer.md §1)
//
// The first night, until Ace has met the man at the bins (NightLedger.MetHim), opens inside Night 1 (Night 0):
//   1. after "Close up for the night" and the Night card, Ace stands just inside the café's back door with the bin
//      bag in hand (NightCarry). The night's clock waits (it starts at 11 PM once the deal is made), and nothing
//      else at night is offered yet (NightInteractable: only the bins' own things);
//   2. E at the back door: "Take the bins out". Ace is out on Back Street, and the camera looks at the bins from
//      the street (Lodger's bins view);
//   3. E at the dumpster: "Bin it". The near lid lifts, the bag goes in, the lid drops. A beat. The lamp over the bins
//      flickers, the view pushes in (a Cinemachine blend to the set's camera and back), the far lid creaks up and a man
//      stands up out of the dumpster, and looks round;
//   4. the deal (the Night lines' held scene): who he isn't, what he knows, the leverage, the turn, the notebook
//      (his pages go in on its line: the inherited source), the first errand (Grace's gnome), and the cop who drinks
//      Ace's coffee. Ace answers twice; each reply nudges his warmth (NightLedger);
//   5. then the night proper: he ducks back into the dumpster (until Ace brings what he asked for: Lodger), the clock
//      runs, Ace's line ("I own a café and a man lives in my bins."), and the night's notes.
// Every night after the deal (session 3) opens the same way, without the reveal: the bag, the back door, "Bin it",
// and he pops out of the other half (6 Oct, Mansoor's playtest: a quick pop, a peek over the rim, or a slow rise, a night
// each; Lodger.Pop). He turns to Ace and says his verdict on the day just ended (if anything
// happened: a straight face that morning, the officer's question), then tonight's scene (LodgerStory.WhatTonight):
// the ask for his next favour (held, Ace answers; warm, he says why), the ask again after a skip (colder, from code),
// a night off, or nothing yet. Then the clock runs, and a note says where the thing is.
// Nothing is saved during a night: quitting part way comes back to the recap, and the bins again.
//
// Any door (playtest 3, 6 Oct 2026): the step follows where Ace is while the bag is in hand. Inside the café it is
// AtTheDoor ("Take the bins out" at the back door, the obvious way); outside it is Outside ("Bin it" at the dumpster),
// whichever door Ace used. If the goal is to bin the bag, the route is the player's. The notes keep to the word budget
// (WordBudget.Note), one a beat.
//
// Made by NightCycle at nightfall, only when it's due (Due, Ritual); a lab that checks the night proper skips it
// (Skip For Lab: NightOneCheck). Without the set in the scene (Fixit Fidget > Night > Bins 1) there are no bins.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightZero : MonoBehaviour
{
    /// <summary>Where tonight's bins are. Deal: the deal on Night 0, tonight's scene on any other night.</summary>
    public enum Step { AtTheDoor, Outside, Binning, Reveal, Deal, Done }

    /// <summary>What Ace carries out every night (NightCarry's id).</summary>
    public const string BagId = "bag";

    public static NightZero Instance { get; private set; }
    /// <summary>For one Play session: a lab that checks the night proper (Night 1's checks) skips the bins.</summary>
    public static bool SkipForLab { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        SkipForLab = false;
    }

    /// <summary>Does tonight open with Night 0? Not met yet, the bins are in the scene, and no lab says otherwise.</summary>
    public static bool Due => !SkipForLab && NightZeroSet.Instance != null && SaveManager.Instance != null && !SaveManager.Instance.Night.MetHim;

    /// <summary>Does tonight open at the bins, as every night after the deal does? Met, the bins in the scene, no lab says otherwise.</summary>
    public static bool Ritual => !SkipForLab && NightZeroSet.Instance != null && SaveManager.Instance != null && SaveManager.Instance.Night.MetHim;

    /// <summary>The bins are under way tonight: until his scene is over, only the bins' things are offered.</summary>
    public static bool Pending => Instance != null && Instance.Now != Step.Done;

    public Step Now { get; private set; } = Step.AtTheDoor;
    /// <summary>Night 0 (the reveal and the deal), not the bins of a later night.</summary>
    public bool First { get; private set; }
    /// <summary>What he said tonight after the bag went in (a later night), for the reports and checks.</summary>
    public LodgerStory.Tonight Tonight { get; private set; } = LodgerStory.Tonight.Wait;
    /// <summary>His verdict tonight (a pool), or "".</summary>
    public string Verdict { get; private set; } = "";
    /// <summary>Ace's replies tonight, in order (0 or 1 each), for the reports.</summary>
    public string Replies { get; private set; } = "";
    /// <summary>Seconds of the reveal, from the bag going in to his first line (reports).</summary>
    public float RevealSeconds { get; private set; }
    /// <summary>How he came up tonight, a night after the deal (reports, checks).</summary>
    public Lodger.PopStyle Popped { get; private set; }

    NightZeroSet set;
    Lodger man;
    PlayerMovement ace;
    AceBody aceBody;
    CafeViewMode view;
    NightLedger ledger;
    float lampIntensity;
    LodgerStory.Favour asked;

    static int Night => DayClock.Instance != null ? DayClock.Instance.Day : 0;

    /// <summary>The bins begin (NightCycle, at nightfall, while the screen is dark): Ace at the back door with the bag.</summary>
    public static NightZero Begin(Lodger man)
    {
        if (Instance != null) return Instance;
        NightZeroSet set = NightZeroSet.Instance;
        PlayerMovement ace = FindAnyObjectByType<PlayerMovement>();
        if (set == null || ace == null || SaveManager.Instance == null) return null;
        var go = new GameObject("The bins (while the night runs)");
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
        First = !ledger.MetHim;
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
        // With the bag in hand the step follows Ace: inside the café, the back door offers the way out; outside, the
        // dumpster offers "Bin it", whichever door Ace came out of. Not mid-blink: the back door has already said where
        // Ace is going, and Ace is still on this side behind the black for a moment.
        if ((Now == Step.AtTheDoor || Now == Step.Outside) && !NightCycle.Blinking)
            Now = AceInsideTheCafe ? Step.AtTheDoor : Step.Outside;
        // A scene that stopped some other way (a check clearing the screen): the night must not stay waiting.
        if (Now == Step.Deal && !Barks.ScenePlaying) SceneDone();
    }

    bool AceInsideTheCafe
    {
        get
        {
            if (view == null) view = FindAnyObjectByType<CafeViewMode>();
            return view != null ? view.AceInsideCafe : CafeDaylight.CafeInside.Contains(new Vector2(ace.transform.position.x, ace.transform.position.z));
        }
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

    /// <summary>"Bin it" (NightBins): the bag goes in; on Night 0 he stands up out of the other half.</summary>
    public void BinIt()
    {
        if (!CanBinIt) return;
        Now = Step.Binning;
        StartCoroutine(TheBins());
    }

    Transform Who(string speakerId) => speakerId == LodgerStory.SpeakerId && man != null ? man.Speaker : null;

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
            bag.name = "The bag going in";
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

        if (!First)
        {
            // Any night after the deal: a beat, and he pops out of the other half (a quick pop, a peek over the rim, or a slow
            // rise, a night each); he turns to Ace, and says his piece.
            yield return new WaitForSeconds(.35f);
            if (man != null)
            {
                Popped = Lodger.StyleFor(Night);
                float up = man.Pop(Popped);
                man.Face(ace.transform.position);
                yield return new WaitForSeconds(up);
            }
            if (aceBody != null && man != null) aceBody.FaceToward(man.Speaker.position);
            yield return new WaitForSeconds(.35f);
            Now = Step.Deal;
            bool said = PlayTonight();
            PlayerMovement.Release(this);   // the scene holds Ace now (or, with no scene in the lines, nothing does)
            if (!said)
            {
                // No lines for it in the Night lines (Barks 1 not run yet): what it does still happens, without the words.
                if (asked != null) OnAskLine(asked.firstLine);
                SceneDone();
            }
            yield break;
        }

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
        bool playing = Barks.Play(LodgerStory.DealScene, Who, onDone: SceneDone, onLineId: OnDealLine, onReply: Replied);
        PlayerMovement.Release(this);   // the scene holds Ace now (or, with no scene in the lines, nothing does)
        if (!playing)
        {
            OnDealLine(LodgerStory.HandOverLine);
            SceneDone();
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
                foreach (NotebookFactData page in LodgerStory.Pages(InTheGame))   // the parking page waits for the cones
                    if (notebook.Learn(page, NotebookHooks.Today)) learned++;
            Sfx.Play2D("notebook.handover");
            if (learned > 0) NightCycle.Note($"His notebook is yours now. {ControlHints.NotebookPage} to read it.", 6f);
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

    // ---------- tonight (every night after the deal) ----------

    // His verdict, then tonight's scene. False when there were no lines to play (the Night lines lack it).
    bool PlayTonight()
    {
        int night = Night;
        LodgerStory.Favour favour = LodgerStory.FindFavour(ledger.Favour);
        Tonight = LodgerStory.WhatTonight(ledger, night, InTheGame);
        Verdict = LodgerStory.Verdict(ledger, night);
        string verdict = Verdict.Length > 0 ? Barks.PoolLine(LodgerStory.SpeakerId, Verdict) : "";
        switch (Tonight)
        {
            case LodgerStory.Tonight.Ask:
                ledger.Ask(night);
                asked = favour;
                return Barks.Play(LodgerStory.AskScene(favour, ledger.Warmth), Who, onDone: SceneDone, onLineId: OnAskLine, onReply: Replied,
                    leadSpeaker: LodgerStory.SpeakerId, leadText: verdict);
            case LodgerStory.Tonight.AskAgain:
            {
                ledger.Ask(night);
                // Colder: one of his cold lines, then what he wants. Said from code (Ace held), no reply.
                var speakers = new List<string>(3);
                var texts = new List<string>(3);
                void Add(string text)
                {
                    if (string.IsNullOrWhiteSpace(text)) return;
                    speakers.Add(LodgerStory.SpeakerId);
                    texts.Add(text);
                }
                Add(verdict);
                Add(Barks.PoolLine(LodgerStory.SpeakerId, LodgerStory.Cold));
                NightLines lines = NightLines.Current;
                Add(favour != null && lines != null ? lines.FindLine(favour.remind)?.text : "");
                return texts.Count > 0 && Barks.PlayLines(speakers, texts, holdAce: true, Who, onDone: SceneDone);
            }
            case LodgerStory.Tonight.Off:
                return Barks.Play(LodgerStory.OffScene, Who, onDone: SceneDone, leadSpeaker: LodgerStory.SpeakerId, leadText: verdict);
            default:
                return Barks.Play(LodgerStory.WaitScene, Who, onDone: SceneDone, leadSpeaker: LodgerStory.SpeakerId, leadText: verdict);
        }
    }

    /// <summary>A favour (or a page's subject) is in the game when there's something to take for it: a thing in the scene,
    /// or one Ace already has. Her photos (a page's subject) aren't yet.</summary>
    public static bool InTheGame(string favour)
    {
        if (NightThings.Find(favour) == null) return false;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (night != null && night.HasTrophy(favour)) return true;
        foreach (NightTrophy thing in FindObjectsByType<NightTrophy>(FindObjectsInactive.Include))
            if (thing.thingId == favour) return true;
        return false;
    }

    // On the ask's first line Ace hears of the thing: the notebook learns it (his word for it).
    void OnAskLine(string id)
    {
        if (asked == null || string.IsNullOrEmpty(id) || id != asked.firstLine) return;
        NightThing thing = NightThings.Find(asked.id);
        if (thing != null && NotebookHooks.HeardFromHim(thing)) Sfx.Play2D("notebook.page");
    }

    void SceneDone()
    {
        if (Now == Step.Done) return;
        Now = Step.Done;
        if (First) ledger.Meet(Night);
        NightWalk walk = NightWalk.Instance;
        if (walk != null) walk.ClockHeld = false;
        // His say done, he's back in the bin (he comes up again for Ace bringing what he asked for).
        if (man != null) man.Duck(Lodger.DuckAfterSay);
        StartCoroutine(After());
    }

    /// <summary>The keys, said once on the first night (a beat after where to go).</summary>
    public static string KeysNote => $"{ControlHints.Torch} torch · {ControlHints.NotebookPage} notebook · {ControlHints.Interact} at a café door: home.";

    // Ace's line of the night (Night 0), then what the night is for: one note a beat (the budget: 12 words).
    IEnumerator After()
    {
        yield return new WaitForSeconds(.6f);
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (First)
        {
            NightLines lines = NightLines.Current;
            NightLines.Line mine = lines != null ? lines.FindLine(LodgerStory.AceNightZero) : null;
            if (mine != null) Barks.SayAce(mine.text);
            yield return new WaitForSeconds(3.2f);
            NightThing gnome = NightThings.Find(LodgerStory.FirstErrand);
            string name = gnome == null ? "the gnome" : notebook != null && notebook.Knows(gnome.id) ? gnome.name : gnome.unknownName;
            // Where to go; then, as it fades, the keys.
            NightCycle.Note(LodgerStory.Hint(LodgerStory.Favours[0], name), 7f);
            NightCycle.NoteThen(KeysNote, 7.5f, 6f);
            yield break;
        }
        LodgerStory.Favour errand = LodgerStory.FindFavour(ledger.Errand);
        NightThing thing = errand != null ? NightThings.Find(errand.id) : null;
        string known = thing == null ? "" : notebook != null && notebook.Knows(thing.id) ? thing.name : thing.unknownName;
        // A night off is a night off, whatever he's still waiting for. Already Ace's (a night that ended with it in hand, or
        // taken before he asked): it's on Ace's shelf, behind the counter (NightShelfTake hands it back), not where it was.
        string hint = Tonight == LodgerStory.Tonight.Off ? "Night off. The street is yours."
            : thing == null ? "Nothing tonight. The street is yours."
            : ledger.OnShelf(thing.id) ? $"{Capital(known)} is on Ace's shelf, behind the counter."
            : LodgerStory.Hint(errand, known);
        NightCycle.Note(hint, 7f);
    }

    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    public string Describe() =>
        (First ? $"Night 0: {Now}; replies {(Replies.Length > 0 ? Replies : "none yet")}; the reveal took {RevealSeconds:0.0} s; "
               : $"The bins: {Now}; he came up {Popped}; tonight {Tonight}{(Verdict.Length > 0 ? ", his verdict " + Verdict : "")}; replies {(Replies.Length > 0 ? Replies : "none")}; ") +
        $"his warmth {(ledger != null ? ledger.Warmth : 0)}; his favour {(ledger != null && ledger.Favour.Length > 0 ? ledger.Favour : "none")} " +
        $"(skips {(ledger != null ? ledger.Skips : 0)}); the clock {(NightWalk.Instance != null && NightWalk.Instance.ClockHeld ? "waiting" : "running")}.";
}
