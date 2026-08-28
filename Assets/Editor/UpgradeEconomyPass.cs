#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// UPGRADE ECONOMY PASS — 2026-08-28
//
// THE PROBLEM, MEASURED
//
// Five logged days earned $476 / $963 / $736 / $825 / $792. The entire upgrade
// catalogue cost $1,080. You could own everything before day 2 ended.
//
// That kills the loop this game's progression is supposed to run on:
//
//   "What screwed me today?"  ->  "I kept running out of shelf space."
//   ->  buy the shelf upgrade  ->  "Okay. Tomorrow I'm ready."
//
// The question only exists if you can't afford everything. Scarcity is what
// turns a shopping list into a decision.
//
// This is fallout from making repair prices realistic (a screen swap went from
// $35 to $110) without moving the other side of the ledger.
//
// HALF THE SYSTEM WAS ALSO UNREACHABLE
//
// UpgradeManager implements six types. Only three had assets. ShelfCapacity was
// specced in the Café Step 1 pass and never created, so the "No room on the
// shelf" squeeze had no relief valve at all.
//
// THE TARGET SHAPE
//
// Roughly one meaningful purchase per day early on, slowing as levels climb.
// First levels ~half a day's takings; later levels multiple days. Structural
// upgrades — the ones that change HOW you play rather than by how much — cost
// the most, because they're worth the most.
//
// ⚠️ ASSET NAMES ARE SAVE IDENTITY. This tool never renames an existing asset,
// only edits its numbers. Renaming one post-ship would orphan every save.
// ---------------------------------------------------------------------------

public static class UpgradeEconomyPass
{
    private const string Folder = "Assets/AssetsPrefabs";

    private struct Plan
    {
        public string assetName;
        public string displayName;
        public string description;
        public UpgradeType type;
        public float valuePerLevel;
        public int[] costs;
        public string why;
    }

    private static List<Plan> BuildPlan() => new()
    {
        // ---- existing assets: numbers only, names untouched ----

        new Plan
        {
            assetName = "Upgrade_MagneticDriver",
            displayName = "Magnetic Driver",
            description = "Screws come out faster.",
            type = UpgradeType.ScrewSpeed,
            valuePerLevel = 0.30f,
            costs = new[] { 350, 900, 2000 },
            why = "The first thing anyone wants. Cheapest entry, and a third " +
                  "level added so it stays worth saving for."
        },

        new Plan
        {
            assetName = "Upgrade_FastMachine",
            displayName = "Faster Machine",
            description = "Pulls coffee and latte in less time.",
            type = UpgradeType.BrewSpeed,
            valuePerLevel = 0.25f,
            costs = new[] { 300, 800, 1800 },
            why = "Cheap to start because the cafe is the half you learn first."
        },

        new Plan
        {
            assetName = "Upgrade_BiggerBench",
            displayName = "Bigger Bench",
            description = "Hold one more device on the bench at once.",
            type = UpgradeType.BenchCapacity,
            valuePerLevel = 1f,
            costs = new[] { 900, 2400 },
            why = "STRUCTURAL. Two devices torn down at once isn't a percentage " +
                  "— it's a different game, and the most expensive thing here."
        },

        // ---- missing assets ----

        new Plan
        {
            assetName = "Upgrade_ShelfSpace",
            displayName = "Shelf Space",
            description = "One more slot on the intake shelf.",
            type = UpgradeType.ShelfCapacity,
            valuePerLevel = 1f,
            costs = new[] { 600, 1500 },
            why = "Specced in the Cafe Step 1 pass and never built, so 'No room " +
                  "on the shelf' has been a squeeze with no relief valve."
        },

        new Plan
        {
            assetName = "Upgrade_WideBrush",
            displayName = "Wide Brush",
            description = "Scrub grime away faster.",
            type = UpgradeType.ScrubSpeed,
            valuePerLevel = 0.30f,
            costs = new[] { 300, 750 },
            why = "Newly worth buying: two of the four faults are now cleaning " +
                  "jobs with three grime spots each."
        },

        new Plan
        {
            assetName = "Upgrade_BulkRestock",
            displayName = "Bulk Restock",
            description = "More beans and cups per purchase.",
            type = UpgradeType.RestockSize,
            valuePerLevel = 5f,
            costs = new[] { 400, 1000 },
            why = "Answers a problem the designer hit in play: ran out of beans " +
                  "on day 4 and lost customers to a shortage."
        }
    };

