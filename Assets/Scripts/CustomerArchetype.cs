using UnityEngine;

[System.Serializable]
public class CustomerArchetype
{
    public string archetypeName = "Cheerful";
    public float patienceMultiplier = 1f;
    public float tipMultiplier = 1f;
    public Color moodColor = Color.white;

    [Tooltip("Where this sort of person waits once you've taken their job. " +
             "Impatient types loiter by the counter; calm ones sit down.")]
    public WaitingSpot.SpotKind preferredWaitKind = WaitingSpot.SpotKind.Seat;

    public DialogueSet lines;
}