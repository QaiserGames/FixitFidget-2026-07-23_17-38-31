using UnityEngine;

public class CustomerIdentity : MonoBehaviour
{
    // Only ever APPEND to this — it's serialized by index in DialogueSet lookups
    // and headed for save data with the memory pass.
    public enum Beat { Intake, Accepted, Completed, Declined, Reassured, StormedOut, OrderedDrink }

    public string DisplayName { get; private set; } = "Customer";
    public Color ThemeColor { get; private set; } = Color.white;
    public bool IsRegular => profile != null;
    public CustomerProfile Profile => profile;

    // Exposed for the day log, so a run can be read back as "the impatient
    // ones are the ones storming out" rather than just "three people left".
    public CustomerArchetype Archetype => archetype;
    public int Relationship { get; private set; }
    public bool HasMetBefore { get; private set; }
    // Regulars have faces. Walk-ins fall back to a silhouette in the UI.
    public Sprite Portrait => profile != null ? profile.portraitNeutral : null;

    private CustomerProfile profile;
    private CustomerArchetype archetype;
    private string deviceName = "thing";

    // Lowercased on the way in, because it arrives as a ticket label
    // ("Cracked Screen") and comes out mid-sentence ("my phone, cracked
    // screen"). Capitals mid-line read as a UI string that leaked into speech.
    private string faultName = "something";

    public void SetupRegular(
        CustomerProfile p,
        int relationship = 0,
        bool hasMetBefore = false)
    {
        profile = p;
        archetype = null;
        DisplayName = p.characterName;
        ThemeColor = p.themeColor;
        Relationship = relationship;
        HasMetBefore = hasMetBefore;
    }

    public void SetupWalkIn(CustomerArchetype a, string name)
    {
        archetype = a;
        profile = null;
        DisplayName = name;
        ThemeColor = a != null ? a.moodColor : Color.white;
        Relationship = 0;
        HasMetBefore = false;
    }

    public void SetDevice(string device)
    {
        if (!string.IsNullOrEmpty(device)) deviceName = device;
    }

    /// <summary>What's actually wrong with it, for the {fault} token.</summary>
    public void SetFault(string fault)
    {
        if (!string.IsNullOrEmpty(fault)) faultName = fault.ToLowerInvariant();
    }

    public float PatienceMultiplier
    {
        get
        {
            float identityMultiplier =
                profile != null ? profile.patienceMultiplier :
                archetype != null ? archetype.patienceMultiplier : 1f;

            return identityMultiplier * RelationshipPatienceMultiplier;
        }
    }

    public float TipMultiplier
    {
        get
        {
            float identityMultiplier =
                profile != null ? profile.tipMultiplier :
                archetype != null ? archetype.tipMultiplier : 1f;

            return identityMultiplier * RelationshipTipMultiplier;
        }
    }

    // Mechanical trust stays modest; authored requests and story access are
    // the larger Loyal reward. Walk-ins never receive relationship modifiers.
    private float RelationshipPatienceMultiplier =>
        !IsRegular           ? 1f :
        Relationship <= -2   ? 0.90f :
        Relationship >= 5    ? 1.10f :
        Relationship >= 2    ? 1.05f :
                               1f;

    private float RelationshipTipMultiplier =>
        !IsRegular           ? 1f :
        Relationship <= -2   ? 0.75f :
        Relationship >= 5    ? 1.25f :
        Relationship >= 2    ? 1.15f :
                               1f;

    public WaitingSpot.SpotKind PreferredWaitKind =>
        profile != null ? profile.preferredWaitKind :
        archetype != null ? archetype.preferredWaitKind : WaitingSpot.SpotKind.Loiter;

    // How likely this person is to want a coffee WHILE waiting on a repair.
    // Zero when we know nothing about them, so an unconfigured archetype can't
    // silently flood the machine with orders.
    public float DrinkWishChance =>
        profile != null ? profile.drinkWishChance :
        archetype != null ? archetype.drinkWishChance : 0f;

    public string Say(Beat beat)
    {
        DialogueSet set = ResolveSet();
        if (set == null) return "";

        string[] pool = beat switch
        {
            Beat.Intake     => set.intake,
            Beat.Accepted   => set.accepted,
            Beat.Completed  => set.completed,
            Beat.Declined   => set.declined,
            Beat.Reassured  => set.reassured,
            Beat.StormedOut => set.stormedOut,
            Beat.OrderedDrink => set.orderedDrink,
            _ => null
        };

        // {device} and {fault} let one written line work across the whole
        // roster. Five personalities x seven beats is 35 lines that cover every
        // device-and-fault combination, instead of 35 per combination.
        //
        // {fault} matters more than it looks: it's how the player learns what
        // they're being asked to take on before they press E. A decline can't
        // be a real decision if every job is described identically.
        return set.Pick(pool)
                  .Replace("{device}", deviceName)
                  .Replace("{fault}", faultName);
    }

    private DialogueSet ResolveSet()
    {
        if (profile != null)
        {
            bool warmAvailable = HasLines(profile.warmLines);
            if (Relationship >= 2 && warmAvailable) return profile.warmLines;

            bool returnAvailable = HasLines(profile.returnLines);
            if (HasMetBefore && returnAvailable) return profile.returnLines;

            return profile.lines;
        }
        return archetype != null ? archetype.lines : null;
    }

    private static bool HasLines(DialogueSet set)
    {
        return set != null && set.intake != null && set.intake.Length > 0;
    }
}