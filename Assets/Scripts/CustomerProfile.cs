using UnityEngine;

[CreateAssetMenu(fileName = "Regular_", menuName = "FixitFiasco/Customer Profile")]
public class CustomerProfile : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Alex";
    [TextArea(2, 4)] public string bio;
    public Color themeColor = Color.white;

    [Header("Portraits (regulars only — wired later)")]
    public Sprite portraitNeutral;
    public Sprite portraitHappy;
    public Sprite portraitAnnoyed;
    public Sprite portraitSad;

    [Header("Behaviour")]
    public float patienceMultiplier = 1f;
    public float tipMultiplier = 1f;

    [Tooltip("Where they wait once you've taken their job.")]
    public WaitingSpot.SpotKind preferredWaitKind = WaitingSpot.SpotKind.Seat;

    [Range(0f, 1f)]
    [Tooltip("Chance they ALSO want a drink while waiting on a repair. " +
             "A regular who always orders the same coffee is a cheap piece of " +
             "characterisation — set this to 1 and give them a signature drink.")]
    public float drinkWishChance = 0.5f;

    [Header("What they usually bring")]
    [Tooltip("Their signature device. Left empty = fully random.")]
    public GameObject preferredDevice;

    [Range(0f, 1f)]
    [Tooltip("Chance they bring their signature device. The rest of the time it's a surprise.")]
    public float preferredDeviceChance = 0.7f;

    [Header("Dialogue")]
    public DialogueSet lines;

    [Header("Once we know each other (relationship 2+, wired with memory later)")]
    public DialogueSet warmLines;
}