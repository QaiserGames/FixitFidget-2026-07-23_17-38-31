using UnityEngine;

public class DropInteractable : Interactable
{
    private DropSpot spot;

    private void Awake()
    {
        spot = GetComponent<DropSpot>();
    }

    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = FindAnyObjectByType<PlayerCarry>();
            return carry != null && carry.IsCarrying;
        }
    }

    public override string Prompt =>
        spot != null && spot.Kind == DropSpot.SpotKind.Counter ? "Return item" : "Set down";

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null || !carry.IsCarrying) return;

        Transform point = spot.ResolvePoint(carry.Carried);
        carry.PlaceAt(point);
    }
}