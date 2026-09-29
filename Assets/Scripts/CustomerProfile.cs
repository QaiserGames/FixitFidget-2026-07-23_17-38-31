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

// Which of a regular's visits a topic can be asked on.
public enum TopicVisits { Any, FirstMeeting, Returning }

// Something Ace can ask a regular about at the counter: a line in the conversation's reply list, and
// their answer (the dialogue pass, claude/dialogue-skyrim-proposal.md §6.4). Asking costs time, since
// the queue keeps draining, and never changes what happens: only what Ace knows.
[System.Serializable]
public class ConversationTopic
{
    [Tooltip("A stable name for it (never shown). Keep it the same once saves exist.")]
    public string id = "";
    [Tooltip("What Ace asks, as it reads in the reply list. Under 30 characters.")]
    public string ask = "";
    [Tooltip("Their answer: one line per beat (Enter between them), each short (aim for 90 characters). " +
             "{device} and {fault} are filled in.")]
    [TextArea(2, 4)] public string answer = "";
    [Tooltip("Which visits it can be asked on.")]
    public TopicVisits visits = TopicVisits.Any;
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

    [Header("Home (night track)")]
    [Tooltip("Their front door: the Home Id of a HomeDoor in the café scene (e.g. home.grace). They always walk " +
             "out of it to the café and back into it afterwards, and Ace can notice where they live. " +
             "Empty = no home yet: they arrive like anyone else.")]
    [SerializeField] private string homeId = "";

    public string HomeId => string.IsNullOrWhiteSpace(homeId) ? "" : homeId.Trim();

    [Header("Stand-in look (until their own model)")]
    [Tooltip("The city look they wear until their own model exists: the name of one of the café's walk-in looks " +
             "(a prefab in Assets/Art/CityNeighbors/Prefabs, e.g. Character_BusinessWoman). Nobody else wears it: " +
             "walk-ins, patrons and street neighbours skip it, so this regular stays recognisable. Empty, or " +
             "without the purchased art (a fresh clone of the public repository): the actor's own body.")]
    [SerializeField] private string standInLook = "";

    public string StandInLook => string.IsNullOrWhiteSpace(standInLook) ? "" : standInLook.Trim();

    // Every loaded profile's stand-in look, so that nobody else wears it (PolygonNpcVisual).
    private static readonly System.Collections.Generic.Dictionary<CustomerProfile, string> standIns = new();

    /// <summary>True when a loaded regular's profile names <paramref name="lookName"/> as their stand-in look.</summary>
    public static bool IsStandInLook(string lookName)
    {
        if (string.IsNullOrEmpty(lookName)) return false;
        foreach (string look in standIns.Values)
            if (look == lookName) return true;
        return false;
    }

    private void OnEnable() => RegisterStandIn();
    private void OnValidate() => RegisterStandIn();
    private void OnDisable() => standIns.Remove(this);

    private void RegisterStandIn()
    {
        if (StandInLook.Length == 0) standIns.Remove(this);
        else standIns[this] = StandInLook;
    }

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

    [Tooltip("How this regular moves and carries themselves (pass 2). Empty = a stable pick from the profile " +
             "library, the same one every visit. Presentation only: patience and orders are unaffected.")]
    public NpcMovementProfile movementProfile;

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

    [Header("Storyteller (opt-in; pending bench repairs only)")]
    public bool storyteller;
    [TextArea(2, 4)] public string[] storyLines;
    [Min(1f)] public float storyFirstDelay = 12f;
    [Min(1f)] public float storyInterval = 22f;
    [Range(0, 5)] public int storyMaxLines = 3;
    [TextArea(2, 4)] public string focusReply = "Of course. I'll let you concentrate.";
    [TextArea(2, 4)] public string focusReturnLine = "I remember you need quiet while you work. I'll let you concentrate.";

    [Header("Things Ace can ask about (the reply list; up to two show)")]
    public ConversationTopic[] topics;

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
