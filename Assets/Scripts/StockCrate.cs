using UnityEngine;

// ---------------------------------------------------------------------------
// A CRATE OF CUPS AND BEANS (24 Oct 2026; Designer P's spec §2.5 and §6.6, restock by hand)
//
// Taken from the back shelf (BackShelf: "Take cups and beans"), carried in one hand to the drinks machine's cup stack ("Refill
// the machine": ShopInventory.Refill fills it back to 12 of each from the shelf), or put back on the shelf with one press.
// PlayerCarry carries only a JobBase, so the crate is one: nobody's, no pay, never handed to anyone, never set down on a
// counter or the bench (DropInteractable.Takes and ItemSlotArea refuse it). It is a token: nothing moves until the refill.
// Made in code, a small box in the back shelf's own colours; it goes when it's used or put back. Never saved (a crate in hand
// at closing just goes: the stock never left the shelf).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class StockCrate : JobBase
{
    /// <summary>What the HUD's "in hand" line calls it (PlayerCarry.Summary reads the object's name).</summary>
    public const string Name = "Cups and beans";

    public override JobFamily Family => JobFamily.Cafe;
    public override bool IsComplete => true;
    /// <summary>Never handed to anyone.</summary>
    public override bool CanHandBack => false;

    private void Awake()
    {
        payout = 0;
        restHeight = .09f;
    }

    /// <summary>A crate at <paramref name="at"/>, in <paramref name="look"/>'s colours (the back shelf's own material; null: Unity's default).</summary>
    public static StockCrate Make(Vector3 at, Quaternion facing, Material look, Material lid)
    {
        var root = new GameObject(Name);
        root.transform.SetPositionAndRotation(at, facing);
        Box(root.transform, "Crate", new Vector3(0f, .07f, 0f), new Vector3(.24f, .14f, .17f), look);
        Box(root.transform, "Cup sleeves", new Vector3(-.05f, .16f, 0f), new Vector3(.07f, .06f, .07f), lid);
        Box(root.transform, "Bean bag", new Vector3(.06f, .16f, 0f), new Vector3(.09f, .05f, .10f), look);
        // Solid, for nothing to walk through while it is down for a frame; PlayerCarry switches colliders off while it's held.
        root.AddComponent<BoxCollider>().center = new Vector3(0f, .09f, 0f);
        root.GetComponent<BoxCollider>().size = new Vector3(.24f, .18f, .17f);
        return root.AddComponent<StockCrate>();
    }

    private static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Collider c = part.GetComponent<Collider>();
        if (c != null) Destroy(c);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>The crate in Ace's hands (either hand), or null.</summary>
    public static StockCrate InHand(PlayerCarry carry)
    {
        if (carry == null) return null;
        for (int i = 0; i < carry.Count; i++)
            if (carry.GetItem(i) is StockCrate crate) return crate;
        return null;
    }
}
