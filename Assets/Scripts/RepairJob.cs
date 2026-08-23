using UnityEngine;

public class RepairJob : JobBase
{
    public override JobFamily Family => JobFamily.Mechanical;

    private int totalTasks;

    private void Start()
    {
        // Snapshot how much was wrong with it when it arrived. Grime spots are
        // destroyed as they're cleaned and parts flip to replaced, so this has
        // to be captured before the player touches anything.
        totalTasks = GetComponentsInChildren<GrimeSpot>().Length
                   + GetComponentsInChildren<ReplaceablePart>().Length;

        if (totalTasks == 0)
            Debug.Log($"{name}: no teardown work — treated as a Human-family " +
                      $"fault (the fix is a conversation). If that's not what " +
                      $"you meant, check the device's fault Enable Objects.", this);
    }

    private int RemainingTasks()
    {
        int remaining = GetComponentsInChildren<GrimeSpot>().Length;

        foreach (ReplaceablePart part in GetComponentsInChildren<ReplaceablePart>())
            if (!part.IsReplaced) remaining++;

        return remaining;
    }

    // Nothing to physically do — "not broken, just muted". The GDD's Human
    // family. Full marks: you identified it and handed it straight back, which
    // IS the fix. Never a data error that punishes the player.
    public override float Quality =>
        totalTasks <= 0
            ? 1f
            : Mathf.Clamp01((totalTasks - RemainingTasks()) / (float)totalTasks);

    // Kept for anything still asking the old question — the ticket rail, the
    // "is this finished" badge. It now means PERFECT specifically.
    public override bool IsComplete => CanHandBack && Quality >= 0.999f;
}