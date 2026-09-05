using System;
using System.Collections.Generic;

// One day's names and regular arrivals. A scheduled regular owns both their
// stable ID and display name; walk-ins cannot borrow either identity.
public sealed class CustomerVisitRoster
{
    private readonly HashSet<string> regularIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> unavailableNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> names = new();
    private int fallbackNumber;

    public void Reset(IEnumerable<string> candidates, IEnumerable<string> reservedNames)
    {
        regularIds.Clear();
        unavailableNames.Clear();
        names.Clear();
        fallbackNumber = 0;
        if (reservedNames != null)
            foreach (string name in reservedNames)
                if (!string.IsNullOrWhiteSpace(name)) unavailableNames.Add(name.Trim());

        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        if (candidates != null)
            foreach (string name in candidates)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string trimmed = name.Trim();
                if (!unavailableNames.Contains(trimmed) && unique.Add(trimmed)) names.Add(trimmed);
            }
    }

    public int RemainingNames => names.Count;

    public string TakeName(int index)
    {
        if (names.Count > 0)
        {
            if (index < 0 || index >= names.Count) throw new ArgumentOutOfRangeException(nameof(index));
            string chosen = names[index];
            names.RemoveAt(index);
            unavailableNames.Add(chosen);
            return chosen;
        }

        string fallback;
        do { fallback = "Walk-in " + ++fallbackNumber; }
        while (unavailableNames.Contains(fallback));
        unavailableNames.Add(fallback);
        return fallback;
    }

    public bool CanVisit(string id) => !string.IsNullOrWhiteSpace(id) && !regularIds.Contains(id);
    public bool CanVisitRandomly(string id, string featuredId) => CanVisit(id) && !string.Equals(id, featuredId, StringComparison.Ordinal);
    public void RecordArrival(string id)
    {
        if (!string.IsNullOrWhiteSpace(id)) regularIds.Add(id);
    }
}
