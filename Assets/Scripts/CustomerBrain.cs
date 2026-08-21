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
    public bool InService => IsWaiting;

    private float CurrentMax => IsWaiting ? serviceMax : queueMax;

    // ---------- the intake beat ----------

    public bool CanHearIntake => state == State.WaitingInQueue && !intakeGiven;
    public bool CanDecide => state == State.WaitingInQueue && intakeGiven;
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

    private bool InConversation => state == State.WaitingInQueue && intakeGiven;

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
                // Walking to their spot. Still waiting on you, so still draining —
                // but let the agent steer, don't fight it for the rotation.
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) { StormOut(); break; }
                if (!hasPendingDestination && Arrived()) state = State.Waiting;
                break;

            case State.Waiting:
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

    // ---------- player actions ----------

    public string HearIntake()
    {
        if (!CanHearIntake) return "";

        intakeGiven = true;
        animator.SetTrigger("Interact");
        return identity != null ? identity.Say(CustomerIdentity.Beat.Intake) : "";
    }

    public string AcceptJob()
    {
        if (!CanAcceptJob || record == null) return "";

        patienceLeft = serviceMax;

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

        LeaveTheCounter();

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
    private void LeaveTheCounter()
    {
        if (slotIndex >= 0 && queue != null)
        {
            queue.ReleaseSlot(this);
            slotIndex = -1;
        }

        WaitingSpot.SpotKind preferred = identity != null
            ? identity.PreferredWaitKind : WaitingSpot.SpotKind.Loiter;

        waitingSpot = WaitingArea.Instance != null
            ? WaitingArea.Instance.Claim(this, preferred) : null;

        if (waitingSpot != null)
        {
            state = State.Settling;
            MoveAfterReacting(waitingSpot.StandPoint.position);
        }
        else
        {
            // Nowhere free. They stand their ground rather than teleporting —
            // agent avoidance will nudge them out of the queue's way.
            state = State.Waiting;
        }
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

        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Declined) : "";
        LeaveAfterSpeaking(line, false);
        return line;
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
        LeaveAfterSpeaking(line, true);
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
        Say(line);
        LeaveAfterSpeaking(line, true);
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
