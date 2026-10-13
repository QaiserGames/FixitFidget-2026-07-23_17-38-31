using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE GUIDE'S CARDS (Day 1's guided visits; any day's first impatient customer)
//
// Observes the existing interactions. Does not issue orders, grant stock, complete repairs, change patience, or consume
// input.
//
// PLAYTEST 4 (10 Oct 2026; the first-hour playtester's #1, #3 and #4: "Day 1 teaches the whole loop in 6-second cards that
// never come back"):
//   - A step's card stays until the step is done (DayOneHintTimer): in full for the day's Opening Hint Duration (6 s), then
//     a compact line; still stuck 10 s later, it comes back in full once, with one pulse, and stays. It used to be a 6 s
//     toast that could never return: Grace waited unheard at the counter with no card on screen until she walked out.
//   - At the bench (the close-up) the card sits over the bottom prompt, near the work, where the eye is.
//   - In the overhead view a pin names the step's target over it (UiPins): Grace, the intake tray, the repair bench, the
//     drinks machine, whoever to hand back to; and "You" over Ace for his first seconds.
//   - The first card says who you are. The Officer's coffee (the game's first drink) gets its cards too.
//   - Day 1's controls strip says how to walk and use until Ace has walked a few metres and used E (MovementHintWanted,
//     read by ShopUI).
//   - Any day, the first time someone waiting on Ace gets frustrated (the sweat drop), one card says how to reassure them.
//
// PLAYTEST 4, RUN 2 (11 Oct 2026; the first hour's #3, the veteran's Day 5 note at 3:16 PM): the card agrees with E.
//   - At the counter it follows whoever E talks to first (PlayerInteractor.QueueHead) while the guided visit is still to be
//     heard, and names the visit as waiting too: it said "DAY 1 · THE OFFICER" for 52 s while E talked to Tomas.
//   - The visit the card follows keeps it while Ace is busy with its things: at the drinks machine for the Officer's coffee
//     it turned to "DAY 1 · GRACE" (her latte was on order too).
//   - The impatience card goes when their phone needs Ace (support rang): it said "walk over and reassure" while the fix was
//     "[E] Answer support".
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public class DayOneGuideUI : MonoBehaviour
{
    private TMP_Text sourceText;
    private PlayerInteractor interactor;
    private ItemInspector inspector;
    private ConversationController conversation;
    private GameObject recap;
    private CafeViewMode view;
    private CustomerSpawner spawner;
    private PlayerCarry carry;
    private EspressoMachine machine;
    private DropSpot[] drops;
    private GameObject panel;
    private RectTransform panelRect;
    private Image edge;
    private TMP_Text hint;
    private float nextRefresh, panelY = float.NaN;
    private int hintDay = -1;
    private readonly DayOneHintTimer toast = new();

    // ---- what the card says now, and where its pin points (set as the step's words are worked out) ----
    private bool guiding;
    private string cardTitle = "", cardStep = "", cardAlso = "";
    private DayOneHintTimer.Kind stepKind = DayOneHintTimer.Kind.Step;
    private Transform pinWho;
    private Vector3 pinPoint;
    private string pinLabel = "";
    private CustomerBrain primary;
    private readonly List<CustomerBrain> present = new();
    private readonly List<CustomerBrain> guided = new();
    private float presentAt = -10f;

    // ---- as it is drawn ----
    private string shownText;
    private DayOneHintTimer.Look shownLook = DayOneHintTimer.Look.Hidden;
    private bool shownAtBench, placedAtBench;
    private string shownTitle = "", shownStep = "", shownAlso = "";

    // ---- the first impatient customer (any day) ----
    private CustomerBrain impatient;
    private string impatientText = "";
    private float impatientUntil;
    private int impatienceDay = -1;
    private readonly Dictionary<CustomerBrain, Vector2> lastPatience = new();

    // ---- Ace on his first day ----
    private Vector3 aceLast, selfFrom;
    private bool aceKnown, usedE, promptWas, selfDone;
    private float walked, selfSince = -1f;

    // ---- the stations' tops (they don't move: found once) ----
    private Vector3 drinksTop, benchTop, trayTop;

    /// <summary>Day 1's controls strip should say how to walk and use (ShopUI): until Ace has walked a few metres and used E.</summary>
    public static bool MovementHintWanted { get; private set; }
    /// <summary>Ace has reassured someone this session: the impatience card has done its job.</summary>
    public static bool ReassuredOnce { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MovementHintWanted = false;
        ReassuredOnce = false;
    }

    /// <summary>The sweat drop's line (NpcBeats: Mood.Frustrated under this much patience).</summary>
    public const float SweatDropAt = .3f;
    /// <summary>Day 1's controls strip shows until Ace has walked this far (metres) and used E once.</summary>
    public const float WalkMetres = 3f;
    const float SelfMetres = 1f, SelfSeconds = 15f, ImpatienceSeconds = 12f;

    public const string ImpatientTitle = "GETTING IMPATIENT";
    public const string SelfWords = "You";
    public const string BehindCounterName = "Behind the counter";

    public void Initialize(TMP_Text source, PlayerInteractor player,
        ItemInspector itemInspector, ConversationController dialogue, GameObject recapPanel)
    {
        sourceText = source;
        interactor = player;
        inspector = itemInspector != null ? itemInspector : (player != null ? player.GetComponent<ItemInspector>() : null);
        conversation = dialogue != null ? dialogue : (player != null ? player.GetComponent<ConversationController>() : null);
        recap = recapPanel;
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        view = player != null ? player.GetComponent<CafeViewMode>() : null;
        drops = FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude);
    }

    // ------------------------------------------------------------------ what the labs and checks read

    public bool CardShowing => panel != null && panel.activeSelf;
    /// <summary>The card's words as drawn (its title too, when it's in full).</summary>
    public string CardText => CardShowing && hint != null ? hint.text : "";
    public string CardTitle => CardShowing && shownLook != DayOneHintTimer.Look.Compact ? shownTitle : "";
    public bool CardCompact => CardShowing && shownLook == DayOneHintTimer.Look.Compact;
    /// <summary>The card sits over the bottom prompt, at the bench.</summary>
    public bool CardAtBench => CardShowing && shownAtBench;
    /// <summary>The step has been stuck long enough for the card to come back in full (with its one pulse).</summary>
    public bool CardReminding => CardShowing && impatient == null && toast.Reminding(UiClock.Now);
    /// <summary>The card on the screen, in screen pixels from the bottom left.</summary>
    public Rect CardOnScreen
    {
        get
        {
            if (!CardShowing) return default;
            var corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
    public bool ImpatienceShowing => impatient != null && CardShowing;
    /// <summary>Whose steps the card follows (Grace, the Officer, the opening lesson's customer).</summary>
    public CustomerBrain GuidedCustomer => guiding ? primary : null;
    /// <summary>What the step's pin says and where it points (overhead only; "" for none).</summary>
    public string PinLabel => pinLabel;
    public Transform PinWho => pinWho;
    public Vector3 PinPoint => pinPoint;
    public float WalkedOnDayOne => walked;
    public bool UsedEOnDayOne => usedE;

    // STATIONS AS REACH (playtest 3, 7 Oct 2026): nothing is stepped up to any more, so no line says "press F". The
    // steps are "walk up and ...", in the words of the view in use: first person aims (look at it, click or RT), the
    // overhead view uses E on what's in reach (the dispenser's close-up, a device on the bench).
    private bool FirstPerson => view != null && view.FirstPersonSelected;
    private bool AtDrinks => interactor != null && (interactor.IsAtBeverageStation || interactor.NearDrinks);

    private void LateUpdate()
    {
        // The day intro (13 Oct 2026, Cinematics): the cards wait while it plays; the hints' clock (UiClock) is held with it.
        if (UiPins.DayIntroPlaying) { Show(false); return; }
        DayClock clock = DayClock.Instance;
        float now = UiClock.Now;
        if (clock != null && hintDay != clock.Day)
        {
            hintDay = clock.Day;
            toast.Reset();
            nextRefresh = 0f;
            primary = null;
            impatient = null;
            lastPatience.Clear();
        }
        bool night = NightWalk.Instance != null && NightWalk.Instance.Active;
        WatchAce(clock, night, now);

        bool blocked = clock == null || clock.DayOver || night
            || (recap != null && recap.activeInHierarchy)
            || (conversation != null && conversation.InConversation)
            || sourceText == null || !sourceText.gameObject.activeInHierarchy;
        if (blocked)
        {
            // Dialogue/recap must never leave an old card waiting to pop back up in full: the same step comes back
            // compact after it (DayOneHintTimer), a fresh step in full.
            if (clock == null || clock.DayOver) toast.Reset();
            else toast.Dismiss();
            Show(false);
            GuidePin(false);
            return;
        }

        // Hints need no per-frame scene searches or string/layout rebuilding.
        if (now >= nextRefresh)
        {
            nextRefresh = now + 0.15f;
            Refresh(clock, now);
        }
        Present(now);
    }

    // ------------------------------------------------------------------ what the card says

    private void Refresh(DayClock clock, float now)
    {
        if (now - presentAt >= 1f || presentAt > now)
        {
            presentAt = now;
            present.Clear();
            present.AddRange(FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude));
        }
        WatchReassurance();
        bool wasImpatient = impatient != null;
        if (WatchImpatience(clock, now)) return;
        // The guide's step comes back after the impatience card (compact: it was seen).
        if (wasImpatient) toast.Dismiss();
        guiding = GuideStep(clock, now);
    }

    // The guide's card for this moment: false for none. Sets the card's words, its kind and the pin.
    private bool GuideStep(DayClock clock, float now)
    {
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        CustomerProfile featured = spawner != null ? spawner.FeaturedHintProfile : null;
        if (spawner == null || (!spawner.IsGuidedOpening && featured == null)
            || (!clock.IsOpen && spawner.OpeningCustomer == null && spawner.FeaturedCustomer == null))
        {
            toast.Reset();
            ClearPin();
            return false;
        }

        ClearPin();
        CustomerBrain also = null;
        CustomerBrain customer = featured != null ? PickGuided(spawner.FeaturedCustomer, featured, out also) : spawner.OpeningCustomer;
        if (featured == null) primary = customer;
        else
        {
            // E talks to whoever has waited longest at the counter: while the visit the card follows is still to be heard,
            // the card follows E's target and names the visit as waiting too (it stays the visit the card comes back to).
            CustomerBrain first = HeardFirst(customer);
            if (first != null)
            {
                if (customer.CanHearIntake || customer.CanDecide) also = customer;
                else if (also == first) also = null;
                customer = first;
            }
        }
        bool drinkLesson = spawner.OpeningStep == DayOneOpening.Step.Drink;
        bool visitor = featured != null && customer != null && customer != spawner.FeaturedCustomer;
        string title = featured != null
            ? $"DAY {clock.Day} · {(visitor ? Called(customer) : featured.characterName).ToUpperInvariant()}"
            : drinkLesson ? "FIRST DRINK" : "FIRST REPAIR";
        string next = featured != null
            ? visitor ? VisitAction(customer) : FeaturedAction(customer, featured)
            : NextAction(customer, drinkLesson);
        cardTitle = title;
        cardStep = next;
        cardAlso = also != null ? $"{Cap(Called(also))} is at the counter too." : "";
        // The title separates the visits, so the same instruction can appear once for each visit.
        toast.Observe(title + "\n" + next + "\n" + cardAlso, now, spawner.OpeningHintDuration, stepKind);
        return true;
    }

    // Whose steps the card follows on a guided day: today's featured regular, and on Day 1 the story visit too (the
    // Officer's coffee: Day 1 has no other regulars). Whose thing Ace has in hand (or at the bench, or at the drinks) first;
    // then whoever waits at the counter to be heard; then whoever the card already follows.
    private CustomerBrain PickGuided(CustomerBrain featuredCustomer, CustomerProfile featured, out CustomerBrain alsoWaiting)
    {
        alsoWaiting = null;
        guided.Clear();
        if (featuredCustomer != null && !featuredCustomer.IsLeaving) guided.Add(featuredCustomer);
        if (hintDay == 1)
            foreach (CustomerBrain c in present)
                if (c != null && c != featuredCustomer && !c.IsLeaving && IsStoryVisitor(c, featured)) guided.Add(c);
        if (guided.Count == 0)
        {
            primary = featuredCustomer;
            return featuredCustomer;   // on the way, or the visit is over
        }
        CustomerBrain pick = null;
        // The visit the card already follows keeps it while Ace is busy with its things: at the drinks machine for the
        // Officer's coffee, Grace's latte (on order too) took the card the moment he got there (playtest 4 run 2).
        if (primary != null && guided.Contains(primary) && InHand(primary)) pick = primary;
        if (pick == null)
            foreach (CustomerBrain c in guided)
                if (InHand(c)) { pick = c; break; }
        if (pick == null)
            foreach (CustomerBrain c in guided)
                if (c.CanHearIntake || c.CanDecide) { pick = c; break; }
        if (pick == null && primary != null && guided.Contains(primary)) pick = primary;
        if (pick == null) pick = guided[0];
        primary = pick;
        foreach (CustomerBrain c in guided)
            if (c != pick && (c.CanHearIntake || c.CanDecide)) { alsoWaiting = c; break; }
        return pick;
    }

    // Whoever E talks to first at the counter (PlayerInteractor.QueueHead: the longest wait there), when that isn't the visit
    // the card follows and that visit hasn't been heard yet (on its way in, or waiting at the counter); null otherwise. Its
    // card would say "E talks to ..." of someone E won't reach first.
    private CustomerBrain HeardFirst(CustomerBrain guidedNow)
    {
        if (interactor == null || guidedNow == null || guidedNow.IsLeaving || guidedNow.WasAccepted || InHand(guidedNow)) return null;
        CustomerInteractable head = interactor.QueueHead();
        CustomerBrain first = head != null ? head.Brain : null;
        return first != null && first != guidedNow ? first : null;
    }

    private static bool IsStoryVisitor(CustomerBrain c, CustomerProfile featured)
    {
        CustomerProfile profile = c.Identity != null ? c.Identity.Profile : null;
        return profile != null && profile != featured;
    }

    // What Ace is doing is this customer's: their device in hand or at the bench, their drink in hand, or their order at the
    // drinks machine.
    private bool InHand(CustomerBrain c)
    {
        JobBase job = c.ActiveJob;
        if (job != null && (carry != null && carry.Contains(job) || inspector != null && inspector.FocusedItem == job)) return true;
        if (c.CanReceiveDrink) return true;
        return c.WasAccepted && c.HasDrinkOrder && AtDrinks;
    }

    private string FeaturedAction(CustomerBrain customer, CustomerProfile profile)
    {
        string name = profile != null ? profile.characterName : customer != null ? Called(customer) : "Your guest";
        if (customer == null || customer.IsLeaving)
            return spawner != null && spawner.FeaturedVisitStarted
                ? Noting(VisitOver(name, OpenOrders(customer)))
                : hintDay == 1
                    ? Waiting(FirstCard(name, interactor != null && interactor.BehindCounter))
                    : Waiting($"{name} is visiting again. Talk at the counter to hear how things went.");
        // On their way in on Day 1: who you are and who's coming, with the pin over them.
        if (hintDay == 1 && !customer.WasAccepted && !customer.CanHearIntake && !customer.CanDecide)
            return Waiting(Over(customer, FirstCard(name, interactor != null && interactor.BehindCounter)));
        return VisitAction(customer);
    }

    // A guided visit's next step, once they're in.
    private string VisitAction(CustomerBrain customer)
    {
        if (!customer.WasAccepted) return NextAction(customer, false);
        // A repair can be returned while its drink is still outstanding.
        // Observe that order, even when another hand/cup has claimed it.
        if (customer.HasReturnedRepair && customer.HasDrinkOrder)
            return "Repair returned. " + DrinkAction(customer);
        if (customer.Record != null && customer.Record.kind == JobKind.Drink)
            return DrinkAction(customer);
        if (customer.HasDrinkOrder && (customer.CanReceiveDrink || AtDrinks))
            return DrinkAction(customer);
        return RepairAction(customer);
    }

    private string NextAction(CustomerBrain customer, bool drinkLesson)
    {
        if (customer == null || customer.IsLeaving)
            return Waiting(hintDay == 1 && drinkLesson && spawner != null && spawner.OpeningCustomer == null
                ? "You're Ace. Your café's first day: a customer is on the way."
                : "A customer is on the way. Start with one request.");
        if (!customer.WasAccepted)
        {
            string name = Called(customer);
            if (interactor == null || !interactor.BehindCounter)
                return OverPoint(CounterStand(), BehindCounterName, "Walk behind the service counter to take orders.");
            if (customer.OutOfStock)
                return Over(customer, $"No stock: {E} to talk, then {Q} to apologise.");
            if (customer.ShelfFull)
                return OverPoint(TrayPoint(), FocusNameUI.TrayName, "The intake tray is full. Move an item to a free bench slot.");
            if (customer.CanHearIntake || customer.CanDecide)
                return Over(customer, TalkWords(name, FirstPerson, IsDrinkVisit(customer)));
            return Waiting(Over(customer, $"Wait for {name} to reach the counter."));
        }
        return drinkLesson ? DrinkAction(customer) : RepairAction(customer);
    }

    private static bool IsDrinkVisit(CustomerBrain customer) => customer.Record != null && customer.Record.kind == JobKind.Drink;

    private string DrinkAction(CustomerBrain customer)
    {
        DrinkJob held = carry != null ? carry.Carried as DrinkJob : null;
        DrinkDefinition wanted = customer.WantedDrink;
        string name = Called(customer);
        string drink = customer.WantedDrinkName;
        // Delivery accepts either hand, so guidance must not hide a ready drink
        // merely because the other hand was selected at the dispenser.
        if (carry != null)
            for (int i = 0; i < carry.Count; i++)
                if (carry.GetItem(i) is DrinkJob cup && cup.CanHandBack && cup.Drink == wanted)
                    return Over(customer, $"Walk to {name}; {E} serves the {drink}.");
        if (customer.CanApologiseForDrink)
            return Over(customer, $"No stock. {E} near {name} apologises; restock after closing.");
        if (FindAnyObjectByType<BeverageStation>() != null)
        {
            Vector3 top = DrinksPoint();
            if (held != null && !held.IsEmpty && !held.CanHandBack)
                return OverPoint(top, FocusNameUI.DrinksName, "That drink is cold. Use the discard tray; the cup and ingredients are lost.");
            if (held != null && held.CanHandBack && held.Drink == wanted)
                return Over(customer, $"Walk to {name}; {E} serves the {drink}.");
            // From above the machine's controls work in its close-up (E at it); in first person, looking at it.
            bool inView = FirstPerson ? AtDrinks : interactor != null && interactor.IsAtBeverageStation;
            if (!inView)
                return OverPoint(top, FocusNameUI.DrinksName, FirstPerson ? "Walk to the drinks machine on the back bar and look at it."
                    : interactor != null && interactor.NearDrinks ? $"You're at the drinks machine. {E} brings it into view."
                    : $"Walk to the drinks machine on the back bar. {E} brings it into view.");
            OverPoint(top, FocusNameUI.DrinksName, null);
            foreach (var section in FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude))
            {
                if (section.drink != wanted) continue;
                if (section.IsPouring) return Waiting($"{drink} is filling. You can prepare another cup while it pours.");
                if (section.Cup != null && section.Cup.IsEmpty)
                    return Pad ? $"Cup placed. Aim at the {drink} paddle and press {E} to pour."
                        : $"Cup placed. Look at the {drink} paddle and click to pour.";
                if (section.Cup != null && section.Cup.CanHandBack)
                    return Pad ? $"{drink} is ready. Aim at the cup and press {E} to take it."
                        : $"{drink} is ready. Look at the cup and click to take it.";
            }
            if (held != null && held.IsEmpty)
                return Pad ? $"Aim under the {drink} nozzle and press {E}: your cup goes there."
                    : $"Look under the {drink} nozzle and click: your cup goes there.";
            if (carry != null && !carry.HasSpace)
                return $"Both hands are full. {ControlHints.SwitchHand} picks the other item; serve or set one down.";
            return Pad ? $"Aim at the cup stack. {ControlHints.LeftHand} takes a cup in your left hand, {ControlHints.RightHand} in your right."
                : "Look at the cup stack and click to take a cup (left or right click picks the hand).";
        }
        if (held != null && !held.IsEmpty && held.Drink == wanted)
            return Over(customer, $"Take the {drink} to {name}. {E} serves it.");
        if (carry != null && carry.IsCarrying && (held == null || !held.IsEmpty))
            return "Hands full. Set the item down or return your cup.";

        if (machine == null) machine = FindAnyObjectByType<EspressoMachine>();
        Vector3 machineTop = machine != null ? FocusNameUI.TopOf(machine.gameObject) + Vector3.up * .25f : Vector3.zero;
        OverPoint(machineTop, FocusNameUI.DrinksName, null);
        if (machine != null && machine.IsBrewing)
            return Waiting("Brewing... wait for your drink.");

        foreach (DrinkJob cup in DrinkJob.Live)
        {
            if (cup == null || cup.Owner != customer || cup.IsEmpty) continue;
            if (held != null)
                return $"Return the spare cup to its stack with {E}.";
            return $"{drink} ready. Press {E} at the machine or cup.";
        }
        // A previous customer may have left a cup in the machine. Do not tell
        // the player to load another one into an occupied slot.
        if (machine != null && machine.HasCup)
            return "Machine occupied. Clear its cup before making another drink.";

        ShopInventory stock = ShopInventory.Instance;
        if (customer.CanApologiseForDrink &&
            (stock == null || !stock.CanBrew(wanted) || (held == null && stock.Cups <= 0)))
            return Over(customer, $"No stock. {E} near {name} apologises; restock after closing.");
        if (stock != null && !stock.CanBrew(wanted))
            return "Not enough beans. Restock after closing.";
        if (held != null && held.IsEmpty)
            return $"Empty cup ready. Press {E} at the espresso machine.";
        if (stock != null && stock.Cups <= 0)
            return "No cups. Collect a spare or restock after closing.";
        return $"Press {E} at the cup stack to take an empty cup.";
    }

    private string RepairAction(CustomerBrain customer)
    {
        JobBase job = customer.ActiveJob;
        string name = Called(customer);
        string device = DeviceWords(customer, job);
        if (job == null) return Waiting(OverPoint(TrayPoint(), FocusNameUI.TrayName, $"{Cap(Possessive(name))} {device} will appear on the intake tray."));
        bool inspecting = inspector != null && inspector.FocusedItem == job;
        bool carrying = carry != null && carry.Contains(job);

        if (customer.IsCounterRepair)
            return Over(customer, job.IsComplete
                ? $"Sound restored. The phone is returned automatically; {E} on the customer resumes if you stepped away."
                : Pad ? $"At the counter, press {E} to flip the orange mute switch. {ControlHints.Cancel} steps away without resetting the repair."
                : "At the counter, click the orange mute switch. Right-click steps away without resetting the repair.");
        if (job is HoldCallJob call && !call.IsComplete)
            return OverJob(job, "Phone", call.CurrentPhase == HoldCallRun.State.Ringing ? $"Support answered! {E} on the ringing phone picks up."
                : call.CurrentPhase == HoldCallRun.State.OnHold ? "On hold. Work on another job; return when the phone rings."
                : $"{E} on the support phone calls or redials. No menu choices.");
        if (job.IsComplete || (job.CanHandBack && job.Grade != JobGrade.Rejected && carrying))
        {
            if (inspecting)
                return carry != null && !carry.HasSpace
                    ? "Fixed! Free a hand before collecting the item."
                    : $"Fixed! Press {ControlHints.TakeFromBench} to pick up the item and step back from inspection.";   // X on a pad: A uses at the bench (12 Oct)
            if (carrying)
                return Over(customer, $"{job.Grade} repair. Take it to {name}; {E} hands it back."
                    + (job.Grade == JobGrade.Perfect ? "" : " You can keep repairing for a higher grade."));
            return OverJob(job, PlaceName(job), $"Press {E} to pick up the repaired {device} for delivery.");
        }

        if (inspector != null && inspector.IsHoldingItem && !inspecting)
            return $"Wrong item. {ControlHints.Cancel} puts down tools, then leaves inspection.";
        HumanFault human = job.GetComponentInChildren<HumanFault>();
        if (human != null && !human.Finished && !job.HasDetachedParts)
            return Over(customer, Pad ? $"At the counter, press {E} to flip the orange mute switch and turn sound on. {ControlHints.Cancel} steps away."
                : "At the counter, click the orange mute switch to turn sound on. Right-click steps away.");
        if (carrying)
            return OverPoint(BenchPoint(), FocusNameUI.BenchName, $"Carry the {device} to the repair bench. {E} sets it down.");
        if (carry != null && carry.IsCarrying)
            return "Hands full. Set the item down or return your cup.";
        if (inspecting)
            return RepairBenchAction(job);

        if (!OnBench(job))
            return OverJob(job, PlaceName(job), PickUpFromTray(name, device));
        if (!FirstPerson)
            return OverJob(job, FocusNameUI.BenchName, $"Placed. At the repair bench, press {E} to work on the {device}.");
        return OverJob(job, FocusNameUI.BenchName, Pad ? $"Placed. Look at the {device} on the bench and press {ControlHints.Use} to work on it."
            : $"Placed. Look at the {device} on the bench and click to work on it.");
    }

    private string RepairBenchAction(JobBase job)
    {
        ToolType tool = inspector.CurrentTool;

        CircuitPuzzle circuit = job.GetComponentInChildren<CircuitPuzzle>();
        if (circuit != null)
            return circuit.Finished
                ? "Signal restored. Refit any removed parts, then return the phone."
                : Pad ? $"Aim at the signal tiles and press {ControlHints.Use} to join their white ports before the charge arrives."
                : "Click the signal tiles to join their white ports before the charge arrives.";

        HumanFault human = job.GetComponentInChildren<HumanFault>();
        if (job.Quality >= 0.999f || human != null)
        {
            if (job.HasDetachedComponent<RemovablePart>())
            {
                foreach (GameObject loose in job.DetachedParts)
                {
                    RemovablePart cover = loose != null ? loose.GetComponent<RemovablePart>() : null;
                    if (cover != null && cover.Outward.y < -.5f) return $"Fault fixed. The cover's seat is on the face that's down: press {ControlHints.Flip} to turn it over, then drag the cover home.";
                }
                return $"Fault fixed. Drag the cover from the mat back onto its seat (any tool, or bare hands); it snaps home.";
            }

            if (job.HasDetachedComponent<Screw>())
            {
                foreach (GameObject loose in job.DetachedParts)
                {
                    Screw screw = loose != null ? loose.GetComponent<Screw>() : null;
                    if (screw != null && screw.Axis.y < -.5f) return $"The screw holes are underneath. Press {ControlHints.Flip} to turn it over.";
                }
                return tool == ToolType.Screwdriver
                    ? $"Screwdriver in hand. {Hold("each empty screw hole")}: the driver fetches the screw and turns it home."
                    : $"Cover fitted. {Select("screwdriver")} for the screw holes.";
            }

            // A detached part unregisters as its return animation begins. Keep
            // the message accurate during that short transition.
            foreach (Screw screw in job.GetComponentsInChildren<Screw>())
                if (screw.IsOut || screw.IsBusy)
                    return "Finishing the screws...";
        }

        if (human != null)
            return "No parts need replacing. Leave inspection and talk through the problem with the customer.";

        foreach (Screw screw in job.GetComponentsInChildren<Screw>())
            if (!screw.IsOut)
            {
                // The screws are on the face that's down on the mat: turn it over first (the tabletop, 8 Oct).
                if (screw.Axis.y < -.5f) return $"The screws are underneath. Press {ControlHints.Flip} to turn it over on the mat.";
                return tool == ToolType.Screwdriver
                    ? $"Screwdriver in hand. {Hold("each case screw")} until it comes free and drops."
                    : $"{Select("screwdriver")} to back out the case screws.";
            }

        foreach (RemovablePart cover in job.GetComponentsInChildren<RemovablePart>())
            if (!cover.IsRemoved)
                return cover.IsPopped
                    ? "The cover is up. Drag it off and drop it on the mat."
                    : tool == ToolType.Pry
                        ? $"Pry tool in hand. {Hold("an edge of the loosened cover")} until it pops."
                        : $"{Select("pry tool")} to pop the loosened cover.";

        // The first spot still dirty, in the device's own order: the bench's checklist cleans in the same order (RepairChecklist,
        // 12 Oct), so the two never point at different tools.
        GrimeSpot spot = job.GetComponentInChildren<GrimeSpot>();
        if (spot != null)
        {
            // Dirt wants the brush, a smudge on glass the cloth (4b); either comes off where the tool is moved over it.
            bool smudge = spot.Tool == ToolType.Cloth;
            string toolName = smudge ? "cloth" : "brush", what = smudge ? "smudge" : "exposed grime";
            return tool == spot.Tool
                ? Pad ? $"{(smudge ? "Cloth" : "Brush")} in hand. Hold {ControlHints.Use} and move the cursor over the {what}."
                    : $"{(smudge ? "Cloth" : "Brush")} in hand. Hold left-click and move over the {what}."
                : $"{Select(toolName)} to clean the {what}.";
        }

        foreach (ReplaceablePart part in job.GetComponentsInChildren<ReplaceablePart>())
            if (!part.IsReplaced)
            {
                if (part.Outward.y < -.5f) return $"The broken part is on the face that's down. Press {ControlHints.Flip} to turn it over.";
                return tool == ToolType.Tweezers
                    ? $"Tweezers in hand. {Hold("the broken part")} to pinch it out, and drop it in the tray."
                    : $"{Select("tweezers")} to pinch out the broken part.";
            }
        // The broken part is out (it left the device when it was pinched): the fresh one waits in the tray.
        foreach (GameObject loose in job.DetachedParts)
        {
            ReplaceablePart part = loose != null ? loose.GetComponent<ReplaceablePart>() : null;
            if (part != null && !part.IsReplaced)
                return tool == ToolType.Tweezers
                    ? $"{Hold("the fresh part in the tray")}, carry it over the empty seat and let go."
                    : $"{Select("tweezers")} for the fresh part in the tray.";
        }

        return Pad ? $"Follow the part label: pick the tool it names with {ControlHints.Tools}, then press {ControlHints.Use} on the part."
            : "Follow the part label: click the tool it names, then click the part.";
    }

    // Button names follow the device in use (see ControlHints). With the
    // keyboard these read exactly as the original hints did.
    private static bool Pad => PadInput.UsingPad;
    private static string E => ControlHints.Interact;
    private static string Q => ControlHints.Refuse;
    private static string Aim(string target) => Pad ? $"Press {ControlHints.Use} on {target}" : $"Click {target}";
    private static string Hold(string target) => Pad ? $"Hold {ControlHints.Use} on {target}" : $"Hold left-click on {target}";
    // With a mouse a tool is taken by clicking it (ItemInspector.HandleBenchPress); the bottom line says so too (ShopUI).
    private static string Select(string tool) => Pad ? $"Select the {tool} with {ControlHints.Tools}" : $"Take the {tool} (click it)";

    // ------------------------------------------------------------------ the words (public: the checks read them here)

    /// <summary>Day 1's first card: who you are, and who is on the way. Drops "behind the counter" when Ace is there.</summary>
    // PLACEHOLDER COPY: the game's first words to a player (who Ace is), for his sister to rewrite; at most 90 characters.
    public static string FirstCard(string name, bool behindCounter) => behindCounter
        ? $"You're Ace. Your café's first day: {name} is on the way with a repair."
        : $"You're Ace. Your café's first day: {name} is on the way. Go behind the counter.";

    /// <summary>After the featured visit: true words, whether or not anything is still open.</summary>
    public static string VisitOver(string name, int openOrders) => openOrders > 0
        ? $"{name}'s visit is over. Finish the open {(openOrders == 1 ? "order" : "orders")}; closing time brings the recap."
        : $"{name}'s visit is over. Closing time brings the day's recap.";

    /// <summary>The talk step: E talks, and E takes the job (or the order) when the replies appear.</summary>
    public static string TalkWords(string name, bool firstPerson, bool drink)
    {
        string what = drink ? "order" : "job";
        return firstPerson
            ? $"Look at {name}. {E} talks; when your replies appear, {E} takes the {what}."
            : $"{E} talks to {name}; when your replies appear, {E} takes the {what}.";
    }

    /// <summary>The device waits on the intake tray (playtest 4: a real tray in front of the till; it was "the intake shelf").</summary>
    public static string PickUpFromTray(string name, string device) => $"Press {E} to pick up {Possessive(name)} {device} from the intake tray.";

    /// <summary>The impatience card: the first time someone waiting on Ace gets frustrated.</summary>
    // 13 Oct 2026 (Systems): from behind the bench, E reaches a frustrated customer at a repair-bar stool over the counter, so the
    // card no longer says "walk over". PLACEHOLDER COPY.
    public static string ReassureWords(string name) => $"{name} is getting impatient. Get in reach and press {E} to reassure.";

    /// <summary>A customer as a sentence says them: "Grace", "the Officer" (his name is his job).</summary>
    public static string Called(CustomerBrain customer)
    {
        if (customer == null) return "the customer";
        CustomerProfile profile = customer.Identity != null ? customer.Identity.Profile : null;
        if (profile != null && profile.PersistentId == OfficerStory.ProfileId) return "the " + customer.CustomerName;
        return customer.CustomerName;
    }

    public static string Possessive(string name) => string.IsNullOrEmpty(name) ? "the customer's" : name.EndsWith("s") ? name + "'" : name + "'s";

    private static string Cap(string words) => string.IsNullOrEmpty(words) ? words : char.ToUpperInvariant(words[0]) + words.Substring(1);

    // The device as a sentence names it ("reunion film camera", "pocket watch"); "item" when it has no name.
    private static string DeviceWords(CustomerBrain customer, JobBase job)
    {
        string subject = job != null && job.Record != null ? job.Record.Subject : customer != null && customer.Record != null ? customer.Record.Subject : "";
        subject = ReputationRules.InSentence(subject);
        return string.IsNullOrWhiteSpace(subject) ? "item" : subject;
    }

    // How many orders are still open besides this customer's (true words for the visit-over card).
    private int OpenOrders(CustomerBrain except)
    {
        int n = 0;
        foreach (CustomerBrain c in present)
            if (c != null && c != except && c.WasAccepted && !c.IsLeaving && c.HasJob) n++;
        return n;
    }

    private bool OnBench(JobBase job)
    {
        if (StationInteractable.BenchHolds(job)) return true;
        if (drops != null)
            foreach (DropSpot drop in drops)
                if (drop != null && drop.Kind == DropSpot.SpotKind.Bench && drop.Holds(job))
                    return true;
        return false;
    }

    private string PlaceName(JobBase job) => OnBench(job) ? FocusNameUI.BenchName : FocusNameUI.TrayName;

    // ------------------------------------------------------------------ the pin (set with the words)

    private void ClearPin()
    {
        pinWho = null;
        pinPoint = Vector3.zero;
        pinLabel = "";
        stepKind = DayOneHintTimer.Kind.Step;
    }

    private string Waiting(string words)
    {
        stepKind = DayOneHintTimer.Kind.Wait;
        return words;
    }

    private string Noting(string words)
    {
        stepKind = DayOneHintTimer.Kind.Note;
        return words;
    }

    // Over a person's head: their name.
    private string Over(CustomerBrain who, string words)
    {
        if (who != null)
        {
            pinWho = who.transform;
            pinPoint = Vector3.zero;
            pinLabel = who.CustomerName;
        }
        return words;
    }

    // Over a place (a station's top): its name.
    private string OverPoint(Vector3 point, string label, string words)
    {
        if (point != Vector3.zero)
        {
            pinWho = null;
            pinPoint = point;
            pinLabel = label;
        }
        return words;
    }

    // Over the device itself, named for where it is.
    private string OverJob(JobBase job, string label, string words) =>
        job != null ? OverPoint(FocusNameUI.TopOf(job.gameObject) + Vector3.up * .12f, label, words) : words;

    private Vector3 DrinksPoint()
    {
        if (drinksTop != Vector3.zero) return drinksTop;
        GameObject station = interactor != null && interactor.DrinksStation != null ? interactor.DrinksStation.gameObject : null;
        if (station == null)
        {
            BeverageStation found = FindAnyObjectByType<BeverageStation>();
            station = found != null ? found.gameObject : null;
        }
        drinksTop = station != null ? FocusNameUI.TopOf(station) + Vector3.up * .25f : Vector3.zero;
        return drinksTop;
    }

    private Vector3 BenchPoint()
    {
        if (benchTop != Vector3.zero) return benchTop;
        DropSpot spot = FocusNameUI.SpotOf(DropSpot.SpotKind.Bench);
        benchTop = spot != null ? FocusNameUI.TopOf(spot.gameObject) + Vector3.up * .25f : Vector3.zero;
        return benchTop;
    }

    private Vector3 TrayPoint()
    {
        if (trayTop != Vector3.zero) return trayTop;
        DropSpot spot = FocusNameUI.SpotOf(DropSpot.SpotKind.Counter);
        trayTop = spot != null ? FocusNameUI.TopOf(spot.gameObject) + Vector3.up * .25f : Vector3.zero;
        return trayTop;
    }

    // The floor behind the till, where Ace stands to take orders.
    private Vector3 CounterStand()
    {
        Transform stand = interactor != null && interactor.CounterStation != null ? interactor.CounterStation.StandPoint : null;
        return stand != null ? stand.position + Vector3.up * .2f : Vector3.zero;
    }

    // ------------------------------------------------------------------ the first impatient customer (any day)

    // The first time someone waiting on Ace gets frustrated (their sweat drop: NpcBeats), one card says what to do; once a
    // day until Ace has reassured someone. True while the card is up.
    private bool WatchImpatience(DayClock clock, float now)
    {
        if (impatient != null)
        {
            // Their phone needs Ace now (support rang, or the call dropped): that's the fix, not a walk over to reassure (the
            // veteran, run 2: "GETTING IMPATIENT" stayed up while the prompt read "[E] Answer support" for the same customer).
            bool over = ReassuredOnce || !impatient.isActiveAndEnabled || impatient.IsLeaving || !impatient.ShowsPatience
                || impatient.JobNeedsAttention
                || impatient.PatienceFraction >= SweatDropAt + .05f || now >= impatientUntil;
            if (!over) return true;
            impatient = null;
            return false;
        }
        if (ReassuredOnce || impatienceDay == hintDay || !clock.IsOpen) return false;
        foreach (CustomerBrain c in present)
        {
            if (!Frustrated(c)) continue;
            impatient = c;
            impatientUntil = now + ImpatienceSeconds;
            impatienceDay = hintDay;
            impatientText = ReassureWords(c.CustomerName);
            return true;
        }
        return false;
    }

    /// <summary>Waiting on Ace (their job taken), frustrated (the sweat drop), and reassuring would help them now.</summary>
    public static bool Frustrated(CustomerBrain c)
    {
        if (c == null || !c.isActiveAndEnabled || c.IsLeaving || !c.WasAccepted || !c.ShowsPatience) return false;
        if (c.IsCounterRepair || c.JobNeedsAttention || !c.CanReassure) return false;
        NpcBeats beats = c.GetComponent<NpcBeats>();
        return beats != null ? beats.CurrentMood() >= NpcBeats.Mood.Frustrated : c.PatienceFraction < SweatDropAt;
    }

    // Ace reassured someone: their patience jumped and the reassurance is cooling down (CustomerBrain.Reassure). A drink or
    // a hand-back raises patience too, but leaves reassuring possible.
    private void WatchReassurance()
    {
        if (ReassuredOnce) return;
        foreach (CustomerBrain c in present)
        {
            if (c == null || !c.isActiveAndEnabled || c.IsLeaving || !c.WasAccepted) continue;
            float p = c.PatienceFraction;
            bool can = c.CanReassure;
            if (lastPatience.TryGetValue(c, out Vector2 was) && was.y > .5f && !can && p - was.x >= .1f && p < .88f)
                ReassuredOnce = true;
            lastPatience[c] = new Vector2(p, can ? 1f : 0f);
        }
    }

    // ------------------------------------------------------------------ Ace on his first day

    // Day 1's controls strip (how to walk and use) until Ace has walked a few metres and used E; "You" over him for his first
    // seconds, until he moves.
    private void WatchAce(DayClock clock, bool night, float now)
    {
        bool dayOne = clock != null && clock.Day == 1 && !night && !clock.DayOver;
        Transform ace = interactor != null ? interactor.transform : null;
        if (!dayOne || ace == null)
        {
            MovementHintWanted = false;
            SelfPin(false);
            return;
        }
        Vector3 at = ace.position;
        if (aceKnown)
        {
            Vector3 d = at - aceLast;
            d.y = 0f;
            float step = d.magnitude;
            if (step < 2f) walked += step;   // a jump (a lab putting him somewhere) isn't walking
        }
        aceLast = at;
        aceKnown = true;
        bool talking = conversation != null && conversation.InConversation;
        bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame || PadInput.Pressed(PadButton.South);
        if (pressed && (promptWas || talking)) usedE = true;
        promptWas = interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt);
        MovementHintWanted = !(walked >= WalkMetres && usedE);

        if (!selfDone)
        {
            if (selfSince < 0f)
            {
                selfSince = now;
                selfFrom = at;
            }
            Vector3 moved = at - selfFrom;
            moved.y = 0f;
            if (moved.magnitude >= SelfMetres || now - selfSince >= SelfSeconds || talking) selfDone = true;
        }
        SelfPin(!selfDone && view != null && view.OverheadShown);
    }

    private void SelfPin(bool on)
    {
        UiPins pins = on ? UiPins.Ensure() : UiPins.Instance;
        if (pins == null) return;
        if (on && interactor != null) pins.ShowSelf(SelfWords, interactor.transform);
        else pins.HideSelf();
    }

    private void GuidePin(bool on)
    {
        Transform who = null;
        Vector3 point = Vector3.zero;
        string label = "";
        if (on)
        {
            if (impatient != null)
            {
                who = impatient.transform;
                label = impatient.CustomerName;
            }
            else
            {
                who = pinWho;
                point = pinPoint;
                label = pinLabel;
            }
            on = view != null && view.OverheadShown && !string.IsNullOrEmpty(label) && (who != null || point != Vector3.zero);
        }
        UiPins pins = on ? UiPins.Ensure() : UiPins.Instance;
        if (pins == null) return;
        if (on) pins.ShowGuide(label, who, point);
        else pins.HideGuide();
    }

    // ------------------------------------------------------------------ drawing the card

    // THE UI SKIN (playtest 3, session 3): a card of the phone's paper with dark ink, its title in the brand's green, under
    // the tabs in the top left corner, clear of them as they come and go. Playtest 4: at the bench it sits over the bottom
    // prompt instead, near the work (the prompt drops to the bottom edge there: ShopUI); compact, it is one smaller line.
    private static readonly string TitleColour = UiSkin.HexOf(UiSkin.Brand);
    private static readonly string AlsoColour = UiSkin.HexOf(UiSkin.InkSoft);
    private const float Below = 16f;
    private const float CardWidth = 460f, BenchCardWidth = 580f, CompactWidth = 640f;
    private const float BenchCardBottom = 164f;   // the prompt's top is about 142 px up at the bench (ShopUI drops it 78 px)
    private const float FullSize = 21f, CompactSize = 18f;
    private const float PadLeft = 24f, PadRight = 16f, PadFull = 11f, PadCompact = 7f;

    private float WantedY() => -(TicketRailUI.BottomEdge > 0f ? TicketRailUI.BottomEdge + Below : 24f);

    private void Present(float now)
    {
        DayOneHintTimer.Look look;
        string title, step, also;
        if (impatient != null)
        {
            look = DayOneHintTimer.Look.Full;
            title = ImpatientTitle;
            step = impatientText;
            also = "";
        }
        else if (guiding)
        {
            look = toast.LookAt(now);
            title = cardTitle;
            step = cardStep;
            also = cardAlso;
        }
        else
        {
            look = DayOneHintTimer.Look.Hidden;
            title = step = also = "";
        }
        if (look == DayOneHintTimer.Look.Hidden || string.IsNullOrEmpty(step))
        {
            Show(false);
            GuidePin(false);
            return;
        }
        if (panel == null && !CreatePanel()) return;

        bool bench = inspector != null && inspector.IsHoldingItem;
        // Built only when a word, the look or the place changes (nothing is allocated on the frames between).
        if (shownText == null || look != shownLook || bench != shownAtBench || step != shownStep || title != shownTitle || also != shownAlso)
        {
            string text = look == DayOneHintTimer.Look.Compact ? step
                : $"<color={TitleColour}><b>{title}</b></color>\n{step}"
                  + (also.Length > 0 ? $"\n<size=86%><color={AlsoColour}>{also}</color></size>" : "");
            Layout(text, look, bench);
            shownText = text;
            shownLook = look;
            shownAtBench = bench;
            shownStep = step;
            shownTitle = title;
            shownAlso = also;
        }
        Show(true);
        Follow();
        // Stuck: back in full with one pulse (no sound).
        float pulse = impatient == null ? toast.PulseAt(now) : 0f;
        float s = 1f + .06f * pulse;
        panelRect.localScale = new Vector3(s, s, 1f);
        if (edge != null) edge.color = Color.Lerp(UiSkin.Brand, UiSkin.Gold, pulse);
        GuidePin(true);
    }

    // The card's size for its words: full, the title and the step at 21 px in a 460 px card (580 at the bench), as tall as
    // they need; compact, the step alone at 18 px, as wide as it needs up to 640 px.
    private void Layout(string text, DayOneHintTimer.Look look, bool bench)
    {
        bool compact = look == DayOneHintTimer.Look.Compact;
        hint.enableAutoSizing = false;
        hint.fontSize = compact ? CompactSize : FullSize;
        hint.text = text;
        float most = compact ? CompactWidth : bench ? BenchCardWidth : CardWidth;
        float pad = compact ? PadCompact : PadFull;
        Vector2 words = hint.GetPreferredValues(text, most - PadLeft - PadRight, 0f);
        float width = compact ? Mathf.Min(most, Mathf.Ceil(words.x) + PadLeft + PadRight + 2f) : most;
        float height = Mathf.Max(compact ? 34f : 64f, Mathf.Ceil(words.y) + 2f * pad);
        panelRect.sizeDelta = new Vector2(width, height);
        RectTransform label = hint.rectTransform;
        label.offsetMin = new Vector2(PadLeft, pad);
        label.offsetMax = new Vector2(-PadRight, -pad);
        if (bench != placedAtBench || float.IsNaN(panelY))
        {
            placedAtBench = bench;
            panelY = float.NaN;
            if (bench)
            {
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 0f);
                panelRect.pivot = new Vector2(.5f, 0f);
            }
            else
            {
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
            }
        }
    }

    private void Follow()
    {
        if (panelRect == null) return;
        if (placedAtBench)
        {
            panelRect.anchoredPosition = new Vector2(0f, BenchCardBottom);
            return;
        }
        float want = WantedY();
        if (float.IsNaN(panelY) || !panel.activeSelf) panelY = want;
        else panelY = Mathf.Lerp(panelY, want, 1f - Mathf.Exp(-12f * UiClock.Delta));
        if (Mathf.Abs(panelY - want) < .5f) panelY = want;
        panelRect.anchoredPosition = new Vector2(UiSkin.Margin, panelY);
    }

    private bool CreatePanel()
    {
        if (sourceText == null) return false;
        Canvas canvas = sourceText.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return false;
        canvas = canvas.rootCanvas;
        panel = new GameObject("Next action hint (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        panelRect = rect;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(UiSkin.Margin, WantedY());
        rect.sizeDelta = new Vector2(CardWidth, 104f);
        UiSkin.Paint(rect, new Color(UiSkin.Paper.r, UiSkin.Paper.g, UiSkin.Paper.b, .96f), UiSkin.Radius);
        var shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .3f);
        shadow.effectDistance = new Vector2(0f, -3f);
        // The brand's green down the card's left edge (gold for the moment of its one pulse).
        edge = UiSkin.Paint(new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>(), UiSkin.Brand, 3f);
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.SetParent(rect, false);
        edgeRect.anchorMin = new Vector2(0f, 0f);
        edgeRect.anchorMax = new Vector2(0f, 1f);
        edgeRect.pivot = new Vector2(0f, .5f);
        edgeRect.offsetMin = new Vector2(7f, 9f);
        edgeRect.offsetMax = new Vector2(12f, -9f);

        GameObject label = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(PadLeft, PadFull);
        labelRect.offsetMax = new Vector2(-PadRight, -PadFull);
        hint = label.GetComponent<TextMeshProUGUI>();
        hint.font = sourceText.font;
        UiSkin.UseFont(hint);
        hint.fontSize = FullSize;
        hint.enableAutoSizing = false;
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.color = UiSkin.Ink;
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        hint.raycastTarget = false;
        placedAtBench = false;
        panel.SetActive(false);
        return true;
    }

    private void Show(bool visible)
    {
        if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
        if (!visible) shownText = null;
    }

    private void OnDisable()
    {
        toast.Dismiss();
        Show(false);
        GuidePin(false);
        SelfPin(false);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
    }
}

