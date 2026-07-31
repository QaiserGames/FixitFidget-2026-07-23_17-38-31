using UnityEngine;
using Unity.Cinemachine;

public class StationInteractable : Interactable
{
    [SerializeField] private CinemachineCamera stationCamera;
    [SerializeField] private string label = "Work here";

    public override string Prompt => label;

    public override void Interact(PlayerInteractor player)
    {
        player.EnterStation(this);
    }

    public void ActivateCamera(bool on)
    {
        stationCamera.Priority = on ? 20 : 0;
    }
}