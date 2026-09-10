using UnityEngine;

public class ItemInteractable : Interactable
{
    private JobBase job;

    private void Awake()
    {
        job = GetComponentInParent<JobBase>();
        if (job == null) job = GetComponent<JobBase>();
    }

    public override bool IsAvailable
    {
        get
        {
            if (job == null) return false;
            if (job.Owner != null && job.Owner.IsCounterRepair) return false;

            // A cup locked in the machine isn't yours to take yet.
            DrinkJob drink = job as DrinkJob;
            if (drink != null && drink.Locked) return false;

            if (drink == null && job.Owner == null) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && carry.HasSpace;
        }
    }

    public override string Prompt => job is DrinkJob cup && !cup.IsEmpty
        ? $"Take {cup.Drink.drinkName} ({cup.FreshnessStage.ToString().ToLowerInvariant()})" : "Pick up";

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        if (!carry.TryPickUp(job)) return;

        // Taking something always means you're about to walk somewhere —
        // so step out of the station in the same press.
        if (player.IsAtStation && player.CurrentStation.GetComponent<BeverageStation>() == null) player.ExitStation();
    }
}
