using UnityEngine;

public class CustomerInteractable : Interactable
{
    private CustomerBrain brain;

    private void Awake()
    {
        brain = GetComponent<CustomerBrain>();
    }

    public override bool IsAvailable =>
        brain != null && (brain.CanHearIntake || brain.CanDecide || brain.JobReady ||
                          brain.JobFixedButAway ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanHearIntake) return "Hear them out";
            if (brain.CanDecide) return "Accept job     [Q] Decline";
            if (brain.JobReady) return "Hand it back";
            if (brain.JobFixedButAway) return "Bring their item back";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (brain.CanHearIntake) brain.HearIntake();
        else if (brain.CanAcceptJob) brain.AcceptJob();
        else if (brain.JobReady) brain.CompleteJob();
        else if (brain.CanReassure) brain.Reassure();
    }

    // Only re-show on GAINING focus. Looking away never hides a line —
    // the bubble's own timer owns that now.
    public override void SetFocused(bool focused)
    {
        if (brain != null && focused) brain.ShowBubble(true);
    }
}