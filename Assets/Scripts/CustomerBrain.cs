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
    [SerializeField] private float turnSpeed = 240f;

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
    [Tooltip("Each customer rolls an avoidance priority in this range so two " +
             "of them never mirror each other into a standoff. Lower = pushier.")]
    [SerializeField] private int movingPriorityMin = 30;
    [SerializeField] private int movingPriorityMax = 60;

    [Tooltip("Priority once they've settled. High number = low priority, so " +
             "people still walking push past them instead of bouncing off.")]
    [SerializeField] private int settledPriority = 90;

    [Tooltip("Priority while walking away from the counter to their seat. " +
             "Must be HIGHER than settledPriority (= lower priority), so " +
             "someone who's just been served goes around the people still " +
             "waiting instead of shouldering through them.")]
    [SerializeField] private int leavingCounterPriority = 95;

    [Tooltip("Last resort. A customer who has made no measurable progress for " +
             "two full stuckTimeouts escalates to this. Low number = pushy, so " +
             "they will shoulder through rather than stand there forever. " +
             "Normal movement NEVER uses it.")]
    [SerializeField] private int forcePriority = 15;

    [Tooltip("How far counts as 'they moved'. Below this over a whole " +
             "stuckTimeout and they're treated as wedged. Small enough that a " +
             "slow walker never trips it, big enough that avoidance jitter on " +
             "a body pinned against another doesn't read as walking.")]
    [SerializeField] private float progressStep = 0.15f;

    [Tooltip("How far they back away from the counter before turning for their " +
             "seat.\n\nWithout this their route to a table runs ALONG the " +
             "counter, straight through everyone else still queueing — which is " +
             "why serving the person on the right used to shove the other two. " +
             "Set to 0 to walk straight at the seat like before.")]
    [SerializeField] private float counterStepBack = 1.2f;

    [Tooltip("How close counts as arrived. The prefab ships at 1 m, which " +
             "parks people a metre from their own chair.")]
    [SerializeField] private float arriveDistance = 0.3f;

    [Tooltip("If they get no closer to their spot for this long, they're " +
             "jammed. Give up on it and take a different one.")]
    [SerializeField] private float stuckTimeout = 3f;

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

    [Tooltip("Per-customer walk speed variation. 0.15 = ±15%. Identical pace " +
             "reads as a conveyor belt however good the models are.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float walkSpeedJitter = 0.15f;

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
    private CounterQueue queue;
    private Transform exitPoint;
    private PlayerInteractor player;

    private float patienceLeft;
    private float speakTimer;
    private float bubbleTimer;
    private float reassureReadyAt;
    private int reassureUses;
    private int slotIndex = -1;

    // Where they went once you took their job.
    private WaitingSpot waitingSpot;

    // Movement is deferred by reactionTime so they react before they turn away.
    private Vector3 pendingDestination;
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

    // How far up the unwedging ladder this customer currently is. 0 = moving
    // normally. Reset the moment they make progress.
    private int stuckStage;

    // Set on accept, cleared the moment they actually leave the counter slot.
    private bool releaseSlotOnMove;

    // Jam detection while walking to a spot.
    private float stuckDeadline;
    // Progress is measured as DISTANCE ACTUALLY TRAVELLED, not as
    // remainingDistance shrinking.
    //
    // remainingDistance was the obvious choice and it's the wrong one: every
    // re-path changes it discontinuously, so a customer sent the long way round
    // reads as "no progress" while walking perfectly well, and a customer whose
    // baseline was just reset reads as "progress" while standing still. Body
    // moved / body didn't move has neither failure mode.
    private Vector3 lastProgressPos;
    private bool hasProgressPos;
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
    public int JobNumber { get; private set; }
    public Color JobColor { get; private set; } = Color.white;

    public bool HasJob => activeJob != null || drinkOrdered;
    public string JobCardText => record != null ? record.Detail : "";
    public float PatienceFraction => Mathf.Clamp01(patienceLeft / CurrentMax);
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

    private float CurrentMax => IsWaiting ? serviceMax : queueMax;

    // ---------- the intake beat ----------

    public bool CanHearIntake => state == State.WaitingInQueue && !intakeGiven && !decided;
    public bool CanDecide => state == State.WaitingInQueue && intakeGiven && !decided;
    public bool CanRefuse => CanDecide;

    // Nowhere to put their device. You physically cannot take this job until
    // you've cleared the shelf.
    public bool ShelfFull =>
        CanDecide && record != null && record.kind == JobKind.Repair &&
        (IntakeShelf.Instance == null || !IntakeShelf.Instance.HasRoom);

    // A drink order can only be accepted if we can actually make it.
    public bool CanAcceptJob
    {
        get
        {
            if (!CanDecide) return false;

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

    // Is the player holding this customer's drink?
    public bool CanReceiveDrink
    {
        get
        {
            if (!drinkOrdered || !IsWaiting) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry == null || !carry.IsCarrying) return false;

            DrinkJob drink = carry.Carried as DrinkJob;
            if (drink == null) return false;

            // Any latte will do — including one abandoned by someone who left.
            return drink.Drink != null && drink.Drink == WantedDrink;
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

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && carry.Carried == activeJob;
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
            return call != null && (call.CurrentPhase == HoldCallJob.Phase.NeedsDialing ||
                                    call.CurrentPhase == HoldCallJob.Phase.Ringing);
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

        RollMovingPriority();
        agent.stoppingDistance = arriveDistance;

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
        }

        HideBubble();
        if (waitingBadge != null) waitingBadge.Hide();

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
            agent.SetDestination(exitPoint.position);
            return;
        }

        // Nobody walks the same speed. Identical pace is most of why arrivals
        // read as a school dinner queue rather than people coming into a shop.
        agent.speed *= Random.Range(1f - walkSpeedJitter, 1f + walkSpeedJitter);

        state = State.WalkingToCounter;

        // Veer into the room before heading for the counter. Not the full
        // Drifting state from GDD §4.5 — just enough that six arrivals don't
        // trace the same line to the same spot.
        Vector3 drift;
        if (PickDriftPoint(out drift))
        {
            agent.SetDestination(drift);
        }
        else
        {
            driftDone = true;
            agent.SetDestination(queue.SlotPoint(slotIndex).position);
        }
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

        // Queued customers park themselves on arrival (see StopSteering), so
        // shuffling up the line has to wake the agent back up first.
        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            RollMovingPriority();
            agent.SetDestination(queue.SlotPoint(slotIndex).position);
        }
    }

    private void Update()
    {
        if (agent == null) return;

        if (animator != null) animator.SetBool("IsWalking", agent.velocity.magnitude > 0.1f);

        // Held still for a beat after accepting, then released.
        if (hasPendingDestination && Time.time >= moveAllowedAt)
        {
            hasPendingDestination = false;

            // A new move means a new baseline. Without this the watchdog can
            // inherit an anchor from wherever they were standing before the
            // pause and immediately think they're wedged.
            ResetProgressWatch();

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(pendingDestination);
            }

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
                    if (Arrived())
                    {
                        if (!driftDone)
                        {
                            // Reached their look-around spot. Stand a moment,
                            // then go and queue. The pause is what sells it —
                            // walking through a curve at constant speed still
                            // reads as a conveyor belt.
                            driftDone = true;
                            MoveAfter(queue.SlotPoint(slotIndex).position,
                                      Random.Range(driftPauseMin, driftPauseMax));
                            break;
                        }

                        state = State.WaitingInQueue;
                        patienceLeft = queueMax;
                        StopSteering();     // stop shoving whoever's in front
                    }
                    else if (ProgressStalled())
                    {
                        // Wedged on the way IN. This state had no watchdog at
                        // all before, which meant someone blocked in the
                        // doorway stood there until closing time.
                        //
                        // Abandoning the sightseeing detour is free, so do that
                        // first and aim straight at the counter.
                        driftDone = true;

                        if (!TryUnwedge(queue.SlotPoint(slotIndex).position))
                        {
                            // Re-pathing didn't help and neither did barging.
                            // Nine seconds of zero progress means the room is
                            // genuinely impassable for them — let them give up
                            // and walk out, which at least ENDS, and say so
                            // loudly enough to be fixable.
                            Debug.LogWarning($"[{CustomerName}] couldn't reach the " +
                                             $"counter and gave up. Check for a " +
                                             $"gap narrower than the agent radius " +
                                             $"between the door and the counter.", this);
                            StormOut();
                        }
                    }
                }
                break;

            case State.WaitingInQueue:
                FaceTarget();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(queueMax, Color.green);
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.Settling:
                // Walking to their spot. Still waiting on you, so still draining.
                //
                // FaceTarget() is safe to call here: its first line bails out
                // while the agent has velocity, so we still never fight the
                // path for the rotation. It only does anything during the
                // reactionTime pause — which is precisely the beat where
                // nothing was driving the body and they stood frozen.
                FaceTarget();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) { StormOut(); break; }

                if (!hasPendingDestination)
                {
                    if (Arrived())
                    {
                        if (hasStepBack)
                        {
                            // Clear of the counter. NOW turn for the seat.
                            hasStepBack = false;
                            ResetProgressWatch();

                            if (agent.isOnNavMesh)
                            {
                                agent.isStopped = false;

                                // Drop back to polite. If they escalated to
                                // forcePriority getting out of the slot, that
                                // must not follow them across the room.
                                agent.avoidancePriority = leavingCounterPriority;
                                agent.SetDestination(settleDestination);
                            }
                        }
                        else SettleHere();
                    }
                    else if (ProgressStalled())
                    {
                        if (!TryUnwedge(hasStepBack ? stepBackTo : settleDestination))
                            GiveUpOnSpot();
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

                FaceTarget();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.Speaking:
                FaceTarget();
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
                if (Arrived())
                {
                    if (bubbleTimer <= 0f) Destroy(gameObject);
                }
                else if (exitPoint != null && ProgressStalled())
                {
                    // Same ladder on the way out. leaveDeadline above is still
                    // the hard floor, but re-pathing usually beats it by
                    // fifteen seconds and nobody has to watch a customer stand
                    // motionless in the doorway until it fires.
                    TryUnwedge(exitPoint.position);
                }
                break;
        }
    }

    // ---------- conversation hand-off ----------

    // Called by ConversationController.Begin(). From here until the panel
    // closes, they are not allowed to move.
    public void OnConversationOpened(ConversationController controller)
    {
        conversation = controller;
    }

    // Called by ConversationController.End(), which fires only after the
    // closing line has finished revealing AND the hold has elapsed. This is
    // the moment the body comes back to us.
    public void OnConversationClosed()
    {
        conversation = null;

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
        if (!CanHearIntake) return "";

        intakeGiven = true;
        React();
        return identity != null ? identity.Say(CustomerIdentity.Beat.Intake) : "";
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

        RunOrDefer(BeginWaiting);

        React();
        return identity != null ? identity.Say(CustomerIdentity.Beat.Accepted) : "";
    }

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

            Transform shelf = IntakeShelf.Instance != null
                ? IntakeShelf.Instance.Claim(activeJob) : null;

            // CanAcceptJob already checked for room, so null here means the
            // shelf isn't wired up. Leave it at the counter rather than lose it.
            PlacementJitter.Apply(activeJob, shelf != null ? shelf : slotPoint,
                                  shelfYawJitter, shelfOffsetJitter);
        }

        JobMarker itemMarker = spawned.GetComponentInChildren<JobMarker>(true);
        if (itemMarker != null) itemMarker.Show(JobNumber, JobColor);
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

        ResetProgressWatch();

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
        // The direction comes from the SLOT, not from the customer: FaceTarget
        // copies the slot's rotation, so the slot's forward is "at the counter"
        // by definition. Rotate a slot in the scene and the step-back follows
        // it. If the point isn't on the NavMesh we silently do exactly what we
        // did before — this can't introduce a new way to fail.
        if (counterStepBack > 0f && slotIndex >= 0 && queue != null)
        {
            Transform slot = queue.SlotPoint(slotIndex);
            Vector3 probe = slot.position - slot.forward * counterStepBack;

            if (NavMesh.SamplePosition(probe, out NavMeshHit backHit, 1f, NavMesh.AllAreas)
                && CanReach(backHit.position))
            {
                stepBackTo = backHit.position;
                hasStepBack = true;
                destination = stepBackTo;
            }
        }

        // Yield, don't barge.
        //
        // This used to call RollMovingPriority(), which rolls 30-60. Customers
        // parked at the counter sit at settledPriority (90), and Unity's scale
        // runs backwards: LOWER number = HIGHER priority. So the one person who
        // was moving outranked the three standing still, and avoidance decided
        // THEY should get out of HIS way. Being isStopped, they couldn't step
        // aside properly — they just got shoved. Serving the customer on the
        // right visibly barged the middle and left ones out of the way.
        //
        // 95 puts the leaver below everyone he passes, so he steers around the
        // queue instead of through it. Yielding is only safe because the
        // unwedging ladder (see ProgressStalled / TryUnwedge) escalates him
        // back to forcePriority if politeness ever costs him six seconds.
        if (agent != null) agent.avoidancePriority = leavingCounterPriority;

        MoveAfterReacting(destination);
        return true;
    }

    // ---------- crowding ----------

    private void RollMovingPriority()
    {
        if (agent == null) return;
        agent.avoidancePriority = Random.Range(movingPriorityMin, movingPriorityMax + 1);
    }

    // Drop the path and stand still.
    //
    // THE OTHER HALF OF THE STANDOFF: an agent that has "arrived" but still
    // holds a path keeps applying steering toward it every frame. Two of them
    // a few centimetres short of their spots will lean on each other forever,
    // because neither is ever quite done. Once you're there, you're scenery.
    private void StopSteering()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.avoidancePriority = settledPriority;
    }

    private void SettleHere()
    {
        state = State.Waiting;
        StopSteering();
        ScheduleDrinkWish();
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

        React();
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

    // ---------- the unwedging ladder ----------
    //
    // THE RULE THIS ENFORCES: a customer who is supposed to be moving either
    // makes measurable progress, or is resolved within a bounded number of
    // seconds. No state may end in a body standing still forever.
    //
    // Progress is measured as remainingDistance shrinking. Flat for
    // stuckTimeout means something is wrong, and the ladder escalates rather
    // than jumping straight to a drastic fix — because the cheap causes are by
    // far the most common and the drastic fixes are the ones that look bad.
    //
    //   stage 1  (3s)  re-path          — the path went stale; ask for a new one
    //   stage 2  (6s)  barge            — someone really is in the way
    //   stage 3  (9s)  caller's bail-out — give up gracefully, but GIVE UP
    //
    // Any real progress at any point resets the whole thing to stage 0, so a
    // customer who is merely slow is never punished for it.

    // True on the tick where the customer just escalated a stage.
    private bool ProgressStalled()
    {
        if (agent == null || !agent.isOnNavMesh || agent.pathPending) return false;

        // First look at this move: take the anchor and start the clock.
        if (!hasProgressPos)
        {
            hasProgressPos = true;
            lastProgressPos = transform.position;
            stuckDeadline = Time.time + stuckTimeout;
            return false;
        }

        // Covered real ground since the anchor? Then they're fine. Re-anchor,
        // restart the clock, and forget any stage they'd climbed to.
        //
        // A normal walk clears progressStep within a handful of frames, and
        // even someone crawling at 0.1 m/s clears it inside the window. Only a
        // body that is genuinely not moving fails this.
        if ((transform.position - lastProgressPos).sqrMagnitude
            > progressStep * progressStep)
        {
            lastProgressPos = transform.position;
            stuckDeadline = Time.time + stuckTimeout;
            stuckStage = 0;
            return false;
        }

        if (Time.time < stuckDeadline) return false;

        lastProgressPos = transform.position;
        stuckDeadline = Time.time + stuckTimeout;
        stuckStage++;
        return true;
    }

    private void ResetProgressWatch()
    {
        hasProgressPos = false;
        stuckDeadline = 0f;
        stuckStage = 0;
    }

    // Handles stages 1 and 2. Returns false at stage 3+, which means "I'm out
    // of generic ideas, do whatever your state does to end this."
    private bool TryUnwedge(Vector3 goal)
    {
        if (agent == null || !agent.isOnNavMesh) return false;

        switch (stuckStage)
        {
            case 1:
                // Much the most common cause: a path that was valid when it was
                // set and isn't any more — someone parked across it, or it came
                // back partial. Cheap to fix and invisible when it works.
                agent.isStopped = false;
                agent.ResetPath();
                agent.SetDestination(goal);
                return true;

            case 2:
                // A fresh path didn't help, so a body is genuinely in the way
                // and politeness has now cost this customer six seconds.
                // Barge — briefly, and only from here. This is the escape valve
                // that makes "yield by default" safe to have at all.
                Debug.Log($"[{CustomerName}] wedged for {stuckTimeout * 2f}s — " +
                          $"pushing through.", this);
                agent.isStopped = false;

                // Jittered, for the same reason RollMovingPriority is: two
                // customers who both escalate would otherwise land on the
                // IDENTICAL priority and mirror each other into a fresh
                // standoff. Somebody has to be the one who yields.
                agent.avoidancePriority =
                    Mathf.Clamp(forcePriority + Random.Range(-5, 6), 0, 99);
                agent.SetDestination(goal);
                return true;

            default:
                return false;
        }
    }

    // Settling's stage-3 bail-out: this seat isn't happening.
    private void GiveUpOnSpot()
    {
        settleAttempts++;

        if (WaitingArea.Instance != null) WaitingArea.Instance.Release(this);
        waitingSpot = null;
        hasStepBack = false;
        ResetProgressWatch();

        // Try somewhere else, twice. After that stop fighting the room and
        // wait where you are — someone standing slightly wrong is far better
        // than two people wrestling until the shop closes.
        if (settleAttempts <= 2 && TryTakeWaitingSpot()) return;

        Debug.LogWarning($"[{CustomerName}] gave up on finding a seat and is " +
                         $"waiting where they stand. Usually means the seats " +
                         $"are unreachable, not that the floor is full.", this);
        SettleHere();
    }

    private bool CanReach(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    // Stand still for a moment, then walk. Without the pause they slide off
    // mid-"Interact" animation, which reads as moonwalking.
    private void MoveAfterReacting(Vector3 destination) => MoveAfter(destination, reactionTime);

    private void MoveAfter(Vector3 destination, float delay)
    {
        pendingDestination = destination;
        hasPendingDestination = true;
        moveAllowedAt = Time.time + delay;

        if (agent.isOnNavMesh) agent.isStopped = true;
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
        if (!CanReceiveDrink || carry == null) return "";

        DrinkJob drink = carry.Carried as DrinkJob;
        if (drink == null) return "";

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = identity != null ? identity.TipMultiplier : 1f;

        int basePay = drink.Drink != null ? drink.Drink.price : 4;
        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip, false);

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

        React();

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
            string bye = identity != null ? identity.Say(CustomerIdentity.Beat.Completed) : "";
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

        paidBase += basePay;
        paidTip  += tip;
        wasServed = true;
        lastGrade = grade;

        // It was in the player's hands, so make sure the shelf/bench forgets it.
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
            spot.Release(activeJob);

        Destroy(activeJob.gameObject);
        activeJob = null;

        React();

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Completed) : "";

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

            React();
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

        React();
        Say(identity != null ? identity.Say(CustomerIdentity.Beat.Reassured) : "");
    }

    // ---------- leaving ----------

    private void StormOut()
    {
        // Captured BEFORE anything else touches state. Storming out of the
        // queue means you never even heard them; storming out while waiting
        // means you took the job and didn't get back. Different failures,
        // different fixes, so they're worth telling apart in the log.
        lossReason = state == State.WaitingInQueue
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

        // Match the bubble's own lifetime, so they never walk off mid-sentence.
        speakTimer = Mathf.Max(RevealTime(line) + lineHoldTime, 1.8f);
        state = State.Speaking;
    }

    private void Depart(bool happy)
    {
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
            string grade = record != null && record.kind == JobKind.Repair && wasServed
                ? lastGrade.ToString()
                : "";

            SaveManager.Instance.RecordRegularVisit(
                identity.Profile,
                happy,
                wasAccepted,
                wasServed,
                lossReason,
                grade);
        }

        // One line per visit, written the moment the visit is over. Read-only:
        // DayLog changes nothing and can be deleted whenever it stops earning
        // its place.
        DayLog.Record(this, happy, lossReason, wasServed, wasAccepted,
                      paidBase, paidTip, lastGrade, repairStartedAt);

        hasPendingDestination = false;
        if (agent.isOnNavMesh)
        {
            // They were parked at settled priority, which would let everyone
            // else pin them against a table on the way out.
            RollMovingPriority();
            ResetProgressWatch();
            agent.isStopped = false;
            agent.SetDestination(exitPoint.position);
        }
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
        if (animator != null) animator.SetTrigger("Interact");
    }

    // Face the counter while queueing, face however the waiting spot points
    // once they've settled.
    private void FaceTarget()
    {
        // Never steer rotation while the agent is moving us. Turning the body
        // one way while the path drags it another IS the moonwalk.
        if (agent.velocity.sqrMagnitude > 0.01f) return;

        Transform target = null;

        if (slotIndex >= 0 && queue != null) target = queue.SlotPoint(slotIndex);
        else if (waitingSpot != null) target = waitingSpot.StandPoint;

        if (target == null) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target.rotation, turnSpeed * Time.deltaTime);
    }

    // Visible whenever they're actively waiting on you — queue or service.
    // Hidden only while walking in, and once they've been dealt with.
    private bool ShowFloatingBar =>
        state == State.WaitingInQueue || IsWaiting;

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

    // The naive version — !pathPending && remainingDistance <= stoppingDistance —
    // reports TRUE on the first frame after SetDestination, because the path
    // hasn't been built yet so remainingDistance is still 0. That made customers
    // switch to "standing still" logic while they were visibly still walking.
    private bool Arrived()
    {
        if (agent.pathPending) return false;
        if (agent.remainingDistance > agent.stoppingDistance) return false;
        return !agent.hasPath || agent.velocity.sqrMagnitude < 0.01f;
    }
}