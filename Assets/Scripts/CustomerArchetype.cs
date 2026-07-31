using UnityEngine;

[System.Serializable]
public class CustomerArchetype
{
    public string archetypeName = "Cheerful";

    [TextArea(2, 4)]
    public string[] lines;

    [Tooltip("Multiplies both patience meters. <1 = impatient, >1 = laid back.")]
    public float patienceMultiplier = 1f;

    [Tooltip("Multiplies the speed tip. >1 = generous.")]
    public float tipMultiplier = 1f;

    public Color moodColor = Color.white;
}