    [MenuItem("Fixit Fidget/Content/7 · Rescale the upgrade economy")]
    public static void Run()
    {
        List<Plan> plan = BuildPlan();

        int total = 0;
        foreach (Plan p in plan) foreach (int c in p.costs) total += c;

        if (!EditorUtility.DisplayDialog("Upgrade economy pass",
            $"Rescales 3 existing upgrades and creates 3 missing ones.\n\n" +
            $"Whole catalogue goes from $1,080 to ${total:n0}.\n" +
            $"At roughly $800 a day that's about {total / 800} days of full spend.\n\n" +
            "Existing assets keep their names — only the numbers change. " +
            "Asset names are save identity.",
            "Do it", "Cancel")) return;

        Directory.CreateDirectory(Folder);

        List<string> log = new();
        List<UpgradeDefinition> all = new();

        foreach (Plan p in plan)
        {
            string path = $"{Folder}/{p.assetName}.asset";

            UpgradeDefinition u = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            bool isNew = u == null;
            if (isNew) u = ScriptableObject.CreateInstance<UpgradeDefinition>();

            string before = isNew ? "new" : Costs(u.costPerLevel);

            u.upgradeName = p.displayName;
            u.description = p.description;
            u.type = p.type;
            u.valuePerLevel = p.valuePerLevel;
            u.costPerLevel = p.costs;

            if (isNew) AssetDatabase.CreateAsset(u, path);
            else EditorUtility.SetDirty(u);

            all.Add(u);
            log.Add($"{(isNew ? "NEW " : "    ")}{p.displayName,-18} {before,-22} -> {Costs(p.costs)}");
        }

        // Put everything in the catalogue, or the new assets exist and are
        // unbuyable — which is exactly how ShelfCapacity ended up missing.
        UpgradeManager mgr = Object.FindAnyObjectByType<UpgradeManager>();
        string wiring;

        if (mgr != null)
        {
            SerializedObject so = new SerializedObject(mgr);
            SerializedProperty arr = so.FindProperty("catalogue");

            Undo.RecordObject(mgr, "Rebuild upgrade catalogue");
            arr.ClearArray();
            arr.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = all[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mgr);

            wiring = $"All {all.Count} wired into UpgradeManager. PRESS CTRL+S.";
        }
        else
        {
            wiring = "No UpgradeManager in the open scene — drag the assets into " +
                     "GameManager > Upgrade Manager > Catalogue by hand.";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string text = string.Join("\n", log);
        Debug.Log("[Upgrade economy pass]\n" + text + "\n\n" + wiring);
        EditorUtility.DisplayDialog("Upgrade economy pass",
            text + $"\n\nWhole catalogue: ${total:n0}\n\n" + wiring, "OK");
    }

    private static string Costs(int[] c)
    {
        if (c == null || c.Length == 0) return "(none)";

        string[] parts = new string[c.Length];
        for (int i = 0; i < c.Length; i++) parts[i] = "$" + c[i];
        return string.Join(" / ", parts);
    }

    [MenuItem("Fixit Fidget/Content/8 · Show the upgrade catalogue")]
    public static void Show()
    {
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDefinition");
        List<string> rows = new();
        int total = 0;

        foreach (string g in guids)
        {
            UpgradeDefinition u = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(
                AssetDatabase.GUIDToAssetPath(g));
            if (u == null) continue;

            int sum = 0;
            if (u.costPerLevel != null) foreach (int c in u.costPerLevel) sum += c;
            total += sum;

            rows.Add($"{u.upgradeName,-18} {u.type,-14} {Costs(u.costPerLevel),-26} (${sum})");
        }

        rows.Sort();
        rows.Add("");
        rows.Add($"Whole catalogue: ${total:n0}");

        string text = string.Join("\n", rows);
        Debug.Log("[Upgrade catalogue]\n" + text);
        EditorUtility.DisplayDialog("Upgrade catalogue", text, "OK");
    }
}
#endif
