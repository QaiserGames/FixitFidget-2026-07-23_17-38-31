using UnityEngine;

[System.Serializable]
public class CustomerArchetype
{
    public string archetypeName = "Cheerful";
    public float patienceMultiplier = 1f;
    public float tipMultiplier = 1f;
    public Color moodColor = Color.white;

    public DialogueSet lines;
}