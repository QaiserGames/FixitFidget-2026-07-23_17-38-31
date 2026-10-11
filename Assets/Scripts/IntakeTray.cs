using System.Collections.Generic;
using UnityEngine;

// THE INTAKE TRAY (playtest 4 fixes, 10 Oct 2026).
//
// The first-hour playtester: "The intake shelf has no object of its own: devices lie on the bare stone in front of the
// till ... but the card says 'pick up ... from the intake shelf'." So each intake slot now has a tray of its own: a
// felt-lined chestnut tray (CK_IntakeTray, Tools/Blender/cafe_intake.py), walled on the customers' side and open on
// Ace's, where an accepted device waits for the bench. Fixit Fidget › Playtest 4 › Tray › 1 puts one under each slot of
// the IntakeShelf's ItemSlotArea, as the slot's child, and adds this component.
//
// What this does: a slot the player hasn't bought yet shows no tray. ItemSlotArea keeps every slot transform active and
// only counts the first (base + Shelf Space) of them as usable, so the tray hides its own renderers until its slot is
// usable: the three base slots from Day 1, the fourth and fifth when Shelf Space buys them (the tray appears the frame
// the upgrade lands, or as the save loads). The rule is ItemSlotArea's own (index < base + extra shelf slots); the base
// count is copied here by the step from the shelf's ItemSlotArea (its field is private), and the step's "0 - Check
// first" says if the two ever disagree.
//
// What it never does: no collider (the device on it keeps E, the crosshair and the aim help), no say in where a device
// lands (PlacementJitter puts it on the slot, as before), nothing at night that it doesn't do by day (it's a thing on the
// counter: its trays stay out after closing).
public class IntakeTray : MonoBehaviour
{
    [Tooltip("This tray's slot: its index in the IntakeShelf's ItemSlotArea slots (0 = the first used). Set by the step.")]
    [SerializeField] private int slotIndex;

    [Tooltip("The IntakeShelf's ItemSlotArea base slots (open from Day 1), copied by the step: its own field is private.")]
    [SerializeField] private int baseSlots = 3;

    private Renderer[] renderers;
    private int shownState = -1;            // -1 not yet decided, 0 hidden, 1 shown

    private static readonly List<IntakeTray> all = new List<IntakeTray>();     // the enabled trays

    /// <summary>Its slot's index in the intake's ItemSlotArea.</summary>
    public int SlotIndex => slotIndex;

    /// <summary>Is its slot usable now (a base slot, or one Shelf Space has bought)?</summary>
    public bool IsOpen => slotIndex < baseSlots + ExtraShelfSlots;

    private static int ExtraShelfSlots => UpgradeManager.Instance != null ? UpgradeManager.Instance.ExtraShelfSlots : 0;

    /// <summary>
    /// The shown tray nearest to a point (e.g. a device's position), or null if none is out: for a marker that points at
    /// "the intake tray" the card names.
    /// </summary>
    public static IntakeTray Nearest(Vector3 point)
    {
        IntakeTray best = null;
        float bestDistance = float.MaxValue;
        foreach (IntakeTray tray in all)
        {
            if (tray == null || tray.shownState != 1) continue;
            float d = (tray.transform.position - point).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = tray; }
        }
        return best;
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        shownState = -1;
        Refresh();
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    // Every frame, because the upgrade can land at any time (bought on the recap phone, restored from a save) and the
    // test is two integer reads.
    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        int want = IsOpen ? 1 : 0;
        if (want == shownState) return;
        shownState = want;
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = want == 1;
    }
}
