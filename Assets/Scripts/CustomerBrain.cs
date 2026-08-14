using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class CustomerBrain : MonoBehaviour
{
    public enum State { WalkingToCounter, WaitingInQueue, WaitingForService, Speaking, Leaving }

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

    [Header("Presence")]
    [SerializeField] private float conversationDrainMultiplier = 0.1f;
    [SerializeField] private float presenceDrainMultiplier = 0.2f;

    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private CustomerIdentity identity;
    [SerializeField] private Transform lookTarget;

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
    public bool InService => state == State.WaitingForService;
    public string JobCardText => record != null ? record.Detail : "";
    public float PatienceFraction => Mathf.Clamp01(patienceLeft / CurrentMax);
    public int SlotIndex => slotIndex;

    private float CurrentMax => state == State.WaitingForService ? serviceMax : queueMax;

    // ---------- the intake beat ----------

    public bool CanHearIntake => state == State.WaitingInQueue && !intakeGiven;
    public bool CanDecide => state == State.WaitingInQueue && intakeGiven;
    public bool CanRefuse => CanDecide;

    // A drink order can only be accepted if we can actually make it.
    public bool CanAcceptJob
    {
        get
        {
            if (!CanDecide) return false;
            if (record != null && record.kind == JobKind.Drink)
                return ShopInventory.Instance != null && ShopInventory.Instance.CanMake(record.drink);
            return true;
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
            if (!drinkOrdered || state != State.WaitingForService) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry == null || !carry.IsCarrying) return false;

            DrinkJob drink = carry.Carried as DrinkJob;
            if (drink == null || record == null) return false;

            // Any latte will do — including one abandoned by someone who left.
            return drink.Drink == record.drink;
        }
    }

    // ---------- handback ----------

    private bool ItemAtCounter
    {
        get
        {
            if (activeJob == null || queue == null || slotIndex < 0) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry != null && carry.Carried == activeJob) return true;

            float dist = Vector3.Distance(activeJob.transform.position,
                                          queue.ItemSpot(slotIndex).position);
            return dist < 1.2f;
        }
    }

    public bool JobReady =>
        state == State.WaitingForService && activeJob != null &&
        activeJob.IsComplete && ItemAtCounter;

    public bool JobFixedButAway =>
        state == State.WaitingForService && activeJob != null &&
        activeJob.IsComplete && !ItemAtCounter;

    public bool CanReassure =>
        state == State.WaitingForService
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

            HoldCallJob call = activeJob as HoldCallJob;
            if (call == null || !call.WantsPlayerPresent) return 1f;

            if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
            if (player == null || !player.IsAtStation) return 1f;

            Interactable f = player.Focused;
            if (f == null) return 1f;

            bool lookingAtMe = f.GetComponent<CustomerBrain>() == this;
            bool lookingAtMyPhone = f.GetComponentInParent<HoldCallJob>() == call;

            return (lookingAtMe || lookingAtMyPhone) ? presenceDrainMultiplier : 1f;
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

    public void MoveToSlot(int newIndex)
    {
        slotIndex = newIndex;
        agent.SetDestination(queue.SlotPoint(slotIndex).position);

        if (activeJob == null || queue == null) return;

        bool onCounter = false;
        for (int i = 0; i < 3; i++)
        {
            if (Vector3.Distance(activeJob.transform.position, queue.ItemSpot(i).position) < 1.2f)
            {
                onCounter = true;
                break;
            }
        }

        if (onCounter) activeJob.transform.position = queue.ItemSpot(slotIndex).position;
    }

    private void Update()
    {
        if (agent == null) return;

        animator.SetBool("IsWalking", agent.velocity.magnitude > 0.1f);

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
                FaceSlot();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(queueMax, Color.green);
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.WaitingForService:
                FaceSlot();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) StormOut();
                break;

            case State.Speaking:
                FaceSlot();
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

        state = State.WaitingForService;
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
            // Nothing spawns. They wait at an empty counter while you go make it.
            drinkOrdered = true;
            drinkStarted = false;
        }
        else
        {
            Transform spot = queue.ItemSpot(slotIndex);
            GameObject spawned = Instantiate(record.devicePrefab, spot.position, spot.rotation);

            DeviceDefinition dev = spawned.GetComponent<DeviceDefinition>();
            if (dev != null) dev.ApplyFault(record.faultIndex);

            activeJob = spawned.GetComponent<JobBase>();
            if (activeJob != null)
            {
                activeJob.SetOwner(this);
                activeJob.Configure(record);
                spawned.transform.position = spot.position + Vector3.up * activeJob.restHeight;
            }

            JobMarker itemMarker = spawned.GetComponentInChildren<JobMarker>(true);
            if (itemMarker != null) itemMarker.Show(JobNumber, JobColor);
        }

        animator.SetTrigger("Interact");
        return identity != null ? identity.Say(CustomerIdentity.Beat.Accepted) : "";
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
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip);

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
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip);

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
            Destroy(activeJob.gameObject);
            activeJob = null;
        }

        drinkOrdered = false;

        if (slotIndex >= 0)
        {
            queue.ReleaseSlot(this);
            slotIndex = -1;
        }

        if (patienceBar != null) patienceBar.gameObject.SetActive(false);

        if (!happy && DayClock.Instance != null) DayClock.Instance.RecordLost();

        agent.SetDestination(exitPoint.position);
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

    private void FaceSlot()
    {
        if (slotIndex < 0) return;
        Quaternion target = queue.SlotPoint(slotIndex).rotation;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

   // Visible whenever they're actively waiting on you — queue or service.
    // Hidden only while walking in, and once they've been dealt with.
    private bool ShowFloatingBar =>
        state == State.WaitingInQueue || state == State.WaitingForService;

    private void UpdateBar(float max, Color fullColor)
    {
        if (patienceBar == null) return;

        bool show = ShowFloatingBar;
        if (patienceBar.gameObject.activeSelf != show)
            patienceBar.gameObject.SetActive(show);

        if (show) patienceBar.SetFraction(patienceLeft / max, fullColor);
    }

    private bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
    }
}