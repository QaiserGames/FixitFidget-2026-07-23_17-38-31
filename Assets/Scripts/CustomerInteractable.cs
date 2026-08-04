using UnityEngine;

public class CustomerInteractable : Interactable
{
    private CustomerBrain brain;

    private void Awake()
    {
        brain = GetComponent<CustomerBrain>();
    }

    // Reassure is deliberately NOT offered while their job needs attention —
    // otherwise it hijacks the button from the real action.
    public override bool IsAvailable =>
        brain != null && (brain.CanAcceptJob || brain.JobReady ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanAcceptJob) return "Accept job";
            if (brain.JobReady) return "Hand it back";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (brain.CanAcceptJob) brain.AcceptJob();
        else if (brain.JobReady) brain.CompleteJob();
        else if (brain.CanReassure) brain.Reassure();
    }

    public override void SetFocused(bool focused)
    {
        if (brain != null) brain.ShowBubble(focused);
    }
}