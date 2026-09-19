using UnityEngine;

// Existing scrub/tweezers controls and normal task grades; a clean camera with
// a broken shutter cannot produce a usable photograph. The strap is preserved
// scenery, never a replacement task or an invisible bonus objective.
public class GraceCameraRepairJob : RepairJob
{
    [SerializeField] private ReplaceablePart shutter;
    public ReplaceablePart Shutter => shutter;
    public override float Quality => shutter != null && shutter.IsReplaced ? base.Quality : 0f;

    public string TaskSummary
    {
        get
        {
            var remaining = GetComponentsInChildren<GrimeSpot>();
            string cleaning = remaining.Length == 0 ? "Lens and film path clean"
                : "Clean: " + string.Join(", ", System.Array.ConvertAll(remaining,
                    spot => spot.name.Replace(" grime", "").Replace("-", " ").ToLowerInvariant()));
            return cleaning + (shutter != null && shutter.IsReplaced ? " · shutter repaired" : " · replace shutter");
        }
    }
}
