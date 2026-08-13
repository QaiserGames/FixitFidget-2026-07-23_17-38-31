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
            if (brain.CanHearIntake) return "Talk to them";
            if (brain.CanDecide) return "Talk to them";
            if (brain.JobReady) return "Hand it back";
            if (brain.JobFixedButAway) return "Bring their item back";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        // Reassurance stays a quick gesture — no panel, no camera move.
        if (!brain.CanHearIntake && !brain.CanDecide && !brain.JobReady && brain.CanReassure)
        {
            brain.Reassure();
            return;
        }

        ConversationController conv = player.GetComponent<ConversationController>();
        if (conv != null) conv.Begin(brain);
    }

    public override void SetFocused(bool focused)
    {
        if (brain != null && focused) brain.ShowBubble(true);
    }
}