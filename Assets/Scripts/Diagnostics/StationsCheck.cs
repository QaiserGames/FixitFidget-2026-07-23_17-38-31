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
// Fixit Fidget > Playtest > Stations as reach - play check, overhead / first person (lab, drives itself). A fresh Day 1
// in the lab; the café's own arrivals stop and the check brings its own two customers, the Day 1 lesson's (a drink,
// then a repair on the bench), and plays each with a virtual gamepad the way a player would, in the one view:
//
//   1. the drink: behind the counter, A talks to them (the prompt says their name; no station first); A, A takes the
//      order; at the dispenser a cup (LB, the left hand), under the nozzle (A), the paddle (A), the poured drink (A);
//      from above the dispenser is a close-up first (A at it; walking out leaves it); then A serves them;
//   2. the repair: the same at the counter; A picks the device up off the intake shelf, A sets it down on the bench;
//      in first person RT works on it, from above A does; the lab's hand does the repair (the bench's own feel is
//      session 4); A picks it up and steps back in one press; A hands it back.
//
// Every press is written down with what it was for, and the station presses (ones that only step into or out of a
// station or a close-up) are counted: none in first person, at most one per station from above (the plan's targets).
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
    bool aimWas, spawnerWas = true;

    struct PressNote { public string customer, what; public bool station; }
    readonly List<PressNote> presses = new List<PressNote>();
    string customer = "";

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
        yield return TakeOrder(owner);
        if (!lastWait || owner == null || !owner.WasAccepted) yield break;
        yield return Until(() => owner.ActiveJob != null, 4f, "their device is on the intake shelf");
        JobBase job = owner.ActiveJob;
        if (job == null) yield break;
        yield return Repair(job);
        if (!lastWait) yield break;
        // Taken back is the device leaving Ace's hands for theirs; they go home only when nobody owes them anything, so a
        // drink still to come keeps them in their seat (CustomerBrain.CompleteJob).
        yield return Hand(owner, () => owner.JobReady, "hand it back", "they take their device back",
            () => owner == null || owner.IsLeaving || owner.ActiveJob == null);
        opening.FinishVisit();
        Check(!interactor.InCloseUp && !inspector.IsHoldingItem && !conversation.InConversation,
            "after the repair nothing is left open (no close-up, no item, no conversation)");

        // ---------- the presses ----------
        Report();
    }

    protected override void Restore()
    {
        if (movement != null) movement.ScriptedInput = null;
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (inspector != null && inspector.IsHoldingItem) inspector.CancelInspection();
        if (conversation != null && conversation.InConversation) conversation.End();
        AimAssist.Enabled = aimWas;
        if (spawner != null) spawner.enabled = spawnerWas;
    }

    // ---------- the counter ----------

    IEnumerator TakeOrder(CustomerBrain who)
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
        yield return Press(GamepadButton.South, $"talk to {name}");
        yield return Until(() => conversation.InConversation, 3f, "A starts the conversation from behind the counter");
        if (!lastWait) yield break;
        for (int i = 0; i < 10 && conversation.InConversation && !who.WasAccepted; i++)
        {
            yield return Press(GamepadButton.South, conversation.RepliesShowing ? "take the request" : "hear them out");
            yield return Seconds(.45f);
        }
        Check(who != null && who.WasAccepted, $"{name}'s request is taken");
        yield return Await(() => !conversation.InConversation, 5f);
        if (conversation.InConversation) yield return Press(GamepadButton.South, "close their thanks");
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

        yield return AimAt(Point(stack));
        yield return Until(() => interactor.Focused == stack, 2f, "the crosshair is on the cup stack");
        yield return Photo($"{customer}-2-at-the-dispenser");
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
        yield return Until(() => !slot.IsPouring && cup != null && !cup.IsEmpty, (drink != null ? drink.brewSeconds : 5f) * 1.6f + 6f,
            "the drink is poured");

        yield return AimAt(Point(zone));
        yield return Frames(3);
        yield return Press(GamepadButton.South, "take the drink");
        yield return Until(() => carry.Contains(cup), 2f, "A takes the poured drink");
        yield return Photo($"{customer}-3-the-drink-made");

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

    IEnumerator Repair(JobBase job)
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
        Check(interactor.Focused == bench, $"at the bench A sets it down: \"{interactor.CurrentPrompt}\"");
        yield return Press(GamepadButton.South, "set it down on the bench");
        yield return Until(() => StationInteractable.BenchHolds(job), 2f, "it's on the bench");
        if (!lastWait) yield break;

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
        yield return Press(GamepadButton.South, "pick it up and step back");
        yield return Until(() => carry.Contains(job) && !inspector.IsHoldingItem, 3f, "A picks it up and leaves the close-up in one press");
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
            yield return Seconds(.55f);
        }
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
        if (!on && firstPerson && Camera.main != null)
        {
            // What the crosshair's ray met instead, nearest first (for the report).
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f));
            RaycastHit[] hits = Physics.RaycastAll(ray, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            CustomerInteractable mine = who.GetComponent<CustomerInteractable>();
            Note($"  the crosshair's ray: {string.Join("; ", hits.Take(5).Select(h => $"{h.collider.name} at {h.distance:0.00} m{(h.collider.isTrigger ? " (trigger)" : "")}"))}" +
                 $"; their prompt \"{(mine != null ? mine.Prompt : "")}\", available {(mine != null && mine.IsAvailable)}, on the floor {(mine != null && mine.FloorAvailable)}");
            Vector3 eyeNow = Camera.main.transform.position;
            Note($"  Ace's eye at {eyeNow.x:0.00}, {eyeNow.y:0.00}, {eyeNow.z:0.00} looking {Camera.main.transform.forward.x:0.00}, {Camera.main.transform.forward.y:0.00}, {Camera.main.transform.forward.z:0.00}; " +
                 $"{name} at {who.transform.position.x:0.00}, {who.transform.position.y:0.00}, {who.transform.position.z:0.00}, face {(who.LookTarget != null ? who.LookTarget.position.ToString("0.00") : "none")}");
            foreach (Collider c in who.GetComponentsInChildren<Collider>(true))
                Note($"  their collider {c.name} ({c.GetType().Name}): on {c.enabled && c.gameObject.activeInHierarchy}, trigger {c.isTrigger}, layer {LayerMask.LayerToName(c.gameObject.layer)}, bounds {c.bounds.center:0.00} size {c.bounds.size:0.00}");
        }
        yield return Press(GamepadButton.South, what);
        yield return Until(finished, 4f, done);
    }

    // ---------- the presses ----------

    IEnumerator Press(GamepadButton button, string what, bool station = false)
    {
        presses.Add(new PressNote { customer = customer, what = what, station = station });
        yield return PadPress(button);
    }

    IEnumerator Pull(string what, bool station = false)
    {
        presses.Add(new PressNote { customer = customer, what = what, station = station });
        yield return PadTrigger(true);
    }

    void Report()
    {
        report.AppendLine();
        report.AppendLine($"The presses, {(firstPerson ? "first person" : "from above")} (station presses: only stepping into or out of a station or a close-up):");
        foreach (var group in presses.GroupBy(p => p.customer))
        {
            report.AppendLine($"  {group.Key}: {group.Count()} presses, {group.Count(p => p.station)} of them station presses");
            foreach (PressNote p in group) report.AppendLine($"     {(p.station ? "STATION " : "        ")}{p.what}");
        }
        int drinkStation = presses.Count(p => p.customer == "drink" && p.station);
        int repairStation = presses.Count(p => p.customer == "repair" && p.station);
        if (firstPerson)
            Check(drinkStation == 0 && repairStation == 0, $"first person: no station presses (the drink {drinkStation}, the repair {repairStation}; 4 and 3 before)");
        else
            Check(drinkStation <= 1 && repairStation <= 1, $"from above: at most one a station (the drink {drinkStation}, the repair {repairStation}; 4 and 3 before)");
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
        Vector3 eye = Camera.main != null ? Camera.main.transform.position : movement.transform.position + Vector3.up * .65f;
        // The camera trails a frame: work out the eye from the view instead.
        float floor = movement.transform.position.y - 1f;
        eye = new Vector3(movement.transform.position.x, floor + view.eyeHeight - movement.EyeDrop, movement.transform.position.z);
        Vector2 a = AimAssist.Angles(eye, point);
        view.LookTo(a.x, a.y);
        yield return Frames(3);
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
