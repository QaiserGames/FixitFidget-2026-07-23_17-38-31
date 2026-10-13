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
//
// ---------------------------------------------------------------------------
// 12 OCTOBER 2026 — THE FIRST PURCHASE AT ABOUT HALF A DAY (playtest 4, §10.10)
//
// WHY
//
// The 28 Aug ladder (350/900/2000, 300/800/1800, 900/2400, 600/1500, 300/750,
// 400/1000: $14,000) was priced against $476-963 days. Those days are gone:
// the spawner now brings 3-9 people to the till in a 180 s day, the payouts
// are the realistic ones, and he plays with one pair of hands. His own days
// (7 Oct, the Project's playtest notes and DayLogs/AcesCafeLayout) earned
// $60 / $249 / $203 on Days 1-3; the veteran's median across his days was
// $226 ($182-374). So the cheapest first level, $300, was 1.3 of his days
// away, and both playtesters called it: the shop had nothing to sell on the
// night that should sell an upgrade. His call (12 Oct, 21:00): "level 1 at
// about half a day; I trust your judgement."
//
// THE ARITHMETIC (UpgradeEconomyChecks plays the days through; the figures
// here are its 12 Oct run, in the 180 s day the game plays now)
//
//   Day 1 (Day_01_GraceCamera): Grace's camera ($40, Perfect $50) and her latte
//     ($6), the Officer's coffee ($3), one or two walk-ins (a drink, or a pocket
//     watch at $75/$160), the patrons' $70: a steady player about $160 (a good
//     one $330); a bad Day 1 still clears $120 on the patrons and Grace.
//   Day 2 (Day_02_GraceReturn): her latte, three walk-ins with the phone in the
//     pool: about $270 steady ($390 good); his real Day 2 was $249.
//   Day 3 (Day_03_TheRealGame): seven at the till, four lost to the clock at a
//     steady pace: about $200-215 steady ($380 good); his real Day 3 was $203.
//   Days 4-5: about $180-230 steady, $390 good.
//
//   A realistic early day is therefore $250 (his $249; the veteran's $226; the
//   arithmetic's $200-270). Every number below is a fraction of that day:
//
//     level 1  0.5 day  ($125) for the three tools, so the first choice is his
//              (bench, machine or brush, the same price), 0.4 day ($100) for
//              Bulk Restock (a convenience: it saves $6 a restock), 0.8 day
//              ($200) for Shelf Space and 1.0 day ($250) for Bigger Bench, the
//              structural two, the dearest first levels, never more than a day;
//     level 2  about 2.5 x level 1 (1.0-2.6 days);
//     level 3  about 5.6 x level 1 (2.8 days), the two tools only.
//
//   What that does: the cheapest level 1 is affordable at the FIRST recap in
//   every simulated Day 1 (steady or good); the dearest by the first recap for
//   a good player and the second for a steady one; all six first levels by
//   recap 3-4; the whole catalogue ($4,675) at about recap 12 for a good player
//   and about recap 25 for a steady one (whose repeated Day 5 earns about
//   $180), never before the demo's Day 3 ends (0% of runs). One meaningful
//   purchase a day for the first five days, then one every 1.3 days (level 2),
//   then every 3 days (level 3): the target shape.
//
// WHEN THE DAY GOES TO 420 s (the ship length) the takings roughly double and
// this ladder is too cheap again: measure his days, set the reference day
// below, scale the table, run this menu, then the check. Asset names stay.
//
// THE PASS NO LONGER TOUCHES THE SCENE. On 28 Aug it also rewired
// UpgradeManager's catalogue (and asked for Ctrl+S). All six assets exist and
// are wired now; a data pass should save assets and nothing else, so it only
// reads the catalogue and reports what is missing from it.
// ---------------------------------------------------------------------------

public static class UpgradeEconomyPass
{
    public const string Folder = "Assets/AssetsPrefabs";

    /// <summary>A realistic early day's takings, 12 Oct 2026 (his Day 2: $249; the veteran's median: $226). Every cost
    /// in the table is a fraction of it; the dialog states the days of spend against it.</summary>
    public const int ReferenceDay = 250;

