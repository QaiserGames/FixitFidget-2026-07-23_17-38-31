using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class CustomerBrain : MonoBehaviour
{
    public enum State { WalkingToCounter, WaitingInQueue, WaitingForService, Thanking, Leaving }

    [SerializeField] private float queuePatience = 15f;
    [SerializeField] private float servicePatience = 45f;
    [SerializeField] private float maxTipFraction = 0.6f;
    [SerializeField] private float thankDuration = 1.2f;
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float bubbleDuration = 5f;

    [Header("Reassurance")]
    [SerializeField] private float reassureAmount = 0.3f;      // first use returns this much
    [SerializeField] private float reassureCooldown = 12f;
    [SerializeField] private float reassureFalloff = 0.6f;     // each use returns 60% of the last
    [SerializeField] private float reassureTipCost = 0.15f;    // each use costs 15% of the tip
    [SerializeField] private int reassureMaxUses = 3;

    private int reassureUses;

    [Header("Presence")]
    [Tooltip("Patience drain multiplier while the player is present for a call.")]
    [SerializeField] private float presenceDrainMultiplier = 0.2f;

    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private GameObject itemPrefab;

    private State state;
    private NavMeshAgent agent;
    private Animator animator;
    private CounterQueue queue;
    private Transform exitPoint;
    private PlayerInteractor player;
    private float patienceLeft;
    private float thankTimer;
    private float bubbleTimer;
    private float reassureReadyAt;
    private int slotIndex = -1;
    private JobBase activeJob;

    private CustomerArchetype mood;
    private string currentLine = "";
    private float queueMax;
    private float serviceMax;

    public bool CanAcceptJob => state == State.WaitingInQueue;
    public bool JobReady => state == State.WaitingForService && activeJob != null && activeJob.IsComplete;

    private float CurrentMax => state == State.WaitingForService ? serviceMax : queueMax;

    public bool CanReassure =>
        (state == State.WaitingInQueue || state == State.WaitingForService)
        && Time.time >= reassureReadyAt
        && reassureUses < reassureMaxUses
        && patienceLeft < CurrentMax * 0.9f;

    // Is this job asking for the player right now? Blocks the reassure prompt
    // so it can't hijack the button from dialling or answering.
    public bool JobNeedsAttention
    {
        get
        {
            HoldCallJob call = activeJob as HoldCallJob;
            return call != null && (call.CurrentPhase == HoldCallJob.Phase.NeedsDialing ||
                                    call.CurrentPhase == HoldCallJob.Phase.Ringing);
        }
    }

    // Actively working this customer's call slows their patience right down —
    // you're both standing there listening to the same hold music.
    // Only the customer you're actually looking at gets soothed —
    // being vaguely at the counter isn't the same as attending to someone.
    private float DrainRate
    {
        get
        {
            HoldCallJob call = activeJob as HoldCallJob;
            if (call == null || !call.WantsPlayerPresent) return 1f;

            if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
            if (player == null || !player.IsAtStation) return 1f;

            // Looking at this customer, or at their phone on the counter.
            Interactable f = player.Focused;
            if (f == null) return 1f;

            bool lookingAtMe = f.GetComponent<CustomerInteractable>() != null &&
                               f.GetComponent<CustomerBrain>() == this;
            bool lookingAtMyPhone = f.GetComponentInParent<HoldCallJob>() == call;

            return (lookingAtMe || lookingAtMyPhone) ? presenceDrainMultiplier : 1f;
        }
    }

    public void Init(CounterQueue counterQueue, Transform exit, CustomerArchetype archetype)
    {
        queue = counterQueue;
        exitPoint = exit;
        mood = archetype;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        float mult = mood != null ? mood.patienceMultiplier : 1f;
        queueMax = queuePatience * mult;
        serviceMax = servicePatience * mult;

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
    }

    private void Update()
    {
        if (agent == null) return;

        animator.SetBool("IsWalking", agent.velocity.magnitude > 0.1f);

        if (bubbleTimer > 0f)
        {
            bubbleTimer -= Time.deltaTime;
            if (bubbleTimer <= 0f) ShowBubble(false);
        }

        switch (state)
        {
            case State.WalkingToCounter:
                if (Arrived())
                {
                    state = State.WaitingInQueue;
                    patienceLeft = queueMax;
                    PickLine();
                }
                break;

            case State.WaitingInQueue:
                FaceSlot();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(queueMax, Color.green);
                if (patienceLeft <= 0f) Leave(false);
                break;

            case State.WaitingForService:
                FaceSlot();
                patienceLeft -= Time.deltaTime * DrainRate;
                UpdateBar(serviceMax, new Color(0.3f, 0.7f, 1f));
                if (patienceLeft <= 0f) Leave(false);
                break;

            case State.Thanking:
                FaceSlot();
                thankTimer -= Time.deltaTime;
                if (thankTimer <= 0f) Leave(true);
                break;

            case State.Leaving:
                if (Arrived()) Destroy(gameObject);
                break;
        }
    }

    public void AcceptJob()
    {
        if (!CanAcceptJob) return;

        state = State.WaitingForService;
        patienceLeft = serviceMax;

        // Small random offset so two items can't land in exactly the same place.
        Transform spot = queue.ItemSpot(slotIndex);
        Vector3 offset = new Vector3(Random.Range(-0.06f, 0.06f), 0f, Random.Range(-0.04f, 0.04f));
        GameObject spawned = Instantiate(itemPrefab, spot.position + offset, spot.rotation);

        activeJob = spawned.GetComponent<JobBase>();
        if (activeJob != null) activeJob.SetOwner(this);

        animator.SetTrigger("Interact");
        ShowBubble(false);
    }

    public void CompleteJob()
    {
        if (!JobReady) return;

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = mood != null ? mood.tipMultiplier : 1f;

        int basePay = activeJob.Payout;

        // Every reassurance eats into what they'll tip you.
        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip);

        Debug.Log($"[{(mood != null ? mood.archetypeName : "?")}] base ${basePay} + tip ${tip}");

        Destroy(activeJob.gameObject);
        activeJob = null;

        animator.SetTrigger("Interact");
        state = State.Thanking;
        thankTimer = thankDuration;
    }

    public void Reassure()
    {
        if (!CanReassure) return;

        // Diminishing returns: each use gives back less than the last.
        float gain = CurrentMax * reassureAmount * Mathf.Pow(reassureFalloff, reassureUses);
        patienceLeft = Mathf.Min(patienceLeft + gain, CurrentMax);

        reassureUses++;
        reassureReadyAt = Time.time + reassureCooldown;

        animator.SetTrigger("Interact");
        PickLine();
    }

    private void Leave(bool happy)
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
        HideBubble();

        if (!happy && DayClock.Instance != null) DayClock.Instance.RecordLost();

        agent.SetDestination(exitPoint.position);
    }

    // ---------- dialogue ----------

    private void PickLine()
    {
        if (mood == null || mood.lines == null || mood.lines.Length == 0) return;

        currentLine = mood.lines[Random.Range(0, mood.lines.Length)];
        ShowBubble(true);
        bubbleTimer = bubbleDuration;
    }

    public void ShowBubble(bool on)
    {
        if (speechBubble == null) return;

        if (on && !string.IsNullOrEmpty(currentLine))
        {
            speechBubble.text = currentLine;
            speechBubble.color = mood != null ? mood.moodColor : Color.white;
            speechBubble.gameObject.SetActive(true);
        }
        else
        {
            speechBubble.gameObject.SetActive(false);
        }
    }

    private void HideBubble()
    {
        currentLine = "";
        ShowBubble(false);
        bubbleTimer = 0f;
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