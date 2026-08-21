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

    // Which of those actions can be done from the shop floor, rather than only
    // from behind the counter. Intake is a proper conversation and stays
    // counter-only; handing things over and reassuring people happen wherever
    // they're standing.
    public bool FloorAvailable =>
        brain != null && (brain.CanReceiveDrink || brain.JobReady ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            if (brain.CanReceiveDrink) return $"Serve the {brain.Record.Subject}";
            if (brain.CanHearIntake || brain.CanDecide) return "Talk to them";
            if (brain.JobReady) return "Hand it back";
            if (brain.JobFixedButAway) return $"Their {brain.Record.Subject} is ready";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        // Quick handovers — no camera move, no panel.
        if (brain.CanReceiveDrink)
        {
            PlayerCarry carry = player.GetComponent<PlayerCarry>();
            brain.ServeDrink(carry);
            return;
        }

        if (brain.JobReady) { brain.CompleteJob(); return; }

        if (!brain.CanHearIntake && !brain.CanDecide && brain.CanReassure)
        {
            brain.Reassure();
            return;
        }

        // The conversation is only for the intake beat — meeting someone
        // and deciding whether to help them. Anything else that got focus
        // (a delivery you're not carrying, say) does nothing.
        if (!brain.CanHearIntake && !brain.CanDecide) return;

        ConversationController conv = player.GetComponent<ConversationController>();
        if (conv != null) conv.Begin(brain);
    }

    public override void SetFocused(bool focused)
    {
        if (brain != null && focused) brain.ShowBubble(true);
    }
}
