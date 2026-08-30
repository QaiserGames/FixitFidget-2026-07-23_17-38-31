using System;
using UnityEngine;

// WHY A CUSTOMER LEFT WITHOUT BEING SERVED.
//
// "Lost" used to be a single number covering two completely different days:
// the day you over-committed and three people stormed off, and the day you
// judged you were full, declined three walk-ins, and served everyone you took.
// Those are a failure and a good decision, and the recap called them the same
// thing — so it could never answer "what screwed me today?", which is the
// question the whole upgrade loop hangs off.
//
// Only ever APPEND to this. It's headed for save data and the journal.
public enum LostReason
{
    StormedOutInQueue,   // ran out of patience before you even heard them
    StormedOutWaiting,   // you took the job and didn't get back in time
    Declined,            // you pressed Q. A choice, not a failure.
    OutOfStock,          // couldn't make their drink. Not your fault today.
    ShelfFull,           // no room to take the device
    StillInShopAtClose   // day ended with them unserved
}

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

    // Wall-clock stamp of this morning's opening, so anything logging an event
    // can say WHEN in the day it happened rather than just that it happened.
    private float dayStartedAt;

    /// <summary>Seconds since the shop opened this morning.</summary>
    public float SecondsIntoDay => Time.time - dayStartedAt;

    // Today's forgiveness, set by CustomerSpawner each morning from the
    // DayDefinition. Lives here rather than on the spawner because it's a
    // property of the DAY, and CustomerBrain shouldn't have to know that days
    // are authored on a spawner.
    //
    // Reset to 1 in StartDay, so a schedule that's removed or runs out can't
    // leave a stale multiplier applied to every customer forever.
    public float PatienceMultiplier { get; set; } = 1f;
    public bool IsOpen { get; private set; }
    public bool DayOver { get; private set; }

    // Stats for the recap.
    //
    // ⚠️ Served counts TRANSACTIONS, not people. RecordServed fires once per
    // thing delivered, so one customer with a repair and a coffee lands twice
    // — which is how a day could report "Arrived 9, Served 11" while three
    // people stormed out.
    //
    // Kept, because "how much work did I get through" is a real number worth
    // having. Renamed in the recap to say what it actually means, and joined
    // by Visitors below, which counts humans.
    public int Served { get; private set; }

    // Distinct people whose visit ended with them getting something. This is
    // the number that belongs next to Lost and Declined, because those count
    // people too and comparing them against a transaction count is nonsense.
    public int Visitors { get; private set; }

    // Passive cafe income. Patrons pay on arrival and never call RecordServed,
    // so their money reached the till while "Earned today" never moved — a
    // recap that under-reported the day by exactly $5 a head.
    public int PatronIncome { get; private set; }

    // Storm-outs ONLY. People who wanted serving and didn't get it.
    public int Lost { get; private set; }

    // You turned them away on purpose. Kept apart from Lost so restraint isn't
    // scored as failure — see the LostReason comment above.
    public int Declined { get; private set; }
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

        // Patrons too, and they need saying separately.
        //
        // DayClock deliberately does NOT wait for patrons before ending the day
        // — they're atmosphere and would keep a finished day alive forever. But
        // that also means nothing was ever clearing them, and Time.timeScale
        // goes to 0 at the recap, which freezes Time.time and therefore freezes
        // every patron's leave timer mid-count.
        //
        // Result: yesterday's customers woke up in today's cafe, still holding
        // yesterday's seats, with the remainder of yesterday's stay to run.
        // Plain Destroy is right here — a patron owns nothing but its chair,
        // and PatronBrain.OnDestroy releases that.
        PatronBrain[] stragglers = FindObjectsByType<PatronBrain>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (PatronBrain p in stragglers)
            if (p != null) Destroy(p.gameObject);

        TimeRemaining = dayLengthSeconds;
        dayStartedAt = Time.time;
        IsOpen = true;
        DayOver = false;
        Served = 0;
        Visitors = 0;
        PatronIncome = 0;
        Lost = 0;
        Declined = 0;
        PatienceMultiplier = 1f;
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

    /// <summary>One human whose visit ended with them having got something.</summary>
    public void RecordVisitorSatisfied() => Visitors++;

    /// <summary>A patron bought a coffee on the way in. No ticket, no demand.</summary>
    public void RecordPatronIncome(int amount)
    {
        PatronIncome += amount;
        Earned += amount;
    }

    // A customer left without being served. The reason decides which column it
    // lands in: a storm-out is a failure, a decline is a decision.
    public void RecordLost(LostReason reason)
    {
        switch (reason)
        {
            case LostReason.Declined:
            case LostReason.OutOfStock:
            case LostReason.ShelfFull:
                Declined++;
                break;

            default:
                Lost++;
                break;
        }
    }

    // Used by the save system on load, before the first day starts.
    public void SetDay(int day)
    {
        Day = Mathf.Max(1, day);
    }

}