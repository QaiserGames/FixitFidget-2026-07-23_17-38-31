using UnityEngine;

public class CustomerInteractable : Interactable
{
    private CustomerBrain brain;

    private void Awake()
    {
        brain = GetComponent<CustomerBrain>();
    }

    public override bool IsAvailable => brain != null && (brain.CanAcceptJob || brain.JobReady);

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanAcceptJob) return "Accept job";
            if (brain.JobReady) return "Hand it back";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (brain.CanAcceptJob) brain.AcceptJob();
        else if (brain.JobReady) brain.CompleteJob();
    }

    // Looking at someone re-shows what they said.
    public override void SetFocused(bool focused)
    {
        if (brain != null) brain.ShowBubble(focused);
    }
}