using System.Collections.Generic;
using UnityEngine;

public enum JobFamily { Mechanical, Cleaning, Software, Human, Bureaucratic }

public abstract class JobBase : MonoBehaviour
{
    [SerializeField] protected int payout = 25;

    [Tooltip("How far above a surface this item's anchor should sit.")]
    public float restHeight = 0.01f;

    public int Payout => payout;
    public abstract JobFamily Family { get; }
    public abstract bool IsComplete { get; }

    public CustomerBrain Owner { get; private set; }
    public void SetOwner(CustomerBrain owner) => Owner = owner;

    // The record this physical item was spawned from.
    public Job Record { get; private set; }

    public void Configure(Job record)
    {
        Record = record;
        if (record != null) payout = record.payout;
    }

    // Card text now comes from the record, not a field on the prefab.
    public string JobCard => Record != null ? Record.faultDescription : "";

    // ---------- detached parts ----------
    // Removed screws and plates are unparented so rotating the item doesn't
    // drag them around. They stay OWNED here, which is also how we know
    // whether the thing has been put back together.

    private readonly List<GameObject> detached = new();

    public bool HasDetachedParts
    {
        get
        {
            for (int i = detached.Count - 1; i >= 0; i--)
                if (detached[i] == null) detached.RemoveAt(i);
            return detached.Count > 0;
        }
    }

    public void RegisterDetached(GameObject part)
    {
        if (part != null && !detached.Contains(part)) detached.Add(part);
    }

    public void UnregisterDetached(GameObject part) => detached.Remove(part);

    private void OnDestroy()
    {
        foreach (GameObject g in detached)
            if (g != null) Destroy(g);
    }
}