using UnityEngine;
using Unity.Cinemachine;

public class StationInteractable : Interactable
{
    [SerializeField] private CinemachineCamera stationCamera;
    [SerializeField] private string label = "Work here";
    [SerializeField] private DropSpot dropSpot;

    private PlayerCarry Carry => FindAnyObjectByType<PlayerCarry>();

    public override string Prompt
    {
        get
        {
            PlayerCarry c = Carry;
            PlayerInteractor p = FindAnyObjectByType<PlayerInteractor>();
            bool onFloor = p == null || !p.IsAtStation;

            if (onFloor && c != null && c.IsCarrying && dropSpot != null)
            {
                if (!dropSpot.CanAccept(c.Carried)) return "No room here";
                return dropSpot.Kind == DropSpot.SpotKind.Counter ? "Return item" : "Set down";
            }

            return label;
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry c = player.GetComponent<PlayerCarry>();

        if (!player.IsAtStation && c != null && c.IsCarrying && dropSpot != null)
        {
            Transform point = dropSpot.ResolvePoint(c.Carried);
            if (point == null) return;      // full, prompt already said so
            c.PlaceAt(point);
            return;
        }

        player.EnterStation(this);
    }

    public void ActivateCamera(bool on)
    {
        stationCamera.Priority = on ? 20 : 0;
    }
}