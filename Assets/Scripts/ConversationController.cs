using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// Ace's side of a conversation: the reply list (the dialogue pass, claude/dialogue-skyrim-proposal.md).
// PLACEHOLDER COPY: Mansoor rewrites it. Keep each under 30 characters (Fixit Fidget > Checks > Dialogue rules).
public static class AceReplies
{
    public const string TakeRepair = "I'll take a look.";
    public const string TakeDrink = "Coming right up.";
    public const string TurnAwayRepair = "Not today.";
    public const string TurnAwayDrink = "Sorry, not today.";
    public const string OutOfStock = "Sorry, we're out.";
    public const string ShelfFull = "No room on the shelf.";
    public const string HandBack = "Here you go.";

    public static readonly string[] All = { TakeRepair, TakeDrink, TurnAwayRepair, TurnAwayDrink, OutOfStock, ShelfFull, HandBack };
}

public enum ReplyKind { Accept, Topic, Refuse, HandBack }

/// <summary>One line in the reply list.</summary>
public sealed class ConversationReply
{
    public readonly ReplyKind Kind;
    public readonly string Text;
    /// <summary>The thing it asks about (a Topic reply), or null.</summary>
    public readonly TopicChoice Topic;

    public ConversationReply(ReplyKind kind, string text, TopicChoice topic = null)
    {
        Kind = kind;
        Text = text ?? "";
        Topic = topic;
    }
}

