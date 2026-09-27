using UnityEngine;

/// <summary>
/// The set of movement profiles people in the café can have, and how they are
/// handed out: walk-ins and patrons roll one by weight; a regular without an
/// authored profile (CustomerProfile.movementProfile) gets the same one every
/// visit, chosen from a stable hash of their save key, so nobody changes
/// personality between days. Built by Fixit Fidget > Café life > Pass 2 - 3.
/// </summary>
[CreateAssetMenu(menuName = "Fixit Fidget/NPC profile library", fileName = "NpcProfileLibrary")]
public sealed class NpcProfileLibrary : ScriptableObject
{
    public NpcMovementProfile[] profiles = new NpcMovementProfile[0];

    public int Count => profiles != null ? profiles.Length : 0;

    /// <summary>A random profile by weight, or null with an empty library.</summary>
    public NpcMovementProfile Roll()
    {
        if (profiles == null || profiles.Length == 0) return null;
        float total = 0f;
        foreach (NpcMovementProfile p in profiles) if (p != null) total += Mathf.Max(0f, p.weight);
        if (total <= 0f) return profiles[Random.Range(0, profiles.Length)];
        float pick = Random.value * total;
        foreach (NpcMovementProfile p in profiles)
        {
            if (p == null) continue;
            pick -= Mathf.Max(0f, p.weight);
            if (pick <= 0f) return p;
        }
        return profiles[profiles.Length - 1];
    }

    /// <summary>The same profile for the same key every time (a regular's persistent id).</summary>
    public NpcMovementProfile ForKey(string key)
    {
        if (profiles == null || profiles.Length == 0) return null;
        if (string.IsNullOrEmpty(key)) return Roll();
        uint hash = 2166136261;
        unchecked { foreach (char c in key) hash = (hash ^ c) * 16777619; }
        return profiles[(int)(hash % (uint)profiles.Length)];
    }

    public NpcMovementProfile Find(string displayName)
    {
        if (profiles == null) return null;
        foreach (NpcMovementProfile p in profiles)
            if (p != null && string.Equals(p.Name, displayName, System.StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }
}
