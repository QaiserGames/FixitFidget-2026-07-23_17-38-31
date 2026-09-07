using UnityEngine;

[DisallowMultipleComponent]
public sealed class HumanFault : MonoBehaviour
{
    [SerializeField] private string completionLine = "It's ringing! ...That was the switch, wasn't it?";
    [Tooltip("Optional authored presentation, e.g. a Blender model wrapped in a Unity prefab. Empty uses the prototype.")]
    [SerializeField] private CounterPhoneModel presentationPrefab;
    public CounterPhoneModel PresentationPrefab => presentationPrefab;
    private JobBase job;
    public int TotalTasks => 1;
    public int RemainingTasks => Finished ? 0 : 1;
    public bool Finished { get; private set; }
    public string CompletionLine => completionLine;
    public JobBase Job { get { EnsureInitialized(); return job; } }

    private void Awake() => EnsureInitialized();
    public void EnsureInitialized()
    {
        if (job == null) job = GetComponentInParent<JobBase>();
    }

    public bool CanFix(CustomerBrain customer) => isActiveAndEnabled && !Finished
        && Job != null && Job.Owner == customer && customer != null && customer.InService && !customer.IsLeaving
        && job.Record != null && job.Record.faultType == FaultType.Human
        && Time.timeScale > 0f && !(DayClock.Instance != null && DayClock.Instance.DayOver);

    public bool Flip(CustomerBrain customer)
    {
        if (!CanFix(customer)) return false;
        Finished = true;
        return true;
    }
}
