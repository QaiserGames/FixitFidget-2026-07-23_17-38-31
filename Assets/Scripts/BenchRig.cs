using UnityEngine;

public class BenchRig : MonoBehaviour
{
    [SerializeField] private Transform screwBin;
    [SerializeField] private Transform[] traySlots;

    public Transform ScrewBin => screwBin;

    public Transform TraySlot(int index)
    {
        if (traySlots == null || traySlots.Length == 0) return null;
        return traySlots[Mathf.Clamp(index, 0, traySlots.Length - 1)];
    }
}