using UnityEngine;

[DisallowMultipleComponent]
public sealed class HumanFault : MonoBehaviour
{
    [SerializeField] private HumanFaultScenario scenario;
    private HumanConversationRun run;
    private JobBase job;
    public HumanConversationRun Run { get { EnsureInitialized(); return run; } }
    public int TotalTasks => Mathf.Max(1, Run.Count);
    public int RemainingTasks => TotalTasks - Run.Credits;
    public bool Finished => Run.Finished;

    private void Awake() => EnsureInitialized();
    public void EnsureInitialized()
    {
        if (run != null) return;
        job = GetComponentInParent<JobBase>();
        run = scenario != null ? scenario.CreateRun() : new HumanConversationRun(null, null);
        if (!run.IsValid) Debug.LogError("Human fault configuration: " + run.Error, this);
    }

    public bool CanTalkWith(CustomerBrain customer) => isActiveAndEnabled && Run.IsValid && !Finished
        && job != null && job.Owner == customer && customer != null && customer.InService && !customer.IsLeaving
        && job.Record != null && job.Record.faultType == FaultType.Human
        && Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);

    public bool Choose(CustomerBrain customer, int index) => CanTalkWith(customer) && Run.Choose(index);
}