    /// <summary>One upgrade's planned numbers, for the check (UpgradeEconomyChecks) to hold the assets against.</summary>
    public struct Entry
    {
        public string assetName;
        public string displayName;
        public UpgradeType type;
        public float valuePerLevel;
        public int[] costs;
        /// <summary>STRUCTURAL: changes how you play (another tray, another bench slot), not by how much. Priced dearest.</summary>
        public bool structural;
    }

    private struct Plan
    {
        public string assetName;
        public string displayName;
        public string description;
        public UpgradeType type;
        public float valuePerLevel;
        public int[] costs;
        public bool structural;
        public string why;
    }

    private static List<Plan> BuildPlan() => new()
    {
        // ---- the tools: 0.5 / 1.3 / 2.8 days ----

        new Plan
        {
            assetName = "Upgrade_MagneticDriver",
            displayName = "Magnetic Driver",
            description = "Screws come out faster.",
            type = UpgradeType.ScrewSpeed,
            valuePerLevel = 0.30f,
            costs = new[] { 125, 325, 700 },
            why = "The first thing anyone wants. 12 Oct: the same price as the machine and the brush, so the " +
                  "first choice is his; half a realistic day."
        },

        new Plan
        {
            assetName = "Upgrade_FastMachine",
            displayName = "Faster Machine",
            description = "Pulls coffee and latte in less time.",
            type = UpgradeType.BrewSpeed,
            valuePerLevel = 0.25f,
            costs = new[] { 125, 325, 700 },
            why = "The cafe is the half you learn first. 12 Oct: half a day, like the driver."
        },

        new Plan
        {
            assetName = "Upgrade_WideBrush",
            displayName = "Wide Brush",
            description = "Scrub grime away faster.",
            type = UpgradeType.ScrubSpeed,
            valuePerLevel = 0.30f,
            costs = new[] { 125, 300 },
            why = "Two of the phone's four faults are cleaning jobs. 12 Oct: half a day; two levels only, " +
                  "so its second is a touch under the tools' with three."
        },

        // ---- the convenience: 0.4 / 1.0 days ----

        new Plan
        {
            assetName = "Upgrade_BulkRestock",
            displayName = "Bulk Restock",
            description = "More beans and cups per purchase.",
            type = UpgradeType.RestockSize,
            valuePerLevel = 5f,
            costs = new[] { 100, 250 },
            why = "Answers a problem the designer hit in play: ran out of beans on day 4. 12 Oct: it saves $6 a " +
                  "restock and a trip to the Shop, so it is the cheapest thing here, not a tool."
        },

        // ---- the structural two: 0.8 / 2.0 and 1.0 / 2.6 days ----

        new Plan
        {
            assetName = "Upgrade_ShelfSpace",
            displayName = "Shelf Space",
            // Playtest 4 run 2 (11 Oct 2026): the object is the intake tray everywhere else (Playtest 4 › Content › 5).
            description = "One more intake tray at the till.",
            type = UpgradeType.ShelfCapacity,
            valuePerLevel = 1f,
            costs = new[] { 200, 500 },
            structural = true,
            why = "STRUCTURAL. One more job taken before 'the trays are full' turns someone away (the veteran " +
                  "measured it on Day 5). 12 Oct: 0.8 of a day, under the bench, over every tool."
        },

        new Plan
        {
            assetName = "Upgrade_BiggerBench",
            displayName = "Bigger Bench",
            description = "Hold one more device on the bench at once.",
            type = UpgradeType.BenchCapacity,
            valuePerLevel = 1f,
            costs = new[] { 250, 650 },
            structural = true,
            why = "STRUCTURAL. Two devices torn down at once isn't a percentage: it's a different game, and the " +
                  "most expensive thing here. 12 Oct: a whole day for the first level, the dearest first level."
        }
    };

    /// <summary>The ladder as planned, in the catalogue's order. Names, types and costs only (the check reads this).</summary>
    public static List<Entry> PlannedLadder()
    {
        List<Entry> ladder = new();
        foreach (Plan p in BuildPlan())
            ladder.Add(new Entry
            {
                assetName = p.assetName, displayName = p.displayName, type = p.type,
                valuePerLevel = p.valuePerLevel, costs = (int[])p.costs.Clone(), structural = p.structural
            });
        return ladder;
    }

