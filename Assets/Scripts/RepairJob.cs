using UnityEngine;

public class RepairJob : JobBase
{
    public override JobFamily Family => JobFamily.Mechanical;

    public override bool IsComplete
    {
        get
        {
            if (GetComponentsInChildren<GrimeSpot>().Length > 0) return false;

            foreach (ReplaceablePart r in GetComponentsInChildren<ReplaceablePart>(true))
                if (!r.IsReplaced) return false;

            foreach (RemovablePart p in GetComponentsInChildren<RemovablePart>(true))
                if (p.IsRemoved) return false;

            foreach (Screw s in GetComponentsInChildren<Screw>(true))
                if (s.IsOut) return false;

            return true;
        }
    }
}