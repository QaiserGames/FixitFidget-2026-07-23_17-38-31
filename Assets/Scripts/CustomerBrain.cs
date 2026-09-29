using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class CustomerBrain : MonoBehaviour
{
    // Settling and Waiting replace the old WaitingForService. The important
    // change is that the counter slot is released the instant a job is
    // accepted — the queue is now only ever the people who haven't been HEARD
    // yet, not everyone in the building.
    public enum State { WalkingToCounter, WaitingInQueue, Settling, Waiting, Speaking, Leaving }

    // TUNING, 2026-08-27. Was 15s. Three logged days showed queued customers
    // surviving ~18s of queue patience while an accepted job kept the player
    // away from the counter for ~66s — a 3.5x mismatch, and the reason 32 of 55
    // arrivals stormed out before ever being spoken to. 40s lets someone in the
    // queue survive one full repair cycle.
    //
    
    [SerializeField] private float queuePatience = 40f;
    [SerializeField] private float servicePatience = 45f;
    [SerializeField] private float maxTipFraction = 0.6f;

    [Header("Dialogue pacing")]
    [SerializeField] private float wordsPerSecond = 3f;
    [SerializeField] private float charactersPerSecond = 45f;
    [SerializeField] private float lineHoldTime = 2.5f;

    [Header("Reassurance")]
    [SerializeField] private float reassureAmount = 0.3f;
    [SerializeField] private float reassureCooldown = 12f;
    [SerializeField] private float reassureFalloff = 0.6f;
    [SerializeField] private float reassureTipCost = 0.15f;
    [SerializeField] private int reassureMaxUses = 3;

    [Header("Movement")]
    [Tooltip("A beat of acknowledgement before they turn and walk off, so the " +
             "'Interact' animation isn't cut short the instant you accept.")]
    [SerializeField] private float reactionTime = 0.6f;

    [Tooltip("Every spot was taken when they were ready to move. How often to " +
             "look again. They stand near the counter until one frees up.")]
    [SerializeField] private float spotRetryInterval = 1f;

    [Header("Crowding")]

    // THE DEADLOCK. Two agents with the same avoidance priority mirror each
    // other's dodge exactly — both step left, collide, both step right,
    // collide — and grind face to face until closing time. Unity's RVO has no
    // tie-break when priorities match. Randomising means somebody always
    // yields.
    //
    // Unity's convention is backwards from what you'd guess: LOWER number =
    // HIGHER priority. 0 barges through everyone, 99 gets barged.
    //
    // HOW they walk (turning, the walk clip, stalls and how to recover from
    // them) lives in NpcLocomotion since pass 1; this brain only says where
    // to go, how close counts, and how pushy to be on the way.
    [Tooltip("Each customer rolls an avoidance priority in this range so two " +
             "of them never mirror each other into a standoff. Lower = pushier.")]
    [SerializeField] private int movingPriorityMin = 30;
    [SerializeField] private int movingPriorityMax = 60;

    [Tooltip("Priority while walking away from the counter to their seat. " +
             "Stationary agents use priority zero, so " +
             "someone who's just been served goes around the people still " +
             "waiting instead of shouldering through them.")]
    [SerializeField] private int leavingCounterPriority = 95;

    [Tooltip("How far they back away from the counter before turning for their " +
             "seat.\n\nWithout this their route to a table runs ALONG the " +
             "counter, straight through everyone else still queueing — which is " +
             "why serving the person on the right used to shove the other two. " +
             "Set to 0 to walk straight at the seat like before.")]
    [SerializeField] private float counterStepBack = 1.2f;

    [Tooltip("Leaving the counter they hand over their device first (the Interact gesture) and only then turn " +
             "away: seconds after the job is taken. Turning while the arm is still out read as a spin.")]
    [SerializeField, Range(0f, 2f)] private float counterTurnAwayAfter = .95f;

    [Tooltip("Stepping back from the counter, this close to the step-back point (and still walking) the walk " +
             "carries straight on towards their spot instead of stopping there first, metres.")]
    [SerializeField, Range(0f, 1f)] private float stepBackCarryOn = .6f;

    [Tooltip("The step back goes this far to the side of straight back, towards the side their spot is on, metres. " +
             "Straight back was an about-turn that could go either way round, then a second turn for the seat " +
             "(pass 2c: the \"circle\" after ordering).")]
    [SerializeField, Range(0f, 1.2f)] private float counterStepAside = .8f;

    [Tooltip("Only step back when somebody is standing within this distance of the direct way to their spot " +
             "(over its first couple of metres); otherwise they walk straight there, metres.")]
    [SerializeField, Range(.3f, 1.2f)] private float counterWayClearance = .65f;

    [Tooltip("Longest they wait for the hand-over gesture (Interact) to finish before turning away anyway, seconds " +
             "past the usual reaction beat.")]
    [SerializeField, Range(0f, 2f)] private float gestureWaitMax = .8f;

    [Tooltip("How close counts as arrived. The prefab ships at 1 m, which " +
             "parks people a metre from their own chair.")]
    [SerializeField] private float arriveDistance = 0.3f;

    [Tooltip("Within this of a chair's stand point, still walking, the seating takes over and the walk carries " +
             "on into the chair (no stop, no idle, no hover). Pass 2.")]
    [SerializeField, Range(0.3f, 1f)] private float sitHandoverDistance = 0.7f;

    [Header("Arrival")]

    [Tooltip("How far into the room they wander before joining the queue, and " +
             "how wide of the direct line. Everyone spawning at one point and " +
             "walking one straight line to one slot is what makes arrivals look " +
             "like a school dinner queue.")]
    [SerializeField] private float driftDistanceMin = 2f;
    [SerializeField] private float driftDistanceMax = 5f;
    [Range(0f, 90f)]
    [SerializeField] private float driftSpreadDegrees = 70f;

    [Tooltip("How long they stand and look around before heading to the counter.")]
    [SerializeField] private float driftPauseMin = 1f;
    [SerializeField] private float driftPauseMax = 3f;

    [Header("The drink track")]

    [Tooltip("How long after settling before someone waiting on a repair asks " +
             "for a coffee. Deliberately AFTER they sit, not at the counter — " +
             "landing mid-teardown is what makes the café compete for your hands.")]
    [SerializeField] private float orderDelayMin = 4f;
    [SerializeField] private float orderDelayMax = 8f;

    [Tooltip("Patience given back the moment they ORDER, as a fraction of max. " +
             "They've decided to settle in. FIXED rather than proportional: a " +
             "proportional top-up would rescue the angriest customers hardest " +
             "and make 'let him order' better than 'serve him fast'.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float orderTopUp = 0.10f;

    [Tooltip("Patience given back when you actually hand it over. The visible " +
             "receipt — the real reward is the drain multiplier below.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float serveBump = 0.08f;

    [Tooltip("Patience given back when you hand back a REPAIR to someone who " +
             "still has a drink coming. Same idea as serveBump, on the half " +
             "that never had one — see the note in CompleteJob.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float handbackBump = 0.08f;

    [Tooltip("How long the drink keeps them happy at the Drinking rate.")]
    [SerializeField] private float drinkingSeconds = 20f;

    [Tooltip("Drain while drinking, then afterwards. These MULTIPLY with the " +
             "waiting spot — a seated customer (0.6) with a fresh coffee (0.5) " +
             "drains at 0.3, which on a 45s meter is 150s of patience. That is " +
             "very probably too generous; expect to pull it down.")]
    [SerializeField] private float drinkingDrain = 0.5f;
    [SerializeField] private float satisfiedDrain = 0.8f;

    [Tooltip("Placeholder used only while DialogueSet.orderedDrink is empty, so " +
             "grey-box isn't silent. DELETE THE FALLBACK once real lines exist " +
             "— written content does not belong in code.")]
    [SerializeField] private string orderFallback = "Could I get a {drink} while I wait?";

    [Tooltip("Hard limit on walking to the exit. A customer who can't reach it " +
             "is never destroyed, and DayClock waits for the customer count to " +
             "hit zero — so one stuck body used to hang the day forever.")]
    [SerializeField] private float leaveTimeout = 20f;

    [Header("Shelf look")]
    [Tooltip("Devices land on the shelf at a slight angle rather than in " +
             "perfect unison. Seeded per device, so it never jumps around.")]
    [SerializeField] private float shelfYawJitter = 12f;
    [SerializeField] private float shelfOffsetJitter = 0.02f;

    [Header("Presence")]
    [SerializeField] private float conversationDrainMultiplier = 0.1f;
    [SerializeField] private float presenceDrainMultiplier = 0.2f;

    [Header("Attention (pass 1b)")]
    [Tooltip("At the counter: how far away Ace can be and still be looked at, metres.")]
    [SerializeField] private float counterLookRange = 4f;
    [Tooltip("Waiting: how far away Ace can be, carrying their order, before they follow him with their eyes, metres.")]
    [SerializeField] private float deliveryNoticeRange = 5f;
    [Tooltip("Waiting and standing: within this distance a customer whose order is coming turns to face Ace, metres.")]
    [SerializeField] private float deliveryTurnRange = 2.6f;
    [Tooltip("Where Ace's eyes are above his transform (the capsule's centre), metres.")]
    [SerializeField] private float aceEyeOffset = 0.65f;
    [Tooltip("Queue variation: how far a customer stands off the slot sideways, metres (either way).")]
    [SerializeField] private float queueSideVariation = 0.18f;
    [Tooltip("Queue variation: how far a customer stands off the slot along it, metres (mostly back).")]
    [SerializeField] private float queueDepthVariation = 0.14f;
    [Tooltip("Queue variation: how far off the slot's facing a customer stands, degrees (either way).")]
    [SerializeField] private float queueYawVariation = 14f;

    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private CustomerIdentity identity;
    [SerializeField] private Transform lookTarget;

    [Tooltip("Optional. Floats their job number above them while they wait, so " +
             "you can find whose device you're carrying. Same colour as the ticket.")]
    [SerializeField] private JobMarker waitingBadge;

    private State state;
    private NavMeshAgent agent;
    private Animator animator;
    // How the body gets where this brain sends it (turning, walk clip, stalls).
    private NpcLocomotion locomotion;
    // Sits them on a chair when their waiting spot is a table seat (optional).
    private NpcSeating seating;
    private CounterQueue queue;
    private Transform exitPoint;
    private PlayerInteractor player;
    private PlayerCarry playerCarry;
    private float playerSearchedAt = -10f;
    private CustomerStoryteller storyteller;
    // Head look (counter, delivery) and the personal way of standing in the queue.
    private NpcLookAt lookAt;
    // The life layer (pass 2): movement profile, ambient glances and gestures.
    // The brain's own attention to Ace goes through it too, and always wins.
    private NpcSocial social;
    // Body language from the Mixamo clips (greetings, thanks, frustration...), when
    // they are in the project; see ReactOr. Optional.
    private NpcBeats beats;
    private float queueSide, queueDepth, queueYaw, nextQueueShift;
    // Delivery acknowledgement: is Ace bringing my order, and have I turned to him for it.
    private bool orderComing, turnedForDelivery;
    private float orderCheckAt, attentionUntil;

    private float patienceLeft;
    private float speakTimer;
    private float bubbleTimer;
    private float reassureReadyAt;
    private int reassureUses;
    private int slotIndex = -1;
    private bool aceAtMyCounter, aceServingMe, attentiveLooking;
    private float aceDistanceSample, aceDistanceSampleAt, queueLookFrom, queueLookUntil, queueLookCooldownUntil, attentiveSwitchAt;

    // Where they went once you took their job.
    private WaitingSpot waitingSpot;

    // Movement is deferred by reactionTime so they react before they turn away.
    private Vector3 pendingDestination;
    private NpcLocomotion.Move pendingMove;
    private bool hasPendingDestination;
    private float moveAllowedAt;

    private Job record;
    private JobBase activeJob;

    private bool intakeGiven;
    private bool departHappy = true;

    // ---------- why they left, and what happened while they were here ----------
    //
    // Depart() is the single exit for every customer, but by the time it runs
    // the reason is gone — state has already moved on and the decision that
    // caused it happened frames earlier. So each exit path stamps its reason
    // here on the way past, and Depart reads it.
    //
    // Defaulting to StormedOutWaiting rather than Declined is deliberate: if a
    // path is ever added that forgets to stamp, the day should over-report
    // failures, not quietly hide them.
    private LostReason lossReason = LostReason.StormedOutWaiting;

    // Everything DayLog needs about this visit. Cheap to keep, and it means the
    // log never has to reach into a half-destroyed object at exit time.
    private float arrivedAt;
    private bool  wasAccepted;
    private bool  wasServed;
    private int   paidBase;
    private int   paidTip;
    private JobGrade lastGrade = JobGrade.Rejected;
    private bool repairReturned;
    internal bool HasReturnedRepair => repairReturned;
    private string intakeLine = "";
    private float repairStartedAt = -1f;

    // Remembered rather than read off waitingSpot, because Depart releases the
    // spot and nulls the reference BEFORE the log call — so asking the live
    // spot would report blank for every single customer. Where they waited is
    // one of the more interesting columns (did the seated ones survive and the
    // loiterers storm out?), so it's worth a field.
    private WaitingSpot.SpotKind? lastWaitKind;

    public float ArrivedAt => arrivedAt;
    public bool  WasAccepted => wasAccepted;
    public LostReason Loss => lossReason;
    public WaitingSpot.SpotKind? WaitKind => lastWaitKind;

    // ---------- conversation ownership ----------
    //
    // THE CONTRACT: while a conversation is open, this customer's body belongs
    // to the ConversationController. Nothing may move them, release their
    // counter slot, or change their state until the panel closes.
    //
    // Before this existed, AcceptJob() sent them walking after `reactionTime`
    // (0.6s) while CloseWith() held the panel open for `line.Length/30 +
    // closingPause` (~2.5s). Two timers, never introduced to each other — so
    // they walked away mid-sentence with the conversation camera chasing them.

    private ConversationController conversation;
    private System.Action pendingHandoff;

    // Accepted or refused. Guards against a second decision landing during the
    // closing beat, which would spawn a second device and burn a job number.
    private bool decided;
    private bool jobAccepted;

    // Set when they wanted a waiting spot and the floor was full.
    private float retryClaimAt;

    // False until they've done their look-around on the way in.
    private bool driftDone;

    // Where they're actually headed once they've stepped clear of the counter,
    // and the clear-of-the-counter point itself.
    private Vector3 settleDestination;
    private Vector3 stepBackTo;
    private bool hasStepBack;

    // Set on accept, cleared the moment they actually leave the counter slot.
    private bool releaseSlotOnMove;

    // How many spots they have given up on this visit (NpcLocomotion reports
    // the give-up; this brain decides what to do with it).
    private int   settleAttempts;

    // Backstop for the walk to the door.
    private float leaveDeadline = float.MaxValue;

    // When they asked out loud for a drink. The espresso machine serves in
    // this order — see EspressoMachine.PendingOrders.
    public float DrinkOrderedAt { get; private set; }

    // Drink orders: accepted, but nothing has been made yet.
    private bool drinkOrdered;
    private bool drinkStarted;

    // ---------- the drink WISH ----------
    //
    // THE POINT OF THE WHOLE PASS. RollJob flips a coin — 40% drink, 60% repair
    // — so a customer was never both, and "ordering a coffee while they wait
    // for their repair" (the sentence the project is built on) could not
    // happen. This is the second, parallel track.
    //
    // It lives here rather than on Job because Job.kind is deliberately
    // exclusive and Record.drink being null for a repair customer is what keeps
    // the rest of the code honest. WantedDrink is the single place that knows
    // how to answer "what would they like?" for both kinds of customer.

    private DrinkDefinition drinkWish;   // rolled at spawn; may be null
    private float drinkAskAt;            // when they'll speak up. 0 = not scheduled
    private float drinkServedAt;         // when it reached their hands

    public DrinkDefinition WantedDrink =>
        record != null && record.kind == JobKind.Drink ? record.drink : drinkWish;

    private string currentLine = "";
    private Coroutine revealRoutine;
    private float queueMax;
    private float serviceMax;

    public CustomerIdentity Identity => identity;
    public Transform LookTarget => lookTarget;
    public string CustomerName => identity != null ? identity.DisplayName : "Customer";
    public Job Record => record;
    public JobBase ActiveJob => activeJob;
    internal bool SpeechBusy => bubbleTimer > 0f || InConversation;
    internal bool CanTellStory => isActiveAndEnabled && identity != null && identity.Profile != null
        && identity.Profile.storyteller && state == State.Waiting && !InConversation
        && activeJob is RepairJob && !activeJob.IsComplete
        && Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);
    public bool CanRequestFocus => storyteller != null && storyteller.CanRequestFocus && !JobNeedsAttention;
    public HumanFault HumanConversation => activeJob != null && record != null && record.faultType == FaultType.Human
        ? activeJob.GetComponentInChildren<HumanFault>() : null;
    public bool IsCounterRepair => record != null && record.kind == JobKind.Repair && record.faultType == FaultType.Human;
    public bool CanFixAtCounter => IsCounterRepair && jobAccepted && !IsLeaving && HumanConversation != null
        && (HumanConversation.CanFix(this) || HumanConversation.Finished)
        && Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);
    public int JobNumber { get; private set; }
    public Color JobColor { get; private set; } = Color.white;

    public bool HasJob => activeJob != null || drinkOrdered;
    public string JobCardText => record != null ? record.Detail : "";
    public float PatienceFraction => Mathf.Clamp01(patienceLeft / CurrentMax);
    /// <summary>
    /// Waiting on Ace with the patience bar showing (queueing, walking to a spot,
    /// waiting there), and not talking to him: when the body may show impatience (NpcBeats).
    /// </summary>
    public bool ShowsPatience => (state == State.WaitingInQueue || IsWaiting) && !InConversation;
    public int SlotIndex => slotIndex;

    // Anywhere between "you took the job" and "you finished it" — walking to
    // their spot counts, they're already waiting on you.
    private bool IsWaiting => state == State.Settling || state == State.Waiting;

    // Done with you — walking to the door or saying their last line. DayClock
    // uses this to tell "still serving people" apart from "nobody's leaving".
    public bool IsLeaving => state == State.Leaving || state == State.Speaking;

    // Kept for TicketRailUI, which asks whether there's live work for them.
    //
    // `jobAccepted` is in here so the ticket appears the moment you press E,
    // rather than two seconds later when the panel closes and they start
    // walking. A device sitting on the shelf with no ticket on the rail is the
    // bookkeeping lying to you. Declines can't leak a ticket — TicketRailUI
    // also requires HasJob, and a refused customer has neither a device nor a
    // drink order.
    public bool InService => IsWaiting || jobAccepted;

    private float CurrentMax => IsWaiting || (IsCounterRepair && jobAccepted) ? serviceMax : queueMax;

    // ---------- the intake beat ----------

    public bool CanHearIntake => state == State.WaitingInQueue && !intakeGiven && !decided;
    public bool CanDecide => state == State.WaitingInQueue && intakeGiven && !decided;
    public bool CanRefuse => CanDecide;

    // Nowhere to put their device. You physically cannot take this job until
    // you've cleared the shelf.
    public bool ShelfFull =>
        CanDecide && record != null && record.kind == JobKind.Repair && !IsCounterRepair &&
        (IntakeShelf.Instance == null || !IntakeShelf.Instance.HasRoom);

    // A drink order can only be accepted if we can actually make it.
    public bool CanAcceptJob
    {
        get
        {
            if (!CanDecide) return false;
            // Fail before reserving the visit if a Human prefab has no physical task.
            if (IsCounterRepair)
            {
                var definition = record.devicePrefab != null ? record.devicePrefab.GetComponent<DeviceDefinition>() : null;
                var fault = definition != null ? definition.GetFault(record.faultIndex) : null;
                if (fault == null || fault.enableObjects == null || record.devicePrefab.GetComponent<JobBase>() == null) return false;
                foreach (var obj in fault.enableObjects)
                    if (obj != null && obj.GetComponentInChildren<HumanFault>(true) != null) return true;
                return false;
            }

            if (record != null && record.kind == JobKind.Drink)
                return ShopInventory.Instance != null && ShopInventory.Instance.CanMake(record.drink);

            return !ShelfFull;
        }
    }

    // Told them we're out of stock — a different decline, and not our fault.
    public bool OutOfStock =>
        CanDecide && record != null && record.kind == JobKind.Drink &&
        (ShopInventory.Instance == null || !ShopInventory.Instance.CanMake(record.drink));

    // Now the literal truth rather than an inference. The old version stayed
    // true forever once intake had been heard, so pressing F to step away left
    // them draining at the conversation rate (0.1x) for the rest of the queue
    // wait — you could park someone indefinitely by talking to them once.
    private bool InConversation => conversation != null;

    // ---------- café ----------

    // Waiting on a drink that doesn't physically exist yet.
    //
    // THE BUG THIS FIXES: this used to read `drinkOrdered && !drinkStarted`,
    // and drinkStarted was a LATCH — set the moment you loaded a cup, cleared
    // only when the drink reached their hands. Nothing ever checked that the
    // cup still existed.
    //
    // So any cup that was started and never delivered — abandoned on a shelf,
    // destroyed, or handed to someone else who wanted the same thing (which
    // CanReceiveDrink explicitly allows) — left that customer latched shut
    // forever. Their ticket stayed on the rail, because the rail reads
    // drinkOrdered. The espresso machine skipped them, because it read
    // AwaitingDrink. The player was shown an order they could not fulfil for
    // the rest of the day, and the customer eventually stormed out over a
    // coffee the game had refused to let anyone make.
    //
    // The flag is now advisory and physical reality is authoritative: you're
    // awaiting a drink if you ordered one and no cup is bound to you. Every
    // failure path self-heals, including ones we haven't thought of — lose the
    // cup, and the order simply reappears at the machine.
    public bool AwaitingDrink => drinkOrdered && !DrinkJob.ExistsFor(this);

    // Kept so the machine can still say what it's doing, but nothing gates on
    // it any more.
    public bool DrinkStarted => drinkStarted;

    /// <summary>They've asked for a drink, whether or not it's been made yet.</summary>
    public bool HasDrinkOrder => drinkOrdered;

    public void MarkDrinkStarted() => drinkStarted = true;

    // Delivery chooses the matching item from either physical hand. Reading a
    // prompt must never change which hand the player uses at a work station.
    private PlayerCarry DeliveryCarry
    {
        get
        {
            PlayerInteractor ace = Player;
            if (ace != null)
            {
                // `player` can be handed in from outside (checks); derive the hands from him.
                if (playerCarry == null || playerCarry.gameObject != ace.gameObject) playerCarry = ace.GetComponent<PlayerCarry>();
                if (playerCarry != null) return playerCarry;
            }
            return FindAnyObjectByType<PlayerCarry>();
        }
    }

    private DrinkJob FindServeableDrink(PlayerCarry carry)
    {
        if (carry == null) return null;
        for (int i = 0; i < carry.Count; i++)
            if (carry.GetItem(i) is DrinkJob cup && cup.Drink == WantedDrink && cup.CanHandBack)
                return cup;
        return null;
    }

    private static bool SelectDeliveryItem(PlayerCarry carry, JobBase item)
    {
        if (carry == null || item == null) return false;
        for (int side = 0; side < carry.Capacity; side++)
            if (carry.GetHandItem(side) == item) return carry.SelectHand(side);
        return false;
    }

    // Any matching drink will do, including a cup whose original owner left.
    public bool CanReceiveDrink
    {
        get
        {
            if (!drinkOrdered || !IsWaiting) return false;

            return FindServeableDrink(DeliveryCarry) != null;
        }
    }

    public bool HasColdDrinkForOrder
    {
        get
        {
            if (!drinkOrdered || !IsWaiting) return false;
            var carry = DeliveryCarry;
            if (carry == null || FindServeableDrink(carry) != null) return false;
            for (int i = 0; i < carry.Count; i++)
                if (carry.GetItem(i) is DrinkJob cup && cup.Drink == WantedDrink
                    && cup.FreshnessStage == DrinkFreshness.Stage.Cold) return true;
            return false;
        }
    }

    // ---------- what the tab says ----------
    //
    // One customer, one card. The rail asks for this every frame rather than
    // being told once at Bind(), because a drink can be added to an existing
    // tab long after the ticket was created.
    public string TabLines
    {
        get
        {
            if (record == null) return "";

            string s;

            if (record.kind == JobKind.Drink)
            {
                s = record.Subject + "\n" + record.Detail;
            }
            else
            {
                s = record.deviceName + "\n" + record.faultDescription;

                // Only once they've actually asked. A wish nobody has voiced
                // must not appear on the tab — that would be the readout
                // telling you something the character hasn't.
                if (drinkOrdered && WantedDrink != null)
                    s += "\n+ " + WantedDrink.drinkName;
            }

            return s;
        }
    }

    // ---------- handback ----------

    // Handing back is now a delivery, not a counter transaction: you have to be
    // holding their device and standing in front of them. Same shape as
    // CanReceiveDrink, so repairs and drinks are one verb.
    public bool JobReady
    {
        get
        {
            // Gate is REASSEMBLY, not perfection. You may hand back a device
            // with grime still in it — you'll just be paid Passable for it.
            // That trade is the decision the clock is supposed to force.
            if (!IsWaiting || activeJob == null || !activeJob.CanHandBack) return false;

            PlayerCarry carry = DeliveryCarry;
            return carry != null && carry.Contains(activeJob);
        }
    }

    // Fixed, but you're not carrying it — the prompt nudges you to go get it.
    // Still uses IsComplete (= Perfect) deliberately: the nudge should only
    // fire when it's genuinely finished, not when it's merely handable.
    public bool JobFixedButAway =>
        IsWaiting && activeJob != null && activeJob.IsComplete && !JobReady;

    // What they'd be handed right now, for the prompt. The player sees the
    // grade BEFORE committing — without that it's a punishment, not a choice.
    public JobGrade PendingGrade =>
        activeJob != null ? activeJob.Grade : JobGrade.Rejected;

    public bool CanReassure =>
        IsWaiting
        && Time.time >= reassureReadyAt
        && reassureUses < reassureMaxUses
        && patienceLeft < CurrentMax * 0.9f;

    // THE DEAD END THIS FIXES, found in play 2026-08-28.
    //
    // The repair was done and handed back. They stayed for the coffee they'd
    // ordered. The beans had run out. There was no verb for "sorry, we're out"
    // once someone was seated — that only existed at intake — so the player
    // stood and watched a customer they had already served and been paid by
    // drain to zero and storm off, with no action available in any direction.
    //
    // The architecture spec predicted this exact case and guessed it would be
    // fine: "they wait, drink never arrives, they just don't get the bonus. No
    // penalty. Revisit if it feels bad." It feels bad. This is the revisit.
    //
    // Deliberately narrow: only when the drink is genuinely unmakeable. It is
    // an apology for a shortage, not a way to cancel orders you'd rather not
    // fill.
    public bool CanApologiseForDrink
    {
        get
        {
            if (!IsWaiting || !drinkOrdered) return false;
            if (DrinkJob.ExistsFor(this)) return false;      // it's coming — wait for it

            DrinkDefinition want = WantedDrink;
            if (want == null) return false;

            return ShopInventory.Instance == null || !ShopInventory.Instance.CanMake(want);
        }
    }

    /// <summary>The drink they're waiting on, for the prompt.</summary>
    public string WantedDrinkName => WantedDrink != null ? WantedDrink.drinkName : "drink";

    public bool JobNeedsAttention
    {
        get
        {
            HoldCallJob call = activeJob as HoldCallJob;
            return call != null && (call.CurrentPhase == HoldCallRun.State.NeedsDialing ||
                                    call.CurrentPhase == HoldCallRun.State.Ringing);
        }
    }

    // 1 until they've been handed a drink, then calm, then merely content.
    private float DrinkRate
    {
        get
        {
            if (drinkServedAt <= 0f) return 1f;
            return (Time.time - drinkServedAt) <= drinkingSeconds ? drinkingDrain : satisfiedDrain;
        }
    }

    private float DrainRate
    {
        get
        {
            if (InConversation) return conversationDrainMultiplier;

            // Where they chose to wait changes how fast they sour. Sitting is
            // calm, loitering by the counter is not. A coffee in their hands
            // multiplies on top of that — THIS is the answer to "why bother
            // with the café": the radio needs 90 seconds you don't have, so you
            // buy some of them back.
            float spotRate = (waitingSpot != null ? waitingSpot.DrainMultiplier : 1f) * DrinkRate;

            HoldCallJob call = activeJob as HoldCallJob;
            if (call == null || !call.WantsPlayerPresent) return spotRate;

            if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
            if (player == null || !player.IsAtStation) return spotRate;

            Interactable f = player.Focused;
            if (f == null) return spotRate;

            bool lookingAtMe = f.GetComponent<CustomerBrain>() == this;
            bool lookingAtMyPhone = f.GetComponentInParent<HoldCallJob>() == call;

            return (lookingAtMe || lookingAtMyPhone) ? spotRate * presenceDrainMultiplier : spotRate;
        }
    }

    // ---------- setup ----------

    public void Init(CounterQueue counterQueue, Transform exit, Job job,
                     DrinkDefinition wish = null)
    {
        queue = counterQueue;
        exitPoint = exit;
        record = job;

        arrivedAt = DayClock.Instance != null ? DayClock.Instance.SecondsIntoDay : 0f;

        // Rolled at spawn and kept quiet until they've settled. Deterministic
        // and simpler than deciding mid-wait — and indistinguishable from the
        // player's side, since they only ever find out when it's said out loud.
        drinkWish = job != null && job.kind == JobKind.Repair ? wish : null;

        agent = GetComponent<NavMeshAgent>();

        // GetComponentInChildren rather than GetComponent, so a model swapped
        // in as a child later still works without touching this again.
        animator = GetComponentInChildren<Animator>();
        seating = GetComponent<NpcSeating>();
        locomotion = GetComponent<NpcLocomotion>();
        if (locomotion == null) locomotion = gameObject.AddComponent<NpcLocomotion>();
        lookAt = GetComponent<NpcLookAt>();
        if (lookAt == null) lookAt = gameObject.AddComponent<NpcLookAt>();
        social = GetComponent<NpcSocial>();
        if (social == null) social = gameObject.AddComponent<NpcSocial>();
        beats = GetComponent<NpcBeats>();
        // Who they are as a mover: a regular's own profile, else a roll. Presentation only.
        NpcMovementProfile profile = social.Profile != null ? social.Profile : social.AssignProfile(identity);
        float looseness = profile != null ? profile.facingLooseness : 1f;

        // Nobody stands on the exact same centimetre facing the exact same way.
        queueSide = Random.Range(-queueSideVariation, queueSideVariation);
        queueDepth = Random.Range(-queueDepthVariation, queueDepthVariation * .35f);
        queueYaw = Random.Range(-queueYawVariation, queueYawVariation) * looseness;
        nextQueueShift = Time.time + Random.Range(6f, 12f);

        // Two multipliers, and they mean different things. The identity's is WHO
        // this person is — an Impatient customer is impatient on every day. The
        // day's is WHEN this is — day 1 is forgiving to everyone, because the
        // player is learning the shop rather than learning it's cruel.
        float mult = identity != null ? identity.PatienceMultiplier : 1f;
        float dayMult = DayClock.Instance != null ? DayClock.Instance.PatienceMultiplier : 1f;

        queueMax = queuePatience * mult * dayMult;
        serviceMax = servicePatience * mult * dayMult;

        // They know what they came in for, so dialogue can name it.
        if (identity != null && record != null)
        {
            identity.SetDevice(record.Subject);
            identity.SetFault(record.faultDescription);
            identity.SetStoryRequest(record);
        }

        HideBubble();
        if (waitingBadge != null) waitingBadge.Hide();

        storyteller = GetComponent<CustomerStoryteller>();
        if (identity != null && identity.Profile != null && identity.Profile.storyteller)
        {
            if (storyteller == null) storyteller = gameObject.AddComponent<CustomerStoryteller>();
            storyteller.Initialize(this);
        }
        else if (storyteller != null) storyteller.Initialize(this);

        slotIndex = queue.ClaimSlot(this);
        if (slotIndex < 0)
        {
            // CustomerSpawner now checks CounterQueue.HasFreeSlot before it
            // creates anyone, so this should be unreachable. Keeping it honest
            // rather than silent: if we ever do turn someone away at the door,
            // it counts against the day and says so in the Console.
            Debug.LogWarning($"[{CustomerName}] arrived with no free counter slot " +
                             $"and left immediately. The spawner should have held " +
                             $"them back — check CustomerSpawner.counterQueue is wired.", this);

            state = State.Leaving;
            leaveDeadline = Time.time + leaveTimeout;
            WalkToExit();
            return;
        }

        state = State.WalkingToCounter;
        if (social != null) social.Current = NpcSocial.Situation.Walking;
        NpcAttentionDirector.NotifyArrival(transform);

        // Veer into the room before heading for the counter. Not the full
        // Drifting state from GDD §4.5 — just enough that six arrivals don't
        // trace the same line to the same spot.
        Vector3 drift;
        if (PickDriftPoint(out drift))
        {
            locomotion.MoveTo(drift, Walk("drift"));
        }
        else
        {
            driftDone = true;
            locomotion.MoveTo(QueuePoint(slotIndex), Walk("slot"));
        }
    }

    // ---------- standing in the queue ----------
    //
    // The slot is where the queue puts them; this is where THEY stand: a hand's
    // width to one side, a little back, turned a few degrees - rolled once per
    // customer, and shifted slightly every so often (a weight shift). The queue
    // order and the slot logic do not change.
    private Vector3 QueuePoint(int index)
    {
        Transform slot = queue.SlotPoint(index);
        Vector3 point = slot.position + slot.right * queueSide + slot.forward * queueDepth;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, .5f, NavMesh.AllAreas)) point = hit.position;
        return point;
    }

    private Quaternion QueueFacing(int index) => queue.SlotPoint(index).rotation * Quaternion.Euler(0f, queueYaw, 0f);

    // ---------- how to walk each leg ----------
    //
    // The brain says where and how pushy; NpcLocomotion does the walking.

    // Into the room and to the counter: an ordinary walker.
    private NpcLocomotion.Move Walk(string purpose) =>
        NpcLocomotion.Move.To(arriveDistance, RollMovingPriority(), purpose);

    // Away from the counter to a spot: yield to the people still queueing (see
    // TryTakeWaitingSpot); the locomotion's ladder still pushes through if
    // politeness costs them too long.
    private NpcLocomotion.Move LeaveCounter(string purpose) =>
        NpcLocomotion.Move.To(arriveDistance, leavingCounterPriority, purpose);

    // The door is an area, not a point (CafeArrivals.DepartureRadius), so
    // several people can leave at once without fighting over one coordinate.
    private void WalkToExit()
    {
        if (exitPoint == null) return;
        locomotion.MoveTo(CafeArrivals.DepartureTarget(exitPoint.position), NpcLocomotion.Move.Exit(RollMovingPriority()));
    }

    // Somewhere INTO the shop, but off the direct line. Takes the bearing to
    // the counter and swings it wide, so they always make progress inward —
    // wandering back out of the door would look broken, not lifelike.
    private bool PickDriftPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (agent == null || !agent.isOnNavMesh) return false;

        Vector3 toCounter = queue.SlotPoint(slotIndex).position - transform.position;
        toCounter.y = 0f;
        if (toCounter.sqrMagnitude < 0.01f) return false;

        Vector3 dir = Quaternion.Euler(0f, Random.Range(-driftSpreadDegrees, driftSpreadDegrees), 0f)
                    * toCounter.normalized;

        Vector3 probe = transform.position + dir * Random.Range(driftDistanceMin, driftDistanceMax);

        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
        if (!CanReach(hit.position)) return false;

        point = hit.position;
        return true;
    }

    // Someone ahead of them left — shuffle up the line. Their device isn't
    // involved any more; it lives on the shelf.
    public void MoveToSlot(int newIndex)
    {
        slotIndex = newIndex;
        driftDone = true;   // the line moved — stop sightseeing and get in it

        locomotion.MoveTo(QueuePoint(slotIndex), Walk("slot"));
    }

    private void Update()
    {
        if (agent == null) return;

        UpdateAttention();

        if (InConversation)
        {
            // Follow-up diagnosis can begin while settling, not just at intake.
            // Keep normal conversation patience drain; the locomotion is paused
            // (OnConversationOpened) until the camera gives this customer back.
            patienceLeft -= Time.deltaTime * DrainRate;
            UpdateBar(CurrentMax, Color.green);
            if (patienceLeft <= 0f) StormOut();
            return;
        }

        // Held still for a beat after accepting, then released - but never
        // while the hand-over gesture is still playing: turning with the arm
        // still out was a swivel on still legs (pass 2c).
        if (hasPendingDestination && Time.time >= moveAllowedAt
            && (!GestureStillPlaying() || Time.time >= moveAllowedAt + gestureWaitMax))
        {
            hasPendingDestination = false;
            locomotion.MoveTo(pendingDestination, pendingMove);

            // THE COUNTER SLOT IS RELEASED HERE, NOT ON ACCEPT.
            //
            // BeginWaiting used to free it the instant the conversation closed
            // — while the customer was still standing in it, stopped, for the
            // reaction beat and however long it took to claim a waiting spot.
            // The slot was logically empty and physically occupied by a body
            // that cannot be pushed: a NavMeshAgent with isStopped = true is
            // immovable, avoidance can't shift it.
            //
            // So CounterQueue.ReleaseSlot would shuffle the next person into
            // that slot, they'd path to within arriveDistance (0.3 m) of a spot
            // someone was still standing in, and lean on them until the parked
            // customer finally walked off. That's the whole queue freezing until
            // one specific person leaves.
            //
            // Now the space is only declared free at the moment the body
            // actually starts moving out of it.
            if (releaseSlotOnMove)
            {
                releaseSlotOnMove = false;
                ReleaseCounterSlot();
            }
        }

        if (bubbleTimer > 0f)
        {
            bubbleTimer -= Time.deltaTime;
            if (bubbleTimer <= 0f)
            {
                currentLine = "";
                HideNow();
            }
        }

        switch (state)
        {
            case State.WalkingToCounter:
                if (!hasPendingDestination)
                {
                    if (locomotion.HasArrived)
                    {
                        if (!driftDone)
                        {
                            // Reached their look-around spot. Stand a moment,
                            // then go and queue. The pause is what sells it —
                            // walking through a curve at constant speed still
                            // reads as a conveyor belt.
                            driftDone = true;
                            locomotion.Park(null);
                            // A hurried person barely pauses; a relaxed one has a proper look round.
                            float urgency = social != null && social.Profile != null ? social.Profile.urgency : .5f;
                            float pause = Random.Range(driftPauseMin, driftPauseMax) * (1.35f - .7f * urgency);
                            MoveAfter(QueuePoint(slotIndex), Walk("slot"), pause);
                            // Taking the room in, with the whole body when the clips are there.
                            if (beats != null) beats.React(NpcBeats.Moment.LookAround, seconds: pause + .3f);
                            break;
                        }

                        state = State.WaitingInQueue;
                        patienceLeft = queueMax;
                        StopSteering();     // stop shoving whoever's in front
                    }
                    else if (!driftDone && locomotion.StuckStage >= 1)
                    {
                        // The look-around detour is not worth fighting for:
                        // aim straight at the counter instead.
                        driftDone = true;
                        locomotion.MoveTo(QueuePoint(slotIndex), Walk("slot"));
                    }
                    else if (locomotion.GaveUp)
                    {
                        // Re-pathing, stepping aside and pushing all failed.
                        // The room is genuinely impassable for them — let them
                        // give up and walk out, which at least ENDS, and say so
                        // loudly enough to be fixable.
                        Debug.LogWarning($"[{CustomerName}] couldn't reach the " +
                                         $"counter and gave up. Check for a " +
                                         $"gap narrower than the agent radius " +
                                         $"between the door and the counter.", this);
                        StormOut();
                    }
                }
                break;

            case State.WaitingInQueue:
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(CurrentMax, Color.green);
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.Settling:
                // Walking to their spot. Still waiting on you, so still draining.
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) { StormOut(); break; }

                if (!hasPendingDestination)
                {
                    if (hasStepBack && locomotion.IsMoving && locomotion.DistanceToGoal < stepBackCarryOn)
                    {
                        // Clear of the counter and still walking: carry straight on
                        // towards the seat. Stopping on the step-back point and
                        // turning there read as walk back - stop - spin - walk.
                        hasStepBack = false;
                        locomotion.MoveTo(settleDestination, LeaveCounter("spot"));
                    }
                    else if (locomotion.HasArrived)
                    {
                        if (hasStepBack)
                        {
                            // Clear of the counter. NOW turn for the seat, back
                            // at the polite priority whatever it took to get
                            // out of the slot.
                            hasStepBack = false;
                            locomotion.MoveTo(settleDestination, LeaveCounter("spot"));
                        }
                        else SettleHere();
                    }
                    else if (locomotion.GaveUp) GiveUpOnSpot();
                    else if (!hasStepBack && waitingSpot is TableSeat early && seating != null && seating.CanSit(early)
                             && locomotion.IsMoving && seating.ReadyToTakeOver(early, sitHandoverDistance))
                    {
                        // Close to the chair and still walking: the seating takes
                        // the last steps, so walk, turn and sit are one movement.
                        SettleHere(locomotion.Speed);
                    }
                }
                break;

            case State.Waiting:
                // Landed in Waiting without a spot — the floor was full when
                // they were ready to move. Keep asking.
                if (waitingSpot == null && jobAccepted && Time.time >= retryClaimAt)
                {
                    if (!TryTakeWaitingSpot()) retryClaimAt = Time.time + spotRetryInterval;
                }

                TickDrinkWish();

                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.Speaking:
                speakTimer -= Time.deltaTime;
                if (speakTimer <= 0f) Depart(departHappy);
                break;

            case State.Leaving:
                // Backstop first: if the door is unreachable this is the only
                // thing that ever ends this state, and DayClock is waiting on it.
                if (Time.time >= leaveDeadline)
                {
                    Debug.LogWarning($"[{CustomerName}] couldn't reach the exit in " +
                                     $"{leaveTimeout}s — removing them. Check the " +
                                     $"NavMesh between the shop floor and the door.", this);
                    Destroy(gameObject);
                    break;
                }

                // Don't vanish while still mid-sentence — let the line finish first.
                //
                // Out of the door they walk back to their car or home (CafeArrivals
                // strips this brain there, so from here on nothing counts them);
                // without CafeArrivals they vanish at the door as before.
                if (locomotion.HasArrived)
                {
                    if (bubbleTimer <= 0f && !CafeArrivals.TryDepart(gameObject)) Destroy(gameObject);
                }
                else if (locomotion.GaveUp && bubbleTimer <= 0f && exitPoint != null
                         && locomotion.DistanceToGoal <= CafeArrivals.DepartureRadius + 2f)
                {
                    // Boxed in within a couple of metres of the door: hand the
                    // walk over to the street from here. NpcJourney steers by
                    // itself and does not need the last metre of NavMesh.
                    if (!CafeArrivals.TryDepart(gameObject)) Destroy(gameObject);
                }
                break;
        }
    }

    // ---------- attention: looking at Ace ----------
    //
    // The head does the looking (NpcLookAt); the body turns only when the head
    // cannot reach, and only once, so nobody pivots after the player.
    //   Queue:    look at Ace while he is near the counter and in front of them;
    //             the customer he is talking to also turns to face him.
    //   Waiting:  when Ace comes over CARRYING THEIR ORDER (their device, or a
    //             cup of what they asked for), follow him with the eyes from a
    //             few metres, and - if standing - turn to face him when close.
    //             The hand-over itself keeps the gaze on him for a moment.
    //   Leaving:  nothing; they have somewhere to be.
    private PlayerInteractor Player
    {
        get
        {
            if (player == null && Time.time - playerSearchedAt > 2f)
            {
                playerSearchedAt = Time.time;
                player = FindAnyObjectByType<PlayerInteractor>();
                playerCarry = player != null ? player.GetComponent<PlayerCarry>() : null;
            }
            return player;
        }
    }

    private void UpdateAttention()
    {
        if (lookAt == null) return;
        PlayerInteractor ace = Player;
        bool seated = seating != null && seating.Busy;
        if (social != null) social.Current = SituationNow(seated);
        if (ace == null) { ClearAttention(); return; }
        Vector3 eyes = Vector3.up * aceEyeOffset;
        Vector3 toAce = ace.transform.position - transform.position;
        toAce.y = 0f;
        float distance = toAce.magnitude;
        float bearing = distance > .05f ? Vector3.Angle(transform.forward, toAce) : 0f;

        if (InConversation)
        {
            Attend(ace.transform, eyes, 1f);
            return;
        }

        switch (state)
        {
            case State.WaitingInQueue:
                QueueAttention(ace.transform, eyes, distance, bearing);
                ShiftWeightInQueue();
                break;

            case State.Settling:
            case State.Waiting:
                if (Time.time >= orderCheckAt)
                {
                    orderCheckAt = Time.time + .2f;
                    bool coming = jobAccepted && distance < deliveryNoticeRange && (JobReady || CanReceiveDrink);
                    if (!coming) turnedForDelivery = false;
                    // Ace is bringing their order: the phone goes away, eyes up.
                    else if (!orderComing && beats != null) beats.StopIdle();
                    orderComing = coming;
                }
                if (orderComing || Time.time < attentionUntil)
                {
                    Attend(ace.transform, eyes, 1f);
                    // Standing: turn to meet him once he is close. Seated: the head is enough.
                    if (orderComing && !seated && !turnedForDelivery && distance < deliveryTurnRange && state == State.Waiting)
                    {
                        turnedForDelivery = true;
                        locomotion.Face(FacingTowards(ace.transform.position));
                    }
                }
                else if (distance < 2.2f && bearing < 100f) Attend(ace.transform, eyes, .5f);   // someone walked up
                else ClearAttention();
                break;

            case State.Speaking:
                if (Time.time < attentionUntil || (distance < 3f && bearing < 110f)) Attend(ace.transform, eyes, 1f);
                else ClearAttention();
                break;

            default:
                ClearAttention();
                break;
        }
    }

    // Waiting at the counter to be heard. The one he is talking to has his
    // full attention (InConversation, above). The others give him a look when
    // he comes over to their part of the counter or steps up right in front of
    // them - a second or two, then back to their own thoughts, and NpcSocial's
    // queue beats take over (the menu board, the room, a neighbour, the odd
    // glance his way). Behind the counter all day, he used to be stared at by
    // the whole line for as long as they stood there.
    private void QueueAttention(Transform ace, Vector3 eyes, float distance, float bearing)
    {
        float now = Time.time;
        bool atMyCounter = distance < 1.7f && bearing < 100f;
        bool arrived = atMyCounter && !aceAtMyCounter;
        aceAtMyCounter = atMyCounter;

        // Next to be served, with him standing right across the counter: the
        // strongest attention in the line - eyes on him, with a short look away
        // now and then (the menu, the room) so it never becomes a stare.
        // Across the counter from the serving spot is ~1.5-1.9 m; the neighbouring
        // slot is ~42 degrees off, so it never counts as "me".
        bool servingMe = CanHearIntake && distance < 2.4f && bearing < 35f;
        if (servingMe)
        {
            // Ace has come over to serve them: whatever they were doing stops, and they
            // say hello (once a visit), unless they are past pleasantries.
            if (!aceServingMe && beats != null)
            {
                beats.StopIdle();
                if (PatienceFraction >= .3f) beats.React(NpcBeats.Moment.Greet);
            }
            if (!aceServingMe || now >= attentiveSwitchAt)
            {
                attentiveLooking = !aceServingMe || !attentiveLooking;
                attentiveSwitchAt = now + (attentiveLooking ? Random.Range(3f, 6f) : Random.Range(.8f, 1.6f));
            }
            aceServingMe = true;
            if (attentiveLooking) Attend(ace, eyes, .9f);
            else ClearAttention();
            return;
        }
        aceServingMe = false;

        // A slow sample of his distance: is he coming this way?
        bool comingOver = false;
        if (now >= aceDistanceSampleAt)
        {
            comingOver = distance < counterLookRange && bearing < 110f && aceDistanceSample - distance > .6f;
            aceDistanceSample = distance;
            aceDistanceSampleAt = now + .5f;
        }

        if ((arrived || comingOver) && now >= queueLookCooldownUntil)
        {
            NpcMovementProfile profile = social != null ? social.Profile : null;
            float tendency = profile != null ? profile.lookTendency : .5f;
            float delay = profile != null ? profile.reactionDelay * .4f : 0f;
            queueLookFrom = now + delay;
            queueLookUntil = queueLookFrom + Random.Range(1.4f, 2.6f) * (.75f + .5f * tendency);
            queueLookCooldownUntil = queueLookUntil + Random.Range(3f, 7f);
        }

        if (now >= queueLookFrom && now < queueLookUntil) Attend(ace, eyes, .85f);
        else ClearAttention();
    }

    // The brain's attention goes through NpcSocial when there is one (it wins
    // over anything ambient there); straight to the head otherwise.
    private void Attend(Transform target, Vector3 offset, float weight)
    {
        if (social != null) social.Focus(target, offset, weight);
        else lookAt.LookAt(target, offset, weight);
    }

    private void ClearAttention()
    {
        if (social != null) social.ClearFocus();
        else lookAt.Clear();
    }

    private NpcSocial.Situation SituationNow(bool seated)
    {
        if (InConversation) return NpcSocial.Situation.Ordering;
        switch (state)
        {
            case State.WaitingInQueue: return hasPendingDestination || (locomotion != null && locomotion.HasGoal) ? NpcSocial.Situation.Walking : NpcSocial.Situation.Queue;
            case State.Settling: return NpcSocial.Situation.Walking;
            case State.Waiting:
                if (seating != null && seating.IsSeated) return NpcSocial.Situation.Seated;
                if (seated) return NpcSocial.Situation.Walking;   // stepping to or from the chair
                return hasPendingDestination || (locomotion != null && locomotion.HasGoal) ? NpcSocial.Situation.Walking : NpcSocial.Situation.StandingWait;
            case State.Speaking:
            case State.Leaving: return NpcSocial.Situation.Leaving;
            default: return NpcSocial.Situation.Walking;
        }
    }

    // A small change of facing every so often while queueing: a weight shift,
    // not a fidget. Suppressed while Ace is being looked at from close by.
    private void ShiftWeightInQueue()
    {
        if (Time.time < nextQueueShift || slotIndex < 0 || queue == null) return;
        nextQueueShift = Time.time + Random.Range(7f, 14f);
        if (hasPendingDestination || locomotion == null || locomotion.HasGoal) return;
        queueYaw = Mathf.Clamp(queueYaw + Random.Range(-6f, 6f), -queueYawVariation - 4f, queueYawVariation + 4f);
        locomotion.Face(QueueFacing(slotIndex));
    }

    private Quaternion FacingTowards(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to.normalized, Vector3.up) : transform.rotation;
    }

    // Keep the eyes on Ace for a moment after something changed hands.
    private void HoldAttention(float seconds) => attentionUntil = Mathf.Max(attentionUntil, Time.time + seconds);

    // ---------- conversation hand-off ----------

    // Called by ConversationController.Begin(). From here until the panel
    // closes, they are not allowed to move.
    public void OnConversationOpened(ConversationController controller)
    {
        conversation = controller;
        if (beats != null) beats.StopIdle();   // the phone goes away when Ace talks to them
        if (locomotion != null)
        {
            locomotion.Pause();
            // Turn to the person you are talking to (the head follows him; the
            // body squares up once, here, not every time he shifts).
            PlayerInteractor ace = Player;
            if (ace != null && !(seating != null && seating.Busy)) locomotion.Face(FacingTowards(ace.transform.position));
        }
        HoldAttention(1.5f);
    }

    // Called by ConversationController.End(), which fires only after the
    // closing line has finished revealing AND the hold has elapsed. This is
    // the moment the body comes back to us.
    public void OnConversationClosed()
    {
        conversation = null;
        if (locomotion != null) locomotion.Resume();

        System.Action change = pendingHandoff;
        pendingHandoff = null;
        if (change != null) change();
    }

    // Every physical change routes through here. Not all of them happen inside
    // a conversation — floor handback and drink service never do — so this
    // can't just be "move the code into End()".
    private void RunOrDefer(System.Action change)
    {
        if (change == null) return;

        if (conversation != null) { pendingHandoff = change; return; }
        change();
    }

    // ---------- player actions ----------

    public string HearIntake()
    {
        // Leaving and reopening the conversation repeats the same request;
        // it must not reroll a memory callback or leave an empty dialogue box.
        if (intakeGiven) return intakeLine;
        if (!CanHearIntake) return "";

        intakeGiven = true;
        intakeLine = identity != null ? identity.Say(CustomerIdentity.Beat.Intake) : "";
        // A hello if they have not greeted Ace yet, a head shake if they have waited
        // too long; otherwise the usual gesture. The body follows the portrait's face.
        ReactOr(NpcBeats.Moment.Intake, PanelFace());
        // Ace has just been told this: a regular's story goes in the notebook.
        NotebookHooks.HeardIntake(identity, record);
        return intakeLine;
    }

    // DATA ONLY. Nothing here touches the agent, the counter slot, the waiting
    // spot, or the state enum — that's BeginWaiting(), and it doesn't run until
    // the conversation formally closes. The device still lands on the shelf on
    // the same frame you press E, so the feedback is as instant as it was.
    public string AcceptJob()
    {
        if (!CanAcceptJob || record == null) return "";

        wasAccepted = true;
        repairStartedAt = DayClock.Instance != null ? DayClock.Instance.SecondsIntoDay : 0f;

        decided = true;
        jobAccepted = true;

        if (JobIdentityManager.Instance != null)
        {
            JobIdentityManager.Instance.Next(out int num, out Color col);
            JobNumber = num;
            JobColor = col;
        }
        record.number = JobNumber;
        record.color = JobColor;

        if (record.kind == JobKind.Drink)
        {
            // Nothing spawns. They go and wait while you make it.
            drinkOrdered = true;
            drinkStarted = false;
            DrinkOrderedAt = Time.time;
        }
        else
        {
            SpawnDeviceOntoShelf();
        }

        // Their number floats over them so you can find them across the room.
        if (waitingBadge != null) waitingBadge.Show(JobNumber, JobColor);

        if (IsCounterRepair)
            patienceLeft = serviceMax; // Keep their counter slot; no shelf, waiting spot or secondary drink.
        else
            RunOrDefer(BeginWaiting);

        // A repair is handed over (the Interact gesture); a drink order gets a nod.
        if (record.kind == JobKind.Drink) ReactOr(NpcBeats.Moment.AcceptedDrink);
        else
        {
            React();
            if (social != null) social.Nod(7f);
        }
        string acceptedLine = identity != null ? identity.Say(CustomerIdentity.Beat.Accepted) : "";
        if (identity == null) return acceptedLine;
        // Their thanks is heard too: for Grace's camera, the rest of her story (the notebook). On her
        // return visit the photo's news and the print take the place of her usual thanks.
        NotebookHooks.HeardThanks(identity);
        return identity.AcceptReturnMemento(acceptedLine);
    }

    /// <summary>
    /// Ace asks them about something (a topic in the conversation's reply list): their answer, one line
    /// per beat. Data only, like AcceptJob: the conversation shows it.
    /// </summary>
    public string AskTopic(TopicChoice topic) => identity != null ? identity.Ask(topic) : "";

    // The device goes on the intake shelf, not in front of the customer —
    // they're about to walk away from the counter.
    private void SpawnDeviceOntoShelf()
    {
        Transform slotPoint = queue.SlotPoint(slotIndex);
        GameObject spawned = Instantiate(record.devicePrefab, slotPoint.position, slotPoint.rotation);

        DeviceDefinition dev = spawned.GetComponent<DeviceDefinition>();
        if (dev != null) dev.ApplyFault(record.faultIndex);

        activeJob = spawned.GetComponent<JobBase>();
        if (activeJob != null)
        {
            activeJob.SetOwner(this);
            activeJob.Configure(record);

            Transform shelf = !IsCounterRepair && IntakeShelf.Instance != null
                ? IntakeShelf.Instance.Claim(activeJob) : null;

            // CanAcceptJob already checked for room, so null here means the
            // shelf isn't wired up. Leave it at the counter rather than lose it.
            PlacementJitter.Apply(activeJob, shelf != null ? shelf : slotPoint,
                                  shelfYawJitter, shelfOffsetJitter);
            // The view presents this customer's device. The owned task remains
            // alive here when the player steps away; it is never a carried item.
            if (IsCounterRepair)
            {
                foreach (var renderer in spawned.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var collider in spawned.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
        }

        JobMarker itemMarker = spawned.GetComponentInChildren<JobMarker>(true);
        if (itemMarker != null && !IsCounterRepair) itemMarker.Show(JobNumber, JobColor);
    }

    // Free the counter slot and go stand somewhere else. This is the whole
    // point of the pass: the next person can be served immediately.
    //
    // Runs on conversation close, never during it.
    private void BeginWaiting()
    {
        patienceLeft = serviceMax;
        settleAttempts = 0;

        // Armed, not fired. The slot is handed back when they physically start
        // walking out of it — see the deferred-move block in Update. If they
        // can't find anywhere to go they keep standing here and keep the slot,
        // which is honest: the queue really is full, and the spawner correctly
        // holds new arrivals back rather than sending someone to a space that
        // has a person in it.
        releaseSlotOnMove = true;

        if (!TryTakeWaitingSpot())
        {
            // Floor was full, or every free spot is unreachable. They hold
            // position near the counter and keep looking, rather than being
            // planted there permanently with nowhere to go.
            state = State.Waiting;
            retryClaimAt = Time.time + spotRetryInterval;
            ScheduleDrinkWish();   // stuck by the counter still counts as settled
        }
    }

    private void ReleaseCounterSlot()
    {
        if (slotIndex >= 0 && queue != null) queue.ReleaseSlot(this);
        slotIndex = -1;
    }

    // THE GUARD THAT KILLS THE HOVER: Settling cannot be entered without a
    // claimed spot AND a complete path to it. Previously we entered Settling
    // with isStopped = true and no destination, which is the definition of
    // standing there doing nothing.
    private bool TryTakeWaitingSpot()
    {
        if (WaitingArea.Instance == null) return false;
        if (agent == null || !agent.isOnNavMesh) return false;

        WaitingSpot.SpotKind preferred = identity != null
            ? identity.PreferredWaitKind : WaitingSpot.SpotKind.Loiter;

        WaitingSpot spot = WaitingArea.Instance.Claim(this, preferred);
        if (spot == null) return false;

        Vector3 destination = spot.StandPoint.position;

        // A spot sitting off the NavMesh used to freeze that customer forever —
        // it's the first thing in the step-1 troubleshooting table. Now we hand
        // it back and try a different one next tick.
        if (!CanReach(destination))
        {
            spot.Release(this);
            return false;
        }

        waitingSpot = spot;
        if (spot != null) lastWaitKind = spot.Kind;
        state = State.Settling;
        settleDestination = destination;
        hasStepBack = false;

        // STEP BACK BEFORE YOU TURN.
        //
        // The seat is out in the room, but the straight line to it from a
        // counter slot runs along the counter frontage — through every other
        // person standing at it. Local avoidance can nudge an agent sideways;
        // it never re-plans the path. So they grind along it: shoving when
        // they're allowed to, stalling when they're not. Widening the slots
        // doesn't help, because the conflict is ALONG the rank, not between
        // neighbours.
        //
        // So take one step backwards into open floor first. From there the
        // route to any table is clear and nobody is in it.
        //
        // The direction comes from the SLOT, not from the customer: queued
        // customers are parked facing the slot's rotation, so the slot's
        // forward is "at the counter" by definition. Rotate a slot in the scene and the step-back follows
        // it. If the point isn't on the NavMesh we silently do exactly what we
        // did before — this can't introduce a new way to fail.
        //
        // Pass 2c: only when it is needed, and towards the spot's side. Measured
        // in the pass 2b recordings, the straight-back step was an about-turn
        // that went either way round (the long way for 1-5 people in 5-7), then
        // a second turn at walking pace for the seat: 40-170 degrees more
        // turning than the way to the seat needed - the "circle" after
        // ordering. Now: if nobody is standing along the first couple of metres
        // of the direct way, they turn the short way and walk straight there.
        // If somebody is, the step goes back AND to the side the spot is on, so
        // the turn away is the short way round and the walk curves on behind
        // the person at the counter.
        if (counterStepBack > 0f && slotIndex >= 0 && queue != null)
        {
            Transform slot = queue.SlotPoint(slotIndex);
            Vector3 back = -slot.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = -transform.forward;
            back.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, -back);
            Vector3 toSpot = destination - transform.position;
            toSpot.y = 0f;

            if (DirectWayBlocked(destination, back))
            {
                float aside = Vector3.Dot(toSpot, right) >= 0f ? counterStepAside : -counterStepAside;
                Vector3 probe = slot.position + back * counterStepBack + right * aside;
                if (NavMesh.SamplePosition(probe, out NavMeshHit backHit, 1f, NavMesh.AllAreas)
                    && CanReach(backHit.position))
                {
                    stepBackTo = backHit.position;
                    hasStepBack = true;
                    destination = stepBackTo;
                }
            }
        }

        // Yield, don't barge.
        //
        // Customers parked at the counter stand at priority 0, and Unity's
        // scale runs backwards: LOWER number = HIGHER priority. A leaver at an
        // ordinary walking priority would outrank the three standing still,
        // and avoidance would decide THEY should get out of HIS way — being
        // stopped, they'd just get shoved. 95 puts the leaver below everyone
        // he passes, so he steers around the queue instead of through it.
        // Yielding is only safe because the locomotion's recovery ladder still
        // steps aside and pushes through if politeness costs him too long.
        // From the counter: hand the device over first, then turn away.
        if (hasStepBack) MoveAfter(destination, LeaveCounter("step back"), Mathf.Max(reactionTime, counterTurnAwayAfter));
        else MoveAfterReacting(destination, LeaveCounter("spot"));
        return true;
    }

    // ---------- crowding ----------

    private int RollMovingPriority() => Random.Range(movingPriorityMin, movingPriorityMax + 1);

    // Drop the path and stand still, facing the way the slot or spot points.
    //
    // THE OTHER HALF OF THE STANDOFF: an agent that has "arrived" but still
    // holds a path keeps applying steering toward it every frame. Two of them
    // a few centimetres short of their spots will lean on each other forever,
    // because neither is ever quite done. Once you're there, you're scenery.
    private void StopSteering()
    {
        Quaternion? facing = null;
        if (slotIndex >= 0 && queue != null) facing = QueueFacing(slotIndex);
        else if (waitingSpot != null) facing = waitingSpot.StandPoint.rotation;
        locomotion.Park(facing);
    }

    private void SettleHere() => SettleHere(0f);

    private void SettleHere(float carriedSpeed)
    {
        state = State.Waiting;
        StopSteering();
        ScheduleDrinkWish();

        // A table seat means an actual chair: sit down on it. NpcSeating walks
        // round the chair, sits, and stands them up again by itself the moment
        // they're given somewhere else to go. Without it (or with Snap To Seat
        // off) they wait standing beside the chair, as they always have.
        if (seating != null && waitingSpot is TableSeat tableSeat)
        {
            if (!seating.TrySit(tableSeat, carriedSpeed) && carriedSpeed > 0f)
            {
                // Handed over early but the seating declined: finish the walk to the stand point.
                state = State.Settling;
                locomotion.MoveTo(settleDestination, LeaveCounter("spot"));
            }
        }
    }

    // ---------- the drink wish ----------

    // Starts the clock the moment they're actually settled, so the order lands
    // while you're heads-down at the bench rather than while they're still
    // walking. Guarded because SettleHere can run more than once — the jam
    // detector gives up and settles them where they stand.
    private void ScheduleDrinkWish()
    {
        if (drinkWish == null || drinkOrdered || drinkAskAt > 0f) return;
        drinkAskAt = Time.time + Random.Range(orderDelayMin, orderDelayMax);
    }

    private void TickDrinkWish()
    {
        if (drinkAskAt <= 0f || drinkOrdered) return;
        if (Time.time < drinkAskAt) return;

        drinkOrdered = true;
        drinkStarted = false;
        DrinkOrderedAt = Time.time;      // EspressoMachine serves oldest-first

        // They've decided to settle in, so they're in less of a hurry. Small,
        // fixed, and applied on ORDERING rather than on serving — without it,
        // asking for a coffee would hand you a second job and no extra time,
        // which is punishment dressed up as a feature.
        patienceLeft = Mathf.Min(patienceLeft + serviceMax * orderTopUp, serviceMax);

        // Calling across the room: a wave from a chair or on foot. With the Mixamo clips
        // they also look his way, and on foot turn to him first - a wave at the window
        // they were facing read wrong (27 Sept recording).
        if (beats != null && beats.Ready)
        {
            HoldAttention(3f);
            PlayerInteractor ace = Player;
            if (ace != null && state == State.Waiting && !(seating != null && seating.Busy))
                locomotion.Face(FacingTowards(ace.transform.position));
        }
        ReactOr(NpcBeats.Moment.CallForDrink);
        Say(OrderLine(), broadcast: true);      // they're calling across the room
    }

    private string OrderLine()
    {
        string line = identity != null ? identity.Say(CustomerIdentity.Beat.OrderedDrink) : "";

        // Placeholder only. Delete this branch once orderedDrink has real lines.
        if (string.IsNullOrEmpty(line)) line = orderFallback;

        string drinkName = WantedDrink != null ? WantedDrink.drinkName : "coffee";
        return line.Replace("{drink}", drinkName);
    }

    // The locomotion gave up on the way to a spot: this seat isn't happening.
    private void GiveUpOnSpot()
    {
        settleAttempts++;

        if (WaitingArea.Instance != null) WaitingArea.Instance.Release(this);
        waitingSpot = null;
        hasStepBack = false;

        // Try somewhere else, twice. After that stop fighting the room and
        // wait where you are — someone standing slightly wrong is far better
        // than two people wrestling until the shop closes.
        if (settleAttempts <= 2 && TryTakeWaitingSpot()) return;

        Debug.LogWarning($"[{CustomerName}] gave up on finding a seat and is " +
                         $"waiting where they stand. Usually means the seats " +
                         $"are unreachable, not that the floor is full.", this);
        SettleHere();
    }

    // Is somebody standing (not walking past) close to the first couple of
    // metres of the direct way from here to the spot? A way that already heads
    // away from the counter (within 50 degrees of straight back) never runs
    // along it, so it counts as clear.
    private bool DirectWayBlocked(Vector3 destination, Vector3 back)
    {
        var way = new NavMeshPath();
        if (!agent.CalculatePath(destination, way) || way.status != NavMeshPathStatus.PathComplete) return true;
        Vector3[] corners = way.corners;
        if (corners.Length < 2) return false;
        Vector3 first = corners[1] - corners[0];
        first.y = 0f;
        if (first.sqrMagnitude > 1e-4f && Vector3.Angle(first, back) < 50f) return false;
        const float look = 2.2f;
        float along = 0f;
        for (int i = 1; i < corners.Length && along < look; i++)
        {
            Vector3 a = corners[i - 1], b = corners[i];
            a.y = b.y = 0f;
            float len = (b - a).magnitude;
            if (len < 1e-4f) continue;
            if (along + len > look) { b = a + (b - a) * ((look - along) / len); len = look - along; }
            foreach (NpcLocomotion other in NpcLocomotion.All)
            {
                if (other == null || other == locomotion || !other.isActiveAndEnabled) continue;
                if (other.Speed > .4f) continue;   // walking past: gone by the time we get there
                Vector3 p = other.transform.position;
                p.y = 0f;
                Vector3 ab = b - a;
                float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                if (((a + ab * u) - p).sqrMagnitude < counterWayClearance * counterWayClearance) return true;
            }
            along += len;
        }
        return false;
    }

    // The café animator's states are named after the library's clips.
    private static readonly int InteractStateHash = Animator.StringToHash("CharacterArmature|Interact");
    private static readonly int InteractShortHash = Animator.StringToHash("Interact");
    private static bool IsInteract(AnimatorStateInfo state) =>
        state.shortNameHash == InteractStateHash || state.shortNameHash == InteractShortHash;

    // The hand-over gesture is still up (or just starting): not yet time to turn away.
    private bool GestureStillPlaying()
    {
        // A reaction from the Mixamo clips (a nod, thanks) counts as the gesture too.
        if (beats != null && beats.Reacting) return true;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
        if (animator.IsInTransition(0))
        {
            if (IsInteract(animator.GetNextAnimatorStateInfo(0))) return true;
            return IsInteract(animator.GetCurrentAnimatorStateInfo(0))
                   && animator.GetAnimatorTransitionInfo(0).normalizedTime < .4f;
        }
        return IsInteract(animator.GetCurrentAnimatorStateInfo(0));
    }

    private bool CanReach(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    // Stand still for a moment, then walk. Without the pause they slide off
    // mid-"Interact" animation, which reads as moonwalking.
    private void MoveAfterReacting(Vector3 destination, NpcLocomotion.Move how) => MoveAfter(destination, how, reactionTime);

    private void MoveAfter(Vector3 destination, NpcLocomotion.Move how, float delay)
    {
        pendingDestination = destination;
        pendingMove = how;
        hasPendingDestination = true;
        moveAllowedAt = Time.time + delay;
        locomotion.Pause();
    }

    // Tell them we can't make it. The order clears and they go.
    //
    // Whether this counts as a loss depends entirely on what else they came
    // for. Someone who got their repair got what they came for and leaves
    // slightly disappointed — that is NOT the same failure as a person who
    // waited and stormed out, and the recap has to stop conflating them or the
    // "what screwed me today?" question goes back to being unanswerable.
    public string ApologiseForDrink()
    {
        if (!CanApologiseForDrink) return "";

        drinkOrdered = false;
        drinkStarted = false;

        // Repair delivered = a served customer who missed out on a coffee.
        // Nothing delivered = a genuine turn-away, and OutOfStock already means
        // "not your fault" in the recap.
        bool alreadyServed = wasServed;
        lossReason = LostReason.OutOfStock;

        // A small ding rather than a full tip, because they did wait for
        // something that never came — but the repair money stands.
        paidTip = Mathf.Max(0, paidTip - 1);

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Declined) : "";
        if (beats != null) beats.React(NpcBeats.Moment.LetDown);

        if (conversation == null) Say(line);
        FinishAndLeave(line, alreadyServed);
        return line;
    }

    public string RefuseJob()
    {
        if (!CanRefuse) return "";

        // Read before `decided` flips, because OutOfStock and ShelfFull both
        // hang off CanDecide and go false the instant it does.
        lossReason = OutOfStock  ? LostReason.OutOfStock
                   : ShelfFull   ? LostReason.ShelfFull
                                 : LostReason.Declined;

        decided = true;

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Declined) : "";
        if (beats != null) beats.React(NpcBeats.Moment.LetDown);
        FinishAndLeave(line, false);
        return line;
    }

    // The closing line has already been delivered — in the panel if we're in a
    // conversation, or as a floating bubble if this happened out on the floor.
    // Don't make them say it twice.
    //
    // This is why the fix is a gate rather than "move the code into End()":
    // CompleteJob can arrive either way, and ServeDrink never has a panel at all.
    private void FinishAndLeave(string line, bool happy)
    {
        if (conversation != null) RunOrDefer(() => Depart(happy));
        else LeaveAfterSpeaking(line, happy);
    }

    // Hand over a finished drink.
    public string ServeDrink(PlayerCarry carry)
    {
        if (!drinkOrdered || !IsWaiting || carry == null) return "";
        DrinkJob drink = FindServeableDrink(carry);
        if (!SelectDeliveryItem(carry, drink)) return "";

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = identity != null ? identity.TipMultiplier : 1f;

        int basePay = drink.Drink != null ? drink.Drink.price : 4;
        tipMult *= drink.FreshnessTipMultiplier;
        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip, false);
        Sfx.Play("handover.drink", transform.position);
        Sfx.PlayLater("money.paid", transform.position, .25f);
        if (tip > 0) Sfx.PlayLater("money.tip", transform.position, .55f);

        // Accumulated, not assigned — a repair customer who also bought a
        // coffee gets paid twice in one visit, and the log should show the
        // whole visit, not the last thing that happened in it.
        paidBase += basePay;
        paidTip  += tip;
        wasServed = true;

        carry.Consume();
        drinkOrdered = false;
        drinkStarted = false;
        drinkServedAt = Time.time;      // starts the calm-drain window

        // The visible half of the reward. The drain multiplier is the half that
        // actually wins you the day, but a bar draining slightly slower is not
        // something anyone notices mid-panic.
        patienceLeft = Mathf.Min(patienceLeft + serviceMax * serveBump, serviceMax);

        ReactOr(NpcBeats.Moment.Served);
        HoldAttention(2.5f);

        // THE SPLIT THAT MAKES THE WHOLE PASS WORK.
        //
        // This used to end the visit unconditionally, which was fine while a
        // drink customer had only ever come for a drink. The moment a repair
        // customer can also want coffee, handing it over would send them home
        // WITH THEIR PHONE STILL ON YOUR SHELF.
        //
        // So: leave only if the drink was the whole reason they came.
        if (activeJob == null)
        {
            string bye = identity != null ? identity.SayDrinkCompleted() : "";
            FinishAndLeave(bye, true);
            return bye;
        }

        // Still owed a repair. Thank you, and back to waiting.
        string thanks = identity != null ? identity.Say(CustomerIdentity.Beat.Reassured) : "";
        Say(thanks);
        return thanks;
    }

    public string CompleteJob()
    {
        if (!JobReady) return "";
        var carry = DeliveryCarry;
        if (!SelectDeliveryItem(carry, activeJob)) return "";
        return CompleteRepair(carry);
    }

    public string CompleteCounterRepair(JobBase expected)
    {
        if (!CanFixAtCounter || expected == null || expected != activeJob || expected.Owner != this
            || !expected.IsComplete || !expected.CanHandBack) return "";
        return CompleteRepair();
    }

    private string CompleteRepair(PlayerCarry deliveryCarry = null)
    {
        string physicalEnding = IsCounterRepair && HumanConversation != null ? HumanConversation.CompletionLine : null;

        // TWO INDEPENDENT AXES, deliberately:
        //   quality -> base pay   (fix it well)
        //   speed   -> tip        (fix it fast)
        //
        // GDD 5.3 folds "under par" into Perfect, which means being slow
        // penalises you twice and you can't tell which lever did what. Split
        // apart, the player learns both rules in about three repairs.
        JobGrade grade = activeJob.Grade;
        float gradeMult = JobBase.PayMultiplier(grade);

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = identity != null ? identity.TipMultiplier : 1f;

        int basePay = Mathf.RoundToInt(activeJob.Payout * gradeMult);
        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);

        // A shoddy job earns a shoddy tip no matter how fast it was.
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip, true, grade);
        Sfx.Play("handover.repair", transform.position);
        Sfx.Play2DLater("repair.returned", .15f);
        Sfx.PlayLater("money.paid", transform.position, .3f);
        if (tip > 0) Sfx.PlayLater("money.tip", transform.position, .6f);

        paidBase += basePay;
        paidTip  += tip;
        wasServed = true;
        lastGrade = grade;
        repairReturned = true;

        // It was in the player's hands, so make sure the shelf/bench forgets it.
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
            spot.Release(activeJob);

        if (deliveryCarry != null) deliveryCarry.Consume();
        else Destroy(activeJob.gameObject);
        activeJob = null;

        // Excited for a perfect repair, thanks for a good one, a shrug for a passable
        // one, disappointment when it comes back unfixed.
        bool reacted = ReactOr(NpcBeats.Moment.Returned, PortraitExpression.Neutral, grade);
        HoldAttention(2.5f);

        string line = physicalEnding ?? (identity != null ? identity.SayRepairCompleted(grade) : "");

        // Only bubble it if there's no panel showing the same words.
        if (conversation == null) Say(line);

        // THE MIRROR OF THE ServeDrink SPLIT, which was never written.
        //
        // ServeDrink learned not to end the visit while a repair was still
        // outstanding. Handing a repair BACK never learned the reciprocal
        // check, so giving someone their phone sent them home on top of a
        // coffee they'd ordered and you hadn't made yet — and because Depart
        // clears drinkOrdered, the ticket vanished without a word. You lost the
        // sale and the game never told you it had happened.
        //
        // Same rule in both directions now: you leave when nobody owes you
        // anything.
        if (drinkOrdered)
        {
            // FOUND IN PLAYTEST, 2026-08-27, by the first person to play this
            // build who wasn't me.
            //
            // Making them stay for their coffee was the right fix. But nothing
            // gave them any relief for the half you HAD delivered, so they kept
            // draining at full rate while you walked to the machine. Handing
            // back a repair felt like it accomplished nothing, and a customer
            // who wanted both became strictly harder than two separate people.
            //
            // hud-spec.md already names this failure: "adding a second task
            // without adding time is pure punishment." It gave the drink a
            // visible jump on serve and never gave the repair one, because
            // until this build a repair handback ended the visit outright.
            //
            // Same principle, same size, applied to the half that was missing
            // it. Fixed rather than proportional, for the same reason as
            // orderTopUp: a proportional bump would rescue the angriest hardest
            // and make dawdling profitable.
            patienceLeft = Mathf.Min(patienceLeft + serviceMax * handbackBump, serviceMax);

            if (!reacted) React();
            Say(line);
            return line;
        }

        FinishAndLeave(line, true);
        return line;
    }

    public void Reassure()
    {
        if (!CanReassure) return;

        float gain = CurrentMax * reassureAmount * Mathf.Pow(reassureFalloff, reassureUses);
        patienceLeft = Mathf.Min(patienceLeft + gain, CurrentMax);

        reassureUses++;
        reassureReadyAt = Time.time + reassureCooldown;

        ReactOr(NpcBeats.Moment.Reassured);
        Say(identity != null ? identity.Say(CustomerIdentity.Beat.Reassured) : "");
    }

    public void RequestFocus()
    {
        if (!CanRequestFocus || !storyteller.RequestFocus()) return;
        // Not reassurance: no patience bonus, tip charge, or relationship cost.
        // Hide the interrupted story before acknowledging the boundary.
        HideBubble();
        string reply = identity.Profile.focusReply;
        Say(string.IsNullOrWhiteSpace(reply) ? "Of course. I'll let you concentrate." : reply, true);
    }

    internal bool TrySayStoryLine(string line)
    {
        if (!CanTellStory || SpeechBusy || speechBubble == null || string.IsNullOrWhiteSpace(line)) return false;
        Say(line, true);
        return true;
    }

    // ---------- leaving ----------

    private void StormOut()
    {
        // Captured BEFORE anything else touches state. Storming out of the
        // queue means you never even heard them; storming out while waiting
        // means you took the job and didn't get back. Different failures,
        // different fixes, so they're worth telling apart in the log.
        lossReason = state == State.WaitingInQueue && !jobAccepted
            ? LostReason.StormedOutInQueue
            : LostReason.StormedOutWaiting;

        decided = true;

        // If their patience runs out mid-conversation, the panel would
        // otherwise sit there offering "[E] Take the job" while they're
        // actually walking out in disgust. Drop the deferred move first so
        // closing the panel doesn't send them to a waiting spot on the way.
        pendingHandoff = null;
        if (conversation != null) conversation.End();

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.StormedOut) : "";
        Say(line, broadcast: true);   // shouting at the room, not talking to you
        LeaveAfterSpeaking(line, false);
        // The angry gesture (or a dismissive wave) before they go; in a chair, an angry one there.
        if (beats != null) beats.React(NpcBeats.Moment.StormOut);
    }

    // Remove them immediately, with no goodbye and no walk to the door.
    //
    // WHY THIS ISN'T JUST Destroy(gameObject): a customer's device is a
    // SEPARATE GameObject sitting in an intake shelf slot. OnDestroy only
    // releases their waiting spot and closes any open conversation — releasing
    // the shelf slot and destroying the device both live in Depart().
    //
    // So destroying a customer directly would strand their phone on the shelf
    // holding a slot forever. A full shelf blocks intake, so you'd quietly lose
    // the ability to take repairs, one abandoned device per incident, with
    // nothing in the Console to explain it.
    //
    // Used by DayClock.StartDay to guarantee a new day begins with an empty
    // shop. Nothing here touches stats — a customer cleared this way was never
    // served and was already counted (or deliberately not) elsewhere.
    public void ForceRemove()
    {
        pendingHandoff = null;
        if (conversation != null) conversation.End();

        if (activeJob != null)
        {
            foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
                spot.Release(activeJob);

            Destroy(activeJob.gameObject);
            activeJob = null;
        }

        drinkOrdered = false;

        if (slotIndex >= 0 && queue != null) queue.ReleaseSlot(this);
        slotIndex = -1;

        if (WaitingArea.Instance != null) WaitingArea.Instance.Release(this);
        waitingSpot = null;

        Destroy(gameObject);
    }

    private void LeaveAfterSpeaking(string line, bool happy)
    {
        departHappy = happy;
        // Say it standing still; Depart sends them to the door afterwards.
        if (locomotion != null && !(seating != null && seating.Busy)) locomotion.Park(null);

        // Match the bubble's own lifetime, so they never walk off mid-sentence.
        speakTimer = Mathf.Max(RevealTime(line) + lineHoldTime, 1.8f);
        state = State.Speaking;
    }

    private void Depart(bool happy)
    {
        if (state == State.Leaving) return;
        state = State.Leaving;
        leaveDeadline = Time.time + leaveTimeout;

        // Whatever was queued up, it's moot now.
        pendingHandoff = null;
        jobAccepted = false;

        if (activeJob != null)
        {
            foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
                spot.Release(activeJob);

            Destroy(activeJob.gameObject);
            activeJob = null;
        }

        drinkOrdered = false;

        if (slotIndex >= 0)
        {
            queue.ReleaseSlot(this);
            slotIndex = -1;
        }

        if (WaitingArea.Instance != null) WaitingArea.Instance.Release(this);
        waitingSpot = null;

        if (waitingBadge != null) waitingBadge.Hide();
        if (patienceBar != null) patienceBar.gameObject.SetActive(false);

        // Counted once per PERSON, here at the exit, rather than once per thing
        // handed over. Served and Visitors answer different questions and the
        // recap needs both: "how much work did I get through" and "how many
        // people left happy".
        if (wasServed && DayClock.Instance != null) DayClock.Instance.RecordVisitorSatisfied();

        if (!happy && DayClock.Instance != null) DayClock.Instance.RecordLost(lossReason);

        // Named regulars carry one compact memory record into the next day and
        // the next session. Walk-ins never enter the save file.
        if (identity != null && identity.Profile != null && SaveManager.Instance != null)
        {
            string grade = repairReturned
                ? lastGrade.ToString()
                : "";

            SaveManager.Instance.RecordRegularVisit(
                identity.Profile,
                happy,
                wasAccepted,
                wasServed,
                lossReason,
                grade,
                storyteller != null && storyteller.FocusRequested,
                record);
        }

        // One line per visit, written the moment the visit is over. Read-only:
        // DayLog changes nothing and can be deleted whenever it stops earning
        // its place.
        DayLog.Record(this, happy, lossReason, wasServed, wasAccepted,
                      paidBase, paidTip, lastGrade, repairStartedAt);

        // The same facts, as a review for the café's reputation. Reviews only
        // count at closing (claude/reputation-spec.md).
        if (SaveManager.Instance != null)
            SaveManager.Instance.RecordReview(this, new ReviewFacts
            {
                happy = happy,
                served = wasServed,
                accepted = wasAccepted,
                repairVisit = record != null && record.kind == JobKind.Repair,
                repairReturned = repairReturned,
                grade = lastGrade,
                reason = lossReason,
                patienceAtExit = PatienceFraction
            });

        hasPendingDestination = false;
        // Seated customers stand up first (NpcLocomotion asks NpcSeating), and
        // the walk uses an ordinary priority again: parked at 0, everyone else
        // would pin them against a table on the way out.
        WalkToExit();
    }

    private void OnDestroy()
    {
        // Belt and braces — a customer destroyed any other way must not leave
        // a spot marked occupied forever.
        if (WaitingArea.Instance != null) WaitingArea.Instance.Release(this);

        // Nor a conversation panel open with nobody on the other side of it.
        pendingHandoff = null;
        if (conversation != null) conversation.End();
    }

    // ---------- dialogue ----------

    private float RevealTime(string line)
    {
        if (string.IsNullOrEmpty(line)) return 0f;
        return line.Length / Mathf.Max(charactersPerSecond, 1f);
    }

    // BROADCAST — say it whether or not the player is looking.
    //
    // ForceShow used to refuse unless you were focusing this customer or they
    // were formally Speaking. Sensible for chatter; wrong for the two moments
    // that MATTER, both of which happen while you're heads-down at the bench
    // with your back to the room:
    //
    //   - a repair customer deciding they want a coffee
    //   - somebody giving up and walking out
    //
    // Suppressed, the ticket rail silently grew a line and a person silently
    // vanished. The HUD generated a task and the HUD deleted a customer. That
    // is exactly the "game screwed me" failure — the room is full of people and
    // none of them can get your attention.
    //
    // Broadcast is deliberately rare. If everything shouts, nothing does.
    private void Say(string line, bool broadcast = false)
    {
        if (string.IsNullOrEmpty(line)) return;

        currentLine = line;
        bubbleTimer = RevealTime(line) + lineHoldTime;
        ForceShow(broadcast);
    }

    public void ShowBubble(bool on)
    {
        if (speechBubble == null) return;

        if (!on && bubbleTimer > 0f) return;
        if (on && bubbleTimer > 0f && revealRoutine != null) return;

        if (on) ForceShow();
        else HideNow();
    }

    private void ForceShow(bool broadcast = false)
    {
        if (speechBubble == null || string.IsNullOrEmpty(currentLine)) return;

        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        bool isFocused = player != null && player.Focused != null &&
                         player.Focused.GetComponent<CustomerBrain>() == this;
        if (!broadcast && !isFocused && state != State.Speaking) return;

        if (revealRoutine != null) StopCoroutine(revealRoutine);

        speechBubble.text = currentLine;
        speechBubble.color = identity != null ? identity.ThemeColor : Color.white;
        speechBubble.gameObject.SetActive(true);
        revealRoutine = StartCoroutine(RevealWords());
    }

    private void HideNow()
    {
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        if (speechBubble != null) speechBubble.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator RevealWords()
    {
        speechBubble.ForceMeshUpdate();
        int total = speechBubble.textInfo.characterCount;
        speechBubble.maxVisibleCharacters = 0;

        float perChar = 1f / Mathf.Max(charactersPerSecond, 1f);
        float carry = 0f;

        for (int i = 1; i <= total; i++)
        {
            speechBubble.maxVisibleCharacters = i;

            // Batch characters when they're faster than a frame.
            carry += perChar;
            if (carry >= Time.deltaTime)
            {
                yield return new WaitForSeconds(carry);
                carry = 0f;
            }
        }

        speechBubble.maxVisibleCharacters = total;
        revealRoutine = null;
    }

    private void HideBubble()
    {
        currentLine = "";
        bubbleTimer = 0f;
        HideNow();
    }

    // ---------- helpers ----------

    // Null-safe because a downloaded model might arrive without an Animator,
    // or with one that has no "Interact" state. A missing animation should
    // never take the customer's whole brain down with it.
    private void React()
    {
        // The standing gesture would pull a seated customer up out of the chair.
        if (seating != null && seating.Busy) return;
        if (animator != null) animator.SetTrigger("Interact");
    }

    // The body language for this moment (NpcBeats: a nod, thanks, a greeting...);
    // when that can't play - no Mixamo clips in the project, or the body is busy -
    // the one gesture every moment used to share. True when a beat played.
    private bool ReactOr(NpcBeats.Moment moment, PortraitExpression face = PortraitExpression.Neutral, JobGrade grade = JobGrade.Good)
    {
        if (beats != null && beats.React(moment, face, grade)) return true;
        React();
        return false;
    }

    // The face the conversation panel shows right now.
    private PortraitExpression PanelFace() =>
        identity != null ? identity.PanelExpressionAt(PatienceFraction) : PortraitExpression.Neutral;

    // Visible whenever they're actively waiting on you — queue or service.
    // Hidden while walking in, once they've been dealt with, and while Ace is in a
    // conversation, whoever it's with: its close-up frames the speaker's head, and
    // the bars over the speaker and anyone standing near them filled the top of the
    // screen. Patience still drains as before, the portrait shows the speaker's
    // mood, and the bars are back when the conversation closes.
    private bool ShowFloatingBar =>
        (state == State.WaitingInQueue || IsWaiting) && !ConversationController.AnyOpen;

    // Drives both readouts. Only ever called from the states where they're
    // actually waiting on you, which is deliberate: Speaking and Leaving don't
    // call it, so the tint FREEZES at whatever it last was. Someone who storms
    // out stays furious all the way to the door; someone served happily walks
    // out their own colour. That's free drama for no extra code.
    private void UpdateBar(float max, Color fullColor)
    {
        float fraction = Mathf.Clamp01(patienceLeft / Mathf.Max(max, 0.01f));

        if (patienceBar == null) return;

        bool show = ShowFloatingBar;
        if (patienceBar.gameObject.activeSelf != show)
            patienceBar.gameObject.SetActive(show);

        if (show) patienceBar.SetFraction(fraction, fullColor);
    }
}
