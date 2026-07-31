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
    [SerializeField] private PatienceBar patienceBar;
    [SerializeField] private TMP_Text speechBubble;
    [SerializeField] private GameObject itemPrefab;

    private State state;
    private NavMeshAgent agent;
    private Animator animator;
    private CounterQueue queue;
    private Transform exitPoint;
    private float patienceLeft;
    private float thankTimer;
    private float bubbleTimer;
    private int slotIndex = -1;
    private RepairJob activeJob;

    private CustomerArchetype mood;
    private string currentLine = "";
    private float queueMax;
    private float serviceMax;

    public bool CanAcceptJob => state == State.WaitingInQueue;
    public bool JobReady => state == State.WaitingForService && activeJob != null && activeJob.IsComplete;

    public void Init(CounterQueue counterQueue, Transform exit, CustomerArchetype archetype)
    {
        queue = counterQueue;
        exitPoint = exit;
        mood = archetype;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Personality scales both meters.
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

        // Bubble auto-hides after a few seconds, but the line stays retrievable.
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
                    PickLine();               // they greet you on arrival
                }
                break;

            case State.WaitingInQueue:
                FaceSlot();
                patienceLeft -= Time.deltaTime;
                UpdateBar(queueMax, Color.green);
                if (patienceLeft <= 0f) Leave(false);
                break;

            case State.WaitingForService:
                FaceSlot();
                patienceLeft -= Time.deltaTime;
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

        Transform spot = queue.ItemSpot(slotIndex);
        GameObject spawned = Instantiate(itemPrefab, spot.position, spot.rotation);
        activeJob = spawned.GetComponent<RepairJob>();

        animator.SetTrigger("Interact");
        ShowBubble(false);      // tuck it away, but keep the line retrievable
    }

    public void CompleteJob()
    {
        if (!JobReady) return;

        float speedFraction = Mathf.Clamp01(patienceLeft / serviceMax);
        float tipMult = mood != null ? mood.tipMultiplier : 1f;

        int basePay = activeJob.Payout;
        int tip = Mathf.RoundToInt(basePay * maxTipFraction * speedFraction * tipMult);

        ShopEconomy.Instance.AddMoney(basePay + tip);
        Debug.Log($"[{(mood != null ? mood.archetypeName : "?")}] base ${basePay} + tip ${tip}");

        Destroy(activeJob.gameObject);
        activeJob = null;

        animator.SetTrigger("Interact");
        state = State.Thanking;
        thankTimer = thankDuration;
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

    // Public so the crosshair can re-show what someone said.
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