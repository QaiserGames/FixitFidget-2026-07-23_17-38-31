using UnityEngine;

// ---------------------------------------------------------------------------
// TAKING IT BACK OFF ACE'S SHELF, FOR HIM (6 Oct 2026; Fixit Fidget > Night > Bins 1 adds it to the shelf)
//
// A place (a trigger) in front of Ace's trophy shelf, behind the counter. When the man at the bins is still waiting
// for a thing that is on Ace's shelf (the night ended with it in Ace's hand, so it went on the shelf; or a save from
// before he existed), "Take Barnaby off the shelf": it's in Ace's hand again (NightCarry), to bring to the bins.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightShelfTake : NightInteractable
{
    [Tooltip("The shelf whose copy of the thing Ace takes in hand.")]
    public TrophyShelf shelf;

    public override bool IsZone => true;

    string Wanted
    {
        get
        {
            NightLedger ledger = SaveManager.Instance != null ? SaveManager.Instance.Night : null;
            string errand = LodgerStory.Errand(ledger);
            return errand.Length > 0 && ledger.OnShelf(errand) ? errand : "";
        }
    }

    protected override bool AvailableTonight => shelf != null && Wanted.Length > 0
        && (NightCarry.Current == null || !NightCarry.Current.Holding);

    // The interactor reads the prompt every frame while Ace is near: made again only when the name changes.
    string promptFor, prompt = "";

    public override string Prompt
    {
        get
        {
            string name = NameOf(Wanted);
            if (!ReferenceEquals(name, promptFor)) { promptFor = name; prompt = $"Take {name} off the shelf"; }
            return prompt;
        }
    }

    // Barnaby, or "the garden gnome" while Ace doesn't know his name.
    static string NameOf(string id)
    {
        NightThing thing = NightThings.Find(id);
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        return thing == null ? "it" : notebook != null && notebook.Knows(thing.id) ? thing.name : thing.unknownName;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        string wanted = Wanted;
        GameObject copy = shelf.CopyOf(wanted);
        NightCarry carry = NightCarry.Ensure();
        if (copy == null || carry == null) return;
        carry.Hold(copy, wanted);
        Sfx.Play("night.take", transform.position);
        NightCycle.Note($"Bring {NameOf(wanted)} to the man at the bins.");
    }
}
