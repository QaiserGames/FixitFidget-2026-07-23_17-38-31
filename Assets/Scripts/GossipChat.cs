using UnityEngine;

// ---------------------------------------------------------------------------
// "CHAT": E ON A SEATED GUEST WITH GOSSIP (24 Oct 2026; Designer P's spec §2.4; GuestGossip says why)
//
// Made in code on a seated guest who holds a line of gossip (GuestGossip.Deal), and gone once it's told or they get up: a
// small trigger round their chest, so E from above reaches them from beside their table and the first-person crosshair finds
// them (a trigger: the ray looks past it to nothing else, as the props' do). One press: they say the line, the notebook
// learns it, and Ace is free. The HUD gives the button its icon (the prompt is the verb alone, as every prop's is).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class GossipChat : Interactable
{
    /// <summary>What E says on a guest with gossip. PLACEHOLDER COPY.</summary>
    public const string Words = "Chat";
    /// <summary>The trigger's radius round the guest's chest, metres.</summary>
    public const float Radius = .55f;

    /// <summary>The line this guest holds (NotebookEntries' gossip id).</summary>
    public string GossipId { get; private set; } = "";
    /// <summary>Who holds it.</summary>
    public PatronBrain Guest { get; private set; }

    /// <summary>A chat on <paramref name="guest"/> for line <paramref name="id"/>, its trigger <paramref name="height"/> over their feet.</summary>
    public static GossipChat Make(PatronBrain guest, string id, float height)
    {
        if (guest == null) return null;
        var go = new GameObject("Chat (gossip: " + id + ")");
        go.transform.SetParent(guest.transform, false);
        go.transform.localPosition = Vector3.up * height;
        go.layer = 0;   // Default: every interactor query sees it
        SphereCollider trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Radius;
        GossipChat chat = go.AddComponent<GossipChat>();
        chat.GossipId = id ?? "";
        chat.Guest = guest;
        return chat;
    }

    public override bool IsAvailable
    {
        get
        {
            if (Guest == null || !Guest.isActiveAndEnabled || !Guest.IsSeated || Guest.IsLeaving) return false;
            if (GuestGossip.Instance == null) return false;
            DayClock clock = DayClock.Instance;
            if (clock != null && clock.DayOver) return false;
            return NightWalk.Instance == null || !NightWalk.Instance.Active;
        }
    }

    public override string Prompt => Words;

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable) return;
        GuestGossip.Instance.Chat(this);
    }
}
