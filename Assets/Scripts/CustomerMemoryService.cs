using System;
using System.Collections.Generic;

// Owns regular-customer history. Reads return copies; only RecordVisit changes
// history. SaveManager remains responsible for when and where it is saved.
public sealed class CustomerMemoryService
{
    private readonly Dictionary<string, RegularMemoryData> records = new(StringComparer.Ordinal);

    public void Restore(IEnumerable<RegularMemoryData> saved)
    {
        records.Clear();
        if (saved == null) return;
        foreach (RegularMemoryData memory in saved)
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.profileId)) continue;
            records[memory.profileId] = memory.Copy();
        }
    }

    public RegularMemoryData Read(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return null;
        return records.TryGetValue(profileId, out RegularMemoryData memory) ? memory.Copy() : null;
    }

    public void RecordVisit(string profileId, int day, bool happy, bool accepted,
                            bool served, LostReason reason, string grade)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return;
        if (!records.TryGetValue(profileId, out RegularMemoryData memory))
        {
            memory = new RegularMemoryData { profileId = profileId };
            records.Add(profileId, memory);
        }

        memory.visits++;
        memory.relationship = Math.Max(-4, Math.Min(6,
            memory.relationship + RelationshipDelta(happy, accepted, served, reason)));
        memory.lastSeenDay = day;
        memory.lastVisitHappy = happy;
        memory.lastJobAccepted = accepted;
        memory.lastVisitServed = served;
        // An apologised-for drink can end an otherwise served visit. Preserve
        // that fact even though the existing economy treats the visit as happy.
        memory.lastLossReason = happy && reason != LostReason.OutOfStock ? "" : reason.ToString();
        memory.lastGrade = grade ?? "";
    }

    public RegularMemoryData[] Snapshot()
    {
        List<RegularMemoryData> result = new();
        foreach (RegularMemoryData memory in records.Values) result.Add(memory.Copy());
        result.Sort((a, b) => string.CompareOrdinal(a.profileId, b.profileId));
        return result.ToArray();
    }

    private static int RelationshipDelta(bool happy, bool accepted, bool served, LostReason reason)
    {
        // Existing relationship tuning is preserved in this extraction.
        if (happy && served) return 2;
        if (reason == LostReason.OutOfStock || reason == LostReason.ShelfFull) return 0;
        if (accepted || reason == LostReason.StormedOutInQueue || reason == LostReason.StormedOutWaiting) return -2;
        return -1;
    }
}

public enum CustomerReturnOutcome
{
    FirstVisit, SuccessfulRepair, ImperfectRepair, RejectedRepair,
    IncompleteService, MissedVisit, DeclinedVisit, CapacityRefusal, ServedVisit, UnknownReturn
}

public static class CustomerReturnPolicy
{
    public static CustomerReturnOutcome Classify(RegularMemoryData memory)
    {
        if (memory == null || memory.visits <= 0) return CustomerReturnOutcome.FirstVisit;

        bool capacity = memory.lastLossReason == nameof(LostReason.OutOfStock)
                     || memory.lastLossReason == nameof(LostReason.ShelfFull);
        if (capacity)
            return memory.lastVisitServed ? CustomerReturnOutcome.IncompleteService : CustomerReturnOutcome.CapacityRefusal;

        if (!memory.lastVisitHappy)
        {
            if (memory.lastVisitServed) return CustomerReturnOutcome.IncompleteService;
            if (memory.lastLossReason == nameof(LostReason.Declined)) return CustomerReturnOutcome.DeclinedVisit;
            return CustomerReturnOutcome.MissedVisit;
        }

        if (!memory.lastVisitServed) return CustomerReturnOutcome.UnknownReturn;
        return memory.lastGrade switch
        {
            nameof(JobGrade.Perfect) or nameof(JobGrade.Good) => CustomerReturnOutcome.SuccessfulRepair,
            nameof(JobGrade.Passable) => CustomerReturnOutcome.ImperfectRepair,
            nameof(JobGrade.Rejected) => CustomerReturnOutcome.RejectedRepair,
            _ => CustomerReturnOutcome.ServedVisit
        };
    }

    public static bool AllowsWarmDialogue(CustomerReturnOutcome outcome) =>
        outcome == CustomerReturnOutcome.SuccessfulRepair
        || outcome == CustomerReturnOutcome.ImperfectRepair
        || outcome == CustomerReturnOutcome.ServedVisit;
}
