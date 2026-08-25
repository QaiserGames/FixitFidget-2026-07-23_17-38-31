using System;
using UnityEngine;

public class DayClock : MonoBehaviour
{
    public static DayClock Instance { get; private set; }

    [SerializeField] private float dayLengthSeconds = 180f;   // 3 min for testing, 7 for ship
    [SerializeField] private int startingDay = 1;

    [Tooltip("Deadlock backstop. Only counts once EVERY remaining customer is " +
             "walking out — while anyone is still being served it resets, so " +
             "it can never cut a repair short. Generous on purpose: by the time " +
             "this fires, something is genuinely wrong.")]
    [SerializeField] private float closingGrace = 90f;

    private float closedAt;

    public int Day { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsOpen { get; private set; }
    public bool DayOver { get; private set; }

    // Stats for the recap.
    public int Served { get; private set; }
    public int Lost { get; private set; }
    public int Repairs { get; private set; }
    public int Drinks { get; private set; }
    public int Tips { get; private set; }
    public int Earned { get; private set; }

    // Grade tally, so the recap can tell you HOW you did, not just how much.
    public int Perfect { get; private set; }
    public int Good { get; private set; }
    public int Passable { get; private set; }

    public event Action OnDayEnded;

    private void Awake()
    {
        Instance = this;
        Day = startingDay;
        StartDay();
    }

    public void StartDay()
    {
        // A new day always begins with an empty shop.
        //
        // Stated as a rule here rather than as cleanup buried in an error path,
        // because it's true for BOTH ways a day can end: normally, or by the
        // closing grace expiring on someone who couldn't reach the door.
        //
        // ForceRemove rather than Destroy — a customer's device is a separate
        // object holding an intake shelf slot, and only ForceRemove gives it
        // back. See the comment on CustomerBrain.ForceRemove.
        CustomerBrain[] leftovers = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (CustomerBrain c in leftovers)
            if (c != null) c.ForceRemove();

        TimeRemaining = dayLengthSeconds;
        IsOpen = true;
        DayOver = false;
        Served = 0;
        Lost = 0;
        Repairs = 0;
        Drinks = 0;
        Tips = 0;
        Earned = 0;
        Perfect = 0;
        Good = 0;
        Passable = 0;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (DayOver) return;

        if (IsOpen)
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsOpen = false;      // last orders — stop accepting new arrivals
                closedAt = Time.time;
            }
            return;
        }

        // Closed: wait for everyone still inside to finish up.
        CustomerBrain[] remaining = FindObjectsByType<CustomerBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (remaining.Length == 0) { EndDay(); return; }

        // THE BUG THIS FIXES: the grace used to count wall-clock time from
        // closing, so it fired on customers who were perfectly fine — just
        // still being served. With a 45s grace and 45-90s repairs, anyone
        // accepted near closing got cut off mid-job and the day ended while
        // the player was working. That's most days, not an edge case.
        //
        // The grace is a deadlock backstop, so it must only measure DEADLOCK.
        // While anyone is still waiting on you, the clock resets and never
        // expires. It only counts down once everyone left is walking out.
        //
        // This can't hang: patience always drains — even mid-conversation it's
        // 0.1x, never zero — so a waiting customer always resolves itself by
        // being served or storming off. And someone who can't reach the door is
        // already handled by CustomerBrain.leaveTimeout.
        foreach (CustomerBrain c in remaining)
        {
            if (c == null || c.IsLeaving) continue;
            closedAt = Time.time;    // real work in progress — hold the door
            return;
        }

        // THE HANG THIS PREVENTS: a customer is only destroyed when they reach
        // the exit. One who can't path there — wedged in furniture, stranded on
        // a NavMesh island after a re-bake — means this count never reaches zero,
        // the recap never appears, and the only way out of the game is to kill it.
        //
        // Never wait forever on a thing that can silently fail.
        if (Time.time - closedAt < closingGrace) return;

        Debug.LogWarning($"[DayClock] Closing grace of {closingGrace}s elapsed with " +
                         $"{remaining.Length} customer(s) still in the shop — they " +
                         $"couldn't reach the exit. Ending the day anyway; they'll be " +
                         $"cleared when the next day starts. If this repeats, check " +
                         $"the NavMesh around the door.");

        // Deliberately NOT removing them here. They're cleared by StartDay, so
        // the recap you're about to read still shows the shop as it actually
        // was — people standing in it included.
        EndDay();
    }

    private void EndDay()
    {
        DayOver = true;
        Time.timeScale = 0f;
        OnDayEnded?.Invoke();
    }

    public void NextDay()
    {
        Day++;
        StartDay();
    }

    // Called by CustomerBrain. A drink is not a repair — counting them
    // together made the recap claim you'd fixed things you hadn't.
    public void RecordServed(int basePay, int tip, bool wasRepair,
                             JobGrade grade = JobGrade.Perfect)
    {
        Served++;
        if (wasRepair)
        {
            Repairs++;
            switch (grade)
            {
                case JobGrade.Perfect:  Perfect++;  break;
                case JobGrade.Good:     Good++;     break;
                case JobGrade.Passable: Passable++; break;
            }
        }
        else Drinks++;

        Tips += tip;
        Earned += basePay + tip;
    }

    public void RecordLost() => Lost++;

    // Used by the save system on load, before the first day starts.
    public void SetDay(int day)
    {
        Day = Mathf.Max(1, day);
    }

}