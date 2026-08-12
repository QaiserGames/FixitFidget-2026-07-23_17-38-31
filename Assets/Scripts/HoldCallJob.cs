using System.Collections.Generic;
using UnityEngine;

public class HoldCallJob : JobBase
{
    public enum Phase { NeedsDialing, InTree, OnHold, Ringing, Done }

    [SerializeField] private float holdMin = 60f;
    [SerializeField] private float holdMax = 120f;
    [SerializeField] private float ringWindow = 15f;
    [SerializeField] private float wrongPressPenalty = 8f;

    [Tooltip("Departments the tree can offer. The goal is picked from these.")]
    [SerializeField] private string[] departments =
    {
        "Billing", "Warranty claims", "Repairs and servicing",
        "Account security", "Parts ordering", "Technical support",
        "Returns", "Shipping enquiries", "Business accounts"
    };

    public override JobFamily Family => JobFamily.Bureaucratic;
    public override bool IsComplete => phase == Phase.Done;

    public Phase CurrentPhase => phase;
    public string Goal { get; private set; }

    // True whenever the player being present should slow the customer's patience.
    public bool WantsPlayerPresent =>
        phase == Phase.InTree || phase == Phase.OnHold || phase == Phase.Ringing;

    private Phase phase = Phase.NeedsDialing;
    private float timer;
    private float penaltyTimer;

    private string[][] levels = new string[2][];
    private int[] correctIndex = new int[2];
    private int currentLevel;

    private void Awake()
    {
        Goal = departments[Random.Range(0, departments.Length)];
        
    }

    private void Update()
    {
        if (penaltyTimer > 0f)
        {
            penaltyTimer -= Time.deltaTime;
            return;
        }

        switch (phase)
        {
            case Phase.OnHold:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    phase = Phase.Ringing;
                    timer = ringWindow;
                }
                break;

            case Phase.Ringing:
                timer -= Time.deltaTime;
                if (timer <= 0f) phase = Phase.NeedsDialing;   // hung up, redial
                break;
        }
    }

    public void Activate()
    {
        if (phase == Phase.NeedsDialing) Dial();
        else if (phase == Phase.Ringing) phase = Phase.Done;
    }

    private void Dial()
    {
        BuildTree();
        currentLevel = 0;
        phase = Phase.InTree;
    }

    // Fresh tree every call. Options are drawn WITHOUT replacement so nothing
    // repeats, and the goal never appears as a decoy.
    private void BuildTree()
    {
        for (int level = 0; level < levels.Length; level++)
        {
            int count = Random.Range(3, 5);

            // Pool of decoys: everything except the goal.
            List<string> pool = new List<string>();
            foreach (string d in departments)
                if (d != Goal) pool.Add(d);

            List<string> options = new List<string>();
            for (int i = 0; i < count - 1 && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                options.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            // Plant the correct entry in a random slot.
            string correctLabel = level == levels.Length - 1 ? Goal : "More options";
            int correct = Random.Range(0, options.Count + 1);
            options.Insert(correct, correctLabel);

            correctIndex[level] = correct;
            levels[level] = options.ToArray();
        }
    }

    public void PressNumber(int number)
    {
        if (phase != Phase.InTree || penaltyTimer > 0f) return;

        int index = number - 1;
        if (index < 0 || index >= levels[currentLevel].Length) return;

        if (index == correctIndex[currentLevel])
        {
            currentLevel++;
            if (currentLevel >= levels.Length)
            {
                phase = Phase.OnHold;
                timer = Random.Range(holdMin, holdMax);
            }
        }
        else
        {
            currentLevel = 0;
            penaltyTimer = wrongPressPenalty;
        }
    }

    public string StatusLine
    {
        get
        {
            if (penaltyTimer > 0f)
                return $"\"Returning you to the main menu...\"  ({penaltyTimer:F0}s)";

            switch (phase)
            {
                case Phase.NeedsDialing:
                    return $"Needs: {Goal}   [E] Dial support";

                case Phase.InTree:
                    string s = $"\"You need: {Goal}\"\n";
                    for (int i = 0; i < levels[currentLevel].Length; i++)
                        s += $"   Press {i + 1} for {levels[currentLevel][i]}\n";
                    return s;

                case Phase.OnHold:
                    return $"On hold...  {timer:F0}s   (you can walk away)";

                case Phase.Ringing:
                    return $"THEY PICKED UP!  [E] Answer   ({timer:F0}s)";

                default:
                    return "Call resolved.";
            }
        }
    }
}