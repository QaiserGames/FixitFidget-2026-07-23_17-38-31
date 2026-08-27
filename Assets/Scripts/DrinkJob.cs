using UnityEngine;
 
public class DrinkJob : JobBase
{
    // ---------- the live cup registry ----------
    //
    // Every cup in the world, so "is there already a drink for this person?"
    // is a walk over a handful of cups instead of a full-scene scan per
    // customer per frame. Same self-registering pattern WaitingSpot uses.
    //
    // This exists because AwaitingDrink used to trust a bool. See the comment
    // on CustomerBrain.AwaitingDrink for the bug that caused.
    private static readonly System.Collections.Generic.List<DrinkJob> live = new();

    public static System.Collections.Generic.IReadOnlyList<DrinkJob> Live => live;

    // A static list survives between play sessions when Enter Play Mode Options
    // skips domain reload, so run two would start holding run one's destroyed
    // cups. Same trap WaitingArea hit.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => live.Clear();

    private void OnEnable()  { if (!live.Contains(this)) live.Add(this); }
    private void OnDisable() { live.Remove(this); }

    /// <summary>Is a cup already spoken for by this customer — brewing or brewed?</summary>
    public static bool ExistsFor(CustomerBrain customer)
    {
        if (customer == null) return false;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            DrinkJob d = live[i];
            if (d == null) { live.RemoveAt(i); continue; }
            if (d.Owner == customer) return true;
        }
        return false;
    }

    public override JobFamily Family => JobFamily.Cafe;
 
    [SerializeField] private Color emptyColor = new Color(0.9f, 0.9f, 0.88f);
 
    // An empty cup isn't servable. Only a brewed one is.
    public override bool IsComplete => Drink != null;
 
    // A coffee has no partial credit — it's brewed or it isn't. There's no
    // "60% of a latte", so drinks never enter the grading system.
    public override float Quality => Drink != null ? 1f : 0f;
    public override bool CanHandBack => Drink != null;
    // True while sitting in the machine. Stops the player snatching it mid-brew.
    public bool Locked { get; set; }
 
    public DrinkDefinition Drink { get; private set; }
    public bool IsEmpty => Drink == null;
 
    // Was this cup ever claimed by someone? Used to notice the moment they
    // leave without it.
    private bool hadOwner;

    private void Awake()
    {
        Tint(emptyColor);
    }

    private void Update()
    {
        // THE ORPHANED LATTE. CanReceiveDrink deliberately lets any latte go to
        // anyone who wants a latte — including one abandoned by a customer who
        // stormed off. But the cup kept the dead customer's job number and
        // colour, so handing it to the next person delivered a drink tagged
        // with a stranger's ticket. Correct payment, and it reads as a bug.
        //
        // Once the owner is gone the cup stops belonging to anyone: badge off,
        // owner cleared. It's just a latte now, and anyone who wants a latte
        // can have it.
        if (!hadOwner)
        {
            if (Owner != null) hadOwner = true;
            return;
        }

        if (Owner != null) return;

        hadOwner = false;
        SetOwner(null);

        JobMarker marker = GetComponentInChildren<JobMarker>(true);
        if (marker != null) marker.Hide();
    }
 
    public void SetDrink(DrinkDefinition drink)
    {
        Drink = drink;
        if (drink != null) Tint(drink.cupColor);
    }
 
    private void Tint(Color c)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.material.color = c;
    }
}