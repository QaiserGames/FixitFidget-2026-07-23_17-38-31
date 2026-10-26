using UnityEngine;

// ---------------------------------------------------------------------------
// THE FURNITURE CATALOGUE (24 Oct 2026; furniture-spec.md §4.1, §4.10)
//
// The kinds the phone's Shop sells, in the order the Arrange strip shows them, and the ghost's see-through material. One
// asset (Assets/Data/Furniture/Furniture catalogue.asset), made by Fixit Fidget > Cafe redesign > Furniture 1 and filled by
// Furniture 2; CafeLayout holds it. The ghost's material is an asset Furniture 2 makes, so nothing calls Shader.Find in a
// build (a shader found by name is stripped from a build unless something references it).
// ---------------------------------------------------------------------------
public sealed class FurnitureCatalogue : ScriptableObject
{
    [Tooltip("The kinds, in the strip's order: by the day they come on sale (the tables first on Day 1, then the plant), then by price.")]
    public FurnitureDefinition[] kinds = new FurnitureDefinition[0];

    [Tooltip("The ghost's see-through material (URP Lit, transparent): Furniture 2 makes it. Without it the ghost draws its own.")]
    public Material ghostMaterial;

    /// <summary>A kind by its asset name (the save identity); null if none.</summary>
    public FurnitureDefinition Find(string kind)
    {
        if (string.IsNullOrEmpty(kind) || kinds == null) return null;
        foreach (FurnitureDefinition k in kinds)
            if (k != null && k.name == kind) return k;
        return null;
    }
}
