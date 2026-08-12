using UnityEngine;

public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;

    [Tooltip("Carried items render smaller so they don't block the isometric view.")]
    [SerializeField] private float carryScale = 0.6f;

    public JobBase Carried { get; private set; }
    public bool IsCarrying => Carried != null;

    private Vector3 originalScale = Vector3.one;
    private Renderer[] carriedRenderers;
    private PlayerInteractor interaction;
    private bool renderersVisible = true;

    private void Awake()
    {
        interaction = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        // The job can die under us (customer stormed out). Unity reports
        // destroyed objects as null, so just let go.
        if (Carried == null) return;

        Carried.transform.position = carryPoint.position;
        Carried.transform.rotation = carryPoint.rotation;

        // Hidden while at a station — you're working, not walking. Stops the
        // item floating at odd heights in the bench and counter views.
        bool shouldBeVisible = interaction == null || !interaction.IsAtStation;
        if (shouldBeVisible != renderersVisible) SetRenderers(shouldBeVisible);
    }

    public void PickUp(JobBase item)
    {
        if (IsCarrying || item == null) return;

        // Tell whatever surface it was on that the slot is free again.
        foreach (DropSpot spot in FindObjectsByType<DropSpot>(FindObjectsSortMode.None))
            spot.Release(item);

        originalScale = item.transform.localScale;
        item.transform.localScale = originalScale * carryScale;

        Carried = item;
        carriedRenderers = item.GetComponentsInChildren<Renderer>();
        renderersVisible = true;

        SetCollidersEnabled(item, false);   // carried items don't block rays or triggers
    }

    // Place at an exact spot (bench slot, counter item spot).
    public void PlaceAt(Transform spot)
    {
        if (!IsCarrying) return;

        SetRenderers(true);      // must come back on before we let go

        Carried.transform.position = spot.position + Vector3.up * Carried.restHeight;
        Carried.transform.rotation = spot.rotation;
        Carried.transform.localScale = originalScale;

        SetCollidersEnabled(Carried, true);

        Carried = null;
        carriedRenderers = null;
    }

    private void SetRenderers(bool on)
    {
        renderersVisible = on;
        if (carriedRenderers == null) return;

        foreach (Renderer r in carriedRenderers)
            if (r != null) r.enabled = on;
    }

    private void SetCollidersEnabled(JobBase item, bool enabled)
    {
        foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
            c.enabled = enabled;
    }
}