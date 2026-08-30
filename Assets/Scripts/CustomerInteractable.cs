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
                          brain.CanApologiseForDrink ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    // Which of those actions can be done from the shop floor, rather than only
    // from behind the counter. Intake is a proper conversation and stays
    // counter-only; handing things over and reassuring people happen wherever
    // they're standing.
    public bool FloorAvailable =>
        brain != null && (brain.CanReceiveDrink || brain.JobReady ||
                          brain.CanApologiseForDrink ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    public override string Prompt
    {
        get
        {
            if (brain == null) return "";
            // Record.Subject is what they CAME for. For a drink-only walk-in
            // that happens to be the drink; for a repair customer who ordered a
            // coffee while waiting it's their pocket watch — so this read
            // "Serve the Pocket Watch" while you stood there holding a latte.
            // WantedDrinkName covers both.
            if (brain.CanReceiveDrink) return $"Serve the {brain.WantedDrinkName}";

            // Ranked above reassurance on purpose: if their drink can't be
            // made, calming them down only postpones the same dead end.
            if (brain.CanApologiseForDrink)
                return $"Sorry, we're out of {brain.WantedDrinkName}";
            if (brain.CanHearIntake || brain.CanDecide) return "Talk to them";
            // The grade is shown BEFORE you commit. This is the whole point:
            // without it, handing back a half-done repair is a nasty surprise
            // rather than a choice you made under pressure.
            if (brain.JobReady)
            {
                JobGrade g = brain.PendingGrade;
                return g == JobGrade.Perfect
                    ? "Hand it back"
                    : $"Hand it back ({g})";
            }
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

        if (brain.CanApologiseForDrink) { brain.ApologiseForDrink(); return; }

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