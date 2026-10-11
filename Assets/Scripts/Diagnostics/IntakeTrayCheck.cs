using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE INTAKE TRAYS IN PLAY (playtest 4 fixes, 10 Oct 2026; Lead 6). A lab check, started by
//   Fixit Fidget > Playtest 4 > Tray > 2 - The trays in play: Shelf Space shows its trays (lab, drives itself)
// in a lab session on a fresh Day 1 (the lab's own save; your playtest save is not used).
//
// What it proves: the trays Tray 1 put under the intake slots show only for the slots the player can use. On a fresh game
// the base slots' trays show and the upgrade slots' don't; each Shelf Space level shows one more, the frame it lands; and
// taking the levels back hides them again. No tray has a collider (the device on it keeps E and the crosshair). It sets
// the upgrade's level the way a save restores it (UpgradeManager.SetLevel) and puts the level back before it reports.
// Photos and report.txt go to Logs/Playtest4Tray/trays-in-play-<time>/. Stop Play when it reports.
// ---------------------------------------------------------------------------
public sealed class IntakeTrayCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Playtest4.TrayCheck";
    const string ShelfSpace = "Upgrade_ShelfSpace";      // the upgrade's asset name (its save identity)

    protected override string Title => "The intake trays in play (Shelf Space)";
    protected override string Tag => "[Tray check]";

    UpgradeDefinition shelfSpace;
    int levelWas = -1;

#if UNITY_EDITOR
    // Asked for by the editor (Playtest4TraySteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Tray check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Tray check (this Play session only)");
        var check = go.AddComponent<IntakeTrayCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Playtest4Tray",
            $"trays-in-play-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    protected override IEnumerator Run()
    {
        yield return Until(() => UpgradeManager.Instance != null && IntakeShelf.Instance != null
                                 && FindObjectsByType<IntakeTray>(FindObjectsInactive.Include).Length > 0, 15f,
                           "the café is up: the upgrades, the intake shelf and its trays");
        if (!lastWait) yield break;
        yield return Frames(3);

        IntakeTray[] trays = FindObjectsByType<IntakeTray>(FindObjectsInactive.Include).OrderBy(t => t.SlotIndex).ToArray();
        Check(trays.Select(t => t.SlotIndex).Distinct().Count() == trays.Length,
              $"{trays.Length} trays, one per slot ({string.Join(", ", trays.Select(t => t.SlotIndex))})");
        Check(trays.All(t => t.GetComponentsInChildren<Collider>(true).Length == 0), "no tray has a collider");
        Check(trays.All(t => t.transform.parent != null && t.GetComponentInParent<IntakeShelf>() == IntakeShelf.Instance),
              "every tray sits under a slot of the intake shelf");

        shelfSpace = UpgradeManager.Instance.FindByName(ShelfSpace);
        Check(shelfSpace != null, $"the upgrade {ShelfSpace} is in the shop's catalogue");
        if (shelfSpace == null) yield break;
        levelWas = UpgradeManager.Instance.LevelOf(shelfSpace);
        int baseOpen = trays.Count(t => t.IsOpen) - UpgradeManager.Instance.ExtraShelfSlots;
        Note($"Shelf Space level {levelWas} at the start (a fresh game: 0); {baseOpen} base slot(s) by the trays.");
        // The shelf's own rule (ItemSlotArea's private baseSlots, read the way a lab may): the trays' copy must agree.
        ItemSlotArea area = IntakeShelf.Instance.GetComponent<ItemSlotArea>();
        FieldInfo field = typeof(ItemSlotArea).GetField("baseSlots", BindingFlags.Instance | BindingFlags.NonPublic);
        int shelfBase = baseOpen;
        if (area != null && field != null)
        {
            shelfBase = (int)field.GetValue(area);
            Check(shelfBase == baseOpen, $"the trays open with the shelf's own base slots ({shelfBase}; the trays say {baseOpen})");
        }
        else Note("ItemSlotArea.baseSlots couldn't be read here; the trays' own copy is used.");

        // From level 0 up to the top level and back: the trays shown follow the slots the player can use.
        for (int level = 0; level <= shelfSpace.MaxLevel; level++)
        {
            UpgradeManager.Instance.SetLevel(shelfSpace, level);
            yield return Frames(2);
            int usable = Mathf.Min(shelfBase + UpgradeManager.Instance.ExtraShelfSlots, trays.Length);     // the shelf's own rule
            int shown = trays.Count(Shown);
            bool right = trays.All(t => Shown(t) == (t.SlotIndex < usable));
            Check(shown == usable && right, $"Shelf Space level {level}: {shown} tray(s) shown, slots 0..{usable - 1} (the usable ones){(right ? "" : ": the wrong ones")}");
            yield return Photo($"{level + 1:00}-shelf-space-level-{level}");
        }
        UpgradeManager.Instance.SetLevel(shelfSpace, 0);
        yield return Frames(2);
        Check(trays.Count(Shown) == Mathf.Min(shelfBase, trays.Length), $"taken back to level 0: {trays.Count(Shown)} tray(s) shown again");
    }

    protected override void Restore()
    {
        if (shelfSpace != null && levelWas >= 0 && UpgradeManager.Instance != null) UpgradeManager.Instance.SetLevel(shelfSpace, levelWas);
    }

    static bool Shown(IntakeTray tray)
    {
        Renderer[] renderers = tray.GetComponentsInChildren<Renderer>(true);
        return renderers.Length > 0 && renderers.All(r => r.enabled);
    }
}
