using System;
using UnityEngine;

public class DayClock : MonoBehaviour
{
    public static DayClock Instance { get; private set; }

    [SerializeField] private float dayLengthSeconds = 180f;   // 3 min for testing, 7 for ship
    [SerializeField] private int startingDay = 1;

    public int Day { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsOpen { get; private set; }
    public bool DayOver { get; private set; }

    // Stats for the recap.
    public int Served { get; private set; }
    public int Lost { get; private set; }
    public int Repairs { get; private set; }
    public int Tips { get; private set; }
    public int Earned { get; private set; }

    public event Action OnDayEnded;

    private void Awake()
    {
        Instance = this;
        Day = startingDay;
        StartDay();
    }

    public void StartDay()
    {
        TimeRemaining = dayLengthSeconds;
        IsOpen = true;
        DayOver = false;
        Served = 0;
        Lost = 0;
        Repairs = 0;
        Tips = 0;
        Earned = 0;
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
            }
            return;
        }

        // Closed: wait for everyone still inside to finish up.
        if (FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0)
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

    // Called by CustomerBrain.
    public void RecordServed(int basePay, int tip)
    {
        Served++;
        Repairs++;
        Tips += tip;
        Earned += basePay + tip;
    }

    public void RecordLost() => Lost++;
}