using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// AIM HELP ON A PAD: A PLAY CHECK (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.3 and §2.5)
//
// Fixit Fidget > Playtest > Aim help - play check (lab, drives itself). Mansoor: "very hard to get the controller
// cursor onto an item". A fresh Day 1 in the lab, a virtual gamepad, and in first person:
//
//   1. at the dispenser: the view turned a few degrees off a cup pad (three ways: left, right, below); the right stick
//      pushed toward it, the way a thumb does, until the crosshair is near; let go: the crosshair settles on the pad
//      within half a second, and it's what A would use. Over the pad the view turns slower than in the open (friction);
//      D-pad right steps to the next control to the right;
//   2. behind the counter, a customer waiting: the same from three offsets;
//   3. at the bench, their device in the close-up: the pad's cursor let go 30 to 50 px from a screw settles on it, and
//      D-pad right steps through the parts in the order the work goes (screws first).
//
// THE FORGIVING BENCH (14 Oct 2026; systems-forgiving-bench.md §4.6, §6): the points the cursor settles on are the bench's own
// (the picker's eligible targets and the tools: ItemInspector.AimPoints), not this check's guess at them; over a target of the
// step to do now the cursor moves at no more than 0.6 of its open speed (friction); and D-pad right reaches a target of the
// step even when something hides it from the camera (a block the check puts in the way).
//
// The mouse is never helped, and with aim help switched off nothing settles (checked last). Photos and report.txt go
// to Logs/AimHelp/aim-help-<time>/. Nothing is saved in the scene.
//
// SEEN PARTS ONLY (12-13 Oct, the simplicity pass): the help offers only parts the camera can see (ItemInspector.Seen): a
// phone arrives face up with its screws in its back, and the D-pad used to send the cursor to them, under the glass. So
// this check counts seen parts as the game does, and turns the device over (the lab's hand, as Y would) when no screw is
// in sight, before the screw and the D-pad beats.
//
// HELP THAT DOESN'T FIGHT (23 Oct 2026). His note: "if I grab a cup and like someone ordered an espresso, I grab the cup and I
// go to the espresso machine, I'm like locked to the cup. It's like pushing me. It's like hard for me to even look to press the
// nozzle". The help now fades as the stick is pushed harder, draws only toward what's ahead, never slows or settles back onto
// what the crosshair is leaving, is lighter where targets crowd, and has two strengths (AimAssist). The check runs at Light, the
// default, and at Strong where it says so; the player's own setting (switch and strength) is put back at the end:
//   1. at the dispenser, first person: the friction check of 7 Oct pushed off the pad's middle and wanted the turn slower, which
//      is the drag he felt, so now: a nudge onto the pad turns slower while on it (at Light and at Strong, the pad's neighbours
//      crowding it); a nudge up off its middle doesn't, and the pad never slows it; and a full push turns at the speed it turns
//      with no help, each against the same push with the help off;
//   1b. the drinks close-up from above, a cup in each hand (Drinks): a thumb from the cup stack to Espresso's place, A sets a
//      cup down there, the thumb from that cup up to Espresso's paddle (again at Strong): each lands, its push no slower than the
//      same thumb's with no help, the view never moving against the stick nor bending 15° off it, nothing in his hands ever a target,
//      pulled to, slowed by, settled on or picked, nothing gliding back to what it left; two nudges up off the cup let go short
//      of the paddle never glide back to the cup; a full push across the nozzles turns at the no-help speed; a small nudge that
//      stops 2.5° off the paddle still settles on it from below, the left and the right, and one that went just past it settles
//      back (her case: the help she found useful on 19 Oct). The words on the drinks machine are never read here (they change
//      on 23 Oct 2026).
// ---------------------------------------------------------------------------
public sealed class AimHelpCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.AimHelp.Check";

    protected override string Title => "Aim help on a pad - play check";
    protected override string Tag => "[Aim help check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Aim help check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Aim help check (this Play session only)");
        go.AddComponent<AimHelpCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AimHelp",
            $"aim-help-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    PlayerCarry carry;
    ItemInspector inspector;
    ConversationController conversation;
    CustomerSpawner spawner;
    bool aimWas, aimTaken, spawnerWas = true;
    AimHelpLevel strengthWas = AimHelpLevel.Light;
    readonly List<AimAssist.Target> scratch = new List<AimAssist.Target>(32);

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        carry = movement != null ? movement.GetComponent<PlayerCarry>() : null;
        inspector = movement != null ? movement.GetComponent<ItemInspector>() : null;
        conversation = movement != null ? movement.GetComponent<ConversationController>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null && spawner != null,
            "Ace and the café's spawner are in the scene");
        if (interactor == null || view == null || spawner == null || interactor.DrinksStation == null || interactor.CounterStation == null) yield break;

        yield return Await(() => typeof(CustomerSpawner).GetField("openingDrink", Any).GetValue(spawner) != null, 10f);
        spawnerWas = spawner.enabled;
        spawner.enabled = false;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        aimWas = AimAssist.Enabled;
        strengthWas = AimAssist.Strength;
        aimTaken = true;   // from here the player's setting is put back whatever happens (Restore, and OnDisable if Play stops)
        AimAssist.Enabled = true;
        AimAssist.Strength = AimHelpLevel.Light;   // 23 Oct 2026: the default, what a player has; Strong where a check says so
        PlugInPad("Aim help check pad");
        // A touch of the pad: the game now takes it as the one in hand (the help is for a pad only).
        yield return PadPress(GamepadButton.LeftShoulder);
        yield return Frames(3);
        Check(PadInput.UsingPad && AimAssist.Active, "the pad is the one in hand and aim help is on");
        view.SetFirstPerson(true);
        yield return AfterBlend();

        // ---------- 1. the dispenser ----------
        StationInteractable drinks = interactor.DrinksStation;
        BeverageControl pad1 = drinks.GetComponentsInChildren<BeverageControl>()
            .Where(c => !c.dispenseButton).OrderBy(c => Mathf.Abs(c.transform.GetComponentInChildren<Collider>().bounds.center.x - drinks.transform.position.x)).FirstOrDefault();
        Check(pad1 != null, "the dispenser has cup pads");
        if (pad1 == null) yield break;
        PutAce(drinks.StandPoint.position, drinks.StandPoint.eulerAngles.y);
        yield return Frames(4);
        Vector3 padPoint = pad1.GetComponentInChildren<Collider>().bounds.center;
        yield return Settles(pad1, padPoint, new Vector2(-5f, 0f), "a cup pad, from 5° to its left");
        yield return Settles(pad1, padPoint, new Vector2(6f, 0f), "a cup pad, from 6° to its right");
        yield return Settles(pad1, padPoint, new Vector2(0f, 5f), "a cup pad, from 5° below");
        yield return Photo("01-settled-on-a-cup-pad");
        yield return Friction(pad1, padPoint);
        yield return FullPushFirstPerson(padPoint);

        // D-pad right: the next control to the right.
        Look(padPoint, Vector2.zero);
        yield return Frames(4);
        Vector3 right = Camera.main.transform.right;
        Interactable was = interactor.Focused;
        yield return PadPress(GamepadButton.DpadRight);
        yield return Seconds(.4f);
        Interactable now = interactor.Focused;
        bool stepped = now != null && now != was && PlayerInteractor.IsDrinksThing(now)
                       && Vector3.Dot(Middle(now) - padPoint, right) > 0f;
        Check(stepped, $"D-pad right steps to the next control to the right ({Name(was)} to {Name(now)})");
        yield return Photo("02-dpad-stepped");

        // ---------- 1b. the drinks close-up, a cup in hand (23 Oct 2026) ----------
        yield return Drinks(drinks);
        if (interactor.IsAtStation) interactor.ExitStation();
        // Had the drinks part stopped early, whatever it left in Ace's hands goes back (the bench part needs a free hand).
        carry.UseAutomaticHand();
        for (int i = 0; i < 2 && carry.Carried is DrinkJob; i++)
        {
            if (((DrinkJob)carry.Carried).IsEmpty && ShopInventory.Instance != null) ShopInventory.Instance.ReturnCup();
            carry.Consume();
        }
        yield return AfterBlend();
        view.SetFirstPerson(true);
        yield return AfterBlend();
        Check(view.FirstPersonSelected && carry.Count == 0, $"back in first person, nothing in hand ({(carry.Count == 0 ? "empty" : carry.Summary)})");

        // ---------- 2. a customer at the counter ----------
        DayOneOpening opening = (DayOneOpening)typeof(CustomerSpawner).GetField("opening", Any).GetValue(spawner);
        opening.Reset(true);
        opening.TryStartVisit();
        opening.FinishVisit();          // the lesson's second visit: a repair (their device comes in useful at the bench)
        CustomerBrain who = null;
        for (int attempt = 0; attempt < 8 && who == null; attempt++)
        {
            if (attempt > 0) { opening.Reset(true); opening.TryStartVisit(); opening.FinishVisit(); }
            typeof(CustomerSpawner).GetMethod("Spawn", Any).Invoke(spawner, null);
            CustomerBrain c = spawner.OpeningCustomer;
            if (c == null) break;
            if (c.Record != null && c.Record.kind == JobKind.Repair && !c.IsCounterRepair
                && (c.Record.faultType == FaultType.Mechanical || c.Record.faultType == FaultType.Cleaning)) who = c;
            else { Destroy(c.gameObject); opening.FinishVisit(); yield return Frames(2); }
        }
        Check(who != null, $"a customer with a device for the bench comes in ({(who != null ? who.CustomerName : "none")})");
        if (who == null) yield break;
        yield return Until(() => who == null || who.CanHearIntake, 90f, "they reach the counter");
        if (!lastWait || who == null) yield break;
        Transform till = interactor.CounterStation.StandPoint;
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(4);
        Vector3 chest = who.LookTarget != null ? who.LookTarget.position - Vector3.up * .2f : who.transform.position + Vector3.up * 1.4f;
        CustomerInteractable talk = who.GetComponent<CustomerInteractable>();
        yield return Settles(talk, chest, new Vector2(-6f, 0f), "a customer at the counter, from 6° to their left");
        yield return Settles(talk, chest, new Vector2(7f, 0f), "a customer at the counter, from 7° to their right");
        yield return Settles(talk, chest, new Vector2(0f, -5f), "a customer at the counter, from 5° above");
        yield return Photo("03-settled-on-a-customer");

        // ---------- 3. the bench ----------
        // Their request taken (by the check's hand: this check is about aiming), their device to the bench.
        conversation.Begin(who);
        for (int i = 0; i < 12 && conversation.InConversation && who != null && !who.WasAccepted; i++)
        {
            conversation.Advance();
            yield return Seconds(.35f);
        }
        yield return Await(() => !conversation.InConversation, 6f);
        if (conversation.InConversation) conversation.Advance();
        yield return Await(() => !conversation.InConversation, 2f);
        if (conversation.InConversation) conversation.End();
        yield return Until(() => who != null && who.ActiveJob != null, 4f, "their device comes in");
        JobBase job = who != null ? who.ActiveJob : null;
        StationInteractable bench = StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);
        if (job == null || bench == null) yield break;
        carry.TryPickUp(job);
        bench.Interact(interactor);
        Check(StationInteractable.BenchHolds(job), "it's on the bench");
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Frames(4);
        Check(interactor.WorkOn(job.GetComponentInChildren<ItemInteractable>()), "the close-up opens on it");
        yield return AfterBlend();
        yield return Frames(6);
        Check(PadCursor.IsActive, "the pad's cursor is on screen");
        Camera cam = Camera.main;
        // The screws are in the device's back (the phone's are): with none in sight, over it goes, as Y would turn it.
        ScrewTarget[] allScrews = job.GetComponentsInChildren<ScrewTarget>();
        if (allScrews.Length > 0 && !allScrews.Any(s => On(cam, s.transform) && Seen(cam, s.transform)))
        {
            Note("no screw is in sight on the face that's up (they're in the back): turning it over, as Y would");
            inspector.FlipOver();
            yield return Await(() => !inspector.Flipping, 2f);
            yield return Seconds(.3f);
        }
        ScrewTarget screw = allScrews.FirstOrDefault(s => On(cam, s.transform) && Seen(cam, s.transform));
        Check(screw != null, "a screw is in view, in the camera's sight");
        if (screw != null)
        {
            Vector2 at = cam.WorldToScreenPoint(Centre(screw.transform));
            float scale = Mathf.Max(.5f, Screen.height / 1080f);
            // The bench's own points (the picker's), the screw's own aside.
            yield return Frames(2);
            List<Vector2> others = inspector.AimPoints.Where(p => (p - at).sqrMagnitude > 16f * Mathf.Max(.25f, scale * scale)).ToList();
            if (others.Count == 0) others = SnapPoints(job, cam).Where(p => (p - at).sqrMagnitude > 4f).ToList();
            yield return CursorSettles(at, others, 30f * scale, "30 px");
            yield return CursorSettles(at, others, 45f * scale, "45 px");
            yield return CursorSettles(at, others, 50f * scale, "50 px");
            yield return Photo("04-cursor-on-a-screw");
            yield return FrictionOver(screw, cam, scale);
            yield return HiddenStepTarget(job, screw, cam, scale);
        }

        // D-pad right through the parts, in the order the work goes: from nowhere in particular, the first of the work
        // (a screw); then each of them.
        yield return DpadRun(job, cam, "with the screws in");
        // The screws out (the check's hand): the cover comes first now, and the screws (to go back in) after it.
        foreach (ScrewTarget t in job.GetComponentsInChildren<ScrewTarget>())
            if (!t.GetComponent<Screw>().IsOut && t.CanInteract) { t.Activate(); yield return Seconds(.6f); }
        yield return Seconds(.6f);
        yield return DpadRun(job, cam, "with the screws out");
        yield return Photo("05-dpad-on-the-parts");

        // Off: nothing settles.
        AimAssist.Enabled = false;
        if (screw != null)
        {
            ScrewTarget any = job.GetComponentsInChildren<ScrewTarget>(true).FirstOrDefault();
            Vector2 at = any != null ? (Vector2)cam.WorldToScreenPoint(Centre(any.transform)) : new Vector2(Screen.width * .5f, Screen.height * .5f);
            PadCursor.GlideTo(at + new Vector2(40f, 0f) * Mathf.Max(.5f, Screen.height / 1080f));
            yield return Seconds(.35f);
            PadHold(new GamepadState { rightStick = new Vector2(-.3f, 0f) });
            yield return Frames(2);
            PadRelease();
            yield return Seconds(.4f);
            Check(!PadCursor.Gliding && (PadCursor.Position - at).magnitude > 15f, $"with aim help off, the cursor stays where it was let go ({(PadCursor.Position - at).magnitude:0} px from the screw)");
        }
        AimAssist.Enabled = true;
        inspector.CancelInspection();
        yield return AfterBlend();
    }

    // D-pad right from a corner of the screen, once for each stop of the work and once more: where the cursor lands each time.
    // The stops are ItemInspector's (GatherWork): every part that work can be done on now and is in the camera's sight, and
    // every grime spot it can reach (nothing between the camera and it), which comes at 2 in the work's order. Playtest 4:
    // the check counted parts only, so on a cleaning job (a watch with debris inside) the grime, a stop by design, read as
    // "on nothing" (-1). 12 Oct: parts out of sight (the screws under a phone lying face up) are no longer stops.
    IEnumerator DpadRun(JobBase job, Camera cam, string when)
    {
        PadCursor.GlideTo(new Vector2(Screen.width * .08f, Screen.height * .9f));
        yield return Seconds(.4f);
        List<GrimeSpot> grime = ReachableGrime(job, cam);
        int points = AllParts(job).Count(p => p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform) && Seen(cam, p.transform)) + grime.Count;
        var orders = new List<int>();
        for (int i = 0; i < Mathf.Max(2, points + 1); i++)
        {
            yield return PadPress(GamepadButton.DpadRight);
            yield return Seconds(.3f);
            orders.Add(StopUnder(job, cam, grime, PadCursor.Position));
        }
        int first = AllParts(job).Where(p => p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform) && Seen(cam, p.transform))
            .Select(ItemInspector.WorkOrder).Concat(grime.Select(_ => 2)).DefaultIfEmpty(-1).Min();
        if (grime.Count > 0) Note($"{when}: {grime.Count} grime spot(s) the D-pad steps to as well (the work's place 2)");
        bool ordered = true;
        for (int i = 1; i < Mathf.Min(orders.Count, points); i++) ordered &= orders[i] >= orders[i - 1];
        Check(orders.Count > 0 && orders[0] == first && ordered && !orders.Contains(-1),
            $"{when}: D-pad right lands on a part each time, the first of the work first, in the work's order (places: {string.Join(", ", orders)}; first {first})");
    }

    static List<BenchInteractable> AllParts(JobBase job)
    {
        var all = new List<BenchInteractable>(job.GetComponentsInChildren<BenchInteractable>());
        foreach (GameObject loose in job.LooseParts) if (loose != null) all.AddRange(loose.GetComponentsInChildren<BenchInteractable>());
        return all;
    }

    // What the cursor may settle on, on screen and in the camera's sight: the parts that can be worked on, the grime, the tools.
    static IEnumerable<Vector2> SnapPoints(JobBase job, Camera cam)
    {
        foreach (BenchInteractable p in AllParts(job))
            if (p.isActiveAndEnabled && p.CanInteract && On(cam, p.transform) && Seen(cam, p.transform)) yield return cam.WorldToScreenPoint(Centre(p.transform));
        foreach (GrimeSpot g in job.GetComponentsInChildren<GrimeSpot>())
            if (On(cam, g.transform) && Seen(cam, g.transform)) yield return cam.WorldToScreenPoint(Centre(g.transform));
        foreach (ToolPickup t in FindObjectsByType<ToolPickup>(FindObjectsInactive.Exclude))
            if (On(cam, t.transform)) yield return cam.WorldToScreenPoint(Centre(t.transform));
    }

    // A thing is in the camera's sight, as ItemInspector.Seen reckons it: nothing solid between the camera and its middle but
    // the thing itself (the tool in the hand aside). A screw in the back of a phone lying face up is not.
    static bool Seen(Camera cam, Transform thing)
    {
        Vector3 from = cam.transform.position;
        Vector3 to = Centre(thing) - from;
        float length = to.magnitude;
        if (length < 1e-4f) return true;
        RaycastHit[] hits = Physics.RaycastAll(from, to / length, length + .02f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Transform first = null;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.collider.GetComponentInParent<ToolPickup>() != null) continue;
            if (h.distance < nearest) { nearest = h.distance; first = h.collider.transform; }
        }
        return first == null || first.IsChildOf(thing);
    }

    protected override void Restore()
    {
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        PutAimHelpBack();
        if (spawner != null) spawner.enabled = spawnerWas;
    }

    // THE PLAYER'S SETTING, ALWAYS (19 Oct 2026). The check turns the help off for one case and on again after; stopping
    // Play between the two (15 Oct, on his Mac) left "off" saved for every player on that machine, and his girlfriend's
    // playtest ran without the help until he found the switch. Restore runs only when the check finishes, so the setting is
    // put back on OnDisable too (Play stopping destroys the check: OnDisable runs), and GameSettings puts it on once more
    // on the next launch (AimHelpResetKey). Since 23 Oct 2026 its strength too (the check runs at Light, and at Strong in places).
    void PutAimHelpBack()
    {
        if (!aimTaken) return;
        aimTaken = false;
        if (AimAssist.Enabled != aimWas) AimAssist.Enabled = aimWas;
        if (AimAssist.Strength != strengthWas) AimAssist.Strength = strengthWas;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        PutAimHelpBack();
    }

    // ---------- first person: the stick toward a target, let go, settled ----------

    IEnumerator Settles(Interactable thing, Vector3 point, Vector2 offset, string what)
    {
        Look(point, offset);
        yield return Frames(4);
        // Toward it: as a thumb does, until the crosshair is near (or a second and a half).
        Vector2 toward = new Vector2(-offset.x, offset.y).normalized;   // yaw left offset: push right; pitch below (+): push up
        float until = Time.realtimeSinceStartup + 1.5f;
        PadHold(new GamepadState { rightStick = toward * .45f });
        while (Time.realtimeSinceStartup < until && AngleTo(point) > 3f) yield return null;
        float near = AngleTo(point);
        PadRelease();
        float letGo = Time.realtimeSinceStartup;
        yield return Await(() => interactor.Focused == thing && AngleTo(point) < 1f, .5f);
        bool settled = lastWait;
        float took = Time.realtimeSinceStartup - letGo;
        Check(settled, $"{what}: let go {near:0.0}° off, it settles on it in {took:0.00} s ({AngleTo(point):0.00}° off; A would use {Name(interactor.Focused)})");
    }

    // ---------- first person: friction, and a full push ----------

    // FRICTION, 23 OCT 2026 (his note: "locked to the cup ... hard for me to even look to press the nozzle"). The 7 Oct check
    // pushed firmly (0.6) off the pad's middle and wanted the turn under 0.75 of the open's: that is the drag he felt, so by
    // design it no longer holds (nothing slows the view off what it leaves, and a firmer push gets less help). Now, with a nudge
    // (0.45: 26% of the stick's travel past its dead zone, the whole help), its speed taken frame by frame (each frame's turn
    // over that frame's own time, as the view takes it, so it holds at 15 fps as at 60), against the same nudge from the same
    // place with the help off:
    //   * from beside the pad (its left neighbour) onto it, on the frames the crosshair is on it: under 0.95 of the speed with
    //     no help at Light, under 0.85 at Strong (by design, the pad crowded by its neighbours: 0.85 and 0.7; a model of the
    //     dispenser gives 0.88 and 0.77, the pull's share added as it draws the view onto the middle);
    //   * up off its middle (toward the paddles, nothing near enough to slow it), on the frames it is still on it: at least 0.97
    //     of the speed with no help, and the pad slows it on no frame.
    const float Nudge = .45f;

    IEnumerator Friction(Interactable pad, Vector3 point)
    {
        float size = HalfSize(pad, point);
        Func<bool> onIt = () => AngleTo(point) <= size;
        Vector2 across = new Vector2(Nudge, 0f);
        // The same nudge with no help.
        Look(point, new Vector2(-(size + 4f), 0f));
        yield return Frames(4);
        AimAssist.Enabled = false;
        var bare = new Rate();
        yield return PushRate(across, 1.4f, onIt, null, bare);
        AimAssist.Enabled = true;
        foreach (AimHelpLevel level in new[] { AimHelpLevel.Light, AimHelpLevel.Strong })
        {
            AimAssist.Strength = level;
            // From beside it (its half-size and 4° more, to its left: off it and its fade), a nudge right across it.
            Look(point, new Vector2(-(size + 4f), 0f));
            yield return Frames(4);
            var onto = new Rate();
            yield return PushRate(across, 1.4f, onIt, null, onto);
            float bar = level == AimHelpLevel.Light ? .95f : .85f;
            Check(bare.rate > .01f && onto.frames > 0 && onto.rate < bare.rate * bar,
                $"{level}: a nudge onto a cup pad turns slower while on it ({onto.rate:0.0}°/s over {onto.frames} frames against {bare.rate:0.0}°/s with no help: " +
                $"{(bare.rate > 0f ? onto.rate / bare.rate : 0f):0.00}x; under {bar:0.00})");
        }
        AimAssist.Strength = AimHelpLevel.Light;
        // Up off its middle: never slower, and the pad never slows it.
        Vector2 up = new Vector2(0f, Nudge * (Get<bool>(view, "invertPadLookY") ? -1f : 1f) * GameSettings.YSign);
        Look(point, Vector2.zero);
        yield return Frames(4);
        AimAssist.Enabled = false;
        var bareUp = new Rate();
        yield return PushRate(up, .3f, onIt, null, bareUp);
        AimAssist.Enabled = true;
        Look(point, Vector2.zero);
        yield return Frames(4);
        var off = new Rate();
        yield return PushRate(up, .3f, onIt, pad, off);
        Check(bareUp.rate > .01f && off.frames > 0 && off.rate >= bareUp.rate * .97f && off.slowedBy == 0,
            $"a nudge up off the pad's middle is never slowed ({off.rate:0.0}°/s over {off.frames} frames on it against {bareUp.rate:0.0}°/s with no help: " +
            $"{(bareUp.rate > 0f ? off.rate / bareUp.rate : 0f):0.00}x, 0.97 at least; slowed by the pad on {off.slowedBy} frames)");
        yield return Photo("01b-a-nudge-up-off-the-pad");
    }

    // STRONG PUSHES ARE FREE (23 Oct 2026): pushed all the way, the help is nothing (AimAssist.Intent), so the view turns at its
    // full speed across the dispenser's controls, as with no help: the same full push from the pad's middle with the help on
    // (Light) and off, frame by frame; and the help's own share on every frame of it, 0. Short, so it stays near the machine.
    IEnumerator FullPushFirstPerson(Vector3 point)
    {
        float seconds = Mathf.Min(.25f, 40f / Mathf.Max(1f, Get<float>(view, "padLookYawSpeed") * GameSettings.PadLookScale));
        Look(point, Vector2.zero);
        yield return Frames(4);
        var helped = new Rate();
        yield return PushRate(new Vector2(1f, 0f), seconds, null, null, helped);
        AimAssist.Enabled = false;
        Look(point, Vector2.zero);
        yield return Frames(4);
        var bare = new Rate();
        yield return PushRate(new Vector2(1f, 0f), seconds, null, null, bare);
        AimAssist.Enabled = true;
        Check(bare.rate > .01f && helped.rate >= bare.rate * .98f && helped.helpedFrames == 0,
            $"a full push turns at full speed across the dispenser ({helped.rate:0}°/s with the help, {bare.rate:0}°/s without, over {helped.frames} frames; " +
            $"the help's share above 0 on {helped.helpedFrames} of them)");
    }

    // A push held (the stick as the virtual pad sends it); the view's speed, degrees a second, frame by frame (each frame's turn,
    // in heading and tilt, over the frame's own time, clamped as the views clamp it), on the frames `counts` says (before and
    // after the frame; all when null). Also the frames friction came from `from` (what the push leaves) and the frames any help
    // applied at all. First person by default; the close-up passes its own look's angles and help.
    sealed class Rate
    {
        public float rate;
        public int frames, slowedBy, helpedFrames;
    }

    IEnumerator PushRate(Vector2 stick, float seconds, Func<bool> counts, UnityEngine.Object from, Rate into,
        Func<Vector2> angles = null, Func<AimAssist.State> help = null)
    {
        if (angles == null) angles = () => new Vector2(Get<float>(view, "yaw"), Get<float>(view, "pitch"));
        if (help == null) help = () => view.Aim;
        float sum = 0f;
        int n = 0, slowed = 0, helped = 0;
        PadHold(new GamepadState { rightStick = stick });
        Vector2 before = angles();
        bool countedBefore = counts == null || counts();
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
            Vector2 after = angles();
            bool countsNow = counts == null || counts();
            if (PadInput.RightStick != Vector2.zero)
            {
                if (countedBefore && countsNow)
                {
                    float turned = new Vector2(Mathf.DeltaAngle(before.x, after.x), after.y - before.y).magnitude;
                    sum += turned / Mathf.Max(1e-4f, Mathf.Min(Time.unscaledDeltaTime, .1f));
                    n++;
                }
                AimAssist.State aim = help();
                if (from != null && aim.Slowing == from) slowed++;
                if (AimAssist.Active && aim.Help > 0f) helped++;
            }
            before = after;
            countedBefore = countsNow;
        }
        PadRelease();
        yield return Frames(3);
        into.rate = n > 0 ? sum / n : 0f;
        into.frames = n;
        into.slowedBy = slowed;
        into.helpedFrames = helped;
    }

    // The help's own half-size for a thing (degrees), from where the crosshair is: what PlayerInteractor.AimTargets offers.
    float HalfSize(Interactable thing, Vector3 point)
    {
        interactor.AimTargets(scratch);
        Vector3 eye = Camera.main != null ? Camera.main.transform.position : Eye();
        foreach (AimAssist.Target t in scratch)
            if (t.thing == thing)
            {
                AimAssist.AngleTo(eye, AimAssist.Angles(eye, point), t, out float size);
                return size;
            }
        Note($"{Name(thing)} isn't among the help's targets from here: its half-size taken as 3°");
        return 3f;
    }

    // ---------- 1b. the drinks close-up, a cup in hand (23 Oct 2026) ----------

    // One push in the close-up, as Thumb measures it.
    sealed class Push
    {
        public bool landed;
        public float seconds = -1f, pushed = -1f, letGo, end;
        public int frames, against, bent, held, back;
    }

    // The thumb lets go this many degrees from the goal's middle (the 7 Oct pushes let go at 3).
    const float LetGoAt = 2.5f;

    // His note: "I grab the cup and I go to the espresso machine, I'm like locked to the cup ... hard for me to even look to
    // press the nozzle". From above, A beside the dispenser opens its close-up (as a player gets there); LB and RB take a cup in
    // each hand at the cup stack. Then: the thumb from the stack to Espresso's place under its nozzle; A sets a cup down there;
    // the thumb from that cup up to Espresso's paddle, the button that pours (and again at Strong); two nudges up off the cup
    // let go short of the paddle; full pushes across the nozzles; small nudges onto the paddle (Light and Strong). Then the
    // cups go back (the one under the nozzle into the free hand, both into the cup return), so the bench part has free hands.
    IEnumerator Drinks(StationInteractable drinks)
    {
        DispenserFrameCheck.Face face = DispenserFrameCheck.Face.Of(drinks);
        BeverageControl place = Espresso(face.places), paddle = Espresso(face.paddles);
        BeverageLook look = drinks.GetComponentInChildren<BeverageLook>();
        bool whole = place != null && place.slot != null && paddle != null && face.stack != null && face.basin != null && look != null;
        Check(whole, $"the dispenser has Espresso's place and paddle ({Name(place)}, {Name(paddle)}), the cup stack, the cup return and the close-up's look");
        if (!whole) yield break;

        view.SetFirstPerson(false);
        yield return AfterBlend();
        PutAce(drinks.StandPoint.position, drinks.StandPoint.eulerAngles.y);
        yield return Frames(4);
        yield return PadPress(GamepadButton.South);
        if (interactor.CurrentStation != drinks)
        {
            Note("A beside the dispenser, from above, didn't open its close-up: the check's hand opens it (the Stations lab checks the A)");
            interactor.EnterStation(drinks);
        }
        yield return AfterBlend();
        yield return Frames(3);
        Check(interactor.CurrentStation == drinks && AimAssist.Active, "from above, A beside the dispenser opens its close-up; the pad in hand, the help on (Light)");
        if (interactor.CurrentStation != drinks) yield break;

        Vector3 stackPoint = Centre(face.stack.transform);
        yield return Glide(look, stackPoint);
        yield return PadPress(GamepadButton.LeftShoulder);
        yield return Frames(3);
        yield return PadPress(GamepadButton.RightShoulder);
        yield return Frames(3);
        bool both = carry.GetHandItem(0) is DrinkJob && carry.GetHandItem(1) is DrinkJob;
        Check(both, $"at the cup stack LB and RB take a cup in each hand (in hand: {carry.Summary})");
        if (!both) yield break;
        yield return Photo("06-close-up-a-cup-in-each-hand");

        // From the cup stack to Espresso's place, under its nozzle; A sets a cup down there.
        yield return Leg(look, face.stack, stackPoint, place, "a cup in each hand, from the cup stack to Espresso's place under its nozzle",
            "07-landed-on-espresso-place", true);
        yield return Glide(look, Centre(place.transform));
        yield return PadPress(GamepadButton.South);
        yield return Frames(3);
        DrinkJob under = place.slot.Cup;
        ItemInteractable cup = under != null ? under.GetComponentInChildren<ItemInteractable>() : null;
        bool placed = cup != null && carry.Count == 1 && carry.Carried is DrinkJob;
        Check(placed, $"A sets a cup down under Espresso's nozzle, the other still in hand (under it: {(under != null ? under.name : "nothing")}; in hand: {carry.Summary})");
        if (!placed) yield break;
        yield return Photo("08-a-cup-under-espresso");

        // From that cup up to Espresso's paddle: at Light (against the same thumb with no help), then at Strong.
        Vector3 cupPoint = Centre(cup.transform);
        yield return Leg(look, cup, cupPoint, paddle, "a cup in hand, from the cup under the nozzle up to Espresso's paddle",
            "09-landed-on-espresso-paddle", true);
        AimAssist.Strength = AimHelpLevel.Strong;
        yield return Leg(look, cup, cupPoint, paddle, "a cup in hand, from the cup under the nozzle up to Espresso's paddle", null, false);
        AimAssist.Strength = AimHelpLevel.Light;

        // Nudges up off the cup, let go short of the paddle: nothing glides back to the cup.
        yield return LetGoShort(look, cup, cupPoint, paddle, 4f);
        yield return LetGoShort(look, cup, cupPoint, paddle, 9f);
        yield return Photo("10-let-go-short-of-the-paddle");

        // Strong pushes are free.
        yield return FullPushCloseUp(look, stackPoint);
        yield return Photo("11-after-a-full-push-across-the-nozzles");

        // Help still helps: her case.
        foreach (AimHelpLevel level in new[] { AimHelpLevel.Light, AimHelpLevel.Strong })
        {
            AimAssist.Strength = level;
            yield return SmallNudge(look, paddle, new Vector2(0f, 1f), $"{level}: from below");
            yield return SmallNudge(look, paddle, new Vector2(-1f, 0f), $"{level}: from the left");
            yield return SmallNudge(look, paddle, new Vector2(1f, 0f), $"{level}: from the right");
            yield return JustPast(look, paddle, level);
        }
        AimAssist.Strength = AimHelpLevel.Light;
        yield return Photo("12-a-small-nudge-settled-on-the-paddle");

        // The cups go back: the one under the nozzle into the free hand (A), then both into the cup return (A, A).
        yield return Glide(look, Centre(place.transform));
        yield return PadPress(GamepadButton.South);
        yield return Frames(3);
        yield return Glide(look, Centre(face.basin.transform));
        for (int i = 0; i < 2 && carry.Count > 0; i++)
        {
            yield return PadPress(GamepadButton.South);
            yield return Frames(3);
        }
        Check(carry.Count == 0 && place.slot.Cup == null,
            $"the cups go back: the one under the nozzle taken up, both into the cup return (in hand: {(carry.Count == 0 ? "nothing" : carry.Summary)})");
    }

    // Espresso's control in a row of the face (its places or its paddles, left to right): by the drink's name, or the fourth from
    // the left, as the installer lays the six out (BeverageStationSetup: Coffee, Tea, Hot chocolate, Espresso, Americano, Latte).
    static BeverageControl Espresso(List<(string name, Interactable thing)> row)
    {
        foreach (var entry in row)
            if (entry.thing is BeverageControl c && c.slot != null && c.slot.drink != null
                && string.Equals(c.slot.drink.drinkName, "Espresso", StringComparison.OrdinalIgnoreCase)) return c;
        return row.Count == 6 ? row[3].thing as BeverageControl : null;
    }

    // One push: the thumb with the help, then (bareToo) the same thumb from the same place with the help off, for the time it
    // takes with none. It lands (on it and settled, within Thumb's 0.8 s of letting go), and its push, from the stick's first
    // touch to letting go, is no slower than with no help but for 30% and 0.15 s. The settle is left out of that sum: it takes
    // about four frames whatever the look's speed (0.27 s at 15 fps), so at pad look x2 it was most of the help's time and a
    // bar on the whole landing passed by 0.07 s. Outside Unity at 60 fps, from the stack to the nozzle area: the 7 Oct help landed
    // in 2.47 s, the same thumb with no help let go after 1.10 s, and the help now lets go after 1.15 s and lands in 1.35 s. And
    // on every frame the view never moved against the stick, nor more than 15° off the stick's own way (the bend he felt as
    // "pushing me": up to 32° on that leg under the 7 Oct help, under 4° now), nothing in Ace's hands was a target, pulled to,
    // slowed by, settled on or picked, and after letting go nothing went back toward what the push left.
    IEnumerator Leg(BeverageLook look, Interactable from, Vector3 fromPoint, Interactable goal, string what, string photo, bool bareToo)
    {
        string level = AimAssist.Strength.ToString();
        yield return Glide(look, fromPoint);
        var helped = new Push();
        yield return Thumb(look, goal, from, fromPoint, helped);
        if (photo != null) yield return Photo(photo);
        var bare = new Push();
        if (bareToo)
        {
            yield return Glide(look, fromPoint);
            AimAssist.Enabled = false;
            yield return Thumb(look, goal, from, fromPoint, bare);
            AimAssist.Enabled = true;
        }
        bool inTime = helped.landed && (!bareToo || bare.pushed < 0f || helped.pushed <= bare.pushed * 1.3f + .15f);
        Check(inTime, $"{level}, {what}: lands on it in {Took(helped)}, let go after {helped.pushed:0.00} s" +
                      (bareToo ? $" (the same thumb with no help let go after {bare.pushed:0.00} s)" : "") +
                      $"; let go {helped.letGo:0.0}° from its middle, {helped.end:0.00}° after the settle");
        Check(helped.frames > 0 && helped.against == 0 && helped.bent == 0 && helped.held == 0 && helped.back == 0,
            $"{level}, {what}: of {helped.frames} frames pushed, the view moved against the stick on {helped.against} and bent more than 15° off it " +
            $"on {helped.bent}; something in Ace's hands was a target, pulled to, slowed by, settled on or picked on {helped.held}; after letting go " +
            $"it went back toward what it left on {helped.back}");
    }

    static string Took(Push p) => p.landed ? $"{p.seconds:0.00} s" : "never";

    // A thumb on the right stick in the close-up, the way a player aims: every frame it points at the goal's middle (the stick's
    // way mapped through the look's own speeds and the player's invert), firm while far (0.7 of the way) and easing to a nudge
    // (0.45) from 15° to 3° off, and it lets go LetGoAt from it; then the view has 0.8 s to settle. Landed: the crosshair on the
    // goal (what A would use) and still, within 1° of its middle with the help on (with it off, on it is enough: nothing centres
    // it). Every frame pushed is watched (Watch); after letting go, `back` counts the frames the view turned back toward `from`
    // and any settle onto it.
    IEnumerator Thumb(BeverageLook look, Interactable goal, Interactable from, Vector3 fromPoint, Push result)
    {
        Vector3 goalPoint = Centre(goal.transform);
        bool helpOn = AimAssist.Enabled;
        look.Aim.Reset();
        float started = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - started < 4f)
        {
            Transform t = look.transform;
            float remaining = Vector3.Angle(t.forward, goalPoint - t.position);
            if (remaining <= LetGoAt) break;
            Vector2 at = LookAngles(t);
            float raw = Mathf.Lerp(Nudge, .7f, Mathf.InverseLerp(3f, 15f, remaining));
            PadHold(new GamepadState { rightStick = StickToward(look, at, goalPoint) * raw });
            yield return null;
            Watch(look, at, result);
        }
        PadRelease();
        result.pushed = Time.realtimeSinceStartup - started;
        result.letGo = AngleOf(look, goalPoint);
        float fromBefore = AngleOf(look, fromPoint);
        float letGo = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - letGo < .8f)
        {
            yield return null;
            float fromNow = AngleOf(look, fromPoint);
            if (fromNow < fromBefore - .01f || from != null && look.Aim.Settling == from) result.back++;
            fromBefore = fromNow;
            if (HeldHelped(look.Aim)) result.held++;
            if (interactor.Focused == goal && !look.Aim.Gliding && (!helpOn || AngleOf(look, goalPoint) < 1f))
            {
                result.landed = true;
                result.seconds = Time.realtimeSinceStartup - started;
                break;
            }
        }
        result.end = AngleOf(look, goalPoint);
    }

    // The stick's way (a unit vector) that turns the close-up's look from `at` toward `point`: the look turns stickSpeed.x a
    // second sideways and stickSpeed.y up and down at full stick, up and down turned over with invert Y (BeverageLook).
    static Vector2 StickToward(BeverageLook look, Vector2 at, Vector3 point)
    {
        Vector2 want = AimAssist.Angles(look.transform.position, point);
        Vector2 off = new Vector2(Mathf.DeltaAngle(at.x, want.x), want.y - at.y);
        Vector2 way = new Vector2(off.x / Mathf.Max(1f, look.stickSpeed.x), -off.y / (Mathf.Max(1f, look.stickSpeed.y) * GameSettings.YSign));
        return way.sqrMagnitude > 1e-12f ? way.normalized : Vector2.zero;
    }

    // One frame of a push in the close-up: did the view move against the stick (against the way the stick alone turns it, from
    // the stick as the game read it this frame), or bend more than 15° off that way? The bend is what tells the 7 Oct help from
    // today's on his note ("it's like pushing me"): its pull could never turn the view backward, but it bent it up to 32° from
    // the stack to the nozzle area (the review found 47° on a long diagonal); now under 4° on the lab's legs (the review's worst
    // over random pushes: 3° at Light, 7° at Strong). And was anything in Ace's hands a target of the help, what it pulled
    // toward, slowed by or settled on, or the pick?
    void Watch(BeverageLook look, Vector2 before, Push result)
    {
        result.frames++;
        Vector2 after = LookAngles(look.transform);
        Vector2 moved = new Vector2(Mathf.DeltaAngle(before.x, after.x), after.y - before.y);
        Vector2 s = PadInput.RightStick;
        Vector2 alone = new Vector2(s.x * look.stickSpeed.x, -s.y * look.stickSpeed.y * GameSettings.YSign);
        if (alone.sqrMagnitude > 1e-8f && moved.sqrMagnitude > 1e-10f && Vector2.Dot(moved, alone.normalized) < -1e-3f) result.against++;
        if (alone.sqrMagnitude > 1e-8f && moved.sqrMagnitude > 1e-6f && Vector2.Angle(moved, alone) > 15f) result.bent++;
        if (HeldHelped(look.Aim)) result.held++;
    }

    bool HeldHelped(AimAssist.State aim)
    {
        interactor.AimTargets(scratch);
        foreach (AimAssist.Target t in scratch)
            if (Holds(t.thing)) return true;
        return Holds(aim.Pulling) || Holds(aim.Slowing) || Holds(aim.Settling) || Holds(interactor.Focused);
    }

    // Part of something in Ace's hands.
    bool Holds(UnityEngine.Object thing)
    {
        if (thing == null || carry == null || carry.Count == 0) return false;
        Component part = thing as Component;
        JobBase job = part != null ? part.GetComponentInParent<JobBase>() : null;
        return job != null && carry.Contains(job);
    }

    // A nudge up off the cup toward the paddle, let go `above` degrees from the cup's middle, short of the paddle: nothing glides
    // back to the cup. At 4° the crosshair is still under the middle of the cup's nozzle area (it may slide on up to it); at 9° it
    // is past that middle, still on the area (it stays where it is). The 7 Oct help glided back down onto the cup from both.
    IEnumerator LetGoShort(BeverageLook look, Interactable cup, Vector3 cupPoint, Interactable paddle, float above)
    {
        yield return Glide(look, cupPoint);
        look.Aim.Reset();
        Vector2 way = StickToward(look, LookAngles(look.transform), Centre(paddle.transform));
        var push = new Push();
        float until = Time.realtimeSinceStartup + 3f / LookSpeed;
        while (Time.realtimeSinceStartup < until && AngleOf(look, cupPoint) < above)
        {
            Vector2 at = LookAngles(look.transform);
            PadHold(new GamepadState { rightStick = way * Nudge });
            yield return null;
            Watch(look, at, push);
        }
        PadRelease();
        float letGo = AngleOf(look, cupPoint), before = letGo;
        int back = 0;
        float stop = Time.realtimeSinceStartup + .6f;
        while (Time.realtimeSinceStartup < stop)
        {
            yield return null;
            float now = AngleOf(look, cupPoint);
            if (now < before - .01f || look.Aim.Settling == cup) back++;
            before = now;
        }
        Check(push.frames > 0 && back == 0 && push.against == 0 && push.held == 0,
            $"a nudge up off the cup, let go {letGo:0.0}° above it (short of the paddle): it never goes back to the cup ({back} frames; now " +
            $"{AngleOf(look, cupPoint):0.0}° above it, on {Name(interactor.Focused)}; against the stick on {push.against} of {push.frames} frames)");
    }

    // STRONG PUSHES ARE FREE in the close-up: full stick from the cup stack across the nozzles, frame by frame, with the help on
    // (Light, then Strong) and off; the help's own share 0 on every frame. Short enough to stay inside the look's turn (52°).
    IEnumerator FullPushCloseUp(BeverageLook look, Vector3 stackPoint)
    {
        float seconds = Mathf.Min(.35f, 40f / Mathf.Max(1f, look.stickSpeed.x * GameSettings.PadLookScale));
        Func<Vector2> angles = () => LookAngles(look.transform);
        Func<AimAssist.State> state = () => look.Aim;
        var rates = new Dictionary<AimHelpLevel, Rate>();
        foreach (AimHelpLevel level in new[] { AimHelpLevel.Light, AimHelpLevel.Strong, AimHelpLevel.Off })
        {
            AimAssist.Strength = level == AimHelpLevel.Off ? AimHelpLevel.Light : level;
            yield return Glide(look, stackPoint);
            look.Aim.Reset();
            AimAssist.Enabled = level != AimHelpLevel.Off;
            var r = new Rate();
            yield return PushRate(new Vector2(1f, 0f), seconds, null, null, r, angles, state);
            rates[level] = r;
            AimAssist.Enabled = true;
        }
        AimAssist.Strength = AimHelpLevel.Light;
        Rate bare = rates[AimHelpLevel.Off];
        foreach (AimHelpLevel level in new[] { AimHelpLevel.Light, AimHelpLevel.Strong })
        {
            Rate r = rates[level];
            Check(bare.rate > .01f && r.rate >= bare.rate * .98f && r.helpedFrames == 0,
                $"{level}: a full push across the nozzles turns at full speed ({r.rate:0}°/s, {bare.rate:0}°/s with no help, over {r.frames} frames; " +
                $"the help's share above 0 on {r.helpedFrames} of them)");
        }
    }

    // Her case (19 Oct, the help helped her once it was on): the look starts 3° off the paddle's edge on one side, a small nudge
    // toward it stops 2.5° off its edge, and let go it settles on it within half a second.
    IEnumerator SmallNudge(BeverageLook look, Interactable paddle, Vector2 side, string what)
    {
        Vector3 point = Centre(paddle.transform);
        float size = HalfSize(paddle, point);
        yield return Glide(look, Beside(look.transform.position, point, side * (size + 3f)));
        look.Aim.Reset();
        Vector2 way = StickToward(look, LookAngles(look.transform), point);
        float until = Time.realtimeSinceStartup + 2f / LookSpeed;
        while (Time.realtimeSinceStartup < until && AngleOf(look, point) > size + 2.5f)
        {
            PadHold(new GamepadState { rightStick = way * Nudge });
            yield return null;
        }
        PadRelease();
        float letGo = AngleOf(look, point);
        yield return Await(() => interactor.Focused == paddle && !look.Aim.Gliding && AngleOf(look, point) < 1f, .5f);
        Check(lastWait, $"{what}: a small nudge let go {letGo:0.0}° from the paddle's middle ({letGo - size:0.0}° off its {size:0.0}° edge) settles " +
                        $"on it ({AngleOf(look, point):0.00}° off; A would use {Name(interactor.Focused)})");
    }

    // One that went just past it: from 5° below the paddle's middle a nudge straight up until 0.5° past its far edge; let go, it
    // settles back onto it (behind by less than half the push: the bench's rule, AimAssist).
    IEnumerator JustPast(BeverageLook look, Interactable paddle, AimHelpLevel level)
    {
        Vector3 point = Centre(paddle.transform);
        float size = HalfSize(paddle, point);
        yield return Glide(look, Beside(look.transform.position, point, new Vector2(0f, 5f)));
        look.Aim.Reset();
        Vector2 up = new Vector2(0f, GameSettings.YSign);   // the look turns up for the stick pushed up (down with invert Y)
        float until = Time.realtimeSinceStartup + 2f / LookSpeed;
        while (Time.realtimeSinceStartup < until
               && !(LookAngles(look.transform).y < AimAssist.Angles(look.transform.position, point).y && AngleOf(look, point) >= size + .5f))
        {
            PadHold(new GamepadState { rightStick = up * Nudge });
            yield return null;
        }
        PadRelease();
        float letGo = AngleOf(look, point);
        yield return Await(() => interactor.Focused == paddle && !look.Aim.Gliding && AngleOf(look, point) < 1f, .6f);
        Check(lastWait, $"{level}: a nudge that went just past the paddle (let go {letGo:0.0}° from its middle, {letGo - size:0.0}° past its far edge) " +
                        $"settles back onto it ({AngleOf(look, point):0.00}° off; A would use {Name(interactor.Focused)})");
    }

    // The pushes' time limits were set at pad look x1, and the lab plays under the player's own Settings (the slider goes down to
    // x0.5, where "just past the paddle" needs 2.3 s at 15 fps and had 2): so they are divided by the pad look speed, and never
    // shorter than at x1 (the review, 23 Oct 2026).
    static float LookSpeed => Mathf.Clamp(GameSettings.PadLookScale, .25f, 1f);

    // The close-up's look glides onto a point (a D-pad step's glide: the help must be on), and comes to rest.
    IEnumerator Glide(BeverageLook look, Vector3 point)
    {
        look.StepAimTo(point);
        yield return Frames(2);
        yield return Await(() => !look.Aim.Gliding, 1.5f);
        yield return Frames(2);
    }

    // The heading and tilt a transform looks along (degrees; tilt positive looks down, as AimAssist's).
    static Vector2 LookAngles(Transform t)
    {
        Vector3 f = t.forward;
        return new Vector2(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg);
    }

    static float AngleOf(BeverageLook look, Vector3 point) => Vector3.Angle(look.transform.forward, point - look.transform.position);

    // A point as far from the eye as `point`, turned from it by `offset` (degrees: heading, tilt; tilt positive is below).
    static Vector3 Beside(Vector3 eye, Vector3 point, Vector2 offset)
    {
        Vector2 a = AimAssist.Angles(eye, point);
        return eye + Quaternion.Euler(a.y + offset.y, a.x + offset.x, 0f) * Vector3.forward * (point - eye).magnitude;
    }

    // ---------- the bench: friction over the step's targets, and the D-pad through what hides one ----------

    // A light push held 0.1 s with the cursor on a screw of the step to do now, then the same push in the open: over the screw
    // the cursor moves at no more than 0.6 of its open speed (§4.6; 0.55 asked).
    IEnumerator FrictionOver(ScrewTarget screw, Camera cam, float scale)
    {
        if (!OfTheStep(screw)) { Note("friction: the screws aren't the step to do now on this device: left out"); yield break; }
        Vector2 on = cam.WorldToScreenPoint(screw.GetComponent<Screw>().HeadPoint);
        PadCursor.GlideTo(on);
        yield return Seconds(.35f);
        Check(inspector.HoveredIsStepTarget, $"the cursor on a screw of the step to do now (hovered: {(inspector.Hovered != null ? inspector.Hovered.name : "nothing")})");
        Vector2 before = PadCursor.Position;
        PadHold(new GamepadState { rightStick = new Vector2(.3f, 0f) });
        yield return Seconds(.1f);
        PadRelease();
        yield return Frames(2);
        float over = (PadCursor.Position - before).magnitude;
        // The open: a corner of the screen with nothing to settle on near it.
        Vector2 open = new Vector2(Screen.width * .5f, Screen.height * .06f);
        PadCursor.GlideTo(open);
        yield return Seconds(.35f);
        before = PadCursor.Position;
        PadHold(new GamepadState { rightStick = new Vector2(.3f, 0f) });
        yield return Seconds(.1f);
        PadRelease();
        yield return Frames(2);
        float openMove = (PadCursor.Position - before).magnitude;
        Check(openMove > 1f && over <= openMove * .6f,
            $"over a target of the step the cursor moves slower: {over / scale:0.0} px against {openMove / scale:0.0} px in the open ({(openMove > 0f ? over / openMove : 0f):0.00}x; 0.6 at most)");
    }

    // A block in the camera's line to one of the step's screws: the screw is out of sight, and D-pad right still gets there.
    IEnumerator HiddenStepTarget(JobBase job, ScrewTarget screw, Camera cam, float scale)
    {
        if (!OfTheStep(screw)) { Note("the hidden step target: the screws aren't the step to do now on this device: left out"); yield break; }
        Screw s = screw.GetComponent<Screw>();
        Vector3 head = s.HeadPoint;
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Aim help check: something in the way";
        block.transform.position = Vector3.Lerp(cam.transform.position, head, .7f);
        block.transform.localScale = Vector3.one * .012f;
        Physics.SyncTransforms();
        yield return Frames(3);
        bool hidden = !inspector.IsSeen(screw.transform);
        Check(hidden, "a block between the camera and a screw of the step: it is out of sight");
        Vector2 at = cam.WorldToScreenPoint(head);
        PadCursor.GlideTo(new Vector2(Screen.width * .08f, Screen.height * .9f));
        yield return Seconds(.4f);
        bool reached = false;
        for (int i = 0; i < 8 && !reached; i++)
        {
            yield return PadPress(GamepadButton.DpadRight);
            yield return Seconds(.3f);
            reached = (PadCursor.Position - at).magnitude <= 12f * scale;
        }
        Check(reached, $"D-pad right reaches the hidden screw all the same (the step's targets first, hidden or not)");
        yield return Photo("04b-dpad-to-a-hidden-screw");
        Destroy(block);
        yield return Frames(2);
    }

    bool OfTheStep(ScrewTarget screw)
    {
        RepairChecklist.Step step = inspector.CurrentStep;
        Screw s = screw != null ? screw.GetComponent<Screw>() : null;
        return step != null && step.targets != null && s != null && step.targets.Contains(s);
    }

    // ---------- the bench: the cursor let go near a screw ----------

    // From a point `distance` off the target, on a side where the target is the nearest thing to settle on.
    IEnumerator CursorSettles(Vector2 target, List<Vector2> others, float distance, string what)
    {
        Vector2 offset = Vector2.zero;
        bool found = false;
        for (int k = 0; k < 24 && !found; k++)
        {
            float a = k * 15f * Mathf.Deg2Rad;
            Vector2 o = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
            Vector2 p = target + o;
            if (p.x < 5f || p.y < 5f || p.x > Screen.width - 5f || p.y > Screen.height - 5f) continue;
            if (others.All(q => (q - p).magnitude > distance + 6f)) { offset = o; found = true; }
        }
        if (!found)
        {
            Note($"  no side {what} from the screw where it's the nearest thing to settle on: skipped");
            yield break;
        }
        what = $"{what} off ({Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg:0}°)";
        PadCursor.GlideTo(target + offset);
        yield return Seconds(.35f);
        // A small push toward it, then let go.
        Vector2 dir = (-offset).normalized;
        PadHold(new GamepadState { rightStick = dir * .3f });
        yield return Frames(2);
        PadRelease();
        float letGo = Time.realtimeSinceStartup;
        float tolerance = 3f * Mathf.Max(.5f, Screen.height / 1080f);
        yield return Await(() => (PadCursor.Position - target).magnitude <= tolerance, .5f);
        Check(lastWait, $"the cursor let go {what} of a screw settles on it in {Time.realtimeSinceStartup - letGo:0.00} s ({(PadCursor.Position - target).magnitude:0.0} px off)");
    }

    // The work's place of what the pad's cursor sits on, within 12 px (at 1080p): a part's (ItemInspector.WorkOrder), a
    // reachable grime spot's (2), or -1 for nothing.
    static int StopUnder(JobBase job, Camera cam, List<GrimeSpot> grime, Vector2 at)
    {
        float limit = 12f * Mathf.Max(.5f, Screen.height / 1080f);
        BenchInteractable part = NearestPart(job, cam, at, out float px);
        float grimePx = float.PositiveInfinity;
        foreach (GrimeSpot g in grime)
        {
            if (g == null) continue;
            Vector3 s = cam.WorldToScreenPoint(Centre(g.transform));
            if (s.z > 0f) grimePx = Mathf.Min(grimePx, (new Vector2(s.x, s.y) - at).magnitude);
        }
        if (part != null && px < limit && px <= grimePx) return ItemInspector.WorkOrder(part);
        return grimePx < limit ? 2 : -1;
    }

    // The grime spots the D-pad steps to, as ItemInspector gathers them: active, on screen, and nothing between the camera
    // and the spot (ItemInspector.Uncovered's ray).
    static List<GrimeSpot> ReachableGrime(JobBase job, Camera cam)
    {
        var found = new List<GrimeSpot>();
        var roots = new List<Transform> { job.transform };
        foreach (GameObject loose in job.LooseParts) if (loose != null) roots.Add(loose.transform);
        foreach (Transform root in roots)
            foreach (GrimeSpot g in root.GetComponentsInChildren<GrimeSpot>())
            {
                if (!g.isActiveAndEnabled || !On(cam, g.transform)) continue;
                Vector3 to = Centre(g.transform) - cam.transform.position;
                if (Physics.Raycast(cam.transform.position, to.normalized, out RaycastHit hit, to.magnitude + .05f)
                    && hit.collider.GetComponentInParent<GrimeSpot>() != g) continue;
                found.Add(g);
            }
        return found;
    }

    static BenchInteractable NearestPart(JobBase job, Camera cam, Vector2 at, out float px)
    {
        px = float.PositiveInfinity;
        BenchInteractable best = null;
        foreach (BenchInteractable p in AllParts(job))
        {
            if (!p.isActiveAndEnabled || !Seen(cam, p.transform)) continue;   // a part under the device can't be what the cursor sits on
            Vector3 s = cam.WorldToScreenPoint(Centre(p.transform));
            if (s.z <= 0f) continue;
            float d = (new Vector2(s.x, s.y) - at).magnitude;
            if (d < px) { px = d; best = p; }
        }
        return best;
    }

    // ---------- looking ----------

    Vector3 Eye()
    {
        Vector3 p = movement.transform.position;
        return new Vector3(p.x, p.y - 1f + view.eyeHeight - movement.EyeDrop, p.z);
    }

    // The view turned onto a point, then off it by an offset (degrees: yaw, pitch; pitch positive looks down).
    void Look(Vector3 point, Vector2 offset)
    {
        Vector2 a = AimAssist.Angles(Eye(), point);
        view.LookTo(a.x + offset.x, a.y + offset.y);
        view.Aim.Reset();
    }

    float AngleTo(Vector3 point)
    {
        Vector2 now = new Vector2(Get<float>(view, "yaw"), Get<float>(view, "pitch"));
        Vector3 forward = Quaternion.Euler(now.y, now.x, 0f) * Vector3.forward;
        return Vector3.Angle(forward, point - Eye());
    }

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        view.LookTo(yaw, 10f);
    }

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    static bool On(Camera cam, Transform t)
    {
        Vector3 s = cam.WorldToScreenPoint(Centre(t));
        return s.z > 0f && s.x > 0f && s.y > 0f && s.x < Screen.width && s.y < Screen.height;
    }

    static Vector3 Centre(Transform t)
    {
        var c = t.GetComponentInChildren<Collider>();
        if (c != null && c.enabled) return c.bounds.center;
        var r = t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }

    static Vector3 Middle(Interactable it) => Centre(it.transform);

    static string Name(Interactable it) => it == null ? "nothing" : it.name;

    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Any).GetValue(target);
}
