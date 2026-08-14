using UnityEngine;

public class DrinkJob : JobBase
{
    public override JobFamily Family => JobFamily.Cafe;

    [SerializeField] private Color emptyColor = new Color(0.9f, 0.9f, 0.88f);

    // An empty cup isn't servable. Only a brewed one is.
    public override bool IsComplete => Drink != null;

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