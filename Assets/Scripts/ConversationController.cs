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
//   * step away (Esc or Tab; F stepped away too until playtest 3 retired F by day).
// W/S, the arrow keys or the mouse wheel move the highlight; E, Enter or a click picks it; 1-4 pick one
// directly. On a controller: the stick or D-pad, A picks, Y turns them away, B steps away.
// Dialogue gates the decision: nothing can be picked until their line is up, and an E pressed before
// then only brings the replies up. A closing line is skippable with E.
//
// The pace (playtest 4, 10 Oct 2026: the veteran counted four presses of nine per coffee in the conversation, "talk, hear
// them out, hear them out, take the request"). Lines that offer no choice already move on by themselves: the whole line
// shows at once, the replies come up after the listening beat (0.35-1 s, ConversationUI), and a closing line closes once
// it's read. So the presses a player needs are two, E to talk and E to take it; an E in between only brings the replies up
// sooner (§6.1's fast path: three). The fourth was a press thrown away: for 0.2 s after opening every press was ignored, so
// a quick second E did nothing. Now one press does one thing: in the frame a press opened it, asked or picked, no press
// counts again; from the next frame every press is the player's own (inputDelay can still add a wait; the café scene's is 0).
//
// THE VIEW, SKYRIM'S WAY (12 Oct 2026). Mansoor's playtest: "when talking to the customer to the far left, lets say im right
// infront of this customer whose on the left side, even if im looking right at him and talk, the camera angle shifts to the
// right and downwards ... it should feel like how it feels when talking to anyone in skyrim". The conversation's camera was a
// fixed one behind the counter (x -0.48, 1.47 m up, 0.8 m behind the till) that only turned: for the customer on the left
// the view jumped 2.2 m right, 18 cm down and 72 cm back (the conversation view probe). Agreed (claude/playtest-4-findings-
// and-proposals.md §2.2): the same camera, placed where Ace is.
//   * In first person it sits at Ace's eyes (CafeViewMode.EyePoint): on E the view only turns to their face and leans in,
//     the lens going from the walk's 65° to 45° over the brain's blend; their face a little above the middle of the screen,
//     so the subtitle and the replies never cover it. When it ends the view eases back to where Ace was looking.
//   * From the overhead view it comes down to just behind Ace's shoulder (Shoulder Offset), and Ace is drawn in the shot,
//     turned to face them (CafeViewMode.TalkingOverShoulder, AceBody). With no room behind him for it (a wall, a shelf), it is
//     seen from his eyes instead.
// ---------------------------------------------------------------------------
public class ConversationController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera conversationCam;
    [SerializeField] private ConversationUI ui;
    [SerializeField] private PlayerInteractor interactor;

    [Tooltip("Seconds after opening before a press counts. The frame that opened it never counts, so the key that started " +
             "it can't also answer it; 0 means a second E counts from the next frame. (At 0.2, until playtest 4, a quick " +
             "second E was thrown away.)")]
    [SerializeField] private float inputDelay = 0f;
    [Tooltip("Pause after a closing line has been read, before the panel closes (E closes it at once). " +
             "The customer does not move until this elapses, so raising it holds " +
             "them at the counter longer.")]
    [SerializeField] private float closingPause = 1.2f;

    [Header("The view (12 Oct: Skyrim's way)")]
    [Tooltip("The conversation's lens, degrees (the walk's is 65): the lean in.")]
    [SerializeField, Range(25f, 70f)] private float lensDegrees = 45f;
    [Tooltip("Where their face sits on the screen, above the middle as a share of its height: a little above, so the " +
             "subtitle and the replies never cover it.")]
    [SerializeField, Range(-.3f, .3f)] private float faceAboveMiddle = .07f;
    [Tooltip("From the overhead view: where the view sits from Ace's eyes, metres, as he faces them: x to his right, y up, " +
             "z forward (negative: behind him). His head and shoulder stay at the left edge of the picture.")]
    // 26 Oct 2026 (Art fixes) a36: "Ace's head fills the talk camera" (the art audit, 23 Oct: "Ace's blocky hair and flat ear fill the
    // left fifth of the screen"). About 20 cm further from his eyes (0.68 m to 0.87 m), mostly back and a little out, so his head
    // keeps the left edge but at about three quarters of the size. Fixit Fidget > Art fixes > 3 sets the same on the scene's copy.
    public static readonly Vector3 ShoulderOffsetDefault = new Vector3(.6f, .06f, -.62f);
    [SerializeField] private Vector3 shoulderOffset = ShoulderOffsetDefault;
    [Tooltip("The least room behind Ace the shot over his shoulder needs, metres from his eyes. With less (a wall, a shelf), " +
             "the conversation is seen from his eyes instead.")]
    [SerializeField, Range(.1f, .8f)] private float shoulderRoom = .35f;

    // Keep ownership through the closing frame, so Escape cannot also move
    // the player out of something else later in that same frame.
    public bool InConversation => conversationOpen || Time.frameCount == closedAtFrame;

    /// <summary>The conversation open now is seen from just behind Ace's shoulder (it began in the overhead view): Ace is drawn.</summary>
    public bool SeenOverShoulder => conversationOpen && seenOverShoulder;

    /// <summary>The conversation's camera (the labs measure it).</summary>
    public CinemachineCamera ConversationCamera => conversationCam;

    /// <summary>Who Ace is talking to, or null.</summary>
    public CustomerBrain Partner => conversationOpen ? partner : null;

    /// <summary>
    /// A conversation is open, whoever it's with. The floating patience bars hide meanwhile
    /// (CustomerBrain): otherwise the close-up shows the bars over the speaker, and over anyone
    /// standing near them, across the top of the screen.
    /// </summary>
    public static bool AnyOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyOpen = false;

    private CustomerBrain partner;
    private CafeViewMode view;
    private AceBody aceBody;
    // This conversation is seen from just behind Ace's shoulder (it began in the overhead view, with room behind him).
    private bool seenOverShoulder;
    private float inputReadyAt;
    // The frame a press last did something here (opened it, asked, picked): no press counts again in that frame, so one press
    // can't do two things. A press in any later frame is the player's own (playtest 4).
    private int pressedFrame = -1;
    private bool closing;
    private float closeAt;
    private CustomerBrain counterAfterClose;
    private bool conversationOpen;
    private int closedAtFrame = -1;
    // The Night 1 slice: the morning after a night, the straight-face scene runs
    // first (MorningFace), then the usual request. Session 3: the officer's question
    // runs after his order instead (MorningFace.AfterTheOrder), and then the conversation closes.
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
        pressedFrame = Time.frameCount;
        replies.Clear();
        highlighted = 0;

        // Take ownership of their body. Until End(), nothing moves them.
        brain.OnConversationOpened(this);

        // Frame them, from where Ace is (12 Oct): his eyes in first person, just behind his shoulder from above. The Rotation
        // Composer holds the shot on their face, so look input does nothing.
        if (conversationCam != null)
        {
            if (view == null) view = GetComponent<CafeViewMode>();
            if (aceBody == null) aceBody = GetComponent<AceBody>();
            conversationCam.Target.TrackingTarget = brain.LookTarget != null
                ? brain.LookTarget : brain.transform;
            seenOverShoulder = view != null && view.isActiveAndEnabled && !view.FirstPersonSelected
                && RoomBehindAce(out _, out _) >= shoulderRoom;
            if (seenOverShoulder && aceBody != null) aceBody.FaceToward(brain.transform.position);
            PlaceTheView();
            conversationCam.Lens.FieldOfView = lensDegrees;
            var composer = conversationCam.GetComponent<CinemachineRotationComposer>();
            if (composer != null)
            {
                ScreenComposerSettings composition = composer.Composition;
                composition.ScreenPosition = new Vector2(0f, -faceAboveMiddle);   // the composer's y runs down the screen
                composer.Composition = composition;
            }
            conversationCam.Priority = 40;
        }

        Sprite face = brain.Identity != null ? brain.Identity.Portrait : null;
        Color tint = brain.Identity != null ? brain.Identity.ThemeColor : Color.white;
        ui.FaceAboveMiddle = faceAboveMiddle;   // the replies' column starts beside their face (12 Oct)
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
        seenOverShoulder = false;
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
        PlaceTheView();

        bool ready = Time.time >= inputReadyAt && Time.frameCount > pressedFrame;
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
        // to what they came in for, as usual. A question after the order (the
        // officer's) has had its answer read by then: the conversation closes.
        if (morningFace != null)
        {
            morningFace.Tick(Time.deltaTime, Time.time >= inputReadyAt);
            if (morningFace.Now != MorningFace.Step.Done) return;
            bool afterOrder = morningFace.AfterOrder;
            morningFace = null;
            if (afterOrder) { End(); return; }
            ui.SetLine(partner.HearIntake());
            RefreshPortrait();
            inputReadyAt = Time.time + inputDelay;
            pressedFrame = Time.frameCount;   // the press that ended the scene can't also skip what they came for
            return;
        }

        // Controller: B steps away. Tab too, as in Skyrim. (F and X did as well, until playtest 3 retired them by day.)
        bool stepAway = ready && (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
            || PadInput.Pressed(PadButton.East));
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
            ui.SetReplies(null, -1, Footer());
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
        ui.SetReplies(Render(), highlighted, Footer());
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

    // ---------- the view ----------

    // Where the conversation is seen from: Ace's eyes, or just behind his shoulder (stopping short of anything behind him).
    private void PlaceTheView()
    {
        if (conversationCam == null || partner == null) return;
        if (view == null) view = GetComponent<CafeViewMode>();
        if (view == null) return;
        Vector3 eye = view.EyePoint;
        if (!seenOverShoulder)
        {
            conversationCam.transform.position = eye;
            return;
        }
        float room = RoomBehindAce(out Vector3 toward, out float wanted);
        // Never nearer his eyes than the room it began with allowed (no sudden lurch into his head), never into what's behind.
        float distance = Mathf.Clamp(room - .05f, Mathf.Min(wanted, shoulderRoom - .05f), wanted);
        conversationCam.transform.position = eye + toward * distance;
    }

    // How far the view can go from Ace's eyes toward the place over his shoulder before it meets something (a wall, a shelf,
    // the back counter), metres; <paramref name="toward"/> is that way, <paramref name="wanted"/> how far the place is.
    private float RoomBehindAce(out Vector3 toward, out float wanted)
    {
        toward = Vector3.back;
        wanted = 0f;
        if (view == null || partner == null) return 0f;
        Vector3 eye = view.EyePoint;
        Vector3 face = partner.LookTarget != null ? partner.LookTarget.position : partner.transform.position + Vector3.up * 1.6f;
        Vector3 forward = face - eye;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 offset = right * shoulderOffset.x + Vector3.up * shoulderOffset.y + forward * shoulderOffset.z;
        wanted = offset.magnitude;
        if (wanted < 1e-3f) return 0f;
        toward = offset / wanted;
        // A sphere the size of a camera's head room, from inside Ace's own capsule (a cast ignores what it starts in).
        return Physics.SphereCast(eye, .1f, toward, out RaycastHit hit, wanted, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.distance : wanted;
    }

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
        // The NPC life pass (13 Oct 2026): they take Ace's reply in with a nod (CustomerBrain.OnAceReplied; visual only).
        if (partner != null) partner.OnAceReplied();
        switch (reply.Kind)
        {
            case ReplyKind.Accept:
                if (!partner.CanAcceptJob) return;
                string accepted = partner.AcceptJob();
                if (partner.CanFixAtCounter) { counterAfterClose = partner; End(); return; }
                // Session 3: the officer has a question after his order (OfficerStory): "say nothing".
                morningFace = MorningFace.AfterTheOrder(partner, ui, accepted);
                if (morningFace != null)
                {
                    ClearReplies();
                    morningFace.Begin();
                    RefreshPortrait();
                    // The press that ordered can't also skip the question (its meter reads inputReadyAt: left as it was).
                    inputReadyAt = Time.time + .15f;
                    pressedFrame = Time.frameCount;
                    return;
                }
                CloseWith(accepted);
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
                // The press that asked can't also skip the answer; the next one can (it only brings the replies back).
                pressedFrame = Time.frameCount;
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

    private const string Gold = "#D4A23A", Soft = "#E4DFD6", Faded = "#8E8980", HintColour = "#D9D2C7";
    // The highlighted reply: the brand's light green (12 Oct, the new layout), on its plate with a green edge (ConversationUI).
    private static readonly string Lit = UiSkin.HexOf(UiSkin.BrandBright);
    private readonly List<string> rows = new();

    // The list as drawn, a row a reply (ConversationUI puts each on its own plate): the highlighted reply in light green with
    // a marker and its button's icon, asked topics faded (with "noted" when Ace learned something), the turn-away line with
    // its button's icon. The icons (19 Oct 2026: her "Instead of writing out square, just put the icon") sit after the words
    // at the words' own size, no brackets: ControlHints.
    private List<string> Render()
    {
        rows.Clear();
        var text = new StringBuilder();
        for (int i = 0; i < replies.Count; i++)
        {
            ConversationReply reply = replies[i];
            bool on = i == highlighted;
            bool asked = reply.Topic != null && reply.Topic.Asked;
            text.Clear();
            text.Append(on ? $"<color={Lit}>►</color><space=0.35em>" : "<space=1.15em>");
            text.Append($"<color={(on ? Lit : asked ? Faded : Soft)}>{reply.Text}</color>");
            if (asked && reply.Topic.Taught) text.Append($" <size=72%><color={Gold}>noted</color></size>");
            if (on) text.Append($"  {ControlHints.Interact}");
            else if (reply.Kind == ReplyKind.Refuse) text.Append($"  {ControlHints.Refuse}");
            rows.Add(text.ToString());
        }
        return rows;
    }

    // The keys' line under the replies: icon + verb, no brackets (19 Oct 2026).
    private string Footer()
    {
        string back = ControlHints.Back;
        if (replies.Count < 2) return $"<color={HintColour}>{back}  Step away</color>";
        return $"<color={HintColour}>{ControlHints.Choose}  Choose      {back}  Step away</color>";
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
        // The press that chose can't also close it; the next one can.
        pressedFrame = Time.frameCount;
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
