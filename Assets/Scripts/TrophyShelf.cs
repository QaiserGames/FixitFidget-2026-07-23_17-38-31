using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// ACE'S SHELF (the Night 1 slice; claude/ace-after-dark.md §3.2: "trophies are objects: a shelf of
// shame in Ace's back room, physically on display")
//
// The café has no back room yet, so it's a plain shelf on the back wall behind the counter, where
// the customers don't go. Each slot is one thing Ace can take at night; its copy stands on the shelf
// once the night's ledger has it, by day and by night. Put in the scene by Fixit Fidget > Night >
// Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene.
//
// Since the man at the bins (6 Oct 2026): a thing Ace has given him is in his corner, not here, and a thing in Ace's
// hand (NightCarry) isn't on the shelf while Ace holds it.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class TrophyShelf : MonoBehaviour
{
    [Serializable]
    public sealed class Slot
    {
        [Tooltip("The thing's id in NightThings.")]
        public string thingId = "";
        [Tooltip("Its copy on the shelf: hidden until Ace has taken the thing.")]
        public GameObject shown;
    }

    public Slot[] slots = Array.Empty<Slot>();

    NightLedger ledger;
    bool listening;

    /// <summary>How many trophies are on the shelf now.</summary>
    public int Showing { get; private set; }

    void OnEnable()
    {
        Listen();
        Refresh();
    }

    // The save manager may wake after this does.
    void Start()
    {
        Listen();
        Refresh();
    }

    void OnDisable()
    {
        if (listening && ledger != null) ledger.Changed -= Refresh;
        listening = false;
    }

    void Listen()
    {
        if (listening) return;
        ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
        if (ledger == null) return;
        ledger.Changed += Refresh;
        listening = true;
    }

    /// <summary>Every shelf looks again (what's in Ace's hand changed).</summary>
    public static void RefreshAll()
    {
        foreach (TrophyShelf shelf in FindObjectsByType<TrophyShelf>(FindObjectsInactive.Exclude)) shelf.Refresh();
    }

    /// <summary>The shelf's copy of <paramref name="thingId"/> (shown or not), or null: what Ace takes in hand from it.</summary>
    public GameObject CopyOf(string thingId)
    {
        foreach (Slot slot in slots)
            if (slot != null && slot.thingId == thingId && slot.shown != null) return slot.shown;
        return null;
    }

    public void Refresh()
    {
        Showing = 0;
        NightCarry carry = NightCarry.Current;
        foreach (Slot slot in slots)
        {
            if (slot == null || slot.shown == null) continue;
            bool on = ledger != null && ledger.OnShelf(slot.thingId) && (carry == null || carry.HeldId != slot.thingId);
            if (slot.shown.activeSelf != on) slot.shown.SetActive(on);
            if (on) Showing++;
        }
    }
}
