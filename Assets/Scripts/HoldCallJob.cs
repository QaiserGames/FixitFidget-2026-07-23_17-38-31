using UnityEngine;

public class HoldCallJob : JobBase
{
    [SerializeField, Min(1f)] private float holdMin = 20f;
    [SerializeField, Min(1f)] private float holdMax = 30f;
    [SerializeField, Min(1f)] private float ringWindow = 15f;
    private readonly HoldCallRun run = new();
    public override JobFamily Family => JobFamily.Bureaucratic;
    public override bool IsComplete => run.Phase == HoldCallRun.State.Done;
    public HoldCallRun.State CurrentPhase => run.Phase;
    public float SecondsRemaining => run.Remaining;
    public int MissedCalls => run.MissedCalls;
    public bool WantsPlayerPresent => CurrentPhase == HoldCallRun.State.Ringing;
    public bool CanOperate => isActiveAndEnabled && Owner != null && Owner.InService && !Owner.IsLeaving
        && Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);
    public bool CanActivate => CanOperate && (CurrentPhase == HoldCallRun.State.NeedsDialing || CurrentPhase == HoldCallRun.State.Ringing);

    private void Start()
    {
        if (GetComponent<HoldCallPresentation>() == null) gameObject.AddComponent<HoldCallPresentation>();
    }
    private void Update()
    {
        if (CanOperate) run.Tick(Time.deltaTime);
    }
    public void Activate()
    {
        if (!CanActivate) return;
        if (CurrentPhase == HoldCallRun.State.Ringing) run.Answer();
        else run.Dial(Random.Range(Mathf.Max(1f, holdMin), Mathf.Max(1f, holdMin, holdMax)), Mathf.Max(1f, ringWindow));
    }
    public string StatusLine => CurrentPhase switch
    {
        HoldCallRun.State.NeedsDialing => MissedCalls > 0 ? "Missed call — redial support" : "Call support",
        HoldCallRun.State.Connecting => "Connecting...",
        HoldCallRun.State.OnHold => $"On hold · {Mathf.CeilToInt(SecondsRemaining)}s\nYou can leave and do another job.",
        HoldCallRun.State.Ringing => $"Support is ready! Answer · {Mathf.CeilToInt(SecondsRemaining)}s",
        _ => "Connection restored. Return the phone."
    };
}
