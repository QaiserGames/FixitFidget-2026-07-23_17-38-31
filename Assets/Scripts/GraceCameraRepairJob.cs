using UnityEngine;

// Existing scrub/tweezers controls and normal task grades; a clean camera with
// a broken shutter cannot produce a usable photograph. The strap is preserved
// scenery, never a replacement task or an invisible bonus objective.
public class GraceCameraRepairJob : RepairJob
{
    [SerializeField] private ReplaceablePart shutter;
    public ReplaceablePart Shutter => shutter;
    public override float Quality => shutter != null && shutter.IsReplaced ? base.Quality : 0f;
}
