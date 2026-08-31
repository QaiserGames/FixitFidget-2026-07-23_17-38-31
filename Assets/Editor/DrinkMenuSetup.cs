#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE DRINK MENU — 2 to 6, 2026-08-30
//
// WHY
//
// Two drinks is not a cafe. Every order was Coffee or Latte, so the espresso
// machine asked the same question all day and "what do they want?" had a coin
// flip for an answer. GDD v4 sets the 1.0 menu at six readable recipes, and
// this is the cheapest breadth available in the whole project: a
// DrinkDefinition is a name, a duration, a price and a colour.
//
// THE NUMBERS, AND WHY THESE ONES
//
//   Coffee          3s   $3    (existing — untouched)
//   Tea             5s   $3
//   Espresso        5s   $4
//   Americano       6s   $4
//   Latte           7s   $6    (existing — untouched)
//   Hot Chocolate   9s   $5
//
// Coffee and Latte are the tuned anchors at each end and are deliberately not
// modified — the new four slot BETWEEN them, so the spread widens without
// invalidating anything already measured in the day logs.
//
// The spread is the point: 3s to 9s. The machine holds one cup and refuses
// everything else while brewing, so a hot chocolate during a rush costs three
// coffees' worth of machine time. That is the decision the cafe should be
// asking for, and with two drinks it could never be asked.
//
// ⚠️ WHY TEA DOESN'T "PARK"
//
// GDD Part 6 describes tea as 5s + 20s idle — the one drink that gives time
// back. It can't, yet. EspressoMachine holds a single loadedCup and every
// other order is refused while IsBrewing, so a 25-second tea wouldn't hand
// time back, it would lock your only machine and starve the queue.
//
// That mechanic needs the multi-slot free-brewing change (see
// claude_free-brewing-spec.md), where finished cups wait on the machine's
// cup-warmer top and the machine itself is free again. Tea ships at 5s until
// then. This is a deferral, not a cut.
//
// ⚠️ WHY TEA STILL COSTS A BEAN
//
// It's leaves, not beans, and zero-cost tea would be a nice detail. It would
// also mean running out of beans still lets you serve something — a mid-day
// escape hatch from forgetting to restock, which is explicitly not wanted.
// Running out is meant to be the cost of a bad end-of-day decision. Uniform
// cost keeps that intact.
// ---------------------------------------------------------------------------

public static class DrinkMenuSetup
{
    private const string Folder = "Assets/AssetsPrefabs/Drinks";

    private struct Recipe
    {
        public string asset, display;
        public float brew;
        public int price;
        public Color cup;
        public string note;
    }

    private static List<Recipe> Menu() => new()
    {
        new Recipe {
            asset = "Drink_Tea", display = "Tea",
            brew = 5f, price = 3,
            cup = new Color(0.72f, 0.45f, 0.18f),          // amber
            note = "Cheap and quick. Ships at 5s — the 20s steep needs free brewing."
        },
        new Recipe {
            asset = "Drink_Espresso", display = "Espresso",
            brew = 5f, price = 4,
            cup = new Color(0.24f, 0.17f, 0.12f),          // near-black
            note = "Fast, but pricier than drip. The efficient order."
        },
        new Recipe {
            asset = "Drink_Americano", display = "Americano",
            brew = 6f, price = 4,
            cup = new Color(0.42f, 0.29f, 0.18f),          // mid brown
            note = "Middle of everything. The unremarkable one, on purpose."
        },
        new Recipe {
            asset = "Drink_HotChocolate", display = "Hot Chocolate",
            brew = 9f, price = 5,
            cup = new Color(0.45f, 0.26f, 0.18f),          // rich cocoa
            note = "Slowest on the menu. Three coffees of machine time."
        }
    };

