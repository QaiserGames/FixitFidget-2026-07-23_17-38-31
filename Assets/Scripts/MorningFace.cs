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
// The words and the meter's difficulty come from the thing (NightThings).
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
    StraightFaceMeter meter;
    float meterAt = -1f;
    float resultSince = -1f;

    // A beat to take in what they said before the needle starts (E starts it at once).
    const float MeterBeat = .6f;

    public Step Now { get; private set; } = Step.Complaint;
    public StraightFaceMeter Meter => meter;
    public NightThing Thing => thing;
    public CustomerBrain Who => who;
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
        return new MorningFace(brain, ui, deed, thing);
    }

    MorningFace(CustomerBrain who, ConversationUI ui, NightDeedData deed, NightThing thing)
    {
        this.who = who;
        this.ui = ui;
        this.deed = deed;
        this.thing = thing;
        Last = this;
    }

    /// <summary>Their complaint: the first line of the conversation.</summary>
    public void Begin()
    {
        who.Identity.Feel(PortraitExpression.Worried);
        ui.SetLine(thing.complaint);
        ui.SetOptions("");
        // Taken without knowing whose it was (they never mentioned it): now Ace knows, and notes it down.
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        if (notebook != null && !notebook.Knows(thing.id + ".taken"))
        {
            NotebookHooks.HeardMention(who.CustomerName, thing);
            NotebookHooks.TookAtNight(thing);
        }
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
                meter.Tick(deltaTime);
                bool stop = inputReady && (keys != null && keys.spaceKey.wasPressedThisFrame || PadInput.Pressed(PadButton.West));
                if (stop) meter.Stop();
                if (meter.Stopped) Finish();
                else StraightFaceUI.Draw(meter);
                return;

            case Step.Result:
                if (!ui.LineFinished)
                {
                    if (next) ui.SkipReveal();
                    return;
                }
                if (resultSince < 0f) resultSince = Time.time;
                ui.SetOptions($"[{ControlHints.Interact}]  Go on");
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

    string LastLine => meter != null && meter.Held ? thing.held : thing.cracked;

    static float ReadTime(string line) => Mathf.Clamp((line ?? "").Length / 30f, 1.6f, 4f);

    static string StopKey => ControlHints.Say("Space", PadInput.Label(PadButton.West));

    void StartMeter()
    {
        meter = StraightFaceMeter.Rolled(thing.sweepSeconds, thing.green, thing.near, thing.patience, Rng);
        Now = Step.Meter;
        ui.SetOptions($"[{StopKey}]  Keep a straight face");
        StraightFaceUI.Draw(meter);
    }

    void Finish()
    {
        bool held = meter.Held;
        int day = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        NightLedger night = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (night != null) night.Faced(deed, !held, day);
        if (!held) NotebookHooks.Suspects(who.CustomerName, thing);
        Sfx.Play2D(held ? "face.held" : "face.cracked");
        who.Identity.Feel(held ? PortraitExpression.Happy : PortraitExpression.Surprised);
        StraightFaceUI.Result(meter);
        ui.SetLine(held ? thing.held : thing.cracked);
        ui.SetOptions("");
        resultSince = -1f;
        Now = Step.Result;
    }

    public string Describe() => $"Morning scene with {who?.CustomerName ?? "nobody"} about {thing?.name ?? "?"}: {Now}" +
        (meter == null ? "" : $"; needle {meter.Needle:0.00}, green {meter.GreenLeft:0.00}-{meter.GreenRight:0.00}, " +
         $"{(meter.Stopped ? (meter.Held ? "held" : meter.TimedOut ? "cracked (never stopped)" : "cracked") : "running")} after {meter.Elapsed:0.0} s");
}
