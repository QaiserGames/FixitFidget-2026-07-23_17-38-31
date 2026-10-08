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

    // ---------- parts off the device ----------
    // Removed screws, covers and a part pinched out of its seat are unparented so rotating the item doesn't drag them
    // around. They stay OWNED here, which is also how we know whether the thing has been put back together: while any
    // of them is off, it is in pieces (the gate above).
    //
    // Loose things of the job's that DON'T count against it: the fresh part waiting in the tray before it is seated
    // (stock, not a piece of the device) and the broken one once the fresh is in (scrap). Owned the same way, so they
    // are cleaned up with the job and the pad's cursor can reach them, but never a reason the device can't be handed back
    // (the bench, v2, 7 Oct: the gate stays reassembly; a part swap not started is a grade, not a gate).

    private readonly List<GameObject> detached = new();
    private readonly List<GameObject> loose = new();

    public bool HasDetachedParts
    {
        get
        {
            RemoveMissing();
            return detached.Count > 0;
        }
    }

    /// <summary>The parts off it right now that it can't be handed back without (screws, covers, a part out of its seat).</summary>
    public IReadOnlyList<GameObject> DetachedParts
    {
        get
        {
            RemoveMissing();
            return detached;
        }
    }

    /// <summary>Everything of the job's lying about the bench: the detached parts, then the stock and scrap. What the
    /// bench's hover and D-pad step through too.</summary>
    public IEnumerable<GameObject> LooseParts
    {
        get
        {
            RemoveMissing();
            foreach (GameObject g in detached) yield return g;
            foreach (GameObject g in loose) yield return g;
        }
    }

    public bool HasDetachedComponent<T>() where T : Component
    {
        RemoveMissing();
        foreach (GameObject part in detached)
            if (part.GetComponent<T>() != null) return true;
        return false;
    }

    private void RemoveMissing()
    {
        for (int i = detached.Count - 1; i >= 0; i--)
            if (detached[i] == null) detached.RemoveAt(i);
        for (int i = loose.Count - 1; i >= 0; i--)
            if (loose[i] == null) loose.RemoveAt(i);
    }

    /// <summary>A piece off the device: it can't be handed back until this is put back (or made scrap).</summary>
    public void RegisterDetached(GameObject part)
    {
        if (part == null) return;
        loose.Remove(part);
        if (!detached.Contains(part)) detached.Add(part);
    }

    public void UnregisterDetached(GameObject part) => detached.Remove(part);

    /// <summary>Stock or scrap of the job's: owned and reachable, never a reason it can't be handed back.</summary>
    public void RegisterLoose(GameObject part)
    {
        if (part == null) return;
        detached.Remove(part);
        if (!loose.Contains(part)) loose.Add(part);
    }

    public void UnregisterLoose(GameObject part) => loose.Remove(part);

    private void OnDestroy()
    {
        foreach (GameObject g in detached) Remove(g);
        foreach (GameObject g in loose) Remove(g);
    }

    private static void Remove(GameObject g)
    {
        if (g == null) return;
        if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);   // the editor's checks build and tear down jobs in edit mode
    }
}
