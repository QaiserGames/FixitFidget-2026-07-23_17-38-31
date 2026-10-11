using UnityEngine;

public class CustomerInteractable : Interactable
{
    private CustomerBrain brain;

    private void Awake()
    {
        brain = GetComponent<CustomerBrain>();
    }

    public override bool IsAvailable =>
        brain != null && (brain.CanHearIntake || brain.CanDecide || brain.CanFixAtCounter || brain.JobReady ||
                          brain.JobFixedButAway || brain.CanReceiveDrink ||
                          brain.CanApologiseForDrink || brain.CanRequestFocus || brain.HasColdDrinkForOrder ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    // What only happens across the counter: hearing them, their decision, and the counter phone. Since playtest 3
    // (stations as reach) nothing is stepped up to: from behind the counter (PlayerInteractor.BehindCounter) E talks,
    // to whoever the crosshair is on in first person and to whoever has waited longest from above.
    public bool CounterBusiness => brain != null && (brain.CanHearIntake || brain.CanDecide || brain.CanFixAtCounter);

    // Which of those actions can be done from the shop floor, rather than only
    // from behind the counter. Intake is a proper conversation and stays
    // counter-only; handing things over and reassuring people happen wherever
    // they're standing.
    public bool FloorAvailable =>
        brain != null && (brain.CanReceiveDrink || brain.JobReady ||
                          brain.CanApologiseForDrink || brain.CanRequestFocus || brain.HasColdDrinkForOrder ||
                         (brain.CanReassure && !brain.JobNeedsAttention));

    /// <summary>Whose E this is.</summary>
    public CustomerBrain Brain => brain;

    // ACROSS THE COUNTER (playtest 4, 10 Oct 2026; PlayerInteractor): the repair bar hands things over the counter.

    /// <summary>
    /// Waiting for what Ace is holding: their drink, or their device finished (in one piece, nothing left to fix). From
    /// behind the counter or the repair bar's bench, E hands it over whatever else is nearer.
    /// </summary>
    public bool HandOverReady => brain != null && (brain.CanReceiveDrink
        || brain.JobReady && brain.ActiveJob != null && brain.ActiveJob.IsComplete);

    /// <summary>
    /// Anything else E would do for them from the floor, over the counter, when nothing nearer is in reach: everything
    /// but handing back a device that isn't finished (E on them would hand it back unfixed; at the bench it's set down,
    /// and a deliberate early hand-back is still a walk round, or the crosshair in first person) and a cold cup (E does
    /// nothing with that).
    /// </summary>
    public bool HelpAcrossTheCounter => brain != null && FloorAvailable && !brain.HasColdDrinkForOrder
        && (brain.CanReceiveDrink || !brain.JobReady || brain.ActiveJob != null && brain.ActiveJob.IsComplete);

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
            if (brain.HasColdDrinkForOrder) return "This one's gone cold — discard it";

            // Ranked above reassurance on purpose: if their drink can't be
            // made, calming them down only postpones the same dead end.
            if (brain.CanApologiseForDrink)
                return $"Sorry, we're out of {brain.WantedDrinkName}";
            if (brain.CanHearIntake || brain.CanDecide) return $"Talk to {brain.CustomerName}";
            if (brain.CanFixAtCounter) return brain.HumanConversation.Finished ? "Return the phone" : "Check the mute switch";
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
            if (brain.CanRequestFocus) return "Let me focus";
            if (brain.CanReassure && !brain.JobNeedsAttention) return "Reassure";
            return "";
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (brain.HasColdDrinkForOrder) return;
        // Quick handovers — no camera move, no panel.
        if (brain.CanReceiveDrink)
        {
            PlayerCarry carry = player.GetComponent<PlayerCarry>();
            brain.ServeDrink(carry);
            return;
        }

        if (brain.CanApologiseForDrink) { brain.ApologiseForDrink(); return; }

        if (brain.CanFixAtCounter)
        {
            player.GetComponent<CounterRepairView>()?.Open(brain);
            return;
        }
        if (brain.JobReady) { brain.CompleteJob(); return; }
        if (brain.CanRequestFocus) { brain.RequestFocus(); return; }

        if (!brain.CanHearIntake && !brain.CanDecide && brain.CanReassure)
        {
            brain.Reassure();
            return;
        }

        // Remaining conversations are the intake beat. Follow-up diagnosis
        // was handled above; a delivery we're not carrying does nothing.
        if (!brain.CanHearIntake && !brain.CanDecide) return;

        ConversationController conv = player.GetComponent<ConversationController>();
        if (conv != null) conv.Begin(brain);
    }

    public override void SetFocused(bool focused)
    {
        if (brain != null && focused) brain.ShowBubble(true);
    }
}
