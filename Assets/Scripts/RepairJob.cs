using UnityEngine;

public class RepairJob : JobBase
{
    public override JobFamily Family => Record != null ? Record.faultType switch
    {
        FaultType.Cleaning => JobFamily.Cleaning,
        FaultType.Software => JobFamily.Software,
        FaultType.Human => JobFamily.Human,
        FaultType.Bureaucratic => JobFamily.Bureaucratic,
        _ => JobFamily.Mechanical
    } : JobFamily.Mechanical;

    private int totalTasks;
    private bool captured;
    private GrimeSpot[] grime;
    private ReplaceablePart[] parts;
    private CircuitPuzzle[] circuits;

    private void Start() => EnsureSnapshot();

    // ApplyFault calls this after all enable/disable changes. Quality is also safe
    // before Start, so another component cannot see a free Perfect on spawn.
    public void CaptureTasks()
    {
        grime = GetComponentsInChildren<GrimeSpot>();
        parts = GetComponentsInChildren<ReplaceablePart>();
        circuits = GetComponentsInChildren<CircuitPuzzle>();
        totalTasks = grime.Length + parts.Length;
        foreach (CircuitPuzzle puzzle in circuits) totalTasks += puzzle.TotalTasks;
        captured = true;
    }

    private void EnsureSnapshot() { if (!captured) CaptureTasks(); }

    private int RemainingTasks()
    {
        int remaining = 0;
        // A hidden/disabled part is not a completed part. Only the selected fault
        // was captured; visual visibility changes cannot alter its denominator.
        foreach (GrimeSpot spot in grime)
            if (spot != null) remaining++;

        foreach (ReplaceablePart part in parts)
            if (part == null || !part.IsReplaced) remaining++;

        foreach (CircuitPuzzle puzzle in circuits)
            if (puzzle != null) remaining += puzzle.RemainingTasks;
            else return totalTasks; // Missing puzzle is a fault, never free completion.

        return remaining;
    }

    // Nothing to physically do — "not broken, just muted". The GDD's Human
    // family. Full marks: you identified it and handed it straight back, which
    // IS the fix. Never a data error that punishes the player.
    public override float Quality
    {
        get
        {
            EnsureSnapshot();
            return totalTasks <= 0 ? 1f : Mathf.Clamp01((totalTasks - RemainingTasks()) / (float)totalTasks);
        }
    }

    // Kept for anything still asking the old question — the ticket rail, the
    // "is this finished" badge. It now means PERFECT specifically.
    public override bool IsComplete => CanHandBack && Quality >= 0.999f;
}
