using UnityEngine;

[System.Serializable]
public class CustomerArchetype
{
    public string archetypeName = "Cheerful";

    [TextArea(2, 4)]
    [Tooltip("Idle personality lines. Rarely used now that jobs carry their own dialogue.")]
    public string[] lines;

    [TextArea(2, 3)]
    [Tooltip("Replies to being reassured. Should sound like an answer, not a greeting.")]
    public string[] reassuredLines;

    [Tooltip("Multiplies both patience meters. <1 = impatient, >1 = laid back.")]
    public float patienceMultiplier = 1f;

    [Tooltip("Multiplies the speed tip. >1 = generous.")]
    public float tipMultiplier = 1f;

    public Color moodColor = Color.white;
}