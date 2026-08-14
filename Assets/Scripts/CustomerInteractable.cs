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
                          brain.JobFixedButAway || brain.CanReceiveDrink ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanReceiveDrink) return $"Serve the {brain.Record.Subject}";
            if (brain.CanHearIntake || brain.CanDecide) return "Talk to them";
            if (brain.JobReady) return "Hand it back";
            if (brain.JobFixedButAway) return "Bring their item back";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        // Serving a drink is a quick handover, not a conversation.
        if (brain.CanReceiveDrink)
        {
            PlayerCarry carry = player.GetComponent<PlayerCarry>();
            brain.ServeDrink(carry);
            return;
        }

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