    [MenuItem("Fixit Fidget/Content/13 · Build the drink menu")]
    public static void Build()
    {
        // Copy the cup prefab off an existing drink rather than asking for it.
        // Both current drinks share one, and a new DrinkDefinition with a null
        // cupPrefab produces an order nobody can ever fulfil.
        DrinkDefinition template = FindExisting();
        if (template == null || template.cupPrefab == null)
        {
            EditorUtility.DisplayDialog("Drink menu",
                "Couldn't find an existing drink with a cup prefab to copy.\n\n" +
                "Expected Drink_Coffee or Drink_Latte in " + Folder + ".", "OK");
            return;
        }

        List<Recipe> menu = Menu();

        if (!EditorUtility.DisplayDialog("Build the drink menu",
            $"Creates {menu.Count} new drinks in {Folder}, then wires ALL drinks " +
            "into CustomerSpawner.\n\n" +
            "Coffee and Latte are NOT modified — they're the tuned anchors at " +
            "each end of the spread.\n\n" +
            "Scene edit, so Ctrl+Z works. Nothing saves until Ctrl+S.",
            "Build it", "Cancel")) return;

        Directory.CreateDirectory(Folder);

        List<string> log = new();

        foreach (Recipe r in menu)
        {
            string path = $"{Folder}/{r.asset}.asset";

            DrinkDefinition d = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(path);
            bool isNew = d == null;
            if (isNew) d = ScriptableObject.CreateInstance<DrinkDefinition>();

            d.drinkName = r.display;
            d.brewSeconds = r.brew;
            d.price = r.price;
            d.cupsCost = 1;
            d.beansCost = 1;
            d.cupColor = r.cup;
            d.cupPrefab = template.cupPrefab;

            if (isNew) AssetDatabase.CreateAsset(d, path);
            else EditorUtility.SetDirty(d);

            log.Add($"{(isNew ? "NEW " : "    ")}{r.display,-15} {r.brew,4:0}s  ${r.price}   {r.note}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.Add("");
        log.Add(WireSpawner());

        string text = string.Join("\n", log);
        Debug.Log("[Drink menu]\n" + text);
        EditorUtility.DisplayDialog("Drink menu", text + "\n\nPRESS CTRL+S.", "OK");
    }

    private static DrinkDefinition FindExisting()
    {
        foreach (string g in AssetDatabase.FindAssets("t:DrinkDefinition"))
        {
            DrinkDefinition d = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(
                AssetDatabase.GUIDToAssetPath(g));
            if (d != null && d.cupPrefab != null) return d;
        }
        return null;
    }

    // Every drink asset in the project, wired into the spawner. A drink that
    // exists but isn't in this array can never be ordered, which is exactly
    // how three upgrade types sat unbuyable for weeks.
    private static string WireSpawner()
    {
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null)
            return "⚠ No CustomerSpawner in the open scene — wire the drinks by hand.";

        List<DrinkDefinition> all = new();
        foreach (string g in AssetDatabase.FindAssets("t:DrinkDefinition"))
        {
            DrinkDefinition d = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(
                AssetDatabase.GUIDToAssetPath(g));
            if (d != null) all.Add(d);
        }

        // Cheapest first, so the Inspector reads like a menu board.
        all.Sort((a, b) => a.price != b.price
            ? a.price.CompareTo(b.price)
            : a.brewSeconds.CompareTo(b.brewSeconds));

        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty arr = so.FindProperty("drinks");
        if (arr == null) return "⚠ Couldn't find the 'drinks' field on CustomerSpawner.";

        Undo.RecordObject(spawner, "Wire drink menu");

        arr.ClearArray();
        arr.arraySize = all.Count;
        for (int i = 0; i < all.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = all[i];

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);

        List<string> names = new();
        foreach (DrinkDefinition d in all) names.Add(d.drinkName);

        return $"Wired {all.Count} drinks into CustomerSpawner:\n   " +
               string.Join(" · ", names);
    }

    [MenuItem("Fixit Fidget/Content/14 · Show the drink menu")]
    public static void Show()
    {
        List<DrinkDefinition> all = new();
        foreach (string g in AssetDatabase.FindAssets("t:DrinkDefinition"))
        {
            DrinkDefinition d = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(
                AssetDatabase.GUIDToAssetPath(g));
            if (d != null) all.Add(d);
        }

        all.Sort((a, b) => a.brewSeconds.CompareTo(b.brewSeconds));

        List<string> rows = new() { "Drink            brew   price   $/sec   cup  beans" };
        foreach (DrinkDefinition d in all)
        {
            float rate = d.brewSeconds > 0f ? d.price / d.brewSeconds : 0f;
            string flag = d.cupPrefab == null ? "  ⚠ NO CUP PREFAB" : "";
            rows.Add($"{d.drinkName,-15} {d.brewSeconds,4:0}s   ${d.price}    " +
                     $"{rate,5:0.00}     {d.cupsCost}     {d.beansCost}{flag}");
        }

        int spawnerCount = 0;
        CustomerSpawner s = Object.FindAnyObjectByType<CustomerSpawner>();
        if (s != null)
        {
            SerializedProperty p = new SerializedObject(s).FindProperty("drinks");
            spawnerCount = p != null ? p.arraySize : 0;
        }

        rows.Add("");
        rows.Add($"Assets: {all.Count}   ·   wired into spawner: {spawnerCount}");
        if (spawnerCount < all.Count)
            rows.Add("⚠ Some drinks exist but can never be ordered. Run '13'.");

        rows.Add("");
        rows.Add("$/sec is money per second of machine time. The machine holds");
        rows.Add("ONE cup, so a slow drink blocks every other order while it runs.");

        string text = string.Join("\n", rows);
        Debug.Log("[Drink menu]\n" + text);
        EditorUtility.DisplayDialog("Drink menu", text, "OK");
    }
}
#endif