    [MenuItem("Fixit Fidget/Content/7 · Rescale the upgrade economy")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Upgrade economy pass", "Stop Play Mode first: the pass saves assets.", "OK");
            return;
        }

        List<Plan> plan = BuildPlan();

        int total = 0, existing = 0, missing = 0, totalBefore = 0;
        foreach (Plan p in plan)
        {
            foreach (int c in p.costs) total += c;
            UpgradeDefinition u = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"{Folder}/{p.assetName}.asset");
            if (u == null) { missing++; continue; }
            existing++;
            if (u.costPerLevel != null) foreach (int c in u.costPerLevel) totalBefore += c;
        }

        string creates = missing > 0 ? $" and creates {missing} missing" : "";
        if (!EditorUtility.DisplayDialog("Upgrade economy pass",
            $"Rescales {existing} existing upgrade{(existing == 1 ? "" : "s")}{creates}.\n\n" +
            $"Whole catalogue goes from ${totalBefore:n0} to ${total:n0}.\n" +
            $"At a realistic ${ReferenceDay} a day that's about {Mathf.RoundToInt(total / (float)ReferenceDay)} days of full spend " +
            "(12 Oct 2026: the first level at about half a day).\n\n" +
            "Existing assets keep their names — only the numbers change. " +
            "Asset names are save identity. The scene is not touched.",
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
            bool same = !isNew && SameCosts(u.costPerLevel, p.costs);

            u.upgradeName = p.displayName;
            u.description = p.description;
            u.type = p.type;
            u.valuePerLevel = p.valuePerLevel;
            u.costPerLevel = (int[])p.costs.Clone();

            if (isNew) AssetDatabase.CreateAsset(u, path);
            else EditorUtility.SetDirty(u);

            all.Add(u);
            log.Add($"{(isNew ? "NEW " : same ? "same" : "    ")} {p.displayName,-16} {before,-24} -> {Costs(p.costs)}");
        }

        // The catalogue is scene data. The pass reads it and says what is missing; it never edits the scene
        // (12 Oct 2026). Wiring a new asset is a step's job, or one drag in the inspector.
        string wiring;
        UpgradeManager mgr = Object.FindAnyObjectByType<UpgradeManager>();
        if (mgr == null)
        {
            wiring = "No UpgradeManager in the open scene: nothing to compare the catalogue with (the café scene was not open).";
        }
        else
        {
            List<string> unwired = new();
            foreach (UpgradeDefinition u in all)
            {
                bool wired = false;
                if (mgr.Catalogue != null)
                    foreach (UpgradeDefinition c in mgr.Catalogue)
                        if (c == u) { wired = true; break; }
                if (!wired) unwired.Add(u.name);
            }
            wiring = unwired.Count == 0
                ? $"All {all.Count} are in UpgradeManager's catalogue (the scene was not touched)."
                : "NOT in UpgradeManager's catalogue: " + string.Join(", ", unwired) +
                  ". Drag them into GameManager > Upgrade Manager > Catalogue by hand (the pass no longer edits the scene).";
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string text = string.Join("\n", log) +
                      $"\n\nWhole catalogue: ${totalBefore:n0} -> ${total:n0}  (about {total / (float)ReferenceDay:0.0} realistic days of ${ReferenceDay})";
        Debug.Log("[Upgrade economy pass]\n" + text + "\n\n" + wiring +
                  "\n\nNext: Fixit Fidget › Checks › Upgrade economy - the ladder and the first purchase.");
        EditorUtility.DisplayDialog("Upgrade economy pass",
            text + "\n\n" + wiring + "\n\nNext: Checks › Upgrade economy - the ladder and the first purchase.", "OK");
    }

    private static bool SameCosts(int[] a, int[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
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
        rows.Add($"Whole catalogue: ${total:n0}  (about {total / (float)ReferenceDay:0.0} realistic days of ${ReferenceDay})");

        string text = string.Join("\n", rows);
        Debug.Log("[Upgrade catalogue]\n" + text);
        EditorUtility.DisplayDialog("Upgrade catalogue", text, "OK");
    }
}
#endif
