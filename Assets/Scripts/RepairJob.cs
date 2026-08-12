using UnityEngine;

public class RepairJob : JobBase
{
    public override JobFamily Family => JobFamily.Mechanical;

    private bool hadWorkToDo;

    private void Start()
    {
        // Remember whether this device actually spawned with something wrong.
        hadWorkToDo = GetComponentsInChildren<GrimeSpot>().Length > 0
                   || GetComponentsInChildren<ReplaceablePart>().Length > 0;

        if (!hadWorkToDo)
            Debug.LogWarning($"{name}: spawned with no active fault — check the device's fault Enable Objects.", this);
    }

    public override bool IsComplete
    {
        get
        {
            // A job with no fault can't be "complete" — it's a data error.
            if (!hadWorkToDo) return false;

            if (GetComponentsInChildren<GrimeSpot>().Length > 0) return false;

            foreach (ReplaceablePart r in GetComponentsInChildren<ReplaceablePart>())
                if (!r.IsReplaced) return false;

            if (HasDetachedParts) return false;

            return true;
        }
    }
}