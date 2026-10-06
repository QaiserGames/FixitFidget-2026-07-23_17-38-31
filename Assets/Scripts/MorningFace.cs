using UnityEngine;
using UnityEngine.InputSystem;

// ---------------------------------------------------------------------------
// THE MORNING AFTER: KEEP A STRAIGHT FACE (claude/ace-after-dark.md §3.2; the Night 1 slice)
//
// Runs inside a counter conversation, before the person's usual request, when they have something to
// tell Ace about last night (NightLedger.Unfaced): Grace, the morning after Barnaby went missing.
//
//   1. Their complaint reveals like any line (E skips the reveal). A beat later the meter appears.
//   2. The needle sweeps; Space, or X / Square on a pad, stops it (StraightFaceMeter, drawn by
//      StraightFaceUI). Left alone, Ace cracks when the meter's patience runs out.
//   3. Held: their thanks. Cracked: they ask if Ace is smiling and become a little more suspicious
//      (NightLedger; the notebook notes what they said). Stars are never touched.
//   4. After a moment (or E) the conversation goes on to what they came in for, as usual.
//
// The scene owns the conversation while it runs: there's no stepping away or turning them away in
// the middle of it (it's about ten seconds). If the conversation closes some other way before the
// meter is stopped (the day ends), nothing is recorded and the scene waits for the next time they talk.
// The words and the meter's difficulty come from the thing (NightThings). Once the man at the bins has
// taught Ace Nerve (LodgerStory, 6 Oct 2026), the green and its "near enough" are wider, and the meter's
// title says so.
//
// Its second flavour (session 3: claude/session-3-favours-stalling-officer.md §5): "Say nothing". The officer, after
// his order, asks a question about the man at the bins (OfficerStory). The same meter with the question's words and
// difficulty ("Say nothing", then "Said nothing." or "You flinched."); kept, his thanks; flinched, he noticed (one
// step more suspicious, a line in the notebook). Either way his description goes in the notebook, and the question
// is asked once (NightLedger.Questioned). It comes after the order (AfterTheOrder), so when it's done the conversation
// closes (AfterOrder), instead of going on to the request.
// ---------------------------------------------------------------------------
public sealed class MorningFace
{
    public enum Step { Complaint, Meter, Result, Done }

    // Its own random numbers (where the green sits), never UnityEngine.Random: the day's customers
    // are drawn from that stream.
    static readonly System.Random Rng = new System.Random();

    readonly CustomerBrain who;
    readonly ConversationUI ui;
    readonly NightDeedData deed;
    readonly NightThing thing;
    readonly OfficerStory.Question question;
    readonly string opening;
    StraightFaceMeter meter;
    float meterAt = -1f;
    float resultSince = -1f;
    string stopHint = "";   // made once when the meter starts (not every frame)
    /// <summary>Ace has Nerve (the man's lesson): the green was wider this time.</summary>
    public bool Nerve { get; private set; }

    // A beat to take in what they said before the needle starts (E starts it at once).
    const float MeterBeat = .6f;

    public Step Now { get; private set; } = Step.Complaint;
    public StraightFaceMeter Meter => meter;
    /// <summary>The thing they're telling Ace about (a complaint), or null (a question).</summary>
    public NightThing Thing => thing;
    /// <summary>The question being asked (the officer's "say nothing"), or null (a complaint).</summary>
    public OfficerStory.Question Question => question;
    public CustomerBrain Who => who;
    /// <summary>A question after the order: when it's done the conversation closes.</summary>
    public bool AfterOrder => question != null;
    /// <summary>The most recent scene (reports and checks).</summary>
    public static MorningFace Last { get; private set; }

    /// <summary>The scene for <paramref name="brain"/>, if they have something to tell Ace this morning; or null.</summary>
    public static MorningFace For(CustomerBrain brain, ConversationUI ui)
    {
        if (brain == null || ui == null) return null;
        CustomerIdentity identity = brain.Identity;
        if (identity == null || !identity.IsRegular || identity.Profile == null) return null;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (night == null) return null;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        NightDeedData deed = night.Unfaced(identity.Profile.PersistentId, day);
        NightThing thing = deed != null ? NightThings.Find(deed.thing) : null;
        if (thing == null || string.IsNullOrWhiteSpace(thing.complaint)) return null;
        return new MorningFace(brain, ui, deed, thing, null, thing.complaint);
    }

    /// <summary>
    /// The question <paramref name="brain"/> asks after their order, if they have one today (OfficerStory); or null.
    /// <paramref name="accepted"/>, what they said to Ace's "Coming right up.", comes first, on its own line.
    /// </summary>
    public static MorningFace AfterTheOrder(CustomerBrain brain, ConversationUI ui, string accepted)
    {
        if (brain == null || ui == null) return null;
        CustomerIdentity identity = brain.Identity;
        if (identity == null || !identity.IsRegular || identity.Profile == null) return null;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        OfficerStory.Question q = OfficerStory.Due(identity.Profile.PersistentId, day, night);
        if (q == null || string.IsNullOrWhiteSpace(q.question)) return null;
        string opening = string.IsNullOrWhiteSpace(accepted) ? q.question : accepted.TrimEnd() + "\n" + q.question;
        return new MorningFace(brain, ui, null, null, q, opening);
    }

    MorningFace(CustomerBrain who, ConversationUI ui, NightDeedData deed, NightThing thing, OfficerStory.Question question, string opening)
    {
        this.who = who;
        this.ui = ui;
        this.deed = deed;
        this.thing = thing;
        this.question = question;
        this.opening = opening;
        Last = this;
    }

