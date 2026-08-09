using UnityEngine;

public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;

    public JobBase Carried { get; private set; }
    public bool IsCarrying => Carried != null;

    private void Update()
    {
        // The job can die under us (customer stormed out). Unity reports
        // destroyed objects as null, so just let go.
        if (Carried == null) return;

        Carried.transform.position = carryPoint.position;
        Carried.transform.rotation = carryPoint.rotation;
    }

    public void PickUp(JobBase item)
    {
        if (IsCarrying || item == null) return;

        // Tell whatever surface it was on that the slot is free again.
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None))
            spot.Release(item);

        Carried = item;
        SetCollidersEnabled(item, false);
    }

    // Place at an exact spot (bench surface, counter item spot).
    public void PlaceAt(Transform spot)
    {
        if (!IsCarrying) return;

        Carried.transform.position = spot.position;
        Carried.transform.rotation = spot.rotation;
        SetCollidersEnabled(Carried, true);
        Carried = null;
    }

    private void SetCollidersEnabled(JobBase item, bool enabled)
    {
        foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
            c.enabled = enabled;
    }
}