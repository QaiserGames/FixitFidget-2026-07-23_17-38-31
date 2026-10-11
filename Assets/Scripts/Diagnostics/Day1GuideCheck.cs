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
//   3. Grace at the counter: the talk card, her name pinned over her head (E's target), her patience bar 56 x 7 px over
//      her, coloured by her patience; the card settles to one line after ~6 s and comes back in full after ~10 s more;
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
        yield return Frames(4);
        Check(pins.FocusShowing && pins.FocusText == "Grace" && !pins.GuideShowing,
            "her name is pinned over her head (what E talks to), one pin: the guide's own stands aside for the name");
        Rect name = pins.FocusOnScreen;
        Vector3 head = Camera.main.WorldToScreenPoint(Barks.HeadPointOf(grace.transform));
        Check(name.yMin >= head.y - 1f && Mathf.Abs(name.center.x - head.x) < name.width, $"...above her head ({name.yMin:0} px up; her head point {head.y:0})");
        float sf = PinsScale();
        if (pins.TryGetBar(grace.transform, out Rect bar, out Color colour, out bool dot))
        {
            Check(Mathf.Abs(bar.width - UiPins.BarWidth * sf) <= 1.5f && Mathf.Abs(bar.height - UiPins.BarHeight * sf) <= 1f,
                $"her patience bar is {bar.width / sf:0} x {bar.height / sf:0} px at 1080p ({bar.width:0} x {bar.height:0} on this screen; the rule is 56 x 7)");
            Color want = UiPins.ColourAt(grace.PatienceFraction);
            Check(Mathf.Abs(colour.r - want.r) + Mathf.Abs(colour.g - want.g) + Mathf.Abs(colour.b - want.b) < .08f,
                $"...coloured by her patience ({grace.PatienceFraction:0.00})");
            Check(bar.yMax <= name.yMin + .5f, "...and under her name, never on it");
            Check(!dot, "no job colour beside it yet (nothing taken)");
        }
        else Check(false, "her patience bar is drawn over her (UiPins)");
        yield return Photo1080("02-grace-at-the-counter-name-and-bar");

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
        if (pins.TryGetBar(grace.transform, out _, out _, out bool tagged))
            Check(tagged, "her bar has her job's colour beside it now (the ticket's tape, the device's tag)");
        yield return Photo1080("05-the-intake-tray");

        // ---------- 6. picked up ----------
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(3);
        ItemInteractable item = camera.GetComponentInChildren<ItemInteractable>();
        Check(item != null, "the camera can be picked up");
        if (item == null) yield break;
        item.Interact(interactor);
        yield return Until(() => carry.Contains(camera), 3f, "Ace picks the camera up (as E on it does)");
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
        yield return Frames(4);
        Check(pins.FocusShowing || pins.GuideShowing, $"a pin over it says what it is (\"{(pins.FocusShowing ? pins.FocusText : pins.GuideText)}\")");
        yield return Photo1080("07-on-the-bench");

        // ---------- 8. at the bench ----------
        Check(interactor.WorkOn(camera.GetComponentInChildren<ItemInteractable>()), "E on it at the bench brings it into the close-up");
        yield return Until(() => inspector.IsHoldingItem, 3f, "...the close-up is up");
        yield return Seconds(1.6f);
        Rect card = guide.CardOnScreen;
        float hud = HudScale();
        Check(guide.CardShowing && guide.CardAtBench, "at the bench the card sits near the work, over the bottom prompt (not in the top left)");
        Check(Mathf.Abs(card.center.x - Screen.width * .5f) < 2f && card.yMin >= 150f * hud && card.yMax <= Screen.height * .45f,
            $"...centred, from {card.yMin / hud:0} to {card.yMax / hud:0} px up at 1080p (the prompt's top is about 142)");
        Check(pins.AllHidden, "no pins over the close-up");
        JobMarker[] markers = FindObjectsByType<JobMarker>(FindObjectsInactive.Exclude);
        Check(markers.All(m => m.LabelText.Length == 0), $"no job number anywhere ({markers.Length} job tags up in the café)");
        Check(!markers.Any(m => m.TagShowing && m.GetComponentInParent<JobBase>(true) == inspector.FocusedItem), "no tag over the device on the mat");
        Check(shop.PromptLine.Contains("[Click a tool]") && shop.PromptLine.Contains("[Drag]") && shop.PromptLine.Contains("[Right-click]"),
            $"the mouse's tools line, with its real bindings: \"{Plain(shop.PromptLine)}\"");
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
            yield return Photo1080("09-getting-impatient");
            grace.Reassure();   // what E beside her does (CustomerInteractable)
            yield return Until(() => DayOneGuideUI.ReassuredOnce && !guide.ImpatienceShowing, 3f, "reassured: the card goes, and won't come back this session");
        }
        if (grace != null) left.SetValue(grace, was);   // as she was, so the rest isn't about her leaving
    }

    // The Officer comes in at about a third of Day 1 for a coffee (the game's first drink). The lab moves the clock to his
    // time if it isn't there yet and makes room (walk-ins sent home), then waits a while for him.
    IEnumerator TheOfficer(CustomerBrain grace, Transform till)
    {
        DayClock clock = DayClock.Instance;
        if (clock != null && clock.NormalizedDay < .36f)
        {
            PropertyInfo remaining = typeof(DayClock).GetProperty("TimeRemaining", BindingFlags.Instance | BindingFlags.Public);
            float length = (float)(typeof(DayClock).GetField("dayLengthSeconds", Any)?.GetValue(clock) ?? 0f);
            if (remaining != null && length > 0f)
            {
                remaining.GetSetMethod(true)?.Invoke(clock, new object[] { length * .64f });
                Note($"the clock moved on to the Officer's time ({ShopUI.FormatHour(clock.CurrentHour)})");
            }
        }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            if (c != null && c != grace && (c.Identity == null || c.Identity.Profile == null))
            {
                Note($"a walk-in ({c.CustomerName}) was sent home to make room");
                c.ForceRemove();
            }
        CustomerBrain officer = null;
        yield return Await(() => (officer = FindRegular(OfficerStory.ProfileId)) != null && officer.CanHearIntake, 90f);
        if (!lastWait || officer == null)
        {
            Note("the Officer didn't reach the counter within the check's time: his cards aren't checked this run");
            yield break;
        }
        PutAce(till.position, till.eulerAngles.y);
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

