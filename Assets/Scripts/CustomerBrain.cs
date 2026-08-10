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
    [SerializeField] private float wordsPerSecond = 6f;
    [SerializeField] private Nameplate nameplate;

    [Header("Reassurance")]
    [SerializeField] private float reassureAmount = 0.3f;
    [SerializeField] private float reassureCooldown = 12f;
    [SerializeField] private float reassureFalloff = 0.6f;
    [SerializeField] private float reassureTipCost = 0.15f;
    [SerializeField] private int reassureMaxUses = 3;

    [Header("Presence")]
    [SerializeField] private float presenceDrainMultiplier = 0.2f;

    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private JobMarker jobMarker;
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
    private int reassureUses;
    private int slotIndex = -1;
    private JobBase activeJob;

    // Read off the item prefab at spawn, so they can complain BEFORE we accept.
    private JobBase persona;

    private CustomerArchetype mood;
    private string currentLine = "";
    private Coroutine revealRoutine;
    private float queueMax;
    private float serviceMax;

    public string CustomerName { get; private set; } = "Customer";
    public void SetName(string n)
    {
        CustomerName = n;
        if (nameplate != null) nameplate.SetName(n);
    }

    public int JobNumber { get; private set; }
    public Color JobColor { get; private set; } = Color.white;

    public bool CanAcceptJob => state == State.WaitingInQueue;

    public bool HasJob => activeJob != null;
    public bool InService => state == State.WaitingForService;
    public string JobCardText => activeJob != null ? activeJob.JobCard : "";
    public float PatienceFraction => Mathf.Clamp01(patienceLeft / CurrentMax);
    public int SlotIndex => slotIndex;

    private float CurrentMax => state == State.WaitingForService ? serviceMax : queueMax;

    // Their item is close enough to their spot to hand over.
    private bool ItemAtCounter
    {
        get
        {
            if (activeJob == null || queue == null || slotIndex < 0) return false;

            // Being carried by the player counts — you're standing right there.
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            if (carry != null && carry.Carried == activeJob) return true;

            float dist = Vector3.Distance(activeJob.transform.position, queue.ItemSpot(slotIndex).position);
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
        (state == State.WaitingInQueue || state == State.WaitingForService)
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
            HoldCallJob call = activeJob as HoldCallJob;
            if (call == null || !call.WantsPlayerPresent) return 1f;

            if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
            if (player == null || !player.IsAtStation) return 1f;

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

        // Learn what problem we're bringing in, without spawning it yet.
        if (itemPrefab != null) persona = itemPrefab.GetComponent<JobBase>();

        HideBubble();
        if (jobMarker != null) jobMarker.Hide();

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

        // Only move their item if it's still ON the counter. Items on the bench
        // or in the player's hands stay exactly where they are.
        if (activeJob == null || queue == null) return;

        float distToOldSpot = Vector3.Distance(activeJob.transform.position, queue.ItemSpot(newIndex).position);
        bool onCounter = false;

        for (int i = 0; i < 3; i++)
        {
            if (Vector3.Distance(activeJob.transform.position, queue.ItemSpot(i).position) < 0.6f)
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
            if (bubbleTimer <= 0f) ShowBubble(false);
        }

        switch (state)
        {
            case State.WalkingToCounter:
                if (Arrived())
                {
                    state = State.WaitingInQueue;
                    patienceLeft = queueMax;
                    // The complaint: setup of the joke.
                    Say(persona != null ? persona.complaintLine : "");
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

        if (JobIdentityManager.Instance != null)
        {
            JobIdentityManager.Instance.Next(out int num, out Color col);
            JobNumber = num;
            JobColor = col;
        }

        Transform spot = queue.ItemSpot(slotIndex);
        GameObject spawned = Instantiate(itemPrefab, spot.position, spot.rotation);

        activeJob = spawned.GetComponent<JobBase>();
        if (activeJob != null) activeJob.SetOwner(this);

        // Items get the number; people get their name.
        JobMarker itemMarker = spawned.GetComponentInChildren<JobMarker>(true);
        if (itemMarker != null) itemMarker.Show(JobNumber, JobColor);
        

        animator.SetTrigger("Interact");

        // The relief beat.
        Say(activeJob != null ? activeJob.acceptedLine : "");
    }

    public void CompleteJob()
    {
        if (!JobReady) return;

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = mood != null ? mood.tipMultiplier : 1f;

        int basePay = activeJob.Payout;

        float reassurePenalty = Mathf.Clamp01(1f - reassureUses * reassureTipCost);
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult * reassurePenalty);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        if (DayClock.Instance != null) DayClock.Instance.RecordServed(basePay, tip);

        // The payoff beat — say it BEFORE the job is destroyed.
        Say(activeJob.completedLine);

        Destroy(activeJob.gameObject);
        activeJob = null;
        if (jobMarker != null) jobMarker.Hide();

        animator.SetTrigger("Interact");
        state = State.Thanking;
        thankTimer = thankDuration;
    }

    public void Reassure()
    {
        if (!CanReassure) return;

        float gain = CurrentMax * reassureAmount * Mathf.Pow(reassureFalloff, reassureUses);
        patienceLeft = Mathf.Min(patienceLeft + gain, CurrentMax);

        reassureUses++;
        reassureReadyAt = Time.time + reassureCooldown;

        animator.SetTrigger("Interact");
        if (mood != null && mood.reassuredLines != null && mood.reassuredLines.Length > 0)
            Say(mood.reassuredLines[Random.Range(0, mood.reassuredLines.Length)]);
    }

    public void ShowNameTag(bool on)
    {
        if (jobMarker == null) return;
        if (on && HasJob) jobMarker.ShowText(CustomerName, JobColor);
        else jobMarker.Hide();
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
        if (jobMarker != null) jobMarker.Hide();
        HideBubble();

        if (!happy && DayClock.Instance != null) DayClock.Instance.RecordLost();

        agent.SetDestination(exitPoint.position);
    }

    // ---------- dialogue ----------

    // Say a specific line. Duration scales with length, so long lines linger.
    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        currentLine = line;
        ShowBubble(true);

        int words = line.Split(' ').Length;
        bubbleTimer = 2f + words * 0.45f;
    }

    // Personality flavour, used when there's no job-specific line.
    private void PickLine()
    {
        if (mood == null || mood.lines == null || mood.lines.Length == 0) return;
        Say(mood.lines[Random.Range(0, mood.lines.Length)]);
    }

    public void ShowBubble(bool on)
    {
        if (speechBubble == null) return;

        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }

        if (on && !string.IsNullOrEmpty(currentLine))
        {
            speechBubble.text = currentLine;
            speechBubble.color = mood != null ? mood.moodColor : Color.white;
            speechBubble.gameObject.SetActive(true);
            revealRoutine = StartCoroutine(RevealWords());
        }
        else
        {
            speechBubble.gameObject.SetActive(false);
        }
    }

    // Pokémon-style: the line arrives a word at a time so it's readable.
    private System.Collections.IEnumerator RevealWords()
    {
        speechBubble.ForceMeshUpdate();
        int total = speechBubble.textInfo.wordCount;
        speechBubble.maxVisibleWords = 0;

        for (int i = 1; i <= total; i++)
        {
            speechBubble.maxVisibleWords = i;
            yield return new WaitForSeconds(1f / wordsPerSecond);
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