using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// DAY 1'S GUIDE - PLAY CHECK (playtest 4, 10 Oct 2026; a lab session only: Fixit Fidget > Playtest 4 > UI > 1 - Day 1
// guide - play check (lab, drives itself))
//
// A fresh Day 1 in the lab (the lab's own save; the playtest save is never used), the overhead view, the keyboard's words.
// It drives Grace's visit with the game's own calls (as the conversation check does) up to her camera on the bench, and
// photographs the cards and the pins at each step, at 1920 x 1080:
//   1. the first screen: "You're Ace. ..." on the card, "You" over Ace, "WASD Move · E Use" in the strip;
//   2. a step along the counter on Ace's own legs (a scripted stick, as the café lab walks him): "You" goes; the strip
//      stays until he has walked a few metres and used E;
//   3. Grace at the counter: the talk card, her name pinned over her head (E's target), her patience bar over her at the
//      bar's size (UiPins.BarWidth x BarHeight: 72 x 9 px at BarScale 1.3), coloured by her patience; the card settles to
//      one line after ~6 s and comes back in full after ~10 s more;
//   4. in first person nothing is pinned (the crosshair has the name); a few metres more on foot; E for real (when the
//      Game view has the keyboard): the conversation, no card and no pins over it, the strip's movement words go; she's
//      heard and the camera taken;
//   5. the intake tray: the card says so, and Day 1's pin says "Intake tray" over the camera;
//   6. picked up: "Carry the reunion film camera to the repair bench", the pin over the bench;
//   7. set down on the bench; 8. worked on: the card over the bottom prompt near the work, no pins, no job number, the
//      mouse's tools line; stepped back;
//   9. Grace frustrated (her patience set low): the impatience card once, with her pin; reassured, it goes;
//  10. the Officer, if he comes in within the check's time: his talk card ("DAY 1 · THE OFFICER"), his coffee's first
//      steps (the drinks machine on the back bar, "Drinks" pinned over it, the cup stack in its close-up).
// Playtest 4 run 2 (11 Oct): right after the pickup at the till, E won't give the camera straight back unfixed; a walk-in
// sent in at the door as the Officer sets off reaches the counter first, and the card names whoever E talks to (that
// walk-in, "the Officer is at the counter too"); at the drinks machine the card stays on his coffee, not Grace's latte.
// 12-13 Oct (the findings' §4: the bar bigger, "not too big obviously but enough for it to feel good and look good"): the bar,
// the name over her head and her job's dot are measured against UiPins' one family (BarScale), never a number of their own;
// and three close-ups of her head (name, bar, dot) at three times a 1080p screen's size, for the Producer's eye on the size:
// 02b when her bar first shows, 05b with her job's colour beside it, 09b impatient (the bar low and red, her name pinned).
// The repair itself is the bench's (Bench 3). Photos and report.txt go to Logs/UI/day1-guide-<time>/. Nothing is saved in
// the scene; the lab's save is the only file the game writes. Play mode ends by itself when it reports.
// ---------------------------------------------------------------------------
public sealed class Day1GuideCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Playtest4.Day1GuideCheck";

    protected override string Title => "Day 1's guide: the cards, the pins, the names and the bars - play check";
    protected override string Tag => "[Day 1 guide check]";

