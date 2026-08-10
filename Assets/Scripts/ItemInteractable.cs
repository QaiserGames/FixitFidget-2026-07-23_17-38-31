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
            if (job == null || job.Owner == null) return false;

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && !carry.IsCarrying;
        }
    }

    public override string Prompt => "Pick up";

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        carry.PickUp(job);

        // Taking something always means you're about to walk somewhere —
        // so step out of the station in the same press.
        if (player.IsAtStation) player.ExitStation();
    }
}