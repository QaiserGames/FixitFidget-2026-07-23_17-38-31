using System;
using UnityEngine;

/// <summary>
/// The Mixamo clips the café's people play as "beats" (batch 1, 27 Sept 2026):
/// which baked clip fills which state of the customer animator's Beats layer,
/// and the phone held in the two phone clips.
///
/// THE ASSET IS NOT IN THE PUBLIC REPOSITORY. Fixit Fidget > NPC > Mixamo 3 builds
/// it in Assets/Art/Mixamo/Baked/Resources/, which is git-ignored with the rest of
/// the Mixamo folder (Adobe's terms allow the clips in the game, not sharing the
/// files), and <see cref="NpcBeats"/> loads it from Resources. The animator itself
/// only holds empty placeholder clips, named <see cref="SlotPrefix"/> + the beat's
/// name; at run time an override controller swaps the baked clips in. A copy of
/// the project without this asset has no beats: people use the café's own clips,
/// exactly as before.
/// </summary>
public sealed class NpcBeatLibrary : ScriptableObject
{
    public enum Hand { None, Left, Right }

    [Serializable]
    public sealed class Entry
    {
        [Tooltip("The clip's short name, which is also its state in the animator's Beats layer (\"Thankful\").")]
        public string name;
        [Tooltip("The clip baked onto the café rig by Mixamo 1.")]
        public AnimationClip clip;
        [Tooltip("A seated clip (lined up with the café's chairs) rather than a standing one.")]
        public bool seated;
        [Tooltip("The hand that holds the phone (the two phone clips only).")]
        public Hand phoneHand;
    }

    /// <summary>Where NpcBeats finds the asset: Resources/NpcBeatLibrary.</summary>
    public const string ResourceName = "NpcBeatLibrary";
    /// <summary>The animator layer the beats play on (Mixamo 3 adds it; weight 0 unless a beat is showing).</summary>
    public const string LayerName = "Beats";
    /// <summary>The empty state the Beats layer rests in.</summary>
    public const string RestState = "No beat";
    /// <summary>The animator's placeholder clips are named this plus the beat's name.</summary>
    public const string SlotPrefix = "Beat slot - ";

    /// <summary>Where a city look's ears are: its head bone's own space (Mixamo 3 measures them on the head mesh).</summary>
    [Serializable]
    public sealed class Ears
    {
        public string look;
        public Vector3 left, right;
    }

    public Entry[] entries = Array.Empty<Entry>();
    [Tooltip("What people hold in the two phone clips: POLYGON City's smartphone (SM_Prop_SmartPhone_01).")]
    public GameObject phone;
    [Tooltip("Each city look's ears, for the phone on a call (measured by Mixamo 3 from the looks' head meshes).")]
    public Ears[] ears = Array.Empty<Ears>();

    public bool TryGetEars(string look, out Vector3 left, out Vector3 right)
    {
        left = right = Vector3.zero;
        if (ears == null || string.IsNullOrEmpty(look)) return false;
        foreach (Ears e in ears)
            if (e != null && string.Equals(e.look, look, StringComparison.Ordinal)) { left = e.left; right = e.right; return true; }
        return false;
    }

    public Entry Find(string beatName)
    {
        if (entries == null || string.IsNullOrEmpty(beatName)) return null;
        foreach (Entry e in entries)
            if (e != null && string.Equals(e.name, beatName, StringComparison.Ordinal)) return e;
        return null;
    }
}
