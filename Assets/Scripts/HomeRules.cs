using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Homes for regulars (claude/night-homes-spec.md): the rules, without Unity.
//
//  * A regular with a home always walks out of their own front door to the
//    café, and back in afterwards: the walking route whose homeId is theirs.
//  * Everyone else (every walk-in, every patron, a regular with no home yet)
//    picks among the public routes only. A home's door is never a stranger's.
//  * Ace notices where someone lives only by seeing them at their door: the
//    first time is a hunch, seeing it again on a later day makes it likely.
//    Sightings never make Ace sure: that is kept for something stronger (a
//    name on a mailbox at night, an address on paper).
//
// CafeArrivals and NotebookHooks use these. No Unity types: Tests/HomeRules
// compiles this file.
// ---------------------------------------------------------------------------
public static class HomeRules
{
    /// <summary>Ace notices someone at their door while they are this close to it (metres).</summary>
    public const float WatchRadius = 3f;

    /// <summary>
    /// The route that is this home's (its homeId matches and it has a path), or -1.
    /// </summary>
    public static int HomeRoute(string homeId, IReadOnlyList<string> routeHomes, IReadOnlyList<int> routePoints)
    {
        if (string.IsNullOrWhiteSpace(homeId) || routeHomes == null) return -1;
        string id = homeId.Trim();
        for (int i = 0; i < routeHomes.Count; i++)
            if (Points(routePoints, i) > 1 && string.Equals((routeHomes[i] ?? "").Trim(), id, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>
    /// A public route picked by weight, with <paramref name="roll"/> in [0, 1).
    /// Routes that belong to a home are never picked. -1 when there are none.
    /// </summary>
    public static int PublicRoute(IReadOnlyList<string> routeHomes, IReadOnlyList<float> weights, IReadOnlyList<int> routePoints, double roll)
    {
        if (routeHomes == null) return -1;
        double total = 0;
        int last = -1;
        for (int i = 0; i < routeHomes.Count; i++)
        {
            if (!IsPublic(routeHomes, routePoints, i)) continue;
            total += Weight(weights, i);
            last = i;
        }
        if (last < 0 || total <= 0) return -1;
        double left = Math.Max(0, Math.Min(roll, 0.999999)) * total;
        for (int i = 0; i < routeHomes.Count; i++)
        {
            if (!IsPublic(routeHomes, routePoints, i)) continue;
            left -= Weight(weights, i);
            if (left < 0) return i;
        }
        return last;
    }

    /// <summary>
    /// How sure one more sighting makes Ace, given what the notebook already says
    /// (null: never noticed before). Hunch the first time; likely once it has
    /// been seen on an earlier day; never sure.
    /// </summary>
    public static string SightingSureness(NotebookFactData known, int today)
    {
        if (known == null) return Notebook.Sureness.Hunch;
        return known.day < today ? Notebook.Sureness.Likely : Notebook.Sureness.Hunch;
    }

    private static bool IsPublic(IReadOnlyList<string> homes, IReadOnlyList<int> points, int i) =>
        Points(points, i) > 1 && string.IsNullOrWhiteSpace(homes[i]);

    private static int Points(IReadOnlyList<int> points, int i) => points != null && i < points.Count ? points[i] : 0;

    private static double Weight(IReadOnlyList<float> weights, int i) =>
        weights != null && i < weights.Count ? Math.Max(0f, weights[i]) : 1f;
}
