using UnityEngine;

public class DrinkJob : JobBase
{
    public override JobFamily Family => JobFamily.Cafe;

    // A drink is finished the moment it's made — it only needs delivering.
    public override bool IsComplete => true;

    public DrinkDefinition Drink { get; private set; }

    public void SetDrink(DrinkDefinition drink)
    {
        Drink = drink;

        // Tint the cup so you can tell orders apart at a glance.
        if (drink == null) return;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.material.color = drink.cupColor;
    }
}