#if UNITY_EDITOR
    // Asked for by the editor (Playtest4UISteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Day 1 guide check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Day 1 guide check (this Play session only)");
        go.AddComponent<Day1GuideCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "UI",
            $"day1-guide-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    ConversationController conversation;
    DayOneGuideUI guide;
    ShopUI shop;
    UiPins pins;
    MethodInfo setPad;
    bool padWas;

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.Day == 1 && DayClock.Instance.IsOpen
                                 && !DayClock.Instance.DayOver && Camera.main != null, 30f, "the lab opened on a fresh Day 1");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        guide = FindAnyObjectByType<DayOneGuideUI>();
        shop = FindAnyObjectByType<ShopUI>();
        pins = UiPins.Instance;
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null && conversation != null,
            "Ace is in the scene, with his interactor, view, hands, bench and conversation");
        Check(guide != null && shop != null && pins != null, "the HUD has its guide, its controls strip and the pins over the café");
        if (movement == null || interactor == null || view == null || carry == null || inspector == null || conversation == null
            || guide == null || shop == null || pins == null) yield break;

        // The keyboard's words for the check (a pad touched in an earlier lab leaves its flag up), put back at the end.
        setPad = typeof(PadInput).GetProperty("UsingPad", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true);
        padWas = PadInput.UsingPad;
        setPad?.Invoke(null, new object[] { false });
        if (view.FirstPersonSelected)
        {
            view.SetFirstPerson(false);
            yield return Seconds(1.5f);
        }
        yield return Seconds(.6f);
        Note($"the Game view is {Screen.width} x {Screen.height}; photos are saved at 1920 x 1080; the HUD's scale here is {HudScale():0.00}");

        // ---------- 1. the first screen ----------
        yield return Until(() => guide.CardShowing && guide.CardText.Contains("You're Ace"), 15f,
            "the first card says who you are (\"You're Ace. ...\")");
        string first = guide.CardText;
        Check(first.Contains("DAY 1 · GRACE") && (first.Contains(DayOneGuideUI.FirstCard("Grace", true)) || first.Contains(DayOneGuideUI.FirstCard("Grace", false))),
            $"...and who is on the way, in the guide's words: \"{Plain(first)}\"");
        Check(interactor.BehindCounter != first.Contains("behind the counter"),
            $"\"behind the counter\" only when Ace isn't there already (he is{(interactor.BehindCounter ? "" : "n't")})");
        Check(pins.SelfShowing && pins.SelfText == DayOneGuideUI.SelfWords, "\"You\" is pinned over Ace");
        Check(DayOneGuideUI.MovementHintWanted && shop.ViewHint.Contains("WASD") && shop.ViewHint.Contains("Move") && shop.ViewHint.Contains("Use"),
            $"Day 1's controls strip says how to walk and use: \"{Plain(shop.ViewHint)}\"");
        yield return Photo1080("01-the-first-screen");

        // ---------- 2. a step along the counter (his own legs): "You" goes ----------
        Transform till = interactor.CounterStation != null ? interactor.CounterStation.StandPoint : null;
        StationInteractable bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        Check(till != null && bench != null, "the counter and the repair bench have stand points");
        if (till == null || bench == null) yield break;
        bool youWasUp = pins.SelfShowing;
        Vector3 along = bench.StandPoint.position - till.position;
        along.y = 0f;
        yield return WalkTo(till.position + along.normalized * 1.2f, 6f);
        yield return WalkTo(till.position, 6f);
        yield return Frames(3);
        if (youWasUp) Check(!pins.SelfShowing, $"\"You\" goes once Ace moves (he walked {guide.WalkedOnDayOne:0.0} m)");
        Check(DayOneGuideUI.MovementHintWanted, "the strip's \"WASD Move · E Use\" stays until he has walked a few metres and used E");

        // ---------- 3. Grace at the counter ----------
        CustomerBrain grace = null;
        yield return Until(() => (grace = FindRegular(GraceCameraEpisode.ProfileId)) != null, 60f, "Grace comes in");
        if (!lastWait) yield break;
        // The guide reads the café every 0.15 s: give it a look at her before judging its card and pin.
        yield return Seconds(.4f);
        if (!grace.CanHearIntake && !grace.CanDecide)
            Check(guide.CardText.Contains("You're Ace") && guide.PinWho == grace.transform,
                "while she walks in, the card still says who you are, with the pin over her");
        yield return Until(() => grace == null || grace.CanHearIntake, 150f, "she reaches the counter");
        if (!lastWait || grace == null) yield break;
        if (!interactor.BehindCounter) PutAce(till.position, till.eulerAngles.y);
        string talk = DayOneGuideUI.TalkWords("Grace", false, false);
        yield return Until(() => guide.CardText.Contains(talk), 5f, $"the card: \"{talk}\"");
        Check(guide.PinLabel == "Grace" && guide.PinWho == grace.transform, "Day 1's pin is on Grace");
        // The name fades in as the guide's pin fades out (an eighth of a second each): judged once they have, not mid-way
        // (11 Oct: one run of three judged it 4 frames in, both pins still crossing).
        yield return Await(() => pins.FocusShowing && pins.FocusText == "Grace" && !pins.GuideShowing, 1.5f);
        Check(pins.FocusShowing && pins.FocusText == "Grace" && !pins.GuideShowing,
            "her name is pinned over her head (what E talks to), one pin: the guide's own stands aside for the name");
        Rect name = pins.FocusOnScreen;
        Vector3 head = Camera.main.WorldToScreenPoint(Barks.HeadPointOf(grace.transform));
        Check(name.yMin >= head.y - 1f && Mathf.Abs(name.center.x - head.x) < name.width, $"...above her head ({name.yMin:0} px up; her head point {head.y:0})");
        Check(Mathf.Abs(pins.FocusTextSize - UiPins.NameSize) < .01f,
            $"...her name at {pins.FocusTextSize:0} px, the bar's family ({UiPins.Family()})");
        float sf = PinsScale();
        bool barUp = pins.TryGetBar(grace.transform, out Rect bar, out Color colour, out bool dot);
        if (barUp)
        {
            Check(Mathf.Abs(bar.width - UiPins.BarWidth * sf) <= 1.5f && Mathf.Abs(bar.height - UiPins.BarHeight * sf) <= 1f,
                $"her patience bar is {bar.width / sf:0} x {bar.height / sf:0} px at 1080p ({bar.width:0} x {bar.height:0} on this screen; " +
                $"the rule is {UiPins.BarWidth:0} x {UiPins.BarHeight:0}, BarScale {UiPins.BarScale:0.0#})");
            Color want = UiPins.ColourAt(grace.PatienceFraction);
            Check(Mathf.Abs(colour.r - want.r) + Mathf.Abs(colour.g - want.g) + Mathf.Abs(colour.b - want.b) < .08f,
                $"...coloured by her patience ({grace.PatienceFraction:0.00})");
            Check(bar.yMax <= name.yMin + .5f, "...and under her name, never on it");
            Check(!dot, "no job colour beside it yet (nothing taken)");
        }
        else Check(false, "her patience bar is drawn over her (UiPins)");
        yield return Photo1080("02-grace-at-the-counter-name-and-bar");
        // Close on her head the moment her bar shows: her name over it, the bar under it (the Producer judges the size by eye).
        if (barUp) yield return CloseOnHead("02b-patience-bar-close", grace.transform);

        // The card stays: settled to one line after ~6 s, back in full after ~10 s more with no progress.
        yield return Until(() => guide.CardCompact, 9f, "after about six seconds the card settles to one smaller line, still on screen");
        if (lastWait) yield return Photo1080("03-the-card-settled");
        yield return Until(() => !guide.CardCompact && guide.CardReminding, 13f, "ten seconds more with no progress: back in full, once (a pulse, no sound)");
        if (lastWait) yield return Photo1080("04-the-card-back-in-full");

        // In first person the name is the crosshair's (as it always was): nothing pinned.
        view.SetFirstPerson(true);
        yield return Seconds(1.6f);
        Check(!pins.FocusShowing, "in first person no name is pinned (the crosshair has it)");
        view.SetFirstPerson(false);
        yield return Seconds(1.6f);

        // A few metres more on foot, if he hasn't walked them yet (to the bench and back, behind the counter).
        if (guide.WalkedOnDayOne < DayOneGuideUI.WalkMetres)
        {
            yield return WalkTo(bench.StandPoint.position, 8f);
            yield return WalkTo(till.position, 8f);
            yield return Frames(3);
        }
        Check(guide.WalkedOnDayOne >= DayOneGuideUI.WalkMetres, $"Ace has walked {guide.WalkedOnDayOne:0.0} m on his own legs");
        if (!interactor.BehindCounter) PutAce(till.position, till.eulerAngles.y);
        yield return Seconds(.4f);

        // ---------- 4. E: the conversation ----------
        yield return PressKey(Key.E);
        bool realE = conversation.InConversation;
        if (!realE)
        {
            Note("the Game view did not have the keyboard: the conversation was opened directly (as E on her does)");
            conversation.Begin(grace);
        }
        yield return Frames(3);
        Check(conversation.InConversation, "E on her opens the conversation");
        Check(!guide.CardShowing && pins.AllHidden, "a conversation has the screen: no card, no pins");
        if (realE)
            Check(!DayOneGuideUI.MovementHintWanted && !shop.ViewHint.Contains("WASD"),
                "walked a few metres and used E: the strip's \"WASD Move · E Use\" goes");
        else Note("E wasn't pressed for real, so the strip's movement words stay (they go after a real E)");
        conversation.Advance();
        yield return Until(() => conversation.RepliesShowing, 10f, "her request, then Ace's replies");
        if (!lastWait) yield break;
        int take = conversation.Replies.ToList().FindIndex(r => r.Kind == ReplyKind.Accept);
        Check(take >= 0, $"a reply takes the job ({string.Join(" | ", conversation.ReplyTexts)})");
        if (take < 0) yield break;
        conversation.ChooseReply(take);
        yield return Until(() => grace == null || grace.WasAccepted, 3f, "Ace takes her camera");
        for (int i = 0; i < 8 && conversation.InConversation; i++)
        {
            conversation.Advance();
            yield return Seconds(.4f);
        }
        yield return Until(() => !conversation.InConversation, 12f, "the conversation closes");
        if (grace == null) yield break;

        // ---------- 5. the intake tray ----------
        yield return Until(() => grace.ActiveJob != null, 10f, "her camera comes out on the intake tray");
        JobBase camera = grace != null ? grace.ActiveJob : null;
        if (camera == null) yield break;
        yield return Until(() => guide.CardText.Contains("from the intake tray"), 6f, "the card: pick it up from the intake tray");
        Check(guide.CardText.Contains("Press E to pick up Grace's") && !guide.CardText.Contains("shelf"), $"...in those words: \"{Plain(guide.CardText)}\"");
        Check(guide.PinLabel == FocusNameUI.TrayName && guide.PinWho == null
              && (guide.PinPoint - FocusNameUI.TopOf(camera.gameObject)).magnitude < .6f, "Day 1's pin says \"Intake tray\", over the camera on it");
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);   // a step away: nothing in reach, so the pin shows alone
        yield return Seconds(.6f);
        Check(pins.GuideShowing && pins.GuideText == FocusNameUI.TrayName, "...on screen, from across the café");
        bool tagged = false;
        if (pins.TryGetBar(grace.transform, out Rect waiting, out _, out tagged))
        {
            Check(tagged, "her bar has her job's colour beside it now (the ticket's tape, the device's tag)");
            if (tagged && pins.TryGetDot(grace.transform, out Rect jobDot))
                Check(Mathf.Abs(jobDot.width - UiPins.TagDot * sf) <= 1f && Mathf.Abs(jobDot.center.y - waiting.center.y) <= 1f
                      && jobDot.xMax <= waiting.xMin + .5f,
                    $"...a {jobDot.width / sf:0} px dot (the family's {UiPins.TagDot:0}), level with the bar, just left of it");
        }
        yield return Photo1080("05-the-intake-tray");
        if (tagged) yield return CloseOnHead("05b-patience-bar-close-her-job-colour", grace.transform);

        // ---------- 6. picked up ----------
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(3);
        ItemInteractable item = camera.GetComponentInChildren<ItemInteractable>();
        Check(item != null, "the camera can be picked up");
        if (item == null) yield break;
        item.Interact(interactor);
        yield return Until(() => carry.Contains(camera), 3f, "Ace picks the camera up (as E on it does)");
        // Grace is still within E's reach on her way to a seat: E must not give her the camera straight back unfixed (playtest
        // 4 run 2, the first hour's #2: "[E] Hand it back (Rejected)" under this very card, one press from "Never again").
        yield return Frames(3);
        string prompt = interactor.CurrentPrompt ?? "";
        Check(!grace.JobReady && !prompt.Contains("Hand it back") && !prompt.Contains("unfixed"),
            $"...and E won't give it straight back to her unfixed (the bottom line: \"{(prompt.Length > 0 ? prompt : "nothing")}\")");
        // Checklist v2 §5.7: on the carry step the intake tray doesn't offer it back either; the line shows only "In hand".
        Check(!prompt.Contains(DropInteractable.IntakeTrayWords) && !prompt.Contains(DropInteractable.PutBackWords)
              && Plain(shop.PromptLine).Contains("In hand"),
            $"...nor does the intake tray (the line: \"{Plain(shop.PromptLine)}\")");
        yield return Until(() => guide.CardText.Contains("to the repair bench"), 4f, "the card: carry it to the repair bench");
        Check(guide.PinLabel == FocusNameUI.BenchName, "Day 1's pin: \"Repair bench\"");
        yield return Seconds(.6f);
        Check(pins.GuideShowing && pins.GuideText == FocusNameUI.BenchName, "...over the bench, where it is from the counter");
        yield return Photo1080("06-carry-it-to-the-bench");

        // ---------- 7. on the bench ----------
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Frames(3);
        bench.Interact(interactor);
        yield return Until(() => !carry.Contains(camera) && StationInteractable.BenchHolds(camera), 3f, "...and sets it down on the bench (as E there does)");
        yield return Until(() => guide.CardText.Contains("At the repair bench, press E"), 4f, "the card: E at the bench works on it");
        // Within a second (15 Oct: four frames at 15 fps caught the green pin giving way to the name as it faded in).
        for (float until = Time.realtimeSinceStartup + 1f; !(pins.FocusShowing || pins.GuideShowing) && Time.realtimeSinceStartup < until;) yield return null;
        Check(pins.FocusShowing || pins.GuideShowing, $"a pin over it says what it is (\"{(pins.FocusShowing ? pins.FocusText : pins.GuideText)}\")");
        Check(!(pins.FocusShowing && pins.GuideShowing && pins.FocusOnScreen.Overlaps(pins.GuideOnScreen)),
            $"...one pin, not two on each other (the name \"{pins.FocusText}\"{(pins.GuideShowing ? ", the green pin \"" + pins.GuideText + "\"" : "")})");
        yield return Photo1080("07-on-the-bench");

        // ---------- 8. at the bench ----------
        Check(interactor.WorkOn(camera.GetComponentInChildren<ItemInteractable>()), "E on it at the bench brings it into the close-up");
        yield return Until(() => inspector.IsHoldingItem, 3f, "...the close-up is up");
        yield return Seconds(1.6f);
        // Checklist v2 §5.6: no guide card at the bench; the bench's own card names her and the step's tool glows.
        Check(!guide.CardShowing, "at the bench there is no guide card: the bench's card and the glow are the guide");
        Check(BenchCards.Showing && BenchCards.JobLine.StartsWith("Grace:"), $"...the bench's card names her: \"{BenchCards.JobLine}\" / \"{BenchCards.CurrentVerb}\"");
        Check(BenchGlow.GlowingTool != null && BenchCards.CurrentStep != null && BenchGlow.GlowingTool.tool == BenchCards.CurrentStep.tool,
            $"...and the step's tool glows in its stand ({(BenchGlow.GlowingTool != null ? BenchGlow.GlowingTool.displayName : "nothing glows")})");
        Check(pins.AllHidden, "no pins over the close-up");
        JobMarker[] markers = FindObjectsByType<JobMarker>(FindObjectsInactive.Exclude);
        Check(markers.All(m => m.LabelText.Length == 0), $"no job number anywhere ({markers.Length} job tags up in the café)");
        Check(!markers.Any(m => m.TagShowing && m.GetComponentInParent<JobBase>(true) == inspector.FocusedItem), "no tag over the device on the mat");
        // The close-up's controls are on their own card since 12 Oct (BenchCards), not the bottom prompt's tools line.
        yield return Await(() => BenchCards.ControlsText.Contains("[Drag a tool]"), 2f);
        string controls = BenchCards.ControlsText;
        Check(controls.Contains("[Drag a tool]") && controls.Contains("[Right-click]") && controls.Contains("[Tab]") && BenchCards.ControlsShowing,
            $"the first repair's controls card, in the mouse's words: \"{Plain(controls)}\"");
        yield return Photo1080("08-at-the-bench");
        inspector.StepBack();
        yield return Until(() => !inspector.IsHoldingItem, 3f, "...and right-click with bare hands puts it down and steps back");
        yield return Seconds(1.4f);

        // ---------- 9. the first frustrated customer ----------
        yield return Impatience(grace);

        // ---------- 10. the Officer's coffee, if he comes in within the check's time ----------
        yield return TheOfficer(grace, till);
    }

    // Grace's patience set low (the lab's hand): the impatience card, once; reassured, it goes.
    IEnumerator Impatience(CustomerBrain grace)
    {
        FieldInfo left = typeof(CustomerBrain).GetField("patienceLeft", Any);
        if (grace == null || left == null || left.FieldType != typeof(float) || !grace.ShowsPatience)
        {
            Note("Grace's patience couldn't be set here (she isn't waiting, or the field is gone): the impatience card isn't checked");
            yield break;
        }
        if (DayOneGuideUI.ReassuredOnce)
        {
            Note("someone was already reassured this session: the impatience card has done its job and doesn't show");
            yield break;
        }
        float was = (float)left.GetValue(grace);
        float fraction = grace.PatienceFraction;
        float max = fraction > .001f ? was / fraction : 0f;
        if (max <= 0f)
        {
            Note("Grace's patience is empty: the impatience card isn't checked");
            yield break;
        }
        left.SetValue(grace, max * .2f);
        yield return Until(() => guide.ImpatienceShowing, 3f, "someone waiting on Ace gets frustrated (the sweat drop): one card says what to do");
        if (lastWait)
        {
            string text = guide.CardText;
            Check(text.Contains(DayOneGuideUI.ImpatientTitle) && text.Contains(DayOneGuideUI.ReassureWords("Grace")),
                $"...in the guide's words: \"{Plain(text)}\"");
            yield return Seconds(.4f);   // the pin fades in (UiPins.FadeRate): judge it once it has
            Check((pins.GuideShowing && pins.GuideText == "Grace") || (pins.FocusShowing && pins.FocusText == "Grace"), "...with her name pinned over her");
            if (pins.GuideShowing && pins.GuideText == "Grace")
                Check(Mathf.Abs(pins.GuideTextSize - UiPins.PinTextSize) < .01f, $"...Day 1's pin at {pins.GuideTextSize:0} px, the family's {UiPins.PinTextSize:0}");
            yield return Photo1080("09-getting-impatient");
            yield return CloseOnHead("09b-patience-bar-close-impatient", grace.transform);
            grace.Reassure();   // what E beside her does (CustomerInteractable)
            yield return Until(() => DayOneGuideUI.ReassuredOnce && !guide.ImpatienceShowing, 3f, "reassured: the card goes, and won't come back this session");
        }
        if (grace != null) left.SetValue(grace, was);   // as she was, so the rest isn't about her leaving
    }

    // The Officer comes in on Day 1 for a coffee (the game's first drink), at his authored time: a quarter of the day since
    // 14 Oct (specs/days-1-3.md §4.1: ≈ 1:52 of a 420 s day; it was .35 of 180 s). The lab moves the clock just past his time
    // (read from today's story visits) if it isn't there yet and makes room (walk-ins sent home), then waits a while for him.
    IEnumerator TheOfficer(CustomerBrain grace, Transform till)
    {
        DayClock clock = DayClock.Instance;
        float due = Mathf.Clamp01(OfficerArrivesAt() + .01f);
        if (clock != null && clock.NormalizedDay < due)
        {
            PropertyInfo remaining = typeof(DayClock).GetProperty("TimeRemaining", BindingFlags.Instance | BindingFlags.Public);
            float length = (float)(typeof(DayClock).GetField("dayLengthSeconds", Any)?.GetValue(clock) ?? 0f);
            if (remaining != null && length > 0f)
            {
                remaining.GetSetMethod(true)?.Invoke(clock, new object[] { length * (1f - due) });
                Note($"the clock moved on to the Officer's time, {due:0.00} of the day ({ShopUI.FormatHour(clock.CurrentHour)})");
            }
        }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (c != null && c != grace && (c.Identity == null || c.Identity.Profile == null))
            {
                Note($"a walk-in ({c.CustomerName}) was sent home to make room");
                c.ForceRemove();
            }
        CustomerBrain officer = null;
        // Once he's on his way (from the street, about 45 s), one walk-in goes in at the café's door: they reach the counter
        // first, so E talks to them first (PlayerInteractor.QueueHead), and the card has to say so (playtest 4 run 2, the first
        // hour's #3: "DAY 1 · THE OFFICER" for 52 s while E talked to Tomas). The lab's hand keeps them waiting till he's there.
        yield return Await(() => (officer = FindRegular(OfficerStory.ProfileId)) != null, 60f);
        CustomerBrain sentIn = null;
        if (officer != null && CafeLabDirector.Instance != null)
        {
            var before = new System.Collections.Generic.HashSet<CustomerBrain>(FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude));
            CafeLabDirector.Instance.SendCustomersNow(1);
            foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
                if (c != null && !before.Contains(c) && (c.Identity == null || c.Identity.Profile == null)) sentIn = c;
            Note(sentIn != null ? $"a walk-in ({sentIn.CustomerName}) went in at the café's door as the Officer set off from the street"
                                : "no walk-in could be sent in at the door: the card following E to someone else may not be checked");
        }
        FieldInfo patienceLeft = typeof(CustomerBrain).GetField("patienceLeft", Any), queueMax = typeof(CustomerBrain).GetField("queueMax", Any);
        yield return Await(() =>
        {
            if (sentIn != null && !sentIn.IsLeaving && patienceLeft != null && queueMax != null && patienceLeft.FieldType == typeof(float))
                patienceLeft.SetValue(sentIn, queueMax.GetValue(sentIn));   // the lab's hand: they wait as long as it takes
            return (officer = FindRegular(OfficerStory.ProfileId)) != null && officer.CanHearIntake;
        }, 90f);
        if (!lastWait || officer == null)
        {
            Note("the Officer didn't reach the counter within the check's time: his cards aren't checked this run");
            yield break;
        }
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(4);
        // E talks to whoever has waited longest at the counter, and the card follows E: whoever reached the counter before
        // him is heard first, so the card names them, and the Officer as waiting too.
        CustomerInteractable head = interactor.QueueHead();
        CustomerBrain ahead = head != null ? head.Brain : null;
        if (ahead != null && ahead != officer)
        {
            yield return Until(() => guide.CardText.Contains("DAY 1 · " + ahead.CustomerName.ToUpperInvariant())
                                     && guide.CardText.Contains("Officer is at the counter too"), 5f,
                $"{ahead.CustomerName} reached the counter before him, so E talks to {ahead.CustomerName} first: the card names them, and the Officer as waiting too");
            Check(interactor.CurrentPrompt != null && interactor.CurrentPrompt.Contains(ahead.CustomerName),
                $"...and the bottom line agrees: \"{interactor.CurrentPrompt}\"");
            yield return Photo1080("10a-heard-first-the-card-agrees");
            Note($"{ahead.CustomerName} was sent home, to leave the Officer first in line");
            ahead.ForceRemove();
            yield return Frames(4);
        }
        else Note("nobody reached the counter before the Officer this run: the card following E to someone else isn't checked");
        string talk = DayOneGuideUI.TalkWords("the Officer", false, true);
        yield return Until(() => guide.CardText.Contains("DAY 1 · THE OFFICER") && guide.CardText.Contains(talk), 5f,
            $"the Officer at the counter: his own card (\"{talk}\")");
        yield return Frames(4);
        Check((pins.FocusShowing && pins.FocusText == officer.CustomerName) || (pins.GuideShowing && pins.GuideText == officer.CustomerName),
            "his name is pinned over him");
        yield return Photo1080("10-the-officer-at-the-counter");

        conversation.Begin(officer);
        yield return Frames(2);
        conversation.Advance();
        yield return Until(() => conversation.RepliesShowing, 10f, "his order, then Ace's replies");
        if (!lastWait) yield break;
        int take = conversation.Replies.ToList().FindIndex(r => r.Kind == ReplyKind.Accept);
        if (take < 0) { Check(false, $"a reply takes his order ({string.Join(" | ", conversation.ReplyTexts)})"); yield break; }
        conversation.ChooseReply(take);
        for (int i = 0; i < 8 && conversation.InConversation; i++)
        {
            conversation.Advance();
            yield return Seconds(.4f);
        }
        yield return Until(() => !conversation.InConversation, 12f, "...taken, and the conversation closes");
        yield return Until(() => guide.CardText.Contains("drinks machine on the back bar"), 5f, "the card: walk to the drinks machine on the back bar");
        Check(guide.PinLabel == FocusNameUI.DrinksName, "Day 1's pin: \"Drinks\", over the machine");
        yield return Seconds(.6f);
        yield return Photo1080("11-his-coffee-the-drinks-machine");

        StationInteractable drinks = interactor.DrinksStation;
        if (drinks == null || drinks.StandPoint == null) { Note("the drinks machine has no stand point here"); yield break; }
        PutAce(drinks.StandPoint.position, drinks.StandPoint.eulerAngles.y);
        yield return Frames(4);
        interactor.EnterStation(drinks);
        yield return Seconds(1.6f);
        Check(interactor.IsAtBeverageStation && guide.CardText.Contains("cup stack"), $"in its close-up the card says to take a cup: \"{Plain(guide.CardText)}\"");
        // Still his coffee: the card follows the order Ace just took (playtest 4 run 2, the first hour's #3: at the machine it
        // turned to "DAY 1 · GRACE" and her latte, which was on order too).
        bool latteOwed = grace != null && !grace.IsLeaving && grace.HasDrinkOrder;
        Check(guide.GuidedCustomer == officer && !guide.CardText.Contains("DAY 1 · GRACE") && !guide.CardText.Contains("Latte"),
            $"...his coffee's, not Grace's latte{(latteOwed ? " (her latte is on order too)" : " (her latte isn't on order this run)")}: the card follows the Officer");
        yield return Photo1080("12-at-the-drinks-machine");
        interactor.ExitStation();
        yield return Seconds(1f);
    }

    protected override void Restore()
    {
        if (movement != null) movement.ScriptedInput = null;
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        setPad?.Invoke(null, new object[] { padWas });
#if UNITY_EDITOR
        // Play mode ends by itself once the report is written (the editor's next update, after this frame's Finish).
        UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- helpers ----------

    // The Officer's authored time today (DayDefinition.storyVisits, read at the counter), or .35 (the old Day 1) if none.
    static float OfficerArrivesAt()
    {
        CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
        DayDefinition today = spawner != null ? spawner.Today : null;
        if (today != null && DayClock.Instance != null)
            foreach (StoryVisit visit in today.StoryVisitsOn(DayClock.Instance.Day))
                if (visit != null && visit.who != null && visit.who.PersistentId == OfficerStory.ProfileId) return visit.arrivesAt;
        return .35f;
    }

    static CustomerBrain FindRegular(string id)
    {
        foreach (CustomerBrain brain in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (brain != null && brain.Identity != null && brain.Identity.Profile != null && brain.Identity.Profile.PersistentId == id)
                return brain;
        return null;
    }

    // Ace walks there with his own controller (a scripted stick, as the café lab walks him), or as far as he gets.
    IEnumerator WalkTo(Vector3 to, float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
        {
            Vector3 d = to - movement.transform.position;
            d.y = 0f;
            if (d.magnitude < .35f) break;
            float yaw = view.isActiveAndEnabled ? view.MovementYaw : 45f;
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * d.normalized;
            movement.ScriptedInput = new Vector2(local.x, local.z);
            yield return null;
        }
        movement.ScriptedInput = null;
        yield return Frames(2);
    }

    void PutAce(Vector3 at, float yaw)
    {
        var controller = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (controller != null) controller.enabled = was;
    }

    // A key pressed and let go the way a player does it, when the Game view has the keyboard.
    static IEnumerator PressKey(Key key)
    {
        if (!Application.isFocused || Keyboard.current == null) yield break;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return null;
    }

    // The HUD canvas's scale on this screen (1920 x 1080 reference, half width half height).
    static float HudScale() => Mathf.Sqrt(Screen.width / 1920f * (Screen.height / 1080f));

    float PinsScale()
    {
        Canvas canvas = pins != null ? pins.GetComponentInChildren<Canvas>() : null;
        return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : HudScale();
    }

    static string Plain(string rich) => Short(RecapPhone.Plain(rich ?? ""));

    // A close look at one head, for the Producer's eye on the bar's size (the findings' §4): 320 x 180 px of a 1080p screen
    // round the head point (the bar just under it and the face under that, the name and Day 1's pin over it, room for a
    // badge), saved three times as large as a 1080p screen shows it, as a PNG (no JPEG smear on a 9 px bar). Cut from the
    // screenshot the way AceTurnCheck cuts its close-ups; enlarged here on the CPU, smoothly, so the colours pass through as
    // they were drawn. Nothing when the head is off the screen.
    const float CloseWidth = 320f, CloseBelow = 70f, CloseAbove = 110f, CloseZoom = 3f;

    IEnumerator CloseOnHead(string name, Transform who)
    {
        yield return new WaitForEndOfFrame();
        Camera cam = Camera.main;
        if (who == null || cam == null) yield break;
        Vector3 head = cam.WorldToScreenPoint(Barks.HeadPointOf(who));
        if (head.z <= 0f || head.x < 0f || head.x > Screen.width || head.y < 0f || head.y > Screen.height)
        {
            Note($"{name}: the head isn't on the screen, so no close-up");
            yield break;
        }
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        Texture2D big = null;
        try
        {
            float sx = shot.width / (float)Mathf.Max(1, Screen.width), sy = shot.height / (float)Mathf.Max(1, Screen.height);
            float sf = PinsScale();
            int w = Mathf.Clamp(Mathf.RoundToInt(CloseWidth * sf * sx), 8, shot.width);
            int h = Mathf.Clamp(Mathf.RoundToInt((CloseBelow + CloseAbove) * sf * sy), 8, shot.height);
            int x = Mathf.Clamp(Mathf.RoundToInt(head.x * sx) - w / 2, 0, shot.width - w);
            int y = Mathf.Clamp(Mathf.RoundToInt((head.y - CloseBelow * sf) * sy), 0, shot.height - h);
            Color[] cut = shot.GetPixels(x, y, w, h);
            int bw = Mathf.RoundToInt(CloseWidth * CloseZoom), bh = Mathf.RoundToInt((CloseBelow + CloseAbove) * CloseZoom);
            var pixels = new Color[bw * bh];
            for (int j = 0; j < bh; j++)
            {
                float fy = Mathf.Clamp((j + .5f) * h / bh - .5f, 0f, h - 1f);
                int y0 = (int)fy, y1 = Mathf.Min(y0 + 1, h - 1);
                float ty = fy - y0;
                for (int i = 0; i < bw; i++)
                {
                    float fx = Mathf.Clamp((i + .5f) * w / bw - .5f, 0f, w - 1f);
                    int x0 = (int)fx, x1 = Mathf.Min(x0 + 1, w - 1);
                    float tx = fx - x0;
                    Color low = Color.Lerp(cut[y0 * w + x0], cut[y0 * w + x1], tx), high = Color.Lerp(cut[y1 * w + x0], cut[y1 * w + x1], tx);
                    pixels[j * bw + i] = Color.Lerp(low, high, ty);
                }
            }
            big = new Texture2D(bw, bh, TextureFormat.RGB24, false);
            big.SetPixels(pixels);
            big.Apply();
            photos++;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), big.EncodeToPNG());
            Note($"{name}.png: her head close up at {CloseZoom:0}x a 1080p screen ({UiPins.Family()})");
        }
        finally
        {
            if (big != null) Destroy(big);
            Destroy(shot);
        }
    }

    // A photo at 1920 x 1080 (the screen's, resampled when the Game view is another size): what a 1080p player sees.
    IEnumerator Photo1080(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        Texture2D save = shot;
        if (shot.width != 1920 || shot.height != 1080)
        {
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);   // the screenshot is already encoded: pass its bytes through (sRGB here washed the photos out)
            Graphics.Blit(shot, target);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = target;
            save = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            save.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            save.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(target);
        }
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), save.EncodeToJPG(90));
        if (save != shot) Destroy(save);
        Destroy(shot);
    }
}

