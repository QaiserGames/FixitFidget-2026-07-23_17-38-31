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
    [Tooltip("Words revealed per second. Lower = slower, easier to read.")]
    [SerializeField] private float wordsPerSecond = 3f;
    [Tooltip("How long the finished line stays on screen after the last word.")]
    [SerializeField] private float lineHoldTime = 2.5f;

    [Header("Reassurance")]
    [SerializeField] private float reassureAmount = 0.3f;
    [SerializeField] private float reassureCooldown = 12f;
    [SerializeField] private float reassureFalloff = 0.6f;
    [SerializeField] private float reassureTipCost = 0.15f;
    [SerializeField] private int reassureMaxUses = 3;

    [Header("Presence")]
    [Tooltip("Drain multiplier while you're actively listening to them or deciding.")]
    [SerializeField] private float conversationDrainMultiplier = 0.1f;
    [Tooltip("Drain multiplier while present for their hold call.")]
    [SerializeField] private float presenceDrainMultiplier = 0.2f;

    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private CustomerIdentity identity;

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
    private float decisionReadyAt;
    private bool departHappy = true;

    private string currentLine = "";
    private Coroutine revealRoutine;
    private float queueMax;
    private float serviceMax;

    public string CustomerName => identity != null ? identity.DisplayName : "Customer";
    public Job Record => record;
    public int JobNumber { get; private set; }
    public Color JobColor { get; private set; } = Color.white;

    public bool HasJob => activeJob != null;
    public bool InService => state == State.WaitingForService;
    public string JobCardText => record != null ? record.faultDescription : "";
    public float PatienceFraction => Mathf.Clamp01(patienceLeft / CurrentMax);
    public int SlotIndex => slotIndex;

    private float CurrentMax => state == State.WaitingForService ? serviceMax : queueMax;

    // ---------- the intake beat ----------

    public bool CanHearIntake => state == State.WaitingInQueue && !intakeGiven;

    public bool CanDecide =>
        state == State.WaitingInQueue && intakeGiven && Time.time >= decisionReadyAt;

    public bool CanAcceptJob => CanDecide;
    public bool CanRefuse => CanDecide;

    // Listening to them, or weighing the decision, both count as being served.
    private bool InConversation => state == State.WaitingInQueue && intakeGiven;

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

    // Reassurance is only for people whose job we've already taken.
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
            // Being listened to IS being served.
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

        if (identity != null && record != null) identity.SetDevice(record.deviceName);

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
                currentLine = "";      // spent — nothing left to re-show
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

            // Standing still to finish saying something before walking off.
            case State.Speaking:
                FaceSlot();
                speakTimer -= Time.deltaTime;
                if (speakTimer <= 0f) Depart(departHappy);
                break;

            case State.Leaving:
                if (Arrived()) Destroy(gameObject);
                break;
        }
    }

    // ---------- the four player actions ----------

    public void HearIntake()
    {
        if (!CanHearIntake) return;

        intakeGiven = true;
        string line = identity != null ? identity.Say(CustomerIdentity.Beat.Intake) : "";
        Say(line);

        // Can't decide until they've actually finished the sentence.
        decisionReadyAt = Time.time + RevealTime(line);
        animator.SetTrigger("Interact");
    }

    public void AcceptJob()
    {
        if (!CanAcceptJob || record == null) return;

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

        animator.SetTrigger("Interact");
        Say(identity != null ? identity.Say(CustomerIdentity.Beat.Accepted) : "");
    }

    public void RefuseJob()
    {
        if (!CanRefuse) return;
        SpeakThenLeave(identity != null ? identity.Say(CustomerIdentity.Beat.Declined) : "", false);
    }

    public void CompleteJob()
    {
        if (!JobReady) return;

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
        SpeakThenLeave(identity != null ? identity.Say(CustomerIdentity.Beat.Completed) : "", true);
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
        SpeakThenLeave(identity != null ? identity.Say(CustomerIdentity.Beat.StormedOut) : "", false);
    }

    // Stand still, finish the sentence, THEN walk away.
    private void SpeakThenLeave(string line, bool happy)
    {
        Say(line);
        departHappy = happy;
        speakTimer = Mathf.Max(RevealTime(line) + 0.6f, 1.2f);
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

    // How long the word-by-word reveal takes.
    private float RevealTime(string line)
    {
        if (string.IsNullOrEmpty(line)) return 0f;
        return line.Split(' ').Length / Mathf.Max(wordsPerSecond, 0.5f);
    }

    // A deliberate new utterance ALWAYS interrupts whatever came before.
    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        currentLine = line;
        bubbleTimer = RevealTime(line) + lineHoldTime;
        ForceShow();
    }

    // Focus-driven. Can't restart a line in progress, can't hide one either.
    public void ShowBubble(bool on)
    {
        if (speechBubble == null) return;

        if (!on && bubbleTimer > 0f) return;                    // looking away never cuts a line
        if (on && bubbleTimer > 0f && revealRoutine != null) return;   // nor restarts one

        if (on) ForceShow();
        else HideNow();
    }

    private void ForceShow()
    {
        if (speechBubble == null || string.IsNullOrEmpty(currentLine)) return;

        // Only the customer the player is dealing with speaks aloud.
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
        int total = speechBubble.textInfo.wordCount;
        speechBubble.maxVisibleWords = 0;

        for (int i = 1; i <= total; i++)
        {
            speechBubble.maxVisibleWords = i;
            yield return new WaitForSeconds(1f / Mathf.Max(wordsPerSecond, 0.5f));
        }
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

    private void UpdateBar(float max, Color fullColor)
    {
        if (patienceBar != null) patienceBar.SetFraction(patienceLeft / max, fullColor);
    }

    private bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
    }
}