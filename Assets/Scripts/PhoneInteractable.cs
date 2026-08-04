using UnityEngine;

public class PhoneInteractable : Interactable
{
    private HoldCallJob job;

    private void Awake()
    {
        job = GetComponentInParent<HoldCallJob>();
    }

    public override bool IsAvailable =>
        job != null &&
        (job.CurrentPhase == HoldCallJob.Phase.NeedsDialing ||
         job.CurrentPhase == HoldCallJob.Phase.Ringing);

    public override string Prompt
    {
        get
        {
            if (job == null) return "";
            if (job.CurrentPhase == HoldCallJob.Phase.NeedsDialing) return "Dial support";
            if (job.CurrentPhase == HoldCallJob.Phase.Ringing) return "Answer!";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (job != null) job.Activate();
    }
}