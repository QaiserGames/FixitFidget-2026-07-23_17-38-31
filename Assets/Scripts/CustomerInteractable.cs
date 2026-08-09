using UnityEngine;

public class CustomerInteractable : Interactable
{
    private CustomerBrain brain;

    private void Awake()
    {
        brain = GetComponent<CustomerBrain>();
    }

    public override bool IsAvailable =>
        brain != null && (brain.CanAcceptJob || brain.JobReady || brain.JobFixedButAway ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanAcceptJob) return "Accept job";
            if (brain.JobReady) return "Hand it back";
            if (brain.JobFixedButAway) return "Bring their item back";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (brain.CanAcceptJob) brain.AcceptJob();
        else if (brain.JobReady) brain.CompleteJob();
        else if (brain.CanReassure) brain.Reassure();
        // JobFixedButAway deliberately does nothing — the prompt is the instruction.
    }
}