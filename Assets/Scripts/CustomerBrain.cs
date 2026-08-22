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

    [SerializeField] private float queuePatience = 15f;
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

    // Drink orders: accepted, but nothing has been made yet.
    private bool drinkOrdered;
    private bool drinkStarted;

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

    // Waiting on a drink that hasn't been started yet.
    public bool AwaitingDrink => drinkOrdered && !drinkStarted;

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
            if (drink == null || record == null) return false;

            // Any latte will do — including one abandoned by someone who left.
            return drink.Drink == record.drink;
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
            if (!IsWaiting || activeJob == null || !activeJob.IsComplete) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && carry.Carried == activeJob;
        }
    }

    // Fixed, but you're not carrying it — the prompt nudges you to go get it.
    public bool JobFixedButAway =>
        IsWaiting && activeJob != null && activeJob.IsComplete && !JobReady;

    public bool CanReassure =>
        IsWaiting
        && Time.time >= reassureReadyAt
        && reassureUses < reassureMaxUses
        && patienceLeft < CurrentMax * 0.9f;

    public bool JobNeedsAttention
    {
        get
        {
            HoldCallJob call = activeJob as HoldCallJob;
            return call != null && (call.CurrentPhase == HoldCallJob.Phase.NeedsDialing ||
                                    call.CurrentPhase == HoldCallJob.Phase.Ringing);
        }
    }

    private float DrainRate
    {
        get
        {
            if (InConversation) return conversationDrainMultiplier;

            // Where they chose to wait changes how fast they sour. Sitting is
            // calm, loitering by the counter is not.
            float spotRate = waitingSpot != null ? waitingSpot.DrainMultiplier : 1f;

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

    public void Init(CounterQueue counterQueue, Transform exit, Job job)
    {
        queue = counterQueue;
        exitPoint = exit;
        record = job;

        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        float mult = identity != null ? identity.PatienceMultiplier : 1f;
        queueMax = queuePatience * mult;
        serviceMax = servicePatience * mult;

        // They know what they came in for, so dialogue can name it.
        if (identity != null && record != null) identity.SetDevice(record.Subject);

        HideBubble();
        if (waitingBadge != null) waitingBadge.Hide();

        slotIndex = queue.ClaimSlot(this);
        if (slotIndex < 0)
        {
            state = State.Leaving;
            agent.SetDestination(exitPoint.position);
            return;
        }

        state = State.WalkingToCounter;
        agent.SetDestination(queue.SlotPoint(slotIndex).position);
    }

    // Someone ahead of them left — shuffle up the line. Their device isn't
    // involved any more; it lives on the shelf.
    public void MoveToSlot(int newIndex)
    {
        slotIndex = newIndex;
        agent.SetDestination(queue.SlotPoint(slotIndex).position);
    }

    private void Update()
    {
        if (agent == null) return;

        animator.SetBool("IsWalking", agent.velocity.magnitude > 0.1f);

        // Held still for a beat after accepting, then released.
        if (hasPendingDestination && Time.time >= moveAllowedAt)
        {
            hasPendingDestination = false;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(pendingDestination);
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
                if (Arrived())
                {
                    state = State.WaitingInQueue;
                    patienceLeft = queueMax;
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
                if (!hasPendingDestination && Arrived()) state = State.Waiting;
                break;

            case State.Waiting:
                // Landed in Waiting without a spot — the floor was full when
                // they were ready to move. Keep asking.
                if (waitingSpot == null && jobAccepted && Time.time >= retryClaimAt)
                {
                    if (!TryTakeWaitingSpot()) retryClaimAt = Time.time + spotRetryInterval;
                }

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
                // Don't vanish while still mid-sentence — let the line finish first.
                if (Arrived() && bubbleTimer <= 0f) Destroy(gameObject);
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
        animator.SetTrigger("Interact");
        return identity != null ? identity.Say(CustomerIdentity.Beat.Intake) : "";
    }

    // DATA ONLY. Nothing here touches the agent, the counter slot, the waiting
    // spot, or the state enum — that's BeginWaiting(), and it doesn't run until
    // the conversation formally closes. The device still lands on the shelf on
    // the same frame you press E, so the feedback is as instant as it was.
    public string AcceptJob()
    {
        if (!CanAcceptJob || record == null) return "";

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
        }
        else
        {
            SpawnDeviceOntoShelf();
        }

        // Their number floats over them so you can find them across the room.
        if (waitingBadge != null) waitingBadge.Show(JobNumber, JobColor);

        RunOrDefer(BeginWaiting);

        animator.SetTrigger("Interact");
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
            if (shelf != null)
            {
                spawned.transform.position = shelf.position + Vector3.up * activeJob.restHeight;
                spawned.transform.rotation = shelf.rotation;
            }
            else
            {
                spawned.transform.position = slotPoint.position + Vector3.up * activeJob.restHeight;
            }
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

        ReleaseCounterSlot();

        if (!TryTakeWaitingSpot())
        {
            // Floor was full, or every free spot is unreachable. They hold
            // position near the counter and keep looking, rather than being
            // planted there permanently with nowhere to go.
            state = State.Waiting;
            retryClaimAt = Time.time + spotRetryInterval;
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
        state = State.Settling;
        MoveAfterReacting(destination);
        return true;
    }

    private bool CanReach(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    // Stand still for a moment, then walk. Without the pause they slide off
    // mid-"Interact" animation, which reads as moonwalking.
    private void MoveAfterReacting(Vector3 destination)
    {
        pendingDestination = destination;
        hasPendingDestination = true;
        moveAllowedAt = Time.time + reactionTime;

        if (agent.isOnNavMesh) agent.isStopped = true;
    }

    public string RefuseJob()
    {
        if (!CanRefuse) return "";

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

        carry.Consume();
        drinkOrdered = false;

        animator.SetTrigger("Interact");

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Completed) : "";
        FinishAndLeave(line, true);
        return line;
    }

    public string CompleteJob()
    {
        if (!JobReady) return "";

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = identity != null ? identity.TipMultiplier : 1f;

        int basePay = activeJob.Payout;
        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip, true);

        // It was in the player's hands, so make sure the shelf/bench forgets it.
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude))
            spot.Release(activeJob);

        Destroy(activeJob.gameObject);
        activeJob = null;

        animator.SetTrigger("Interact");

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Completed) : "";

        // Only bubble it if there's no panel showing the same words.
        if (conversation == null) Say(line);
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

        animator.SetTrigger("Interact");
        Say(identity != null ? identity.Say(CustomerIdentity.Beat.Reassured) : "");
    }

    // ---------- leaving ----------

    private void StormOut()
    {
        decided = true;

        // If their patience runs out mid-conversation, the panel would
        // otherwise sit there offering "[E] Take the job" while they're
        // actually walking out in disgust. Drop the deferred move first so
        // closing the panel doesn't send them to a waiting spot on the way.
        pendingHandoff = null;
        if (conversation != null) conversation.End();

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.StormedOut) : "";
        Say(line);      // bubble — they're shouting at the room, not talking to you
        LeaveAfterSpeaking(line, false);
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

        if (!happy && DayClock.Instance != null) DayClock.Instance.RecordLost();

        hasPendingDestination = false;
        if (agent.isOnNavMesh)
        {
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

    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        currentLine = line;
        bubbleTimer = RevealTime(line) + lineHoldTime;
        ForceShow();
    }

    public void ShowBubble(bool on)
    {
        if (speechBubble == null) return;

        if (!on && bubbleTimer > 0f) return;
        if (on && bubbleTimer > 0f && revealRoutine != null) return;

        if (on) ForceShow();
        else HideNow();
    }

    private void ForceShow()
    {
        if (speechBubble == null || string.IsNullOrEmpty(currentLine)) return;

        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        bool isFocused = player != null && player.Focused != null &&
                         player.Focused.GetComponent<CustomerBrain>() == this;
        if (!isFocused && state != State.Speaking) return;

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

    private void UpdateBar(float max, Color fullColor)
    {
        if (patienceBar == null) return;

        bool show = ShowFloatingBar;
        if (patienceBar.gameObject.activeSelf != show)
            patienceBar.gameObject.SetActive(show);

        if (show) patienceBar.SetFraction(patienceLeft / max, fullColor);
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
