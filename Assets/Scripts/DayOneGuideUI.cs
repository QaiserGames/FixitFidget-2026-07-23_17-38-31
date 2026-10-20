using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE NOW LINE, AND THE GUIDE'S CARDS (Day 1's guided visits; any day's first impatient customer; the night's goal)
//
// Observes the existing interactions. Does not issue orders, grant stock, complete repairs, change patience, or consume
// input.
//
// PLAYTEST 7 (19 Oct 2026; his girlfriend, dyslexic, first time, on a PlayStation pad: she "felt overall lost most of the
// time" and did not want to keep playing. "Wait for Grace to reach the counter. Yeah, but you should have like an
// object[ive]." "I feel like you should do like maybe a checklist or something there." "your text needs to be bolder."):
//   - ONE OBJECTIVE LINE, always in the same place, day and night: the top left (under the tabs by day; at night the top
//     left is free). A dark plate (UiSkin.Band) with a thin amber bar down its left; the step in 26 px bold, at most
//     WordBudget.NowLine (10) words, the button as its icon where the step names one (ControlHints: the icon contract of
//     19 Oct). When the line changes it pulses once (1.06 to 1 over 0.25 s) and plays a soft tick (the "bark.choose" cue).
//     It hides only while a conversation, the phone or the recap, the day intro or a night card owns the screen, and at
//     the bench (the bench's own card guides there).
//   - By day: today's guide step while a featured visit is guided (its full card, title and step, for its first 6 s as
//     before, then the line); when nothing is guided (Day 3, or after the featured visits) the most urgent customer's next
//     step from the same step logic (whoever E would serve first, else whoever is closest to walking out); at closing
//     "Closing time: serve who's inside."
//   - At night: NightCycle.NowLine (the bag, the bins, the errand's thing, home), with the notebook's icon and "notes" on
//     the end until the notebook has been opened once; hidden while a scene plays.
//   - The first card says the goal once: "Five stars and the café can franchise. Grace is on her way." ("Does that seem
//     like it's apparent for you? Like you know to franchise or no?... No."). "Go behind the counter" is worked out every
//     time the card's text is set, and the repair bar's side and the back bar count as behind it (the reviewers saw it
//     while Ace stood at the till). With a filled cup in hand the drink step says "Take the latte to Grace". One step a
//     line, no semicolons: "Talk to Grace" now; taking the job is the conversation's own (the card hides meanwhile).
//   - The full card's step is 26 px and its title 20 px; the line is 26 px (they were 21 / 18, the smallest type on screen
//     for the hardest reader).
//
// PLAYTEST 4 (10 Oct 2026; the first-hour playtester's #1, #3 and #4: "Day 1 teaches the whole loop in 6-second cards that
// never come back"):
//   - A step's card stays until the step is done (DayOneHintTimer): in full for the day's Opening Hint Duration (6 s), then
//     a compact line; still stuck 10 s later, it comes back in full once, with one pulse, and stays. It used to be a 6 s
//     toast that could never return: Grace waited unheard at the counter with no card on screen until she walked out.
//   - At the bench (the close-up) the card sat over the bottom prompt, near the work. Since checklist v2 (14 Oct) there is
//     no guide card at the bench: the bench's own card and the glow guide there (see "drawing the card").
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
    private int hintDay = -1, heldCount = -1;
    private readonly DayOneHintTimer toast = new();

    // ---- what the card says now, and where its pin points (set as the step's words are worked out) ----
    private bool guiding, fallback;
    private string cardTitle = "", cardStep = "", cardAlso = "", cardNow = "", stepAlso = "";
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
    private string shownTitle = "", shownStep = "", shownAlso = "", shownLine = "";
    private float changedAt = -10f;

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
    /// <summary>
    /// The device the card is telling Ace to carry to the repair bench ("Carry the reunion film camera to the repair bench"),
    /// or null. While it is, the intake tray doesn't offer to take it back (DropInteractable, StationInteractable): the card
    /// and E agree (checklist v2 §5.7, the First Hour's #4: right after the pickup at the till, E offered to put it straight
    /// back).
    /// </summary>
    public static JobBase CarryingToBench { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MovementHintWanted = false;
        ReassuredOnce = false;
        CarryingToBench = null;
    }

    /// <summary>The sweat drop's line (NpcBeats: Mood.Frustrated under this much patience).</summary>
    public const float SweatDropAt = .3f;
    /// <summary>Day 1's controls strip shows until Ace has walked this far (metres) and used E once.</summary>
    public const float WalkMetres = 3f;
    const float SelfMetres = 1f, SelfSeconds = 15f, ImpatienceSeconds = 12f;

    public const string ImpatientTitle = "GETTING IMPATIENT";
    public const string SelfWords = "You";
    public const string BehindCounterName = "Behind the counter";
    /// <summary>The line once the café has closed and someone is still inside (the fallback, any day).</summary>
    public const string ClosingWords = "Closing time: serve who's inside.";
    /// <summary>The soft tick as the line changes (a SoundBank UI cue: "a soft tick").</summary>
    public const string ChangeCue = "bark.choose";

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
    /// <summary>The one line as drawn (the step; the compact words where the first card has shorter ones), or "".</summary>
    public string NowText => CardShowing ? shownLine : "";
    /// <summary>The line is the fallback (no featured visit guided: the most urgent customer's step, or closing time).</summary>
    public bool Fallback => CardShowing && fallback;
    /// <summary>The line is the night's (NightCycle.NowLine).</summary>
    public bool NightLine => CardShowing && shownNight;
    /// <summary>The size the step's words are drawn at (the full card's step and the line are both LineSize).</summary>
    public float LineSize => hint != null ? hint.fontSize : 0f;
    /// <summary>The card sits over the bottom prompt, at the bench.</summary>
    public bool CardAtBench => CardShowing && shownAtBench;
    /// <summary>The step has been stuck long enough for the card to come back in full (with its one pulse).</summary>
    public bool CardReminding => CardShowing && impatient == null && !fallback && toast.Reminding(UiClock.Now);
    /// <summary>The line changed within the last quarter second: it is mid-pulse.</summary>
    public bool Pulsing => CardShowing && UiClock.Now - changedAt < ChangePulseSeconds;
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
    /// <summary>Whose steps the card follows (Grace, the Officer, the opening lesson's customer; the fallback's customer).</summary>
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
    // Behind the counter, in the player's sense: the till's side, the repair bar's side, or the back bar (the drinks).
    private bool StaffSide => interactor != null && (interactor.OnStaffSide || interactor.NearDrinks || interactor.IsAtBeverageStation);

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

        // The night: the one line from the night's own state (NightCycle.NowLine), in the same place as by day.
        if (night)
        {
            CarryingToBench = null;
            PresentNight(now);
            return;
        }

        bool blocked = clock == null || clock.DayOver || PausePhone.Paused
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
            CarryingToBench = null;
            return;
        }

        // A pickup or a put-down reads the step again at once: the carry step's CarryingToBench must hold from the frame the
        // device is in hand, or a quick second E at the till would put it straight back (checklist v2 §5.7).
        int held = carry != null ? carry.Count : 0;
        if (held != heldCount)
        {
            heldCount = held;
            nextRefresh = 0f;
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
        fallback = false;
        guiding = GuideStep(clock, now);
        // Nothing guided, or the guide's last word said (a visit-over note shown and gone): the fallback line.
        if (!guiding || toast.LookAt(now) == DayOneHintTimer.Look.Hidden) guiding = FallbackStep(clock);
    }

    // The guide's card for this moment: false for none. Sets the card's words, its kind and the pin.
    private bool GuideStep(DayClock clock, float now)
    {
        CarryingToBench = null;   // set again below while the card says to carry a device to the bench
        cardNow = stepAlso = "";
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
        cardAlso = also != null ? $"{Cap(Called(also))} is at the counter too." : stepAlso;
        // The title separates the visits, so the same instruction can appear once for each visit.
        toast.Observe(title + "\n" + next + "\n" + cardAlso, now, spawner.OpeningHintDuration, stepKind);
        return true;
    }

    // Nothing guided (Day 3; Days 1-2 after their featured visits): the most urgent customer's next step, from the same
    // step logic, as the one line (never a full card); at closing, serve who's inside. False when there is nothing to say.
    private bool FallbackStep(DayClock clock)
    {
        CarryingToBench = null;
        cardNow = stepAlso = "";
        ClearPin();   // (the guide's timer is left alone: a visit-over note it has shown must not come back)
        string line = "";
        if (!clock.IsOpen)
        {
            foreach (CustomerBrain c in present)
                if (c != null && c.isActiveAndEnabled && !c.IsLeaving) { line = ClosingWords; break; }
        }
        else
        {
            CustomerBrain customer = MostUrgent();
            primary = customer;
            if (customer != null) line = customer.WasAccepted ? VisitAction(customer) : NextAction(customer, false);
        }
        if (string.IsNullOrEmpty(line)) { ClearPin(); return false; }
        fallback = true;
        cardTitle = "";
        cardStep = line;
        cardAlso = stepAlso;
        return true;
    }

    // Whoever E would serve first: the one whose thing is in Ace's hands (or at the bench, or at the drinks); then whoever E
    // talks to first at the counter (the longest wait there); then, among those waiting on Ace, whoever is closest to walking
    // out; then anyone on their way in.
    private CustomerBrain MostUrgent()
    {
        CustomerBrain pick = null;
        foreach (CustomerBrain c in present)
            if (Here(c) && InHand(c)) { pick = c; break; }
        if (pick == null && interactor != null)
        {
            CustomerInteractable head = interactor.QueueHead();
            CustomerBrain first = head != null ? head.Brain : null;
            if (first != null && Here(first) && (first.CanHearIntake || first.CanDecide)) pick = first;
        }
        if (pick == null)
        {
            float lowest = float.PositiveInfinity;
            foreach (CustomerBrain c in present)
            {
                if (!Here(c) || !c.WasAccepted) continue;
                float p = c.ShowsPatience ? c.PatienceFraction : 2f;   // not waiting on Ace: after everyone who is
                if (p < lowest) { lowest = p; pick = c; }
            }
            // The one the line already follows keeps it while nobody is clearly closer to walking out: two waiting customers
            // with much the same patience mustn't swap the line (and its tick) back and forth.
            if (pick != null && primary != null && primary != pick && Here(primary) && primary.WasAccepted
                && (primary.ShowsPatience ? primary.PatienceFraction : 2f) < lowest + .15f) pick = primary;
        }
        if (pick == null)
            foreach (CustomerBrain c in present)
                if (Here(c)) { pick = c; break; }
        return pick;
    }

    private static bool Here(CustomerBrain c) => c != null && c.isActiveAndEnabled && !c.IsLeaving;

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
    // card would say "Talk to ..." of someone E won't reach first.
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
                    ? Waiting(First(name))
                    : Waiting($"{name} is visiting again. Talk at the counter.");
        // On their way in on Day 1: who you are and who's coming, with the pin over them.
        if (hintDay == 1 && !customer.WasAccepted && !customer.CanHearIntake && !customer.CanDecide)
            return Waiting(Over(customer, First(name)));
        return VisitAction(customer);
    }

    // The first card (the goal, said once), and its one line once it has settled: worked out every time the words are set,
    // so "Go behind the counter" is never said to someone standing there (the reviewers saw it at the till).
    private string First(string name)
    {
        bool behind = StaffSide;
        cardNow = FirstNow(name, behind);
        return FirstCard(name, behind);
    }

    // A guided visit's next step, once they're in.
    private string VisitAction(CustomerBrain customer)
    {
        if (!customer.WasAccepted) return NextAction(customer, false);
        // A repair can be returned while its drink is still outstanding.
        // Observe that order, even when another hand/cup has claimed it.
        if (customer.HasReturnedRepair && customer.HasDrinkOrder)
            return DrinkAction(customer);
        if (customer.Record != null && customer.Record.kind == JobKind.Drink)
            return DrinkAction(customer);
        if (customer.HasDrinkOrder && (customer.CanReceiveDrink || AtDrinks))
            return DrinkAction(customer);
        return RepairAction(customer);
    }

    private string NextAction(CustomerBrain customer, bool drinkLesson)
    {
        if (customer == null || customer.IsLeaving)
            return Waiting("A customer is on the way.");
        if (!customer.WasAccepted)
        {
            string name = Called(customer);
            if (interactor == null || !interactor.BehindCounter)
                return OverPoint(CounterStand(), BehindCounterName, "Go behind the counter to take orders.");
            if (customer.OutOfStock)
                return Over(customer, $"No stock. {E} Talk to {name} and apologise.");
            if (customer.ShelfFull)
                return OverPoint(TrayPoint(), FocusNameUI.TrayName, "The intake tray is full. Move an item to the bench.");
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
                    return Over(customer, TakeDrinkWords(drink, name));
        if (customer.CanApologiseForDrink)
            return Over(customer, $"No stock. {E} Talk to {name} and apologise.");
        if (FindAnyObjectByType<BeverageStation>() != null)
        {
            Vector3 top = DrinksPoint();
            if (held != null && !held.IsEmpty && !held.CanHandBack)
                return OverPoint(top, FocusNameUI.DrinksName, "That drink is cold. Put it in the discard tray.");
            if (held != null && held.CanHandBack && held.Drink == wanted)
                return Over(customer, TakeDrinkWords(drink, name));
            // From above the machine's controls work in its close-up (E at it); in first person, looking at it.
            bool inView = FirstPerson ? AtDrinks : interactor != null && interactor.IsAtBeverageStation;
            // "You're at it" only when E is the close-up (15 Oct, Day 1 by hand: half a metre short of the machine the card
            // said "E brings it into view" while E said "Take empty cup", and took one; NearDrinks keeps half a metre more
            // than the offer, for the card's steadiness).
            if (!inView)
                return OverPoint(top, FocusNameUI.DrinksName, FirstPerson ? "Walk to the drinks machine and look at it."
                    : interactor != null && interactor.Focused is StationCloseUp ? $"{E} Use the drinks machine."
                    : interactor != null && interactor.NearDrinks ? "One more step to the drinks machine."
                    : "Walk to the drinks machine on the back bar.");
            OverPoint(top, FocusNameUI.DrinksName, null);
            foreach (var section in FindObjectsByType<BeverageSlot>(FindObjectsInactive.Exclude))
            {
                if (section.drink != wanted) continue;
                if (section.IsPouring) return Waiting($"{drink} is pouring. Wait, or start another cup.");
                if (section.Cup != null && section.Cup.IsEmpty)
                    return Pad ? $"Aim at the {drink} paddle. {E} Pour."
                        : $"Look at the {drink} paddle. {Use} Pour.";
                if (section.Cup != null && section.Cup.CanHandBack)
                    return Pad ? $"{drink} is ready. Aim at the cup. {E} Take it."
                        : $"{drink} is ready. Look at the cup. {Use} Take it.";
            }
            if (held != null && held.IsEmpty)
                return Pad ? $"Aim under the {drink} nozzle. {E} Place the cup."
                    : $"Look under the {drink} nozzle. {Use} Place the cup.";
            if (carry != null && !carry.HasSpace)
                return "Both hands are full. Serve or set one down.";
            return Pad ? $"Aim at the cup stack. {ControlHints.LeftHand} or {ControlHints.RightHand} Take a cup."
                : $"Look at the cup stack. {ControlHints.LeftHand} or {ControlHints.RightHand} Take a cup.";
        }
        if (held != null && !held.IsEmpty && held.Drink == wanted)
            return Over(customer, TakeDrinkWords(drink, name));
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
                return $"{E} Return the spare cup to its stack.";
            return $"{drink} ready. {E} Take it at the machine.";
        }
        // A previous customer may have left a cup in the machine. Do not tell
        // the player to load another one into an occupied slot.
        if (machine != null && machine.HasCup)
            return "Machine occupied. Clear its cup before making another drink.";

        ShopInventory stock = ShopInventory.Instance;
        if (customer.CanApologiseForDrink &&
            (stock == null || !stock.CanBrew(wanted) || (held == null && stock.Cups <= 0)))
            return Over(customer, $"No stock. {E} Talk to {name} and apologise.");
        if (stock != null && !stock.CanBrew(wanted))
            return "Not enough beans. Restock after closing.";
        if (held != null && held.IsEmpty)
            return $"Empty cup ready. {E} Use the espresso machine.";
        if (stock != null && stock.Cups <= 0)
            return "No cups. Collect a spare or restock after closing.";
        return $"{E} Take an empty cup from the stack.";
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
                ? "Sound restored. The phone goes back by itself."
                : $"At the counter, {(Pad ? E : Use)} Flip the orange mute switch.");
        if (job is HoldCallJob call && !call.IsComplete)
            return OverJob(job, "Phone", call.CurrentPhase == HoldCallRun.State.Ringing ? $"Support answered! {E} Pick up the ringing phone."
                : call.CurrentPhase == HoldCallRun.State.OnHold ? "On hold. Work on another job until the phone rings."
                : $"{E} Call support on the phone.");
        if (job.IsComplete || (job.CanHandBack && job.Grade != JobGrade.Rejected && carrying))
        {
            if (inspecting)
                return carry != null && !carry.HasSpace
                    ? "Fixed! Free a hand before collecting the item."
                    : $"Fixed! {ControlHints.TakeFromBench} Pick it up.";   // X on a pad: A uses at the bench (12 Oct)
            if (carrying)
            {
                // Not Perfect: a second, smaller line says it can still be bettered (one step on the line itself).
                if (job.Grade != JobGrade.Perfect) stepAlso = "You can keep repairing for a higher grade.";
                return Over(customer, $"{job.Grade} repair. Take it to {name}. {E} Hand it back.");
            }
            return OverJob(job, PlaceName(job), $"{E} Pick up the repaired {device}.");
        }

        if (inspector != null && inspector.IsHoldingItem && !inspecting)
            return $"Wrong item. {ControlHints.Cancel} Step back.";
        HumanFault human = job.GetComponentInChildren<HumanFault>();
        if (human != null && !human.Finished && !job.HasDetachedParts)
            return Over(customer, $"At the counter, {(Pad ? E : Use)} Flip the orange mute switch.");
        if (carrying)
        {
            // While the card says this, the intake tray doesn't offer to take it back (CarryingToBench).
            CarryingToBench = job;
            return OverPoint(BenchPoint(), FocusNameUI.BenchName, $"Carry the {device} to the repair bench. {E} Set it down.");
        }
        if (carry != null && carry.IsCarrying)
            return "Hands full. Set the item down or return your cup.";
        // At the bench the guide is the bench's own card, its icons and the glow (checklist v2 §5.6): no card here.
        if (inspecting)
            return "";

        if (!OnBench(job))
            return OverJob(job, PlaceName(job), PickUpFromTray(name, device));
        if (!FirstPerson)
            return OverJob(job, FocusNameUI.BenchName, $"At the repair bench, {E} Work on the {device}.");
        return OverJob(job, FocusNameUI.BenchName, $"Look at the {device} on the bench. {Use} Work on it.");
    }

    // Button names follow the device in use (see ControlHints), as icons since 19 Oct (the icon contract: an icon needs no
    // brackets, and no line says "Press X"; the icon stands where the button's name stood, then the verb).
    private static bool Pad => PadInput.UsingPad;
    private static string E => ControlHints.Interact;
    private static string Use => ControlHints.Use;
    // (Checklist v2, 14 Oct 2026: the bench's step words went with RepairBenchAction; the bench's card, its icons and the glow
    // say them now. Their helpers Aim, Hold and Select went with them.)

    // ------------------------------------------------------------------ the words (public: the checks read them here)

    /// <summary>
    /// Day 1's first card: the goal, said once ("Five stars and the café can franchise."), and who is on the way. Adds "Go
    /// behind the counter." when Ace isn't on the staff's side. PLACEHOLDER COPY (the game's first words to a player), for
    /// his sister to rewrite; at most 12 words before the suffix, 90 characters.
    /// </summary>
    public static string FirstCard(string name, bool behindCounter) =>
        $"Five stars and the café can franchise. {name} is on {(name == "Grace" ? "her" : "the")} way." + (behindCounter ? "" : " Go behind the counter.");

    /// <summary>The first card's one line once it has settled (the goal was said in full): who is coming, and where to stand.</summary>
    public static string FirstNow(string name, bool behindCounter) =>
        behindCounter ? $"{name} is on {(name == "Grace" ? "her" : "the")} way." : $"Go behind the counter. {name} is on the way.";

    /// <summary>After the featured visit: true words, whether or not anything is still open.</summary>
    public static string VisitOver(string name, int openOrders) => openOrders > 0
        ? $"{name}'s visit is over. Finish the open {(openOrders == 1 ? "order" : "orders")}."
        : $"{name}'s visit is over. Closing time brings the recap.";

    /// <summary>The talk step: one step, "Talk to Grace" (in first person, look at her first). Taking the job is the
    /// conversation's own step (its replies say it; the card hides meanwhile).</summary>
    public static string TalkWords(string name, bool firstPerson, bool drink) =>
        firstPerson ? $"Look at {name}. {E} Talk." : $"{E} Talk to {name}.";

    /// <summary>With a filled cup in hand: take it to them (the reviewers: the panel still said "Take a cup").</summary>
    public static string TakeDrinkWords(string drink, string name) => $"Take the {drink} to {name}. {E} Serve it.";

    /// <summary>The device waits on the intake tray (playtest 4: a real tray in front of the till; it was "the intake shelf").</summary>
    public static string PickUpFromTray(string name, string device) => $"{E} Pick up {Possessive(name)} {device} from the intake tray.";

    /// <summary>The impatience card: the first time someone waiting on Ace gets frustrated.</summary>
    // 13 Oct 2026 (Systems): from behind the bench, E reaches a frustrated customer at a repair-bar stool over the counter, so the
    // card no longer says "walk over". "In reach" had no visible distance (the reviewers): at the counter, then. PLACEHOLDER COPY.
    public static string ReassureWords(string name) => $"{name} is getting impatient. At the counter, {E} Reassure.";

    /// <summary>
    /// The impatience card at the bench (checklist v2 §5.6): at the bench E takes the device away and A uses the tool, so it
    /// says to step back first, in the bench's keys (as icons): "Grace is getting impatient. [Circle] Step back, then [Cross]."
    /// 8 words with the icons (they aren't words), the alert's budget is 12 (WordBudget.BenchAlert). PLACEHOLDER COPY.
    /// </summary>
    public static string BenchReassureWords(string name) =>
        $"{name} is getting impatient. {ControlHints.Cancel} Step back, then {ControlHints.Interact}.";

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
    // seconds, until he moves. (At night the night's own "You" is UiPins' to keep: HideSelf leaves it.)
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

    // THE LOOK (playtest 7): one dark plate, the skin's band (the night's notes and speech boxes, the one style that read in
    // every photo), with the amber bar down its left; the title in amber, the step in the band's cream, bold. In full: the
    // title (20 px) over the step (26 px), and a second, smaller line under it when there is one; the line: the step alone
    // at 26 px, as wide as it needs. Top left under the tabs by day, the same place at night. (It was the phone's paper with
    // a green title at 21 px, settling to 18 px: the smallest, palest type on the screen, the reviewers.)
    // CHECKLIST V2 (14 Oct 2026, §5.6): no guide card at the bench. The bench's own card (BenchCards: her words, the tool's
    // icon big with its verb) and the glow on the next thing to touch are the guide there; the Day 1 card said the step a
    // fourth time ("Take the cloth (click it) to clean the smudge."). Only the impatience card shows at the bench, in the
    // bench's own keys (at the bench E takes the device away and A uses the tool) and with no title (its words say it), at
    // the bottom middle 26 px up (the stage's zone U6: the prompt line it used to sit over is gone).
    private static readonly string TitleColour = UiSkin.HexOf(UiSkin.Gold);
    private static readonly string AlsoColour = UiSkin.HexOf(UiSkin.BandFaint);
    private const float Below = 16f;
    private const float CardWidth = 560f, BenchCardWidth = 580f, CompactWidth = 800f;
    // 20 Oct (Bench 6 on his Mac): at the bench the one-line card is the impatience alert, and it lives in the stage's zone U6
    // (580 px across the top middle, BenchStageContract); in Baloo 2 at 26 px the line ran 658 px, so there it wraps instead
    // (the full card at the bench is 580 too, and doesn't grow when it pulses).
    private const float BenchAlertWidth = 580f;
    private const float BenchCardBottom = 26f;   // the stage's zone U6 (966-1054 px down at 1080p)
    /// <summary>The step's size (the full card's and the line's), the title's, and the second line's (reference pixels).</summary>
    public const float FullSize = 26f, CompactSize = 26f, TitleSize = 20f, AlsoSize = 20f;
    private const float PadLeft = 26f, PadRight = 18f, PadFull = 12f, PadCompact = 9f;
    /// <summary>The change pulse: 1.06 to 1 over this long, with the tick.</summary>
    public const float ChangePulseSeconds = .25f;

    private float WantedY() => -(TicketRailUI.BottomEdge > 0f ? TicketRailUI.BottomEdge + Below : 24f);

    private void Present(float now)
    {
        bool bench = inspector != null && inspector.IsHoldingItem;
        DayOneHintTimer.Look look;
        string title, step, also, line;
        if (impatient != null)
        {
            look = DayOneHintTimer.Look.Full;
            title = bench ? "" : ImpatientTitle;
            step = bench ? BenchReassureWords(impatient.CustomerName) : impatientText;
            also = "";
        }
        else if (guiding && !bench)
        {
            look = fallback ? DayOneHintTimer.Look.Compact : toast.LookAt(now);
            title = cardTitle;
            step = cardStep;
            also = cardAlso;
        }
        else
        {
            look = DayOneHintTimer.Look.Hidden;
            title = step = also = "";
        }
        // The line: the step, or the first card's shorter words once it has settled.
        line = look == DayOneHintTimer.Look.Compact && cardNow.Length > 0 ? cardNow : step;
        Draw(look, title, step, also, line, bench, false, now);
    }

    // The night's line (NightCycle.NowLine), hidden while a night card, the phone or a scene has the screen.
    private void PresentNight(float now)
    {
        string line = NightCycle.NowLine;
        bool blocked = PausePhone.Paused || NightCycle.Instance == null || NightCycle.Instance.Now != NightCycle.Phase.Night
            || (conversation != null && conversation.InConversation) || sourceText == null || !sourceText.gameObject.activeInHierarchy;
        if (blocked || string.IsNullOrEmpty(line))
        {
            Show(false);
            GuidePin(false);
            return;
        }
        Draw(DayOneHintTimer.Look.Compact, "", line, "", line, false, true, now);
    }

    private bool shownNight;

    private void Draw(DayOneHintTimer.Look look, string title, string step, string also, string line, bool bench, bool night, float now)
    {
        if (look == DayOneHintTimer.Look.Hidden || string.IsNullOrEmpty(step))
        {
            Show(false);
            GuidePin(false);
            return;
        }
        if (panel == null && !CreatePanel()) return;

        // Built only when a word, the look or the place changes (nothing is allocated on the frames between).
        if (shownText == null || look != shownLook || bench != shownAtBench || step != shownStep || title != shownTitle || also != shownAlso || line != shownLine)
        {
            // The step bold (it instructs: "your text needs to be bolder"); the title bold in amber; the second line plain.
            string text = look == DayOneHintTimer.Look.Compact ? $"<b>{line}</b>"
                : (title.Length > 0 ? $"<size={TitleSize}><color={TitleColour}><b>{title}</b></color></size>\n" : "") + $"<b>{step}</b>"
                  + (also.Length > 0 ? $"\n<size={AlsoSize}><color={AlsoColour}>{also}</color></size>" : "");
            Layout(text, look, bench);
            // The line changed: one pulse and a soft tick (not for the same words drawn again in another look).
            if (line != shownLine && !string.IsNullOrEmpty(shownLine) || shownText == null)
            {
                changedAt = now;
                Sfx.Play2D(ChangeCue);
            }
            shownText = text;
            shownLook = look;
            shownAtBench = bench;
            shownStep = step;
            shownTitle = title;
            shownAlso = also;
            shownLine = line;
            shownNight = night;
        }
        Show(true);
        Follow();
        // Stuck: back in full with one pulse (no sound). Changed: one pulse with the tick.
        float stuck = impatient == null && !fallback && !night ? toast.PulseAt(now) : 0f;
        float change = Mathf.Clamp01(1f - (now - changedAt) / ChangePulseSeconds);
        float pulse = Mathf.Max(stuck, change);
        // At the bench the card is the impatience alert in the stage's zone U6 (580 px wide): it pulses by its edge's colour
        // only, never by size, so it never grows out of its zone over the work (20 Oct, Bench 6).
        float s = bench ? 1f : 1f + .06f * pulse;
        panelRect.localScale = new Vector3(s, s, 1f);
        if (edge != null) edge.color = Color.Lerp(UiSkin.Gold, UiSkin.Paper, stuck);
        GuidePin(!night);
    }

    // The card's size for its words: full, the title and the step in a 560 px card (580 at the bench), as tall as they
    // need; the line, the step alone, as wide as it needs up to 800 px (a second line of words when it must).
    private void Layout(string text, DayOneHintTimer.Look look, bool bench)
    {
        bool compact = look == DayOneHintTimer.Look.Compact;
        hint.enableAutoSizing = false;
        hint.fontSize = compact ? CompactSize : FullSize;
        hint.text = text;
        float most = compact ? (bench ? BenchAlertWidth : CompactWidth) : bench ? BenchCardWidth : CardWidth;
        float pad = compact ? PadCompact : PadFull;
        Vector2 words = hint.GetPreferredValues(text, most - PadLeft - PadRight, 0f);
        float width = compact ? Mathf.Min(most, Mathf.Ceil(words.x) + PadLeft + PadRight + 2f) : most;
        float height = Mathf.Max(compact ? 46f : 72f, Mathf.Ceil(words.y) + 2f * pad);
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
        panel = new GameObject("Now line (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        panelRect = rect;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(UiSkin.Margin, WantedY());
        rect.sizeDelta = new Vector2(CardWidth, 104f);
        // The skin's band: the dark plate the night's notes and speech boxes sit on.
        UiSkin.Paint(rect, UiSkin.Band, UiSkin.Radius);
        var shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .35f);
        shadow.effectDistance = new Vector2(0f, -3f);
        // The amber bar down the plate's left edge (paper for the moment of the stuck card's one pulse).
        edge = UiSkin.Paint(new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>(), UiSkin.Gold, 3f);
        RectTransform edgeRect = edge.rectTransform;
        edgeRect.SetParent(rect, false);
        edgeRect.anchorMin = new Vector2(0f, 0f);
        edgeRect.anchorMax = new Vector2(0f, 1f);
        edgeRect.pivot = new Vector2(0f, .5f);
        edgeRect.offsetMin = new Vector2(8f, 9f);
        edgeRect.offsetMax = new Vector2(13f, -9f);

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
        hint.color = UiSkin.BandText;
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        hint.raycastTarget = false;
        placedAtBench = false;
        panel.SetActive(false);
        return true;
    }

    private void Show(bool visible)
    {
        if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
        if (!visible) { shownText = null; shownLine = ""; }
    }

    private void OnDisable()
    {
        toast.Dismiss();
        Show(false);
        GuidePin(false);
        SelfPin(false);
        CarryingToBench = null;
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
