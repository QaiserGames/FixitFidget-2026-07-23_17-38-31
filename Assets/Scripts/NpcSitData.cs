using UnityEngine;

/// <summary>
/// Measurements of the café NPCs' baked sit clips, written by
/// Fixit Fidget > NPC > Sit 1 - Bake sit animations. NpcSeating uses them to
/// put a body exactly where the clip expects the chair to be.
/// All positions are in the NPC model's own space (feet at the origin, facing
/// +Z) and in unscaled model units; NpcSeating applies the NPC's scale.
/// </summary>
[CreateAssetMenu(menuName = "Fixit Fidget/NPC sit data", fileName = "NpcSitData")]
public sealed class NpcSitData : ScriptableObject
{
    [Tooltip("Length of the sit-down clip, seconds.")]
    public float enterLength = 1.3f;
    [Tooltip("Length of the stand-up clip, seconds.")]
    public float exitLength = 1.03f;
    [Tooltip("Midpoint of the two hip joints while seated.")]
    public Vector3 seatedHip = new Vector3(0f, .5f, -.26f);
    [Tooltip("Midpoint of the two ankles while seated (the feet stay where the NPC stood).")]
    public Vector3 seatedFeet = new Vector3(0f, .1f, .12f);
    [Tooltip("Height of the hip joints above the model origin when standing at the start of the sit-down.")]
    public float standingHipHeight = .85f;
    [Tooltip("Uniform scale that was applied to the source motion to fit this rig (leg length ratio).")]
    public float retargetScale = 1f;
}
