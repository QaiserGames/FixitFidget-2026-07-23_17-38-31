using UnityEngine;
 
public class DrinkJob : JobBase
{
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
 
    private void Awake()
    {
        Tint(emptyColor);
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