using UnityEngine;

public class ItemInteractable : Interactable
{
    private JobBase job;

    private void Awake()
    {
        job = GetComponentInParent<JobBase>();
        if (job == null) job = GetComponent<JobBase>();
    }

    // Only when someone owns this job and our hands are free.
    public override bool IsAvailable
    {
        get
        {
            if (job == null || job.Owner == null) return false;

            PlayerInteractor p = FindAnyObjectByType<PlayerInteractor>();
            if (p == null || p.IsAtStation) return false;   // floor only

            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && !carry.IsCarrying;
        }
    }

    public override string Prompt => "Pick up";

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry != null) carry.PickUp(job);
    }
}