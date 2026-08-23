using System.Collections.Generic;
using UnityEngine;

public enum JobFamily { Mechanical, Cleaning, Software, Human, Bureaucratic, Cafe }

// How well the job was actually done. Only ever append to this — it's headed
// for save data eventually.
public enum JobGrade { Rejected, Passable, Good, Perfect }

public abstract class JobBase : MonoBehaviour
{
    [SerializeField] protected int payout = 25;

    [Tooltip("How far above a surface this item's anchor should sit.")]
    public float restHeight = 0.01f;

    public int Payout => payout;
    public abstract JobFamily Family { get; }
    public abstract bool IsComplete { get; }

    // ---------- quality ----------
    //
    // THE CHANGE: handing back used to require IsComplete — every fault
    // resolved. So the only way to do badly was to be SLOW. In a game whose
    // antagonist is the clock, "do it properly or rush it?" is the most
    // interesting question available, and it was structurally impossible to ask.
    //
    // Now the two are separated:
    //   CanHandBack  — is it in one piece? A hard gate. Physical, obvious.
    //   Quality      — how much did you actually fix? A grade, not a gate.
    //
    // Reassembly is visible, so the gate never feels arbitrary. Fault-clearing
    // is the quality axis. Neither needs explaining to the player.

    /// <summary>0 = nothing fixed, 1 = everything fixed.</summary>
    public virtual float Quality => IsComplete ? 1f : 0f;

    /// <summary>You can't hand someone a device in pieces.</summary>
    public virtual bool CanHandBack => !HasDetachedParts;

    public JobGrade Grade
    {
        get
        {
            float q = Quality;
            if (q >= 0.999f) return JobGrade.Perfect;
            if (q >= 0.66f)  return JobGrade.Good;
            if (q > 0f)      return JobGrade.Passable;
            return JobGrade.Rejected;
        }
    }

    public static float PayMultiplier(JobGrade grade) => grade switch
    {
        JobGrade.Perfect  => 1.25f,
        JobGrade.Good     => 1f,
        JobGrade.Passable => 0.6f,
        _                 => 0f
    };

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