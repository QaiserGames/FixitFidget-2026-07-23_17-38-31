using System;
using UnityEngine;

/// <summary>
/// The café NPCs' walk and idle clips by style, with each walk's natural
/// ground speed (metres per second at playback 1, rig at scale 1) so
/// NpcLocomotion can keep the feet planted whichever walk a person has:
/// WalkRate = speed / (clipSpeed x the NPC's scale). Written by
/// Fixit Fidget > Café life > Pass 2 - 1 (bake), which also wires the clips
/// into the animator's Walk and Idle blend trees (WalkStyle / IdleStyle).
/// </summary>
[CreateAssetMenu(menuName = "Fixit Fidget/NPC gait data", fileName = "NpcGaitData")]
public sealed class NpcGaitData : ScriptableObject
{
    [Serializable]
    public sealed class Walk
    {
        public string name;
        public AnimationClip clip;
        [Tooltip("Stance-foot speed of the clip, m/s at playback 1 on the unscaled rig.")]
        public float clipSpeed = 1.35f;
    }

    [Tooltip("Indexed by NpcMovementProfile.WalkStyle: 0 normal, 1 relaxed, 2 brisk.")]
    public Walk[] walks = new Walk[0];
    [Tooltip("Indexed by NpcMovementProfile.IdleStyle: 0 normal, 1 relaxed.")]
    public AnimationClip[] idles = new AnimationClip[0];
    [Tooltip("Standing talk loop, played while a standing customer talks (Standing Talk state).")]
    public AnimationClip standingTalk;

    public bool HasWalk(int style) => walks != null && style >= 0 && style < walks.Length && walks[style] != null && walks[style].clip != null;

    /// <summary>The natural speed of a walk style, or <paramref name="fallback"/> when it is not baked.</summary>
    public float ClipSpeed(int style, float fallback) =>
        HasWalk(style) && walks[style].clipSpeed > .1f ? walks[style].clipSpeed : fallback;
}
