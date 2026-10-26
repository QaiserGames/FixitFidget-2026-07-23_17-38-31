using UnityEngine;

// ---------------------------------------------------------------------------
// A KIND OF FURNITURE (24 Oct 2026; Docs/Studio/sessions/2026-10-23/designs/furniture-spec.md §4.1, his answers of 24 Oct)
//
// His choice of 23 Oct: "Grow the café", "Seats count, décor is looks". A kind is one thing the phone's Shop sells at closing
// (Arrange the café): its name on the card, its prefab (a PlacedPiece at the root, built by Fixit Fidget > Cafe redesign >
// Furniture 2 from the café's own dressed sets, grain and all), its price, its seats, the day it is for sale from, whether it
// goes against a side wall, and its seats' zone for the labs.
//
// THE ASSET'S NAME IS ITS SAVE IDENTITY: never rename it after shipping, as for the upgrades ("upgrade assets must NEVER be
// renamed after shipping", SaveData.cs). A placed piece saves its kind as this asset's name (PlacedPieceData.kind), and a
// save naming a kind that isn't in the catalogue skips that piece with one warning (CafeLayout.Apply).
//
// His prices (24 Oct, r2: "the furniture spec's"): bistro $80, square $90, window counter $150, banquette bay $140, lounge
// corner $160, communal $360; plants $35 (looks only: "seats count, décor is looks", f5). A piece sells back for half its
// price (f3); moving is free. Tier 2 (Marble & Brass) is later (f4): the save keeps every piece's tier from day one.
// ---------------------------------------------------------------------------
[CreateAssetMenu(menuName = "Fixit Fidget/Furniture kind")]
public sealed class FurnitureDefinition : ScriptableObject
{
    [Tooltip("Its name on the Shop's card and the ghost's plate (\"Bistro table\").")]
    public string displayName = "";

    [Tooltip("The piece itself: a prefab with a PlacedPiece at its root (Furniture 2 builds it). Plants: the first variant's.")]
    public GameObject prefab;

    [Tooltip("Plants only: one prefab per kind of plant, chosen with Turn while it's in hand. Empty for everything else.")]
    public GameObject[] variants = new GameObject[0];

    [Tooltip("Plants only: each variant's name, in the same order (\"Monstera\", \"Rubber plant\", \"Fiddle-leaf fig\", \"Olive tree\"). " +
             "Saved with the piece (PlacedPieceData.variant).")]
    public string[] variantNames = new string[0];

    [Tooltip("What it costs at closing, dollars. Selling it back gives half (f3, his answer of 24 Oct).")]
    public int price;

    [Tooltip("How many seats it brings, shown on its card. Furniture 2 checks it against the prefab's own seats.")]
    public int seats;

    [Tooltip("On sale from this day's closing (Day 1: the bistro, the square table, plants; Day 2: the window counter and the bay; " +
             "Day 3: the communal table and the lounge corner).")]
    public int forSaleFromDay = 1;

    [Tooltip("A wall piece (the window counter, the banquette bay): its back against the west or the east wall, sliding along it; " +
             "it isn't turned, it faces the room.")]
    public bool againstAWall;

    [Tooltip("Its seats' zone for the labs and the reports (\"bistro\"). Never used to choose a seat.")]
    public string zone = "";

    /// <summary>What selling it back gives: half its price (f3), rounded down.</summary>
    public int SellBack => Mathf.Max(0, price) / 2;

    /// <summary>A plant (looks only: no seats, a variant to choose).</summary>
    public bool IsPlant => seats == 0 && variants != null && variants.Length > 0;

    /// <summary>The prefab for a variant ("" or an unknown name: the first one, else the main prefab).</summary>
    public GameObject PrefabFor(string variant)
    {
        if (variants != null && variants.Length > 0)
        {
            if (!string.IsNullOrEmpty(variant) && variantNames != null)
                for (int i = 0; i < variantNames.Length && i < variants.Length; i++)
                    if (variantNames[i] == variant && variants[i] != null) return variants[i];
            foreach (GameObject v in variants) if (v != null) return v;
        }
        return prefab;
    }

    /// <summary>The variant after <paramref name="variant"/> (Turn on a plant in hand), wrapping round; "" for a kind without any.</summary>
    public string NextVariant(string variant)
    {
        if (variantNames == null || variantNames.Length == 0) return "";
        int at = System.Array.IndexOf(variantNames, variant);
        return variantNames[(at + 1 + variantNames.Length) % variantNames.Length];
    }

    /// <summary>The first variant's name, or "".</summary>
    public string FirstVariant => variantNames != null && variantNames.Length > 0 ? variantNames[0] : "";
}
