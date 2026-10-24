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
// STATIONS AS REACH: A PLAY CHECK (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.5)
//
// Fixit Fidget > Playtest > Stations as reach - play check, overhead / first person (lab, drives itself), or the same as
// Fixit Fidget > Playtest 4 > Systems > Lab A / B - Stations and the repair bar, from above / first person. A fresh Day 1
// in the lab; the café's own arrivals stop and the check brings its own customers, the Day 1 lesson's (a drink, then a
// repair on the bench, then a drink again), and plays each with a virtual gamepad the way a player would, in the one view:
//
//   1. the drink: behind the counter, A talks to them (the prompt says their name; no station first); A, A takes the
//      order; at the dispenser a cup (LB, the left hand), under the nozzle (A), the paddle (A), the poured drink (A);
//      from above the dispenser is a close-up first (A at it; walking out leaves it); then A serves them. The machine's own
//      line (BeverageStation, 23 Oct 2026: the café prompt's look) is checked at three moments: "(A) Take a cup" with the
//      two hands on the stack, "Pouring ..." with its ring, and who the drink in hand is for;
//   2. the repair: the same at the counter; A picks the device up off the intake shelf, A sets it down on the bench;
//      in first person RT works on it, from above A does; the lab's hand does the repair (the bench's own feel is
//      session 4); X picks it up and steps back in one press (12 Oct: in the close-up A uses, as RT does; before, A took
//      it away, the first thing a pad player presses on a screw); A hands it back.
//
// PLAYTEST 4 (10 Oct 2026, the repair bar): the repair's customer waits at a repair-bar stool (the rest of the room is
// held while their request is taken, so a stool is their only choice), and Ace, still behind the bench with their
// finished device, hands it back OVER THE COUNTER with A: no step taken (the walk round the counter's east end was about
// 20 m). Carrying their device to the bench while they wait right there, A still sets it down on the bench (it isn't
// finished). Then
//   3. a drink over the repair bar: a drink customer waits at a stool; the drink is made as in 1; behind the bench, A
//      serves it over the counter, no step taken.
// Photographed from the game's camera ("repair-over-the-counter", "bar-drink-over-the-counter": run it from above for
// the overhead photos).
//
// REASSURE FROM BEHIND THE BENCH (12 Oct 2026, the playtesters' #8): while Ace carries the repair's device to the bench,
// its owner at the stool runs low (the check drains them to 25%: the sweat drop's line is 30%, DayOneGuideUI.Frustrated).
// With the device in hand, A at the bench still sets it down (the mat wins). With empty hands, from above A is for the
// frustrated customer across the counter before the device on the mat ("Reassure"), one press and no step taken; in
// first person the crosshair decides: on the device E is the device's, on them it reaches over the counter. Once they're
// calmer, E is the mat's again. Their patience is then put back, so the rest of the leg is not their race.
//
// Every press is written down with what it was for, and the station presses (ones that only step into or out of a
// station or a close-up) are counted: none in first person, at most one per station from above (the plan's targets).
// The total and the conversation's share are REPORTED, not asserted (playtest 4: the conversation's own pacing decides
// how many a player needs, and the check reads like a player: it waits for the line to reveal and the replies to come up
// before pressing, and presses to hear them out only when they don't come up by themselves).
// It also checks that walking away from each station works and that nothing stays open. Aiming in first person is
// done the way a mouse does it (the view turned onto the thing); the pad's aim help has its own check (AimHelpCheck).
// Photos and report.txt go to Logs/Stations/stations-<view>-<time>/. Nothing is saved in the scene.
// ---------------------------------------------------------------------------
public sealed class StationsCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Stations.Check";
    public const string FirstPersonKey = "FixitFidget.Stations.FirstPerson";

    public bool firstPerson;

    protected override string Title => "Stations as reach - play check, " + (firstPerson ? "first person" : "the overhead view");
    protected override string Tag => "[Stations check]";

