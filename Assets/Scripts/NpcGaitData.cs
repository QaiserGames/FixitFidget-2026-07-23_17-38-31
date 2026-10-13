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

    [Tooltip("Indexed by NpcMovementProfile.WalkStyle: 0 normal, 1 relaxed, 2 brisk. Batch 2 (NpcLocomotion.Gait): " +
             "3 hurry, 4 jog, 5 run, 6 angry; until those are baked the locomotion falls back to brisk at a higher pace.")]
    public Walk[] walks = new Walk[0];

    /// <summary>The walk styles by index, for traces and the bake's names.</summary>
    public static readonly string[] StyleNames = { "normal", "relaxed", "brisk", "hurry", "jog", "run", "angry" };

    public static string StyleName(int style) => style >= 0 && style < StyleNames.Length ? StyleNames[style] : style.ToString();
    [Tooltip("Indexed by NpcMovementProfile.IdleStyle: 0 normal, 1 relaxed.")]
    public AnimationClip[] idles = new AnimationClip[0];
    [Tooltip("Standing talk loop, played while a standing customer talks (Standing Talk state).")]
    public AnimationClip standingTalk;

    public bool HasWalk(int style) => walks != null && style >= 0 && style < walks.Length && walks[style] != null && walks[style].clip != null;

    /// <summary>The natural speed of a walk style, or <paramref name="fallback"/> when it is not baked.</summary>
    public float ClipSpeed(int style, float fallback) =>
        HasWalk(style) && walks[style].clipSpeed > .1f ? walks[style].clipSpeed : fallback;
}
