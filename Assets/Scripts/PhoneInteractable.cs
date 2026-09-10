using UnityEngine;

public class PhoneInteractable : Interactable
{
    private HoldCallJob job;
    private void Awake() => job = GetComponentInParent<HoldCallJob>();
    public override bool IsAvailable
    {
        get
        {
            if (job == null || !job.CanOperate) return false;
            if (job.CanActivate) return true;
            var carry = FindAnyObjectByType<PlayerCarry>();
            return job.IsComplete && carry != null && carry.HasSpace;
        }
    }
    public override string Prompt => job == null ? "" : job.IsComplete ? "Pick up the phone"
        : job.CurrentPhase == HoldCallRun.State.Ringing ? "Answer support" : job.MissedCalls > 0 ? "Redial support" : "Call support";
    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        if (job.CanActivate) { job.Activate(); return; }
        var carry = player.GetComponent<PlayerCarry>();
        if (carry == null || !carry.HasSpace || !job.IsComplete) return;
        carry.PickUp(job);
        if (player.IsAtStation) player.ExitStation();
    }
}