// ---------------------------------------------------------------------------
// A COUNTER CONVERSATION
//
// They speak (ConversationUI shows it as a subtitle, line by line); once their line is up, Ace's replies
// appear on the right, Skyrim style:
//   * take it: "I'll take a look." / "Coming right up." (highlighted first, so E, E still takes the job);
//   * up to two things to ask, regulars only (CustomerIdentity.Topics): they answer and it greys out;
//   * turn them away: "Not today." (Q at any time);
//   * step away (Esc, F or Tab).
// W/S, the arrow keys or the mouse wheel move the highlight; E, Enter or a click picks it; 1-4 pick one
// directly. On a controller: the stick or D-pad, A picks, Y turns them away, B steps away.
// Dialogue gates the decision: nothing can be picked until their line is up, and an E pressed before
// then only brings the replies up. A closing line is skippable with E.
// ---------------------------------------------------------------------------
public class ConversationController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera conversationCam;
    [SerializeField] private ConversationUI ui;
    [SerializeField] private PlayerInteractor interactor;

    [Tooltip("Ignore input briefly after opening, so the key that started it doesn't also answer it.")]
    [SerializeField] private float inputDelay = 0.2f;
    [Tooltip("Pause after a closing line has been read, before the panel closes (E closes it at once). " +
             "The customer does not move until this elapses, so raising it holds " +
             "them at the counter longer.")]
    [SerializeField] private float closingPause = 1.2f;

    // Keep ownership through the closing frame, so F/Escape cannot also move
    // the player out of a station later in that same frame.
    public bool InConversation => conversationOpen || Time.frameCount == closedAtFrame;

    /// <summary>
    /// A conversation is open, whoever it's with. The floating patience bars hide meanwhile
    /// (CustomerBrain): otherwise the close-up shows the bars over the speaker, and over anyone
    /// standing near them, across the top of the screen.
    /// </summary>
    public static bool AnyOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyOpen = false;

    private CustomerBrain partner;
    private float inputReadyAt;
    private bool closing;
    private float closeAt;
    private CustomerBrain counterAfterClose;
    private bool conversationOpen;
    private int closedAtFrame = -1;
    // The Night 1 slice: the morning after a night, the straight-face scene runs
    // first (MorningFace), then the usual request.
    private MorningFace morningFace;

    // The reply list: what's offered, which one E picks, and whether the stick is back in the middle.
    private readonly List<ConversationReply> replies = new();
    private int highlighted;
    private bool stickArmed = true;

    /// <summary>The straight-face scene running in this conversation, or null.</summary>
    public MorningFace Face => morningFace;

    /// <summary>Ace's replies as offered right now; empty while they're still talking (reports and checks).</summary>
    public IReadOnlyList<string> ReplyTexts
    {
        get
        {
            var texts = new List<string>(replies.Count);
            foreach (ConversationReply reply in replies) texts.Add(reply.Text);
            return texts;
        }
    }

    /// <summary>The replies as offered right now (reports and checks).</summary>
    public IReadOnlyList<ConversationReply> Replies => replies;

    public bool RepliesShowing => replies.Count > 0;

    /// <summary>Which reply E would pick.</summary>
    public int Highlighted => highlighted;

    /// <summary>What they're saying now, all of its lines (reports and checks).</summary>
    public string CurrentLine => ui != null ? ui.Line : "";

    /// <summary>A closing line is playing: the conversation ends once it's read (or on E).</summary>
    public bool Closing => closing;

    public void Begin(CustomerBrain brain)
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver) return;
        if (brain == null || InConversation || Time.timeScale <= 0f || ui == null) return;
        if (!brain.CanHearIntake && !brain.CanDecide) return;

        partner = brain;
        conversationOpen = true;
        AnyOpen = true;
        closing = false;
        inputReadyAt = Time.time + inputDelay;
        replies.Clear();
        highlighted = 0;

        // Take ownership of their body. Until End(), nothing moves them.
        brain.OnConversationOpened(this);

        // Frame them. Rotation Composer holds the shot, so look input does nothing.
        if (conversationCam != null)
        {
            conversationCam.Target.TrackingTarget = brain.LookTarget != null
                ? brain.LookTarget : brain.transform;
            conversationCam.Priority = 40;
        }

        Sprite face = brain.Identity != null ? brain.Identity.Portrait : null;
        Color tint = brain.Identity != null ? brain.Identity.ThemeColor : Color.white;
        ui.Show(brain.CustomerName, tint, face);

        // First beat: whatever they came here to say. The morning after a night,
        // what they have to say about it comes first (the Night 1 slice).
        morningFace = MorningFace.For(brain, ui);
        if (morningFace != null) morningFace.Begin();
        else ui.SetLine(brain.HearIntake());
        RefreshPortrait();
    }

    public void End()
    {
        // Hand the body back BEFORE dropping the reference. This is the single
        // moment a customer is allowed to release their counter slot, claim a
        // waiting spot, and start walking.
        //
        // Unity's overloaded == is deliberate here: it catches a partner who
        // has been Destroy()ed, which the null-conditional operator would not.
        CustomerBrain leaving = partner;
        if (conversationOpen) closedAtFrame = Time.frameCount;
        partner = null;
        var counterCustomer = counterAfterClose;
        counterAfterClose = null;
        conversationOpen = false;
        AnyOpen = false;
        closing = false;
        replies.Clear();
        if (morningFace != null) { morningFace.Abandon(); morningFace = null; }

        if (conversationCam != null)
        {
            conversationCam.Priority = 0;
            conversationCam.Target.TrackingTarget = null;
        }

        if (ui != null) ui.Hide();

        // What happened, as a short line on screen now the conversation is over (Grace's print): never a
        // narrator in the speech panel.
        string note = leaving != null && leaving.Identity != null ? leaving.Identity.TakeClosingNote() : "";

        if (leaving != null) leaving.OnConversationClosed();
        if (!string.IsNullOrWhiteSpace(note) && isActiveAndEnabled) NightCycle.Note(note);
        if (counterCustomer != null && isActiveAndEnabled)
            StartCoroutine(OpenCounterNextFrame(counterCustomer));
    }

    private void Update()
    {
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            if (conversationOpen || closing) End();
            return;
        }
        if (partner == null)
        {
            if (conversationOpen) End(); // Also clean up a destroyed Unity object.
            return;
        }
        if (Time.timeScale <= 0f) return;
        RefreshPortrait();

        bool ready = Time.time >= inputReadyAt;
        Keyboard kb = Keyboard.current;
        bool press = ready && Pressed(kb);

        // A closing line: held until it's read, or E closes it at once.
        if (closing)
        {
            if (!ui.LineFinished)
            {
                if (press) ui.SkipReveal();
                return;
            }
            if (closeAt < 0f) closeAt = Time.time + closingPause + ClosingRead();
            if (press || Time.time >= closeAt) End();
            return;
        }

        // The straight-face scene owns the conversation until it's done; then on
        // to what they came in for, as usual.
        if (morningFace != null)
        {
            morningFace.Tick(Time.deltaTime, Time.time >= inputReadyAt);
            if (morningFace.Now != MorningFace.Step.Done) return;
            morningFace = null;
            ui.SetLine(partner.HearIntake());
            RefreshPortrait();
            inputReadyAt = Time.time + inputDelay;
            return;
        }

        // Controller: B (or X, like F) steps away. Tab too, as in Skyrim.
        bool stepAway = ready && (kb != null && (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame
                                                 || kb.tabKey.wasPressedThisFrame)
            || PadInput.Pressed(PadButton.East) || PadInput.Pressed(PadButton.West));
        if (stepAway) { End(); return; }

        // Still talking: E shows everything they're saying and brings the replies up, never an answer.
        if (!ui.LineFinished)
        {
            ClearReplies();
            if (press) ui.SkipReveal();
            return;
        }

        BuildReplies();
        if (replies.Count == 0)
        {
            ui.SetOptions(Footer());
            return;
        }
        highlighted = Mathf.Clamp(highlighted, 0, replies.Count - 1);
        if (ready)
        {
            int move = Move(kb);
            if (move != 0) highlighted = (highlighted + move + replies.Count) % replies.Count;
            int direct = Direct(kb);
            bool refuse = kb != null && kb.qKey.wasPressedThisFrame || PadInput.Pressed(PadButton.North);
            if (direct >= 0 && direct < replies.Count) { Choose(direct); return; }
            if (press) { Choose(highlighted); return; }
            if (refuse)
            {
                int turnAway = IndexOf(ReplyKind.Refuse);
                if (turnAway >= 0) { Choose(turnAway); return; }
            }
        }
        ui.SetOptions(Render());
    }

    /// <summary>
    /// Picks a reply as E on it would, once their line is up (the conversation checks use this; players
    /// use the keys). Out-of-range or early calls do nothing.
    /// </summary>
    public void ChooseReply(int index)
    {
        if (partner == null || closing || morningFace != null || ui == null || !ui.LineFinished) return;
        BuildReplies();
        if (index < 0 || index >= replies.Count) return;
        Choose(index);
    }

    /// <summary>
    /// What E does right now (the conversation checks use this): while they talk, everything at once and
    /// the replies; with the replies up, the highlighted one; on a closing line, close it.
    /// </summary>
    public void Advance()
    {
        if (partner == null || ui == null || morningFace != null) return;
        if (closing)
        {
            if (!ui.LineFinished) ui.SkipReveal();
            else End();
            return;
        }
        if (!ui.LineFinished) { ui.SkipReveal(); return; }
        BuildReplies();
        if (replies.Count > 0) Choose(Mathf.Clamp(highlighted, 0, replies.Count - 1));
    }

    private System.Collections.IEnumerator OpenCounterNextFrame(CustomerBrain owner)
    {
        yield return null; // Opening E must never activate the switch too.
        if (owner != null && owner.CanFixAtCounter)
            GetComponent<CounterRepairView>()?.Open(owner);
    }

    private void OnDisable() => End();

    // ---------- the replies ----------

    private void BuildReplies()
    {
        replies.Clear();
        if (partner == null) return;
        bool drink = partner.Record != null && partner.Record.kind == JobKind.Drink;
        if (partner.OutOfStock)
        {
            replies.Add(new ConversationReply(ReplyKind.Refuse, AceReplies.OutOfStock));
            return;
        }
        if (partner.ShelfFull)
        {
            replies.Add(new ConversationReply(ReplyKind.Refuse, AceReplies.ShelfFull));
            return;
        }
        if (partner.CanAcceptJob)
        {
            replies.Add(new ConversationReply(ReplyKind.Accept, drink ? AceReplies.TakeDrink : AceReplies.TakeRepair));
            if (partner.Identity != null)
                foreach (TopicChoice topic in partner.Identity.Topics())
                    replies.Add(new ConversationReply(ReplyKind.Topic, topic.Ask, topic));
        }
        else if (partner.JobReady)
        {
            replies.Add(new ConversationReply(ReplyKind.HandBack, AceReplies.HandBack));
            return;
        }
        if (partner.CanRefuse)
            replies.Add(new ConversationReply(ReplyKind.Refuse, drink ? AceReplies.TurnAwayDrink : AceReplies.TurnAwayRepair));
    }

    private void Choose(int index)
    {
        ConversationReply reply = replies[index];
        switch (reply.Kind)
        {
            case ReplyKind.Accept:
                if (!partner.CanAcceptJob) return;
                string accepted = partner.AcceptJob();
                if (partner.CanFixAtCounter) { counterAfterClose = partner; End(); }
                else CloseWith(accepted);
                return;
            case ReplyKind.HandBack:
                if (!partner.JobReady) return;
                CloseWith(partner.CompleteJob());
                return;
            case ReplyKind.Refuse:
                if (!partner.CanRefuse) return;
                CloseWith(partner.RefuseJob());
                return;
            case ReplyKind.Topic:
                ClearReplies();
                ui.SetLine(partner.AskTopic(reply.Topic));
                RefreshPortrait();
                highlighted = 0;
                // The press that asked can't also skip the answer.
                inputReadyAt = Time.time + .15f;
                return;
        }
    }

    private int IndexOf(ReplyKind kind)
    {
        for (int i = 0; i < replies.Count; i++)
            if (replies[i].Kind == kind) return i;
        return -1;
    }

    private void ClearReplies()
    {
        replies.Clear();
        ui.SetOptions("");
    }

    // E, Enter, a left click, or A on a controller.
    private static bool Pressed(Keyboard kb)
    {
        Mouse mouse = Mouse.current;
        return kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            || mouse != null && mouse.leftButton.wasPressedThisFrame
            || PadInput.Pressed(PadButton.South);
    }

    // Up is -1, down is +1: W/S, the arrow keys, the wheel, the D-pad, or the stick (one step each push).
    private int Move(Keyboard kb)
    {
        int move = 0;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) move--;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) move++;
        }
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > .01f) move--;
            else if (wheel < -.01f) move++;
        }
        if (PadInput.Pressed(PadButton.DpadUp)) move--;
        if (PadInput.Pressed(PadButton.DpadDown)) move++;
        float stick = PadInput.LeftStick.y;
        if (stickArmed && Mathf.Abs(stick) > .6f)
        {
            move += stick > 0f ? -1 : 1;
            stickArmed = false;
        }
        else if (Mathf.Abs(stick) < .3f) stickArmed = true;
        return Mathf.Clamp(move, -1, 1);
    }

    // 1-4 on the number row or the keypad: that reply, directly. -1 for none.
    private static int Direct(Keyboard kb)
    {
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
        return -1;
    }

    private const string Gold = "#D4A23A", Bright = "#FFFFFF", Soft = "#E4DFD6", Faded = "#8E8980",
        KeyColour = "#CFC27D", HintColour = "#9C968C";

    // The list as drawn: the highlighted reply bright with a marker and its key, asked topics faded
    // (with "noted" when Ace learned something), the turn-away line with Q, and a small footer.
    private string Render()
    {
        var text = new StringBuilder();
        for (int i = 0; i < replies.Count; i++)
        {
            ConversationReply reply = replies[i];
            bool on = i == highlighted;
            bool asked = reply.Topic != null && reply.Topic.Asked;
            text.Append(on ? $"<color={Gold}>►</color><space=0.35em>" : "<space=1.15em>");
            text.Append($"<color={(on ? Bright : asked ? Faded : Soft)}>{reply.Text}</color>");
            if (asked && reply.Topic.Taught) text.Append($" <size=72%><color={Gold}>noted</color></size>");
            if (on) text.Append($"  <size=78%><color={KeyColour}>[{ControlHints.Interact}]</color></size>");
            else if (reply.Kind == ReplyKind.Refuse) text.Append($"  <size=78%><color={KeyColour}>[{ControlHints.Refuse}]</color></size>");
            text.Append('\n');
        }
        text.Append(Footer());
        return text.ToString();
    }

    private string Footer()
    {
        string back = ControlHints.Say("Esc", ControlHints.Back);
        if (replies.Count < 2) return $"<size=74%><color={HintColour}>[{back}]  Step away</color></size>";
        string choose = ControlHints.Pad ? "D-pad" : "W/S";
        return $"<size=74%><color={HintColour}>[{choose}]  Choose      [{back}]  Step away</color></size>";
    }

    // ---------- closing ----------

    // A closing line: the replies go, and the conversation ends once it has been read (or on E).
    private void CloseWith(string line)
    {
        ClearReplies();
        ui.SetLine(line);
        RefreshPortrait();
        closing = true;
        closeAt = -1f;
        // The press that chose can't also close it.
        inputReadyAt = Time.time + .15f;
    }

    // How long the last line of a closing stays once everything is up: long lines aren't cut off.
    private float ClosingRead()
    {
        string line = ui != null ? ui.Line : "";
        int last = line.LastIndexOf('\n');
        int length = last >= 0 ? line.Length - last - 1 : line.Length;
        return Mathf.Clamp(length / 30f, .4f, 2.5f);
    }

    private void RefreshPortrait()
    {
        if (ui == null || partner == null || partner.Identity == null) return;
        CustomerIdentity identity = partner.Identity;
        // The panel shows how they took the last line; the ticket rail is the
        // place that shows their running mood (CustomerIdentity.ExpressionAt).
        ui.SetPortrait(identity.PanelPortraitAt(partner.PatienceFraction), identity.PanelExpressionAt(partner.PatienceFraction));
    }
}
