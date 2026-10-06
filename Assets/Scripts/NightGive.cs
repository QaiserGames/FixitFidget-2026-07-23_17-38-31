using UnityEngine;

// ---------------------------------------------------------------------------
// HANDING HIM WHAT HE ASKED FOR (6 Oct 2026; NightZeroSet, Fixit Fidget > Night > Bins 1)
//
// A place (a trigger) in front of the dumpster's far half, where the man at the bins stands. With his errand's
// thing in hand (NightCarry), "Give him Barnaby" (or "Give him the garden gnome", the way Ace knows it): the return
// scene plays (Lodger.Receive), and the thing joins his corner.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightGive : NightInteractable
{
    public override bool IsZone => true;

    string Held => NightCarry.Current != null ? NightCarry.Current.HeldId : "";

    protected override bool AvailableTonight => Lodger.Instance != null && Lodger.Instance.CanTake(Held);

    // The interactor reads the prompt every frame while Ace is near: made again only when the name changes.
    string promptFor, prompt = "";

    public override string Prompt
    {
        get
        {
            NightThing thing = NightThings.Find(Held);
            if (thing == null) return "Give it to him";
            Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
            string name = notebook != null && notebook.Knows(thing.id) ? thing.name : thing.unknownName;
            if (!ReferenceEquals(name, promptFor)) { promptFor = name; prompt = "Give him " + name; }
            return prompt;
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (IsAvailable) Lodger.Instance.Receive(Held);
    }
}