    /// <summary>Their complaint (or their question): the line the meter answers.</summary>
    public void Begin()
    {
        who.Identity.Feel(question != null ? PortraitExpression.Neutral : PortraitExpression.Worried);
        ui.SetLine(opening);
        ui.SetOptions("");
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook == null || thing == null) return;
        // Taken without knowing whose it was (they never mentioned it): now Ace knows, and notes down
        // what they just said (not the day's mention, which Ace never heard) and whose it was.
        if (!notebook.Knows(thing.id)) NotebookHooks.HeardComplaint(who.CustomerName, thing);
        NightLedger night = SaveManager.Instance.Night;
        if (!notebook.Knows(thing.id + ".taken")) NotebookHooks.TookAtNight(thing, night != null && night.HasGiven(thing.id));
    }

    /// <summary>
    /// One frame of the scene. <paramref name="inputReady"/> is false for a moment after the
    /// conversation opened (the key that opened it mustn't also answer it).
    /// </summary>
    public void Tick(float deltaTime, bool inputReady)
    {
        Keyboard keys = Keyboard.current;
        bool next = inputReady && (keys != null && keys.eKey.wasPressedThisFrame || PadInput.Pressed(PadButton.South));
        switch (Now)
        {
            case Step.Complaint:
                ui.SetOptions("");
                if (!ui.LineFinished)
                {
                    if (next) ui.SkipReveal();
                    return;
                }
                if (meterAt < 0f) meterAt = Time.time + MeterBeat;
                if (Time.time < meterAt && !next) return;
                StartMeter();
                return;

            case Step.Meter:
                // A press stops the needle where the player saw it (last frame's drawing), before this
                // frame moves it on.
                bool stop = inputReady && (keys != null && keys.spaceKey.wasPressedThisFrame || PadInput.Pressed(PadButton.West));
                if (stop) meter.Stop();
                else meter.Tick(deltaTime);
                if (meter.Stopped) Finish();
                else StraightFaceUI.Draw(meter, stopHint);
                return;

            case Step.Result:
                if (!ui.LineFinished)
                {
                    if (next) ui.SkipReveal();
                    return;
                }
                if (resultSince < 0f) resultSince = Time.time;
                StraightFaceUI.Result(meter, HeldWord, CrackedWord, $"[{ControlHints.Interact}]  Go on");
                if (next || Time.time - resultSince >= ReadTime(ui.LineFinished ? LastLine : ""))
                {
                    Now = Step.Done;
                    StraightFaceUI.Hide();
                    ui.SetOptions("");
                }
                return;
        }
    }

    /// <summary>The conversation closed some other way: put the meter away.</summary>
    public void Abandon()
    {
        StraightFaceUI.Hide();
        if (Now != Step.Result && Now != Step.Done) Now = Step.Done;
    }

    string Held => question != null ? question.held : thing.held;
    string Cracked => question != null ? question.cracked : thing.cracked;
    string HeldWord => question != null ? question.heldWord : null;
    string CrackedWord => question != null ? question.crackedWord : null;
    string LastLine => meter != null && meter.Held ? Held : Cracked;

    static float ReadTime(string line) => Mathf.Clamp((line ?? "").Length / 30f, 1.6f, 4f);

    static string StopKey => ControlHints.Say("Space", PadInput.Label(PadButton.West));

    void StartMeter()
    {
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        Nerve = night != null && night.Knows(LodgerStory.Nerve);
        float sweep = question != null ? question.sweepSeconds : thing.sweepSeconds;
        float green = question != null ? question.green : thing.green;
        float near = question != null ? question.near : thing.near;
        float patience = question != null ? question.patience : thing.patience;
        meter = StraightFaceMeter.Rolled(sweep, LodgerStory.Green(green, Nerve), LodgerStory.Near(near, Nerve), patience, Rng);
        string what = question != null && !string.IsNullOrWhiteSpace(question.title) ? question.title : "Keep a straight face";
        stopHint = $"[{StopKey}]  {what}" + (Nerve ? "   <color=#A6A6A6>Nerve</color>" : "");
        Now = Step.Meter;
        // The meter takes the place of Ace's replies (bottom right) and gives its key itself
        // (StraightFaceUI), so it sits beside the person's line instead of over it or their face.
        ui.SetOptions("");
        StraightFaceUI.Draw(meter, stopHint);
    }

    void Finish()
    {
        bool held = meter.Held;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (question != null)
        {
            string asker = who.Identity != null && who.Identity.Profile != null ? who.Identity.Profile.PersistentId : question.asker;
            if (night != null) night.Questioned(question.id, asker, day, !held);
            NotebookHooks.AskedBy(who.CustomerName, question, flinched: !held);
        }
        else
        {
            if (night != null) night.Faced(deed, !held, day);
            if (!held) NotebookHooks.Suspects(who.CustomerName, thing);
        }
        Sfx.Play2D(held ? "face.held" : "face.cracked");
        who.Identity.Feel(held ? PortraitExpression.Happy : PortraitExpression.Surprised);
        StraightFaceUI.Result(meter, HeldWord, CrackedWord);
        ui.SetLine(held ? Held : Cracked);
        ui.SetOptions("");
        resultSince = -1f;
        Now = Step.Result;
    }

    public string Describe() =>
        $"Morning scene with {who?.CustomerName ?? "nobody"} " +
        (question != null ? $"(his question {question.id})" : $"about {thing?.name ?? "?"}") + $": {Now}{(Nerve ? " (Nerve)" : "")}" +
        (meter == null ? "" : $"; needle {meter.Needle:0.00}, green {meter.GreenLeft:0.00}-{meter.GreenRight:0.00}, " +
         $"{(meter.Stopped ? (meter.Held ? "held" : meter.TimedOut ? "cracked (never stopped)" : "cracked") : "running")} after {meter.Elapsed:0.0} s");
}
