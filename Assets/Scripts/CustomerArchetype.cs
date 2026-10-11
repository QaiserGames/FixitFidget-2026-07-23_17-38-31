using UnityEngine;

[System.Serializable]
public class CustomerArchetype
{
    public string archetypeName = "Cheerful";
    public float patienceMultiplier = 1f;
    public float tipMultiplier = 1f;
    public Color moodColor = Color.white;

    [Tooltip("Where this sort of person waits once you've taken their job. Seat: they sit, and patience drains slowly; " +
             "Loiter: they stand at the counter, and standing drains it faster (WaitingSpot). Their short fuse is " +
             "patienceMultiplier, not where they wait (playtest 4).")]
    public WaitingSpot.SpotKind preferredWaitKind = WaitingSpot.SpotKind.Seat;

    [Range(0f, 1f)]
    [Tooltip("Chance this sort of person ALSO wants a drink while waiting on a " +
             "repair. Nothing to do with customers who came only for coffee — " +
             "that's CustomerSpawner.drinkChance.")]
    public float drinkWishChance = 0.5f;

    public DialogueSet lines;
}
