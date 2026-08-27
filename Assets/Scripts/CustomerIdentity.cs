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
    public int Relationship { get; private set; }      // stub until memory lands
    // Regulars have faces. Walk-ins fall back to a silhouette in the UI.
    public Sprite Portrait => profile != null ? profile.portraitNeutral : null;

    private CustomerProfile profile;
    private CustomerArchetype archetype;
    private string deviceName = "thing";

    public void SetupRegular(CustomerProfile p, int relationship = 0)
    {
        profile = p;
        archetype = null;
        DisplayName = p.characterName;
        ThemeColor = p.themeColor;
        Relationship = relationship;
    }

    public void SetupWalkIn(CustomerArchetype a, string name)
    {
        archetype = a;
        profile = null;
        DisplayName = name;
        ThemeColor = a != null ? a.moodColor : Color.white;
        Relationship = 0;
    }

    public void SetDevice(string device)
    {
        if (!string.IsNullOrEmpty(device)) deviceName = device;
    }

    public float PatienceMultiplier =>
        profile != null ? profile.patienceMultiplier :
        archetype != null ? archetype.patienceMultiplier : 1f;

    public float TipMultiplier =>
        profile != null ? profile.tipMultiplier :
        archetype != null ? archetype.tipMultiplier : 1f;

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

        // {device} lets one written line work across the whole roster.
        return set.Pick(pool).Replace("{device}", deviceName);
    }

    private DialogueSet ResolveSet()
    {
        if (profile != null)
        {
            bool warmAvailable = profile.warmLines != null
                              && profile.warmLines.intake != null
                              && profile.warmLines.intake.Length > 0;

            if (Relationship >= 2 && warmAvailable) return profile.warmLines;
            return profile.lines;
        }
        return archetype != null ? archetype.lines : null;
    }
}