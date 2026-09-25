using System;
using UnityEngine;

/// <summary>
/// A traffic car whose original (CC0 Kenney) body was swapped for a purchased
/// POLYGON City body by the city pass. The StreetLife entry - route, lane,
/// stops, spacing - is untouched; only the body and its wheels changed.
///
/// The purchased art is never in the public repository. When it is missing
/// (a fresh clone), the POLYGON body has no renderers left, so on Awake this
/// puts the original body and wheels back before StreetLife reads them, and
/// the car looks exactly as it did before the pass.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class CityCarBody : MonoBehaviour
{
    [SerializeField] private GameObject polygonBody;
    [SerializeField] private GameObject originalBody;
    [SerializeField] private Transform[] originalWheels = Array.Empty<Transform>();
    [SerializeField] private float originalWheelRadius = .25f;
    [SerializeField] private float originalVehicleLength = 4.2f;
    [SerializeField] private Vector3 originalWheelAxis = Vector3.right;

    public GameObject PolygonBody => polygonBody;
    public GameObject OriginalBody => originalBody;
    public Transform[] OriginalWheels => originalWheels;
    public float OriginalWheelRadius => originalWheelRadius;
    public float OriginalVehicleLength => originalVehicleLength;
    public Vector3 OriginalWheelAxis => originalWheelAxis;

    public void Configure(GameObject polygon, GameObject original, Transform[] wheels, float wheelRadius, float vehicleLength, Vector3 wheelAxis)
    {
        originalWheelAxis = wheelAxis;
        polygonBody = polygon;
        originalBody = original;
        originalWheels = wheels ?? Array.Empty<Transform>();
        originalWheelRadius = wheelRadius;
        originalVehicleLength = vehicleLength;
    }

    private void Awake()
    {
        if (polygonBody != null && polygonBody.GetComponentInChildren<Renderer>(true) != null) return;
        // Purchased body missing: fall back to the original one.
        if (polygonBody != null) polygonBody.SetActive(false);
        if (originalBody != null) originalBody.SetActive(true);
        foreach (StreetLife life in FindObjectsByType<StreetLife>(FindObjectsInactive.Include))
            foreach (StreetLife.Actor actor in life.actors)
                if (actor != null && actor.actor == transform)
                {
                    actor.wheels = originalWheels;
                    actor.wheelRadius = originalWheelRadius;
                    actor.vehicleLength = originalVehicleLength;
                    actor.wheelAxis = originalWheelAxis;
                }
    }
}
