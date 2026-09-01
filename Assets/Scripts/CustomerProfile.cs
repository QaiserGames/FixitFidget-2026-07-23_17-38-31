using UnityEngine;

public enum RegularVisitKind
{
    FollowDayMix,
    RepairOnly,
    DrinkOnly
}

[CreateAssetMenu(fileName = "Regular_", menuName = "FixitFiasco/Customer Profile")]
public class CustomerProfile : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Alex";
    [TextArea(2, 4)] public string bio;
    public Color themeColor = Color.white;

    [Tooltip("Stable key used in save files. Set this once (for example, grace) " +
             "and never change it after players have saves. Empty falls back to the asset name.")]
    [SerializeField] private string persistentId = "";

    public string PersistentId => string.IsNullOrWhiteSpace(persistentId)
        ? name
        : persistentId.Trim();

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

    [Header("Visit pattern")]
    [Tooltip("Whether their main reason for visiting follows the day's mix, " +
             "is always a repair, or is always a café order.")]
    public RegularVisitKind primaryVisitKind = RegularVisitKind.FollowDayMix;

    [Tooltip("Their signature drink. Used for a drink-only visit and for a " +
             "secondary order while waiting. Empty = choose from today's menu.")]
    public DrinkDefinition preferredDrink;

    [Header("What they usually bring")]
    [Tooltip("Their signature device. Left empty = fully random.")]
    public GameObject preferredDevice;

    [Range(0f, 1f)]
    [Tooltip("Chance they bring their signature device. The rest of the time it's a surprise.")]
    public float preferredDeviceChance = 0.7f;

    [Header("First visit dialogue")]
    public DialogueSet lines;

    [Header("Returning after a rough or neutral visit")]
    [Tooltip("Used when this person has visited before but relationship is below 2.")]
    public DialogueSet returnLines;

    [Header("Returning with trust (relationship 2+)")]
    public DialogueSet warmLines;
}