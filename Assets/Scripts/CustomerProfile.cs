using UnityEngine;

public enum RegularVisitKind
{
    FollowDayMix,
    RepairOnly,
    DrinkOnly
}

public enum PortraitExpression { Neutral, Happy, Worried, Impatient, Surprised }

[System.Serializable]
public class CustomerReturnDialogue
{
    [TextArea(2, 4)] public string[] successfulRepair;
    [TextArea(2, 4)] public string[] imperfectRepair;
    [TextArea(2, 4)] public string[] rejectedRepair;
    [TextArea(2, 4)] public string[] incompleteService;
    [TextArea(2, 4)] public string[] missedVisit;
    [TextArea(2, 4)] public string[] declinedVisit;
    [TextArea(2, 4)] public string[] capacityRefusal;
    [TextArea(2, 4)] public string[] servedVisit;

    public string[] For(CustomerReturnOutcome outcome) => outcome switch
    {
        CustomerReturnOutcome.SuccessfulRepair => successfulRepair,
        CustomerReturnOutcome.ImperfectRepair => imperfectRepair,
        CustomerReturnOutcome.RejectedRepair => rejectedRepair,
        CustomerReturnOutcome.IncompleteService => incompleteService,
        CustomerReturnOutcome.MissedVisit => missedVisit,
        CustomerReturnOutcome.DeclinedVisit => declinedVisit,
        CustomerReturnOutcome.CapacityRefusal => capacityRefusal,
        CustomerReturnOutcome.ServedVisit => servedVisit,
        _ => null
    };
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

    [Header("Portrait expressions (regulars)")]
    public Sprite portraitNeutral;
    public Sprite portraitHappy;
    public Sprite portraitAnnoyed;
    public Sprite portraitSad;
    public Sprite portraitSurprised;

    public Sprite PortraitFor(PortraitExpression expression)
    {
        Sprite chosen = expression switch
        {
            PortraitExpression.Happy => portraitHappy,
            PortraitExpression.Worried => portraitSad,
            PortraitExpression.Impatient => portraitAnnoyed,
            PortraitExpression.Surprised => portraitSurprised,
            _ => portraitNeutral
        };
        return chosen != null ? chosen : portraitNeutral;
    }

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
    [Tooltip("Used after an earlier visit when trust is low or the latest outcome does not support warm dialogue.")]
    public DialogueSet returnLines;

    [Header("Returning with trust (relationship 2+)")]
    public DialogueSet warmLines;

    [Header("Intake callback from the actual previous visit")]
    public CustomerReturnDialogue returnMemoryLines = new();

    [Header("Service outcome responses")]
    [TextArea(2, 4)] public string[] passableRepairLines;
    [TextArea(2, 4)] public string[] rejectedRepairLines;
    [TextArea(2, 4)] public string[] drinkCompletedLines;
}
