using UnityEngine;
using Unity.Cinemachine;

public class StationInteractable : Interactable
{
    [SerializeField] private CinemachineCamera stationCamera;
    [SerializeField] private string label = "Work here";
    [SerializeField] private DropSpot dropSpot;

    [Tooltip("Can items be picked up and repaired at this station? Bench yes, counter no.")]
    [SerializeField] private bool isWorkSurface = false;

    public string StationLabel => label;
    public bool IsWorkSurface => isWorkSurface;

    // Only offered to Interact when we're carrying something to put down.
    public override bool IsAvailable
    {
        get
        {
            PlayerCarry c = FindAnyObjectByType<PlayerCarry>();
            return c != null && c.IsCarrying && dropSpot != null;
        }
    }

    public override string Prompt
    {
        get
        {
            PlayerCarry c = FindAnyObjectByType<PlayerCarry>();
            if (c == null || !c.IsCarrying || dropSpot == null) return "";
            if (!dropSpot.CanAccept(c.Carried)) return "No room here";
            return dropSpot.Kind == DropSpot.SpotKind.Counter ? "Return item" : "Set down";
        }
    }

    // Dropping just drops. The player presses F when THEY decide to start working —
    // we don't guess, because "still ferrying" and "ready to work" are both valid.
    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry c = player.GetComponent<PlayerCarry>();
        if (c == null || !c.IsCarrying || dropSpot == null) return;

        Transform point = dropSpot.ResolvePoint(c.Carried);
        if (point == null) return;      // full — the prompt already said so

        c.PlaceAt(point);
    }

    public void ActivateCamera(bool on)
    {
        stationCamera.Priority = on ? 20 : 0;
    }

    // Always arrive facing forward, never at whatever angle we left it.
    public void ResetView()
    {
        if (stationCamera == null) return;

        var panTilt = stationCamera.GetComponent<CinemachinePanTilt>();
        if (panTilt == null) return;

        panTilt.PanAxis.Value = panTilt.PanAxis.Center;
        panTilt.TiltAxis.Value = panTilt.TiltAxis.Center;
    }
}