#if UNITY_EDITOR
    // Asked for by the editor (Playtest3Session2Steps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        bool fp = PlayerPrefs.GetInt(FirstPersonKey, 0) == 1;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.DeleteKey(FirstPersonKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Stations check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Stations check (this Play session only)");
        var check = go.AddComponent<StationsCheck>();
        check.firstPerson = fp;
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Stations",
            $"stations-{(fp ? "first-person" : "overhead")}-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
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

    struct PressNote { public string customer, what; public bool station, talk; }
    readonly List<PressNote> presses = new List<PressNote>();
    string customer = "";
    // Presses inside a conversation are counted as the conversation's (TakeOrder).
    bool talking;

    // The repair bar (playtest 4): the room's other places to wait, held for a moment so the next customer waits at a stool.
    static readonly FieldInfo BrainSpot = typeof(CustomerBrain).GetField("waitingSpot", Any);
    static readonly FieldInfo BrainState = typeof(CustomerBrain).GetField("state", Any);
    // Reassure from behind the bench (12 Oct, #8): the owner's patience, drained to the sweat drop and put back after.
    static readonly FieldInfo BrainPatience = typeof(CustomerBrain).GetField("patienceLeft", Any);
    static readonly FieldInfo BrainServiceMax = typeof(CustomerBrain).GetField("serviceMax", Any);
    static readonly FieldInfo BrainReassureUses = typeof(CustomerBrain).GetField("reassureUses", Any);
    static readonly FieldInfo BrainReassureReadyAt = typeof(CustomerBrain).GetField("reassureReadyAt", Any);
    const float FrustratedAt = .25f;   // under the sweat drop's 30% (DayOneGuideUI.SweatDropAt), with room to drain while the check looks
    GameObject seatHolder;
    readonly List<WaitingSpot> heldSeats = new List<WaitingSpot>();
    readonly List<string> overTheCounter = new List<string>();
    bool reported;
    // The longest a speech takes to come up by itself: its lines at reading pace (ConversationUI: 1.4-4.5 s each, at most
    // three on screen) and the listening beat (at most 1 s) before Ace's replies appear. A reader waits that long.
    const float ReadingWait = 12f;

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
        Check(movement != null && interactor != null && view != null && carry != null && inspector != null && conversation != null && spawner != null,
            "Ace (his walking, reach, view, hands, bench and conversation) and the café's spawner are in the scene");
        if (interactor == null || spawner == null || view == null) yield break;
        Check(interactor.CounterStation != null && interactor.DrinksStation != null && Bench() != null,
            "the counter, the dispenser and the bench are found by the interactor");
        if (interactor.CounterStation == null || interactor.DrinksStation == null || Bench() == null) yield break;

        // The café's own arrivals stop once it has set the day up: the check brings its own customers.
        yield return Await(() => OpeningDrink() != null, 10f);
        spawnerWas = spawner.enabled;
        spawner.enabled = false;
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        aimWas = AimAssist.Enabled;
        aimTaken = true;   // the player's setting goes back whatever happens (Restore, and OnDisable if Play stops: 19 Oct)
        AimAssist.Enabled = true;
        PlugInPad("Stations check pad");
        yield return Frames(4);
        if (view.FirstPersonSelected != firstPerson) view.SetFirstPerson(firstPerson);
        yield return Seconds(1.2f);
        Check(view.FirstPersonSelected == firstPerson, firstPerson ? "the view is first person" : "the view is the overhead one");
        Geometry();

        // ---------- 1. a drink ----------
        customer = "drink";
        DayOneOpening opening = Opening();
        opening.Reset(true);                        // the Day 1 lesson's first visit: a drink
        Spawn();
        CustomerBrain drinker = spawner.OpeningCustomer;
        Check(drinker != null && drinker.Record != null && drinker.Record.kind == JobKind.Drink,
            $"a drink customer comes in ({(drinker != null ? drinker.CustomerName : "nobody")}, {(drinker != null && drinker.Record != null ? drinker.Record.Subject : "")})");
        if (drinker == null) yield break;
        yield return TakeOrder(drinker);
        if (!lastWait || drinker == null || !drinker.WasAccepted) yield break;
        DrinkDefinition drink = drinker.WantedDrink;
        yield return MakeDrink(drink);
        if (!lastWait) yield break;
        yield return Hand(drinker, () => drinker.CanReceiveDrink, "serve the drink", "they take the drink",
            () => drinker == null || drinker.IsLeaving || !drinker.HasDrinkOrder);
        opening.FinishVisit();
        Check(!interactor.InCloseUp && !inspector.IsHoldingItem && !conversation.InConversation,
            "after the drink nothing is left open (no close-up, no item, no conversation)");

        // ---------- 2. a repair on the bench ----------
        customer = "repair";
        CustomerBrain owner = null;
        for (int attempt = 0; attempt < 8 && owner == null; attempt++)
        {
            ForceRepairStep(opening);
            Spawn();
            CustomerBrain c = spawner.OpeningCustomer;
            if (c == null) break;
            if (IsBenchRepair(c)) owner = c;
            else
            {
                Note($"  {c.CustomerName}'s {(c.Record != null ? c.Record.Subject + " (" + c.Record.faultType + ")" : "visit")} isn't a bench repair: sent home, and another comes in");
                Destroy(c.gameObject);
                opening.FinishVisit();
                yield return Frames(2);
            }
        }
        Check(owner != null, $"a repair customer with a device for the bench comes in ({(owner != null ? owner.CustomerName + ", " + owner.Record.Subject + ", " + owner.Record.faultType : "none")})");
        if (owner == null) yield break;
        // The repair bar (playtest 4): they wait at one of its stools, across the counter from the bench. The rest of the
        // room is held while their request is taken, and the stools let go the moment it is (they choose a place when the
        // conversation closes: CustomerBrain.BeginWaiting).
        List<TableSeat> stools = RepairBarStools();
        int freeStools = HoldTheRoom(stools);
        Check(stools.Count > 0 && freeStools > 0,
            $"the repair bar has stools across the counter from the bench ({stools.Count} within E's reach over it, {freeStools} free; 3 in the redesign)");
        yield return TakeOrder(owner, () => FreeTheStools(stools));
        if (!lastWait || owner == null || !owner.WasAccepted) yield break;
        yield return Until(() => owner.ActiveJob != null, 4f, "their device is on the intake shelf");
        JobBase job = owner.ActiveJob;
        if (job == null) yield break;
        yield return AtAStool(owner, stools);
        if (!lastWait) yield break;
        yield return Repair(job, owner);
        if (!lastWait) yield break;
        // Taken back is the device leaving Ace's hands for theirs; they go home only when nobody owes them anything, so a
        // drink still to come keeps them in their seat (CustomerBrain.CompleteJob). Over the counter, from the bench.
        yield return HandOverTheCounter(owner, () => owner.JobReady, "hand it back over the counter", "they take their device back",
            () => owner == null || owner.IsLeaving || owner.ActiveJob == null);
        if (!lastWait) yield break;
        opening.FinishVisit();
        Check(!interactor.InCloseUp && !inspector.IsHoldingItem && !conversation.InConversation,
            "after the repair nothing is left open (no close-up, no item, no conversation)");
        if (owner != null && !owner.IsLeaving)
        {
            // Staying for a drink they asked for while they waited: sent home, so the next drink can only be the next customer's.
            Note($"  {owner.CustomerName} stays on for a drink they asked for meanwhile: sent home before the next customer");
            Destroy(owner.gameObject);
            yield return Frames(2);
        }

        // ---------- 3. a drink over the repair bar (playtest 4) ----------
        customer = "bar-drink";
        opening.Reset(true);                        // the Day 1 lesson's first visit again: a drink
        Spawn();
        CustomerBrain barDrinker = spawner.OpeningCustomer;
        Check(barDrinker != null && barDrinker.Record != null && barDrinker.Record.kind == JobKind.Drink,
            $"another drink customer comes in ({(barDrinker != null ? barDrinker.CustomerName : "nobody")}, {(barDrinker != null && barDrinker.Record != null ? barDrinker.Record.Subject : "")})");
        if (barDrinker == null) yield break;
        // The repair's customer gets up from their stool as they leave (a few seconds), and a patron may be on another: wait for
        // one to come free before judging (11 Oct: judged the instant the hand-back ended, 0 of 3 once, and Walter then sat on
        // the stool the repair's customer had just left).
        yield return Await(() => (freeStools = HoldTheRoom(stools)) > 0, 12f);
        Check(freeStools > 0, $"a repair-bar stool is free for them ({freeStools} of {stools.Count}; the rest of the room held while their order is taken)");
        yield return TakeOrder(barDrinker, () => FreeTheStools(stools));
        if (!lastWait || barDrinker == null || !barDrinker.WasAccepted) yield break;
        yield return AtAStool(barDrinker, stools);
        if (!lastWait) yield break;
        yield return MakeDrink(barDrinker.WantedDrink);
        if (!lastWait) yield break;
        yield return HandOverTheCounter(barDrinker, () => barDrinker.CanReceiveDrink, "serve the drink over the counter", "they take the drink",
            () => barDrinker == null || barDrinker.IsLeaving || !barDrinker.HasDrinkOrder);
        if (!lastWait) yield break;
        opening.FinishVisit();
        Check(!interactor.InCloseUp && !inspector.IsHoldingItem && !conversation.InConversation,
            "after the drink over the bar nothing is left open (no close-up, no item, no conversation)");

        // ---------- the presses ----------
        Report();
    }

    // The player's aim help setting is put back even when Play stops mid-check (the same net as AimHelpCheck's, 19 Oct 2026).
    void PutAimHelpBack()
    {
        if (!aimTaken) return;
        aimTaken = false;
        if (AimAssist.Enabled != aimWas) AimAssist.Enabled = aimWas;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        PutAimHelpBack();
    }

    protected override void Restore()
    {
        if (movement != null) movement.ScriptedInput = null;
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        PutAimHelpBack();
        if (spawner != null) spawner.enabled = spawnerWas;
        ReleaseHeldSeats();
        if (seatHolder != null) Destroy(seatHolder);
        // A check that stopped early still says what was pressed up to there.
        Report();
    }

    // ---------- the counter ----------

    // accepted: run the moment their request is taken, while their thanks is still on screen (the repair bar's legs let
    // the stools go then).
    IEnumerator TakeOrder(CustomerBrain who, Action accepted = null)
    {
        string name = who.CustomerName;
        yield return Until(() => who == null || who.CanHearIntake, 90f, $"{name} reaches the counter");
        if (!lastWait || who == null) { lastWait = false; yield break; }
        Transform till = interactor.CounterStation.StandPoint;
        PutAce(till.position, till.eulerAngles.y);
        yield return Frames(4);
        Check(interactor.BehindCounter, "Ace at the till is behind the counter");
        if (firstPerson) yield return LookAt(Chest(who));
        yield return Frames(3);
        string prompt = interactor.CurrentPrompt ?? "";
        Check(prompt.Contains(name) && interactor.Focused is CustomerInteractable,
            $"the prompt names whoever is waiting, with nothing stepped up to: \"{prompt}\"");
        yield return Photo($"{customer}-1-at-the-counter");
        talking = true;
        yield return Press(GamepadButton.South, $"talk to {name}");
        yield return Until(() => conversation.InConversation, 3f, "A starts the conversation from behind the counter");
        if (!lastWait) { talking = false; yield break; }
        // Read like a player (playtest 4): their speech comes up line by line at reading pace and Ace's replies follow by
        // themselves, so A only once they're up; "hear them out" (everything at once) only if they don't come up within a
        // reading's time. The check used to press every 0.45 s, which counted two presses a player needn't make.
        float listened = 0f;
        for (int i = 0; i < 10 && who != null && conversation.InConversation && !who.WasAccepted; i++)
        {
            float from = Time.realtimeSinceStartup;
            yield return Await(() => who == null || !conversation.InConversation || who.WasAccepted || conversation.RepliesShowing, ReadingWait);
            listened += Time.realtimeSinceStartup - from;
            if (who == null || !conversation.InConversation || who.WasAccepted) break;
            yield return Press(GamepadButton.South, conversation.RepliesShowing ? "take the request" : "hear them out");
            yield return Seconds(.45f);
        }
        Check(who != null && who.WasAccepted, $"{name}'s request is taken (their speech and the replies came up in {listened:0.0} s, read)");
        if (who != null && who.WasAccepted) accepted?.Invoke();
        yield return Await(() => !conversation.InConversation, 5f);
        if (conversation.InConversation) yield return Press(GamepadButton.South, "close their thanks");
        talking = false;
        yield return Until(() => !conversation.InConversation, 4f, "the conversation closes");
        yield return AfterBlend();
    }

    // ---------- the drinks ----------

    IEnumerator MakeDrink(DrinkDefinition drink)
    {
        StationInteractable drinks = interactor.DrinksStation;
        BeverageSlot slot = drinks.GetComponentsInChildren<BeverageSlot>().FirstOrDefault(s => s.drink == drink);
        BeverageCupSupply stack = drinks.GetComponentsInChildren<BeverageCupSupply>().FirstOrDefault(s => !s.discard);
        Check(slot != null && stack != null, $"the dispenser pours {(drink != null ? drink.drinkName : "their drink")} and has a cup stack");
        if (slot == null || stack == null) { lastWait = false; yield break; }
        BeverageControl zone = slot.GetComponentsInChildren<BeverageControl>().FirstOrDefault(c => !c.dispenseButton);
        BeverageControl paddle = slot.GetComponentsInChildren<BeverageControl>().FirstOrDefault(c => c.dispenseButton);
        Transform stand = drinks.StandPoint;
        if (firstPerson)
        {
            PutAce(stand.position, stand.eulerAngles.y);
            yield return Frames(4);
            Check(!interactor.IsAtStation, "in first person nothing is stepped up to at the dispenser");
        }
        else
        {
            // From above: beside the dispenser (inside its 1.5 m), E (A) opens the close-up.
            PutAce(stand.position - stand.forward * .6f, stand.eulerAngles.y);
            yield return Frames(4);
            string offer = interactor.CurrentPrompt ?? "";
            Check(offer == drinks.StationLabel, $"from above, beside the dispenser, the prompt offers its close-up: \"{offer}\"");
            yield return Press(GamepadButton.South, "open the drinks close-up", station: true);
            yield return Until(() => interactor.IsAtStation, 2f, "A opens the drinks close-up");
            if (!lastWait) yield break;
            yield return AfterBlend();
        }

        if (firstPerson)
        {
            // The whole dispenser first, as a player walking up looks at it (playtest 4, the Bench lead's B7: aimed at
            // the cup stack the far paddles are off screen, so this photo is the one that shows what a player sees).
            Bounds face = new Bounds(Point(stack), Vector3.zero);
            foreach (BeverageControl c in drinks.GetComponentsInChildren<BeverageControl>()) face.Encapsulate(Point(c));
            yield return AimAt(face.center);
            yield return Photo($"{customer}-2a-the-whole-dispenser");
        }

        yield return AimAt(Point(stack));
        yield return Until(() => interactor.Focused == stack, 2f, "the crosshair is on the cup stack");
        yield return Photo($"{customer}-2-at-the-dispenser");
        // 23 Oct 2026, the drinks line in the café prompt's look (BeverageStation): on the stack with both hands free, the action
        // with its button, the two hands under it, and Step back only in the close-up from above (in first person there is
        // nothing to leave).
        BeverageStation drinksLine = drinks.GetComponent<BeverageStation>();
        if (drinksLine != null)
            Check(drinksLine.ActionText == ControlHints.Interact + "  Take a cup" && drinksLine.HandsShowing
                  && drinksLine.StepBackShowing != firstPerson,
                $"the drinks line: \"{RecapPhone.Plain(drinksLine.ActionText)}\", the two hands {(drinksLine.HandsShowing ? "under it" : "MISSING")}, " +
                $"Step back {(drinksLine.StepBackShowing ? "shown" : "not shown")} ({(firstPerson ? "first person: nothing to leave" : "the close-up")})");
        yield return Press(GamepadButton.LeftShoulder, "take a cup in the left hand");
        yield return Until(() => carry.GetHandItem(0) is DrinkJob, 2f, "LB takes a cup in the left hand");
        DrinkJob cup = carry.GetHandItem(0) as DrinkJob;

        yield return AimAt(Point(zone));
        yield return Until(() => interactor.Focused == zone, 2f, $"the crosshair is under the {drink.drinkName} nozzle");
        yield return Press(GamepadButton.South, "put the cup under the nozzle");
        yield return Until(() => slot.Cup == cup, 2f, "A puts the cup under the nozzle");

        yield return AimAt(Point(paddle));
        yield return Until(() => interactor.Focused == paddle, 2f, $"the crosshair is on the {drink.drinkName} paddle");
        yield return Press(GamepadButton.South, "pour");
        yield return Until(() => slot.IsPouring, 2f, "A on the paddle starts the pour");
        if (drinksLine != null)
        {
            // While it pours: "Pouring Latte" with the gold ring beside it, no number, no hands (23 Oct 2026).
            yield return Frames(2);
            Check(drinksLine.RingShowing && drinksLine.ActionText == "Pouring " + drink.drinkName && !drinksLine.HandsShowing,
                $"pouring: \"{drinksLine.ActionText}\", the gold ring beside it ({drinksLine.RingFill:P0}), no hands");
        }
        yield return Until(() => !slot.IsPouring && cup != null && !cup.IsEmpty, (drink != null ? drink.brewSeconds : 5f) * 1.6f + 6f,
            "the drink is poured");

        yield return AimAt(Point(zone));
        yield return Frames(3);
        yield return Press(GamepadButton.South, "take the drink");
        yield return Until(() => carry.Contains(cup), 2f, "A takes the poured drink");
        yield return Photo($"{customer}-3-the-drink-made");
        // With the drink in hand: who it is for (its picture first), no hands (23 Oct 2026).
        if (drinksLine != null)
            Check(RecapPhone.Plain(drinksLine.ActionText).Contains($"Take the {drink.drinkName} to ") && !drinksLine.HandsShowing,
                $"the drink in hand, who it is for: \"{RecapPhone.Plain(drinksLine.ActionText)}\"");

        if (!firstPerson)
        {
            // Walking leaves the close-up: no press.
            PadHold(new GamepadState { leftStick = new Vector2(0f, -1f) });
            yield return Frames(10);
            PadRelease();
            yield return Frames(4);
            Check(!interactor.IsAtStation, "walking (the left stick) leaves the drinks close-up, with no press");
            yield return AfterBlend();
        }
        else Check(!interactor.InCloseUp, "in first person there was no close-up to leave");
        lastWait = true;
    }

    // ---------- the bench ----------

    // waiting: its owner, waiting across the counter from the bench (the repair bar, playtest 4): the device isn't
    // finished, so at the bench A still sets it down rather than handing it back.
    IEnumerator Repair(JobBase job, CustomerBrain waiting = null)
    {
        // Off the intake shelf: behind the counter, level with it.
        Transform till = interactor.CounterStation.StandPoint;
        Vector3 at = till.position;
        at.x = job.transform.position.x;
        PutAce(at, till.eulerAngles.y);
        yield return Frames(4);
        if (firstPerson) yield return LookAt(Middle(job.transform));
        yield return Frames(3);
        Check(interactor.Focused is ItemInteractable shelfItem && shelfItem.Job == job,
            $"the device on the intake shelf is what A picks up: \"{interactor.CurrentPrompt}\"");
        yield return Press(GamepadButton.South, "pick the device up off the shelf");
        yield return Until(() => carry.Contains(job), 2f, "A picks it up");
        if (!lastWait) yield break;

        // Onto the bench.
        StationInteractable bench = Bench();
        PutAce(bench.StandPoint.position, bench.StandPoint.eulerAngles.y);
        yield return Frames(4);
        if (firstPerson) yield return LookAt(BenchTop(bench));
        yield return Frames(3);
        // (12 Oct, #8) Their patience has run low while Ace carried their device over: the sweat drop (under 30%).
        bool frustrated = false;
        if (waiting != null)
        {
            frustrated = MakeFrustrated(waiting);
            yield return Frames(3);
            frustrated = frustrated && DayOneGuideUI.Frustrated(waiting);
            Check(frustrated, $"{waiting.CustomerName} is frustrated at the stool while Ace holds their device (patience {waiting.PatienceFraction:P0}; the sweat drop's line is {DayOneGuideUI.SweatDropAt:P0})");
        }
        string across = waiting != null
            ? $" ({waiting.CustomerName} waits across the counter, {Flat(waiting.transform.position - movement.transform.position).magnitude:0.00} m away, " +
              $"in E's reach over it: {interactor.AcrossTheCounter(waiting.transform.position)}{(frustrated ? ", frustrated" : "")}; an unfinished device isn't handed back)"
            : "";
        Check(interactor.Focused == bench, $"at the bench, the device in hand, A sets it down{(frustrated ? ": the mat wins over a frustrated customer while the hands are full" : "")}{across}: \"{interactor.CurrentPrompt}\"");
        yield return Press(GamepadButton.South, "set it down on the bench");
        yield return Until(() => StationInteractable.BenchHolds(job), 2f, "it's on the bench");
        if (!lastWait) yield break;

        // (12 Oct, #8) Hands empty now: a word over the counter for the frustrated owner, then the mat is E's again.
        if (waiting != null && frustrated)
        {
            yield return ReassureFromTheBench(waiting, job);
            CalmDown(waiting);
        }

        // Work on it.
        if (firstPerson) yield return LookAt(Middle(job.transform));
        yield return Frames(4);
        string say = interactor.CurrentPrompt ?? "", work = interactor.WorkPrompt ?? "";
        if (firstPerson)
        {
            Check(say.StartsWith("Pick up") && work == "Work on it", $"on the device: E \"{say}\", RT \"{work}\"");
            yield return Photo($"{customer}-4-at-the-bench");
            yield return Pull("work on it (RT)");
        }
        else
        {
            Check(say.StartsWith("Work on"), $"from above, E on the device: \"{say}\"");
            yield return Photo($"{customer}-4-at-the-bench");
            yield return Press(GamepadButton.South, "work on it", station: true);
        }
        yield return Until(() => inspector.IsHoldingItem && inspector.FocusedItem == job, 2f, "the inspection close-up opens, nothing stepped up to first");
        if (!lastWait) yield break;
        CinemachineCamera inspectCam = (CinemachineCamera)typeof(ItemInspector).GetField("inspectCam", Any).GetValue(inspector);
        Check(inspectCam != null && inspectCam.Priority == 30, "the inspection camera has the screen");
        yield return AfterBlend();
        Vector3 standing = movement.transform.position;
        PadHold(new GamepadState { leftStick = new Vector2(1f, 0f) });   // the left stick turns the item here; Ace stays put
        yield return Frames(10);
        PadRelease();
        yield return Frames(3);
        Check(Flat(movement.transform.position - standing).magnitude < .02f, "Ace's legs stay still in the close-up");
        yield return Photo($"{customer}-5-working-on-it");

        yield return LabHand(job);
        Check(job.IsComplete, $"the repair is done (by the lab's hand: the bench's feel is session 4); grade {job.Grade}");
        // In the close-up A uses, as RT does (12 Oct): over nothing it leaves the device where it is. (A press the check makes
        // to prove it, not one a player needs: not counted with the visit's presses.)
        yield return PadPress(GamepadButton.South);
        Check(inspector.IsHoldingItem && inspector.FocusedItem == job && !carry.Contains(job), "A in the close-up doesn't take the device away");
        yield return Press(GamepadButton.West, "pick it up and step back (X)");
        yield return Until(() => carry.Contains(job) && !inspector.IsHoldingItem, 3f, "X picks it up and leaves the close-up in one press");
        Check(inspectCam == null || inspectCam.Priority == 0, "the inspection camera lets go");
        Check(!interactor.InCloseUp, "nothing stays open after the bench");
        lastWait = true;
    }

    // Does the job's steps in the order the work goes, as the tools would (Activate is what a tool's click calls): open it
    // up and fix it while there's work left (screws out, the cover off, the grime, the part), then close it up (the cover
    // back on, the screws in). The two halves are kept apart on purpose: the first version picked "lift the cover" before
    // "screw it back in", so a finished device was opened again, round and round, until the lab gave up (7 Oct).
    IEnumerator LabHand(JobBase job)
    {
        handSteps.Clear();
        for (int step = 0; step < 60 && job != null && !job.IsComplete; step++)
        {
            var parts = new List<Component>();
            parts.AddRange(job.GetComponentsInChildren<Component>());
            foreach (GameObject loose in job.DetachedParts)
                if (loose != null) parts.AddRange(loose.GetComponentsInChildren<Component>());
            bool fixing = job.Quality < .999f;
            Component did = null;
            if (fixing)
            {
                ScrewTarget unscrew = parts.OfType<ScrewTarget>().FirstOrDefault(s => !s.GetComponent<Screw>().IsOut && s.CanInteract);
                RemovablePart lift = parts.OfType<RemovablePart>().FirstOrDefault(p => !p.IsRemoved && p.CanInteract);
                GrimeSpot grime = parts.OfType<GrimeSpot>().FirstOrDefault(g => g != null);
                ReplaceablePart replace = parts.OfType<ReplaceablePart>().FirstOrDefault(p => p.CanInteract);
                if (unscrew != null) { unscrew.Activate(); did = unscrew; }
                else if (lift != null) { lift.Activate(); did = lift; }
                else if (grime != null) { grime.Scrub(100000f); did = grime; }
                else if (replace != null) { replace.Activate(); did = replace; }
            }
            else
            {
                RemovablePart refit = parts.OfType<RemovablePart>().FirstOrDefault(p => p.IsRemoved && p.CanInteract);
                ScrewTarget rescrew = parts.OfType<ScrewTarget>().FirstOrDefault(s => s.GetComponent<Screw>().IsOut && s.CanInteract);
                if (refit != null) { refit.Activate(); did = refit; }
                else if (rescrew != null) { rescrew.Activate(); did = rescrew; }
            }
            if (did == null)
            {
                // Something busy (a screw on its way, a cover moving): wait for it.
                yield return Seconds(.3f);
                continue;
            }
            handSteps.Add($"{(fixing ? "fix" : "close")}: {did.GetType().Name} {did.name}");
            // The forgiving bench (14 Oct, §6): each piece flies to its place by itself; the next call waits until it has
            // landed (no longer busy, in its place or back on the device), and no bench piece is ever a rigidbody.
            Component landing = did;
            float until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until && !Settled(landing))
            {
                if (HasBodies(job)) benchBodies++;
                yield return null;
            }
            if (!Settled(landing)) Note($"  {did.GetType().Name} {did.name} hadn't landed after 4 s");
            if (HasBodies(job)) benchBodies++;
            yield return Seconds(.15f);
        }
        Check(benchBodies == 0, $"no bench piece held a rigidbody at any point of the work ({benchBodies} frame(s) with one)");
        if (job != null && !job.IsComplete)
        {
            // What was left, for the report: every part, and what each said it could do.
            var parts = new List<Component>();
            parts.AddRange(job.GetComponentsInChildren<Component>(true));
            foreach (GameObject loose in job.DetachedParts)
                if (loose != null) parts.AddRange(loose.GetComponentsInChildren<Component>(true));
            Note($"the lab's hand gave up: quality {job.Quality:0.00}, {job.DetachedParts.Count} part(s) off, steps: {string.Join("; ", handSteps)}");
            foreach (BenchInteractable part in parts.OfType<BenchInteractable>())
                Note($"  {part.GetType().Name} {part.name}: active {part.gameObject.activeInHierarchy}, can {part.CanInteract}, " +
                     (part is ScrewTarget st ? $"out {st.GetComponent<Screw>().IsOut}, busy {st.GetComponent<Screw>().IsBusy}"
                      : part is RemovablePart rp ? $"removed {rp.IsRemoved}"
                      : part is ReplaceablePart pp ? $"replaced {pp.IsReplaced}" : ""));
        }
    }

    readonly List<string> handSteps = new List<string>();
    int benchBodies;

    // A piece the lab's hand worked on has landed: a screw out in its cup (or home), a cover in its well (or home), a part's
    // swap done with the broken one in the tray, a spot gone.
    static bool Settled(Component did)
    {
        if (did == null) return true;
        BenchStage stage = BenchStage.Instance;
        switch (did)
        {
            case ScrewTarget target:
            {
                Screw screw = target.GetComponent<Screw>();
                return screw == null || !screw.IsBusy && !screw.Flying && (!screw.IsOut || screw.InCup && (stage == null || stage.InPlace(screw)));
            }
            case RemovablePart cover:
                return !cover.Flying && !cover.Popping && (cover.State == RemovablePart.CoverState.Seated || cover.InWell && (stage == null || stage.InPlace(cover)));
            case ReplaceablePart part:
                return part.IsReplaced && (stage == null || !stage.IsFlying(part));
            default:
                return true;
        }
    }

    // Any piece of the job (on the device or off it) with a rigidbody.
    static bool HasBodies(JobBase job)
    {
        if (job == null) return false;
        if (job.GetComponentsInChildren<Rigidbody>(true).Length > 0) return true;
        foreach (GameObject loose in job.LooseParts)
            if (loose != null && loose.GetComponentsInChildren<Rigidbody>(true).Length > 0) return true;
        return false;
    }

    // ---------- handing over ----------

    IEnumerator Hand(CustomerBrain who, Func<bool> ready, string what, string done, Func<bool> finished)
    {
        string name = who != null ? who.CustomerName : "them";
        yield return Until(() => who == null || ready(), 30f, $"{name} is ready for it");
        if (who == null) yield break;
        // In first person the spot must also see them (a seated customer's chair or their table can be in the way from
        // one side, and the chair's box can hide the chest: a player walks round and looks at the face; the lab picks a
        // side and a point, the face first, that the reach ray gets to).
        Vector3 spot = FreeSpotNear(who.transform.position, 1.1f, firstPerson ? who : null);
        PutAce(spot, Yaw(spot, who.transform.position));
        yield return Frames(4);
        // The camera may still be blending back from a close-up (the bench's, just left): the reach ray comes from the
        // camera on screen, so wait for it to be the walking view's before looking.
        if (firstPerson) yield return AfterBlend();
        if (firstPerson) yield return LookAt(SeenPoint(spot + Vector3.up * 1.65f, who));
        yield return Frames(3);
        bool on = interactor.Focused is CustomerInteractable ci && ci.GetComponent<CustomerBrain>() == who;
        Check(on, $"walking up to {name}, A is for them: \"{interactor.CurrentPrompt}\"");
        if (!on && firstPerson) NoteTheCrosshair(who);
        yield return Press(GamepadButton.South, what);
        yield return Until(finished, 4f, done);
    }

    // What the crosshair's ray met instead of them, nearest first (for the report).
    void NoteTheCrosshair(CustomerBrain who)
    {
        if (who == null || Camera.main == null) return;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        CustomerInteractable mine = who.GetComponent<CustomerInteractable>();
        Note($"  the crosshair's ray: {string.Join("; ", hits.Take(5).Select(h => $"{h.collider.name} at {h.distance:0.00} m{(h.collider.isTrigger ? " (trigger)" : "")}"))}" +
             $"; their prompt \"{(mine != null ? mine.Prompt : "")}\", available {(mine != null && mine.IsAvailable)}, on the floor {(mine != null && mine.FloorAvailable)}");
        Vector3 eyeNow = Camera.main.transform.position;
        Note($"  Ace's eye at {eyeNow.x:0.00}, {eyeNow.y:0.00}, {eyeNow.z:0.00} looking {Camera.main.transform.forward.x:0.00}, {Camera.main.transform.forward.y:0.00}, {Camera.main.transform.forward.z:0.00}; " +
             $"{who.CustomerName} at {who.transform.position.x:0.00}, {who.transform.position.y:0.00}, {who.transform.position.z:0.00}, face {(who.LookTarget != null ? who.LookTarget.position.ToString("0.00") : "none")}");
        foreach (Collider c in who.GetComponentsInChildren<Collider>(true))
            Note($"  their collider {c.name} ({c.GetType().Name}): on {c.enabled && c.gameObject.activeInHierarchy}, trigger {c.isTrigger}, layer {LayerMask.LayerToName(c.gameObject.layer)}, bounds {c.bounds.center:0.00} size {c.bounds.size:0.00}");
    }

    // ---------- the repair bar: over the counter (playtest 4) ----------

    // Behind the repair bar's bench, holding what someone at a stool across the counter is waiting for: A hands it over,
    // no step taken. From above E is for them whatever else is nearer (the bench, which would take a device set down);
    // in first person the crosshair says who, and reaches over the counter.
    IEnumerator HandOverTheCounter(CustomerBrain who, Func<bool> ready, string what, string done, Func<bool> finished)
    {
        string name = who != null ? who.CustomerName : "them";
        Transform stand = Bench().StandPoint;
        // Behind the bench: where the repair was done (Ace is there already), or, with a drink from the dispenser, along
        // behind the counter to it (a player walks it; never round the counter).
        float along = Flat(movement.transform.position - stand.position).magnitude;
        if (along > .05f)
        {
            Note($"  Ace takes it along behind the counter to the bench: {along:0.0} m, straight");
            PutAce(stand.position, stand.eulerAngles.y);
            yield return Frames(4);
            yield return Seconds(.8f);              // the camera catches up with him before the photo
        }
        yield return Until(() => who == null || ready(), 30f, $"{name} is ready for it");
        if (!lastWait || who == null) { lastWait = false; yield break; }
        if (firstPerson) yield return AfterBlend();
        Vector3 standing = movement.transform.position;
        float across = Flat(who.transform.position - standing).magnitude;
        Check(interactor.OnStaffSide, "Ace is behind the repair bar's bench (the staff side of the counter)");
        if (firstPerson) yield return LookAt(SeenPoint(Eye(), who));
        yield return Frames(3);
        string prompt = interactor.CurrentPrompt ?? "";
        bool on = interactor.Focused is CustomerInteractable ci && ci.Brain == who;
        Check(on, $"behind the bench, A is for {name} at the stool across the counter, {across:0.00} m away (flat; E reaches " +
                  $"{interactor.AcrossCounterReach:0.0} m over it): \"{prompt}\"");
        if (!on && firstPerson) NoteTheCrosshair(who);
        yield return Photo($"{customer}-over-the-counter");
        yield return Press(GamepadButton.South, what);
        yield return Until(finished, 4f, done);
        bool handed = lastWait;
        float walked = Flat(movement.transform.position - standing).magnitude;
        Check(walked < .05f, $"no step taken for it: Ace walked {walked:0.00} m (round the counter's east end it was about 20 m)");
        overTheCounter.Add($"{customer}: \"{prompt}\" to {name} at the stool, {across:0.00} m across the counter (flat), " +
                           $"{(handed ? "taken" : "NOT taken")}; Ace walked {walked:0.00} m");
        lastWait = handed;
    }

    // ---------- the repair bar: a word over the counter (12 Oct, #8) ----------

    // Behind the bench with empty hands, the owner frustrated at their stool across the counter: from above A is for them
    // before the device on the mat; in first person the crosshair says who (on the device, E is still the device's). One
    // press reassures them, no step taken; calmer, they let the mat have E again.
    IEnumerator ReassureFromTheBench(CustomerBrain who, JobBase job)
    {
        string name = who != null ? who.CustomerName : "them";
        if (who == null) yield break;
        float before = who.PatienceFraction;
        Vector3 standing = movement.transform.position;
        Check(interactor.HandsEmpty && !interactor.IsBehindCounter(movement.transform.position) && interactor.OnStaffSide,
            "Ace is behind the repair bar's bench (not the till) with empty hands");
        if (firstPerson)
        {
            // The crosshair decides. On the device: E is the device's, whoever is frustrated across the counter.
            yield return LookAt(Middle(job.transform));
            yield return Frames(3);
            Check(interactor.Focused is ItemInteractable onMat && onMat.Job == job,
                $"first person, hands empty, the crosshair on the device: E is still the device's, {name} frustrated or not: \"{interactor.CurrentPrompt}\"");
            yield return LookAt(SeenPoint(Eye(), who));
        }
        yield return Frames(3);
        string prompt = interactor.CurrentPrompt ?? "";
        bool on = interactor.Focused is CustomerInteractable ci && ci.Brain == who;
        Check(on && prompt == "Reassure", firstPerson
            ? $"first person, the crosshair on {name} at the stool: E reaches them over the counter: \"{prompt}\""
            : $"from above, hands empty, {name} frustrated at the stool ({before:P0}): E is for them before the device on the mat: \"{prompt}\"");
        if (!on && firstPerson) NoteTheCrosshair(who);
        yield return Photo($"{customer}-reassure-over-the-counter");
        // A press a player would make here, not one of the repair's own: not counted with the visit's presses.
        yield return PadPress(GamepadButton.South);
        yield return Await(() => who == null || who.PatienceFraction > before + .1f, 3f);
        float after = who != null ? who.PatienceFraction : 0f;
        Check(lastWait, $"{name} is reassured from behind the bench with one press (patience {before:P0} → {after:P0})");
        float walked = Flat(movement.transform.position - standing).magnitude;
        Check(walked < .05f, $"no step taken for it: Ace walked {walked:0.00} m (round the counter's east end it was about 20 m)");
        overTheCounter.Add($"{customer}: \"{prompt}\" to {name} at the stool, frustrated ({before:P0} → {after:P0}), from behind the bench with " +
                           $"empty hands, {(lastWait ? "reassured" : "NOT reassured")}; Ace walked {walked:0.00} m");

        // Calmer (the sweat drop gone), the device on the mat has E again.
        if (firstPerson) yield return LookAt(Middle(job.transform));
        yield return Frames(4);
        Check(interactor.Focused is ItemInteractable again && again.Job == job,
            $"{name} calmer, E is the device on the mat's again: \"{interactor.CurrentPrompt}\"");
    }

    // Drains them to the sweat drop (under 30%: DayOneGuideUI.Frustrated), through the field the game drains.
    bool MakeFrustrated(CustomerBrain who)
    {
        if (who == null || BrainPatience == null || BrainServiceMax == null) return false;
        float max = (float)BrainServiceMax.GetValue(who);
        if (max <= 0f) return false;
        BrainPatience.SetValue(who, max * FrustratedAt);
        return true;
    }

    // Their patience back to full and the reassurance forgotten, so the rest of the leg isn't their race.
    void CalmDown(CustomerBrain who)
    {
        if (who == null || BrainPatience == null || BrainServiceMax == null) return;
        BrainPatience.SetValue(who, (float)BrainServiceMax.GetValue(who));
        BrainReassureUses?.SetValue(who, 0);
        BrainReassureReadyAt?.SetValue(who, 0f);
    }

    // The repair bar's stools: the seats across the counter from the bench (in front of its stand point, the way it faces,
    // and within E's reach over the counter from there), west to east.
    List<TableSeat> RepairBarStools()
    {
        var stools = new List<TableSeat>();
        StationInteractable bench = Bench();
        if (bench == null || bench.StandPoint == null) return stools;
        Vector3 from = bench.StandPoint.position;
        Vector3 toward = Flat(bench.StandPoint.forward);
        if (toward.sqrMagnitude < 1e-6f) return stools;
        toward.Normalize();
        foreach (TableSeat seat in FindObjectsByType<TableSeat>(FindObjectsInactive.Exclude))
        {
            if (seat == null || !seat.isActiveAndEnabled) continue;
            Vector3 d = Flat(seat.StandPoint.position - from);
            if (Vector3.Dot(d, toward) > .5f && d.magnitude <= interactor.AcrossCounterReach) stools.Add(seat);
        }
        stools.Sort((a, b) => a.StandPoint.position.x.CompareTo(b.StandPoint.position.x));
        return stools;
    }

    // Holds every free place to wait in the room, the stools too (claimed by the check, as a patron claims a seat), so
    // nobody else takes a stool while the next request is taken (and patrons don't come in meanwhile: PatronSpawner keeps
    // seats free for customers). How many stools were free.
    int HoldTheRoom(List<TableSeat> stools)
    {
        ReleaseHeldSeats();
        return Hold(stools, true);
    }

    // Their request taken: whatever was freed meanwhile is held too, and the stools are let go: the only places left.
    void FreeTheStools(List<TableSeat> stools)
    {
        Hold(stools, false);
        if (seatHolder == null) return;
        foreach (TableSeat stool in stools)
            if (heldSeats.Remove(stool)) stool.Release(seatHolder.transform);
    }

    int Hold(List<TableSeat> stools, bool stoolsToo)
    {
        if (seatHolder == null) seatHolder = new GameObject("Stations check: the room held (this Play session only)");
        int free = 0;
        foreach (WaitingSpot spot in FindObjectsByType<WaitingSpot>(FindObjectsInactive.Exclude))
        {
            if (spot == null || !spot.isActiveAndEnabled || heldSeats.Contains(spot) || !spot.IsAvailable) continue;
            bool stool = spot is TableSeat seat && stools.Contains(seat);
            if (stool && !stoolsToo) continue;
            if (!spot.Claim(seatHolder.transform)) continue;
            heldSeats.Add(spot);
            if (stool) free++;
        }
        return free;
    }

    void ReleaseHeldSeats()
    {
        if (seatHolder != null)
            foreach (WaitingSpot spot in heldSeats)
                if (spot != null) spot.Release(seatHolder.transform);
        heldSeats.Clear();
    }

    // They settle at a stool (the room is the café's again once they have).
    IEnumerator AtAStool(CustomerBrain who, List<TableSeat> stools)
    {
        string name = who != null ? who.CustomerName : "them";
        Check(BrainSpot != null && BrainState != null, "the check can see where a customer waits (CustomerBrain's waitingSpot and state)");
        yield return Await(() => who == null || who.IsLeaving || AtStool(who, stools), 30f);
        ReleaseHeldSeats();
        bool there = who != null && !who.IsLeaving && AtStool(who, stools);
        WaitingSpot spot = SpotOf(who);
        Vector3 bench = Bench().StandPoint.position;
        Check(there, there
            ? $"{name} waits at a repair-bar stool ({spot.name}, draining x{spot.DrainMultiplier:0.00}), {Flat(who.transform.position - bench).magnitude:0.00} m from the bench's stand point, flat"
            : $"{name} waits at a repair-bar stool (they're {(who == null ? "gone" : StateOf(who))}, at {(spot != null ? spot.name : "no place yet")})");
        lastWait = there;
    }

    bool AtStool(CustomerBrain who, List<TableSeat> stools) =>
        StateOf(who) == "Waiting" && SpotOf(who) is TableSeat seat && stools.Contains(seat);

    static WaitingSpot SpotOf(CustomerBrain who) => who != null && BrainSpot != null ? BrainSpot.GetValue(who) as WaitingSpot : null;
    static string StateOf(CustomerBrain who) => who != null && BrainState != null ? BrainState.GetValue(who)?.ToString() ?? "" : "";

    // ---------- the presses ----------

    IEnumerator Press(GamepadButton button, string what, bool station = false)
    {
        presses.Add(new PressNote { customer = customer, what = what, station = station, talk = talking });
        yield return PadPress(button);
    }

    IEnumerator Pull(string what, bool station = false)
    {
        presses.Add(new PressNote { customer = customer, what = what, station = station, talk = talking });
        yield return PadTrigger(true);
    }

    // The presses each visit took, written down; the station presses checked (none in first person, at most one a visit
    // from above). How many presses a visit takes in all is REPORTED, not checked (playtest 4): the conversation's share
    // is its own pacing (the Content lead's lines), and the check reads it like a player.
    void Report()
    {
        if (reported) return;
        reported = true;
        report.AppendLine();
        report.AppendLine($"The presses, {(firstPerson ? "first person" : "from above")} (STATION: only stepping into or out of a station or a close-up; talk: in the conversation):");
        foreach (var group in presses.GroupBy(p => p.customer))
        {
            report.AppendLine($"  {group.Key}: {group.Count()} presses, {group.Count(p => p.talk)} of them the conversation's, {group.Count(p => p.station)} station presses");
            foreach (PressNote p in group) report.AppendLine($"     {(p.station ? "STATION " : p.talk ? "talk    " : "        ")}{p.what}");
        }
        report.AppendLine("  (Playtest 4's run: 9 a drink and 9 a repair, 4 of each the conversation's, pressed every 0.45 s before the replies came up by themselves.)");
        if (overTheCounter.Count > 0)
        {
            report.AppendLine("Over the counter (the repair bar, playtest 4):");
            foreach (string line in overTheCounter) report.AppendLine("  " + line);
        }
        foreach (string visit in presses.Select(p => p.customer).Distinct())
        {
            int station = presses.Count(p => p.customer == visit && p.station);
            if (firstPerson)
                Check(station == 0, $"first person, the {visit}: no station presses ({station}; before playtest 3, 4 a drink and 3 a repair)");
            else
                Check(station <= 1, $"from above, the {visit}: at most one station press ({station}; before playtest 3, 4 a drink and 3 a repair)");
        }
    }

    // ---------- the café ----------

    DayOneOpening Opening() => (DayOneOpening)typeof(CustomerSpawner).GetField("opening", Any).GetValue(spawner);
    DrinkDefinition OpeningDrink() => (DrinkDefinition)typeof(CustomerSpawner).GetField("openingDrink", Any).GetValue(spawner);
    void Spawn() => typeof(CustomerSpawner).GetMethod("Spawn", Any).Invoke(spawner, null);

    // The lesson's second visit (a repair), however the last attempt ended.
    static void ForceRepairStep(DayOneOpening opening)
    {
        opening.Reset(true);
        opening.TryStartVisit();
        opening.FinishVisit();
    }

    // A device worked on at the bench: no counter phone, no support line, no circuit (the lab's hand can't play one).
    static bool IsBenchRepair(CustomerBrain c)
    {
        if (c.Record == null || c.Record.kind != JobKind.Repair || c.IsCounterRepair) return false;
        if (c.Record.faultType != FaultType.Mechanical && c.Record.faultType != FaultType.Cleaning) return false;
        GameObject device = c.Record.devicePrefab;
        return device != null && device.GetComponentInChildren<HoldCallJob>(true) == null;
    }

    StationInteractable Bench() =>
        StationInteractable.All.FirstOrDefault(s => s != null && s.IsWorkSurface && s.StandPoint != null);

    void Geometry()
    {
        Transform till = interactor.CounterStation.StandPoint, drinks = interactor.DrinksStation.StandPoint, bench = Bench().StandPoint;
        Note($"the till (the counter's stand point) at {P(till.position)}, facing {till.eulerAngles.y:0}°; behind the counter there: {interactor.IsBehindCounter(till.position + Vector3.up)}");
        CounterQueue queue = FindAnyObjectByType<CounterQueue>();
        if (queue != null)
            for (int i = 0; i < queue.SlotCount; i++)
            {
                Vector3 p = queue.SlotPoint(i).position;
                Note($"  queue slot {i} at {P(p)}: {Flat(p - till.position).magnitude:0.00} m from the till; behind the counter: {interactor.IsBehindCounter(p + Vector3.up)}");
            }
        Note($"the dispenser's stand point at {P(drinks.position)} ({Flat(drinks.position - till.position).magnitude:0.00} m from the till); the bench's at {P(bench.position)}");
        foreach (TableSeat stool in RepairBarStools())
            Note($"  repair-bar stool {stool.name} at {P(stool.StandPoint.position)}: {Flat(stool.StandPoint.position - bench.position).magnitude:0.00} m from the bench's stand point " +
                 $"(E reaches {interactor.AcrossCounterReach:0.0} m over the counter); drains x{stool.DrainMultiplier:0.00}, sat on: {stool.SnapToSeat}");
    }

    static string P(Vector3 v) => $"({v.x:0.00}, {v.z:0.00})";

    // ---------- moving and looking ----------

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        if (firstPerson) view.LookTo(yaw, 10f);
    }

    // First person: turn the view onto a point (as a mouse would); the close-up: glide its look onto it.
    IEnumerator LookAt(Vector3 point)
    {
        Vector2 a = AimAssist.Angles(Eye(), point);
        view.LookTo(a.x, a.y);
        yield return Frames(3);
    }

    // Ace's first-person eye where he stands (the camera trails a frame: worked out from the view instead).
    Vector3 Eye()
    {
        Vector3 at = movement.transform.position;
        float floor = at.y - 1f;
        return new Vector3(at.x, floor + view.eyeHeight - movement.EyeDrop, at.z);
    }

    IEnumerator AimAt(Vector3 point)
    {
        if (interactor.IsAtStation)
        {
            BeverageLook look = interactor.CurrentStation.GetComponentInChildren<BeverageLook>();
            if (look != null)
            {
                look.StepAimTo(point);
                yield return Frames(2);
                yield return Await(() => !look.Aim.Gliding, 1.5f);
            }
            yield return Frames(3);
        }
        else yield return LookAt(point);
    }

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    static Vector3 Point(Component c)
    {
        var col = c.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : c.transform.position;
    }

    static Vector3 Middle(Transform t)
    {
        var r = t.GetComponentsInChildren<Renderer>().Where(x => x.enabled).ToArray();
        if (r.Length == 0) return t.position;
        Bounds b = r[0].bounds;
        foreach (Renderer x in r) b.Encapsulate(x.bounds);
        return b.center;
    }

    static Vector3 Chest(CustomerBrain who) =>
        who.LookTarget != null ? who.LookTarget.position - Vector3.up * .2f : who.transform.position + Vector3.up * 1.4f;

    static Vector3 BenchTop(StationInteractable bench)
    {
        var col = bench.GetComponent<Collider>();
        if (col == null) col = bench.GetComponentInChildren<Collider>();
        if (col == null) return bench.transform.position;
        Bounds b = col.bounds;
        return new Vector3(b.center.x, b.max.y - .01f, b.center.z);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float Yaw(Vector3 from, Vector3 to) { Vector3 d = Flat(to - from); return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    // A place Ace's capsule fits about `distance` from a point, on the floor; with `seeing`, one whose first-person eye sees
    // that customer's chest past every solid (the reach ray stops at the first solid, as it does for a player).
    Vector3 FreeSpotNear(Vector3 target, float distance, CustomerBrain seeing = null)
    {
        float floor = movement.transform.position.y - 1f;
        Vector3 fallback = Vector3.zero;
        bool anyFree = false;
        for (int ring = 0; ring < 4; ring++)
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(target.x + Mathf.Sin(a) * (distance + ring * .3f), floor, target.z + Mathf.Cos(a) * (distance + ring * .3f));
                Vector3 low = p + Vector3.up * (PlayerMovement.CapsuleRadius + .1f), high = p + Vector3.up * (PlayerMovement.StandingHeight - PlayerMovement.CapsuleRadius);
                bool blocked = false;
                foreach (Collider c in Physics.OverlapCapsule(low, high, PlayerMovement.BodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(movement.transform)) { blocked = true; break; }
                if (blocked) continue;
                Vector3 spot = new Vector3(p.x, 0f, p.z);
                if (seeing == null || Sees(p + Vector3.up * 1.65f, seeing)) return spot;
                if (!anyFree) { fallback = spot; anyFree = true; }
            }
        return anyFree ? fallback : target + Vector3.back * distance;
    }

    // Whether an eye sees some part of a customer the reach ray can pick (their face, their chest, their middle).
    bool Sees(Vector3 eye, CustomerBrain who)
    {
        foreach (Vector3 point in AimPoints(who))
            if (Clear(eye, point, who)) return true;
        return false;
    }

    // The first of those points the eye sees (the chest when none is clear: the check then says what the prompt was).
    Vector3 SeenPoint(Vector3 eye, CustomerBrain who)
    {
        foreach (Vector3 point in AimPoints(who))
            if (Clear(eye, point, who)) return point;
        return Chest(who);
    }

    static IEnumerable<Vector3> AimPoints(CustomerBrain who)
    {
        if (who.LookTarget != null) yield return who.LookTarget.position;
        yield return Chest(who);
        yield return who.transform.position + Vector3.up * 1f;
    }

    // Nothing solid between an eye and a point but the customer (the reach ray stops at the first solid).
    bool Clear(Vector3 eye, Vector3 point, CustomerBrain who)
    {
        Vector3 to = point - eye;
        foreach (RaycastHit hit in Physics.RaycastAll(eye, to.normalized, to.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(movement.transform) || hit.collider.transform.IsChildOf(who.transform)) continue;
            return false;
        }
        return true;
    }
}