// Time/input-independent policy, covered by the Edit Mode checks (Checks › Day 1 onboarding).
//
// PLAYTEST 4: a step's card stays until the step changes. In full for its duration (the day's Opening Hint Duration, 3 to
// 10 s), then a compact line; still the same step StuckAfter seconds later, it comes back in full once, with one pulse, and
// stays in full. A step seen before (the player went back a step, or a conversation came between) comes back compact. A
// waiting card (nothing for the player to do yet) settles and never pulses; a note (a visit is over) shows once, in full, for
// its duration, and goes. Observing the step on screen again never restarts its timing.
public sealed class DayOneHintTimer
{
    public enum Look { Hidden, Full, Compact }
    public enum Kind { Step, Wait, Note }

    /// <summary>Compact this long with no progress: the card comes back in full, once, with one pulse.</summary>
    public const float StuckAfter = 10f;
    public const float PulseSeconds = .6f;

    private readonly HashSet<string> shown = new();
    private string current;
    private Kind kind;
    private float fullUntil, remindAt;

    /// <summary>The step on screen now. True when the card's words change (a new step).</summary>
    public bool Observe(string key, float now, float duration, Kind stepKind = Kind.Step)
    {
        if (key == current) return false;
        if (string.IsNullOrEmpty(key))
        {
            Dismiss();
            return false;
        }
        bool again = !shown.Add(key);
        if (again && stepKind == Kind.Note)
        {
            Dismiss();
            return false;
        }
        current = key;
        kind = stepKind;
        fullUntil = again ? now : now + Mathf.Clamp(duration, 3f, 10f);
        remindAt = fullUntil + StuckAfter;
        return true;
    }

    public Look LookAt(float now)
    {
        if (current == null) return Look.Hidden;
        if (now < fullUntil) return Look.Full;
        if (kind == Kind.Note) return Look.Hidden;
        if (kind == Kind.Step && now >= remindAt) return Look.Full;
        return Look.Compact;
    }

    public bool IsVisible(float now) => LookAt(now) != Look.Hidden;
    /// <summary>The step has been stuck long enough to come back in full.</summary>
    public bool Reminding(float now) => current != null && kind == Kind.Step && now >= remindAt;
    /// <summary>The one pulse as the card comes back in full: 0 to 1 and back over PulseSeconds, then 0.</summary>
    public float PulseAt(float now) => !Reminding(now) || now - remindAt >= PulseSeconds ? 0f : Mathf.Sin((now - remindAt) / PulseSeconds * Mathf.PI);
    public string Current => current;
    public void Dismiss() => current = null;
    public void Reset()
    {
        Dismiss();
        shown.Clear();
    }
}
